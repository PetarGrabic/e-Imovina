using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;

namespace eImovina.App.Services;

/// <summary>
/// Typed wrapper over HttpClient used by every page to talk to eImovina.Api. Maps connection
/// failures and non-2xx responses to a typed ApiError instead of letting exceptions/raw status
/// codes leak into page code. The auth DelegatingHandler (Section 5) attaches to the same
/// AddHttpClient&lt;ApiClient&gt; registration this class is resolved from - no changes needed here.
/// </summary>
public class ApiClient
{
    private readonly HttpClient _http;

    public ApiClient(HttpClient http)
    {
        _http = http;
    }

    public Task<ApiResult<T>> GetAsync<T>(string requestUri, CancellationToken ct = default)
        => SendAsync<T>(() => _http.GetAsync(requestUri, ct), ct);

    public Task<ApiResult<TResponse>> PostAsync<TRequest, TResponse>(string requestUri, TRequest body, CancellationToken ct = default)
        => SendAsync<TResponse>(() => _http.PostAsJsonAsync(requestUri, body, ct), ct);

    public Task<ApiResult> PostAsync<TRequest>(string requestUri, TRequest body, CancellationToken ct = default)
        => SendAsync(() => _http.PostAsJsonAsync(requestUri, body, ct), ct);

    public Task<ApiResult<TResponse>> PutAsync<TRequest, TResponse>(string requestUri, TRequest body, CancellationToken ct = default)
        => SendAsync<TResponse>(() => _http.PutAsJsonAsync(requestUri, body, ct), ct);

    public Task<ApiResult> PutAsync<TRequest>(string requestUri, TRequest body, CancellationToken ct = default)
        => SendAsync(() => _http.PutAsJsonAsync(requestUri, body, ct), ct);

    public Task<ApiResult> DeleteAsync(string requestUri, CancellationToken ct = default)
        => SendAsync(() => _http.DeleteAsync(requestUri, ct), ct);

    private async Task<ApiResult<T>> SendAsync<T>(Func<Task<HttpResponseMessage>> send, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await send();
        }
        catch (HttpRequestException ex)
        {
            return ApiResult<T>.Failure(new ApiError(0, $"Nije moguće povezati se s API-jem: {ex.Message}"));
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                var value = await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
                return ApiResult<T>.Success(value!);
            }

            return ApiResult<T>.Failure(await BuildErrorAsync(response, ct));
        }
    }

    private async Task<ApiResult> SendAsync(Func<Task<HttpResponseMessage>> send, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await send();
        }
        catch (HttpRequestException ex)
        {
            return ApiResult.Failure(new ApiError(0, $"Nije moguće povezati se s API-jem: {ex.Message}"));
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                return ApiResult.Success();
            }

            return ApiResult.Failure(await BuildErrorAsync(response, ct));
        }
    }

    private static async Task<ApiError> BuildErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var statusCode = (int)response.StatusCode;
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken: ct);
            var message = problem?.Detail ?? problem?.Title;
            if (!string.IsNullOrWhiteSpace(message))
            {
                return new ApiError(statusCode, message);
            }
        }
        catch
        {
            // Body wasn't valid ProblemDetails JSON - fall through to the generic message below.
        }

        return new ApiError(statusCode, $"API je vratio grešku ({statusCode} {response.ReasonPhrase}).");
    }
}
