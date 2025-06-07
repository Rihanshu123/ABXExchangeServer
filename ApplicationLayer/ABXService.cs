using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using Models;

namespace ApplicationLayer
{
    public class ABXService : IABXService
    {
        private const string Host = "localhost";
        private const int Port = 3000;
        private const int PacketSize = 17;
        public ABXService()
        {

        }
        public async Task<SortedDictionary<int, TickerPacket>> ReceiveAllPackets()
        {
            var packets = new SortedDictionary<int, TickerPacket>();
            using var client = new TcpClient();
            await client.ConnectAsync(Host, Port);
            using var stream = client.GetStream();

            // Send callType 1 (Stream All Packets)
            stream.WriteByte(1);
            stream.WriteByte(0); // resendSeq not used for type 1

            byte[] buffer = new byte[PacketSize];
            int bytesRead;

            try
            {
                while ((bytesRead = await stream.ReadAsync(buffer, 0, PacketSize)) == PacketSize)
                {
                    var packet = ParsePacket(buffer);
                    if (!packets.ContainsKey(packet.Sequence))
                    {
                        packets[packet.Sequence] = packet;
                        Console.WriteLine($"Received Seq {packet.Sequence}: {packet.Symbol} {packet.Side} {packet.Quantity} @ {packet.Price}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading packets: {ex.Message}");
            }

            return packets;
        }
        internal TickerPacket ParsePacket(byte[] buffer)
        {
            return new TickerPacket
            {
                Symbol = Encoding.ASCII.GetString(buffer, 0, 4),
                Side = (char)buffer[4],
                Quantity = BinaryPrimitives.ReadInt32BigEndian(buffer.AsSpan(5, 4)),
                Price = BinaryPrimitives.ReadInt32BigEndian(buffer.AsSpan(9, 4)),
                Sequence = BinaryPrimitives.ReadInt32BigEndian(buffer.AsSpan(13, 4))
            };
        }
        public async Task<List<int>> GetMissingSequences(SortedDictionary<int, TickerPacket> packets)
        {
            var missing = new List<int>();
            if (packets.Count == 0) return missing;

            int expected = packets.Keys.Min();
            int last = packets.Keys.Max();

            for (int i = expected; i <= last; i++)
            {
                if (!packets.ContainsKey(i))
                    missing.Add(i);
            }

            Console.WriteLine($" Missing sequences: {string.Join(", ", missing)}");
            return missing;
        }
        public async Task<TickerPacket?> RequestMissingPacket(int seq)
        {
            using var client = new TcpClient();
            await client.ConnectAsync(Host, Port);
            using var stream = client.GetStream();

            // Send callType 2 (Resend)
            stream.WriteByte(2);
            stream.WriteByte((byte)seq); // resendSeq (1 byte)

            byte[] buffer = new byte[PacketSize];
            int bytesRead = await stream.ReadAsync(buffer, 0, PacketSize);

            if (bytesRead == PacketSize)
            {
                var packet = ParsePacket(buffer);
                Console.WriteLine($"🔁 Resent Seq {packet.Sequence}: {packet.Symbol} {packet.Side} {packet.Quantity} @ {packet.Price}");
                return packet;
            }

            Console.WriteLine($"❌ Failed to receive resent packet for Seq {seq}");
            return null;
        }
    }
}
