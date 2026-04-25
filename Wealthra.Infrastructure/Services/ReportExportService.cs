using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Export.Models;

namespace Wealthra.Infrastructure.Services;

public class ReportExportService : IReportExportService
{
    public ReportExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private string T(string key, string lang)
    {
        var dict = new Dictionary<string, (string En, string Tr)>
        {
            ["ReportTitle"] = ("Wealthra Financial Report", "Wealthra Finansal Raporu"),
            ["Period"] = ("Period:", "Dönem:"),
            ["Currency"] = ("Currency:", "Para Birimi:"),
            ["DateRange"] = ("Date Range:", "Tarih Aralığı:"),
            ["Summary"] = ("Summary", "Özet"),
            ["TotalIncome"] = ("Total Income:", "Toplam Gelir:"),
            ["TotalExpenses"] = ("Total Expenses:", "Toplam Gider:"),
            ["NetCashFlow"] = ("Net Cash Flow:", "Net Nakit Akışı:"),
            ["Incomes"] = ("Incomes", "Gelirler"),
            ["Expenses"] = ("Expenses", "Giderler"),
            ["Budgets"] = ("Budgets", "Bütçeler"),
            ["Goals"] = ("Goals", "Hedefler"),
            ["Date"] = ("Date", "Tarih"),
            ["Name"] = ("Name", "İsim"),
            ["Method"] = ("Method", "Yöntem"),
            ["Amount"] = ("Amount", "Tutar"),
            ["Category"] = ("Category", "Kategori"),
            ["Description"] = ("Description", "Açıklama"),
            ["Limit"] = ("Limit", "Limit"),
            ["Spent"] = ("Spent", "Harcanan"),
            ["UsedPct"] = ("% Used", "% Kullanım"),
            ["GoalName"] = ("Goal Name", "Hedef Adı"),
            ["Deadline"] = ("Deadline", "Bitiş Tarihi"),
            ["Target"] = ("Target", "Hedef"),
            ["Current"] = ("Current", "Mevcut"),
            ["Progress"] = ("Progress", "İlerleme"),
            ["Page"] = ("Page ", "Sayfa "),
            ["Of"] = (" of ", " / ")
        };

        var isTr = lang.ToLower() == "tr";
        if (dict.TryGetValue(key, out var translation))
        {
            return isTr ? translation.Tr : translation.En;
        }
        return key;
    }

