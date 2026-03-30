using Microsoft.ML;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ATMML
{
	class ForecastRequest
	{
		public string model { get; set; } = "timegpt-1";
		public string freq { get; set; } = "D";
		public int fh { get; set; } = 6;
		public int[] level { get; set; } = new int[] { 50, 80, 90 };
		public Dictionary<string, double[]> x { get; set; } = new Dictionary<string, double[]>();
		public Dictionary<string, double> y { get; set; } = new Dictionary<string, double>();
		public bool clean_ex_first { get; set; } = true;
		public int finetune_steps { get; set; } = 10;
		public string finetune_loss { get; set; } = "default";
	}

	public class ForecastData
	{
		public List<string> timestamp { get; set; }
		public List<double> value { get; set; }
		public int input_tokens { get; set; }
		public int output_tokens { get; set; }
		public int finetune_tokens { get; set; }
		[JsonProperty("lo-50")]
		public List<double> lo50 { get; set; }
		[JsonProperty("hi-50")]
		public List<double> hi50 { get; set; }
		[JsonProperty("lo-80")]
		public List<double> lo80 { get; set; }
		[JsonProperty("hi-80")]
		public List<double> hi80 { get; set; }
		[JsonProperty("lo-90")]
		public List<double> lo90 { get; set; }
		[JsonProperty("hi-90")]
		public List<double> hi90 { get; set; }
	}

	public class ForecastHeaders
	{
	}

	public class ForecastResponse
	{
		public int status { get; set; }
		public ForecastData data { get; set; }
		public string message { get; set; }
		public string details { get; set; }
		public string code { get; set; }
		public string support { get; set; }
		public string requestID { get; set; }
		public ForecastHeaders headers { get; set; }
	}

	public class Forecast
	{
		[JsonIgnore]
		public List<DateTime> X;
		public Dictionary<string, List<double>> Y = new Dictionary<string, List<double>>();
	}

	public class ForecastService
	{
		public static Dictionary<string, Forecast> Forecasts = new Dictionary<string, Forecast>();

		public static  bool IsIntradayInterval(string interval)
		{
			bool output = false;
			int idx = interval.Length - 1;
			if (idx >= 0)
			{
				output = (interval[idx] != 'D' && interval[idx] != 'W' && interval[idx] != 'M' && interval[idx] != 'Q' && interval[idx] != 'Y');
			}
			return output;
		}

		private static int getTimeIndex(string interval, List<DateTime> times, DateTime dateTime)
		{
			var index = 0;
			if (times.Count > 0)
			{
				if (!IsIntradayInterval(interval))
				{
					dateTime = new DateTime(dateTime.Year, dateTime.Month, dateTime.Day);
				}

				var searchTime = dateTime;
				//lock (_barLock)
				{
					index = times.BinarySearch(searchTime);
					if (index < 0 || index >= times.Count)
					{
						int count = times.Count;
						index = count - 1;
						if (IsIntradayInterval(interval))
						{
							for (int ii = count - 1; ii >= 0; ii--)
							{
								if (times[ii] <= dateTime)
								{
									index = ii;
									break;
								}
							}
						}
						else
						{
							for (int ii = 0; ii < count; ii++)
							{
								if (times[ii] >= dateTime)
								{
									index = ii;
									break;
								}
							}
						}
					}
				}
			}
			return index;
		}

		public class ModelInput
		{
			public float close { get; set; }
			public float ft { get; set; }
			public float st { get; set; }
		}

		public class ModelOutput
		{
			public float[] forecast { get; set; }
			public float[] upper { get; set; }
			public float[] lower { get; set; }
		}

		public static Forecast GetForecast(string ticker, string interval, DateTime date)
		{
			var key1 = ticker + ":" + interval + ":" + date.ToString("yyyyMMddHHmm");
			return Forecasts.ContainsKey(key1) ? Forecasts[key1] : new Forecast();
		}

		public static void CalculateForecast(string ticker, string interval, List<DateTime> times, Series[] bars, DateTime date)
		{
			var index = times.FindIndex(x => x >= date);
			if (index > 0)
			{
				var key1 = ticker + ":" + interval + ":" + times[index].ToString("yyyyMMddHHmm");

				var forecasts = Forecasts;

				if (!forecasts.ContainsKey(key1)) // not in memory
				{
					try
					{
						Forecast forecast = null;

						if (bars[3].Count > 0) // otherwise call service
						{
							forecast = forecasts.ContainsKey(key1) ? forecasts[key1] : new Forecast();
							forecasts[key1] = forecast;

							Series hi = bars[1];
							Series lo = bars[2];
							Series cl = bars[3];
							Series ft = atm.calculateFT(hi, lo, cl);
							Series st = atm.calculateST(hi, lo, cl);
							var r1 = Enumerable.Range(0, index - 1);
							var r2 = Enumerable.Range(index - 1, 1);
							var trainInput = r1.Select(i => new ModelInput { close = (float)cl[i], ft = (float)ft[i], st = (float)st[i] });
							var testInput = r2.Select(i => new ModelInput { close = (float)cl[i], ft = (float)ft[i], st = (float)st[i] });
							var mlContext = new MLContext();

							var dist = ft.TurnsUp() | ft.TurnsDown();
							var count = 0;
							var idx0 = 0;
							var average = 0.0;
							for (var ii = dist.Count - 1; ii > 0 && count < 4; ii--)
							{
								var sig = dist[ii];
								if (sig == 1)
								{
									if (idx0 == 0)
									{
										idx0 = ii;
									}
									else
									{
										var period = idx0 - ii;
										average += period;
										idx0 = 0;
										count++;
									}
								}
							}
							var ws = (count == 0) ? 7 : (int)average / count;
							
							var trainData = mlContext.Data.LoadFromEnumerable<ModelInput>(trainInput);
							var forecastingPipeline = mlContext.Forecasting.ForecastBySsa(
								outputColumnName: "forecast",
								inputColumnName: "close",
								windowSize: ws,
								seriesLength: 100,
								trainSize: cl.Count,
								horizon: 6,
								confidenceLevel: 0.68f,
								confidenceLowerBoundColumn: "lower",
								confidenceUpperBoundColumn: "upper"
								);

							var forecaster = forecastingPipeline.Fit(trainData);

							var testData = mlContext.Data.LoadFromEnumerable<ModelInput>(testInput);
							IDataView predictions = forecaster.Transform(testData);
							var modelOutput = mlContext.Data.CreateEnumerable<ModelOutput>(predictions, true).First();

							var time0 = times[index];
							var index0 = getTimeIndex(interval, times, time0);
							var range = Enumerable.Range(index0, modelOutput.forecast.Length);

							var emptyList = range.Select(i => double.NaN).ToList();

							forecast.X = range.Select(i => times[i]).ToList();
							// todo find the bar index of the first time and then assign times from the bars starting at that index
							forecast.Y.Clear();
							forecast.Y["value"] = modelOutput.forecast.Select(f => (double)f).ToList();
							forecast.Y["upper"] = modelOutput.lower.Select(f => (double)f).ToList();
							forecast.Y["lower"] = modelOutput.upper.Select(f => (double)f).ToList();

							var close = bars[3][index];
							var proj = forecast.Y["value"][0];
							var offset = close - proj;
							forecast.Y["value"] = forecast.Y["value"].Select(x => x + offset).ToList();
							forecast.Y["upper"] = forecast.Y["upper"].Select(x => x + offset).ToList();
							forecast.Y["lower"] = forecast.Y["lower"].Select(x => x + offset).ToList();
						}
					}
					catch (Exception x)
					{
					}
				}
			}
		}
	}
}