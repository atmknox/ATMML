using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace ATMML
{
    enum TradeState
    {
        Off,
        New,
        Current,
        Reduce,
        Older,
        Exit,
        NewBullish,
        NewBearish,
        CurrentBullish,
        CurrentBearish
    }

    class Result
    {
        public Result()
        {
            LTft = new int[2];
            MTft = new int[2];
            STft = new int[2];
            LTst = new int[2];
            MTst = new int[2];
            STst = new int[2];
            LTsc = new int[2];
            MTsc = new int[2];
            STsc = new int[2];
            LTFT = new int[2];
            MTFT = new int[2];
            STFT = new int[2];
            LTST = new int[2];
            MTST = new int[2];
            STST = new int[2];
            LTFTTurn = new int[2];
            MTFTTurn = new int[2];
            STFTTurn = new int[2];
            LTSTTurn = new int[2];
            MTSTTurn = new int[2];
            STSTTurn = new int[2];
            STpr = new double[2];
            MTpr = new double[2];
            ATRX = new int[2];
            Prediction = "";
            PositionColor = Colors.Transparent;
            Add1UpColor = Colors.Transparent;
            Add1DnColor = Colors.Transparent;
            Add2UpColor = Colors.Transparent;
            Add2DnColor = Colors.Transparent;
            Add3UpColor = Colors.Transparent;
            Add3DnColor = Colors.Transparent;
            ShortRetUpColor = Colors.Transparent;
            LongRetDnColor = Colors.Transparent;
            ExhUpColor = Colors.Transparent;
            ExhDnColor = Colors.Transparent;
            PositionUpColor = Colors.Transparent;
            PositionDnColor = Colors.Transparent;
            Add1UpColorNxt = Colors.Transparent;
            Add1DnColorNxt = Colors.Transparent;
            Add2UpColorNxt = Colors.Transparent;
            Add2DnColorNxt = Colors.Transparent;
            Add3UpColorNxt = Colors.Transparent;
            Add3DnColorNxt = Colors.Transparent;
            ShortRetUpColorNxt = Colors.Transparent;
            LongRetDnColorNxt = Colors.Transparent;
            ExhUpColorNxt = Colors.Transparent;
            ExhDnColorNxt = Colors.Transparent;
            PositionUpColor = Colors.Transparent;
            PositionDnColor = Colors.Transparent;
        }

        public string Prediction { get; set; }
        public TradeState TSBShort { get; set; }
        public TradeState TSBLong { get; set; }
        public int[] LTFT { get; set; }
        public int[] MTFT { get; set; }
        public int[] STFT { get; set; }
        public int[] LTST { get; set; }
        public int[] MTST { get; set; }
        public int[] STST { get; set; }
        public int[] LTFTTurn { get; set; }
        public int[] MTFTTurn { get; set; }
        public int[] STFTTurn { get; set; }
        public int[] LTSTTurn { get; set; }
        public int[] MTSTTurn { get; set; }
        public int[] STSTTurn { get; set; }
        public int[] LTft { get; set; }
        public int[] MTft { get; set; }
        public int[] STft { get; set; }
        public int[] LTst { get; set; }
        public double[] MTpr { get; set; }
        public double[] STpr { get; set; }
        public int[] MTst { get; set; }
        public int[] STst { get; set; }
        public int[] LTsc { get; set; }
        public int[] MTsc { get; set; }
        public int[] STsc { get; set; }
		public int[] ATRX { get; set; }
		public TradeState PRLong { get; set; }
        public TradeState PRShort { get; set; }
        public TradeState PRLongLT { get; set; }
        public TradeState PRShortLT { get; set; }
        public TradeState PRLongST { get; set; }
        public TradeState PRShortST { get; set; }
        public TradeState PATBLong { get; set; }
        public TradeState PATBShort { get; set; }
        public TradeState FTOB { get; set; }
        public TradeState FTOS { get; set; }
        public TradeState STFTOB { get; set; }
        public TradeState STFTOS { get; set; }
        public TradeState LTFTOB { get; set; }
        public TradeState LTFTOS { get; set; }
        public TradeState MTSTOB { get; set; }
        public TradeState MTSTOS { get; set; }
        public TradeState STSTOB { get; set; }
        public TradeState STSTOS { get; set; }
        public TradeState STFTGD { get; set; }
        public TradeState STFTGU { get; set; }
        public TradeState STFTTU { get; set; }
        public TradeState STFTTD { get; set; }
        public TradeState MTFTGD { get; set; }
        public TradeState MTFTGU { get; set; }
        public TradeState MTFTTU { get; set; }
        public TradeState MTFTTD { get; set; }
        public TradeState LTFTGD { get; set; }
        public TradeState LTFTGU { get; set; }
        public TradeState LTFTTU { get; set; }
        public TradeState LTFTTD { get; set; }
        public TradeState STSTGD { get; set; }
        public TradeState STSTGU { get; set; }
        public TradeState STSTTU { get; set; }
        public TradeState STSTTD { get; set; }
        public TradeState MTSTGD { get; set; }
        public TradeState MTSTGU { get; set; }
        public TradeState MTSTTU { get; set; }
        public TradeState MTSTTD { get; set; }
        public TradeState LTSTGD { get; set; }
        public TradeState LTSTGU { get; set; }
        public TradeState LTSTTU { get; set; }
        public TradeState LTSTTD { get; set; }
        public TradeState TBarUpST { get; set; }
        public TradeState TBarDnST { get; set; }
        public TradeState TBarUpMT { get; set; }
        public TradeState TBarDnMT { get; set; }
        public TradeState TBarUpLT { get; set; }
        public TradeState TBarDnLT { get; set; }
        public TradeState TLineUpST { get; set; }
        public TradeState TLineDnST { get; set; }
        public TradeState TLineUpMT { get; set; }
        public TradeState TLineDnMT { get; set; }
        public TradeState TLineUpLT { get; set; }
        public TradeState TLineDnLT { get; set; }
        public TradeState PAShort { get; set; }
        public TradeState PALong { get; set; }
        public TradeState EXHPRLong { get; set; }
        public TradeState EXHPRShort { get; set; }
        public TradeState EXHLong { get; set; }
        public TradeState EXHShort { get; set; }
        public TradeState SFTPRLong { get; set; }
        public TradeState SFTPRShort { get; set; }
        public TradeState TBShort { get; set; }
        public TradeState TBLong { get; set; }
        public TradeState PXFShort { get; set; }
        public TradeState PXFLong { get; set; }
        public TradeState PXFltShort { get; set; }
        public TradeState PXFltLong { get; set; }
        public TradeState stPxfLong { get; set; }
		public TradeState stUnitLong { get; set; }
		public TradeState stPxfShort { get; set; }
		public TradeState stUnitShort { get; set; }
		public TradeState mtPxfLong { get; set; }
        public TradeState mtPxfShort { get; set; }
        public TradeState ltPxfLong { get; set; }
        public TradeState ltPxfShort { get; set; }
        public TradeState stVofLong { get; set; }
        public TradeState stVofShort { get; set; }
        public TradeState mtVofLong { get; set; }
        public TradeState mtVofShort { get; set; }
        public TradeState ltVofLong { get; set; }
        public TradeState ltVofShort { get; set; }
        public TradeState PXFmtShort { get; set; }
        public TradeState PXFmtLong { get; set; }
        public TradeState TSBltLong { get; set; } // used
        public TradeState TSBltShort { get; set; }
        public TradeState TSBmtLong { get; set; }
        public TradeState TSBmtShort { get; set; }
        public TradeState TSBstLong { get; set; }
        public TradeState TSBstShort { get; set; }
        public TradeState LTSTOB { get; set; }
        public TradeState LTSTOS { get; set; }
        public TradeState PXFstShort { get; set; }
        public TradeState PXFstLong { get; set; }
        public TradeState SFTLong { get; set; }
        public TradeState SFTShort { get; set; }

        public int ltCorrectCount { get; set; }
        public int ltTotalCount { get; set; }
        public int mtCorrectCount { get; set; }
        public int mtTotalCount { get; set; }
        public int stCorrectCount { get; set; }
        public int stTotalCount { get; set; }
        public TradeState longAdvice { get; set; }
        public TradeState shortAdvice { get; set; }

        public Color PositionColor { get; set; }
        public Color PositionUpColor { get; set; }
        public Color PositionDnColor { get; set; }
        public Color Add1UpColor { get; set; }
        public Color Add1DnColor { get; set; }
        public Color Add1UpColorNxt { get; set; }
        public Color Add1DnColorNxt { get; set; }
        public Color Add2UpColor { get; set; }
        public Color Add2DnColor { get; set; }
        public Color Add2UpColorNxt { get; set; }
        public Color Add2DnColorNxt { get; set; }
        public Color Add3UpColor { get; set; }
        public Color Add3DnColor { get; set; }
        public Color Add3UpColorNxt { get; set; }
        public Color Add3DnColorNxt { get; set; }
        public Color ShortRetUpColor { get; set; }
        public Color LongRetDnColor { get; set; }
        public Color ShortRetUpColorNxt { get; set; }
        public Color LongRetDnColorNxt { get; set; }
        public Color ExhUpColor { get; set; }
        public Color ExhDnColor { get; set; }
        public Color ExhUpColorNxt { get; set; }
        public Color ExhDnColorNxt { get; set; }
    }
}
