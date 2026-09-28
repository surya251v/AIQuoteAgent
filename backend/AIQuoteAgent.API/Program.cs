using System.Text.Json;
using AIQuoteAgent.API.Data;
using AIQuoteAgent.API.Models;
using AIQuoteAgent.API.Services;
using Google.GenAI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactApp", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=AIQuoteAgent.db"));

builder.Services.AddScoped<QuotationPdfService>();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("ReactApp");
app.UseHttpsRedirection();

// Test endpoint
app.MapGet("/", () => "AI Quote Agent API is running!");

// Get all services
app.MapGet("/api/services", async (AppDbContext db) =>
{
    var services = await db.Services.ToListAsync();

    return Results.Ok(services);
});

// Add service
app.MapPost("/api/services", async (Service service, AppDbContext db) =>
{
    db.Services.Add(service);

    await db.SaveChangesAsync();

    return Results.Created($"/api/services/{service.Id}", service);
});

app.MapPut("/api/services/{id}", async (
    int id,
    Service updatedService,
    AppDbContext db) =>
{
    var service = await db.Services.FindAsync(id);

    if (service == null)
        return Results.NotFound("Service not found.");

    if (string.IsNullOrWhiteSpace(updatedService.Name))
        return Results.BadRequest("Service name is required.");

    if (string.IsNullOrWhiteSpace(updatedService.Unit))
        return Results.BadRequest("Unit is required.");

    if (updatedService.Price < 0)
        return Results.BadRequest("Price cannot be negative.");

    service.Name = updatedService.Name;
    service.Unit = updatedService.Unit;
    service.Price = updatedService.Price;

    await db.SaveChangesAsync();

    return Results.Ok(service);
});

app.MapDelete("/api/services/{id}", async (
    int id,
    AppDbContext db) =>
{
    var service = await db.Services.FindAsync(id);

    if (service == null)
        return Results.NotFound("Service not found.");

    db.Services.Remove(service);

    await db.SaveChangesAsync();

    return Results.NoContent();
});

app.MapPost("/api/businesses", async (Business business, AppDbContext db) =>
{
    db.Businesses.Add(business);

    await db.SaveChangesAsync();

    return Results.Created($"/api/businesses/{business.Id}", business);
});

app.MapGet("/api/businesses", async (AppDbContext db) =>
{
    var businesses = await db.Businesses.ToListAsync();

    return Results.Ok(businesses);
});

app.MapPost("/api/quotes", async (Quote quote, AppDbContext db) =>
{
    decimal total = 0;

    foreach (var item in quote.Items)
    {
        var service = await db.Services.FindAsync(item.ServiceId);

        if (service == null)
        {
            return Results.BadRequest(
                $"Service with ID {item.ServiceId} was not found.");
        }

        item.UnitPrice = service.Price;
        total += item.Quantity * service.Price;
    }

    quote.TotalAmount = total;
    quote.CreatedAt = DateTime.UtcNow;

    db.Quotes.Add(quote);

    await db.SaveChangesAsync();

    return Results.Created($"/api/quotes/{quote.Id}", quote);
});

app.MapGet("/api/quotes/{id}", async (int id, AppDbContext db) =>
{
    var quote = await db.Quotes
        .Include(q => q.Customer)
        .Include(q => q.Items)
        .ThenInclude(i => i.Service)
        .FirstOrDefaultAsync(q => q.Id == id);

    if (quote == null)
    {
        return Results.NotFound();
    }

    return Results.Ok(quote);
});


