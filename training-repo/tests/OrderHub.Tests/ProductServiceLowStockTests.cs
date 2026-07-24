using OrderHub.Core.Domain;

namespace OrderHub.Tests;

public class ProductServiceLowStockTests
{
    [Fact]
    public async Task GetLowStockAsync_FiltersAndSortsCorrectly()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);

        // stock=3 → 應出現（< 10）
        TestSetup.AddProduct(db, stock: 3, sku: "SKU-C");
        // stock=8 → 應出現（< 10）
        TestSetup.AddProduct(db, stock: 8, sku: "SKU-B");
        // stock=10 → 不應出現（不是 <，是等於）
        TestSetup.AddProduct(db, stock: 10, sku: "SKU-A");
        // stock=20 → 不應出現
        TestSetup.AddProduct(db, stock: 20, sku: "SKU-D");

        var result = await service.GetLowStockAsync(10);

        Assert.Equal(2, result.Count);
        Assert.Equal("SKU-C", result[0].Sku);   // 庫存 3，排第一
        Assert.Equal("SKU-B", result[1].Sku);   // 庫存 8，排第二
    }

    [Fact]
    public async Task GetLowStockAsync_ExcludesInactiveProducts()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);

        // 停售商品，庫存 < 門檻，不應出現
        TestSetup.AddProduct(db, stock: 5, isActive: false, sku: "SKU-INACTIVE");
        // 販售中商品，應出現
        TestSetup.AddProduct(db, stock: 5, isActive: true, sku: "SKU-ACTIVE");

        var result = await service.GetLowStockAsync(10);

        Assert.Single(result);
        Assert.Equal("SKU-ACTIVE", result[0].Sku);
    }

    [Fact]
    public async Task GetLowStockAsync_SoldLast30Days_ExcludesCancelledOrders()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);

        var customer = TestSetup.AddCustomer(db);
        var product = TestSetup.AddProduct(db, stock: 3, sku: "SKU-TEST");

        // 有效訂單（Pending），售出 4 件，應計入
        var pendingOrder = new Order
        {
            CustomerId = customer.Id,
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddDays(-5)
        };
        pendingOrder.Items.Add(new OrderItem { ProductId = product.Id, Quantity = 4, UnitPriceSnapshot = 100m });
        db.Orders.Add(pendingOrder);

        // Cancelled 訂單，售出 10 件，不應計入
        var cancelledOrder = new Order
        {
            CustomerId = customer.Id,
            Status = OrderStatus.Cancelled,
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        };
        cancelledOrder.Items.Add(new OrderItem { ProductId = product.Id, Quantity = 10, UnitPriceSnapshot = 100m });
        db.Orders.Add(cancelledOrder);

        db.SaveChanges();

        var result = await service.GetLowStockAsync(10);

        Assert.Single(result);
        Assert.Equal(4, result[0].SoldLast30Days);   // 只算 Pending 的 4 件
    }
}
