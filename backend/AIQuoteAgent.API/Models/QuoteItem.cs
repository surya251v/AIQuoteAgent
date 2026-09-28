namespace AIQuoteAgent.API.Models;

public class QuoteItem
{
    public int Id { get; set; }

    public int QuoteId { get; set; }

    public Quote? Quote { get; set; }

    public int ServiceId { get; set; }

    public Service? Service { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal Total => Quantity * UnitPrice;
}