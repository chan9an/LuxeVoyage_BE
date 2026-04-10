using DotNetEnv;
using Hotel.API.Data;
using Hotel.API.Repositories;
using Hotel.API.Services;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Shared.Events;
using System.Text;
using System.Text.Json.Serialization;

// ── Load .env ────────────────────────────────────────────────────────────────
Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Inject .env values into IConfiguration
builder.Configuration["ConnectionStrings:DefaultConnection"] = Environment.GetEnvironmentVariable("DB_CONNECTION")        ?? builder.Configuration["ConnectionStrings:DefaultConnection"];
builder.Configuration["JwtSettings:Secret"]                  = Environment.GetEnvironmentVariable("JWT_SECRET")           ?? builder.Configuration["JwtSettings:Secret"];
builder.Configuration["JwtSettings:Issuer"]                  = Environment.GetEnvironmentVariable("JWT_ISSUER")           ?? builder.Configuration["JwtSettings:Issuer"];
builder.Configuration["JwtSettings:Audience"]                = Environment.GetEnvironmentVariable("JWT_AUDIENCE")         ?? builder.Configuration["JwtSettings:Audience"];
builder.Configuration["Cloudinary:CloudName"]                = Environment.GetEnvironmentVariable("CLOUDINARY_CLOUD_NAME") ?? builder.Configuration["Cloudinary:CloudName"];
builder.Configuration["Cloudinary:ApiKey"]                   = Environment.GetEnvironmentVariable("CLOUDINARY_API_KEY")    ?? builder.Configuration["Cloudinary:ApiKey"];
builder.Configuration["Cloudinary:ApiSecret"]                = Environment.GetEnvironmentVariable("CLOUDINARY_API_SECRET") ?? builder.Configuration["Cloudinary:ApiSecret"];
builder.Configuration["RabbitMQ:Host"]                       = Environment.GetEnvironmentVariable("RABBITMQ_HOST")        ?? builder.Configuration["RabbitMQ:Host"];
builder.Configuration["RabbitMQ:VirtualHost"]                = Environment.GetEnvironmentVariable("RABBITMQ_VHOST")       ?? builder.Configuration["RabbitMQ:VirtualHost"];
builder.Configuration["RabbitMQ:Username"]                   = Environment.GetEnvironmentVariable("RABBITMQ_USERNAME")    ?? builder.Configuration["RabbitMQ:Username"];
builder.Configuration["RabbitMQ:Password"]                   = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD")    ?? builder.Configuration["RabbitMQ:Password"];

// ── DbContext ─────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<HotelDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── JWT Authentication (validates tokens from Auth.API) ───────────────────────
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["JwtSettings:Issuer"],
            ValidAudience            = builder.Configuration["JwtSettings:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:Secret"]!))
        };
    });

builder.Services.AddAuthorization();

// ── MassTransit → RabbitMQ ────────────────────────────────────────────────────
var rabbit = builder.Configuration.GetSection("RabbitMQ");
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<BookingStartedConsumer>();
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(rabbit["Host"], rabbit["VirtualHost"], h =>
        {
            h.Username(rabbit["Username"]!);
            h.Password(rabbit["Password"]!);
        });
        cfg.ConfigureEndpoints(ctx);
    });
});

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IHotelService, HotelService>();
builder.Services.AddScoped<ICloudinaryService, CloudinaryService>();

// ── CORS ──────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
    options.AddPolicy("AllowAngular", policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()));

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ── Pipeline ──────────────────────────────────────────────────────────────────
var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AllowAngular");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
