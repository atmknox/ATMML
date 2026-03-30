using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;

namespace ATMML
{
    public class FastTrigTPOp
    {
        int m_FK1_period = 8;
        int m_FK2_period = 8;
        double m_EA1_factor = 0.5;
        double m_EA2_factor = 0.5;
        int m_LR_period = 5;
        double m_LR_s2 = 0;
        double m_LR_s4 = 0;
        int m_SA_period = 2;
        double m_tickSize = .01;
        bool m_history = true;
        List<FastTrigTPOpState> m_state = new List<FastTrigTPOpState>();

        Series oldHigh = new Series();
        Series oldLow = new Series();
        Series oldClose = new Series();

        private class FastTrigTPOpState
        {
            public double prvHigh = Double.NaN;
            public double prvLow = Double.NaN;
            public double prvClose = Double.NaN;
            public int prvDir = 0;
            public double prvTo = Double.NaN;
            public Queue<double> to = new Queue<double>();
            public double FK1_max = Double.NaN;
            public double FK1_min = Double.NaN;
            public Queue<double> FK1_hiVal = new Queue<double>();
            public Queue<double> FK1_loVal = new Queue<double>();
            public double FK2_max = Double.NaN;
            public double FK2_min = Double.NaN;
            public Queue<double> FK2_hiVal = new Queue<double>();
            public Queue<double> FK2_loVal = new Queue<double>();
            public double EA1_avg = Double.NaN;
            public double EA2_avg = Double.NaN;
            public double LR_s1 = 0;
            public double LR_s3 = 0;
            public Queue<double> LR_inVal = new Queue<double>();
            public double SA_acc = 0;
            public double SA_cnt = 0;
            public double SA_avg = Double.NaN;

            public FastTrigTPOpState()
            {
            }

            public FastTrigTPOpState(FastTrigTPOpState other)
            {
                Copy(other);
            }

            public void Clear()
            {
                prvHigh = Double.NaN;
                prvLow = Double.NaN;
                prvClose = Double.NaN;
                prvDir = 0;
                prvTo = Double.NaN;
                to.Clear();
                FK1_max = Double.NaN;
                FK1_min = Double.NaN;
                FK1_hiVal.Clear();
                FK1_loVal.Clear();
                FK2_max = Double.NaN;
                FK2_min = Double.NaN;
                FK2_hiVal.Clear();
                FK2_loVal.Clear();
                EA1_avg = Double.NaN;
                EA2_avg = Double.NaN;
                LR_s1 = 0;
                LR_s3 = 0;
                LR_inVal.Clear();
                SA_acc = 0;
                SA_cnt = 0;
                SA_avg = Double.NaN;
            }

            public void Copy(FastTrigTPOpState other)
            {
                prvHigh = other.prvHigh;
                prvLow = other.prvLow;
                prvClose = other.prvClose;
                prvDir = other.prvDir;
                prvTo = other.prvTo;
                to = new Queue<double>(other.to);
                FK1_max = other.FK1_max;
                FK1_min = other.FK1_min;
                FK1_hiVal = new Queue<double>(other.FK1_hiVal);
                FK1_loVal = new Queue<double>(other.FK1_loVal);
                FK2_max = other.FK2_max;
                FK2_min = other.FK2_min;
                FK2_hiVal = new Queue<double>(other.FK2_hiVal);
                FK2_loVal = new Queue<double>(other.FK2_loVal);
                EA1_avg = other.EA1_avg;
                EA2_avg = other.EA2_avg;
                LR_s1 = other.LR_s1;
                LR_s3 = other.LR_s3;
                LR_inVal = new Queue<double>(other.LR_inVal);
                SA_acc = other.SA_acc;
                SA_cnt = other.SA_cnt;
                SA_avg = other.SA_avg;
            }
        }

        public FastTrigTPOp()
        {
            m_state.Add(new FastTrigTPOpState());
            m_state.Add(new FastTrigTPOpState());

            for (int ii = 1; ii <= m_LR_period; ii++)
            {
                m_LR_s2 += ii;
                m_LR_s4 += ii * ii;
            }
        }

        public bool OkToCalculateLastOnly(Series newHigh, Series newLow, Series newClose)
        {
            bool ok = false;
            if (newClose.Count == oldClose.Count)
            {
                ok = true;
                for (int ii = 0; ii < newClose.Count - 1; ii++)
                {
                    if (newHigh[ii] != oldHigh[ii] || newLow[ii] != oldLow[ii] || newClose[ii] != oldClose[ii])
                    {
                        ok = false;
                        break;
                    }
                }
            }

            if (ok)
            {
                m_state[0].Copy(m_state[1]);
            }
            else
            {
                m_state[0].Clear();
                oldHigh = newHigh;
                oldLow = newLow;
                oldClose = newClose;
            }

            return ok;
        }

