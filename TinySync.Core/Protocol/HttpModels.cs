using Newtonsoft.Json;

namespace TinySync.Core.Protocol;

// 创建房间请求
public class CreateRoomRequest
{
    [JsonProperty("player_id")]
    public int PlayerId { get; set; }
    [JsonProperty("net_peer_id")]
    public int NetPeerId { get; set; }
    [JsonProperty("max_players")]
    public int MaxPlayers { get; set; }
    [JsonProperty("message")] 
    public string Message { get; set; } = "";
}
// 创建房间响应
public class CreateRoomResponse
{
    [JsonProperty("player_id")]
    public int PlayerId { get; set; }
    [JsonProperty("room_id")]
    public int RoomId { get; set; }
    [JsonProperty("success")]
    public bool Success { get; set; }
    [JsonProperty("message")] 
    public string Message { get; set; } = "";
}

// 加入房间请求
public class JoinRoomRequest
{
    [JsonProperty("player_id")]
    public int PlayerId { get; set; }
    [JsonProperty("room_id")]
    public int RoomId { get; set; }
    [JsonProperty("net_peer_id")]
    public int NetPeerId { get; set; }
    [JsonProperty("message")] 
    public string Message { get; set; } = "";
}
// 加入房间响应
public class JoinRoomResponse
{
    [JsonProperty("room_id")]
    public long RoomId { get; set; }
    [JsonProperty("player_id")]
    public int PlayerId { get; set; }
    [JsonProperty("success")]
    public bool Success { get; set; }
    [JsonProperty("message")] 
    public string Message { get; set; } = "";
}

// 离开房间请求
public class LeaveRoomRequest
{
    [JsonProperty("player_id")]
    public int PlayerId { get; set; }
    [JsonProperty("room_id")]
    public int RoomId { get; set; }
    [JsonProperty("message")] 
    public string Message { get; set; } = "";
}
// 离开房间响应
public class LeaveRoomResponse
{
    [JsonProperty("room_id")]
    public int RoomId { get; set; }
    [JsonProperty("player_id")]
    public int PlayerId { get; set; }
    [JsonProperty("success")]
    public bool Success { get; set; }
    [JsonProperty("message")] 
    public string Message { get; set; } = "";
}

// 开始游戏请求
public class StartGameRequest
{
    [JsonProperty("player_id")]
    public int PlayerId { get; set; }
    [JsonProperty("room_id")]
    public int RoomId { get; set; }
    [JsonProperty("message")] 
    public string Message { get; set; } = "";
}
// 开始游戏响应
public class StartGameResponse
{
    [JsonProperty("room_id")]
    public int RoomId { get; set; }
    [JsonProperty("player_id")]
    public int PlayerId { get; set; }
    [JsonProperty("success")]
    public bool Success { get; set; }
    [JsonProperty("message")] 
    public string Message { get; set; } = "";
}

