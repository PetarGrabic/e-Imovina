namespace eImovina.App.Services;

/// <summary>
/// Typed representation of a failed Api call. StatusCode is 0 for connection-level failures
/// (Api unreachable) since there's no HTTP response to carry a real status code.
/// </summary>
public sealed record ApiError(int StatusCode, string Message);
