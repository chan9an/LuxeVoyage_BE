using DotNetEnv;
using MassTransit;
using Notification.Worker;

Env.Load();

/*
 * Host.CreateApplicationBuilder is the worker service equivalent of WebApplication.CreateBuilder.
 * Unlike a web API, a worker service doesn't have an HTTP pipeline — it's a long-running background
 * process that just sits there listening for messages. The Host abstraction gives us dependency
 * injection, configuration, and logging without any of the web server overhead.
 */
var builder = Host.CreateApplicationBuilder(args);

builder.Configuration["RabbitMQ:Host"]        = Environment.GetEnvironmentVariable("RABBITMQ_HOST")     ?? builder.Configuration["RabbitMQ:Host"];
builder.Configuration["RabbitMQ:VirtualHost"]  = Environment.GetEnvironmentVariable("RABBITMQ_VHOST")    ?? builder.Configuration["RabbitMQ:VirtualHost"];
builder.Configuration["RabbitMQ:Username"]     = Environment.GetEnvironmentVariable("RABBITMQ_USERNAME") ?? builder.Configuration["RabbitMQ:Username"];
builder.Configuration["RabbitMQ:Password"]     = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") ?? builder.Configuration["RabbitMQ:Password"];
builder.Configuration["Smtp:Host"]             = Environment.GetEnvironmentVariable("SMTP_HOST")         ?? builder.Configuration["Smtp:Host"];
builder.Configuration["Smtp:Port"]             = Environment.GetEnvironmentVariable("SMTP_PORT")         ?? builder.Configuration["Smtp:Port"];
builder.Configuration["Smtp:Username"]         = Environment.GetEnvironmentVariable("SMTP_USERNAME")     ?? builder.Configuration["Smtp:Username"];
builder.Configuration["Smtp:Password"]         = Environment.GetEnvironmentVariable("SMTP_PASSWORD")     ?? builder.Configuration["Smtp:Password"];
builder.Configuration["Smtp:FromEmail"]        = Environment.GetEnvironmentVariable("SMTP_FROM_EMAIL")   ?? builder.Configuration["Smtp:FromEmail"];
builder.Configuration["Smtp:FromName"]         = Environment.GetEnvironmentVariable("SMTP_FROM_NAME")    ?? builder.Configuration["Smtp:FromName"];

var rabbit = builder.Configuration.GetSection("RabbitMQ");

/*
 * This is where the entire messaging architecture of the Notification.Worker gets wired up.
 * AddMassTransit registers MassTransit as a hosted service, which means it starts automatically
 * when the application starts and maintains a persistent connection to RabbitMQ. Each consumer
 * class is registered with AddConsumer, which tells MassTransit to instantiate it via DI whenever
 * a matching message arrives. The UsingRabbitMq call configures the transport layer — we connect
 * to CloudAMQP on port 5671 with TLS because CloudAMQP is a cloud-hosted broker that requires
 * encrypted connections. Each ReceiveEndpoint maps a named durable queue to a specific consumer.
 * Durable means the queue survives a broker restart, so messages aren't lost if RabbitMQ goes down
 * briefly and comes back up.
 */
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<UserRegisteredConsumer>();
    x.AddConsumer<BookingConfirmedConsumer>();
    x.AddConsumer<PasswordResetConsumer>();
    x.AddConsumer<EmailVerificationConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(rabbit["Host"], 5671, rabbit["VirtualHost"], h =>
        {
            h.Username(rabbit["Username"]!);
            h.Password(rabbit["Password"]!);
            h.UseSsl(ssl => ssl.Protocol = System.Security.Authentication.SslProtocols.Tls12);
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

        cfg.ReceiveEndpoint("luxevoyage.password-reset", e =>
        {
            e.Durable = true;
            e.ConfigureConsumer<PasswordResetConsumer>(ctx);
        });

        cfg.ReceiveEndpoint("luxevoyage.email-verification", e =>
        {
            e.Durable = true;
            e.ConfigureConsumer<EmailVerificationConsumer>(ctx);
        });
    });
});

var host = builder.Build();
await host.RunAsync();
