using System.Collections.Concurrent;
using LiteNetLib;
using LiteNetLib.Utils;
using MemoryPack;
using NLog;
using TinySync.Core.Protocol;

namespace TinySync.Server;

/// <summary>
/// 表示一个逻辑房间（Room）。
/// 负责：维护成员连接、收集某一帧的所有玩家输入、按固定 tick 驱动并广播帧包。
/// 为避免锁竞争，推荐“每房间单线程”方式在 Program 的主循环中按顺序驱动。
/// </summary>
public class Room
{
	// ====== 日志 ======
	private static readonly Logger Log = LogManager.GetLogger("Room");
	// ====== 配置与状态 ======
	private readonly TimeSpan m_TickInterval;      // 每次 tick 的时间间隔
	private DateTime m_NextTick;                   // 下一次 tick 的目标时间点（UTC）

	// ====== 成员与输入缓存 ======
	private readonly ConcurrentDictionary<int,NetPeer> m_Peers = new();          // 玩家 ID -> 对应网络连接
	private readonly ConcurrentDictionary<int, bool> m_PlayerLoadSceneDone = new();    // 玩家是否加载完成
	private readonly ConcurrentDictionary<uint,ConcurrentDictionary<int,RoomMessage.PlayerInput>> m_PlayerInputsBuffer = new();    // 玩家输入缓存,uint:帧号,int:玩家ID,PlayerInput:玩家输入
	private readonly ConcurrentDictionary<int,RoomMessage.PlayerInput> m_CurrentPlayersInput = new();    // 尚未出帧的“当前帧输入”缓存
	private readonly ConcurrentDictionary<int,uint> m_PlayerSyncedFrame = new();	// 玩家已同步到的帧
	private readonly ConcurrentQueue<RoomMessage.PlayerInput[]> m_PlayersHistoryInputs = new();    // 已出帧的“历史输入”（按顺序）
	private bool m_IsStartFrameSynced = false; // 是否启动帧同步
	private readonly List<int> m_PlayerIds = []; // 房间内玩家ID列表


	public int RoomId { get; }
	public int TickRate { get; }
	public int OwnerId { get; set; }
	public int MaxPlayers { get; set; }
	public uint CurrentFrame { get; private set; }
	public int PlayerCount { get; private set; }

	// 初始化房间
	public Room(int roomId, int tickRate, int ownerId, int maxPlayers)
	{
		RoomId = roomId;
		TickRate = tickRate;
		OwnerId = ownerId;
		MaxPlayers = maxPlayers;
		m_TickInterval = TimeSpan.FromSeconds(1.0 / TickRate);
		CurrentFrame = 0;
		PlayerCount = 0;
	}
	

	/// <summary>
	/// 添加或更新玩家连接。重复加入会更新其 NetPeer（断线重连时有用）。
	/// </summary>
	public void AddOrUpdatePlayer(int playerId, NetPeer peer)
	{
		if(IsPlayerInRoom(playerId))return;
		m_Peers[playerId] = peer;
		m_PlayerIds.Add(playerId);
		PlayerCount = m_PlayerIds.Count;
		Log.Info($"Room[{RoomId}] AddPlayer[{playerId}], Player Count[{PlayerCount}]");
		
		// 通知所以玩家新玩家加入房间
		var roomMemberUpdate = new RoomMessage.RoomMemberUpdate()
		{
			RoomId = this.RoomId,
			MaxPlayers = this.MaxPlayers,
			OwnerId = this.OwnerId,
			MemberIds = m_PlayerIds.ToArray(),
		};
		var roomMessage = new RoomMessage()
		{
			RoomId = this.RoomId,
			MsgType = RoomMessage.RoomMsgType.MemberUpdate,
			Payload = MemoryPackSerializer.Serialize(roomMemberUpdate),
		};
		
		var payload = MemoryPackSerializer.Serialize(roomMessage);
		SendMessageToAllPlayers(MsgType.RoomMsg, payload);
		// StartFrameSync();
	}

	public void RemovePlayer(int playerId)
	{
		if (!IsPlayerInRoom(playerId)) return;
		m_PlayerIds.Remove(playerId);
		m_Peers.TryRemove(playerId,out _);
		PlayerCount = m_PlayerIds.Count;
		Log.Info($"Room[{RoomId}] Remove Player[{playerId}], Player Count[{PlayerCount}]");
		
		// 通知所有玩家玩家离开房间
		var roomMemberUpdate = new RoomMessage.RoomMemberUpdate()
		{
			RoomId = this.RoomId,
			MaxPlayers = this.MaxPlayers,
			OwnerId = this.OwnerId,
			MemberIds = m_PlayerIds.ToArray(),
		};
		var roomMessage = new RoomMessage()
		{
			RoomId = this.RoomId,
			MsgType = RoomMessage.RoomMsgType.MemberUpdate,
			Payload = MemoryPackSerializer.Serialize(roomMemberUpdate),
		};
		
		var payload = MemoryPackSerializer.Serialize(roomMessage);
		SendMessageToAllPlayers(MsgType.RoomMsg, payload);
		// StopFrameSync();
	}

