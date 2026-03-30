using System;
using System.Collections.Generic;
using System.Text;

namespace ATMML
{
    public enum MAType
    {
        Simple,
        Exponential,
        Weighted,
        Variable,
        Triangular
    }

    public enum OscType
    {
        Difference,
        Ratio,
        Percentage
    }

    public class Series
    {
        private List<double> _data = new List<double>();
        private List<int> _holes = null;

        public Series()
        {
        }

        public Series(int count)
        {
            for (int ii = 0; ii < count; ii++)
            {
                this.Append(double.NaN);
            }
        }

        public Series(int count, double value)
        {
            for (int ii = 0; ii < count; ii++)
            {
                this.Append(value);
            }
        }

        public Series(double startValue, double endValue) :
            this(startValue, endValue, 1)
        {
        }

        public Series(double startValue, double endValue, double step)
        {
            if ((startValue < endValue && step < 0) ||
                (startValue > endValue && step > 0))
            {
                return;
            }

            if (step > 0)
            {
                double value = startValue;
                while (value <= endValue)
                {
                    this.Append(value);
                    value += step;
                }
            }
            else
            {
                double value = startValue;
                while (value >= endValue)
                {
                    this.Append(value);
                    value += step;
                }
            }
        }

        internal Series(List<double> data)
        {
            _data = data;
        }

        public int Capacity
        {
            get { return _data.Capacity; }
            set { _data.Capacity = value; }
        }

        public void Drop(int amount)
        {
            _data.RemoveRange(0, amount);
        }

        public Series Resize(int count)
        {
            var size = _data.Count;
            if (size > count)
            {
                Drop(size - count);
            }
            else if (size < count)
            {
                var s = new Series(count - size);
                _data.InsertRange(0, s._data);
            }
            return this;
        }

        public void Add(int amount)
        {
            for (int ii = 0; ii < amount; ii++)
            {
                _data.Insert(0, double.NaN);
            }
        }

        public int Count
        {
            get { return _data.Count; }
        }

        public List<double> Data
        {
            get { return _data; }
        }

        public void Append(double value)
        {
            _data.Add(value);
        }

        public double this[int index]
        {
            get
            {
                return (index >= 0 && index < _data.Count) ? _data[index] : double.NaN;
            }
            set
            {
                if (index >= 0 && index < _data.Count) _data[index] = value;
            }
        }

        public static Series RemoveHoles(Series input)
        {
            return RemoveHoles(new Series[] { input })[0];
        }

        public static Series[] RemoveHoles(Series[] inputs)
        {
            Series[] outputs = null;

            int count = 0;
            foreach (Series series in inputs)
            {
                if (series.Count > count)
                {
                    count = Math.Max(series.Count, count);
                }
            }

            List<int> holes = null;

            for (int ii = 0; ii < count; ii++)
            {
                foreach (Series series in inputs)
                {
                    if (ii >= series.Count || double.IsNaN(series[ii]))
                    {
                        if (holes == null)
                        {
                            holes = new List<int>();
                        }
                        holes.Add(ii);
                        break;
                    }
                }
            }

            if (holes == null)
            {
                outputs = inputs;
            }
            else
            {
                outputs = new Series[inputs.Length];

                for (int ii = 0; ii < inputs.Length; ii++)
                {
                    outputs[ii] = new Series();
                    outputs[ii]._holes = holes;

                    int index = 0;
                    for (int jj = 0; jj < count; jj++)
                    {
                        if (index < holes.Count && jj == holes[index])
                        {
                            index++;
                        }
                        else
                        {
                            outputs[ii].Append(inputs[ii][jj]);
                        }
                    }
                }
            }
            return outputs;
        }

        public Series AddHoles(Series[] series)
        {
            return AddHoles(series[0]);
        }

        public Series AddHoles(Series series)
        {
            Series output = null;
            if (series._holes == null)
            {
                output = this;
            }
            else
            {
                output = new Series(Count + series._holes.Count);
                int index = 0;
                for (int ii = 0; ii < Count + index; ii++)
                {
                    if (index < series._holes.Count && ii == series._holes[index])
                    {
                        index++;
                    }
                    else
                    {
                        output[ii] = this[ii - index];
                    }
                }
            }
            return output;
        }

        public Series Since(int direction)
        {
            Series output = new Series();
            Series[] input = RemoveHoles(new Series[] { this });
            var since = double.MaxValue;
            for (int ii = 0; ii < input[0].Count; ii++)
            {
                if (input[0][ii] == direction) since = 0;
                else if (since < double.MaxValue) since++;
                output.Append(since);
            }
            return output.AddHoles(input);
        }

