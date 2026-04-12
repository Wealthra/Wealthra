using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Common.Models.Export;

namespace Wealthra.Api.Controllers
{
    [Authorize]
    public class ExportController : ApiControllerBase
    {
        private readonly IExportService _exportService;

        public ExportController(IExportService exportService)
        {
            _exportService = exportService;
        }

        [HttpPost("pdf")]
        public async Task<IActionResult> ExportToPdf([FromBody] ExportRequestDto request, CancellationToken cancellationToken)
        {
            var pdfBytes = await _exportService.ExportToPdfAsync(request, cancellationToken);
            var filename = request.Filename ?? "report.pdf";
            if (!filename.EndsWith(".pdf")) filename += ".pdf";

            return File(pdfBytes, "application/pdf", filename);
        }

        [HttpPost("excel")]
        public async Task<IActionResult> ExportToExcel([FromBody] ExportRequestDto request, CancellationToken cancellationToken)
        {
            var excelBytes = await _exportService.ExportToExcelAsync(request, cancellationToken);
            var filename = request.Filename ?? "report.xlsx";
            if (!filename.EndsWith(".xlsx")) filename += ".xlsx";

            return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", filename);
        }
    }
}
