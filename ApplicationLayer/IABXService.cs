using Models;

namespace ApplicationLayer
{
    public interface IABXService
    {
        Task<SortedDictionary<int, TickerPacket>> ReceiveAllPackets();
        Task<List<int>> GetMissingSequences(SortedDictionary<int, TickerPacket> packets);
        Task<TickerPacket?> RequestMissingPacket(int seq);
    }
}
