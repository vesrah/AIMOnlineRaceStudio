using AimOnlineRaceStudio.Api.Configuration;
using AimOnlineRaceStudio.Api.Services;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 100 * 1024 * 1024); // 100 MB

builder.Services.Configure<XrkApiOptions>(builder.Configuration.GetSection(XrkApiOptions.SectionName));
builder.Services.Configure<CsvStorageOptions>(builder.Configuration.GetSection(CsvStorageOptions.SectionName));
builder.Services.AddHttpClient<IXrkApiClient, XrkApiClient>();
builder.Services.AddScoped<IDatabaseService, DatabaseService>();
builder.Services.AddScoped<IFilesRepository, FilesRepository>();
builder.Services.AddScoped<IFilesService, FilesService>();

builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 100 * 1024 * 1024; // 100 MB
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddControllers();

var app = builder.Build();

app.UseCors();

// Apply schema on startup so DB is ready
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IDatabaseService>();
    try
    {
        await db.ApplySchemaIfNeededAsync();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Schema init failed (DB may not be ready yet)");
    }
}

app.MapControllers();

// Health for Docker
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
