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

    /// <summary>URL แบบเต็มของ Log API เช่น http://192.168.0.100/ApiItManagement/api/Log/CreateLog</summary>
    public string CreateLogUrl { get; set; } = string.Empty;

    /// <summary>ค่า header "apiKey" สำหรับยิงไปที่ Log API</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>ผู้สร้าง log ถ้าไม่มี user login จะใช้ค่านี้แทน (default: SourceSystem)</summary>
    public string? DefaultCreatedBy { get; set; }

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
}
