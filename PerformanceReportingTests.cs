// TotalReturnReporting_SmokeTest.cs
//
// Comprehensive smoke tests for the fixed TotalReturnReporting engine
// Tests all critical fixes and edge cases
//

using System;
using System.Collections.Generic;
using System.Linq;
using PerformanceReporting;
using System.Diagnostics;

namespace PerformanceReporting.Tests
{
	public class SmokeTests
	{
		private const decimal TOLERANCE = 0.0001m; // 1 basis point tolerance for decimal comparisons

		public static void Test()
		{
			Trace.WriteLine("=".PadRight(80, '='));
			Trace.WriteLine("TOTAL RETURN REPORTING ENGINE - SMOKE TESTS");
			Trace.WriteLine("=".PadRight(80, '='));
			Trace.WriteLine("");

			int passed = 0;
			int failed = 0;

			// Run all tests
			RunTest("Test 1: Basic Single Period (No Flows)", Test1_BasicSinglePeriod, ref passed, ref failed);
			RunTest("Test 2: Average AUM Fix Verification", Test2_AverageAumFix, ref passed, ref failed);
			RunTest("Test 3: High Water Mark Fix Verification", Test3_HighWaterMarkFix, ref passed, ref failed);
			RunTest("Test 4: Modified Dietz Flow Weighting", Test4_ModifiedDietzWeighting, ref passed, ref failed);
			RunTest("Test 5: Multi-Period Time-Weighted Returns", Test5_MultiPeriodTwr, ref passed, ref failed);
			RunTest("Test 6: NAV Continuity Check (Should Pass)", Test6_NavContinuityPass, ref passed, ref failed);
			RunTest("Test 7: NAV Continuity Check (Should Fail)", Test7_NavContinuityFail, ref passed, ref failed);
			RunTest("Test 8: Performance Fee Non-HWM", Test8_PerformanceFeeNonHwm, ref passed, ref failed);
			RunTest("Test 9: Performance Fee with HWM", Test9_PerformanceFeeWithHwm, ref passed, ref failed);
			RunTest("Test 10: Operating Expenses In/Out NAV", Test10_OperatingExpenses, ref passed, ref failed);
			RunTest("Test 11: After-Tax Returns", Test11_AfterTaxReturns, ref passed, ref failed);
			RunTest("Test 12: Large Subscription Impact", Test12_LargeSubscription, ref passed, ref failed);

			// Summary
			Trace.WriteLine("");
			Trace.WriteLine("=".PadRight(80, '='));
			Trace.WriteLine($"SUMMARY: {passed} passed, {failed} failed out of {passed + failed} tests");
			Trace.WriteLine("=".PadRight(80, '='));

			if (failed > 0)
			{
				Trace.WriteLine("\n⚠️  SOME TESTS FAILED - Review output above");
				//Environment.Exit(1);
			}
			else
			{
				Trace.WriteLine("\n✓ ALL TESTS PASSED");
				//Environment.Exit(0);
			}
		}

		private static void RunTest(string name, Action test, ref int passed, ref int failed)
		{
			Trace.WriteLine($"\n{name}");
			Trace.WriteLine("-".PadRight(80, '-'));
			try
			{
				test();
				Trace.WriteLine("✓ PASSED");
				passed++;
			}
			catch (Exception ex)
			{
				Trace.WriteLine($"✗ FAILED: {ex.Message}");
				Trace.WriteLine($"  {ex.StackTrace?.Split('\n')[0]}");
				failed++;
			}
		}

