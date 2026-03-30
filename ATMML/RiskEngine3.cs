using MathNet.Numerics.Statistics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static Bloomberglp.Blpapi.Logging;
using static TorchSharp.torch.utils;

namespace RiskEngine3
{

	// Bloomberg Field Codes for Data Retrieval
	public static class BloombergFields
	{
		// Price and Volume
		public const string PX_LAST = "PX_LAST";                     // Last Price
		public const string PX_VOLUME = "PX_VOLUME";                 // Volume
		public const string PX_BID = "PX_BID";                       // Bid Price
		public const string PX_ASK = "PX_ASK";                       // Ask Price
		public const string VOLUME_AVG_30D = "VOLUME_AVG_30D";       // 30-day Average Volume

		// Volatility and Risk
		public const string VOLATILITY_30D = "VOLATILITY_30D";       // 30-day Volatility
		public const string VOLATILITY_60D = "VOLATILITY_60D";       // 60-day Volatility
		public const string VOLATILITY_90D = "VOLATILITY_90D";       // 90-day Volatility
		public const string HIST_BETA = "HIST_BETA";                 // Historical Beta
		public const string ADJ_BETA = "ADJ_BETA";                   // Adjusted Beta
		public const string AVERAGE_TRUE_RANGE = "AVERAGE_TRUE_RANGE"; // ATR

		// Classification
		public const string GICS_SECTOR_NAME = "GICS_SECTOR_NAME";   // GICS Sector
		public const string GICS_INDUSTRY_GROUP_NAME = "GICS_INDUSTRY_GROUP_NAME"; // Industry
		public const string GICS_INDUSTRY_NAME = "GICS_INDUSTRY_NAME"; // Sub-Industry

		// Index Sensitivity
		public const string BETA_SP_500 = "BETA_S&P_500";          // Beta to S&P 500
        public const string CORR_SP_500 = "CORR_S&P_500";          // Correlation to S&P 500
        public const string DELTA_ADJUSTED_EXPOSURE = "DELTA_ADJUSTED_EXPOSURE"; // For options

		// Financial Data
		public const string MARKET_CAP = "CUR_MKT_CAP";                      // Market Cap
		public const string HISTORICAL_MARKET_CAP = "HISTORICAL_MARKET_CAP"; // Historical Market Cap
		public const string EQY_SH_OUT = "EQY_SH_OUT";                       // Shares Outstanding
		public const string DVD_YIELD = "DIVIDEND_YIELD";                    // Dividend Yield

		// Risk Factors
		public const string BEST_EPS_GROWTH = "BEST_EPS_GROWTH";     // EPS Growth
		public const string PE_RATIO = "PE_RATIO";                   // P/E Ratio
		public const string PB_RATIO = "PX_TO_BOOK_RATIO";          // P/B Ratio
		public const string ROE = "RETURN_ON_EQUITY";                // ROE

		// Correlation Data
		public const string CORR_MATRIX = "CORR_MATRIX";             // Correlation Matrix
		public const string CORR_COEF_SPX = "CORR_COEF_SPX";        // Correlation to S&P 500
	}

	// Enums
	public enum PositionType
	{
		Long,
		Short,
		Hedge
	}

	public enum OptimizationObjective
	{
		MinimizeRisk,
		MaximizeSharpeRatio,
		RiskParity,
		FactorNeutral
	}

	public enum RiskFactorType
	{
		Market,
		Size,
		Value,
		Momentum,
		Quality,
		Volatility,
		Sector,
		Industry,
		SubIndustry
	}

	// Market Data Classes
	public class PriceBar
	{
		public DateTime Timestamp { get; set; }
		public double Open { get; set; }
		public double High { get; set; }
		public double Low { get; set; }
		public double Close { get; set; }
		public double Volume { get; set; }
		public double Bid { get; set; }
		public double Ask { get; set; }
		public double ATR { get; set; }
		public double AvgVol { get; set; }
		public double RealizedVolatility { get; set; }

		public double MidPrice => (Bid + Ask) / 2;
		public double Spread => Ask - Bid;
		public double SpreadBps => Spread / MidPrice * 10000;
	}

	public class MarketDataSnapshot
	{
		public DateTime Timestamp { get; set; }
		public Dictionary<string, PriceBar> PriceBars { get; set; }
		public Dictionary<string, Dictionary<string, double>> Correlations { get; set; }
		public double MarketVolatility { get; set; }

		public MarketDataSnapshot()
		{
			PriceBars = new Dictionary<string, PriceBar>();
			Correlations = new Dictionary<string, Dictionary<string, double>>();
		}
	}

	// Enhanced Position class with classification
	public class Position
	{
		public string Symbol { get; set; }
		public string ISIN { get; set; }
		public string SEDOL { get; set; }
		public PositionType Type { get; set; }

		// Classification
		public string Sector { get; set; }
		public string Industry { get; set; }
		public string SubIndustry { get; set; }

		// Market Data
		public double Price { get; set; }
		public double Bid { get; set; }
		public double Ask { get; set; }
		public double ATR { get; set; }
		public double DailyVolatility { get; set; }
		public double AverageVolume30D { get; set; }

		// Risk Metrics
		public double ExpectedReturn { get; set; }
		public double HistoricalBeta { get; set; }
		public double AdjustedPredictiveBeta { get; set; }
		public FactorExposures FactorExposures { get; set; }

		// Position Details
		public double TargetWeight { get; set; }
		public double CurrentWeight { get; set; }
		public double InvestmentAmount { get; set; }
		public int Shares { get; set; }
		public bool IsHedge { get; set; }
		public string HedgeTarget { get; set; }
		public double HedgeRatio { get; set; }

		// Risk Contributions
		public double RiskContribution { get; set; }
		public double MarginalVaR { get; set; }
		public double ComponentVaR { get; set; }
		public double PredictedVolatility { get; set; }

		// Trading Constraints
		public double MaxPositionPercent { get; set; } = 0.10;
		public double MinPositionPercent { get; set; } = 0.01;
		public double MaxDailyTurnover { get; set; } = 0.20; // 20% of ADV

		public void UpdateMarketData(PriceBar priceBar)
		{
			Price = priceBar.Close;
			Bid = priceBar.Bid;
			Ask = priceBar.Ask;
			ATR = priceBar.ATR;
			AverageVolume30D = priceBar.AvgVol;
			DailyVolatility = priceBar.RealizedVolatility;
		}

		public void UpdatePosition(double investmentAmount, double totalPortfolioValue)
		{
			bool bp = false;
			try
			{
				InvestmentAmount = investmentAmount;
				Shares = (int)(investmentAmount / Price);
				CurrentWeight = investmentAmount / totalPortfolioValue;
			}
			catch (Exception)
			{
				bp = true;
			}
		}

		public double GetEffectiveBeta() => IsHedge ? -AdjustedPredictiveBeta * HedgeRatio : AdjustedPredictiveBeta;

		public double GetTradingCost()
		{
			double spreadCost = (Ask - Bid) / Price * 0.5; // Half spread
			double impactCost = 0.0001 * (double)Math.Sqrt((double)(Shares / AverageVolume30D));
			return spreadCost + impactCost;
		}
	}

	// Factor Exposures
	public class FactorExposures
	{
		public Dictionary<RiskFactorType, double> Exposures { get; set; }

		public FactorExposures()
		{
			Exposures = new Dictionary<RiskFactorType, double>();
			foreach (RiskFactorType factor in Enum.GetValues(typeof(RiskFactorType)))
			{
				Exposures[factor] = 0;
			}
		}
	}

	// Enhanced Portfolio Constraints with new limits
	public class PortfolioConstraints
	{
		// Position Level
		public double MaxPositionPercent { get; set; } = 0.10;
		public double MinPositionPercent { get; set; } = 0.01;

		// Portfolio Level
		public double MaxGrossBookPercent { get; set; } = 2.00;
		public double MaxNetExposurePercent { get; set; } = 0.10;

		// Sector Constraints
		public double MaxGrossSectorExposure { get; set; } = 1.75;          // 175%
		public double MaxNetSectorExposure { get; set; } = 0.12;            // 12%
		public double MaxNetBetaSectorExposure { get; set; } = 0.12;        // 12%

