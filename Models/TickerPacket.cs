namespace Models
{
    public class TickerPacket
    {
        public int Sequence { get; set; }
        public string Symbol { get; set; }
        public char Side { get; set; } // 'B' or 'S'
        public int Quantity { get; set; }
        public int Price { get; set; }
    }
}
