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

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

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
            // em produção: permita seu domínio do front via variável de ambiente
            // Ex: CORS_ORIGINS="https://seusite.vercel.app,https://clarigo.com.br"
            var origins = (Environment.GetEnvironmentVariable("CORS_ORIGINS") ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (origins.Length > 0)
            {
                p.WithOrigins(origins)
                 .AllowAnyHeader()
                 .AllowAnyMethod();
            }
            else
            {
                // fallback: se você quiser testar rápido, pode liberar tudo
                // (o ideal é configurar o CORS_ORIGINS!)
                p.AllowAnyOrigin()
                 .AllowAnyHeader()
                 .AllowAnyMethod();
            }
        }
    });
});

var app = builder.Build();

app.UseCors("cors");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // Swagger em produção é opcional; se quiser, remova esse bloco.
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();
app.Run();