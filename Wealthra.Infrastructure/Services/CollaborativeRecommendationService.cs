using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Recommendations.Models;

namespace Wealthra.Infrastructure.Services
{
    public class CollaborativeRecommendationService : ICollaborativeRecommendationService
    {
        private readonly IApplicationDbContext _context;
        private readonly IMemoryCache _memoryCache;
        private const string CacheKey = "recommendations-peer-profile-v2";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

        public CollaborativeRecommendationService(IApplicationDbContext context, IMemoryCache memoryCache)
        {
            _context = context;
            _memoryCache = memoryCache;
        }

        public async Task<List<CollaborativeSuggestion>> GetSuggestionsAsync(string userId, string language, CancellationToken cancellationToken)
        {
            var normalizedLanguage = language?.Trim().ToLowerInvariant() ?? "en";
            var isTurkish = normalizedLanguage == "tr";
            var categories = await _context.Categories.AsNoTracking().ToListAsync(cancellationToken);
            if (categories.Count == 0)
            {
                return new List<CollaborativeSuggestion>();
            }

            var categoryMap = categories.ToDictionary(
                c => c.Id,
                c => isTurkish ? c.NameTr : c.NameEn);

            var modelData = await _memoryCache.GetOrCreateAsync(CacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheDuration;
                return await BuildModelDataAsync(cancellationToken);
            });

            if (modelData is null)
            {
                return new List<CollaborativeSuggestion>();
            }

            var currentUserProfile = modelData.UserProfiles.GetValueOrDefault(userId)
                                     ?? new Dictionary<int, float>();

            var userIncome = modelData.UserAvgMonthlyIncome.GetValueOrDefault(userId, 0m);
            var bucket = IncomeBucket(userIncome, modelData.Q1, modelData.Q2, modelData.Q3, modelData.UseIncomeBuckets);
            var peerScores = modelData.PeerScoresByIncomeBucket.GetValueOrDefault(bucket)
                             ?? modelData.PeerScoresByIncomeBucket.GetValueOrDefault(1)
                             ?? new Dictionary<int, float>();

            var existingUserCategoryIds = await _context.Expenses
                .AsNoTracking()
                .Where(e => e.CreatedBy == userId)
                .Select(e => e.CategoryId)
                .Distinct()
                .ToHashSetAsync(cancellationToken);

            var suggestions = new List<CollaborativeSuggestion>();
            foreach (var pair in peerScores)
            {
                if (existingUserCategoryIds.Contains(pair.Key))
                {
                    continue;
                }

                var userScore = currentUserProfile.GetValueOrDefault(pair.Key);
                var peerScore = pair.Value;
                var gap = peerScore - userScore;
                if (gap > 0.20f)
                {
                    suggestions.Add(new CollaborativeSuggestion
                    {
                        CategoryId = pair.Key,
                        CategoryName = categoryMap.GetValueOrDefault(pair.Key, isTurkish ? "Kategori" : "Category"),
                        Score = gap,
                        Evidence = isTurkish
                            ? "Benzer aylık gelir bandındaki kullanıcılar bu kategoriye gelirlerinin daha yüksek bir payını ayırıyor."
                            : "Users in a similar monthly income band allocate a higher share of income to this category."
                    });
                }
            }

            return suggestions
                .OrderByDescending(s => s.Score)
                .Take(3)
                .ToList();
        }

        private static int IncomeBucket(decimal avgMonthlyIncome, decimal q1, decimal q2, decimal q3, bool useBuckets)
        {
            if (!useBuckets || avgMonthlyIncome <= 0)
            {
                return 1;
            }

            if (avgMonthlyIncome < q1)
            {
                return 0;
            }

            if (avgMonthlyIncome < q2)
            {
                return 1;
            }

            if (avgMonthlyIncome < q3)
            {
                return 2;
            }

            return 3;
        }

