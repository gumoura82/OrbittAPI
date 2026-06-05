using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OrbittAPI.Application.Services;
using OrbittAPI.Domain.Interfaces;
using OrbittAPI.Infrastructure.Data;
using OrbittAPI.Infrastructure.Repositories;
using OrbittAPI.Middleware;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ─── Banco de Dados ───────────────────────────────────────────────────────────
// Em produção: trocar por SQL Server com connection string via variável de ambiente
// Para desenvolvimento local: InMemory (sem necessidade de instância SQL)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrEmpty(connectionString))
{
    builder.Services.AddDbContext<OrbittDbContext>(opt =>
        opt.UseInMemoryDatabase("OrbittDev"));
}
else
{
    builder.Services.AddDbContext<OrbittDbContext>(opt =>
        opt.UseSqlServer(connectionString));
}

// ─── JWT Authentication ───────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
    throw new InvalidOperationException(
        "Jwt:Key não configurada ou muito curta (mínimo 32 caracteres). " +
        "Defina em appsettings.json ou via variável de ambiente Jwt__Key.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = !string.IsNullOrEmpty(builder.Configuration["Jwt:Issuer"]),
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = !string.IsNullOrEmpty(builder.Configuration["Jwt:Audience"]),
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ClockSkew = TimeSpan.Zero
        };
    });

// ─── Injeção de Dependência ───────────────────────────────────────────────────
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
builder.Services.AddScoped<IApiCallRepository, ApiCallRepository>();
builder.Services.AddScoped<ISatelliteDataService, SatelliteDataService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// ─── Controllers + Swagger ───────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "OrbittAPI",
        Version = "v1",
        Description = "Plataforma SaaS de acesso a dados satelitais — FIAP Global Solution 2026",
        Contact = new OpenApiContact { Name = "OrbittAPI Team" }
    });

    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Description = "Insira sua API Key no header: X-Api-Key: {sua_chave}",
        Name = "X-Api-Key",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT obtido via /api/auth/login. Insira no formato: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" }
            },
            Array.Empty<string>()
        },
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// ─── Migração automática (InMemory apenas) ────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrbittDbContext>();
    db.Database.EnsureCreated();
}

// ─── Pipeline ─────────────────────────────────────────────────────────────────
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "OrbittAPI v1");
    c.RoutePrefix = "swagger";
});

app.UseMiddleware<GlobalExceptionMiddleware>();  // Tratamento global de exceções
app.UseMiddleware<ApiKeyMiddleware>();           // Autenticação por API Key

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