		// Industry Constraints
		public double MaxGrossIndustryExposure { get; set; } = 2.00;        // 200%
		public double MaxNetIndustryExposure { get; set; } = 0.12;          // 12%
		public double MaxNetBetaIndustryExposure { get; set; } = 0.12;      // 12%

		// Sub-Industry Constraints
		public double MaxGrossSubIndustryExposure { get; set; } = 2.00;     // 200%
		public double MaxNetSubIndustryExposure { get; set; } = 0.12;       // 12%
		public double MaxNetBetaSubIndustryExposure { get; set; } = 0.12;   // 12%

		// Risk Constraints
		public double MaxPortfolioVaR95Percent { get; set; } = 0.01;
		public double MaxPortfolioCVaR95Percent { get; set; } = 0.015;
		public double MaxMarginalVaR95Percent { get; set; } = 0.15;
		public double MaxFactorRiskPercent { get; set; } = 0.18;
		public double MaxPredictedVolatilityPercent { get; set; } = 0.12;

		// Other
		public double TargetVolatility { get; set; } = 0.10;
		public double MinHedgeRatio { get; set; } = 0.7;
		public double MaxDailyTurnover { get; set; } = 0.30; // 30% daily turnover limit
	}

	// Classification Exposures
	public class ClassificationExposures
	{
		public Dictionary<string, double> SectorGrossExposures { get; set; }
		public Dictionary<string, double> SectorNetExposures { get; set; }
		public Dictionary<string, double> SectorNetBetaExposures { get; set; }

		public Dictionary<string, double> IndustryGrossExposures { get; set; }
		public Dictionary<string, double> IndustryNetExposures { get; set; }
		public Dictionary<string, double> IndustryNetBetaExposures { get; set; }

		public Dictionary<string, double> SubIndustryGrossExposures { get; set; }
		public Dictionary<string, double> SubIndustryNetExposures { get; set; }
		public Dictionary<string, double> SubIndustryNetBetaExposures { get; set; }

		public ClassificationExposures()
		{
			SectorGrossExposures = new Dictionary<string, double>();
			SectorNetExposures = new Dictionary<string, double>();
			SectorNetBetaExposures = new Dictionary<string, double>();

			IndustryGrossExposures = new Dictionary<string, double>();
			IndustryNetExposures = new Dictionary<string, double>();
			IndustryNetBetaExposures = new Dictionary<string, double>();

			SubIndustryGrossExposures = new Dictionary<string, double>();
			SubIndustryNetExposures = new Dictionary<string, double>();
			SubIndustryNetBetaExposures = new Dictionary<string, double>();
		}
	}

	// Enhanced Risk Metrics
	public class RiskMetrics
	{
		// Portfolio-level metrics
		public double PortfolioVaR95 { get; set; }
		public double PortfolioVaR99 { get; set; }
		public double PortfolioCVaR95 { get; set; }
		public double PortfolioCVaR99 { get; set; }
		public double MaxMarginalVaR95 { get; set; }
		public double PortfolioVolatility { get; set; }
		public double PredictedVolatility { get; set; }
		public double SharpeRatio { get; set; }
		public double MaxDrawdown { get; set; }
		public double NetBetaAdjustedExposure { get; set; }

		// Classification exposures
		public ClassificationExposures ClassificationExposures { get; set; }

		// Factor risks
		public Dictionary<RiskFactorType, double> FactorRisks { get; set; }
		public double MaxFactorRisk { get; set; }
		public RiskFactorType MaxFactorRiskType { get; set; }

		// Constraint checks
		public bool VaRConstraintMet { get; set; }
		public bool CVaRConstraintMet { get; set; }
		public bool MarginalVaRConstraintMet { get; set; }
		public bool GrossBookConstraintMet { get; set; }
		public bool NetExposureConstraintMet { get; set; }
		public bool FactorRiskConstraintMet { get; set; }
		public bool PredictedVolatilityConstraintMet { get; set; }

		// New classification constraint checks
		public bool SectorConstraintsMet { get; set; }
		public bool IndustryConstraintsMet { get; set; }
		public bool SubIndustryConstraintsMet { get; set; }

		public bool AllConstraintsMet => VaRConstraintMet && CVaRConstraintMet &&
			MarginalVaRConstraintMet && GrossBookConstraintMet && NetExposureConstraintMet &&
			FactorRiskConstraintMet && PredictedVolatilityConstraintMet &&
			SectorConstraintsMet && IndustryConstraintsMet && SubIndustryConstraintsMet;
	}

	// Portfolio with dynamic sizing capability
	public class Portfolio
	{
		public string PortfolioId { get; set; }
		public double TotalCapital { get; set; }
		public List<Position> Positions { get; private set; }
		public List<Position> TargetPositions { get; private set; } // User-specified targets
		public PortfolioConstraints Constraints { get; set; }
		public RiskMetrics CurrentRiskMetrics { get; private set; }
		public DateTime LastRebalanceTime { get; private set; }

		private readonly IRiskModel _riskModel;
		private readonly IPositionSizer _positionSizer;
		private readonly Dictionary<string, List<PriceBar>> _priceHistory;

		public Portfolio(string portfolioId, double totalCapital, PortfolioConstraints constraints = null,
			IRiskModel riskModel = null, IPositionSizer positionSizer = null)
		{
			PortfolioId = portfolioId;
			TotalCapital = totalCapital;
			Positions = new List<Position>();
			TargetPositions = new List<Position>();
			Constraints = constraints ?? new PortfolioConstraints();
			_riskModel = riskModel ?? new EnhancedMonteCarloRiskModel();
			_positionSizer = positionSizer ?? new DynamicPositionSizer();
			_priceHistory = new Dictionary<string, List<PriceBar>>();
		}

		public void SetTargetPositions(List<Position> targetPositions)
		{
			TargetPositions = targetPositions;
		}

		public async Task ProcessPriceBar(MarketDataSnapshot marketData)
		{
			// Update price history
			foreach (var kvp in marketData.PriceBars)
			{
				if (!_priceHistory.ContainsKey(kvp.Key))
					_priceHistory[kvp.Key] = new List<PriceBar>();

				// Keep only recent history (e.g., 50 days)
				if (_priceHistory[kvp.Key].Count > 50)
					_priceHistory[kvp.Key].RemoveAt(0);
			}

			// Update positions with latest market data
			foreach (var position in Positions)
			{
				if (marketData.PriceBars.ContainsKey(position.Symbol))
				{
					bool bp = false;
					if (marketData.PriceBars[position.Symbol].Close == 0)
					{
						bp = true;
					}
					position.UpdateMarketData(marketData.PriceBars[position.Symbol]);
				}
			}

			// Resize positions based on current market conditions
			await ResizePositions(marketData);

			// Update risk metrics
			UpdateRiskMetrics(marketData);

			LastRebalanceTime = marketData.Timestamp;
		}

		private async Task ResizePositions(MarketDataSnapshot marketData)
		{
			// Get sizing recommendations
			var sizingRecommendations = _positionSizer.CalculatePositionSizes(
				this, TargetPositions, marketData, _priceHistory);

			// Apply position changes
			foreach (var recommendation in sizingRecommendations)
			{
				var position = Positions.FirstOrDefault(p => p.Symbol == recommendation.Symbol);
				if (position == null && recommendation.TargetShares > 0)
				{
					// Add new position
					position = CreatePositionFromTarget(recommendation.Symbol);
					if (position != null)
					{
						position.UpdateMarketData(marketData.PriceBars[position.Symbol]);
						Positions.Add(position);
					}
				}

				if (position != null)
				{
					// Update position size
					double targetAmount = recommendation.TargetShares * recommendation.Price;
					position.UpdatePosition(targetAmount, TotalCapital);

					// Track trading costs
					if (recommendation.SharesChange != 0)
					{
						double tradingCost = Math.Abs(recommendation.SharesChange) * position.GetTradingCost() * position.Price;
						// Log or account for trading costs
					}
				}
			}

			// Remove positions that should be closed
			Positions.RemoveAll(p => p.Shares == 0);
		}

