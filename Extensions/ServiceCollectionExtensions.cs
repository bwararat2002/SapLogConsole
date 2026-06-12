using ApiLogger.Core.BackgroundServices;
using ApiLogger.Core.Handlers;
using ApiLogger.Core.Middleware;
using ApiLogger.Core.Options;
using ApiLogger.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApiLogger.Core.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// ลงทะเบียน ApiLogger ทั้งหมด (queue, sender, background dispatcher, outgoing handler)
    /// อ่าน config จาก section "ApiLogger" ใน appsettings.json
    ///
    /// ตัวอย่าง appsettings.json:
    /// {
    ///   "ApiLogger": {
    ///     "SourceSystem": "ITM",
    ///     "ProgramCode": "WB2025-012",
    ///     "CreateLogUrl": "http://192.168.0.100/ApiItManagement/api/Log/CreateLog",
    ///     "ApiKey": "CCP!weare_1Q&qazP@ssw0rd"
    ///   }
    /// }
    /// </summary>
    public static IServiceCollection AddApiLogger(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ApiLoggerOptions>(configuration.GetSection(ApiLoggerOptions.SectionName));
        return services.AddApiLoggerCore();
    }

    /// <summary>ลงทะเบียน ApiLogger โดยกำหนดค่า options ด้วยโค้ดเอง (ทางเลือกแทนการอ่านจาก appsettings.json)</summary>
    public static IServiceCollection AddApiLogger(this IServiceCollection services, Action<ApiLoggerOptions> configureOptions)
    {
        services.Configure(configureOptions);
        return services.AddApiLoggerCore();
    }

    private static IServiceCollection AddApiLoggerCore(this IServiceCollection services)
    {
        services.AddSingleton<ILogQueue, LogQueue>();
        services.AddTransient<OutgoingApiLoggingHandler>();

        // HttpClient เฉพาะสำหรับยิงไปที่ CreateLog API
        services.AddHttpClient<ILogSender, LogSender>("ApiLogger.CreateLog");

        // BackgroundService ที่คอยส่ง log ออกจากคิว
        services.AddHostedService<LogDispatcherService>();

        return services;
    }

    /// <summary>
    /// เพิ่ม Middleware สำหรับ log request ที่ "เข้ามา" ที่ API นี้
    /// เรียกใน Program.cs ก่อน app.MapControllers() (แนะนำให้วางต้น ๆ ของ pipeline)
    /// </summary>
    public static IApplicationBuilder UseApiLogger(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ApiRequestLoggingMiddleware>();
    }

    /// <summary>
    /// เพิ่ม ApiLogger handler เข้ากับ HttpClient ที่ใช้ยิงไป API ภายนอก (เช่น SAP, ระบบอื่น)
    /// ทุก request/response ของ HttpClient ตัวนี้จะถูก log แบบ background โดยอัตโนมัติ
    ///
    /// ตัวอย่าง:
    ///   builder.Services.AddHttpClient("SapApi", c => c.BaseAddress = new Uri("http://sap-host/"))
    ///          .AddApiLoggingHandler();
    /// </summary>
    public static IHttpClientBuilder AddApiLoggingHandler(this IHttpClientBuilder builder)
    {
        return builder.AddHttpMessageHandler<OutgoingApiLoggingHandler>();
    }
}
