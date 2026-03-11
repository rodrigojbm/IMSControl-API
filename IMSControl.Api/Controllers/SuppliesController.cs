using IMSControl.Api.Data;
using IMSControl.Api.Dtos;
using IMSControl.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace IMSControl.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/supplies")]
public class SuppliesController : ControllerBase
{
    private readonly AppDbContext _db;
    public SuppliesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<Supply>>> List()
    {
        var supplies = await _db.Supplies.AsNoTracking().OrderBy(s => s.Name).ToListAsync();
        supplies.ForEach(x => x.TotalValue = x.Quantity * x.CostPerUnit);

        return supplies;
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Supply>> Get(int id)
    {
        var supply = await _db.Supplies.FindAsync(id);

        return supply is null ? NotFound() : Ok(supply);
    }

    [HttpPost]
    public async Task<ActionResult<Supply>> Create([FromBody] Supply input)
    {
        input.Id = 0;
        _db.Supplies.Add(input);

        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = input.Id }, input);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<Supply>> Update(int id, [FromBody] Supply input)
    {
        var supply = await _db.Supplies.FindAsync(id);
        if (supply is null) 
            return NotFound();

        // atualiza campos editáveis
        supply.Name = input.Name;
        supply.Category = input.Category;
        supply.Unit = input.Unit;
        //supply.Quantity = input.Quantity;
        supply.MinQuantity = input.MinQuantity;
        //supply.CostPerUnit = input.CostPerUnit;
        //supply.TotalValue = input.TotalValue;
        supply.Supplier = input.Supplier;
        supply.Notes = input.Notes;

        await _db.SaveChangesAsync();
        return Ok(supply);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var supply = await _db.Supplies.FindAsync(id);
        if (supply is null) 
            return NotFound();

        _db.Supplies.Remove(supply);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