		private Position CreatePositionFromTarget(string symbol)
		{
			var target = TargetPositions.FirstOrDefault(t => t.Symbol == symbol);
			if (target == null) return null;

			return new Position
			{
				Symbol = target.Symbol,
				Type = target.Type,
				Sector = target.Sector,
				Industry = target.Industry,
				SubIndustry = target.SubIndustry,
				TargetWeight = target.TargetWeight,
				MaxPositionPercent = target.MaxPositionPercent,
				MinPositionPercent = target.MinPositionPercent,
				FactorExposures = target.FactorExposures,
				IsHedge = target.IsHedge,
				HedgeTarget = target.HedgeTarget,
				HedgeRatio = target.HedgeRatio
			};
		}

		private void UpdateRiskMetrics(MarketDataSnapshot marketData)
		{
			// Calculate correlation matrix from market data
			var correlationMatrix = CalculateCorrelationMatrix(marketData);

			// Update risk metrics
			CurrentRiskMetrics = _riskModel.CalculateRisk(Positions, correlationMatrix,
				Constraints, marketData.MarketVolatility);

			// Calculate classification exposures
			UpdateClassificationExposures();
		}

		//This process calculates a correlation matrix for a portfolio of positions and uses it to update risk metrics. It does this in two main steps:
		//Builds the correlation matrix using market data or fallback estimation.
		//Estimates correlation heuristically when market data is missing, using sector and industry classifications.
		//Iterates over all pairs of positions in the portfolio and estimates correlation to infer correlation based on classification of sector, industry and subindusty
		//It starts with a base correltion of 0.3 and adds to it based on how closely the two positions are related by classification. The final value is campled 
		//between -0.95 and +0.95.
		//The resulting correlation matrix is passed to the riskModel which uses it to compute portfolio VaR.  Assess diversification and concertration risk and adjusts for market volality. 

		private double[,] CalculateCorrelationMatrix(MarketDataSnapshot marketData)
		{
			int n = Positions.Count;
			var matrix = new double[n, n];

			for (int i = 0; i < n; i++)
			{
				for (int j = 0; j < n; j++)
				{
					if (i == j)
					{
						matrix[i, j] = 1.0;
					}
					else
					{
						var symbol1 = Positions[i].Symbol;
						var symbol2 = Positions[j].Symbol;

						if (marketData.Correlations.ContainsKey(symbol1) &&
							marketData.Correlations[symbol1].ContainsKey(symbol2))
						{
							matrix[i, j] = marketData.Correlations[symbol1][symbol2];
						}
						else
						{
							matrix[i, j] = EstimateCorrelation(Positions[i], Positions[j]);
						}
					}
				}
			}

			return matrix;
		}

		private double EstimateCorrelation(Position pos1, Position pos2)
		{
			double correlation = 0.3; // Base correlation

			if (pos1.Sector == pos2.Sector) correlation += 0.2;
			if (pos1.Industry == pos2.Industry) correlation += 0.1;
			if (pos1.SubIndustry == pos2.SubIndustry) correlation += 0.1;

			if (pos1.Type != pos2.Type) correlation *= -0.5;

			return Math.Max(-0.95, Math.Min(0.95, correlation));
		}

		private void UpdateClassificationExposures()
		{
			var exposures = new ClassificationExposures();

			// Calculate exposures by classification
			foreach (var position in Positions)
			{
				double grossExposure = Math.Abs(position.InvestmentAmount) / TotalCapital;
				double netExposure = position.Type == PositionType.Short ? -grossExposure : grossExposure;
				double betaExposure = netExposure * position.AdjustedPredictiveBeta;

				// Sector
				if (!string.IsNullOrEmpty(position.Sector))
				{
					if (!exposures.SectorGrossExposures.ContainsKey(position.Sector))
					{
						exposures.SectorGrossExposures[position.Sector] = 0;
						exposures.SectorNetExposures[position.Sector] = 0;
						exposures.SectorNetBetaExposures[position.Sector] = 0;
					}

					exposures.SectorGrossExposures[position.Sector] += grossExposure;
					exposures.SectorNetExposures[position.Sector] += netExposure;
					exposures.SectorNetBetaExposures[position.Sector] += betaExposure;
				}

				// Industry
				if (!string.IsNullOrEmpty(position.Industry))
				{
					if (!exposures.IndustryGrossExposures.ContainsKey(position.Industry))
					{
						exposures.IndustryGrossExposures[position.Industry] = 0;
						exposures.IndustryNetExposures[position.Industry] = 0;
						exposures.IndustryNetBetaExposures[position.Industry] = 0;
					}

					exposures.IndustryGrossExposures[position.Industry] += grossExposure;
					exposures.IndustryNetExposures[position.Industry] += netExposure;
					exposures.IndustryNetBetaExposures[position.Industry] += betaExposure;
				}

				// Sub-Industry
				if (!string.IsNullOrEmpty(position.SubIndustry))
				{
					if (!exposures.SubIndustryGrossExposures.ContainsKey(position.SubIndustry))
					{
						exposures.SubIndustryGrossExposures[position.SubIndustry] = 0;
						exposures.SubIndustryNetExposures[position.SubIndustry] = 0;
						exposures.SubIndustryNetBetaExposures[position.SubIndustry] = 0;
					}

					exposures.SubIndustryGrossExposures[position.SubIndustry] += grossExposure;
					exposures.SubIndustryNetExposures[position.SubIndustry] += netExposure;
					exposures.SubIndustryNetBetaExposures[position.SubIndustry] += betaExposure;
				}
			}

			CurrentRiskMetrics.ClassificationExposures = exposures;

			// Check constraints
			CheckClassificationConstraints();
		}

		private void CheckClassificationConstraints()
		{
			var exposures = CurrentRiskMetrics.ClassificationExposures;

			// Sector constraints
			CurrentRiskMetrics.SectorConstraintsMet = true;
			foreach (var sector in exposures.SectorGrossExposures)
			{
				if (sector.Value > Constraints.MaxGrossSectorExposure ||
					Math.Abs(exposures.SectorNetExposures[sector.Key]) > Constraints.MaxNetSectorExposure ||
					Math.Abs(exposures.SectorNetBetaExposures[sector.Key]) > Constraints.MaxNetBetaSectorExposure)
				{
					CurrentRiskMetrics.SectorConstraintsMet = false;
					break;
				}
			}

			// Industry constraints
			CurrentRiskMetrics.IndustryConstraintsMet = true;
			foreach (var industry in exposures.IndustryGrossExposures)
			{
				if (industry.Value > Constraints.MaxGrossIndustryExposure ||
					Math.Abs(exposures.IndustryNetExposures[industry.Key]) > Constraints.MaxNetIndustryExposure ||
					Math.Abs(exposures.IndustryNetBetaExposures[industry.Key]) > Constraints.MaxNetBetaIndustryExposure)
				{
					CurrentRiskMetrics.IndustryConstraintsMet = false;
					break;
				}
			}

			// Sub-Industry constraints
			CurrentRiskMetrics.SubIndustryConstraintsMet = true;
			foreach (var subIndustry in exposures.SubIndustryGrossExposures)
			{
				if (subIndustry.Value > Constraints.MaxGrossSubIndustryExposure ||
					Math.Abs(exposures.SubIndustryNetExposures[subIndustry.Key]) > Constraints.MaxNetSubIndustryExposure ||
					Math.Abs(exposures.SubIndustryNetBetaExposures[subIndustry.Key]) > Constraints.MaxNetBetaSubIndustryExposure)
				{
					CurrentRiskMetrics.SubIndustryConstraintsMet = false;
					break;
				}
			}
		}
	}

	// Interfaces
	public interface IRiskModel
	{
		RiskMetrics CalculateRisk(List<Position> positions, double[,] correlationMatrix,
			PortfolioConstraints constraints, double marketVolatility);
	}

	public interface IPositionSizer
	{
		List<SizingRecommendation> CalculatePositionSizes(Portfolio portfolio,
			List<Position> targetPositions, MarketDataSnapshot marketData,
			Dictionary<string, List<PriceBar>> priceHistory);
	}

