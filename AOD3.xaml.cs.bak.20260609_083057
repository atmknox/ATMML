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
    public class AOD3Input
    {
        public string Interval = "";
        public string ModelName = "";
        public int ShortTermIndex = 0;
        public Dictionary<string, bool> SCAddEnbs = new Dictionary<string, bool>();
        public Dictionary<string, object> ReferenceData = new Dictionary<string, object>();
        public List<DateTime> ShortTermTimes = new List<DateTime>();
        public List<DateTime> MidTermTimes = new List<DateTime>();
        public List<DateTime> LongTermTimes = new List<DateTime>();
        public Series[] ShortTermSeries = new Series[4];
        public Series[] MidTermSeries = new Series[4];
        public Series[] LongTermSeries = new Series[4];
        public Dictionary<DateTime, double> ShortTermPredictions = new Dictionary<DateTime, double>();
        public Dictionary<DateTime, double> ShortTermActuals = new Dictionary<DateTime, double>();
        public Dictionary<DateTime, double> MidTermPredictions = new Dictionary<DateTime, double>();
        public Dictionary<DateTime, double> MidTermActuals = new Dictionary<DateTime, double>();
    }
    public partial class AOD3 : UserControl
    {
        public event OrderEventHandler OrderEvent;
        public event AodEventHandler AodEvent;

        private Dictionary<DateTime, double> ShortTermPredictions = new Dictionary<DateTime, double>();
        private Dictionary<DateTime, double> ShortTermActuals = new Dictionary<DateTime, double>();
        private Dictionary<DateTime, double> MidTermPredictions = new Dictionary<DateTime, double>();
        private Dictionary<DateTime, double> MidTermActuals = new Dictionary<DateTime, double>();

        private bool _next = false;

        public AOD3()
        {
            DataContext = this;
            InitializeComponent();
            Interval = "Daily";

            ModelName = "";

            MouseEnter += AOD3_MouseEnter;
            MouseLeave += AOD3_MouseLeave;

            IntervalSelector.ItemsSource = Intervals;
            IntervalSelector.Width = 15;
            IntervalSelector.ToolTip = "Change Intervals";
            IntervalSelector.Padding = new Thickness(0, 1, 0, 1);
            IntervalSelector.Height = 13;
            IntervalSelector.Margin = new Thickness(0, 0, 0, 0);
            IntervalSelector.FontSize = 7;
            IntervalSelector.Foreground = Brushes.White;
            IntervalSelector.Background = Brushes.Transparent;
            IntervalSelector.SelectionChanged += Interval_SelectionChanged;
            var style = Application.Current.Resources["ComboBoxStyle1"] as Style;
            IntervalSelector.Style = style;

            TickerSymbol.Visibility = Visibility.Visible;
            //TickerSymbol.IsReadOnly = true;
            TickerSymbol.BorderThickness = new Thickness(0);
            TickerSymbol.FontSize = 10;
            TickerSymbol.CharacterCasing = CharacterCasing.Upper;
            TickerSymbol.Background = Brushes.Transparent;
            //TickerSymbol.Foreground = new SolidColorBrush(_foregroundColor);
            TickerSymbol.AllowDrop = true;
            TickerSymbol.GotFocus += new RoutedEventHandler(TickerSymbol_GotFocus);
            TickerSymbol.LostFocus += new RoutedEventHandler(TickerSymbol_LostFocus);
            TickerSymbol.PreviewMouseLeftButtonUp += new MouseButtonEventHandler(TickerSymbol_PreviewMouseLeftButtonUp);
            TickerSymbol.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(TickerSymbol_PreviewMouseLeftButtonDown);
            TickerSymbol.PreviewDrop += new DragEventHandler(TickerSymbol_PreviewDrop);
            TickerSymbol.PreviewKeyDown += TickerSymbol_PreviewKeyDown;
            TickerSymbol.KeyDown += TickerSymbol_KeyDown;

            AODPanel.KeyDown += new KeyEventHandler(KeyDown);
            AODPanel.KeyUp += new KeyEventHandler(KeyUp);
            AODPanel.TextInput += new TextCompositionEventHandler(TextInput);
        }

        bool _symbolDrop = false;
        bool _controlModifier = false;
        bool _shiftModifier = false;
        //bool _clearEdit = true;
        bool _editingSymbol = false;

        void TickerSymbol_PreviewDrop(object sender, DragEventArgs e)
        {
            TickerSymbol.Text = "";
            _symbolDrop = true;
        }

        void TickerSymbol_LostFocus(object sender, RoutedEventArgs e)
        {
            _editingSymbol = false;
            TickerSymbol.Background = Brushes.Transparent;
            TickerSymbol.Foreground = Brushes.Silver;
        }

        void TickerSymbol_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            TickerSymbol.SelectAll();
            TickerSymbol.Background = Brushes.Transparent;
            TickerSymbol.Foreground = Brushes.Silver;
        }

        void TickerSymbol_GotFocus(object sender, RoutedEventArgs e)
        {
            _editingSymbol = true;
            TickerSymbol.SelectAll();
            TickerSymbol.Background = Brushes.Transparent;
            TickerSymbol.Foreground = Brushes.Silver;
        }

        void TickerSymbol_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!TickerSymbol.IsKeyboardFocusWithin)
            {
                TickerSymbol.Focus();
                e.Handled = true;
            }
        }

        private void TickerSymbol_KeyDown(object sender, KeyEventArgs e)
        {
        }

        private void TickerSymbol_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            KeyDown(sender, e);
        }

        void KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                _controlModifier = false;
            }
            else if (e.Key == Key.LeftShift || e.Key == Key.RightShift)
            {
                _shiftModifier = false;
            }
        }

        void KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                _controlModifier = true;
            }
            else if (e.Key == Key.LeftShift || e.Key == Key.RightShift)
            {
                _shiftModifier = true;
            }
            else if (e.Key == Key.Space)
            {
                TickerSymbol.Text += " ";
                e.Handled = true;
            }
            else if (e.Key == Key.Back)
            {
                if (TickerSymbol.Text.Length > 0)
                {
                    var index = TickerSymbol.SelectionStart;
                    if (index == 0 || index >= TickerSymbol.Text.Length) index = TickerSymbol.Text.Length - 1;
                    TickerSymbol.Text = TickerSymbol.Text.Remove(index, 1);
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.F1)
            {
                string command = TickerSymbol.Text;
                command += " People";
                TickerSymbol.Text = command;
                e.Handled = true;
            }
            else if (e.Key == Key.F2)
            {
                string command = TickerSymbol.Text;
                command += " Govt";
                TickerSymbol.Text = command;
                e.Handled = true;
            }
            else if (e.Key == Key.F3)
            {
                string command = TickerSymbol.Text;
                command += " Corp";
                TickerSymbol.Text = command;
                e.Handled = true;
            }
            else if (e.Key == Key.F4)
            {
                string command = TickerSymbol.Text;
                command += " Mtge";
                TickerSymbol.Text = command;
                e.Handled = true;
            }
            else if (e.Key == Key.F5)
            {
                string command = TickerSymbol.Text;
                command += " M-Mkt";
                TickerSymbol.Text = command;
                e.Handled = true;
            }
            else if (e.Key == Key.F6)
            {
                string command = TickerSymbol.Text;
                command += " Muni";
                TickerSymbol.Text = command;
                e.Handled = true;
            }
            else if (e.Key == Key.F7)
            {
                string command = TickerSymbol.Text;
                command += " Pfd";
                TickerSymbol.Text = command;
                e.Handled = true;
            }
            else if (e.Key == Key.F8)
            {
                string command = TickerSymbol.Text;
                command += " Equity";
                TickerSymbol.Text = command;
                e.Handled = true;
            }
            else if (e.Key == Key.F9)
            {
                string command = TickerSymbol.Text;
                command += " Comdty";
                TickerSymbol.Text = command;
                e.Handled = true;
            }
            else if (e.Key == Key.F10 || e.Key == Key.System)
            {
                string command = TickerSymbol.Text;
                command += " Index";
                TickerSymbol.Text = command;
                e.Handled = true;
            }
            else if (e.Key == Key.F11)
            {
                string command = TickerSymbol.Text;
                command += " Curncy";
                TickerSymbol.Text = command;
                e.Handled = true;
            }
            else if (e.Key == Key.F12)
            {
                string command = TickerSymbol.Text;
                command += " Client";
                TickerSymbol.Text = command;
                e.Handled = true;
            }
            else if (e.Key == Key.Return)
            {
                processSymbolEdit();
                e.Handled = true;
            }
        }

        void processSymbolEdit()
        {
            string command = TickerSymbol.Text;
            command = command.Trim();

            string[] fields = command.Split(ATMML.Symbol.SpreadCharacter);
            command = "";
            for (int ii = 0; ii < fields.Length; ii++)
            {
                if (ii > 0) command += ATMML.Symbol.SpreadCharacter;
                string symbol = fields[ii].Trim();
                if (!symbol.Contains(' ') && !Portfolio.isCQGSymbol(symbol))
                {
                    symbol += " US Equity";
                }
                command += symbol;
            }

            if (command.Length > 0)
            {
                processCommand(command);
                command = "";
                //TickerSymbol.Text = command;
            }
        }

        private void processCommand(string command)
        {
            Symbol = command;
            _editingSymbol = false;
            //_clearEdit = true;
            sendAodEvent(new AodEventArgs(AodEventType.Symbol));
        }

        void TextInput(object sender, TextCompositionEventArgs e)
        {
            //string command = _clearEdit ? "" : TickerSymbol.Text;
            string command = TickerSymbol.Text;
            //_clearEdit = false;

            string text = e.Text;

            if (text == "\b")
            {
                if (command.Length > 0)
                {
                    command = command.Substring(0, command.Length - 1);
                    TickerSymbol.Text = command;
                }
            }
            else if (text == "\r")
            {
                command = command.Trim();
                if (command.Length > 0)
                {
                    processCommand(command);
                    command = "";
                    TickerSymbol.Text = command;
                }
            }
            else
            {
                text = text.ToUpper();
                command += text;
                TickerSymbol.Text = command;
            }
        }

        public string Interval { get; set; }
        public string ModelName { get; set; }


        private void Interval_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var cb = sender as ComboBox;
            var time = cb.SelectedItem as string;
            if (time != null)
            {
                cb.SelectedValue = "";

                if (time == "D") time = "Daily";
                else if (time == "W") time = "Weekly";
                else if (time == "M") time = "Monthly";
                else if (time == "Q") time = "Quarterly";
                else if (time == "S") time = "SemiAnnually";
                else if (time == "Y") time = "Yearly";
                Interval = time;
                sendAodEvent(new AodEventArgs(AodEventType.Interval));
            }
        }

        private List<string> Intervals
        {
            get
            {
                string[] intervals = { "M", "W", "D", "240", "120", "60", "30", "15", "5" };
                return intervals.ToList();
            }
        }

        private void AOD3_MouseLeave(object sender, MouseEventArgs e)
        {
            BlueRec.Visibility = Visibility.Visible;
            WhiteRec.Visibility = Visibility.Hidden;
        }

        private void AOD3_MouseEnter(object sender, MouseEventArgs e)
        {
            WhiteRec.Visibility = Visibility.Visible;
            BlueRec.Visibility = Visibility.Hidden;
        }

        private void sendAodEvent(AodEventArgs e)
        {
            if (AodEvent != null)
            {
                AodEvent(this, e);
            }
        }
        private double _accuracy;


        public List<Tuple<String, Color>> GetAdvice()
        {
            return _advice1;
        }

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

            _accuracy = ((double)(truePositive + trueNegative) / total);
        }

        string _forecastPriceType = "";
        double _referencePrice1 = double.NaN;
        double _referencePrice2 = double.NaN;

        public void update(AOD3Input input)
        {
            // input
            var interval0 = input.Interval;
            var interval1 = GetOverviewInterval(interval0, 1);
            ShortTermInterval = interval0;
            MidTermInterval = interval1;

            var scAddEnbs = input.SCAddEnbs;

            var referenceData = input.ReferenceData;
            var shortTermTimes = input.ShortTermTimes;
            var midTermTimes = input.MidTermTimes;
            //var longTermTimes = input.LongTermTimes;
            var shortTermSeries = input.ShortTermSeries;
            var midTermSeries = input.MidTermSeries;
            //var longTermSeries = input.LongTermSeries;
            int shortTermCurrentBarIndex = input.ShortTermIndex;

            ShortTermPredictions = input.ShortTermPredictions;
            ShortTermActuals = input.ShortTermActuals;

            if (ShortTermPredictions.Count > 1)
            {
                var stDateCurrent = shortTermTimes[shortTermCurrentBarIndex - 1];
                var stDateNext = shortTermTimes[shortTermCurrentBarIndex];
                ShortTermPredictionCurrent = ShortTermPredictions.ContainsKey(stDateCurrent) ? ShortTermPredictions[stDateCurrent] : 0;
                ShortTermPredictionNext = ShortTermPredictions.ContainsKey(stDateNext) ? ShortTermPredictions[stDateNext] : 0;
            }

            updateMatrix(shortTermTimes);

            if (shortTermTimes.Count > 1)
            {
                Time = shortTermTimes[shortTermCurrentBarIndex];

                ShortTermClose = shortTermSeries[3][shortTermCurrentBarIndex];
                ShortTermClose1 = shortTermSeries[3][shortTermCurrentBarIndex - 1];

                var ago = 101;

                var ft_st = atm.calculateFT(shortTermSeries[1], shortTermSeries[2], shortTermSeries[3]);

                var st_st = atm.calculateST(shortTermSeries[1], shortTermSeries[2], shortTermSeries[3]);

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
            }

            if (shortTermTimes.Count > 0 && midTermTimes.Count > 0)
            {
                var _scores = calculateScore(shortTermTimes, shortTermSeries, midTermTimes, midTermSeries, interval0);

                ShortTermScore = _scores[shortTermCurrentBarIndex];
            }

            Dictionary<string, List<DateTime>> times = new Dictionary<string, List<DateTime>>();
            times[ShortTermInterval] = shortTermTimes;
            times[MidTermInterval] = midTermTimes;
            Dictionary<string, Series[]> bars = new Dictionary<string, Series[]>();
            bars[ShortTermInterval] = shortTermSeries;
            bars[MidTermInterval] = midTermSeries;
            var intervals = new string[]{ ShortTermInterval, MidTermInterval };

            //_advice1 = atm.getAction(Symbol, times, bars, intervals, referenceData, scAddEnbs, shortTermCurrentBarIndex);
            _advice1 = atm.getAdvice(Symbol, times, bars, intervals, referenceData, scAddEnbs, shortTermCurrentBarIndex);

            var scenario = getScenario();
            var text1 = scenario.Trim();
            var index1 = text1.IndexOf("|");
            if (index1 != -1)
            {
                var referencePrice = text1.Substring(index1 + 2, 1).Replace("O", "O").Replace("H", "H").Replace("L", "L").Replace("C", "C");
                var forecastPrice = text1.Substring(0, 1).Replace("O", "O").Replace("H", "H").Replace("L", "L").Replace("C", "C");
                var referenceIndex = int.Parse(text1.Substring(text1.Length - 1, 1)) - 1;
                var forecastIndex = int.Parse(text1.Substring(index1 - 2, 1));

                var idx1 = shortTermCurrentBarIndex - referenceIndex - 1;
                var rp1 = shortTermSeries[3][idx1];
                if (referencePrice == "O") rp1 = shortTermSeries[0][idx1];
                else if (referencePrice == "H") rp1 = shortTermSeries[1][idx1];
                else if (referencePrice == "L") rp1 = shortTermSeries[2][idx1];

                var idx2 = shortTermCurrentBarIndex - referenceIndex;
                var rp2 = shortTermSeries[3][idx2];
                if (referencePrice == "O") rp1 = shortTermSeries[0][idx2];
                else if (referencePrice == "H") rp1 = shortTermSeries[1][idx2];
                else if (referencePrice == "L") rp1 = shortTermSeries[2][idx2];

                var bias = getBias();

                _forecastPriceType = forecastPrice;
                _referencePrice1 = rp1 + bias;
                _referencePrice2 = rp2 + bias;
            }
        }

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
        public string Description;
        public string PoP;
        public string PxProj;
        public string ShortTermInterval;
        public string MidTermInterval;
        public DateTime Time;

        public double ShortTermPredictionCurrent;
        public double ShortTermPredictionNext;
        public double ShortTermClose;
        public double ShortTermClose1;
        public double ShortTermScore;
        public double ShortTermFTTPUpNext;
        public double ShortTermFTTPUp;
        public double ShortTermFTTPDnNext;
        public double ShortTermFTTPDn;
        public double ShortTermSTTPUpNext;
        public double ShortTermSTTPUp;
        public double ShortTermSTTPDnNext;
        public double ShortTermSTTPDn;

        List<Tuple<string, Color>> _advice1 = new List<Tuple<string, Color>>();  // Advice


        public void Clear()
        {
            _accuracy = double.NaN;

            ShortTermPredictionCurrent = double.NaN;
            ShortTermPredictionNext = double.NaN;
            ShortTermClose = double.NaN;
            ShortTermScore = double.NaN;

            ShortTermFTTPUp = 0;
            ShortTermFTTPDn = 0;
            ShortTermFTTPUpNext = 0;
            ShortTermFTTPDnNext = 0;

            Cl.Content = "";
            STScore.Content = "";

            _advice1.Clear();
            for (int ii = 0; ii < 5; ii++) _advice1.Add(new Tuple<string, Color>("", Colors.Black));

            STScore.Content = "";
            STPredict.Text = "";
            ForecastPrice.Content = "";
            AdjustedPrice.Content = "";
            Accuracy.Content = "";
            StrategyPosition.Content = "";
            ModelPosition.Content = "";
            ForecastPosition.Content = "";
        }

        private void drawMatrix()
        {
            Accuracy.Content = (double.IsNaN(_accuracy)) ? "" : (100 * _accuracy).ToString("0.00");
        }

        string getIntervalLabel(string input)
        {
            var output = input;
            if (input == "Q") output = "Quarterly";
            else if (input == "M") output = "Month";
            else if (input == "W") output = "Weekly";
            else if (input == "D") output = "Daily";
            return output;
        }

        public void CopyToClipboard()
        {
            BitmapSource source = captureAODimage();
            Clipboard.SetData(DataFormats.Bitmap, source);
        }

        private BitmapSource captureAODimage()
        {
            Visual target = AODPanel;

            double dpiX = 300;
            double dpiY = 300;

            Rect bounds = VisualTreeHelper.GetDescendantBounds(target);
            RenderTargetBitmap rtb = new RenderTargetBitmap((int)(bounds.Width * dpiX / 96.0), (int)(bounds.Height * dpiY / 96.0), dpiX, dpiY, PixelFormats.Pbgra32);
            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext ctx = dv.RenderOpen())
            {
                VisualBrush vb = new VisualBrush(target);
                ctx.DrawRectangle(vb, null, new Rect(new Point(), bounds.Size));
            }
            rtb.Render(dv);

            return rtb;
        }

        string getScenario()
        {
            var output = "";
            var model = MainView.GetModel(ModelName);
            if (model != null)
            {
                output = MainView.GetSenarioLabel(model.Scenario);
            }
            return output;
        }

        double getBias()
        {
            var output = 0.0;
            var model = MainView.GetModel(ModelName);
            if (model != null)
            {
                var key = Symbol + ":" + Interval;
                if (model.Biases.ContainsKey(key))
                {
                    output = model.Biases[key];
                }
            }
            return output;
        }

        public void DrawClose(double close0, double close1)
        {
            ShortTermClose = close0;
            ShortTermClose1 = close1;

            if (ShortTermClose1 > ShortTermClose)
            {
                Cl.Foreground = Brushes.Red;
            }
            else if (ShortTermClose1 < ShortTermClose)
            {
                Cl.Foreground = Brushes.Lime;
            }
            Cl.Content = (double.IsNaN(ShortTermClose)) ? "" : ShortTermClose.ToString("0.00");
        }

        public void DrawAdvice(List<Tuple<String, Color>> advice)
        {
            _advice1 = advice;
            Draw();
        }

        public void Draw()
        {
            if (ModelName == "No Prediction")
            {
                Accuracy.Content = ""; 
                ForecastPrice.Visibility = Visibility.Hidden;
                STPredict.Visibility = Visibility.Hidden; 
                AdjustedPrice.Visibility = Visibility.Hidden;
                Current.Visibility = Visibility.Hidden;
                Next.Visibility = Visibility.Hidden;
            }
            else
            {
                ForecastPrice.Visibility = Visibility.Visible;
                STPredict.Visibility = Visibility.Visible;
                AdjustedPrice.Visibility = Visibility.Visible;
                Current.Visibility = Visibility.Visible;
                Next.Visibility = Visibility.Visible;
            }

            var activeBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
            Current.Foreground = _next ? Brushes.White : activeBrush;
            Next.Foreground = _next ? activeBrush : Brushes.White;

            if (!_editingSymbol) TickerSymbol.Text = Symbol;
            //Desc.Content = Description;
            IntervalText.Text = getIntervalAbbreviation(Interval);

            Model.Content = ModelName;
            Scenario.Content = getScenario();

            CurrentPosition.Content = "Current "  + getIntervalLabel(ShortTermInterval) + " Model Position";  //Current Position
            CurrentRec.Content = "Current "  + getIntervalLabel(ShortTermInterval) + " Model Position Analysis";
            NewRec.Content = "Next " + getIntervalLabel(ShortTermInterval) + " Model Position Analysis";

            if (_advice1 != null && _advice1.Count >= 5) 
            {
                StrategyPosition.Content = _advice1[1].Item1;
                StrategyPosition.Foreground = new SolidColorBrush(_advice1[1].Item2);
                ModelPosition.Content = _advice1[2].Item1;
                ModelPosition.Foreground = new SolidColorBrush(_advice1[2].Item2);
                ForecastPosition.Content = _advice1[3].Item1;
                ForecastPosition.Foreground = new SolidColorBrush(_advice1[3].Item2);
                PressureMessage.Content = _advice1[4].Item1;
                PressureMessage.Foreground = new SolidColorBrush(_advice1[4].Item2);
            }

            drawMatrix();

            var prediction = _next ? ShortTermPredictionNext : ShortTermPredictionCurrent;
            STPredict.Text = double.IsNaN(prediction) ? "" : (prediction > 0) ? "\u25B2" : "\u25BC";
            STPredict.Foreground = double.IsNaN(prediction) ? Brushes.Transparent : (prediction > 0) ? Brushes.Lime : Brushes.Red;
            ForecastPrice.Content = _forecastPriceType;
            var referencePrice = _next ? _referencePrice2 : _referencePrice1;
            AdjustedPrice.Content = double.IsNaN(referencePrice) ? "" : referencePrice.ToString("0.00") + " | Acc";

            if (ShortTermClose1 > ShortTermClose)
            {
                Cl.Foreground = Brushes.Red;
            }
            else if (ShortTermClose1 < ShortTermClose)
            {
                Cl.Foreground = Brushes.Lime;
            }
            Cl.Content = (double.IsNaN(ShortTermClose)) ? "" : ShortTermClose.ToString("0.00");

            var ClosetoClose = 100 * (ShortTermClose - ShortTermClose1) / ShortTermClose;
            if (ShortTermClose1 > ShortTermClose)
            {
                //clToCl.Foreground = Brushes.Red;
            }
            else if (ShortTermClose1 < ShortTermClose)
            {
                //clToCl.Foreground = Brushes.Lime;
            }
            //clToCl.Content = (double.IsNaN(ClosetoClose)) ? "" : ClosetoClose.ToString("0.00");

            char ch1 = '\u2b63';
            char ch2 = '\u2b62';

            STScore.Content = (double.IsNaN(ShortTermScore)) ? "" : ShortTermScore.ToString("0.00");

            if (ShortTermScore > 50) { STScore.Foreground = Brushes.Lime; }
            else if (ShortTermScore <= 50) { STScore.Foreground = Brushes.Red; }
            else if (ShortTermScore <= 0) { STScore.Foreground = Brushes.Transparent; }
        }

        private class AnalysisInfo
        {
            public AnalysisInfo(double value, AnalysisType type, bool midTerm, double direction1 = 0.0, double direction2 = 0.0, bool isEqual = false)
            {
                Value = value;
                Type = type;
                MidTerm = midTerm;
                Direction1 = direction1;
                Direction2 = direction2;
                IsEqual = isEqual;
            }

            public double Value;
            public AnalysisType Type;
            public bool MidTerm;
            public double Direction1;
            public double Direction2;
            public bool IsEqual;
        }

        private void OurView_MouseEnter(object sender, MouseEventArgs e)
        {
            var label = sender as Control;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
        }
        private void OurView_MouseLeave(object sender, MouseEventArgs e)
        {
            var label = sender as Control;
            var active = (label == Current && !_next) || (label == Next && _next);
            if (!active) label.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
        }

        private void CloseAOD_MouseDown(object sender, MouseButtonEventArgs e)
        {
            sendAodEvent(new AodEventArgs(AodEventType.Close));
            e.Handled = true;
        }

        private void AddNewAOD_MouseDown(object sender, MouseButtonEventArgs e)
        {
            sendAodEvent(new AodEventArgs(AodEventType.Add));
            e.Handled = true;
        }

        private void SourceButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            sendAodEvent(new AodEventArgs(AodEventType.Source));
            e.Handled = true;
        }

        private void SelectModel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            sendAodEvent(new AodEventArgs(AodEventType.Model));
            e.Handled = true;
        }

        private void SelectCurrent_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _next = false;
            Draw();
        }
        private void SelectNext_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _next = true;
            Draw();
        }

        private void SaveMonitorSettings_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            sendAodEvent(new AodEventArgs(AodEventType.Save));
            e.Handled = true;
        }

        public void Close()
        {
            TickerSymbol.GotFocus -= new RoutedEventHandler(TickerSymbol_GotFocus);
            TickerSymbol.LostFocus -= new RoutedEventHandler(TickerSymbol_LostFocus);
            TickerSymbol.PreviewMouseLeftButtonUp -= new MouseButtonEventHandler(TickerSymbol_PreviewMouseLeftButtonUp);
            TickerSymbol.PreviewMouseLeftButtonDown -= new MouseButtonEventHandler(TickerSymbol_PreviewMouseLeftButtonDown);
            TickerSymbol.PreviewDrop -= new DragEventHandler(TickerSymbol_PreviewDrop);
        }

        private void MouseRec_MouseDown(object sender, MouseButtonEventArgs e)
        {
            sendAodEvent(new AodEventArgs(AodEventType.Chart));
        }
    }
}
