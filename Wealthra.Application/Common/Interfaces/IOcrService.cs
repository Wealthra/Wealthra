namespace Wealthra.Application.Common.Interfaces
{
    public interface IOcrService
    {
        Task<string> ExtractTextAsync(Stream imageStream, string language = "eng", CancellationToken cancellationToken = default);
    }
}
