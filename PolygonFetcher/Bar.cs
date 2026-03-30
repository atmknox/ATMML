using System;

namespace PolygonFetcher
{
    public class Bar
    {
        public DateTime Time { get; set; }
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public double Volume { get; set; }

        public Bar()
        {
        }

        public Bar(DateTime time, double open, double high, double low, double close, double volume)
        {
            Time = time;
            Open = open;
            High = high;
            Low = low;
            Close = close;
            Volume = volume;
        }

        public override string ToString()
        {
            return $"{Time:yyyy-MM-dd HH:mm} O:{Open:F2} H:{High:F2} L:{Low:F2} C:{Close:F2} V:{Volume:N0}";
        }
    }
}
