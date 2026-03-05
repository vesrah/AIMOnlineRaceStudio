using Npgsql;

namespace AimOnlineRaceStudio.Api.Services;

public interface IDatabaseService
{
    Task ApplySchemaIfNeededAsync(CancellationToken ct = default);
}

public class DatabaseService : IDatabaseService
{
    private readonly IConfiguration _config;
    private readonly ILogger<DatabaseService> _logger;

    public DatabaseService(IConfiguration config, ILogger<DatabaseService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task ApplySchemaIfNeededAsync(CancellationToken ct = default)
    {
        var connectionString = _config.GetConnectionString("Default");
        if (string.IsNullOrEmpty(connectionString))
        {
            _logger.LogWarning("No ConnectionStrings:Default configured; skipping schema init");
            return;
        }

        var schemaPath = Path.Combine(AppContext.BaseDirectory, "Schema", "01_schema.sql");
        if (!File.Exists(schemaPath))
        {
            _logger.LogWarning("Schema file not found at {Path}", schemaPath);
            return;
        }

        var sql = await File.ReadAllTextAsync(schemaPath, ct);
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(ct);
        _logger.LogInformation("Schema applied successfully");
    }
}
