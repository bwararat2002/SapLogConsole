using ApiLogger.Core.Models;
using ApiLogger.Core.Options;
using ApiLogger.Core.Services;
using Microsoft.Extensions.Options;

namespace ApiLogger.Core.Handlers;

/// <summary>
/// DelegatingHandler สำหรับ "ฝั่งที่ API นี้ยิงออกไปหา API อื่น"
/// ใส่ handler ตัวนี้เข้ากับ HttpClient ที่ใช้เรียก API ปลายทาง แล้วทุก request/response
/// จะถูก log เข้าคิวแบบ background โดยอัตโนมัติ
///
/// วิธีใช้ (Program.cs):
///   builder.Services.AddHttpClient("SapApi")
///          .AddHttpMessageHandler<OutgoingApiLoggingHandler>();
///
/// สามารถใส่ header เสริมที่ request ขาออกเพื่อให้ log มีรายละเอียดมากขึ้น (ไม่บังคับ):
///   X-Transaction-Type, X-Reference-DocNo, X-Reference-ItemNo, X-Flow-No, X-Flow-Key
/// </summary>
public class OutgoingApiLoggingHandler : DelegatingHandler
{
    private readonly ILogQueue _logQueue;
    private readonly ApiLoggerOptions _options;

    public OutgoingApiLoggingHandler(ILogQueue logQueue, IOptions<ApiLoggerOptions> options)
    {
        _logQueue = logQueue;
        _options = options.Value;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!_options.EnableOutgoingLogging)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        var createdAt = DateTime.UtcNow;

        var requestBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        string? errorMessage = null;
        HttpResponseMessage? response = null;
        string? responseBody = null;
        var statusCode = 0;

        try
        {
            response = await base.SendAsync(request, cancellationToken);
            statusCode = (int)response.StatusCode;
            responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // เช่น timeout / connection refused / DNS error
            errorMessage = ex.Message;
            statusCode = (int)System.Net.HttpStatusCode.ServiceUnavailable; // ใช้ map -> SystemFailed
            throw; // ส่งต่อ exception ให้ caller จัดการตามปกติ
        }
        finally
        {
            var completedAt = DateTime.UtcNow;
            var status = errorMessage != null ? "SystemFailed" : LogStatusMapper.ToLogStatus(statusCode);

            var entry = new LogEntry
            {
                SourceSystem = _options.SourceSystem,
                ProgramCode = _options.ProgramCode,
                TransactionType = _options.ResolveTransactionType(
                    GetHeader(request, "X-Transaction-Type"),
                    request.RequestUri?.AbsolutePath),
                ReferenceDocNo = GetHeader(request, "X-Reference-DocNo"),
                ReferenceItemNo = GetHeader(request, "X-Reference-ItemNo"),
                Status = status,
                ErrorCode = errorMessage != null ? "EXCEPTION" : LogStatusMapper.ToErrorCode(statusCode),
                ErrorMessage = errorMessage ?? (status != "Success"
                    ? LogStatusMapper.Truncate(responseBody, _options.MaxBodyLength)
                    : null),
                CreatedBy = _options.DefaultCreatedBy ?? _options.SourceSystem,
                CreatedAt = createdAt,
                UpdatedAt = completedAt,
                CompletedAt = completedAt,
                Command = request.RequestUri?.ToString(),
                RequestBody = LogStatusMapper.Truncate(requestBody, _options.MaxBodyLength),
                ResponseBody = LogStatusMapper.Truncate(responseBody, _options.MaxBodyLength),
                FlowNo = TryParseInt(GetHeader(request, "X-Flow-No")),
                FlowKey = GetHeader(request, "X-Flow-Key"),
                HttpMethod = request.Method.Method,
                Action = "Outgoing"
            };

            _logQueue.Enqueue(entry);
        }

        return response!;
    }

    private static string? GetHeader(HttpRequestMessage request, string name)
        => request.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;

    private static int? TryParseInt(string? value)
        => int.TryParse(value, out var result) ? result : null;
}
