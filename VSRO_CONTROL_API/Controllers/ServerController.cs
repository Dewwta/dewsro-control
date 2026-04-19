using CoreLib.Tools.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VSRO_CONTROL_API.Attributes;
using VSRO_CONTROL_API.VSRO;
using VSRO_CONTROL_API.VSRO.DTO;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Network;
using VSRO_CONTROL_API.VSRO.Settings;

namespace VSRO_CONTROL_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [RequireAdmin]
    public class ServerController : ControllerBase
    {
        // GET api/server/public-status
        [AllowAnonymous]
        [HttpGet("public-status")]
        public IActionResult GetPublicStatus()
        {
            if (!Overseer.HasInitialized)
                return Ok(new { isRunning = false });

            var status = Overseer.GetServerStatus();
            return Ok(new { isRunning = status.IsRunning });
        }

        // GET api/server/status
        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            if (!Overseer.HasInitialized)
                return StatusCode(503, new { message = "Overseer not initialized." });

            return Ok(Overseer.GetServerStatus());
        }

        // POST api/server/start
        [HttpPost("start")]
        public async Task<IActionResult> Start()
        {
            if (!Overseer.HasInitialized)
                return StatusCode(503, new { message = "Overseer not initialized." });

            bool initiated = await Overseer.StartVsroServer();
            if (!initiated)
                return Conflict(new { message = "Server startup is already in progress." });

            return Accepted(new { message = "Server startup initiated." });
        }

        // POST api/server/shutdown
        [HttpPost("shutdown")]
        public IActionResult Shutdown()
        {
            if (!Overseer.HasInitialized)
                return StatusCode(503, new { message = "Overseer not initialized." });

            bool success = Overseer.ShutdownVsroServer();
            if (!success)
                return Conflict(new { message = "Cannot shutdown while startup is in progress." });

            return Ok(new { message = "Shutdown complete." });
        }

        // POST api/server/restart-gateway
        [HttpPost("restart-gateway")]
        public IActionResult RestartGateway()
        {
            if (!Overseer.HasInitialized)
                return StatusCode(503, new { message = "Overseer not initialized." });

            bool success = Overseer.RestartGatewayServer();
            if (!success)
                return Conflict(new { message = "Cannot restart while startup is in progress." });

            return Ok(new { message = "Gateway Server restarted." });
        }

        // POST api/server/restart-shard-game
        [HttpPost("restart-shard-game")]
        public async Task<IActionResult> RestartShardGame()
        {
            if (!Overseer.HasInitialized)
                return StatusCode(503, new { message = "Overseer not initialized." });

            bool initiated = await Overseer.RestartShardAndGame();
            if (!initiated)
                return Conflict(new { message = "Cannot restart while startup or another restart is in progress." });

            return Accepted(new { message = "Shard Manager + Game Server restart initiated." });
        }

        // GET api/server/settings
        [HttpGet("settings")]
        public IActionResult GetSettings()
        {
            if (!Overseer.HasInitialized)
                return StatusCode(503, new { message = "Overseer not initialized." });

            var settings = Overseer.GetSettings();
            if (settings == null)
                return NotFound(new { message = "Settings not loaded." });

            return Ok(settings);
        }

        // PUT api/server/settings
        [HttpPut("settings")]
        public async Task<IActionResult> UpdateSettings([FromBody] StartupSettings settings)
        {
            if (!Overseer.HasInitialized)
                return StatusCode(503, new { message = "Overseer not initialized." });

            await Overseer.UpdateSettings(settings);
            return Ok(new { message = "Settings updated." });
        }

        // POST api/server/proxy/start
        [HttpPost("proxy/start")]
        public IActionResult StartProxy()
        {
            if (!Overseer.HasInitialized)
                return StatusCode(503, new { message = "Overseer not initialized." });

            bool started = Overseer.StartProxy();
            if (!started)
                return Conflict(new { message = "Proxy is already running." });

            return Ok(new { message = "Integrated proxy started." });
        }

        // POST api/server/proxy/stop
        [HttpPost("proxy/stop")]
        public IActionResult StopProxy()
        {
            if (!Overseer.HasInitialized)
                return StatusCode(503, new { message = "Overseer not initialized." });

            bool stopped = Overseer.StopProxy();
            if (!stopped)
                return Conflict(new { message = "Proxy is not running." });

            return Ok(new { message = "Integrated proxy stopped." });
        }

        // POST api/server/proxy/restart
        [HttpPost("proxy/restart")]
        public IActionResult RestartProxy()
        {
            if (!Overseer.HasInitialized)
                return StatusCode(503, new { message = "Overseer not initialized." });

            Overseer.RestartProxy();
            return Ok(new { message = "Integrated proxy restarted." });
        }

        [HttpPost("notice")]
        public IActionResult SendNotice([FromBody] NoticeRequest request)
        {
            if (!Overseer.HasInitialized)
                return StatusCode(503, new { message = "Overseer not initialized." });

            if (string.IsNullOrWhiteSpace(request?.Message))
                return BadRequest(new { message = "Notice message cannot be empty." });

            int sent = Overseer.SendNotice(request.Message);
            if (sent < 0)
                return StatusCode(503, new { message = "Proxy not running." });

            return Ok(new { message = $"Notice sent to {sent} player(s).", playerCount = sent });
        }

        [HttpGet("log-opcodes")]
        public IActionResult GetLogOpcodes()
        {
            var opcodes = Overseer.opcodeLogHandlers.Keys
                .Select(op => $"0x{op:X4}")
                .ToList();
            return Ok(opcodes);
        }

        [HttpGet("add-log-opcode")]
        public IActionResult AddNewOpcode([FromQuery] string opcode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(opcode))
                    return BadRequest(new { message = "Opcode is required." });

                ushort op = Convert.ToUInt16(opcode, opcode.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? 16 : 10);

                if (Overseer.opcodeLogHandlers.ContainsKey(op))
                    return BadRequest(new { message = $"Opcode 0x{op:X4} is already being logged." });

                Server.PacketTransferEventHandler handler = (sender, e) =>
                {
                    byte[] data = e.Packet.GetBytes();
                    Logger.Info(typeof(Overseer),
                        $"0x{e.Packet.Opcode:X4} | {data.Length} bytes HEX: {BitConverter.ToString(data).Replace("-", " ")} " +
                        $"ASCII: {System.Text.Encoding.ASCII.GetString(data.Select(b => b >= 0x20 && b < 0x7F ? b : (byte)'.').ToArray())}");
                };

                // Register on both client and server sides so you catch it regardless of direction
                Overseer.AgentProxy?.RegisterClientPacketHandler(op, handler);
                Overseer.AgentProxy?.RegisterServerPacketHandler(op, handler);
                Overseer.GatewayProxy?.RegisterServerPacketHandler(op, handler);
                Overseer.GatewayProxy?.RegisterServerPacketHandler(op, handler);

                Overseer.opcodeLogHandlers[op] = handler;

                return Ok(new { message = $"Now logging opcode 0x{op:X4}" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Invalid opcode: {ex.Message}" });
            }
        }

        [HttpGet("remove-log-opcode")]
        public IActionResult RemoveOpcode([FromQuery] string opcode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(opcode))
                    return BadRequest(new { message = "Opcode is required." });

                ushort op = Convert.ToUInt16(opcode, opcode.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? 16 : 10);

                if (!Overseer.opcodeLogHandlers.TryGetValue(op, out var handler))
                    return BadRequest(new { message = $"Opcode 0x{op:X4} is not being logged." });

                Overseer.AgentProxy?.UnregisterClientPacketHandler(op, handler);
                Overseer.AgentProxy?.UnregisterServerPacketHandler(op, handler);
                Overseer.GatewayProxy?.UnregisterClientPacketHandler(op, handler);
                Overseer.GatewayProxy?.UnregisterServerPacketHandler(op, handler);

                Overseer.opcodeLogHandlers.Remove(op);

                return Ok(new { message = $"Stopped logging opcode 0x{op:X4}" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Invalid opcode: {ex.Message}" });
            }
        }

        [HttpGet("clear-log-opcodes")]
        public IActionResult ClearOpcodes()
        {
            try
            {
                foreach (var kvp in Overseer.opcodeLogHandlers)
                {
                    Overseer.AgentProxy?.UnregisterClientPacketHandler(kvp.Key, kvp.Value);
                    Overseer.AgentProxy?.UnregisterServerPacketHandler(kvp.Key, kvp.Value);
                    Overseer.GatewayProxy?.UnregisterClientPacketHandler(kvp.Key, kvp.Value);
                    Overseer.GatewayProxy?.UnregisterServerPacketHandler(kvp.Key, kvp.Value);
                }

                int count = Overseer.opcodeLogHandlers.Count;
                Overseer.opcodeLogHandlers.Clear();

                return Ok(new { message = $"Cleared {count} logged opcode(s)." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }


    }
}
