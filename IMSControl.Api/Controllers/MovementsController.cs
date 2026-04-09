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
    public async Task<ActionResult<List<StockMovement>>> List([FromQuery] string? sort, [FromQuery] int? limit, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
    {
        IQueryable<StockMovement> q = _db.StockMovements.AsNoTracking();

        if (startDate.HasValue)
        {
            q = q.Where(m => m.MovementDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            var endOfDay = endDate.Value.Date.AddDays(1).AddTicks(-1);
            q = q.Where(m => m.MovementDate <= endOfDay);
        }

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

    [HttpPost("batch")]
    public async Task<ActionResult<List<StockMovement>>> CreateBatch([FromBody] List<StockMovement> inputs)
    {
        if (inputs == null || !inputs.Any())
            return BadRequest("A lista de movimentações não pode estar vazia.");

        await using var tx = await _db.Database.BeginTransactionAsync();

        try
        {
            var processedMovements = new List<StockMovement>();

            foreach (var input in inputs)
            {
                input.Id = 0;

                if (input.Quantity <= 0)
                    return BadRequest($"Quantidade deve ser maior que 0 para o item '{input.ItemName}'.");

                if (input.Type != "entrada" && input.Type != "saida")
                    return BadRequest($"Tipo deve ser 'entrada' ou 'saida' para o item '{input.ItemName}'.");

                if (input.ItemType != "item" && input.ItemType != "produto")
                    return BadRequest($"Tipo de item deve ser 'item' ou 'produto' para o item '{input.ItemName}'.");

                // Normaliza valores
                input.UnitValue = input.UnitValue < 0 ? 0 : input.UnitValue;
                input.TotalValue = input.TotalValue > 0 ? input.TotalValue : (input.UnitValue * input.Quantity);

                if (input.ItemType == "item")
                {
                    var supply = await _db.Supplies.FirstOrDefaultAsync(s => s.Id == input.ItemId);
                    if (supply is null)
                        return BadRequest($"Insumo '{input.ItemName}' não encontrado.");

                    var newQty = input.Type == "entrada" ? supply.Quantity + input.Quantity : supply.Quantity - input.Quantity;

                    if (newQty < 0)
                        return BadRequest($"Estoque insuficiente para saída do insumo '{input.ItemName}'.");

                    var isEntradaCompra = input.Type == "entrada" && input.Category == "compra" && input.UnitValue > 0;

                    if (isEntradaCompra)
                    {
                        var qtdAtual = supply.Quantity;
                        var custoAtual = supply.CostPerUnit;
                        var qtdEntrada = input.Quantity;
                        var custoEntrada = input.UnitValue;

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
                        return BadRequest($"Produto '{input.ItemName}' não encontrado.");

                    var delta = (int)Math.Round(input.Quantity, MidpointRounding.AwayFromZero);

                    if (delta <= 0)
                        return BadRequest($"Quantidade inválida para o produto '{input.ItemName}' (precisa ser >= 1).");

                    var newQty = input.Type == "entrada" ? product.Quantity + delta : product.Quantity - delta;

                    if (newQty < 0)
                        return BadRequest($"Estoque insuficiente para saída do produto '{input.ItemName}'.");

                    product.Quantity = newQty;
                }

                _db.StockMovements.Add(input);
                processedMovements.Add(input);
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return Ok(processedMovements);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            // Log do erro se necessário
            return StatusCode(500, $"Erro ao processar as movimentações: {ex.Message}");
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var mov = await _db.StockMovements.FindAsync(id);
        if (mov is null) 
            return NotFound();

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            if (mov.ItemType == "item")
            {
                var supply = await _db.Supplies.FirstOrDefaultAsync(s => s.Id == mov.ItemId);
                if (supply != null)
                {
                    // Reverte a movimentação
                    if (mov.Type == "entrada")
                    {
                        supply.Quantity -= mov.Quantity;
                        if (supply.Quantity < 0) supply.Quantity = 0;
                    }
                    else if (mov.Type == "saida")
                    {
                        supply.Quantity += mov.Quantity;
                    }
                }
            }
            else if (mov.ItemType == "produto")
            {
                var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == mov.ItemId);
                if (product != null)
                {
                    var delta = (int)Math.Round(mov.Quantity, MidpointRounding.AwayFromZero);
                    if (mov.Type == "entrada")
                    {
                        product.Quantity -= delta;
                        if (product.Quantity < 0) product.Quantity = 0;
                    }
                    else if (mov.Type == "saida")
                    {
                        product.Quantity += delta;
                    }
                }
            }

            _db.StockMovements.Remove(mov);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return StatusCode(500, $"Erro ao processar o estorno: {ex.Message}");
        }
    }
}
