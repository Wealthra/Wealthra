using System.Security.Cryptography;
using System.Text;
using Wealthra.Application.Common.Interfaces;

namespace Wealthra.Infrastructure.Services
{
    public class DeterministicTextEmbeddingService : ITextEmbeddingService
    {
        private const int EmbeddingSize = 16;

        public Task<float[]> CreateEmbeddingAsync(string text, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Task.FromResult(new float[EmbeddingSize]);
            }

            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text.Trim().ToLowerInvariant()));
            var vector = new float[EmbeddingSize];

            for (var i = 0; i < EmbeddingSize; i++)
            {
                vector[i] = hash[i] / 255f;
            }

            return Task.FromResult(vector);
        }
    }
}
