//using Microsoft.Windows.Controls.PropertyGrid.Editors;
using HedgeFundReporting;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Configuration;
using System.Windows.Media;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using static TorchSharp.torch.distributions.constraints;

namespace ATMML
{
    class Study
    {
        private Chart _chart;

        public Study(Chart chart)
        {
            _chart = chart;
        }

        public void Close()
        {
            //_chart = null;
        }

        public static string getForecastInterval(string input, int level)
        {
            string output = "";

            var rangeIntervals = new int[] { 5, 10, 15, 20, 25, 30, 35, 50 }.ToList();
            if (input[0] == 'R')
            {
                output = input;
                var range = int.Parse(input.Substring(1));
                var ix = rangeIntervals.IndexOf(range);
                if (ix != -1)
                {
                    ix += level;
                    if (ix < 0) ix = 0;
                    if (ix > rangeIntervals.Count - 1) ix = rangeIntervals.Count - 1;
                    output = "R" + rangeIntervals[ix];
                }
            }
            else if (input == "Yearly" || input == "1Y" || input == "Y")
            {
                output = "Yearly";
            }
            else if (input == "SemiAnnually" || input == "1S" || input == "S")
            {
                //output = "SemiAnnually";
                output = (level == -1) ? "Quarterly" : (level == 0) ? "SemiAnnually" : (level == 1) ? "Yearly" : "Yearly";
            }
            else if (input == "Quarterly" || input == "1Q" || input == "Q")
            {
                output = (level == -1) ? "Monthly" : (level == 0) ? "Quarterly" : (level == 1) ? "Yearly" : "Yearly";  //replaced SemiAnnually in level 1
            }
            else if (input == "Monthly" || input == "1M" || input == "M")
            {
                output = (level == -1) ? "Weekly" : (level == 0) ? "Monthly" : (level == 1) ? "Quarterly" : "Yearly";
            }
            else if (input == "Weekly" || input == "1W" || input == "W" || input == "W30")
            {
                output = (level == -1) ? "Daily" : (level == 0) ? "Weekly" : (level == 1) ? "Monthly" : "Quarterly";
            }
            else if (input == "Daily" || input == "1D" || input == "D" || input == "D30")
            {
                output = (level == -1) ? "240" : (level == 0) ? "Daily" : (level == 1) ? "Weekly" : "Monthly";
            }
            else if (input == "240" || input == "240 Min")
            {
                output = (level == -1) ? "120" : (level == 0) ? "240" : (level == 1) ? "Daily" : "Weekly";
            }
            else if (input == "120" || input == "120 Min")
            {
                output = (level == -1) ? "60" : (level == 0) ? "120" : (level == 1) ? "Daily" : "Weekly";
            }
            else if (input == "60" || input == "60 Min")
            {
                output = (level == -1) ? "30" : (level == 0) ? "60" : (level == 1) ? "240" : "Daily";
            }
            else if (input == "30" || input == "30 Min")
            {
                output = (level == -1) ? "15" : (level == 0) ? "30" : (level == 1) ? "120" : "Daily";
            }
            else if (input == "15" || input == "15 Min")
            {
                output = (level == -1) ? "5" : (level == 0) ? "15" : (level == 1) ? "60" : "240";
            }
            else if (input == "5" || input == "5 Min")
            {
                output = (level == -1) ? "1" : (level == 0) ? "5" : (level == 1) ? "30" : "120";
            }
            else if (input == "2" || input == "2 Min")
            {
                output = (level == -1) ? "1" : (level == 0) ? "2" : (level == 1) ? "5" : "15";
            }
            else if (input == "1" || input == "1 Min")
            {
                output = (level == -1) ? "1" : (level == 0) ? "1" : (level == 1) ? "5" : "30";
            }
            return output;
        }

        public static string getGuideInterval(string input)
        {
            string output = "";

            if (input == "Yearly" || input == "1Y")
            {
                output = "Y";
            }
            else if (input == "SemiAnnually" || input == "1S")
            {
                output = "S";
            }
            else if (input == "Quarterly" || input == "1Q")
            {
                output = "S";
            }
            else if (input == "Monthly" || input == "1M")
            {
                output = "Q";
            }
            else if (input == "Weekly" || input == "1W")
            {
                output = "M";
            }
            else if (input == "Daily" || input == "1D")
            {
                output = "W";
            }
            else
            {
                output = "D";
            }
            return output;
        }

        public Color getCursorColor(string portfolio, string ticker, string interval, DateTime dateTime)
        {
            Color color = Colors.LightGray;
            color.A = 0x40;
            if (MainView.EnableCursorColor)
            {
                Color colorUp = Color.FromArgb(0x65, 0x00, 0xff, 0x00);  // Lime
                Color colorDn = Color.FromArgb(0x65, 0xff, 0x00, 0x00);  // Red
                Color colorNe = Color.FromArgb(0x65, 0xff, 0xff, 0xff);  // Bullish Exit ff00ff & Bearish Exit ffff00
                Color colorUc = Color.FromArgb(0x65, 0x05, 0x7f, 0x81);  // Dark Green Up contra Longer Trend
                Color colorDc = Color.FromArgb(0x65, 0x7f, 0x03, 0x00);  // Dark Red Dn Contra Longer Trend
                int direction = 0;
                List<Trade> positions = Trade.Manager.getPositions(ticker, getGuideInterval(interval));
                int count = positions.Count;
                for (int ii = count - 1; ii >= 0; ii--)
                {
                    if (dateTime.CompareTo(positions[ii].OpenDateTime) >= 0)
                    {
                        direction = (int)positions[ii].Direction;
                        color = (direction > 0) ? colorUp : (direction < 0) ? colorDn : colorNe;
                        //break;

                        if (direction == 1)
                        {
                            color = colorUp;
                        }
                        else if (direction == -1)
                        {
                            color = colorDn;
                        }
                        else if (direction == 3)
                        {
                            color = colorUc;
                        }
                        else if (direction == -3)
                        {
                            color = colorDc;
                        }
                        break;
                    }
                }
            }
            return color;
        }

        public void updateTradeEvents(List<Trade> trades, DateTime endTime, List<Bar> symbolBars, List<Bar> indexBars, bool currentEnable, bool histEnable)
        {
            for (int ii = 0; ii < _chart.Panels.Count; ii++)
            {
                _chart.Panels[ii].ClearTrendLines("TradeEvent");
            }

            if (currentEnable || histEnable)
            {
                int count = trades.Count;
                if (count > 0)
                {
                    List<ChartLine> lines1 = new List<ChartLine>();
                    List<ChartLine> lines2 = new List<ChartLine>();

                    double maxVal = 1000000;
                    double minVal = -1000000;

                    Color colorLime = Color.FromArgb(0x60, 0x00, 0xff, 0x00);
                    Color colorRed = Color.FromArgb(0x60, 0xff, 0x00, 0x00);
                    Color colorMagenta = Color.FromArgb(0x60, 0xff, 0x00, 0xff);
                    Color colorYellow = Color.FromArgb(0x60, 0xff, 0xff, 0x00);

                    Color colorLong = colorLime;
                    Color colorShort = colorRed;
                    Color colorExitLong = colorMagenta;
                    Color colorExitShort = colorYellow;

                    int startIndex = 0;
                    if (!histEnable)
                    {
                        startIndex = count - 1;
                    }

                    for (int ii = startIndex; ii < count; ii++)
                    {
                        Trade trade = trades[ii];

                        if (histEnable || trade.IsOpen())
                        {

                            int size = (int)trade.Direction;

                            string side = (size > 0) ? " Long " : ((size < 0) ? " Short " : "");

                            ChartLine line1 = new ChartLine("TradeEvent");
                            line1.Scalable = false;
                            line1.Point1 = new TimeValuePoint(trade.OpenDateTime, maxVal, 0);
                            line1.Point2 = new TimeValuePoint(trade.OpenDateTime, minVal, 0);
                            Color color1 = (size > 0) ? colorLong : colorShort;
                            line1.Color = color1;
                            line1.Thickness = 5;
                            line1.ZOrder = 10;
                            line1.ToolTip = side; // trade.User;
                            lines1.Add(line1);
                            lines2.Add(line1);

                            if (!trade.IsOpen())
                            {
                                ChartLine line2 = new ChartLine("TradeEvent");
                                line2.Scalable = false;
                                line2.Point1 = new TimeValuePoint(trade.CloseDateTime, maxVal, 0);
                                line2.Point2 = new TimeValuePoint(trade.CloseDateTime, minVal, 0);
                                line2.Color = Color.FromArgb(90, 0xff, 0xff, 0x00);
                                line2.Thickness = 5;
                                line2.ZOrder = 10;
                                line2.ToolTip = "";
                                lines1.Add(line2);
                                lines2.Add(line2);
                            }
                            else
                            {
                                var openPrice = trade.OpenPrice;
                                ChartLine line3 = new ChartLine("TradeEvent");
                                line3.Scalable = true;
                                line3.Point1 = new TimeValuePoint(trade.OpenDateTime, openPrice, 0);
                                line3.Point2 = new TimeValuePoint(DateTime.UtcNow, openPrice, 100);
                                Color color3 = (size >= 0) ? colorLong : colorShort;
                                line3.Color = color1;
                                line3.Thickness = 5;
                                line3.ZOrder = 10;
                                line3.ToolTip = side; // _chart.getValueText(trade.OpenPrice);
                                lines1.Add(line3);
                            }
                        }
                    }

                    int mainPanelIndex = _chart.GetPanelIndex("Main");
                    _chart.Panels[mainPanelIndex].AddTrendLines(lines1); // zOrder = -10
                    for (int ii = 1; ii < _chart.Panels.Count; ii++)
                    {
                        _chart.Panels[ii].AddTrendLines(lines2); // zOrder = -10
                    }
                }
            }
        }

        public void updatePositionRatio(List<DateTime> times, Series close, Series PR, bool enable)
        {
            for (int ii = 0; ii < _chart.Panels.Count; ii++)
            {
                _chart.Panels[ii].ClearTrendLines("PositionRatio");
            }

            if (enable && times != null && times.Count > 0)
            {
                List<ChartLine> lines = new List<ChartLine>();

                double maxVal = 1000000;
                double minVal = -1000000;

                int count = times.Count;
                for (int ii = count - 1; ii > 0; ii--)
                {
                    double pr0 = (ii >= 0 && ii < PR.Count) ? PR[ii] : double.NaN;
                    double pr1 = (ii >= 1 && ii < PR.Count) ? PR[ii - 1] : double.NaN;
                    double cl0 = (ii >= 0 && ii < close.Count) ? close[ii] : double.NaN;

                    if (!double.IsNaN(cl0))
                    {

                        bool longEntry = (pr1 != 1.5 && pr0 == 1.5);
                        bool shortEntry = (pr1 != -1.5 && pr0 == -1.5);

                        bool longExit = (pr1 == 1.5 && pr0 != 1.5);
                        bool shortExit = (pr1 == -1.5 && pr0 != -1.5);

                        if (longEntry || shortEntry || longExit || shortExit)
                        {
                            DateTime date = times[ii];

                            Color color = Colors.Black;

                            if (longEntry) color = Colors.Lime;
                            else if (shortEntry) color = Colors.Red;
                            else if (longExit) color = Colors.Yellow;
                            else if (shortExit) color = Colors.Orange;

                            break;
                        }
                    }
                }

                for (int ii = 0; ii < _chart.Panels.Count; ii++)
                {
                    _chart.Panels[ii].AddTrendLines(lines); // zOrder = -10
                }
            }
        }

        public void updateSCStartDate(List<DateTime> times, Series close, Series SC, bool enable)
        {
            for (int ii = 0; ii < _chart.Panels.Count; ii++)
            {
                _chart.Panels[ii].ClearTrendLines("SCStartDate");
            }

            if (enable)
            {
                List<ChartLine> lines = new List<ChartLine>();

                double maxVal = 1000000;
                double minVal = -1000000;

                int count = times.Count;
                for (int ii = count - 1; ii > 0; ii--)
                {
                    double pr0 = (ii >= 0 && ii < SC.Count) ? SC[ii] : double.NaN;
                    double pr1 = (ii >= 1 && ii < SC.Count) ? SC[ii - 1] : double.NaN;
                    double cl0 = (ii >= 0 && ii < close.Count) ? close[ii] : double.NaN;

                    if (!double.IsNaN(cl0))
                    {

                        bool longEntry = (pr1 != 1 && pr0 == 1);
                        bool shortEntry = (pr1 != -1 && pr0 == -1);

                        if (longEntry || shortEntry)
                        {
                            DateTime date = times[ii];

                            Color color = Colors.Black;

                            if (longEntry) color = Colors.Lime;
                            else if (shortEntry) color = Colors.Red;

                            ChartLine line1 = new ChartLine("SCStartDate");
                            line1.Scalable = false;
                            line1.Point1 = new TimeValuePoint(date, maxVal, 0);
                            line1.Point2 = new TimeValuePoint(date, minVal, 0);
                            line1.Color = color;
                            line1.ToolTip = "ATM SC";
                            line1.Thickness = 1;
                            line1.ZOrder = -10;
                            lines.Add(line1);
                            break;
                        }
                    }
                }

                for (int ii = 0; ii < _chart.Panels.Count; ii++)
                {
                    _chart.Panels[ii].AddTrendLines(lines); // zOrder = -10
                }
            }
        }

		public void updateConditionTrades(string signalName, Dictionary<string, List<DateTime>> times, Dictionary<string, Series[]> bars,
	string symbol, string resultInterval, string[] conditionList, Dictionary<string, object> referenceData)
		{
			var yield = atm.isYieldTicker(symbol);

			//chart z order
			Color colorLime = Color.FromArgb(0x95, 0x00, 0x66, 0x00);  // 0x95, 0x00, 0xc0, 0x00o
			Color colorRed = Color.FromArgb(0x95, 0x80, 0x00, 0x00);  // 0x95, 0xc0, 0x00, 0x00
			Color colorLimeShade = Color.FromArgb(0x30, 0x00, 0x66, 0x00);  // 0x17, 0xcc, 0xff, 0xff
			Color colorRedShade = Color.FromArgb(0x30, 0x80, 0x00, 0x00);  // 0x17, 0xf0, 0x80, 0x80
			Color colorOrange = Color.FromArgb(0x40, 0xff, 0x8c, 0x00);  // Dark Orange
			Color colorViolet = Color.FromArgb(0x30, 0x8e, 0x82, 0xee);  // Violet

			// condition color order
			Color[] colors1 = { colorLime, colorViolet, colorOrange, colorRed, Colors.White, Colors.Aqua };
			Color[] colors2 = { colorRed, colorOrange, colorLime, colorViolet, Colors.Aqua, Colors.White }; // for yield

			double maxVal = 10000;
			double minVal = -10000;

			List<ChartLine> lines = new List<ChartLine>();

			int barCount = times[resultInterval].Count;

			int conditionCount = Math.Min(conditionList.Length, colors1.Length);

			Series[] outputs = new Series[conditionCount];
			for (int ii = 0; ii < conditionCount; ii++)
			{
				Series signal1 = null;
				Series signal2 = new Series(barCount, 0);
				bool first = true;

				string compoundCondition = conditionList[ii];
				if (compoundCondition.Length > 0)
				{
					string[] conditions = compoundCondition.Split('\u0001');
					foreach (string condition in conditions)
					{
						string[] field = condition.Split('\u0002');
						string name = field[0];
						string interval = field[1].Replace(" Min", "");
						string ago = (field.Length > 2) ? field[2] : "10000";
						string type = (field.Length > 3) ? field[3] : "1";

						var intervalList = Conditions.GetIntervals(condition);
						if (!intervalList.Contains(resultInterval)) intervalList.Add(resultInterval);
						Series series = Conditions.Calculate(name, symbol, intervalList.ToArray(), barCount, times, bars, referenceData);

						if (interval != resultInterval)
						{
							List<DateTime> times1 = times[interval];
							List<DateTime> times2 = times[resultInterval];

							int cnt = series.Count;

							int cnt1 = times1.Count;
							int cnt2 = times2.Count;

							int sidx1 = cnt1 - 1;

							signal1 = new Series(cnt2, 0);

							string i1 = interval.Substring(0, 1);
							string i2 = resultInterval.Substring(0, 1);
							int keySize1 = (i1 == "Y") ? 4 : ((i1 == "S" || i1 == "Q" || i1 == "M") ? 6 : ((i1 == "W" || i1 == "D") ? 8 : 12));
							int keySize2 = (i2 == "Y") ? 4 : ((i2 == "S" || i2 == "Q" || i2 == "M") ? 6 : ((i2 == "W" || i2 == "D") ? 8 : 12));
							int keySize = Math.Min(keySize1, keySize2);

							for (int idx2 = cnt2 - 1; idx2 >= 0; idx2--)
							{
								DateTime time2 = times2[idx2];
								long key2 = long.Parse(time2.ToString("yyyyMMddHHmm").Substring(0, keySize));

								for (int idx1 = sidx1; idx1 >= 0; idx1--)
								{
									DateTime time1 = times1[idx1];
									if (i1 == "W" && i2 == "D")
									{
										time1 -= new TimeSpan(5, 0, 0, 0);
									}
									else if (i1 == "M" && i2 == "W")
									{
										time1 = new DateTime(time1.Year, time1.Month, 1);
									}
									long key1 = long.Parse(time1.ToString("yyyyMMddHHmm").Substring(0, keySize));

									if (key1 <= key2)
									{
										int idx = (cnt - 1) - ((cnt1 - 1) - idx1);

										for (int kk = 0; kk < ago.Length; kk++)
										{
											if (ago[kk] == '1')
											{
												if (series[idx - kk] == 1)
												{
													signal1[idx2] = 1;
												}
											}
										}
										sidx1 = idx1;
										break;
									}
								}
							}
						}
						else
						{
							signal1 = new Series(series.Count, 0);
							for (int idx = ago.Length; idx < series.Count; idx++)
							{
								for (int kk = 0; kk < ago.Length; kk++)
								{
									if (ago[kk] == '1')
									{
										if (series[idx - kk] == 1)
										{
											signal1[idx] = 1;
										}
									}
								}
							}
						}

						int c1 = signal2.Count;
						int c2 = signal1.Count;

						if (type == "2")
						{
							for (int jj = 0; jj < c2; jj++)
							{
								if (jj != c2 - 1 || signal1[jj + 1] == 1)
								{
									signal1[jj] = 0;
								}
							}
						}

						for (int idx = 0; idx < c2; idx++)
						{
							int index1 = c1 - 1 - idx;
							int index2 = c2 - 1 - idx;
							if (index1 >= 0 && index2 >= 0)
							{
								if (first)
								{
									signal2[index1] = signal1[index2];
								}
								else
								{
									signal2[index1] = (signal1[index2] == 1 && signal2[index1] == 1) ? 1 : 0;
								}
							}
						}
						first = false;
					}
				}
				outputs[ii] = signal2;
			}

			for (int ii = 0; ii < conditionCount; ii++)
			{
				var cond = conditionList[ii];
				if (cond.Length > 0)
				{
					string[] field = conditionList[ii].Split('\u0002');
					if (field.Length >= 2)
					{
						string name = field[0];
						string interval = field[1].Replace(" Min", "");

						string trdUp = yield ? "Bearish" : "Bullish";
						string trdDn = yield ? "Bullish" : "Bearish";

						string toolTip = name;
						if (name == "SC Up and FT TU") toolTip = interval + " Trend is " + trdUp + ", and " + interval + " FT is Turning Up";
						else if (name == "SC Up and FT TD") toolTip = interval + " Trend is " + trdUp + ", and " + interval + " FT is Turning Dn";
						else if (name == "SC Dn and FT TU") toolTip = interval + " Trend is " + trdDn + ", and " + interval + " FT is Turning Up";
						else if (name == "SC Dn and FT TD") toolTip = interval + " Trend is " + trdDn + ", and " + interval + " FT is Turning Dn";

						for (int jj = 0; jj < outputs[ii].Count; jj++)
						{
							bool ok = (!double.IsNaN(outputs[ii][jj]) && outputs[ii][jj] != 0);
							if (ok)
							{
								DateTime date = times[resultInterval][jj];
								ChartLine line1 = new ChartLine(signalName);
								line1.Scalable = false;
								line1.Point1 = new TimeValuePoint(date, maxVal, 0);
								line1.Point2 = new TimeValuePoint(date, minVal, 0);
								line1.Color = yield ? colors2[ii] : colors1[ii];
								line1.Thickness = 8;  //3
								line1.ZOrder = 200000;
								//line1.ToolTip = toolTip;
								lines.Add(line1);
							}
						}
					}
				}
			}

			for (int ii = 0; ii < _chart.Panels.Count; ii++)
			{
				_chart.Panels[ii].ClearTrendLines(signalName);
				_chart.Panels[ii].AddTrendLines(lines);
			}
		}

