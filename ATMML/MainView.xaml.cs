using Python.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;

namespace ATMML
{
    public delegate void MainEventHandler(object sender, MainEventArgs e);

    public enum MainEventType
    {
        PCMOrder = 1
    }

    public class MainEventArgs : EventArgs
    {
        MainEventType _id = MainEventType.PCMOrder;

        public MainEventArgs(MainEventType id)
        {
            _id = id;
        }

        public MainEventType Id
        {
            get { return _id; }
        }
    }

    public partial class MainView : Window
    {
        static string _id = "";
        static string _username;
        static bool _loginOk = true;

        private DateTime _progressTime;

        static bool _rick = false;
        static bool _sqpt = false;
        static bool _testUser = false;


        static bool _closeConfirm = true;

        // Views
        static bool _enableMaps = false;
        static bool _enableAlerts = false;
        static bool _enableServer = false;
        static bool _enablePositions = false;
        static bool _enableDiary = false;
        static bool _enableResults = false;
        static bool _enableAutoTrade = false;
        static bool _enableNetworks = false;
        static bool _enableLinks = false;

        // Portfolios
        static bool _enablePortfolio = true;
        static bool _enableUS100 = false;
        static bool _enableUS500 = false;
        static bool _enableMajorETF = false;
        static bool _enableRickStocks = false;  // need to break out other portfolios
        static bool _enableFinancial = false;   // use only for Rick 
        static bool _enablePINZPortfolio = false;
        static bool _enableSQPTPortfolio = false;
        static bool _enableGlobalIndices = false;
        static bool _enablePINZOrders = false;
        static bool _enablePINZExport = false;
        static bool _enablePRTUExport = false;
        static bool _enablePRTUHistory = false;
        static bool _enableGuiPortfolio = false;
        // Chart and Member List 
        static bool _enableTradesOnLargeIntervals = false;
        static bool _enableRecommendations = false;
        static bool _enableCurrentRecommendations = false;
        static bool _enableHistoricalRecommendations = false;
        static bool _enableServerTicker = false;
        // Chart Features
        static bool _enableCursorColor = false;
        static bool _enableEditTrades = false;
        static bool _enablePINZEditTrades = false;
        static bool _enableSQPTEditTrades = false;
        static bool _enableTradeConditions = true;
        static bool _enableChartPositions = false;
        static bool _enableScore = false;
        static bool _enablePR = false;
        static bool _enableRP = false;
        static bool _enableTRT = false;
        static bool _enableADX = false;
        // Scans
        static bool _enableHappenedWithinConditions = false;
        static bool _enableSpreadScans = true;
        static bool _enableServerOutput = false;
        static bool _enableStrategies = false;
        // Navigation
        static bool _enableCMR = false;

        struct Setting
        {
            public Setting(string i_value, bool i_changed)
            {
                value = i_value; changed = i_changed;
            }

            public bool changed;
            public string value;
        }
        public Dictionary<string, string> Parameters { get; set; }

        static Dictionary<string, Setting> _settings = new Dictionary<string, Setting>();
        static bool _saveSettings = false;

        static public string CalculatorSettings { get; set; }

        static int conditionCount = 6;
        static string[,] _conditions = new string[4, conditionCount];

        static string[,] _hashmarkConditions = new string[2, 4];

        static Dictionary<string, AnticipatedAction> _reviewSymbols = new Dictionary<string, AnticipatedAction>();

        static Dictionary<string, Dictionary<string, Research>> _research = new Dictionary<string, Dictionary<string, Research>>();

        string _view = "";

        bool _bypassStartup = false;

        bool _hideChartCursor = false;

        DispatcherTimer _timer = new DispatcherTimer();

        static bool _positionsAvailable = false;

        Dictionary<string, List<IPDFDocument>> docPages = new Dictionary<string, List<IPDFDocument>>();

        public static string Username { get { return _username; } }

        // APPLICATION ENABLEMENTS

        // Restrict Views 
        public static bool EnableMaps { get { return _enableMaps; } }  //  Maps view enablement  
        public static bool EnableAlerts { get { return _enableAlerts; } }  //  Alets view enablement 
        public static bool EnablePositions { get { return _enablePositions; } }  //  Positions View enablement
        public static bool EnableNetworks { get { return _enableNetworks; } }  //  Networks view enablement 
        public static bool EnableResults { get { return _enableResults; } }  //  Results view enablement 
        public static bool EnableDiary { get { return _enableDiary; } }  //  Diary view enablement 
        public static bool EnableAutoTrade { get { return _enableAutoTrade; } }  //  Auto Trade view enablement 
        public static bool EnableLinks { get { return _enableLinks; } }  //  Chart and Acct Link 
        public static bool EnableServer { get { return _enableServer; } }

        // Portfolios - one of these is added to each portfolio
        public static bool EnablePortfolio { get { return _enablePortfolio; } } // enable portfolio for all clients
        public static bool EnableUS100 { get { return _enableUS100; } }  // US 100  Portfolio
        public static bool EnableUS500 { get { return _enableUS500; } }  // NEW
        public static bool EnableMajorETF { get { return _enableMajorETF; } }  // Major ETF  Portfolio
        public static bool EnableRickStocks { get { return _enableRickStocks; } }  // this is used everywhere - fix this
        public static bool EnableFinancial { get { return _enableFinancial; } }  // used to control items in Chart Legend
        public static bool EnableGlobalIndices { get { return _enableGlobalIndices; } }
        public static bool EnablePINZPortfolio { get { return _enablePINZPortfolio; } }  // PINZ Portfolio 
        public static bool EnablePINZOrders { get { return _enablePINZOrders; } }  // PINZ Order View
        public static bool EnablePINZExport { get { return _enablePINZExport; } }  // PINZ Export of Orders 
        public static bool EnablePRTUExport { get { return _enablePRTUExport; } }  // PRTU Export in Server
        public static bool EnablePRTUHistory { get { return _enablePRTUHistory; } }  // PRTU History Feature in Server
        public static bool EnableSQPTPortfolio { get { return _enableSQPTPortfolio; } }  // SQPT Portfolio 
        public static bool EnableGuiPortfolio { get { return _enableGuiPortfolio; } }  // SQPT Portfolio 

        // Chart and Member List recommendations - ask whether recommendations are on Chart or in Mmember List 
        public static bool EnableTradesOnLargeIntervals { get { return _enableTradesOnLargeIntervals; } }  // allows clients to view trades on longer time frames d,w,m recommendations  
        public static bool EnableRecommendations { get { return _enableRecommendations; } }
        public static bool EnableCurrentRecommendations { get { return EnableRecommendations; } }
        public static bool EnableHistoricalRecommendations { get { return _enableHistoricalRecommendations; } }  //  Chart Dialog Historical Recommendations 
        public static bool EnableServerTicker { get { return _enableServerTicker; } }  //  Ticker Mousedown in Server View Spreadsheet
  
        public static bool PositionsAvailable { get { return _positionsAvailable; } set { _positionsAvailable = value; } }  //currently in maps - allows positions in Maps  

        // Chart features  
        public static bool EnableCursorColor { get { return _enableCursorColor; } }     // cursor coloring feature               
        public static bool EnableEditTrades { get { return _enableEditTrades; } }
        public static bool EnablePINZEditTrades { get { return _enablePINZEditTrades; } }
        public static bool EnableSQPTEditTrades { get { return _enableSQPTEditTrades; } }
        public static bool EnableTradeConditions { get { return _enableTradeConditions; } } //  in Scans Trade condition is only for me and adam   
        public static bool EnableChartPositions { get { return _enableChartPositions; } }
        public static bool EnableScore { get { return _enableScore; } }
        public static bool EnablePR { get { return _enablePR; } }
        public static bool EnableRP { get { return _enableRP; } }
        public static bool EnableTRT { get { return _enableTRT; } }
        public static bool EnableADX { get { return _enableADX; } }

        // Scan features      
        public static bool EnableHappenedWithinConditions { get { return _enableHappenedWithinConditions; } }  //  in Scans Happens Within condition is only for me       
        public static bool EnableSpreadScans { get { return _enableSpreadScans; } }  //  Allows Scanning of Spreads    
        public static bool EnableServerOutput { get { return _enableServerOutput; } }  // Allows Scans to operate as a server
        public static bool EnableStrategies { get { return _enableStrategies; } }  // Allows Scans to operate as a server

        // Scan Navigation     
        public static bool EnableCMR { get { return _enableCMR; } }

        public event MainEventHandler MainEvent;

        private void onMainEvent(MainEventArgs e)
        {
            if (MainEvent != null)
            {
                MainEvent(this, e);
            }
        }


        public bool HideChartCursor
        {
            get { return _hideChartCursor; }
            set { _hideChartCursor = value; }
        }

        public DateTime StartUpTime { get; private set; }

        void MainViewLoaded(object sender, RoutedEventArgs e)
        {
            this.Closing += OnClose;
            //LoadReportPages();
            StartUpTime = DateTime.Now;

            SelectedMLModel = loadMLModel("EXAMPLE");

            usernameBox.Text = LoadUserData("username");
            //if (usernameBox.Text.Length > 0)
            //{
            //    passwordBox.Focus();
            //}
		}

		public static Model SelectedMLModel { get; set; }

        public static List<string> getModelNames()
        {
            var path = MainView.GetDataFolder() + @"\senarios";
            Directory.CreateDirectory(path);
            var modelNames1 = Directory.EnumerateDirectories(path).Select(x => System.IO.Path.GetFileName(x)).ToList();
            var modelNames2 = modelNames1.Where(x => !x.Contains("-save")).ToList();
            return modelNames2;
        }

        public static List<string> getFactorModelNames()
        {
            var path = MainView.GetDataFolder() + @"\models\MODELS";
            Directory.CreateDirectory(path);
            var modelNames = Directory.EnumerateFiles(path).Select(x => System.IO.Path.GetFileName(x)).ToList();
            return modelNames;
        }


        private static Model loadMLModel(string name)
        {
            Model model = null;
            try
            {
                var path = @"senarios" + @"\" + name + @"\model";
                if (ExistsUserData(path))
                {
                    var data = LoadUserData(path);
                    model = Model.load(data);
                }
            }
            catch (Exception x)
            {
                //Trace.WriteLine(x.Message);
            }
         
            return model;
        }

        public static Model GetModel(string name)
        {
            return loadMLModel(name);
        }

        public static string GetSenarioLabel(Senario input)
        {
            var output = "";

            string text = input.ToString();

            if (input == Senario.ATR50Less5) output = "Volatility(50) .5 or Less";
            else if (input == Senario.ATR505to1) output = "Volatility(50) .5 to 1.0";
            else if (input == Senario.ATR501to15) output = "Volatility(50) 1.0 to 1.5";
            else if (input == Senario.ATR50Greater15) output = "Volatility(50) 1.5 or Higher";

            else if (input == Senario.ATR30Less5) output = "Volatility(30) .5 or Less";
            else if (input == Senario.ATR305to1) output = "Volatility(30) .5 to 1.0";
            else if (input == Senario.ATR301to15) output = "Volatility(30) 1.0 to 1.5";
            else if (input == Senario.ATR30Greater15) output = "Volatility(30) 1.5 or Higher";

            else if (input == Senario.ATR20Less5) output = "Volatility(20) .5 or Less";
            else if (input == Senario.ATR205to1) output = "Volatility(20) .5 to 1.0";
            else if (input == Senario.ATR201to15) output = "Volatility(20) 1.0 to 1.5";
            else if (input == Senario.ATR20Greater15) output = "Volatility(20) 1.5 or Higher";

            else if (input == Senario.ATR10Less5) output = "Volatility(10) .5 or Less";
            else if (input == Senario.ATR105to1) output = "Volatility(10) .5 to 1.0";
            else if (input == Senario.ATR101to15) output = "Volatility(10) 1.0 to 1.5";
            else if (input == Senario.ATR10Greater15) output = "Volatility(10) 1.5 or Higher";

            else if (input == Senario.ATR5Less5) output = "Volatility(5) .5 or Less";
            else if (input == Senario.ATR55to1) output = "Volatility(5) .5 to 1.0";
            else if (input == Senario.ATR51to15) output = "Volatility(5) 1.0 to 1.5";
            else if (input == Senario.ATR5Greater15) output = "Volatility(5) 1.5 or Higher";

            else if (input == Senario.TrendOp11) output = "Op 10% Extreme";
            else if (input == Senario.TrendCl11) output = "Cl 10% Extreme";
            else if (input == Senario.TrendOpCl11) output = "Op Cl 10% Extreme";

            else if (input == Senario.TrendOp) output = "Op 20% Extreme";
            else if (input == Senario.TrendCl) output = "Cl 20% Extreme";
            else if (input == Senario.TrendOpCl) output = "Op Cl 20% Extreme";

            else if (input == Senario.TrendOp31) output = "Op 30% Extreme";
            else if (input == Senario.TrendCl31) output = "Cl 30% Extreme";
            else if (input == Senario.TrendOpCl31) output = "Op Cl 30% Extreme";

            else
            {
                var index1 = text.IndexOf("Ago");
                var index2 = text.IndexOf("Plus");
                var agoPrice = text.Substring(0, 1);
                var plusPrice = text.Substring(index1 + 3, 1);
                var ago = text.Substring(index1 - 1, 1);
                var plus = text.Substring(index2 - 1, 1);
                var minusSign = (ago == "0") ? "-" : "-";
                var plusSign = (plus == "0") ? "+" : "+";
                output =  plusPrice + " " + plusSign + plus + " | " + agoPrice + " " + minusSign + (int.Parse(ago) + 1);
            }

            return output;
        }

