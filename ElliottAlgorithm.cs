using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ATMML
{
   public class OneWave : IComparable
   {
      public OneWave()
      {
         wave = 0;
         stat = 0;
         level = 0;
         group = 0;
         beg_idx = 0;
         end_idx = 0;
      }

      public short wave;		// wave number
      public short stat;		// status
      public short level;	   // division level
      public short group;	   // group number
      public short beg_idx;	// start of wave
      public short end_idx;	// end of wave

      public int CompareTo(object obj)
      {
         OneWave wave1 = this;
         OneWave wave2 = obj as OneWave;
         if (wave2 == null)
         {
            throw new ArgumentException("Object is not a OneWave");
         }

	      if (wave1.level < wave2.level) {
		      return -1;
	      }
	      else if (wave1.level > wave2.level) {
		      return 1;
	      }
	      else {
		      if (wave1.beg_idx < wave2.beg_idx) {
			      return -1;
		      }
		      else if (wave1.beg_idx > wave2.beg_idx) {
			      return 1;
		      }
		      else {
			      if (wave1.end_idx < wave2.end_idx) {
				      return -1;
			      }
			      else if (wave1.end_idx > wave2.end_idx) {
				      return 1;
			      }
		      }
	      }
	      return 0;
      }
   }

   public class EWProj 
   {
      public EWProj()
      {
         wave = 0;
         dir = 0;
         pct = 0;
         idx = 0;
         prc = 0;
         odds = 0;
         odds_idx = 0;
         odds_prc = 0;
         channel_len = 0;
         channel_beg_idx = 0;
         channel_end_idx = 0;
      }

      public short wave;               // wave 5 for PTI
      public short dir;                // to adjust Channel end bar index
      public short pct;
      public short idx;
      public double prc;
      public short odds;     				// PTI
      public short odds_idx;
      public double odds_prc;
      public short channel_len;			// Channel period
      public short channel_beg_idx; 	// Channel begin bar index
      public short channel_end_idx;    // Channel end bar index
   }

   // todo 
   public class TimeInterval
   {
   }

   public class ElliottWaveInput 
   {
      public short barCount;
 	  public short overlap;
	  public TimeInterval timeInterval;
	  public TimeInterval timeIntervalX;
      public List<DateTime> timeX;
	  public List<DateTime> time;
	  public Series lowX;
	  public Series highX;
      public Series low;
	  public Series high;
	  public Series close;
	  public Series oscX;
	  public Series osc1;
	  public Series osc2;
	  public bool completedWavesOnly;
   }

   public class ElliottWaveOutput 
   {
      public Series[] wave = new Series[ElliottAlgorithm.MAX_ELLIOTT_WAVE_LEVEL];
      public Series[] waveEnd = new Series[ElliottAlgorithm.MAX_ELLIOTT_WAVE_LEVEL];
      public Series[] channel = new Series[ElliottAlgorithm.MAX_CHANNEL_FACTORS];
 	  public Series projectionWaveNumber;
 	  public Series projectionPrice;
 	  public Series projectionFactor;

      public Series[] waveUp = new Series[ElliottAlgorithm.MAX_ELLIOTT_WAVE_LEVEL];
      public Series[] waveDn = new Series[ElliottAlgorithm.MAX_ELLIOTT_WAVE_LEVEL];

      public int pti = 0;
      public int ptiDir = 0;
      public double ptiPrice = 0;
      public int ptiIndex = 0;
   }

   public class ElliottAlgorithm
   {
      public const int MAX_WAVE_PROJECTION_COUNT = 10;
      public const int MAX_ELLIOTT_WAVE_LEVEL = 3;
      public const int MAX_CHANNEL_FACTORS = 3;

	  public const short WAVE_STAT_OK = 0;
      public const short WAVE_STAT_UNKNOWN = 1;
      public const short WAVE_STAT_4FAILED = 2;
      public const short WAVE_STAT_5FAILED = 3;

      public const short MIN_WAVE_PERS = 35;

      public const double MIN_VALUE = double.MinValue;
      public const double MAX_VALUE = double.MaxValue;

      public const short N_FIB_FACTORS = 8;
      public const short START3_5 = 3;

      public const short MIN_WAVE_SEG_LEN = 10;

      public const short EW_PERIOD1 = 5;
      public const short EW_PERIOD2 = 35;

      public const short FUTURES_4_1_OVERLAP = 17;
      public const short STOCKS_4_1_OVERLAP = 0;

      public short[] FibFactors = { 236, 382, 500, 618, 1000, 1618, 2618, 4250 };

      public short[] ChannelFactors = {62, 100, 138 };

      public short[,] WaveOddsTable1 = 
      {
         {80,  99,  99,  99,  99,  99,  99,  99,  99,  99,  99,  98,  99,  94,  96,  97,  91,  95,  97,  91},
         {80,  99,  99,  99,  99,  99,  99,  96,  97,  96,  95,  96,  94,  89,  91,  95,  83,  86,  90,  84},
         {80,  99,  99,  97,  97,  96,  98,  92,  93,  91,  89,  89,  90,  81,  83,  87,  76,  82,  90,  75},
	     {80,  99,  89,  88,  91,  91,  93,  88,  90,  89,  86,  82,  82,  74,  77,  80,  69,  75,  81,  66},
	     {80,  99,  82,  77,  85,  84,  90,  82,  86,  83,  81,  78,  80,  69,  69,  77,  65,  66,  77,  59},
         {80,  86,  82,  73,  79,  78,  84,  75,  80,  79,  77,  73,  75,  67,  62,  70,  60,  66,  74,  52},
         {80,  86,  79,  65,  74,  70,  73,  69,  74,  75,  74,  67,  68,  60,  59,  62,  55,  61,  71,  41},
         {80,  86,  75,  60,  65,  63,  67,  64,  68,  69,  72,  62,  64,  56,  54,  54,  47,  55,  61,  32},
         {80,  86,  75,  60,  61,  57,  62,  58,  61,  63,  67,  59,  61,  54,  51,  50,  41,  55,  61,  27},
         {80,  71,  71,  58,  54,  55,  55,  53,  56,  54,  63,  53,  56,  48,  49,  47,  39,  52,  61,  25},
         {80,  71,  61,  54,  49,  53,  52,  47,  50,  48,  58,  46,  46,  43,  45,  41,  37,  45,  58,  25},
         {80,  57,  57,  47,  48,  48,  47,  42,  41,  42,  51,  42,  44,  40,  41,  36,  35,  39,  55,  25},
         {80,  43,  57,  46,  46,  44,  43,  39,  34,  37,  47,  38,  38,  37,  38,  34,  32,  39,  45,  20},
         {80,  43,  57,  45,  43,  42,  40,  36,  32,  34,  40,  36,  33,  31,  32,  27,  32,  30,  45,  20},
         {60,  43,  57,  42,  42,  39,  37,  33,  30,  31,  36,  29,  28,  29,  28,  25,  29,  25,  39,  16},
         {60,  43,  57,  40,  37,  36,  36,  31,  29,  27,  34,  26,  25,  25,  27,  24,  27,  20,  39,  14},
         {60,  43,  57,  40,  36,  33,  34,  28,  28,  26,  30,  23,  22,  24,  26,  21,  20,  18,  26,  14},
         {60,  43,  57,  36,  33,  31,  33,  26,  25,  25,  29,  22,  20,  20,  24,  18,  17,  14,  23,  11},
         {60,  43,  54,  36,  32,  29,  32,  25,  22,  22,  27,  21,  19,  19,  23,  11,  16,  14,  16,   9},
         {60,  43,  54,  35,  31,  28,  31,  24,  21,  21,  26,  19,  19,  16,  20,   8,  13,  14,   1,   9},
         {60,  43,  50,  35,  30,  27,  30,  23,  20,  19,  23,  17,  18,  14,  17,   8,  12,  14,   1,   9},
         {60,  43,  43,  35,  29,  25,  28,  22,  19,  18,  21,  16,  16,  12,  15,   8,  12,  14,   6,   9},
         {60,  43,  43,  35,  29,  23,  27,  21,  18,  17,  19,  15,  15,   8,  15,   7,  11,  14,   6,   9},
         {60,  43,  43,  31,  28,  21,  27,  19,  17,  16,  19,  15,  11,   7,  13,   7,   9,  14,   3,   9},
         {60,  43,  43,  29,  24,  19,  25,  17,  16,  14,  17,  14,  11,   7,  13,   6,   9,  14,   3,   9},
         {60,  43,  43,  26,  22,  19,  24,  15,  15,  13,  16,  13,   1,   7,  12,   6,   9,  11,   3,   7},
         {60,  43,  43,  24,  22,  18,  23,  13,  14,  13,  15,  11,   9,   7,   1,   6,   9,  11,   3,   7},
         {60,  43,  39,  24,  22,  16,  22,  13,  13,  13,  14,   1,   9,   7,   1,   6,   8,  11,   3,   7},
         {60,  43,  39,  24,  21,  15,  21,  12,  13,  12,  14,   9,   8,   7,   9,   6,   7,  11,   3,   7},
         {60,  43,  39,  21,  20,  15,  20,  12,  11,  10,  14,   9,   7,   7,   9,   5,   7,  11,   3,   7},
         {60,  43,  32,  18,  19,  14,  19,  12,  11,  10,  13,   8,   6,   7,   8,   5,   5,  11,   3,   5},
         {60,  43,  29,  17,  17,  14,  18,  11,  10,  10,  13,   7,   5,   7,   7,   3,   3,   7,   3,   2},
         {60,  43,  29,  17,  17,  13,  17,  11,   1,   9,  13,   7,   4,   6,   7,   3,   3,   7,   3,   2},
         {60,  43,  29,  17,  17,  12,  17,  11,   9,   8,  12,   7,   4,   6,   6,   3,   3,   7,   3,   2},
         {60,  43,  29,  14,  17,  11,  16,  11,   9,   7,  12,   6,   4,   6,   6,   3,   3,   5,   3,   2},
         {60,  43,  29,  13,  17,  11,  16,  11,   9,   7,  11,   6,   4,   6,   6,   2,   1,   5,   3,   2},
         {60,  43,  21,  12,  17,  11,  15,  11,   8,   6,   1,   6,   4,   4,   6,   2,   1,   5,   3,   2},
         {60,  43,  21,  12,  17,  11,  13,  11,   7,   6,   9,   6,   4,   4,   5,   2,   1,   5,   3,   2},
         {60,  29,  18,  12,  15,  11,  13,  11,   6,   6,   9,   5,   4,   3,   5,   2,   1,   5,   3,   2},
         {40,  29,  18,  12,  15,  11,  12,  10,   6,   6,   9,   5,   3,   3,   4,   2,   1,   5,   3,   2},
         {20,  29,  18,  12,  15,  10,  12,   1,   6,   6,   8,   5,   3,   3,   4,   2,   1,   5,   3,   2},
         {20,  29,  18,  12,  14,   1,  11,   9,   6,   6,   8,   5,   3,   3,   3,   2,   1,   5,   3,   2},
         {20,  29,  18,  12,  13,   1,  11,   9,   6,   6,   8,   4,   3,   3,   3,   1,   1,   5,   3,   2},
         {20,  29,  18,  12,  13,   1,  10,   7,   5,   5,   8,   4,   3,   3,   3,   1,   1,   5,   3,   2},
         {20,  29,  18,  12,  13,   1,   9,   7,   5,   5,   8,   4,   3,   2,   3,   1,   1,   5,   3,   2},
         {20,  29,  18,  12,  12,   9,   8,   7,   5,   5,   7,   4,   2,   2,   2,   1,   1,   5,   3,   2},
         {20,  29,  18,  10,  12,   9,   8,   7,   5,   5,   7,   4,   2,   2,   2,   0,   1,   5,   3,   0},
         {20,  29,  18,  10,  12,   9,   7,   7,   4,   4,   7,   4,   2,   2,   2,   0,   1,   2,   3,   0},
         {20,  29,  18,   9,  10,   8,   6,   7,   4,   4,   7,   4,   2,   2,   2,   0,   1,   2,   3,   0},
         {20,  29,  18,   8,  10,   8,   6,   5,   4,   4,   7,   4,   2,   2,   2,   0,   1,   2,   3,   0},
         {20,  29,  18,   8,  10,   8,   6,   5,   4,   4,   6,   3,   2,   2,   2,   0,   1,   2,   3,   0},
         {20,  29,  18,   8,  10,   8,   5,   5,   3,   3,   6,   3,   2,   2,   2,   0,   1,   2,   3,   0},
         {20,  29,  18,   8,   1,   8,   5,   5,   3,   3,   5,   3,   2,   2,   2,   0,   0,   2,   3,   0},
         {20,  29,  18,   6,   8,   7,   5,   4,   3,   3,   5,   3,   2,   2,   1,   0,   0,   2,   3,   0},
         {20,  29,  18,   6,   8,   7,   4,   3,   3,   3,   4,   3,   2,   2,   1,   0,   0,   2,   3,   0},
         {20,  29,  18,   6,   8,   7,   4,   3,   3,   2,   4,   3,   2,   2,   1,   0,   0,   2,   3,   0},
         {20,  29,  18,   6,   8,   7,   4,   3,   3,   2,   4,   2,   2,   2,   1,   0,   0,   2,   3,   0},
         {20,  29,  11,   5,   8,   6,   4,   3,   3,   2,   3,   2,   1,   2,   1,   0,   0,   2,   3,   0},
         {20,  29,   7,   5,   8,   6,   4,   2,   2,   2,   3,   2,   1,   2,   1,   0,   0,   2,   3,   0},
         {20,  29,   7,   4,   8,   6,   4,   2,   2,   2,   2,   2,   1,   1,   1,   0,   0,   2,   3,   0}
      };

      private Series low;
      private Series high;
      private Series osc;

      private short MinWaveSegLen;
      private short EWPct4Overlap1;
      private short EWPct1LenOf3;
      private short BarCount;

      private short c_group;
      private short c_level;
      private List<OneWave> waves = new List<OneWave>();
      private EWProj[] ewproj = new EWProj[MAX_WAVE_PROJECTION_COUNT];

      // todo
      public ElliottAlgorithm() 
      {
      }

      // todo
      public void Evaluate(ElliottWaveInput input, ElliottWaveOutput output)
      {
          /* initialize variables
          */
          MinWaveSegLen = MIN_WAVE_SEG_LEN;
          EWPct1LenOf3 = 50;
          EWPct4Overlap1 = input.overlap;
          BarCount = input.barCount;

          Series loVS1, loVS2;
          Series hiVS1, hiVS2;
          Series osVS1, osVS2;

          /* find the first level waves
          */
          int count = input.lowX.Count;
          int start1 = 0;
          //for (; start1 < count; start1++)
          //{
          //    if (!double.IsNaN(input.lowX[start1]))
          //    {
          //        break;
          //    }
          //}

          loVS1 = new Series(count - start1);
          hiVS1 = new Series(count - start1);
          osVS1 = new Series(count - start1);
          count = loVS1.Count;
          for (int ii = 0; ii < count; ii++)
          {
              loVS1[ii] = input.lowX[start1 + ii];
              hiVS1[ii] = input.highX[start1 + ii];
              osVS1[ii] = input.oscX[start1 + ii];
          }

          c_group = 0;
          c_level = 0;
          low = loVS1;
          high = hiVS1;
          osc = osVS1;
          int barCount = osc.Count;
          for (int ijk = 0; ijk < barCount; ijk++)
          {
              if (double.IsNaN(osc[ijk]))
              {
                  osc[ijk] = 0;
              }
          }
          count = osc.Count;
          for (int idx = 0; idx <= EW_PERIOD2 - 1 && idx < count; idx++)
          {
              osc[idx] = 0;
          }
          FndWavesL1(0);
          FixWavesL1();

          /* adjust cross referenced waves
          */
          //todo 	     if (input.timeIntervalX.Compare(input.timeInterval) != 0) 
          //todo        {
          //todo  		     AdjustCrossRef (input.time, input.timeX, input.timeIntervalX);
          //todo        }

          /* calculate wave projections
          */
          WaveProjections();

          /* find second level waves
          */
          int outputCount = input.low.Count;
          count = outputCount;
          int start2 = 0;
          //for (; start2 < count; start2++)
          //{
          //    if (!double.IsNaN(input.low[start2]))
          //    {
          //        break;
          //    }
          //}

          loVS2 = new Series(count - start2);
          hiVS2 = new Series(count - start2);
          osVS2 = new Series(count - start2);
          count = loVS2.Count;
          for (int ii = 0; ii < count; ii++)
          {
              loVS2[ii] = input.low[start2 + ii];
              hiVS2[ii] = input.high[start2 + ii];
              osVS2[ii] = input.osc1[start2 + ii];
          }

          c_level = 1;
          low = loVS2;
          high = hiVS2;
          osc = osVS2;
          barCount = osc.Count;
          for (int ijk = 0; ijk < barCount; ijk++)
          {
              if (double.IsNaN(osc[ijk]))
              {
                  osc[ijk] = 0;
              }
          }
          count = osc.Count;
          for (int idx = 0; idx <= EW_PERIOD2 - 1 && idx < count; idx++)
          {
              osc[idx] = 0;
          }
          FndWavesL2();

          /* find third level waves
          */
          c_level = 2;

          for (int ii = 0; ii < count; ii++)
          {
              osVS2[ii] = input.osc2[start2 + ii];
          }

          osc = osVS2;
          //todo	      osc->ConditionalReplace (EQUAL, double.NaN, 0);
          count = osc.Count;
          for (int idx = 0; idx <= EW_PERIOD2 - 1 && idx < count; idx++)
          {
              osc[idx] = 0;
          }
          FndWavesL2();

          /* create wave value sets
          */
          for (int ii = 0; ii < MAX_ELLIOTT_WAVE_LEVEL; ii++)
          {
              output.waveUp[ii] = new Series(outputCount, 0);
              output.waveDn[ii] = new Series(outputCount, 0);
          }

          output.wave[0] = new Series(outputCount, 0);
          output.wave[1] = new Series(outputCount, 0);
          output.wave[2] = new Series(outputCount, 0);
          output.waveEnd[0] = new Series(outputCount, 0);
          output.waveEnd[1] = new Series(outputCount, 0);
          output.waveEnd[2] = new Series(outputCount, 0);
          int wCnt = waveCount();
          for (int waveIndex = 0; waveIndex < wCnt; waveIndex++)
          {
              int stat = waves[waveIndex].stat;
              int level = waves[waveIndex].level;
              int offset = (level == 1) ? start1 : start2;
              int begIndex = waves[waveIndex].beg_idx + offset + 1;
              int endIndex = waves[waveIndex].end_idx + offset;
              if (stat == WAVE_STAT_4FAILED) endIndex = outputCount - 1;
              begIndex = Math.Max(begIndex, 0);
              begIndex = Math.Min(begIndex, outputCount - 1);
              endIndex = Math.Max(endIndex, 0);
              endIndex = Math.Min(endIndex, outputCount - 1);
              int begIdx = Math.Min(begIndex, endIndex);
              int endIdx = Math.Max(begIndex, endIndex);
              if (level < MAX_ELLIOTT_WAVE_LEVEL && stat != WAVE_STAT_5FAILED)
              {
                  int waveNum = waves[waveIndex].wave;
                  for (int ii = begIdx; ii <= endIdx; ii++)
                  {
                      output.wave[level][ii] = waveNum;
                  }
                  if (stat == WAVE_STAT_OK)
                  {
                      double val = (100 * (output.waveEnd[level][endIdx]) + (waveNum + 10));
                      output.waveEnd[level][endIdx] = val;

                      if (waveNum > 0)
                      {
                          output.waveUp[level][endIdx] = waveNum;
                      }
                      else
                      {
                          output.waveDn[level][endIdx] = -waveNum;
                      }
                  }
              }
          }

          /* create channel value sets
          */
          for (int ii = 0; ii < MAX_CHANNEL_FACTORS; ii++)
          {
              output.channel[ii] = new Series(outputCount, double.NaN);
              for (int jj = 0; jj < MAX_WAVE_PROJECTION_COUNT; jj++)
              {
                  if (ewproj[jj] != null && ewproj[jj].channel_len != 0)
                  {
                      short period = (short)(((long)ewproj[jj].channel_len * ChannelFactors[ii]) / 100L);
                      if (period > 0)
                      {
                          int offset = start1;
                          int begIndex = Math.Max(ewproj[jj].channel_beg_idx - period + offset, 0);
                          int endIndex = Math.Min(ewproj[jj].channel_end_idx + 4 + offset, outputCount);
                          int size = endIndex - begIndex;
                          if (period <= size)
                          {
                              Series average = input.close.movAvg(MAType.Simple, period);

                              //Series average = new Series(size);
                              //average.Copy (input.close, begIndex, 0, size);

                              //Series revAverage = new Series(size);
                              //for (int idx = 0; idx < size; idx++)
                              //{
                              //    revAverage[size - 1 - idx] = average[idx];
                              //}
                              //revAverage.SimpleAverage (period);
                              //for (int idx = 0; idx < size; idx++)
                              //{
                              //    average[size - 1 - idx] = revAverage[idx];
                              //}
                              //
                              //int kk = begIndex + (period - 1);
                              //for (; kk < endIndex; kk++)
                              //{
                              //    if ((ewproj[jj].dir >= 1 && input.low[kk] < average[kk - begIndex]) ||
                              //       (ewproj[jj].dir <= -1 && input.high[kk] > average[kk - begIndex]))
                              //    {
                              //        break;
                              //    }
                              //}
                              //begIndex += (period - 1);
                              //endIndex = kk;

                              int kk = begIndex + (period - 1);
                              for (; kk < endIndex; kk++)
                              {
                                  if ((ewproj[jj].dir >= 1 && input.low[kk] < average[kk]) ||
                                     (ewproj[jj].dir <= -1 && input.high[kk] > average[kk]))
                                  {
                                      break;
                                  }
                              }
                              begIndex += (period - 1);
                              endIndex = kk;

                              //	(output.channel[ ii ])->Copy (&average, period - 1, begIndex, endIndex - begIndex);
                              for (int idx = begIndex; idx < endIndex; idx++)
                              {
                                  output.channel[ii][idx] = average[idx];
                              }
                          }
                      }
                  }
              }
          }

          /* create profit taking index value set
          */
          //output.pti = new Series(outputCount, double.NaN);
          //double pti = double.NaN;
          for (int ii = 0; ii < MAX_WAVE_PROJECTION_COUNT; ii++)
          {
              if (ewproj[ii] != null && ewproj[ii].wave == 5)
              {
                  if (ewproj[ii].odds != 0)
                  {
                      output.pti = ewproj[ii].odds;
                      output.ptiDir = ewproj[ii].dir;
                      output.ptiPrice = ewproj[ii].odds_prc;
                      output.ptiIndex = ewproj[ii].odds_idx;
                      break;
                  }
              }
          }
          //if (!double.IsNaN(pti))
          //{
          //    int begIndex = 0;
          //    int endIndex = outputCount - 1;
          //    for (int waveIndex = waveCount() - 1; waveIndex >= 0; waveIndex--)
          //    {
          //        int level = waves[waveIndex].level;
          //        int offset = (level == 1) ? start1 : start2;
          //        int waveCount1 = waves[waveIndex].wave;
          //        if (level == 0)
          //        {
          //            if (Math.Abs(waveCount1) == 5 && waves[waveIndex].stat == WAVE_STAT_OK)
          //            {
          //                endIndex = Math.Min(waves[waveIndex].beg_idx, outputCount - 1) + offset;
          //            }
          //            else if (Math.Abs(waveCount1) == 4)
          //            {
          //                begIndex = Math.Max(waves[waveIndex].beg_idx, (short)0) + offset;
          //                //todo            	      output.pti->Initialize (pti, begIndex, endIndex - begIndex + 1);
          //                break;
          //            }
          //        }
          //    }
          //}


          /* create projection value sets
          */
          output.projectionWaveNumber = new Series(MAX_WAVE_PROJECTION_COUNT, double.NaN);
          output.projectionPrice = new Series(MAX_WAVE_PROJECTION_COUNT, double.NaN);
          output.projectionFactor = new Series(MAX_WAVE_PROJECTION_COUNT, double.NaN);
          int index = 0;
          for (int ii = 0; ii < MAX_WAVE_PROJECTION_COUNT; ii++)
          {
              if (ewproj[ii] != null && ewproj[ii].wave != 0)
              {
                  output.projectionWaveNumber[index] = ewproj[ii].wave;
                  output.projectionPrice[index] = ewproj[ii].prc;
                  output.projectionFactor[index] = ewproj[ii].pct;
                  index++;
              }
          }
      }

      public void	WaveToLabel(int level, int wave, out string pstr)
      {
  	       string[,] EWLabels = 
          {
            {"1", "2",    "3",  "4", "5", "X", "A", "B", "C"},
            {"1", "2",    "3",  "4", "5", "X", "A", "B", "C"},
            {"i", "ii", "iii", "iv", "v", "x", "a", "b", "c"}
          };
	      wave = Math.Abs(wave) - 1;
	      if (0 <= level && level < 3 && 0 <= wave && wave < 9) 
         {
  		      pstr = EWLabels[ level, wave ];
	      }
	      else 
         {
		      pstr = "";
          }
      }

      private double getHi(short idx) 
      {
          short minIdx = (short)0; // Math.Max(0, high.Count - BarCount);
          short maxIdx = (short)Math.Max(0, high.Count);
          return (minIdx <= idx && idx < maxIdx)? high[ idx ] : 0;
      }

      private double getLo(short idx) 
      {
          short minIdx = (short)0; // Math.Max(0, low.Count - BarCount);
          short maxIdx = (short)Math.Max(0, low.Count);
          return (minIdx <= idx && idx < maxIdx) ? low[idx] : 0;
      }

      private double getOsc(short idx) 
      {
          short minIdx = (short)0; // Math.Max(0, osc.Count - BarCount);
          short maxIdx = (short)Math.Max(0, osc.Count);
          return (minIdx <= idx && idx < maxIdx) ? osc[idx] : 0;
      }
   
	   private short barCount()
	   {
		   return (short)low.Count;
	   }

      private short waveCount()
	   {
		   return (short)waves.Count;
	   }

      // todo
      private void AdjustCrossRef(List<DateTime> time, List<DateTime> timeX, TimeInterval timeIntervalX)
      {
         /* remove cross reference waves which end before
         *  the earliest time
         */
         DateTime earlyTime = time[0];
         while (waveCount() != 0)
         {
            DateTime waveEndTime = timeX[waves[0].end_idx];
            if (waveEndTime < earlyTime)
            {
               RemoveWave(0);
            }
            else
            {
               break;
            }
         }

         /* adjust remaining wave's begin and end indexes
         */
         for (short ii = 0; ii < waveCount(); ii++)
         {
            for (short jj = 0; jj < 2; jj++)
            {
               short waveIndex = (jj != 0) ? waves[ii].beg_idx : waves[ii].end_idx;
               short wave = waves[ii].wave;
               DateTime seekTime = timeX[waveIndex];
/* todo */     short begIndex = 0; //  time->SeekByKeyGE(seekTime);
               if (waveIndex + 1 < barCount())
               {
                  seekTime = timeX[waveIndex + 1];
               }
               else
               {
                  /*
                   * assume we're using IGNORE_INTERVAL...
                   */
/* todo */        //seekTime.SetTime(seekTime.GetTime() + timeIntervalX.AsRTime());
               }
/* todo */     short endIndex = 0; // time->SeekByKeyGE(seekTime);
               short index = begIndex;
               if (wave > 0 && ((wave % 2) != 0) || wave < 0 && ((wave % 2) == 0))
               {
                  for (short kk = (short)(begIndex + 1); kk < endIndex; kk++)
                  {
                     if (getLo(kk) < getLo(index))
                     {
                        index = kk;
                     }
                  }
               }
               else
               {
                  for (short kk = (short)(begIndex + 1); kk < endIndex; kk++)
                  {
                     if (getHi(kk) > getHi(index))
                     {
                        index = kk;
                     }
                  }
               }
               if (jj != 0)
               {
                  waves[ii].beg_idx = index;
               }
               else
               {
                  waves[ii].beg_idx = index;
               }
            }
         }
      }

      private short GetMaxOsc(out short begidx, out short endidx, out short osc_idx, short start)
      {
         short c, idx, bidx, eidx, sig = 0, end;
         double osc_max;

         begidx = 0;
         endidx = 0;

         osc_idx = start;
         osc_max = 0;
         end = (short)(barCount() - 1);
         for (c = 0; c <= waveCount(); ++c)
         {
            bidx = (c == 0) ? start : waves[c - 1].end_idx;
            eidx = (c == waveCount()) ? end : waves[c].beg_idx;

            if ((eidx - bidx) <= MinWaveSegLen)
            {
               continue;
            }

            idx = bidx;
            if (getOsc(idx) > 0)
            {
               for (; getOsc(idx) > 0 && idx < eidx; ++idx)
               {
               }
            }
            else if (getOsc(idx) < 0)
            {
               for (; getOsc(idx) < 0 && idx < eidx; ++idx)
               {
               }
            }

            for (; idx <= eidx; ++idx)
            {
               if (getOsc(idx) > osc_max)
               {
                  begidx = bidx;
                  endidx = eidx;
                  osc_idx = idx;
                  osc_max = getOsc(idx);
                  sig = 1;
               }
               if (getOsc(idx) < -osc_max)
               {
                  osc_max = -(getOsc(idx));
                  begidx = bidx;
                  endidx = eidx;
                  osc_idx = idx;
                  sig = -1;
               }
            }
         }
         return sig;
      }

      private void FndWavesL1(short start)  // default value = 0
      {
         short beg_idx = 0;
         short end_idx = 0;
         short osc_max_idx = 0;
         short type,
         lbeg_idx = -1,
         lend_idx = -1;

         /*  lbeg_idx = start-1;*/
         for (; ; )
         {
            type = GetMaxOsc(out beg_idx, out end_idx, out osc_max_idx, start);
            if (beg_idx == lbeg_idx && end_idx == lend_idx)
               break;
            lbeg_idx = beg_idx;
            lend_idx = end_idx;

            switch (type)
            {
               case 1: FndUpWaves(beg_idx, end_idx, osc_max_idx, 0); break;
               case -1: FndDnWaves(beg_idx, end_idx, osc_max_idx, 0); break;
               default: return;
            }
         }
      }

      private void FndUpWaves(short beg_idx, short end_idx, short osc_max_idx, short fixing_waves)
      {
         short wave0_idx, wave1_idx, wave2_idx, wave3_idx, wave4_idx, wave5_idx,
            idx, tp_time, nwave4_idx, nwave5_idx, wave4_stat, wave5_stat;

         tp_time = 23;
         if (fixing_waves != 0)  		/* we know where this is!!!		*/
            wave0_idx = beg_idx;
         else
            wave0_idx = GetBegOfWaveUp(beg_idx, osc_max_idx, tp_time);	/* first find start of wave using osc & tp 	*/

         tp_time = 23;
         if (end_idx - wave0_idx < MinWaveSegLen || wave0_idx < MinWaveSegLen)
         {
            wave0_idx = GetMaxLoTpRight(beg_idx, wave0_idx);
         }
         wave4_idx = GetWave4OscIdxUp(wave0_idx, osc_max_idx, end_idx, out wave4_stat); /*wave4 to temporary place where osc < 0 	*/
         wave3_idx = GetMaxIdxPrc((short)(wave0_idx + 1), (short)(wave4_idx - 1));		/* set wave 3 to high inbetween			*/

         wave1_idx = GetWave1IdxUp(wave0_idx, wave3_idx, EWPct1LenOf3);	/* set wave 1 to tp in 75% of wave3 - wave1	*/
         wave2_idx = GetWave24IdxUp(wave1_idx, wave3_idx);	/* set wave2 to low between 1 & 3		*/

         FitWave1_2IdxUp(wave0_idx, wave3_idx, ref wave1_idx, ref wave2_idx);

         if (getLo(wave2_idx) >= GetXPctRtcWaveUp(wave0_idx, wave1_idx, 30))
         {
            idx = GetMaxLoTpLeft(beg_idx, wave0_idx);
            if (getLo(idx) <= getLo(wave0_idx))
            {
               wave0_idx = idx;
               /* new 11/27/90	*/
               wave4_idx = GetWave4OscIdxUp(wave0_idx, osc_max_idx, end_idx, out wave4_stat); /*wave4 to temporary place where osc < 0 	*/
               wave3_idx = GetMaxIdxPrc((short)(wave0_idx + 1), (short)(wave4_idx - 1));		/* set wave 3 to high inbetween			*/

               wave1_idx = GetWave1IdxUp(wave0_idx, wave3_idx, EWPct1LenOf3);	/* set wave 1 to tp in 75% of wave3 - wave1	*/
               wave2_idx = GetWave24IdxUp(wave1_idx, wave3_idx);	/* set wave2 to low between 1 & 3		*/

               FitWave1_2IdxUp(wave0_idx, wave3_idx, ref wave1_idx, ref wave2_idx);
            }
         }
         wave5_idx = GetWave5IdxUp(wave0_idx, wave1_idx, wave3_idx, wave4_idx, end_idx, EWPct4Overlap1);

         if (wave5_idx == -1)
         {          /* 1-2-3 collapsed to A-B-C	*/
            if (fixing_waves != 0)
               wave3_idx = end_idx;
            else if (end_idx - wave3_idx <= MinWaveSegLen)
               wave3_idx = GetMaxHiTpRight(wave3_idx, end_idx);
            Adjust123ToABCUp(wave0_idx, wave1_idx, wave3_idx);
         }
         else
         {
            if (fixing_waves != 0)
            {
               wave5_idx = end_idx;
               wave4_idx = GetWave24IdxUp(wave3_idx, wave5_idx);
            }
            else
            {
               tp_time = GetHiTpLen(wave3_idx);
               nwave5_idx = GetMaxHiTpRight(wave5_idx, end_idx);
               nwave4_idx = GetWave24IdxUp(wave3_idx, nwave5_idx);
               if (getLo(nwave4_idx) >= GetXPctRtcWaveUp(wave0_idx, wave1_idx, EWPct4Overlap1) &&
                  (nwave5_idx - nwave4_idx) <= 3 * (wave3_idx - wave0_idx))
               {
                  wave4_idx = nwave4_idx;
                  wave5_idx = nwave5_idx;
                  wave3_idx = GetMaxIdxPrc(wave2_idx, (short)(wave4_idx - 1));
               }
               else
                  wave4_idx = GetWave24IdxUp(wave3_idx, wave5_idx);
            }
            if (getLo(wave4_idx) < GetXPctRtcWaveUp(wave0_idx, wave1_idx, EWPct4Overlap1))
               Adjust123ToABCUp(wave0_idx, wave1_idx, end_idx);
            else
            {
               wave1_idx = RollWave13UpLeft(wave0_idx, wave1_idx, wave2_idx);
               wave3_idx = RollWave13UpLeft(wave2_idx, wave3_idx, wave4_idx);
               wave5_stat = (wave4_stat == WAVE_STAT_OK &&
                  getHi(wave5_idx) >= getHi(wave3_idx)) ? WAVE_STAT_OK : WAVE_STAT_5FAILED;
               SetWave(wave0_idx, wave1_idx, 1, WAVE_STAT_OK);
               SetWave(wave1_idx, wave2_idx, 2, WAVE_STAT_OK);
               SetWave(wave2_idx, wave3_idx, 3, WAVE_STAT_OK);
               SetWave(wave3_idx, wave4_idx, 4, wave4_stat);
               SetWave(wave4_idx, wave5_idx, 5, wave5_stat);
               ++c_group;
            }
         }
      }

      private void FndDnWaves(short beg_idx, short end_idx, short osc_max_idx, short fixing_waves)
      {
         short wave0_idx, wave1_idx, wave2_idx, wave3_idx, wave4_idx, wave5_idx,
            idx, tp_time, nwave4_idx, nwave5_idx, wave4_stat, wave5_stat;

         tp_time = 23;
         if (fixing_waves != 0)
            wave0_idx = beg_idx;			/* we know where this is!!!	*/
         else
            wave0_idx = GetBegOfWaveDn(beg_idx, osc_max_idx, tp_time);

         if (end_idx - wave0_idx < MinWaveSegLen || wave0_idx < MinWaveSegLen)
            wave0_idx = GetMaxHiTpRight(beg_idx, wave0_idx);
         wave4_idx = GetWave4OscIdxDn(wave0_idx, osc_max_idx, end_idx, out wave4_stat);	/* temporary    */
         wave3_idx = GetMinIdxPrc((short)(wave0_idx + 1), (short)(wave4_idx - 1));

         wave1_idx = GetWave1IdxDn(wave0_idx, wave3_idx, EWPct1LenOf3);
         wave2_idx = GetWave24IdxDn(wave1_idx, wave3_idx);
         FitWave1_2IdxDn(wave0_idx, wave3_idx, ref wave1_idx, ref wave2_idx);
         if (getHi(wave2_idx) <= GetXPctRtcWaveDn(wave0_idx, wave1_idx, 30))
         {
            idx = GetMaxHiTpLeft(beg_idx, wave0_idx);
            if (getHi(idx) >= getHi(wave0_idx))
            {
               wave0_idx = idx;
               /* new 11/27/90	*/
               wave4_idx = GetWave4OscIdxDn(wave0_idx, osc_max_idx, end_idx, out wave4_stat);	/* temporary    */
               wave3_idx = GetMinIdxPrc((short)(wave0_idx + 1), (short)(wave4_idx - 1));

               wave1_idx = GetWave1IdxDn(wave0_idx, wave3_idx, EWPct1LenOf3);	/* set wave 1 to tp in 75% of wave3 - wave1	*/
               wave2_idx = GetWave24IdxDn(wave1_idx, wave3_idx);		/* set wave2 to low between 1 & 3		*/
               FitWave1_2IdxDn(wave0_idx, wave3_idx, ref wave1_idx, ref wave2_idx);
            }
         }
         wave5_idx = GetWave5IdxDn(wave0_idx, wave1_idx, wave3_idx, wave4_idx, end_idx, EWPct4Overlap1);
         if (wave5_idx == -1)
         {
            if (fixing_waves != 0)
               wave3_idx = end_idx;
            else if (end_idx - wave3_idx <= MinWaveSegLen)
               wave3_idx = GetMaxLoTpRight(wave3_idx, end_idx);
            Adjust123ToABCDn(wave0_idx, wave1_idx, wave3_idx);
         }
         else
         {
            if (fixing_waves != 0)
            {
               wave5_idx = end_idx;
               wave4_idx = GetWave24IdxDn(wave3_idx, wave5_idx);
            }
            else
            {
               tp_time = GetLoTpLen(wave3_idx);
               nwave5_idx = GetMaxLoTpRight(wave5_idx, end_idx);
               nwave4_idx = GetWave24IdxDn(wave3_idx, nwave5_idx);
               if (getHi(nwave4_idx) <= GetXPctRtcWaveDn(wave0_idx, wave1_idx, EWPct4Overlap1) &&
                  (nwave5_idx - nwave4_idx) <= 3 * (wave3_idx - wave0_idx))
               {
                  wave4_idx = nwave4_idx;
                  wave5_idx = nwave5_idx;
                  wave3_idx = GetMinIdxPrc(wave2_idx, (short)(wave4_idx - 1));
               }
               else
                  wave4_idx = GetWave24IdxDn(wave3_idx, wave5_idx);
            }
            if (getHi(wave4_idx) > GetXPctRtcWaveDn(wave0_idx, wave1_idx, EWPct4Overlap1))
               Adjust123ToABCDn(wave0_idx, wave1_idx, end_idx);
            else
            {
               wave1_idx = RollWave13DnLeft(wave0_idx, wave1_idx, wave2_idx);
               wave3_idx = RollWave13DnLeft(wave2_idx, wave3_idx, wave4_idx);
               wave5_stat = (wave4_stat == WAVE_STAT_OK &&
                  getLo(wave5_idx) <= getLo(wave3_idx)) ? WAVE_STAT_OK : WAVE_STAT_5FAILED;

               SetWave(wave0_idx, wave1_idx, -1, WAVE_STAT_OK);
               SetWave(wave1_idx, wave2_idx, -2, WAVE_STAT_OK);
               SetWave(wave2_idx, wave3_idx, -3, WAVE_STAT_OK);
               SetWave(wave3_idx, wave4_idx, -4, wave4_stat);
               SetWave(wave4_idx, wave5_idx, -5, wave5_stat);
               ++c_group;
            }
         }
      }

      private void FndWavesL2()
      {
         short c, n_waves, beg_idx, end_idx, sub;

         n_waves = waveCount();
         for (c = 0; c < n_waves; ++c)
         {
            if (waves[c].level != c_level - 1)
               continue;
            beg_idx = waves[c].beg_idx;
            end_idx = waves[c].end_idx;
            if (end_idx - beg_idx < 10)
               continue;
            if ((waves[c].wave % 2) == 0)
               sub = (waves[c].wave > 0) ?
            FndDnWavesL2_ABC(beg_idx, end_idx) :
            FndUpWavesL2_ABC(beg_idx, end_idx);
            else
               sub = (waves[c].wave > 0) ?
            FndUpWavesL2_123(beg_idx, end_idx) :
            FndDnWavesL2_123(beg_idx, end_idx);

         }
      }

      private short FndUpWavesL2_ABC(short beg_idx, short end_idx)
      {
         return (Adjust123ToABCUp(beg_idx, (short)(beg_idx + 1), end_idx));
      }

      private short FndDnWavesL2_ABC(short beg_idx, short end_idx)
      {
         return (Adjust123ToABCDn(beg_idx, (short)(beg_idx + 1), end_idx));
      }

      private short FndUpWavesL2_123(short beg_idx, short end_idx)
      {
         short wave0_idx, wave1_idx, wave2_idx, wave3_idx, wave4_idx, wave5_idx,
            idx, osc_max_idx, stat, tries;

         wave0_idx = beg_idx;
         wave5_idx = end_idx;
         idx = (short)(beg_idx + (end_idx - beg_idx) * 90L / 100);
         for (wave4_idx = wave0_idx; wave4_idx <= (wave5_idx + wave0_idx) / 2; )
         {
            osc_max_idx = GetOscMaxIdxUp(osc, wave4_idx, idx);
            wave4_idx = GetWave4OscIdxUp(wave4_idx, osc_max_idx, end_idx, out stat);  /* set wave4 to temporary place where osc < 0 	*/
         }

         /* changed to +1 10/9/89	*/
         wave3_idx = GetMaxIdxPrc((short)(wave0_idx + 1), (short)(wave4_idx - 1));		/* set wave 3 to high inbetween			*/
         wave1_idx = GetWave1IdxUp(wave0_idx, wave3_idx, 50);		/* set wave 1 to tp in 50% of wave3 - wave1	*/
         wave2_idx = GetWave24IdxUp(wave1_idx, wave3_idx);		/* set wave2 to low between 1 & 3		*/
         idx = (short)(beg_idx + (end_idx - beg_idx) * 618L / 1000);
         wave3_idx = GetMaxHiTpLeft(idx, wave3_idx);
         FitWave1_2IdxUp(wave0_idx, wave3_idx, ref wave1_idx, ref wave2_idx);
         wave4_idx = GetWave24IdxUp(wave3_idx, wave5_idx);
         wave1_idx = GetMaxIdxPrc((short)(wave0_idx + 1), wave1_idx);
         wave3_idx = GetMaxIdxPrc(wave3_idx, (short)(wave4_idx - 1));

         idx = (short)(beg_idx + (end_idx - beg_idx) * 90L / 100);
         for (tries = 0;
            getHi(wave3_idx) - getLo(wave2_idx) < getHi(wave1_idx) - getLo(wave0_idx) &&
            tries < 3 &&
            wave3_idx <= idx; ++tries)
         {

            wave3_idx = GetMaxHiTpRight(wave4_idx, idx);
            wave4_idx = GetWave24IdxUp(wave3_idx, wave5_idx);
            wave3_idx = GetMaxIdxPrc(wave3_idx, (short)(wave4_idx - 1));
            wave1_idx = GetWave1IdxUp(wave0_idx, wave3_idx, 50);
            wave2_idx = GetWave24IdxUp(wave1_idx, wave3_idx);
            FitWave1_2IdxUp(wave0_idx, wave3_idx, ref wave1_idx, ref wave2_idx);
         }

         wave1_idx = RollWave13UpLeft(wave0_idx, wave1_idx, wave2_idx);
         wave3_idx = RollWave13UpLeft(wave2_idx, wave3_idx, wave4_idx);
         wave2_idx = RollWave24UpRight(wave1_idx, wave2_idx, wave3_idx);
         wave4_idx = RollWave24UpRight(wave3_idx, wave4_idx, wave5_idx);

         if (getLo(wave4_idx) >= GetXPctRtcWaveUp(wave0_idx, wave1_idx, EWPct4Overlap1) &&
            wave3_idx < wave5_idx &&
            getHi(wave3_idx) - getLo(wave2_idx) >= getHi(wave1_idx) - getLo(wave0_idx))
         {
            SetWave(wave0_idx, wave1_idx, 1, WAVE_STAT_OK);
            SetWave(wave1_idx, wave2_idx, 2, WAVE_STAT_OK);
            SetWave(wave2_idx, wave3_idx, 3, WAVE_STAT_OK);
            SetWave(wave3_idx, wave4_idx, 4, WAVE_STAT_OK);
            SetWave(wave4_idx, wave5_idx, 5, WAVE_STAT_OK);
            ++c_group;
            return 1;		/* yes wave subdivided	*/
         }
         return 0;		/* no it didn't		*/
      }

      private short FndDnWavesL2_123(short beg_idx, short end_idx)
      {
         short wave0_idx, wave1_idx, wave2_idx, wave3_idx, wave4_idx, wave5_idx,
            idx, osc_max_idx, stat, tries;

         wave0_idx = beg_idx;
         wave5_idx = end_idx;
         idx = (short)(beg_idx + (end_idx - beg_idx) * 90L / 100);
         for (wave4_idx = wave0_idx; wave4_idx <= (wave5_idx + wave0_idx) / 2; )
         {
            osc_max_idx = GetOscMaxIdxDn(osc, wave4_idx, idx);
            wave4_idx = GetWave4OscIdxDn(wave4_idx, osc_max_idx, end_idx, out stat);  /* set wave4 to temporary place where osc < 0 	*/
         }

         /* changed to +1 10/9/89	*/
         wave3_idx = GetMinIdxPrc((short)(wave0_idx + 1), (short)(wave4_idx - 1));
         wave1_idx = GetWave1IdxDn(wave0_idx, wave3_idx, 50);
         wave2_idx = GetWave24IdxDn(wave1_idx, wave3_idx);
         idx = (short)(beg_idx + (end_idx - beg_idx) * 618L / 1000);
         wave3_idx = GetMaxLoTpLeft(idx, wave3_idx);
         FitWave1_2IdxDn(wave0_idx, wave3_idx, ref wave1_idx, ref wave2_idx);
         wave4_idx = GetWave24IdxDn(wave3_idx, wave5_idx);
         wave1_idx = GetMinIdxPrc((short)(wave0_idx + 1), wave1_idx);
         wave3_idx = GetMinIdxPrc(wave3_idx, (short)(wave4_idx - 1));

         idx = (short)(beg_idx + (end_idx - beg_idx) * 90L / 100);
         for (tries = 0;
            getHi(wave2_idx) - getLo(wave3_idx) < getHi(wave0_idx) - getLo(wave1_idx) &&
            tries < 3 &&
            wave3_idx <= idx; ++tries)
         {
            wave3_idx = GetMaxLoTpRight(wave4_idx, idx);
            wave4_idx = GetWave24IdxDn(wave3_idx, wave5_idx);
            wave3_idx = GetMinIdxPrc(wave3_idx, (short)(wave4_idx - 1));
            wave1_idx = GetWave1IdxDn(wave0_idx, wave3_idx, 50);
            wave2_idx = GetWave24IdxDn(wave1_idx, wave3_idx);
            FitWave1_2IdxDn(wave0_idx, wave3_idx, ref wave1_idx, ref wave2_idx);
         }
         wave1_idx = RollWave13DnLeft(wave0_idx, wave1_idx, wave2_idx);
         wave3_idx = RollWave13DnLeft(wave2_idx, wave3_idx, wave4_idx);
         wave2_idx = RollWave24DnRight(wave1_idx, wave2_idx, wave3_idx);
         wave4_idx = RollWave24DnRight(wave3_idx, wave4_idx, wave5_idx);
         if (getHi(wave4_idx) <= GetXPctRtcWaveDn(wave0_idx, wave1_idx, EWPct4Overlap1) &&
            wave3_idx < wave5_idx &&
            getHi(wave2_idx) - getLo(wave3_idx) >= getHi(wave0_idx) - getLo(wave1_idx))
         {
            SetWave(wave0_idx, wave1_idx, -1, WAVE_STAT_OK);
            SetWave(wave1_idx, wave2_idx, -2, WAVE_STAT_OK);
            SetWave(wave2_idx, wave3_idx, -3, WAVE_STAT_OK);
            SetWave(wave3_idx, wave4_idx, -4, WAVE_STAT_OK);
            SetWave(wave4_idx, wave5_idx, -5, WAVE_STAT_OK);
            ++c_group;
            return 1;		/* yes wave subdivided	*/
         }
         return 0;		/* no it didn't		*/
      }

      private void SetWave(short beg_idx, short end_idx, short wave, short wave_stat)
      {
 	      OneWave ow = new OneWave();
  	      ow.wave = wave;
  	      ow.stat = wave_stat;
  	      ow.group = c_group;
  	      ow.level = c_level;
  	      ow.beg_idx = beg_idx;
  	      ow.end_idx = end_idx;

         int index = waves.BinarySearch(ow);
         if (index >= 0)
         {
            waves[index] = ow;
         }
         else
         {
            waves.Insert(~index, ow);
         }
      }

      private void FixWavesL1()
      {
         Cnvt5ABCTo5Up();
         Cnvt5ABCTo5Dn();
         MeldDuplicateUpWaves();
         MeldDuplicateDnWaves();
      }

      private void Cnvt5ABCTo5Up()
      {
         short c;

         for (c = 0; c < waveCount() - 3; ++c)
         {
            if (waves[c].wave == 5 &&
               waves[c + 1].wave == 7 &&
               waves[c + 2].wave == 8 &&
               waves[c + 3].wave == 9)
            {

               waves[c].stat = WAVE_STAT_OK;		/* 1/24/92	*/

               waves[c].end_idx = waves[c + 3].end_idx;

               RemoveWave((short)(c + 1));	/* remove the 'A'	*/
               RemoveWave((short)(c + 1));	/* remove the 'B'	*/
               RemoveWave((short)(c + 1));	/* remove the 'C'	*/
               --c;						/* look for more	*/
            }
         }
      }

      private void Cnvt5ABCTo5Dn()
      {
         short c;

         for (c = 0; c < waveCount() - 3; ++c)
         {
            if (waves[c].wave == -5 &&
               waves[c + 1].wave == -7 &&
               waves[c + 2].wave == -8 &&
               waves[c + 3].wave == -9)
            {

               waves[c].stat = WAVE_STAT_OK;		/* 1/24/92	*/

               waves[c].end_idx = waves[c + 3].end_idx;

               RemoveWave((short)(c + 1));	/* remove the -'A'	*/
               RemoveWave((short)(c + 1));	/* remove the -'B'	*/
               RemoveWave((short)(c + 1));	/* remove the -'C'	*/
               --c;						/* look for more	*/
            }
         }
      }

      private void MeldDuplicateUpWaves()
      {
         short c, sav_c, beg_idx, end_idx, osc_max_idx;
         short group;

         for (c = (short)(waveCount() - 1); c >= 3; --c)
         {
            if (waves[c].group != waves[c - 1].group &&
               waves[c].wave > 0 && waves[c - 1].wave > 0)
            {
               sav_c = c;
               for (group = waves[c].group, ++c;
                  c < waveCount() && waves[c].group == group; ++c)
               {
               }
               end_idx = waves[c - 1].end_idx;
               for (c = (short)(sav_c - 1); ; )
               {
                  for (group = waves[c].group, --c;
                     c >= 0 && waves[c].group == group;
                     --c)
                  {
                  }
                  if (c >= 0 && waves[c].wave > 0)  	/* another one!!!	*/
                     continue;
                  break;
               }
               beg_idx = waves[c + 1].beg_idx;
               osc_max_idx = GetOscMaxIdxUp(osc, beg_idx, end_idx);
               RemoveWavesByIdx(beg_idx, end_idx);
               FndUpWaves(beg_idx, end_idx, osc_max_idx, 1);
            }
         }
      }

      private void MeldDuplicateDnWaves()
      {
         short c, sav_c, beg_idx, end_idx, osc_max_idx;
         short group;

         for (c = (short)(waveCount() - 1); c >= 3; --c)
         {
            if (waves[c].group != waves[c - 1].group &&
            waves[c].wave < 0 && waves[c - 1].wave < 0)
            {
               sav_c = c;
               for (group = waves[c].group, ++c;
                  c < waveCount() && waves[c].group == group; ++c)
               {
               }
               end_idx = waves[c - 1].end_idx;
               for (c = (short)(sav_c - 1); ; )
               {
                  for (group = waves[c].group, --c;
                     c >= 0 && waves[c].group == group;
                     --c)
                  {
                  }
                  if (c >= 0 && waves[c].wave < 0)  	/* another one!!!	*/
                     continue;
                  break;
               }
               beg_idx = waves[c + 1].beg_idx;
               osc_max_idx = GetOscMaxIdxDn(osc, beg_idx, end_idx);
               RemoveWavesByIdx(beg_idx, end_idx);
               FndDnWaves(beg_idx, end_idx, osc_max_idx, 1);
            }
         }
      }

      private short GetOscMaxIdxUp(Series osc, short beg_idx, short end_idx)
      {
	      short c;
  	      short osc_max_idx = beg_idx;

  	      for(c = (short)(beg_idx + 1); c <= end_idx; ++c) {
   	      if( getOsc(c) > getOsc(osc_max_idx) ) {
   	  	      osc_max_idx = c;
            }
         }
  	      return osc_max_idx;
      }

      private short GetOscMaxIdxDn(Series osc, short beg_idx, short end_idx)
      {
	      short c;
  	      short osc_max_idx = beg_idx;

  	      for(c = (short)(beg_idx + 1); c <= end_idx; ++c) {
   	      if( getOsc(c) < getOsc(osc_max_idx) ) {
   	  	      osc_max_idx = c;
            }
         }
  	      return osc_max_idx;
      }

      private void RemoveWave(short idx)
      {
         if (idx < waveCount())
         {
            waves.RemoveAt(idx);
         }
      }

      private void RemoveWavesByIdx(short beg_idx, short end_idx)
      {
         short c;
         for (c = 0; c < waveCount(); )
         {
            if (waves[c].beg_idx >= beg_idx && waves[c].end_idx <= end_idx)
               RemoveWave(c);
            else
               ++c;
         }
      }

      private void SetWaveUpOdds(List<OneWave> waves, short idx, EWProj pprj)
      {
	      short  c,n,pct_area,pct_prc;
	      double wave3_len,wave4_len;
	      double base1;
  	      double wave3_area,wave4_area;

         c = waves[ idx + 2 ].beg_idx;
	      base1 = getLo(c);
	      for(wave3_area = 0L,n = 0;
   	      c <= waves[ idx + 2 ].end_idx; ++c,++n) {
    	      if( getHi(c) > base1 )
      	      wave3_area += (getHi(c) - base1);
         }
  	      if( n != 0 )
		      wave3_area /= n;

         c = waves[ idx + 3 ].beg_idx;
	      base1 = getHi(c);
  	      for(wave4_area = 0L,n = 0;
   	      c <= waves[ idx + 3 ].end_idx; ++c,++n) {
    	      if( getLo(c) < base1 )
      	      wave4_area += (base1 - getLo(c));
         }
  	      if( n != 0 )
   	      wave4_area /= n;
  	      if( wave3_area == 0)
   	      wave3_area = 1;
  	      if((pct_area = (short)((100L*wave4_area/wave3_area)/5) ) > 59 || pct_area < 0)
   	      pct_area = 59;
  	      wave3_len = getHi(waves[ idx + 2 ].end_idx) - getLo(waves[ idx + 2 ].beg_idx);
  	      wave4_len = getHi(waves[ idx + 3 ].beg_idx) - getLo(waves[ idx + 3 ].end_idx);
  	      if( wave3_len == 0 )
   	      wave3_len = 1;
  	      if((pct_prc = (short)((100L * wave4_len / wave3_len) / 5)) < 0)
   	      pct_prc = 0;
  	      else if(pct_prc > 19)
   	      pct_prc = 19;
  	      pprj.dir = 1;
  	      pprj.channel_len = (short)(waves[ idx + 2 ].end_idx - waves[ idx ].beg_idx);
  	      pprj.channel_beg_idx = waves[ idx + 3 ].beg_idx;
  	      pprj.channel_end_idx = waves[ idx + 3 ].end_idx;
  	      pprj.odds = WaveOddsTable1[pct_area, pct_prc];
  	      pprj.odds_prc = getLo(waves[ idx + 1 ].end_idx);
  	      pprj.odds_idx = waves[ idx + 4 ].end_idx;      
      }

      private void SetWaveDnOdds(List<OneWave> waves, short idx, EWProj pprj)
      {
	      short  c,n,pct_area,pct_prc;
	      double wave3_len,wave4_len;
	      double base1;
  	      double wave3_area,wave4_area;

         c = waves[ idx + 2 ].beg_idx;
	      base1 = getHi(c);
	      for(wave3_area = 0L,n = 0;
   	      c <= waves[ idx + 2 ].end_idx; ++c,++n) {
  		      if( getLo(c) < base1 )
   		      wave3_area += (base1 - getLo(c));
         }
  	      if( n != 0 )
		      wave3_area /= n;

         c = waves[ idx + 3 ].beg_idx;
	      base1 = getLo(c);
	      for(wave4_area = 0L,n = 0;
   	      c <= waves[ idx + 3 ].end_idx; ++c,++n) {
    	      if( getHi(c) > base1 )
      	      wave4_area += (getHi(c) - base1);
         }
  	      if( n != 0 )
   	      wave4_area /= n;
  	      if( wave3_area == 0 )
   	      wave3_area = 1;
  	      if((pct_area = (short)((100L*wave4_area/wave3_area)/5)) > 59 || pct_area < 0)
   	      pct_area = 59;
  	      wave3_len = getHi(waves[ idx + 2 ].beg_idx) - getLo(waves[ idx + 2 ].end_idx);
  	      wave4_len = getHi(waves[ idx + 3 ].end_idx) - getLo(waves[ idx + 3 ].beg_idx);
  	      if( wave3_len == 0 )
   	      wave3_len = 1;
  	      if((pct_prc = (short)((100L*wave4_len/wave3_len)/5)) < 0 )
   	      pct_prc = 0;
  	      else if(pct_prc > 19)
   	      pct_prc = 19;
  	      pprj.dir = -1;
  	      pprj.channel_len = (short)(waves[ idx + 2 ].end_idx - waves[ idx ].beg_idx);
  	      pprj.channel_beg_idx = waves[ idx + 3 ].beg_idx;
  	      pprj.channel_end_idx = waves[ idx + 3 ].end_idx;
  	      pprj.odds = WaveOddsTable1[pct_area, pct_prc];
  	      pprj.odds_prc = getHi(waves[ idx + 1 ].end_idx);
  	      pprj.odds_idx = waves[ idx + 4 ].end_idx;
      }

      private void SetWaveProj123Up(short idx)
      {
         EWProj pprj = new EWProj();
         short prjIdx = 0;
         ewproj[prjIdx] = pprj;

         double prc = 0;
         double wave1_len, wave3_len, LenForWave5;
         short cfct, trig;
         double wave1_rtc, wave0_prc, wave1_prc, wave2_prc, wave3_prc, wave4_prc, wave5_prc, nwave4_prc, nwave3_prc;
         double RatioWave3To1;

         if (idx < waveCount() - 4)
         {
            wave0_prc = getLo(waves[idx].beg_idx);
            wave1_prc = getHi(waves[idx].end_idx);
            wave2_prc = getLo(waves[idx + 1].end_idx);
            wave3_prc = getHi(waves[idx + 2].end_idx);
            wave4_prc = getLo(waves[idx + 3].end_idx);
            wave5_prc = getHi(GetMaxIdxPrc(waves[idx + 4].beg_idx, waves[idx + 4].end_idx));
            wave1_len = wave1_prc - wave0_prc;
            wave3_len = wave3_prc - wave2_prc;
            if (wave1_len <= 0 || wave3_len <= 0) return;
            nwave3_prc = wave3_prc;
            if (waves[idx + 3].stat != WAVE_STAT_OK)
            {	/* wave 4 not yet found */
               for (trig = 0, cfct = START3_5; cfct < N_FIB_FACTORS; ++cfct)
               {
                  if ((prc = wave2_prc + (double)((long)FibFactors[cfct] * wave1_len / 1000L)) >= wave3_prc)
                  {
                     pprj.pct = FibFactors[cfct];
                     pprj.prc = prc;
                     pprj.wave = 3;
                     pprj.idx = waves[idx + 2].end_idx;

                     pprj = new EWProj();
                     prjIdx++;
                     ewproj[prjIdx] = pprj;

                     if (trig == 0) trig = 1;
                     else break;
                  }
               }
               nwave3_prc = prc;
               wave1_rtc = GetXPctRtcWaveUp(waves[idx].beg_idx, waves[idx].end_idx, 15);
               nwave4_prc = wave4_prc;
               for (cfct = trig = 0; cfct < N_FIB_FACTORS; ++cfct)
               {
                  if ((prc = wave3_prc - (double)((long)FibFactors[cfct] * wave3_len / 1000L)) <= wave4_prc &&
                     prc >= wave1_rtc)
                  {
                     pprj.pct = FibFactors[cfct];
                     pprj.prc = prc;
                     pprj.wave = 4;
                     pprj.idx = waves[idx + 3].end_idx;

                     pprj = new EWProj();
                     prjIdx++;
                     ewproj[prjIdx] = pprj;

                     if (trig == 0)
                     {
                        nwave4_prc = prc;
                        trig = 1;
                     }
                     else
                        break;
                  }
               }
               wave4_prc = nwave4_prc;
            }
            if (wave1_len == 0)
               wave1_len = 1;
            RatioWave3To1 = (short)(100L * wave3_len / wave1_len);
            LenForWave5 = (RatioWave3To1 >= 175) ? wave3_len : wave3_prc - wave0_prc;
            for (trig = 0, cfct = START3_5; cfct < N_FIB_FACTORS; ++cfct)
            {
               if ((prc = wave4_prc + (double)((long)FibFactors[cfct] * LenForWave5 / 1000L)) >= wave5_prc &&
                  prc > nwave3_prc)
               {							/* higher than wave3 projections	*/
                  pprj.pct = FibFactors[cfct];
                  pprj.prc = prc;
                  pprj.wave = 5;
                  pprj.idx = waves[idx + 4].end_idx;
                  if (trig == 0 && waves[idx + 2].stat == WAVE_STAT_OK && waves[idx + 4].stat != WAVE_STAT_OK &&
                     getHi((short)(barCount() - 1)) < getHi(waves[idx + 2].end_idx))
                     SetWaveUpOdds(waves, idx, pprj);

                  pprj = new EWProj();
                  prjIdx++;
                  ewproj[prjIdx] = pprj;

                  if (trig == 0) trig = 1;
                  else break;
               }
            }
         }
      }

      private void SetWaveProj123Dn(short idx)
      {
         EWProj pprj = new EWProj();
         short prjIdx = 0;
         ewproj[prjIdx] = pprj;

         double prc = 0;
         double wave1_len, wave3_len, LenForWave5;
         short cfct, trig;
         double wave0_prc, wave1_prc, wave2_prc, wave3_prc, wave4_prc, wave5_prc, nwave4_prc, nwave3_prc, wave1_rtc;
         double RatioWave3To1;

         if (idx < waveCount() - 4)
         {
            wave0_prc = getHi(waves[idx].beg_idx);
            wave1_prc = getLo(waves[idx].end_idx);
            wave2_prc = getHi(waves[idx + 1].end_idx);
            wave3_prc = getLo(waves[idx + 2].end_idx);
            wave4_prc = getHi(waves[idx + 3].end_idx);
            wave5_prc = getLo(GetMinIdxPrc(waves[idx + 4].beg_idx, waves[idx + 4].end_idx));
            wave1_len = wave0_prc - wave1_prc;
            wave3_len = wave2_prc - wave3_prc;

            if (wave1_len <= 0 || wave3_len <= 0) return;
            nwave3_prc = wave3_prc;
            if (waves[idx + 3].stat != WAVE_STAT_OK)
            {	/* wave 4 not yet found */
               for (trig = 0, cfct = START3_5; cfct < N_FIB_FACTORS; ++cfct)
               {
                  if ((prc = wave2_prc - (double)((long)FibFactors[cfct] * wave1_len / 1000L)) <= wave3_prc)
                  {
                     pprj.pct = (short)(FibFactors[cfct] / 10);
                     pprj.prc = prc;
                     pprj.wave = 3;
                     pprj.idx = waves[idx + 2].end_idx;

                     pprj = new EWProj();
                     prjIdx++;
                     ewproj[prjIdx] = pprj;

                     if (trig == 0) trig = 1;
                     else break;
                  }
               }
               nwave3_prc = prc;
               wave1_rtc = GetXPctRtcWaveDn(waves[idx].beg_idx, waves[idx].end_idx, 15);
               nwave4_prc = wave4_prc;
               for (cfct = trig = 0; cfct < N_FIB_FACTORS; ++cfct)
               {
                  if ((prc = wave3_prc + (double)((long)FibFactors[cfct] * wave3_len / 1000L)) >= wave4_prc &&
                     prc <= wave1_rtc)
                  {
                     pprj.pct = (short)(FibFactors[cfct] / 10);
                     pprj.prc = prc;
                     pprj.wave = 4;
                     pprj.idx = waves[idx + 3].end_idx;

                     pprj = new EWProj();
                     prjIdx++;
                     ewproj[prjIdx] = pprj;

                     if (trig == 0)
                     {
                        nwave4_prc = prc;
                        trig = 1;
                     }
                     else
                        break;
                  }
               }
               wave4_prc = nwave4_prc;
            }
            if (wave1_len == 0)
               wave1_len = 1;
            RatioWave3To1 = (short)(100L * wave3_len / wave1_len);
            LenForWave5 = (RatioWave3To1 >= 175) ? wave3_len : wave0_prc - wave3_prc;
            for (trig = 0, cfct = START3_5; cfct < N_FIB_FACTORS; ++cfct)
            {
               if ((prc = wave4_prc - (double)((long)FibFactors[cfct] * LenForWave5 / 1000L)) <= wave5_prc &&
                  prc < nwave3_prc)
               {		/* lower than wave3 projection	*/
                  pprj.pct = (short)(FibFactors[cfct] / 10);
                  pprj.prc = prc;
                  pprj.wave = 5;
                  pprj.idx = waves[idx + 4].end_idx;
                  if (trig == 0 && waves[idx + 2].stat == WAVE_STAT_OK && waves[idx + 4].stat != WAVE_STAT_OK &&
                     getLo((short)(barCount() - 1)) > getLo(waves[idx + 2].end_idx))
                     SetWaveDnOdds(waves, idx, pprj);

                  pprj = new EWProj();
                  prjIdx++;
                  ewproj[prjIdx] = pprj;

                  if (trig == 0) trig = 1;
                  else break;
               }
            }
         }
      }

      private void WaveProjections()
      {
         short c;
         short group;

         for (int ii = 0; ii < 10; ii++)
         {
            ewproj[ii] = null;
         }

         if ((c = (short)(waveCount() - 1)) >= 4)
         {
            for (group = waves[c].group;
               c >= 0 && waves[c].group == group; --c)
            {
            }
            ++c;
            if (waves[c].wave < 0 && waves[c].wave >= -5)
               SetWaveProj123Dn(c);
            else if (waves[c].wave > 0 && waves[c].wave <= 5)
               SetWaveProj123Up(c);
            for (--c; c >= 0; --c)
               waves[c].stat = WAVE_STAT_OK;
         }
      }

      private short GetMaxIdxPrc(short beg_idx, short idx)
      {
	      short c;
  	      double max = getHi(idx);

  	      for(c = (short)(idx - 1); c >= beg_idx ; --c) {
   	      if(getHi(c) > max) {
      	      max = getHi(c);
      	      idx = c;
            }
         }
  	      return idx;
      }

      private short GetMinIdxPrc(short beg_idx, short idx)
      {
	      short c;
  	      double min = getLo(idx);

  	      for(c = (short)(idx - 1); c >= beg_idx ; --c) {
   	      if(getLo(c) < min) {
      	      min = getLo(c);
      	      idx = c;
            }
         }
  	      return idx;
      }

      private short GetLoTpLeft(short beg_idx, short end_idx, short time)
      {
	      short c;
  	      double min;
  	      short idx,i,bad_time,ok_time,ok_idx;

  	      ok_time = bad_time = 0;
  	      for(ok_idx = idx = end_idx;idx >= 0;) {
  		      short ii1 = idx;
    	      for(c = 0, i = (short)(idx - 1), min = getLo(ii1--); c < time && i >= 0; --i,++c,--ii1) {
      	      if( getLo(ii1) < min ) {
				      min = getLo(ii1);                      /* reset max			*/
				      if((idx = i) < beg_idx)
	  				      break; 			/* move idx			*/
				      c = -1;				/* zero c: will ++ above  	*/
			      }
   	      }
   	      if( (c == time || i < 0) && idx >= -1 && idx < barCount() - 1 ) {
  			      short ii2 = (short)(idx + 1);
        	      int cnt = (barCount() - (idx + 1) < time)?
       		      barCount() - (idx + 1) : time;
      	      for(c = 0; c < cnt && getLo(ii2) >= min; ++c,++ii2)
			      {
			      }
      	      if( c == cnt ) {
				      if( time < bad_time - 1 ) {
	  				      ok_time = time;
	  				      ok_idx = idx;
	  				      time = (short)((bad_time + ok_time) >> 1);
	  				      if(time == ok_time) return ok_idx;
	  				      idx = end_idx;
	 				      continue;
	  			      }
				      else
	  				      return idx;
			      }
      	      else if( i >= beg_idx ) {
				      idx = i;
				      continue;
			      }
   	      }
   	      bad_time = time;			/* remember time		*/
  		      time = (short)((bad_time + ok_time) >> 1);	/* so...relax time		*/
   	      if(time == ok_time) return ok_idx;
   	      idx = end_idx;			/* and reset to start		*/
	      }
	      return ok_idx;
      }

      private short GetHiTpLeft(short beg_idx, short end_idx, short time)
      {
	      short c;
 	      double max;
  	      short idx,i,ok_time,bad_time,ok_idx;

  	      bad_time = ok_time = 0;
  	      for(ok_idx = idx = end_idx;idx >= 0;) {
  		      short ii1 = idx;
    	      for(c = 0, i = (short)(idx - 1), max = getHi(ii1--); c < time && i >= 0; --i,++c,--ii1) {
      	      if( getHi(ii1) > max ) {
				      max = getHi(ii1);                      /* reset max			*/
				      if((idx = i) < beg_idx)
	  				      break; 			/* move idx			*/
				      c = -1;				/* zero c: will ++ above  	*/
			      }
            }
    	      if( (c == time || i < 0) && idx >= -1 && idx < barCount() - 1) {
  			      short ii2 = (short)(idx + 1);
       	      int cnt = (barCount() - (idx + 1) < time)?
       		      barCount() - (idx + 1) : time;
	     	      for(c = 0; c < cnt && getHi(ii2) <= max ;++c,++ii2)
			      {
			      }
      	      if( c == cnt ) {
				      if( time < bad_time - 1 ) {
	 				      ok_time = time;
	  				      ok_idx = idx;
	  				      time = (short)((bad_time + ok_time) >> 1);
	  				      if(time == ok_time) return ok_idx;
	  				      idx = end_idx;
	  				      continue;
	  			      }
				      else
	  				      return idx;
			      }
     	 	      else if( i >= beg_idx ) {
				      idx = i;
				      continue;
			      }
   	      }
   	      bad_time = time;			/* remember time		*/
   	      time = (short)((bad_time + ok_time) >> 1);	/* so...relax time		*/
   	      if(time == ok_time) return ok_idx;
   	      idx = end_idx;			/* and reset to start		*/
          }
          return ok_idx;     
      }

      private short GetLoTpRight(short beg_idx, short end_idx, short time)
      {
	      short i,c;
  	      double min;
  	      short idx,ok_time,bad_time,ok_idx;

  	      ok_time = bad_time = 0;
  	      for(ok_idx = idx = beg_idx;idx < barCount ();) {
  		      short ii1 = idx;
   	      for(c = 0, i = (short)(idx + 1),min = getLo(ii1++); c < time && i < barCount (); ++i,++c,++ii1) {
	            if( getLo(ii1) < min ) {
				      min = getLo(ii1);                      /* reset max			*/
				      if((idx = i) > end_idx) 	/* move idx			*/
		  			      break;			/* faster if check only here	*/
				      c = -1;				/* will ++ above  		*/
			      }
  	 	      }
  		      if( (c == time || i == barCount ()) && idx > 0 && idx <= barCount ()) {
  			      short ii2 = (short)(idx - 1);
       	      int cnt = (idx < time)? idx : time;
	     	      for(c = 0; c < cnt && getLo(ii2) >= min; ++c,--ii2)
			      {
			      }
   	  	      if( c == cnt ) {
				      if( time < bad_time - 1 ) {
	  				      ok_time = time;
	  				      ok_idx = idx;
	  				      time = (short)((bad_time + ok_time) >> 1);
	  				      if(time == ok_time) return ok_idx;
	  				      idx = beg_idx;
	  				      continue;
	 			      }
				      else
	  				      return idx;
			      }
      	      else if( i <= end_idx ) {    	/* did not search to end	*/
				      idx = i;			/* skip idx to here		*/
				      continue;			/* continue: don't fall below	*/
			      }
   	      }
   	      bad_time = time;			/* remember time		*/
   	      time = (short)((bad_time + ok_time) >> 1);	/* so...relax time		*/
   	      if(time == ok_time) return ok_idx;
   	      idx = beg_idx;			/* and reset to start		*/
         }
         return ok_idx;
      }

	   private short GetHiTpRight(short beg_idx, short end_idx, short time)
      {
	      short i,c;
  	      double max;
  	      short idx,ok_time,bad_time,ok_idx;

  	      ok_time = bad_time = 0;
  	      for(ok_idx = idx = beg_idx;idx < barCount ();) {
  		      short ii1 = idx;
    	      for(c = 0, i = (short)(idx + 1), max = getHi(ii1++); c < time && i < barCount (); ++i,++c,++ii1) {
      	      if( getHi(ii1) > max ) {
				      max = getHi(ii1);                     	/* reset max			*/
				      if((idx = i) > end_idx)   			/* move idx			*/
	  				      break; 		        				/* faster if check only here	*/
				      c = -1;									/* will ++ above  		*/
			      }
            }
    	      if( (c == time || i == barCount ()) && idx > 0 && idx <= barCount () ) {
    		      short ii2 = (short)(idx - 1);
      	      int cnt = (idx < time)? idx : time;
			      for(c = 0; c < cnt && getHi(ii2) <= max; ++c,--ii2)
			      {
			      }
     	 	      if( c == cnt ) {
				      if( time < bad_time - 1 ) {
	  				      ok_time = time;
	  				      ok_idx = idx;
	  				      time = (short)((bad_time + ok_time) >> 1);
	  				      if(time == ok_time) return ok_idx;
	  				      idx = beg_idx;
	  				      continue;
	  			      }
				      else
	  				      return idx;
			      }
      	      else if( i <= end_idx ) {    	/* did not search to end	*/
				      idx = i;			/* skip idx to here		*/
				      continue;			/* continue: don't fall below	*/
			      }
            }
    	      bad_time = time;			/* remember time		*/
    	      time = (short)((bad_time+ok_time) >> 1);	/* so...relax time		*/
    	      if(time == ok_time) return ok_idx;
    	      idx = beg_idx;			/* and reset to start		*/
          }
          return ok_idx;
      }

      private short GetHiTpLen(short idx)
      {
	      double max;
  	      short l,r,left,rite;

	      short ii = idx;
	      max = getHi(ii--);
	      for(left = 1, l = (short)(idx - 1);
		      l != 0 && getHi(ii) <= max; ++left,--ii,--l)
	      {
	      }

	      ii = idx;
	      max = getHi(ii++);
	      for(rite = 1, r = (short)(idx + 1);
            r < barCount () && getHi(ii) <= max; ++rite,++ii,++r)
	      {
	      }

  	      if( l < 0 && r == barCount ()) return( Math.Max(left,rite) );
  	      if( l < 0 ) return rite;
  	      if( r == barCount ()) return left;
	      return( Math.Min(left,rite) );
      }

      private short GetLoTpLen(short idx)
      {
	      double min;
  	      short l,r,left,rite;
         
         short ii = idx;
	      min = getLo(ii--);
	      for(left = 1, l = (short)(idx - 1);
		      l != 0 && getLo(ii) >= min; ++left,--ii,--l)
	      {
	      }

         ii = idx;
	      min = getLo(ii++);
	      for(rite = 1, r = (short)(idx + 1);
            r < barCount () && getLo(ii) >= min; ++rite,++ii,++r)
	      {
	      }

  	      if( l < 0 && r == barCount ()) return( Math.Max(left,rite) );
  	      if( l < 0 ) return rite;
  	      if( r == barCount ()) return left;
	      return( Math.Min(left,rite) );
      }

      private short GetBegOfWaveUp(short beg_idx, short osc_max_idx, short tp_time)
      {
         double max, min, rally;
         double fract_osc;
         short c, lo_idx;

         fract_osc = -(getOsc(osc_max_idx)) * 618 / 1000;
         if ((c = (short)(osc_max_idx + 20)) >= barCount())
            c = (short)(barCount() - 1);
         max = getHi(GetMaxIdxPrc(osc_max_idx, c));
         rally = min = MAX_VALUE;
         for (lo_idx = c = osc_max_idx;
            c > beg_idx && (getOsc(c) > fract_osc || getOsc(c) >= getOsc((short)(c - 1))); --c)
         {
            if (getLo(c) <= min)
            {									/* use <= because we're going left	*/
               min = getLo(c);
               lo_idx = c;
               if (getOsc(c) <= 0)
                  rally = min + (max - min) * 42 / 100;	/* now look for counter rally	*/
            }
            if (getHi(c) > max) max = getHi(c);
            if (getHi(c) > rally) 									/* went too far			*/
               break;
         }
         return (GetLoTpLeft(beg_idx, lo_idx, tp_time));
      }

      private short GetBegOfWaveDn(short beg_idx, short osc_max_idx, short tp_time)
      {
         double max, min, decline;
         double fract_osc;
         short c, hi_idx;

         fract_osc = -(getOsc(osc_max_idx)) * 618 / 1000;
         if ((c = (short)(osc_max_idx + 20)) >= barCount())
            c = (short)(barCount() - 1);
         min = getLo(GetMinIdxPrc(osc_max_idx, c));
         max = decline = MIN_VALUE;
         for (hi_idx = c = osc_max_idx;
            c > beg_idx && (getOsc(c) < fract_osc || getOsc(c) <= getOsc((short)(c - 1))); --c)
         {
            if (getHi(c) >= max)
            {							/* use >= because we're going left	*/
               max = getHi(c);
               hi_idx = c;
               if (getOsc(c) >= 0)
                  decline = max - (max - min) * 42 / 100;
            }
            if (getLo(c) <= min) min = getLo(c);
            if (getLo(c) < decline)
               break;
         }
         return (GetHiTpLeft(beg_idx, hi_idx, tp_time));
      }

      private short GetWave1IdxUp(short wave0_idx, short wave3_idx, short max_pct)
      {
  	      short idx;
  	      short beg_idx,end_idx;
  	      double dis,max,min;

  	      dis = getHi(wave3_idx) - getLo(wave0_idx);
  	      max = getLo(wave0_idx) + (long)max_pct*dis/100;
  	      min = getLo(wave0_idx) + 5L*dis/100;
  	      beg_idx = (short)(wave0_idx + 1); 									/* Plus 1 as not to equal wave0_idx	*/
  	      end_idx = wave3_idx;
  	      for(idx = beg_idx; idx < wave3_idx; ++idx) {
   	      if( getHi(idx) >= min && beg_idx == wave0_idx+1 )
      	      beg_idx = idx;
    	      if( getHi(idx) > max ) {
      	      end_idx = idx;
      	      break;
            }
         }
  	      return( GetMaxHiTpRight(beg_idx,end_idx) );
      }

      private short GetWave1IdxDn(short wave0_idx, short wave3_idx, short max_pct)
      {
  	      short idx;
  	      short beg_idx,end_idx;
  	      double dis,max,min;

  	      dis = getHi(wave0_idx) - getLo(wave3_idx);
  	      max = getHi(wave0_idx) - 5L*dis/100;
  	      min = getHi(wave0_idx) - (long)max_pct*dis/100;
  	      beg_idx = (short)(wave0_idx + 1);	/* Plus one as not to equal wave0_idx	*/
  	      end_idx = wave3_idx;
  	      for(idx = beg_idx; idx < wave3_idx; ++idx) {
   	      if( getLo(idx) <= max && beg_idx == wave0_idx+1 )
      	      beg_idx = idx;
    	      if( getLo(idx) < min ) {
      	      end_idx = idx;
      	      break;
            }
         }
  	      return( GetMaxLoTpRight(beg_idx,end_idx) );
      }

      private short GetMaxHiTpRight(short beg_idx, short end_idx)
      {
         return (GetHiTpRight(beg_idx, end_idx, (short)(barCount() - 1)));
      }

      private short GetMaxLoTpRight(short beg_idx, short end_idx)
      {
         return (GetLoTpRight(beg_idx, end_idx, (short)(barCount() - 1)));
      }

      private short GetMaxHiTpLeft(short beg_idx, short end_idx)
      {
         return (GetHiTpLeft(beg_idx, end_idx, (short)(barCount() - 1)));
      }

      private short GetMaxLoTpLeft(short beg_idx, short end_idx)
      {
         return (GetLoTpLeft(beg_idx, end_idx, (short)(barCount() - 1)));
      }

      private bool IsBegOfWaveAt(short wave_level, short idx)
      {
	      short c;
  	      short n_waves;

  	      for(c = 0, n_waves = waveCount (); c < n_waves; ++c)
   	      if( waves[ c ].level == wave_level && waves[ c ].beg_idx == idx )
      	      return true;
  		   return false;
      }

      private short GetWave4OscIdxUp(short wave0_idx, short osc_max_idx, short end_idx, out short wave4_stat)
      {
	      short c;
  	      double max,min,decline;
         double wave4_sig;
         double wave4_osc_sig;
         double osc_val, osc_peak;
  	      short idx;
  	      short osc_idx;
  	      short factor;

  	      osc_peak = getOsc(osc_max_idx);
  	      wave4_stat = WAVE_STAT_OK;
  	      for(max = MIN_VALUE,c = idx = wave0_idx; c < osc_max_idx; ++c) {
   	      if( getHi(c) > max ) {
     		      max = getHi(c);
      	      idx = c;
            }
         }
  	      min = getLo(wave0_idx);
  	      decline = max - (max - min) * 15 / 100;
  	      wave4_osc_sig = osc_peak * 15 / 100;
  	      if((c = idx) < MIN_WAVE_PERS)
   	      c = MIN_WAVE_PERS;
  	      wave4_sig = 0;
  	      for(; c < end_idx; ++c) {
   	      if( c > osc_max_idx && getOsc(c) <= wave4_osc_sig )	/* new c > osc_max_idx	*/
      	      wave4_sig = 1;                 					/* set signal		*/
    	      if( wave4_sig != 0 && getLo(c) <= decline )
      	      break;													/* found a four		*/
    	      else if( getHi(c) > max ) {									/* new high ?		*/
      	      max = getHi(c);												/* reset max		*/
      	      decline = max - (max - min) * 15 / 100;	   /* new decline		*/
            }
         }
  	      if( c < end_idx )
   	      return(c);
  	      osc_val = osc_peak * 80 / 100;
  	      osc_idx = -1;													/* -5 to ensure not same peak	*/
  	      for(c = wave0_idx; c < osc_max_idx-5; ++c) {
   	      if( getOsc(c) > osc_val ) {
      	      osc_val = getOsc(c);
      	      osc_idx = c;
            }
         }
  	      if( osc_idx != -1 ) {
   	      osc_val = osc_val * 50 / 100;
    	      for(c = osc_idx,osc_idx = -1; c < osc_max_idx; ++c) {
      	      if( getOsc(c) < osc_val ) {
				      osc_val = getOsc(c);
				      osc_idx = c;
			      }
            }
    	      if( osc_idx != -1 )
     		      return osc_idx;
         }
  	      wave4_stat = WAVE_STAT_4FAILED;
  	      if( c_level == 0 && !IsBegOfWaveAt(c_level,end_idx) )
   	      return (short)(end_idx - 1);
  	      factor = (short)((c_level == 0) ? 500 : 618);
  	      osc_idx = (short)(wave0_idx+(long)(end_idx-wave0_idx)*factor/1000);
  	      idx = GetOscMaxIdxDn(osc, osc_idx, (short)(end_idx - 1));
  	      if( idx < osc_max_idx )
         idx = GetMaxLoTpLeft(osc_idx, (short)(end_idx - 1));		/* was from right	*/
  	      return(idx);
      }

      private short GetWave4OscIdxDn(short wave0_idx, short osc_max_idx, short end_idx, out short wave4_stat)
      {
	      short c;
  	      double max, min, rally;
  	      double wave4_sig,idx, wave4_osc_sig, osc_val, osc_peak;
  	      short osc_idx;
  	      short factor;

  	      osc_peak = getOsc(osc_max_idx);
  	      wave4_stat = WAVE_STAT_OK;

         c = wave0_idx;
         idx = wave0_idx;
  	      for(min = MAX_VALUE; c < osc_max_idx; ++c) 
         {
   	      if( getLo(c) < min ) 
            {
     		      min = getLo(c);
      	      idx = c;
            }
         }
  	      max = getHi(wave0_idx);
  	      rally = min + (max - min) * 15 / 100;
  	      wave4_osc_sig = osc_peak * 15 / 100;
  	      if((c = (short)(idx)) < MIN_WAVE_PERS)
   	      c = MIN_WAVE_PERS;
  	      wave4_sig = 0;
  	      for(; c < end_idx; ++c) 
         {
   	      if( c > osc_max_idx && getOsc(c) >= wave4_osc_sig )	/* new c > osc_max_idx	*/
      	      wave4_sig = 1;                 					/* set signal		*/
    	      if( wave4_sig != 0 && getHi(c) >= rally )
      	      break;													/* found a four		*/
    	      else if( getLo(c) < min ) {									/* new low  ?		*/
      	      min = getLo(c);												/* reset min		*/
      	      rally = min + (max - min) * 15 / 100;			/* new rally  		*/
            }
         }
  	      if( c < end_idx )
  		      return(c);
  	      osc_val = osc_peak * 80 / 100;
  	      osc_idx = -1;													/* -5 to ensure not same peak	*/
  	      for(c = wave0_idx; c < osc_max_idx - 5; ++c) 
         {
   	      if( getOsc(c) < osc_val ) {
     		      osc_val = getOsc(c);
      	      osc_idx = c;
            }
         }
  	      if( osc_idx != -1 ) 
         {
   	      osc_val = osc_val * 50 / 100L;
    	      for(c = osc_idx,osc_idx = -1; c < osc_max_idx; ++c) 
            {
      	      if( getOsc(c) > osc_val ) {
				      osc_val = getOsc(c);
				      osc_idx = c;
			      }
   	      }
   	      if( osc_idx != -1 )
   		      return osc_idx;
	      }
  	      wave4_stat = WAVE_STAT_4FAILED;
  	      if( c_level == 0 && !IsBegOfWaveAt(c_level, end_idx) )
   	      return (short)(end_idx - 1);
  	      factor = (short)((c_level == 0) ? 500 : 618);
 	      osc_idx = (short)(wave0_idx + (long)(end_idx - wave0_idx) * factor / 1000);
  	      idx = GetOscMaxIdxUp(osc, osc_idx, (short)(end_idx - 1));
  	      if( idx < osc_max_idx )
         idx = GetMaxHiTpLeft(osc_idx, (short)(end_idx - 1));	/* was from right	*/
         return (short)(idx);
      }

      private short GetWave24IdxUp(short wave1_idx, short wave3_idx)
      {
         short wave2_idx, idx, tp_time;
         double wave1_hi;

         wave1_hi = getHi(wave1_idx);
         wave2_idx = GetMinIdxPrc((short)(wave1_idx + 1), wave3_idx);
         tp_time = (short)(GetLoTpLen(wave2_idx) + 1);
         idx = wave2_idx;
         idx = GetMaxLoTpRight(idx, wave3_idx);
         idx = GetMinIdxPrc(idx, wave3_idx);
         if (c_level >= 1 && GetLoTpLen(idx) <= tp_time / 2)
            return (wave2_idx);
         if (getLo(idx) < wave1_hi &&
            (wave1_hi - getLo(idx)) * 2 >= (wave1_hi - getLo(wave2_idx)))
            return (idx);
         return (wave2_idx);
      }

      private short GetWave24IdxDn(short wave1_idx, short wave3_idx)
      {
         short wave2_idx, idx, tp_time;
         double wave1_lo;

         wave1_lo = getLo(wave1_idx);
         wave2_idx = GetMaxIdxPrc((short)(wave1_idx + 1), wave3_idx);
         tp_time = (short)(GetHiTpLen(wave2_idx) + 1);
         idx = wave2_idx;
         idx = GetMaxHiTpRight(idx, wave3_idx);
         idx = GetMaxIdxPrc(idx, wave3_idx);
         if (c_level >= 1 && GetHiTpLen(idx) <= tp_time / 2)
            return (wave2_idx);
         if (getHi(idx) > wave1_lo &&
            (getHi(idx) - wave1_lo) * 2 >= (getHi(wave2_idx) - wave1_lo))
            return (idx);
         return (wave2_idx);
      }

      private void FitWave1_2IdxUp(short wave0_idx, short wave3_idx, ref short wave1_idx, ref short wave2_idx)
      {
	      short wave3_len = 0;
         short wave1_len,idx;
  	      short max_pct;

  	      for(max_pct = 42; max_pct > 5 ;max_pct -= 5) 
         {
   	      wave3_len = (short)(getHi(wave3_idx) - getLo(wave2_idx));
            wave1_len = (short)(getHi(wave1_idx) - getLo(wave0_idx));
    	      if (wave1_len <= wave3_len) break;
    	      wave1_idx = GetWave1IdxUp(wave0_idx, wave3_idx, max_pct);
    	      wave2_idx = GetWave24IdxUp(wave1_idx, wave3_idx);
         }
  	      idx = GetMaxIdxPrc(wave1_idx, (short)(wave2_idx - 1));
  	      if ((short)(getHi(idx) - getLo(wave0_idx)) <= wave3_len)
   	      wave1_idx = idx;
      }

      private void FitWave1_2IdxDn(short wave0_idx, short wave3_idx, ref short wave1_idx, ref short wave2_idx)
      {
	      short wave3_len = 0;
         short wave1_len,idx;
  	      short max_pct;

  	      for (max_pct = 42; max_pct > 5 ;max_pct -= 2) 
         {
   	      wave3_len = (short)(getHi(wave2_idx) - getLo(wave3_idx));
    	      wave1_len = (short)(getHi(wave0_idx) - getLo(wave1_idx));
    	      if( wave1_len <= wave3_len ) break;
    	      wave1_idx = GetWave1IdxDn(wave0_idx,wave3_idx,max_pct);
    	      wave2_idx = GetWave24IdxDn(wave1_idx,wave3_idx);
         }
  	      idx = GetMinIdxPrc(wave1_idx, (short)(wave2_idx - 1));
 	      if ((short)(getHi(wave0_idx) - getLo(idx)) <=  wave3_len )
   	      wave1_idx = idx;
      }

      private short GetWave5IdxUp(short wave0_idx, short wave1_idx, short wave3_idx, short wave4_idx, short end_idx, short EWPct4Overlap1)
      {
         double max, min;
         short wave5_idx, idx;
         bool found = false;

         max = getHi(wave3_idx);
         min = GetXPctRtcWaveUp(wave0_idx, wave1_idx, EWPct4Overlap1);
         for (wave5_idx = idx = wave4_idx; idx <= end_idx; ++idx)
         {
            if (getLo(idx) < min)	/* signal that 1-2-3 collapsed	*/
               return (-1);
            if (getHi(idx) >= max)
            {
               found = true;
               max = getHi(idx);
               wave5_idx = idx;
            }
         }
         return ((!found || wave5_idx > end_idx) ? end_idx : wave5_idx);
      }

      private short GetWave5IdxDn(short wave0_idx, short wave1_idx, short wave3_idx, short wave4_idx, short end_idx, short EWPct4Overlap1)
      {
         double max, min;
         short wave5_idx, idx;
         bool found = false;

         max = GetXPctRtcWaveDn(wave0_idx, wave1_idx, EWPct4Overlap1);
         min = getLo(wave3_idx);
         for (wave5_idx = idx = wave4_idx; idx <= end_idx; ++idx)
         {
            if (getHi(idx) > max)
               return (-1);  /* signal that 1-2-3 collapsed	*/
            if (getLo(idx) <= min)
            {
               found = true;
               min = getLo(idx);
               wave5_idx = idx;
            }
         }
         return ((!found || wave5_idx > end_idx) ? end_idx : wave5_idx);
      }

      private double	GetXPctRtcWaveUp(short wave0_idx, short wave1_idx, short pct)
      {
         return (getHi(wave1_idx) - ((long)pct * (getHi(wave1_idx) - getLo(wave0_idx))) / 100);
      }

      private double GetXPctRtcWaveDn(short wave0_idx, short wave1_idx, short pct)
      {
         return (getLo(wave1_idx) + ((long)pct * (getHi(wave0_idx) - getLo(wave1_idx))) / 100);
      }

      private short Adjust123ToABCUp(short wave0_idx, short wave1_idx, short wave3_idx)
      {
         short wave2_idx, idx;

         idx = (short)(wave0_idx + (short)((wave3_idx - wave0_idx) * 146L / 1000L));
         wave2_idx = GetMaxLoTpRight((short)(idx + 1), wave3_idx);
         wave1_idx = GetMaxIdxPrc(wave0_idx, (short)(wave2_idx - 1));
         if (wave1_idx <= wave2_idx && wave2_idx <= wave3_idx)
         {
            SetWave(wave0_idx, wave1_idx, 7, WAVE_STAT_OK);
            SetWave(wave1_idx, wave2_idx, 8, WAVE_STAT_OK);
            SetWave(wave2_idx, wave3_idx, 9, WAVE_STAT_OK);
            ++c_group;
            return 1;
         }
         return 0;
      }

      private short Adjust123ToABCDn(short wave0_idx, short wave1_idx, short wave3_idx)
      {
         short wave2_idx, idx;

         idx = (short)(wave0_idx + (short)((wave3_idx - wave0_idx) * 146L / 1000L));
         wave2_idx = GetMaxHiTpRight((short)(idx + 1), wave3_idx);
         wave1_idx = GetMinIdxPrc(wave0_idx, (short)(wave2_idx - 1));
         if (wave1_idx <= wave2_idx && wave2_idx <= wave3_idx)
         {
            SetWave(wave0_idx, wave1_idx, -7, WAVE_STAT_OK);
            SetWave(wave1_idx, wave2_idx, -8, WAVE_STAT_OK);
            SetWave(wave2_idx, wave3_idx, -9, WAVE_STAT_OK);
            ++c_group;
            return 1;
         }
         return 0;
      }

      private short RollWave13UpLeft(short wave0_idx, short wave1_idx, short wave2_idx)
      {
          double lo, max;
          short beg_idx, tp_time, idx;

          lo = getLo(wave2_idx);
          if (getHi(wave1_idx) > lo && wave1_idx - wave0_idx >= 5)
          {
              tp_time = GetHiTpLen(wave1_idx);
              max = lo + (getHi(wave1_idx) - lo) * 90L / 100L;
              for (beg_idx = idx = (short)(wave1_idx - 3); idx > wave0_idx + 1; --idx)
              {
                  if (getHi(idx) >= max)
                      beg_idx = idx;
              }
              idx = GetMaxHiTpRight(beg_idx, (short)(wave1_idx - 2));
              if (getHi(idx) >= max && GetHiTpLen(idx) >= tp_time / 2)
                  wave1_idx = idx;
          }
          return (wave1_idx);
      }

      private short RollWave13DnLeft(short wave0_idx, short wave1_idx, short wave2_idx)
      {
          double hi, min;
          short tp_time, beg_idx, idx;

          hi = getHi(wave2_idx);
          if (hi > getLo(wave1_idx) && wave1_idx - wave0_idx >= 5)
          {
              min = hi - (hi - getLo(wave1_idx)) * 90L / 100L;
              tp_time = GetLoTpLen(wave1_idx);
              for (beg_idx = idx = (short)(wave1_idx - 3); idx > wave0_idx + 1; --idx)
              {
                  if (getLo(idx) <= min)
                      beg_idx = idx;
              }
              idx = GetMaxLoTpRight(beg_idx, (short)(wave1_idx - 2));
              if (getLo(idx) <= min && GetLoTpLen(idx) >= tp_time / 2)
                  wave1_idx = idx;
          }
          return (wave1_idx);
      }

      private short RollWave24UpRight(short wave1_idx, short wave2_idx, short wave3_idx)
      {
          double lo, min;
          short end_idx, tp_time, idx;

          lo = getLo(wave2_idx);
          if (getHi(wave1_idx) > lo && wave3_idx - wave2_idx >= 5)
          {
              tp_time = GetLoTpLen(wave2_idx);
              min = lo + (getHi(wave1_idx) - lo) * 10L / 100L;
              for (end_idx = idx = (short)(wave2_idx + 3); idx < wave3_idx - 1; ++idx)
              {
                  if (getLo(idx) <= min)
                      end_idx = idx;
              }
              idx = GetMaxLoTpRight((short)(wave2_idx + 2), end_idx);
              if (getLo(idx) <= min && GetLoTpLen(idx) >= tp_time / 2)
                  wave2_idx = idx;
          }
          return (wave2_idx);
      }

      private short RollWave24DnRight(short wave1_idx, short wave2_idx, short wave3_idx)
      {
          double hi, max;
          short end_idx, tp_time, idx;

          hi = getHi(wave2_idx);
          if (getLo(wave1_idx) < hi && wave3_idx - wave2_idx >= 5)
          {
              tp_time = GetHiTpLen(wave2_idx);
              max = hi - (hi - getLo(wave1_idx)) * 10L / 100L;
              for (end_idx = idx = (short)(wave2_idx + 3); idx < wave3_idx - 1; ++idx)
              {
                  if (getHi(idx) >= max)
                      end_idx = idx;
              }
              idx = GetMaxHiTpRight((short)(wave2_idx + 2), end_idx);
              if (getHi(idx) >= max && GetHiTpLen(idx) >= tp_time / 2)
                  wave2_idx = idx;
          }
          return (wave2_idx);
      }
   }
}

