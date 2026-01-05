namespace HaMCP.Core;

/// <summary>
/// Generic result wrapper for MCP tool responses.
/// Provides a consistent pattern for success/failure responses.
/// </summary>
/// <typeparam name="T">The data type on success</typeparam>
public class Result<T>
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public T? Data { get; init; }

    public static Result<T> Ok(T data) => new() { Success = true, Data = data };
    public static Result<T> Fail(string error) => new() { Success = false, Error = error };
    public static Result<T> Fail(Exception ex) => new() { Success = false, Error = ex.Message };
}

/// <summary>
/// Non-generic result for operations without return data
/// </summary>
public class Result
{
    public bool Success { get; init; }
    public string? Error { get; init; }

    public static Result Ok() => new() { Success = true };
    public static Result Fail(string error) => new() { Success = false, Error = error };
    public static Result Fail(Exception ex) => new() { Success = false, Error = ex.Message };
}

/// <summary>
/// Common data types used across multiple tools
/// </summary>
public record Point2D(int X, int Y);
public record Size2D(int Width, int Height);
public record Rect2D(int Left, int Top, int Right, int Bottom);
