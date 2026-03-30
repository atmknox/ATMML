using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ATMML
{
    enum AnalysisType
    {
        Open,
        High,
        Low,
        Close,
        Sig,
        ATMTarget,
        FTTP,
        STTP,
        Pivot,
        FibPivot
    }

    public enum OrderEventType
    {
        None,
        Long,
        LongAdd,
        LongReduce,
        LongClose,
        Short,
        ShortAdd,
        ShortReduce,
        ShortClose
    }

    public class OrderEventArgs : EventArgs
    {
        public OrderEventArgs(OrderEventType type = OrderEventType.None)
        {
            Type = type;
        }

        public OrderEventType Type { get; set; }
    }

    public delegate void OrderEventHandler(object sender, OrderEventArgs e);
}
