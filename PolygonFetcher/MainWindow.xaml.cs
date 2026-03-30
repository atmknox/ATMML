using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PolygonFetcher
{
    public partial class MainWindow : Window
    {
        private CancellationTokenSource _cancellationTokenSource;
        private ObservableCollection<LogEntry> _logEntries = new ObservableCollection<LogEntry>();
        private int _totalBars = 0;
        private int _successCount = 0;
        private int _errorCount = 0;

        // All S&P 500 tickers that were in the index at any point from Jan 1, 2022 to Jan 28, 2026
        private readonly string[] SP500Tickers = new string[]
        {
            "A", "AAL", "AAP", "AAPL", "ABBV", "ABNB", "ABT", "ACGL", "ACN", "ADBE",
            "ADI", "ADM", "ADP", "ADSK", "AEE", "AEP", "AES", "AFL", "AIG", "AIZ",
            "AJG", "AKAM", "ALB", "ALGN", "ALK", "ALL", "ALLE", "AMAT", "AMCR", "AMD",
            "AME", "AMGN", "AMP", "AMT", "AMTM", "AMZN", "ANET", "ANSS", "AON", "AOS",
            "APA", "APD", "APH", "APO", "APP", "APTV", "ARE", "ATO", "ATVI", "AVB",
            "AVGO", "AVY", "AWK", "AXON", "AXP", "AZO", "BA", "BAC", "BALL", "BAX",
            "BBWI", "BBY", "BDX", "BEN", "BF.B", "BIIB", "BIO", "BK", "BKNG", "BKR",
            "BLDR", "BLK", "BMY", "BR", "BRK.B", "BRO", "BSX", "BWA", "BX", "C",
            "CAG", "CAH", "CARR", "CAT", "CB", "CBOE", "CBRE", "CCI", "CCL", "CDNS",
            "CDW", "CE", "CEG", "CERN", "CF", "CFG", "CHD", "CHTR", "CI", "CINF",
            "CL", "CLX", "CMA", "CMCSA", "CME", "CMG", "CMI", "CMS", "CNC", "CNP",
            "COF", "COIN", "COO", "COP", "COR", "COST", "CPB", "CPRT", "CPT", "CRM",
            "CRWD", "CSCO", "CSGP", "CSX", "CTAS", "CTLT", "CTRA", "CTSH", "CTXS", "CVS",
            "CVX", "CZR", "D", "DAL", "DASH", "DAY", "DD", "DE", "DECK", "DELL",
            "DFS", "DG", "DGX", "DHI", "DHR", "DINO", "DIS", "DISH", "DLR", "DLTR",
            "DOC", "DOV", "DOW", "DPZ", "DRI", "DTE", "DUK", "DVA", "DVN", "DXC",
            "DXCM", "EA", "EBAY", "ECL", "ED", "EFX", "EG", "EIX", "EL", "ELV",
            "EME", "EMN", "EMR", "ENPH", "EOG", "EPAM", "EQIX", "EQR", "EQT", "ERIE",
            "ES", "ESS", "ETN", "ETR", "ETSY", "EVRG", "EW", "EXC", "EXE", "EXPD",
            "EXPE", "EXR", "F", "FANG", "FAST", "FCX", "FDS", "FDX", "FE", "FFIV",
            "FI", "FICO", "FIS", "FITB", "FMC", "FOX", "FOXA", "FRC", "FRT", "FSLR",
            "FTNT", "FTV", "GD", "GDDY", "GE", "GEHC", "GEN", "GEV", "GILD", "GIS",
            "GL", "GLW", "GM", "GNRC", "GOOG", "GOOGL", "GPC", "GPN", "GRMN", "GS",
            "GWW", "HAL", "HAS", "HBAN", "HCA", "HD", "HES", "HIG", "HII", "HLT",
            "HOLX", "HON", "HOOD", "HPE", "HPQ", "HRL", "HSIC", "HST", "HSY", "HUBB",
            "HUM", "HWM", "IBKR", "IBM", "ICE", "IDXX", "IEX", "IFF", "ILMN", "INCY",
            "INTC", "INTU", "INVH", "IP", "IPG", "IQV", "IR", "IRM", "ISRG", "IT",
            "ITW", "IVZ", "J", "JBHT", "JBL", "JCI", "JKHY", "JNJ", "JNPR", "JPM",
            "K", "KDP", "KEY", "KEYS", "KHC", "KIM", "KKR", "KLAC", "KMB", "KMI",
            "KMX", "KO", "KR", "KVUE", "L", "LDOS", "LEG", "LEN", "LH", "LHX",
            "LII", "LIN", "LKQ", "LLY", "LMT", "LNC", "LNT", "LOW", "LRCX", "LULU",
            "LUMN", "LUV", "LVS", "LW", "LYB", "LYV", "MA", "MAA", "MAR", "MAS",
            "MCD", "MCHP", "MCK", "MCO", "MDLZ", "MDT", "MET", "META", "MGM", "MHK",
            "MKC", "MKTX", "MLM", "MMC", "MMM", "MNST", "MO", "MOH", "MOS", "MPC",
            "MPWR", "MRK", "MRNA", "MRO", "MS", "MSCI", "MSFT", "MSI", "MTB", "MTCH",
            "MTD", "MU", "NCLH", "NDAQ", "NDSN", "NEE", "NEM", "NFLX", "NI", "NKE",
            "NLSN", "NOC", "NOW", "NRG", "NSC", "NTAP", "NTRS", "NUE", "NVDA", "NVR",
            "NWL", "NWS", "NWSA", "NXPI", "O", "ODFL", "OGN", "OKE", "OMC", "ON",
            "ORCL", "ORLY", "OTIS", "OXY", "PANW", "PARA", "PAYC", "PAYX", "PBCT", "PCAR",
            "PCG", "PEG", "PENN", "PEP", "PFE", "PFG", "PG", "PGR", "PH", "PHM",
            "PKG", "PLD", "PLTR", "PM", "PNC", "PNR", "PNW", "PODD", "POOL", "PPG",
            "PPL", "PRU", "PSA", "PSX", "PTC", "PWR", "PYPL", "QCOM", "QRVO", "RCL",
            "REG", "REGN", "RF", "RHI", "RJF", "RL", "RMD", "ROK", "ROL", "ROP",
            "ROST", "RSG", "RTX", "RVTY", "SBAC", "SBNY", "SBUX", "SCHW", "SEDG", "SEE",
            "SHW", "SIVB", "SJM", "SLB", "SMCI", "SNA", "SNPS", "SO", "SOLV", "SPG",
            "SPGI", "SRE", "STE", "STLD", "STT", "STX", "STZ", "SW", "SWK", "SWKS",
            "SYF", "SYK", "SYY", "T", "TAP", "TDG", "TDY", "TECH", "TEL", "TER",
            "TFC", "TFX", "TGT", "TJX", "TKO", "TMO", "TMUS", "TPR", "TRGP", "TRMB",
            "TROW", "TRV", "TSCO", "TSLA", "TSN", "TT", "TTD", "TTWO", "TWTR", "TXN",
            "TXT", "TYL", "UA", "UAL", "UBER", "UDR", "UHS", "ULTA", "UNH", "UNP",
            "UPS", "URI", "USB", "V", "VICI", "VLO", "VLTO", "VMC", "VRSK", "VRSN",
            "VRTX", "VST", "VTR", "VTRS", "VZ", "WAB", "WAT", "WBA", "WBD", "WDAY",
            "WDC", "WEC", "WELL", "WFC", "WHR", "WM", "WMB", "WMT", "WRB", "WSM",
            "WST", "WTW", "WY", "WYNN", "XEL", "XOM", "XYL", "XYZ", "YUM", "ZBH",
            "ZBRA", "ZION", "ZTS"
        };

        public MainWindow()
        {
            InitializeComponent();
            LogListBox.ItemsSource = _logEntries;
            
            // Set default dates
            StartDatePicker.SelectedDate = new DateTime(2022, 1, 1);
            EndDatePicker.SelectedDate = DateTime.Today;
            
            Log("Application initialized. Ready to fetch data for 543 S&P 500 tickers.", LogLevel.Info);
        }

        private async void FetchButton_Click(object sender, RoutedEventArgs e)
        {
            if (StartDatePicker.SelectedDate == null || EndDatePicker.SelectedDate == null)
            {
                MessageBox.Show("Please select start and end dates.", "Validation Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _cancellationTokenSource = new CancellationTokenSource();
            FetchButton.IsEnabled = false;
            CancelButton.IsEnabled = true;
            _totalBars = 0;
            _successCount = 0;
            _errorCount = 0;

            var interval = ((ComboBoxItem)IntervalCombo.SelectedItem).Tag.ToString();
            var startDate = StartDatePicker.SelectedDate.Value;
            var endDate = EndDatePicker.SelectedDate.Value;

            _logEntries.Clear();
            Log($"Starting fetch for {SP500Tickers.Length} tickers", LogLevel.Info);
            Log($"Interval: {interval}, Date Range: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}", LogLevel.Info);
            Log(new string('-', 60), LogLevel.Info);

            var polygon = new PolygonData();
            int processed = 0;

            try
            {
				var tickers = SP500Tickers;

				foreach (var ticker in tickers)
                {
                    if (_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        Log("Fetch cancelled by user.", LogLevel.Warning);
                        break;
                    }

                    processed++;
                    UpdateStatus($"Fetching {ticker}... ({processed}/{SP500Tickers.Length})");
                    ProgressBar.Value = (processed * 100.0) / SP500Tickers.Length;

                    // Add a log entry for this ticker that we'll update
                    var logEntry = new LogEntry 
                    { 
                        Message = $"{ticker,-6} | Fetching...", 
                        Level = LogLevel.Info 
                    };
                    
                    Dispatcher.Invoke(() =>
                    {
                        _logEntries.Add(logEntry);
                        LogListBox.ScrollIntoView(logEntry);
                    });

                    // Subscribe to progress updates - use BeginInvoke to avoid blocking
                    Action<int> progressHandler = (barCount) =>
                    {
                        Dispatcher.BeginInvoke(() =>
                        {
                            logEntry.Message = $"{ticker,-6} | {barCount,6:N0} bars...";
                        });
                    };
                    polygon.OnBarsUpdated += progressHandler;

                    try
                    {
                        var bars = await polygon.FetchBars(ticker, interval, startDate, endDate);
                        int barCount = bars?.Count ?? 0;
                        _totalBars += barCount;

                        // Save to cache
                        if (bars != null && bars.Count > 0)
                        {
                            PolygonData.SaveToCache(ticker, interval, bars);
                        }

                        // Update the existing log entry with final count
                        if (barCount > 0)
                        {
                            _successCount++;
                            logEntry.Message = $"{ticker,-6} | {barCount,6:N0} bars";
                            logEntry.Level = LogLevel.Success;
                        }
                        else
                        {
                            _errorCount++;
                            logEntry.Message = $"{ticker,-6} | No data";
                            logEntry.Level = LogLevel.Warning;
                        }
                    }
                    catch (Exception ex)
                    {
                        _errorCount++;
                        logEntry.Message = $"{ticker,-6} | ERROR: {ex.Message}";
                        logEntry.Level = LogLevel.Error;
                    }

                    // Unsubscribe to avoid memory leaks
                    polygon.OnBarsUpdated -= progressHandler;

                    // Small delay to avoid rate limiting
                    await Task.Delay(250, _cancellationTokenSource.Token);
                }
            }
            catch (OperationCanceledException)
            {
                Log("Operation was cancelled.", LogLevel.Warning);
            }

            Log(new string('-', 60), LogLevel.Info);
            Log($"Fetch complete! Total: {_totalBars:N0} bars | Success: {_successCount} | Errors: {_errorCount}", LogLevel.Info);

            UpdateStatus($"Complete - {_totalBars:N0} total bars");
            FetchButton.IsEnabled = true;
            CancelButton.IsEnabled = false;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            CancelButton.IsEnabled = false;
        }

        private void Log(string message, LogLevel level)
        {
            Dispatcher.Invoke(() =>
            {
                _logEntries.Add(new LogEntry { Message = $"[{DateTime.Now:HH:mm:ss}] {message}", Level = level });
                LogListBox.ScrollIntoView(_logEntries[_logEntries.Count - 1]);
            });
        }

        private void UpdateStatus(string status)
        {
            Dispatcher.Invoke(() => StatusText.Text = status);
        }
    }

    public enum LogLevel
    {
        Info,
        Success,
        Warning,
        Error
    }

    public class LogEntry : INotifyPropertyChanged
    {
        private string _message;
        private LogLevel _level;

        public string Message
        {
            get => _message;
            set
            {
                _message = value;
                OnPropertyChanged(nameof(Message));
            }
        }

        public LogLevel Level
        {
            get => _level;
            set
            {
                _level = value;
                OnPropertyChanged(nameof(Level));
                OnPropertyChanged(nameof(TextColor));
            }
        }
        
        public Brush TextColor
        {
            get
            {
                return Level switch
                {
                    LogLevel.Success => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4E, 0xC9, 0x4E)),
                    LogLevel.Warning => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xC1, 0x07)),
                    LogLevel.Error => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF4, 0x43, 0x36)),
                    _ => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE0, 0xE0, 0xE0))
                };
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