		// ============================================================================
		// TEST 1: Basic single period with no flows
		// ============================================================================
		private static void Test1_BasicSinglePeriod()
		{
			var config = new TotalReturnConfig
			{
				ManagementFeeRateAnnual = 0.01m, // 1%
				PerformanceFeeRate = 0.20m,      // 20%
				CustodyFeeRateAnnual = 0.0005m,  // 0.05%
				DayCountBasis = 365,
				UseHighWaterMark = false
			};

			var engine = new TotalReturnEngine(config);

			var result = engine.ComputePeriodReturn(
				startDate: new DateTime(2024, 1, 1),
				endDate: new DateTime(2024, 2, 1), // 31 days
				navStart: 100_000_000m,
				navEndBeforeFees: 105_000_000m,
				cashFlows: new PeriodCashFlows() // No flows
			);

			Trace.WriteLine($"Start NAV: ${result.StartNav:N0}");
			Trace.WriteLine($"End NAV (before fees): ${result.EndNavBeforeFees:N0}");
			Trace.WriteLine($"Gross Return: {result.GrossOfFeesReturn:P4}");
			Trace.WriteLine($"Management Fee: ${result.ManagementFee:N2}");
			Trace.WriteLine($"Performance Fee: ${result.PerformanceFee:N2}");
			Trace.WriteLine($"Net Return: {result.NetOfFeesReturn:P4}");

			// Verify calculations
			AssertApproxEqual(5_000_000m, result.EndNavBeforeFees - result.StartNav, "Investment gain");
			AssertApproxEqual(0.05m, (decimal)result.GrossOfFeesReturn, "Gross return should be 5%");

			// Management fee: (100M + 105M) / 2 * 1% * 31/365 = 87,328.77
			decimal expectedMgmtFee = 102_500_000m * 0.01m * (31m / 365m);
			AssertApproxEqual(expectedMgmtFee, result.ManagementFee, "Management fee");

			// Verify performance fee calculated correctly on profit after mgmt fee
			Assert(result.PerformanceFee > 0, "Should have performance fee");
		}

		// ============================================================================
		// TEST 2: Verify Average AUM fix (Critical Error #1)
		// ============================================================================
		private static void Test2_AverageAumFix()
		{
			var config = new TotalReturnConfig
			{
				ManagementFeeRateAnnual = 0.01m, // 1%
				CustodyFeeRateAnnual = 0.0005m,
				DayCountBasis = 365
			};

			var engine = new TotalReturnEngine(config);

			var result = engine.ComputePeriodReturn(
				startDate: new DateTime(2024, 1, 1),
				endDate: new DateTime(2024, 1, 31), // 30 days
				navStart: 100_000_000m,
				navEndBeforeFees: 110_000_000m,
				cashFlows: new PeriodCashFlows
				{
					Subscriptions = 10_000_000m // $10M subscription
				}
			);

			Trace.WriteLine($"Start NAV: ${result.StartNav:N0}");
			Trace.WriteLine($"End NAV: ${result.EndNavBeforeFees:N0}");
			Trace.WriteLine($"Subscriptions: ${result.NetExternalFlow:N0}");
			Trace.WriteLine($"Average AUM: ${result.AverageAum:N0}");
			Trace.WriteLine($"Management Fee: ${result.ManagementFee:N2}");

			// CRITICAL: Average AUM should be (100M + 110M) / 2 = 105M
			// NOT (100M + 110M - 10M) / 2 = 100M (old bug)
			decimal expectedAvgAum = 105_000_000m;
			AssertApproxEqual(expectedAvgAum, result.AverageAum, "Average AUM calculation");

			// Expected management fee: 105M * 1% * 30/365
			decimal expectedMgmtFee = expectedAvgAum * 0.01m * (30m / 365m);
			AssertApproxEqual(expectedMgmtFee, result.ManagementFee, "Management fee with subscription");

			Trace.WriteLine($"✓ Average AUM correctly calculated as simple average");
			Trace.WriteLine($"✓ Management fee correctly charged on {expectedAvgAum:N0}");
		}

