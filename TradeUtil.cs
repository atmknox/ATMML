using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using System.IO;
using System.Threading;
using tmsg;
using System.Globalization;
using System.Net;

namespace ATMML
{
    public enum TradeHorizon
    {
        ShortTerm,
        MidTerm,
        LongTerm,
        VeryLongTerm,
        ExtraLongTerm,
        Any
    }

    public enum TradeEventType
    {
        None,
        PRTU_Loading_Complete
    }

    public class TradeEventArgs : EventArgs
    {
        public TradeEventArgs(TradeEventType type = TradeEventType.None)
        {
            Type = type;
        }

       public TradeEventType Type { get; set; }
    }

    public class NewPositionEventArgs : EventArgs
    {
        bool _complete;

        public NewPositionEventArgs(bool complete)
        {
            _complete = complete;
        }

        public bool Complete
        {
            get { return _complete; }
        }
    }

    public delegate void TradeEventHandler(object sender, TradeEventArgs e);
    public delegate void NewPositionEventHandler(object sender, NewPositionEventArgs e);

    public class TradeManager
    {
        private bool _ready = false;

        public event TradeEventHandler TradeEvent;
        public event NewPositionEventHandler NewPositions;

        bool _saveEnable = true;

       // CQGCEL m_CEL;

        Dictionary<string, Trade> _positionDataBase = new Dictionary<string, Trade>();
        object _positionLock = new Object();
        List<Trade> _positions = new List<Trade>();
        List<Trade> _newPositions = new List<Trade>();

        Dictionary<string, DateTime> _currentTradeStart = new Dictionary<string, DateTime>();

        Portfolio _portfolio = new Portfolio(16);
        Dictionary<string, Symbol> _symbols = new Dictionary<string, Symbol>();
        int _barRequestCount = 0;
        int _betaRequestCount = 0;
        bool _useBeta = false;

        //List<string> _messages = new List<string>();
 
        //int _positionCount = 0;

        bool _update = false;
        BarCache _barCache;

        Dictionary<string, AccountInfo> _account = new Dictionary<string, AccountInfo>();

        private object _cmrDataLock = new object();
#if USE_CMR_SERVER
        private CMRDataChannel _cmrData = null;
#endif

        private DateTime _timeStamp = DateTime.MinValue;
        private DateTime _pollForNewPositions = DateTime.MinValue;

        bool _firstTimer = true;
        System.Timers.Timer _timer;
        int _seconds = 0;

        Dictionary<string, int> _barsReceived = new Dictionary<string, int>();

        // data base portfolios
        string[] _portfolioNames = { 
               "US 100 H", 
               "US 100", 
               "US 500", 
               "SP GROUPS", 
               //"COUNTRY ETF", 
               "GLOBAL INDICES"};

        private Dictionary<string, Dictionary<string, Symbol>> _portfolios = new Dictionary<string, Dictionary<string, Symbol>>();

        private bool _readTradeIdeasThreadRunning = true;

        public TradeManager()
        {
            //initializeCQG();

            lock (_account)
            {
                _account.Add("US 100", new AccountInfo());
                _account["US 100"].StartDate = new DateTime(2010, 1, 1, 0, 0, 0);
                _account["US 100"].StartBalance = 100000000;   // was 100000000

                if (MainView.EnableRickStocks)
                {
                    _account.Add("SINGLE CTY ETF", new AccountInfo());
                    _account["SINGLE CTY ETF"].StartDate = new DateTime(1990, 1, 1, 0, 0, 0);
                    _account["SINGLE CTY ETF"].StartBalance = 0;
                }

                if (MainView.EnableUS500)
                {
                    _account.Add("US 500", new AccountInfo());
                    _account["US 500"].StartDate = new DateTime(1990, 1, 1, 0, 0, 0);
                    _account["US 500"].StartBalance = 100000000;
                }

                if (MainView.EnableGlobalIndices)
                {
                    _account.Add("GLOBAL INDICES", new AccountInfo());
                    _account["GLOBAL INDICES"].StartDate = new DateTime(1990, 1, 1, 0, 0, 0);
                    _account["GLOBAL INDICES"].StartBalance = 0;
                }

                if (MainView.EnableRickStocks)
                {
                    _account.Add("US 100 H", new AccountInfo());
                    _account["US 100 H"].StartDate = new DateTime(2010, 6, 1, 0, 0, 0);
                    _account["US 100 H"].StartBalance = 0;
                }
            }
 
            _portfolio.PortfolioChanged += new PortfolioChangedEventHandler(portfolioChanged);
            _barCache = new BarCache(barChanged);

            _timer = new System.Timers.Timer(1000);
            _timer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
            _timer.AutoReset = true;
            _timer.Start();

            loadPositionDataBase();
        }

        private void loadPositionDataBase()
        {
            _positionDataBase.Clear();                             

            // to add trades manually - add them to PositionDataBase.csv
            // and then temporarily comment out reading the data base from the server

            // copy database from Capital Markets Research server to local database
            //if (!MainView.EnableEditTrades)
            //{
            //    readServerDataBase();  //use for laptop version
            //}

            // load trades from local data base
            //loadLocalTrades();

            // load trades from Bloomberg TMSG database or set ready
            Thread thread = new Thread(new ThreadStart(loadPositionInfos));
            thread.Start();
            //_ready = true;
        }

        private void savePositionDataBase()
        {
        }

        public Dictionary<string, Dictionary<string, Symbol>> Portfolios
        {
            get { return _portfolios; }
        }

        private IdeaIdType openTradeIdea(string networkName, string symbol, string hedge, int size, bool networkHedge)
        {
            IdeaIdType id = new IdeaIdType { ThirdPartyId = Guid.NewGuid().ToString() };
            // connection code removed
            return id;
        }

        private void addTrade(Trade trade)
        {
            var request = getRequest(MainView.CMREndPoint + "/addTrade.php?", trade);
            MainView.SendWebRequest(request);
        }

        private void removeTrade(Trade trade)
        {
            MainView.SendWebRequest(MainView.CMREndPoint + "/removeTrade.php?i1=" + trade.Id.ThirdPartyId);
            //sendMail(trade.Network, trade, 1);
        }

        private void updateTrade(Trade trade)
        {
            MainView.SendWebRequest(getRequest(MainView.CMREndPoint + "/updateTrade.php?", trade));
            //sendMail(trade.Network, trade, 2);
        }

        private bool createSqPtIdea(Trade trade, string comment)
        {
            bool ok = false;
            // connection code removed
            return ok;
        }

        private void loadSqPtIdeas()
        {
            // connection code removed
        }

        private bool modifySqPtIdea(string action, int units, string id, string comment)
        {
            bool ok = false;
            // connection code removed
            return ok;
        }

        private int getSqPtIdea(string id)
        {
            int size = 0;
            // connection code removed
            return size;
        }

        private string getRequest(string input, Trade trade)
        {
            string output = input;
            output += "n=" + trade.Group;
            output += "&s=" + trade.Ticker;
            output += "&h=" + (trade.Id.BloombergIdSpecified? "true" : "false");
            output += "&b=" + trade.Id.BloombergId.ToString();
            output += "&i1=" + trade.Id.ThirdPartyId.ToString();
            output += "&z=" + trade.Direction;
            output += "&d1=" + trade.EntryDate;
            output += "&p1=" + trade.EntryPrice.ToString();
            output += "&d2=" + trade.ExitDate;
            output += "&p2=" + trade.ExitPrice.ToString();
            output += "&i2=" + trade.Investment1.ToString();
            output += "&p=" + trade.Price.ToString();
            output += "&pnl=" + trade.PnL.ToString();
            output += "&u=" + MainView.Username;
            return output;
        }
        
        private void closeTradeIdea(string networkName, string symbol, IdeaIdType id, string date, double price, bool saveToServer)
        {
            // connection code removed
        }

        private bool hasSymbol(string symbol, IdeaInstrumentType instrument)
        {
            bool ok = false;

            if (instrument != null)
            {
                var item = instrument.Item;
                if (item != null)
                {
                    SecurityType securityType = item as SecurityType;

                    if (securityType != null)
                    {
                        var securityIdentifierType = securityType.Identifier;

                        if (securityIdentifierType != null)
                        {
                            //if (securityIdentifierType.ParseKey == symbol)
                            //{
                            //    ok = true;
                            //}
                        }
                    }
                }
            }
            return ok;
        }

        Dictionary<string, Symbol> getCMRPortfolio(string name)
        {
            Dictionary<string, Symbol> symbols = new Dictionary<string, Symbol>();

#if USE_CMR_SERVER
            CMRDataChannel cmrData = getDataChannel();
            if (cmrData != null)
            {
                int firstId = -1;
                while (true)
                {
                    string text = (cmrData != null) ? cmrData.GetPortfolio(name, firstId) : "";
                    if (text.Length == 0 || text.Substring(0, 9) == "EXCEPTION")
                    {
                        break;
                    }
                    firstId = loadCMRPortfolio(name, text, symbols);
                }
            }
#endif
            return symbols;
        }

        private void saveAccounts()
        {
            string info = "";
            lock (_account)
            {
                /* 0 */
                info += (MainView.EnableUS100) ? _account["US 100"].StartBalance.ToString() + ";" + _account["US 100"].StartDate.ToString("yyyy-MM-dd") + ";" : "100000000;2010-01-01;";
                /* 1 */
                info += (MainView.EnableUS500) ? _account["SINGLE CTY ETF"].StartBalance.ToString() + ";" + _account["SINGLE CTY ETF"].StartDate.ToString("yyyy-MM-dd") + ";" : "0;1990-01-01;";
                /* 2 */
                info += (MainView.EnableUS500) ? _account["GLOBAL INDICES"].StartBalance.ToString() + ";" + _account["GLOBAL INDICES"].StartDate.ToString("yyyy-MM-dd") + ";" : "0;1990-01-01;";
                info += (MainView.EnableRickStocks) ? _account["US 100 H"].StartBalance.ToString() + ";" + _account["US 100 H"].StartDate.ToString("yyyy-MM-dd") + ";" : "0;1990-01-01;";
                /* 26 */
                info += (MainView.EnableRickStocks) ? _account["GLOBAL MACRO"].StartBalance.ToString() + ";" + _account["GLOBAL MACRO"].StartDate.ToString("yyyy-MM-dd") + ";" : "50000000;1990-01-01;";
                /* 27 */
                info += (MainView.EnableRickStocks) ? _account["GLOBAL 10YR"].StartBalance.ToString() + ";" + _account["GLOBAL 10YR"].StartDate.ToString("yyyy-MM-dd") + ";" : "50000000;1990-01-01;";
                /* 28 */
                info += (MainView.EnableRickStocks) ? _account["SPOT FX"].StartBalance.ToString() + ";" + _account["GLOBAL FX"].StartDate.ToString("yyyy-MM-dd") + ";" : "50000000;1990-01-01;";
                                                           
            }

            MainView.SaveSetting("Accounts1", info);
        }