		//public void updateConditionTradesOld(Dictionary<string, List<DateTime>> times, Dictionary<string, Series[]> bars, string symbol, string resultInterval, string[] conditionList)
  //      {
  //          //chart z order
  //          Color colorLime = Color.FromArgb(0x95, 0x00, 0x66, 0x00);  // 0x95, 0x00, 0xc0, 0x00o
  //          Color colorRed = Color.FromArgb(0x95, 0x80, 0x00, 0x00);  // 0x95, 0xc0, 0x00, 0x00
  //          Color colorLimeShade = Color.FromArgb(0x30, 0x00, 0x66, 0x00);  // 0x17, 0xcc, 0xff, 0xff
  //          Color colorRedShade = Color.FromArgb(0x30, 0x80, 0x00, 0x00);  // 0x17, 0xf0, 0x80, 0x80

  //          // condition color order
  //          Color[] colors = { colorLime, colorLimeShade, colorRed, colorRedShade, Colors.White, Colors.Aqua };

  //          double maxVal = 1000000;
  //          double minVal = -1000000;

  //          List<ChartLine> lines = new List<ChartLine>();

  //          int barCount = times[resultInterval].Count;

  //          int conditionCount = Math.Min(conditionList.Length, colors.Length);

  //          Series[] outputs = new Series[conditionCount];
  //          for (int ii = 0; ii < conditionCount; ii++)
  //          {
  //              Series signal1 = null;
  //              Series signal2 = new Series(barCount, 0);
  //              bool first = true;

  //              string compoundCondition = conditionList[ii];
  //              if (compoundCondition.Length > 0)
  //              {
  //                  string[] conditions = compoundCondition.Split('\u0001');
  //                  foreach (string condition in conditions)
  //                  {
  //                      string[] field = condition.Split('\u0002');
  //                      string name = field[0];
  //                      string interval = field[1].Replace(" Min", "");
  //                      string ago = (field.Length > 2) ? field[2] : "10000";
  //                      string type = (field.Length > 3) ? field[3] : "1";

  //                      var intervalList = Conditions.GetIntervals(condition);
  //                      if (!intervalList.Contains(resultInterval)) intervalList.Add(resultInterval);
  //                      Series series = Conditions.Calculate(name, symbol, intervalList.ToArray(), barCount, times, bars, null);

  //                      if (interval != resultInterval)
  //                      {
  //                          List<DateTime> times1 = times[interval];
  //                          List<DateTime> times2 = times[resultInterval];

  //                          int cnt = series.Count;

  //                          int cnt1 = times1.Count;
  //                          int cnt2 = times2.Count;

  //                          int sidx1 = cnt1 - 1;

  //                          signal1 = new Series(cnt2, 0);

  //                          string i1 = interval.Substring(0, 1);
  //                          string i2 = resultInterval.Substring(0, 1);
  //                          int keySize1 = (i1 == "Y") ? 4 : ((i1 == "S" || i1 == "Q" || i1 == "M") ? 6 : ((i1 == "W" || i1 == "D") ? 8 : 12));
  //                          int keySize2 = (i2 == "Y") ? 4 : ((i2 == "S" || i2 == "Q" || i2 == "M") ? 6 : ((i2 == "W" || i2 == "D") ? 8 : 12));
  //                          int keySize = Math.Min(keySize1, keySize2);

  //                          for (int idx2 = cnt2 - 1; idx2 >= 0; idx2--)
  //                          {
  //                              DateTime time2 = times2[idx2];
  //                              long key2 = long.Parse(time2.ToString("yyyyMMddHHmm").Substring(0, keySize));

  //                              for (int idx1 = sidx1; idx1 >= 0; idx1--)
  //                              {
  //                                  DateTime time1 = times1[idx1];
  //                                  if (i1 == "W" && i2 == "D")
  //                                  {
  //                                      time1 -= new TimeSpan(5, 0, 0, 0);
  //                                  }
  //                                  else if (i1 == "M" && i2 == "W")
  //                                  {
  //                                      time1 = new DateTime(time1.Year, time1.Month, 1);
  //                                  }
  //                                  long key1 = long.Parse(time1.ToString("yyyyMMddHHmm").Substring(0, keySize));

  //                                  if (key1 <= key2)
  //                                  {
  //                                      int idx = (cnt - 1) - ((cnt1 - 1) - idx1);

  //                                      for (int kk = 0; kk < ago.Length; kk++)
  //                                      {
  //                                          if (ago[kk] == '1')
  //                                          {
  //                                              if (series[idx - kk] == 1)
  //                                              {
  //                                                  signal1[idx2] = 1;
  //                                              }
  //                                          }
  //                                      }
  //                                      sidx1 = idx1;
  //                                      break;
  //                                  }
  //                              }
  //                          }
  //                      }
  //                      else
  //                      {
  //                          signal1 = new Series(series.Count, 0);
  //                          for (int idx = ago.Length; idx < series.Count; idx++)
  //                          {
  //                              for (int kk = 0; kk < ago.Length; kk++)
  //                              {
  //                                  if (ago[kk] == '1')
  //                                  {
  //                                      if (series[idx - kk] == 1)
  //                                      {
  //                                          signal1[idx] = 1;
  //                                      }
  //                                  }
  //                              }
  //                          }
  //                      }

  //                      int c1 = signal2.Count;
  //                      int c2 = signal1.Count;

  //                      if (type == "2")
  //                      {
  //                          for (int jj = 0; jj < c2; jj++)
  //                          {
  //                              if (jj != c2 - 1 || signal1[jj + 1] == 1)
  //                              {
  //                                  signal1[jj] = 0;
  //                              }
  //                          }
  //                      }

  //                      for (int idx = 0; idx < c2; idx++)
  //                      {
  //                          int index1 = c1 - 1 - idx;
  //                          int index2 = c2 - 1 - idx;
  //                          if (index1 >= 0 && index2 >= 0)
  //                          {
  //                              if (first)
  //                              {
  //                                  signal2[index1] = signal1[index2];
  //                              }
  //                              else
  //                              {
  //                                  signal2[index1] = (signal1[index2] == 1 && signal2[index1] == 1) ? 1 : 0;
  //                              }
  //                          }
  //                      }
  //                      first = false;
  //                  }
  //              }
  //              outputs[ii] = signal2;
  //          }

  //          for (int ii = 0; ii < conditionCount; ii++)
  //          {
  //              string[] field = conditionList[ii].Split('\u0002');
  //              string name = field[0];

  //              for (int jj = 0; jj < outputs[ii].Count; jj++)
  //              {
  //                  bool ok = (!double.IsNaN(outputs[ii][jj]) && outputs[ii][jj] != 0);
  //                  if (ok)
  //                  {
  //                      DateTime date = times[resultInterval][jj];
  //                      ChartLine line1 = new ChartLine("ConditionTrade");
  //                      line1.Scalable = false;
  //                      line1.Point1 = new TimeValuePoint(date, maxVal, 0);
  //                      line1.Point2 = new TimeValuePoint(date, minVal, 0);
  //                      line1.Color = colors[ii];
  //                      line1.Thickness = 8;  //3
  //                      line1.ZOrder = -20;
  //                      line1.ToolTip = name;
  //                      lines.Add(line1);
  //                  }
  //              }
  //          }

  //          for (int ii = 0; ii < _chart.Panels.Count; ii++)
  //          {
  //              _chart.Panels[ii].ClearTrendLines("ConditionTrade");
  //              _chart.Panels[ii].AddTrendLines(lines);
  //          }
  //      }

        public void calculateElliottWave(List<DateTime> times, Series[] series, bool showCounts, bool showPTI, bool showChannels, bool showProjections, int currentBarIndex, int extraBars, Dictionary<string, Indicator> indicators)
        {
            if (series != null)
            {
                if (series[1] != null && series[2] != null && series[3] != null)
                {
                    int count = times.Count - extraBars;
                    Series hi = new Series(count);
                    Series lo = new Series(count);
                    Series cl = new Series(count);
                    for (int ii = 0; ii < count; ii++)
                    {
                        hi[ii] = series[1][ii];
                        lo[ii] = series[2][ii];
                        cl[ii] = series[3][ii];
                    }

                    ElliottWaveInput input = new ElliottWaveInput();
                    ElliottWaveOutput output = new ElliottWaveOutput();

                    ElliottAlgorithm algorithm = new ElliottAlgorithm();

                    input.lowX = lo;
                    input.highX = hi;
                    input.low = lo;
                    input.high = hi;
                    input.close = cl;
                    int period1 = ElliottAlgorithm.EW_PERIOD1;
                    int period2 = ElliottAlgorithm.EW_PERIOD2;
                    Series mid = (hi + lo) / 2;
                    input.oscX = mid.osc(OscType.Difference, MAType.Simple, MAType.Simple, period1, period2);
                    input.osc1 = mid.osc(OscType.Difference, MAType.Simple, MAType.Simple, period1, period2);
                    input.osc2 = mid.osc(OscType.Difference, MAType.Simple, MAType.Simple, period1, period2 / 2);
                    input.overlap = ElliottAlgorithm.STOCKS_4_1_OVERLAP;
                    input.completedWavesOnly = true;
                    input.barCount = 1000;

                    int barCount = input.close.Count;

                    int end = Math.Max(0, barCount - input.barCount);
                    for (int ii = 0; ii < end; ii++)
                    {
                        input.lowX[ii] = double.NaN;
                        input.highX[ii] = double.NaN;
                        input.low[ii] = double.NaN;
                        input.high[ii] = double.NaN;
                        input.close[ii] = double.NaN;
                    }

                    end = Math.Max(0, barCount - (input.barCount - period2));
                    for (int ii = 0; ii < end; ii++)
                    {
                        input.oscX[ii] = 0;
                        input.osc1[ii] = 0;
                        input.osc2[ii] = 0;
                    }

                    algorithm.Evaluate(input, output);

                    string[] majorLabels = { "1", "2", "3", "4", "5", "X", "A", "B", "C" };
                    string[] intermediateLabels = { "1", "2", "3", "4", "5", "X", "A", "B", "C" };
                    string[] minorLabels = { "i", "ii", "iii", "iv", "v", "x", "a", "b", "c" };

                    addElliottLabels(showCounts, 2001, times, output.waveUp[2], minorLabels, true, Colors.Lime, 1.8);
                    addElliottLabels(showCounts, 2002, times, output.waveDn[2], minorLabels, false, Colors.Red, 1.8);
                    addElliottLabels(showCounts, 2003, times, output.waveUp[1], intermediateLabels, true, Colors.Lime, 2.1);
                    addElliottLabels(showCounts, 2004, times, output.waveDn[1], intermediateLabels, false, Colors.Red, 2.1);
                    addElliottLabels(showCounts, 2005, times, output.waveUp[0], majorLabels, true, Colors.Lime, 2.5);
                    addElliottLabels(showCounts, 2006, times, output.waveDn[0], majorLabels, false, Colors.Red, 2.5);
                    addElliottChannels(showChannels, output.channel);
                    addElliottPTILabel(showPTI, 2007, times, output.pti, output.ptiDir, output.ptiIndex, output.ptiPrice);
                    addElliottProjectionLabels(showProjections, 2008, times, output.projectionWaveNumber, output.projectionPrice, currentBarIndex);
                }
            }
        }

        private void addElliottChannels(bool show, Series[] channel)
        {
            int mainPanelIndex = _chart.GetPanelIndex("Main");

            if (show)
            {
                int count = channel.Length;
                for (int ii = 0; ii < count; ii++)
                {
                    if (channel[ii] != null)
                    {
                        Color color = (ii == 0) ? Colors.Cyan : (ii == 1) ? Colors.Lime : Colors.Red;
                        List<Color> colorList = new List<Color>();
                        colorList.Add(color);

                        string name = "Channel" + (ii + 1).ToString();
                        _chart.Panels[mainPanelIndex].AddCurve(name, colorList, 1.0);
                        _chart.Panels[mainPanelIndex].SetCurveTooltip(name, "Wave 4 Channel Retracements");
                        _chart.Panels[mainPanelIndex].LoadCurveValues1(name, channel[ii].Data);
                        _chart.Panels[mainPanelIndex].SetCurveType(name, CurveType.Line);
                        _chart.Panels[mainPanelIndex].SetCurveScalable(name, false);
                    }
                }
            }
            else
            {
                _chart.Panels[mainPanelIndex].RemoveCurve("Channel1");
                _chart.Panels[mainPanelIndex].RemoveCurve("Channel2");
                _chart.Panels[mainPanelIndex].RemoveCurve("Channel3");
            }
        }

        private void addElliottProjectionLabels(bool show, int id, List<DateTime> times, Series numbers, Series prices, int currentBarIndex)
        {
            int mainPanelIndex = _chart.GetPanelIndex("Main");

            _chart.Panels[mainPanelIndex].RemoveAnnotations(id);

            if (show)
            {
                int space = 4;
                int count = numbers.Count;
                int index = currentBarIndex + space;

                int previousNumber = 0;
                for (int ii = 0; ii < count; ii++)
                {
                    if (!double.IsNaN(numbers[ii]))
                    {
                        int number = (int)numbers[ii];
                        double price = prices[ii];

                        if (previousNumber != 0 && number != previousNumber)
                        {
                            index += space;
                        }
                        previousNumber = number;

                        TextAnnotation marker = new TextAnnotation();
                        marker.Id = id;
                        marker.DateTime = times[index];
                        marker.Placement = Placement.Value;
                        marker.Value = price;
                        marker.Color = Colors.White;
                        marker.Offset = 1;
                        marker.Width = 5.0;
                        marker.Height = 3.0;
                        marker.Text = "- " + number.ToString() + " -";
                        marker.Tooltip = price.ToString(".00");
                        _chart.Panels[mainPanelIndex].AddAnnotation(marker);
                    }
                }
            }
        }

        private void addElliottPTILabel(bool show, int id, List<DateTime> times, int pti, int ptiDir, int ptiIndex, double ptiPrice)
        {
            int mainPanelIndex = _chart.GetPanelIndex("Main");

            _chart.Panels[mainPanelIndex].RemoveAnnotations(id);

            if (show)
            {
                if (pti != 0)
                {
                    TextAnnotation marker = new TextAnnotation();
                    marker.Id = id;
                    marker.DateTime = times[ptiIndex];
                    marker.Placement = Placement.Value;
                    marker.Value = ptiPrice;
                    marker.Color = (ptiDir > 0) ? Colors.Lime : Colors.Red;
                    marker.Offset = 1;
                    marker.Width = 3.0;
                    marker.Height = 3.0;
                    marker.Text = Math.Abs(pti).ToString();
                    marker.Tooltip = "Wave 4 Profit Taking Index";
                    _chart.Panels[mainPanelIndex].AddAnnotation(marker);
                }
            }
        }

        private void addElliottLabels(bool show, int id, List<DateTime> times, Series input, string[] labels, bool up, Color color, double size)
        {
            int mainPanelIndex = _chart.GetPanelIndex("Main");

            _chart.Panels[mainPanelIndex].RemoveAnnotations(id);

            if (show)
            {
                int count = input.Count;
                for (int ii = 0; ii < count; ii++)
                {
                    int value = (int)input[ii];
                    if (value >= 1 && value <= 9)
                    {
                        int idx = value - 1;
                        bool odd = ((value & 1) == 1);
                        TextAnnotation marker = new TextAnnotation();
                        marker.Id = id;
                        marker.DateTime = times[ii];
                        marker.Placement = (up && !odd || !up && odd) ? Placement.Below : Placement.Above;
                        marker.Color = color;
                        marker.Offset = 1;
                        marker.Width = size;
                        marker.Height = size;
                        marker.Text = labels[idx];
                        _chart.Panels[mainPanelIndex].AddAnnotation(marker);
                    }
                }
            }
        }
		public void calculateMTFastTurningPoint(List<DateTime> times, Series[] series, int currentBarIndex, bool currentEnable, bool historyEnable, bool estEnable, Dictionary<string, Indicator> indicators)
		{
			int mainPanelIndex = _chart.GetPanelIndex("Main");

			bool update = false;

			if ((currentEnable || historyEnable) && series != null)
			{
				Series hi = series[1];
				Series lo = series[2];
				Series cl = series[3];

				if (hi != null && lo != null && cl != null)
				{
					if (estEnable)
					{
						double lastCl = cl[currentBarIndex];
						hi[currentBarIndex + 1] = lastCl;
						lo[currentBarIndex + 1] = lastCl;
						cl[currentBarIndex + 1] = lastCl;
					}

					Series FT = atm.calculateFT(hi, lo, cl);

					int count = FT.Count;

					Series fttpUp1 = new Series(count);
					Series fttpDn1 = new Series(count);
					Series fttpUp2 = new Series(count);
					Series fttpDn2 = new Series(count);

					FastTrigTPOp op = new FastTrigTPOp();
					double[] input = new double[3];
					double[] output = new double[3];
					for (int ii = 0; ii < count; ii++)
					{

						var bp = false;
						if (ii == 487)
						{
							bp = true;
						}


						output[0] = double.NaN;
						output[1] = double.NaN;
						if (!double.IsNaN(hi[ii]) && !double.IsNaN(lo[ii]) && !double.IsNaN(cl[ii]))
						{
							input[0] = hi[ii];
							input[1] = lo[ii];
							input[2] = cl[ii];
							op.Calculate(input, output, ii == count - 1);
						}

						double up = double.IsNaN(output[0]) ? output[1] : output[0];
						double dn = double.IsNaN(output[1]) ? output[0] : output[1];

						bool display = (historyEnable || ii >= currentBarIndex);

						if (ii >= 2 && FT[ii - 1] < FT[ii - 2] && up > 0)
						{
							fttpUp1[ii] = display ? up : double.NaN;
							fttpUp2[ii] = up;
						}
						else if (ii >= 2 && FT[ii - 1] > FT[ii - 2] && dn > 0)
						{
							fttpDn1[ii] = display ? dn : double.NaN;
							fttpDn2[ii] = dn;
						}
					}

					if (estEnable)
					{
						hi[currentBarIndex + 1] = double.NaN;
						lo[currentBarIndex + 1] = double.NaN;
						cl[currentBarIndex + 1] = double.NaN;
					}

					var MTFTTPUp1 = (indicators["Mid Term FT Current"].Parameters["MTFTTPUp1"] as ColorParameter).Value;
					var MTFTTPDn1 = (indicators["Mid Term FT Current"].Parameters["MTFTTPDn1"] as ColorParameter).Value;

					List<Color> upColorList1 = new List<Color>();
					upColorList1.Add(MTFTTPUp1);

					List<Color> dnColorList1 = new List<Color>();
					dnColorList1.Add(MTFTTPDn1);

					update = true;

					_chart.Panels[mainPanelIndex].RemoveCurve("MTFTTPUp1");
					_chart.Panels[mainPanelIndex].RemoveCurve("MTFTTPDn1");

					_chart.Panels[mainPanelIndex].AddCurve("MTFTTPUp1", upColorList1, 2.5);
					_chart.Panels[mainPanelIndex].LoadCurveValues1("MTFTTPUp1", fttpUp1.Data);
					_chart.Panels[mainPanelIndex].SetCurveType("MTFTTPUp1", CurveType.Dash);
					_chart.Panels[mainPanelIndex].SetCurveScalable("MTFTTPUp1", false);

					_chart.Panels[mainPanelIndex].AddCurve("MTFTTPDn1", dnColorList1, 2.5);
					_chart.Panels[mainPanelIndex].LoadCurveValues1("MTFTTPDn1", fttpDn1.Data);
					_chart.Panels[mainPanelIndex].SetCurveType("MTFTTPDn1", CurveType.Dash);
					_chart.Panels[mainPanelIndex].SetCurveScalable("MTFTTPDn1", false);
				}
			}
			if (!update)
			{
				_chart.Panels[mainPanelIndex].RemoveCurve("MTFTTPUp1");
				_chart.Panels[mainPanelIndex].RemoveCurve("MTFTTPDn1");
				_chart.Panels[mainPanelIndex].RemoveCurve("MTFTTPUp2");
				_chart.Panels[mainPanelIndex].RemoveCurve("MTFTTPDn2");
			}
		}


