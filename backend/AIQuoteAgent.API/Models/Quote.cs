namespace AIQuoteAgent.API.Models;

public class Quote
{
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public Customer? Customer { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public decimal TotalAmount { get; set; }

    public string Status { get; set; } = "Draft";

    public List<QuoteItem> Items { get; set; } = new();
}