using System.Globalization;
using GSLInvoicing.Web.Models.ViewModels;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GSLInvoicing.Web.Services;

public static class StatementPdfGenerator
{
    private static readonly CultureInfo CurrencyCulture = CultureInfo.CreateSpecificCulture("en-NZ");

    public static byte[] Generate(StatementPageViewModel model)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var periodLabel = $"{model.FromDate:yyyy-MM-dd} to {model.ToDate:yyyy-MM-dd}";

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(style => style.FontSize(10));

                page.Header().Column(header =>
                {
                    header.Spacing(6);
                    header.Item().Text("Statement").FontSize(20).Bold();
                    header.Item().Text($"Period: {periodLabel}");
                    header.Item().Text($"Client: {model.ClientName}");
                    if (!string.IsNullOrWhiteSpace(model.ClientContact))
                    {
                        header.Item().Text($"Contact: {model.ClientContact}");
                    }

                    if (!string.IsNullOrWhiteSpace(model.ClientEmail))
                    {
                        header.Item().Text($"Email: {model.ClientEmail}");
                    }
                });

                page.Content().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(84);
                        columns.ConstantColumn(56);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(2);
                        columns.ConstantColumn(85);
                        columns.ConstantColumn(90);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderCell).Text("Date").SemiBold();
                        header.Cell().Element(HeaderCell).Text("Type").SemiBold();
                        header.Cell().Element(HeaderCell).Text("Detail").SemiBold();
                        header.Cell().Element(HeaderCell).Text("Particulars").SemiBold();
                        header.Cell().Element(HeaderCell).Text("Code").SemiBold();
                        header.Cell().Element(HeaderCell).Text("Reference").SemiBold();
                        header.Cell().Element(HeaderCell).AlignRight().Text("Amount").SemiBold();
                        header.Cell().Element(HeaderCell).AlignRight().Text("Balance").SemiBold();
                    });

                    foreach (var row in model.Transactions)
                    {
                        table.Cell().Element(BodyCell).Text(row.TransDate.ToString("yyyy-MM-dd"));
                        table.Cell().Element(BodyCell).Text(row.TransType ?? string.Empty);
                        table.Cell().Element(BodyCell).Text(row.Detail ?? string.Empty);
                        table.Cell().Element(BodyCell).Text(row.Particulars ?? string.Empty);
                        table.Cell().Element(BodyCell).Text(row.Code ?? string.Empty);
                        table.Cell().Element(BodyCell).Text(row.Reference ?? string.Empty);
                        table.Cell().Element(BodyCell).AlignRight().Text(FormatCurrency(row.Amount));
                        table.Cell().Element(BodyCell).AlignRight().Text(FormatCurrency(row.RunningBalance));
                    }
                });

                page.Footer().AlignRight().Text($"Closing Balance: {FormatCurrency(model.ClosingBalance)}").Bold();
            });
        }).GeneratePdf();
    }

    private static IContainer HeaderCell(IContainer container)
    {
        return container
            .Background(Colors.Grey.Lighten3)
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten1)
            .Padding(4);
    }

    private static IContainer BodyCell(IContainer container)
    {
        return container
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten2)
            .Padding(4);
    }

    private static string FormatCurrency(decimal amount)
    {
        return amount.ToString("C2", CurrencyCulture);
    }
}