        public void Calculate(
            double[] input,  // three inputs (high, low, and close)
            double[] output, // three outputs (tp1, tp2, and to)
            bool lastBar)
        {
            if (lastBar)
            {
                m_state[1].Copy(m_state[0]);
            }

            FastTrigTPOpState state = m_state[0];

            FastTrigTPOpState tryState = new FastTrigTPOpState(state);

            double trigTPUp = Double.NaN;
            double trigTPDn = Double.NaN;
            double curTo = Double.NaN;
            bool limitFlag = false;
            bool changeInDirection = false;

            if (Double.IsNaN(input[0])) input[0] = state.prvHigh;
            if (Double.IsNaN(input[1])) input[1] = state.prvLow;
            if (Double.IsNaN(input[2])) input[2] = state.prvClose;

            double high = input[0];
            double low = input[1];
            double close = input[2];

            if (!Double.IsNaN(high) && !Double.IsNaN(low) && !Double.IsNaN(close))
            {
                curTo = calculateTriggerOscillator(high, low, close, state);

                if (Double.IsNaN(curTo))
                {
                    curTo = state.prvTo;
                }
                if (!Double.IsNaN(curTo))
                {
                    state.to.Enqueue(curTo);
                    if (state.to.Count > 12)
                    {
                        state.to.Dequeue();
                    }
                }

                if (m_history || lastBar)
                {
                    double[] tosc = state.to.ToArray();
                    int toCnt = tosc.Length;

                    int dir = 0;
                    double prvTo = Double.NaN;
                    if (toCnt > 0)
                    {
                        for (int ii = toCnt - 2; ii >= 0; ii--)
                        {
                            prvTo = tosc[ii];
                            if (prvTo < curTo)
                            {
                                dir = 1;
                                break;
                            }
                            else if (prvTo > curTo)
                            {
                                dir = -1;
                                break;
                            }
                        }
                    }

                    if (dir == -1)
                    {
                        int min = 0;
                        int max = 1;
                        int limit = -1;
                        double to = Double.NaN;
                        for (int ii = 0; ii < 20; ii++)
                        {
                            double cl = close + max * m_tickSize;
                            double hi = (cl > high) ? cl : high;
                            double lo = low;

                            FastTrigTPOpState st = new FastTrigTPOpState(tryState);

                            double pTo = to;
                            to = calculateTriggerOscillator(hi, lo, cl, st);

                            if (!double.IsNaN(pTo))
                            {
                                if (limit == -1 && pTo == to) limit = min;
                                if (pTo != to) limit = -1;
                                if (to >= prvTo) break;
                            }
                            min = max;
                            max = 2 * max;
                        }

                        if (to < prvTo && limit != -1)
                        {
                            to = prvTo;
                            max = limit;
                            min = limit / 2;
                            limitFlag = true;
                        }

                        if (to >= prvTo)
                        {
                            for (int ii = 0; ii < 1000 && max - min > 1; ii++)
                            {
                                int tryMe = ((max + min) + 1) / 2;
                                double cl = close + tryMe * m_tickSize;
                                double hi = (cl > high) ? cl : high;
                                double lo = low;

                                FastTrigTPOpState st = new FastTrigTPOpState(tryState);

                                to = calculateTriggerOscillator(hi, lo, cl, st);

                                if (to >= prvTo) max = tryMe;
                                else min = tryMe;
                            }
                            trigTPUp = close + max * m_tickSize;
                        }
                    }

                    if (dir == 1)
                    {
                        int min = 0;
                        int max = 1;
                        int limit = -1;
                        double to = Double.NaN;
                        for (int ii = 0; ii < 40; ii++)
                        {
                            double cl = close - max * m_tickSize;
                            double hi = high;
                            double lo = (cl < low) ? cl : low;

                            FastTrigTPOpState st = new FastTrigTPOpState(tryState);

                            double pTo = to;
                            to = calculateTriggerOscillator(hi, lo, cl, st);

                            if (!double.IsNaN(pTo))
                            {
                                if (limit == -1 && pTo == to) limit = min;
                                if (pTo != to) limit = -1;
                                if (to <= prvTo) break;
                            }
                            min = max;
                            max = 2 * max;
                        }

                        if (to > prvTo && limit != -1)
                        {
                            to = prvTo;
                            max = limit;
                            min = limit / 2;
                            limitFlag = true;
                        }

                        if (to <= prvTo)
                        {
                            for (int ii = 0; ii < 1000 && max - min > 1; ii++ )
                            {
                                int tryMe = ((max + min) + 1) / 2;
                                double cl = close - tryMe * m_tickSize;
                                double hi = high;
                                double lo = (cl < low) ? cl : low;

                                FastTrigTPOpState st = new FastTrigTPOpState(tryState);

                                to = calculateTriggerOscillator(hi, lo, cl, st);

                                if (to <= prvTo) max = tryMe;
                                else min = tryMe;
                            }
                            trigTPDn = close - max * m_tickSize;
                        }
                    }

                    if (dir != state.prvDir)
                    {
                        changeInDirection = true;
                    }
                    state.prvDir = dir;

                    state.prvHigh = high;
                    state.prvLow = low;
                    state.prvClose = close;
                }

                output[0] = limitFlag ? Double.NaN : ((changeInDirection && !Double.IsNaN(trigTPDn)) ? trigTPDn : trigTPUp);			// curve1  tp up
                output[1] = limitFlag ? Double.NaN : ((changeInDirection && !Double.IsNaN(trigTPUp)) ? trigTPUp : trigTPDn);			// curve2  tp dn

                state.prvTo = curTo;
            }
        }