        public static Series Or(Series series1, Series series2)
        {
            Series output = new Series();
            Series[] input = RemoveHoles(new Series[] { series1, series2 });
            for (int ii = 0; ii < input[0].Count; ii++)
            {
                output.Append((input[0][ii] != 0 || input[1][ii] != 0) ? 1 : 0);
            }
            return output.AddHoles(input); 
        }

        public static Series operator |(Series series1, Series series2)
        {
            return Or(series1, series2);
        }

        public Series Or(Series series2)
        {
            return Or(this, series2);
        }

        public static Series And(Series series1, Series series2)
        {
            Series output = new Series();
            Series[] input = RemoveHoles(new Series[] { series1, series2 });
            for (int ii = 0; ii < input[0].Count; ii++)
            {
                output.Append((input[0][ii] != 0 && input[1][ii] != 0) ? 1 : 0);
            }
            return output.AddHoles(input);
        }

        public static Series operator &(Series series1, Series series2)
        {
            return And(series1, series2);
        }

        public Series And(Series series2)
        {
            return And(this, series2);
        }

        public static Series Add(Series series1, Series series2)
        {
            Series output = new Series();
            Series[] input = RemoveHoles(new Series[] { series1, series2 });
            for (int ii = 0; ii < input[0].Count; ii++)
            {
                output.Append(input[0][ii] + input[1][ii]);
            }
            return output.AddHoles(input);
        }

        public static Series operator +(Series series1, Series series2)
        {
            return Add(series1, series2);
        }

        public static Series Subtract(Series series1, Series series2)
        {
            Series output = new Series();
            Series[] input = RemoveHoles(new Series[] { series1, series2 });
            for (int ii = 0; ii < input[0].Count; ii++)
            {
                output.Append(input[0][ii] - input[1][ii]);
            }
            return output.AddHoles(input);
        }

        public static Series operator -(Series series1, Series series2)
        {
            return Subtract(series1, series2);
        }

        public static Series Multiply(Series series1, Series series2)
        {
            Series output = new Series();
            Series[] input = RemoveHoles(new Series[] { series1, series2 });
            for (int ii = 0; ii < input[0].Count; ii++)
            {
                output.Append(input[0][ii] * input[1][ii]);
            }
            return output.AddHoles(input);
        }

        public static Series operator *(Series series1, Series series2)
        {
            return Multiply(series1, series2);
        }

        public static Series Divide(Series series1, Series series2)
        {
            Series output = new Series();
            Series[] input = RemoveHoles(new Series[] { series1, series2 });
            for (int ii = 0; ii < input[0].Count; ii++)
            {
                output.Append((input[1][ii] != 0) ? input[0][ii] / input[1][ii] : double.NaN);
            }
            return output.AddHoles(input);
        }

        public static Series operator /(Series series1, Series series2)
        {
            return Divide(series1, series2);
        }

        public static Series ShiftRight(Series series, int amount)
        {
            Series output = new Series();
            Series input = series; // RemoveHoles(series);
            int index = -amount;
            for (int ii = 0; ii < input.Count; ii++, index++)
            {
                output.Append(input[index]);
            }
            return output; // output.AddHoles(input);
        }

        public static Series operator >>(Series series, int amount)
        {
            return ShiftRight(series, amount);
        }

        public Series ShiftRight(int amount)
        {
            return ShiftRight(this, amount);
        }

        public static Series ShiftLeft(Series series, int amount)
        {
            Series output = new Series();
            Series input = RemoveHoles(series);
            int index = amount;
            for (int ii = 0; ii < input.Count; ii++, index++)
            {
                output.Append(input[index]);
            }
            return output.AddHoles(input);
        }

        public static Series operator <<(Series series, int amount)
        {
            return ShiftLeft(series, amount);
        }

        public Series ShiftLeft(int amount)
        {
            return ShiftLeft(this, amount);
        }

        public static Series Not(Series series)
        {
            Series output = new Series();
            Series input = RemoveHoles(series);
            for (int ii = 0; ii < input.Count; ii++)
            {
                output.Append((input[ii] == 0) ? 1 : 0);
            }
            return output.AddHoles(input);
        }

        public static Series Negate(Series series)
        {
            Series output = new Series();
            Series input = RemoveHoles(series);
            for (int ii = 0; ii < input.Count; ii++)
            {
                output.Append(-input[ii]);
            }
            return output.AddHoles(input);
        }