        public static Senario GetSenarioFromLabel(string input1, string input2 = "")
        {
            var output = Senario.Close0AgoClose1Plus;

            var input = input1;

            var text = "";
            if (input2.Length > 0)
            {
                var items1 = input1.Split(' ');
                var items2 = input2.Split(' ');

                var forecastPrice = items1[1].Replace("Op", "Open").Replace("Hi", "High").Replace("Lo", "Low").Replace("Cl", "Close");
                var referencePrice = items2[1].Replace("Op", "Open").Replace("Hi", "High").Replace("Lo", "Low").Replace("Cl", "Close");
                var forecastIndex = items1[2].Replace("+", "");
                var referenceIndex = items2[2].Replace("-", "");

                text = referencePrice + (int.Parse(referenceIndex) - 1) + "Ago" + forecastPrice + forecastIndex + "Plus";

                input = items1[1][0] + " " + items1[2]  + " | " + items2[1][0] + " " + items2[2];
            }
            else
            {
                var text1 = input.Trim();
                if (text1.Contains('|'))
                {

                    var fields = text1.Split('|');
                    var fText = fields[0].Trim();
                    var rText = fields[1].Trim();

                    var forecastPrice = fText.Substring(0, 1).Replace("O", "Open").Replace("H", "High").Replace("L", "Low").Replace("C", "Close");
                    var referencePrice = rText.Substring(0, 1).Replace("O", "Open").Replace("H", "High").Replace("L", "Low").Replace("C", "Close");
                    var forecastIndex = fText.Last();
                    var referenceIndex = rText.Last().ToString();
                    text = referencePrice + (int.Parse(referenceIndex) - 1) + "Ago" + forecastPrice + forecastIndex + "Plus";
                }
            }

            Senario senario;
            bool ok = Enum.TryParse(text, out senario);
            if (ok)
            {
                output = senario;
            }
            else if (input == "Volatility(50) .5 or Less") output = Senario.ATR50Less5;
            else if (input == "Volatility(50) .5 to 1.0") output = Senario.ATR505to1;
            else if (input == "Volatility(50) 1.0 to 1.5") output = Senario.ATR501to15;
            else if (input == "Volatility(50) 1.5 or Higher") output = Senario.ATR50Greater15;

            else if (input == "Volatility(30) .5 or Less") output = Senario.ATR30Less5;
            else if (input == "Volatility(30) .5 to 1.0") output = Senario.ATR305to1;
            else if (input == "Volatility(30) 1.0 to 1.5") output = Senario.ATR301to15;
            else if (input == "Volatility(30) 1.5 or Higher") output = Senario.ATR30Greater15;

            else if (input == "Volatility(20) .5 or Less") output = Senario.ATR20Less5;
            else if (input == "Volatility(20) .5 to 1.0") output = Senario.ATR205to1;
            else if (input == "Volatility(20) 1.0 to 1.5") output = Senario.ATR201to15;
            else if (input == "Volatility(20) 1.5 or Higher") output = Senario.ATR20Greater15;

            else if (input == "Volatility(10) .5 or Less") output = Senario.ATR10Less5;
            else if (input == "Volatility(10) .5 to 1.0") output = Senario.ATR105to1;
            else if (input == "Volatility(10) 1.0 to 1.5") output = Senario.ATR101to15;
            else if (input == "Volatility(10) 1.5 or Higher") output = Senario.ATR10Greater15; 

            else if (input == "Volatility(5) .5 or Less") output = Senario.ATR5Less5;
            else if (input == "Volatility(5) .5 to 1.0") output = Senario.ATR55to1;
            else if (input == "Volatility(5) 1.0 to 1.5") output = Senario.ATR51to15;
            else if (input == "Volatility(5) 1.5 or Higher") output = Senario.ATR5Greater15;

            else if (input == "Op 10% Extreme") output = Senario.TrendOp11;
            else if (input == "Cl 10% Extreme") output = Senario.TrendCl11;
            else if (input == "Op Cl 10% Extreme") output = Senario.TrendOpCl11;

            else if (input == "Op 20% Extreme") output = Senario.TrendOp;
            else if (input == "Cl 20% Extreme") output = Senario.TrendCl;
            else if (input == "Op Cl 20% Extreme") output = Senario.TrendOpCl;

            else if (input == "Op 30% Extreme") output = Senario.TrendOp31;
            else if (input == "Cl 30% Extreme") output = Senario.TrendCl31;
            else if (input == "Op Cl 30% Extreme") output = Senario.TrendOpCl31;

            return output;
        }

        public static string ToPath(string input)
        {
            return input.Replace('/', '_');
        }

        public static void AutoMLTrain(string pathName, string scenario = "", string maxTime = "10", string metric = "ACCURACY", string split = "80", bool trained = false)
        {
            var cd = Directory.GetCurrentDirectory();   

            var mode = scenario.Contains("PX") ? "train_regression" : "train_binary";

            var baseDir = MainView.GetDataFolder();

            //            var message = RunCmd(baseDir + @"\AutoML\netcoreapp3.1\ATMMLML.ConsoleApp.exe" + " \"" + baseDir + "\"" + " \"" + pathName + "\" " + mode + " " + maxTime + " " + metric + " " + split + " " + trained);
            //Trace.WriteLine("*************************************************");
            //Trace.WriteLine(message);
            //Trace.WriteLine("*************************************************");

            AutoML.Calculate(new string[] { baseDir, pathName, mode, maxTime, metric, split, trained.ToString() });
        }

        public static Dictionary<string, Dictionary<string, Dictionary<DateTime, double>>> AutoMLPredict(string pathName, string scenario = "")
        {
            var output = new Dictionary<string, Dictionary<string, Dictionary<DateTime, double>>>();
            try
            {
                var mode = scenario.Contains("PX") ? "predict_regression" : "predict_binary";

                var baseDir = MainView.GetDataFolder();
                //var message = RunCmd(baseDir + @"\AutoML\netcoreapp3.1\ATMMLML.ConsoleApp.exe" + " \"" + baseDir + "\"" + " \"" + pathName + "\" " + mode);
                //Trace.WriteLine("*************************************************");
                //Trace.WriteLine(message);
                //Trace.WriteLine("*************************************************");

                AutoML.Calculate(new string[] { baseDir, pathName, mode });
                output = getPredictions(pathName);
            }
            catch (Exception x)
            {

            }
            return output;
        }

        public static bool HasPredictions(string modelName, string symbol, string interval)
        {
            var output = false;
            var model = GetModel(modelName);
            if (model != null)
            {
                var path = @"senarios\" + model.Name + @"\" + interval + (model.UseTicker ? @"\" + MainView.ToPath(symbol) : "") + @"\result.csv";
                var data = LoadUserData(path);
                output = data.Length > 0;
            }
            return output;
        }

        public static Dictionary<string, Dictionary<string, Dictionary<DateTime, double>>> getPredictions(string pathName)
        {
            var output = new Dictionary<string, Dictionary<string, Dictionary<DateTime, double>>>();
            string path = pathName + @"\output.csv";
            var data = LoadUserData(path);
            var lines = data.Split('\n');
            foreach (var line in lines)
            {
                if (line.Length > 0)
                {
                    var fields = line.Split(',');
                    var ticker = fields[0];
                    var items = fields[1].Split(';');
                    var interval1 = items[0];
                    var date = DateTime.ParseExact(items[1], "yyyyMMddHHmmss", null);
                    var prediction = (bool.Parse(fields[2])) ? 1.0 : 0.0;
                    if (!output.ContainsKey(ticker))
                    {
                        output[ticker] = new Dictionary<string, Dictionary<DateTime, double>>();
                    }
                    if (!output[ticker].ContainsKey(interval1))
                    {
                        output[ticker][interval1] = new Dictionary<DateTime, double>();
                    }
                    output[ticker][interval1][date] = prediction;
                }
            }
            return output;
        }

        public static Dictionary<string, Dictionary<string, Dictionary<DateTime, double>>> getActuals(string pathName)
        {
            var output = new Dictionary<string, Dictionary<string, Dictionary<DateTime, double>>>();
            string path = pathName + @"\test.csv";
            var data = LoadUserData(path);
            var lines = data.Split('\n');

            var header = true;
            foreach (var line in lines)
            {
                if (line.Length > 0)
                {
                    if (!header)
                    {
                        var fields = line.Split(',');
                        var ticker = fields[1];
                        var items = fields[2].Split(';');
                        var interval1 = items[0];
                        var date = DateTime.ParseExact(items[1], "yyyyMMddHHmmss", null);
                        var actual = double.Parse(fields[0]);
                        if (!output.ContainsKey(ticker))
                        {
                            output[ticker] = new Dictionary<string, Dictionary<DateTime, double>>();
                        }
                        if (!output[ticker].ContainsKey(interval1))
                        {
                            output[ticker][interval1] = new Dictionary<DateTime, double>();
                        }
                        output[ticker][interval1][date] = actual;
                    }
                    header = false;
                }
            }
            return output;
        }

        public static Dictionary<string, Dictionary<string, Dictionary<DateTime, double>>> getScores(string pathName)
        {
            var output = new Dictionary<string, Dictionary<string, Dictionary<DateTime, double>>>();

            string path = pathName + @"\output.csv";
            var data = LoadUserData(path);
            var lines = data.Split('\n');
            foreach (var line in lines)
            {
                if (line.Length > 0)
                {
                    var fields = line.Split(',');
                    var ticker = fields[0];
                    var items = fields[1].Split(';');
                    var interval1 = items[0];
                    var date = DateTime.ParseExact(items[1], "yyyyMMddHHmmss", null);
                    var score = double.Parse(fields[3]);
                    if (!output.ContainsKey(ticker))
                    {
                        output[ticker] = new Dictionary<string, Dictionary<DateTime, double>>();
                    }
                    if (!output[ticker].ContainsKey(interval1))
                    {
                        output[ticker][interval1] = new Dictionary<DateTime, double>();
                    }
                    output[ticker][interval1][date] = score;
                }
            }

            return output;
        }

        public static bool _appStore = false;
        public static string SendWebRequest(string input)
        {
            string output = "";
            if (!_appStore)
            {
                try
                {
                    WebRequest request = WebRequest.Create(input);
                    WebResponse response = request.GetResponse();
                    Stream stream = response.GetResponseStream();
                    StreamReader reader = new StreamReader(stream);
                    output = reader.ReadToEnd();
                }
                catch (Exception x)
                {
                    //MessageBox.Show("Connection to Server Failed");
                }
            }
            return output;
        }

        public static string GetDataFolder()
        {
            var folder = System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + @"\ATMML"; 

            return folder;
        }

        public static void SaveUserData(string path, string data, bool useLocalDatabase = false, bool append = false)
        {
            if (_appStore)
            {
                //CachedData.SaveData<string>(path.Replace('\', '-'), data);
            }
            else
            {
                var folder = GetDataFolder() + @"\" + path.Replace("|", "(bar)").Replace(":", "(colon)");
                var folders = folder.Split('\\');
                var ok = folders.Length > 0 && folders[folders.Length - 1].Length > 0;
                if (ok)
                {
                    var folderPath = "";
                    for (var ii = 0; ii < folders.Length - 1; ii++)
                    {
                        folderPath += folders[ii];
                        Directory.CreateDirectory(folderPath);
                        folderPath += "\\";
                    }

                    try
                    {
                        var sw = new StreamWriter(folder, append); // Save user settings
                        using (sw)
                        {
                            sw.Write(data);
                        }
                    }
                    catch (Exception x)
                    {
                        //Trace.WriteLine("SaveUserData exception: " + x.Message);
                    }
                }
            }
        }

        public static string LoadUserData(string path, bool useLocalDatabase = false)
        {
            string output = "";
            if (_appStore)
            {
               
            }
            else
            {
                try
                {
                    var folder = GetDataFolder() + @"\" + path.Replace("|", "(bar)").Replace(":", "(colon)");
                    if (File.Exists(folder))
                    {
                        var sr = new StreamReader(folder);
                        using (sr)
                        {
                            output = sr.ReadToEnd();
                        }
                    }
                }
                catch (Exception x)
                {
                    //Trace.WriteLine("LoadUserData exception: " + x.Message);
                }
            }
            return output;
        }

        public static void DeleteUserData(string path, bool useLocalDatabase = false)
        {
            if (_appStore)
            {
                
            }
            else
            {
                try
                {
                    var folder = GetDataFolder() + @"\" + path;
                    if (folder.Length > 0)
                    {
                        if (Directory.Exists(folder))
                        {
                            Directory.Delete(folder, true);
                        }
                        else if (File.Exists(folder)) 
                        {
                            File.Delete(folder);
                        }
                    }
                }
                catch (Exception x)
                {
                    //Trace.WriteLine("DeleteUserData exception: " + x.Message);
                }
            }
        }

        public static bool ExistsUserData(string path, bool useLocalDatabase = false)
        {
            bool output = false;
            if (_appStore)
            {
                
            }
            else
            {
                try
                {
                    var filePath = GetDataFolder() + @"\" + path;
                    output = File.Exists(filePath);
                }
                catch (Exception x)
                {
                    //Trace.WriteLine("ExistsUserData exception: " + x.Message);
                }
            }
            return output;
        }

        private static string RunCmd(string command, int maxTime = 10)
        {
            string returnvalue = string.Empty;

            var processes = Process.GetProcessesByName("ATMMLML.ConsoleApp");
            try
            {
                for (int ii = 0; ii < processes.Length; ii++)
                {
                    processes[ii].Kill();
                }
            }
            catch (Exception x)
            {
                //Trace.WriteLine(x.ToString());
            }

            bool done = false;
            bool timeOut = true;

            Process process = new Process();
            process.StartInfo.FileName = "cmd";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
            {
                if (!String.IsNullOrEmpty(e.Data))
                {
                    var text = e.Data;
                    if (text == "End")
                    {
                        done = true;
                    }
                    else
                    {
                        //Trace.WriteLine(text);
                        returnvalue += text + "\n";
                    }
                }
            });

            process.Start();

            var sw = process.StandardInput;
            sw.WriteLine(command);

            // Asynchronously read the standard output of the spawned process. 
            // This raises OutputDataReceived events for each line of output.
            process.BeginOutputReadLine();

            DateTime time2 = DateTime.Now;
            while (!done)
            {
                var ts = DateTime.Now - time2;
                if (ts.TotalSeconds > maxTime + 30) 
                {
                    timeOut = false;
                    done = true;
                }
            }

            sw.Close();
            process.Close();

            //Trace.WriteLine(command);

            if (!timeOut)
            {
                //Trace.WriteLine("*************************** TIME OUT WAITING FOR TRAINING ********************************");
            }

            return returnvalue;
        }

