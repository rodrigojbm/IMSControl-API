using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using IMSControl.Api.Data;
using IMSControl.Api.Models;
using Microsoft.AspNetCore.Identity;

namespace IMSControl.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<User> _hasher;

    public AuthController(IConfiguration config, AppDbContext db, IPasswordHasher<User> hasher)
    {
        _config = config;
        _db = db;
        _hasher = hasher;
    }

    [Authorize]
    [HttpGet("users")]
    public IActionResult GetUsers()
    {
        return Ok(_db.Users.Select(u => new { u.Id, u.Username }).ToList());
    }

    [Authorize]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
            return BadRequest();

        var exists = _db.Users.Any(u => u.Username == dto.Username);
        if (exists)
            return Conflict(new { message = "Username already taken" });

        var user = new User { Username = dto.Username! };
        user.PasswordHash = _hasher.HashPassword(user, dto.Password!);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return CreatedAtAction(null, new { id = user.Id });
    }

    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] RegisterDto dto)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();

        if (string.IsNullOrWhiteSpace(dto.Username)) return BadRequest();

        // Check if username is being changed to one that already exists
        if (dto.Username != user.Username && _db.Users.Any(u => u.Username == dto.Username))
        {
            return Conflict(new { Message = "Username already exists." });
        }

        user.Username = dto.Username;

        if (!string.IsNullOrWhiteSpace(dto.Password))
        {
            user.PasswordHash = _hasher.HashPassword(user, dto.Password);
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginDto dto)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
            return Unauthorized();

        var user = _db.Users.SingleOrDefault(u => u.Username == dto.Username);
        if (user is null)
            return Unauthorized();

        var verify = _hasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
        if (verify == PasswordVerificationResult.Failed)
            return Unauthorized();

        var jwtKey = _config["Jwt:Key"] ?? Environment.GetEnvironmentVariable("JWT_KEY") ?? "your_super_secret_key_needs_to_be_at_least_32_characters";
        var jwtIssuer = _config["Jwt:Issuer"] ?? "IMSControl";
        var jwtAudience = _config["Jwt:Audience"] ?? "IMSControlClient";
        var expireMinutes = int.TryParse(_config["Jwt:ExpireMinutes"], out var m) ? m : 60;

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username)
        };
        if (!string.IsNullOrWhiteSpace(user.Role))
            claims.Add(new Claim(ClaimTypes.Role, user.Role));

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expireMinutes),
            signingCredentials: credentials
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return Ok(new { token = tokenString });
    }
}
