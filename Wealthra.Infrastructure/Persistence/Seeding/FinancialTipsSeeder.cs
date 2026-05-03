using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Domain.Entities;
using Wealthra.Infrastructure.Persistence;

namespace Wealthra.Infrastructure.Persistence.Seeding
{
    public static class FinancialTipsSeeder
    {
        /// <summary>
        /// Ensures the full tip corpus exists and embeddings match <see cref="ITextEmbeddingService"/> on Topic+Body+Tags.
        /// Idempotent; updates vectors for existing rows so legacy migration data is corrected.
        /// </summary>
        public static async Task EnsureAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        {
            var db = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var embeddings = serviceProvider.GetRequiredService<ITextEmbeddingService>();

            if (!db.Database.IsRelational())
            {
                return;
            }

            foreach (var def in FinancialTipSeedData.All)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var existing = await db.FinancialTips
                    .FirstOrDefaultAsync(f => f.Topic == def.Topic && f.Locale == def.Locale, cancellationToken);

                if (existing is null)
                {
                    existing = new FinancialTip
                    {
                        Topic = def.Topic,
                        Body = def.Body,
                        Locale = def.Locale,
                        Tags = def.Tags
                    };
                    db.FinancialTips.Add(existing);
                    await db.SaveChangesAsync(cancellationToken);
                }
                else if (existing.Body != def.Body || existing.Tags != def.Tags)
                {
                    existing.Body = def.Body;
                    existing.Tags = def.Tags;
                    await db.SaveChangesAsync(cancellationToken);
                }

                var canonical = $"{def.Topic} {def.Body} {def.Tags}";
                var vector = await embeddings.CreateEmbeddingAsync(canonical, cancellationToken);
                var literal = ToVectorLiteral(vector);

                await db.Database.ExecuteSqlRawAsync(
                    """UPDATE "FinancialTips" SET "Embedding" = CAST({0} AS vector) WHERE "Id" = {1}""",
                    literal, existing.Id);
            }
        }

        private static string ToVectorLiteral(float[] vector)
        {
            return $"[{string.Join(",", vector.Select(v => v.ToString(CultureInfo.InvariantCulture)))}]";
        }
    }
}
