using System.Net.Sockets;
using System.Text.Json;
using ApplicationLayer;
using Microsoft.AspNetCore.Mvc;
using Models;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ABXController : ControllerBase
    {
        private readonly IABXService _abxService;
        private readonly ILogger<ABXController> _logger;
        public ABXController(IABXService aBXService, ILogger<ABXController> logger)
        {
            _abxService = aBXService;
            _logger = logger;
        }
        [HttpGet("GetDataPackets", Name = "GetDataPackets")]
        public async Task<IActionResult> GetDataPackets()
        {
            try
            {
                _logger.LogInformation("Starting to receive ABX packets...");

                var packets = await _abxService.ReceiveAllPackets();
                int expectedCount = packets.Values.Max(p => p.Sequence);
                HashSet<int> missingSequences = Enumerable.Range(1, expectedCount)
                                          .Where(seq => !packets.ContainsKey(seq))
                                          .ToHashSet();
                while (missingSequences.Any())
                {
                    _logger.LogInformation($"Missing sequences: {string.Join(", ", missingSequences)}");

                    foreach (int seq in missingSequences.ToList())
                    {
                        try
                        {
                            var packet = await _abxService.RequestMissingPacket(seq);
                            if (packet != null)
                            {
                                packets[packet.Sequence] = packet;
                                missingSequences.Remove(packet.Sequence);
                            }
                            else
                            {
                                _logger.LogWarning($"Could not retrieve packet with sequence {seq}.");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error retrieving packet with sequence {seq}.");
                        }
                    }

                    // Optional delay between retries
                    await Task.Delay(100);
                }

                // Final sorting and export
                var sortedPackets = new List<TickerPacket>(packets.Values);
                sortedPackets.Sort((a, b) => a.Sequence.CompareTo(b.Sequence));

                string json = JsonSerializer.Serialize(sortedPackets, new JsonSerializerOptions { WriteIndented = true });

                var userRoot = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var outputPath = Path.Combine(userRoot, "output.json");

                await System.IO.File.WriteAllTextAsync(outputPath, json);
                _logger.LogInformation($"All packets written to: {outputPath}");

                return Ok(json);
            }
            catch (SocketException ex)
            {
                _logger.LogError(ex, "Network error while connecting to ABX server.");
                return StatusCode(503, "Unable to connect to ABX server. Please try again later.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred while processing ABX packets.");
                return StatusCode(500, "An error occurred while processing your request.");
            }
        }
    }
}
