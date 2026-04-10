using Auth.API.Domain.Entities;
using Auth.API.Infrastructure.Data;
using DotNetEnv;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

// ── Load .env ────────────────────────────────────────────────────────────────
Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Inject .env values into IConfiguration
builder.Configuration["ConnectionStrings:DefaultConnection"]  = Environment.GetEnvironmentVariable("DB_CONNECTION")           ?? builder.Configuration["ConnectionStrings:DefaultConnection"];
builder.Configuration["JwtSettings:Secret"]                   = Environment.GetEnvironmentVariable("JWT_SECRET")              ?? builder.Configuration["JwtSettings:Secret"];
builder.Configuration["JwtSettings:Issuer"]                   = Environment.GetEnvironmentVariable("JWT_ISSUER")              ?? builder.Configuration["JwtSettings:Issuer"];
builder.Configuration["JwtSettings:Audience"]                 = Environment.GetEnvironmentVariable("JWT_AUDIENCE")            ?? builder.Configuration["JwtSettings:Audience"];
builder.Configuration["JwtSettings:ExpiryMinutes"]            = Environment.GetEnvironmentVariable("JWT_EXPIRY_MINUTES")      ?? builder.Configuration["JwtSettings:ExpiryMinutes"];
builder.Configuration["Authentication:Google:ClientId"]       = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID")        ?? builder.Configuration["Authentication:Google:ClientId"];
builder.Configuration["Authentication:Google:ClientSecret"]   = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET")    ?? builder.Configuration["Authentication:Google:ClientSecret"];
builder.Configuration["RabbitMQ:Host"]                        = Environment.GetEnvironmentVariable("RABBITMQ_HOST")           ?? builder.Configuration["RabbitMQ:Host"];
builder.Configuration["RabbitMQ:VirtualHost"]                 = Environment.GetEnvironmentVariable("RABBITMQ_VHOST")          ?? builder.Configuration["RabbitMQ:VirtualHost"];
builder.Configuration["RabbitMQ:Username"]                    = Environment.GetEnvironmentVariable("RABBITMQ_USERNAME")       ?? builder.Configuration["RabbitMQ:Username"];
builder.Configuration["RabbitMQ:Password"]                    = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD")       ?? builder.Configuration["RabbitMQ:Password"];

// ── DbContext ─────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Identity ──────────────────────────────────────────────────────────────────
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<AuthDbContext>()
    .AddDefaultTokenProviders();

// ── JWT + Google OAuth ────────────────────────────────────────────────────────
var jwtSettings = builder.Configuration.GetSection("JwtSettings");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer           = true,
        ValidateAudience         = true,
        ValidateLifetime         = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer              = jwtSettings["Issuer"],
        ValidAudience            = jwtSettings["Audience"],
        IssuerSigningKey         = new SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(jwtSettings["Secret"]!))
    };
})
.AddGoogle(options =>
{
    options.ClientId     = builder.Configuration["Authentication:Google:ClientId"]     ?? throw new InvalidOperationException("Google ClientId missing");
    options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? throw new InvalidOperationException("Google ClientSecret missing");
});

// ── CORS ──────────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddCors(options =>
    options.AddPolicy("AllowAngular", policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()));

// ── Swagger ───────────────────────────────────────────────────────────────────
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Auth.API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization", Type = SecuritySchemeType.Http,
        Scheme = "Bearer", BearerFormat = "JWT", In = ParameterLocation.Header
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {{
        new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
        Array.Empty<string>()
    }});
});

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddScoped<Auth.API.Application.Interfaces.IJwtTokenGenerator,
                           Auth.API.Infrastructure.Security.JwtTokenGenerator>();

// ── MassTransit → RabbitMQ ────────────────────────────────────────────────────
var rabbit = builder.Configuration.GetSection("RabbitMQ");
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(rabbit["Host"], rabbit["VirtualHost"], h =>
        {
            h.Username(rabbit["Username"]!);
            h.Password(rabbit["Password"]!);
        });
    });
});

// ── Pipeline ──────────────────────────────────────────────────────────────────
var app = builder.Build();

using (var scope = app.Services.CreateScope())
    await Auth.API.Infrastructure.Data.RoleSeeder.SeedRolesAsync(scope.ServiceProvider);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAngular");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
