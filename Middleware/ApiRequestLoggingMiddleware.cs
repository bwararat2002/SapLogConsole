using System.Text;
using ApiLogger.Core.Models;
using ApiLogger.Core.Options;
using ApiLogger.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApiLogger.Core.Middleware;

/// <summary>
/// Middleware ที่ดักทุก request ที่ "ผู้ใช้/ระบบอื่นยิงเข้ามา" ที่ API นี้
/// แล้วส่ง log เข้าคิวแบบ background (ไม่ block response กลับไปยัง client)
///
/// ใส่ header เสริมเพื่อให้ log มีรายละเอียดมากขึ้น (ไม่บังคับ):
///   X-Transaction-Type : เช่น GoodsReceipt
///   X-Reference-DocNo  : เลขที่เอกสารอ้างอิง
///   X-Reference-ItemNo : เลขที่ item อ้างอิง
///   X-Flow-No          : เลข flow
///   X-Flow-Key         : key ของ flow
/// </summary>
public class ApiRequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogQueue _logQueue;
    private readonly ApiLoggerOptions _options;
    private readonly ILogger<ApiRequestLoggingMiddleware> _logger;

    public ApiRequestLoggingMiddleware(
        RequestDelegate next,
        ILogQueue logQueue,
        IOptions<ApiLoggerOptions> options,
        ILogger<ApiRequestLoggingMiddleware> logger)
    {
        _next = next;
        _logQueue = logQueue;
        _options = options.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.EnableIncomingLogging || ShouldSkip(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var createdAt = DateTime.UtcNow;

        // 1) อ่าน request body (ต้อง EnableBuffering เพื่อให้ controller อ่านได้ตามปกติด้วย)
        context.Request.EnableBuffering();
        var requestBody = await ReadRequestBodyAsync(context.Request);
        context.Request.Body.Position = 0;

        // 2) สลับ response body ไปเป็น MemoryStream เพื่อแอบอ่าน โดยยังคง stream เดิมไว้ส่งกลับ client
        var originalResponseBody = context.Response.Body;
        using var responseBuffer = new MemoryStream();
        context.Response.Body = responseBuffer;

        string? errorMessage = null;

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            throw; // ส่งต่อให้ exception handler middleware ตัวอื่นจัดการตามปกติ
        }
        finally
        {
            var completedAt = DateTime.UtcNow;

            // คัดลอก response ที่ capture ไว้กลับไปยัง stream จริง เพื่อให้ client ได้รับ response ตามปกติ
            responseBuffer.Seek(0, SeekOrigin.Begin);
            var responseBodyText = await new StreamReader(responseBuffer).ReadToEndAsync();
            responseBuffer.Seek(0, SeekOrigin.Begin);
            await responseBuffer.CopyToAsync(originalResponseBody);
            context.Response.Body = originalResponseBody;

            var statusCode = context.Response.StatusCode;
            var status = errorMessage != null ? "SystemFailed" : LogStatusMapper.ToLogStatus(statusCode);

            var entry = new LogEntry
            {
                SourceSystem = _options.SourceSystem,
                ProgramCode = _options.ProgramCode,
                TransactionType = _options.ResolveTransactionType(
                    context.Request.Headers["X-Transaction-Type"].FirstOrDefault(),
                    context.Request.Path.Value),
                ReferenceDocNo = context.Request.Headers["X-Reference-DocNo"].FirstOrDefault(),
                ReferenceItemNo = context.Request.Headers["X-Reference-ItemNo"].FirstOrDefault(),
                Status = status,
                ErrorCode = errorMessage != null ? "EXCEPTION" : LogStatusMapper.ToErrorCode(statusCode),
                ErrorMessage = errorMessage ?? (status != "Success"
                    ? LogStatusMapper.Truncate(responseBodyText, _options.MaxBodyLength)
                    : null),
                CreatedBy = context.User?.Identity?.Name ?? _options.DefaultCreatedBy ?? _options.SourceSystem,
                CreatedAt = createdAt,
                UpdatedAt = completedAt,
                CompletedAt = completedAt,
                Command = context.Request.Path.Value,
                RequestBody = LogStatusMapper.Truncate(requestBody, _options.MaxBodyLength),
                ResponseBody = LogStatusMapper.Truncate(responseBodyText, _options.MaxBodyLength),
                FlowNo = TryParseInt(context.Request.Headers["X-Flow-No"].FirstOrDefault()),
                FlowKey = context.Request.Headers["X-Flow-Key"].FirstOrDefault(),
                HttpMethod = context.Request.Method,
                Action = "Incoming"
            };

            // enqueue แบบ non-blocking, ไม่กระทบ response ที่กำลังจะส่งกลับ client
            _logQueue.Enqueue(entry);
        }
    }

    private bool ShouldSkip(PathString path)
    {
        var value = path.Value ?? string.Empty;
        return _options.ExcludePaths.Any(p => value.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        if (request.ContentLength is null or 0)
        {
            return string.Empty;
        }

        using var reader = new StreamReader(
            request.Body,
            encoding: Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 4096,
            leaveOpen: true);

        return await reader.ReadToEndAsync();
    }

    private static int? TryParseInt(string? value)
        => int.TryParse(value, out var result) ? result : null;
}
