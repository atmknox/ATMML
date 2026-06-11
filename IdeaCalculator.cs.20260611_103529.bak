//using System.Management.Automation;
//using Microsoft.CodeAnalysis;
//using Microsoft.ML.Probabilistic.Collections;
using BetaNeutralRiskEngine;
using ExecutionCosts;
using HedgeFundReporting;
using MathNet.Numerics.Integration;
using Microsoft.ML.Trainers.FastTree;
using OPTANO.Modeling.Common;
using OPTANO.Modeling.Optimization.Operators;
using OPTANO.Modeling.Optimization.Operators.Interfaces;
using Python.Runtime;
using RiskEngine2;
using RiskEngine3;
using RiskEngineMN3;
using RiskEngineMNParity;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Timers;
using System.Windows.Input;
using static ATMML.IdeaCalculator;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static TorchSharp.torch.backends;
using static TorchSharp.torch.distributions.constraints;

namespace ATMML
{
	public class IdeaCalculator
	{
		Model _model = null;
		Portfolio _portfolio1 = null;
		Portfolio _portfolio2 = null;
		BarCache _barCache = null;
		int _indexBarRequestCount = 0;
		List<string> _indexSymbols = new List<string>();
		List<string> _referenceSymbols = new List<string>();
		Dictionary<string, object> _referenceData = new Dictionary<string, object>();
		int _totalBarRequest = 0;
		List<string> _barRequests = new List<string>();
		ProgressState _progressState = ProgressState.Complete;
		private bool _runCancelled = false; // set true on user cancel from checksum dialog
		private bool _tradeSetRebuilt = false; // set true when RebuildTradeSetLockFromCSV runs this session
		int _progressTotalNumber = 0;
		int _progressCompletedNumber = 0;
		DateTime _progressTime = DateTime.UtcNow;
		DateTime _barReceivedTime = DateTime.UtcNow;
		bool _barCollectionTimedOut = false; // true when bar collection timed out with 0 bars received
		bool _waitForBars = false;
		bool _waitForFundamentals = false;
		bool _waitForFilters = false;
		bool _stop = false;
		int _reportCount = 0;
		List<string> _refSyms = new List<string>();
		DateTime _fundamentalResponseTime = DateTime.UtcNow;
		List<string> _fundamentalSymbols = new List<string>();
		Scan _scan = null;
		ScanThread _scanThread = null;
		private List<ScanSummary> _scanSummary = null;
		string _interval = "Weekly";
		Dictionary<string, Series> _factorInputs = new Dictionary<string, Series>();
		private Dictionary<string, Dictionary<DateTime, double>> _factorScores = new Dictionary<string, Dictionary<DateTime, double>>();
		private Dictionary<string, Dictionary<DateTime, int>> _predictionData = new Dictionary<string, Dictionary<DateTime, int>>();
		Dictionary<string, Dictionary<string, Dictionary<string, double>>> _scoreCache = new Dictionary<string, Dictionary<string, Dictionary<string, double>>>();
		private Dictionary<string, Dictionary<string, double>> _marketCaps = new Dictionary<string, Dictionary<string, double>>();
		private List<(DateTime Date, double NetBeta, int LongCount, int ShortCount)> _preRebalanceBetaLog = new();

		static Dictionary<string, IdeaCalculator> _calculators = new Dictionary<string, IdeaCalculator>();

		private System.Timers.Timer _timer;
		public event ProgressEventHandler ProgressEvent;

		List<TickerInfo> _tickerInfos;

		public static IdeaCalculator GetIdeaCalculator(Model model)
		{
			IdeaCalculator output = null;
			if (model != null)
			{
				if (_calculators.ContainsKey(model.Name))
				{
					output = _calculators[model.Name];
					output._model = model;
				}
				else
				{
					output = new IdeaCalculator(model);
					_calculators[model.Name] = output;
					AssistantService.Event += output.AssistantService_Event;
					Runtime.PythonDLL = @"C:\Users\Admin\AppData\Local\Programs\Python\Python313\python313.dll";
				}
			}
			return output;
		}

		private void AssistantService_Event(object sender, AssistantEventArgs e)
		{
		}

		private IdeaCalculator(Model model)
		{
			_model = model;
			_portfolio1 = new Portfolio(100);
			_portfolio2 = new Portfolio(101);
			_barCache = new BarCache(barChanged);
			_portfolio1.PortfolioChanged += new PortfolioChangedEventHandler(portfolio1Changed);
			_portfolio2.PortfolioChanged += new PortfolioChangedEventHandler(portfolio2Changed);
			_tickerInfos = ReadTickerInfo(MainView.GetDataFolder() + @"\_StartEndDateSP500.csv");
		}

		public static List<TickerInfo> ReadTickerInfo(string filePath)
		{
			var result = new List<TickerInfo>();

			if (File.Exists(filePath))
			{

				foreach (var line in File.ReadLines(filePath))
				{
					if (string.IsNullOrWhiteSpace(line))
						continue;

					// Supports tab-separated or comma-separated
					var fields = line.Split(new[] { '\t', ',' }, StringSplitOptions.None);

					if (fields.Length < 5)
						continue; // or throw if you want strict validation

					var ticker = fields[0].Trim().Trim('\t', ' ', '\r', '\n', (char)0xFEFF);
					var startDateStr = fields[1].Trim();
					var endDateStr = fields[2].Trim();
					var sectorTicker = fields[4].Trim();

					if (!DateTime.TryParse(startDateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var startDate))
						throw new FormatException($"Invalid StartDate: {startDateStr}");

					if (!DateTime.TryParse(endDateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var endDate))
					{
						if (endDateStr == "") endDate = DateTime.MaxValue;
						else throw new FormatException($"Invalid EndDate: {endDateStr}");
					}

					result.Add(new TickerInfo
					{
						Ticker = ticker,
						StartDate = startDate,
						EndDate = endDate,
						SectorTicker = sectorTicker
					});
				}
			}

			return result;
		}

		public string ModelName { get { return _model.Name; } }

		Thread _thread = null;

		public void Run()
		{
			if (_model != null)
			{
				_stop = false;
				_barReceivedTime = DateTime.UtcNow;
				_barCollectionTimedOut = false;
				_progressState = ProgressState.CollectingData;
				sendProgressEvent();

				_timer = new System.Timers.Timer(1000);
				_timer.Elapsed += new ElapsedEventHandler(timerEvent);
				_timer.AutoReset = true;
				_timer.Start();

				_thread = new Thread(new ThreadStart(run));
				_thread.Start();
			}
		}

		public void Stop()
		{
			_stop = true;

			if (_timer != null)
			{
				_timer.Stop();
			}

			if (_thread != null)
			{
				_thread.Join(20000);
			}

			_progressTotalNumber = 0;
			_progressCompletedNumber = 0;
			_progressState = ProgressState.Complete;
			sendProgressEvent();
		}

		Dictionary<string, Tuple<Series, Series>> _strategyData = null;


		public string GetLowestInterval()
		{
			var interval = "Quarterly";

			//         if (_model.UseFilters)
			//         {
			//             interval = "Monthly";
			//         }
			if (_model.UseRanking)
			{
				interval = getLowerInterval(interval, _model.RankingInterval);
			}
			//         if (_model.Groups[0].UseATMStrategies)
			//         {
			//             interval = getLowerInterval(interval, _model.Groups[0].AlignmentInterval);
			//         }
			//         if (_model.UseML)
			//         {
			//             interval = getLowerInterval(interval, _model.ATMMLInterval);
			//         }
			//var enableATMAnaysis = _model.Groups[0].UseNewTrend || _model.Groups[0].UsePressure || _model.Groups[0].UseAdd || _model.UseFTEntry ||
			//             _model.Groups[0].UseMTTSB || _model.Groups[0].UseMTST || _model.Groups[0].UseMTSC || _model.Groups[0].UseFtFt || _model.Groups[0].UseSectorFT;
			//         if (enableATMAnaysis)
			//         {
			//	interval = getLowerInterval(interval, _model.ATMAnalysisInterval);
			//}

			interval = getLowerInterval(interval, _model.ATMAnalysisInterval);
			//interval = getLowerInterval(interval, _model.RiskInterval);

			return interval;
		}

