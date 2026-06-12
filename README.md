# ApiLogger.Core

Package สำหรับเก็บ Log API แบบ background (ไม่กระทบ performance ของ request หลัก)
รองรับ 2 รูปแบบ:

1. **Incoming** — มีคนยิงเข้ามาที่ API ของเรา (ผ่าน Middleware)
2. **Outgoing** — API ของเรายิงไปหา API อื่น เช่น SAP (ผ่าน HttpClient DelegatingHandler)

ทุก log จะถูกส่งไปที่ปลายทาง `POST /api/Log/CreateLog` ตาม payload ที่กำหนด
(ดู `Models/LogEntry.cs`)

`SourceSystem` และ `ProgramCode` จะถูกอ่านจาก **appsettings.json ของ API ที่ใช้ package นี้**
package เองไม่ hardcode ค่าเหล่านี้

---

## 1. ติดตั้ง

อ้างอิง project (หรือแพ็คเป็น .nupkg แล้ว `dotnet add package ApiLogger.Core`):

```bash
dotnet add reference ../ApiLogger.Core/ApiLogger.Core.csproj
```

---

## 2. ตั้งค่า appsettings.json

ใส่ section `ApiLogger` ใน API ที่จะใช้งาน (ค่าพวกนี้จะถูกใส่ลงใน field
`sourceSystem` / `programCode` ของทุก log ที่ส่งออกจาก API ตัวนี้):

```json
{
  "ApiLogger": {
    "SourceSystem": "ITM",
    "ProgramCode": "WB2025-012",
    "CreateLogUrl": "http://192.168.0.100/ApiItManagement/api/Log/CreateLog",
    "ApiKey": "CCP!weare_1Q&qazP@ssw0rd",
    "DefaultCreatedBy": "ITM-Service",
    "EnableIncomingLogging": true,
    "EnableOutgoingLogging": true,
    "ExcludePaths": [ "/health", "/swagger" ],
    "MaxBodyLength": 8000,
    "QueueCapacity": 2000,
    "MaxRetryCount": 3,
    "RetryDelayMilliseconds": 2000
  }
}
```

> ทุก API ที่อ้างอิง package นี้ ใส่ค่า `SourceSystem` / `ProgramCode` ของตัวเอง
> ตามชื่อ/รหัสของ API นั้น ๆ ได้เลย

---

## 3. ลงทะเบียนใน Program.cs

```csharp
using ApiLogger.Core.Extensions;

var builder = WebApplication.CreateBuilder(args);

// 1) ลงทะเบียน ApiLogger (อ่าน config จาก section "ApiLogger")
builder.Services.AddApiLogger(builder.Configuration);

// 2) (ออปชัน) ตั้งค่า HttpClient สำหรับยิงไป API อื่น พร้อม log ขาออกอัตโนมัติ
builder.Services.AddHttpClient("SapApi", client =>
{
    client.BaseAddress = new Uri("http://sap-host/sap-api/");
})
.AddApiLoggingHandler();   // <-- log request/response ของ HttpClient ตัวนี้อัตโนมัติ

builder.Services.AddControllers();

var app = builder.Build();

// 3) เปิดใช้ middleware สำหรับ log request ที่ "เข้ามา" (วางไว้ต้น ๆ pipeline)
app.UseApiLogger();

app.MapControllers();
app.Run();
```

---

## 4. การทำงาน

### 4.1 Incoming (ผู้ใช้/ระบบอื่นยิงเข้ามาที่ API นี้)

`ApiRequestLoggingMiddleware` จะ:

- อ่าน request body (ไม่กระทบ controller เพราะใช้ `EnableBuffering`)
- อ่าน response body ของ controller (โดยยังคง response เดิมส่งกลับ client ตามปกติ)
- คำนวณ `status` จาก HTTP status code ของ response:
  - `2xx` → `Success`
  - `4xx` → `BusinessFailed`
  - `5xx` หรือเกิด Exception → `SystemFailed`