        private static Dictionary<int, float> PeerCategorySharesForUsers(
            IReadOnlyList<ExpenseUserCategoryRow> expenseRows,
            HashSet<string> userFilter,
            Dictionary<string, float> userTotals)
        {
            var filtered = expenseRows.Where(r => userFilter.Contains(r.CreatedBy)).ToList();
            if (filtered.Count == 0)
            {
                return new Dictionary<int, float>();
            }

            return filtered
                .GroupBy(x => x.CategoryId)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var scores = g.Select(x =>
                        {
                            var total = userTotals[x.CreatedBy];
                            return total <= 0 ? 0f : x.Spend / total;
                        });
                        return scores.DefaultIfEmpty(0f).Average();
                    });
        }

        private async Task<ModelData?> BuildModelDataAsync(CancellationToken cancellationToken)
        {
            var expenseAggregates = await _context.Expenses
                .AsNoTracking()
                .GroupBy(e => new { e.CreatedBy, e.CategoryId })
                .Select(g => new
                {
                    g.Key.CreatedBy,
                    g.Key.CategoryId,
                    Spend = (float)g.Sum(x => x.Amount)
                })
                .ToListAsync(cancellationToken);

            var expenseRows = expenseAggregates
                .Select(x => new ExpenseUserCategoryRow(x.CreatedBy, x.CategoryId, x.Spend))
                .ToList();

            if (expenseRows.Count == 0)
            {
                return null;
            }

            var userTotals = expenseRows
                .GroupBy(x => x.CreatedBy)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Spend));

            var userProfiles = expenseRows
                .GroupBy(x => x.CreatedBy)
                .ToDictionary(
                    g => g.Key,
                    g => g.ToDictionary(
                        x => x.CategoryId,
                        x => userTotals[g.Key] <= 0 ? 0f : x.Spend / userTotals[g.Key]));

            var incomeMonths = await _context.Incomes
                .AsNoTracking()
                .GroupBy(i => new { i.CreatedBy, i.TransactionDate.Year, i.TransactionDate.Month })
                .Select(g => new
                {
                    g.Key.CreatedBy,
                    MonthlyTotal = g.Sum(x => x.Amount)
                })
                .ToListAsync(cancellationToken);

            var userAvgMonthlyIncome = incomeMonths
                .GroupBy(x => x.CreatedBy)
                .ToDictionary(
                    g => g.Key,
                    g => g.Average(x => x.MonthlyTotal));

            var incomeValues = userProfiles.Keys
                .Select(id => userAvgMonthlyIncome.GetValueOrDefault(id, 0m))
                .Where(x => x > 0)
                .OrderBy(x => x)
                .ToList();

            var useIncomeBuckets = incomeValues.Count >= 4;
            decimal q1, q2, q3;
            if (useIncomeBuckets)
            {
                q1 = incomeValues[incomeValues.Count / 4];
                q2 = incomeValues[incomeValues.Count / 2];
                q3 = incomeValues[(incomeValues.Count * 3) / 4];
            }
            else
            {
                q1 = q2 = q3 = 0m;
            }

            var allExpenseUsers = userProfiles.Keys.ToHashSet();
            var globalPeerScores = PeerCategorySharesForUsers(expenseRows, allExpenseUsers, userTotals);

            var peerScoresByIncomeBucket = new Dictionary<int, Dictionary<int, float>>();
            if (!useIncomeBuckets)
            {
                peerScoresByIncomeBucket[1] = new Dictionary<int, float>(globalPeerScores);
            }
            else
            {
                for (var b = 0; b < 4; b++)
                {
                    var usersInBucket = userProfiles.Keys
                        .Where(uid => IncomeBucket(userAvgMonthlyIncome.GetValueOrDefault(uid, 0m), q1, q2, q3, true) == b)
                        .ToHashSet();
                    var forBucket = PeerCategorySharesForUsers(expenseRows, usersInBucket, userTotals);
                    peerScoresByIncomeBucket[b] = forBucket.Count > 0
                        ? forBucket
                        : new Dictionary<int, float>(globalPeerScores);
                }
            }

            return new ModelData(userProfiles, userAvgMonthlyIncome, peerScoresByIncomeBucket, q1, q2, q3, useIncomeBuckets);
        }

        private sealed record ExpenseUserCategoryRow(string CreatedBy, int CategoryId, float Spend);

        private sealed class ModelData
        {
            public ModelData(
                Dictionary<string, Dictionary<int, float>> userProfiles,
                Dictionary<string, decimal> userAvgMonthlyIncome,
                Dictionary<int, Dictionary<int, float>> peerScoresByIncomeBucket,
                decimal q1,
                decimal q2,
                decimal q3,
                bool useIncomeBuckets)
            {
                UserProfiles = userProfiles;
                UserAvgMonthlyIncome = userAvgMonthlyIncome;
                PeerScoresByIncomeBucket = peerScoresByIncomeBucket;
                Q1 = q1;
                Q2 = q2;
                Q3 = q3;
                UseIncomeBuckets = useIncomeBuckets;
            }

            public Dictionary<string, Dictionary<int, float>> UserProfiles { get; }
            public Dictionary<string, decimal> UserAvgMonthlyIncome { get; }
            public Dictionary<int, Dictionary<int, float>> PeerScoresByIncomeBucket { get; }
            public decimal Q1 { get; }
            public decimal Q2 { get; }
            public decimal Q3 { get; }
            public bool UseIncomeBuckets { get; }
        }
    }
}
