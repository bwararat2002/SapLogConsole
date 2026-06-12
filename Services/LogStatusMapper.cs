using System.Net;

namespace ApiLogger.Core.Services;

/// <summary>
/// Helper functions ที่ใช้ร่วมกันระหว่าง Middleware (Incoming) และ DelegatingHandler (Outgoing)
/// </summary>
public static class LogStatusMapper
{
    /// <summary>
    /// แปลง HTTP status code -> สถานะของ log
    /// 2xx          => Success
    /// 4xx          => BusinessFailed
    /// 5xx / อื่นๆ   => SystemFailed
    /// </summary>
    public static string ToLogStatus(int httpStatusCode)
    {
        return httpStatusCode switch
        {
            >= 200 and < 300 => "Success",
            >= 400 and < 500 => "BusinessFailed",
            >= 500 => "SystemFailed",
            _ => "Processing"
        };
    }

    public static string? ToErrorCode(int httpStatusCode)
        => httpStatusCode >= 400 ? httpStatusCode.ToString() : null;

    /// <summary>ตัด body ให้ไม่เกินความยาวที่กำหนด เพื่อไม่ให้ log payload ใหญ่เกินไป</summary>
    public static string? Truncate(string? content, int maxLength)
    {
        if (string.IsNullOrEmpty(content) || content.Length <= maxLength)
        {
            return content;
        }

        return content[..maxLength] + "...(truncated)";
    }

    public static string? ReasonPhrase(int httpStatusCode)
    {
        try
        {
            return ((HttpStatusCode)httpStatusCode).ToString();
        }
        catch
        {
            return null;
        }
    }
}
