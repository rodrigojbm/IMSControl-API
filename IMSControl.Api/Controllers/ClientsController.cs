using IMSControl.Api.Data;
using IMSControl.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IMSControl.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ClientsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ClientsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetClients()
    {
        var clients = await _db.Clients.ToListAsync();
        return Ok(clients);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetClient(int id)
    {
        var client = await _db.Clients.FindAsync(id);
        if (client == null) return NotFound();
        return Ok(client);
    }

    [HttpPost]
    public async Task<IActionResult> CreateClient([FromBody] Client client)
    {
        if (client == null) return BadRequest();
        _db.Clients.Add(client);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetClient), new { id = client.Id }, client);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateClient(int id, [FromBody] Client updatedClient)
    {
        if (id != updatedClient.Id) return BadRequest();

        var client = await _db.Clients.FindAsync(id);
        if (client == null) return NotFound();

        client.Name = updatedClient.Name;
        client.Email = updatedClient.Email;
        client.Phone = updatedClient.Phone;
        client.Document = updatedClient.Document;
        client.Address = updatedClient.Address;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteClient(int id)
    {
        var client = await _db.Clients.FindAsync(id);
        if (client == null) return NotFound();

        _db.Clients.Remove(client);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