        public static Series operator -(Series series)
        {
            return Negate(series);
        }

        public static Series Sign(Series series)
        {
            Series output = new Series();
            Series input = RemoveHoles(series);
            for (int ii = 0; ii < input.Count; ii++)
            {
                output.Append((input[ii] > 0) ? 1 : ((input[ii] < 0) ?  -1 : 0));
            }
            return output.AddHoles(input);
        }

        public Series Sign()
        {
            return Sign(this);
        }

        public static Series Equal(Series series1, Series series2)
        {
            Series output = new Series();
            Series[] input = RemoveHoles(new Series[] { series1, series2 });
            for (int ii = 0; ii < input[0].Count; ii++)
            {
                output.Append(((input[0][ii] == input[1][ii]) ? 1 : 0));
            }
            return output.AddHoles(input);
        }

        public static Series Equal(Series series, double value)
        {
            Series output = new Series();
            Series input = RemoveHoles(series);
            for (int ii = 0; ii < input.Count; ii++)
            {
                output.Append(((input[ii] == value) ? 1 : 0));
            }
            return output.AddHoles(input);
        }

        public static Series NotEqual(Series series1, Series series2)
        {
            Series output = new Series();
            Series[] input = RemoveHoles(new Series[] { series1, series2 });
            for (int ii = 0; ii < input[0].Count; ii++)
            {
                output.Append(((input[0][ii] != input[1][ii]) ? 1 : 0));
            }
            return output.AddHoles(input);
        }

        public static Series NotEqual(Series series, double value)
        {
            Series output = new Series();
            Series input = RemoveHoles(series);
            for (int ii = 0; ii < input.Count; ii++)
            {
                output.Append(((input[ii] != value) ? 1 : 0));
            }
            return output.AddHoles(input);
        }

        public static Series Greater(Series series1, Series series2)
        {
            Series output = new Series();
            Series[] input = RemoveHoles(new Series[] { series1, series2 });
            for (int ii = 0; ii < input[0].Count; ii++)
            {
                output.Append(((input[0][ii] > input[1][ii]) ? 1 : 0));
            }
            return output.AddHoles(input);
        }

        public static Series operator >(Series series1, Series series2)
        {
            return Greater(series1, series2);
        }

        public static Series Greater(Series series, double value)
        {
            Series output = new Series();
            Series input = RemoveHoles(series);
            for (int ii = 0; ii < input.Count; ii++)
            {
                output.Append(((input[ii] > value) ? 1 : 0));
            }
            return output.AddHoles(input);
        }

        public static Series operator >(Series series, double value)
        {
            return Greater(series, value);
        }

        public static Series Less(Series series1, Series series2)
        {
            Series output = new Series();
            Series[] input = RemoveHoles(new Series[] { series1, series2 });
            for (int ii = 0; ii < input[0].Count; ii++)
            {
                output.Append(((input[0][ii] < input[1][ii]) ? 1 : 0));
            }
            return output.AddHoles(input);
        }

        public static Series operator <(Series series1, Series series2)
        {
            return Less(series1, series2);
        }

        public static Series Less(Series series, double value)
        {
            Series output = new Series();
            Series input = RemoveHoles(series);
            for (int ii = 0; ii < input.Count; ii++)
            {
                output.Append(((input[ii] < value) ? 1 : 0));
            }
            return output.AddHoles(input);
        }

        public static Series operator <(Series series, double value)
        {
            return Less(series, value);
        }

        public static Series GreaterEqual(Series series1, Series series2)
        {
            Series output = new Series();
            Series[] input = RemoveHoles(new Series[] { series1, series2 });
            for (int ii = 0; ii < input[0].Count; ii++)
            {
                output.Append(((input[0][ii] >= input[1][ii]) ? 1 : 0));
            }
            return output.AddHoles(input);
        }

        public static Series operator >=(Series series1, Series series2)
        {
            return GreaterEqual(series1, series2);
        }

        public static Series GreaterEqual(Series series, double value)
        {
            Series output = new Series();
            Series input = RemoveHoles(series);
            for (int ii = 0; ii < input.Count; ii++)
            {
                output.Append(((input[ii] >= value) ? 1 : 0));
            }
            return output.AddHoles(input);
        }

        public static Series operator >=(Series series, double value)
        {
            return GreaterEqual(series, value);
        }

