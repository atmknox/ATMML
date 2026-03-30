using RiskEngineMNParity;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ATMML
{
	public enum FundamentalChartCurveType
	{
		Line,
		Histogram
	}

	public class FundamentalChartCurve
	{
		FundamentalChartCurveType _type;
		List<double> _values;

		public FundamentalChartCurve(FundamentalChartCurveType type, List<double> values)
		{
			_type = type;
			_values = values;
		}

		public Brush Brush { get; set; }

		public FundamentalChartCurveType Type
		{
			get
			{
				return _type;
			}
		}

		public double GetValue(int index)
		{
			double output = double.NaN;
			if (index >= 0 && index < _values.Count)
			{
				output = _values[index];
			}
			return output;
		}

		public double MaxValue(int index1, int index2) // exclusive
		{
			double output = double.MinValue;
			for (int index = index1; index < index2; index++)
			{
				var value = (index < _values.Count) ? _values[index] : double.NaN;
				if (!double.IsNaN(value))
				{
					output = Math.Max(output, value);
				}
			}
			return output;
		}

		public double MinValue(int index1, int index2) // exclusive
		{
			double output = double.MaxValue;
			for (int index = index1; index < index2; index++)
			{
				var value = (index < _values.Count) ? _values[index] : double.NaN;
				if (!double.IsNaN(value))
				{
					output = Math.Min(output, value);
				}
			}
			return output;
		}

		public int ValueCount
		{
			get
			{
				return _values.Count;
			}
		}
	}

	public class GraphEventArgs : EventArgs
	{
		DateTime _cursorTime;
		DateTime _time1;
		DateTime _time2;

		public GraphEventArgs(DateTime cursorTime, DateTime time1, DateTime time2)
		{
			_cursorTime = cursorTime;
			_time1 = time1;
			_time2 = time2;
		}

		public DateTime CursorTime
		{
			get { return _cursorTime; }
		}

		public DateTime Time1
		{
			get { return _time1; }
		}

		public DateTime Time2
		{
			get { return _time2; }
		}
	}

	public delegate void GraphEventHandler(object sender, GraphEventArgs e);

	public class VerticalLine
	{
		public DateTime Date { get; set; }
		public Color Color { get; set; }
	}

	public class FundamentalGraph : INotifyPropertyChanged
	{

		public event GraphEventHandler GraphEvent;
		public event PropertyChangedEventHandler PropertyChanged;

		private Canvas _canvas;
		private List<DateTime> _times = new List<DateTime>();
		private List<FundamentalChartCurve> _curves;
		private List<VerticalLine> _verticalLines;
		private DateTime _cursorTime;
		private bool _cursorActivated = true;

		private int _index1; // inclusive
		private int _index2; // exclusive
		private bool _changeTimeScale = false;
		private bool _scrollChart = false;
		private double _mouseDownXPosition;

		private int _xMaxCnt = 0;

		private double _titleHeight = 0;   // there is no title
		private double _scaleWidth = 40;
		private double _scaleHeight = 8;

		private double _width;
		private double _height;
		private double _lMargin;
		private double _rMargin;
		private double _tMargin;
		private double _bMargin;
		private double _ww;
		private double _hh;
		private double _xMin;
		private double _xMax;
		private double _yMin;
		private double _yMax;
		private double _xScale;
		private double _yScale;
		private double _xOffset;
		private double _yOffset;

		private Line _cursorLine = null;

		private bool _showTimeScale = true;

		public FundamentalGraph(Canvas canvas)
		{
			_canvas = canvas;
			_curves = new List<FundamentalChartCurve>();
			_verticalLines = new List<VerticalLine>();

			_canvas.MouseLeftButtonDown += new MouseButtonEventHandler(MouseLeftButtonDown);
			_canvas.MouseLeftButtonUp += new MouseButtonEventHandler(MouseLeftButtonUp);
			_canvas.MouseMove += new MouseEventHandler(MouseMove);
		}

		protected virtual void OnPropertyChanged(string name)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
		}

		public bool ShowTimeScale
		{
			get { return _showTimeScale; }
			set { _showTimeScale = value; }
		}

		private void onGraphEvent(GraphEventArgs e)
		{
			if (GraphEvent != null)
			{
				GraphEvent(this, e);
			}
		}

		public List<DateTime> Times
		{
			get
			{
				return _times;
			}

			set
			{
				_times = value;
				if (_times != null && _times.Count > 0 && _cursorTime == default(DateTime))
				{
					CursorTime = _times[_times.Count - 1];
				}

				if (_times != null)
				{
					_index1 = 0;
					_index2 = _times.Count;
				}
			}
		}

		public bool CursorActivated
		{
			get
			{
				return _cursorActivated;
			}

			set
			{
				_cursorActivated = value;
			}
		}

		public DateTime CursorTime
		{
			get
			{
				return _cursorTime;
			}

			set
			{
				if (value != _cursorTime)
				{
					_cursorTime = value;
					drawCursor();
				}
			}
		}

		public DateTime Time1
		{
			get
			{
				return (0 <= _index1 && _index1 < _times.Count) ? _times[_index1] : default(DateTime);
			}

			set
			{
				if (value != default(DateTime) && value != Time1)
				{
					_index1 = Math.Max(0, getIndex(value));
					Draw();
				}
			}
		}

		public DateTime Time2
		{
			get
			{
				return (0 <= _index2 && _index2 < _times.Count) ? _times[_index2] : default(DateTime);
			}

			set
			{
				if (value != default(DateTime) && value != Time2)
				{
					_index2 = Math.Max(0, getIndex(value));
					Draw();
				}
			}
		}

		void MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			_changeTimeScale = false;
			_scrollChart = false;
			_canvas.ReleaseMouseCapture();

			if (!_mouseMoved)
			{
				OnPropertyChanged("SelectTime");
			}
		}

		bool _mouseMoved = false;

		void MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			_canvas.CaptureMouse();

			_mouseMoved = false;

			Point position = e.GetPosition(_canvas);

			double y1 = _height - _scaleHeight - 20;
			double y2 = _height;

			if (y1 <= position.Y && position.Y <= y2)
			{
				_changeTimeScale = true;
				_mouseDownXPosition = position.X;
			}
			else
			{
				_scrollChart = true;
				_mouseDownXPosition = position.X;
			}
		}
		void MouseMove(object sender, MouseEventArgs e)
		{
			if (_changeTimeScale)
			{
				Point position = e.GetPosition(_canvas);
				double x = position.X;
				int amount = (int)Math.Round(1.0 * (x - _mouseDownXPosition));
				if (Math.Abs(amount) != 0)
				{
					_mouseMoved = true;

					_index1 -= amount;
					if (_index1 < 0) _index1 = 0;
					if (_index2 > _times.Count) _index2 = _times.Count;
					if (_index1 > _times.Count - 10) _index1 = _times.Count - 10;
					if (_index2 < _index1 + 10) _index2 = _index1 + 10;
					Draw();
					_mouseDownXPosition = x;
					onGraphEvent(new GraphEventArgs(_cursorTime, Time1, Time2));
				}
			}
			else if (_scrollChart)
			{
				Point position = e.GetPosition(_canvas);
				double x = position.X;
				var count = _index2 - _index1;
				int amount = (int)Math.Round(1.0 * (x - _mouseDownXPosition));
				if (Math.Abs(amount) != 0)
				{
					_mouseMoved = true;

					_index1 -= amount;
					if (_index1 < 0) _index1 = 0;
					_index2 = _index1 + count;
					if (_index2 > _times.Count)
					{
						_index2 = _times.Count;
						_index1 = _index2 - count;
						if (_index1 < 0) _index1 = 0;
					}
					Draw();
					_mouseDownXPosition = x;
					onGraphEvent(new GraphEventArgs(_cursorTime, Time1, Time2));
				}
			}
			else if (_cursorActivated)
			{
				Point position = e.GetPosition(_canvas);
				DateTime time = getTime(position.X);
				CursorTime = time;
				onGraphEvent(new GraphEventArgs(_cursorTime, Time1, Time2));

				//System.Diagnostics.Debug.WriteLine(position.X.ToString("##.00") + " " +  " " + time.ToString());
			}
		}

		public void RemoveCurves()
		{
			_curves.Clear();
		}

		public void AddCurve(FundamentalChartCurve input)
		{
			_curves.Add(input);
		}

		public void AddVerticalLine(VerticalLine line)
		{
			_verticalLines.Add(line);
		}


		public void Draw()
		{
			calculateScales();
			_canvas.Children.Clear();
			drawTimeScale(getTimeScaleInfo());
			drawPercentScale();
			drawCurves();
			drawVerticalLines();
			drawCursor();
		}

		private void drawVerticalLines()
		{
			// Guard against invalid scale
			if (_xMax <= _xMin) return;

			_verticalLines.ForEach(l =>
			{
				int index = getIndex(l.Date);

				// Skip if date not found in times list
				if (index < 0) return;

				var x = getXPixel(index);

				// Skip if x is not a valid finite number
				if (!double.IsFinite(x)) return;

				Line line1 = new Line();
				line1.Stroke = new SolidColorBrush(l.Color);
				line1.X1 = x;
				line1.X2 = x;
				line1.Y1 = _tMargin;
				line1.Y2 = _height;
				line1.HorizontalAlignment = HorizontalAlignment.Center;
				line1.VerticalAlignment = VerticalAlignment.Center;
				line1.StrokeThickness = 1;
				_canvas.Children.Add(line1);
			});
		}

		private void drawCursor()
		{
			if (_xMax > _xMin)
			{
				if (_cursorLine != null)
				{
					_canvas.Children.Remove(_cursorLine);
					_cursorLine = null;
				}

				if (_cursorTime != default(DateTime))
				{
					_cursorLine = new Line();
					int index = getIndex(_cursorTime);
					if (index >= _index1 && index < _index2)
					{
						double thickness = Math.Min(10.0, 1 * _xScale);
						double x = getXPixel(index);
						_cursorLine.X1 = x;
						_cursorLine.Y1 = _tMargin;
						_cursorLine.X2 = _cursorLine.X1;
						_cursorLine.Y2 = _height;
						_cursorLine.Stroke = new SolidColorBrush(Color.FromArgb(0x40, 0xff, 0xff, 0xff));
						_cursorLine.StrokeEndLineCap = PenLineCap.Flat;
						_cursorLine.StrokeThickness = thickness;
						_cursorLine.IsHitTestVisible = false;
						_canvas.Children.Add(_cursorLine);
					}
				}
			}
		}

		class TimeScaleInfo
		{
			public TimeScaleInfo()
			{
				m_time = new DateTime();
				m_pixel = 0;
				m_level = 0;
				m_type = 0;
			}
			public DateTime m_time;
			public double m_pixel;
			public int m_level; // 0 = nothing, 1 = major grid and label, 2 = minor grid
			public int m_type;  // 0 = no label, 1 = MM/dd-HH:mm, 2 = MM/dd, 3 = yyyy/MM, 4 = yyyy
		};

		private bool isTimeValid(DateTime time)
		{
			return (time.Year > 1);
		}

		private void calculateScales()
		{
			_width = _canvas.ActualWidth;
			_height = _canvas.ActualHeight;

			_lMargin = _scaleWidth;
			_rMargin = _scaleWidth;
			_tMargin = _titleHeight;
			_bMargin = _scaleHeight;

			_ww = _width - _lMargin - _rMargin;
			_hh = _height - _tMargin - _bMargin;

			_xMin = _index1;
			_xMax = _index2;

			_xMaxCnt = 0;

			_yMin = double.MaxValue;
			_yMax = double.MinValue;
			foreach (var curve in _curves)
			{
				_xMaxCnt = Math.Max(_xMaxCnt, curve.ValueCount);

				var minVal = curve.MinValue(_index1, _index2);
				var maxVal = curve.MaxValue(_index1, _index2);

				_yMin = Math.Min(_yMin, (curve.Type == FundamentalChartCurveType.Histogram) ? Math.Min(0, minVal) : minVal);
				_yMax = Math.Max(_yMax, (curve.Type == FundamentalChartCurveType.Histogram) ? Math.Max(0, maxVal) : maxVal);
			}

			// Handle case where no valid data was found
			if (_yMin == double.MaxValue || _yMax == double.MinValue)
			{
				_yMin = -0.01;
				_yMax = 0.01;
			}
			else if (_yMin == 0 && _yMax == 0)
			{
				_yMin = -0.01;
				_yMax = 0.01;
			}
			else
			{
				_yMin *= (_yMin >= 0) ? 0.98 : 1.02;
				_yMax *= (_yMax >= 0) ? 1.02 : 0.98;
			}

			// Safeguard against division by zero for X scale
			if (_xMax <= _xMin)
			{
				_xScale = 1;
			}
			else
			{
				_xScale = _ww / (_xMax - _xMin);
			}

			// Safeguard against division by zero for Y scale
			if (_yMax <= _yMin)
			{
				_yScale = 1;
			}
			else
			{
				_yScale = _hh / (_yMax - _yMin);
			}

			_xOffset = _lMargin;
			_yOffset = _tMargin;
		}
		private IList<TimeScaleInfo> getTimeScaleInfo()
		{
			int cnt = _times.Count;
			IList<TimeScaleInfo> info = new List<TimeScaleInfo>(cnt);

			if (_times.Count > 0)
			{
				for (int ii = 0; ii < cnt; ii++)
				{
					info.Add(new TimeScaleInfo());
				}

				bool useLocalTime = false;

				bool found = false;

				DateTime firstPrvTime = new DateTime();

				int textWidth2 = 60;
				int textWidth3 = 40;
				int textWidth4 = 20;

				// MM/dd every day
				if (!found)
				{
					DateTime prvTime = firstPrvTime;
					double prvPixel = double.NaN;
					int collisionDistance = textWidth2 + 12;
					for (int idx = _index1; idx < _index2; idx++)
					{
						info[idx].m_type = 0;
						info[idx].m_level = 0;
						DateTime time = _times[idx];
						double pixel = getXPixel(idx);
						info[idx].m_pixel = pixel;
						if (isTimeValid(time) && isTimeValid(prvTime))
						{
							if (useLocalTime)
							{
								time = time.ToLocalTime();
							}
							info[idx].m_time = time;
							if (prvTime.Day != time.Day)
							{
								found = true;
								info[idx].m_type = 2;
								info[idx].m_level = 1;
								if (!double.IsNaN(prvPixel))
								{
									if (Math.Abs(pixel - prvPixel) < collisionDistance)
									{
										found = false;
										break;
									}
								}
								prvPixel = pixel;
							}
							int minute = time.Minute + 60 * time.Hour;
							if (info[idx].m_level == 0 && minute % 60 == 0)
							{
								info[idx].m_level = 2;
							}
						}
						prvTime = time;
					}
				}

				// MM/dd every week
				if (!found)
				{
					int[] interval3 = { 1, 2, 3 };
					DateTime prvTime = firstPrvTime;
					double prvPixel = double.NaN;
					int collisionDistance = textWidth2 + 12;
					for (int idx = _index1; idx < _index2; idx++)
					{
						info[idx].m_type = 0;
						info[idx].m_level = 0;
						DateTime time = _times[idx];
						double pixel = getXPixel(idx);
						info[idx].m_pixel = pixel;
						if (isTimeValid(time) && isTimeValid(prvTime))
						{
							if (useLocalTime)
							{
								time = time.ToLocalTime();
							}
							info[idx].m_time = time;
							if (prvTime.DayOfWeek > time.DayOfWeek)
							{
								found = true;
								info[idx].m_type = 2;
								info[idx].m_level = 1;
								if (!double.IsNaN(prvPixel))
								{
									if (Math.Abs(pixel - prvPixel) < collisionDistance)
									{
										found = false;
										break;
									}
								}
								prvPixel = pixel;
							}
							if (info[idx].m_level == 0 && prvTime.Day != time.Day)
							{
								info[idx].m_level = 2;
							}
						}
						prvTime = time;
					}
				}

				// yyyy/MM
				if (!found)
				{
					int[] interval3 = { 1, 2, 3 };
					int collisionDistance = textWidth3 + 12;
					for (int ii = 0; ii < 3 && !found; ii++)
					{
						DateTime prvTime = firstPrvTime;
						double prvPixel = double.NaN;
						for (int idx = _index1; idx < _index2; idx++)
						{
							info[idx].m_type = 0;
							info[idx].m_level = 0;
							DateTime time = _times[idx];
							double pixel = getXPixel(idx);
							info[idx].m_pixel = pixel;
							if (isTimeValid(time) && isTimeValid(prvTime))
							{
								if (useLocalTime)
								{
									time = time.ToLocalTime();
								}
								info[idx].m_time = time;
								if (prvTime.Month != time.Month)
								{
									int month = time.Month - 1;
									if (month % interval3[ii] == 0)
									{
										found = true;
										info[idx].m_type = 3;
										info[idx].m_level = 1;
										if (!double.IsNaN(prvPixel))
										{
											if (Math.Abs(pixel - prvPixel) < collisionDistance)
											{
												found = false;
												break;
											}
										}
										prvPixel = pixel;
									}
								}
								if (info[idx].m_level == 0 && ((ii == 0 && prvTime.DayOfWeek > time.DayOfWeek) || (ii > 0 && prvTime.Month != time.Month)))
								{
									info[idx].m_level = 2;
								}
							}
							prvTime = time;
						}
					}
				}

				// yyyy
				if (!found)
				{
					int[] interval4 = { 1, 2, 4, 5, 10, 20, 25, 50, 100 };
					int collisionDistance = textWidth4 + 12;
					for (int ii = 0; ii < 9 && !found; ii++)
					{
						DateTime prvTime = firstPrvTime;
						double prvPixel = double.NaN;
						for (int idx = _index1; idx < _index2; idx++)
						{
							info[idx].m_type = 0;
							info[idx].m_level = 0;
							DateTime time = _times[idx];
							double pixel = getXPixel(idx);
							info[idx].m_pixel = pixel;
							if (isTimeValid(time) && isTimeValid(prvTime))
							{
								if (useLocalTime)
								{
									time = time.ToLocalTime();
								}
								info[idx].m_time = time;
								if (prvTime.Year != time.Year)
								{
									int year = time.Year;
									if (year % interval4[ii] == 0)
									{
										found = true;
										info[idx].m_type = 4;
										info[idx].m_level = 1;
										if (!double.IsNaN(prvPixel))
										{
											if (Math.Abs(pixel - prvPixel) < collisionDistance)
											{
												found = false;
												break;
											}
										}
										prvPixel = pixel;
									}
								}
								if (info[idx].m_level == 0 && prvTime.Month != time.Month)
								{
									info[idx].m_level = 2;
								}
							}
							prvTime = time;
						}
					}
				}
			}
			return info;
		}

		void drawTimeScale(IList<TimeScaleInfo> timeScaleInfo)
		{
			if (_showTimeScale)
			{
				Brush brush1 = Brushes.White;

				double labelHeight = 1 * _yScale;

				double x1 = _lMargin;
				double x2 = _width - _rMargin;

				double y1 = _height - _scaleHeight;
				double y2 = _height;

				if (_width > 0)
				{
					int fontSize = 8;
					Color foregroundColor = Colors.White;
					string[] timeScaleFormats = { "", "MM/dd-HH:mm", "MM/dd", "MMM yyyy", "yyyy" };

					for (int ii = 0; ii < timeScaleInfo.Count; ii++)
					{
						if (timeScaleInfo[ii].m_level == 1)
						{
							double x = timeScaleInfo[ii].m_pixel;

							if (x1 < x && x < x2)
							{
								Line line1 = new Line();
								line1.Stroke = brush1;
								line1.StrokeThickness = 0.5;
								line1.X1 = x;
								line1.X2 = x;
								line1.Y1 = y1;
								line1.Y2 = y2;
								line1.HorizontalAlignment = HorizontalAlignment.Center;
								line1.VerticalAlignment = VerticalAlignment.Center;
								line1.StrokeThickness = 1;
								_canvas.Children.Add(line1);

								TextBlock textBlock = new TextBlock();
								DateTime time = timeScaleInfo[ii].m_time;
								textBlock.Text = time.ToString(timeScaleFormats[timeScaleInfo[ii].m_type]);
								textBlock.Height = _scaleHeight;
								textBlock.SetValue(Canvas.LeftProperty, x + 1);
								textBlock.SetValue(Canvas.TopProperty, y1);
								textBlock.TextAlignment = TextAlignment.Left;
								textBlock.FontSize = fontSize;
								textBlock.FontWeight = FontWeights.Light;
								textBlock.FontFamily = new FontFamily("Arial");
								textBlock.Foreground = new SolidColorBrush(foregroundColor);
								_canvas.Children.Add(textBlock);  // drawTimeScale
							}
						}
					}
				}
			}
		}

		private void drawPercentScale()
		{
			if (_height > 0)
			{
				double labelOffset = 14;

				int numberOfLabels = (int)Math.Round(_hh / 30);
				if (numberOfLabels < 4) numberOfLabels = 4;
				else if (numberOfLabels > 8) numberOfLabels = 8;

				double yInc = 1;
				double vInc = 1;
				double yRange = _yMax - _yMin;
				yInc = yRange / numberOfLabels;

				double[] labelIncs = { 0.1, 0.2, 0.5, 1, 2, 5, 10, 20, 25, 50, 100, 200, 250, 500, 1000, 2000, 2500, 5000, 10000, 20000, 25000, 50000, 100000, 200000, 250000, 500000 };
				double[] lineIncs = { 0.02, 0.05, 0.1, 0.25, 0.5, 1, 2, 5, 5, 10, 20, 50, 50, 100, 200, 500, 500, 1000, 2000, 5000, 5000, 10000, 20000, 50000, 50000, 100000 };
				double selectedLabelInc = 1;
				double selectedLineInc = 1;
				double minDistance = double.MaxValue;
				for (int inc = 0; inc < labelIncs.Length; inc++)
				{
					double distance = Math.Abs(yInc - labelIncs[inc]);
					if (distance < minDistance)
					{
						minDistance = distance;
						selectedLabelInc = labelIncs[inc];
						selectedLineInc = lineIncs[inc];
					}
				}

				yInc = selectedLabelInc;
				vInc = selectedLineInc;

				LinearGradientBrush cgBrush = new LinearGradientBrush();
				cgBrush.StartPoint = new Point(0, 0);
				cgBrush.EndPoint = new Point(1, 0);
				cgBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0x00, 0xcc, 0x00), 0));
				cgBrush.GradientStops.Add(new GradientStop(Colors.Black, 1));

				LinearGradientBrush clBrush = new LinearGradientBrush();
				clBrush.StartPoint = new Point(0, 0);
				clBrush.EndPoint = new Point(1, 0);
				clBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0xcc, 0x00, 0x00), 0));
				clBrush.GradientStops.Add(new GradientStop(Colors.Black, 1));

				LinearGradientBrush ogBrush = new LinearGradientBrush();
				ogBrush.StartPoint = new Point(0, 0);
				ogBrush.EndPoint = new Point(1, 0);
				ogBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0x00, 0xcc, 0xcc), 0));
				ogBrush.GradientStops.Add(new GradientStop(Colors.Black, 1));

				LinearGradientBrush olBrush = new LinearGradientBrush();
				olBrush.StartPoint = new Point(0, 0);
				olBrush.EndPoint = new Point(1, 0);
				olBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0xcc, 0xcc, 0x00), 0));
				olBrush.GradientStops.Add(new GradientStop(Colors.Black, 1));

				Brush brush1 = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
				Brush brush2 = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));

				Brush lBrush = new SolidColorBrush(Color.FromRgb(0x40, 0xbf, 0x40));  //lime
				Brush sBrush = new SolidColorBrush(Color.FromRgb(0xba, 0x2c, 0x2c));  //red

				bool addPlus = false;

				double top = _yMax - (_yMax % vInc) + vInc;
				double bot = _yMin - (_yMin % vInc);

				string format = vInc < 1 ? "0.00" : "0";

				for (double value = top; value >= bot; value -= vInc)

				{
					double yy = getYPixel(value);

					double linev = Math.Abs(value) % vInc;
					double labelv = Math.Abs(value) % yInc;

					bool line = linev <= 1e-2 || Math.Abs(vInc - linev) <= 1e-2;
					bool label = labelv <= 1e-2 || Math.Abs(yInc - labelv) <= 1e-2;

					if (line && !label && yy > _titleHeight && yy < _height - _scaleHeight)
					{
						Line line1 = new Line();
						line1.Stroke = (value >= 0) ? lBrush : sBrush;
						line1.X1 = label ? _lMargin : _width - _scaleWidth / 2 - 2;
						line1.X2 = label ? _width - _scaleWidth : _width - _scaleWidth / 2 + 2;
						line1.Y1 = yy;
						line1.Y2 = yy;
						line1.HorizontalAlignment = HorizontalAlignment.Center;
						line1.VerticalAlignment = VerticalAlignment.Center;
						line1.StrokeThickness = 1;
						_canvas.Children.Add(line1);
					}

					if (label && yy > _titleHeight && yy < _height - _scaleHeight)
					{
						string text = Math.Abs(value).ToString(format);
						if (Math.Abs(value) >= 1000000000) text = (value / 1000000000).ToString() + "B";
						else if (Math.Abs(value) >= 1000000) text = (value / 1000000).ToString() + "M";
						else if (Math.Abs(value) >= 1000) text = (value / 1000).ToString() + "K";

						if (addPlus)
						{
							text += "+";
							addPlus = false;
						}

						Label labelText1 = new Label();
						labelText1.Content = text;
						labelText1.Foreground = (value >= 0) ? lBrush : sBrush;
						Canvas.SetLeft(labelText1, _width - _scaleWidth);
						Canvas.SetTop(labelText1, yy - labelOffset);
						labelText1.Width = _scaleWidth;
						labelText1.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Center;
						labelText1.VerticalContentAlignment = System.Windows.VerticalAlignment.Center;
						_canvas.Children.Add(labelText1);
					}
				}
			}
		}

		private double getXPixel(double input)
		{
			return _xOffset + _xScale * (input - _xMin);
		}
		private double getYPixel(double input)
		{
			return _yOffset + _yScale * (_yMax - input);
		}

		private DateTime getTime(double input)
		{
			DateTime output = new DateTime();
			if (_times.Count > 0)
			{
				int index = _index1 + (int)Math.Round(((input - _xOffset) / _xScale - _xMin));
				if (0 <= index && index < _times.Count)
				{
					output = _times[index];
				}
				else
				{
					output = _times[_times.Count - 1];
				}
			}
			return output;
		}

		private int getIndex(DateTime input)
		{
			int output = _times.IndexOf(input);
			return output;
		}

		private void drawCurves()
		{
			if (_width > 0 && _height > 0)
			{
				double xMin = _lMargin;
				double xMax = _width - _rMargin;

				foreach (var curve in _curves)
				{
					Brush brush1 = curve.Brush;

					if (curve.Type == FundamentalChartCurveType.Line)
					{
						double v1 = double.NaN;
						int xIndexOffset = curve.ValueCount - _xMaxCnt;
						for (int ii = _index1; ii < _index2; ii++)
						{
							int index = ii + xIndexOffset;
							double v2 = curve.GetValue(index);
							if (!double.IsNaN(v1) && !double.IsNaN(v2))
							{
								double x1 = getXPixel(index - 1);
								double x2 = getXPixel(index);
								double y1 = getYPixel(v1);
								double y2 = getYPixel(v2);

								if (x2 > xMin && x1 <= xMax && !double.IsNaN(y1) && !double.IsNaN(y2))
								{
									Line line1 = new Line();
									line1.Stroke = brush1;
									line1.X1 = x1;
									line1.X2 = x2;
									line1.Y1 = y1;
									line1.Y2 = y2;
									line1.HorizontalAlignment = HorizontalAlignment.Center;
									line1.VerticalAlignment = VerticalAlignment.Center;
									line1.StrokeThickness = 1;
									_canvas.Children.Add(line1);
								}
							}
							v1 = v2;
						}
					}
					else if (curve.Type == FundamentalChartCurveType.Histogram)
					{
						double y1 = getYPixel(0);

						int xIndexOffset = curve.ValueCount - _xMaxCnt;
						for (int ii = _index1; ii < _index2; ii++)
						{
							int index = ii + xIndexOffset;
							double v1 = curve.GetValue(index);
							if (!double.IsNaN(v1))
							{
								// Center the bar in its time slot: getXPixel(index) returns the
								// LEFT edge of the slot; WPF Line is drawn centered on its X
								// coordinate, so add _xScale/2 to place it in the middle.
								double x1 = getXPixel(index) + _xScale / 2.0;
								double y2 = getYPixel(v1);

								if (!double.IsNaN(y1) && !double.IsNaN(y2))
								{
									Line line1 = new Line();
									line1.Stroke = brush1;
									line1.X1 = x1;
									line1.X2 = x1;
									line1.Y1 = y1;
									line1.Y2 = y2;
									line1.HorizontalAlignment = HorizontalAlignment.Center;
									line1.VerticalAlignment = VerticalAlignment.Center;
									line1.ToolTip = v1.ToString("##.00");
									line1.StrokeThickness = _xScale - 0.1;
									_canvas.Children.Add(line1);
								}
							}
						}
					}
				}
			}
		}
	}
}