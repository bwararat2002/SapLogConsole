using ApiLogger.Core.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ApiLogger.Core.BackgroundServices;

/// <summary>
/// BackgroundService ที่คอยอ่าน LogEntry จากคิว แล้วส่งไปยัง CreateLog API
/// ทำงานแยก thread จาก request pipeline หลัก ทำให้ไม่กระทบ performance ของ API
/// </summary>
public class LogDispatcherService : BackgroundService
{
    private readonly ILogQueue _queue;
    private readonly ILogSender _sender;
    private readonly ILogger<LogDispatcherService> _logger;

    public LogDispatcherService(ILogQueue queue, ILogSender sender, ILogger<LogDispatcherService> logger)
    {
        _queue = queue;
        _sender = sender;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ApiLogger LogDispatcherService started.");

        try
        {
            await foreach (var entry in _queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await _sender.SendAsync(entry, stoppingToken);
                }
                catch (Exception ex)
                {
                    // ป้องกัน background service หยุดทำงานทั้งหมดเพราะ log เดียวพัง
                    _logger.LogError(ex, "ApiLogger: unexpected error while dispatching log entry.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // ปกติเวลา shutdown
        }

        _logger.LogInformation("ApiLogger LogDispatcherService stopped.");
    }
}