        public static Series LessEqual(Series series1, Series series2)
        {
            Series output = new Series();
            Series[] input = RemoveHoles(new Series[] { series1, series2 });
            for (int ii = 0; ii < input[0].Count; ii++)
            {
                output.Append(((input[0][ii] <= input[1][ii]) ? 1 : 0));
            }
            return output.AddHoles(input);
        }

        public static Series operator <=(Series series1, Series series2)
        {
            return LessEqual(series1, series2);
        }

        public static Series LessEqual(Series series, double value)
        {
            Series output = new Series();
            Series input = RemoveHoles(series);
            for (int ii = 0; ii < input.Count; ii++)
            {
                output.Append(((input[ii] <= value) ? 1 : 0));
            }
            return output.AddHoles(input);
        }

        public static Series operator <=(Series series, double value)
        {
            return LessEqual(series, value);
        }

        public static Series Add(Series series, double value)
        {
            Series output = new Series();
            Series input = RemoveHoles(series);
            for (int ii = 0; ii < input.Count; ii++)
            {
                output.Append(input[ii] + value);
            }
            return output.AddHoles(input);
        }

        public static Series operator +(Series series, double value)
        {
            return Add(series, value);
        }

        public static Series Subtract(Series series, double value)
        {
            Series output = new Series();
            Series input = RemoveHoles(series);
            for (int ii = 0; ii < input.Count; ii++)
            {
                output.Append(input[ii] - value);
            }
            return output.AddHoles(input);
        }

        public static Series operator -(Series series, double value)
        {
            return Subtract(series, value);
        }

        public static Series Multiply(Series series, double value)
        {
            Series output = new Series();
            Series input = RemoveHoles(series);
            for (int ii = 0; ii < input.Count; ii++)
            {
                output.Append(input[ii] * value);
            }
            return output.AddHoles(input);
        }

        public static Series operator *(Series series, double value)
        {
            return Multiply(series, value);
        }

        public static Series Divide(Series series, double value)
        {
            Series output = new Series();
            Series input = RemoveHoles(series);
            for (int ii = 0; ii < input.Count; ii++)
            {
                output.Append((value != 0) ? input[ii] / value : double.NaN);
            }
            return output.AddHoles(input);
        }

        public static Series operator /(Series series, double value)
        {
            return Divide(series, value);
        }

        public Series Replace(double oldValue, double newValue)
        {
            Series output = new Series();
            Series input = RemoveHoles(this);
            for (int ii = 0; ii < input.Count; ii++)
            {
                output.Append((input[ii] != oldValue) ? input[ii] : newValue);
            }
            return output.AddHoles(input);
        }

        public Series ClipAbove(double value)
        {
            Series output = new Series();
            Series input = RemoveHoles(this);
            for (int ii = 0; ii < input.Count; ii++)
            {
                output.Append((input[ii] > value) ? value : input[ii]);
            }
            return output.AddHoles(input);
        }

        public Series ClipBelow(double value)
        {
            Series output = new Series();
            Series input = RemoveHoles(this);
            for (int ii = 0; ii < input.Count; ii++)
            {
                output.Append((input[ii] < value) ? value : input[ii]);
            }
            return output.AddHoles(input);
        }

        public Series ReplaceNaN(double newValue)
        {
            Series output = new Series();
            for (int ii = 0; ii < this.Count; ii++)
            {
                output.Append((!double.IsNaN(this[ii])) ? this[ii] : newValue);
            }
            return output;
        }

        public static Series Mid(Series high, Series low)
        {
            return (high + low) / 2;
        }

        public static Series Range(Series high, Series low)
        {
            return high - low;
        }

        public static Series HLCAvg(Series high, Series low, Series close)
        {
            return (high + low + close) / 3;
        }

        public static Series OHLCAvg(Series open, Series high, Series low, Series close)
        {
            return (open + high + low + close) / 4;
        }

        public static Series TrueHigh(Series high, Series close)
        {
            return high.Higher(close >> 1);
        }

        public static Series TrueLow(Series low, Series close)
        {
            return low.Lower(close >> 1);
        }

        public static Series TrueMid(Series high, Series low, Series close)
        {
            return (TrueHigh(high, close) + TrueLow(low, close)) / 2;
        }

        public static Series TrueHLCAvg(Series high, Series low, Series close)
        {
            return (TrueHigh(high, close) + TrueLow(low, close) + close) / 3;
        }

