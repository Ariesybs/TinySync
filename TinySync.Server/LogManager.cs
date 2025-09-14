namespace TinySync.Server;
using NLog;
using NLog.Config;
using NLog.Targets;

public static class LogManager
{
    private static bool m_Initialized = false;

    public static void Initialize()
    {
        if(m_Initialized)return;
        m_Initialized = true;

        var config = new LoggingConfiguration();
        
        // 控制台输出
        var consoleLog = new ConsoleTarget("console_log");
        
        // 颜色控制台输出
        var colorConsoleLog = new ColoredConsoleTarget("color_console_log")
        {
            Layout = "${longdate} [${level}] ${message}",
            UseDefaultRowHighlightingRules = false,
        };
        colorConsoleLog.RowHighlightingRules.Add(
            new ConsoleRowHighlightingRule(
                condition: "level == LogLevel.Debug",
                foregroundColor: ConsoleOutputColor.Yellow,
                backgroundColor: ConsoleOutputColor.Black
                ));
        
        // Info → 蓝
        colorConsoleLog.RowHighlightingRules.Add(
            new ConsoleRowHighlightingRule(
                condition: "level == LogLevel.Info",
                foregroundColor: ConsoleOutputColor.Blue,
                backgroundColor: ConsoleOutputColor.Black));
        
        // 普通日志输出
        var fileLog = new FileTarget("file_info_log")
        {
            FileName = "logs/${shortdate}/info.log",
            Layout = "${longdate} [${level:uppercase=true}] ${logger}: ${message} ${exception:format=tostring}",
            ArchiveFileName = "logs/${shortdate}/archive/info-{#}.log",
            ArchiveEvery = FileArchivePeriod.Day,
            MaxArchiveFiles = 30
        };
        
        // 错误日志输出
        var errorLog = new FileTarget("file_error_log")
        {
            FileName = "logs/${shortdate}/error.log",
            Layout = "${longdate} [${level:uppercase=true}] ${logger}: ${message} ${exception:format=tostring}",
            ArchiveFileName = "logs/${shortdate}/archive/error-{#}.log",
            ArchiveEvery = FileArchivePeriod.Day,
            MaxArchiveFiles = 30
        };
        
        config.AddRule(LogLevel.Debug, LogLevel.Fatal, colorConsoleLog);
        config.AddRule(LogLevel.Info, LogLevel.Fatal, fileLog);
        config.AddRule(LogLevel.Error, LogLevel.Fatal, errorLog);
        NLog.LogManager.Configuration = config;
    }

    public static Logger GetLogger(string? name = "Unknown")
    {
        return NLog.LogManager.GetLogger(name ?? "Unknown");
    }

    public static void Shutdown()
    {
        if (!m_Initialized) return;
        NLog.LogManager.Shutdown();
        m_Initialized = false;
    }
}