using IMSControl.Api.Data;
using IMSControl.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IMSControl.Api.Controllers;

[ApiController]
[Route("api/movements")]
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

        if (input.ItemType != "insumo" && input.ItemType != "produto")
            return BadRequest("itemType deve ser 'insumo' ou 'produto'.");

        // garante valores
        input.UnitValue = input.UnitValue < 0 ? 0 : input.UnitValue;
        input.TotalValue = input.TotalValue <= 0 ? (input.UnitValue * input.Quantity) : input.TotalValue;

        await using var tx = await _db.Database.BeginTransactionAsync();

        // Atualiza estoque
        if (input.ItemType == "insumo")
        {
            var supply = await _db.Supplies.FirstOrDefaultAsync(s => s.Id == input.ItemId);
            if (supply is null) 
                return BadRequest("Insumo não encontrado.");

            var newQty = input.Type == "entrada" ? supply.Quantity + input.Quantity : supply.Quantity - input.Quantity;
            if (newQty < 0) 
                return BadRequest("Estoque insuficiente para saída.");

            supply.Quantity = newQty;
        }
        else
        {
            var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == input.ItemId);
            if (product is null) 
                return BadRequest("Produto não encontrado.");

            var newQty = input.Type == "entrada" ? product.Quantity + (int)Math.Round(input.Quantity) : product.Quantity - (int)Math.Round(input.Quantity);
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
