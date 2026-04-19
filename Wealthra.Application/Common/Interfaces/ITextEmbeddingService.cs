namespace Wealthra.Application.Common.Interfaces
{
    public interface ITextEmbeddingService
    {
        Task<float[]> CreateEmbeddingAsync(string text, CancellationToken cancellationToken);
    }
}
