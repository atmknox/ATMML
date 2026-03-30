using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ATMML
{
    public partial class Matrix : UserControl
    {
        public Matrix()
        {
            InitializeComponent();
        }

        public void setScenario(string value)
        {
            Scenario.Content = value;
            var trend = value.Contains("Extreme");
            //True.Content = trend ? "Yes" : "Up";
            True1.Content = trend ? "Yes" : "Up";
            True2.Content = trend ? "Yes Predictive Rate" : "Up Predictive Rate";
            //False.Content = trend ? "No" : "Dn";
            False1.Content = trend ? "No" : "Dn";
            False2.Content = trend ? "No Predictive Rate" : "Dn Predictive Rate";
        }

        public void setTrueNegative(int value)
        {
            TrueNeg.Content = value.ToString();
        }

        public void setFalseNegative(int value)
        {
            FalseNeg.Content = value.ToString();
        }

        public void setFalsePositive(int value)
        {
            FalsePos.Content = value.ToString();
        }

        public void setTruePositive(int value)
        {
            TruePos.Content = value.ToString();
        }

        public void setTestRange(int value)
        {
            //TestRange.Content = value.ToString();
            //TestRange1.Content = value.ToString();
        }

        public void setTotalPositive(int value)
        {
            TotalPos.Content = value.ToString();
        }

        public void setActualPositive(int value)
        {
            //ActualPos.Content = value.ToString();
        }

        public void setTotalNegative(int value)
        {
            TotalNeg.Content = value.ToString();
        }

        public void setActualNegative(int value)
        {
            //ActualNeg.Content = value.ToString();
        }

        public void setAccuracy(double value)
        {
            Accuracy.Content = value.ToString("0.00 %");
        }

        public void setErrorRate(double value)
        {
            ErrorRate.Content = value.ToString("0.00 %");
        }

        public void setSensitivity(double value)
        {
            Sensitivity.Content = value.ToString("0.00 %");
        }

        public void setFalsePos(double value)
        {
            FalsePosPer.Content = value.ToString("0.00 %");
        }

        public void setSpecificity(double value)
        {
            Specificity.Content = value.ToString("0.00 %");
        }

        public void setPrevalence(double value)
        {
            Prevalence.Content = value.ToString("0.00 %");
        }

        public void setNullError(double value)
        {
            NullError.Content = value.ToString("0.00 %");
        }

        public void setPositivePredictiveRate(double value)
        {
            PosPredRate.Content = value.ToString("0.00 %");
        }

        public void setNegativePredictiveRate(double value)
        {
            NegPredRate.Content = value.ToString("0.00 %");
        }
    }
}