		// ============================================================================
		// TEST 3: Verify High Water Mark fix (Critical Error #2)
		// ============================================================================
		private static void Test3_HighWaterMarkFix()
		{
			var config = new TotalReturnConfig
			{
				ManagementFeeRateAnnual = 0.01m,
				PerformanceFeeRate = 0.20m,
				CustodyFeeRateAnnual = 0m,
				DayCountBasis = 365,
				UseHighWaterMark = true,
				InitialHighWaterMarkNav = 100_000_000m
			};

			var engine = new TotalReturnEngine(config);

			var result = engine.ComputePeriodReturn(
				startDate: new DateTime(2024, 1, 1),
				endDate: new DateTime(2024, 2, 1), // 31 days
				navStart: 100_000_000m,
				navEndBeforeFees: 115_000_000m,
				cashFlows: new PeriodCashFlows
				{
					Subscriptions = 10_000_000m // $10M subscription
				}
			);

			Trace.WriteLine($"Start NAV: ${result.StartNav:N0}");
			Trace.WriteLine($"End NAV (before fees): ${result.EndNavBeforeFees:N0}");
			Trace.WriteLine($"Subscriptions: ${result.NetExternalFlow:N0}");
			Trace.WriteLine($"HWM at Start: ${result.HighWaterMarkAtStart:N0}");
			Trace.WriteLine($"Management Fee: ${result.ManagementFee:N2}");

			// Calculate NAV after mgmt fee
			decimal navAfterMgmt = result.EndNavBeforeFees - result.ManagementFee;
			Trace.WriteLine($"NAV after mgmt fee: ${navAfterMgmt:N0}");

			// CRITICAL: Performance fee base should be (navAfterMgmt - HWM)
			// NOT (navAfterMgmt - HWM - subscriptions) [old bug]
			decimal hwm = result.HighWaterMarkAtStart.Value;
			decimal expectedPerfFeeBase = navAfterMgmt - hwm;
			decimal expectedPerfFee = expectedPerfFeeBase * 0.20m;

			Trace.WriteLine($"Expected perf fee base: ${expectedPerfFeeBase:N0}");
			Trace.WriteLine($"Expected perf fee (20%): ${expectedPerfFee:N2}");
			Trace.WriteLine($"Actual perf fee: ${result.PerformanceFee:N2}");

			AssertApproxEqual(expectedPerfFee, result.PerformanceFee, "Performance fee with HWM");

			Trace.WriteLine($"✓ HWM calculation does NOT subtract external flows");
			Trace.WriteLine($"✓ Performance fee correctly charged on NAV above HWM");
		}

		// ============================================================================
		// TEST 4: Modified Dietz flow weighting (Design Issue #3)
		// ============================================================================
		private static void Test4_ModifiedDietzWeighting()
		{
			var config = new TotalReturnConfig
			{
				ManagementFeeRateAnnual = 0m, // No fees for simplicity
				PerformanceFeeRate = 0m,
				CustodyFeeRateAnnual = 0m
			};

			var engine = new TotalReturnEngine(config);

			// Test with flow at beginning vs end of period
			var resultFlowAtStart = engine.ComputePeriodReturn(
				startDate: new DateTime(2024, 1, 1),
				endDate: new DateTime(2024, 2, 1), // 31 days
				navStart: 100_000_000m,
				navEndBeforeFees: 110_000_000m,
				cashFlows: new PeriodCashFlows
				{
					Subscriptions = 5_000_000m,
					SubscriptionsDaysFromStart = 0.0 // At start of period
				}
			);

			var resultFlowAtEnd = engine.ComputePeriodReturn(
				startDate: new DateTime(2024, 1, 1),
				endDate: new DateTime(2024, 2, 1),
				navStart: 100_000_000m,
				navEndBeforeFees: 110_000_000m,
				cashFlows: new PeriodCashFlows
				{
					Subscriptions = 5_000_000m,
					SubscriptionsDaysFromStart = 31.0 // At end of period
				}
			);

			Trace.WriteLine($"Flow at start - Gross return: {resultFlowAtStart.GrossOfFeesReturn:P4}");
			Trace.WriteLine($"Flow at end - Gross return: {resultFlowAtEnd.GrossOfFeesReturn:P4}");

			// Flow at start gets full weight (1.0), flow at end gets no weight (0.0)
			// So flow at start should result in LOWER return (larger denominator)
			Assert(resultFlowAtStart.GrossOfFeesReturn < resultFlowAtEnd.GrossOfFeesReturn,
				   "Flow at start should have lower return than flow at end");

			Trace.WriteLine($"✓ Flow timing correctly affects return calculation");
			Trace.WriteLine($"✓ Modified Dietz properly weights flows by time in period");
		}

