using System.Text.Json.Serialization;

namespace IMSControl.Api.Models
{
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";

        public string? Description { get; set; }
        public string Size { get; set; } = "media";

        public int Quantity { get; set; }
        public int MinQuantity { get; set; }

        public decimal ProductionCost { get; set; }
        public decimal SalePrice { get; set; }

        public string? ImageUrl { get; set; }

        public List<ProductRecipeItem> Recipe { get; set; } = new();
    }

    public class ProductRecipeItem
    {
        public int Id { get; set; }

        public int ProductId { get; set; }
        
        [JsonIgnore]
        public Product Product { get; set; } = null!;

        public int SupplyId { get; set; }
        public string SupplyName { get; set; } = "";
        public decimal Quantity { get; set; }
        public string Unit { get; set; } = "un";
    }
}
