using NLog;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TinySync.Core.Protocol;

namespace TinySync.Server;

[ApiController]
[Route("api/room")]
public class HttpEndpoints: ControllerBase
{
    private static readonly Logger Log = LogManager.GetLogger("HttpEndpoints");

    [HttpPost("create")]
    public ActionResult<CreateRoomResponse> CreateRoom([FromBody] CreateRoomRequest req)
    {
        Log.Info($"Player[{req.PlayerId}] request create room");
        var (ok, roomId, message) = RoomManager.CreateRoom(req.PlayerId, req.NetPeerId,req.MaxPlayers);
        return Ok(new CreateRoomResponse()
        {
            RoomId = roomId,
            PlayerId = req.PlayerId,
            Success = ok,
            Message = message
        });

    }

    [HttpPost("join")]
    public ActionResult<JoinRoomResponse> JoinRoom([FromBody] JoinRoomRequest req)
    {
        Log.Info($"Player[{req.PlayerId}] request join room[{req.RoomId}]");
        var (ok,message) = RoomManager.JoinRoom(req.PlayerId, req.RoomId, req.NetPeerId);
        return Ok(new JoinRoomResponse()
        {
            Success = ok,
            PlayerId = req.PlayerId,
            RoomId = req.RoomId,
            Message = message
        });
    }

    [HttpPost("leave")]
    public ActionResult<LeaveRoomResponse> LeaveRoom([FromBody] LeaveRoomRequest req)
    {
        Log.Info($"Player[{req.PlayerId}] request leave room[{req.RoomId}]");
        var (ok, message) = RoomManager.LeaveRoom(req.PlayerId,req.RoomId);
        return Ok(new LeaveRoomResponse() { Success = ok, Message = message });
    }

    [HttpPost("start")]
    public ActionResult<StartGameResponse> StartGame([FromBody] StartGameRequest req)
    {
        Log.Info($"Player[{req.PlayerId}] request start game");
        var (ok, message) = RoomManager.StartGame(req.PlayerId, req.RoomId);
        return Ok(new StartGameResponse() { Success = ok, Message = message });
    }
}