app.MapPost("/api/ai/understand", async (AIQuoteRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
    {
        return Results.BadRequest("Message is required.");
    }

    var apiKey = System.Environment.GetEnvironmentVariable("GEMINI_API_KEY");

    if (string.IsNullOrWhiteSpace(apiKey))
    {
        return Results.Problem("GEMINI_API_KEY is not configured.");
    }

    var client = new Client(apiKey: apiKey);

  var prompt = """
    You are an AI assistant for a quotation system.

    Analyze the customer's request.

    Identify:
    - Service name
    - Quantity
    - Unit

    Return ONLY valid JSON.
    Do NOT use markdown.
    Do NOT use ```json.
    Do NOT add explanations.

    Required JSON format:

    {
    "services": [
        {
        "service_Name": "string",
        "quantity": 0,
        "unit": "string"
        }
    ]
    }

    Customer request:
    """ + request.Message;

    var response = await client.Models.GenerateContentAsync(
        model: "gemini-3.5-flash-lite",
        contents: prompt);

    var text = response.Candidates?[0]
        ?.Content?
        .Parts?[0]
        ?.Text;

    if (string.IsNullOrWhiteSpace(text))
    {
        return Results.Problem("Gemini returned an empty response.");
    }

    try
    {
        var result = JsonSerializer.Deserialize<AIQuoteResult>(
            text,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        return Results.Ok(result);
    }
    catch (JsonException)
    {
        return Results.Problem(
            $"Gemini returned invalid JSON: {text}");
    }
});

app.MapPost("/api/quotes/ai", async (
    AIQuoteRequest request,
    AppDbContext db) =>
{
    // 1. Validate request
    if (string.IsNullOrWhiteSpace(request.Message))
    {
        return Results.BadRequest("Message is required.");
    }

    // 2. Validate customer
    var customer = await db.Customers.FindAsync(request.CustomerId);

    if (customer == null)
    {
        return Results.BadRequest(
            $"Customer with ID {request.CustomerId} was not found.");
    }

    // 3. Get Gemini API key
    var apiKey = System.Environment.GetEnvironmentVariable("GEMINI_API_KEY");

    if (string.IsNullOrWhiteSpace(apiKey))
    {
        return Results.Problem(
            "GEMINI_API_KEY is not configured.");
    }

    // 4. Create Gemini client
    var client = new Client(apiKey: apiKey);

    // 5. Build AI prompt
    var prompt =
        """
        You are an AI quotation assistant.

        Analyze the customer's request and identify every service
        that the customer wants.

        Return ONLY valid JSON using exactly this structure:

        {
          "services": [
            {
              "service_name": "string",
              "quantity": 0,
              "unit": "string"
            }
          ]
        }

        Rules:
        - Identify every requested service.
        - Identify the quantity.
        - Identify the unit.
        - Do NOT calculate prices.
        - Do NOT add explanations.
        - Do NOT use markdown.
        - Return ONLY valid JSON.

        Customer request:
        """
        + request.Message;

    // 6. Call Gemini
    var response = await client.Models.GenerateContentAsync(
        model: "gemini-3.8-flash",
        contents: prompt);

    // 7. Get Gemini response text
    var aiText = response.Candidates?[0]
        ?.Content?
        .Parts?[0]
        ?.Text;

    if (string.IsNullOrWhiteSpace(aiText))
    {
        return Results.Problem(
            "Gemini returned an empty response.");
    }

    // 8. Remove markdown code fences if Gemini adds them
    aiText = aiText
        .Replace("```json", "")
        .Replace("```", "")
        .Trim();

    // 9. Convert AI response into our C# model
    AIQuoteResult? aiResult;

    try
    {
        aiResult =
            System.Text.Json.JsonSerializer.Deserialize<AIQuoteResult>(
                aiText,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            $"Could not understand AI response: {ex.Message}");
    }

    if (aiResult == null || aiResult.Services.Count == 0)
    {
        return Results.BadRequest(
            "No services were identified in the customer request.");
    }

    // 10. Load all services from our price list
    var services = await db.Services.ToListAsync();

    if (services.Count == 0)
    {
        return Results.BadRequest(
            "No services have been configured in the price list.");
    }

    // 11. Create the quote
    var quote = new Quote
    {
        CustomerId = customer.Id,
        CreatedAt = DateTime.UtcNow,
        Status = "Draft",
        TotalAmount = 0
    };

    decimal grandTotal = 0;

    // 12. Match AI services with database services
    foreach (var aiItem in aiResult.Services)
    {
        var requestedName = aiItem.ServiceName
            .Trim()
            .ToLower();

        if (string.IsNullOrWhiteSpace(requestedName))
        {
            continue;
        }

        // First try exact match
        var service = services.FirstOrDefault(s =>
            s.Name.Trim().ToLower() == requestedName);

        // If exact match fails, try partial matching
        if (service == null)
        {
            service = services.FirstOrDefault(s =>
                requestedName.Contains(
                    s.Name.Trim().ToLower()) ||
                s.Name.Trim().ToLower().Contains(
                    requestedName));
        }

        // Service doesn't exist in price list
        if (service == null)
        {
            return Results.BadRequest(
                $"Service '{aiItem.ServiceName}' was not found in the price list.");
        }

        // Validate quantity
        if (aiItem.Quantity <= 0)
        {
            return Results.BadRequest(
                $"Invalid quantity for service '{service.Name}'.");
        }

        // Calculate item total
        var itemTotal =
            aiItem.Quantity * service.Price;

        // Add quote item
        quote.Items.Add(new QuoteItem
        {
            ServiceId = service.Id,
            Quantity = aiItem.Quantity,
            UnitPrice = service.Price
        });

        // Add to grand total
        grandTotal += itemTotal;
    }

    // 13. Validate that we have quote items
    if (quote.Items.Count == 0)
    {
        return Results.BadRequest(
            "No valid quote items were created.");
    }

    // 14. Set final quote total
    quote.TotalAmount = grandTotal;

    // 15. Save quote + quote items
    db.Quotes.Add(quote);

    await db.SaveChangesAsync();

    // 16. Build response
    var resultItems = quote.Items
        .Select(item =>
        {
            var service = services.First(s =>
                s.Id == item.ServiceId);

            return new
            {
                serviceId = service.Id,
                serviceName = service.Name,
                unit = service.Unit,
                quantity = item.Quantity,
                unitPrice = item.UnitPrice,
                total = item.Quantity * item.UnitPrice
            };
        })
        .ToList();

    // 17. Return created quotation
    return Results.Created(
        $"/api/quotes/{quote.Id}",
        new
        {
            quoteId = quote.Id,
            customerId = customer.Id,
            customerName = customer.Name,
            customerPhone = customer.Phone,
            customerMessage = request.Message,
            createdAt = quote.CreatedAt,
            status = quote.Status,
            items = resultItems,
            grandTotal = quote.TotalAmount
        });
});

app.MapPost("/api/customers", async (Customer customer, AppDbContext db) =>
{
    db.Customers.Add(customer);

    await db.SaveChangesAsync();

    return Results.Created($"/api/customers/{customer.Id}", customer);
});

app.MapPost("/api/customers/upsert", async (
    Customer customer,
    AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(customer.Name))
        return Results.BadRequest("Customer name is required.");

    if (string.IsNullOrWhiteSpace(customer.Phone))
        return Results.BadRequest("Customer phone is required.");

    var existingCustomer = await db.Customers
        .FirstOrDefaultAsync(c => c.Phone == customer.Phone);

    if (existingCustomer != null)
    {
        existingCustomer.Name = customer.Name;
        existingCustomer.Address = customer.Address;

        await db.SaveChangesAsync();

        return Results.Ok(existingCustomer);
    }

    db.Customers.Add(customer);
    await db.SaveChangesAsync();

    return Results.Created(
        $"/api/customers/{customer.Id}",
        customer);
});

app.MapGet("/api/customers", async (AppDbContext db) =>
{
    var customers = await db.Customers.ToListAsync();

    return Results.Ok(customers);
});

app.MapGet("/api/quotes/{id}/pdf", async (
    int id,
    [FromServices] QuotationPdfService pdfService) =>
{
    var pdf = await pdfService.GenerateAsync(id);

    if (pdf == null)
    {
        return Results.NotFound(
            $"Quote with ID {id} was not found.");
    }

    return Results.File(
        pdf,
        "application/pdf",
        $"Quote-{id}.pdf");
});

app.MapGet("/api/quotes", async (AppDbContext db) =>
{
    var quotes = await db.Quotes
        .Include(q => q.Customer)
        .Include(q => q.Items)
        .ThenInclude(i => i.Service)
        .OrderByDescending(q => q.CreatedAt)
        .Select(q => new
        {
            quoteId = q.Id,
            customerName = q.Customer!.Name,
            customerPhone = q.Customer.Phone,
            createdAt = q.CreatedAt,
            status = q.Status,
            totalAmount = q.TotalAmount,
            itemCount = q.Items.Count
        })
        .ToListAsync();

    return Results.Ok(quotes);
});

app.Run();