		// ============================================================================
		// TEST 5: Multi-period time-weighted returns
		// ============================================================================
		private static void Test5_MultiPeriodTwr()
		{
			var config = new TotalReturnConfig
			{
				ManagementFeeRateAnnual = 0.01m,
				PerformanceFeeRate = 0.20m,
				CustodyFeeRateAnnual = 0.0005m,
				DayCountBasis = 365
			};

			var engine = new TotalReturnEngine(config);

			var periods = new List<(DateTime, DateTime, decimal, decimal, PeriodCashFlows)>
			{
                // Period 1: 10% gain
                (new DateTime(2024, 1, 1), new DateTime(2024, 2, 1),
				 100_000_000m, 110_000_000m, new PeriodCashFlows()),

                // Period 2: 5% gain with subscription
                (new DateTime(2024, 2, 1), new DateTime(2024, 3, 1),
				 108_500_000m, // Start with NAV after fees from period 1 (approx)
                 118_900_000m,
				 new PeriodCashFlows { Subscriptions = 5_000_000m }),

                // Period 3: -2% loss
                (new DateTime(2024, 3, 1), new DateTime(2024, 4, 1),
				 118_400_000m, // Adjusted for period 2 fees
                 116_500_000m,
				 new PeriodCashFlows())
			};

			var (grossTwr, netTwr, netAfterTaxTwr, breakdowns) =
				engine.ComputeTimeWeightedSeries(periods);

			Trace.WriteLine($"Time-Weighted Returns (3 periods):");
			Trace.WriteLine($"  Gross TWR: {grossTwr:P4}");
			Trace.WriteLine($"  Net TWR: {netTwr:P4}");
			Trace.WriteLine($"  Net After-Tax TWR: {netAfterTaxTwr:P4}");
			Trace.WriteLine("");

			foreach (var bd in breakdowns)
			{
				Trace.WriteLine($"  {bd}");
			}

			// Verify linking
			double manualGross = 1.0;
			double manualNet = 1.0;
			foreach (var bd in breakdowns)
			{
				manualGross *= (1.0 + bd.GrossOfFeesReturn);
				manualNet *= (1.0 + bd.NetOfFeesReturn);
			}
			manualGross -= 1.0;
			manualNet -= 1.0;

			AssertApproxEqual((decimal)grossTwr, (decimal)manualGross, "Gross TWR linking");
			AssertApproxEqual((decimal)netTwr, (decimal)manualNet, "Net TWR linking");

			Trace.WriteLine($"✓ Time-weighted returns correctly chain across periods");
			Trace.WriteLine($"✓ {breakdowns.Count} periods processed successfully");
		}

		// ============================================================================
		// TEST 6: NAV continuity check - should pass (Minor Issue #5)
		// ============================================================================
		private static void Test6_NavContinuityPass()
		{
			var config = new TotalReturnConfig
			{
				ManagementFeeRateAnnual = 0.01m,
				PerformanceFeeRate = 0m,
				CustodyFeeRateAnnual = 0m,
				NavContinuityToleranceBps = 1m // 1 basis point
			};

			var engine = new TotalReturnEngine(config);

			// Period 1
			var period1 = engine.ComputePeriodReturn(
				new DateTime(2024, 1, 1),
				new DateTime(2024, 2, 1),
				100_000_000m,
				105_000_000m,
				new PeriodCashFlows()
			);

			decimal period1EndNav = period1.EndNavAfterTax;
			Trace.WriteLine($"Period 1 ending NAV: ${period1EndNav:N2}");

			// Period 2 - starts with period 1's ending NAV (within tolerance)
			var periods = new List<(DateTime, DateTime, decimal, decimal, PeriodCashFlows)>
			{
				(new DateTime(2024, 1, 1), new DateTime(2024, 2, 1),
				 100_000_000m, 105_000_000m, new PeriodCashFlows()),

				(new DateTime(2024, 2, 1), new DateTime(2024, 3, 1),
				 period1EndNav, // Exact match
                 110_000_000m, new PeriodCashFlows())
			};

			var (grossTwr, netTwr, netAfterTaxTwr, breakdowns) =
				engine.ComputeTimeWeightedSeries(periods);

			Trace.WriteLine($"Period 2 starting NAV: ${periods[1].Item3:N2}");
			Trace.WriteLine($"✓ NAV continuity check passed - NAVs match within tolerance");
		}