    public byte[] GenerateExcelReport(FinancialReportData data)
    {
        using var workbook = new XLWorkbook();
        var l = data.Language;

        // Summary Sheet
        var summarySheet = workbook.Worksheets.Add(T("Summary", l));
        summarySheet.Cell(1, 1).Value = T("ReportTitle", l);
        summarySheet.Cell(2, 1).Value = $"{T("Period", l)} {data.StartDate:yyyy-MM-dd} - {data.EndDate:yyyy-MM-dd}";
        summarySheet.Cell(3, 1).Value = $"{T("Currency", l)} {data.Currency}";

        summarySheet.Cell(5, 1).Value = T("TotalIncome", l);
        summarySheet.Cell(5, 2).Value = data.Incomes.Sum(i => i.Amount);
        summarySheet.Cell(6, 1).Value = T("TotalExpenses", l);
        summarySheet.Cell(6, 2).Value = data.Expenses.Sum(e => e.Amount);
        summarySheet.Cell(7, 1).Value = T("NetCashFlow", l);
        summarySheet.Cell(7, 2).Value = data.Incomes.Sum(i => i.Amount) - data.Expenses.Sum(e => e.Amount);
        summarySheet.Columns().AdjustToContents();

        // Incomes Sheet
        if (data.Incomes.Any())
        {
            var incomeSheet = workbook.Worksheets.Add(T("Incomes", l));
            var table = incomeSheet.Cell(1, 1).InsertTable(data.Incomes.Select(i => new { i.TransactionDate, i.Name, i.Amount, i.Method }));
            var headers = table.HeadersRow();
            headers.Cell(1).Value = T("Date", l);
            headers.Cell(2).Value = T("Name", l);
            headers.Cell(3).Value = T("Amount", l);
            headers.Cell(4).Value = T("Method", l);
            incomeSheet.Columns().AdjustToContents();
        }

        // Expenses Sheet
        if (data.Expenses.Any())
        {
            var expenseSheet = workbook.Worksheets.Add(T("Expenses", l));
            var table = expenseSheet.Cell(1, 1).InsertTable(data.Expenses.Select(e => new { e.TransactionDate, e.CategoryName, e.Description, e.Amount, e.PaymentMethod }));
            var headers = table.HeadersRow();
            headers.Cell(1).Value = T("Date", l);
            headers.Cell(2).Value = T("Category", l);
            headers.Cell(3).Value = T("Description", l);
            headers.Cell(4).Value = T("Amount", l);
            headers.Cell(5).Value = T("Method", l);
            expenseSheet.Columns().AdjustToContents();
        }

        // Budgets Sheet
        if (data.Budgets.Any())
        {
            var budgetSheet = workbook.Worksheets.Add(T("Budgets", l));
            var table = budgetSheet.Cell(1, 1).InsertTable(data.Budgets.Select(b => new { b.CategoryName, b.LimitAmount, b.CurrentAmount, b.PercentageUsed }));
            var headers = table.HeadersRow();
            headers.Cell(1).Value = T("Category", l);
            headers.Cell(2).Value = T("Limit", l);
            headers.Cell(3).Value = T("Spent", l);
            headers.Cell(4).Value = T("UsedPct", l);
            budgetSheet.Columns().AdjustToContents();
        }

        // Goals Sheet
        if (data.Goals.Any())
        {
            var goalSheet = workbook.Worksheets.Add(T("Goals", l));
            var table = goalSheet.Cell(1, 1).InsertTable(data.Goals.Select(g => new { g.Name, g.Deadline, g.TargetAmount, g.CurrentAmount, g.ProgressPercentage }));
            var headers = table.HeadersRow();
            headers.Cell(1).Value = T("GoalName", l);
            headers.Cell(2).Value = T("Deadline", l);
            headers.Cell(3).Value = T("Target", l);
            headers.Cell(4).Value = T("Current", l);
            headers.Cell(5).Value = T("Progress", l);
            goalSheet.Columns().AdjustToContents();
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] GeneratePdfReport(FinancialReportData data)
    {
        var l = data.Language;
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Element(compose => ComposeHeader(compose, data));
                page.Content().Element(compose => ComposeContent(compose, data));
                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span(T("Page", l));
                    x.CurrentPageNumber();
                    x.Span(T("Of", l));
                    x.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    private void ComposeHeader(IContainer container, FinancialReportData data)
    {
        var l = data.Language;
        container.PaddingBottom(20).Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text(T("ReportTitle", l)).FontSize(20).SemiBold().FontColor(Colors.Blue.Darken2);
                column.Item().Text($"{T("DateRange", l)} {data.StartDate:yyyy-MM-dd} - {data.EndDate:yyyy-MM-dd}").FontSize(12);
                column.Item().Text($"{T("Currency", l)} {data.Currency}").FontSize(12);
            });
            row.ConstantItem(100).AlignRight().Text($"{DateTime.UtcNow:yyyy-MM-dd}").FontSize(10);
        });
    }

    private void ComposeContent(IContainer container, FinancialReportData data)
    {
        container.Column(column =>
        {
            column.Spacing(25);

            column.Item().Element(c => ComposeSummary(c, data));

            if (data.Incomes.Any())
                column.Item().Element(c => ComposeIncomes(c, data));

            if (data.Expenses.Any())
                column.Item().Element(c => ComposeExpenses(c, data));

            if (data.Budgets.Any())
                column.Item().Element(c => ComposeBudgets(c, data));

            if (data.Goals.Any())
                column.Item().Element(c => ComposeGoals(c, data));
        });
    }

    private void ComposeSummary(IContainer container, FinancialReportData data)
    {
        var l = data.Language;
        container.Column(column =>
        {
            column.Item().PaddingBottom(5).Text(T("Summary", l)).FontSize(14).SemiBold();
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                var totalIncome = data.Incomes.Sum(i => i.Amount);
                var totalExpense = data.Expenses.Sum(e => e.Amount);
                var netCashFlow = totalIncome - totalExpense;

                table.Cell().Text(T("TotalIncome", l)).SemiBold();
                table.Cell().Text($"{totalIncome:N2} {data.Currency}");

                table.Cell().Text(T("TotalExpenses", l)).SemiBold();
                table.Cell().Text($"{totalExpense:N2} {data.Currency}");

                table.Cell().Text(T("NetCashFlow", l)).SemiBold();
                table.Cell().Text($"{netCashFlow:N2} {data.Currency}")
                    .FontColor(netCashFlow < 0 ? Colors.Red.Medium : Colors.Green.Medium);
            });
        });
    }

    private void ComposeIncomes(IContainer container, FinancialReportData data)
    {
        var l = data.Language;
        container.Column(column =>
        {
            column.Item().PaddingBottom(5).Text(T("Incomes", l)).FontSize(14).SemiBold();
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(80);
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                table.Header(header =>
                {
                    header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).Text(T("Date", l)).SemiBold();
                    header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).Text(T("Name", l)).SemiBold();
                    header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).Text(T("Method", l)).SemiBold();
                    header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).AlignRight().Text(T("Amount", l)).SemiBold();
                });

                foreach (var inc in data.Incomes.OrderByDescending(x => x.TransactionDate))
                {
                    table.Cell().PaddingVertical(2).Text(inc.TransactionDate.ToString("yyyy-MM-dd"));
                    table.Cell().PaddingVertical(2).Text(inc.Name);
                    table.Cell().PaddingVertical(2).Text(inc.Method);
                    table.Cell().PaddingVertical(2).AlignRight().Text($"{inc.Amount:N2}");
                }
            });
        });
    }

    private void ComposeExpenses(IContainer container, FinancialReportData data)
    {
        var l = data.Language;
        container.Column(column =>
        {
            column.Item().PaddingBottom(5).Text(T("Expenses", l)).FontSize(14).SemiBold();
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(80);
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                table.Header(header =>
                {
                    header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).Text(T("Date", l)).SemiBold();
                    header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).Text(T("Category", l)).SemiBold();
                    header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).Text(T("Description", l)).SemiBold();
                    header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).AlignRight().Text(T("Amount", l)).SemiBold();
                });

                foreach (var exp in data.Expenses.OrderByDescending(x => x.TransactionDate))
                {
                    table.Cell().PaddingVertical(2).Text(exp.TransactionDate.ToString("yyyy-MM-dd"));
                    table.Cell().PaddingVertical(2).Text(exp.CategoryName);
                    table.Cell().PaddingVertical(2).Text(exp.Description);
                    table.Cell().PaddingVertical(2).AlignRight().Text($"{exp.Amount:N2}");
                }
            });
        });
    }

    private void ComposeBudgets(IContainer container, FinancialReportData data)
    {
        var l = data.Language;
        container.Column(column =>
        {
            column.Item().PaddingBottom(5).Text(T("Budgets", l)).FontSize(14).SemiBold();
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                table.Header(header =>
                {
                    header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).Text(T("Category", l)).SemiBold();
                    header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).AlignRight().Text(T("Limit", l)).SemiBold();
                    header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).AlignRight().Text(T("Spent", l)).SemiBold();
                    header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).AlignRight().Text(T("UsedPct", l)).SemiBold();
                });

                foreach (var budget in data.Budgets.OrderByDescending(x => x.PercentageUsed))
                {
                    table.Cell().PaddingVertical(2).Text(budget.CategoryName);
                    table.Cell().PaddingVertical(2).AlignRight().Text($"{budget.LimitAmount:N2}");
                    table.Cell().PaddingVertical(2).AlignRight().Text($"{budget.CurrentAmount:N2}");
                    table.Cell().PaddingVertical(2).AlignRight().Text($"{budget.PercentageUsed:N1}%")
                        .FontColor(budget.PercentageUsed >= 100 ? Colors.Red.Medium : Colors.Black);
                }
            });
        });
    }

    private void ComposeGoals(IContainer container, FinancialReportData data)
    {
        var l = data.Language;
        container.Column(column =>
        {
            column.Item().PaddingBottom(5).Text(T("Goals", l)).FontSize(14).SemiBold();
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.ConstantColumn(80);
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                table.Header(header =>
                {
                    header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).Text(T("GoalName", l)).SemiBold();
                    header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).Text(T("Deadline", l)).SemiBold();
                    header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).AlignRight().Text(T("Target", l)).SemiBold();
                    header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).AlignRight().Text(T("Current", l)).SemiBold();
                    header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).AlignRight().Text(T("Progress", l)).SemiBold();
                });

                foreach (var goal in data.Goals.OrderBy(x => x.Deadline))
                {
                    table.Cell().PaddingVertical(2).Text(goal.Name);
                    table.Cell().PaddingVertical(2).Text(goal.Deadline.ToString("yyyy-MM-dd"));
                    table.Cell().PaddingVertical(2).AlignRight().Text($"{goal.TargetAmount:N2}");
                    table.Cell().PaddingVertical(2).AlignRight().Text($"{goal.CurrentAmount:N2}");
                    table.Cell().PaddingVertical(2).AlignRight().Text($"{goal.ProgressPercentage:N1}%")
                        .FontColor(goal.IsCompleted ? Colors.Green.Medium : Colors.Black);
                }
            });
        });
    }
}