        double calculateLinearRegression
            (int period,
             double s2,
             double s4,
             ref double s1,
             ref double s3,
             Queue<double> inVal,
             double input)
        {
            double output = Double.NaN;
            if (!Double.IsNaN(input))
            {
                inVal.Enqueue(input);
                if (inVal.Count <= period)
                {
                    s1 += input * inVal.Count;
                    s3 += input;
                }
                else
                {
                    if (inVal.Count > period + 1) inVal.Dequeue();
                    s1 += input * period - s3;
                    s3 += input - inVal.Peek();
                }
                double slope = Double.NaN;
                if ((s4 * period - s2 * s2) < -(1E-100) || (s4 * period - s2 * s2) > 1E-100)
                    slope = (s1 * period - s3 * s2) / (s4 * period - s2 * s2);
                double intercept = Double.NaN;
                if (period != 0)
                    intercept = (s3 - slope * s2) / period;
                output = intercept + slope * period;
            }
            return output;
        }

        double calculateExponentialAverage
            (double factor,
            ref double average,
            double input)
        {
            double output = Double.NaN;
            if (!Double.IsNaN(input))
            {
                double val = (!Double.IsNaN(average)) ? average : input;
                average = val + factor * (input - val);
                output = average;
            }
            return output;
        }

        double calculateSmoothedAverage
            (int period,
             ref double accumulator,
             ref double validCount,
             ref double average,
             double input)
        {
            double output = Double.NaN;
            if (!Double.IsNaN(input))
            {
                if (Double.IsNaN(average))
                {
                    accumulator += input;
                    if (++validCount == period && period != 0)
                    {
                        average = accumulator / period;
                        output = average;
                    }
                }
                else
                {
                    if (period != 0)
                        average += (input - average) / period;
                    output = average;
                }
            }
            return output;
        }

        double calculateFastK
            (int period,
             ref double max,
             ref double min,
             Queue<double> hiVal,
             Queue<double> loVal,
             double high,
             double low,
             double input)
        {
            double output = Double.NaN;
            if (!Double.IsNaN(input) && !Double.IsNaN(high) && !Double.IsNaN(low))
            {
                bool recalcMax = true;
                bool recalcMin = true;
                if (Double.IsNaN(max) ||
                    (hiVal.Count < period && high > max))
                {
                    max = high;
                    recalcMax = false;
                }
                else if (hiVal.Peek() == max)
                {
                    if (high >= max)
                    {
                        max = high;
                        recalcMax = false;
                    }
                }
                if (Double.IsNaN(min) ||
                    (loVal.Count < period && low < min))
                {
                    min = low;
                    recalcMin = false;
                }
                else if (loVal.Peek() == min)
                {
                    if (low <= min)
                    {
                        min = low;
                        recalcMin = false;
                    }
                }
                hiVal.Enqueue(high);
                if (hiVal.Count > period) hiVal.Dequeue();
                loVal.Enqueue(low);
                if (loVal.Count > period) loVal.Dequeue();
                if (recalcMax)
                {
                    double[] hi = hiVal.ToArray();
                    if (0 < period && period <= hi.Length)
                    {
                        for (int ii = hi.Length - 1, jj = 0; jj < period; ii--, jj++)
                        {
                            if (jj == 0 || hi[ii] > max)
                            {
                                max = hi[ii];
                            }
                        }
                    }
                }
                if (recalcMin)
                {
                    double[] lo = loVal.ToArray();
                    if (0 < period && period <= lo.Length)
                    {
                        for (int ii = lo.Length - 1, jj = 0; jj < period; ii--, jj++)
                        {
                            if (jj == 0 || lo[ii] < min)
                            {
                                min = lo[ii];
                            }
                        }
                    }
                }
                if (!Double.IsNaN(period) && hiVal.Count >= period)
                {
                    output = ((max - min) < -(1E-100) || (max - min) > 1E-100) ? 100.0 * (input - min) / (max - min) : 50.0;
                }
            }
            return output;
        }

        double calculateTriggerOscillator
            (double hi,
             double lo,
             double cl,
             FastTrigTPOpState st)
        {
            double fk1 = calculateFastK(m_FK1_period, ref st.FK1_max, ref st.FK1_min, st.FK1_hiVal, st.FK1_loVal, hi, lo, cl);
            double ea1 = calculateExponentialAverage(m_EA1_factor, ref st.EA1_avg, fk1);
            double fk2 = calculateFastK(m_FK2_period, ref st.FK2_max, ref st.FK2_min, st.FK2_hiVal, st.FK2_loVal, ea1, ea1, ea1);
            double ea2 = calculateExponentialAverage(m_EA2_factor, ref st.EA2_avg, fk2);
            double lr = calculateLinearRegression(m_LR_period, m_LR_s2, m_LR_s4, ref st.LR_s1, ref st.LR_s3, st.LR_inVal, ea2);
            double to = calculateSmoothedAverage(m_SA_period, ref st.SA_acc, ref st.SA_cnt, ref st.SA_avg, lr);
            return to;
        }
    }

