using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Linq;

namespace ATMML
{
    public partial class ScanDialog : Window
    {
        public event DialogEventHandler DialogEvent;

        private MainView _mainView = null;

        private ConditionAlert _conditionAlert = null;

        private string _title = "";
        private string _portfolio = "";
        private string _condition = "";

        private Color _textColor1 = Color.FromRgb(255, 255, 255);
        private Color _textColor2 = Color.FromRgb(255, 255, 255);
        private Color _textColor3 = Color.FromRgb(255, 255, 255);

        private bool _notificationOn = true;
        private bool _useOnTop = true;
        private int _runInterval = 5;

        private bool _useDefaultTitle = true;

        public ScanDialog(MainView mainView)
        {
            _mainView = mainView;
            InitializeComponent();

            ResourceDictionary dictionary = new ResourceDictionary();
            dictionary.Source = new Uri("pack://application:,,,/ATMML;component/StyleDictionary.xaml");
            this.Resources.MergedDictionaries.Add(dictionary);

            //TextColorPicker1.SelectedColor = _textColor1;
            //TextColorPicker2.SelectedColor = _textColor2;
            //TextColorPicker3.SelectedColor = _textColor3;
            NotificationCheckbox.IsChecked = _notificationOn;
            OnTopCheckbox.IsChecked = _useOnTop;
            RunInterval.Text = _runInterval.ToString();
            SpreadsheetRadio.IsChecked = true;

            DefaultTitleCheckbox.IsChecked = _useDefaultTitle;

            initPortfolioTree();
            initConditionTree();
        }

        public ConditionAlert ConditionAlert
        {
            get { return _conditionAlert; }
            set { _conditionAlert = value; }
        }

        public Color TextColor1
        {
            get { return _textColor1; }
            set { _textColor1 = value; /*TextColorPicker1.SelectedColor = value;*/ }
        }

        public Color TextColor2
        {
            get { return _textColor2; }
            set { _textColor2 = value; /*TextColorPicker2.SelectedColor = value;*/ }
        }

        public Color TextColor3
        {
            get { return _textColor3; }
            set { _textColor3 = value; /*TextColorPicker3.SelectedColor = value;*/ }
        }

        public Alert.AlertViewType ViewType
        {
            get
            {
                Alert.AlertViewType orientation = Alert.AlertViewType.Spreadsheet;
                if (SpreadsheetRadio.IsChecked == true)
                {
                    orientation = Alert.AlertViewType.Spreadsheet;
                }
                else if (TreeRadio.IsChecked == true)
                {
                    orientation = Alert.AlertViewType.DateTree;
                }
                return orientation;
            }

            set
            {
                if (value == Alert.AlertViewType.Spreadsheet)
                {
                    SpreadsheetRadio.IsChecked = true;
                    TreeRadio.IsChecked = false;
                }
                else if (value == Alert.AlertViewType.DateTree)
                {
                    SpreadsheetRadio.IsChecked = false;
                    TreeRadio.IsChecked = true;
                }
            }
        }

        public bool NotificationOn
        {
            get { return _notificationOn; }
            set { _notificationOn = value; NotificationCheckbox.IsChecked = value; }
        }

        public bool UseOnTop
        {
            get { return _useOnTop; }
            set { _useOnTop = value; OnTopCheckbox.IsChecked = value; }
        }

        public int RunIntervalMinutes
        {
            get { return _runInterval; }
            set { _runInterval = value; RunInterval.Text = value.ToString(); }
        }

        public string Title
        {
            get { return _title; }
            set { _title = value; ScanTitle.Text = _title; }
        }

        public string PortfolioText
        {
            get { return _portfolio; }
            set { setPortfolio(value); }
        }

        public string Condition
        {
            get { return _condition; }
            set { setCondition(value); }
        }

        public bool UseDefaultTitle
        {
            get { return _useDefaultTitle; }
            set { _useDefaultTitle = value; DefaultTitleCheckbox.IsChecked = value; }
        }

        private List<string> getPortfolios(string groupName)
        {
            List<string> names = new List<string>();
            try
            {
                string text = MainView.GetSetting(groupName);
                if (text != null && text.Length > 0)
                {
                    string[] portfolios = text.Split('\u0001');
                    foreach (string portfolio in portfolios)
                    {
                        names.Add(portfolio);
                    }
                }
            }
            catch (Exception)
            {
            }
            return names;
        }

        private void savePortfolios(string groupName)
        {
            PortfolioTreeViewItem item = getPortfolioItem(PortfolioTree, groupName);
            if (item != null)
            {
                string text = "";
                int count = item.Items.Count;
                for (int ii = 0; ii < count; ii++)
                {
                    PortfolioTreeViewItem eqsItem = item.Items[ii] as PortfolioTreeViewItem;
                    if (eqsItem != null)
                    {
                        StackPanel panel = eqsItem.Header as StackPanel;
                        TextBox textBox = panel.Children[1] as TextBox;
                        string name = textBox.Text;
                        if (ii > 0)
                        {
                            text += "\u0001";
                        }
                        text += name;

                        if (panel.Children.Count > 2)
                        {
                            ComboBox comboBox = panel.Children[2] as ComboBox;
                            if (comboBox != null)
                            {
                                string portfolio = comboBox.SelectedItem as string;
                                text += "." + portfolio;
                            }
                        }
                    }
                }
                MainView.SaveSetting(groupName, text);
            }
        }

        private void addPortfolio(Portfolio.PortfolioType type, string name, ItemsControl parent)
        {
            PortfolioTreeViewItem item = new PortfolioTreeViewItem();
            item.Type = type;

            string[] fields = name.Split('.');
            string name1 = fields[0];
            string name2 = (fields.Length > 1) ? fields[1] : "";

            item.Symbol = name;

            StackPanel panel = new StackPanel();

            panel.Orientation = Orientation.Horizontal;
            panel.Margin = new Thickness(0);
            panel.Background = new SolidColorBrush(Colors.Black);
            CheckBox checkBox = new CheckBox();
            item.CheckBox = checkBox;
            checkBox.Click += new RoutedEventHandler(PortfolioCheckBox_Click);
            checkBox.Margin = new Thickness(0, 2, 2, 0);
            panel.Children.Add(checkBox);

            TextBox textBox = new TextBox();
            textBox.FontSize = 10;
            textBox.Width = 128;
            textBox.Padding = new Thickness(0);
            textBox.Background = new SolidColorBrush(Colors.White);
            textBox.Text = name1;
            textBox.TextChanged += new TextChangedEventHandler(PortfolioTextBox_TextChanged);
            panel.Children.Add(textBox);

            if (type == Portfolio.PortfolioType.Spread)
            {
                ComboBox comboBox = new ComboBox();
                comboBox.SelectionChanged += new SelectionChangedEventHandler(comboBox_SelectionChanged);
                comboBox.FontSize = 10;
                List<string> portfolios = Portfolio.GetBuiltInPortfolios(false);
                foreach (string portfolio in portfolios)
                {
                    if (portfolio != "FIN SPREADS")
                    {
                        comboBox.Items.Add(portfolio);
                    }
                }

                if (name2.Length > 0)
                {
                    comboBox.SelectedItem = name2;
                }
                else
                {
                    comboBox.SelectedIndex = 0;
                }

                panel.Children.Add(comboBox);
            }

            item.Header = panel;

            parent.Items.Add(item);
        }

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			// DESIGN SIZE (Viewbox content)
			const double baseWidth = 725.0;
			const double baseHeight = 548.0;
			const double aspect = baseWidth / baseHeight;

			// Working area (screen minus taskbar)
			var wa = SystemParameters.WorkArea;

			// Fraction of screen height to use (keep height under control)
			const double heightFraction = 0.65;

			// Compute window HEIGHT using normal aspect scaling:
			double targetH = wa.Height * heightFraction;

			// Compute width needed to preserve layout height scaling:
			double scaledW = targetH * aspect;

			// ★ Add extra width for more UI space ★
			double extraWidth = scaledW * 0.35;  // <-- increase this multiplier for more width

			double targetW = scaledW + extraWidth;

			// Apply
			this.Width = targetW;
			this.Height = targetH;

			// Center window
			this.Left = wa.Left + (wa.Width - this.Width) / 2;
			this.Top = wa.Top + (wa.Height - this.Height) / 2;

