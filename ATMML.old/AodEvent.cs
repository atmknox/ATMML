using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ATMML
{
    public enum AodEventType
    {
        Close = 1,
        Add = 2,
        Source = 3,
        Interval = 4,
        Symbol = 5,
        Chart = 6,
        Model = 7,
        Save = 8
    }

    public class AodEventArgs : EventArgs
    {
        AodEventType _id = AodEventType.Close;

        public AodEventArgs(AodEventType id)
        {
            _id = id;
        }

        public AodEventType Id
        {
            get { return _id; }
        }
    }

    public delegate void AodEventHandler(object sender, AodEventArgs e);
}