		public void calculateFastTurningPoint(List<DateTime> times, Series[] series, int currentBarIndex, bool currentEnable, bool historyEnable, bool estEnable, Dictionary<string, Indicator> indicators)
        {
            int mainPanelIndex = _chart.GetPanelIndex("Main");

            bool update = false;

            if ((currentEnable || historyEnable) && series != null)
            {
                Series hi = series[1];
                Series lo = series[2];
                Series cl = series[3];

                if (hi != null && lo != null && cl != null)
                {
                    if (estEnable)
                    {
                        double lastCl = cl[currentBarIndex];
                        hi[currentBarIndex + 1] = lastCl;
                        lo[currentBarIndex + 1] = lastCl;
                        cl[currentBarIndex + 1] = lastCl;
                    }

                    Series FT = atm.calculateFT(hi, lo, cl);

                    int count = FT.Count;

                    Series fttpUp1 = new Series(count);
                    Series fttpDn1 = new Series(count);
                    Series fttpUp2 = new Series(count);
                    Series fttpDn2 = new Series(count);

                    FastTrigTPOp op = new FastTrigTPOp();
                    double[] input = new double[3];
                    double[] output = new double[3];
                    for (int ii = 0; ii < count; ii++)
                    {

                        var bp = false;
                        if (ii == 487)
                        {
                            bp = true;
                        }


                        output[0] = double.NaN;
                        output[1] = double.NaN;
                        if (!double.IsNaN(hi[ii]) && !double.IsNaN(lo[ii]) && !double.IsNaN(cl[ii]))
                        {
                            input[0] = hi[ii];
                            input[1] = lo[ii];
                            input[2] = cl[ii];
                            op.Calculate(input, output, ii == count - 1);
                        }

                        double up = double.IsNaN(output[0]) ? output[1] : output[0];
                        double dn = double.IsNaN(output[1]) ? output[0] : output[1];

                        bool display = (historyEnable || ii >= currentBarIndex);

                        if (ii >= 2 && FT[ii - 1] < FT[ii - 2] && up > 0)
                        {
                            fttpUp1[ii] = display ? up : double.NaN;
                            fttpUp2[ii] = up;
                        }
                        else if (ii >= 2 && FT[ii - 1] > FT[ii - 2] && dn > 0)
                        {
                            fttpDn1[ii] = display ? dn : double.NaN;
                            fttpDn2[ii] = dn;
                        }
                    }

                    if (estEnable)
                    {
                        hi[currentBarIndex + 1] = double.NaN;
                        lo[currentBarIndex + 1] = double.NaN;
                        cl[currentBarIndex + 1] = double.NaN;
                    }

					var FTTPUp1 = (indicators["Short Term FT Current"].Parameters["FTTPUp1"] as ColorParameter).Value;
                    var FTTPDn1 = (indicators["Short Term FT Current"].Parameters["FTTPDn1"] as ColorParameter).Value;

                    List<Color> upColorList1 = new List<Color>();
                    upColorList1.Add(FTTPUp1);

                    List<Color> dnColorList1 = new List<Color>();
                    dnColorList1.Add(FTTPDn1);


                    var FTTPUp2 = (indicators["Short Term FT Nxt Bar"].Parameters["FTTPUp2"] as ColorParameter).Value;
                    var FTTPDn2 = (indicators["Short Term FT Nxt Bar"].Parameters["FTTPDn2"] as ColorParameter).Value;

                    List<Color> upColorList2 = new List<Color>();
                    upColorList2.Add(FTTPUp2);

                    List<Color> dnColorList2 = new List<Color>();
                    dnColorList2.Add(FTTPDn2);

                    update = true;

                    _chart.Panels[mainPanelIndex].RemoveCurve("FTTPUp1");
                    _chart.Panels[mainPanelIndex].RemoveCurve("FTTPDn1");
                    _chart.Panels[mainPanelIndex].RemoveCurve("FTTPUp2");
                    _chart.Panels[mainPanelIndex].RemoveCurve("FTTPDn2");

                    _chart.Panels[mainPanelIndex].AddCurve("FTTPUp1", upColorList1, 2.5);
                    _chart.Panels[mainPanelIndex].LoadCurveValues1("FTTPUp1", fttpUp1.Data);
                    _chart.Panels[mainPanelIndex].SetCurveType("FTTPUp1", CurveType.Dash);
                    _chart.Panels[mainPanelIndex].SetCurveScalable("FTTPUp1", false);

                    _chart.Panels[mainPanelIndex].AddCurve("FTTPDn1", dnColorList1, 2.5);
                    _chart.Panels[mainPanelIndex].LoadCurveValues1("FTTPDn1", fttpDn1.Data);
                    _chart.Panels[mainPanelIndex].SetCurveType("FTTPDn1", CurveType.Dash);
                    _chart.Panels[mainPanelIndex].SetCurveScalable("FTTPDn1", false);

                    _chart.Panels[mainPanelIndex].AddCurve("FTTPUp2", upColorList2, 0);
                    _chart.Panels[mainPanelIndex].LoadCurveValues1("FTTPUp2", fttpUp2.Data);
                    _chart.Panels[mainPanelIndex].SetCurveType("FTTPUp2", CurveType.None);
                    _chart.Panels[mainPanelIndex].SetCurveScalable("FTTPUp2", false);
                    _chart.Panels[mainPanelIndex].SetCurveLegend("FTTPUp2", "FT");

                    _chart.Panels[mainPanelIndex].AddCurve("FTTPDn2", dnColorList2, 0);
                    _chart.Panels[mainPanelIndex].LoadCurveValues1("FTTPDn2", fttpDn2.Data);
                    _chart.Panels[mainPanelIndex].SetCurveType("FTTPDn2", CurveType.None);
                    _chart.Panels[mainPanelIndex].SetCurveScalable("FTTPDn2", false);
                    _chart.Panels[mainPanelIndex].SetCurveLegend("FTTPDn2", "FT");
                }
            }
            if (!update)
            {
                _chart.Panels[mainPanelIndex].RemoveCurve("FTTPUp1");
                _chart.Panels[mainPanelIndex].RemoveCurve("FTTPDn1");
                _chart.Panels[mainPanelIndex].RemoveCurve("FTTPUp2");
                _chart.Panels[mainPanelIndex].RemoveCurve("FTTPDn2");
            }
        }

        public void calculateSlowTurningPoint(List<DateTime> times, Series[] series, int currentBarIndex, bool currentEnable, bool historyEnable, bool estEnable, Dictionary<string, Indicator> indicators)
        {
            int mainPanelIndex = _chart.GetPanelIndex("Main");

            bool update = false;

            if ((currentEnable || historyEnable) && series != null)
            {
                Series hi = series[1];
                Series lo = series[2];
                Series cl = series[3];

                if (hi != null && lo != null && cl != null)
                {
                    if (estEnable)
                    {
                        double lastCl = cl[currentBarIndex];
                        hi[currentBarIndex + 1] = lastCl;
                        lo[currentBarIndex + 1] = lastCl;
                        cl[currentBarIndex + 1] = lastCl;
                    }

                    Series ST = atm.calculateST(hi, lo, cl);

                    int count = ST.Count;

                    Series sttpUp1 = new Series(count);
                    Series sttpDn1 = new Series(count);
                    Series sttpUp2 = new Series(count);
                    Series sttpDn2 = new Series(count);

                    SlowTrigTPOp op = new SlowTrigTPOp();
                    double[] input = new double[3];
                    double[] output = new double[3];
                    for (int ii = 0; ii <= currentBarIndex + 1; ii++)
                    {
                        output[0] = double.NaN;
                        output[1] = double.NaN;
                        if (!double.IsNaN(hi[ii]) && !double.IsNaN(lo[ii]) && !double.IsNaN(cl[ii]))
                        {
                            input[0] = hi[ii];
                            input[1] = lo[ii];
                            input[2] = cl[ii];

                            op.Calculate(input, output, ii == count - 1);
                        }

                        double up = double.IsNaN(output[0]) ? output[1] : output[0];
                        double dn = double.IsNaN(output[1]) ? output[0] : output[1];

                        bool display = (historyEnable || ii >= currentBarIndex);

                        bool stUp = ii >= 2 && ST[ii - 1] < ST[ii - 2];
                        bool stDn = ii >= 2 && ST[ii - 1] > ST[ii - 2];

                        if (stUp && up > 0)
                        {
                            sttpUp1[ii] = display ? up : double.NaN;
                            sttpUp2[ii] = up;
                        }
                        if (stDn && dn > 0)
                        {
                            sttpDn1[ii] = display ? dn : double.NaN;
                            sttpDn2[ii] = dn;
                        }
                    }

                    if (estEnable)
                    {
                        hi[currentBarIndex + 1] = double.NaN;
                        lo[currentBarIndex + 1] = double.NaN;
                        cl[currentBarIndex + 1] = double.NaN;
                    }

                    var STTPUp1 = (indicators["Short Term ST Current"].Parameters["STTPUp1"] as ColorParameter).Value;
                    var STTPDn1 = (indicators["Short Term ST Current"].Parameters["STTPDn1"] as ColorParameter).Value;

                    List<Color> upColorList1 = new List<Color>();
                    upColorList1.Add(STTPUp1);

                    List<Color> dnColorList1 = new List<Color>();
                    dnColorList1.Add(STTPDn1);

                    var STTPUp2 = (indicators["Short Term ST Nxt Bar"].Parameters["STTPUp2"] as ColorParameter).Value;
                    var STTPDn2 = (indicators["Short Term ST Nxt Bar"].Parameters["STTPDn2"] as ColorParameter).Value;

                    List<Color> upColorList2 = new List<Color>();
                    upColorList2.Add(STTPUp2);

                    List<Color> dnColorList2 = new List<Color>();
                    dnColorList2.Add(STTPDn2);

                    update = true;

                    _chart.Panels[mainPanelIndex].RemoveCurve("STTPUp1");
                    _chart.Panels[mainPanelIndex].RemoveCurve("STTPDn1");
                    _chart.Panels[mainPanelIndex].RemoveCurve("STTPUp2");
                    _chart.Panels[mainPanelIndex].RemoveCurve("STTPDn2");

                    _chart.Panels[mainPanelIndex].AddCurve("STTPUp1", upColorList1, 1.5);
                    _chart.Panels[mainPanelIndex].LoadCurveValues1("STTPUp1", sttpUp1.Data);
                    _chart.Panels[mainPanelIndex].SetCurveType("STTPUp1", CurveType.Dash);
                    _chart.Panels[mainPanelIndex].SetCurveScalable("STTPUp1", false);

                    _chart.Panels[mainPanelIndex].AddCurve("STTPDn1", dnColorList1, 1.5);
                    _chart.Panels[mainPanelIndex].LoadCurveValues1("STTPDn1", sttpDn1.Data);
                    _chart.Panels[mainPanelIndex].SetCurveType("STTPDn1", CurveType.Dash);
                    _chart.Panels[mainPanelIndex].SetCurveScalable("STTPDn1", false);

                    _chart.Panels[mainPanelIndex].AddCurve("STTPUp2", upColorList2, 0);
                    _chart.Panels[mainPanelIndex].LoadCurveValues1("STTPUp2", sttpUp2.Data);
                    _chart.Panels[mainPanelIndex].SetCurveType("STTPUp2", CurveType.None);
                    _chart.Panels[mainPanelIndex].SetCurveScalable("STTPUp2", false);
                    _chart.Panels[mainPanelIndex].SetCurveLegend("STTPUp2", "ST");

                    _chart.Panels[mainPanelIndex].AddCurve("STTPDn2", dnColorList2, 0);
                    _chart.Panels[mainPanelIndex].LoadCurveValues1("STTPDn2", sttpDn2.Data);
                    _chart.Panels[mainPanelIndex].SetCurveType("STTPDn2", CurveType.None);
                    _chart.Panels[mainPanelIndex].SetCurveScalable("STTPDn2", false);
                    _chart.Panels[mainPanelIndex].SetCurveLegend("STTPDn2", "ST");
                }
            }

            if (!update)
            {
                _chart.Panels[mainPanelIndex].RemoveCurve("STTPUp1");
                _chart.Panels[mainPanelIndex].RemoveCurve("STTPDn1");
                _chart.Panels[mainPanelIndex].RemoveCurve("STTPUp2");
                _chart.Panels[mainPanelIndex].RemoveCurve("STTPDn2");
            }
        }

        public void calculateTRTTurningPoint(List<DateTime> times, Series[] series, int currentBarIndex, bool currentEnable, bool historyEnable, bool estEnable)
        {
            int mainPanelIndex = _chart.GetPanelIndex("Main");

            if ((currentEnable || historyEnable) && series != null)
            {
                Series hi = series[1];
                Series lo = series[2];
                Series cl = series[3];

                if (hi != null && lo != null && cl != null)
                {
                    if (estEnable)
                    {
                        double lastCl = cl[currentBarIndex];
                        hi[currentBarIndex + 1] = lastCl;
                        lo[currentBarIndex + 1] = lastCl;
                        cl[currentBarIndex + 1] = lastCl;
                    }

                    Series TRT = atm.calculateTRT(cl);

                    int count = TRT.Count;

                    Series trttpUp1 = new Series(count);
                    Series trttpDn1 = new Series(count);
                    Series trttpUp2 = new Series(count);
                    Series trttpDn2 = new Series(count);

                    TRTTPOp op = new TRTTPOp();
                    double[] input = new double[3];
                    double[] output = new double[3];
                    for (int ii = 0; ii < count; ii++)
                    {
                        output[0] = double.NaN;
                        output[1] = double.NaN;
                        if (!double.IsNaN(hi[ii]) && !double.IsNaN(lo[ii]) && !double.IsNaN(cl[ii]))
                        {
                            input[0] = hi[ii];
                            input[1] = lo[ii];
                            input[2] = cl[ii];
                            op.Calculate(input, output, ii == count - 1);
                        }

                        double up = double.IsNaN(output[0]) ? output[1] : output[0];
                        double dn = double.IsNaN(output[1]) ? output[0] : output[1];

                        bool display = (historyEnable || ii >= currentBarIndex);

                        if (ii >= 2 && TRT[ii - 1] < TRT[ii - 2] && up > 0)
                        {
                            trttpUp1[ii] = display ? up : double.NaN;
                            trttpUp2[ii] = up;
                        }
                        else if (ii >= 2 && TRT[ii - 1] > TRT[ii - 2] && dn > 0)
                        {
                            trttpDn1[ii] = display ? dn : double.NaN;
                            trttpDn2[ii] = dn;
                        }
                    }

                    if (estEnable)
                    {
                        hi[currentBarIndex + 1] = double.NaN;
                        lo[currentBarIndex + 1] = double.NaN;
                        cl[currentBarIndex + 1] = double.NaN;
                    }

                    List<Color> upColorList = new List<Color>();
                    upColorList.Add(Colors.Cyan);

                    List<Color> dnColorList = new List<Color>();
                    dnColorList.Add(Colors.Yellow);

                    _chart.Panels[mainPanelIndex].AddCurve("TRTTPUp1", upColorList, 2.5);
                    _chart.Panels[mainPanelIndex].LoadCurveValues1("TRTTPUp1", trttpUp1.Data);
                    _chart.Panels[mainPanelIndex].SetCurveType("TRTTPUp1", CurveType.Dash);
                    _chart.Panels[mainPanelIndex].SetCurveScalable("TRTTPUp1", false);

                    _chart.Panels[mainPanelIndex].AddCurve("TRTTPDn1", dnColorList, 2.5);
                    _chart.Panels[mainPanelIndex].LoadCurveValues1("TRTTPDn1", trttpDn1.Data);
                    _chart.Panels[mainPanelIndex].SetCurveType("TRTTPDn1", CurveType.Dash);
                    _chart.Panels[mainPanelIndex].SetCurveScalable("TRTTPDn1", false);

                    _chart.Panels[mainPanelIndex].AddCurve("TRTTPUp2", upColorList, 0);
                    _chart.Panels[mainPanelIndex].LoadCurveValues1("TRTTPUp2", trttpUp2.Data);
                    _chart.Panels[mainPanelIndex].SetCurveType("TRTTPUp2", CurveType.None);
                    _chart.Panels[mainPanelIndex].SetCurveScalable("TRTTPUp2", false);
                    _chart.Panels[mainPanelIndex].SetCurveLegend("TRTTPUp2", "TRT");

                    _chart.Panels[mainPanelIndex].AddCurve("TRTTPDn2", dnColorList, 0);
                    _chart.Panels[mainPanelIndex].LoadCurveValues1("TRTTPDn2", trttpDn2.Data);
                    _chart.Panels[mainPanelIndex].SetCurveType("TRTTPDn2", CurveType.None);
                    _chart.Panels[mainPanelIndex].SetCurveScalable("TRTTPDn2", false);
                    _chart.Panels[mainPanelIndex].SetCurveLegend("TRTTPDn2", "TRT");
                }
            }
            else
            {
                _chart.Panels[mainPanelIndex].RemoveCurve("TRTTPUp1");
                _chart.Panels[mainPanelIndex].RemoveCurve("TRTTPDn1");
                _chart.Panels[mainPanelIndex].RemoveCurve("TRTTPUp2");
                _chart.Panels[mainPanelIndex].RemoveCurve("TRTTPDn2");
            }
        }

        public void calculateTrendLineStudy(List<DateTime> times, Series[] series, bool enable, Dictionary<string, Parameter> parameters)
        {
            int mainPanelIndex = _chart.GetPanelIndex("Main");

            if (enable && series != null)
            {
                Series hi = series[1];
                Series lo = series[2];
                Series cl = series[3];

                if (hi != null && lo != null && cl != null)
                {
                    List<Series> TL = atm.calculateTrendLines(hi, lo, cl, 3);

                    var tlUpColor = (parameters["TLup1"] as ColorParameter).Value;
                    var tlDnColor = (parameters["TLdn1"] as ColorParameter).Value;

                    List<Color> upColorList = new List<Color>();
                    upColorList.Add(tlUpColor);

                    List<Color> dnColorList = new List<Color>();
                    dnColorList.Add(tlDnColor);

                    _chart.Panels[mainPanelIndex].AddCurve("TLup1", upColorList, 2.5);
                    _chart.Panels[mainPanelIndex].SetCurveTooltip("TLup1", "");
                    _chart.Panels[mainPanelIndex].LoadCurveValues1("TLup1", TL[0].Data);
                    _chart.Panels[mainPanelIndex].SetCurveLegend("TLup1", "Tu");

                    _chart.Panels[mainPanelIndex].AddCurve("TLdn1", dnColorList, 2);
                    _chart.Panels[mainPanelIndex].SetCurveTooltip("TLdn1", "");
                    _chart.Panels[mainPanelIndex].LoadCurveValues1("TLdn1", TL[1].Data);
                    _chart.Panels[mainPanelIndex].SetCurveLegend("TLdn1", "Td");
                }
            }

            else
            {
                _chart.Panels[mainPanelIndex].RemoveCurve("TLup1");
                _chart.Panels[mainPanelIndex].RemoveCurve("TLdn1");
            }
        }

        public void calculateTrendBarStudy(List<DateTime> times, Series[] series, bool enable, Dictionary<string, Parameter> parameters)
        {
            lock (_chart.ColorList)
            {
                if (enable && series != null)
                {
                    Series hi = series[1];
                    Series lo = series[2];
                    Series cl = series[3];

                    if (hi != null && lo != null && cl != null)
                    {
                        List<Series> TDI = atm.calculateTDI(hi, lo);
                        Series up = (lo > TDI[1]).And(cl > TDI[0]).Replace(double.NaN, 0);
                        Series dn = (hi < TDI[0]).And(cl < TDI[1]).Replace(double.NaN, 0);

                        var tbUpColor = (parameters["TDIup"] as ColorParameter).Value;
                        var tbDnColor = (parameters["TDIdn"] as ColorParameter).Value;

                        int count = up.Count;
                        for (int ii = 0; ii < count; ii++)
                        {
                            _chart.ColorIndex.Add((up[ii] == 1) ? 2 : ((dn[ii] == 1) ? 1 : 0));
                        }
                        _chart.ColorList.Add(Colors.Transparent);
                        _chart.ColorList.Add(tbDnColor);
                        _chart.ColorList.Add(tbUpColor);
                    }
                }
            }
        }

        public void clearBarColors()
        {
            lock (_chart.ColorList)
            {
                _chart.ColorList.Clear();
                _chart.ColorIndex.Clear();
            }
        }

        public void calculateSCBarStudy(Series score, Series rp, bool enable, Dictionary<string, Parameter> parameters, bool rev)
        {
            lock (_chart.ColorList)
            {
                if (enable)
                {
                    Series SC = atm.calculateSCSig(score, rp, 2);

                    var scUpColor = (parameters["SCBUp"] as ColorParameter).Value;
                    var scDnColor = (parameters["SCBDn"] as ColorParameter).Value;

                    int count = SC.Count;
                    for (int ii = 0; ii < count; ii++)
                    {
                        var sc = rev ? -SC[ii] : SC[ii];
                        _chart.ColorIndex.Add((sc == 1) ? 2 : ((sc == -1) ? 1 : 0));
                    }
                    _chart.ColorList.Add(Colors.Transparent);
                    _chart.ColorList.Add(scDnColor);
                    _chart.ColorList.Add(scUpColor);
                }
            }
        }

