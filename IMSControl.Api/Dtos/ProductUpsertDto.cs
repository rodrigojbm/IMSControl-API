namespace IMSControl.Api.Dtos;

public class ProductUpsertDto
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Size { get; set; } = "media";

    public int Quantity { get; set; }
    public int MinQuantity { get; set; }

    public decimal ProductionCost { get; set; }
    public decimal SalePrice { get; set; }

    public string? ImageUrl { get; set; }

    public List<ProductRecipeItemDto> Recipe { get; set; } = new();
}

public class ProductRecipeItemDto
{
    public int SupplyId { get; set; }
    public decimal Quantity { get; set; }
}
