using System.Globalization;
using GSLInvoicing.Web.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GSLInvoicing.Web.Services;

public static class InvoicePdfGenerator
{
    private static readonly CultureInfo InvoiceCurrencyCulture = CultureInfo.CreateSpecificCulture("en-NZ");

    public static byte[] Generate(Invoice invoice, AppUser? appUser = null)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var vendor = invoice.Client?.Vendor;
        var contactPhone = appUser?.Phone ?? vendor?.Phone;
        var contactEmail = appUser?.Email ?? vendor?.Email;
        var items = invoice.InvoiceItems.OrderBy(i => i.Id).ToList();
        var subtotal = items.Sum(i => i.Amount);
        var gstTotal = items.Sum(i => i.GST);
        var grandTotal = items.Sum(i => i.Total);

        var dueDate = new DateOnly(invoice.InvoiceDate.Year, invoice.InvoiceDate.Month, 1).AddMonths(1).AddDays(19);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginVertical(30);
                page.MarginHorizontal(34);
                page.DefaultTextStyle(x => x.FontSize(9.5f));

                page.Header().Column(column =>
                {
                    column.Spacing(14);

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            left.Spacing(3);
                            left.Item().Text(vendor?.Name ?? "Vendor").FontSize(20).Bold();

                            foreach (var line in SplitLines(vendor?.Address))
                                left.Item().Text(line);

                            if (!string.IsNullOrWhiteSpace(contactPhone))
                                left.Item().Text($"Phone: {contactPhone}");

                            if (!string.IsNullOrWhiteSpace(contactEmail))
                                left.Item().Text($"Email: {contactEmail}");

                            if (!string.IsNullOrWhiteSpace(vendor?.GSTNumber))
                                left.Item().Text($"GST No: {vendor.GSTNumber}");
                        });

