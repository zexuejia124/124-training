namespace OrderHub.Core.Common;

public class LowStockItemDto
{
    public string Sku { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int StockQuantity { get; init; }
    public int SoldLast30Days { get; init; }
}