	// Position Sizing Recommendation
	public class SizingRecommendation
	{
		public string Symbol { get; set; }
		public int CurrentShares { get; set; }
		public int TargetShares { get; set; }
		public int SharesChange { get; set; }
		public double Price { get; set; }
		public double EstimatedCost { get; set; }
		public string Reason { get; set; }
	}

	// Dynamic Position Sizer
	public class DynamicPositionSizer : IPositionSizer
	{
		public List<SizingRecommendation> CalculatePositionSizes(Portfolio portfolio,
			List<Position> targetPositions, MarketDataSnapshot marketData,
			Dictionary<string, List<PriceBar>> priceHistory)
		{
			var recommendations = new List<SizingRecommendation>();

			// Calculate volatility-adjusted sizes
			foreach (var target in targetPositions)
			{
				if (!marketData.PriceBars.ContainsKey(target.Symbol))
					continue;

				var priceBar = marketData.PriceBars[target.Symbol];
				var currentPosition = portfolio.Positions.FirstOrDefault(p => p.Symbol == target.Symbol);

				// Calculate target size based on multiple factors
				double targetSize = CalculateTargetSize(portfolio, target, priceBar,
					marketData.MarketVolatility, priceHistory);

				// Apply constraints
				targetSize = ApplyConstraints(portfolio, target, targetSize);

				// Calculate shares
				int targetShares = (int)(targetSize / priceBar.Close);
				int currentShares = currentPosition?.Shares ?? 0;
				int sharesChange = targetShares - currentShares;

				// Check if change is significant enough
				if (Math.Abs(sharesChange * priceBar.Close) > portfolio.TotalCapital * 0.001) // 0.1% threshold
				{
					recommendations.Add(new SizingRecommendation
					{
						Symbol = target.Symbol,
						CurrentShares = currentShares,
						TargetShares = targetShares,
						SharesChange = sharesChange,
						Price = priceBar.Close,
						EstimatedCost = Math.Abs(sharesChange) * priceBar.Close * 0.001, // 10 bps estimate
						Reason = DetermineSizingReason(target, currentShares, targetShares, priceBar)
					});
				}
			}

			return OptimizeTradeList(portfolio, recommendations, marketData);
		}

		private double CalculateTargetSize(Portfolio portfolio, Position target, PriceBar priceBar,
			double marketVolatility, Dictionary<string, List<PriceBar>> priceHistory)
		{
			// Base size from target weight
			double baseSize = target.TargetWeight * portfolio.TotalCapital;

			// Volatility adjustment
			double volAdjustment = CalculateVolatilityAdjustment(target.Symbol, priceBar,
				marketVolatility, priceHistory);

			// Liquidity adjustment
			double liquidityAdjustment = CalculateLiquidityAdjustment(priceBar, target.AverageVolume30D);

			// Risk parity adjustment
			double riskParityAdjustment = CalculateRiskParityAdjustment(portfolio, target);

			// Combine adjustments
			double adjustedSize = baseSize * volAdjustment * liquidityAdjustment * riskParityAdjustment;

			return adjustedSize;
		}

		private double CalculateVolatilityAdjustment(string symbol, PriceBar priceBar,
			double marketVolatility, Dictionary<string, List<PriceBar>> priceHistory)
		{
			if (!priceHistory.ContainsKey(symbol) || priceHistory[symbol].Count < 20)
				return 1.0;

			// Calculate realized volatility
			var returns = new List<double>();
			var bars = priceHistory[symbol];
			for (int i = 1; i < bars.Count; i++)
			{
				double dailyReturn = (bars[i].Close - bars[i - 1].Close) / bars[i - 1].Close;
				returns.Add(dailyReturn);
			}

			double realizedVol = CalculateStandardDeviation(returns);
			double targetVol = marketVolatility * 1.2; // Target slightly above market

			// Reduce size if volatility is high
			return Math.Min(1.5, Math.Max(0.5, targetVol / (realizedVol + 0.001)));
		}

		private double CalculateLiquidityAdjustment(PriceBar priceBar, double avgVolume30D)
		{
			if (avgVolume30D <= 0) return 0.5;

			// Adjust based on spread
			double spreadBps = priceBar.SpreadBps;
			double spreadAdjustment = 1 - (spreadBps / 100); // Reduce size for wide spreads

			// Adjust based on volume
			double volumeRatio = priceBar.Volume / avgVolume30D;
			double volumeAdjustment = Math.Min(1.2, Math.Max(0.8, volumeRatio));

			return spreadAdjustment * volumeAdjustment;
		}

		private double CalculateRiskParityAdjustment(Portfolio portfolio, Position target)
		{
			if (portfolio.CurrentRiskMetrics == null) return 1.0;

			// Calculate current risk contribution
			double currentRiskContribution = target.RiskContribution;
			double targetRiskContribution = 1.0 / portfolio.Positions.Count;

			// Adjust to equalize risk
			if (currentRiskContribution > 0)
			{
				return Math.Min(1.5, Math.Max(0.5, targetRiskContribution / currentRiskContribution));
			}

			return 1.0;
		}

		private double ApplyConstraints(Portfolio portfolio, Position target, double targetSize)
		{
			var constraints = portfolio.Constraints;

			// Position level constraints
			double maxSize = constraints.MaxPositionPercent * portfolio.TotalCapital;
			double minSize = constraints.MinPositionPercent * portfolio.TotalCapital;

			targetSize = Math.Max(minSize, Math.Min(maxSize, targetSize));

			// Check classification constraints
			double sectorExposure = GetCurrentSectorExposure(portfolio, target.Sector);
			double industryExposure = GetCurrentIndustryExposure(portfolio, target.Industry);
			double subIndustryExposure = GetCurrentSubIndustryExposure(portfolio, target.SubIndustry);

			// Reduce size if approaching limits
			if (sectorExposure + targetSize / portfolio.TotalCapital > constraints.MaxGrossSectorExposure * 0.95)
			{
				targetSize *= 0.8;
			}

			if (industryExposure + targetSize / portfolio.TotalCapital > constraints.MaxGrossIndustryExposure * 0.95)
			{
				targetSize *= 0.8;
			}

			if (subIndustryExposure + targetSize / portfolio.TotalCapital > constraints.MaxGrossSubIndustryExposure * 0.95)
			{
				targetSize *= 0.8;
			}

			return targetSize;
		}

		private double GetCurrentSectorExposure(Portfolio portfolio, string sector)
		{
			if (string.IsNullOrEmpty(sector) || portfolio.CurrentRiskMetrics?.ClassificationExposures == null)
				return 0;

			return portfolio.CurrentRiskMetrics.ClassificationExposures.SectorGrossExposures
				.GetValueOrDefault(sector, 0);
		}

		private double GetCurrentIndustryExposure(Portfolio portfolio, string industry)
		{
			if (string.IsNullOrEmpty(industry) || portfolio.CurrentRiskMetrics?.ClassificationExposures == null)
				return 0;

			return portfolio.CurrentRiskMetrics.ClassificationExposures.IndustryGrossExposures
				.GetValueOrDefault(industry, 0);
		}

		private double GetCurrentSubIndustryExposure(Portfolio portfolio, string subIndustry)
		{
			if (string.IsNullOrEmpty(subIndustry) || portfolio.CurrentRiskMetrics?.ClassificationExposures == null)
				return 0;

			return portfolio.CurrentRiskMetrics.ClassificationExposures.SubIndustryGrossExposures
				.GetValueOrDefault(subIndustry, 0);
		}

		private List<SizingRecommendation> OptimizeTradeList(Portfolio portfolio,
			List<SizingRecommendation> recommendations, MarketDataSnapshot marketData)
		{
			// Apply turnover constraint
			double totalTurnover = recommendations.Sum(r => Math.Abs(r.SharesChange * r.Price));
			double turnoverLimit = portfolio.TotalCapital * portfolio.Constraints.MaxDailyTurnover;

			if (totalTurnover > turnoverLimit)
			{
				// Scale down all trades proportionally
				double scaleFactor = turnoverLimit / totalTurnover;
				foreach (var rec in recommendations)
				{
					rec.SharesChange = (int)(rec.SharesChange * scaleFactor);
					rec.TargetShares = rec.CurrentShares + rec.SharesChange;
				}
			}

			// Sort by priority (largest risk reduction first)
			return recommendations.OrderByDescending(r => Math.Abs(r.SharesChange * r.Price)).ToList();
		}

