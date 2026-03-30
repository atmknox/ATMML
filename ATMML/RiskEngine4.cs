using System.Collections.Generic;
using System;
using System.Linq;

namespace RiskEngine4
{

	public enum PositionType
	{
		Long,
		Short
	}

	public class Stock
	{
		public string Symbol { get; set; }
		public double Price { get; set; }
		public double ATR { get; set; }
		public PositionType Type { get; set; }
		public double InvestmentAmount { get; set; }
		public int Shares { get; set; }
		public double RiskAmount { get; set; }

		public Stock(string symbol, double price, double atr, PositionType type)
		{
			Symbol = symbol;
			Price = price;
			ATR = atr;
			Type = type;
		}

		public void CalculatePosition(double investmentAmount)
		{
			InvestmentAmount = investmentAmount;
			Shares = (int)(investmentAmount / Price);
			RiskAmount = Shares * ATR;
		}

		public override string ToString()
		{
			return $"{Symbol} ({Type}): {Shares:N0} shares @ ${Price:F2}, Investment: ${InvestmentAmount:N0}, Risk: ${RiskAmount:N0}";
		}
	}

	public class PortfolioCalculator
	{
		public double TotalCapital { get; set; }
		public double MaxNetExposurePercent { get; set; }
		public double MaxPositionPercent { get; set; }
		public List<Stock> Stocks { get; set; }

		public PortfolioCalculator(double totalCapital, double maxNetExposurePercent = 0.10, double maxPositionPercent = 0.10)
		{
			TotalCapital = totalCapital;
			MaxNetExposurePercent = maxNetExposurePercent;
			MaxPositionPercent = maxPositionPercent;
			Stocks = new List<Stock>();
		}

		public void AddStock(Stock stock)
		{
			Stocks.Add(stock);
		}

		public void CalculatePositions()
		{
			var longStocks = Stocks.Where(s => s.Type == PositionType.Long).ToList();
			var shortStocks = Stocks.Where(s => s.Type == PositionType.Short).ToList();

			// Calculate required exposures to maintain net exposure constraint
			double maxNetExposure = TotalCapital * MaxNetExposurePercent;
			double maxPositionSize = TotalCapital * MaxPositionPercent;

			// L + S = TotalCapital (gross constraint)
			// L - S = maxNetExposure (net constraint)
			// Solving: L = (TotalCapital + maxNetExposure) / 2, S = (TotalCapital - maxNetExposure) / 2
			double totalLongExposure = (TotalCapital + maxNetExposure) / 2;
			double totalShortExposure = (TotalCapital - maxNetExposure) / 2;

			// Calculate per-stock investment amounts
			if (longStocks.Any())
			{
				double longPerStock = totalLongExposure / longStocks.Count;
				foreach (var stock in longStocks)
				{
					// Ensure position doesn't exceed max position size
					double investmentAmount = Math.Min(longPerStock, maxPositionSize);
					stock.CalculatePosition(investmentAmount);
				}
			}

			if (shortStocks.Any())
			{
				double shortPerStock = totalShortExposure / shortStocks.Count;
				foreach (var stock in shortStocks)
				{
					// Ensure position doesn't exceed max position size
					double investmentAmount = Math.Min(shortPerStock, maxPositionSize);
					stock.CalculatePosition(investmentAmount);
				}
			}
		}

		public PortfolioSummary GetPortfolioSummary()
		{
			var longStocks = Stocks.Where(s => s.Type == PositionType.Long).ToList();
			var shortStocks = Stocks.Where(s => s.Type == PositionType.Short).ToList();

			double totalLongExposure = longStocks.Sum(s => s.InvestmentAmount);
			double totalShortExposure = shortStocks.Sum(s => s.InvestmentAmount);
			double grossExposure = totalLongExposure + totalShortExposure;
			double netExposure = totalLongExposure - totalShortExposure;
			double totalRisk = Stocks.Sum(s => s.RiskAmount);

			return new PortfolioSummary
			{
				TotalLongExposure = totalLongExposure,
				TotalShortExposure = totalShortExposure,
				GrossExposure = grossExposure,
				NetExposure = netExposure,
				TotalRisk = totalRisk,
				NetExposurePercent = netExposure / TotalCapital * 100,
				TotalStocks = Stocks.Count,
				LongStocks = longStocks.Count,
				ShortStocks = shortStocks.Count
			};
		}

		public void PrintPortfolioDetails()
		{
			Console.WriteLine("=== PORTFOLIO POSITION DETAILS ===\n");

			var longStocks = Stocks.Where(s => s.Type == PositionType.Long).OrderBy(s => s.Symbol).ToList();
			var shortStocks = Stocks.Where(s => s.Type == PositionType.Short).OrderBy(s => s.Symbol).ToList();

			if (longStocks.Any())
			{
				Console.WriteLine("LONG POSITIONS:");
				foreach (var stock in longStocks)
				{
					Console.WriteLine($"  {stock}");
				}
				Console.WriteLine();
			}

			if (shortStocks.Any())
			{
				Console.WriteLine("SHORT POSITIONS:");
				foreach (var stock in shortStocks)
				{
					Console.WriteLine($"  {stock}");
				}
				Console.WriteLine();
			}

			var summary = GetPortfolioSummary();
			Console.WriteLine("=== PORTFOLIO SUMMARY ===");
			Console.WriteLine($"Total Capital: ${TotalCapital:N0}");
			Console.WriteLine($"Total Stocks: {summary.TotalStocks} ({summary.LongStocks} long, {summary.ShortStocks} short)");
			Console.WriteLine($"Gross Exposure: ${summary.GrossExposure:N0}");
			Console.WriteLine($"Long Exposure: ${summary.TotalLongExposure:N0}");
			Console.WriteLine($"Short Exposure: ${summary.TotalShortExposure:N0}");
			Console.WriteLine($"Net Exposure: ${summary.NetExposure:N0} ({summary.NetExposurePercent:F1}%)");
			Console.WriteLine($"Total Portfolio Risk: ${summary.TotalRisk:N0}");
			Console.WriteLine($"Max Position Size Limit: ${TotalCapital * MaxPositionPercent:N0}");
			Console.WriteLine($"Max Net Exposure Limit: ${TotalCapital * MaxNetExposurePercent:N0}");
		}
	}

	public class PortfolioSummary
	{
		public double TotalLongExposure { get; set; }
		public double TotalShortExposure { get; set; }
		public double GrossExposure { get; set; }
		public double NetExposure { get; set; }
		public double TotalRisk { get; set; }
		public double NetExposurePercent { get; set; }
		public int TotalStocks { get; set; }
		public int LongStocks { get; set; }
		public int ShortStocks { get; set; }
	}

	class Example
	{
		static void Test()
		{
			// Create portfolio calculator with $100M capital, 10% max net exposure, 10% max position size
			var portfolio = new PortfolioCalculator(100_000_000, 0.10, 0.10);

			// Add 15 long positions (all $20 stocks with $4 ATR for simplicity)
			for (int i = 1; i <= 15; i++)
			{
				portfolio.AddStock(new Stock($"LONG{i:D2}", 20, 4, PositionType.Long));
			}

			// Add 5 short positions
			for (int i = 1; i <= 5; i++)
			{
				portfolio.AddStock(new Stock($"SHORT{i}", 20, 4, PositionType.Short));
			}

			// Calculate positions
			portfolio.CalculatePositions();

			// Display results
			portfolio.PrintPortfolioDetails();
		}
	}
}