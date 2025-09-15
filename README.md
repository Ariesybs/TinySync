<p align="center">
  <img src="docs/TinySyncLogo.png" alt="TinySync Logo" width="720" />
</p>

## TinySync

轻量级、房间制的实时同步服务端。基于 .NET 8 与 LiteNetLib/ASP.NET Core，提供低延迟的 UDP 同步能力与简洁的 HTTP 控制接口，适合小游戏、原型验证、教学与小型多人实时互动场景。

### 演示
![TinySync 演示](docs/TinySyncDemo.gif)

### 特性
- **轻量与易部署**：.NET 单一进程即可运行。
- **房间/会话管理**：内置 `Room`、`RoomManager`，便于按房间隔离同步数据。
- **低延迟同步**：使用 LiteNetLib 进行 UDP 通讯（示例/骨架）。
- **HTTP 控制入口**：通过简单的 HTTP API 进行房间与会话操作。
- **高性能序列化**：使用 MemoryPack 进行高性能模型序列化。
- **日志**：集成 NLog，默认输出到 `logs/` 目录。

### 目录结构
```text
TinySync/
  TinySync.Core/
    Protocol/
      HttpModels.cs
      UpdModels.cs
    TinySync.Core.csproj

  TinySync.Server/
    HttpEndpoints.cs
    LogManager.cs
    NetworkManager.cs
    Program.cs
    Room.cs
    RoomManager.cs
    TinySync.Server.csproj

  TinySync.sln
```

### 环境要求
- .NET SDK 8.0+
- Windows/macOS/Linux 均可运行（仓库目前在 Windows 下开发与测试）

### 快速开始
1) 克隆并构建
```bash
git clone https://github.com/Ariesybs/TinySync.git
cd TinySync
dotnet build
```

2) 运行服务端（开发模式）
```bash
dotnet run --project TinySync.Server
```

3) 发布运行（可选）
```bash
dotnet publish TinySync.Server -c Release -o out
# 在 out 目录中启动：
./TinySync.Server
```

运行后将生成日志到 `logs/`（或构建输出目录下的 `logs/`）。

### 基本架构
- **核心模型**：`TinySync.Core/Protocol/*` 定义 HTTP/UDP 的消息/数据模型。
- **网络/房间层**：`TinySync.Server/NetworkManager.cs`、`Room*.cs` 负责会话与广播。
- **HTTP 接口**：`TinySync.Server/HttpEndpoints.cs` 提供房间管理等控制入口。

如需查看/扩展接口，请直接阅读上述文件并按需修改。

### 开发与调试
- 使用 IDE（JetBrains Rider / VS / VS Code）打开 `TinySync.sln`。
- 启动 `TinySync.Server` 即可在本地调试。
- 日志默认输出在运行目录的 `logs/` 下，可按日期查看。

### 路线图（Roadmap）
- 服务端：增加断线重连机制
- 服务端：增加世界快照的保存与恢复
- 客户端示例：
  - 客户端预测与回滚
  - 定点数库的接入
  - 网络波动的优化（抖动/丢包/延迟抖动处理）

### 许可协议
选择并添加合适的开源许可证（例如 MIT/Apache-2.0）。在仓库根目录放置 `LICENSE` 文件。

### 致谢
- [LiteNetLib](https://github.com/RevenantX/LiteNetLib)
- [MemoryPack](https://github.com/Cysharp/MemoryPack)
- [ASP.NET Core](https://github.com/dotnet/aspnetcore)
- [NLog](https://github.com/NLog/NLog)