		private string DetermineSizingReason(Position target, int currentShares, int targetShares, PriceBar priceBar)
		{
			if (currentShares == 0 && targetShares > 0)
				return "New position entry";
			else if (currentShares > 0 && targetShares == 0)
				return "Position exit";
			else if (targetShares > currentShares)
				return $"Increase position (vol: {priceBar.RealizedVolatility:P1})";
			else
				return $"Reduce position (vol: {priceBar.RealizedVolatility:P1})";
		}

		private double CalculateStandardDeviation(List<double> values)
		{
			if (values.Count == 0) return 0;

			double mean = values.Average();
			double sumSquares = values.Sum(v => (v - mean) * (v - mean));
			return (double)Math.Sqrt((double)(sumSquares / values.Count));
		}
	}

	// Enhanced Risk Model
	public class EnhancedMonteCarloRiskModel : IRiskModel
	{
		private readonly Random _random = new Random();
		private readonly int _simulations = 100;

		public RiskMetrics CalculateRisk(List<Position> positions, double[,] correlationMatrix,
			PortfolioConstraints constraints, double marketVolatility)
		{
			var metrics = new RiskMetrics();
			var portfolioReturns = new List<double>();

			// Run Monte Carlo simulation
			for (int sim = 0; sim < _simulations; sim++)
			{
				double portfolioReturn = SimulatePortfolioReturn(positions, correlationMatrix);
				portfolioReturns.Add(portfolioReturn);
			}

			portfolioReturns.Sort();

			// Calculate VaR and CVaR
			int var95Index = (int)(_simulations * 0.05);
			int var99Index = (int)(_simulations * 0.01);

			metrics.PortfolioVaR95 = Math.Abs(portfolioReturns[var95Index]);
			metrics.PortfolioVaR99 = Math.Abs(portfolioReturns[var99Index]);

			double cvar95Sum = 0;
			for (int i = 0; i <= var95Index; i++)
				cvar95Sum += Math.Abs(portfolioReturns[i]);
			metrics.PortfolioCVaR95 = cvar95Sum / (var95Index + 1);

			double cvar99Sum = 0;
			for (int i = 0; i <= var99Index; i++)
				cvar99Sum += Math.Abs(portfolioReturns[i]);
			metrics.PortfolioCVaR99 = cvar99Sum / (var99Index + 1);

			// Calculate other metrics
			metrics.PortfolioVolatility = CalculateVolatility(portfolioReturns);
			metrics.PredictedVolatility = PredictForwardVolatility(positions, correlationMatrix, marketVolatility);
			metrics.MaxDrawdown = CalculateMaxDrawdown(portfolioReturns);

			// Calculate position-specific metrics
			CalculatePositionMetrics(positions, metrics);

			// Calculate factor risks
			CalculateFactorRisks(positions, metrics);

			// Check constraints
			CheckConstraints(positions, metrics, constraints);

			return metrics;
		}

		private double SimulatePortfolioReturn(List<Position> positions, double[,] correlationMatrix)
		{
			int n = positions.Count;
			var correlatedReturns = GenerateCorrelatedReturns(n, correlationMatrix);

			double portfolioValue = positions.Sum(p => p.InvestmentAmount);
			double portfolioReturn = 0;

			for (int i = 0; i < n; i++)
			{
				double positionReturn = correlatedReturns[i] * positions[i].DailyVolatility;
				double dollarReturn = positionReturn * positions[i].InvestmentAmount;

				if (positions[i].Type == PositionType.Short)
					dollarReturn = -dollarReturn;

				portfolioReturn += dollarReturn;
			}

			return portfolioReturn / portfolioValue;
		}

		private double[] GenerateCorrelatedReturns(int n, double[,] correlationMatrix)
		{
			// Generate independent normal random variables
			var independent = new double[n];
			for (int i = 0; i < n; i++)
			{
				independent[i] = GenerateNormalRandom();
			}

			// Apply Cholesky decomposition
			var cholesky = CalculateCholesky(correlationMatrix);
			var correlated = new double[n];

			for (int i = 0; i < n; i++)
			{
				correlated[i] = 0;
				for (int j = 0; j <= i; j++)
				{
					correlated[i] += cholesky[i, j] * independent[j];
				}
			}

			return correlated;
		}

		private double[,] CalculateCholesky(double[,] correlation)
		{
			int n = correlation.GetLength(0);
			var cholesky = new double[n, n];

			for (int i = 0; i < n; i++)
			{
				for (int j = 0; j <= i; j++)
				{
					double sum = 0;
					for (int k = 0; k < j; k++)
					{
						sum += cholesky[i, k] * cholesky[j, k];
					}

					if (i == j)
					{
						cholesky[i, j] = (double)Math.Sqrt(Math.Max(0, (double)(correlation[i, j] - sum)));
					}
					else
					{
						if (cholesky[j, j] > 0)
							cholesky[i, j] = (correlation[i, j] - sum) / cholesky[j, j];
					}
				}
			}

			return cholesky;
		}

		private double GenerateNormalRandom()
		{
			double u1 = _random.NextDouble();
			double u2 = _random.NextDouble();
			double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
			return (double)randStdNormal;
		}

		private double CalculateVolatility(List<double> returns)
		{
			double mean = returns.Average();
			double sumSquares = returns.Sum(r => (r - mean) * (r - mean));
			return (double)Math.Sqrt((double)(sumSquares / returns.Count));
		}

		private double PredictForwardVolatility(List<Position> positions, double[,] correlationMatrix,
			double marketVolatility)
		{
			// Use GARCH-style prediction
			double currentVol = positions.WeightedAverage(p => p.DailyVolatility, p => p.CurrentWeight);
			double longTermVol = marketVolatility;
			double alpha = 0.1; // Weight on recent shocks
			double beta = 0.8;  // Weight on previous volatility
			double gamma = 0.1; // Weight on long-term average

			double predictedVol = alpha * currentVol + beta * currentVol + gamma * longTermVol;

			// Annualize
			return predictedVol * (double)Math.Sqrt(252);
		}

		private double CalculateMaxDrawdown(List<double> returns)
		{
			double peak = 0;
			double maxDrawdown = 0;
			double cumReturn = 0;

			foreach (var ret in returns)
			{
				cumReturn += ret;
				if (cumReturn > peak)
					peak = cumReturn;

				double drawdown = peak > 0 ? (peak - cumReturn) / (1 + peak) : 0;
				maxDrawdown = Math.Max(maxDrawdown, drawdown);
			}

			return maxDrawdown;
		}

		private void CalculatePositionMetrics(List<Position> positions, RiskMetrics metrics)
		{
			double totalValue = positions.Sum(p => p.InvestmentAmount);
			double maxMarginalVaR = 0;

			foreach (var position in positions)
			{
				// Marginal VaR
				double marginalVaR = position.DailyVolatility * position.CurrentWeight *
					Math.Abs(position.GetEffectiveBeta()) * 1.645;
				position.MarginalVaR = marginalVaR;
				maxMarginalVaR = Math.Max(maxMarginalVaR, marginalVaR);

				// Component VaR
				position.ComponentVaR = metrics.PortfolioVaR95 * position.CurrentWeight;

				// Risk contribution
				position.RiskContribution = marginalVaR * position.CurrentWeight;

				// Predicted volatility
				position.PredictedVolatility = position.DailyVolatility *
					Math.Abs(position.GetEffectiveBeta()) * (double)Math.Sqrt(252);
			}

			metrics.MaxMarginalVaR95 = maxMarginalVaR;
		}