        public void calculateTargetLineStudy(List<DateTime> times, Series[] series, bool enable, int currentIndex, Dictionary<string, Parameter> parameters)
        {
            int mainPanelIndex = _chart.GetPanelIndex("Main");

            if (enable && series != null)
            {
                Series hi = series[1];
                Series lo = series[2];
                Series cl = series[3];

                if (hi != null && lo != null && cl != null)
                {
                    Series md = Series.Mid(hi, lo);
                    Dictionary<string, Series> tl = atm.calculateTrend(hi, lo, cl, md);
                    Series TLup = tl["TLup"];
                    Series TLdn = tl["TLdn"];

                    List<Series> ul = atm.calculateUpperTargetLines(hi, lo, TLup, currentIndex);
                    List<Series> ll = atm.calculateLowerTargetLines(hi, lo, TLdn, currentIndex);

                    var targetUpColor = (parameters["TargetUp"] as ColorParameter).Value;
                    var targetDnColor = (parameters["TargetDn"] as ColorParameter).Value;

                    List<Color> colorListUp1 = new List<Color>();
                    colorListUp1.Add(targetUpColor);
                    List<Color> colorListDn1 = new List<Color>();
                    colorListDn1.Add(targetDnColor);

                    for (int ii = 4; ii >= 0; ii--)
                    {
                        string name1 = "TargetUp" + (ii + 1).ToString();
                        _chart.Panels[mainPanelIndex].AddCurve(name1, colorListUp1, 1.0);
                        _chart.Panels[mainPanelIndex].SetCurveType(name1, CurveType.Dash);
                        _chart.Panels[mainPanelIndex].SetCurveTooltip(name1, "Target Up Level " + (ii + 1).ToString());
                        _chart.Panels[mainPanelIndex].SetCurveLegend(name1, "T" + (ii + 1).ToString());
                        _chart.Panels[mainPanelIndex].LoadCurveValues1(name1, ul[ii].Data);
                        _chart.Panels[mainPanelIndex].SetLegendColor(name1, Colors.Red);
                    }

                    for (int ii = 0; ii < 5; ii++)
                    {
                        string name2 = "TargetDn" + (ii + 1).ToString();
                        _chart.Panels[mainPanelIndex].AddCurve(name2, colorListDn1, 1.0);
                        _chart.Panels[mainPanelIndex].SetCurveType(name2, CurveType.Dash);
                        _chart.Panels[mainPanelIndex].SetCurveTooltip(name2, "Target Dn Level " + (ii + 1).ToString());
                        _chart.Panels[mainPanelIndex].SetCurveLegend(name2, "T" + (ii + 1).ToString());
                        _chart.Panels[mainPanelIndex].LoadCurveValues1(name2, ll[ii].Data);
                        _chart.Panels[mainPanelIndex].SetLegendColor(name2, Colors.Lime);
                    }
                }
            }
            else
            {
                _chart.Panels[mainPanelIndex].RemoveCurve("TargetUp1");
                _chart.Panels[mainPanelIndex].RemoveCurve("TargetUp2");
                _chart.Panels[mainPanelIndex].RemoveCurve("TargetUp3");
                _chart.Panels[mainPanelIndex].RemoveCurve("TargetUp4");
                _chart.Panels[mainPanelIndex].RemoveCurve("TargetUp5");
                _chart.Panels[mainPanelIndex].RemoveCurve("TargetDn1");
                _chart.Panels[mainPanelIndex].RemoveCurve("TargetDn2");
                _chart.Panels[mainPanelIndex].RemoveCurve("TargetDn3");
                _chart.Panels[mainPanelIndex].RemoveCurve("TargetDn4");
                _chart.Panels[mainPanelIndex].RemoveCurve("TargetDn5");
            }
        }

        public void calculateFTChartLineStudy(List<DateTime> times, Series[] series, bool enable, int level, int currentIndex, bool shortTerm)
        {
            int mainPanelIndex = _chart.GetPanelIndex("Main");

            string upName = "FTChartLineUp" + level.ToString();
            string dnName = "FTChartLineDn" + level.ToString();

            if (enable && series != null)
            {
                List<ChartLine> upLines = new List<ChartLine>();
                List<ChartLine> dnLines = new List<ChartLine>();

                Series op = series[0];
                Series hi = series[1];
                Series lo = series[2];
                Series cl = series[3];

                if (hi != null && lo != null && cl != null)
                {
                    Series FT = atm.calculateFT(hi, lo, cl);

                    int count = Math.Min(times.Count, FT.Count);

                    Series ftUpLines = new Series(count);
                    Series ftDnLines = new Series(count);

                    Series pAlert = null;
                    if (!shortTerm)
                    {
                        pAlert = atm.calculatePressureAlert(op, hi, lo, cl);  // if "FT Lines on ST" no P Alert
                    }

                    atm.calculateFTLinesOB(FT, hi, currentIndex, ref ftUpLines, ref ftDnLines);
                    atm.calculateFTLinesOS(FT, lo, currentIndex, ref ftUpLines, ref ftDnLines);

                    int startIndex = -1;
                    double startValue = double.NaN;
                    double pAlertValue = 0;
                    for (int ii = 0; ii < count; ii++)
                    {
                        if (double.IsNaN(startValue))
                        {
                            if (!double.IsNaN(ftUpLines[ii]))
                            {
                                startValue = ftUpLines[ii];
                                startIndex = ii;
                                pAlertValue = shortTerm ? 0 : pAlert[ii];
                            }
                        }
                        else if (ftUpLines[ii] != startValue || (!double.IsNaN(startValue) && (shortTerm || (pAlert[ii] != pAlertValue))))
                        {
                            ChartLine line = new ChartLine(upName);
                            line.Point1 = new TimeValuePoint(times[startIndex], startValue);
                            DateTime endTime = times[ii];
                            line.Truncate = true;
                            line.Point2 = new TimeValuePoint(endTime, startValue);
                            line.Color = shortTerm ? Colors.Green : Colors.Cyan;
                            line.Thickness = (pAlertValue != 0) ? 8.0 : (shortTerm ? 4.0 : 2.0);    // if "FT Lines on ST" no P Alert and change thickness to 4
                            upLines.Add(line);
                            startValue = ftUpLines[ii];
                            startIndex = ii;
                            pAlertValue = shortTerm ? 0 : pAlert[ii];
                        }
                    }

                    startIndex = -1;
                    startValue = double.NaN;
                    pAlertValue = 0;
                    for (int ii = 0; ii < count; ii++)
                    {
                        if (double.IsNaN(startValue))
                        {
                            if (!double.IsNaN(ftDnLines[ii]))
                            {
                                startValue = ftDnLines[ii];
                                startIndex = ii;
                                pAlertValue = shortTerm ? 0 : pAlert[ii];
                            }
                        }
                        else if (ftDnLines[ii] != startValue || (!double.IsNaN(startValue) && (shortTerm || (pAlert[ii] != pAlertValue))))
                        {
                            ChartLine line = new ChartLine(dnName);
                            line.Point1 = new TimeValuePoint(times[startIndex], startValue);
                            DateTime endTime = times[ii];
                            line.Truncate = true;
                            line.Point2 = new TimeValuePoint(endTime, startValue);
                            line.Color = shortTerm ? Colors.Red : Colors.Magenta;
                            line.Thickness = (pAlertValue != 0) ? 8.0 : (shortTerm ? 4.0 : 2.0);    // if "FT Lines on ST" no P Alert and change thickness to 4
                            dnLines.Add(line);
                            startValue = ftDnLines[ii];
                            startIndex = ii;
                            pAlertValue = shortTerm ? 0 : pAlert[ii];
                        }
                    }
                    _chart.Panels[mainPanelIndex].ClearTrendLines(upName);
                    _chart.Panels[mainPanelIndex].ClearTrendLines(dnName);

                    _chart.Panels[mainPanelIndex].AddTrendLines(upLines);
                    _chart.Panels[mainPanelIndex].AddTrendLines(dnLines);
                }
                else
                {
                    _chart.Panels[mainPanelIndex].ClearTrendLines(upName);
                    _chart.Panels[mainPanelIndex].ClearTrendLines(dnName);

                }
            }
            else
            {
                _chart.Panels[mainPanelIndex].ClearTrendLines(upName);
                _chart.Panels[mainPanelIndex].ClearTrendLines(dnName);

            }
        }

        public void calculateMA200Study(List<DateTime> times, Series[] series, bool enable, Dictionary<string, Parameter> parameters)
        {
            int mainPanelIndex = _chart.GetPanelIndex("Main");

            if (enable && series != null)
            {

                Series cl = series[3];

                if (cl != null)
                {
                    Series ma = atm.calculateMovingAvg200(cl);

                    var ma200Color = (parameters["MA200"] as ColorParameter).Value;

                    List<Color> color = new List<Color>();
                    color.Add(ma200Color);

                    _chart.Panels[mainPanelIndex].AddCurve("MA 200", color, 2.0);
                    _chart.Panels[mainPanelIndex].LoadCurveValues1("MA 200", ma.Data);

                }
            }
            else
            {
                _chart.Panels[mainPanelIndex].RemoveCurve("MA 200");
            }
        }

        public void calculateMA100Study(List<DateTime> times, Series[] series, bool enable, Dictionary<string, Parameter> parameters)
        {
            int mainPanelIndex = _chart.GetPanelIndex("Main");

            if (enable && series != null)
            {

                Series cl = series[3];

                if (cl != null)
                {
                    Series ma = atm.calculateMovingAvg100(cl);

                    var ma100Color = (parameters["MA100"] as ColorParameter).Value;

                    List<Color> color = new List<Color>();
                    color.Add(ma100Color);

                    _chart.Panels[mainPanelIndex].AddCurve("MA 100", color, 2.0);
                    _chart.Panels[mainPanelIndex].LoadCurveValues1("MA 100", ma.Data);

                }
            }
            else
            {
                _chart.Panels[mainPanelIndex].RemoveCurve("MA 100");
            }
        }

        public void calculateMA50Study(List<DateTime> times, Series[] series, bool enable, Dictionary<string, Parameter> parameters)
        {
            int mainPanelIndex = _chart.GetPanelIndex("Main");

            if (enable && series != null)
            {

                Series cl = series[3];

                if (cl != null)
                {
                    Series ma = atm.calculateMovingAvg50(cl);

                    var ma50Color = (parameters["MA50"] as ColorParameter).Value;

                    List<Color> color = new List<Color>();
                    color.Add(ma50Color);

                    _chart.Panels[mainPanelIndex].AddCurve("MA 50", color, 2.0);
                    _chart.Panels[mainPanelIndex].LoadCurveValues1("MA 50", ma.Data);

                }
            }
            else
            {
                _chart.Panels[mainPanelIndex].RemoveCurve("MA 50");
            }
        }

        public void calculateADXStudy(string name, List<DateTime> times, Series[] series, bool enable, Dictionary<string, Parameter> parameters)
        {
            int panelIndex = _chart.GetPanelIndex("ADX");

            if (panelIndex >= 0)
            {
                if (enable && series != null)
                {
                    var periodParm = parameters["Period"] as NumberParameter;
                    var colorParm = parameters["Color"] as ColorParameter;

                    var period = periodParm.Value;

                    Series adx = atm.calculateADX(series[1], series[2], series[3], (int)period);

                    List<Color> color = new List<Color>();
                    color.Add(colorParm.Value);

                    _chart.Panels[panelIndex].AddCurve(name, color, 2.0);
                    _chart.Panels[panelIndex].LoadCurveValues1(name, adx.Data);

                    List<ChartLine> limits = new List<ChartLine>();
                    var count = times.Count;
                    if (count > 0)
                    {
                        ChartLine ob = new ChartLine("ADX");
                        ob.Point1 = new TimeValuePoint(times[0], 75);
                        ob.Point2 = new TimeValuePoint(times[count - 1], 75);
                        ob.Color = Colors.Red;
                        ob.Thickness = 0.5;
                        limits.Add(ob);
                        ChartLine os = new ChartLine("ADX");
                        os.Point1 = new TimeValuePoint(times[0], 25);
                        os.Point2 = new TimeValuePoint(times[count - 1], 25);
                        os.Color = Colors.Blue;
                        os.Thickness = 0.5;
                        limits.Add(os);

                        _chart.Panels[panelIndex].AddTrendLines(limits);
                    }
                }
                else
                {
                    _chart.Panels[panelIndex].RemoveCurve(name);
                }
            }
        }

        public void calculateATRStudy(string name, List<DateTime> times, Series[] series, bool enable, Dictionary<string, Parameter> parameters)
        {
            int panelIndex = _chart.GetPanelIndex("ATR");

            if (panelIndex >= 0)
            {
                if (enable && series != null)
                {
                    var maTypeParm = parameters["MAType"] as ChoiceParameter;
                    var curveTypeParm = parameters["DisplayType"] as ChoiceParameter;
                    var periodParm = parameters["Period"] as NumberParameter;
                    var colorParm = parameters["Color"] as ColorParameter;

                    var maType = maTypeParm.Value;
                    var curveTypeText = curveTypeParm.Value;
                    var period = periodParm.Value;

                    Series atr = atm.calculateATR(series[1], series[2], series[3], maType, (int)period);



                    List<Color> color = new List<Color>();
                    color.Add(colorParm.Value);

                    var curveType = CurveType.Line;
                    if (curveTypeText == "Histogram") curveType = CurveType.Histogram;

                    _chart.Panels[panelIndex].AddCurve(name, color, 1.0);
                    _chart.Panels[panelIndex].SetCurveType(name, curveType);
                    _chart.Panels[panelIndex].LoadCurveValues1(name, atr.Data);
                }
                else
                {
                    _chart.Panels[panelIndex].RemoveCurve(name);
                }
            }
        }


		string _portfolioModelName = "";
		Model _portfolioModel = null;
		Model getModel(string name)
		{
			if (name != _portfolioModelName)
			{
				_portfolioModelName = name;
				var path = @"models\Models\" + name;
				var data = MainView.LoadUserData(path);
				_portfolioModel = Model.load(data);
			}
			return _portfolioModel;
		}

		public void calculateATRXStudy(string name, string interval, List<DateTime> times, Series[] series, bool enable, Dictionary<string, Parameter> parameters, string portfolioName, List<Trade> trades)
		{
			int panelIndex = _chart.GetPanelIndex("Main");

			if (panelIndex >= 0)
			{
                if (enable && series != null)
                {
                    var model = getModel(portfolioName);

                    if (model == null || model.ATMAnalysisInterval == interval)
                    {
                        var maTypeParm = parameters["MAType"] as ChoiceParameter;
                        var multiplierParm = parameters["Multiplier"] as NumberParameter;
                        var periodParm = parameters["Period"] as NumberParameter;
                        var colorParm1 = parameters["BuyStop"] as ColorParameter;
                        var colorParm2 = parameters["SellStop"] as ColorParameter;

                        var maType = maTypeParm.Value;
                        var period = periodParm.Value;
                        var multiplier = multiplierParm.Value;

                        var high = series[1];
                        var low = series[2];
                        var close = series[3];
                        var count = close.Count;
                        trades.Reverse();
                        Series stops = new Series(count);
                        Series colors = new Series(count);
                        trades.ForEach(t =>
                        {
                            if (model != null)
                            {
                                var g1 = model.Groups.Where(g => g.Direction == t.Direction).ToList();
                                var group = g1.Where(g => g.AlphaSymbols.Select(s => s.Ticker).Contains(t.Ticker)).ToList().FirstOrDefault();
                                if (group != null)
                                {
                                    multiplier = group.ATRRiskFactor;
                                    period = group.ATRRiskPeriod;
                                }
                            }

                            Series atr = atm.calculateATR(high, low, close, maType, (int)period) * multiplier;

                            var ot = t.OpenDateTime;
                            var ct = t.CloseDateTime;
                            var ix1 = times.FindIndex(tm => tm == ot);
                            var ix2 = times.FindIndex(tm => tm == ct);
                            if (ix2 == -1) ix2 = count - 1;
                            var dir = t.Direction;

                            var stop1 = dir > 0 ? double.MinValue : double.MaxValue;
                            for (var ii = ix1; ii <= ix2; ii++)
                            {
                                var stop2 = dir < 0 ? high[ii - 1] + atr[ii - 1] : low[ii - 1] - atr[ii - 1];
                                stops[ii] = dir < 0 ? Math.Min(stop1, stop2) : Math.Max(stop1, stop2);
                                colors[ii] = dir < 0 ? 1 : 0;
                                stop1 = stops[ii];
                            }

                        });



                        //var direction = 0;
                        //               var stops = new Series(close.Count);
                        //               var colors = new Series(close.Count);
                        //var stop = double.NaN;
                        //for (var ii = 0; ii < close.Count; ii++)
                        //               {
                        //	if (direction != 0)
                        //	{
                        //		stops[ii] = stop;
                        //		colors[ii] = direction > 0 ? 0 : 1;
                        //	}

                        //	if (!double.IsNaN(atr[ii]))
                        //                   {
                        //		if (direction >= 0) // long
                        //		{
                        //                           if (close[ii] < stop)
                        //                           {
                        //                               direction = -1;
                        //                           }
                        //			stop = low[ii] - atr[ii];
                        //		}
                        //		if (direction <= 0)
                        //		{
                        //			if (close[ii] > stop)
                        //			{
                        //				direction = 1;
                        //			}
                        //			stop = high[ii] + atr[ii];
                        //		}
                        //	}
                        //}

                        List<Color> color = new List<Color>();
                        color.Add(colorParm1.Value);
                        color.Add(colorParm2.Value);

                        var curveType = CurveType.Dash;

                        var colorIndexes = colors.Data.Select(x => (int)x).ToList();

                        _chart.Panels[panelIndex].AddCurve(name, color, 2.0);
                        _chart.Panels[panelIndex].SetCurveType(name, curveType);
                        _chart.Panels[panelIndex].SetCurvesColorIndexes(name, colorIndexes);
                        _chart.Panels[panelIndex].LoadCurveValues1(name, stops.Data);
                        _chart.Panels[panelIndex].SetCurveLegend(name, "Ax");
                    }
                }
                else
                {
                    _chart.Panels[panelIndex].RemoveCurve(name);
                }
			}
		}


		public void calculateBOLStudy(string name, List<DateTime> times, Series[] series, bool enable, Dictionary<string, Parameter> parameters)
        {
            int mainPanelIndex = _chart.GetPanelIndex("Main");

            if (enable && series != null)
            {
                var typeParm = parameters["MAType"] as ChoiceParameter;
                var priceParm = parameters["Price"] as ChoiceParameter;
                var periodParm = parameters["Period"] as NumberParameter;
                var colorParmH = parameters["ColorH"] as ColorParameter;
                var colorParmL = parameters["ColorL"] as ColorParameter;
                var colorParmM = parameters["ColorM"] as ColorParameter;
                var stdDevParm = parameters["StdDev"] as NumberParameter;

                var type = typeParm.Value;

                var price = Conditions.getPrice(priceParm.Value, series[0], series[1], series[2], series[3]);

                var period = periodParm.Value;

                var results = atm.calculateBollinger(type, price, (int)period, stdDevParm.Value);
                var ma = results[0];
                var ub = results[1];
                var lb = results[2];

                List<Color> colorH = new List<Color>();
                colorH.Add(colorParmH.Value);

                List<Color> colorL = new List<Color>();
                colorL.Add(colorParmL.Value);

                List<Color> colorM = new List<Color>();
                colorM.Add(colorParmM.Value);

                _chart.Panels[mainPanelIndex].AddCurve("MA", colorM, 1.0);
                _chart.Panels[mainPanelIndex].AddCurve("High", colorH, 1.0);
                _chart.Panels[mainPanelIndex].AddCurve("Low", colorL, 1.0);
                _chart.Panels[mainPanelIndex].LoadCurveValues1("MA", ma.Data);
                _chart.Panels[mainPanelIndex].LoadCurveValues1("High", ub.Data);
                _chart.Panels[mainPanelIndex].LoadCurveValues1("Low", lb.Data);

            }
            else
            {
                _chart.Panels[mainPanelIndex].RemoveCurve("MA");
                _chart.Panels[mainPanelIndex].RemoveCurve("High");
                _chart.Panels[mainPanelIndex].RemoveCurve("Low");
            }
        }