		// ============================================================================
		// TEST 7: NAV continuity check - should fail (Minor Issue #5)
		// ============================================================================
		private static void Test7_NavContinuityFail()
		{
			var config = new TotalReturnConfig
			{
				ManagementFeeRateAnnual = 0.01m,
				PerformanceFeeRate = 0m,
				CustodyFeeRateAnnual = 0m,
				NavContinuityToleranceBps = 1m // 1 basis point
			};

			var engine = new TotalReturnEngine(config);

			// Period 1
			var period1 = engine.ComputePeriodReturn(
				new DateTime(2024, 1, 1),
				new DateTime(2024, 2, 1),
				100_000_000m,
				105_000_000m,
				new PeriodCashFlows()
			);

			decimal period1EndNav = period1.EndNavAfterTax;

			// Period 2 - starts with WRONG NAV (difference > tolerance)
			var periods = new List<(DateTime, DateTime, decimal, decimal, PeriodCashFlows)>
			{
				(new DateTime(2024, 1, 1), new DateTime(2024, 2, 1),
				 100_000_000m, 105_000_000m, new PeriodCashFlows()),

				(new DateTime(2024, 2, 1), new DateTime(2024, 3, 1),
				 period1EndNav - 500_000m, // $500K difference - way above 1bp tolerance
                 110_000_000m, new PeriodCashFlows())
			};

			Trace.WriteLine($"Period 1 ending NAV: ${period1EndNav:N2}");
			Trace.WriteLine($"Period 2 starting NAV: ${periods[1].Item3:N2}");
			Trace.WriteLine($"Difference: ${500_000:N2} (should exceed tolerance)");

			bool exceptionThrown = false;
			try
			{
				var (grossTwr, netTwr, netAfterTaxTwr, breakdowns) =
					engine.ComputeTimeWeightedSeries(periods);
			}
			catch (InvalidOperationException ex)
			{
				exceptionThrown = true;
				Trace.WriteLine($"✓ Exception correctly thrown: {ex.Message.Split('\n')[0]}");
			}

			Assert(exceptionThrown, "Should throw exception for NAV discontinuity");
		}

		// ============================================================================
		// TEST 8: Performance fee without HWM
		// ============================================================================
		private static void Test8_PerformanceFeeNonHwm()
		{
			var config = new TotalReturnConfig
			{
				ManagementFeeRateAnnual = 0.01m,
				PerformanceFeeRate = 0.20m,
				CustodyFeeRateAnnual = 0m,
				DayCountBasis = 365,
				UseHighWaterMark = false // No HWM
			};

			var engine = new TotalReturnEngine(config);

			// Positive return period
			var result = engine.ComputePeriodReturn(
				new DateTime(2024, 1, 1),
				new DateTime(2024, 2, 1),
				100_000_000m,
				110_000_000m,
				new PeriodCashFlows()
			);

			Trace.WriteLine($"Profit (before fees): ${result.EndNavBeforeFees - result.StartNav:N0}");
			Trace.WriteLine($"Management Fee: ${result.ManagementFee:N2}");
			Trace.WriteLine($"Performance Fee: ${result.PerformanceFee:N2}");

			// Verify performance fee charged on profit after mgmt fee
			decimal navAfterMgmt = result.EndNavBeforeFees - result.ManagementFee;
			decimal profit = navAfterMgmt - result.StartNav;
			decimal expectedPerfFee = profit * 0.20m;

			AssertApproxEqual(expectedPerfFee, result.PerformanceFee, "Performance fee (no HWM)");
			Trace.WriteLine($"✓ Performance fee correctly calculated on profit");
		}

