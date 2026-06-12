using System.Text.Json.Serialization;

namespace ApiLogger.Core.Models;

/// <summary>
/// โครงสร้างข้อมูล Log ตรงกับ payload ของ API: POST /api/Log/CreateLog
/// </summary>
public class LogEntry
{
    [JsonPropertyName("id")]
    public int Id { get; set; } = 0;

    [JsonPropertyName("idempotencyKey")]
    public string IdempotencyKey { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("transactionType")]
    public string? TransactionType { get; set; }

    /// <summary>ดึงค่าจาก ApiLoggerOptions ของ API ที่เรียกใช้ package (ห้าม hardcode ใน package)</summary>
    [JsonPropertyName("sourceSystem")]
    public string SourceSystem { get; set; } = string.Empty;

    /// <summary>ดึงค่าจาก ApiLoggerOptions ของ API ที่เรียกใช้ package (ห้าม hardcode ใน package)</summary>
    [JsonPropertyName("programCode")]
    public string ProgramCode { get; set; } = string.Empty;

    [JsonPropertyName("referenceDocNo")]
    public string? ReferenceDocNo { get; set; }

    [JsonPropertyName("referenceItemNo")]
    public string? ReferenceItemNo { get; set; }

    [JsonPropertyName("sapDocumentNo")]
    public string? SapDocumentNo { get; set; }

    [JsonPropertyName("sapFiscalYear")]
    public string? SapFiscalYear { get; set; }

    /// <summary>Processing / Success / BusinessFailed / SystemFailed (คำนวณอัตโนมัติจาก HTTP status code)</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "Processing";

    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("createdBy")]
    public string? CreatedBy { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updatedAt")]
    public DateTime? UpdatedAt { get; set; }

    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }

    [JsonPropertyName("retryCount")]
    public int RetryCount { get; set; } = 0;

    [JsonPropertyName("archiveFlag")]
    public bool ArchiveFlag { get; set; } = false;

    /// <summary>ชื่อ action/command ที่เรียก เช่น ชื่อ endpoint หรือชื่อ method ที่ยิงออก</summary>
    [JsonPropertyName("command")]
    public string? Command { get; set; }

    [JsonPropertyName("requestBody")]
    public string? RequestBody { get; set; }

    [JsonPropertyName("responseBody")]
    public string? ResponseBody { get; set; }

    [JsonPropertyName("flowNo")]
    public int? FlowNo { get; set; }

    [JsonPropertyName("flowKey")]
    public string? FlowKey { get; set; }

    [JsonPropertyName("httpMethod")]
    public string? HttpMethod { get; set; }

    /// <summary>"Incoming" (มีคนยิงเข้ามาที่ API นี้) หรือ "Outgoing" (API นี้ยิงไป API อื่น)</summary>
    [JsonPropertyName("action")]
    public string? Action { get; set; }
}
