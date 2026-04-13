/*
 * Ocelot is a .NET API Gateway library. Its job is to be the single entry point for all
 * HTTP traffic coming from the Angular frontend. Instead of the frontend knowing about
 * five different microservice ports, it only ever talks to this one gateway on port 7217.
 * Ocelot reads ocelot.json at startup and uses those route definitions to figure out
 * which downstream service to forward each incoming request to.
 */
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

/*
 * We load ocelot.json as a separate configuration file rather than putting routes inside
 * appsettings.json. This keeps the routing config clean and isolated. The reloadOnChange: true
 * flag means Ocelot will pick up changes to ocelot.json without needing a full restart —
 * useful when you're adding new routes during development.
 */
builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

/*
 * CORS must be configured at the gateway level because this is the only service the browser
 * actually talks to directly. The individual microservices (Auth.API, Hotel.API, Booking.API)
 * also have their own CORS policies, but those only matter when you hit them directly —
 * in normal operation all browser traffic goes through here first.
 */
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// AddOcelot registers all the Ocelot middleware and services needed for request routing.
builder.Services.AddOcelot();

var app = builder.Build();

// CORS must be applied before Ocelot processes the request, otherwise preflight OPTIONS
// requests get swallowed by the routing middleware before the CORS headers are added.
app.UseCors("AllowAngular");

// Simple health check endpoint — useful for confirming the gateway is alive without
// triggering any downstream service calls.
app.MapGet("/", () => "Hello World from ApiGateway!");

/*
 * UseOcelot() is the main middleware that does all the heavy lifting — it matches the
 * incoming request URL against the routes defined in ocelot.json, rewrites the path,
 * and proxies the request to the correct downstream service. The .Wait() call is because
 * Ocelot's middleware registration is async but the ASP.NET pipeline expects synchronous
 * registration here. It's a known Ocelot pattern, not a mistake.
 */
app.UseOcelot().Wait();

app.Run();
