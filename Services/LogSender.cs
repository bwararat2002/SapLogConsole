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
        var maxAttempts = Math.Max(1, _options.MaxRetryCount);
        Exception? lastException = null;
        int? lastStatusCode = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            lastException = null;
            lastStatusCode = null;

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

                lastStatusCode = (int)response.StatusCode;
                _logger.LogWarning(
                    "ApiLogger: CreateLog API returned {StatusCode} (attempt {Attempt}/{MaxAttempt}). Command={Command}",
                    lastStatusCode, attempt, maxAttempts, entry.Command);
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex,
                    "ApiLogger: failed to send log to CreateLog API (attempt {Attempt}/{MaxAttempt}). Command={Command}",
                    attempt, maxAttempts, entry.Command);
            }

            if (attempt < maxAttempts)
            {
                await Task.Delay(_options.RetryDelayMilliseconds, cancellationToken);
            }
        }

        // ส่งไม่สำเร็จแม้ retry ครบแล้ว
        _logger.LogError("ApiLogger: giving up sending log after {MaxAttempt} attempts. Command={Command}",
            maxAttempts, entry.Command);

        if (_options.OnSendError is not null)
        {
            // lastException จะเป็น null ถ้าเหตุผลที่ fail คือ API ตอบ non-success status code
            // ในกรณีนั้นให้สร้าง HttpRequestException เพื่อให้ผู้ใช้ได้รับ context ที่มีประโยชน์
            var errorToReport = lastException
                ?? new HttpRequestException(
                    $"ApiLogger: CreateLog API returned non-success status code {lastStatusCode} for Command={entry.Command}");

            try
            {
                _options.OnSendError(errorToReport, entry);
            }
            catch (Exception callbackEx)
            {
                _logger.LogError(callbackEx, "ApiLogger: exception thrown inside OnSendError callback.");
            }
        }
    }
}
