using LiteNetLib;
using Newtonsoft.Json.Serialization;
using NLog;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace TinySync.Server;

public static class NetworkManager
{
    private static readonly Logger Log = LogManager.GetLogger("TinySync.NetworkManager");
    private const string ConnectionKey = "TinySyncServer"; //连接密钥，用于简单鉴权（LiteNetLib 的 Connect key）
    
    // Http Config
    private const int HttpPort = 5000; // HTTP服务器端口
    
    // Udp Config
    private const int UpdPort = 9050; // UDP 端口
    private static EventBasedNetListener? m_Listener;
    public static event EventBasedNetListener.OnPeerConnected? OnPeerConnectedEvent;
    public static event EventBasedNetListener.OnNetworkReceive? OnPeerReceiveEvent;
    public static event EventBasedNetListener.OnPeerDisconnected? OnPeerDisconnectedEvent;

    public static async Task StartAsync(CancellationTokenSource cts)
    {
        await InitializeUdpServer(cts);
        await InitializeHttpServer(cts);
        Log.Info("TinySync Server started!");
    }


    private static async Task InitializeHttpServer(CancellationTokenSource cts)
    {
        try
        {
            // 启动HTTP服务器
            var builder = WebApplication.CreateBuilder();

            // 配置服务
            builder.Services.AddControllers().AddNewtonsoftJson(options =>
            {
                // 保留 JsonProperty 里写的 snake_case 名称
                options.SerializerSettings.ContractResolver =
                    new DefaultContractResolver();
            });
            builder.Services.AddEndpointsApiExplorer();

            // 配置HTTP日志
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();
            builder.Logging.SetMinimumLevel(LogLevel.Warning); // 只看 Warning 及以上

            var app = builder.Build();
            app.UseRouting();
            app.MapControllers();


            Log.Info($"HTTP: http://localhost:{HttpPort}");
            // 启动HTTP服务器
            await app.RunAsync($"http://localhost:{HttpPort}");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private static async Task InitializeUdpServer(CancellationTokenSource cts)
    {
        //初始化 LiteNetLib 服务器
        m_Listener = new EventBasedNetListener();
        var net = new NetManager(m_Listener)
        {
            AutoRecycle = true, // 自动回收数据缓冲，减少 GC 负担
        };
        net.Start(UpdPort);

        // Console.WriteLine($"TinySync Server started on port {Port}");
        Log.Info($"UPD: http://localhost:{UpdPort}");

        // 连接鉴权：只有提供正确 key 的连接才会被接受
        m_Listener.ConnectionRequestEvent += req=> req.AcceptIfKey(ConnectionKey);
        m_Listener.PeerConnectedEvent += OnPeerConnectedEvent;
        m_Listener.NetworkReceiveEvent += OnPeerReceiveEvent;
        m_Listener.PeerDisconnectedEvent += OnPeerDisconnectedEvent;

        _ = Task.Run(async () =>
        {
            try
            {
                while (net.IsRunning && !cts.IsCancellationRequested)
                {
                    net.PollEvents();
                    await Task.Delay(1, cts.Token);
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "UDP Server loop error");
            }
        });
        
        await Task.CompletedTask;
    }

    public static int GetHttpPort()
    {
        return HttpPort;
    }

    public static int GetUpdPort()
    {
        return UpdPort;
    }
    
}