		private void run()
		{
			//Debug.WriteLine($"[IdeaCalculator] === RUN STARTED ===");
			//Debug.WriteLine($"[IdeaCalculator] === RUN STARTED ===");
			//Debug.WriteLine($"[VERSION] IdeaCalculator build: 2026-03-05-A");
			//Debug.WriteLine($"[IdeaCalculator] Model: {_model?.Name}");
			//Debug.WriteLine($"[IdeaCalculator] Universe: {_model?.Universe}");
			//Debug.WriteLine($"[IdeaCalculator] Symbols count: {_model?.Symbols?.Count ?? 0}");

			if (_model?.Symbols?.Count > 0)
			{
				//Debug.WriteLine($"[IdeaCalculator] First 10 symbols:");
				foreach (var s in _model.Symbols.Take(10))
				{
					//Debug.WriteLine($"[IdeaCalculator]   - {s.Ticker}");
				}
			}
			else
			{
				//Debug.WriteLine($"[IdeaCalculator] WARNING: No symbols in model!");
			}

			_waitForBars = true;

			clearData("scores", _model);
			clearData("predictions", _model);
			clearData("weights", _model);
			clearData("shares", _model);
			MainView.DeleteUserData(@"portfolios\trades\" + _model.Name);
			// NOTE: Do NOT delete portfolios\[ModelName] folder — it contains LockedNav
			// which must survive across runs for locked trade replay to work.
			// MainView.DeleteUserData(@"portfolios" + _model.Name); // DISABLED
			// For live portfolios: do NOT delete NAV/times data before the run.
			// The run will overwrite these at the end if successful.
			// Deleting first means a failed run leaves the portfolio with NO historical data.
			if (!_model.IsLiveMode)
			{
				MainView.DeleteUserData(_model.Name + " PortfolioTimes");
				MainView.DeleteUserData(_model.Name + " PortfolioValues");
				MainView.DeleteUserData(_model.Name + " PortfolioMonthTimes");
				MainView.DeleteUserData(_model.Name + " PortfolioMonthValues");
			}

			var interval = GetLowestInterval();

			requestReferenceData(); // get relative indexes of symbols in model

			if (_model.UseFilters)
			{
				_waitForFilters = true;
				string filter = _model.Filter;

				string conditions = "";
				var items = filter.Split('\u0001');
				for (int ii = 0; ii < items.Length; ii++)
				{
					conditions += items[ii] + "\u0002" + interval;
					if (ii < items.Length - 1) conditions += "\u0001";
				}

				_scanThread = new ScanThread();

				var portfolio = _model.Universe;
				if (_model.Universe == "Custom")
				{
					portfolio = "\u0008";
					foreach (var symbol in _model.Symbols)
					{
						portfolio += symbol.Ticker + "\t";
					}
				}

				_scan = new Scan(_model.Name, portfolio, conditions, _scanThread, int.MaxValue, BarServer.MaxBarCount);
				_scan.ScanComplete += _scan_ScanComplete;
				_scan.Run();
			}

			if (_model.UseRanking)
			{
				// Try loading cached fundamentals first
				var cacheDate = _model.UseCurrentDate ? DateTime.Today : _model.DataRange.Time2.Date;
				if (FundamentalCache.Exists(cacheDate))
				{
					//Trace.WriteLine($"[FundamentalCache] Loading cached fundamentals for {cacheDate:yyyy-MM-dd}");
					FundamentalCache.Load(cacheDate, _model.Symbols);
				}
				else
				{
					_waitForFundamentals = true;
					requestReportDates();
				}
			}

			waitForDataComplete();

			// Cache fundamentals for reproducibility — save regardless of UseRanking
			// so Timing can access MarketCap, Beta etc. in live mode
			if (!_stop)
			{
				var cacheDate = _model.UseCurrentDate ? DateTime.Today : _model.DataRange.Time2.Date;
				if (!FundamentalCache.Exists(cacheDate))
				{
					FundamentalCache.Save(cacheDate, _model.Symbols);
				}
			}

			//testPython();
			//AssistantService.Request("What should the share sizes be if we go Long AAPL, GOOG and go short INTC, AMD?");

			//testRiskEngine2();

			if (!_stop)
			{

				// start calculating...

				// Force Collect to 100% before transitioning so UI shows completion
				if (_progressTotalNumber > 0) _progressCompletedNumber = _progressTotalNumber;
				sendProgressEvent();

				_progressState = ProgressState.ProcessingData;
				_progressCompletedNumber = 0;
				_progressTotalNumber = 0;
				sendProgressEvent();

				List<PortfolioMember> portfolio = createPortfolio(interval, true);
				List<PortfolioMember> hedgePortfolio = createPortfolio(interval, false);

				// 0 means no trade, 1 means long trade, 2 means short trade, 3 means both long and short trade

				// ADD DEBUG CODE HERE:
				//Debug.WriteLine($"[IdeaCalculator] Portfolio created: {portfolio?.Count ?? 0} members");
				//Debug.WriteLine($"[IdeaCalculator] Hedge Portfolio created: {hedgePortfolio?.Count ?? 0} members");

				if (portfolio?.Count > 0)
				{
					//Debug.WriteLine($"[IdeaCalculator] First 5 portfolio members:");
					foreach (var p in portfolio.Take(5))
					{
						//Debug.WriteLine($"[IdeaCalculator]   - {p.Ticker}");
					}
				}

				// 0 means no trade, 1 means long trade, 2 means short trade, 3 means both long and short trade

				if (_model.UseFilters && _model.Filter.Length > 0)
				{
					var filterData = getFilterData(portfolio, interval);
					portfolio = filterPortfolio(-1, portfolio, filterData);
				}

				if (_model.UseRanking)
				{
					var rankingData = getRankingData(portfolio, interval);
					portfolio = filterPortfolio(-1, portfolio, rankingData);
				}

				if (_model.UseML)
				{
					var preditionData = mlPredict(portfolio, interval);
					portfolio = filterPortfolio(-1, portfolio, preditionData);
				}

				var groupCount = _model.Groups.Count;
				for (var group = 0; group < groupCount; group++)
				{
					if (_model.Groups[group].UseATMStrategies)
					{
						var strategyData = getStrategyData(group, portfolio, interval);
						portfolio = filterPortfolio(group, portfolio, strategyData);
					}

					//if (_model.Groups[group].UseMT)
					//{
					//    if (_model.Groups[0].MTNT != 0 || _model.Groups[0].MTP != 0 || _model.Groups[0].MTFT != 0)
					//    {
					//        var mtFilterData = getMTFilterData(group, portfolio, false, interval);
					//        portfolio = filterPortfolio(group, portfolio, mtFilterData);
					//        if (_model.Groups[0].MTNT == 3 || _model.Groups[0].MTP == 3 || _model.Groups[0].MTFT == 3)
					//        {
					//            var mtSectorFilterData = getMTFilterData(group, portfolio, true, interval);
					//            portfolio = filterPortfolio(group, portfolio, mtSectorFilterData);
					//        }
					//    }
					//}

					//if (_model.Groups[group].UseST)
					//{
					//    var stFilterData = getSTFilterData(group, portfolio, interval);
					//    portfolio = filterPortfolio(group, portfolio, stFilterData);
					//}

					var filterData = getATMConditionData(group, portfolio, interval);
					portfolio = filterPortfolio(group, portfolio, filterData);

					var enableATMAnaysis = _model.Groups[group].UseNewTrend || _model.Groups[group].UsePressure || _model.Groups[group].UseAdd || _model.UseFTEntry;
					if (enableATMAnaysis)
					{
						var analysisData = getATMAnalysis(group, portfolio, _model.DataRange.Time1, interval);
						portfolio = filterPortfolio(group, portfolio, analysisData);
					}

					if (!enableATMAnaysis && _model.Groups[group].UseConviction)
					{
						portfolio = getConviction(group, portfolio, interval, _model.Groups[group].ConvictionPercent);
					}

					// hedge filtering
					//if (_model.Groups[group].BetaHedgeEnable)
					//               {
					//	var hedgeFilterData = getATMHedgeConditionData(hedgePortfolio, interval); // todo
					//	hedgePortfolio = filterPortfolio(hedgePortfolio, filterData);
					//}
				}

				//testPython2(_model.DataRange.Time2, 10000000, portfolio);
				//testPython();
				//testQuboSolver();

				createTrades(portfolio);
				//savePortfolio(portfolio);

				if (_runCancelled)
				{
					//Debug.WriteLine("[Checksum] Run was cancelled — skipping Finished state.");
					_runCancelled = false;
					return; // skip Finished/Complete — Portfolio_Builder gets no update signal
				}

				// Force previous state to 100% before transitioning to Finished
				if (_progressTotalNumber > 0) _progressCompletedNumber = _progressTotalNumber;
				sendProgressEvent();

				_progressState = ProgressState.Finished;
				_progressCompletedNumber = 0;
				_progressTotalNumber = 0;
				sendProgressEvent();

				//DateTime date1 = _model.DataRange.Time1; // getStartDate(interval, lowestInterval); // mlPredict
				//DateTime date2 = _model.UseCurrentDate ? DateTime.MaxValue : _model.DataRange.Time2; // DateTime.MaxValue;
				//var times = _barCache.GetTimes(_model.Benchmark, interval, 0);
				//var portfolioTimes = times.Where(x => x >= date1 && x <= date2).ToList();
				//saveList<DateTime>(_model.Name + " PortfolioTimes", portfolioTimes);

			}
			_progressState = ProgressState.Complete;
			sendProgressEvent();

		}

		List<PortfolioMember> getATMHedgeConditionData(List<PortfolioMember> input, string lowestInterval)
		{
			var output = new List<PortfolioMember>();

			for (var ii = 0; ii < input.Count; ii++)
			{
				var member = PortfolioMember.Clone(input[ii]);
				output.Add(member);
			}

			string stInterval = lowestInterval;
			string mtInterval = Study.getForecastInterval(stInterval, 1);
			string ltInterval = Study.getForecastInterval(stInterval, 2);
			string[] intervals = { stInterval, mtInterval, ltInterval };

			foreach (var member in output)
			{
				var ticker = member.Ticker;

				var group = _model.Groups.Find(g => g.Id == member.GroupId);
				var side = group.Direction;
				var conditions = group.ATMConditionsHedge;

				Dictionary<string, List<DateTime>> times = new Dictionary<string, List<DateTime>>();
				Dictionary<string, Series[]> bars = new Dictionary<string, Series[]>();
				PortfolioMember filter = member;

				foreach (string interval in intervals)
				{
					times[interval] = _barCache.GetTimes(ticker, interval, 0);
					bars[interval] = _barCache.GetSeries(ticker, interval, new string[] { "Open", "High", "Low", "Close" }, 0);
				}


				var barCount = times[stInterval].Count;
				Series upFilter = new Series(barCount, 1);
				Series dnFilter = new Series(barCount, 1);

				for (int jj = 0; jj < conditions.Count; jj++)
				{
					if (conditions[jj].Length > 0)
					{
						var items = conditions[jj].Split('\u0002');
						var upCondition = items[0].Trim();
						var dnCondition = Conditions.GetReverseCondition(upCondition);
						var term = items[1].Trim();

						Series upSig;
						lock (_referenceData)
						{
							upSig = Conditions.Calculate(upCondition, ticker, intervals, barCount, times, bars, _referenceData);
						}
						if (term == "Mid Term")
						{
							upSig = atm.sync(upSig, mtInterval, stInterval, times[mtInterval], times[stInterval]);
						}

						Series dnSig;
						lock (_referenceData)
						{
							dnSig = Conditions.Calculate(dnCondition, ticker, intervals, barCount, times, bars, _referenceData);
						}
						if (term == "Mid Term")
						{
							dnSig = atm.sync(dnSig, mtInterval, stInterval, times[mtInterval], times[stInterval]);
						}
						upFilter = upFilter & upSig;
						dnFilter = dnFilter & dnSig;
					}
				}

				var ti = member.Sizes;
				for (var ii = 0; ii < barCount; ii++)
				{
					var ll = upFilter[ii] == 1;
					var ss = dnFilter[ii] == 1;
					var time = times[stInterval][ii];
					if (ti.ContainsKey(time))
					{

						ti[time] = side > 0 && ll || side < 0 && ss ? 1 : 0;
					}
				}
			}
			return output;
		}

		List<PortfolioMember> getATMConditionData(int group, List<PortfolioMember> input, string lowestInterval)
		{
			var output = new List<PortfolioMember>();

			for (var ii = 0; ii < input.Count; ii++)
			{
				var member = PortfolioMember.Clone(input[ii]);
				output.Add(member);
			}

			string stInterval = lowestInterval;
			string mtInterval = Study.getForecastInterval(stInterval, 1);
			string ltInterval = Study.getForecastInterval(mtInterval, 1);

			string[] intervals = { stInterval, mtInterval, ltInterval };

			var groupId = _model.Groups[group].Id;
			var members = output.Where(x => x.GroupId == groupId).ToList();
			var g = _model.Groups[group];

			foreach (var member in members)
			{
				var ticker = member.Ticker;

				var symbol = _model.Symbols.Find(x => x.Ticker == ticker);

				bool bp = false;
				var sectorTicker = "";
				var sector = symbol.Sector;
				var sectorNumber = 0;
				if (int.TryParse(sector, out sectorNumber))
				{
					sectorTicker = _portfolio1.getSectorSymbol(sectorNumber);
					lock (_referenceData)
					{
						_referenceData["SectorTicker"] = sectorTicker;
					}
				}
				else
				{
					bp = true;
				}

				var betas = symbol.Beta;
				var name1 = "BETA_ADJ_OVERRIDABLE";
				lock (_referenceData)
				{
					foreach (var kvp in betas)
					{
						string key = ticker + ":" + name1 + ":" + kvp.Key;
						_referenceData[key] = kvp.Value;
					}
				}

				var betaTests = symbol.BetaTTest;
				var name2 = "BETA_T_TEST";
				Series upSig;
				lock (_referenceData)
				{
					foreach (var kvp in betaTests)
					{
						string key = ticker + ":" + name2 + ":" + kvp.Key;
						_referenceData[key] = kvp.Value;
					}
				}

				var side = g.Direction;
				var conditionsEnter = g.ATMConditionsEnter;

				if (conditionsEnter.Count > 0)
				{
					Dictionary<string, List<DateTime>> times = new Dictionary<string, List<DateTime>>();
					Dictionary<string, Series[]> bars = new Dictionary<string, Series[]>();
					PortfolioMember filter = member;

					if (sectorTicker.Length > 0)
					{
						foreach (string interval in intervals)
						{
							times["sector:" + interval] = _barCache.GetTimes(sectorTicker, interval, 0);
							bars["sector:" + interval] = _barCache.GetSeries(sectorTicker, interval, new string[] { "Open", "High", "Low", "Close" }, 0);
						}
					}

					var indexKey = ticker + ":REL_INDEX";
					var indexTicker = "";
					lock (_referenceData)
					{
						if (_referenceData.ContainsKey(indexKey))
						{
							indexTicker = _referenceData[indexKey] as string;
						}
					}
					if (indexTicker.Length > 0)
					{
						foreach (string interval in intervals)
						{
							times["index:" + interval] = _barCache.GetTimes(indexTicker, interval, 0);
							bars["index:" + interval] = _barCache.GetSeries(indexTicker, interval, new string[] { "Open", "High", "Low", "Close" }, 0);
						}
					}


					foreach (string interval in intervals)
					{
						times[interval] = _barCache.GetTimes(ticker, interval, 0);
						bars[interval] = _barCache.GetSeries(ticker, interval, new string[] { "Open", "High", "Low", "Close" }, 0);
					}

					var barCount = times[stInterval].Count;
					Series enter = new Series(barCount, 1);

					string[] midTermintervals = { mtInterval, ltInterval };

					for (int jj = 0; jj < conditionsEnter.Count; jj++)
					{
						if (conditionsEnter[jj].Length > 0)
						{
							var items = conditionsEnter[jj].Split('\u0002');
							var condition = items[0].Trim();
							var term = items[1].Trim();

							var calcInterval = term == "Mid Term" ? midTermintervals : intervals;

							Series sig;
							lock (_referenceData)
							{
								sig = Conditions.Calculate(condition, ticker, calcInterval, barCount, times, bars, _referenceData);
							}
							if (term == "Mid Term")
							{
								sig = atm.sync(sig, mtInterval, stInterval, times[mtInterval], times[stInterval]);
							}

							enter = enter & sig;
						}
					}

					var ti = member.Sizes;
					for (var ii = 0; ii < barCount; ii++)
					{
						var ok = enter[ii] == 1;
						var time = times[stInterval][ii];
						if (ti.ContainsKey(time))
						{
							ti[time] = ok ? ti[time] : 0;
						}
					}
				}
			}
			return output;
		}

		void testPython()
		{
			// Initialize the Python runtime
			PythonEngine.Initialize();

			using (Py.GIL())  // Always acquire GIL before using Python objects
			{
				// Add the current directory to Python's sys.path
				dynamic sys = Py.Import("sys");
				sys.path.append(@"..\..\..");

				// Import the Python script (as a module)
				dynamic script = Py.Import(@"myscript");

				dynamic pd = Py.Import("pandas");

				var bars = getBars("SPY US Equity", "Daily", 1000);
				var dates = bars.Select(b => b.Time.ToString("yyyy-MM-dd")).ToArray();
				var prices = bars.Select(b => b.Close).ToArray();

				dynamic data = new Dictionary<string, object>();
				data["Date"] = pd.to_datetime(dates);
				data["SPY US Equity"] = prices;

				// Create pandas DataFrame from the data dictionary
				dynamic stock_data = pd.DataFrame(data);

				dynamic PortfolioOptimizer = script.PortfolioOptimizer;
				dynamic optimizer = PortfolioOptimizer(stock_data, 2.0, 2);

				// Call the optimize_portfolio method
				dynamic result = optimizer.optimize_portfolio();
			}
		}

		public class TradingPortfolio
		{
			public double PortfolioValue { get; set; }
			public List<string> Tickers { get; set; }
			public List<int> Positions { get; set; }
			public List<double> Prices { get; set; }
			public List<List<double>> PriceHistory { get; set; } // rows = time, columns = tickers
		}

		void testQuboSolver()
		{
			PythonEngine.Initialize();
			try
			{
				using (Py.GIL())
				{
					// --- Diagnostics: show python version & executable ---
					PyObject sys = Py.Import("sys");
					//Trace.WriteLine("PY Version: " + sys.GetAttr("version"));
					//Trace.WriteLine("PY Executable: " + sys.GetAttr("executable"));

					// Ensure absolute path to your project root (adjust if needed)
					string projectRoot = Path.GetFullPath(
						Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..")
					);

					// Add projectRoot to sys.path
					PyObject path = sys.GetAttr("path");
					path.InvokeMethod("append", new PyString(projectRoot));

					// (Optional) print sys.path entries
					//Trace.WriteLine("sys.path:");

					// Print sys.path entries safely
					PyObject keysList = path.InvokeMethod("copy"); // returns a Python list

					long count = keysList.Length();
					for (int i = 0; i < count; i++)
					{
						using (PyObject item = keysList[i])
						{
							//Trace.WriteLine("  - " + item.ToString());
						}
					}

					// --- Import your python helper ---
					PyObject solver = Py.Import("qubosolver2");

					// Build the QUBO: minimize x0 + x1 - 2*x0*x1
					var Q = new Dictionary<(int, int), double>
			{
				{ (0,0), 1.0 },
				{ (1,1), 1.0 },
				{ (0,1), -2.0 }
			};

					using (PyDict pyQ = new PyDict())
					{
						foreach (var kv in Q)
						{
							var key = new PyTuple(new PyObject[]
							{
						new PyInt(kv.Key.Item1),
						new PyInt(kv.Key.Item2)
							});
							pyQ.SetItem(key, new PyFloat(kv.Value));
						}

						// --- Sanity check: ping() to verify the argument reaches Python as a dict ---
						using (PyTuple pingArgs = new PyTuple(new PyObject[] { pyQ }))
						{
							PyObject pingFunc = solver.GetAttr("ping");
							PyObject pingResult = pingFunc.Invoke(pingArgs, null);
							//Trace.WriteLine("ping() -> " + pingResult.ToString());
						}

						// --- Now call solve_local(Q) with a proper args tuple ---
						PyObject func = solver.GetAttr("solve_local");
						using (PyTuple args = new PyTuple(new PyObject[] { pyQ }))
						{
							PyObject result = func.Invoke(args, null);

							// Extract allocations dict
							PyObject allocations = result.GetItem("allocations");

							// Turn allocations.keys() into a list
							PyObject keysList2 = Py.Import("builtins").GetAttr("list")
								.Invoke(new PyObject[] { allocations.InvokeMethod("keys") });

							long n = keysList2.Length();
							for (int i = 0; i < n; i++)
							{
								using (PyObject ticker = keysList2[i])
								{
									string t = ticker.ToString();
									long shares = allocations.GetItem(ticker).As<long>();
									//Trace.WriteLine($"{t}: {shares} shares");
								}
							}

							//Trace.WriteLine($"Solver used: {result.GetItem("solver")}");
						}
					}
				}
			}
			catch (PythonException ex)
			{
				//Trace.WriteLine(">>> PythonException caught:");
				//Trace.WriteLine(ex.Message);

				// Print full Python traceback
				using (Py.GIL())
				{
					try
					{
						PyObject traceback = Py.Import("traceback");
						PyObject formatted = traceback.InvokeMethod("format_exc");
						//Trace.WriteLine(formatted.ToString());
					}
					catch { /* ignore */ }
				}
			}
			finally
			{
				PythonEngine.Shutdown();
			}
		}

		// needs work
		//void testPython2(DateTime date, double portfolioValue, List<PortfolioTicker> input)
		//       {
		//		int count = 0;
		//           var dates = new List<DateTime>();
		//           var prices = new Dictionary<string, List<double>>();
		//           input.ForEach(member =>
		//           {
		//               var ticker = member.Ticker;

		//			var b = _barCache.GetBars(ticker, _model.RiskInterval, 0);
		//               var cl = new Series(b.Select(x => x.Close).ToList());
		//               if (count == 0)
		//               {
		//                   dates = b.Select(x => x.Time).Where(x => x <= date).ToList();
		//                   count = dates.Count;
		//               }
		//               prices[ticker] = cl.Resize(count).Data;
		//           });

		//           if (date > dates.Last())
		//           {
		//               date = dates.Last();
		//           }

		//		var portfolio = new TradingPortfolio();
		//		portfolio.PortfolioValue = portfolioValue;
		//           portfolio.Tickers = tickers;
		//		portfolio.Positions = portfolio.Tickers.Select(ticker =>
		//		{
		//			var size = 0;
		//			if (input[ticker].Sizes.ContainsKey(date))
		//			{
		//				size = input[ticker].Sizes[date].UpSize > 0 ? 1 : input[ticker].Sizes[date].UpSize > 0 ? -1 : 0;
		//			}
		//			return size;
		//		}).ToList();
		//           portfolio.Prices = tickers.Select(ticker => prices[ticker].Last()).ToList();
		//           portfolio.PriceHistory = null;

		//           double? targetVolatility = null;

		//		// Initialize the Python runtime
		//		PythonEngine.Initialize();

		//           using (Py.GIL())
		//           {
		//			// Add the current directory to Python's sys.path
		//			dynamic sys = Py.Import("sys");
		//			sys.path.append(@"..\..\..");

		//			dynamic optimizer = Py.Import("optimizer");

		//               // Convert portfolio fields
		//               var pyTickers = new PyList((PyObject)portfolio.Tickers.Select(t => new PyString(t)));
		//               var pyPositions = new PyList((PyObject)portfolio.Positions.Select(p => p.ToPython()));
		//               var pyPrices = new PyList((PyObject)portfolio.Prices.Select(p => p.ToPython()));

		//			// Convert 2D price history (list of list of floats) to nested PyList
		//			PyObject none = PyObject.FromManagedObject(null);
		//			PyObject pyPriceHistory = none;
		//               if (portfolio.PriceHistory != null && portfolio.PriceHistory.Count > 0)
		//               {
		//                   var outerList = new PyList();
		//                   foreach (var row in portfolio.PriceHistory)
		//                   {
		//                       outerList.Append(new PyList((PyObject)row.Select(p => p.ToPython())));
		//                   }
		//                   pyPriceHistory = outerList;
		//               }

		//               // Call the Python function
		//               dynamic result = optimizer.compute_volatility_weighted_share_sizes(
		//                   portfolio.PortfolioValue.ToPython(),
		//                   pyTickers,
		//                   pyPositions,
		//                   pyPrices,
		//                   pyPriceHistory,
		//                   targetVolatility.HasValue ? targetVolatility.Value.ToPython() : none
		//               );

		//               // Output the result
		//               string status = result["status"].ToString();
		//               Trace.WriteLine($"Optimization status: {status}");

		//               if (status != "optimal")
		//               {
		//                   Trace.WriteLine("Optimization failed or infeasible.");
		//                   return;
		//               }

		//               var tickers2 = result["tickers"];
		//               var shares = result["share_sizes"];

		//               for (int i = 0; i < tickers2.len(); i++)
		//               {
		//                   string ticker = tickers[i].ToString();
		//                   double shareCount = shares[i].As<double>();
		//                   Trace.WriteLine($"{ticker}: {shareCount:0.00} shares");
		//               }
		//           }
		//       }

		private string getLowerInterval(string interval1, string interval2)
		{
			var historicalIntervals = new string[] { "Yearly", "SemiAnnually", "Quarterly", "Monthly", "Weekly", "Daily", "D30" };
			string interval = interval1;
			int hist1Index = historicalIntervals.ToList().FindIndex(x => x == interval1);
			int hist2Index = historicalIntervals.ToList().FindIndex(x => x == interval2);
			if (hist1Index != -1 && hist2Index != -1) // both historical
			{
				if (hist2Index > hist1Index) interval = interval2;
			}
			else if (hist1Index != -1 && hist2Index == -1) // interval 1 is historical, interval 2 is not historical
			{
				interval = interval2;
			}
			else if (hist1Index == -1 && hist2Index == -1) // both intervals are intraday
			{
				int intraday1 = int.Parse(interval1);
				int intraday2 = int.Parse(interval2);
				if (intraday2 < intraday1) interval = interval2;
			}
			return interval;
		}

		class PortfolioMember
		{
			public PortfolioMember(string groupId, string ticker)
			{
				GroupId = groupId;
				Ticker = ticker;

			}

			public static PortfolioMember Clone(PortfolioMember input)
			{
				var output = new PortfolioMember(input.GroupId, input.Ticker);
				output.Sizes = input.Sizes.ToDictionary(entry => entry.Key, entry => entry.Value);
				return output;
			}

			public string GroupId { get; set; }
			public string Ticker { get; set; }
			public Dictionary<DateTime, double> Sizes { get; set; } = new Dictionary<DateTime, double>();
		}

		private List<PortfolioMember> createPortfolio(string interval, bool alpha)
		{
			var output = new List<PortfolioMember>();

			string benchmarkSymbol = _model.Symbols[0].Ticker; // _model.Benchmark;
			Dictionary<string, List<Bar>> bars = new Dictionary<string, List<Bar>>();
			bars[benchmarkSymbol] = getBars(benchmarkSymbol, interval, BarServer.MaxBarCount);
			List<DateTime> times = bars[benchmarkSymbol].Select(x => x.Time).ToList();

			DateTime date1 = _model.DataRange.Time1; // getStartDate(interval, lowestInterval); // mlPredict
			DateTime date2 = _model.UseCurrentDate ? DateTime.MaxValue : _model.DataRange.Time2; // DateTime.MaxValue;

			_model.Groups.ForEach(g =>
			{
				var symbols = alpha ? g.AlphaSymbols : g.HedgeSymbols;
				symbols.ForEach(s =>
				{
					var ticker = s.Ticker;
					var member = new PortfolioMember(g.Id, ticker);
					foreach (var time in times)
					{
						if (time >= date1 && time <= date2)
						{
							member.Sizes[time] = 1.0;
						}
					}
					output.Add(member);
				});
			});
			return output;
		}

		private List<PortfolioMember> filterPortfolio(int group, List<PortfolioMember> input, List<PortfolioMember> filter)
		{
			var output = new List<PortfolioMember>();

			for (var ii = 0; ii < input.Count; ii++)
			{
				var member = PortfolioMember.Clone(input[ii]);
				output.Add(member);
			}

			var groupId = group >= 0 ? _model.Groups[group].Id : "";
			var members = output.Where(x => groupId == "" || x.GroupId == groupId).ToList();
			var filters = filter.Where(x => groupId == "" || x.GroupId == groupId).ToList();

			int count = members.Count;

			for (var ii = 0; ii < count; ii++) // per rebalance date  <- line 1735
			{
				//if (ii == 0 || ii == 200 || ii == 400) Debug.WriteLine($"[TRADING] ii={ii} count={count}");
				var values = members[ii].Sizes;
				foreach (var kvp2 in values)
				{
					var date = kvp2.Key;
					var f = filters[ii].Sizes.ContainsKey(date) ? filters[ii].Sizes[date] : 1.0;
					members[ii].Sizes[date] = members[ii].Sizes[date] * f;
				}
			}
			return output;
		}

		private Symbol getSymbol(string ticker)
		{
			return _model.Symbols.FirstOrDefault(x => x.Ticker == ticker);
		}

		public struct BarData
		{
			public Series Time;
			public Series Open;
			public Series High;
			public Series Low;
			public Series Close;
			public Series TwoBar;
			public Series ATR;
			public Series Volume;
			public Series AvgVolume;
			public Series AvgVolatility;
			public Series Scores;
			public Series UpperTargetPrices;
			public Series LowerTargetPrices;
			public Series TradePrice;
		}

		// ============================================================
		// BUG FIX #1: Helper to get actual shares at ex-date
		// ============================================================
		private double GetSharesAtExDate(Trade trade, DateTime exDate)
		{
			if (trade.Shares == null || trade.Shares.Count == 0)
				return 0.0;

			// Find the most recent Shares entry on or before exDate
			var entry = trade.Shares
				.Where(kv => kv.Key.Date <= exDate.Date)
				.OrderByDescending(kv => kv.Key)
				.FirstOrDefault();

			return entry.Key == default(DateTime) ? 0.0 : entry.Value;
		}

		// ============================================================
		// BUG FIX #2: Centralized dividend processing with correct shares
		// ============================================================
		private void ProcessTradeDividends(
			Trade trade,
			Symbol symbol,
			DateTime prvDate,
			DateTime date,
			Dictionary<DateTime, double> dividends,
			HashSet<string> processedDividends)
		{
			if (symbol?.Dividends == null) return;

			foreach (var kvp in symbol.Dividends)
			{
				string key = kvp.Key;
				string[] fields = key.Split(':');
				if (fields.Length < 2) continue;

				DateTime exDate, payOutDate;
				if (!DateTime.TryParseExact(fields[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out exDate))
					continue;
				if (!DateTime.TryParseExact(fields[1], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out payOutDate))
					continue;

				// Check if ex-date falls in current period
				if (exDate.CompareTo(prvDate) >= 0 && exDate.CompareTo(date) <= 0)
				{
					// BUG FIX #3: Duplicate prevention
					string dividendKey = $"{trade.Id}:{symbol.Ticker}:{exDate:yyyy-MM-dd}";
					if (processedDividends.Contains(dividendKey))
						continue;

					// BUG FIX #1: Use actual shares at ex-date, not stale Investment1
					double sharesAtExDate = GetSharesAtExDate(trade, exDate);
					if (sharesAtExDate == 0.0) continue;

					double dividendPerShare = kvp.Value;

					// BUG FIX #4: Clear direction handling
					// Long position (Direction=1): receive dividends (+)
					// Short position (Direction=-1): pay dividends (-)
					double dividendAmount = trade.Direction * sharesAtExDate * dividendPerShare;
					if (dividendAmount < 0 && trade.Direction == 1)
					{ //Debug.WriteLine($"[DIV-NEG] ticker={symbol.Ticker} dir={trade.Direction} shares={sharesAtExDate:F0} divPerShare={dividendPerShare:F4} amount={dividendAmount:F2} exDate={exDate:yyyy-MM-dd}");
					}
					dividends[payOutDate] = dividends.ContainsKey(payOutDate)
											? dividends[payOutDate] + dividendAmount
						: dividendAmount;

					processedDividends.Add(dividendKey);
				}
			}
		}

		private Tuple<int, double> getTargetMarketPrice(string ticker, string interval, DateTime date)
		{
			var stInterval = interval;
			var mtInterval = atm.getInterval(stInterval, 1);

			var stBars = _barCache.GetBars(ticker, stInterval, 0);
			var mtBars = _barCache.GetBars(ticker, mtInterval, 0);

			List<DateTime> stTime = stBars.Select(b => b.Time).ToList();
			Series stOp = new Series(stBars.Select(b => b.Open).ToList());
			Series stHi = new Series(stBars.Select(b => b.High).ToList());
			Series stLo = new Series(stBars.Select(b => b.Low).ToList());
			Series stCl = new Series(stBars.Select(b => b.Close).ToList());

			int count = stCl.Count;
			Series md = Series.Mid(stHi, stLo);
			Series tr = Series.TrueRange(stHi, stLo, stCl);
			Series simTR = Series.SimpleMovingAverage(tr, 69);
			Series expTR = Series.EMAvg(tr, 69);
			Series midAv = Series.SmoothAvg(Series.EMAvg(md, 69), 5);

			Series stFt = atm.calculateFT(stHi, stLo, stCl);
			Series stFtUp = stFt.TurnsUp();
			Series stFtDn = stFt.TurnsDown();

			List<DateTime> mtTime = mtBars.Select(b => b.Time).ToList();
			Series mtHi = new Series(mtBars.Select(b => b.High).ToList());
			Series mtLo = new Series(mtBars.Select(b => b.Low).ToList());
			Series mtCl = new Series(mtBars.Select(b => b.Close).ToList());

			Series mtFt = atm.calculateFT(mtHi, mtLo, mtCl);
			Series mtFtUp = atm.sync(mtFt.TurnsUp(), mtInterval, stInterval, mtTime, stTime);
			Series mtFtDn = atm.sync(mtFt.TurnsDown(), mtInterval, stInterval, mtTime, stTime);

			var upBullCounts = new List<int>();
			var upBearCounts = new List<int>();
			var dnBullCounts = new List<int>();
			var dnBearCounts = new List<int>();
			var state = 0;
			var index = 0;
			var stDir = 0;
			var mtDir = 0;

			for (int ii = 0; ii < count; ii++)
			{
				if (mtFtUp[ii] == 1 || mtFtDn[ii] == 1)
				{
					mtDir = mtFtUp[ii] == 1 ? 1 : -1;
					state = 1;
				}

				if (stFtUp[ii] == 1 || stFtDn[ii] == 1)
				{
					stDir = stFtUp[ii] == 1 ? 1 : -1;

					if (state == 1)
					{
						state = 2;
						index = ii;
					}
					else if (state == 2)
					{
						int cnt = ii - index;
						if (mtDir > 0 && stDir < 0) upBullCounts.Add(cnt);
						else if (mtDir > 0 && stDir > 0) dnBullCounts.Add(cnt);
						else if (mtDir < 0 && stDir < 0) upBearCounts.Add(cnt);
						else if (mtDir < 0 && stDir > 0) dnBearCounts.Add(cnt);
						state = 1;
						index = ii;
					}
				}
			}

			var avgCnt = 6;
			if (upBullCounts.Count > 0 && mtDir > 0 && stDir > 0) avgCnt = (int)Math.Round((double)upBullCounts.Sum() / upBullCounts.Count);
			if (dnBullCounts.Count > 0 && mtDir > 0 && stDir < 0) avgCnt = (int)Math.Round((double)dnBullCounts.Sum() / dnBullCounts.Count);
			if (upBearCounts.Count > 0 && mtDir < 0 && stDir > 0) avgCnt = (int)Math.Round((double)upBearCounts.Sum() / upBearCounts.Count);
			if (dnBearCounts.Count > 0 && mtDir < 0 && stDir < 0) avgCnt = (int)Math.Round((double)dnBearCounts.Sum() / dnBearCounts.Count);

			var forecastBars = new Series[] { stOp, stHi, stLo, stCl };
			ForecastService.CalculateForecast(ticker, interval, stTime, forecastBars, date);
			var forecast = ForecastService.GetForecast(ticker, interval, date);
			var price = forecast.Y.ContainsKey("value") ? forecast.Y["value"][1] : double.NaN;

			var output = new Tuple<int, double>(avgCnt, price);
			return output;
		}

		private void testRiskEngine2()
		{
			ExampleRun.Run();
		}

		private double getAnnFactor(List<DateTime> input)
		{
			var annFactor = double.NaN;
			if (input.Count > 0)
			{
				var startTime = input.First();
				var endTime = input.Last();
				var duration = endTime - startTime;
				var yearDur = duration.TotalDays / 365.25;
				annFactor = input.Count / yearDur;
			}
			return annFactor;
		}

		Series stSC = null;
		Series mtSC = null;

		private MarketRegime DetectMarketRegime(int barIndex, string interval)
		{
			if (stSC == null)
			{
				var ticker = "SPY US Equity";

				var stInterval = interval;
				var mtInterval = Study.getForecastInterval(stInterval, 1);

				var stTimes = _barCache.GetTimes(ticker, stInterval, 0);
				var mtTimes = _barCache.GetTimes(ticker, mtInterval, 0);
				var stBars = _barCache.GetSeries(ticker, stInterval, new string[] { "Open", "High", "Low", "Close" }, 0);
				var mtBars = _barCache.GetSeries(ticker, mtInterval, new string[] { "Open", "High", "Low", "Close" }, 0);

				Series rp;
				lock (_referenceData)
				{
					rp = atm.calculateRelativePrice(stInterval, stBars, _referenceData, 5);
				}

				var times = new Dictionary<string, List<DateTime>>();
				times[stInterval] = stTimes;
				times[mtInterval] = mtTimes;
				var bars = new Dictionary<string, Series[]>();
				bars[stInterval] = stBars;
				bars[mtInterval] = mtBars;
				Series score = atm.getScore(times, bars, new string[] { stInterval, mtInterval });
				stSC = atm.calculateSCSig(score, rp, 2);
			}

			return stSC[barIndex] == 1 ? MarketRegime.RiskOn : MarketRegime.RiskOff;
		}

		private bool filterTicker(string ticker, DateTime date)
		{
			// Try exact match first, then with " US Equity" suffix
			var entries = _tickerInfos.Where(t => string.Equals(t.Ticker.Trim(), ticker.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

			// Extract base ticker for debug/lookup (strip " US Equity" suffix if present)

			if (entries.Count == 0)
			{
				return false;
			}

			// Filter out if date is outside valid membership window (delisted/acquired)
			if (!entries.Any(t => date >= t.StartDate && date <= t.EndDate))
			{
				return true;
			}

			// Filter out if end date has passed by more than 30 days
			var latestEnd = entries.Max(t => t.EndDate);
			if (latestEnd < date.AddDays(-30))
			{
				return true;
			}

			return false;
		}

		private async void createTrades(List<PortfolioMember> portfolio)
		{
			var tradeInfo = new List<string>();

			var moneyManagementEnable = _model.UsePortRisk1;
			var stopLossPercent1 = 2.5;
			var stopLossPercent2 = 4.0;
			var stopLossPercent3 = 6.0;
			var reductionPercent1 = 50.0;
			var reductionPercent2 = 75.0;
			var reductionPercent3 = 100.0;
			var recoveryPercent1 = 25.0; // increase the risk percent
			var recoveryPercent2 = 50.0;
			var recoveryPercent3 = 100.0;
			var recoveryPeriod1 = 2; // weeks
			var recoveryPeriod2 = 4;
			var recoveryPeriod3 = 6;

			var exitLevel = 0;

			// model parameters
			var useCost = _model.UseExecutionCost;
			var useBorrowingCost = _model.UseBorrowingCost;
			var useDiv = _model.UseDiv;
			var useHedge = _model.UseHedge;
			var useBetaHedge = _model.UseBetaHedge;
			//var useBetaAdjust = _model.UseBetaAdjust;
			//var useBorrowingCost = true;
			//var useDividends = true;
			// useBetaNeutral = _model.getConstraint("BetaNeutral").Enable;
			//var useMktNeutral = _model.getConstraint("MktNeutral").Enable;
			//var usePortRisk1 = _model.UsePortRisk1;
			//var portRiskPercent1 = _model.PortRiskPercent1;
			//var usePortRisk2 = _model.UsePortRisk2;
			//var portRiskPercent2 = _model.PortRiskPercent2;
			//var usePortRisk3 = _model.UsePortRisk3;
			//var portRiskPercent3 = _model.PortRiskPercent3;
			var useTradeRisk = _model.UseTradeRisk;
			var tradeRiskPercent = _model.TradeRiskPercent;
			var useCoolDown = _model.UseCoolDown;
			var coolDownPeriod = useCoolDown ? _model.CoolDownPeriod : 0;
			var riskBudget = _model.RiskBudget;
			var useRegimeRiskEngine = false;  // dont use this one
			var useRiskEngineMN3 = false;

			// use these two for the portfolio
			var useBetaNeutralRiskEngine = true;  // old engine MNRiskParity
			var useRiskOnOff = _model.UseRiskOnOff;   // false is no RiskON|Off  this is the beta adjust code   filter for the beta of longs and shorts
			var useSharpeThrottle = false;

			var useAlphaWeight = _model.getConstraint("AlphaWeighting").Enable;
			var alphaWeightDiag = new AlphaWeightingDiagnostic();

			var useRiskEngine = useRegimeRiskEngine || useBetaNeutralRiskEngine || useRiskEngineMN3;

			var riskInterval = _model.ATMAnalysisInterval; // _model.RiskInterval;

			var random = new Random(42);

			var useVwap = _model.rbNextBarVWAP;

			var initialPortfolioValue = _model.InitialPortfolioBalance * _model.Groups[0].Leverage;
			var peakPortfolioValue = initialPortfolioValue;

			var regimes = new List<int>();

			var borrowingCostEngine = new BorrowingCostEngine();

			// NOISE TRADE SUPPRESSION
			const double NoiseDeadZonePct = 0.96;
			const double NoiseDeadZoneNavBps = 0.015;
			int noiseTradesBlocked = 0;

			var throttleConfig = new GrossThrottleConfig
			{
				WindowDays = 10,
				GrossMin = 0.45,
				OffThreshold = 0.0,
				OnThreshold = 0.5,
				RampDownPerDay = 0.15,
				RampUpPerDay = 0.10
			};
			var throttle = new RebalanceAwareGrossThrottle(throttleConfig, initialPortfolioValue);

			//var data = new Dictionary<DateTime, Dictionary<string, TradeSize>>();
			//var startTime = DateTime.MaxValue;
			//foreach (var member in alphaPortfolio)
			//{
			//    var ticker = member.Ticker;
			//    foreach (var kvp2 in member.Sizes)
			//    {
			//        var date = kvp2.Key;
			//        startTime = date < startTime ? date : startTime;
			//        var signal = kvp2.Value;

			//        if (!data.ContainsKey(date))
			//        {
			//            data[date] = new Dictionary<string, TradeSize>();
			//        }
			//        data[date][ticker] = signal;
			//    }
			//}

			var bars = new Dictionary<string, BarData>();
			var dailyBars = new Dictionary<string, BarData>();
			var dates = new List<DateTime>(); // data.Keys.OrderBy(x => x).ToList();

			var vixCloses = new Dictionary<DateTime, double>();

			//var tickers1 = alphaPortfolio.Keys.ToList();

			Trade hedgeTrade = null;
			string hedgeTicker = "SPY US Equity";
			Symbol hedgeSymbol = new Symbol(hedgeTicker);
			//tickers1.Add(hedgeTicker);

			//string marketTicker = "SPY US Equity";
			//Symbol marketSymbol = new Symbol(marketTicker);
			//tickers1.Add(marketTicker);

			var tradeInterval = riskInterval == "D30" ? "Daily" : riskInterval;
			var vixBars = _barCache.GetBars("VIX Index", tradeInterval, 0);
			foreach (Bar vixBar in vixBars)
			{
				vixCloses[vixBar.Time] = vixBar.Close;
			}

			double annFactor = double.NaN;
			int count = 0;

			DateTime lastBarDate = default(DateTime);

			foreach (var member in portfolio)
			{
				var ticker = member.Ticker;
				if (!bars.ContainsKey(ticker))
				{
					// model group parameters
					var group = _model.Groups.FirstOrDefault(g => g.Id == member.GroupId);
					var atrPeriod = group != null ? group.ATRRiskPeriod : 1;

					var barData = new BarData();
					var b = _barCache.GetBars(ticker, riskInterval, 0);


					if (dates.Count == 0)
					{
						dates = b.Select(x => x.Time).ToList();
						annFactor = getAnnFactor(dates);
						count = dates.Count;
						lastBarDate = b[count - 1].Time;
						//Debug.WriteLine($"[DATES] Last bar date: {dates.Last():yyyy-MM-dd} ({dates.Last().DayOfWeek})");
						//Debug.WriteLine($"[DATES] Total weekly bars: {dates.Count}");
					}

					// sync bars to dates (remove bars outside of the test range and fill in empty bars inside the test range)
					//var t1 = dates.ToHashSet();
					//var t2 = b.Where(x => t1.Contains(x.Time)).Select(x => (x.Time, x)).ToDictionary();
					//foreach (var d in dates)
					//{
					//	if (!t2.ContainsKey(d))
					//	{
					//		var b2 = new Bar();
					//		b2.Time = d;
					//		t2[d] = b2;
					//	}
					//}
					//var t3 = t2.Keys.OrderBy(x => x);
					//b = t3.Select(x => t2[x]).ToList();


					var ti = new Series(b.Select(x => x.Time.ToOADate()).ToList());
					var op = new Series(b.Select(x => x.Open).ToList());
					var hi = new Series(b.Select(x => x.High).ToList());
					var lo = new Series(b.Select(x => x.Low).ToList());
					var cl = new Series(b.Select(x => x.Close).ToList());
					var vl = new Series(b.Select(x => x.Volume).ToList());
					var tb = atm.calculateTwoBarPattern(op, hi, lo, cl, 0);
					var avgTrueRange = Series.SmoothAvg(Series.TrueRange(hi, lo, cl), atrPeriod);
					var factor = riskInterval == "Daily" ? 1 : riskInterval == "Weekly" ? 5 : 20;
					var avgVolume = Series.SmoothAvg(vl * 1000, 20) / factor;
					var avgVolatility = new Series(cl.Data.Select((_, i) => atm.GetVolatility(20, i, cl, annFactor)).ToList());

					var b1 = _barCache.GetBars(ticker, tradeInterval, 0);
					var tradePrice = new Series(b1.Select(x => x.Close).ToList());

					// calculate scores
					var stInterval = riskInterval;
					string mtInterval = Study.getForecastInterval(stInterval, 1);
					var stTimes = _barCache.GetTimes(ticker, stInterval, 0);
					var mtTimes = _barCache.GetTimes(ticker, mtInterval, 0);
					var times = new Dictionary<string, List<DateTime>>();
					times[stInterval] = stTimes;
					times[mtInterval] = mtTimes;
					var bars1 = new Dictionary<string, Series[]>();
					var stBars = _barCache.GetSeries(ticker, stInterval, new string[] { "Open", "High", "Low", "Close" }, 0);
					var mtBars = _barCache.GetSeries(ticker, mtInterval, new string[] { "Open", "High", "Low", "Close" }, 0);
					bars1[stInterval] = stBars;
					bars1[mtInterval] = mtBars;
					Series scores = atm.getScore(times, bars1, new string[] { stInterval, mtInterval });

					// calculate target prices
					var sigmaTargetPrices = atm.calculate3Sigma(hi, lo, cl);
					var upperTargetPrices = new Series(cl.Data.Select((c, i) =>
					{
						var max = double.MinValue;
						sigmaTargetPrices.ForEach(t => { max = (c < t[i]) ? Math.Max(t[i], max) : max; });
						return max;
					}).ToList());
					var lowerTargetPrices = new Series(cl.Data.Select((c, i) =>
					{
						var min = double.MaxValue;
						sigmaTargetPrices.ForEach(t => { min = (c > t[i]) ? Math.Min(t[i], min) : min; });
						return min;
					}).ToList());

					barData.Time = ti.Resize(count);

					// sanity check
					//bool bp = false;
					//for (var kk = 0; kk < count; kk++)
					//{
					//	if (ti[kk] != dates[kk].ToOADate())
					//	{
					//		bp = true;
					//	}
					//}

					barData.Open = op.Resize(count);
					barData.High = hi.Resize(count);
					barData.Low = lo.Resize(count);
					barData.Close = cl.Resize(count);
					barData.Volume = vl.Resize(count);
					barData.TwoBar = tb.Resize(count);
					barData.ATR = avgTrueRange.Resize(count);
					barData.AvgVolume = avgVolume.Resize(count);
					barData.AvgVolatility = avgVolatility.Resize(count);
					barData.Scores = scores.Resize(count);
					barData.UpperTargetPrices = upperTargetPrices.Resize(count);
					barData.LowerTargetPrices = lowerTargetPrices.Resize(count);
					barData.TradePrice = tradePrice.Resize(count);

					bars[ticker] = barData;
				}

				if (!dailyBars.ContainsKey(ticker))
				{
					var barData = new BarData();
					var b = _barCache.GetBars(ticker, "Daily", 0);
					var ti = new Series(b.Select(x => x.Time.ToOADate()).ToList());
					var cl = new Series(b.Select(x => x.Close).ToList());
					barData.Time = ti;
					barData.Close = cl;
					dailyBars[ticker] = barData;
				}
			}

			var maxVaR = 0.5;

			// Force previous state to 100% before transitioning to Predicting
			if (_progressTotalNumber > 0) _progressCompletedNumber = _progressTotalNumber;
			sendProgressEvent();

			_progressState = ProgressState.Predicting;
			_progressCompletedNumber = 0;
			_progressTotalNumber = count;
			sendProgressEvent();

			var openTrades = new Dictionary<string, Trade>();

			// ============================================================
			// ── ARCHIVE BOOTSTRAP ───────────────────────────────────────────
			// One-time seed: if a model's LockedNav in the internal store is
			// missing or stale, read the correct NAV from the archive dailyNav.csv
			// and write it to the internal store. This runs on every startup but
			// only writes if the archive file is newer than what's stored.
			// Models to bootstrap and their archive folder timestamps:
			BootstrapLockedNavFromArchive("OEX V2 DUP", "202603091045_backtest_archive");
			BootstrapLockedNavFromArchive("CMR US LG CAP", "202603091045_backtest_archive", "OEX V2 DUP");

			// TRADE SET LOCK CHECK
			// If locked trades exist, skip the entire main loop and replay
			// the locked trade history. This guarantees identical transactions
			// and metrics regardless of Bloomberg data changes.
			// ============================================================

			// In Live mode, never load locked trades — always run the main loop fresh
			var _liveResetDone = false; // Live mode: tracks one-time reset at inception boundary
			var _lockedTradeSet = _model.IsLiveMode ? null : LoadLockedTrades();
			var _usingLockedTrades = _lockedTradeSet != null;
			//Debug.WriteLine($"[LOCK] _usingLockedTrades={_usingLockedTrades} trades={_lockedTradeSet?.Count ?? 0} model={_model.Name}");

			// ── CHECKSUM GUARD ──────────────────────────────────────────────
			// If a trade lock exists, verify model settings haven't changed.
			// Mismatch = strategy parameters were altered after the lock was
			// created. Show a warning and let the user cancel.
			// In Live mode, check if any DecisionLock files exist -- if so, settings are frozen
			var _decisionLockFolder = System.IO.Path.Combine(MainView.GetDataFolder(), "decisionlock", _model.Name);
			var _hasLiveDecisionLocks = _model.IsLiveMode && System.IO.Directory.Exists(_decisionLockFolder)
				&& System.IO.Directory.GetFiles(_decisionLockFolder, "*.csv").Length > 0;

			if ((_usingLockedTrades || _hasLiveDecisionLocks) && !VerifyLockChecksum())
			{
				var msg = "⚠️  Strategy Settings Mismatch\n\n" +
					"The current model settings differ from when the trade lock\n" +
					"was created. Replaying the locked trades with changed settings\n" +
					"will produce misleading performance figures.\n\n" +
					"Locked trades cannot be regenerated without deleting the lock.\n\n" +
					"  OK  =  Restore original settings and continue\n" +
					"  Cancel  =  Abort this run\n\n" +
					"To regenerate with new settings, manually delete:\n" +
					$"  tradelock\\{_model.Name}\n" +
					$"  decisionlock\\{_model.Name}";

				var result = System.Windows.MessageBox.Show(
					msg,
					"Trade Lock — Settings Mismatch",
					System.Windows.MessageBoxButton.OKCancel,
					System.Windows.MessageBoxImage.Warning);

				if (result == System.Windows.MessageBoxResult.Cancel)
				{
					//Debug.WriteLine("[Checksum] Run cancelled by user.");
					_runCancelled = true;
					return; // abort createTrades cleanly
				}
				else
				{
					// User clicked OK — they acknowledge settings changed but want
					// to continue with locked trades. Replay proceeds unchanged.
					//Debug.WriteLine("[Checksum] User chose to continue despite mismatch.");
				}
			}

			var trades = _lockedTradeSet ?? new List<Trade>();

			var totalDividendAmount = 0.0;

			// todo 
			// List<double> portfolioValues;
			// index is group number of ticker
			var portfolioValue = initialPortfolioValue;
			var monthPortfolioValue = initialPortfolioValue;
			var highWaterPortfolioValue = initialPortfolioValue;
			var coolDown = 0;

			var previousPortfolioValue = portfolioValue;
			var riskPercent = 100.0;
			DateTime dateAtRiskReduction = default(DateTime);
			double portfolioValueRiskReduction = 0;
			double reductionPercent = 0;
			int consecutivePositiveWeeks = 0;
			double portfolioValueAtLastWeek = initialPortfolioValue;
			//var rampUpDate = DateTime.MinValue;

			var values = new List<double>();
			var returns = new List<double>();

			var monthDate = default(DateTime);
			var monthValue = 0.0;
			var monthDates = new List<DateTime>();
			var monthValues = new List<double>();
			var monthIndex = 0;

			var useYield = false;

			//DateTime date2 = _model.UseCurrentDate ? DateTime.UtcNow : _model.DataRange.Time2; // DateTime.MaxValue;
			//var now = DateTime.Now;
			//var currentDay = new DateTime(now.Year, now.Month, now.Day);
			//var endsOnCurrentDay = (now - date2).Days < 7;

			var resetPortfolio = new Dictionary<string, int>();
			var rand = new Random();

			// daily start index of earliest signal
			//var startIndex = dates.FindIndex(x => x >= startTime);
			//var signals = new Dictionary<string, TradeSize>();

			var constraints = new PortfolioConstraints
			{
				MaxGrossBookPercent = 2.00,
				MaxNetExposurePercent = 0.10,
				MaxGrossSectorExposure = 1.75,
				MaxNetSectorExposure = 0.12,
				MaxNetBetaSectorExposure = 0.12,
				MaxGrossIndustryExposure = 2.00,
				MaxNetIndustryExposure = 0.12,
				MaxNetBetaIndustryExposure = 0.12,
				MaxGrossSubIndustryExposure = 2.00,
				MaxNetSubIndustryExposure = 0.12,
				MaxNetBetaSubIndustryExposure = 0.12,
				MaxPortfolioVaR95Percent = 0.01,
				MaxPortfolioCVaR95Percent = 0.015,
				MaxPredictedVolatilityPercent = 0.12
			};

			monthIndex = 0;

			DateTime startDate = _model.DataRange.Time1;
			DateTime liveStartDate = _model.IsLiveMode ? _model.LiveStartDate : startDate; // ← add this

			var borrowingDate = _model.IsLiveMode ? liveStartDate : startDate;

			var dividends = new Dictionary<DateTime, double>();

			// BUG FIX #3: Track processed dividends to prevent duplicates
			var processedDividends = new HashSet<string>();

			var nav = new Dictionary<DateTime, double>();

			var prvDate = startDate;

			// ============================================================
			// DIAGNOSTIC: Track Shares entries creation
			// ============================================================
			int sharesEntriesCreatedTotal = 0;
			int daysWithOpenTrades = 0;
			int totalOpenTradesAcrossAllDays = 0;

			// ============================================================
			// DIAGNOSTIC: Price comparison logging
			// ============================================================
			var priceComparisonLog = new List<string>();

			// main loop — skipped entirely when trade set is locked
			if (!_usingLockedTrades)
				for (var ii = 0; ii < count; ii++) // per rebalance date  <- line 1735
				{
					//if (ii == 0 || ii == 200 || ii == 400) Debug.WriteLine($"[TRADING] ii={ii} count={count}");
					//if (ii == 0) Debug.WriteLine($"[FLAGS] useRegime={useRegimeRiskEngine} useBetaNeutral={useBetaNeutralRiskEngine}");
					var riskValue = riskPercent / 100 * portfolioValue;

					//var rampUpDuration = DateTime.Now - rampUpDate;
					//var rampUpWeeks = rampUpDuration.Days / 7;
					//if (rampUpWeeks < recoveryPeriod1) riskPercent = recoveryPercent1;
					//else if (rampUpWeeks < recoveryPeriod2) riskPercent = recoveryPercent2;
					//else if (rampUpWeeks < recoveryPeriod3) riskPercent = recoveryPercent3;

					var di = ii;
					var date = dates[di];


					if (date < startDate) continue;

					// Live mode: skip all dates before live inception
					if (_model.IsLiveMode && date < liveStartDate) continue;

					// Live mode: skip non-rebalance bars (Mon-Thu).
					// Sizes[] is pre-populated for every bar date so we cannot use ContainsKey.
					// Rebalance is weekly on Friday; any non-Friday live bar must be skipped.
					if (_model.IsLiveMode && date >= liveStartDate && date.DayOfWeek != DayOfWeek.Friday) continue;

					// Live mode: one-time hard reset at the inception boundary
					if (_model.IsLiveMode && !_liveResetDone)
					{
						openTrades.Clear();
						trades.Clear();
						portfolioValue = initialPortfolioValue;
						monthPortfolioValue = initialPortfolioValue;
						highWaterPortfolioValue = initialPortfolioValue;
						nav.Clear();
						_liveResetDone = true;
					}


					DateTime endDate = _model.UseCurrentDate ? DateTime.MaxValue : _model.DataRange.Time2;
					if (date > endDate) break;
					var last = (ii == count - 1) || (ii < count - 1 && dates[ii + 1] > endDate);

					if (monthIndex == 0)
					{
						monthIndex = di;
						monthDate = date;
					}

					var newMonth = date.Month != monthDate.Month;

					if (newMonth || last)
					{
						for (var ix = monthIndex; ix < di; ix++)
						{
							monthDates.Add(dates[ix]);
							var mv = _model.Groups[0].Leverage * (100 * (portfolioValue - monthPortfolioValue) / monthPortfolioValue);
							monthValues.Add(mv);
						}

						monthDate = date;
						monthValue = 0.0;
						monthPortfolioValue = portfolioValue;
						monthIndex = di;
					}

					var closeTrades = new List<Trade>();

					// PRE-REBALANCE BETA: snapshot outgoing portfolio beta before optimizer runs
					if (openTrades.Count > 0)
					{
						var betaKey = date.ToString("yyyy-MM-dd");
						double weightedBeta = 0.0;
						int preLongCnt = 0, preShortCnt = 0;
						foreach (var kvp in openTrades)
						{
							var trade = kvp.Value;
							var sym = _model.Symbols.FirstOrDefault(x => x.Ticker == trade.Ticker);
							if (sym == null) continue;
							double beta;
							if (sym.Beta.ContainsKey(betaKey))
								beta = sym.Beta[betaKey];
							else if (sym.Beta.Count > 0)
							{
								var lastBetaKey = sym.Beta.Keys
									.Where(bk => string.Compare(bk, betaKey) < 0)
									.OrderByDescending(bk => bk).FirstOrDefault();
								beta = lastBetaKey != null ? sym.Beta[lastBetaKey] : 1.0;
							}
							else beta = 1.0;

							// signed dollar exposure: positive = long, negative = short
							weightedBeta += trade.Direction * trade.Investment1 * beta;
							if (trade.Direction > 0) preLongCnt++;
							else preShortCnt++;
						}
						double preNetBeta = portfolioValue > 0 ? weightedBeta / portfolioValue : 0.0;
						_preRebalanceBetaLog.Add((date, preNetBeta, preLongCnt, preShortCnt));
					}

					//if (data.ContainsKey(date))
					//{
					//    signals = data[date];
					//}

					var riskSizes = new Dictionary<string, double>();

					var regime = DetectMarketRegime(ii, riskInterval);
					regimes.Add((int)regime);

					if (useRegimeRiskEngine)
					{
						var stocks = new List<RiskEngineMNParity.Stock>();
						var lCnt = 0;
						var sCnt = 0;

						foreach (var member in portfolio)
						{
							var ticker = member.Ticker;
							var signal = member.Sizes.ContainsKey(date) ? member.Sizes[date] : 0;
							if (_model.IsLiveMode && date < liveStartDate) signal = 0;
							var group = _model.Groups.Find(g => g.Id == member.GroupId);
							if (filterTicker(ticker, date)) signal = 0;
							var side = group.Direction;
							var groupId = member.GroupId;
							var riskKey = groupId + "\t" + ticker;
							riskSizes[riskKey] = 0;

							if (signal != 0)
							{
								var symbol = _model.Symbols.FirstOrDefault(x => x.Ticker == ticker);
								var t = member.Ticker;
								var rawSector = symbol.Sector ?? "";
								var rawInd = symbol.Industry ?? "";
								var rawSubInd = symbol.SubIndustry ?? "";
								var s = rawSector.Length >= 2 ? rawSector.Substring(0, 2) : rawSector;
								var ind = rawInd.Length >= 4 ? rawInd.Substring(0, 4) : rawInd;
								var subInd = rawSubInd.Length >= 6 ? rawSubInd.Substring(0, 6) : rawSubInd;
								var k = date.ToString("yyyy-MM-dd");
								double beta;
								if (symbol.Beta.ContainsKey(k))
								{
									beta = symbol.Beta[k];
								}
								else if (symbol.Beta.Count > 0)
								{
									var lastBetaKey = symbol.Beta.Keys.Where(bk => string.Compare(bk, k) < 0).OrderByDescending(bk => bk).FirstOrDefault();
									beta = lastBetaKey != null ? symbol.Beta[lastBetaKey] : 1;
								}
								else
								{
									beta = 1;
								}
								if (!bars.ContainsKey(ticker))
								{
									continue;
								}
								var closes = bars[ticker].Close.Data.Take(dates.Count()).Select(x => x).ToList();
								double marketcap = symbol.Marketcap.ContainsKey(k) ? symbol.Marketcap[k] : 100000000;

								if (!double.IsNaN(closes[ii]))
								{
									var sec = new RiskEngineMNParity.Stock
									{
										GroupId = groupId ?? "",
										Ticker = t ?? "",
										Sector = s ?? "Unknown",
										Industry = ind ?? "Unknown",
										SubIndustry = subInd ?? "Unknown",
										RequiredBook = side > 0 ? RiskEngineMNParity.BookSide.Long : RiskEngineMNParity.BookSide.Short,
										Beta = double.IsNaN(beta) ? 1.0 : beta,
										Volatility = (bars.ContainsKey(ticker) &&
													bars[ticker].AvgVolatility != null &&
													ii < bars[ticker].AvgVolatility.Count &&
													!double.IsNaN(bars[ticker].AvgVolatility[ii]))
													? bars[ticker].AvgVolatility[ii]
													: 0.25,
										MarketCap = marketcap,
										Price = closes[ii],
										AlphaScore = Math.Abs(signal),
									};
									stocks.Add(sec);
									if (side > 0) lCnt++;
									else if (side < 0) sCnt++;
								}
							}
						}


						if (lCnt > 0 && sCnt > 0)
						{
							// Validate stocks before optimization
							//Trace.WriteLine($"[RiskEngine] Validating {stocks.Count} stocks...");

							var invalidStocks = new List<string>();
							foreach (var stock in stocks)
							{
								var issues = new List<string>();
								if (string.IsNullOrEmpty(stock.Ticker)) issues.Add("Ticker null/empty");
								if (string.IsNullOrEmpty(stock.GroupId)) issues.Add("GroupId null/empty");
								if (stock.Sector == null) issues.Add("Sector null");
								if (stock.Industry == null) issues.Add("Industry null");
								if (stock.SubIndustry == null) issues.Add("SubIndustry null");
								if (double.IsNaN(stock.Beta)) issues.Add($"Beta=NaN");
								if (double.IsNaN(stock.Volatility) || stock.Volatility <= 0) issues.Add($"Volatility={stock.Volatility}");
								if (double.IsNaN(stock.Price) || stock.Price <= 0) issues.Add($"Price={stock.Price}");
								if (double.IsNaN(stock.AlphaScore)) issues.Add("AlphaScore=NaN");

								if (issues.Count > 0)
								{
									invalidStocks.Add($"{stock.Ticker}: {string.Join(", ", issues)}");
									//Trace.WriteLine($"[RiskEngine] INVALID: {stock.Ticker} - {string.Join(", ", issues)}");
								}
								else
								{
									//Trace.WriteLine($"[RiskEngine] VALID: {stock.Ticker} Book={stock.RequiredBook} Beta={stock.Beta:F2} Vol={stock.Volatility:F4} Price={stock.Price:F2} Alpha={stock.AlphaScore:F2}");
								}
							}

							if (invalidStocks.Count > 0)
							{
								//Trace.WriteLine($"[RiskEngine] WARNING: Found {invalidStocks.Count} invalid stocks");
							}
							//Trace.WriteLine($"[RiskEngine] ========== {date:yyyy-MM-dd} ==========");
							//Trace.WriteLine($"[RiskEngine] Detected Regime: {regime}");
							//Trace.WriteLine($"[RiskEngine] Long Candidates: {lCnt}, Short Candidates: {sCnt}");

							var cfg = RiskEngineMNParity.RegimeConfigFactory.Create(regime, portfolioValue);

							// Override with model constraints
							cfg.MaxPerNameFraction = _model.getConstraint("SingleNameCapAbs").Enable
								? _model.getConstraint("SingleNameCapAbs").Value.Value / 100 : cfg.MaxPerNameFraction;
							cfg.UseRiskParity = _model.getConstraint("RiskParity").Enable;
							cfg.EnforceMarketNeutral = _model.getConstraint("MktNeutral").Enable;
							cfg.EnforceBetaTarget = _model.getConstraint("BetaNeutral").Enable;
							cfg.EnforceVolatilityNeutral = _model.getConstraint("VolNeutral") != null && _model.getConstraint("VolNeutral").Enable;
							cfg.MaxSectorFraction = _model.getConstraint("SectorNet").Enable
								? _model.getConstraint("SectorNet").Value.Value / 100 : cfg.MaxSectorFraction;
							cfg.MaxIndustryFraction = _model.getConstraint("IndustryNet").Enable
								? _model.getConstraint("IndustryNet").Value.Value / 100 : cfg.MaxIndustryFraction;
							cfg.MaxSubIndustryFraction = _model.getConstraint("SubIndNet").Enable
								? _model.getConstraint("SubIndNet").Value.Value / 100 : cfg.MaxSubIndustryFraction;

							//Trace.WriteLine($"[RiskEngine] Config: TargetBeta={cfg.TargetPortfolioBeta:F2}, BetaTolerance={cfg.BetaTolerance:F2}");
							//Trace.WriteLine($"[RiskEngine] Config: EnforceBetaTarget={cfg.EnforceBetaTarget}, EnforceMarketNeutral={cfg.EnforceMarketNeutral}");
							//Trace.WriteLine($"[RiskEngine] Portfolio Value: {cfg.PortfolioValue:C0}");

							RiskEngineMNParity.OptimizationResult result = null;

							try
							{
								// Determine which stocks to optimize
								List<RiskEngineMNParity.Stock> stocksToOptimize = null;

								if (useRiskOnOff)
								{
									var (minLongBeta, maxLongBeta, minShortBeta, maxShortBeta) = RegimeConfigFactory.GetBetaFilters(regime);

									//Trace.WriteLine($"[RiskEngine] Beta filters for {regime}: Long=[{minLongBeta:F2}, {maxLongBeta:F2}], Short=[{minShortBeta:F2}, {maxShortBeta:F2}]");

									var filteredStocks = stocks.Where(s =>
									{
										bool passes;
										if (s.RequiredBook == RiskEngineMNParity.BookSide.Long)
										{
											passes = minLongBeta <= s.Beta && s.Beta <= maxLongBeta;
										}
										else
										{
											passes = minShortBeta <= s.Beta && s.Beta <= maxShortBeta;
										}

										if (!passes)
										{
											//Trace.WriteLine($"[RiskEngine] Beta filter EXCLUDED: {s.Ticker} Beta={s.Beta:F2} Book={s.RequiredBook}");
										}
										return passes;
									}).ToList();

									var filteredLongs = filteredStocks.Count(s => s.RequiredBook == RiskEngineMNParity.BookSide.Long);
									var filteredShorts = filteredStocks.Count(s => s.RequiredBook == RiskEngineMNParity.BookSide.Short);
									//Trace.WriteLine($"[RiskEngine] After beta filter: {filteredStocks.Count} stocks ({filteredLongs} longs, {filteredShorts} shorts)");

									// FALLBACK: If beta filter removes all stocks from one side, use unfiltered stocks
									if (filteredLongs == 0 || filteredShorts == 0)
									{
										//Trace.WriteLine($"[RiskEngine] WARNING: Beta filter too restrictive! Using ALL {stocks.Count} unfiltered stocks instead.");
										stocksToOptimize = null;
									}
									else
									{
										stocksToOptimize = filteredStocks;
									}
								}
								else
								{
									// No beta filtering - use all stocks
									stocksToOptimize = stocks;
								}
								// Only run optimizer if we have valid stocks
								if (stocksToOptimize != null && stocksToOptimize.Count > 0)
								{
									//Trace.WriteLine($"[RiskEngine] === PRE-OPTIMIZER ===");
									//Trace.WriteLine($"[RiskEngine] Optimizing {stocksToOptimize.Count} stocks");
									foreach (var st in stocksToOptimize)
									{
										//Trace.WriteLine($"[RiskEngine]   -> {st.Ticker} Book={st.RequiredBook} Beta={st.Beta:F2}");
									}

									using (var engine = new RiskEngineMNParity.Engine(cfg))
									{
										result = engine.Optimize(stocksToOptimize);
									}

									if (result != null && result.DollarWeights != null)
									{
										var debugLongFrac = result.DollarWeights.Where(kvp => kvp.Value > 0).Sum(kvp => Math.Abs(kvp.Value));
										var debugShortFrac = result.DollarWeights.Where(kvp => kvp.Value < 0).Sum(kvp => Math.Abs(kvp.Value));
										//Debug.WriteLine($"[DEBUG RiskEngine Output] Long fraction: {debugLongFrac:F6}, Short fraction: {debugShortFrac:F6}");
									}
								}
								else
								{
									//Trace.WriteLine($"[RiskEngine] No stocks to optimize - skipping this period");
								}

								// Final diagnostic before optimizer call
								//Trace.WriteLine($"[RiskEngine] === PRE-OPTIMIZER DIAGNOSTIC ===");
								//Trace.WriteLine($"[RiskEngine] stocksToOptimize.Count = {stocksToOptimize.Count}");
								var finalLongs = stocksToOptimize.Count(s => s.RequiredBook == RiskEngineMNParity.BookSide.Long);
								var finalShorts = stocksToOptimize.Count(s => s.RequiredBook == RiskEngineMNParity.BookSide.Short);
								//Trace.WriteLine($"[RiskEngine] Final Longs: {finalLongs}, Final Shorts: {finalShorts}");
								foreach (var st in stocksToOptimize)
								{
									//Trace.WriteLine($"[RiskEngine]   -> {st.Ticker} Book={st.RequiredBook} Beta={st.Beta:F2} Vol={st.Volatility:F4} Price={st.Price:F2}");
								}
								//Trace.WriteLine($"[RiskEngine] === END PRE-OPTIMIZER DIAGNOSTIC ===");

								// Run optimizer
								using (var engine = new RiskEngineMNParity.Engine(cfg))
								{
									result = engine.Optimize(stocksToOptimize);
								}

								// Debug output
								if (result != null && result.DollarWeights != null)
								{
									var debugLongFrac = result.DollarWeights.Where(kvp => kvp.Value > 0).Sum(kvp => Math.Abs(kvp.Value));
									var debugShortFrac = result.DollarWeights.Where(kvp => kvp.Value < 0).Sum(kvp => Math.Abs(kvp.Value));
									//Debug.WriteLine($"[DEBUG RiskEngine Output] Long fraction: {debugLongFrac:F6}, Short fraction: {debugShortFrac:F6}, Diff: {Math.Abs(debugLongFrac - debugShortFrac):F6}");
								}
							}
							catch (Exception x)
							{
								var msg = x.Message;
								//Trace.WriteLine($"[RiskEngine] EXCEPTION: {x.Message}");
								//Trace.WriteLine($"[RiskEngine] StackTrace: {x.StackTrace}");
							}

							if (result != null && result.Success)
							{
								double longGross = 0;
								double shortGross = 0;
								double portfolioBeta = 0;

								foreach (var kvp in result.DollarWeights.OrderBy(k => k.Key))
								{
									var parts = kvp.Key.Split('_');
									var groupId = parts.Length > 1 ? parts[0] : "";
									var ticker = parts.Length > 1 ? parts[1] : kvp.Key;
									var fractionSize = kvp.Value;

									if (!double.IsNaN(fractionSize))
									{
										var stock = stocks.FirstOrDefault(s => s.Ticker == ticker && s.GroupId == groupId);

										if (stock != null && stock.Price > 0)
										{
											var dollarSize = Math.Abs(fractionSize) * cfg.PortfolioValue;
											var shareCount = dollarSize / stock.Price;

											var riskKey = groupId + "\t" + ticker;
											riskSizes[riskKey] = shareCount;

											if (fractionSize > 0)
												longGross += Math.Abs(fractionSize);
											else
												shortGross += Math.Abs(fractionSize);

											portfolioBeta += fractionSize * stock.Beta;

											//Trace.WriteLine($"[RiskEngine] POSITION: {ticker} fraction={fractionSize:F6}, dollars=${dollarSize:F2}, shares={shareCount:F2}, price=${stock.Price:F2}, beta={stock.Beta:F4}");
										}
										else
										{
											//Trace.WriteLine($"[RiskEngine] WARNING: Could not find stock or price for {ticker}");
										}
									}
									else
									{
										//Trace.WriteLine($"[RiskEngine] WARNING: Invalid fraction from risk engine for {ticker}");
									}
								}

								var longDollars = longGross * cfg.PortfolioValue;
								var shortDollars = shortGross * cfg.PortfolioValue;
								var imbalance = longDollars - shortDollars;
								var totalGross = longGross + shortGross;

								//Trace.WriteLine($"[RiskEngine] === Portfolio Summary ===");
								//Trace.WriteLine($"[RiskEngine] Regime: {regime}");
								//Trace.WriteLine($"[RiskEngine] Target Beta: {cfg.TargetPortfolioBeta:F2}");
								//Trace.WriteLine($"[RiskEngine] Long Gross:  {longGross:P2} (${longDollars:F2})");
								//Trace.WriteLine($"[RiskEngine] Short Gross: {shortGross:P2} (${shortDollars:F2})");
								//Trace.WriteLine($"[RiskEngine] Total Gross: {totalGross:P2}");
								//Trace.WriteLine($"[RiskEngine] Imbalance:   ${imbalance:F2}");
								//Trace.WriteLine($"[RiskEngine] Portfolio Beta: {portfolioBeta:F4}");
								//Trace.WriteLine($"[RiskEngine] Positions:   {result.DollarWeights.Count}");

								if (result.Metrics != null)
								{
									//Trace.WriteLine($"[RiskEngine] Actual Metrics - Beta: {result.Metrics.PortfolioBeta:F4}, LongGross: {result.Metrics.LongGross:P2}, ShortGross: {result.Metrics.ShortGross:P2}");
								}
							}
							else
							{
								//Trace.WriteLine($"[RiskEngine] Optimization FAILED or no result");
								if (result != null)
								{
									//Trace.WriteLine($"[RiskEngine] Status: {result.Status}, Message: {result.Message}");
								}
							}
						}
						else
						{
							//Trace.WriteLine($"[RiskEngine] SKIPPED - Need both longs ({lCnt}) AND shorts ({sCnt}) for market-neutral");
						}
					}
					else if (useBetaNeutralRiskEngine)
					{
						var rnd = new Random(7);
						var stocks = new List<BetaNeutralRiskEngine.Stock>();
						var lCnt = 0;
						var sCnt = 0;

						foreach (var member in portfolio) // per ticker
						{
							var ticker = member.Ticker;
							var signal = member.Sizes.ContainsKey(date) ? member.Sizes[date] : 0;
							if (_model.IsLiveMode && date < liveStartDate) signal = 0;
							if (filterTicker(ticker, date))
							{
								signal = 0;
							}
							var group = _model.Groups.Find(g => g.Id == member.GroupId);
							var side = group.Direction;
							var groupId = member.GroupId;
							var riskKey = groupId + "\t" + ticker;
							riskSizes[riskKey] = 0;

							if (signal != 0)
							{
								var symbol = _model.Symbols.FirstOrDefault(x => x.Ticker == ticker);
								var t = member.Ticker;
								var rawSector = symbol.Sector ?? "";
								var rawInd = symbol.Industry ?? "";
								var rawSubInd = symbol.SubIndustry ?? "";
								var s = rawSector.Length >= 2 ? rawSector.Substring(0, 2) : rawSector;
								var ind = rawInd.Length >= 4 ? rawInd.Substring(0, 4) : rawInd;
								var subInd = rawSubInd.Length >= 6 ? rawSubInd.Substring(0, 6) : rawSubInd;
								var k = date.ToString("yyyy-MM-dd");
								double beta;
								if (symbol.Beta.ContainsKey(k))
								{
									beta = symbol.Beta[k];
								}
								else if (symbol.Beta.Count > 0)
								{
									var lastBetaKey = symbol.Beta.Keys.Where(bk => string.Compare(bk, k) < 0).OrderByDescending(bk => bk).FirstOrDefault();
									beta = lastBetaKey != null ? symbol.Beta[lastBetaKey] : 1;
								}
								else
								{
									beta = 1;
								}
								var closes = bars[ticker].Close.Data.Take(dates.Count()).Select(x => x).ToList();
								double marketcap = symbol.Marketcap.ContainsKey(k) ? symbol.Marketcap[k] : 100000000;

								if (!double.IsNaN(closes[ii]))
								{
									var sec = new BetaNeutralRiskEngine.Stock
									{
										GroupId = groupId,
										Ticker = t,
										Sector = s,
										Industry = ind,
										SubIndustry = subInd,
										RequiredBook = side > 0 ? BetaNeutralRiskEngine.BookSide.Long : BetaNeutralRiskEngine.BookSide.Short,
										Beta = beta,
										Volatility = bars[ticker].AvgVolatility[ii],
										MarketCap = marketcap,
										Price = closes[ii],
										SignalScore = bars[ticker].Scores[ii],
										TargetPrice = side > 0 ? bars[ticker].UpperTargetPrices[ii] : bars[ticker].LowerTargetPrices[ii],
									};
									stocks.Add(sec);
									if (side > 0) lCnt++;
									else if (side < 0) sCnt++;
								}
							}
						}


						//Debug.WriteLine($"[CANDIDATES] date={date:yyyy-MM-dd} lCnt={lCnt} sCnt={sCnt} totalStocks={stocks.Count}");
						if (lCnt > 0 && sCnt > 0)
						{
							// ---- DECISION LOCK: check for a frozen decision before running optimizer ----
							var lockedDecisions = LoadLockedDecisions(date);

							if (lockedDecisions != null)
							{
								// Replay locked riskSizes — optimizer skipped entirely for this date.
								// GroupIds may have changed. Lock stores signed share counts: +=long, -=short.
								// Match by (ticker, groupDirection) so each ticker maps to the correct group.
								var tickerDirToRiskKey = new Dictionary<(string, int), string>();
								foreach (var k in riskSizes.Keys)
								{
									var ti = k.IndexOf('\t');
									if (ti < 0) continue;
									var kGroupId = k.Substring(0, ti);
									var kTicker = k.Substring(ti + 1);
									var kGroup = _model.Groups.FirstOrDefault(g => g.Id == kGroupId);
									if (kGroup != null) tickerDirToRiskKey[(kTicker, kGroup.Direction)] = k;
								}
								int matchCount = 0, missCount = 0;
								foreach (var kvp in lockedDecisions)
								{
									var ti = kvp.Key.IndexOf('\t');
									var lockTicker = ti >= 0 ? kvp.Key.Substring(ti + 1) : kvp.Key;
									// Sign encodes which group this position belongs to
									var lockDir = kvp.Value >= 0 ? 1 : -1;
									if (tickerDirToRiskKey.TryGetValue((lockTicker, lockDir), out var currentRiskKey))
									{
										riskSizes[currentRiskKey] = Math.Abs(kvp.Value); // unsigned magnitude only
										matchCount++;
									}
									else { missCount++; }
								}
								//Debug.WriteLine($"[DecisionLock] Replayed {matchCount}/{lockedDecisions.Count} positions for {date:yyyy-MM-dd} (missed={missCount})");
							}
							else
							{
								// No lock exists — run optimizer normally, then lock the result
								var riskGovernor = RiskGovernorConfig.Aggressive;
								riskGovernor.VixLevel = vixCloses.ContainsKey(date) ? vixCloses[date] : double.NaN;

								var cfg = new BetaNeutralRiskEngine.Config
								{
									PortfolioValue = portfolioValue,
									MaxPerNameFraction = _model.getConstraint("SingleNameCapAbs").Enable ? _model.getConstraint("SingleNameCapAbs").Value.Value / 100 : 0.10,  // 10% max per stock
									GrossTargetPerSideFraction = 0.40,   // 35% target per side

									UseRiskParity = _model.getConstraint("RiskParity").Enable,

									// Tolerances
									EnforceMarketNeutral = _model.getConstraint("MktNeutral").Enable,
									MaxMarketImbalanceFraction = 0.10,   // Allow ±5% imbalance

									UseAlphaWeighting = _model.getConstraint("AlphaWeighting") != null ? _model.getConstraint("AlphaWeighting").Enable : false,

									EnforceBetaNeutral = _model.getConstraint("BetaNeutral").Enable,
									BetaTolerance = 0.05,                // Allow ±0.05 beta

									EnforceVolatilityNeutral = _model.getConstraint("VolNeutral") == null ? false : _model.getConstraint("VolNeutral").Enable,  // Enable vol neutrality
									MaxVolatilityImbalanceFraction = 0.05,  // 5% tolerance

									EnforceSectorLimits = _model.getConstraint("SectorNet").Enable,
									MaxSectorFraction = _model.getConstraint("SectorNet").Enable ? _model.getConstraint("SectorNet").Value.Value / 100 : 0.12,  // 12% max per stock

									EnforceIndustryLimits = _model.getConstraint("IndustryNet").Enable,
									MaxIndustryFraction = _model.getConstraint("IndustryNet").Enable ? _model.getConstraint("IndustryNet").Value.Value / 100 : 0.12,  // 12% max per stock

									EnforceSubIndustryLimits = _model.getConstraint("SubIndNet").Enable,
									MaxSubIndustryFraction = _model.getConstraint("SubIndNet").Enable ? _model.getConstraint("SubIndNet").Value.Value / 100 : 0.12,  // 12% max per stock

									EnableRiskGovernor = true,
									RiskGovernor = riskGovernor,

								};

								BetaNeutralRiskEngine.OptimizeResult result = null;
								try
								{
									var bp = false;
									if (date.Year == 2025 && date.Month == 5 && date.Day == 30)
									{
										bp = true;
										//Trace.WriteLine($"[RiskEngine] Date: {date:yyyy-MM-dd}, Stocks: {stocks.Count}");
										stocks.ForEach(s =>
										{
											//Trace.WriteLine(s.ToString());
										});
									}

									if (useRiskOnOff)
									{
										var (minLongBeta, maxLongBeta, minShortBeta, maxShortBeta) = RegimeConfigFactory.GetBetaFilters(regime);


										//Trace.WriteLine($"[RiskEngine] ========== {date:yyyy-MM-dd} ==========");
										//Trace.WriteLine($"[RiskEngine] Detected Regime: {regime}");

										var filteredStocks = stocks.Where(s =>
										{
											if (s.RequiredBook == BetaNeutralRiskEngine.BookSide.Long)
											{
												return minLongBeta <= s.Beta && s.Beta <= maxLongBeta;
											}
											else
											{
												return minShortBeta <= s.Beta && s.Beta <= maxShortBeta;
											}
										}).ToList();

										result = BetaNeutralRiskEngine.PortfolioOptimizer.Optimize(filteredStocks, cfg);
									}
									else
									{
										result = BetaNeutralRiskEngine.PortfolioOptimizer.Optimize(stocks, cfg);
									}

									// DEBUG: Verify RiskEngine output
									var debugLongFrac = result.DollarWeights.Where(p => p.Value > 0).Sum(p => Math.Abs(p.Value));
									var debugShortFrac = result.DollarWeights.Where(p => p.Value < 0).Sum(p => Math.Abs(p.Value));
									var debugNonZero = result.DollarWeights.Count(p => p.Value != 0);
									//Debug.WriteLine($"[OPTIMIZER] date={date:yyyy-MM-dd} result={result != null} weights={result?.DollarWeights?.Count ?? 0} nonZero={debugNonZero} longFrac={debugLongFrac:F4} shortFrac={debugShortFrac:F4}");

									result.DollarWeights.ForEach(dw => {
										var f = dw.Key.Split('\t');
										var ticker = f[1];
										var weight = dw.Value;
										alphaWeightDiag.LogPosition(date, ticker, weight > 0 ? 1 : -1, bars[ticker].Scores[ii], weight, portfolioValue * weight);
									});
								}
								catch (Exception x)
								{
									var msg = x.Message;
									//Trace.WriteLine($"[RiskEngine] ERROR: {msg}");
								}

								if (date.Month == 2 && date.Day == 20 && date.Year == 2026)
									if (date.Month == 2 && date.Day == 20 && date.Year == 2026)
									{
										//Debug.WriteLine($"[2/20 DEBUG] stocks.Count={stocks.Count}, lCnt={lCnt}, sCnt={sCnt}");
										//Debug.WriteLine($"[2/20 DEBUG] result is null: {result == null}");
										if (result != null)
										{
											//Debug.WriteLine($"[2/20 DEBUG] result.DollarWeights.Count={result.DollarWeights.Count}");
										}
										var nanBetas = stocks.Count(s => double.IsNaN(s.Beta));
										var nanPrices = stocks.Count(s => double.IsNaN(s.Price) || s.Price <= 0);
										var nanVols = stocks.Count(s => double.IsNaN(s.Volatility) || s.Volatility <= 0);
										var zeroBetas = stocks.Count(s => s.Beta == 0);
										//Debug.WriteLine($"[2/20 DEBUG] NaN betas={nanBetas}, zero betas={zeroBetas}, NaN prices={nanPrices}, NaN vols={nanVols}");
										//Debug.WriteLine($"[2/20 DEBUG] Beta range: {stocks.Min(s => s.Beta):F4} to {stocks.Max(s => s.Beta):F4}");
										//Debug.WriteLine($"[2/20 DEBUG] Price range: {stocks.Min(s => s.Price):F2} to {stocks.Max(s => s.Price):F2}");
										//Debug.WriteLine($"[2/20 DEBUG] Vol range: {stocks.Min(s => s.Volatility):F4} to {stocks.Max(s => s.Volatility):F4}");
										//Debug.WriteLine($"[2/20 DEBUG] Regime={regime}, portfolioValue={portfolioValue:N0}");
										//Debug.WriteLine($"[2/20 DEBUG] cfg.PortfolioValue={cfg.PortfolioValue:N0}");
										//Debug.WriteLine($"[2/20 DEBUG] cfg.MaxPerNameFraction={cfg.MaxPerNameFraction:F4}");
										var sampleTicker = stocks.First().Ticker;
										var sampleSymbol = _model.Symbols.FirstOrDefault(x => x.Ticker == sampleTicker);
										if (sampleSymbol != null)
										{
											var betaKeys = sampleSymbol.Beta.Keys.OrderBy(x => x).ToList();
											//Debug.WriteLine($"[2/20 BETA] {sampleTicker} has {betaKeys.Count} beta entries");
											if (betaKeys.Count > 0)
											{
												//Debug.WriteLine($"[2/20 BETA] First: {betaKeys.First()}, Last: {betaKeys.Last()}");
												//Debug.WriteLine($"[2/20 BETA] Looking for key: {date.ToString("yyyy-MM-dd")}");
											}
										}
									}

								if (result != null)
								{
									// ============================================================
									// PHASE 1: Collect optimizer weights and build sector map
									// v7: Removed redundant PHASE 2 sector capping - the engine
									// now handles sector/industry/sub-industry limits internally
									// with proper ordering (step 5 + step 9 final enforcement).
									// Applying sector caps here after the engine could distort
									// the engine's carefully balanced market/beta neutrality.
									// ============================================================
									var positionWeights = new Dictionary<string, double>();   // riskKey -> signed fraction
									var positionStocks = new Dictionary<string, BetaNeutralRiskEngine.Stock>(); // riskKey -> stock
									var sectorGross = new Dictionary<string, double>();       // sector -> gross fraction

									foreach (var p in result.DollarWeights.OrderBy(k => k.Key))
									{
										var riskKey = p.Key;
										var f = riskKey.Split('\t');
										var ticker = (f.Length >= 2) ? f[1] : f[0];
										var fractionSize = p.Value;
										var stock = stocks.FirstOrDefault(s => s.Ticker == ticker);

										if (!double.IsNaN(fractionSize) && stock != null && stock.Price > 0)
										{
											positionWeights[riskKey] = fractionSize;
											positionStocks[riskKey] = stock;

											var sector = stock.Sector ?? "Unknown";
											if (!sectorGross.ContainsKey(sector))
												sectorGross[sector] = 0;
											sectorGross[sector] += Math.Abs(fractionSize);
										}
									}

									// ============================================================
									// ============================================================
									// PHASE 1b: Enforce signed-net sector limit
									// The optimizer enforces MaxSectorFraction as GROSS.
									// SectorNet constraint targets signed NET (long-short).
									// Use RequiredBook for sign -- DollarWeights may be unsigned.
									// ============================================================
									if (cfg.EnforceSectorLimits && cfg.MaxSectorFraction > 0)
									{
										// Pass 1: compute signed-net per sector using RequiredBook
										var sectorNet1b = new Dictionary<string, double>();
										foreach (var kv1b in positionWeights)
										{
											if (!positionStocks.ContainsKey(kv1b.Key)) continue;
											var stk1b = positionStocks[kv1b.Key];
											var sec1b = stk1b.Sector ?? "";
											var sign1b = stk1b.RequiredBook == BetaNeutralRiskEngine.BookSide.Long ? 1.0 : -1.0;
											if (!sectorNet1b.ContainsKey(sec1b)) sectorNet1b[sec1b] = 0;
											sectorNet1b[sec1b] += sign1b * Math.Abs(kv1b.Value);
										}
										// Pass 2: scale down dominant side in any violating sector
										foreach (var sec1b in sectorNet1b.Keys.ToList())
										{
											var net1b = sectorNet1b[sec1b];
											if (Math.Abs(net1b) <= cfg.MaxSectorFraction) continue;
											var sf1b = cfg.MaxSectorFraction / Math.Abs(net1b);
											foreach (var rk1b in positionWeights.Keys.ToList())
											{
												if (!positionStocks.ContainsKey(rk1b)) continue;
												var stk1bR = positionStocks[rk1b];
												if ((stk1bR.Sector ?? "") != sec1b) continue;
												var isDominant = net1b > 0
													? stk1bR.RequiredBook == BetaNeutralRiskEngine.BookSide.Long
													: stk1bR.RequiredBook == BetaNeutralRiskEngine.BookSide.Short;
												if (isDominant)
													positionWeights[rk1b] *= sf1b;
											}
										}
									}

									// ============================================================
									// PHASE 1c: Save final sector fractions so Hedge Fund App
									// can display them directly (bypasses price mismatch).
									// ============================================================
									if (cfg.EnforceSectorLimits)
									{
										var sectorFinal = new Dictionary<string, double>();
										foreach (var kv1c in positionWeights)
										{
											if (!positionStocks.ContainsKey(kv1c.Key)) continue;
											var stk1c = positionStocks[kv1c.Key];
											var sec1c = stk1c.Sector ?? "";
											var sign1c = stk1c.RequiredBook == BetaNeutralRiskEngine.BookSide.Long ? 1.0 : -1.0;
											if (!sectorFinal.ContainsKey(sec1c)) sectorFinal[sec1c] = 0;
											sectorFinal[sec1c] += sign1c * Math.Abs(kv1c.Value);
										}
										var sb1c = new System.Text.StringBuilder();
										foreach (var kvf in sectorFinal)
											sb1c.AppendLine(kvf.Key + "," + kvf.Value.ToString("R"));
										MainView.SaveUserData(@"portfolios\sectorFracs\" + _model.Name + @"\" + date.ToString("yyyy-MM-dd"), sb1c.ToString());
									}

									// PHASE 2: Convert final weights to share counts
									// ============================================================
									double longGross = 0;
									double shortGross = 0;
									double portfolioBeta = 0;

									foreach (var kvp in positionWeights.OrderBy(k => k.Key))
									{
										var riskKey = kvp.Key;
										var fractionSize = kvp.Value;
										var stock = positionStocks[riskKey];

										var dollarSize = Math.Abs(fractionSize) * cfg.PortfolioValue;
										var shareCount = dollarSize / stock.Price;

										riskSizes[riskKey] = shareCount;

										if (fractionSize > 0)
											longGross += Math.Abs(fractionSize);
										else
											shortGross += Math.Abs(fractionSize);

										portfolioBeta += fractionSize * stock.Beta;

										//Trace.WriteLine($"[RiskEngine] {date} {stock.Ticker}: fraction={fractionSize:F6}, " +
										//$"dollars=${dollarSize:F2}, shares={shareCount:F2}, " +
										//$"price=${stock.Price:F2}, beta={stock.Beta:F4}");
									}

									// ============================================================
									// DECISION LOCK SAVE — immediately after riskSizes is populated
									// ============================================================
									if (positionWeights.Count > 0)
									{
										var signedSizes = positionWeights.ToDictionary(
											kvp => kvp.Key,
											kvp => {
												var stock = positionStocks[kvp.Key];
												var dollarSize = Math.Abs(kvp.Value) * cfg.PortfolioValue;
												var shareCount = dollarSize / stock.Price;
												return kvp.Value > 0 ? shareCount : -shareCount;
											});
										SaveLockedDecisions(date, signedSizes, stocks);
									}

									// ============================================================
									// PHASE 3: Report portfolio characteristics
									// ============================================================
									var longDollars = longGross * cfg.PortfolioValue;
									var shortDollars = shortGross * cfg.PortfolioValue;
									var imbalance = longDollars - shortDollars;
									var totalGross = longGross + shortGross;

									//Trace.WriteLine($"[RiskEngine] === Portfolio Summary {date:yyyy-MM-dd} ===");
									//Trace.WriteLine($"[RiskEngine] Long Gross:  {longGross:P2} (${longDollars:N0})");
									//Trace.WriteLine($"[RiskEngine] Short Gross: {shortGross:P2} (${shortDollars:N0})");
									//Trace.WriteLine($"[RiskEngine] Portfolio Beta: {portfolioBeta:F6}");

									foreach (var kvp in sectorGross.OrderByDescending(x => x.Value))
									{
										var flag = kvp.Value > cfg.MaxSectorFraction ? " *** VIOLATION ***" : "";
										//Trace.WriteLine($"[RiskEngine] Sector '{kvp.Key}': {kvp.Value:P2}{flag}");
									}

									if (Math.Abs(imbalance) > 10_000)
									{
										//Trace.WriteLine($"[RiskEngine] WARNING: Market imbalance ${imbalance:N0} exceeds tolerance!");
									}
									if (Math.Abs(portfolioBeta) > 0.05)
									{
										//Trace.WriteLine($"[RiskEngine] WARNING: Portfolio beta {portfolioBeta:F6} exceeds tolerance!");
									}
									// Save last good risk sizes only if optimizer produced weights
									if (positionWeights.Count > 0)
									{
										//lastGoodRiskSizes = new Dictionary<string, double>(riskSizes);
									}
								}
								else
								{
									//Trace.WriteLine($"[RiskEngine] No result returned from optimizer");
								}
							} // end: no lock — optimizer ran
						}
					}
					else if (useRiskEngineMN3)
					{
						//RiskEngineMN3.Run();
						var rnd = new Random(7);
						var stocks = new List<RiskEngineMN3.Stock>();
						var lCnt = 0;
						var sCnt = 0;

						foreach (var member in portfolio) // per ticker
						{
							var ticker = member.Ticker;
							var signal = member.Sizes.ContainsKey(date) ? member.Sizes[date] : 0;
							if (filterTicker(ticker, date)) signal = 0;
							var group = _model.Groups.Find(g => g.Id == member.GroupId);
							var side = group.Direction;
							var groupId = member.GroupId;
							var riskKey = groupId + "\t" + ticker;
							riskSizes[riskKey] = 0;

							if (signal != 0)
							{
								var symbol = _model.Symbols.FirstOrDefault(x => x.Ticker == ticker);
								var t = member.Ticker;
								var rawSector = symbol.Sector ?? "";
								var rawInd = symbol.Industry ?? "";
								var rawSubInd = symbol.SubIndustry ?? "";
								var s = rawSector.Length >= 2 ? rawSector.Substring(0, 2) : rawSector;
								var ind = rawInd.Length >= 4 ? rawInd.Substring(0, 4) : rawInd;
								var subInd = rawSubInd.Length >= 6 ? rawSubInd.Substring(0, 6) : rawSubInd;
								var k = date.ToString("yyyy-MM-dd");
								double beta;
								if (symbol.Beta.ContainsKey(k))
								{
									beta = symbol.Beta[k];
								}
								else if (symbol.Beta.Count > 0)
								{
									var lastBetaKey = symbol.Beta.Keys.Where(bk => string.Compare(bk, k) < 0).OrderByDescending(bk => bk).FirstOrDefault();
									beta = lastBetaKey != null ? symbol.Beta[lastBetaKey] : 1;
								}
								else
								{
									beta = 1;
								}
								var closes = bars[ticker].Close.Data.Take(dates.Count()).Select(x => x).ToList();
								double marketcap = symbol.Marketcap.ContainsKey(k) ? symbol.Marketcap[k] : 100000000;

								if (!double.IsNaN(closes[ii]))
								{
									var sec = new RiskEngineMN3.Stock
									{
										GroupId = groupId,
										Ticker = t,
										Sector = s,
										Industry = ind,
										SubIndustry = subInd,
										RequiredBook = side > 0 ? RiskEngineMN3.BookSide.Long : RiskEngineMN3.BookSide.Short,
										Beta = beta,
										MarketCap = marketcap,
										Price = closes[ii],
									};
									stocks.Add(sec);
									if (side > 0) lCnt++;
									else if (side < 0) sCnt++;
								}
							}
						}

						if (lCnt > 0 && sCnt > 0)
						{
							var cfg = new RiskEngineMN3.Config
							{
								PortfolioValue = portfolioValue,
								MaxPerNameFraction = 0.10,           // 10% max per stock
								GrossTargetPerSideFraction = 0.50,   // 50% target per side

								// Tolerances
								EnforceMarketNeutral = _model.getConstraint("MktNeutral").Enable,
								MaxMarketImbalanceFraction = 0.05,   // Allow ±5% imbalance

								EnforceBetaNeutral = _model.getConstraint("BetaNeutral").Enable,
								BetaTolerance = 0.05,                // Allow ±0.05 beta
							};

							RiskEngineMN3.OptimizeResult result = null;
							try
							{
								var bp = false;
								if (date.Year == 2025 && date.Month == 5 && date.Day == 30)
								{
									bp = true;
									//Trace.WriteLine($"[RiskEngine] Date: {date:yyyy-MM-dd}, Stocks: {stocks.Count}");
									stocks.ForEach(s =>
									{
										//Trace.WriteLine(s.ToString());
									});
								}

								result = RiskEngineMN3.PortfolioOptimizer.Optimize(stocks, cfg, _model.Constraints);

								// DEBUG: Verify RiskEngine output
								var debugLongFrac = result.Positions.Where(p => p.Shares > 0).Sum(p => Math.Abs(p.Shares));
								var debugShortFrac = result.Positions.Where(p => p.Shares < 0).Sum(p => Math.Abs(p.Shares));
								//Debug.WriteLine($"[DEBUG RiskEngine Output] Long fraction: {debugLongFrac:F6}, Short fraction: {debugShortFrac:F6}, Diff: {Math.Abs(debugLongFrac - debugShortFrac):F6}");

							}
							catch (Exception x)
							{
								var msg = x.Message;
								//Trace.WriteLine($"[RiskEngine] ERROR: {msg}");
							}

							if (result != null)
							{
								// Calculate portfolio metrics for verification
								double longGross = 0;
								double shortGross = 0;
								double portfolioBeta = 0;

								result.Positions.ForEach(p =>
								{
									var groupId = p.GroupId;
									var ticker = p.Ticker;
									var fractionSize = p.Shares;  // Signed fraction (positive=long, negative=short)

									// Find the stock to get its price and beta
									var stock = stocks.FirstOrDefault(s => s.Ticker == ticker && s.GroupId == groupId);

									if (stock != null && stock.Price > 0)
									{
										// Convert fraction to dollar amount, then to share count
										var dollarSize = Math.Abs(fractionSize) * cfg.PortfolioValue;
										var shareCount = dollarSize / stock.Price;

										var riskKey = groupId + "\t" + ticker;
										riskSizes[riskKey] = shareCount;  // Store share count

										// Accumulate metrics for reporting
										if (fractionSize > 0)
											longGross += Math.Abs(fractionSize);
										else
											shortGross += Math.Abs(fractionSize);

										portfolioBeta += fractionSize * stock.Beta;

										//Trace.WriteLine($"[RiskEngine] {date} {ticker}: fraction={fractionSize:F6}, " +
										//$"dollars=${dollarSize:F2}, shares={shareCount:F2}, " +
										//$"price=${stock.Price:F2}, beta={stock.Beta:F4}");
									}
									else
									{
										//Trace.WriteLine($"[RiskEngine] WARNING: Could not find stock or price for {ticker}");
									}
								});

								// Report portfolio characteristics
								var longDollars = longGross * cfg.PortfolioValue;
								var shortDollars = shortGross * cfg.PortfolioValue;
								var imbalance = longDollars - shortDollars;
								var totalGross = longGross + shortGross;

								//Trace.WriteLine($"[RiskEngine] === Portfolio Summary ===");
								//Trace.WriteLine($"[RiskEngine] Long Gross:  {longGross:F4} (${longDollars:F2})");
								//Trace.WriteLine($"[RiskEngine] Short Gross: {shortGross:F4} (${shortDollars:F2})");
								//Trace.WriteLine($"[RiskEngine] Total Gross: {totalGross:F4}");
								//Trace.WriteLine($"[RiskEngine] Imbalance:   ${imbalance:F2} (target: ±$10,000)");
								//Trace.WriteLine($"[RiskEngine] Portfolio Beta: {portfolioBeta:F6} (target: ±0.05)");
								//Trace.WriteLine($"[RiskEngine] Positions:   {result.Positions.Count}");

								// Warnings if constraints are violated (shouldn't happen with optimizer)
								if (Math.Abs(imbalance) > 10_000)
								{
									//Trace.WriteLine($"[RiskEngine] WARNING: Market imbalance ${imbalance:F2} exceeds tolerance!");
								}
								if (Math.Abs(portfolioBeta) > 0.05)
								{
									//Trace.WriteLine($"[RiskEngine] WARNING: Portfolio beta {portfolioBeta:F6} exceeds tolerance!");
								}
								// Save last good risk sizes
								//lastGoodRiskSizes = new Dictionary<string, double>(riskSizes);
							}
							else
							{
								// Optimizer returned empty weights - carry forward last good sizes
							}
						}
					}

					if (useSharpeThrottle)
					{
						var tickerToUseForDates = portfolio.Select(m => m.Ticker).FirstOrDefault(t => bars.ContainsKey(t));
						var dailyDates = dailyBars[tickerToUseForDates].Time.Data.Where(dt => { var date1 = DateTime.FromOADate(dt); return prvDate < date1 && date1 < date; }).ToList();
						var heldSharesOvernight = new Dictionary<string, double>();
						foreach (var otKvp in openTrades.OrderBy(k => k.Key))
						{
							var otTrade = otKvp.Value;
							var otTicker = otTrade.Ticker;
							heldSharesOvernight[otTicker] = otTrade.Direction * otTrade.Investment2;
						}
						var bp = false;
						dailyDates.ForEach(date1 =>
						{
							var todaysCloses = new Dictionary<string, double>();
							foreach (var hsKvp in heldSharesOvernight.OrderBy(k => k.Key))
							{
								var hsTicker = hsKvp.Key;
								var closes = dailyBars[hsTicker].Close.Data;
								var idx = dailyBars[hsTicker].Time.Data.FindIndex(dt => dt == date1);
								if (idx >= 0)
								{
									todaysCloses[hsTicker] = dailyBars[hsTicker].Close[idx];
								}
								else
								{
									bp = true;
								}
							}
							throttle.UpdateNavDaily(DateTime.FromOADate(date1), heldSharesOvernight, todaysCloses);
						});
						var sizes = new Dictionary<string, double>();
						var riskSizeKeys = new Dictionary<string, string>();
						foreach (var rsKvp in riskSizes.OrderBy(k => k.Key))
						{
							var rsKey = rsKvp.Key;
							var rsF = rsKey.Split('\t');
							var rsTicker = rsF[1];
							var rsSize = rsKvp.Value;
							if (rsSize != 0)
							{
								var rsGroupId = rsF[0];
								var rsGroup = _model.Groups.FirstOrDefault(g => g.Id == rsGroupId);
								var rsDir = rsGroup.Direction;
								sizes[rsTicker] = rsDir * rsSize;
								riskSizeKeys[rsTicker] = rsKey;
							}
						}
						var throttleSizes = throttle.ScaleOnRebalance(sizes);

						var change = false;
						foreach (var kvp in sizes.OrderBy(k => k.Key))
						{
							var sKey = kvp.Key;
							var sVal = kvp.Value;
							if (throttleSizes[sKey] != sVal)
							{
								change = true;
							}
						}

						if (change)
						{
							foreach (var tsKvp in throttleSizes.OrderBy(k => k.Key))
							{
								var tsTicker = tsKvp.Key;
								var tsKey = riskSizeKeys[tsTicker];
								riskSizes[tsKey] = throttleSizes.ContainsKey(tsTicker) ? Math.Abs(throttleSizes[tsTicker]) : 0.0;
							}
						}
					}

					foreach (var member in portfolio) // per ticker
					{
						var ticker = member.Ticker;

						var groupId = member.GroupId;
						var group = _model.Groups.FirstOrDefault(g => g.Id == groupId);
						var side = group.Direction;

						//if (!(useHedge || useBetaHedge) || (ticker != hedgeTicker && ticker != marketTicker))
						{
							var signal = member.Sizes.ContainsKey(date) ? member.Sizes[date] : 0;
							if (_model.IsLiveMode && date < liveStartDate) signal = 0;

							var symbol = getSymbol(ticker);

							useYield = atm.isYieldTicker(ticker);

							var avgVolatility = bars[ticker].AvgVolatility[ii];

							var resetLong = resetPortfolio.ContainsKey(ticker) && resetPortfolio[ticker] == 1;
							var resetShort = resetPortfolio.ContainsKey(ticker) && resetPortfolio[ticker] == -1;
							var okToEnterLong = !resetLong && !double.IsNaN(avgVolatility);
							var okToEnterShort = !resetShort && !double.IsNaN(avgVolatility);

							var upSize = side > 0 && useRiskEngine ? signal : Math.Min(0.02, signal);
							var dnSize = side < 0 && useRiskEngine ? signal : Math.Min(0.02, signal);

							if (side > 0 && upSize == 0 && resetLong) resetPortfolio[ticker] = 0;
							if (side < 0 && dnSize == 0 && resetShort) resetPortfolio[ticker] = 0;

							var upSig = upSize != 0 && okToEnterLong;
							var dnSig = dnSize != 0 && okToEnterShort;

							var close = bars[ticker].Close[ii];

							var openKey = groupId + "\t" + ticker;

							var riskKey = groupId + "\t" + ticker;


							var direction = openTrades.ContainsKey(openKey) ? openTrades[openKey].Direction : 0;
							var noSize = useRiskEngine && riskSizes[riskKey] == 0;
							var mmScale = 1.0;

							// BUG FIX: Process dividends using corrected helper method
							// Uses actual shares at ex-date, prevents duplicates, clear direction handling
							if (openTrades.ContainsKey(openKey))
							{
								var trade = openTrades[openKey];
								ProcessTradeDividends(trade, symbol, (_model.IsLiveMode ? liveStartDate : prvDate), date, dividends, processedDividends);
							}

							//if (openTrades.ContainsKey(openKey))
							//{
							//    var trade = openTrades[openKey];
							//    var size = trade.Investment1;
							//    var divs = symbol.Dividends;
							//    divs.ForEach(kvp =>
							//    {
							//        string key = kvp.Key;
							//        string[] fields = key.Split(':');
							//        DateTime exDate = DateTime.ParseExact(fields[0], "yyyy-MM-dd", null);
							//        if (exDate.CompareTo(prvDate) >= 0 && exDate.CompareTo(date) <= 0)
							//        {
							//            DateTime payOutDate = DateTime.ParseExact(fields[1], "yyyy-MM-dd", null);
							//            double dollar = size * kvp.Value;
							//            if (trade.Direction < 0) dollar = -dollar;
							//            dividends[payOutDate] = (dividends.ContainsKey(payOutDate)) ? dividends[payOutDate] + dollar : dollar;
							//        }
							//    });
							//}

							// exit
							if (date.Month == 2 && date.Day == 20 && date.Year == 2026 && direction != 0 && ticker == openTrades.Keys.FirstOrDefault()?.Split('\t').Last())
							{
								//Debug.WriteLine($"[2/20 EXIT] {ticker} dir={direction} signal={signal} upSig={upSig} dnSig={dnSig} noSize={noSize} riskSize={riskSizes[riskKey]}");
							}
							if (direction == 1 && (!upSig || noSize) || direction == -1 && (!dnSig || noSize))
							{
								var price = bars[ticker].TradePrice[ii];
								var trade = openTrades[openKey];
								trade.CloseDateTime = date;
								trade.ExitPrice = price;
								trade.SetOpen(false);
								openTrades.Remove(openKey);
								trade.Shares[date] = 0;
								trade.Closes[date] = price;
								trade.TradeTypes[date] = TradeType.PExhaustion;
								closeTrades.Add(trade);

								if (trade.Direction < 0)
								{
									borrowingCostEngine.CloseShortPosition(ticker, date);
								}

								if (useCost)
								{
									try
									{
										var shares = Math.Abs(trade.Shares.ContainsKey(dates[di - 1]) ? trade.Shares[dates[di - 1]] : 0.0);
										var (execComm, execImpact, cost) = ExecutionCostHelper.ComputeExecutionCost(price, shares, portfolioValue, bars[ticker].AvgVolatility[ii], bars[ticker].AvgVolume[ii]);
										if (double.IsNaN(cost))
										{
											cost = 0;
										}
										portfolioValue -= cost;
										trade.Cost += cost;
										if (trade.ExecutionCosts.ContainsKey(date))
										{ var (pc, pi) = trade.ExecutionCosts[date]; trade.ExecutionCosts[date] = (pc + execComm, pi + execImpact); }
										else trade.ExecutionCosts[date] = (execComm, execImpact);
									}
									catch (Exception x)
									{
									}
								}
							}

							// long entry
							var okToBuy = group.Direction >= 0; // || ticker == hedgeTicker;
																// Price floor and last-bar validity check
							var _entryPrice = bars[ticker].TradePrice[ii];
							var _lastValidBar = bars[ticker].Close.Data.Take(ii + 1).LastOrDefault(x => !double.IsNaN(x));
							var _stockInactive = (ii > 0 && double.IsNaN(bars[ticker].Close[ii])) || (!double.IsNaN(_entryPrice) && _entryPrice < 5.0);
							if (coolDown == 0 && (!useRiskEngine || riskSizes[riskKey] != 0) && riskValue > 0 && direction <= 0 && upSig && okToBuy && !_stockInactive)
							{
								var price = bars[ticker].TradePrice[ii];
								if (!double.IsNaN(price))
								{
									var id = new tmsg.IdeaIdType();
									id.BloombergIdSpecified = false;
									id.ThirdPartyId = Guid.NewGuid().ToString();
									var shares = Math.Abs(useRiskEngine ? riskSizes[riskKey] * mmScale : getShareAmount((riskValue * upSize) / close, close));
									var trade = new Trade(groupId, ticker, id, 1, shares, date, price, default(DateTime), double.NaN, true);
									int sectorNumber;
									int.TryParse(symbol.Sector, out sectorNumber);
									trade.Sector = _portfolio1.GetSectorLabel(sectorNumber);
									openTrades[openKey] = trade;
									trades.Add(trade);
									trade.Shares[date] = shares;
									trade.TradeTypes[date] = TradeType.NewTrend;

									if (useCost)
									{
										try
										{
											var (execComm, execImpact, cost) = ExecutionCostHelper.ComputeExecutionCost(price, shares, portfolioValue, bars[ticker].AvgVolatility[ii], bars[ticker].AvgVolume[ii]);
											if (double.IsNaN(cost))
											{
												cost = 0;
											}
											portfolioValue -= cost;
											trade.Cost += cost;
											if (trade.ExecutionCosts.ContainsKey(date))
											{ var (pc, pi) = trade.ExecutionCosts[date]; trade.ExecutionCosts[date] = (pc + execComm, pi + execImpact); }
											else trade.ExecutionCosts[date] = (execComm, execImpact);
										}
										catch (Exception x)
										{
										}
									}
								}
							}

							// short entry
							var okToShort = group.Direction <= 0;// || ticker == hedgeTicker;
							if (coolDown == 0 && (!useRiskEngine || riskSizes[riskKey] != 0) && riskValue > 0 && direction >= 0 && dnSig && okToShort && !_stockInactive)
							{
								var price = bars[ticker].TradePrice[ii];
								if (!double.IsNaN(price))
								{
									var id = new tmsg.IdeaIdType();
									id.BloombergIdSpecified = false;
									id.ThirdPartyId = Guid.NewGuid().ToString();
									var shares = Math.Abs(useRiskEngine ? riskSizes[riskKey] * mmScale : getShareAmount((riskValue * dnSize) / close, close));
									var trade = new Trade(groupId, ticker, id, -1, shares, date, price, default(DateTime), double.NaN, true);
									int sectorNumber;
									int.TryParse(symbol.Sector, out sectorNumber);
									trade.Sector = _portfolio1.GetSectorLabel(sectorNumber);
									openTrades[openKey] = trade;
									trades.Add(trade);
									trade.Shares[date] = shares;
									trade.TradeTypes[date] = TradeType.NewTrend;

									borrowingCostEngine.OpenShortPosition(ticker, date, (decimal)-shares, (decimal)price);

									if (useCost)
									{
										try
										{
											var (execComm, execImpact, cost) = ExecutionCostHelper.ComputeExecutionCost(price, shares, portfolioValue, bars[ticker].AvgVolatility[ii], bars[ticker].AvgVolume[ii]);
											if (double.IsNaN(cost))
											{
												cost = 0;
											}
											portfolioValue -= cost;
											trade.Cost += cost;
											if (trade.ExecutionCosts.ContainsKey(date))
											{ var (pc, pi) = trade.ExecutionCosts[date]; trade.ExecutionCosts[date] = (pc + execComm, pi + execImpact); }
											else trade.ExecutionCosts[date] = (execComm, execImpact);
										}
										catch (Exception x)
										{
										}
									}
								}
							}

							if (openTrades.ContainsKey(openKey))
							{
								var trade = openTrades[openKey];

								//var curUpSize = upSize;
								//var curDnSize = dnSize;
								//var prvUpSize = trade.Direction > 0 && trade.Size.ContainsKey(dates[di - 1]) ? trade.Size[dates[di - 1]] : 0.0;
								//var prvDnSize = trade.Direction < 0 && trade.Size.ContainsKey(dates[di - 1]) ? trade.Size[dates[di - 1]] : 0.0;

								var averagePrice = trade.AvgPrice.ContainsKey(dates[di - 1]) ? trade.AvgPrice[dates[di - 1]] : trade.EntryPrice;
								trade.AvgPrice[date] = averagePrice;

								trade.Scores[date] = bars[ticker].Scores[ii];

								var prvUpShares = Math.Abs(trade.Direction > 0 && trade.Shares.ContainsKey(dates[di - 1]) ? trade.Shares[dates[di - 1]] : 0.0);
								var prvDnShares = Math.Abs(trade.Direction < 0 && trade.Shares.ContainsKey(dates[di - 1]) ? trade.Shares[dates[di - 1]] : 0.0);

								var curUpShares = Math.Abs(trade.Direction > 0 ? (useRiskEngine ? riskSizes[riskKey] * mmScale : getShareAmount(riskValue * upSize / close, close)) : 0.0);
								var curDnShares = Math.Abs(trade.Direction < 0 ? (useRiskEngine ? riskSizes[riskKey] * mmScale : getShareAmount(riskValue * dnSize / close, close)) : 0.0);

								var price = bars[ticker].Close[ii];

								// NOISE TRADE SUPPRESSION
								if (trade.Direction > 0 && prvUpShares > 0 && curUpShares > 0)
								{
									var deltaPct = Math.Abs(curUpShares - prvUpShares) / prvUpShares;
									var deltaNav = Math.Abs(curUpShares - prvUpShares) * price / portfolioValue;
									if (deltaPct < NoiseDeadZonePct && deltaNav < NoiseDeadZoneNavBps)
									{
										curUpShares = prvUpShares;
										noiseTradesBlocked++;
									}
								}
								if (trade.Direction < 0 && prvDnShares > 0 && curDnShares > 0)
								{
									var deltaPct = Math.Abs(curDnShares - prvDnShares) / prvDnShares;
									var deltaNav = Math.Abs(curDnShares - prvDnShares) * price / portfolioValue;
									if (deltaPct < NoiseDeadZonePct && deltaNav < NoiseDeadZoneNavBps)
									{
										curDnShares = prvDnShares;
										noiseTradesBlocked++;
									}
								}

								if (trade.Direction > 0)
								{
									if (prvUpShares > 0 && curUpShares > prvUpShares) // add
									{
										var currentNumberOfShares = trade.Investment2;
										var numberOfSharesToAdd = curUpShares - prvUpShares;
										var totalNumberOfShares = currentNumberOfShares + numberOfSharesToAdd;

										trade.Investment2 += numberOfSharesToAdd;
										trade.AvgPrice[date] = (averagePrice * currentNumberOfShares + price * numberOfSharesToAdd) / totalNumberOfShares;

										trade.Scores[date] = bars[ticker].Scores[ii];

										if (useCost)
										{
											try
											{
												var shares = numberOfSharesToAdd;
												var (execComm, execImpact, cost) = ExecutionCostHelper.ComputeExecutionCost(price, shares, portfolioValue, bars[ticker].AvgVolatility[ii], bars[ticker].AvgVolume[ii]);
												if (double.IsNaN(cost))
												{
													cost = 0;
												}
												portfolioValue -= cost;
												trade.Cost += cost;
												if (trade.ExecutionCosts.ContainsKey(date))
												{ var (pc, pi) = trade.ExecutionCosts[date]; trade.ExecutionCosts[date] = (pc + execComm, pi + execImpact); }
												else trade.ExecutionCosts[date] = (execComm, execImpact);
											}
											catch (Exception x)
											{
											}
										}
									}
									if (prvUpShares > 0 && curUpShares < prvUpShares) // reduce
									{
										var numberOfSharesToReduce = prvUpShares - curUpShares;
										trade.Investment2 -= numberOfSharesToReduce;

										if (useCost)
										{
											try
											{
												var shares = numberOfSharesToReduce;
												var (execComm, execImpact, cost) = ExecutionCostHelper.ComputeExecutionCost(price, shares, portfolioValue, bars[ticker].AvgVolatility[ii], bars[ticker].AvgVolume[ii]);
												if (double.IsNaN(cost))
												{
													cost = 0;
												}
												portfolioValue -= cost;
												trade.Cost += cost;
												if (trade.ExecutionCosts.ContainsKey(date))
												{ var (pc, pi) = trade.ExecutionCosts[date]; trade.ExecutionCosts[date] = (pc + execComm, pi + execImpact); }
												else trade.ExecutionCosts[date] = (execComm, execImpact);
											}
											catch (Exception x)
											{
											}
										}
									}
									//trade.Size[date] = curUpSize;
									trade.Shares[date] = trade.Investment2;
									trade.TradeTypes[date] = (prvUpShares == 0 && trade.Investment2 != 0)
		? TradeType.NewTrend
		: TradeType.Rebalance;
									trade.Closes[date] = price;
									//trade.TradeTypes[date] = signal.TradeType;
								}
								if (trade.Direction < 0)
								{
									if (prvDnShares > 0 && curDnShares > prvDnShares) // add
									{
										var currentNumberOfShares = trade.Investment2;
										var numberOfSharesToAdd = curDnShares - prvDnShares;
										var totalNumberOfShares = currentNumberOfShares + numberOfSharesToAdd;

										borrowingCostEngine.UpdateShortPosition(ticker, date, (decimal)-totalNumberOfShares, (decimal)price);

										trade.Investment2 += numberOfSharesToAdd;
										trade.AvgPrice[date] = (averagePrice * currentNumberOfShares + price * numberOfSharesToAdd) / totalNumberOfShares;

										trade.Scores[date] = bars[ticker].Scores[ii];

										if (useCost)
										{
											try
											{
												var shares = numberOfSharesToAdd;
												var (execComm, execImpact, cost) = ExecutionCostHelper.ComputeExecutionCost(price, shares, portfolioValue, bars[ticker].AvgVolatility[ii], bars[ticker].AvgVolume[ii]);
												if (double.IsNaN(cost))
												{
													cost = 0;
												}
												portfolioValue -= cost;
												trade.Cost += cost;
												if (trade.ExecutionCosts.ContainsKey(date))
												{ var (pc, pi) = trade.ExecutionCosts[date]; trade.ExecutionCosts[date] = (pc + execComm, pi + execImpact); }
												else trade.ExecutionCosts[date] = (execComm, execImpact);
											}
											catch (Exception x)
											{
											}
										}
									}
									if (prvDnShares > 0 && curDnShares < prvDnShares) // reduce
									{

										var currentNumberOfShares = trade.Investment2;
										var numberOfSharesToReduce = prvDnShares - curDnShares;
										var totalNumberOfShares = currentNumberOfShares - numberOfSharesToReduce;

										borrowingCostEngine.UpdateShortPosition(ticker, date, (decimal)-totalNumberOfShares, (decimal)price);

										trade.Investment2 -= numberOfSharesToReduce;

										if (useCost)
										{
											try
											{
												var shares = numberOfSharesToReduce;
												var (execComm, execImpact, cost) = ExecutionCostHelper.ComputeExecutionCost(price, shares, portfolioValue, bars[ticker].AvgVolatility[ii], bars[ticker].AvgVolume[ii]);
												if (double.IsNaN(cost))
												{
													cost = 0;
												}
												portfolioValue -= cost;
												trade.Cost += cost;
												if (trade.ExecutionCosts.ContainsKey(date))
												{ var (pc, pi) = trade.ExecutionCosts[date]; trade.ExecutionCosts[date] = (pc + execComm, pi + execImpact); }
												else trade.ExecutionCosts[date] = (execComm, execImpact);
											}
											catch (Exception x)
											{
											}
										}
									}
									//trade.Size[date] = curDnSize;
									trade.Shares[date] = trade.Investment2;
									trade.TradeTypes[date] = (prvDnShares == 0 && trade.Investment2 != 0)
		? TradeType.NewTrend
		: TradeType.Rebalance;
									trade.Closes[date] = price;
									//trade.TradeTypes[date] = signal.TradeType;
								}

								// check for no more than 10% of risk value
								//var dollarsInSymbol = trade.Investment2 * price;
								//                     if (dollarsInSymbol > 0.1 * riskValue)
								//                     {
								//                         dollarsInSymbol = 0.1 * riskValue;
								//                         var newInvestment = dollarsInSymbol / price;
								//                         var numberOfSharesToReduce = trade.Investment2 - newInvestment;
								//                         trade.Investment2 -= numberOfSharesToReduce;
								//                         trade.ClosedProfit += numberOfSharesToReduce * (price - trade.AvgPrice[date]);
								//                         trade.Size[date] = dollarsInSymbol / riskValue;
								//                         trade.Shares[date] = newInvestment;
								//                     }

							}
						}
					}

					// this loop is for print out to output window only
					//foreach (var trade in closeTrades)
					//{
					//    var ticker = trade.Ticker;
					//    trade.Investment2 = 0;
					//    var symbol = getSymbol(ticker);
					//    //var sigType = IdeaIdType.Equals.
					//    var direction = trade.Direction;
					//    var investment1 = trade.Investment1;
					//    var investment2 = trade.Investment2;
					//    var price = bars[ticker].Close[ii];
					//    var previousPrice = bars[ticker].Close[ii - 1];
					//    var profit = 0.0;
					//    var size = 0.0;
					//    if (!double.IsNaN(price) && !double.IsNaN(previousPrice))
					//    {
					//        size = direction * investment1;
					//        profit = size * (price - previousPrice);
					//    }
					//    tradeInfo.Add("C," + date.ToString("MM/dd/yyyy") + "," + ticker + "," + price + "," + direction + "," + direction * investment1 + "," + direction * (investment2 - investment1) + "," + direction * investment2);
					//}

					var ot = openTrades.OrderBy(x => x.Key).Select(x => x.Value).ToList();
					foreach (var t in ot)
					{
						var price1 = t.AvgPrice[t.AvgPrice.Keys.OrderBy(x => x).LastOrDefault()];
						var price0 = bars[t.Ticker].TradePrice[ii];

						// trade exit on stop loss
						var exit = useTradeRisk && (t.Direction == 1 ? price0 < (100 - tradeRiskPercent) / 100 * price1 : price0 > (100 + tradeRiskPercent) / 100 * price1);

						var ticker = t.Ticker;
						var symbol = getSymbol(ticker);
						var dir = t.Direction;

						//var useTwoBarExit = group != null? group.UseTwoBar : false;

						var groupId = t.Group;
						var group = _model.Groups.FirstOrDefault(g => g.Id == groupId);
						var useATR = group != null ? group.UseATRRisk : false;
						var atrFactor = group != null ? group.ATRRiskFactor : 0.0;

						var justEntered = t.OpenDateTime.Date == date.Date;

						// two bar exit
						//var twoBarExit = useTwoBarExit ? dir > 0 && bars[t.Ticker].TwoBar[ii] < 0 || dir < 0 && bars[t.Ticker].TwoBar[ii] > 0 : false;

						// atr exit
						var atrl = bars[t.Ticker].Low[ii - 1] - atrFactor * bars[t.Ticker].ATR[ii - 1];
						var atrs = bars[t.Ticker].High[ii - 1] + atrFactor * bars[t.Ticker].ATR[ii - 1];
						var date1 = di > 0 ? dates[di - 1] : default(DateTime);
						atrl = Math.Max(t.ATRX.ContainsKey(date1) ? t.ATRX[date1] : double.MinValue, atrl);
						atrs = Math.Min(t.ATRX.ContainsKey(date1) ? t.ATRX[date1] : double.MaxValue, atrs);
						t.ATRX[date] = dir > 0 ? atrl : atrs;

						var atrExit = useATR && !justEntered ? dir > 0 ? price0 < atrl : price0 > atrs : false;

						// exit all trades if portfolio loses more than a specified percent in one bar  
						if (exit || /*twoBarExit || */ atrExit)
						{
							var openKey = t.Group + "\t" + t.Ticker;
							var trade = openTrades[openKey];
							trade.CloseDateTime = date;
							trade.ExitPrice = price0;
							trade.SetOpen(false);
							trade.Shares[date] = 0;
							trade.Closes[date] = price0;
							trade.TradeTypes[date] = TradeType.PortfolioStop;
							openTrades.Remove(openKey);
							closeTrades.Add(trade);
							resetPortfolio[ticker] = dir;

							if (trade.Direction < 0)
							{
								borrowingCostEngine.CloseShortPosition(ticker, date);
							}
							if (useCost)
							{
								try
								{
									var shares = Math.Abs(trade.Shares.ContainsKey(dates[di - 1]) ? trade.Shares[dates[di - 1]] : 0.0);
									var (execComm, execImpact, cost) = ExecutionCostHelper.ComputeExecutionCost(price0, shares, portfolioValue, bars[ticker].AvgVolatility[ii], bars[ticker].AvgVolume[ii]);
									if (double.IsNaN(cost))
									{
										cost = 0;
									}
									portfolioValue -= cost;
									trade.Cost += cost;
									if (trade.ExecutionCosts.ContainsKey(date))
									{ var (pc, pi) = trade.ExecutionCosts[date]; trade.ExecutionCosts[date] = (pc + execComm, pi + execImpact); }
									else trade.ExecutionCosts[date] = (execComm, execImpact);
								}
								catch (Exception x)
								{
								}
							}
						}
					}

					var shortPrices = new Dictionary<string, decimal>();

					foreach (var kvp in openTrades.OrderBy(k => k.Key))
					{
						var trade = kvp.Value;
						var ticker = trade.Ticker;
						var symbol = getSymbol(ticker);
						var direction = trade.Direction;
						var investment1 = trade.Investment1;
						var investment2 = trade.Investment2;
						var price = bars[ticker].Close[ii];
						if (direction < 0)
						{
							shortPrices[ticker] = (decimal)price;
						}
						var previousPrice = (trade.OpenDateTime.Date == date.Date) ? price
							: (ii > 0 ? bars[ticker].Close[ii - 1] : price);
						var profit = 0.0;
						var size = 0.0;
						if (!double.IsNaN(price) && !double.IsNaN(previousPrice))
						{
							size = direction * investment1;
							profit = size * (price - previousPrice);
						}
						trade.Profit += profit;

						// ============================================================
						// DIAGNOSTIC: Log price data for comparison
						// ============================================================
						if (priceComparisonLog.Count < 100 && Math.Abs(profit) > 0.01)
						{
							priceComparisonLog.Add($"MAIN,{date:yyyy-MM-dd},{ticker},{direction},{investment1:F0},{previousPrice:F4},{price:F4},{profit:F2}");
						}

						bool bp = false;
						if (!double.IsNaN(profit) && !double.IsInfinity(profit))
						{
							portfolioValue += profit;
							monthValue += profit;
						}
						else
						{
							bp = true;
						}

						tradeInfo.Add("O," + date.ToString("MM/dd/yyyy") + "," + ticker + "," + price + "," + direction + "," + direction * investment1 + "," + direction * (investment2 - investment1) + "," + direction * investment2);

						trade.Investment1 = trade.Investment2;
					}

					if (ii % 52 == 0) // Log once per year approximately
					{
						var longPnL = openTrades.Where(kvp => kvp.Value.Direction > 0).Sum(kvp => kvp.Value.Profit);
						var shortPnL = openTrades.Where(kvp => kvp.Value.Direction < 0).Sum(kvp => kvp.Value.Profit);
						var longCount = openTrades.Count(kvp => kvp.Value.Direction > 0);
						var shortCount = openTrades.Count(kvp => kvp.Value.Direction < 0);

						//Debug.WriteLine($"[YEARLY] {date:yyyy-MM-dd} NAV=${portfolioValue:N0} Long:{longCount} Short:{shortCount} LongPnL=${longPnL:N0} ShortPnL=${shortPnL:N0}");
					}

					foreach (var trade in closeTrades.OrderBy(t => t.Group).ThenBy(t => t.Ticker))
					{
						var ticker = trade.Ticker;
						var symbol = getSymbol(ticker);
						//var sigType = IdeaIdType.Equals.
						var direction = trade.Direction;
						var investment = trade.Investment1;
						var price = bars[ticker].Close[ii];
						var previousPrice = bars[ticker].Close[ii - 1];
						var profit = 0.0;
						var size = 0.0;
						if (!double.IsNaN(price) && !double.IsNaN(previousPrice))
						{
							size = direction * investment;
							profit = size * (price - previousPrice);
						}
						trade.Profit += profit;

						bool bp = false;
						if (!double.IsNaN(profit) && !double.IsInfinity(profit))
						{
							portfolioValue += profit;
							monthValue += profit;
						}
						else
						{
							bp = true;
						}
					}

					if (useBorrowingCost)
					{
						var dayCount = (date - borrowingDate).TotalDays;
						// Skip borrow on first live iteration: positions just opened this bar,
						// no overnight borrow has accrued yet. prvDate < liveStartDate means
						// the previous iteration was a pre-live continue, not a real held day.
						var skipBorrow = _model.IsLiveMode && prvDate < liveStartDate;
						borrowingDate = date;
						if (!skipBorrow)
						{
							var borrowingCost = dayCount * (double)borrowingCostEngine.CalculateDailyBorrowCosts(date, shortPrices);
							portfolioValue -= borrowingCost;
						}
					}

					if (useDiv)
					{
						var dividendAmount = 0.0;
						foreach (var divKvp in dividends.OrderBy(k => k.Key))
						{
							var divDate = divKvp.Key;
							if (divDate > prvDate && divDate <= date)
							{
								var divAmt = divKvp.Value;
								dividendAmount += divAmt;
							}
						}
						portfolioValue += dividendAmount;
						totalDividendAmount += dividendAmount;
					}

					var v = (100 * (portfolioValue - initialPortfolioValue) / initialPortfolioValue);

					// Live mode: suppress NAV for intra-week MtM dates until after 4 PM
					// On rebalance Friday we always include the value so times/values stay aligned
					var _isTodayUnconfirmed = _model.IsLiveMode && date.Date == DateTime.Today
						&& DateTime.Now.TimeOfDay < TimeSpan.FromHours(16)
						&& DateTime.Today.DayOfWeek != DayOfWeek.Friday;  // keep Friday value so times/values stay aligned
					if (!_isTodayUnconfirmed)
					{
						values.Add(v);
						v = (portfolioValue / previousPortfolioValue) - 1;
						returns.Add(v);
					}
					else
					{
						v = (portfolioValue / previousPortfolioValue) - 1;
					}

					//            if (coolDown == 0)
					//            {
					//                rampUpDate = date;
					if (portfolioValue > highWaterPortfolioValue)
					{
						highWaterPortfolioValue = portfolioValue;
						exitLevel = 0;
					}
					//            }
					//            else
					//            {
					//	coolDown--;
					//            }
					if (moneyManagementEnable)
					{
						// Drawdown triggers: compare portfolio value to high water mark
						var exit1 = (portfolioValue < ((100 - stopLossPercent1) / 100) * highWaterPortfolioValue);
						var exit2 = (portfolioValue < ((100 - stopLossPercent2) / 100) * highWaterPortfolioValue);
						var exit3 = (portfolioValue < ((100 - stopLossPercent3) / 100) * highWaterPortfolioValue);

						// Apply drawdown reduction levels
						if (exit3 && exitLevel <= 2)
						{
							exitLevel = 3;
							consecutivePositiveWeeks = 0;
							dateAtRiskReduction = date;
							portfolioValueAtLastWeek = portfolioValue;
							reductionPercent = reductionPercent3;
							riskPercent = 5;  // near-flat but maintains minimal positions for recovery tracking
						}
						else if (exit2 && exitLevel <= 1)
						{
							exitLevel = 2;
							consecutivePositiveWeeks = 0;
							dateAtRiskReduction = date;
							portfolioValueAtLastWeek = portfolioValue;
							reductionPercent = reductionPercent2;
							riskPercent = 100 - reductionPercent;  // 25% risk
						}
						else if (exit1 && exitLevel == 0)
						{
							exitLevel = 1;
							consecutivePositiveWeeks = 0;
							dateAtRiskReduction = date;
							portfolioValueAtLastWeek = portfolioValue;
							reductionPercent = reductionPercent1;
							riskPercent = 100 - reductionPercent;  // 50% risk
						}

						// Recovery: count consecutive positive weeks and ramp up
						if (exitLevel > 0 && riskPercent < 100)
						{
							// Check if this week was positive vs last week
							if (portfolioValue > portfolioValueAtLastWeek)
							{
								consecutivePositiveWeeks++;
							}
							else
							{
								consecutivePositiveWeeks = 0;  // reset on any negative week
							}
							portfolioValueAtLastWeek = portfolioValue;

							// Ramp up based on consecutive positive weeks (check highest first)
							var basePercent = 100 - reductionPercent;
							if (consecutivePositiveWeeks >= recoveryPeriod3)
							{
								riskPercent = 100;  // full recovery
								exitLevel = 0;
								highWaterPortfolioValue = portfolioValue;  // reset HWM to current
							}
							else if (consecutivePositiveWeeks >= recoveryPeriod2)
							{
								riskPercent = basePercent + reductionPercent * (recoveryPercent2 / 100);
							}
							else if (consecutivePositiveWeeks >= recoveryPeriod1)
							{
								riskPercent = basePercent + reductionPercent * (recoveryPercent1 / 100);
							}
						}
					}
					//if (riskPercent == 0)
					//            {
					//                rampUpDate = date;
					//	coolDown = coolDownPeriod;
					//	highWaterPortfolioValue = portfolioValue;
					//}

					if (portfolioValue > peakPortfolioValue)
					{
						peakPortfolioValue = portfolioValue;
					}
					if (moneyManagementEnable && exitLevel > 0)
					{
						//Debug.WriteLine($"[MM] {date:yyyy-MM-dd} exitLevel={exitLevel} riskPct={riskPercent:F0} consWeeks={consecutivePositiveWeeks} PV={portfolioValue:N0} HWM={highWaterPortfolioValue:N0} DD={((portfolioValue / highWaterPortfolioValue) - 1) * 100:F2}%");
					}
					previousPortfolioValue = portfolioValue;

					_progressCompletedNumber++;
					sendProgressEvent();

					nav[date] = portfolioValue;

					// ============================================================
					// DIAGNOSTIC: Count Shares entries for today
					// ============================================================
					var openCount = openTrades.Count;
					if (openCount > 0)
					{
						daysWithOpenTrades++;
						totalOpenTradesAcrossAllDays += openCount;
						foreach (var kvp in openTrades.OrderBy(k => k.Key))
						{
							if (kvp.Value.Shares != null && kvp.Value.Shares.ContainsKey(date))
								sharesEntriesCreatedTotal++;
						}
					}

					prvDate = date;
					if (last)
					{
						//Debug.WriteLine($"[LAST DATE] date={date:yyyy-MM-dd}, openTrades={openTrades.Count}");
						foreach (var kvp in openTrades.Take(5))
						{
							//Debug.WriteLine($"[LAST OPEN] {kvp.Key} shares={kvp.Value.Shares?.LastOrDefault()}");
						}
					}
					var backtestEndDate = _model.UseCurrentDate ? DateTime.MaxValue : _model.DataRange.Time2;
					if (date > backtestEndDate) break;
					if (last && date >= endDate) break;
				}  // <-- main loop closes here

			// ============================================================
			// DIAGNOSTIC: Shares dictionary analysis
			// ============================================================
			//Debug.WriteLine("============================================================");
			//Debug.WriteLine("[DIAGNOSTIC] SHARES DICTIONARY ANALYSIS");
			//Debug.WriteLine("============================================================");
			//Debug.WriteLine($"[DIAGNOSTIC] Days with open trades: {daysWithOpenTrades}");
			//Debug.WriteLine($"[DIAGNOSTIC] Total open-trade-days (sum of open trades each day): {totalOpenTradesAcrossAllDays}");
			//Debug.WriteLine($"[DIAGNOSTIC] Shares entries actually created: {sharesEntriesCreatedTotal}");
			//Debug.WriteLine($"[DIAGNOSTIC] Missing entries: {totalOpenTradesAcrossAllDays - sharesEntriesCreatedTotal}");

			if (totalOpenTradesAcrossAllDays > 0)
			{
				var coveragePercent = 100.0 * sharesEntriesCreatedTotal / totalOpenTradesAcrossAllDays;
				//Debug.WriteLine($"[DIAGNOSTIC] Coverage: {coveragePercent:F1}% of expected entries");
			}

			// Analyze a specific trade's Shares dictionary
			var sampleTrade = trades.FirstOrDefault(t => t.Shares != null && t.Shares.Count > 5);
			if (sampleTrade != null)
			{
				var tradeDuration = (sampleTrade.CloseDateTime - sampleTrade.OpenDateTime).Days;
				//Debug.WriteLine($"[DIAGNOSTIC] Sample trade: {sampleTrade.Ticker}");
				//Debug.WriteLine($"[DIAGNOSTIC]   Direction: {sampleTrade.Direction}");
				//Debug.WriteLine($"[DIAGNOSTIC]   Open: {sampleTrade.OpenDateTime:yyyy-MM-dd}");
				//Debug.WriteLine($"[DIAGNOSTIC]   Close: {sampleTrade.CloseDateTime:yyyy-MM-dd}");
				//Debug.WriteLine($"[DIAGNOSTIC]   Duration: {tradeDuration} calendar days");
				//Debug.WriteLine($"[DIAGNOSTIC]   Shares entries: {sampleTrade.Shares.Count}");
				//Debug.WriteLine($"[DIAGNOSTIC]   Gap: {tradeDuration - sampleTrade.Shares.Count} missing days");

				// Show first 5 and last 5 entries to see pattern
				var sortedShares = sampleTrade.Shares.OrderBy(kv => kv.Key).ToList();
				//Debug.WriteLine($"[DIAGNOSTIC]   First 5 entries:");
				foreach (var kv in sortedShares.Take(5))
				{ } //Debug.WriteLine - DIAGNOSTIC Take(5)

				if (sortedShares.Count > 10)
				{
					//Debug.WriteLine($"[DIAGNOSTIC]   Last 5 entries:");
					foreach (var kv in sortedShares.TakeLast(5))
					{ } //Debug.WriteLine - DIAGNOSTIC TakeLast(5)
				}
			}
			//Debug.WriteLine("============================================================");

			// ============================================================
			// DIAGNOSTIC: Output main loop price samples
			// ============================================================
			//Debug.WriteLine("");
			//Debug.WriteLine("============================================================");
			//Debug.WriteLine("[DIAGNOSTIC] MAIN LOOP PRICE SAMPLES (first 30)");
			//Debug.WriteLine("============================================================");
			//Debug.WriteLine("Source,Date,Ticker,Dir,Shares,PrevPrice,CurrPrice,Profit");
			foreach (var line in priceComparisonLog.Take(30))
			{
				//Debug.WriteLine(line);
			}
			//Debug.WriteLine("============================================================");

			// ============================================================
			// FIX: Build price map from SAME bars used in main loop
			// ============================================================
			var mainLoopPrices = new Dictionary<string, Dictionary<DateTime, double>>(
				StringComparer.OrdinalIgnoreCase);
			foreach (var ticker in bars.Keys.OrderBy(k => k))
			{
				if (ticker == "VIX Index") continue;  // Skip VIX
				var priceMap = new Dictionary<DateTime, double>();
				var tickerBars = bars[ticker];
				// dates[ii] corresponds to tickerBars.Close[ii]
				for (int ii = 0; ii < dates.Count && ii < tickerBars.Close.Count; ii++)
				{
					var date = dates[ii];
					var price = tickerBars.Close[ii];
					if (!double.IsNaN(price))
					{
						priceMap[date] = price;
					}
				}
				if (priceMap.Count > 0)
				{
					mainLoopPrices[ticker] = priceMap;
				}
			}

			// BUILD DAILY PRICES FOR GIPS-COMPLIANT DAILY NAV
			// Try daily bars first; fall back to weekly bars for tickers without daily cache.
			// Weekly closes are sufficient — BuildDailyNavWithPriceMap uses last-value lookback.
			var lastBacktestDate = _model.UseCurrentDate ? DateTime.MaxValue : _model.DataRange.Time2;

			var dailyPrices = new Dictionary<string, Dictionary<DateTime, double>>(StringComparer.OrdinalIgnoreCase);
			foreach (var ticker in bars.Keys.OrderBy(k => k))
			{
				if (ticker == "VIX Index") continue;

				// Try daily bars first
				var tickerDailyBars = _barCache.GetBars(ticker, "Daily", 0);
				if (tickerDailyBars != null && tickerDailyBars.Count > 0)
				{
					var priceMap = new Dictionary<DateTime, double>();
					foreach (var bar in tickerDailyBars)
					{
						if (!double.IsNaN(bar.Close) && bar.Time.Date <= lastBacktestDate)
							priceMap[bar.Time.Date] = bar.Close;
					}
					if (priceMap.Count > 0)
					{
						dailyPrices[ticker] = priceMap;
						continue; // daily bars found — skip weekly fallback
					}
				}

				// Fallback: use weekly bars (already cached for all tickers)
				var tickerWeeklyBars = _barCache.GetBars(ticker, "Weekly", 0);
				if (tickerWeeklyBars != null && tickerWeeklyBars.Count > 0)
				{
					var priceMap = new Dictionary<DateTime, double>();
					foreach (var bar in tickerWeeklyBars)
					{
						if (!double.IsNaN(bar.Close) && bar.Time.Date <= lastBacktestDate)
							priceMap[bar.Time.Date] = bar.Close;
					}
					if (priceMap.Count > 0)
						dailyPrices[ticker] = priceMap;
				}
			}
			//Debug.WriteLine($"[DailyPrices] Total tickers with price data: {dailyPrices.Count}");

			string path3 = @"portfolios\trades\" + _model.Name;
			var sb2 = new StringBuilder();
			trades.ForEach(x => sb2.Append(x.Serialize() + "\n"));
			var data3 = sb2.ToString();
			MainView.SaveUserData(path3, data3);

			// TRADE SET LOCK SAVE
			// Save on first run (no lock exists) or when new trades were generated.
			// This becomes the permanent auditable record of all transactions.
			if (!_usingLockedTrades)
			{
				// Safety guard: never overwrite a longer lock with a shorter one.
				// This allows going backward in time without destroying future trades.
				var existingLock = LoadLockedTrades();
				var existingCount = existingLock?.Count ?? 0;
				if (trades.Count >= existingCount)
				{
					SaveLockedTradeSet(trades);
					SaveLockChecksum(); // snapshot current settings alongside the lock
					if (trades.Count > 0)
					{ //Debug.WriteLine($"[TradeSetLock] Trade set locked: {trades.Count} trades from {trades.Min(t => t.OpenDateTime):yyyy-MM-dd} to {trades.Max(t => t.OpenDateTime):yyyy-MM-dd}");
					}
					else
					{ //Debug.WriteLine($"[TradeSetLock] Trade set locked: 0 trades");
					}
				}
				else
				{
					//Debug.WriteLine($"[TradeSetLock] SKIPPED save — new set ({trades.Count}) smaller than existing lock ({existingCount}). Historical record preserved.");
				}
			}
			else
			{
				// Restore portfolioValue from locked NAV so downstream reporting is correct.
				// The main loop was skipped so portfolioValue was never updated.
				// In live mode, always start fresh at initialPortfolioValue.
				if (!_model.IsLiveMode)
				{
					var lockedNavForRestore = LoadLockedNav($@"decisionlock\{_model.Name}\LockedNav");
					if (lockedNavForRestore.Count > 0)
						portfolioValue = lockedNavForRestore.OrderBy(k => k.Key).Last().Value;
				}
				//Debug.WriteLine($"[TradeSetLock] Replaying {trades.Count} locked trades — main loop skipped. portfolioValue restored to ${portfolioValue:N0}");
			}

			// PRE-REBALANCE BETA LOG: reconstruct from locked trades if main loop was skipped
			if (_preRebalanceBetaLog.Count == 0 && trades.Count > 0)
			{
				var rebalanceDates = trades
					.Select(t => t.OpenDateTime.Date)
					.Distinct()
					.OrderBy(d => d)
					.ToList();

				var betaLogNav = LoadLockedNav($@"decisionlock\{_model.Name}\LockedNav");

				foreach (var rebalDate in rebalanceDates)
				{
					// trades open going INTO this rebalance = opened before this date, not yet closed
					var openAtRebal = trades.Where(t =>
						t.OpenDateTime.Date < rebalDate &&
						(t.CloseDateTime == default || t.CloseDateTime.Date >= rebalDate))
						.ToList();

					if (openAtRebal.Count == 0) continue;

					var betaKey = rebalDate.ToString("yyyy-MM-dd");

					double navAtDate = initialPortfolioValue;
					if (betaLogNav.Count > 0)
					{
						var navEntry = betaLogNav.Where(k => k.Key <= rebalDate)
							.OrderByDescending(k => k.Key).FirstOrDefault();
						if (navEntry.Value > 0) navAtDate = navEntry.Value;
					}

					double weightedBeta = 0.0;
					int lCnt = 0, sCnt = 0;

					foreach (var trade in openAtRebal)
					{
						var sym = _model.Symbols.FirstOrDefault(x => x.Ticker == trade.Ticker);
						if (sym == null) continue;
						double beta;
						if (sym.Beta.ContainsKey(betaKey))
							beta = sym.Beta[betaKey];
						else if (sym.Beta.Count > 0)
						{
							var lastBetaKey = sym.Beta.Keys
								.Where(bk => string.Compare(bk, betaKey) < 0)
								.OrderByDescending(bk => bk).FirstOrDefault();
							beta = lastBetaKey != null ? sym.Beta[lastBetaKey] : 1.0;
						}
						else beta = 1.0;

						weightedBeta += trade.Direction * trade.Investment1 * beta;
						if (trade.Direction > 0) lCnt++;
						else sCnt++;
					}

					double netBeta = navAtDate > 0 ? weightedBeta / navAtDate : 0.0;
					_preRebalanceBetaLog.Add((rebalDate, netBeta, lCnt, sCnt));
				}
			}

			var portfolioEndDate = _model.UseCurrentDate ? DateTime.MaxValue : _model.DataRange.Time2;
			// Live mode: hide lookback period from UI
			var uiStartDate = _model.IsLiveMode ? _model.LiveStartDate : startDate;

			// LIVE MODE: Intra-week daily MtM NAV
			// On non-rebalance days compute today's NAV from daily bars.
			DateTime _dailyNavDate = default(DateTime);
			double _dailyNavValue = double.NaN;
			if (_model.IsLiveMode && openTrades.Count > 0)
			{
				var todayDate = DateTime.Today;
				var isNonRebalanceDay = todayDate.DayOfWeek != DayOfWeek.Friday;
				if (isNonRebalanceDay)
				{
					var lastFridayNav = (values.Count > 0)
						? initialPortfolioValue * (1.0 + values.Last() / 100.0)
						: initialPortfolioValue;
					var _intraDayNav = lastFridayNav;
					foreach (var kvp in openTrades)
					{
						var trade = kvp.Value;
						var ticker = trade.Ticker;
						if (!dailyBars.ContainsKey(ticker)) continue;
						var db = dailyBars[ticker];
						var todayIdx = db.Time.Data.FindIndex(t => DateTime.FromOADate(t).Date == todayDate);
						if (todayIdx < 0) continue;
						var todayClose = db.Close[todayIdx];
						var fridayDate = trade.Shares.Count > 0
							? trade.Shares.Keys.OrderByDescending(k => k).First().Date
							: trade.OpenDateTime.Date;
						var fridayIdx = db.Time.Data.FindIndex(t => DateTime.FromOADate(t).Date == fridayDate);
						if (fridayIdx < 0) continue;
						var fridayClose = db.Close[fridayIdx];
						var sharesEntry = trade.Shares.OrderByDescending(s => s.Key).FirstOrDefault();
						var shares = sharesEntry.Value;
						if (shares == 0) continue;
						var pnl = trade.Direction * shares * (todayClose - fridayClose);
						if (!double.IsNaN(pnl) && !double.IsInfinity(pnl))
							_intraDayNav += pnl;
					}
					_dailyNavDate = todayDate;
					_dailyNavValue = 100.0 * (_intraDayNav - initialPortfolioValue) / initialPortfolioValue;
					//Debug.WriteLine($"[DAILY-MtM] date={todayDate:yyyy-MM-dd} openTrades={openTrades.Count} nav={_intraDayNav:N0} v={_dailyNavValue:F4}");
				}
			}

			// In live mode filter to Fridays only, then append intra-week daily date if present
			var portfolioTimes = _model.IsLiveMode
				? dates.Where(x => x >= uiStartDate && x <= portfolioEndDate && x.DayOfWeek == DayOfWeek.Friday).ToList()
				: dates.Where(x => x >= uiStartDate && x <= portfolioEndDate).ToList();
			//Debug.WriteLine($"[LOCK] portfolioTimes.Count={portfolioTimes.Count} dates.Count={dates.Count}");
			if (_model.IsLiveMode && _dailyNavDate != default(DateTime) && !double.IsNaN(_dailyNavValue))
			{
				portfolioTimes.Add(_dailyNavDate);
				values.Add(_dailyNavValue);
				returns.Add(0);
			}

			// Live Friday rebalance: today's MOC bar is not yet in the bar cache (market still open),
			// so the Friday filter above misses today. Inject it explicitly so the UI arrow can
			// navigate to today and preview potential trades before the 3:00 PM signal run.
			if (_model.IsLiveMode
				&& DateTime.Today.DayOfWeek == DayOfWeek.Friday
				&& !portfolioTimes.Any(x => x.Date == DateTime.Today))
			{
				var lastValue = values.Count > 0 ? values.Last() : 0.0;
				portfolioTimes.Add(DateTime.Today);
				values.Add(lastValue);   // placeholder: flat until MOC confirms NAV
				returns.Add(0.0);
			}

			// ============================================================
			// LOCKED TRADE REPLAY: UI collections rebuilt below after dailyNav is computed.
			// ============================================================

			var dailyNav = BuildDailyNavWithPriceMap(trades, dailyPrices, initialPortfolioValue);

			var path2 = @"portfolios\" + _model.Name + @"\";

			// --- NAV LOCK START ---
			var lockedNavPath = $@"decisionlock\{_model.Name}\LockedNav";

			// Clone targets: when model X runs locked replay, also write its LockedNav to these models.
			var navCloneTargets = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
			{
				{ "OEX V2 DUP", new List<string> { "CMR US LG CAP" } }
			};

			// NAV source override: models that must display another model's LockedNav.
			// CMR US LG CAP has no trade lock and runs a live main loop, so its own
			// computed NAV will differ from OEX V2 DUP. Always read from OEX V2 DUP instead.
			var navSourceOverride = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			{
				{ "CMR US LG CAP", "OEX V2 DUP" }
			};

			Dictionary<DateTime, double> lockedNav;
			if (_model.IsLiveMode)
			{
				lockedNav = new Dictionary<DateTime, double>();
			}
			else if (navSourceOverride.TryGetValue(_model.Name, out var sourceModelName))
			{
				// Load NAV from the authoritative source model, not this model's own store
				lockedNav = LoadLockedNav($@"decisionlock\{sourceModelName}\LockedNav");
			}
			else
			{
				lockedNav = LoadLockedNav(lockedNavPath);
			}

			if (!_usingLockedTrades && !navSourceOverride.ContainsKey(_model.Name))
			{
				// Normal model: compute and save own LockedNav
				var backtestEnd = _model.UseCurrentDate ? DateTime.MaxValue : _model.DataRange.Time2;
				lockedNav = new Dictionary<DateTime, double>();
				var navSource = nav.Count == 0 ? dailyNav : nav;
				foreach (var kvp in navSource)
				{
					if (kvp.Key <= backtestEnd)
						lockedNav[kvp.Key] = kvp.Value;
				}
				SaveLockedNav(lockedNavPath, lockedNav);

				// Seed any clone models
				if (navCloneTargets.TryGetValue(_model.Name, out var cloneTargets))
				{
					foreach (var cloneName in cloneTargets)
						SaveLockedNav($@"decisionlock\{cloneName}\LockedNav", lockedNav);
				}
			}
			else if (_usingLockedTrades)
			{
				// Locked replay: seed any clone models with this model's authoritative LockedNav
				if (navCloneTargets.TryGetValue(_model.Name, out var cloneTargets))
				{
					foreach (var cloneName in cloneTargets)
						SaveLockedNav($@"decisionlock\{cloneName}\LockedNav", lockedNav);
				}
			}

			var finalNav = lockedNav.OrderBy(k => k.Key).ToDictionary(k => k.Key, k => k.Value);
			// --- NAV LOCK END ---

			// ============================================================
			// LOCKED TRADE REPLAY: Rebuild UI collections from finalNav
			// ============================================================
			if (_usingLockedTrades && finalNav.Count > 0)
			{
				var navSeries = finalNav.OrderBy(k => k.Key).ToList();
				var navFirst = navSeries.First().Value;
				var navPrev = navFirst;

				foreach (var pt in portfolioTimes)
				{
					var navEntry = navSeries.LastOrDefault(k => k.Key <= pt);
					var navVal = navEntry.Value > 0 ? navEntry.Value : navFirst;
					values.Add(100.0 * (navVal - initialPortfolioValue) / initialPortfolioValue);
					returns.Add(navPrev > 0 ? (navVal / navPrev) - 1.0 : 0.0);
					navPrev = navVal;
					regimes.Add(0);
				}

				var monthReturnByYearMonth = new Dictionary<(int, int), double>();
				var monthStartNav = navFirst;
				var prevYM = (-1, -1);
				var lastNavValue = navFirst;
				foreach (var kvp in navSeries)
				{
					if (kvp.Key < startDate) continue;
					var ym = (kvp.Key.Year, kvp.Key.Month);
					if (ym != prevYM)
					{
						if (prevYM.Item1 != -1)
							monthReturnByYearMonth[prevYM] = _model.Groups[0].Leverage * 100.0 * (lastNavValue - monthStartNav) / monthStartNav;
						monthStartNav = kvp.Value;
						prevYM = ym;
					}
					lastNavValue = kvp.Value;
				}
				if (prevYM.Item1 != -1 && monthStartNav > 0)
					monthReturnByYearMonth[prevYM] = _model.Groups[0].Leverage * 100.0 * (lastNavValue - monthStartNav) / monthStartNav;
				foreach (var pt in portfolioTimes)
				{
					var ym = (pt.Year, pt.Month);
					monthDates.Add(pt);
					monthValues.Add(monthReturnByYearMonth.TryGetValue(ym, out var mr) ? mr : 0.0);
				}
			}
			// Strip today from portfolioTimes only for intra-week (Mon-Thu) MtM dates before 4 PM.
			// On a live rebalance Friday we always keep today so the UI can navigate here
			// and preview potential trades before the 3:00 PM signal run.
			var _todayUnconfirmed = _model.IsLiveMode && portfolioTimes.Count > 0
				&& portfolioTimes.Last().Date == DateTime.Today
				&& DateTime.Now.TimeOfDay < TimeSpan.FromHours(16)
				&& DateTime.Today.DayOfWeek != DayOfWeek.Friday;  // ← keep Friday navigable
			if (_todayUnconfirmed)
			{
				portfolioTimes.RemoveAt(portfolioTimes.Count - 1);
				if (finalNav.Count > 0) finalNav.Remove(finalNav.Keys.Last());
			}

			// CRITICAL: if bar collection timed out for a live portfolio, the optimizer ran
			// without real price data. Do not overwrite historical disk values with bad data.
			if (_model.IsLiveMode && _barCollectionTimedOut)
			{
				// Skip save -- preserve existing disk values
				return;
			}

			saveList<DateTime>(_model.Name + " PortfolioTimes", portfolioTimes);
			saveList<double>(_model.Name + " PortfolioNavs", finalNav.Select(kvp => kvp.Value).ToList());
			// Debug: show portfolioTimes vs values alignment
			for (int dbg = 0; dbg < Math.Min(portfolioTimes.Count, values.Count); dbg++)

			saveList<double>(_model.Name + " PortfolioValues", values);
			saveList<double>(_model.Name + " PortfolioReturns", returns);
			saveList<DateTime>(_model.Name + " PortfolioMonthTimes", monthDates);
			saveList<double>(_model.Name + " PortfolioMonthValues", monthValues);
			//Debug.WriteLine($"[SAVE] portfolioTimes={portfolioTimes.Count} values={values.Count} returns={returns.Count} monthDates={monthDates.Count} finalNav={finalNav.Count}");
			//Debug.WriteLine($"[NoiseSuppression] Noise trades BLOCKED: {noiseTradesBlocked}");
			saveList<int>(_model.Name + " PortfolioRegimes", regimes);

			#region DEBUG_BEFORE_BUILDDAILYNAV
			// ============================================================
			// DEBUG: Verify trade directions before BuildDailyNav
			// ============================================================
			//Debug.WriteLine("============================================================");
			//Debug.WriteLine("[DEBUG] TRADE DIRECTION VERIFICATION");
			//Debug.WriteLine("============================================================");
			var longTrades = trades.Where(t => t.Direction == 1).ToList();
			var shortTrades = trades.Where(t => t.Direction == -1).ToList();
			var zeroDirectionTrades = trades.Where(t => t.Direction == 0).ToList();
			var otherDirectionTrades = trades.Where(t => t.Direction != 1 && t.Direction != -1 && t.Direction != 0).ToList();

			//Debug.WriteLine($"[DEBUG] Total trades: {trades.Count}");
			//Debug.WriteLine($"[DEBUG] Long trades (Direction=1): {longTrades.Count}");
			//Debug.WriteLine($"[DEBUG] Short trades (Direction=-1): {shortTrades.Count}");
			//Debug.WriteLine($"[DEBUG] Zero direction trades (Direction=0): {zeroDirectionTrades.Count}");
			//Debug.WriteLine($"[DEBUG] Other direction trades: {otherDirectionTrades.Count}");

			if (shortTrades.Count > 0)
			{
				var sampleShort = shortTrades.First();
				//Debug.WriteLine($"[DEBUG] Sample SHORT trade: Ticker={sampleShort.Ticker}, Direction={sampleShort.Direction}, Shares count={sampleShort.Shares?.Count ?? 0}");
				if (sampleShort.Shares != null && sampleShort.Shares.Count > 0)
				{
					var firstShareEntry = sampleShort.Shares.First();
					//Debug.WriteLine($"[DEBUG]   First Shares entry: Date={firstShareEntry.Key:yyyy-MM-dd}, Shares={firstShareEntry.Value} (should be POSITIVE)");
				}
			}

			if (longTrades.Count > 0)
			{
				var sampleLong = longTrades.First();
				//Debug.WriteLine($"[DEBUG] Sample LONG trade: Ticker={sampleLong.Ticker}, Direction={sampleLong.Direction}, Shares count={sampleLong.Shares?.Count ?? 0}");
				if (sampleLong.Shares != null && sampleLong.Shares.Count > 0)
				{
					var firstShareEntry = sampleLong.Shares.First();
					//Debug.WriteLine($"[DEBUG]   First Shares entry: Date={firstShareEntry.Key:yyyy-MM-dd}, Shares={firstShareEntry.Value} (should be POSITIVE)");
				}
			}

			// Check for any trades with unexpected Direction values
			if (otherDirectionTrades.Count > 0)
			{
				//Debug.WriteLine($"[DEBUG] WARNING: Found {otherDirectionTrades.Count} trades with unexpected Direction values!");
				foreach (var t in otherDirectionTrades.Take(5))
				{
					//Debug.WriteLine($"[DEBUG]   Ticker={t.Ticker}, Direction={t.Direction}");
				}
			}

			//Debug.WriteLine($"[DEBUG] Main loop final portfolioValue: ${portfolioValue:N0}");
			//Debug.WriteLine($"[DEBUG] Initial portfolio value: ${initialPortfolioValue:N0}");
			//Debug.WriteLine($"[DEBUG] Main loop total return: {((portfolioValue / initialPortfolioValue) - 1) * 100:F2}%");
			//Debug.WriteLine("============================================================");

			#endregion

			saveList(path2 + DateTime.Now.ToString("MM-dd-yyyy-HH-mm") + ".csv", tradeInfo);

			//var spxBars = _barCache.GetBars("SPY US Equity", "Daily", 0);
			//var dailySPXCloses = spxBars.Select(b => (b.Time, b.Close)).ToDictionary();
			//var dailyNav = nav;
			//testReporting(_model.Name, trades, nav, dailySPXCloses, borrowingCostEngine);

			#region DEBUG_AFTER_BUILDDAILYNAV
			// ============================================================
			// DEBUG: Compare NAV values
			// ============================================================
			//Debug.WriteLine("============================================================");
			//Debug.WriteLine("[DEBUG] NAV COMPARISON");
			//Debug.WriteLine("============================================================");

			if (dailyNav.Count > 0)
			{
				var dailyNavFirst = dailyNav.First();
				var dailyNavLast = dailyNav.Last();
				//Debug.WriteLine($"[DEBUG] BuildDailyNav first: {dailyNavFirst.Key:yyyy-MM-dd} = ${dailyNavFirst.Value:N0}");
				//Debug.WriteLine($"[DEBUG] BuildDailyNav last: {dailyNavLast.Key:yyyy-MM-dd} = ${dailyNavLast.Value:N0}");
				//Debug.WriteLine($"[DEBUG] BuildDailyNav total return: {((dailyNavLast.Value / dailyNavFirst.Value) - 1) * 100:F2}%");
				//Debug.WriteLine($"[DEBUG] Main loop final: ${portfolioValue:N0}");
				//Debug.WriteLine($"[DEBUG] Ratio (Main/BuildDailyNav): {portfolioValue / dailyNavLast.Value:F2}x");

				if (Math.Abs(portfolioValue - dailyNavLast.Value) > 1000)
				{
					//Debug.WriteLine($"[DEBUG] WARNING: Significant NAV discrepancy detected!");
					//Debug.WriteLine($"[DEBUG] Difference: ${Math.Abs(portfolioValue - dailyNavLast.Value):N0}");
				}
				//Debug.WriteLine("============================================================");
			}
			#endregion

			var spxBars = _barCache.GetBars("SPY US Equity", "Daily", 0);
			var dailySPXCloses = spxBars
				.Where(b => !double.IsNaN(b.Close) && !double.IsInfinity(b.Close) && b.Close > 0)
				.Select(b => (b.Time, b.Close))
				.ToDictionary();
			//Debug.WriteLine($"[SPX] Bar count: {spxBars.Count}, Filtered closes: {dailySPXCloses.Count}");
			if (spxBars.Count > dailySPXCloses.Count)
			{ //Debug.WriteLine($"[SPX] WARNING: Filtered out {spxBars.Count - dailySPXCloses.Count} bad SPX bars");
			}

			if (nav.Count > 0)
			{
				// DIAGNOSTIC: Check actual return and cost magnitudes
				var navFirst = nav.OrderBy(k => k.Key).First();
				var navLast = nav.OrderBy(k => k.Key).Last();
				var totalReturn = (navLast.Value - navFirst.Value);
				var totalReturnPct = (navLast.Value / navFirst.Value - 1) * 100;

				//Debug.WriteLine("============================================================");
				//Debug.WriteLine("[COST DIAGNOSTIC]");
				//Debug.WriteLine("============================================================");
				//Debug.WriteLine($"Initial NAV: ${navFirst.Value:N0}");
				//Debug.WriteLine($"Final NAV: ${navLast.Value:N0}");
				//Debug.WriteLine($"Total Return: ${totalReturn:N0} ({totalReturnPct:F2}%)");
				//Debug.WriteLine($"Total Execution Costs (from trades): ${trades.Sum(t => t.Cost):N0}");
				//Debug.WriteLine($"Execution Cost as % of Initial: {(trades.Sum(t => t.Cost) / navFirst.Value) * 100:F2}%");
				var borrowSummary = borrowingCostEngine.GetSummary();
				//Debug.WriteLine($"[NAV BREAKDOWN] Final portfolioValue: ${portfolioValue:N0}");
				//Debug.WriteLine($"[NAV BREAKDOWN] Total Dividend Income ITD: ${totalDividendAmount:N0}");
				//Debug.WriteLine($"[NAV BREAKDOWN] Gross Borrow Cost: ${borrowSummary.TotalGrossBorrowCost:N0}");
				//Debug.WriteLine($"[NAV BREAKDOWN] Rebate Income: ${borrowSummary.TotalRebateIncome:N0}");
				//Debug.WriteLine($"[NAV BREAKDOWN] Net Borrow Cost: ${borrowSummary.TotalNetBorrowCost:N0}");
				//Debug.WriteLine($"Total Gross Borrowing: ${borrowSummary.TotalGrossBorrowCost:N0}");
				//Debug.WriteLine($"Total Rebate Income: ${borrowSummary.TotalRebateIncome:N0}");
				//Debug.WriteLine($"Total Net Borrowing: ${borrowSummary.TotalNetBorrowCost:N0}");
				//Debug.WriteLine("============================================================");
			}

			// Only run testReporting on a full main loop run — not on locked trade replay.
			// When replaying, borrowingCostEngine is empty and would overwrite borrowingCosts.csv with a blank file.
			// In live mode, always run reporting regardless of locked trade state.
			if (!_usingLockedTrades || _model.IsLiveMode)
			{
				// Filter finalNav to weekly (Friday) entries only — finalNav may contain daily
				// mark-to-market entries from BuildDailyNavWithPriceMap which inflate GIPSReport vol.
				var weeklyNav = finalNav
					.Where(kvp => kvp.Key.DayOfWeek == DayOfWeek.Friday)
					.OrderBy(kvp => kvp.Key)
					.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
				testReporting(_model.Name, trades, weeklyNav.Count > 0 ? weeklyNav : finalNav, dailySPXCloses, borrowingCostEngine);
			}
			else if (_usingLockedTrades)
			{
				// Locked trade replay — run reporting but skip borrowing costs (engine is empty)
				var weeklyNav = finalNav
					.Where(kvp => kvp.Key.DayOfWeek == DayOfWeek.Friday)
					.OrderBy(kvp => kvp.Key)
					.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
				testReporting(_model.Name, trades, weeklyNav.Count > 0 ? weeklyNav : finalNav, dailySPXCloses, null);
			}

			alphaWeightDiag.OutputAnalysis(useAlphaWeight);
		}

		private void testReporting(string name, List<Trade> trades, Dictionary<DateTime, double> dailyNav, Dictionary<DateTime, double> spxCloses, BorrowingCostEngine costEngine)
		{
			try
			{
				var config = new PortfolioConfiguration
				{
					FundName = "Capital Markets Research US Lg Cap",
					StartDate = _model.IsLiveMode ? _model.LiveStartDate : _model.DataRange.Time1,
					InitialBalance = (decimal)_model.InitialPortfolioBalance,    // ← from UI
					ManagementFeePercent = 0.0m,
					IncentiveFeePercent = 20.0m,
					PrimaryBenchmark = "SPY US Equity"
				};

				var timestamp = _model.IsLiveMode
					? DateTime.Now.ToString("yyyyMMddHHmm") + "_live"
					: DateTime.Now.ToString("yyyyMMddHHmm") + "_backtest_archive";
				var generator = new ATMMLHedgeFundReportGenerator(config, timestamp);
				var reports = generator.GenerateAllReports(name, trades, dailyNav, spxCloses);

				// Save reports to files
				var path = @"C:\Users\Public\Documents\ATMML\portfolios";
				Directory.CreateDirectory(path);
				path += @"\reports";
				Directory.CreateDirectory(path);
				path += @"\" + name;
				Directory.CreateDirectory(path);
				path += @"\" + timestamp;
				Directory.CreateDirectory(path);

				reports.SaveToFiles(path);
				if (costEngine != null)
					costEngine.ExportDailyCostsToCSV(path + @"\borrowingCosts.csv");

				WriteCsv(dailyNav, path + @"\dailyNav.csv");

				// PRE-REBALANCE BETA LOG
				if (_preRebalanceBetaLog.Count > 0)
				{
					using var bw = new StreamWriter(path + @"\preRebalanceBetaLog.csv");
					bw.WriteLine("Date,NetBeta,LongCount,ShortCount");
					foreach (var e in _preRebalanceBetaLog.OrderBy(e => e.Date))
						bw.WriteLine($"{e.Date:yyyy-MM-dd},{e.NetBeta:F4},{e.LongCount},{e.ShortCount}");
				}
			}
			catch (Exception x)
			{
				//Debug.WriteLine($"[REPORT ERROR] {x.Message}");
				//Debug.WriteLine($"[REPORT ERROR] {x.StackTrace}");
			}
		}

		static void WriteCsv(
			Dictionary<DateTime, double> data,
			string filePath)
		{
			using var writer = new StreamWriter(filePath);

			// Header
			writer.WriteLine("Date,Value");

			foreach (var kvp in data.OrderBy(k => k.Key))
			{
				string date = kvp.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
				string value = kvp.Value.ToString(CultureInfo.InvariantCulture);

				writer.WriteLine($"{date},{value}");
			}
		}


		private void saveList<T>(string path, List<T> input)
		{
			StringBuilder sb = new StringBuilder();
			foreach (var item in input)
			{
				var text = item.ToString();
				if (text.Length > 0)
				{
					sb.Append(text + "\n");
				}
			}
			var data = sb.ToString();

			MainView.SaveUserData(@"models\lists\" + path, data);
		}

		private List<PortfolioMember> getFilterData(List<PortfolioMember> input, string lowestInterval)
		{
			var interval = _scan.getLowestInterval();

			string benchmarkSymbol = _model.Benchmark;
			Dictionary<string, List<Bar>> bars = new Dictionary<string, List<Bar>>();
			bars[benchmarkSymbol] = getBars(benchmarkSymbol, interval, BarServer.MaxBarCount);
			List<DateTime> times = bars[benchmarkSymbol].Select(x => x.Time).ToList();

			DateTime date1 = _model.DataRange.Time1; // getStartDate(interval, lowestInterval); // mlPredict
			DateTime date2 = _model.UseCurrentDate ? DateTime.UtcNow : _model.DataRange.Time2; // DateTime.MaxValue;

			var output = new List<PortfolioMember>();

			for (var ii = 0; ii < input.Count; ii++)
			{
				var member = PortfolioMember.Clone(input[ii]);

				foreach (var time in times)
				{
					if (time >= date1 && time <= date2)
					{
						member.Sizes[time] = 0;
						foreach (var scanSummary in _scanSummary)
						{
							if (scanSummary.Date == time)
							{
								var results = scanSummary.GetResults();
								foreach (var result in results)
								{
									if (result.Enable && result.Symbol == member.Ticker)
									{
										var tl = getLowestTimes(result.Symbol, time, interval, lowestInterval);
										tl.ForEach(t => member.Sizes[t] = 1);
									}
								}
							}
						}
					}
				}
			}

			return output;
		}

		private double getShareAmount(double input, double price)
		{
			//var log = Math.Max(0, (int)Math.Log10(price));
			//var factor = Math.Pow(10, log);

			var factor = 10000;
			if (price >= 1000) factor = 1;
			else if (price >= 500) factor = 5;
			else if (price >= 100) factor = 10;
			else if (price >= 50) factor = 50;
			else if (price >= 10) factor = 100;
			else if (price >= 5) factor = 500;
			else if (price >= 1) factor = 1000;

			var output = Math.Max(input == 0 ? 0 : 1, Math.Round(input / factor)) * factor;
			return output;

		}

		private List<DateTime> getLowestTimes(string ticker, DateTime time, string interval, string lowestInterval)
		{
			int aggOffset = _model.AggressiveOffset;  // +1 = non-aggressive,  0 = Friday at signal, -1 = Thursday before signal, -2 = Wednesday before signal, -3 Tuesday before signal, -4 = Monday before signal
			if (aggOffset == 2) aggOffset = 0;

			var output = new List<DateTime>();
			if (interval == lowestInterval)
			{
				output.Add(time);
			}
			else
			{
				var higherTimes = _barCache.GetTimes(ticker, interval, 0);
				higherTimes.Insert(0, DateTime.MinValue);
				higherTimes.Add(DateTime.MaxValue);
				var index1 = higherTimes.FindIndex(x => x == time);
				if (index1 != -1)
				{
					var time1 = higherTimes[index1 - 1];
					var time2 = higherTimes[index1 - 0];
					var lowerTimes = _barCache.GetTimes(ticker, lowestInterval, 0);
					var totalRange = Enumerable.Range(0, lowerTimes.Count);
					var range = totalRange.Where(x => time1 < lowerTimes[x] && lowerTimes[x] <= time2).Select(i => i + 4 + aggOffset).ToList();

					output.AddRange(range.Where(i => i >= 0 && i < lowerTimes.Count).Select(x => lowerTimes[x]));
				}
			}
			return output;
		}

		private List<PortfolioMember> getRankingData(List<PortfolioMember> input, string lowestInterval)
		{
			var output = new List<PortfolioMember>();

			for (var ii = 0; ii < input.Count; ii++)
			{
				var member = PortfolioMember.Clone(input[ii]);
				output.Add(member);
			}

			Dictionary<string, Dictionary<DateTime, double>> factorScores = getFactorScores(lowestInterval);

			string benchmarkSymbol = _model.Benchmark;
			string interval = _model.RankingInterval;
			Dictionary<string, List<Bar>> bars = new Dictionary<string, List<Bar>>();
			bars[benchmarkSymbol] = getBars(benchmarkSymbol, interval, BarServer.MaxBarCount);
			List<DateTime> times = bars[benchmarkSymbol].Select(x => x.Time).ToList();

			DateTime date1 = _model.DataRange.Time1; // getStartDate(interval, lowestInterval); // mlPredict
			DateTime date2 = _model.UseCurrentDate ? DateTime.UtcNow : _model.DataRange.Time2; // DateTime.MaxValue;

			var tickers = factorScores.Keys.ToList();
			int tickerCount = tickers.Count;

			foreach (var time in times)
			{
				var scores = new Dictionary<string, double>();
				if (time >= date1 && time <= date2)
				{
					foreach (var ticker in tickers)
					{
						var score = (factorScores[ticker].ContainsKey(time)) ? factorScores[ticker][time] : 0.0;
						scores[ticker] = score;
					}

					// rank lowest to highest
					List<string> ranks = scores.OrderBy(x => x.Value).Select(x => x.Key).ToList();

					var pcs = getRankingPercents(_model);
					var top = pcs.Item1;
					var bot = pcs.Item2;

					for (int ii = 0; ii < tickerCount; ii++)
					{
						int value = 0;
						var ticker = ranks[ii];
						var percent = (100 * ii) / tickerCount;
						if (percent >= top) value = 1;
						if (percent <= bot) value = -1;

						var members = output.Where(x => x.Ticker == ticker).ToList();

						members.ForEach(member =>
						{
							var group = _model.Groups.Find(g => g.Id == member.GroupId);
							var side = group.Direction;

							var tl = getLowestTimes(ticker, time, interval, lowestInterval);
							tl.ForEach(t => member.Sizes[t] = (side > 0 && value > 0 || side < 0 && value < 0) ? 1 : 0);
						});
					}
				}
			}
			return output;
		}

		private List<PortfolioMember> getSTFilterData(int group, List<PortfolioMember> input, string lowestInterval)
		{
			var output = new List<PortfolioMember>();

			for (var ii = 0; ii < input.Count; ii++)
			{
				var member = PortfolioMember.Clone(input[ii]);
				output.Add(member);
			}

			var stInterval = lowestInterval;
			string mtInterval = Study.getForecastInterval(stInterval, 1);
			foreach (var member in output)
			{
				var ticker = member.Ticker;

				var g = _model.Groups.Find(g => g.Id == member.GroupId);
				var side = g.Direction;

				var stTimes = _barCache.GetTimes(ticker, stInterval, 0);
				var mtTimes = _barCache.GetTimes(ticker, mtInterval, 0);

				if (g.UseSTTSB)
				{
					var stBars = _barCache.GetSeries(ticker, stInterval, new string[] { "Open", "High", "Low", "Close" }, 0);
					var sigUp = Conditions.calculatePressureUpORBullishTSB(stBars);
					var sigDn = Conditions.calculatePressureDnORBearishTSB(stBars);
					var sig = side > 0 ? sigUp : sigDn;
					var r1 = Enumerable.Range(0, stTimes.Count);
					member.Sizes = r1.ToList().ToDictionary(i => stTimes[i], i => sig[i] == 1 ? 1.0 : 0.0);
				}
				else if (g.UseSTFT)
				{
					var stBars = _barCache.GetSeries(ticker, stInterval, new string[] { "Open", "High", "Low", "Close" }, 0);
					var ft = atm.calculateFT(stBars[1], stBars[2], stBars[3]);
					var sigUp = ft < 30 | ft.IsRising();
					var sigDn = ft > 70 | ft.IsFalling();
					var sig = side > 0 ? sigUp : sigDn;
					var r1 = Enumerable.Range(0, stTimes.Count);
					member.Sizes = r1.ToList().ToDictionary(i => stTimes[i], i => sig[i] == 1 ? 1.0 : 0.0);
				}
				else if (g.UseSTST)
				{
					var stBars = _barCache.GetSeries(ticker, stInterval, new string[] { "Open", "High", "Low", "Close" }, 0);
					var sigUp = atm.calculateSTStrongUp(stBars[1], stBars[2], stBars[3]);
					var sigDn = atm.calculateSTStrongDn(stBars[1], stBars[2], stBars[3]);
					var sig = side > 0 ? sigUp : sigDn;
					var r1 = Enumerable.Range(0, stTimes.Count);
					member.Sizes = r1.ToList().ToDictionary(i => stTimes[i], i => sig[i] == 1 ? 1.0 : 0.0);
				}
				else if (g.UseSTSC)
				{
					var stBars = _barCache.GetSeries(ticker, stInterval, new string[] { "Open", "High", "Low", "Close" }, 0);
					var mtBars = _barCache.GetSeries(ticker, mtInterval, new string[] { "Open", "High", "Low", "Close" }, 0);

					Series rp;
					lock (_referenceData)
					{
						rp = atm.calculateRelativePrice(stInterval, stBars, _referenceData, 5);
					}

					var times = new Dictionary<string, List<DateTime>>();
					times[stInterval] = stTimes;
					times[mtInterval] = mtTimes;
					var bars = new Dictionary<string, Series[]>();
					bars[stInterval] = stBars;
					bars[mtInterval] = mtBars;
					Series score = atm.getScore(times, bars, new string[] { stInterval, mtInterval });
					var sig = atm.calculateSCSig(score, rp, 2);

					var r1 = Enumerable.Range(0, stTimes.Count);
					member.Sizes = r1.ToList().ToDictionary(i => stTimes[i], i => side > 0 && sig[i] == 1 || side < 0 && sig[i] == -1 ? 1.0 : 0.0);
				}
				else
				{
					var r1 = Enumerable.Range(0, stTimes.Count);
					member.Sizes = r1.ToList().ToDictionary(i => stTimes[i], i => 1.0);
				}
			}
			return output;
		}

		private List<PortfolioMember> getMTFilterData(int group, List<PortfolioMember> input, bool useSector, string lowestInterval)
		{
			var output = new List<PortfolioMember>();

			for (var ii = 0; ii < input.Count; ii++)
			{
				var member = PortfolioMember.Clone(input[ii]);
				output.Add(member);
			}

			string stInterval = lowestInterval;
			string mtInterval = Study.getForecastInterval(stInterval, 1);
			string ltInterval = Study.getForecastInterval(stInterval, 2);

			_progressTotalNumber = output.Count;
			sendProgressEvent();

			foreach (var member in output)
			{
				var ticker = member.Ticker;

				var g = _model.Groups.Find(g => g.Id == member.GroupId);
				var side = g.Direction;

				var stTimes = _barCache.GetTimes(ticker, stInterval, 0);

				var symbol = _model.Symbols.FirstOrDefault(s => s.Ticker == ticker);

				var ok = true;
				var dataTicker = ticker;
				if (useSector)
				{
					try
					{
						dataTicker = _portfolio1.getSectorSymbol(int.Parse(symbol.Sector));
					}
					catch (Exception)
					{
						ok = false;
					}
				}

				if (ok)
				{
					var mtTimes = _barCache.GetTimes(dataTicker, mtInterval, 0);
					var ltTimes = _barCache.GetTimes(dataTicker, ltInterval, 0);
					var times = new Dictionary<string, List<DateTime>>();
					times[mtInterval] = mtTimes;
					times[ltInterval] = ltTimes;
					var mtBars = _barCache.GetSeries(dataTicker, mtInterval, new string[] { "Open", "High", "Low", "Close" }, 0);
					var ltBars = _barCache.GetSeries(dataTicker, ltInterval, new string[] { "Open", "High", "Low", "Close" }, 0);
					var bars = new Dictionary<string, Series[]>();
					bars[mtInterval] = mtBars;
					bars[ltInterval] = ltBars;
					var op = mtBars[0];
					var hi = mtBars[1];
					var lo = mtBars[2];
					var cl = mtBars[3];
					var ft = atm.calculateFT(hi, lo, cl);
					var ftgu = ft.IsRising();
					var ftgd = ft.IsFalling();
					var intervalList = new string[] { mtInterval, ltInterval };
					Series score = atm.getScore(times, bars, intervalList);

					Series rp;
					lock (_referenceData)
					{
						rp = atm.calculateRelativePrice(mtInterval, mtBars, _referenceData, 5);
					}


					var scSig = atm.calculateSCSig(score, rp, 2);
					Series ptSig = atm.calculatePressureFilter(op, hi, lo, cl);
					var sig1 = scSig;
					var sig2 = ptSig;
					var sig3 = ftgu - ftgd;
					var signal1 = _model.Groups[0].MTNT != 0 ? atm.sync(sig1, mtInterval, stInterval, mtTimes, stTimes, _model.Groups[0].MTNT != 1) : new Series(stTimes.Count, 0);
					var signal2 = _model.Groups[0].MTP != 0 ? atm.sync(sig2, mtInterval, stInterval, mtTimes, stTimes, _model.Groups[0].MTP != 1) : new Series(stTimes.Count, 0);
					var signal3 = _model.Groups[0].MTFT != 0 ? atm.sync(sig3, mtInterval, stInterval, mtTimes, stTimes, _model.Groups[0].MTFT != 1) : new Series(stTimes.Count, 0);
					var sigUp = signal1 > 0 | signal2 > 0 | signal3 > 0;
					var sigDn = signal1 < 0 | signal2 < 0 | signal3 < 0;
					var sig = side > 0 ? sigUp : sigDn;
					var r1 = Enumerable.Range(0, stTimes.Count);
					member.Sizes = r1.ToList().ToDictionary(i => stTimes[i], i => sig[i] == 1 ? 1.0 : 0.0);
				}
				else
				{
					var r1 = Enumerable.Range(0, stTimes.Count);
					member.Sizes = r1.ToList().ToDictionary(i => stTimes[i], i => 1.0);
				}
			}

			return output;
		}

		private List<PortfolioMember> getConviction(int group, List<PortfolioMember> input, string interval, double convictionSize)
		{
			var output = new List<PortfolioMember>();

			for (var ii = 0; ii < input.Count; ii++)
			{
				var member = PortfolioMember.Clone(input[ii]);
				output.Add(member);
			}

			var groupId = _model.Groups[group].Id;
			var members = output.Where(x => x.GroupId == groupId).ToList();

			foreach (var member in members)
			{
				var ticker = member.Ticker;

				var g = _model.Groups.Find(g => g.Id == member.GroupId);
				var side = g.Direction;

				var times = _barCache.GetTimes(ticker, interval, 0);
				var bars = _barCache.GetSeries(ticker, interval, new string[] { "Close" }, 0);

				var ti = member.Sizes;
				var dates = ti.Keys.OrderBy(x => x).ToList();
				var date = dates.FirstOrDefault();
				var index = times.FindIndex(d => d == date);

				var upSize = 0.0;
				var dnSize = 0.0;
				for (var ii = 0; ii < dates.Count; ii++, index++)
				{
					date = dates[ii];
					var price = bars[0][index];
					var price1 = index > 0 ? bars[0][index - 1] : price;

					var inLong = side > 0 && upSize > 0 && ti[date] > 0;
					var inShort = side < 0 && dnSize > 0 && ti[date] > 0;

					if (inLong)
					{
						if (price > price1)
						{
							upSize *= 1 - convictionSize / 100.0;
						}
						else if (price < price1)
						{
							upSize *= 1 + convictionSize / 100.0;
						}
					}
					else
					{
						upSize = ti[date];
					}

					if (inShort)
					{
						if (price > price1)
						{
							dnSize *= 1 + convictionSize / 100.0;
						}
						else if (price < price1)
						{
							dnSize *= 1 - convictionSize / 100.0;
						}
					}
					else
					{
						dnSize = ti[date];
					}

					member.Sizes[date] = side > 0 ? upSize : dnSize;
				}
			}

			return output;
		}


		private List<PortfolioMember> getATMAnalysis(int group, List<PortfolioMember> input, DateTime startTime, string lowestInterval)
		{
			var output = new List<PortfolioMember>();

			for (var ii = 0; ii < input.Count; ii++)
			{
				var member = PortfolioMember.Clone(input[ii]);
				output.Add(member);
			}
			string stInterval = lowestInterval;
			string mtInterval = Study.getForecastInterval(stInterval, 1);
			string ltInterval = Study.getForecastInterval(stInterval, 2);

			string[] intervals = { stInterval, mtInterval, ltInterval };

			_progressTotalNumber = output.Count;
			sendProgressEvent();

			var groupId = _model.Groups[group].Id;
			var members = output.Where(x => x.GroupId == groupId).ToList();
			var g = _model.Groups[group];
			var side = g.Direction;

			foreach (var member in members)
			{
				var ticker = member.Ticker;

				Dictionary<string, List<DateTime>> times = new Dictionary<string, List<DateTime>>();
				Dictionary<string, Series[]> bars = new Dictionary<string, Series[]>();
				PortfolioMember filter = member;

				foreach (string interval in intervals)
				{
					times[interval] = _barCache.GetTimes(ticker, interval, 0);
					bars[interval] = _barCache.GetSeries(ticker, interval, new string[] { "Open", "High", "Low", "Close" }, 0);
				}

				var position = "Long | Short";
				var enbs = new Dictionary<string, bool>();
				enbs["New Trend"] = _model.Groups[group].UseNewTrend;
				enbs["TwoBar"] = _model.Groups[group].UseTwoBar;
				enbs["FTRoc"] = _model.Groups[group].UseFTRoc;
				enbs["NT OB OS"] = _model.Groups[group].UseNTObOs;
				enbs["UseProveIt"] = _model.Groups[group].UseProveIt;
				enbs["Pressure Alert"] = _model.Groups[group].UsePressure;
				enbs["Add Alert"] = _model.Groups[group].UseAdd;
				enbs["Retrace"] = _model.Groups[group].UseRetrace;
				enbs["FTEntry"] = _model.UseFTEntry;
				enbs["Exhaustion"] = _model.Groups[group].UseExhaustion;
				enbs["PExhaustion"] = _model.Groups[group].UsePExhaustion;
				enbs["MaxUnitsEnable"] = true;// _model.MaxUnitsEnable;
				enbs["FreqPeriodEnable"] = _model.FreqPeriodEnable;
				enbs["UseConviction"] = _model.Groups[group].UseConviction;
				enbs["UseFtFt"] = _model.Groups[group].UseFtFt;
				enbs["UseMTST"] = _model.Groups[group].UseMTST;
				enbs["UseMTSC"] = _model.Groups[group].UseMTSC;
				enbs["UseMTTSB"] = _model.Groups[group].UseMTTSB;
				enbs["UseMTExit"] = _model.UseMTExit;

				var symbolCount = _model.Symbols.Count;

				var sizes = new Dictionary<string, double>();
				sizes["NewTrendUnit"] = _model.Groups[group].NewTrendUnit;
				sizes["PressureUnit"] = _model.Groups[group].PressureUnit;
				sizes["AddUnit"] = _model.Groups[group].AddUnit;
				sizes["FTEntryUnit"] = _model.FTEntryUnit;
				sizes["RetracePercent"] = _model.Groups[group].RetracePercent;
				sizes["ExhaustionPercent"] = _model.Groups[group].ExhaustionPercent;
				sizes["PExhaustionPercent"] = _model.Groups[group].PExhaustionPercent;
				sizes["ConvictionPercent"] = _model.Groups[group].ConvictionPercent;
				sizes["UseATR"] = _model.ATRPeriod;
				sizes["TwoBarPercent"] = _model.Groups[group].TwoBarPercent;
				sizes["FTRocPercent"] = _model.Groups[group].FTRocPercent;

				enbs["TimeExit"] = _model.Groups[group].UseTimeExit;
				sizes["TimeExitCount"] = _model.Groups[group].BarsToExit;
				sizes["TimeExitPercent"] = _model.Groups[group].PercentBarsToExit;

				lock (_referenceData)
				{
					member.Sizes = atm.getTrades(side, filter.Sizes, ticker, startTime, times, bars, intervals, _referenceData, position, enbs, sizes, 100 * _model.Groups[group].Leverage, _model.FreqPeriod);
				}

				_progressCompletedNumber++;
				sendProgressEvent();
			}
			return output;
		}

		private List<PortfolioMember> getStrategyData(int group, List<PortfolioMember> input, string lowestInterval)
		{
			var output = new List<PortfolioMember>();

			for (var ii = 0; ii < input.Count; ii++)
			{
				var member = PortfolioMember.Clone(input[ii]);
				output.Add(member);
			}

			_progressTotalNumber = output.Count;
			sendProgressEvent();

			var groupId = _model.Groups[group].Id;
			var members = output.Where(x => x.GroupId == groupId).ToList();
			var g = _model.Groups[group];
			var side = g.Direction;

			foreach (var member in members)
			{
				string vstInterval = Study.getForecastInterval(g.AlignmentInterval, -1);
				string stInterval = g.AlignmentInterval;
				string mtInterval = Study.getForecastInterval(stInterval, 1);
				string ltInterval = Study.getForecastInterval(stInterval, 2);
				string[] intervals = { stInterval, mtInterval, ltInterval };

				var direction = 0;

				var ticker = member.Ticker;

				Dictionary<string, List<DateTime>> times = new Dictionary<string, List<DateTime>>();
				Dictionary<string, Series[]> bars = new Dictionary<string, Series[]>();

				foreach (string interval in intervals)
				{
					times[interval] = _barCache.GetTimes(ticker, interval, 0);
					bars[interval] = _barCache.GetSeries(ticker, interval, new string[] { "Open", "High", "Low", "Close" }, 0);
				}

				var strategy = atm.getStrategy(g.Strategy, times, bars, intervals, _referenceData);

				Series vstCondition = null;
				if (_model.AggressiveOffset == 2)
				{
					times[vstInterval] = _barCache.GetTimes(ticker, vstInterval, 0);
					bars[vstInterval] = _barCache.GetSeries(ticker, vstInterval, new string[] { "Open", "High", "Low", "Close" }, 0);
					var u1 = Conditions.Calculate("FT Going Up", ticker, new string[] { vstInterval }, times[vstInterval].Count, times, bars, null);
					var d1 = Conditions.Calculate("FT Going Dn", ticker, new string[] { vstInterval }, times[vstInterval].Count, times, bars, null);
					var u2 = Conditions.Calculate("FT < 80", ticker, new string[] { vstInterval }, times[vstInterval].Count, times, bars, null);
					var d2 = Conditions.Calculate("FT > 20", ticker, new string[] { vstInterval }, times[vstInterval].Count, times, bars, null);
					var u3 = Conditions.Calculate("ST > FT by 30", ticker, new string[] { vstInterval }, times[vstInterval].Count, times, bars, null);
					var d3 = Conditions.Calculate("ST < FT by 30", ticker, new string[] { vstInterval }, times[vstInterval].Count, times, bars, null);
					var u4 = Conditions.Calculate("ST Going Up", ticker, new string[] { vstInterval }, times[vstInterval].Count, times, bars, null);
					var d4 = Conditions.Calculate("ST Going Dn", ticker, new string[] { vstInterval }, times[vstInterval].Count, times, bars, null);
					var u5 = Conditions.Calculate("ST > 75", ticker, new string[] { vstInterval }, times[vstInterval].Count, times, bars, null);
					var d5 = Conditions.Calculate("ST < 25", ticker, new string[] { vstInterval }, times[vstInterval].Count, times, bars, null);
					var u6 = Conditions.Calculate("ST > 25", ticker, new string[] { vstInterval }, times[vstInterval].Count, times, bars, null);
					var d6 = Conditions.Calculate("ST < 75", ticker, new string[] { vstInterval }, times[vstInterval].Count, times, bars, null);
					var u7 = Conditions.Calculate("TSB Bullish", ticker, new string[] { vstInterval }, times[vstInterval].Count, times, bars, null);
					var d7 = Conditions.Calculate("TSB Bearish", ticker, new string[] { vstInterval }, times[vstInterval].Count, times, bars, null);
					var u8 = Conditions.Calculate("TSB Bullish Ends", ticker, new string[] { vstInterval }, times[vstInterval].Count, times, bars, null);
					var d8 = Conditions.Calculate("TSB Bearish Ends", ticker, new string[] { vstInterval }, times[vstInterval].Count, times, bars, null);
					var u9 = Conditions.Calculate("Pressure Going Up", ticker, new string[] { vstInterval }, times[vstInterval].Count, times, bars, null);
					var d9 = Conditions.Calculate("Pressure Going Dn", ticker, new string[] { vstInterval }, times[vstInterval].Count, times, bars, null);
					vstCondition = ((u1 & u2) | (u3 & u7 & u9)) - ((d1 & d2) | (d3 & d7 & d9));
				}

				var oldDirection = direction;
				for (int ii = 0; ii < times[stInterval].Count; ii++)
				{
					var time = times[stInterval][ii];
					var value1 = strategy[ii];

					var tl = getLowestTimes(ticker, time, stInterval, lowestInterval);
					tl.ForEach(t =>
					{
						var value2 = value1;
						if (vstCondition != null)
						{
							var idx = times[vstInterval].IndexOf(t);
							value2 = (idx >= 0) ? (int)vstCondition[idx] : value1;
							// value1 = weekly value2 = daily
							if (direction == 1 && value1 != 1 && value2 != 1) direction = 0;
							if (direction == -1 && value1 != -1 && value2 != -1) direction = 0;
							if (direction != 1 && value1 == 1 && value2 == 1) direction = 1;
							if (direction != -1 && value1 == -1 && value2 == -1) direction = -1;
							value2 = direction;
						}
						member.Sizes[t] = (side > 0 && value1 > 0 && value2 > 0) || (side < 0 && value1 < 0 && value2 < 0) ? 1 : 0;
					});
				}

				_progressCompletedNumber++;
				sendProgressEvent();
			}
			return output;
		}

		private double getTradePrice(Model model, List<Bar> bars, int index)
		{
			double output = double.NaN;
			if (model.Slippage == Slippage.CurrentClose)
			{
				output = getLastClose(bars, index);
			}
			else if (model.Slippage == Slippage.NextDayOpen)
			{
				output = getNextOpen(bars, index);
				if (double.IsNaN(output))
				{
					output = getLastClose(bars, index);
				}
			}
			return output;
		}

		private double getLastClose(List<Bar> bars, int index)
		{
			double output = double.NaN;
			for (int ii = index; ii >= 0; ii--)
			{
				var close = bars[ii].Close;
				if (!double.IsNaN(close))
				{
					output = close;
					break;
				}
			}
			return output;
		}

		private double getNextOpen(List<Bar> bars, int index)
		{
			double output = double.NaN;
			for (int ii = index + 1; ii < bars.Count; ii++)
			{
				var open = bars[ii].Open;
				if (!double.IsNaN(open))
				{
					output = open;
					break;
				}
			}
			return output;
		}

		private bool rebalance(Model model, DateTime time1, DateTime time2)
		{
			return true;

			bool ok = false;

			while (time2.DayOfWeek == DayOfWeek.Saturday || time2.DayOfWeek == DayOfWeek.Sunday)
			{
				time2 += new TimeSpan(1, 0, 0, 0);
			}

			string period = model.RankingInterval;

			if (period == "Daily")
			{
				ok = true;
			}
			else if (period == "Weekly")
			{
				ok = ((int)time1.DayOfWeek > (int)time2.DayOfWeek);
			}
			else if (period == "Monthly")
			{
				ok = (time1.Month != time2.Month);
			}
			else if (period == "Quarterly")
			{
				ok = (((time1.Month - 1) / 3) != ((time2.Month - 1) / 3));
			}
			else if (period == "SemiAnnually")
			{
				ok = (((time1.Month - 1) / 6) != ((time2.Month - 1) / 6));
			}
			else if (period == "Annually")
			{
				ok = (time1.Year != time2.Year);
			}
			return ok;
		}

		// returns primary and overlay portfolios
		private Tuple<List<string>, List<string>> getPortfolio(Model model, int index, DateTime rebalanceDate, List<DateTime> times)
		{
			List<string> universe = getTickers();

			Dictionary<string, double> factorScores = new Dictionary<string, double>();
			Dictionary<string, double> atmFilters = new Dictionary<string, double>();

			// filter universe
			List<string> tickers = new List<string>();
			bool filterFound = false;
			if (model.UseFilters)
			{
				foreach (var scanSummary in _scanSummary)
				{
					if (scanSummary.Date == rebalanceDate)
					{
						filterFound = true;
						var results = scanSummary.GetResults();
						foreach (var result in results)
						{
							if (result.Enable)
							{
								tickers.Add(result.Symbol);
							}
						}
					}
				}
			}

			if (!filterFound)
			{
				tickers = universe;
			}

			if (_model.UseRanking)
			{
				int tickerCount = tickers.Count;

				// Add up all of the factors in the rebalance period to create the score for each symbol for this rebalance period

				DateTime previousRebalanceDate = getPreviousRebalanceDate(model, rebalanceDate, times);
				factorScores = new Dictionary<string, double>();
				for (int ii = 0; ii < tickerCount; ii++)
				{
					var ticker = tickers[ii];
					factorScores[ticker] = 0;
					if (_factorScores.ContainsKey(ticker))
					{
						foreach (var kvp in _factorScores[ticker])
						{
							var date = kvp.Key;
							var factorScore = kvp.Value;
							if (!double.IsNaN(factorScore))
							{
								if (date > previousRebalanceDate && date <= rebalanceDate)
								{
									factorScores[ticker] += factorScore;
								}
							}
						}
					}
				}
				saveData("scores", model, rebalanceDate, factorScores);
			}

			Dictionary<string, double> atmPredictions = new Dictionary<string, double>();

			// get atm prediction for each ticker in the universe for the rebalance period
			atmPredictions = model.UseML ? getPredictions(model, rebalanceDate) : new Dictionary<string, double>();  // key = ticker, value  = prediction

			saveData("predictions", model, rebalanceDate, atmPredictions);

			List<string> overlay = getPortfolio(rebalanceDate, model, tickers, factorScores, atmPredictions);

			return new Tuple<List<string>, List<string>>(null, overlay);
		}

		private void saveData(string name, Model model, DateTime date, Dictionary<string, double> input)
		{
			string path = @"models\" + name + @"\User\" + model.Name;
			var data = "";
			foreach (var kvp in input)
			{
				string symbol = kvp.Key;
				double value = kvp.Value;
				data += date.ToString("yyyyMMdd") + ":" + symbol + ":" + value.ToString() + "\n";
			}
			MainView.SaveUserData(path, data, false, true);
		}

		private void clearData(string name, Model model)
		{
			string path = @"models\" + name + @"\User\" + model.Name;
			MainView.DeleteUserData(path);
		}

		private Tuple<int, int> getRankingPercents(Model model)
		{
			var rankingType = model.Ranking;

			var top = 100;
			var bot = 0;
			if (rankingType == "T 10% B 10%") { top = 90; bot = 10; }
			else if (rankingType == "T 20% B 20%") { top = 80; bot = 20; }
			else if (rankingType == "T 30% B 30%") { top = 70; bot = 30; }
			else if (rankingType == "T 40% B 40%") { top = 60; bot = 40; }
			else if (rankingType == "T 50% B 50%") { top = 50; bot = 50; }
			else if (rankingType == "T 60% B 60%") { top = 40; bot = 60; }
			else if (rankingType == "T 70% B 70%") { top = 30; bot = 70; }
			else if (rankingType == "T 80% B 80%") { top = 20; bot = 80; }
			else if (rankingType == "T 90% B 90%") { top = 10; bot = 90; }
			else if (rankingType == "T 100% B 100%") { top = 0; bot = 100; }
			else if (rankingType == "T 10%") { top = 90; }
			else if (rankingType == "T 20%") { top = 80; }
			else if (rankingType == "T 30%") { top = 70; }
			else if (rankingType == "T 40%") { top = 60; }
			else if (rankingType == "T 50%") { top = 50; }
			else if (rankingType == "T 60%") { top = 40; }
			else if (rankingType == "T 70%") { top = 30; }
			else if (rankingType == "T 80%") { top = 20; }
			else if (rankingType == "T 90%") { top = 10; }
			else if (rankingType == "T 100%") { top = 0; }
			else if (rankingType == "B 10%") { bot = 10; }
			else if (rankingType == "B 20%") { bot = 20; }
			else if (rankingType == "B 30%") { bot = 30; }
			else if (rankingType == "B 40%") { bot = 40; }
			else if (rankingType == "B 50%") { bot = 50; }
			else if (rankingType == "B 60%") { bot = 60; }
			else if (rankingType == "B 70%") { bot = 70; }
			else if (rankingType == "B 80%") { bot = 80; }
			else if (rankingType == "B 90%") { bot = 90; }
			else if (rankingType == "B 100%") { bot = 100; }
			return new Tuple<int, int>(top, bot);
		}

		private List<string> getPortfolio(DateTime rebalanceDate, Model model, List<string> tickers, Dictionary<string, double> scores, Dictionary<string, double> predictions)
		{
			List<string> output = new List<string>();

			List<string> topGroup = new List<string>();
			List<string> botGroup = new List<string>();

			if (model.UseRanking)
			{
				// rank lowest to highest
				List<string> rank = null;
				rank = scores.OrderBy(x => x.Value).Select(x => x.Key).ToList();
				var tickerCount = rank.Count;

				//Debug.WriteLine("REBALANCE TIME = " + rebalanceDate.ToString());
				//foreach (var kvp in rank)
				//{
				//    Debug.WriteLine(kvp.Key + " = " + kvp.Value.ToString("###.00"));
				//}

				var pcs = getRankingPercents(model);
				var top = pcs.Item1;
				var bot = pcs.Item2;

				// todo = get atm prediction and filter out tickers
				// we now have the universe of tickers sorted by the rebalance score

				if (scores.Count > 0)
				{
					// assume top 10% -  starting with the tickers with the top rebalance score...  find the 10% of the universe that has a buy atm prediction

					var count = (int)(model.GetPortfolioPercent() / 100 * model.Symbols.Count);
					bool getTopGroup = (top != 100);
					bool getBotGroup = (bot != 0);

					// bool useAtmFilters = model.UseATMFactors && (_ftFilter || _scFilter || _stFilter || _prFilter);

					if (getTopGroup)
					{
						// select from lower half  (the higher score half)
						int idx1 = rank.Count - 1;
						int idx2 = scores.Count / 2;
						for (var idx = idx1; idx >= idx2; idx--)
						{
							var ticker = rank[idx];

							var ok1 = true;
							if (model.UseML)
							{
								ok1 = (predictions.ContainsKey(ticker)) ? predictions[ticker] >= 0.5 : false;
							}

							if (ok1)
							{
								topGroup.Add("T:" + ticker);
								if (topGroup.Count == count) break;
							}
						}
					}

					if (getBotGroup)
					{
						// select from upper half  (the lower score half)
						int idx1 = 0;
						int idx2 = rank.Count / 2;
						for (var idx = idx1; idx <= idx2; idx++)
						{
							var ticker = rank[idx];

							var ok1 = true;
							if (model.UseML)
							{

								ok1 = (predictions.ContainsKey(ticker)) ? predictions[ticker] < 0.5 : false;
							}

							if (ok1)
							{
								botGroup.Add("B:" + ticker);
								if (botGroup.Count == count) break;
							}
						}
					}
				}
			}
			else if (model.UseML)
			{

				topGroup = predictions.Where(x => x.Value > 0.5).Select(x => "T:" + x.Key).ToList();
				botGroup = predictions.Where(x => x.Value < 0.5).Select(x => "B:" + x.Key).ToList();
			}
			else
			{
				topGroup = tickers.Select(x => "T:" + x).ToList();
				botGroup = tickers.Select(x => "B:" + x).ToList();
			}

			output.AddRange(topGroup);
			output.AddRange(botGroup);


			return output;
		}


		private Dictionary<string, double> getPredictions(Model model, DateTime time)
		{
			var output = new Dictionary<string, double>();

			foreach (var symbol in model.Symbols)
			{
				if (_predictionData.ContainsKey(symbol.Ticker))
				{
					if (_predictionData[symbol.Ticker].ContainsKey(time))
					{
						output[symbol.Ticker] = _predictionData[symbol.Ticker][time];
					}
					else
					{
						foreach (var kvp in _predictionData[symbol.Ticker])
						{
							DateTime key = kvp.Key;
							double prediction = kvp.Value;
							output[symbol.Ticker] = prediction;
							if (key >= time)
							{
								break;
							}
						}
					}
				}
			}
			return output;
		}

		private double getNotionalMultiplier(string ticker)
		{
			var notionalValueMultiplier = new Dictionary<string, double>() {
							{"C 1 Comdty", 50},
							{"CC1 Comdty", 10},
							{"CL1 Comdty", 1000},
							{"CT1 Comdty", 500},
                            //{"EC1 Curncy", 1},
                            {"GC1 Comdty", 100},
							{"HG1 Comdty", 250},
							{"KCA Comdty", 375},
							{"NG1 Comdty", 10000},
							{"S 1 Comdty", 50},
							{"SB1 Comdty", 1120},
							{"SIA Comdty", 5000},
                            //{"TY1 Comdty", 1},
                            {"W 1 Comdty", 50}
                            //{"ES1 Index",  1}
                    };
			var output = (notionalValueMultiplier.ContainsKey(ticker)) ? notionalValueMultiplier[ticker] : 1.0;
			return output;
		}

		private double getMarketCap(string symbol, string date = "")
		{
			double output = double.NaN;

			lock (_marketCaps)
			{
				if (_marketCaps.ContainsKey(symbol))
				{
					if (date.Length > 0 && _marketCaps[symbol].ContainsKey(date))
					{
						output = _marketCaps[symbol][date];
					}
					else
					{
						var maxDate = _marketCaps[symbol].Keys.ToList().Max();
						output = _marketCaps[symbol][maxDate];
					}
				}
				else
				{
					var marketCaps = _portfolio2.loadData(symbol, "CUR_MKT_CAP");
					foreach (var kvp in marketCaps)
					{
						var marketCapDate = kvp.Key;
						var marketCap = kvp.Value;
						double marketCapValue;
						if (double.TryParse(marketCap, out marketCapValue))
						{
							if (!_marketCaps.ContainsKey(symbol))
							{
								_marketCaps[symbol] = new Dictionary<string, double>();
							}
							_marketCaps[symbol][marketCapDate] = marketCapValue;
							if (marketCapDate == date)
							{
								output = marketCapValue;
							}
						}
					}
				}
			}
			return output;
		}

		private double getTotalMarketCap(string date = "")
		{
			double output = 0;

			foreach (var symbol in _model.Symbols)
			{
				var symbolCap = getMarketCap(symbol.Ticker, date);
				if (!double.IsNaN(symbolCap))
				{
					output += symbolCap;
				}
			}

			return output;
		}

		private double getTotalPrice(Dictionary<string, List<Bar>> bars, string date = "")
		{
			double output = 0;

			var useVwap = _model.rbNextBarVWAP;

			foreach (var symbol in _model.Symbols)
			{
				var price = getPrice(useVwap, symbol, bars[symbol.Ticker], DateTime.Parse(date));
				if (!double.IsNaN(price))
				{
					output += price;
				}
			}

			return output;
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

		private double getPrice(bool useVwap, Symbol symbol, List<Bar> bars, DateTime time)
		{
			double price = double.NaN;

			if (useVwap)
			{
				var oneDay = new TimeSpan(1, 0, 0, 0);
				var t1 = new DateTime(time.Year, time.Month, time.Day) + oneDay;
				for (var n = 0; n < 4; n++)
				{
					var key = t1.ToString("yyyy-MM-dd");
					if (symbol.VWAP.ContainsKey(key))
					{
						price = symbol.VWAP[key];
						break;
					}
					t1 += oneDay;
				}
			}

			if (double.IsNaN(price))
			{
				var index = bars.Count - 1;

				for (var ii = bars.Count - 1; ii >= 0 && bars[ii].Time > time; ii--)
				{
					index--;
				}

				if (index > 0)
				{
					price = bars[index].Close;
					if (double.IsNaN(price))
					{
						price = bars[index - 1].Close;
					}
				}
			}

			return price;
		}

		/// <summary>
		/// Seeds the internal LockedNav store from an archive dailyNav.csv file.
		/// If sourceModel is specified, uses that model's archive instead.
		/// Always overwrites — archive is the authoritative source of truth.
		/// </summary>
		private void BootstrapLockedNavFromArchive(string modelName, string archiveTimestamp, string sourceModel = null)
		{
			try
			{
				var archiveModel = sourceModel ?? modelName;
				var csvPath = $@"C:\Users\Public\Documents\ATMML\portfolios\reports\{archiveModel}\{archiveTimestamp}\dailyNav.csv";
				if (!File.Exists(csvPath)) return;

				var nav = new Dictionary<DateTime, double>();
				foreach (var line in File.ReadAllLines(csvPath).Skip(1)) // skip header
				{
					if (string.IsNullOrWhiteSpace(line)) continue;
					var parts = line.Split(',');
					if (parts.Length == 2
						&& DateTime.TryParse(parts[0], out var date)
						&& double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
					{
						nav[date] = val;
					}
				}
				if (nav.Count == 0) return;

				var lockedNavPath = $@"decisionlock\{modelName}\LockedNav";
				SaveLockedNav(lockedNavPath, nav);
			}
			catch { }
		}

		private Dictionary<DateTime, double> LoadLockedNav(string path)
		{
			var locked = new Dictionary<DateTime, double>();
			try
			{
				var data = MainView.LoadUserData(path);
				if (string.IsNullOrEmpty(data)) return locked;

				var lines = data.Split('\n');
				foreach (var line in lines)
				{
					if (string.IsNullOrWhiteSpace(line)) continue;
					var parts = line.Split(',');
					if (parts.Length == 2
						&& DateTime.TryParse(parts[0], out var date)
						&& double.TryParse(parts[1], out var nav))
					{
						locked[date] = nav;
					}
				}
			}
			catch (Exception x)
			{
				//Debug.WriteLine($"[NavLock] LoadLockedNav failed: {x.Message}");
			}
			return locked;
		}

		private void SaveLockedNav(string path, Dictionary<DateTime, double> navByDate)
		{
			var sb = new StringBuilder();
			foreach (var kvp in navByDate.OrderBy(k => k.Key))
			{
				sb.AppendLine($"{kvp.Key:yyyy-MM-dd},{kvp.Value:F2}");
			}
			MainView.SaveUserData(path, sb.ToString());
		}

		// ============================================================
		// DECISION LOCK — freezes optimizer output per rebalance date
		// so Bloomberg data revisions cannot retroactively change which
		// positions were held on any historical date.
		// File: decisionlock\[ModelName]\YYYY-MM-DD.csv
		// IMPORTANT: Must NOT be under portfolios\ — deleteModelData() wipes that path.
		// Format per line: groupId<TAB>ticker,signedShares,price
		// ============================================================

		// In-memory set of dates locked during this run.
		// Prevents duplicate saves when MainView.LoadUserData cannot
		// see files written earlier in the same session.
		private readonly HashSet<DateTime> _decisionLockedDates = new HashSet<DateTime>();

		private string DecisionLockPath(DateTime rebalanceDate)
		{
			var folder = $@"decisionlock\{_model.Name}\";
			return folder + rebalanceDate.ToString("yyyy-MM-dd") + ".csv";
		}

		/// <summary>
		/// Returns locked riskSizes for this date if a lock file exists, otherwise null.
		/// Null means no lock — caller must run the optimizer and then call SaveLockedDecisions.
		/// Checks the in-memory set first to handle dates locked earlier in the same run.
		/// </summary>
		private Dictionary<string, double> LoadLockedDecisions(DateTime rebalanceDate)
		{
			// If sector/industry constraints are enabled, never replay locks --
			// always re-run the optimizer so constraints are enforced fresh.
			if (_model.getConstraint("SectorNet").Enable ||
				_model.getConstraint("IndustryNet").Enable ||
				_model.getConstraint("SubIndNet").Enable)
				return null;

			// Check in-memory set first — covers dates written earlier in this same run
			// where MainView.LoadUserData may not yet see the newly written file
			if (_decisionLockedDates.Contains(rebalanceDate))
			{
				//Debug.WriteLine($"[DecisionLock] In-memory hit for {rebalanceDate:yyyy-MM-dd} — skipping optimizer.");
				// Still need to load the actual positions from disk
			}

			try
			{
				var lockPath = DecisionLockPath(rebalanceDate);
				var data = MainView.LoadUserData(lockPath);
				if (string.IsNullOrEmpty(data) && !_decisionLockedDates.Contains(rebalanceDate))
				{
					return null;
				}
				if (string.IsNullOrEmpty(data)) return null;

				var locked = new Dictionary<string, double>();
				foreach (var line in data.Split('\n'))
				{
					if (string.IsNullOrWhiteSpace(line)) continue;
					var commaIdx = line.IndexOf(',');
					if (commaIdx < 0) continue;
					var riskKey = line.Substring(0, commaIdx).Trim();
					var rest = line.Substring(commaIdx + 1).Split(',');
					if (rest.Length < 1) continue;
					if (double.TryParse(rest[0].Trim(), out var shares))
						locked[riskKey] = shares;
				}
				//Debug.WriteLine($"[DecisionLock] LOADED {locked.Count} locked positions for {rebalanceDate:yyyy-MM-dd}");
				return locked;
			}
			catch (Exception x)
			{
				//Debug.WriteLine($"[DecisionLock] Load failed for {rebalanceDate:yyyy-MM-dd}: {x.Message}");
				return null;
			}
		}

		/// <summary>
		/// Saves the optimizer output for this rebalance date. Never overwrites an existing lock.
		/// signedSizes: positive = long, negative = short (for audit readability).
		/// </summary>
		private void SaveLockedDecisions(DateTime rebalanceDate,
										  Dictionary<string, double> signedSizes,
										  List<BetaNeutralRiskEngine.Stock> stocks)
		{
			// Primary guard: in-memory set catches duplicates within the same run
			// even when MainView.LoadUserData cannot yet see freshly written files
			if (_decisionLockedDates.Contains(rebalanceDate))
			{
				//Debug.WriteLine($"[DecisionLock] Already locked in-memory for {rebalanceDate:yyyy-MM-dd} — skipping.");
				return;
			}

			var path = DecisionLockPath(rebalanceDate);

			// Secondary guard: catches duplicates across runs (file already on disk)
			var existing = MainView.LoadUserData(path);
			if (!string.IsNullOrEmpty(existing))
			{
				_decisionLockedDates.Add(rebalanceDate); // sync in-memory set
														 //Debug.WriteLine($"[DecisionLock] Lock already exists on disk for {rebalanceDate:yyyy-MM-dd} — skipping.");
				return;
			}

			var sb = new StringBuilder();
			foreach (var kvp in signedSizes.OrderBy(k => k.Key))
			{
				if (kvp.Value == 0) continue;
				var f = kvp.Key.Split('\t');
				var ticker = f.Length >= 2 ? f[1] : kvp.Key;
				var stock = stocks.FirstOrDefault(s => s.Ticker == ticker);
				var price = stock?.Price ?? 0.0;
				sb.AppendLine($"{kvp.Key},{kvp.Value:F4},{price:F4}");
			}

			MainView.SaveUserData(path, sb.ToString());
			_decisionLockedDates.Add(rebalanceDate); // mark in-memory so this run won't save again

			// Seed the checksum on the very first decision lock write so that
			// any subsequent setting change triggers the mismatch warning.
			var _existingChecksum = MainView.LoadUserData(ChecksumLockPath());
			if (string.IsNullOrEmpty(_existingChecksum)) SaveLockChecksum();
			//Debug.WriteLine($"[DecisionLock] SAVED {signedSizes.Count(k => k.Value != 0)} decisions for {rebalanceDate:yyyy-MM-dd}");
		}

		// ============================================================
		// TRADE SET LOCK — freezes the complete trade history so that
		// Bloomberg data changes cannot alter past transactions.
		// This is the authoritative record sent to allocators.
		// File: portfolios\[ModelName]\TradeSetLock
		// Format: one serialized Trade per line (uses Trade.Serialize())
		// ============================================================

		// ============================================================
		// MODEL CHECKSUM — detects if strategy settings have changed
		// since the trade lock was created. Covers all parameters
		// that would alter trade generation if modified.
		// ============================================================

		private string ChecksumLockPath()
		{
			return $@"tradelock\{_model.Name}_checksum";
		}

		private string ComputeModelChecksum()
		{
			var sb = new StringBuilder();

			// Signal weights
			if (_model.Groups != null && _model.Groups.Count > 0)
			{
				sb.Append($"MTNT={_model.Groups[0].MTNT}|");
				sb.Append($"MTP={_model.Groups[0].MTP}|");
				sb.Append($"MTFT={_model.Groups[0].MTFT}|");
				sb.Append($"Leverage={_model.Groups[0].Leverage}|");
			}

			// Risk constraints
			foreach (var key in new[] { "BetaNeutral", "MktNeutral", "SectorNet", "AlphaWeighting",
				"RiskParity", "SingleNameCapAbs", "VolNeutral" })
			{
				var c = _model.getConstraint(key);
				if (c != null)
					sb.Append($"{key}={c.Enable},{c.Value}|");
			}

			// Execution & model settings
			sb.Append($"Commission={_model.UseCommission},{_model.CommissionAmt}|");
			sb.Append($"RankingInterval={_model.RankingInterval}|");
			sb.Append($"UseBetaHedge={_model.UseBetaHedge}|");

			// Symbol universe (sorted for stability)
			if (_model.Symbols != null)
			{
				var tickers = string.Join(",", _model.Symbols.Select(s => s.Ticker).OrderBy(t => t));
				sb.Append($"Symbols={tickers}|");
			}

			// Hash to a compact string
			using (var sha = System.Security.Cryptography.SHA256.Create())
			{
				var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
				var hash = sha.ComputeHash(bytes);
				return BitConverter.ToString(hash).Replace("-", "").Substring(0, 16);
			}
		}

		private void SaveLockChecksum()
		{
			try
			{
				var checksum = ComputeModelChecksum();
				MainView.SaveUserData(ChecksumLockPath(), checksum);
				//Debug.WriteLine($"[Checksum] Saved: {checksum}");
			}
			catch (Exception ex)
			{
				//Debug.WriteLine($"[Checksum] Save failed: {ex.Message}");
			}
		}

		private bool VerifyLockChecksum()
		{
			try
			{
				var saved = MainView.LoadUserData(ChecksumLockPath());
				if (string.IsNullOrEmpty(saved)) return true; // no checksum yet — first run, allow
				var current = ComputeModelChecksum();
				if (saved.Trim() == current) return true;
				//Debug.WriteLine($"[Checksum] MISMATCH — saved: {saved.Trim()}, current: {current}");
				return false;
			}
			catch
			{
				return true; // if checksum file missing, allow run
			}
		}


		/// <summary>
		/// One-time utility: rebuilds the TradeSetLock from TradeSetLock_Reconstructed.txt
		/// which was generated from Transactions.csv and CampaignDetail.csv.
		/// Call this once on startup if TradeSetLock is empty/missing.
		/// File format per line (tab-separated):
		///   Ticker|Direction|OpenDate|OpenPrice|CloseDate|ClosePrice|Cost|Profit|date1:shares1|date2:shares2...
		/// </summary>
		public void RebuildTradeSetLockFromCSV(string reconstructedFilePath)
		{
			try
			{
				if (!System.IO.File.Exists(reconstructedFilePath))
				{
					//Debug.WriteLine($"[RebuildLock] File not found: {reconstructedFilePath}");
					return;
				}

				var lines = System.IO.File.ReadAllLines(reconstructedFilePath);
				var trades = new List<Trade>();
				var errors = 0;

				foreach (var line in lines)
				{
					if (string.IsNullOrWhiteSpace(line)) continue;
					try
					{
						var parts = line.Split('\t');
						if (parts.Length < 9) continue;

						var ticker = parts[0];
						var direction = int.Parse(parts[1]);
						var openDate = DateTime.Parse(parts[2]);
						var openPrice = double.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture);
						var closeDate = string.IsNullOrEmpty(parts[4]) ? default(DateTime) : DateTime.Parse(parts[4]);
						var closePrice = string.IsNullOrEmpty(parts[5]) || parts[5] == "0.0000" ? double.NaN : double.Parse(parts[5], System.Globalization.CultureInfo.InvariantCulture);
						var cost = double.Parse(parts[6], System.Globalization.CultureInfo.InvariantCulture);
						var profit = double.Parse(parts[7], System.Globalization.CultureInfo.InvariantCulture);

						// Reconstruct Shares dictionary
						var sharesDict = new Dictionary<DateTime, double>();
						var shareEntries = parts[8].Split('|');
						foreach (var entry in shareEntries)
						{
							if (string.IsNullOrWhiteSpace(entry)) continue;
							var kv = entry.Split(':');
							if (kv.Length == 2 && DateTime.TryParse(kv[0], out var sd)
								&& double.TryParse(kv[1], System.Globalization.NumberStyles.Any,
									System.Globalization.CultureInfo.InvariantCulture, out var sv))
								sharesDict[sd] = sv;
						}

						var isOpen = closeDate == default(DateTime);

						// Build IdeaIdType for the trade id
						var tradeId = new tmsg.IdeaIdType();
						tradeId.BloombergIdSpecified = false;
						tradeId.ThirdPartyId = $"REBUILD_{trades.Count + 1}";

						var initShares = sharesDict.Count > 0 ? sharesDict.Values.Max() : 0;
						var trade = new Trade("0", ticker, tradeId, direction,
											initShares,
											openDate, openPrice, closeDate, closePrice, isOpen);

						// Populate Shares dictionary (read-only property — must use indexer)
						foreach (var kvp in sharesDict)
							trade.Shares[kvp.Key] = kvp.Value;

						trade.Cost = cost;
						trade.Profit = profit;

						trades.Add(trade);
					}
					catch (Exception ex)
					{
						errors++;
						//Debug.WriteLine($"[RebuildLock] Parse error on line: {ex.Message}");
					}
				}

				//Debug.WriteLine($"[RebuildLock] Parsed {trades.Count} trades, {errors} errors.");

				if (trades.Count > 0)
				{
					SaveLockedTradeSet(trades);
					SaveLockChecksum();
					_tradeSetRebuilt = true; // signal to force-refresh LockedNav this run
											 //Debug.WriteLine($"[RebuildLock] TradeSetLock rebuilt successfully with {trades.Count} trades.");
				}
				else
				{
					//Debug.WriteLine("[RebuildLock] No trades parsed — lock NOT written.");
				}
			}
			catch (Exception ex)
			{
				//Debug.WriteLine($"[RebuildLock] Failed: {ex.Message}");
			}
		}

		private string TradeSetLockPath()
		{
			// IMPORTANT: Must NOT be under portfolios\ or portfolios	rades\
			// because deleteModelData() wipes those paths before every run.
			return $@"tradelock\{_model.Name}";
		}

		/// <summary>
		/// Returns the locked trade set if it exists, otherwise null.
		/// When locked trades exist, the main loop is skipped entirely.
		/// </summary>
		private List<Trade> LoadLockedTrades()
		{
			try
			{
				var data = MainView.LoadUserData(TradeSetLockPath());
				if (string.IsNullOrEmpty(data)) return null;

				var trades = new List<Trade>();
				foreach (var line in data.Split('\n'))
				{
					if (string.IsNullOrWhiteSpace(line)) continue;
					var trade = new Trade();
					trade.Deserialize(line);
					trades.Add(trade);
				}
				//Debug.WriteLine($"[TradeSetLock] LOADED {trades.Count} locked trades.");
				return trades;
			}
			catch (Exception x)
			{
				//Debug.WriteLine($"[TradeSetLock] Load failed: {x.Message}");
				return null;
			}
		}

		/// <summary>
		/// Saves the complete trade set as the authoritative locked record.
		/// Never overwrites — once locked, always locked.
		/// For production: call SaveLockedTradeSet after each new rebalance
		/// to append new trades and re-lock the updated set.
		/// </summary>
		private void SaveLockedTradeSet(List<Trade> trades)
		{
			try
			{
				var sb = new StringBuilder();
				foreach (var trade in trades.OrderBy(t => t.OpenDateTime).ThenBy(t => t.Ticker))
					sb.AppendLine(trade.Serialize());
				MainView.SaveUserData(TradeSetLockPath(), sb.ToString());
				//Debug.WriteLine($"[TradeSetLock] SAVED {trades.Count} trades to lock.");
			}
			catch (Exception x)
			{
				//Debug.WriteLine($"[TradeSetLock] Save failed: {x.Message}");
			}
		}

		private Dictionary<string, double> getShareAmounts(Model model, string benchmarkSymbol, DateTime rebalanceDate, double value, int index, List<string> portfolio, Dictionary<string, List<Bar>> bars)
		{

			var shares = new Dictionary<string, double>();
			var weights1 = new Dictionary<string, double>();

			var dateKey = rebalanceDate.ToString("yyyy-MM-dd");
			Model.PortfolioWeightType portfolioWeight = model.PortfolioWeight;

			// size recommendations
			bool useMaxLongPercent = (model.MaxPercentAmt != 0);
			bool useMaxShortPercent = (model.MaxPercentAmt != 0);
			bool useMaxLongDollar = (model.MaxDollarAmt != 0);
			bool useMaxShortDollar = (model.MaxDollarAmt != 0);
			double maxLongPercent = model.MaxPercentAmt; // model.MaxLongPercent
			double maxShortPercent = model.MaxPercentAmt; // model.MaxShortPercent
			double maxLongDollars = model.MaxDollarAmt; // model.MaxLongDollar
			double maxShortDollars = model.MaxDollarAmt; // model.MaxShortDollar
			double maxLongPercentDollars = value * (maxLongPercent / 100.0);
			double maxShortPercentDollars = value * (maxShortPercent / 100.0);

			if (model.SizeRecommendation == SizeRecommendation.ATR)
			{
				var percent = model.MaxPercentAmt;

				var period = model.ATRPeriod;
				var index1 = Math.Max(1, index - period);
				var index2 = index;
				foreach (var item in portfolio)
				{
					string[] fields = item.Split(':');
					string type = fields[0];
					string symbol = fields[1];


					var t1 = bars[symbol][index].Time;
					var sum = 0.0;
					for (int ii = index1; ii < index2; ii++)
					{
						var prvCl = bars[symbol][ii - 1].Close;
						var high = bars[symbol][ii].High;
						var low = bars[symbol][ii].Low;
						var tr = Math.Max(high, prvCl) - Math.Min(low, prvCl);
						sum += tr;
					}
					var atr = sum / period;

					var multiplier = getNotionalMultiplier(symbol);
					weights1[symbol] = 1;
					shares[symbol] = Math.Floor((value * percent / 100.0) / (atr * multiplier));
				}
			}
			else if (portfolioWeight == Model.PortfolioWeightType.Equal)
			{
				int longCount = 0;
				int shortCount = 0;
				Dictionary<string, double> closes = new Dictionary<string, double>();
				Dictionary<string, int> directions = new Dictionary<string, int>();
				foreach (var item in portfolio)
				{
					string[] fields = item.Split(':');
					string type = fields[0];
					string symbol = fields[1];
					int direction = (type == "T") ? 1 : -1;
					var close = double.NaN;
					if (bars.ContainsKey(symbol))
					{
						close = getTradePrice(model, bars[symbol], index);
						if (!double.IsNaN(close))
						{
							if (direction > 0)
							{
								if (!model.UseMaxNumberLongs || longCount < model.MaxNumberLongs)
								{
									longCount++;
								}
							}
							else
							{
								if (!model.UseMaxNumberShorts || shortCount < model.MaxNumberShorts)
								{
									shortCount++;
								}
							}
						}
					}
					var multiplier = getNotionalMultiplier(symbol);
					closes[symbol] = multiplier * close;
					directions[symbol] = (type == "T") ? 1 : -1;
				}

				double equalInvestment = value / (longCount + shortCount);

				//Trace.WriteLine(rebalanceDate.ToString("yyyy MM dd") + " NAV = " + value + " count = " + longCount + " " + shortCount + " investment = " + equalInvestment);

				foreach (var kvp in closes)
				{
					var symbol = kvp.Key;
					var close = kvp.Value;
					var direction = directions[symbol];


					double investment = equalInvestment;

					if (direction > 0)
					{
						if (useMaxLongDollar) investment = Math.Min(investment, maxLongDollars);
						if (useMaxLongPercent) investment = Math.Min(investment, maxLongPercentDollars);
					}
					else if (direction < 0)
					{
						if (useMaxShortDollar) investment = Math.Min(investment, maxShortDollars);
						if (useMaxShortPercent) investment = Math.Min(investment, maxShortPercentDollars);
					}

					weights1[symbol] = investment;
					shares[symbol] = double.IsNaN(close) ? 0 : Math.Floor(investment / close);
				}
			}
			else
			{
				bool useMarketCap = (model.PortfolioWeight == Model.PortfolioWeightType.MarketCap);
				bool useVwap = model.rbNextBarVWAP;

				double totalWeight = useMarketCap ? getTotalMarketCap(dateKey) : getTotalPrice(bars, dateKey);

				Dictionary<string, double> weights = new Dictionary<string, double>();
				Dictionary<string, double> closes = new Dictionary<string, double>();
				Dictionary<string, int> directions = new Dictionary<string, int>();

				foreach (var item in portfolio)
				{
					string[] fields = item.Split(':');
					string type = fields[0];
					string ticker = fields[1];
					var symbol = getSymbol(ticker);

					double weight = 0;
					var symbolWeight = useMarketCap ? getMarketCap(ticker, dateKey) : getPrice(useVwap, symbol, bars[ticker], DateTime.Parse(dateKey));
					if (!double.IsNaN(symbolWeight))
					{
						weight = symbolWeight / totalWeight;
					}

					var close = double.NaN;
					if (bars.ContainsKey(ticker))
					{
						close = getTradePrice(model, bars[ticker], index);
					}

					weights[ticker] = weight;
					closes[ticker] = close;
					directions[ticker] = (type == "T") ? 1 : -1;
				}

				var sortedWeights = weights.ToList();
				sortedWeights.Sort((kvp1, kvp2) => -kvp1.Value.CompareTo(kvp2.Value));

				int longCount = 0;
				int shortCount = 0;
				double weightSum = 0;
				foreach (var kvp in sortedWeights)
				{
					var symbol = kvp.Key;
					var weight = kvp.Value;
					var close = closes[symbol];
					var direction = directions[symbol];
					if (!double.IsNaN(weight) && !double.IsNaN(close))
					{
						bool ok = false;
						if (direction > 0)
						{
							if (!model.UseMaxNumberLongs || longCount < model.MaxNumberLongs)
							{
								longCount++;
								ok = true;
							}
						}
						else
						{
							if (!model.UseMaxNumberShorts || shortCount < model.MaxNumberShorts)
							{
								shortCount++;
								ok = true;
							}
						}

						if (ok) weightSum += weight;
						else directions[symbol] = 0;
					}
				}

				foreach (var kvp in sortedWeights)
				{
					var symbol = kvp.Key;
					var weight = kvp.Value;
					var close = closes[symbol];
					var direction = directions[symbol];
					if (!double.IsNaN(weight) && !double.IsNaN(close) && direction != 0)
					{
						var marketCapWeight = weight / weightSum * value;

						if (direction > 0)
						{
							if (useMaxLongDollar) marketCapWeight = Math.Min(marketCapWeight, maxLongDollars);
							if (useMaxLongPercent) marketCapWeight = Math.Min(marketCapWeight, maxLongPercentDollars);
						}
						else if (direction < 0)
						{
							if (useMaxShortDollar) marketCapWeight = Math.Min(marketCapWeight, maxShortDollars);
							if (useMaxShortPercent) marketCapWeight = Math.Min(marketCapWeight, maxShortPercentDollars);
						}

						weights1[symbol] = marketCapWeight;
						shares[symbol] = Math.Floor(marketCapWeight / close);
					}
					else
					{
						weights1[symbol] = 0;
						shares[symbol] = 0;
					}
				}

			}

			double totalWeight1 = weights1.Sum(x => x.Value);
			var keys = weights1.Keys.ToList();
			keys.ForEach(x => weights1[x] /= totalWeight1);

			saveData("weights", model, rebalanceDate, weights1);
			saveData("shares", model, rebalanceDate, shares);
			return shares;
		}

		private PortfolioValue getPortfolioValue(Model model, int index, List<string> portfolio, Dictionary<string, double> shares, Dictionary<string, List<Bar>> bars, double cash)
		{
			var output = new PortfolioValue();
			output.longValue = 0;
			output.shortValue = 0;
			output.cashValue = cash;

			foreach (var item in portfolio)
			{
				string[] fields = item.Split(':');
				string type1 = fields[0];
				string symbol = fields[1];
				double close = double.NaN;
				var multiplier = getNotionalMultiplier(symbol);
				if (bars.ContainsKey(symbol))
				{
					close = getLastClose(bars[symbol], index);
					if (!double.IsNaN(close))
					{
						double value = multiplier * (close * shares[symbol]);
						if (type1 == "T") output.longValue += value;
						else if (type1 == "B") output.shortValue += value;
						//System.Diagnostics.Debug.WriteLine(bars[symbol][index].Time.ToString("yyyyMMdd") + " " + symbol + " " + close.ToString() + " " + shares[symbol].ToString() + " " + (close * shares[symbol]).ToString());
					}
					else
					{
						// System.Diagnostics.Debug.WriteLine("getPortfolioValue could not get close of " + symbol + " at " + bars[symbol][index].Time.ToString());
					}
				}
				else
				{
					//System.Diagnostics.Debug.WriteLine("getPortfolioValue could not get bars of " + symbol);
				}
			}
			return output;
		}

		//private void calculatePortfolioReturn()
		//{
		//    lock (_scoreCache)
		//    {
		//        _scoreCache.Clear();
		//    }

		//    try
		//    {
		//        string benchmarkSymbol = _model.Benchmark;

		//        string interval = getInterval(); // "Daily";

		//        Dictionary<string, List<Bar>> bars = new Dictionary<string, List<Bar>>();
		//        bars[benchmarkSymbol] = getBars(benchmarkSymbol, interval, BarServer.MaxBarCount);

		//        List<DateTime> times = bars[benchmarkSymbol].Select(x => x.Time).ToList();
		//        DateTime date1 = _model.DataRange.Time1; // getStartDate(interval, lowestInterval); // mlPredict
		//        DateTime date2 = _model.DataRange.Time2; // DateTime.MaxValue;

		//        List<Symbol> universe = _model.Symbols;
		//        if (universe != null)
		//        {
		//            var rebalancePortfolioStream = new StringBuilder();
		//            var portfolioStream = new StringBuilder();

		//            foreach (var symbol1 in universe)
		//            {
		//                var ticker = symbol1.Ticker;
		//                bars[ticker] = getBars(ticker, interval, BarServer.MaxBarCount, bars[benchmarkSymbol]);
		//            }

		//            List<double> benchmarkValues = new List<double>();
		//            List<double> portfolioValues = new List<double>();

		//            double benchmarkPrice1 = double.NaN;

		//            DateTime previousTime = new DateTime();

		//            DateTime lastRebalanceDate = new DateTime();

		//            double initialPortfolioValue = 1000000;

		//            PortfolioValue portfolioValue = new PortfolioValue();
		//            portfolioValue.longValue = 0;
		//            portfolioValue.shortValue = 0;
		//            portfolioValue.cashValue = initialPortfolioValue;

		//            var rankingType = _model.Ranking;
		//            double top = 0;
		//            double bot = 0;
		//            if (rankingType == "T 10% B 10%") { top = 1; bot = 1; }
		//            else if (rankingType == "T 20% B 20%") { top = 1; bot = 1; }
		//            else if (rankingType == "T 30% B 30%") { top = 1; bot = 1; }
		//            else if (rankingType == "T 40% B 40%") { top = 1; bot = 1; }
		//            else if (rankingType == "T 50% B 50%") { top = 1; bot = 1; }
		//            else if (rankingType == "T 60% B 60%") { top = 1; bot = 1; }
		//            else if (rankingType == "T 70% B 70%") { top = 1; bot = 1; }
		//            else if (rankingType == "T 80% B 80%") { top = 1; bot = 1; }
		//            else if (rankingType == "T 90% B 90%") { top = 1; bot = 1; }
		//            else if (rankingType == "T 100% B 100%") { top = 1; bot = 1; }
		//            else if (rankingType == "T 10%") { top = 1; }
		//            else if (rankingType == "T 20%") { top = 1; }
		//            else if (rankingType == "T 30%") { top = 1; }
		//            else if (rankingType == "T 40%") { top = 1; }
		//            else if (rankingType == "T 50%") { top = 1; }
		//            else if (rankingType == "T 60%") { top = 1; }
		//            else if (rankingType == "T 70%") { top = 1; }
		//            else if (rankingType == "T 80%") { top = 1; }
		//            else if (rankingType == "T 90%") { top = 1; }
		//            else if (rankingType == "T 100%") { top = 1; }
		//            else if (rankingType == "B 10%") { bot = 1; }
		//            else if (rankingType == "B 20%") { bot = 1; }
		//            else if (rankingType == "B 30%") { bot = 1; }
		//            else if (rankingType == "B 40%") { bot = 1; }
		//            else if (rankingType == "B 50%") { bot = 1; }
		//            else if (rankingType == "B 60%") { bot = 1; }
		//            else if (rankingType == "B 70%") { bot = 1; }
		//            else if (rankingType == "B 80%") { bot = 1; }
		//            else if (rankingType == "B 90%") { bot = 1; }
		//            else if (rankingType == "B 100%") { bot = 1; }

		//            double initialLongPortfolioValue = top * initialPortfolioValue;
		//            double initialShortPortfolioValue = bot * initialPortfolioValue;

		//            double netAssetValue = initialPortfolioValue;
		//            double netAssetValueAtStartOfRebalancePeriod = initialPortfolioValue;

		//            double returnValue = 0;
		//            int rebalanceBarCount = 0;

		//            List<DateTime> portfolioTimes = new List<DateTime>();
		//            List<DateTime> monthDates = new List<DateTime>();
		//            List<double> monthValues = new List<double>();

		//            double averageTurnoverSum = 0;
		//            int averageTurnoverCount = 0;

		//            bool firstQuarter = true;
		//            double netAssetValueAtStartOfQuarter = netAssetValue;

		//            List<string> rebalancePortfolio = new List<string>();

		//            List<string> newPortfolio = new List<string>();
		//            List<string> oldPortfolio = new List<string>();

		//            Dictionary<string, double> oldShares = new Dictionary<string, double>();
		//            Dictionary<string, double> newShares = new Dictionary<string, double>();

		//            var strategyData = _strategyData; 

		//            var trades = new List<Trade>();

		//            var stoppedOutTrades = new List<Trade>();

		//            var barCount = bars[benchmarkSymbol].Count;

		//            for (int ii = 0; ii < barCount; ii++)
		//            {
		//                DateTime time = times[ii];
		//                if (time >= time1 && time <= time2)
		//                {
		//                    DateTime nextTime = (ii < times.Count - 1) ? times[ii + 1] : time + new TimeSpan(1, 0, 0, 0);

		//                    double close = getTradePrice(_model, bars[benchmarkSymbol], ii);
		//                    double value = double.NaN;
		//                    if (!double.IsNaN(close))
		//                    {
		//                        if (double.IsNaN(benchmarkPrice1))
		//                        {
		//                            benchmarkPrice1 = close;
		//                        }

		//                        if (!double.IsNaN(benchmarkPrice1))
		//                        {
		//                            double price2 = close;
		//                            value = 100 * (price2 - benchmarkPrice1) / benchmarkPrice1;
		//                        }
		//                    }
		//                    benchmarkValues.Add(value);
		//                    portfolioTimes.Add(time);

		//                    oldPortfolio = new List<string>(newPortfolio);
		//                    oldShares = new Dictionary<string, double>(newShares);

		//                    if (rebalance(_model, time, nextTime))
		//                    {
		//                        stoppedOutTrades.Clear();

		//                        lastRebalanceDate = time;

		//                        returnValue = 100 * (netAssetValue - netAssetValueAtStartOfRebalancePeriod) / netAssetValueAtStartOfRebalancePeriod;
		//                        for (int idx = 0; idx < rebalanceBarCount; idx++)
		//                        {
		//                            monthValues.Add(returnValue);
		//                        }
		//                        rebalanceBarCount = 0;

		//                        var portfolios = getPortfolio(_model, ii, time, times);
		//                        rebalancePortfolio = portfolios.Item2;

		//                        //Trace.WriteLine("Rebalance " + time.ToString("yyyyMMdd") + " " + rebalancePortfolio.Count + " " + (int)netAssetValue);
		//                        //rebalancePortfolio.ForEach(x => Trace.WriteLine("    " + x));

		//                        //int turnoverCount = getTurnoverCount(oldPortfolio, rebalancePortfolio);
		//                        //if (oldPortfolio.Count > 0)
		//                        //{
		//                        //    averageTurnoverSum += 100.0 * turnoverCount / oldPortfolio.Count;
		//                        //    averageTurnoverCount++;
		//                        //}

		//                        double navForShares = netAssetValue * _model.GrossLeverage;
		//                        newShares = getShareAmounts(_model, benchmarkSymbol, time, navForShares, ii, rebalancePortfolio, bars);

		//                        var rebalanceInvestments = new Dictionary<string, double>();
		//                        foreach (var symbol in rebalancePortfolio)
		//                        {
		//                            var fields = symbol.Split(':');
		//                            var ticker = fields[1];
		//                            var shares = newShares[ticker];
		//                            var price = getPrice(bars[ticker], time);
		//                            rebalanceInvestments[ticker] = shares * price;
		//                        }
		//                        saveModelPortfolio(rebalancePortfolioStream, time, rebalancePortfolio, rebalanceInvestments);
		//                    }

		//                    double fees = 0;
		//                    if ((time.Month - 1) / 3 != (nextTime.Month - 1) / 3)
		//                    {
		//                        double timeFactor = firstQuarter ? (time - time1).TotalDays / 90.0 : 1;
		//                        fees += timeFactor * (netAssetValue * (_model.ManagementFee / (4 * 100)));

		//                        if (netAssetValue > netAssetValueAtStartOfQuarter)
		//                        {
		//                            fees += (netAssetValue - netAssetValueAtStartOfQuarter) * (_model.PerformanceFee / 100);
		//                        }
		//                        netAssetValueAtStartOfQuarter = netAssetValue;
		//                        firstQuarter = false;
		//                    }

		//                    newPortfolio = (_model.UseATMStrategies) ? strategyFilter(barCount - 1 - ii, rebalancePortfolio, strategyData) : rebalancePortfolio;
		//                    newPortfolio = newPortfolio.Where(x => { var f = x.Split(':'); return !double.IsNaN(newShares[f[1]]); }).ToList();

		//                    var entries = newPortfolio.Where(x => !oldPortfolio.Any(y => y.CompareTo(x) == 0)).ToList();
		//                    var exits = oldPortfolio.Where(x => !newPortfolio.Any(y => y.CompareTo(x) == 0)).ToList();

		//                    var transactions = getTransactions(oldPortfolio, newPortfolio, oldShares, newShares);

		//                    entries.ForEach(x =>
		//                    {
		//                        var fields = x.Split(':');
		//                        var direction = (fields[0] == "B") ? -1 : 1;
		//                        var ticker = fields[1];

		//                        var stoppedOut = (stoppedOutTrades.Find(y => ticker == y.Ticker) != null);

		//                        if (!stoppedOut)
		//                        {
		//                            var price = getPrice(bars[ticker], time);
		//                            if (!double.IsNaN(price))
		//                            {
		//                                var shares = newShares[ticker];
		//                                var investment = shares * price;

		//                                var id = new tmsg.IdeaIdType();
		//                                id.BloombergIdSpecified = false;
		//                                id.ThirdPartyId = Guid.NewGuid().ToString();

		//                                var trade = new Trade(_model.Name, ticker, id, direction, investment, time, price, time2, double.NaN, true);
		//                                trades.Add(trade);
		//                            }
		//                        }
		//                    });

		//                    exits.ForEach(x =>
		//                    {
		//                        var fields = x.Split(':');
		//                        var direction = (fields[0] == "B") ? -1 : 1;
		//                        var ticker = fields[1];
		//                        var trade = trades.Where(y => y.Ticker == ticker && y.IsOpen()).FirstOrDefault();
		//                        if (trade != null)
		//                        {
		//                            var price = getPrice(bars[ticker], time);
		//                            trade.CloseDateTime = time;
		//                            trade.ClosePrice = price;
		//                            trade.SetOpen(false);
		//                        }
		//                    });

		//                    if (_model.UseStopLossPercent && _model.StopLossPercent != null && _model.StopLossPercent.Length > 0)
		//                    {
		//                        stoppedOutTrades.AddRange(trades.Where(x =>
		//                        {
		//                            bool stoppedOut = false;

		//                            if (x.IsOpen())
		//                            {
		//                                var price = getPrice(bars[x.Ticker], time);
		//                                stoppedOut = (Math.Sign(x.Direction) * 100 * (price - x.EntryPrice)) / x.EntryPrice <= -double.Parse(_model.StopLossPercent);
		//                                if (stoppedOut)
		//                                {
		//                                    x.CloseDateTime = time;
		//                                    x.ClosePrice = price;
		//                                    x.SetOpen(false);
		//                                }
		//                            }
		//                            return stoppedOut;
		//                        }).ToList());
		//                    }

		//                    var investments = new Dictionary<string, double>();
		//                    foreach (var symbol in newPortfolio)
		//                    {
		//                        var fields = symbol.Split(':');
		//                        var ticker = fields[1];
		//                        var shares = newShares[ticker];
		//                        var price = getPrice(bars[ticker], time);
		//                        investments[ticker] = shares * price;
		//                    }

		//                    var entryAmounts = investments.Where(x => entries.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value);
		//                    double costs = getTranactionCosts(transactions, entryAmounts);
		//                    double cash = getCash(ii, transactions, bars, portfolioValue.cashValue) - costs - fees;
		//                    portfolioValue = getPortfolioValue(_model, ii, newPortfolio, newShares, bars, cash);


		//                    //transactions.ToList().ForEach(x => { if (x.Value != 0) Trace.WriteLine(time.ToString("yyyyMMdd") + " " + x.Key + " " + (int)x.Value + " " + (int)(x.Value * bars[x.Key][ii].Close)); });


		//                    netAssetValue = portfolioValue.cashValue + portfolioValue.longValue - portfolioValue.shortValue;

		//                    if (netAssetValue != 1000000)
		//                    {
		//                        Trace.WriteLine(time.ToString("yyyyMMdd") + " " + netAssetValue + " " + portfolioValue.cashValue + " " + portfolioValue.longValue + " " + portfolioValue.shortValue);
		//                    }

		//                    if (rebalanceBarCount == 0)
		//                    {
		//                        netAssetValueAtStartOfRebalancePeriod = netAssetValue;
		//                    }

		//                    returnValue = 100 * (netAssetValue - initialPortfolioValue) / initialPortfolioValue;
		//                    portfolioValues.Add(returnValue);

		//                    saveModelPortfolio(portfolioStream, time, newPortfolio, investments);

		//                    monthDates.Add(time);
		//                    previousTime = time;

		//                    rebalanceBarCount++;
		//                }
		//            }

		//            if (rebalanceBarCount > 0)
		//            {
		//                returnValue = 100 * (netAssetValue - netAssetValueAtStartOfRebalancePeriod) / netAssetValueAtStartOfRebalancePeriod;
		//                for (int idx = 0; idx < rebalanceBarCount; idx++)
		//                {
		//                    monthValues.Add(returnValue);
		//                }
		//                rebalanceBarCount = 0;
		//            }

		//            for (int ii = monthValues.Count - 1; ii > 0; ii--)
		//            {
		//                monthValues[ii] = monthValues[ii - 1];
		//            }

		//            var _currentPortfolio = rebalancePortfolio;
		//            var _portfolioTimes = portfolioTimes;
		//            var _benchmarkValues = benchmarkValues;
		//            var _portfolioValues = portfolioValues;
		//            var _portfolioMonthTimes = monthDates;
		//            var _portfolioMonthValues = monthValues;

		//            //loadCompareValues(_model, time1, time2);

		//            // save data
		//            saveList<string>(_model.Name + " CurrentPortFolio", _currentPortfolio);
		//            saveList<DateTime>(_model.Name + " PortfolioTimes", _portfolioTimes);
		//            saveList<double>(_model.Name + " BenchmarkValues", _benchmarkValues);
		//            saveList<double>(_model.Name + " PortfolioValues", _portfolioValues);
		//            saveList<DateTime>(_model.Name + " PortfolioMonthTimes", _portfolioMonthTimes);
		//            saveList<double>(_model.Name + " PortfolioMonthValues", _portfolioMonthValues);
		//            //saveList<double>(_model.Name + " CompareToValues", _compareToValues);

		//            //_averageTurnover = averageTurnoverSum / averageTurnoverCount;

		//            var path1 = @"portfolios\rebalance_\" + _model.Name;
		//            var data1 = rebalancePortfolioStream.ToString();
		//            MainView.SaveUserData(path1, data1);

		//            var path2 = @"portfolios\" + _model.Name;
		//            var data2 = portfolioStream.ToString();
		//            MainView.SaveUserData(path2, data2);

		//            string path3 = @"portfolios\trades\" + _model.Name;
		//            var sb = new StringBuilder();
		//            trades.ForEach(x => sb.Append(x.Serialize() + "\n"));
		//            var data3 = sb.ToString();
		//            MainView.SaveUserData(path3, data3);
		//        }
		//    }
		//    catch (Exception x)
		//    {
		//        //Trace.WriteLine("calclatePortfolioReturn exception: " + x.Message);
		//    }
		//}

		private List<string> strategyFilter(int ago, List<string> rebalancePortfolio, Dictionary<string, Tuple<Series, Series>> timingData)
		{
			var output = new List<string>();
			foreach (var symbol in rebalancePortfolio)
			{
				string[] fields = symbol.Split(':');
				var direction = fields[0];
				var ticker = fields[1];
				if (timingData.ContainsKey(ticker))
				{
					int index1 = timingData[ticker].Item1.Count - 1 - ago;
					int index2 = timingData[ticker].Item2.Count - 1 - ago;
					var value1 = (index1 >= 0) ? timingData[ticker].Item1[index1] : 0;  // long filter  = allow trade if value is true (1.0)
					var value2 = (index2 >= 0) ? timingData[ticker].Item2[index2] : 0;  // short filter = allow trade if value is true (1.0)
					if ((direction == "T" && value1 == 1) || (direction == "B" && value2 == 1))
					{
						output.Add(symbol);
					}
				}
			}
			return output;
		}

		private Dictionary<string, double> getTransactions(List<string> oldPortfolio, List<string> newPortfolio, Dictionary<string, double> oldShares, Dictionary<string, double> newShares)
		{
			Dictionary<string, double> transactions = new Dictionary<string, double>();

			foreach (var symbol in oldPortfolio)
			{
				string[] fields = symbol.Split(':');
				var direction = fields[0];
				var ticker = fields[1];
				var shares = (oldShares.ContainsKey(ticker)) ? (direction == "T") ? -oldShares[ticker] : oldShares[ticker] : 0;
				transactions[ticker] = shares;
			}

			foreach (var symbol in newPortfolio)
			{
				string[] fields = symbol.Split(':');
				var direction = fields[0];
				var ticker = fields[1];
				var oldAmount = (transactions.ContainsKey(ticker)) ? transactions[ticker] : 0;
				var newAmount = (direction == "T") ? newShares[ticker] : -newShares[ticker];
				transactions[ticker] = newAmount + oldAmount;
			}

			return transactions;
		}

		private double getCash(int index, Dictionary<string, double> transactions, Dictionary<string, List<Bar>> bars, double input)
		{
			double output = input;
			foreach (var kvp in transactions)
			{
				var ticker = kvp.Key;
				var shares = kvp.Value;
				var multiplier = getNotionalMultiplier(ticker);
				if (bars.ContainsKey(ticker))
				{
					double close = getLastClose(bars[ticker], index);
					if (!double.IsNaN(close))
					{
						output -= multiplier * (close * shares);
					}
				}
			}
			return output;
		}

		private double getTranactionCosts(Dictionary<string, double> transactions, Dictionary<string, double> entryInvestments)
		{
			double commission = 0;
			if (_model.UseCommission)
			{
				foreach (var shares in transactions.Values)
				{
					commission += (Math.Abs(shares) * _model.CommissionAmt) / 100;
				}
			}

			double priceImpact = 0;
			if (_model.UsePriceImpactAmt)
			{
				foreach (var dollars in entryInvestments.Values)
				{
					priceImpact += (dollars * _model.PriceImpactAmt) / 10000;
				}
			}

			return commission + priceImpact;
		}

		private void saveModelPortfolio(StringBuilder sb, DateTime date, List<string> portfolio, Dictionary<string, double> investments)
		{
			foreach (var symbol in portfolio)
			{
				var fields = symbol.Split(':');
				var ticker = fields[1];
				if (investments.ContainsKey(ticker))
				{
					sb.Append(date.ToString("yyyyMMdd") + "," + symbol.Substring(2) + "," + ((symbol.Substring(0, 2) == "T:") ? 1 : -1) + "," + ((investments != null) ? investments[ticker] : 0.0) + "\n");
				}
			}
		}

		private void _scan_ScanComplete(object sender, EventArgs e)
		{
			var scan = sender as Scan;
			_scanSummary = scan.GetScanSummary();
			_scanThread.Close();
			_waitForFilters = false;
		}

		private void waitForDataComplete()
		{
			//Debug.WriteLine($"[IdeaCalculator] === waitForDataComplete STARTED ===");
			//Debug.WriteLine($"[IdeaCalculator] _waitForBars: {_waitForBars}");
			//Debug.WriteLine($"[IdeaCalculator] _waitForFilters: {_waitForFilters}");
			//Debug.WriteLine($"[IdeaCalculator] _waitForFundamentals: {_waitForFundamentals}");

			while (!_stop && (_waitForBars || _waitForFundamentals || _waitForFilters))
			{
				Thread.Sleep(100);
			}
		}

		//private int returnPeriod()
		//{
		//    int output = 1;

		//    string period = _model.Rebalance;
		//    if (period == "Weekly")
		//    {
		//        output = 5;
		//    }
		//    else if (period == "Monthly")
		//    {
		//        output = 20;
		//    }
		//    else if (period == "Quarterly")
		//    {
		//        output = 60;
		//    }
		//    else if (period == "Semi Annual")
		//    {
		//        output = 125;
		//    }
		//    else if (period == "Annually")
		//    {
		//        output = 250;
		//    }
		//    return output;
		//}

		private string getInterval()
		{
			string output = _model.GetInterval();
			if (output == "Semi Annual")
			{
				output = "SemiAnnually";
			}
			else if (output == "Annually")
			{
				output = "Yearly";
			}
			return output;
		}

		private List<Bar> getBars(string symbol, string interval, int barCount, List<Bar> sync = null)
		{
			List<Bar> output = null;

			List<Bar> bars = _barCache.GetBars(symbol, interval, 0, barCount);

			if (sync == null || bars.Count == 0)
			{
				int count = barCount - bars.Count;
				for (int ii = 0; ii < count; ii++)
				{
					var bar = new Bar();
					bar.Time = DateTime.MinValue;
					bars.Insert(0, bar);
				}
				output = bars;
			}
			else
			{
				output = new List<Bar>();

				int index2 = 0;
				for (var index1 = 0; index1 < sync.Count; index1++)
				{
					var time1 = sync[index1].Time;
					var time2 = (index2 < bars.Count) ? bars[index2].Time : DateTime.MaxValue;
					if (time1 < time2)
					{
						var bar1 = new Bar();
						bar1.Time = time1;
						output.Add(bar1);
					}
					else if (time1 == time2)
					{
						output.Add(bars[index2]);
						index2++;
					}
					else if (time1 > time2)
					{
						index2++;
						index1--;
					}
				}
			}

			return output;
		}

		private void getFundamentalFactors(string ticker)
		{
			try
			{
				string benchmarkSymbol = _model.Benchmark;
				List<DateTime> barTimes = _barCache.GetTimes(benchmarkSymbol, _interval, 0, BarServer.MaxBarCount);
				int barCount = barTimes.Count;

				List<string> fieldNames = new List<string>();

				var factors = _model.FactorNames;

				foreach (var factor in factors)
				{
					string fieldName = Conditions.GetFieldNameFromDescription(factor);
					fieldNames.Add(fieldName);
				}

				var data = new Dictionary<string, Dictionary<string, string>>();
				foreach (var fieldName in fieldNames)
				{
					data[fieldName] = _portfolio2.loadData(ticker, fieldName);
				}

				foreach (var factorName in fieldNames)
				{
					int factorCount = data[factorName].Count;

					string key1 = factorName + ":" + ticker;
					if (!_factorInputs.ContainsKey(key1))
					{
						_factorInputs[key1] = new Series(barCount);
					}

					List<string> inputTimes = data[factorName].Keys.ToList();
					inputTimes.Sort();
					int inputCount = inputTimes.Count;

					//    a     b      c
					// xxxaaaaaabbbbbbbccccccccccccccccccc

					int iIdx = 0;

					double iValue = double.NaN;
					for (int oIdx = 0; oIdx < barCount; oIdx++)
					{
						string oDate = barTimes[oIdx].ToString("yyyy-MM-dd");
						for (; iIdx < inputCount; iIdx++)
						{
							var date = inputTimes[iIdx];
							if (date.CompareTo(oDate) > 0) break; // iDate > oDate
							if (data[factorName].ContainsKey(date))
							{
								iValue = double.Parse(data[factorName][date]);
							}
						}
						_factorInputs[key1][oIdx] = iValue;
					}
				}

			}
			catch (Exception x)
			{
				//Debug.WriteLine(x.Message);
			}
		}

		private Dictionary<string, Dictionary<DateTime, double>> getFactorScores(string lowestInterval)
		{
			// fill predictions for each ticker or each day in the test range...
			// _predictions = Dictionary<ticker, Dictionary<date, score>>
			string benchmarkSymbol = _model.Benchmark;
			string interval = _model.RankingInterval;
			Dictionary<string, List<Bar>> bars = new Dictionary<string, List<Bar>>();
			bars[benchmarkSymbol] = getBars(benchmarkSymbol, interval, BarServer.MaxBarCount);
			List<DateTime> times = bars[benchmarkSymbol].Select(x => x.Time).ToList();

			DateTime date1 = _model.DataRange.Time1; // getStartDate(interval, lowestInterval); // mlPredict
			DateTime date2 = _model.UseCurrentDate ? DateTime.UtcNow : _model.DataRange.Time2; // DateTime.MaxValue;

			double flipPercent = _model.GetPortfolioPercent();
			int takeCount = (int)Math.Round(_model.Symbols.Count * flipPercent / 100);
			int skipCount = _model.Symbols.Count - takeCount;

			// calculate the returns for the each ticker in universe
			Dictionary<string, Series> returns = new Dictionary<string, Series>();
			int period = 1;
			foreach (var symbol in _model.Symbols)
			{
				Series ret = new Series(BarServer.MaxBarCount);
				var bars1 = getBars(symbol.Ticker, interval, BarServer.MaxBarCount);
				for (int ii = 0; ii < BarServer.MaxBarCount; ii++)
				{
					ret[ii] = (ii - period >= 0 && !double.IsNaN(bars1[ii].Close) && !double.IsNaN(bars1[ii - period].Close)) ? (bars1[ii].Close - bars1[ii - period].Close) / bars1[ii - period].Close : double.NaN;
				}
				returns[symbol.Ticker] = ret;

				getFundamentalFactors(symbol.Ticker);
			}

			var barCount = BarServer.MaxBarCount;

			// calculate factor values
			_factorScores.Clear();
			_model.Symbols.ForEach(symbol => _factorScores[symbol.Ticker] = new Dictionary<DateTime, double>());
			for (int ii = 0; ii < barCount; ii++)
			{

				var barReturns = returns.Select(x => x.Value[ii]); // all returns for the universe at this bar
				var averageReturn = getAverage(barReturns.ToList());

				DateTime time = times[ii];
				if (time >= date1 && time <= date2)
				{
					// returns dictionary    key = factor name, value = list of factors for this bar sorted by symbol in universe
					var rawFactors = getRawFactors(_model, ii);
					var normFactors = getNormalizedFactors(rawFactors);   //  (value - mean) / stddev

					// for each factor
					// sort tickers by factor value
					// create two sub portfolios of equal size based on ranking selection (top 10% := group A = bottom 10% , group B = top 10%) 
					// get sum of the returns in group A and in group B
					// if group A return is less than group B return....  flip the factorfor this day (negate the factor)
					_model.Symbols.ForEach(symbol => _factorScores[symbol.Ticker][time] = 0);
					foreach (var factorName in _model.FactorNames)
					{
						var averageNormValue = getAverage(normFactors[factorName]);

						Dictionary<string, double> values = new Dictionary<string, double>();
						int count = normFactors[factorName].Count;
						for (int idx = 0; idx < count; idx++)
						{
							var normValue = normFactors[factorName][idx];
							values[_model.Symbols[idx].Ticker] = (double.IsNaN(normValue)) ? averageNormValue : normValue;
						}
						var portfolio = values.OrderBy(x => x.Value).Select(x => x.Key).ToList();  // portfolio sorted acsending by factor

						var groupA = portfolio.Take(takeCount).ToList();
						var groupB = portfolio.Skip(skipCount).Take(takeCount).ToList();

						if (groupA.Count > 0 && groupB.Count > 0)
						{

							var returnA = groupA.Average(x => double.IsNaN(returns[x][ii]) ? averageReturn : returns[x][ii]);
							var returnB = groupB.Average(x => double.IsNaN(returns[x][ii]) ? averageReturn : returns[x][ii]);

							bool flip = (returnA > returnB);

							for (int idx = 0; idx < count; idx++)
							{
								var ticker = _model.Symbols[idx].Ticker;
								var factor = flip ? -normFactors[factorName][idx] : normFactors[factorName][idx];
								if (!double.IsNaN(factor))
								{
									_factorScores[ticker][time] += factor;
								}
							}
						}
					}
				}
			}
			return _factorScores;
		}

		private double getAverage(List<double> input)
		{
			double sum = 0;
			int count = 0;
			foreach (var value in input)
			{
				if (!double.IsNaN(value))
				{
					count++;
					sum += value;
				}
			}
			double output = (count > 0) ? sum / count : double.NaN;
			return output;
		}

		private Dictionary<string, List<double>> getRawFactors(Model model, int index)
		{
			Dictionary<string, List<double>> rawFactors = new Dictionary<string, List<double>>(); // keyed by fieldName:ticker

			int factorCount = model.FactorNames.Count;

			List<string> tickers = getTickers();
			var tickerCount = tickers.Count;

			for (var ii = 0; ii < factorCount; ii++)
			{
				var factorName = model.FactorNames[ii];
				string fieldName = Conditions.GetFieldNameFromDescription(factorName);

				rawFactors[fieldName] = new List<double>();

				for (var jj = 0; jj < tickerCount; jj++)
				{
					string ticker = tickers[jj];

					string key = fieldName + ":" + ticker;
					Series factorSeries = null;
					double factor = double.NaN;
					if (_factorInputs.TryGetValue(key, out factorSeries))
					{
						int count = factorSeries.Count;
						int idx = (index == int.MaxValue) ? count - 1 : index;
						factor = (idx >= 0 && idx < count) ? factorSeries[idx] : double.NaN;
					}
					rawFactors[fieldName].Add(factor);
				}
			}
			return rawFactors;
		}

		private double getMean(List<double> input)
		{
			double sum = 0;
			int cnt = 0;
			foreach (double value in input)
			{
				if (!double.IsNaN(value))
				{
					cnt++;
					sum += value;
				}
			}

			double output = (cnt == 0) ? double.NaN : sum / cnt;
			return output;
		}

		private double getStandardDeviation(List<double> input)
		{
			double output = double.NaN;

			double mean = getMean(input);

			if (!double.IsNaN(mean))
			{

				// Calculate the total for the standard deviation
				double bigSum = 0;
				for (int i = 0; i < input.Count; i++)
				{
					var value = input[i];
					if (!double.IsNaN(value))
					{
						bigSum += Math.Pow(value - mean, 2);
					}
				}

				// Now we can calculate the standard deviation
				output = Math.Sqrt(bigSum / (input.Count - 1));
			}

			return output;
		}

		private Dictionary<string, List<double>> getNormalizedFactors(Dictionary<string, List<double>> rawFactors)
		{
			Dictionary<string, List<double>> normFactors = new Dictionary<string, List<double>>();
			foreach (var kvp in rawFactors)
			{
				var fieldName = kvp.Key;
				var values = kvp.Value;

				double mean = getMean(values);
				double stdDev = getStandardDeviation(values);
				normFactors[fieldName] = new List<double>();

				if (!double.IsNaN(stdDev))
				{
					for (int i = 0; i < values.Count; i++)
					{
						var value = values[i];
						double normValue = (!double.IsNaN(value)) ? (value - mean) / stdDev : value;
						normFactors[fieldName].Add(normValue);
					}
				}
				else
				{
					for (int i = 0; i < values.Count; i++)
					{
						normFactors[fieldName].Add(double.NaN);
					}
				}
			}
			return normFactors;
		}

		//private DateTime getStartDate(string ltInterval, string stInterval)
		//{
		//    string ticker = _model.Benchmark;
		//    int count = int.Parse(_model.MLMaxBars);

		//    var stTimes = _barCache.GetTimes(ticker, stInterval, 0);

		//    var stCount = stTimes.Count;

		//    var time = (count < stCount) ? stTimes[stCount - count - 1] : stTimes.First();

		//    if (ltInterval != stInterval)
		//    {
		//        var ltTimes = _barCache.GetTimes(ticker, ltInterval, 0);
		//        var ltTime = ltTimes.First();
		//        for (int ii = 0; ii < ltTimes.Count; ii++)
		//        {
		//            if (ltTimes[ii] > time) break;
		//            ltTime = ltTimes[ii];
		//        }
		//        time = ltTime;
		//    }
		//    return time;
		//}

		private List<PortfolioMember> mlPredict(List<PortfolioMember> input, string lowestInterval)
		{
			var output = new List<PortfolioMember>();

			for (var ii = 0; ii < input.Count; ii++)
			{
				var member = PortfolioMember.Clone(input[ii]);
				output.Add(member);
			}

			// Force previous state to 100% before transitioning to Training
			if (_progressTotalNumber > 0) _progressCompletedNumber = _progressTotalNumber;
			sendProgressEvent();

			_progressState = ProgressState.Training;
			_progressCompletedNumber = 0;
			_progressTotalNumber = 0;
			sendProgressEvent();

			_predictionData.Clear();

			var interval = _model.ATMMLInterval;

			string benchmarkSymbol = _model.Benchmark;
			Dictionary<string, List<Bar>> bars = new Dictionary<string, List<Bar>>();
			bars[benchmarkSymbol] = getBars(benchmarkSymbol, interval, BarServer.MaxBarCount);
			List<DateTime> times = bars[benchmarkSymbol].Select(x => x.Time).ToList();
			if (times.Count > 0)
			{
				DateTime date1 = _model.DataRange.Time1; // getStartDate(interval, lowestInterval); // mlPredict
				DateTime date2 = _model.UseCurrentDate ? DateTime.UtcNow : _model.DataRange.Time2; // DateTime.MaxValue;

				var mlModel = MainView.GetModel(_model.MLModelName);

				if (mlModel != null)
				{
					var tickers = _model.Symbols.Select(x => x.Ticker).ToList();

					var features = mlModel.FeatureNames;
					var scenario = mlModel.Scenario;

					var seconds = 2 * (mlModel.UseTicker ? tickers.Count : 1);
					_progressTime = DateTime.Now + new TimeSpan(0, 0, seconds);
					sendProgressEvent();

					var st = _model.DataRange.Time1;
					var et = _model.UseCurrentDate ? DateTime.UtcNow : _model.DataRange.Time2;

					var predictions = new Dictionary<string, Dictionary<string, Dictionary<DateTime, double>>>();
					if (!mlModel.UseTicker)
					{
						var pathName1 = @"senarios\" + mlModel.Name + @"\" + interval;
						var data1 = atm.getModelData(mlModel.FeatureNames, mlModel.Scenario, _barCache, tickers, interval, st, et, int.Parse(mlModel.MLMaxBars), mlModel.MLSplit, false, false).data;
						atm.saveModelData(pathName1 + @"\test.csv", data1);
						predictions = MainView.AutoMLPredict(pathName1, "");
					}

					foreach (var member in output)
					{
						var ticker = member.Ticker;

						var g = _model.Groups.Find(g => g.Id == member.GroupId);
						var side = g.Direction;

						if (mlModel.UseTicker)
						{
							var pathName2 = @"senarios\" + mlModel.Name + @"\" + interval + @"\" + MainView.ToPath(ticker);
							var data2 = atm.getModelData(mlModel.FeatureNames, mlModel.Scenario, _barCache, (new string[] { ticker }).ToList(), interval, st, et, int.Parse(mlModel.MLMaxBars), mlModel.MLSplit, false, false).data;
							atm.saveModelData(pathName2 + @"\test.csv", data2);
							predictions = MainView.AutoMLPredict(pathName2, "");
						}

						if (!_predictionData.ContainsKey(ticker))
						{
							_predictionData[ticker] = new Dictionary<DateTime, int>();
						}

						foreach (var date in predictions[ticker][interval].Keys)
						{
							var prediction = predictions[ticker][interval][date];


							var tl = getLowestTimes(ticker, date, interval, lowestInterval);
							// tl.ForEach(t => _predictionData[ticker][t] = (prediction < 0) ? 2 : ((prediction > 0) ? 1 : 0));
							tl.ForEach(t =>
							{
								_predictionData[ticker][t] = (prediction == 0) ? 2 : 1; // 2 = short, 1 = long
								member.Sizes[t] = (side > 0 && prediction != 0 || side < 0 && prediction == 0) ? 1 : 0;
							});
						}
					}
				}
			}
			return output;
		}

		private DateTime getPreviousRebalanceDate(Model model, DateTime input, List<DateTime> times)
		{
			DateTime output = new DateTime();

			int count = times.Count;
			for (int ii = count - 1; ii > 0; ii--)
			{
				DateTime time = times[ii];
				DateTime previousTime = (ii >= 1) ? times[ii - 1] : times[ii] - new TimeSpan(1, 0, 0, 0, 0);

				if (rebalance(model, previousTime, time))
				{
					if (previousTime < input)
					{
						output = previousTime;
						break;
					}
				}
			}
			return output;
		}

		public void Close()
		{
			if (_scanThread != null)
			{
				_scanThread.Close();
			}

			Stop();

			_portfolio1.PortfolioChanged -= new PortfolioChangedEventHandler(portfolio1Changed);
			_portfolio1.Close();
			_portfolio2.PortfolioChanged -= new PortfolioChangedEventHandler(portfolio2Changed);
			_portfolio2.Close();

			_barCache.Clear();
		}

		public ProgressState ProgressState
		{
			get { return _progressState; }
		}

		public int ProgressTotalNumber
		{
			get { return _progressTotalNumber; }
		}

		public int ProgressCompletedNumber
		{
			get { return _progressCompletedNumber; }
		}

		public DateTime ProgressTime
		{
			get { return _progressTime; }
		}

		protected void sendProgressEvent()
		{
			if (ProgressEvent != null)
			{
				if (_progressCompletedNumber > _progressTotalNumber)
				{
					_progressCompletedNumber = _progressTotalNumber;
				}
				ProgressEvent(this, new EventArgs());
			}
		}

		private bool isCQGPortfolio(List<Symbol> symbols)
		{
			return symbols.Count > 0 && Portfolio.isCQGSymbol(symbols[0].Ticker);
		}

		void requestReferenceData()
		{
			//Debug.WriteLine($"[IdeaCalculator] === requestReferenceData ===");
			List<Symbol> symbols = _model.Symbols;
			var syms1 = _portfolio1.GetSymbols();
			var syms2 = _portfolio2.GetSymbols();
			lock (_referenceData)
			{
				_referenceData.Clear();
			}
			if (isCQGPortfolio(symbols))
			{
				_indexSymbols.Clear();
				_indexSymbols.Add("F.EP");
				//Debug.WriteLine($"[IdeaCalculator] About to call requestIndexBars");
				requestIndexBars();
			}
			else
			{
				int count = symbols.Count;
				//Debug.WriteLine($"[IdeaCalculator] NOT CQG - processing {count} symbols");

				_referenceSymbols = new List<string>();
				for (int ii = 0; ii < count; ii++)
				{
					_referenceSymbols.Add(symbols[ii].Ticker);
					_refSyms.Add(symbols[ii].Ticker);
				}

				//Debug.WriteLine($"[IdeaCalculator] Requesting REL_INDEX for {count} symbols");
				for (int jj = 0; jj < count; jj++)
				{
					if (jj < 5) // Log first 5 only
					{
						//Debug.WriteLine($"[IdeaCalculator] RequestReferenceData for: {symbols[jj].Ticker}");
					}
					string[] dataFieldNames = { "REL_INDEX" };
					_portfolio1.RequestReferenceData(symbols[jj].Ticker, dataFieldNames);
				}
				//Debug.WriteLine($"[IdeaCalculator] Finished requesting reference data");
				//Debug.WriteLine($"[IdeaCalculator] Finished requesting reference data");
				//Debug.WriteLine($"[IdeaCalculator] _referenceSymbols count: {_referenceSymbols.Count}");
			}
		}

		void requestReportDates()
		{
			List<string> symbols = getTickers();
			DateTime date = DateTime.UtcNow - new TimeSpan(20 * 365, 0, 0, 0);
			_reportCount = symbols.Count;
			_portfolio2.RequestFundamentalReportDates(symbols, date.Year);
		}

		void requestHistoricalReferenceData()
		{
			var tickers = getTickers();

			_portfolio2.RequestReferenceData(_model.Benchmark, new string[] { "CUR_MKT_CAP", "COUNT_INDEX_MEMBERS" });

			int count = tickers.Count;

			var factors = new List<string>(_model.FactorNames);
			factors.Add("CUR_MKT_CAP");

			var dataFieldNames = new List<string>();
			foreach (var factor in factors)
			{
				string fieldName = Conditions.GetFieldNameFromDescription(factor);
				dataFieldNames.AddRange(getRequiredHistoricalReferenceData(fieldName));
			}

			_fundamentalSymbols.Clear();
			_fundamentalResponseTime = DateTime.UtcNow;
			for (int jj = 0; jj < count; jj++)
			{
				_fundamentalSymbols.Add(tickers[jj]);
				_portfolio2.RequestReferenceData(tickers[jj], dataFieldNames.ToArray());
			}
		}

		private List<string> getRequiredHistoricalReferenceData(string factor)
		{
			List<string> output = new List<string>();

			// todo: figure out another way to distiguish between atm and fundamental factors
			if (factor.Length >= 3 && factor.Substring(0, 3) != "ATM")
			{
				output.Add(factor);
			}
			output = output.Distinct().ToList();
			return output;
		}

		private List<string> getTickers()
		{
			List<string> tickers = new List<string>();
			if (_model.Symbols != null)
			{
				foreach (var symbol in _model.Symbols)
				{
					tickers.Add(symbol.Ticker);
				}
			}
			return tickers;
		}

		void portfolio1Changed(object sender, PortfolioEventArgs e)
		{
			if (e.Type == PortfolioEventType.ReferenceData)
			{
				//Debug.WriteLine($"[IdeaCalculator] portfolio1Changed: {e.Ticker}, _referenceSymbols.Count: {_referenceSymbols.Count}");

				bool okToRequestIndexBars = false;
				foreach (KeyValuePair<string, object> kvp in e.ReferenceData)
				{
					string name = kvp.Key;
					string value = kvp.Value as string;
					if (name == "REL_INDEX")
					{
						if (!value.Contains(" Index"))
						{
							value += " Index";
						}

						string key = e.Ticker + ":" + name;
						lock (_referenceData)
						{
							_referenceData[key] = value;
						}

						//System.Diagnostics.Debug.WriteLine("Reference list count " + _referenceSymbols.Count);

						if (_referenceSymbols.Count > 0)
						{
							for (int ii = 0; ii < _referenceSymbols.Count; ii++)
							{
								if (e.Ticker == _referenceSymbols[ii])
								{
									_referenceSymbols.RemoveAt(ii);
									break;
								}
							}

							// Show remaining symbols when count is low
							if (_referenceSymbols.Count <= 5 && _referenceSymbols.Count > 0)
							{
								//Debug.WriteLine($"[IdeaCalculator] Remaining symbols ({_referenceSymbols.Count}):");
								foreach (var sym in _referenceSymbols)
								{
									//Debug.WriteLine($"[IdeaCalculator]   - {sym}");
								}
							}

							okToRequestIndexBars = (_referenceSymbols.Count == 0);
						}
					}
					else
					{
						var val = (double)kvp.Value;
						string key = e.Ticker + ":" + name;
						lock (_referenceData)
						{
							_referenceData[key] = val;
						}
					}
				}

				if (okToRequestIndexBars && !_stop)
				{
					if (_referenceSymbols.Count == 0)
					{
						//Debug.WriteLine($"[IdeaCalculator] All reference data received! Building index list...");
						_indexSymbols = new List<string>();
						_indexSymbols.Add("VIX Index");
						lock (_referenceData)
						{
							foreach (KeyValuePair<string, object> kvp in _referenceData)
							{
								string key = kvp.Key;
								if (key.Contains("REL_INDEX"))
								{
									string indexSymbol = kvp.Value as string;
									if (!_indexSymbols.Contains(indexSymbol) && indexSymbol != " Index")
									{
										_indexSymbols.Add(indexSymbol);
									}
								}
							}
						}
						//Debug.WriteLine($"[IdeaCalculator] Index symbols: {string.Join(", ", _indexSymbols)}");
						//Debug.WriteLine($"[IdeaCalculator] Calling requestIndexBars()");
						requestIndexBars();
					}
				}
			}
		}

		void portfolio2Changed(object sender, PortfolioEventArgs e)
		{
			if (e.Type == PortfolioEventType.ReferenceData)
			{
				if (_reportCount > 0)
				{
					if (--_reportCount == 0)
					{
						requestHistoricalReferenceData();
					}
				}
				else
				{
					_fundamentalResponseTime = DateTime.UtcNow;
					_fundamentalSymbols.Remove(e.Ticker);
					if (_fundamentalSymbols.Count == 0)
					{
						_waitForFundamentals = false;
					}
				}
			}
		}

		private void requestIndexBars()
		{
			//Debug.WriteLine($"[IdeaCalculator] === requestIndexBars ===");
			//Debug.WriteLine($"[IdeaCalculator] _indexSymbols.Count: {_indexSymbols?.Count ?? 0}");

			var indexIntervals = getDataIntervals(true);
			_indexBarRequestCount = _indexSymbols.Count * indexIntervals.Count;

			//Debug.WriteLine($"[IdeaCalculator] _indexBarRequestCount: {_indexBarRequestCount}");

			_progressCompletedNumber = 0;
			_progressTotalNumber = (_indexSymbols.Count + _model.Symbols.Count) * indexIntervals.Count;
			sendProgressEvent();

			if (_indexBarRequestCount > 0)
			{
				foreach (var interval in indexIntervals)
				{
					if (_stop) break;
					foreach (string symbol in _indexSymbols)
					{
						if (_stop) break;
						//Debug.WriteLine($"[IdeaCalculator] RequestBars for index: {symbol}, interval: {interval}");
						_barCache.RequestBars(symbol, interval, false, BarServer.MaxBarCount, false);
					}
				}

				// Start timeout timer - if Bloomberg doesn't respond in 5 seconds, use local data
				System.Threading.Tasks.Task.Run(async () =>
				{
					await System.Threading.Tasks.Task.Delay(30000); // Wait 30 seconds

					if (_indexBarRequestCount > 0 && !_stop)
					{
						//Debug.WriteLine($"[IdeaCalculator] TIMEOUT: Bloomberg didn't respond for {_indexBarRequestCount} index bars");
						//Debug.WriteLine($"[IdeaCalculator] Forcing proceed to requestSymbolBars()");

						_indexBarRequestCount = 0;
						requestSymbolBars();
					}
				});
			}
			else
			{
				requestSymbolBars();
			}
		}

		public string getDataInterval(string interval)
		{
			if (interval == "1D" || interval == "D") interval = "Daily";
			else if (interval == "1W" || interval == "W") interval = "Weekly";
			else if (interval == "1M" || interval == "M") interval = "Monthly";
			else if (interval == "1Q" || interval == "Q") interval = "Quarterly";
			else if (interval == "1S" || interval == "S") interval = "SemiAnnually";
			else if (interval == "1Y" || interval == "Y") interval = "Yearly";
			return interval;
		}

		private List<string> getDataIntervals(bool forIndex)
		{
			var intervals = new List<string>();
			//var veryShortTermInterval = Study.getForecastInterval(_model.ATMAnalysisInterval, -1);
			var shortTermInterval = _model.ATMAnalysisInterval;
			var midTermInterval = Study.getForecastInterval(shortTermInterval, 1);
			var longTermInterval = Study.getForecastInterval(midTermInterval, 1);
			//intervals.Add(getDataInterval(veryShortTermInterval));
			intervals.Add(getDataInterval(shortTermInterval));
			intervals.Add(getDataInterval(midTermInterval));
			intervals.Add(getDataInterval(longTermInterval));
			_model.Groups.ForEach(g => intervals.Add(getDataInterval(g.AlignmentInterval)));
			intervals.Add(getDataInterval(_model.ATMMLInterval));
			//intervals.Add(getDataInterval(_model.RiskInterval));

			// polygon does noy support index
			if (forIndex)
			{
				intervals = intervals.Select(x => x == "D30" ? "Daily" : x).ToList();
				intervals = intervals.Select(x => x == "W30" ? "Weekly" : x).ToList();
			}

			intervals = intervals.Distinct().Where(x => x != null).ToList();
			return intervals;
		}

		void requestSymbolBars()
		{
			var intervals = getDataIntervals(false);

			lock (_barRequests)
			{
				_totalBarRequest = 0;
				_barRequests.Clear();

				//string benchMarkSymbol = _model.Benchmark;
				//foreach (var interval in intervals)
				//{
				//    if (_stop) break;
				//    var key1 = benchMarkSymbol + ":" + interval;
				//    if (!_barRequests.Contains(key1))
				//    {
				//        _barRequests.Add(key1);
				//        _barCache.RequestBars(benchMarkSymbol, interval, false, BarServer.MaxBarCount, false);
				//    }
				//}

				HashSet<string> sectorTickers = new HashSet<string>();
				foreach (var symbol in _model.Symbols)
				{
					var ticker = symbol.Ticker;
					var sector = symbol.Sector;
					var sectorNumber = 0;
					int.TryParse(sector, out sectorNumber);
					var sectorTicker = _portfolio1.getSectorSymbol(sectorNumber);
					sectorTickers.Add(sectorTicker);
				}

				foreach (var symbol in _model.Symbols)
				{
					if (_stop) break;
					var ticker = symbol.Ticker;
					foreach (var interval in intervals)
					{
						var key = ticker + ":" + interval;
						if (!_barRequests.Contains(key))
						{
							_barRequests.Add(key);
							//Debug.WriteLine($"[IdeaCalculator] RequestBars for ticker: {ticker}, interval: {interval}");
							_barCache.RequestBars(ticker, interval, false, BarServer.MaxBarCount, false);
						}
					}
				}


				// market symbol
				var marketSymbol = "SPY US Equity";
				var key1a = marketSymbol + ":" + intervals[0];
				if (!_barRequests.Contains(key1a))
				{
					_barRequests.Add(key1a);
					_barCache.RequestBars(marketSymbol, intervals[0], false, BarServer.MaxBarCount, false);
				}
				var key1b = marketSymbol + ":" + intervals[1];
				if (!_barRequests.Contains(key1b))
				{
					_barRequests.Add(key1b);
					_barCache.RequestBars(marketSymbol, intervals[1], false, BarServer.MaxBarCount, false);
				}

				// hedge symbol
				var hedgeSymbol = "SPY US Equity";
				var key2 = hedgeSymbol + ":" + intervals[0];
				if (!_barRequests.Contains(key2))
				{
					_barRequests.Add(key2);
					_barCache.RequestBars(hedgeSymbol, intervals[0], false, BarServer.MaxBarCount, false);
				}

				//if (_model.Groups[0].MTNT == 3 || _model.Groups[0].MTP == 3 || _model.Groups[0].MTFT == 3)
				//            {
				//                var sectors = _model.Symbols.Select(x => _portfolio1.getSectorSymbol(int.Parse(x.Sector))).Distinct().ToList();
				//                sectors.ForEach(ticker =>
				//                {
				//		var key1 = ticker + ":" + intervals[1];
				//		if (!_barRequests.Contains(key1))
				//		{
				//			_barRequests.Add(key1);
				//			_barCache.RequestBars(ticker, intervals[1], false, BarServer.MaxBarCount, false);
				//		}
				//		var key2 = ticker + ":" + intervals[2];
				//		if (!_barRequests.Contains(key2))
				//		{
				//			_barRequests.Add(key2);
				//			_barCache.RequestBars(ticker, intervals[2], false, BarServer.MaxBarCount, false);
				//		}
				//	});
				//}

				_totalBarRequest = _barRequests.Count;
			}
		}

		private void barChanged(object sender, BarEventArgs e)
		{
			if (e.Type == BarEventArgs.EventType.BarsReceived)
			{
				string ticker = e.Ticker;
				string interval = e.Interval;

				if (_indexBarRequestCount > 0)
				{
					Series[] indexSeries = _barCache.GetSeries(ticker, interval, new string[] { "Close" }, 0, BarServer.MaxBarCount);
					if (indexSeries.Length > 0 && indexSeries[0].Count > 0)
					{
						lock (_referenceData)
						{
							_referenceData["Index Prices : " + interval] = indexSeries[0];
						}
					}

					if (_progressState == ProgressState.CollectingData)
					{
						_progressCompletedNumber++;
						sendProgressEvent();
					}

					//Trace.WriteLine("INDEX " + _indexBarRequestCount + " " + ticker + " " + interval);

					if (--_indexBarRequestCount == 0)
					{
						requestSymbolBars();
					}
				}
				else
				{
					lock (_barRequests)
					{
						string key = ticker + ":" + interval;
						int index = _barRequests.IndexOf(key);
						if (index >= 0)
						{
							if (_progressState == ProgressState.CollectingData)
							{
								_barReceivedTime = DateTime.UtcNow;
								_progressCompletedNumber++;
								sendProgressEvent();
							}

							_barRequests.RemoveAt(index);

							//Trace.WriteLine(_barRequests.Count + " " + key);
							if (_barRequests.Count <= 5)
							{
								for (var jj = 0; jj < _barRequests.Count; jj++)
								{ } //Trace.WriteLine - bar requests
							}

							if (_barRequests.Count == 0)
							{
								_waitForBars = false;
							}
						}
					}
				}
			}
		}

		private void timerEvent(object source, ElapsedEventArgs e)
		{
			sendProgressEvent();

			if (_fundamentalSymbols.Count < _model.Symbols.Count && _fundamentalSymbols.Count > 0)
			{
				var timeSpan = DateTime.UtcNow - _fundamentalResponseTime;
				if (timeSpan.TotalSeconds > 30) // give up on getting all of the requested fundamentals
				{
					_fundamentalSymbols.Clear();
					_waitForFundamentals = false;
				}
			}

			if (_progressState == ProgressState.CollectingData)
			{
				var timeSpan = DateTime.UtcNow - _barReceivedTime;
				if (timeSpan.TotalSeconds > 45) // give up on getting all of the requested bars
				{
					_progressCompletedNumber = _progressTotalNumber;
					_waitForBars = false;
					_barCollectionTimedOut = true; // flag: no bars received, protect disk data
					sendProgressEvent(); // push 100% to UI before state change
				}
			}
		}


		/// <summary>
		/// Builds daily NAV using pre-computed price map from main loop.
		/// This ensures we use the EXACT same prices as the main loop.
		/// </summary>
		public static Dictionary<DateTime, double> BuildDailyNavWithPriceMap(
			IReadOnlyList<Trade> trades,
			Dictionary<string, Dictionary<DateTime, double>> pricesByTicker,
			double initialNav)
		{
			var navByDate = new Dictionary<DateTime, double>();
			try
			{
				if (trades == null || trades.Count == 0)
					throw new ArgumentException("Trades required.");

				if (pricesByTicker == null || pricesByTicker.Count == 0)
					throw new ArgumentException("pricesByTicker required.");

				if (initialNav <= 0)
					throw new ArgumentException("initialNav must be > 0.");

				// Diagnostic
				var longCount = trades.Count(t => t.Direction == 1);
				var shortCount = trades.Count(t => t.Direction == -1);
				//Debug.WriteLine($"[BuildDailyNavWithPriceMap] Trades: {longCount} long, {shortCount} short");
				//Debug.WriteLine($"[BuildDailyNavWithPriceMap] Price map has {pricesByTicker.Count} tickers");

				// 1. Get date range from Shares
				var allShareDates = trades
					.Where(t => t.Shares != null && t.Shares.Count > 0)
					.SelectMany(t => t.Shares.Keys)
					.Select(d => d.Date)
					.ToList();

				if (allShareDates.Count == 0)
					throw new InvalidOperationException("No Shares found.");

				DateTime startDate = allShareDates.Min();
				DateTime endDate = allShareDates.Max();

				//Debug.WriteLine($"[BuildDailyNavWithPriceMap] Date range: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");

				// 2. Build Shares lookup
				var tradeSteps = trades.ToDictionary(
					t => t,
					t => (t.Shares ?? new Dictionary<DateTime, double>())
							.Select(k => (date: k.Key.Date, shares: k.Value))
							.OrderBy(x => x.date)
							.ToList()
				);


				// DIAGNOSTIC: Check Shares entries per trade
				int tradesWithNoShares = 0;
				int tradesWithOneEntry = 0;
				int tradesWithMultipleEntries = 0;

				foreach (var t in trades)
				{
					var steps = tradeSteps[t];
					if (steps.Count == 0) tradesWithNoShares++;
					else if (steps.Count == 1) tradesWithOneEntry++;
					else tradesWithMultipleEntries++;
				}

				//Debug.WriteLine($"[BuildDailyNavWithPriceMap] Trades with NO Shares entries: {tradesWithNoShares}");
				//Debug.WriteLine($"[BuildDailyNavWithPriceMap] Trades with 1 Shares entry: {tradesWithOneEntry}");
				//Debug.WriteLine($"[BuildDailyNavWithPriceMap] Trades with 2+ Shares entries: {tradesWithMultipleEntries}");

				// Show sample of trades with no shares
				if (tradesWithNoShares > 0)
				{
					//Debug.WriteLine($"[BuildDailyNavWithPriceMap] Sample trades with NO Shares:");
					foreach (var t in trades.Where(x => tradeSteps[x].Count == 0).Take(5))
					{
						//Debug.WriteLine($"  - {t.Ticker} Dir={t.Direction} Open={t.OpenDateTime:yyyy-MM-dd} Close={t.CloseDateTime:yyyy-MM-dd}");
					}
				}

				static double SharesAt(List<(DateTime date, double shares)> steps, DateTime d)
				{
					if (steps == null || steps.Count == 0) return 0.0;
					if (d < steps[0].date) return 0.0;

					int lo = 0, hi = steps.Count - 1;
					while (lo <= hi)
					{
						int mid = lo + ((hi - lo) >> 1);
						if (steps[mid].date <= d) lo = mid + 1;
						else hi = mid - 1;
					}
					return hi >= 0 ? steps[hi].shares : 0.0;
				}

				// 3. Build calendar from price map dates
				var calendar = new SortedSet<DateTime>();
				foreach (var t in trades)
				{
					if (!pricesByTicker.TryGetValue(t.Ticker, out var m)) continue;
					foreach (var d in m.Keys)
					{
						if (d >= startDate && d <= endDate)
							calendar.Add(d);
					}
				}

				if (calendar.Count < 2)
					throw new InvalidOperationException("Insufficient price coverage.");

				var dates = calendar.ToList();
				//Debug.WriteLine($"[BuildDailyNavWithPriceMap] Calendar: {dates.Count} trading days");

				// 4. Walk forward computing P&L

				double nav = initialNav;
				navByDate[dates[0]] = nav;

				int processed = 0, skippedNoPrice = 0, skippedZeroShares = 0;
				double totalLongPnl = 0, totalShortPnl = 0;

				// For price comparison logging
				var priceLog = new List<string>();

				var sortedTrades = trades.OrderBy(t => t.Ticker).ThenBy(t => t.OpenDateTime).ToList();

				for (int i = 1; i < dates.Count; i++)
				{
					DateTime prev = dates[i - 1];
					DateTime curr = dates[i];
					double pnl = 0.0;

					foreach (var t in sortedTrades)
					{
						if (!pricesByTicker.TryGetValue(t.Ticker, out var m))
						{
							skippedNoPrice++;
							continue;
						}
						if (!m.TryGetValue(prev, out var p0))
						{
							skippedNoPrice++;
							continue;
						}
						if (!m.TryGetValue(curr, out var p1))
						{
							skippedNoPrice++;
							continue;
						}

						var steps = tradeSteps[t];

						// Get shares at start and end of period
						double sharesAtPrev = SharesAt(steps, prev);
						double sharesAtCurr = SharesAt(steps, curr);

						// If trade opened this period: no P&L on open day (entry price = mark price)
						// If trade closed this period: use prev shares for final day P&L
						// If held full period: use prev shares
						double shares;
						if (sharesAtPrev == 0 && sharesAtCurr > 0)
						{
							// Trade opened on curr date: zero P&L on entry day
							skippedZeroShares++;
							continue;
						}
						else if (sharesAtPrev > 0 && sharesAtCurr == 0)
						{
							// Trade closed during this period
							shares = sharesAtPrev;
						}
						else
						{
							// Trade held full period or not held at all
							shares = sharesAtPrev;
						}

						if (shares == 0.0)
						{
							skippedZeroShares++;
							continue;
						}

						// P&L = Direction * Shares * PriceChange
						double tradePnl = t.Direction * shares * (p1 - p0);
						pnl += tradePnl;
						processed++;

						if (t.Direction > 0)
							totalLongPnl += tradePnl;
						else
							totalShortPnl += tradePnl;

						// Log first few for verification
						if (priceLog.Count < 30 && Math.Abs(tradePnl) > 0.01)
						{
							priceLog.Add($"FIXED,{curr:yyyy-MM-dd},{t.Ticker},{t.Direction},{shares:F0},{p0:F4},{p1:F4},{tradePnl:F2}");
						}
					}

					nav += pnl;
					navByDate[curr] = nav;
				}

				// Output diagnostics
				//Debug.WriteLine($"[BuildDailyNavWithPriceMap] === FINAL SUMMARY ===");
				//Debug.WriteLine($"[BuildDailyNavWithPriceMap] Processed: {processed} trade-days");
				//Debug.WriteLine($"[BuildDailyNavWithPriceMap] Skipped (no price): {skippedNoPrice}");
				//Debug.WriteLine($"[BuildDailyNavWithPriceMap] Skipped (zero shares): {skippedZeroShares}");
				//Debug.WriteLine($"[BuildDailyNavWithPriceMap] Long P&L: ${totalLongPnl:N0}");
				//Debug.WriteLine($"[BuildDailyNavWithPriceMap] Short P&L: ${totalShortPnl:N0}");
				//Debug.WriteLine($"[BuildDailyNavWithPriceMap] Total P&L: ${(totalLongPnl + totalShortPnl):N0}");
				//Debug.WriteLine($"[BuildDailyNavWithPriceMap] Final NAV: ${nav:N0}");

				// Output price samples for verification
				//Debug.WriteLine("");
				//Debug.WriteLine("============================================================");
				//Debug.WriteLine("[FIXED] PRICE SAMPLES (should match MAIN loop now)");
				//Debug.WriteLine("============================================================");
				//Debug.WriteLine("Source,Date,Ticker,Dir,Shares,PrevPrice,CurrPrice,PnL");
				foreach (var line in priceLog)
				{
					//Debug.WriteLine(line);
				}
				//Debug.WriteLine("============================================================");
			}
			catch (Exception x)
			{
				//Debug.WriteLine($"⚠ BuildDailyNavWithPriceMap FAILED: {x.Message}");
				//Debug.WriteLine($"⚠ navByDate has {navByDate.Count} entries at time of failure");
				//throw; 
			}

			return navByDate;
		}
	}
}