	public void StartGame()
	{
		// 通知所有玩家，游戏开始
		var roomMessage = new RoomMessage()
		{
			RoomId = this.RoomId,
			MsgType = RoomMessage.RoomMsgType.StartGame,
			Payload = [],
		};
		var payload = MemoryPackSerializer.Serialize(roomMessage);
		SendMessageToAllPlayers(MsgType.RoomMsg, payload);
		
		// 初始化玩家加载状态字典
		m_PlayerLoadSceneDone.Clear();
		foreach (var playerId in m_PlayerIds)
		{
			m_PlayerLoadSceneDone[playerId] = false;
		}
	}

	public bool IsFull()
	{
		return PlayerCount >= MaxPlayers;
	}

	public bool IsEmpty()
	{
		return PlayerCount == 0;
	}

	public bool IsPlayerInRoom(int playerId)
	{
		return m_PlayerIds.Contains(playerId);
	}

	public bool IsRoomOwner(int playerId)
	{
		return OwnerId == playerId;
	}

	public void OnMessage(RoomMessage message)
	{
		var payload = message.Payload;
		switch (message.MsgType)
		{
			case RoomMessage.RoomMsgType.LoadSceneDone:
				var loadSceneDone = MemoryPackSerializer.Deserialize<RoomMessage.LoadSceneDone>(payload);
				if (loadSceneDone == null) return;
				m_PlayerLoadSceneDone[loadSceneDone.PlayerId] = true;
				var playerLoadDoneCount = 0;
				foreach (var playerId in m_PlayerIds)
				{
					var isDone = m_PlayerLoadSceneDone[playerId];
					if (isDone) playerLoadDoneCount++;
				}
				Log.Info($"Room[{RoomId}] LoadSceneDone, Player Count[{m_PlayerIds.Count}], Done Count[{playerLoadDoneCount}], Not Done Count[{m_PlayerIds.Count - playerLoadDoneCount}]");
				if (playerLoadDoneCount == m_PlayerIds.Count)
				{
					// 所有玩家都加载完成，开启帧同步
					Log.Info($"Room[{RoomId}] All Players LoadSceneDone, Start FrameSync.");
					StartFrameSync();
				}
				break;
			case RoomMessage.RoomMsgType.PlayerInput:
				var playerInput = MemoryPackSerializer.Deserialize<RoomMessage.PlayerInput>(payload);
				if (playerInput == null) return;
				HandlePlayerInput(playerInput);
				HandlePlayerSyncedFrame(playerInput);
				break;
		}
		// HandlePlayerInput(playerInput);
		// HandlePlayerSyncedFrame(playerInput);
	}

	/// <summary>
	/// 记录玩家输入。仅当输入对应的目标帧等于当前帧时才会进入待处理集合。
	/// </summary>
	private void HandlePlayerInput(RoomMessage.PlayerInput input)
	{
		if (input.TargetFrame < CurrentFrame)
		{
			// 输入帧迟到，丢弃
			// Log.Debug($"Input[{input.PlayerId}] is later than current frame.hope {CurrentFrame} but {input.TargetFrame}");
			return;
		}
		if (input.TargetFrame == CurrentFrame)
		{
			// 输入对应当前帧，加入待处理集合
			m_CurrentPlayersInput[input.PlayerId] = input;
		}

		if (input.TargetFrame > CurrentFrame)
		{
			// 输入帧提前到达,缓存
			// Log.Debug($"Input[{input.PlayerId}] is earlier than current frame.hope {CurrentFrame} but {input.TargetFrame}");
			var frameDict = m_PlayerInputsBuffer.GetOrAdd(input.TargetFrame,
				_ => new ConcurrentDictionary<int, RoomMessage.PlayerInput>());
			frameDict.AddOrUpdate(input.PlayerId, input, (_, __) => input);
			// Log.Debug($"Input[{input.PlayerId}] is earlier than current frame.");
		}
		// 若输入早/晚于当前帧，在严格实现中可丢弃或缓存/回退，这里保持最小实现不处理。
	}

	private void HandlePlayerSyncedFrame(RoomMessage.PlayerInput input)
	{
		var syncedFrame = input.SyncedFrame;
		m_PlayerSyncedFrame[input.PlayerId] = syncedFrame;
	}