- `action = "Incoming"`, `httpMethod` = HTTP method จริง, `command` = path ของ request
- **ไม่ block** response — log ถูก `Enqueue` เข้าคิวแล้วส่งโดย background service แยกต่างหาก

### 4.2 Outgoing (API นี้ยิงไปหา API อื่น)

`OutgoingApiLoggingHandler` (attach ผ่าน `.AddApiLoggingHandler()`) จะ:

- จับ request/response ของ `HttpClient` ที่แนบ handler นี้ไว้
- คำนวณ `status` จาก HTTP status code ของ response ปลายทาง (เหมือนข้อ 4.1)
- ถ้า connection error / timeout / exception → `status = SystemFailed`
- `action = "Outgoing"`, `command` = URL ปลายทางที่ยิงไป

### 4.3 Header เสริม (ใส่ได้ทั้ง Incoming request และ Outgoing HttpRequestMessage)

ใส่ header เหล่านี้เพื่อให้ log มีข้อมูลครบตาม payload ของ CreateLog:

| Header               | Map ไปยัง field   |
|----------------------|-------------------|
| `X-Transaction-Type`| `transactionType` |
| `X-Reference-DocNo` | `referenceDocNo`  |
| `X-Reference-ItemNo`| `referenceItemNo` |
| `X-Flow-No`         | `flowNo`          |
| `X-Flow-Key`        | `flowKey`         |

ตัวอย่างการเรียก API ของเรา (ฝั่ง client):

```bash
curl -X POST 'https://your-api/goods-receipt' \
  -H 'X-Transaction-Type: GoodsReceipt' \
  -H 'X-Reference-DocNo: GR202606120001' \
  -H 'X-Reference-ItemNo: 10' \
  -H 'X-Flow-No: 2001' \
  -H 'X-Flow-Key: FLOW-GR-20260612-0001' \
  -H 'Content-Type: application/json' \
  -d '{ "materialCode": "MAT001", "quantity": 100, "unit": "PCS" }'
```

ตัวอย่างการยิงออกจากฝั่งเรา (Outgoing):

```csharp
public class GoodsReceiptService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public GoodsReceiptService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<HttpResponseMessage> CallSapAsync(object payload)
    {
        var client = _httpClientFactory.CreateClient("SapApi");

        using var request = new HttpRequestMessage(HttpMethod.Post, "goods-receipt")
        {
            Content = JsonContent.Create(payload)
        };

        request.Headers.Add("X-Transaction-Type", "GoodsReceipt");
        request.Headers.Add("X-Reference-DocNo", "GR202606120001");
        request.Headers.Add("X-Reference-ItemNo", "10");
        request.Headers.Add("X-Flow-No", "2001");
        request.Headers.Add("X-Flow-Key", "FLOW-GR-20260612-0001");

        // request/response นี้จะถูก log อัตโนมัติโดย OutgoingApiLoggingHandler
        return await client.SendAsync(request);
    }
}
```

---

## 5. การันตี Performance

- ทุกการ log ใช้ `Channel<LogEntry>` (in-memory queue) + `BackgroundService`
  → การ `Enqueue` เป็น non-blocking, ไม่รอผลการยิงไป CreateLog API
- ถ้าคิวเต็ม (`QueueCapacity`) จะ "ทิ้ง log เก่าสุด" (DropOldest) เพื่อไม่ให้แอป
  ใช้หน่วยความจำบวมหรือ block
- ถ้า CreateLog API ล่ม/ช้า จะ retry ตาม `MaxRetryCount` / `RetryDelayMilliseconds`
  แต่ทำใน background thread เท่านั้น ไม่กระทบ response ของ request จริง
- `MaxBodyLength` ช่วยตัด request/response body ที่ใหญ่เกินไปก่อนส่ง log

---

## 6. รองรับ .NET เวอร์ชัน

`net8.0`, `net9.0`, `net10.0` (multi-target ใน `.csproj`)