        public static Series TrueOHLCAvg(Series open, Series high, Series low, Series close)
        {
            return (open + TrueHigh(high, close) + TrueLow(low, close) + close) / 4;
        }

        public static Series TrueRange(Series high, Series low, Series close)
        {
            Series cl1 = (close >> 1);
            return high.Higher(cl1) - low.Lower(cl1);
        }

        public static Series DMUp(Series high, Series low, Series close, int period)
        {
            var tr = TrueRange(high, low, close);
            var atr = SmoothAvg(tr, period);
            var dm = high - high.ShiftRight(1);
            var adm = SmoothAvg(dm, period);
            return adm / atr * 100;
        }

        public static Series DMDn(Series high, Series low, Series close, int period)
        {
            var tr = TrueRange(high, low, close);
            var atr = SmoothAvg(tr, period);
            var dm = low.ShiftRight(1) - low;
            var adm = SmoothAvg(dm, period);
            return adm / atr * 100;
        }

        public static Series ADX(Series high, Series low, Series close, int period)
        {
            var up = DMUp(high, low, close, period);
            var dn = DMDn(high, low, close, period);
            return Abs(up - dn) / (up + dn) * 100;
        }

        public Series CrossesAbove(Series series)
        {
            Series output = new Series();
            Series[] input = RemoveHoles(new Series[] { this >> 1, series >> 1, this, series });
            for (int ii = 0; ii < input[0].Count; ii++)
            {
                output.Append((input[0][ii] <= input[1][ii] && input[2][ii] > input[3][ii]) ? 1 : 0);
            }
            return output.AddHoles(input);
        }

        public Series CrossesBelow(Series series)
        {
            Series output = new Series();
            Series[] input = RemoveHoles(new Series[] { this >> 1, series >> 1, this, series });
            for (int ii = 0; ii < input[0].Count; ii++)
            {
                output.Append((input[0][ii] >= input[1][ii] && input[2][ii] < input[3][ii]) ? 1 : 0);
            }
            return output.AddHoles(input);
        }

        public Series CrossesAbove(double value)
        {
            Series output = new Series();
            Series[] input = RemoveHoles(new Series[] { this >> 1, this });
            for (int ii = 0; ii < input[0].Count; ii++)
            {
                output.Append((input[0][ii] <= value && input[1][ii] > value) ? 1 : 0);
            }
            return output.AddHoles(input);
        }

        public Series CrossesBelow(double value)
        {
            Series output = new Series();
            Series[] input = RemoveHoles(new Series[] { this >> 1, this });
            for (int ii = 0; ii < input[0].Count; ii++)
            {
                output.Append((input[0][ii] >= value && input[1][ii] < value) ? 1 : 0);
            }
            return output.AddHoles(input);
        }

        public Series Crosses(Series series)
        {
            return CrossesAbove(series) | CrossesBelow(series);
        }

        public Series Crosses(double value)
        {
            return CrossesAbove(value) | CrossesBelow(value);
        }

        public Series IsRising()
        {
            return (this > (this >> 1));
        }

        public Series TurnsUp()
        {
            return (this > (this >> 1)).And((this >> 2) > (this >> 1));
        }

        public Series TurnsDown()
        {
            return (this < (this >> 1)).And((this >> 2) < (this >> 1));
        }

        public Series IsFalling()
        {
            return (this < (this >> 1));
        }

        public Series Higher(Series series)
        {
            Series output = new Series();
            Series[] input = RemoveHoles(new Series[] { this, series });
            for (int ii = 0; ii < input[0].Count; ii++)
            {
                output.Append(Math.Max(input[0][ii], input[1][ii]));
            }
            return output.AddHoles(input);
        }

        public Series Lower(Series series)
        {
            Series output = new Series();
            Series[] input = RemoveHoles(new Series[] { this, series });
            for (int ii = 0; ii < input[0].Count; ii++)
            {
                output.Append(Math.Min(input[0][ii], input[1][ii]));
            }
            return output.AddHoles(input);
        }

        public double Highest()
        {
            double max = double.NaN;
            Series input = RemoveHoles(this);
            for (int ii = 0; ii < input.Count; ii++)
            {
                if (double.IsNaN(max) || input[ii] > max)
                {
                    max = input[ii];
                }
            }
            return max;
        }

        public double Lowest()
        {
            double min = double.NaN;
            Series input = RemoveHoles(this);
            for (int ii = 0; ii < input.Count; ii++)
            {
                if (double.IsNaN(min) || input[ii] < min)
                {
                    min = input[ii];
                }
            }
            return min;
        }

