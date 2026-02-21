using IMSControl.Api.Data;
using IMSControl.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IMSControl.Api.Controllers;

[ApiController]
[Route("api/productions")]
public class ProductionsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ProductionsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult> List([FromQuery] string? sort, [FromQuery] int? limit)
    {
        IQueryable<Production> q = _db.Productions.AsNoTracking()
            .Include(p => p.SuppliesUsed);

        if (sort == "-productionDate")
            q = q.OrderByDescending(p => p.ProductionDate).ThenByDescending(p => p.Id);
        else if (sort == "productionDate")
            q = q.OrderBy(p => p.ProductionDate).ThenBy(p => p.Id);
        else
            q = q.OrderByDescending(p => p.Id);

        if (limit is > 0) q = q.Take(limit.Value);

        var result = await q.Select(p => new {
            p.Id,
            p.ProductId,
            p.ProductName,
            p.Quantity,
            p.ProductionDate,
            p.TotalCost,
            p.Notes,
            SuppliesUsed = p.SuppliesUsed.Select(s => new {
                s.Id,
                s.SupplyId,
                s.SupplyName,
                s.QuantityUsed,
                s.Unit
            })
        }).ToListAsync();

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<Production>> Create([FromBody] Production input)
    {
        input.Id = 0;
        foreach (var s in input.SuppliesUsed) 
            s.Id = 0;

        if (input.Quantity <= 0) 
            return BadRequest("quantity deve ser maior que 0.");

        await using var tx = await _db.Database.BeginTransactionAsync();

        // 1) Validar produto
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == input.ProductId);
        if (product is null) 
            return BadRequest("Produto não encontrado.");

        // 2) Validar insumos suficientes
        foreach (var used in input.SuppliesUsed)
        {
            var supply = await _db.Supplies.FirstOrDefaultAsync(s => s.Id == used.SupplyId);
            if (supply is null) 
                return BadRequest($"Insumo {used.SupplyId} não encontrado.");

            if (supply.Quantity < used.QuantityUsed)
                return BadRequest($"Estoque insuficiente para o insumo: {used.SupplyName}.");
        }

        // 3) Baixar insumos
        foreach (var used in input.SuppliesUsed)
        {
            var supply = await _db.Supplies.FirstAsync(s => s.Id == used.SupplyId);
            supply.Quantity -= used.QuantityUsed;

            // cria movement de saída do insumo
            _db.StockMovements.Add(new StockMovement
            {
                Type = "saida",
                Category = "producao",
                ItemType = "item",
                ItemId = used.SupplyId,
                ItemName = used.SupplyName,
                Quantity = used.QuantityUsed,
                Unit = used.Unit,
                UnitValue = supply.CostPerUnit,
                TotalValue = supply.TotalValue,
                MovementDate = input.ProductionDate,
                Notes = $"Usado na produção de {input.Quantity}x {input.ProductName}"
            });
        }

        // 4) Dar entrada no produto
        product.Quantity += input.Quantity;

        // movement de entrada do produto
        var unitCost = input.TotalCost > 0 && input.Quantity > 0 ? (input.TotalCost / input.Quantity) : 0;
        _db.StockMovements.Add(new StockMovement
        {
            Type = "entrada",
            Category = "producao",
            ItemType = "produto",
            ItemId = input.ProductId,
            ItemName = input.ProductName,
            Quantity = input.Quantity,
            Unit = "un",
            UnitValue = unitCost,
            TotalValue = input.TotalCost,
            MovementDate = input.ProductionDate
        });

        // 5) Salvar produção
        _db.Productions.Add(input);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(input);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var prod = await _db.Productions.FindAsync(id);
        if (prod is null) 
            return NotFound();

        // por simplicidade: deletar não desfaz estoque/movements
        _db.Productions.Remove(prod);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
