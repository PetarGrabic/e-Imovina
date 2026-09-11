namespace eImovina.App.Services;

/// <summary>Result of an Api call that returns a response body.</summary>
public sealed class ApiResult<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public ApiError? Error { get; }

    private ApiResult(bool isSuccess, T? value, ApiError? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static ApiResult<T> Success(T value) => new(true, value, null);
    public static ApiResult<T> Failure(ApiError error) => new(false, default, error);
}

/// <summary>Result of an Api call with no response body (POST/PUT/DELETE actions).</summary>
public sealed class ApiResult
{
    public bool IsSuccess { get; }
    public ApiError? Error { get; }

    private ApiResult(bool isSuccess, ApiError? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static ApiResult Success() => new(true, null);
    public static ApiResult Failure(ApiError error) => new(false, error);
}
