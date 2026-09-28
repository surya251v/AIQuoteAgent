using AIQuoteAgent.API.Data;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AIQuoteAgent.API.Services;

public class QuotationPdfService
{
    private readonly AppDbContext _db;

    public QuotationPdfService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<byte[]?> GenerateAsync(int quoteId)
    {
        var quote = await _db.Quotes
            .Include(q => q.Customer)
            .Include(q => q.Items)
            .ThenInclude(i => i.Service)
            .FirstOrDefaultAsync(q => q.Id == quoteId);

        if (quote == null)
        {
            return null;
        }

        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);

                page.Header()
                    .Text("QUOTATION")
                    .FontSize(28)
                    .Bold();

                page.Content()
                    .PaddingVertical(20)
                    .Column(column =>
                    {
                        column.Item()
                            .Text($"Quote #: {quote.Id}")
                            .FontSize(14);

                        column.Item()
                            .Text($"Date: {quote.CreatedAt:dd-MMM-yyyy}")
                            .FontSize(14);

                        column.Item()
                            .PaddingTop(15)
                            .Text($"Customer: {quote.Customer?.Name}")
                            .FontSize(14)
                            .Bold();

                        column.Item()
                            .Text($"Phone: {quote.Customer?.Phone}")
                            .FontSize(12);

                        column.Item()
                            .Text($"Address: {quote.Customer?.Address}")
                            .FontSize(12);

                        column.Item()
                            .PaddingTop(20)
                            .Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Text("Service").Bold();
                                    header.Cell().Text("Qty").Bold();
                                    header.Cell().Text("Price").Bold();
                                    header.Cell().Text("Total").Bold();
                                });

                                foreach (var item in quote.Items)
                                {
                                    table.Cell()
                                        .Text(item.Service?.Name ?? "");

                                    table.Cell()
                                        .Text(item.Quantity.ToString("0.##"));

                                    table.Cell()
                                        .Text($"₹{item.UnitPrice:N2}");

                                    table.Cell()
                                        .Text(
                                            $"₹{item.Quantity * item.UnitPrice:N2}");
                                }
                            });

                        column.Item()
                            .PaddingTop(20)
                            .AlignRight()
                            .Text($"TOTAL: ₹{quote.TotalAmount:N2}")
                            .FontSize(18)
                            .Bold();

                        column.Item()
                            .PaddingTop(30)
                            .Text("Thank you for your business!")
                            .FontSize(12);
                    });
            });
        });

        return document.GeneratePdf();
    }
}