        public void calculateDMIStudy(string name, List<DateTime> times, Series[] series, bool enable, Dictionary<string, Parameter> parameters)
        {
            int panelIndex = _chart.GetPanelIndex("DMI");

            if (panelIndex >= 0)
            {
                if (enable && series != null)
                {
                    var maTypeParm = parameters["MAType"] as ChoiceParameter;
                    var periodParm = parameters["Period"] as NumberParameter;
                    var colorUpParm = parameters["ColorUp"] as ColorParameter;
                    var colorDnParm = parameters["ColorDn"] as ColorParameter;

                    var maType = maTypeParm.Value;
                    var period = periodParm.Value;

                    List<Series> dmi = atm.calculateDMI(series[1], series[2], series[3], maType, (int)period);

                    List<Color> colorUp = new List<Color>();
                    colorUp.Add(colorUpParm.Value);

                    List<Color> colorDn = new List<Color>();
                    colorDn.Add(colorDnParm.Value);

                    _chart.Panels[panelIndex].AddCurve("DMIUp", colorUp, 1.0);
                    _chart.Panels[panelIndex].AddCurve("DMIDn", colorDn, 1.0);
                    _chart.Panels[panelIndex].LoadCurveValues1("DMIUp", dmi[0].Data);
                    _chart.Panels[panelIndex].LoadCurveValues1("DMIDn", dmi[1].Data);
                }
                else
                {
                    _chart.Panels[panelIndex].RemoveCurve(name);
                }
            }
        }

        public void calculateDDIFStudy(string name, List<DateTime> times, Series[] series, bool enable, Dictionary<string, Parameter> parameters)
        {
            int panelIndex = _chart.GetPanelIndex("D DIF");

            if (panelIndex >= 0)
            {
                if (enable && series != null)
                {
                    var maTypeParm = parameters["MAType"] as ChoiceParameter;
                    var periodParm = parameters["Period"] as NumberParameter;
                    var colorParm = parameters["Color"] as ColorParameter;

                    var maType = maTypeParm.Value;
                    var period = periodParm.Value;

                    Series dmi = atm.calculateDDIF(series[1], series[2], series[3], maType, (int)period);

                    List<Color> color = new List<Color>();
                    color.Add(colorParm.Value);

                    _chart.Panels[panelIndex].AddCurve("DMI", color, 1.0);
                    _chart.Panels[panelIndex].LoadCurveValues1("DMI", dmi.Data);
                }
                else
                {
                    _chart.Panels[panelIndex].RemoveCurve(name);
                }
            }
        }

        public void calculateMAStudy(string name, List<DateTime> times, Series[] series, bool enable, Dictionary<string, Parameter> parameters)
        {
            int mainPanelIndex = _chart.GetPanelIndex("Main");

            if (enable && series != null)
            {
                var typeParm = parameters["Type"] as ChoiceParameter;
                var priceParm = parameters["Price"] as ChoiceParameter;
                var periodParm = parameters["Period"] as NumberParameter;
                var colorParm = parameters["Color"] as ColorParameter;

                var type = typeParm.Value;

                var price = Conditions.getPrice(priceParm.Value, series[0], series[1], series[2], series[3]);

                var period = periodParm.Value;

                if (price != null)
                {
                    Series ma = atm.calculateMovingAvg(type, price, (int)period);

                    List<Color> color = new List<Color>();
                    color.Add(colorParm.Value);

                    _chart.Panels[mainPanelIndex].AddCurve(name, color, 1.0);
                    _chart.Panels[mainPanelIndex].LoadCurveValues1(name, ma.Data);

                }
            }
            else
            {
                _chart.Panels[mainPanelIndex].RemoveCurve(name);
            }
        }

        public void calculateMACDStudy(string name, List<DateTime> times, Series[] series, bool enable, Dictionary<string, Parameter> parameters)
        {
            int panelIndex = _chart.GetPanelIndex("MACD");

            if (panelIndex >= 0)
            {
                if (enable && series != null)
                {
                    var priceParm = parameters["Price"] as ChoiceParameter;
                    var maTypeParm = parameters["MAType"] as ChoiceParameter;
                    var curveTypeParm = parameters["DisplayType"] as ChoiceParameter;
                    var periodParm1 = parameters["Period1"] as NumberParameter;
                    var periodParm2 = parameters["Period2"] as NumberParameter;
                    var periodParm3 = parameters["Period3"] as NumberParameter;
                    var colorParm1 = parameters["Color1"] as ColorParameter;
                    var colorParm2 = parameters["Color2"] as ColorParameter;

                    var maType = maTypeParm.Value;
                    var curveTypeText = curveTypeParm.Value;
                    var period1 = periodParm1.Value;
                    var period2 = periodParm2.Value;
                    var period3 = periodParm3.Value;

                    var price = Conditions.getPrice(priceParm.Value, series[0], series[1], series[2], series[3]);

                    Series ma1 = atm.calculateMovingAvg(maType, price, (int)period1);
                    Series ma2 = atm.calculateMovingAvg(maType, price, (int)period2);
                    Series osc = ma1 - ma2;
                    Series signal = atm.calculateMovingAvg(maType, osc, (int)period3);

                    List<Color> color1 = new List<Color>();
                    color1.Add(colorParm1.Value);

                    List<Color> color2 = new List<Color>();
                    color2.Add(colorParm2.Value);

                    var curveType = CurveType.Line;
                    if (curveTypeText == "Histogram") curveType = CurveType.Histogram;

                    _chart.Panels[panelIndex].AddCurve(name, color1, 1.0);
                    _chart.Panels[panelIndex].SetCurveType(name, curveType);
                    _chart.Panels[panelIndex].LoadCurveValues1(name, osc.Data);

                    _chart.Panels[panelIndex].AddCurve("Signal", color2, 1.0);
                    _chart.Panels[panelIndex].LoadCurveValues1("Signal", signal.Data);

                }
                else
                {
                    _chart.Panels[panelIndex].RemoveCurve(name);
                    _chart.Panels[panelIndex].RemoveCurve("Signal");
                }
            }
        }

        public void calculateMOMStudy(string name, List<DateTime> times, Series[] series, bool enable, Dictionary<string, Parameter> parameters)
        {
            int panelIndex = _chart.GetPanelIndex("MOM");
            if (panelIndex >= 0)
            {
                if (enable && series != null)
                {
                    var priceParm = parameters["Price"] as ChoiceParameter;
                    var periodParm = parameters["Period"] as NumberParameter;
                    var colorParm = parameters["Color"] as ColorParameter;

                    var price = priceParm.Value;
                    Series prices = series[3];
                    if (price == "Open") prices = series[0];
                    else if (price == "High") prices = series[1];
                    else if (price == "Low") prices = series[2];


                    var period = periodParm.Value;

                    if (prices != null)
                    {
                        Series mom = atm.calculateMOM(prices, (int)period);

                        List<Color> color = new List<Color>();
                        color.Add(colorParm.Value);

                        _chart.Panels[panelIndex].AddCurve(name, color, 1.0);
                        _chart.Panels[panelIndex].LoadCurveValues1(name, mom.Data);

                    }

                    List<ChartLine> limits = new List<ChartLine>();
                    var count = times.Count;
                    if (count > 0)
                    {
                        ChartLine ob = new ChartLine("MOM");
                        ob.Point1 = new TimeValuePoint(times[0], 25);
                        ob.Point2 = new TimeValuePoint(times[count - 1], 25);
                        ob.Color = Colors.Red;
                        ob.Thickness = 0.5;
                        limits.Add(ob);
                        ChartLine os = new ChartLine("MOM");
                        os.Point1 = new TimeValuePoint(times[0], -25);
                        os.Point2 = new TimeValuePoint(times[count - 1], -25);
                        os.Color = Colors.Blue;
                        os.Thickness = 0.5;
                        limits.Add(os);

                        _chart.Panels[panelIndex].AddTrendLines(limits);
                    }
                }
                else
                {
                    _chart.Panels[panelIndex].RemoveCurve(name);
                }
            }
        }

        public void calculateOSCStudy(string name, List<DateTime> times, Series[] series, bool enable, Dictionary<string, Parameter> parameters)
        {
            int panelIndex = _chart.GetPanelIndex("OSC");
            if (panelIndex >= 0)
            {

                if (enable && series != null)
                {
                    var type1 = parameters["Type1"] as ChoiceParameter;
                    var type2 = parameters["Type2"] as ChoiceParameter;
                    var priceType1 = parameters["Price1"] as ChoiceParameter;
                    var priceType2 = parameters["Price2"] as ChoiceParameter;
                    var period1 = parameters["Period1"] as NumberParameter;
                    var period2 = parameters["Period2"] as NumberParameter;
                    var colorParm = parameters["Color"] as ColorParameter;
                    var curveTypeParm = parameters["DisplayType"] as ChoiceParameter;

                    var price1 = Conditions.getPrice(priceType1.Value, series[0], series[1], series[2], series[3]);
                    var price2 = Conditions.getPrice(priceType2.Value, series[0], series[1], series[2], series[3]);

                    Series ma1 = atm.calculateMovingAvg(type1.Value, price1, (int)period1.Value);
                    Series ma2 = atm.calculateMovingAvg(type2.Value, price2, (int)period2.Value);
                    Series osc = ma1 - ma2;

                    List<Color> color = new List<Color>();
                    color.Add(colorParm.Value);

                    var curveType = CurveType.Line;
                    if (curveTypeParm.Value == "Histogram") curveType = CurveType.Histogram;

                    _chart.Panels[panelIndex].AddCurve(name, color, 1.0);
                    _chart.Panels[panelIndex].SetCurveType(name, curveType);
                    _chart.Panels[panelIndex].LoadCurveValues1(name, osc.Data);
                }
                else
                {
                    _chart.Panels[panelIndex].RemoveCurve(name);
                }
            }
        }

        public void calculateROCStudy(string name, List<DateTime> times, Series[] series, bool enable, Dictionary<string, Parameter> parameters)
        {
            int panelIndex = _chart.GetPanelIndex("ROC");

            if (panelIndex >= 0)
            {
                if (enable && series != null)
                {
                    var curveTypeParm = parameters["DisplayType"] as ChoiceParameter;
                    var periodParm = parameters["Period"] as NumberParameter;
                    var colorParm = parameters["Color"] as ColorParameter;

                    var priceParm = parameters["Price"] as ChoiceParameter;

                    var priceType = priceParm.Value;
                    Series price = Conditions.getPrice(priceType, series[0], series[1], series[2], series[3]);

                    var curveTypeText = curveTypeParm.Value;
                    var period = periodParm.Value;

                    Series roc = atm.calculateROC(price, (int)period);

                    List<Color> color = new List<Color>();
                    color.Add(colorParm.Value);

                    var curveType = CurveType.Line;
                    if (curveTypeText == "Histogram") curveType = CurveType.Histogram;

                    _chart.Panels[panelIndex].AddCurve(name, color, 1.0);
                    _chart.Panels[panelIndex].SetCurveType(name, curveType);

                    _chart.Panels[panelIndex].LoadCurveValues1(name, roc.Data);
                }
                else
                {
                    _chart.Panels[panelIndex].RemoveCurve(name);
                }
            }
        }

        public void calculateRSIStudy(string name, List<DateTime> times, Series[] series, bool enable, Dictionary<string, Parameter> parameters)
        {
            int panelIndex = _chart.GetPanelIndex("RSI");
            if (panelIndex >= 0)
            {
                if (enable && series != null)
                {
                    var priceParm = parameters["Price"] as ChoiceParameter;
                    var periodParm = parameters["Period"] as NumberParameter;
                    var colorParm = parameters["Color"] as ColorParameter;

                    var price = priceParm.Value;
                    Series prices = series[3];
                    if (price == "Open") prices = series[0];
                    else if (price == "High") prices = series[1];
                    else if (price == "Low") prices = series[2];


                    var period = periodParm.Value;

                    if (prices != null)
                    {
                        Series rsi = atm.calculateRSI(prices, (int)period);

                        List<Color> color = new List<Color>();
                        color.Add(colorParm.Value);

                        _chart.Panels[panelIndex].AddCurve(name, color, 1.0);
                        _chart.Panels[panelIndex].LoadCurveValues1(name, rsi.Data);

                    }

                    List<ChartLine> limits = new List<ChartLine>();
                    var count = times.Count;
                    if (count > 0)
                    {
                        ChartLine ob = new ChartLine("RSI"); // calculateRSIStudy
                        ob.Point1 = new TimeValuePoint(times[0], 75);
                        ob.Point2 = new TimeValuePoint(times[count - 1], 75);
                        ob.Color = Colors.Red;
                        ob.Thickness = 0.5;
                        limits.Add(ob);
                        ChartLine os = new ChartLine("RSI");  // calculateRSIStudy
                        os.Point1 = new TimeValuePoint(times[0], 25);
                        os.Point2 = new TimeValuePoint(times[count - 1], 25);
                        os.Color = Colors.Blue;
                        os.Thickness = 0.5;
                        limits.Add(os);

                        _chart.Panels[panelIndex].AddTrendLines(limits);
                    }
                }
                else
                {
                    _chart.Panels[panelIndex].RemoveCurve(name);
                }
            }
        }

        public void calculate3SigmaStudy(List<DateTime> times, Series[] series, bool enable, Dictionary<string, Parameter> parameters)
        {
            int mainPanelIndex = _chart.GetPanelIndex("Main");

            if (enable && series != null)
            {
                Series hi = series[1];
                Series lo = series[2];
                Series cl = series[3];

                if (hi != null && lo != null && cl != null)
                {
                    List<Series> ts = atm.calculate3Sigma(hi, lo, cl);

                    var sigUp3Color = (parameters["S3up3"] as ColorParameter).Value;
                    var sigUp2Color = (parameters["S3up2"] as ColorParameter).Value;
                    var sigUp1Color = (parameters["S3up1"] as ColorParameter).Value;
                    var sigDn1Color = (parameters["S3dn1"] as ColorParameter).Value;
                    var sigDn2Color = (parameters["S3dn2"] as ColorParameter).Value;
                    var sigDn3Color = (parameters["S3dn3"] as ColorParameter).Value;

                    List<Color> colorListUp1 = new List<Color>();
                    colorListUp1.Add(sigUp1Color);
                    List<Color> colorListDn1 = new List<Color>();
                    colorListDn1.Add(sigDn1Color);

                    List<Color> colorListUp2 = new List<Color>();
                    colorListUp2.Add(sigUp2Color);
                    List<Color> colorListDn2 = new List<Color>();
                    colorListDn2.Add(sigDn2Color);

                    List<Color> colorListUp3 = new List<Color>();
                    colorListUp3.Add(sigUp3Color);
                    List<Color> colorListDn3 = new List<Color>();
                    colorListDn3.Add(sigDn3Color);

                    _chart.Panels[mainPanelIndex].AddCurve("S3up3", colorListUp3, 1.0);
                    _chart.Panels[mainPanelIndex].LoadCurveValues1("S3up3", ts[0].Data);
                    _chart.Panels[mainPanelIndex].SetCurveLegend("S3up3", "S3");
                    _chart.Panels[mainPanelIndex].AddCurve("S3up2", colorListUp2, 1.0);
                    _chart.Panels[mainPanelIndex].LoadCurveValues1("S3up2", ts[1].Data);
                    _chart.Panels[mainPanelIndex].SetCurveLegend("S3up2", "S2");
                    _chart.Panels[mainPanelIndex].AddCurve("S3up1", colorListUp1, 1.0);
                    _chart.Panels[mainPanelIndex].LoadCurveValues1("S3up1", ts[2].Data);
                    _chart.Panels[mainPanelIndex].SetCurveLegend("S3up1", "S1");

                    _chart.Panels[mainPanelIndex].AddCurve("S3dn1", colorListDn1, 1.0);
                    _chart.Panels[mainPanelIndex].LoadCurveValues1("S3dn1", ts[3].Data);
                    _chart.Panels[mainPanelIndex].SetCurveLegend("S3dn1", "S1");
                    _chart.Panels[mainPanelIndex].AddCurve("S3dn2", colorListDn2, 1.0);
                    _chart.Panels[mainPanelIndex].LoadCurveValues1("S3dn2", ts[4].Data);
                    _chart.Panels[mainPanelIndex].SetCurveLegend("S3dn2", "S2");
                    _chart.Panels[mainPanelIndex].AddCurve("S3dn3", colorListDn3, 1.0);
                    _chart.Panels[mainPanelIndex].LoadCurveValues1("S3dn3", ts[5].Data);
                    _chart.Panels[mainPanelIndex].SetCurveLegend("S3dn3", "S3");
                }
            }
            else
            {
                _chart.Panels[mainPanelIndex].RemoveCurve("S3up1");
                _chart.Panels[mainPanelIndex].RemoveCurve("S3up2");
                _chart.Panels[mainPanelIndex].RemoveCurve("S3up3");
                _chart.Panels[mainPanelIndex].RemoveCurve("S3dn1");
                _chart.Panels[mainPanelIndex].RemoveCurve("S3dn2");
                _chart.Panels[mainPanelIndex].RemoveCurve("S3dn3");
            }
        }

