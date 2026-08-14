namespace ResearchAssistant.Web.Services;

/// <summary>单条内存日志。</summary>
public sealed record LogEntry(DateTimeOffset Timestamp, LogLevel Level, string Category, string Message, string? Exception);

/// <summary>
/// 内存日志环形缓冲：容量固定，写满挤掉最旧。进程重启即清空——仅限本地演示；
/// 需要持久化时换 Serilog，本存储的查询接口不变。
/// </summary>
public sealed class InMemoryLogStore
{
    private const int Capacity = 1000;
    private readonly object _gate = new();
    private readonly Queue<LogEntry> _entries = new(Capacity);

    public void Add(LogEntry entry)
    {
        lock (_gate)
        {
            if (_entries.Count >= Capacity)
            {
                _entries.Dequeue();
            }
            _entries.Enqueue(entry);
        }
    }

    /// <summary>按最低级别过滤，最新在前，最多 take 条。</summary>
    public IReadOnlyList<LogEntry> Query(LogLevel minLevel, int take)
    {
        lock (_gate)
        {
            return _entries.Where(e => e.Level >= minLevel).Reverse().Take(take).ToList();
        }
    }
}

/// <summary>把框架日志同时写入内存缓冲的 Provider（控制台等原有输出不受影响）。</summary>
public sealed class InMemoryLoggerProvider(InMemoryLogStore store) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new InMemoryLogger(categoryName, store);

    public void Dispose()
    {
    }

    private sealed class InMemoryLogger(string category, InMemoryLogStore store) : ILogger
    {
        // 不实现作用域串联（KISS）：日志条目只看级别/分类/消息
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }
            store.Add(new LogEntry(DateTimeOffset.Now, logLevel, category, formatter(state, exception), exception?.ToString()));
        }
    }
}
