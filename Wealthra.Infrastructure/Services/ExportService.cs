using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Common.Models.Export;

namespace Wealthra.Infrastructure.Services
{
    public class ExportService : IExportService
    {
        private readonly HttpClient _httpClient;

        public ExportService(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("ExportClient");
        }

        public async Task<byte[]> ExportToPdfAsync(ExportRequestDto request, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync("export/pdf", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }

        public async Task<byte[]> ExportToExcelAsync(ExportRequestDto request, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync("export/excel", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
    }
}