        public void calculateTriggerStudy(List<DateTime> times, Series[] series, int currentBarIndex, bool enable, Dictionary<string, Parameter> parameters)
        {
            if (enable && series != null && times != null && times.Count > 0)
            {
                Series hi = series[1];
                Series lo = series[2];
                Series cl = series[3];

                if (hi != null && lo != null && cl != null && cl.Count != 0)
                {
                    var ftUpColor = (parameters["FTUp"] as ColorParameter).Value;
                    var ftDnColor = (parameters["FTDn"] as ColorParameter).Value;

                    List<Color> FTColors = new List<Color>();
                    FTColors.Add(ftUpColor);
                    FTColors.Add(ftDnColor);

                    Series FT = atm.calculateFT(hi, lo, cl);

					//Series FTTurnsUp = FT.TurnsUp();
					//Series FTTurnsDn = FT.TurnsDown();
					//int cnt_tu = 0;
					//int cnt_td = 0; 
     //               int cnt_tus = 0;
					//int cnt_tds = 0;
					//for (var ii = 1; ii < FT.Count; ii++)
     //               {
					//	if (FTTurnsUp[ii - 1] == 1)
					//	{
					//		var tu_roc = Math.Abs(FT[ii] - FT[ii - 1]);
     //                       if (tu_roc < 2)
     //                       {
     //                           cnt_tus++;
     //                       }
					//		cnt_tu++;
					//	}
					//	if (FTTurnsDn[ii - 1] == 1)
					//	{
					//		var td_roc = Math.Abs(FT[ii] - FT[ii - 1]);
     //                       if (td_roc < 2)
     //                       {
     //                           cnt_tds++;
     //                       }
					//		cnt_td++;
					//	}
					//}



					List<int> FTColorIndex = new List<int>();
                    int count = FT.Count;
                    for (int ii = 0; ii < count; ii++)
                    {
                        FTColorIndex.Add((ii > 0 && !double.IsNaN(FT[ii]) && !double.IsNaN(FT[ii - 1]) && FT[ii] > FT[ii - 1]) ? 0 : 1);
                    }

                    var stUpColor = (parameters["STUp"] as ColorParameter).Value;
                    var stDnColor = (parameters["STDn"] as ColorParameter).Value;

                    List<Color> STColors = new List<Color>();
                    STColors.Add(stDnColor);
                    STColors.Add(stUpColor);

                    Series ST = atm.calculateST(hi, lo, cl);
                    count = ST.Count;
                    for (int ii = currentBarIndex + 1; ii < count; ii++)
                    {
                        ST[ii] = double.NaN;
                    }

                    List<int> STColorIndex = new List<int>();
                    count = ST.Count;
                    for (int ii = 0; ii < count; ii++)
                    {
                        STColorIndex.Add((ii > 0 && !double.IsNaN(ST[ii]) && !double.IsNaN(ST[ii - 1]) && ST[ii] > ST[ii - 1]) ? 1 : 0);
                    }

                    Series EZI = atm.calculateEZI(cl);
                    Series TSB = atm.calculateTSB(hi, lo, cl);
                    Series upSet = (TSB > 30).And(ST > 75).And((ST.ShiftRight(6) < 20).Or(ST.ShiftRight(7) < 20));
                    Series dnSet = (TSB < 70).And(ST < 25).And((ST.ShiftRight(6) > 80).Or(ST.ShiftRight(7) > 80));
                    Series upRes = ST < 65;
                    Series dnRes = ST > 35;
                    Series Utsb = (EZI >= 80).And(TSB >= 70);
                    Series Dtsb = (EZI <= 20).And(TSB <= 30);
                    Series Uezi = (EZI <= 80).And(TSB >= 70);
                    Series Dezi = (EZI >= 20).And(TSB <= 30);
                    Series Uset = (TSB < 70) * atm.setReset(upSet, upRes);
                    Series Dset = (TSB > 30) * atm.setReset(dnSet, dnRes);

                    int upperLimit = 102;
                    int lowerLimit = -2;

                    var c1 = (parameters["TSBUp"] as ColorParameter).Value;
                    var c2 = (parameters["TSBDn"] as ColorParameter).Value;
                    var c3 = (parameters["Uezi"] as ColorParameter).Value;
                    var c4 = (parameters["Dezi"] as ColorParameter).Value;
                    var c5 = (parameters["Uset"] as ColorParameter).Value;
                    var c6 = (parameters["Dset"] as ColorParameter).Value;

                    List<ChartLine> TSBu = createTriggerLines(times, Utsb, upperLimit, c1, 3, 10, "Bullish ATM TSB (Trend Strength Bar)");
                    List<ChartLine> TSBd = createTriggerLines(times, Dtsb, lowerLimit, c2, 3, 10, "Bearish ATM TSB (Trend Strength Bar)");
                    List<ChartLine> EZIu = createTriggerLines(times, Uezi, upperLimit, c3, 3, 10, "Weak Bullish ATM TSB");
                    List<ChartLine> EZId = createTriggerLines(times, Dezi, lowerLimit, c4, 3, 10, "Weak Bearish ATM TSB");
                    List<ChartLine> SETu = createTriggerLines(times, Uset, upperLimit, c5, 3, 10, "Bullish Parallel up movement of FT and ST");
                    List<ChartLine> SETd = createTriggerLines(times, Dset, lowerLimit, c6, 3, 10, "Bearish Parallel down movement of FT and ST");

                    Series prsVal = (((TSB - 30) * ((double)upperLimit - lowerLimit) / (70 - 30)) + lowerLimit).ClipAbove(upperLimit).ClipBelow(lowerLimit);
                    Series prsDir = atm.calculatePressure(hi, lo, cl);
                    Series prsUp = (prsVal * Series.Equal(prsDir, 1)).Replace(0, double.NaN);
                    Series prsDn = (prsVal * Series.Equal(prsDir, -1)).Replace(0, double.NaN);

                    var PrsUpColor = (parameters["PrsUp"] as ColorParameter).Value;
                    var PrsDnColor = (parameters["PrsDn"] as ColorParameter).Value;

                    List<Color> PrsUpColors = new List<Color>();
                    PrsUpColors.Add(PrsUpColor);
                    List<Color> PrsDnColors = new List<Color>();
                    PrsDnColors.Add(PrsDnColor);

                    List<ChartLine> limits = new List<ChartLine>();
                    count = times.Count;
                    if (count > 0)
                    {
                        ChartLine ob = new ChartLine("TSB"); // calculateTriggerStudy
                        ob.Point1 = new TimeValuePoint(times[0], 75);
                        ob.Point2 = new TimeValuePoint(times[count - 1], 75);
                        ob.Color = Colors.Red;
                        ob.Thickness = 0.5;
                        limits.Add(ob);
                        ChartLine os = new ChartLine("TSB");  // calculateTriggerStudy
                        os.Point1 = new TimeValuePoint(times[0], 25);
                        os.Point2 = new TimeValuePoint(times[count - 1], 25);
                        os.Color = Colors.Blue;
                        os.Thickness = 0.5;
                        limits.Add(os);
                    }

                    int panelIndex = _chart.GetPanelIndex("TRIGGER");
                    if (panelIndex != -1)
                    {
                        _chart.Panels[panelIndex].ClearTrendLines("TSB");

                        _chart.Panels[panelIndex].AddTrendLines(TSBu); // zOrder = 10
                        _chart.Panels[panelIndex].AddTrendLines(TSBd); // zOrder = 10
                        _chart.Panels[panelIndex].AddTrendLines(EZIu); // zOrder = 10
                        _chart.Panels[panelIndex].AddTrendLines(EZId); // zOrder = 10
                        _chart.Panels[panelIndex].AddTrendLines(SETu); // zOrder = 10
                        _chart.Panels[panelIndex].AddTrendLines(SETd); // zOrder = 10

                        _chart.Panels[panelIndex].AddTrendLines(limits); // zOrder = 1

                        _chart.Panels[panelIndex].AddCurve("PrsUp", PrsUpColors, 1);
                        _chart.Panels[panelIndex].SetCurveTooltip("PrsUp", "ATM Pressure Line");
                        _chart.Panels[panelIndex].LoadCurveValues1("PrsUp", prsUp.Data);
                        _chart.Panels[panelIndex].SetCurveZOrder("PrsUp", 2);

                        _chart.Panels[panelIndex].AddCurve("PrsDn", PrsDnColors, 1);
                        _chart.Panels[panelIndex].SetCurveTooltip("PrsDn", "ATM Pressure Line");
                        _chart.Panels[panelIndex].LoadCurveValues1("PrsDn", prsDn.Data);
                        _chart.Panels[panelIndex].SetCurveZOrder("PrsDn", 2);

                        _chart.Panels[panelIndex].AddCurve("ST", STColors, 1.5);
                        _chart.Panels[panelIndex].SetCurveTooltip("ST", "ATM ST (Slow Trigger)");
                        _chart.Panels[panelIndex].LoadCurveValues1("ST", ST.Data);
                        _chart.Panels[panelIndex].SetCurvesColorIndexes("ST", STColorIndex);
                        _chart.Panels[panelIndex].SetCurveZOrder("ST", 3);

                        _chart.Panels[panelIndex].AddCurve("FT", FTColors, 2);
                        _chart.Panels[panelIndex].SetCurveTooltip("FT", "ATM FT (Fast Trigger)");
                        _chart.Panels[panelIndex].LoadCurveValues1("FT", FT.Data);
                        _chart.Panels[panelIndex].SetCurvesColorIndexes("FT", FTColorIndex);
                        _chart.Panels[panelIndex].SetCurveZOrder("FT", 4);
                    }
                }
            }
        }

        public void calculateElliottWaveOscillator(List<DateTime> times, Series[] series, bool enable, Dictionary<string, Parameter> parameters)
        {
            if (enable && series != null)
            {
                Series hi = series[1];
                Series lo = series[2];

                if (hi != null && lo != null)
                {
                    Series md = Series.Mid(hi, lo);

                    int period1 = ElliottAlgorithm.EW_PERIOD1;
                    int period2 = ElliottAlgorithm.EW_PERIOD2;
                    Series osc = md.osc(OscType.Difference, MAType.Simple, MAType.Simple, period1, period2);

                    int panelIndex = _chart.GetPanelIndex("OSCILLATOR");
                    if (panelIndex != -1)
                    {

                        var oscUpColor = (parameters["AboveZero"] as ColorParameter).Value;
                        var oscDnColor = (parameters["BelowZero"] as ColorParameter).Value;

                        List<Color> colorList1 = new List<Color>();
                        colorList1.Add(oscUpColor);
                        colorList1.Add(oscDnColor);

                        Series colorData = (osc < 0);
                        List<int> colors = new List<int>();
                        int count = colorData.Count;
                        for (int ii = 0; ii < count; ii++)
                        {
                            colors.Add((colorData.Data[ii] == 0) ? 0 : 1);
                        }

                        _chart.Panels[panelIndex].AddCurve("OSCILLATOR", colorList1, 1);
                        _chart.Panels[panelIndex].SetCurveType("OSCILLATOR", CurveType.Histogram);
                        _chart.Panels[panelIndex].LoadCurveValues1("OSCILLATOR", osc.Data);
                        _chart.Panels[panelIndex].SetCurvesColorIndexes("OSCILLATOR", colors);
                    }
                }
            }
        }

        public void updateHistogram(string title, int panelIndex, Color[] colors, List<double> data, List<int> colorIds)
        {
            _chart.Panels[panelIndex].Title = title;
            _chart.Panels[panelIndex].AddCurve(title, colors.ToList(), 1);
            _chart.Panels[panelIndex].SetCurveType(title, CurveType.Histogram);
            _chart.Panels[panelIndex].LoadCurveValues1(title, data);
            _chart.Panels[panelIndex].SetCurvesColorIndexes(title, colorIds);
        }

        public void calculatePredictionPriceLine(Senario scenario, List<DateTime> times, Series[] series, Dictionary<DateTime, double> predictions, Dictionary<DateTime, double> actuals, bool enable)
        {
            if (enable)
            {
                int panelIndex = _chart.GetPanelIndex("Main");
                if (panelIndex != -1)
                {
                    var label = MainView.GetSenarioLabel(scenario);

                    var offset = int.Parse(label.Substring(label.IndexOf("PX") + 2));

                    int pti = 3;
                    if (label[0] == 'O') pti = 0;
                    else if (label[1] == 'H') pti = 1;
                    else if (label[2] == 'L') pti = 2;

                    var data1 = new List<double>();
                    var color1 = new List<int>();

                    for (var ii = 0; ii < offset; ii++)
                    {
                        data1.Add(double.NaN);
                    }

                    for (int ii = 0; ii < times.Count; ii++)
                    {
                        var price = double.NaN;
                        if (ii >= offset)
                        {
                            if (predictions.ContainsKey(times[ii]))
                            {
                                var prediction = predictions[times[ii]];
                                var p1 = series[pti][ii - offset];
                                price = ((100 + prediction) / 100) * p1;
                            }
                        }
                        data1.Add(price);
                        color1.Add(0);
                    }

                    var colors = new List<Color>();
                    colors.Add(Colors.Yellow);
                    _chart.Panels[panelIndex].SetCurvesColorIndexes("Price", color1);
                    _chart.Panels[panelIndex].SetCurveColors("Price", colors);
                    _chart.Panels[panelIndex].SetCurveLegend("Price", "Pxf");
                    _chart.Panels[panelIndex].SetCurveType("Price", CurveType.Line);
                    _chart.Panels[panelIndex].LoadCurveValues1("Price", data1);
                }
            }
        }

        public string getPredictionText(Senario scenario, Series[] series, int currentBarIndex, bool up)
        {
            var label = MainView.GetSenarioLabel(scenario);
            var text1 = label.Trim();
            var index1 = text1.IndexOf("|");

            var output = text1;

            if (index1 != -1)
            {
                var op1 = series[0];
                var hi1 = series[1];
                var lo1 = series[2];
                var cl1 = series[3];

                var referencePrice = text1.Substring(index1 + 2, 1).Replace("O", "Open").Replace("H", "High").Replace("L", "Low").Replace("C", "Close");
                var referenceIndex = int.Parse(text1.Substring(text1.Length - 1, 1)) - 1;
                var forecastIndex = int.Parse(text1.Substring(index1 - 2, 1));

                var rp = cl1;
                if (referencePrice == "Open") rp = op1;
                else if (referencePrice == "High") rp = hi1;
                else if (referencePrice == "Low") rp = lo1;

                var rPrice = rp.ShiftRight(referenceIndex + forecastIndex);

                output = double.IsNaN(rPrice[currentBarIndex]) ? "" :  "Px " + (up ? ">" : "<") + " " + rPrice[currentBarIndex].ToString("0.00");
            }
            return output;
        }

        public void calculatePredictionOscillator(Senario scenario, List<DateTime> times, Series[] series, Dictionary<DateTime, double> predictions, Dictionary<DateTime, double> actuals, bool enable, int currentBarIndex)
        {
            if (enable)
            {
                int panelIndex = _chart.GetPanelIndex("PREDICTION");
                if (panelIndex != -1)
                {
                    _chart.Panels[panelIndex].ClearAnnotations();

                    var scenarioLabel = MainView.GetSenarioLabel(scenario);
                    var trendModel = scenarioLabel.Contains("Extreme");

                    var data1 = new List<double>();
                    var data2 = new List<double>();
                    var data3 = new List<double>();

                    var offset = 1;
                    var text1 = scenarioLabel.Trim();
                    var index1 = text1.IndexOf("|");
                    if (index1 != -1)
                    {
                        //var referenceIndex = int.Parse(text1.Substring(text1.Length - 1, 1));
                        var forecastIndex = int.Parse(text1.Substring(index1 - 2, 1));
                        offset = forecastIndex;
                    }

                    for (var ii = 0; ii < offset; ii++)
                    {
                        data1.Add(double.NaN);
                        data2.Add(double.NaN);
                    }

                    for (int ii = 0; ii < times.Count; ii++)
                    {
                        var value1 = double.NaN;
                        if (predictions.ContainsKey(times[ii]))
                        {
                            value1 = predictions[times[ii]];
                        }
                        data1.Add(value1);

                        var value2 = double.NaN;
                        if (actuals.ContainsKey(times[ii]))
                        {
                            value2 = actuals[times[ii]];
                        }
                        data2.Add(value2);

                        data3.Add(0);
                    }

                    List<Color> colorList1 = new List<Color>();
                    colorList1.Add(Colors.Lime);
                    colorList1.Add(Colors.Firebrick);
                    colorList1.Add(Colors.Yellow);
                    colorList1.Add(Colors.DarkOrange);
                    colorList1.Add(Colors.White);

                    List<int> colors = new List<int>();
                    int count = data1.Count;

                    if (trendModel)
                    { // trend forcast
                        for (int ii = 0; ii < count; ii++)
                        {
                            if (!double.IsNaN(data1[ii]))
                            {
                                var correct = (data1[ii] == data2[ii]);
                                var upPred = (data1[ii] > 0.5);
                                var forecast = (double.IsNaN(data2[ii]));
                                data1[ii] = upPred ? 1.0 : 0.7;
                                data3[ii] = upPred ? 0.0 : 0.3;
                                colors.Add(forecast ? 4 : (correct ? (upPred ? 0 : 0) : (upPred ? 1 : 1)));
                            }
                            else
                            {
                                colors.Add(0);
                                data1[ii] = 0;
                            }
                        }
                    }
                    else  // price forcast
                    {
                        for (int ii = 0; ii < count; ii++)
                        {
                            if (!double.IsNaN(data1[ii]))
                            {
                                var correct = (data1[ii] == data2[ii]);
                                var upPred = (data1[ii] > 0.5);
                                var forecast = (double.IsNaN(data2[ii]));
                                data1[ii] = (correct || forecast) ? upPred ? 1.0 : -1.0 : upPred ? 1 : -1;
                                colors.Add(forecast ? 4 : (correct ? (upPred ? 0 : 0) : (upPred ? 1 : 1)));
                            }
                            else
                            {
                                colors.Add(0);
                                data1[ii] = 0;
                            }
                        }
                    }

                    //_chart.Panels[panelIndex].Title = "PREDICTION" + "\n" + getPredictionText(scenario, series, currentBarIndex, data1[currentBarIndex + offset] > 0.5);
                    _chart.Panels[panelIndex].AddCurve("PREDICTION", colorList1, 1);
                    _chart.Panels[panelIndex].SetCurveType("PREDICTION", CurveType.Histogram);
                    _chart.Panels[panelIndex].LoadCurveValues1("PREDICTION", data1);
                    _chart.Panels[panelIndex].LoadCurveValues2("PREDICTION", data3);
                    _chart.Panels[panelIndex].SetCurvesColorIndexes("PREDICTION", colors);
                }
            }
        }

        public void calculateScoreOscillator(List<DateTime> times, Dictionary<DateTime, double> scores, bool enable)
        {
            if (enable)
            {
                int panelIndex = _chart.GetPanelIndex("Score");
                if (panelIndex != -1)
                {
                   
                    var data1 = new List<double>();
                    data1.Add(double.NaN);
                    for (int ii = 0; ii < times.Count; ii++)
                    {
                        var value = double.NaN;
                        if (scores.ContainsKey(times[ii]))
                        {
                            value = scores[times[ii]];
                        }
                        data1.Add(value);
                    }

                    _chart.Panels[panelIndex].AddCurve("Score", new List<Color>(), 1);
                    _chart.Panels[panelIndex].SetCurveType("Score", CurveType.Line);
                    _chart.Panels[panelIndex].LoadCurveValues1("Score", data1);
                }
            }
        }

        public void calculateTradeVelocity(string interval, List<DateTime> times, Series[] series, int level, bool enable)
        {
            if (enable && series != null)
            {
                Series hi = series[1];
                Series lo = series[2];
                Series cl = series[3];

                if (hi != null && lo != null && cl != null)
                {
                    Series FT = atm.calculateFT(hi, lo, cl);
                    Series ftUp = atm.turnsUpBelowLevel(FT, 100);
                    Series ftDn = atm.turnsDnAboveLevel(FT, 0);

                    int direction = 0;
                    int count = FT.Count;
                    int index = 0;
                    for (int ii = count - 1; ii >= 0 && direction == 0; ii--)
                    {
                        if (ftUp[ii] == 1)
                        {
                            direction = 1;
                            index = ii;
                        }
                        else if (ftDn[ii] == 1)
                        {
                            direction = -1;
                            index = ii;
                        }
                    }

                    List<ChartBezierShading> shading = createBezierShading(interval, direction, index, times, cl, level);

                    int mainPanelIndex = _chart.GetPanelIndex("Main");
                    _chart.Panels[mainPanelIndex].AddBezierShading(shading);
                }
            }
        }
		
		public void calculateForecast(string ticker, string interval, int index, List<DateTime> times, Series[] series, bool enableForecast, bool enableBoundaries)
		{
			int mainPanelIndex = _chart.GetPanelIndex("Main");
			_chart.Panels[mainPanelIndex].ClearTrendLines("Forecast");

            bool enable = enableForecast || enableBoundaries;

			if (enable && series != null)
			{

				var date = times[index];
				ForecastService.CalculateForecast(ticker, interval, times, series, date);
                var forecast = ForecastService.GetForecast(ticker, interval, date);

                var dates = forecast.X;

                if (dates != null && dates.Count > 0)
                {
                    var middle = forecast.Y["value"];
                    var upper = forecast.Y["upper"];
                    var lower = forecast.Y["lower"];

					List<ChartLine> lines = new List<ChartLine>();

					var cnt = dates.Count;
                    for (int ii = 0; ii < cnt - 1; ii++)
                    {
						var date1 = dates[ii];
						var date2 = dates[ii + 1];
						var mid1 = middle[ii];
                        var up1 = upper[ii];
                        var lo1 = lower[ii];
                        var mid2 = middle[ii + 1];
                        var up2 = upper[ii + 1];
                        var lo2 = lower[ii + 1];
                        if (enableForecast)
                        {
                            lines.Add(new ChartLine("Forecast")
                            {
                                Type = LineType.Segment,
                                Point1 = new TimeValuePoint(date1, mid1),
                                Point2 = new TimeValuePoint(date2, mid2),
                                Color = Colors.DodgerBlue,
                                Thickness = 2.0
                            });
                        }
                        if (enableBoundaries)
                        {
                            lines.Add(new ChartLine("Forecast")
                            {
                                Type = LineType.Segment,
                                Point1 = new TimeValuePoint(date1, up1),
                                Point2 = new TimeValuePoint(date2, up2),
                                Color = Colors.Yellow,
                                Thickness = 1.0
                            });
                            lines.Add(new ChartLine("Forecast")
                            {
                                Type = LineType.Segment,
                                Point1 = new TimeValuePoint(date1, lo1),
                                Point2 = new TimeValuePoint(date2, lo2),
                                Color = Colors.Yellow,
                                Thickness = 1.0
                            });
                        }
                    }
					_chart.Panels[mainPanelIndex].AddTrendLines(lines);
				}			
			}
		}

		private int getLeftOffset(Model model)
        {
            var offset = 0;
            try
            {
                var scenario = model.Scenario;
                var scenarioLabel = MainView.GetSenarioLabel(scenario);
                var fields = scenarioLabel.Split('|');
                var number = "";
                number += fields[1].Trim().Last();
                offset = int.Parse(number);
            }
            catch (Exception)
            {
            }
            return offset;
        }

        private int getRightOffset(Model model)
        {
            var offset = 1;
            try
            {
                var scenario = model.Scenario;
                var scenarioLabel = MainView.GetSenarioLabel(scenario);
                var fields = scenarioLabel.Split('|');
                var number = "";
                number += fields[0].Trim().Last();
                offset = int.Parse(number);
            }
            catch (Exception)
            {
            }
            return offset;
        }

        private string getMetric(Model model, string ticker, string interval)
        {
            var modelName = model.Name;

            bool useTicker = model.UseTicker;

            var path = @"senarios\" + modelName + @"\" + interval + (useTicker ? @"\" + ticker : "") + @"\result.csv";
            var results = MainView.LoadUserData(path);

            var fieldNumber = 1;
            if (model.MLRank == "ACCURACY") fieldNumber = 1;
            else if (model.MLRank == "AUC") fieldNumber = 2;
            else if (model.MLRank == "AUPRC") fieldNumber = 3;
            else if (model.MLRank == "F1-SCORE") fieldNumber = 4;

            var rows = results.Split(':');
            var row = (rows.Length > 0) ? rows[0] : "";
            var fields = row.Split(',');
            var metric = (fields.Length > fieldNumber) ? fields[fieldNumber] : "";
            return metric;
        }

