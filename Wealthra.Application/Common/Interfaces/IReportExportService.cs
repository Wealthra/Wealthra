using Wealthra.Application.Features.Export.Models;

namespace Wealthra.Application.Common.Interfaces;

public interface IReportExportService
{
    byte[] GenerateExcelReport(FinancialReportData data);
    byte[] GeneratePdfReport(FinancialReportData data);
}