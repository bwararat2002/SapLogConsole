using System.Threading.Channels;
using ApiLogger.Core.Models;
using ApiLogger.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApiLogger.Core.Services;

/// <summary>
/// คิวในหน่วยความจำสำหรับเก็บ LogEntry ที่รอส่งไปยัง CreateLog API
/// การ Enqueue เป็นแบบ non-blocking (fire-and-forget) เพื่อไม่ให้กระทบ performance ของ request หลัก
/// </summary>
public interface ILogQueue
{
    /// <summary>เพิ่ม log เข้าคิว (ไม่ block, ไม่ throw)</summary>
    void Enqueue(LogEntry entry);

    /// <summary>อ่าน log ออกจากคิวแบบ async (ใช้โดย background service เท่านั้น)</summary>
    IAsyncEnumerable<LogEntry> ReadAllAsync(CancellationToken cancellationToken);
}

public class LogQueue : ILogQueue
{
    private readonly Channel<LogEntry> _channel;
    private readonly ILogger<LogQueue> _logger;

    public LogQueue(IOptions<ApiLoggerOptions> options, ILogger<LogQueue> logger)
    {
        _logger = logger;

        // BoundedChannel: ถ้าคิวเต็ม ให้ "ทิ้ง log เก่าสุด" (DropOldest)
        // เพื่อรับประกันว่า Enqueue() จะไม่ block thread ของ request ที่กำลังทำงานอยู่
        _channel = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(options.Value.QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public void Enqueue(LogEntry entry)
    {
        // TryWrite เป็น non-blocking เสมอ
        if (!_channel.Writer.TryWrite(entry))
        {
            _logger.LogWarning("ApiLogger queue is full, log entry dropped. Command={Command}", entry.Command);
        }
    }

    public IAsyncEnumerable<LogEntry> ReadAllAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}
