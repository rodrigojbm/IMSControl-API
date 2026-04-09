using IMSControl.Api.Data;
using IMSControl.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IMSControl.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _db;

    public OrdersController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<Order>>> List()
    {
        var orders = await _db.Orders
            .Include(o => o.Client)
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .OrderByDescending(o => o.OrderDate)
            .AsNoTracking()
            .ToListAsync();
            
        return Ok(orders);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Order>> Get(int id)
    {
        var order = await _db.Orders
            .Include(o => o.Client)
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null)
            return NotFound("Pedido não encontrado.");

        return Ok(order);
    }

    [HttpPost]
    public async Task<ActionResult<Order>> Create([FromBody] Order input)
    {
        if (input.Items == null || !input.Items.Any())
            return BadRequest("O pedido deve conter pelo menos um item.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            input.Id = 0;
            input.OrderDate = DateTime.UtcNow;
            input.Status = "Pendente"; // Status inicial
            
            // Computar Totais
            input.TotalAmount = input.Items.Sum(i => i.Quantity * i.UnitPrice);
            input.TotalCost = input.Items.Sum(i => i.Quantity * i.UnitCost);

            // A quantidade do produto entra como "Reservada"
            foreach (var item in input.Items)
            {
                var product = await _db.Products.FindAsync(item.ProductId);
                if (product == null)
                    return BadRequest($"Produto não encontrado para o ID {item.ProductId}.");

                product.ReservedQuantity += item.Quantity;
                item.Id = 0; // Garantir nova inserção de item
            }

            _db.Orders.Add(input);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return Ok(input);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return StatusCode(500, $"Erro ao criar o pedido: {ex.Message}");
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] Order input)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id);
        if (order is null)
            return NotFound();

        // Apenas atualizar metadados primários nesse verb (Status, Datas, Notas)
        order.DeliveryDate = input.DeliveryDate;
        order.PaymentStatus = input.PaymentStatus;
        order.Notes = input.Notes;
        // Não recomendamos trocar "Status" aqui pois ele flui via ações (Produce, Deliver) ou Cancelamento.

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var order = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null)
            return NotFound();

        if (order.Status == "Entregue")
            return BadRequest("Não é possível excluir um pedido que já foi entregue.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // Se o pedido não foi entregue mas tinha reservas, estorna as reservas:
            if (order.Status == "Pendente" || order.Status == "Em Produção" || order.Status == "Pronto")
            {
                foreach (var item in order.Items)
                {
                    var product = await _db.Products.FindAsync(item.ProductId);
                    if (product != null)
                    {
                        product.ReservedQuantity -= item.Quantity;
                        if (product.ReservedQuantity < 0) product.ReservedQuantity = 0;
                    }
                }
            }

            _db.Orders.Remove(order);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return StatusCode(500, $"Erro ao excluir o pedido: {ex.Message}");
        }
    }

    // --- ACTIONS ---

    [HttpPost("{id:int}/produce")]
    public async Task<IActionResult> Produce(int id)
    {
        var order = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null) return NotFound("Pedido não encontrado.");

        if (order.Status != "Pendente")
            return BadRequest($"Apenas pedidos Pendentes podem ser passados para Produção. Status atual: {order.Status}");

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            foreach (var item in order.Items)
            {
                // Carrega produto e a receita
                var product = await _db.Products
                    .Include(p => p.Recipe)
                    .FirstOrDefaultAsync(p => p.Id == item.ProductId);

                if (product == null) continue;

                // Verifica o estoque dos insumos OBRIGATÓRIOS primeiro
                foreach (var recipeItem in product.Recipe)
                {
                    // A quantidade total a consumir do insumo
                    var totalSupplyNeeded = recipeItem.Quantity * item.Quantity;

                    var supply = await _db.Supplies.FindAsync(recipeItem.SupplyId);
                    if (supply == null)
                        return BadRequest($"Insumo base não encontrado para a receita: {recipeItem.SupplyName}");

                    if (supply.Quantity < totalSupplyNeeded)
                        return BadRequest($"Estoque insuficiente de '{supply.Name}'. Necessário: {totalSupplyNeeded}, Disponível: {supply.Quantity}");
                    
                    // Consome o insumo
                    supply.Quantity -= totalSupplyNeeded;

                    // Adiciona Movimentação Histórica Saída Insumo
                    _db.StockMovements.Add(new StockMovement
                    {
                        Type = "saida",
                        ItemType = "item",
                        Category = "producao",
                        ItemId = supply.Id,
                        ItemName = supply.Name,
                        Quantity = totalSupplyNeeded,
                        Unit = supply.Unit,
                        MovementDate = DateTime.UtcNow,
                        UnitValue = supply.CostPerUnit,
                        TotalValue = supply.CostPerUnit * totalSupplyNeeded,
                        Notes = $"Automático: Produção para o Pedido #{order.Id}"
                    });
                }

                // Incrementa estoque do Produto Acabado
                product.Quantity += item.Quantity;

                // Salva o histórico de Entrada do Produto Acabado
                _db.StockMovements.Add(new StockMovement
                {
                    Type = "entrada",
                    ItemType = "produto",
                    Category = "producao",
                    ItemId = product.Id,
                    ItemName = product.Name,
                    Quantity = item.Quantity,
                    Unit = "un", // geralmente unidade
                    MovementDate = DateTime.UtcNow,
                    UnitValue = product.ProductionCost,
                    TotalValue = product.ProductionCost * item.Quantity,
                    Notes = $"Automático: Produto Finalizado para o Pedido #{order.Id}"
                });
            }

            order.Status = "Pronto";
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return Ok(new { message = "Pedido produzido com sucesso. O estoque foi atualizado." });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return StatusCode(500, $"Falha na produção: {ex.Message}");
        }
    }

    [HttpPost("{id:int}/deliver")]
    public async Task<IActionResult> Deliver(int id)
    {
        var order = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null) return NotFound();

        if (order.Status == "Entregue" || order.Status == "Cancelado")
            return BadRequest($"O pedido não pode ser entregue. Status atual: {order.Status}");

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            foreach (var item in order.Items)
            {
                var product = await _db.Products.FindAsync(item.ProductId);
                if (product != null)
                {
                    // Checar se o produto realmente está no estoque físico
                    if (product.Quantity < item.Quantity)
                    {
                        return BadRequest($"O produto {product.Name} (ID {product.Id}) não tem saldo físico suficiente para ser entregue (Físico: {product.Quantity}, Pedido: {item.Quantity}). Você esqueceu de produzir?");
                    }

                    // Tira do físico e da reserva
                    product.Quantity -= item.Quantity;
                    product.ReservedQuantity -= item.Quantity;
                    if (product.ReservedQuantity < 0) product.ReservedQuantity = 0;

                    // Histórico "Saída de Venda" do Produto
                    _db.StockMovements.Add(new StockMovement
                    {
                        Type = "saida",
                        ItemType = "produto",
                        Category = "venda",
                        ItemId = product.Id,
                        ItemName = product.Name,
                        Quantity = item.Quantity,
                        Unit = "un",
                        MovementDate = DateTime.UtcNow,
                        UnitValue = item.UnitPrice,
                        TotalValue = item.Quantity * item.UnitPrice,
                        Notes = $"Automático: Despacho do Pedido #{order.Id}"
                    });
                }
            }

            order.Status = "Entregue";
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return Ok(new { message = "Pedido entregue com sucesso e abatido do estoque e reservas." });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return StatusCode(500, $"Falha na entrega: {ex.Message}");
        }
    }
}
