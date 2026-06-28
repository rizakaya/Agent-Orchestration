using System.Text.Json;
using System.Text.Json.Serialization;
using AgentToolGateway.Api.Abstractions;
using AgentToolGateway.Api.Contracts;
using Microsoft.Extensions.Options;

namespace AgentToolGateway.Api.Infrastructure;

public sealed class JsonlToolAuditStore(
    IOptions<ToolGatewayOptions> options,
    IWebHostEnvironment environment) : IToolAuditStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task WriteAsync(ToolAuditLogEntry entry, CancellationToken cancellationToken)
    {
        var path = ResolvePath(options.Value.AuditLogPath, environment.ContentRootPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var line = JsonSerializer.Serialize(entry, JsonOptions);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(path, line + Environment.NewLine, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    private static string ResolvePath(string configuredPath, string contentRoot)
    {
        return Path.IsPathRooted(configuredPath)
            ? Path.GetFullPath(configuredPath)
            : Path.GetFullPath(Path.Combine(contentRoot, configuredPath));
    }
}
