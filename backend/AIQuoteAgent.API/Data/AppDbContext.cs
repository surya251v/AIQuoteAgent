using AIQuoteAgent.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AIQuoteAgent.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Service> Services => Set<Service>();

    public DbSet<Business> Businesses => Set<Business>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Quote> Quotes => Set<Quote>();

    public DbSet<QuoteItem> QuoteItems => Set<QuoteItem>();
}