	private void StartFrameSync()
	{
		if(m_IsStartFrameSynced)return;
		m_IsStartFrameSynced = true;
		// 通知所有玩家启动帧指令收集
		var roomMessage = new RoomMessage()
		{
			RoomId = this.RoomId,
			MsgType = RoomMessage.RoomMsgType.StartFrameSync,
			Payload = [],
		};
		var payload = MemoryPackSerializer.Serialize(roomMessage);
		SendMessageToAllPlayers(MsgType.RoomMsg, payload);
		m_NextTick = DateTime.UtcNow + m_TickInterval; // 让第一帧在一个间隔后触发，避免“连发”
		_ = Task.Run((() =>
		{
			while (m_IsStartFrameSynced)
			{
				UpdateFrame();
				Thread.Sleep(1);
			}
		}));
		
	}

	
	/// <summary>
	/// 房间的帧驱动：按 Tick 率定时执行一次。
	/// - 未到下一 Tick 则直接返回。
	/// - 到点后：为缺失的玩家补默认输入，随后广播该帧并推进帧号。
	/// </summary>
	private void UpdateFrame()
	{
		// Console.WriteLine("UpdateFrame");
		var now = DateTime.UtcNow;
		if (now < m_NextTick) return;             // 尚未到出帧时间
		m_NextTick += m_TickInterval;             // 预约下一帧的时间
		
		var bufferedFrame = TryDequeueBufferedFrame(CurrentFrame, out var bufferedDict);

		// 对于未上报的玩家，使用“空输入”补齐，保证每帧都有完整人数的输入
		foreach (var playerId in m_Peers.Keys)
		{
			if (m_CurrentPlayersInput.ContainsKey(playerId))
				continue;

			// 2. 其次看提前缓存
			if (bufferedFrame && bufferedDict.TryGetValue(playerId, out var cachedInput))
			{
				m_CurrentPlayersInput[playerId] = cachedInput;
				// Log.Debug($"Player[{playerId}] Input[{cachedInput.Cmd}] is cached.");
				continue;
			}

			// 3. 都没有才补空输入
			m_CurrentPlayersInput[playerId] = new RoomMessage.PlayerInput
			{
				PlayerId = playerId,
				TargetFrame = CurrentFrame,
				Cmd = -1,
				Arg = [],
			};
		}
		
		// 广播当前帧，然后清空缓存并进入下一帧
		var playerInputsBufferArray = m_CurrentPlayersInput.Values.ToArray();
		BroadcastFrame(CurrentFrame, playerInputsBufferArray);
		m_PlayersHistoryInputs.Enqueue(playerInputsBufferArray);
		m_CurrentPlayersInput.Clear();
		CurrentFrame++;
		Log.Debug($"Synced Frame[{CurrentFrame}]");
		// Console.WriteLine("Frame[{0}] Broadcasted.", CurrentFrame);
	}
	
	private bool TryDequeueBufferedFrame(uint frame, out ConcurrentDictionary<int, RoomMessage.PlayerInput> frameDict)
	{
		return m_PlayerInputsBuffer.TryRemove(frame, out frameDict);
	}

	/// <summary>
	/// 将聚合后的帧包序列化并发送给所有在房间内的连接。
	/// </summary>
	private void BroadcastFrame(uint frame, RoomMessage.PlayerInput[] inputs)
	{
		// Console.WriteLine("BroadcastFrame[{0}]", frame);
		var pkg = new RoomMessage.FramePackage()
		{
			RoomId = RoomId,
			Frame = frame,
			Inputs = inputs,
		};
		var roomMessage = new RoomMessage()
		{
			RoomId = this.RoomId,
			MsgType = RoomMessage.RoomMsgType.SyncFrame,
			Payload = MemoryPackSerializer.Serialize(pkg),
		};
		var payload = MemoryPackSerializer.Serialize(roomMessage);
		SendMessageToAllPlayers(MsgType.RoomMsg, payload);
	}

	private void SendMessageToAllPlayers(MsgType msgType, byte[] payload)
	{
		var writer = new NetDataWriter();
		writer.Put((int)msgType); // 先放 MsgType，再放二进制 payload
		writer.Put(payload);
		foreach (var p in m_Peers.Values)
		{
			p.Send(writer,DeliveryMethod.ReliableOrdered);
		}
	}

	public void Dispose()
	{
		Log.Info($"Room[{RoomId}] Disposed.");
		m_PlayersHistoryInputs.Clear();
		m_CurrentPlayersInput.Clear();
		m_Peers.Clear();
		m_PlayerSyncedFrame.Clear();
	}
	
}