                        row.ConstantItem(210).Background(Colors.Grey.Lighten4).Border(1).BorderColor(Colors.Grey.Lighten1).Padding(12).Column(right =>
                        {
                            right.Spacing(6);
                            right.Item().AlignCenter().Text("TAX INVOICE").FontSize(22).Bold();

                            right.Item().Row(r =>
                            {
                                r.RelativeItem().Text("Invoice No.").SemiBold();
                                r.RelativeItem().AlignRight().Text(invoice.InvoiceNumber);
                            });
                            right.Item().Row(r =>
                            {
                                r.RelativeItem().Text("Invoice Date").SemiBold();
                                r.RelativeItem().AlignRight().Text(invoice.InvoiceDate.ToString("dd MMM yyyy"));
                            });
                            right.Item().Row(r =>
                            {
                                r.RelativeItem().Text("Due Date").SemiBold();
                                r.RelativeItem().AlignRight().Text(dueDate.ToString("dd MMM yyyy"));
                            });
                            right.Item().Row(r =>
                            {
                                r.RelativeItem().Text("PO Number").SemiBold();
                                r.RelativeItem().AlignRight().Text(invoice.PONumber ?? "-");
                            });
                            right.Item().Row(r =>
                            {
                                r.RelativeItem().Text("Contact").SemiBold();
                                r.RelativeItem().AlignRight().Text(invoice.Contact ?? invoice.Client?.Contact ?? "-");
                            });
                        });
                    });
                });

                page.Content().PaddingTop(10).Column(column =>
                {
                    column.Spacing(14);

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Column(billTo =>
                        {
                            billTo.Spacing(3);
                            billTo.Item().Text("Bill To").SemiBold();
                            billTo.Item().Text(invoice.Client?.Name ?? string.Empty).Bold();

                            foreach (var line in GetClientAddressLines(invoice.Client))
                                billTo.Item().Text(line);

                            if (!string.IsNullOrWhiteSpace(invoice.Client?.Contact))
                                billTo.Item().Text($"Attn: {invoice.Client.Contact}");

                            if (!string.IsNullOrWhiteSpace(invoice.Client?.Email))
                                billTo.Item().Text(invoice.Client.Email);
                        });

                        row.ConstantItem(220).Border(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Column(payment =>
                        {
                            payment.Spacing(3);
                            payment.Item().Text("Payment Details").SemiBold();

                            if (!string.IsNullOrWhiteSpace(vendor?.BankDetails))
                            {
                                foreach (var line in SplitLines(vendor.BankDetails))
                                    payment.Item().Text(line);
                            }
                            else
                            {
                                payment.Item().Text("Please use the invoice number as your payment reference.");
                            }
                        });
                    });

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(5);
                            columns.ConstantColumn(55);
                            columns.ConstantColumn(70);
                            columns.ConstantColumn(70);
                            columns.ConstantColumn(60);
                            columns.ConstantColumn(75);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCellStyle).Text("Description").SemiBold();
                            header.Cell().Element(HeaderCellStyle).AlignRight().Text("Hours").SemiBold();
                            header.Cell().Element(HeaderCellStyle).AlignRight().Text("Rate").SemiBold();
                            header.Cell().Element(HeaderCellStyle).AlignRight().Text("Amount").SemiBold();
                            header.Cell().Element(HeaderCellStyle).AlignRight().Text("GST").SemiBold();
                            header.Cell().Element(HeaderCellStyle).AlignRight().Text("Total").SemiBold();
                        });

                        foreach (var item in items)
                        {
                            table.Cell().Element(CellStyle).Text(item.Description ?? string.Empty);
                            table.Cell().Element(CellStyle).AlignRight().Text(item.Hours > 0 ? item.Hours.ToString("0.##") : string.Empty);
                            table.Cell().Element(CellStyle).AlignRight().Text(item.Rate > 0 ? FormatCurrency(item.Rate) : string.Empty);
                            table.Cell().Element(CellStyle).AlignRight().Text(FormatCurrency(item.Amount));
                            table.Cell().Element(CellStyle).AlignRight().Text(FormatCurrency(item.GST));
                            table.Cell().Element(CellStyle).AlignRight().Text(FormatCurrency(item.Total));
                        }
                    });

                    column.Item().AlignRight().Width(260).Border(1).BorderColor(Colors.Grey.Lighten1).Padding(8).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.ConstantColumn(100);
                        });

                        table.Cell().PaddingVertical(4).Text("Subtotal").SemiBold();
                        table.Cell().PaddingVertical(4).AlignRight().Text(FormatCurrency(subtotal));
                        table.Cell().PaddingVertical(4).Text("GST").SemiBold();
                        table.Cell().PaddingVertical(4).AlignRight().Text(FormatCurrency(gstTotal));
                        table.Cell().PaddingTop(6).BorderTop(1).Text("Total Due").Bold();
                        table.Cell().PaddingTop(6).BorderTop(1).AlignRight().Text(FormatCurrency(grandTotal)).Bold();
                    });
                });

                page.Footer().AlignCenter().Text("Invoice generated by Griffin Solutions Ltd.").FontSize(8).FontColor(Colors.Grey.Darken1);
            });
        }).GeneratePdf();
    }

    private static string FormatCurrency(decimal value)
    {
        return value.ToString("C2", InvoiceCurrencyCulture);
    }

    private static IContainer HeaderCellStyle(IContainer container)
    {
        return container
            .Background(Colors.Grey.Lighten3)
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten1)
            .PaddingVertical(7)
            .PaddingHorizontal(4);
    }

    private static IContainer CellStyle(IContainer container)
    {
        return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(6).PaddingHorizontal(4);
    }

    private static IEnumerable<string> SplitLines(string? text)
    {
        return (text ?? string.Empty)
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line));
    }

    private static IEnumerable<string> GetClientAddressLines(Client? client)
    {
        if (client == null)
            yield break;

        if (!string.IsNullOrWhiteSpace(client.Street))
            yield return client.Street;

        if (!string.IsNullOrWhiteSpace(client.Suburb))
            yield return client.Suburb;

        var cityLine = string.Join(" ", new[] { client.City, client.Postcode }.Where(x => !string.IsNullOrWhiteSpace(x)));
        if (!string.IsNullOrWhiteSpace(cityLine))
            yield return cityLine;

        if (!string.IsNullOrWhiteSpace(client.Country))
            yield return client.Country;
    }
}