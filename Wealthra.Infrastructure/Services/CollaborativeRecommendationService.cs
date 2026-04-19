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
        private const string CacheKey = "recommendations-peer-profile-v1";

        public CollaborativeRecommendationService(IApplicationDbContext context, IMemoryCache memoryCache)
        {
            _context = context;
            _memoryCache = memoryCache;
        }

        public async Task<List<CollaborativeSuggestion>> GetSuggestionsAsync(string userId, CancellationToken cancellationToken)
        {
            var categories = await _context.Categories.AsNoTracking().ToListAsync(cancellationToken);
            if (categories.Count == 0)
            {
                return new List<CollaborativeSuggestion>();
            }

            var categoryMap = categories.ToDictionary(c => c.Id, c => c.NameTr);
            var modelData = await _memoryCache.GetOrCreateAsync(CacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(4);
                return await BuildModelDataAsync(cancellationToken);
            });

            if (modelData is null || !modelData.UserProfiles.TryGetValue(userId, out var currentUserProfile))
            {
                return new List<CollaborativeSuggestion>();
            }

            var existingUserCategoryIds = await _context.Expenses
                .AsNoTracking()
                .Where(e => e.CreatedBy == userId)
                .Select(e => e.CategoryId)
                .Distinct()
                .ToHashSetAsync(cancellationToken);

            var suggestions = new List<CollaborativeSuggestion>();
            foreach (var pair in modelData.PeerCategoryAverageScores)
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
                        CategoryName = categoryMap.GetValueOrDefault(pair.Key, "Kategori"),
                        Score = gap,
                        Evidence = "Benzer gelir grubundaki kullanıcılar bu kategoriye daha fazla pay ayırıyor."
                    });
                }
            }

            return suggestions
                .OrderByDescending(s => s.Score)
                .Take(3)
                .ToList();
        }

        private async Task<ModelData?> BuildModelDataAsync(CancellationToken cancellationToken)
        {
            var expenseRows = await _context.Expenses
                .AsNoTracking()
                .GroupBy(e => new { e.CreatedBy, e.CategoryId })
                .Select(g => new
                {
                    g.Key.CreatedBy,
                    g.Key.CategoryId,
                    Spend = (float)g.Sum(x => x.Amount)
                })
                .ToListAsync(cancellationToken);

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

            var peerCategoryAverageScores = expenseRows
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

            return new ModelData(userProfiles, peerCategoryAverageScores);
        }

        private sealed class ModelData
        {
            public ModelData(Dictionary<string, Dictionary<int, float>> userProfiles, Dictionary<int, float> peerCategoryAverageScores)
            {
                UserProfiles = userProfiles;
                PeerCategoryAverageScores = peerCategoryAverageScores;
            }

            public Dictionary<string, Dictionary<int, float>> UserProfiles { get; }
            public Dictionary<int, float> PeerCategoryAverageScores { get; }
        }
    }
}