    public class SlowTrigTPOp
    {
        int m_FK1_period = 21;
        int m_MA1_period = 2;
        int m_LR_period = 8;
        double m_LR_s2 = 0;
        double m_LR_s4 = 0;
        int m_SA_period = 2;
        double m_tickSize = .01;
        bool m_history = true;
        List<SlowTrigTPOpState> m_state = new List<SlowTrigTPOpState>();

        Series oldHigh = new Series();
        Series oldLow = new Series();
        Series oldClose = new Series();

        private class SlowTrigTPOpState
        {
            public double prvHigh = Double.NaN;
            public double prvLow = Double.NaN;
            public double prvClose = Double.NaN;
            public int prvDir = 0;
            public double prvTo = Double.NaN;
            public Queue<double> to = new Queue<double>();
            public double FK1_max = Double.NaN;
            public double FK1_min = Double.NaN;
            public Queue<double> FK1_hiVal = new Queue<double>();
            public Queue<double> FK1_loVal = new Queue<double>();
            public Queue<double> FK1_upperVal = new Queue<double>();
            public Queue<double> FK1_lowerVal = new Queue<double>();
            public double LR_s1 = 0;
            public double LR_s3 = 0;
            public Queue<double> LR_inVal = new Queue<double>();
            public double SA_acc = 0;
            public double SA_cnt = 0;
            public double SA_avg = Double.NaN;

            public SlowTrigTPOpState()
            {
            }

            public SlowTrigTPOpState(SlowTrigTPOpState other)
            {
                Copy(other);
            }

            public void Clear()
            {
                prvHigh = Double.NaN;
                prvLow = Double.NaN;
                prvClose = Double.NaN;
                prvDir = 0;
                prvTo = Double.NaN;
                to.Clear();
                FK1_max = Double.NaN;
                FK1_min = Double.NaN;
                FK1_hiVal.Clear();
                FK1_loVal.Clear();
                FK1_upperVal.Clear();
                FK1_lowerVal.Clear();
                LR_s1 = 0;
                LR_s3 = 0;
                LR_inVal.Clear();
                SA_acc = 0;
                SA_cnt = 0;
                SA_avg = Double.NaN;
            }

            public void Copy(SlowTrigTPOpState other)
            {
                prvHigh = other.prvHigh;
                prvLow = other.prvLow;
                prvClose = other.prvClose;
                prvDir = other.prvDir;
                prvTo = other.prvTo;
                to = new Queue<double>(other.to);
                FK1_max = other.FK1_max;
                FK1_min = other.FK1_min;
                FK1_hiVal = new Queue<double>(other.FK1_hiVal);
                FK1_loVal = new Queue<double>(other.FK1_loVal);
                FK1_upperVal = new Queue<double>(other.FK1_upperVal);
                FK1_lowerVal = new Queue<double>(other.FK1_lowerVal);
                LR_s1 = other.LR_s1;
                LR_s3 = other.LR_s3;
                LR_inVal = new Queue<double>(other.LR_inVal);
                SA_acc = other.SA_acc;
                SA_cnt = other.SA_cnt;
                SA_avg = other.SA_avg;
            }
        }

        public SlowTrigTPOp()
        {
            m_state.Add(new SlowTrigTPOpState());
            m_state.Add(new SlowTrigTPOpState());

            for (int ii = 1; ii <= m_LR_period; ii++)
            {
                m_LR_s2 += ii;
                m_LR_s4 += ii * ii;
            }
        }

