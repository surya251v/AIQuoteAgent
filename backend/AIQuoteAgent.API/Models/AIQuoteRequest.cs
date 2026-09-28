namespace AIQuoteAgent.API.Models;

public class AIQuoteRequest
{
    public int CustomerId { get; set; }
    public string Message { get; set; } = string.Empty;
}