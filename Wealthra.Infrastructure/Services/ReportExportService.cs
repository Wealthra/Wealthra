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

    public byte[] GenerateExcelReport(FinancialReportData data)
    {
        using var workbook = new XLWorkbook();

        // Summary Sheet
        var summarySheet = workbook.Worksheets.Add("Summary");
        summarySheet.Cell(1, 1).Value = "Wealthra Financial Report";
        summarySheet.Cell(2, 1).Value = $"Period: {data.StartDate:yyyy-MM-dd} to {data.EndDate:yyyy-MM-dd}";
        summarySheet.Cell(3, 1).Value = $"Currency: {data.Currency}";

        summarySheet.Cell(5, 1).Value = "Total Income:";
        summarySheet.Cell(5, 2).Value = data.Incomes.Sum(i => i.Amount);
        summarySheet.Cell(6, 1).Value = "Total Expenses:";
        summarySheet.Cell(6, 2).Value = data.Expenses.Sum(e => e.Amount);
        summarySheet.Cell(7, 1).Value = "Net Cash Flow:";
        summarySheet.Cell(7, 2).Value = data.Incomes.Sum(i => i.Amount) - data.Expenses.Sum(e => e.Amount);

        summarySheet.Columns().AdjustToContents();

        if (data.Incomes.Any())
        {
            var incomeSheet = workbook.Worksheets.Add("Incomes");
            incomeSheet.Cell(1, 1).InsertTable(data.Incomes.Select(i => new
            {
                i.TransactionDate, i.Name, i.Amount, i.Method, i.IsRecurring
            }));
            incomeSheet.Columns().AdjustToContents();
        }

        if (data.Expenses.Any())
        {
            var expenseSheet = workbook.Worksheets.Add("Expenses");
            expenseSheet.Cell(1, 1).InsertTable(data.Expenses.Select(e => new
            {
                e.TransactionDate, e.Description, e.CategoryName, e.Amount, e.PaymentMethod, e.IsRecurring
            }));
            expenseSheet.Columns().AdjustToContents();
        }

        if (data.Budgets.Any())
        {
            var budgetSheet = workbook.Worksheets.Add("Budgets");
            budgetSheet.Cell(1, 1).InsertTable(data.Budgets.Select(b => new
            {
                b.CategoryName, b.LimitAmount, b.CurrentAmount, Remaining = b.LimitAmount - b.CurrentAmount, b.PercentageUsed
            }));
            budgetSheet.Columns().AdjustToContents();
        }

        if (data.Goals.Any())
        {
            var goalSheet = workbook.Worksheets.Add("Goals");
            goalSheet.Cell(1, 1).InsertTable(data.Goals.Select(g => new
            {
                g.Name, g.Deadline, g.TargetAmount, g.CurrentAmount, g.ProgressPercentage, g.IsCompleted
            }));
            goalSheet.Columns().AdjustToContents();
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] GeneratePdfReport(FinancialReportData data)
    {
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
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" of ");
                    x.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    private void ComposeHeader(IContainer container, FinancialReportData data)
    {
        container.PaddingBottom(20).Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text("Wealthra Financial Report").FontSize(20).SemiBold().FontColor(Colors.Blue.Darken2);
                column.Item().Text($"Date Range: {data.StartDate:MMM dd, yyyy} - {data.EndDate:MMM dd, yyyy}").FontSize(12);
                column.Item().Text($"Currency: {data.Currency}").FontSize(12);
            });
            row.ConstantItem(100).AlignRight().Text($"{DateTime.UtcNow:MMM dd, yyyy}").FontSize(10);
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
        container.Column(column =>
        {
            column.Item().PaddingBottom(5).Text("Summary").FontSize(14).SemiBold();
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

                table.Cell().Text("Total Income:").SemiBold();
                table.Cell().Text($"{totalIncome:N2} {data.Currency}");

                table.Cell().Text("Total Expenses:").SemiBold();
                table.Cell().Text($"{totalExpense:N2} {data.Currency}");

                table.Cell().Text("Net Cash Flow:").SemiBold();
                table.Cell().Text($"{netCashFlow:N2} {data.Currency}")
                    .FontColor(netCashFlow < 0 ? Colors.Red.Medium : Colors.Green.Medium);
            });
        });
    }

    private void ComposeIncomes(IContainer container, FinancialReportData data)
    {
        container.Column(column =>
        {
            column.Item().PaddingBottom(5).Text("Incomes").FontSize(14).SemiBold();
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
                    header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).Text("Date").SemiBold();
                    header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).Text("Name").SemiBold();
                    header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).Text("Method").SemiBold();
                    header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).AlignRight().Text("Amount").SemiBold();
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
        container.Column(column =>
        {
            column.Item().PaddingBottom(5).Text("Expenses").FontSize(14).SemiBold();
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
                    header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).Text("Date").SemiBold();
                    header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).Text("Category").SemiBold();
                    header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).Text("Description").SemiBold();
                    header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).AlignRight().Text("Amount").SemiBold();
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
        container.Column(column =>
        {
            column.Item().PaddingBottom(5).Text("Budgets").FontSize(14).SemiBold();
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
                    header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).Text("Category").SemiBold();
                    header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).AlignRight().Text("Limit").SemiBold();
                    header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).AlignRight().Text("Spent").SemiBold();
                    header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).AlignRight().Text("% Used").SemiBold();
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
        container.Column(column =>
        {
            column.Item().PaddingBottom(5).Text("Goals").FontSize(14).SemiBold();
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
                    header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).Text("Goal Name").SemiBold();
                    header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).Text("Deadline").SemiBold();
                    header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).AlignRight().Text("Target").SemiBold();
                    header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).AlignRight().Text("Current").SemiBold();
                    header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).AlignRight().Text("Progress").SemiBold();
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