		private void CalculateFactorRisks(List<Position> positions, RiskMetrics metrics)
		{
			metrics.FactorRisks = new Dictionary<RiskFactorType, double>();
			double totalValue = positions.Sum(p => p.InvestmentAmount);

			foreach (RiskFactorType factor in Enum.GetValues(typeof(RiskFactorType)))
			{
				double factorExposure = 0;

				foreach (var position in positions)
				{
					if (position.FactorExposures.Exposures.ContainsKey(factor))
					{
						double exposure = position.FactorExposures.Exposures[factor];
						double weight = position.InvestmentAmount / totalValue;
						int direction = position.Type == PositionType.Short ? -1 : 1;

						factorExposure += exposure * weight * direction;
					}
				}

				// Factor risk = exposure * factor volatility
				double factorVol = GetFactorVolatility(factor);
				metrics.FactorRisks[factor] = Math.Abs(factorExposure * factorVol);
			}

			// Find maximum factor risk
			var maxRisk = metrics.FactorRisks.OrderByDescending(f => f.Value).First();
			metrics.MaxFactorRisk = maxRisk.Value;
			metrics.MaxFactorRiskType = maxRisk.Key;
		}

		private double GetFactorVolatility(RiskFactorType factor)
		{
			// Annual volatilities
			var volatilities = new Dictionary<RiskFactorType, double>
			{
				{ RiskFactorType.Market, 0.16 },
				{ RiskFactorType.Size, 0.08 },
				{ RiskFactorType.Value, 0.10 },
				{ RiskFactorType.Momentum, 0.12 },
				{ RiskFactorType.Quality, 0.06 },
				{ RiskFactorType.Volatility, 0.20 },
				{ RiskFactorType.Sector, 0.14 },
				{ RiskFactorType.Industry, 0.16 },
				{ RiskFactorType.SubIndustry, 0.18 }
			};

			return volatilities.GetValueOrDefault(factor, 0.10) / (double)Math.Sqrt(252); // Daily
		}

		private void CheckConstraints(List<Position> positions, RiskMetrics metrics,
			PortfolioConstraints constraints)
		{
			double totalValue = positions.Sum(p => p.InvestmentAmount);
			double grossExposure = positions.Sum(p => Math.Abs(p.InvestmentAmount));

			// Standard constraints
			metrics.VaRConstraintMet = metrics.PortfolioVaR95 <= constraints.MaxPortfolioVaR95Percent;
			metrics.CVaRConstraintMet = metrics.PortfolioCVaR95 <= constraints.MaxPortfolioCVaR95Percent;
			metrics.MarginalVaRConstraintMet = metrics.MaxMarginalVaR95 <= constraints.MaxMarginalVaR95Percent;
			metrics.GrossBookConstraintMet = grossExposure / totalValue <= constraints.MaxGrossBookPercent;
			metrics.FactorRiskConstraintMet = metrics.MaxFactorRisk <= constraints.MaxFactorRiskPercent;
			metrics.PredictedVolatilityConstraintMet = metrics.PredictedVolatility <= constraints.MaxPredictedVolatilityPercent;

			// Calculate net beta-adjusted exposure
			double netBetaExposure = 0;
			foreach (var position in positions)
			{
				double betaAdjusted = position.InvestmentAmount * position.GetEffectiveBeta();
				if (position.Type == PositionType.Short)
					betaAdjusted = -betaAdjusted;
				netBetaExposure += betaAdjusted;
			}
			metrics.NetBetaAdjustedExposure = netBetaExposure / totalValue;
			metrics.NetExposureConstraintMet = Math.Abs(metrics.NetBetaAdjustedExposure) <= constraints.MaxNetExposurePercent;
		}
	}

	// PORTFOLIO MANAGEMENT - Main orchestrator
	public class PortfolioManager
	{
		private readonly Portfolio _portfolio;
		private readonly IDataProvider _dataProvider;
		private readonly IExecutionService _executionService;
		private readonly ILogger _logger;

		public PortfolioManager(Portfolio portfolio, IDataProvider dataProvider,
			IExecutionService executionService, ILogger logger)
		{
			_portfolio = portfolio;
			_dataProvider = dataProvider;
			_executionService = executionService;
			_logger = logger;
		}

		public async Task RunTradingSession(DateTime startTime, DateTime endTime)
		{
			_logger.Log($"Starting trading session from {startTime} to {endTime}");

			// Subscribe to market data
			await _dataProvider.Subscribe(_portfolio.TargetPositions.Select(p => p.Symbol).ToList());

			// Process price bars as they arrive
			await foreach (var marketData in _dataProvider.GetMarketDataStream(startTime, endTime))
			{
				try
				{
					await _portfolio.ProcessPriceBar(marketData);

					// Log portfolio state
					LogPortfolioState();

					// Execute any required trades
					// In a real system, this would send orders to the market
					await ExecuteTrades();
				}
				catch (Exception ex)
				{
					_logger.LogError($"Error processing price bar: {ex.Message}");
				}
			}

			_logger.Log("Trading session completed");
		}

		private void LogPortfolioState()
		{
			var metrics = _portfolio.CurrentRiskMetrics;
			if (metrics == null) return;

			_logger.Log($"Portfolio State at {_portfolio.LastRebalanceTime:HH:mm:ss}");
			_logger.Log($"  Gross Exposure: {_portfolio.Positions.Sum(p => Math.Abs(p.InvestmentAmount)) / _portfolio.TotalCapital:P1}");
			_logger.Log($"  Net Beta Exposure: {metrics.NetBetaAdjustedExposure:P2}");
			_logger.Log($"  VaR (95%): {metrics.PortfolioVaR95:P3}");
			_logger.Log($"  Predicted Vol: {metrics.PredictedVolatility:P2}");
			_logger.Log($"  All Constraints Met: {metrics.AllConstraintsMet}");

			if (!metrics.AllConstraintsMet)
			{
				_logger.LogWarning("Constraint violations detected:");
				if (!metrics.SectorConstraintsMet) _logger.LogWarning("  - Sector constraints violated");
				if (!metrics.IndustryConstraintsMet) _logger.LogWarning("  - Industry constraints violated");
				if (!metrics.SubIndustryConstraintsMet) _logger.LogWarning("  - Sub-industry constraints violated");
			}
		}

		private async Task ExecuteTrades()
		{
			// In a real implementation, this would:
			// 1. Generate orders from position differences
			// 2. Apply smart order routing
			// 3. Send orders to execution venues
			// 4. Monitor fills and update positions

			await Task.CompletedTask;
		}
	}

	// Supporting interfaces
	public interface IDataProvider
	{
		Task Subscribe(List<string> symbols);
		IAsyncEnumerable<MarketDataSnapshot> GetMarketDataStream(DateTime startTime, DateTime endTime);
	}

	public interface IExecutionService
	{
		Task<OrderResult> SendOrder(Order order);
		Task<List<Fill>> GetFills(string orderId);
	}

	public interface ILogger
	{
		void Log(string message);
		void LogWarning(string message);
		void LogError(string message);
	}

	// Helper classes
	public class Order
	{
		public string OrderId { get; set; }
		public string Symbol { get; set; }
		public int Quantity { get; set; }
		public OrderType Type { get; set; }
		public double? LimitPrice { get; set; }
	}

	public enum OrderType { Market, Limit, Stop }

	public class OrderResult
	{
		public string OrderId { get; set; }
		public bool Success { get; set; }
		public string Message { get; set; }
	}

	public class Fill
	{
		public string FillId { get; set; }
		public string OrderId { get; set; }
		public int Quantity { get; set; }
		public double Price { get; set; }
		public DateTime Timestamp { get; set; }
	}

	// Extension methods
	public static class Extensions
	{
		public static double WeightedAverage<T>(this IEnumerable<T> source,
			Func<T, double> valueSelector, Func<T, double> weightSelector)
		{
			double totalWeight = source.Sum(weightSelector);
			if (totalWeight == 0) return 0;

			return source.Sum(item => valueSelector(item) * weightSelector(item)) / totalWeight;
		}

