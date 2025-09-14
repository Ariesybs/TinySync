
using NLog;

namespace TinySync.Server
{
    public static class Program
    {
        private static readonly Logger Log = LogManager.GetLogger("TinySync.Server");
        private static readonly CancellationTokenSource m_CancellationTokenSource = new();
        public static async Task Main()
        {
            try
            {
                // 注册退出事件
                RegisterExitHandlers();
                // 初始化日志
                LogManager.Initialize();
                // 初始化网络连接
                var tasks = new List<Task>()
                {
                    NetworkManager.StartAsync(m_CancellationTokenSource)
                };
                // 初始化房间管理器
                RoomManager.Initialize();
                await Task.WhenAll(tasks);
            }
            catch (Exception e)
            {
                LogManager.Shutdown();
            }
        }
        

        private static void RegisterExitHandlers()
        {
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                ShutdownServer();
            };
            AppDomain.CurrentDomain.ProcessExit += (sender, eventArgs) =>
            {
                 Log.Info("Process exit, shutting down server...");
                 ShutdownServer();
            };

            AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
            {
                Log.Fatal(eventArgs.ExceptionObject as Exception, "Unhandled exception occurred");
                ShutdownServer();
            };
        }
        

        private static void ShutdownServer()
        {
            m_CancellationTokenSource.Cancel();
            Log.Info("Server shutdown");
        }
    }
}