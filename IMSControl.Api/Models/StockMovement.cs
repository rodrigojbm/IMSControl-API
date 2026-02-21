namespace IMSControl.Api.Models
{
    public class StockMovement
    {
        public int Id { get; set; }

        public string Type { get; set; } = "";       // "entrada" | "saida"
        public string Category { get; set; } = "";   // "compra" | "venda" | "producao" | ...
        public string ItemType { get; set; } = "";   // "item" | "produto"

        public int ItemId { get; set; }
        public string ItemName { get; set; } = "";

        public decimal Quantity { get; set; }
        public string Unit { get; set; } = "un";

        public decimal UnitValue { get; set; }
        public decimal TotalValue { get; set; }

        public DateTime MovementDate { get; set; }

        public string? Notes { get; set; }
    }
}
