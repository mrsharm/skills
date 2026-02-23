using Microsoft.EntityFrameworkCore;

namespace CaptiveDependencyDemo;

public class AppDbContext : DbContext
{
    public DbSet<Order> Orders => Set<Order>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
}

public class Order
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = "";
    public decimal Total { get; set; }
}

public interface IOrderService
{
    Task<Order?> GetOrderAsync(int id);
    Task SaveOrderAsync(Order order);
}

// BUG: Singleton capturing a scoped DbContext = captive dependency
public class OrderService : IOrderService
{
    private readonly AppDbContext _db;

    public OrderService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Order?> GetOrderAsync(int id)
    {
        return await _db.Orders.FindAsync(id);
    }

    public async Task SaveOrderAsync(Order order)
    {
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();
    }
}
