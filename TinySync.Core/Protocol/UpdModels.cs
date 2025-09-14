using System;
using MemoryPack;

namespace TinySync.Core.Protocol
{
    /// <summary>
    /// 协议消息类型。所有网络包的第一个字节都是该枚举，用于区分消息。
    /// </summary>
    public enum MsgType
    {
        ServerMsg, // 服务器消息
        RoomMsg, // 房间消息
    }
    
    [MemoryPackable]
    public partial class ServerMessage
    {
        public enum ServerMsgType
        {
            HelloServer, // 客户端向服务器发送必要数据(玩家ID)
        }
        public ServerMsgType MsgType { get; set; }
        public byte[] Payload { get; set; } = [];
        
        [MemoryPackable]
        public partial class HelloServer
        {
            [MemoryPackOrder(0)] public int PlayerId { get; set; } // 玩家 ID
        }
    }

    /// <summary>
    /// 客户端上报输入的网络消息。携带房间以及具体输入内容。
    /// </summary>
    [MemoryPackable]
    public partial class RoomMessage
    {
        public enum RoomMsgType
        {
            MemberUpdate, // 房间成员变动
            StartGame, // 服务器通知所有玩家开始游戏
            LoadSceneDone, // 玩家通知服务器加载场景完成
            StartFrameSync, // 服务器通知所有玩家开始帧同步
            PlayerInput, // 客户端上报输入
            SyncFrame, // 服务器下发所有玩家帧数据
        }
        [MemoryPackOrder(0)] public int RoomId { get; set; } // 房间 ID
        [MemoryPackOrder(1)] public RoomMsgType MsgType { get; set; }
        [MemoryPackOrder(1)] public byte[] Payload { get; set; } = []; // 玩家输入数据
        
        [MemoryPackable]
        public partial class RoomMemberUpdate
        {
            [MemoryPackOrder(0)] public int RoomId { get; set; }
            [MemoryPackOrder(1)] public int MaxPlayers { get; set; }
            [MemoryPackOrder(2)] public int OwnerId { get; set; }
            [MemoryPackOrder(3)] public int[] MemberIds { get; set; } = [];
        }

        [MemoryPackable]
        public partial class LoadSceneDone
        {
            [MemoryPackOrder(0)] public int PlayerId { get; set; }
        }
        
        /// <summary>
        /// 单个玩家在某一帧的输入。
        /// </summary>
        [MemoryPackable]
        public partial class PlayerInput
        {
            [MemoryPackOrder(0)] public int PlayerId { get; set; } // 玩家 ID
            [MemoryPackOrder(1)] public uint SyncedFrame { get; set; } // 已同步的帧号
            [MemoryPackOrder(2)] public uint TargetFrame { get; set; } // 该输入所属的目标帧号
            [MemoryPackOrder(3)] public int Cmd { get; set; } // 输入命令编码
            [MemoryPackOrder(4)] public byte[] Arg { get; set; } = []; // 命令附带参数
        }
        
        /// <summary>
        /// 服务器聚合后的帧包。包含该帧内所有玩家的输入数组，下发给所有客户端。
        /// </summary>
        [MemoryPackable]
        public partial class FramePackage
        {
            [MemoryPackOrder(0)] public int RoomId { get; set; } // 房间 ID
            [MemoryPackOrder(1)] public uint Frame { get; set; } // 帧号
            [MemoryPackOrder(2)] public PlayerInput[] Inputs { get; set; } = []; // 该帧内所有玩家输入
        }
    }

    
    
}