        public Series SetMinimum(double minValue)
        {
            Series output = new Series();
            Series input = RemoveHoles(this);
            for (int ii = 0; ii < input.Count; ii++)
            {
                output.Append((input[ii] > minValue) ? input[ii] : minValue);
            }
            return output.AddHoles(input);
        }

        public Series SetMaximum(double maxValue)
        {
            Series output = new Series();
            Series input = RemoveHoles(this);
            for (int ii = 0; ii < input.Count; ii++)
            {
                output.Append((input[ii] < maxValue) ? input[ii] : maxValue);
            }
            return output.AddHoles(input);
        }

        public static Series Maximum(Series series, int period)
        {
            return series.Maximum(period);
        }

        public Series Maximum(int period)
        {
            Series output = new Series();
            Series input = RemoveHoles(this);
            for (int ii = 0; ii < input.Count; ii++)
            {
                double max = double.NaN;
                if (ii >= period)
                {
                    max = input[ii];
                    for (int jj = 1; jj < period; jj++)
                    {
                        if (input[ii - jj] > max)
                        {
                            max = input[ii - jj];
                        }
                    }
                }
                output.Append(max);
            }
            return output.AddHoles(input);
        }

        public static Series Minimum(Series series, int period)
        {
            return series.Minimum(period);
        }

        public Series Minimum(int period)
        {
            Series output = new Series();
            Series input = RemoveHoles(this);
            for (int ii = 0; ii < input.Count; ii++)
            {
                double min = double.NaN;
                if (ii >= period)
                {
                    min = input[ii];
                    for (int jj = 1; jj < period; jj++)
                    {
                        if (input[ii - jj] < min)
                        {
                            min = input[ii - jj];
                        }
                    }
                }
                output.Append(min);
            }
            return output.AddHoles(input);
        }

        public Series Abs()
        {
            Series output = new Series();
            Series input = RemoveHoles(this);
            for (int ii = 0; ii < input.Count; ii++)
            {
                output.Append(Math.Abs(input[ii]));
            }
            return output.AddHoles(input);
        }

        public static Series Abs(Series series)
        {
            return series.Abs();
        }

        public static Series Momentum(Series series, int period)
        {
            return series.Momentum(period);
        }

        public Series Momentum(int period)
        {
            Series output = new Series();
            Series input = RemoveHoles(this);
            double mom = double.NaN;
            for (int ii = 0; ii < input.Count; ii++)
            {
                if (ii >= period)
                {
                    mom = input[ii] - input[ii - period];
                }
                output.Append(mom);
            }
            return output.AddHoles(input);
        }

        public static Series SimpleMovingAverage(Series series, int period)
        {
            return series.SimpleMovingAverage(period);
        }

        public Series SimpleMovingAverage(int period)
        {
            Series output = new Series();
            Series input = RemoveHoles(this);
            double sum = 0;
            double average = double.NaN;
            for (int ii = 0; ii < input.Count; ii++)
            {
                if (ii >= period)
                {
                    sum -= input[ii - period];
                }
                sum += input[ii];

                if (ii >= period - 1)
                {
                    average = sum / period;
                }
                output.Append(average);
            }
            return output.AddHoles(input);
        }

        public static Series WMAvg(Series series, int period)
        {
            return series.WMAvg(period);
        }

        public Series WMAvg(int period)
        {
            List<double> weights = new List<double>();
            for (int ii = 0; ii < period; ii++)
            {
                weights.Add(ii + 1);
            }
            return weightedAverage(weights);
        }

        public static Series TMAvg(Series series, int period)
        {
            return series.TMAvg(period);
        }

        public Series TMAvg(int period)
        {
            List<double> weights = new List<double>();
            int weight = 1;
            for (int ii = 0; ii < period; ii++)
            {
                weights.Add(weight);
                weight = (2 * (ii + 1) == period) ? weight : ((ii < period / 2) ? weight + 1 : weight - 1);
            }
            return weightedAverage(weights);
        }

        private Series weightedAverage(List<double> weights)
        {
            Series output = new Series();
            Series input = RemoveHoles(this);

            int period = weights.Count;
            double weightSum = 0;
            for (int ii = 0; ii < period; ii++)
            {
                weightSum += weights[ii];
            }

            double average = double.NaN;

            for (int ii = 0; ii < input.Count; ii++)
            {
                if (weightSum != 0 && ii >= period)
                {
                    double inputSum = 0;
                    for (int jj = 0; jj < period; jj++)
                    {
                        inputSum += weights[period - 1 - jj] * input[ii - jj];
                    }
                    average = inputSum / weightSum;
                }

                output.Append(average);
            }
            return output.AddHoles(input);
        }

