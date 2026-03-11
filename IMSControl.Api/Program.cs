using IMSControl.Api.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles
    );

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var jwtKey = builder.Configuration["Jwt:Key"] ?? Environment.GetEnvironmentVariable("JWT_KEY") ?? "your_super_secret_key_needs_to_be_at_least_32_characters";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "IMSControl";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "IMSControlClient";

builder.Services.AddAuthentication(opt =>
{
    opt.DefaultAuthenticateScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
    opt.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(opt =>
{
    opt.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtKey))
    };
});

var connectionString = builder.Configuration.GetConnectionString("Default");
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlServer(connectionString!));

builder.Services.AddCors(opt =>
{
    opt.AddPolicy("cors", p =>
    {
        // em dev você libera seu vite
        if (builder.Environment.IsDevelopment())
        {
            p.WithOrigins("http://localhost:5173")
             .AllowAnyHeader()
             .AllowAnyMethod();
        }
        else
        {
            var origins = (Environment.GetEnvironmentVariable("CORS_ORIGINS") ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            // sempre libera localhost pra testes
            origins.Add("http://localhost:5173");
            origins.Add("http://localhost:5174");

            p.WithOrigins(origins.Distinct().ToArray())
             .AllowAnyHeader()
             .AllowAnyMethod();
        }
    });
});

builder.Services.AddScoped<Microsoft.AspNetCore.Identity.IPasswordHasher<IMSControl.Api.Models.User>, Microsoft.AspNetCore.Identity.PasswordHasher<IMSControl.Api.Models.User>>();

var app = builder.Build();

app.UseCors("cors");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Seed admin user
using (var scope = app.Services.CreateScope())
{
    var _db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var _hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<IMSControl.Api.Models.User>();
    
    // Force admin password to admin123
    var adminUser = _db.Users.SingleOrDefault(u => u.Username == "admin");
    if (adminUser == null)
    {
        adminUser = new IMSControl.Api.Models.User
        {
            Username = "admin",
            Role = "Admin"
        };
        adminUser.PasswordHash = _hasher.HashPassword(adminUser, "admin123");
        _db.Users.Add(adminUser);
    }
    else
    {
        adminUser.PasswordHash = _hasher.HashPassword(adminUser, "admin123");
    }
    _db.SaveChanges();
}


app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();