        public void Calculate(
            double[] input,  // three inputs (high, low, and close)
            double[] output, // three outputs (tp1, tp2, and to)
            bool lastBar)
        {
            if (lastBar)
            {
                m_state[1].Copy(m_state[0]);
            }

            SlowTrigTPOpState state = m_state[0];

            SlowTrigTPOpState tryState = new SlowTrigTPOpState(state);

            double trigTPUp = Double.NaN;
            double trigTPDn = Double.NaN;
            double curTo = Double.NaN;
            bool limitFlag = false;
            bool changeInDirection = false;

            if (Double.IsNaN(input[0])) input[0] = state.prvHigh;
            if (Double.IsNaN(input[1])) input[1] = state.prvLow;
            if (Double.IsNaN(input[2])) input[2] = state.prvClose;

            double high = input[0];
            double low = input[1];
            double close = input[2];

            if (!Double.IsNaN(high) && !Double.IsNaN(low) && !Double.IsNaN(close))
            {
                curTo = calculateTriggerOscillator(high, low, close, state);

                if (Double.IsNaN(curTo))
                {
                    curTo = state.prvTo;
                }
                if (!Double.IsNaN(curTo))
                {
                    state.to.Enqueue(curTo);
                    if (state.to.Count > 12)
                    {
                        state.to.Dequeue();
                    }
                }

                if (m_history || lastBar)
                {
                    double[] tosc = state.to.ToArray();
                    int toCnt = tosc.Length;

                    int dir = 0;
                    double prvTo = Double.NaN;
                    if (toCnt > 0)
                    {
                        for (int ii = toCnt - 2; ii >= 0; ii--)
                        {
                            prvTo = tosc[ii];
                            if (prvTo < curTo)
                            {
                                dir = 1;
                                break;
                            }
                            else if (prvTo > curTo)
                            {
                                dir = -1;
                                break;
                            }
                        }
                    }

                    if (dir == -1)
                    {
                        double min = 0;
                        double max = 1;
                        double limit = -1;
                        double to = Double.NaN;
                        for (int ii = 0; ii < 100; ii++)
                        {
                            double cl = close + max * m_tickSize;
                            double hi = (cl > high) ? cl : high;
                            double lo = low;

                            SlowTrigTPOpState st = new SlowTrigTPOpState(tryState);

                            double pTo = to;
                            to = calculateTriggerOscillator(hi, lo, cl, st);

                            if (limit == -1 && pTo == to) limit = min;
                            if (pTo != to) limit = -1;
                            if (to >= prvTo) break;
                            min = max;
                            max = 2 * max;
                        }

                        if (to < prvTo && limit != -1)
                        {
                            to = prvTo;
                            max = limit;
                            min = limit / 2;
                            limitFlag = true;
                        }

                        if (to >= prvTo)
                        {
                            for (int ii = 0; ii < 1000 && max - min > 1; ii++)
                            {
                                double tryMe = ((max + min) + 1) / 2;
                                double cl = close + tryMe * m_tickSize;
                                double hi = (cl > high) ? cl : high;
                                double lo = low;

                                SlowTrigTPOpState st = new SlowTrigTPOpState(tryState);

                                to = calculateTriggerOscillator(hi, lo, cl, st);

                                if (to >= prvTo) max = tryMe;
                                else min = tryMe;
                            }
                            trigTPUp = close + max * m_tickSize;
                        }
                    }

                    if (dir == 1)
                    {
                        double min = 0;
                        double max = 1;
                        double limit = -1;
                        double to = Double.NaN;
                        for (int ii = 0; ii < 100; ii++)
                        {
                            double cl = close - max * m_tickSize;
                            double hi = high;
                            double lo = (cl < low) ? cl : low;

                            SlowTrigTPOpState st = new SlowTrigTPOpState(tryState);

                            double pTo = to;
                            to = calculateTriggerOscillator(hi, lo, cl, st);

                            if (limit == -1 && pTo == to) limit = min;
                            if (pTo != to) limit = -1;
                            if (to <= prvTo) break;
                            min = max;
                            max = 2 * max;
                        }

                        if (to > prvTo && limit != -1)
                        {
                            to = prvTo;
                            max = limit;
                            min = limit / 2;
                            limitFlag = true;
                        }

                        if (to <= prvTo)
                        {
                            for (int ii = 0; ii < 1000 && max - min > 1; ii++)
                            {
                                double tryMe = ((max + min) + 1) / 2;
                                double cl = close - tryMe * m_tickSize;
                                double hi = high;
                                double lo = (cl < low) ? cl : low;

                                SlowTrigTPOpState st = new SlowTrigTPOpState(tryState);

                                to = calculateTriggerOscillator(hi, lo, cl, st);

                                if (to <= prvTo) max = tryMe;
                                else min = tryMe;
                            }
                            trigTPDn = close - max * m_tickSize;
                        }
                    }

                    if (dir != state.prvDir)
                    {
                        changeInDirection = true;
                    }
                    state.prvDir = dir;

                    state.prvHigh = high;
                    state.prvLow = low;
                    state.prvClose = close;
                }

                output[0] = limitFlag ? Double.NaN : ((changeInDirection && !Double.IsNaN(trigTPDn)) ? trigTPDn : trigTPUp);			// curve1
                output[1] = limitFlag ? Double.NaN : ((changeInDirection && !Double.IsNaN(trigTPUp)) ? trigTPUp : trigTPDn);			// curve2

                state.prvTo = curTo;
            }
        }

        double calculateLinearRegression
            (int period,
             double s2,
             double s4,
             ref double s1,
             ref double s3,
             Queue<double> inVal,
             double input)
        {
            double output = Double.NaN;
            if (!Double.IsNaN(input))
            {
                inVal.Enqueue(input);
                if (inVal.Count <= period)
                {
                    s1 += input * inVal.Count;
                    s3 += input;
                }
                else
                {
                    if (inVal.Count > period + 1) inVal.Dequeue();
                    s1 += input * period - s3;
                    s3 += input - inVal.Peek();
                }
                double slope = Double.NaN;
                if ((s4 * period - s2 * s2) < -(1E-100) || (s4 * period - s2 * s2) > 1E-100)
                    slope = (s1 * period - s3 * s2) / (s4 * period - s2 * s2);
                double intercept = Double.NaN;
                if (period != 0)
                    intercept = (s3 - slope * s2) / period;
                output = intercept + slope * period;
            }
            return output;
        }

        double calculateSmoothedAverage
            (int period,
             ref double accumulator,
             ref double validCount,
             ref double average,
             double input)
        {
            double output = Double.NaN;
            if (!Double.IsNaN(input))
            {
                if (Double.IsNaN(average))
                {
                    accumulator += input;
                    if (++validCount == period && period != 0)
                    {
                        average = accumulator / period;
                        output = average;
                    }
                }
                else
                {
                    if (period != 0)
                        average += (input - average) / period;
                    output = average;
                }
            }
            return output;
        }

