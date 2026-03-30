using System;
using System.Collections.Generic;

namespace ATMML.Monitoring
{
	// ── Connectivity ─────────────────────────────────────────────────────────────

	public interface IFlexOneSessionState
	{
		bool IsConnected { get; }
		DateTime LastHeartbeat { get; }
	}

	public interface IBloombergSessionState
	{
		bool IsConnected { get; }
		DateTime LastTickTimestamp { get; }
	}

	// ── Portfolio snapshot ────────────────────────────────────────────────────────

	public interface IPositionSnapshot
	{
		string Ticker { get; }
		double WeightOfNAV { get; }   // positive = long, negative = short
		double VaR95OfNAV { get; }   // always positive
		double BorrowCostBps { get; }   // shorts only, annualised bps
	}

	public interface IGroupExposure
	{
		string Name { get; }   // sector / industry / sub-industry name
		double GrossOfNAV { get; }   // always positive
		double NetOfNAV { get; }   // signed
	}

	public interface IPortfolioSnapshot
	{
		IReadOnlyList<IPositionSnapshot> Positions { get; }
		IReadOnlyList<IGroupExposure> SectorExposures { get; }
		IReadOnlyList<IGroupExposure> IndustryExposures { get; }
		IReadOnlyList<IGroupExposure> SubIndustryExposures { get; }

		double GrossExposureOfNAV { get; }
		double NetExposureOfNAV { get; }   // signed
		double IntradayDrawdownOfNAV { get; }   // negative value
	}

	// ── Risk engine snapshot ──────────────────────────────────────────────────────

	public interface IRiskEngineSnapshot
	{
		double WeightedNetBeta { get; }
		double PredictedAnnualisedVol { get; }
		double PortfolioVaR95OfNAV { get; }
	}
}
