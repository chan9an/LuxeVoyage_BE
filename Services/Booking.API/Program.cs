using Booking.API.Data;
using Booking.API.Repositories;
using Booking.API.Services;
using DotNetEnv;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

// ── Load .env ────────────────────────────────────────────────────────────────
Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Inject .env values into IConfiguration
builder.Configuration["ConnectionStrings:DefaultConnection"] = Environment.GetEnvironmentVariable("DB_CONNECTION")     ?? builder.Configuration["ConnectionStrings:DefaultConnection"];
builder.Configuration["RabbitMQ:Host"]                       = Environment.GetEnvironmentVariable("RABBITMQ_HOST")     ?? builder.Configuration["RabbitMQ:Host"];
builder.Configuration["RabbitMQ:VirtualHost"]                = Environment.GetEnvironmentVariable("RABBITMQ_VHOST")    ?? builder.Configuration["RabbitMQ:VirtualHost"];
builder.Configuration["RabbitMQ:Username"]                   = Environment.GetEnvironmentVariable("RABBITMQ_USERNAME") ?? builder.Configuration["RabbitMQ:Username"];
builder.Configuration["RabbitMQ:Password"]                   = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") ?? builder.Configuration["RabbitMQ:Password"];

// ── DbContext ─────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<BookingDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── MassTransit → RabbitMQ ────────────────────────────────────────────────────
var rabbit = builder.Configuration.GetSection("RabbitMQ");
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<RoomReservedConsumer>();
    x.AddConsumer<RoomReservationFailedConsumer>();
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
builder.Services.AddScoped<IBookingService, BookingService>();

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
app.UseAuthorization();
app.MapControllers();
app.Run();