        double calculateFastK
            (int period,
             ref double max,
             ref double min,
             Queue<double> hiVal,
             Queue<double> loVal,
             int maPeriod,
             Queue<double> upperVal,
             Queue<double> lowerVal,
             double high,
             double low,
             double input)
        {
            double output = Double.NaN;
            if (!Double.IsNaN(input) && !Double.IsNaN(high) && !Double.IsNaN(low))
            {
                bool recalcMax = true;
                bool recalcMin = true;
                if (Double.IsNaN(max) ||
                    (hiVal.Count < period && high > max))
                {
                    max = high;
                    recalcMax = false;
                }
                else if (hiVal.Peek() == max)
                {
                    if (high >= max)
                    {
                        max = high;
                        recalcMax = false;
                    }
                }
                if (Double.IsNaN(min) ||
                    (loVal.Count < period && low < min))
                {
                    min = low;
                    recalcMin = false;
                }
                else if (loVal.Peek() == min)
                {
                    if (low <= min)
                    {
                        min = low;
                        recalcMin = false;
                    }
                }
                hiVal.Enqueue(high);
                if (hiVal.Count > period) hiVal.Dequeue();
                loVal.Enqueue(low);
                if (loVal.Count > period) loVal.Dequeue();
                if (recalcMax)
                {
                    double[] hi = hiVal.ToArray();
                    if (0 < period && period <= hi.Length)
                    {
                        for (int ii = hi.Length - 1, jj = 0; jj < period; ii--, jj++)
                        {
                            if (jj == 0 || hi[ii] > max)
                            {
                                max = hi[ii];
                            }
                        }
                    }
                }
                if (recalcMin)
                {
                    double[] lo = loVal.ToArray();
                    if (0 < period && period <= lo.Length)
                    {
                        for (int ii = lo.Length - 1, jj = 0; jj < period; ii--, jj++)
                        {
                            if (jj == 0 || lo[ii] < min)
                            {
                                min = lo[ii];
                            }
                        }
                    }
                }
                if (!Double.IsNaN(period) && hiVal.Count >= period)
                {
                    upperVal.Enqueue(input - min);
                    if (upperVal.Count > maPeriod) upperVal.Dequeue();
                    lowerVal.Enqueue(max - min);
                    if (lowerVal.Count > maPeriod) lowerVal.Dequeue();
                    if (!Double.IsNaN(maPeriod) && upperVal.Count >= maPeriod && lowerVal.Count >= maPeriod)
                    {
                        double upper = 0;
                        double lower = 0;
                        IEnumerator<double> upperEnum = upperVal.GetEnumerator();
                        IEnumerator<double> lowerEnum = lowerVal.GetEnumerator();
                        for (int ii = 0; ii < maPeriod; ii++)
                        {
                            upperEnum.MoveNext();
                            lowerEnum.MoveNext();
                            upper += upperEnum.Current;
                            lower += lowerEnum.Current;
                        }
                        output = (lower < -(1E-100) || lower > 1E-100) ? 100.0 * upper / lower : 50.0;
                    }
                }
            }
            return output;
        }

        double calculateTriggerOscillator
            (double hi,
             double lo,
             double cl,
             SlowTrigTPOpState st)
        {
            double fk1 = calculateFastK(m_FK1_period, ref st.FK1_max, ref st.FK1_min, st.FK1_hiVal, st.FK1_loVal, m_MA1_period, st.FK1_upperVal, st.FK1_lowerVal, hi, lo, cl);
            double lr = calculateLinearRegression(m_LR_period, m_LR_s2, m_LR_s4, ref st.LR_s1, ref st.LR_s3, st.LR_inVal, fk1);
            double to = calculateSmoothedAverage(m_SA_period, ref st.SA_acc, ref st.SA_cnt, ref st.SA_avg, lr);
            return to;
        }
    }

    public class TRTTPOp
    {
        int m_LR1_period = 5;
        double m_LR1_s2 = 0;
        double m_LR1_s4 = 0;
        int m_LR2_period = 5;
        double m_LR2_s2 = 0;
        double m_LR2_s4 = 0;
        int m_SA_period = 3;
        double m_tickSize = .01;
        bool m_history = true;
        List<TRTTPOpState> m_state = new List<TRTTPOpState>();

        Series oldHigh = new Series();
        Series oldLow = new Series();
        Series oldClose = new Series();

        private class TRTTPOpState
        {

            public double prvHigh = Double.NaN;
            public double prvLow = Double.NaN;
            public double prvClose = Double.NaN;

            public int prvDir = 0;
            public double prvTo = Double.NaN;
            public Queue<double> to = new Queue<double>();
     
            public double LR1_s1 = 0;
            public double LR1_s3 = 0;
            public Queue<double> LR1_inVal = new Queue<double>();
            public double LR2_s1 = 0;
            public double LR2_s3 = 0;
            public Queue<double> LR2_inVal = new Queue<double>();
            public double SA1_acc = 0;
            public double SA1_cnt = 0;
            public double SA1_avg = Double.NaN;
            public double SA2_acc = 0;
            public double SA2_cnt = 0;
            public double SA2_avg = Double.NaN;

            public double lr1;

            public TRTTPOpState()
            {
            }

            public TRTTPOpState(TRTTPOpState other)
            {
                Copy(other);
            }