			// Optional min sizes
			this.MinHeight = targetH * 0.80;
		}
		void comboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox comboBox = sender as ComboBox;
            if (comboBox != null)
            {
                StackPanel panel = comboBox.Parent as StackPanel;
                if (panel != null)
                {
                    string name1 = "";
                    TextBox textBox = panel.Children[1] as TextBox;
                    if (textBox != null)
                    {
                        name1 = textBox.Text;
                    }

                    string name2 = comboBox.SelectedItem as string;

                    PortfolioTreeViewItem item = panel.Parent as PortfolioTreeViewItem;
                    if (item != null)
                    {
                        item.Symbol = name1;
                        if (name2.Length > 0) item.Symbol += "." + name2;

                        updateTitle();
                    }
                }
            }
        }


		private void CloseDialog_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if (e.LeftButton == MouseButtonState.Pressed)
				this.Close();
		}

		void PortfolioCheckBox_Click(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = sender as CheckBox;
            if (checkBox != null)
            {
                StackPanel panel = checkBox.Parent as StackPanel;
                if (panel != null)
                {
                    string name1 = "";
                    TextBox textBox = panel.Children[1] as TextBox;
                    if (textBox != null)
                    {
                        name1 = textBox.Text;
                    }

                    string name2 = "";
                    ComboBox comboBox = (panel.Children.Count > 2) ? panel.Children[2] as ComboBox : null;
                    if (comboBox != null)
                    {
                        name2 = comboBox.SelectedItem as string;
                    }

                    PortfolioTreeViewItem item = panel.Parent as PortfolioTreeViewItem;
                    if (item != null)
                    {
                        item.Symbol = name1;
                        if (name2.Length > 0) item.Symbol += "." + name2;

                        updateTitle();
                    }
                }
            }
        }

        void PortfolioTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            if (textBox != null)
            {
                string name1 = textBox.Text;

                StackPanel panel = textBox.Parent as StackPanel;
                if (panel != null)
                {
                    string name2 = "";
                    ComboBox comboBox = (panel.Children.Count > 2) ? panel.Children[2] as ComboBox : null;
                    if (comboBox != null)
                    {
                        name2 = comboBox.SelectedItem as string;
                    }

                    PortfolioTreeViewItem item = panel.Parent as PortfolioTreeViewItem;
                    if (item != null)
                    {
                        item.Symbol = name1;
                        if (name2.Length > 0) item.Symbol += "." + name2;

                        updateTitle();
                    }
                }
            }
        }

        private void initPortfolioTree()
        {
            PortfolioTree.Background = Brushes.Black;
            PortfolioTree.Foreground = Brushes.White;
            PortfolioTree.FontSize = 10;

            PortfolioTreeViewItem item = new PortfolioTreeViewItem();
            List<string> names = getModelNames();

            addPortfolioItems(PortfolioTree, "");
        }

        private void addParameters(IAddChild parent)
        {
            FundamentalTreeViewItem item = parent as FundamentalTreeViewItem;

            StackPanel panel = new StackPanel();
            panel.Margin = new Thickness(2);
            panel.Orientation = Orientation.Horizontal;

            Label label = item.Header as Label;
            label.Foreground = new SolidColorBrush(Colors.White);
            item.Header = null;

            CheckBox checkBox = new CheckBox();
            checkBox.Margin = new Thickness(0, 2, 2, 0);
            checkBox.Click += new RoutedEventHandler(condition_checkBox_Click);
            panel.Children.Add(checkBox);

            label.Width = 200;
            label.Foreground = new SolidColorBrush(Colors.White);
            panel.Children.Add(label);

            string name = label.Content as string;

            ComboBox relationship = new ComboBox();
            relationship.Width = 50;
            relationship.Height = 18;
            relationship.FontSize = 10;
            relationship.Foreground = new SolidColorBrush(Colors.White);
            relationship.Items.Add(">=");
            relationship.Items.Add("<=");
            relationship.Items.Add("=");
            relationship.SelectedIndex = 0;
            panel.Children.Add(relationship);

            TextBox variable = new TextBox();
            variable.Width = 80;
            variable.Height = 18;
            variable.Foreground = new SolidColorBrush(Colors.White);
            variable.FontSize = 10;
            variable.Text = getDefaultValue(name);
            variable.TextAlignment = TextAlignment.Right;
            panel.Children.Add(variable);

            item.CheckBox = checkBox;
            item.ComboBox = relationship;
            item.TextBox = variable;

            item.Header = panel;
        }

        string getDefaultValue(string name)
        {
            return "1";
        }

        private void addIntervalTree(IAddChild parent)
        {
            string[] intervals = getIntervalList();
            foreach (string interval in intervals)
            {
                CheckboxTreeViewItem item = new CheckboxTreeViewItem();
                item.HasInterval = true;

                StackPanel panel = new StackPanel();

                Label label = new Label();
                label.Padding = new Thickness(0);
                label.Foreground = new SolidColorBrush(Colors.White);
                label.FontSize = 10;
                label.Width = 60;
                label.Content = interval + ":";
                panel.Children.Add(label);

                for (int ii = 0; ii < 1; ii++)
                {
                    panel.Orientation = Orientation.Horizontal;
                    panel.Margin = new Thickness(0);

                    CheckBox checkBox = new CheckBox();
                    item.CheckBox.Add(checkBox);
                    checkBox.Margin = new Thickness(0, 2, 2, 0);
                    checkBox.Click += new RoutedEventHandler(condition_checkBox_Click);
                    panel.Children.Add(checkBox);
                }

                item.Header = panel;

                parent.AddChild(item);
            }
        }

        private void addCheckBoxPanel(CheckboxTreeViewItem item, string name, string tooltip)
        {
            StackPanel panel = new StackPanel();

            Label label = new Label();
            label.Content = name;
            label.Padding = new Thickness(0, 0, 20, 0);
            label.Foreground = new SolidColorBrush(Colors.White);
            label.FontSize = 10;
            if (tooltip.Length > 0) label.ToolTip = tooltip;
            panel.Children.Add(label);

            panel.Orientation = Orientation.Horizontal;
            panel.Margin = new Thickness(0);

            CheckBox checkBox = new CheckBox();
            item.CheckBox.Add(checkBox);
            checkBox.Margin = new Thickness(0, 2, 2, 0);
            checkBox.Click += new RoutedEventHandler(condition_checkBox_Click);
            panel.Children.Add(checkBox);

            item.Header = panel;
        }

        private TreeNode root = null;

        private void initConditionTree()
        {
            ConditionTree.Background = Brushes.Black;
            ConditionTree.Foreground = Brushes.White;
            ConditionTree.FontSize = 10;

            if (root == null)
            {
                root = new TreeNode("ATMDataServer");
            }
            ConditionTree.ItemsSource = root.Children;
        }

        //returns true if leaf added
        private bool addPortfolioItems(ItemsControl parent, string name)
        {
            bool leaf = true;

            var portfolioType = Portfolio.PortfolioType.Index;

            List<string> portfolios = new List<string>();
            if (name == "Portfolios")
            {
                if (MainView.EnableSQPTPortfolio)
                {
                    portfolios.Add("HF2");
                }
            }
            else if (name == "WORKSHEETS")
            {
                portfolios.AddRange(Portfolio.GetWorksheetNames());
                portfolioType = Portfolio.PortfolioType.Worksheet;
            }
            else
            {
                portfolios.AddRange(getPortfolioList(name));
            }

            foreach (string portfolio in portfolios)
            {
                leaf = false;

                string[] field = portfolio.Split('.');
                bool hasSymbol = (field.Length > 1);
                string text = hasSymbol ? field[1] : portfolio;
                string symbol = hasSymbol ? field[0] : portfolio;

                bool hasCheckbox = (text == "US 100" || text == "US STOCKS" || text == "FINANCIAL" || text == "ALPHA" || text == "ALPHA 2" || text == "ALPHA 3");

                PortfolioTreeViewItem item = new PortfolioTreeViewItem();

                bool noSubItems = true;
                string[] field1 = symbol.Split(':');

                if (field1.Length == 1)
                {
                    symbol = field1[0];
                    if (name != symbol && item.Type != Portfolio.PortfolioType.Worksheet)
                    {
                        noSubItems = addPortfolioItems(item, symbol);

                        //Debug.WriteLine("1 " + symbol + " " + noSubItems);
                    }
                }
                else if (field1.Length == 2)
                {
                    symbol = field1[0];
                    string link = field1[1];
                    noSubItems = addPortfolioItems(item, link);
                    //Debug.WriteLine("2 " + link + " " + noSubItems);
                }

                string[] field2 = text.Split(':');
                if (field2.Length == 2)
                {
                    text = field2[0];
                }

                item.Symbol = symbol;

                hasCheckbox |= (noSubItems || hasSymbol);

                StackPanel panel = new StackPanel();
                panel.Orientation = Orientation.Horizontal;
                panel.Margin = new Thickness(0);
                panel.Background = new SolidColorBrush(Colors.Black);
                if (hasCheckbox)
                {
                    CheckBox checkBox = new CheckBox();
                    item.CheckBox = checkBox;
                    checkBox.Margin = new Thickness(0, 2, 2, 0);
                    checkBox.Click += new RoutedEventHandler(portfolio_checkBox_Click);
                    panel.Children.Add(checkBox);
                }
                Label label = new Label();
                label.Padding = new Thickness(0);
                label.Foreground = new SolidColorBrush(Colors.White);
                label.Content = text;
                panel.Children.Add(label);
                item.Header = panel;

                parent.Items.Add(item);
            }

            return leaf;
        }

        void portfolio_checkBox_Click(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = sender as CheckBox;
            if (checkBox != null)
            {
                StackPanel panel = checkBox.Parent as StackPanel;
                if (panel != null)
                {
                    PortfolioTreeViewItem item = panel.Parent as PortfolioTreeViewItem;
                    if (item != null)
                    {
                        clearParentCheckBoxes(item);
                        clearChildCheckBoxes(item);

                        updateTitle();
                    }
                }
            }
        }

        void condition_checkBox_Click(object sender, RoutedEventArgs e)
        {
            updateTitle();
        }

        private void updateTitle()
        {
            {
                string portfolio = getPortfolio(PortfolioTree);
                string condition = getCondition(ConditionTree);
                string title = Scan.GetTitle(portfolio, condition);
                ScanTitle.Text = title;
            }
        }

        private void clearParentCheckBoxes(PortfolioTreeViewItem child)
        {
            PortfolioTreeViewItem parent = child.Parent as PortfolioTreeViewItem;
            if (parent != null && parent.CheckBox != null)
            {
                parent.CheckBox.IsChecked = false;
                clearParentCheckBoxes(parent);
            }
        }

        private void clearChildCheckBoxes(PortfolioTreeViewItem parent)
        {
            foreach (PortfolioTreeViewItem child in parent.Items)
            {
                if (child.CheckBox != null)
                {
                    child.CheckBox.IsChecked = false;
                }
                clearChildCheckBoxes(child);
            }
        }

        private List<string> getModelNames()
        {
            var output = MainView.getModelNames();
            output.Sort();
            return output;
        }

        private List<string> getFactorModelNames()
        {
            System.Diagnostics.Debug.WriteLine("[RBAC_BUILD_CHECK] ScanDialog.getFactorModelNames invoked");
            var output = MainView.getFactorModelNames();
            // RBAC: non-admin users see LIVE models only in the ML PORTFOLIOS scan picker.
            if (!ATMML.Auth.AuthContext.Current.IsAdmin)
                output = output.Where(n => ModelAccessGate.IsLive(n)).ToList();
            output.Sort();
            return output;
        }

        private string[] getPortfolioList(string name)
        {
            string[] list = { };

            if (name == "")
            {
                List<string> items = new List<string>();
                if (BarServer.ConnectedToBloomberg() || !BarServer.ConnectedToCQG()) items.AddRange(new string[] { "COMMODITIES", "N AMERICA EQ", "EUROPE EQ", "MEA EQ", "ASIA EQ", "OCEANIA EQ", "ETF", "CRYPTO", "FX", "GLOBAL FUTURES", "GLOBAL TREAS RATES", "ML PORTFOLIOS" });
                if (BarServer.ConnectedToCQG()) items.AddRange(new string[] { "CQG COMMODITIES", "CQG EQUITIES", "CQG ETF", "CQG FX & CRYPTO", "CQG INTEREST RATES", "CQG STOCK INDICES" });
                list = items.ToArray();
            }
            else if (name == "N AMERICA EQ")
            {
                list = new string[] { "USA", "CANADA" };
            }
            else if (name == "EUROPE EQ")
            {
                list = new string[] { "EUROPEAN INDICES", "UK", "FRANCE", "GERMANY", "ITALY",  "SPAIN", "SWITZERLAND", "NORWAY", "SWEDEN", "FINLAND", "PORTUGAL" };
            }
            else if (name == "MEA EQ")
            {
                list = new string[] { "SOUTHAFRICA", "KUWAIT", "SAUDIARABIA",  "OMAN", "QATAR", "UAE" };
            }
            else if (name == "ASIA EQ")
            {
                list = new string[] { "CHINA", "HONGKONG", "INDIA", "INDONESIA", "JAPAN", "MALAYSIA", "SINGAPORE", "SOUTHKOREA", "TAIWAN", "THAILAND",  };
            }
            else if (name == "OCEANIA EQ")
            {
                list = new string[] { "AUSTRALIA"};
            }
            else if (name == "GENERIC")
            {
                list = new string[]
                {
                    "GENERIC WORLD EQ FUTURES","GENERIC WORLD BOND FUTURES", "GENERIC FX", "GENERIC ENERGY OIL | GAS", "GENERIC METALS",  "GENERIC ALLOYS", "GENERIC FOREST", "GENERIC GRAIN", "GENERIC LIVESTOCK", "GENERIC SOFTS",
                };
            }

            else if (name == "ACTIVE")
            {
                list = new string[]
                {
                    "ACTIVE WORLD EQ FUTURES","ACTIVE WORLD BOND FUTURES", "ACTIVE FX", "ACTIVE ENERGY OIL | GAS", "ACTIVE METALS", "ACTIVE ALLOYS", "ACTIVE FOREST", "ACTIVE GRAIN", "ACTIVE LIVESTOCK", "ACTIVE SOFTS",
                };
            }

            else if (name == "AGRICULTURE")
            {
                list = new string[] {
                    "FERTILIZER",
                    "FIBERS",                   
                    "FOODSTUFF",                   
                    "FOREST PRODUCTS",
                    "GRAINS",
                    "MEATS",
                    "OLECHEMICALS",
                    "SOFTS"
                };
            }

            else if (name == "ENERGY")
            {
                list = new string[] {
                    "CRUDE",
                    "NATURAL GAS",
                    "PETROCHEMICAL",
                    "REFINED | HEAVY",
                    "REFINED | LIGHT",
                    "REFINED | MIDDLE"
                };
            }    
            
            else if (name == "ENVIRONMENT")
            {
                list = new string[] {
                    "EMISSIONS",
                };
            }
                        
            else if (name == "METALS")
            {
                list = new string[] {
                    "BASE",
                    "BRASS", 
                    "COKING COAL",
                    "FERRO ALLOYS",
                    "MINERALS",
                    "MINOR",
                    "PRECIOUS",
                    "RARE EARTHS",
                    "STEEL"
                };
            }

            else if (name == "WORLD BOND FUTURES")
            {
                list = new string[] {
                    "BOND FUTURES"
                };
            }
            else if (name == "WORLD EQUITY FUTURES")
            {
                list = new string[] {
                    "EQUITY FUTURES"
                };
            }
            //else if (name == "WORLD CURRENCY")
            //{
            //    list = new string[] {
            //        "USD BASE",
            //        "EUR BASE",
            //        "GBP BASE",
            //        "G 10"
            //    };
            //}

            else if (name == "GLOBAL GENERIC RATES")
            {
                list = new string[] {
                    "GLOBAL 30YR",
                    "GLOBAL 10YR",
                    "GLOBAL 7YR",
                    "GLOBAL 5YR",
                    "GLOBAL 2YR",
                    "GLOBAL 1YR",
                };
            }
            else if (name == "N AMERICA RATES")
            {
                list = new string[] {
                    "US RATES",
                    "CANADA RATES",
                    "MEXICO RATES"
                };
            }
            else if (name == "S AMERICA RATES")
            {
                list = new string[] {
                    "BRAZIL RATES",
                    "CHILE RATES",
                    "COLUMBIA RATES",
                    "PERU RATES"
                };
            }
            else if (name == "EUROPE RATES")
            {
                list = new string[] {
                    "EUROZONE RATES",
                    "AUSTRIA RATES",
                    "BELGIUM RATES",
                    "ENGLAND RATES",
                    "CZECH REP RATES",
                    "DENMARK RATES",
                    "ENGLAND RATES",
                    "FINLAND RATES",
                    "FRANCE RATES",
                    "GERMANY RATES",
                    "GREECE RATES",
                    "HUNGARY RATES",
                    "IRELAND RATES",
                    "ITALY RATES",
                    "NETHERLANDS RATES",
                    "NORWAY RATES",
                    "POLAND RATES",
                    "PORTUGAL RATES",
                    "ROMANIA RATES",
                    "RUSSIA RATES",
                    "SLOVAKIA RATES",
                    "SPAIN RATES",
                    "SWEDEN RATES",
                    "SWITZERLAND RATES",
                    "TURKEY RATES"
                };
            }
            else if (name == "ASIA RATES")
            {
                list = new string[] {
                    "CHINA RATES",
                    "HONG KONG RATES",
                    "INDIA RATES",
                    "INDONESIA RATES",
                    "JAPAN RATES",
                    "MALAYSIA RATES",
                    "PAKISTAN RATES",
                    "PHILIPPINES RATES",
                    "SINGAPORE RATES",
                    "SOUTH KOREA RATES",
                    "TAIWAN RATES",
                    "THAILAND RATES"
                };
            }
            else if (name == "MEA RATES")
            {
                list = new string[] {
                    "ISRAEL RATES",
                    "SOUTH AFRICA RATES"
                };
            }
            else if (name == "OCEANIA RATES")
            {
                list = new string[] {
                    "AUSTRALIA RATES",
                    "NEW ZEALAND RATES"
                };
            }
            else if (name == "WORLD")
            {
                list = new string[]
                {
                    "WORLD","AMERICAS", "EMEA", "ASIA PACIFIC"
                };
            }
            else if (name == "GLOBAL MACRO")
            {
                list = new string[]
                {
                    "FX", "RATES", "SINGLE CTY ETF", "WORLD EQUITY INDICES"
                };
            }           
            else if (name == "MEATS")
            {
                list = new string[]
                {
                    "CATTLE",
                    "PORK",
                    "POULTRY",
                    "SEAFOOD",
                    "SHEEP"
                };
            }           
            else if (name == "FERTILIZER")
            {
                list = new string[]
                {
                    "UREA",
                    "AMMOFOS",
                    "AMMONIA",
                    "AMMO NITRATE",
                    "AMMO SULFATE",
                    "AQUA AMMONIA",
                    "CALCIUM AMMO NITRATE",
                    "DIAMMOFOS",
                    "MONO AMMO PHOSPHATE",
                    "PHOSPHORIC ROCK",
                    "PHOSPORIC ACID",
                    "POTASH",
                    "POTASSIUM CHLORIDE",
                    "POTASSIUM NITRATE",
                    "POTASSIUM SULFATE",
                    "SODA ASH",
                    "SODIUM MITRATE",
                    "SOP MAGNESIA",
                    "SULFUR",
                    "SULFURIC ACID"
                };
            }            
            else if (name == "FIBERS")
            {
                list = new string[]
                {
                   "COTTON"
                };
            }            
            else if (name == "FOODSTUFF")
            {
                list = new string[]
                {
                    "DAIRY",
                    "EGGS",
                    "FOOD OIL"
                };
            } 
            else if (name == "FOREST PRODUCTS")
            {
                list = new string[]
                {
                    "LUMBER",
                    "PAPER"
                };
            }            
            else if (name == "GRAINS")
            {
                list = new string[]
                {
                    "CORN",
                    "SOYBEANS",
                    "WHEAT",
                    "BARLEY",
                    "OATS",
                    "RICE",
                    //"OILSEED"
                };
            }            
            else if (name == "OLECHEMICALS")
            {
                list = new string[]
                {
                    "FATTY ACID",
                    "GLYCERINE",
                    "TURPENTINE",
                    "VARNISH"
                };
            } 
            else if (name == "SOFTS")
            {
                list = new string[]
                {
                    "COFFEE",
                    "SUGAR",
                    "COCOA",
                    "OJ"
                };
            } 
            else if (name == "COKING COAL")
            {
                list = new string[]
                {
                    "COKE",
                };
            }            
            else if (name == "CRUDE")
            {
                list = new string[]
                {
                    "CRUDE OIL",
                };
            }            
            else if (name == "NATURAL GAS")
            {
                list = new string[]
                {
                    "LNG",
                };
            }            
            else if (name == "PETROCHEMICAL")
            {
                list = new string[]
                {
                    "2-ETHYLHEXANOL",
                    "ACETIC ACID",
                    "ACETONE",
                    "ACRYLIC ACID",
                    "ADIPIC ACID",
                    "ALPHA OLEFIN",
                    "AROMA LINEAR ALKYLBENZENE",
                    "AROMA MIXED XYLENES",
                    "AROMA TOLUENE",
                    "BASE OILS",
                    "CAPROLACTAM",
                    "CARBON BLACK",
                    "CYCLOHEXANONE",
                    "EDC",
                    "EPOXIDE RESINS",
                    "ETHYLENE GLYCOL",
                    "ETHYLENE OXIDE",
                    "HDPE",
                    "ISOPROPYL ALCOHOL",
                    "LDPE",
                    "MALEIC ANHYDRIDE",
                    "MDI",
                    "MEG",
                    "MONOMER CAN",
                    "MTBE",
                    "OCTANOL",
                    "OLEFINS BUTADIENE",
                    "OLEFINS ETHYLENE",
                    "OLEFINS PROPYLENE",
                    "PET",
                    "PHENOL",
                    "POLYAMIDE FIBER",
                    "POLYAMIDE RESIN",
                    "POLYESTER RESIN",
                    "POLYMER ABS",
                    "POLYMER EVA",
                    "POLYMER LLDPE",
                    "POLYMER POLYSTYRENE",
                    "PP",
                    "PROPYLENE GLYCOL",
                    "PROPYLENE OXIDE",
                    "PTA",
                    "PVC",
                    "STYRENE",
                    "TDI",
                    "VCM",
                    "VINYL ACETATE MONOMER"
                };
            }            
            else if (name == "REFINED | HEAVY")
            {
                list = new string[]
                {
                    "BITUMEN",
                    "FUEL OIL",
                    "MARINE DIESEL",
                    "MARINE GASOIL"
                };
            }            
            else if (name == "REFINED | MIDDLE")
            {
                list = new string[]
                {
                    "DIESEL",
                    "GASOIL",
                    "JET FUEL"
                };
            }            
            else if (name == "REFINED | LIGHT")
            {
                list = new string[]
                {
                    "CONDENSATE",
                    "GASOLINE GASOHOL",
                    "GASOLINE LEAD SUB",
                    "GASOLINE REFORMATE",
                    "NAPHTHA"
                };
            }             
            else if (name == "EMISSIONS")
            {
                list = new string[]
                {
                    "EMISSIONS",
                };
            }             
            else if (name == "BASE")
            {
                list = new string[]
                {
                    "ALUMINUM",
                    "COPPER",
                    "IRON",
                    "LEAD",
                    "NICKEL",
                    "TIN",
                    "URANIUM",
                    "ZINC"
                };
            }              
            else if (name == "BRASS")
            {
                list = new string[]
                {
                    "BRASS"
                };
            }              
            else if (name == "MINERALS")
            {
                list = new string[]
                {
                    "FLUORSPAR",
                    "GODANTI"
                };
            }           
            else if (name == "RARE EARTHS")
            {
                list = new string[]
                {
                    "DYSPROSIUM",
                    "ERBIUM",
                    "EUROPIUM",
                    "GADOLINIUM",
                    "HOLMIUM",
                    "LANTHANUM",
                    "LUTETIUM",
                    "MISCHMETAL",
                    "NEODYMIUM",
                    "PRASEODYMIUM",
                    "SAMARIUM",
                    "SCANDIUM",
                    "TERBIUM",
                    "YTTRIUM",
                    "YTTERBIUM"  
                };
            }            
            else if (name == "PRECIOUS")
            {
                list = new string[]
                {
                     "GOLD",
                     "SILVER",
                     "PALLADIUM",          
                     "PLATINUM",           
                     "IRIDIUM",
                     "RHODIUM",
                     "RUTHENIUM",
                     "RHENIUM"
                };
            }              
            else if (name == "FERRO ALLOYS")
            {
                list = new string[]
                {
                    "CALCIUM SILICON",
                    "FERRO BORON",
                    "FERRO CHROME",
                    "FERRO DYSPROSIUM",
                    "FERRO GADOLINIUM",
                    "FERRO HOLMIUM",
                    "FERRO MANGANESE",
                    "FERRO MOLYBDENUM",
                    "FERRO NICKEL",
                    "FERRO NIOBIUM",
                    "FERRO PHOSPHORUS",
                    "FERRO SILICON",
                    "FERRO TITANIUM",
                    "FERRO TUNGSTEN",
                    "FERRO VANADIUM",
                    "SILICO MANGANESE"
                };
            }            
            else if (name == "MINOR")
            {
                list = new string[]
                {
                    "ANTIMONY",
                    "ARSENIC",
                    "BERYLLIUM",
                    "BISMUTH",
                    "CADMIUM",
                    "CALCIUM",
                    "CARBON",
                    "CHROMIUM",
                    "COBALT",
                    "GALLIUM",
                    "GERMANIUM",
                    "INDIUM",
                    "LITHIUM",
                    "MAGNESIUM",
                    "MANGANESE",
                    "MERCURY",
                    "MOLYBDENUM",
                    "NIOBIUM",
                    "POTASSIUM",
                    "SELENIUM",
                    "SILICON",
                    "SODIUM",
                    "STRONTIUM",
                    "TANTALUM",
                    "TELLURIUM",
                    "TITANIUM",
                    "TUNGSTEN",
                    "VANADIUM",
                    "ZIRCONIUM"
                };
            }   
            else if (name == "STEEL")
            {
                list = new string[]
                {
                    "ANGLE",
                    "BAR",
                    "BILLET",
                    "CARBON STRUT",
                    "CAST IRON",
                    "CHANNEL",
                    "COKE",
                    "CR COIL",
                    "CR SHEET",           
                    "GALVANISED",
                    "HR COIL",
                    "HR SHEET",
                    "HR STRIP",
                    "H BEAM",                          
                    "LINE PIPE",                    
                    "LONG PRODUCT",
                    "PIPE",
                    "PLATE",
                    "REBAR",
                    "SCRAP",
                    "STAINLESS BAR",
                    "STAINLESS CR",
                    "STAINLESS HR",
                    "STAINLESS SCRAP",
                    "STAINLESS SEAMLESS",
                    "STAINLESS SHEETS",                          
                    "STEEL PILING",                      
                    "WIRE DRAWN",                      
                    "WIRE ROD",
                };
            }   
            else if (name == "US RATES")
            {
                list = new string[]
                {
                    "US GENERIC","US CURVES", "US BUTTERFLIES", "US BREAKEVEN", "US CDS", "US SPREADS", "US SWAPS"
                };
            }
            else if (name == "CANADA RATES")
            {
                list = new string[]
                {
                    "CANADA GENERIC", "CANADA CURVES", "CANADA BUTTERFLIES", "CANADA BREAKEVEN", "CANADA CDS", "CANADA SPREADS", "CANADA SWAPS"
                };
            }
            else if (name == "MEXICO RATES")
            {
                list = new string[]
                {
                    "MEXICO GENERIC", "MEXICO BREAKEVEN", "MEXICO CDS", "MEXICO TIIE SWAPS", "MEXICO TIIE LIBORSWAPS"
                };
            }
            else if (name == "BRAZIL RATES")
            {
                list = new string[]
                {
                    "BRAZI GENERIC",
                    "BRAZIL BREAKEVEN",
                    "BRAZIL CDS",
                    "BRAZIL BMF"
                };
            }
            else if (name == "CHILE RATES")
            {
                list = new string[]
                {
                    "CHILE GENERIC",
                    "CHILE CDS",
                    "CHILE SWAPS"
                };
            }
            else if (name == "COLUMBIA RATES")
            {
                list = new string[]
                {
                    "COLUMBIA GENERIC",
                    "COLUMBIA CDS",
                    "COLUMBIA SWAPS"
                };
            }
            else if (name == "PERU RATES")
            {
                list = new string[]
                {
                    "PERU GENERIC",
                    "PERU CDS"
                };
            }
            else if (name == "ISRAEL RATES")
            {
                list = new string[]
                {
                    "ISRAEL GENERIC",
                    "ISRAEL BREAKEVEN",
                    "ISRAEL SWAPS"
                };
            }
            else if (name == "SOUTH AFRICA RATES")
            {
                list = new string[]
                {
                    "SOUTH AFRICA GENERIC",
                    "SOUTH AFRICA BREAKEVEN",
                    "SOUTH AFRICA CDS",
                    "SOUTH AFRICA SWAPS"
                };
            }
            else if (name == "EUROZONE RATES")
            {
                list = new string[]
                {
                    "EUROZONE GENERIC",
                    "EUROZONE CDS",
                    "EUROZONE SPREADS",
                    "EUROZONE ANNUAL SWAPS",
                    "EUROZONE SWAPS 3M EURIBOR",
                    "EUROZONE SWAPS EONIA"
                };
            }
            else if (name == "FRANCE RATES")
            {
                list = new string[]
                {
                    "FRANCE GENERIC",
                    "FRANCE CURVES",
                    "FRANCE BUTTERFLIES",
                    "FRANCE BREAKEVEN",
                    "FRANCE CDS"
                };
            }
            else if (name == "GERMANY RATES")
            {
                list = new string[]
                {
                    "GERMANY GENERIC",
                    "GERMANY CURVES",
                    "GERMANY BUTTERFLIES",
                    "GERMANY BREAKEVEN",
                    "GERMANY CDS"
                };
            }
            else if (name == "ITALY RATES")
            {
                list = new string[]
                {
                    "ITALY GENERIC",
                    "ITALY CURVES",
                    "ITALY BUTTERFLIES",
                    "ITALY BREAKEVEN"
                };
            }
            else if (name == "ENGLAND RATES")
            {
                list = new string[]
                {
                    "ENGLAND GENERIC",
                    "ENGLAND CURVES",
                    "ENGLAND BUTTERFLIES",
                    "ENGLAND BREAKEVEN",
                    "ENGLAND CDS",
                    "ENGLAND SPREADS",
                    "ENGLAND SWAPS",
                    "ENGLAND SONIA OIS"
                };
            }
            else if (name == "AUSTRIA RATES")
            {
                list = new string[]
                {
                    "AUSTRIA GENERIC",
                    "AUSTRIA CURVES",
                    "AUSTRIA BUTTERFLIES",
                    "AUSTRIA CDS"
                };
            }
            else if (name == "BELGIUM RATES")
            {
                list = new string[]
                {
                    "BELGIUM GENERIC",
                    "BELGIUM CURVES",
                    "BELGIUM BUTTERFLIES",
                    "BELGIUM CDS"
                };
            }
            else if (name == "CZECH REP RATES")
            {
                list = new string[]
                {
                    "CZECH REP GENERIC",
                    "CZECH REP CDS",
                    "CZECH REP SWAPS"
                };
            }
            else if (name == "DENMARK RATES")
            {
                list = new string[]
                {
                    "DENMARK GENERIC",
                    "DENMARK CURVES",
                    "DENMARK BUTTERFLIES",
                    "DENMARK BREAKEVEN",
                    "DENMARK CDS",
                    "DENMARK SWAPS"
                };
            }
            else if (name == "FINLAND RATES")
            {
                list = new string[]
                {
                    "FINLAND GENERIC",
                    "FINLAND CURVES",
                    "FINLAND BUTTERFLIES",
                    "FINLAND CDS"
                };
            }
            else if (name == "GREECE RATES")
            {
                list = new string[]
                {
                    "GREECE GENERIC",
                    "GREECE CURVES",
                    "GREECE BUTTERFLIES",
                    "GREECE CDS"
                };
            }
            else if (name == "HUNGARY RATES")
            {
                list = new string[]
                {
                    "HUNGARY GENERIC",
                    "HUNGARY CDS",
                    "HUNGARY SWAPS"
                };
            }
            else if (name == "IRELAND RATES")
            {
                list = new string[]
                {
                    "IRELAND GENERIC",
                    "IRELAND CURVES",
                    "IRELAND BUTTERFLIES"
                };
            }
            else if (name == "NETHERLANDS RATES")
            {
                list = new string[]
                {
                    "NETHERLANDS GENERIC",
                    "NETHERLANDS CURVES",
                    "NETHERLANDS BUTTERFLIES",
                    "NETHERLANDS CDS"
                };
            }
            else if (name == "NORWAY RATES")
            {
                list = new string[]
                {
                    "NORWAY GENERIC",
                    "NORWAY CURVES",
                    "NORWAY BUTTERFLIES",
                    "NORWAY CDS",
                    "NORWAY SWAPS"
                };
            }
            else if (name == "POLAND RATES")
            {
                list = new string[]
                {
                    "POLAND GENERIC",
                    "POLAND BREAKEVEN",
                    "POLAND CDS",
                    "POLAND SPREADS",
                    "POLAND SWAPS"
                };
            }
            else if (name == "PORTUGAL RATES")
            {
                list = new string[]
                {
                    "PORTUGAL GENERIC",
                    "PORTUGAL CURVES",
                    "PORTUGAL BUTTERFLIES"
                };
            }
            else if (name == "ROMANIA RATES")
            {
                list = new string[]
                {
                    "ROMANIA GENERIC",
                    "ROMANIA BREAKEVEN",
                    "ROMANIA CDS",
                    "ROMANIA SWAPS"
                };
            }
            else if (name == "RUSSIA RATES")
            {
                list = new string[]
                {
                    "RUSSIA GENERIC",
                    "RUSSIA BREAKEVEN",
                    "RUSSIA CDS",
                    "RUSSIA SWAPS"
                };
            }
            else if (name == "SLOVAKIA RATES")
            {
                list = new string[]
                {
                    "SLOVAKIA GENERIC",
                    "SLOVAKIA BREAKEVEN",
                    "SLOVAKIA CDS"
                };
            }
            else if (name == "SPAIN RATES")
            {
                list = new string[]
                {
                    "SPAIN GENERIC",
                    "SPAIN CURVES",
                    "SPAIN BUTTERFLIES",
                    "SPAIN BREAKEVEN",
                    "SPAIN CDS"
                };
            }
            else if (name == "SWEDEN RATES")
            {
                list = new string[]
                {
                    "SWEDEN GENERIC",
                    "SWEDEN CURVES",
                    "SWEDEN BUTTERFLIES",
                    "SWEDEN BREAKEVEN",
                    "SWEDEN CDS",
                    "SWEDEN SWAPS"
                };
            }
            else if (name == "SWITZERLAND RATES")
            {
                list = new string[]
                {
                    "SWITZERLAND GENERIC",
                    "SWITZERLAND CURVES",
                    "SWITZERLAND BUTTERFLIES",
                    "SWITZERLAND SWAPS",
                    "SWITZERLAND SPREADS"
                };
            }
            else if (name == "TURKEY RATES")
            {
                list = new string[]
                {
                    "TURKEY GENERIC",
                    "TURKEY BREAKEVEN",
                    "TURKEY CDS",
                    "TURKEY SWAPS"
                };
            }
            else if (name == "CHINA RATES")
            {
                list = new string[]
                {
                    "CHINA GENERIC",
                    "CHINA BREAKEVEN",
                    "CHINA CDS",
                    "CHINA ONSHORE 7D",
                    "CHINA ONSHORE 3M",
                    "CHINA USDCNY Non Del"
                };
            }
            else if (name == "HONG KONG RATES")
            {
                list = new string[]
                {
                    "HONG KONG GENERIC",
                    "HONG KONG CURVES",
                    "HONG KONG BUTTERFLIES",
                    "HONG KONG CDS",
                    "HONG KONG SPREADS",
                    "HONG KONG SWAPS"
                };
            }
            else if (name == "INDIA RATES")
            {
                list = new string[]
                {
                    "INDIA GENERIC",
                    "INDIA CDS",
                    "INDIA SWAPS"
                };
            }
            else if (name == "INDONESIA RATES")
            {
                list = new string[]
                {
                    "INDONESIA GENERIC",
                    "INDONESIA CDS",
                    "INDONESIA SWAPS 3M",
                    "INDONESIA OFFSHORE NDS",
                    "INDONESIA IONA"
                };
            }
            else if (name == "JAPAN RATES")
            {
                list = new string[]
                {
                    "JAPAN GENERIC",
                    "JAPAN CURVES",
                    "JAPAN BUTTERFLIES",
                    "JAPAN BREAKEVEN",
                    "JAPAN CDS",
                    "JAPAN SPREADS",
                    "JAPAN SWAPS",
                    "JAPAN SWAPS TOKYO CL"
                };
            }
            else if (name == "MALAYSIA RATES")
            {
                list = new string[]
                {
                    "MALAYSIA GENERIC",
                    "MALAYSIA CDS",
                    "MALAYSIA SWAPS"
                };
            }
            else if (name == "PAKISTAN RATES")
            {
                list = new string[]
                {
                    "PAKISTAN GENERIC",
                    "PAKISTAN CDS"
                };
            }
            else if (name == "PHILIPPINES RATES")
            {
                list = new string[]
                {
                    "PHILIPPINES GENERIC",
                    "PHILIPPINES CDS",
                    "PHILIPPINES SWAPS"
                };
            }
            else if (name == "SINGAPORE RATES")
            {
                list = new string[]
                {
                    "SINGAPORE GENERIC",
                    "SINGAPORE CURVES",
                    "SINGAPORE BUTTERFLIES",
                    "SINGAPORE SWAPS"
                };
            }
            else if (name == "SOUTH KOREA RATES")
            {
                list = new string[]
                {
                    "SOUTH KOREA GENERIC",
                    "SOUTH KOREA CDS",
                    "SOUTH KOREA OFFSHORE KRW USD",
                    "SOUTH KOREA ONSHORE KRW USD",
                    "SOUTH KOREA ONSHORE KRW KRW"
                };
            }
            else if (name == "TAIWAN RATES")
            {
                list = new string[]
                {
                    "TAIWAN GENERIC",
                    "TAIWAN ONSHORE TWD TWD",
                    "TAIWAN ONSHORE TWD USD"
                };
            }
            else if (name == "THAILAND RATES")
            {
                list = new string[]
                {
                    "THAILAND GENERIC",
                    "THAILAND CDS",
                    "THAILAND ONSHORE THB THB",
                    "THAILAND ONSHORE THB USD",
                    "THAILAND OFFSHORE THB USD"
                };
            }
            else if (name == "AUSTRALIA RATES")
            {
                list = new string[]
                {
                    "AUSTRALIA GENERIC",
                    "AUSTRALIA CURVES",
                    "AUSTRALIA BUTTERFLIES",
                    "AUSTRALIA BREAKEVEN",
                    "AUSTRALIA CDS",
                    "AUSTRALIA SPREADS",
                    "AUSTRALIA SWAPS"
                };
            }
            else if (name == "NEW ZEALAND RATES")
            {
                list = new string[]
                {
                    "NEW ZEALAND GENERIC",
                    "NEW ZEALAND CURVES",
                    "NEW ZEALAND BUTTERFLIES",
                    "NEW ZEALAND CDS",
                    "NEW ZEALAND SPREADS",
                    "NEW ZEALAND SWAPS"
                };
            }
            else if (name == "REVENUE")
            {
                list = new string[]
                {
                    "REV AAA",
                    "REV AA",
                    "REV A",
                };
            }
            else if (name == "GO")
            {
                list = new string[]
                {
                    "GO AAA",
                    "GO AA",
                    "GO A",
                    "GO BBB"
                };
            }
            else if (name == "GO TAXABLE")
            {
                list = new string[]
                {
                    "TAXABLE GO AAA",
                };
            }
            else if (name == "REVENUE TAXABLE")
            {
                list = new string[]
                {
                    "TAXABLE REVENUE AAA",
                };
            }
            else if (name == "MUNI SPREADS")
            {
                list = new string[]
                {
                    "MUNI TREAS SPREADS"
                };
            }
            else if (name == "COMMUNICATION SECTOR | RATING")
            {
                list = new string[]
                {
                    "COMM A",
                    "COMM BBB"
                };
            }
            else if (name == "CONS DISC SECTOR | RATING")
            {
                list = new string[]
                {
                    "DISC AA",
                    "DISC A",
                    "DISC BBB"
                };
            }
            else if (name == "CONS STAPLES SECTOR | RATING")
            {
                list = new string[]
                {
                    "STAPLES AAA",
                    "STAPLES AA",
                    "STAPLES A"
                };
            }
            else if (name == "ENERGY SECTOR | RATING")
            {
                list = new string[]
                {
                    "ENERGY AAA",
                    "ENERGY AA",
                    "ENERGY A",
                    "ENERGY BBB",
                };
            }
            else if (name == "FINANCIALS SECTOR | RATING")
            {
                list = new string[]
                {
                    "FINANCIALS AAA",
                    "FINANCIALS AA",
                    "FINANCIALS A",
                    "FINANCIALS BBB",
                };
            }
            else if (name == "HEALTH CARE SECTOR | RATING")
            {
                list = new string[]
                {
                    "HEALTH CARE AAA",
                    "HEALTH CARE AA",
                    "HEALTH CARE A",
                    "HEALTH CARE BBB",
                };
            }
            else if (name == "INDUSTRIAL SECTOR | RATING")
            {
                list = new string[]
                {
                    "INDUSTRIAL AAA",
                    "INDUSTRIAL AA",
                    "INDUSTRIAL A",
                    "INDUSTRIAL BBB",
                };
            }
            else if (name == "MATERIALS SECTOR | RATING")
            {
                list = new string[]
                {
                    "MATERIALS A",
                    "MATERIALS BBB",
                };
            }
            else if (name == "TECHNOLOGY SECTOR | RATING")
            {
                list = new string[]
                {
                    "TECHNOLOGY AAA",
                    "TECHNOLOGY AA",
                    "TECHNOLOGY A",
                    "TECHNOLOGY BBB",
                };
            }
            else if (name == "UTIL SECTOR | RATING >")
            {
                list = new string[]
                {
                    "UTIL A",
                    "UTIL BBB",
                };
            }

            else if (name == "FORWARD CROSS")
            {
                list = new string[]
                {
                    "USDCAD Forward",
                    "USDMXN Forward",
                    "USDGBP Forward",
                    "USDEUR Forward",
                    "USDFRF Forward",
                    "USDITL Forward",
                    "USDNOK Forward",
                    "USDSEK Forward",
                    "USDCHF Forward",
                    "USDBGN Forward",
                    "USDHRK Forward",
                    "USDCZK Forward",
                    "USDDKK Forward",
                    "USDHUF Forward",
                    "USDPLN Forward",
                    "USDRON Forward",
                    "USDRUB Forward",
                    "USDILS Forward",
                    "USDKED Forward",
                    "USDZAR Forward",
                    "USDJPY Forward",
                    "USDCNY Forward",
                    "USDHKD Forward",
                    "USDMYR Forward",
                    "USDSGD Forward",
                    "USDTHB Forward",
                    "USDVND Forward",
                    "USDAUD Forward",
                    "USDNZD Forward"
                };
            }
            else if (name == "CRYPTO")
            {
                list = new string[]
                {
                    "BITCOIN",
                    "BITCOIN CASH",
                    "DASH",
                    "EOS TOKENS",
                    "ETHEREUM",
                    "ETHEREUM CLASSIC",
                    "LITECOIN",
                    "RIPPLE",
                    "ZCASH",
                };
            }
            else if (name == "HOMEBUILDERS")
            {
                list = new string[]
                {
                    "HOUSING COST ITEMS"
                };
            }
            else if (name == "USA")
            {
                list = new string[] {
                    "OEX",
                    "CMR",
                    //"SP",
                    "SPXL1",
                    "SPXL2",
                    "SPXL3",
					"SPX | INDUSTRIES:SPX Groups",
                    "MID | INDUSTRIES:MID Groups",
                    "SML | INDUSTRIES:SML Groups",
                    "RAY | INDUSTRIES:RAY Groups",
                    "NDX | INDUSTRIES:NDX Groups",                   
                    "US ETF",
                    "US SECTOR ETF",
                    "SPY ETF"
                };
            }
            else if (name == "MID Groups")
            {
                list = new string[] {
                    "MID.MID INDEX",
                    "MID SECTORS:MID SECTORS",
                    "MID INDUSTRIES:MID INDUSTRIES",
                    "MID SUB-INDUSTRIES:MID SUB-INDUSTRIES" };
            }
            else if (name == "MID SECTORS")
            {
                list = new string[] {
                    "S4COND.MID CONS DISC",
                    "S4CONS.MID CONS STAPLES",
                    "S4ENRS.MID ENERGY",
                    "S4FINL.MID FINANCIALS",
                    "S4HLTH.MID HEALTH CARE",
                    "S4INDU.MID INDUSTRIALS",
                    "S4INFTMID.INFO TECH",
                    "S4MATR.MID MATERIALS",
                    "S4TELS.MID TELECOM",
                    "S4UTIL.MID UTILITIES" };
            }
            else if (name == "MID INDUSTRIES")
            {
                list = new string[] {
                    "S4AUCO.MID AUTO & COMP",
                    "S4BANKX.MID BANKS",
                    "S4CODU.MID CAPITAL GOODS",
                    "S4COMS.MID COMM & PROF",
                    "S4CPGS.MID CON DUR & AP",
                    "S4DIVF.MID DIV FINANCE",
                    "S4ENRSX.MID ENERGY",
                    "S4FDBT.MID FOOD/STAPLES",
                    "S4FDSR.MID FOOD BEV TOB",
                    "S4HCES.MID HC EQUIP",
                    "S4HOTR.MID CONS SERV",
                    "S4HOUS.MID HOUSEHOLD PROD",
                    "S4INSU.MID INSURANCE",
                    "S4MATRX.MID MATERIALS",
                    "S4MEDA.MID MEDIA",
                    "S4PHRM.MID PHRM BIO & LIFE",
                    "S4REAL.MID REAL ESTATE",
                    "S4RETL.MID RETAILING",
                    "S4SSEQX.MID SEMI & EQP",
                    "S4SFTW.MID SOFTWARE & SVCS",
                    "S4TECH.MID TECH HW & EQP",
                    "S4TELSX.MID TELECOM SVCS",
                    "S4TRAN.MID TRANSPORT",
                    "S4UTILX.MID UTILTIES" };
            }
            else if (name == "MID SUB-INDUSTRIES")
            {
                list = new string[] {
                    "S4AEROX.MID AERO & DEF",
                    "S4AIRFX.MID AIR FRT & LOG",
                    "S4AIRLX.MID AIRLINES",
                    "S4AUTC.MID AUTO COMP",
                    "S4AUTO.MID AUTOMOBILES",
                    "S4BEVG.MID BEVERAGES",
                    "S4BIOTX.MID BIOTECH",
                    "S4BUILX.MID BLDG PRODS",
                    "S4CAPM.MID CAPITAL MKTS",
                    "S4CBNK.MID COMM BANKS",
                    "S4CFINX.MID CONS FINANCE",
                    "S4CHEM.MID CHEMICALS",
                    "S4CMPE.MID COMPUTERS & PER",
                    "S4COMM.MID COMMUNICATION EQP",
                    "S4COMSX.MID COMMERCIAL SRBS",
                    "S4CONP.MID CONTAINER & PKG",
                    "S4CSTEX.MID CONST & ENG",
                    "S4CSTMX.MID CONST MATERIAL",
                    "S4DCON.MID DIVERSIFIED SRVC",
                    "S4DISTX.MID DISBRIBUTORS",
                    "S4DIVT.MID DIV TEL SVC",
                    "S4DVFS.MID DIV FIN SVC",
                    "S4ELEIX.MID ELECTRONIC EQUP",
                    "S4ELEQ.MID ELECTRICAL EQUP",
                    "S4ELUTX.MID ELECTRIC UTL",
                    "S4ENRE.MID ENERGY EQUP & SV",
                    "S4FDPR.MID FOOD PROD IND",
                    "S4FDSRX.MID FOOD & STAPLES RET",
                    "S4GASUX.MID GAS UTL",
                    "S4HCEQ.MID HC EQUP & SUP",
                    "S4HCPS.MID HC PROVIDERS SVC",
                    "S4HCTEX.MID HC TECHNOLOGY",
                    "S4HODU.MID HOUSEHOLD DURABLES",
                    "S4HOPRX.MID HOUSELHOLD PROD",
                    "S4HOTRX.MID HOTELS REST & LEIS",
                    "S4INCR.MID INTERNET CATALOG",
                    "S4INDCX.MID INDUSTRIAL CONGL",
                    "S4INSSX.MID INTERNET SOFTWARE",
                    "S4INSUX.MID INSUURANCE IND",
                    "S4ITSV.MID IT SERV IND",
                    "S4LEIS.MID LEISURE EQUP",
                    "S4LSTSX.MID LIFE SCI IND",
                    "S4MACH.MID MACHINERY",
                    "S4MARIX.MID MARINE",
                    "S4MDREX.MID RE MGM",
                    "S4MEDAX.MID MEDIA",
                    "S4METL.MID METAL & MIN",
                    "S4MRET.MID MULTILINE RET",
                    "S4MUTIX.MID MULTI UTL",
                    "S4OFFEX.MID OFFICE ELECT",
                    "S4OILG.MID OIL GAS FUEL",
                    "S4PAFO.MID PAPER FORSET PROD",
                    "S4PHARX.MID PHARMA",
                    "S4PRSV.MID PROF SRVS",
                    "S4REITS.MID RE INV TRUSTS",
                    "S4ROAD.MID ROARD & RAIL",
                    "S4SOFT.MID SOFTWARE",
                    "S4SPRE.MID SPECIALTY RET",
                    "S4SSEQ.MID SEMICOND & EQUP",
                    "S4TEXA.MID TXTL & APPRL",
                    "S4THMFX.MID THRIFTS & MORT",
                    "S4TOBAX.MID TOBACCO",
                    "S4TRADX.MID TRADING CO & DIS",
                    "S4WATUX.MID WATER UTL",
                    "S4WIREX.MID WIRELESS TELECOM" };
            }

            else if (name == "NDX Groups")
            {
                list = new string[]
                {
                    "NDX.NDX INDEX",
                    "NDX SECTORS:NDX SECTORS"
                };
            }
            else if (name == "NDX SECTORS")
            {
                list = new string[] 
                {                   
                    "CBNK.NDX BANK",
                    "NBI.NDX BIOTECHNOLOGY",
                    "IXK.NDX COMPUTER",
                    "NDF.NDX FINANCIAL",
                    "CIND.NDX INDUSTRIAL",
                    "CINS.NDX INSURANCE",
                    "CUTL.NDX TELECOMMUNICATION",
                    "CTRN.NDX TRANSPORTATION"
                };
            }
            else if (name == "WORLD")
            {
                list = new string[] {
                    "WORLD | INDUSTRIES:WORLD Groups",
                 };
            }
            else if (name == "WORLD Groups")
            {
                list = new string[]
                {
                    "BWORLD.BWORLD INDEX",
                    "WORLD SECTORS:WORLD SECTORS",
                    "WORLD INDUSTRIES:WORLD INDUSTRIES"
                };
            }
            else if (name == "WORLD SECTORS")
            {
                list = new string[]
                {
                    "BWFINL.BBG WRLD FINANCIAL IX",
                    "BWCNCY.BBG WRLD CON NON CYC IX",
                    "BWINDU.BBG WRLD INDUSTRIAL IX",
                    "BWCCYS.BBG WRLD CON CYC IX",
                    "BWCOMM.BBG WRLD COMM INDEX",
                    "BWTECH.BBG WRLD TECH INDEX",
                    "BWENRS.BBG WRLD ENERGY INDEX",
                    "BWBMAT.BBG WRLD BASIC MA INDEX",
                    "BWUTIL.BBG WRLD UTILITIES IX"
                };
            }
            else if (name == "WORLD INDUSTRIES")
            {
                list = new string[]
                {
                    "BWBANK.BBG WORLD BANKS INDEX",
                    "BWOILP.BBG WORLD OIL&GAS PRO IX",
                    "BWPHRM.BBG WORLD PHARMACEUT IDX",
                    "BWITNT.BBG WORLD INTERNET IDX",
                    "BWRETL.BBG WORLD RETAIL INDEX",
                    "BWTELE.BBG WORLD TELECOM INDEX",
                    "BWINSU.BBG WORLD INSURANCE INDX",
                    "BWSFTW.BBG WORLD SOFTWARE INDEX",
                    "BWDFIN.BBG WORLD DIV FIN SER IX",
                    "BWCOMP.BBG WORLD COMPUTERS INDX",
                    "BWFOOD.BBG WORLD FOOD INDEX",
                    "BWSEMI.BBG WORLD SEMICONDUCT IX",
                    "BWCHEM.BBG WORLD CHEMICALS INDX",
                    "BWELEC.BBG WORLD ELECTRIC INDEX",
                    "BWREIT.BBG WORLD REIT INDEX",
                    "BWCMMS.BBG WORLD COMMER SER IDX",
                    "BWREAL.BBG WORLD REAL ESTATE IX",
                    "BWBEVG.BBG WORLD BEVERAGES INDX",
                    "BWHCPR.BBG WORLD HEALTH CARE PR",
                    "BWELCT.BBG WORLD ELECTRONICS IX",
                    "BWTRAN.BBG WORLD TRANSPORT INDX",
                    "BWAUTM.BBG WORLD A MANUFACTR IX",
                    "BWMING.BBG WORLD MINING INDEX",
                    "BWMEDA.BBG WORLD MEDIA INDEX",
                    "BWENGN.BBG WORLD ENGIN & CON IX",
                    "BWBIOT.BBG WORLD BIOTECHNOLO IX",
                    "BWAERO.BBG WORLD AEROSP/DEF IDX",
                    "BWMMAN.BBG WORLD MISC-MANU INDX",
                    "BWHCSV.BBG WORLD HEALTH C SV IX",
                    "BWBUIL.BBG WORLD BUILDING MA IX",
                    "BWAPPR.BBG WORLD APPAREL INDEX",
                    "BWCOSM.BBG WORLD COSM/PER CA IX",
                    "BWAGRI.BBG WORLD AGRICULTURE IX",
                    "BWMCHD.BBG WORLD MACH-DIVERS IX",
                    "BWAUTP.BBG WORLD A PARTS/EQ IDX",
                    "BWIRON.BBG WORLD IRON/STEEL IDX",
                    "BWELCM.BBG WORLD ELEC COM/EQ IX",
                    "BWDIST.BBG WORLD DIST/WHOLES IX",
                    "BWLODG.BBG WORLD LODGING INDEX",
                    "BWHFUR.BBG WORLD HOME FURNIS IX",
                    "BWINVS.BBG WORLD INVEST COMP IX",
                    "BWGAS.BBG WORLD GAS INDEX",
                    "BWMCHC.BBG WORLD MAC-CONS/MI IX",
                    "BWAIRL.BBG WORLD AIRLINES INDEX",
                    "BWENTE.BBG WORLD ENTERTAINMT IX",
                    "BWOILS.BBG WORLD OIL&GAS SER IX",
                    "BWPIPE.BBG WORLD PIPELINES INDX",
                    "BWLEIS.BBG WORLD LEISURE TI IDX",
                    "BWHOUS.BBG WORLD HOSHLD PR/W IX",
                    "BWMETL.BBG WORLD MET FAB/HDW IX",
                    "BWHBLD.BBG WORLD HOME BUILD IDX",
                    "BWFRST.BBG WORLD FOR PROD/PA IX",
                    "BWTOOL.BBG WORLD HAND/MACH T IX",
                    "BWENVR.BBG WORLD ENVIR CONTL IX",
                    "BWPACK.BBG WORLD PACKAGING INDX",
                    "BWCOAL.BBG WORLD COAL INDEX",
                    "BWADVT.BBG WORLD ADVERTISING IX",
                    "BWENRG.BBG WORLD ENERGY-ATL  IX",
                    "BWWATR.BBG WORLD WATER INDEX",
                    "BWTEXT.BBG WORLD TEXTILES INDEX",
                    "BWTOYS.BBG WORLD TOY/GAM/HOB IX",
                    "BWOFFE.BBG WORLD OFF/BUS EQU IX",
                    "BWFSRV.BBG WORLD FOOD SERVIC IX",
                    "BWSAVL.BBG WORLD SAV & LOANS IX",
                    "BWSHIP.BBG WORLD SHIPBUILDIN IX",
                    "BWHWAR.BBG WORLD HOUSEWARES IDX",
                    "BWSTOR.BBG WORLD STOR/WAREH IDX",
                    "BWOFUR.BBG WORLD OFFICE FURN IX",
                    "BWTRUC.BBG WORLD TRUCK&LEAS INX"
                };
            }
            else if (name == "AMERICAS INDUSTRIES")
            {
                list = new string[]
                {
                    "BUSBANK.BBG AMER BANKS INDEX",
                    "BUSSFTW.BBG AMER SOFTWARE INDEX",
                    "BUSRETL.BBG AMER RETAIL INDEX",
                    "BUSPHRM.BBG AMER PHARMACEUTICAL",
                    "BUSCOMP.BBG AMER COMPUTERS INDEX",
                    "BUSOILP.BBG AMER OIL & GAS PROD",
                    "BUSINSU.BBG AMER INSURANCE INDEX",
                    "BUSDFIN.BBG AMER DIV FIN SERV IX",
                    "BUSSEMI.BBG AMER SEMICONDUCTOR",
                    "BUSHCPR.BBG AMER HEALTH-PRODUCT",
                    "BUSTELE.BBG AMER TELECOMM INDEX",
                    "BUSCMMS.BBG AMER COMM SERVICE IX",
                    "BUSELEC.BBG AMER ELECTRIC INDEX",
                    "BUSBIOT.BBG AMER BIOTECHNOLOGY",
                    "BUSAERO.BBG AMER AERO/DEFENSE IX",
                    "BUSHCSV.BBG AMER HEALTH-SERVICE",
                    "BUSMEDA.BBG AMER MEDIA INDEX",
                    "BUSCHEM.BBG AMER CHEMICALS INDEX",
                    "BUSBEVG.BBG AMER BEVERAGES INDEX",
                    "BUSFOOD.BBG AMER FOOD INDEX",
                    "BUSTRAN.BBG AMER TRANSPORTATION",
                    "BUSMMAN.BBG AMER MISC-MANUFACT",
                    "BUSELCT.BBG AMER ELECTRONICS IDX",
                    "BUSCOSM.BBG AMER COSMET/PERS IDX",
                    "BUSAGRI.BBG AMER AGRICULTURE IDX",
                    "BUSMING.BBG AMER MINING INDEX",
                    "BUSMCHD.BBG AMER MACH-DIVERS IDX",
                    "BUSPIPE.BBG AMER PIPELINES INDEX",
                    "BUSAUTM.BBG AMER AUTO MANUFACT",
                    "BUSOILS.BBG AMER OIL & GAS SERV",
                    "BUSAPPR.BBG AMER APPAREL INDEX",
                    "BUSLODG.BBG AMER LODGING INDEX",
                    "BUSBUIL.BBG AMER BUILDING MAT IX",
                    "BUSAIRLX.BBG AMER AIRLINES INDEX",
                    "BUSAUTP.BBG AMER AUTO PART/EQP",
                    "BUSIRON.BBG AMER IRON/STEEL INDX",
                    "BUSINVS.BBG AMER INVESTMENT CO",
                    "BUSELCM.BBG AMER ELEC COMP/EQP",
                    "BUSENVR.BBG AMER ENVIRON CTRL IX",
                    "BUSLEIS.BBG AMER LEISURE TIME IX",
                    "BUSGAS.BBG AMER GAS INDEX",
                    "BUSPACK.BBG AMER PACK & CONTAIN",
                    "BUSMCHC.BBG AMER MACH-CONST/MIN",
                    "BUSHOUS.BBG AMER HOUSE PRD/WARE",
                    "BUSENTE.BBG AMER ENTERTAINMENT",
                    "BUSENGN.BBG AMER ENGIN & CONST",
                    "BUSDIST.BBG AMER DIST/WHOLE IDX",
                    "BUSHBLD.BBG AMER HOME BUILDERS",
                    "BUSREAL.BBG AMER REAL ESTATE IDX",
                    "BUSFRST.BBG AMER FOR PROD/PAPER",
                    "BUSSAVL.BBG AMER SAV & LOANS IDX",
                    "BUSTOOL.BBG AMER HAND/MACH TOOL",
                    "BUSWATR.BBG AMER WATER INDEX",
                    "BUSENRG.BBG AMER ENRG-ALT SRCE",
                    "BUSMETL.BBG AMER METAL FAB/HRD",
                    "BUSADVT.BBG AMER ADVERTISING IDX",
                    "BUSHWAR.BBG AMER HOUSEWARES IDX",
                    "BUSHFUR.BBG AMER HOME FURNISH IX",
                    "BUSTEXT.BBG AMER TEXTILES INDEX",
                    "BUSTOYS.BBG AMER TOY/GAME/HOB IX",
                    "BUSSTOR.BBG AMER STOR/WAREHOUS",
                    "BUSOFUR.BBG AMER OFFICE FURN IDX",
                    "BUSOFFE.BBG AMER OFFC/BUS EQUP",
                    "BUSCOAL.BBG AMER COAL INDEX",
                    "BUSTRUC.BBG AMER TRUCK & LEAS IX",
                    "BUSFSRV.BBG AMER FOOD SERVICE IX",
                    "BUSSHIP.BBG AMER SHIPBUILDING IX",
                    "BWOFUR.BBG WORLD OFFICE FURN IX",
                    "BWTRUC.BBG WORLD TRUCK&LEAS INX"
                };
            }
            else if (name == "EMEA INDUSTRIES")
            {
                list = new string[]
                {
                    "BEUBANK.BBG EMEA BANKS INDEX",
                    "BEUOILP.BBG EMEA OIL & GAS PRODC",
                    "BEUPHRM.BBG EMEA PHARMACEUTICALS",
                    "BEUFOOD.BBG EMEA FOOD INDEX",
                    "BEUTELE.BBG EMEA TELECOMM INDEX",
                    "BEUCHEM.BBG EMEA CHEMICALS INDEX",
                    "BEUINSU.BBG EMEA INSURANCE INDEX",
                    "BEUELEC.BBG EMEA ELECTRIC INDEX",
                    "BEUAPPR.BBG EMEA APPAREL INDEX",
                    "BEUBEVG.BBG EMEA BEVERAGES INDEX",
                    "BEUMING.BBG EMEA MINING INDEX",
                    "BEURETL.BBG EMEA RETAIL INDEX",
                    "BEUCMMS.BBG EMEA COMM SERVS INDX",
                    "BEUENGN.BBG EMEA ENGIN & CONSTRU",
                    "BEUSFTW.BBG EMEA SOFTWARE INDEX",
                    "BEUMEDA.BBG EMEA MEDIA INDEX",
                    "BEUREAL.BBG EMEA REAL ESTATE IDX",
                    "BEUCOSM.BBG EMEA COSM/PER CARE",
                    "BEUAERO.BBG EMEA AERO/DEFENSE IX",
                    "BEUBUIL.BBG EMEA BUILDING MAT IX",
                    "BEUAUTM.BBG EMEA AUTO MANUFAC",
                    "BEUINVS.BBG EMEA INVESTMENT CO",
                    "BEUMMAN.BBG EMEA MISCELL-MANU IX",
                    "BEUDFIN.BBG EMEA DIV FINL SERV",
                    "BEUHCPR.BBG EMEA HEALTH CARE-PRD",
                    "BEUREIT.BBG EMEA REIT INDEX",
                    "BEUAGRI.BBG EMEA AGRICULTURE IDX",
                    "BEUTRAN.BBG EMEA TRANSPORTATION",
                    "BEUSEMI.BBG EMEA SEMICONDUCTORS",
                    "BEUMCHD.BBG EMEA MACH- DIV INDEX",
                    "BEUHCSV.BBG EMEA HEALTH CARE-SRV",
                    "BEUIRON.BBG EMEA IRON/STEEL INDX",
                    "BEUGAS.BBG EMEA GAS INDEX",
                    "BEUELCT.BBG EMEA ELECTRONICS IDX",
                    "BEUAUTP.BBG EMEA AUTO PARTS &EQP",
                    "BEUELCM.BBG EMEA ELEC COMP&EQUIP",
                    "BEUBIOT.BBG EMEA BIOTECHNOLOGY",
                    "BEUFRST.BBG EMEA FOREST PROD/PAP",
                    "BEUMCHC.BBG EMEA MACH- CONST/MIN",
                    "BEUCOMP.BBG EMEA COMPUTER INDEX",
                    "BEUAIRL.BBG EMEA AIRLINES INDEX",
                    "BEUHOUS.BBG EMEA HOUSEHOLD PRODT",
                    "BEUDIST.BBG EMEA DIST/WHLSALE IX",
                    "BEUTOOL.BBG EMEA HAND/MACH TOOLS",
                    "BEUMETL.BBG EMEA METAL FAB/HDWR",
                    "BEUADVT.BBG EMEA ADVERTISING IDX",
                    "BEUFSRV.BBG EMEA FOOD SERVICE IX",
                    "BEULEIS.BBG EMEA LEISURE TIME IX",
                    "BEUHBLD.BBG EMEA HOME BUILDERS",
                    "BEULODG.BBG EMEA LODGING INDEX",
                    "BEUENTE.BBG EMEA ENTERTAINMENT",
                    "BEUWATR.BBG EMEA WATER INDEX",
                    "BEUENRG.BBG EMEA ENERGY-ALT SRC",
                    "BEUOILS.BBG EMEA OIL & GAS SERVS",
                    "BEUHFUR.BBG EMEA HOME FURNISHING",
                    "BEUPACK.BBG EMEA PACK & CONTAINR",
                    "BEUENVR.BBG EMEA ENVIRON CONTR",
                    "BEUITNT.BBG EMEA INTERNET INDEX",
                    "BEUSTOR.BBG EMEA STORAGE/WAREHOU",
                    "BEUTEXT.BBG EMEA TEXTILES INDEX",
                    "BEUCOAL.BBG EMEA COAL INDEX",
                    "BEUHWAR.BBG EMEA HOUSEWARES INDX",
                    "BEUOFFE.BBG EMEA OFFICE/BUS EQUP",
                    "BEUOFUR.BBG EMEA OFFICE FURNISH",
                    "BEUPIPE.BBG EMEA PIPELINES INDEX",
                    "BEUSAVL.BBG EMEA SAVINGS & LOANS",
                    "BEUSHIP.BBG EMEA SHIPBUILDING IX",
                    "BEUTOYS.BBG EMEA TOYS/GAMES/HOBB",
                    "BEUTRUC.BBG EMEA TRUCKING&LEASIN"
                };
            }
            else if (name == "ASIA PACIFIC INDUSTRIES")
            {
                list = new string[]
                {
                    "BPRBANK.BBG AP BANKS INDEX",
                    "BPRREAL.BBG AP REAL ESTATE INDEX",
                    "BPRTELE.BBG AP TELECOMM INDEX",
                    "BPRINSU.BBG AP INSURANCE INDEX",
                    "BPRDFIN.BBG AP DIVERS FINCL SVCS",
                    "BPRPHRM.BBG AP PHARMACEUTICALS",
                    "BPROILP.BBG AP OIL & GAS PRODUCR",
                    "BPRSEMI.BBG AP SEMICONDUCTORS",
                    "BPRELCT.BBG AP ELECTRONICS INDEX",
                    "BPRAUTM.BBG AP AUTO MANUFACTURER",
                    "BPRFOOD.BBG AP FOOD INDEX",
                    "BPRCHEM.BBG AP CHEMICALS INDEX",
                    "BPRENGN.BBG AP ENGINEER & CONST",
                    "BPRRETL.BBG AP RETAIL INDEX",
                    "BPRTRAN.BBG AP TRANSPORTATION",
                    "BPRELEC.BBG AP ELECTRIC INDEX",
                    "BPRMING.BBG AP MINING INDEX",
                    "BPRAUTP.BBG AP AUTO PTS & EQUIP",
                    "BPRCOMP.BBG AP COMPUTERS INDEX",
                    "BPRBEVG.BBG AP BEVERAGES INDEX",
                    "BPRHFUR.BBG AP HOME FURNISHINGS",
                    "BPRCMMS.BBG AP COMMERCIAL SVCS",
                    "BPRBUIL.BBG AP BUILDING MATERIAL",
                    "BPRDIST.BBG AP DIST/WHOLESALE",
                    "BPRMCHD.BBG AP MACH-DIVERSIFIED",
                    "BPRMMAN.BBG AP MISC-MANUFACTURE",
                    "BPRAGRI.BBG AP AGRICULTURE INDEX",
                    "BPRIRON.BBG AP IRON/STEEL INDEX",
                    "BPRELCM.BBG AP ELE COMP & EQUIP",
                    "BPRENTE.BBG AP ENTERTAINMENT IDX",
                    "BPRCOAL.BBG AP COAL INDEX",
                    "BPRSFTW.BBG AP SOFTWARE INDEX",
                    "BPRLODG.BBG AP LODGING INDEX",
                    "BPRBIOT.BBG AP BIOTECH INDEX",
                    "BPRCOSM.BBG AP COSMETICS/PER CAR",
                    "BPRMCHC.BBG AP MACH-CNSTR & MINE",
                    "BPRGAS.BBG AP GAS INDEX",
                    "BPRHCPR.BBG AP HEALTH CARE-PRODS",
                    "BPRMETL.BBG AP METAL FABR/HARDWR",
                    "BPRLEIS.BBG AP LEISURE TIME INDX",
                    "BPRHOUS.BBG AP HSEHLD PROD/WARES",
                    "BPRAIRL.BBG AP AIRLINES INDEX",
                    "BPRHCSV.BBG AP HEALTH CARE-SVCS",
                    "BPRAPPR.BBG AP APPAREL INDEX",
                    "BPRTOOL.BBG AP HAND/MACHINE TOOL",
                    "BPRHBLD.BBG AP HOME BUILDERS IDX",
                    "BPROFFE.BBG AP OFFICE/BUS EQUIP",
                    "BPRTEXT.BBG AP TEXTILES INDEX",
                    "BPRENVR.BBG AP ENVIRONMTL CONTRL",
                    "BPRTOYS.BBG AP TOYS/GAMES/HOBBY",
                    "BPRPACK.BBG AP PACKAGING & CONT",
                    "BPRENRG.BBG AP ENERGY-ALT SOURCE",
                    "BPRADVT.BBG AP ADVERTISING INDEX",
                    "BPRFRST.BBG AP FOREST PRD & PAPR",
                    "BPRINVS.BBG AP INVESTMT COMPANY",
                    "BPRMEDA.BBG AP MEDIA INDEX",
                    "BPROILS.BBG AP OIL & GAS SERVICE",
                    "BPRWATR.BBG AP WATER INDEX",
                    "BPRSHIP.BBG AP SHIPBUILDING INDX",
                    "BPRHWAR.BBG AP HOUSEWARES INDEX",
                    "BPRAERO.BBG AP AEROSPACE/DEFENSE",
                    "BPRSTOR.BBG AP STORAGE/WAREHOUSE",
                    "BPROFUR.BBG AP OFFICE FURNISHING",
                    "BPRFSRV.BBG PR FOOD SERVICE INDX",
                    "BPRPIPE.BBG AP PIPELINES INDEX",
                    "BPRSAVL.BBG AP SAVINGS & LOANS",
                    "BPRTRUC.BBG AP TRUCKING/LEASING",
                    "BEUTOYS.BBG EMEA TOYS/GAMES/HOBB",
                    "BEUTRUC.BBG EMEA TRUCKING&LEASIN"
                };
            }
            else if (name == "RIY Groups")
            {
                list = new string[]
                {
                    "RIY.RIY INDEX",
                    "RIY SECTORS:RIY SECTORS",
                    "RIY INDUSTRIES:RIY INDUSTRIES"
                };
            }

            else if (name == "RIY SECTORS")
            {
                list = new string[] 
                {
                    "RGUSDL.RIY CONS DISC",
                    "RGUSSL.RIY CONS STAPLES",
                    "RGUSEL.RIY ENERGY",
                    "RGUSFL.RIY FINANCIALS",
                    "RGUSHL.RIY HEALTH CARE",
                    "RGUSML.RIY MATERIALS",
                    "RGUSPL.RIY PRODUCER DUR",
                    "RGUSTL.RIY TECHNOLOGY",
                    "RGUSUL.RIY UTILITIES"
                };
            }
            else if (name == "RIY INDUSTRIES")
            {
                list = new string[] 
                {
                    "RGUSDLAA.RIY AD AGENCIES",
                    "RGUSPLAS.RIY AEROSPACE",
                    "RGUSPLAI.RIY AIR TRANSPORT",
                    "RGUSMLAL.RIY ALUMINUM",
                    "RGUSFLAM.RIY ASSET MGT & CUST",
                    "RGUSDLAP.RIY AUTO PARTS",
                    "RGUSDLAS.RIY AUTO SVC",
                    "RGUSDLAU.RIY AUTOMOBILES",
                    "RGUSFLBK.RIY BANKS DVSFD",
                    "RGUSSLBD.RIY BEV BRW & DSTLR",
                    "RGUSSLSD.RIY BEV SOFT DRNK",
                    "RGUSHLBT.RIY BIOTEC",
                    "RGUSMLCC.RIY BLDG CLIMATE CTRL",
                    "RGUSFLBS.RIY BNK SVG THRF MRT",
                    "RGUSPLBO.RIY BO SUP HR & CONS",
                    "RGUSMLBM.RIY BUILDING MATL",
                    "RGUSDLCT.RIY CABLE TV SVC",
                    "RGUSDLCG.RIY CASINOS & GAMB",
                    "RGUSMLCM.RIY CEMENT",
                    "RGUSMLCS.RIY CHEM SPEC",
                    "RGUSMLCD.RIY CHEM DVFSD",
                    "RGUSTLCS.RIY CMP SVC SFW & SYS",
                    "RGUSELCO.RIY COAL",
                    "RGUSPLCS.RIY COMM SVC",
                    "RGUSFLFM.RIY COMM FIN & MORT",
                    "RGUSPLCL.RIY COMM SVC RN",
                    "RGUSTLCM.RIY COMM TECH",
                    "RGUSPLCV.RIY COMM VEH & PRTS",
                    "RGUSTLCT.RIY COMPUTER TECH",
                    "RGUSPLCN.RIY CONS",
                    "RGUSDLCM.RIY CONS SVC  MISC",
                    "RGUSFLCL.RIY CONSUMER LEND",
                    "RGUSDLCE.RIY CONSUMER ELECT",
                    "RGUSMLCP.RIY CONTAINER & PKG",
                    "RGUSMLCR.RIY COPPER",
                    "RGUSDLCS.RIY COSMETICS",
                    "RGUSSLDG.RIY DRUG & GROC CHN",
                    "RGUSFLDF.RIY DVSFD FNCL SVC",
                    "RGUSDLDM.RIY DVSFD MEDIA",
                    "RGUSDLDR.RIY DVSFD RETAIL",
                    "RGUSMLDM.RIY DVSFD MAT & PROC",
                    "RGUSPLDO.RIY DVSFD MFG OPS",
                    "RGUSDLES.RIY EDUCATION SVC",
                    "RGUSTLEC.RIY ELECT COMP",
                    "RGUSTLEE.RIY ELECT ENT",
                    "RGUSTLEL.RIY ELECTRONICS",
                    "RGUSELEQ.RIY ENERGY EQ",
                    "RGUSPLEC.RIY ENG & CONTR SVC",
                    "RGUSDLEN.RIY ENTERTAINMENT",
                    "RGUSPLEN.RIY ENV MN & SEC SVC",
                    "RGUSMLFT.RIY FERTILIZERS",
                    "RGUSFLFD.RIY FINCL DATA & SYS",
                    "RGUSSLFO.RIY FOODS",
                    "RGUSMLFP.RIY FOREST PROD",
                    "RGUSPLFB.RIY FRM & BLK PRNT SVC",
                    "RGUSSLFG.RIY FRUIT & GRN PROC",
                    "RGUSDLFU.RIY FUN PARLOR & CEM",
                    "RGUSELGP.RIY GAS PIPELINE",
                    "RGUSMLGO.RIY GOLD",
                    "RGUSDLHE.RIY HHLD EQP & PROD",
                    "RGUSDLHF.RIY HHLD FURN",
                    "RGUSHLHF.RIY HLTH CARE FAC",
                    "RGUSHLHS.RIY HLTH CARE SVC",
                    "RGUSHLHM.RIY HLTH C MGT SVC",
                    "RGUSDLHB.RIY HOMEBUILDING",
                    "RGUSDLHO.RIY HOTEL/MOTEL",
                    "RGUSDLHA.RIY HOUSEHOLD APPL",
                    "RGUSFLIL.RIY INS LIFE",
                    "RGUSFLIM.RIY INS MULTI-LN",
                    "RGUSFLIP.RIY INS PROP-CAS",
                    "RGUSPLIT.RIY INTL TRD & DV LG",
                    "RGUSDLLT.RIY LEISURE TIME",
                    "RGUSDLLX.RIY LUXURY ITEMS",
                    "RGUSPLMI.RIY MACH INDU",
                    "RGUSPLMT.RIY MACH TOOLS",
                    "RGUSPLMA.RIY MACH AG",
                    "RGUSPLME.RIY MACH ENGINES",
                    "RGUSPLMS.RIY MACH SPECIAL",
                    "RGUSPLMH.RIY MCH CONS & HNDL",
                    "RGUSHLMD.RIY MD & DN INS & SUP",
                    "RGUSHLME.RIY MED EQ",
                    "RGUSHLMS.RIY MED SVC",
                    "RGUSMLMD.RIY MET & MIN DVFSD",
                    "RGUSMLMF.RIY METAL FABRIC",
                    "RGUSSLMC.RIY MISC CONS STAPLE",
                    "RGUSPLOE.RIY OFF SUP & EQ",
                    "RGUSELOF.RIY OFFSHORE DRILL",
                    "RGUSELOI.RIY OIL INTEGRATE",
                    "RGUSELOC.RIY OIL CRUDE PROD",
                    "RGUSELOR.RIY OIL REF & MKT",
                    "RGUSELOW.RIY OIL WELL EQ & SVC",
                    "RGUSMLPC.RIY PAINT & COATING",
                    "RGUSMLPA.RIY PAPER",
                    "RGUSSLPC.RIY PERSONAL CARE",
                    "RGUSHLPH.RIY PHRM",
                    "RGUSPLPD.RIY PROD DUR MISC",
                    "RGUSTLPR.RIY PRODUCT TECH EQ",
                    "RGUSDLPU.RIY PUBLISHING",
                    "RGUSPLPT.RIY PWR TRANSM EQ",
                    "RGUSDLRB.RIY RADIO & TV BROAD",
                    "RGUSPLRL.RIY RAILROAD EQ",
                    "RGUSPLRA.RIY RAILROADS",
                    "RGUSFLRE.RIY REAL ESTATE",
                    "RGUSFLRI.RIY REIT",
                    "RGUSDLRT.RIY RESTAURANTS",
                    "RGUSMLRW.RIY ROOF WALL & PLUM",
                    "RGUSDLRC.RIY RT & LS SVC CONS",
                    "RGUSDLRV.RIY RV & BOATS",
                    "RGUSPLSI.RIY SCI INS CTL & FLT",
                    "RGUSPLSP.RIY SCI INS POL CTRL",
                    "RGUSPLSG.RIY SCI INST GG & MTR",
                    "RGUSPLSE.RIY SCI INSTR ELEC",
                    "RGUSFLSB.RIY SEC BRKG & SVC",
                    "RGUSTLSC.RIY SEMI COND & COMP",
                    "RGUSPLSH.RIY SHIPPING",
                    "RGUSDLSR.RIY SPEC RET",
                    "RGUSMLST.RIY STEEL",
                    "RGUSTLTM.RIY TECHNO MISC",
                    "RGUSTLTE.RIY TELE EQ",
                    "RGUSDLTX.RIY TEXT APP & SHOES",
                    "RGUSSLTO.RIY TOBACCO",
                    "RGUSDLTY.RIY TOYS",
                    "RGUSPLTM.RIY TRANS MISC",
                    "RGUSPLTK.RIY TRUCKERS",
                    "RGUSULUM.RIY UTIL  MISC",
                    "RGUSULUE.RIY UTIL GAS DIST",
                    "RGUSULUT.RIY UTIL TELE",
                    "RGUSULUW.RIY UTIL WATER"
                };
            }
            else if (name == "RLG Groups")
            {
                list = new string[] {
                    "RLG.RLG INDEX",
                    "RLG SECTORS:RLG SECTORS",
                    "RLG INDUSTRIES:RLG INDUSTRIES" };
            }
            else if (name == "RLG SECTORS")
            {
                list = new string[] 
                {
                    "RGUSDLG.RLG CONS DISC",
                    "RGUSSLG.RLG CONS STAPLES",
                    "RGUSELG.RLG ENERGY",
                    "RGUSFLG.RLG FINANCIALS",
                    "RGUSHLG.RLG HEALTH CARE",
                    "RGUSMLG.RLG MATERIALS",
                    "RGUSPLG.RLG PRODUCER DUR",
                    "RGUSTLG.RLG TECHNOLOGY",
                    "RGUSULG.RLG UTILITIES"
                };
            }
            else if (name == "RLG INDUSTRIES")
            {
                list = new string[] 
                {
                    "RGUDLAAG.RLG Ad Age Grw",
                    "RGUDLAPG.RLG AutoPrts Grw",
                    "RGUDLASG.RLG Auto Svc Grw",
                    "RGUDLAUG.RLG Automob Grw",
                    "RGUDLCEG.RLG ConsElect Grw",
                    "RGUDLCGG.RLG Cas&Gamb Grw",
                    "RGUDLCMG.RLG ConSvcMisc Grw",
                    "RGUDLCSG.RLG Cosmetics Grw",
                    "RGUDLCTG.RLG CabTV Svc Grw",
                    "RGUDLDMG.RLG Dvsfd Med Grw",
                    "RGUDLDRG.RLG Dvsfd Ret Grw",
                    "RGUDLENG.RLG Entertain Grw",
                    "RGUDLESG.RLG Edu Svc Grw",
                    "RGUDLFUG.RLG FuPar&Cem Grw",
                    "RGUDLHAG.RLG HholdAppl Grw",
                    "RGUDLHBG.RLG HomeBldg Grw",
                    "RGUDLHEG.RLG HHEq&Pro Grw",
                    "RGUDLHFG.RLG Hhld Furn Grw",
                    "RGUDLHOG.RLG Htel/Mtel Grw",
                    "RGUDLLTG.RLG Leis Time Grw",
                    "RGUDLLXG.RLG Lux Items Grw",
                    "RGUDLMHG.RLG Mfg Hous Grw",
                    "RGUDLPCG.RLG Prnt&CpySv Grw",
                    "RGUDLPHG.RLG Photograph Grw",
                    "RGUDLPUG.RLG Publishing Grw",
                    "RGUDLRBG.RLG Radio&TVBr Grw",
                    "RGUDLRCG.RLG R&LSv Con Grw",
                    "RGUDLRTG.RLG Restaurant Grw",
                    "RGUDLRVG.RLG RV & Boats Grw",
                    "RGUDLSFG.RLG StorageFac Grw",
                    "RGUDLSRG.RLG Spec Ret  Grw",
                    "RGUDLTXG.RLG TxtAp&Shoe Grw",
                    "RGUDLTYG.RLG Toys Grw",
                    "RGUDLVCG.RLG Vend&Cat Grw",
                    "RGUELAEG.RLG AlterEner Grw",
                    "RGUELCOG.RLG Coal Grw",
                    "RGUELEQG.RLG Energy Eq Grw",
                    "RGUELGPG.RLG Gas Pipe Grw",
                    "RGUELOCG.RLG Oil CrudPr Grw",
                    "RGUELOFG.RLG OffshrDri Grw",
                    "RGUELOIG.RLG Oil Integ Grw",
                    "RGUELORG.RLG Oil Ref&Mkt GR",
                    "RGUELOWG.RLG OilWlEq&Sv Grw",
                    "RGUFLAMG.RLG AstMgt&Cs Grw",
                    "RGUFLBKG.RLG Bnks Dvfd Grw",
                    "RGUFLBSG.RLG BkSvThrfMt Grw",
                    "RGUFLCLG.RLG Cons Lend Grw",
                    "RGUFLDFG.RLG DvfFnlSvc Grw",
                    "RGUFLDRG.RLG DvsfREstateActG",
                    "RGUFLEDG.RLG EquityREIT DivG",
                    "RGUFLEFG.RLG EquityREIT InfG",
                    "RGUFLEHG.RLG EquityREIT HcaG",
                    "RGUFLEIG.RLG EquityREIT IndG",
                    "RGUFLELG.RLG EquityREIT L&RG",
                    "RGUFLEOG.RLG EquityREIT OffG",
                    "RGUFLERG.RLG EquityREIT ResG",
                    "RGUFLESG.RLG EquityREIT StoG",
                    "RGUFLFDG.RLG FinDat&Sy Grw",
                    "RGUFLFMG.RLG ComFn&Mtg Grw",
                    "RGUFLILG.RLG Ins Life Grw",
                    "RGUFLIMG.RLG Ins MulLn Grw",
                    "RGUFLIPG.RLG Ins ProCa Grw",
                    "RGUFLMCG.RLG MortgREIT ComG",
                    "RGUFLMDG.RLG MortgREIT DivG",
                    "RGUFLMSG.RLG MortgREIT ResG",
                    "RGUFLOSG.RLG EquityREIT OSpG",
                    "RGUFLRHG.RLG RealEstate H&DG",
                    "RGUFLRSG.RLG RealEstate SvcG",
                    "RGUFLRTG.RLG EquityREIT RetG",
                    "RGUFLSBG.RLG SecBrk&Svc Grw",
                    "RGUFLTIG.RLG EquityREIT TimG",
                    "RGUHLBTG.RLG Biotec Grw",
                    "RGUHLHCG.RLG HlthC Misc Grw",
                    "RGUHLHFG.RLG HC Fac Grw",
                    "RGUHLHMG.RLG HC MgtSvc Grw",
                    "RGUHLHSG.RLG HCare Svc Grw",
                    "RGUHLMDG.RLG M&DIns&Sp Grw",
                    "RGUHLMEG.RLG Med Eq Grw",
                    "RGUHLMSG.RLG Med Svc Grw",
                    "RGUHLPHG.RLG Phrm Grw",
                    "RGUMLALG.RLG Aluminum Grw",
                    "RGUMLBMG.RLG BldgMatl Grw",
                    "RGUMLCCG.RLG Bld ClCtr Grw",
                    "RGUMLCDG.RLG Chem Dvfd Grw",
                    "RGUMLCMG.RLG Cement Grw",
                    "RGUMLCPG.RLG Cont&Pkg Grw",
                    "RGUMLCRG.RLG Copper Grw",
                    "RGUMLCSG.RLG Chem Spec Grw",
                    "RGUMLDMG.RLG DvfMt&Prc Grw",
                    "RGUMLFPG.RLG ForestPrd Grw",
                    "RGUMLFTG.RLG Fertiliz Grw",
                    "RGUMLGLG.RLG Glass Grw",
                    "RGUMLGOG.RLG Gold Grw",
                    "RGUMLMDG.RLG Met&MinDvf Grw",
                    "RGUMLMFG.RLG MetFabric Grw",
                    "RGUMLPAG.RLG Paper Grw",
                    "RGUMLPCG.RLG Paint&Coat Grw",
                    "RGUMLPLG.RLG Plastics Grw",
                    "RGUMLPMG.RLG PrcMt&Min Grw",
                    "RGUMLRWG.RLG RfWal&Plm Grw",
                    "RGUMLSTG.RLG Steel Grw",
                    "RGUMLSYG.RLG SynFib&Chm Grw",
                    "RGUMLTPG.RLG Text Prod Grw",
                    "RGUPLAIG.RLG Air Trans Grw",
                    "RGUPLASG.RLG Aerospace Grw",
                    "RGUPLBOG.RLG BOSupHR&Cons GR",
                    "RGUPLCLG.RLG CommSvcR&L Grw",
                    "RGUPLCNG.RLG Cons Grw",
                    "RGUPLCVG.RLG CoVeh&Prt Grw",
                    "RGUPLDOG.RLG DvfMfgOps Grw",
                    "RGUPLECG.RLG Eng&CtrSv Grw",
                    "RGUPLENG.RLG EnvMn&SecSvc GR",
                    "RGUPLFBG.RLG FmBlkPrSv Grw",
                    "RGUPLITG.RLG IntlTrd&DvLg GR",
                    "RGUPLMAG.RLG Mach Ag Grw",
                    "RGUPLMEG.RLG Mach Eng Grw",
                    "RGUPLMHG.RLG Mch Cn&Hn Grw",
                    "RGUPLMIG.RLG Mach Indu Grw",
                    "RGUPLMSG.RLG Mach Spec Grw",
                    "RGUPLMTG.RLG Mach Tool Grw",
                    "RGUPLOEG.RLG OffSup&Eq Grw",
                    "RGUPLPDG.RLG PrdDr Misc Grw",
                    "RGUPLPTG.RLG PwrTranEq Grw",
                    "RGUPLRAG.RLG Railroads Grw",
                    "RGUPLRLG.RLG RailroadEq Grw",
                    "RGUPLSEG.RLG SciIn Elc Grw",
                    "RGUPLSGG.RLG ScInGg&Mt Grw",
                    "RGUPLSHG.RLG Shipping Grw",
                    "RGUPLSIG.RLG SciICt&Fl Grw",
                    "RGUPLSPG.RLG SciInPCtrl Grw",
                    "RGUPLTKG.RLG Truckers Grw",
                    "RGUPLTMG.RLG Trans Misc Grw",
                    "RGUSLAFG.RLG AgFsh&Ran Grw",
                    "RGUSLBDG.RLG Bev Br&Ds Grw",
                    "RGUSLDGG.RLG Drg&GrcCh Grw",
                    "RGUSLFGG.RLG Fru&GrnPrc Grw",
                    "RGUSLFOG.RLG Foods Grw",
                    "RGUSLMCG.RLG MiscConsSt Grw",
                    "RGUSLPCG.RLG Pers Care Grw",
                    "RGUSLSDG.RLG Bev SftDr Grw",
                    "RGUSLSUG.RLG Sugar Grw",
                    "RGUSLTOG.RLG Tobacco Grw",
                    "RGUTLCMG.RLG Comm Tech Grw",
                    "RGUTLCSG.RLG CpSvSw&Sy Grw",
                    "RGUTLCTG.RLG Comp Tech Grw",
                    "RGUTLECG.RLG Elec Comp Grw",
                    "RGUTLEEG.RLG ElecEnt Grw",
                    "RGUTLELG.RLG Elec Grw",
                    "RGUTLPRG.RLG ProdTechEq Grw",
                    "RGUTLSCG.RLG SemiCn&Cp Grw",
                    "RGUTLTEG.RLG Tele Eq Grw",
                    "RGUTLTMG.RLG Tech Misc Grw",
                    "RGUULUEG.RLG Util Elec Grw",
                    "RGUULUGG.RLG Util GasDi Grw",
                    "RGUULUMG.RLG Util Misc Grw",
                    "RGUULUTG.RLG Util Tele Grw",
                    "RGUULUWG.RLG Util Water Grw"
                };
            }

            else if (name == "RLV Groups")
            {
                list = new string[] {
                    "RLV.RLV INDEX",
                    "RLV SECTORS:RLV SECTORS",
                    "RLV INDUSTRIES:RLV INDUSTRIES" };
            }
            else if (name == "RLV SECTORS")
            {
                list = new string[] 
                {
                    "RGUSDLV.RLV CONS DISC",
                    "RGUSSLV.RLV CONS STAPLES",
                    "RGUSELV.RLV ENERGY",
                    "RGUSFLV.RLV FINANCIALS",
                    "RGUSHLV.RLV HEALTH CARE",
                    "RGUSMLV.RLV MATERIALS",
                    "RGUSPLV.RLV PRODUCER DUR",
                    "RGUSTLV.RLV TECHNOLOGY",
                    "RGUSULV.RLV UTILITIES"
                };
                    
            }
            else if (name == "RLV INDUSTRIES")
            {
                list = new string[] 
                {
                    "RGUDLAAV.RLV Ad Age Val",
                    "RGUDLAPV.RLV AutoPrts Val",
                    "RGUDLASV.RLV Auto Svc Val",
                    "RGUDLAUV.RLV Automob Val",
                    "RGUDLCEV.RLV ConsElect Val",
                    "RGUDLCGV.RLV Cas&Gamb Val",
                    "RGUDLCMV.RLV ConSvcMisc Val",
                    "RGUDLCSV.RLV Cosmetics Val",
                    "RGUDLCTV.RLV CabTV Svc Val",
                    "RGUDLDMV.RLV Dvsfd Med Val",
                    "RGUDLDRV.RLV Dvsfd Ret Val",
                    "RGUDLENV.RLV Entertain Val",
                    "RGUDLESV.RLV Edu Svc Val",
                    "RGUDLFUV.RLV FuPar&Cem Val",
                    "RGUDLHAV.RLV HholdAppl Val",
                    "RGUDLHBV.RLV HomeBldg Val",
                    "RGUDLHEV.RLV HHEq&Pro Val",
                    "RGUDLHFV.RLV Hhld Furn Val",
                    "RGUDLHOV.RLV Htel/Mtel Val",
                    "RGUDLLTV.RLV Leis Time Val",
                    "RGUDLLXV.RLV Lux Items Val",
                    "RGUDLMHV.RLV Mfg Hous Val",
                    "RGUDLPCV.RLV Prnt&CpySv Val",
                    "RGUDLPHV.RLV Photograph Val",
                    "RGUDLPUV.RLV Publishing Val",
                    "RGUDLRBV.RLV Radio&TVBr Val",
                    "RGUDLRCV.RLV R&LSv:Con Val",
                    "RGUDLRTV.RLV Restaurant Val",
                    "RGUDLRVV.RLV RV & Boats Val",
                    "RGUDLSFV.RLV StorageFac Val",
                    "RGUDLSRV.RLV Spec Ret  Val",
                    "RGUDLTXV.RLV TxtAp&Shoe Val",
                    "RGUDLTYV.RLV Toys Val",
                    "RGUDLVCV.RLV Vend&Cat Val",
                    "RGUELAEV.RLV AlterEner Val",
                    "RGUELCOV.RLV Coal Val",
                    "RGUELEQV.RLV Energy Eq Val",
                    "RGUELGPV.RLV Gas Pipe Val",
                    "RGUELOCV.RLV Oil:CrudPr Val",
                    "RGUELOFV.RLV OffshrDri Val",
                    "RGUELOIV.RLV Oil Integ Val",
                    "RGUELORV.RLV Oil:Ref&Mkt VA",
                    "RGUELOWV.RLV OilWlEq&Sv Val",
                    "RGUFLAMV.RLV AstMgt&Cs Val",
                    "RGUFLBKV.RLV Bnks:Dvfd Val",
                    "RGUFLBSV.RLV BkSvThrfMt Val",
                    "RGUFLCLV.RLV Cons Lend Val",
                    "RGUFLDFV.RLV DvfFnlSvc Val",
                    "RGUFLDRV.RLV DvsfREstateActV",
                    "RGUFLEDV.RLV EquityREIT:DivV",
                    "RGUFLEFV.RLV EquityREIT:InfV",
                    "RGUFLEHV.RLV EquityREIT:HcaV",
                    "RGUFLEIV.RLV EquityREIT:IndV",
                    "RGUFLELV.RLV EquityREIT:L&RV",
                    "RGUFLEOV.RLV EquityREIT:OffV",
                    "RGUFLERV.RLV EquityREIT:ResV",
                    "RGUFLESV.RLV EquityREIT:StoV",
                    "RGUFLFDV.RLV FinDat&Sy Val",
                    "RGUFLFMV.RLV ComFn&Mtg Val",
                    "RGUFLILV.RLV Ins:Life Val",
                    "RGUFLIMV.RLV Ins:MulLn Val",
                    "RGUFLIPV.RLV Ins:ProCa Val",
                    "RGUFLMCV.RLV MortgREIT:ComV",
                    "RGUFLMDV.RLV MortgREIT:DivV",
                    "RGUFLMSV.RLV MortgREIT:ResV",
                    "RGUFLOSV.RLV EquityREIT:OSpV",
                    "RGUFLRHV.RLV RealEstate H&DV",
                    "RGUFLRSV.RLV RealEstate SvcV",
                    "RGUFLRTV.RLV EquityREIT:RetV",
                    "RGUFLSBV.RLV SecBrk&Svc Val",
                    "RGUFLTIV.RLV EquityREIT:TimV",
                    "RGUHLBTV.RLV Biotec Val",
                    "RGUHLHCV.RLV HlthC:Misc Val",
                    "RGUHLHFV.RLV HC Fac Val",
                    "RGUHLHMV.RLV HC MgtSvc Val",
                    "RGUHLHSV.RLV HCare Svc Val",
                    "RGUHLMDV.RLV M&DIns&Sp Val",
                    "RGUHLMEV.RLV Med Eq Val",
                    "RGUHLMSV.RLV Med Svc Val",
                    "RGUHLPHV.RLV Phrm Val",
                    "RGUMLALV.RLV Aluminum Val",
                    "RGUMLBMV.RLV BldgMatl Val",
                    "RGUMLCCV.RLV Bld:ClCtr Val",
                    "RGUMLCDV.RLV Chem:Dvfd Val",
                    "RGUMLCMV.RLV Cement Val",
                    "RGUMLCPV.RLV Cont&Pkg Val",
                    "RGUMLCRV.RLV Copper Val",
                    "RGUMLCSV.RLV Chem Spec Val",
                    "RGUMLDMV.RLV DvfMt&Prc Val",
                    "RGUMLFPV.RLV ForestPrd Val",
                    "RGUMLFTV.RLV Fertiliz Val",
                    "RGUMLGLV.RLV Glass Val",
                    "RGUMLGOV.RLV Gold Val",
                    "RGUMLMDV.RLV Met&MinDvf Val",
                    "RGUMLMFV.RLV MetFabric Val",
                    "RGUMLPAV.RLV Paper Val",
                    "RGUMLPCV.RLV Paint&Coat Val",
                    "RGUMLPLV.RLV Plastics Val",
                    "RGUMLPMV.RLV PrcMt&Min Val",
                    "RGUMLRWV.RLV RfWal&Plm Val",
                    "RGUMLSTV.RLV Steel Val",
                    "RGUMLSYV.RLV SynFib&Chm Val",
                    "RGUMLTPV.RLV Text Prod Val",
                    "RGUPLAIV.RLV Air Trans Val",
                    "RGUPLASV.RLV Aerospace Val",
                    "RGUPLBOV.RLV BOSupHR&Cons VA",
                    "RGUPLCLV.RLV CommSvcR&L Val",
                    "RGUPLCNV.RLV Cons Val",
                    "RGUPLCVV.RLV CoVeh&Prt Val",
                    "RGUPLDOV.RLV DvfMfgOps Val",
                    "RGUPLECV.RLV Eng&CtrSv Val",
                    "RGUPLENV.RLV EnvMn&SecSvc VA",
                    "RGUPLFBV.RLV FmBlkPrSv Val",
                    "RGUPLITV.RLV IntlTrd&DvLg VA",
                    "RGUPLMAV.RLV Mach:Ag Val",
                    "RGUPLMEV.RLV Mach:Eng Val",
                    "RGUPLMHV.RLV Mch:Cn&Hn Val",
                    "RGUPLMIV.RLV Mach: Indu Val",
                    "RGUPLMSV.RLV Mach:Spec Val",
                    "RGUPLMTV.RLV Mach:Tool Val",
                    "RGUPLOEV.RLV OffSup&Eq Val",
                    "RGUPLPDV.RLV PrdDr:Misc Val",
                    "RGUPLPTV.RLV PwrTranEq Val",
                    "RGUPLRAV.RLV Railroads Val",
                    "RGUPLRLV.RLV RailroadEq Val",
                    "RGUPLSEV.RLV SciIn:Elc Val",
                    "RGUPLSGV.RLV ScInGg&Mt Val",
                    "RGUPLSHV.RLV Shipping Val",
                    "RGUPLSIV.RLV SciICt&Fl Val",
                    "RGUPLSPV.RLV SciInPCtrl Val",
                    "RGUPLTKV.RLV Truckers Val",
                    "RGUPLTMV.RLV Trans Misc Val",
                    "RGUSLAFV.RLV AgFsh&Ran Val",
                    "RGUSLBDV.RLV Bev:Br&Ds Val",
                    "RGUSLDGV.RLV Drg&GrcCh Val",
                    "RGUSLFGV.RLV Fru&GrnPrc Val",
                    "RGUSLFOV.RLV Foods Val",
                    "RGUSLMCV.RLV MiscConsSt Val",
                    "RGUSLPCV.RLV Pers Care Val",
                    "RGUSLSDV.RLV Bev:SftDr Val",
                    "RGUSLSUV.RLV Sugar Val",
                    "RGUSLTOV.RLV Tobacco Val",
                    "RGUTLCMV.RLV Comm Tech Val",
                    "RGUTLCSV.RLV CpSvSw&Sy Val",
                    "RGUTLCTV.RLV Comp Tech Val",
                    "RGUTLECV.RLV Elec Comp Val",
                    "RGUTLEEV.RLV ElecEnt Val",
                    "RGUTLELV.RLV Elec Val",
                    "RGUTLPRV.RLV ProdTechEq Val",
                    "RGUTLSCV.RLV SemiCn&Cp Val",
                    "RGUTLTEV.RLV Tele Eq Val",
                    "RGUTLTMV.RLV Tech:Misc Val",
                    "RGUULUEV.RLV Util:Elec Val",
                    "RGUULUGV.RLV Util:GasDi Val",
                    "RGUULUMV.RLV Util: Misc Val",
                    "RGUULUTV.RLV Util:Tele Val",
                    "RGUULUWV.RLV Util:Water Val"
                };
            }


            else if (name == "RAY Groups")
            {
                list = new string[] 
                {
                    //"RAY.RAY INDEX",
                    //"RAY SECTORS:RAY SECTORS",
                    "RAY INDUSTRIES:RAY INDUSTRIES"
                };
            }
            else if (name == "RAY SECTORS")
            {
                list = new string[] 
                {
                    "RGUSD.RAY CONS DISC",
                    "RGUSS.RAY CONS STAPLES",
                    "RGUSE.RAY ENERGY",
                    "RGUSF.RAY FINANCIALS",
                    "RGUSH.RAY HEALTH CARE",
                    "RGUSM.RAY MATERIALS",
                    "RGUSP.RAY PRODUCER DUR",
                    "RGUST.RAY TECHNOLOGY",
                    "RGUSU.RAY UTILITIES"
                };
            }
            else if (name == "RAY INDUSTRIES")
            {
                list = new string[] 
                {
                    "RGUSDAA.RAY AD AGENCIES",
                    "RGUSPAS.RAY AEROSPACE",
                    "RGUSSAF.RAY AG FISH & RNCH",
                    "RGUSPAI.RAY AIR TRANSPORT",
                    "RGUSEAE.RAY ALTER ENERGY",
                    "RGUSMAL.RAY ALUMINUM",
                    "RGUSFAM.RAY ASSET MGT & CUST",
                    "RGUSDAP.RAY AUTO PARTS",
                    "RGUSDAS.RAY AUTO SVC",
                    "RGUSDAU.RAY AUTOMOBILES",
                    "RGUSFBK.RAY BANKS DVSFD",
                    "RGUSFBS.RAY BEV BRW & DSTLR",
                    "RGUSSBD.RAY BEV SOFT DRNK",
                    "RGUSSSD.RAY SOFT DRNK",
                    "RGUSHBT.RAY BIOTEC",
                    "RGUSMCC.RAY BLDG CLIMATE CTRL",
                    "RGUSPBO.RAY BO SUP HR & CONS",
                    "RGUSMBM.RAY BUILDING MATL",
                    "RGUSDCT.RAY CABLE TV SVC",
                    "RGUSDCG.RAY CASINOS & GAMB",
                    "RGUSMCM.RAY CEMENT",
                    "RGUSMCS.RAY CHEM SPEC",
                    "RGUSMCD.RAY CHEM DVFSD",
                    "RGUSECO.RAY CMP SVC SFW & SYS",
                    "RGUSFFM.RAY COAL",
                    "RGUSPCS.RAY COMM SVC",
                    "RGUSPCL.RAY COMM FIN & MORT",
                    "RGUSTCM.RAY COMM SVC RN",
                    "RGUSPCV.RAY COMM TECH",
                    "RGUSTCS.RAY COMM VEH & PRTS",
                    "RGUSTCT.RAY COMPUTER TECH",
                    "RGUSPCN.RAY CONS",
                    "RGUSDCM.RAY CONS SVC  MISC",
                    "RGUSDCE.RAY CONSUMER LEND",
                    "RGUSFCL.RAY CONSUMER ELECT",
                    "RGUSMCP.RAY CONTAINER & PKG",
                    "RGUSMCR.RAY COPPER",
                    "RGUSDCS.RAY COSMETICS",
                    "RGUSDDM.RAY DRUG & GROC CHN",
                    "RGUSSDG.RAY DVSFD FNCL SVC",
                    "RGUSFDF.RAY DVSFD MEDIA",
                    "RGUSDDR.RAY DVSFD RETAIL",
                    "RGUSMDM.RAY DVSFD MAT & PROC",
                    "RGUSPDO.RAY DVSFD MFG OPS",
                    "RGUSDES.RAY EDUCATION SVC",
                    "RGUSTEE.RAY ELECT COMP",
                    "RGUSTEC.RAY ELECT ENT",
                    "RGUSTEL.RAY ELECTRONICS",
                    "RGUSEEQ.RAY ENERGY EQ",
                    "RGUSPEC.RAY ENG & CONTR SVC",
                    "RGUSDEN.RAY ENTERTAINMENT",
                    "RGUSPEN.RAY ENV MN & SEC SVC",
                    "RGUSMFT.RAY FERTILIZERS",
                    "RGUSFFD.RAY FINCL DATA & SYS",
                    "RGUSSFO.RAY FOODS",
                    "RGUSMFP.RAY FOREST PROD",
                    "RGUSPFB.RAY FRM & BLK PRNT SVC",
                    "RGUSSFG.RAY FRUIT & GRN PROC",
                    "RGUSDFU.RAY FUN PARLOR & CEM",
                    "RGUSEGP.RAY GAS PIPELINE",
                    "RGUSMGL.RAY GLASS",
                    "RGUSMGO.RAY GOLD",
                    "RGUSDHE.RAY HHLD EQP & PROD",
                    "RGUSDHF.RAY HHLD FURN",
                    "RGUSHHF.RAY HLTH CARE FAC",
                    "RGUSHHM.RAY HLTH CARE SVC",
                    "RGUSHHS.RAY HLTH C MGT SVC",
                    "RGUSHHC.RAY HLTH C MISC",
                    "RGUSDHB.RAY HOME BUILDING",
                    "RGUSDHO.RAY HOTEL/MOTEL",
                    "RGUSDHA.RAY HOUSEHOLD APPL",
                    "RGUSFIL.RAY INS LIFE",
                    "RGUSFIM.RAY INS MULTI-LN",
                    "RGUSFIP.RAY INS PROP-CAS",
                    "RGUSPIT.RAY INTL TRD & DV LG",
                    "RGUSDLT.RAY LEISURE TIME",
                    "RGUSPMI.RAY MACH IND",
                    "RGUSPMT.RAY MACH TOOLS",
                    "RGUSPMA.RAY MACH AG",
                    "RGUSPMH.RAY MACH ENGINES",
                    "RGUSPME.RAY MACH SPECIAL",
                    "RGUSPMS.RAY MCH CONS & HNDL",
                    "RGUSHME.RAY MD & DN INS & SUP",
                    "RGUSHMS.RAY MED EQ",
                    "RGUSHMD.RAY MED SVC",
                    "RGUSMMD.RAY MET & MIN DVFSD",
                    "RGUSMMF.RAY METAL FABRIC",
                    "RGUSDMH.RAY MFG HOUSING",
                    "RGUSSMC.RAY MISC CONS STAPL",
                    "RGUSPOE.RAY OFF SUP & EQ",
                    "RGUSEOF.RAY OFFSHORE DRILL",
                    "RGUSEOI.RAY OIL INTERGATE",
                    "RGUSEOW.RAY OIL CRUDE PROD",
                    "RGUSEOC.RAY OIL REF & MKT",
                    "RGUSEOR.RAY OIL WELL EQ & SVC",
                    "RGUSMPC.RAY PAINT & COATING",
                    "RGUSMPA.RAY PAPER",
                    "RGUSSPC.RAY PERSONAL CARE",
                    "RGUSDPH.RAY PHOTOGRAPHY",
                    "RGUSHPH.RAY PHRM",
                    "RGUSMPL.RAY PLASTICS",
                    "RGUSPOPT.RAY PREC MET & MINL",
                    "RGUSMPM.RAY PRINT & COPY SVC",
                    "RGUSDPC.RAY PROD DUR MISC",
                    "RGUSPPD.RAY PRODUCT TECH EQ",
                    "RGUSTPR.RAY PUBLISHING",
                    "RGUSDPU.RAY PWR TRANSM EQ",
                    "RGUSDRB.RAY RADIO & TV BROAD",
                    "RGUSPRL.RAY RAILROAD EQ",
                    "RGUSPRA.RAY RAILROADS",
                    "RGUSFRE.RAY REAL ESTATE",
                    "RGUSFRI.RAY REIT",
                    "RGUSDRC.RAY RESTAURANTS",
                    "RGUSDRT.RAY ROOF WALL & PLUM",
                    "RGUSMRW.RAY RT & LS SVC CONS",
                    "RGUSDRV.RAY RV & BOATS",
                    "RGUSPSE.RAY SCI INS CTL & FLT",
                    "RGUSPSP.RAY SCI INS POL CTRL",
                    "RGUSPSI.RAY Sci INSTR ELEC",
                    "RGUSFSB.RAY SEC BRKG & SVC",
                    "RGUSTSC.RAY SE COND & COMP",
                    "RGUSPSH.RAY SHIPPING",
                    "RGUSDSR.RAY SPEC RET",
                    "RGUSMST.RAY STEEL",
                    "RGUSMSY.RAY SYN FIBR & CHEM",
                    "RGUSTTM.RAY TECHNOLOGY MISC",
                    "RGUSTTE.RAY TELEG EQ",
                    "RGUSDTX.RAY TEXT APP & SHORES",
                    "RGUSMTP.RAY TEXTILE PROD",
                    "RGUSSTO.RAY TOBACC0",
                    "RGUSDTY.RAY TOYS",
                    "RGUSPTM.RAY TRANS MISC",
                    "RGUSPTK.RAY TRUCK MISC",
                    "RGUSUUE.RAY UTIL ELEC",
                    "RGUSUUG.RAY UTIL GAS DIST",
                    "RGUSUUT.RAY UTIL TELE",
                    "RGUSUUW.RAY UTIL WATER"};
            }
            else if (name == "RUO Groups")
            {
                list = new string[]
                {
                    "RUO.RUO INDEX",
                    "RUO SECTORS:RUO SECTORS",
                    "RUO INDUSTRIES:RUO INDUSTRIES"
                };
            }
            else if (name == "RUO SECTORS")
            {
                list = new string[] 
                {
                    "RGUSDSG.RUO CONS DISC",
                    "RGUSSSG.RUO CONS STAPLES",
                    "RGUSESG.RUO ENERGY",
                    "RGUSFSG.RUO FINANCIALS",
                    "RGUSHSG.RUO HEALTH CARE",
                    "RGUSMSG.RUO MATERIALS",
                    "RGUSPSG.RUO PRODUCER DUR",
                    "RGUSTSG.RUO TECHNOLOGY",
                    "RGUSUSG.RUO UTILITIES"
                };
            }
            else if (name == "RUO INDUSTRIES")
            {
                list = new string[] 
                {
                    "RGUDSAAG.RUO Ad Age Grw",
                    "RGUDSAPG.RUO AutoPrts Grw",
                    "RGUDSASG.RUO Auto Svc Grw",
                    "RGUDSAUG.RUO Automob Grw",
                    "RGUDSCEG.RUO ConsElect Grw",
                    "RGUDSCGG.RUO Cas&Gamb Grw",
                    "RGUDSCMG.RUO ConSvcMisc Grw",
                    "RGUDSCSG.RUO Cosmetics Grw",
                    "RGUDSCTG.RUO CabTV Svc Grw",
                    "RGUDSDMG.RUO Dvsfd Med Grw",
                    "RGUDSDRG.RUO Dvsfd Ret Grw",
                    "RGUDSENG.RUO Entertain Grw",
                    "RGUDSESG.RUO Edu Svc Grw",
                    "RGUDSFUG.RUO FuPar&Cem Grw",
                    "RGUDSHAG.RUO HholdAppl Grw",
                    "RGUDSHBG.RUO HomeBldg Grw",
                    "RGUDSHEG.RUO HHEq&Pro Grw",
                    "RGUDSHFG.RUO Hhld Furn Grw",
                    "RGUDSHOG.RUO Htel/Mtel Grw",
                    "RGUDSLTG.RUO Leis Time Grw",
                    "RGUDSLXG.RUO Lux Items Grw",
                    "RGUDSMHG.RUO Mfg Hous Grw",
                    "RGUDSPCG.RUO Prnt&CpySv Grw",
                    "RGUDSPHG.RUO Photograph Grw",
                    "RGUDSPUG.RUO Publishing Grw",
                    "RGUDSRBG.RUO Radio&TVBr Grw",
                    "RGUDSRCG.RUO R&LSv:Con Grw",
                    "RGUDSRTG.RUO Restaurant Grw",
                    "RGUDSRVG.RUO RV & Boats Grw",
                    "RGUDSSFG.RUO StorageFac Grw",
                    "RGUDSSRG.RUO Spec Ret  Grw",
                    "RGUDSTXG.RUO TxtAp&Shoe Grw",
                    "RGUDSTYG.RUO Toys Grw",
                    "RGUDSVCG.RUO Vend&Cat Grw",
                    "RGUESAEG.RUO AlterEner Grw",
                    "RGUESCOG.RUO Coal Grw",
                    "RGUESEQG.RUO Energy Eq Grw",
                    "RGUESGPG.RUO Gas Pipe Grw",
                    "RGUESOCG.RUO Oil:CrudPr Grw",
                    "RGUESOFG.RUO OffshrDri Grw",
                    "RGUESOIG.RUO Oil Integ Grw",
                    "RGUESORG.RUO Oil:Ref&Mkt GR",
                    "RGUESOWG.RUO OilWlEq&Sv Grw",
                    "RGUFSAMG.RUO AstMgt&Cs Grw",
                    "RGUFSBKG.RUO Bnks:Dvfd Grw",
                    "RGUFSBSG.RUO BkSvThrfMt Grw",
                    "RGUFSCLG.RUO Cons Lend Grw",
                    "RGUFSDFG.RUO DvfFnlSvc Grw",
                    "RGUFSDRG.RUO DvsfREstateActG",
                    "RGUFSEDG.RUO EquityREIT:DivG",
                    "RGUFSEFG.RUO EquityREIT:InfG",
                    "RGUFSEHG.RUO EquityREIT:HcaG",
                    "RGUFSEIG.RUO EquityREIT:IndG",
                    "RGUFSELG.RUO EquityREIT:L&RG",
                    "RGUFSEOG.RUO EquityREIT:OffG",
                    "RGUFSERG.RUO EquityREIT:ResG",
                    "RGUFSESG.RUO EquityREIT:StoG",
                    "RGUFSFDG.RUO FinDat&Sy Grw",
                    "RGUFSFMG.RUO ComFn&Mtg Grw",
                    "RGUFSILG.RUO Ins:Life Grw",
                    "RGUFSIMG.RUO Ins:MulLn Grw",
                    "RGUFSIPG.RUO Ins:ProCa Grw",
                    "RGUFSMCG.RUO MortgREIT:ComG",
                    "RGUFSMDG.RUO MortgREIT:DivG",
                    "RGUFSMSG.RUO MortgREIT:ResG",
                    "RGUFSOSG.RUO EquityREIT:OSpG",
                    "RGUFSRHG.RUO RealEstate H&DG",
                    "RGUFSRSG.RUO RealEstate SvcG",
                    "RGUFSRTG.RUO EquityREIT:RetG",
                    "RGUFSSBG.RUO SecBrk&Svc Grw",
                    "RGUFSTIG.RUO EquityREIT:TimG",
                    "RGUHSBTG.RUO Biotec Grw",
                    "RGUHSHCG.RUO HlthC:Misc Grw",
                    "RGUHSHFG.RUO HC Fac Grw",
                    "RGUHSHMG.RUO HC MgtSvc Grw",
                    "RGUHSHSG.RUO HCare Svc Grw",
                    "RGUHSMDG.RUO M&DIns&Sp Grw",
                    "RGUHSMEG.RUO Med Eq Grw",
                    "RGUHSMSG.RUO Med Svc Grw",
                    "RGUHSPHG.RUO Phrm Grw",
                    "RGUMSALG.RUO Aluminum Grw",
                    "RGUMSBMG.RUO BldgMatl Grw",
                    "RGUMSCCG.RUO Bld:ClCtr Grw",
                    "RGUMSCDG.RUO Chem:Dvfd Grw",
                    "RGUMSCMG.RUO Cement Grw",
                    "RGUMSCPG.RUO Cont&Pkg Grw",
                    "RGUMSCRG.RUO Copper Grw",
                    "RGUMSCSG.RUO Chem Spec Grw",
                    "RGUMSDMG.RUO DvfMt&Prc Grw",
                    "RGUMSFPG.RUO ForestPrd Grw",
                    "RGUMSFTG.RUO Fertiliz Grw",
                    "RGUMSGLG.RUO Glass Grw",
                    "RGUMSGOG.RUO Gold Grw",
                    "RGUMSMDG.RUO Met&MinDvf Grw",
                    "RGUMSMFG.RUO MetFabric Grw",
                    "RGUMSPAG.RUO Paper Grw",
                    "RGUMSPCG.RUO Paint&Coat Grw",
                    "RGUMSPLG.RUO Plastics Grw",
                    "RGUMSPMG.RUO PrcMt&Min Grw",
                    "RGUMSRWG.RUO RfWal&Plm Grw",
                    "RGUMSSTG.RUO Steel Grw",
                    "RGUMSSYG.RUO SynFib&Chm Grw",
                    "RGUMSTPG.RUO Text Prod Grw",
                    "RGUPSAIG.RUO Air Trans Grw",
                    "RGUPSASG.RUO Aerospace Grw",
                    "RGUPSBOG.RUO BOSupHR&Cons GR",
                    "RGUPSCLG.RUO CommSvcR&L Grw",
                    "RGUPSCNG.RUO Cons Grw",
                    "RGUPSCVG.RUO CoVeh&Prt Grw",
                    "RGUPSDOG.RUO DvfMfgOps Grw",
                    "RGUPSECG.RUO Eng&CtrSv Grw",
                    "RGUPSENG.RUO EnvMn&SecSvc GR",
                    "RGUPSFBG.RUO FmBlkPrSv Grw",
                    "RGUPSITG.RUO IntlTrd&DvLg GR",
                    "RGUPSMAG.RUO Mach:Ag Grw",
                    "RGUPSMEG.RUO Mach:Eng Grw",
                    "RGUPSMHG.RUO Mch:Cn&Hn Grw",
                    "RGUPSMIG.RUO Mach: Indu Grw",
                    "RGUPSMSG.RUO Mach:Spec Grw",
                    "RGUPSMTG.RUO Mach:Tool Grw",
                    "RGUPSOEG.RUO OffSup&Eq Grw",
                    "RGUPSPDG.RUO PrdDr:Misc Grw",
                    "RGUPSPTG.RUO PwrTranEq Grw",
                    "RGUPSRAG.RUO Railroads Grw",
                    "RGUPSRLG.RUO RailroadEq Grw",
                    "RGUPSSEG.RUO SciIn:Elc Grw",
                    "RGUPSSGG.RUO ScInGg&Mt Grw",
                    "RGUPSSHG.RUO Shipping Grw",
                    "RGUPSSIG.RUO SciICt&Fl Grw",
                    "RGUPSSPG.RUO SciInPCtrl Grw",
                    "RGUPSTKG.RUO Truckers Grw",
                    "RGUPSTMG.RUO Trans Misc Grw",
                    "RGUSSAFG.RUO AgFsh&Ran Grw",
                    "RGUSSBDG.RUO Bev:Br&Ds Grw",
                    "RGUSSDGG.RUO Drg&GrcCh Grw",
                    "RGUSSFGG.RUO Fru&GrnPrc Grw",
                    "RGUSSFOG.RUO Foods Grw",
                    "RGUSSGSD.RUO Bev:SftDr Grw",
                    "RGUSSMCG.RUO MiscConsSt Grw",
                    "RGUSSPCG.RUO Pers Care Grw",
                    "RGUSSSUG.RUO Sugar Grw",
                    "RGUSSTOG.RUO Tobacco Grw",
                    "RGUTSCMG.RUO Comm Tech Grw",
                    "RGUTSCSG.RUO CpSvSw&Sy Grw",
                    "RGUTSCTG.RUO Comp Tech Grw",
                    "RGUTSECG.RUO Elec Comp Grw",
                    "RGUTSEEG.RUO ElecEnt Grw",
                    "RGUTSELG.RUO Elec Grw",
                    "RGUTSPRG.RUO ProdTechEq Grw",
                    "RGUTSSCG.RUO SemiCn&Cp Grw",
                    "RGUTSTEG.RUO Tele Eq Grw",
                    "RGUTSTMG.RUO Tech:Misc Grw",
                    "RGUUSUEG.RUO Util:Elec Grw",
                    "RGUUSUGG.RUO Util:GasDi Grw",
                    "RGUUSUMG.RUO Util: Misc Grw",
                    "RGUUSUTG.RUO Util:Tele Grw",
                    "RGUUSUWG.RUO Util:Water Grw"
                };
            }

            else if (name == "RUJ Groups")
            {
                list = new string[]
                {
                    "RUJ.RUJ INDEX",
                    "RUJ SECTORS:RUJ SECTORS",
                    "RUJ INDUSTRIES:RUJ INDUSTRIES"
                };
            }
            else if (name == "RUJ SECTORS")
            {
                list = new string[]
                {
                    "RGUSDSV.RUJ CONS DISC",
                    "RGUSSSV.RUJ CONS STAPLES",
                    "RGUSESV.RUJ ENERGY",
                    "RGUSFSV.RUJ FINANCIALS",
                    "RGUSHSV.RUJ HEALTH CARE",
                    "RGUSMSV.RUJ MATERIALS",
                    "RGUSPSV.RUJ PRODUCER DUR",
                    "RGUSTSV.RUJ TECHNOLOGY",
                    "RGUSUSV.RUJ UTILITIES"
                };
            }
            else if (name == "RUJ INDUSTRIES")
            {
                list = new string[]
                {
                    "RGUDSAAV.RUJ Ad Age Val",
                    "RGUDSAPV.RUJ AutoPrts Val",
                    "RGUDSASV.RUJ Auto Svc Val",
                    "RGUDSAUV.RUJ Automob Val",
                    "RGUDSCEV.RUJ ConsElect Val",
                    "RGUDSCGV.RUJ Cas&Gamb Val",
                    "RGUDSCMV.RUJ ConSvcMisc Val",
                    "RGUDSCSV.RUJ Cosmetics Val",
                    "RGUDSCTV.RUJ CabTV Svc Val",
                    "RGUDSDMV.RUJ Dvsfd Med Val",
                    "RGUDSDRV.RUJ Dvsfd Ret Val",
                    "RGUDSENV.RUJ Entertain Val",
                    "RGUDSESV.RUJ Edu Svc Val",
                    "RGUDSFUV.RUJ FuPar&Cem Val",
                    "RGUDSHAV.RUJ HholdAppl Val",
                    "RGUDSHBV.RUJ HomeBldg Val",
                    "RGUDSHEV.RUJ HHEq&Pro Val",
                    "RGUDSHFV.RUJ Hhld Furn Val",
                    "RGUDSHOV.RUJ Htel/Mtel Val",
                    "RGUDSLTV.RUJ Leis Time Val",
                    "RGUDSLXV.RUJ Lux Items Val",
                    "RGUDSMHV.RUJ Mfg Hous Val",
                    "RGUDSPCV.RUJ Prnt&CpySv Val",
                    "RGUDSPHV.RUJ Photograph Val",
                    "RGUDSPUV.RUJ Publishing Val",
                    "RGUDSRBV.RUJ Radio&TVBr Val",
                    "RGUDSRCV.RUJ R&LSv:Con Val",
                    "RGUDSRTV.RUJ Restaurant Val",
                    "RGUDSRVV.RUJ RV & Boats Val",
                    "RGUDSSFV.RUJ StorageFac Val",
                    "RGUDSSRV.RUJ Spec Ret  Val",
                    "RGUDSTXV.RUJ TxtAp&Shoe Val",
                    "RGUDSTYV.RUJ Toys Val",
                    "RGUDSVCV.RUJ Vend&Cat Val",
                    "RGUESAEV.RUJ AlterEner Val",
                    "RGUESCOV.RUJ Coal Val",
                    "RGUESEQV.RUJ Energy Eq Val",
                    "RGUESGPV.RUJ Gas Pipe Val",
                    "RGUESOCV.RUJ Oil:CrudPr Val",
                    "RGUESOFV.RUJ OffshrDri Val",
                    "RGUESOIV.RUJ Oil Integ Val",
                    "RGUESORV.RUJ Oil:Ref&Mkt VA",
                    "RGUESOWV.RUJ OilWlEq&Sv Val",
                    "RGUFSAMV.RUJ AstMgt&Cs Val",
                    "RGUFSBKV.RUJ Bnks:Dvfd Val",
                    "RGUFSBSV.RUJ BkSvThrfMt Val",
                    "RGUFSCLV.RUJ Cons Lend Val",
                    "RGUFSDFV.RUJ DvfFnlSvc Val",
                    "RGUFSDRV.RUJ DvsfREstateActV",
                    "RGUFSEDV.RUJ EquityREIT:DivV",
                    "RGUFSEFV.RUJ EquityREIT:InfV",
                    "RGUFSEHV.RUJ EquityREIT:HcaV",
                    "RGUFSEIV.RUJ EquityREIT:IndV",
                    "RGUFSELV.RUJ EquityREIT:L&RV",
                    "RGUFSEOV.RUJ EquityREIT:OffV",
                    "RGUFSERV.RUJ EquityREIT:ResV",
                    "RGUFSESV.RUJ EquityREIT:StoV",
                    "RGUFSFDV.RUJ FinDat&Sy Val",
                    "RGUFSFMV.RUJ ComFn&Mtg Val",
                    "RGUFSILV.RUJ Ins:Life Val",
                    "RGUFSIMV.RUJ Ins:MulLn Val",
                    "RGUFSIPV.RUJ Ins:ProCa Val",
                    "RGUFSMCV.RUJ MortgREIT:ComV",
                    "RGUFSMDV.RUJ MortgREIT:DivV",
                    "RGUFSMSV.RUJ MortgREIT:ResV",
                    "RGUFSOSV.RUJ EquityREIT:OSpV",
                    "RGUFSRHV.RUJ RealEstate H&DV",
                    "RGUFSRSV.RUJ RealEstate SvcV",
                    "RGUFSRTV.RUJ EquityREIT:RetV",
                    "RGUFSSBV.RUJ SecBrk&Svc Val",
                    "RGUFSTIV.RUJ EquityREIT:TimV",
                    "RGUHSBTV.RUJ Biotec Val",
                    "RGUHSHCV.RUJ HlthC:Misc Val",
                    "RGUHSHFV.RUJ HC Fac Val",
                    "RGUHSHMV.RUJ HC MgtSvc Val",
                    "RGUHSHSV.RUJ HCare Svc Val",
                    "RGUHSMDV.RUJ M&DIns&Sp Val",
                    "RGUHSMEV.RUJ Med Eq Val",
                    "RGUHSMSV.RUJ Med Svc Val",
                    "RGUHSPHV.RUJ Phrm Val",
                    "RGUMSALV.RUJ Aluminum Val",
                    "RGUMSBMV.RUJ BldgMatl Val",
                    "RGUMSCCV.RUJ Bld:ClCtr Val",
                    "RGUMSCDV.RUJ Chem:Dvfd Val",
                    "RGUMSCMV.RUJ Cement Val",
                    "RGUMSCPV.RUJ Cont&Pkg Val",
                    "RGUMSCRV.RUJ Copper Val",
                    "RGUMSCSV.RUJ Chem Spec Val",
                    "RGUMSDMV.RUJ DvfMt&Prc Val",
                    "RGUMSFPV.RUJ ForestPrd Val",
                    "RGUMSFTV.RUJ Fertiliz Val",
                    "RGUMSGLV.RUJ Glass Val",
                    "RGUMSGOV.RUJ Gold Val",
                    "RGUMSMDV.RUJ Met&MinDvf Val",
                    "RGUMSMFV.RUJ MetFabric Val",
                    "RGUMSPAV.RUJ Paper Val",
                    "RGUMSPCV.RUJ Paint&Coat Val",
                    "RGUMSPLV.RUJ Plastics Val",
                    "RGUMSPMV.RUJ PrcMt&Min Val",
                    "RGUMSRWV.RUJ RfWal&Plm Val",
                    "RGUMSSTV.RUJ Steel Val",
                    "RGUMSSYV.RUJ SynFib&Chm Val",
                    "RGUMSTPV.RUJ Text Prod Val",
                    "RGUPSAIV.RUJ Air Trans Val",
                    "RGUPSASV.RUJ Aerospace Val",
                    "RGUPSBOV.RUJ BOSupHR&Cons VA",
                    "RGUPSCLV.RUJ CommSvcR&L Val",
                    "RGUPSCNV.RUJ Cons Val",
                    "RGUPSCVV.RUJ CoVeh&Prt Val",
                    "RGUPSDOV.RUJ DvfMfgOps Val",
                    "RGUPSECV.RUJ Eng&CtrSv Val",
                    "RGUPSENV.RUJ EnvMn&SecSvc VA",
                    "RGUPSFBV.RUJ FmBlkPrSv Val",
                    "RGUPSITV.RUJ IntlTrd&DvLg VA",
                    "RGUPSMAV.RUJ Mach:Ag Val",
                    "RGUPSMEV.RUJ Mach:Eng Val",
                    "RGUPSMHV.RUJ Mch:Cn&Hn Val",
                    "RGUPSMIV.RUJ Mach: Indu Val",
                    "RGUPSMSV.RUJ Mach:Spec Val",
                    "RGUPSMTV.RUJ Mach:Tool Val",
                    "RGUPSOEV.RUJ OffSup&Eq Val",
                    "RGUPSPDV.RUJ PrdDr:Misc Val",
                    "RGUPSPTV.RUJ PwrTranEq Val",
                    "RGUPSRAV.RUJ Railroads Val",
                    "RGUPSRLV.RUJ RailroadEq Val",
                    "RGUPSSEV.RUJ SciIn:Elc Val",
                    "RGUPSSGV.RUJ ScInGg&Mt Val",
                    "RGUPSSHV.RUJ Shipping Val",
                    "RGUPSSIV.RUJ SciICt&Fl Val",
                    "RGUPSSPV.RUJ SciInPCtrl Val",
                    "RGUPSTKV.RUJ Truckers Val",
                    "RGUPSTMV.RUJ Trans Misc Val",
                    "RGUSSAFV.RUJ AgFsh&Ran Val",
                    "RGUSSBDV.RUJ Bev:Br&Ds Val",
                    "RGUSSDGV.RUJ Drg&GrcCh Val",
                    "RGUSSFGV.RUJ Fru&GrnPrc Val",
                    "RGUSSFOV.RUJ Foods Val",
                    "RGUSSMCV.RUJ MiscConsSt Val",
                    "RGUSSPCV.RUJ Pers Care Val",
                    "RGUSSSDV.RUJ Bev:SftDr Val",
                    "RGUSSSUV.RUJ Sugar Val",
                    "RGUSSTOV.RUJ Tobacco Val",
                    "RGUTSCMV.RUJ Comm Tech Val",
                    "RGUTSCSV.RUJ CpSvSw&Sy Val",
                    "RGUTSCTV.RUJ Comp Tech Val",
                    "RGUTSECV.RUJ Elec Comp Val",
                    "RGUTSEEV.RUJ ElecEnt Val",
                    "RGUTSELV.RUJ Elec Val",
                    "RGUTSPRV.RUJ ProdTechEq Val",
                    "RGUTSSCV.RUJ SemiCn&Cp Val",
                    "RGUTSTEV.RUJ Tele Eq Val",
                    "RGUTSTMV.RUJ Tech:Misc Val",
                    "RGUUSUEV.RUJ Util:Elec Val",
                    "RGUUSUGV.RUJ Util:GasDi Val",
                    "RGUUSUMV.RUJ Util: Misc Val",
                    "RGUUSUTV.RUJ Util:Tele Val",
                    "RGUUSUWV.RUJ Util:Water Val"
                };
            }

            else if (name == "R2500 Groups")
            {
                list = new string[]
                {
                    "R2500.R2500 INDEX",
                    "R2500 SECTORS:R2500 SECTORS"
                };
            }
            else if (name == "R2500 SECTORS")
            {
                list = new string[] {
                    "RGU25D.R2500 CONS DISC",
                    "RGU25S.R2500 CONS STAPLES",
                    "RGU25E.R2500 ENERGY",
                    "RGU25F.R2500 FINANCIALS",
                    "RGU25H.R2500 HEALTH CARE",
                    "RGU25M.R2500 MATERIALS",
                    "RGU25P.R2500 PRODUCER DUR",
                    "RGU25T.R2500 TECHNOLOGY",
                    "RGU25U.R2500 UTILITIES" };
            }
            else if (name == "R2500G Groups")
            {
                list = new string[]
                {
                    "R2500G.R2500G INDEX",
                    "R2500G SECTORS:R2500G SECTORS"
                };
            }
            else if (name == "R2500 SECTORS")
            {
                list = new string[] {
                    "RGU25DG.R2500G CONS DISC",
                    "RGU25SG.R2500G CONS STAPLES",
                    "RGU25EG.R2500G ENERGY",
                    "RGU25FG.R2500G FINANCIALS",
                    "RGU25HG.R2500G HEALTH CARE",
                    "RGU25MG.R2500G MATERIALS",
                    "RGU25PG.R2500G PRODUCER DUR",
                    "RGU25TG.R2500G TECHNOLOGY",
                    "RGU25UG.R2500G UTILITIES" };
            }
            else if (name == "R2500V Groups")
            {
                list = new string[]
                {
                    "R2500V.R2500V INDEX",
                    "R2500V SECTORS:R2500V SECTORS"
                };
            }
            else if (name == "R2500V SECTORS")
            {
                list = new string[] {
                    "RGU25DV.R2500V CONS DISC",
                    "RGU25SV.R2500V CONS STAPLES",
                    "RGU25EV.R2500V ENERGY",
                    "RGU25FV.R2500V FINANCIALS",
                    "RGU25HV.R2500V HEALTH CARE",
                    "RGU25MV.R2500V MATERIALS",
                    "RGU25PV.R2500V PRODUCER DUR",
                    "RGU25TV.R2500V TECHNOLOGY",
                    "RGU25UV.R2500V UTILITIES" };
            }
            else if (name == "RAG Groups")
            {
                list = new string[]
                {
                    "RAG.RAG INDEX",
                    "RAG SECTORS:RAG SECTORS"
                };
            }
            else if (name == "RAG SECTORS")
            {
                list = new string[] {
                    "RGUSDG.RAG CONS DISC",
                    "RGUSSG.RAG CONS STAPLES",
                    "RGUSEG.RAG ENERGY",
                    "RGUSFG.RAG FINANCIALS",
                    "RGUSHG.RAG HEALTH CARE",
                    "RGUSMG.RAG MATERIALS",
                    "RGUSPG.RAG PRODUCER DUR",
                    "RGUSTG.RAG TECHNOLOGY",
                    "RGUSUG.RAG UTILITIES" };
            }
            else if (name == "RAV Groups")
            {
                list = new string[]
                {
                    "RAV.RAV INDEX",
                    "RAV SECTORS:RAV SECTORS"
                };
            }
            else if (name == "RAV SECTORS")
            {
                list = new string[]
                {
                    "RGUSDV.RAV CONS DISC",
                    "RGUSSV.RAV CONS STAPLES",
                    "RGUSEV.RAV ENERGY",
                    "RGUSFV.RAV FINANCIALS",
                    "RGUSHV.RAV HEALTH CARE",
                    "RGUSMV.RAV MATERIALS",
                    "RGUSPV.RAV PRODUCER DUR",
                    "RGUSTV.RAV TECHNOLOGY",
                    "RGUSUV.RAV UTILITIES"
                };
            }
            else if (name == "RMC Groups")
            {
                list = new string[]
                {
                    "RMC.RMC INDEX",
                    "RMC SECTORS:RMC SECTORS"
                };
            }
            else if (name == "RMC SECTORS")
            {
                list = new string[]
                {
                    "RGUMCD.RMC CONS DISC",
                    "RGUMCS.RMC CONS STAPLES",
                    "RGUMCE.RMC ENERGY",
                    "RGUMCF.RMC FINANCIALS",
                    "RGUMCH.RMC HEALTH CARE",
                    "RGUMCM.RMC MATERIALS",
                    "RGUMCP.RMC PRODUCER DUR",
                    "RGUMCT.RMC TECHNOLOGY",
                    "RGUMCU.RMC UTILITIES"
                };
            }
            else if (name == "RDG Groups")
            {
                list = new string[]
                {
                    "RDG.RDG INDEX",
                    "RDG SECTORS:RDG SECTORS"
                };
            }
            else if (name == "RDG SECTORS")
            {
                list = new string[]
                {
                    "RGUMCDG.RDG CONS DISC",
                    "RGUMCSG.RDG CONS STAPLES",
                    "RGUMCEG.RDG ENERGY",
                    "RGUMCFG.RDG FINANCIALS",
                    "RGUMCHG.RDG HEALTH CARE",
                    "RGUMCMG.RDG MATERIALS",
                    "RGUMCPG.RDG PRODUCER DUR",
                    "RGUMCTG.RDG TECHNOLOGY",
                    "RGUMCUG.RDG UTILITIES"
                };
            }
            else if (name == "RMV Groups")
            {
                list = new string[]
                {
                    "RMV.RMV INDEX",
                    "RMV SECTORS:RMV SECTORS"
                };
            }
            else if (name == "RMV SECTORS")
            {
                list = new string[]
                {
                    "RGUMCDV.RMV CONS DISC",
                    "RGUMCSV.RMV CONS STAPLES",
                    "RGUMCEV.RMV ENERGY",
                    "RGUMCFV.RMV FINANCIALS",
                    "RGUMCHV.RMV HEALTH CARE",
                    "RGUMCMV.RMV MATERIALS",
                    "RGUMCPV.RMV PRODUCER DUR",
                    "RGUMCTV.RMV TECHNOLOGY",
                    "RGUMCUV.RMV UTILITIES"
                };
            }
            else if (name == "RMICRO Groups")
            {
                list = new string[]
                {
                    "RMICRO.RMICRO INDEX",
                    "RMICRO SECTORS:RMICRO SECTORS"
                };
            }
            else if (name == "RMICRO SECTORS")
            {
                list = new string[]
                {
                    "RGMICD.RMICRO CONS DISC",
                    "RGMICS.RMICRO CONS STAPLES",
                    "RGMICE.RMICRO ENERGY",
                    "RGMICF.RMICRO FINANCIALS",
                    "RGMICH.RMICRO HEALTH CARE",
                    "RGMICM.RMICRO MATERIALS",
                    "RGMICP.RMICRO PRODUCER DUR",
                    "RGMICT.RMICRO TECHNOLOGY",
                    "RGMICU.RMICRO UTILITIES"
                };
            }
            else if (name == "RMICROG Groups")
            {
                list = new string[]
                {
                    "RMICROG.RMICROG INDEX",
                    "RMICROG SECTORS:RMICROG SECTORS"

                };
            }
            else if (name == "RMICROG SECTORS")
            {
                list = new string[]
                {
                    "RGMICDG.RMICROG CONS DISC",
                    "RGMICSG.RMICROG CONS STAPLES",
                    "RGMICEG.RMICROG ENERGY",
                    "RGMICFG.RMICROG FINANCIALS",
                    "RGMICHG.RMICROG HEALTH CARE",
                    "RGMICMG.RMICROG MATERIALS",
                    "RGMICPG.RMICROG PRODUCER DUR",
                    "RGMICTG.RMICROG TECHNOLOGY",
                    "RGMICUG.RMICROG UTILITIES"
                };
            }
            else if (name == "RMICROV Groups")
            {
                list = new string[]
                {
                    "RMICROV.RMICROV INDEX",
                    "RMICROV SECTORS:RMICROV SECTORS"
                };
            }
            else if (name == "RMICROV SECTORS")
            {
                list = new string[]
                {
                    "RGMICDV.RMICROV CONS DISC",
                    "RGMICSV.RMICROV CONS STAPLES",
                    "RGMICEV.RMICROV ENERGY",
                    "RGMICFV.RMICROV FINANCIALS",
                    "RGMICHV.RMICROV HEALTH CARE",
                    "RGMICMV.RMICROV MATERIALS",
                    "RGMICPV.RMICROV PRODUCER DUR",
                    "RGMICTV.RMICROV TECHNOLOGY",
                    "RGMICUV.RMICROV UTILITIES"
                };
            }

            else if (name == "RTY Groups")
            {
                list = new string[] 
                {
                    "RTY.RTY INDEX",
                    "RTY SECTORS:RTY SECTORS",
                    "RTY INDUSTRIES:RTY INDUSTRIES"
                };
            }
            else if (name == "RTY SECTORS")
            {
                list = new string[] 
                {
                    "RGUSDS.RTY CONS DISC",
                    "RGUSSS.RTY CONS STAPLES",
                    "RGUSES.RTY ENERGY",
                    "RGUSFS.RTY FINANCIALS",
                    "RGUSHS.RTY HEALTH CARE",
                    "RGUSMS.RTY MATERIALS",
                    "RGUSPS.RTY PRODUCER DUR",
                    "RGUSTS.RTY TECHNOLOGY",
                    "RGUSULS.RIY UTILITIES"
                };
            }
            else if (name == "RTY INDUSTRIES")
            {
                list = new string[] 
                {
                    "RGUSDSAA.RTY AD AGENCIES",
                    "RGUSPSAS.RTY AEROSPACE",
                    "RGUSSSAF.RTY AG FISH & RNCH",
                    "RGUSPSAI.RTY AIR TRANSPORT",
                    "RGUSESAE.RTY ALTER ENERGY",
                    "RGUSMSAL.RTY ALUMINUM",
                    "RGUSFSAM.RTY ASSET MGT & CUST",
                    "RGUSDSAP.RTY AUTO PARTS",
                    "RGUSDSAS.RTY AUTO SVC",
                    "RGUSFSBK.RTY BANKS DVSFD",
                    "RGUSSSBD.RTY BEV BRW & DSTLR",
                    "RGUSSSSD.RTY BEV SOFT DRNK",
                    "RGUSHSBT.RTY BIOTEC",
                    "RGUSMSCC.RTY BLDG CLIMATE CTRL",
                    "RGUSFSBS.RTY BNK SVG THRF MRT",
                    "RGUSPSBO.RTY BO SUP HR & CONS",
                    "RGUSMSBM.RTY BUILDING MATL",
                    "RGUSDSCT.RTY CABLE TV SVC",
                    "RGUSDSCG.RTY CASINOS & GAMB",
                    "RGUSMSCM.RTY CEMENT",
                    "RGUSMSCS.RTY CHEM SPEC",
                    "RGUSMSCD.RTY CHEM DVFSD",
                    "RGUSTSCS.RTY CMP SVC SFW & SYS",
                    "RGUSESCO.RTY COAL",
                    "RGUSFSFM.RTY COMM SVC",
                    "RGUSPSCS.RTY COMM FIN & MORT",
                    "RGUSPSCL.RTY COMM SVC RN",
                    "RGUSTSCM.RTY COMM TECH",
                    "RGUSPSCV.RTY COMM VEH & PRTS",
                    "RGUSTSCT.RTY COMPUTER TECH",
                    "RGUSPSCN.RTY CONS",
                    "RGUSDSCM.RTY CONS SVC  MISC",
                    "RGUSFSCL.RTY CONSUMER LEND",
                    "RGUSDSCE.RTY CONSUMER ELECT",
                    "RGUSMSCP.RTY CONTAINER & PKG",
                    "RGUSMSCR.RTY COPPER",
                    "RGUSDSCS.RTY COSMETICS",
                    "RGUSSSDG.RTY DRUG & GROC CHN",
                    "RGUSFSDF.RTY DVSFD FNCL SVC",
                    "RGUSDSDM.RTY DVSFD MEDIA",
                    "RGUSDSDR.RTY DVSFD RETAIL",
                    "RGUSMSDM.RTY DVSFD MAT & PROC",
                    "RGUSPSDO.RTY DVSFD MFG OPS",
                    "RGUSDSES.RTY EDUCATION SVC",
                    "RGUSTSEC.RTY ELECT COMP",
                    "RGUSTSEE.RTY ELECT ENT",
                    "RGUSTSEL.RTY ELECTRONICS",
                    "RGUSESEQ.RTY ENERGY EQ",
                    "RGUSPSEC.RTY ENG & CONTR SVC",
                    "RGUSDSEN.RTY ENTERTAINMENT",
                    "RGUSPSEN.RTY ENV MN & SEC SVC",
                    "RGUSMSFT.RTY FERTILIZERS",
                    "RGUSFSFD.RTY FINCL DATA & SYS",
                    "RGUSSSFO.RTY FOODS",
                    "RGUSMSFP.RTY FOREST PROD",
                    "RGUSPSFB.RTY FRM & BLK PRNT SVC",
                    "RGUSDSFU.RTY FUN PARLOR & CEM",
                    "RGUSESGP.RTY GAS PIPELINE",
                    "RGUSMSGL.RTY GLASS",
                    "RGUSMSGO.RTY GOLD",
                    "RGUSDSHE.RTY HHLD EQP & PROD",
                    "RGUSDSHF.RTY HHLD FURN",
                    "RGUSHSHF.RTY HLTH CARE FAC",
                    "RGUSHSHS.RTY HLTH CARE SVC",
                    "RGUSHSHM.RTY HLTH C MGT SVC",
                    "RGUSHSHC.RTY HLTH C MISC",
                    "RGUSDSHB.RTY HOMEBUILDING",
                    "RGUSDSHO.RTY HOTEL/MOTEL",
                    "RGUSDSHA.RTY HOUSEHOLD APPL",
                    "RGUSFSIL.RTY INS LIFE",
                    "RGUSFSIM.RTY INS MULTI-LN",
                    "RGUSFSIP.RTY INS PROP-CAS",
                    "RGUSPSIT.RTY INTL TRD & DV LG",
                    "RGUSDSLT.RTY LEISURE TIME",
                    "RGUSDSLX.RTY LUXURY ITEMS",
                    "RGUSPSMI.RTY MACH INDU",
                    "RGUSPSMA.RTY MACH AG",
                    "RGUSPSME.RTY MACH ENGINES",
                    "RGUSPSMS.RTY MACH SPECIAL",
                    "RGUSPSMH.RTY MCH CONS & HNDL",
                    "RGUSHSMD.RTY MD & DN INS & SUP",
                    "RGUSHSME.RTY MED EQ",
                    "RGUSHSMS.RTY MED SVC",
                    "RGUSMSMD.RTY MET & MIN DVFSD",
                    "RGUSMSMF.RTY METAL FABRIC",
                    "RGUSDSMH.RTY MFG HOUSING",
                    "RGUSSSMC.RTY MISC CONS STAPLE",
                    "RGUSPSOE.RTY OFF SUP & EQ",
                    "RGUSESOF.RTY OFFSHORE DRILL",
                    "RGUSESOI.RTY OIL INTEGRATE",
                    "RGUSESOC.RTY OIL CRUDE PROD",
                    "RGUSESOR.RTY OIL REF & MKT",
                    "RGUSESOW.RTY OIL WELL EQ & SVC",
                    "RGUSMSPC.RTY PAINT & COATING",
                    "RGUSMSPA.RTY PAPER",
                    "RGUSSSPC.RTY PERSONAL CARE",
                    "RGUSHSPH.RTY PHRM",
                    "RGUSMSPL.RTY PLASTICS",
                    "RGUSPSPT.RTY PREC MET & MINL",
                    "RGUSMSPM.RTY PRINT & COPY SVC",
                    "RGUSDSPC.RTY PROD DUR MISC",
                    "RGUSPSPD.RTY PRODUCT TECH EQ",
                    "RGUSTSPR.RTY PUBLISHING",
                    "RGUSDSPU.RTY PWR TRANSM EQ",
                    "RGUSDSRB.RTY RADIO & TV BROAD",
                    "RGUSPSRL.RTY RAILROAD EQ",
                    "RGUSPSRA.RTY RAILROADS",
                    "RGUSFSRE.RTY REAL ESTATE",
                    "RGUSFSRI.RTY REIT",
                    "RGUSDSRT.RTY RESTAURANTS",
                    "RGUSMSRW.RTY ROOF WALL & PLUM",
                    "RGUSDSRC.RTY RT & LS SVC CONS",
                    "RGUSDSRV.RTY RV & BOATS",
                    "RGUSPSSI.RTY SCI INS CTL & FLT",
                    "RGUSPSSP.RTY SCI INS POL CTRL",
                    "RGUSPSSG.RTY SCI INST GG & MTR",
                    "RGUSPSSE.RTY SCI INSTR ELEC",
                    "RGUSFSSB.RTY SEC BRKG & SVC",
                    "RGUSTSSC.RTY SEMI COND & COMP",
                    "RGUSPSSH.RTY SHIPPING",
                    "RGUSDSSR.RTY SPEC RET",
                    "RGUSMSST.RTY STEEL",
                    "RGUSMSSY.RTY SYN FIBR & CHEM",
                    "RGUSTSTM.RTY TECHNO MISC",
                    "RGUSTSTE.RTY TELE EQ",
                    "RGUSDSTX.RTY TEXT APP & SHOES",
                    "RGUSMSTP.RTY TEXTILE PROD",
                    "RGUSSSTO.RTY TOBACCO",
                    "RGUSDSTY.RTY TOYS",
                    "RGUSPSTM.RTY TRANS MISC",
                    "RGUSPSTK.RTY TRUCKERS",
                    "RGUSUSUM.RTY UTIL  MISC",
                    "RGUSUSUE.RTY UTIL ELEC",
                    "RGUSUSUG.RTY UTIL GAS DIST",
                    "RGUSUSUT.RTY UTIL TELE",
                    "RGUSUSUW.RTY UTIL WATER"
                };
            }
            else if (name == "SML Groups")
            {
                list = new string[] 
                {
                    "SML.SML INDEX",
                    "SML SECTORS:SML SECTORS",
                    "SML INDUSTRIES:SML INDUSTRIES",
                    "SML SUB-INDUSTRIES:SML SUB-INDUSTRIES"
                };
            }
            else if (name == "SML SECTORS")
            {
                list = new string[] 
                {
                    "S6COND.SML CONS DISC",
                    "S6CONS.SML CONS STAPLES",
                    "S6ENRS.SML ENERGY",
                    "S6FINL.SML FINANCIALS",
                    "S6HLTH.SML HEALTH CARE",
                    "S6INDU.SML INDUSTRIALS",
                    "S6INFT.SML INFO TECH",
                    "S6MATR.SML MATERIALS",
                    "S6TELS.SML TELECOM",
                    "S6UTIL.SML UTILITIES"
                };
            }
            else if (name == "SML INDUSTRIES")
            {
                list = new string[] 
                {
                    "S6AUCO.SML AUTO & COMP",
                    "S6BANKX.SML BANKS",
                    "S6CODU.SML CAPITAL GOODS",
                    "S6COMS.SML COMM & PROF",
                    "S6CPGS.SML CON DUR & AP",
                    "S6DIVF.SML DIV FINANCE",
                    "S6ENRSX.SML ENERGY",
                    "S6FDBT.SML FOOD/STAPLES",
                    "S6FDSR.SML FOOD BEV TOB",
                    "S6HCES.SML HC EQUIP",
                    "S6HOTR.SML CONS SERV",
                    "S6HOUS.SML HOUSEHOLD PROD",
                    "S6INSU.SML INSURANCE",
                    "S6MATRX.SML MATERIALS",
                    "S6MEDA.SML MEDIA",
                    "S6PHRM.SML PHRM BIO & LIFE",
                    "S6REAL.SML REAL ESTATE",
                    "S6RETL.SML RETAILING",
                    "S6SSEQX.SML SEMI & EQP",
                    "S6SFTW.SML SOFTWARE & SVCS",
                    "S6TECH.SML TECH HW & EQP",
                    "S6TELSX.SML TELECOM SVCS",
                    "S6TRAN.SML TRANSPORT",
                    "S6UTILX.SML UTILTIES"
                };
            }
            else if (name == "SML SUB-INDUSTRIES")
            {
                list = new string[] 
                {
                    "S6AEROX.SML AERO & DEF",
                    "S6AIRFX.SML AIR FRT & LOG",
                    "S6AIRLX.SML AIRLINES",
                    "S6AUTC.SML AUTO COMP",
                    "S6AUTO.SML AUTOMOBILES",
                    "S6BEVG.SML BEVERAGES",
                    "S6BIOTX.SML BIOTECH",
                    "S6BUILX.SML BLDG PRODS",
                    "S6CAPM.SML CAPITAL MKTS",
                    "S6CBNK.SML COMM BANKS",
                    "S6CFINX.SML CONS FINANCE",
                    "S6CHEM.SML CHEMICALS",
                    "S6CMPE.SML COMPUTERS & PER",
                    "S6COMM.SML COMMUNICATION EQP",
                    "S6COMSX.SML COMMERCIAL SRBS",
                    "S6CONP.SML CONTAINER & PKG",
                    "S6CSTEX.SML CONST & ENG",
                    "S6CSTMX.SML CONST MATERIAL",
                    "S6DCON.SML DIVERSIFIED SRVC",
                    "S6DISTX.SML DISBRIBUTORS",
                    "S6DIVT.SML DIV TEL SVC",
                    "S6DVFS.SML DIV FIN SVC",
                    "S6ELEIX.SML ELECTRONIC EQUP",
                    "S6ELEQ.SML ELECTRICAL EQUP",
                    "S6ELUTX.SML ELECTRIC UTL",
                    "S6ENRE.SML ENERGY EQUP & SV",
                    "S6FDPR.SML FOOD PROD IND",
                    "S6FDSRX.SML FOOD & STAPLES RET",
                    "S6GASUX.SML GAS UTL",
                    "S6HCEQ.SML HC EQUP & SUP",
                    "S6HCPS.SML HC PROVIDERS SVC",
                    "S6HCTEX.SML HC TECHNOLOGY",
                    "S6HODU.SML HOUSEHOLD DURABLES",
                    "S6HOPRX.SML HOUSELHOLD PROD",
                    "S6HOTRX.SML HOTELS REST & LEIS",
                    "S6INCR.SML INTERNET CATALOG",
                    "S6INDCX.SML INDUSTRIAL CONGL",
                    "S6INSSX.SML INTERNET SOFTWARE",
                    "S6INSUX.SML INSUURANCE IND",
                    "S6ITSV.SML IT SERV IND",
                    "S6LEIS.SML LEISURE EQUP",
                    "S6LSTSX.SML LIFE SCI IND",
                    "S6MACH.SML MACHINERY",
                    "S6MARIX.SML MARINE",
                    "S6MDREX.SML RE MGM",
                    "S6MEDAX.SML MEDIA",
                    "S6METL.SML METAL & MIN",
                    "S6MRET.SML MULTILINE RET",
                    "S6MUTIX.SML MULTI UTL",
                    "S6OILG.SML OIL GAS FUEL",
                    "S6PAFO.SML PAPER FORSET PROD",
                    "S6PERSX.SML PERSONAL PROD",
                    "S6PHARX.SML PHARMA",
                    "S6PRSV.SML PROF SRVS",
                    "S6REITS.SML RE INV TRUSTS",
                    "S6ROAD.SML ROARD & RAIL",
                    "S6SOFT.SML SOFTWARE",
                    "S6SPRE.SML SPECIALTY RET",
                    "S6SSEQ.SML SEMICOND & EQUP",
                    "S6TEXA.SML TXTL & APPRL",
                    "S6THMFX.SML THRIFTS & MORT",
                    "S6TOBAX.SML TOBACCO",
                    "S6TRADX.SML TRADING CO & DIS",
                    "S6WATUX.SML WATER UTL",
                    "S6WIREX.SML WIRELESS TELECOM"
                };
            }
            else if (name == "SPR Groups")
            {
                list = new string[] 
                {
                    "SPR.SPR INDEX",
                    "SPR SECTORS:SPR SECTORS",
                    "SPR INDUSTRIES:SPR INDUSTRIES",
                    "SPR SUB-INDUSTRIES:SPR SUB-INDUSTRIES"
                };
            }
            else if (name == "SPR SECTORS")
            {
                list = new string[] 
                {
                    "S15COND.SPR CONS DISC",
                    "S15CONS.SPR CONS STAPLES",
                    "S15ENRS.SPR ENERGY",
                    "S15FINL.SPR FINANCIALS",
                    "S15HLTH.SPR HEALTH CARE",
                    "S15INDU.SPR INDUSTRIALS",
                    "S15INFT.SPR INFO TECH",
                    "S15MATR.SPR MATERIALS",
                    "S15TELS.SPR TELECOM",
                    "S15UTIL.SPR UTILITIES"
                };
            }
            else if (name == "SPR INDUSTRIES")
            {
                list = new string[] 
                {
                    "S15AUCO.SPR AUTO & COMP",
                    "S15BANKX.SPR BANKS",
                    "S15CODU.SPR CAPITAL GOODS",
                    "S15COMS.SPR COMM & PROF",
                    "S15CPGS.SPR CON DUR & AP",
                    "S15DIVF.SPR DIV FINANCE",
                    "S15ENRSX.SPR ENERGY",
                    "S15FDBT.SPR FOOD/STAPLES",
                    "S15FDSR.SPR FOOD BEV TOB",
                    "S15HCES.SPR HC EQUIP",
                    "S15HOTR.SPR CONS SERV",
                    "S15HOUS.SPR HOUSEHOLD PROD",
                    "S15INSU.SPR INSURANCE",
                    "S15MATRX.SPR MATERIALS",
                    "S15MEDA.SPR MEDIA",
                    "S15PHRM.SPR PHRM BIO & LIFE",
                    "S15REAL.SPR REAL ESTATE",
                    "S15RETL.SPR RETAILING",
                    "S15SSEQX.SPR SEMI & EQP",
                    "S15SFTW.SPR SOFTWARE & SVCS",
                    "S15TECH.SPR TECH HW & EQP",
                    "S15TELSX.SPR TELECOM SVCS",
                    "S15TRAN.SPR TRANSPORT",
                    "S15UTILX.SPR UTILTIES"
                };
            }
            else if (name == "SPR SUB-INDUSTRIES")
            {
                list = new string[] 
                {
                    "S15AEROX.SPR AERO & DEF",
                    "S15AIRFX.SPR AIR FRT & LOG",
                    "S15AIRLX.SPR AIRLINES",
                    "S15AUTC.SPR AUTO COMP",
                    "S15AUTO.SPR AUTOMOBILES",
                    "S15BEVG.SPR BEVERAGES",
                    "S15BIOTX.SPR BIOTECH",
                    "S15BUILX.SPR BLDG PRODS",
                    "S15CAPM.SPR CAPITAL MKTS",
                    "S15CBNK.SPR COMM BANKS",
                    "S15CFINX.SPR CONS FINANCE",
                    "S15CHEM.SPR CHEMICALS",
                    "S15CMPE.SPR COMPUTERS & PER",
                    "S15COMM.SPR COMMUNICATION EQP",
                    "S15COMSX.SPR COMMERCIAL SRBS",
                    "S15CONP.SPR CONTAINER & PKG",
                    "S15CSTEX.SPR CONST & ENG",
                    "S15CSTMX.SPR CONST MATERIAL",
                    "S15DCON.SPR DIVERSIFIED SRVC",
                    "S15DISTX.SPR DISBRIBUTORS",
                    "S15DIVT.SPR DIV TEL SVC",
                    "S15DVFS.SPR DIV FIN SVC",
                    "S15ELEIX.SPR ELECTRONIC EQUP",
                    "S15ELEQ.SPR ELECTRICAL EQUP",
                    "S15ELUTX.SPR ELECTRIC UTL",
                    "S15ENRE.SPR ENERGY EQUP & SV",
                    "S15FDPR.SPR FOOD PROD IND",
                    "S15FDSRX.SPR FOOD & STAPLES RET",
                    "S15GASUX.SPR GAS UTL",
                    "S15HCEQ.SPR HC EQUP & SUP",
                    "S15HCPS.SPR HC PROVIDERS SVC",
                    "S15HCTEX.SPR HC TECHNOLOGY",
                    "S15HODU.SPR HOUSEHOLD DURABLES",
                    "S15HOPRX.SPR HOUSELHOLD PROD",
                    "S15HOTRX.SPR HOTELS REST & LEIS",
                    "S15INCR.SPR INTERNET CATALOG",
                    "S15INDCX.SPR INDUSTRIAL CONGL",
                    "S15INSSX.SPR INTERNET SOFTWARE",
                    "S15INSUX.SPR INSUURANCE IND",
                    "S15IPPEX.SPR INDEP PWR PROD",
                    "S15ITSV.SPR IT SERV IND",
                    "S15LEIS.SPR LEISURE EQUP",
                    "S15LSTSX.SPR LIFE SCI IND",
                    "S15MACH.SPR MACHINERY",
                    "S15MARIX.SPR MARINE",
                    "S15MDREX.SPR RE MGM",
                    "S15MEDAX.SPR MEDIA",
                    "S15METL.SPR METAL & MIN",
                    "S15MRET.SPR MULTILINE RET",
                    "S15MUTIX.SPR MULTI UTL",
                    "S15OFFEX.SPR OFFICE ELECT",
                    "S15OILG.SPR OIL GAS FUEL",
                    "S15PAFO.SPR PAPER FORSET PROD",
                    "S15PERSX.SPR PERSONAL PROD",
                    "S15PHARX.SPR PHARMA",
                    "S15PRSV.SPR PROF SRVS",
                    "S15REITS.SPR RE INV TRUSTS",
                    "S15ROAD.SPR ROARD & RAIL",
                    "S15SOFT.SPR SOFTWARE",
                    "S15SPRE.SPR SPECIALTY RET",
                    "S15SSEQ.SPR SEMICOND & EQUP",
                    "S15TEXA.SPR TXTL & APPRL",
                    "S15THMFX.SPR THRIFTS & MORT",
                    "S15TOBAX.SPR TOBACCO",
                    "S15TRADX.SPR TRADING CO & DIS",
                    "S15WATUX .SPR WATER UTL",
                    "S15WIREX .SPR WIRELESS TELECOM"
                };
            }
            else if (name == "SPX Groups")
            {
                list = new string[] 
                {
                    "SPX.SPX INDEX",
                    "SPX SECTORS:SPX SECTORS",
                    "SPX INDUSTRIES:SPX INDUSTRIES",
                    "SPX SUB-INDUSTRIES:SPX SUB-INDUSTRIES"
                };
            }
            else if (name == "SPX SECTORS")
            {
                list = new string[] 
                {
                    "S5COND.SPX CONS DISC",
                    "S5CONS.SPX CONS STAPLES",
                    "S5ENRS.SPX ENERGY",
                    "S5FINL.SPX FINANCIALS",
                    "S5HLTH.SPX HEALTH CARE",
                    "S5INDU.SPX INDUSTRIALS",
                    "S5INFT.SPX INFO TECH",
                    "S5MATR.SPX MATERIALS",
                    "S5TELS.SPX TELECOM",
                    "S5UTIL.SPX UTILITIES"
                };
            }
            else if (name == "SPX INDUSTRIES")
            {
                list = new string[] {
                    "S5AUCO.SPX AUTO & COMP",
                    "S5BANKX.SPX BANKS",
                    "S5CODU.SPX CAPITAL GOODS",
                    "S5COMS.SPX COMM & PROF",
                    "S5CPGS.SPX CON DUR & AP",
                    "S5DIVF.SPX DIV FINANCE",
                    "S5ENRSX.SPX ENERGY",
                    "S5FDBT.SPX FOOD/STAPLES",
                    "S5FDSR.SPX FOOD BEV TOB",
                    "S5HCES.SPX HC EQUIP",
                    "S5HOTR.SPX CONS SERV",
                    "S5HOUS.SPX HOUSEHOLD PROD",
                    "S5INSU.SPX INSURANCE",
                    "S5MATRX.SPX MATERIALS",
                    "S5MEDA.SPX MEDIA",
                    "S5PHRM.SPX PHRM BIO & LIFE",
                    "S5REAL.SPX REAL ESTATE",
                    "S5RETL.SPX RETAILING",
                    "S5SSEQX.SPX SEMI & EQP",
                    "S5SFTW.SPX SOFTWARE & SVCS",
                    "S5TECH.SPX TECH HW & EQP",
                    "S5TELSX.SPX TELECOM SVCS",
                    "S5TRAN.SPX TRANSPORT",
                    "S5UTILX.SPX UTILTIES" };
            }
            else if (name == "SPX SUB-INDUSTRIES")
            {
                list = new string[] {
                    "S5AEROX.SPX AERO & DEF",
                    "S5AIRFX.SPX AIR FRT & LOG",
                    "S5AIRLX.SPX AIRLINES",
                    "S5AUTC.SPX AUTO COMP",
                    "S5AUTO.SPX AUTOMOBILES",
                    "S5BEVG.SPX BEVERAGES",
                    "S5BIOTX.SPX BIOTECH",
                    "S5BUILX.SPX BLDG PRODS",
                    "S5CAPM.SPX CAPITAL MKTS",
                    "S5CBNK.SPX COMM BANKS",
                    "S5CFINX.SPX CONS FINANCE",
                    "S5CHEM.SPX CHEMICALS",
                    "S5CMPE.SPX COMPUTERS & PER",
                    "S5COMM.SPX COMMUNICATION EQP",
                    "S5COMSX.SPX COMMERCIAL SRBS",
                    "S5CONP.SPX CONTAINER & PKG",
                    "S5CSTEX.SPX CONST & ENG",
                    "S5CSTMX.SPX CONST MATERIAL",
                    "S5DCON.SPX DIVERSIFIED SRVC",
                    "S5DISTX.SPX DISBRIBUTORS",
                    "S5DIVT.SPX DIV TEL SVC",
                    "S5DVFS.SPX DIV FIN SVC",
                    "S5ELEIX.SPX ELECTRONIC EQUP",
                    "S5ELEQ.SPX ELECTRICAL EQUP",
                    "S5ELUTX.SPX ELECTRIC UTL",
                    "S5ENRE.SPX ENERGY EQUP & SV",
                    "S5FDPR.SPX FOOD PROD IND",
                    "S5FDSRX.SPX FOOD & STAPLES RET",
                    "S5GASUX.SPX GAS UTL",
                    "S5HCEQ.SPX HC EQUP & SUP",
                    "S5HCPS.SPX HC PROVIDERS SVC",
                    "S5HCTEX.SPX HC TECHNOLOGY",
                    "S5HODU.SPX HOUSEHOLD DURABLES",
                    "S5HOPRX.SPX HOUSELHOLD PROD",
                    "S5HOTRX.SPX HOTELS REST & LEIS",
                    "S5INCR.SPX INTERNET CATALOG",
                    "S5INDCX.SPX INDUSTRIAL CONGL",
                    "S5INSSX.SPX INTERNET SOFTWARE",
                    "S5INSUX.SPX INSUURANCE IND",
                    "S5IPPEX.SPX INDEP PWR PROD",
                    "S5ITSV.SPX IT SERV IND",
                    "S5LEIS.SPX LEISURE EQUP",
                    "S5LSTSX.SPX LIFE SCI IND",
                    "S5MACH.SPX MACHINERY",
                    "S5MDREX.SPX RE MGM",
                    "S5MEDAX.SPX MEDIA",
                    "S5METL.SPX METAL & MIN",
                    "S5MRET.SPX MULTILINE RET",
                    "S5MUTIX.SPX MULTI UTL",
                    "S5OFFEX.SPX OFFICE ELECT",
                    "S5OILG.SPX OIL GAS FUEL",
                    "S5PAFO.SPX PAPER FORSET PROD",
                    "S5PERSX.SPX PERSONAL PROD",
                    "S5PHARX.SPX PHARMA",
                    "S5PRSV.SPX PROF SRVS",
                    "S5REITS.SPX RE INV TRUSTS",
                    "S5ROAD.SPX ROARD & RAIL",
                    "S5SOFT.SPX SOFTWARE",
                    "S5SPRE.SPX SPECIALTY RET",
                    "S5SSEQ.SPX SEMICOND & EQUP",
                    "S5TEXA.SPX TXTL & APPRL",
                    "S5THMFX.SPX THRIFTS & MORT",
                    "S5TOBAX.SPX TOBACCO",
                    "S5TRADX.SPX TRADING CO & DIS",
                    "S5WIREX .SPX WIRELESS TELECOM" };
            }
            else if (name == "CANADA")
            {
                list = new string[] {
                    "SPTSX | INDUSTRIES:SPTSX Groups",
                    //"SPTSX60",
                    //"TS300",
                    //"TXEQ"
                    };
            }
            else if (name == "SPTSX Groups")
            {
                list = new string[] {
                    "SPTSX.SPTSX INDEX",
                    "STCOND.SPTSX CONSUMER DISC",
                    "STCONS.SPTSX CONSUMER STAPLES",
                    "STENRS.SPTSX ENERGY",
                    "STFINL.SPTSX FINANCIALS",
                    "STHLTH.SPTSX HEALTH CARE",
                    "STINDU.SPTSX INDUSTRIALS",
                    "STINFT.SPTSX INFO TECH",
                    "STMATR.SPTSX MATERIALS",
                    "STTELS.SPTSX TELECOM",
                    "STUTIL.SPTSX UTILITIES" };
            }
            else if (name == "AMERICAS")
            {
                list = new string[] {
                    "AMERICAS | INDUSTRIES:AMERICAS Groups",
                 };
            }
            else if (name == "AMERICAS Groups")
            {
                list = new string[]
                {
                    "BWORLDUS.BWORLDUS INDEX",
                    "AMERICAS INDUSTRIES:AMERICAS INDUSTRIES"
                };
            }
            else if (name == "EMEA")
            {
                list = new string[] {
                    "EMEA | INDUSTRIES:EMEA Groups",
                 };
            }
            else if (name == "EMEA Groups")
            {
                list = new string[]
                {
                    "BWORLDEU.BWORLDEU INDEX",
                    "EMEA INDUSTRIES:EMEA INDUSTRIES"
                };
            }
            else if (name == "ASIA PACIFIC")
            {
                list = new string[] {
                    "ASIA PACIFIC | INDUSTRIES:ASIA PACIFIC Groups",
                 };
            }
            else if (name == "ASIA PACIFIC Groups")
            {
                list = new string[]
                {
                    "BWORLDPR.BWORLDPR INDEX",
                    "ASIA PACIFIC INDUSTRIES:ASIA PACIFIC INDUSTRIES"
                };
            }
            else if (name == "MEXICO")
            {
                list = new string[] { "MEXBOL", "INMEX", "IMC30" };
            }
            else if (name == "ARGENTINA")
            {
                list = new string[] { "MERVAL", "MAR", "IBG" };
            }
            else if (name == "BRAZIL")
            {
                list = new string[] { "IBOV", "IBX", "IBOVIEE", "ITEL", "IGCX", "IVBX2", "IBX50" };
            }
            else if (name == "CHILE")
            {
                list = new string[] { "IPSA", "INTER10", "CHILE65", "CHLRGCAP", "CHSMLCAP" };
            }
            else if (name == "EUROPEAN INDICES")
            {
                list = new string[] {
                    "BE500 | INDUSTRIES:BE500 Groups",
                    "SXXE | INDUSTRIES:SXXE Groups",
                    "SXXP | INDUSTRIES:SXXP Groups",
                    "E300 | INDUSTRIES:E300 Groups",
                    "SPEU | INDUSTRIES:SPEU Groups"
                    };
            }
            else if (name == "BE500 Groups")
            {
                list = new string[] {
                    "BE500.BE500 INDEX",
                    "BEAUTOP.BE500 Auto Parts",
                    "BEAUTOS.BE500 Autos",
                    "BEBANKS.BE500 Bank & Fin",
                    "BEBEVGS.BE500 Beverages",
                    "BEBULDM.BE500 Build Materials",
                    "BECHEMC.BE500 Chemicals",
                    "BECOMPH.BE500 Comp HW",
                    "BECOMPS.BE500 Computer Service",
                    "BECOMSV.BE500 Commercial Serv",
                    "BECONSP.BE500 Consumer Prods",
                    "BECONST.BE500 Const & Engin",
                    "BEDIVRX.BE500 Diversified ",
                    "BEELECT.BE500 Electric",
                    "BEENRGX.BE500 Energy",
                    "BEFOOD.BE500 Food",
                    "BEFOODR.BE500 Food Retail",
                    "BEFURNI.BE500 Furn & Apparel",
                    "BEGAS.BE500 Gas",
                    "BEHLTHC.BE500 Health Care",
                    "BEINDUP.BE500 Indust Products",
                    "BEINSUR.BE500 Insurance",
                    "BEINVST.BE500 Investment Svcs",
                    "BEMACHN.BE500 Machinery",
                    "BEMANUF.BE500 Manufacturing",
                    "BEMEDIA.BE500 Media",
                    "BEMETAL.BE500 Metal & Mine",
                    "BEPAPER.BE500 Paper & Forest",
                    "BEPHARM.BE500 Pharmaceutical",
                    "BEREALE.BE500 Real Estate",
                    "BERETAI.BE500 Retail",
                    "BESTEEL.BE500 Steel",
                    "BETELEE.BE500 Telecom Equip",
                    "BETELES.BE500 Telecom Ser",
                    "BETOBAC.BE500 Tobacco",
                    "BETRANS.BE500 Transportation",
                    "BETRAVL.BE500 Travel & Lesiure",
                    "BEWATER.BE500 Water"
                    };
            }
            else if (name == "SXXE Groups")
            {
                list = new string[] {
                    "SXXE.SXXE INDEX",
                    "SXAE.SXXE Auto & Parts",
                    "SX7E.SXXE Bank",
                    "SXPE.SXXE Basic Research",
                    "SX4E.SXXE Chemical",
                    "SXOE.SXXE Const & Materials",
                    "SXFE.SXXE Financial Services",
                    "SX3E.SXXE Food & Bev",
                    "SXDE.SXXE Healthcare",
                    "SXNE.SXXE Industrial Goods",
                    "SXIE.SXXE Insurance",
                    "SXME.SXXE Media",
                    "SXEE.SXXE Oil&Gas",
                    "SXQE.SXXE Personal & House",
                    "SX86E.SXXE Re-Estate",
                    "SXRE.SXXE Retail Price",
                    "SX8E.SXXE Technology",
                    "SXKE.SXXE Telecom",
                    "SXTE.SXXE Travel & Leisure",
                    "SX6E.SXXE Utilities" };
            }
            else if (name == "SXXP Groups")
            {
                list = new string[] {
                    "SXXP.SXXP INDEX",
                    "SXAP.SXXP Auto & Parts",
                    "SX7P.SXXP Bank",
                    "SXPP.SXXP Basic Research",
                    "SX4P.SXXP Chemical",
                    "SXOP.SXXP Const & Materials",
                    "SXFP.SXXP Financial Services",
                    "SX3P.SXXP Food & Bev",
                    "SXDP.SXXP Healthcare",
                    "SXNP.SXXP Industrial Goods",
                    "SXIP.SXXP Insurance",
                    "SXMP.SXXP Media",
                    "SXEP.SXXP Oil&Gas",
                    "SXQP.SXXP Personal & House",
                    "SX86P.SXXP Re-Estate",
                    "SXRP.SXXP Retail Price",
                    "SX8P.SXXP Technology",
                    "SXKP.SXXP Telecom",
                    "SXTP.SXXP Travel & Leisure",
                    "SX6P.SXXP Utilities"};
            }
            else if (name == "E300 Groups")
            {
                list = new string[] {
                    "E300.E300 INDEX",
                    "E300,E300 INDEX",
                    "E3AERO,E300 AEROSPACE",
                    "E3ALNRG,E300 ALT ENERGY",
                    "E3AUTO,E300 AUTO & PARTS",
                    "E3BANK,E300 BANK",
                    "E3BEVG,E300 BEVERAGE",
                    "E3CHEM,E300 CHEMICALS",
                    "E3CONS,E300 CONST",
                    "E3ELEC,E300 ELEC",
                    "E3ELTR,E300 ELECTONIC ELECTt",
                    "E3OTHR,E300 FINANCIAL",
                    "E3FOOD,E300 FOOD",
                    "E3FDRT,E300 FOOD & DRUG",
                    "E3UTLO,E300 GAS WATER & MULTI UTL",
                    "E3DIND,E300 GEN INDUSTRIALS",
                    "E3RETG,E300 GENl RETAILERS",
                    "E3HLTH,E300 HEALTH CARE EQP",
                    "EFHOUGE,E300 HOUSEHOLD GOODS",
                    "E3HOUS,E300 HOUSING",
                    "E3ENGN,E300 IND ENG",
                    "E3INSU,E300 INSURANCE",
                    "E3INVC,E300 INVEST INST",
                    "E3LIFE,E300 LIFE INS",
                    "E3MEDA,E300 MEDIA",
                    "E3METL,E300 METALS",
                    "E3MNG,E300 MINING",
                    "EFMOBTE,E300 MOBILE TELECOM",
                    "EFNEIIE,E300 NonEQUITY INV",
                    "E3OILG,E300 OIL & GAS",
                    "EFOESDE,E300 OIL EQP SVC",
                    "E3PAPR,E300 PAPER",
                    "E3PERC,E300 PERSONAL GOODS",
                    "E3PHRM,E300 PHARM & BIOTECH",
                    "E3REISV,E300 REAL ESTATE INV",
                    "E3REITS,E300 REITS",
                    "E3SOFT,E300 SOFTWARE & COMP",
                    "E3SUPP,E300 SUPPORT SVC",
                    "E3INFT,E300 TECHNOLOGY H/W",
                    "E3TELE,E300 TELECOM",
                    "E3TOBC,E300 TOBACCO",
                    "E3TRAN,E300 TRANSPORTATION",
                    "E3LEIS,E300 TRAVELl & LEISURE" };
            }
            else if (name == "SPEU Groups")
            {
                list = new string[] {
                    "SPEU.SPEU INDEX",
                    "SEUCOND.SPEU Cons Disc",
                    "SEUCONS.SPEU Cons Staples",
                    "SEUENRS.SPEU Energy",
                    "SEUFINL.SPEU Financials",
                    "SEUHLTH.SPEU Health Care",
                    "SEUINDU.SPEU Industrials",
                    "SEUINFT.SPEU Info Tech",
                    "SEUMATR.SPEU Material",
                    "SEUTELS.SPEU Telecom",
                    "SEUUTIL.SPEU Utilities"};
            }
            else if (name == "UK")
            {
                list = new string[] {
                    "UKX",
                    "AXX | INDUSTRIES:AXX Groups",
                    "ASX | INDUSTRIES:ASX Groups",
                    "ASXE",
                    "FTNSX",
                    "FTLIX",
                    "FTHIX",
                    "MCX",
                    "NMX | INDUSTRIES:NMX Groups",
                    "MCIX",
                    "RGGB",
                    "RGGBG",
                    "RGGBV",
                    "RGGBL",
                    "RGGBLG",
                    "RGGBLV",
                    "RGGBM",
                    "RGGBMG",
                    "RGGBM",
                    "RGGBS",
                    "RGGBSG",
                    "RGGBSV ",
                    "SMX",
                    "T1X",
                    "TASX",
                    "TMS1" };
            }
            else if (name == "AXX Groups")
            {
                list = new string[] {
                    "AXX.AXX INDEX",
                    "AXAUP.AXX AUTO & PARTS",
                    "AXBANK.AXX BANKS",
                    "AXBASR.AXX BASIC RESOURCES",
                    "AXCHEM.AXX CHEMICALS",
                    "AXCONS.AXX CONST MATERIALS",
                    "AXFIN.AXX FINANCIAL SERV",
                    "AXFOB.AXX FOOD & BEVERAGE",
                    "AXHEAL.AXX HEALTH CARE",
                    "AXIGS.AXX IND GOOD & SRV",
                    "AXINSU.AXX INSURANCE",
                    "AXMEDI.AXX MEDIA",
                    "AXOIG.AXX OIL & GAS",
                    "AXPERS.AXX PERS & HOUSEHOLD",
                    "AXRETA.AXX RETAIL",
                    "AXREAL.AXX REAL ESTATE",
                    "AXTECH.AXX TECHNOLOGY",
                    "AXTELE.AXX TELECOM",
                    "AXTRAV.AXX TRAVEL LEISURE",
                    "AXUTIL.AXX UTILITIES"  };
            }
            else if (name == "ASX Groups")
            {
                list = new string[] {
                    "ASX.ASX INDEX",
                    "FAAERO.ASX AEROSPACE & DEF",
                    "FAALNRG.ASX ALT ENERGY",
                    "FAAUTO.ASX AUTO PARTS",
                    "FABANK.ASX BANKS",
                    "FABEVG.ASX BEVERAGES",
                    "FACHEM.ASX CHEMICAL",
                    "FACONS.ASX CONSTRUCTION MAT",
                    "FAELEC.ASX ELECTRICITY",
                    "FAELTR.ASX ELECT/ELECT EQUIP",
                    "FAINVC.ASX EGY INVST INSTR",
                    "FATELE.ASX FIXED LINE TELE",
                    "FAOTHR.ASX FINANCIAL SRVS",
                    "FAFDRT.ASX FOOD DRUG RETAIL",
                    "FAFOOD.ASX FOOD PRODUCERS",
                    "FAPAPR.ASX FORESTRY PAPERS",
                    "FADIND.ASX GEN INDUSTRIALS",
                    "FARETG.ASX GEN RETAILERS",
                    "FAUTLO.ASX GAS WATER UTL",
                    "FAHLTH.ASX HEALTH CARE EQUIP",
                    "FXHOUGE.ASX HOUSE GD & HM",
                    "FAENGN.ASX INDURTRIAL ENG",
                    "FAMETL.ASX INDUST METAL & MINE",
                    "FATRAN.ASX INDUST TRANSPORT",
                    "FAHOUS.ASX LEISURE GOODS",
                    "FALIFE.ASX LIFE INS",
                    "FAMEDA.ASX MEDIA",
                    "FAMNG.ASX MINING",
                    "FXMOBTE.ASX MOBILE TELECOM",
                    "FAINSU.ASX NON LIFE INS",
                    "FXOESDE.ASX OIL EQUIP SERV",
                    "FAOILG.ASX OIL GAS PROD",
                    "FAPHRM.ASX PHARM & BIOTECH",
                    "FAPERC.ASX PERSONAL GOODS",
                    "FAREITS.ASX REITS",
                    "FAREISV.ASX RE INVST SRV",
                    "FASOFT.ASX SOFTWARE COMP SVRS",
                    "FASUPP.ASX SUPPORT SRVS",
                    "FAINFT.ASX TECH HARDWARE",
                    "FATOBC.ASX TOBACCO",
                    "FALEIS.ASX TRAVEL LEISURE" };
            }
            else if (name == "NMX Groups")
            {
                list = new string[] {
                    "NMX.NMX INDEX",
                    "F3AERO.NMX AEROSPACE & DEF",
                    "F3ALNRG.NMX ALT ENERGY",
                    "F3AUTO.NMX AUTO & PART",
                    "F3BANK.NMX BANKS",
                    "F3BEVG.NMX BEVERAGES",
                    "F3CHEM.NMX CHEMCIALS",
                    "F3CONS.NMX CONST MATERIAL",
                    "F3ELEC.NMX ELECTRICITY",
                    "F3ELTR.NMX ELEC-ELEC EQUIP",
                    "F3INVC.NMX EQT INVEST",
                    "F3FDRT.NMX FOOD DRUG",
                    "F3TELE.NMX FIXED LINE TELECOM",
                    "F3APAPR.NMX FRSTRY-PAPER",
                    "F3FOOD.NMX FOOD PROD",
                    "F3OTHR.NMX FINANCIAL SERV",
                    "F3UTLO.NMX GAS WTR & MULTI",
                    "F3DIND.NMX GEN INDUSTRIAL",
                    "F3RETG.NMX GEN RETAIL",
                    "F3HLTH.NMX HC EQUIP",
                    "F3HOUGE.NMX HOUSE GD & H CON",
                    "F3ENGN.NMX INDUSTRY ENG",
                    "F3METL.NMX INDUST METAL & MINING",
                    "F3TRAN.NMX INDUSTRY TRANSPORT",
                    "F3LIFE.NMX LIFE INS",
                    "F3INSU.NMX NON LIFE INSUR",
                    "F3MEDA.NMX MEDIA",
                    "F3MNG.NMX MINING",
                    "F3MOBTE.NMX MOBILE TEL",
                    "F3OESDE.NMX OIL EQ SVS",
                    "F3OILG.NMX OIL & GAS",
                    "F3PERC.NMX PERSONAL",
                    "F3PHRM.NMX PHARM & BIOTECH",
                    "F3REITS.NMX REITS",
                    "F3REISV.NMX RE INVEST SRV",
                    "F3SOFT.NMX SOFTWARE/PC SRV",
                    "F3SUPP.NMX SUPPORT SERV",
                    "F3INFT.NMX TECH HRD & EQUIP",
                    "F3TOBC.NMX TOBACCO",
                    "F3LEIS.NMX TRAVEL & LEISURE" };
            }
            else if (name == "FRANCE")
            {
                list = new string[] { "CAC", "CM100", "CN20", "CS90", "MS190", "PAX", "SBF250 | INDUSTRIES:SBF250 Groups" };
            }
            else if (name == "SBF250 Groups")
            {
                list = new string[] {
                    "SBF250.SBF250 INDEX",
                    "EPBASE.SBF250 BASIC MATERIAL",
                    "EPBCYC.SBF250 CONSUMER GOODS",
                    "EPSCYC.SBF250 CONSUMER SRV",
                    "EPSFIN.SBF250 FINANCIALS",
                    "EPBNCY.SBF250 HEALTH CARE",
                    "EPGENE.SBF250 INDUSTRIALS",
                    "EPRESS.SBF250 OIL & GAS",
                    "EPTECI.SBF250 TECHNOLOGY",
                    "EPSNCY.SBF250 TELECOM",
                    "EPSPUB.SBF250 UTLITIES" };
            }
            else if (name == "IRELAND")
            {
                list = new string[] { "ISEQ | INDUSTRIES:ISEQ Groups" };
            }
            else if (name == "ISEQ Groups")
            {
                list = new string[] {
                    "ISEQ.ISEQ INDEX",
                    "ISEF.ISEQ FINANCIAL" };
            }
            else if (name == "GERMANY")
            {
                list = new string[] { "CDAX", "CLXP", "DAX", "HDAX", "MDAX", "MIDP", "NMDP", "PXAP", "TDXP" };
            }
            else if (name == "SPAIN")
            {
                list = new string[] { "IBEX", "MADX | INDUSTRIES:MADX Groups", "ES30" };
            }
            else if (name == "MADX Groups")
            {
                list = new string[] {
                    "MADX.MADX INDEX",
                    "MAB2.MADX BASIC MATERIALS",
                    "MAB3.MADX CONSUMER GOODS",
                    "MAS4.MADX CONSUMER SRVS",
                    "MAS5.MADX FINANCIAL",
                    "MAE1.MADX PTRL & PWR",
                    "MAS6T.MADX TECH & TELECOM" };
            }
            else if (name == "SWITZERLAND")
            {
                list = new string[] {
                    "SMI",
                    "SPI",
                    "SPIMLC",
                    "SPI19",
                    "SPI20",
                    "SBC100",
                    "SPI21",
                    "SSIP",
                    "CH30",
                    "SPIEXX",
                    "SMIM",
                    "SLI" };
            }
            else if (name == "ITALY")
            {
                list = new string[] {
                    "FTSEMIB",
                    "ITLMS | INDUSTRIES:ITLMS Groups",
                    "ITMC",
                    "ITSTAR",
                    "IT30" };
            }
            else if (name == "ITLMS Groups")
            {
                list = new string[] {
                    "ITLMS.ITLMS INDEX",
                    "IT1000.ITLMS BASIC MATERIAL",
                    "IT3000.ITLMS CONSUMER GDS",
                    "IT5000.ITLMS CONSUMER SER",
                    "IT8000.ITLMS FINANCIALS",
                    "IT4000.ITLMS HEALTH CARE",
                    "IT2000.ITLMS INDUSTRIAL",
                    "IT0001.ITLMS OIL",
                    "IT9000.ITLMS TECHNOLOGY",
                    "IT6000.ITLMS TELECOM",
                    "IT7000.ITLMS UTILITIES" };
            }
            else if (name == "PORTUGAL")
            {
                list = new string[] { "BVLX | INDUSTRIES:BVLX Groups", "PSI20" };
            }
            else if (name == "BVLX Groups")
            {
                list = new string[] {
                    "BVLX.BVLX INDEX",
                    "PSIIND.BVLX BASIC MATERIALS",
                    "PSICCG.BVLX CONS GOODS",
                    "PSICSV.BVLX CONS SERVICE",
                    "PSIFIN.BVLX FINANCIALS",
                    "PSIGEN.BVLX INDUSTRIALS",
                    "PSITEC.BVLX TECHNOLOGY",
                    "PSINSV.BVLX TELECOM",
                    "PSIUTL.BVLX UTILITIES" };
            }
            else if (name == "NETHERLANDS")
            {
                list = new string[] { "AEX", "AMX" };
            }
            else if (name == "DENMARK")
            {
                list = new string[] { "KFX", "KAX | INDUSTRIES:KAX Groups" };
            }
            else if (name == "KAX Groups")
            {
                list = new string[] {
                    "KAX.KAX INDEX",
                    "CX3000PI.KAX CONS GOODS",
                    "CX5000PI.KAX CONS SERVICES",
                    "CX0001PI.KAX ENERGY",
                    "CX8000PI.KAX FINANCIALS",
                    "CX4000PI.KAX HEALTH CARE",
                    "CX2000PI.KAX INDUSTRIALS",
                    "CX1000PI.KAX MATERIALS",
                    "CX9000PI.KAX TECH",
                    "CX9000PI.KAX TELECOM",
                    "CX7000PI.KAX UTILITIES" };
            }
            else if (name == "FINLAND")
            {
                list = new string[] { "HEX | INDUSTRIES:HEX Groups", "HEX25", "HEXP" };
            }
            else if (name == "HEX Groups")
            {
                list = new string[] {
                    "HEX.HEX INDEX",
                    "HX3000PI.HEX CONS GOODS",
                    "HX5000PI.HEX CONS SERVICES",
                    "HX0001PI.HEX ENERGY",
                    "HX8000PI.HEX FINANCIALS",
                    "HX4000PI.HEX HEALTH CARE",
                    "HX2000PI.HEX INDUSTRIALS",
                    "HX1000PI.HEX MATERIALS",
                    "HX9000PI.HEX TECH",
                    "HX9000PI.HEX TELECOM",
                    "HX7000PI.HEX UTILITIES" };
            }
            else if (name == "NORWAY")
            {
                list = new string[] { "OBX", "OBXP", "OSEAX", "OSEBX", "OSEFX", "OSESX" };
            }
            else if (name == "SWEDEN")
            {
                list = new string[] { "OMX", "SBX", "SAX | INDUSTRIES:SAX Groups", "SE30" };
            }
            else if (name == "SAX Groups")
            {
                list = new string[] {
                    "SAX.SAX INDEX",
                    "SX3000PI.SAX CONS GOODS",
                    "SX5000PI.SAX CONS SERVICES",
                    "SX0001PI.SAX ENERGY",
                    "SX8000PI.SAX FINANCIALS",
                    "SX4000PI.SAX HEALTH CARE",
                    "SX2000PI.SAX INDUSTRIALS",
                    "SX1000PI.SAX MATERIALS",
                    "SX9000PI.SAX TECH",
                    "SX9000PI.SAX TELECOM",
                    "SX7000PI.SAX UTILITIES" };
            }
            else if (name == "AUSTRIA")
            {
                list = new string[] { "ATX", "WBI", "ATXPRIME" };
            }
            else if (name == "GREECE")
            {
                list = new string[] { "ASE", "FTASE", "FTSEM" };
            }
            else if (name == "POLAND")
            {
                list = new string[] { "WIG", "WIG20", "SWIG80", "MIDWIG" };
            }
            else if (name == "RUSSIA")
            {
                list = new string[] { "IMOEX", "RTSI$",  "CRTX" };
            }
            else if (name == "HUNGARY")
            {
                list = new string[] { "BUX", "CHTX" };
            }
            else if (name == "TURKEY")
            {
                list = new string[] { "XU100", "XUO30", "TR201" };
            }
            else if (name == "SOUTHAFRICA")
            {
                list = new string[] { "TOP40", "JALSH | INDUSTRIES:JALSH Groups", "INDI25" };
            }
            else if (name == "JALSH Groups")
            {
                list = new string[] {
                    "JALSH.JALSH INDEX",
                    "JAUTO.JALSH AUTOMOBILE",
                    "JBNKS.JALSH BANKS",
                    "JBEVE.JALSH BEVERAGES",
                    "JCHEM.JALSH CHEMICAL",
                    "JCBDM.JALSH CONS & MAT",
                    "JEEEQ.JALSH ELECT EQUIP",
                    "JINVC.JALSH EQY INV",
                    "JSPOF.JALSH GEN FINANCE",
                    "JGENR.JALSH GEN RETAIL",
                    "JTLSV.JALSH FIX LINE TELECOM",
                    "JFDRT.JALSH FOOD & DRUG",
                    "JFRPP.JALSH FOREST & PAPER",
                    "JFPPS.JALSH HC EQ SRV",
                    "JHLTH.JALSH HC EQUIP",
                    "JHOGD.JALSH HOUS GDS",
                    "JEGMC.JALSH INDU ENG",
                    "JSTMT.JALSH INDUTRIAL METAL",
                    "JDIND.JALSH GEN INDUSTRY",
                    "JTRNS.JALSH INDUSTRIAL TRAN",
                    "JREDVSV.JALSH INVEST SRV",
                    "JLFEA.JALSH LIFE ASR",
                    "JMDPT.JALSH MEDIA",
                    "JMNNG.JALSH MINING",
                    "JMOTE.JALSH MOBILE TELE",
                    "JINSR.JALSH NLF INS",
                    "JOLGS.JALSH OIL PRODUCT",
                    "JPBIO.JALSH PHARM & BIO",
                    "JPCHP.JALSH PERSONAL GDS",
                    "JREITS.JALSH REITS",
                    "JSSEV.JALSH SUP SVC",
                    "JLEHT.JALSH TRAVEL & LEISURE" };
            }
            else if (name == "EQYPT")
            {
                list = new string[] { "HERMES", "CASE" };
            }
            else if (name == "MOROCCO")
            {
                list = new string[] { "MOSENEW", "MOSEMDX" };
            }
            else if (name == "KUWAIT")
            {
                list = new string[] { "SECTMIND | INDUSTRIES:SECTMIND Groups", "KWSEIDX" };
            }
            else if (name == "SECTMIND Groups")
            {
                list = new string[] {
                    "SECTMIND.SECTMIND INDEX",
                    "KWTBANKC.SECTMIND BANKS",
                    "KWTGOODC.SECTMIND CONS GOODS",
                    "KWTSVCC.SECTMIND CONS SERVICE",
                    "KWTFINC.SECTMIND FINANCIAL",
                    "KWTHLTHC.SECTMIND HEALTH CARE",
                    "KWTINDC.SECTMIND INDUSTRIAL",
                    "KWTINSC.SECTMIND INSURANCE",
                    "KWTINVC.SECTMIND INVEST INSTRU",
                    "KWTMATC.SECTMIND MATERIALS",
                    "KWTOILC.SECTMIND OIL & GAS",
                    "KWTPRLLC.SECTMIND PARALLEL MKT",
                    "KWTRESTC.SECTMIND REAL ESTATE",
                    "KWTTECHC.SECTMIND TECHNOLOGY",
                    "KWTTELC.SECTMIND TELECOM",
                    "KWTUTILC.SECTMIND UTIL" };
            }
            else if (name == "BAHRAIN")
            {
                list = new string[] { "BHSEASI" };
            }
            else if (name == "JORDAN")
            {
                list = new string[] { "JOSMGNFF" };
            }
            else if (name == "LEBANON")
            {
                list = new string[] { "BLOM" };
            }
            else if (name == "OMAN")
            {
                list = new string[] { "MSM30" };
            }
            else if (name == "ISRAEL")
            {
                list = new string[] { "TA-25", "TA-125" };
            }
            else if (name == "UAE")
            {
                list = new string[] { "DFMGI | INDUSTRIES:DFMGI Groups", "ADSMI", "DUAE" };
            }
            else if (name == "DFMGI Groups")
            {
                list = new string[] {
                    "DFMGI.DFMGI INDEX",
                    "DFIBANK.DFMGI BANKS",
                    "DFCONSTP.DFMGI CONSUMER STAPLES",
                    "DFINVEST.DFMGI FINANCIAL INV",
                    "DFIINSU.DFMGI INSURANCE",
                    "DFMATERL.DFMGI MATERIALS",
                    "DFREALTY.DFMGI REAL ESTATE",
                    "DFTELCO.DFMGI TELECOM",
                    "DFTRANS.DFMGI TRANSPORT",
                    "DFUTIL.DFMGI UTILITIES" };
            }
            else if (name == "CHINA")
            {
                list = new string[] { "SHASHR", "SHBSHR", "SHCOMP | INDUSTRIES:SHCOMP Groups", "SHNCOMP", "SHSZ300 | INDUSTRIES:SHSZ300 Groups", "SICOM", "SSE180", "SSE50", "SZASHR", "SZBSHR", "SZCOMP", "SZNCOMP" };
            }
            else if (name == "SHCOMP Groups")
            {
                list = new string[] {
                    "SHCOMP.SHCOMP INDEX",
                    "SHCOMM.SHCOMP COMMERCIAL",
                    "SHCNG.SHCOMP CONGLOMERATE",
                    "SHINDU.SHCOMP INDUSTRY",
                    "SHPROP.SHCOMP PROPERTY",
                    "SHUTIL.SHCOMP UTILITY" };
            }
            else if (name == "SHSZ300 Groups")
            {
                list = new string[] {
                    "SHSZ300.SHSZ300 INDEX",
                    "SZ399911.SHSZ300 CONS DISCRETION",
                    "SZ399912.SHSZ300 CONS STAPLES",
                    "SZ399908.SHSZ300 ENERGY",
                    "SZ399914.SHSZ300 FINANCIAL",
                    "SZ399913.SHSZ300 HEALTHCARE",
                    "SZ399910.SHSZ300 INDUSTRIAL",
                    "SZ399915.SHSZ300 INFO TECH",
                    "SZ399909.SHSZ300 MATERIAL",
                    "SZ399916.SHSZ300 TELECOM",
                    "SZ399917.SHSZ300 UTILITIES" };
            }
            else if (name == "HONGKONG")
            {
                list = new string[] { "H-FIN", "HKSPGEM", "HKSPLC25", "HSCCI", "HSCEI", "HSCI | INDUSTRIES:HSCI Groups", "HSFML25", "HSHK35", "HSI | INDUSTRIES:HSI Groups", "HSML100" };
            }
            else if (name == "HSI Groups")
            {
                list = new string[] {
                    "HSI.HSI INDEX",
                    "HSC.HSI COMM INDU",
                    "HSF.HSI FINANCE",
                    "HSP.HSI PROPERTY",
                    "HSU.HSI UTILITIES" };
            }
            else if (name == "HSCI Groups")
            {
                list = new string[] {
                    "HSCI.HSCI INDEX",
                    "HSCICG.HSCI CONS GOODS",
                    "HSCICO.HSCI CONGLOMERATE",
                    "HSCIEN.HSCI ENERGY",
                    "HSCIFN.HSCI FINANCIAL",
                    "HSCIIN.HSCI INDUSTRIAL GOODS",
                    "HSCIIT.HSCI INFO TECH",
                    "HSCIMT.HSCI MATERIALS",
                    "HSCIPC.HSCI PROP & CONST",
                    "HSCISV.HSCI SERVICES",
                    "HSCITC.HSCI TELECOM",
                    "HSCIUT.HSCI UTILITIES" };
            }
            else if (name == "INDIA")
            {
                list = new string[] { "BSE100", "BSE200", "BSE500", "NIFTY", "SENSEX" };
            }
            else if (name == "JAPAN")
            {
                list = new string[] { "JSDA", "NEY", "NKY", "NKY500", "NKYJQ", "TPX | INDUSTRIES:TPX Groups", "TPX100", "TPX500", "TPXC30", "TPXL70", "TPXM400", "TPXSM", "TSE2", "TSEREIT" };
            }
            else if (name == "TPX Groups")
            {
                list = new string[] {
                        "TPX.TPX INDEX",
                        "TPNBNK.TPX BANKS",
                        "TPNCHM.TPX CHEMICALS",
                        "TPCONT.TPX CONSTRUCTION",
                        "TPELEC.TPX ELEC POWR & GAS",
                        "TPELMH.TPX ELECTRIC APPL",
                        "TPFISH.TPX FISH/AGR/FRST",
                        "TPFOOD.TPX FOODS",
                        "TPGLAS.TPX GLASS & CRMC",
                        "TPCOMM.TPX INFO & COMM",
                        "TPINSU.TPX INSURANCE",
                        "TPIRON.TPX IRON & STEEL",
                        "TPLAND.TPX LAND TRANSPORT",
                        "TPMACH.TPX MACHINERY",
                        "TPMART.TPX MARITIME TRAN",
                        "TPMETL.TPX METAL",
                        "TPMINN.TPX MINING",
                        "TPNMET.TPX NONFER METAL",
                        "TPPROD.TPX OTHER PRODUCTS",
                        "TPFINC.TPX OTHER FINC BUS",
                        "TPPHRM.TPX PHARMACEUTICAL",
                        "TPPREC.TPX PREC INSTRUMENT",
                        "TPPAPR.TPX PULP & PAPER",
                        "TPREAL.TPX REAL ESTATE",
                        "TPRETL.TPX RETAIL TRADE",
                        "TPRUBB.TPX RUBBER PRODUCTS",
                        "TPSECR.TPX SEC & CMDTY FUTR",
                        "TPSERV.TPX SERVICES",
                        "TPTRAN.TPX TRANSPORT EQUIP",
                        "TPTEXT.TPX TXTL & APPRL",
                        "TPWARE.TPX WARE&HARB TRNS",
                        "TPWSAL.TPX WHOLESALE TRADE" };
            }
            else if (name == "PHILIPPINES")
            {
                list = new string[] { "PCOMP" };
            }
            else if (name == "TAIWAN")
            {
                list = new string[] { "TWSE | INDUSTRIES:TWSE Groups", "TWOTCI", "TW50" };
            }
            else if (name == "TWSE Groups")
            {
                list = new string[] {
                    "TWSE.TWSE INDEX",
                    "TWSEAUTO.TWSE AUTO",
                    "TWSEBMC.TWSE BIO & MED",
                    "TWSECEM.TWSE CEMENT",
                    "TWSECHI.TWSE CHEMICAL",
                    "TWSECII.TWSE COMM & INTERNET",
                    "TWSECPE.TWSE COMP EQUIP",
                    "TWSECON.TWSE CONSTRUCTION",
                    "TWSEDEPT.TWSE DEPT STORES",
                    "TWSEEAW.TWSE ELEC APPLIANCE",
                    "TWSEECI.TWSE ELECTRONIC PARTS",
                    "TWSEEPD.TWSE ELECTRONIC PROD",
                    "TWSEBKI.TWSE FIN/INS",
                    "TWSEFOOD.TWSE FOOD",
                    "TWSEGLP.TWSE GLASS",
                    "TWSEISI.TWSE INFO SRV",
                    "TWSEMACH.TWSE MACHINERY",
                    "TWSEOEG.TWSE OIL & GAS",
                    "TWSEOPE.TWSE OPTOELECTRICAL",
                    "TWSEOTHR.TWSE OTHER",
                    "TWSEOEI.TWSE OTHER ELECTORNIC",
                    "TWSEPLAS.TWSE PLASTIC",
                    "TWSEPP.TWSE PULP/PAPER",
                    "TWSERUB.TWSE RUBBER",
                    "TWSESCI.TWSE SEMICONDUCTOR",
                    "TWSESTEE.TWSE STEEL",
                    "TWSETEXT.TWSE TEXTILES",
                    "TWSETOUR.TWSE TOURIST",
                    "TWSETRAN.TWSE TRANSPORT" };
            }
            else if (name == "SOUTHKOREA")
            {
                list = new string[] { "KOSPI | INDUSTRIES:KOSPI Groups", "KOSPI2", "KOSPI100", "KOSPI50", "KOSDAQ | INDUSTRIES:KOSDAQ Groups" };
            }
            else if (name == "KOSPI Groups")
            {
                list = new string[] {
                        "KOSPI.KOSPI INDEX",
                        "KOSPCHEM.CHEMICAL PROD",
                        "KOSPCOMM.COMMUNICATION",
                        "KOSPCONS.CONSTRUCTION",
                        "KOSPELEC.ELECTRIC & ELEC EQ",
                        "KOSPELGS.ELECT & GAS",
                        "KOSPFBEV.FOOD & BEVERAGE",
                        "KOSPFIN.FINANCIAL",
                        "KOSPBMET.IRON & METAL",
                        "KOSPMACH.MACHINERY",
                        "KOSPMED.MEDICINE",
                        "KOSPMDEQ.MEDICAL PREC",
                        "KOSPMISC.MISCELLANEOUS",
                        "KOSPNMET.NONMETALLIC MINRL",
                        "KOSPPPRD.PAPER & PAPER PRD",
                        "KOSPSERV.SERVICES",
                        "KOSPTXAP.TEXTILE & APPAREL",
                        "KOSPTREQ.TRANS EQUIP",
                        "KOSPTRAN.TRANSHPORT & STRGE",
                        "KOSPWHOL.WHOLESALE TRADE" };
            }
            else if (name == "KOSDAQ Groups")
            {
                list = new string[] {
                        "KOSDAQ.KOSDAQ INDEX",
                        "KOSCNST.KOSDAQ CONSTRUCTION",
                        "KOSDIST.KOSDAQ DISTRIB SRVC",
                        "KOSFINC.KOSDAQ FINANCE",
                        "KOSITCP.KOSDAQ IT COMPOSITE",
                        "KOSMANU.KOSDAQ MANUFACTURING",
                        "KOSOTHR.KOSDAQ OTHERS", };
            }
            else if (name == "VIETNAM")
            {
                list = new string[] { "VNINDEX", "VHINDEX" };
            }
            else if (name == "PAKISTAN")
            {
                list = new string[] { "KSE100", "KSE30" };
            }
            else if (name == "THAILAND")
            {
                list = new string[] { "SET | INDUSTRIES:SET Groups", "SET50" };
            }
            else if (name == "SET Groups")
            {
                list = new string[] {
                    "SET.SET INDEX",
                    "SETAGRI.SET AGRI",
                    "SETAUTO.SET AUTOMOTIVE",
                    "SETBANK.SET BANKING",
                    "SETCOM.SET COMMERCE",
                    "SETCOMMT.SET CONSTR MATERIAL",
                    "SETETRON.SET ELECTRONIC COMP",
                    "SETENERG.SET ENERGY & UTIL",
                    "SETFASH.SET FASHION",
                    "SETFIN.SET FINANCE",
                    "SETFOOD.SET FOOD & BEV",
                    "SETHELTH.SET HEALTH CARE",
                    "SETHHOLD.SET HOME & OFFICE PRD",
                    "SETIMM.SET INDU MAT & MACH",
                    "SETCOMUN.SET INFO & COMM",
                    "SETINS.SET INSURANCE",
                    "SETENTER.SET MEDIA & PUBLISH",
                    "SETMINE.SET MINING",
                    "SETPKG.SET PACKAGING",
                    "SETPAPER.SET PAPER & PRINT",
                    "SETPERS.SET PERSONAL PROD",
                    "SETPETRO.SET PETRO CHEMICAL",
                    "SETPROF.SET PROFESSIONAL SRV",
                    "SETPROP.SET PROPERTY DEV",
                    "SETPFUND.SET PROPERTY FUND",
                    "SETSTEEL.SET STEEL",
                    "SETHOT.SET TOURISM & LEISURE",
                    "SETTRANS.SET TRANSPORT & LOGIST"  };
            }
            else if (name == "INDONESIA")
            {
                list = new string[] { "JCI | INDUSTRIES:JCI Groups", "LQ45" };
            }
            else if (name == "JCI Groups")
            {
                list = new string[] {
                    "JCI.JCI INDEX",
                    "JAKAGRI.JCI AGRI",
                    "JAKBIND.JCI BASIC & CHEMICAL IND ",
                    "JAKPROP.JCI CONST PROP & RE",
                    "JAKCONS.JCI CONSUMER GOODS",
                    "JAKFIN.JCI FINANCE",
                    "JAKINFR.JCI INFRA UTILITY & TRANSPORT",
                    "JAKMINE.JCI MINING",
                    "JAKMIND.JCI MISC INDUSTRIES",
                    "JAKTRAD.JCI TRADE & SRV" };
            }
            else if (name == "SINGAPORE")
            {
                list = new string[] { "FSSTI", "FSTAS | INDUSTRIES:FSTAS Groups" };
            }
            else if (name == "FSTAS Groups")
            {
                list = new string[] {
                    "FSTAS.FSTAS INDEX",
                    "FSTBM.FSTAS BASIC MATERIAL",
                    "FSTCG.FSTAS CONSUMER GOODS",
                    "FSTCS.FSTAS CONSUMER SRV",
                    "FSTFN.FSTAS FINANCIALS",
                    "FSTHC.FSTAS HEALTH CARE",
                    "FSTIN.FSTAS INDUSTRIALS",
                    "FSTOG.FSTAS OIL & GAS",
                    "FSTTG.FSTAS TECHNOLOGY",
                    "FSTTC.FSTAS TELECOM",
                    "FSTUT.FSTAS UTILITIES" };
            }
            else if (name == "MALAYSIA")
            {
                list = new string[] { "FBMKLCI", "FBMEMAS" };
            }
            else if (name == "AUSTRALIA")
            {
                list = new string[] { "AS25", "AS31", "AS34", "AS51 | INDUSTRIES:AS51 Groups", "AS52 | INDUSTRIES:AS52 Groups", };
            }
            else if (name == "AS51 Groups")
            {
                list = new string[] {
                    "AS51.AS51 INDEX",
                    "AS51COND.AS51 CONS DISC",
                    "AS51CONS.AS51 CONS STAPLES",
                    "AS51ENGY.AS51 ENERGY",
                    "AS51FIN.AS51 FINANCIAL",
                    "AS51HC.AS51 HEALTH CARE",
                    "AS51INDU.AS51 INDUSTRIAL",
                    "AS51IT.AS51 INFO TECH",
                    "AS51MATL.AS51 MATERIALS",
                    "AS51TELE.AS51 TELECOM",
                    "AS51UTIL.AS51 UTILITIES" };
            }
            else if (name == "AS52 Groups")
            {
                list = new string[] {
                    "AS52.AS52 INDEX",
                    "AS52COND.AS52 CONS DISC",
                    "AS52CONS.AS52 CONS STAPLES",
                    "AS52ENGY.AS52 ENERGY",
                    "AS52FIN.AS52 FINANCIAL",
                    "AS52HC.AS52 HEALTH CARE",
                    "AS52INDU.AS52 INDUSTRIAL",
                    "AS52IT.AS52 INFO TECH",
                    "AS52MATL.AS52 MATERIALS",
                    "AS52TELE.AS52 TELECOM",
                    "AS52UTIL.AS52 UTILITIES" };
            }
            else if (name == "NEWZEALAND")
            {
                list = new string[] { "NZSE50FG", "NZSE10", "NZSX15G", "NZSE | INDUSTRIES:NZSE Groups" };
            }
            else if (name == "NZSE Groups")
            {
                list = new string[] {
                    "NZSE.NZSE INDEX",
                    "NZAGRI.NZSE AGRI & FISH",
                    "NZBLDM.NZSE BUILDING",
                    "NZCSMR.NZSE CONSUMER",
                    "NZENRG.NZSE ENERGY DIST",
                    "NZFINC.NZSE FINANCE",
                    "NZFOOD.NZSE FOOD & BEV",
                    "NZFRST.NZSE FORESTRY & PROD",
                    "NZINTD.NZSE INTER & DURABLES",
                    "NZINVS.NZSE INVESTMENT",
                    "NZLEIS.NZSE LEISURE & TOURISM",
                    "NZMEDI.NZSE MEDIA & TELECOM",
                    "NZMINE.NZSE MINING",
                    "NZPORT.NZSE PORTS",
                    "NZPROP.NZSE PROPERTY",
                    "NZXTS.NZSE SCI TECH CAPITAL",
                    "NZTEXT.NZSE TEXTILES & APPR",
                    "NZTRAN.NZSE TRANSPORT" };
            }
            else if (name == "QATAR")
            {
                list = new string[] { "DSM | INDUSTRIES:DSM Groups" };
            }
            else if (name == "DSM Groups")
            {
                list = new string[] {
                    "DSM.DSM INDEX",
                    "DSMBNKI.DSM BANKING",
                    "DSMINDI.DSM INDUSTRIAL",
                    "DSMINSI.DSM INSURANCE",
                    "DSMSRVI.DSM SERVICES" };
            }
            else if (name == "SAUDIARABIA")
            {
                list = new string[] { "SASEIDX | INDUSTRIES:SASEIDX Groups" };
            }
            else if (name == "SASEIDX Groups")
            {
                list = new string[] {
                    "SASEIDX.SASEIDX INDEX",
                    "SASEAGRI.SASEIDX AGRICULTURE",
                    "SASEBNK.SASEIDX BANK",
                    "SASEBULD.SASEIDX BUILD",
                    "SASECEM.SASEIDX CEMENT",
                    "SASEENER.SASEIDX ENERGY",
                    "SASEHOTE.SASEIDX HOTEL",
                    "SASEINDI.SASEIDX INDUSTRIAL",
                    "SASEINS.SASEIDX INSURANCE",
                    "SASEMEDI.SASEIDX MEDIA",
                    "SASEMINV.SASEIDX MULTILINE INS",
                    "SASEPETR.SASEIDX PETRO",
                    "SASEREAL.SASEIDX REALES",
                    "SASERETL.SASEIDX RETAIL",
                    "SASETEL.SASEIDX TELECOM",
                    "SASETRAN.SASEIDX TRANSPORTATION" };
            }
            else if (name == "ETF")
            {
                list = new string[] { "SINGLE CTY ETF", "US ETF", "US SECTOR ETF", "SPY ETF" };
            }
            else if (name == "CRYPTO")
            {
                list = new string[] { "CRYPTO CURRENCY", "CRYPTO FUTURES", "CRYPTO INDICES" };
            }

            //else if (name == "COMMODITIES")
            //{
            //    list = new string[] 
            //    { "US COMMODITIES",
            //        "CORN",
            //        "FIBERS",
            //        "FOODSTUFF",
            //        "LIVESTOCK",
            //        "OTHER GRAIN",
            //        "SOY",
            //        "COAL",
            //        "BONDS",
            //        "CREDIT DERIVATIVES",
            //        "CURRENCY",
            //        "CROSS CURRENCY",
            //        "INTEREST RATES",
            //        "SWAPS",
            //        "EQUITY INDEX",
            //        "HOUSING",
            //        "NON EQUITY INDEX",
            //        "BASE METALS",
            //        "INDUSTRIAL MATERIAL",
            //        "PRECIOUS METALS" };
            //}

            else if (name == "CRYPTO")
            {
                list = new string[]
                {
                    "CRYPTO CURRENCY >",
                    "",
                    "CRYPTO FUTURES >",
                    "",
                    "CRYPTO INDICES >"
                };
            }
            else if (name == "CANNABIS")
            {
                list = new string[]
                {
                    "CANNABIS STOCKS"
                };
            }
            else if (name == "WORLD EQUITY INDICES")
            {
                list = new string[] {
                    "GLOBAL INDICES"
                };
            }

            else if (name == "FX")
            {
                list = new string[] {
                    "USD BASE",
                    "EUR BASE",
                    "GBP BASE",
                    "G 10"
                };
            }

            else if (name == "COMMODITIES")
            {
                list = new string[]
                {
                    "AGRICULTURE",
                    "ENERGY",
                    "ENVIRONMENT",
                    "METALS"
                };
            }
            else if (name == "ML PORTFOLIOS")
            {
                var names = getFactorModelNames();
                list = names.Select(x => "\u0007" + x).ToArray();

            }
            //else if (name == "USER DATA")
            //{
            //    list = new string[]
            //    {
            //        "USER SYMBOL LIST 1",           
            //        "USER SYMBOL LIST 2",         
            //        "USER SYMBOL LIST 3",
            //    };
            //}
            else if (name == "US INDUSTRIES")
            {
                list = new string[] 
                {
                    "HOMEBUILDERS"
                };
            }

            else if (name == "GLOBAL FUTURES")
            {
                list = new string[]
                {
                    "WORLD BOND FUTURES",
                    "WORLD CURRENCY",
                    "WORLD EQUITY FUTURES"
                };
            }

            else if (name == "CQG COMMODITIES")
            {
                list = new string[]
                {
                    "CQG AGRICULTURE",                                       
                    "CQG ENERGY",                    
                    "CQG ENVIRONMENT",                 
                    "CQG HOUSING",                  
                    "CQG METALS",                    
                    "CQG SHIPPING",                  
                    "CQG WATER",
                };
            }
            else if (name == "CQG AGRICULTURE")
            {
                list = new string[]
                {
                    "CQG FERTILIZER",
                    "CQG FIBERS",
                    "CQG FOODSTUFF",
                    "CQG FOREST PRODUCTS",
                    "CQG GRAINS",
                    "CQG MEATS",
                    "CQG SOFTS",
                };
            }
            else if (name == "CQG FERTILIZER")
            {
                list = new string[]
                {
                    "CBOT FERTILIZER"
                };
            }
            else if (name == "CQG FIBERS")
            {
                list = new string[]
                {
                    "NYMEX COTTON"
                };
            }
            else if (name == "CQG FOODSTUFF")
            {
                list = new string[]
                {
                    "CME FOODSTUFF"
                };
            }
            else if (name == "CQG FOREST PRODUCTS")
            {
                list = new string[]
                {
                    "CME LUMBER"
                };
            }
            else if (name == "CQG GRAINS")
            {
                list = new string[]
                {
                    "CBOT GRAINS",
                    "BME GRAINS"
                };
            }
            else if (name == "CQG MEATS")
            {
                list = new string[]
                {
                    "CME MEATS",
                    "BME MEATS"
                };
            }
            else if (name == "CQG SOFTS")
            {
                list = new string[]
                {
                    "NYMEX SOFTS",
                    "BME SOFTS"
                };
            }
            else if (name == "CQG ENERGY")
            {
                list = new string[]
                {
                    "CQG CRUDE",
                    "CQG NATURAL GAS",
                    "CQG FUEL",
                };
            }
            else if (name == "CQG CRUDE")
            {
                list = new string[]
                {
                    "NYMEX CRUDE"
                };
            }
            else if (name == "CQG NATURAL GAS")
            {
                list = new string[]
                {
                    "NYMEX METHANOL",
                    "NYMEX NATURAL GAS",
                    "NYMEX ETHYLENE",
                };
            }
            else if (name == "CQG FUEL")
            {
                list = new string[]
                {
                    "NYMEX BUTANE",
                    "NYMEX COAL",
                    "NYMEX FUEL OIL",                   
                    "NYMEX GASOLINE",
                    "NYMEX PROPANE",
                };
            }
            else if (name == "CQG ENVIRONMENT")
            {
                list = new string[]
                {
                    "CQG EMISSIONS",
                };
            }
            else if (name == "CQG EMISSIONS")
            {
                list = new string[]
                {
                    "NYMEX EMISSIONS",
                };
            }
            else if (name == "CQG HOUSING")
            {
                list = new string[]
                {
                    "CME HOUSING",
                };
            }
            else if (name == "CQG METALS")
            {
                list = new string[]
                {
                    "CQG BASE",
                    "CQG PRECIOUS",
                    "CQG STEEL",
                };
            }
            else if (name == "CQG BASE")
            {
                list = new string[]
                {
                    "COMEX BASE"
                };
            }
            else if (name == "CQG PRECIOUS")
            {
                list = new string[]
                {
                    "COMEX PRECIOUS",            
                    "NYMEX PRECIOUS",           
                    "BMF PRECIOUS"
                };
            }
            else if (name == "CQG STEEL")
            {
                list = new string[]
                {
                    "COMEX STEEL"
                };
            }
            else if (name == "CQG SHIPPING")
            {
                list = new string[]
                {
                    "CQG FREIGHT",
                };
            }
            else if (name == "CQG FREIGHT")
            {
                list = new string[]
                {
                    "NYMEX FREIGHT",
                };
            }
            else if (name == "CQG WATER")
            {
                list = new string[]
                {
                    "CME WATER",
                };
            }
            else if (name == "CQG EQUITIES")
            {
                list = new string[]
                {
                    "CQG NASDAQ",
                    "CQG NYSE"
                };
            }
            else if (name == "CQG NASDAQ")
            {
                list = new string[]
                {
                    "CQG NDX 100"
                };
            }
            else if (name == "CQG NYSE")
            {
                list = new string[]
                {
                    "CQG NYSE 75"
                };
            }
            else if (name == "CQG ETF")
            {
                list = new string[]
                {
                    "CQG SINGLE CTY ETF",
                    "CQG SECTOR ETF",
                    "CQG SPY ETF"
                };
            }
            else if (name == "CQG SINGLE CTY ETF")
            {
                list = new string[]
                {
                    "NYSE AMERICAN SINGLE CTY ETF",
                };
            }
            else if (name == "CQG SECTOR ETF")
            {
                list = new string[]
                {
                    "NYSE AMERICAN SECTOR ETF",
                };
            }
            else if (name == "CQG SPY ETF")
            {
                list = new string[]
                {
                    "NYSE AMERICAN SPY",
                };
            }
            else if (name == "CQG FX & CRYPTO")
            {
                list = new string[]
                {
                    "CQG WORLD CURRENCY",
                    "CQG CRYPTO FUTURES"
                };
            }
            else if (name == "CQG WORLD CURRENCY")
            {
                list = new string[]
                {
                    "CME WORLD CURRENCY",
                    "SGX WORLD CURRENCY"
                };
            }
            else if (name == "CQG CRYPTO FUTURES")
            {
                list = new string[]
                {
                    "CME CRYPTO FUTURES"
                };
            }
            else if (name == "CQG INTEREST RATES")
            {
                list = new string[]
                {
                    "CQG N AMERICA RATES"
                };
            }
            else if (name == "CQG N AMERICA RATES")
            {
                list = new string[]
                {
                    "CQG US RATES",
                };
            }
            else if (name == "CQG US RATES")
            {
                list = new string[]
                {
                    "CBOT US RATES",
                    "CME US RATES"
                };
            }
            else if (name == "CQG N AMERICA STK INDICES")
            {
                list = new string[]
                {
                    "CQG US RATES",
                };
            }
            else if (name == "CQG STOCK INDICES")
            {
                list = new string[]
                {
                    "CBOT STOCK INDICES",
                    "CME STOCK INDICES"
                };
            }


            else if (name == "GLOBAL TREAS RATES")
            {
                list = new string[] 
                {
                    "N AMERICA RATES",
                    "S AMERICA RATES",
                    "EUROPE RATES",
                    "MEA RATES",
                    "ASIA RATES",
                    "OCEANIA RATES"
                };
            }

            else if (name == "US MUNI BONDS")
            {
                list = new string[]
                {
                    "GO",
                    "GO TAXABLE",
                    "REVENUE",
                    "REVENUE TAXABLE",
                    "MUNI SPREADS"
                };
            }

            else if (name == "GLOBAL CDS")
            {
                list = new string[]
                {
                    "SOVR CDS"
                };
            }

            else if (name == "RATES")
            {
                list = new string[] {
                    "GLOBAL 30YR",
                    "GLOBAL 10YR",
                    "GLOBAL 7YR",
                    "GLOBAL 5YR",
                    "GLOBAL 2YR",
                    "GLOBAL 1YR",
                };
            }
            return list;
        }

        private string[] getIntervalList()
        {
            string[] list = { "Quarterly", "Monthly", "Weekly", "Daily", "240 Min", "120 Min", "60 Min", "30 Min", "15 Min", "5 Min" };
            return list;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            _title = ScanTitle.Text;
            _portfolio = getPortfolio(PortfolioTree);
            _condition = getCondition(ConditionTree);
            _notificationOn = (NotificationCheckbox.IsChecked == true);
            //_textColor1 = TextColorPicker1.SelectedColor;
            //_textColor2 = TextColorPicker2.SelectedColor;
            //_textColor3 = TextColorPicker3.SelectedColor;
            _useOnTop = (OnTopCheckbox.IsChecked == true);
            _runInterval = int.Parse(RunInterval.Text);
            savePortfolios("EQS");
            savePortfolios("PRTU");
            savePortfolios("Spread");

            if (_portfolio.Length == 0 || _condition.Length == 0)
            {
                MessageBox.Show("Please select portfolio and condition.");
            }
            else
            {
                if (DialogEvent != null)
                {
                    DialogEvent(this, new DialogEventArgs(DialogEventArgs.EventType.Ok));
                }
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (DialogEvent != null)
            {
                DialogEvent(this, new DialogEventArgs(DialogEventArgs.EventType.Cancel));
            }
        }

        private void Mouse_Enter(object sender, MouseEventArgs e)
        {
            Label label = sender as Label;
            label.Background = Brushes.DimGray;
        }

        private void Mouse_Leave(object sender, MouseEventArgs e)
        {
            Label label = sender as Label;
            label.Background = new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x30));
        }

        private void setCondition(string input)
        {
            _condition = input;

            clearTree(root.Children);

            string[] conditions = _condition.Split('\u0001');
            foreach (string condition in conditions)
            {
                if (condition.Length > 0)
                {
                    string[] field1 = condition.Split('\u0002');
                    string name = field1[0];
                    string[] field2 = name.Split('\u0004');

                    if (field2.Length > 0)
                    {
                        name = field2[0];
                    }

                    name = name.Trim();

                    string interval = (field1.Length > 1) ? field1[1] : "";
                    string ago = (field1.Length > 2) ? field1[2] : "10000";
                    ago = "10000"; // temporary

                    var item = getItem(root.Children, name);

                    if (item != null)
                    {
                        item.Relationship = (field2.Length > 1) ? field2[1] : "<";
                        item.Value = (field2.Length > 2) ? field2[2] : "<";

                        if (interval == "")
                        {
                            item.IsChecked = true;
                        }
                        else
                        {
                            foreach (var child in item.Children)
                            {
                                if (child.Interval == interval)
                                {
                                    child.IsChecked = true;
                                }
                            }
                        }
                    }
                }
            }

            updateTitle();
        }

        private void clearTree(IEnumerable<TreeNode> items)
        {
            foreach (TreeNode item in items)
            {
                item.IsExpanded = false;
                item.IsChecked = false;
                if (item.Children != null)
                {
                    clearTree(item.Children);
                }
            }
        }

        private TreeNode getItem(IEnumerable<TreeNode> items, string name)
        {
            TreeNode output = null;
            foreach (TreeNode item in items)
            {
                if (item.Name == name)
                {
                    item.IsExpanded = true;
                    output = item;
                    break;
                }
                else if (item.Children != null)
                {
                    output = getItem(item.Children, name);
                    if (output != null)
                    {
                        item.IsExpanded = true;
                        break;
                    }
                }
            }
            return output;
        }

        public static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }

        private void setPortfolio(string input)
        {
            _portfolio = input;

            PortfolioTree.SetValue(VirtualizingPanel.IsVirtualizingProperty, true);
            PortfolioTree.SetValue(VirtualizingPanel.VirtualizationModeProperty, VirtualizationMode.Recycling);

            PortfolioTree.Items.Clear();
            initPortfolioTree();

            if (_portfolio.Length > 0)
            {
                string[] portfolios = _portfolio.Split('\u0001');
                foreach (string portfolio in portfolios)
                {
                    PortfolioTreeViewItem item = getPortfolioItem(PortfolioTree, portfolio);
                    if (item != null)
                    {
                        item.IsExpanded = true;
                        item.CheckBox.IsChecked = true;
                    }
                }
            }

            updateTitle();
        }

        private PortfolioTreeViewItem getPortfolioItem(ItemsControl parent, string name)
        {
            PortfolioTreeViewItem item = null;
            foreach (var it in parent.Items)
            {
                var child = it as PortfolioTreeViewItem;
                if (child != null)
                {
                    bool isEQS = (name.Length >= 1 && name.Substring(0, 1) == "\u0005");
                    bool isPRTU = (name.Length >= 1 && name.Substring(0, 1) == "\u0006");
                    bool isModel = (name.Length >= 1 && name.Substring(0, 1) == "\u0007");
                    bool isSpread = (name.Length >= 1 && name.Substring(0, 1) == "\u00008");
                    bool isLIST = (name.Length >= 1 && name.Substring(0, 1) == "\u00009");
                    bool isWorksheet = (name.Length >= 1 && name.Substring(0, 1) == "\u001e");

                    if (isEQS && child.Type == Portfolio.PortfolioType.EQS && child.Symbol == name.Substring(1))
                    {
                        item = child;
                        break;
                    }
                    else if (isPRTU && child.Type == Portfolio.PortfolioType.PRTU && child.Symbol == name.Substring(1))
                    {
                        item = child;
                        break;
                    }
                    else if (isModel && child.Type == Portfolio.PortfolioType.Model && child.Symbol == name.Substring(1))
                    {
                        item = child;
                        break;
                    }
                    else if (isSpread && child.Type == Portfolio.PortfolioType.Spread)
                    {
                        if (child.Symbol == name.Substring(1))
                        {
                            item = child;
                            break;
                        }
                    }
                    else if (isWorksheet && child.Type == Portfolio.PortfolioType.Worksheet)
                    {
                        if (child.Symbol == name.Substring(1))
                        {
                            item = child;
                            break;
                        }
                    }
                    else if (name == child.Symbol)
                    {
                        item = child;
                        break;
                    }
                    else
                    {
                        item = getPortfolioItem(child, name);
                        if (item != null)
                        {
                            child.IsExpanded = true;
                            break;
                        }
                    }
                }
            }
            return item;
        }

        private string getPortfolio(ItemsControl item)
        {
            string portfolio = "";

            PortfolioTreeViewItem portfolioItem = item as PortfolioTreeViewItem;

            bool isChecked = (portfolioItem != null && portfolioItem.CheckBox != null && portfolioItem.CheckBox.IsChecked == true);

            if (isChecked)
            {
                if (portfolioItem.Type == Portfolio.PortfolioType.EQS)
                {
                    portfolio += "\u0005";
                }
                else if (portfolioItem.Type == Portfolio.PortfolioType.PRTU)
                {
                    portfolio += "\u0006";
                }
                else if (portfolioItem.Type == Portfolio.PortfolioType.Model)
                {
                    portfolio += "\u0007";
                }
                else if (portfolioItem.Type == Portfolio.PortfolioType.Spread)
                {
                    portfolio += "\u0008";
                }
                else if (portfolioItem.Type == Portfolio.PortfolioType.Worksheet)
                {
                    portfolio += "\u001e";
                }
                portfolio += portfolioItem.Symbol;
            }
            else
            {
                foreach (var it in item.Items)
                {
                    var child = it as ItemsControl;
                    if (child != null)
                    {
                        string symbol = getPortfolio(child);
                        if (symbol.Length > 0)
                        {
                            if (portfolio.Length > 0)
                            {
                                portfolio += "\u0001";
                            }
                            portfolio += symbol;
                        }
                    }
                }
            }
            
            return portfolio;
        }

        private void ScanTitle_TextChanged(object sender, TextChangedEventArgs e)
        {
            string title = ScanTitle.Text;
            if (title == "")
            {
                _useDefaultTitle = true;
                updateTitle();
            }
            else
            {
                string portfolio = getPortfolio(PortfolioTree);
                string condition = getCondition(ConditionTree);
                if (condition != "" && portfolio != "")
                {
                    string defaultTitle = Scan.GetTitle(portfolio, condition);
                    _useDefaultTitle = (title == defaultTitle);
                }
            }
        }

        private void DefaultTitleCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            bool useDefaultTitle = (DefaultTitleCheckbox.IsChecked == true);
            if (useDefaultTitle)
            {
                _useDefaultTitle = true;
                updateTitle();
            }
            else
            {
                _useDefaultTitle = false;
            }
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = sender as CheckBox;
            TreeNode treeNode = checkBox.DataContext as TreeNode;
            treeNode.IsSelected = (checkBox.IsChecked == true);

            updateTitle();
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            updateTitle();
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            TreeNode treeNode = textBox.DataContext as TreeNode;
            treeNode.Value = textBox.Text;

            updateTitle();
        }

        private string getCondition(TreeView input)
        {
            string output = "";
 
            foreach (TreeNode node in input.Items)
            {
                var text = getCondition(node);
                if (text.Length > 0)
                {
                    output += ((output.Length > 0) ? "\u0001" : "") + text;
                }
            }
            return output;
        }

        private string getCondition(TreeNode input)
        {
            string output = "";
            if (input.HasChildren)
            {
                foreach (var node in input.Children)
                {
                    var text = getCondition(node);
                    if (text.Length > 0)
                    {
                        output += ((output.Length > 0) ? "\u0001" : "") + text;
                    }
                }
            }
            else if (input.IsChecked)
            {
                output = input.GetDescription();
            }
            return output;
        }

        private void ConditionTree_Loaded(object sender, RoutedEventArgs e)
        {
            setCondition(_condition);
        }

        private void RunInterval_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = IsTextNumeric(e.Text);
        }

        private static bool IsTextNumeric(string str)
        {
            System.Text.RegularExpressions.Regex reg = new System.Text.RegularExpressions.Regex("[^0-9]");
            return reg.IsMatch(str);
        }
    }

    class CheckboxTreeViewItem : TreeViewItem
    {
        public CheckboxTreeViewItem()
        {
            _checkBox.Add(new CheckBox());
        }

        List<CheckBox> _checkBox = new List<CheckBox>();
        bool _hasInterval = false;

        public bool HasInterval
        {
            get { return _hasInterval; }
            set { _hasInterval = value; }
        }

        public List<CheckBox> CheckBox
        {
            get { return _checkBox; }
            set { _checkBox = value; }
        }
    }

    class IntervalTreeViewItem : TreeViewItem
    {
        public string ParentName { get; set; }
    }

    class FundamentalTreeViewItem : TreeViewItem
    {
        CheckBox _checkBox;
        ComboBox _comboBox;
        TextBox _textBox;

        public CheckBox CheckBox
        {
            get { return _checkBox; }
            set { _checkBox = value; }
        }
        public ComboBox ComboBox
        {
            get { return _comboBox; }
            set { _comboBox = value; }
        }
        public TextBox TextBox
        {
            get { return _textBox; }
            set { _textBox = value; }
        }
    }


    class PortfolioTreeViewItem : TreeViewItem
    {
        Portfolio.PortfolioType _type = Portfolio.PortfolioType.Index;
        CheckBox _checkBox = null;
        string _symbol = "";

        public CheckBox CheckBox
        {
            get { return _checkBox; }
            set { _checkBox = value; }
        }

        public string Symbol
        {
            get { return _symbol; }
            set { _symbol = value; }
        }

        public Portfolio.PortfolioType Type
        {
            get { return _type; }
            set { _type = value; }
        }
    }

    public delegate void DialogEventHandler(object sender, DialogEventArgs e);

    public class DialogEventArgs : EventArgs
    {
        public enum EventType
        {
            Ok,
            Cancel
        }

        EventType _type;

        public DialogEventArgs(EventType type)
        {
            _type = type;
        }

        public EventType Type
        {
            get { return _type; }
        }
    }
}