        public List<Tuple<string, Color>> calculateSCAdd(List<DateTime> times, Series[] series, Series score, Series rp, int currentBarIndex, Dictionary<string, Indicator> indicators)
        {
            var output = new List<Tuple<string, Color>>();

            int id = 12345;
            int mainPanelIndex = _chart.GetPanelIndex("Main");
            if (mainPanelIndex >= 0)
            {
                var names = new string[] { "New Trend", "Pressure", "Add", "2 Bar", "Retrace", "Exh" };
                var enable = names.Aggregate(false, (o, n) => o || indicators[n].Enabled);
                if (enable)
                { 
                    var scSig = atm.calculateSCSig(score, rp, 2);

                    var newTrendEnb = indicators["New Trend"].Enabled;
                    var addEnb = indicators["Add"].Enabled;
                    var exhEnb = indicators["Exh"].Enabled;
                    var redEnb = indicators["Retrace"].Enabled;
                    var ptEnb = indicators["Pressure"].Enabled; 
                    var twobarEnb = indicators["2 Bar"].Enabled;

                    var op = series[0];
                    var hi = series[1];
                    var lo = series[2];
                    var cl = series[3];

                    Series ft = atm.calculateFT(hi, lo, cl);
                    Series exh = exhEnb ? atm.calculateExhaustion(hi, lo, cl, atm.ExhaustionLevelSelection.AllLevels) : new Series(cl.Count, 0);
                    Series pt = atm.calculatePressureAlert(op, hi, lo, cl); 
                    Series twobar = atm.calculateTwoBarPattern(op, hi, lo, cl, 0);

                    Series ftgu = ft.IsRising();
                    Series ftgd = ft.IsFalling();

					Series ftlt70 = ft < 70;
					Series ftgt30 = ft > 30;

					var signals = new Series(cl.Count, 0);
                    for (int ii = 1; ii < signals.Count; ii++) {
                        //if (scSig[ii] == 1 && (ftgu[ii] == 1 && ftgd[ii - 1] == 1 || scSig[ii - 1] == -1)) signals[ii] = 5; // new long
						if (scSig[ii] == 1 && (ftgu[ii] == 1 && ftgd[ii - 1] == 1 && ftlt70[ii] == 1)) signals[ii] = 5; //add one

						else if (scSig[ii] == 1 && exh[ii] == -1) signals[ii] = 2; // exit long
                        else if (scSig[ii] == 1 && ftgu[ii - 1] == 1 && ftgd[ii] == 1) signals[ii] = 3; // reduce long
                        else if (scSig[ii] == 1 && ftgu[ii] == 1) signals[ii] = 4; // stay long or exit long if fttp
                        else if (scSig[ii] == 1 && ftgd[ii] == 1) signals[ii] = 1; // enter long if fttp or waiting for long

						//else if (scSig[ii] == -1 && (ftgd[ii] == 1 && ftgu[ii - 1] == 1 || scSig[ii - 1] == 1)) signals[ii] = -5; // new short
						else if (scSig[ii] == -1 && (ftgd[ii] == 1 && ftgu[ii - 1] == 1 && ftgt30[ii] == 1)) signals[ii] = -5; // add on
						else if (scSig[ii] == -1 && exh[ii] == 1) signals[ii] = -2; // exit short
                        else if (scSig[ii] == -1 && ftgd[ii - 1] == 1 && ftgu[ii] == 1) signals[ii] = -3; // reduce short
                        else if (scSig[ii] == -1 && ftgd[ii] == 1) signals[ii] = -4; // stay short or exit short if fttp
                        else if (scSig[ii] == -1 && ftgu[ii] == 1) signals[ii] = -1; // enter short if fttp or waiting for short
                    }

                    var lng = false;
                    var sht = false;
                    var lng1 = false;
                    var sht1 = false;

                    _chart.Panels[mainPanelIndex].RemoveAnnotations(id);

                    for (var ii = 0; ii <= currentBarIndex; ii++)
                    {
                        lng1 = lng;
                        sht1 = sht;

                        var sig = signals[ii];

                        var l0 = scSig[ii - 1] == -1 && scSig[ii] == 1;
                        var l1 = ptEnb && pt[ii] > 0 && scSig[ii] == 1;
                        var l2 = addEnb && sig == 5;
                        var l3 = twobarEnb && twobar[ii] > 0 && scSig[ii] == 1;

                        var lentry = l0 || l1 || l2 || l3;
                        var lexit =  lng && (exhEnb && sig == 2 || scSig[ii - 1] == 1 && scSig[ii] == -1);

                        var s0 = scSig[ii - 1] == 1 && scSig[ii] == -1;
                        var s1 = ptEnb && pt[ii] < 0 && scSig[ii] == -1;
                        var s2 = addEnb && sig == -5;
                        var s3 = twobarEnb && twobar[ii] < 0 && scSig[ii] == -1;

                        var sentry = s0 || s1 || s2 || s3;
                        var sexit =  sht & (exhEnb && sig == -2 || scSig[ii - 1] == -1 && scSig[ii] == 1);
                        
                        lng = lentry ? true : lexit ? false : lng;
                        sht = sentry ? true : sexit ? false : sht;

                        var lreduce = redEnb && lng && sig == 3;
                        var sreduce = redEnb && sht && sig == -3;

                        var legendColor = Colors.Transparent;

                        var name = "";

                        if (lentry || lexit || sentry || sexit)
                        {  
                            var symbolType = (lentry || sentry) ? SymbolType.Circle : SymbolType.Square;

                            var tooltip = "New";
                            if (lentry || sentry) // circle
                            {
                                var up = l0 || l1 || l2 || l3;

                                if (l0 || s0) name = "New Trend";
                                else if (l2 || s2) name = "Add";
                                else if (l1 || s1) name = "Pressure";
                                else if (l3 || s3) name = "2 Bar";

                                if (l0 || s0) tooltip = "New Trend";
                                else if (l2 || s2) tooltip = "Add";
                                else if (l1 || s1) tooltip = "Pressure";
                                else if (l3 || s3) tooltip = "2 Bar";

                                legendColor = _chart.getSCColor(name, up);
                               
                            }
                            else // square
                            {
                                legendColor = _chart.getSCColor("Exh", sexit);
                                tooltip = "Exh";
                            }

                            if (newTrendEnb || name != "New Trend")
                            {
                                SymbolAnnotation marker = new SymbolAnnotation();
                                marker.Id = id;
                                marker.Tooltip = tooltip;
                                marker.DateTime = times[ii];
                                marker.Placement = (lentry || sexit) ? Placement.Below : Placement.Above;
                                marker.SymbolType = symbolType;
                                marker.Color = legendColor;
                                marker.Offset = 1;
                                marker.Width = 1.5;
                                marker.Height = 1.5;
                                _chart.Panels[mainPanelIndex].AddAnnotation(marker);
                            }
                        }

                        if (lreduce || sreduce)
                        {
                            var tooltip = "Retrace";
                            legendColor = _chart.getSCColor("Retrace", sreduce);
  
                            TextAnnotation marker = new TextAnnotation();
                            marker.Id = id;
                            marker.Tooltip = tooltip;
                            marker.DateTime = times[ii];
                            marker.Placement = sreduce ? Placement.Below : Placement.Above;
                            marker.Color = legendColor;
                            marker.Offset = 1;
                            marker.Width = 1.5;
                            marker.Height = 4.0;
                            marker.Text = lreduce ? "\u25D3" : "\u25D2";  // half painted circle
                            _chart.Panels[mainPanelIndex].AddAnnotation(marker);
                        }
                    }

                    var signal = signals[currentBarIndex];
    
                    var ticker = _chart.Symbol;
                    var rev = atm.isYieldTicker(ticker);
                    var tu = rev ? "Short" : "Long";        
                    var td = rev ? "Long" : "Short";

                    var trend =  (scSig[currentBarIndex] == 1) ? rev?  "Bearish" : "Bullish" : rev? "Bullish" : "Bearish";
                    var position = "";
                    var action = "";
                    var color = Colors.White;

                    var ft_tp_st = atm.calculateFastTurningPoints(hi, lo, cl, ft, currentBarIndex);
                    var ftp_st_up = ft_tp_st[0];
                    var ftp_st_dn = ft_tp_st[1];

                    var tpup0 = ftp_st_up[currentBarIndex];
                    var tpdn0 = ftp_st_dn[currentBarIndex];
                    var tpup1 = ftp_st_up[currentBarIndex + 1];
                    var tpdn1 = ftp_st_dn[currentBarIndex + 1];

                    var trendTurnsUp = scSig[currentBarIndex - 1] == -1 && scSig[currentBarIndex] ==  1;
                    var trendTurnsDn = scSig[currentBarIndex - 1] ==  1 && scSig[currentBarIndex] == -1;

                    position = lng ? tu : sht ? td : "Out";

                    var lent =    lng && !lng1 && (addEnb && signal == 5 || !addEnb && trendTurnsUp);
                    var ladd =    lng && addEnb && signal == 5;
                    var lred =    lng && redEnb && signal == 3;
                    var lexi =    lng && (scSig[currentBarIndex] == -1 || (exhEnb && signal == 2));
                    var ltpDn =   lng && !Double.IsNaN(tpdn0);
                    var ltpUp =  !lng &&  scSig[currentBarIndex] ==  1 && !Double.IsNaN(tpup0);

                    var sent =    sht && !sht1 && (addEnb && signal == -5 || !addEnb && trendTurnsDn);
                    var sadd =    sht && addEnb && signal == -5;
                    var sred =    sht && redEnb && signal == -3;
                    var sexi =    sht && (scSig[currentBarIndex] ==  1 || (exhEnb && signal == -2));
                    var stpUp =   sht && !Double.IsNaN(tpup0);
                    var stpDn =  !sht &&  scSig[currentBarIndex] == -1 && !Double.IsNaN(tpdn0);

                    if (lent) { action = "New " + tu; color = rev ? Colors.Red : Colors.Lime; }
                    else if (sent) { action = "New " + td; color = rev ? Colors.Lime : Colors.Red; }
                    else if (lexi) { action = "Exit " + tu; color =  rev ? Colors.Magenta : Colors.Cyan; }
                    else if (sexi) { action = "Exit " + td; color =  rev ? Colors.Cyan :  Colors.Magenta; }
                    else if (ladd) { action = "Add " + tu; color =  rev? Colors.Red : Colors.Lime; }
                    else if (sadd) { action = "Add " + td; color =  rev? Colors.Lime : Colors.Red; }
                    else if (lred) { action = "Reduce " + tu; color = rev ? Colors.Yellow : Colors.Yellow; }  // first dark org
                    else if (sred) { action = "Reduce " + td; color = rev ? Colors.Yellow :  Colors.Yellow; }  // second dark org
                    else if (ltpDn) { action = "Expect " + tu + " to retrace if close < " + tpdn0; color = rev ? Colors.Yellow : Colors.Yellow; } // first dark org
                    else if (stpUp) { action = "Expect " + td + " to retrace if close > " + tpup0 ; color =  rev ? Colors.Yellow :  Colors.Yellow; }  // second dark org
                    else if (ltpUp) { action = "Go " + tu + " if close > " + tpup0; color = rev ? Colors.Red : Colors.Lime; }
                    else if (stpDn) { action = "Go " + td + " if close < " + tpdn0; color = rev ? Colors.Lime :  Colors.Red; }
                    else if (lng) { action = "Stay " + tu; color =  rev? Colors.Red : Colors.Lime; }
                    else if (sht) { action = "Stay " + td; color =  rev? Colors.Lime : Colors.Red; }
                    
                    else if ( scSig[currentBarIndex] == 1) { action = "Wait to go " + tu; color =  rev ? Colors.Yellow : Colors.Yellow; }  // first dark org
                    else if ( scSig[currentBarIndex] == -1) { action = "Wait to go " + td; color = rev ? Colors.Yellow : Colors.Yellow; }  // second dark org

                    string action1 = "";
                    Color color1 = Colors.White;          
                    if (lred && !Double.IsNaN(tpdn1)) { action1 = "Reduce " + tu + " if Cl \u003c " + tpdn1.ToString(".00"); color1 = Colors.Yellow; }
                    else if (lng && (Double.IsNaN(tpdn0) || cl[currentBarIndex] > tpdn0)) {  action1 = "Stay " + tu; color1 = rev ? Colors.Red :Colors.Lime; }
                    else if (lng && !Double.IsNaN(tpdn0) && cl[currentBarIndex] <= tpdn0) {  action1 = "Wait to go " + tu; color1 = color; }
                    else if (!sht && !Double.IsNaN(tpup1)) { action1 = ((action == "Stay " + tu) ? "Add to " : "Go ") + tu + " if Cl \u003e " + tpup1.ToString(".00"); color1 = rev ? Colors.Red :Colors.Lime; }
                    else if (!sht && action.Length == 0)  { action1 = ((action == "Stay "+ tu) ? "Stay " : "Wait to go ") + tu; color1 = color; }
            
                    if (sred && !Double.IsNaN(tpup1)) { action1 = "Reduce " + td + " if Cl \u003e " + tpup1.ToString(".00"); color1 = Colors.Yellow; }
                    else if (sht && (Double.IsNaN(tpup0) || cl[currentBarIndex] < tpup0)) {  action1 = "Stay " + td; color1 = rev ? Colors.Lime :Colors.Red;}
                    else if (sht && !Double.IsNaN(tpup0) && cl[currentBarIndex] <= tpup0) {  action1 = "Wait to go " + td; color1 = color; }                   
                    else if (!lng && !Double.IsNaN(tpdn1)) { action1 = ((action == "Stay " + td) ? "Add to " : "Go ") + td + " if Cl \u003c " + tpdn1.ToString(".00"); color1 = rev ? Colors.Lime : Colors.Red; }
                    else if (!lng && action.Length == 0)  { action1 = ((action == "Stay " + td) ? "Stay " : "Wait to go ") + td; color1 = color; }
                    
                    output.Add(new Tuple<string, Color>(trend, (trend == "Bullish") ? Colors.Lime : Colors.Red));
                    output.Add(new Tuple<string, Color>(position, (position == "Long") ? Colors.Lime :(position == "Short") ? Colors.Red : Colors.White ));
                    output.Add(new Tuple<string, Color>(action, color));
                    output.Add(new Tuple<string, Color>(action1, color1));
                }
                else
                {
                    _chart.Panels[mainPanelIndex].RemoveAnnotations(id);
                }
            }
            return output;
        }