        public Series VMAvg(int period)
        {
            const double delta = 1E-10;
            const int varCount = 13;
            const double varFactor = 0.078;

            Series output = new Series();
            Series input = RemoveHoles(this);

            double average = double.NaN;

            for (int ii = 0; ii < input.Count; ii++)
            {
                if (ii == period - 1 + varCount)
                {
                    average = input[ii];
                }
                else if (ii > period - 1 + varCount)
                {
                    double df1 = 0;
                    double hi1 = double.MinValue;
                    double lo1 = double.MaxValue;
                    for (int jj = 0; jj < period; jj++)
                    {
                        double val1 = input[ii - (jj + 1)];
                        double val2 = input[ii - (jj + 2)];
                        df1 += Math.Abs(val1 - val2);
                        hi1 = (val1 > hi1) ? val1 : hi1;
                        lo1 = (val1 < lo1) ? val1 : lo1;
                    }

                    double df2 = 0;
                    double hi2 = double.MinValue;
                    double lo2 = double.MaxValue;
                    for (int jj = 0; jj < period; jj++)
                    {
                        double val1 = input[ii - (varCount - 1) - (jj + 1)];
                        double val2 = input[ii - (varCount - 1) - (jj + 2)];
                        df2 += Math.Abs(val1 - val2);
                        hi2 = (val1 > hi2) ? val1 : hi2;
                        lo2 = (val1 < lo2) ? val1 : lo2;
                    }

                    double ratio1 = (df1 == 0) ? 1 : (hi1 - lo1) / df1;
                    double ratio2 = (df2 == 0) ? 1 : (hi2 - lo2) / df2;
                    double factor = (ratio2 < delta) ? varFactor : varFactor * (ratio1 / ratio2);
                    average = factor * input[ii] + (1 - factor) * average;
                }

                output.Append(average);
            }
            return output.AddHoles(input);
        }

        public static Series SmoothAvg(Series series, int period)
        {
            return series.SmoothAvg(period);
        }

        public Series SmoothAvg(int period)
        {
            Series output = new Series();
            Series input = RemoveHoles(this);

            double sum = 0;
            double average = double.NaN;
            for (int ii = 0; ii < input.Count; ii++)
            {
                if (ii >= period)
                {
                    sum -= average;
                }
                sum += input[ii];

                if (ii >= period - 1)
                {
                    average = sum / period;
                }
                output.Append(average);
            }
            return output.AddHoles(input);
        }

        public static Series EMAvg(Series series, int period)
        {
            return series.EMAvg(period);
        }

        public Series EMAvg(int period)
        {
            Series output = new Series();
            Series input = RemoveHoles(this);
            double average = double.NaN;
            double factor = 2.0 / (period + 1.0);
            for (int ii = 0; ii < input.Count; ii++)
            {
                if (ii == 0)
                {
                    average = input[ii];
                }
                else
                {
                    average = (1.0 - factor) * average + factor * input[ii];
                }
                output.Append(average);
            }
            return output.AddHoles(input);
        }

        public Series movAvg(MAType maType, int period)
        {
            Series output = new Series();
            if (maType == MAType.Simple)
            {
                output = SimpleMovingAverage(period);
            }
            else if (maType == MAType.Exponential)
            {
                output = EMAvg(period);
            }
            else if (maType == MAType.Weighted)
            {
                output = WMAvg(period);
            }
            else if (maType == MAType.Triangular)
            {
                output = TMAvg(period);
            }
            else if (maType == MAType.Variable)
            {
                output = VMAvg(period);
            }
            return output;
        }

        public Series osc(OscType oscType, MAType maType1, MAType maType2, int period1, int period2)
        {
            Series output = new Series();
            Series ma1 = movAvg(maType1, period1);
            Series ma2 = movAvg(maType2, period2);
            if (oscType == OscType.Difference)
            {
                output = ma1 - ma2;
            }
            else if (oscType == OscType.Percentage)
            {
                output = ((ma1 - ma2) / ma2) * 100;
            }
            else if (oscType == OscType.Ratio)
            {
                output = ma1 / ma2;
            }
            return output;
        }