		// ============================================================================
		// TEST 9: Performance fee with HWM - multiple scenarios
		// ============================================================================
		private static void Test9_PerformanceFeeWithHwm()
		{
			var config = new TotalReturnConfig
			{
				ManagementFeeRateAnnual = 0.01m,
				PerformanceFeeRate = 0.20m,
				CustodyFeeRateAnnual = 0m,
				DayCountBasis = 365,
				UseHighWaterMark = true,
				InitialHighWaterMarkNav = 100_000_000m
			};

			var engine = new TotalReturnEngine(config);

			// Scenario 1: Above HWM - should charge
			Trace.WriteLine("Scenario 1: Above HWM");
			var result1 = engine.ComputePeriodReturn(
				new DateTime(2024, 1, 1),
				new DateTime(2024, 2, 1),
				100_000_000m,
				110_000_000m,
				new PeriodCashFlows()
			);
			Trace.WriteLine($"  Performance Fee: ${result1.PerformanceFee:N2}");
			Trace.WriteLine($"  HWM updated to: ${result1.HighWaterMarkAtEnd:N0}");
			Assert(result1.PerformanceFee > 0, "Should charge perf fee above HWM");

			// Scenario 2: Below new HWM - should NOT charge
			Trace.WriteLine("\nScenario 2: Below HWM");
			var result2 = engine.ComputePeriodReturn(
				new DateTime(2024, 2, 1),
				new DateTime(2024, 3, 1),
				result1.EndNavAfterFeesBeforeTax,
				result1.EndNavAfterFeesBeforeTax * 1.02m, // Small gain but below HWM
				new PeriodCashFlows()
			);
			Trace.WriteLine($"  Performance Fee: ${result2.PerformanceFee:N2}");
			Trace.WriteLine($"  HWM remains: ${result2.HighWaterMarkAtEnd:N0}");
			AssertApproxEqual(0m, result2.PerformanceFee, "No perf fee below HWM");

			Trace.WriteLine($"✓ HWM correctly prevents performance fees when below watermark");
		}

		// ============================================================================
		// TEST 10: Operating expenses in/out of NAV (Minor Issue #6)
		// ============================================================================
		private static void Test10_OperatingExpenses()
		{
			// Test 1: Operating expenses IN NAV (default)
			var config1 = new TotalReturnConfig
			{
				ManagementFeeRateAnnual = 0.01m,
				PerformanceFeeRate = 0m,
				CustodyFeeRateAnnual = 0m,
				OperatingExpensesInNav = true
			};

			var engine1 = new TotalReturnEngine(config1);
			var result1 = engine1.ComputePeriodReturn(
				new DateTime(2024, 1, 1),
				new DateTime(2024, 2, 1),
				100_000_000m,
				104_900_000m, // Already reflects $100K operating expenses
				new PeriodCashFlows { OperatingExpenses = 100_000m }
			);

			Trace.WriteLine("Operating Expenses IN NAV:");
			Trace.WriteLine($"  End NAV (before fees): ${result1.EndNavBeforeFees:N0}");
			Trace.WriteLine($"  Explicit Op Ex: ${result1.OperatingExpensesExplicit:N2}");
			Assert(result1.OperatingExpensesExplicit == 0m, "No explicit op ex when in NAV");

			// Test 2: Operating expenses OUT of NAV
			var config2 = new TotalReturnConfig
			{
				ManagementFeeRateAnnual = 0.01m,
				PerformanceFeeRate = 0m,
				CustodyFeeRateAnnual = 0m,
				OperatingExpensesInNav = false
			};

			var engine2 = new TotalReturnEngine(config2);
			var result2 = engine2.ComputePeriodReturn(
				new DateTime(2024, 1, 1),
				new DateTime(2024, 2, 1),
				100_000_000m,
				105_000_000m, // Does NOT reflect operating expenses
				new PeriodCashFlows { OperatingExpenses = 100_000m }
			);

			Trace.WriteLine("\nOperating Expenses OUT of NAV:");
			Trace.WriteLine($"  End NAV (before fees): ${result2.EndNavBeforeFees:N0}");
			Trace.WriteLine($"  Explicit Op Ex: ${result2.OperatingExpensesExplicit:N2}");
			Assert(result2.OperatingExpensesExplicit == 100_000m, "Explicit op ex deducted");

			Trace.WriteLine($"✓ Operating expenses handled correctly in both modes");
		}