		public static TValue GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dict,
			TKey key, TValue defaultValue = default)
		{
			return dict.TryGetValue(key, out TValue value) ? value : defaultValue;
		}
	}

	// Example usage
	class Example
	{
		static async Task Test()
		{
			// Create portfolio with constraints
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

			var portfolio = new Portfolio("MAIN_PORTFOLIO", 100_000_000, constraints);

			// Define target positions (these would come from your strategy)
			var targetPositions = new List<Position>
			{
                // Long positions
                new Position { Symbol = "AAPL", Type = PositionType.Long, TargetWeight = 0.05,
					Sector = "Technology", Industry = "Hardware", SubIndustry = "Consumer Electronics" },
				new Position { Symbol = "MSFT", Type = PositionType.Long, TargetWeight = 0.05,
					Sector = "Technology", Industry = "Software", SubIndustry = "Systems Software" },
				new Position { Symbol = "JPM", Type = PositionType.Long, TargetWeight = 0.04,
					Sector = "Financials", Industry = "Banks", SubIndustry = "Diversified Banks" },
                
                // Short positions
                new Position { Symbol = "TSLA", Type = PositionType.Short, TargetWeight = 0.03,
					Sector = "Consumer Discretionary", Industry = "Automobiles", SubIndustry = "Auto Manufacturers" },
				new Position { Symbol = "NFLX", Type = PositionType.Short, TargetWeight = 0.02,
					Sector = "Communication Services", Industry = "Media", SubIndustry = "Streaming Services" }
			};

			portfolio.SetTargetPositions(targetPositions);

			// Create PORTFOLIO MANAGEMENT with mock services
			var dataProvider = new MockDataProvider();
			var executionService = new MockExecutionService();
			var logger = new ConsoleLogger();

			var manager = new PortfolioManager(portfolio, dataProvider, executionService, logger);

			// Run trading session
			await manager.RunTradingSession(DateTime.Today.AddHours(9.5), DateTime.Today.AddHours(16));
		}
	}

	// Mock implementations for testing
	public class MockDataProvider : IDataProvider
	{
		private List<string> _symbols;
		private Random _random = new Random();

		public Task Subscribe(List<string> symbols)
		{
			_symbols = symbols;
			return Task.CompletedTask;
		}

		public async IAsyncEnumerable<MarketDataSnapshot> GetMarketDataStream(DateTime startTime, DateTime endTime)
		{
			var currentTime = startTime;
			while (currentTime <= endTime)
			{
				yield return GenerateSnapshot(currentTime);
				currentTime = currentTime.AddMinutes(1);
				await Task.Delay(100); // Simulate real-time
			}
		}

		private MarketDataSnapshot GenerateSnapshot(DateTime timestamp)
		{
			var snapshot = new MarketDataSnapshot { Timestamp = timestamp };

			foreach (var symbol in _symbols)
			{
				var basePrice = 100 + (double)_random.NextDouble() * 200;
				var spread = basePrice * 0.0001 * (1 + (double)_random.NextDouble());

				snapshot.PriceBars[symbol] = new PriceBar
				{
					Timestamp = timestamp,
					Open = basePrice,
					High = basePrice * 1.001,
					Low = basePrice * 0.999,
					Close = basePrice + (double)(_random.NextDouble() - 0.5) * 2,
					Volume = 1000000 + _random.Next(9000000),
					Bid = basePrice - spread / 2,
					Ask = basePrice + spread / 2,
					ATR = basePrice * 0.02,
					RealizedVolatility = 0.01 + (double)_random.NextDouble() * 0.03
				};
			}

			snapshot.MarketVolatility = 0.015 + (double)_random.NextDouble() * 0.005;
			return snapshot;
		}
	}

	public class MockExecutionService : IExecutionService
	{
		public Task<OrderResult> SendOrder(Order order)
		{
			return Task.FromResult(new OrderResult
			{
				OrderId = order.OrderId,
				Success = true,
				Message = "Order sent successfully"
			});
		}

		public Task<List<Fill>> GetFills(string orderId)
		{
			return Task.FromResult(new List<Fill>());
		}
	}

	public class ConsoleLogger : ILogger
	{
		public void Log(string message)
		{
			Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] INFO: {message}");
		}

		public void LogWarning(string message)
		{
			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] WARN: {message}");
			Console.ResetColor();
		}

		public void LogError(string message)
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ERROR: {message}");
			Console.ResetColor();
		}
	}

	// Report Generator
	public class PortfolioReporter
	{
		public static void GenerateDetailedReport(Portfolio portfolio, string outputPath = null)
		{
			var sb = new StringBuilder();
			var metrics = portfolio.CurrentRiskMetrics;
			var timestamp = portfolio.LastRebalanceTime;

			sb.AppendLine(new string('=', 120));
			sb.AppendLine("PORTFOLIO RISK OPTIMIZATION REPORT - DYNAMIC SIZING");
			sb.AppendLine(new string('=', 120));
			sb.AppendLine($"Portfolio ID: {portfolio.PortfolioId}");
			sb.AppendLine($"Report Time: {timestamp:yyyy-MM-dd HH:mm:ss}");
			sb.AppendLine($"Total Capital: ${portfolio.TotalCapital:N0}");
			sb.AppendLine();

			// Position details
			sb.AppendLine("=== POSITION DETAILS ===");
			sb.AppendLine();

			var positions = portfolio.Positions.OrderBy(p => p.Type).ThenBy(p => p.Symbol);

			sb.AppendLine($"{"Symbol",-10} {"Type",-6} {"Shares",10} {"Price",10} {"Value",15} {"Weight",8} {"Beta",6} {"Vol",6} {"Sector",-20}");
			sb.AppendLine(new string('-', 100));

			foreach (var pos in positions)
			{
				sb.AppendLine($"{pos.Symbol,-10} {pos.Type,-6} {pos.Shares,10:N0} ${pos.Price,9:F2} " +
					$"${pos.InvestmentAmount,14:N0} {pos.CurrentWeight,7:P1} {pos.AdjustedPredictiveBeta,6:F2} " +
					$"{pos.DailyVolatility,6:P1} {pos.Sector,-20}");
			}

			sb.AppendLine();
			sb.AppendLine("=== EXPOSURE SUMMARY ===");
			sb.AppendLine();

			double grossExposure = positions.Sum(p => Math.Abs(p.InvestmentAmount));
			double longExposure = positions.Where(p => p.Type == PositionType.Long).Sum(p => p.InvestmentAmount);
			double shortExposure = positions.Where(p => p.Type == PositionType.Short).Sum(p => p.InvestmentAmount);
			double netExposure = longExposure - shortExposure;

			sb.AppendLine($"Gross Exposure: ${grossExposure:N0} ({grossExposure / portfolio.TotalCapital:P1})");
			sb.AppendLine($"Long Exposure: ${longExposure:N0} ({longExposure / portfolio.TotalCapital:P1})");
			sb.AppendLine($"Short Exposure: ${shortExposure:N0} ({shortExposure / portfolio.TotalCapital:P1})");
			sb.AppendLine($"Net Exposure: ${netExposure:N0} ({netExposure / portfolio.TotalCapital:P1})");
			sb.AppendLine($"Net Beta-Adjusted Exposure: {metrics?.NetBetaAdjustedExposure:P2}");

			// Classification exposures
			if (metrics?.ClassificationExposures != null)
			{
				sb.AppendLine();
				sb.AppendLine("=== SECTOR EXPOSURES ===");
				sb.AppendLine();
				sb.AppendLine($"{"Sector",-30} {"Gross",10} {"Net",10} {"Net Beta",10}");
				sb.AppendLine(new string('-', 65));

				foreach (var sector in metrics.ClassificationExposures.SectorGrossExposures.Keys.OrderBy(k => k))
				{
					var gross = metrics.ClassificationExposures.SectorGrossExposures[sector];
					var net = metrics.ClassificationExposures.SectorNetExposures.GetValueOrDefault(sector);
					var netBeta = metrics.ClassificationExposures.SectorNetBetaExposures.GetValueOrDefault(sector);

					sb.AppendLine($"{sector,-30} {gross,9:P1} {net,9:P1} {netBeta,9:P1}");
				}

				sb.AppendLine();
				sb.AppendLine("=== INDUSTRY EXPOSURES ===");
				sb.AppendLine();
				sb.AppendLine($"{"Industry",-30} {"Gross",10} {"Net",10} {"Net Beta",10}");
				sb.AppendLine(new string('-', 65));

				foreach (var industry in metrics.ClassificationExposures.IndustryGrossExposures.Keys.OrderBy(k => k))
				{
					var gross = metrics.ClassificationExposures.IndustryGrossExposures[industry];
					var net = metrics.ClassificationExposures.IndustryNetExposures.GetValueOrDefault(industry);
					var netBeta = metrics.ClassificationExposures.IndustryNetBetaExposures.GetValueOrDefault(industry);

					sb.AppendLine($"{industry,-30} {gross,9:P1} {net,9:P1} {netBeta,9:P1}");
				}
			}

			// Risk metrics
			if (metrics != null)
			{
				sb.AppendLine();
				sb.AppendLine("=== RISK METRICS ===");
				sb.AppendLine();
				sb.AppendLine($"Portfolio Volatility (Daily): {metrics.PortfolioVolatility:P2}");
				sb.AppendLine($"Portfolio Volatility (Annual): {metrics.PortfolioVolatility * (double)Math.Sqrt(252):P2}");
				sb.AppendLine($"Predicted Volatility: {metrics.PredictedVolatility:P2}");
				sb.AppendLine($"VaR (95%): {metrics.PortfolioVaR95:P3}");
				sb.AppendLine($"VaR (99%): {metrics.PortfolioVaR99:P3}");
				sb.AppendLine($"CVaR (95%): {metrics.PortfolioCVaR95:P3}");
				sb.AppendLine($"CVaR (99%): {metrics.PortfolioCVaR99:P3}");
				sb.AppendLine($"Max Drawdown: {metrics.MaxDrawdown:P2}");
				sb.AppendLine($"Sharpe Ratio: {metrics.SharpeRatio:F2}");

				sb.AppendLine();
				sb.AppendLine("=== CONSTRAINT STATUS ===");
				sb.AppendLine();
				sb.AppendLine($"{"Constraint",-40} {"Current",12} {"Limit",12} {"Status",8}");
				sb.AppendLine(new string('-', 75));

				// Position and portfolio constraints
				sb.AppendLine($"{"Gross Book Size",-40} {grossExposure / portfolio.TotalCapital,11:P1} " +
					$"{portfolio.Constraints.MaxGrossBookPercent,11:P0} {(metrics.GrossBookConstraintMet ? "PASS" : "FAIL"),8}");

				sb.AppendLine($"{"Net Exposure (Beta-Adjusted)",-40} {metrics.NetBetaAdjustedExposure,11:P2} " +
					$"±{portfolio.Constraints.MaxNetExposurePercent,10:P0} {(metrics.NetExposureConstraintMet ? "PASS" : "FAIL"),8}");

				// Risk constraints
				sb.AppendLine($"{"VaR (95%)",-40} {metrics.PortfolioVaR95,11:P3} " +
					$"{portfolio.Constraints.MaxPortfolioVaR95Percent,11:P2} {(metrics.VaRConstraintMet ? "PASS" : "FAIL"),8}");

				sb.AppendLine($"{"CVaR (95%)",-40} {metrics.PortfolioCVaR95,11:P3} " +
					$"{portfolio.Constraints.MaxPortfolioCVaR95Percent,11:P2} {(metrics.CVaRConstraintMet ? "PASS" : "FAIL"),8}");

				sb.AppendLine($"{"Predicted Volatility",-40} {metrics.PredictedVolatility,11:P2} " +
					$"{portfolio.Constraints.MaxPredictedVolatilityPercent,11:P0} {(metrics.PredictedVolatilityConstraintMet ? "PASS" : "FAIL"),8}");

				sb.AppendLine($"{"Max Factor Risk",-40} {metrics.MaxFactorRisk,11:P2} " +
					$"{portfolio.Constraints.MaxFactorRiskPercent,11:P0} {(metrics.FactorRiskConstraintMet ? "PASS" : "FAIL"),8}");

				// Classification constraints
				sb.AppendLine($"{"Sector Constraints",-40} {"",-11} {"",-11} {(metrics.SectorConstraintsMet ? "PASS" : "FAIL"),8}");
				sb.AppendLine($"{"Industry Constraints",-40} {"",-11} {"",-11} {(metrics.IndustryConstraintsMet ? "PASS" : "FAIL"),8}");
				sb.AppendLine($"{"Sub-Industry Constraints",-40} {"",-11} {"",-11} {(metrics.SubIndustryConstraintsMet ? "PASS" : "FAIL"),8}");

				sb.AppendLine();
				sb.AppendLine($"ALL CONSTRAINTS MET: {(metrics.AllConstraintsMet ? "YES" : "NO")}");

				// Factor risks
				if (metrics.FactorRisks != null && metrics.FactorRisks.Any())
				{
					sb.AppendLine();
					sb.AppendLine("=== FACTOR RISK ANALYSIS ===");
					sb.AppendLine();
					sb.AppendLine($"{"Factor",-20} {"Risk",10}");
					sb.AppendLine(new string('-', 32));

					foreach (var factor in metrics.FactorRisks.OrderByDescending(f => f.Value))
					{
						sb.AppendLine($"{factor.Key,-20} {factor.Value,9:P2}");
					}
				}
			}

			// Output
			if (string.IsNullOrEmpty(outputPath))
			{
				Console.WriteLine(sb.ToString());
			}
			else
			{
				System.IO.File.WriteAllText(outputPath, sb.ToString());
			}
		}
	}

	public class DirectionalBetaHedgeResult
	{
		public double ExpectedMarketReturn { get; set; }
		public double TargetBeta { get; set; }
		public double HedgeNotional { get; set; }
		public double HedgeUnits { get; set; }
		public string MarketDirection { get; set; }
		public string HedgeFocus { get; set; }
		public string Comment { get; set; }
	}

	public static class ProtectiveBetaHedgeCalculator
	{
		public static DirectionalBetaHedgeResult CalculateDirectionalHedge(
			double currentMarketPrice,
			double targetMarketPrice,
			int daysToTarget,
			double netLongBeta,     // e.g. 0.8  BetaPlus RK540
			double netShortBeta,    // e.g. -0.3 BetaMinus RK541
			double portfolioValue,
			double hedgeInstrumentPrice,
			double hedgeInstrumentBeta = 1.0,
			double contractMultiplier = 1.0,
			double aggressiveness = 12.0,
			double maxBetaHedge = 1.5
		)
		{
			// Step 1: Expected annualized return
			double expectedReturn = Math.Pow(targetMarketPrice / currentMarketPrice, 252.0 / daysToTarget) - 1;

			string direction = expectedReturn >= 0 ? "Bullish" : "Bearish";  // add slightly bullish and slightly bearish
			string hedgeFocus = expectedReturn >= 0 ? "Protect shorts" : "Protect longs";

			// Step 2: Compute directional hedge target
			double directionBeta = expectedReturn >= 0 ? Math.Abs(netShortBeta) : Math.Abs(netLongBeta);
			double scaledTargetBeta = Math.Tanh(aggressiveness * Math.Abs(expectedReturn)) * directionBeta;
			double hedgeTargetBeta = expectedReturn >= 0 ? -scaledTargetBeta : scaledTargetBeta;

			// Step 3: Compute hedge
			double hedgeNotional = hedgeTargetBeta * portfolioValue / hedgeInstrumentBeta;
			double hedgeUnits = hedgeNotional / (hedgeInstrumentPrice * contractMultiplier);

			return new DirectionalBetaHedgeResult
			{
				ExpectedMarketReturn = expectedReturn,
				TargetBeta = hedgeTargetBeta,
				HedgeNotional = hedgeNotional,
				HedgeUnits = hedgeUnits,
				MarketDirection = direction,
				HedgeFocus = hedgeFocus,
				Comment = $"Direction: {direction} | Focus: {hedgeFocus} | Annualized Market Return: {expectedReturn:P2} | Hedge Target Beta: {hedgeTargetBeta:F3}"
			};
		}
	}
}
