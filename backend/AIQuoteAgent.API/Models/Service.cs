namespace AIQuoteAgent.API.Models;

public class Service
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    public decimal Price { get; set; }
}