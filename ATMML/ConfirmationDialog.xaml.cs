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
    /// <summary>
    /// Interaction logic for ConfirmationDialog.xaml
    /// </summary>
    public partial class ConfirmationDialog : UserControl
    {
        private MainView _mainView = null;
        private string _message = "";

        public event DialogEventHandler DialogEvent;
    
        public ConfirmationDialog(MainView mainView, string message)
        {
            _mainView = mainView;
            _message = message;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (DialogEvent != null)
            {
                DialogEvent(this, new DialogEventArgs(DialogEventArgs.EventType.Ok));
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (DialogEvent != null)
            {
                DialogEvent(this, new DialogEventArgs(DialogEventArgs.EventType.Cancel));
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            Message1.Text = _message;
        }
    }
}
