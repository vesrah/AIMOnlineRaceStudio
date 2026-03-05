using XrkApi;

var builder = WebApplication.CreateBuilder(args);

if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
    builder.WebHost.UseUrls("http://0.0.0.0:5000");

builder.Services.AddOpenApi();
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 100 * 1024 * 1024);

var app = builder.Build();

app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetService<Microsoft.Extensions.Logging.ILoggerFactory>()?.CreateLogger("XrkApi");
        logger?.LogError(ex, "Unhandled exception");
        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { error = "An error occurred.", detail = ex.Message });
        }
    }
});

app.Use(async (context, next) =>
{
    if (XrkService.IsLocalOrPrivateAddress(context.Connection.RemoteIpAddress))
    {
        await next(context);
        return;
    }
    context.Response.StatusCode = 403;
    await context.Response.WriteAsync(XrkService.PrivateAccessOnlyMessage);
});

if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.MapPost("/csv", (IFormFile file, bool nocache = false) => XrkService.WithXrkFileAsync(file, returnCsv: true, useCache: !nocache))
    .WithName("GetXrkCsv")
    .DisableAntiforgery();

app.MapPost("/metadata", (IFormFile file, bool nocache = false) => XrkService.WithXrkFileAsync(file, returnCsv: false, useCache: !nocache))
    .WithName("GetXrkMetadata")
    .DisableAntiforgery();

app.MapGet("/health", (IHostEnvironment env) => XrkService.GetHealthResult(env))
    .WithName("HealthCheck");

app.MapGet("/cache/{key}", (string key) =>
{
    var exists = XrkService.CacheExists(key);
    return Results.Json(new { key, exists });
})
    .WithName("CacheExists")
    .Produces<object>(200);

app.Run();