        private void loadAccounts()
        {
            // todo... load the account balance in and fix the field numbers
            lock(_account)
            {
                try
                {
                    string info = MainView.GetSetting("Accounts1");
                    if (info != null && info.Length > 0)
                    {
                        string[] fields = info.Split(';');
                        int count = fields.Length;

                        if (count > 0) _account["US 100"].StartDate = DateTime.Parse(fields[0]);

                        if (MainView.EnableRickStocks)
                        {
                            if (count > 1) _account["SINGLE CTY ETF"].StartDate = DateTime.Parse(fields[1]);
                        }
                        if (MainView.EnableRickStocks)
                        {
                            if (count > 2) _account["GLOBAL INDICES"].StartDate = DateTime.Parse(fields[2]);
                        }
                        if (MainView.EnableRickStocks)
                        {
                            if (count > 24) _account["US 100 H"].StartDate = DateTime.Parse(fields[24]);
                        }
                        if (MainView.EnableRickStocks)
                        {
                            if (count > 26) _account["GLOBAL MACRO"].StartDate = DateTime.Parse(fields[25]);
                        }
                        if (MainView.EnableRickStocks)
                        {
                            if (count > 27) _account["GLOBAL 10YR"].StartDate = DateTime.Parse(fields[25]);
                        }
                        if (MainView.EnableRickStocks)
                        {
                            if (count > 28) _account["SPOT FX"].StartDate = DateTime.Parse(fields[25]);
                        }
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        string _tradeTimeStamp = "";

        private void refreshPositionInfos()
        {
            WebRequest request = WebRequest.Create(MainView.CMREndPoint + "/refreshTrades.php?t=" + _tradeTimeStamp);
            WebResponse response = request.GetResponse();
            Stream stream = response.GetResponseStream();
            StreamReader reader = new StreamReader(stream);
            string value = reader.ReadToEnd();
            string[] rows = value.Split('\n');
            _tradeTimeStamp = "";
            bool update = false;
            foreach (string row in rows)
            {
                if (row.Length > 0)
                {
                    int index = row.IndexOf(',');
                    string timeStamp = row.Substring(0, index);
                    if (timeStamp.CompareTo(_tradeTimeStamp) > 0) _tradeTimeStamp = timeStamp;
                    string trade = row.Substring(index + 1);
                    Trade positionInfo = new Trade();
                    positionInfo.Deserialize(trade);
                    string id = positionInfo.Id.BloombergIdSpecified ? positionInfo.Id.BloombergId.ToString() : positionInfo.Id.ThirdPartyId;
                    _positionDataBase[id] = positionInfo;
                    update = true;
                }
            }

            if (update)
            {
                lock (_positionLock)
                {
                    _positions.Clear();
                    foreach (Trade trade in _positionDataBase.Values)
                    {
                        _positions.Add(trade);
                    }
                    _positions.Sort();
                }
                _update = true;
            }
        }

        private void loadTrades()
        {
            List<Trade> trades = new List<Trade>();

            string value = MainView.SendWebRequest(MainView.CMREndPoint + "/trades.php");

            string[] rows = value.Split('\n');
            _tradeTimeStamp = "";
            foreach (string row in rows)
            {
                if (row.Length > 0)
                {
                    try
                    {
                        int index = row.IndexOf(',');
                        string timeStamp = row.Substring(0, index);
                        if (timeStamp.CompareTo(_tradeTimeStamp) > 0) _tradeTimeStamp = timeStamp;
                        string trade = row.Substring(index + 1);
                        Trade positionInfo = new Trade();
                        positionInfo.Deserialize(trade);
                        string id = positionInfo.Id.BloombergIdSpecified ? positionInfo.Id.BloombergId.ToString() : positionInfo.Id.ThirdPartyId;


                        bool bp = false;
                        if (_positionDataBase.ContainsKey(id))
                        {
                            bp = true;
                        }

                        _positionDataBase[id] = positionInfo;
                        trades.Add(positionInfo);
                    }
                    catch (Exception x)
                    {
                        //Trace.WriteLine("Bad trade: " + row);
                    }
                }
            }
            //var oexmt = trades.Where(x => x.Network == "OEX MT").ToList();
        }

        DateTime _prtuTime = DateTime.Now;
        bool _prtuLoaded = false;

        public void LoadPRTUTrades(string PRTUid, List<PRTUTrade> input)
        {
            //Trace.WriteLine(DateTime.Now.ToString("yyyyMMdd HH:mm:ss") + " Response " + PRTUid + " count = " + input.Count);

            _prtuTime = DateTime.Now;
            _prtuLoaded = true;

            string network = PRTUid;

            {

                lock (_positionLock)
                {
                    removeAllTradesFromNetwork(PRTUid);
                    foreach (PRTUTrade trade in input)
                    {
                        Trade positionInfo = new Trade();
                        positionInfo.Group = network;
                        positionInfo.Id = new IdeaIdType { ThirdPartyId = network + "-" + trade.Symbol };
                        positionInfo.Ticker = trade.Symbol;

                        _portfolio.RequestReferenceData(trade.Symbol, new string[] { "DS192", "GICS_SECTOR"} );

                        double direction = 0;
                        double investment = 0;
                        double.TryParse(trade.Position, out direction);
                        double.TryParse(trade.MarketValue, out investment);
                        positionInfo.Direction = (int)Math.Round(direction);
                        positionInfo.Investment1 = investment;
                        positionInfo.OpenDateTime = DateTime.ParseExact(trade.EntryDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                        positionInfo.CloseDateTime = DateTime.MaxValue;
                        string id = positionInfo.Id.BloombergIdSpecified ? positionInfo.Id.BloombergId.ToString() : positionInfo.Id.ThirdPartyId;
                        _positions.Add(positionInfo);
                        _positionDataBase[id] = positionInfo;
                    }
                    _positions.Sort();
                }
            }

            //requestBarsForMissingTradePrices();

            onTradeEvent(new TradeEventArgs(TradeEventType.PRTU_Loading_Complete));
        }

        private void removeAllTradesFromNetwork(string network)
        {
            lock (_positionLock)
            {
                List<Trade> positions = new List<Trade>();
                foreach (Trade trade in _positions)
                {
                    if (trade.Group != network)
                    {
                        positions.Add(trade);
                    }
                }
                _positions = positions;
                _positions.Sort();
            }
        }

        // requests PRTU which causes the Portfoio to call back to loadPRTUTrade
        private void loadPinzTrades()
        {
            //Trace.WriteLine(DateTime.Now.ToString("yyyyMMdd HH:mm:ss") + " Request PRTUs");

            //_portfolio.RequestSymbols("U20365502-3", Portfolio.PortfolioType.PRTU, false);  //Pinz Main

            //_portfolio.RequestSymbols("U13087063-35", Portfolio.PortfolioType.PRTU, false);  //Pinz RESEARCH Buys Asia Europe
            //_portfolio.RequestSymbols("U13087063-34", Portfolio.PortfolioType.PRTU, false);  //Pinz RESEARCH Sells Asia Europe

            //_portfolio.RequestSymbols("U13087063-51", Portfolio.PortfolioType.PRTU, false);  //Pinz RESEARCH Buys NA SA
            //_portfolio.RequestSymbols("U13087063-36", Portfolio.PortfolioType.PRTU, false);  //Pinz RESEARCH Sells NA SA

            //_portfolio.RequestSymbols("U13127220-129", Portfolio.PortfolioType.PRTU, false);
            //_portfolio.RequestSymbols("U6890015-17", Portfolio.PortfolioType.PRTU, false);
            //_portfolio.RequestSymbols("U13127220-99", Portfolio.PortfolioType.PRTU, false);
            //_portfolio.RequestSymbols("U13127220-98", Portfolio.PortfolioType.PRTU, false);

            //_portfolio.RequestSymbols("U6890015-11", Portfolio.PortfolioType.PRTU, false);
        }

        private void loadSQPTTrades()
        {
            _portfolio.RequestSymbols("U6890015-18", Portfolio.PortfolioType.PRTU, false);

        }

        // background thread to load trades
        private void loadPositionInfos()
        {
            clearPositions();

#if USE_CMR_SERVER
            loadCMRPortfolios();
            loadCMRPositions();
#endif
   
            _update = true;

        }

        public List<Trade> GetNewPositions()
        {
            List<Trade> positions = new List<Trade>();
            lock (_newPositions)
            {
                positions.AddRange(_newPositions);
            }
            return positions;
        }

        public void Close()
        {
            _readTradeIdeasThreadRunning = false;

            _portfolio.Close();

            _timer.Stop();

            savePositionDataBase();

#if USE_CMR_SERVER
            if (_cmrData != null)
            {
                _cmrData.Close();
            }
#endif
        }

        private void onTradeEvent(TradeEventArgs e)
        {
            if (TradeEvent != null)
            {
                TradeEvent(this, e);
            }
        }

        private void loadPortfolio()
        {
            List<Symbol> symbols = new List<Symbol>();

            symbols.AddRange(_portfolio.GetSymbols("US 100"));

            if (MainView.EnableUS500)
            {
                symbols.AddRange(_portfolio.GetSymbols("US 500"));
            }
            if (MainView.EnableRickStocks)
            {
                symbols.AddRange(_portfolio.GetSymbols("SP GROUPS"));
            }
            if (MainView.EnableRickStocks)
            {
                symbols.AddRange(_portfolio.GetSymbols("US 100 H"));
            }
            if (MainView.EnableGlobalIndices)
            {
                symbols.AddRange(_portfolio.GetSymbols("GLOBAL INDICES"));
            }

            if (_symbols.Count > 0)
            {
                _portfolio.SetSymbols(symbols);
            }
        }

        private void requestBetas(List<Symbol> symbols)
        {
            _betaRequestCount = 0;

            if (_useBeta)
            {
                Dictionary<string, List<DateTime>> requestBetaList = new Dictionary<string, List<DateTime>>();

                List<Trade> trades = getTrades(TradeHorizon.MidTerm, symbols, "");

                foreach (Trade trade in trades)
                {
                    string ticker = trade.Ticker;
                    if (!requestBetaList.ContainsKey(ticker))
                    {
                        requestBetaList[ticker] = new List<DateTime>();
                    }
                    _betaRequestCount++;

                    requestBetaList[ticker].Add(DateTime.Parse(trade.EntryDate));
                }

                _portfolio.RequestBetas(requestBetaList);
            }
        }

        private void requestBars(List<string> symbols)
        {
            _barCache.Clear();

            lock (_barsReceived)
            {
                _barsReceived.Clear();
                foreach (string symbol in symbols)
                {
                    _barCache.RequestBars(symbol, "Daily", true);

                    _barsReceived[symbol] = 0;
                }
                _barRequestCount = _barsReceived.Count;
            }
        }

        private void barChanged(object sender, BarEventArgs e)
        {
            if (e.Type == BarEventArgs.EventType.BarsLoaded || e.Type == BarEventArgs.EventType.BarsReceived)
            {
                bool sendNotification = false;

                string ticker = e.Ticker;
                string interval = e.Interval;
                
                List<DateTime> times = _barCache.GetTimes(ticker, interval, 0);
                Series[] series = _barCache.GetSeries(ticker, interval, new string[] { "Close" }, 0);
                Series cl = series[0];
                if (times.Count > 0)
                {
                    lock (_positionLock) {
                        foreach (Trade trade in _positions)
                        {
                            if (trade.Ticker == ticker)
                            {
                                TimeSpan oneDay = new TimeSpan(1, 0, 0, 0);

                                if (double.IsNaN(trade.OpenPrice))
                                {
                                    DateTime dateTime = new DateTime(trade.OpenDateTime.Year, trade.OpenDateTime.Month, trade.OpenDateTime.Day);
                                    for (int ii = 0; ii < 10; ii++)
                                    {
                                        int index = times.FindIndex(item => item.CompareTo(dateTime) == 0);
                                        if (index != -1 && !double.IsNaN(cl[index]))
                                        {
                                            trade.OpenPrice = cl[index];
                                            //_update = true;
                                            sendNotification = true;
                                            break;
                                        }
                                        dateTime += oneDay;
                                    }
                                }
                                if (double.IsNaN(trade.ClosePrice) && !trade.IsOpen())
                                {
                                    DateTime dateTime = new DateTime(trade.CloseDateTime.Year, trade.CloseDateTime.Month, trade.CloseDateTime.Day);
                                    for (int ii = 0; ii < 10; ii++)
                                    {
                                        int index = times.FindIndex(item => item.CompareTo(dateTime) == 0);
                                        if (index != -1 && !double.IsNaN(cl[index]))
                                        {
                                            trade.ClosePrice = cl[index];
                                            //_update = true;
                                            sendNotification = true;
                                            break;
                                        }
                                        dateTime += oneDay;
                                    }
                                }

                                for (int ii = cl.Count; ii >= 0; ii--)
                                {
                                    double lastClose = cl[ii];
                                    if (!double.IsNaN(lastClose))
                                    {
                                        trade.LastPrice = lastClose;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }

                lock (_barsReceived)
                {
                    int count = 0;
                    if (_barsReceived.TryGetValue(ticker, out count))
                    {
                        if (count == 0)
                        {
                            _barsReceived[ticker] = 1;
                            _barRequestCount--;
                            if (_barRequestCount == 0)
                            {
                                savePositionDataBase();
                                _update = true;
                                _ready = true;
                            }
                        }
                    }
                }
            }
        }

        void portfolioChanged(object sender, PortfolioEventArgs e)
        {
            if (_useBeta)
            {
                _betaRequestCount--;
                if (_betaRequestCount == 0)
                {
                    _update = true;
                }
            }
            else
            {
                loadSymbols();
            }
        }

        private void update(string symbol)
        {
            return;
        }

        private void updatePortfolio(string portfolioName, string ticker)
        {
            bool ok = true;
            List<Symbol> symbols = _portfolio.GetSymbols(portfolioName);
            
            if (ticker.Length > 0)
            {
                ok = false;
                foreach (Symbol symbol in symbols)
                {
                    if (ticker == symbol.Ticker)
                    {
                        ok = true;
                        break;
                    }
                }
            }

            if (ok)
            {
                lock (_account)
                {
                    string startDate = _account[portfolioName].StartDate.ToString("yyyy-MM-dd");

                    TradeHorizon tradeHorizon = GetTradeHorizon(portfolioName);

                    _account[portfolioName].Trades = getTrades(tradeHorizon, symbols, startDate);
                }
                //calculateTradeSizes(portfolioName);
            }
        }

        private double getClosedTradeProfit(string date, List<Trade> openTrades)
        {
            double profit = 0;
            List<Trade> closedTrades = new List<Trade>();
            foreach (Trade trade in openTrades)
            {
                if (!trade.IsOpen() && date.CompareTo(trade.ExitDate) >= 0)
                {
                    closedTrades.Add(trade);
                }
            }

            foreach (Trade trade in closedTrades)
            {
                bool hedge = (trade.Ticker == "ES1 Index");
                double multiplier = hedge ? 50 : 1;

                profit += multiplier * trade.Direction * (trade.ExitPrice - trade.EntryPrice);
                openTrades.Remove(trade);
            }
            return profit;
        }

        private double getOpenTradeProfit(string date, List<Trade> openTrades)
        {
            double profit = 0;
            foreach (Trade trade in openTrades)
            {
                bool hedge = (trade.Ticker == "ES1 Index");
                double multiplier = hedge ? 50 : 1;

                double close = getClose(trade.Ticker, date);

                if (!double.IsNaN(close))
                {
                    profit += multiplier * trade.Direction * (close - trade.EntryPrice);
                }
                else if (trade.Ticker.CompareTo("SLE UN Equity") != 0)
                {
                    double text = getClose(trade.Ticker, date);
                }
            }
            return profit;
        }

        private double getOpenTradeValue(int direction, string date, List<Trade> openTrades)
        {
            double value = 0;
            foreach (Trade trade in openTrades)
            {
                if (trade.Direction == direction)
                {
                    double close = getClose(trade.Ticker, date);
                    if (!double.IsNaN(close))
                    {
                        value += Math.Abs(trade.Direction) * close;
                    }
                }
            }
            return value;
        }

        public double GetMarkToMarketPrice(string symbol, int year, int month)
        {
            double price = double.NaN;
            string interval = "Daily";
            List<DateTime> times = _barCache.GetTimes(symbol, interval, 0);
            Series[] series = _barCache.GetSeries(symbol, interval, new string[] { "Close" }, 0);
            if (times != null && series.Length > 0 && series[0] != null && series[0].Count > 0 && times.Count > 0)
            {
                for (int ii = 0; ii < times.Count; ii++)
                {
                    if (times[ii].Year < year || (times[ii].Year == year && times[ii].Month <= month))
                    {
                        price = series[0][ii];
                    }
                    else
                    {
                        break;
                    }
                }
            } return price;
        }

        public double GetLastPrice(string symbol)
        {
            double price = double.NaN;
            string interval = "Daily";
            List<DateTime> times = _barCache.GetTimes(symbol, interval, 0);
            Series[] series = _barCache.GetSeries(symbol, interval, new string[] { "Close" }, 0);
            if (times != null && series.Length > 0 && series[0] != null && series[0].Count > 0 && times.Count > 0)
            {
                var index = series[0].Count - 1;
                price = series[0][index];
                if (double.IsNaN(price) && index > 0)
                {
                    price = series[0][index - 1];
                }
            }
            return price;
        }

        public double GetPrice(string symbol, DateTime date)
        {
            return getClose(symbol, date.ToString());
        }
 
        private double getClose(string symbol, string date)
        {
            double close = double.NaN;

            string interval = "Daily";
            List<DateTime> times = _barCache.GetTimes(symbol, interval, 0);
            Series[] series = _barCache.GetSeries(symbol, interval, new string[] { "Close" }, 0);
            if (times != null && series.Length > 0 && series[0] != null && series[0].Count > 0 && times.Count > 0)
            {
                DateTime dateTime = DateTime.Parse(date);
                for (int ii = 0; ii < times.Count; ii++)
                {
                    if (times[ii] <= dateTime)
                    {
                        close = series[0][ii];
                    }
                    else
                    {
                        break;
                    }
                }
            }
            return close;
        }

        private double getTradePrice(Symbol symbol, string date)
        {
            double price = double.NaN;

            if (date.Length == 10)
            {
                bool vwapOnTradeDate = false;

                // todo - add VWAP as an option
                if (date != "")
                {
                    string vwapDate = "";
                    lock (symbol)
                    {
                        foreach (string date1 in symbol.VWAP.Keys)
                        {
                            int dateCmp1 = date1.CompareTo(date);
                            if (dateCmp1 > 0 || (vwapOnTradeDate && dateCmp1 == 0))
                            {
                                int dateCmp2 = date1.CompareTo(vwapDate);
                                if (vwapDate == "" || (dateCmp2 < 0 || (vwapOnTradeDate && dateCmp2 == 0)))
                                {
                                    vwapDate = date1;
                                }
                            }
                        }

                        if (vwapDate != "")
                        {
                            price = symbol.VWAP[vwapDate];
                        }
                    }
                }
            }
            return price;
        }

        private void findDividends(string portfolioName, Symbol symbol, string entryDate, string exitDate, int size, ref Dictionary<string, double> dividends)
        {
            if (exitDate == "")
            {
                exitDate = DateTime.Now.ToString("yyyy-MM-dd");
            }

            foreach (KeyValuePair<string, double> kvp in symbol.Dividends)
            {
                string key = kvp.Key;
                string[] fields = key.Split(':');
                string exDate = fields[0];
                if (exDate.CompareTo(entryDate) >= 0 && exDate.CompareTo(exitDate) < 0)
                {
                    string date = fields[1];
                    double dollar = size * kvp.Value;
                    dividends[date] = (dividends.ContainsKey(date)) ? dividends[date] + dollar : dollar;
                    lock (_account)
                    {
                        _account[portfolioName].Dividends[date] = (_account[portfolioName].Dividends.ContainsKey(date)) ? _account[portfolioName].Dividends[date] + dollar : dollar;
                    }
                }
            }
        }

        private double getDividendAmount(string entryDate, ref Dictionary<string, double> dividends)
        {
            double amount = 0;

            // use dividends on or before entry date
            List<string> dates = new List<string>();
            foreach (KeyValuePair<string, double> kvp in dividends)
            {
                string payableDate = kvp.Key;
                double dollar = kvp.Value;
                if (payableDate.CompareTo(entryDate) <= 0)
                {
                    amount += dollar;
                    dates.Add(payableDate);
                }
            }

            // remove dividends from the dividend container that were used
            foreach (string date in dates)
            {
                dividends.Remove(date);
            }

            return amount;
        }

        private void loadSymbols()
        {
            lock (_symbols)
            {
                _symbols.Clear();

                List<Symbol> symbols = _portfolio.GetSymbols();
                foreach (Symbol symbol in symbols)
                {
                    string ticker = symbol.Ticker;
                    _symbols[ticker] = symbol;
                }

                //requestBars();
                //requestBetas(symbols);
            }
        }

        public void clearPositions()
        {
            lock (_positionLock)
            {
                _positions.Clear();
            }
        }

        public int loadPositions(string positions, bool newPositions)
        {
            int id = 0;
#if USE_CMR_SERVER
            if (positions != null && positions.Length >= 9 && positions.Substring(0, 9).CompareTo("EXCEPTION") != 0)
            {
                string previousWeekday;
                DateTime dateTime = DateTime.Now;
                TimeSpan oneDay = new TimeSpan(1, 0, 0, 0);
                DateTime date1 = new DateTime(dateTime.Year, dateTime.Month, dateTime.Day);
                do
                {
                    date1 -= oneDay;
                } while (date1.DayOfWeek == DayOfWeek.Saturday || date1.DayOfWeek == DayOfWeek.Sunday);
                previousWeekday = date1.ToString("yyyy-MM-dd");

                string[] lines = positions.Split('\n');

                foreach (string line in lines)
                {
                    if (line.Length > 0)
                    {
                        string[] info = line.Split(',');
                        int count = info.Length;

                        if (count == 1)
                        {
                            _timeStamp = DateTime.Parse(info[0]);
                        }
                        else if (count >= 6)
                        {
                            int index = 0;

                            id = int.Parse(info[index]);
                            index++;
                            string ticker = info[index];
                            string[] tickerFields = ticker.Split(' ');
                            if (tickerFields.Length == 2 && tickerFields[1] == "Equity")
                            {
                                ticker = tickerFields[0] + " US Equity";
                            }
                            index++;
                            string interval = info[index];
                            index++;
                            int direction = (info[index].Length > 0) ? int.Parse(info[index]) : 0;
                            index++;

                            string date = "";
                            if (info[index].Length > 0)
                            {
                                DateTime dateTime1 = DateTime.Parse(info[index]);
                                bool atZeroHour1 = (dateTime1.Hour == 0 && dateTime1.Minute == 0);
                                date = dateTime1.ToString(atZeroHour1 ? "yyyy-MM-dd" : "yyyy-MM-dd HH:mm");
                            }

                            index++;
                            double price = (info[index].Length > 0) ? double.Parse(info[index]) : double.NaN;

                            _positionCount++;

                            PositionInfo position2 = new PositionInfo(ticker, interval, direction, date, price);

                            lock (_positionLock)
                            {
                                if (direction == -2)  // remove event
                                {
                                    foreach (PositionInfo position in _positions)
                                    {
                                        if (position.Interval == interval && position.compareSymbol(ticker) == 0 && position.Date.CompareTo(date) == 0)
                                        {
                                            _positions.Remove(position);
                                            break;
                                        }
                                    }
                                }
                                else  // add event
                                {
                                    if (newPositions)
                                    {
                                        PositionInfo position1 = findPosition(position2);
                                        if (position1 != null)
                                        {
                                            _positions.Remove(position1);
                                        }
                                    }

                                    Symbol symbol;
                                    if (_symbols.TryGetValue(ticker, out symbol))
                                    {
                                        double tradePrice = getTradePrice(symbol, position2.Date);
                                        if (!double.IsNaN(tradePrice))
                                        {
                                            position2.Price = tradePrice;
                                        }
                                    }

                                    _positions.Add(position2);
                                }
                            }

                            if (direction != 9 && (newPositions || (position2.Date.CompareTo(previousWeekday) >= 0)))
                            {
                                lock (_newPositions)
                                {
                                    _newPositions.Add(position2);
                                }
                            }
                        }
                    }
                }
            }
#endif
            return id;
        }

        public Trade findPosition(Trade input)
        {
            Trade output = null;
            lock (_positionLock)
            {
                foreach (Trade position in _positions)
                {
                    if (position.CompareTo(input) == 0 && position.Direction == input.Direction)
                    {
                        output = position;
                        break;
                    }
                }
            }
            return output;
        }

        // for excel files
        public string getPositionsText(ref int index)
        {
            string positions = "";

            lock (_positionLock)
            {
                int count = _positions.Count;
                for (; index < count; index++)
                {
                    positions += _positions[index].ToString() + "\r\n";
                    if (positions.Length > 10000)
                    {
                        break;
                    }
                }
            }
            return positions;
        }

        public void SavePositions()
        {
            //Thread thread = new Thread(new ThreadStart(savePositionsToCMRData));
            //thread.Start();
        }

        public void RemovePosition(string portfolioName, TradeHorizon horizon, string symbol, string date)
        {
            lock (_positionLock)
            {
                string network = _portfolio.GetTradeNetwork(portfolioName, horizon, symbol);
                DateTime dateTime = DateTime.Parse(date);
                foreach (Trade position in _positions)
                {
                    if (network == position.Group && position.compareSymbol(symbol) == 0 && position.OpenDateTime.CompareTo(dateTime) == 0)
                    {
                        string id = position.Id.BloombergIdSpecified ? position.Id.BloombergId.ToString() : position.Id.ThirdPartyId;                    
                       _positions.Remove(position);
                       _positionDataBase.Remove(id);
                        update(symbol);

                        removeTrade(position);



#if USE_CMR_DATA
                        PositionInfo trade = new PositionInfo(position.Symbol, position.Interval, -2, position.Date, position.Price);
                        Thread thread = new Thread(new ParameterizedThreadStart(savePositionToCMRData));
                        thread.Start(trade);
#else
                        //ChangePosition(portfolioName, horizon, symbol, 0, date, 0.0);
#endif
                        break;
                    }
                }
            }
        }
        
        public double GetExposure(string portfolioName, string symbol, int direction)
        {
            double exposure = 0;
            lock (_positionLock)
            {
                string network = _portfolio.GetTradeNetwork(portfolioName, TradeHorizon.MidTerm, symbol);
                foreach (Trade position in _positions)
                {
                    if (position.Group == network && position.IsOpen())
                    {
                        if (position.Direction < 0 && direction < 0 || position.Direction > 0 && direction > 0)
                        {
                            //exposure += position.Investment;
                            DateTime tradeDate = position.OpenDateTime;
                            double tradePrice = _barCache.GetClosePrice(position.Ticker, tradeDate);
                            if (!double.IsNaN(tradePrice))
                            {
                                double tradeSize = position.Investment1 / tradePrice;
                                double currentPrice = _barCache.GetLastPrice(PriceType.Close, position.Ticker, "Daily", 0);
                                exposure += tradeSize * currentPrice;
                            }
                        }
                    }
                }
            }
            return exposure;
        }

        public void ChangePosition(string portfolioName, TradeHorizon horizon, string symbol, int direction, string date, double price, string comment)
        {
            string ticker = symbol;

            bool combineOutRightAndHedged = Portfolio.GetLinkAccounts();

            string[] field1 = symbol.Split(Symbol.SpreadCharacter);
            if (field1.Length == 2)
            {
                ticker = field1[0].Trim();
            }
 
#if USE_CMR_DATA
            Thread thread = new Thread(new ParameterizedThreadStart(savePositionToCMRData));
            thread.Start(trade);
#else

            List<string> portfolios = new List<string>();
            if (combineOutRightAndHedged)
            {
                portfolios = _portfolio.GetTradePortfolios(symbol, combineOutRightAndHedged);
            }
            else
            {
                portfolios.Add(portfolioName);
            }

            foreach (string portfolio in portfolios)
            {
                string network = _portfolio.GetTradeNetwork(portfolio, horizon, symbol);

                bool saveToServer = true;
                bool sendToSqPt = portfolioName == "SQ PT";

                if (direction == 0) // exit
                {
                    lock (_positionLock)
                    {
                        Trade trade = findOpenTrade(network, symbol);
                        if (trade != null)
                        {

                            bool ok = true;
                            if (sendToSqPt)
                            {
                                var oldSize = (trade != null) ? trade.Direction : 0;
                                var newSize = 0;

                                var units = Math.Abs(newSize - oldSize);
                                var oldUnits = Math.Abs(oldSize);
                                var newUnits = Math.Abs(newSize);

                                var exitLong = oldSize > 0 && newSize == 0;
                                var exitShort = oldSize < 0 && newSize == 0;

                                var action = "";
                                if (exitLong) action = "exit " + oldUnits + " units of long on " + trade.Ticker;
                                else if (exitShort) action = "exit " + oldUnits + " units of short on " + trade.Ticker;

                                //MessageBoxResult result = MessageBox.Show("Are you sure you want to \n" + action + " ? ", "Confirm trade", MessageBoxButton.YesNo, MessageBoxImage.Question);
                                //ok = (result == MessageBoxResult.Yes);
                            }

                            if (ok)
                            {
                                if (sendToSqPt)
                                {
                                    int position = Math.Sign(trade.Direction) * getSqPtIdea(trade.Id.ThirdPartyId);
                                    if (position != 0)
                                    {
                                        ok = modifySqPtIdea("close", 0, trade.Id.ThirdPartyId, comment);
                                    }
                                }

                                if (ok)
                                {
                                    closeTradeIdea(network, symbol, trade.Id, date, price, saveToServer);
                                }

                                if (!ok)
                                {
                                    //MessageBoxResult result = MessageBox.Show("Trade failed", "Trade failed", MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                            }
                        }
                    }
                }
                else // entry or increase, or decrease
                {
                    if (network.Length > 0)
                    {
                        int size = 0;
                        string hedgeSymbol = _portfolio.GetTradeHedge(portfolio);
                        if (symbol == hedgeSymbol)
                        {
                            size = (int)(direction * Trade.Manager.GetExposure(portfolio, symbol, -direction));
                        }
 
                        else
                        {

                            double balance = _portfolio.GetTradeBalance(portfolio);
                            double percent = _portfolio.GetTradePercent(portfolio);
                            size = direction;
                            if (balance > 0 && percent > 0)
                            {
                                size = (int)(direction * percent / 100 * balance);
                            }
                        }

                        if (size != 0)
                        {
                            Trade trade = findOpenTrade(network, symbol);

                            bool ok = true;
                            if (sendToSqPt)
                            {
                                var oldSize = (trade != null) ? trade.Direction : 0;
                                var newSize = direction + oldSize;

                                var units = Math.Abs(newSize - oldSize);
                                var oldUnits = Math.Abs(oldSize);
                                var newUnits = Math.Abs(newSize);

                                var buy = oldSize == 0 && newSize > 0;
                                var sell = oldSize == 0 && newSize < 0;
                                var exitLong = oldSize > 0 && newSize == 0;
                                var exitShort = oldSize < 0 && newSize == 0;
                                var exit = exitLong || exitShort;
                                var increaseLong = !exit && oldSize > 0 && newSize > oldSize;
                                var decreaseLong = !exit && oldSize > 0 && newSize < oldSize;
                                var increaseShort = !exit && oldSize < 0 && newSize < oldSize;
                                var decreaseShort = !exit && oldSize < 0 && newSize > oldSize;

                                var action = "";
                                if (buy) action = "buy " + units + " units of " + symbol;
                                else if (sell) action = "sell " + units + " units of " + symbol;
                                else if (exitLong) action = "exit " + oldUnits + " units of long on " + symbol;
                                else if (exitShort) action = "exit " + oldUnits + " units of short on " + symbol;
                                else if (increaseLong) action = "increase long to " + newUnits + " units of " + symbol;
                                else if (decreaseLong) action = "decrease long to " + newUnits + " units of " + symbol;
                                else if (increaseShort) action = "increase short to " + newUnits + " units of " + symbol;
                                else if (decreaseShort) action = "decrease short to " + newUnits + " units of " + symbol;

                                //MessageBoxResult result = MessageBox.Show("Are you sure you want \n" + action + " ? ", "Confirm trade", MessageBoxButton.YesNo, MessageBoxImage.Question);
                                //ok = (result == MessageBoxResult.Yes);
                            }

                            if (ok)
                            {
                                var investment = direction;

                                if (trade == null)
                                {
                                    string hedge = _portfolio.GetTradeHedge(portfolio, ticker);
                                    IdeaIdType tradeIdeaId = openTradeIdea(network, ticker, hedge, size, symbol == hedgeSymbol);
                                    trade = new Trade(network, symbol, tradeIdeaId, direction, investment, DateTime.Parse(date), price, DateTime.MaxValue, double.NaN, true);
                                }

                                if (sendToSqPt)
                                {
                                    string id = trade.Id.BloombergIdSpecified ? trade.Id.BloombergId.ToString() : trade.Id.ThirdPartyId;

                                    // assumes either 2 or 4 million investment (position = 0, 1, 2 units       1 unit = 2000000)
                                    int position = Math.Sign(trade.Direction) * getSqPtIdea(id);
                                    if (position == 0)
                                    {
                                        ok = createSqPtIdea(trade, comment);
                                    }
                                    else
                                    {
                                        // buy one unit
                                        if (direction == 1)
                                        {
                                            // -1 => 0
                                            if (position == -1)
                                            {
                                                closeTradeIdea(network, symbol, trade.Id, date, price, saveToServer);
                                                ok = modifySqPtIdea("close", 0, id, comment);
                                                investment = 0;
                                            }
                                            // -2 => -1
                                            else if (position == -2)
                                            {
                                                trade.Direction = -1;
                                                ok = modifySqPtIdea("decrease", 1, id, comment);
                                                investment = -1;
                                            }
                                            // 1 => 2
                                            else if (position == 1)
                                            {
                                                trade.Direction = 2;
                                                ok = modifySqPtIdea("increase", 2, id, comment);
                                                investment = 2;
                                            }
                                        }

                                        // sell one unit
                                        else if (direction == -1)
                                        {
                                            // 1 => 0
                                            if (position == 1)
                                            {
                                                closeTradeIdea(network, symbol, trade.Id, date, price, saveToServer);
                                                ok = modifySqPtIdea("close", 0, id, comment);
                                                investment = 0;
                                            }
                                            // 2 => 1
                                            else if (position == 2)
                                            {
                                                trade.Direction = 1;
                                                ok = modifySqPtIdea("decrease", 1, id, comment);
                                                investment = 1;
                                            }
                                            // -1 -> -2
                                            else if (position == -1)
                                            {
                                                trade.Direction = -2;
                                                ok = modifySqPtIdea("increase", -2, id, comment);
                                                investment = -2;
                                            }
                                        }
                                    }
                                }

                                if (ok)
                                {
                                    lock (_positionLock)
                                    {
                                        string id = trade.Id.BloombergIdSpecified ? trade.Id.BloombergId.ToString() : trade.Id.ThirdPartyId;

                                        // add trade to local data base
                                        _positions.Add(trade);
                                        _positionDataBase[id] = trade;

                                        // add trade to remote database
                                        if (saveToServer)
                                        {
                                            addTrade(trade);
                                        }
                                    }
                                }
                                else
                                {
                                    //MessageBoxResult result = MessageBox.Show("Trade failed", "Trade failed", MessageBoxButton.OK, MessageBoxImage.Error);
                                }

                            }
                        }
                    }
                }
            }
#endif
            lock (_positionLock)
            {
                _positions.Sort();
            }
            update(symbol);
        }

        private Trade findOpenTrade(string network, string symbol)
        {
            Trade output = null;
            lock (_positionLock)
            {
                foreach (Trade position in _positions)
                {
                    if (position.Group == network && position.IsOpen())
                    {
                        string[] field2 = position.Ticker.Split(Symbol.SpreadCharacter);
                        bool match =( position.Ticker == symbol);
                        if (match)
                        {
                            output = position;
                            break;
                        }
                    }
                }
            }
            return output;
        }

        public List<Trade> getPositions(string symbol, string interval)
        {
            List<Trade> positions = new List<Trade>();
            lock (_positionLock)
            {
                List<Trade> list1 = new List<Trade>();
                foreach (Trade position in _positions)
                {
                    string positionInterval = "D";
                    if (Char.IsNumber(positionInterval[0])) positionInterval = "D";

                    if (Math.Abs(position.Direction) != 5 && positionInterval == interval && position.compareSymbol(symbol) == 0)   // not risk lines
                    {
                        positions.Add(position);
                    }
                }
            }
            return positions;
        }

        public List<Trade> getRisks(string symbol, string interval)
        {
            List<Trade> positions = new List<Trade>();
            lock (_positionLock)
            {
                List<Trade> list1 = new List<Trade>();
                foreach (Trade position in _positions)
                {
                    string positionInterval = "D";
                    if (Char.IsNumber(positionInterval[0])) positionInterval = "D";

                    if (Math.Abs(position.Direction) == 5 && positionInterval == interval && position.compareSymbol(symbol) == 0)  // risk lines
                    {
                        positions.Add(position);
                    }
                }
            }
            return positions;
        }

        public int getCurrentPosition(string ticker, string interval)
        {
            int direction = 0;
            List<Trade> positions = new List<Trade>();
            lock (_positionLock)
            {
                List<Trade> list1 = new List<Trade>();
                foreach (Trade position in _positions)
                {
                    if (Math.Abs(position.Direction) != 5 && interval == "D" && position.compareSymbol(ticker) == 0) // not risk lines
                    {
                        positions.Add(position);
                    }
                }
            }
            if (positions.Count > 0)
            {
                direction = (int)positions[positions.Count - 1].Direction;
            }
            return direction;
        }

        public List<Trade> getSymbolListPositions(TradeHorizon tradeHorizon, List<Symbol> symbols)
        {
            List<Trade> positions = new List<Trade>();
            lock (_positionLock)
            {
                foreach (Trade position in _positions)
                {
                    string symbol = getSymbol(position, symbols);
                    if (symbol.Length > 0)
                    {
                        if (isTradeInterval(tradeHorizon, symbol, position.Group))
                        {
                            positions.Add(position);
                        }
                    }
                }
            }
            return positions;
        }

        public DateTime getCurrentTradeDate(string ticker)
        {
            DateTime date;
            if (!_currentTradeStart.TryGetValue(ticker, out date))
            {
                date = DateTime.MaxValue;
            }
            return date;
        }

        private bool isTradeInterval(TradeHorizon tradeHorizon, string symbol, string network)
        {
            bool intervalOk = IsTradeInterval(tradeHorizon, network);
            //bool symbolOk = (intervalOk ? (tradeHorizon == TradeHorizon.ShortTerm ? _portfolio.IsShortTermSymbol(symbol) : _portfolio.IsNotShortTermSymbol(symbol)) : false);
            //return (intervalOk && symbolOk);
            return intervalOk;
        }

        public bool IsTradeInterval(TradeHorizon tradeHorizon, string network)
        {
            bool ok = false;
            if (network.EndsWith("ST") && tradeHorizon == TradeHorizon.ShortTerm) ok = true;
            else if (network.EndsWith("LT") && tradeHorizon == TradeHorizon.LongTerm) ok = true;
            else if (tradeHorizon == TradeHorizon.MidTerm) ok = true;
            return ok;
        }

        public List<Trade> getTrades(TradeHorizon tradeHorizon, List<Symbol> symbols, string date)
        {
            List<Trade> trades = new List<Trade>();

            DateTime dateTime = (date.Length == 0) ? DateTime.MinValue : DateTime.Parse(date);

            lock (_positionLock)
            {
                foreach (Trade position in _positions)
                {
                    if ((date.Length == 0 || position.OpenDateTime.CompareTo(dateTime) >= 0))
                    {
                        string symbol = getSymbol(position, symbols);
                        if (symbol.Length > 0)
                        {
                            if (isTradeInterval(tradeHorizon, symbol, position.Group))
                            {
                                trades.Add(position);
                            }
                        }
                    }
                }
            }
            return trades;
        }

        public List<Trade> getTrades(List<Symbol> symbols, string date)
        {
            List<Trade> trades = new List<Trade>();

            DateTime dateTime = (date.Length == 0) ? DateTime.MinValue : DateTime.Parse(date);

            lock (_positionLock)
            {
                foreach (Trade position in _positions)
                {
                    if (Math.Abs(position.Direction) != 5)  // not risk lines
                    {
                        if ((date.Length == 0 || position.OpenDateTime.CompareTo(dateTime) >= 0))
                        {
                            string symbol = getSymbol(position, symbols);
                            if (symbol.Length > 0)
                            {
                                trades.Add(position);
                            }  
                        }
                    }
                }
            }
            return trades;
        }
        
        public List<Trade> getTradesForSymbol(string portfolioName, TradeHorizon horizon, string symbol)
        {
            List<Trade> trades = new List<Trade>();

            string network = _portfolio.GetTradeNetwork(portfolioName, horizon, symbol);

            lock (_positionLock)
            {
                foreach (Trade position in _positions)
                {
                    if (position.Group == network && position.Ticker == symbol)
                    {
                        trades.Add(position);
                    }
                }
            }
            return trades;
        }

        public DateTime getStartDate(string portfolioName)
        {
            DateTime date = DateTime.Now;
            lock (_account)
            {
                if (_account.ContainsKey(portfolioName))
                {
                    date = _account[portfolioName].StartDate;
                }
            }
            return date;
        }

        public double getStartBalance(string portfolioName)
        {
            double balance = 0;
            lock (_account)
            {
                if (_account.ContainsKey(portfolioName))
                {
                    balance = _account[portfolioName].StartBalance;
                }
            }
            return balance;
        }

        public TradeHorizon GetIntervalTradeHorizon(string interval)
        {
            TradeHorizon horizon = TradeHorizon.MidTerm;
            if (interval == "1Y" || interval == "Y") horizon = TradeHorizon.LongTerm;
            else if (interval == "1S" || interval == "S") horizon = TradeHorizon.ExtraLongTerm;
            else if (interval == "1Q" || interval == "Q") horizon = TradeHorizon.VeryLongTerm;
            else if (interval == "1M" || interval == "M") horizon = TradeHorizon.LongTerm;
            else if (interval == "1W" || interval == "W") horizon = TradeHorizon.MidTerm;
            else horizon = TradeHorizon.ShortTerm;
            return horizon;
        }

        public TradeHorizon GetNetworkTradeHorizon(string network)
        {
            TradeHorizon horizon = TradeHorizon.MidTerm;
            if (network.EndsWith("LT")) horizon = TradeHorizon.LongTerm;
            else if (network.EndsWith("ST")) horizon = TradeHorizon.ShortTerm;
            return horizon;
        }

        public TradeHorizon GetTradeHorizon(string portfolioName)
        {
            TradeHorizon tradeHorizon = TradeHorizon.MidTerm;

            if (portfolioName == "EU IND")
            {
                tradeHorizon = TradeHorizon.LongTerm;
            }
            else if (portfolioName == "N AMERICAN IND")
            {
                tradeHorizon = TradeHorizon.LongTerm;
            }
            else if (portfolioName == "EUROPEAN IND")
            {
                tradeHorizon = TradeHorizon.LongTerm;
            }
            else if (portfolioName == "ASIAN IND")
            {
                tradeHorizon = TradeHorizon.LongTerm;
            }
            else if (portfolioName == "OCEANIA IND")
            {
                tradeHorizon = TradeHorizon.LongTerm;
            }
            else if (portfolioName == "US IND")
            {
                tradeHorizon = TradeHorizon.LongTerm;
            }
            else if (portfolioName == "CA IND")
            {
                tradeHorizon = TradeHorizon.LongTerm;
            }
            else if (portfolioName == "AU IND")
            {
                tradeHorizon = TradeHorizon.LongTerm;
            }
            else if (portfolioName == "HK IND")
            {
                tradeHorizon = TradeHorizon.LongTerm;
            }
            else if (portfolioName == "JP IND")
            {
                tradeHorizon = TradeHorizon.LongTerm;
            }
            else if (portfolioName == "KR IND")
            {
                tradeHorizon = TradeHorizon.LongTerm;
            }
            else if (portfolioName == "TW IND")
            {
                tradeHorizon = TradeHorizon.LongTerm;
            }
            //else if (portfolioName == "CURRENCY")
            //{
            //    tradeHorizon = TradeHorizon.LongTerm;
            //}
            //else if (portfolioName == "GLOBAL 10yr")
            //{
            //    tradeHorizon = TradeHorizon.LongTerm;
            //}
            //else if (portfolioName == "ENERGY/METAL")
            //{
            //    tradeHorizon = TradeHorizon.LongTerm;
            //}
            
            return tradeHorizon;
        }

        public void setStartDateAndBalance(string portfolioName, DateTime date, double balance)
        {
            lock (_account)
            {
                if (_account.ContainsKey(portfolioName))
                {
                    _account[portfolioName].StartDate = date;
                    _account[portfolioName].StartBalance = balance;

                    TradeHorizon tradeHorizon = GetTradeHorizon(portfolioName);
                    _account[portfolioName].Trades = getTrades(tradeHorizon, _portfolio.GetSymbols(portfolioName), date.ToString("yyyy-MM-dd"));
                }
            }

            //calculateTradeSizes(portfolioName);

            onTradeEvent(new TradeEventArgs());
        }

        public List<Trade> getTradesForNetwork(string network)
        {
            List<Trade> trades = new List<Trade>();

            lock (_positionLock)
            {
                foreach (Trade position in _positions)
                {
                    if (position.Group == network)
                    {
                        trades.Add(position);
                    }
                }
            }
            return trades;
        }


        public List<Trade> getTrades(string portfolioName)
        {
            var trades = new List<Trade>();
            string network = _portfolio.GetTradeNetwork(portfolioName);
            lock (_positionLock)
            {
                foreach (Trade position in _positions)
                {
                    if (position.Group == network)
                    {
                        trades.Add(position);
                    }
                }
            }
            //var pcmt = _positions.Where(x => x.Network == "PCM").ToList();
            return trades;
        }

        public Dictionary<string, Symbol> getOpenTradeSymbols(string portfolioName)
        {
            var symbols = new Dictionary<string, Symbol>();

            string network = _portfolio.GetTradeNetwork(portfolioName);
            lock (_positionLock)
            {
                foreach (Trade position in _positions)
                {
                    if (position.Group == network && position.IsOpen())
                    {
                        if (!symbols.ContainsKey(position.Ticker))
                        {
                            Symbol symbol = new Symbol(position.Ticker);
                            symbols[position.Ticker] = symbol;
                        }
                    }
                }
            }
            return symbols;
        }

        public Dictionary<string, Symbol> getOpenTradeSymbolsForNetwork(string network, int side = 0) // side = 0 all open, side = 1 long open, side = - 1 short open
        {
            var symbols = new Dictionary<string, Symbol>();

            lock (_positionLock)
            {
                foreach (Trade position in _positions)
                {
                    if (position.Group == network && position.IsOpen())
                    {
                        if (!symbols.ContainsKey(position.Ticker))
                        {
                            if (side == 0 || (side == 1 && position.Direction > 0 || side == -1 && position.Direction < 0))
                            {
                                Symbol symbol = new Symbol(position.Ticker);
                                symbols[position.Ticker] = symbol;
                            }
                        }
                    }
                }
            }
            return symbols;
        }
        public int getOpenTradeDirectionForNetworkAndSymbol(string network, string symbol)
        {
            int size = 0;
            lock (_positionLock)
            {
                foreach (Trade position in _positions)
                {
                    if (position.Group == network && position.Ticker == symbol && position.IsOpen())
                    {
                        size = (int)position.Direction;
                    }
                }
            }
            return size;
        }
        public double getOpenMarketValue(string portfolioName, TradeHorizon tradeHorizon, string symbol)
        {
            //string[] fields = symbol.Split(' ');
            //symbol = (fields.Length == 3 && fields[2] == "Equity" && (fields[1] == "UW" || fields[1] == "UN")) ? fields[0] + " US " + fields[2] : symbol;

            string network = _portfolio.GetTradeNetwork(portfolioName, tradeHorizon);

            double marketValue = 0;
            lock (_positionLock)
            {
                foreach (Trade position in _positions)
                {
                    if (position.Group == network && position.Ticker == symbol && position.IsOpen())
                    {
                        marketValue = (position.Investment1 != 0) ? Math.Sign(position.Direction) *  Math.Abs(position.Investment1) : Math.Sign(position.Direction);
                        break;
                    }
                }
            }
            return marketValue;
        }

        public DateTime getOpenDate(string portfolioName, TradeHorizon tradeHorizon, string symbol)
        {
            //string[] fields = symbol.Split(' ');
            //symbol = (fields.Length == 3 && fields[2] == "Equity" && (fields[1] == "UW" || fields[1] == "UN")) ? fields[0] + " US " + fields[2] : symbol;

            string network = _portfolio.GetTradeNetwork(portfolioName, tradeHorizon);

            DateTime openDate = new DateTime();
            lock (_positionLock)
            {
                foreach (Trade position in _positions)
                {
                    if (position.Group == network && position.Ticker == symbol && position.IsOpen())
                    {
                        openDate = position.OpenDateTime;
                        break;
                    }
                }
            }
            return openDate;
        }

        public string getOpenTradeUserForNetworkAndSymbol(string network, string symbol)
        {
            string user = "";
            //lock (_positionLock)
            //{
            //    foreach (Trade position in _positions)
            //    {
            //        if (position.Network == network && position.Symbol == symbol && position.IsOpen())
            //        {
            //            user = position.User;
            //        }
            //    }
            //}
            return user;
        }

        public List<Trade> getOpenTrades(string portfolioName)
        {
            List<Trade> trades = new List<Trade>();
            lock (_account)
            {
                if (_account.ContainsKey(portfolioName))
                {
                    foreach (Trade trade in _account[portfolioName].Trades)
                    {
                        if (trade.IsOpen())
                        {
                            trades.Add(trade);
                        }
                    }
                }
            }
            return trades;
        }

        public Dictionary<string, Symbol> getSymbols(string portfolioName, Dictionary<string, Symbol> input, int direction)
        {

            Dictionary<string, Symbol> output = output = (direction == 0) ? new Dictionary<string, Symbol>(input) : new Dictionary<string, Symbol>();

            lock (_account)
            {
                List<string> trades = new List<string>();

                if (_account.ContainsKey(portfolioName))
                {
                    foreach (Trade trade in _account[portfolioName].Trades)
                    {
                        if (trade.IsOpen())
                        {
                            foreach (string ticker in input.Keys)
                            {
                                if (trade.compareSymbol(ticker) == 0)
                                {
                                    if (direction == trade.Direction)
                                    {
                                        output.Add(ticker, input[ticker]);
                                    }
                                    else if (direction == 0)
                                    {
                                        output.Remove(ticker);
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            return output;
        }
        public int getTradeSize(string portfolioName, string symbol, string date)
        {
            int size = 0;

            List<Trade> hedgeTrades = new List<Trade>();

            lock (_account)
            {
                if (_account.ContainsKey(portfolioName))
                {
                    foreach (Trade trade in _account[portfolioName].Trades)
                    {
                        if (trade.compareSymbol("ES1 Index") == 0)
                        {
                            hedgeTrades.Add(trade);
                        }

                        if (trade.compareSymbol(symbol) == 0)
                        {
                            if (date.CompareTo(trade.EntryDate.Substring(0,10)) >= 0 && (trade.IsOpen() || date.CompareTo(trade.ExitDate.Substring(0,10)) <= 0))
                            {
                                size = (int)trade.Direction - size;
                            }
                        }
                    }
                }
            }
            return size;
        }
        public List<Trade> getTrades(string ticker, TradeHorizon tradeHorizon)
        {
            List<Trade> trades = new List<Trade>();

            List<Symbol> symbols = new List<Symbol>();
            symbols.Add(new Symbol(ticker));

            trades = getTrades(tradeHorizon, symbols, "");

            return trades;
        }

        public List<Trade> getTrades(string portfolioName, string symbol)
        {
            List<Trade> trades = new List<Trade>();
            lock (_account)
            {
                if (_account.ContainsKey(portfolioName))
                {
                    foreach (Trade trade in _account[portfolioName].Trades)
                    {
                        if (trade.compareSymbol(symbol) == 0 && (trade.Direction != 3 || trade.Direction != -3 || trade.Direction != 9))
                        {
                            trades.Add(trade);
                        }
                    }
                }
            }
            return trades;
        }

        public double getDividends(string portfolioName, int year1, int month1)
        {
            double amount = 0;
            lock (_account)
            {
                if (_account.ContainsKey(portfolioName))
                {
                    foreach (KeyValuePair<string, double> kvp in _account[portfolioName].Dividends)
                    {
                        string date = kvp.Key;
                        int year2 = int.Parse(date.Substring(0, 4));
                        int month2 = int.Parse(date.Substring(5, 2));
                        if (year1 == year2 && month1 == month2)
                        {
                            amount += kvp.Value;
                        }
                    }
                }
            }
            return amount;
        }

        private string getSymbol(Trade position, List<Symbol> symbols)
        {
            string symbol = "";
            foreach (Symbol symbol1 in symbols)
            {
                if (position.compareSymbol(symbol1.Ticker) == 0)
                {
                    symbol = symbol1.Ticker;
                    break;
                }
            }
            return symbol;
        }

        private bool symbolOk(string ticker)
        {
            bool ok = true;

            List<Symbol> symbols = new List<Symbol>();
            symbols.AddRange(_portfolio.GetSymbols("US 100"));

            if (MainView.EnableUS500)
            {
                symbols.AddRange(_portfolio.GetSymbols("US 500"));
            }
            if (MainView.EnableRickStocks)
            {
                symbols.AddRange(_portfolio.GetSymbols("SP GROUPS"));
            }
            if (MainView.EnableRickStocks)
            {
                symbols.AddRange(_portfolio.GetSymbols("US 100 H"));
            }
            if (MainView.EnableGlobalIndices)
            {
                symbols.AddRange(_portfolio.GetSymbols("GLOBAL INDICES"));
            }

            ok = false;
            foreach (Symbol symbol in symbols)
            {
                if (ticker == symbol.Ticker)
                {
                    ok = true;
                    break;
                }
            }
            return ok;
        }
        public int getTradeOpenDirection(string portfolioName, TradeHorizon tradeHorizon, string ticker)
        {
            int size = 0;

            string[] fields = ticker.Split(Symbol.SpreadCharacter);
            bool spread = (fields.Length == 2);
            if (spread)
            {
                size = getOpenDirection(portfolioName, tradeHorizon, ticker);
                if (size == 0)
                {
                    size = -getOpenDirection(portfolioName, tradeHorizon, fields[1].Trim() + " - " + fields[0].Trim());
                }
            }
            else
            {
                size = getOpenDirection(portfolioName, tradeHorizon, ticker);
            }
            return size;
        }
        private int getOpenDirection(string portfolioName, TradeHorizon tradeHorizon, string ticker)
        {
            int size = 0;

            bool ok = true; // symbolOk(ticker);

            string network = _portfolio.GetTradeNetwork(portfolioName, tradeHorizon, ticker);

            if (ok)
            {
                lock (_positionLock)
                {
                    int begin = 0;
                    int end = _positions.Count;
                    while (end > begin)
                    {
                        int index = (begin + end) / 2;
                        Trade position = _positions[index];
                        if (position.compareSymbol(ticker) >= 0)
                            end = index;
                        else
                            begin = index + 1;
                    }

                    int count = _positions.Count;
                    for (int ii = begin; ii < count; ii++)
                    {
                        Trade position = _positions[ii];
                        if (position.compareSymbol(ticker) == 0)
                        {
                            if (/*network == "" ||*/ network == position.Group)
                            //if (isTradeInterval(tradeHorizon, ticker, position.Interval))
                            {
                                if (position.IsOpen())
                                {
                                    size = (int)position.Direction;
                                }
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
            return size;
        }
        public double getTradeOpenPrice(TradeHorizon tradeHorizon, string symbol)
        {
            double price = 0;
            lock (_positionLock)
            {
                int begin = 0;
                int end = _positions.Count;
                while (end > begin)
                {
                    int index = (begin + end) / 2;
                    Trade position = _positions[index];
                    if (position.compareSymbol(symbol) >= 0)
                        end = index;
                    else
                        begin = index + 1;
                }

                int count = _positions.Count;
                for (int ii = begin; ii < count; ii++)
                {
                    Trade position = _positions[ii];
                    if (position.compareSymbol(symbol) == 0)
                    {
                        if (isTradeInterval(tradeHorizon, symbol, position.Group))
                        {
                            price = position.OpenPrice;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
            return price;
        }

        public DateTime getTradeOpenDate(TradeHorizon tradeHorizon, string symbol, string portfolioName = "")
        {            
            DateTime date = DateTime.MinValue;

            string network = _portfolio.GetTradeNetwork(portfolioName, tradeHorizon);

            lock (_positionLock)
            {
                int begin = 0;
                int end = _positions.Count;
                while (end > begin)
                {
                    int index = (begin + end) / 2;
                    Trade position = _positions[index];
                    if (position.compareSymbol(symbol) >= 0)
                        end = index;
                    else
                        begin = index + 1;
                }

                int count = _positions.Count;
                for (int ii = begin; ii < count; ii++)
                {
                    Trade position = _positions[ii];
                    if (position.compareSymbol(symbol) == 0)
                    {
                        if (position.IsOpen() && (network == "" || position.Group == network) && (tradeHorizon == TradeHorizon.Any || isTradeInterval(tradeHorizon, symbol, position.Group)))
                        {
                            date = position.OpenDateTime;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
            return date;
        }

        public DateTime getTradeCloseDate(TradeHorizon tradeHorizon, string symbol)
        {
            DateTime date = DateTime.MinValue;
            lock (_positionLock)
            {
                int begin = 0;
                int end = _positions.Count;
                while (end > begin)
                {
                    int index = (begin + end) / 2;
                    Trade position = _positions[index];
                    if (position.compareSymbol(symbol) >= 0)
                        end = index;
                    else
                        begin = index + 1;
                }

                int count = _positions.Count;
                for (int ii = begin; ii < count; ii++)
                {
                    Trade position = _positions[ii];
                    if (position.compareSymbol(symbol) == 0)
                    {
                        if (isTradeInterval(tradeHorizon, symbol, position.Group))
                        {
                            date = position.CloseDateTime;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
            return date;
        }

        public int getPositionDirection(TradeHorizon tradeHorizon, string symbol, DateTime date, string interval, out int exit)
        {
            int size = int.MinValue;
            exit = 0;

            //bool ok = symbolOk(symbol);

           // if (ok)
            {
                lock (_positionLock)
                {
                    int begin = 0;
                    int end = _positions.Count;
                    while (end > begin)
                    {
                        int index = (begin + end) / 2;
                        Trade position = _positions[index];
                        if (position.compareSymbol(symbol) >= 0)
                            end = index;
                        else
                            begin = index + 1;
                    }

                    int count = _positions.Count;
                    int previousSize = 0;
                    for (int ii = begin; ii < count; ii++)
                    {
                        Trade position = _positions[ii];
                        if (position.compareSymbol(symbol) == 0)
                        {
                            if (interval == "Trade" && isTradeInterval(tradeHorizon, symbol, position.Group) || interval == "D")
                            {
                                DateTime date1 = position.OpenDateTime;
                                date1 = new DateTime(date1.Year, date1.Month, date1.Day);

                                if (date1.CompareTo(date) > 0)
                                {
                                    break;
                                }

                                size = (int)position.Direction;
                                if (size == 0 && previousSize > 0) exit = 1;
                                if (size == 0 && previousSize < 0) exit = -1;

                                previousSize = size;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
            return size;
        }

        private void savePositionToCMRData(object obj)
        {
#if USE_CMR_SERVER          
            CMRDataChannel cmrData = getDataChannel();

            if (cmrData != null)
            {
                PositionInfo pi = obj as PositionInfo;
                cmrData.SavePositionInfo(pi);
            }
#endif
        }

        private void savePositionsToCMRData()
        {
#if USE_CMR_SERVER
            CMRDataChannel cmrData = getDataChannel();

            if (cmrData != null)
            {
                string msg = cmrData.ClearPositions();

                string text = "";
                int count = 0;

                lock (_positionLock)
                {
                    int count1 = 0;
                    int count2 = 0;
                    foreach (PositionInfo positionInfo in _positions)
                    {
                        count1++;
                        text += positionInfo.ToString() + ";";
                        if (text.Length > 8000)
                        {
                            msg = cmrData.SavePositions(text);
                            int index1 = msg.IndexOf("Count1 = ");
                            int index2 = msg.IndexOf("Count2 = ");
                            if (index1 > 0)
                            {
                                count2 = int.Parse(msg.Substring(index1 + 9, index2 - index1 - 9));
                                count += count2;
                            }

                            text = "";
                            count1 = 0;
                            count2 = 0;
                        }
                    }
                }

                if (text.Length > 0)
                {
                    msg = cmrData.SavePositions(text);
                    int index1 = msg.IndexOf("Count1 = ");
                    int index2 = msg.IndexOf("Count2 = ");
                    if (index1 > 0)
                    {
                        count += int.Parse(msg.Substring(index1 + 9, index2 - index1 - 9));
                    }
                }
            }
#endif
        }

        public void loadCMRPositions()
        {
            clearPositions();

#if USE_CMR_SERVER
            int firstId = -1;

            // code to fetch trades from Trades.cs and balance from Database. 
            string[] rows = Trades._trades.Split('\n');
            foreach (string row in rows)
            {
                firstId = loadPositions(row, false);
            }

            CMRDataChannel cmrData = getDataChannel();

            if (cmrData != null)
            {
                while (true)
                {
                    string text = cmrData.GetPositions(firstId);
                    if (text.Length == 0 || text.Substring(0, 9) == "EXCEPTION")
                    {
                        break;
                    }
                    int nextId = loadPositions(text, false);
                    if (nextId == firstId)
                    {
                        break;
                    }
                    firstId = nextId;
                }
            }
#endif

            lock (_positionLock)
            {
                _positions.Sort();
            }

            MainView.PositionsAvailable = true;

            if (NewPositions != null)
            {
               NewPositions(this, new NewPositionEventArgs(true));
            }

            _update = true;
        }

        public List<string> GetSpreadPositionSymbols()
        {
            List<string> symbols = new List<string>(); 
            lock (_positionLock)
            {
                foreach (Trade info in _positions)
                {
                    if (info.Ticker.Contains(ATMML.Symbol.SpreadCharacter))
                    {
                        string[] fields = info.Ticker.Split(Symbol.SpreadCharacter);
                        if (Char.IsLetter(fields[1].Trim()[0]))
                        {
                            bool found = false;
                            foreach (string symbol in symbols)
                            {
                                if (symbol == info.Ticker)
                                {
                                    found = true;
                                    break;
                                }
                            }
                            if (!found)
                            {
                                symbols.Add(info.Ticker);
                            }
                        }
                    }
                }
            }
            return symbols;
        }

#if USE_CMR_SERVER
        private CMRDataChannel getDataChannel()
        {
            lock (_cmrDataLock)
            {
                for (int attempt = 0; attempt < 4 && _cmrData == null; attempt++)
                {
                    CMRDataChannel cmrData = new CMRDataChannel();
                    if (cmrData.Open())
                    {
                        _cmrData = cmrData;
                    }
                }
            }
            return _cmrData;
        }
#endif

        private bool loadNewPositionsFromCMRData()
        {
            bool newPositions = false;

#if USE_CMR_SERVER
            lock (_newPositions)
            {
                _newPositions.Clear();
            }

            DateTime timeStamp = _timeStamp + new TimeSpan(0, 0, 1);
            //timeStamp -= new TimeSpan(0, 0, 1); // testing - go back one second from latest timestamp to get a least one position

            CMRDataChannel cmrData = getDataChannel();

            if (timeStamp.CompareTo(DateTime.MinValue) != 0)
            {
                string text = "";

                text = (cmrData != null) ? cmrData.GetNewPositions(timeStamp) : "";

                if (text.Length > 0)
                {
                    newPositions = true;
                }

                loadPositions(text, true);

                lock (_positionLock)
                {
                    _positions.Sort();
                }

                lock (_newPositions)
                {
                    _newPositions.Sort();
                }
            }
#endif
            return newPositions;
        }

        private void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            _seconds++;

            if (_seconds >= 60) // save trades every 1 min
            {
                _seconds = 0;
                //savePositionDataBase();
                //refreshPositionInfos();
            }

            if (_seconds == 15)
            {
                if (_positions.Count == 0 || !_prtuLoaded)
                {
                    loadPositionDataBase();
                }
            }

            if (_portfolio.GetSymbolCount() == 0)
            {
                loadPortfolio();
            }

            if (_update)
            {
                _update = false;
                update("");
                onTradeEvent(new TradeEventArgs());
            }

            bool newPositions = false;

            if (_firstTimer)
            {
                _firstTimer = false;
            }
            else if (DateTime.Now - _pollForNewPositions > new TimeSpan(0, 1, 0))
            {
                _pollForNewPositions = DateTime.Now;
                //newPositions = loadNewPositionsFromCMRData();
            }

            if (newPositions)
            {
                if (NewPositions != null)
                {
                    NewPositions(this, new NewPositionEventArgs(true));
                }
            }
        }
    }

    internal class SecurityType
    {
        internal object Identifier;
    }

    public class Trade : IComparable<Trade>
    {
        string _group = "";
        string _ticker = "";
        IdeaIdType _id = null;
        int _direction = 0;
        DateTime _openDateTime;
        DateTime _closeDateTime;
        double _openPrice = double.NaN;
        double _closePrice = double.NaN;
		double _investment1 = double.NaN; // # of shares at start of bar
		double _investment2 = double.NaN; // # of shares at end of bar
		double _lastPrice = double.NaN;
        bool _open = false;
        int _maxProfitIndex = 0;
        double _price = double.NaN;
        double _pnl = double.NaN;
		double _units = double.NaN;
		string _user = "";
		Dictionary<DateTime, double> _size = new Dictionary<DateTime, double>();
		Dictionary<DateTime, double> _shares = new Dictionary<DateTime, double>();
		Dictionary<DateTime, double> _scores = new Dictionary<DateTime, double>();
		Dictionary<DateTime, double> _avgPrice = new Dictionary<DateTime, double>();
		Dictionary<DateTime, TradeType> _tradeTypes = new Dictionary<DateTime, TradeType>();
		Dictionary<DateTime, (double commission, double impact)> _executionCosts = new Dictionary<DateTime, (double commission, double impact)>();
		Dictionary<DateTime, double> _close = new Dictionary<DateTime, double>();
		Dictionary<DateTime, double> _atrx = new Dictionary<DateTime, double>();
		double _closedProfit = 0;

		private static TradeManager _tradeManager = null;
        private static Object _lock = new Object();

        public static TradeManager Manager
        {
            get
            {
                lock (_lock)
                {
                    if (_tradeManager == null)
                    {
                        _tradeManager = new TradeManager();
                    }
                    return _tradeManager;
                }
            }
        }

        public Trade()
        {
            _open = true;
        }
 
        public Trade(string group, string ticker, IdeaIdType id, int direction, double investment2, DateTime openDateTime, double openPrice, DateTime closeDateTime, double closePrice, bool open, int maxProfitIndex = 0)
        {
            _group = group;
            _ticker = ticker;
            _id = id;
            _direction = direction;
			_investment1 = 0;
			_investment2 = investment2;
			_openDateTime = openDateTime;
            _openPrice = openPrice;
            _closeDateTime = closeDateTime;
            _closePrice = closePrice;
            _open = open;
            _maxProfitIndex = maxProfitIndex;
        }

        public double Cost { get; set; } = 0;

        public string Sector { get; set; } = "";

        public bool IsOpen()
        {
            return _open || _closeDateTime == default(DateTime) || _closeDateTime.Year > 3000 || double.IsNaN(_closePrice);
        }

        public void SetOpen(bool input)
        {
            _open = input;
        }

		public Dictionary<DateTime, double> Shares
		{
			get
			{
				return _shares;
			}
		}

		public Dictionary<DateTime, double> Scores
		{
			get
			{
				return _scores;
			}
		}


		public Dictionary<DateTime, double> AvgPrice
		{
			get
			{
				return _avgPrice;
			}
		}
		public Dictionary<DateTime, TradeType> TradeTypes
		{
			get
			{
				return _tradeTypes;
			}
		}


		public Dictionary<DateTime, (double commission, double impact)> ExecutionCosts
		{
			get
			{
				return _executionCosts;
			}
		}

		public Dictionary<DateTime, double> Closes
		{
			get
			{
				return _close;
			}
		}
		public Dictionary<DateTime, double> ATRX
		{
			get
			{
				return _atrx;
			}
		}


		public string Serialize()
        {
            var sb = new StringBuilder();
            sb.Append(_group);
            sb.Append(",");
            sb.Append(_ticker);
            sb.Append(",");
            sb.Append(_id.BloombergIdSpecified.ToString());
            sb.Append(",");
            sb.Append(_id.BloombergId.ToString());
            sb.Append(",");
            sb.Append(_id.ThirdPartyId);
            sb.Append(",");
            sb.Append(_direction.ToString());
            sb.Append(",");
            sb.Append(_openDateTime);
            sb.Append(",");
            sb.Append(_openPrice.ToString());
            sb.Append(",");
            sb.Append(_closeDateTime);
            sb.Append(",");
            sb.Append(_closePrice.ToString());
            sb.Append(",");
            sb.Append(_investment1.ToString());
            sb.Append(",");
            sb.Append(_user.ToString());
            sb.Append(",");
            sb.Append(_price.ToString());
            sb.Append(",");
            sb.Append(_pnl.ToString());
			sb.Append(",");
			_size.ToList().ForEach(x => sb.Append(x.Key.ToString("yyyyMMddHHmm") + ":" + x.Value + ";"));
			sb.Append(",");
			_shares.ToList().ForEach(x => sb.Append(x.Key.ToString("yyyyMMddHHmm") + ":" + x.Value + ";"));
			sb.Append(",");
			_avgPrice.ToList().ForEach(x => sb.Append(x.Key.ToString("yyyyMMddHHmm") + ":" + x.Value + ";"));
			sb.Append(",");
			sb.Append(_closedProfit.ToString());
			sb.Append(",");
			_tradeTypes.ToList().ForEach(x => sb.Append(x.Key.ToString("yyyyMMddHHmm") + ":" + x.Value + ";"));
			sb.Append(",");
			_close.ToList().ForEach(x => sb.Append(x.Key.ToString("yyyyMMddHHmm") + ":" + x.Value + ";"));
			sb.Append(",");
			_atrx.ToList().ForEach(x => sb.Append(x.Key.ToString("yyyyMMddHHmm") + ":" + x.Value + ";"));
			sb.Append(",");
            sb.Append(Cost.ToString());
			sb.Append(",");
			sb.Append(Sector);
			return sb.ToString();
        }

        public void Deserialize(string text)
        {
            string[] fields = text.Split(',');
            if (fields.Length >= 15)
            {
                _id = new IdeaIdType();

                if (fields[9] == "NAN") fields[9] = "NaN";

                var index = 0;
                _group = fields[index++];
                _ticker = fields[index++];
                _id.BloombergIdSpecified = (fields[index].Length > 0) ? bool.Parse(fields[index++]) : false;
                _id.BloombergId = (fields[index].Length > 0) ? int.Parse(fields[index++]) : 0;
                _id.ThirdPartyId = (fields[index].Length > 0) ? fields[index++] : Guid.NewGuid().ToString();
                _direction = int.Parse(fields[index++]);
                _openDateTime = DateTime.Parse(fields[index++]);
                _openPrice = double.Parse(fields[index++]);
                _closeDateTime = DateTime.Parse(fields[index++]);
                _closePrice = double.Parse(fields[index++]);
                _investment1 = (fields[index].Length > 0) ? double.Parse(fields[index++]) : 0.0;
                _user = fields[index++];
                _price = double.Parse(fields[index++]);
                _pnl = double.Parse(fields[index++]);

                var f = fields[index++].Split(';');
                f.ToList().ForEach(x =>
                {
                    var kv = x.Split(':');
                    if (kv.Length == 2)
                    {
                        DateTime dateTime = DateTime.ParseExact(kv[0], "yyyyMMddHHmm", null);
                        double size = double.Parse(kv[1]);
                        _size.Add(dateTime, size);
                    }
                });

                if (index < fields.Length)
                {
                    var f2 = fields[index++].Split(';');
                    f2.ToList().ForEach(x =>
                    {
                        var kv = x.Split(':');
                        if (kv.Length == 2)
                        {
                            DateTime dateTime = DateTime.ParseExact(kv[0], "yyyyMMddHHmm", null);
                            double size = double.Parse(kv[1]);
                            _shares.Add(dateTime, size);
                        }
                    });
                }


				if (index < fields.Length)
				{
					var f2 = fields[index++].Split(';');
					f2.ToList().ForEach(x =>
					{
						var kv = x.Split(':');
						if (kv.Length == 2)
						{
							DateTime dateTime = DateTime.ParseExact(kv[0], "yyyyMMddHHmm", null);
							double size = double.Parse(kv[1]);
							_avgPrice.Add(dateTime, size);
						}
					});
				}
				_closedProfit = index < fields.Length ? double.Parse(fields[index++]) : 0;

				if (index < fields.Length)
				{
					var f2 = fields[index++].Split(';');
					f2.ToList().ForEach(x =>
					{
						var kv = x.Split(':');
						if (kv.Length == 2)
						{
							DateTime dateTime = DateTime.ParseExact(kv[0], "yyyyMMddHHmm", null);
                            if (kv[1] == "Exhuastion") kv[1] = "Exhaustion";
							var tradeType = (TradeType)Enum.Parse(typeof(TradeType),kv[1]);
							_tradeTypes.Add(dateTime, tradeType);
						}
					});
				}

				if (index < fields.Length)
				{
					var f2 = fields[index++].Split(';');
					f2.ToList().ForEach(x =>
					{
						var kv = x.Split(':');
						if (kv.Length == 2)
						{
							DateTime dateTime = DateTime.ParseExact(kv[0], "yyyyMMddHHmm", null);
							double value = double.Parse(kv[1]);
							_close.Add(dateTime, value);
						}
					});
				}

				if (index < fields.Length)
				{
					var f2 = fields[index++].Split(';');
					f2.ToList().ForEach(x =>
					{
						var kv = x.Split(':');
						if (kv.Length == 2)
						{
							DateTime dateTime = DateTime.ParseExact(kv[0], "yyyyMMddHHmm", null);
							double value = double.Parse(kv[1]);
							_atrx.Add(dateTime, value);
						}
					});
				}

                if (index < fields.Length)
                {
                    Cost = double.Parse(fields[index++]);
                }


				if (index < fields.Length)
				{
					Sector = fields[index++];
				}

				_open = false;
                _open = IsOpen();
            }
        }

        public int CompareTo(Trade other)
        {
            int val = compareSymbol(other._ticker);
            if (val != 0) return val;
            val = _openDateTime.CompareTo(other._openDateTime.Date);
            if (val != 0) return val;
            return 0;
        }

        // for excel files
        public override string ToString()
        {
            return
                     Group + "," +
                     Ticker + "," +
                     Direction.ToString() + "," +
                     _openDateTime.ToString() + "," +
                     ((double.IsNaN(_openPrice)) ? "" : _openPrice.ToString()) + "," +
                     _closeDateTime.ToString() + "," +
                     ((double.IsNaN(_closePrice)) ? "" : _closePrice.ToString()) + "," +
                     _investment1.ToString() + "," + _open;
        }

        public int compareSymbol(string symbol2)
        {
            string symbol1 = _ticker;
            string[] sym1 = symbol1.Split(' ');
            if (sym1.Length == 3)
            {
                if (sym1[1] == "US" || sym1[1] == "UW" || sym1[1] == "UN" || sym1[1] == "UQ")
                {
                    symbol1 = sym1[0] + sym1[2];
                }
            }

            string[] sym2 = symbol2.Split(' ');
            if (sym2.Length == 3)
            {
                if (sym2[1] == "US" || sym2[1] == "UW" || sym2[1] == "UN" || sym2[1] == "UQ")
                {
                    symbol2 = sym2[0] + sym2[2];
                }
            }

            return string.Compare(symbol1, symbol2, StringComparison.OrdinalIgnoreCase);
        }

        public string Group { get { return _group; } set { _group = value; } }
        public string Ticker { get { return _ticker; } set { _ticker = value; } }
        public IdeaIdType Id { get { return _id; } set { _id = value; } }
        public int Direction { get { return _direction; } set { _direction = value; } }
        public DateTime OpenDateTime { get { return _openDateTime; } set { _openDateTime = value; } }
        public double OpenPrice { get { return _openPrice; } set { _openPrice = value; } }
        public DateTime CloseDateTime { get { return _closeDateTime; } set { _closeDateTime = value; } }
        public double ClosePrice { get { return _closePrice; } set { _closePrice = value; } }
        public string EntryDate { get { return _openDateTime.ToString(); } }
        public double EntryPrice { get { return _openPrice; } set { _openPrice = value; } }
        public string ExitDate { get { return _closeDateTime.ToString(); } }
        public double ExitPrice { get { return _closePrice; } set { _closePrice = value; } }
		public double Units { get { return _units; } set { _units= value; } }
		public double Investment1
		{
			get
			{
				return _investment1;
			}

			set
			{
				_investment1 = value;
			}
		}
		public double Investment2
		{
			get
			{
				return _investment2;
			}

			set
			{
				_investment2 = value;
			}
		}
		public double Profit { get; set; }

		public double Price { get { return _price; } set { _price = value; } }
        public double PnL { get { return _pnl; } set { _pnl = value; } }
        public double LastPrice { get { return _lastPrice; } set { _lastPrice = value; } }
        public int MaxProfitIndex { get { return _maxProfitIndex; } }
    }


    public class PRTUTrade
    {
        public string Symbol { get; set; }
        public string Position { get; set; }
        public string MarketValue { get; set; }
        public string EntryDate { get; set; }
    }

      public class AccountInfo
    {
        DateTime _startDate = DateTime.Now;
        double _startBalance = 0;
        List<Trade> _trades = new List<Trade>();
        Dictionary<string, double> _dividends = new Dictionary<string, double>();

        public DateTime StartDate
        {
            get { return _startDate; }
            set { _startDate = value; }
        }

        public double StartBalance
        {
            get { return _startBalance; }
            set { _startBalance = value; }
        }

        public List<Trade> Trades
        {
            get { return _trades; }
            set { _trades = value; }
        }

        public Dictionary<string, double> Dividends
        {
            get { return _dividends; }
            set { _dividends = value; }
        }
    }
}

