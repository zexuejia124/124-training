using Microsoft.EntityFrameworkCore;
using OrderHub.Core.Common;
using OrderHub.Core.Domain;
using OrderHub.Core.Interfaces;
using OrderHub.Infrastructure.Data;

namespace OrderHub.Infrastructure.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly OrderHubDbContext _db;

    public ProductRepository(OrderHubDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync() =>
        await _db.Products.OrderBy(p => p.Sku).ToListAsync();

    public async Task<IReadOnlyList<Product>> GetActiveAsync() =>
        await _db.Products.Where(p => p.IsActive).OrderBy(p => p.Sku).ToListAsync();

    public Task<Product?> GetByIdAsync(int id) =>
        _db.Products.FirstOrDefaultAsync(p => p.Id == id);

    public async Task<IReadOnlyList<LowStockItemDto>> GetLowStockAsync(int threshold)
    {
        var products = await _db.Products
            .Where(p => p.IsActive && p.StockQuantity < threshold)
            .OrderBy(p => p.StockQuantity)
            .ToListAsync();

        if (products.Count == 0) return Array.Empty<LowStockItemDto>();

        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        var ids = products.Select(p => p.Id).ToList();

        var sales = await _db.OrderItems
            .Where(oi => ids.Contains(oi.ProductId)
                      && oi.Order!.Status != OrderStatus.Cancelled
                      && oi.Order.CreatedAt >= thirtyDaysAgo)
            .GroupBy(oi => oi.ProductId)
            .Select(g => new { ProductId = g.Key, Total = g.Sum(oi => oi.Quantity) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Total);

        return products.Select(p => new LowStockItemDto
        {
            Sku = p.Sku,
            Name = p.Name,
            StockQuantity = p.StockQuantity,
            SoldLast30Days = sales.GetValueOrDefault(p.Id, 0)
        }).ToList();
    }

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}
