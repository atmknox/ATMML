using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ATMML
{
    public class Yield
    {
        public Yield(string ticker, string description, double years, double value, double condition)
        {
            Ticker = ticker;
            Description = description;
            Years = years;
            Value = value;
            Condition = condition;
        }
        public string Ticker { get; set; }
        public string Description { get; set; }
        public double Years { get; set; }
        public double Value { get; set; }
        public double Condition { get; set; }
    }

    public enum YieldEventType
    {
        Ticker,
        Condition
    }

    public class YieldChartEventArgs : EventArgs
    {
  
        public YieldChartEventArgs(YieldEventType type, string description)
        {
            Type = type;
            Description = description;
        }


        public YieldEventType Type
        {
            get; private set;
        }

        public string Description { get; private set; }    
    }

    public class YieldChart
    {
        public delegate void YieldChartEventHandler(object sender, YieldChartEventArgs e);
        public event YieldChartEventHandler YieldChartEvent;

        private string _title;

        private Canvas _canvas;
        private double _titleHeight = 16;
        private double _scaleWidth = 40;
        private double _scaleHeight = 21;
        private double _panelHeight = 25; // percent

        private double _xScale = 1.0;
        private double _xOffset = 0.0;
        private double _yOffset = 0.0;

        private bool _adjustYScale;
        private double _adjustYScaleBase;

        private bool _adjustXScale;
        private double _adjustXScaleBase;

        private bool _scrollChart;
        private double _scrollXChartBase;
        private double _scrollYChartBase;

        private Stopwatch _doubleClickStopwatch = null;

        private bool _active = false;

        private void _canvas_MouseLeave(object sender, MouseEventArgs e)
        {
            _active = false;
            Draw();
        }

        private void _canvas_MouseEnter(object sender, MouseEventArgs e)
        {
            _active = true;
            Draw();
        }

        public string Title
        {
            get { return _title; }
            set { _title = value; }
        }

        public bool IsVisible()
        {
            return (_canvas.Visibility == Visibility.Visible);
        }
 
        private void _canvas_MouseMove(object sender, MouseEventArgs e)
        {
            var position = e.GetPosition(_canvas);

            if (_adjustXScale)
            {
                var delta = position.X - _adjustXScaleBase;
                _adjustXScaleBase = position.X;

                _xScale -= delta / 20;

                _xScale = Math.Min(100.0, Math.Max(0.01, _xScale));

                Draw();
            }
            else if (_adjustYScale)
            {
                var delta = position.Y - _adjustYScaleBase;
                _yMargin += delta;
                _yMargin = Math.Min(500, Math.Max(5, _yMargin));

                _adjustYScaleBase = position.Y;

                Draw();
            }
            else if (_scrollChart)
            {
                var deltaX = position.X - _scrollXChartBase;
                var deltaY = position.Y - _scrollYChartBase;

                _scrollXChartBase = position.X;
                _scrollYChartBase = position.Y;

                _xOffset -= deltaX / 20;
                _xOffset = Math.Min(100.0, Math.Max(0.0, _xOffset));

                _yOffset += deltaY / 100;
                //_yOffset = Math.Min(100.0, Math.Max(0.0, _yOffset));

                Draw();
            }
        }

        private void _canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _canvas.ReleaseMouseCapture();

            _adjustXScale = false;
            _adjustYScale = false;
            _scrollChart = false;
        }

        private void _canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _canvas.Focus();
            _canvas.CaptureMouse();
  
            var position = e.GetPosition(_canvas);

            double width = _canvas.ActualWidth;
            double height = _canvas.ActualHeight;

            double interval = (_doubleClickStopwatch == null) ? 10000 : _doubleClickStopwatch.ElapsedMilliseconds;
            if (interval < 500)
            {
                _xScale = 1.0;
                _xOffset = 0.0;
                _yOffset = 0.0;
                _yMargin = 5.0;

                Draw();
            }
            else if (position.Y >= height - _scaleHeight)
            {
                _adjustXScale = true;
                _adjustXScaleBase = position.X;
            }
            else  if (position.X >= width - _scaleWidth)
            {
                _adjustYScale = true;
                _adjustYScaleBase = position.Y;
            }
            else
            {
                _scrollChart = true;
                _scrollXChartBase = position.X;
                _scrollYChartBase = position.Y;
            }

            _doubleClickStopwatch = Stopwatch.StartNew();
        }

        private void onYieldChartEvent(YieldChartEventArgs e)
        {
            YieldChartEvent?.Invoke(this, e);
        }

        double _yMargin = 5;

        List<List<Yield>> _yields;
        public List<List<Yield>> Yields
        {
            get
            {
                return _yields;
            }

            set
            {
                _yields = value;
            }
        }

        class YieldData
        {
            public int Id;
            public string Description;
            public string Ticker;
            public double Years;
            public double Value;
            public double Condition;
        }

        public List<string> Intervals
        {
            get
            {
                string[] intervals = { "M", "W", "D", "240", "120", "60", "30", "15", "5" };
                return intervals.ToList();
            }
        }

        Panel _intervalSelectionPanel = null;

        private void drawIntervalSelection()
        {
            if (_intervalSelectionPanel == null)
            {
                _intervalSelectionPanel = new Grid();
                var comboBox = new ComboBox();
                comboBox.ItemsSource = Intervals;
                comboBox.Width = 60;
                comboBox.Height = _titleHeight - 2;
                comboBox.FontSize = 7;
                comboBox.Foreground = Brushes.White;
                comboBox.SelectionChanged += Interval_SelectionChanged;

                var style = Application.Current.Resources["ComboBoxStyle1"] as Style;
                comboBox.Style = style;

                _intervalSelectionPanel.Children.Add(comboBox);
            }

            int width = (int)_canvas.ActualWidth;
            _intervalSelectionPanel.SetValue(Canvas.LeftProperty, width - 61.0);
            _intervalSelectionPanel.SetValue(Canvas.TopProperty, 2.0);
            _canvas.Children.Add(_intervalSelectionPanel);
        }


        string _interval = "D";

        public string Interval { get { return _interval; } }

        private void Interval_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var cb = sender as ComboBox;
            var time = cb.SelectedItem as string;
            if (time != null)
            {
                cb.SelectedValue = "";
                _interval = time;
            }
        }

        public void Draw()
        {
            _canvas.Children.Clear();

            //drawTitle();
            drawChart();
            drawPanel();
            drawIntervalSelection();
            drawBorder();
        }

        void drawBorder()
        {
            int width1 = (int)_canvas.ActualWidth;
            int height1 = (int)_canvas.ActualHeight;
  
            Color borderColor = _active ? Color.FromRgb(0xd3, 0xd3, 0xd3) : Color.FromRgb(0x12, 0x4b, 0x72);
            drawRectangle(new Point(0.0, 0.0), new Point((double)width1, (double)height1), borderColor, Colors.Transparent, 1);
        }

        void drawRectangle(Point p1, Point p2, Color strokeColor, Color fillColor, double thickness)
        {
            double width = p2.X - p1.X;
            double height = p2.Y - p1.Y;
            if (width > 0 && height > 0)
            {
                Rectangle rectangle = new Rectangle();
                rectangle.Height = height;
                rectangle.Width = width;
                rectangle.SetValue(Canvas.LeftProperty, p1.X);
                rectangle.SetValue(Canvas.TopProperty, p1.Y);
                rectangle.Fill = new SolidColorBrush(fillColor);
                rectangle.Stroke = new SolidColorBrush(strokeColor);
                rectangle.StrokeEndLineCap = PenLineCap.Flat;
                rectangle.StrokeThickness = thickness;
                rectangle.RadiusX = 3;
                rectangle.RadiusY = 3;
                rectangle.SetValue(Canvas.ZIndexProperty, -100);
                _canvas.Children.Add(rectangle);
            }
        }

        private void drawChart()
        {
            double width = _canvas.ActualWidth;
            double height = _canvas.ActualHeight;

            var panelHeight = ((height - _titleHeight - _scaleHeight) * _panelHeight / 100);

            double lMargin = 0;
            double rMargin = _scaleWidth;
            double tMargin = _titleHeight;
            double bMargin = _scaleHeight + panelHeight;

            var canvasHeight = height - bMargin - tMargin;
            var canvasWidth = width - lMargin - rMargin;

            Rectangle rectangle = new Rectangle();
            rectangle.Height = height;
            rectangle.Width = width;
            rectangle.SetValue(Canvas.LeftProperty, 0.0);
            rectangle.SetValue(Canvas.TopProperty, 0.0);
            rectangle.Fill = Brushes.Black;
            rectangle.SetValue(Canvas.ZIndexProperty, -100);
            _canvas.Children.Add(rectangle);

            if (canvasWidth > 0 && canvasHeight > 0)
            {
                double xMin = double.MaxValue;
                double xMax = double.MinValue;
                double yMin = double.MaxValue;
                double yMax = double.MinValue;

                var data = new List<YieldData>();
                int listCount = Yields.Count;
                for (int ii = 0; ii < listCount; ii++)
                {
                    var count = Yields[ii].Count;
                    for (int jj = 0; jj < count; jj++)
                    {
                        Yield yield = Yields[ii][jj];
                        var years = yield.Years;

                        double value = yield.Value;
                        if (!double.IsNaN(value))
                        {
                            if (value > yMax) yMax = value;
                            if (value < yMin) yMin = value;

                            var yieldData = new YieldData();
                            yieldData.Id = ii;
                            yieldData.Ticker = yield.Ticker;
                            yieldData.Description = yield.Description;
                            yieldData.Years = years;
                            yieldData.Value = value;
                            yieldData.Condition = yield.Condition;
                            data.Add(yieldData);
                        }
                    }
                }

                var yearList = data.Select(x => x.Years).Distinct().OrderBy(x => x).ToList();

                xMin = _xOffset;
                xMax = _xOffset + yearList.Count * _xScale;

                double yIncrement = 1;
                var yRange = yMax - yMin;
                if (yRange < 1) yIncrement = 0.25;
                else if (yRange < 2) yIncrement = 0.5;
                else if (yRange < 5) yIncrement = 1;
                else if (yRange < 10) yIncrement = 2;
                else if (yRange < 20) yIncrement = 5;
                else if (yRange < 100) yIncrement = 20;
                else yIncrement = 100;

                int xIncrement = Math.Max(1, (int)(xMax - xMin) / 10);

                if (xMax > 0)
                {
                    if (xMax > xMin && yMax > yMin)
                    {
                        var canvas = new Canvas();
                        canvas.Height = canvasHeight;
                        canvas.Width = canvasWidth;

                        var border = new Border();
                        border.ClipToBounds = true;
                        border.Height = canvas.Height;
                        border.Width = canvas.Width;
                        border.CornerRadius = new CornerRadius(2);
                        border.BorderThickness = new Thickness(0, 1, 1, 1);
                        Color borderColor = _active ? Color.FromRgb(0xd3, 0xd3, 0xd3) : Color.FromRgb(0x12, 0x4b, 0x72);
                        border.BorderBrush = new SolidColorBrush(Color.FromArgb(0xff, 0x12, 0x4b, 0x72));
                        border.SetValue(Canvas.LeftProperty, lMargin);
                        border.SetValue(Canvas.TopProperty, tMargin);
                        border.Child = canvas;

                        _canvas.Children.Add(border);

                        double ww = width - lMargin - rMargin;
                        double hh = height - tMargin - bMargin;

                        double yExtra = _yMargin * (yMax - yMin) / 100;
                        yMax += yExtra;
                        yMin -= yExtra;

                        yMax += _yOffset;
                        yMin += _yOffset;

                        double xScale = ww / (xMax - xMin);
                        double yScale = hh / (yMax - yMin);

                        double xOffset = lMargin - xMin * xScale;
                        double yOffset = yMax * yScale;

                        double labelHeight = 24;
                        double labelWidth = 96;

                        Brush lBrush = new SolidColorBrush(Color.FromRgb(0x40, 0xbf, 0x40));  //FromRgb(0x00, 0xff, 0x00));  // green
                        Brush sBrush = new SolidColorBrush(Color.FromRgb(0xba, 0x2c, 0x2c));  //FromRgb(0xff, 0x00, 0x00));  // red
                        Brush hBrush = new SolidColorBrush(Color.FromRgb(0xef, 0x60, 0x01));

                        Brush brush1 = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
                        Brush brush2 = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
                        Brush brush3 = Brushes.Black;
                        Brush brush4 = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));

                        double xRadius = 6;
                        double yRadius = 6;

                        var ytop = yIncrement * ((int)Math.Ceiling(yMax / yIncrement));
                        var ybot = yIncrement * ((int)Math.Floor(yMin / yIncrement));

                        for (double value = ytop; value >= ybot; value -= yIncrement)
                        {
                            double yy = yOffset - (value * yScale);

                            bool label = true;

                            if (yy < height - bMargin)
                            {
                                Line line = new Line();
                                line.Stroke = (value >= 0) ? lBrush : sBrush;
                                line.X1 = label ? 0 : width - rMargin / 2 - 2;
                                line.X2 = label ? width - rMargin : width - rMargin / 2 + 2;
                                line.Y1 = yy;
                                line.Y2 = yy;
                                line.HorizontalAlignment = HorizontalAlignment.Center;
                                line.VerticalAlignment = VerticalAlignment.Center;
                                line.StrokeThickness = 1;
                                canvas.Children.Add(line);
                            }

                            if (label)
                            {
                                var vPos = yy + tMargin;
                                if (vPos >= tMargin && vPos <= height - bMargin)
                                {
                                    vPos -= labelHeight / 2;

                                    Label labelText = new Label();
                                    labelText.Content = value.ToString((yIncrement < 1) ? "0.00" : "0");
                                    brush1 = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));  //green  FromRgb(0x40, 0xbf, 0x40));
                                    brush2 = new SolidColorBrush(Color.FromRgb(0xba, 0x2c, 0x2c));  //red
                                    labelText.Foreground = (value >= 0) ? brush1 : brush2;
                                    Canvas.SetLeft(labelText, width - rMargin);
                                    Canvas.SetTop(labelText, vPos);
                                    labelText.Width = rMargin;
                                    labelText.FontSize = 10;
                                    labelText.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left;
                                    labelText.VerticalContentAlignment = System.Windows.VerticalAlignment.Top;
                                    _canvas.Children.Add(labelText);
                                }
                            }
                        }

                        // draw time scale
                        double xold = double.MinValue;

                        for (int ii = 0; ii < yearList.Count; ii++)
                        {
                            double value = yearList[ii];

                            double xx = xOffset + (ii * xScale) - (xRadius / 2);

                            if ((value <= 7 && xx - xold > 20) || (value > 7 && (value % 5) == 0))
                            {
                                Label labelText = new Label();
                                string text = (value < 1) ? ((int)(12 * value)).ToString() + "M" : value.ToString();

                                labelText.Content = text;
                                labelText.Foreground = brush4;
                                Canvas.SetLeft(labelText, xx);
                                Canvas.SetTop(labelText, tMargin + hh + 1 + panelHeight);
                                labelText.Height = _scaleHeight;
                                labelText.Width = labelWidth;
                                labelText.FontSize = 10;
                                labelText.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left;
                                labelText.VerticalContentAlignment = System.Windows.VerticalAlignment.Top;
                                _canvas.Children.Add(labelText);

                                xold = xx;
                            }
                        }

                        // draw yields
                        for (int ii = 0; ii < data.Count; ii++)
                        {
                            YieldData yield = data[ii];

                            int index = yearList.IndexOf(yield.Years);
                            double value = yield.Value;

                            double xx = xOffset + (index * xScale) - (xRadius / 2);
                            double yy = yOffset - (value * yScale) - (yRadius / 2);

                            Shape shape = (yield.Id == 0) ? new Ellipse() as Shape : new Rectangle() as Shape;
                            var radius = 1;
                            shape.Width = radius * xRadius;
                            shape.Height = radius * yRadius;
                            shape.Fill = (yield.Condition > 0) ? lBrush : (yield.Condition < 0) ? sBrush : hBrush;
                            Canvas.SetLeft(shape, xx - shape.Width / 2);
                            Canvas.SetTop(shape, yy - shape.Height / 2);
                            shape.MouseDown += Shape_MouseDown;
                            shape.ToolTip = yield.Description + "   " + yield.Value.ToString("0.00");
                            shape.Tag = yield.Ticker;
                            shape.Cursor = Cursors.Hand;
                            canvas.Children.Add(shape);
                        }
                    }
                }
            }
        }
        private void drawPanel()
        {
            double width = _canvas.ActualWidth;
            double height = _canvas.ActualHeight;

            Rectangle rectangle = new Rectangle();
            rectangle.Height = height;
            rectangle.Width = width;
            rectangle.SetValue(Canvas.LeftProperty, 0.0);
            rectangle.SetValue(Canvas.TopProperty, 0.0);
            rectangle.Fill = Brushes.Black;
            rectangle.SetValue(Canvas.ZIndexProperty, -100);
            _canvas.Children.Add(rectangle);

            var mainHeight = ((height - _titleHeight - _scaleHeight) * (100 - _panelHeight) / 100);

            double lMargin = 0;
            double rMargin = _scaleWidth;
            double tMargin = _titleHeight + mainHeight;
            double bMargin = _scaleHeight;

            var canvasHeight = height - bMargin - tMargin;
            var canvasWidth = width - lMargin - rMargin;

            if (canvasWidth > 0 && canvasHeight > 0)
            {
                double xMin = double.MaxValue;
                double xMax = double.MinValue;
                double yMin = double.MaxValue;
                double yMax = double.MinValue;

                var data = new List<YieldData>();
                int listCount = Yields.Count;

                var ix1 = (listCount == 2) ? 1 : 0;

                var yieldA = Yields[0].ToDictionary(x => x.Years, x => x.Value);
                var yieldB = Yields[ix1].ToDictionary(x => x.Years, x => x.Value);

                var years = yieldA.Keys.ToList();
                years.AddRange(yieldB.Keys.ToList());
                years = years.Distinct().ToList();

                for (int jj = 0; jj < years.Count; jj++)
                {
                    var year = years[jj];
                    if (yieldA.ContainsKey(year) && yieldB.ContainsKey(year))
                    {
                        var value1 = yieldA[year];
                        var value2 = yieldB[year];

                        if (!double.IsNaN(value1) && !double.IsNaN(value2))
                        {
                            double value = value1 - value2;
                            if (value > yMax) yMax = value;
                            if (value < yMin) yMin = value;

                            var yieldData = new YieldData();
                            yieldData.Id = jj;
                            yieldData.Years = year;
                            yieldData.Value = value;
                            data.Add(yieldData);
                        }
                    }
                }

                var largestValue = Math.Max(Math.Abs(yMax), Math.Abs(yMin));
                yMax = largestValue;
                yMin = -largestValue;

                if (yMax == yMin)
                {
                    yMax += 0.01;
                    yMin -= 0.01;
                }

                var yearList = data.Select(x => x.Years).Distinct().OrderBy(x => x).ToList();

                xMin = _xOffset;
                xMax = _xOffset + yearList.Count * _xScale;

                double yIncrement = 1;
                var yRange = yMax - yMin;
                if (yRange < 1) yIncrement = 0.20;
                else if (yRange < 2) yIncrement = 0.5;
                else if (yRange < 5) yIncrement = 1;
                else if (yRange < 10) yIncrement = 2;
                else if (yRange < 20) yIncrement = 5;
                else if (yRange < 100) yIncrement = 20;
                else yIncrement = 100;

                int xIncrement = Math.Max(1, (int)(xMax - xMin) / 10);

                if (xMax > 0)
                {
                    if (xMax > xMin && yMax > yMin)
                    {
                        var canvas = new Canvas();

                        canvas.Height = canvasHeight;
                        canvas.Width = canvasWidth;

                        var border = new Border();
                        border.ClipToBounds = true;
                        border.Height = canvas.Height;
                        border.Width = canvas.Width;
                        border.CornerRadius = new CornerRadius(2);
                        border.BorderThickness = new Thickness(0, 0, 1, 1);
                        border.BorderBrush = new SolidColorBrush(Color.FromArgb(0xff, 0x12, 0x4b, 0x72));
                        border.SetValue(Canvas.LeftProperty, lMargin);
                        border.SetValue(Canvas.TopProperty, tMargin);
                        border.Child = canvas;

                        _canvas.Children.Add(border);

                        double ww = width - lMargin - rMargin;
                        double hh = height - tMargin - bMargin;

                        double yExtra = _yMargin * (yMax - yMin) / 100;
                        yMax += yExtra;
                        yMin -= yExtra;

                        yMax += _yOffset;
                        yMin += _yOffset;

                        double xScale = ww / (xMax - xMin);
                        double yScale = hh / (yMax - yMin);

                        double xOffset = lMargin - xMin * xScale;
                        double yOffset = yMax * yScale;

                        double labelHeight = 20;

                        Brush lBrush = new SolidColorBrush(Color.FromRgb(0x40, 0xbf, 0x40));  //FromRgb(0x00, 0xff, 0x00));  // green
                        Brush sBrush = new SolidColorBrush(Color.FromRgb(0xba, 0x2c, 0x2c));  //FromRgb(0xff, 0x00, 0x00));  // red
                        Brush hBrush = new SolidColorBrush(Color.FromRgb(0xef, 0x60, 0x01));

                        Brush brush1 = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
                        Brush brush2 = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
                        Brush brush3 = Brushes.Black;
                        Brush brush4 = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));

                        double xRadius = 8;

                        var ytop = yIncrement * ((int)Math.Ceiling(yMax / yIncrement));
                        var ybot = yIncrement * ((int)Math.Floor(yMin / yIncrement));

                        for (double value = ytop; value >= ybot; value -= yIncrement)
                        {
                            double yy = yOffset - (value * yScale);

                            bool label = true;

                            if (yy < height - bMargin)
                            {
                                Line line = new Line();
                                line.Stroke = (value >= 0) ? lBrush : sBrush;
                                line.X1 = label ? 0 : width - rMargin / 2 - 2;
                                line.X2 = label ? width - rMargin : width - rMargin / 2 + 2;
                                line.Y1 = yy;
                                line.Y2 = yy;
                                line.HorizontalAlignment = HorizontalAlignment.Center;
                                line.VerticalAlignment = VerticalAlignment.Center;
                                line.StrokeThickness = 1;
                                canvas.Children.Add(line);
                            }

                            if (label)
                            {
                                var vPos = yy  + tMargin;
                                if (vPos >= tMargin && vPos <= height - bMargin)
                                {
                                    vPos -= labelHeight / 2;

                                    Label labelText = new Label();
                                    labelText.Content = value.ToString((yIncrement < 1) ? "0.00" : "0");
                                    brush1 = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));  //green  FromRgb(0x40, 0xbf, 0x40));
                                    brush2 = new SolidColorBrush(Color.FromRgb(0xba, 0x2c, 0x2c));  //red
                                    labelText.Foreground = (value >= 0) ? brush1 : brush2;
                                    Canvas.SetLeft(labelText, width - rMargin);
                                    Canvas.SetTop(labelText, vPos);
                                    labelText.Width = rMargin;
                                    labelText.FontSize = 8;
                                    labelText.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left;
                                    labelText.VerticalContentAlignment = System.Windows.VerticalAlignment.Center;
                                    _canvas.Children.Add(labelText);
                                }
                            }
                        }


                        // draw yields
                        for (int ii = 0; ii < data.Count; ii++)
                        {
                            YieldData yield = data[ii];

                            int index = yearList.IndexOf(yield.Years);
                            double value = yield.Value;

                            double xx = xOffset + (index * xScale) - (xRadius / 2);
                            double y1 = yOffset;
                            double y2 = yOffset - (value * yScale);

                            Rectangle shape = new Rectangle();
                            shape.Width = 6;
                            shape.Height = Math.Abs(y1 - y2);
                            shape.Fill = hBrush;
                            shape.ToolTip = value.ToString("0.00");
                            Canvas.SetLeft(shape, xx - shape.Width / 2);
                            Canvas.SetTop(shape, Math.Min(y1, y2));
                            canvas.Children.Add(shape);
                        }
                    }
                }
            }
        }

        private void Shape_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var element = sender as FrameworkElement;
            var ticker = element.Tag as string;
            onYieldChartEvent(new YieldChartEventArgs(YieldEventType.Ticker, ticker));
        }


        private void initializeContextMenu()
        {
            ContextMenu menu = new ContextMenu();
            menu.Margin = new Thickness(0);
            menu.Padding = new Thickness(0);
            menu.Foreground = Brushes.White;
            menu.Background = Brushes.Black;
            menu.FontSize = 11;
            menu.BorderThickness = new Thickness(1);

            ContextMenuService.SetShowOnDisabled(menu, true);

            MenuItem item = new MenuItem();
            item.Margin = new Thickness(4);
            item.Padding = new Thickness(4);
            item.FontSize = 11;
            item.Foreground = Brushes.LightGray;
            item.Background = Brushes.Black;
            item.StaysOpenOnClick = false;
            menu.Items.Add(item);
            //item.Header = "ATM Trend Bars";
            item.Click += new RoutedEventHandler(contextMenu_Click);

            item = new MenuItem();
            item.Margin = new Thickness(4);
            item.Padding = new Thickness(4);
            item.FontSize = 11;
            item.Foreground = Brushes.LightGray;
            item.Background = Brushes.Black;
            item.StaysOpenOnClick = false;
            menu.Items.Add(item);
            item.Header = "ATM FT";
            item.Click += new RoutedEventHandler(contextMenu_Click);

            item = new MenuItem();
            item.Margin = new Thickness(4);
            item.Padding = new Thickness(4);
            item.FontSize = 11;
            item.Foreground = Brushes.LightGray;
            item.Background = Brushes.Black;
            item.StaysOpenOnClick = false;
            menu.Items.Add(item);
            item.Header = "ATM ST";
            item.Click += new RoutedEventHandler(contextMenu_Click);

            item = new MenuItem();
            item.Margin = new Thickness(4);
            item.Padding = new Thickness(4);
            item.FontSize = 11;
            item.Foreground = Brushes.LightGray;
            item.Background = Brushes.Black;
            item.StaysOpenOnClick = false;
            menu.Items.Add(item);
            item.Click += new RoutedEventHandler(contextMenu_Click);

            _canvas.ContextMenu = menu;
        }

        void contextMenu_Click(object sender, RoutedEventArgs e)
        {
            MenuItem item = sender as MenuItem;
            string condition = item.Header as string;
             if (condition.Length > 0)
            {
                bool active = item.IsChecked;
                onYieldChartEvent(new YieldChartEventArgs(YieldEventType.Condition, condition));
            }
        }

    }
}
