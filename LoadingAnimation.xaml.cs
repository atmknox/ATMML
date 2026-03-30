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

namespace LoadingControl.Control
{
    public partial class LoadingAnimation : UserControl
    {
        public LoadingAnimation(int width, int height)
        {
            InitializeComponent();

            BusyViewBox.Width = width;
            BusyViewBox.Height = height;

        }

        public void Hide()
        {
            this.Visibility = Visibility.Hidden;
        }
    }
}