        private static string RunCmdTest(params string[] commands)
        {
            string returnvalue = string.Empty;

            ProcessStartInfo info = new ProcessStartInfo("cmd");
            info.UseShellExecute = false;
            info.RedirectStandardInput = true;
            info.RedirectStandardOutput = true;
            info.CreateNoWindow = true;

            //try
            //{
            //    var processes = Process.GetProcessesByName("ATMMLML.ConsoleApp");
            //    for (int ii = 0; ii < processes.Length; ii++)
            //    {
            //        processes[ii].Kill();
            //    }
            //}
            //catch
            //{
            //}

            //Trace.WriteLine("Start process for console app.");
            using (Process process = Process.Start(info))
            {
                StreamWriter sw = process.StandardInput; // auto ml
                StreamReader sr = process.StandardOutput;

                //Trace.WriteLine("Start console app: " + commands[0]);
                foreach (string command in commands)
                {
                    sw.WriteLine(command);
                }

                bool ok = true;
                string text = "";

                sw.Close();

                //Trace.WriteLine("Wait for console app to complete.");

                if (ok) {
                    DateTime time2 = DateTime.Now;

                    var done = false;

                    while (!done)
                    {
                        if (sr.Peek() > 0)
                        {
                            text = sr.ReadLine();

                            if (text == "End")
                            {
                                done = true;
                            }
                            else
                            {
                                //Trace.WriteLine(text);
                                returnvalue += text + "\n";
                            }
                        }

                        var ts = DateTime.Now - time2;
                        if (ts.TotalSeconds > 60)
                        {
                            ok = false; // time out waiting for console app to end
                            //Trace.WriteLine("AutoML TIMEOUT");
                            done = true;
                        }
                    } 
                }

                if (!ok)
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                    }
                }

