# nullable enable

using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EAABAddIn.Src.Core.Http;

/// <summary>
/// Servicio HTTP ligero (GET / POST) con:
///  - Manejo explícito de errores 400
///  - Timeout por solicitud
///  - Cache en memoria (thread-safe) opcional
///  - Sin librerías externas (solo BCL)
/// </summary>
public sealed class HttpService
{
    private static readonly Lazy<HttpService> _instance = new(() => new HttpService());
    public static HttpService Instance => _instance.Value;

    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

    private HttpService()
    {
        _httpClient = new HttpClient();
    }

    #region Public API
    public Task<HttpResult> GetAsync(string url, HttpRequestOptions? options = null) =>
        SendAsync(HttpMethod.Get, url, null, null, options);

    public Task<HttpResult> PostAsync(string url, string? body, string? contentType = "application/json", HttpRequestOptions? options = null) =>
        SendAsync(HttpMethod.Post, url, body, contentType, options);
    #endregion

    #region Core Logic
    private async Task<HttpResult> SendAsync(HttpMethod method, string url, string? body, string? contentType, HttpRequestOptions? options)
    {
        options ??= HttpRequestOptions.Default;
        var cacheKey = BuildCacheKey(method, url, body);

        // Intentar cache (solo GET o POST permitido explícitamente)
        if (options.UseCache && (method == HttpMethod.Get || (method == HttpMethod.Post && options.CachePostResponses)))
        {
            if (_cache.TryGetValue(cacheKey, out var entry) && !entry.IsExpired)
            {
                return HttpResult.Success(HttpStatusCode.OK, entry.Content, fromCache: true);
            }
        }

        using var cts = new CancellationTokenSource(options.Timeout);
        try
        {
            using var request = new HttpRequestMessage(method, url);
            if (method == HttpMethod.Post && body != null)
            {
                request.Content = new StringContent(body, Encoding.UTF8, contentType ?? "text/plain");
            }

            var response = await _httpClient.SendAsync(request, cts.Token).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                if (options.ThrowOn400)
                    throw new HttpBadRequestException("HTTP 400 Bad Request", content, url);

                return HttpResult.Failure(response.StatusCode, "Solicitud inválida (400)", content);
            }

            if (!response.IsSuccessStatusCode)
            {
                return HttpResult.Failure(response.StatusCode, $"Error HTTP {(int)response.StatusCode}", content);
            }

            // Guardar en cache si aplica
            if (options.UseCache && (method == HttpMethod.Get || (method == HttpMethod.Post && options.CachePostResponses)))
            {
                var newEntry = new CacheEntry(content, DateTimeOffset.UtcNow.Add(options.CacheDuration));
                _cache[cacheKey] = newEntry;
            }

            return HttpResult.Success(response.StatusCode, content, fromCache: false);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            return HttpResult.Failure(0, "Timeout de la solicitud");
        }
        catch (HttpBadRequestException)
        {
            throw; // Propagar si se configuró ThrowOn400
        }
        catch (Exception ex)
        {
            return HttpResult.Failure(0, ex.Message);
        }
        finally
        {
            // Limpieza simple de entradas expiradas (lazy)
            CleanupExpiredEntries();
        }
    }
    #endregion

    #region Helpers
    private static string BuildCacheKey(HttpMethod method, string url, string? body)
    {
        if (string.IsNullOrEmpty(body)) return method + "::" + url;
        var hash = body.GetHashCode(); // suficiente para cache simple
        return method + "::" + url + "::" + hash;
    }

    private void CleanupExpiredEntries()
    {
        if (_cache.IsEmpty) return;
        foreach (var kv in _cache)
        {
            if (kv.Value.IsExpired)
            {
                _cache.TryRemove(kv.Key, out _);
            }
        }
    }
    #endregion

    #region Nested Types
    private sealed record CacheEntry(string Content, DateTimeOffset ExpiresAt)
    {
        public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
    }

    public sealed class HttpRequestOptions
    {
        public static HttpRequestOptions Default => new();
        public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
        public bool UseCache { get; init; } = true;
        public TimeSpan CacheDuration { get; init; } = TimeSpan.FromMinutes(5);
        public bool CachePostResponses { get; init; } = false;
        public bool ThrowOn400 { get; init; } = false;
    }

    public sealed class HttpResult
    {
        public bool IsSuccess { get; private init; }
        public bool FromCache { get; private init; }
        public HttpStatusCode? StatusCode { get; private init; }
        public string? Content { get; private init; }
        public string? ErrorMessage { get; private init; }

        public static HttpResult Success(HttpStatusCode status, string content, bool fromCache) => new()
        {
            IsSuccess = true,
            StatusCode = status,
            Content = content,
            FromCache = fromCache
        };

        public static HttpResult Failure(HttpStatusCode status, string error, string? content = null) => new()
        {
            IsSuccess = false,
            StatusCode = status,
            ErrorMessage = error,
            Content = content,
            FromCache = false
        };

        public static HttpResult Failure(int statusCode, string error) => new()
        {
            IsSuccess = false,
            StatusCode = statusCode == 0 ? null : (HttpStatusCode?)statusCode,
            ErrorMessage = error,
            FromCache = false
        };
    }

    public sealed class HttpBadRequestException : Exception
    {
        public string? ResponseContent { get; }
        public string Url { get; }
        public HttpBadRequestException(string message, string? responseContent, string url) : base(message)
        {
            ResponseContent = responseContent;
            Url = url;
        }
    }
    #endregion
}

