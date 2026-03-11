using IMSControl.Api.Data;
using IMSControl.Api.Dtos;
using IMSControl.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace IMSControl.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ProductsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<Product>>> List()
    {
        return await _db.Products.AsNoTracking().Include(p => p.Recipe).OrderBy(p => p.Name).ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Product>> Get(int id)
    {
        var product = await _db.Products.AsNoTracking().Include(p => p.Recipe).FirstOrDefaultAsync(p => p.Id == id);

        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost]
    public async Task<ActionResult<Product>> Create([FromBody] ProductUpsertDto input)
    {
        try
        {
            // valida recipe e busca supplies para preencher nome/unidade
            var recipeItems = await BuildRecipeItems(input.Recipe);

            var product = new Product
            {
                Name = input.Name,
                Description = input.Description,
                Size = input.Size,
                Quantity = input.Quantity,
                MinQuantity = input.MinQuantity,
                ProductionCost = input.ProductionCost,
                SalePrice = input.SalePrice,
                ImageUrl = input.ImageUrl,
                Recipe = recipeItems
            };

            _db.Products.Add(product);
            await _db.SaveChangesAsync();

            var created = await _db.Products
                .AsNoTracking()
                .Include(p => p.Recipe)
                .FirstAsync(p => p.Id == product.Id);

            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<Product>> Update(int id, [FromBody] ProductUpsertDto input)
    {
        try
        {
            var product = await _db.Products
            .Include(p => p.Recipe)
            .FirstOrDefaultAsync(p => p.Id == id);

            if (product is null) return NotFound();

            // campos simples
            product.Name = input.Name;
            product.Description = input.Description;
            product.Size = input.Size;
            product.Quantity = input.Quantity;
            product.MinQuantity = input.MinQuantity;
            product.ProductionCost = input.ProductionCost;
            product.SalePrice = input.SalePrice;
            product.ImageUrl = input.ImageUrl;

            // recipe (apaga e recria)
            _db.ProductRecipeItems.RemoveRange(product.Recipe);
            product.Recipe.Clear();

            var recipeItems = await BuildRecipeItems(input.Recipe);
            foreach (var ri in recipeItems)
                product.Recipe.Add(ri);

            await _db.SaveChangesAsync();

            var updated = await _db.Products
                .AsNoTracking()
                .Include(p => p.Recipe)
                .FirstAsync(p => p.Id == id);

            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product is null) return NotFound();

        _db.Products.Remove(product);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<List<ProductRecipeItem>> BuildRecipeItems(List<ProductRecipeItemDto>? recipe)
    {
        recipe ??= new();

        // remove linhas inválidas (SupplyId 0, qty <= 0)
        var cleaned = recipe.Where(r => r.SupplyId > 0 && r.Quantity > 0).ToList();

        if (cleaned.Count == 0)
            return new();

        // busca supplies para validar e preencher SupplyName/Unit
        var supplyIds = cleaned.Select(r => r.SupplyId).Distinct().ToList();
        var supplies = await _db.Supplies.AsNoTracking().Where(s => supplyIds.Contains(s.Id)).Select(s => new { s.Id, s.Name, s.Unit }).ToListAsync();

        var supplyMap = supplies.ToDictionary(s => s.Id, s => s);

        // valida: se veio supplyId inexistente, retorna 400
        var missing = supplyIds.Where(id => !supplyMap.ContainsKey(id)).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException($"SupplyId inválido: {string.Join(", ", missing)}");

        // monta lista final
        return cleaned.Select(r =>
        {
            var s = supplyMap[r.SupplyId];
            return new ProductRecipeItem
            {
                SupplyId = r.SupplyId,
                SupplyName = s.Name,
                Unit = s.Unit,
                Quantity = r.Quantity
            };
        }).ToList();
    }
}
