using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    /// <summary>
    /// Interaction logic for PortfolioGroup.xaml
    /// </summary>
    public partial class PortfolioGroup : UserControl
    {
        public PortfolioGroup()
        {
            InitializeComponent();
        }
		private void Portfolio_MouseEnter(object sender, MouseEventArgs e)
		{
			Control label = sender as Control;
			label.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
		}
		private void Portfolio_MouseLeave(object sender, MouseEventArgs e)
		{
			Control label = sender as Control;
			label.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
		}
		private void Portfolio_MouseLeave2(object sender, MouseEventArgs e)
		{
			Control label = sender as Control;
			label.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
		}
		private void UseATM_Checked(object sender, RoutedEventArgs e)
		{
		}

		private void UseATM_Unchecked(object sender, RoutedEventArgs e)
		{
		}
	}
}