                    //returnvalue = sr.ReadToEnd();
            }

            return returnvalue;
        }

        public void LoadReportPages()
        {
            if (docPages.Count == 0)
            {
                docPages["LgCapCover"] = new List<IPDFDocument>();
                //docPages["LgCapCover"].Add(new BoxCover(this, "COND", 162, true, ""));
                //docPages["LgCapCover"].Add(new LgCapBookFrontBack(this, "COND", 162, true, ""));
                //docPages["LgCapCover"].Add(new LgCapFrontBack(this, "COND", 162, true, ""));
                //docPages["LgCapCover"].Add(new LgCapInsideBackPage(this, "COND"));



                docPages["USLgCapCoverto89"] = new List<IPDFDocument>();   //setup as Internal Publish docs

                //docPages["USLgCapCoverto89"].Add(new CatPubFrontInsidePg(this, "UTIL", 1, true, ""));
                //docPages["USLgCapCoverto89"].Add(new CatFrontPage(this));
                //docPages["USLgCapCoverto89"].Add(new CatInsideLeftCover(this));
                //docPages["USLgCapCoverto89"].Add(new CatPubWho(this, "COND", 1, true, ""));
                //docPages["USLgCapCoverto89"].Add(new CatPubInsideLeft(this, "COND", 2, true, ""));
                //docPages["USLgCapCoverto89"].Add(new CatPubInsideRight(this, "COND", 3, true, ""));
                //docPages["USLgCapCoverto89"].Add(new CatPubIntro(this, "", "COND", 4, true, ""));
                //docPages["USLgCapCoverto89"].Add(new CatPubOurView(this, "US 500", "CMR COND US Lg Cap ST", "", "COND", 5, true, ""));
                //docPages["USLgCapCoverto89"].Add(new CatPubOpenResults(this, "US 500", "CMR COND US Lg Cap ST", "", "COND", 6, true));
                //docPages["USLgCapCoverto89"].Add(new CatPubClosedResults(this, "US 500", "CMR COND US Lg Cap ST", "", "COND", 7, true));
                //docPages["USLgCapCoverto89"].Add(new CatPubOpenResults(this, "US 500", "CMR COND US Lg Cap MT", "", "COND", 8, true));
                //docPages["USLgCapCoverto89"].Add(new CatPubClosedResults(this, "US 500", "CMR COND US Lg Cap MT", "", "COND", 9, true));
                //docPages["USLgCapCoverto89"].Add(new CatPubOpenResults(this, "US 500", "CMR COND US Lg Cap LT", "", "COND", 10, true));
                //docPages["USLgCapCoverto89"].Add(new CatPubClosedResults(this, "US 500", "CMR COND US Lg Cap LT", "", "COND", 11, true));

                ////docPages["USLgCapCoverto89"].Add(new CatIntro(this, "", "CONS", 12, true, ""));
                //docPages["USLgCapCoverto89"].Add(new CatOurView(this, "US 500", "CMR CONS US Lg Cap ST", "", "CONS", 13, true, ""));
                //docPages["USLgCapCoverto89"].Add(new CatOpenResults(this, "US 500", "CMR CONS US Lg Cap ST", "", "CONS", 14, true));
                //docPages["USLgCapCoverto89"].Add(new CatClosedResults(this, "US 500", "CMR CONS US Lg Cap ST", "", "CONS", 15, true));
                //docPages["USLgCapCoverto89"].Add(new CatOpenResults(this, "US 500", "CMR CONS US Lg Cap MT", "", "CONS", 16, true));
                //docPages["USLgCapCoverto89"].Add(new CatClosedResults(this, "US 500", "CMR CONS US Lg Cap MT", "", "CONS", 17, true));
                //docPages["USLgCapCoverto89"].Add(new CatOpenResults(this, "US 500", "CMR CONS US Lg Cap LT", "", "CONS", 18, true));
                //docPages["USLgCapCoverto89"].Add(new CatClosedResults(this, "US 500", "CMR CONS US Lg Cap LT", "", "CONS", 19, true));
                //docPages["USLgCapCoverto89"].Add(new CatIntro(this, "ENRS", 20, true, ""));
                //docPages["USLgCapCoverto89"].Add(new CatOurView(this, "US 500", "CMR ENRS US Lg Cap ST", "", "ENRS", 21, true, ""));
                //docPages["USLgCapCoverto89"].Add(new CatOpenResults(this, "CMR ENRS US Lg Cap ST", "", "ENRS", 22, true));
                //docPages["USLgCapCoverto89"].Add(new CatClosedResults(this, "CMR ENRS US Lg Cap ST", "", "ENRS", 23, true));
                //docPages["USLgCapCoverto89"].Add(new CatOpenResults(this, "CMR ENRS US Lg Cap MT", "", "ENRS", 24, true));
                //docPages["USLgCapCoverto89"].Add(new CatClosedResults(this, "CMR ENRS US Lg Cap MT", "", "ENRS", 25, true));
                //docPages["USLgCapCoverto89"].Add(new CatOpenResults(this, "CMR ENRS US Lg Cap LT", "", "ENRS", 26, true));
                //docPages["USLgCapCoverto89"].Add(new CatClosedResults(this, "CMR ENRS US Lg Cap LT", "", "ENRS", 27, true));
                //docPages["USLgCapCoverto89"].Add(new CatIntro(this, "FINL", 28, true, ""));
                //docPages["USLgCapCoverto89"].Add(new CatOurView(this, "US 500", "CMR FINL US Lg Cap ST", "", "FINL", 29, true, ""));
                //docPages["USLgCapCoverto89"].Add(new CatOpenResults(this, "CMR FINL US Lg Cap ST", "", "FINL", 30, true));
                //docPages["USLgCapCoverto89"].Add(new CatClosedResults(this, "CMR FINL US Lg Cap ST", "", "FINL", 31, true));
                //docPages["USLgCapCoverto89"].Add(new CatOpenResults(this, "CMR FINL US Lg Cap MT", "", "FINL", 32, true));
                //docPages["USLgCapCoverto89"].Add(new CatClosedResults(this, "CMR FINL US Lg Cap MT", "", "FINL", 33, true));
                //docPages["USLgCapCoverto89"].Add(new CatOpenResults(this, "CMR FINL US Lg Cap LT", "", "FINL", 34, true));
                //docPages["USLgCapCoverto89"].Add(new CatClosedResults(this, "CMR FINL US Lg Cap LT", "", "FINL", 35, true));
                //docPages["USLgCapCoverto89"].Add(new CatIntro(this, "HLTH", 36, true, ""));
                //docPages["USLgCapCoverto89"].Add(new CatOurView(this, "US 500", "CMR HLTH US Lg Cap ST", "", "HLTH", 37, true, ""));
                //docPages["USLgCapCoverto89"].Add(new CatOpenResults(this, "CMR HLTH US Lg Cap ST", "", "HLTH", 38, true));
                //docPages["USLgCapCoverto89"].Add(new CatClosedResults(this, "CMR HLTH US Lg Cap ST", "", "HLTH", 39, true));
                //docPages["USLgCapCoverto89"].Add(new CatOpenResults(this, "CMR HLTH US Lg Cap MT", "", "HLTH", 40, true));
                //docPages["USLgCapCoverto89"].Add(new CatClosedResults(this, "CMR HLTH US Lg Cap MT", "", "HLTH", 41, true));
                //docPages["USLgCapCoverto89"].Add(new CatOpenResults(this, "CMR HLTH US Lg Cap LT", "", "HLTH", 42, true));
                //docPages["USLgCapCoverto89"].Add(new CatClosedResults(this, "CMR HLTH US Lg Cap LT", "", "HLTH", 43, true));
                //docPages["USLgCapCoverto89"].Add(new CatIntro(this, "INDU", 44, true, ""));
                //docPages["USLgCapCoverto89"].Add(new CatOurView(this, "US 500", "CMR INDU US Lg Cap ST", "", "INDU", 45, true, ""));
                //docPages["USLgCapCoverto89"].Add(new CatOpenResults(this, "CMR INDU US Lg Cap ST", "", "INDU", 46, true));
                //docPages["USLgCapCoverto89"].Add(new CatClosedResults(this, "CMR INDU US Lg Cap ST", "", "INDU", 47, true));
                //docPages["USLgCapCoverto89"].Add(new CatOpenResults(this, "CMR INDU US Lg Cap MT", "", "INDU", 48, true));
                //docPages["USLgCapCoverto89"].Add(new CatClosedResults(this, "CMR INDU US Lg Cap MT", "", "INDU", 49, true));
                //docPages["USLgCapCoverto89"].Add(new CatOpenResults(this, "CMR INDU US Lg Cap LT", "", "INDU", 50, true));
                //docPages["USLgCapCoverto89"].Add(new CatClosedResults(this, "CMR INDU US Lg Cap LT", "", "INDU", 51, true));
                //docPages["USLgCapCoverto89"].Add(new CatIntro(this, "INFT", 52, true, ""));
                //docPages["USLgCapCoverto89"].Add(new CatOurView(this, "US 500", "CMR INFT US Lg Cap ST", "", "INFT", 53, true, ""));
                //docPages["USLgCapCoverto89"].Add(new CatOpenResults(this, "CMR INFT US Lg Cap ST", "", "INFT", 54, true));
                //docPages["USLgCapCoverto89"].Add(new CatClosedResults(this, "CMR INFT US Lg Cap ST", "", "INFT", 55, true));
                //docPages["USLgCapCoverto89"].Add(new CatOpenResults(this, "CMR INFT US Lg Cap MT", "", "INFT", 56, true));
                //docPages["USLgCapCoverto89"].Add(new CatClosedResults(this, "CMR INFT US Lg Cap MT", "", "INFT", 57, true));
                //docPages["USLgCapCoverto89"].Add(new CatOpenResults(this, "CMR INFT US Lg Cap LT", "", "INFT", 58, true));
                //docPages["USLgCapCoverto89"].Add(new CatClosedResults(this, "CMR INFT US Lg Cap LT", "", "INFT", 59, true));
                //docPages["USLgCapCoverto89"].Add(new CatIntro(this, "MATR", 60, true, ""));
                //docPages["USLgCapCoverto89"].Add(new CatOurView(this, "US 500", "CMR MATR US Lg Cap ST", "", "MATR", 61, true, ""));
                //docPages["USLgCapCoverto89"].Add(new CatOpenResults(this, "CMR MATR US Lg Cap ST", "", "MATR", 62, true));
                //docPages["USLgCapCoverto89"].Add(new CatClosedResults(this, "CMR MATR US Lg Cap ST", "", "MATR", 63, true));
                //docPages["USLgCapCoverto89"].Add(new CatOpenResults(this, "CMR MATR US Lg Cap MT", "", "MATR", 64, true));
                //docPages["USLgCapCoverto89"].Add(new CatClosedResults(this, "CMR MATR US Lg Cap MT", "", "MATR", 65, true));
                //docPages["USLgCapCoverto89"].Add(new CatOpenResults(this, "CMR MATR US Lg Cap LT", "", "MATR", 66, true));
                //docPages["USLgCapCoverto89"].Add(new CatClosedResults(this, "CMR MATR US Lg Cap LT", "", "MATR", 67, true));
                //docPages["USLgCapCoverto89"].Add(new CatIntro(this, "", "TELS", 68, true, ""));
                //docPages["USLgCapCoverto89"].Add(new CatOurView(this, "US 500", "CMR TELS US Lg Cap ST", "", "TELS", 69, true, ""));
                //docPages["USLgCapCoverto89"].Add(new CatOpenResults(this, "US 500", "CMR TELS US Lg Cap ST", "", "TELS", 70, true));
                //docPages["USLgCapCoverto89"].Add(new CatClosedResults(this, "US 500", "CMR TELS US Lg Cap ST", "", "TELS", 71, true));
                //docPages["USLgCapCoverto89"].Add(new CatOpenResults(this, "US 500", "CMR TELS US Lg Cap MT", "", "TELS", 72, true));
                //docPages["USLgCapCoverto89"].Add(new CatClosedResults(this, "US 500", "CMR TELS US Lg Cap MT", "", "TELS", 73, true));
                //docPages["USLgCapCoverto89"].Add(new CatOpenResults(this, "US 500", "CMR TELS US Lg Cap LT", "", "TELS", 74, true));
                //docPages["USLgCapCoverto89"].Add(new CatClosedResults(this, "US 500", "CMR TELS US Lg Cap LT", "", "TELS", 75, true));
                //docPages["USLgCapCoverto89"].Add(new CatIntro(this, "UTIL", 76, true, ""));
                //docPages["USLgCapCoverto89"].Add(new CatOurView(this, "US 500", "CMR UTIL US Lg Cap ST", "", "UTIL", 77, true, ""));
                //docPages["USLgCapCoverto89"].Add(new CatOpenResults(this, "CMR UTIL US Lg Cap ST", "", "UTIL", 78, true));
                //docPages["USLgCapCoverto89"].Add(new CatClosedResults(this, "CMR UTIL US Lg Cap ST", "", "UTIL", 79, true));
                //docPages["USLgCapCoverto89"].Add(new CatOpenResults(this, "CMR UTIL US Lg Cap MT", "", "UTIL", 80, true));
                //docPages["USLgCapCoverto89"].Add(new CatClosedResults(this, "CMR UTIL US Lg Cap MT", "", "UTIL", 81, true));
                //docPages["USLgCapCoverto89"].Add(new CatOpenResults(this, "CMR UTIL US Lg Cap LT", "", "UTIL", 82, true));
                //docPages["USLgCapCoverto89"].Add(new CatClosedResults(this, "CMR UTIL US Lg Cap LT", "", "UTIL", 83, true));
                //docPages["USLgCapCoverto89"].Add(new CatIntro(this, "CMR COUNTRY ETF ST", "CETF", 84, true, ""));
                //docPages["USLgCapCoverto89"].Add(new CatOurView(this, "COUNTRY ETF", "", "", "CETF", 85, true, ""));
                //docPages["USLgCapCoverto89"].Add(new CatOpenResults(this, "COUNTRY ETF", "CMR COUNTRY ETF ST", "", "CETF", 86, true));
                //docPages["USLgCapCoverto89"].Add(new CatClosedResults(this, "COUNTRY ETF", "CMR COUNTRY ETF ST", "", "CETF", 87, true));
                //docPages["USLgCapCoverto89"].Add(new CatOpenResults(this, "COUNTRY ETF", "CMR COUNTRY ETF MT", "", "CETF", 88, true));
                //docPages["USLgCapCoverto89"].Add(new CatClosedResults(this, "COUNTRY ETF", "CMR COUNTRY ETF MT", "", "CETF", 89, true));
                //docPages["USLgCapCoverto89"].Add(new CatOpenResults(this, "COUNTRY ETF", "CMR COUNTRY ETF LT", "", "CETF", 90, true));
                //docPages["USLgCapCoverto89"].Add(new CatClosedResults(this, "COUNTRY ETF", "CMR COUNTRY ETF LT", "", "CETF", 91, true));
                //docPages["USLgCapCoverto89"].Add(new CatPubDisclosure(this, "TMSG", 92, true, ""));
                //docPages["USLgCapCoverto89"].Add(new CatPubTMSG(this, "TMSG", 93, true, ""));
                //docPages["USLgCapCoverto89"].Add(new CatPubProduct(this, "PROD", 94, true, ""));
                //docPages["USLgCapCoverto89"].Add(new CatPubInsideBackCover(this, "UTIL", 1, true, ""));
                //docPages["USLgCapCoverto89"].Add(new CatInsideBackCover(this));
                //docPages["USLgCapCoverto89"].Add(new CatBackPage(this));



                //docPages["USLgCapCoverto89"].Add(new LgCapCover(this, "COND", 1, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapInsideCircles(this, "COND", 2, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapOurView(this, "COND", 1, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapOpenResults(this, "COND", 2, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapClosedResults(this, "COND", 3, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapAttribution(this, "COND", 4, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapRisk(this, "COND", 5, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapMomentumMap(this, "COND", 6, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapMomentum(this, "COND", 7, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapStrengthMap(this, "COND", 8, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapStrength(this, "COND", 9, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapEWMap(this, "COND", 10, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapEWTable(this, "COND", 11, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapValuationMap(this, "COND", 12, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapValuation(this, "COND", 13, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapProfitMap(this, "COND", 14, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapProfit(this, "COND", 15, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapSolvencyMap(this, "COND", 16, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapSolvency(this, "COND", 17, true, ""));

                //docPages["USLgCapCoverto89"].Add(new LgCapInsideCircles(this, "CONS", 18, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapOurView(this, "CONS", 19, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapOpenResults(this, "CONS", 20, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapClosedResults(this, "CONS", 21, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapAttribution(this, "CONS", 22, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapRisk(this, "CONS", 23, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapMomentumMap(this, "CONS", 24, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapMomentum(this, "CONS", 25, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapStrengthMap(this, "CONS", 26, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapStrength(this, "CONS", 27, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapEWMap(this, "CONS", 28, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapEWTable(this, "CONS", 29, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapValuationMap(this, "CONS", 30, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapValuation(this, "CONS", 31, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapProfitMap(this, "CONS", 32, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapProfit(this, "CONS", 33, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapSolvencyMap(this, "CONS", 34, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapSolvency(this, "CONS", 35, true, ""));

                //docPages["USLgCapCoverto89"].Add(new LgCapInsideCircles(this, "ENRS", 36, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapOurView(this, "ENRS", 37, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapOpenResults(this, "ENRS", 38, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapClosedResults(this, "ENRS", 39, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapAttribution(this, "ENRS", 40, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapRisk(this, "ENRS", 41, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapMomentumMap(this, "ENRS", 42, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapMomentum(this, "ENRS", 43, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapStrengthMap(this, "ENRS", 44, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapStrength(this, "ENRS", 45, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapEWMap(this, "ENRS", 46, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapEWTable(this, "ENRS", 47, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapValuationMap(this, "ENRS", 48, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapValuation(this, "ENRS", 49, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapProfitMap(this, "ENRS", 50, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapProfit(this, "ENRS", 51, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapSolvencyMap(this, "ENRS", 52, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapSolvency(this, "ENRS", 53, true, ""));

                //docPages["USLgCapCoverto89"].Add(new LgCapInsideSector(this, "FINL", 54, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapOurView(this, "FINL", 55, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapOpenResults(this, "FINL", 56, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapClosedResults(this, "FINL", 57, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapAttribution(this, "FINL", 58, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapRisk(this, "FINL", 59, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapMomentumMap(this, "FINL", 60, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapMomentum(this, "FINL", 61, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapStrengthMap(this, "FINL", 62, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapStrength(this, "FINL", 63, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapEWMap(this, "FINL", 64, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapEWTable(this, "FINL", 65, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapValuationMap(this, "FINL", 66, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapValuation(this, "FINL", 67, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapProfitMap(this, "FINL", 68, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapProfit(this, "FINL", 69, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapSolvencyMap(this, "FINL", 70, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapSolvency(this, "FINL", 71, true, ""));

                //docPages["USLgCapCoverto89"].Add(new LgCapInsideSector(this, "HLTH", 72, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapOurView(this, "HLTH", 73, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapOpenResults(this, "HLTH", 74, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapClosedResults(this, "HLTH", 75, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapAttribution(this, "HLTH", 76, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapRisk(this, "HLTH", 77, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapMomentumMap(this, "HLTH", 78, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapMomentum(this, "HLTH", 79, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapStrengthMap(this, "HLTH", 80, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapStrength(this, "HLTH", 81, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapEWMap(this, "HLTH", 82, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapEWTable(this, "HLTH", 83, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapValuationMap(this, "HLTH", 84, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapValuation(this, "HLTH", 85, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapProfitMap(this, "HLTH", 86, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapProfit(this, "HLTH", 87, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapSolvencyMap(this, "HLTH", 88, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapSolvency(this, "HLTH", 89, true, ""));


                docPages["USLgCap90toBackPg"] = new List<IPDFDocument>();

                //docPages["USLgCap90toBackPg"].Add(new CatFrontPage(this));
                //docPages["USLgCap90toBackPg"].Add(new CatInsideLeftCover(this));
                //docPages["USLgCap90toBackPg"].Add(new CatWho(this, "COND", 1, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new CatInsideLeft(this, "COND", 2, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new CatInside(this, "COND", 3, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new CatIntro(this, "", "COND", 4, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new CatOurView(this, "US 500", "CMR COND US Lg Cap ST", "", "COND", 5, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new CatOpenResults(this, "US 500", "CMR COND US Lg Cap ST", "", "COND", 6, true));
                //docPages["USLgCap90toBackPg"].Add(new CatClosedResults(this, "US 500", "CMR COND US Lg Cap ST", "", "COND", 7, true));
                //docPages["USLgCap90toBackPg"].Add(new CatOpenResults(this, "US 500", "CMR COND US Lg Cap MT", "", "COND", 8, true));
                //docPages["USLgCap90toBackPg"].Add(new CatClosedResults(this, "US 500", "CMR COND US Lg Cap MT", "", "COND", 9, true));
                //docPages["USLgCap90toBackPg"].Add(new CatOpenResults(this, "US 500", "CMR COND US Lg Cap LT", "", "COND", 10, true));
                //docPages["USLgCap90toBackPg"].Add(new CatClosedResults(this, "US 500", "CMR COND US Lg Cap LT", "", "COND", 11, true));
                //docPages["USLgCap90toBackPg"].Add(new CatDisclosure(this, "DISC", 12, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new CatTMSG(this, "TMSG", 13, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new CatProductPg(this, "PROD", 14, true, ""));

                //docPages["USLgCap90toBackPg"].Add(new LgCapInsideSector(this, "INDU", 90, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapOurView(this, "INDU", 91, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapOpenResults(this, "INDU", 92, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapClosedResults(this, "INDU", 93, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapAttribution(this, "INDU", 94, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapRisk(this, "INDU", 95, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapMomentumMap(this, "INDU", 96, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapMomentum(this, "INDU", 97, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapStrengthMap(this, "INDU", 98, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapStrength(this, "INDU", 99, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapEWMap(this, "INDU", 100, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapEWTable(this, "INDU", 101, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapValuationMap(this, "INDU", 102, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapValuation(this, "INDU", 103, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapProfitMap(this, "INDU", 104, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapProfit(this, "INDU", 105, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapSolvencyMap(this, "INDU", 106, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapSolvency(this, "INDU", 107, true, ""));

                //docPages["USLgCap90toBackPg"].Add(new LgCapInsideSector(this, "INFT", 108, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapOurView(this, "INFT", 109, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapOpenResults(this, "INFT", 110, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapClosedResults(this, "INFT", 111, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapAttribution(this, "INFT", 112, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapRisk(this, "INFT", 113, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapMomentumMap(this, "INFT", 114, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapMomentum(this, "INFT", 115, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapStrengthMap(this, "INFT", 116, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapStrength(this, "INFT", 117, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapEWMap(this, "INFT", 118, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapEWTable(this, "INFT", 119, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapValuationMap(this, "INFT", 120, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapValuation(this, "INFT", 121, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapProfitMap(this, "INFT", 122, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapProfit(this, "INFT", 123, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapSolvencyMap(this, "INFT", 124, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapSolvency(this, "INFT", 125, true, ""));

                //docPages["USLgCap90toBackPg"].Add(new LgCapInsideSector(this, "MATR", 126, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapOurView(this, "MATR", 127, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapOpenResults(this, "MATR", 128, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapClosedResults(this, "MATR", 129, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapAttribution(this, "MATR", 130, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapRisk(this, "MATR", 131, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapMomentumMap(this, "MATR", 132, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapMomentum(this, "MATR", 133, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapStrengthMap(this, "MATR", 134, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapStrength(this, "MATR", 135, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapEWMap(this, "MATR", 136, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapEWTable(this, "MATR", 137, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapValuationMap(this, "MATR", 138, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapValuation(this, "MATR", 139, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapProfitMap(this, "MATR", 140, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapProfit(this, "MATR", 141, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapSolvencyMap(this, "MATR", 142, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapSolvency(this, "MATR", 143, true, ""));

                //docPages["USLgCap90toBackPg"].Add(new LgCapInsideSector(this, "TELS", 144, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapOurView(this, "TELS", 145, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapOpenResults(this, "TELS", 146, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapClosedResults(this, "TELS", 147, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapAttribution(this, "TELS", 148, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapRisk(this, "TELS", 149, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapMomentumMap(this, "TELS", 150, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapMomentum(this, "TELS", 151, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapStrengthMap(this, "TELS", 152, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapStrength(this, "TELS", 153, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapEWMap(this, "TELS", 154, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapEWTable(this, "TELS", 155, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapValuationMap(this, "TELS", 156, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapValuation(this, "TELS", 157, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapProfitMap(this, "TELS", 158, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapProfit(this, "TELS", 159, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapSolvencyMap(this, "TELS", 160, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapSolvency(this, "TELS", 161, true, ""));

                //docPages["USLgCap90toBackPg"].Add(new LgCapInsideSector(this, "UTIL", 162, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapOurView(this, "UTIL", 163, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapOpenResults(this, "UTIL", 164, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapClosedResults(this, "UTIL", 165, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapAttribution(this, "UTIL", 166, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapRisk(this, "UTIL", 167, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapMomentumMap(this, "UTIL", 168, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapMomentum(this, "UTIL", 169, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapStrengthMap(this, "UTIL", 170, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapStrength(this, "UTIL", 171, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapEWMap(this, "UTIL", 172, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapEWTable(this, "UTIL", 173, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapValuationMap(this, "UTIL", 174, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapValuation(this, "UTIL", 175, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapProfitMap(this, "UTIL", 176, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapProfit(this, "UTIL", 177, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapSolvencyMap(this, "UTIL", 178, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapSolvency(this, "UTIL", 179, true, ""));

                //docPages["USLgCap90toBackPg"].Add(new LgCapMomentumGuide(this, "COND", 180, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapStrengthGuide(this, "COND", 181, true, ""));
                //docPages["USLgCap90toBackPg"].Add(new LgCapTMSG(this, "TMSG", 182, true, ""));


                docPages["GlobalEQCover"] = new List<IPDFDocument>();
                //docPages["GlobalEQCover"].Add(new BookFrontBack(this));
                //docPages["GlobalEQCover"].Add(new GMInsideBackPage(this));
                //docPages["GlobalEQCover"].Add(new GMOutsideCover(this, "EQ"));
                //docPages["GlobalEQCover"].Add(new GMInsideBackPage(this, "EQ"));

                docPages["GlobalAll"] = new List<IPDFDocument>();
                //docPages["GlobalAll"].Add(new CoverPageDoc(this, "EQ", 1, true));
                //docPages["GlobalAll"].Add(new CoverPageDoc2(this, "EQ", 1, true));
                //docPages["GlobalAll"].Add(new InsideGMDocPublish(this, "EQ", 2, true));
                //docPages["GlobalAll"].Add(new OurViewDoc(this, "EQ", 1, true));
                //docPages["GlobalAll"].Add(new ClosedResultsDoc(this, "EQ", 3, true));
                //docPages["GlobalAll"].Add(new AttributionDoc(this, "EQ", 4, true));
                //docPages["GlobalAll"].Add(new RiskDoc(this, "EQ", 5, true));
                //docPages["GlobalAll"].Add(new MomentumMapDoc(this, "EQ", 6, true));
                //docPages["GlobalAll"].Add(new MomentumTableDoc(this, "EQ", 7, true));
                //docPages["GlobalAll"].Add(new StrengthMapDoc(this, "EQ", 8, true));
                //docPages["GlobalAll"].Add(new StrengthTableDoc(this, "EQ", 9, true));
                //docPages["GlobalAll"].Add(new EWMapDoc(this, "EQ", 10, true));
                //docPages["GlobalAll"].Add(new EWTableDoc(this, "EQ", 11, true));
                //docPages["GlobalAll"].Add(new VolMapDoc(this, "EQ", 12, true));
                //docPages["GlobalAll"].Add(new VolTableDoc(this, "EQ", 13, true));
                //docPages["GlobalAll"].Add(new ValuationMapDoc(this, "EQ", 14, true));
                //docPages["GlobalAll"].Add(new ValuationTableDoc(this, "EQ", 15, true));
                //docPages["GlobalAll"].Add(new ProfitMapDoc(this, "EQ", 16, true));
                //docPages["GlobalAll"].Add(new ProfitTableDoc(this, "EQ", 17, true));
                //docPages["GlobalAll"].Add(new SolvencyMapDoc(this, "EQ", 18, true));
                //docPages["GlobalAll"].Add(new SolvencyTableDoc(this, "EQ", 19, true));
                //docPages["GlobalAll"].Add(new InsideGMDocPublish(this, "FX", 20, true));
                //docPages["GlobalAll"].Add(new OurViewDoc(this, "FX", 21, true));
                //docPages["GlobalAll"].Add(new OpenResultsDoc(this, "FX", 22, true));
                //docPages["GlobalAll"].Add(new ClosedResultsDoc(this, "FX", 23, true));
                //docPages["GlobalAll"].Add(new AttributionDoc(this, "FX", 24, true));
                //docPages["GlobalAll"].Add(new RiskDoc(this, "FX", 25, true));
                //docPages["GlobalAll"].Add(new MomentumMapDoc(this, "FX", 26, true));
                //docPages["GlobalAll"].Add(new MomentumTableDoc(this, "FX", 27, true));
                //docPages["GlobalAll"].Add(new StrengthMapDoc(this, "FX", 28, true));
                //docPages["GlobalAll"].Add(new StrengthTableDoc(this, "FX", 29, true));
                //docPages["GlobalAll"].Add(new EWMapDoc(this, "FX", 30, true));
                //docPages["GlobalAll"].Add(new EWTableDoc(this, "FX", 31, true));
                //docPages["GlobalAll"].Add(new VolMapDoc(this, "FX", 32, true));
                //docPages["GlobalAll"].Add(new VolTableDoc(this, "FX", 33, true));
                //docPages["GlobalAll"].Add(new InsideGMDocPublish(this, "FI", 34, true));
                //docPages["GlobalAll"].Add(new OurViewDoc(this, "FI", 35, true));
                //docPages["GlobalAll"].Add(new OpenResultsDoc(this, "FI", 36, true));
                //docPages["GlobalAll"].Add(new ClosedResultsDoc(this, "FI", 37, true));
                //docPages["GlobalAll"].Add(new AttributionDoc(this, "FI", 38, true));
                //docPages["GlobalAll"].Add(new RiskDoc(this, "FI", 39, true));
                //docPages["GlobalAll"].Add(new MomentumMapDoc(this, "FI", 40, true));
                //docPages["GlobalAll"].Add(new MomentumTableDoc(this, "FI", 41, true));
                //docPages["GlobalAll"].Add(new StrengthMapDoc(this, "FI", 42, true));
                //docPages["GlobalAll"].Add(new StrengthTableDoc(this, "FI", 43, true));
                //docPages["GlobalAll"].Add(new EWMapDoc(this, "FI", 44, true));
                //docPages["GlobalAll"].Add(new EWTableDoc(this, "FI", 45, true));
                //docPages["GlobalAll"].Add(new VolMapDoc(this, "FI", 46, true));
                //docPages["GlobalAll"].Add(new VolTableDoc(this, "FI", 47, true));
                //docPages["GlobalAll"].Add(new GDPMapDoc(this, "EQ", 48, true, ""));
                //docPages["GlobalAll"].Add(new GDPTableDoc(this, "EQ", 49, true, ""));
                //docPages["GlobalAll"].Add(new UERMapDoc(this, "EQ", 50, true, ""));
                //docPages["GlobalAll"].Add(new UERTableDoc(this, "EQ", 51, true, ""));
                //docPages["GlobalAll"].Add(new CPIMapDoc(this, "EQ", 52, true, ""));
                //docPages["GlobalAll"].Add(new CPITableDoc(this, "EQ", 53, true, ""));
                //docPages["GlobalAll"].Add(new DemoMapDoc(this, "EQ", 54, true, ""));
                //docPages["GlobalAll"].Add(new DemoTableDoc(this, "EQ", 55, true, ""));
                //docPages["GlobalAll"].Add(new MomentumGuideDoc(this, "EQ", 56, true, ""));
                //docPages["GlobalAll"].Add(new StrengthGuideDoc(this, "EQ", 57, true, ""));
                //docPages["GlobalAll"].Add(new TMSGDoc(this, "EQ", 58, true, ""));
                //docPages["GlobalAll"].Add(new ProductDoc(this, "EQ", 59, true, ""));


                docPages["GlobalEQAll"] = new List<IPDFDocument>();
                //docPages["GlobalEQAll"].Add(new CoverPageDoc(this, "EQ", 1, true, ""));
                //docPages["GlobalEQAll"].Add(new InsideDoc(this, "EQ", 2, true, ""));
                //docPages["GlobalEQAll"].Add(new OurViewDoc(this, "EQ", 1, true));
                //docPages["GlobalEQAll"].Add(new OpenResultsDoc(this, "EQ", 2, true));
                //docPages["GlobalEQAll"].Add(new ClosedResultsDoc(this, "EQ", 3, true));
                //docPages["GlobalEQAll"].Add(new AttributionDoc(this, "EQ", 4, true));
                //docPages["GlobalEQAll"].Add(new RiskDoc(this, "EQ", 5, true));
                //docPages["GlobalEQAll"].Add(new EWMapDoc(this, "EQ", 6, true));
                //docPages["GlobalEQAll"].Add(new EWTableDoc(this, "EQ", 7, true));
                //docPages["GlobalEQAll"].Add(new MomentumMapDoc(this, "EQ", 8, true));
                //docPages["GlobalEQAll"].Add(new MomentumTableDoc(this, "EQ", 9, true));
                //docPages["GlobalEQAll"].Add(new StrengthMapDoc(this, "EQ", 10, true));
                //docPages["GlobalEQAll"].Add(new StrengthTableDoc(this, "EQ", 11, true));
                //docPages["GlobalEQAll"].Add(new VolMapDoc(this, "EQ", 12, true));
                //docPages["GlobalEQAll"].Add(new VolTableDoc(this, "EQ", 13, true));
                //docPages["GlobalEQAll"].Add(new ValuationMapDoc(this, "EQ", 14, true));
                //docPages["GlobalEQAll"].Add(new ValuationTableDoc(this, "EQ", 15, true));
                //docPages["GlobalEQAll"].Add(new ProfitMapDoc(this, "EQ", 16, true));
                //docPages["GlobalEQAll"].Add(new ProfitTableDoc(this, "EQ", 17, true));
                //docPages["GlobalEQAll"].Add(new SolvencyMapDoc(this, "EQ", 18, true));
                //docPages["GlobalEQAll"].Add(new SolvencyTableDoc(this, "EQ", 19, true));
                //docPages["GlobalEQAll"].Add(new GDPMapDoc(this, "EQ", 20, true));
                //docPages["GlobalEQAll"].Add(new GDPTableDoc(this, "EQ", 21, true));
                //docPages["GlobalEQAll"].Add(new UERMapDoc(this, "EQ", 22, true));
                //docPages["GlobalEQAll"].Add(new UERTableDoc(this, "EQ", 23, true));
                //docPages["GlobalEQAll"].Add(new CPIMapDoc(this, "EQ", 24, true));
                //docPages["GlobalEQAll"].Add(new CPITableDoc(this, "EQ", 25, true));
                //docPages["GlobalEQAll"].Add(new DemoMapDoc(this, "EQ", 26, true));
                //docPages["GlobalEQAll"].Add(new DemoTableDoc(this, "EQ", 27, true));
                //docPages["GlobalEQAll"].Add(new MomentumGuideDoc(this, "EQ", 28, true));
                //docPages["GlobalEQAll"].Add(new StrengthGuideDoc(this, "EQ", 29, true));
                //docPages["GlobalEQAll"].Add(new DisclosureDoc(this, "EQ", 30, true));
                //docPages["GlobalEQAll"].Add(new ProductDoc(this, "EQ", 31, true));
                //docPages["GlobalEQAll"].Add(new BackCoverDoc(this)); 

                docPages["FXAll"] = new List<IPDFDocument>();
                //docPages["FXAll"].Add(new CoverPageDoc(this, "FX", 1, true, ""));
                //docPages["FXAll"].Add(new InsideDoc(this, "FX", 2, true, ""));
                //docPages["FXAll"].Add(new OurViewDoc(this, "FX", 1, true));
                //docPages["FXAll"].Add(new OpenResultsDoc(this, "FX", 2, true));
                //docPages["FXAll"].Add(new ClosedResultsDoc(this, "FX", 3, true));
                //docPages["FXAll"].Add(new AttributionDoc(this, "FX", 4, true));
                //docPages["FXAll"].Add(new RiskDoc(this, "FX", 5, true));
                //docPages["FXAll"].Add(new EWMapDoc(this, "FX", 6, true));
                //docPages["FXAll"].Add(new EWTableDoc(this, "FX", 7, true));
                //docPages["FXAll"].Add(new MomentumMapDoc(this, "FX", 8, true));
                //docPages["FXAll"].Add(new MomentumTableDoc(this, "FX", 9, true));
                //docPages["FXAll"].Add(new StrengthMapDoc(this, "FX", 10, true));
                //docPages["FXAll"].Add(new StrengthTableDoc(this, "FX", 11, true));
                //docPages["FXAll"].Add(new VolMapDoc(this, "FX", 12, true));
                //docPages["FXAll"].Add(new VolTableDoc(this, "FX", 13, true));
                //docPages["FXAll"].Add(new GDPMapDoc(this, "FX", 14, true));
                //docPages["FXAll"].Add(new GDPTableDoc(this, "FX", 15, true));
                //docPages["FXAll"].Add(new UERMapDoc(this, "FX", 16, true));
                //docPages["FXAll"].Add(new UERTableDoc(this, "FX", 17, true));
                //docPages["FXAll"].Add(new CPIMapDoc(this, "FX", 18, true));
                //docPages["FXAll"].Add(new CPITableDoc(this, "FX", 19, true));
                //docPages["FXAll"].Add(new DemoMapDoc(this, "FX", 20, true));
                //docPages["FXAll"].Add(new DemoTableDoc(this, "FX", 21, true));
                //docPages["FXAll"].Add(new MomentumGuideDoc(this, "FX", 22, true));
                //docPages["FXAll"].Add(new StrengthGuideDoc(this, "FX", 23, true));
                //docPages["FXAll"].Add(new DisclosureDoc(this, "FX", 24, true));
                //docPages["FXAll"].Add(new ProductDoc(this, "FX", 25, true));
                //docPages["FXAll"].Add(new BackCoverDoc(this)); 


                docPages["FIAll"] = new List<IPDFDocument>();
                //docPages["FIAll"].Add(new OurViewDoc(this, "FI", 1, true));
                //docPages["FIAll"].Add(new OpenResultsDoc(this, "FI", 2, true));
                //docPages["FIAll"].Add(new ClosedResultsDoc(this, "FI", 3, true));
                //docPages["FIAll"].Add(new AttributionDoc(this, "FI", 4, true));
                //docPages["FIAll"].Add(new RiskDoc(this, "FI", 5, true));
                //docPages["FIAll"].Add(new EWMapDoc(this, "FI", 6, true));
                //docPages["FIAll"].Add(new EWTableDoc(this, "FI", 7, true));
                //docPages["FIAll"].Add(new MomentumMapDoc(this, "FI", 8, true));
                //docPages["FIAll"].Add(new MomentumTableDoc(this, "FI", 9, true));
                //docPages["FIAll"].Add(new StrengthMapDoc(this, "FI", 10, true));
                //docPages["FIAll"].Add(new StrengthTableDoc(this, "FI", 11, true));
                //docPages["FIAll"].Add(new VolMapDoc(this, "FI", 12, true));
                //docPages["FIAll"].Add(new VolTableDoc(this, "FI", 13, true));
                //docPages["FIAll"].Add(new GDPMapDoc(this, "FI", 14, true));
                //docPages["FIAll"].Add(new GDPTableDoc(this, "FI", 15, true));
                //docPages["FIAll"].Add(new UERMapDoc(this, "FI", 16, true));
                //docPages["FIAll"].Add(new UERTableDoc(this, "FI", 17, true));
                //docPages["FIAll"].Add(new CPIMapDoc(this, "FI", 18, true));
                //docPages["FIAll"].Add(new CPITableDoc(this, "FI", 19, true));
                //docPages["FIAll"].Add(new DemoMapDoc(this, "FI", 20, true));
                //docPages["FIAll"].Add(new DemoTableDoc(this, "FI", 21, true));
                //docPages["FIAll"].Add(new MomentumGuideDoc(this, "FI", 22, true));
                //docPages["FIAll"].Add(new StrengthGuideDoc(this, "FI", 23, true));
                //docPages["FIAll"].Add(new DisclosureDoc(this, "FI", 24, true)); 


                docPages["FXPublish"] = new List<IPDFDocument>();
                //docPages["FXPublish"].Add(new OurViewDoc(this, "FX", 1, true));
                //docPages["FXPublish"].Add(new OpenResultsDoc(this, "FX", 2, true));
                //docPages["FXPublish"].Add(new ClosedResultsDoc(this, "FX", 3, true));
                //docPages["FXPublish"].Add(new AttributionDoc(this, "FX", 4, true));
                //docPages["FXPublish"].Add(new RiskDoc(this, "FX", 5, true));
                //docPages["FXPublish"].Add(new EWMapDoc(this, "FX", 6, true));
                //docPages["FXPublish"].Add(new EWTableDoc(this, "FX", 7, true));
                //docPages["FXPublish"].Add(new MomentumMapDoc(this, "FX", 8, true));
                //docPages["FXPublish"].Add(new MomentumTableDoc(this, "FX", 9, true));
                //docPages["FXPublish"].Add(new StrengthMapDoc(this, "FX", 10, true));
                //docPages["FXPublish"].Add(new StrengthTableDoc(this, "FX", 11, true));
                //docPages["FXPublish"].Add(new VolMapDoc(this, "FX", 12, true));
                //docPages["FXPublish"].Add(new VolTableDoc(this, "FX", 13, true));
                //docPages["FXPublish"].Add(new GDPMapDoc(this, "FX", 14, true));
                //docPages["FXPublish"].Add(new GDPTableDoc(this, "FX", 15, true));
                //docPages["FXPublish"].Add(new UERMapDoc(this, "FX", 16, true));
                //docPages["FXPublish"].Add(new UERTableDoc(this, "FX", 17, true));
                //docPages["FXPublish"].Add(new CPIMapDoc(this, "FX", 18, true));
                //docPages["FXPublish"].Add(new CPITableDoc(this, "FX", 19, true));
                //docPages["FXPublish"].Add(new DemoMapDoc(this, "FX", 20, true));
                //docPages["FXPublish"].Add(new DemoTableDoc(this, "FX", 21, true));
                //docPages["FXPublish"].Add(new MomentumGuideDoc(this, "FX", 22, true));
                //docPages["FXPublish"].Add(new StrengthGuideDoc(this, "FX", 23, true));
                //docPages["FXPublish"].Add(new DisclosureDoc(this, "FX", 24, true)); 

                docPages["FIDigital"] = new List<IPDFDocument>();
                //docPages["FIDigital"].Add(new CoverPageDoc(this, "FI", 1, true, ""));
                //docPages["FIDigital"].Add(new InsideDoc(this, "FI", 2, true, ""));
                //docPages["FIDigital"].Add(new OurViewDoc(this, "FI", 1, true));
                //docPages["FIDigital"].Add(new OpenResultsDoc(this, "FI", 2, true));
                //docPages["FIDigital"].Add(new ClosedResultsDoc(this, "FI", 3, true));
                //docPages["FIDigital"].Add(new AttributionDoc(this, "FI", 4, true));
                //docPages["FIDigital"].Add(new RiskDoc(this, "FI", 5, true));
                //docPages["FIDigital"].Add(new EWMapDoc(this, "FI", 6, true));
                //docPages["FIDigital"].Add(new EWTableDoc(this, "FI", 7, true));
                //docPages["FIDigital"].Add(new MomentumMapDoc(this, "FI", 8, true));
                //docPages["FIDigital"].Add(new MomentumTableDoc(this, "FI", 9, true));
                //docPages["FIDigital"].Add(new StrengthMapDoc(this, "FI", 10, true));
                //docPages["FIDigital"].Add(new StrengthTableDoc(this, "FI", 11, true));
                //docPages["FIDigital"].Add(new VolMapDoc(this, "FI", 12, true));
                //docPages["FIDigital"].Add(new VolTableDoc(this, "FI", 13, true));
                //docPages["FIDigital"].Add(new GDPMapDoc(this, "FI", 14, true));
                //docPages["FIDigital"].Add(new GDPTableDoc(this, "FI", 15, true));
                //docPages["FIDigital"].Add(new UERMapDoc(this, "FI", 16, true));
                //docPages["FIDigital"].Add(new UERTableDoc(this, "FI", 17, true));
                //docPages["FIDigital"].Add(new CPIMapDoc(this, "FI", 18, true));
                //docPages["FIDigital"].Add(new CPITableDoc(this, "FI", 19, true));
                //docPages["FIDigital"].Add(new DemoMapDoc(this, "FI", 20, true));
                //docPages["FIDigital"].Add(new DemoTableDoc(this, "FI", 21, true));
                //docPages["FIDigital"].Add(new MomentumGuideDoc(this, "FI", 22, true));
                //docPages["FIDigital"].Add(new StrengthGuideDoc(this, "FI", 23, false));
                //docPages["FIDigital"].Add(new DisclosureDoc(this, "FI", 24, false)); 
                //docPages["FIDigital"].Add(new ProductDoc(this, "FI", 25, false));
                //docPages["FIDigital"].Add(new BackCoverDoc(this)); 

                docPages["EQPublish"] = new List<IPDFDocument>();
                //docPages["EQPublish"].Add(new OurViewDoc(this, "EQ", 1, true));
                //docPages["EQPublish"].Add(new OpenResultsDoc(this, "EQ", 2, true));
                //docPages["EQPublish"].Add(new ClosedResultsDoc(this, "EQ", 3, true));
                //docPages["EQPublish"].Add(new AttributionDoc(this, "EQ", 4, true));
                //docPages["EQPublish"].Add(new RiskDoc(this, "EQ", 5, true));
                //docPages["EQPublish"].Add(new EWMapDoc(this, "EQ", 6, true));
                //docPages["EQPublish"].Add(new EWTableDoc(this, "EQ", 7, true));
                //docPages["EQPublish"].Add(new MomentumMapDoc(this, "EQ", 8, true));
                //docPages["EQPublish"].Add(new MomentumTableDoc(this, "EQ", 9, true));
                //docPages["EQPublish"].Add(new StrengthMapDoc(this, "EQ", 10, true));
                //docPages["EQPublish"].Add(new StrengthTableDoc(this, "EQ", 11, true));
                //docPages["EQPublish"].Add(new VolMapDoc(this, "EQ", 12, true));
                //docPages["EQPublish"].Add(new VolTableDoc(this, "EQ", 13, true));
                //docPages["EQPublish"].Add(new ValuationMapDoc(this, "EQ", 14, true));
                //docPages["EQPublish"].Add(new ValuationTableDoc(this, "EQ", 15, true));
                //docPages["EQPublish"].Add(new ProfitMapDoc(this, "EQ", 16, true));
                //docPages["EQPublish"].Add(new ProfitTableDoc(this, "EQ", 17, true));
                //docPages["EQPublish"].Add(new SolvencyMapDoc(this, "EQ", 18, true));
                //docPages["EQPublish"].Add(new SolvencyTableDoc(this, "EQ", 19, true));
                //docPages["EQPublish"].Add(new GDPMapDoc(this, "EQ", 20, true));
                //docPages["EQPublish"].Add(new GDPTableDoc(this, "EQ", 21, true));
                //docPages["EQPublish"].Add(new UERMapDoc(this, "EQ", 22, true));
                //docPages["EQPublish"].Add(new UERTableDoc(this, "EQ", 23, true));
                //docPages["EQPublish"].Add(new CPIMapDoc(this, "EQ", 24, true));
                //docPages["EQPublish"].Add(new CPITableDoc(this, "EQ", 25, true));
                //docPages["EQPublish"].Add(new DemoMapDoc(this, "EQ", 26, true));
                //docPages["EQPublish"].Add(new DemoTableDoc(this, "EQ", 27, true));
                //docPages["EQPublish"].Add(new MomentumGuideDoc(this, "EQ", 28, true));
                //docPages["EQPublish"].Add(new StrengthGuideDoc(this, "EQ", 29, true));
                //docPages["EQPublish"].Add(new DisclosureDoc(this, "EQ", 30, true));

                docPages["GI"] = new List<IPDFDocument>();
                //docPages["USLgCapCoverto89"].Add(new LgCapScoreMap(this, "COND", 10, true, ""));
                //docPages["USLgCapCoverto89"].Add(new LgCapScore(this, "COND", 11, true, ""));
                //docPages["GI"].Add(new GIFrontPage(this));
                //docPages["GI"].Add(new GIInside(this));
                //docPages["GI"].Add(new GIOurViewMap(this));
                //docPages["GI"].Add(new GIOurViewFXMap(this));
                //docPages["GI"].Add(new GIOurViewFIMap(this));
                //docPages["GI"].Add(new GIOurResultsOpen(this));
                //docPages["GI"].Add(new GIOurResultsClosed(this));
                //docPages["GI"].Add(new GIOurResultsOpenFX(this));
                //docPages["GI"].Add(new GIOurResultsClosedFX(this));
                //docPages["GI"].Add(new GIOurResultsOpenFI(this));
                //docPages["GI"].Add(new GIOurResultsClosedFI(this));
                //docPages["GI"].Add(new GIPerformance(this));
                //docPages["GI"].Add(new GIRiskReward(this));
                //docPages["GI"].Add(new GIEWMap(this));
                //docPages["GI"].Add(new GIEW(this));
                //docPages["GI"].Add(new GIMomentumMap(this));
                //docPages["GI"].Add(new GIMomentum(this));
                //docPages["GI"].Add(new GIStrengthMap(this));
                //docPages["GI"].Add(new GIStrength(this));
                //docPages["GI"].Add(new GIVolatilityMap(this));
                //docPages["GI"].Add(new GIVolatility(this));
                //docPages["GI"].Add(new GIValuationMap(this));
                //docPages["GI"].Add(new GIValuation(this));
                //docPages["GI"].Add(new GIProfitabilityMap(this));
                //docPages["GI"].Add(new GIProfitability(this));
                //docPages["GI"].Add(new GISolvencyMap(this));
                //docPages["GI"].Add(new GISolvency(this));
                //docPages["GI"].Add(new GIGDPMap(this));
                //docPages["GI"].Add(new GIGDP(this));
                //docPages["GI"].Add(new GIEMPMap(this));
                //docPages["GI"].Add(new GIEMP(this));
                //docPages["GI"].Add(new GICPIMap(this));
                //docPages["GI"].Add(new GICPI(this));
                //docPages["GI"].Add(new GIDemographicsMap(this));
                //docPages["GI"].Add(new GIDemographics(this));
                //docPages["GI"].Add(new GIMomentumGuide(this));
                //docPages["GI"].Add(new GIStrengthGuide(this));
                //docPages["GI"].Add(new GIDisclosure(this));
                //docPages["GI"].Add(new GIBlank(this));
            }
        }

        public bool AreDocumentsReady(string name)
        {
            bool ready = true;
            if (docPages.ContainsKey(name))
            {
                for (int ii = 0; ii < docPages[name].Count; ii++)
                {
                    if (((IPDFDocument)docPages[name][ii]).Ready() == false)
                    {
                        ready = false;
                        break;
                    }
                }
            }
            return ready;
        }

        public void ClearDocPages()
        {
            docPages["LgCapCover"].Clear();
            docPages["USLgCapCoverto89"].Clear();
            docPages["USLgCap90toBackPg"].Clear();

            docPages["GlobalEQCover"].Clear();
            docPages["GlobalAll"].Clear();

            docPages["GlobalEQAll"].Clear();
            docPages["FXAll"].Clear();
            docPages["FIAll"].Clear();

            docPages["EQPublish"].Clear();
            docPages["FXPublish"].Clear();
            docPages["FIDigital"].Clear();

            //docPages["GI"].Clear();
            //docPages["100Publish"].Clear();

            docPages.Clear();

            //LoadReportPages();
        }

        public List<IPDFDocument> GetDocPages(string reportName)
        {
            return docPages.ContainsKey(reportName) ? docPages[reportName] : new List<IPDFDocument>();
        }

        void OnClose(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_closeConfirm)
            {
                MessageBoxResult result1 = MessageBox.Show("Exit ATM ML APP?", "Confirm Exit", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result1 == MessageBoxResult.Yes)
                {
                    Close();
                }
                else
                {
                    e.Cancel = true;
                }
            }
            else
            {
                Close();
            }
        }

		// constructor
		public MainView()
		{
			InitializeComponent();
			usernameBox.Focus();
			_research["Outside"] = new Dictionary<string, Research>();
			Loaded += MainViewLoaded;
			_startTime = DateTime.Now;
		}

		private void loadResearch(string name)
        {
            string fileName = name.ToLower() + "p.php";
            string value = SendWebRequest(MainView.CMREndPoint + "/" + fileName);

            string[] rows = value.Split('\n');
            foreach (string row in rows)
            {
                if (row.Length > 0)
                {
                    string[] fields = row.Split('\u001f');
                    if (fields.Length >= 5)
                    {
                        string key = fields[0] + ':' + fields[1];
                        Research research  = new Research();
                        research.Direction = int.Parse(fields[2]);
                        research.Comment = fields[3];
                        research.Description = fields[4];
                        _research[name][key] = research;
                    }
                }
            }
        }

        private void initializeManagers()
        {
            TradeManager tradeManager = Trade.Manager;  // download position infos and request bars
            ScanManager scanManager = Scan.Manager;     // start scanning
            AlertManager alertManager = Alert.Manager;
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            updateSettings();

            try
            {
                if (_saveSettings)
                {
                    _saveSettings = false;
                    //saveSettingsToDatabase(Username);
                    saveSettingsLocally();
                }
            }
            catch
            {
                // ignore web exceptions due to internet down... will try to save setting later after the internet comes back up
            }

            if (!checkId())
            {
                //_closeConfirm = false;
                //Application.Current.Shutdown();
            }
        }

        List<MainView> _screens = new List<MainView>();

        private void displayView()
        {
            int screenCount = 1; // System.Windows.Forms.Screen.AllScreens.Length;
            for (int ii = 0; ii < screenCount - 1; ii++)
            {
                MainView mv = new MainView();
                _screens.Add(mv);

                System.Windows.Forms.Screen s1 = System.Windows.Forms.Screen.AllScreens[ii];

                System.Drawing.Rectangle r1 = s1.WorkingArea;

                mv.Top = r1.Top;
                mv.Left = r1.Left;
                mv.Show();

                mv.Owner = this;

                if (ii == 0) // left
                {
                    mv.Content = new MarketMonitor(mv);
                }
                else // right
                {
                    mv.Content = new PortfolioBuilder(mv);
                }
                mv.UpdateLayout();
            }

            var ts = DateTime.Now - _startTime;
            //System.Diagnostics.Debug.WriteLine("displayView " + ts.TotalSeconds.ToString());

            _progressTime = DateTime.Now;

            this.Content = new LandingPage(this); // Opening View
            this.UpdateLayout();
        }

        public Style LoadStyle(string key)
        {
            Style style;
            using (var s = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/ATMML;component/StyleDictionary.xaml", UriKind.RelativeOrAbsolute)).Stream)
            {
                var dic = (ResourceDictionary)XamlReader.Load(s);
                style = dic[key] as Style;
            }
            return style;
        }

        public string GetInfo(string name)
        {
            return GetSetting(name);
        }

        public void SetInfo(string name, string info)
        {
            SaveSetting(name, info);
        }

        public new void Close()
        {
            //foreach (IPDFDocument doc in docPages)
            //{
            //    doc.Close();
            //}

            Trade.Manager.Close();
            Scan.Manager.Close();
            Alert.Manager.Close();
            BarCache.Shutdown();

            PortfolioBuilder.Shutdown();
            AutoMLView.Shutdown();

            if (_loginOk)
            {
                closeView(this);
                foreach (var mv in _screens)
                {
                    closeView(mv);
                }

                updateSettings();
                //saveSettingsToDatabase(Username);
                saveSettingsLocally();
            }
        }

        private void closeView(MainView input)
        {
            MarketMonitor map = input.Content as MarketMonitor;
            if (map != null) map.Close();

            Charts ourView = input.Content as Charts;
            if (ourView != null) ourView.Close();

            Alerts myAlerts = input.Content as Alerts;
            if (myAlerts != null) myAlerts.Close();

            PortfolioBuilder factors = input.Content as PortfolioBuilder;
            if (factors != null) factors.Close();

            Timing timing = input.Content as Timing;
            if (timing != null) timing.Close();

            //Competitor aodView = input.Content as Competitor;
            //if (aodView != null) aodView.Close();
            
            AutoMLView autoMLView = input.Content as AutoMLView;
            if (autoMLView != null) autoMLView.Close();
        }

        static public string GetSetting(string key)
        {
            string setting = "";
            if (_settings.ContainsKey(key))
            {
                setting = _settings[key].value;
            }
            return setting;
        }

        static List<Order> _orders = new List<Order>();

        public static Order GetOrder(string PRTU, string ticker)  // return null if no order found
        {
            Order output = null;
            foreach (var order in _orders)
            {
                if (order.PRTU == PRTU && order.Ticker == ticker)
                {
                    output = order;
                    break;
                }
            }
            return output;
        }

        public static void AddOrder(Order input)
        {

            bool reinstated = false;

            string ticker = input.Ticker;
            string PRTU = input.PRTU;

            Order oldOrder = GetOrder(PRTU, ticker);

            if (oldOrder != null)
            {
                oldOrder.Cancelled = false;
                reinstated = true;
            }

            if (reinstated)
            {
                SendWebRequest(MainView.CMREndPoint + "/changeOrder.php?p=" + input.PRTU + "&t=" + input.Ticker + "&a=" + input.Type + "&s=" + input.Size.ToString() + "&c=0");
            }
            else
            {
                _orders.Add(input);
                SendWebRequest(MainView.CMREndPoint + "/addOrder.php?p=" + input.PRTU + "&t=" + input.Ticker + "&a=" + input.Type + "&s=" + input.Size.ToString());
            }
        }

        public static void CancelOrder(string PRTU, string ticker)
        {
            string region1 = getRegion(PRTU);

            foreach (var order in _orders)
            {
                string region2 = getRegion(order.PRTU);
                if (region1== region2  && order.Ticker == ticker)
                {
                    order.Cancelled = true;
                    SendWebRequest(MainView.CMREndPoint + "/changeOrder.php?p=" + order.PRTU + "&t=" + order.Ticker + "&a=" + order.Type + "&s=" + order.Size.ToString() + "&c=1");        
                    break;
                }
            }
        }

        private static string getRegion(string input)
        {
            string output = "";
            if (input.Contains("PCM MAIN")) output = input.Substring(9);
            else if (input.Contains("PCM ATM")) output = input.Substring(8);
            return output;
        }

        public static List<Order> GetOrders(string prtuName = "")
        {
            string region1 = getRegion(prtuName);

            var output = new List<Order>();
            foreach (var order in _orders)
            {
                string region2 = getRegion(order.PRTU);

                bool sameRegion = (region1.Length > 0 && region1 == region2);

                if (prtuName.Length == 0 || order.PRTU == prtuName || sameRegion)
                {
                    output.Add(order);
                }
            }
            return output;
        }

        public static void DeleteOrders(string prtuName = "")
        {
            var region1 = getRegion(prtuName);
            
            var orders = new List<Order>();
            foreach (var order in _orders)
            {
                string region2 = getRegion(order.PRTU);
                if (region1 == region2)
                {
                    orders.Add(order);
                }
            }

            DateTime now = DateTime.Now;
            foreach (var order in orders)
            {
                _orders.Remove(order);
            }

            clearOrders(region1);
        }

        public static void ExportOrders(BarCache barCache, string prtuName = "")
        {
            //string path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            //path += "\\PRTU Orders\\";
            //Directory.CreateDirectory(path);

            //var orders = GetOrders(prtuName);
            //var region = getRegion(prtuName);

            //DateTime now = DateTime.Now;

            //StreamWriter sw = new StreamWriter(path + "" + region + " Recommendations" + " " + now.ToString(" yyyyMMdd HHmm") + ".csv");
            //using (sw)
            //{
            //    foreach (var order in orders)
            //    {
            //        if (!order.Cancelled)
            //        {
            //            string[] fields = order.Ticker.Split(' ');

            //            string ticker = fields[0] + ((fields.Length > 1) ? " " + fields[1] : "");

            //            sw.WriteLine(ticker + "," + order.Type + "," + Math.Abs(order.Size).ToString() + ",mkt,,jefi");

            //        }
            //    }
            //}
        }


        private static void clearOrders(string region)
        {
            SendWebRequest(MainView.CMREndPoint + "/clearOrders.php?r=" + region);         
        }

        static public void SetChartMode(string mode)
        {
            SaveSetting("ChartMode", mode);
        }

        static public string GetChartMode()
        {
            string mode = GetSetting("ChartMode");
            if (mode == null || mode.Length == 0)
            {
                mode = "POSITIONS";
            }
            return mode;
        }

        static public void SetPortfolios(List<string> portfolios)
        {
            string data = "";
            foreach (string portfolio in portfolios)
            {
                if (data.Length > 0)
                {
                    data += "|";
                }
                data += portfolio;
            }
            SaveSetting("PortfolioList68", data);  // CHANGE VERSION NUMBER FOR DEFAULT
        }

        static public List<string> GetPortfolios()
        {
            List<string> portfolios = new List<string>();
            string data = MainView.GetSetting("PortfolioList68");  // CHANGE VERSION NUMBER FOR DEFAULT
            if (data != null)
            {
                string[] fields = data.Split('|');
                foreach (string field in fields)
                {
                    portfolios.Add(field);
                }
            }
            else
            {
                portfolios.Add("US 500");
                portfolios.Add("COUNTRY ETF");
                portfolios.Add("GLOBAL INDICES");
                portfolios.Add("GLOBAL 10YR");
                portfolios.Add("GLOBAL 30YR");
                portfolios.Add("GLOBAL 7YR");
                portfolios.Add("GLOBAL 5YR");
                portfolios.Add("GLOBAL 2YR");
                portfolios.Add("GLOBAL 1YR");
                portfolios.Add("COMMODITIES");
            }
            return portfolios;
        }

        static public List<string> GetCustomPortfolios()
        {
            List<string> portfolios = new List<string>();
            return portfolios;
        }


        private static void updateOutsideResearch(string symbol, int direction)
        {
            SendWebRequest(MainView.CMREndPoint + "/updateOutsideresearch.php" + "?s=" + symbol + "&d=" + direction.ToString());            
        }

        private static void clearOutsideResearch()
        {
             SendWebRequest(MainView.CMREndPoint + "/clearOutsideresearch.php");
        }

        public static void ClearAllResearchDirections(string name)
        {
            foreach (Research research in _research[name].Values) research.Direction = 0;
            clearOutsideResearch();
        }
        
        public static void SetResearchDirection(string name, string symbol, int direction)
        {
            string key = symbol;
            _research[name][key].Direction = direction;
            updateOutsideResearch(symbol, direction);
        }

        public static int GetResearchDirection(string name, string symbol, int ago)
        {
            string key = symbol + ":" + ago.ToString();
            int direction = _research[name].ContainsKey(key) ? _research[name][key].Direction : 0;
            return direction;
        }

        public static string GetResearchComment(string name, string symbol, int ago)
        {
            string key = symbol + ":" + ago.ToString();
            string comment = _research[name].ContainsKey(key) ? _research[name][key].Comment : "";
            return comment;
        }

        public static string GetResearchDescription(string name, string symbol)
        {
            string key = symbol + ":0";
            string comment = _research[name].ContainsKey(key) ? _research[name][key].Description : "";
            return comment;
        }

        public static List<string> GetResearchSymbols(string name, int direction)
        {
            List<string> output = new List<string>();
            foreach (KeyValuePair<string, Research> kvp in _research[name])
            {
                if (kvp.Value.Direction == direction || direction == 0)
                {
                    string[] fields = kvp.Key.Split(':');
                    if (fields.Length >= 2 && fields[1] == "0")
                    {
                        output.Add(fields[0]);
                    }
                }
            }
            return output;
        }

       static public void SaveSetting(string key, string value)
        {
            string oldValue = GetSetting(key);
            if (value != oldValue)
            {
                _settings[key] = new Setting(value, true);
                _saveSettings = true;
            }
        }

        private void loadSettingsFromDatabase(string user)
        {
            string value = SendWebRequest(MainView.CMREndPoint + "/settings.php?u=" + user);

            string[] rows = value.Split('\x1e');
            foreach (string row in rows)
            {
                if (row.Length > 0)
                {
                    string[] fields = row.Split('\x1f');
                    if (fields.Length >= 2)
                    {
                        _settings[fields[0]] = new Setting(fields[1], true);
                    }
                }
            }
        }

        private bool loadSettingsLocally()
        {
            bool ok = false;
            var path = MainView.GetDataFolder() + @"\settings";
            Directory.CreateDirectory(path);
            var names = Directory.EnumerateFiles(path).ToList();
            var settingNames = names.Select(x => { var ix = x.LastIndexOf('\\'); return x.Substring(ix + 1); }).ToList();

            foreach (var settingName in settingNames)
            {
                var settingValue = LoadUserData(@"settings/" + settingName);
                _settings[settingName] = new Setting(settingValue, false);
                ok = true;
            }
            return ok;
        }

        private void saveSettingsToDatabase(string user) 
        {
            foreach (KeyValuePair<string, Setting> kvp in _settings)
            {
                string settings = "";
                Setting setting = kvp.Value;
                if (setting.changed)
                {
                    setting.changed = false;

                    settings += kvp.Key + "\x1f" + setting.value + "\x1e";

                    SendWebRequest(MainView.CMREndPoint + "/updateSettings.php?u=" + user + "&s=" + Uri.EscapeDataString(settings));
                }
            }            
        }

        private void saveSettingsLocally()
        {
            foreach (KeyValuePair<string, Setting> kvp in _settings)
            {
                string settings = "";
                Setting setting = kvp.Value;
                if (setting.changed)
                {
                    setting.changed = false;

                    settings += setting.value;
                    SaveUserData(@"settings/" + kvp.Key, settings);
                }
            }
        }

        private void updateSettings()
        {
            try
            {
                SaveSetting("Calculator", CalculatorSettings);

                SaveSetting("ByPassStartup1", _bypassStartup.ToString());
                SaveSetting("ShowChartCursor", _hideChartCursor.ToString());

                string view = "Maps";

                if (this.Content as Charts != null) view = "Positions";
                else if (this.Content as Timing != null) view = "Server";
                else if (this.Content as Alerts != null) view = "Alerts";
                else if (this.Content as MarketMonitor != null) view = "Maps";

                SaveSetting("View", view);

                Portfolio.SavePortfolioInfo();

                for (int horizon = 0; horizon < 4; horizon++)
                {
                    for (int type = 0; type < conditionCount; type++)
                    {
                        string name = "Condition" + horizon.ToString() + type.ToString();
                        SaveSetting(name, _conditions[horizon, type]);
                    }
                }

                for (int index = 0; index < 2; index++)
                {
                    for (int type = 0; type < 4; type++)
                    {
                        string name = "HashmarkCondition" + index.ToString() + type.ToString();
                        SaveSetting(name, _hashmarkConditions[index, type]);
                    }
                }

                string symbols = "";
                foreach (KeyValuePair<string, AnticipatedAction> kvp in _reviewSymbols)
                {
                    symbols += kvp.Key + "," + (int)kvp.Value.Action + "," + kvp.Value.Time.ToString() + ";";
                }
                SaveSetting("REView Symbols v2", symbols);
            }
            catch (Exception x)
            {
            }
        }

        private void loadSettings()
        {
           try
            {
                CalculatorSettings = GetSetting("Calculator");

                string text1 = GetSetting("ByPassStartup1");
                string text2 = GetSetting("ShowChartCursor");

                _bypassStartup = (text1.Length > 0) ? bool.Parse(text1) : false;
                _hideChartCursor = (text2.Length > 0) ? bool.Parse(text2) : false;

                for (int horizon = 0; horizon < 4; horizon++)
                {
                    for (int type = 0; type < conditionCount; type++)
                    {
                        string name = "Condition" + horizon.ToString() + type.ToString();
                        _conditions[horizon, type] = GetSetting(name);
                        if (_conditions[horizon, type] == null)
                        {
                            _conditions[horizon, type] = "";
                        }
                    }
                }

                for (int index = 0; index < 2; index++)
                {
                    for (int type = 0; type < 4; type++)
                    {
                        string name = "HashmarkCondition" + index.ToString() + type.ToString();
                        _hashmarkConditions[index, type] = GetSetting(name);
                        if (_hashmarkConditions[index, type] == null)
                        {
                            _hashmarkConditions[index, type] = "";
                        }
                    }
                }

                _reviewSymbols.Clear();
                string symbols = GetSetting("REView Symbols v2");
                if (symbols != null)
                {
                    string[] items = symbols.Split(';');
                    foreach (string item in items)
                    {
                        if (item.Length > 0)
                        {
                            string[] fields = item.Split(',');
                            if (fields.Length == 3)
                            {

                                ReviewAction action = (ReviewAction)Enum.Parse(typeof(ReviewAction), fields[1]);
                                DateTime time = DateTime.Parse(fields[2]);

                                _reviewSymbols[fields[0]] = new AnticipatedAction(action, time);
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        public static AnticipatedAction GetReviewSymbolInterval(string portfolioName, string symbol, out string interval)
        {
            AnticipatedAction action = new AnticipatedAction(ReviewAction.StayingOut, DateTime.MinValue);
            interval = "";
            int length = symbol.Length;

            foreach (KeyValuePair<string, AnticipatedAction> kvp in _reviewSymbols)
            {
                string[] fields = kvp.Key.Split('|');
                string reviewSymbol = fields[0];
                string reviewInterval = fields[1];
                string reviewPortfolioName = fields[2];

                if (reviewPortfolioName == portfolioName && reviewSymbol == symbol)
                {
                    interval = reviewInterval;
                    action = kvp.Value;
                    break;
                }
            }
            return action;
        }

        public static string[] GetConditions(int horizon)
        {
            string[] conditions = new string[conditionCount];
            for (int ii = 0; ii < conditionCount; ii++) {
                conditions[ii] = _conditions[horizon + 1, ii];
            }
            return conditions;
        }

        public static void SetConditions(int horizon, string[] conditions)
        {
            for (int ii = 0; ii < conditionCount; ii++)
            {
                _conditions[horizon + 1, ii] = conditions[ii];
            }
        }

        public static string[] GetHashmarkConditions(int index)
        {
            string[] conditions = new string[4];
            conditions[0] = _hashmarkConditions[index, 0];
            conditions[1] = _hashmarkConditions[index, 1];
            conditions[2] = _hashmarkConditions[index, 2];
            conditions[3] = _hashmarkConditions[index, 3];
            return conditions;
        }

        public static void SetHashmarkConditions(int index, string[] conditions)
        {
            _hashmarkConditions[index, 0] = conditions[0];
            _hashmarkConditions[index, 1] = conditions[1];
            _hashmarkConditions[index, 2] = conditions[2];
            _hashmarkConditions[index, 3] = conditions[3];
        }

        private void GoToApp_Click(object sender, RoutedEventArgs e)
        {
            //_bypassStartup = (ByPass.IsChecked == true);

            //displayView();
        }

        private void GoToHelp_Click(object sender, RoutedEventArgs e)
        {
            //_bypassStartup = (ByPass.IsChecked == true);

            //BlpTerminal.RunFunction("CMR", "", null, "<activate>");
        }

        public DateTime _startTime;

        private void saveCredentials(string username, string password) 
        {
            string[] text = {username, password};
            System.IO.File.WriteAllLines("irs2", text);
        }


        private bool loginWithSavedCredentials()
        {
            bool ok = false;
            try
            {
                if (File.Exists("irs2"))
                {
                    string[] text = System.IO.File.ReadAllLines("irs2");
                    ok = login(text[0], text[1]);
                }
            }
            catch (Exception x)
            {
            }
            return ok;
        }

        private bool checkId()
        {
            bool ok = true;
            if (_id.Length > 0)
            {
                string value = SendWebRequest(MainView.CMREndPoint + "/check.php?u=" + _username + "&i=" + _id);
                if (value != "ok")
                {
                    ok = false;
                }
            }
            return ok;
        }

        // initial app logon: user creates password
        // setpw.php
        //string value = SendWebRequest(MainView.CMREndPoint + "/setpw.php?u=" + username + "&p=" + password);

        // forgot password
        // send email to user with link to http://www.capitalmarketsresearch.com/resetpw.http (user name must be available)
        // http://www.capitalmarketsresearch.com/resetpw.http has two fields (new password and reenter new password)
        // calls into http://www.capitalmarketsresearch.com/setpw.php?u=" + username + "&p=" + password

        private bool createPasswordReset(string address, out string message)
        {
            bool output = false;
            try
            {
                string url = "http://www.capitalmarketsresearch.com/send-password-reset.php";
                string[] input = new string[] { address };
                message = Encryption.Message(url, input);
                output = (message != "Password reset failed!");
            }
            catch (Exception x)
            {
                message = "";
                Debug.WriteLine("ATMML createPasswordReset exception: " + x.Message);
            }
            return output;
        }

        private bool login(string username, string password)
        {
			displayView();

			_timer.Interval = TimeSpan.FromMilliseconds(15 * 1000);
			_timer.Tick += new System.EventHandler(timer_Tick);
			_timer.Start();

			if (!loadSettingsLocally())
			{
			//	loadSettingsFromDatabase(Username);
			}
			loadSettings();

			return true;

			bool ok = false;
            try
            {
                _username = username;
                _id = Guid.NewGuid().ToString();

                SaveUserData("username", username);

                string value = "";

                //value = SendWebRequest(MainView.CMREndPoint + "/hello.php?u=" + username + "&p=" + password + "&i=" + _id);

                if (true) //(_letInRick && username.Contains("rknox19@bloomberg.net"))
                {
                    value = "ok,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1";
                }

                string[] fields = value.Split(',');
                if (fields[0] == "ok")
                {
                    ok = true;
                    _rick = (username == "rknox19@bloomberg.net");
                    _testUser = (username == "TestUser");

                    _enableMaps = (fields[1] == "1");
                    _enableAlerts = (fields[2] == "1");
                    _enablePositions = (fields[3] == "1");
                    _enableNetworks = (fields[4] == "1");
                    _enableResults = (fields[5] == "1");
                    _enableDiary = (fields[6] == "1");
                    _enablePortfolio = (fields[7] == "1");
                    _enableUS100 = (fields[8] == "1");
                    _enableUS500 = (fields[9] == "1");
                    _enableMajorETF = (fields[10] == "1");
                    _enableRickStocks = (fields[11] == "1");
                    _enableFinancial = (fields[12] == "1");
                    _enableTradesOnLargeIntervals = (fields[13] == "1");
                    _enableRecommendations = (fields[14] == "1");
                    _enableHistoricalRecommendations = (fields[15] == "1");
                    _enableChartPositions = (fields[16] == "1");
                    _enableCursorColor = (fields[17] == "1");
                    _enableEditTrades = (fields[18] == "1");
                    _enableTradeConditions = (fields[19] == "1");
                    _enableHappenedWithinConditions = (fields[20] == "1");
                    _enableSpreadScans = (fields[21] == "1");
                    _enableCMR = (fields[22] == "1");
                    _enableCurrentRecommendations = (fields[23] == "1");
                    //_enableMPMPortfolio = (fields[24] == "1");
                    _enableGlobalIndices = (fields[25] == "1");
                    _enableScore = (fields[26] == "1");
                    _enablePR = (fields[27] == "1");
                    _enableRP = (fields[28] == "1");
                    _enableTRT = (fields[29] == "1");
                    _enableLinks = (fields[30] == "1");
                    _enableADX = (fields[31] == "1");
                    _enablePINZPortfolio = (fields[32] == "1");
                    _enablePINZEditTrades = (fields[33] == "1");
                    _enableSQPTPortfolio = (fields[34] == "1");
                    _enableSQPTEditTrades = (fields[35] == "1");
                    _enableAutoTrade = (fields[36] == "1");  //index out of range
                    _enableServerOutput = (fields[37] == "1");
                    _enableStrategies = (fields[38] == "1");
                    _enableServer = (fields[39] == "1");
                    _enablePINZOrders = (fields[40] == "1");
                    _enablePINZExport = (fields[41] == "1");
                    _enablePRTUExport = (fields[42] == "1");
                    _enablePRTUHistory = (fields[43] == "1");
                    _enableServerTicker = (fields[44] == "1");
                    _enableGuiPortfolio = (fields[45] == "1");

                    FlashManager flashManager = Flash.Manager;
                    TradeManager tradeManager = Trade.Manager;
                    new Thread(new ThreadStart(initializeManagers)).Start();

                    displayView();

                    _loginOk = true;
                }
                else
                {
                    LoginError.Visibility = Visibility.Visible;
                    //LoginError2.Visibility = Visibility.Visible;
                    //    Application.Current.Shutdown();

                }
            }
            catch(Exception x)
            {
                MessageBox.Show(x.ToString());
            }

             return ok;
        }

        private void CreatePW_Mousedown(object sender, MouseEventArgs e)
        {
            Login.Visibility = Visibility.Collapsed;
            SetPW.Visibility = Visibility.Visible;
            SendReset.Visibility = Visibility.Collapsed;
        }
        private void LoginPage_Mousedown(object sender, MouseEventArgs e)
        {
            Login.Visibility = Visibility.Visible;
            SetPW.Visibility = Visibility.Collapsed;
            SendReset.Visibility = Visibility.Collapsed;
        }

        private void ForgotPW_Mousedown(object sender, MouseEventArgs e)
        {
           // Login.Visibility = Visibility.Collapsed;
           // SetPW.Visibility = Visibility.Collapsed;
           // SendReset.Visibility = Visibility.Visible;
           // _username = usernameBox.Text;
           // string message = "Password reset sent to your Bloomberg email.";
           // if (createPasswordReset(Username, out message)) {
           //     sendEmail(Username, message);
           // }
        }

        private void Exit_Mousedown(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        public static string CMREndPoint { get { return "http://www.tcx1.com"; } } 

        private void Exit_Enter(object sender, MouseEventArgs e)
        {
            Label label = sender as Label;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
        }
        private void Exit_Leave(object sender, MouseEventArgs e)
        {
            Label label = sender as Label;
            //label.Background = new SolidColorBrush(Color.FromRgb(0x90, 0x90, 0x90));
            label.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
        }


        private string getUserName()
        {
            var output = usernameBox.Text.Replace("\0", "");
            return output;
        }

        private void Login_Mousedown(object sender, RoutedEventArgs e)
        {
            string username = getUserName();
            string password = "";// passwordBox.Password;

            if (login(username, password))
            {
                //if ((bool)checkBox.IsChecked)
                //{
                //    saveCredentials(username, password);
                //}
            }
        }

        //private void Button_Click(object sender, RoutedEventArgs e)
        //{
        //    string username = getUserName();
        //    string password = "";// passwordBox.Password;

        //    if (login(username, password))
        //    {
        //        //if ((bool)checkBox.IsChecked)
        //        //{
        //        //    saveCredentials(username, password);
        //        //}
        //    }
        //}

        private void LoginButton2_MouseEnter(object sender, MouseEventArgs e)
        {
            Button label = sender as Button;
            label.Background = Brushes.DimGray;
        }

        private void LoginButton2_MouseLeave(object sender, MouseEventArgs e)
        {
            Button label = sender as Button;
            label.Background = new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x30));
        }

        private void usernameBox_GotFocus(object sender, RoutedEventArgs e)
        {
            LoginError.Visibility = Visibility.Collapsed;
        }

        private void passwordBox_GotFocus(object sender, RoutedEventArgs e)
        {
            LoginError.Visibility = Visibility.Collapsed;
        }

        private void usernameBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Return)
            {

                string username = getUserName();
                string password = "";// passwordBox.Password;

                login(username, password);
            }
        }

        private void Label7_MouseEnter(object sender, MouseEventArgs e)
        {
            Label label = sender as Label;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
        }

        private void Label7_MouseLeave(object sender, MouseEventArgs e)
        {
            Label label = sender as Label;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
        }

        private void ShowEula_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Eula.Visibility = Visibility.Visible;
            Login.Visibility = Visibility.Collapsed;
            CloseEula.Visibility = Visibility.Visible;
        }

        private void CloseEula_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Eula.Visibility = Visibility.Collapsed;
            Login.Visibility = Visibility.Visible;
        }

        private void PrintEula_MouseDown(object sender, MouseButtonEventArgs e)
        {
            EulaDoc.Foreground = Brushes.Black;
            DoThePrint(EulaDoc);
            EulaDoc.Foreground = Brushes.White;
        }

        private void DoThePrint(System.Windows.Documents.FlowDocument document)
        {
            System.IO.MemoryStream s = new System.IO.MemoryStream();
            TextRange source = new TextRange(document.ContentStart, document.ContentEnd);
            source.Save(s, DataFormats.Xaml);
            FlowDocument copy = new FlowDocument();
            TextRange dest = new TextRange(copy.ContentStart, copy.ContentEnd);
            dest.Load(s, DataFormats.Xaml);

            System.Printing.PrintDocumentImageableArea ia = null;

            //PageRangeSelection pageRangeSelection = new PageRangeSelection();
            //PageRange pageRange = new PageRange();
            System.Windows.Xps.XpsDocumentWriter docWriter = System.Printing.PrintQueue.CreateXpsDocumentWriter(ref ia); //, ref pageRangeSelection, ref pageRange);

            if (docWriter != null && ia != null)
            {
                DocumentPaginator paginator = ((IDocumentPaginatorSource)copy).DocumentPaginator;

                paginator.PageSize = new Size(ia.MediaSizeWidth, ia.MediaSizeHeight);
                Thickness t = new Thickness(72);  // copy.PagePadding;
                copy.PagePadding = new Thickness(
                                 Math.Max(ia.OriginWidth, t.Left),
                                 Math.Max(ia.OriginHeight, t.Top),
                                 Math.Max(ia.MediaSizeWidth - (ia.OriginWidth + ia.ExtentWidth), t.Right),
                                 Math.Max(ia.MediaSizeHeight - (ia.OriginHeight + ia.ExtentHeight), t.Bottom));

                copy.ColumnWidth = double.PositiveInfinity;


                //var page = paginator.GetPage(1);
                //docWriter.Write(page.Visual);

                docWriter.Write(paginator);
            }

        }

        bool _letInRick = false;

        private void Label_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _letInRick = true;
        }

		private void Login_Mousedown(object sender, MouseButtonEventArgs e)
		{

		}
	}


	public class IPDFDocument : ContentControl
    {
        virtual public bool Ready() { return false; }
        virtual public void Close() { }
    }

    public enum ReviewAction
    {
        AnticipateShort,
        AnticipateBuy,
        AddShort,
        AddLong,
        HoldPosition,
        HoldLong,
        StayingOut,
        ExpectLongExit,
        ExpectShortExit
    }

    public class AnticipatedAction
    {
        public AnticipatedAction(ReviewAction action, DateTime time)
        {
            Action = action;
            Time = time;
        }
        public ReviewAction Action;
        public DateTime Time;
    }

    public class Research
    {
        public int Direction { get; set; }
        public string Description { get; set; }
        public string Comment { get; set; }
    }
}
