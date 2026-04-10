using DotNetEnv;
using MassTransit;
using Notification.Worker;

// ── Load .env ────────────────────────────────────────────────────────────────
Env.Load();

var builder = Host.CreateApplicationBuilder(args);

// Inject .env values into IConfiguration
builder.Configuration["RabbitMQ:Host"]      = Environment.GetEnvironmentVariable("RABBITMQ_HOST")     ?? builder.Configuration["RabbitMQ:Host"];
builder.Configuration["RabbitMQ:VirtualHost"]= Environment.GetEnvironmentVariable("RABBITMQ_VHOST")    ?? builder.Configuration["RabbitMQ:VirtualHost"];
builder.Configuration["RabbitMQ:Username"]  = Environment.GetEnvironmentVariable("RABBITMQ_USERNAME") ?? builder.Configuration["RabbitMQ:Username"];
builder.Configuration["RabbitMQ:Password"]  = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") ?? builder.Configuration["RabbitMQ:Password"];
builder.Configuration["Smtp:Host"]          = Environment.GetEnvironmentVariable("SMTP_HOST")         ?? builder.Configuration["Smtp:Host"];
builder.Configuration["Smtp:Port"]          = Environment.GetEnvironmentVariable("SMTP_PORT")         ?? builder.Configuration["Smtp:Port"];
builder.Configuration["Smtp:Username"]      = Environment.GetEnvironmentVariable("SMTP_USERNAME")     ?? builder.Configuration["Smtp:Username"];
builder.Configuration["Smtp:Password"]      = Environment.GetEnvironmentVariable("SMTP_PASSWORD")     ?? builder.Configuration["Smtp:Password"];
builder.Configuration["Smtp:FromEmail"]     = Environment.GetEnvironmentVariable("SMTP_FROM_EMAIL")   ?? builder.Configuration["Smtp:FromEmail"];
builder.Configuration["Smtp:FromName"]      = Environment.GetEnvironmentVariable("SMTP_FROM_NAME")    ?? builder.Configuration["Smtp:FromName"];

var rabbit = builder.Configuration.GetSection("RabbitMQ");

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<UserRegisteredConsumer>();
    x.AddConsumer<BookingConfirmedConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(rabbit["Host"], rabbit["VirtualHost"], h =>
        {
            h.Username(rabbit["Username"]!);
            h.Password(rabbit["Password"]!);
        });

        cfg.ReceiveEndpoint("luxevoyage.user-registered", e =>
        {
            e.Durable = true;
            e.ConfigureConsumer<UserRegisteredConsumer>(ctx);
        });

        cfg.ReceiveEndpoint("luxevoyage.booking-started", e =>
        {
            e.Durable = true;
            e.ConfigureConsumer<BookingConfirmedConsumer>(ctx);
        });
    });
});

var host = builder.Build();
await host.RunAsync();
