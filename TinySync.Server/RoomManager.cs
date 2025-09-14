using System.Collections.Concurrent;
using LiteNetLib;
using LiteNetLib.Utils;
using MemoryPack;
using NLog;
using TinySync.Core.Protocol;

namespace TinySync.Server;

// 房间管理器
public static class RoomManager
{
    private static readonly Logger Log = LogManager.GetLogger("TinySync.RoomManager");
    private const int Port = 9050; //服务器监听端口（UDP）
    private static readonly ConcurrentDictionary<int, Room> m_GameRooms = new(); // 房间容器,key 为房间 ID
    private static readonly ConcurrentDictionary<int, Room> m_PlayerBelongs = new(); // 客户端连接所属房间, key 为玩家 ID
    private static readonly ConcurrentDictionary<int,NetPeer> m_playerIdToNetPeer = new(); // 玩家 ID 与 NetPeer 映射, key 为玩家 ID

    private static int m_RoomId = 0;

    public static void Initialize()
    {
        NetworkManager.OnPeerConnectedEvent += OnPeerConnected;
        NetworkManager.OnPeerDisconnectedEvent += OnPeerDisconnected;
        NetworkManager.OnPeerReceiveEvent += OnPeerReceiveEvent;
    }
    private static void OnPeerReceiveEvent(NetPeer peer, NetPacketReader reader, byte m_Channel, DeliveryMethod m_DeliveryMethod)
    {
        var msgType = reader.GetInt();
        var len = reader.AvailableBytes;
        var data = new byte[len];
        reader.GetBytes(data, 0, len);

        switch ((MsgType)msgType)
        {
            case MsgType.RoomMsg:
            {
                var roomMessage = MemoryPackSerializer.Deserialize<RoomMessage>(data);
                if (roomMessage == null)
                {
                    Console.Write($"Deserialize InputMsg Failed");
                    return;
                }
                HandleRoomMessage(roomMessage);
                break;
            }
            case MsgType.ServerMsg:
            {
                var serverMessage = MemoryPackSerializer.Deserialize<ServerMessage>(data);
                if(serverMessage == null)return;
                switch (serverMessage.MsgType)
                {
                    case ServerMessage.ServerMsgType.HelloServer:
                        var hello = MemoryPackSerializer.Deserialize<ServerMessage.HelloServer>(serverMessage.Payload);
                        if (hello == null) return;
                        var playerId = hello.PlayerId;
                        m_playerIdToNetPeer.TryAdd(playerId, peer);
                        break;
                }
                
                break;
            }
        }

        reader.Recycle();
    }
    

    private static void HandleRoomMessage(RoomMessage roomMessage)
    {
        // 所有玩家输入上报：附带房间 ID 和目标帧
        if (!m_GameRooms.TryGetValue(roomMessage.RoomId, out var room))
        {
            Console.WriteLine($"Room {roomMessage.RoomId} not found");
            return;
        }
        room.OnMessage(roomMessage);
    }
    

    private static void OnPeerConnected(NetPeer peer)
    {
        
    }

    private static void OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
    {
        

    }

    public static (bool, int, string) CreateRoom(int playerId, int netPeerId,int maxPlayers)
    {
        var playerInRoom = m_PlayerBelongs.TryGetValue(playerId, out var belongRoom);
        if (playerInRoom && belongRoom!= null)
        {
            Log.Info($"playerId:{playerId} already in room[{belongRoom.RoomId}]");
            return (false, belongRoom.RoomId,$"you already in room[{belongRoom.RoomId}]");
        }
        m_RoomId++;
        Log.Info($"playerId:{playerId} request create room[{m_RoomId}]");
        var room = new Room(m_RoomId, 30,playerId,maxPlayers);
        var addSuccess = m_GameRooms.TryAdd(m_RoomId, room);
        if (!addSuccess)
        {
            return (false, -1, $"create room[{m_RoomId}] failed");
        }
        var ok = m_playerIdToNetPeer.TryGetValue(playerId, out var netPeer);
        if (!ok || netPeer == null)
        {
            Log.Info($"NetPeer not found");
            return (false,-1,$"please connect udp server first");
        }
        room.AddOrUpdatePlayer(playerId, netPeer);
        m_PlayerBelongs.TryAdd(playerId, room);
        return (addSuccess, m_RoomId,$"create room[{m_RoomId}] success");
    }

    public static (bool, string) JoinRoom(int playerId, int roomId, int netPeerId)
    {
        var playerInRoom = m_PlayerBelongs.TryGetValue(playerId, out var belongRoom);
        if (playerInRoom && belongRoom!= null)
        {
            Log.Info($"playerId:{playerId} already in room[{belongRoom.RoomId}]");
            return (false,$"you already in room[{belongRoom.RoomId}]");
        }
        m_GameRooms.TryGetValue(roomId, out var room);
        if (room == null)
        {
            Log.Info($"Room {roomId} not found");
            return (false,$"Room {roomId} not found");
        }

        var ok = m_playerIdToNetPeer.TryGetValue(playerId, out var netPeer);
        if (!ok || netPeer == null)
        {
            Log.Info($"NetPeer not found");
            return (false,$"please connect udp server");
        }

        if (room.IsFull())
        {
            Log.Info($"Room {roomId} is full");
            return (false,$"Room {roomId} is full");
        }

        room.AddOrUpdatePlayer(playerId, netPeer);
        m_PlayerBelongs.TryAdd(playerId, room);
        return (true,$"join room[{roomId}] success");
    }

    public static (bool, string) LeaveRoom(int playerId, int roomId)
    {
        var ok = m_PlayerBelongs.TryGetValue(playerId, out var room);
        if (!ok || room == null)
        {
            Log.Info($"Player {playerId} not found in any room");
            return (false,$"you not in any room");
        }
        if (room.RoomId != roomId)
        {
            Log.Info($"Player {playerId} not in room {roomId}");
            return (false,$"you not in room {roomId}");
        }

        room.RemovePlayer(playerId);
        m_PlayerBelongs.TryRemove(playerId, out var removedRoom);
        // 玩家数为0,房间已空,释放
        if (room.IsEmpty())
        {
            var isSuccess = m_GameRooms.TryRemove(room.RoomId, out var deletedRoom);
            if (isSuccess && deletedRoom != null)
            {
                deletedRoom.Dispose();
            }
        }
        Log.Info($"Player[{playerId}] leave room [{roomId}] success");
        return (true,$"leave room[{roomId}] success");
    }

    public static (bool, string) StartGame(int playerId, int roomId)
    {
        var isRoomFound = m_GameRooms.TryGetValue(roomId, out var room);
        if (!isRoomFound || room == null)
        {
            Log.Info($"Room [{roomId}] not found");
            return (false,$"room {roomId} not found");
        }

        if (!room.IsPlayerInRoom(playerId))
        {
            Log.Info($"Player [{playerId}] not in room [{roomId}]");
            return (false,$"you not in room {roomId}");
        }

        if (!room.IsRoomOwner(playerId))
        {
            Log.Info($"Player [{playerId}] not is the room[{roomId}]'s owner");
            return (false,$"you are not owner");
        }
        
        room.StartGame();
        Log.Info($"Start game in room [{roomId}] success");
        return (true,$"start game in room {roomId} success");
    }
}