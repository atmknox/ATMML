using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ATMML
{
    public class AOD1Input
    {
        public string Symbol = "";
        public string Interval = "";
        public string Senario = "";
        public int ShortTermIndex = 0;
        public int MidTermIndex = 0;
        public List<DateTime> ShortTermTimes = new List<DateTime>();
        public List<DateTime> MidTermTimes = new List<DateTime>();
        public List<DateTime> LongTermTimes = new List<DateTime>();
        public Series[] ShortTermSeries = new Series[4];
        public Series[] MidTermSeries = new Series[4];
        public Series[] LongTermSeries = new Series[4];
        public Series[] ShortTermIndexSeries = new Series[4];
        public Series[] MidTermIndexSeries = new Series[4];
        public Dictionary<DateTime, double> ShortTermPredictions = new Dictionary<DateTime, double>();
        public Dictionary<DateTime, double> ShortTermActuals = new Dictionary<DateTime, double>();
        public Dictionary<DateTime, double> MidTermPredictions = new Dictionary<DateTime, double>();
        public Dictionary<DateTime, double> MidTermActuals = new Dictionary<DateTime, double>();
    }

    public partial class AOD1 : UserControl
    {
   	public event OrderEventHandler OrderEvent;

        private Dictionary<DateTime, double> ShortTermPredictions = new Dictionary<DateTime, double>();
        private Dictionary<DateTime, double> ShortTermActuals = new Dictionary<DateTime, double>();
        private Dictionary<DateTime, double> MidTermPredictions = new Dictionary<DateTime, double>();
        private Dictionary<DateTime, double> MidTermActuals = new Dictionary<DateTime, double>();

        public AOD1()
        {
            DataContext = this;
            InitializeComponent();
        }

        private void sendOrderEvent(OrderEventArgs e)
        {
            if (OrderEvent != null)
            {
                OrderEvent(this, e);
            }
        }

        private double _truePositive;
        private double _falsePositive;
        private double _falseNegative;
        private double _trueNegative;
        private double _totalPositive;
        private double _totalNegative;
        private double _actualPositive;
        private double _actualNegative;
        private double _accuracy;
        private double _errorRate;
        private double _sensitivity;
        private double _falsePosPer;

        private double _specificity;
        private double _prevalence;
        private double _nullError;
        private double _precision;
        private double _f1;
        private double _probability;
        private double _probabilityYes;
        private double _probabilityNo;
        private double _probabilityRandom;
        private double _positivePredictiveRate;
        private double _negativePredictiveRate;
        private void updateMatrix(List<DateTime> times)
        {
            var falsePositive = 0;
            var falseNegative = 0;
            var truePositive = 0;
            var trueNegative = 0;

            for (int ii = 0; ii < times.Count; ii++)
            {
                var prediction = double.NaN;
                var actual = double.NaN;
                if (ShortTermPredictions.ContainsKey(times[ii]))
                {
                    prediction = ShortTermPredictions[times[ii]];
                }
                if (ShortTermActuals.ContainsKey(times[ii]))
                {
                    actual = ShortTermActuals[times[ii]];
                }

                if (!double.IsNaN(prediction) && !double.IsNaN(actual))
                {
                    if (actual > 0.5 && prediction > 0.5) truePositive++;
                    if (actual < 0.5 && prediction < 0.5) trueNegative++;
                    if (actual < 0.5 && prediction > 0.5) falsePositive++;
                    if (actual > 0.5 && prediction < 0.5) falseNegative++;
                }
            }

            int total = truePositive + falsePositive + falseNegative + trueNegative;

            _truePositive = truePositive;
            _trueNegative = trueNegative;
            _falsePositive = falsePositive;
            _falseNegative = falseNegative;
            _totalPositive = truePositive + falsePositive;
            _totalNegative = trueNegative + falseNegative;
            _actualPositive = trueNegative + falsePositive;
            _actualNegative = truePositive + falseNegative;

            _accuracy = ((double)(truePositive + trueNegative) / total);
            _precision = (double)truePositive / (truePositive + falsePositive);
            _sensitivity = (double)truePositive / (truePositive + falseNegative);
            _specificity = (double)trueNegative / (trueNegative + falsePositive); 
            _falsePosPer = (double)falsePositive / (truePositive + falseNegative); 
            _positivePredictiveRate = (double)truePositive / (truePositive + falsePositive);
            _negativePredictiveRate = (double)trueNegative / (trueNegative + falseNegative);
            _prevalence = (double)(truePositive + falseNegative) / total;
            _errorRate = (double)(falsePositive + falseNegative) / total;
            _nullError = (double)(trueNegative + falsePositive) / total;
            _f1 = 2 * (_precision * _sensitivity) / (_precision + _sensitivity);
        }

        public void update(AOD1Input input)
        {
            // input
            Symbol = input.Symbol;
            ModelScenario = input.Senario;
            var interval0 = input.Interval;
            var interval1 = GetOverviewInterval(interval0, 1);
            var shortTermTimes = input.ShortTermTimes;
            var midTermTimes = input.MidTermTimes;
            var longTermTimes = input.LongTermTimes;
            var shortTermSeries = input.ShortTermSeries;
            var midTermSeries = input.MidTermSeries;
            var longTermSeries = input.LongTermSeries;
            var shortTermIndexSeries = input.ShortTermIndexSeries;
            var midTermIndexSeries = input.MidTermIndexSeries;
            int shortTermCurrentBarIndex = input.ShortTermIndex;
            int midTermCurrentBarIndex = input.MidTermIndex;

            ShortTermPredictions = input.ShortTermPredictions;
            ShortTermActuals = input.ShortTermActuals;
            MidTermPredictions = input.MidTermPredictions;
            MidTermActuals = input.MidTermActuals;

            if (ShortTermPredictions.Count > 0)
            {
                var stDateCurrent = shortTermTimes[shortTermCurrentBarIndex - 1];
                var stDateNext = shortTermTimes[shortTermCurrentBarIndex];
                ShortTermPredictionCurrent = ShortTermPredictions.ContainsKey(stDateCurrent) ? ShortTermPredictions[stDateCurrent] : 0;
                ShortTermPredictionNext = ShortTermPredictions.ContainsKey(stDateNext) ? ShortTermPredictions[stDateNext] : 0;
            }

            if (MidTermPredictions.Count > 0)
            {
                var mtDateCurrent = midTermTimes[midTermCurrentBarIndex - 1];
                var mtDateNext = midTermTimes[midTermCurrentBarIndex];
                MidTermPredictionCurrent = MidTermPredictions.ContainsKey(mtDateCurrent) ? MidTermPredictions[mtDateCurrent] : 0;
                MidTermPredictionNext = MidTermPredictions.ContainsKey(mtDateNext) ?  MidTermPredictions[mtDateNext] : 0;
            }

            ShortTermInterval = getIntervalAbbreviation(interval0);
            MidTermInterval = getIntervalAbbreviation(interval1);

            updateMatrix(shortTermTimes);

            if (shortTermTimes.Count > 0 && _calcST)
            {
                Time = shortTermTimes[shortTermCurrentBarIndex];

                _calcST = false;

                ShortTermOpen = shortTermSeries[0][shortTermCurrentBarIndex];
                ShortTermOpen1 = shortTermSeries[0][shortTermCurrentBarIndex - 1];
                ShortTermHigh = shortTermSeries[1][shortTermCurrentBarIndex];
                ShortTermHigh1 = shortTermSeries[1][shortTermCurrentBarIndex - 1];
                ShortTermLow = shortTermSeries[2][shortTermCurrentBarIndex];
                ShortTermLow1 = shortTermSeries[2][shortTermCurrentBarIndex - 1];
                ShortTermClose = shortTermSeries[3][shortTermCurrentBarIndex];
                ShortTermClose1 = shortTermSeries[3][shortTermCurrentBarIndex - 1];

                ShortTermTS = atm.getTSBSignals2(2, shortTermSeries[1], shortTermSeries[2], shortTermSeries[3]).Last();
                ShortTermTC = atm.GetTrendCondition(2, shortTermSeries[1], shortTermSeries[2], shortTermSeries[3]).Last();
                ShortTermTD = atm.GetTrendDirection(2, shortTermSeries[1], shortTermSeries[2], shortTermSeries[3]).Last();

                var ft_st = atm.calculateFT(shortTermSeries[1], shortTermSeries[2], shortTermSeries[3]);
                ShortTermFT = ft_st[shortTermCurrentBarIndex];
                ShortTermFT1 = ft_st[shortTermCurrentBarIndex - 1];
                ShortTermFT2 = ft_st[shortTermCurrentBarIndex - 2];

                var st_st = atm.calculateST(shortTermSeries[1], shortTermSeries[2], shortTermSeries[3]);
                ShortTermST = st_st[shortTermCurrentBarIndex];
                ShortTermST1 = st_st[shortTermCurrentBarIndex - 1];
                ShortTermST2 = st_st[shortTermCurrentBarIndex - 2];

                var ft_tp_st = atm.calculateFastTurningPoints(shortTermSeries[1], shortTermSeries[2], shortTermSeries[3], ft_st, shortTermCurrentBarIndex);
                var st_tp_st = atm.calculateSlowTurningPoints(shortTermSeries[1], shortTermSeries[2], shortTermSeries[3], st_st, shortTermCurrentBarIndex);
                var ftp_st_up = ft_tp_st[0];
                var stp_st_up = st_tp_st[0];
                var ftp_st_dn = ft_tp_st[1];
                var stp_st_dn = st_tp_st[1];

                ShortTermFTTPUpNext = ftp_st_up[shortTermCurrentBarIndex + 1];
                ShortTermFTTPUp = ftp_st_up[shortTermCurrentBarIndex];
                ShortTermFTTPDnNext = ftp_st_dn[shortTermCurrentBarIndex + 1];
                ShortTermFTTPDn = ftp_st_dn[shortTermCurrentBarIndex];
                ShortTermSTTPUpNext = stp_st_up[shortTermCurrentBarIndex + 1];
                ShortTermSTTPUp = stp_st_up[shortTermCurrentBarIndex];
                ShortTermSTTPDnNext = stp_st_dn[shortTermCurrentBarIndex + 1];
                ShortTermSTTPDn = stp_st_dn[shortTermCurrentBarIndex];

                var sig_st = atm.calculate3Sigma(shortTermSeries[1], shortTermSeries[2], shortTermSeries[3]);
                ShortTermSigUp1 = sig_st[2][shortTermCurrentBarIndex];
                ShortTermSigUp2 = sig_st[1][shortTermCurrentBarIndex];
                ShortTermSigUp3 = sig_st[0][shortTermCurrentBarIndex];
                ShortTermSigDn1 = sig_st[3][shortTermCurrentBarIndex];
                ShortTermSigDn2 = sig_st[4][shortTermCurrentBarIndex];
                ShortTermSigDn3 = sig_st[5][shortTermCurrentBarIndex];

                Series md_st = Series.Mid(shortTermSeries[1], shortTermSeries[2]);
                Dictionary<string, Series> trend_st = atm.calculateTrend(shortTermSeries[1], shortTermSeries[2], shortTermSeries[3], md_st);
                Series TLup_st = trend_st["TLup"];
                Series TLdn_st = trend_st["TLdn"];
                var target_st_up = atm.calculateUpperTargetLines(shortTermSeries[1], shortTermSeries[2], TLup_st, shortTermCurrentBarIndex);
                var target_st_dn = atm.calculateLowerTargetLines(shortTermSeries[1], shortTermSeries[2], TLdn_st, shortTermCurrentBarIndex);
                ShortTermTargetUp1 = target_st_up[0][shortTermCurrentBarIndex];
                ShortTermTargetUp2 = target_st_up[1][shortTermCurrentBarIndex];
                ShortTermTargetUp3 = target_st_up[2][shortTermCurrentBarIndex];
                ShortTermTargetUp4 = target_st_up[3][shortTermCurrentBarIndex];
                ShortTermTargetUp5 = target_st_up[4][shortTermCurrentBarIndex];
                ShortTermTargetDn1 = target_st_dn[0][shortTermCurrentBarIndex];
                ShortTermTargetDn2 = target_st_dn[1][shortTermCurrentBarIndex];
                ShortTermTargetDn3 = target_st_dn[2][shortTermCurrentBarIndex];
                ShortTermTargetDn4 = target_st_dn[3][shortTermCurrentBarIndex];
                ShortTermTargetDn5 = target_st_dn[4][shortTermCurrentBarIndex];

                var tb_st = atm.calculateTrendBars(shortTermSeries[1], shortTermSeries[2], shortTermSeries[3]);
                ShortTermTrendBar = tb_st[shortTermCurrentBarIndex];

                var tl_st = atm.calculateTrendLines(shortTermSeries[1], shortTermSeries[2], shortTermSeries[3], 3);
                var tl_st_val = (!double.IsNaN(tl_st[0][shortTermCurrentBarIndex])) ? 1 : ((!double.IsNaN(tl_st[1][shortTermCurrentBarIndex])) ? -1 : 0);
                ShortTermTrendLine = tl_st_val;

                stPivotR3 = atm.calculatePivotR3(shortTermSeries)[shortTermCurrentBarIndex];
                stPivotR2 = atm.calculatePivotR2(shortTermSeries)[shortTermCurrentBarIndex];
                stPivotR1 = atm.calculatePivotR1(shortTermSeries)[shortTermCurrentBarIndex];

                stPivotS1 = atm.calculatePivotS1(shortTermSeries)[shortTermCurrentBarIndex];
                stPivotS2 = atm.calculatePivotS2(shortTermSeries)[shortTermCurrentBarIndex];
                stPivotS3 = atm.calculatePivotS3(shortTermSeries)[shortTermCurrentBarIndex];

                stFibPivotR3 = atm.calculateFibPivotR3(shortTermSeries)[shortTermCurrentBarIndex];
                stFibPivotR2 = atm.calculateFibPivotR2(shortTermSeries)[shortTermCurrentBarIndex];
                stFibPivotR1 = atm.calculateFibPivotR1(shortTermSeries)[shortTermCurrentBarIndex];

                stFibPivotS1 = atm.calculateFibPivotS1(shortTermSeries)[shortTermCurrentBarIndex];
                stFibPivotS2 = atm.calculateFibPivotS2(shortTermSeries)[shortTermCurrentBarIndex];
                stFibPivotS3 = atm.calculateFibPivotS3(shortTermSeries)[shortTermCurrentBarIndex];
            }

            if (shortTermTimes.Count > 0 && midTermTimes.Count > 0 && _calcSTMT)
            {
                _calcSTMT = false;

                var _PR = calculatePositionRatio(shortTermTimes, shortTermSeries, midTermTimes, midTermSeries, shortTermIndexSeries, interval0);
                var _scores = calculateScore(shortTermTimes, shortTermSeries, midTermTimes, midTermSeries, interval0);

                ShortTermPR = _PR[shortTermCurrentBarIndex];
                ShortTermScore = _scores[shortTermCurrentBarIndex];
            }

            if (midTermTimes.Count > 0 && longTermTimes.Count > 0 && _calcMTLT)
            {
                _calcMTLT = false;

                var score_mt = calculateScore(midTermTimes, midTermSeries, longTermTimes, longTermSeries, interval1);
                var pr_mt = calculatePositionRatio(midTermTimes, midTermSeries, longTermTimes, longTermSeries, midTermIndexSeries, interval1);
                MidTermPR = pr_mt[midTermCurrentBarIndex];
                MidTermScore = score_mt[midTermCurrentBarIndex];
            }

            if (midTermTimes.Count > 0 && _calcMT)
            {
                _calcMT = false;

                var mtft = atm.calculateFT(midTermSeries[1], midTermSeries[2], midTermSeries[3]);
                var mtst = atm.calculateST(midTermSeries[1], midTermSeries[2], midTermSeries[3]);

                var ft_mt = mtft;
                var st_mt = mtst;

                var ft_tp_mt = atm.calculateFastTurningPoints(midTermSeries[1], midTermSeries[2], midTermSeries[3], mtft, midTermCurrentBarIndex);
                var st_tp_mt = atm.calculateSlowTurningPoints(midTermSeries[1], midTermSeries[2], midTermSeries[3], mtst, midTermCurrentBarIndex);

                var ftp_mt_up = ft_tp_mt[0]; // atm.sync(ft_tp_mt[0], interval1, interval, midTermTimes, shortTermTimes);
                var stp_mt_up = st_tp_mt[0]; // atm.sync(st_tp_mt[0], interval1, interval, midTermTimes, shortTermTimes);
                var ftp_mt_dn = ft_tp_mt[1]; // atm.sync(ft_tp_mt[1], interval1, interval, midTermTimes, shortTermTimes);
                var stp_mt_dn = st_tp_mt[1]; // atm.sync(st_tp_mt[1], interval1, interval, midTermTimes, shortTermTimes);

                var sig_mt = atm.calculate3Sigma(midTermSeries[1], midTermSeries[2], midTermSeries[3]);

                Series md_mt = Series.Mid(midTermSeries[1], midTermSeries[2]);
                Dictionary<string, Series> trend_mt = atm.calculateTrend(midTermSeries[1], midTermSeries[2], midTermSeries[3], md_mt);
                Series TLup_mt = trend_mt["TLup"];
                Series TLdn_mt = trend_mt["TLdn"];

                var target_mt_up = atm.calculateUpperTargetLines(midTermSeries[1], midTermSeries[2], TLup_mt, midTermCurrentBarIndex);
                var target_mt_dn = atm.calculateLowerTargetLines(midTermSeries[1], midTermSeries[2], TLdn_mt, midTermCurrentBarIndex);

                var ago = 2;
                var ts_mt = atm.getTSBSignals2(ago, midTermSeries[1], midTermSeries[2], midTermSeries[3]).Last();
                var tc_mt = atm.GetTrendCondition(ago, midTermSeries[1], midTermSeries[2], midTermSeries[3]).Last();
                var td_mt = atm.GetTrendCondition(ago, midTermSeries[1], midTermSeries[2], midTermSeries[3]).Last();

                var tb_mt = atm.calculateTrendBars(midTermSeries[1], midTermSeries[2], midTermSeries[3]); //]atm.sync(atm.calculateTrendBars(midTermSeries[1], midTermSeries[2], midTermSeries[3]), interval1, interval, midTermTimes, shortTermTimes);

                var tl_mt = atm.calculateTrendLines(midTermSeries[1], midTermSeries[2], midTermSeries[3], 3);
                var tl_mt_val = (!double.IsNaN(tl_mt[0][midTermCurrentBarIndex])) ? 1 : ((!double.IsNaN(tl_mt[1][midTermCurrentBarIndex])) ? -1 : 0);

                MidTermOpen = midTermSeries[0][midTermCurrentBarIndex];
                MidTermOpen1 = midTermSeries[0][midTermCurrentBarIndex - 1];
                MidTermHigh = midTermSeries[1][midTermCurrentBarIndex];
                MidTermHigh1 = midTermSeries[1][midTermCurrentBarIndex - 1];
                MidTermLow = midTermSeries[2][midTermCurrentBarIndex];
                MidTermLow1 = midTermSeries[2][midTermCurrentBarIndex - 1];
                MidTermClose = midTermSeries[3][midTermCurrentBarIndex];
                MidTermClose1 = midTermSeries[3][midTermCurrentBarIndex - 1];

                MidTermTS = ts_mt;
                MidTermTC = tc_mt;
                MidTermTD = td_mt;
                MidTermTrendBar = tb_mt[midTermCurrentBarIndex];
                MidTermTrendLine = tl_mt_val;
                MidTermFT = ft_mt[midTermCurrentBarIndex];
                MidTermFT1 = ft_mt[midTermCurrentBarIndex - 1];
                MidTermFT2 = ft_mt[midTermCurrentBarIndex - 2];
                MidTermST = st_mt[midTermCurrentBarIndex];
                MidTermST1 = st_mt[midTermCurrentBarIndex - 1];
                MidTermST2 = st_mt[midTermCurrentBarIndex - 2];
                MidTermFTTPUpNext = ftp_mt_up[midTermCurrentBarIndex + 1];
                MidTermFTTPUp = ftp_mt_up[midTermCurrentBarIndex];
                MidTermFTTPDnNext = ftp_mt_dn[midTermCurrentBarIndex + 1];
                MidTermFTTPDn = ftp_mt_dn[midTermCurrentBarIndex];
                MidTermSTTPUpNext = stp_mt_up[midTermCurrentBarIndex + 1];
                MidTermSTTPUp = stp_mt_up[midTermCurrentBarIndex];
                MidTermSTTPDnNext = stp_mt_dn[midTermCurrentBarIndex + 1];
                MidTermSTTPDn = stp_mt_dn[midTermCurrentBarIndex];
                MidTermSigUp1 = sig_mt[2][midTermCurrentBarIndex];
                MidTermSigUp2 = sig_mt[1][midTermCurrentBarIndex];
                MidTermSigUp3 = sig_mt[0][midTermCurrentBarIndex];
                MidTermSigDn1 = sig_mt[3][midTermCurrentBarIndex];
                MidTermSigDn2 = sig_mt[4][midTermCurrentBarIndex];
                MidTermSigDn3 = sig_mt[5][midTermCurrentBarIndex];
                MidTermTargetUp1 = target_mt_up[0][midTermCurrentBarIndex];
                MidTermTargetUp2 = target_mt_up[1][midTermCurrentBarIndex];
                MidTermTargetUp3 = target_mt_up[2][midTermCurrentBarIndex];
                MidTermTargetUp4 = target_mt_up[3][midTermCurrentBarIndex];
                MidTermTargetUp5 = target_mt_up[4][midTermCurrentBarIndex];
                MidTermTargetDn1 = target_mt_dn[0][midTermCurrentBarIndex];
                MidTermTargetDn2 = target_mt_dn[1][midTermCurrentBarIndex];
                MidTermTargetDn3 = target_mt_dn[2][midTermCurrentBarIndex];
                MidTermTargetDn4 = target_mt_dn[3][midTermCurrentBarIndex];
                MidTermTargetDn5 = target_mt_dn[4][midTermCurrentBarIndex];

                mtPivotR3 = atm.calculatePivotR3(midTermSeries)[midTermCurrentBarIndex];
                mtPivotR2 = atm.calculatePivotR2(midTermSeries)[midTermCurrentBarIndex];
                mtPivotR1 = atm.calculatePivotR1(midTermSeries)[midTermCurrentBarIndex];
                mtPivotS1 = atm.calculatePivotS1(midTermSeries)[midTermCurrentBarIndex];
                mtPivotS2 = atm.calculatePivotS2(midTermSeries)[midTermCurrentBarIndex];
                mtPivotS3 = atm.calculatePivotS3(midTermSeries)[midTermCurrentBarIndex];
                mtFibPivotR3 = atm.calculateFibPivotR3(midTermSeries)[midTermCurrentBarIndex];
                mtFibPivotR2 = atm.calculateFibPivotR2(midTermSeries)[midTermCurrentBarIndex];
                mtFibPivotR1 = atm.calculateFibPivotR1(midTermSeries)[midTermCurrentBarIndex];
                mtFibPivotS1 = atm.calculateFibPivotS1(midTermSeries)[midTermCurrentBarIndex];
                mtFibPivotS2 = atm.calculateFibPivotS2(midTermSeries)[midTermCurrentBarIndex];
                mtFibPivotS3 = atm.calculateFibPivotS3(midTermSeries)[midTermCurrentBarIndex];
            }

            var supResList = new List<AnalysisInfo>();

            if (!double.IsNaN(ShortTermOpen)) supResList.Add(new AnalysisInfo(ShortTermOpen, AnalysisType.Open, false, ShortTermOpen - ShortTermClose1));
            if (!double.IsNaN(ShortTermHigh)) supResList.Add(new AnalysisInfo(ShortTermHigh, AnalysisType.High, false, ShortTermHigh - ShortTermClose1));
            if (!double.IsNaN(ShortTermLow)) supResList.Add(new AnalysisInfo(ShortTermLow, AnalysisType.Low, false, ShortTermLow - ShortTermClose1));
            if (!double.IsNaN(ShortTermClose)) supResList.Add(new AnalysisInfo(ShortTermClose, AnalysisType.Close, false, MidTermClose - MidTermClose1, ShortTermClose - ShortTermClose1));

            if (!double.IsNaN(ShortTermSigUp1)) supResList.Add(new AnalysisInfo(ShortTermSigUp1, AnalysisType.Sig, false));
            if (!double.IsNaN(ShortTermSigUp2)) supResList.Add(new AnalysisInfo(ShortTermSigUp2, AnalysisType.Sig, false));
            if (!double.IsNaN(ShortTermSigUp3)) supResList.Add(new AnalysisInfo(ShortTermSigUp3, AnalysisType.Sig, false));

            if (!double.IsNaN(ShortTermSigDn1)) supResList.Add(new AnalysisInfo(ShortTermSigDn1, AnalysisType.Sig, false));
            if (!double.IsNaN(ShortTermSigDn2)) supResList.Add(new AnalysisInfo(ShortTermSigDn2, AnalysisType.Sig, false));
            if (!double.IsNaN(ShortTermSigDn3)) supResList.Add(new AnalysisInfo(ShortTermSigDn3, AnalysisType.Sig, false));

            if (!double.IsNaN(ShortTermTargetUp1)) supResList.Add(new AnalysisInfo(ShortTermTargetUp1, AnalysisType.ATMTarget, false));
            if (!double.IsNaN(ShortTermTargetUp2)) supResList.Add(new AnalysisInfo(ShortTermTargetUp2, AnalysisType.ATMTarget, false));
            if (!double.IsNaN(ShortTermTargetUp3)) supResList.Add(new AnalysisInfo(ShortTermTargetUp3, AnalysisType.ATMTarget, false));
            if (!double.IsNaN(ShortTermTargetUp4)) supResList.Add(new AnalysisInfo(ShortTermTargetUp4, AnalysisType.ATMTarget, false));
            if (!double.IsNaN(ShortTermTargetUp5)) supResList.Add(new AnalysisInfo(ShortTermTargetUp5, AnalysisType.ATMTarget, false));

            if (!double.IsNaN(ShortTermTargetDn1)) supResList.Add(new AnalysisInfo(ShortTermTargetDn1, AnalysisType.ATMTarget, false));
            if (!double.IsNaN(ShortTermTargetDn2)) supResList.Add(new AnalysisInfo(ShortTermTargetDn2, AnalysisType.ATMTarget, false));
            if (!double.IsNaN(ShortTermTargetDn3)) supResList.Add(new AnalysisInfo(ShortTermTargetDn3, AnalysisType.ATMTarget, false));
            if (!double.IsNaN(ShortTermTargetDn4)) supResList.Add(new AnalysisInfo(ShortTermTargetDn4, AnalysisType.ATMTarget, false));
            if (!double.IsNaN(ShortTermTargetDn5)) supResList.Add(new AnalysisInfo(ShortTermTargetDn5, AnalysisType.ATMTarget, false));

            if (!double.IsNaN(ShortTermFTTPUp)) supResList.Add(new AnalysisInfo(ShortTermFTTPUp, AnalysisType.FTTP, false));
            if (!double.IsNaN(ShortTermFTTPUpNext)) supResList.Add(new AnalysisInfo(ShortTermFTTPUpNext, AnalysisType.FTTP, false));

            if (!double.IsNaN(ShortTermFTTPDn)) supResList.Add(new AnalysisInfo(ShortTermFTTPDn, AnalysisType.FTTP, false));
            if (!double.IsNaN(ShortTermFTTPDnNext)) supResList.Add(new AnalysisInfo(ShortTermFTTPDnNext, AnalysisType.FTTP, false));

            if (!double.IsNaN(ShortTermSTTPUp)) supResList.Add(new AnalysisInfo(ShortTermSTTPUp, AnalysisType.STTP, false));
            if (!double.IsNaN(ShortTermSTTPUpNext)) supResList.Add(new AnalysisInfo(ShortTermSTTPUpNext, AnalysisType.STTP, false));

            if (!double.IsNaN(ShortTermSTTPDn)) supResList.Add(new AnalysisInfo(ShortTermSTTPDn, AnalysisType.STTP, false));
            if (!double.IsNaN(ShortTermSTTPDnNext)) supResList.Add(new AnalysisInfo(ShortTermSTTPDnNext, AnalysisType.STTP, false));

            if (!double.IsNaN(stPivotR3)) supResList.Add(new AnalysisInfo(stPivotR3, AnalysisType.Pivot, false));
            if (!double.IsNaN(stPivotR2)) supResList.Add(new AnalysisInfo(stPivotR2, AnalysisType.Pivot, false));
            if (!double.IsNaN(stPivotR1)) supResList.Add(new AnalysisInfo(stPivotR1, AnalysisType.Pivot, false));

            if (!double.IsNaN(stPivotS1)) supResList.Add(new AnalysisInfo(stPivotS1, AnalysisType.Pivot, false));
            if (!double.IsNaN(stPivotS2)) supResList.Add(new AnalysisInfo(stPivotS2, AnalysisType.Pivot, false));
            if (!double.IsNaN(stPivotS3)) supResList.Add(new AnalysisInfo(stPivotS3, AnalysisType.Pivot, false));
            if (!double.IsNaN(stFibPivotR3)) supResList.Add(new AnalysisInfo(stFibPivotR3, AnalysisType.FibPivot, false));
            if (!double.IsNaN(stFibPivotR2)) supResList.Add(new AnalysisInfo(stFibPivotR2, AnalysisType.FibPivot, false));
            if (!double.IsNaN(stFibPivotR1)) supResList.Add(new AnalysisInfo(stFibPivotR1, AnalysisType.FibPivot, false));

            if (!double.IsNaN(stFibPivotS1)) supResList.Add(new AnalysisInfo(stFibPivotS1, AnalysisType.FibPivot, false));
            if (!double.IsNaN(stFibPivotS2)) supResList.Add(new AnalysisInfo(stFibPivotS2, AnalysisType.FibPivot, false));
            if (!double.IsNaN(stFibPivotS3)) supResList.Add(new AnalysisInfo(stFibPivotS3, AnalysisType.FibPivot, false));

            if (!double.IsNaN(MidTermOpen)) supResList.Add(new AnalysisInfo(MidTermOpen, AnalysisType.Open, true, MidTermOpen - MidTermOpen1));
            if (!double.IsNaN(MidTermHigh)) supResList.Add(new AnalysisInfo(MidTermHigh, AnalysisType.High, true, MidTermHigh - MidTermHigh1));
            if (!double.IsNaN(MidTermLow)) supResList.Add(new AnalysisInfo(MidTermLow, AnalysisType.Low, true, MidTermLow - MidTermLow1));
 
            if (!double.IsNaN(MidTermSigUp1)) supResList.Add(new AnalysisInfo(MidTermSigUp1, AnalysisType.Sig, true));
            if (!double.IsNaN(MidTermSigUp2)) supResList.Add(new AnalysisInfo(MidTermSigUp2, AnalysisType.Sig, true));
            if (!double.IsNaN(MidTermSigUp3)) supResList.Add(new AnalysisInfo(MidTermSigUp3, AnalysisType.Sig, true));

            if (!double.IsNaN(MidTermSigDn1)) supResList.Add(new AnalysisInfo(MidTermSigDn1, AnalysisType.Sig, true));
            if (!double.IsNaN(MidTermSigDn2)) supResList.Add(new AnalysisInfo(MidTermSigDn2, AnalysisType.Sig, true));
            if (!double.IsNaN(MidTermSigDn3)) supResList.Add(new AnalysisInfo(MidTermSigDn3, AnalysisType.Sig, true));

            if (!double.IsNaN(MidTermTargetUp1)) supResList.Add(new AnalysisInfo(MidTermTargetUp1, AnalysisType.ATMTarget, true));
            if (!double.IsNaN(MidTermTargetUp2)) supResList.Add(new AnalysisInfo(MidTermTargetUp2, AnalysisType.ATMTarget, true));
            if (!double.IsNaN(MidTermTargetUp3)) supResList.Add(new AnalysisInfo(MidTermTargetUp3, AnalysisType.ATMTarget, true));
            if (!double.IsNaN(MidTermTargetUp4)) supResList.Add(new AnalysisInfo(MidTermTargetUp4, AnalysisType.ATMTarget, true));
            if (!double.IsNaN(MidTermTargetUp5)) supResList.Add(new AnalysisInfo(MidTermTargetUp5, AnalysisType.ATMTarget, true));

            if (!double.IsNaN(MidTermTargetDn1)) supResList.Add(new AnalysisInfo(MidTermTargetDn1, AnalysisType.ATMTarget, true));
            if (!double.IsNaN(MidTermTargetDn2)) supResList.Add(new AnalysisInfo(MidTermTargetDn2, AnalysisType.ATMTarget, true));
            if (!double.IsNaN(MidTermTargetDn3)) supResList.Add(new AnalysisInfo(MidTermTargetDn3, AnalysisType.ATMTarget, true));
            if (!double.IsNaN(MidTermTargetDn4)) supResList.Add(new AnalysisInfo(MidTermTargetDn4, AnalysisType.ATMTarget, true));
            if (!double.IsNaN(MidTermTargetDn5)) supResList.Add(new AnalysisInfo(MidTermTargetDn5, AnalysisType.ATMTarget, true));

            if (!double.IsNaN(MidTermFTTPUp)) supResList.Add(new AnalysisInfo(MidTermFTTPUp, AnalysisType.FTTP, true));
            if (!double.IsNaN(MidTermFTTPUpNext)) supResList.Add(new AnalysisInfo(MidTermFTTPUpNext, AnalysisType.FTTP, true));

            if (!double.IsNaN(MidTermFTTPDn)) supResList.Add(new AnalysisInfo(MidTermFTTPDn, AnalysisType.FTTP, true));
            if (!double.IsNaN(MidTermFTTPDnNext)) supResList.Add(new AnalysisInfo(MidTermFTTPDnNext, AnalysisType.FTTP, true));

            if (!double.IsNaN(MidTermSTTPUp)) supResList.Add(new AnalysisInfo(MidTermSTTPUp, AnalysisType.STTP, true));
            if (!double.IsNaN(MidTermSTTPUpNext)) supResList.Add(new AnalysisInfo(MidTermSTTPUpNext, AnalysisType.STTP, true));

            if (!double.IsNaN(MidTermSTTPDn)) supResList.Add(new AnalysisInfo(MidTermSTTPDn, AnalysisType.STTP, true));
            if (!double.IsNaN(MidTermSTTPDnNext)) supResList.Add(new AnalysisInfo(MidTermSTTPDnNext, AnalysisType.STTP, true));

            if (!double.IsNaN(mtPivotR3)) supResList.Add(new AnalysisInfo(mtPivotR3, AnalysisType.Pivot, true));
            if (!double.IsNaN(mtPivotR2)) supResList.Add(new AnalysisInfo(mtPivotR2, AnalysisType.Pivot, true));
            if (!double.IsNaN(mtPivotR1)) supResList.Add(new AnalysisInfo(mtPivotR1, AnalysisType.Pivot, true));
            if (!double.IsNaN(mtPivotS1)) supResList.Add(new AnalysisInfo(mtPivotS1, AnalysisType.Pivot, true));
            if (!double.IsNaN(mtPivotS2)) supResList.Add(new AnalysisInfo(mtPivotS2, AnalysisType.Pivot, true));
            if (!double.IsNaN(mtPivotS3)) supResList.Add(new AnalysisInfo(mtPivotS3, AnalysisType.Pivot, true));
            if (!double.IsNaN(mtFibPivotR3)) supResList.Add(new AnalysisInfo(mtFibPivotR3, AnalysisType.FibPivot, true));
            if (!double.IsNaN(mtFibPivotR2)) supResList.Add(new AnalysisInfo(mtFibPivotR2, AnalysisType.FibPivot, true));
            if (!double.IsNaN(mtFibPivotR1)) supResList.Add(new AnalysisInfo(mtFibPivotR1, AnalysisType.FibPivot, true));
            if (!double.IsNaN(mtFibPivotS1)) supResList.Add(new AnalysisInfo(mtFibPivotS1, AnalysisType.FibPivot, true));
            if (!double.IsNaN(mtFibPivotS2)) supResList.Add(new AnalysisInfo(mtFibPivotS2, AnalysisType.FibPivot, true));
            if (!double.IsNaN(mtFibPivotS3)) supResList.Add(new AnalysisInfo(mtFibPivotS3, AnalysisType.FibPivot, true));

            _supResList = (from x in supResList orderby x.Value descending select x).ToList();
        }

        private List<AnalysisInfo> _supResList = new List<AnalysisInfo>();

        public string GetOverviewInterval(string interval, int level)
        {
            return Study.getForecastInterval(interval, level);
        }

        private string getIntervalAbbreviation(string interval)
        {
            string label = interval;
            if (interval == "1D" || interval == "Daily") label = "D";
            else if (interval == "1W" || interval == "Weekly") label = "W";
            else if (interval == "1M" || interval == "Monthly") label = "M";
            else if (interval == "1Q" || interval == "Quarterly") label = "Q";
            else if (interval == "1S" || interval == "SemiAnually") label = "S";
            else if (interval == "1Y" || interval == "Yearly") label = "Y";
            else label = interval;
            return label;
        }

        private Series calculatePositionRatio(List<DateTime> t1, Series[] b1, List<DateTime> t2, Series[] b2, Series[] indexSeries, string shortTermInterval)
        {
            Dictionary<string, List<DateTime>> times = new Dictionary<string, List<DateTime>>();
            Dictionary<string, Series[]> bars = new Dictionary<string, Series[]>();

            times[shortTermInterval] = t1;
            bars[shortTermInterval] = b1;
            int barCount = t1.Count;

            string midTermInterval = GetOverviewInterval(shortTermInterval, 1);
            times[midTermInterval] = t2;
            bars[midTermInterval] = b2;

            Dictionary<string, object> referenceData = new Dictionary<string, object>();
            referenceData["symbol"] = Symbol;

            if (indexSeries.Length > 0 && indexSeries[0].Count > 0)
            {
                referenceData["Index Prices : " + shortTermInterval] = indexSeries[0];
            }
            string[] intervalList = { shortTermInterval, midTermInterval };

            string conditionName = "Position Ratio 1";

            return Conditions.Calculate(conditionName, Symbol, intervalList, barCount, times, bars, referenceData);
        }

        private Series calculateScore(List<DateTime> t1, Series[] b1, List<DateTime> t2, Series[] b2, string shortTermInterval)
        {
            Dictionary<string, List<DateTime>> times = new Dictionary<string, List<DateTime>>();
            Dictionary<string, Series[]> bars = new Dictionary<string, Series[]>();

            times[shortTermInterval] = t1;
            bars[shortTermInterval] = b1;
            int barCount = t1.Count;

            string midTermInterval = GetOverviewInterval(shortTermInterval, 1);
            times[midTermInterval] = t2;
            bars[midTermInterval] = b2;

            Dictionary<string, object> referenceData = new Dictionary<string, object>();

            string[] intervalList = { shortTermInterval, midTermInterval };
            return Conditions.Calculate("Score", Symbol, intervalList, barCount, times, bars, referenceData);
        }

        public string Symbol;
        public string ModelScenario;
        public string ShortTermInterval;
        public string MidTermInterval;
        public DateTime Time;

        public double ShortTermPredictionCurrent;
        public double ShortTermPredictionNext;
        public double MidTermPredictionCurrent;
        public double MidTermPredictionNext;

        public double mtPivotR3;
        public double mtPivotR2;
        public double mtPivotR1; 
        public double mtPivotS1; 
        public double mtPivotS2;
        public double mtPivotS3; 
        public double mtFibPivotR3;
        public double mtFibPivotR2;
        public double mtFibPivotR1;
        public double mtFibPivotS1;
        public double mtFibPivotS2;
        public double mtFibPivotS3;

        public double stPivotR3;
        public double stPivotR2;
        public double stPivotR1;
        public double stPivotS1;
        public double stPivotS2;
        public double stPivotS3;
        public double stFibPivotR3;
        public double stFibPivotR2;
        public double stFibPivotR1;
        public double stFibPivotS1;
        public double stFibPivotS2;
        public double stFibPivotS3;
                                   
        public double ShortTermHigh;
        public double ShortTermHigh1;
        public double ShortTermOpen;
        public double ShortTermOpen1;
        public double ShortTermLow;
        public double ShortTermLow1;
        public double ShortTermClose;
        public double ShortTermClose1;
        public double ShortTermTS;
        public double ShortTermTC;
        public double ShortTermTD;
        public double MidTermHigh;
        public double MidTermHigh1;
        public double MidTermOpen;
        public double MidTermOpen1;
        public double MidTermLow;
        public double MidTermLow1;
        public double MidTermClose;
        public double MidTermClose1;
        public double MidTermTS;
        public double MidTermTC;
        public double MidTermTD;
        public double TrendLine;
        public double TrendBar;
        public double STGreaterFT;
        public double STLessFT;
        public double STOB;
        public double STOS;
        public double ShortTermPR;
        public double MidTermPR;
        public double ShortTermScore;
        public double MidTermScore;
        public double ShortTermTrendBar;
        public double MidTermTrendBar;
        public double ShortTermTrendLine;
        public double MidTermTrendLine;
        public double ShortTermFT;
        public double ShortTermFT1;
        public double ShortTermFT2;
        public double MidTermFT;
        public double MidTermFT1;
        public double MidTermFT2;
        public double ShortTermST;
        public double ShortTermST1;
        public double ShortTermST2;
        public double MidTermST;
        public double MidTermST1;
        public double MidTermST2;
        public double ShortTermFTTPUpNext;
        public double ShortTermFTTPUp;
        public double MidTermFTTPUpNext;
        public double MidTermFTTPUp;
        public double ShortTermFTTPDnNext;
        public double ShortTermFTTPDn;
        public double MidTermFTTPDnNext;
        public double MidTermFTTPDn;
        public double ShortTermSTTPUpNext;
        public double ShortTermSTTPUp;
        public double MidTermSTTPUpNext;
        public double MidTermSTTPUp;
        public double ShortTermSTTPDnNext;
        public double ShortTermSTTPDn;
        public double MidTermSTTPDnNext;
        public double MidTermSTTPDn;
        public double MidTermSigDn1;
        public double MidTermSigUp1;
        public double MidTermSigDn2;
        public double MidTermSigUp2;
        public double MidTermSigDn3;
        public double MidTermSigUp3;
        public double ShortTermSigDn1;
        public double ShortTermSigUp1;
        public double ShortTermSigDn2;
        public double ShortTermSigUp2;
        public double ShortTermSigDn3;
        public double ShortTermSigUp3;
        public double MidTermTargetDn1;
        public double MidTermTargetUp1;
        public double MidTermTargetDn2;
        public double MidTermTargetUp2;
        public double MidTermTargetDn3;
        public double MidTermTargetUp3;
        public double MidTermTargetDn4;
        public double MidTermTargetUp4;
        public double MidTermTargetDn5;
        public double MidTermTargetUp5;
        public double ShortTermTargetUp1;
        public double ShortTermTargetDn1;
        public double ShortTermTargetUp2;
        public double ShortTermTargetDn2;
        public double ShortTermTargetUp3;
        public double ShortTermTargetDn3;
        public double ShortTermTargetUp4;
        public double ShortTermTargetDn4;
        public double ShortTermTargetUp5;
        public double ShortTermTargetDn5;

        bool _calcST = true;
        bool _calcSTMT = true;
        bool _calcMTLT = true;
        bool _calcMT = true;

        public void Clear()
        {
            _calcST = true;
            _calcSTMT = true;
            _calcMTLT = true;
            _calcMT = true;

            _positivePredictiveRate = double.NaN;
            _truePositive = double.NaN;
            _falsePositive = double.NaN;
            _falseNegative = double.NaN;
            _trueNegative = double.NaN;
            _totalPositive = double.NaN;
            _totalNegative = double.NaN;
            _actualPositive = double.NaN;
            _actualNegative = double.NaN;
            _accuracy = double.NaN;
            _errorRate = double.NaN;
            _sensitivity = double.NaN;
            _falsePosPer = double.NaN;
            _specificity = double.NaN;
            _prevalence = double.NaN;
            _precision = double.NaN;
            _nullError = double.NaN;
            _f1 = double.NaN;
            _probability = double.NaN;
            _probabilityYes = double.NaN;
            _probabilityNo = double.NaN;
            _probabilityRandom = double.NaN;
            _positivePredictiveRate = double.NaN;
            _negativePredictiveRate = double.NaN;

            ShortTermPredictionCurrent = double.NaN;
            ShortTermPredictionNext = double.NaN;
            MidTermPredictionCurrent = double.NaN;
            MidTermPredictionNext = double.NaN;

            MidTermOpen = double.NaN;
            MidTermOpen1 = double.NaN;
            ShortTermOpen = double.NaN;
            ShortTermOpen1 = double.NaN;
            MidTermHigh = double.NaN;
            MidTermHigh1 = double.NaN;
            ShortTermHigh = double.NaN;
            ShortTermHigh1 = double.NaN;
            MidTermLow = double.NaN;
            MidTermLow1 = double.NaN;
            ShortTermLow = double.NaN;
            ShortTermLow1 = double.NaN;
            MidTermClose = double.NaN;
            MidTermClose1 = double.NaN;
            ShortTermClose = double.NaN;
            ShortTermClose1 = double.NaN;
            ShortTermScore = double.NaN;
            MidTermScore = double.NaN;

            ShortTermFT = double.NaN;
            ShortTermFT1 = double.NaN;
            ShortTermFT2 = double.NaN;
            ShortTermST = double.NaN;
            ShortTermST1 = double.NaN;
            ShortTermST2 = double.NaN;
            ShortTermTrendBar = 0;
            ShortTermTrendLine = 0;
            ShortTermTS = 0;
            ShortTermTC = 5;

            MidTermFT = double.NaN;
            MidTermFT1 = double.NaN;
            MidTermFT2 = double.NaN;
            MidTermST = double.NaN;
            MidTermST1 = double.NaN;
            MidTermST2 = double.NaN;
            MidTermTrendBar = 0;
            MidTermTrendLine = 0;
            MidTermTS = 0;
            MidTermTC = 5;

            ShortTermFTTPUp = 0;
            ShortTermFTTPDn = 0;
            ShortTermFTTPUpNext = 0;
            ShortTermFTTPDnNext = 0;
            ShortTermSTTPUp = 0;
            ShortTermSTTPDn = 0;
            ShortTermSTTPUpNext = 0;
            ShortTermSTTPDnNext = 0;

            MidTermFTTPUp = 0;
            MidTermFTTPDn = 0;
            MidTermFTTPUpNext = 0;
            MidTermFTTPDnNext = 0;
            MidTermSTTPUp = 0;
            MidTermSTTPDn = 0;
            MidTermSTTPUpNext = 0;
            MidTermSTTPDnNext = 0;

            MTglfttp0.Visibility = Visibility.Collapsed;
            MTrlfttp0.Visibility = Visibility.Collapsed;
            MTglfttp1.Visibility = Visibility.Collapsed;
            MTrlfttp1.Visibility = Visibility.Collapsed;
            MTglsttp0.Visibility = Visibility.Collapsed;
            MTrlsttp0.Visibility = Visibility.Collapsed;
            MTglsttp1.Visibility = Visibility.Collapsed;
            MTrlsttp1.Visibility = Visibility.Collapsed;

            STglfttp0.Visibility = Visibility.Collapsed;
            STrlfttp0.Visibility = Visibility.Collapsed;
            STglfttp1.Visibility = Visibility.Collapsed;
            STrlfttp1.Visibility = Visibility.Collapsed;
            STglsttp0.Visibility = Visibility.Collapsed;
            STrlsttp0.Visibility = Visibility.Collapsed;
            STglsttp1.Visibility = Visibility.Collapsed;
            STrlsttp1.Visibility = Visibility.Collapsed;

            recSTpr.Fill =  Brushes.Transparent;
            recSTpr.Fill =  Brushes.Transparent;
            recSTsc.Fill =  Brushes.Transparent;
            recSTft.Fill =  Brushes.Transparent;
            recSTst.Fill =  Brushes.Transparent;
            recSTTSB.Fill = Brushes.Transparent;

            recMTpr.Fill =  Brushes.Transparent;
            recMTsc.Fill =  Brushes.Transparent;
            recMTft.Fill =  Brushes.Transparent;
            recMTst.Fill =  Brushes.Transparent;
            recMTtsb.Fill = Brushes.Transparent;

            PRPR.Fill =  Brushes.Transparent;
            PRFT.Fill =  Brushes.Transparent;
            PRSC.Fill =  Brushes.Transparent;
            PRST.Fill =  Brushes.Transparent;
            PRTSB.Fill = Brushes.Transparent;

            SCPR.Fill  = Brushes.Transparent;
            SCFT.Fill  = Brushes.Transparent;
            SCSC.Fill  = Brushes.Transparent;
            SCST.Fill  = Brushes.Transparent;
            SCTSB.Fill = Brushes.Transparent;

            FTPR.Fill =  Brushes.Transparent;
            FTFT.Fill =  Brushes.Transparent;
            FTSC.Fill =  Brushes.Transparent;
            FTST.Fill =  Brushes.Transparent;
            FTTSB.Fill = Brushes.Transparent;

            STPR.Fill =  Brushes.Transparent;
            STFT.Fill =  Brushes.Transparent;
            STSC.Fill =  Brushes.Transparent;
            STST.Fill =  Brushes.Transparent;
            STTSB.Fill = Brushes.Transparent;

            TSBPR.Fill =  Brushes.Transparent;
            TSBFT.Fill =  Brushes.Transparent;
            TSBSC.Fill =  Brushes.Transparent;
            TSBST.Fill =  Brushes.Transparent;
            TSBTSB.Fill = Brushes.Transparent;

            LMTOp.Content = "";
            LMTHi.Content = "";
            LMTLo.Content = "";
            LMTCl.Content = "";
            LSTOp.Content = "";
            LSTHi.Content = "";
            LSTLo.Content = "";
            LSTCl.Content = "";
            Op.Content = "";
            Hi.Content = "";
            Lo.Content = "";
            Cl.Content = "";
            MTHi.Content = "";
            MTLo.Content = "";
            MTOp.Content = "";
            MTCl.Content = "";
            clToOp.Content = "";
            clToCl.Content = "";
            MTclToOp.Content = "";
            MTclToCl.Content = "";
            MT1.Content = "";
            ST1.Content = "";
            MT2.Content = "";
            ST2.Content = "";
            MT3.Content = "";
            ST3.Content = "";
            MT4.Content = "";
            ST4.Content = "";
            MT5.Content = "";
            ST5.Content = "";
            MT6.Content = "";
            ST6.Content = "";
            MT7.Content = "";
            ST7.Content = "";
            MT8.Content = "";
            ST8.Content = "";
            MT9.Content = "";
            ST9.Content = "";
            MT10.Content = "";
            ST10.Content = "";
            STScore.Content = "";
            MTScore.Content = "";
            STftUpdn.Content = "";
            MTstToft.Content = "";
            STstToft.Content = "";
            MTstUpDn.Content = "";
            STstUpDn.Content = "";
            MTftUpDn.Content = "";
            STftUpdn.Content = "";
            STtsb.Content = "";
            MTtsb.Content = "";
            MTtc.Content = "";
            STtc.Content = "";
            MTtL.Content = "";
            STtL.Content = "";
            MTtB.Content = "";
            STtB.Content = "";
            ST1fttp0.Content = "";
            ST1fttp1.Content = "";
            MT1fttp0.Content = "";
            MT1fttp1.Content = "";
            ST1sttp0.Content = "";
            ST1sttp1.Content = "";
            MT1sttp0.Content = "";
            MT1sttp1.Content = "";
            clToHi.Content = "";
            clToLo.Content = "";
            MTclToHi.Content = "";
            MTclToLo.Content = "";
        }

        private void drawMatrix()
        {
            PosPredRate.Content = (double.IsNaN(_positivePredictiveRate)) ? "" : (100 * _positivePredictiveRate).ToString("0.00");
            NegPredRate.Content = (double.IsNaN(_negativePredictiveRate)) ? "" : (100 * _negativePredictiveRate).ToString("0.00");
            Accuracy.Content = (double.IsNaN(_accuracy)) ? "" : (100 * _accuracy).ToString("0.00");
            Sensitivity.Content = (double.IsNaN(_sensitivity)) ? "" : (100 * _sensitivity).ToString("0.00");
            Specificity.Content = (double.IsNaN(_specificity)) ? "" : (100 * _specificity).ToString("0.00");
            FalsePosPer.Content = (double.IsNaN(_falsePositive)) ? "" : (_falsePositive).ToString("0.00");
            ErrorRate.Content = (double.IsNaN(_errorRate)) ? "" : (100 * _errorRate).ToString("0.00");
            Prevalence.Content = (double.IsNaN(_prevalence)) ? "" : (100 * _prevalence).ToString("0.00");
            NullError.Content = (double.IsNaN(_nullError)) ? "" : (100 * _nullError).ToString("0.00");
            F1.Content = (double.IsNaN(_f1)) ? "" : (100 * _f1).ToString("0.00");

            TruePos.Content = _truePositive;
            FalsePos.Content = _falsePositive;
            TotalPos.Content = _totalPositive;
            TrueNeg.Content = _trueNegative;
            FalseNeg.Content = _falseNegative;
            TotalNeg.Content = _totalNegative;
        }

        public void Draw()
        {
            drawMatrix();

            STPredict.Text = double.IsNaN(ShortTermPredictionCurrent) ? "" : (ShortTermPredictionCurrent > 0) ? "\u2191" : "\u2193";
            STPredict.Foreground = double.IsNaN(ShortTermPredictionCurrent) ? Brushes.Transparent : (ShortTermPredictionCurrent > 0) ? Brushes.Lime : Brushes.Red;
            STPredict1.Text = double.IsNaN(ShortTermPredictionNext) ? "" : (ShortTermPredictionNext > 0) ? "\u2191" : "\u2193";
            STPredict1.Foreground = double.IsNaN(ShortTermPredictionNext) ? Brushes.Transparent : (ShortTermPredictionNext > 0) ? Brushes.Lime : Brushes.Red;

            MTPredict.Text = double.IsNaN(MidTermPredictionCurrent) ? "" : (MidTermPredictionCurrent > 0) ? "\u2191" : "\u2193";
            MTPredict.Foreground = double.IsNaN(MidTermPredictionCurrent) ? Brushes.Transparent : (MidTermPredictionCurrent > 0) ? Brushes.Lime : Brushes.Red;
            MTPredict1.Text = double.IsNaN(MidTermPredictionNext) ? "" : (MidTermPredictionNext > 0) ? "\u2191" : "\u2193";
            MTPredict1.Foreground = double.IsNaN(MidTermPredictionNext) ? Brushes.Transparent : (MidTermPredictionNext > 0) ? Brushes.Lime : Brushes.Red;

            STChartInterval.Content = ShortTermInterval;
            time.Content = Time;

            Scenario.Content = ModelScenario;

            LMTOp.Content = MidTermInterval;
            LMTHi.Content = MidTermInterval;
            LMTLo.Content = MidTermInterval;
            LMTCl.Content = MidTermInterval;

            LSTOp.Content = ShortTermInterval;
            LSTHi.Content = ShortTermInterval;
            LSTLo.Content = ShortTermInterval;
            LSTCl.Content = ShortTermInterval;

            if (ShortTermClose1 > ShortTermOpen)
            {
                Op.Foreground = Brushes.Red;
            }
            else if (ShortTermClose1 < ShortTermOpen)
            {
                Op.Foreground = Brushes.Lime;
            }
            Op.Content = (double.IsNaN(ShortTermOpen)) ? "" : ShortTermOpen.ToString("0.00");

            Hi.Content = (double.IsNaN(ShortTermHigh)) ? "" : ShortTermHigh.ToString("0.00");

            Lo.Content = (double.IsNaN(ShortTermLow)) ? "" : ShortTermLow.ToString("0.00");

            if (ShortTermClose1 > ShortTermClose)
            {
                Cl.Foreground = Brushes.Red;
            }
            else if (ShortTermClose1 < ShortTermClose)
            {
                Cl.Foreground = Brushes.Lime;
            }
            Cl.Content = (double.IsNaN(ShortTermClose)) ? "" : ShortTermClose.ToString("0.00");

            if (MidTermClose1 > MidTermOpen)
            {
                MTOp.Foreground = Brushes.Red;
            }
            else if (MidTermClose1 < MidTermOpen)
            {
                MTOp.Foreground = Brushes.Lime;
            }
            MTOp.Content = (double.IsNaN(MidTermOpen)) ? "" : MidTermOpen.ToString("0.00");

            MTHi.Content = (double.IsNaN(MidTermHigh)) ? "" : MidTermHigh.ToString("0.00");

            MTLo.Content = (double.IsNaN(MidTermLow)) ? "" : MidTermLow.ToString("0.00");

            if (MidTermClose1 > MidTermClose)
            {
                MTCl.Foreground = Brushes.Red;
            }
            else if (MidTermClose1 < MidTermClose)
            {
                MTCl.Foreground = Brushes.Lime;
            }
            MTCl.Content = (double.IsNaN(MidTermClose)) ? "" : MidTermClose.ToString("0.00");

            var ClosetoOpen = 100 * (ShortTermOpen - ShortTermClose1) / ShortTermOpen;

            if (ShortTermClose1 > ShortTermOpen)
            {
                clToOp.Foreground = Brushes.Red;
            }
            else if (ShortTermClose1 < ShortTermOpen)
            {
                clToOp.Foreground = Brushes.Lime;
            }
            clToOp.Content = (double.IsNaN(ClosetoOpen)) ? "" : ClosetoOpen.ToString("0.00");

            var ClosetoClose = 100 * (ShortTermClose - ShortTermClose1) / ShortTermClose;
            if (ShortTermClose1 > ShortTermClose)
            {
                clToCl.Foreground = Brushes.Red;
            }
            else if (ShortTermClose1 < ShortTermClose)
            {
                clToCl.Foreground = Brushes.Lime;
            }
            clToCl.Content = (double.IsNaN(ClosetoClose)) ? "" : ClosetoClose.ToString("0.00");

            char ch1 = '\u2b63';
            char ch2 = '\u2b62';

            MT1.Content = MidTermInterval;
            ST1.Content = ShortTermInterval;
            MT2.Content = MidTermInterval;
            ST2.Content = ShortTermInterval;
            MT3.Content = MidTermInterval + " " + ch1;
            ST3.Content = ShortTermInterval + " " + ch2;
            MT4.Content = MidTermInterval;
            ST4.Content = ShortTermInterval;
            MT5.Content = MidTermInterval;
            ST5.Content = ShortTermInterval;
            MT6.Content = MidTermInterval;
            ST6.Content = ShortTermInterval;
            MT7.Content = MidTermInterval;
            ST7.Content = ShortTermInterval;
            MT8.Content = MidTermInterval;
            ST8.Content = ShortTermInterval;
            MT9.Content = MidTermInterval;
            ST9.Content = ShortTermInterval;
            MT10.Content = MidTermInterval;
            ST10.Content = ShortTermInterval;

            var MTClosetoClose = 100 * (MidTermClose - MidTermClose1) / MidTermClose;
            if (MidTermClose1 > MidTermClose)
            {
                MTclToCl.Foreground = Brushes.Red;
            }
            else if (MidTermClose1 < MidTermClose)
            {
                MTclToCl.Foreground = Brushes.Lime;
            }
            MTclToCl.Content = MTClosetoClose.ToString("0.00");

            var MTClosetoHigh = 100 * (MidTermHigh - MidTermClose1) / MidTermHigh;
            if (MidTermClose1 > MidTermHigh)
            {
                MTclToHi.Foreground = Brushes.Red;
            }
            else if (MidTermClose1 < MidTermHigh)
            {
                MTclToHi.Foreground = Brushes.Lime;
            }
            MTclToHi.Content = MTClosetoHigh.ToString("0.00");

            var MTClosetoLow = 100 * (ShortTermLow - ShortTermClose1) / ShortTermLow;
            if (MidTermClose1 > MidTermLow)
            {
                MTclToLo.Foreground = Brushes.Red;
            }
            else if (MidTermClose1 < MidTermLow)
            {
                MTclToLo.Foreground = Brushes.Lime;
            }
            MTclToLo.Content = MTClosetoLow.ToString("0.00");

            var MTClosetoOpen = 100 * (MidTermOpen - MidTermClose1) / ShortTermOpen;
            if (MidTermClose1 > MidTermOpen)
            {
                MTclToOp.Foreground = Brushes.Red;
            }
            else if (MidTermClose1 < MidTermOpen)
            {
                MTclToOp.Foreground = Brushes.Lime;
            }
            MTclToOp.Content = MTClosetoOpen.ToString("0.00");

            MTglfttp0.Visibility = (MidTermFTTPUp > 1) ? Visibility.Visible : Visibility.Collapsed;
            MTrlfttp0.Visibility = (MidTermFTTPDn > 1) ? Visibility.Visible : Visibility.Collapsed;
            MTglfttp1.Visibility = (MidTermFTTPUpNext > 1) ? Visibility.Visible : Visibility.Collapsed;
            MTrlfttp1.Visibility = (MidTermFTTPDnNext > 1) ? Visibility.Visible : Visibility.Collapsed;
            MTglsttp0.Visibility = (MidTermSTTPUp > 1) ? Visibility.Visible : Visibility.Collapsed;
            MTrlsttp0.Visibility = (MidTermSTTPDn > 1) ? Visibility.Visible : Visibility.Collapsed;
            MTglsttp1.Visibility = (MidTermSTTPUpNext > 1) ? Visibility.Visible : Visibility.Collapsed;
            MTrlsttp1.Visibility = (MidTermSTTPDnNext > 1) ? Visibility.Visible : Visibility.Collapsed;

            STglfttp0.Visibility = (ShortTermFTTPUp > 1) ? Visibility.Visible : Visibility.Collapsed;
            STrlfttp0.Visibility = (ShortTermFTTPDn > 1) ? Visibility.Visible : Visibility.Collapsed;
            STglfttp1.Visibility = (ShortTermFTTPUpNext > 1) ? Visibility.Visible : Visibility.Collapsed;
            STrlfttp1.Visibility = (ShortTermFTTPDnNext > 1) ? Visibility.Visible : Visibility.Collapsed;
            STglsttp0.Visibility = (ShortTermSTTPUp > 1) ? Visibility.Visible : Visibility.Collapsed;
            STrlsttp0.Visibility = (ShortTermSTTPDn > 1) ? Visibility.Visible : Visibility.Collapsed;
            STglsttp1.Visibility = (ShortTermSTTPUpNext > 1) ? Visibility.Visible : Visibility.Collapsed;
            STrlsttp1.Visibility = (ShortTermSTTPDnNext > 1) ? Visibility.Visible : Visibility.Collapsed;

            updateTurningPoint(ST1fttp0, ShortTermFTTPUp, ShortTermFTTPDn);
            updateTurningPoint(ST1fttp1, ShortTermFTTPUpNext, ShortTermFTTPDnNext);
            updateTurningPoint(MT1fttp0, MidTermFTTPUp, MidTermFTTPDn);
            updateTurningPoint(MT1fttp1, MidTermFTTPUpNext, MidTermFTTPDnNext);
            updateTurningPoint(ST1sttp0, ShortTermSTTPUp, ShortTermSTTPDn);
            updateTurningPoint(ST1sttp1, ShortTermSTTPUpNext, ShortTermSTTPDnNext);
            updateTurningPoint(MT1sttp0, MidTermSTTPUp, MidTermSTTPDn);
            updateTurningPoint(MT1sttp1, MidTermSTTPUpNext, MidTermSTTPDnNext);

            var shortTermTrendBar = ShortTermTrendBar;
            var midTermTrendBar = MidTermTrendBar;
            var shortTermTrendLine = ShortTermTrendLine;
            var midTermTrendLine = MidTermTrendLine;
            var midTermTS = MidTermTS;
            var shortTermTS = ShortTermTS;
            var midTermTC = MidTermTC;
            var shortTermTC = ShortTermTC;

            if (Symbol != null)
            {
                var rev = atm.isYieldTicker(Symbol);
                if (rev)
                {
                    shortTermTrendBar = -ShortTermTrendBar;
                    midTermTrendBar = -MidTermTrendBar;
                    shortTermTrendLine = -ShortTermTrendLine;
                    midTermTrendLine = -MidTermTrendLine;
                    midTermTS = -MidTermTS;
                    shortTermTS = -ShortTermTS;
                    midTermTC = 10 - MidTermTC;
                    shortTermTC = 10 - ShortTermTC;
                }
            }

            //Score - TS TC FT ST FTST
            STScore.Content = ShortTermScore.ToString("0.00");
            MTScore.Content = MidTermScore.ToString("0.00");
            if (ShortTermScore > 50) { STScore.Foreground = Brushes.Lime; }
            else if (ShortTermScore <= 50) { STScore.Foreground = Brushes.Red; }

            if (MidTermScore > 50) { MTScore.Foreground = Brushes.Lime; }
            else if (MidTermScore <= 50) { MTScore.Foreground = Brushes.Red; }

            // Trend Bars
            if (shortTermTrendBar == 1.0) { STtB.Content = "Bullish"; STtB.Foreground = Brushes.ForestGreen; }
            else if (shortTermTrendBar == 0) { STtB.Content = ""; STtB.Foreground = Brushes.Black; }
            else if (shortTermTrendBar == -1.0) { STtB.Content = "Bearish"; STtB.Foreground = Brushes.Crimson; }

            if (midTermTrendBar == 1.0) { MTtB.Content = "Bullish"; MTtB.Foreground = Brushes.ForestGreen; }
            else if (midTermTrendBar == 0) { MTtB.Content = ""; MTtB.Foreground = Brushes.Black; }
            else if (midTermTrendBar == -1.0) { MTtB.Content = "Bearish"; MTtB.Foreground = Brushes.Crimson; }

            // Trend Lines
            if (shortTermTrendLine == 1.0) { STtL.Content = "Bullish"; STtL.Foreground = Brushes.ForestGreen; }
            else if (shortTermTrendLine == 0) { STtL.Content = ""; STtL.Foreground = Brushes.Black; }
            else if (shortTermTrendLine == -1.0) { STtL.Content = "Bearish"; STtL.Foreground = Brushes.Crimson; }

            if (midTermTrendLine == 1.0) { MTtL.Content = "Bullish"; MTtL.Foreground = Brushes.ForestGreen; }
            else if (midTermTrendLine == 0) { MTtL.Content = ""; MTtL.Foreground = Brushes.Black; }
            else if (midTermTrendLine == -1.0) { MTtL.Content = "Bearish"; MTtL.Foreground = Brushes.Crimson; }

            // Trend Strength
            if (midTermTS == 4) { MTtsb.Content = "Tran Up"; MTtsb.Foreground = Brushes.Yellow; }
            else if (midTermTS == 3) { MTtsb.Content = "Quick Up"; MTtsb.Foreground = Brushes.Cyan; }
            else if (midTermTS == 2) { MTtsb.Content = "Strong Up"; MTtsb.Foreground = Brushes.Lime; }
            else if (midTermTS == 1) { MTtsb.Content = "Early Up"; MTtsb.Foreground = Brushes.Green; }
            else if (midTermTS == 0) { MTtsb.Content = ""; MTtsb.Foreground = Brushes.White; }
            else if (midTermTS == -1) { MTtsb.Content = "Early Dn"; MTtsb.Foreground = Brushes.Crimson; }
            else if (midTermTS == -2) { MTtsb.Content = "Strong Dn"; MTtsb.Foreground = Brushes.Red; }
            else if (midTermTS == -3) { MTtsb.Content = "Quick Dn"; MTtsb.Foreground = Brushes.Magenta; }
            else if (midTermTS == -4) { MTtsb.Content = "Tran Dn"; MTtsb.Foreground = Brushes.DarkOrange; }

            if (shortTermTS == 4) { STtsb.Content = "Tran Up"; STtsb.Foreground = Brushes.Yellow; }
            else if (shortTermTS == 3) { STtsb.Content = "Quick Up"; STtsb.Foreground = Brushes.Cyan; }
            else if (shortTermTS == 2) { STtsb.Content = "Strong Up"; STtsb.Foreground = Brushes.Lime; }
            else if (shortTermTS == 1) { STtsb.Content = "Early Up"; STtsb.Foreground = Brushes.Green; }
            else if (shortTermTS == 0) { STtsb.Content = ""; STtsb.Foreground = Brushes.White; }
            else if (shortTermTS == -1) { STtsb.Content = "Early Dn"; STtsb.Foreground = Brushes.Crimson; }
            else if (shortTermTS == -2) { STtsb.Content = "Strong Dn"; STtsb.Foreground = Brushes.Red; }
            else if (shortTermTS == -3) { STtsb.Content = "Quick Dn"; STtsb.Foreground = Brushes.Magenta; }
            else if (shortTermTS == -4) { STtsb.Content = "Tran Dn"; STtsb.Foreground = Brushes.DarkOrange; }

            //  Trend Condition

            if (midTermTC == 1) { MTtc.Content = "Bullish"; MTtc.Foreground = Brushes.Cyan; }
            else if (midTermTC == 2) { MTtc.Content = "Bullish"; MTtc.Foreground = Brushes.Lime; }
            else if (midTermTC == 3) { MTtc.Content = "Bullish"; MTtc.Foreground = Brushes.ForestGreen; }
            else if (midTermTC == 4) { MTtc.Content = "Bullish"; MTtc.Foreground = Brushes.Yellow; }
            else if (midTermTC == 5) { MTtc.Content = ""; MTtc.Foreground = Brushes.White; }
            else if (midTermTC == 6) { MTtc.Content = "Bearish"; MTtc.Foreground = Brushes.DarkOrange; }
            else if (midTermTC == 7) { MTtc.Content = "Bearish"; MTtc.Foreground = Brushes.Crimson; }
            else if (midTermTC == 8) { MTtc.Content = "Bearish"; MTtc.Foreground = Brushes.Red; }
            else if (midTermTC == 9) { MTtc.Content = "Bearish"; MTtc.Foreground = Brushes.Magenta; }

            if (shortTermTC == 1) { STtc.Content = "Bullish"; STtc.Foreground = Brushes.Cyan; }
            else if (shortTermTC == 2) { STtc.Content = "Bullish"; STtc.Foreground = Brushes.Lime; }
            else if (shortTermTC == 3) { STtc.Content = "Bullish"; STtc.Foreground = Brushes.ForestGreen; }
            else if (shortTermTC == 4) { STtc.Content = "Bullish"; STtc.Foreground = Brushes.Yellow; }
            else if (shortTermTC == 5) { STtc.Content = ""; STtc.Foreground = Brushes.White; }
            else if (shortTermTC == 6) { STtc.Content = "Bearish"; STtc.Foreground = Brushes.DarkOrange; }
            else if (shortTermTC == 7) { STtc.Content = "Bearish"; STtc.Foreground = Brushes.Crimson; }
            else if (shortTermTC == 8) { STtc.Content = "Bearish"; STtc.Foreground = Brushes.Red; }
            else if (shortTermTC == 9) { STtc.Content = "Bearish"; STtc.Foreground = Brushes.Magenta; }

            // FT Going Up Dn STftUpdn
            var ftGoingUpSt = ShortTermFT > ShortTermFT1;
            var ftGoingDnSt = ShortTermFT < ShortTermFT1;

            if (ftGoingUpSt && ShortTermPR == 1.5) { STftUpdn.Content = "Bullish"; STftUpdn.Foreground = Brushes.Lime; }
            else if (ftGoingUpSt && ShortTermPR == -1.0) { STftUpdn.Content = "Bear Retrace"; STftUpdn.Foreground = Brushes.DarkOrange; }

            if (ftGoingDnSt && ShortTermPR == -1.5) { STftUpdn.Content = "Bearish"; STftUpdn.Foreground = Brushes.Red; }
            else if (ftGoingDnSt && ShortTermPR == 1.0) { STftUpdn.Content = "Bull Retrace"; STftUpdn.Foreground = Brushes.Yellow; }

            var ftGoingUpMt = MidTermFT > MidTermFT1;
            var ftGoingDnMt = MidTermFT < MidTermFT1;

            if (ftGoingUpMt && MidTermPR == 1.5) { MTftUpDn.Content = "Bullish"; MTftUpDn.Foreground = Brushes.Lime; }
            else if (ftGoingUpMt && MidTermPR == -1.0) { MTftUpDn.Content = "Bear Retrace"; MTftUpDn.Foreground = Brushes.DarkOrange; }

            if (ftGoingDnMt && MidTermPR == -1.5) { MTftUpDn.Content = "Bearish"; MTftUpDn.Foreground = Brushes.Red; }
            else if (ftGoingDnMt && MidTermPR == 1.0) { MTftUpDn.Content = "Bull Retrace"; MTftUpDn.Foreground = Brushes.Yellow; }

            // ST Going Up Dn
            var stGoingUpSt = ShortTermST > ShortTermST1;
            var stGoingDnSt = ShortTermST < ShortTermST1;

            if (stGoingUpSt && ShortTermST >= 75) { STstUpDn.Content = "Strong"; STstUpDn.Foreground = Brushes.Cyan; }
            else if ((stGoingUpSt && ShortTermST > 25) && (stGoingUpSt && ShortTermST < 75)) { STstUpDn.Content = "Up"; STstUpDn.Foreground = Brushes.Lime; }
            else if ((stGoingUpSt && ShortTermST > 0) && (stGoingUpSt && ShortTermST <= 25)) { STstUpDn.Content = "Weak"; STstUpDn.Foreground = Brushes.Magenta; }

            if (stGoingDnSt && ShortTermST <= 25) { STstUpDn.Content = "Weak"; STstUpDn.Foreground = Brushes.Magenta; }
            else if ((stGoingDnSt && ShortTermST > 25) && (stGoingDnSt && ShortTermST < 75)) { STstUpDn.Content = "Dn"; STstUpDn.Foreground = Brushes.Red; }
            else if ((stGoingDnSt && ShortTermST > 75) && (stGoingDnSt && ShortTermST < 100)) { STstUpDn.Content = "Strong"; STstUpDn.Foreground = Brushes.Cyan; }

            var stGoingUpMt = MidTermST > MidTermST1;
            var stGoingDnMt = MidTermST < MidTermST1;

            if (stGoingUpMt && MidTermST >= 75) { MTstUpDn.Content = "Strong"; MTstUpDn.Foreground = Brushes.Cyan; }
            else if ((stGoingUpMt && MidTermST > 25) && (stGoingUpMt && MidTermST < 75)) { MTstUpDn.Content = "Up"; MTstUpDn.Foreground = Brushes.Lime; }
            else if ((stGoingUpMt && MidTermST > 0) && (stGoingUpMt && MidTermST <= 25)) { MTstUpDn.Content = "Weak"; MTstUpDn.Foreground = Brushes.Magenta; }

            if (stGoingDnMt && MidTermST <= 25) { MTstUpDn.Content = "Weak"; MTstUpDn.Foreground = Brushes.Magenta; }
            else if ((stGoingDnMt && MidTermST > 25) && (stGoingDnMt && MidTermST < 75)) { MTstUpDn.Content = "Dn"; MTstUpDn.Foreground = Brushes.Red; }
            else if ((stGoingDnMt && MidTermST > 75) && (stGoingDnMt && MidTermST < 100)) { MTstUpDn.Content = "Strong"; MTstUpDn.Foreground = Brushes.Cyan; }

            // ST to FT
            if ((ShortTermST - ShortTermFT >= 1) && (ShortTermST - ShortTermFT < 100)) { STstToft.Content = "Bullish"; STstToft.Foreground = Brushes.Lime; }
            if ((ShortTermFT - ShortTermST >= 1) && (ShortTermFT - ShortTermST < 100)) { STstToft.Content = "Bearish"; STstToft.Foreground = Brushes.Red; }

            if ((MidTermST - MidTermFT >= 1) && (MidTermST - MidTermFT < 100)) { MTstToft.Content = "Bullish"; MTstToft.Foreground = Brushes.Lime; }
            if ((MidTermFT - MidTermST >= 1) && (MidTermFT - MidTermST < 100)) { MTstToft.Content = "Bearish"; MTstToft.Foreground = Brushes.Red; }

            recSTpr.Fill = (ShortTermPR == 1.5) ? Brushes.Lime : (ShortTermPR == 1.0) ? Brushes.Yellow : (ShortTermPR == -1.0) ? Brushes.DarkOrange : (ShortTermPR == -1.5) ? Brushes.Red : Brushes.Black;
            recSTsc.Fill = (ShortTermScore > 50) ? Brushes.Lime : (ShortTermScore <= 50) ? Brushes.Red : Brushes.Black;
            recSTft.Fill = ftGoingUpSt ? Brushes.Lime : ftGoingDnSt ? Brushes.Red : Brushes.Black;
            recSTst.Fill = stGoingUpSt ? Brushes.Lime : stGoingDnSt ? Brushes.Red : Brushes.Black;
            recSTTSB.Fill = ShortTermTS > 1 ? Brushes.Lime : ShortTermTS < 1 ? Brushes.Red : Brushes.Black;

            recMTpr.Fill = (MidTermPR == 1.5) ? Brushes.Lime : (MidTermPR == 1.0) ? Brushes.Yellow : (MidTermPR == -1.0) ? Brushes.DarkOrange : (MidTermPR == -1.5) ? Brushes.Red : Brushes.Black;
            recMTsc.Fill = (MidTermScore > 50) ? Brushes.Lime : (MidTermScore <= 50) ? Brushes.Red : Brushes.Black;
            recMTft.Fill = ftGoingUpMt ? Brushes.Lime : ftGoingDnMt ? Brushes.Red : Brushes.Black;
            recMTst.Fill = stGoingUpMt ? Brushes.Lime : stGoingDnMt ? Brushes.Red : Brushes.Black;
            recMTtsb.Fill = MidTermTS > 1 ? Brushes.Lime : MidTermTS < 1 ? Brushes.Red : Brushes.Black;

            PRPR.Fill = (MidTermPR == 1.5 && ShortTermPR == 1.5) ? Brushes.ForestGreen : (MidTermPR == -1.5 && ShortTermPR == -1.5) ? Brushes.DarkRed : Brushes.Black;
            PRFT.Fill = (MidTermPR == 1.5 && ftGoingUpSt) ? Brushes.ForestGreen : (MidTermPR == -1.5 && ftGoingDnSt) ? Brushes.DarkRed : Brushes.Black;
            PRSC.Fill = (MidTermPR == 1.5 && ShortTermScore > 50) ? Brushes.ForestGreen : (MidTermPR == -1.5 && ShortTermScore <= 50) ? Brushes.DarkRed : Brushes.Black;
            PRST.Fill = (MidTermPR == 1.5 && stGoingUpSt) ? Brushes.ForestGreen : (MidTermPR == -1.5 && stGoingDnSt) ? Brushes.DarkRed : Brushes.Black;
            PRTSB.Fill = (MidTermPR == 1.5 && ShortTermTS > 1) ? Brushes.ForestGreen : (MidTermPR == -1.5 && ShortTermTS < 1) ? Brushes.DarkRed : Brushes.Black;

            SCPR.Fill = (MidTermScore > 50 && ShortTermPR == 1.5) ? Brushes.ForestGreen : (MidTermScore <= 50 && ShortTermPR == -1.5) ? Brushes.DarkRed : Brushes.Black;
            SCFT.Fill = (MidTermScore > 50 && ftGoingUpSt) ? Brushes.ForestGreen : (MidTermScore <= 50 && ftGoingDnSt) ? Brushes.DarkRed : Brushes.Black;
            SCSC.Fill = (MidTermScore > 50 && ShortTermScore > 50) ? Brushes.ForestGreen : (MidTermScore <= 50 && ShortTermScore <= 50) ? Brushes.DarkRed : Brushes.Black;
            SCST.Fill = (MidTermScore > 50 && stGoingUpSt) ? Brushes.ForestGreen : (MidTermScore <= 50 && stGoingDnSt) ? Brushes.DarkRed : Brushes.Black;
            SCTSB.Fill = (MidTermScore > 50 && ShortTermTS > 1) ? Brushes.ForestGreen : (MidTermScore <= 50 && ShortTermTS < 1) ? Brushes.DarkRed : Brushes.Black;

            FTPR.Fill = (ftGoingUpMt && ShortTermPR == 1.5) ? Brushes.ForestGreen : (ftGoingDnMt && ShortTermPR == -1.5) ? Brushes.DarkRed : Brushes.Black;
            FTFT.Fill = (ftGoingUpMt && ftGoingUpSt) ? Brushes.ForestGreen : (ftGoingDnMt && ftGoingDnSt) ? Brushes.DarkRed : Brushes.Black;
            FTSC.Fill = (ftGoingUpMt && ShortTermScore > 50) ? Brushes.ForestGreen : (ftGoingDnMt && ShortTermScore <= 50) ? Brushes.DarkRed : Brushes.Black;
            FTST.Fill = (ftGoingUpMt && stGoingUpSt) ? Brushes.ForestGreen : (ftGoingDnMt && stGoingDnSt) ? Brushes.DarkRed : Brushes.Black;
            FTTSB.Fill = (ftGoingUpMt && ShortTermTS > 1) ? Brushes.ForestGreen : (ftGoingDnMt && ShortTermTS < 1) ? Brushes.DarkRed : Brushes.Black;

            STPR.Fill = (stGoingUpMt && ShortTermPR == 1.5) ? Brushes.ForestGreen : (stGoingDnMt && ShortTermPR == -1.5) ? Brushes.DarkRed : Brushes.Black;
            STFT.Fill = (stGoingUpMt && ftGoingUpSt) ? Brushes.ForestGreen : (stGoingDnMt && ftGoingDnSt) ? Brushes.DarkRed : Brushes.Black;
            STSC.Fill = (stGoingUpMt && ShortTermScore > 50) ? Brushes.ForestGreen : (stGoingDnMt && ShortTermScore <= 50) ? Brushes.DarkRed : Brushes.Black;
            STST.Fill = (stGoingUpMt && stGoingUpSt) ? Brushes.ForestGreen : (stGoingDnMt && stGoingDnSt) ? Brushes.DarkRed : Brushes.Black;
            STTSB.Fill = (stGoingUpMt && ShortTermTS > 1) ? Brushes.ForestGreen : (stGoingDnMt && ShortTermTS < 1) ? Brushes.DarkRed : Brushes.Black;

            TSBPR.Fill = (MidTermTS > 1 && ShortTermPR == 1.5) ? Brushes.ForestGreen : (MidTermTS < 1 && ShortTermPR == -1.5) ? Brushes.DarkRed : Brushes.Black;
            TSBFT.Fill = (MidTermTS > 1 && ftGoingUpSt) ? Brushes.ForestGreen : (MidTermTS < 1 && ftGoingDnSt) ? Brushes.DarkRed : Brushes.Black;
            TSBSC.Fill = (MidTermTS > 1 && ShortTermScore > 50) ? Brushes.ForestGreen : (MidTermTS < 1 && ShortTermScore <= 50) ? Brushes.DarkRed : Brushes.Black;
            TSBST.Fill = (MidTermTS > 1 && stGoingUpSt) ? Brushes.ForestGreen : (MidTermTS < 1 && stGoingDnSt) ? Brushes.DarkRed : Brushes.Black;
            TSBTSB.Fill = (MidTermTS > 1 && ShortTermTS > 1) ? Brushes.ForestGreen : (MidTermTS < 1 && ShortTermTS < 1) ? Brushes.DarkRed : Brushes.Black;

            SupResPanel.Children.Clear();
            foreach (var x in _supResList)
            {
                if (getEnable(x.MidTerm, x))
                {
                    var grid = new Grid();
                    grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(15) });  // mid term OHLC
                    grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(50) });  // midterm price
                    grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1.0, GridUnitType.Star) });  // rect
                    grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(50) });  // short term price
                    grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(15) });  // short term OHLC

                    if (x.Type == AnalysisType.Close)
                    {
                        var tb1 = new TextBlock();
                        tb1.FontSize = 9;
                        tb1.FontFamily = new FontFamily("Helvetica Neue");
                        tb1.Padding = new Thickness(2, 2, 2, 2);
                        tb1.Text = "C";
                        tb1.Foreground = (x.Direction1 > 0) ? Brushes.Lime : (x.Direction1 < 0) ? Brushes.Red : Brushes.White;
                        tb1.HorizontalAlignment = HorizontalAlignment.Center;
                        Grid.SetColumn(tb1, 0);

                        var tb2 = new TextBlock();
                        tb2.FontSize = 9;
                        tb2.FontFamily = new FontFamily("Helvetica Neue");
                        tb2.Padding = new Thickness(2, 2, 2, 2);
                        tb2.Text = x.Value.ToString("0.00");
                        tb2.Foreground = Brushes.White;
                        tb2.HorizontalAlignment = HorizontalAlignment.Center;
                        Grid.SetColumn(tb2, 1);

                        var rect = new Rectangle();
                        rect.Height = 1;
                        rect.Width = 5;
                        rect.Fill = Brushes.White;
                        Grid.SetColumn(rect, 2);

                        var tb3 = new TextBlock();
                        tb3.FontSize = 9;
                        tb3.FontFamily = new FontFamily("Helvetica Neue");
                        tb3.Padding = new Thickness(2, 2, 2, 2);
                        tb3.Text = x.Value.ToString("0.00");
                        tb3.Foreground = Brushes.White;
                        tb3.HorizontalAlignment = HorizontalAlignment.Center;
                        Grid.SetColumn(tb3, 3);

                        var tb4 = new TextBlock();
                        tb4.FontSize = 9;
                        tb4.FontFamily = new FontFamily("Helvetica Neue");
                        tb4.Padding = new Thickness(2, 2, 2, 2);
                        tb4.Text = "C";
                        tb4.Foreground = (x.Direction2 > 0) ? Brushes.Lime : (x.Direction2 < 0) ? Brushes.Red : Brushes.White;
                        tb4.HorizontalAlignment = HorizontalAlignment.Center;
                        Grid.SetColumn(tb4, 4);

                        grid.Children.Add(tb1);
                        grid.Children.Add(tb2);
                        grid.Children.Add(rect);
                        grid.Children.Add(tb3);
                        grid.Children.Add(tb4);
                    }

                    else if (x.Type == AnalysisType.Open || x.Type == AnalysisType.High || x.Type == AnalysisType.Low)
                    {
                        var tb1 = new TextBlock();
                        tb1.FontSize = 9;
                        tb1.FontFamily = new FontFamily("Helvetica Neue");
                        tb1.Padding = new Thickness(2, 2, 2, 2);
                        tb1.Text = x.Type == AnalysisType.Open ? "O" : x.Type == AnalysisType.High ? "H" : "L";
                        tb1.Foreground = Brushes.White;
                        tb1.HorizontalAlignment = HorizontalAlignment.Center;
                        tb1.Foreground = (x.Direction1 > 0) ? Brushes.Lime : (x.Direction1 < 0) ? Brushes.Red : Brushes.White;
                        Grid.SetColumn(tb1, x.MidTerm ? 0 : 4);

                        var tb2 = new TextBlock();
                        tb2.FontSize = 9;
                        tb2.FontFamily = new FontFamily("Helvetica Neue");
                        tb2.Padding = new Thickness(2, 2, 2, 2);
                        tb2.Text = x.Value.ToString("0.00");
                        tb2.Foreground = Brushes.White;
                        tb2.HorizontalAlignment = HorizontalAlignment.Center;
                        Grid.SetColumn(tb2, x.MidTerm ? 1 : 3);

                        grid.Children.Add(tb1);
                        grid.Children.Add(tb2);
                    }
                    else
                    {
                        var tb = new TextBlock();
                        tb.Foreground = getBrush(x.MidTerm, x);
                        tb.FontSize = 9;
                        tb.FontFamily = new FontFamily("Helvetica Neue");
                        tb.Padding = new Thickness(2, 2, 2, 2);
                        tb.Text = x.Value.ToString("0.00");
                        tb.HorizontalAlignment = HorizontalAlignment.Center;
                        Grid.SetColumn(tb, x.MidTerm ? 1 : 3);

                        grid.Children.Add(tb);
                    }
                    SupResPanel.Children.Add(grid);
                }
            }
        }

        private bool getEnable(bool midTerm, AnalysisInfo input)
        {
            var output = true;
            if (!midTerm && input.Type == AnalysisType.Sig) output = Use3SigST.IsChecked == true;
            else if (midTerm && input.Type == AnalysisType.Sig) output = Use3SigMT.IsChecked == true;
            else if (!midTerm && input.Type == AnalysisType.ATMTarget) output = UseTargetST.IsChecked == true;
            else if (midTerm && input.Type == AnalysisType.ATMTarget) output = UseTargetMT.IsChecked == true;
            else if (!midTerm && input.Type == AnalysisType.FTTP) output = UseFTTPST.IsChecked == true;
            else if (midTerm && input.Type == AnalysisType.FTTP) output = UseFTTPMT.IsChecked == true;
            else if (!midTerm && input.Type == AnalysisType.STTP) output = UseSTTPST.IsChecked == true;
            else if (midTerm && input.Type == AnalysisType.STTP) output = UseSTTPMT.IsChecked == true;
            else if (!midTerm && input.Type == AnalysisType.Pivot) output = UsePivotST.IsChecked == true;
            else if (midTerm && input.Type == AnalysisType.Pivot) output = UsePivotMT.IsChecked == true;
            else if (!midTerm && input.Type == AnalysisType.FibPivot) output = UseFibPivotST.IsChecked == true;
            else if (midTerm && input.Type == AnalysisType.FibPivot) output = UseFibPivotMT.IsChecked == true;
            return output;
        }

        private Brush getBrush(bool midTerm, AnalysisInfo input)
        {
            var output = Brushes.White;
            if (!midTerm && input.Type == AnalysisType.ATMTarget) output = Brushes.Red;
            else if ( midTerm && input.Type == AnalysisType.ATMTarget) output = Brushes.Red;
            else if (!midTerm && input.Type == AnalysisType.Sig) output = Brushes.DarkOrange;
            else if (midTerm && input.Type == AnalysisType.Sig) output = Brushes.DarkOrange;
            else if (!midTerm && input.Type == AnalysisType.FTTP) output = Brushes.Lime;
            else if (midTerm && input.Type == AnalysisType.FTTP) output = Brushes.Lime;
            else if (!midTerm && input.Type == AnalysisType.STTP) output = Brushes.Cyan;
            else if (midTerm && input.Type == AnalysisType.STTP) output = Brushes.Cyan;
            else if (!midTerm && input.Type == AnalysisType.Pivot) output = Brushes.Magenta;
            else if (midTerm && input.Type == AnalysisType.Pivot) output = Brushes.Magenta;
            else if (!midTerm && input.Type == AnalysisType.FibPivot) output = Brushes.Yellow;
            else if (midTerm && input.Type == AnalysisType.FibPivot) output = Brushes.Yellow;
            return output;
        }

        private class AnalysisInfo
        {
            public AnalysisInfo(double value, AnalysisType type, bool midTerm, double direction1 = 0.0, double direction2 = 0.0)
            {
                Value = value;
                Type = type;
                MidTerm = midTerm;
                Direction1 = direction1;
                Direction2 = direction2;
            }

            public double Value;
            public AnalysisType Type;
            public bool MidTerm;
            public double Direction1;
            public double Direction2;
        }

        private void updateTurningPoint(Label label, double upVal, double dnVal)
        {
            if (!double.IsNaN(upVal) && upVal != 0)
            {
                label.Content = upVal.ToString("0.00");
                label.Foreground = (ShortTermClose > upVal) ? Brushes.Lime : Brushes.Gray;
            }
            else if (!double.IsNaN(dnVal) && dnVal != 0)
            {
                label.Content = dnVal.ToString("0.00");
                label.Foreground = (ShortTermClose < dnVal) ? Brushes.Red : Brushes.Gray;
            }
        }

        private void SupRes_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SupRes.Visibility = Visibility.Visible;
            SupResNav.Visibility = Visibility.Visible;
            Trend.Visibility = Visibility.Collapsed;
            TrendNav.Visibility = Visibility.Collapsed;
            Predict.Visibility = Visibility.Collapsed;
            PredictNav.Visibility = Visibility.Collapsed;
            Trade.Visibility = Visibility.Collapsed;
            TradeNav.Visibility = Visibility.Collapsed;
        }

        private void Predict_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SupRes.Visibility = Visibility.Collapsed;
            SupResNav.Visibility = Visibility.Collapsed;
            Trend.Visibility = Visibility.Collapsed;
            TrendNav.Visibility = Visibility.Collapsed;
            Predict.Visibility = Visibility.Visible;
            PredictNav.Visibility = Visibility.Visible;
            Trade.Visibility = Visibility.Collapsed;
            TradeNav.Visibility = Visibility.Collapsed;
        }

        private void Trend_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SupRes.Visibility = Visibility.Collapsed;
            SupResNav.Visibility = Visibility.Collapsed;
            Trend.Visibility = Visibility.Visible;
            TrendNav.Visibility = Visibility.Visible;
            Predict.Visibility = Visibility.Collapsed;
            PredictNav.Visibility = Visibility.Collapsed;
            Trade.Visibility = Visibility.Collapsed;
            TradeNav.Visibility = Visibility.Collapsed;
        }
        private void Trade_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SupRes.Visibility = Visibility.Collapsed;
            SupResNav.Visibility = Visibility.Collapsed;
            Trend.Visibility = Visibility.Collapsed;
            TrendNav.Visibility = Visibility.Collapsed;
            Predict.Visibility = Visibility.Collapsed;
            PredictNav.Visibility = Visibility.Collapsed;
            Trade.Visibility = Visibility.Visible;
            TradeNav.Visibility = Visibility.Visible;
        }

        private void Trade_MouseEnter(object sender, MouseEventArgs e)
        {
            var label = sender as Control;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
        }
        private void Trade_MouseLeave(object sender, MouseEventArgs e)
        {
            var label = sender as Control;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0x00, 0x00));
        }
        private void OurView_MouseEnter(object sender, MouseEventArgs e)
        {
            var label = sender as Control;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
        }
        private void OurView_MouseLeave(object sender, MouseEventArgs e)
        {
            var label = sender as Control;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
        }

        private void UseATM_Checked(object sender, RoutedEventArgs e)
        {
            Draw();
        }
        private void UseATM_Unchecked(object sender, RoutedEventArgs e)
        {
            Draw();
        }

        private void Long_MouseDown(object sender, MouseButtonEventArgs e)
        {
            sendOrderEvent(new OrderEventArgs(OrderEventType.Long));
        }
        private void LongAdd_MouseDown(object sender, MouseButtonEventArgs e)
        {
            sendOrderEvent(new OrderEventArgs(OrderEventType.LongAdd));
        }
        private void LongReduce_MouseDown(object sender, MouseButtonEventArgs e)
        {
            sendOrderEvent(new OrderEventArgs(OrderEventType.LongReduce));
        }
        private void LongClose_MouseDown(object sender, MouseButtonEventArgs e)
        {
            sendOrderEvent(new OrderEventArgs(OrderEventType.LongClose));
        }
        private void Short_MouseDown(object sender, MouseButtonEventArgs e)
        {
            sendOrderEvent(new OrderEventArgs(OrderEventType.Short));
        }
        private void ShortAdd_MouseDown(object sender, MouseButtonEventArgs e)
        {
            sendOrderEvent(new OrderEventArgs(OrderEventType.ShortAdd));
        }
        private void ShortReduce_MouseDown(object sender, MouseButtonEventArgs e)
        {
            sendOrderEvent(new OrderEventArgs(OrderEventType.ShortReduce));
        }
        private void ShortClose_MouseDown(object sender, MouseButtonEventArgs e)
        {
            sendOrderEvent(new OrderEventArgs(OrderEventType.ShortClose));
        }
    }
}
