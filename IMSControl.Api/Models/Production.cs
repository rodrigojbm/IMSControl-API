namespace IMSControl.Api.Models
{
    public class Production
    {
        public int Id { get; set; }

        public int ProductId { get; set; }
        public string ProductName { get; set; } = "";

        public int Quantity { get; set; }
        public DateTime ProductionDate { get; set; }

        public decimal TotalCost { get; set; }
        public string? Notes { get; set; }

        public List<ProductionSupplyUsed> SuppliesUsed { get; set; } = new();
    }

    public class ProductionSupplyUsed
    {
        public int Id { get; set; }

        public int ProductionId { get; set; }
        public Production Production { get; set; } = null!;

        public int SupplyId { get; set; }
        public string SupplyName { get; set; } = "";
        public decimal QuantityUsed { get; set; }
        public string Unit { get; set; } = "un";
    }
}
