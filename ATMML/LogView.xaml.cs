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
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ATMML
{
    public partial class LogView : ContentControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        Log _log = null;
        ObservableCollection<string> _data = new ObservableCollection<string>();

        public LogView()
        {
            InitializeComponent();

            Logger.DataContext = this;
            Binding binding = new Binding("Data");
            binding.Mode = BindingMode.OneWay;
            Logger.SetBinding(ListView.ItemsSourceProperty, binding);
        }

        public void Show(string name)
        {
            if (_log != null)
            {
                _log.LogChanged -= new LogEventHandler(OnLogChanged);
            }

            Log log = Log.Get(name);
            _log = log;

            _data.Clear();
            _data = _log.GetData();

            _log.LogChanged += new LogEventHandler(OnLogChanged);

            NotifyPropertyChanged("Data");
        }

        void OnLogChanged(object sender, LogEventArgs e)
        {
            Action action = delegate
            {
                _data.Add(e.Text);
            };

            this.Dispatcher.Invoke(action);
        }

        public ObservableCollection<string> Data
        {
            get { return _data; }
        }

        protected void NotifyPropertyChanged(string name)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(name));
        }

    }

    public delegate void LogEventHandler(object sender, LogEventArgs e);

    public class LogEventArgs : EventArgs
    {
        private string _text;

        public LogEventArgs(string text)
        {
            _text = text;
        }

        public string Text
        {
            get { return _text; }
        }
    }

    public class Log
    {
        public event LogEventHandler LogChanged;

        static Dictionary<string, Log> _logs = new Dictionary<string, Log>();

        private Stopwatch _stopwatch = new Stopwatch();
        private List<string> _data = new List<string>();

        public static Log Get(string name)
        {
            Log log = null;
            lock (_logs)
            {
                if (!_logs.TryGetValue(name, out log))
                {
                    log = new Log();
                    _logs[name] = log;
                }
            }
            return log;
        }

        public ObservableCollection<string> GetData()
        {
            lock (_data)
            {
                ObservableCollection<string> output = new ObservableCollection<string>(_data);
                return output;
            }
        }

        public static void Add(string text, string name = "default", bool resetTimer = false)
        {
            Log log = null;
            lock (_logs)
            {

                if (!_logs.TryGetValue(name, out log))
                {
                    log = new Log();
                    _logs[name] = log;
                }
            }

            long milliseconds = 0;
            lock (log._stopwatch)
            {
                log._stopwatch.Stop();
                milliseconds = log._stopwatch.ElapsedMilliseconds;
            }

            string displayText = DateTime.Now.ToString("hh:mm ss");
            displayText += "  " + milliseconds.ToString("00000");
            displayText += "  " + text;

            lock (log._data)
            {
                log._data.Add(displayText);
            }

            if (log.LogChanged != null)
            {
                log.LogChanged(log, new LogEventArgs(displayText));
            }

            lock (log._stopwatch)
            {
                if (resetTimer)
                {
                    log._stopwatch.Reset();
                }

                log._stopwatch.Start();
            }
        }
    }
}
