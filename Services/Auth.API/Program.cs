using Auth.API.Domain.Entities;
using Auth.API.Infrastructure.Data;
using DotNetEnv;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

/*
 * DotNetEnv is a third-party NuGet package that reads a .env file from the project root and loads
 * each key-value pair into the process's environment variables. We call Env.Load() as the very first
 * thing in the application so that by the time the builder reads configuration, all our secrets are
 * already available. This keeps credentials out of appsettings.json and out of source control entirely.
 */
Env.Load();

var builder = WebApplication.CreateBuilder(args);

/*
 * This block manually bridges the gap between environment variables and ASP.NET's IConfiguration system.
 * The ?? fallback pattern means that if an environment variable is missing (e.g. in a CI environment
 * that hasn't set it up yet), we gracefully fall back to whatever is in appsettings.json rather than
 * crashing. In production, the environment variables always win.
 */
builder.Configuration["ConnectionStrings:DefaultConnection"]  = Environment.GetEnvironmentVariable("DB_CONNECTION")           ?? builder.Configuration["ConnectionStrings:DefaultConnection"];
builder.Configuration["JwtSettings:Secret"]                   = Environment.GetEnvironmentVariable("JWT_SECRET")              ?? builder.Configuration["JwtSettings:Secret"];
builder.Configuration["JwtSettings:Issuer"]                   = Environment.GetEnvironmentVariable("JWT_ISSUER")              ?? builder.Configuration["JwtSettings:Issuer"];
builder.Configuration["JwtSettings:Audience"]                 = Environment.GetEnvironmentVariable("JWT_AUDIENCE")            ?? builder.Configuration["JwtSettings:Audience"];
builder.Configuration["JwtSettings:ExpiryMinutes"]            = Environment.GetEnvironmentVariable("JWT_EXPIRY_MINUTES")      ?? builder.Configuration["JwtSettings:ExpiryMinutes"];
builder.Configuration["RabbitMQ:Host"]                        = Environment.GetEnvironmentVariable("RABBITMQ_HOST")           ?? builder.Configuration["RabbitMQ:Host"];
builder.Configuration["RabbitMQ:VirtualHost"]                 = Environment.GetEnvironmentVariable("RABBITMQ_VHOST")          ?? builder.Configuration["RabbitMQ:VirtualHost"];
builder.Configuration["RabbitMQ:Username"]                    = Environment.GetEnvironmentVariable("RABBITMQ_USERNAME")       ?? builder.Configuration["RabbitMQ:Username"];
builder.Configuration["RabbitMQ:Password"]                    = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD")       ?? builder.Configuration["RabbitMQ:Password"];

builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

/*
 * AddIdentity wires up the full ASP.NET Core Identity system — user creation, password hashing,
 * role management, lockout policies, and the claims infrastructure. AddEntityFrameworkStores tells
 * Identity to use our AuthDbContext as its backing store, so all user data goes into SQL Server.
 * AddDefaultTokenProviders registers the built-in token generators that we rely on for OTP generation
 * and password reset flows — without this, GeneratePasswordResetTokenAsync would throw at runtime.
 */
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<AuthDbContext>()
    .AddDefaultTokenProviders();

var jwtSettings = builder.Configuration.GetSection("JwtSettings");

/*
 * JWT is the only authentication scheme we need. The JwtBearer middleware intercepts every
 * incoming request, validates the Authorization: Bearer header, and rejects anything that
 * doesn't pass — wrong issuer, expired token, bad signature all get a 401.
 */
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
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddCors(options =>
    options.AddPolicy("AllowAngular", policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()));

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

builder.Services.AddScoped<Auth.API.Application.Interfaces.IJwtTokenGenerator,
                           Auth.API.Infrastructure.Security.JwtTokenGenerator>();

/*
 * MassTransit is a message bus abstraction that sits on top of RabbitMQ. Rather than talking to
 * RabbitMQ directly with raw AMQP calls, we use MassTransit's higher-level API which handles
 * connection management, serialisation, and retry policies for us. Auth.API is a publisher only —
 * it fires events like IUserRegisteredEvent and IEmailVerificationRequestedEvent and then forgets
 * about them. The Notification.Worker service is the one that actually consumes those events and
 * sends emails. This decoupling means Auth.API never has to wait for an email to be sent before
 * returning a response to the user. Port 5671 with TLS is required because we're using CloudAMQP,
 * which is a cloud-hosted RabbitMQ service that enforces encrypted connections.
 */
var rabbit = builder.Configuration.GetSection("RabbitMQ");
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(rabbit["Host"], 5671, rabbit["VirtualHost"], h =>
        {
            h.Username(rabbit["Username"]!);
            h.Password(rabbit["Password"]!);
            h.UseSsl(ssl => ssl.Protocol = System.Security.Authentication.SslProtocols.Tls12);
        });
    });
});

var app = builder.Build();

/*
 * We seed the two application roles (HotelManager and Customer) on every startup. The seeder
 * checks whether each role already exists before creating it, so this is completely safe to run
 * repeatedly. It means we never have to manually run a SQL script or migration to set up roles
 * in a fresh environment — the app bootstraps itself.
 */
using (var scope = app.Services.CreateScope())
    await Auth.API.Infrastructure.Data.RoleSeeder.SeedRolesAsync(scope.ServiceProvider);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Middleware order is critical here. CORS headers must be added before the auth middleware runs,
// otherwise preflight OPTIONS requests get rejected before they can be processed correctly.
app.UseCors("AllowAngular");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
