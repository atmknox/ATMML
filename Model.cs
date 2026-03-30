using Microsoft.ML.Probabilistic.Models.Attributes;
using Newtonsoft.Json;
using RiskEngine2;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace ATMML
{
    public class TimeRange : IEquatable<TimeRange>
    {
        public DateTime Time1 { get; set; }
        public DateTime Time2 { get; set; }

        public bool Equals(TimeRange other)
        {
            return other != null && Time1.Equals(other.Time1) && Time2.Equals(other.Time2);
        }

	}

    public enum  Slippage
    {
        CurrentClose,
        NextDayOpen,
        NextDayVWAP
    }

    public enum SizeRecommendation
    {
        FullyInvested,
        MaxDollar,
        MaxPercent,
        ATR
    }

	public class ModelGroup
	{
		public ModelGroup(string name = "Group 1")
		{
			Name = name;
		}

		#region Constants
		public static int levelCount = 6;
		public static string DefaultUniverse => "";
		#endregion

		public static string DefaultAlignmentInterval => "Daily";

		public string Id { get; } = Guid.NewGuid().ToString();

		public string Name { get; set; }

		public int Leverage { get; set; } = 1;
		public int Direction { get; set; } = 1;
		public int MTNT { get; set; }
		public int MTP { get; set; }
		public int MTFT { get; set; }

		public bool UseTimeExit { get; set; }
		public int BarsToExit { get; set; }
		public double PercentBarsToExit { get; set; }

		public bool UseConviction { get; set; }

		public double ConvictionPercent { get; set; }

		public bool UseSectorFT { get; set; }
		public bool UseSTFT { get; set; }
		public bool UseSTSC { get; set; }
		public bool UseSTST { get; set; }
		public bool UseSTTSB { get; set; }
		public bool UseFTRoc { get; set; }
		public bool UseFTSTdiv { get; set; }
		public bool UseFtFt { get; set; }
		public bool UseMTSC { get; set; }
		public bool UseMTST { get; set; }
		public bool UseMTTSB { get; set; }
		public bool UseTwoBar { get; set; }
		public double TwoBarPercent { get; set; }

		public double FTRocPercent { get; set; }
		public bool UseNewTrend { get; set; }
		public bool UseNTObOs { get; set; }
		public bool UseProveIt { get; set; }
		public bool UseMT { get; set; } = false;
		public bool UseST { get; set; } = false;
		public bool UseEntries { get; set; } = true;
		public double NewTrendUnit { get; set; }
		public double PressureUnit { get; set; }
		public double ExhaustionPercent { get; set; }
		public double RetracePercent { get; set; }
		public bool UseExhaustion { get; set; }
		public bool UsePExhaustion { get; set; }
		public double PExhaustionPercent { get; set; }
		public bool UsePressure { get; set; }
		public bool UseRetrace { get; set; }
		public bool UseAdd { get; set; }
		public double AddUnit { get; set; }
		public bool FreqPeriodCheckbox { get; set; }
		public double FreqPeriod { get; set; }

		public bool UseATMStrategies {
			get 
			{ 
				return useATMStrategies;
			}
			set
			{ 
				useATMStrategies = value;
			} 
		}
		private bool useATMStrategies = false;

		public bool UseATRRisk { get; set; }

		public double ATRRiskFactor { get; set; }

		public int ATRRiskPeriod { get; set; }

		public string AlignmentInterval { get; set; } = DefaultAlignmentInterval;

		public List<string[]> Levels { get; set; } =
			Enumerable.Range(0, levelCount)
			  .Select(_ => new[] { "", "" })
			  .ToList();

		public string AlphaPortfolio { get; set; } = DefaultUniverse;

		public string HedgePortfolio { get; set; } = DefaultUniverse;

		public List<Symbol> AlphaSymbols { get; set; } = new List<Symbol>();

		public List<Symbol> HedgeSymbols { get; set; } = new List<Symbol>();

		public List<string> ATMConditionsEnter { get; set; } = new List<string>();
		public List<string> ATMConditionsExit { get; set; } = new List<string>();

		public bool BetaHedgeEnable { get; set; } = false;
		public bool BetaMaxEnable { get; set; } = false;
		public bool BetaTTestEnable { get; set; } = false;
		public List<string> ATMConditionsHedge { get; set; } = new List<string>();

		public string Strategy
		{
			get
			{
				return _strategy;
			}
			set
			{
				_strategy = value;
			}
		} 
		private string _strategy = "SC | SC";

	}

	/// <summary>
	/// Represents a complete configuration and execution model for portfolio strategies,
	/// including static defaults, runtime settings, machine learning parameters,
	/// risk controls, performance tracking, and trading rules.
	///
	/// File structure (in order):
	/// 1) Static Properties (constants, defaults, static arrays)
	/// 2) Instance Properties (grouped & alphabetized)
	/// 3) Public Static Methods
	/// 4) Public Instance Methods (incl. constructor)
	/// 5) Private/Internal/Other (helpers, enums)
	/// </summary>
	public class Model : IEquatable<Model>
	{
		public Model()
		{
		}

		public List<ModelGroup> Groups { get; } = new List<ModelGroup>();


		#region Static Properties
		#region Defaults
		public static string DefaultAlignmentInterval => "Daily";
		public static string DefaultATMMLInterval => "Weekly";
		public static string DefaultBenchmark => "SPY US Equity";
		public static string DefaultRanking => "T 30%";
		public static string DefaultRankingInterval => "Monthly";
		#endregion

		#region Static Option Arrays

		public class Constraint : INotifyPropertyChanged
		{
			public string Category { get; set; } = "";
			public string Name { get; set; } = "";

			private bool _enable = false;
			public bool Enable { 
				get {
					return _enable;
				} 
				set
				{
					bool bp = false;
					if (Name == "BetaNeutral")
					{
						bp = true;
					}
					_enable = value;
					OnPropertyChanged("Enable");
				}
			}

			public double Penalty { get; set; } = 0.0; // lambda weight
			public double? Value { get; set; } = null;

			private bool _hard = false;
			public bool Hard
			{
				get
				{
					return _hard;
				}
				set
				{
					_hard = value;
					OnPropertyChanged("Hard");
					OnPropertyChanged("PenaltyVisible");
				}
			}

			public Visibility PenaltyVisible
			{
				get
				{
					return Hard ? Visibility.Hidden : Visibility.Visible;
				}
			}				

			public string ValueText
			{
				get
				{
					return Value?.ToString("0.0");
				}
				set
				{
					double result;
					if (double.TryParse(value, out result))
					{
						Value = result;
					}
				}
			}

			public event PropertyChangedEventHandler PropertyChanged;

			protected virtual void OnPropertyChanged(string name)
			{
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
			}
		}

		public List<Constraint> Constraints { get; set; } = new List<Constraint>();


		public Constraint getConstraint(string name)
		{
			return Constraints.Find(x => x.Name == name);
		}

		public static List<Constraint> getDefaultConstraints()
		{
			var output = new List<Constraint>();
			output.Add(new Constraint { Name = "AlphaWeighting", Enable = true, Penalty = 0.1, Hard = true });
			output.Add(new Constraint { Name = "MktNeutral", Enable = true, Penalty = 0.1, Hard = true });
			output.Add(new Constraint { Name = "BetaNeutral", Enable = true, Penalty = 0.1, Hard = true });
			output.Add(new Constraint { Name = "RiskParity", Enable = false, Penalty = 0.1, Hard = false });
			output.Add(new Constraint { Name = "VolNeutral", Enable = false, Penalty = 0.1, Hard = false });
			output.Add(new Constraint { Category = "UT", Name = "CapitalUtilization", Enable = false, Value = 50.0, Penalty = 1.0, Hard = false });
			//output.Add(new Constraint { Category = "Budget", Name = "RiskBudget", Enable = true, Value = 2.0, Penalty = 1.0, Hard = false });
			output.Add(new Constraint { Category = "Book", Name = "GrossBookAbs", Enable = true, Value = 200.0, Penalty = 1.0, Hard = false });
			output.Add(new Constraint { Category = "Book", Name = "NetBookAbs", Enable = true, Value = 10.0, Penalty = 1.0, Hard = false });
			output.Add(new Constraint { Category = "Position", Name = "SingleNameCapAbs", Enable = true, Value = 10.0, Penalty = 1.0, Hard = false });
			output.Add(new Constraint { Category = "Position", Name = "MaxSumT10Long", Enable = false, Value = 75.0, Penalty = 1.0, Hard = false });
			output.Add(new Constraint { Category = "Position", Name = "MaxSumT10Shorts",Enable = false,  Value = 65.0, Penalty = 1.0, Hard = false});
			output.Add(new Constraint { Category = "Position", Name = "MaxSumT5Longs", Enable = false, Value = 40.0, Penalty = 1.0, Hard = false });
			output.Add(new Constraint { Category = "Position", Name = "MaxSumT5Shorts", Enable = false, Value = 35.0, Penalty = 1.0, Hard = false }); ;
			output.Add(new Constraint { Category = "Sector", Name = "SectorGross", Enable = true, Value = 200.0, Penalty = 1.0, Hard = false });
			output.Add(new Constraint { Category = "Sector", Name = "SectorNet", Enable = false, Value = 12.0, Penalty = 1.0, Hard = false });
			output.Add(new Constraint { Category = "Industry", Name = "IndustryGross", Enable = true, Value = 150.0, Penalty = 1.0, Hard = false });
			output.Add(new Constraint { Category = "Industry", Name = "IndustryNet", Enable = false, Value = 12.0, Penalty = 1.0, Hard = false });
			output.Add(new Constraint { Category = "Sub-Ind", Name = "SubIndGross", Enable = true, Value = 100.0, Penalty = 1.0, Hard = false });
			output.Add(new Constraint { Category = "Sub-Ind", Name = "SubIndNet", Enable = false, Value = 12.0, Penalty = 1.0, Hard = false });
			//output.Add(new Constraint { Category = "Country", Name = "US Gross", Enable = true, Value = 200.0, Penalty = 1.0, Hard = false });
			//output.Add(new Constraint { Category = "Country", Name = "US Net", Enable = true, Value = 15.0, Penalty = 1.0, Hard = false });
			//output.Add(new Constraint { Category = "Country", Name = "Max Non-US Gross", Enable = true, Value = 100.0, Penalty = 1.0, Hard = false });
			//output.Add(new Constraint { Category = "Country", Name = "Max Non-US Net", Enable = true, Value = 15.0, Penalty = 1.0, Hard = false });
			//output.Add(new Constraint { Category = "Country", Name = "NA Non-US Gross", Enable = true, Value = 100.0, Penalty = 1.0, Hard = false });
			//output.Add(new Constraint { Category = "Country", Name = "NA Non-US Net", Enable = true, Value = 15.0, Penalty = 1.0, Hard = false });
			//output.Add(new Constraint { Category = "Country", Name = "EU+UK Gross", Enable = true, Value = 50.0, Penalty = 1.0, Hard = false });
			//output.Add(new Constraint { Category = "Country", Name = "EU+UK Net", Enable = true, Value = 10.0, Penalty = 1.0, Hard = false });
			//output.Add(new Constraint { Category = "Country", Name = "APAC Gross", Enable = true, Value = 50.0, Penalty = 1.0, Hard = false });
			//output.Add(new Constraint { Category = "Country", Name = "APAC Net", Enable = true, Value = 10.0, Penalty = 1.0, Hard = false });
			//output.Add(new Constraint { Category = "Country", Name = "EM Gross", Enable = true, Value = 25.0, Penalty = 1.0, Hard = false });
			//output.Add(new Constraint { Category = "Country", Name = "EM Net", Enable = true, Value = 5.0, Penalty = 1.0, Hard = false });
			//output.Add(new Constraint { Category = "Issuer", Name = "Max Pos | Issuer", Enable = true, Value = 10.0, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "Issuer", Name = "MaxSum T 10 Longs", Enable = true, Value = 75.0, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "Issuer", Name = "MaxSum T 10 Shorts", Enable = true, Value = 65.0, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "Issuer", Name = "MaxSum T 5 Longs", Enable = true, Value = 40.0, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "Issuer", Name = "MaxSum T 5 Shorts", Enable = true, Value = 35.0, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "Position", Name = "MaxSum T 10 Longs", Enable = true, Value = 75.0, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "Position", Name = "MaxSum T 10 Shorts", Enable = true, Value = 65.0, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "Position", Name = "MaxSum T 5 Longs", Enable = true, Value = 40.0, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "Position", Name = "MaxSum T 5 Shorts", Enable = true, Value = 35.0, Penalty = 1.0, Hard = false});
			output.Add(new Constraint { Category = "Position", Name = "Under5LongCap", Enable = false, Value = 2.0, Penalty = 1.0, Hard = false});
			output.Add(new Constraint { Category = "Position", Name = "Under5ShortCap", Enable = false, Value = .0, Penalty = 1.0, Hard = false});
			output.Add(new Constraint { Category = "Liq", Name = "LiquidityADV20", Enable = false, Value = 30.0, Penalty = 1.0, Hard = false});
			output.Add(new Constraint { Category = "Liq", Name = "LiquidityADV50", Enable = false, Value = 10.0, Penalty = 1.0, Hard = false});
			output.Add(new Constraint { Category = "Liq", Name = "LiquidityADV100", Enable = false, Value = 0.0, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "Liq", Name = "LiquidityADV200", Enable = true, Value = 0.0, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "MktCap", Name = "Max Gross > 5B", Enable = true, Value = 175.0, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "MktCap", Name = "Max Net > 5B", Enable = true, Value = 15.0, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "MktCap", Name = "Max Gr 5B|1B", Enable = true, Value = 10.0, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "MktCap", Name = "Max Net 5B|1B", Enable = true, Value = 15.0, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "MktCap", Name = "Max Gr 1B|.5B", Enable = true, Value = 2.0, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "MktCap", Name = "Max Net 1B|.5B", Enable = true, Value = 2.5, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "MktCap", Name = "Max Gross <.5B", Enable = true, Value = 0.0, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "MktCap", Name = "Max Net < .5B", Enable = true, Value = 0.0, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "Opt", Name = "Max Gr Delta-Adj", Enable = true, Value = 15.0, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "Opt", Name = "Long Calls", Enable = true, Value = 10.0, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "Opt", Name = "Long Puts", Enable = true, Value = 10.0, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "Opt", Name = "Covered Calls", Enable = true, Value = 10.0, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "Opt", Name = "Covered Puts", Enable = true, Value = 10.0, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "Opt", Name = "Naked Calls", Enable = true, Value = 0.0, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "Opt", Name = "Naked Puts", Enable = true, Value = 0.0, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "Opt", Name = "Net Theta | Day", Enable = true, Value = 0.05, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "Opt", Name = "Net Vega | Vol", Enable = true, Value = 0.05, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "Greek", Name = "Min Gam | Port Lvl", VEnable = true, alue = 0.0, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "Greek", Name = "Min Vega | Port Lvl", Enable = true, Value = 0.0, Penalty = 1.0, Hard = false});
			output.Add(new Constraint { Category = "FX", Name = "CurrencyNetAbs", Enable = false, Value = 10.0, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "FX", Name = "Max ADR Pos", Enable = true, Value = 0.0, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "FX", Name = "Max Non-USD", Enable = true, Value = 12.5, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "FX", Name = "Max Gr N-USD", Enable = true, Value = 15.0, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "FX", Name = "Max Net N-USD", Enable = true, Value = 2.5, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "FX", Name = "Max Net G10", Enable = true, Value = 2.5, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "FX", Name = "Max Net EMEA", Enable = true, Value = 2.5, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "FX", Name = "Max Net LatAm", Enable = true, Value = 2.5, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "FX", Name = "Max Net Asia", Enable = true, Value = 2.5, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "FX", Name = "Max Net Sgl N-USD", Enable = true, Value = 5.0, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "Earn", Name = "EarningsGross", Enable = false, Value = 50.0, Penalty = 1.0, Hard = false });
			//output.Add(new Constraint { Category = "Earn", Name = "EarningsNetAbs", Enable = false, Value = 50.0, Penalty = 1.0, Hard = false });
			//output.Add(new Constraint { Category = "Earn", Name = "Gross Exp", Enable = true, Value = 50.0, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "Earn", Name = "Gross Explicit Exp", Enable = true, Value = 50.0, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "Earn", Name = "Sgl Name Related", Enable = true, Value = 20.0, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "Earn", Name = "Sgl Name Explicit", Enable = true, Value = 20.0, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "M&A", Name = "Max Sgl Pos M&A", Enable = true, Value = 5.0, Penalty = 1.0, Hard = false});
			output.Add(new Constraint { Category = "VaR95", Name = "MaxPortVaR95", Enable = false, Value = 1.0, Penalty = 1.0, Hard = false});
			output.Add(new Constraint { Category = "CVaR95", Name = "MaxPortCVaR95", Enable = false, Value = 0.5, Penalty = 1.0, Hard = false});
			output.Add(new Constraint { Category = "MVaR95", Name = "MaxPosVaR95", Enable = false, Value = 15.0, Penalty = 1.0, Hard = false});
			output.Add(new Constraint { Category = "Idio Risk", Name = "MinIdioRisk", Enable = false, Value = 70.0, Penalty = 1.0, Hard = false});
			output.Add(new Constraint { Category = "Fact Risk", Name = "Max Factor Risk", Enable = false, Value = 18.0, Penalty = 1.0, Hard = false});
			output.Add(new Constraint { Category = "Pred Vol", Name = "MaxPredVOL", Enable = false, Value = 10.0, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "Eq Stress", Name = "Max Loss +/- 5%", Enable = true, Value = 2.0, Penalty = 1.0, Hard = false});
			//output.Add(new Constraint { Category = "Eq Stress", Name = "Max Loss +/- 10%", Enable = true, Value = 3.5, Penalty = 1.0, Hard = false});
			
			return output;
		}

		//public static List<Constraint> getDefaultContraints2()
		//{
		//	var output = new List<Constraint>();	
		//	output.Add(new Constraint { Category = "UT", Name = "Capital Utilization", Value = 50.0 });
		//	output.Add(new Constraint { Category = "Budget", Name = "Risk Budget", Value = 2.0 });
		//	//output.Add(new Constraint { Category = "Exposure", Name = "Max Gross Book Size (Intraday)", Value = 200.0 });
		//	//output.Add(new Constraint { Category = "Exposure", Name = "Max Net Exposure (PredBeta-adjusted) (Intraday)", Value = 10.0 });
		//	//output.Add(new Constraint { Category = "Exposure", Name = "Max Net Mkt Exposure (Intraday)", Value = 10.0 });
		//	output.Add(new Constraint { Category = "Book", Name = "Max Gross", Value = 200.0 });
		//	output.Add(new Constraint { Category = "Book", Name = "Max Net Pred-Beta", Value = 10.0 });
		//	output.Add(new Constraint { Category = "Book", Name = "Max Net", Value = 10.0 });
		//	output.Add(new Constraint { Category = "Sector", Name = "Max Gross Sector", Value = 200.0 });
		//	output.Add(new Constraint { Category = "Sector", Name = "Max Net Sector", Value = 12.0 });
		//	output.Add(new Constraint { Category = "Industry", Name = "Max Gross Ind", Value = 150.0 });
		//	output.Add(new Constraint { Category = "Industry", Name = "Max Net Industry", Value = 12.0 });
		//	output.Add(new Constraint { Category = "Sub-Ind", Name = "Max Gross Sub-Ind", Value = 100.0 });
		//	output.Add(new Constraint { Category = "Sub-Ind", Name = "Max Net Sub-Ind", Value = 12.0 });
		//	output.Add(new Constraint { Category = "Country", Name = "US Gross", Value = 200.0 });
		//	output.Add(new Constraint { Category = "Country", Name = "US Net", Value = 15.0 });
		//	output.Add(new Constraint { Category = "Country", Name = "Max Non-US Gross", Value = 100.0 });
		//	output.Add(new Constraint { Category = "Country", Name = "Max Non-US Net", Value = 15.0 });
		//	output.Add(new Constraint { Category = "Country", Name = "NA Non-US Gross", Value = 100.0 });
		//	output.Add(new Constraint { Category = "Country", Name = "NA Non-US Net", Value = 15.0 });
		//	output.Add(new Constraint { Category = "Country", Name = "EU+UK Gross", Value = 50.0 });
		//	output.Add(new Constraint { Category = "Country", Name = "EU+UK Net", Value = 10.0 });
		//	output.Add(new Constraint { Category = "Country", Name = "APAC Gross", Value = 50.0 });
		//	output.Add(new Constraint { Category = "Country", Name = "APAC Net", Value = 10.0 });
		//	output.Add(new Constraint { Category = "Country", Name = "EM Gross", Value = 25.0 });
		//	output.Add(new Constraint { Category = "Country", Name = "EM Net", Value = 5.0 });
		//	output.Add(new Constraint { Category = "Issuer", Name = "Max Pos | Issuer", Value = 10.0 });
		//	output.Add(new Constraint { Category = "Sgl Name", Name = "Max Pos", Value = 10.0 });
		//	output.Add(new Constraint { Category = "Sgl Name", Name = "Max Pos Pred-Beta", Value = 10.0 });
		//	output.Add(new Constraint { Category = "Top Issuers", Name = "MaxSum T 10 Longs", Value = 75.0 });
		//	output.Add(new Constraint { Category = "Top Issuers", Name = "MaxSum T 10 Shorts", Value = 65.0 });
		//	output.Add(new Constraint { Category = "Top Issuers", Name = "MaxSum T 5 Longs", Value = 40.0 });
		//	output.Add(new Constraint { Category = "Top Issuers", Name = "MaxSum T 5 Shorts", Value = 35.0 });
		//	output.Add(new Constraint { Category = "Top Pos", Name = "MaxSum T 10 Longs", Value = 75.0 });
		//	output.Add(new Constraint { Category = "Top Pos", Name = "MaxSum T 10 Shorts", Value = 65.0 });
		//	output.Add(new Constraint { Category = "Top Pos", Name = "MaxSum T 5 Longs", Value = 40.0 });
		//	output.Add(new Constraint { Category = "Top Pos", Name = "MaxSum T 5 Shorts", Value = 35.0 });
		//	output.Add(new Constraint { Category = "STK Px", Name = "Max Gr Lg < $5", Value = 20.0 });
		//	output.Add(new Constraint { Category = "STK Px", Name = "Max Gr Sh < $5", Value = 20.0 });
		//	output.Add(new Constraint { Category = "Liq", Name = "Max Gr D Vol > 20%", Value = 30.0 });
		//	output.Add(new Constraint { Category = "Liq", Name = "Max Gr D Vol > 50%", Value = 10.0 });
		//	output.Add(new Constraint { Category = "Liq", Name = "Max Gr D Vol > 100", Value = 0.0 });
		//	output.Add(new Constraint { Category = "Liq", Name = "Max Gr D Vol > 200", Value = 0.0 });
		//	output.Add(new Constraint { Category = "Opt", Name = "Max Gr Delta-Adj", Value = 15.0 });
		//	output.Add(new Constraint { Category = "Opt", Name = "Long Calls", Value = 10.0 });
		//	output.Add(new Constraint { Category = "Opt", Name = "Long Puts", Value = 10.0 });
		//	output.Add(new Constraint { Category = "Opt", Name = "Covered Calls", Value = 10.0 });
		//	output.Add(new Constraint { Category = "Opt", Name = "Covered Puts", Value = 10.0 });
		//	output.Add(new Constraint { Category = "Opt", Name = "Naked Calls", Value = 0.0 });
		//	output.Add(new Constraint { Category = "Opt", Name = "Naked Puts", Value = 0.0 });
		//	output.Add(new Constraint { Category = "Opt", Name = "Net Theta | Day", Value = 0.05 });
		//	output.Add(new Constraint { Category = "Opt", Name = "Net Vega | Vol", Value = 0.05 });
		//	output.Add(new Constraint { Category = "MktCap", Name = "Max Gross > 5B", Value = 175.0 });
		//	output.Add(new Constraint { Category = "MktCap", Name = "Max Net > 5B", Value = 15.0 });
		//	output.Add(new Constraint { Category = "MktCap", Name = "Max Gr 5B|1B", Value = 10.0 });
		//	output.Add(new Constraint { Category = "MktCap", Name = "Max Net 5B|1B", Value = 15.0 });
		//	output.Add(new Constraint { Category = "MktCap", Name = "Max Gr 1B|.5B", Value = 2.0 });
		//	output.Add(new Constraint { Category = "MktCap", Name = "Max Net 1B|.5B", Value = 2.5 });
		//	output.Add(new Constraint { Category = "MktCap", Name = "Max Gross <.5B", Value = 0.0 });
		//	output.Add(new Constraint { Category = "MktCap", Name = "Max Net < .5B", Value = 0.0 });
		//	output.Add(new Constraint { Category = "FX", Name = "Max ADR Pos", Value = 0.0 });
		//	output.Add(new Constraint { Category = "FX", Name = "Max Non-USD", Value = 12.5 });
		//	output.Add(new Constraint { Category = "FX", Name = "Max Gr N-USD", Value = 15.0 });
		//	output.Add(new Constraint { Category = "FX", Name = "Max Net N-USD", Value = 2.5 });
		//	output.Add(new Constraint { Category = "FX", Name = "Max Net G10", Value = 2.5 });
		//	output.Add(new Constraint { Category = "FX", Name = "Max Net EMEA", Value = 2.5 });
		//	output.Add(new Constraint { Category = "FX", Name = "Max Net LatAm", Value = 2.5 });
		//	output.Add(new Constraint { Category = "FX", Name = "Max Net Asia", Value = 2.5 });
		//	output.Add(new Constraint { Category = "FX", Name = "Max Net Sgl N-USD", Value = 5.0 });
		//	output.Add(new Constraint { Category = "Earn", Name = "Gross Exp", Value = 50.0 });
		//	output.Add(new Constraint { Category = "Earn", Name = "Gross Explicit Exp", Value = 50.0 });
		//	output.Add(new Constraint { Category = "Earn", Name = "Sgl Name Related", Value = 20.0 });
		//	output.Add(new Constraint { Category = "Earn", Name = "Sgl Name Explicit", Value = 20.0 });
		//	output.Add(new Constraint { Category = "M&A", Name = "Max Sgl Pos M&A", Value = 5.0 });
		//	output.Add(new Constraint { Category = "VaR95", Name = "Max Port VaR95", Value = 1.0 });
		//	output.Add(new Constraint { Category = "CVaR95", Name = "Max Port CVaR95", Value = 0.5 });
		//	output.Add(new Constraint { Category = "MVaR95", Name = "Max Pos VaR95", Value = 15.0 });
		//	output.Add(new Constraint { Category = "Idio Risk", Name = "Min Idio Risk", Value = 70.0 });
		//	output.Add(new Constraint { Category = "Fact Risk", Name = "Max Factor Risk", Value = 18.0 });
		//	output.Add(new Constraint { Category = "Pred Vol", Name = "Max Pred VOL", Value = 10.0 });
		//	output.Add(new Constraint { Category = "Eq Stress", Name = "Max Loss +/- 5%", Value = 2.0 });
		//	output.Add(new Constraint { Category = "Eq Stress", Name = "Max Loss +/- 10%", Value = 3.5 });
		//	output.Add(new Constraint { Category = "Greek", Name = "Min Gam | Port Lvl", Value = 0.0 });
		//	output.Add(new Constraint { Category = "Greek", Name = "Min Vega | Port Lvl", Value = 0.0 });
		//	return output;
		//}
	
		public static string[] Alignments => new string[]
		{
		"Quarterly", "", "Monthly", "", "Weekly", "", "Daily", "",
		"240", "", "120", "", "60", "", "30", "", "15", "", "5",
		};

		public static string[] ATMStrategies => new string[]
		{
		"None",
		"ATM Research",
		"FT | FT", "FT | P", "FT | SC", "FT | ST", "FT | TSB",
		"SC | FT", "SC | P", "SC | SC", "SC | ST", "SC | TSB",
		"ST | FT", "ST | SC", "ST | ST", "ST | TSB",
		"TSB | FT", "TSB | SC", "TSB | ST", "TSB | TSB",
		};

		public static string[] AutoMLIntervals => new string[]
		{
		"Quarterly", "", "Monthly", "", "Weekly", "", "Daily", "",
		"240", "", "120", "", "60", "", "30", "", "15", "", "5",
		};

		public static string[] Benchmarks => new string[]
		{
		"SPY US Equity",
		"SPTSX Index",
		"MEXBOL Index",
		"MERVAL Index",
		"IBOV Index",
		"CHILE65 Index",
		"COLCAP Index",
		"SPBLPGPT Index",
		"UKX Index",
		"ISEQ Index",
		"HEX Index",
		"OBX Index",
		"OMX Index",
		"ATX Index",
		"BEL20 Index",
		"KFX Index",
		"CAC Index",
		"DAX Index",
		"ASE Index",
		"FTSEMIB Index",
		"LUXXX Index",
		"AEX Index",
		"PSI20 Index",
		"IBEX Index",
		"SMI Index",
		"SOFIX Index",
		"CRO Index",
		"PX Index",
		"TALSE Index",
		"BUX Index",
		"RIGSE Index",
		"VILSE Index",
		"WIG Index",
		"BET Index",
		"SKSM Index",
		"SBITOP Index",
		"XU100 Index",
		"IMOEX Index",
		"HERMES Index",
		"TA-125 Index",
		"JOSMGNFF Index",
		"SECTMIND Index",
		"BLOM Index",
		"MSM30 Index",
		"SASEIDX Index",
		"DSM Index",
		"DFMGI Index",
		"JALSH Index",
		"KOSPI Index",
		"NKY Index",
		"SHCOMP Index",
		"HSI Index",
		"JCI Index",
		"FBMKLCI Index",
		"PCOMP Index",
		"STI Index",
		"TWSE Index",
		"SET Index",
		"VNINDEX Index",
		"NIFTY Index",
		"KSE100 Index",
		"CSEALL Index",
		"AS51 Index",
		"NZSE50FG Index",
		"CRSPTM1 Index",
		"CRSPTMT Index",
		"CRSPSC1 Index",
		"CRSPMC1 Index",
		"CRSPMI1 Index",
		"CRSPLCVT Index",
		"CRSPSCG1 Index",
		"CRSPLCG1 Index",
		"CRSPSCT Index",
		"CRSPMIVT Index",
		"CRSPLCV1 Index",
		"CRSPIT1 Index",
		"CRSPME1 Index",
		"CRSPMIT Index",
		"CRSPLC1 Index",
		"CRSPLCGT Index",
		"CRSPRE1 Index",
		"CRSPSCV1 Index",
		"CRSPMIG1 Index",
		"CRSPLCT Index",
		"CRSPSCGT Index",
		"CRSPMIGT Index",
		"CRSPFN1 Index",
		"CRSPMIV1 Index",
		"CRSPMT1 Index",
		"CRSPSCVT Index",
		"CRSPEN1 Index",
		"CRSPCS1 Index",
		"CRSPENT Index",
		"CRSPMEGT Index",
		"CRSPMET Index",
		"CRSPHC1 Index",
		"CRSPMEVT Index",
		"CRSPTCH1 Index",
		"CRSPMCT Index",
		"CRSPSX1 Index",
		"CRSPTMCT Index",
		"CRSPUT1 Index",
		"CRSPCG1 Index",
		"CRSPSMG1 Index",
		"CRSPCGT Index",
		"CRSPCST Index",
		"CRSPFNT Index",
		"CRSPHCT Index",
		"CRSPID1 Index",
		"CRSPIDT Index",
		"CRSPITT Index",
		"CRSPMEG1 Index",
		"CRSPMEV1 Index",
		"CRSPMTT Index",
		"CRSPRET Index",
		"CRSPSM1 Index",
		"CRSPSMGT Index",
		"CRSPSMT Index",
		"CRSPSMV1 Index",
		"CRSPSMVT Index",
		"CRSPSXT Index",
		"CRSPTAH1 Index",
		"CRSPTAHT Index",
		"CRSPTCHT Index",
		"CRSPTE1 Index",
		"CRSPTET Index",
		"CRSPTMA1 Index",
		"CRSPTMAT Index",
		"CRSPTMC1 Index",
		"CRSPUTT Index",
		};

		public static string[] MLBinaryRanks => new string[]
		{
		"ACCURACY", "", "AUC", "", "AUPRC", "", "F1-SCORE",
		};

		public static string[] MLITERATIONS => new string[]
		{
		"1", "", "2", "", "3", "", "4", "", "5", "", "10",
		};

		public static string[] MLIntervals => new string[]
		{
		"Quarterly", "", "Monthly", "", "Weekly", "", "Daily", "",
		"240", "", "120", "", "60", "", "30", "", "15", "", "5", "",
		};

		public static string[] MLRegressionRanks => new string[]
		{
		"RSquared", "", "MAE", "", "MSE", "", "RMSE",
		};

		public static string[] MLSplits => new string[]
		{
		"90 | 10", "", "85 | 15", "", "80 | 20", "", "75 | 25", "",
		"70 | 30", "", "65 | 35", "", "60 | 40", "", "55 | 45", "", "50 | 50",
		};

		public static string[,] Rankings => new string[,]
		{
		{ "T 10%", "Long Only      -   Top 10%                         " },
		{ "T 20%", "Long Only      -   Top 20%                         " },
		{ "T 30%", "Long Only      -   Top 30%                         " },
		{ "T 40%", "Long Only      -   Top 40%                         " },
		{ "T 50%", "Long Only      -   Top 50%                         " },
		{ "T 60%", "Long Only      -   Top 60%                         " },
		{ "T 70%", "Long Only      -   Top 70%                         " },
		{ "T 80%", "Long Only      -   Top 80%                         " },
		{ "T 90%", "Long Only      -   Top 90%                         " },
		{ "T 100%", "Long Only     -   Top 100%                        " },
		{ "T 10% B 10%", "Long | Short   -   Top 10% and Bottom 10%    " },
		{ "T 20% B 20%", "Long | Short   -   Top 20% and Bottom 20%    " },
		{ "T 30% B 30%", "Long | Short   -   Top 30% and Bottom 30%    " },
		{ "T 40% B 40%", "Long | Short   -   Top 40% and Bottom 40%    " },
		{ "T 50% B 50%", "Long | Short   -   Top 50% and Bottom 50%    " },
		{ "T 60% B 60%", "Long | Short   -   Top 60% and Bottom 60%    " },
		{ "T 70% B 70%", "Long | Short   -   Top 70% and Bottom 70%    " },
		{ "T 80% B 80%", "Long | Short   -   Top 80% and Bottom 80%    " },
		{ "T 90% B 90%", "Long | Short   -   Top 90% and Bottom 90%    " },
		{ "T 100% B 100%", "Long | Short   -   Top 100% and Bottom 100%" },
		{ "B 10%", "Short Only      -   Bottom 10%                     " },
		{ "B 20%", "Short Only      -   Bottom 20%                     " },
		{ "B 30%", "Short Only      -   Bottom 30%                     " },
		{ "B 40%", "Short Only      -   Bottom 40%                     " },
		{ "B 50%", "Short Only      -   Bottom 50%                     " },
		{ "B 60%", "Short Only      -   Bottom 60%                     " },
		{ "B 70%", "Short Only      -   Bottom 70%                     " },
		{ "B 80%", "Short Only      -   Bottom 80%                     " },
		{ "B 90%", "Short Only      -   Bottom 90%                     " },
		{ "B 100%", "Short Only      -   Bottom 100%                    " },
		};

		public static string[] Rebalances => new string[]
		{
		"Quarterly", "", "Monthly", "", "Weekly", "", "W30", "", "Daily", "", "D30", "",
		"240", "", "120", "", "60", "", "30", "", "15", "", "5",
		};

		#endregion

		#endregion

		#region Instance Properties

		#region ATM / ML Settings (alphabetized)

		public bool Aggressive { get; set; } = true;
		public int AggressiveOffset { get; set; } = 0;
		public string ATMAnalysisInterval { get; set; } = "Daily";
		public string ATMMLInterval { get; set; } = DefaultATMMLInterval;
		public string MLInterval { get; set; } = "Daily";
		public string MLMaxBars { get; set; } = "2000";
		public string MLMaxTime { get; set; } = "30";
		public string MLModelName { get; set; } = "";
		public string MLRank { get; set; } = "ACCURACY";
		public string MLSplit { get; set; } = "80 | 20";
		public bool Trained { get; set; } = false;
		public bool UseML { get => _useML; set => _useML = value; }
		public bool UseTicker { get; set; } = false;

		public bool UseExecutionCost { get; set; } = false;
		public bool UseBorrowingCost { get; set; } = false;
		public bool UseDiv { get; set; } = false;

		#endregion

		#region Core Settings (alphabetized)

		public bool ApplyMoneyMgt { get; set; } = false;
		public string Benchmark { get; set; } = DefaultBenchmark;
		public Dictionary<string, double> Biases { get; set; } = new Dictionary<string, double>();
		public bool Compound { get; set; } = true;
		public string Conditions { get; set; } = "";
		public double CurrentPortfolioBalance { get; set; } = 1000000;
		public string EntryPeriod { get; set; }
		public List<string> FactorNames { get; set; } = new List<string>();
		public List<string> FeatureNames { get; set; } = new List<string>();
		public string Filter { get; set; } = "";

		public string Name { get; set; }
		public DateTime LiveStartDate { get; set; } = DateTime.Today;
		public bool IsLiveMode { get; set; } = false;  // ← add this
		public bool Percent { get; set; } = true;
		public string Ranking { get; set; } = DefaultRanking;
		public string RankingInterval { get; set; } = DefaultRankingInterval;

		public string Universe { 
			get 
			{
				string output = "";
				if (Groups.Count > 0)
				{
					output = Groups[0].AlphaPortfolio;
				}
				return output;
			}

			set
			{
				if (Groups.Count > 0)
				{
					Groups[0].AlphaPortfolio = value;
				}
			}
		}

		public List<Symbol> Symbols
		{
			get
			{ 
				return Groups.SelectMany(g => g.AlphaSymbols).ToList();
			}
		}

		public string TickerNavigationPaths { get; set; } = "";

		#endregion

		#region Portfolio Settings (alphabetized)

		public double FTEntryUnit { get; set; }
		public bool FreqPeriodEnable { get; set; }
		public int FreqPeriod { get; set; }
		public double InitialPortfolioBalance { get; set; } = 1000000;
		public bool MaxUnitsEnable { get; set; }
		public double PositionPercent { get; set; } = 1;
		public PortfolioWeightType PortfolioWeight { get; set; }
		public string Sector { get; set; } = ""; // no sector filter by default

		#endregion

		#region Performance & Fee Settings (alphabetized)

		public int CommissionAmt { get; set; }
		public double ManagementFee { get; set; }
		public double PerformanceFee { get; set; }
		public double PriceImpactAmt { get; set; }
		public Slippage Slippage { get; set; }
		public bool UseCommission { get; set; }
		public bool UseMgmFee { get; set; }
		public double UsePerformanceFee { get; set; }
		public bool UsePriceImpactAmt { get; set; }

		#endregion

		#region Ranges & Data Settings (alphabetized)

		public TimeRange DataRange { get; set; } = new TimeRange
		{
			Time1 = new DateTime(2022, 1, 4),
			Time2 = DateTime.UtcNow
		};
		public TimeRange TestingRange { get; set; } = new TimeRange();
		public TimeRange TrainingRange { get; set; } = new TimeRange();

		public bool UseCurrentDate { get; set; } = true;
		public bool UseInceptionDate { get; set; } = false;

		#endregion

		#region Risk Settings (alphabetized)

		public int ATRPeriod { get; set; }
		public int CoolDownPeriod { get; set; }
		public int GrossLeverage { get; set; } = 1;
		public int LongNet { get; set; }
		public double MaxDollarAmt { get; set; }
		public int MaxLongDollars { get; set; }
		public int MaxLongPercent { get; set; }
		public int MaxNumberLongs { get; set; }
		public int MaxNumberShorts { get; set; }
		public int MaxSectorExp { get; set; }
		public int MaxShortDollars { get; set; }
		public int MaxShortPercent { get; set; }
		public int MaxBetaExp { get; set; }
		public double MaxPercentAmt { get; set; }
		public string RiskInterval { get; set; } = "Daily";
		public double RiskBudget { get; set; }
		public int ShortNet { get; set; }
		public SizeRecommendation SizeRecommendation { get; set; }
		public string StopLossPercent { get; set; }
		public bool TargetPercent { get; set; }
		public double TradeRiskPercent { get; set; }
		public bool UseATMFactors { get; set; }
		public bool UseATMTimingLong { get; set; }
		public bool UseATMTimingShort { get; set; }
		public bool UseCoolDown { get; set; }
		public bool UseFTEntry { get; set; }
		public bool UseHedge { get; set; }
		public bool UseBetaHedge { get; set; }
		public bool UseRiskOnOff { get; set; }
		public bool UseBetaNeutral { get; set; }
		public bool UseMktNeutral { get; set; }
		//public bool UseBetaAdjust { get; set; }
		public bool UseMaxBetaExp { get; set; }
		public bool UseMaxLongDollars { get; set; }
		public bool UseMaxLongPercent { get; set; }
		public bool UseMaxNumberLongs { get; set; }
		public bool UseMaxNumberShorts { get; set; }
		public bool UseMaxSectorExp { get; set; }
		public bool UseMaxShortDollars { get; set; }
		public bool UseMaxShortPercent { get; set; }
		public bool UseMTExit { get; set; }
		public bool UsePortRisk1 { get; set; }
		public bool UsePortRisk2 { get; set; }
		public bool UsePortRisk3 { get; set; }

		public bool UseRiskEngine2 { get; set; }
		public bool UseRiskEngine3 { get; set; }
		public bool UseRiskEngine4 { get; set; }

		public bool UseStopLossPercent { get; set; }
		public bool UseTargetPercent { get; set; }
		public bool UseTradeRisk { get; set; }

		public double PortRiskPercent1 { get; set; }
		public double PortRiskPercent2 { get; set; }
		public double PortRiskPercent3 { get; set; }

		#endregion

		#region Strategy Flags (alphabetized)

		public bool cbLTFTBuy { get; set; }
		public bool cbLTFTDn { get; set; }
		public bool cbLTFTOS { get; set; }
		public bool cbLTFTOB { get; set; }
		public bool cbLTFTSell { get; set; }
		public bool cbLTFTUp { get; set; }
		public bool cbLTNetLong { get; set; }
		public bool cbLTNetShort { get; set; }
		public bool cbLTSCBuy { get; set; }
		public bool cbLTSCSell { get; set; }
		public bool cbLTScoreDn { get; set; }
		public bool cbLTScoreUp { get; set; }
		public bool cbLTSTBuy { get; set; }
		public bool cbLTSTDn { get; set; }
		public bool cbLTSTSell { get; set; }
		public bool cbLTSTUp { get; set; }
		public bool cbLTTBDn { get; set; }
		public bool cbLTTBUp { get; set; }
		public bool cbLTTLDn { get; set; }
		public bool cbLTTLUp { get; set; }
		public bool cbLTTSBDn { get; set; }
		public bool cbLTTSBUp { get; set; }

		public bool cbMTFTBuy { get; set; }
		public bool cbMTFTDn { get; set; }
		public bool cbMTFTOS { get; set; }
		public bool cbMTFTOB { get; set; }
		public bool cbMTFTSell { get; set; }
		public bool cbMTFTUp { get; set; }
		public bool cbMTNetLong { get; set; }
		public bool cbMTNetShort { get; set; }
		public bool cbMTSCBuy { get; set; }
		public bool cbMTSCSell { get; set; }
		public bool cbMTScoreDn { get; set; }
		public bool cbMTScoreUp { get; set; }
		public bool cbMTSTBuy { get; set; }
		public bool cbMTSTDn { get; set; }
		public bool cbMTSTSell { get; set; }
		public bool cbMTSTUp { get; set; }
		public bool cbMTTBDn { get; set; }
		public bool cbMTTBUp { get; set; }
		public bool cbMTTLDn { get; set; }
		public bool cbMTTLUp { get; set; }
		public bool cbMTTSBDn { get; set; }
		public bool cbMTTSBUp { get; set; }

		public bool cbSTFTBuy { get; set; }
		public bool cbSTFTDn { get; set; }
		public bool cbSTFTOS { get; set; }
		public bool cbSTFTOB { get; set; }
		public bool cbSTFTSell { get; set; }
		public bool cbSTFTUp { get; set; }
		public bool cbSTNetLong { get; set; }
		public bool cbSTNetShort { get; set; }
		public bool cbSTPUp { get; set; }
		public bool cbSTPDn { get; set; }
		public bool cbSTSCBuy { get; set; }
		public bool cbSTSCSell { get; set; }
		public bool cbSTScoreDn { get; set; }
		public bool cbSTScoreUp { get; set; }
		public bool cbSTSTBuy { get; set; }
		public bool cbSTSTDn { get; set; }
		public bool cbSTSTSell { get; set; }
		public bool cbSTSTUp { get; set; }
		public bool cbSTTBDn { get; set; }
		public bool cbSTTBUp { get; set; }
		public bool cbSTTLDn { get; set; }
		public bool cbSTTLUp { get; set; }
		public bool cbSTTSBDn { get; set; }
		public bool cbSTTSBUp { get; set; }

		public bool LongNet0 { get; set; }
		public bool LongNet1 { get; set; }
		public bool LongNet2 { get; set; }
		public bool LongNet3 { get; set; }

		public bool rbCurrentClose { get; set; }
		public bool rbLTLong0 { get; set; }
		public bool rbLTLong1 { get; set; }
		public bool rbLTLong2 { get; set; }
		public bool rbLTLong3 { get; set; }
		public bool rbLTShort0 { get; set; }
		public bool rbLTShort1 { get; set; }
		public bool rbLTShort2 { get; set; }
		public bool rbLTShort3 { get; set; }
		public bool rbMTLong0 { get; set; }
		public bool rbMTLong1 { get; set; }
		public bool rbMTLong2 { get; set; }
		public bool rbMTLong3 { get; set; }
		public bool rbMTShort0 { get; set; }
		public bool rbMTShort1 { get; set; }
		public bool rbMTShort2 { get; set; }
		public bool rbMTShort3 { get; set; }
		public bool rbNextBarOpen { get; set; }
		public bool rbNextBarVWAP { get; set; }
		public bool rbVWAP { get; set; }

		public bool ShortNet0 { get; set; }
		public bool ShortNet1 { get; set; }
		public bool ShortNet2 { get; set; }
		public bool ShortNet3 { get; set; }

		#endregion

		#region Strategy Tuning (alphabetized)

		public string ConditionsDetailed => Conditions; // read-only alias if needed
		public string RiskIntervalDetail => RiskInterval; // read-only alias if needed
		
		#endregion

		#region Toggle Groups (alphabetized)

		public bool UseFilters { get => _useFilters; set => _useFilters = value; }
		public bool UseFiltersToggle => UseFilters;
		public bool UseRanking { get => _useRanking; set => _useRanking = value; }

		#endregion

		#region Backing Fields

		private bool _useATMStrategies = false;
		private bool _useFilters = false;
		private bool _useML = false;
		private bool _useRanking = false;

		#endregion

		#endregion

		#region Public Static Methods

		public static string GetRanking(string input)
		{
			string output = input;
			int count = Rankings.GetLength(0);
			for (int ii = 0; ii < count; ii++)
			{
				if (input == Rankings[ii, 1])
				{
					output = Rankings[ii, 0];
					break;
				}
			}
			return output;
		}

		public static string GetRankingDescription(string input)
		{
			string output = input;
			int count = Rankings.GetLength(0);
			for (int ii = 0; ii < count; ii++)
			{
				if (input == Rankings[ii, 0])
				{
					output = Rankings[ii, 1];
					break;
				}
			}
			return output;
		}

		public static string[] RankingDescriptions
		{
			get
			{
				int count = Rankings.GetLength(0);
				string[] output = new string[count];
				for (int ii = 0; ii < count; ii++)
				{
					output[ii] = Rankings[ii, 1];
				}
				return output;
			}
		}

		public static Model getDefaultModel(string name)
		{
			var text = "1," + name + @",4,OPER_MARGIN,PROF_MARGIN,RETURN_ON_ASSET,RETURN_COM_EQY,,,,SPX Index,Daily,T 30%,OEX,1/4/2022 12:00:00 AM,7/3/2020 1:47:31 PM,True,Equal,,0,0,1,0,0,0,0,0,0,0,CurrentClose,0,0,True,True,False,False,False,False,False,False,True,0,False,True,False,3,-3,False,,True,,False,False,False,False,False,False,False,False,False,False,False,False,False,False,False,False,False,False,True,True,False,False,False,False,False,False,False,False,False,False,False,False,False,False,False,False,False,False,False,False,False,False,False,False,False,False,False,False,False,False,False,False,True,,False,False,False,False,False,,,,,,,,,,,,,,False,False,False,False,False,False,False,False,False,False,False,False,False,False,False,False,False,False,False,False,False,False,False,False,False,False,False,False,False,False,Close1AgoClose1Plus,False,False,True,False,False,True,FullyInvested,0,0,ACCURACY,10,False,Daily,20,ATM FT     Short Term, FT Going Up     Short Term, FT Going Dn     Short Term, ATM ST     Short Term, ST Going Up     Short Term, ST Going Dn     Short Term, TSB Bullish     Short Term, TSB Bearish     Short Term, TL Bullish     Short Term, TL Bearish     Short Term, TB Bullish     Short Term, TB Bearish     Short Term, Current SC Sig     Short Term, Current PR Sig     Short Term, ATM FT     Mid Term, FT Going Up     Mid Term, FT Going Dn     Mid Term, ATM ST     Mid Term, ST Going Up     Mid Term, ST Going Dn     Mid Term,10,80 | 20,False,0,True,True,FT | SC,False,101,AAPL UW Equity                                      ,ABBV UN Equity                                     ,ABT UN Equity                                      ,ACN UN Equity                                      ,ADBE UW Equity                                     ,AIG UN Equity                                      ,ALL UN Equity                                      ,AMGN UW Equity                                     ,AMT UN Equity                                      ,AMZN UW Equity                                     ,AXP UN Equity                                      ,BA UN Equity                                       ,BAC UN Equity                                      ,BIIB UW Equity                                     ,BK UN Equity                                       ,BKNG UW Equity                                     ,BLK UN Equity                                      ,BMY UN Equity                                      ,BRK/B UN Equity, C UN Equity, CAT UN Equity, CHTR UW Equity, CL UN Equity, CMCSA UW Equity, COF UN Equity, COP UN Equity, COST UW Equity, CRM UN Equity, CSCO UW Equity, CVS UN Equity, CVX UN Equity, DD UN Equity, DHR UN Equity, DIS UN Equity, DOW UN Equity, DUK UN Equity, EMR UN Equity, EXC UW Equity, F UN Equity, FB UW Equity, FDX UN Equity, GD UN Equity, GE UN Equity, GILD UW Equity, GM UN Equity, GOOG UW Equity, GOOGL UW Equity, GS UN Equity, HD UN Equity, HON UN Equity, IBM UN Equity, INTC UW Equity, JNJ UN Equity, JPM UN Equity, KHC UN Equity, KMI UN Equity, KO UN Equity, LLY UN Equity, LMT UN Equity, LOW UN Equity, MA UN Equity, MCD UN Equity, MDLZ UW Equity, MDT UN Equity, MET UN Equity, MMM UN Equity, MO UN Equity, MRK UN Equity, MS UN Equity, MSFT UW Equity, NEE UN Equity, NFLX UW Equity, NKE UN Equity, NVDA UW Equity, ORCL UN Equity, OXY UN Equity, PEP UW Equity, PFE UN Equity, PG UN Equity, PM UN Equity, PYPL UW Equity, QCOM UW Equity, RTX UN Equity, SBUX UW Equity, SLB UN Equity, SO UN Equity, SPG UN Equity, T UN Equity, TGT UN Equity, TMO UN Equity, TXN UW Equity, UNH UN Equity, UNP UN Equity, UPS UN Equity, USB UN Equity, V UN Equity, VZ UN Equity, WBA UW Equity, WFC UN Equity, WMT UN Equity, XOM UN Equity";
			var model = Model.load(text);
			return model;
		}

		[JsonIgnore]
		public bool NeedsLoading = false;

		public static Model load(string input)
		{
			Model m = null;
			bool bp = false;
			try
			{
				m = JsonConvert.DeserializeObject<Model>(input);

				if (m.RiskInterval == null)
				{
					m.RiskInterval = "Daily";
				}

				//if (m.Constraints.Count != getDefaultConstraints().Count)
				//{
				//	m.Constraints = getDefaultConstraints();
				//}
			}
			catch(Exception x) {
				bp = true;
			}

			return m;
		}

		#endregion

		#region Public Instance Methods

		public Model(string name = "")
		{
			Name = name;
		}

		public bool Equals(Model other)
		{
			var output = false;
			if (other != null)
			{
				var ok1 = MLMaxBars.Equals(other.MLMaxBars);
				var ok2 = !FactorNames.Except(other.FactorNames).Any();
				var ok3 = !other.FactorNames.Except(FactorNames).Any();
				var ok4 = !FeatureNames.Except(other.FeatureNames).Any();
				var ok5 = !other.FeatureNames.Except(FeatureNames).Any();
				var ok6 = Universe.Equals(other.Universe);
				var ok7 = RankingInterval.Equals(other.RankingInterval);
				var ok8 = Groups[0].AlignmentInterval.Equals(other.Groups[0].AlignmentInterval);
				var ok9 = ATMMLInterval.Equals(other.ATMMLInterval);
				var ok10 = Ranking.Equals(other.Ranking);
				var ok11 = Benchmark.Equals(other.Benchmark);
				var ok12 = DataRange.Equals(other.DataRange);
				var ok13 = UseCurrentDate.Equals(other.UseCurrentDate);
				var ok14 = UseInceptionDate.Equals(other.UseInceptionDate);
				var ok15 = MLInterval.Equals(other.MLInterval);
				var ok16 = MLMaxTime.Equals(other.MLInterval);
				var ok17 = MLSplit.Equals(other.MLInterval);

				output = ok1 && ok2 && ok3 && ok4 && ok5 && ok6 && ok7 && ok8 && ok9 && ok10 && ok11 && ok12 && ok13 && ok14 && ok15 && ok16 && ok17;
			}

			return output;
		}

		public string GetDescription(string input)
		{
			string output = "";
			if (Symbols != null)
			{
				foreach (var symbol in Symbols)
				{
					if (symbol.Ticker == input)
					{
						output = symbol.Description;
						break;
					}
				}
			}
			return output;
		}

		public string GetInterval()
		{
			string output = "Daily";

			string period = RankingInterval;

			if (period == "Weekly")
			{
				output = "Weekly";
			}
			else if (period == "Monthly")
			{
				output = "Monthly";
			}
			else if (period == "Quarterly")
			{
				output = "Quarterly";
			}
			else if (period == "Semi Annual")
			{
				output = "SemiAnnually";
			}
			else if (period == "Annually")
			{
				output = "Yearly";
			}
			else if (period == "240")
			{
				output = "240";
			}
			else if (period == "120")
			{
				output = "120";
			}
			else if (period == "60")
			{
				output = "60";
			}
			else if (period == "30")
			{
				output = "30";
			}
			else if (period == "15")
			{
				output = "15";
			}
			else if (period == "5")
			{
				output = "5";
			}

			return output;
		}

		public string GetSector(string input)
		{
			string output = "";
			if (Symbols != null)
			{
				foreach (var symbol in Symbols)
				{
					if (symbol.Ticker == input)
					{
						output = symbol.Sector;
						break;
					}
				}
			}
			return output;
		}

		public double GetPortfolioPercent()
		{
			if (Ranking == "T 10%") return 10;
			else if (Ranking == "T 20%") return 20;
			else if (Ranking == "T 30%") return 30;
			else if (Ranking == "T 40%") return 40;
			else if (Ranking == "T 50%") return 50;
			else if (Ranking == "T 60%") return 60;
			else if (Ranking == "T 70%") return 70;
			else if (Ranking == "T 80%") return 80;
			else if (Ranking == "T 90%") return 90;
			else if (Ranking == "T 100%") return 100;
			else if (Ranking == "T 10% B 10%") return 10;
			else if (Ranking == "T 20% B 20%") return 20;
			else if (Ranking == "T 30% B 30%") return 30;
			else if (Ranking == "T 40% B 40%") return 40;
			else if (Ranking == "T 50% B 50%") return 50;
			else if (Ranking == "T 60% B 60%") return 60;
			else if (Ranking == "T 70% B 70%") return 70;
			else if (Ranking == "T 80% B 80%") return 80;
			else if (Ranking == "T 90% B 90%") return 90;
			else if (Ranking == "T 100% B 100%") return 100;
			else if (Ranking == "B 10%") return 10;
			else if (Ranking == "B 20%") return 20;
			else if (Ranking == "B 30%") return 30;
			else if (Ranking == "B 40%") return 40;
			else if (Ranking == "B 50%") return 50;
			else if (Ranking == "B 60%") return 60;
			else if (Ranking == "B 70%") return 70;
			else if (Ranking == "B 80%") return 80;
			else if (Ranking == "B 90%") return 90;
			else if (Ranking == "B 100%") return 100;
			else return 50;
		}

		public string save()
		{
			var output = "";
			var bp = false;
			try
			{
				output = JsonConvert.SerializeObject(this);
			}
			catch (Exception x)
			{
				bp = true;
			}
			return output;
		}

		public void SetTimeRanges()
		{
			bool splitTimeRange = false;

			var currentDate = DateTime.UtcNow;

			if (splitTimeRange)
			{
				// Split time range 80/20 into Training/Testing ranges
				double split = 80.0 / 100.0;
				DateTime date1 = DataRange.Time1;
				DateTime date2 = UseCurrentDate ? currentDate : DataRange.Time2;
				TimeSpan timeSpan = date2 - date1;
				int minutes = (int)Math.Round(split * timeSpan.TotalMinutes);
				TimeSpan trainSpan = new TimeSpan(0, 0, minutes, 0);
				DateTime splitDate = date1 + trainSpan;
				TrainingRange.Time1 = date1;
				TrainingRange.Time2 = splitDate;
				TestingRange.Time1 = splitDate;
				TestingRange.Time2 = date2;
			}
			else
			{
				TrainingRange.Time1 = DataRange.Time1;
				TrainingRange.Time2 = UseCurrentDate ? currentDate : DataRange.Time2;
				TestingRange.Time1 = DataRange.Time1;
				TestingRange.Time2 = UseCurrentDate ? currentDate : DataRange.Time2;
			}
		}

		#endregion

		#region Private / Internal Methods

		private void clearStrategies()
		{
			cbLTFTUp = false;
			cbLTFTDn = false;
			cbLTSTUp = false;
			cbLTScoreUp = false;
			cbMTScoreUp = false;
			cbSTScoreUp = false;
			cbLTScoreDn = false;
			cbMTScoreDn = false;
			cbSTScoreDn = false;
			cbLTSTDn = false;
			cbLTTSBUp = false;
			cbLTTSBDn = false;
			cbLTTBUp = false;
			cbLTTBDn = false;
			cbLTTLUp = false;
			cbLTTLDn = false;
			cbLTSCBuy = false;
			cbLTSCSell = false;
			cbLTSTBuy = false;
			cbLTSTSell = false;
			cbLTFTBuy = false;
			cbLTFTSell = false;
			cbLTNetLong = false;
			cbLTNetShort = false;
			rbLTLong0 = false;
			rbLTShort0 = false;
			rbLTLong1 = false;
			rbLTShort1 = false;
			rbLTLong2 = false;
			rbLTShort2 = false;
			rbLTLong3 = false;
			rbLTShort3 = false;
			cbMTFTUp = false;
			cbMTFTDn = false;
			cbMTSCBuy = false;
			cbMTSCSell = false;
			cbMTSTBuy = false;
			cbMTSTSell = false;
			cbMTFTBuy = false;
			cbMTFTSell = false;
			cbMTNetLong = false;
			cbMTNetShort = false;
			rbMTLong0 = false;
			rbMTShort0 = false;
			rbMTLong1 = false;
			rbMTShort1 = false;
			rbMTLong2 = false;
			rbMTShort2 = false;
			rbMTLong3 = false;
			rbMTShort3 = false;
			cbMTSTUp = false;
			cbMTSTDn = false;
			cbMTTSBUp = false;
			cbMTTSBDn = false;
			cbMTTBUp = false;
			cbMTTBDn = false;
			cbMTTLUp = false;
			cbMTTLDn = false;
			cbSTFTUp = false;
			cbSTFTDn = false;
			cbSTSCBuy = false;
			cbSTSCSell = false;
			cbSTSTBuy = false;
			cbSTSTSell = false;
			cbSTFTBuy = false;
			cbSTFTSell = false;
			cbSTNetLong = false;
			cbSTNetShort = false;
			LongNet0 = false;
			ShortNet0 = false;
			LongNet1 = false;
			ShortNet1 = false;
			LongNet2 = false;
			ShortNet2 = false;
			LongNet3 = false;
			ShortNet3 = false;
			cbSTSTUp = false;
			cbSTSTDn = false;
			cbSTTSBUp = false;
			cbSTTSBDn = false;
			cbSTTBUp = false;
			cbSTTBDn = false;
			cbSTTLUp = false;
			cbSTTLDn = false;
			rbCurrentClose = false;
			rbNextBarOpen = false;
			rbVWAP = false;
			rbNextBarVWAP = false;
			cbLTFTOB = false;
			cbMTFTOB = false;
			cbSTFTOB = false;
			cbLTFTOS = false;
			cbMTFTOS = false;
			cbSTFTOS = false;
		}

		private string getStrategy()
		{
			string output = "";
			if (cbSTFTUp && cbMTFTUp && cbSTFTDn && cbMTFTDn)
			{
				output = "FT | FT";
			}
			else if (cbSTScoreUp && cbMTFTUp && cbSTScoreDn && cbMTFTDn)
			{
				output = "FT | SC";
			}
			else if (cbSTSTUp && cbMTFTUp && cbSTSTDn && cbMTFTDn)
			{
				output = "FT | ST";
			}
			else if (cbSTTSBUp && cbMTFTUp && cbSTTSBDn && cbMTFTDn)
			{
				output = "FT | TSB";
			}
			else if (cbSTFTUp && cbMTSTUp && cbSTFTDn && cbMTSTDn)
			{
				output = "ST | FT";
			}
			else if (cbSTScoreUp && cbMTSTUp && cbSTScoreDn && cbMTSTDn)
			{
				output = "ST | SC";
			}
			else if (cbSTSTUp && cbMTSTUp && cbSTSTDn && cbMTSTDn)
			{
				output = "ST | ST";
			}
			else if (cbSTTSBUp && cbMTSTUp && cbSTTSBDn && cbMTSTDn)
			{
				output = "ST | TSB";
			}
			else if (cbSTFTUp && cbMTScoreUp && cbSTFTDn && cbMTScoreDn)
			{
				output = "SC | FT";
			}
			else if (cbSTScoreUp && cbMTScoreUp && cbSTScoreDn && cbMTScoreDn)
			{
				output = "SC | SC";
			}
			else if (cbSTSTUp && cbMTScoreUp && cbSTSTDn && cbMTScoreDn)
			{
				output = "SC | ST";
			}
			else if (cbSTTSBUp && cbMTScoreUp && cbSTTSBDn && cbMTScoreDn)
			{
				output = "SC | TSB";
			}

			else if (cbSTFTUp && cbMTTSBUp && cbSTFTDn && cbMTTSBDn)
			{
				output = "TSB | FT";
			}
			else if (cbSTScoreUp && cbMTTSBUp && cbSTScoreDn && cbMTTSBDn)
			{
				output = "TSB | SC";
			}
			else if (cbSTSTUp && cbMTTSBUp && cbSTSTDn && cbMTTSBDn)
			{
				output = "TSB | ST";
			}
			else if (cbSTTSBUp && cbMTTSBUp && cbSTTSBDn && cbMTTSBDn)
			{
				output = "TSB | TSB";
			}
			else if (cbSTFTUp && cbSTFTDn)
			{
				output = "FT";
			}
			else if (cbSTSTUp && cbSTSTDn)
			{
				output = "ST";
			}
			else if (cbSTScoreUp && cbSTScoreDn)
			{
				output = "SC";
			}
			return output;
		}

		private void setStrategy(string input)
		{
			clearStrategies();
			if (input == "FT")
			{
				cbMTFTUp = true;
				cbMTFTDn = true;
			}
			else if (input == "ST")
			{
				cbMTSTUp = true;
				cbMTSTDn = true;
			}
			else if (input == "SC")
			{
				cbSTScoreUp = true;
				cbSTScoreDn = true;
			}
			else if (input == "FT | P")
			{
				cbSTPUp = true;
				cbMTFTUp = true;
				cbSTPDn = true;
				cbMTFTDn = true;
			}
			else if (input == "FT | FT")
			{
				cbSTFTUp = true;
				cbMTFTUp = true;
				cbSTFTDn = true;
				cbMTFTDn = true;
			}
			else if (input == "FT | SC")
			{
				cbSTScoreUp = true;
				cbMTFTUp = true;
				cbSTScoreDn = true;
				cbMTFTDn = true;
			}
			else if (input == "FT | ST")
			{
				cbSTSTUp = true;
				cbMTFTUp = true;
				cbSTSTDn = true;
				cbMTFTDn = true;
			}
			else if (input == "FT | TSB")
			{
				cbSTTSBUp = true;
				cbMTFTUp = true;
				cbSTTSBDn = true;
				cbMTFTDn = true;
			}
			else if (input == "ST | FT")
			{
				cbSTFTUp = true;
				cbMTSTUp = true;
				cbSTFTDn = true;
				cbMTSTDn = true;
			}
			else if (input == "ST | SC")
			{
				cbSTScoreUp = true;
				cbMTSTUp = true;
				cbSTScoreDn = true;
				cbMTSTDn = true;
			}
			else if (input == "ST | ST")
			{
				cbSTSTUp = true;
				cbMTSTUp = true;
				cbSTSTDn = true;
				cbMTSTDn = true;
			}
			else if (input == "ST | TSB")
			{
				cbSTTSBUp = true;
				cbMTSTUp = true;
				cbSTTSBDn = true;
				cbMTSTDn = true;
			}
			else if (input == "SC | FT")
			{
				cbSTFTUp = true;
				cbMTScoreUp = true;
				cbSTFTDn = true;
				cbMTScoreDn = true;
			}
			else if (input == "SC | SC")
			{
				cbSTScoreUp = true;
				cbMTScoreUp = true;
				cbSTScoreDn = true;
				cbMTScoreDn = true;
			}
			else if (input == "SC | ST")
			{
				cbSTSTUp = true;
				cbMTScoreUp = true;
				cbSTSTDn = true;
				cbMTScoreDn = true;
			}
			else if (input == "SC | TSB")
			{
				cbSTTSBUp = true;
				cbMTScoreUp = true;
				cbSTTSBDn = true;
				cbMTScoreDn = true;
			}
			else if (input == "TSB | FT")
			{
				cbSTFTUp = true;
				cbMTTSBUp = true;
				cbSTFTDn = true;
				cbMTTSBDn = true;
			}
			else if (input == "TSB | SC")
			{
				cbSTScoreUp = true;
				cbMTTSBUp = true;
				cbSTScoreDn = true;
				cbMTTSBDn = true;
			}
			else if (input == "TSB | ST")
			{
				cbSTSTUp = true;
				cbMTTSBUp = true;
				cbSTSTDn = true;
				cbMTTSBDn = true;
			}
			else if (input == "TSB | TSB")
			{
				cbSTTSBUp = true;
				cbMTTSBUp = true;
				cbSTTSBDn = true;
				cbMTTSBDn = true;
			}
		}

		#endregion

		#region Other Members & Enums

		public enum PortfolioWeightType
		{
			Equal,
			MarketCap,
			Price
		}

		public Senario Scenario { get; set; }

		#endregion
	}
}
