# HaMCP Server Documentation

**Version**: 1.0.0
**Last Updated**: 2026-01-05

## Overview

HaMCP is a Model Context Protocol (MCP) server that enables AI assistants (Claude, GPT, etc.) to interact with MapleStory WZ/IMG files programmatically. It provides 74 tools across 10 categories for reading, analyzing, modifying, and exporting game data.

---

## Quick Start

### Installation

```bash
# Build the server
cd HaRepacker-resurrected
dotnet build HaMCP/HaMCP.csproj
```

---

## Connection Methods

HaMCP supports two transport modes: **stdio** (standard input/output) and **HTTP** (Streamable HTTP with SSE).

### Method 1: Stdio (Default)

The stdio method runs the server as a subprocess. The client communicates via stdin/stdout.

```bash
# Run in stdio mode (default)
dotnet run --project HaMCP
```

**Claude Desktop Configuration** (`%APPDATA%\Claude\claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "harepacker": {
      "command": "dotnet",
      "args": ["run", "--project", "E:/path/to/HaMCP/HaMCP.csproj"],
      "env": {
        "HAMCP_DATA_PATH": "D:\\Extract\\v83"
      }
    }
  }
}
```

**Claude Code Configuration** (`.mcp.json` in project root):

```json
{
  "mcpServers": {
    "harepacker": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["run", "--project", "E:/path/to/HaMCP/HaMCP.csproj"],
      "env": {
        "HAMCP_DATA_PATH": "D:\\Extract\\v83"
      }
    }
  }
}
```

---

### Method 2: HTTP (Streamable HTTP)

The HTTP method runs the server as a standalone web service. Clients connect via HTTP requests with Server-Sent Events (SSE) for streaming responses.

```bash
# Run in HTTP mode
dotnet run --project HaMCP -- --http

# Or specify a custom port
dotnet run --project HaMCP -- --http --port 8080
```

**Claude Code Configuration** (`.mcp.json`):

```json
{
  "mcpServers": {
    "harepacker": {
      "type": "http",
      "url": "http://127.0.0.1:13338/mcp",
      "env": {
        "HAMCP_DATA_PATH": "D:\\Extract\\v83"
      }
    }
  }
}
```

> **Note:** When using HTTP mode, the `HAMCP_DATA_PATH` environment variable should be set when starting the server, not in the client config. The `env` block above is shown for reference but is only applied by the client when spawning the server (stdio mode). For HTTP mode, set it before running the server:
> ```bash
> # Windows
> set HAMCP_DATA_PATH=D:\Extract\v83
> dotnet run --project HaMCP -- --http
>
> # Linux/macOS
> HAMCP_DATA_PATH=/path/to/data dotnet run --project HaMCP -- --http
> ```

**Using with curl (testing):**

```bash
# Initialize connection (returns session ID)
curl -X POST http://127.0.0.1:13338/mcp \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"initialize","params":{"capabilities":{}},"id":1}'

# Call a tool
curl -X POST http://127.0.0.1:13338/mcp \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"list_categories","arguments":{}},"id":2}'
```

---

### Environment Variables

| Variable | Description |
|----------|-------------|
| `HAMCP_DATA_PATH` | Path to IMG filesystem extracted by HaCreator (must contain `manifest.json`) |
| `HAMCP_PORT` | Server port for HTTP mode (default: 13338) |
| `HAMCP_TRANSPORT` | Transport mode: `stdio` (default) or `http` |

### Command Line Arguments

| Argument | Description |
|----------|-------------|
| `--http` | Run in HTTP mode instead of stdio |
| `--port <port>` | Set HTTP server port (default: 13338) |
| `--data-path <path>` | Set data source path |

---

### Comparison: Stdio vs HTTP

| Feature | Stdio | HTTP |
|---------|-------|------|
| **Startup** | Launched per-session by client | Runs as persistent service |
| **Performance** | Low latency (direct IPC) | Slight overhead (TCP/HTTP) |
| **Multiple clients** | One client per process | Multiple concurrent clients |
| **Debugging** | Harder (subprocess) | Easier (standalone process) |
| **Deployment** | Embedded | Can run on remote server |
| **Best for** | Claude Desktop, local dev | CI/CD, remote access, shared servers |

**Expected Directory Structure:**
```
D:\Extract\v83\
├── manifest.json          # Version metadata (required)
├── Character/
│   ├── 00002000.img
│   ├── 00012000.img
│   └── ...
├── Map/
│   ├── Map0/
│   │   ├── 000010000.img
│   │   └── ...
│   └── ...
├── Mob/
├── Npc/
├── Sound/
└── ...
```

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│                              AI Clients                                 │
├─────────────────┬─────────────────┬─────────────────────────────────────┤
│  Claude Desktop │   Claude Code   │        Other MCP Clients            │
│   (subprocess)  │  (CLI/remote)   │      (custom applications)          │
└────────┬────────┴────────┬────────┴─────────────────┬───────────────────┘
         │                 │                          │
         │ stdio           │ stdio or HTTP            │ HTTP
         │                 │                          │
         ▼                 ▼                          ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                           HaMCP Server                                  │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │                      Transport Layer                               │ │
│  │  ┌─────────────────────┐      ┌──────────────────────────────────┐ │ │
│  │  │   Stdio Transport   │      │       HTTP Transport             │ │ │
│  │  │   (stdin/stdout)    │      │   (Streamable HTTP + SSE)        │ │ │
│  │  │                     │      │   Port: 13338 (default)          │ │ │
│  │  └──────────┬──────────┘      └────────────────┬─────────────────┘ │ │
│  └─────────────┼──────────────────────────────────┼───────────────────┘ │
│                │                                  │                     │
│                └────────────────┬─────────────────┘                     │
│                                 ▼                                       │
│                    ┌─────────────────────────┐                          │
│                    │   MCP Protocol Handler  │                          │
│                    │   (JSON-RPC 2.0)        │                          │
│                    └────────────┬────────────┘                          │
│                                 ▼                                       │
│                    ┌─────────────────────────┐                          │
│                    │      Tool Classes       │                          │
│                    │   (74 tools, ToolBase)  │                          │
│                    └────────────┬────────────┘                          │
│                                 ▼                                       │
│                    ┌─────────────────────────┐                          │
│                    │   WzSessionManager      │                          │
│                    │   (cache, state)        │                          │
│                    └────────────┬────────────┘                          │
│                                 ▼                                       │
│                    ┌─────────────────────────┐                          │
│                    │       MapleLib          │                          │
│                    │    (WZ/IMG I/O)         │                          │
│                    └─────────────────────────┘                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### Core Components

| Component | Description |
|-----------|-------------|
| `ToolBase` | Base class providing session validation and error handling |
| `Result<T>` | Generic wrapper for tool responses (Success/Error + Data) |
| `WzSessionManager` | Manages loaded data sources and image cache |
| `WzDataConverter` | Converts WZ properties to serializable formats |

---

## Response Format

All tools return `Result<T>` with consistent structure:

```json
{
  "success": true,
  "data": { ... },
  "error": null
}
```

On failure:
```json
{
  "success": false,
  "data": null,
  "error": "Error message"
}
```

---

## Tool Reference

### File Operations (`FileTools`)

Tools for data source initialization and management.

| Tool | Description |
|------|-------------|
| `init_data_source` | Initialize IMG filesystem from directory path |
| `scan_img_directories` | Scan for available IMG filesystems |
| `get_data_source_info` | Get current data source metadata |
| `list_categories` | List available categories (Map, Mob, etc.) |
| `list_images_in_category` | List .img files in a category |
| `get_cache_stats` | Get cache statistics |
| `clear_cache` | Clear loaded image cache |

**Example:**
```
Tool: init_data_source
Parameters: { "basePath": "E:/MapleData" }
Response: { "success": true, "data": { "name": "v176", "categories": ["Map", "Mob", ...] } }
```

---

### Navigation (`NavigationTools`)

Tools for exploring and searching WZ data structures.

| Tool | Description |
|------|-------------|
| `get_subdirectories` | List subdirectories in category |
| `list_properties` | List child properties of a node |
| `get_tree_structure` | Get hierarchical property tree |
| `search_by_name` | Search properties by name pattern |
| `search_by_value` | Search by property value |
| `get_property_path` | Get full path of a property |

**Example:**
```
Tool: search_by_name
Parameters: { "pattern": "*attack*", "category": "Mob" }
Response: { "success": true, "data": { "matches": [...], "totalFound": 42 } }
```

---

### Property Access (`PropertyTools`)

Tools for reading property values with type support.

| Tool | Description |
|------|-------------|
| `get_property` | Get property with full metadata |
| `get_property_value` | Get just the value |
| `get_string` | Get string property value |
| `get_int` | Get integer property value |
| `get_float` | Get float property value |
| `get_vector` | Get vector property (X, Y) |
| `resolve_uol` | Resolve UOL link to target |
| `get_children` | Get all child properties |
| `get_property_count` | Count child properties |
| `iterate_properties` | Iterate with pagination |
| `get_properties_batch` | Get multiple properties at once |

**Property Types:**
- `Null`, `Short`, `Int`, `Long`, `Float`, `Double`, `String`
- `SubProperty` (container), `Canvas` (image), `Vector` (X,Y point)
- `Sound` (audio), `UOL` (link), `Lua` (script), `Convex` (shape)

---

### Canvas Operations (`ImageTools`)

Tools for working with images and animations.

| Tool | Description |
|------|-------------|
| `get_canvas_bitmap` | Get image as base64 PNG |
| `get_canvas_info` | Get canvas metadata |
| `get_canvas_origin` | Get draw offset point |
| `get_canvas_head` | Get head position |
| `get_canvas_bounds` | Get lt/rb bounds |
| `get_canvas_delay` | Get animation frame delay |
| `get_animation_frames` | Get all frames with metadata |
| `list_canvas_in_image` | List all canvases in image |
| `resolve_canvas_link` | Resolve _inlink/_outlink |

**Animation Frame Structure:**
```json
{
  "frameCount": 4,
  "totalDuration": 480,
  "frames": [
    { "index": 0, "width": 100, "height": 120, "origin": {"x": 50, "y": 100}, "delay": 120 }
  ]
}
```

---

### Audio Operations (`AudioTools`)

Tools for working with sound properties.

| Tool | Description |
|------|-------------|
| `get_sound_info` | Get sound metadata |
| `get_sound_data` | Get raw audio as base64 |
| `list_sounds_in_image` | List all sounds in image |
| `resolve_sound_link` | Resolve UOL to sound |

---

### Export Operations (`ExportTools`)

Tools for exporting data to various formats.

| Tool | Description |
|------|-------------|
| `export_to_json` | Export property tree to JSON |
| `export_to_xml` | Export property tree to XML |
| `export_png` | Export canvas to PNG file |
| `export_mp3` | Export sound to MP3 file |
| `export_all_images` | Batch export all canvases |
| `export_all_sounds` | Batch export all sounds |

---

### Analysis (`AnalysisTools`)

Tools for data analysis and validation.

| Tool | Description |
|------|-------------|
| `get_statistics` | Get data source statistics |
| `get_category_summary` | Summarize a category |
| `find_broken_uols` | Find broken UOL references |
| `compare_properties` | Compare two property trees |
| `get_version_info` | Get server version info |
| `validate_image` | Validate image structure |

---

### Modification (`ModifyTools`)

Tools for editing WZ data.

| Tool | Description |
|------|-------------|
| `set_string` | Set string value |
| `set_int` | Set integer value |
| `set_float` | Set float value |
| `set_vector` | Set vector (X, Y) |
| `add_property` | Add new property |
| `delete_property` | Delete property |
| `rename_property` | Rename property |
| `copy_property` | Deep copy property |
| `set_canvas_bitmap` | Replace canvas image |
| `set_canvas_origin` | Set canvas origin |
| `import_png` | Import PNG as canvas |
| `import_sound` | Import audio as sound |
| `save_image` | Save changes to disk |
| `discard_changes` | Revert unsaved changes |

---

### Batch Operations (`BatchTools`)

Tools for bulk operations.

| Tool | Description |
|------|-------------|
| `extract_to_img` | Extract WZ to IMG format |
| `pack_to_wz` | Pack IMG to WZ format |
| `batch_export_images` | Export images from category |
| `batch_search` | Search across categories |

---

### Lifecycle (`LifecycleTools`)

Tools for memory management.

| Tool | Description |
|------|-------------|
| `parse_image` | Parse image into memory |
| `unparse_image` | Free image memory |
| `is_image_parsed` | Check if image is parsed |
| `get_parsed_images` | List parsed images |
| `preload_category` | Preload all images |
| `unload_category` | Unload all images |

---

## Code Architecture

### ToolBase Pattern

All tool classes extend `ToolBase` for consistent error handling:

```csharp
public class MyTools : ToolBase
{
    public MyTools(WzSessionManager session) : base(session) { }

    [McpServerTool(Name = "my_tool")]
    public Result<MyData> MyTool(string param)
    {
        return Execute(() =>
        {
            // Business logic here
            return new MyData { ... };
        });
    }
}
```

### Execute Helpers

| Method | Description |
|--------|-------------|
| `Execute<T>()` | Validates session, catches exceptions |
| `ExecuteRaw<T>()` | No session check (for init operations) |
| `GetImage()` | Get parsed WzImage |
| `GetProperty()` | Get property by path |
| `GetRequiredProperty<T>()` | Get typed property (throws if not found) |

### Result Types

```csharp
// Generic result for tools
public class Result<T>
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public T? Data { get; init; }
}

// Common data types
public record Point2D(int X, int Y);
public record Size2D(int Width, int Height);
```

---

## Project Structure

```
HaMCP/
├── Program.cs                 # Entry point
├── Core/
│   ├── Result.cs              # Generic result types
│   └── ToolBase.cs            # Base class for tools
├── Server/
│   └── WzSessionManager.cs    # Session management
├── Tools/
│   ├── FileTools.cs           # 7 tools
│   ├── NavigationTools.cs     # 6 tools
│   ├── PropertyTools.cs       # 11 tools
│   ├── ImageTools.cs          # 9 tools
│   ├── AudioTools.cs          # 4 tools
│   ├── ExportTools.cs         # 6 tools
│   ├── AnalysisTools.cs       # 6 tools
│   ├── ModifyTools.cs         # 14 tools
│   ├── BatchTools.cs          # 4 tools
│   └── LifecycleTools.cs      # 6 tools
└── Utils/
    └── WzDataConverter.cs     # Type conversion utilities
```

---

## Testing

```bash
# Run unit tests
dotnet test HaMCP.Tests

# Test with MCP Inspector
npx @modelcontextprotocol/inspector dotnet run --project HaMCP
```

---

## Dependencies

```xml
<PackageReference Include="ModelContextProtocol" Version="0.1.0-preview" />
<ProjectReference Include="..\MapleLib\MapleLib.csproj" />
```

---

## License

Same as HaRepacker-resurrected project.
