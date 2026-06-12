using System.Text;
using System.Text.Json;
using ApiLogger.Core.Models;
using ApiLogger.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApiLogger.Core.Services;

/// <summary>
/// รับผิดชอบยิง HTTP POST ไปที่ Log API (CreateLog) จริง ๆ
/// </summary>
public interface ILogSender
{
    Task SendAsync(LogEntry entry, CancellationToken cancellationToken);
}

public class LogSender : ILogSender
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
    };

    private readonly HttpClient _httpClient;
    private readonly ApiLoggerOptions _options;
    private readonly ILogger<LogSender> _logger;

    public LogSender(HttpClient httpClient, IOptions<ApiLoggerOptions> options, ILogger<LogSender> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(LogEntry entry, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.CreateLogUrl))
        {
            _logger.LogWarning("ApiLogger: CreateLogUrl is not configured, skip sending log.");
            return;
        }

        var json = JsonSerializer.Serialize(entry, JsonOptions);

        for (var attempt = 1; attempt <= Math.Max(1, _options.MaxRetryCount); attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, _options.CreateLogUrl)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };

                if (!string.IsNullOrWhiteSpace(_options.ApiKey))
                {
                    request.Headers.Add("apiKey", _options.ApiKey);
                }

                using var response = await _httpClient.SendAsync(request, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    return;
                }

                _logger.LogWarning(
                    "ApiLogger: CreateLog API returned {StatusCode} (attempt {Attempt}/{MaxAttempt}). Command={Command}",
                    (int)response.StatusCode, attempt, _options.MaxRetryCount, entry.Command);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "ApiLogger: failed to send log to CreateLog API (attempt {Attempt}/{MaxAttempt}). Command={Command}",
                    attempt, _options.MaxRetryCount, entry.Command);
            }

            if (attempt < _options.MaxRetryCount)
            {
                await Task.Delay(_options.RetryDelayMilliseconds, cancellationToken);
            }
        }

        // ส่งไม่สำเร็จแม้ retry ครบแล้ว -> ไม่ throw ต่อ เพื่อไม่ให้กระทบ flow หลักของระบบ
        _logger.LogError("ApiLogger: giving up sending log after {MaxAttempt} attempts. Command={Command}",
            _options.MaxRetryCount, entry.Command);
    }
}
