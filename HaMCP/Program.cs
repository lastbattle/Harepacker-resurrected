using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using ModelContextProtocol.Server;
using HaMCP.Server;
using HaMCP.Tools;

namespace HaMCP;

class Program
{
    static async Task Main(string[] args)
    {
        // Parse transport mode from arguments
        var transport = ParseTransportMode(args);
        var port = ParsePort(args);

        if (transport == TransportMode.Http)
        {
            await RunHttpServer(args, port);
        }
        else
        {
            await RunStdioServer(args);
        }
    }

    private static async Task RunStdioServer(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        // Configure logging to stderr for stdio transport
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options =>
        {
            options.LogToStandardErrorThreshold = LogLevel.Trace;
        });
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        // Register services
        builder.Services.AddSingleton<WzSessionManager>();

        // Configure MCP server with stdio transport
        builder.Services
            .AddMcpServer(options =>
            {
                options.ServerInfo = new()
                {
                    Name = "harepacker-mcp",
                    Version = "1.0.0"
                };
            })
            .WithStdioServerTransport()
            .WithToolsFromAssembly();

        var host = builder.Build();
        await host.RunAsync();
    }

    private static async Task RunHttpServer(string[] args, int port)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Configure logging
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        // Configure URLs
        builder.WebHost.UseUrls($"http://localhost:{port}");

        // Register services
        builder.Services.AddSingleton<WzSessionManager>();

        // Configure MCP server with HTTP/SSE transport
        builder.Services
            .AddMcpServer(options =>
            {
                options.ServerInfo = new()
                {
                    Name = "harepacker-mcp",
                    Version = "1.0.0"
                };
            })
            .WithHttpTransport()
            .WithToolsFromAssembly();

        var app = builder.Build();

        // Log startup info
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("HaRepacker MCP Server starting on http://localhost:{Port}", port);
        logger.LogInformation("Streamable HTTP endpoint: POST http://localhost:{Port}/", port);
        logger.LogInformation("Legacy SSE endpoint: GET http://localhost:{Port}/sse", port);
        logger.LogInformation("Legacy message endpoint: POST http://localhost:{Port}/message", port);

        // Map MCP endpoints (supports both Streamable HTTP and legacy SSE)
        app.MapMcp();

        await app.RunAsync();
    }

    private static TransportMode ParseTransportMode(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i].ToLowerInvariant();

            // Support --transport=http or --transport http
            if (arg == "--transport" && i + 1 < args.Length)
            {
                return args[i + 1].ToLowerInvariant() switch
                {
                    "http" or "sse" or "web" => TransportMode.Http,
                    _ => TransportMode.Stdio
                };
            }

            if (arg.StartsWith("--transport="))
            {
                var value = arg.Substring("--transport=".Length);
                return value switch
                {
                    "http" or "sse" or "web" => TransportMode.Http,
                    _ => TransportMode.Stdio
                };
            }

            // Shorthand flags
            if (arg == "--http" || arg == "-h" || arg == "--sse" || arg == "--web")
            {
                return TransportMode.Http;
            }

            if (arg == "--stdio" || arg == "-s")
            {
                return TransportMode.Stdio;
            }
        }

        // Default to stdio for backwards compatibility
        return TransportMode.Stdio;
    }

    private static int ParsePort(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i].ToLowerInvariant();

            // Support --port=8080 or --port 8080
            if (arg == "--port" && i + 1 < args.Length)
            {
                if (int.TryParse(args[i + 1], out var port))
                    return port;
            }

            if (arg.StartsWith("--port="))
            {
                var value = arg.Substring("--port=".Length);
                if (int.TryParse(value, out var port))
                    return port;
            }

            // Short form -p
            if (arg == "-p" && i + 1 < args.Length)
            {
                if (int.TryParse(args[i + 1], out var port))
                    return port;
            }
        }

        // Default port
        return 13339;
    }

    private enum TransportMode
    {
        Stdio,
        Http
    }
}
