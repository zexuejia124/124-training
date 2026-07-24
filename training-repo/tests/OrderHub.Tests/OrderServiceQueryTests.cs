using OrderHub.Core.Domain;

namespace OrderHub.Tests;

public class OrderServiceQueryTests
{
    [Fact]
    public async Task GetOrders_WithStatusFilter_ReturnsOnlyMatchingStatus()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);
        var customer = TestSetup.AddCustomer(db);

        db.Orders.AddRange(
            new Order { CustomerId = customer.Id, Status = OrderStatus.Pending, CreatedAt = DateTime.UtcNow },
            new Order { CustomerId = customer.Id, Status = OrderStatus.Shipped, CreatedAt = DateTime.UtcNow },
            new Order { CustomerId = customer.Id, Status = OrderStatus.Shipped, CreatedAt = DateTime.UtcNow });
        db.SaveChanges();

        var result = await service.GetOrdersAsync(1, 20, OrderStatus.Shipped);

        Assert.All(result.Items, o => Assert.Equal(OrderStatus.Shipped, o.Status));
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task GetOrders_ReportsTotalCountAndTotalPages()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);
        var customer = TestSetup.AddCustomer(db);

        for (var i = 0; i < 45; i++)
            db.Orders.Add(new Order { CustomerId = customer.Id, Status = OrderStatus.Confirmed, CreatedAt = DateTime.UtcNow.AddMinutes(-i) });
        db.SaveChanges();

        var result = await service.GetOrdersAsync(1, 20, null);

        Assert.Equal(45, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public async Task GetOrders_Page1_ContainsNewestOrder()
    {
        // 回歸：修復前 Skip(page*pageSize) 在 page=1 會跳過第一頁，最新訂單找不到
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);
        var customer = TestSetup.AddCustomer(db);

        var baseTime = DateTime.UtcNow;
        for (var i = 24; i >= 1; i--)
            db.Orders.Add(new Order { CustomerId = customer.Id, Status = OrderStatus.Pending, CreatedAt = baseTime.AddMinutes(-i) });

        var newestOrder = new Order { CustomerId = customer.Id, Status = OrderStatus.Pending, CreatedAt = baseTime };
        db.Orders.Add(newestOrder);
        db.SaveChanges();

        var result = await service.GetOrdersAsync(1, 20, null);

        Assert.Contains(result.Items, o => o.Id == newestOrder.Id);
    }

    [Fact]
    public async Task GetOrders_LastPage_IsNotEmpty()
    {
        // 回歸：修復前 Skip(TotalPages*pageSize) 超出總筆數，最後一頁空白
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);
        var customer = TestSetup.AddCustomer(db);

        var baseTime = DateTime.UtcNow;
        for (var i = 0; i < 40; i++)
            db.Orders.Add(new Order { CustomerId = customer.Id, Status = OrderStatus.Pending, CreatedAt = baseTime.AddMinutes(-i) });
        db.SaveChanges();

        var firstResult = await service.GetOrdersAsync(1, 20, null);
        var lastPage = firstResult.TotalPages;
        var lastResult = await service.GetOrdersAsync(lastPage, 20, null);

        Assert.NotEmpty(lastResult.Items);
    }

    [Fact]
    public async Task GetCustomerOrders_ReturnsOnlyThatCustomersOrders()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);
        var customerA = TestSetup.AddCustomer(db, name: "客戶A");
        var customerB = TestSetup.AddCustomer(db, name: "客戶B");

        db.Orders.AddRange(
            new Order { CustomerId = customerA.Id, Status = OrderStatus.Pending, CreatedAt = DateTime.UtcNow },
            new Order { CustomerId = customerB.Id, Status = OrderStatus.Pending, CreatedAt = DateTime.UtcNow },
            new Order { CustomerId = customerA.Id, Status = OrderStatus.Shipped, CreatedAt = DateTime.UtcNow });
        db.SaveChanges();

        var orders = await service.GetCustomerOrdersAsync(customerA.Id);

        Assert.Equal(2, orders.Count);
        Assert.All(orders, o => Assert.Equal(customerA.Id, o.CustomerId));
    }
}
