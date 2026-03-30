using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media;
using static TorchSharp.torch.distributions.constraints;

namespace ATMML
{
	public enum TradeType
	{
        None,
		NewTrend,
		Add,
		Pressure,
		Retrace,
		Exhaustion,
        FTEntry,
        PortfolioStop,
        Bias,
        Alignment,
		PExhaustion,
        TimeExit,
		Signal,       
		Rebalance   
	}

	public class TradeSize
	{
		public TradeSize(double upSize, double dnSize, TradeType tradeType = TradeType.None)
		{
			UpSize = upSize;
			DnSize = dnSize;
            TradeType = tradeType;
		}

		public double UpSize { get; set; }
		public double DnSize { get; set; }

        public TradeType TradeType { get; set; }

		public static TradeSize operator &(TradeSize t1, TradeSize t2)
		{
            var tradeType = (TradeType)(Math.Max((int)t1.TradeType, (int)t2.TradeType));
			return new TradeSize(t1.UpSize * t2.UpSize, t1.DnSize * t2.DnSize, tradeType);
		}
	}

	public enum Senario
    {
        Close0AgoClose1Plus,
        Close0AgoClose2Plus,
        Close0AgoClose3Plus,
        Close0AgoClose4Plus,
        Close0AgoClose5Plus,
        Close0AgoClose6Plus,
        Close0AgoClose7Plus,
        Close0AgoClose8Plus,
        Close0AgoClose9Plus,
       
        Close1AgoClose1Plus,
        Close1AgoClose2Plus,
        Close1AgoClose3Plus,
        Close1AgoClose4Plus,
        Close1AgoClose5Plus,
        Close1AgoClose6Plus,
        Close1AgoClose7Plus,
        Close1AgoClose8Plus,
        Close1AgoClose9Plus,
      
        Close2AgoClose1Plus,
        Close2AgoClose2Plus,
        Close2AgoClose3Plus,
        Close2AgoClose4Plus,
        Close2AgoClose5Plus,
        Close2AgoClose6Plus,
        Close2AgoClose7Plus,
        Close2AgoClose8Plus,
        Close2AgoClose9Plus,

        Close3AgoClose1Plus,
        Close3AgoClose2Plus,
        Close3AgoClose3Plus,
        Close3AgoClose4Plus,
        Close3AgoClose5Plus,
        Close3AgoClose6Plus,
        Close3AgoClose7Plus,
        Close3AgoClose8Plus,
        Close3AgoClose9Plus,
        
        Close4AgoClose1Plus,
        Close4AgoClose2Plus,
        Close4AgoClose3Plus,
        Close4AgoClose4Plus,
        Close4AgoClose5Plus,
        Close4AgoClose6Plus,
        Close4AgoClose7Plus,
        Close4AgoClose8Plus,
        Close4AgoClose9Plus,
        
        Close5AgoClose1Plus,
        Close5AgoClose2Plus,
        Close5AgoClose3Plus,
        Close5AgoClose4Plus,
        Close5AgoClose5Plus,
        Close5AgoClose6Plus,
        Close5AgoClose7Plus,
        Close5AgoClose8Plus,
        Close5AgoClose9Plus,
      
        Close6AgoClose1Plus,
        Close6AgoClose2Plus,
        Close6AgoClose3Plus,
        Close6AgoClose4Plus,
        Close6AgoClose5Plus,
        Close6AgoClose6Plus,
        Close6AgoClose7Plus,
        Close6AgoClose8Plus,
        Close6AgoClose9Plus,
        
        Close7AgoClose1Plus,
        Close7AgoClose2Plus,
        Close7AgoClose3Plus,
        Close7AgoClose4Plus,
        Close7AgoClose5Plus,
        Close7AgoClose6Plus,
        Close7AgoClose7Plus,
        Close7AgoClose8Plus,
        Close7AgoClose9Plus,
        
        Close8AgoClose1Plus,
        Close8AgoClose2Plus,
        Close8AgoClose3Plus,
        Close8AgoClose4Plus,
        Close8AgoClose5Plus,
        Close8AgoClose6Plus,
        Close8AgoClose7Plus,
        Close8AgoClose8Plus,
        Close8AgoClose9Plus,
       
        Close9AgoClose1Plus,
        Close9AgoClose2Plus,
        Close9AgoClose3Plus,
        Close9AgoClose4Plus,
        Close9AgoClose5Plus,
        Close9AgoClose6Plus,
        Close9AgoClose7Plus,
        Close9AgoClose8Plus,
        Close9AgoClose9Plus,

        Close0AgoHigh1Plus,
        Close0AgoHigh2Plus,
        Close0AgoHigh3Plus,
        Close0AgoHigh4Plus,
        Close0AgoHigh5Plus,
        Close0AgoHigh6Plus,
        Close0AgoHigh7Plus,
        Close0AgoHigh8Plus,
        Close0AgoHigh9Plus,
        Close1AgoHigh1Plus,
        Close1AgoHigh2Plus,
        Close1AgoHigh3Plus,
        Close1AgoHigh4Plus,
        Close1AgoHigh5Plus,
        Close1AgoHigh6Plus,
        Close1AgoHigh7Plus,
        Close1AgoHigh8Plus,
        Close1AgoHigh9Plus,
        Close2AgoHigh1Plus,
        Close2AgoHigh2Plus,
        Close2AgoHigh3Plus,
        Close2AgoHigh4Plus,
        Close2AgoHigh5Plus,
        Close2AgoHigh6Plus,
        Close2AgoHigh7Plus,
        Close2AgoHigh8Plus,
        Close2AgoHigh9Plus,
        Close3AgoHigh1Plus,
        Close3AgoHigh2Plus,
        Close3AgoHigh3Plus,
        Close3AgoHigh4Plus,
        Close3AgoHigh5Plus,
        Close3AgoHigh6Plus,
        Close3AgoHigh7Plus,
        Close3AgoHigh8Plus,
        Close3AgoHigh9Plus,
        Close4AgoHigh1Plus,
        Close4AgoHigh2Plus,
        Close4AgoHigh3Plus,
        Close4AgoHigh4Plus,
        Close4AgoHigh5Plus,
        Close4AgoHigh6Plus,
        Close4AgoHigh7Plus,
        Close4AgoHigh8Plus,
        Close4AgoHigh9Plus,
        Close5AgoHigh1Plus,
        Close5AgoHigh2Plus,
        Close5AgoHigh3Plus,
        Close5AgoHigh4Plus,
        Close5AgoHigh5Plus,
        Close5AgoHigh6Plus,
        Close5AgoHigh7Plus,
        Close5AgoHigh8Plus,
        Close5AgoHigh9Plus,
        Close6AgoHigh1Plus,
        Close6AgoHigh2Plus,
        Close6AgoHigh3Plus,
        Close6AgoHigh4Plus,
        Close6AgoHigh5Plus,
        Close6AgoHigh6Plus,
        Close6AgoHigh7Plus,
        Close6AgoHigh8Plus,
        Close6AgoHigh9Plus,
        Close7AgoHigh1Plus,
        Close7AgoHigh2Plus,
        Close7AgoHigh3Plus,
        Close7AgoHigh4Plus,
        Close7AgoHigh5Plus,
        Close7AgoHigh6Plus,
        Close7AgoHigh7Plus,
        Close7AgoHigh8Plus,
        Close7AgoHigh9Plus,
        Close8AgoHigh1Plus,
        Close8AgoHigh2Plus,
        Close8AgoHigh3Plus,
        Close8AgoHigh4Plus,
        Close8AgoHigh5Plus,
        Close8AgoHigh6Plus,
        Close8AgoHigh7Plus,
        Close8AgoHigh8Plus,
        Close8AgoHigh9Plus,
        Close9AgoHigh1Plus,
        Close9AgoHigh2Plus,
        Close9AgoHigh3Plus,
        Close9AgoHigh4Plus,
        Close9AgoHigh5Plus,
        Close9AgoHigh6Plus,
        Close9AgoHigh7Plus,
        Close9AgoHigh8Plus,
        Close9AgoHigh9Plus,

        Close0AgoLow1Plus,
        Close0AgoLow2Plus,
        Close0AgoLow3Plus,
        Close0AgoLow4Plus,
        Close0AgoLow5Plus,
        Close0AgoLow6Plus,
        Close0AgoLow7Plus,
        Close0AgoLow8Plus,
        Close0AgoLow9Plus,
        Close1AgoLow1Plus,
        Close1AgoLow2Plus,
        Close1AgoLow3Plus,
        Close1AgoLow4Plus,
        Close1AgoLow5Plus,
        Close1AgoLow6Plus,
        Close1AgoLow7Plus,
        Close1AgoLow8Plus,
        Close1AgoLow9Plus,
        Close2AgoLow1Plus,
        Close2AgoLow2Plus,
        Close2AgoLow3Plus,
        Close2AgoLow4Plus,
        Close2AgoLow5Plus,
        Close2AgoLow6Plus,
        Close2AgoLow7Plus,
        Close2AgoLow8Plus,
        Close2AgoLow9Plus,
        Close3AgoLow1Plus,
        Close3AgoLow2Plus,
        Close3AgoLow3Plus,
        Close3AgoLow4Plus,
        Close3AgoLow5Plus,
        Close3AgoLow6Plus,
        Close3AgoLow7Plus,
        Close3AgoLow8Plus,
        Close3AgoLow9Plus,
        Close4AgoLow1Plus,
        Close4AgoLow2Plus,
        Close4AgoLow3Plus,
        Close4AgoLow4Plus,
        Close4AgoLow5Plus,
        Close4AgoLow6Plus,
        Close4AgoLow7Plus,
        Close4AgoLow8Plus,
        Close4AgoLow9Plus,
        Close5AgoLow1Plus,
        Close5AgoLow2Plus,
        Close5AgoLow3Plus,
        Close5AgoLow4Plus,
        Close5AgoLow5Plus,
        Close5AgoLow6Plus,
        Close5AgoLow7Plus,
        Close5AgoLow8Plus,
        Close5AgoLow9Plus,
        Close6AgoLow1Plus,
        Close6AgoLow2Plus,
        Close6AgoLow3Plus,
        Close6AgoLow4Plus,
        Close6AgoLow5Plus,
        Close6AgoLow6Plus,
        Close6AgoLow7Plus,
        Close6AgoLow8Plus,
        Close6AgoLow9Plus,
        Close7AgoLow1Plus,
        Close7AgoLow2Plus,
        Close7AgoLow3Plus,
        Close7AgoLow4Plus,
        Close7AgoLow5Plus,
        Close7AgoLow6Plus,
        Close7AgoLow7Plus,
        Close7AgoLow8Plus,
        Close7AgoLow9Plus,
        Close8AgoLow1Plus,
        Close8AgoLow2Plus,
        Close8AgoLow3Plus,
        Close8AgoLow4Plus,
        Close8AgoLow5Plus,
        Close8AgoLow6Plus,
        Close8AgoLow7Plus,
        Close8AgoLow8Plus,
        Close8AgoLow9Plus,
        Close9AgoLow1Plus,
        Close9AgoLow2Plus,
        Close9AgoLow3Plus,
        Close9AgoLow4Plus,
        Close9AgoLow5Plus,
        Close9AgoLow6Plus,
        Close9AgoLow7Plus,
        Close9AgoLow8Plus,
        Close9AgoLow9Plus,
        Close0AgoOpen1Plus,
        Close0AgoOpen2Plus,
        Close0AgoOpen3Plus,
        Close0AgoOpen4Plus,
        Close0AgoOpen5Plus,
        Close0AgoOpen6Plus,
        Close0AgoOpen7Plus,
        Close0AgoOpen8Plus,
        Close0AgoOpen9Plus,
        Close1AgoOpen1Plus,
        Close1AgoOpen2Plus,
        Close1AgoOpen3Plus,
        Close1AgoOpen4Plus,
        Close1AgoOpen5Plus,
        Close1AgoOpen6Plus,
        Close1AgoOpen7Plus,
        Close1AgoOpen8Plus,
        Close1AgoOpen9Plus,
        Close2AgoOpen1Plus,
        Close2AgoOpen2Plus,
        Close2AgoOpen3Plus,
        Close2AgoOpen4Plus,
        Close2AgoOpen5Plus,
        Close2AgoOpen6Plus,
        Close2AgoOpen7Plus,
        Close2AgoOpen8Plus,
        Close2AgoOpen9Plus,
        Close3AgoOpen1Plus,
        Close3AgoOpen2Plus,
        Close3AgoOpen3Plus,
        Close3AgoOpen4Plus,
        Close3AgoOpen5Plus,
        Close3AgoOpen6Plus,
        Close3AgoOpen7Plus,
        Close3AgoOpen8Plus,
        Close3AgoOpen9Plus,
        Close4AgoOpen1Plus,
        Close4AgoOpen2Plus,
        Close4AgoOpen3Plus,
        Close4AgoOpen4Plus,
        Close4AgoOpen5Plus,
        Close4AgoOpen6Plus,
        Close4AgoOpen7Plus,
        Close4AgoOpen8Plus,
        Close4AgoOpen9Plus,
        Close5AgoOpen1Plus,
        Close5AgoOpen2Plus,
        Close5AgoOpen3Plus,
        Close5AgoOpen4Plus,
        Close5AgoOpen5Plus,
        Close5AgoOpen6Plus,
        Close5AgoOpen7Plus,
        Close5AgoOpen8Plus,
        Close5AgoOpen9Plus,
        Close6AgoOpen1Plus,
        Close6AgoOpen2Plus,
        Close6AgoOpen3Plus,
        Close6AgoOpen4Plus,
        Close6AgoOpen5Plus,
        Close6AgoOpen6Plus,
        Close6AgoOpen7Plus,
        Close6AgoOpen8Plus,
        Close6AgoOpen9Plus,
        Close7AgoOpen1Plus,
        Close7AgoOpen2Plus,
        Close7AgoOpen3Plus,
        Close7AgoOpen4Plus,
        Close7AgoOpen5Plus,
        Close7AgoOpen6Plus,
        Close7AgoOpen7Plus,
        Close7AgoOpen8Plus,
        Close7AgoOpen9Plus,
        Close8AgoOpen1Plus,
        Close8AgoOpen2Plus,
        Close8AgoOpen3Plus,
        Close8AgoOpen4Plus,
        Close8AgoOpen5Plus,
        Close8AgoOpen6Plus,
        Close8AgoOpen7Plus,
        Close8AgoOpen8Plus,
        Close8AgoOpen9Plus,
        Close9AgoOpen1Plus,
        Close9AgoOpen2Plus,
        Close9AgoOpen3Plus,
        Close9AgoOpen4Plus,
        Close9AgoOpen5Plus,
        Close9AgoOpen6Plus,
        Close9AgoOpen7Plus,
        Close9AgoOpen8Plus,
        Close9AgoOpen9Plus,

        // op to
        Open0AgoOpen1Plus,
        Open0AgoOpen2Plus,
        Open0AgoOpen3Plus,
        Open0AgoOpen4Plus,
        Open0AgoOpen5Plus,
        Open0AgoOpen6Plus,
        Open0AgoOpen7Plus,
        Open0AgoOpen8Plus,
        Open0AgoOpen9Plus,
        Open1AgoOpen1Plus,
        Open1AgoOpen2Plus,
        Open1AgoOpen3Plus,
        Open1AgoOpen4Plus,
        Open1AgoOpen5Plus,
        Open1AgoOpen6Plus,
        Open1AgoOpen7Plus,
        Open1AgoOpen8Plus,
        Open1AgoOpen9Plus,
        Open2AgoOpen1Plus,
        Open2AgoOpen2Plus,
        Open2AgoOpen3Plus,
        Open2AgoOpen4Plus,
        Open2AgoOpen5Plus,
        Open2AgoOpen6Plus,
        Open2AgoOpen7Plus,
        Open2AgoOpen8Plus,
        Open2AgoOpen9Plus,
        Open3AgoOpen1Plus,
        Open3AgoOpen2Plus,
        Open3AgoOpen3Plus,
        Open3AgoOpen4Plus,
        Open3AgoOpen5Plus,
        Open3AgoOpen6Plus,
        Open3AgoOpen7Plus,
        Open3AgoOpen8Plus,
        Open3AgoOpen9Plus,
        Open4AgoOpen1Plus,
        Open4AgoOpen2Plus,
        Open4AgoOpen3Plus,
        Open4AgoOpen4Plus,
        Open4AgoOpen5Plus,
        Open4AgoOpen6Plus,
        Open4AgoOpen7Plus,
        Open4AgoOpen8Plus,
        Open4AgoOpen9Plus,
        Open5AgoOpen1Plus,
        Open5AgoOpen2Plus,
        Open5AgoOpen3Plus,
        Open5AgoOpen4Plus,
        Open5AgoOpen5Plus,
        Open5AgoOpen6Plus,
        Open5AgoOpen7Plus,
        Open5AgoOpen8Plus,
        Open5AgoOpen9Plus,
        Open6AgoOpen1Plus,
        Open6AgoOpen2Plus,
        Open6AgoOpen3Plus,
        Open6AgoOpen4Plus,
        Open6AgoOpen5Plus,
        Open6AgoOpen6Plus,
        Open6AgoOpen7Plus,
        Open6AgoOpen8Plus,
        Open6AgoOpen9Plus,
        Open7AgoOpen1Plus,
        Open7AgoOpen2Plus,
        Open7AgoOpen3Plus,
        Open7AgoOpen4Plus,
        Open7AgoOpen5Plus,
        Open7AgoOpen6Plus,
        Open7AgoOpen7Plus,
        Open7AgoOpen8Plus,
        Open7AgoOpen9Plus,
        Open8AgoOpen1Plus,
        Open8AgoOpen2Plus,
        Open8AgoOpen3Plus,
        Open8AgoOpen4Plus,
        Open8AgoOpen5Plus,
        Open8AgoOpen6Plus,
        Open8AgoOpen7Plus,
        Open8AgoOpen8Plus,
        Open8AgoOpen9Plus,
        Open9AgoOpen1Plus,
        Open9AgoOpen2Plus,
        Open9AgoOpen3Plus,
        Open9AgoOpen4Plus,
        Open9AgoOpen5Plus,
        Open9AgoOpen6Plus,
        Open9AgoOpen7Plus,
        Open9AgoOpen8Plus,
        Open9AgoOpen9Plus,
        Open0AgoLow1Plus,
        Open0AgoLow2Plus,
        Open0AgoLow3Plus,
        Open0AgoLow4Plus,
        Open0AgoLow5Plus,
        Open0AgoLow6Plus,
        Open0AgoLow7Plus,
        Open0AgoLow8Plus,
        Open0AgoLow9Plus,
        Open1AgoLow1Plus,
        Open1AgoLow2Plus,
        Open1AgoLow3Plus,
        Open1AgoLow4Plus,
        Open1AgoLow5Plus,
        Open1AgoLow6Plus,
        Open1AgoLow7Plus,
        Open1AgoLow8Plus,
        Open1AgoLow9Plus,
        Open2AgoLow1Plus,
        Open2AgoLow2Plus,
        Open2AgoLow3Plus,
        Open2AgoLow4Plus,
        Open2AgoLow5Plus,
        Open2AgoLow6Plus,
        Open2AgoLow7Plus,
        Open2AgoLow8Plus,
        Open2AgoLow9Plus,
        Open3AgoLow1Plus,
        Open3AgoLow2Plus,
        Open3AgoLow3Plus,
        Open3AgoLow4Plus,
        Open3AgoLow5Plus,
        Open3AgoLow6Plus,
        Open3AgoLow7Plus,
        Open3AgoLow8Plus,
        Open3AgoLow9Plus,
        Open4AgoLow1Plus,
        Open4AgoLow2Plus,
        Open4AgoLow3Plus,
        Open4AgoLow4Plus,
        Open4AgoLow5Plus,
        Open4AgoLow6Plus,
        Open4AgoLow7Plus,
        Open4AgoLow8Plus,
        Open4AgoLow9Plus,
        Open5AgoLow1Plus,
        Open5AgoLow2Plus,
        Open5AgoLow3Plus,
        Open5AgoLow4Plus,
        Open5AgoLow5Plus,
        Open5AgoLow6Plus,
        Open5AgoLow7Plus,
        Open5AgoLow8Plus,
        Open5AgoLow9Plus,
        Open6AgoLow1Plus,
        Open6AgoLow2Plus,
        Open6AgoLow3Plus,
        Open6AgoLow4Plus,
        Open6AgoLow5Plus,
        Open6AgoLow6Plus,
        Open6AgoLow7Plus,
        Open6AgoLow8Plus,
        Open6AgoLow9Plus,
        Open7AgoLow1Plus,
        Open7AgoLow2Plus,
        Open7AgoLow3Plus,
        Open7AgoLow4Plus,
        Open7AgoLow5Plus,
        Open7AgoLow6Plus,
        Open7AgoLow7Plus,
        Open7AgoLow8Plus,
        Open7AgoLow9Plus,
        Open8AgoLow1Plus,
        Open8AgoLow2Plus,
        Open8AgoLow3Plus,
        Open8AgoLow4Plus,
        Open8AgoLow5Plus,
        Open8AgoLow6Plus,
        Open8AgoLow7Plus,
        Open8AgoLow8Plus,
        Open8AgoLow9Plus,
        Open9AgoLow1Plus,
        Open9AgoLow2Plus,
        Open9AgoLow3Plus,
        Open9AgoLow4Plus,
        Open9AgoLow5Plus,
        Open9AgoLow6Plus,
        Open9AgoLow7Plus,
        Open9AgoLow8Plus,
        Open9AgoLow9Plus,
        Open0AgoHigh1Plus,
        Open0AgoHigh2Plus,
        Open0AgoHigh3Plus,
        Open0AgoHigh4Plus,
        Open0AgoHigh5Plus,
        Open0AgoHigh6Plus,
        Open0AgoHigh7Plus,
        Open0AgoHigh8Plus,
        Open0AgoHigh9Plus,
        Open1AgoHigh1Plus,
        Open1AgoHigh2Plus,
        Open1AgoHigh3Plus,
        Open1AgoHigh4Plus,
        Open1AgoHigh5Plus,
        Open1AgoHigh6Plus,
        Open1AgoHigh7Plus,
        Open1AgoHigh8Plus,
        Open1AgoHigh9Plus,
        Open2AgoHigh1Plus,
        Open2AgoHigh2Plus,
        Open2AgoHigh3Plus,
        Open2AgoHigh4Plus,
        Open2AgoHigh5Plus,
        Open2AgoHigh6Plus,
        Open2AgoHigh7Plus,
        Open2AgoHigh8Plus,
        Open2AgoHigh9Plus,
        Open3AgoHigh1Plus,
        Open3AgoHigh2Plus,
        Open3AgoHigh3Plus,
        Open3AgoHigh4Plus,
        Open3AgoHigh5Plus,
        Open3AgoHigh6Plus,
        Open3AgoHigh7Plus,
        Open3AgoHigh8Plus,
        Open3AgoHigh9Plus,
        Open4AgoHigh1Plus,
        Open4AgoHigh2Plus,
        Open4AgoHigh3Plus,
        Open4AgoHigh4Plus,
        Open4AgoHigh5Plus,
        Open4AgoHigh6Plus,
        Open4AgoHigh7Plus,
        Open4AgoHigh8Plus,
        Open4AgoHigh9Plus,
        Open5AgoHigh1Plus,
        Open5AgoHigh2Plus,
        Open5AgoHigh3Plus,
        Open5AgoHigh4Plus,
        Open5AgoHigh5Plus,
        Open5AgoHigh6Plus,
        Open5AgoHigh7Plus,
        Open5AgoHigh8Plus,
        Open5AgoHigh9Plus,
        Open6AgoHigh1Plus,
        Open6AgoHigh2Plus,
        Open6AgoHigh3Plus,
        Open6AgoHigh4Plus,
        Open6AgoHigh5Plus,
        Open6AgoHigh6Plus,
        Open6AgoHigh7Plus,
        Open6AgoHigh8Plus,
        Open6AgoHigh9Plus,
        Open7AgoHigh1Plus,
        Open7AgoHigh2Plus,
        Open7AgoHigh3Plus,
        Open7AgoHigh4Plus,
        Open7AgoHigh5Plus,
        Open7AgoHigh6Plus,
        Open7AgoHigh7Plus,
        Open7AgoHigh8Plus,
        Open7AgoHigh9Plus,
        Open8AgoHigh1Plus,
        Open8AgoHigh2Plus,
        Open8AgoHigh3Plus,
        Open8AgoHigh4Plus,
        Open8AgoHigh5Plus,
        Open8AgoHigh6Plus,
        Open8AgoHigh7Plus,
        Open8AgoHigh8Plus,
        Open8AgoHigh9Plus,
        Open9AgoHigh1Plus,
        Open9AgoHigh2Plus,
        Open9AgoHigh3Plus,
        Open9AgoHigh4Plus,
        Open9AgoHigh5Plus,
        Open9AgoHigh6Plus,
        Open9AgoHigh7Plus,
        Open9AgoHigh8Plus,
        Open9AgoHigh9Plus,
        Open0AgoClose1Plus,
        Open0AgoClose2Plus,
        Open0AgoClose3Plus,
        Open0AgoClose4Plus,
        Open0AgoClose5Plus,
        Open0AgoClose6Plus,
        Open0AgoClose7Plus,
        Open0AgoClose8Plus,
        Open0AgoClose9Plus,
        Open1AgoClose1Plus,
        Open1AgoClose2Plus,
        Open1AgoClose3Plus,
        Open1AgoClose4Plus,
        Open1AgoClose5Plus,
        Open1AgoClose6Plus,
        Open1AgoClose7Plus,
        Open1AgoClose8Plus,
        Open1AgoClose9Plus,
        Open2AgoClose1Plus,
        Open2AgoClose2Plus,
        Open2AgoClose3Plus,
        Open2AgoClose4Plus,
        Open2AgoClose5Plus,
        Open2AgoClose6Plus,
        Open2AgoClose7Plus,
        Open2AgoClose8Plus,
        Open2AgoClose9Plus,
        Open3AgoClose1Plus,
        Open3AgoClose2Plus,
        Open3AgoClose3Plus,
        Open3AgoClose4Plus,
        Open3AgoClose5Plus,
        Open3AgoClose6Plus,
        Open3AgoClose7Plus,
        Open3AgoClose8Plus,
        Open3AgoClose9Plus,
        Open4AgoClose1Plus,
        Open4AgoClose2Plus,
        Open4AgoClose3Plus,
        Open4AgoClose4Plus,
        Open4AgoClose5Plus,
        Open4AgoClose6Plus,
        Open4AgoClose7Plus,
        Open4AgoClose8Plus,
        Open4AgoClose9Plus,
        Open5AgoClose1Plus,
        Open5AgoClose2Plus,
        Open5AgoClose3Plus,
        Open5AgoClose4Plus,
        Open5AgoClose5Plus,
        Open5AgoClose6Plus,
        Open5AgoClose7Plus,
        Open5AgoClose8Plus,
        Open5AgoClose9Plus,
        Open6AgoClose1Plus,
        Open6AgoClose2Plus,
        Open6AgoClose3Plus,
        Open6AgoClose4Plus,
        Open6AgoClose5Plus,
        Open6AgoClose6Plus,
        Open6AgoClose7Plus,
        Open6AgoClose8Plus,
        Open6AgoClose9Plus,
        Open7AgoClose1Plus,
        Open7AgoClose2Plus,
        Open7AgoClose3Plus,
        Open7AgoClose4Plus,
        Open7AgoClose5Plus,
        Open7AgoClose6Plus,
        Open7AgoClose7Plus,
        Open7AgoClose8Plus,
        Open7AgoClose9Plus,
        Open8AgoClose1Plus,
        Open8AgoClose2Plus,
        Open8AgoClose3Plus,
        Open8AgoClose4Plus,
        Open8AgoClose5Plus,
        Open8AgoClose6Plus,
        Open8AgoClose7Plus,
        Open8AgoClose8Plus,
        Open8AgoClose9Plus,
        Open9AgoClose1Plus,
        Open9AgoClose2Plus,
        Open9AgoClose3Plus,
        Open9AgoClose4Plus,
        Open9AgoClose5Plus,
        Open9AgoClose6Plus,
        Open9AgoClose7Plus,
        Open9AgoClose8Plus,
        Open9AgoClose9Plus,

        // hi to
        High0AgoOpen1Plus,
        High0AgoOpen2Plus,
        High0AgoOpen3Plus,
        High0AgoOpen4Plus,
        High0AgoOpen5Plus,
        High0AgoOpen6Plus,
        High0AgoOpen7Plus,
        High0AgoOpen8Plus,
        High0AgoOpen9Plus,
        High1AgoOpen1Plus,
        High1AgoOpen2Plus,
        High1AgoOpen3Plus,
        High1AgoOpen4Plus,
        High1AgoOpen5Plus,
        High1AgoOpen6Plus,
        High1AgoOpen7Plus,
        High1AgoOpen8Plus,
        High1AgoOpen9Plus,
        High2AgoOpen1Plus,
        High2AgoOpen2Plus,
        High2AgoOpen3Plus,
        High2AgoOpen4Plus,
        High2AgoOpen5Plus,
        High2AgoOpen6Plus,
        High2AgoOpen7Plus,
        High2AgoOpen8Plus,
        High2AgoOpen9Plus,
        High3AgoOpen1Plus,
        High3AgoOpen2Plus,
        High3AgoOpen3Plus,
        High3AgoOpen4Plus,
        High3AgoOpen5Plus,
        High3AgoOpen6Plus,
        High3AgoOpen7Plus,
        High3AgoOpen8Plus,
        High3AgoOpen9Plus,
        High4AgoOpen1Plus,
        High4AgoOpen2Plus,
        High4AgoOpen3Plus,
        High4AgoOpen4Plus,
        High4AgoOpen5Plus,
        High4AgoOpen6Plus,
        High4AgoOpen7Plus,
        High4AgoOpen8Plus,
        High4AgoOpen9Plus,
        High5AgoOpen1Plus,
        High5AgoOpen2Plus,
        High5AgoOpen3Plus,
        High5AgoOpen4Plus,
        High5AgoOpen5Plus,
        High5AgoOpen6Plus,
        High5AgoOpen7Plus,
        High5AgoOpen8Plus,
        High5AgoOpen9Plus,
        High6AgoOpen1Plus,
        High6AgoOpen2Plus,
        High6AgoOpen3Plus,
        High6AgoOpen4Plus,
        High6AgoOpen5Plus,
        High6AgoOpen6Plus,
        High6AgoOpen7Plus,
        High6AgoOpen8Plus,
        High6AgoOpen9Plus,
        High7AgoOpen1Plus,
        High7AgoOpen2Plus,
        High7AgoOpen3Plus,
        High7AgoOpen4Plus,
        High7AgoOpen5Plus,
        High7AgoOpen6Plus,
        High7AgoOpen7Plus,
        High7AgoOpen8Plus,
        High7AgoOpen9Plus,
        High8AgoOpen1Plus,
        High8AgoOpen2Plus,
        High8AgoOpen3Plus,
        High8AgoOpen4Plus,
        High8AgoOpen5Plus,
        High8AgoOpen6Plus,
        High8AgoOpen7Plus,
        High8AgoOpen8Plus,
        High8AgoOpen9Plus,
        High9AgoOpen1Plus,
        High9AgoOpen2Plus,
        High9AgoOpen3Plus,
        High9AgoOpen4Plus,
        High9AgoOpen5Plus,
        High9AgoOpen6Plus,
        High9AgoOpen7Plus,
        High9AgoOpen8Plus,
        High9AgoOpen9Plus,
        High0AgoLow1Plus,
        High0AgoLow2Plus,
        High0AgoLow3Plus,
        High0AgoLow4Plus,
        High0AgoLow5Plus,
        High0AgoLow6Plus,
        High0AgoLow7Plus,
        High0AgoLow8Plus,
        High0AgoLow9Plus,
        High1AgoLow1Plus,
        High1AgoLow2Plus,
        High1AgoLow3Plus,
        High1AgoLow4Plus,
        High1AgoLow5Plus,
        High1AgoLow6Plus,
        High1AgoLow7Plus,
        High1AgoLow8Plus,
        High1AgoLow9Plus,
        High2AgoLow1Plus,
        High2AgoLow2Plus,
        High2AgoLow3Plus,
        High2AgoLow4Plus,
        High2AgoLow5Plus,
        High2AgoLow6Plus,
        High2AgoLow7Plus,
        High2AgoLow8Plus,
        High2AgoLow9Plus,
        High3AgoLow1Plus,
        High3AgoLow2Plus,
        High3AgoLow3Plus,
        High3AgoLow4Plus,
        High3AgoLow5Plus,
        High3AgoLow6Plus,
        High3AgoLow7Plus,
        High3AgoLow8Plus,
        High3AgoLow9Plus,
        High4AgoLow1Plus,
        High4AgoLow2Plus,
        High4AgoLow3Plus,
        High4AgoLow4Plus,
        High4AgoLow5Plus,
        High4AgoLow6Plus,
        High4AgoLow7Plus,
        High4AgoLow8Plus,
        High4AgoLow9Plus,
        High5AgoLow1Plus,
        High5AgoLow2Plus,
        High5AgoLow3Plus,
        High5AgoLow4Plus,
        High5AgoLow5Plus,
        High5AgoLow6Plus,
        High5AgoLow7Plus,
        High5AgoLow8Plus,
        High5AgoLow9Plus,
        High6AgoLow1Plus,
        High6AgoLow2Plus,
        High6AgoLow3Plus,
        High6AgoLow4Plus,
        High6AgoLow5Plus,
        High6AgoLow6Plus,
        High6AgoLow7Plus,
        High6AgoLow8Plus,
        High6AgoLow9Plus,
        High7AgoLow1Plus,
        High7AgoLow2Plus,
        High7AgoLow3Plus,
        High7AgoLow4Plus,
        High7AgoLow5Plus,
        High7AgoLow6Plus,
        High7AgoLow7Plus,
        High7AgoLow8Plus,
        High7AgoLow9Plus,
        High8AgoLow1Plus,
        High8AgoLow2Plus,
        High8AgoLow3Plus,
        High8AgoLow4Plus,
        High8AgoLow5Plus,
        High8AgoLow6Plus,
        High8AgoLow7Plus,
        High8AgoLow8Plus,
        High8AgoLow9Plus,
        High9AgoLow1Plus,
        High9AgoLow2Plus,
        High9AgoLow3Plus,
        High9AgoLow4Plus,
        High9AgoLow5Plus,
        High9AgoLow6Plus,
        High9AgoLow7Plus,
        High9AgoLow8Plus,
        High9AgoLow9Plus,
        High0AgoHigh1Plus,
        High0AgoHigh2Plus,
        High0AgoHigh3Plus,
        High0AgoHigh4Plus,
        High0AgoHigh5Plus,
        High0AgoHigh6Plus,
        High0AgoHigh7Plus,
        High0AgoHigh8Plus,
        High0AgoHigh9Plus,
        High1AgoHigh1Plus,
        High1AgoHigh2Plus,
        High1AgoHigh3Plus,
        High1AgoHigh4Plus,
        High1AgoHigh5Plus,
        High1AgoHigh6Plus,
        High1AgoHigh7Plus,
        High1AgoHigh8Plus,
        High1AgoHigh9Plus,
        High2AgoHigh1Plus,
        High2AgoHigh2Plus,
        High2AgoHigh3Plus,
        High2AgoHigh4Plus,
        High2AgoHigh5Plus,
        High2AgoHigh6Plus,
        High2AgoHigh7Plus,
        High2AgoHigh8Plus,
        High2AgoHigh9Plus,
        High3AgoHigh1Plus,
        High3AgoHigh2Plus,
        High3AgoHigh3Plus,
        High3AgoHigh4Plus,
        High3AgoHigh5Plus,
        High3AgoHigh6Plus,
        High3AgoHigh7Plus,
        High3AgoHigh8Plus,
        High3AgoHigh9Plus,
        High4AgoHigh1Plus,
        High4AgoHigh2Plus,
        High4AgoHigh3Plus,
        High4AgoHigh4Plus,
        High4AgoHigh5Plus,
        High4AgoHigh6Plus,
        High4AgoHigh7Plus,
        High4AgoHigh8Plus,
        High4AgoHigh9Plus,
        High5AgoHigh1Plus,
        High5AgoHigh2Plus,
        High5AgoHigh3Plus,
        High5AgoHigh4Plus,
        High5AgoHigh5Plus,
        High5AgoHigh6Plus,
        High5AgoHigh7Plus,
        High5AgoHigh8Plus,
        High5AgoHigh9Plus,
        High6AgoHigh1Plus,
        High6AgoHigh2Plus,
        High6AgoHigh3Plus,
        High6AgoHigh4Plus,
        High6AgoHigh5Plus,
        High6AgoHigh6Plus,
        High6AgoHigh7Plus,
        High6AgoHigh8Plus,
        High6AgoHigh9Plus,
        High7AgoHigh1Plus,
        High7AgoHigh2Plus,
        High7AgoHigh3Plus,
        High7AgoHigh4Plus,
        High7AgoHigh5Plus,
        High7AgoHigh6Plus,
        High7AgoHigh7Plus,
        High7AgoHigh8Plus,
        High7AgoHigh9Plus,
        High8AgoHigh1Plus,
        High8AgoHigh2Plus,
        High8AgoHigh3Plus,
        High8AgoHigh4Plus,
        High8AgoHigh5Plus,
        High8AgoHigh6Plus,
        High8AgoHigh7Plus,
        High8AgoHigh8Plus,
        High8AgoHigh9Plus,
        High9AgoHigh1Plus,
        High9AgoHigh2Plus,
        High9AgoHigh3Plus,
        High9AgoHigh4Plus,
        High9AgoHigh5Plus,
        High9AgoHigh6Plus,
        High9AgoHigh7Plus,
        High9AgoHigh8Plus,
        High9AgoHigh9Plus,
        High0AgoClose1Plus,
        High0AgoClose2Plus,
        High0AgoClose3Plus,
        High0AgoClose4Plus,
        High0AgoClose5Plus,
        High0AgoClose6Plus,
        High0AgoClose7Plus,
        High0AgoClose8Plus,
        High0AgoClose9Plus,
        High1AgoClose1Plus,
        High1AgoClose2Plus,
        High1AgoClose3Plus,
        High1AgoClose4Plus,
        High1AgoClose5Plus,
        High1AgoClose6Plus,
        High1AgoClose7Plus,
        High1AgoClose8Plus,
        High1AgoClose9Plus,
        High2AgoClose1Plus,
        High2AgoClose2Plus,
        High2AgoClose3Plus,
        High2AgoClose4Plus,
        High2AgoClose5Plus,
        High2AgoClose6Plus,
        High2AgoClose7Plus,
        High2AgoClose8Plus,
        High2AgoClose9Plus,
        High3AgoClose1Plus,
        High3AgoClose2Plus,
        High3AgoClose3Plus,
        High3AgoClose4Plus,
        High3AgoClose5Plus,
        High3AgoClose6Plus,
        High3AgoClose7Plus,
        High3AgoClose8Plus,
        High3AgoClose9Plus,
        High4AgoClose1Plus,
        High4AgoClose2Plus,
        High4AgoClose3Plus,
        High4AgoClose4Plus,
        High4AgoClose5Plus,
        High4AgoClose6Plus,
        High4AgoClose7Plus,
        High4AgoClose8Plus,
        High4AgoClose9Plus,
        High5AgoClose1Plus,
        High5AgoClose2Plus,
        High5AgoClose3Plus,
        High5AgoClose4Plus,
        High5AgoClose5Plus,
        High5AgoClose6Plus,
        High5AgoClose7Plus,
        High5AgoClose8Plus,
        High5AgoClose9Plus,
        High6AgoClose1Plus,
        High6AgoClose2Plus,
        High6AgoClose3Plus,
        High6AgoClose4Plus,
        High6AgoClose5Plus,
        High6AgoClose6Plus,
        High6AgoClose7Plus,
        High6AgoClose8Plus,
        High6AgoClose9Plus,
        High7AgoClose1Plus,
        High7AgoClose2Plus,
        High7AgoClose3Plus,
        High7AgoClose4Plus,
        High7AgoClose5Plus,
        High7AgoClose6Plus,
        High7AgoClose7Plus,
        High7AgoClose8Plus,
        High7AgoClose9Plus,
        High8AgoClose1Plus,
        High8AgoClose2Plus,
        High8AgoClose3Plus,
        High8AgoClose4Plus,
        High8AgoClose5Plus,
        High8AgoClose6Plus,
        High8AgoClose7Plus,
        High8AgoClose8Plus,
        High8AgoClose9Plus,
        High9AgoClose1Plus,
        High9AgoClose2Plus,
        High9AgoClose3Plus,
        High9AgoClose4Plus,
        High9AgoClose5Plus,
        High9AgoClose6Plus,
        High9AgoClose7Plus,
        High9AgoClose8Plus,
        High9AgoClose9Plus,

        // lo to
        Low0AgoOpen1Plus,
        Low0AgoOpen2Plus,
        Low0AgoOpen3Plus,
        Low0AgoOpen4Plus,
        Low0AgoOpen5Plus,
        Low0AgoOpen6Plus,
        Low0AgoOpen7Plus,
        Low0AgoOpen8Plus,
        Low0AgoOpen9Plus,
        Low1AgoOpen1Plus,
        Low1AgoOpen2Plus,
        Low1AgoOpen3Plus,
        Low1AgoOpen4Plus,
        Low1AgoOpen5Plus,
        Low1AgoOpen6Plus,
        Low1AgoOpen7Plus,
        Low1AgoOpen8Plus,
        Low1AgoOpen9Plus,
        Low2AgoOpen1Plus,
        Low2AgoOpen2Plus,
        Low2AgoOpen3Plus,
        Low2AgoOpen4Plus,
        Low2AgoOpen5Plus,
        Low2AgoOpen6Plus,
        Low2AgoOpen7Plus,
        Low2AgoOpen8Plus,
        Low2AgoOpen9Plus,
        Low3AgoOpen1Plus,
        Low3AgoOpen2Plus,
        Low3AgoOpen3Plus,
        Low3AgoOpen4Plus,
        Low3AgoOpen5Plus,
        Low3AgoOpen6Plus,
        Low3AgoOpen7Plus,
        Low3AgoOpen8Plus,
        Low3AgoOpen9Plus,
        Low4AgoOpen1Plus,
        Low4AgoOpen2Plus,
        Low4AgoOpen3Plus,
        Low4AgoOpen4Plus,
        Low4AgoOpen5Plus,
        Low4AgoOpen6Plus,
        Low4AgoOpen7Plus,
        Low4AgoOpen8Plus,
        Low4AgoOpen9Plus,
        Low5AgoOpen1Plus,
        Low5AgoOpen2Plus,
        Low5AgoOpen3Plus,
        Low5AgoOpen4Plus,
        Low5AgoOpen5Plus,
        Low5AgoOpen6Plus,
        Low5AgoOpen7Plus,
        Low5AgoOpen8Plus,
        Low5AgoOpen9Plus,
        Low6AgoOpen1Plus,
        Low6AgoOpen2Plus,
        Low6AgoOpen3Plus,
        Low6AgoOpen4Plus,
        Low6AgoOpen5Plus,
        Low6AgoOpen6Plus,
        Low6AgoOpen7Plus,
        Low6AgoOpen8Plus,
        Low6AgoOpen9Plus,
        Low7AgoOpen1Plus,
        Low7AgoOpen2Plus,
        Low7AgoOpen3Plus,
        Low7AgoOpen4Plus,
        Low7AgoOpen5Plus,
        Low7AgoOpen6Plus,
        Low7AgoOpen7Plus,
        Low7AgoOpen8Plus,
        Low7AgoOpen9Plus,
        Low8AgoOpen1Plus,
        Low8AgoOpen2Plus,
        Low8AgoOpen3Plus,
        Low8AgoOpen4Plus,
        Low8AgoOpen5Plus,
        Low8AgoOpen6Plus,
        Low8AgoOpen7Plus,
        Low8AgoOpen8Plus,
        Low8AgoOpen9Plus,
        Low9AgoOpen1Plus,
        Low9AgoOpen2Plus,
        Low9AgoOpen3Plus,
        Low9AgoOpen4Plus,
        Low9AgoOpen5Plus,
        Low9AgoOpen6Plus,
        Low9AgoOpen7Plus,
        Low9AgoOpen8Plus,
        Low9AgoOpen9Plus,
        Low0AgoLow1Plus,
        Low0AgoLow2Plus,
        Low0AgoLow3Plus,
        Low0AgoLow4Plus,
        Low0AgoLow5Plus,
        Low0AgoLow6Plus,
        Low0AgoLow7Plus,
        Low0AgoLow8Plus,
        Low0AgoLow9Plus,
        Low1AgoLow1Plus,
        Low1AgoLow2Plus,
        Low1AgoLow3Plus,
        Low1AgoLow4Plus,
        Low1AgoLow5Plus,
        Low1AgoLow6Plus,
        Low1AgoLow7Plus,
        Low1AgoLow8Plus,
        Low1AgoLow9Plus,
        Low2AgoLow1Plus,
        Low2AgoLow2Plus,
        Low2AgoLow3Plus,
        Low2AgoLow4Plus,
        Low2AgoLow5Plus,
        Low2AgoLow6Plus,
        Low2AgoLow7Plus,
        Low2AgoLow8Plus,
        Low2AgoLow9Plus,
        Low3AgoLow1Plus,
        Low3AgoLow2Plus,
        Low3AgoLow3Plus,
        Low3AgoLow4Plus,
        Low3AgoLow5Plus,
        Low3AgoLow6Plus,
        Low3AgoLow7Plus,
        Low3AgoLow8Plus,
        Low3AgoLow9Plus,
        Low4AgoLow1Plus,
        Low4AgoLow2Plus,
        Low4AgoLow3Plus,
        Low4AgoLow4Plus,
        Low4AgoLow5Plus,
        Low4AgoLow6Plus,
        Low4AgoLow7Plus,
        Low4AgoLow8Plus,
        Low4AgoLow9Plus,
        Low5AgoLow1Plus,
        Low5AgoLow2Plus,
        Low5AgoLow3Plus,
        Low5AgoLow4Plus,
        Low5AgoLow5Plus,
        Low5AgoLow6Plus,
        Low5AgoLow7Plus,
        Low5AgoLow8Plus,
        Low5AgoLow9Plus,
        Low6AgoLow1Plus,
        Low6AgoLow2Plus,
        Low6AgoLow3Plus,
        Low6AgoLow4Plus,
        Low6AgoLow5Plus,
        Low6AgoLow6Plus,
        Low6AgoLow7Plus,
        Low6AgoLow8Plus,
        Low6AgoLow9Plus,
        Low7AgoLow1Plus,
        Low7AgoLow2Plus,
        Low7AgoLow3Plus,
        Low7AgoLow4Plus,
        Low7AgoLow5Plus,
        Low7AgoLow6Plus,
        Low7AgoLow7Plus,
        Low7AgoLow8Plus,
        Low7AgoLow9Plus,
        Low8AgoLow1Plus,
        Low8AgoLow2Plus,
        Low8AgoLow3Plus,
        Low8AgoLow4Plus,
        Low8AgoLow5Plus,
        Low8AgoLow6Plus,
        Low8AgoLow7Plus,
        Low8AgoLow8Plus,
        Low8AgoLow9Plus,
        Low9AgoLow1Plus,
        Low9AgoLow2Plus,
        Low9AgoLow3Plus,
        Low9AgoLow4Plus,
        Low9AgoLow5Plus,
        Low9AgoLow6Plus,
        Low9AgoLow7Plus,
        Low9AgoLow8Plus,
        Low9AgoLow9Plus,
        Low0AgoHigh1Plus,
        Low0AgoHigh2Plus,
        Low0AgoHigh3Plus,
        Low0AgoHigh4Plus,
        Low0AgoHigh5Plus,
        Low0AgoHigh6Plus,
        Low0AgoHigh7Plus,
        Low0AgoHigh8Plus,
        Low0AgoHigh9Plus,
        Low1AgoHigh1Plus,
        Low1AgoHigh2Plus,
        Low1AgoHigh3Plus,
        Low1AgoHigh4Plus,
        Low1AgoHigh5Plus,
        Low1AgoHigh6Plus,
        Low1AgoHigh7Plus,
        Low1AgoHigh8Plus,
        Low1AgoHigh9Plus,
        Low2AgoHigh1Plus,
        Low2AgoHigh2Plus,
        Low2AgoHigh3Plus,
        Low2AgoHigh4Plus,
        Low2AgoHigh5Plus,
        Low2AgoHigh6Plus,
        Low2AgoHigh7Plus,
        Low2AgoHigh8Plus,
        Low2AgoHigh9Plus,
        Low3AgoHigh1Plus,
        Low3AgoHigh2Plus,
        Low3AgoHigh3Plus,
        Low3AgoHigh4Plus,
        Low3AgoHigh5Plus,
        Low3AgoHigh6Plus,
        Low3AgoHigh7Plus,
        Low3AgoHigh8Plus,
        Low3AgoHigh9Plus,
        Low4AgoHigh1Plus,
        Low4AgoHigh2Plus,
        Low4AgoHigh3Plus,
        Low4AgoHigh4Plus,
        Low4AgoHigh5Plus,
        Low4AgoHigh6Plus,
        Low4AgoHigh7Plus,
        Low4AgoHigh8Plus,
        Low4AgoHigh9Plus,
        Low5AgoHigh1Plus,
        Low5AgoHigh2Plus,
        Low5AgoHigh3Plus,
        Low5AgoHigh4Plus,
        Low5AgoHigh5Plus,
        Low5AgoHigh6Plus,
        Low5AgoHigh7Plus,
        Low5AgoHigh8Plus,
        Low5AgoHigh9Plus,
        Low6AgoHigh1Plus,
        Low6AgoHigh2Plus,
        Low6AgoHigh3Plus,
        Low6AgoHigh4Plus,
        Low6AgoHigh5Plus,
        Low6AgoHigh6Plus,
        Low6AgoHigh7Plus,
        Low6AgoHigh8Plus,
        Low6AgoHigh9Plus,
        Low7AgoHigh1Plus,
        Low7AgoHigh2Plus,
        Low7AgoHigh3Plus,
        Low7AgoHigh4Plus,
        Low7AgoHigh5Plus,
        Low7AgoHigh6Plus,
        Low7AgoHigh7Plus,
        Low7AgoHigh8Plus,
        Low7AgoHigh9Plus,
        Low8AgoHigh1Plus,
        Low8AgoHigh2Plus,
        Low8AgoHigh3Plus,
        Low8AgoHigh4Plus,
        Low8AgoHigh5Plus,
        Low8AgoHigh6Plus,
        Low8AgoHigh7Plus,
        Low8AgoHigh8Plus,
        Low8AgoHigh9Plus,
        Low9AgoHigh1Plus,
        Low9AgoHigh2Plus,
        Low9AgoHigh3Plus,
        Low9AgoHigh4Plus,
        Low9AgoHigh5Plus,
        Low9AgoHigh6Plus,
        Low9AgoHigh7Plus,
        Low9AgoHigh8Plus,
        Low9AgoHigh9Plus,
        Low0AgoClose1Plus,
        Low0AgoClose2Plus,
        Low0AgoClose3Plus,
        Low0AgoClose4Plus,
        Low0AgoClose5Plus,
        Low0AgoClose6Plus,
        Low0AgoClose7Plus,
        Low0AgoClose8Plus,
        Low0AgoClose9Plus,
        Low1AgoClose1Plus,
        Low1AgoClose2Plus,
        Low1AgoClose3Plus,
        Low1AgoClose4Plus,
        Low1AgoClose5Plus,
        Low1AgoClose6Plus,
        Low1AgoClose7Plus,
        Low1AgoClose8Plus,
        Low1AgoClose9Plus,
        Low2AgoClose1Plus,
        Low2AgoClose2Plus,
        Low2AgoClose3Plus,
        Low2AgoClose4Plus,
        Low2AgoClose5Plus,
        Low2AgoClose6Plus,
        Low2AgoClose7Plus,
        Low2AgoClose8Plus,
        Low2AgoClose9Plus,
        Low3AgoClose1Plus,
        Low3AgoClose2Plus,
        Low3AgoClose3Plus,
        Low3AgoClose4Plus,
        Low3AgoClose5Plus,
        Low3AgoClose6Plus,
        Low3AgoClose7Plus,
        Low3AgoClose8Plus,
        Low3AgoClose9Plus,
        Low4AgoClose1Plus,
        Low4AgoClose2Plus,
        Low4AgoClose3Plus,
        Low4AgoClose4Plus,
        Low4AgoClose5Plus,
        Low4AgoClose6Plus,
        Low4AgoClose7Plus,
        Low4AgoClose8Plus,
        Low4AgoClose9Plus,
        Low5AgoClose1Plus,
        Low5AgoClose2Plus,
        Low5AgoClose3Plus,
        Low5AgoClose4Plus,
        Low5AgoClose5Plus,
        Low5AgoClose6Plus,
        Low5AgoClose7Plus,
        Low5AgoClose8Plus,
        Low5AgoClose9Plus,
        Low6AgoClose1Plus,
        Low6AgoClose2Plus,
        Low6AgoClose3Plus,
        Low6AgoClose4Plus,
        Low6AgoClose5Plus,
        Low6AgoClose6Plus,
        Low6AgoClose7Plus,
        Low6AgoClose8Plus,
        Low6AgoClose9Plus,
        Low7AgoClose1Plus,
        Low7AgoClose2Plus,
        Low7AgoClose3Plus,
        Low7AgoClose4Plus,
        Low7AgoClose5Plus,
        Low7AgoClose6Plus,
        Low7AgoClose7Plus,
        Low7AgoClose8Plus,
        Low7AgoClose9Plus,
        Low8AgoClose1Plus,
        Low8AgoClose2Plus,
        Low8AgoClose3Plus,
        Low8AgoClose4Plus,
        Low8AgoClose5Plus,
        Low8AgoClose6Plus,
        Low8AgoClose7Plus,
        Low8AgoClose8Plus,
        Low8AgoClose9Plus,
        Low9AgoClose1Plus,
        Low9AgoClose2Plus,
        Low9AgoClose3Plus,
        Low9AgoClose4Plus,
        Low9AgoClose5Plus,
        Low9AgoClose6Plus,
        Low9AgoClose7Plus,
        Low9AgoClose8Plus,
        Low9AgoClose9Plus,

        ATR50Less5,
        ATR505to1,
        ATR501to15,
        ATR50Greater15,

        ATR30Less5,
        ATR305to1,
        ATR301to15,
        ATR30Greater15,

        ATR20Less5,
        ATR205to1,
        ATR201to15,
        ATR20Greater15,

        ATR10Less5,
        ATR105to1,
        ATR101to15,
        ATR10Greater15,

        ATR5Less5,
        ATR55to1,
        ATR51to15,
        ATR5Greater15,

        TrendOp11,
        TrendCl11,
        TrendOpCl11,

        TrendOp,
        TrendCl,
        TrendOpCl,

        TrendOp31,
        TrendCl31,
        TrendOpCl31,

        Volatility
    }
    
    public class atm
    {
        public static bool isYieldTicker(string ticker)
        {
            var m1 = new System.Text.RegularExpressions.Regex("[A-Z]+[0-9]+YR Index").IsMatch(ticker);
            var m2 = new System.Text.RegularExpressions.Regex("[A-Z]+[0-9]+M Index").IsMatch(ticker);
            var m3 = new System.Text.RegularExpressions.Regex("[A-Z]+[0-9]+T Index").IsMatch(ticker);
            var m4 = new System.Text.RegularExpressions.Regex("[A-Z]+[0-9]+BVLI Index").IsMatch(ticker);
            var m5 = new System.Text.RegularExpressions.Regex("[A-Z]+AVG Index").IsMatch(ticker);
            var m6 = new System.Text.RegularExpressions.Regex("[A-Z]+[0-9]+ARM Index").IsMatch(ticker);
            var m7 = new System.Text.RegularExpressions.Regex("[A-Z]+[0-9]+FHA Index").IsMatch(ticker);
            var m8 = new System.Text.RegularExpressions.Regex("MB30 Index").IsMatch(ticker); 
            var m9 = new System.Text.RegularExpressions.Regex("[A-Z]+[0-9]+Y Index").IsMatch(ticker);
            return m1 || m2 || m3 || m4 || m5 || m6 || m7 || m8 || m9;
        }

        public static List<Tuple<String, Color>> getAdvice(string ticker, Dictionary<string, List<DateTime>> times, Dictionary<string,
            Series[]> bars, string[] intervalList, Dictionary<string, object> referenceData, Dictionary<string, bool> enbs, int idx)
        {
            var output = new List<Tuple<String, Color>>();

            var rev = atm.isYieldTicker(ticker);

            var add1Enb = enbs.ContainsKey("Pressure") ? enbs["Pressure"] : true;
            var add2Enb = enbs.ContainsKey("Add") ? enbs["Add"] : true; 
            var add3Enb = enbs.ContainsKey("2 Bar") ? enbs["2 Bar"] : true;
            var exhEnb = enbs.ContainsKey("Exh") ? enbs["Exh"] : true;
            var redEnb = enbs.ContainsKey("Retrace") ? enbs["Retrace"] : true;

            var interval = intervalList[0];

            var op = bars[interval][0];
            var hi = bars[interval][1];
            var lo = bars[interval][2];
            var cl = bars[interval][3];

            var ft = atm.calculateFT(hi, lo, cl);
            var st = atm.calculateST(hi, lo, cl);
            var sc = atm.getScore(times, bars, intervalList);
            var exh = exhEnb ? atm.calculateExhaustion(hi, lo, cl, atm.ExhaustionLevelSelection.AllLevels) : new Series(cl.Count, 0);
            var rp = atm.calculateRelativePrice(interval, bars[interval], referenceData, 5);
            var scSig = atm.calculateSCSig(sc, rp, 2);
            var ft_tp_st = atm.calculateFastTurningPoints(hi, lo, cl, ft, idx);
            var ftp_st_up = ft_tp_st[0];
            var ftp_st_dn = ft_tp_st[1];

            var a1 = atm.calculatePressureAlert(op, hi, lo, cl);
            var a2 = ft.TurnsUp() - ft.TurnsDown();
            var a3 = atm.calculateTwoBarPattern(op, hi, lo, cl, 0);

            var pt = add1Enb ? a1 : new Series(cl.Count, 0);
            var ftTurn = add2Enb ?  a2 : new Series(cl.Count, 0);
            var twobar = add3Enb ? a3 : new Series(cl.Count, 0);

            Series EZI = atm.calculateEZI(cl);
            Series TSB = atm.calculateTSB(hi, lo, cl);
            Series Utsb = (EZI >= 80).And(TSB >= 70);
            Series Dtsb = (EZI <= 20).And(TSB <= 30);
            Series Uezi = (EZI <= 80).And(TSB >= 70);
            Series Dezi = (EZI >= 20).And(TSB <= 30);

            var ftUp = ft.IsRising()[idx] > 0;
            var ftDn = ft.IsFalling()[idx] > 0;
            var ftUp1Ago = ft.IsRising()[idx - 1] > 0;
            var ftDn1Ago = ft.IsFalling()[idx - 1] > 0;
            var stGoingUp = st.IsRising()[idx] > 0;
            var stGoingDn = st.IsFalling()[idx] > 0;
            var stStrong = st[idx] > 75;
            var stWeak = st[idx] < 25;

            var tpup1 = ftp_st_up[idx]; // (double.IsNaN(ftp_st_up[idx]) && ftUp && ftDn1Ago) ? ftp_st_up[idx - 1] : ftp_st_up[idx];
            var tpdn1 = ftp_st_dn[idx]; // (double.IsNaN(ftp_st_dn[idx]) && ftDn && ftUp1Ago) ? ftp_st_dn[idx - 1] : ftp_st_dn[idx];
            var tpup2 = ftp_st_up[idx + 1]; // (double.IsNaN(ftp_st_up[idx + 1]) && ftDn) ? ftp_st_up[idx] : ftp_st_up[idx + 1];
            var tpdn2 = ftp_st_dn[idx + 1]; // (double.IsNaN(ftp_st_dn[idx + 1]) && ftUp) ? ftp_st_dn[idx] : ftp_st_dn[idx + 1];

            Series trend = scSig;

            var lretrace = redEnb ? atm.setReset(trend > 0 & a2 < 0, trend < 0 | a1.ShiftRight(1) > 0 | a2 > 0 | a3.ShiftRight(1) > 0) : new Series(cl.Count, 0);
            var sretrace = redEnb ? atm.setReset(trend < 0 & a2 > 0, trend > 0 | a1.ShiftRight(1) < 0 | a2 < 0 | a3.ShiftRight(1) < 0) : new Series(cl.Count, 0);
            var retrace = lretrace - sretrace;

            List<double> dir = getDirection(trend, ftTurn, pt, twobar, exh, new Series(cl.Count, 0), enbs);


            var lexhaustion = exhEnb ? atm.setReset(trend > 0 & exh < 0, trend < 0 | a1.ShiftRight(1) > 0 | a2 > 0 | a3.ShiftRight(1) > 0) : new Series(cl.Count, 0);
            var sexhaustion = exhEnb ? atm.setReset(trend < 0 & exh > 0, trend > 0 | a1.ShiftRight(1) < 0 | a2 < 0 | a3.ShiftRight(1) < 0) : new Series(cl.Count, 0);

            Color trendColor = Colors.Transparent;
            Color positionColor = Colors.White;
            Color currentColor = Colors.White;
            Color nextColor = Colors.Yellow;
            Color trendChangeColor = Colors.Yellow;

            var pctForTp = 0.25; // 25 percent

            // inputs
            var bullish = trend[idx] > 0;
            var bearish = trend[idx] < 0;
            var newLong = idx > 0 && dir[idx - 1] <= 0 && dir[idx] > 0;
            var newShort = idx > 0 && dir[idx - 1] >= 0 && dir[idx] < 0;
            var inLong = dir[idx] > 0;
            var inShort = dir[idx] < 0;
            var hasTpUp1 = !double.IsNaN(tpup1) && ((tpup1 - cl[idx]) / cl[idx]) <= pctForTp;
            var hasTpDn1 = !double.IsNaN(tpdn1) && ((cl[idx] - tpdn1) / cl[idx]) <= pctForTp;
            var hasTpUp2 = !double.IsNaN(tpup2) && ((tpup2 - cl[idx]) / cl[idx]) <= pctForTp;
            var hasTpDn2 = !double.IsNaN(tpdn2) && ((cl[idx] - tpdn2) / cl[idx]) <= pctForTp;
            var l0 = trend[idx] == 1 && trend[idx - 1] != 1;
            var l1 = pt[idx] == 1;
            var l2 = ftTurn[idx] == 1;
            var l3 = twobar[idx] == 1;
            var s0 = trend[idx] == -1 && trend[idx - 1] != -1;
            var s1 = pt[idx] == -1;
            var s2 = ftTurn[idx] == -1;
            var s3 = twobar[idx] == -1;
            var exhUp = exh[idx] > 0;
            var exhDn = exh[idx] < 0;
            var lRetrace = lretrace[idx] > 0;
            var sRetrace = sretrace[idx] > 0;
            var lretraceStart = lretrace.IsRising()[idx] > 0;
            var lretraceEnd = lretrace.IsFalling()[idx] > 0;
            var sretraceStart = sretrace.IsRising()[idx] > 0;
            var sretraceEnd = sretrace.IsFalling()[idx] > 0;
            var lExhaustion = lexhaustion[idx] > 0;
            var sExhaustion = sexhaustion[idx] > 0;
            var midScore = sc[idx] > 45 && sc[idx] < 55;
            var ftOS = ft[idx] < 30;
            var ftOB = ft[idx] > 70;
            var upClose = cl[idx] > cl[idx - 1];
            var upClose1Ago = cl[idx - 1] > cl[idx - 2];
            var dnClose = cl[idx] < cl[idx - 1];
            var dnClose1Ago = cl[idx - 1] < cl[idx - 2];

            var aoaUpSetup1 = hasTpUp1 && ftTurn.Since(1)[idx] >= 5 && !l1 && !l2 && !l3;
            var aoaDnSetup1 = hasTpDn1 && ftTurn.Since(-1)[idx] >= 5 && !s1 && !s2 && !s3;
            var aoaUpSetup2 = hasTpUp2 && ftTurn.Since(1)[idx] >= 5;
            var aoaDnSetup2 = hasTpDn2 && ftTurn.Since(-1)[idx] >= 5;
            var pAlertUpSetup = ft[idx] < 25 && pt.Since(1)[idx] >= 5 && !l1 && !l2 && !l3;
            var pAlertDnSetup = ft[idx] > 75 && pt.Since(-1)[idx] >= 5 && !s1 && !s2 && !s3;
            var tbu1Setup = ftDn && upClose && twobar.Since(1)[idx] >= 4 && !l1 && !l2 && !l3;
            var tbd1Setup = ftUp && dnClose && twobar.Since(-1)[idx] >= 4 && !s1 && !s2 && !s3;
            var tbuSetup = ftDn && ftDn1Ago && op[idx - 1] < cl[idx - 1] && op[idx] > op[idx - 1] && op[idx] < cl[idx];
            var tbdSetup = ftUp && ftUp1Ago && op[idx - 1] > cl[idx - 1] && op[idx] < op[idx - 1] && op[idx] > cl[idx];
            var exhUpSetup = (Utsb[idx] > 0 || Uezi[idx] > 0) && ft[idx] > 70 && st[idx] > 65 && ftUp;
            var exhDnSetup = (Dtsb[idx] > 0 || Dezi[idx] > 0) && ft[idx] < 30 && st[idx] < 35 && ftDn;
            var stPressureUp = (st[idx] > 25 && stGoingUp) || stStrong;
            var stPressureDn = (st[idx] < 75 && stGoingDn) || stWeak;

            string trendMessage = (bearish && !rev || bullish && rev) ? "Bearish\u00A0\u00A0" : "Bullish\u00A0\u00A0";
            string positionMessage = "";
            string currentMessage = "";
            string nextMessage = "";

            var pressureMessage = rev ? (inLong && stPressureDn) ? "\u00A0\u00A0Caution: Px Pressure is Down" : (inShort && stPressureUp) ? "\u00A0\u00A0Caution: Px Pressure is Up" : "" :
                                        (inLong && stPressureDn) ? "\u00A0\u00A0Caution: Px Pressure is Down" : (inShort && stPressureUp) ? "\u00A0\u00A0Caution: Px Pressure is Up" : "";

            var trendChangeMessageDn = rev ? (bullish && midScore) ? "\u00A0\u00A0Potential Trend Chg if Next " + interval + " Score < 45" : "" :
                                           (bearish && midScore) ? "\u00A0\u00A0Potential Trend Chg if Next " + interval + " Score > 55" : "";
            var trendChangeMessageUp = rev ? (bearish && midScore) ? "\u00A0\u00A0Potential Trend Chg if Next " + interval + " Score > 55" : "" :
                                           (bullish && midScore) ? "\u00A0\u00A0Potential Trend Chg if Next " + interval + " Score < 45" : "";
            var trendChangeMessage = trendChangeMessageUp + trendChangeMessageDn;

            // ADD1
            var setupAdd1u1 = add1Enb && bullish && /* inLong  && */ pAlertUpSetup;
            var setupAdd1d1 = add1Enb && bearish && /* inShort && */ pAlertDnSetup;
            var eventAdd1u1 = add1Enb && bullish && inLong  && l1;
            var eventAdd1d1 = add1Enb && bearish && inShort && s1;

            // ADD1+
            var setupAdd1u2 = add1Enb && bullish && /* inLong  && */ pAlertUpSetup && !l1;
            var setupAdd1d2 = add1Enb && bearish && /* inShort && */ pAlertDnSetup && !s1;

            // ADD2
            var setupAdd2u1 = add2Enb && bullish && /*inLong  && */ hasTpUp1 && aoaUpSetup1;
            var setupAdd2d1 = add2Enb && bearish && /*inShort && */hasTpDn1 && aoaDnSetup1;
            var eventAdd2u1 = add2Enb && bullish && inLong  && l2;
            var eventAdd2d1 = add2Enb && bearish && inShort && s2;

            // ADD2+
            var setupAdd2u2 = add2Enb && bullish && /*inLong  && */ hasTpUp2 && aoaUpSetup2 && !l2;
            var setupAdd2d2 = add2Enb && bearish && /*inShort && */ hasTpDn2 && aoaDnSetup2 && !s2;

            // ADD3
            var setupAdd3u1 = add3Enb && bullish && /* inLong  && */ tbuSetup;
            var setupAdd3d1 = add3Enb && bearish && /* inShort && */ tbdSetup;
            var eventAdd3u1 = add3Enb && bullish && inLong  && l3;
            var eventAdd3d1 = add3Enb && bearish && inShort && s3;

            // ADD3+
            var setupAdd3u2 = add3Enb && bullish && /*inLong && */ tbu1Setup && !l3;
            var setupAdd3d2 = add3Enb && bearish && /*inShort &&*/ tbd1Setup && !s3;


            // Retrace
            var setupShortRetu1 = redEnb && bearish && inShort && hasTpUp1 && !sretraceStart;
            var setupLongRetd1 =  redEnb && bullish && inLong  && hasTpDn1 && !lretraceStart;
            var eventShortRetu1 = redEnb && bearish && inShort && hasTpUp1 && sretraceStart;
            var eventLongRetd1 =  redEnb && bullish && inLong  && hasTpDn1 && lretraceStart;

            // RETRACE +
            var setupLongRetd2 = redEnb && bullish && inLong  && hasTpDn2;
            var setupShortRetu2 =  redEnb && bearish && inShort && hasTpUp2;

            // EXH
            var setupExhu1 = exhEnb && bearish && inShort && hasTpUp1 && exhDnSetup && !exhUp;
            var setupExhd1 = exhEnb && bullish && inLong  && hasTpDn1 && exhUpSetup && !exhDn; 
            var eventExhu1 = exhEnb && bearish && hasTpUp1 && exhUp;
            var eventExhd1 = exhEnb && bullish && hasTpDn1 && exhDn;

            // EXH+

            var setupExhu2 = exhEnb && bearish && inShort && hasTpUp2 && exhDnSetup;
            var setupExhd2 = exhEnb && bullish && inLong  && hasTpDn2 && exhUpSetup;

            // Position
            var newLongPosition = bullish && newLong;
            var newShortPosition = bearish && inShort;
            var longPosition = bullish && newShort;
            var shortPosition = bearish && inShort;
            var outLongPositionExh = exhEnb && bullish && !inLong;
            var outShortPositionExh = exhEnb && bearish && !inShort;

            // Trend scsig          
            var newBullTrend = bullish && newLong;
            var bullTrend = bullish;
            var newBearTrend = bearish && newShort;
            var bearTrend = bearish;
            var bullScoreChg = bullish && inLong && midScore;
            var bearScoreChg = bearish && inShort && midScore;

            // 1 New Trend Event
            if (bullish && newLong)
            {
                var text1 = l0 ? " Trend" : l1 ? " Add1" : l2 ? " Add2" : " Add3";
                trendMessage = rev ? "Bearish\u00A0\u00A0" : "Bullish\u00A0\u00A0";
                positionMessage = rev ? "New Short\u00A0\u00A0" : "New Long\u00A0\u00A0";
                currentMessage = rev ? "New\u00A0" + text1 + "\u00A0\u00A0" : "New\u00A0" + text1 + "\u00A0\u00A0";
                nextMessage = rev ? "Expect " + interval + " Yld To move Higher\u00A0\u00A0" : "Expect " + interval + " Px to move Higher\u00A0\u00A0";
                trendColor = rev ? Colors.Red : Colors.Lime;
                positionColor = rev ? Colors.Red : Colors.Lime;
                currentColor = rev ? Colors.Red : Colors.Lime;
                nextColor = rev ? Colors.Red : Colors.Lime;
            }
            else if (bearish && newShort)
            {
                var text1 = s0 ? "Trend" : s1 ? "Add1" : s2 ? "Add2" : "Add3";
                trendMessage = rev ? "Bullish\u00A0\u00A0" : "Bearish\u00A0\u00A0";
                positionMessage = rev ? "New Long\u00A0\u00A0" : "New Short\u00A0\u00A0";
                currentMessage = rev ? "New " + text1 + "\u00A0\u00A0" : "New " + text1 + "\u00A0\u00A0";
                nextMessage = rev ? "Expect " + interval + " Yld To move Lower\u00A0\u00A0" : "Expect " + interval + " Px To move Lower\u00A0\u00A0";
                trendColor = rev ? Colors.Lime : Colors.Red;
                positionColor = rev ? Colors.Lime : Colors.Red;
                currentColor = rev ? Colors.Lime : Colors.Red;
                nextColor = rev ? Colors.Lime : Colors.Red;
            }

            // 2 Add2 Add on Alert SETUP 
            else if (add2Enb && bullish && inLong && hasTpUp1 && aoaUpSetup1)
            {
                trendMessage = rev ? "Bearish\u00A0\u00A0" : "Bullish\u00A0\u00A0";
                positionMessage = rev ? "Short\u00A0\u00A0" : "Long\u00A0\u00A0";
                currentMessage = rev ? "Add2 Short Alert if Yld " + interval + " Cl > " + tpup1.ToString("0.00") + "\u00A0\u00A0" : "Add2 Long Alert if Px " + interval + " Cl > " + tpup1.ToString("0.00") + "\u00A0\u00A0";
                var warn = cl[idx] <= tpup1;
                trendColor = rev ? Colors.Red : Colors.Lime;
                positionColor = rev ? Colors.Red : Colors.Lime;
                currentColor = warn ? Colors.Yellow : rev ? Colors.Red : Colors.Lime;
            }
            else if (add2Enb && bearish && inShort && hasTpDn1 && aoaDnSetup1)
            {
                trendMessage = rev ? "Bullish\u00A0\u00A0" : "Bearish\u00A0\u00A0";
                positionMessage = rev ? "Long\u00A0\u00A0" : "Short\u00A0\u00A0";
                currentMessage = rev ? "Add2 Long Alert if Yld " + interval + " Cl < " + tpdn1.ToString("0.00") + "\u00A0\u00A0" : "Add2 Short Alert if Px  " + interval + " Cl < " + tpdn1.ToString("0.00") + "\u00A0\u00A0";
                var warn = cl[idx] >= tpdn1;
                trendColor = rev ? Colors.Lime : Colors.Red;
                positionColor = rev ? Colors.Lime : Colors.Red;
                currentColor = warn ? Colors.Yellow : rev ? Colors.Lime : Colors.Red;
            }
            // 2a Add2 Add on Alert EVENT 
            else if (add2Enb && bullish && inLong && l2)
            {
                trendMessage = rev ? "Bearish\u00A0\u00A0" : "Bullish\u00A0\u00A0";
                positionMessage = rev ? "Short - New Add2\u00A0\u00A0" : "Long - New Add2\u00A0\u00A0";
                var extraAdvice = double.IsNaN(tpup1) ? "" : " if " + interval + " Cl > " + tpup1.ToString("0.00");
                currentMessage = (rev ? "Add2 Alert: Expect Yld to Increase\u00A0\u00A0" : "Add2 Alert: Expect Px to Rise\u00A0\u00A0") + extraAdvice;
                var warn = !double.IsNaN(tpup1) && cl[idx] <= tpup1;
                trendColor = rev ? Colors.Red : Colors.Lime;
                positionColor = rev ? Colors.Red : Colors.Lime;
                currentColor = warn ? Colors.Yellow : rev ? Colors.Red : Colors.Lime;
            }
            else if (add2Enb && bearish && inShort && s2)
            {
                trendMessage = rev ? "Bullish\u00A0\u00A0" : "Bearish\u00A0\u00A0";
                positionMessage = rev ? "Long - New Add2\u00A0\u00A0" : "Short - New Add2\u00A0\u00A0";
                var extraAdvice = double.IsNaN(tpdn1) ? "" : " if " + interval + " Cl < " + tpdn1.ToString("0.00");
                currentMessage = (rev ? "Add2 Alert: Expect Yld to Decrease\u00A0\u00A0" : "Add2 Alert: Expect Px to Fall\u00A0\u00A0") + extraAdvice;
                var warn = !double.IsNaN(tpdn1) && cl[idx] >= tpdn1;
                trendColor = rev ? Colors.Lime : Colors.Red;
                positionColor = rev ? Colors.Lime : Colors.Red;
                currentColor = warn ? Colors.Yellow : rev ? Colors.Lime : Colors.Red;
            }

            // 3 Add1 P alert SETUP
            else if (add1Enb && bullish && inLong  && pAlertUpSetup)
            {
                var price = lo[idx] + .6 * (hi[idx] - lo[idx]);
                trendMessage = rev ? "Bearish\u00A0\u00A0" : "Bullish\u00A0\u00A0";
                positionMessage = rev ? "Short\u00A0\u00A0" : "Long\u00A0\u00A0";
                currentMessage = rev ? "Add1 Short Alert if Yld " + interval + " Cl > " + price.ToString("0.00") + "\u00A0\u00A0" : "Add1 Long Alert if Px " + interval + " Cl > " + price.ToString("0.00") + "\u00A0\u00A0";
                var warn = cl[idx] <= price;
                trendColor = rev ? Colors.Red : Colors.Lime;
                positionColor = rev ? Colors.Red : Colors.Lime;
                currentColor = warn ? Colors.Yellow : rev ? Colors.Red : Colors.Lime;
            }
            else if (add1Enb && bearish && inShort && pAlertDnSetup)
            {
                var price = hi[idx] - .6 * (hi[idx] - lo[idx]);
                trendMessage = rev ? "Bullish\u00A0\u00A0" : "Bearish\u00A0\u00A0";
                positionMessage = rev ? "Long\u00A0\u00A0" : "Short\u00A0\u00A0";
                currentMessage = rev ? "Add1 Long Alert if Yld " + interval + " Cl < " + price.ToString("0.00") + "\u00A0\u00A0" : "Add1 Short Alert if Px " + interval + " Cl < " + price.ToString("0.00") + "\u00A0\u00A0";
                var warn = cl[idx] >= price;
                trendColor = rev ? Colors.Lime : Colors.Red;
                positionColor = rev ? Colors.Lime : Colors.Red;
                currentColor = warn ? Colors.Yellow : rev ? Colors.Lime : Colors.Red;
            }
            // 3a Add1 P alert EVENT
            else if (add1Enb && bullish && inLong  && l1)
            {
                var price = lo[idx] + .6 * (hi[idx] - lo[idx]);
                trendMessage = rev ? "Bearish\u00A0\u00A0" : "Bullish\u00A0\u00A0";
                positionMessage = rev ? "Short - New Add1 Alert\u00A0\u00A0" : "Long - New Add1 Alert\u00A0\u00A0";
                currentMessage = rev ? "Add1 Alert: Expect Yld to Increase if " + interval + " Cl > " + price.ToString("0.00") + "\u00A0\u00A0" : "Add1 Alert: Expect Px to Rise if " + interval + " Cl > " + price.ToString("0.00") + "\u00A0\u00A0";
                var warn = cl[idx] <= price;
                trendColor = rev ? Colors.Red : Colors.Lime;
                positionColor = rev ? Colors.Red : Colors.Lime;
                currentColor = warn ? Colors.Yellow : rev ? Colors.Red : Colors.Lime;
            }
            else if (add1Enb && bearish && inShort && s1)
            {
                var price = hi[idx] - .6 * (hi[idx] - lo[idx]);
                trendMessage = rev ? "Bullish\u00A0\u00A0" : "Bearish\u00A0\u00A0";
                positionMessage = rev ? "Long - New Add1\u00A0\u00A0" : "Short - New Add1\u00A0\u00A0";
                currentMessage = rev ? "Add1 Alert: Expect Yld to Fall if " + interval + " Cl < " + price.ToString("0.00") + "\u00A0\u00A0" : "Add1 Alert: Expect Px to Fall if " + interval + " Cl < " + price.ToString("0.00") + "\u00A0\u00A0";
                var warn = cl[idx] >= price;
                trendColor = rev ? Colors.Lime : Colors.Red;
                positionColor = rev ? Colors.Lime : Colors.Red;
                currentColor = warn ? Colors.Yellow : rev ? Colors.Lime : Colors.Red;
            }

            // 4a Add3 Two bar alert SETUP 
            else if (add3Enb && bullish && inLong && tbuSetup)
            {
                var price = cl[idx - 1];
                trendMessage = rev ? "Bearish\u00A0\u00A0" : "Bullish\u00A0\u00A0";
                positionMessage = rev ? "Short\u00A0\u00A0" : "Long\u00A0\u00A0";
                currentMessage = rev ? "Add3 Short Alert if Yld on " + interval + " Cl > " + price.ToString("0.00") + "\u00A0\u00A0" : "Add3 Long Alert: If " + interval + " Cl > " + price.ToString("0.00") + "\u00A0\u00A0";
                var warn = cl[idx] <= price;
                trendColor = rev ? Colors.Red : Colors.Lime;
                positionColor = rev ? Colors.Red : Colors.Lime;
                currentColor = warn ? Colors.Yellow : rev ? Colors.Red : Colors.Lime;
            }
            else if (add3Enb && bearish && inShort && tbdSetup)
            {
                var price = cl[idx - 1];
                trendMessage = rev ? "Bullish\u00A0\u00A0" : "Bearish\u00A0\u00A0";
                positionMessage = rev ? "Long - New Add3\u00A0\u00A0" : "Short - New Add3\u00A0\u00A0";
                currentMessage = rev ? "Add3 Long Alert if Yld on " + interval + " Cl < " + price.ToString("0.00") + "\u00A0\u00A0" : "Add3 Short Alert: If " + interval + " Cl < " + price.ToString("0.00") + "\u00A0\u00A0";
                var warn = cl[idx] >= price;
                trendColor = rev ? Colors.Lime : Colors.Red;
                positionColor = rev ? Colors.Lime : Colors.Red;
                currentColor = warn ? Colors.Yellow : rev ? Colors.Lime : Colors.Red;
            }
            // 4 Add3 Two bar signal EVENT
            else if (add3Enb && bullish && inLong  && l3)
            {
                var price = cl[idx - 1];
                trendMessage = rev ? "Bearish\u00A0\u00A0" : "Bullish\u00A0\u00A0";
                positionMessage = rev ? "Short - New Add3\u00A0\u00A0" : "Long - New Add3\u00A0\u00A0";
                currentMessage = rev ? "Add3 Alert: Expect Yld to Fall if  " + interval + " Cl > " + price.ToString("0.00") + "\u00A0\u00A0" : "Add3 Alert: Expect Px to Rise if " + interval + " Cl > " + price.ToString("0.00") + "\u00A0\u00A0";
                var warn = cl[idx] <= price;
                trendColor = rev ? Colors.Red : Colors.Lime;
                positionColor = rev ? Colors.Red : Colors.Lime;
                currentColor = warn ? Colors.Yellow : rev ? Colors.Red : Colors.Lime;
            }
            else if (add3Enb && bearish && inShort && s3)
            {
                var price = cl[idx - 1];
                trendMessage = rev ? "Bullish\u00A0\u00A0" : "Bearish\u00A0\u00A0";
                positionMessage = rev ? "Long - New Add3\u00A0\u00A0" : "Short - New Add3\u00A0\u00A0";
                currentMessage = rev ? "Add3 Alert: Expect Yld to Rise if  " + interval + " Cl < " + price.ToString("0.00") + "\u00A0\u00A0" : "Add3 Alert: Expect Px to Fall if " + interval + " Cl < " + price.ToString("0.00") + "\u00A0\u00A0";
                var warn = cl[idx] >= price;
                trendColor = rev ? Colors.Lime : Colors.Red;
                positionColor = rev ? Colors.Lime : Colors.Red;
                currentColor = warn ? Colors.Yellow : rev ? Colors.Lime : Colors.Red;
            }

            // 5  Exh Signal Setup 
            else if (exhEnb && bullish && inLong  && hasTpDn1 && exhUpSetup && !exhDn)
            {
                trendMessage = rev ? "Bearish\u00A0\u00A0" : "Bullish\u00A0\u00A0";
                positionMessage = rev ? "Short\u00A0\u00A0" : "Long\u00A0\u00A0";
                currentMessage = rev ? "Short Exit Exh Alert if " + interval + " Cl < " + tpdn1.ToString("0.00") + "\u00A0\u00A0" : "Long Exit Exh Alert if " + interval + " Cl < " + tpdn1.ToString("0.00") + "\u00A0\u00A0";
                var warn = cl[idx] >= tpdn1;
                trendColor = rev ? Colors.Red : Colors.Lime;
                positionColor = rev ? Colors.Red : Colors.Lime;
                currentColor = warn ? Colors.Yellow : rev ? Colors.Lime : Colors.Red;
            }
            else if (exhEnb && bearish && inShort && hasTpUp1 && exhDnSetup && !exhUp)
            {
                trendMessage = rev ? "Bullish\u00A0\u00A0" : "Bearish\u00A0\u00A0";
                positionMessage = rev ? "Long\u00A0\u00A0" : "Short\u00A0\u00A0";
                currentMessage = rev ?  "Long Exit Exh Alert if " + interval + " Cl > " + tpup1.ToString("0.00") + "\u00A0\u00A0" : "Short Exit Exh Alert if " + interval + " Cl > " + tpup1.ToString("0.00") + "\u00A0\u00A0";
                var warn = cl[idx] <= tpup1;
                trendColor = rev ? Colors.Lime : Colors.Red;
                positionColor = rev ? Colors.Lime : Colors.Red;
                currentColor = warn ? Colors.Yellow : rev ? Colors.Red : Colors.Lime;
            }
            // 5a Exh Signal Event 
            else if (exhEnb && bullish && exhDn)
            {
                trendMessage = rev ? "Bearish\u00A0\u00A0" : "Bullish\u00A0\u00A0";
                positionMessage = "Out - New Exh\u00A0\u00A0";
                currentMessage = rev ? "Exh Alert: Expect Yld to Falll\u00A0\u00A0" : "Exh Alert: Expect PX to Fall\u00A0\u00A0"; 
                trendColor = rev ? Colors.Red : Colors.Lime;
                positionColor = rev ? Colors.Lime : Colors.Red;
                currentColor = rev ? Colors.Lime : Colors.Red;
            }
            else if (exhEnb && bearish && exhUp)
            {
                trendMessage = rev ? "Bullish\u00A0\u00A0" : "Bearish\u00A0\u00A0";
                positionMessage = "Out - New Exh\u00A0\u00A0";
                currentMessage = rev ? "Exh Alert: Expect Yld to Rise\u00A0\u00A0" : "Exh Alert: Expect Px to Rise\u00A0\u00A0";
                trendColor = rev ? Colors.Lime : Colors.Red;
                positionColor = rev ? Colors.Red : Colors.Lime;
                currentColor = rev ? Colors.Red : Colors.Lime;
            }

            // 5b Exh Signal Get Back In AOA Setup
            else if (exhEnb && add2Enb && bullish && !inLong && hasTpUp1 && aoaUpSetup1 && lExhaustion)
            {
                trendMessage = rev ? "Bearish\u00A0\u00A0" : "Bullish\u00A0\u00A0";
                positionMessage = "Out\u00A0\u00A0";
                currentMessage = rev ? "Add2 Short Alert if Yld " + interval + " Cl > " + tpup1.ToString("0.00") + "\u00A0\u00A0" : "Add2 Long Alert if Px " + interval + " Cl > " + tpup1.ToString("0.00") + "\u00A0\u00A0";
                var warn = cl[idx] <= tpup1;
                trendColor = rev ? Colors.Red : Colors.Lime;
                positionColor = Colors.White;
                currentColor = warn ? Colors.Yellow : rev ? Colors.Red : Colors.Lime;
            }
            else if (exhEnb && add2Enb && bearish && !inShort && hasTpDn1 && aoaDnSetup1 && sExhaustion)
            {
                trendMessage = rev ? "Bullish\u00A0\u00A0" : "Bearish\u00A0\u00A0";
                positionMessage = "Out\u00A0\u00A0";
                currentMessage = rev ? "Add2 Long Alert if Yld " + interval + " Cl < " + tpdn1.ToString("0.00") + "\u00A0\u00A0" : "Add2 Short Alert if Px  " + interval + " Cl < " + tpdn1.ToString("0.00") + "\u00A0\u00A0";
                var warn = cl[idx] >= tpdn1;
                trendColor = rev ? Colors.Lime : Colors.Red;
                positionColor = Colors.White;
                currentColor = warn ? Colors.Yellow : rev ? Colors.Lime : Colors.Red;
            }
            // 5c Exh Signal Get Back In AOA Event
            else if (exhEnb && add2Enb && bullish && !inLong && hasTpUp1 && lExhaustion && l2)
            {
                trendMessage = rev ? "Bearish\u00A0\u00A0" : "Bullish\u00A0\u00A0";
                positionMessage = rev ? "Short - New Add2\u00A0\u00A0" : "Long - New Add2\u00A0\u00A0";
                currentMessage = rev ? "Add2 Alert: Expect Yld to Increase if " + interval + " Cl > " + tpup1.ToString("0.00") + "\u00A0\u00A0" : "Add2 Alert: Expect Px to Rise if " + interval + " Cl > " + tpup1.ToString("0.00") + "\u00A0\u00A0";
                var warn = cl[idx] <= tpup1; 
                trendColor = rev ? Colors.Red : Colors.Lime;
                positionColor = Colors.White;
                currentColor = warn ? Colors.Yellow : rev ? Colors.Red : Colors.Lime;
            }
            else if (exhEnb && add2Enb && bearish && !inShort && hasTpDn1 && sExhaustion && s2)
            {
                trendMessage = rev ? "Bullish\u00A0\u00A0" : "Bearish\u00A0\u00A0";
                positionMessage = rev ? "Long - New Add2\u00A0\u00A0" : "Short - New Add2\u00A0\u00A0";
                currentMessage = rev ? "Add2 Alert: Expect Yld to Fall if " + interval + " Cl < " + tpdn1.ToString("0.00") + "\u00A0\u00A0" : "Add2 Alert: Expect Px to Fall if " + interval + " Cl < " + tpdn1.ToString("0.00") + "\u00A0\u00A0";
                var warn = cl[idx] >= tpdn1;
                trendColor = rev ? Colors.Lime : Colors.Red;
                positionColor = Colors.White;
                currentColor = warn ? Colors.Yellow : rev ? Colors.Lime : Colors.Red;
            }

            // 5d Exh Signal Get Back In 2 bar Setup
            else if (exhEnb && add3Enb && bullish && !inLong  && tbu1Setup && lExhaustion)
            {
                var price = cl[idx - 1];
                trendMessage = rev ? "Bearish\u00A0\u00A0" : "Bullish\u00A0\u00A0";
                positionMessage = "Out\u00A0\u00A0";
                currentMessage = rev ? "Add3 Short Alert: if Yld on Next " + interval + " Cl > " + price.ToString("0.00") + "\u00A0\u00A0" : "Add3 Long Alert: If Next " + interval + " Cl > " + price.ToString("0.00") + "\u00A0\u00A0";
                var warn = cl[idx] <= price;
                trendColor = rev ? Colors.Red : Colors.Lime;
                positionColor = Colors.White;
                currentColor = Colors.Yellow;
            }
            else if (exhEnb && add3Enb && bearish && !inShort && tbd1Setup && sExhaustion)
            {
                var price = cl[idx - 1];
                trendMessage = rev ? "Bullish\u00A0\u00A0" : "Bearish\u00A0\u00A0";
                positionMessage = "Out\u00A0\u00A0";
                currentMessage = rev ? "Add3 Long Alert: if Yld on Next " + interval + " Cl < " + price.ToString("0.00") + "\u00A0\u00A0" : "Add3 Short Alert: If Next " + interval + " Cl < " + price.ToString("0.00") + "\u00A0\u00A0";
                var warn = cl[idx] >= price;
                trendColor = rev ? Colors.Lime : Colors.Red;
                positionColor = Colors.White;
                currentColor = Colors.Yellow;
            }
            // 5e Exh Signal Get Back In 2 bar Event
            else if (exhEnb && add3Enb && bullish && !inLong  && lExhaustion && l3)
            {
                var price = cl[idx - 1];
                trendMessage = rev ? "Bearish\u00A0\u00A0" : "Bullish\u00A0\u00A0";
                positionMessage = rev ? "Short - New Add3\u00A0\u00A0" : "Long - New Add3\u00A0\u00A0";
                currentMessage = rev ? "Add3 Alert if Yld on Next " + interval + " Cl > " + price.ToString("0.00") + "\u00A0\u00A0" : "Add3 Alert: If Next " + interval + " Cl > " + price.ToString("0.00") + "\u00A0\u00A0";
                var warn = cl[idx] <= price;
                trendColor = rev ? Colors.Red : Colors.Lime;
                positionColor = Colors.White;
                currentColor = Colors.Yellow;
            }
            else if (exhEnb && add3Enb && bearish && !inShort && sExhaustion && s3)
            {
                var price = cl[idx - 1];
                trendMessage = rev ? "Bullish\u00A0\u00A0" : "Bearish\u00A0\u00A0";
                positionMessage = rev ? "Long - New Add3\u00A0\u00A0" : "Short - New Add3\u00A0\u00A0";
                currentMessage = rev ? "Add3 Alert if Yld on Next " + interval + " Cl < " + price.ToString("0.00") + "\u00A0\u00A0" : "Add3 Alert: If Next " + interval + " Cl < " + price.ToString("0.00") + "\u00A0\u00A0";
                var warn = cl[idx] >= price;
                trendColor = rev ? Colors.Lime : Colors.Red;
                positionColor = Colors.White;
                currentColor = Colors.Yellow;
            }

            // 5f Exh Sig Get Back in PAlert Setup
            else if (exhEnb && add1Enb && bullish && !inLong  && pAlertUpSetup && lExhaustion)
            {
                var price = lo[idx] + .6 * (hi[idx] - lo[idx]);
                trendMessage = rev ? "Bearish\u00A0\u00A0" : "Bullish\u00A0\u00A0";
                positionMessage = "Out\u00A0\u00A0";
                currentMessage = rev ? "Add1 Short Alert if Yld " + interval + " Cl > " + price.ToString("0.00") + "\u00A0\u00A0" : "Add1 Long Alert if " + interval + " Cl > " + price.ToString("0.00") + "\u00A0\u00A0";
                var warn = cl[idx] <= price;
                trendColor = rev ? Colors.Red : Colors.Lime;
                positionColor = Colors.White;
                currentColor = warn ? Colors.Yellow : rev ? Colors.Red : Colors.Lime;
            }
            else if (exhEnb && add1Enb && bearish && !inShort && pAlertDnSetup && sExhaustion)
            {
                var price = lo[idx] + .6 * (hi[idx] - lo[idx]);
                trendMessage = rev ? "Bullish\u00A0\u00A0" : "Bearish\u00A0\u00A0";
                positionMessage = "Out\u00A0\u00A0";
                currentMessage = rev ? "Add1 Long Alert if Yld " + interval + " Cl < " + price.ToString("0.00") + "\u00A0\u00A0" : "Add1 Short Alert if " + interval + " Cl < " + price.ToString("0.00") + "\u00A0\u00A0";
                var warn = cl[idx] >= price;
                trendColor = rev ? Colors.Lime : Colors.Red;
                positionColor = Colors.White;
                currentColor = warn ? Colors.Yellow : rev ? Colors.Lime : Colors.Red;
            }          
            // 5g Exh Sig Get Back in PAlert Event
            else if (exhEnb && add1Enb && bullish && !inLong  && lExhaustion && l1)
            {
                var price = cl[idx - 1];
                trendMessage = rev ? "Bearish\u00A0\u00A0" : "Bullish\u00A0\u00A0";
                positionMessage = rev ? "Short - New Add1\u00A0\u00A0" : "Long - New Add1\u00A0\u00A0";
                currentMessage = rev ? "Add1 Alert: Expect Yld to Increase if " + interval + " Cl > " + price.ToString("0.00") + "\u00A0\u00A0" : "Add1 Alert: Expect Px to Rise if " + interval + " Cl > " + price.ToString("0.00") + "\u00A0\u00A0";
                var warn = cl[idx] <= price;
                trendColor = rev ? Colors.Red : Colors.Lime;
                positionColor = Colors.White;
                currentColor = warn ? Colors.Yellow : rev ? Colors.Red : Colors.Lime;
            }
            else if (exhEnb && add1Enb && bearish && !inShort && sExhaustion && s1)
            {
                var price = cl[idx - 1];
                trendMessage = rev ? "Bullish\u00A0\u00A0" : "Bearish\u00A0\u00A0";
                positionMessage = rev ? "Long - New Add1\u00A0\u00A0" : "Short - New Add1\u00A0\u00A0";
                currentMessage = rev ? "Add1 Alert if Yld  " + interval + " Cl < " + price.ToString("0.00") + "\u00A0\u00A0" : "Add1 Alert: If " + interval + " Cl < " + price.ToString("0.00") + "\u00A0\u00A0";
                var warn = cl[idx] >= price; 
                trendColor = rev ? Colors.Lime : Colors.Red;
                positionColor = Colors.White;
                currentColor = warn ? Colors.Yellow : rev ? Colors.Lime : Colors.Red;
            }

            // 5h Exh Out Condition TRUE
            else if (exhEnb && bullish && !inLong)
            {
                trendMessage = rev ? "Bearish\u00A0\u00A0" : "Bullish\u00A0\u00A0";
                positionMessage = "Out\u00A0\u00A0";
                currentMessage = rev ? "Out: Wait for New Short\u00A0\u00A0" : "Out: Wait for New Long\u00A0\u00A0";
                trendColor = rev ? Colors.Red : Colors.Lime;
                positionColor = Colors.White;
                currentColor = Colors.Yellow;
            }
            else if (exhEnb && bearish && !inShort)
            {
                trendMessage = rev ? "Bullish\u00A0\u00A0" : "Bearish\u00A0\u00A0";
                positionMessage = "Out\u00A0\u00A0";
                currentMessage = rev ? "Out: Wait for New Long\u00A0\u00A0" : "Out: Wait for New Short\u00A0\u00A0";
                trendColor = rev ? Colors.Lime : Colors.Red;
                positionColor = Colors.White;
                currentColor = Colors.Yellow;
            }
                               
            // 6 Retrace Signal Setup 
            else if (redEnb && bullish && inLong  && hasTpDn1 && !lretraceStart)
            {
                trendMessage = rev ? "Bearish\u00A0\u00A0" : "Bullish\u00A0\u00A0";
                positionMessage = rev ? "Short\u00A0\u00A0" : "Long\u00A0\u00A0";
                currentMessage = rev ? "Retrace Down Alert if " + interval + " Cl < " + tpdn1.ToString("0.00") + "\u00A0\u00A0" : "Retrace Dn Alert if " + interval + " Cl < " + tpdn1.ToString("0.00") + "\u00A0\u00A0";
                var warn = cl[idx] >= tpdn1;
                trendColor = rev ? Colors.Red : Colors.Lime;
                positionColor = rev ? Colors.Red : Colors.Lime;
                currentColor = Colors.Yellow;
            }
            else if (redEnb && bearish && inShort && hasTpUp1 && !sretraceStart)
            {
                trendMessage = rev ? "Bullish\u00A0\u00A0" : "Bearish\u00A0\u00A0";
                positionMessage = rev ? "Long\u00A0\u00A0" : "Short\u00A0\u00A0";
                currentMessage = rev ? "Retrace Down Alert if " + interval + " Cl > " + tpup1.ToString("0.00") + "\u00A0\u00A0" : "Retrace Up Alert if " + interval + " Cl > " + tpup1.ToString("0.00") + "\u00A0\u00A0";
                var warn = cl[idx] <= tpup1;
                trendColor = rev ? Colors.Lime : Colors.Red;
                positionColor = rev ? Colors.Lime : Colors.Red;
                currentColor = Colors.Yellow;
            }
            // 6a Retrace Sig Event 
            else if (redEnb && bullish && inLong  && hasTpDn1 && lretraceStart)
            {
                trendMessage = rev ? "Bearish\u00A0\u00A0" : "Bullish\u00A0\u00A0";
                positionMessage = rev ? "Short Retracing\u00A0\u00A0" : "Long Retracing\u00A0\u00A0";
                currentMessage = rev ? "Retrace Alert: Reduce Short if " + interval + " Cl < " + tpdn1.ToString("0.00") + "\u00A0\u00A0" : "Retrace Alert: Reduce Long if " + interval + " Cl < " + tpdn1.ToString("0.00") + "\u00A0\u00A0";
                var warn = cl[idx] >= tpdn1;
                trendColor = rev ? Colors.Red : Colors.Lime;
                positionColor = rev ? Colors.Red : Colors.Lime;
                currentColor = warn ? Colors.Yellow : rev ? Colors.Lime : Colors.Red;
            }
            else if (redEnb && bearish && inShort && hasTpUp1 && sretraceStart)
            {
                trendMessage = rev ? "Bullish\u00A0\u00A0" : "Bearish\u00A0\u00A0";
                positionMessage = rev ? "Long\u00A0\u00A0" : "Short\u00A0\u00A0";
                currentMessage = rev ? "Retrace Alert: Reduce Long if " + interval + " Cl > " + tpup1.ToString("0.00") + "\u00A0\u00A0" : "Retrace Alert: Reduce Short if " + interval + " Cl > " + tpup1.ToString("0.00") + "\u00A0\u00A0";
                var warn = cl[idx] <= tpup1;
                trendColor = rev ? Colors.Lime : Colors.Red;
                positionColor = rev ? Colors.Lime : Colors.Red;
                currentColor = warn ? Colors.Yellow :  rev ? Colors.Red : Colors.Lime;
            }
            // 6b Retrace Out Condition TRUE
            else if (redEnb && bullish && inLong && lRetrace)
            {
                trendMessage = rev ? "Bearish\u00A0\u00A0" : "Bullish\u00A0\u00A0";
                positionMessage = rev ? "Short Retracing\u00A0\u00A0" : "Long Retracing\u00A0\u00A0";
                currentMessage = rev ? "Expect Yld to Retrace Higher" + "\u00A0\u00A0" : "Expect Px to Retrace Lower" + "\u00A0\u00A0";
                trendColor = rev ? Colors.Red : Colors.Lime;
                positionColor = rev ? Colors.Red : Colors.Lime;
                currentColor = Colors.Yellow;
            }
            else if (redEnb && bearish && inShort && sRetrace)
            {
                trendMessage = rev ? "Bullish\u00A0\u00A0" : "Bearish\u00A0\u00A0";
                positionMessage = rev ? "Long Retracing\u00A0\u00A0" : "Short Retracing\u00A0\u00A0";
                currentMessage = rev ? "Expect Yld to Retrace Lower" + "\u00A0\u00A0" : "Expect Px to Retrace Higher" + "\u00A0\u00A0";
                trendColor = rev ? Colors.Lime : Colors.Red;
                positionColor = rev ? Colors.Lime : Colors.Red;
                currentColor = Colors.Yellow;
            }
           
            // 6c Retrace Signal Get Back In AOA Setup
            else if (redEnb && add2Enb && bullish && !inLong  && hasTpUp1 && aoaUpSetup1 && lRetrace)
            {
                trendMessage = rev ? "Bearish\u00A0\u00A0" : "Bullish\u00A0\u00A0";
                positionMessage = "Out\u00A0\u00A0";
                currentMessage = rev ? "Add2 Short Alert if Yld " + interval + " Cl > " + tpup1.ToString("0.00") + "\u00A0\u00A0" : "Add2 Long Alert if " + interval + " Cl > " + tpup1.ToString("0.00") + "\u00A0\u00A0";
                var warn = cl[idx] <= tpup1;
                trendColor = rev ? Colors.Red : Colors.Lime;
                positionColor = Colors.White;
                currentColor = warn ? Colors.Yellow : rev ? Colors.Red : Colors.Lime;
            }
            else if (redEnb && add2Enb && bearish && !inShort && hasTpDn1 && aoaDnSetup1 && sRetrace)
            {
                trendMessage = rev ? "Bullish\u00A0\u00A0" : "Bearish\u00A0\u00A0";
                positionMessage = "Out\u00A0\u00A0";
                currentMessage = rev ? "Add2 Long Alert if Yld " + interval + " Cl < " + tpdn1.ToString("0.00") + "\u00A0\u00A0" : "Add2 Short Alert if " + interval + " Cl < " + tpdn1.ToString("0.00") + "\u00A0\u00A0";
                var warn = cl[idx] >= tpdn1;
                trendColor = rev ? Colors.Lime : Colors.Red;
                positionColor = Colors.White;
                currentColor = warn ? Colors.Yellow : rev ? Colors.Lime : Colors.Red;
            }
            // 6d Retrace Signal Get Back In AOA Event
            else if (redEnb && add2Enb && bullish && !inLong  && hasTpUp1 && !lRetrace && l2)
            {
                trendMessage = rev ? "Bearish\u00A0\u00A0" : "Bullish\u00A0\u00A0";
                positionMessage = rev ? "Short - New Add2\u00A0\u00A0" : "Long - New Add2\u00A0\u00A0";
                currentMessage = rev ? "Add2 Alert: Expect Yld to Increase if " + interval + " Cl > " + tpup1.ToString("0.00") + "\u00A0\u00A0" : "Add2 Alert: Expect Px to Rise if " + interval + " Cl > " + tpup1.ToString("0.00") + "\u00A0\u00A0";
                var warn = cl[idx] <= tpup1;
                trendColor = rev ? Colors.Red : Colors.Lime;
                positionColor = Colors.White;
                currentColor = warn ? Colors.Yellow :  rev ? Colors.Red : Colors.Lime;
            }
            else if (redEnb && add2Enb && bearish && !inShort && hasTpDn1 && !sRetrace && s2)
            {
                trendMessage = rev ? "Bullish\u00A0\u00A0" : "Bearish\u00A0\u00A0";
                positionMessage = rev ? "Long - New Add2\u00A0\u00A0" : "Short - New Add2\u00A0\u00A0";
                currentMessage = rev ? "Add2 Alert: Expect Yld to Fall if " + interval + " Cl < " + tpdn1.ToString("0.00") + "\u00A0\u00A0" : "Add2 Alert: Expect Px to Fall if " + interval + " Cl < " + tpdn1.ToString("0.00") + "\u00A0\u00A0";
                var warn = cl[idx] >= tpdn1;
                trendColor = rev ? Colors.Lime : Colors.Red;
                positionColor = Colors.White;
                currentColor = warn ? Colors.Yellow : rev ? Colors.Lime : Colors.Red;
            }
            
            // 6e Retrace Sig Get Back in PAlert Setup
            else if (redEnb && add1Enb && bullish && !inLong  && pAlertUpSetup && lRetrace)
            {
                var price = lo[idx] + .6 * (hi[idx] - lo[idx]);
                trendMessage = rev ? "Bearish\u00A0\u00A0" : "Bullish\u00A0\u00A0";
                positionMessage = "Out\u00A0\u00A0";
                currentMessage = rev ? "Add1 Short Alert if Yld " + interval + " Cl > " + price.ToString("0.00") + "\u00A0\u00A0" : "Add1 Long Alert if " + interval + " Cl > " + price.ToString("0.00") + "\u00A0\u00A0";
                var warn = cl[idx] <= price;
                trendColor = rev ? Colors.Red : Colors.Lime;
                positionColor = Colors.White;
                currentColor = warn ? Colors.Yellow : rev ? Colors.Red : Colors.Lime;
            }
            else if (redEnb && add1Enb && bearish && !inShort && pAlertDnSetup && sRetrace)
            {
                var price = lo[idx] + .6 * (hi[idx] - lo[idx]);
                trendMessage = rev ? "Bullish\u00A0\u00A0" : "Bearish\u00A0\u00A0";
                positionMessage = "Out\u00A0\u00A0";
                currentMessage = rev ? "Add1 Long Alert if Yld " + interval + " Cl < " + price.ToString("0.00") + "\u00A0\u00A0" : "Add1 Short Alert if " + interval + " Cl < " + price.ToString("0.00") + "\u00A0\u00A0";
                var warn = cl[idx] >= price;
                positionColor = Colors.White;
                currentColor = warn ? Colors.Yellow : rev ? Colors.Lime : Colors.Red;
            }
            // 6f Retrace - Sig Get Back in PAlert Event
            else if (redEnb && add1Enb && bullish && !inLong  && !lRetrace && l1)
            {
                var price = cl[idx - 1];
                trendMessage = rev ? "Bearish\u00A0\u00A0" : "Bullish\u00A0\u00A0";
                positionMessage = (rev ? "Short - New Add1\u00A0\u00A0" : "Long - New Add1\u00A0\u00A0") + pressureMessage;
                currentMessage = rev ? "Add1 Alert: Expect Yld to Increase if " + interval + " Cl > " + price.ToString("0.00") + "\u00A0\u00A0" : "Add1 Alert: Expect Px to Rise if " + interval + " Cl > " + price.ToString("0.00") + "\u00A0\u00A0";
                var warn = cl[idx] <= price;
                trendColor = rev ? Colors.Red : Colors.Lime;
                positionColor = Colors.White;
                currentColor = warn ? Colors.Yellow : rev ? Colors.Red : Colors.Lime;
            }
            else if (redEnb && add1Enb && bearish && !inShort && !sRetrace && s1)
            {
                var price = cl[idx - 1];
                trendMessage = rev ? "Bullish\u00A0\u00A0" : "Bearish\u00A0\u00A0";
                positionMessage = (rev ? "Long - New Add1\u00A0\u00A0" : "Short - New Add1\u00A0\u00A0") + pressureMessage;
                currentMessage = rev ? "Add1 Alert: Expect Yld to Decrease if  " + interval + " Cl < " + price.ToString("0.00") + "\u00A0\u00A0" : "Add1 Alert:  Expect Px to Fall if" + interval + " Cl < " + price.ToString("0.00") + "\u00A0\u00A0";
                var warn = cl[idx] >= price;
                trendColor = rev ? Colors.Lime : Colors.Red;
                positionColor = Colors.White;
                currentColor = warn ? Colors.Yellow : rev ? Colors.Lime : Colors.Red;
            }

            // 6g Retrace Signal Get Back In 2 bar Setup
            else if (redEnb && add3Enb && bullish && !inLong  && tbu1Setup && lRetrace)
            {
                var price = cl[idx - 1];
                trendMessage = rev ? "Bearish\u00A0\u00A0" : "Bullish\u00A0\u00A0";
                positionMessage = "Out\u00A0\u00A0";
                currentMessage = rev ? "Add3 Short Alert: if Yld on Next" + interval + " Cl > " + price.ToString("0.00") + "\u00A0\u00A0" : "Add3 Long Alert if Next " + interval + " Cl > " + price.ToString("0.00") + "\u00A0\u00A0";
                var warn = cl[idx] <= price;
                trendColor = rev ? Colors.Red : Colors.Lime;
                positionColor = Colors.White;
                currentColor = Colors.Yellow;
            }
            else if (redEnb && add3Enb && bearish && !inShort && tbd1Setup && sRetrace)
            {
                var price = cl[idx - 1];
                trendMessage = rev ? "Bullish\u00A0\u00A0" : "Bearish\u00A0\u00A0";
                positionMessage = "Out\u00A0\u00A0";
                currentMessage = rev ? "Add3 Long Alert: if Yld on Next" + interval + " Cl < " + price.ToString("0.00") + "\u00A0\u00A0" : "Add3 Short Alert if Next " + interval + " Cl < " + price.ToString("0.00") + "\u00A0\u00A0";
                var warn = cl[idx] >= price;
                trendColor = rev ? Colors.Lime : Colors.Red;
                positionColor = Colors.White;
                currentColor = Colors.Yellow;
            }
            // 6h Retrace Signal Get Back In 2 bar Event
            else if (redEnb && add3Enb && bullish && !inLong  && !lRetrace && l3)
            {
                var price = cl[idx - 1];
                trendMessage = rev ? "Bearish\u00A0\u00A0" : "Bullish\u00A0\u00A0";
                positionMessage = (rev ? "Short - New Add3\u00A0\u00A0" : "Long - New Add3\u00A0\u00A0") + pressureMessage;
                currentMessage = rev ? "Add3 Alert if Yld on Next " + interval + " Cl > " + price.ToString("0.00") + "\u00A0\u00A0" : "Add3 Alert: If Next " + interval + " Cl > " + price.ToString("0.00") + "\u00A0\u00A0";
                var warn = cl[idx] <= price;
                trendColor = rev ? Colors.Red : Colors.Lime;
                positionColor = Colors.White;
                currentColor = Colors.Yellow;
            }
            else if (redEnb && add3Enb && bearish && !inShort && !sRetrace && s3)
            {
                var price = cl[idx - 1];
                trendMessage = rev ? "Bullish\u00A0\u00A0" : "Bearish\u00A0\u00A0";
                positionMessage = (rev ? "Long - New Add3\u00A0\u00A0" : "Short - New Add3\u00A0\u00A0") + pressureMessage;
                currentMessage = rev ? "Add3 Alert if Yld on Next " + interval + " Cl < " + price.ToString("0.00") + "\u00A0\u00A0" : "Add3 Alert: If Next " + interval + " Cl < " + price.ToString("0.00") + "\u00A0\u00A0";
                var warn = cl[idx] >= price;
                trendColor = rev ? Colors.Lime : Colors.Red;
                positionColor = Colors.White;
                currentColor = Colors.Yellow;
            }

            // 7 reverse trend setup
            //else if (bullish && inLong && midScore)
            //{
            //    trendMessage = (rev ? "Bearish" : "Bullish") + trendChangeMessage;
            //    positionMessage = rev ? "Short" : "Long";
            //    currentMessage = rev ? "Expect Lower Yld if " + interval + " Score < 45" : "Expect Lower Px if " + interval + " Score < 45";
            //    trendColor = rev ? Colors.Red : Colors.Lime;
            //    positionColor = rev ? Colors.Red : Colors.Lime;
            //    currentColor = Colors.Yellow;
            //}
            //else if (bearish && inShort && midScore)
            //{
            //    trendMessage = (rev ? "Bullish" : "Bearish") + trendChangeMessage;
            //    positionMessage = rev ? "Long" : "Short";
            //    currentMessage = rev ? "Expect Higher Yld if " + interval + " Score > 55" : "Expect Higher Px if " + interval + " Score > 55";
            //    trendColor = rev ? Colors.Lime : Colors.Red;
            //    positionColor = rev ? Colors.Lime : Colors.Red;
            //    currentColor = Colors.Yellow;
            //}

            // 8 stay in position - no change 
            else if (bullish && inLong)
            {
                var dir1 = ftUp ? "Up" : "Dn";
                var dir2 = ftUp ? "Higher" : "Lower";
                trendMessage = rev ? "Bearish\u00A0\u00A0" : "Bullish\u00A0\u00A0";
                positionMessage = rev ? "Short\u00A0\u00A0" : "Long\u00A0\u00A0";
                currentMessage = rev ? "Expect " + dir2 + " Yld as FT Goes " + dir1 + "\u00A0\u00A0" : "Expect " + dir2 + " Px as FT Goes " + dir1 + "\u00A0\u00A0";
                trendColor = rev ? Colors.Red : Colors.Lime;
                positionColor = rev ? Colors.Red : Colors.Lime;
                currentColor = Colors.Yellow;
            }
            else if (bearish && inShort)
            {
                var dir1 = ftUp ? "Up" : "Dn";
                var dir2 = ftUp ? "Higher" : "Lower";
                trendMessage = rev ? "Bullish\u00A0\u00A0" : "Bearish\u00A0\u00A0";
                positionMessage = rev ? "Long\u00A0\u00A0" : "Short\u00A0\u00A0";
                currentMessage = rev ? "Expect " + dir2 + " Yld as FT Goes " + dir1 + "\u00A0\u00A0" : "Expect " + dir2 + " Px as FT Goes " + dir1 + "\u00A0\u00A0";
                trendColor = rev ? Colors.Lime : Colors.Red;
                positionColor = rev ? Colors.Lime : Colors.Red;
                currentColor = Colors.Yellow;
            }

            //Next Msg
            // 1 Add2 Add on Alert SETUP 
            if (add2Enb && bullish && inLong  && hasTpUp2 && aoaUpSetup2 && !l2)
            {
                nextMessage = rev ? "Potential Add2 Short Alert if Yld " + interval + " on Next Cl > " + tpup2.ToString("0.00") + "\u00A0\u00A0" : "Potential Add2 Long Alert if Next " + interval + " Cl > " + tpup2.ToString("0.00") + "\u00A0\u00A0";
                if (rev)
                {
                    if (!double.IsNaN(tpup2) && cl[idx] > tpup2) nextColor = Colors.Red;
                    else nextColor = Colors.Yellow;
                }
                else
                {
                    if (!double.IsNaN(tpup2) && cl[idx] > tpup2) nextColor = Colors.Lime;
                    else nextColor = Colors.Yellow;

                }
            }
            else if (add2Enb && bearish && inShort && hasTpDn2 && aoaDnSetup2 && !s2)
            {
                nextMessage = rev ? "Potential Add2 Long Alert if Yld " + interval + " Cl on Next < " + tpdn2.ToString("0.00") + "\u00A0\u00A0" : "Potential Add2 Short Alert if Next " + interval + " Cl < " + tpdn2.ToString("0.00") + "\u00A0\u00A0";
                if (rev)
                {
                    if (!double.IsNaN(tpdn2) && cl[idx] < tpdn2) nextColor = Colors.Lime;
                    else nextColor = Colors.Yellow;
                }
                else
                {
                    if (!double.IsNaN(tpdn2) && cl[idx] < tpdn2) nextColor = Colors.Red;
                    else nextColor = Colors.Yellow;
                }
            }

            // 2 Add1 P alert SETUP
            else if (add1Enb && bullish && inLong && pAlertUpSetup && !l1)
            {
                nextMessage = rev ? "Potential Add1 Short if Next Bar Cl > .618 of Range" + "\u00A0\u00A0" : "Potential Add1 Long if Next Bar Cl > .618 of Range" + "\u00A0\u00A0";
                nextColor = Colors.Yellow;
            }
            else if (add1Enb && bearish && inShort && pAlertDnSetup && !s1)
            {
                nextMessage = rev ? "Potential Add1 Long if Next Bar Cl < .618 of Range" + "\u00A0\u00A0" : "Potential Add1 Short if Next Bar Cl < .618 of Range" + "\u00A0\u00A0";
                nextColor = Colors.Yellow;
            }
            
            // 3 Add3 Two bar alert SETUP 
            else if (add3Enb && bullish && inLong  && tbu1Setup && !l3)
            {
                var price = cl[idx];
                nextMessage = rev ? "Potential Add3 Short Alert: if Next " + interval + " Cl > " + price.ToString("0.00") + "\u00A0\u00A0" : "Potential Add3 Long Alert: If Next " + interval + " Cl > " + price.ToString("0.00") + "\u00A0\u00A0";
                nextColor = Colors.Yellow;
            }
            else if (add3Enb && bearish && inShort && tbd1Setup && !s3)
            {
                var price = cl[idx];
                nextMessage = rev ? "Potential Add3 Long Alert: if Next " + interval + " Cl < " + price.ToString("0.00") + "\u00A0\u00A0" : "Potential Add3 Short Alert: If Next " + interval + " Cl < " + price.ToString("0.00") + "\u00A0\u00A0";
                nextColor = Colors.Yellow;
            }

            // 4  Exh Signal Setup 
            else if (bullish && inLong  && hasTpDn2 && exhUpSetup)
            {
                nextMessage = rev ? "Potential Short Exit Exh Alert if Next " + interval + " Cl < " + tpdn2.ToString("0.00") + "\u00A0\u00A0" : "Potential Long Exit Exh Alert if Next " + interval + " Cl < " + tpdn2.ToString("0.00") + "\u00A0\u00A0";
                if (rev)
                {
                    if (!double.IsNaN(tpdn2) && cl[idx] < tpdn2) nextColor = Colors.Lime;
                    else nextColor = Colors.Yellow;
                }
                else
                {
                    if (!double.IsNaN(tpdn2) && cl[idx] < tpdn2) nextColor = Colors.Red;
                    else nextColor = Colors.Yellow;
                }
            }
            else if (bearish && inShort && hasTpUp2 && exhDnSetup)
            {
                nextMessage = rev ? "Potential Long Exit Exh Alert if Next " + interval + " Cl > " + tpup2.ToString("0.00") + "\u00A0\u00A0" : "Potential Short Exit Exh Alert if Next " + interval + " Cl > " + tpup2.ToString("0.00") + "\u00A0\u00A0";
                if (rev)
                {
                    if (!double.IsNaN(tpup2) && cl[idx] > tpup2) nextColor = Colors.Red;
                    else nextColor = Colors.Yellow;
                }
                else
                {
                    if (!double.IsNaN(tpup2) && cl[idx] > tpup2) nextColor = Colors.Lime;
                    else nextColor = Colors.Yellow;
                }
            }
            // 4a Exh Signal Get Back In AOA Setup
            else if (exhEnb && add2Enb && bullish && !inLong  && hasTpUp2 && aoaUpSetup2 && lExhaustion)
            {
                nextMessage = rev ? "Potential Add2 Short Alert if Yld " + interval + " on Next Cl > " + tpup2.ToString("0.00") + "\u00A0\u00A0" : "Potential Add2 Long Alert if Next " + interval + " Cl > " + tpup2.ToString("0.00") + "\u00A0\u00A0";
                if (rev)
                {
                    if (!double.IsNaN(tpdn2) && cl[idx] > tpdn2) nextColor = Colors.Lime;
                    else nextColor = Colors.Yellow;
                }
                else
                {
                    if (!double.IsNaN(tpdn2) && cl[idx] > tpdn2) nextColor = Colors.Red;
                    else nextColor = Colors.Yellow;
                }
            }
            else if (exhEnb && add2Enb && bearish && !inShort && hasTpDn2 && aoaDnSetup2 && sExhaustion)
            {
                nextMessage = rev ? "Potential Add2 Long Alert if Yld " + interval + " on Next Cl < " + tpdn2.ToString("0.00") + "\u00A0\u00A0" : "Potential Add2 Short Alert if Next  " + interval + " Cl < " + tpdn2.ToString("0.00") + "\u00A0\u00A0";
                if (rev)
                {
                    if (!double.IsNaN(tpdn2) && cl[idx] < tpdn2) nextColor = Colors.Lime;
                    else nextColor = Colors.Yellow;
                }
                else
                {
                    if (!double.IsNaN(tpdn2) && cl[idx] < tpdn2) nextColor = Colors.Red;
                    else nextColor = Colors.Yellow;
                }
            }
            // 4b Exh Sig Get Back in PAlert Setup
            else if (exhEnb && add1Enb && bullish && !inLong  && pAlertUpSetup && lExhaustion)
            {
                {
                    nextMessage = rev ? "Potential Add1 Short if Next Bar Cl > .618 of Range" + "\u00A0\u00A0" : "Potential Add1 Long if Next Bar Cl > .618 of Range" + "\u00A0\u00A0";
                    nextColor = Colors.Yellow;
                }
            }
            else if (exhEnb && add1Enb && bearish && !inShort && pAlertDnSetup && sExhaustion)
            {
                {
                    nextMessage = rev ? "Potential Add1 Long if Next Bar Cl < .618 of Range" + "\u00A0\u00A0" : "Potential Add1 Short if Next Bar Cl < .618 of Range" + "\u00A0\u00A0";
                    nextColor = Colors.Yellow;
                }
            }
            // 4c Exh Signal Get Back In 2 bar Setup
            else if (exhEnb && add3Enb && bullish && !inLong  && tbu1Setup && lExhaustion)
            {
                var price = cl[idx - 1];
                nextMessage = rev ? "Potential Add3 Short Alert: if Yld  " + interval + " on Next Cl > " + price.ToString("0.00") + "\u00A0\u00A0" : "Potential Add3 Long Alert: If Mext " + interval + " Cl > " + price.ToString("0.00") + "\u00A0\u00A0";
                nextColor = Colors.Yellow;
            }
            else if (exhEnb && add3Enb && bearish && !inShort && tbd1Setup && sExhaustion)
            {
                var price = cl[idx - 1];
                nextMessage = rev ? "Potential Add3 Long Alert: if Yld  " + interval + " on Next Cl < " + price.ToString("0.00") + "\u00A0\u00A0" : "Potential Add3 Short Alert: If Next " + interval + " Cl < " + price.ToString("0.00") + "\u00A0\u00A0";
                nextColor = Colors.Yellow;
            }
            // 4d Exh Out Condition TRUE
            else if (exhEnb && bullish && !inLong)
            {
                nextMessage = rev ? "Out: Wait for New Short" + "\u00A0\u00A0" : "Out: Wait for New Long" + "\u00A0\u00A0";
                nextColor = Colors.Yellow;
            }
            else if (exhEnb && bearish && !inShort)
            {
                nextMessage = rev ? "Out: Wait for New Long" + "\u00A0\u00A0" : "Out: Wait for New Short" + "\u00A0\u00A0";
                nextColor = Colors.Yellow;
            }

            // 5 Retrace Signal Setup 
            else if (redEnb && bullish && inLong  && hasTpDn2)
            {
                nextMessage = rev ? "Potential Retrace Down Alert if Next " + interval + " Cl < " + tpdn2.ToString("0.00") + "\u00A0\u00A0" : "Potential Retrace Dn Alert if Next " + interval + " Cl < " + tpdn2.ToString("0.00") + "\u00A0\u00A0";
                if (rev)
                {
                    if (!double.IsNaN(tpdn2) && cl[idx] < tpdn2) nextColor = Colors.Lime;
                    else nextColor = Colors.Yellow;
                }
                else
                {
                    if (!double.IsNaN(tpdn2) && cl[idx] < tpdn2) nextColor = Colors.Red;
                    else nextColor = Colors.Yellow;
                }
            }
            else if (redEnb && bearish && inShort && hasTpUp2)
            {
                nextMessage = rev ? "Potential Retrace Down Alert if Next " + interval + " Cl > " + tpup2.ToString("0.00") + "\u00A0\u00A0" : "Potential Retrace Up Alert if Next " + interval + " Cl > " + tpup2.ToString("0.00") + "\u00A0\u00A0";
                if (rev)
                {
                    if (!double.IsNaN(tpup2) && cl[idx] > tpup2) nextColor = Colors.Red;
                    else nextColor = Colors.Yellow;
                }
                else
                {
                    if (!double.IsNaN(tpup2) && cl[idx] > tpup2) nextColor = Colors.Lime;
                    else nextColor = Colors.Yellow;
                }
            }
            // 5a Retrace Signal Get Back In AOA Setup
            else if (redEnb && add2Enb && bullish && !inLong  && aoaUpSetup2 && !lRetrace)
            {
                nextMessage = rev ? "Potential Add2 Short Alert if Yld " + interval + " on Next Cl > " + tpup2.ToString("0.00") + "\u00A0\u00A0" : "Potential Add2 Long Alert if Next " + interval + " Cl > " + tpup2.ToString("0.00") + "\u00A0\u00A0";
                if (rev)
                {
                    if (!double.IsNaN(tpup2) && cl[idx] > tpup2) nextColor = Colors.Red;
                    else nextColor = Colors.Yellow;
                }
                else
                {
                    if (!double.IsNaN(tpup2) && cl[idx] > tpup2) nextColor = Colors.Lime;
                    else nextColor = Colors.Yellow;
                }
            }
            else if (redEnb && add2Enb && bearish && !inShort && aoaDnSetup2 && !sRetrace)
            {
                nextMessage = rev ? "Potential Add2 Long Alert if Yld " + interval + " on Next Cl < " + tpdn2.ToString("0.00") + "\u00A0\u00A0" : "Potential Add2 Short Alert if Next " + interval + " Cl < " + tpdn2.ToString("0.00") + "\u00A0\u00A0";
                if (rev)
                {
                    if (!double.IsNaN(tpup2) && cl[idx] < tpup2) nextColor = Colors.Red;
                    else nextColor = Colors.Yellow;
                }
                else
                {
                    if (!double.IsNaN(tpup2) && cl[idx] < tpup2) nextColor = Colors.Lime;
                    else nextColor = Colors.Yellow;
                }
            }
            // 5b Retrace Sig Get Back in PAlert Setup
            else if (redEnb && add1Enb && bullish && !inLong && pAlertUpSetup && !lRetrace)
            {
                {
                    nextMessage = rev ? "Potential Add1 Short if Next Bar Cl > .618 of Range" + "\u00A0\u00A0" : "Potential Add1 Long if Next Bar Cl > .618 of Range" + "\u00A0\u00A0";
                    nextColor = Colors.Yellow;
                }
            }
            else if (redEnb && add1Enb && bearish && !inShort && pAlertDnSetup && !sRetrace)
            {
                {
                    nextMessage = rev ? "Potential Add1 Long if Next Bar Cl < .618 of Range" + "\u00A0\u00A0" : "Potential Add1 Short if Next Bar Cl < .618 of Range" + "\u00A0\u00A0";
                    nextColor = Colors.Yellow;
                }
            }
            // 5c Retrace Signal Get Back In 2 bar Setup
            else if (redEnb && add3Enb && bullish && !inLong  && tbu1Setup && !lRetrace)
            {
                var price = cl[idx - 1];
                nextMessage = rev ? "Potential Add3 Alert: if Yld  " + interval + " on Next Cl > " + price.ToString("0.00") + "\u00A0\u00A0" : "Potential Add3 Long Alert: if Next " + interval + " Cl > " + price.ToString("0.00") + "\u00A0\u00A0";
                nextColor = Colors.Yellow;
            }
            else if (redEnb && add3Enb && bearish && !inShort && tbd1Setup && !sRetrace)
            {
                var price = cl[idx - 1];
                nextMessage = rev ? "Potential Add3 Long Alert: if Yld " + interval + " on Next Cl < " + price.ToString("0.00") + "\u00A0\u00A0" : "Potential Add3 Short Alert: if Next " + interval + " Cl < " + price.ToString("0.00") + "\u00A0\u00A0";
                nextColor = Colors.Yellow;
            }         
            // 5d Retrace Out Condition TRUE
            else if (redEnb && bullish && inLong  && lRetrace)
            {
                nextMessage = rev ? "Expect Yld to Retrace Lower" : "Expect Px to Retrace Lower";
                nextColor = Colors.Yellow;
            }
            else if (redEnb && bearish && inShort && sRetrace)
            {
                nextMessage = rev ? "Expect Yld to Retrace Higher" : "Expect Px to Retrace Higher";
                nextColor = Colors.Yellow;
            }

            // 6 reverse trend setup
            //else if (bullish && inLong && midScore)
            //{
            //    nextMessage = rev ? "Potential New Trend Dn if Next " + interval + " Score < 45" : "Potential New Trend Dn if Next " + interval + " Score < 45";
            //    nextColor = Colors.Yellow;
            //}
            //else if (bearish && inShort && midScore)
            //{
            //    nextMessage = rev ? "Potential New Trend Up if Next " + interval + " Score > 55" : "Potential New Trend Up if Next " + interval + " Score > 55";
            //    nextColor = Colors.Yellow;
            //}

            // 7 stay in position - no change 
            else if (bullish && inLong)
            {
                var dir1 = ftUp ? "Up" : "Dn";
                var dir2 = ftUp ? "Higher" : "Lower";
                nextMessage = rev ? "Expect " + dir2 + " Yld as FT Goes " + dir1 + "\u00A0\u00A0" : "Expect " + dir2 + " Px as FT Goes " + dir1 + "\u00A0\u00A0";
                nextColor = Colors.Yellow;
            }
            else if (bearish && inShort)
            {
                var dir1 = ftUp ? "Up" : "Dn";
                var dir2 = ftUp ? "Higher" : "Lower";
                nextMessage = rev ? "Expect " + dir2 + " Yld as FT Goes " + dir1 + "\u00A0\u00A0" : "Expect " + dir2 + " Px as FT Goes " + dir1 + "\u00A0\u00A0";
                nextColor = Colors.Yellow;
            }

            output.Add(new Tuple<string, Color>(trendMessage, trendColor));                                                                     // 0
            output.Add(new Tuple<string, Color>(positionMessage, positionColor));                                                               // 1
            output.Add(new Tuple<string, Color>(currentMessage, currentColor));                                                                 // 2
            output.Add(new Tuple<string, Color>(nextMessage, nextColor));                                                                       // 3
            output.Add(new Tuple<string, Color>(pressureMessage, Colors.Yellow));                                                               // 4

            output.Add(new Tuple<string, Color>("Add1Up1", eventAdd1u1 ? upColor1(rev) : setupAdd1u1 ? Colors.Yellow : Colors.Transparent));      // 5
            output.Add(new Tuple<string, Color>("Add1Dn1", eventAdd1d1 ? dnColor1(rev)  : setupAdd1d1 ? Colors.Yellow : Colors.Transparent));      // 6
            output.Add(new Tuple<string, Color>("Add2Up1", eventAdd2u1 ? upColor1(rev) : setupAdd2u1 ? Colors.Yellow : Colors.Transparent));      // 7
            output.Add(new Tuple<string, Color>("Add2Dn1", eventAdd2d1 ? dnColor1(rev)  : setupAdd2d1 ? Colors.Yellow : Colors.Transparent));      // 8
            output.Add(new Tuple<string, Color>("Add3Up1", eventAdd3u1 ? upColor1(rev) : setupAdd3u1 ? Colors.Yellow : Colors.Transparent));      // 9
            output.Add(new Tuple<string, Color>("Add3Dn1", eventAdd3d1 ? dnColor1(rev)  : setupAdd3d1 ? Colors.Yellow : Colors.Transparent));      // 10
            output.Add(new Tuple<string, Color>("ShortRetUp1", eventShortRetu1 ? upColor1(rev) : (setupShortRetu1 && !setupExhu1) ? Colors.Yellow : Colors.Transparent)); // 11
            output.Add(new Tuple<string, Color>("LongRetDn1",   eventLongRetd1 ? dnColor1(rev) :  (setupLongRetd1 && !setupExhd1) ? Colors.Yellow : Colors.Transparent)); // 12
            output.Add(new Tuple<string, Color>("ExhUp1", eventExhu1 ? upColor1(rev) : setupExhu1 ? Colors.Yellow : Colors.Transparent)); // 13
            output.Add(new Tuple<string, Color>("ExhDn1", eventExhd1 ? dnColor1(rev) :  setupExhd1 ? Colors.Yellow : Colors.Transparent)); // 14

            output.Add(new Tuple<string, Color>("Add1Up2", getNextAdd1UpColor(setupAdd1u2, rev, tpup2, cl[idx])));  // 15
            output.Add(new Tuple<string, Color>("Add1Dn2", getNextAdd1DnColor(setupAdd1d2, rev, tpdn2, cl[idx])));  // 16
            output.Add(new Tuple<string, Color>("Add2Up2", getNextAdd2UpColor(setupAdd2u2, rev, tpup2, cl[idx])));  // 17
            output.Add(new Tuple<string, Color>("Add2Dn2", getNextAdd2DnColor(setupAdd2d2, rev, tpdn2, cl[idx])));  // 18
            output.Add(new Tuple<string, Color>("Add2Up2", getNextAdd3UpColor(setupAdd3u2, rev, tpup2, cl[idx])));  // 19
            output.Add(new Tuple<string, Color>("Add2Dn2", getNextAdd3DnColor(setupAdd3d2, rev, tpdn2, cl[idx])));  // 20
            output.Add(new Tuple<string, Color>("ShortRetUp2", getNextShortRetu2(setupShortRetu2 && !setupExhu2, rev, tpup2, cl[idx]))); // 21
            output.Add(new Tuple<string, Color>("LongRetDn2",  getNextLongRetd2 (setupLongRetd2 && !setupExhd2,  rev, tpdn2, cl[idx]))); // 22
            output.Add(new Tuple<string, Color>("ExhUp2", getNextExhu2(setupExhu2, rev, tpup2, cl[idx]))); // 23
            output.Add(new Tuple<string, Color>("ExhDn2", getNextExhd2(setupExhd2, rev, tpdn2, cl[idx]))); // 24


            Color positionUpColor = newLong ? upColor1(rev) : inLong ? upColor2(rev) : (bullish && !inLong) ? Colors.White : lRetrace ? Colors.Yellow : Colors.Transparent;
            Color positionDnColor = newShort ? dnColor1(rev) : inShort ? dnColor2(rev) : (bearish && !inShort) ? Colors.White : sRetrace ? Colors.Yellow : Colors.Transparent;

            output.Add(new Tuple<string, Color>("PostiionUp", positionUpColor)); // 25
            output.Add(new Tuple<string, Color>("PositionDn", positionDnColor)); // 26

            output.Add(new Tuple<string, Color>(trendChangeMessage, Colors.Yellow)); //27

            return output;
        }

        static Color upColor1(bool rev)
        {
            return rev ? Colors.Red : Colors.Lime;
        }
        static Color dnColor1(bool rev)
        {
            return rev ? Colors.Lime : Colors.Red;
        }

        static Color upColor2(bool rev)
        {
            return rev ? Colors.DarkRed : Colors.DarkGreen;
        }
        static Color dnColor2(bool rev)
        {
            return rev ? Colors.DarkGreen : Colors.DarkRed;
        }


        static Color getNextAdd1UpColor(bool setupAdd1u2, bool rev, double tpup2, double close)
        {
            var output = Colors.Transparent;
            if (setupAdd1u2)
            {
                if (rev)
                {
                    if (!double.IsNaN(tpup2) && close > tpup2) output = Colors.Red;
                    else output = Colors.Yellow;
                }
                else
                {
                    if (!double.IsNaN(tpup2) && close > tpup2) output = Colors.Lime;
                    else output = Colors.Yellow;
                }
            }
            return output;
        }

        static Color getNextAdd1DnColor(bool setupAdd1d2, bool rev, double tpdn2, double close)
        {
            var output = Colors.Transparent;
            if (setupAdd1d2)
            {
                if (rev)
                {
                    if (!double.IsNaN(tpdn2) && close < tpdn2) output = Colors.Lime;
                    else output = Colors.Yellow;
                }
                else
                {
                    if (!double.IsNaN(tpdn2) && close < tpdn2) output = Colors.Red;
                    else output = Colors.Yellow;
                }
            }
            return output;
        }

        static Color getNextAdd2UpColor(bool setupAdd1u2, bool rev, double tpup2, double close)
        {
            var output = Colors.Transparent;
            if (setupAdd1u2)
            {
                if (rev)
                {
                    if (!double.IsNaN(tpup2) && close > tpup2) output = Colors.Red;
                    else output = Colors.Yellow;
                }
                else
                {
                    if (!double.IsNaN(tpup2) && close > tpup2) output = Colors.Lime;
                    else output = Colors.Yellow;
                }
            }
            return output;
        }

        static Color getNextAdd2DnColor(bool setupAdd2d2, bool rev, double tpdn2, double close)
        {
            var output = Colors.Transparent;
            if (setupAdd2d2)
            {
                if (rev)
                {
                    if (!double.IsNaN(tpdn2) && close < tpdn2) output = Colors.Lime;
                    else output = Colors.Yellow;
                }
                else
                {
                    if (!double.IsNaN(tpdn2) && close < tpdn2) output = Colors.Red;
                    else output = Colors.Yellow;
                }
            }
            return output;
        }

        static Color getNextAdd3UpColor(bool setupAdd3u2, bool rev, double tpup2, double close)
        {
            var output = Colors.Transparent;
            if (setupAdd3u2)
            {
                if (rev)
                {
                    if (!double.IsNaN(tpup2) && close > tpup2) output = Colors.Red;
                    else output = Colors.Yellow;
                }
                else
                {
                    if (!double.IsNaN(tpup2) && close > tpup2) output = Colors.Lime;
                    else output = Colors.Yellow;
                }
            }
            return output;
        }

        static Color getNextAdd3DnColor(bool setupAdd3d2, bool rev, double tpdn2, double close)
        {
            var output = Colors.Transparent;
            if (setupAdd3d2)
            {
                if (rev)
                {
                    if (!double.IsNaN(tpdn2) && close < tpdn2) output = Colors.Lime;
                    else output = Colors.Yellow;
                }
                else
                {
                    if (!double.IsNaN(tpdn2) && close < tpdn2) output = Colors.Red;
                    else output = Colors.Yellow;
                }
            }
            return output;
        }

        static Color getNextShortRetu2(bool setupShortRetu2, bool rev, double tpup2, double close)
        {
            var output = Colors.Transparent;
            if (setupShortRetu2)
            {
                if (rev)
                {
                    if (!double.IsNaN(tpup2) && close > tpup2) output = Colors.Red;
                    else output = Colors.Yellow;
                }
                else
                {
                    if (!double.IsNaN(tpup2) && close > tpup2) output = Colors.Lime;
                    else output = Colors.Yellow;
                }
            }
            return output;
        }

        static Color getNextLongRetd2(bool setupLongRetd2, bool rev, double tpdn2, double close)
        {
            var output = Colors.Transparent;
            if (setupLongRetd2)
            {
                if (rev)
                {
                    if (!double.IsNaN(tpdn2) && close < tpdn2) output = Colors.Lime;
                    else output = Colors.Yellow;
                }
                else
                {
                    if (!double.IsNaN(tpdn2) && close < tpdn2) output = Colors.Red;
                    else output = Colors.Yellow;
                }
            }
            return output;
        }

        static Color getNextExhu2(bool setupExhu2, bool rev, double tpup2, double close)
        {
            var output = Colors.Transparent;
            if (setupExhu2)
            {
                if (rev)
                {
                    if (!double.IsNaN(tpup2) && close > tpup2) output = Colors.Red;
                    else output = Colors.Yellow;
                }
                else
                {
                    if (!double.IsNaN(tpup2) && close > tpup2) output = Colors.Lime;
                    else output = Colors.Yellow;
                }
            }
            return output;
        }
        static Color getNextExhd2(bool setupExhd2, bool rev, double tpdn2, double close)
        {
            var output = Colors.Transparent;
            if (setupExhd2)
            {
                if (rev)
                {
                    if (!double.IsNaN(tpdn2) && close < tpdn2) output = Colors.Lime;
                    else output = Colors.Yellow;
                }
                else
                {
                    if (!double.IsNaN(tpdn2) && close < tpdn2) output = Colors.Red;
                    else output = Colors.Yellow;
                }
            }
            return output;
        }

        private static List<double> getDirection(Series trend, Series ao, Series pt, Series twobar, Series exh, Series retrace,  Dictionary<string, bool> parameters) 
        {
            var add1Enb = parameters.ContainsKey("Pressure") ? parameters["Pressure"] : false;
            var add2Enb = parameters.ContainsKey("Add") ? parameters["Add"] : false;
            var add3Enb = parameters.ContainsKey("2 Bar") ? parameters["2 Bar"] : false;
            var exhEnb = parameters.ContainsKey("Exh") ? parameters["Exh"] : false;
            var retEnb = parameters.ContainsKey("Retrace") ? parameters["Retrace"] : false;

            var barCount = trend.Count;

            var dir1 = 0.0;
            var dir = Enumerable.Range(0, barCount).Select(i =>
            {
                if (i > 0)
                {
                    var lin = trend[i - 1] != 1 && trend[i] == 1;
                    var lout = trend[i - 1] == 1 && trend[i] != 1;
                    var sin = trend[i - 1] != -1 && trend[i] == -1;
                    var sout = trend[i - 1] == -1 && trend[i] != -1;

                    var le = lin || trend[i] > 0 && add1Enb && pt[i] == 1 || trend[i] > 0 && add2Enb && ao[i] == 1 || trend[i] > 0 && add3Enb && twobar[i] == 1;
                    var se = sin || trend[i] < 0 && add1Enb && pt[i] == -1 || trend[i] < 0 && add2Enb && ao[i] == -1 || trend[i] < 0 && add3Enb && twobar[i] == -1;
                    var lx = lout || exhEnb && exh[i] == -1 || retEnb && retrace[i] > 0;
                    var sx = sout || exhEnb && exh[i] ==  1 || retEnb && retrace[i] < 0;

                    dir1 = le ? 1 : se ? -1 : lx || sx ? 0 : dir1;
                }
                return dir1;
            }).ToList();

            return dir;
        }

		public static Series getStrategy(string name, Dictionary<string, List<DateTime>> times, Dictionary<string, Series[]> bars, string[] intervalList, Dictionary<string, object> referenceData)
        {
            Series output = null;

            var fields = name.Split('(');
            var strategy = fields[0].ToUpper();

            var parameters = new Dictionary<string, string>();
            if (fields.Length > 1)
            {
                var parms = fields[1].Replace(")", "").Replace(" ", "").Replace("\u0002", "").Split(';');
                foreach(var parm in parms)
                {
                    var items = parm.Split('=');
                    parameters[items[0]] = items[1];
                }
            }

            var closes = bars[intervalList[0]][3];
            var currentIndex = 0;
            for (var ii = closes.Count; ii >= 0; ii--)
            {
                if (!double.IsNaN(closes[ii]))
                {
                    currentIndex = ii;
                    break;
				}
            }


			if (strategy == "SC")
            {
                string shortTerm = intervalList[0];
                var shortTermTimes = times[shortTerm];


                Series rp = (referenceData == null) ? null : atm.calculateRelativePrice(intervalList[0], bars[intervalList[0]], referenceData, 5);
                var score = getScore(times, bars, intervalList);

                output = atm.calculateSCSig(score, rp, 2);
                //var scUp = score >= 50;
                //var scDn = score < 50;
                //var up1 = Series.Equal(scUp, 1);
                //var dn1 = Series.Equal(scDn, 1);
                // output = up1 - dn1;
            }

            else if (strategy == "5 20")
            {
                string shortTerm = intervalList[0];

                var ma5 = Series.SimpleMovingAverage(bars[shortTerm][3], 5);
                var ma20 = Series.SimpleMovingAverage(bars[shortTerm][3], 20);
                var up1 = ma5 >= ma20; // ma5.CrossesAbove(ma20);
                var dn1 = ma5 < ma20; //  ma5.CrossesBelow(ma20);

                output = up1 - dn1;
            }

            else if (strategy == "ATM RESEARCH")
            {
                var interval = intervalList[0];
                Series op = bars[interval][0];
                Series hi = bars[interval][1];
                Series lo = bars[interval][2];
                Series cl = bars[interval][3];

                var pt = atm.calculatePressureAlert(op, hi, lo, cl);
                var twobar = atm.calculateTwoBarPattern(op, hi, lo, cl);
                var ft = atm.calculateFT(hi, lo, cl);
                var sc = atm.getScore(times, bars, intervalList);
                var exh = atm.calculateExhaustion(hi, lo, cl, atm.ExhaustionLevelSelection.AllLevels);
                var rp = atm.calculateRelativePrice(interval, bars[interval], referenceData, 5);
                var scSig = atm.calculateSCSig(sc, rp, 2);
                var a1 = atm.calculatePressureAlert(op, hi, lo, cl);
                var a2 = ft.TurnsUp() - ft.TurnsDown();
                var a3 = atm.calculateTwoBarPattern(op, hi, lo, cl);

                Series trend = scSig;

                var lretrace = atm.setReset(trend > 0 & a2 < 0, trend < 0 | a1.ShiftRight(1) > 0 | a2 > 0 | a3.ShiftRight(1) > 0);
                var sretrace = atm.setReset(trend < 0 & a2 > 0, trend > 0 | a1.ShiftRight(1) < 0 | a2 < 0 | a3.ShiftRight(1) < 0);
                var retrace = lretrace - sretrace;

                var parms = new Dictionary<string, bool>();
                parms["Pressure"] = parameters.ContainsKey("Pressure") ? bool.Parse(parameters["Pressure"]) : false;
                parms["Add"] = parameters.ContainsKey("Add") ? bool.Parse(parameters["Add"]) : false;
                parms["2 Bar"] = parameters.ContainsKey("2 Bar") ? bool.Parse(parameters["2 Bar"]) : false;
                parms["Exh"] = parameters.ContainsKey("Exh") ? bool.Parse(parameters["Exh"]) : false;
                parms["Retrace"] = parameters.ContainsKey("Retrace") ? bool.Parse(parameters["Retrace"]) : false;

                List<double> dir = getDirection(trend, a2, pt, twobar, exh, retrace, parms);

                output = new Series(dir);
            }

            else if (strategy == "PR")
            {
                Series PR = Conditions.calculatePositionRatio1(times, bars, intervalList, referenceData, 1);

                var pr = new Series(PR.Count, 0);

                for (int ii = 1; ii < PR.Count; ii++)
                {
                    if (PR[ii - 1] != 1.5 && PR[ii] == 1.5) pr[ii] = 4;  //newLong 
                    else if (PR[ii - 1] != -1.5 && PR[ii] == -1.5) pr[ii] = -4;  //newShort
                    else if (PR[ii] == 1.0) pr[ii] = 3;  //reduceLong
                    else if (PR[ii] == -1.0) pr[ii] = -3;  //reduceShort
                    else if (PR[ii - 1] == 1.0 && PR[ii] == 1.0) pr[ii] = 5;  //reduceLong 2nd bar
                    else if (PR[ii - 1] == -1.0 && PR[ii] == -1.0) pr[ii] = -5; //reduceShort 2nd bar
                    else if (PR[ii - 1] >= 1.0 && PR[ii] < 0) pr[ii] = 2;  //exit
                    else if (PR[ii - 1] <= -1.0 && PR[ii] > 0) pr[ii] = -2; //exit
                    else if (PR[ii] == 1.5) pr[ii] = 1;  //isLong
                    else if (PR[ii] == -1.5) pr[ii] = -1;  //isShort
                }

                var up1 = Series.Greater(pr, 0) * pr;
                var dn1 = Series.Less(pr, 0) * pr;

                output = up1 + dn1;
            }

            else if (strategy == "FTTP | CL")
            {
                string shortTerm = intervalList[0];
                string midTerm = (intervalList.Length > 1) ? intervalList[1] : intervalList[0];

                var shortTermTimes = times[shortTerm];
                Series shortTermHi = bars[shortTerm][1];
                Series shortTermLo = bars[shortTerm][2];
                Series shortTermCl = bars[shortTerm][3];

                var midTermTimes = times[midTerm];
                Series midTermHi = bars[midTerm][1]; //midTermHi.Append(double.NaN);
                Series midTermLo = bars[midTerm][2]; //midTermLo.Append(double.NaN);
                Series midTermCl = bars[midTerm][3]; //midTermCl.Append(double.NaN);

                var ftMidTerm = atm.calculateFT(midTermHi, midTermLo, midTermCl);
                var fttpMidTerm = atm.calculateFastTurningPoints(midTermHi, midTermLo, midTermCl, ftMidTerm);
                var fttpUp = fttpMidTerm[0];
                var fttpDn = fttpMidTerm[1];

                var stC = shortTermCl;

                Series ftUp = atm.sync(fttpUp, midTerm, shortTerm, midTermTimes, shortTermTimes);
                Series ftDn = atm.sync(fttpDn, midTerm, shortTerm, midTermTimes, shortTermTimes);

                Series up1 = stC > ftUp;
                Series dn1 = stC < ftDn;

                output = up1.ReplaceNaN(0) - dn1.ReplaceNaN(0);
            }

            else if (strategy == "FT | P")
            {
                string shortTerm = intervalList[0];
                string midTerm = (intervalList.Length > 1) ? intervalList[1] : intervalList[0];

                var shortTermTimes = times[shortTerm];
                Series shortTermOp = bars[shortTerm][0];
                Series shortTermHi = bars[shortTerm][1];
                Series shortTermLo = bars[shortTerm][2];
                Series shortTermCl = bars[shortTerm][3];

                var midTermTimes = times[midTerm];
                Series midTermHi = bars[midTerm][1];
                Series midTermLo = bars[midTerm][2];
                Series midTermCl = bars[midTerm][3];

                var ftShortTerm = atm.calculateFT(shortTermHi, shortTermLo, shortTermCl);
                var ftMidTerm = atm.calculateFT(midTermHi, midTermLo, midTermCl);
                var pShortTerm = atm.calculatePressureAlert(shortTermOp, shortTermHi, shortTermLo, shortTermCl);

                Series ftUp = atm.sync(ftMidTerm.IsRising(), midTerm, shortTerm, midTermTimes, shortTermTimes);
                Series ftDn = atm.sync(ftMidTerm.IsFalling(), midTerm, shortTerm, midTermTimes, shortTermTimes);
                //Series pSTUp = pShortTerm.TurnsUp();
                //Series pSTDn = pShortTerm.TurnsDown();
                Series ftTU = ftShortTerm.TurnsUp();
                Series ftTD = ftShortTerm.TurnsDown();

                var count = shortTermTimes.Count;

                output = new Series(count);
                var dir = 0;
                var cnt = 0;
                for (var ii = 0; ii <= currentIndex; ii++)
                {
                    if (ftUp[ii] == 1 && pShortTerm[ii] == 1)
                    {
                        dir = 1;
                        cnt = 0;
                    }
                    if (ftDn[ii] == 1 && pShortTerm[ii] == -1)
                    {
                        dir = -1;
                        cnt = 0;
                    }
                    if (dir != 0)
                    {
                        cnt++;
                        if (cnt == 6)
                        {
                            dir = 0;
                        }
                    }
                    if (dir > 0)
                    {
                        if (ftShortTerm[ii] >= 50 || ftTD[ii] == 1)
                        {
                            dir = 0;
                        }
                    }
                    if (dir < 0)
                    {
                        if (ftShortTerm[ii] <= 50 || ftTU[ii] == 1)
                        {
                            dir = 0;
                        }
                    }
                    output[ii] = dir;
                }
            }

            else if (strategy == "FT | FT")
            {
                string shortTerm = intervalList[0];
                string midTerm = (intervalList.Length > 1) ? intervalList[1] : intervalList[0];

                var shortTermTimes = times[shortTerm];
                Series shortTermHi = bars[shortTerm][1];
                Series shortTermLo = bars[shortTerm][2];
                Series shortTermCl = bars[shortTerm][3];

                var midTermTimes = times[midTerm];
                Series midTermHi = bars[midTerm][1];
                Series midTermLo = bars[midTerm][2];
                Series midTermCl = bars[midTerm][3];

                var ftMidTerm = atm.calculateFT(midTermHi, midTermLo, midTermCl);
                var ftShortTerm = atm.calculateFT(shortTermHi, shortTermLo, shortTermCl);

                Series mtup = atm.sync(ftMidTerm.IsRising(), midTerm, shortTerm, midTermTimes, shortTermTimes);
                Series mtdn = atm.sync(ftMidTerm.IsFalling(), midTerm, shortTerm, midTermTimes, shortTermTimes);
                Series stup = ftShortTerm.IsRising() & ftShortTerm < 50; // st ft value filter added
                Series stdn = ftShortTerm.IsFalling() & ftShortTerm > 50;

                var mtUpStUp = Series.Equal(mtup, 1).And(Series.Equal(stup, 1));
                var mtUpStDn = Series.Equal(mtup, 0).And(Series.Equal(stup, 1));

                var mtDnStDn = Series.Equal(mtdn, 1).And(Series.Equal(stdn, 1));
                var mtDnStUp = Series.Equal(mtdn, 0).And(Series.Equal(stdn, 1));

                var nl = mtup.And(Series.Not(stup.ShiftRight(1))).And(stup) * 4; // st ft turns up
                var sl = mtup.And(stup.ShiftRight(1)).And(stup) * 3;             // st ft going up               
                var rl = mtup.And(stup.ShiftRight(1)).And(Series.Not(stup)) * 2; // st ft turns down
                var wl = mtup.And(Series.Not(stup.ShiftRight(1))).And(Series.Not(stup)) * 1;
                var up = nl + sl + rl + wl;

                var ns = mtdn.And(Series.Not(stdn.ShiftRight(1))).And(stdn) * 4;
                var ss = mtdn.And(stdn.ShiftRight(1)).And(stdn) * 3;
                var rs = mtdn.And(stdn.ShiftRight(1)).And(Series.Not(stdn)) * 2;
                var ws = mtdn.And(Series.Not(stdn.ShiftRight(1))).And(Series.Not(stdn)) * 1;
                var dn = ns + ss + rs + ws;

                output = (up > 2) - (dn > 2); // -1 to 1
            }

            else if (strategy == "FT || FT")
            {
                string shortTerm = intervalList[0];
                string longTerm = (intervalList.Length > 2) ? intervalList[2] : intervalList[0];

                var shortTermTimes = times[shortTerm];
                Series shortTermHi = bars[shortTerm][1];
                Series shortTermLo = bars[shortTerm][2];
                Series shortTermCl = bars[shortTerm][3];

                var longTermTimes = times[longTerm];
                Series longTermHi = bars[longTerm][1];
                Series longTermLo = bars[longTerm][2];
                Series longTermCl = bars[longTerm][3];

                var ftLongTerm = atm.calculateFT(longTermHi, longTermLo, longTermCl);
                var ftShortTerm = atm.calculateFT(shortTermHi, shortTermLo, shortTermCl);

                Series ltup = atm.sync(ftLongTerm.IsRising(), longTerm, shortTerm, longTermTimes, shortTermTimes);
                Series ltdn = atm.sync(ftLongTerm.IsFalling(), longTerm, shortTerm, longTermTimes, shortTermTimes);
                Series stup = ftShortTerm.IsRising();
                Series stdn = ftShortTerm.IsFalling();

                var ltUpStUp = Series.Equal(ltup, 1).And(Series.Equal(stup, 1));
                var ltUpStDn = Series.Equal(ltup, 0).And(Series.Equal(stup, 1));

                var ltDnStDn = Series.Equal(ltdn, 1).And(Series.Equal(stdn, 1));
                var ltDnStUp = Series.Equal(ltdn, 0).And(Series.Equal(stdn, 1));

                var nl = ltup.And(Series.Not(stup.ShiftRight(1))).And(stup) * 4;
                var sl = ltup.And(stup.ShiftRight(1)).And(stup) * 3;
                var rl = ltup.And(stup.ShiftRight(1)).And(Series.Not(stup)) * 2;
                var wl = ltup.And(Series.Not(stup.ShiftRight(1))).And(Series.Not(stup)) * 1;
                var up = nl + sl + rl + wl;

                var ns = ltdn.And(Series.Not(stdn.ShiftRight(1))).And(stdn) * 4;
                var ss = ltdn.And(stdn.ShiftRight(1)).And(stdn) * 3;
                var rs = ltdn.And(stdn.ShiftRight(1)).And(Series.Not(stdn)) * 2;
                var ws = ltdn.And(Series.Not(stdn.ShiftRight(1))).And(Series.Not(stdn)) * 1;
                var dn = ns + ss + rs + ws;

                output = (up > 2) - (dn > 2); // -1 to 1
            }


            else if (strategy == "FT | ST")
            {

                string shortTerm = intervalList[0];
                string midTerm = (intervalList.Length > 1) ? intervalList[1] : intervalList[0];

                var shortTermTimes = times[shortTerm];
                Series shortTermHi = bars[shortTerm][1];
                Series shortTermLo = bars[shortTerm][2];
                Series shortTermCl = bars[shortTerm][3];

                var midTermTimes = times[midTerm];
                Series midTermHi = bars[midTerm][1];
                Series midTermLo = bars[midTerm][2];
                Series midTermCl = bars[midTerm][3];

                var ftMidTerm = atm.calculateFT(midTermHi, midTermLo, midTermCl);
                var stup = atm.calculateSTUp(shortTermHi, shortTermLo, shortTermCl);
                var stdn = atm.calculateSTDn(shortTermHi, shortTermLo, shortTermCl);

                Series mtup = atm.sync(ftMidTerm.IsRising(), midTerm, shortTerm, midTermTimes, shortTermTimes);
                Series mtdn = atm.sync(ftMidTerm.IsFalling(), midTerm, shortTerm, midTermTimes, shortTermTimes);

                var nl = mtup.And(Series.Not(stup.ShiftRight(1))).And(stup) * 4;
                var sl = mtup.And(stup.ShiftRight(1)).And(stup) * 3;
                var rl = mtup.And(stup.ShiftRight(1)).And(Series.Not(stup)) * 2;
                var wl = mtup.And(Series.Not(stup.ShiftRight(1))).And(Series.Not(stup)) * 1;
                var up = nl + sl + rl + wl;

                var ns = mtdn.And(Series.Not(stdn.ShiftRight(1))).And(stdn) * 4;
                var ss = mtdn.And(stdn.ShiftRight(1)).And(stdn) * 3;
                var rs = mtdn.And(stdn.ShiftRight(1)).And(Series.Not(stdn)) * 2;
                var ws = mtdn.And(Series.Not(stdn.ShiftRight(1))).And(Series.Not(stdn)) * 1;
                var dn = ns + ss + rs + ws;

                output = (up > 2) - (dn > 2); // -1 to 1
            }

            else if (strategy == "FT | ST PR")
            {
                Series PR = Conditions.calculatePositionRatio1(times, bars, intervalList, referenceData, 1);

                string shortTerm = intervalList[0];
                string midTerm = (intervalList.Length > 1) ? intervalList[1] : intervalList[0];

                var shortTermTimes = times[shortTerm];
                Series shortTermHi = bars[shortTerm][1];
                Series shortTermLo = bars[shortTerm][2];
                Series shortTermCl = bars[shortTerm][3];

                var midTermTimes = times[midTerm];
                Series midTermHi = bars[midTerm][1];
                Series midTermLo = bars[midTerm][2];
                Series midTermCl = bars[midTerm][3];

                var ftMidTerm = atm.calculateFT(midTermHi, midTermLo, midTermCl);
                var stSTUp = atm.calculateSTUp(shortTermHi, shortTermLo, shortTermCl);
                var stSTDn = atm.calculateSTDn(shortTermHi, shortTermLo, shortTermCl);

                Series ftUp = atm.sync(ftMidTerm.IsRising(), midTerm, shortTerm, midTermTimes, shortTermTimes);
                Series ftDn = atm.sync(ftMidTerm.IsFalling(), midTerm, shortTerm, midTermTimes, shortTermTimes);

                var pr = new Series(PR.Count, 0);
                for (int ii = 1; ii < PR.Count; ii++)
                {
                    if (PR[ii - 1] != 1.5 && PR[ii] == 1.5) pr[ii] = 4;
                    else if (PR[ii - 1] != -1.5 && PR[ii] == -1.5) pr[ii] = -4;
                    else if (PR[ii - 1] == 1.5 && PR[ii] == 1.0) pr[ii] = 3;
                    else if (PR[ii - 1] == -1.5 && PR[ii] == -1.0) pr[ii] = -3;
                    else if (PR[ii - 1] >= 1.0 && PR[ii] < 0) pr[ii] = 2;
                    else if (PR[ii - 1] <= -1.0 && PR[ii] > 0) pr[ii] = -2;
                    else if (PR[ii - 1] == 1.5 && PR[ii] == 1.5) pr[ii] = 1;
                    else if (PR[ii - 1] == -1.5 && PR[ii] == -1.5) pr[ii] = -1;
                    else pr[ii] = 0;
                }

                var up1 = Series.Equal(ftUp, 1) * Series.Equal(stSTUp, 1) * Series.Greater(pr, 0) * pr;
                var dn1 = Series.Equal(ftDn, 1) * Series.Equal(stSTDn, 1) * Series.Less(pr, 0) * pr;

                output = (up1 > 2) - (dn1 < -2); // -1 to 1
            }


            else if (strategy == "FT | SC")
            {
                string shortTerm = intervalList[0];
                string midTerm = (intervalList.Length > 1) ? intervalList[1] : intervalList[0];
                var score = getScore(times, bars, intervalList);
                Series rp = calculateRelativePrice(shortTerm, bars[shortTerm], referenceData, 5);
                Series sc = atm.calculateSCSig(score, rp, 2);

                var shortTermTimes = times[shortTerm];
                var midTermTimes = times[midTerm];
                Series midTermHi = bars[midTerm][1];
                Series midTermLo = bars[midTerm][2];
                Series midTermCl = bars[midTerm][3];
                var ftMidTerm = atm.calculateFT(midTermHi, midTermLo, midTermCl);

                Series mtup = atm.sync(ftMidTerm.IsRising(), midTerm, shortTerm, midTermTimes, shortTermTimes);
                Series mtdn = atm.sync(ftMidTerm.IsFalling(), midTerm, shortTerm, midTermTimes, shortTermTimes);



                //string shortTerm = intervalList[0];
                //            string midTerm = (intervalList.Length > 1) ? intervalList[1] : intervalList[0];

                //            var shortTermTimes = times[shortTerm];

                //            var midTermTimes = times[midTerm];
                //            Series midTermHi = bars[midTerm][1];
                //            Series midTermLo = bars[midTerm][2];
                //            Series midTermCl = bars[midTerm][3];

                //            var scores = getScore(times, bars, intervalList);
                //            var scSTUp = scores >= 50;
                //            var scSTDn = scores < 50;

                //            var ftMidTerm = atm.calculateFT(midTermHi, midTermLo, midTermCl);

                //            Series mtup = atm.sync(ftMidTerm.IsRising(), midTerm, shortTerm, midTermTimes, shortTermTimes);
                //            Series mtdn = atm.sync(ftMidTerm.IsFalling(), midTerm, shortTerm, midTermTimes, shortTermTimes);
                Series scUp = sc > 0;
                Series scDn = sc < 0;

                var nl = mtup.And(Series.Not(scUp.ShiftRight(1))).And(scUp) * 4;
                var sl = mtup.And(scUp.ShiftRight(1)).And(scUp) * 3;
                var rl = mtup.And(scUp.ShiftRight(1)).And(Series.Not(scUp)) * 2;
                var wl = mtup.And(Series.Not(scUp.ShiftRight(1))).And(Series.Not(scUp)) * 1;
                var up = nl + sl + rl + wl;

                var ns = mtdn.And(Series.Not(scDn.ShiftRight(1))).And(scDn) * 4;
                var ss = mtdn.And(scDn.ShiftRight(1)).And(scDn) * 3;
                var rs = mtdn.And(scDn.ShiftRight(1)).And(Series.Not(scDn)) * 2;
                var ws = mtdn.And(Series.Not(scDn.ShiftRight(1))).And(Series.Not(scDn)) * 1;
                var dn = ns + ss + rs + ws;

                output = (up > 2) - (dn > 2); // -1 to 1
            }

            else if (strategy == "FT | SC PR")
            {
                Series PR = Conditions.calculatePositionRatio1(times, bars, intervalList, referenceData, 1);

                string shortTerm = intervalList[0];
                string midTerm = (intervalList.Length > 1) ? intervalList[1] : intervalList[0];

                var shortTermTimes = times[shortTerm];

                var midTermTimes = times[midTerm];
                Series midTermHi = bars[midTerm][1];
                Series midTermLo = bars[midTerm][2];
                Series midTermCl = bars[midTerm][3];

                var scores = getScore(times, bars, intervalList);
                var scSTUp = scores >= 50;
                var scSTDn = scores < 50;

                var ftMidTerm = atm.calculateFT(midTermHi, midTermLo, midTermCl);

                Series ftUp = atm.sync(ftMidTerm.IsRising(), midTerm, shortTerm, midTermTimes, shortTermTimes);
                Series ftDn = atm.sync(ftMidTerm.IsFalling(), midTerm, shortTerm, midTermTimes, shortTermTimes);

                var pr = new Series(PR.Count, 0);
                for (int ii = 1; ii < PR.Count; ii++)
                {
                    if (PR[ii - 1] != 1.5 && PR[ii] == 1.5) pr[ii] = 4;
                    else if (PR[ii - 1] != -1.5 && PR[ii] == -1.5) pr[ii] = -4;
                    else if (PR[ii - 1] == 1.5 && PR[ii] == 1.0) pr[ii] = 3;
                    else if (PR[ii - 1] == -1.5 && PR[ii] == -1.0) pr[ii] = -3;
                    else if (PR[ii - 1] >= 1.0 && PR[ii] < 0) pr[ii] = 2;
                    else if (PR[ii - 1] <= -1.0 && PR[ii] > 0) pr[ii] = -2;
                    else if (PR[ii - 1] == 1.5 && PR[ii] == 1.5) pr[ii] = 1;
                    else if (PR[ii - 1] == -1.5 && PR[ii] == -1.5) pr[ii] = -1;
                    else pr[ii] = 0;
                }

                var up1 = Series.Equal(ftUp, 1) * Series.Equal(scSTUp, 1) * Series.Greater(pr, 0) * pr;
                var dn1 = Series.Equal(ftDn, 1) * Series.Equal(scSTDn, 1) * Series.Less(pr, 0) * pr;

                output = (up1 > 2) - (dn1 < -2); // -1 to 1
            }

            else if (strategy == "FT | TSB")
            {
                string shortTerm = intervalList[0];
                string midTerm = (intervalList.Length > 1) ? intervalList[1] : intervalList[0];

                var shortTermTimes = times[shortTerm];
                Series shortTermHi = bars[shortTerm][1];
                Series shortTermLo = bars[shortTerm][2];
                Series shortTermCl = bars[shortTerm][3];

                var midTermTimes = times[midTerm];
                Series midTermHi = bars[midTerm][1];
                Series midTermLo = bars[midTerm][2];
                Series midTermCl = bars[midTerm][3];

                var ftMidTerm = atm.calculateFT(midTermHi, midTermLo, midTermCl);
                var tsbSTUp = atm.calculateTSBUp2(shortTermHi, shortTermLo, shortTermCl);
                var tsbSTDn = atm.calculateTSBDn2(shortTermHi, shortTermLo, shortTermCl);

                Series ftUp = atm.sync(ftMidTerm.IsRising(), midTerm, shortTerm, midTermTimes, shortTermTimes);
                Series ftDn = atm.sync(ftMidTerm.IsFalling(), midTerm, shortTerm, midTermTimes, shortTermTimes);

                var up1 = Series.Equal(ftUp, 1).And(Series.Equal(tsbSTUp, 1));
                var dn1 = Series.Equal(ftDn, 1).And(Series.Equal(tsbSTDn, 1));
                output = up1 - dn1;
            }

            else if (strategy == "FT | TSB PR")
            {
                Series PR = Conditions.calculatePositionRatio1(times, bars, intervalList, referenceData, 1);

                string shortTerm = intervalList[0];
                string midTerm = (intervalList.Length > 1) ? intervalList[1] : intervalList[0];

                var shortTermTimes = times[shortTerm];
                Series shortTermHi = bars[shortTerm][1];
                Series shortTermLo = bars[shortTerm][2];
                Series shortTermCl = bars[shortTerm][3];

                var midTermTimes = times[midTerm];
                Series midTermHi = bars[midTerm][1];
                Series midTermLo = bars[midTerm][2];
                Series midTermCl = bars[midTerm][3];

                var ftMidTerm = atm.calculateFT(midTermHi, midTermLo, midTermCl);
                var tsbSTUp = atm.calculateTSBUp2(shortTermHi, shortTermLo, shortTermCl);
                var tsbSTDn = atm.calculateTSBDn2(shortTermHi, shortTermLo, shortTermCl);

                Series ftUp = atm.sync(ftMidTerm.IsRising(), midTerm, shortTerm, midTermTimes, shortTermTimes);
                Series ftDn = atm.sync(ftMidTerm.IsFalling(), midTerm, shortTerm, midTermTimes, shortTermTimes);

                var pr = new Series(PR.Count, 0);
                for (int ii = 1; ii < PR.Count; ii++)
                {
                    if (PR[ii - 1] != 1.5 && PR[ii] == 1.5) pr[ii] = 4;
                    else if (PR[ii - 1] != -1.5 && PR[ii] == -1.5) pr[ii] = -4;
                    else if (PR[ii - 1] == 1.5 && PR[ii] == 1.0) pr[ii] = 3;
                    else if (PR[ii - 1] == -1.5 && PR[ii] == -1.0) pr[ii] = -3;
                    else if (PR[ii - 1] >= 1.0 && PR[ii] < 0) pr[ii] = 2;
                    else if (PR[ii - 1] <= -1.0 && PR[ii] > 0) pr[ii] = -2;
                    else if (PR[ii - 1] == 1.5 && PR[ii] == 1.5) pr[ii] = 1;
                    else if (PR[ii - 1] == -1.5 && PR[ii] == -1.5) pr[ii] = -1;
                    else pr[ii] = 0;
                }

                var up1 = Series.Equal(ftUp, 1) * Series.Equal(tsbSTUp, 1) * Series.Greater(pr, 0) * pr;
                var dn1 = Series.Equal(ftDn, 1) * Series.Equal(tsbSTDn, 1) * Series.Less(pr, 0) * pr;

                output = (up1 > 2) - (dn1 < -2); // -1 to 1
            }

            else if (strategy == "ST | FT")
            {

                string shortTerm = intervalList[0];
                string midTerm = (intervalList.Length > 1) ? intervalList[1] : intervalList[0];

                var shortTermTimes = times[shortTerm];
                Series shortTermHi = bars[shortTerm][1];
                Series shortTermLo = bars[shortTerm][2];
                Series shortTermCl = bars[shortTerm][3];

                var midTermTimes = times[midTerm];
                Series midTermHi = bars[midTerm][1];
                Series midTermLo = bars[midTerm][2];
                Series midTermCl = bars[midTerm][3];

                var stMidTermUp = atm.calculateSTUp(midTermHi, midTermLo, midTermCl);
                var stMidTermDn = atm.calculateSTDn(midTermHi, midTermLo, midTermCl);
                var ftShortTerm = atm.calculateFT(shortTermHi, shortTermLo, shortTermCl);

                Series stUp = atm.sync(stMidTermUp, midTerm, shortTerm, midTermTimes, shortTermTimes);
                Series stDn = atm.sync(stMidTermDn, midTerm, shortTerm, midTermTimes, shortTermTimes);
                Series ftSTUp = ftShortTerm.IsRising();
                Series ftSTDn = ftShortTerm.IsFalling();

                var up1 = Series.Equal(stUp, 1).And(Series.Equal(ftSTUp, 1));
                var dn1 = Series.Equal(stDn, 1).And(Series.Equal(ftSTDn, 1));

                output = up1 - dn1;
            }

            else if (strategy == "ST | ST")
            {

                string shortTerm = intervalList[0];
                string midTerm = (intervalList.Length > 1) ? intervalList[1] : intervalList[0];

                var shortTermTimes = times[shortTerm];
                Series shortTermHi = bars[shortTerm][1];
                Series shortTermLo = bars[shortTerm][2];
                Series shortTermCl = bars[shortTerm][3];

                var midTermTimes = times[midTerm];
                Series midTermHi = bars[midTerm][1];
                Series midTermLo = bars[midTerm][2];
                Series midTermCl = bars[midTerm][3];

                var stMidTermUp = atm.calculateSTUp(midTermHi, midTermLo, midTermCl);
                var stMidTermDn = atm.calculateSTDn(midTermHi, midTermLo, midTermCl);
                var stSTUp = atm.calculateSTUp(shortTermHi, shortTermLo, shortTermCl);
                var stSTDn = atm.calculateSTDn(shortTermHi, shortTermLo, shortTermCl);

                Series stUp = atm.sync(stMidTermUp, midTerm, shortTerm, midTermTimes, shortTermTimes);
                Series stDn = atm.sync(stMidTermDn, midTerm, shortTerm, midTermTimes, shortTermTimes);

                var up1 = Series.Equal(stUp, 1).And(Series.Equal(stSTUp, 1));
                var dn1 = Series.Equal(stDn, 1).And(Series.Equal(stSTDn, 1));

                output = up1 - dn1;
            }

            else if (strategy == "ST | ST PR")
            {
                Series PR = Conditions.calculatePositionRatio1(times, bars, intervalList, referenceData, 1);

                string shortTerm = intervalList[0];
                string midTerm = (intervalList.Length > 1) ? intervalList[1] : intervalList[0];

                var shortTermTimes = times[shortTerm];
                Series shortTermHi = bars[shortTerm][1];
                Series shortTermLo = bars[shortTerm][2];
                Series shortTermCl = bars[shortTerm][3];

                var midTermTimes = times[midTerm];
                Series midTermHi = bars[midTerm][1];
                Series midTermLo = bars[midTerm][2];
                Series midTermCl = bars[midTerm][3];

                var stMidTermUp = atm.calculateSTUp(midTermHi, midTermLo, midTermCl);
                var stMidTermDn = atm.calculateSTDn(midTermHi, midTermLo, midTermCl);
                var stSTUp = atm.calculateSTUp(shortTermHi, shortTermLo, shortTermCl);
                var stSTDn = atm.calculateSTDn(shortTermHi, shortTermLo, shortTermCl);

                Series stUp = atm.sync(stMidTermUp, midTerm, shortTerm, midTermTimes, shortTermTimes);
                Series stDn = atm.sync(stMidTermDn, midTerm, shortTerm, midTermTimes, shortTermTimes);

                var pr = new Series(PR.Count, 0);
                for (int ii = 1; ii < PR.Count; ii++)
                {
                    if (PR[ii - 1] != 1.5 && PR[ii] == 1.5) pr[ii] = 4;
                    else if (PR[ii - 1] != -1.5 && PR[ii] == -1.5) pr[ii] = -4;
                    else if (PR[ii - 1] == 1.5 && PR[ii] == 1.0) pr[ii] = 3;
                    else if (PR[ii - 1] == -1.5 && PR[ii] == -1.0) pr[ii] = -3;
                    else if (PR[ii - 1] >= 1.0 && PR[ii] < 0) pr[ii] = 2;
                    else if (PR[ii - 1] <= -1.0 && PR[ii] > 0) pr[ii] = -2;
                    else if (PR[ii - 1] == 1.5 && PR[ii] == 1.5) pr[ii] = 1;
                    else if (PR[ii - 1] == -1.5 && PR[ii] == -1.5) pr[ii] = -1;
                    else pr[ii] = 0;
                }

                var up1 = Series.Equal(stUp, 1) * Series.Equal(stSTUp, 1) * Series.Greater(pr, 0) * pr;
                var dn1 = Series.Equal(stDn, 1) * Series.Equal(stSTDn, 1) * Series.Less(pr, 0) * pr;

                output = (up1 > 2) - (dn1 < -2); // -1 to 1
            }

            else if (strategy == "ST | SC")
            {

                string shortTerm = intervalList[0];
                string midTerm = (intervalList.Length > 1) ? intervalList[1] : intervalList[0];

                var shortTermTimes = times[shortTerm];
                Series shortTermHi = bars[shortTerm][1];
                Series shortTermLo = bars[shortTerm][2];

                var midTermTimes = times[midTerm];
                Series midTermHi = bars[midTerm][1];
                Series midTermLo = bars[midTerm][2];
                Series midTermCl = bars[midTerm][3];

                var scores = getScore(times, bars, intervalList);
                var scSTUp = scores >= 50;
                var scSTDn = scores < 50;

                var stMidTermUp = atm.calculateSTUp(midTermHi, midTermLo, midTermCl);
                var stMidTermDn = atm.calculateSTDn(midTermHi, midTermLo, midTermCl);

                Series stUp = atm.sync(stMidTermUp, midTerm, shortTerm, midTermTimes, shortTermTimes);
                Series stDn = atm.sync(stMidTermDn, midTerm, shortTerm, midTermTimes, shortTermTimes);

                var up1 = Series.Equal(stUp, 1).And(Series.Equal(scSTUp, 1));
                var dn1 = Series.Equal(stDn, 1).And(Series.Equal(scSTDn, 1));

                output = up1 - dn1;
            }

            else if (strategy == "ST | SC PR")
            {
                Series PR = Conditions.calculatePositionRatio1(times, bars, intervalList, referenceData, 1);

                string shortTerm = intervalList[0];
                string midTerm = (intervalList.Length > 1) ? intervalList[1] : intervalList[0];

                var shortTermTimes = times[shortTerm];
                Series shortTermHi = bars[shortTerm][1];
                Series shortTermLo = bars[shortTerm][2];

                var midTermTimes = times[midTerm];
                Series midTermHi = bars[midTerm][1];
                Series midTermLo = bars[midTerm][2];
                Series midTermCl = bars[midTerm][3];

                var scores = getScore(times, bars, intervalList);
                var scSTUp = scores >= 50;
                var scSTDn = scores < 50;

                var stMidTermUp = atm.calculateSTUp(midTermHi, midTermLo, midTermCl);
                var stMidTermDn = atm.calculateSTDn(midTermHi, midTermLo, midTermCl);

                Series stUp = atm.sync(stMidTermUp, midTerm, shortTerm, midTermTimes, shortTermTimes);
                Series stDn = atm.sync(stMidTermDn, midTerm, shortTerm, midTermTimes, shortTermTimes);

                var pr = new Series(PR.Count, 0);
                for (int ii = 1; ii < PR.Count; ii++)
                {
                    if (PR[ii - 1] != 1.5 && PR[ii] == 1.5) pr[ii] = 4;
                    else if (PR[ii - 1] != -1.5 && PR[ii] == -1.5) pr[ii] = -4;
                    else if (PR[ii - 1] == 1.5 && PR[ii] == 1.0) pr[ii] = 3;
                    else if (PR[ii - 1] == -1.5 && PR[ii] == -1.0) pr[ii] = -3;
                    else if (PR[ii - 1] >= 1.0 && PR[ii] < 0) pr[ii] = 2;
                    else if (PR[ii - 1] <= -1.0 && PR[ii] > 0) pr[ii] = -2;
                    else if (PR[ii - 1] == 1.5 && PR[ii] == 1.5) pr[ii] = 1;
                    else if (PR[ii - 1] == -1.5 && PR[ii] == -1.5) pr[ii] = -1;
                    else pr[ii] = 0;
                }

                var up1 = Series.Equal(stUp, 1) * Series.Equal(scSTUp, 1) * Series.Greater(pr, 0) * pr;
                var dn1 = Series.Equal(stDn, 1) * Series.Equal(scSTDn, 1) * Series.Less(pr, 0) * pr;

                output = (up1 > 2) - (dn1 < -2); // -1 to 1
            }
            else if (strategy == "ST | TSB")
            {

                string shortTerm = intervalList[0];
                string midTerm = (intervalList.Length > 1) ? intervalList[1] : intervalList[0];

                var shortTermTimes = times[shortTerm];
                Series shortTermHi = bars[shortTerm][1];
                Series shortTermLo = bars[shortTerm][2];
                Series shortTermCl = bars[shortTerm][3];

                var midTermTimes = times[midTerm];
                Series midTermHi = bars[midTerm][1];
                Series midTermLo = bars[midTerm][2];
                Series midTermCl = bars[midTerm][3];

                var stMidTermUp = atm.calculateSTUp(midTermHi, midTermLo, midTermCl);
                var stMidTermDn = atm.calculateSTDn(midTermHi, midTermLo, midTermCl);
                var tsbSTUp = atm.calculateTSBUp2(shortTermHi, shortTermLo, shortTermCl);
                var tsbSTDn = atm.calculateTSBDn2(shortTermHi, shortTermLo, shortTermCl);

                Series stUp = atm.sync(stMidTermUp, midTerm, shortTerm, midTermTimes, shortTermTimes);
                Series stDn = atm.sync(stMidTermDn, midTerm, shortTerm, midTermTimes, shortTermTimes);

                var up1 = Series.Equal(stUp, 1).And(Series.Equal(tsbSTUp, 1));
                var dn1 = Series.Equal(stDn, 1).And(Series.Equal(tsbSTDn, 1));

                output = up1 - dn1;
            }

            else if (strategy == "ST | TSB PR")
            {
                Series PR = Conditions.calculatePositionRatio1(times, bars, intervalList, referenceData, 1);

                string shortTerm = intervalList[0];
                string midTerm = (intervalList.Length > 1) ? intervalList[1] : intervalList[0];

                var shortTermTimes = times[shortTerm];
                Series shortTermHi = bars[shortTerm][1];
                Series shortTermLo = bars[shortTerm][2];
                Series shortTermCl = bars[shortTerm][3];

                var midTermTimes = times[midTerm];
                Series midTermHi = bars[midTerm][1];
                Series midTermLo = bars[midTerm][2];
                Series midTermCl = bars[midTerm][3];

                var stMidTermUp = atm.calculateSTUp(midTermHi, midTermLo, midTermCl);
                var stMidTermDn = atm.calculateSTDn(midTermHi, midTermLo, midTermCl);
                var tsbSTUp = atm.calculateTSBUp2(shortTermHi, shortTermLo, shortTermCl);
                var tsbSTDn = atm.calculateTSBDn2(shortTermHi, shortTermLo, shortTermCl);

                Series stUp = atm.sync(stMidTermUp, midTerm, shortTerm, midTermTimes, shortTermTimes);
                Series stDn = atm.sync(stMidTermDn, midTerm, shortTerm, midTermTimes, shortTermTimes);

                var pr = new Series(PR.Count, 0);
                for (int ii = 1; ii < PR.Count; ii++)
                {
                    if (PR[ii - 1] != 1.5 && PR[ii] == 1.5) pr[ii] = 4;
                    else if (PR[ii - 1] != -1.5 && PR[ii] == -1.5) pr[ii] = -4;
                    else if (PR[ii - 1] == 1.5 && PR[ii] == 1.0) pr[ii] = 3;
                    else if (PR[ii - 1] == -1.5 && PR[ii] == -1.0) pr[ii] = -3;
                    else if (PR[ii - 1] >= 1.0 && PR[ii] < 0) pr[ii] = 2;
                    else if (PR[ii - 1] <= -1.0 && PR[ii] > 0) pr[ii] = -2;
                    else if (PR[ii - 1] == 1.5 && PR[ii] == 1.5) pr[ii] = 1;
                    else if (PR[ii - 1] == -1.5 && PR[ii] == -1.5) pr[ii] = -1;
                    else pr[ii] = 0;
                }

                var up1 = Series.Equal(stUp, 1) * Series.Equal(tsbSTUp, 1) * Series.Greater(pr, 0) * pr;
                var dn1 = Series.Equal(stDn, 1) * Series.Equal(tsbSTDn, 1) * Series.Less(pr, 0) * pr;

                output = (up1 > 2) - (dn1 < -2); // -1 to 1
            }
            else if (strategy == "SC | P")
            {

                string shortTerm = intervalList[0];
                string midTerm = (intervalList.Length > 1) ? intervalList[1] : intervalList[0];
                string longTerm = (intervalList.Length > 2) ? intervalList[2] : intervalList[1];

                var shortTermTimes = times[shortTerm];
				Series shortTermOp = bars[shortTerm][0];
				Series shortTermHi = bars[shortTerm][1];
				Series shortTermLo = bars[shortTerm][2];
                Series shortTermCl = bars[shortTerm][3];

                var midTermTimes = times[midTerm];

                var midTermScores = getScore(times, bars, new string[] { midTerm, longTerm });
				var shortTermScores = getScore(times, bars, new string[] { shortTerm, midTerm });

				var scMTUp = atm.sync(midTermScores >= 50, midTerm, shortTerm, midTermTimes, shortTermTimes);
				var scMTDn = atm.sync(midTermScores < 50, midTerm, shortTerm, midTermTimes, shortTermTimes);
				var scSTUp = shortTermScores >= 50;
				var scSTDn = shortTermScores < 50;

				var ftShortTerm = atm.calculateFT(shortTermHi, shortTermLo, shortTermCl);
                var pShortTerm = atm.calculatePressureAlert(shortTermOp, shortTermHi, shortTermLo, shortTermCl);
                Series ftTU = ftShortTerm.TurnsUp();
                Series ftTD = ftShortTerm.TurnsDown();

                var count = shortTermTimes.Count;

                output = new Series(count);
                var dir = 0;
                var cnt = 0;
                for (var ii = 0; ii < count; ii++)
                {
                    if (scMTUp[ii] == 1 && scSTUp[ii] == 1 && pShortTerm[ii] == 1)
                    {
                        dir = 1;
                        cnt = 0;
                    }
                    if (scMTDn[ii] == 1 && scSTDn[ii] == 1 && pShortTerm[ii] == -1)
                    {
                        dir = -1;
                        cnt = 0;
                    }
                    if (dir != 0)
                    {
                        cnt++;
                        if (cnt == 6)
                        {
                            dir = 0;
                        }
                    }
                    if (dir > 0)
                    {
                        if (ftShortTerm[ii] >= 50 || ftTD[ii] == 1)
                        {
                            dir = 0;
                        }
                    }
                    if (dir < 0)
                    {
                        if (ftShortTerm[ii] <= 50 || ftTU[ii] == 1)
                        {
                            dir = 0;
                        }
                    }
                    output[ii] = dir;
                }
            }
            else if (strategy == "SC | FT")
            {

                string shortTerm = intervalList[0];
                string midTerm = (intervalList.Length > 1) ? intervalList[1] : intervalList[0];
                string longTerm = (intervalList.Length > 2) ? intervalList[2] : intervalList[1];

                var shortTermTimes = times[shortTerm];
                Series shortTermHi = bars[shortTerm][1];
                Series shortTermLo = bars[shortTerm][2];
                Series shortTermCl = bars[shortTerm][3];

                var midTermTimes = times[midTerm];

                var scores = getScore(times, bars, new string[] { midTerm, longTerm });

                var scMTUp = atm.sync(scores >= 50, midTerm, shortTerm, midTermTimes, shortTermTimes);
                var scMTDn = atm.sync(scores < 50, midTerm, shortTerm, midTermTimes, shortTermTimes);

                var ftShortTerm = atm.calculateFT(shortTermHi, shortTermLo, shortTermCl);
                Series ftUp = ftShortTerm.IsRising();
                Series ftDn = ftShortTerm.IsFalling();

                var up1 = Series.Equal(scMTUp, 1).And(Series.Equal(ftUp, 1));
                var dn1 = Series.Equal(scMTDn, 1).And(Series.Equal(ftDn, 1));

                output = up1 - dn1;
            }

            else if (strategy == "SC | ST")
            {

                string shortTerm = intervalList[0];
                string midTerm = (intervalList.Length > 1) ? intervalList[1] : intervalList[0];
                string longTerm = (intervalList.Length > 2) ? intervalList[2] : intervalList[1];

                var shortTermTimes = times[shortTerm];
                Series shortTermHi = bars[shortTerm][1];
                Series shortTermLo = bars[shortTerm][2];
                Series shortTermCl = bars[shortTerm][3];

                var midTermTimes = times[midTerm];

                var scores = getScore(times, bars, new string[] { midTerm, longTerm });

                var scMTUp = atm.sync(scores >= 50, midTerm, shortTerm, midTermTimes, shortTermTimes);
                var scMTDn = atm.sync(scores < 50, midTerm, shortTerm, midTermTimes, shortTermTimes);

                var stSTUp = atm.calculateSTUp(shortTermHi, shortTermLo, shortTermCl);
                var stSTDn = atm.calculateSTDn(shortTermHi, shortTermLo, shortTermCl);


                var up1 = Series.Equal(scMTUp, 1).And(Series.Equal(stSTUp, 1));
                var dn1 = Series.Equal(scMTDn, 1).And(Series.Equal(stSTDn, 1));

                output = up1 - dn1;
            }

            else if (strategy == "SC | ST PR")
            {
                Series PR = Conditions.calculatePositionRatio1(times, bars, intervalList, referenceData, 1);

                string shortTerm = intervalList[0];
                string midTerm = (intervalList.Length > 1) ? intervalList[1] : intervalList[0];
                string longTerm = (intervalList.Length > 2) ? intervalList[2] : intervalList[1];

                var shortTermTimes = times[shortTerm];
                Series shortTermHi = bars[shortTerm][1];
                Series shortTermLo = bars[shortTerm][2];
                Series shortTermCl = bars[shortTerm][3];

                var midTermTimes = times[midTerm];

                var scores = getScore(times, bars, new string[] { midTerm, longTerm });

                var scMTUp = atm.sync(scores >= 50, midTerm, shortTerm, midTermTimes, shortTermTimes);
                var scMTDn = atm.sync(scores < 50, midTerm, shortTerm, midTermTimes, shortTermTimes);

                var stSTUp = atm.calculateSTUp(shortTermHi, shortTermLo, shortTermCl);
                var stSTDn = atm.calculateSTDn(shortTermHi, shortTermLo, shortTermCl);

                var pr = new Series(PR.Count, 0);
                for (int ii = 1; ii < PR.Count; ii++)
                {
                    if (PR[ii - 1] != 1.5 && PR[ii] == 1.5) pr[ii] = 4;
                    else if (PR[ii - 1] != -1.5 && PR[ii] == -1.5) pr[ii] = -4;
                    else if (PR[ii - 1] == 1.5 && PR[ii] == 1.0) pr[ii] = 3;
                    else if (PR[ii - 1] == -1.5 && PR[ii] == -1.0) pr[ii] = -3;
                    else if (PR[ii - 1] >= 1.0 && PR[ii] < 0) pr[ii] = 2;
                    else if (PR[ii - 1] <= -1.0 && PR[ii] > 0) pr[ii] = -2;
                    else if (PR[ii - 1] == 1.5 && PR[ii] == 1.5) pr[ii] = 1;
                    else if (PR[ii - 1] == -1.5 && PR[ii] == -1.5) pr[ii] = -1;
                    else pr[ii] = 0;
                }

                var up1 = Series.Equal(scMTUp, 1) * Series.Equal(stSTUp, 1) * Series.Greater(pr, 0) * pr;
                var dn1 = Series.Equal(scMTDn, 1) * Series.Equal(stSTDn, 1) * Series.Less(pr, 0) * pr;

                output = (up1 > 2) - (dn1 < -2); // -1 to 1
            }

            else if (strategy == "SC | SC")
            {
                string shortTerm = intervalList[0];
                string midTerm = (intervalList.Length > 1) ? intervalList[1] : intervalList[0];
                string longTerm = (intervalList.Length > 2) ? intervalList[2] : intervalList[1];

                var shortTermTimes = times[shortTerm];
                Series shortTermHi = bars[shortTerm][1];
                Series shortTermLo = bars[shortTerm][2];
                Series shortTermCl = bars[shortTerm][3];

                var midTermTimes = times[midTerm];

                Series rpST = (referenceData == null) ? null : atm.calculateRelativePrice(intervalList[0], bars[intervalList[0]], referenceData, 5);
                var scoreST = getScore(times, bars, intervalList);
                var scST = atm.calculateSCSig(scoreST, rpST, 2);
                Series rpMT = (referenceData == null) ? null : atm.calculateRelativePrice(intervalList[1], bars[intervalList[1]], referenceData, 5);
                var scoreMT = getScore(times, bars, new string[] { midTerm, longTerm });
                var scm = atm.calculateSCSig(scoreMT, rpMT, 2);
                var scMT = atm.sync(scm, midTerm, shortTerm, midTermTimes, shortTermTimes);
                var up1 = Series.Equal(scST, 1).And(Series.Equal(scMT, 1));
                var dn1 = Series.Equal(scST, -1).And(Series.Equal(scMT, -1));

                //var scores = getScore(times, bars, new string[] { midTerm, longTerm });
                //var scMTUp = atm.sync(scores >= 50, midTerm, shortTerm, midTermTimes, shortTermTimes);
                //var scMTDn = atm.sync(scores < 50, midTerm, shortTerm, midTermTimes, shortTermTimes);
                //var scoresST = getScore(times, bars, intervalList);
                //var scSTUp = scoresST >= 50;
                //var scSTDn = scoresST < 50;
                //var up1 = Series.Equal(scMTUp, 1).And(Series.Equal(scSTUp, 1));
                //var dn1 = Series.Equal(scMTDn, 1).And(Series.Equal(scSTDn, 1));

                output = up1 - dn1;
            }

            else if (strategy == "SC | SC PR")
            {
                Series PR = Conditions.calculatePositionRatio1(times, bars, intervalList, referenceData, 1);

                string shortTerm = intervalList[0];
                string midTerm = (intervalList.Length > 1) ? intervalList[1] : intervalList[0];
                string longTerm = (intervalList.Length > 2) ? intervalList[2] : intervalList[1];

                var shortTermTimes = times[shortTerm];
                Series shortTermHi = bars[shortTerm][1];
                Series shortTermLo = bars[shortTerm][2];
                Series shortTermCl = bars[shortTerm][3];

                var midTermTimes = times[midTerm];

                var scores = getScore(times, bars, new string[] { midTerm, longTerm });

                var scMTUp = atm.sync(scores >= 50, midTerm, shortTerm, midTermTimes, shortTermTimes);
                var scMTDn = atm.sync(scores < 50, midTerm, shortTerm, midTermTimes, shortTermTimes);

                var scoresST = getScore(times, bars, intervalList);
                var scSTUp = scoresST >= 50;
                var scSTDn = scoresST < 50;

                var pr = new Series(PR.Count, 0);
                for (int ii = 1; ii < PR.Count; ii++)
                {
                    if (PR[ii - 1] != 1.5 && PR[ii] == 1.5) pr[ii] = 4;
                    else if (PR[ii - 1] != -1.5 && PR[ii] == -1.5) pr[ii] = -4;
                    else if (PR[ii - 1] == 1.5 && PR[ii] == 1.0) pr[ii] = 3;
                    else if (PR[ii - 1] == -1.5 && PR[ii] == -1.0) pr[ii] = -3;
                    else if (PR[ii - 1] >= 1.0 && PR[ii] < 0) pr[ii] = 2;
                    else if (PR[ii - 1] <= -1.0 && PR[ii] > 0) pr[ii] = -2;
                    else if (PR[ii - 1] == 1.5 && PR[ii] == 1.5) pr[ii] = 1;
                    else if (PR[ii - 1] == -1.5 && PR[ii] == -1.5) pr[ii] = -1;
                    else pr[ii] = 0;
                }

                var up1 = Series.Equal(scMTUp, 1) * Series.Equal(scSTUp, 1) * Series.Greater(pr, 0) * pr;
                var dn1 = Series.Equal(scMTDn, 1) * Series.Equal(scSTDn, 1) * Series.Less(pr, 0) * pr;

                output = (up1 > 2) - (dn1 < -2); // -1 to 1
            }

            else if (strategy == "SC | TSB")
            {
                string shortTerm = intervalList[0];
                string midTerm = (intervalList.Length > 1) ? intervalList[1] : intervalList[0];
                string longTerm = (intervalList.Length > 2) ? intervalList[2] : intervalList[1];

                var shortTermTimes = times[shortTerm];
                Series shortTermHi = bars[shortTerm][1];
                Series shortTermLo = bars[shortTerm][2];
                Series shortTermCl = bars[shortTerm][3];

                var midTermTimes = times[midTerm];

                var scores = getScore(times, bars, new string[] { midTerm, longTerm });

                var scMTUp = atm.sync(scores >= 50, midTerm, shortTerm, midTermTimes, shortTermTimes);
                var scMTDn = atm.sync(scores < 50, midTerm, shortTerm, midTermTimes, shortTermTimes);

                var tsbSTUp = atm.calculateTSBUp2(shortTermHi, shortTermLo, shortTermCl);
                var tsbSTDn = atm.calculateTSBDn2(shortTermHi, shortTermLo, shortTermCl);

                var up1 = Series.Equal(scMTUp, 1).And(Series.Equal(tsbSTUp, 1));
                var dn1 = Series.Equal(scMTDn, 1).And(Series.Equal(tsbSTDn, 1));
                output = up1 - dn1;
            }

            else if (strategy == "SC | TSB PR")
            {
                Series PR = Conditions.calculatePositionRatio1(times, bars, intervalList, referenceData, 1);

                string shortTerm = intervalList[0];
                string midTerm = (intervalList.Length > 1) ? intervalList[1] : intervalList[0];
                string longTerm = (intervalList.Length > 2) ? intervalList[2] : intervalList[1];

                var shortTermTimes = times[shortTerm];
                Series shortTermHi = bars[shortTerm][1];
                Series shortTermLo = bars[shortTerm][2];
                Series shortTermCl = bars[shortTerm][3];

                var midTermTimes = times[midTerm];

                var scores = getScore(times, bars, new string[] { midTerm, longTerm });

                var scMTUp = atm.sync(scores >= 50, midTerm, shortTerm, midTermTimes, shortTermTimes);
                var scMTDn = atm.sync(scores < 50, midTerm, shortTerm, midTermTimes, shortTermTimes);

                var tsbSTUp = atm.calculateTSBUp2(shortTermHi, shortTermLo, shortTermCl);
                var tsbSTDn = atm.calculateTSBDn2(shortTermHi, shortTermLo, shortTermCl);

                var pr = new Series(PR.Count, 0);
                for (int ii = 1; ii < PR.Count; ii++)
                {
                    if (PR[ii - 1] != 1.5 && PR[ii] == 1.5) pr[ii] = 4;
                    else if (PR[ii - 1] != -1.5 && PR[ii] == -1.5) pr[ii] = -4;
                    else if (PR[ii - 1] == 1.5 && PR[ii] == 1.0) pr[ii] = 3;
                    else if (PR[ii - 1] == -1.5 && PR[ii] == -1.0) pr[ii] = -3;
                    else if (PR[ii - 1] >= 1.0 && PR[ii] < 0) pr[ii] = 2;
                    else if (PR[ii - 1] <= -1.0 && PR[ii] > 0) pr[ii] = -2;
                    else if (PR[ii - 1] == 1.5 && PR[ii] == 1.5) pr[ii] = 1;
                    else if (PR[ii - 1] == -1.5 && PR[ii] == -1.5) pr[ii] = -1;
                    else pr[ii] = 0;
                }

                var up1 = Series.Equal(scMTUp, 1) * Series.Equal(tsbSTUp, 1) * Series.Greater(pr, 0) * pr;
                var dn1 = Series.Equal(scMTDn, 1) * Series.Equal(tsbSTDn, 1) * Series.Less(pr, 0) * pr;

                output = (up1 > 2) - (dn1 < -2); // -1 to 1
            }

            else if (strategy == "TSB | FT")
            {

                string shortTerm = intervalList[0];
                string midTerm = (intervalList.Length > 1) ? intervalList[1] : intervalList[0];

                var shortTermTimes = times[shortTerm];
                Series shortTermHi = bars[shortTerm][1];
                Series shortTermLo = bars[shortTerm][2];
                Series shortTermCl = bars[shortTerm][3];

                var midTermTimes = times[midTerm];
                Series midTermHi = bars[midTerm][1];
                Series midTermLo = bars[midTerm][2];
                Series midTermCl = bars[midTerm][3];

                var tsbMidTermUp = atm.calculateTSBUp2(midTermHi, midTermLo, midTermCl);
                var tsbMidTermDn = atm.calculateTSBDn2(midTermHi, midTermLo, midTermCl);
                var ftShortTerm = atm.calculateFT(shortTermHi, shortTermLo, shortTermCl);

                Series tsbUp = atm.sync(tsbMidTermUp, midTerm, shortTerm, midTermTimes, shortTermTimes);
                Series tsbDn = atm.sync(tsbMidTermDn, midTerm, shortTerm, midTermTimes, shortTermTimes);
                Series ftSTUp = ftShortTerm.IsRising();
                Series ftSTDn = ftShortTerm.IsFalling();

                var up1 = Series.Equal(tsbUp, 1).And(Series.Equal(ftSTUp, 1));
                var dn1 = Series.Equal(tsbDn, 1).And(Series.Equal(ftSTDn, 1));

                output = up1 - dn1;
            }

            else if (strategy == "TSB | ST")
            {

                string shortTerm = intervalList[0];
                string midTerm = (intervalList.Length > 1) ? intervalList[1] : intervalList[0];

                var shortTermTimes = times[shortTerm];
                Series shortTermHi = bars[shortTerm][1];
                Series shortTermLo = bars[shortTerm][2];
                Series shortTermCl = bars[shortTerm][3];

                var midTermTimes = times[midTerm];
                Series midTermHi = bars[midTerm][1];
                Series midTermLo = bars[midTerm][2];
                Series midTermCl = bars[midTerm][3];

                var tsbMidTermUp = atm.calculateTSBUp2(midTermHi, midTermLo, midTermCl);
                var tsbMidTermDn = atm.calculateTSBDn2(midTermHi, midTermLo, midTermCl);
                var stSTUp = atm.calculateSTUp(shortTermHi, shortTermLo, shortTermCl);
                var stSTDn = atm.calculateSTDn(shortTermHi, shortTermLo, shortTermCl);

                Series tsbUp = atm.sync(tsbMidTermUp, midTerm, shortTerm, midTermTimes, shortTermTimes);
                Series tsbDn = atm.sync(tsbMidTermDn, midTerm, shortTerm, midTermTimes, shortTermTimes);

                var up1 = Series.Equal(tsbUp, 1).And(Series.Equal(stSTUp, 1));
                var dn1 = Series.Equal(tsbDn, 1).And(Series.Equal(stSTDn, 1));
                output = up1 - dn1;
            }

            else if (strategy == "TSB | ST PR")
            {
                Series PR = Conditions.calculatePositionRatio1(times, bars, intervalList, referenceData, 1);

                string shortTerm = intervalList[0];
                string midTerm = (intervalList.Length > 1) ? intervalList[1] : intervalList[0];

                var shortTermTimes = times[shortTerm];
                Series shortTermHi = bars[shortTerm][1];
                Series shortTermLo = bars[shortTerm][2];
                Series shortTermCl = bars[shortTerm][3];

                var midTermTimes = times[midTerm];
                Series midTermHi = bars[midTerm][1];
                Series midTermLo = bars[midTerm][2];
                Series midTermCl = bars[midTerm][3];

                var tsbMidTermUp = atm.calculateTSBUp2(midTermHi, midTermLo, midTermCl);
                var tsbMidTermDn = atm.calculateTSBDn2(midTermHi, midTermLo, midTermCl);
                var stSTUp = atm.calculateSTUp(shortTermHi, shortTermLo, shortTermCl);
                var stSTDn = atm.calculateSTDn(shortTermHi, shortTermLo, shortTermCl);

                Series tsbUp = atm.sync(tsbMidTermUp, midTerm, shortTerm, midTermTimes, shortTermTimes);
                Series tsbDn = atm.sync(tsbMidTermDn, midTerm, shortTerm, midTermTimes, shortTermTimes);

                var pr = new Series(PR.Count, 0);
                for (int ii = 1; ii < PR.Count; ii++)
                {
                    if (PR[ii - 1] != 1.5 && PR[ii] == 1.5) pr[ii] = 4;
                    else if (PR[ii - 1] != -1.5 && PR[ii] == -1.5) pr[ii] = -4;
                    else if (PR[ii - 1] == 1.5 && PR[ii] == 1.0) pr[ii] = 3;
                    else if (PR[ii - 1] == -1.5 && PR[ii] == -1.0) pr[ii] = -3;
                    else if (PR[ii - 1] >= 1.0 && PR[ii] < 0) pr[ii] = 2;
                    else if (PR[ii - 1] <= -1.0 && PR[ii] > 0) pr[ii] = -2;
                    else if (PR[ii - 1] == 1.5 && PR[ii] == 1.5) pr[ii] = 1;
                    else if (PR[ii - 1] == -1.5 && PR[ii] == -1.5) pr[ii] = -1;
                    else pr[ii] = 0;
                }

                var up1 = Series.Equal(tsbUp, 1) * Series.Equal(stSTUp, 1) * Series.Greater(pr, 0) * pr;
                var dn1 = Series.Equal(tsbDn, 1) * Series.Equal(stSTDn, 1) * Series.Less(pr, 0) * pr;

                output = (up1 > 2) - (dn1 < -2); // -1 to 1
            }
            else if (strategy == "TSB | SC")
            {

                string shortTerm = intervalList[0];
                string midTerm = (intervalList.Length > 1) ? intervalList[1] : intervalList[0];

                var shortTermTimes = times[shortTerm];
                Series shortTermHi = bars[shortTerm][1];
                Series shortTermLo = bars[shortTerm][2];
                Series shortTermCl = bars[shortTerm][3];

                var midTermTimes = times[midTerm];
                Series midTermHi = bars[midTerm][1];
                Series midTermLo = bars[midTerm][2];
                Series midTermCl = bars[midTerm][3];

                var scores = getScore(times, bars, intervalList);
                var scSTUp = scores >= 50;
                var scSTDn = scores < 50;

                var tsbMidTermUp = atm.calculateTSBUp2(midTermHi, midTermLo, midTermCl);
                var tsbMidTermDn = atm.calculateTSBDn2(midTermHi, midTermLo, midTermCl);

                Series tsbUp = atm.sync(tsbMidTermUp, midTerm, shortTerm, midTermTimes, shortTermTimes);
                Series tsbDn = atm.sync(tsbMidTermDn, midTerm, shortTerm, midTermTimes, shortTermTimes);

                var up1 = Series.Equal(tsbUp, 1).And(Series.Equal(scSTUp, 1));
                var dn1 = Series.Equal(tsbDn, 1).And(Series.Equal(scSTDn, 1));

                output = up1 - dn1;
            }


            else if (strategy == "TSB | SC PR")
            {
                Series PR = Conditions.calculatePositionRatio1(times, bars, intervalList, referenceData, 1);

                string shortTerm = intervalList[0];
                string midTerm = (intervalList.Length > 1) ? intervalList[1] : intervalList[0];

                var shortTermTimes = times[shortTerm];
                Series shortTermHi = bars[shortTerm][1];
                Series shortTermLo = bars[shortTerm][2];
                Series shortTermCl = bars[shortTerm][3];

                var midTermTimes = times[midTerm];
                Series midTermHi = bars[midTerm][1];
                Series midTermLo = bars[midTerm][2];
                Series midTermCl = bars[midTerm][3];

                var scores = getScore(times, bars, intervalList);
                var scSTUp = scores >= 50;
                var scSTDn = scores < 50;

                var tsbMidTermUp = atm.calculateTSBUp2(midTermHi, midTermLo, midTermCl);
                var tsbMidTermDn = atm.calculateTSBDn2(midTermHi, midTermLo, midTermCl);

                Series tsbUp = atm.sync(tsbMidTermUp, midTerm, shortTerm, midTermTimes, shortTermTimes);
                Series tsbDn = atm.sync(tsbMidTermDn, midTerm, shortTerm, midTermTimes, shortTermTimes);

                var pr = new Series(PR.Count, 0);
                for (int ii = 1; ii < PR.Count; ii++)
                {
                    if (PR[ii - 1] != 1.5 && PR[ii] == 1.5) pr[ii] = 4;
                    else if (PR[ii - 1] != -1.5 && PR[ii] == -1.5) pr[ii] = -4;
                    else if (PR[ii - 1] == 1.5 && PR[ii] == 1.0) pr[ii] = 3;
                    else if (PR[ii - 1] == -1.5 && PR[ii] == -1.0) pr[ii] = -3;
                    else if (PR[ii - 1] >= 1.0 && PR[ii] < 0) pr[ii] = 2;
                    else if (PR[ii - 1] <= -1.0 && PR[ii] > 0) pr[ii] = -2;
                    else if (PR[ii - 1] == 1.5 && PR[ii] == 1.5) pr[ii] = 1;
                    else if (PR[ii - 1] == -1.5 && PR[ii] == -1.5) pr[ii] = -1;
                    else pr[ii] = 0;
                }

                var up1 = Series.Equal(tsbUp, 1) * Series.Equal(scSTUp, 1) * Series.Greater(pr, 0) * pr;
                var dn1 = Series.Equal(tsbDn, 1) * Series.Equal(scSTDn, 1) * Series.Less(pr, 0) * pr;

                output = (up1 > 2) - (dn1 < -2); // -1 to 1
            }

            else if (strategy == "TSB | TSB")
            {

                string shortTerm = intervalList[0];
                string midTerm = (intervalList.Length > 1) ? intervalList[1] : intervalList[0];

                var shortTermTimes = times[shortTerm];
                Series shortTermHi = bars[shortTerm][1];
                Series shortTermLo = bars[shortTerm][2];
                Series shortTermCl = bars[shortTerm][3];

                var midTermTimes = times[midTerm];
                Series midTermHi = bars[midTerm][1];
                Series midTermLo = bars[midTerm][2];
                Series midTermCl = bars[midTerm][3];

                var tsbMidTermUp = atm.calculateTSBUp2(midTermHi, midTermLo, midTermCl);
                var tsbMidTermDn = atm.calculateTSBDn2(midTermHi, midTermLo, midTermCl);
                var tsbSTUp = atm.calculateTSBUp2(shortTermHi, shortTermLo, shortTermCl);
                var tsbSTDn = atm.calculateTSBDn2(shortTermHi, shortTermLo, shortTermCl);

                Series tsbUp = atm.sync(tsbMidTermUp, midTerm, shortTerm, midTermTimes, shortTermTimes);
                Series tsbDn = atm.sync(tsbMidTermDn, midTerm, shortTerm, midTermTimes, shortTermTimes);

                var up1 = Series.Equal(tsbUp, 1).And(Series.Equal(tsbSTUp, 1));
                var dn1 = Series.Equal(tsbDn, 1).And(Series.Equal(tsbSTDn, 1));

                output = up1 - dn1;
            }

            else if (strategy == "TSB | TSB PR")
            {
                Series PR = Conditions.calculatePositionRatio1(times, bars, intervalList, referenceData, 1);

                string shortTerm = intervalList[0];
                string midTerm = (intervalList.Length > 1) ? intervalList[1] : intervalList[0];

                var shortTermTimes = times[shortTerm];
                Series shortTermHi = bars[shortTerm][1];
                Series shortTermLo = bars[shortTerm][2];
                Series shortTermCl = bars[shortTerm][3];

                var midTermTimes = times[midTerm];
                Series midTermHi = bars[midTerm][1];
                Series midTermLo = bars[midTerm][2];
                Series midTermCl = bars[midTerm][3];

                var tsbMidTermUp = atm.calculateTSBUp2(midTermHi, midTermLo, midTermCl);
                var tsbMidTermDn = atm.calculateTSBDn2(midTermHi, midTermLo, midTermCl);
                var tsbSTUp = atm.calculateTSBUp2(shortTermHi, shortTermLo, shortTermCl);
                var tsbSTDn = atm.calculateTSBDn2(shortTermHi, shortTermLo, shortTermCl);

                Series tsbUp = atm.sync(tsbMidTermUp, midTerm, shortTerm, midTermTimes, shortTermTimes);
                Series tsbDn = atm.sync(tsbMidTermDn, midTerm, shortTerm, midTermTimes, shortTermTimes);

                var pr = new Series(PR.Count, 0);
                for (int ii = 1; ii < PR.Count; ii++)
                {
                    if (PR[ii - 1] != 1.5 && PR[ii] == 1.5) pr[ii] = 4;
                    else if (PR[ii - 1] != -1.5 && PR[ii] == -1.5) pr[ii] = -4;
                    else if (PR[ii - 1] == 1.5 && PR[ii] == 1.0) pr[ii] = 3;
                    else if (PR[ii - 1] == -1.5 && PR[ii] == -1.0) pr[ii] = -3;
                    else if (PR[ii - 1] >= 1.0 && PR[ii] < 0) pr[ii] = 2;
                    else if (PR[ii - 1] <= -1.0 && PR[ii] > 0) pr[ii] = -2;
                    else if (PR[ii - 1] == 1.5 && PR[ii] == 1.5) pr[ii] = 1;
                    else if (PR[ii - 1] == -1.5 && PR[ii] == -1.5) pr[ii] = -1;
                    else pr[ii] = 0;
                }

                var up1 = Series.Equal(tsbUp, 1) * Series.Equal(tsbSTUp, 1) * Series.Greater(pr, 0) * pr;
                var dn1 = Series.Equal(tsbDn, 1) * Series.Equal(tsbSTDn, 1) * Series.Less(pr, 0) * pr;

                output = (up1 > 2) - (dn1 < -2); // -1 to 1
            }
            return output;
        }

        public static double getCorrelation(List<double> array1, List<double> array2)
        {
            double[] array_xy = new double[array1.Count];
            double[] array_xp2 = new double[array1.Count];
            double[] array_yp2 = new double[array1.Count];

            for (int i = 0; i < array1.Count; i++)
                array_xy[i] = (!double.IsNaN(array1[i]) && !double.IsNaN(array2[i])) ? array1[i] * array2[i] : 0;

            for (int i = 0; i < array1.Count; i++)
                array_xp2[i] = (!double.IsNaN(array1[i])) ? Math.Pow(array1[i], 2.0) : 0;

            for (int i = 0; i < array1.Count; i++)
                array_yp2[i] = (!double.IsNaN(array2[i])) ? Math.Pow(array2[i], 2.0) : 0;

            double sum_x = 0;
            double sum_y = 0;
            foreach (double n in array1)
                sum_x += ((!double.IsNaN(n)) ? n : 0);

            foreach (double n in array2)
                sum_y += ((!double.IsNaN(n)) ? n : 0);

            double sum_xy = 0;
            foreach (double n in array_xy)
                sum_xy += n;
            double sum_xpow2 = 0;
            foreach (double n in array_xp2)
                sum_xpow2 += n;
            double sum_ypow2 = 0;
            foreach (double n in array_yp2)
                sum_ypow2 += n;
            double Ex2 = Math.Pow(sum_x, 2.00);
            double Ey2 = Math.Pow(sum_y, 2.00);

            double Correl = (array1.Count * sum_xy - sum_x * sum_y) / Math.Sqrt((array1.Count * sum_xpow2 - Ex2) * (array1.Count * sum_ypow2 - Ey2));

            return Correl;
        }

        public static Series calculateFT(Series high, Series low, Series close)
        {
            Series x1 = lowest(low, 8);
            Series x2 = highest(high, 8);
            Series x3 = Series.EMAvg(((close - x1) * 100) / (x2 - x1), 3);
            Series x4 = lowest(x3, 8);
            Series x5 = highest(x3, 8);
            Series x6 = Series.EMAvg(((x3 - x4) * 100) / (x5 - x4), 3);
            Series x7 = Series.SmoothAvg(Series.LinearReg(x6, 5), 2);
            return x7;
        }

        public static Series hasVal(Series series)
        {
            int count = series.Count;
            Series output = new Series(count);
            for (int ii = 0; ii < count; ii++)
            {
                output[ii] = Double.IsNaN(series[ii]) ? 0 : 1;
            }
            return output;
        }

        public static Series notHasVal(Series series)
        {
            int count = series.Count;
            Series output = new Series(count);
            for (int ii = 0; ii < count; ii++)
            {
                output[ii] = Double.IsNaN(series[ii]) ? 1 : 0;
            }
            return output;
        }

        public static Series calculatestCrosses30Turns30(Series[] bars)
        {
            Series hi = bars[1];
            Series lo = bars[2];
            Series cl = bars[3];

            Series st = atm.calculateST(hi, lo, cl);
            Series st30 = st.CrossesAbove(30);
            Series stRising30 = st.IsRising() & st >= 30;

            Series output = (st30).And(stRising30);

            return output;
        }

        public static Series calculatestCrosses70Turns70(Series[] bars)
        {
            Series hi = bars[1];
            Series lo = bars[2];
            Series cl = bars[3];

            Series st = atm.calculateST(hi, lo, cl);

            Series st70 = st.CrossesBelow(70);

            Series stFalling70 = st.IsFalling() & st <= 70;

            Series output = (st70).And(stFalling70);

            return output;
        }

        public static Series calculateBullishTSB(Series hi, Series lo, Series cl)
        {
            Series ST = atm.calculateST(hi, lo, cl);
            Series EZI = atm.calculateEZI(cl);
            Series TSB = atm.calculateTSB(hi, lo, cl);

            Series upSet = (TSB > 30).And(ST > 75).And((ST.ShiftRight(6) < 20).Or(ST.ShiftRight(7) < 20));
            Series dnSet = (TSB < 70).And(ST < 25).And((ST.ShiftRight(6) > 80).Or(ST.ShiftRight(7) > 80));
            Series upRes = ST < 65;
            Series dnRes = ST > 35;

            Series Utsb = (EZI >= 80).And(TSB >= 70);
            Series Dtsb = (EZI <= 20).And(TSB <= 30);
            Series Uezi = (EZI <= 80).And(TSB >= 70);
            Series Dezi = (EZI >= 20).And(TSB <= 30);
            Series Uset = (TSB < 70) * atm.setReset(upSet, upRes);
            Series Dset = (TSB > 30) * atm.setReset(dnSet, dnRes);

            Series output = Utsb.Or(Uezi.Or(Uset));

            return output;
        }

        public static Series calculateBearishTSB(Series hi, Series lo, Series cl)
        {
            Series ST = atm.calculateST(hi, lo, cl);
            Series EZI = atm.calculateEZI(cl);
            Series TSB = atm.calculateTSB(hi, lo, cl);

            Series upSet = (TSB > 30).And(ST > 75).And((ST.ShiftRight(6) < 20).Or(ST.ShiftRight(7) < 20));
            Series dnSet = (TSB < 70).And(ST < 25).And((ST.ShiftRight(6) > 80).Or(ST.ShiftRight(7) > 80));
            Series upRes = ST < 65;
            Series dnRes = ST > 35;

            Series Utsb = (EZI >= 80).And(TSB >= 70);
            Series Dtsb = (EZI <= 20).And(TSB <= 30);
            Series Uezi = (EZI <= 80).And(TSB >= 70);

            Series Dezi = (EZI >= 20).And(TSB <= 30);
            Series Uset = (TSB < 70) * atm.setReset(upSet, upRes);
            Series Dset = (TSB > 30) * atm.setReset(dnSet, dnRes);

            Series tsbUp = Utsb.Or(Uezi.Or(Uset));
            Series output = Series.NotEqual(tsbUp, 1);

            return output;
        }

        public static List<Series> calculateTDI(Series high, Series low)
        {
            List<Series> sl = new List<Series>();

            Series mid = (high + low) / 2;

            sl.Add(Series.EMAvg(mid, 34));
            sl.Add(Series.EMAvg(mid, 34));

            return sl;
        }

        public static Series setReset(Series set, Series reset)
        {
            double result = Double.NaN;
            int count = set.Count;
            Series output = new Series(count);
            for (int ii = 0; ii < count; ii++)
            {
                if (!Double.IsNaN(set[ii]) && !Double.IsNaN(reset[ii]))
                {
                    if (set[ii] != 0 && reset[ii] == 0)
                    {
                        result = 1.0;
                    }
                    else if (set[ii] == 0 && reset[ii] != 0)
                    {
                        result = 0.0;
                    }
                }
                output[ii] = result;
            }
            return output;
        }

        public static Series lowest(Series input, int period)
        {
            int count = input.Count;
            Series output = new Series(count);
            for (int ii = 0; ii < count; ii++)
            {
                double min = double.NaN;
                for (int jj = 0; jj < period; jj++)
                {
                    if (ii - jj >= 0 && !Double.IsNaN(input[ii - jj]))
                    {
                        if (double.IsNaN(min) || input[ii - jj] < min)
                        {
                            min = input[ii - jj];
                        }
                    }
                }
                output[ii] = min;
            }
            return output;
        }

        public static List<int> GetFTCondition(Series hi, Series lo, Series cl)
        {
            Series FT = atm.calculateFT(hi, lo, cl);
            int count = FT.Count;

            List<int> signals = new List<int>(count);
            int value = 0;
            for (int ii = 0; ii < count; ii++)
            {
                if (FT[ii] > 80) value = 2;
                else if (FT[ii] < 20) value = -2;
                else if (ii > 0 && FT[ii] > FT[ii - 1]) value = 1;
                else if (ii > 0 && FT[ii] < FT[ii - 1]) value = -1;
                else value = 0;
                signals.Add(value);
            }
            return signals;
        }

        public static List<int> GetSTCondition(Series hi, Series lo, Series cl)
        {
            List<int> signals = new List<int>();
            Series ST = atm.calculateST(hi, lo, cl);
            Series sig = ST.IsRising() - ST.IsFalling();
            int count = sig.Count;
            for (int ii = 0; ii < count; ii++)
            {
                signals.Add((int)sig[ii]);
            }
            return signals;
		}

		public static Dictionary<DateTime, double> getTrades(int side, Dictionary<DateTime, double> filter, string ticker, DateTime startTime, Dictionary<string, List<DateTime>> times, Dictionary<string, Series[]> bars, 
            string[] intervalList, Dictionary<string, object> referenceData, string position, Dictionary<string, bool> enbs, 
            Dictionary<string, double> sizes, int maxLeverage, int freqPeriod)
		{
            var unitSize = 2 / 100.0;
            var maxUnitSize = 10 / 100.0;

			var newTrendEnb = enbs.ContainsKey("New Trend") ? enbs["New Trend"] : true;
			var ntObOsEnb = enbs.ContainsKey("NT OB OS") ? enbs["NT OB OS"] : false;
			var proveItEnb = enbs.ContainsKey("UseProveIt") ? enbs["UseProveIt"] : false;
			var ptEnb = enbs.ContainsKey("Pressure Alert") ? enbs["Pressure Alert"] : true;
			var addEnb = enbs.ContainsKey("Add Alert") ? enbs["Add Alert"] : true;
			var exhEnb = enbs.ContainsKey("Exhaustion") ? enbs["Exhaustion"] : true;
			var pExhEnb = enbs.ContainsKey("PExhaustion") ? enbs["PExhaustion"] : true;
			var redEnb = enbs.ContainsKey("Retrace") ? enbs["Retrace"] : true;
			var ftEnb = enbs.ContainsKey("FTEntry") ? enbs["FTEntry"] : false;
			var useConviction = enbs.ContainsKey("UseConviction") ? enbs["UseConviction"] : false;
			var twoBarEnb = enbs.ContainsKey("TwoBar") ? enbs["TwoBar"] : false;
			var ftRocEnb = enbs.ContainsKey("FTRoc") ? enbs["FTRoc"] : false;
            var timeExitEnb = enbs.ContainsKey("TimeExit") ? enbs["TimeExit"] : false;

			var useFtFt = enbs.ContainsKey("UseFtFt") ? enbs["UseFtFt"] : false;
			var useMTST = enbs.ContainsKey("UseMTST") ? enbs["UseMTST"] : false;
			var useMTSC = enbs.ContainsKey("UseMTSC") ? enbs["UseMTSC"] : false;
			var useMTTSB = enbs.ContainsKey("UseMTTSB") ? enbs["UseMTTSB"] : false;

            var useMTFilterExit = enbs.ContainsKey("UseMTExit") ? enbs["UseMTExit"] : false;

			var interval1 = intervalList[0];
			var interval2 = intervalList[1];
			var interval3 = intervalList[2];

            var mtIntervalList = new string[] { interval2, interval3 };

			Series[] series1 = bars[interval1];
			List<DateTime> time1 = times[interval1];
            int currentBarIndex = time1.Count - 1;

			Series[] series2 = bars[interval2];
			List<DateTime> time2 = times[interval2];

			TradeType tradeType = TradeType.None;

			var op = series1[0];
			var hi = series1[1];
			var lo = series1[2];
			var cl = series1[3];

			Series ft1 = calculateFT(hi, lo, cl); // short term ft
			Series st1 = calculateST(hi, lo, cl); // short term st
			Series score = getScore(times, bars, intervalList);
			Series rp = calculateRelativePrice(interval1, series1, referenceData, 5);
            var scSig = calculateSCSig(score, rp, 2);

			Series ft2 = calculateFT(series2[1], series2[2], series2[3]); // mid term ft
			Series st2 = calculateST(series2[1], series2[2], series2[3]); // mid term st
			Series score2 = getScore(times, bars, mtIntervalList); // mid term score
			Series rp2 = calculateRelativePrice(interval2, series2, referenceData, 5);
			var scSig2 = calculateSCSig(score2, rp2, 2);
			var tsb2u = Conditions.calculatePressureUpORBullishTSB(series2); // mid term tsb
			var tsb2d = Conditions.calculatePressureDnORBearishTSB(series2); // mid term tsb

			Series ftftUp = sync(happenedWithin(ft2 < 25, 0, 8) & ft2.IsRising(), interval2, interval1, time2, time1);  //FT over last 8 bars
			Series ftftDn = sync(happenedWithin(ft2 > 75, 0, 8) & ft2.IsFalling(), interval2, interval1, time2, time1);

			Series mtstUp = sync(st2.IsRising() | st2 > 80, interval2, interval1, time2, time1);
			Series mtstDn = sync(st2.IsFalling() | st2 < 20, interval2, interval1, time2, time1);

			Series mtscUp = sync(scSig2 > 0, interval2, interval1, time2, time1);
			Series mtscDn = sync(scSig2 < 0, interval2, interval1, time2, time1);

			Series mttsbUp = sync(tsb2u, interval2, interval1, time2, time1);
			Series mttsbDn = sync(tsb2d, interval2, interval1, time2, time1);

			Series mtFtUp = useFtFt ? ftftUp : new Series(time1.Count, 1);
			Series mtFtDn = useFtFt ? ftftDn : new Series(time1.Count, 1);
			Series mtStUp = useMTST ? mtstUp : new Series(time1.Count, 1);
			Series mtStDn = useMTST ? mtstDn : new Series(time1.Count, 1);
			Series mtScUp = useMTST ? mtscUp : new Series(time1.Count, 1);
			Series mtScDn = useMTST ? mtscDn : new Series(time1.Count, 1);
			Series mtTsbUp = useMTTSB ? mttsbUp : new Series(time1.Count, 1);
			Series mtTsbDn = useMTTSB ? mttsbDn : new Series(time1.Count, 1);

			Series exh = exhEnb ? calculateExhaustion(hi, lo, cl, atm.ExhaustionLevelSelection.AllLevels) : new Series(time1.Count, 0);
            Series pt = calculatePressureAlert(op, hi, lo, cl);
            Series ptUpWithin3Bars = pt.Since(1) <= 3;
			Series ptDnWithin3Bars = pt.Since(-1) <= 3;
			Series twobar = new Series(time1.Count, 0); // atm.calculateTwoBarPattern(op, hi, lo, cl, 5);
            Series par = calculatePAlertReversion(op, hi, lo, cl, ft1);
            Series ftEntry = (ft1 < 50 & ft1.TurnsUp()) - (ft1 >= 50 & ft1.TurnsDown());
            Series pexh = pExhEnb ? calculatePExhaustion(op, hi, lo, cl) : new Series(time1.Count, 0);

            Series twoBar = twoBarEnb ? atm.calculateTwoBarPattern(op, hi, lo, cl, freqPeriod) : new Series(time1.Count, 0);

			var trades = new Dictionary<DateTime, double>();

			var longEnb = position == "Long | Short" || position == "Long Only";
			var shortEnb = position == "Long | Short" || position == "Short Only";

			Series ftgu = ft1.IsRising();
			Series ftgd = ft1.IsFalling();
			Series fttu = ft1.TurnsUp();
			Series fttd = ft1.TurnsDown();
			Series ftlt70 = ft1 < 70;
			Series ftgt30 = ft1 > 30;
			Series ftlt80 = ft1 < 80;
			Series ftgt20 = ft1 > 20;

			Series stgu = st1.IsRising();
			Series stgd = st1.IsFalling();

			var ftRocUp = fttu.ShiftRight(1) & (ft1 - ft1.ShiftRight(1) <  3);
			var ftRocDn = fttd.ShiftRight(1) & (ft1 - ft1.ShiftRight(1) > -3);
            var ftRoc = ftRocUp - ftRocDn;

			Series rl1 = ftgu.ShiftRight(1) & ftgd;// | par < 0;
            Series rs1 = ftgd.ShiftRight(1) & ftgu;// | par > 0;

			Series rl2 = atm.frequentSignalFilter(4, rl1, rl1);
			Series rs2 = atm.frequentSignalFilter(4, rs1, rs1);

            Series atr = Series.SmoothAvg(Series.TrueRange(hi, lo, cl), 14) * 1.5;

            var period = 0;

			var signals = new Series(cl.Count, 0);
			for (int ii = 1; ii < signals.Count; ii++)
			{
				if (scSig[ii] == 1 && (ftgu[ii] == 1 && ftgd[ii - 1] == 1 && ftlt70[ii] == 1)) signals[ii] = 5; //add one
				else if (scSig[ii] == 1 && exh[ii] == -1) signals[ii] = 2; // exit long
				else if (scSig[ii] == 1 && rl2[ii] == 1) signals[ii] = 3; // reduce long
				else if (scSig[ii] == 1 && ftgu[ii] == 1) signals[ii] = 4; // stay long or exit long if fttp
				else if (scSig[ii] == 1 && ftgd[ii] == 1) signals[ii] = 1; // enter long if fttp or waiting for long

				else if (scSig[ii] == -1 && (ftgd[ii] == 1 && ftgu[ii - 1] == 1 && ftgt30[ii] == 1)) signals[ii] = -5; // add on
				else if (scSig[ii] == -1 && exh[ii] == 1) signals[ii] = -2; // exit short
				else if (scSig[ii] == -1 && rs2[ii] == 1) signals[ii] = -3; // reduce short
				else if (scSig[ii] == -1 && ftgd[ii] == 1) signals[ii] = -4; // stay short or exit short if fttp
				else if (scSig[ii] == -1 && ftgu[ii] == 1) signals[ii] = -1; // enter short if fttp or waiting for short
			}

			var lng = false;
			var sht = false;

			var index1 = times[interval1].FindIndex(x => x >= startTime); 
            if (index1 == -1) index1 = currentBarIndex - (cl.Count - 1);
			var index2 = currentBarIndex;

			double size = 0;

			var stTimes = times[interval1];

            var entryIndex = 0;

			for (var ii = index1; ii <= index2; ii++)
			{
                var date = stTimes[ii];
				var sig = signals[ii];

                bool bp = false;
                if (ticker == "COIN US Equity" && date.Year == 2025 && date.Month == 10 && date.Day == 3)
                {
                    bp = true;
                }

				var lfilter = filter.ContainsKey(date) ? side > 0 && filter[date] != 0 : true;
				var sfilter = filter.ContainsKey(date) ? side < 0 && filter[date] != 0 : true;

				tradeType = TradeType.None;

				var mtUpOk = mtFtUp[ii] == 1 && mtStUp[ii] == 1 && mtScUp[ii] == 1 && mtTsbUp[ii] == 1;
				var mtDnOk = mtFtDn[ii] == 1 && mtStDn[ii] == 1 && mtScDn[ii] == 1 && mtTsbDn[ii] == 1;

				var l0 = lfilter && mtUpOk && newTrendEnb && scSig[ii - 1] != 1 && scSig[ii] == 1 && (!ntObOsEnb || ft1[ii] < 75); // && ftlt80[ii] == 1; // && ftgu[ii] == 1/* && ftlt85[ii] == 1*/;
				var l1 = lfilter && mtUpOk && ptEnb && pt[ii] > 0 && scSig[ii] == 1;
				var l2 = lfilter && mtUpOk && addEnb && sig == 5;
				//var l3 = c2u[ii] == 1 && twobarEnb && twobar[ii] > 0 && scSig[ii] == 1;
				//var l4 = lfilter && mtUpOk && newTrendEnb && ii == index1 && scSig[ii] == 1;
				var l5 = lfilter && mtUpOk && ftEnb && ftEntry[ii] > 0;

				//var lredext = redEnb && lng && sig == 3;
				var lreduce = redEnb && sig == 3;

				var s0 = sfilter && mtDnOk && newTrendEnb && scSig[ii - 1] != -1 && scSig[ii] == -1 && (!ntObOsEnb || ft1[ii] > 25); // && ftgt20[ii] == 1; //&& ftgd[ii] == 1/* && ftgt15[ii] == 1*/;
				var s1 = sfilter && mtDnOk && ptEnb && pt[ii] < 0 && scSig[ii] == -1;
				var s2 = sfilter && mtDnOk && addEnb && sig == -5;
				//var s3 = c2d[ii] == 1 && twobarEnb && twobar[ii] < 0 && scSig[ii] == -1;
				//var s4 = sfilter && mtDnOk && newTrendEnb && ii == index1 && scSig[ii] == -1;
				var s5 = sfilter && mtDnOk && ftEnb && ftEntry[ii] < 0;

				//var sredext = redEnb && sht && sig == -3;
				var sreduce = redEnb && sig == -3;

				var lentry = longEnb && (l0 || l1 || l2 || l5);
				var lexitExh = lng && exhEnb && sig == 2;
				var lexitPExh = lng && pExhEnb && pexh[ii] < 0;
				var lexit1 = scSig[ii - 1] == 1 && scSig[ii] == -1;
				var lexit2 = mtFtUp[ii - 1] == 1 && mtFtUp[ii] == 0;
				var lexit3 = ftEnb && ftEntry[ii] < 0 && (useMTFilterExit ? mtDnOk : true);
				var lexit4 = ptEnb && ptUpWithin3Bars[ii] < 3 && ftgd[ii] > 0 && stgd[ii] > 0 && st1[ii] < ft1[ii];
				var lexit = lng && (lexit1 || lexit2 || lexit3 || lexit4); //|| fttd[ii] == 1);

				var sentry = shortEnb && (s0 || s1 || s2 || s5);
				var sexitExh = sht && exhEnb && sig == -2;
                var sexitPExh = sht && pExhEnb && pexh[ii] > 0;
                var sexit1 = scSig[ii - 1] == -1 && scSig[ii] == 1;
                var sexit2 = mtFtDn[ii - 1] == 1 && mtFtDn[ii] == 0;
				var sexit3 = ftEnb && ftEntry[ii] > 0 && (useMTFilterExit ? mtUpOk: true);
				var sexit4 = ptEnb && ptDnWithin3Bars[ii] < 3 && ftgu[ii] > 0 && stgu[ii] > 0 && st1[ii] > ft1[ii];
				var sexit = sht && (sexit1 || sexit2 || sexit3 || sexit4); // || fttu[ii] == 1);

				var enter = !lng && lentry || !sht && sentry;
				var addOn = lng && lentry || sht && sentry;

				var reduc = lreduce || sreduce;

                var twoBarReduce = twoBarEnb && ((lng && twoBar[ii] < 0) || (sht && twoBar[ii] > 0));

                var ftRocReduce = twoBarEnb && ((lng && ftRoc[ii] > 0) || (sht && ftRoc[ii] < 0));

                var newSig = !lng && l0 || !sht && s0;
				var prsSig = (lng || lentry) && l1 || (sht || sentry) && s1;
				var addSig = (lng || lentry) && l2 || (sht || sentry) && s2;
				var addFT = (lng || lentry) && l5 || (sht || sentry) && s5;
                
				var price1 = cl[ii - 1];
				var price = cl[ii];

				period++;

				if (lng || sht)
				{
                    if (useConviction)
                    {
                        if (price > price1)
                        {
                            size *= (lng ? 1 - sizes["ConvictionPercent"] / 100.0 : 1 + sizes["ConvictionPercent"] / 100.0);
                            tradeType = TradeType.Bias;
                        }
                        else if (price < price1)
                        {
                            size *= (lng ? 1 + sizes["ConvictionPercent"] / 100.0 : 1 - sizes["ConvictionPercent"] / 100.0);
							tradeType = TradeType.Bias;
						}
                    }

					if (addOn)
                    {
                        if (!enbs["FreqPeriodEnable"] || period > freqPeriod)
                        {
                            tradeType = prsSig ? TradeType.Pressure : TradeType.Add;
                            period = 0;
                            size += unitSize * (prsSig ? sizes["PressureUnit"] : sizes["AddUnit"]);
                            if (size > maxUnitSize)
                            {
                                size = maxUnitSize;
                            }
                        }
                    }

					if (reduc)
					{
                        tradeType = TradeType.Retrace;
						size *= 1 - sizes["RetracePercent"] / 100.0;
						if (size <= 0)
						{
                            size = 0;
							if (lng) lexit = true;
							else if (sht) sexit = true;
						}
					}

					if (twoBarReduce)
					{
						tradeType = TradeType.Retrace;
						size *= 1 - sizes["TwoBarPercent"] / 100.0;
						if (size <= 0)
						{
                            size = 0;
							if (lng) lexit = true;
							else if (sht) sexit = true;
						}
					}

					if (ftRocReduce)
					{
						tradeType = TradeType.Retrace;
						size *= 1 - sizes["FTRocPercent"] / 100.0;
						if (size <= 0)
						{
                            size = 0;
							if (lng) lexit = true;
							else if (sht) sexit = true;
						}
					}

					// P alert reduction
					if (lexitPExh || sexitPExh)
                    {
                        tradeType = TradeType.PExhaustion;
                        size *= 1 - sizes["PExhaustionPercent"] / 100.0;
						if (size <= 0)
                        {
                            size = 0;
							if (lng) lexit = true;
							else if (sht) sexit = true;
						}
					}

                    // exhaustion reduction
					if (lexitExh || sexitExh)
					{
						tradeType = TradeType.Exhaustion;
						size *= 1 - sizes["ExhaustionPercent"] / 100.0;
						if (size <= 0)
						{
                            size = 0;
							if (lng) lexit = true;
							else if (sht) sexit = true;
						}
					}

                    // time reduction
                    if (timeExitEnb)
                    {
                        if (ii - entryIndex >= sizes["TimeExitCount"])
                        {
							tradeType = TradeType.TimeExit;
							size *= 1 - sizes["TimeExitPercent"] / 100.0;
							if (size <= 0)
							{
                                size = 0;
								if (lng) lexit = true;
								else if (sht) sexit = true;
							}
						}
                    }

                    if (proveItEnb)
                    {
                        var barCnt = ii - entryIndex;
      //                  if (barCnt == 1)
      //                  {
      //                      if (lng)
      //                      {
      //                          lexit = cl[ii] < cl[entryIndex] - atr[entryIndex];
      //                      }
      //                      else
      //                      {
						//		sexit = cl[ii] > cl[entryIndex] + atr[entryIndex];
						//	}
      //                  }
      //                  else if (barCnt == 2)
      //                  {
						//	if (lng)
						//	{
						//		lexit = cl[ii] < cl[ii - 1] && cl[ii - 1] < cl[ii - 2];
						//	}
						//	else
						//	{
						//		sexit = cl[ii] > cl[ii - 1] && cl[ii - 1] > cl[ii - 2];
						//	}
						//}
						//else if (barCnt == 3)
						{
							if (lng)
							{
								lexit = cl[ii] < cl[ii - 3];
							}
							else
							{
								sexit = cl[ii] > cl[ii - 3];
							}
						}
					}
				}

                if (enter)
                {
                    tradeType = newSig ? TradeType.NewTrend : prsSig ? TradeType.Pressure : addSig ? TradeType.Add : TradeType.FTEntry;
					size = unitSize * Math.Min(maxLeverage, newSig ? sizes["NewTrendUnit"] : prsSig ? sizes["PressureUnit"] : addSig ? sizes["AddUnit"] : sizes["FTEntryUnit"]);
                    if (size > maxUnitSize)
                    {
                        size = maxUnitSize;
                    }
					if (!newSig)
                    {
                        period = 0;
                    }
                    entryIndex = ii;
                }

                lng = lentry ? true : lexit ? false : lng;
				sht = sentry ? true : sexit ? false : sht;

                trades[time1[ii]] = side > 0 && lng || side < 0 && sht? size: 0;
			}

			return trades;
		}

		public static Series calculatePAlertReversion(Series op, Series hi, Series lo, Series cl, Series ft)
		{
			Series output = new Series();

			Series u1 = (ft < 15).And(cl > (lo + (hi - lo) * 0.618));
			Series d1 = (ft > 85).And(cl < (lo + (hi - lo) * 0.382));

			Series PrsUp = frequentSignalFilter(5, u1, u1);
			Series PrsDn = frequentSignalFilter(5, d1, d1);

			output = PrsUp - PrsDn;

			return output;
		}

		public static List<int> GetTrendCondition(int maxAgo, Series hi, Series lo, Series cl, bool reverse = false)
        {
            List<int> signals = new List<int>();

            Series TSB = atm.calculateTSB(hi, lo, cl);
            Series ST = atm.calculateST(hi, lo, cl);
            Series EZI = atm.calculateEZI(cl);

            Series upSet = (TSB > 30) & (ST > 75) & (((ST >> 6) < 20) | ((ST >> 7) < 20));
            Series dnSet = (TSB < 70) & (ST < 25) & (((ST >> 6) > 80) | ((ST >> 7) > 80));
            Series upRes = ST < 65;
            Series dnRes = ST > 35;

            Series Utsb = (EZI < 80) & (TSB >= 70);
            Series Dtsb = (EZI <= 20) & (TSB <= 30);
            Series Uezi = (EZI >= 80) & (TSB >= 70);
            Series Dezi = (EZI >= 20) & (TSB <= 30);
            Series Uset = (TSB < 70) & atm.setReset(upSet, upRes);
            Series Dset = (TSB > 30) & atm.setReset(dnSet, dnRes);

            Series FT = atm.calculateFT(hi, lo, cl);

            Series strongBullish = Uezi;
            Series earlyBullish = Utsb;
            Series upTransition = Uset;
            Series dnTransition = Dset;
            Series earlyBearish = Dtsb;
            Series strongBearish = Dezi;

            for (int ago = 0; ago < maxAgo; ago++)
            {
                int index = reverse ? ago : FT.Count - 1 - ago;

                if (index >= 0)
                {
                    int sig = 0;
                    if (strongBullish[index] == 1)
                    {
                        if (FT[index] >= 80) sig = 1;
                        else if (FT[index] <= 20) sig = 3;
                        else sig = 2;
                    }
                    else if (earlyBullish[index] == 1)
                    {
                        if (FT[index] >= 80) sig = 1;
                        else if (FT[index] <= 20) sig = 3;
                        else sig = 2;
                    }
                    else if (earlyBearish[index] == 1)
                    {
                        if (FT[index] >= 80) sig = 7;
                        else if (FT[index] <= 20) sig = 9;
                        else sig = 8;
                    }
                    else if (strongBearish[index] == 1)
                    {
                        if (FT[index] >= 80) sig = 7;
                        else if (FT[index] <= 20) sig = 9;
                        else sig = 8;
                    }
                    else
                    {
                        if (FT[index] >= 80) sig = 4;
                        else if (FT[index] <= 20) sig = 6;
                        else sig = 5;
                    }

                    signals.Add(sig);
                }
            }
            return signals;
        }

        public static List<int> GetTrendDirection(int maxAgo, Series hi, Series lo, Series cl, bool reverse = false)
        {
            List<int> signals = new List<int>();

            Series TSB = atm.calculateTSB(hi, lo, cl);
            Series ST = atm.calculateST(hi, lo, cl);
            Series EZI = atm.calculateEZI(cl);
            Series PRS = atm.calculatePressure(hi, lo, cl);

            Series upSet = (TSB > 30) & (ST > 75) & (((ST >> 6) < 20) | ((ST >> 7) < 20));
            Series dnSet = (TSB < 70) & (ST < 25) & (((ST >> 6) > 80) | ((ST >> 7) > 80));
            Series upRes = ST < 65;
            Series dnRes = ST > 35;
            Series upPrs = PRS.IsRising();
            Series dnPrs = PRS.IsFalling();

            Series Uezi = (upPrs > 0).Or(EZI >= 80) & (TSB >= 70);
            Series Utsb = (upPrs > 0).Or(EZI < 80) & (TSB >= 70);

            Series Dezi = (dnPrs > 0).Or(EZI >= 20) & (TSB <= 30);
            Series Dtsb = (dnPrs > 0).Or(EZI <= 20) & (TSB <= 30);

            //Series Dezi = (dnPrs > 0);
            Series Uset = (TSB < 70) & atm.setReset(upSet, upRes);
            Series Dset = (TSB > 30) & atm.setReset(dnSet, dnRes);

            Series FT = atm.calculateFT(hi, lo, cl);

            Series strongBullish = Uezi;
            Series earlyBullish = Utsb;
            Series upTransition = Uset;
            Series dnTransition = Dset;
            Series earlyBearish = Dtsb;
            Series strongBearish = Dezi;

            for (int ago = 0; ago < maxAgo; ago++)
            {
                int index = reverse ? ago : FT.Count - 1 - ago;

                if (index >= 0)
                {
                    int sig = 0;
                    if (strongBullish[index] == 1)
                    {
                        if (FT[index] >= 80) sig = 1;
                        else if (FT[index] <= 20) sig = 3;
                        else sig = 2;
                    }
                    else if (earlyBullish[index] == 1)
                    {
                        if (FT[index] >= 80) sig = 1;
                        else if (FT[index] <= 20) sig = 3;
                        else sig = 2;
                    }
                    else if (earlyBearish[index] == 1)
                    {
                        if (FT[index] >= 80) sig = 7;
                        else if (FT[index] <= 20) sig = 9;
                        else sig = 8;
                    }
                    else if (strongBearish[index] == 1)
                    {
                        if (FT[index] >= 80) sig = 7;
                        else if (FT[index] <= 20) sig = 9;
                        else sig = 8;
                    }
                    else
                    {
                        if (FT[index] >= 80) sig = 4;
                        else if (FT[index] <= 20) sig = 6;
                        else sig = 5;
                    }

                    signals.Add(sig);
                }
            }
            return signals;
        }
        public static Series highest(Series input, int period)
        {
            int count = input.Count;
            Series output = new Series(count);
            for (int ii = 0; ii < count; ii++)
            {
                double max = double.NaN;
                for (int jj = 0; jj < period; jj++)
                {
                    if (ii - jj >= 0 && !Double.IsNaN(input[ii - jj]))
                    {
                        if (double.IsNaN(max) || input[ii - jj] > max)
                        {
                            max = input[ii - jj];
                        }
                    }
                }
                output[ii] = max;
            }
            return output;
        }

        public static List<Series> calculate3Sigma(Series hi, Series lo, Series cl, bool noFilter = false)
        {
            int count = cl.Count;

            List<Series> ts = new List<Series>();
            ts.Add(new Series(count));
            ts.Add(new Series(count));
            ts.Add(new Series(count));
            ts.Add(new Series(count));
            ts.Add(new Series(count));
            ts.Add(new Series(count));
            ts.Add(new Series(count));

            Series md = Series.Mid(hi, lo);
            Series tr = Series.TrueRange(hi, lo, cl);

            Series simTR = Series.SimpleMovingAverage(tr, 69);
            Series expTR = Series.EMAvg(tr, 69);
            Series midAv = Series.SmoothAvg(Series.EMAvg(md, 69), 5);

            Dictionary<string, Series> tl = atm.calculateTrend(hi, lo, cl);
            Series TLup = tl["TLup"];
            Series TLdn = tl["TLdn"];

            for (int ii = 0; ii < count; ii++)
            {
                if (!double.IsNaN(cl[ii]) && !double.IsNaN(expTR[ii]) && !double.IsNaN(simTR[ii]) && !double.IsNaN(midAv[ii]))
                {
                    double v1 = midAv[ii] + 9.6 * expTR[ii];
                    double v2 = midAv[ii] + 6.4 * expTR[ii];
                    double v3 = midAv[ii] + 3.2 * expTR[ii];
                    double v4 = midAv[ii];
                    double v5 = midAv[ii] - 3.2 * expTR[ii];
                    double v6 = midAv[ii] - 6.4 * expTR[ii];
                    double v7 = midAv[ii] - 9.6 * expTR[ii];

                    double max = 4 * simTR[ii];

                    bool enbUp = (TLup[ii] == 1) || noFilter;
                    bool enbDn = (TLdn[ii] == 1) || noFilter;

					//ts[0][ii] = (enbUp && System.Math.Abs(v1 - cl[ii]) < max && v1 >= 0) ? v1 : double.NaN;
					//ts[1][ii] = (enbUp && System.Math.Abs(v2 - cl[ii]) < max && v2 >= 0) ? v2 : double.NaN;
					//ts[2][ii] = (enbUp && System.Math.Abs(v3 - cl[ii]) < max && v3 >= 0) ? v3 : double.NaN;
					//ts[3][ii] = (enbDn && System.Math.Abs(v5 - cl[ii]) < max && v5 >= 0) ? v5 : double.NaN;
					//ts[4][ii] = (enbDn && System.Math.Abs(v6 - cl[ii]) < max && v6 >= 0) ? v6 : double.NaN;
					//ts[5][ii] = (enbDn && System.Math.Abs(v7 - cl[ii]) < max && v7 >= 0) ? v7 : double.NaN;

					ts[0][ii] = v1;
					ts[1][ii] = v2;
					ts[2][ii] = v3;
					ts[3][ii] = v5;
					ts[4][ii] = v6;
					ts[5][ii] = v7;

					ts[6][ii] = v4;
                }
            }
            return ts;
        }

        public static List<Series> calculateTrendLines(Series high, Series low, Series close, double deviation)
        {
            List<Series> sl = new List<Series>();
            int count = close.Count;
            sl.Add(new Series(count));
            sl.Add(new Series(count));
            sl.Add(new Series(count));
            sl.Add(new Series(count));

            Series mid = (high + low) / 2;

            Series bs = Series.SmoothAvg(Series.EMAvg(high, 55), 5);
            Series ss = Series.SmoothAvg(Series.EMAvg(low, 55), 5);
            Series CPI = calculateCPI(mid, 34);
            Series ema1 = Series.EMAvg(close, 12);
            Series ema2 = Series.EMAvg(close, 26);
            Series fc1 = Series.SmoothAvg((ema1 * ema2) / ema1, 8);
            Series fc2 = Series.EMAvg(fc1, 9);
            Series hh = highest(high, 50);
            Series ll = lowest(low, 50);

            Series std1 = Series.StdDev(high, 55);  // blue
            Series std2 = Series.StdDev(low, 55);   // red
            Series stdUp1 = ss + std1 * deviation;
            Series stdLw1 = bs - std2 * deviation;

            bool ssx = false;
            bool bsx = false;
            bool be1 = false;
            bool se1 = false;
            bool sss = false;
            bool bss = false;

            for (int ii = 0; ii < count; ii++)
            {
                // entry signals
                bool b1s = false;
                bool s1s = false;
                bool bc = false;
                bool sc = false;
                if (!Double.IsNaN(fc1[ii]) &&
                    !Double.IsNaN(fc2[ii]) &&
                    !Double.IsNaN(CPI[ii]) &&
                    (ii > 0 && !Double.IsNaN(CPI[ii - 1])))
                {
                    b1s = fc1[ii] > fc2[ii] && CPI[ii] > 100 && CPI[ii] > CPI[ii - 1];
                    s1s = fc1[ii] < fc2[ii] && CPI[ii] < -100 && CPI[ii] < CPI[ii - 1];
                    bc = (ssx && !b1s) || s1s;
                    sc = (bsx && !s1s) || b1s;
                }

                // stop lines 
                double ssl = Double.NaN;
                double bsl = Double.NaN;
                if (!Double.IsNaN(close[ii]) &&
                    !Double.IsNaN(ss[ii]) &&
                    !Double.IsNaN(bs[ii]))
                {
                    sss = (b1s && close[ii] < ss[ii]) ? true : (close[ii] > ss[ii]) ? false : sss;
                    bss = (s1s && close[ii] > bs[ii]) ? true : (close[ii] < bs[ii]) ? false : bss;
                    ssl = (sss) ? ll[ii] : ss[ii];	   // sell trend line
                    bsl = (bss) ? hh[ii] : bs[ii];	   // buy trend line
                    ssx = close[ii] < ssl;		      // sell trend crossed
                    bsx = close[ii] > bsl;	      	// buy trend crossed
                }

                be1 = b1s ? true : bc ? false : be1;
                se1 = s1s ? true : sc ? false : se1;

                sl[0][ii] = be1 ? ssl : Double.NaN;
                sl[1][ii] = se1 ? bsl : Double.NaN;
                sl[2][ii] = be1 ? stdUp1[ii] : Double.NaN;
                sl[3][ii] = se1 ? stdLw1[ii] : Double.NaN;
            }
            return sl;
        }

        public static Series calculateMovingAvg(string type, Series price, int period)
        {
            var output = new Series();
            if (type == "Simple") output = Series.SimpleMovingAverage(price, period);
            else if (type == "Smooth") output = Series.SmoothAvg(price, period);
            else if (type == "Weighted") output = Series.WMAvg(price, period);
            else if (type == "Exponential") output = Series.EMAvg(price, period);
            return output;
        }

        public static Series calculateATR(Series hi, Series lo, Series cl, string type, int period)
        {
            var output = new Series();
            Series tr = Series.TrueRange(hi, lo, cl);
            if (type == "Simple") output = Series.SimpleMovingAverage(tr, period);
            else if (type == "Smooth") output = Series.SmoothAvg(tr, period);
            else if (type == "Weighted") output = Series.WMAvg(tr, period);
            else if (type == "Exponential") output = Series.EMAvg(tr, period);
            return output;
        }

        public static Series calculateROC(Series price, int period)
        {
            var output = (price * 100) / price.ShiftRight(period);           
            return output;
        }

        public static Series calculateRSI(Series price, int period)
        {
            return Series.RSI(price, period);
        }

        public static List<Series> calculateBollinger(string type, Series price, int period, double stddev)
        {
            Series ma = atm.calculateMovingAvg(type, price, (int)period);
            Series stdDev = price.StdDev((int)period);
            Series ub = ma + stdDev * stddev;
            Series lb = ma - stdDev * stddev;
            var output = new List<Series>();
            output.Add(ma);
            output.Add(ub);
            output.Add(lb);
            return output;
        }

        public static Series calculateMOM(Series price, int period)
        {
            return Series.Momentum(price, period);
        }

        public static Series calculateMovingAvg200(Series close)
        {
            Series cl = close;
            Series ma200 = Series.SimpleMovingAverage(close, 200);

            return ma200;
        }

        public static Series calculateMovingAvg100(Series close)
        {
            Series cl = close;
            Series ma100 = Series.SimpleMovingAverage(close, 100);

            return ma100;
        }

        public static Series calculateMovingAvg50(Series close)
        {
            Series cl = close;
            Series ma50 = Series.SimpleMovingAverage(close, 50);

            return ma50;
        }

        public static Series calculateTRT(Series close)
        {
            return calculateTRTPrivate(close);
        }

        private static Series calculateTRTPrivate(Series close)
        {
            Series trt = Series.LinearReg(RSI(Series.LinearReg(close, 5), 3), 5);
            return trt;
        }

        public static Series calculateFT_Mid(Series high, Series low, Series close)
        {
            Series x1 = lowest(low, 13);
            Series x2 = highest(high, 13);
            Series x3 = Series.EMAvg(((close - x1) * 100) / (x2 - x1), 3);
            Series x4 = lowest(x3, 13);
            Series x5 = highest(x3, 13);
            Series x6 = Series.EMAvg(((x3 - x4) * 100) / (x5 - x4), 3);
            Series x7 = Series.SmoothAvg(Series.LinearReg(x6, 13), 2);
            return x7;
        }

        public static Series calculateFT_LTerm(Series high, Series low, Series close)
        {
            Series x1 = lowest(low, 21);
            Series x2 = highest(high, 21);
            Series x3 = Series.EMAvg(((close - x1) * 100) / (x2 - x1), 3);
            Series x4 = lowest(x3, 21);
            Series x5 = highest(x3, 21);
            Series x6 = Series.EMAvg(((x3 - x4) * 100) / (x5 - x4), 3);
            Series x7 = Series.SmoothAvg(Series.LinearReg(x6, 21), 2);
            return x7;
        }

        public static Series calculateST(Series high, Series low, Series close)
        {
            Series x1 = lowest(low, 21);
            Series x2 = highest(high, 21);
            Series x3 = Series.SimpleMovingAverage((close - x1) * 100, 2) / Series.SimpleMovingAverage(x2 - x1, 2);
            Series x4 = Series.LinearReg(ReplaceNaN(x3, 50), 8);
            Series x5 = Series.SmoothAvg(x4, 2);
            for (int ii = close.Count - 1; ii >= 0; ii--)
            {
                if (double.IsNaN(close[ii])) x5[ii] = double.NaN;
                else break;
            }
            return x5;
        }

        public static Series calculateTSB(Series high, Series low, Series close)
        {
            Series x1 = lowest(low, 89);
            Series x2 = highest(high, 89);
            Series x3 = Series.EMAvg(((close - x1) * 100) / (x2 - x1), 3);
            Series x4 = lowest(x3, 89);
            Series x5 = highest(x3, 89);
            Series x6 = Series.EMAvg(((x3 - x4) * 100) / (x5 - x4), 3);
            Series x7 = Series.SmoothAvg(x6, 8);
            return x7;
        }

        public static Series calculateEZI(Series close)
        {
            Series x1 = Series.EMAvg(close, 12);
            Series x2 = Series.EMAvg(close, 26);
            Series x3 = (x1 * x2) / x1;
            Series x4 = Series.EMAvg(Series.SmoothAvg(x3, 8), 9);
            Series x5 = lowest(x4, 21);
            Series x6 = highest(x4, 21);
            Series x7 = (Series.SimpleMovingAverage(x4 - x5, 3) / Series.SimpleMovingAverage(x6 - x5, 3)) * 100;
            return x7;
        }

        public static Series getPxf(Dictionary<string, List<DateTime>> times, Dictionary<string, Series[]> bars, string[] intervalList)
        {
            Series output = null;

            string shortTerm = intervalList[0];
            string midTerm = (intervalList.Length > 1) ? intervalList[1] : intervalList[0];

            Series shortTermHi = bars[shortTerm][1];
            Series shortTermLo = bars[shortTerm][2];
            Series shortTermCl = bars[shortTerm][3];

            Series midTermHi = bars[midTerm][1];
            Series midTermLo = bars[midTerm][2];
            Series midTermCl = bars[midTerm][3];

            Series shortTermFt = atm.calculateFT(shortTermHi, shortTermLo, shortTermCl);
            Series midTermFt = atm.calculateFT(midTermHi, midTermLo, midTermCl);

            Series shortFTGoingUp = shortTermFt.IsRising();
            Series shortFTGoingDown = shortTermFt.IsFalling();
            Series midFTGoingUp = midTermFt.IsRising();
            Series midFTGoingDown = midTermFt.IsFalling();

            Series x1 = shortFTGoingUp.And(midFTGoingUp) * 2;
            Series x2 = shortFTGoingDown.And(midFTGoingUp);
            Series x3 = shortFTGoingUp.And(midFTGoingDown) * -1;
            Series x4 = shortFTGoingDown.And(midFTGoingDown) * -2;

            output = x1 + x2 + x3 + x4;

            return output;
        }

        public static Series getPxf(Dictionary<string, List<DateTime>> times, Dictionary<string, Series[]> bars, string interval)
        {
            Series output = null;

            Series hi = bars[interval][1];
            Series lo = bars[interval][2];
            Series cl = bars[interval][3];

            Series ft = atm.calculateFT(hi, lo, cl);

            Series ftGoingUp = ft.IsRising();
            Series ftGoingDn = ft.IsFalling();

            Series x1 = ftGoingUp * 1;
            Series x2 = ftGoingDn * -1;

            output = x1 + x2;

            return output;
        }

        public static Series getFTOBOS(Dictionary<string, List<DateTime>> times, Dictionary<string, Series[]> bars, string interval)
        {
            Series output = null;

            Series hi = bars[interval][1];
            Series lo = bars[interval][2];
            Series cl = bars[interval][3];

            Series ft = atm.calculateFT(hi, lo, cl);

            Series ftOB = ft > 75;
            Series ftOS = ft < 25;
            output = ftOB - ftOS;

            return output;
        }

        public static Series getSTOB(Dictionary<string, List<DateTime>> times, Dictionary<string, Series[]> bars, string interval)
        {
            Series output = null;

            Series hi = bars[interval][1];
            Series lo = bars[interval][2];
            Series cl = bars[interval][3];

            Series st = atm.calculateST(hi, lo, cl);

            Series stOB = st > 75;
            output = stOB;

            return output;
        }

        public static Series getSTOS(Dictionary<string, List<DateTime>> times, Dictionary<string, Series[]> bars, string interval)
        {
            Series output = null;

            Series hi = bars[interval][1];
            Series lo = bars[interval][2];
            Series cl = bars[interval][3];

            Series st = atm.calculateST(hi, lo, cl);

            Series stOS = st < 25;
            output = stOS;

            return output;
        }

        public static Series getSThigherFT(Dictionary<string, List<DateTime>> times, Dictionary<string, Series[]> bars, string interval)
        {
            Series output = null;

            Series hi = bars[interval][1];
            Series lo = bars[interval][2];
            Series cl = bars[interval][3];

            Series st = atm.calculateST(hi, lo, cl);
            Series ft = atm.calculateFT(hi, lo, cl);

            Series sthigherft = st > ft;
            output = sthigherft;

            return output;
        }

        public static Series getSTlowerFT(Dictionary<string, List<DateTime>> times, Dictionary<string, Series[]> bars, string interval)
        {
            Series output = null;

            Series hi = bars[interval][1];
            Series lo = bars[interval][2];
            Series cl = bars[interval][3];

            Series st = atm.calculateST(hi, lo, cl);
            Series ft = atm.calculateFT(hi, lo, cl);

            Series stlowerft = st < ft;
            output = stlowerft;

            return output;
        }

        public static Series getScore(Dictionary<string, List<DateTime>> times, Dictionary<string, Series[]> bars, string[] intervalList)
        {
            string shortTerm = intervalList[0];
            string midTerm = (intervalList.Length > 1) ? intervalList[1] : intervalList[0];

            Series shortTermHi = bars[shortTerm][1];
            Series shortTermLo = bars[shortTerm][2];
            Series shortTermCl = bars[shortTerm][3];

            Series midTermHi = bars[midTerm][1];
            Series midTermLo = bars[midTerm][2];
            Series midTermCl = bars[midTerm][3];

            Series midTermFt = atm.calculateFT(midTermHi, midTermLo, midTermCl);

            Series longerTermFt = convert(shortTerm, midTerm, times, midTermFt);
            Series longerTermFtDirection = convert(shortTerm, midTerm, times, midTermFt.IsRising() - midTermFt.IsFalling());

            Series series = calculateScore(shortTermHi, shortTermLo, shortTermCl, longerTermFt, longerTermFtDirection);

            return series;
        }

        public static Series getMidTermFT(Dictionary<string, List<DateTime>> times, Dictionary<string, Series[]> bars, string[] intervalList)
        {
            string shortTerm = intervalList[0];
            string midTerm = (intervalList.Length > 1) ? intervalList[1] : intervalList[0];

            Series shortTermHi = bars[shortTerm][1];
            Series shortTermLo = bars[shortTerm][2];
            Series shortTermCl = bars[shortTerm][3];

            Series midTermHi = bars[midTerm][1];
            Series midTermLo = bars[midTerm][2];
            Series midTermCl = bars[midTerm][3];

            Series midTermFt = atm.calculateFT(midTermHi, midTermLo, midTermCl);

            Series longerTermFt = convert(shortTerm, midTerm, times, midTermFt);
            Series longerTermFtDirection = convert(shortTerm, midTerm, times, midTermFt.IsRising() - midTermFt.IsFalling());

            return longerTermFtDirection;
        }

        public static Series convert(string shortTerm, string midTerm, Dictionary<string, List<DateTime>> times, Series input)
        {
            int barCount = times[shortTerm].Count;
            Series output = new Series(barCount, 0);

            DateTime now = DateTime.UtcNow;
            int jj = 0;
            for (int ii = 0; ii < barCount; ii++)  // short term times
            {
                DateTime shortTermTime = getStartTime(times[shortTerm][ii], shortTerm);
                if (shortTermTime > now) break;

                for (; jj < times[midTerm].Count - 1; jj++) // mid term times
                {
                    DateTime midTermTime = getStartTime(times[midTerm][jj + 1], midTerm);
                    if (midTermTime > shortTermTime)
                    {
                        break;
                    }
                }
                if (0 <= ii && ii < output.Count && 0 <= jj && jj < input.Count)
                {
                    output[ii] = input[jj];
                }
            }
            return output;
        }

        static DateTime getStartTime(DateTime input, string interval)
        {
            DateTime output = input;

            if (input != default(DateTime))
            {
                int year = input.Year;
                int month = input.Month;
                int day = input.Day;
                if (interval == "Weekly")
                {
                    output = input - new TimeSpan(5, 0, 0, 0, 0);
                }
                else if (interval == "Monthly")
                {
                    output = new DateTime(year, month, 1);
                }
                else if (interval == "Quarterly")
                {
                    output = new DateTime(year, month - ((month - 1) % 3), 1, 0, 0, 0, 0);
                }
                else if (interval == "SemiAnnually")
                {
                    output = new DateTime(year, month - ((month - 1) % 6), 1, 0, 0, 0, 0);
                }
                else if (interval == "Year")
                {
                    output = new DateTime(year, 1, 1, 0, 0, 0, 0);
                }
            }
            return output;
        }

        public static Series calculateRelativePrice(string interval, Series[] bars, Dictionary<string, object> referenceData, int ago)
        {
            Series cl = bars[3];

            int Count = cl.Count;

            Series relPrice = new Series(Count, 0.0);

            if (referenceData != null)
            {
                string key = "Index Prices : " + interval;
                if (referenceData.ContainsKey(key))
                {
                    Series ic = (Series)referenceData[key];

                    Series sym5 = cl.ShiftRight(ago);
                    Series idx5 = ic.ShiftRight(ago);
                    Series eret = ((cl - sym5) / sym5) + 1;
                    Series iret = ((ic - idx5) / idx5) + 1;
                    Series relativeIndex = ((eret / iret) - 1) * 100;

                    for (int ii = 0; ii < relativeIndex.Count; ii++)
                    {
                        double value = relativeIndex[ii];

                        relPrice[ii] = value;
                    }
                }
            }
            return relPrice;
        }

        public static Series calculateScore(Series hi, Series lo, Series cl, Series midTermFt, Series midTermFtDir)
        {
            List<int> signals = new List<int>();

            Series FT = atm.calculateFT(hi, lo, cl);
            Series ST = atm.calculateST(hi, lo, cl);
            Series EZI = atm.calculateEZI(cl);
            Series TSB = atm.calculateTSB(hi, lo, cl);

            //TSB
            Series upSet = (TSB > 30).And(ST > 75).And((ST.ShiftRight(6) < 20).Or(ST.ShiftRight(7) < 20));
            Series dnSet = (TSB < 70).And(ST < 25).And((ST.ShiftRight(6) > 80).Or(ST.ShiftRight(7) > 80));
            Series upRes = ST < 65;
            Series dnRes = ST > 35;

            Series Utsb = (EZI >= 80).And(TSB >= 70);
            Series Dtsb = (EZI <= 20).And(TSB <= 30);
            Series Uezi = (EZI <= 80).And(TSB >= 70);
            Series Dezi = (EZI >= 20).And(TSB <= 30);
            Series Uset = (TSB < 70) * atm.setReset(upSet, upRes);
            Series Dset = (TSB > 30) * atm.setReset(dnSet, dnRes);

            Series tsbUp = Utsb.Or(Uezi.Or(Uset));
            Series tsbDn = Dezi.Or(Dtsb).Or(Dset);

            // Pressure
            Series PressureClip = TSB.ClipAbove(70).ClipBelow(30);

            Series PressureUp1 = atm.goingUp(PressureClip);
            Series PressureDn1 = atm.goingDn(PressureClip);

            Series Pressure = TSB;

            //TB
            Series mid = (hi + lo) / 2;
            Series ma = Series.EMAvg(mid, 34);

            Series TBUp = lo > ma;
            Series TBDn = hi < ma;
            Series TBNu = lo <= ma & hi >= ma;
            Series TB = (lo > ma) - (hi < ma); // TB[ii] == 0 for neutral, TB[ii] == 1 for Up, TB[ii] == -1 for down


            //TL
            List<Series> trendLines = atm.calculateTrendLines(hi, lo, cl, 3);
            Series tlUp = Series.NotEqual(trendLines[0].ReplaceNaN(0), 0);
            Series tlDn = Series.NotEqual(trendLines[1].ReplaceNaN(0), 0);
            Series TL = tlUp - tlDn; // TL[ii] == 0 for neutral, TL[ii] == 1 for Up, TL[ii] == -1 for down

            //TSB Assigments
            
            int count1 = TSB.Count;

            Series TSBAssignments = new Series(count1);

            int value1 = 0;

            for (int ii = 0; ii < count1; ii++)
            {
                if (!double.IsNaN(cl[ii]))
                {
                    if (TSB[ii] > 70) value1 = 100;
                    else if (ii > 0 && TSB[ii] > TSB[ii - 1] && TSB[ii] > 60 && TSB[ii] <= 70) value1 = 90;
                    else if (ii > 0 && TSB[ii] > TSB[ii - 1] && TSB[ii] > 50 && TSB[ii] <= 60) value1 = 80;
                    else if (ii > 0 && TSB[ii] > TSB[ii - 1] && TSB[ii] > 40 && TSB[ii] <= 50) value1 = 70;
                    else if (ii > 0 && TSB[ii] > TSB[ii - 1] && TSB[ii] > 30 && TSB[ii] <= 40) value1 = 60;

                    else if (ii > 0 && TSB[ii] < TSB[ii - 1] && TSB[ii] > 60 && TSB[ii] <= 70) value1 = 40;
                    else if (ii > 0 && TSB[ii] < TSB[ii - 1] && TSB[ii] > 50 && TSB[ii] <= 60) value1 = 30;
                    else if (ii > 0 && TSB[ii] < TSB[ii - 1] && TSB[ii] > 40 && TSB[ii] <= 50) value1 = 20;
                    else if (ii > 0 && TSB[ii] < TSB[ii - 1] && TSB[ii] > 30 && TSB[ii] <= 40) value1 = 10;
                    else if (TSB[ii] <= 30) value1 = 1;

                    else value1 = 50;

                    TSBAssignments[ii] = value1;
                }
            }

            //FT Assigments

            int count2 = FT.Count;

            Series FTAssignments = new Series(count2);

            int value2 = 0;

            for (int ii = 0; ii < count2; ii++)
            {

                if (ii > 0 && FT[ii] > FT[ii - 1] && FT[ii] > 80 && FT[ii - 1] > 80 && FT[ii - 2] > 80 && FT[ii - 3] > 80 && FT[ii - 4] > 80) value2 = 70;

                else if (ii > 0 && FT[ii] < FT[ii - 1] && FT[ii] < 20 && FT[ii - 1] < 20 && FT[ii - 2] < 20 && FT[ii - 3] < 20 && FT[ii - 4] < 20) value2 = 30;

                // FT Going Up 
                else if (ii > 0 && FT[ii] > FT[ii - 1] && FT[ii] > 90) value2 = 60;
                else if (ii > 0 && FT[ii] > FT[ii - 1] && FT[ii] > 80 && FT[ii] <= 90) value2 = 65;
                else if (ii > 0 && FT[ii] > FT[ii - 1] && FT[ii] > 70 && FT[ii] <= 80) value2 = 65;
                else if (ii > 0 && FT[ii] > FT[ii - 1] && FT[ii] > 60 && FT[ii] <= 70) value2 = 65;
                else if (ii > 0 && FT[ii] > FT[ii - 1] && FT[ii] > 50 && FT[ii] <= 60) value2 = 75;
                else if (ii > 0 && FT[ii] > FT[ii - 1] && FT[ii] > 40 && FT[ii] <= 50) value2 = 75;
                else if (ii > 0 && FT[ii] > FT[ii - 1] && FT[ii] > 30 && FT[ii] <= 40) value2 = 75;
                else if (ii > 0 && FT[ii] > FT[ii - 1] && FT[ii] > 20 && FT[ii] <= 30) value2 = 80;
                else if (ii > 0 && FT[ii] > FT[ii - 1] && FT[ii] > 10 && FT[ii] <= 20) value2 = 90;
                else if (ii > 0 && FT[ii] > FT[ii - 1] && FT[ii] <= 10) value2 = 100;

                // FT Going Dn
                else if (ii > 0 && FT[ii] < FT[ii - 1] && FT[ii] > 90) value2 = 1;
                else if (ii > 0 && FT[ii] < FT[ii - 1] && FT[ii] > 80 && FT[ii] <= 90) value2 = 10;
                else if (ii > 0 && FT[ii] < FT[ii - 1] && FT[ii] > 70 && FT[ii] <= 80) value2 = 20;
                else if (ii > 0 && FT[ii] < FT[ii - 1] && FT[ii] > 60 && FT[ii] <= 70) value2 = 25;
                else if (ii > 0 && FT[ii] < FT[ii - 1] && FT[ii] > 50 && FT[ii] <= 60) value2 = 25;
                else if (ii > 0 && FT[ii] < FT[ii - 1] && FT[ii] > 40 && FT[ii] <= 50) value2 = 25;
                else if (ii > 0 && FT[ii] < FT[ii - 1] && FT[ii] > 30 && FT[ii] <= 40) value2 = 35;
                else if (ii > 0 && FT[ii] < FT[ii - 1] && FT[ii] > 20 && FT[ii] <= 30) value2 = 35;
                else if (ii > 0 && FT[ii] < FT[ii - 1] && FT[ii] > 10 && FT[ii] <= 20) value2 = 35;
                else if (ii > 0 && FT[ii] < FT[ii - 1] && FT[ii] <= 10) value2 = 40;

                else value2 = 50;

                FTAssignments[ii] = value2;
            }

            //ST Assigments

            int count3 = ST.Count;

            Series STAssignments = new Series(count3);

            int value3 = 0;

            for (int ii = 0; ii < count3; ii++)
            {
                if (ii > 0 && ST[ii] > ST[ii - 1] && ST[ii] > 80 && ST[ii - 1] > 80 && ST[ii - 2] > 80 && ST[ii - 3] > 80 && ST[ii - 4] > 80) value2 = 100;

                else if (ii > 0 && ST[ii] < ST[ii - 1] && ST[ii] < 20 && ST[ii - 1] < 20 && ST[ii - 2] < 20 && ST[ii - 3] < 20 && ST[ii - 4] < 20) value2 = 1;

                //ST going up 
                else if (ii > 0 && ST[ii] > 75) value3 = 100;  //100
                else if (ii > 0 && ST[ii] > ST[ii - 1] && ST[ii] > 60 && ST[ii] <= 75) value3 = 85;  //80
                else if (ii > 0 && ST[ii] > ST[ii - 1] && ST[ii] > 40 && ST[ii] <= 60) value3 = 65;  //70
                else if (ii > 0 && ST[ii] > ST[ii - 1] && ST[ii] > 25 && ST[ii] <= 40) value3 = 55;  //70
                else if (ii > 0 && ST[ii] <= 25) value3 = 1;  //60

                ////ST going dn 
                else if (ii > 0 && ST[ii] < ST[ii - 1] && ST[ii] > 75) value3 = 100;   //40
                else if (ii > 0 && ST[ii] < ST[ii - 1] && ST[ii] > 60 && ST[ii] <= 75) value3 = 45;   //30
                else if (ii > 0 && ST[ii] < ST[ii - 1] && ST[ii] > 40 && ST[ii] <= 60) value3 = 35;   //20
                else if (ii > 0 && ST[ii] < ST[ii - 1] && ST[ii] > 25 && ST[ii] <= 40) value3 = 15;   //20
                else if (ii > 0 && ST[ii] <= 25) value3 = 1;   //1

                else value3 = 50;

                STAssignments[ii] = value3;
            }

            //TB Assignments

            int count4 = TBUp.Count;

            Series TBAssignments = new Series(count4);

            int value4 = 0;

            for (int ii = 0; ii < count4; ii++)
            {
                if (TBUp[ii] > 0) value4 = 100;
                else if (TBDn[ii] > 0) value4 = 1;

                else value4 = 50;

                TBAssignments[ii] = value4;
            }

            //TL Assignments

            int count5 = tlUp.Count;

            Series TLAssignments = new Series(count5);

            int value5 = 0;

            for (int ii = 0; ii < count5; ii++)
            {
                if (tlUp[ii] > 0) value5 = 100;
                else if (tlDn[ii] > 0) value5 = 1;

                else value5 = 50;

                TLAssignments[ii] = value5;
            }

            //FTST Assignments

            int count6 = tlUp.Count;

            Series FTSTAssignments = new Series(count6);

            int value6 = 0;

            for (int ii = 0; ii < count6; ii++)
            {
                //FT < ST
                if ((ST[ii] - FT[ii]) > 50) value6 = 100;
                else if ((ST[ii] - FT[ii]) > 40) value6 = 90;
                else if ((ST[ii] - FT[ii]) > 30) value6 = 80;
                else if ((ST[ii] - FT[ii]) > 20) value6 = 70;
                else if ((ST[ii] - FT[ii]) > 10) value6 = 60;
                else if ((ST[ii] - FT[ii]) > 1) value6 = 55;

                //FT > ST
                else if ((FT[ii] - ST[ii]) > 50) value6 = 1;
                else if ((FT[ii] - ST[ii]) > 40) value6 = 10;
                else if ((FT[ii] - ST[ii]) > 30) value6 = 20;
                else if ((FT[ii] - ST[ii]) > 20) value6 = 30;
                else if ((FT[ii] - ST[ii]) > 10) value6 = 40;
                else if ((FT[ii] - ST[ii]) > 1) value6 = 45;

                else value6 = 50;

                FTSTAssignments[ii] = value6;
            }

            //MidFT Assignments

            int count7 = tlUp.Count;

            Series MidFTAssignments = new Series(count7, 50);

            if (midTermFt != null)
            {
                int value7 = 0;

                for (int ii = 0; ii < count7; ii++)
                {
                    double ft = midTermFt[ii];
                    bool goingUp = (ii > 0 && midTermFtDir[ii] == 1);
                    bool goingDn = (ii > 0 && midTermFtDir[ii] == -1);

                    if (goingUp && ft > 0 && ft <= 90) value7 = 100;
                    else if (goingUp && ft > 90 && ft <= 100) value7 = 80;
                    else if (goingDn && ft < 100 && ft >= 10) value7 = 1;
                    else if (goingDn && ft < 10 && ft > 0) value7 = 20;

                    else value7 = 50;

                    MidFTAssignments[ii] = value7;
                }
            }

            Series SCORE =
                (TSBAssignments * 0.19) +   //.22
                (FTAssignments * 0.17) +     //.19
                (STAssignments * 0.19) +     //(STAssignments * 0.17) 
                (TBAssignments * 0.10) +    //.13
                (TLAssignments * 0.10) +    //.13
                (FTSTAssignments * 0.10) +  //.10 w.13  
                (MidFTAssignments * 0.16)   //.18  w.20  
                ;

            return SCORE;
        }

        public static List<Series> calculateUpperTargets(Series high, Series low, Series TLup)
        {
            List<Series> output = new List<Series>();

            int count = high.Count;

            Series t1 = new Series(count);
            Series t2 = new Series(count);
            Series t3 = new Series(count);
            Series t4 = new Series(count);
            Series t5 = new Series(count);

            double v1 = 0;
            double v2 = 0;
            double v3 = 0;
            double v4 = 0;
            double v5 = 0;

            Dictionary<string, Series> min = minimum(low, 50);

            for (int ii = 0; ii < count; ii++)
            {
                if (ii > 0 && TLup[ii] == 1 && TLup[ii - 1] == 0)
                {
                    double max = high[ii - 1];
                    int ago = (int)min["ago"][ii - 1];
                    for (int jj = ii - 1; jj > 0 && ago >= 0; jj--, ago--)
                    {
                        max = Math.Max(max, high[jj]);
                    }

                    double hi = max;
                    double lo = min["min"][ii - 1];
                    if (!double.IsNaN(hi) && !double.IsNaN(lo))
                    {
                        double range = hi - lo;
                        double pt = hi - 0.618 * range;
                        v1 = pt + 1.000 * range;
                        v2 = pt + 1.618 * range;
                        v3 = pt + 2.618 * range;
                        v4 = pt + 4.236 * range;
                        v5 = pt + 6.857 * range;
                    }
                }

                // check for end
                if (ii > 0 && TLup[ii] == 0 && TLup[ii - 1] == 1)
                {
                    v1 = 0;
                    v2 = 0;
                    v3 = 0;
                    v4 = 0;
                    v5 = 0;
                }

                t1[ii] = v1;
                t2[ii] = v2;
                t3[ii] = v3;
                t4[ii] = v4;
                t5[ii] = v5;
            }

            output.Add(t1);
            output.Add(t2);
            output.Add(t3);
            output.Add(t4);
            output.Add(t5);

            return output;
        }

        public static List<Series> calculateLowerTargets(Series high, Series low, Series TLdn)
        {
            List<Series> output = new List<Series>();

            int count = low.Count;

            Series t1 = new Series(count);
            Series t2 = new Series(count);
            Series t3 = new Series(count);
            Series t4 = new Series(count);
            Series t5 = new Series(count);

            double v1 = 0;
            double v2 = 0;
            double v3 = 0;
            double v4 = 0;
            double v5 = 0;

            Dictionary<string, Series> max = maximum(high, 50);

            for (int ii = 0; ii < count; ii++)
            {
                // check for start - find high
                if (ii > 0 && TLdn[ii] == 1 && TLdn[ii - 1] == 0)
                {
                    double min = low[ii - 1];
                    int ago = (int)max["ago"][ii];
                    for (int jj = ii - 1; jj > 0 && ago >= 0; jj--, ago--)
                    {
                        min = Math.Min(min, low[jj]);
                    }

                    double lo = min;
                    double hi = max["max"][ii];
                    if (!double.IsNaN(hi) && !double.IsNaN(lo))
                    {
                        double range = hi - lo;
                        double pt = lo + 0.618 * range;
                        v1 = pt - 1.000 * range;
                        v2 = pt - 1.618 * range;
                        v3 = pt - 2.618 * range;
                        v4 = pt - 4.236 * range;
                        v5 = pt - 6.857 * range;
                    }
                }

                // check for end
                if (ii > 0 && TLdn[ii] == 0 && TLdn[ii - 1] == 1)
                {
                    v1 = 0;
                    v2 = 0;
                    v3 = 0;
                    v4 = 0;
                    v5 = 0;
                }

                t1[ii] = v1;
                t2[ii] = v2;
                t3[ii] = v3;
                t4[ii] = v4;
                t5[ii] = v5;
            }

            output.Add(t1);
            output.Add(t2);
            output.Add(t3);
            output.Add(t4);
            output.Add(t5);

            return output;
        }

        public static List<Series> calculateUpperTargetLines(Series high, Series low, Series TLup, int currentIndex)
        {
            List<Series> output = new List<Series>();

            int count = high.Count;

            Series t1 = new Series(count);
            Series t2 = new Series(count);
            Series t3 = new Series(count);
            Series t4 = new Series(count);
            Series t5 = new Series(count);

            double v1 = double.NaN;
            double v2 = double.NaN;
            double v3 = double.NaN;
            double v4 = double.NaN;
            double v5 = double.NaN;

            double v4d = double.NaN;
            double v5d = double.NaN;

            Dictionary<string, Series> min = minimum(low, 50);

            for (int ii = 0; ii < count; ii++)
            {
                if (ii > 0 && TLup[ii] == 1 && TLup[ii - 1] == 0)
                {
                    double max = high[ii - 1];
                    int ago = (int)min["ago"][ii - 1];
                    for (int jj = ii - 1; jj > 0 && ago >= 0; jj--, ago--)
                    {
                        max = Math.Max(max, high[jj]);
                    }

                    double hi = max;
                    double lo = min["min"][ii - 1];
                    if (!double.IsNaN(hi) && !double.IsNaN(lo))
                    {
                        double range = hi - lo;
                        double pt = hi - 0.618 * range;
                        v1 = pt + 1.000 * range;
                        v2 = pt + 1.618 * range;
                        v3 = pt + 2.618 * range;
                        v4 = pt + 4.236 * range;
                        v5 = pt + 6.857 * range;

                        if (v1 <= 0) v1 = double.NaN;
                        if (v2 <= 0) v2 = double.NaN;
                        if (v3 <= 0) v3 = double.NaN;
                        if (v4 <= 0) v4 = double.NaN;
                        if (v5 <= 0) v5 = double.NaN;
                    }
                }

                t1[ii] = v1;
                t2[ii] = v2;
                t3[ii] = v3;
                t4[ii] = v4d;
                t5[ii] = v5d;

                bool end = (ii > 0 && TLup[ii] == 0 && TLup[ii - 1] != 0);

                if (!Double.IsNaN(v1) && (end || high[ii] > v1 || ii == currentIndex))
                {
                    v1 = double.NaN;
                }
                if (!Double.IsNaN(v2) && (end || high[ii] > v2 || ii == currentIndex))
                {
                    v2 = double.NaN;
                }
                if (!Double.IsNaN(v3) && (end || high[ii] > v3 || ii == currentIndex))
                {
                    v3 = double.NaN;
                    v4d = v4;
                }
                if (!Double.IsNaN(v4d) && (end || high[ii] > v4d || ii == currentIndex))
                {
                    v4d = double.NaN;
                    v5d = v5;
                }
                if (!Double.IsNaN(v5d) && (end || high[ii] > v5d || ii == currentIndex))
                {
                    v5d = double.NaN;
                }
            }

            output.Add(t1);
            output.Add(t2);
            output.Add(t3);
            output.Add(t4);
            output.Add(t5);

            return output;
        }

        public static List<Series> calculateLowerTargetLines(Series high, Series low, Series TLdn, int currentIndex)
        {
            List<Series> output = new List<Series>();

            int count = low.Count;

            Series t1 = new Series(count);
            Series t2 = new Series(count);
            Series t3 = new Series(count);
            Series t4 = new Series(count);
            Series t5 = new Series(count);

            double v1 = double.NaN;
            double v2 = double.NaN;
            double v3 = double.NaN;
            double v4 = double.NaN;
            double v5 = double.NaN;

            double v4d = double.NaN;
            double v5d = double.NaN;

            Dictionary<string, Series> max = maximum(high, 50);

            for (int ii = 0; ii < count; ii++)
            {
                // check for start - find high
                if (ii > 0 && TLdn[ii] == 1 && TLdn[ii - 1] == 0)
                {
                    double min = low[ii - 1];
                    int ago = (int)max["ago"][ii];
                    for (int jj = ii - 1; jj > 0 && ago >= 0; jj--, ago--)
                    {
                        min = Math.Min(min, low[jj]);
                    }

                    double lo = min;
                    double hi = max["max"][ii];
                    if (!double.IsNaN(hi) && !double.IsNaN(lo))
                    {
                        double range = hi - lo;
                        double pt = lo + 0.618 * range;
                        v1 = pt - 1.000 * range;
                        v2 = pt - 1.618 * range;
                        v3 = pt - 2.618 * range;
                        v4 = pt - 4.236 * range;
                        v5 = pt - 6.857 * range;

                        if (v1 <= 0) v1 = double.NaN;
                        if (v2 <= 0) v2 = double.NaN;
                        if (v3 <= 0) v3 = double.NaN;
                        if (v4 <= 0) v4 = double.NaN;
                        if (v5 <= 0) v5 = double.NaN;
                    }
                }

                t1[ii] = v1;
                t2[ii] = v2;
                t3[ii] = v3;
                t4[ii] = v4d;
                t5[ii] = v5d;

                bool end = (ii > 0 && TLdn[ii] == 0 && TLdn[ii - 1] != 0);

                if (!Double.IsNaN(v1) && (end || low[ii] < v1 || ii == currentIndex))
                {
                    v1 = double.NaN;
                }
                if (!Double.IsNaN(v2) && (end || low[ii] < v2 || ii == currentIndex))
                {
                    v2 = double.NaN;
                }
                if (!Double.IsNaN(v3) && (end || low[ii] < v3 || ii == currentIndex))
                {
                    v3 = double.NaN;
                    v4d = v4;
                }
                if (!Double.IsNaN(v4d) && (end || low[ii] < v4d || ii == currentIndex))
                {
                    v4d = double.NaN;
                    v5d = v5;
                }
                if (!Double.IsNaN(v5d) && (end || low[ii] < v5d || ii == currentIndex))
                {
                    v5d = double.NaN;
                }
            }

            output.Add(t1);
            output.Add(t2);
            output.Add(t3);
            output.Add(t4);
            output.Add(t5);

            return output;
        }

        public static Series not(Series input)
        {
            int count = input.Count;
            Series output = new Series(count);
            for (int ii = 0; ii < count; ii++)
            {
                output[ii] = (!double.IsNaN(input[ii]) && input[ii] == 0) ? 1 : 0;
            }
            return output;
        }

        // "sig" : 1 means a new maximum over the period
        // "max" : the maximum value over the period
        // "ago" : the number of bars ago the maximum occured
        public static Dictionary<string, Series> maximum(Series input, int period)
        {
            Dictionary<string, Series> output = new Dictionary<string, Series>();

            int count = input.Count;

            output["sig"] = new Series(count);
            output["max"] = new Series(count);
            output["ago"] = new Series(count);

            for (int ii = 0; ii < count; ii++)
            {
                double sig = 0;
                double ago = -1;
                double max = Double.NaN;
                for (int jj = 0; jj < period && ii >= jj; jj++)
                {
                    double value = input[ii - jj];
                    if (!Double.IsNaN(value))
                    {
                        if (double.IsNaN(max) || value > max)
                        {
                            max = value;
                            sig = 1;
                            ago = jj;
                        }
                    }
                }

                output["sig"][ii] = sig;
                output["max"][ii] = max;
                output["ago"][ii] = ago;
            }
            return output;
        }

        // "sig" : 1 means a new minimum over the period
        // "min" : the minimum value over the period
        // "ago" : the number of bars ago the minimum occured
        public static Dictionary<string, Series> minimum(Series input, int period)
        {
            Dictionary<string, Series> output = new Dictionary<string, Series>();

            int count = input.Count;

            output["sig"] = new Series(count);
            output["min"] = new Series(count);
            output["ago"] = new Series(count);

            for (int ii = 0; ii < input.Count; ii++)
            {
                double sig = 0;
                double ago = -1;
                double min = Double.NaN;
                for (int jj = 0; jj < period && ii >= jj; jj++)
                {
                    double value = input[ii - jj];
                    if (!Double.IsNaN(value))
                    {
                        if (double.IsNaN(min) || value < min)
                        {
                            min = value;
                            sig = 1;
                            ago = jj;
                        }
                    }
                }

                output["sig"][ii] = sig;
                output["min"][ii] = min;
                output["ago"][ii] = ago;
            }
            return output;
        }

        private static int getFirstBarIndex(Series TL)
        {
            int index = -1;
            for (int ii = TL.Count - 1; ii >= 0; ii--)
            {
                if (TL[ii] == 0)
                {
                    index = ii + 1;
                    break;
                }
            }
            return index;
        }

        public static Series goingUpBelowLevel(Series input, double level)
        {
            int count = input.Count;
            Series output = new Series(count);
            for (int ii = 0; ii < count; ii++)
            {
                double result = 0;
                bool check = (ii > 1 && !Double.IsNaN(input[ii - 1]) && !Double.IsNaN(input[ii]));
                if (check && input[ii] < level && input[ii - 1] < input[ii])
                {
                    result = 1;
                }
                output[ii] = result;
            }
            return output;
        }

        public static Series goingDnAboveLevel(Series input, double level)
        {
            int count = input.Count;
            Series output = new Series(count);
            for (int ii = 0; ii < count; ii++)
            {
                double result = 0;
                bool check = (ii > 1 && !Double.IsNaN(input[ii - 1]) && !Double.IsNaN(input[ii]));
                if (check && input[ii] > level && input[ii - 1] > input[ii])
                {
                    result = 1;
                }
                output[ii] = result;
            }
            return output;
        }

        public static Series turnsUpBelowLevel(Series input, double level)
        {
            int count = input.Count;
            Series output = new Series(count);
            for (int ii = 0; ii < count; ii++)
            {
                double result = 0;
                bool check = (ii > 1 && !Double.IsNaN(input[ii - 2]) && !Double.IsNaN(input[ii - 1]) && !Double.IsNaN(input[ii]));
                if (check && input[ii - 1] < level && input[ii - 2] >= input[ii - 1] && input[ii - 1] < input[ii])
                {
                    result = 1;
                }
                output[ii] = result;
            }
            return output;
        }

        public static Series turnsDnAboveLevel(Series input, double level)
        {
            int count = input.Count;
            Series output = new Series(count);
            for (int ii = 0; ii < count; ii++)
            {
                double result = 0;
                bool check = (ii > 1 && !Double.IsNaN(input[ii - 2]) && !Double.IsNaN(input[ii - 1]) && !Double.IsNaN(input[ii]));
                if (check && input[ii - 1] > level && input[ii - 2] <= input[ii - 1] && input[ii - 1] > input[ii])
                {
                    result = 1;
                }
                output[ii] = result;
            }
            return output;
        }

        public static Series turnsUpAboveLevel(Series input, double level)
        {
            int count = input.Count;
            Series output = new Series(count);
            for (int ii = 0; ii < count; ii++)
            {
                double result = 0;
                bool check = (ii > 1 && !Double.IsNaN(input[ii - 2]) && !Double.IsNaN(input[ii - 1]) && !Double.IsNaN(input[ii]));
                if (check && input[ii - 1] > level && input[ii - 2] >= input[ii - 1] && input[ii - 1] < input[ii])
                {
                    result = 1;
                }
                output[ii] = result;
            }
            return output;
        }

        public static Series turnsDnBelowLevel(Series input, double level)
        {
            int count = input.Count;
            Series output = new Series(count);
            for (int ii = 0; ii < count; ii++)
            {
                double result = 0;
                bool check = (ii > 1 && !Double.IsNaN(input[ii - 2]) && !Double.IsNaN(input[ii - 1]) && !Double.IsNaN(input[ii]));
                if (check && input[ii - 1] < level && input[ii - 2] <= input[ii - 1] && input[ii - 1] > input[ii])
                {
                    result = 1;
                }
                output[ii] = result;
            }
            return output;
        }

        public static Series nothasVal(Series input)
        {
            int count = input.Count;
            Series output = new Series(count);
            for (int ii = 0; ii < count; ii++)
            {
                output[ii] = Double.IsNaN(input[ii]) ? 1 : 0;
            }
            return output;
        }

        public static Series happenedWithin(Series input, int start, int end)
        {
            int count = input.Count;
            Series output = new Series(count);
            int ago = -1;
            for (int ii = 0; ii < count; ii++)
            {
                if (!Double.IsNaN(input[ii]))
                {
                    if (input[ii] != 0)
                    {
                        ago = 0;
                    }
                    else if (ago >= 0)
                    {
                        ago++;
                    }
                }
                output[ii] = (ago >= start && ago <= end) ? 1 : 0;
            }
            return output;
        }

        // outputs are "Usig", "Uago", "Dsig", and "Dago"
        public static Dictionary<string, Series> hook(Series input)
        {
            Dictionary<string, Series> output = new Dictionary<string, Series>();
            int count = input.Count;
            output["Usig"] = new Series(count);
            output["Uago"] = new Series(count);
            output["Dsig"] = new Series(count);
            output["Dago"] = new Series(count);
            double Uago = -1;
            double Dago = -1;
            for (int ii = 0; ii < count; ii++)
            {
                double Usig = 0;
                double Dsig = 0;
                bool check = (ii > 1 && !Double.IsNaN(input[ii - 2]) && !Double.IsNaN(input[ii - 1]) && !Double.IsNaN(input[ii]));
                if (check && input[ii - 1] < 20 && input[ii - 2] >= input[ii - 1] && input[ii - 1] < input[ii])
                {
                    Usig = 1;
                    Uago = 0;
                }
                else if (Uago >= 0)
                {
                    Uago++;
                }
                if (check && input[ii - 1] > 80 && input[ii - 2] <= input[ii - 1] && input[ii - 1] > input[ii])
                {
                    Dsig = 1;
                    Dago = 0;
                }
                else if (Dago >= 0)
                {
                    Dago++;
                }
                output["Usig"][ii] = Usig;
                output["Uago"][ii] = Uago;
                output["Dsig"][ii] = Dsig;
                output["Dago"][ii] = Dago;
            }
            return output;
        }

        // example
        // Series signal = signalLength(input) >= 55;
        public static Series signalLength(Series input)
        {
            int count = input.Count;
            Series output = new Series(count);
            double ago = 0;
            for (int ii = 0; ii < count; ii++)
            {
                if (!Double.IsNaN(input[ii]))
                {
                    if (input[ii] != 0)
                    {
                        ago++;
                    }
                    else
                    {
                        ago = 0;
                    }
                }

                output[ii] = ago;
            }
            return output;
        }

        public enum LevelSelection
        {
            Off,
            Level1Only,
            Level2Only,
            Level3Only,
            Levels1And2,
            Levels1And3,
            Levels2And3,
            AllLevels
        }

        public enum FirstAlertLevelSelection
        {
            InsideLevel1,
            InsideLevel2,
            AllLevels
        }

        public enum AddonAlertLevelSelection
        {
            InsideLevel1,
            InsideLevel2,
            InsideLevel3,
            AllLevels
        }

        public enum ExhaustionLevelSelection
        {
            OutsideLevel2,
            OutsideLevel3,
            AllLevels
        }

        public enum ExhaustionAlertSelection
        {
            ExhaustionAndDivergence,
            DivergenceOnly
        }

        public static Series calculateTrendBars(Series hi, Series lo, Series cl)
        {
            List<Series> TDI = atm.calculateTDI(hi, lo);
            Series up = (lo > TDI[1]).And(cl > TDI[0]).Replace(double.NaN, 0);
            Series dn = (hi < TDI[0]).And(cl < TDI[1]).Replace(double.NaN, 0);
            Series tb = up - dn;
            return tb;
        }

        public static Dictionary<string, Series> calculateTrend(Series high, Series low, Series close)
        {
            Dictionary<string, Series> Output = new Dictionary<string, Series>();

            Series FT = atm.calculateFT(high, low, close);
            Series ST = atm.calculateST(high, low, close);
            Series TSB = atm.calculateTSB(high, low, close);

            return calculateTrend(high, low, close, FT, ST, TSB);
        }


        public static Dictionary<string, Series> calculateTrend(Series high, Series low, Series close, Series FT, Series ST, Series TSB) 
        {
            Dictionary<string, Series> Output = new Dictionary<string, Series>();
            List<Series> TL = atm.calculateTrendLines(high, low, close, 2.0);

            Series tsb_gt_30 = (TSB > 30);
            Series tsb_gt_70 = (TSB > 70);
            Series tsb_lt_70 = (TSB < 70);
            Series tsb_lt_30 = (TSB < 30);

            Series tl_up = atm.hasVal(TL[0]);
            Series not_tl_up = atm.nothasVal(TL[0]);
            Series tl_dn = atm.hasVal(TL[1]);
            Series not_tl_dn = atm.notHasVal(TL[1]);

            Series upSet = (TSB > 30).And(ST > 75).And((ST.ShiftRight(6) < 20).Or(ST.ShiftRight(7) < 20));
            Series dnSet = (TSB < 70).And(ST < 25).And((ST.ShiftRight(6) > 80).Or(ST.ShiftRight(7) > 80));

            Series not_upSet = (upSet <= 0);
            Series not_dnSet = (dnSet <= 0);

            Series upRes = ST < 65;
            Series dnRes = ST > 35;
            Series Uset = (TSB < 70) * atm.setReset(upSet, upRes);
            Series Dset = (TSB > 30) * atm.setReset(dnSet, dnRes);

            Series up_sig = ((tl_up + upSet) * tsb_gt_30 * not_dnSet * turnsUpBelowLevel(FT, 25)) + (not_tl_dn * tsb_gt_70) + (tl_up * Uset);
            Series dn_sig = ((tl_dn + dnSet) * tsb_lt_70 * not_upSet * turnsDnAboveLevel(FT, 75)) + (not_tl_up * tsb_lt_30) + (tl_dn * Dset);

            Series TLup = atm.setReset(up_sig, dn_sig);
            Series TLdn = atm.setReset(dn_sig, up_sig);

            Output["TLup"] = TLup;
            Output["TLdn"] = TLdn;
            return Output;
#if false
            Series FT = calculateFT(high, low, close);
            Series ST = calculateST(high, low, close);
            Series TSB = calculateTSB(high, low, close);
            Series EZI = calculateEZI(close);
            List<Series> TL = calculateTrendLines(high, low, close, 2.0);

            Series Uezi = (EZI <= 80).And(TSB >= 70);
            Series Dezi = (EZI >= 20).And(TSB <= 30);

            Series Utsb = (EZI >= 80).And(TSB >= 70);
            Series Dtsb = (EZI <= 20).And(TSB <= 30);
 
            Series upSet = (TSB > 30).And(ST > 75).And((ST.ShiftRight(6) < 20).Or(ST.ShiftRight(7) < 20));
            Series dnSet = (TSB < 70).And(ST < 25).And((ST.ShiftRight(6) > 80).Or(ST.ShiftRight(7) > 80));
            Series upRes = ST < 65;
            Series dnRes = ST > 35;
            Series Uset = (TSB < 70) * setReset(upSet, upRes);
            Series Dset = (TSB > 30) * setReset(dnSet, dnRes);

            Series upTL = hasVal(TL[0]);
            Series dnTL = hasVal(TL[1]);

            List<Series> TDI = calculateTDI(high, low);
            Series upTDI = (low > TDI[1]).And(close > TDI[0]).Replace(double.NaN, 0);
            Series dnTDI = (high < TDI[0]).And(close < TDI[1]).Replace(double.NaN, 0);

            Series bull = Uezi.Or(Utsb).Or(Uset);
            Series bear = Dezi.Or(Dtsb).Or(Dset);
            Series notBull = Series.NotEqual(bull, 1);
            Series notBear = Series.NotEqual(bear, 1);

            Series TLup = bull.Or(notBull.And(upTL.And(upTDI)));
            Series TLdn = bear.Or(notBear.And(dnTL.And(dnTDI)));

            Output["TLup"] = TLup;
            Output["TLdn"] = TLdn;
            return Output;
#endif
        }

        private static Series calculate20Low(Series lo, Series FT)
        {
            int count = lo.Count;
            Series output = new Series(count);

            Series FTup = turnsUpBelowLevel(FT, 20);
            Series FTdn = turnsDnAboveLevel(FT, 80);
            Series Lo50 = Series.Equal(lo, lowest(lo, 50));

            double low1 = double.NaN;
            bool enable = false;
            for (int ii = 0; ii < count; ii++)
            {
                if (FTup[ii] == 1.0)
                {
                    enable = true;
                }

                if (enable)
                {
                    if (FTup[ii] == 1.0 || FTdn[ii] == 1.0)
                    {
                        low1 = double.NaN;
                    }

                    if (Lo50[ii] == 1.0)
                    {
                        if (double.IsNaN(low1) || lo[ii] < low1)
                        {
                            low1 = lo[ii];
                        }
                    }
                }
                output[ii] = low1;
            }
            return output;
        }

        private static Series calculate8020Low(Series lo, Series FT, bool bar50)
        {
            int count = lo.Count;
            Series output = new Series(count);

            Series FTdn = turnsDnAboveLevel(FT, 80);
            Series FTup = turnsUpBelowLevel(FT, 20);
            Series Lo50 = Series.Equal(lo, lowest(lo, 50));

            double low1 = double.NaN;
            double low2 = double.NaN;
            bool enable = false;
            for (int ii = 0; ii < count; ii++)
            {
                if (FTdn[ii] == 1.0)
                {
                    enable = true;
                }

                if (enable)
                {
                    if (FTdn[ii] == 1.0)
                    {
                        low2 = (!bar50 || (Lo50[ii] == 1)) ? lo[ii] : double.NaN;
                    }

                    if (FTup[ii] == 1.0)
                    {
                        low1 = low2;
                    }

                    if (!bar50 || (Lo50[ii] == 1))
                    {
                        if (double.IsNaN(low2) || lo[ii] < low2)
                        {
                            low2 = lo[ii];
                        }
                    }
                }
                output[ii] = low1;
            }
            return output;
        }

        private static Series calculate80High(Series hi, Series FT)
        {
            int count = hi.Count;
            Series output = new Series(count);

            Series FTdn = turnsDnAboveLevel(FT, 80);
            Series FTup = turnsUpBelowLevel(FT, 20);
            Series Hi50 = Series.Equal(hi, highest(hi, 50));

            double high1 = double.NaN;
            bool enable = false;
            for (int ii = 0; ii < count; ii++)
            {
                if (FTdn[ii] == 1.0)
                {
                    enable = true;
                }

                if (enable)
                {
                    if (FTdn[ii] == 1.0 || FTup[ii] == 1.0)  // reset peak to current high when FT turns down below 80
                    {
                        high1 = double.NaN;
                    }

                    if (Hi50[ii] == 1.0)
                    {
                        if (double.IsNaN(high1) || hi[ii] > high1)  // check if current high is higher than the peak
                        {
                            high1 = hi[ii];
                        }
                    }
                }
                output[ii] = high1;
            }
            return output;
        }

        private static Series calculate2080High(Series hi, Series FT, bool bar50)
        {
            int count = hi.Count;
            Series output = new Series(count);

            Series FTdn = turnsDnAboveLevel(FT, 80);
            Series FTup = turnsUpBelowLevel(FT, 20);
            Series Hi50 = Series.Equal(hi, highest(hi, 50));

            double high1 = double.NaN;
            double high2 = double.NaN;
            bool enable = false;
            for (int ii = 0; ii < count; ii++)
            {
                if (FTup[ii] == 1.0)
                {
                    enable = true;
                }

                if (enable)
                {
                    if (FTup[ii] == 1.0)  // reset peak to current high when FT turns up below 20
                    {
                        high2 = (!bar50 || (Hi50[ii] == 1)) ? hi[ii] : double.NaN;
                    }

                    if (FTdn[ii] == 1.0) // remember peak when FT turns down above 80
                    {
                        high1 = high2;
                    }

                    if (!bar50 || (Hi50[ii] == 1))
                    {
                        if (double.IsNaN(high2) || hi[ii] > high2)  // check if current high is higher than the peak
                        {
                            high2 = hi[ii];
                        }
                    }
                }
                output[ii] = high1;
            }
            return output;
        }

        private static Series calculate802080Low(Series lo, Series FT)
        {
            int count = lo.Count;
            Series output = new Series(count);

            Series FTdn = turnsDnAboveLevel(FT, 80);
            Series FTup = turnsUpBelowLevel(FT, 20);
            Series Lo50 = Series.Equal(lo, lowest(lo, 50));

            double low1 = double.NaN;
            double low2 = double.NaN;
            double low3 = double.NaN;

            bool ok = false;
            bool enb2 = false;
            bool enb3 = false;
            for (int ii = 0; ii < count; ii++)
            {
                if (FTdn[ii] == 1.0)     // starts calculation the first time FT turns down above 80
                {
                    ok = true;
                }

                if (ok)
                {
                    if (FTdn[ii] == 1.0)  // FT turns down above 80
                    {
                        if (enb2)
                        {
                            low1 = low2;
                        }
                        low2 = double.NaN;
                        enb2 = false;
                        enb3 = true;
                    }

                    if (FTup[ii] == 1.0)  // FT turns up below 20
                    {
                        low2 = low3;
                        low3 = double.NaN;
                        enb2 = true;
                        enb3 = false;
                    }

                    if (Lo50[ii] == 1.0)
                    {
                        if (enb2 && double.IsNaN(low2) || lo[ii] < low2)
                        {
                            low2 = lo[ii];
                        }
                        if (enb3 && double.IsNaN(low3) || lo[ii] < low3)
                        {
                            low3 = lo[ii];
                        }
                    }
                }
                output[ii] = low1;
            }
            return output;
        }

        private static Series calculate208020High(Series hi, Series FT)
        {
            int count = hi.Count;
            Series output = new Series(count);

            Series FTdn = turnsDnAboveLevel(FT, 80);
            Series FTup = turnsUpBelowLevel(FT, 20);
            Series Hi50 = Series.Equal(hi, highest(hi, 50));

            double high1 = double.NaN;
            double high2 = double.NaN;
            double high3 = double.NaN;

            bool ok = false;
            bool enb2 = false;
            bool enb3 = false;
            for (int ii = 0; ii < count; ii++)
            {
                if (FTup[ii] == 1.0)     // starts calculation the first time FT turns up below 20
                {
                    ok = true;
                }

                if (ok)
                {
                    if (FTup[ii] == 1.0)  // FT turns up below 20
                    {
                        if (enb2)
                        {
                            high1 = high2;
                        }
                        high2 = double.NaN;
                        enb2 = false;
                        enb3 = true;
                    }

                    if (FTdn[ii] == 1.0)  // FT turns down above 80
                    {
                        high2 = high3;
                        high3 = double.NaN;
                        enb2 = true;
                        enb3 = false;
                    }

                    if (Hi50[ii] == 1.0)  // we have a 50 period high
                    {
                        if (enb2 && double.IsNaN(high2) || hi[ii] > high2)
                        {
                            high2 = hi[ii];
                        }
                        if (enb3 && double.IsNaN(high3) || hi[ii] > high3)
                        {
                            high3 = hi[ii];
                        }
                    }
                }
                output[ii] = high1;
            }
            return output;
        }

        private static Series earlySignalFilter(int period, Series signal, Series filter)
        {
            int count = signal.Count;
            Series output = new Series(count);
            bool enb = false;
            int ago = -1;
            for (int ii = 0; ii < count; ii++)
            {
                if (ii > 0 &
                    !double.IsNaN(filter[ii]) &&
                    !double.IsNaN(filter[ii - 1]) &&
                    filter[ii - 1] == 0 &&
                    filter[ii] != 0)
                {
                    ago = 0;
                    enb = false;
                }
                else if (ago >= 0)
                {
                    if (++ago >= count)
                    {
                        enb = true;
                    }
                }

                output[ii] = enb ? signal[ii] : 0;

            }
            return output;
        }

        private static Series secondarySignalFilter(Series signal, Series filter)
        {
            int count = signal.Count;
            Series output = new Series(count);
            bool enb = false;
            for (int ii = 0; ii < count; ii++)
            {
                if (ii > 0 &
                    !double.IsNaN(filter[ii]) &&
                    !double.IsNaN(filter[ii - 1]) &&
                    filter[ii - 1] == 0 &&
                    filter[ii] != 0)
                {
                    enb = true;
                }
                output[ii] = enb ? signal[ii] : 0;
                if (!double.IsNaN(signal[ii]) && signal[ii] != 0)
                {
                    enb = false;
                }
            }
            return output;
        }

        public static Series frequentSignalFilter(int period, Series signal, Series filter)
        {
            int count = signal.Count;
            Series output = new Series(count);
            bool enb = true;
            int ago = -1;
            for (int ii = 0; ii < count; ii++)
            {
                if (ago >= 0 && ++ago >= period)
                {
                    enb = true;
                }
                output[ii] = enb ? signal[ii] : 0;
                if (!double.IsNaN(filter[ii]) && filter[ii] != 0)
                {
                    ago = 0;
                    enb = false;
                }
            }
            return output;
        }

        private static Series durationSignalFilter(int period, Series signal, Series filter)
        {
            int count = signal.Count;
            Series output = new Series(count);
            int ago = -1;  // the filter leading edge occurred this many bars ago
            for (int ii = 0; ii < count; ii++)
            {
                output[ii] = (ago >= period) ? signal[ii] : 0;
                // leading edge
                if (ii > 0 && !double.IsNaN(filter[ii - 1]) && !double.IsNaN(filter[ii]) &&
                    filter[ii - 1] == 0 && filter[ii] != 0)
                {
                    ago = 0;
                }

                else if (!double.IsNaN(filter[ii]) && filter[ii] != 0)
                {
                    ago++;
                }
            }
            return output;
        }

        private static Series redundantSignalFilter(Series signal, Series filter)
        {
            int count = signal.Count;
            Series output = new Series(count);
            for (int ii = 0; ii < count; ii++)
            {
                output[ii] = (!double.IsNaN(filter[ii]) && filter[ii] != 0) ? 0 : signal[ii];
            }
            return output;
        }

        public static Series ReplaceNaN(Series input, double value)
        {
            int count = input.Count;
            Series output = new Series(count);
            for (int ii = 0; ii < count; ii++)
            {
                output[ii] = double.IsNaN(input[ii]) ? value : input[ii];
            }
            return output;
        }

        public static Series calculateTwoBarPattern(Series op, Series hi, Series lo, Series cl, int suppressionCount = 5)
        {
            Series FT = calculateFT(hi, lo, cl);
            Series FTup = atm.goingUp(FT);
            Series FTdn = atm.goingDn(FT);
            Series op1 = op.ShiftRight(1);
            Series cl1 = cl.ShiftRight(1);
            Series hi1 = hi.ShiftRight(1);
            Series lo1 = lo.ShiftRight(1);
            Series up = FTdn.ShiftRight(1) & FTdn & (op1 < cl1) & (op > op1) & (op < cl) & (cl > cl1);    //  & (cl1 > (lo1 + (hi1 - lo1) * .50)) & (cl > (lo + (hi - lo) * .50))
            Series dn = FTup.ShiftRight(1) & FTup & (op1 > cl1) & (op < op1) & (op > cl) & (cl < cl1);    //  & (cl1 < (lo1 + (hi1 - lo1) * .50)) & (cl > (lo + (hi - lo) * .50));
            Series TBUp = frequentSignalFilter(suppressionCount, up, up);
            Series TBDn = frequentSignalFilter(suppressionCount, dn, dn);
            Series output = TBUp - TBDn;
            return output;
        }

        public static Series calculateEntryXAlerts(Series hi, Series lo, Series close, FirstAlertLevelSelection FALevel, AddonAlertLevelSelection AOALevel)
        {
            Series output = new Series();

            int count = close.Count;

            Series FT = calculateFT(hi, lo, close);

            Series FT_Mid = calculateFT_Mid(hi, lo, close);
            Series FT_LTerm = calculateFT_LTerm(hi, lo, close);
            Series ST = calculateST(hi, lo, close);
            Series EZI = calculateEZI(close);
            Series TSB = calculateTSB(hi, lo, close);

            Dictionary<string, Series> tl = calculateTrend(hi, lo, close, FT, ST, TSB);
            Series TLup = tl["TLup"];
            Series TLdn = tl["TLdn"];

            Series upSet = (TSB > 30).And(ST > 75).And((ST.ShiftRight(6) < 20).Or(ST.ShiftRight(7) < 20));
            Series dnSet = (TSB < 70).And(ST < 25).And((ST.ShiftRight(6) > 80).Or(ST.ShiftRight(7) > 80));
            Series upRes = ST < 65;
            Series dnRes = ST > 35;
            Series Uset = (TSB < 70) * setReset(upSet, upRes);
            Series Dset = (TSB > 30) * setReset(dnSet, dnRes);

            Series Utsb = (EZI >= 80).And(TSB >= 70);
            Series Dtsb = (EZI <= 20).And(TSB <= 30);

            Series Uezi = (EZI <= 80).And(TSB >= 70);
            Series Dezi = (EZI >= 20).And(TSB <= 30);

            Series EZITrendUp = Uezi.Or(Uezi.ShiftRight(1)) * 110;
            Series EZITrendDn = Dezi.Or(Dezi.ShiftRight(1)) * -10;

            Series ftTurnsUp25 = turnsUpBelowLevel(FT, 25);
            Series ftTurnsUp30 = turnsUpBelowLevel(FT, 30);
            Series ftTurnsUp50 = turnsUpBelowLevel(FT, 50);
            Series ftTurnsDn50 = turnsDnAboveLevel(FT, 50);
            Series ftTurnsDn70 = turnsDnAboveLevel(FT, 70);
            Series ftTurnsDn75 = turnsDnAboveLevel(FT, 75);

            List<Series> TargetUp = calculateUpperTargets(hi, lo, TLup);
            Series tgUp1 = ReplaceNaN(TargetUp[0], 0);
            Series tgUp2 = ReplaceNaN(TargetUp[1], 0);

            List<Series> TargetDn = calculateLowerTargets(hi, lo, TLdn);
            Series tgDn1 = ReplaceNaN(TargetDn[0], 0);
            Series tgDn2 = ReplaceNaN(TargetDn[1], 0);

            Series eziTSBStartUp = (EZI.ShiftRight(1) > 0) * (EZI < 20) * EZI.IsRising();
            Series eziTSBTranUpTime = happenedWithin(eziTSBStartUp, 0, 34);
            Series eziTSBStartDn = (EZI.ShiftRight(1) < 100) * (EZI > 80) * EZI.IsFalling();
            Series eziTSBTranDnTime = happenedWithin(eziTSBStartDn, 0, 34);

            List<Series> TL = calculateTrendLines(hi, lo, close, 2.0);
            Series newTrendUp = (hasVal(TL[0]) + (TSB > 70));
            Series newTrendUpTime = happenedWithin(newTrendUp, 0, 21);
            Series newTrendDn = (hasVal(TL[1]) + (TSB < 30));
            Series newTrendDnTime = happenedWithin(newTrendDn, 0, 21);

            Series PXtargetUp1 = hi < tgUp1;
            Series PXtargetUp2 = hi < tgUp2;
            Series PXtargetDn1 = lo > tgDn1;
            Series PXtargetDn2 = lo > tgDn2;

            Series tsbUp = Utsb + Uezi + Uset;
            Series tsbDn = Dtsb + Dezi + Dset;

            Series TSBTranUp = (TSB > 30) * (TSB < 70) * (TSB.ShiftRight(3) < TSB.ShiftRight(2)) * (TSB.ShiftRight(2) < TSB.ShiftRight(1)) * (TSB.ShiftRight(1) < TSB);
            Series TSBTranDn = (TSB > 30) * (TSB < 70) * (TSB.ShiftRight(3) > TSB.ShiftRight(2)) * (TSB.ShiftRight(2) > TSB.ShiftRight(1)) * (TSB.ShiftRight(1) > TSB);

            Series TSBTransitionUp = (TSB >= 30) * (TSB <= 70) * TSB.IsRising();
            Series TSBTransitionDn = (TSB <= 70) * (TSB >= 30) * TSB.IsFalling();

            Series eziTSBTranUpTime13 = happenedWithin(eziTSBStartUp, 0, 13);
            Series eziTSBTranDnTime13 = happenedWithin(eziTSBStartDn, 0, 13);

            Series uf1 = TLup * eziTSBTranUpTime * (ST.ShiftRight(1) >= 40) * ftTurnsUp25 * (Dset <= 0);
            Series df1 = TLdn * eziTSBTranDnTime * (ST.ShiftRight(1) <= 60) * ftTurnsDn75 * (Uset <= 0);
            Series uf2 = TLup * newTrendUpTime * ftTurnsUp30 * (Dset <= 0);
            Series df2 = TLdn * newTrendDnTime * ftTurnsDn70 * (Uset <= 0);

            Series uf3 = (ST > 80).And((ST.ShiftRight(1) - FT.ShiftRight(1)) >= 50).And(turnsUpBelowLevel(FT, 100));
            Series df3 = (ST < 20).And((FT.ShiftRight(1) - ST.ShiftRight(1)) >= 50).And(turnsDnAboveLevel(FT, 0));

            int reqDur = 21;

            Series FA_UL1 = durationSignalFilter(reqDur, secondarySignalFilter(uf1, TLup), TSB < 30);
            Series FA_DL1 = durationSignalFilter(reqDur, secondarySignalFilter(df1, TLdn), TSB > 70);

            Series FA_UL2 = durationSignalFilter(reqDur, secondarySignalFilter(uf2, TLup), TSB < 30);
            Series FA_DL2 = durationSignalFilter(reqDur, secondarySignalFilter(df2, TLdn), TSB > 70);

            Series FA_UL3 = frequentSignalFilter(reqDur, uf3, uf3);
            Series FA_DL3 = frequentSignalFilter(reqDur, df3, df3);

            Series FA_UL4 = tsbUp * ftTurnsUp50 * ((ST - FT) > 15);
            Series FA_DL4 = tsbDn * ftTurnsDn50 * ((FT - ST) > 15);

            Series uf = FA_UL1.Or(FA_UL2).Or(FA_UL3).Or(FA_UL4);
            Series df = FA_DL1.Or(FA_DL2).Or(FA_DL3).Or(FA_DL4);

            Series FAUp = secondarySignalFilter(uf, tsbDn);
            Series FADn = secondarySignalFilter(df, tsbUp);

            if (FALevel == FirstAlertLevelSelection.InsideLevel1)
            {
                FAUp = FAUp.And(PXtargetUp1);
                FADn = FADn.And(PXtargetDn1);
            }
            else if (FALevel == FirstAlertLevelSelection.InsideLevel2)
            {
                FAUp = FAUp.And(PXtargetUp2);
                FADn = FADn.And(PXtargetDn2);
            }

            bool filter = false; // true;

            Series tgUp3 = ReplaceNaN(TargetUp[2], 0);
            Series tgDn3 = ReplaceNaN(TargetDn[2], 0);

            Series PXtargetUp3 = hi < tgUp3;
            Series PXtargetDn3 = lo > tgDn3;

            Series notTsbUp = Series.NotEqual(tsbUp, 1);
            Series notTsbDn = Series.NotEqual(tsbDn, 1);

            Series enbUp1 = (TSB > 30);
            Series enbDn1 = (TSB < 70);

            Series TSBTrendUp = Utsb.Or(Utsb.ShiftRight(1)) * 110;
            Series TSBTrendDn = Dtsb.Or(Dtsb.ShiftRight(1)) * -10;

            Series FTOS = (FT < 30);
            Series FTOB = (FT > 70);
            Series eziEnbUp = setReset(EZI >= 100, EZI <= 0);
            Series eziEnbDn = setReset(EZI <= 0, EZI >= 100);
            Series aoaEnbUp = hasVal(TL[0]) + (notHasVal(TL[0]) * (hasVal(TSBTrendUp) + hasVal(EZITrendUp)));
            Series aoaEnbDn = hasVal(TL[1]) + (notHasVal(TL[1]) * (hasVal(TSBTrendDn) + hasVal(EZITrendDn)));

            Series tl_up = hasVal(TL[0]);
            Series not_tl_up = nothasVal(TL[0]);
            Series tl_dn = hasVal(TL[1]);
            Series not_tl_dn = notHasVal(TL[1]);
            Series eziTransitionUp = (EZI.ShiftRight(1) > 0) * (EZI < 100) * EZI.IsRising();
            Series eziTransitionDn = (EZI.ShiftRight(1) < 100) * (EZI > 0) * EZI.IsFalling();
            Series FiveBarBuy = (FT < 15) * (close.ShiftRight(5) >= close.ShiftRight(8)) * (close.ShiftRight(4) <= close.ShiftRight(7)) * (close.ShiftRight(3) <= close.ShiftRight(6)) * (close.ShiftRight(2) <= close.ShiftRight(5)) * (close.ShiftRight(1) <= close.ShiftRight(4)) * (close <= close.ShiftRight(3));
            Series FiveBarSell = (FT > 85) * (close.ShiftRight(5) <= close.ShiftRight(8)) * (close.ShiftRight(4) >= close.ShiftRight(7)) * (close.ShiftRight(3) >= close.ShiftRight(6)) * (close.ShiftRight(2) >= close.ShiftRight(5)) * (close.ShiftRight(1) >= close.ShiftRight(4)) * (close >= close.ShiftRight(3));

            Series u1 = notTsbDn * hasVal(TL[0]) * TLup * PXtargetUp3 * (ST.ShiftRight(1) > 60) * ftTurnsUp25 * (not_tl_dn) * (Dset <= 0);
            Series d1 = notTsbUp * hasVal(TL[1]) * TLdn * PXtargetDn3 * (ST.ShiftRight(1) < 40) * ftTurnsDn75 * (not_tl_up) * (Uset <= 0);

            Series u2 = notTsbDn * hasVal(TL[0]) * TLup * PXtargetUp3 * ftTurnsUp25 * (not_tl_dn) * (Dset <= 0);
            Series d2 = notTsbUp * hasVal(TL[1]) * TLdn * PXtargetDn3 * ftTurnsDn75 * (not_tl_up) * (Uset <= 0);

            Series u3 = notTsbDn * hasVal(TL[0]) * TLup * ftTurnsUp25 * (not_tl_dn) * (Dset <= 0);
            Series d3 = notTsbUp * hasVal(TL[1]) * TLdn * ftTurnsDn75 * (not_tl_up) * (Uset <= 0);
            Series u4 = tsbUp * ftTurnsUp25 * (Dset <= 0);
            Series d4 = tsbDn * ftTurnsDn75 * (Uset <= 0);

            Series u5 = (ST - FT >= 40) * ftTurnsUp50;
            Series d5 = (FT - ST >= 40) * ftTurnsDn50;

            if (filter)
            {
                u1 = u1 * setReset(FT >= 80, u1.ShiftRight(1));
                d1 = d1 * setReset(FT <= 20, d1.ShiftRight(1));
                u2 = u2 * setReset(FT >= 80, u2.ShiftRight(1));
                d2 = d2 * setReset(FT <= 20, d2.ShiftRight(1));
                u3 = u3 * setReset(FT >= 80, u3.ShiftRight(1));
                d3 = d3 * setReset(FT <= 20, d3.ShiftRight(1));
                u4 = u4 * setReset(FT >= 80, u4.ShiftRight(1));
                d4 = d4 * setReset(FT <= 20, d4.ShiftRight(1));
                u5 = u5 * setReset(FT >= 80, u5.ShiftRight(1));
                d5 = d5 * setReset(FT <= 20, d5.ShiftRight(1));
            }

            Series AOA_UL1 = frequentSignalFilter(5, u1, u1 + u2 + u3 + u4 + u5);
            Series AOA_DL1 = frequentSignalFilter(5, d1, d1 + d2 + d3 + d4 + d5);

            Series AOA_UL2 = frequentSignalFilter(5, u2, u1 + u2 + u3 + u4 + u5);
            Series AOA_DL2 = frequentSignalFilter(5, d2, d1 + d2 + d3 + d4 + d5);

            Series AOA_UL3 = frequentSignalFilter(5, u3, u1 + u2 + u3 + u4 + u5);
            Series AOA_DL3 = frequentSignalFilter(5, d3, d1 + d2 + d3 + d4 + d5);

            Series AOA_UL4 = frequentSignalFilter(5, u4, u1 + u2 + u3 + u4 + u5);
            Series AOA_DL4 = frequentSignalFilter(5, d4, d1 + d2 + d3 + d4 + d5);

            Series AOA_UL5 = frequentSignalFilter(5, u5, u1 + u2 + u3 + u4 + u5);
            Series AOA_DL5 = frequentSignalFilter(5, d5, d1 + d2 + d3 + d4 + d5);

            Series AOAUp = AOA_UL1.Or(AOA_UL2).Or(AOA_UL3).Or(AOA_UL4).Or(AOA_UL5);
            Series AOADn = AOA_DL1.Or(AOA_DL2).Or(AOA_DL3).Or(AOA_DL4).Or(AOA_DL5);

            Series notTLUp = Series.NotEqual(TLup, 1);
            Series notTLDn = Series.NotEqual(TLdn, 1);

            Series not_tl_up2 = nothasVal(TL[0]);
            Series not_tl_dn2 = notHasVal(TL[1]);

            Series ftMID_LT_OS = (FT_Mid <= 25) * (FT_LTerm <= 30);
            Series ftMID_LT_OB = (FT_Mid >= 75) * (FT_LTerm >= 70);

            Series stTurnsUp = turnsUpBelowLevel(ST, 25);
            Series stTurnsDn = turnsDnAboveLevel(ST, 75);

            Series enbUp2 = (FT < 30).And(ST < 30);
            Series enbDn2 = (FT > 70).And(ST > 70);
            Series PBu1 = enbUp2 * tsbUp * TLup * ftMID_LT_OS * (not_tl_dn2) * (FT < 50) * stTurnsUp;
            Series PBd1 = enbDn2 * tsbDn * TLdn * ftMID_LT_OB * (not_tl_up2) * (FT > 50) * stTurnsDn;

            Series PBu2 = enbUp2 * tsbUp * TLup * (FT < 50) * stTurnsUp;
            Series PBd2 = enbDn2 * tsbDn * TLdn * (FT > 50) * stTurnsDn;

            Series PBu3 = enbUp2 * tsbUp * TLup * (FT.ShiftRight(1) <= 30) * (FT < 50) * stTurnsUp;
            Series PBd3 = enbDn2 * tsbDn * TLdn * (FT.ShiftRight(1) >= 70) * (FT > 50) * stTurnsDn;

            Series PBu4 = tsbUp /* TLup * notTLdn */ * (FT.ShiftRight(1) <= 30) * (ST < 20) * ftTurnsUp25;
            Series PBd4 = tsbDn /* TLdn * notTLup */ * (FT.ShiftRight(1) >= 70) * (ST > 80) * ftTurnsDn75;
            PBu1 = PBu1 * setReset(FT >= 70, PBu1.ShiftRight(1));
            PBd1 = PBd1 * setReset(FT <= 30, PBd1.ShiftRight(1));
            PBu2 = PBu2 * setReset(FT >= 70, PBu2.ShiftRight(1));
            PBd2 = PBd2 * setReset(FT <= 30, PBd2.ShiftRight(1));
            PBu3 = PBu3 * setReset(FT >= 70, PBu3.ShiftRight(1));
            PBd3 = PBd3 * setReset(FT <= 30, PBd3.ShiftRight(1));

            Series allUpSignals = PBu1.Or(PBu2.Or(PBu3.Or(PBu4)));
            Series allDnSignals = PBd1.Or(PBd2.Or(PBd3.Or(PBd4)));
            Series PB_UL1 = frequentSignalFilter(8, PBu1, allUpSignals);
            Series PB_DL1 = frequentSignalFilter(8, PBd1, allDnSignals);

            Series PB_UL2 = frequentSignalFilter(8, PBu2, allUpSignals);
            Series PB_DL2 = frequentSignalFilter(8, PBd2, allDnSignals);

            Series PB_UL3 = frequentSignalFilter(8, PBu3, allUpSignals);
            Series PB_DL3 = frequentSignalFilter(8, PBd3, allDnSignals);

            Series PB_UL4 = frequentSignalFilter(8, PBu4, allUpSignals);
            Series PB_DL4 = frequentSignalFilter(8, PBd4, allDnSignals);

            Series PBUp = PB_UL1.Or(PB_UL2).Or(PB_UL3).Or(PB_UL4);
            Series PBDn = PB_DL1.Or(PB_DL2).Or(PB_DL3).Or(PB_DL4);

            Series tsbUpS = (tsbUp >= 1) * (tsbUp.ShiftRight(1) < 1);
            Series tsbUpE = (tsbUp < 1) * (tsbUp.ShiftRight(1) >= 1);
            Series tsbDnS = (tsbDn >= 1) * (tsbDn.ShiftRight(1) < 1);
            Series tsbDnE = (tsbDn < 1) * (tsbDn.ShiftRight(1) >= 1);

            Series X1sig1u = setReset(tsbDnE, tsbUpS);
            Series X1sig2u = turnsUpBelowLevel(FT, 60);
            Series X1sig2ua = TSB > 30;
            Series X1sig3u = (ST - FT > 15).Or((ST < 25).And(FT < 25));
            Series X1sig4u = X1sig1u * X1sig2u * X1sig2ua * X1sig3u;
            Series X1sig5u = setReset(tsbDnE, X1sig4u.ShiftRight(1));
            Series X1sigUp = X1sig4u * X1sig5u;

            Series X1sig1d = setReset(tsbUpE, tsbDnS);
            Series X1sig2d = turnsDnAboveLevel(FT, 40);
            Series X1sig2da = TSB < 70;
            Series X1sig3d = (FT - ST > 15).Or((ST > 75).And(FT > 75));
            Series X1sig4d = X1sig1d * X1sig2d * X1sig2da * X1sig3d;
            Series X1sig5d = setReset(tsbUpE, X1sig4d.ShiftRight(1));
            Series X1sigDn = X1sig4d * X1sig5d;

            X1sigUp = ReplaceNaN(X1sigUp, 0);
            X1sigDn = ReplaceNaN(X1sigDn, 0);

            Series X3sigUp = (TSB < 30).And(ST - FT >= 50).And(turnsUpBelowLevel(FT, 30));
            Series X3sigDn = (TSB > 70).And(FT - ST >= 50).And(turnsDnAboveLevel(FT, 70));

            X3sigUp = ReplaceNaN(X3sigUp, 0);
            X3sigDn = ReplaceNaN(X3sigDn, 0);

            Series up = FAUp.Or(AOAUp).Or(PBUp).Or(X1sigUp).Or(X3sigUp);
            Series dn = FADn.Or(AOADn).Or(PBDn).Or(X1sigDn).Or(X3sigDn);

            output = up - dn;

            return output;
        }

        public static Series calculateFirstAlert(Series hi, Series lo, Series close, FirstAlertLevelSelection FALevel)
        {
            Series output = new Series();

            int count = close.Count;

            Series FT = calculateFT(hi, lo, close);
            Series ST = calculateST(hi, lo, close);
            Series EZI = calculateEZI(close);
            Series TSB = calculateTSB(hi, lo, close);

            Dictionary<string, Series> tl = calculateTrend(hi, lo, close, FT, ST, TSB);
            Series TLup = tl["TLup"];
            Series TLdn = tl["TLdn"];

            Series upSet = (TSB > 30).And(ST > 75).And((ST.ShiftRight(6) < 20).Or(ST.ShiftRight(7) < 20));
            Series dnSet = (TSB < 70).And(ST < 25).And((ST.ShiftRight(6) > 80).Or(ST.ShiftRight(7) > 80));
            Series upRes = ST < 65;
            Series dnRes = ST > 35;
            Series Uset = (TSB < 70) * setReset(upSet, upRes);
            Series Dset = (TSB > 30) * setReset(dnSet, dnRes);

            Series Utsb = (EZI >= 80).And(TSB >= 70);
            Series Dtsb = (EZI <= 20).And(TSB <= 30);

            Series Uezi = (EZI <= 80).And(TSB >= 70);
            Series Dezi = (EZI >= 20).And(TSB <= 30);

            Series EZITrendUp = Uezi.Or(Uezi.ShiftRight(1)) * 110;
            Series EZITrendDn = Dezi.Or(Dezi.ShiftRight(1)) * -10;

            Series ftTurnsUp25 = turnsUpBelowLevel(FT, 25);
            Series ftTurnsUp30 = turnsUpBelowLevel(FT, 30);
            Series ftTurnsUp50 = turnsUpBelowLevel(FT, 50);
            Series ftTurnsDn50 = turnsDnAboveLevel(FT, 50);
            Series ftTurnsDn70 = turnsDnAboveLevel(FT, 70);
            Series ftTurnsDn75 = turnsDnAboveLevel(FT, 75);

            List<Series> TargetUp = calculateUpperTargets(hi, lo, TLup);
            Series tgUp1 = ReplaceNaN(TargetUp[0], 0);
            Series tgUp2 = ReplaceNaN(TargetUp[1], 0);

            List<Series> TargetDn = calculateLowerTargets(hi, lo, TLdn);
            Series tgDn1 = ReplaceNaN(TargetDn[0], 0);
            Series tgDn2 = ReplaceNaN(TargetDn[1], 0);

            Series eziTSBStartUp = (EZI.ShiftRight(1) > 0) * (EZI < 20) * EZI.IsRising();
            Series eziTSBTranUpTime = happenedWithin(eziTSBStartUp, 0, 34);
            Series eziTSBStartDn = (EZI.ShiftRight(1) < 100) * (EZI > 80) * EZI.IsFalling();
            Series eziTSBTranDnTime = happenedWithin(eziTSBStartDn, 0, 34);

            List<Series> TL = calculateTrendLines(hi, lo, close, 2.0);
            Series newTrendUp = (hasVal(TL[0]) + (TSB > 70));
            Series newTrendUpTime = happenedWithin(newTrendUp, 0, 21);
            Series newTrendDn = (hasVal(TL[1]) + (TSB < 30));
            Series newTrendDnTime = happenedWithin(newTrendDn, 0, 21);

            Series PXtargetUp1 = hi < tgUp1;
            Series PXtargetUp2 = hi < tgUp2;
            Series PXtargetDn1 = lo > tgDn1;
            Series PXtargetDn2 = lo > tgDn2;

            Series tsbUp = Utsb + Uezi + Uset;
            Series tsbDn = Dtsb + Dezi + Dset;

            Series TSBTranUp = (TSB > 30) * (TSB < 70) * (TSB.ShiftRight(3) < TSB.ShiftRight(2)) * (TSB.ShiftRight(2) < TSB.ShiftRight(1)) * (TSB.ShiftRight(1) < TSB);
            Series TSBTranDn = (TSB > 30) * (TSB < 70) * (TSB.ShiftRight(3) > TSB.ShiftRight(2)) * (TSB.ShiftRight(2) > TSB.ShiftRight(1)) * (TSB.ShiftRight(1) > TSB);

            Series TSBTransitionUp = (TSB >= 30) * (TSB <= 70) * TSB.IsRising();
            Series TSBTransitionDn = (TSB <= 70) * (TSB >= 30) * TSB.IsFalling();

            Series eziTSBTranUpTime13 = happenedWithin(eziTSBStartUp, 0, 13);
            Series eziTSBTranDnTime13 = happenedWithin(eziTSBStartDn, 0, 13);

            Series uf1 = TLup * eziTSBTranUpTime * (ST.ShiftRight(1) >= 40) * ftTurnsUp25 * (Dset <= 0);
            Series df1 = TLdn * eziTSBTranDnTime * (ST.ShiftRight(1) <= 60) * ftTurnsDn75 * (Uset <= 0);
            Series uf2 = TLup * newTrendUpTime * ftTurnsUp30 * (Dset <= 0);
            Series df2 = TLdn * newTrendDnTime * ftTurnsDn70 * (Uset <= 0);

            Series uf3 = (ST > 80).And((ST.ShiftRight(1) - FT.ShiftRight(1)) >= 50).And(turnsUpBelowLevel(FT, 100));
            Series df3 = (ST < 20).And((FT.ShiftRight(1) - ST.ShiftRight(1)) >= 50).And(turnsDnAboveLevel(FT, 0));

            int reqDur = 21;

            Series FA_UL1 = durationSignalFilter(reqDur, secondarySignalFilter(uf1, TLup), TSB < 30);
            Series FA_DL1 = durationSignalFilter(reqDur, secondarySignalFilter(df1, TLdn), TSB > 70);

            Series FA_UL2 = durationSignalFilter(reqDur, secondarySignalFilter(uf2, TLup), TSB < 30);
            Series FA_DL2 = durationSignalFilter(reqDur, secondarySignalFilter(df2, TLdn), TSB > 70);

            Series FA_UL3 = frequentSignalFilter(reqDur, uf3, uf3);
            Series FA_DL3 = frequentSignalFilter(reqDur, df3, df3);

            Series FA_UL4 = tsbUp * ftTurnsUp50 * ((ST - FT) > 15);
            Series FA_DL4 = tsbDn * ftTurnsDn50 * ((FT - ST) > 15);

            Series uf = FA_UL1.Or(FA_UL2).Or(FA_UL3).Or(FA_UL4);
            Series df = FA_DL1.Or(FA_DL2).Or(FA_DL3).Or(FA_DL4);

            Series FAUp = secondarySignalFilter(uf, tsbDn);
            Series FADn = secondarySignalFilter(df, tsbUp);

            if (FALevel == FirstAlertLevelSelection.InsideLevel1)
            {
                FAUp = FAUp.And(PXtargetUp1);
                FADn = FADn.And(PXtargetDn1);
            }
            else if (FALevel == FirstAlertLevelSelection.InsideLevel2)
            {
                FAUp = FAUp.And(PXtargetUp2);
                FADn = FADn.And(PXtargetDn2);
            }

            output = FAUp - FADn;

            return output;
        }

        public static Series calculateAddOnAlert(Series hi, Series lo, Series close, AddonAlertLevelSelection AOALevel)
        {
            bool filter = false; // true;

            Series output = new Series();

            Series FT = calculateFT(hi, lo, close);
            Series ST = calculateST(hi, lo, close);
            Series EZI = calculateEZI(close);
            Series TSB = calculateTSB(hi, lo, close);

            Dictionary<string, Series> tl = calculateTrend(hi, lo, close, FT, ST, TSB);
            Series TLup = tl["TLup"];
            Series TLdn = tl["TLdn"];

            Series upSet = (TSB > 30).And(ST > 75).And((ST.ShiftRight(6) < 20).Or(ST.ShiftRight(7) < 20));
            Series dnSet = (TSB < 70).And(ST < 25).And((ST.ShiftRight(6) > 80).Or(ST.ShiftRight(7) > 80));
            Series upRes = ST < 65;
            Series dnRes = ST > 35;
            Series Uset = (TSB < 70) * setReset(upSet, upRes);
            Series Dset = (TSB > 30) * setReset(dnSet, dnRes);

            Series Utsb = (EZI >= 80).And(TSB >= 70);
            Series Dtsb = (EZI <= 20).And(TSB <= 30);

            Series Uezi = (EZI <= 80).And(TSB >= 70);
            Series Dezi = (EZI >= 20).And(TSB <= 30);

            Series EZITrendUp = Uezi.Or(Uezi.ShiftRight(1)) * 110;
            Series EZITrendDn = Dezi.Or(Dezi.ShiftRight(1)) * -10;

            Series ftTurnsUp25 = turnsUpBelowLevel(FT, 25);
            Series ftTurnsUp30 = turnsUpBelowLevel(FT, 30);
            Series ftTurnsDn70 = turnsDnAboveLevel(FT, 70);
            Series ftTurnsDn75 = turnsDnAboveLevel(FT, 75);
            Series ftTurnsDn50 = atm.turnsDnAboveLevel(FT, 50);
            Series ftTurnsUp50 = atm.turnsUpBelowLevel(FT, 50);

            List<Series> TargetUp = calculateUpperTargets(hi, lo, TLup);
            Series tgUp1 = ReplaceNaN(TargetUp[0], 0);
            Series tgUp2 = ReplaceNaN(TargetUp[1], 0);
            Series tgUp3 = ReplaceNaN(TargetUp[2], 0);

            List<Series> TargetDn = calculateLowerTargets(hi, lo, TLdn);
            Series tgDn1 = ReplaceNaN(TargetDn[0], 0);
            Series tgDn2 = ReplaceNaN(TargetDn[1], 0);
            Series tgDn3 = ReplaceNaN(TargetDn[2], 0);

            Series eziTSBStartUp = (EZI.ShiftRight(1) > 0) * (EZI < 20) * EZI.IsRising();
            Series eziTSBTranUpTime = happenedWithin(eziTSBStartUp, 0, 34);
            Series eziTSBStartDn = (EZI.ShiftRight(1) < 100) * (EZI > 80) * EZI.IsFalling();
            Series eziTSBTranDnTime = happenedWithin(eziTSBStartDn, 0, 34);

            List<Series> TL = calculateTrendLines(hi, lo, close, 2.0);
            Series newTrendUp = (hasVal(TL[0]) + (TSB > 70));
            Series newTrendUpTime = happenedWithin(newTrendUp, 0, 21);
            Series newTrendDn = (hasVal(TL[1]) + (TSB < 30));
            Series newTrendDnTime = happenedWithin(newTrendDn, 0, 21);

            Series PXtargetUp1 = hi < tgUp1;
            Series PXtargetUp2 = hi < tgUp2;
            Series PXtargetUp3 = hi < tgUp3;
            Series PXtargetDn1 = lo > tgDn1;
            Series PXtargetDn2 = lo > tgDn2;
            Series PXtargetDn3 = lo > tgDn3;

            Series tsbUp = Utsb + Uezi + Uset;
            Series tsbDn = Dtsb + Dezi + Dset;

            Series notTsbUp = Series.NotEqual(tsbUp, 1);
            Series notTsbDn = Series.NotEqual(tsbDn, 1);

            Series TSBTrendUp = Utsb.Or(Utsb.ShiftRight(1)) * 110;
            Series TSBTrendDn = Dtsb.Or(Dtsb.ShiftRight(1)) * -10;

            Series FTOS = (FT < 30);
            Series FTOB = (FT > 70);
            Series eziEnbUp = setReset(EZI >= 100, EZI <= 0);
            Series eziEnbDn = setReset(EZI <= 0, EZI >= 100);
            Series aoaEnbUp = hasVal(TL[0]) + (notHasVal(TL[0]) * (hasVal(TSBTrendUp) + hasVal(EZITrendUp)));
            Series aoaEnbDn = hasVal(TL[1]) + (notHasVal(TL[1]) * (hasVal(TSBTrendDn) + hasVal(EZITrendDn)));

            Series tl_up = hasVal(TL[0]);
            Series not_tl_up = nothasVal(TL[0]);
            Series tl_dn = hasVal(TL[1]);
            Series not_tl_dn = notHasVal(TL[1]);
            Series eziTransitionUp = (EZI.ShiftRight(1) > 0) * (EZI < 100) * EZI.IsRising();
            Series eziTransitionDn = (EZI.ShiftRight(1) < 100) * (EZI > 0) * EZI.IsFalling();
            Series FiveBarBuy = (FT < 15) * (close.ShiftRight(5) >= close.ShiftRight(8)) * (close.ShiftRight(4) <= close.ShiftRight(7)) * (close.ShiftRight(3) <= close.ShiftRight(6)) * (close.ShiftRight(2) <= close.ShiftRight(5)) * (close.ShiftRight(1) <= close.ShiftRight(4)) * (close <= close.ShiftRight(3));
            Series FiveBarSell = (FT > 85) * (close.ShiftRight(5) <= close.ShiftRight(8)) * (close.ShiftRight(4) >= close.ShiftRight(7)) * (close.ShiftRight(3) >= close.ShiftRight(6)) * (close.ShiftRight(2) >= close.ShiftRight(5)) * (close.ShiftRight(1) >= close.ShiftRight(4)) * (close >= close.ShiftRight(3));

            Series u1 = notTsbDn * hasVal(TL[0]) * TLup * PXtargetUp3 * (ST.ShiftRight(1) > 60) * ftTurnsUp25 * (not_tl_dn) * (Dset <= 0);
            Series d1 = notTsbUp * hasVal(TL[1]) * TLdn * PXtargetDn3 * (ST.ShiftRight(1) < 40) * ftTurnsDn75 * (not_tl_up) * (Uset <= 0);

            Series u2 = notTsbDn * hasVal(TL[0]) * TLup * PXtargetUp3 * ftTurnsUp25 * (not_tl_dn) * (Dset <= 0);
            Series d2 = notTsbUp * hasVal(TL[1]) * TLdn * PXtargetDn3 * ftTurnsDn75 * (not_tl_up) * (Uset <= 0);

            Series u3 = notTsbDn * hasVal(TL[0]) * TLup * ftTurnsUp25 * (not_tl_dn) * (Dset <= 0);
            Series d3 = notTsbUp * hasVal(TL[1]) * TLdn * ftTurnsDn75 * (not_tl_up) * (Uset <= 0);
            Series u4 = tsbUp * ftTurnsUp25 * (Dset <= 0);
            Series d4 = tsbDn * ftTurnsDn75 * (Uset <= 0);

            Series u5 = (ST - FT >= 40) * ftTurnsUp50;
            Series d5 = (FT - ST >= 40) * ftTurnsDn50;

            if (filter)
            {
                u1 = u1 * setReset(FT >= 80, u1.ShiftRight(1));
                d1 = d1 * setReset(FT <= 20, d1.ShiftRight(1));
                u2 = u2 * setReset(FT >= 80, u2.ShiftRight(1));
                d2 = d2 * setReset(FT <= 20, d2.ShiftRight(1));
                u3 = u3 * setReset(FT >= 80, u3.ShiftRight(1));
                d3 = d3 * setReset(FT <= 20, d3.ShiftRight(1));
                u4 = u4 * setReset(FT >= 80, u4.ShiftRight(1));
                d4 = d4 * setReset(FT <= 20, d4.ShiftRight(1));
                u5 = u5 * setReset(FT >= 80, u5.ShiftRight(1));
                d5 = d5 * setReset(FT <= 20, d5.ShiftRight(1));
            }

            Series AOA_UL1 = frequentSignalFilter(5, u1, u1 + u2 + u3 + u4 + u5);
            Series AOA_DL1 = frequentSignalFilter(5, d1, d1 + d2 + d3 + d4 + d5);

            Series AOA_UL2 = frequentSignalFilter(5, u2, u1 + u2 + u3 + u4 + u5);
            Series AOA_DL2 = frequentSignalFilter(5, d2, d1 + d2 + d3 + d4 + d5);

            Series AOA_UL3 = frequentSignalFilter(5, u3, u1 + u2 + u3 + u4 + u5);
            Series AOA_DL3 = frequentSignalFilter(5, d3, d1 + d2 + d3 + d4 + d5);

            Series AOA_UL4 = frequentSignalFilter(5, u4, u1 + u2 + u3 + u4 + u5);
            Series AOA_DL4 = frequentSignalFilter(5, d4, d1 + d2 + d3 + d4 + d5);

            Series AOA_UL5 = frequentSignalFilter(5, u5, u1 + u2 + u3 + u4 + u5);
            Series AOA_DL5 = frequentSignalFilter(5, d5, d1 + d2 + d3 + d4 + d5);

            Series AOAUp = AOA_UL1.Or(AOA_UL2).Or(AOA_UL3).Or(AOA_UL4).Or(AOA_UL5);
            Series AOADn = AOA_DL1.Or(AOA_DL2).Or(AOA_DL3).Or(AOA_DL4).Or(AOA_DL5);

            output = AOAUp - AOADn;

            return output;
        }

        public static Series calculatePullbackAlert(Series hi, Series lo, Series close)
        {
            Series output = new Series();

            Series FT = calculateFT(hi, lo, close);
            Series FT_Mid = calculateFT_Mid(hi, lo, close);
            Series FT_LTerm = calculateFT_LTerm(hi, lo, close);
            Series ST = calculateST(hi, lo, close);
            Series EZI = calculateEZI(close);
            Series TSB = calculateTSB(hi, lo, close);

            Dictionary<string, Series> tl = calculateTrend(hi, lo, close, FT, ST, TSB);
            Series TLup = tl["TLup"];
            Series TLdn = tl["TLdn"];

            Series upSet = (TSB > 30).And(ST > 75).And((ST.ShiftRight(6) < 20).Or(ST.ShiftRight(7) < 20));
            Series dnSet = (TSB < 70).And(ST < 25).And((ST.ShiftRight(6) > 80).Or(ST.ShiftRight(7) > 80));
            Series upRes = ST < 65;
            Series dnRes = ST > 35;
            Series Uset = (TSB < 70) * setReset(upSet, upRes);
            Series Dset = (TSB > 30) * setReset(dnSet, dnRes);

            Series Utsb = (EZI >= 80).And(TSB >= 70);
            Series Dtsb = (EZI <= 20).And(TSB <= 30);

            Series notTLUp = Series.NotEqual(TLup, 1);
            Series notTLDn = Series.NotEqual(TLdn, 1);

            Series Uezi = (EZI <= 80).And(TSB >= 70);
            Series Dezi = (EZI >= 20).And(TSB <= 30);

            List<Series> TL = calculateTrendLines(hi, lo, close, 2.0);

            Series tsbUp = Utsb + Uezi + Uset;
            Series tsbDn = Dtsb + Dezi + Dset;

            Series ftTurnsUp25 = atm.turnsUpBelowLevel(FT, 25);
            Series ftTurnsDn75 = atm.turnsDnAboveLevel(FT, 75);
            Series not_tl_up2 = nothasVal(TL[0]);
            Series not_tl_dn2 = notHasVal(TL[1]);

            Series ftMID_LT_OS = (FT_Mid <= 25) * (FT_LTerm <= 30);
            Series ftMID_LT_OB = (FT_Mid >= 75) * (FT_LTerm >= 70);

            Series stTurnsUp = turnsUpBelowLevel(ST, 25);
            Series stTurnsDn = turnsDnAboveLevel(ST, 75);

            Series enbUp = (FT < 30).And(ST < 30);
            Series enbDn = (FT > 70).And(ST > 70);
            Series PBu1 = enbUp * tsbUp * TLup * ftMID_LT_OS * (not_tl_dn2) * (FT < 50) * stTurnsUp;
            Series PBd1 = enbDn * tsbDn * TLdn * ftMID_LT_OB * (not_tl_up2) * (FT > 50) * stTurnsDn;

            Series PBu2 = enbUp * tsbUp * TLup * (FT < 50) * stTurnsUp;
            Series PBd2 = enbDn * tsbDn * TLdn * (FT > 50) * stTurnsDn;

            Series PBu3 = enbUp * tsbUp * TLup * (FT.ShiftRight(1) <= 30) * (FT < 50) * stTurnsUp;
            Series PBd3 = enbDn * tsbDn * TLdn * (FT.ShiftRight(1) >= 70) * (FT > 50) * stTurnsDn;

            Series PBu4 = tsbUp /* TLup * notTLdn */ * (FT.ShiftRight(1) <= 30) * (ST < 20) * ftTurnsUp25;
            Series PBd4 = tsbDn /* TLdn * notTLup */ * (FT.ShiftRight(1) >= 70) * (ST > 80) * ftTurnsDn75;
            PBu1 = PBu1 * setReset(FT >= 70, PBu1.ShiftRight(1));
            PBd1 = PBd1 * setReset(FT <= 30, PBd1.ShiftRight(1));
            PBu2 = PBu2 * setReset(FT >= 70, PBu2.ShiftRight(1));
            PBd2 = PBd2 * setReset(FT <= 30, PBd2.ShiftRight(1));
            PBu3 = PBu3 * setReset(FT >= 70, PBu3.ShiftRight(1));
            PBd3 = PBd3 * setReset(FT <= 30, PBd3.ShiftRight(1));

            Series allUpSignals = PBu1.Or(PBu2.Or(PBu3.Or(PBu4)));
            Series allDnSignals = PBd1.Or(PBd2.Or(PBd3.Or(PBd4)));
            Series PB_UL1 = frequentSignalFilter(8, PBu1, allUpSignals);
            Series PB_DL1 = frequentSignalFilter(8, PBd1, allDnSignals);

            Series PB_UL2 = frequentSignalFilter(8, PBu2, allUpSignals);
            Series PB_DL2 = frequentSignalFilter(8, PBd2, allDnSignals);

            Series PB_UL3 = frequentSignalFilter(8, PBu3, allUpSignals);
            Series PB_DL3 = frequentSignalFilter(8, PBd3, allDnSignals);

            Series PB_UL4 = frequentSignalFilter(8, PBu4, allUpSignals);
            Series PB_DL4 = frequentSignalFilter(8, PBd4, allDnSignals);

            Series PBUp = PB_UL1.Or(PB_UL2).Or(PB_UL3).Or(PB_UL4);
            Series PBDn = PB_DL1.Or(PB_DL2).Or(PB_DL3).Or(PB_DL4);

            output = PBUp - PBDn;

            return output;
        }

        public static Series calculatePExhaustion(Series op, Series hi, Series lo, Series cl)
        {
			Series FT = calculateFT(hi, lo, cl);
			Series ST = calculateST(hi, lo, cl);
            Series prs = calculatePressureAlert(op, hi, lo, cl);
			Series up = FT <= 20 & ST <= 30 & prs > 0;
			Series dn = FT >= 80 & ST >= 70 & prs < 0;
            return up - dn;
		}

		public static Series calculateExhaustion(Series hi, Series lo, Series close, ExhaustionLevelSelection ExLevel, bool setup = false)
        {
            //var t1 = DateTime.Now;
            Series FT = calculateFT(hi, lo, close);
            Series ST = calculateST(hi, lo, close);
            Series EZI = calculateEZI(close);
            Series TSB = calculateTSB(hi, lo, close);
            Dictionary<string, Series> tl = calculateTrend(hi, lo, close, FT, ST, TSB);
            Series TLup = tl["TLup"];
            Series TLdn = tl["TLdn"];
            List<Series> TargetUp = calculateUpperTargets(hi, lo, TLup);
            List<Series> TargetDn = calculateLowerTargets(hi, lo, TLdn);
            //var t2 = DateTime.Now;
            //System.Diagnostics.Debug.WriteLine("exh " + (t2 - t1).Milliseconds);

            Series upSet = (TSB > 30).And(ST > 75).And((ST.ShiftRight(6) < 20).Or(ST.ShiftRight(7) < 20));
            Series dnSet = (TSB < 70).And(ST < 25).And((ST.ShiftRight(6) > 80).Or(ST.ShiftRight(7) > 80));
            Series upRes = ST < 65;
            Series dnRes = ST > 35;
            Series Uset = (TSB < 70) * setReset(upSet, upRes);
            Series Dset = (TSB > 30) * setReset(dnSet, dnRes);

            Series Utsb = (EZI >= 80).And(TSB >= 70);
            Series Dtsb = (EZI <= 20).And(TSB <= 30);

            Series Uezi = (EZI <= 80).And(TSB >= 70);
            Series Dezi = (EZI >= 20).And(TSB <= 30);

            Series tgUp4 = ReplaceNaN(TargetUp[3], 0);
            Series tgDn4 = ReplaceNaN(TargetDn[3], 0);

            Series eziTSBStartUp = (EZI.ShiftRight(1) > 0) * (EZI < 20) * EZI.IsRising();
            Series eziTSBTranUpTime = happenedWithin(eziTSBStartUp, 0, 34);
            Series eziTSBStartDn = (EZI.ShiftRight(1) < 100) * (EZI > 80) * EZI.IsFalling();
            Series eziTSBTranDnTime = happenedWithin(eziTSBStartDn, 0, 34);

            Dictionary<string, Series> max = maximum(hi, 50);
            Dictionary<string, Series> min = minimum(lo, 50);
            Dictionary<string, Series> sig = hook(FT);

            int count = close.Count;
            Series ftUp = new Series(count);
            Series ftDn = new Series(count);
            bool upEnb = false;
            bool dnEnb = false;
            for (int ii = 0; ii < count; ii++)
            {
                if (min["sig"][ii] != 0) upEnb = true;
                if (max["sig"][ii] != 0) dnEnb = true;
                bool upSig = upEnb && sig["Usig"][ii] != 0 && sig["Dago"][ii] < min["ago"][ii];
                bool dnSig = dnEnb && sig["Dsig"][ii] != 0 && sig["Uago"][ii] < max["ago"][ii];
                if (upSig & TLup[ii] != 0 && eziTSBTranUpTime[ii] != 0) upEnb = false;
                if (dnSig & TLdn[ii] != 0 && eziTSBTranDnTime[ii] != 0) dnEnb = false;
                ftUp[ii] = upSig ? 1 : 0;
                ftDn[ii] = dnSig ? 1 : 0;
            }

            Series PXGreaterThantargetUp4 = hi > tgUp4;
            Series PXLessThantargetDn4 = lo < tgDn4;

            Series tsbUp = Utsb + Uezi + Uset;
            Series tsbDn = Dtsb + Dezi + Dset;

            Series ftTurnsUp2 = setup ? new Series(FT.Count, 1.0) : turnsUpBelowLevel(FT, 20);
            Series ftTurnsDn2 = setup ? new Series(FT.Count, 1.0) : turnsDnAboveLevel(FT, 80);

            Series ul1 = tsbDn * TLdn * PXLessThantargetDn4 * (FT.ShiftRight(1) <= 30) * (ST.ShiftRight(1) <= 35) * ftTurnsUp2 * (Dset <= 0);
            Series dl1 = tsbUp * TLup * PXGreaterThantargetUp4 * (FT.ShiftRight(1) >= 70) * (ST.ShiftRight(1) >= 65) * ftTurnsDn2 * (Uset <= 0);
            Series ul2 = tsbDn * TLdn * PXLessThantargetDn4 * ftUp * (Dset <= 0);
            Series dl2 = tsbUp * TLup * PXGreaterThantargetUp4 * ftDn * (Uset <= 0);
            Series ul3 = tsbDn * (FT.ShiftRight(1) <= 30) * (ST.ShiftRight(1) <= 35) * ftTurnsUp2 * (Dset <= 0);
            Series dl3 = tsbUp * (FT.ShiftRight(1) >= 70) * (ST.ShiftRight(1) >= 65) * ftTurnsDn2 * (Uset <= 0);

            ul1 = ul1 * setReset(FT >= 70, ul1.ShiftRight(1));
            dl1 = dl1 * setReset(FT <= 30, dl1.ShiftRight(1));
            ul2 = ul2 * setReset(FT >= 70, ul2.ShiftRight(1));
            dl2 = dl2 * setReset(FT <= 30, dl2.ShiftRight(1));
            ul3 = ul3 * setReset(FT >= 70, ul3.ShiftRight(1));
            dl3 = dl3 * setReset(FT <= 30, dl3.ShiftRight(1));

            Series EX_UL1 = frequentSignalFilter(5, ul1, ul1);
            Series EX_DL1 = frequentSignalFilter(5, dl1, dl1);

            Series EX_UL2 = frequentSignalFilter(5, ul2, ul2);
            Series EX_DL2 = frequentSignalFilter(5, dl2, dl2);

            Series EX_UL3 = frequentSignalFilter(5, ul3, ul3);
            Series EX_DL3 = frequentSignalFilter(5, dl3, dl3);

            Series ExUp = EX_UL1.Or(EX_UL2).Or(EX_UL3);
            Series ExDn = EX_DL1.Or(EX_DL2).Or(EX_DL3);

            Series output = ExUp - ExDn;

            return output;
        }
        public static Series calculateXAlert(Series hi, Series lo, Series close)
        {
            return calculateXAlertPrivate(hi, lo, close);
        }

        private static Series calculateXAlertPrivate(Series hi, Series lo, Series close)
        {
            Series output = new Series();

            Series FT = calculateFT(hi, lo, close);
            Series ST = calculateST(hi, lo, close);
            Series EZI = calculateEZI(close);
            Series TSB = calculateTSB(hi, lo, close);

            Series upSet = (TSB > 30).And(ST > 75).And((ST.ShiftRight(6) < 20).Or(ST.ShiftRight(7) < 20));
            Series dnSet = (TSB < 70).And(ST < 25).And((ST.ShiftRight(6) > 80).Or(ST.ShiftRight(7) > 80));
            Series upRes = ST < 65;
            Series dnRes = ST > 35;
            Series Uset = (TSB < 70) * setReset(upSet, upRes);
            Series Dset = (TSB > 30) * setReset(dnSet, dnRes);
            Series Utsb = (EZI >= 80).And(TSB >= 70);
            Series Dtsb = (EZI <= 20).And(TSB <= 30);
            Series Uezi = (EZI <= 80).And(TSB >= 70);
            Series Dezi = (EZI >= 20).And(TSB <= 30);

            Series tsbUp = Utsb + Uezi + Uset;
            Series tsbDn = Dtsb + Dezi + Dset;

            Series tsbUpS = (tsbUp >= 1) * (tsbUp.ShiftRight(1) < 1);
            Series tsbUpE = (tsbUp < 1) * (tsbUp.ShiftRight(1) >= 1);
            Series tsbDnS = (tsbDn >= 1) * (tsbDn.ShiftRight(1) < 1);
            Series tsbDnE = (tsbDn < 1) * (tsbDn.ShiftRight(1) >= 1);

            Series X1sig1u = setReset(tsbDnE, tsbUpS);
            Series X1sig2u = turnsUpBelowLevel(FT, 60);
            Series X1sig2ua = TSB > 30;
            Series X1sig3u = (ST - FT > 15).Or((ST < 25).And(FT < 25));
            Series X1sig4u = X1sig1u * X1sig2u * X1sig2ua * X1sig3u;
            Series X1sig5u = setReset(tsbDnE, X1sig4u.ShiftRight(1));
            Series X1sigUp = X1sig4u * X1sig5u;

            Series X1sig1d = setReset(tsbUpE, tsbDnS);
            Series X1sig2d = turnsDnAboveLevel(FT, 40);
            Series X1sig2da = TSB < 70;
            Series X1sig3d = (FT - ST > 15).Or((ST > 75).And(FT > 75));
            Series X1sig4d = X1sig1d * X1sig2d * X1sig2da * X1sig3d;
            Series X1sig5d = setReset(tsbUpE, X1sig4d.ShiftRight(1));
            Series X1sigDn = X1sig4d * X1sig5d;

            X1sigUp = ReplaceNaN(X1sigUp, 0);
            X1sigDn = ReplaceNaN(X1sigDn, 0);

            Series X1sig = X1sigUp - X1sigDn;

            Series X3sigUp = (TSB < 30).And(ST - FT >= 50).And(turnsUpBelowLevel(FT, 30));
            Series X3sigDn = (TSB > 70).And(FT - ST >= 50).And(turnsDnAboveLevel(FT, 70));

            X3sigUp = ReplaceNaN(X3sigUp, 0);
            X3sigDn = ReplaceNaN(X3sigDn, 0);

            Series X3sig = X3sigUp - X3sigDn;

            output = X1sig + X3sig;

            return output;
        }

        public static Series calculateZAlert(Series high, Series low, Series close)
        {
            Series output = new Series();

            Series FT = atm.calculateFT(high, low, close);
            Series ST = atm.calculateST(high, low, close);
            Series EZI = atm.calculateEZI(close);
            Series TSB = atm.calculateTSB(high, low, close);

            Series upSet = (TSB > 30).And(ST > 75).And((ST.ShiftRight(6) < 20).Or(ST.ShiftRight(7) < 20));
            Series dnSet = (TSB < 70).And(ST < 25).And((ST.ShiftRight(6) > 80).Or(ST.ShiftRight(7) > 80));
            Series upRes = ST < 65;
            Series dnRes = ST > 35;
            Series Uset = (TSB < 70) * atm.setReset(upSet, upRes);
            Series Dset = (TSB > 30) * atm.setReset(dnSet, dnRes);
            Series Utsb = (EZI >= 80).And(TSB >= 70);
            Series Dtsb = (EZI <= 20).And(TSB <= 30);
            Series Uezi = (EZI <= 80).And(TSB >= 70);
            Series Dezi = (EZI >= 20).And(TSB <= 30);

            Series tsbUp = Utsb + Uezi + Uset;
            Series tsbDn = Dtsb + Dezi + Dset;

            Series lPk2 = calculate2080High(high, FT, true);
            Series lTr2 = calculate8020Low(low, FT, true);
            Series rPk2 = calculate80High(high, FT);
            Series rTr2 = calculate20Low(low, FT);

            Series X3STFTupdiff = ST - FT < 40;
            Series X3STFTdndiff = FT - ST < 40;

            Series X3sigUp = (lTr2 > rTr2) * turnsUpAboveLevel(FT, 20) * X3STFTdndiff * tsbDn;
            Series X3sigDn = (lPk2 < rPk2) * turnsDnBelowLevel(FT, 80) * X3STFTupdiff * tsbUp;

            X3sigUp = ReplaceNaN(X3sigUp, 0);
            X3sigDn = ReplaceNaN(X3sigDn, 0);

            output = X3sigUp - X3sigDn;

            return output;
        }

        public static List<Series> getFTTurningPoints(Series hi, Series lo, Series cl)
        {
            int count = cl.Count + 1;
            Series h = new Series(count);
            Series l = new Series(count);
            Series c = new Series(count);

            bool extend = true;
            for (var ii = count - 1; ii >= 0; ii--)
            {
                h[ii] = hi[ii];
                l[ii] = lo[ii];
                c[ii] = cl[ii];

                if (extend && ii > 0)
                {
                    if (!double.IsNaN(cl[ii - 1]) && double.IsNaN(cl[ii - 1]))
                    {
                        h[ii] = cl[ii - 1];
                        l[ii] = cl[ii - 1];
                        c[ii] = cl[ii - 1];
                        extend = false;
                    }
                }
            }

            Series FT = calculateFT(h, l, c);
            List<Series> fttp = atm.calculateFastTurningPoints(h, l, c, FT);
            return fttp;
        }

        public static Series calculateP5Sig(Series SCORE, Series op, Series hi, Series lo, Series cl)
        {
            Series P5Sig = new Series(cl.Count, 0);

            Series FT = calculateFT(hi, lo, cl);
            Series ST = calculateST(hi, lo, cl);

            List<Series> fttp = getFTTurningPoints(hi, lo, cl);

            Series stgreaterft20 = (ST - FT) > 20;

            Series stlessft20 = (FT - ST) > 20;

            Series u1 = (FT < 25).And(cl > (lo + (hi - lo) * 0.618));
            Series d1 = (FT > 75).And(cl < (lo + (hi - lo) * 0.382));

            Series u5 = (FT - FT.ShiftRight(1) > 5) & u1;
            Series d5 = (FT - FT.ShiftRight(1) < 5) & d1;

            int count1 = SCORE.Count;

            bool pAlertSigUp = false;
            bool pAlertSigDn = false;

            for (int ii = 0; ii < count1; ii++)
            {
                bool pAlertBuy = (u5[ii] == 1) && SCORE[ii] > 50;
                bool pAlertSell = (d5[ii] == 1) && SCORE[ii] < 50;

                double ftTurningPointPriceUp = fttp[0][ii];
                double ftTurningPointPriceDn = fttp[1][ii];
                bool ftAboutToTurnUp1 = (!double.IsNaN(ftTurningPointPriceUp)) ? 100 * (ftTurningPointPriceUp - cl[ii]) / cl[ii] < 1.00 : false;
                bool ftAboutToTurnDn1 = (!double.IsNaN(ftTurningPointPriceDn)) ? 100 * (cl[ii] - ftTurningPointPriceDn) / cl[ii] < 1.00 : false;

                ftTurningPointPriceUp = fttp[0][ii + 1];
                ftTurningPointPriceDn = fttp[1][ii + 1];
                bool ftAboutToTurnUp2 = (!double.IsNaN(ftTurningPointPriceUp)) ? 100 * (ftTurningPointPriceUp - cl[ii]) / cl[ii] < 1.00 : false;
                bool ftAboutToTurnDn2 = (!double.IsNaN(ftTurningPointPriceDn)) ? 100 * (cl[ii] - ftTurningPointPriceDn) / cl[ii] < 1.00 : false;

                bool aboutToTurnUp = ftAboutToTurnUp1 || ftAboutToTurnUp2; 
                bool aboutToTurnDn = ftAboutToTurnDn1 || ftAboutToTurnDn2; 

                pAlertSigUp = pAlertBuy && aboutToTurnUp;
                pAlertSigDn = pAlertSell && aboutToTurnDn;

                if (pAlertBuy) P5Sig[ii] = 1.0;
                else if (pAlertSigDn) P5Sig[ii] = -1.0;
            }

            return P5Sig;
        }

        public static Series calculatePressureAlertNoFilter(Series SCORE, Series op, Series hi, Series lo, Series cl)
        {
            Series PSig = new Series(cl.Count, 0);

            Series FT = calculateFT(hi, lo, cl);
            Series ST = calculateST(hi, lo, cl);

            List<Series> fttp = getFTTurningPoints(hi, lo, cl);

            Series stgreaterft20 = (ST - FT) > 20;

            Series stlessft20 = (FT - ST) > 20;

            Series u1 = (FT < 25).And(cl > (lo + (hi - lo) * 0.618));
            Series d1 = (FT > 75).And(cl < (lo + (hi - lo) * 0.382));

            int count1 = SCORE.Count;

            bool pAlertSigUp = false;
            bool pAlertSigDn = false;

            for (int ii = 0; ii < count1; ii++)
            {
                bool pAlertBuy = (u1[ii] == 1) && SCORE[ii] > 50;
                bool pAlertSell = (d1[ii] == 1) && SCORE[ii] < 50;

                double ftTurningPointPriceUp = fttp[0][ii];
                double ftTurningPointPriceDn = fttp[1][ii];
                bool ftAboutToTurnUp1 = (!double.IsNaN(ftTurningPointPriceUp)) ? 100 * (ftTurningPointPriceUp - cl[ii]) / cl[ii] < 1.00 : false;
                bool ftAboutToTurnDn1 = (!double.IsNaN(ftTurningPointPriceDn)) ? 100 * (cl[ii] - ftTurningPointPriceDn) / cl[ii] < 1.00 : false;

                ftTurningPointPriceUp = fttp[0][ii + 1];
                ftTurningPointPriceDn = fttp[1][ii + 1];
                bool ftAboutToTurnUp2 = (!double.IsNaN(ftTurningPointPriceUp)) ? 100 * (ftTurningPointPriceUp - cl[ii]) / cl[ii] < 1.00 : false;
                bool ftAboutToTurnDn2 = (!double.IsNaN(ftTurningPointPriceDn)) ? 100 * (cl[ii] - ftTurningPointPriceDn) / cl[ii] < 1.00 : false;

                bool aboutToTurnUp = ftAboutToTurnUp1 || ftAboutToTurnUp2;  //ftAboutToTurnUp || stAboutToTurnUp;
                bool aboutToTurnDn = ftAboutToTurnDn1 || ftAboutToTurnDn2;  //ftAboutToTurnDn || stAboutToTurnDn;

                pAlertSigUp = pAlertBuy && aboutToTurnUp;
                pAlertSigDn = pAlertSell && aboutToTurnDn;

                if (pAlertSigUp) PSig[ii] = 1.0;
                else if (pAlertSigDn) PSig[ii] = -1.0;
            }

            return PSig;
        }

		public static Series calculatePressureAlert(Series op, Series hi, Series lo, Series cl)
		{
			Series output = new Series();

			Series FT = calculateFT(hi, lo, cl);

			Series u1 = (FT < 25).And(cl > (lo + (hi - lo) * 0.618));
			Series d1 = (FT > 75).And(cl < (lo + (hi - lo) * 0.382));

			Series PrsUp = frequentSignalFilter(5, u1, u1);
			Series PrsDn = frequentSignalFilter(5, d1, d1);

			output = PrsUp - PrsDn;

			return output;
		}
		public static Series calculatePressureFilter(Series op, Series hi, Series lo, Series cl)
		{
			Series output = new Series();

			Series FT = calculateFT(hi, lo, cl);
			var r1 = Enumerable.Range(0, FT.Count);

			Series u1b = (FT < 25).And(cl > (lo + (hi - lo) * 0.618));
			Series u1e = FT > 75;
			var s1 = 0.0;
			var up = new Series(r1.Select(i => { s1 = u1b[i] == 1 ? 1.0 : u1e[i] == 1 ? 0.0 : s1; return s1; }).ToList());

			Series d1b = (FT > 75).And(cl < (lo + (hi - lo) * 0.382));
			Series d1e = FT < 25;
			var s2 = 0.0;
			var dn = new Series(r1.Select(i => { s2 = d1b[i] == 1 ? 1.0 : d1e[i] == 1 ? 0.0 : s2; return s2; }).ToList());

			output = up - dn;
			return output;
		}

		public static Series calculatePTAlert(Series op, Series hi, Series lo, Series cl, Series score = null, Series rp = null)
        {
            Series PTSig = new Series();

            var SCsig = (score != null && rp != null) ? atm.calculateSCSig(score, rp, 2) : null;

            Series TSBUp = calculateTSBUp(hi, lo, cl);
            Series TSBDn = calculateTSBDn(hi, lo, cl);

            Series pAlert = atm.calculatePressureAlert(op, hi, lo, cl);

            bool ptAlertBuy = false;
            bool ptAlertSell = false;

            for (int ii = 0; ii < pAlert.Count; ii++)
            {
               ptAlertBuy  = (pAlert[ii] ==  1) && TSBUp[ii] == 1 && (SCsig == null || SCsig[ii] > 0);
               ptAlertSell = (pAlert[ii] == -1) && TSBDn[ii] == 1 && (SCsig == null || SCsig[ii] < 0);

                if (ptAlertBuy) PTSig.Append(1.0);
                else if (ptAlertSell) PTSig.Append(-1.0);
                else PTSig.Append(0.0);
            }
            
            return PTSig;
        }

        public static Series calculate2BTAlert(Series op, Series hi, Series lo, Series cl)
        {
            Series TWOBTSig = new Series();

            Series TSBUp = calculateTSBUp(hi, lo, cl);
            Series TSBDn = calculateTSBDn(hi, lo, cl);

            Series twoBAlert = atm.calculateTwoBarPattern(op, hi, lo, cl);

            bool t2bAlertBuy = false;
            bool t2bAlertSell = false;

            for (int ii = 0; ii < twoBAlert.Count; ii++)
            {
                t2bAlertBuy = (twoBAlert[ii] == 1) && TSBUp[ii] == 1;
                t2bAlertSell = (twoBAlert[ii] == -1) && TSBDn[ii] == 1;

                if (t2bAlertBuy) TWOBTSig.Append(1.0);
                else if (t2bAlertSell) TWOBTSig.Append(-1.0);
                else TWOBTSig.Append(0.0);
            }

            return TWOBTSig;
        }

        public static Series calculateScoreAlert(Series score)
        {
            Series output = new Series();

            Series score1 = score.ShiftRight(1);
            Series score2 = score.ShiftRight(2);

            Series u1 = score - score1 > 25 | score - score2 > 25;
            Series d1 = score - score1 < -25 | score - score2 < -25;

            output = u1 - d1;

            return output;
        }

        public static Series calculateNetSig(Series op, Series hi, Series lo, Series cl, Series pr, Series rp, Series score, int type)
        {
            Series stSig = calculateSTSig(op, hi, lo, cl, type);
            Series scSig = calculateSCSig(score, rp, type);
            Series prSig = calculatePRSig(pr, type);
            Series output = stSig + scSig + prSig;
            return output;
        }

        public static Series calculatePRSig(Series PR, int type)
        {
            int count = PR.Count;

            Series enterLong = Series.Equal(PR, 1.5);
            Series enterShort = Series.Equal(PR, -1.5);
            Series exitLong = Series.NotEqual(PR, 1.5);
            Series exitShort = Series.NotEqual(PR, -1.5);

            if (type < 2)
            {
                int direction1 = 0;
                int direction2 = 0;
                bool enable = true;
                for (int ii = count - 1; ii >= 0; ii--)
                {

                    if (enterLong[ii] == 1) direction2 = 1;
                    else if (enterShort[ii] == 1) direction2 = -1;
                    else direction2 = 0;

                    if (direction1 != 0 && direction1 != direction2)
                    {
                        enable = false;
                    }

                    if (!enable)
                    {
                        enterLong[ii] = 0;
                        enterShort[ii] = 0;
                    }

                    direction1 = direction2;
                }
            }

            Series sigUp1 = atm.setReset(enterLong, exitLong).ReplaceNaN(0);
            Series sigDn1 = atm.setReset(enterShort, exitShort).ReplaceNaN(0);

            Series sigUp2 = (type == 0) ? sigUp1.And(not(sigUp1.ShiftRight(1))) : sigUp1;
            Series sigDn2 = (type == 0) ? sigDn1.And(not(sigDn1.ShiftRight(1))) : sigDn1;

            Series output = sigUp2 - sigDn2;
            return output;
        }

        public static Series calculateSCSig(Series SCORE, Series RelPx, int type)
        {
            Series output = new Series();
            if (SCORE != null)
            {
                int count = SCORE.Count;

                Series enterLong = new Series(count, 0);
                Series enterShort = new Series(count, 0);

                for (int ii = 0; ii < count; ii++)
                {
                    bool s50to100 = SCORE[ii] >= 50 && SCORE[ii] <= 100;
                    bool s0to50 = SCORE[ii] > 0 && SCORE[ii] < 50;

                    bool s55to100 = SCORE[ii] > 55 && SCORE[ii] <= 100;
                    bool s75to100 = SCORE[ii] > 75 && SCORE[ii] <= 100;
                    bool s65to75 = SCORE[ii] > 65 && SCORE[ii] <= 75;
                    bool s55to65 = SCORE[ii] > 55 && SCORE[ii] <= 65;
                    bool s54to55 = SCORE[ii] > 54 && SCORE[ii] <= 55;
                    bool s53to54 = SCORE[ii] > 53 && SCORE[ii] <= 54;
                    bool s52to53 = SCORE[ii] > 52 && SCORE[ii] <= 53;
                    bool s51to52 = SCORE[ii] > 51 && SCORE[ii] <= 52;
                    bool s50to51 = SCORE[ii] > 50 && SCORE[ii] <= 51;
                    bool s49to50 = SCORE[ii] > 49 && SCORE[ii] <= 50;
                    bool s48to49 = SCORE[ii] > 48 && SCORE[ii] <= 49;
                    bool s47to48 = SCORE[ii] > 47 && SCORE[ii] <= 48;
                    bool s46to47 = SCORE[ii] > 46 && SCORE[ii] <= 47;
                    bool s45to46 = SCORE[ii] > 45 && SCORE[ii] <= 46;
                    bool s35to45 = SCORE[ii] > 35 && SCORE[ii] <= 45;
                    bool s25to35 = SCORE[ii] > 25 && SCORE[ii] <= 35;
                    bool s0to25 = SCORE[ii] > 0 && SCORE[ii] <= 25;
                    bool s0to45 = SCORE[ii] > 0 && SCORE[ii] <= 45;

                    bool scoreUp = s50to100;
                    bool scoreDn = s0to50;
                    if (RelPx != null)
                    {
                        scoreUp = s55to100 || (s54to55 && RelPx[ii] >= .5) || (s53to54 && RelPx[ii] >= .75) || (s52to53 && RelPx[ii] >= 1) || (s51to52 && RelPx[ii] >= 1.25) || (s50to51 && RelPx[ii] >= 1.5);
                        scoreDn = s0to45 || (s45to46 && RelPx[ii] <= -.5) || (s46to47 && RelPx[ii] <= -.75) || (s47to48 && RelPx[ii] <= -1) || (s48to49 && RelPx[ii] <= -1.25) || (s49to50 && RelPx[ii] <= -1.5);
                    }

                    if (scoreUp) enterLong[ii] = 1;
                    if (scoreDn) enterShort[ii] = 1;
                }

                if (type < 2)
                {
                    int direction1 = 0;
                    int direction2 = 0;
                    bool enable = true;
                    for (int ii = count - 1; ii >= 0; ii--)
                    {

                        if (enterLong[ii] == 1) direction2 = 1;
                        else if (enterShort[ii] == 1) direction2 = -1;
                        else direction2 = 0;

                        if (direction1 != 0 && direction1 != direction2)
                        {
                            enable = false;
                        }

                        if (!enable)
                        {
                            enterLong[ii] = 0;
                            enterShort[ii] = 0;
                        }

                        direction1 = direction2;
                    }
                }

                Series sigUp1 = atm.setReset(enterLong, enterShort).ReplaceNaN(0);
                Series sigDn1 = atm.setReset(enterShort, enterLong).ReplaceNaN(0);

                // condition = _________|-------------|________
                // event =     _________|______________________  type = 0 or 3

                Series sigUp2 = (type == 0 || type == 3) ? sigUp1.And(not(sigUp1.ShiftRight(1))) : sigUp1;
                Series sigDn2 = (type == 0 || type == 3) ? sigDn1.And(not(sigDn1.ShiftRight(1))) : sigDn1;

                output = sigUp2 - sigDn2;
            }
            return output;
        }

        public static Series calculateFTSig(Series op, Series hi, Series lo, Series cl, int type) // type: 0 = first, 1 = current, 2 = history
        {
            Series FTSig = new Series();

            Series FT = atm.calculateFT(hi, lo, cl);

            Series FtGoingUp = atm.goingUp(FT);
            Series FtGoingDn = atm.goingDn(FT);

            Series FtTurnsUp = atm.turnsUpAboveLevel(FT, 0);
            Series FtTurnsDn = atm.turnsDnBelowLevel(FT, 100);

            Series enterLong = (FtGoingUp);
            Series enterShort = (FtGoingDn);

            int count = FTSig.Count;
            if (type < 2)
            {
                int direction1 = 0;
                int direction2 = 0;
                bool enable = true;
                for (int ii = count - 1; ii >= 0; ii--)
                {

                    if (enterLong[ii] == 1) direction2 = 1;
                    else if (enterShort[ii] == 1) direction2 = -1;
                    else direction2 = 0;

                    if (direction1 != 0 && direction1 != direction2)
                    {
                        enable = false;
                    }

                    if (!enable)
                    {
                        enterLong[ii] = 0;
                        enterShort[ii] = 0;
                    }

                    direction1 = direction2;
                }
            }

            Series sigUp1 = atm.setReset(enterLong, enterShort).ReplaceNaN(0);
            Series sigDn1 = atm.setReset(enterShort, enterLong).ReplaceNaN(0);

            Series sigUp2 = (type == 0) ? sigUp1.And(not(sigUp1.ShiftRight(1))) : sigUp1;
            Series sigDn2 = (type == 0) ? sigDn1.And(not(sigDn1.ShiftRight(1))) : sigDn1;

            Series output = sigUp2 - sigDn2;
            return output;
        }

        public static Series calculateTSBSig(Series op, Series hi, Series lo, Series cl, int type) // type: 0 = first, 1 = current, 2 = history
        {
            Series TSBSig = new Series();

            Series ST = atm.calculateST(hi, lo, cl);
            Series EZI = atm.calculateEZI(cl);
            Series TSB = atm.calculateTSB(hi, lo, cl);

            Series upSet = (TSB > 30).And(ST > 75).And((ST.ShiftRight(6) < 20).Or(ST.ShiftRight(7) < 20));
            Series dnSet = (TSB < 70).And(ST < 25).And((ST.ShiftRight(6) > 80).Or(ST.ShiftRight(7) > 80));
            Series upRes = ST < 65;
            Series dnRes = ST > 35;

            Series Utsb = (EZI >= 80).And(TSB >= 70);
            Series Dtsb = (EZI <= 20).And(TSB <= 30);
            Series Uezi = (EZI <= 80).And(TSB >= 70);
            Series Dezi = (EZI >= 20).And(TSB <= 30);
            Series Uset = (TSB < 70) * atm.setReset(upSet, upRes);
            Series Dset = (TSB > 30) * atm.setReset(dnSet, dnRes);

            Series Pressure = TSB.ClipAbove(70).ClipBelow(30);

            // TSB is Bearish
            Series tsbDn = Dezi.Or(Dtsb).Or(Dset);

            // TSB is NOT Bearish  exit shorts   buy long
            Series notBearishTSB = Series.NotEqual(tsbDn, 1);

            // TSB is Bullish  
            Series tsbUp = Uezi.Or(Utsb).Or(Uset);

            // TSB is NOT Bullish  exut long sell short
            Series notBullTSB = Series.NotEqual(tsbUp, 1);

            // Pressure Going Dn
            Series PressureDn1 = atm.goingDn(Pressure);

            // Pressure Going Up
            Series PressureUp1 = atm.goingUp(Pressure);

            Series endTSBUp = Series.Maximum(Series.Equal(tsbDn - (tsbDn >> 1), -1), 8);
            Series endTSBDn = Series.Maximum(Series.Equal(tsbUp - (tsbUp >> 1), -1), 8);

            // BullishTSBEnds
            Series endBullishTSB = endTSBDn;

            // BearishTSBEnds
            Series endBearishTSB = endTSBUp;

            Series enterLong = ((endBullishTSB).And(PressureUp1)).Or(tsbUp);
            Series enterShort = ((endBearishTSB).And(PressureDn1)).Or(tsbDn);

            int count = TSB.Count;
            if (type < 2)
            {
                int direction1 = 0;
                int direction2 = 0;
                bool enable = true;
                for (int ii = count - 1; ii >= 0; ii--)
                {

                    if (enterLong[ii] == 1) direction2 = 1;
                    else if (enterShort[ii] == 1) direction2 = -1;
                    else direction2 = 0;

                    if (direction1 != 0 && direction1 != direction2)
                    {
                        enable = false;
                    }

                    if (!enable)
                    {
                        enterLong[ii] = 0;
                        enterShort[ii] = 0;
                    }

                    direction1 = direction2;
                }
            }

            Series sigUp1 = atm.setReset(enterLong, enterShort).ReplaceNaN(0);
            Series sigDn1 = atm.setReset(enterShort, enterLong).ReplaceNaN(0);

            Series sigUp2 = (type == 0) ? sigUp1.And(not(sigUp1.ShiftRight(1))) : sigUp1;
            Series sigDn2 = (type == 0) ? sigDn1.And(not(sigDn1.ShiftRight(1))) : sigDn1;

            Series output = sigUp2 - sigDn2;
            return output;
        }

        public static Series calculateSTSig(Series op, Series hi, Series lo, Series cl, int type) // type: 0 = first, 1 = current, 2 = history, 3 = history first signal
        {
            Series st = atm.calculateST(hi, lo, cl);

            Series stGoingUp = atm.goingUp(st);
            Series stGoingDn = atm.goingDn(st);

            Series enterLong = ((stGoingUp).And(st >= 30).And(st <= 70)).Or(st > 70);
            Series enterShort = ((stGoingDn).And(st >= 30).And(st <= 70)).Or(st < 30);

            int count = st.Count;
            if (type < 2)
            {
                int direction1 = 0;
                int direction2 = 0;
                bool enable = true;
                for (int ii = count - 1; ii >= 0; ii--)
                {

                    if (enterLong[ii] == 1) direction2 = 1;
                    else if (enterShort[ii] == 1) direction2 = -1;
                    else direction2 = 0;

                    if (direction1 != 0 && direction1 != direction2)
                    {
                        enable = false;
                    }

                    if (!enable)
                    {
                        enterLong[ii] = 0;
                        enterShort[ii] = 0;
                    }

                    direction1 = direction2;
                }
            }

            Series sigUp1 = atm.setReset(enterLong, enterShort).ReplaceNaN(0);
            Series sigDn1 = atm.setReset(enterShort, enterLong).ReplaceNaN(0);

            Series sigUp2 = (type == 0 || type == 3) ? sigUp1.And(not(sigUp1.ShiftRight(1))) : sigUp1;
            Series sigDn2 = (type == 0 || type == 3) ? sigDn1.And(not(sigDn1.ShiftRight(1))) : sigDn1;

            Series output = sigUp2 - sigDn2;
            return output;
        }

        public static Series calculateFTAlert(Series hi, Series lo, Series cl)
        {
            Series output = new Series();

            Series FT = calculateFT(hi, lo, cl);

            Series u1 = atm.turnsUpBelowLevel(FT, 100);
            Series d1 = atm.turnsDnAboveLevel(FT, 0);

            Series FTUp = frequentSignalFilter(5, u1, u1);
            Series FTDn = frequentSignalFilter(5, d1, d1);

            output = FTUp - FTDn;

            return output;
        }

        public static Series calculateSTAlert(Series hi, Series lo, Series cl)
        {
            Series output = new Series();

            Series ST = calculateST(hi, lo, cl);

            Series u1 = atm.turnsUpBelowLevel(ST, 100);
            Series d1 = atm.turnsDnAboveLevel(ST, 0);

            Series STUp = frequentSignalFilter(5, u1, u1);
            Series STDn = frequentSignalFilter(5, d1, d1);

            output = STUp - STDn;

            return output;
        }

        public static Series calculateFTSTAlert(Series hi, Series lo, Series cl)
        {
            Series output = new Series();

            Series FT = calculateFT(hi, lo, cl);
            Series ST = calculateST(hi, lo, cl);

            Series u1 = atm.turnsUpBelowLevel(FT, 100).And(atm.turnsUpBelowLevel(ST, 100));
            Series d1 = atm.turnsDnAboveLevel(FT, 0).And(atm.turnsDnAboveLevel(ST, 0));

            Series FTSTUp = frequentSignalFilter(5, u1, u1);
            Series FTSTDn = frequentSignalFilter(5, d1, d1);

            output = FTSTUp - FTSTDn;

            return output;
        }

        public static Series calculateTRTAlert(Series cl)
        {
            Series output = new Series();

            Series TRT = calculateTRT(cl);

            Series u1 = atm.turnsUpBelowLevel(TRT, 100);
            Series d1 = atm.turnsDnAboveLevel(TRT, 0);

            Series TRTUp = frequentSignalFilter(5, u1, u1);
            Series TRTDn = frequentSignalFilter(5, d1, d1);

            output = TRTUp - TRTDn;

            return output;
        }

        public static double GetPercentNetChange(int index, Series close)
        {
            double close1 = close[index - 1];
            double close2 = close[index - 0];
            double pnc = (100.0 * (close2 - close1)) / close2;
            return pnc;
        }

        public static double GetPercentNetChangeOp(int index, Series open)
        {
            double open1 = open[index - 1];
            double open2 = open[index - 0];
            double pno = (100.0 * (open2 - open1)) / open2;
            return pno;
        }

        public static double GetPercentNetChangeHi(int index, Series high)
        {
            double high1 = high[index - 1];
            double high2 = high[index - 0];
            double pnh = (100.0 * (high2 - high1)) / high2;
            return pnh;
        }

        public static double GetPercentNetChangeLo(int index, Series low)
        {
            double low1 = low[index - 1];
            double low2 = low[index - 0];
            double pnl = (100.0 * (low2 - low1)) / low2;
            return pnl;
        }

        public static double GetPercentNetChangeVolatility(int period, int index, Series close)
        {
            double volatility1 = GetVolatility(period, index - 1, close);
            double volatility2 = GetVolatility(period, index - 0, close);
            double pncv = (100.0 * (volatility2 - volatility1)) / volatility2;
            return pncv;
        }

		public static double GetVolatility(
			int period,
			int index,
			Series close,
			double annFactor = 252.0) // trading days/year
		{
			if (close == null) throw new ArgumentNullException(nameof(close));
			if (period <= 1) return double.NaN;
			if (index <= 0) return double.NaN;
			if (index - period < 0) return double.NaN;

			int start = index - period + 1;             // inclusive
			int end = index;                          // inclusive

			int n = end - start + 1;
			if (n < 2) return double.NaN;

			double sum = 0.0;
			double sumSq = 0.0;

			// compute log returns r_t = ln(P_t / P_{t-1})
			for (int i = start; i <= end; i++)
			{
				double p0 = close[i - 1];
				double p1 = close[i];

				if (p0 <= 0 || p1 <= 0 || double.IsNaN(p0) || double.IsNaN(p1))
					return double.NaN;

				double r = Math.Log(p1 / p0);
				sum += r;
				sumSq += r * r;
			}

			int m = n; // number of returns
			if (m < 2) return double.NaN;

			double mean = sum / m;
			double variance = (sumSq - m * mean * mean) / (m - 1);
			if (variance < 0) variance = 0;

			double dailyStd = Math.Sqrt(variance);
			if (annFactor <= 0) annFactor = 252.0;

			// return annualized volatility as decimal, e.g. 0.20 = 20%
			double annualizedStd = dailyStd * Math.Sqrt(annFactor);
			return annualizedStd;
		}



		public static Series RSI(Series series, int period)
        {
            Series mom = Series.Momentum(series, 1);
            Series up = mom.SetMinimum(0.0);
            Series dn = -mom.SetMaximum(0.0);
            Series upAvg = Series.SmoothAvg(up, period);
            Series dnAvg = Series.SmoothAvg(dn, period);
            Series rsi = (upAvg * 100) / (upAvg + dnAvg);
            return rsi;
        }

        public static Series calculatePressure(Series high, Series low, Series close)
        {
            Series TSB = calculateTSB(high, low, close);

            int count = TSB.Count;
            Series signal = new Series(count, 0);
            int direction = 0;
            for (int ii = 0; ii < count; ii++)
            {
                if (!double.IsNaN(TSB[ii]))
                {
                    if (TSB[ii] > 70)
                    {
                        signal[ii] = (ii > 0 && TSB[ii - 1] <= 70) ? direction : 2;
                        direction = -1;
                    }
                    else if (TSB[ii] < 30)
                    {
                        signal[ii] = (ii > 0 && TSB[ii - 1] >= 30) ? direction : -2;
                        direction = 1;
                    }
                    else
                    {
                        if (ii > 0 && (TSB[ii - 1] > 70 || TSB[ii - 1] < 30))
                        {
                            signal[ii - 1] = direction;
                        }
                        signal[ii] = direction;
                    }
                }
            }
            return signal;
        }

        public static Series calculateCPI(Series input, int period)
        {
            int count = input.Count;

            Series output = new Series(count);
            double CPI = double.NaN;
            Series avg = Series.SimpleMovingAverage(input, period);
            for (int ii = 0; ii < count; ii++)
            {
                double average = avg[ii];
                if (!double.IsNaN(average))
                {
                    double CPIAcc = 0.0;
                    int inputCount = 0;
                    for (int jj = ii; jj >= 0 && inputCount < period; jj--)
                    {
                        if (!double.IsNaN(input[jj]))
                        {
                            CPIAcc += Math.Abs(average - input[jj]);
                            inputCount++;
                        }
                    }
                    double meanDev = (CPIAcc / period) * 0.015;
                    if (meanDev > 1E-100)
                    {
                        CPI = (input[ii] - average) / (meanDev);
                    }
                }
                output[ii] = CPI;
            }
            return output;
        }

        public static Series goingUp(Series input, int amount = 0)
        {
            int count = input.Count;
            Series output = new Series(count);
            for (int ii = 0; ii < count; ii++)
            {
                double result = 0;
                if (ii > 0 && !Double.IsNaN(input[ii]) && !Double.IsNaN(input[ii - 1]))
                {
                    if (input[ii] > input[ii - 1] + amount)
                    {
                        result = 1;
                    }
                }
                output[ii] = result;
            }
            return output;
        }

        public static Series goingUp2ago(Series input, int amount = 0)
        {
            int count = input.Count;
            Series output = new Series(count);
            for (int ii = 0; ii < count; ii++)
            {
                double result = 0;
                if (ii > 0 && !Double.IsNaN(input[ii]) && !Double.IsNaN(input[ii - 1]) && !Double.IsNaN(input[ii - 2]))
                {
                    if (input[ii - 1] > input[ii - 2] + amount)
                    {
                        result = 1;
                    }
                }
                output[ii] = result;
            }
            return output;
        }

        public static Series goingDn(Series input, int amount = 0)
        {
            int count = input.Count;
            Series output = new Series(count);
            for (int ii = 0; ii < count; ii++)
            {
                double result = 0;
                if (ii > 0 && !Double.IsNaN(input[ii]) && !Double.IsNaN(input[ii - 1]))
                {
                    if (input[ii] < input[ii - 1] - amount)
                    {
                        result = 1;
                    }
                }
                output[ii] = result;
            }
            return output;
        }

        public static Series goingDn2ago(Series input, int amount = 0)
        {
            int count = input.Count;
            Series output = new Series(count);
            for (int ii = 0; ii < count; ii++)
            {
                double result = 0;
                if (ii > 0 && !Double.IsNaN(input[ii]) && !Double.IsNaN(input[ii - 1]) && !Double.IsNaN(input[ii - 2]))
                {
                    if (input[ii - 1] < input[ii - 2] - amount)
                    {
                        result = 1;
                    }
                }
                output[ii] = result;
            }
            return output;
        }

        public static Series rising(Series input)
        {
            Series mask = (input > (input >> 1));
            double lastValue = mask[mask.Count - 1];
            mask = mask | (mask >> 1);
            mask[mask.Count - 1] = lastValue;
            mask = mask.Replace(0, Double.NaN);
            Series output = input * mask;
            return output;
        }

        public static Series falling(Series input)
        {
            Series mask = (input < (input >> 1));
            double lastValue = mask[mask.Count - 1];
            mask = mask | (mask >> 1);
            mask[mask.Count - 1] = lastValue;
            mask = mask.Replace(0, Double.NaN);
            Series output = input * mask;
            return output;
        }

        public static List<Series> calculateFastTurningPoints(Series high, Series low, Series close, Series FT, int lastBarIndex = 0)
        {
            List<Series> tp = new List<Series>();

            //Series avgRng = Series.SMAvg(high - low, 20);

            Series FT1 = FT.ShiftRight(1);
            Series EnbUp = FT1.IsFalling();
            Series EnbDn = FT1.IsRising();

            int count = close.Count;

            int endIndex = (lastBarIndex == 0) ? count - 1 : lastBarIndex + 1;

            FastTrigTPOp tpop = new FastTrigTPOp();
            tp.Add(new Series(count));
            tp.Add(new Series(count));
            for (int ii = 0; ii <= endIndex; ii++)
            {
                double[] input = new double[3] { high[ii], low[ii], close[ii] };
                double[] output = new double[2] { Double.NaN, Double.NaN };
                tpop.Calculate(input, output, ii == endIndex);
                double up = double.IsNaN(output[0]) ? output[1] : output[0];
                double dn = double.IsNaN(output[1]) ? output[0] : output[1];

                if (EnbUp[ii] == 1 /*&& Math.Abs(up - close[ii]) < 8 * avgRng[ii]*/ && up > 0)
                {
                    tp[0][ii] = up;
                }
                if (EnbDn[ii] == 1 /*&& Math.Abs(dn - close[ii]) < 8 * avgRng[ii]*/ && dn > 0)
                {
                    tp[1][ii] = dn;
                }
            }
            return tp;
        }

        public static Series calculateADX(Series hi, Series lo, Series cl, int period)
        {
            int count = cl.Count;

            Series tr = Series.TrueRange(hi, lo, cl);

            Series up = hi - (hi >> 1);
            Series dn = (lo >> 1) - lo;

            Series dmUp = new Series(count, 0);
            Series dmDn = new Series(count, 0);
            for (int ii = 0; ii < count; ii++)
            {
                if (up[ii] > dn[ii] && up[ii] > 0) dmUp[ii] = up[ii];
                if (dn[ii] > up[ii] && dn[ii] > 0) dmDn[ii] = dn[ii];
            }

            Series averageTrueRange = tr.SmoothAvg(period);

            Series diUp = (dmUp.SmoothAvg(period) * 100) / averageTrueRange;
            Series diDn = (dmDn.SmoothAvg(period) * 100) / averageTrueRange;

            Series adx = ((diUp - diDn).Abs() / (diUp + diDn)).SmoothAvg(period) * 100;

            return adx;
        }

        public static List<Series> calculateDMI(Series hi, Series lo, Series cl, string maType, int period)
        {
            var output = new List<Series>();

            int count = cl.Count;

            Series tr = Series.TrueRange(hi, lo, cl);

            Series up = hi - (hi >> 1);
            Series dn = (lo >> 1) - lo;

            Series dmUp = new Series(count, 0);
            Series dmDn = new Series(count, 0);
            for (int ii = 0; ii < count; ii++)
            {
                if (up[ii] > dn[ii] && up[ii] > 0) dmUp[ii] = up[ii];
                if (dn[ii] > up[ii] && dn[ii] > 0) dmDn[ii] = dn[ii];
            }

            Series averageTrueRange = tr.SmoothAvg(period);

            Series diUp = (calculateMovingAvg(maType, dmUp, period) * 100) / averageTrueRange;
            Series diDn = (calculateMovingAvg(maType, dmDn, period) * 100) / averageTrueRange;

            output.Add(diUp);
            output.Add(diDn);

            return output;
        }

        public static Series calculateDDIF(Series hi, Series lo, Series cl, string maType, int period)
        {
            int count = cl.Count;

            Series tr = Series.TrueRange(hi, lo, cl);

            Series up = hi - (hi >> 1);
            Series dn = (lo >> 1) - lo;

            Series dmUp = new Series(count, 0);
            Series dmDn = new Series(count, 0);
            for (int ii = 0; ii < count; ii++)
            {
                if (up[ii] > dn[ii] && up[ii] > 0) dmUp[ii] = up[ii];
                if (dn[ii] > up[ii] && dn[ii] > 0) dmDn[ii] = dn[ii];
            }

            Series averageTrueRange = tr.SmoothAvg(period);

            Series diUp = (calculateMovingAvg(maType, dmUp, period) * 100) / averageTrueRange;
            Series diDn = (calculateMovingAvg(maType, dmDn, period) * 100) / averageTrueRange;

            Series output = diUp - diDn;

            return output;
        }

        public static Series calculateSTStrongUp(Series hi, Series lo, Series cl)
        {
            Dictionary<string, Series> signals = new Dictionary<string, Series>();
            Series st = atm.calculateST(hi, lo, cl);
            Series signal = (atm.turnsUpBelowLevel(st, 70) & st >= 30) | st.CrossesAbove(30);
            return signal;
        }

        public static Series calculateSTStrongDn(Series hi, Series lo, Series cl)
        {
            Dictionary<string, Series> signals = new Dictionary<string, Series>();
            Series st = atm.calculateST(hi, lo, cl);
            Series signal = (atm.turnsDnAboveLevel(st, 30) & st <= 70) | st.CrossesBelow(70);
            return signal;
        }

        public static Series calculateFTUp(Series hi, Series lo, Series cl)
        {
            Dictionary<string, Series> signals = new Dictionary<string, Series>();
            Series ft = atm.calculateFT(hi, lo, cl);
            Series s1 = atm.goingUp(ft);
            Series signal = s1;
            return signal;
        }

        public static Series calculateFTDn(Series hi, Series lo, Series cl)
        {
            Dictionary<string, Series> signals = new Dictionary<string, Series>();
            Series ft = atm.calculateFT(hi, lo, cl);
            Series s1 = atm.goingDn(ft);
            Series signal = s1;
            return signal;
        }

        public static Series calculateTSBUp(Series hi, Series lo, Series cl)
        {
            Dictionary<string, Series> signals = new Dictionary<string, Series>();
            Series tsb = atm.calculateBullishTSB(hi, lo, cl);
            Series press = atm.calculatePressure(hi, lo, cl);
            Series pressUp = atm.goingUp(press);
            Series tsbup = tsb | pressUp;
            Series signal = tsbup;
            return signal;
        }

        public static Series calculateTSBDn(Series hi, Series lo, Series cl)
        {
            Dictionary<string, Series> signals = new Dictionary<string, Series>();
            Series tsb = atm.calculateBearishTSB(hi, lo, cl);
            Series press = atm.calculatePressure(hi, lo, cl);
            Series pressDn = atm.goingDn(press);
            Series tsbdn = tsb | pressDn;
            Series signal = tsbdn;
            return signal;
        }

        
        public static Series calculateSTUp(Series hi, Series lo, Series cl)
        {
            Dictionary<string, Series> signals = new Dictionary<string, Series>();
            Series st = atm.calculateST(hi, lo, cl);
            Series s1 = st > 30 & st < 70;
            Series s2 = atm.goingUp(st);
            Series s3 = st >= 70;
            //Series signal = (st.IsRising() & st > 30 & st < 70) | st >= 70); 
            Series signal = (s1 & s2) | s3;
            return signal;
        }

        public static Series calculateSTDn(Series hi, Series lo, Series cl)
        {
            Dictionary<string, Series> signals = new Dictionary<string, Series>();
            Series st = atm.calculateST(hi, lo, cl);
            Series s1 = st > 30 & st < 70;
            Series s2 = atm.goingDn(st);
            Series s3 = st <= 30;
            //Series signal = ((st.IsFalling() & st > 30 & st < 70) | st <= 30);
            Series signal = (s1 & s2) | s3;
            return signal;
        }

        public static Series calculateTSBUp2(Series hi, Series lo, Series cl)
        {
            Dictionary<string, Series> signals = new Dictionary<string, Series>();
            Series tsb = atm.calculateTSB(hi, lo, cl);
            Series s1 = tsb > 30 & tsb < 70;
            Series s2 = atm.goingUp(tsb);
            Series s3 = tsb >= 70;
            //Series signal = (st.IsRising() & st > 30 & st < 70) | st >= 70); 
            Series signal = (s1 & s2) | s3;
            return signal;
        }

        public static Series calculateTSBDn2(Series hi, Series lo, Series cl)
        {
            Dictionary<string, Series> signals = new Dictionary<string, Series>();
            Series tsb = atm.calculateTSB(hi, lo, cl);
            Series s1 = tsb > 30 & tsb < 70;
            Series s2 = atm.goingDn(tsb);
            Series s3 = tsb <= 30;
            //Series signal = ((st.IsFalling() & st > 30 & st < 70) | st <= 30);
            Series signal = (s1 & s2) | s3;
            return signal;
        }

        public static Series calculateADXUpAlert(Series hi, Series lo, Series cl)
        {
            Dictionary<string, Series> signals = new Dictionary<string, Series>();
            Series adx = atm.calculateADX(hi, lo, cl, 8);
            Series signal = adx.CrossesAbove(15);
            return signal;
        }

        public static Series calculateADXDnAlert(Series hi, Series lo, Series cl)
        {
            Dictionary<string, Series> signals = new Dictionary<string, Series>();
            Series adx = atm.calculateADX(hi, lo, cl, 8);
            Series signal = atm.turnsDnAboveLevel(adx, 40);

            return signal;
        }

        public static List<Series> calculatePressureTurningPoints(Series hi, Series lo, Series cl, bool historyEnable, int currentBarIndex)
        {
            Series FT = atm.calculateFT(hi, lo, cl);
            int count = FT.Count;

            List<Series> output = new List<Series>();
            Series ptpUp = new Series(count);
            Series ptpDn = new Series(count);
            output.Add(ptpUp);
            output.Add(ptpDn);

            int ptpUpCountdown = 0;
            int ptpDnCountdown = 0;
            for (int ii = 0; ii < count; ii++)
            {
                bool enable = (historyEnable || ii == currentBarIndex);
                if (FT[ii] < 25 && ptpUpCountdown <= 0)
                {
                    if (enable) ptpUp[ii] = lo[ii] + .618 * (hi[ii] - lo[ii]);
                    if (cl[ii] > ptpUp[ii]) ptpUpCountdown = 5;
                }
                else
                {
                    ptpUpCountdown--;
                }

                if (FT[ii] > 75 && ptpDnCountdown <= 0)
                {
                    if (enable) ptpDn[ii] = lo[ii] + .382 * (hi[ii] - lo[ii]);
                    if (cl[ii] < ptpDn[ii]) ptpDnCountdown = 5;
                }
                else
                {
                    ptpDnCountdown--;
                }
            }
            return output;
        }

        public static List<Series> calculateSlowTurningPoints(Series high, Series low, Series close, Series ST, int lastBarIndex = 0)
        {
            List<Series> tp = new List<Series>();

            Series avgRng = Series.SimpleMovingAverage(high - low, 20);

            Series ST1 = ST.ShiftRight(1);
            Series EnbUp = ST1.IsFalling();
            Series EnbDn = ST1.IsRising();

            int count = close.Count;

            int endIndex = (lastBarIndex == 0) ? count - 1 : lastBarIndex + 1;

            SlowTrigTPOp tpop = new SlowTrigTPOp();
            tp.Add(new Series(count));
            tp.Add(new Series(count));
            for (int ii = 0; ii <= endIndex; ii++)
            {
                double[] input = new double[3] { high[ii], low[ii], close[ii] };
                double[] output = new double[2] { Double.NaN, Double.NaN };
                tpop.Calculate(input, output, ii == endIndex);

                double up = double.IsNaN(output[0]) ? output[1] : output[0];
                double dn = double.IsNaN(output[1]) ? output[0] : output[1];

                if (EnbUp[ii] == 1 /*&& Math.Abs(up - close[ii]) < 10 * avgRng[ii]*/ && up > 0)
                {
                    tp[0][ii] = up;
                }
                if (EnbDn[ii] == 1 /*&& Math.Abs(dn - close[ii]) < 10 * avgRng[ii]*/ && dn > 0)
                {
                    tp[1][ii] = dn;
                }
            }
            return tp;
        }

        public static Dictionary<string, Series> calculateTrend(Series high, Series low, Series close, Series mid)
        {
            Dictionary<string, Series> Output = new Dictionary<string, Series>();

            Series FT = atm.calculateFT(high, low, close);
            Series ST = atm.calculateST(high, low, close);
            Series TSB = atm.calculateTSB(high, low, close);
            List<Series> TL = atm.calculateTrendLines(high, low, close, mid, 2.0);

            Series tsb_gt_30 = (TSB > 30);
            Series tsb_gt_70 = (TSB > 70);
            Series tsb_lt_70 = (TSB < 70);
            Series tsb_lt_30 = (TSB < 30);

            Series tl_up = atm.hasVal(TL[0]);
            Series not_tl_up = atm.nothasVal(TL[0]);
            Series tl_dn = atm.hasVal(TL[1]);
            Series not_tl_dn = atm.notHasVal(TL[1]);

            Series upSet = (TSB > 30).And(ST > 75).And((ST.ShiftRight(6) < 20).Or(ST.ShiftRight(7) < 20));
            Series dnSet = (TSB < 70).And(ST < 25).And((ST.ShiftRight(6) > 80).Or(ST.ShiftRight(7) > 80));

            Series not_upSet = (upSet <= 0);
            Series not_dnSet = (dnSet <= 0);

            Series upRes = ST < 65;
            Series dnRes = ST > 35;
            Series Uset = (TSB < 70) * atm.setReset(upSet, upRes);
            Series Dset = (TSB > 30) * atm.setReset(dnSet, dnRes);

            Series up_sig = ((tl_up + upSet) * tsb_gt_30 * not_dnSet * turnsUpBelowLevel(FT, 25)) + (not_tl_dn * tsb_gt_70) + (tl_up * Uset);
            Series dn_sig = ((tl_dn + dnSet) * tsb_lt_70 * not_upSet * turnsDnAboveLevel(FT, 75)) + (not_tl_up * tsb_lt_30) + (tl_dn * Dset);

            Series TLup = atm.setReset(up_sig, dn_sig);
            Series TLdn = atm.setReset(dn_sig, up_sig);

            Output["TLup"] = TLup;
            Output["TLdn"] = TLdn;
            return Output;
        }

        public static List<Series> calculateTrendLines(Series high, Series low, Series close, Series mid, double deviation)
        {
            List<Series> sl = new List<Series>();
            int count = close.Count;
            sl.Add(new Series(count));
            sl.Add(new Series(count));
            sl.Add(new Series(count));
            sl.Add(new Series(count));

            Series bs = Series.SmoothAvg(Series.EMAvg(high, 55), 5);
            Series ss = Series.SmoothAvg(Series.EMAvg(low, 55), 5);
            Series CPI = Series.CPI(mid, 34);
            Series ema1 = Series.EMAvg(close, 12);
            Series ema2 = Series.EMAvg(close, 26);
            Series fc1 = Series.SmoothAvg((ema1 * ema2) / ema1, 8);
            Series fc2 = Series.EMAvg(fc1, 9);
            Series hh = highest(high, 50);
            Series ll = lowest(low, 50);

            Series std1 = Series.StdDev(high, 55);  // blue
            Series std2 = Series.StdDev(low, 55);  // red
            Series stdUp1 = ss + std1 * deviation;
            Series stdLw1 = bs - std2 * deviation;

            bool ssx = false;
            bool bsx = false;
            bool be1 = false;
            bool se1 = false;
            bool sss = false;
            bool bss = false;

            for (int ii = 0; ii < count; ii++)
            {
                // entry signals
                bool b1s = false;
                bool s1s = false;
                bool bc = false;
                bool sc = false;
                if (!Double.IsNaN(fc1[ii]) &&
                    !Double.IsNaN(fc2[ii]) &&
                    !Double.IsNaN(CPI[ii]) &&
                    (ii > 0 && !Double.IsNaN(CPI[ii - 1])))
                {
                    b1s = fc1[ii] > fc2[ii] && CPI[ii] > 100 && CPI[ii] > CPI[ii - 1];
                    s1s = fc1[ii] < fc2[ii] && CPI[ii] < -100 && CPI[ii] < CPI[ii - 1];
                    bc = (ssx && !b1s) || s1s;
                    sc = (bsx && !s1s) || b1s;
                }

                // stop lines 
                double ssl = Double.NaN;
                double bsl = Double.NaN;
                if (!Double.IsNaN(close[ii]) &&
                    !Double.IsNaN(ss[ii]) &&
                    !Double.IsNaN(bs[ii]))
                {
                    sss = (b1s && close[ii] < ss[ii]) ? true : (close[ii] > ss[ii]) ? false : sss;
                    bss = (s1s && close[ii] > bs[ii]) ? true : (close[ii] < bs[ii]) ? false : bss;
                    ssl = (sss) ? ll[ii] : ss[ii];	// sell trend line
                    bsl = (bss) ? hh[ii] : bs[ii];	// buy trend line
                    ssx = close[ii] < ssl;		    // sell trend crossed
                    bsx = close[ii] > bsl;	      	// buy trend crossed
                }

                be1 = b1s ? true : bc ? false : be1;
                se1 = s1s ? true : sc ? false : se1;

                sl[0][ii] = be1 ? ssl : Double.NaN;
                sl[1][ii] = se1 ? bsl : Double.NaN;
                sl[2][ii] = be1 ? stdUp1[ii] : Double.NaN;
                sl[3][ii] = se1 ? stdLw1[ii] : Double.NaN;
            }
            return sl;
        }

        public static List<int> getSignals(string condition, int maxAgo, Series hi, Series lo, Series cl)
        {
            List<int> signals = null;
            if (condition == "TREND CONDITION")
            {
                signals = atm.getFTSignals(maxAgo, hi, lo, cl);
            }
            else if (condition == "HEAT BARS")
            {
                signals = atm.getHBSignals(maxAgo, hi, lo, cl);
            }
            else if (condition == "TREND STRENGTH")
            {
                signals = atm.getTSBSignals(maxAgo, hi, lo, cl);
            }
			else if (condition == "ATM ANALYSIS")
			{
				//signals = IdeaCalculator.getAnalysi(maxAgo, hi, lo, cl);
			}
			return signals;
        }

        public static List<int> getFTSignals(int maxAgo, Series hi, Series lo, Series cl)
        {
            return atm.GetTrendCondition(maxAgo, hi, lo, cl);
        }

        public static List<int> getHBSignals(int maxAgo, Series hi, Series lo, Series cl)
        {
            List<int> signals = new List<int>();

            Dictionary<string, Series> tl = atm.calculateTrend(hi, lo, cl);
            Series TLup = tl["TLup"];
            Series TLdn = tl["TLdn"];

            List<Series> TargetUp = atm.calculateUpperTargets(hi, lo, TLup);
            List<Series> TargetDn = atm.calculateLowerTargets(hi, lo, TLdn);

            Series hi1 = (TargetUp[0] & (hi < TargetUp[0]));
            Series hi2 = (TargetUp[0] & (hi >= TargetUp[0]) & (hi < TargetUp[1])).Replace(1, 2);
            Series hi3 = (TargetUp[0] & (hi >= TargetUp[1]) & (hi < TargetUp[2])).Replace(1, 3);
            Series hi4 = (TargetUp[0] & (hi >= TargetUp[2]) & (hi < TargetUp[3])).Replace(1, 4);
            Series hi5 = (TargetUp[0] & (hi >= TargetUp[3])).Replace(1, 5);

            Series lo1 = (TargetDn[0] & (lo > TargetDn[0]));
            Series lo2 = (TargetDn[0] & (lo <= TargetDn[0]) & (lo > TargetDn[1])).Replace(1, 2);
            Series lo3 = (TargetDn[0] & (lo <= TargetDn[1]) & (lo > TargetDn[2])).Replace(1, 3);
            Series lo4 = (TargetDn[0] & (lo <= TargetDn[2]) & (lo > TargetDn[3])).Replace(1, 4);
            Series lo5 = (TargetDn[0] & (lo <= TargetDn[3])).Replace(1, 5);

            Series heatBars = (hi1 + hi2 + hi3 + hi4 + hi5) - (lo1 + lo2 + lo3 + lo4 + lo5);

            for (int ago = 0; ago < maxAgo; ago++)
            {
                int index = heatBars.Count - 1 - ago;

                if (index >= 0)
                {
                    int sig = (int)heatBars[index];
                    signals.Add(sig);
                }
            }
            return signals;
        }

        public static List<int> getTSBSignals(int maxAgo, Series hi, Series lo, Series cl)
        {
            List<int> signals = new List<int>();

            Series TSB = atm.calculateTSB(hi, lo, cl);
            Series ST = atm.calculateST(hi, lo, cl);
            Series EZI = atm.calculateEZI(cl);

            Series upSet = (TSB > 30) & (ST > 75) & (((ST >> 6) < 20) | ((ST >> 7) < 20));
            Series dnSet = (TSB < 70) & (ST < 25) & (((ST >> 6) > 80) | ((ST >> 7) > 80));
            Series upRes = ST < 65;
            Series dnRes = ST > 35;

            Series Utsb = (EZI < 80) & (TSB >= 70);
            Series Dtsb = (EZI <= 20) & (TSB <= 30);
            Series Uezi = (EZI >= 80) & (TSB >= 70);
            Series Dezi = (EZI >= 20) & (TSB <= 30);
            Series Uset = (TSB < 70) & atm.setReset(upSet, upRes);
            Series Dset = (TSB > 30) & atm.setReset(dnSet, dnRes);

            Series sig1 = Utsb.Replace(1, 1);
            Series sig2 = Dtsb.Replace(1, -1);
            Series sig3 = Uezi.Replace(1, 2);
            Series sig4 = Dezi.Replace(1, -2);
            Series sig5 = Uset.Replace(1, 3);
            Series sig6 = Dset.Replace(1, -3);

            Series tsb = sig1 + sig2 + sig3 + sig4 + sig5 + sig6;

            for (int ago = 0; ago < maxAgo; ago++)
            {
                int index = tsb.Count - 1 - ago;

                if (index >= 0)
                {
                    int sig = (int)tsb[index];
                    signals.Add(sig);
                }
            }
            return signals;
        }

        public static List<int> getTSBSignals2(int maxAgo, Series hi, Series lo, Series cl)
        {
            List<int> signals = new List<int>();

            Series TSB = atm.calculateTSB(hi, lo, cl);
            Series ST = atm.calculateST(hi, lo, cl);
            Series EZI = atm.calculateEZI(cl);
            //Series PRS = atm.calculatePressure(hi, lo, cl);
            Series prsSig = TSB.IsRising() - TSB.IsFalling();

            Series upSet = (TSB > 30) & (ST > 75) & (((ST >> 6) < 20) | ((ST >> 7) < 20));
            Series dnSet = (TSB < 70) & (ST < 25) & (((ST >> 6) > 80) | ((ST >> 7) > 80));
            Series upRes = ST < 65;
            Series dnRes = ST > 35;

            Series Utsb = (EZI < 80) & (TSB >= 70);
            Series Dtsb = (EZI <= 20) & (TSB <= 30);
            Series Uezi = (EZI >= 80) & (TSB >= 70);
            Series Dezi = (EZI >= 20) & (TSB <= 30);
            Series Uset = (TSB < 70) & atm.setReset(upSet, upRes);
            Series Dset = (TSB > 30) & atm.setReset(dnSet, dnRes);

            Series sig1 = Utsb.Replace(1, 1);
            Series sig2 = Dtsb.Replace(1, -1);
            Series sig3 = Uezi.Replace(1, 2);
            Series sig4 = Dezi.Replace(1, -2);
            Series sig5 = Uset.Replace(1, 3);
            Series sig6 = Dset.Replace(1, -3);

            Series tsb = sig1 + sig2 + sig3 + sig4 + sig5 + sig6;

            for (int ago = 0; ago < maxAgo; ago++)
            {
                int index = tsb.Count - 1 - ago;

                if (index >= 0)
                {
                    int sig = (int)tsb[index];

                    if (sig == 0)
                    {
                        sig = (int)(4 * prsSig[index]);
                    }

                    signals.Add(sig);
                }
            }
            return signals;
        }

        public static void calculateFTLinesOS(Series FT, Series low, int currentIndex, ref Series FTUpAlert, ref Series FTDnAlert)
        {
            Series FT1 = FT.ShiftRight(1);
            Series FT2 = FT.ShiftRight(2);

            double minLo = double.MaxValue;

            bool greEnb = false;
            bool redEnb = false;

            IList<int> greIdx = new List<int>();
            IList<int> redIdx = new List<int>();

            int count1 = FT.Count;
            for (int ii = 0; ii < count1; ii++)
            {
                double lo = low[ii];

                bool valid = !double.IsNaN(FT[ii]) && !double.IsNaN(FT1[ii]) && !double.IsNaN(FT2[ii]);

                if (greEnb)
                {
                    // FT greater than 25 or FT less than 25 and turns down or last bar
                    if (valid && (FT[ii] >= 25 || (FT1[ii] < 25 && FT2[ii] < FT1[ii] && FT1[ii] > FT[ii]) || ii == FT.Count - 1))
                    {
                        greEnb = false;
                        greIdx.Add(ii);  // green ends
                    }
                }

                // FT goes below 25 or FT less than 25 and turns down
                if (valid && ((FT1[ii] >= 25 && FT[ii] < 25) || (FT1[ii] < 25 && FT2[ii] < FT1[ii] && FT1[ii] > FT[ii])))
                {
                    redEnb = true;
                    redIdx.Add(ii);  // red starts
                }

                if (redEnb)
                {
                    if (ii == currentIndex || FT[ii] > FT[ii - 1])
                    {
                        redEnb = false;
                        greEnb = true;
                        redIdx.Add(ii);  // red ends
                        greIdx.Add(ii);  // green starts
                    }
                }

                if (valid && !double.IsNaN(lo) && (FT[ii] < 25 || FT1[ii] < 25))
                {
                    minLo = System.Math.Min(minLo, lo);
                }

                // last bar or FT above 25
                if (ii == currentIndex || (FT[ii] >= 25 && FT1[ii] >= 25))
                {
                    int count2 = greIdx.Count - (greIdx.Count % 2);
                    for (int jj = 0; jj < count2; jj += 2)
                    {
                        for (int index = greIdx[jj]; index < greIdx[jj + 1]; index++)
                        {
                            FTUpAlert[index] = minLo;
                        }
                    }
                    greIdx.Clear();

                    int count3 = redIdx.Count - (redIdx.Count % 2);
                    for (int jj = 0; jj < count3; jj += 2)
                    {
                        for (int index = redIdx[jj]; index < redIdx[jj + 1]; index++)
                        {
                            FTDnAlert[index] = minLo;
                        }
                    }
                    redIdx.Clear();

                    minLo = lo;
                }
            }
        }

        public static void calculateFTLinesOB(Series FT, Series high, int currentIndex, ref Series FTUpAlert, ref Series FTDnAlert)
        {
            Series FT1 = FT.ShiftRight(1);
            Series FT2 = FT.ShiftRight(2);

            double maxHi = double.MinValue;

            bool redEnb = false;
            bool greEnb = false;

            IList<int> redIdx = new List<int>();
            IList<int> greIdx = new List<int>();

            int count1 = FT.Count;
            for (int ii = 0; ii < count1; ii++)
            {
                double hi = high[ii];

                bool valid = !double.IsNaN(FT[ii]) && !double.IsNaN(FT1[ii]) && !double.IsNaN(FT2[ii]);

                if (redEnb)
                {
                    if (valid && (FT[ii] <= 75 || (FT1[ii] > 75 && FT2[ii] > FT1[ii] && FT1[ii] < FT[ii]) || ii == FT.Count - 1))
                    {
                        redEnb = false;
                        redIdx.Add(ii);
                    }
                }

                if (valid && ((FT1[ii] <= 75 && FT[ii] > 75) || (FT1[ii] > 75 && FT2[ii] > FT1[ii] && FT1[ii] < FT[ii])))
                {
                    greEnb = true;
                    greIdx.Add(ii);
                }

                if (greEnb)
                {
                    if (ii == currentIndex || FT[ii] < FT[ii - 1])
                    {
                        greEnb = false;
                        redEnb = true;
                        greIdx.Add(ii);
                        redIdx.Add(ii);
                    }
                }

                if (valid && !double.IsNaN(hi) && (FT[ii] > 75 || FT1[ii] > 75))
                {
                    maxHi = System.Math.Max(maxHi, hi);
                }

                if (ii == currentIndex || (FT[ii] <= 75 && FT1[ii] <= 75))
                {
                    int count2 = redIdx.Count - (redIdx.Count % 2);
                    for (int jj = 0; jj < count2; jj += 2)
                    {
                        for (int index = redIdx[jj]; index < redIdx[jj + 1]; index++)
                        {
                            FTDnAlert[index] = maxHi;
                        }
                    }
                    redIdx.Clear();

                    int count3 = greIdx.Count - (greIdx.Count % 2);
                    for (int jj = 0; jj < count3; jj += 2)
                    {
                        for (int index = greIdx[jj]; index < greIdx[jj + 1]; index++)
                        {
                            FTUpAlert[index] = maxHi;
                        }
                    }
                    greIdx.Clear();

                    maxHi = hi;
                }
            }
        }


        // sync input using times1 to output using times2
        public static Series sync(Series input, string interval1, string interval2, List<DateTime> times1, List<DateTime> times2, bool useCloseTimes = true)
        {
            int cnt = input.Count;

            int cnt1 = times1.Count;
            int cnt2 = times2.Count;

            int sidx1 = cnt1 - 1;

            Series output = new Series(cnt2, 0);

            string i1 = interval1.Substring(0, 1);
            string i2 = interval2.Substring(0, 1);
            int keySize1 = (i1 == "Y") ? 4 : ((i1 == "S" || i1 == "Q" || i1 == "M") ? 6 : ((i1 == "W" || i1 == "D") ? 8 : 12));
            int keySize2 = (i2 == "Y") ? 4 : ((i2 == "S" || i2 == "Q" || i2 == "M") ? 6 : ((i2 == "W" || i2 == "D") ? 8 : 12));
            int keySize = useCloseTimes ? Math.Max(keySize1, keySize2) : Math.Min(keySize1, keySize2);   // changing to Max will wait for signals until the end of the week or month

            var end = i2 == "Y" || i2 == "S" || i2 == "Q" || i2 == "M" || i2 == "W";

            // for each output time in reverse order
            for (int idx2 = cnt2 - 1; idx2 >= 0; idx2--)
            {
                // current output time to consider
                DateTime time2 = times2[idx2];
                long key2 = long.Parse(time2.ToString("yyyyMMddHHmm").Substring(0, keySize));

                //for each input time in reverse order
                for (int idx1 = sidx1; idx1 >= 0; idx1--)
                {

                    DateTime time1 = times1[idx1];
                    long key1 = long.Parse(time1.ToString("yyyyMMddHHmm").Substring(0, keySize));

                    // input time is less than or equal to the output time
                    if (key1 <= key2)
                    {
                        // cnt is size of input data
                        // cnt1 is size of the input times
                        int idx = (cnt - 1) - ((cnt1 - 1) - idx1);

                        var use = end ? idx2 == 0 || times2[idx2 - 1] < DateTime.UtcNow : times2[idx2] < DateTime.UtcNow;
                        if (use)
                        {
                            output[idx2] = input[idx];
                        }

                        sidx1 = idx1; // set the starting input index 
                        break;
                    }
                }
            }
            return output;
        }

        public static string getInterval(string input, int level)
        {
            string output = "";
            if (input == "Yearly" || input == "1Y")
            {
                output = "Yearly";
            }
            else if (input == "SemiAnnually" || input == "1S")
            {
                output = (level == -1) ? "Quarterly" : (level == 0) ? "SemiAnnually" : (level == 1) ? "Yearly" : "Yearly";
            }
            else if (input == "Quarterly" || input == "1Q")
            {
                output = (level == -1) ? "Monthly" : (level == 0) ? "Quarterly" : (level == 1) ? "Yearly" : "Yearly";  //replaced SemiAnnually in level 1
            }
            else if (input == "Monthly" || input == "1M")
            {
                output = (level == -1) ? "Weekly" : (level == 0) ? "Monthly" : (level == 1) ? "Quarterly" : "Yearly";
            }
            else if (input == "Weekly" || input == "1W")
            {
                output = (level == -1) ? "Daily" : (level == 0) ? "Weekly" : (level == 1) ? "Monthly" : "Quarterly";
            }
            else if (input == "Daily" || input == "1D")
            {
                output = (level == -1) ? "240" : (level == 0) ? "Daily" : (level == 1) ? "Weekly" : "Monthly";
            }
            else if (input == "240" || input == "240 Min")
            {
                output = (level == -1) ? "120" : (level == 0) ? "240" : (level == 1) ? "Daily" : "Weekly";
            }
            else if (input == "120" || input == "120 Min")
            {
                output = (level == -1) ? "60" : (level == 0) ? "120" : (level == 1) ? "Daily" : "Weekly";
            }
            else if (input == "60" || input == "60 Min")
            {
                output = (level == -1) ? "30" : (level == 0) ? "60" : (level == 1) ? "240" : "Daily";
            }
            else if (input == "30" || input == "30 Min")
            {
                output = (level == -1) ? "15" : (level == 0) ? "30" : (level == 1) ? "120" : "Daily";
            }
            else if (input == "15" || input == "15 Min")
            {
                output = (level == -1) ? "5" : (level == 0) ? "15" : (level == 1) ? "60" : "240";
            }
            else if (input == "5" || input == "5 Min")
            {
                output = (level == -1) ? "1" : (level == 0) ? "5" : (level == 1) ? "30" : "120";
            }
            else if (input == "2" || input == "2 Min")
            {
                output = (level == -1) ? "1" : (level == 0) ? "2" : (level == 1) ? "5" : "15";
            }
            else if (input == "1" || input == "1 Min")
            {
                output = (level == -1) ? "1" : (level == 0) ? "1" : (level == 1) ? "5" : "30";
            }
            return output;
        }

        //public static void runPowerShell()
        //{

        //   // using (new Impersonator("ATM", "ATM", "rkma1124"))
        //    {
        //        Runspace runSpace = RunspaceFactory.CreateRunspace();
        //        runSpace.Open();
        //        Pipeline pipeline = runSpace.CreatePipeline();
        //        Command getProcess = new Command("Get-Process");
        //        Command sort = new Command("Sort-Object");
        //        sort.Parameters.Add("Property", "VM");
        //        pipeline.Commands.Add(getProcess);
        //        pipeline.Commands.Add(sort);
        //        Collection<PSObject> output = pipeline.Invoke();
        //        foreach (PSObject psObject in output)
        //        {
        //            Process process = (Process)psObject.BaseObject;
        //            Console.WriteLine("Process name: " + process.ProcessName);
        //        }
        //    }

        //    //    using (PowerShell PowerShellInstance = PowerShell.Create())
        //    //    {
        //    //        // use "AddScript" to add the contents of a script file to the end of the execution pipeline.
        //    //        // use "AddCommand" to add individual commands/cmdlets to the end of the execution pipeline.

        //    //        PowerShellInstance.AddCommand("Install-Module AzureRM");
        //    //        PowerShellInstance.AddCommand("Import-Module AzureRM");

        //    //        // use "AddParameter" to add a single parameter to the last command/script on the pipeline.
        //    //        // PowerShellInstance.AddParameter("param1", "parameter 1 value!");

        //    //        Collection<PSObject> PSOutput = PowerShellInstance.Invoke();

        //    //        // loop through each output object item
        //    //        foreach (PSObject outputItem in PSOutput)
        //    //        {
        //    //            // if null object was dumped to the pipeline during the script then a null
        //    //            // object may be present here. check for null to prevent potential NRE.
        //    //            if (outputItem != null)
        //    //            {
        //    //                //TODO: do something with the output item 
        //    //                // outputItem.BaseOBject
        //    //            }
        //    //        }
        //    //    }
        //}

        //public static void createDataFile(Senario type, BarCache barCache, List<string> tickers, List<string> intervals, int barCount, int agoCount, bool train = false)
        //{
        //    var data = atm.getModelData(type, barCache, tickers, intervals, barCount, agoCount, train);

        //    string inputFileName = train ? @"scripts\PxfTrain.csv" : @"scripts\PxfTest.csv";

        //    var colCnt = data[0].Count;
        //    var rowCnt = data.Count;

        //    StreamWriter sw = new StreamWriter(inputFileName);

        //    string header = "target";
        //    for (int ii = 1; ii < colCnt; ii++)
        //    {
        //        header += ",input" + ii.ToString();
        //    }
        //    sw.WriteLine(header);

        //    for (int ii = 0; ii < rowCnt; ii++)
        //    {
        //        string row = "";
        //        for (int jj = 0; jj < colCnt; jj++)
        //        {
        //            if (jj > 0) row += ",";
        //            row += data[ii][jj];
        //        }
        //        sw.WriteLine(row);
        //    }
        //    sw.Close();
        //}

        ////public static Task predict() // warning - could take a long tmme to return - use in non-ui thread
        //{
        //    // predict
        //    var endPnt = "https://ussouthcentral.services.azureml.net/subscriptions/0111a19682204bf58bd9ba352d5d4cb4/services/76a816ad29f449458610d3bf4fc72285/jobs";
        //    var apiKey = "fD9+xU0dGUyqej430Ii8rqKvsTabx7YNm1F+LZmoYQSiEXfAa0GX1RlHNXTtgveGOzsSvQow6UPs3MPuw6ocwg==";
        //    string inputFileName = @"scripts\PxfTest.csv";
        //    var task = TrainModel.InvokeBatchExecutionService(false, endPnt, apiKey, inputFileName);
        //    return task;
        //}

        //public static Dictionary<string, double> createPredictions()
        //{
        //    Dictionary<string, double> output = new Dictionary<string, double>();

        //    var info = new List<Tuple<string, string>>();
        //    var sr1 = new StreamReader(@"scripts\PxfTest.csv");
        //    string line1 = "";
        //    int row1 = 0;
        //    while ((line1 = sr1.ReadLine()) != null)
        //    {
        //        if (row1 > 0)
        //        {
        //            string[] fields = line1.Split(',');
        //            info.Add(new Tuple<string, string>(fields[1], fields[2]));
        //        }
        //        row1++;
        //    }
        //    sr1.Close();

        //    var sr2 = new StreamReader(@"scripts\azure_output_data.csv");
        //    string line2 = "";
        //    int row2 = 0;
        //    while ((line2 = sr2.ReadLine()) != null)
        //    {
        //        if (row2 > 0)
        //        {
        //            string[] fields = line2.Split(',');
        //            var prediction = double.Parse(fields[fields.Length - 2]);
        //            var symbol = info[row2 - 1].Item1;

        //            var intervalAndAgo = info[row2 - 1].Item2;
        //            string[] items = intervalAndAgo.Split(':');
        //            var interval = items[0];
        //            var agoOrTime = items[1];

        //            string key = symbol + ":" + interval + ":" + agoOrTime;
        //            output[key] = prediction;
        //        }
        //        row2++;
        //    }
        //    sr2.Close();

        //    return output;
        //}

        ////public static async Task trainRequestResponse(List<List<string>> input)
        //{
        //    var output = new Dictionary<string, Dictionary<string, Dictionary<DateTime, double>>>();
        //    using (var client = new HttpClient())
        //    {
        //        if (input.Count > 0)
        //        {
        //            var inputData = ATMML.MLPredict.getRequestInputData(input);
        //            var inputs =  inputData.Item1;

        //            var scoreRequest = new
        //            {
        //                Inputs = inputs,
        //                GlobalParameters = new Dictionary<string, string>()
        //                {
        //                }
        //            };

        //            const string apiKey = "";
        //            client.BaseAddress = new Uri("");
        //            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        //            HttpResponseMessage response = null;
        //            try
        //            {
        //                response = await client.PostAsJsonAsync("", scoreRequest);
        //            }
        //            catch (Exception x)
        //            {
        //                //Debug.WriteLine("Request Response Exception : " + x.Message);
        //            }

        //            if (response != null && response.IsSuccessStatusCode)
        //            {
        //                string resultText = await response.Content.ReadAsStringAsync();
        //                dynamic obj = JsonConvert.DeserializeObject(resultText);

        //                var result = (JArray)obj["Results"]["output2"];
        //            }
        //        }
        //    }
        //}

        //public static Task Train() // warning - could take a long tmme to return - use in non-ui thread
        //{
        //    // predict
        //    var endPnt = "https://ussouthcentral.services.azureml.net/subscriptions/0111a19682204bf58bd9ba352d5d4cb4/services/76a816ad29f449458610d3bf4fc72285/jobs";
        //    var apiKey = "fD9+xU0dGUyqej430Ii8rqKvsTabx7YNm1F+LZmoYQSiEXfAa0GX1RlHNXTtgveGOzsSvQow6UPs3MPuw6ocwg==";
        //    string inputFileName = @"scripts\PxfTrain.csv";
        //    var task = TrainModel.InvokeBatchExecutionService(false, endPnt, apiKey, inputFileName);
        //    return task;
        //}

        //public static List<List<string>> getModelData(Senario type, BarCache barCache, List<string> tickers, List<string> intervals, int barCount, int agoCount, bool train = false)
        //{
        //    int count = barCount;
        //    var output = new List<List<string>>();
        //    foreach (var ticker in tickers)
        //    {
        //        foreach (var interval in intervals)
        //        {
        //            var interval1 = interval;
        //            var interval2 = getInterval(interval, 1);

        //            var bars1 = barCache.GetSeries(ticker, interval1, new string[] { "High", "Low", "Close" }, 0, count);
        //            var bars2 = barCache.GetSeries(ticker, interval2, new string[] { "High", "Low", "Close" }, 0, count);

        //            var times1 = (barCache.GetTimes(ticker, interval1, 0, count));
        //            var times2 = (barCache.GetTimes(ticker, interval2, 0, count));

        //            if (bars1 != null && bars1[0].Count > 0 && bars2 != null && bars2[0].Count > 0)
        //            {
        //                Series hi1 = bars1[0];
        //                Series lo1 = bars1[1];
        //                Series cl1 = bars1[2];
        //                Series nxtcl1 = bars1[2].ShiftLeft(1);

        //                Series hi2 = bars2[0];
        //                Series lo2 = bars2[1];
        //                Series cl2 = bars2[2];

        //                Series ft1 = atm.calculateFT(hi1, lo1, cl1);
        //                Series ft1Up = ft1.IsRising();
        //                Series ft1Dn = ft1.IsFalling();

        //                Series ft2x = atm.calculateFT(hi2, lo2, cl2);
        //                Series ft2Upx = ft2x.IsRising();
        //                Series ft2Dnx = ft2x.IsFalling();

        //                Series ft2 = atm.sync(ft2x, interval2, interval1, times2, times1);
        //                Series ft2Up = atm.sync(ft2Upx, interval2, interval1, times2, times1);
        //                Series ft2Dn = atm.sync(ft2Dnx, interval2, interval1, times2, times1);

        //                Series st = atm.calculateST(hi1, lo1, cl1);
        //                Series stUp = st.IsRising();
        //                Series stDn = st.IsFalling();

        //                Series tsbUp = atm.calculateBullishTSB(hi1, lo1, cl1);
        //                Series tsbDn = atm.calculateBearishTSB(hi1, lo1, cl1);

        //                List<Series> TL = atm.calculateTrendLines(hi1, lo1, cl1, 2.0);
        //                Series tlUp = atm.hasVal(TL[0]);
        //                Series tlDn = atm.hasVal(TL[1]);

        //                Series mid = (hi1 + lo1) / 2;
        //                Series ma = Series.EMAvg(mid, 34);
        //                Series tbUp = lo1 > ma;
        //                Series tbDn = hi1 < ma;

        //                Series ret = ((nxtcl1 - cl1) / cl1);
        //                Series tr = Series.TrueRange(hi1, lo1, cl1);
        //                Series atr = Series.SMAvg(tr, 5);
        //                Series val = (type == Senario.Volatility) ? atr : ret;

        //                int crossover = cl1.Count - Math.Min(agoCount, cl1.Count);

        //                int startIndex = train ? 0 : crossover;
        //                int endIndex = train ? crossover : cl1.Count;

        //                for (int ii = startIndex; ii < endIndex; ii++)
        //                {
        //                    var row = new List<string>();
        //                    row.Add(train ? val[ii].ToString() : "0");
        //                    row.Add(ticker);
        //                    row.Add(interval + ";" + times1[ii].ToString("yyyyMMddHHmmss"));
        //                    row.Add(ft1[ii].ToString());
        //                    row.Add(ft1Up[ii].ToString());
        //                    row.Add(ft1Dn[ii].ToString());
        //                    row.Add(ft2[ii].ToString());
        //                    row.Add(ft2Up[ii].ToString());
        //                    row.Add(ft2Dn[ii].ToString());
        //                    row.Add(st[ii].ToString());
        //                    row.Add(stUp[ii].ToString());
        //                    row.Add(stDn[ii].ToString());
        //                    row.Add(tsbUp[ii].ToString());
        //                    row.Add(tsbDn[ii].ToString());
        //                    row.Add(tlUp[ii].ToString());
        //                    row.Add(tlDn[ii].ToString());
        //                    row.Add(tbUp[ii].ToString());
        //                    row.Add(tbDn[ii].ToString());

        //                    output.Add(row);
        //                }
        //            }
        //        }
        //    }

        //    if (train && output.Count > 0)
        //    {
        //        List<double> returns = output.Select(x => double.Parse(x[0])).ToList();
        //        returns.Sort();
        //        int index = returns.Count / 2;
        //        double median = returns[index];

        //        output.ForEach(x => x[0] = (double.Parse(x[0]) >= median) ? "1" : "0");
        //    }

        //    return output;
        //}


        public static void saveModelData(string path, List<List<string>> input)
        {
            var inputCount = (input.Count > 0) ? input[0].Count - 1 : 0;

            var sb = new StringBuilder();

            sb.Append("output,");
            sb.Append(String.Join(",", Enumerable.Range(1, inputCount).Select(number => "input" + number).ToList()));
            //sb.Append(",SamplingKeyColumn");
            sb.Append("\n");
            //var sampleNumber = 0;
            input.ForEach(sample => sb.Append(String.Join(",", sample) + /*"," + (++sampleNumber).ToString() + */ "\n"));

            var data = sb.ToString();
            MainView.SaveUserData(path, data);
        }

        public static List<List<string>> getModelData(List<string> features, Senario senario, BarCache barCache, List<string> tickers, string interval, int count, string split, bool train, bool group)
        {
            return getModelData(features, senario, barCache, tickers, interval, default(DateTime), default(DateTime), count, split, train, group).data;
        }

        private static List<Series> createPatternFeatures(int patternSize, Dictionary<string, List<DateTime>> timeList, Dictionary<string, Series[]> barList,
            string term, string interval1, string interval2)
        {
            var output = new List<Series>();
            PriceType[] priceTypes = { PriceType.High, PriceType.Low, PriceType.Close };
            var bars = (term == "Mid Term") ? barList[interval2] : barList[interval1];
            for (int ago = 0; ago < patternSize; ago++)
            {
                for (var pt = 0; pt < priceTypes.Length; pt++)
                {
                    var results = calculateBarPattern(bars, priceTypes[pt], patternSize, ago);
                    if (term == "Mid Term") results = atm.sync(results, interval2, interval1, timeList[interval2], timeList[interval1]);
                    output.Add(results);
                }
            }
            return output;
        }

        private static List<Series> createWindowFeatures(int patternSize, Dictionary<string, List<DateTime>> timeList, Dictionary<string, Series[]> barList,
            string term, string interval1, string interval2, int windowAgo = 0)
        {
            var output = new List<Series>();
            PriceType[] priceTypes = { PriceType.High, PriceType.Low, PriceType.Close };
            var bars = (term == "Mid Term") ? barList[interval2] : barList[interval1];
            for (var pt = 0; pt < priceTypes.Length; pt++)
            {
                var results = calculateBarPattern(bars, priceTypes[pt], patternSize, 0, windowAgo);
                if (term == "Mid Term") results = atm.sync(results, interval2, interval1, timeList[interval2], timeList[interval1]);
                output.Add(results);
            }
            return output;
        }

        private static Series calculateBarPattern(Series[] bars, PriceType priceType, int period, int ago, int windowAgo = 0)
        {
            Series op = bars[0];
            Series hi = bars[1];
            Series lo = bars[2];
            Series cl = bars[3];

            var price = cl;
            if (priceType == PriceType.Open) price = op;
            if (priceType == PriceType.High) price = hi;
            if (priceType == PriceType.Low) price = lo;

            Series minLo = Series.Minimum(lo, period).ShiftRight(windowAgo);
            Series maxHi = Series.Maximum(hi, period).ShiftRight(windowAgo);
            Series value = ((price - minLo) * 100) / (maxHi - minLo);
            Series output = value.ShiftRight(ago);
            return output;
        }

        public struct ModelData
        {
            public double bias;
            public List<List<string>> data;
        }

        public static ModelData getModelData(List<string> features, Senario senario, BarCache barCache, List<string> tickers, string interval, 
            DateTime date1, DateTime date2, int count, string split, bool train, bool group, Dictionary<string, object> referenceData = null)
        {
            var output = new ModelData();
            double bias = 0.0;
            var data = new List<List<string>>();

            var useCountAndOffset = (date1 == default(DateTime) || date2 == default(DateTime));
            int barRequestCount = BarServer.MaxBarCount;

            var interval1 = interval;
            var interval2 = getInterval(interval, 1);
            var interval3 = getInterval(interval, 2);

            foreach (var ticker in tickers)
            {
                var bars1 = barCache.GetSeries(ticker, interval1, new string[] { "Open", "High", "Low", "Close" }, 0, barRequestCount);
                var bars2 = barCache.GetSeries(ticker, interval2, new string[] { "Open", "High", "Low", "Close" }, 0, barRequestCount);
                var bars3 = barCache.GetSeries(ticker, interval3, new string[] { "Open", "High", "Low", "Close" }, 0, barRequestCount);

                var times1 = (barCache.GetTimes(ticker, interval1, 0, barRequestCount));
                var times2 = (barCache.GetTimes(ticker, interval2, 0, barRequestCount));
                var times3 = (barCache.GetTimes(ticker, interval3, 0, barRequestCount));

                if (bars1 != null && bars1[0].Count > 0)
                {
                    int barCount = times1.Count;

                    var inputs = new List<Series>();

                    for (int jj = 0; jj < features.Count; jj++)
                    {
                        if (features[jj].Length > 0)
                        {
                            try
                            {
                                var items = features[jj].Split('\u0002');
                                var condition = items[0].Trim();
                                var term = items[1].Trim();

                                Dictionary<string, List<DateTime>> timeList1 = new Dictionary<string, List<DateTime>>();
                                timeList1[(term == "Mid Term") ? interval2 : interval1] = (term == "Mid Term") ? times2 : times1;
                                timeList1[(term == "Mid Term") ? interval3 : interval2] = (term == "Mid Term") ? times3 : times2;
                                Dictionary<string, Series[]> barList1 = new Dictionary<string, Series[]>();
                                barList1[(term == "Mid Term") ? interval2 : interval1] = (term == "Mid Term") ? bars2 : bars1;
                                barList1[(term == "Mid Term") ? interval3 : interval2] = (term == "Mid Term") ? bars3 : bars2;

                                if (condition == "2 Bar Pattern") inputs.AddRange(createPatternFeatures(2, timeList1, barList1, term, interval1, interval2));
                                else if (condition == "3 Bar Pattern") inputs.AddRange(createPatternFeatures(3, timeList1, barList1, term, interval1, interval2));
                                else if (condition == "4 Bar Pattern") inputs.AddRange(createPatternFeatures(4, timeList1, barList1, term, interval1, interval2));
                                else if (condition == "5 Bar Pattern") inputs.AddRange(createPatternFeatures(5, timeList1, barList1, term, interval1, interval2));

                                else if (condition == "3 Bar Window") inputs.AddRange(createWindowFeatures(3, timeList1, barList1, term, interval1, interval2));
                                else if (condition == "5 Bar Window") inputs.AddRange(createWindowFeatures(5, timeList1, barList1, term, interval1, interval2));
                                else if (condition == "8 Bar Window") inputs.AddRange(createWindowFeatures(8, timeList1, barList1, term, interval1, interval2));
                                else if (condition == "13 Bar Window") inputs.AddRange(createWindowFeatures(13, timeList1, barList1, term, interval1, interval2));
                                else if (condition == "21 Bar Window") inputs.AddRange(createWindowFeatures(21, timeList1, barList1, term, interval1, interval2));
                                else if (condition == "34 Bar Window") inputs.AddRange(createWindowFeatures(34, timeList1, barList1, term, interval1, interval2));
                                else if (condition == "55 Bar Window") inputs.AddRange(createWindowFeatures(55, timeList1, barList1, term, interval1, interval2));
                                else if (condition == "2 Bar Window 1 Ago") inputs.AddRange(createWindowFeatures(2, timeList1, barList1, term, interval1, interval2, 1));
                                else if (condition == "3 Bar Window 1 Ago") inputs.AddRange(createWindowFeatures(3, timeList1, barList1, term, interval1, interval2, 1));
                                else if (condition == "5 Bar Window 1 Ago") inputs.AddRange(createWindowFeatures(5, timeList1, barList1, term, interval1, interval2, 1));
                                else if (condition == "8 Bar Window 1 Ago") inputs.AddRange(createWindowFeatures(8, timeList1, barList1, term, interval1, interval2, 1));
                                else if (condition == "13 Bar Window 1 Ago") inputs.AddRange(createWindowFeatures(13, timeList1, barList1, term, interval1, interval2, 1));
                                else if (condition == "21 Bar Window 1 Ago") inputs.AddRange(createWindowFeatures(21, timeList1, barList1, term, interval1, interval2, 1));
                                else if (condition == "34 Bar Window 1 Ago") inputs.AddRange(createWindowFeatures(34, timeList1, barList1, term, interval1, interval2, 1));
                                else if (condition == "55 Bar Window 1 Ago") inputs.AddRange(createWindowFeatures(55, timeList1, barList1, term, interval1, interval2, 1));
                                else
                                {
                                    var results = calculateInput(condition, ticker, (term == "Mid Term") ? interval2 : interval1, barCount, timeList1, barList1, referenceData);
                                    if (term == "Mid Term")
                                    {
                                        results = results.Select(x => atm.sync(x, interval2, interval1, times2, times1)).ToList();
                                    }
                                    inputs.AddRange(results);
                                }
                            }
                            catch (Exception x)
                            {
                                var bp = true;
                            }
                        }
                    }

                    Dictionary<string, List<DateTime>> timeList2 = new Dictionary<string, List<DateTime>>();
                    timeList2[interval1] = times1;
                    timeList2[interval2] = times2;
                    Dictionary<string, Series[]> barList2 = new Dictionary<string, Series[]>();
                    barList2[interval1] = bars1;
                    barList2[interval2] = bars2;

                    Series val = calculateOutput(senario, interval1, timeList2, barList2, out bias);

                    double splitValue = double.Parse(split.Substring(0, 2)) / 100.00;

                    int period = (int)(count * splitValue); // 400 by default

                    int ix1 = barCount - count;
                    int ix2 = barCount - (count - period);
                    int ix3 = barCount - (count - period);
                    int ix4 = barCount;

                    // control the lookback
                    if (train && group && useCountAndOffset)
                    //if (false)

                    {
                        int end = barCount - (count - period);
                        double minVal = double.MaxValue;
                        int minIdx = end;
                        for (int ii = period; ii < end; ii++)
                        {
                            double momentum = Math.Abs(bars1[3][ii] - bars1[3][ii - period]);
                            if (momentum <= minVal)
                            {
                                minVal = momentum;
                                minIdx = ii;
                            }
                        }
            
                        ix1 = Math.Max(0, minIdx - period);
                        ix2 = Math.Max(0, minIdx);
                        Debug.WriteLine("Traing range " + times1[ix1].ToString("MMM dd, yyyy") + " " + times1[ix2].ToString("MMM dd, yyyy") + " " + bars1[3][ix1].ToString("0.00") + " " + bars1[3][ix2].ToString("0.00"));
                    }

                    for (int ii = 0; ii < barCount; ii++)
                    {
                        var ok1 = (!useCountAndOffset && date1 < times1[ii] && times1[ii] <= date2);
                        var ok2 = (useCountAndOffset && ((ii >= ix1 && ii < ix2) || (ii >= ix3 && ii < ix4)));

                        if (ok1 || ok2)
                        {
                            if (!train || !double.IsNaN(val[ii]))
                            {
                                var row = new List<string>();
                                //row.Add(train ? val[ii].ToString() : "0");   // Output
                                row.Add( val[ii].ToString());   // Output
                                row.Add(ticker);                            // Input1
                                row.Add(interval + ";" + times1[ii].ToString("yyyyMMddHHmmss"));  // input2
                                for (int jj = 0; jj < inputs.Count; jj++)
                                { 
                                    row.Add(inputs[jj][ii].ToString());
                                }

                                data.Add(row);
                            }
                        }
                    }
                }
            }


            output.bias = bias;
            output.data = data.OrderBy(x => x[2]).ToList();

            return output;
        }

        private static List<Series> calculateInput(string name, string ticker, string interval, int barCount, Dictionary<string, List<DateTime>> times, Dictionary<string, Series[]> bars, Dictionary<string, object> referenceData)
        {
            var output = new List<Series>();

            var interval1 = interval;
            var interval2 = getInterval(interval, 1);
            string[] intervalList = { interval1, interval2 };

            var indicator = Conditions.getIndicator(name);

            var calculated = false;
            if (indicator != null)
            {
                var op = bars[interval1][0];
                var hi = bars[interval1][1];
                var lo = bars[interval1][2];
                var cl = bars[interval1][3];

                var parameters = indicator.Parameters;

                if (indicator.Name == "BOL")
                {
                    var type = (parameters["MAType"] as ChoiceParameter).Value;
                    var period = (parameters["Period"] as NumberParameter).Value;
                    var priceType = (parameters["Price"] as ChoiceParameter).Value;
                    var stddev = (parameters["StdDev"] as NumberParameter).Value;
                    var price = Conditions.getPrice(priceType, op, hi, lo, cl);
                    output.AddRange(atm.calculateBollinger(type, price, (int)period, stddev));
                    calculated = true;
                }
                else if (indicator.Name == "MACD")
                {
                    var priceParm = parameters["Price"] as ChoiceParameter;
                    var maTypeParm = parameters["MAType"] as ChoiceParameter;
                    var periodParm1 = parameters["Period1"] as NumberParameter;
                    var periodParm2 = parameters["Period2"] as NumberParameter;
                    var periodParm3 = parameters["Period3"] as NumberParameter;
                    var colorParm1 = parameters["Color1"] as ColorParameter;
                    var colorParm2 = parameters["Color2"] as ColorParameter;

                    var maType = maTypeParm.Value;
                    var period1 = periodParm1.Value;
                    var period2 = periodParm2.Value;
                    var period3 = periodParm3.Value;

                    var price = Conditions.getPrice(priceParm.Value, op, hi, lo, cl);

                    Series ma1 = atm.calculateMovingAvg(maType, price, (int)period1);
                    Series ma2 = atm.calculateMovingAvg(maType, price, (int)period2);
                    Series osc = ma1 - ma2;
                    Series signal = atm.calculateMovingAvg(maType, osc, (int)period3);
                    output.Add(osc);
                    output.Add(signal);
                    calculated = true;
                }
                else if (indicator.Name == "DMI")
                {
                    var maTypeParm = parameters["MAType"] as ChoiceParameter;
                    var periodParm = parameters["Period"] as NumberParameter;
 
                    var maType = maTypeParm.Value;
                    var period = periodParm.Value;

                    output.AddRange(atm.calculateDMI(hi, lo, cl, maType, (int)period));
                    calculated = true;
                }
            }
            if (!calculated)
            {
                output.Add(Conditions.Calculate(name, ticker, intervalList, barCount, times, bars, referenceData));
            }

            return output;
        }

        public static Series calculateFibPivotR3(Series[] bars)
        {
            Series hi = bars[1];
            Series lo = bars[2];
            Series cl = bars[3];
            var output = new Series(cl.Count);
            var barCount = cl.Count;

            for (int ii = 1; ii < barCount; ii++)
            {
                var p = (hi[ii - 1] + lo[ii - 1] + cl[ii - 1]) / 3;
                var r3 = p + (1.0 * (hi[ii - 1] - lo[ii - 1]));
                output[ii] = r3;
            }
            return output;
        }

        public static Series calculateFibPivotR2(Series[] bars)
        {
            Series hi = bars[1];
            Series lo = bars[2];
            Series cl = bars[3];
            var output = new Series(cl.Count);
            var barCount = cl.Count;

            for (int ii = 1; ii < barCount; ii++)
            {
                var p = (hi[ii - 1] + lo[ii - 1] + cl[ii - 1]) / 3;
                var r2 = p + (0.618 * (hi[ii - 1] - lo[ii - 1]));
                output[ii] = r2;
            }
            return output;
        }

        public static Series calculateFibPivotR1(Series[] bars)
        {
            Series hi = bars[1];
            Series lo = bars[2];
            Series cl = bars[3];
            var output = new Series(cl.Count);
            var barCount = cl.Count;

            for (int ii = 1; ii < barCount; ii++)
            {
                var p = (hi[ii - 1] + lo[ii - 1] + cl[ii - 1]) / 3;
                var r1 = p + (0.382 * (hi[ii - 1] - lo[ii - 1]));
                output[ii] = r1;
            }
            return output;
        }

        public static Series calculateFibPivot(Series[] bars)
        {
            Series hi = bars[1];
            Series lo = bars[2];
            Series cl = bars[3];
            var output = new Series(cl.Count);
            var barCount = cl.Count;

            for (int ii = 1; ii < barCount; ii++)
            {
                var p = (hi[ii - 1] + lo[ii - 1] + cl[ii - 1]) / 3;
                output[ii] = p;
            }
            return output;
        }

        public static Series calculateFibPivotS1(Series[] bars)
        {
            Series hi = bars[1];
            Series lo = bars[2];
            Series cl = bars[3];
            var output = new Series(cl.Count);
            var barCount = cl.Count;

            for (int ii = 1; ii < barCount; ii++)
            {
                var p = (hi[ii - 1] + lo[ii - 1] + cl[ii - 1]) / 3;
                var s1 = p - (0.382 * (hi[ii - 1] - lo[ii - 1]));
                output[ii] = s1;
            }
            return output;
        }

        public static Series calculateFibPivotS2(Series[] bars)
        {
            Series hi = bars[1];
            Series lo = bars[2];
            Series cl = bars[3];
            var output = new Series(cl.Count);
            var barCount = cl.Count;

            for (int ii = 1; ii < barCount; ii++)
            {
                var p = (hi[ii - 1] + lo[ii - 1] + cl[ii - 1]) / 3;
                var s2 = p - (0.618 * (hi[ii - 1] - lo[ii - 1]));
                output[ii] = s2;
            }
            return output;
        }

        public static Series calculateFibPivotS3(Series[] bars)
        {
            Series hi = bars[1];
            Series lo = bars[2];
            Series cl = bars[3];
            var output = new Series(cl.Count);
            var barCount = cl.Count;

            for (int ii = 1; ii < barCount; ii++)
            {
                var p = (hi[ii - 1] + lo[ii - 1] + cl[ii - 1]) / 3;
                var s3 = p - (1.0 * (hi[ii - 1] - lo[ii - 1]));
                output[ii] = s3;
            }
            return output;
        }

        public static Series calculatePivotR3(Series[] bars)
        {
            Series hi = bars[1];
            Series lo = bars[2];
            Series cl = bars[3];
            var output = new Series(cl.Count);
            var barCount = cl.Count;

            for (int ii = 1; ii < barCount; ii++)
            {
                var p = (hi[ii - 1] + lo[ii - 1] + cl[ii - 1]) / 3;
                var r3 = hi[ii - 1] + (2 * (p - lo[ii - 1]));
                output[ii] = r3;
            }
            return output;
        }

        public static Series calculatePivotR2(Series[] bars)
        {
            Series hi = bars[1];
            Series lo = bars[2];
            Series cl = bars[3];
            var output = new Series(cl.Count);
            var barCount = cl.Count;

            for (int ii = 1; ii < barCount; ii++)
            {
                var p = (hi[ii - 1] + lo[ii - 1] + cl[ii - 1]) / 3;
                var r2 = p + (hi[ii - 1] - lo[ii - 1]);
                output[ii] = r2;
            }
            return output;
        }

        public static Series calculatePivotR1(Series[] bars)
        {
            Series hi = bars[1];
            Series lo = bars[2];
            Series cl = bars[3];
            var output = new Series(cl.Count);
            var barCount = cl.Count;

            for (int ii = 1; ii < barCount; ii++)
            {
                var p = (hi[ii - 1] + lo[ii - 1] + cl[ii - 1]) / 3;
                var r1 = (p * 2) - lo[ii - 1];
                output[ii] = r1;
            }
            return output;
        }

        public static Series calculatePivot(Series[] bars)
        {
            Series hi = bars[1];
            Series lo = bars[2];
            Series cl = bars[3];
            var output = new Series(cl.Count);
            var barCount = cl.Count;

            for (int ii = 1; ii < barCount; ii++)
            {
                var p = (hi[ii - 1] + lo[ii - 1] + cl[ii - 1]) / 3;
                output[ii] = p;
            }
            return output;
        }
        public static Series calculatePivotS1(Series[] bars)
        {
            Series hi = bars[1];
            Series lo = bars[2];
            Series cl = bars[3];
            var output = new Series(cl.Count);
            var barCount = cl.Count;

            for (int ii = 1; ii < barCount; ii++)
            {
                var p = (hi[ii - 1] + lo[ii - 1] + cl[ii - 1]) / 3;
                var s1 = (p * 2) - hi[ii - 1];
                output[ii] = s1;
            }
            return output;
        }

        public static Series calculatePivotS2(Series[] bars)
        {
            Series hi = bars[1];
            Series lo = bars[2];
            Series cl = bars[3];
            var output = new Series(cl.Count);
            var barCount = cl.Count;

            for (int ii = 1; ii < barCount; ii++)
            {
                var p = (hi[ii - 1] + lo[ii - 1] + cl[ii - 1]) / 3;
                var s2 = p - (hi[ii - 1] - lo[ii - 1]);
                output[ii] = s2;
            }
            return output;
        }
        public static Series calculatePivotS3(Series[] bars)
        {
            Series hi = bars[1];
            Series lo = bars[2];
            Series cl = bars[3];
            var output = new Series(cl.Count);
            var barCount = cl.Count;

            for (int ii = 1; ii < barCount; ii++)
            {
                var p = (hi[ii - 1] + lo[ii - 1] + cl[ii - 1]) / 3;
                var s3 = (lo[ii - 1] - (2 * (hi[ii - 1] - p)));
                output[ii] = s3;
            }
            return output;
        }

        private static Series calculateOutput(Senario senario, string interval, Dictionary<string, List<DateTime>> times, Dictionary<string, Series[]> bars, out double bias)
        {
            bias = 0;

            var interval1 = interval;
            var interval2 = getInterval(interval, 1);

            Series[] bars1 = bars[interval1];
            Series op1 = bars1[0];
            Series hi1 = bars1[1];
            Series lo1 = bars1[2];
            Series cl1 = bars1[3];
            Series val = cl1;
        
            if (senario == Senario.ATR50Less5)
            {
                Series tr = Series.TrueRange(bars1[1], bars1[2], bars1[3]);
                Series atr = Series.SimpleMovingAverage(tr, 50).ShiftRight(1);
                val = tr <= atr * 0.5;
            }

            else if (senario == Senario.ATR505to1)
            {
                Series tr = Series.TrueRange(bars1[1], bars1[2], bars1[3]);
                Series atr = Series.SimpleMovingAverage(tr, 50).ShiftRight(1);
                val = (tr > atr * 0.5) & (tr < atr * 1.0);
            }

            else if (senario == Senario.ATR501to15)
            {
                Series tr = Series.TrueRange(bars1[1], bars1[2], bars1[3]);
                Series atr = Series.SimpleMovingAverage(tr, 50).ShiftRight(1);
                val = (tr > atr * 1.0) & (tr < atr * 1.5);
            }

            else if (senario == Senario.ATR50Greater15)
            {
                Series tr = Series.TrueRange(bars1[1], bars1[2], bars1[3]);
                Series atr = Series.SimpleMovingAverage(tr, 50).ShiftRight(1);
                val = tr >= atr * 1.5;
            }
            else if (senario == Senario.ATR30Less5)
            {
                Series tr = Series.TrueRange(bars1[1], bars1[2], bars1[3]);
                Series atr = Series.SimpleMovingAverage(tr, 30).ShiftRight(1);
                val = tr <= atr * 0.5;
            }

            else if (senario == Senario.ATR305to1)
            {
                Series tr = Series.TrueRange(bars1[1], bars1[2], bars1[3]);
                Series atr = Series.SimpleMovingAverage(tr, 30).ShiftRight(1);
                val = (tr > atr * 0.5) & (tr < atr * 1.0);
            }

            else if (senario == Senario.ATR301to15)
            {
                Series tr = Series.TrueRange(bars1[1], bars1[2], bars1[3]);
                Series atr = Series.SimpleMovingAverage(tr, 30).ShiftRight(1);
                val = (tr > atr * 1.0) & (tr < atr * 1.5);
            }

            else if (senario == Senario.ATR30Greater15)
            {
                Series tr = Series.TrueRange(bars1[1], bars1[2], bars1[3]);
                Series atr = Series.SimpleMovingAverage(tr, 30).ShiftRight(1);
                val = tr >= atr * 1.5;
            }
            else if (senario == Senario.ATR20Less5)
            {
                Series tr = Series.TrueRange(bars1[1], bars1[2], bars1[3]);
                Series atr = Series.SimpleMovingAverage(tr, 20).ShiftRight(1);
                val = tr <= atr * 0.5;
            }

            else if (senario == Senario.ATR205to1)
            {
                Series tr = Series.TrueRange(bars1[1], bars1[2], bars1[3]);
                Series atr = Series.SimpleMovingAverage(tr, 20).ShiftRight(1);
                val = (tr > atr * 0.5) & (tr < atr * 1.0);
            }

            else if (senario == Senario.ATR201to15)
            {
                Series tr = Series.TrueRange(bars1[1], bars1[2], bars1[3]);
                Series atr = Series.SimpleMovingAverage(tr, 20).ShiftRight(1);
                val = (tr > atr * 1.0) & (tr < atr * 1.5);
            }

            else if (senario == Senario.ATR20Greater15)
            {
                Series tr = Series.TrueRange(bars1[1], bars1[2], bars1[3]);
                Series atr = Series.SimpleMovingAverage(tr, 20).ShiftRight(1);
                val = tr >= atr * 1.5;
            }
            else if (senario == Senario.ATR10Less5)
            {
                Series tr = Series.TrueRange(bars1[1], bars1[2], bars1[3]);
                Series atr = Series.SimpleMovingAverage(tr, 10).ShiftRight(1);
                val = tr <= atr * 0.5;
            }

            else if (senario == Senario.ATR105to1)
            {
                Series tr = Series.TrueRange(bars1[1], bars1[2], bars1[3]);
                Series atr = Series.SimpleMovingAverage(tr, 10).ShiftRight(1);
                val = (tr > atr * 0.5) & (tr < atr * 1.0);
            }

            else if (senario == Senario.ATR101to15)
            {
                Series tr = Series.TrueRange(bars1[1], bars1[2], bars1[3]);
                Series atr = Series.SimpleMovingAverage(tr, 10).ShiftRight(1);
                val = (tr > atr * 1.0) & (tr < atr * 1.5);
            }

            else if (senario == Senario.ATR10Greater15)
            {
                Series tr = Series.TrueRange(bars1[1], bars1[2], bars1[3]);
                Series atr = Series.SimpleMovingAverage(tr, 10).ShiftRight(1);
                val = tr >= atr * 1.5;
            }
            else if (senario == Senario.ATR5Less5)
            {
                Series tr = Series.TrueRange(bars1[1], bars1[2], bars1[3]);
                Series atr = Series.SimpleMovingAverage(tr, 5).ShiftRight(1);
                val = tr <= atr * 0.5;
            }

            else if (senario == Senario.ATR55to1)
            {
                Series tr = Series.TrueRange(bars1[1], bars1[2], bars1[3]);
                Series atr = Series.SimpleMovingAverage(tr, 5).ShiftRight(1);
                val = (tr > atr * 0.5) & (tr < atr * 1.0);
            }

            else if (senario == Senario.ATR51to15)
            {
                Series tr = Series.TrueRange(bars1[1], bars1[2], bars1[3]);
                Series atr = Series.SimpleMovingAverage(tr, 5).ShiftRight(1);
                val = (tr > atr * 1.0) & (tr < atr * 1.5);
            }

            else if (senario == Senario.ATR5Greater15)
            {
                Series tr = Series.TrueRange(bars1[1], bars1[2], bars1[3]);
                Series atr = Series.SimpleMovingAverage(tr, 5).ShiftRight(1);
                val = tr >= atr * 1.5;
            }
            else if (senario == Senario.TrendOp11)
            {
                val = calculateTrendDay11(senario, bars1);
            }

            else if (senario == Senario.TrendCl11)
            {
                val = calculateTrendDay11(senario, bars1);
            }

            else if (senario == Senario.TrendOpCl11)
            {
                val = calculateTrendDay11(senario, bars1);
            }

            else if (senario == Senario.TrendOp)
            {
                val = calculateTrendDay(senario, bars1);
            }

            else if (senario == Senario.TrendCl)
            {
                val = calculateTrendDay(senario, bars1);
            }

            else if (senario == Senario.TrendOpCl)
            {
                val = calculateTrendDay(senario, bars1);
            }

            else if (senario == Senario.TrendOp31)
            {
                val = calculateTrendDay31(senario, bars1);
            }

            else if (senario == Senario.TrendCl31)
            {
                val = calculateTrendDay31(senario, bars1);
            }

            else if (senario == Senario.TrendOpCl31)
            {
                val = calculateTrendDay31(senario, bars1);
            }

            else
            {
                var label = MainView.GetSenarioLabel(senario);
                var text1 = label.Trim();
                var index1 = text1.IndexOf("|");
                if (index1 != -1)
                {
                    var forecastPrice = text1.Substring(0, 1).Replace("O", "Open").Replace("H", "High").Replace("L", "Low").Replace("C", "Close");
                    var referencePrice = text1.Substring(index1 + 2, 1).Replace("O", "Open").Replace("H", "High").Replace("L", "Low").Replace("C", "Close");
                    var forecastIndex = int.Parse(text1.Substring(index1 - 2, 1));
                    var referenceIndex = int.Parse(text1.Substring(text1.Length - 1, 1)) - 1;

                    var fp = cl1;
                    if (forecastPrice == "Open") fp = op1;
                    else if (forecastPrice == "High") fp = hi1;
                    else if (forecastPrice == "Low") fp = lo1;

                    var rp = cl1;
                    if (referencePrice == "Open") rp = op1;
                    else if (referencePrice == "High") rp = hi1;
                    else if (referencePrice == "Low") rp = lo1;

                    var fPrice = fp.ShiftLeft(forecastIndex);
                    var rPrice = rp.ShiftRight(referenceIndex);

                    // calculate bias amount
                    var amounts =  fPrice - rPrice;
                    amounts.Data.Sort();
                    bias = amounts[(int)(amounts.Count * 0.50)];  //increasing number bias is down
                    //bias = 0.0; 

                    val = fPrice >= rPrice + bias;
                }
            }

            return val;
        }

        static public Series calculateTrendDay11(Senario senario, Series[] bars)
        {
            Series op1 = bars[0];
            Series hi1 = bars[1];
            Series lo1 = bars[2];
            Series cl1 = bars[3];

            Series nxtop1 = op1.ShiftLeft(1);
            Series nxthi1 = hi1.ShiftLeft(1);
            Series nxtlo1 = lo1.ShiftLeft(1);
            Series nxtcl1 = cl1.ShiftLeft(1);

            Series output = null;

            if (senario == Senario.TrendOp11)
            {
                output = ((nxtop1 - nxtlo1) <= ((nxthi1 - nxtlo1) * .10)) | ((nxthi1 - nxtop1) <= ((nxthi1 - nxtlo1) * .10));
            }

            else if (senario == Senario.TrendCl11)
            {
                output = ((nxtcl1 - nxtlo1) <= ((nxthi1 - nxtlo1) * .10)) | ((nxthi1 - nxtcl1) <= ((nxthi1 - nxtlo1) * .10));
            }

            else if (senario == Senario.TrendOpCl11)
            {
                var rng = (nxthi1 - nxtlo1) * .10;
                output = ((nxtop1 - nxtlo1) <= rng & (nxthi1 - nxtcl1) <= rng) | ((nxthi1 - nxtop1) <= rng & (nxtcl1 - nxtlo1) <= rng);
            }
            return output;
        }

        static public Series calculateTrendDay(Senario senario, Series[] bars)
        {
            Series op1 = bars[0];
            Series hi1 = bars[1];
            Series lo1 = bars[2];
            Series cl1 = bars[3];

            Series nxtop1 = op1.ShiftLeft(1);
            Series nxthi1 = hi1.ShiftLeft(1);
            Series nxtlo1 = lo1.ShiftLeft(1);
            Series nxtcl1 = cl1.ShiftLeft(1);

            Series output = null;

            if (senario == Senario.TrendOp)
            {
                output = ((nxtop1 - nxtlo1) <= ((nxthi1 - nxtlo1) * .20)) | ((nxthi1 - nxtop1) <= ((nxthi1 - nxtlo1) * .20));
            }

            else if (senario == Senario.TrendCl)
            {
                output = ((nxtcl1 - nxtlo1) <= ((nxthi1 - nxtlo1) * .20)) | ((nxthi1 - nxtcl1) <= ((nxthi1 - nxtlo1) * .20));
            }

            else if (senario == Senario.TrendOpCl)
            {
                var rng = (nxthi1 - nxtlo1) * .20;
                output = ((nxtop1 - nxtlo1) <= rng & (nxthi1 - nxtcl1) <= rng) | ((nxthi1 - nxtop1) <= rng & (nxtcl1 - nxtlo1) <= rng);
            }
            return output;
        }

        static public Series calculateTrendDay31(Senario senario, Series[] bars)
        {
            Series op1 = bars[0];
            Series hi1 = bars[1];
            Series lo1 = bars[2];
            Series cl1 = bars[3];

            Series nxtop1 = op1.ShiftLeft(1);
            Series nxthi1 = hi1.ShiftLeft(1);
            Series nxtlo1 = lo1.ShiftLeft(1);
            Series nxtcl1 = cl1.ShiftLeft(1);

            Series output = null;

            if (senario == Senario.TrendOp31)
            {
                output = ((nxtop1 - nxtlo1) <= ((nxthi1 - nxtlo1) * .30)) | ((nxthi1 - nxtop1) <= ((nxthi1 - nxtlo1) * .30));
            }

            else if (senario == Senario.TrendCl31)
            {
                output = ((nxtcl1 - nxtlo1) <= ((nxthi1 - nxtlo1) * .30)) | ((nxthi1 - nxtcl1) <= ((nxthi1 - nxtlo1) * .30));
            }

            else if (senario == Senario.TrendOpCl31)
            {
                var rng = (nxthi1 - nxtlo1) * .30;
                output = ((nxtop1 - nxtlo1) <= rng & (nxthi1 - nxtcl1) <= rng) | ((nxthi1 - nxtop1) <= rng & (nxtcl1 - nxtlo1) <= rng);
            }
            return output;
        }

        static Series convertToAboveBelowMean(Series input)
        {
            var output = input;
            if (input.Count > 0)
            {
                var sortedReturns = input.Data.OrderBy(x => x).ToList();
                int index = sortedReturns.Count / 2;
                double median = sortedReturns[index];
                var result = input.Data.Select(x => (x >= median) ? 1.0 : 0.0).ToList();
                output = new Series(result);
            }
            return output;
        }
    }
}

