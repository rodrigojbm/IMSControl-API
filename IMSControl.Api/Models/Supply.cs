namespace IMSControl.Api.Models
{
    public class Supply
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Category { get; set; } = "outros";
        public string Unit { get; set; } = "un";

        public decimal Quantity { get; set; }
        public decimal MinQuantity { get; set; }
        public decimal CostPerUnit { get; set; }

        public string? Supplier { get; set; }
        public string? Notes { get; set; }
    }
}
