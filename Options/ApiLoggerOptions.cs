using ApiLogger.Core.Models;

namespace ApiLogger.Core.Options;

/// <summary>
/// Configuration ของ ApiLogger ที่จะต้องตั้งค่าใน appsettings.json ของแต่ละ API
/// ที่มาเรียกใช้ package นี้ (SourceSystem / ProgramCode เป็นค่าประจำตัวของ API นั้น ๆ)
/// </summary>
public class ApiLoggerOptions
{
    public const string SectionName = "ApiLogger";

    /// <summary>ชื่อระบบต้นทาง (เช่น "ITM", "WMS") — บ่งบอกว่า log นี้มาจาก API ตัวไหน</summary>
    public string SourceSystem { get; set; } = string.Empty;

    /// <summary>รหัสโปรแกรม/โมดูล (เช่น "WB2025-012")</summary>
    public string ProgramCode { get; set; } = string.Empty;

    /// <summary>URL แบบเต็มของ Log API เช่น http://xxxx/CreateLog</summary>
    public string CreateLogUrl { get; set; } = string.Empty;

    /// <summary>ค่า header "apiKey" สำหรับยิงไปที่ Log API</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>ผู้สร้าง log ถ้าไม่มี user login จะใช้ค่านี้แทน (default: SourceSystem)</summary>
    public string? DefaultCreatedBy { get; set; }

    /// <summary>
    /// ค่า transactionType เริ่มต้น ถ้า request ไม่ได้ส่ง header X-Transaction-Type มา
    /// และไม่ match กับ PathTransactionTypeMap เช่น "General" — ถ้าไม่ตั้งค่าจะเป็น null
    /// </summary>
    public string? DefaultTransactionType { get; set; }

    /// <summary>
    /// Map จาก path prefix → transactionType แบบ SAP เช่น PR, PO, GR, GI, Billing
    /// ลำดับความสำคัญ: header X-Transaction-Type > PathTransactionTypeMap > DefaultTransactionType
    /// <para>
    /// ตัวอย่างใน appsettings.json:
    /// <code>
    /// "PathTransactionTypeMap": {
    ///   "/purchase-requisition": "PR",
    ///   "/purchase-order":       "PO",
    ///   "/goods-receipt":        "GR",
    ///   "/goods-issue":          "GI",
    ///   "/billing":              "Billing"
    /// }
    /// </code>
    /// </para>
    /// </summary>
    public Dictionary<string, string> PathTransactionTypeMap { get; set; } = [];

    /// <summary>
    /// Resolve transactionType จาก path โดยใช้ PathTransactionTypeMap
    /// (เปรียบเทียบแบบ StartsWith, case-insensitive)
    /// </summary>
    internal string? ResolveTransactionType(string? headerValue, string? path)
    {
        if (!string.IsNullOrWhiteSpace(headerValue))
            return headerValue;

        if (!string.IsNullOrWhiteSpace(path) && PathTransactionTypeMap.Count > 0)
        {
            foreach (var (prefix, txType) in PathTransactionTypeMap)
            {
                if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return txType;
            }
        }

        return DefaultTransactionType;
    }

    /// <summary>เปิด/ปิดการ log request ที่ "เข้ามา" ที่ API นี้ (Middleware)</summary>
    public bool EnableIncomingLogging { get; set; } = true;

    /// <summary>เปิด/ปิดการ log request ที่ API นี้ "ยิงออกไป" หา API อื่น (DelegatingHandler)</summary>
    public bool EnableOutgoingLogging { get; set; } = true;

    /// <summary>path ที่ไม่ต้อง log (เช่น health check, swagger)</summary>
    public string[] ExcludePaths { get; set; } = ["/health", "/swagger", "/favicon.ico"];

    /// <summary>จำกัดความยาวสูงสุดของ requestBody/responseBody ที่จะเก็บ (ตัวอักษร) ป้องกัน body ใหญ่เกิน</summary>
    public int MaxBodyLength { get; set; } = 8000;

    /// <summary>ขนาดคิวสูงสุดของ log ที่รอส่ง (ถ้าคิวเต็มจะ drop log เก่าสุดทิ้งเพื่อไม่ให้แอปช้า/หน่วงความจำ)</summary>
    public int QueueCapacity { get; set; } = 2000;

    /// <summary>จำนวนครั้งที่จะ retry ถ้าส่ง log ไปยัง CreateLog API ไม่สำเร็จ</summary>
    public int MaxRetryCount { get; set; } = 3;

    /// <summary>หน่วงเวลา (ms) ก่อน retry แต่ละครั้ง</summary>
    public int RetryDelayMilliseconds { get; set; } = 2000;

    /// <summary>
    /// Callback ที่จะถูกเรียกเมื่อส่ง log ไปยัง CreateLog API ไม่สำเร็จหลัง retry ครบทุกครั้ง
    /// หรือเมื่อ API ตอบกลับมาด้วย non-success status code
    /// <para>
    /// ตัวอย่าง:
    /// <code>
    /// OnSendError = (ex, entry) =>
    /// {
    ///     // ex จะเป็น null ถ้า API ตอบกลับ non-success (ใช้ LastStatusCode แทน)
    ///     Console.Error.WriteLine($"[ApiLogger] ส่ง log ไม่สำเร็จ: {ex?.Message ?? "non-success response"} | Command={entry.Command}");
    /// }
    /// </code>
    /// </para>
    /// </summary>
    public Action<Exception?, LogEntry>? OnSendError { get; set; }
}
