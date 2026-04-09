using System.Text.Json.Serialization;

namespace IMSControl.Api.Models
{
    public class Order
    {
        public int Id { get; set; }

        public int ClientId { get; set; }
        public Client? Client { get; set; }

        public DateTime OrderDate { get; set; } = DateTime.UtcNow;
        public DateTime? DeliveryDate { get; set; }

        // Pendente, Em Producao, Pronto, Entregue, Cancelado
        public string Status { get; set; } = "Pendente";

        // Pendente, Parcial, Pago
        public string PaymentStatus { get; set; } = "Pendente";

        public decimal TotalAmount { get; set; }
        public decimal TotalCost { get; set; }

        public string? Notes { get; set; }

        public List<OrderItem> Items { get; set; } = new();
    }
}