            public void Clear()
            {
                prvHigh = Double.NaN;
                prvLow = Double.NaN;
                prvClose = Double.NaN;
                prvDir = 0;
                prvTo = Double.NaN;
                to.Clear();
                LR1_s1 = 0;
                LR1_s3 = 0;
                LR1_inVal.Clear();
                LR2_s1 = 0;
                LR2_s3 = 0;
                LR2_inVal.Clear();
                SA1_acc = 0;
                SA1_cnt = 0;
                SA1_avg = Double.NaN;
                SA2_acc = 0;
                SA2_cnt = 0;
                SA2_avg = Double.NaN;
                lr1 = Double.NaN;
            }

            public void Copy(TRTTPOpState other)
            {
                prvHigh = other.prvHigh;
                prvLow = other.prvLow;
                prvClose = other.prvClose;
                prvDir = other.prvDir;
                prvTo = other.prvTo;
                to = new Queue<double>(other.to);
                LR1_s1 = other.LR1_s1;
                LR1_s3 = other.LR1_s3;
                LR1_inVal = new Queue<double>(other.LR1_inVal);
                LR2_s1 = other.LR2_s1;
                LR2_s3 = other.LR2_s3;
                LR2_inVal = new Queue<double>(other.LR2_inVal);
                SA1_acc = other.SA1_acc;
                SA1_cnt = other.SA1_cnt;
                SA1_avg = other.SA1_avg;
                SA2_acc = other.SA2_acc;
                SA2_cnt = other.SA2_cnt;
                SA2_avg = other.SA2_avg;
            }
        }

        public TRTTPOp()
        {
            m_state.Add(new TRTTPOpState());
            m_state.Add(new TRTTPOpState());

            for (int ii = 1; ii <= m_LR1_period; ii++)
            {
                m_LR1_s2 += ii;
                m_LR1_s4 += ii * ii;
            }

            for (int ii = 1; ii <= m_LR2_period; ii++)
            {
                m_LR2_s2 += ii;
                m_LR2_s4 += ii * ii;
            }
        }

        public void Calculate(
            double[] input,  // three inputs (high, low, and close)
            double[] output, // three outputs (tp1, tp2, and to)
            bool lastBar)
        {
            if (lastBar)
            {
                m_state[1].Copy(m_state[0]);
            }

            TRTTPOpState state = m_state[0];

            TRTTPOpState tryState = new TRTTPOpState(state);

            double trigTPUp = Double.NaN;
            double trigTPDn = Double.NaN;
            double curTo = Double.NaN;
            bool limitFlag = false;
            bool changeInDirection = false;

            if (Double.IsNaN(input[0])) input[0] = state.prvHigh;
            if (Double.IsNaN(input[1])) input[1] = state.prvLow;
            if (Double.IsNaN(input[2])) input[2] = state.prvClose;

            double high = input[0];
            double low = input[1];
            double close = input[2];

            if (!Double.IsNaN(high) && !Double.IsNaN(low) && !Double.IsNaN(close))
            {
                curTo = calculateTRT(close, state);

                if (Double.IsNaN(curTo))
                {
                    curTo = state.prvTo;
                }
                if (!Double.IsNaN(curTo))
                {
                    state.to.Enqueue(curTo);
                    if (state.to.Count > 12)
                    {
                        state.to.Dequeue();
                    }
                }

                if (m_history || lastBar)
                {
                    double[] tosc = state.to.ToArray();
                    int toCnt = tosc.Length;

                    int dir = 0;
                    double prvTo = Double.NaN;
                    if (toCnt > 0)
                    {
                        for (int ii = toCnt - 2; ii >= 0; ii--)
                        {
                            prvTo = tosc[ii];
                            if (prvTo < curTo)
                            {
                                dir = 1;
                                break;
                            }
                            else if (prvTo > curTo)
                            {
                                dir = -1;
                                break;
                            }
                        }
                    }

                    if (dir == -1)
                    {
                        int min = 0;
                        int max = 1;
                        int limit = -1;
                        double to = Double.NaN;
                        for (int ii = 0; ii < 20; ii++)
                        {
                            double cl = close + max * m_tickSize;
                            double hi = (cl > high) ? cl : high;
                            double lo = low;

                            TRTTPOpState st = new TRTTPOpState(tryState);

                            double pTo = to;
                            to = calculateTRT(cl, st);

                            if (limit == -1 && pTo == to) limit = min;
                            if (pTo != to) limit = -1;
                            if (to >= prvTo) break;
                            min = max;
                            max = 2 * max;
                        }

                        if (to < prvTo && limit != -1)
                        {
                            to = prvTo;
                            max = limit;
                            min = limit / 2;
                            limitFlag = true;
                        }

                        if (to >= prvTo)
                        {
                            for (int ii = 0; ii < 1000 && max - min > 1; ii++)
                            {
                                int tryMe = ((max + min) + 1) / 2;
                                double cl = close + tryMe * m_tickSize;
                                double hi = (cl > high) ? cl : high;
                                double lo = low;

                                TRTTPOpState st = new TRTTPOpState(tryState);

                                to = calculateTRT(cl, st);

                                if (to >= prvTo) max = tryMe;
                                else min = tryMe;
                            }
                            trigTPUp = close + max * m_tickSize;
                        }
                    }

                    if (dir == 1)
                    {
                        int min = 0;
                        int max = 1;
                        int limit = -1;
                        double to = Double.NaN;
                        for (int ii = 0; ii < 20; ii++)
                        {
                            double cl = close - max * m_tickSize;
                            double hi = high;
                            double lo = (cl < low) ? cl : low;

                            TRTTPOpState st = new TRTTPOpState(tryState);

                            double pTo = to;
                            to = calculateTRT(cl, st);

                            if (limit == -1 && pTo == to) limit = min;
                            if (pTo != to) limit = -1;
                            if (to <= prvTo) break;
                            min = max;
                            max = 2 * max;
                        }

                        if (to > prvTo && limit != -1)
                        {
                            to = prvTo;
                            max = limit;
                            min = limit / 2;
                            limitFlag = true;
                        }

                        if (to <= prvTo)
                        {
                            for (int ii = 0; ii < 1000 && max - min > 1; ii++)
                            {
                                int tryMe = ((max + min) + 1) / 2;
                                double cl = close - tryMe * m_tickSize;
                                double hi = high;
                                double lo = (cl < low) ? cl : low;

                                TRTTPOpState st = new TRTTPOpState(tryState);

                                to = calculateTRT(cl, st);

                                if (to <= prvTo) max = tryMe;
                                else min = tryMe;
                            }
                            trigTPDn = close - max * m_tickSize;
                        }
                    }

                    if (dir != state.prvDir)
                    {
                        changeInDirection = true;
                    }
                    state.prvDir = dir;

                    state.prvHigh = high;
                    state.prvLow = low;
                    state.prvClose = close;
                }

                output[0] = limitFlag ? Double.NaN : ((changeInDirection && !Double.IsNaN(trigTPDn)) ? trigTPDn : trigTPUp);			// curve1
                output[1] = limitFlag ? Double.NaN : ((changeInDirection && !Double.IsNaN(trigTPUp)) ? trigTPUp : trigTPDn);			// curve2

                state.prvTo = curTo;
            }
        }

