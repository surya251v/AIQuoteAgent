using System.Text.Json.Serialization;

namespace AIQuoteAgent.API.Models;

public class AIQuoteResult
{
    [JsonPropertyName("services")]
    public List<AIQuoteItem> Services { get; set; } = new();
}

public class AIQuoteItem
{
    [JsonPropertyName("service_name")]
    public string ServiceName { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }

    [JsonPropertyName("unit")]
    public string Unit { get; set; } = string.Empty;
}