		// ============================================================================
		// TEST 11: After-tax returns
		// ============================================================================
		private static void Test11_AfterTaxReturns()
		{
			var config = new TotalReturnConfig
			{
				ManagementFeeRateAnnual = 0.01m,
				PerformanceFeeRate = 0m,
				CustodyFeeRateAnnual = 0m,
				TrackAfterTaxReturn = true
			};

			var engine = new TotalReturnEngine(config);

			var result = engine.ComputePeriodReturn(
				new DateTime(2024, 1, 1),
				new DateTime(2024, 2, 1),
				100_000_000m,
				105_000_000m,
				new PeriodCashFlows
				{
					TaxesWithheldOnDividends = 50_000m,
					TaxesWithheldOnCapitalGains = 100_000m
				}
			);

			Trace.WriteLine($"End NAV before fees: ${result.EndNavBeforeFees:N0}");
			Trace.WriteLine($"End NAV after fees: ${result.EndNavAfterFeesBeforeTax:N0}");
			Trace.WriteLine($"Taxes withheld: ${result.TaxesWithheld:N2}");
			Trace.WriteLine($"End NAV after tax: ${result.EndNavAfterTax:N0}");
			Trace.WriteLine($"Net of fees return: {result.NetOfFeesReturn:P4}");
			Trace.WriteLine($"Net after-tax return: {result.NetAfterTaxReturn:P4}");

			Assert(result.NetAfterTaxReturn < result.NetOfFeesReturn,
				   "After-tax return should be less than net-of-fees");
			Assert(result.TaxesWithheld == 150_000m, "Total taxes should be $150K");

			Trace.WriteLine($"✓ After-tax returns correctly calculated");
		}

		// ============================================================================
		// TEST 12: Large subscription impact verification
		// ============================================================================
		private static void Test12_LargeSubscription()
		{
			var config = new TotalReturnConfig
			{
				ManagementFeeRateAnnual = 0.01m,
				PerformanceFeeRate = 0.20m,
				CustodyFeeRateAnnual = 0m,
				DayCountBasis = 365,
				UseHighWaterMark = true,
				InitialHighWaterMarkNav = 100_000_000m
			};

			var engine = new TotalReturnEngine(config);

			// Large subscription scenario
			var result = engine.ComputePeriodReturn(
				new DateTime(2024, 1, 1),
				new DateTime(2024, 1, 31), // 30 days
				navStart: 100_000_000m,
				navEndBeforeFees: 160_000_000m, // Includes $50M subscription + gains
				cashFlows: new PeriodCashFlows
				{
					Subscriptions = 50_000_000m // Large subscription
				}
			);

			Trace.WriteLine($"Start NAV: ${result.StartNav:N0}");
			Trace.WriteLine($"End NAV (before fees): ${result.EndNavBeforeFees:N0}");
			Trace.WriteLine($"Subscription: ${result.NetExternalFlow:N0}");
			Trace.WriteLine($"Average AUM: ${result.AverageAum:N0}");
			Trace.WriteLine($"Management Fee: ${result.ManagementFee:N2}");
			Trace.WriteLine($"Performance Fee: ${result.PerformanceFee:N2}");

			// Verify average AUM = (100M + 160M) / 2 = 130M (NOT 105M with old bug)
			AssertApproxEqual(130_000_000m, result.AverageAum, "Avg AUM with large subscription");

			// Verify performance fee calculated without subtracting subscription from HWM comparison
			decimal navAfterMgmt = result.EndNavBeforeFees - result.ManagementFee;
			decimal hwmExcess = navAfterMgmt - 100_000_000m; // Should NOT subtract subscription
			decimal expectedPerfFee = hwmExcess * 0.20m;

			AssertApproxEqual(expectedPerfFee, result.PerformanceFee, "Perf fee with large sub");

			Trace.WriteLine($"✓ Large subscriptions correctly handled in both AUM and HWM calcs");
		}

		// ============================================================================
		// Helper methods
		// ============================================================================
		private static void Assert(bool condition, string message)
		{
			if (!condition)
				throw new Exception($"Assertion failed: {message}");
		}

		private static void AssertApproxEqual(decimal expected, decimal actual, string message)
		{
			decimal diff = Math.Abs(expected - actual);
			decimal tolerance = Math.Max(Math.Abs(expected) * TOLERANCE, 1m); // At least $1 tolerance

			if (diff > tolerance)
			{
				throw new Exception(
					$"Assertion failed: {message}\n" +
					$"  Expected: ${expected:N2}\n" +
					$"  Actual: ${actual:N2}\n" +
					$"  Difference: ${diff:N2} (tolerance: ${tolerance:N2})");
			}
		}
	}
}