        public void calculateXSignalStudy(string ticker, string interval, List<DateTime> times, Series[] series, Series score, Series rp, Series pr, bool firstAlertEnable,
            bool addOnAlertEnable, bool pullbackAlertEnable, bool exhaustionAlertEnable, bool xAlertEnable,
            bool divergenceAlertEnable, bool pressureAlertEnable, bool ADXUpEnable, bool ADXDnEnable,
            bool FTEnable, bool STEnable, bool FTSTEnable, bool TRTEnable, bool twoBarEnable, bool scoreAlertEnable,
            bool scSigStartingEnable, bool scSigCurrentEnable, bool scSigHistoryEnable,
            bool prSigStartingEnable, bool prSigCurrentEnable, bool prSigHistoryEnable,
            bool netSigHistoryEnable, bool pSigEnable, bool pressureTrendAlertEnable, bool twoBarTrendEnable, Dictionary<string, Indicator> indicators)
        {
            if (times != null && times.Count > 0 && series != null)
            {
                Series op = series[0];
                Series hi = series[1];
                Series lo = series[2];
                Series cl = series[3];

                if (op != null && hi != null && lo != null && cl != null)
                {
                    int count = times.Count;

                    Dictionary<string, Series> signals = new Dictionary<string, Series>();
                    signals["FA"] = firstAlertEnable ? atm.calculateFirstAlert(hi, lo, cl, atm.FirstAlertLevelSelection.AllLevels) : new Series(count, 0);
                    signals["AOA"] = addOnAlertEnable ? atm.calculateAddOnAlert(hi, lo, cl, atm.AddonAlertLevelSelection.AllLevels) : new Series(count, 0);
                    signals["PB"] = pullbackAlertEnable ? atm.calculatePullbackAlert(hi, lo, cl) : new Series(count, 0);
                    signals["Exh"] = exhaustionAlertEnable ? atm.calculateExhaustion(hi, lo, cl, atm.ExhaustionLevelSelection.AllLevels) : new Series(count, 0);
                    signals["XAlert"] = xAlertEnable ? atm.calculateXAlert(hi, lo, cl) : new Series(count, 0);
                    signals["Div"] = divergenceAlertEnable ? atm.calculateZAlert(hi, lo, cl) : new Series(count, 0);
                    signals["ATM P Alert"] = pSigEnable && score != null ? atm.calculatePressureAlertNoFilter(score, op, hi, lo, cl) : new Series(count, 0);
                    signals["Prs"] = pressureAlertEnable ? atm.calculatePressureAlert(op, hi, lo, cl) : new Series(count, 0);
                    //signals["PT Alert"] = pressureTrendAlertEnable ? atm.calculatePTAlert(op, hi, lo, cl, score, rp) : new Series(count, 0);
                    signals["FT Alert"] = FTEnable ? atm.calculateFTAlert(hi, lo, cl) : new Series(count, 0);
                    signals["ST Alert"] = STEnable ? atm.calculateSTAlert(hi, lo, cl) : new Series(count, 0);
                    signals["FTST Alert"] = FTSTEnable ? atm.calculateFTSTAlert(hi, lo, cl) : new Series(count, 0);
                    signals["Two Bar"] = twoBarEnable ? atm.calculateTwoBarPattern(op, hi, lo, cl) : new Series(count, 0);
                    //signals["Two Bar Trend"] = twoBarTrendEnable ? atm.calculate2BTAlert(op, hi, lo, cl) : new Series(count, 0);
                    signals["SC"] = scSigCurrentEnable && score != null ? atm.calculateSCSig(score, rp, 1) : new Series(count, 0);
                    signals["Starting FT Sig"] = prSigStartingEnable && score != null ? atm.calculatePRSig(pr, 0) : new Series(count, 0);
                    signals["Current FT Sig"] = prSigCurrentEnable && score != null ? atm.calculatePRSig(pr, 1) : new Series(count, 0);
                    signals["PR"] = prSigHistoryEnable && score != null ? atm.calculatePRSig(pr, 2) : new Series(count, 0);
                    signals["FT | P"] = prSigHistoryEnable && score != null ? atm.calculatePressureAlert(op, hi, lo, cl) : new Series(count, 0);
                    signals["FT | FT"] = prSigHistoryEnable && score != null ? atm.calculatePRSig(pr, 2) : new Series(count, 0);
                    signals["FT || FT"] = prSigHistoryEnable && score != null ? atm.calculatePRSig(pr, 2) : new Series(count, 0); 
                    signals["FT | SC"] = prSigHistoryEnable && score != null ? atm.calculateSCSig(score, rp, 1) : new Series(count, 0);
                    signals["FT | ST"] = prSigHistoryEnable && score != null ? atm.calculateSTSig(op, hi, lo, cl, 0) : new Series(count, 0);
                    signals["FT | TSB"] = prSigHistoryEnable && score != null ? atm.calculateTSB(hi, lo, cl) : new Series(count, 0);

                    int id1 = 20;
                    int id2 = 50;

                    int mainPanelIndex = _chart.GetPanelIndex("Main");

                    if (mainPanelIndex >= 0)
                    {
                        for (int id = id1; id < id2; id++)
                        {
                            _chart.Panels[mainPanelIndex].RemoveAnnotations(id);
                        }

                        foreach (KeyValuePair<string, Series> kvp in signals)
                        {
                            string name = kvp.Key;
                            Series values = kvp.Value;
                            for (int ii = 0; ii < count; ii++)
                            {
                                string tooltip = "";
                                if (ii < values.Count && !double.IsNaN(values[ii]) && values[ii] != 0)
                                {
                                    DateTime markerDateTime = (ii >= 0 && ii < times.Count) ? times[ii] : new DateTime();
                                    double value = values[ii];
                                    bool up = (values[ii] > 0);

                                    SymbolType symbolType = SymbolType.None;
                                    if (name == "FA")
                                    {
                                        //tooltip = "ATM First Alert\nAn ATM condition designed to anticipate the\nbeginning of a potential new trend. The ATM\nFirst Alert is represented by arrows positioned\nbelow a bar for a Bullish First Alert and above\nfor a Bearish First Alert.";

                                        symbolType = up ? SymbolType.UpArrow : SymbolType.DownArrow;
                                        SymbolAnnotation marker = new SymbolAnnotation();
                                        marker.Id = id1;
                                        marker.Tooltip = tooltip;
                                        marker.DateTime = markerDateTime;
                                        marker.Placement = up ? Placement.Below : Placement.Above;
                                        marker.SymbolType = symbolType;
                                        var uc = (indicators["First Alert"].Parameters["FAUp"] as ColorParameter).Value;
                                        var dc = (indicators["First Alert"].Parameters["FADn"] as ColorParameter).Value;
                                        marker.Color = up ? uc : dc;
                                        marker.Offset = 1;
                                        marker.Width = 2;
                                        marker.Height = 2;
                                        _chart.Panels[mainPanelIndex].AddAnnotation(marker);
                                    }
                                    else if (name == "AOA")
                                    {
                                        //tooltip = "ATM Add On Alert\nAn ATM condition designed to locate potential\nopportunities within current market direction.\nATM Add On Alerts are represented by a circle below\nthe bar for an ATM Add On Bullish Alert and a circle above\nthe bar for an ATM Add On Bearish Alert.";

                                        symbolType = SymbolType.Circle;
                                        //tooltip = "Add On Alert";
                                        SymbolAnnotation marker = new SymbolAnnotation();
                                        marker.Id = id1 + 1;
                                        marker.Tooltip = tooltip;
                                        marker.DateTime = markerDateTime;
                                        marker.Placement = up ? Placement.Below : Placement.Above;
                                        marker.SymbolType = symbolType;
                                        var uc = (indicators["Add On Alert"].Parameters["AOAUp"] as ColorParameter).Value;
                                        var dc = (indicators["Add On Alert"].Parameters["AOADn"] as ColorParameter).Value;
                                        marker.Color = up ? uc : dc;
                                        marker.Offset = 1;
                                        marker.Width = 2;
                                        marker.Height = 2;
                                        _chart.Panels[mainPanelIndex].AddAnnotation(marker);
                                    }
                                    else if (name == "PB")
                                    {
                                        //tooltip = "ATM Pullback Alert\nAn ATM condition designed to locate major setback\nopportunities within an established trend. ATM\nPullback Alerts are represented by Diamonds below the bar\nfor a Bullish Pullback Alert and above the bar for a\nBearish Pullback Alert.";

                                        symbolType = SymbolType.Diamond;
                                        SymbolAnnotation marker = new SymbolAnnotation();
                                        marker.Id = id1 + 2;
                                        marker.Tooltip = tooltip;
                                        marker.DateTime = markerDateTime;
                                        marker.Placement = up ? Placement.Below : Placement.Above;
                                        marker.SymbolType = symbolType;
                                        var uc = (indicators["Pullback Alert"].Parameters["PBUp"] as ColorParameter).Value;
                                        var dc = (indicators["Pullback Alert"].Parameters["PBDn"] as ColorParameter).Value;
                                        marker.Color = up ? uc : dc;
                                        marker.Offset = 1;
                                        marker.Width = 2;
                                        marker.Height = 2;
                                        _chart.Panels[mainPanelIndex].AddAnnotation(marker);
                                    }
                                    else if (name == "XAlert")
                                    {
                                        //tooltip = "ATM X Alert\nAn aggressive contra-trend ATM condition designed to\nmonitor the ending action of the ATM TSB (Trend Strength Bar)\nand the ensuing cycle of the ATM FT (Fast Trigger).\nThe ATM X Alert is represented by arrows positioned below a\nbar for a Bullish X Alert and above for a Bearish X Alert.  ";

                                        symbolType = SymbolType.X;
                                        SymbolAnnotation marker = new SymbolAnnotation();
                                        marker.Id = id1 + 3;
                                        marker.Tooltip = tooltip;
                                        marker.DateTime = markerDateTime;
                                        marker.Placement = up ? Placement.Below : Placement.Above;
                                        marker.SymbolType = symbolType;
                                        var uc = (indicators["X Alert"].Parameters["XSigUp"] as ColorParameter).Value;
                                        var dc = (indicators["X Alert"].Parameters["XSigDn"] as ColorParameter).Value;
                                        marker.Color = up ? uc : dc;
                                        marker.Offset = 1;
                                        marker.Width = 2;
                                        marker.Height = 2;
                                        _chart.Panels[mainPanelIndex].AddAnnotation(marker);
                                    }
                                    else if (name == "Div")
                                    {
                                        //tooltip = "ATM Divergence Alert\nAn ATM condition is designed to locate potential\ndivergences of significant price highs/lows to the ATM Fast\nTrigger line. ATM Divergence Alerts are represented by\na square below the bar for a Bullish Divergence Alert and\nabove the bar for a Bearish Divergence Alert.";

                                        symbolType = SymbolType.Square;
                                        SymbolAnnotation marker = new SymbolAnnotation();
                                        marker.Id = id1 + 4;
                                        marker.Tooltip = tooltip;
                                        marker.DateTime = markerDateTime;
                                        marker.Placement = up ? Placement.Below : Placement.Above;
                                        marker.SymbolType = symbolType;
                                        if (name == "Div") marker.Color = up ? Colors.DarkBlue : Colors.DarkRed;
                                        else marker.Color = up ? Colors.DodgerBlue : Colors.Red;
                                        marker.Offset = 1;
                                        marker.Width = 2;
                                        marker.Height = 2;
                                        _chart.Panels[mainPanelIndex].AddAnnotation(marker);
                                    }
                                    else if (name == "Exh")
                                    {
                                        //tooltip = "ATM Exhaustion Alert\nAn ATM condition designed to locate potential short\nterm contra trend tops and bottoms.  ATM Exhaustion\nAlerts are represented by a square below the bar for a\nBullish Exhaustion Alert and above the bar for a Bearish\nExhaustion Alert.";

                                        symbolType = SymbolType.Square;
                                        //tooltip = "Exhaustion Alert";
                                        SymbolAnnotation marker = new SymbolAnnotation();
                                        marker.Id = id1 + 5;
                                        marker.Tooltip = tooltip;
                                        marker.DateTime = markerDateTime;
                                        marker.Placement = up ? Placement.Below : Placement.Above;
                                        marker.SymbolType = symbolType;
                                        var uc = (indicators["Exhaustion Alert"].Parameters["ExhUp"] as ColorParameter).Value;
                                        var dc = (indicators["Exhaustion Alert"].Parameters["ExhDn"] as ColorParameter).Value;
                                        marker.Color = up ? uc : dc;
                                        marker.Offset = 1;
                                        marker.Width = 2;
                                        marker.Height = 2;
                                        _chart.Panels[mainPanelIndex].AddAnnotation(marker);
                                    }

                                    else if (name == "Prs")
                                    {
                                        //tooltip = "ATM Pressure Alert\nAn ATM condition designed to locate early contra activity\nonce the ATM FT reaches oversold or overbought.  ATM\nPressure Alerts are represented by a P below the bar\nfor a Bullish Pressure Alert and above the bar for a\nBearish Pressure Alert.";

                                        TextAnnotation marker = new TextAnnotation();
                                        marker.Id = id1 + 7;
                                        //marker.Tooltip = "Pressure Alert";
                                        marker.Tooltip = tooltip;
                                        marker.DateTime = markerDateTime;
                                        marker.Placement = (up) ? Placement.Below : Placement.Above;
                                        var uc = (indicators["Pressure Alert"].Parameters["PUp"] as ColorParameter).Value;
                                        var dc = (indicators["Pressure Alert"].Parameters["PDn"] as ColorParameter).Value;
                                        marker.Color = up ? uc : dc;
                                        marker.Offset = 1;
                                        marker.Width = 1;
                                        marker.Height = 3;
                                        marker.Text = "P";
                                        _chart.Panels[mainPanelIndex].AddAnnotation(marker);
                                    }

                                    else if (name == "FT Alert")
                                    {
                                        tooltip = "FT Turns";
                                        TextAnnotation marker = new TextAnnotation();
                                        marker.Id = id1 + 10;
                                        marker.Tooltip = tooltip;
                                        marker.DateTime = markerDateTime;
                                        marker.Placement = (up) ? Placement.Below : Placement.Above;
                                        var uc = (indicators["FT Alert"].Parameters["FTAlertUp"] as ColorParameter).Value;
                                        var dc = (indicators["FT Alert"].Parameters["FTAlertDn"] as ColorParameter).Value;
                                        marker.Color = up ? uc : dc;
                                        marker.Offset = 1;
                                        marker.Width = 1;
                                        marker.Height = 3;
                                        marker.Text = "F";
                                        _chart.Panels[mainPanelIndex].AddAnnotation(marker);
                                    }
                                    else if (name == "ST Alert")
                                    {
                                        tooltip = "ST Turns";
                                        TextAnnotation marker = new TextAnnotation();
                                        marker.Id = id1 + 11;
                                        marker.Tooltip = tooltip;
                                        marker.DateTime = markerDateTime;
                                        marker.Placement = (up) ? Placement.Below : Placement.Above;
                                        var uc = (indicators["ST Alert"].Parameters["STAlertUp"] as ColorParameter).Value;
                                        var dc = (indicators["ST Alert"].Parameters["STAlertDn"] as ColorParameter).Value;
                                        marker.Color = up ? uc : dc;
                                        marker.Offset = 1;
                                        marker.Width = 1;
                                        marker.Height = 3.0;
                                        marker.Text = "S";
                                        _chart.Panels[mainPanelIndex].AddAnnotation(marker);
                                    }

                                    else if (name == "FTST Alert")
                                    {
                                        tooltip = "FTST Turns";
                                        TextAnnotation marker = new TextAnnotation();
                                        marker.Id = id1 + 12;
                                        marker.Tooltip = tooltip;
                                        marker.DateTime = markerDateTime;
                                        marker.Placement = (up) ? Placement.Below : Placement.Above;
                                        var uc = (indicators["FTST Alert"].Parameters["FTSTUp"] as ColorParameter).Value;
                                        var dc = (indicators["FTST Alert"].Parameters["FTSTDn"] as ColorParameter).Value;
                                        marker.Color = up ? uc : dc;
                                        marker.Offset = 1;
                                        marker.Width = 1;
                                        marker.Height = 3.0;
                                        marker.Text = "B";
                                        _chart.Panels[mainPanelIndex].AddAnnotation(marker);
                                    }

                                    else if (name == "Two Bar")
                                    {
                                        tooltip = "2 Bar Alert";
                                        TextAnnotation marker = new TextAnnotation();
                                        marker.Id = id1 + 14;
                                        marker.Tooltip = tooltip;
                                        marker.DateTime = markerDateTime;
                                        marker.Placement = (up) ? Placement.Below : Placement.Above;
                                        var uc = (indicators["Two Bar Alert"].Parameters["2BUp"] as ColorParameter).Value;
                                        var dc = (indicators["Two Bar Alert"].Parameters["2BDn"] as ColorParameter).Value;
                                        marker.Color = up ? uc : dc;
                                        marker.Offset = 1;
                                        marker.Width = 2;
                                        marker.Height = 4.0;
                                        marker.Text = up ? "\u25D3" : "\u25D2";  // "\u25D3" : "\u25D2"  half painted circle
                                        _chart.Panels[mainPanelIndex].AddAnnotation(marker);
                                    }

                                    else if (name == "SC")
                                    {
                                        tooltip = "SC";
                                        TextAnnotation marker = new TextAnnotation();
                                        marker.Id = id1 + 19;
                                        marker.Tooltip = tooltip;
                                        marker.DateTime = markerDateTime;
                                        marker.Placement = (up) ? Placement.Below : Placement.Above;
                                        marker.Color = up ? Colors.Lime : Colors.Red;
                                        marker.Offset = 1;
                                        marker.Width = 1.5;
                                        marker.Height = 4.0;
                                        marker.Text = up ? "\u25b3" : "\u25bd";
                                        _chart.Panels[mainPanelIndex].AddAnnotation(marker);
                                    }

                                    //else if (name == "PT Alert")
                                    //{
                                    //    symbolType = SymbolType.Circle;
                                    //    tooltip = "ATM PT Alert\nAn ATM condition designed to locate early contra activity\nonce the ATM FT reaches oversold or overbought.  ATM\nPressure Alerts are represented by a P below the bar\nfor a Bullish Pressure Alert and above the bar for a\nBearish Pressure Alert.";

                                    //    SymbolAnnotation marker = new SymbolAnnotation();
                                    //    marker.Id = id1 + 20;
                                    //    marker.Tooltip = tooltip;
                                    //    marker.DateTime = markerDateTime;
                                    //    marker.Placement = up ? Placement.Below : Placement.Above;
                                    //    marker.SymbolType = symbolType;
                                    //    var uc = (indicators["PT Alert"].Parameters["PTUp"] as ColorParameter).Value;
                                    //    var dc = (indicators["PT Alert"].Parameters["PTDn"] as ColorParameter).Value;
                                    //    marker.Color = up ? uc : dc;
                                    //    marker.Offset = 1;
                                    //    marker.Width = 2;
                                    //    marker.Height = 2;
                                    //    _chart.Panels[mainPanelIndex].AddAnnotation(marker);
                                    //}
                                    //else if (name == "Two Bar Trend")
                                    //{
                                    //    symbolType = SymbolType.Circle;
                                    //    tooltip = "2 Bar Trend";
                                    //    var marker = new SymbolAnnotation();
                                    //    marker.SymbolType = symbolType;
                                    //    marker.Id = id1 + 21;
                                    //    marker.Tooltip = tooltip;
                                    //    marker.DateTime = markerDateTime;
                                    //    marker.Placement = (up) ? Placement.Below : Placement.Above;
                                    //    var uc = (indicators["Two Bar Trend"].Parameters["2BTUp"] as ColorParameter).Value;
                                    //    var dc = (indicators["Two Bar Trend"].Parameters["2BTDn"] as ColorParameter).Value;
                                    //    marker.Color = up ? uc : dc;
                                    //    marker.Offset = 1;
                                    //    marker.Width = 2;
                                    //    marker.Height = 2;
                                    //    _chart.Panels[mainPanelIndex].AddAnnotation(marker);
                                    //}
                                }
                            }
                        }
					}
				}
			}
		}

		private List<ChartLine> createTriggerLines(List<DateTime> times, Series series, double value, Color color, double thickness, int zOrder, string tooltip)
        {
            List<ChartLine> output = new List<ChartLine>();

            if (times.Count > 0)
            {
                int index1 = 0;
                int index2 = 0;
                int count = series.Count;
                for (int ii = 0; ii < count; ii++)
                {
                    if (series[ii] == 1 && (ii == 0 || series[ii - 1] == 0))  // begin
                    {
                        index1 = ii;
                        index2 = (ii + 1 < count) ? ii + 1 : ii;
                    }
                    else if (index1 != 0 && series[ii] == 1)  // continue
                    {
                        index2 = (ii + 1 < count) ? ii + 1 : ii;
                    }
                    else if (index1 != 0)
                    {
                        index2--;
                        int offset1 = (index1 < count) ? 0 : index1 - (count - 1);
                        int offset2 = (index2 < count) ? 0 : index2 - (count - 1);
                        DateTime time1 = (index1 < count) ? times[index1] : times[count - 1];
                        DateTime time2 = (index2 < count) ? times[index2] : times[count - 1];
                        ChartLine line1 = new ChartLine("TSB"); // createTriggerLines
                        line1.Point1 = new TimeValuePoint(time1, value, offset1);
                        line1.Point2 = new TimeValuePoint(time2, value, offset2);
                        line1.Color = color;
                        line1.Thickness = thickness;
                        line1.ZOrder = zOrder;
                        if (tooltip.Length > 0) line1.ToolTip = tooltip;
                        output.Add(line1);

                        index1 = 0;
                        index2 = 0;
                    }
                }

                if (index1 != 0)
                {
                    index2--;
                    int offset1 = (index1 < count) ? 0 : index1 - (count - 1);
                    int offset2 = (index2 < count) ? 0 : index2 - (count - 1);
                    DateTime time1 = (index1 < count) ? times[index1] : times[count - 1];
                    DateTime time2 = (index2 < count) ? times[index2] : times[count - 1];
                    ChartLine line1 = new ChartLine("TSB"); // createTriggerLines
                    line1.Point1 = new TimeValuePoint(time1, value, offset1);
                    line1.Point2 = new TimeValuePoint(time2, value, offset2);
                    line1.Color = color;
                    line1.Thickness = thickness;
                    line1.ZOrder = zOrder;
                    if (tooltip.Length > 0) line1.ToolTip = tooltip;
                    output.Add(line1);
                }
            }
            return output;
        }

        private List<ChartBezierCurve> createBezierCurves(int direction, int index2, List<DateTime> times, Series cl)
        {
            List<ChartBezierCurve> output = new List<ChartBezierCurve>();

            if (times != null && cl != null)
            {
                int index1 = index2 - 30;
                if (index1 >= 0 && index2 >= 0)
                {
                    double volatility = getPercentVolatility(index1, index2, cl);
                    double baseValue = cl[index2];
                    int count = cl.Count;

                    double profit1 = baseValue;
                    double profit2 = baseValue;

                    ChartBezierCurve curve1 = new ChartBezierCurve();
                    ChartBezierCurve curve2 = new ChartBezierCurve();

                    int projection = 7;
                    for (int ii = 0; ii <= projection; ii += (projection / 4))
                    {
                        int index = index2 + ii;
                        int offset = (index < count) ? 0 : index - (count - 1);
                        DateTime time = (index < count) ? times[index] : times[count - 1];

                        double value1 = Math.Exp(1.644853 * volatility * Math.Sqrt(ii));
                        double value2 = Math.Exp(0.674490 * volatility * Math.Sqrt(ii));

                        profit1 = ((direction == 1) ? baseValue * value1 : baseValue / value1);
                        profit2 = ((direction == 1) ? baseValue * value2 : baseValue / value2);

                        curve1.Points.Add(new TimeValuePoint(time, profit1, offset));
                        curve2.Points.Add(new TimeValuePoint(time, profit2, offset));
                    }

                    output.Add(curve1);
                    output.Add(curve2);
                }
            }
            return output;
        }

        private List<ChartBezierShading> createBezierShading(string interval, int direction, int index2, List<DateTime> times, Series cl, int level)
        {
            List<ChartBezierShading> output = new List<ChartBezierShading>();

            if (times != null && cl != null && times.Count > 0)
            {
                int index1 = index2 - 30;
                if (index1 >= 0 && index2 >= 0)
                {
                    double volatility = getPercentVolatility(index1, index2, cl);
                    double baseValue = cl[index2];
                    int count = cl.Count;

                    ChartBezierShading shading = new ChartBezierShading(level.ToString());
                    shading.Direction = direction;

                    int projection = 7;
                    for (int ii = 0; ii <= projection; ii += (projection / 4))
                    {
                        int index = index2 + ii;
                        int offset = (index < count) ? 0 : index - (count - 1);
                        DateTime time = (index < count) ? times[index] : times[count - 1];

                        double value1 = Math.Exp(1.644853 * volatility * Math.Sqrt(ii));
                        double value2 = Math.Exp(0.674490 * volatility * Math.Sqrt(ii));

                        double profit1 = ((direction == 1) ? baseValue * value1 : baseValue / value1);
                        double profit2 = ((direction == 1) ? baseValue * value2 : baseValue / value2);

                        shading.FirstCurvePoints.Add(new TimeValuePoint(time, profit1 - baseValue, offset));
                        shading.SecondCurvePoints.Add(new TimeValuePoint(time, profit2 - baseValue, offset));
                    }
                    shading.Tooltip = interval + " Forecast";
                    shading.ZOrder = -20 - level;
                    output.Add(shading);
                }
            }
            return output;
        }

        private double getPercentVolatility(int index1, int index2, Series input)
        {
            double volatility = 0;

            int count = index2 - index1 + 1;
            Series percent = new Series(count);
            double previous = double.NaN;
            double sum = 0;

            int pcCnt = 0;
            for (int ii = index1; ii < index2; ii++)
            {
                double current = input[ii];
                if (!double.IsNaN(previous) && !double.IsNaN(current))
                {
                    double value1 = Math.Abs(current);
                    double value2 = Math.Abs(previous);
                    double value = (value1 / value2) - 1;

                    if (value < 100)
                    {
                        percent[pcCnt++] = value;
                        sum += value;
                    }
                }
                previous = current;
            }
            if (pcCnt > 2)
            {
                double sumOfSquares = 0;
                double mean = sum / percent.Count;
                for (int ii = 0; ii < pcCnt; ii++)
                {
                    double dif = percent[ii] - mean;
                    sumOfSquares += (dif * dif);
                }
                volatility = Math.Sqrt(sumOfSquares / (percent.Count - 2));
            }
            return volatility;
        }
    }
}
