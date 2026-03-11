using IMSControl.Api.Data;
using IMSControl.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace IMSControl.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class MovementsController : ControllerBase
{
    private readonly AppDbContext _db;
    public MovementsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<StockMovement>>> List([FromQuery] string? sort, [FromQuery] int? limit)
    {
        IQueryable<StockMovement> q = _db.StockMovements.AsNoTracking();

        // sort: "-movement_date" (igual seu front)
        if (sort == "-movementDate") 
            q = q.OrderByDescending(m => m.MovementDate).ThenByDescending(m => m.Id);
        else if (sort == "movementDate") 
            q = q.OrderBy(m => m.MovementDate).ThenBy(m => m.Id);
        else 
            q = q.OrderByDescending(m => m.Id);

        if (limit.HasValue && limit.Value > 0)
            q = q.Take(limit.Value);

        return await q.ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<StockMovement>> Create([FromBody] StockMovement input)
    {
        input.Id = 0;

        if (input.Quantity <= 0)
            return BadRequest("quantity deve ser maior que 0.");

        if (input.Type != "entrada" && input.Type != "saida")
            return BadRequest("type deve ser 'entrada' ou 'saida'.");

        if (input.ItemType != "item" && input.ItemType != "produto")
            return BadRequest("itemType deve ser 'insumo' ou 'produto'.");

        // Normaliza valores
        input.UnitValue = input.UnitValue < 0 ? 0 : input.UnitValue;
        input.TotalValue = input.TotalValue > 0 ? input.TotalValue : (input.UnitValue * input.Quantity);

        await using var tx = await _db.Database.BeginTransactionAsync();

        if (input.ItemType == "item")
        {
            var supply = await _db.Supplies.FirstOrDefaultAsync(s => s.Id == input.ItemId);
            if (supply is null)
                return BadRequest("Insumo não encontrado.");

            // Calcula novo estoque
            var newQty = input.Type == "entrada" ? supply.Quantity + input.Quantity : supply.Quantity - input.Quantity;

            if (newQty < 0)
                return BadRequest("Estoque insuficiente para saída.");

            // Atualiza custo médio SOMENTE se for entrada de compra com unitValue válido
            var isEntradaCompra = input.Type == "entrada" && input.Category == "compra" && input.UnitValue > 0;

            if (isEntradaCompra)
            {
                var qtdAtual = supply.Quantity;
                var custoAtual = supply.CostPerUnit;
                var qtdEntrada = input.Quantity;
                var custoEntrada = input.UnitValue;

                // Se não tinha custo antes, assume o custo da entrada como base
                if (qtdAtual <= 0 || custoAtual <= 0)
                {
                    supply.CostPerUnit = custoEntrada;
                    supply.TotalValue = custoEntrada * qtdEntrada;
                }
                else
                {
                    var totalValor = (qtdAtual * custoAtual) + (qtdEntrada * custoEntrada);
                    var totalQtd = qtdAtual + qtdEntrada;

                    supply.CostPerUnit = totalQtd > 0 ? (totalValor / totalQtd) : custoEntrada;
                    supply.TotalValue = totalValor;
                }
            }
            else if (input.Type == "saida")
            {
                var qtdAtual = supply.Quantity;
                var qtdSaida = input.Quantity;
                var custoAtual = supply.CostPerUnit;

                var totalValor = (qtdAtual - qtdSaida) * custoAtual;
                supply.TotalValue = totalValor;
            }

            supply.Quantity = newQty;
        }
        else // produto
        {
            var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == input.ItemId);
            if (product is null)
                return BadRequest("Produto não encontrado.");

            // produto é int, então arredonda/valida
            var delta = (int)Math.Round(input.Quantity, MidpointRounding.AwayFromZero);

            if (delta <= 0)
                return BadRequest("Quantidade inválida para produto (precisa ser >= 1).");

            var newQty = input.Type == "entrada" ? product.Quantity + delta : product.Quantity - delta;

            if (newQty < 0)
                return BadRequest("Estoque insuficiente para saída.");

            product.Quantity = newQty;
        }

        _db.StockMovements.Add(input);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(input);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var mov = await _db.StockMovements.FindAsync(id);
        if (mov is null) 
            return NotFound();

        // por simplicidade: deletar NÃO desfaz estoque automaticamente
        _db.StockMovements.Remove(mov);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
