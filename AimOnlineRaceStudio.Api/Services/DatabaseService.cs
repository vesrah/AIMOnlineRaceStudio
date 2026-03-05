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

        var schemaDir = Path.Combine(AppContext.BaseDirectory, "Schema");
        if (!Directory.Exists(schemaDir))
        {
            _logger.LogWarning("Schema directory not found at {Path}", schemaDir);
            return;
        }

        var schemaFiles = Directory.GetFiles(schemaDir, "*.sql")
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToArray();
        if (schemaFiles.Length == 0)
        {
            _logger.LogWarning("No schema files found in {Path}", schemaDir);
            return;
        }

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        foreach (var schemaPath in schemaFiles)
        {
            var sql = await File.ReadAllTextAsync(schemaPath, ct);
            await using var cmd = new NpgsqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync(ct);
            _logger.LogInformation("Applied schema {File}", Path.GetFileName(schemaPath));
        }
        _logger.LogInformation("Schema applied successfully");
    }
}