        double calculateRSI
            (int period,
             ref double accumulator1,
             ref double accumulator2,
             ref double validCount1,
             ref double validCount2,
             ref double average1,
             ref double average2,
             double input1,
             double input2)
        {
            double output = Double.NaN;

            if (!Double.IsNaN(input1) && !Double.IsNaN(input2))
            {
                double momentum = input1 - input2;
                double upMom = (momentum > 0) ? momentum : 0;
                double dnMom = (momentum < 0) ? -momentum : 0;
                double upAvg = calculateSmoothedAverage(period, ref accumulator1, ref validCount1, ref average1, upMom);
                double dnAvg = calculateSmoothedAverage(period, ref accumulator2, ref validCount2, ref average2, dnMom);
                output = (100 * upAvg) / (upAvg + dnAvg);
            }
            return output;
        }

        double calculateTRT
            (double cl,
             TRTTPOpState st)
        {
            double lr = calculateLinearRegression(m_LR1_period, m_LR1_s2, m_LR1_s4, ref st.LR1_s1, ref st.LR1_s3, st.LR1_inVal, cl);
            double rsi = calculateRSI(m_SA_period, ref st.SA1_acc, ref st.SA2_acc, ref st.SA1_cnt, ref st.SA2_cnt, ref st.SA1_avg, ref st.SA2_avg, lr, st.lr1);
            double trt = calculateLinearRegression(m_LR2_period, m_LR2_s2, m_LR2_s4, ref st.LR2_s1, ref st.LR2_s3, st.LR2_inVal, rsi);
            st.lr1 = lr;
            return trt;
        }

        double calculateLinearRegression
            (int period,
             double s2,
             double s4,
             ref double s1,
             ref double s3,
             Queue<double> inVal,
             double input)
        {
            double output = Double.NaN;
            if (!Double.IsNaN(input))
            {
                inVal.Enqueue(input);
                if (inVal.Count <= period)
                {
                    s1 += input * inVal.Count;
                    s3 += input;
                }
                else
                {
                    if (inVal.Count > period + 1) inVal.Dequeue();
                    s1 += input * period - s3;
                    s3 += input - inVal.Peek();
                }
                double slope = Double.NaN;
                if ((s4 * period - s2 * s2) < -(1E-100) || (s4 * period - s2 * s2) > 1E-100)
                    slope = (s1 * period - s3 * s2) / (s4 * period - s2 * s2);
                double intercept = Double.NaN;
                if (period != 0)
                    intercept = (s3 - slope * s2) / period;
                output = intercept + slope * period;
            }
            return output;
        }

        double calculateSmoothedAverage
            (int period,
             ref double accumulator,
             ref double validCount,
             ref double average,
             double input)
        {
            double output = Double.NaN;
            if (!Double.IsNaN(input))
            {
                if (Double.IsNaN(average))
                {
                    accumulator += input;
                    if (++validCount == period && period != 0)
                    {
                        average = accumulator / period;
                        output = average;
                    }
                }
                else
                {
                    if (period != 0)
                        average += (input - average) / period;
                    output = average;
                }
            }
            return output;
        }

    }
}
	