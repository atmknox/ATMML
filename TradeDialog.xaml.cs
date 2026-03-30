using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    public partial class TradeDialog : Window
    {
        public double PortfolioBalance { get; private set; }
        public double PortfolioPercent { get; private set; }
        public bool UseBeta { get; private set; }
        public bool UsePricePercent { get; private set; }
        public double PricePercent { get; private set; }
        public bool UseFixedDollar { get; private set; }
        public double FixedDollarAmount { get; private set; }

        public TradeDialog(double portfolioBalance, double portfolioPercent, bool useBeta, bool usePricePercent, double pricePercent, bool useFixedDollar, double fixedDollarAmount)
        {
            InitializeComponent();

            PortfolioBalanceControl.Text = portfolioBalance.ToString();
            PortfolioPercentControl.Text = portfolioPercent.ToString();
            UseBetaControl.IsChecked = useBeta;
            if (usePricePercent)
            {
                radioButton1.IsChecked = true;
            }
            else
            {
                radioButton2.IsChecked = true;
            }
            PricePercentControl.Text = pricePercent.ToString();
            if (useFixedDollar)
            {
                radioButton3.IsChecked = true;
            }
            else
            {
                radioButton4.IsChecked = true;
            }
            FixedDollarControl.Text = fixedDollarAmount.ToString();
        }

        private void btnYes_Click(object sender, RoutedEventArgs e)
        {
            PortfolioBalance = double.Parse(PortfolioBalanceControl.Text);
            PortfolioPercent = double.Parse(PortfolioPercentControl.Text);
            UseBeta = (UseBetaControl.IsChecked == true);
            UsePricePercent = (radioButton1.IsChecked == true);
            PricePercent = double.Parse(PricePercentControl.Text);
            UseFixedDollar = (radioButton3.IsChecked == true);
            FixedDollarAmount = double.Parse(FixedDollarControl.Text);
            DialogResult = true;
            this.Close();
        }

        private void btnNo_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
