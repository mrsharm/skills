namespace OrderSystem;

public interface IOrderRepository
{
    Task SaveAsync(Order order);
    Task<Order?> GetByIdAsync(int id);
    Task<List<Order>> GetAllAsync();
}

public interface INotificationService
{
    Task SendAsync(string message);
}

public class Order
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = "";
    public decimal Total { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum OrderStatus
{
    Pending,
    Confirmed,
    Shipped,
    Cancelled
}

public class OrderService
{
    private readonly IOrderRepository _repository;
    private readonly INotificationService _notifier;

    public OrderService(IOrderRepository repository, INotificationService notifier)
    {
        _repository = repository;
        _notifier = notifier;
    }

    public async Task<Order> PlaceOrderAsync(Order order)
    {
        if (string.IsNullOrWhiteSpace(order.CustomerName))
            throw new ArgumentException("Customer name is required");

        if (order.Total <= 0)
            throw new ArgumentException("Order total must be positive");

        order.Status = OrderStatus.Confirmed;
        order.CreatedAt = DateTime.UtcNow;

        await _repository.SaveAsync(order);
        await _notifier.SendAsync($"Order {order.Id} confirmed for {order.CustomerName}");

        return order;
    }

    public async Task CancelOrderAsync(int orderId)
    {
        var order = await _repository.GetByIdAsync(orderId);
        if (order is null)
            throw new InvalidOperationException($"Order {orderId} not found");

        if (order.Status == OrderStatus.Shipped)
            throw new InvalidOperationException("Cannot cancel a shipped order");

        order.Status = OrderStatus.Cancelled;
        await _repository.SaveAsync(order);
        await _notifier.SendAsync($"Order {orderId} has been cancelled");
    }

    public async Task<decimal> GetTotalRevenueAsync()
    {
        var orders = await _repository.GetAllAsync();
        return orders
            .Where(o => o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Shipped)
            .Sum(o => o.Total);
    }
}