        public static Dictionary<string, Series> MAO(Series series, OscType oscType, MAType maType1, MAType maType2, MAType maType3, int period1, int period2, int period3)
        {
            Dictionary<string, Series> output = new Dictionary<string, Series>();
            output["Osc"] = series.osc(oscType, maType1, maType2, period1, period2);
            output["Signal"] = output["Osc"].movAvg(maType3, period3);
            output["Diff"] = output["Osc"] - output["Signal"];
            return output;
        }

        public static Dictionary<string, Series> Boll(Series series, int period, double upperFactor, double lowerFactor)
        {
            Dictionary<string, Series> output = new Dictionary<string, Series>();
            output["BollMavg"] = series.EMAvg(period);
            output["UpperBand"] = output["BollMavg"] + series.StdDev(period) * upperFactor;
            output["LowerBand"] = output["BollMavg"] - series.StdDev(period) * lowerFactor;
            output["Bandwidth"] = output["UpperBand"] - output["LowerBand"];
            output["PercentB"] = (series - output["LowerBand"]) / (output["UpperBand"] - output["LowerBand"]) * 100;
            return output;
        }

        public static Series RSI(Series series, int period)
        {
            Series mom = series.Momentum(1);
            Series up = mom.SetMinimum(0.0);
            Series dn = -mom.SetMaximum(0.0);
            Series upAvg = up.SmoothAvg(period);
            Series dnAvg = dn.SmoothAvg(period);
            Series rsi = (upAvg * 100) / (upAvg + dnAvg);
            return rsi;
        }

        public static Series LinearReg(Series series, int period)
        {
            return series.LinearReg(period);
        }

        public Series LinearReg(int period)
        {
            Series output = new Series();
            Series input = RemoveHoles(this);
            double regression = double.NaN;
            for (int ii = 0; ii < input.Count; ii++)
            {
                if (ii >= period - 1)
                {
                    double sumx = 0;
                    double sumy = 0;
                    double sumxy = 0;
                    double sumxx = 0;

                    for (int jj = 0; jj < period; jj++)
                    {
                        double x = jj;
                        double y = input[ii - (period - 1 - jj)];
                        sumx += x;
                        sumy += y;
                        sumxx += (x * x);
                        sumxy += (x * y);
                    }
                    double slope = ((period * sumxy) - (sumx * sumy)) / ((period * sumxx) - (sumx * sumx));
                    double intercept = (sumy - (slope * sumx)) / period;
                    regression = intercept + slope * (period - 1);
                }
                output.Append(regression);
            }
            return output.AddHoles(input);
        }

        public static Series CMCI(Series high, Series low, Series close, int period)
        {
            return Series.CPI(Series.HLCAvg(high, low, close), period);
        }

        public static Series CPI(Series series, int period)
        {
            return series.CPI(period);
        }

        public Series CPI(int period)
        {
            Series output = new Series();
            Series input = RemoveHoles(this);
            double sum = 0;
            double CPI = double.NaN;
            for (int ii = 0; ii < input.Count; ii++)
            {
                if (ii >= period)
                {
                    sum -= input[ii - period];
                }
                sum += input[ii];

                if (ii >= period - 1)
                {
                    double average = sum / period;

                    double CPIAcc = 0.0;
                    for (int jj = 0; jj < period; jj++)
                    {
                        CPIAcc += Math.Abs(average - input[ii - jj]);
                    }
                    double meanDev = (CPIAcc / period) * 0.015;
                    if (meanDev < -(1E-100) || meanDev > 1E-100)
                    {
                        CPI = (input[ii] - average) / (meanDev);
                    }

                }
                output.Append(CPI);
            }
            return output.AddHoles(input);
        }

        public static Series StdDev(Series series, int period)
        {
            return series.StdDev(period);
        }

        public Series StdDev(int period)
        {
            Series output = new Series();
            Series input = RemoveHoles(this);
            double sum = 0;
            double average = double.NaN;
            double deviation = double.NaN;
            for (int ii = 0; ii < input.Count; ii++)
            {
                if (ii >= period)
                {
                    sum -= input[ii - period];
                }
                sum += input[ii];

                if (ii >= period - 1)
                {
                    average = sum / period;
                    double accum = 0;
                    for (int jj = 0; jj < period; jj++)
                    {
                        double dif = input[ii - jj] - average;
                        accum += (dif * dif);
                    }
                    deviation = Math.Sqrt(accum / period);
                }
                output.Append(deviation);
            }
            return output.AddHoles(input);
        }
    }
}

