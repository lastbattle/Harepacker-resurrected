using HaMCP.Server;
using MapleLib.WzLib;

namespace HaMCP.Core;

/// <summary>
/// Base class for MCP tools providing common functionality.
/// Supports both new Result&lt;T&gt; pattern and legacy result types.
/// </summary>
public abstract class ToolBase
{
    protected readonly WzSessionManager Session;

    protected ToolBase(WzSessionManager session)
    {
        Session = session;
    }

    #region New Result<T> Pattern

    /// <summary>
    /// Executes an action with session validation and exception handling
    /// </summary>
    protected Result<T> Execute<T>(Func<T> action)
    {
        if (!Session.IsInitialized)
            return Result<T>.Fail("No data source initialized. Call init_data_source first.");

        try
        {
            return Result<T>.Ok(action());
        }
        catch (Exception ex)
        {
            return Result<T>.Fail(ex);
        }
    }

    /// <summary>
    /// Executes an action WITHOUT session validation (for init/scan operations)
    /// </summary>
    protected Result<T> ExecuteRaw<T>(Func<T> action)
    {
        try
        {
            return Result<T>.Ok(action());
        }
        catch (Exception ex)
        {
            return Result<T>.Fail(ex);
        }
    }

    #endregion

    #region Legacy Pattern (for backward compatibility)

    /// <summary>
    /// Executes with session check, returning a legacy result type with Success/Error properties
    /// </summary>
    protected T Run<T>(Func<T> action, Func<T> createError, Func<Exception, T> createException) where T : class
    {
        if (!Session.IsInitialized)
            return createError();

        try
        {
            return action();
        }
        catch (Exception ex)
        {
            return createException(ex);
        }
    }

    /// <summary>
    /// Executes without session check, returning a legacy result type
    /// </summary>
    protected T RunRaw<T>(Func<T> action, Func<Exception, T> createException) where T : class
    {
        try
        {
            return action();
        }
        catch (Exception ex)
        {
            return createException(ex);
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Gets an image and ensures it's parsed
    /// </summary>
    protected WzImage GetImage(string category, string imageName)
    {
        return Session.GetImage(category, imageName);
    }

    /// <summary>
    /// Gets a property from an image
    /// </summary>
    protected WzObject? GetProperty(string category, string imageName, string? path)
    {
        var img = GetImage(category, imageName);
        return string.IsNullOrEmpty(path) ? img : img.GetFromPath(path);
    }

    /// <summary>
    /// Gets a typed property from an image, throws if not found or wrong type
    /// </summary>
    protected T GetRequiredProperty<T>(string category, string imageName, string path, string? typeName = null) where T : WzObject
    {
        var prop = GetProperty(category, imageName, path) as T;
        if (prop == null)
        {
            var type = typeName ?? typeof(T).Name.Replace("Wz", "").Replace("Property", "");
            throw new InvalidOperationException($"{type} property not found: {path}");
        }
        return prop;
    }

    /// <summary>
    /// Validates that a required string parameter is not empty
    /// </summary>
    protected static void RequireNonEmpty(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{paramName} cannot be empty", paramName);
    }

    /// <summary>
    /// Returns true if session is initialized
    /// </summary>
    protected bool IsInitialized => Session.IsInitialized;

    /// <summary>
    /// Standard error message for uninitialized session
    /// </summary>
    protected const string NotInitializedError = "No data source initialized";

    #endregion
}
