using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using static ATMML.AssistantService;

namespace ATMML
{
	public class SymbolLabel : Label
	{
		public string Symbol { get; set; }
		public string Ticker { get; set; }
		public string Description { get; set; }
	}

	public class TextBoxButton : Label
	{
		private TextBox _textBox;
		private CheckBox _checkBox;

		public TextBox TextBox
		{
			get { return _textBox; }
			set { _textBox = value; }
		}
		public CheckBox CheckBox
		{
			get { return _checkBox; }
			set { _checkBox = value; }
		}
	}

	public class Navigation
	{
		public bool UseCheckBoxes { get; set; }
		public bool UseGroup { get; set; }

		private static int compareDescriptions(string first, string second)
		{
			string[] field1 = first.Split(':');
			string symbol1 = field1[0];
			string label1 = (field1.Length == 2) ? field1[1] : field1[0];

			string[] field2 = second.Split(':');
			string symbol2 = field2[0];
			string label2 = (field2.Length == 2) ? field2[1] : field2[0];

			int retval = string.Compare(label1, label2);
			return retval;
		}

		private Dictionary<string, List<PortfolioCheckBox>> _checkBoxList = new Dictionary<string, List<PortfolioCheckBox>>();

		public class PortfolioCheckBox : CheckBox
		{
			public string Symbol { get; set; }

			public string PanelName { get; set; }
		}

		public string GetPortfolio()
		{
			var output = "";
			foreach (var ticker in tickers)
			{
				if (output.Length > 0) output += " & ";
				output += ticker;
			}
			return output;
		}

		public List<string> GetTickerList()
		{
			return tickers.ToList();
		}

		public string GetNavigationPaths()
		{
			var output = "";
			_selectedPortfolios.ToList().ForEach(kvp =>
			{
				var path = kvp.Key;
				output += path + "\n";
			});
			return output;
		}

		private List<string>[] _activeNavigationPath = { new List<string>(), new List<string>(), new List<string>(), new List<string>(), new List<string>(), new List<string>() };

		public void SetNavigationPaths(string input)
		{
			_activeNavigationPathIndex = 0;
			_activeNavigationPath.ToList().ForEach(XamlGeneratedNamespace => XamlGeneratedNamespace.Clear());
			var list = input.Split('\n').ToList();
			list.ForEach(x =>
			{
				var fields = x.Split('\t');
				for (var ii = 0; ii < fields.Length; ii++)
				{
					if (fields[ii].Length > 0)
					{
						_activeNavigationPath[ii].Add(fields[ii]);
					}
				}
			});
		}

		public void ClearPortfolio()
		{
			_selectedPortfolios.Clear();
		}

		HashSet<string> tickers = new HashSet<string>();

		public void SetPortfolio(List<string> input)
		{
			tickers.Clear();

			foreach (var ticker in input)
			{
				tickers.Add(ticker);
			}

			SetCheckBoxes();
		}

		public void SetCheckBoxes()
		{
			_inCheckProcessing = true;

			foreach (var kvp in _checkBoxList)
			{
				var cbl = kvp.Value;
				foreach (var cb in cbl)
				{
					cb.IsChecked = false;
				}
			}

			foreach (var ticker in tickers)
			{
				var symbol = ticker.Trim();
				bool found = false;
				foreach (var kvp in _checkBoxList)
				{
					var cbl = kvp.Value;
					foreach (var cb in cbl)
					{
						if (cb.Tag as string == symbol)
						{
							cb.IsChecked = true;
							found = true;
							break;
						}
					}
					if (found) break;
				}
			}
			_inCheckProcessing = false;
		}

		ScanDialog _scanDialog;

		private void Menu_MouseEnter(object sender, MouseEventArgs e)
		{
			SymbolLabel label = sender as SymbolLabel;
			StackPanel panel = label.Parent as StackPanel;
			if (panel != null)
			{
				panel.Cursor = Cursors.Hand;
			}
		}

		private void Menu_MouseLeave(object sender, MouseEventArgs e)
		{
			SymbolLabel label = sender as SymbolLabel;
			StackPanel panel = label.Parent as StackPanel;
			if (panel != null)
			{
				panel.Cursor = Cursors.Arrow;
			}
		}

		public void addPortfolioMenu(Portfolio.PortfolioType type, StackPanel panel, MouseButtonEventHandler buttonClickEvent, string name = "", bool withTitle = false, bool useSettings = true)
		{
			BrushConverter bc = new BrushConverter();

			StackPanel clientPortfolioPanel = new StackPanel();
			clientPortfolioPanel.Orientation = Orientation.Vertical;
			clientPortfolioPanel.SetValue(Grid.ColumnSpanProperty, 3);
			clientPortfolioPanel.SetValue(Grid.ColumnProperty, 0);
			clientPortfolioPanel.SetValue(Grid.RowProperty, 0);
			clientPortfolioPanel.HorizontalAlignment = HorizontalAlignment.Left;
			clientPortfolioPanel.VerticalAlignment = VerticalAlignment.Center;
			clientPortfolioPanel.Margin = new Thickness(0, 2, 2, 2);

			if (withTitle)
			{
				Label label = new Label();
				label.Content = "";

				if (type == Portfolio.PortfolioType.EQS) label.Content = "EQS - Type Name of EQS";
				else if (type == Portfolio.PortfolioType.PRTU) label.Content = "PRTU - Type PRTU ID - Ex U1234567-01";
				else if (type == Portfolio.PortfolioType.Peers) label.Content = "\nBLOOMBERG PEERS - Ex Tu MSFT US Equity\n\n";
				else if (type == Portfolio.PortfolioType.Model) label.Content = "Your Portfolios";
				else if (type == Portfolio.PortfolioType.Alert) label.Content = "Your Alerts";

				label.Height = 30;
				label.FontSize = 11;
				label.Margin = new Thickness(0, 0, 0, 0);
				label.Padding = new Thickness(0, 0, 0, 0);
				label.Foreground = (Brush)bc.ConvertFrom("#FF98fb98");
				label.Background = Brushes.Transparent;
				clientPortfolioPanel.Children.Add(label);
			}

			StackPanel stackPanel = new StackPanel();
			stackPanel.Orientation = Orientation.Horizontal;

			CheckBox cb = null;
			if (type == Portfolio.PortfolioType.Model)
			{
				cb = new CheckBox();
				cb.Tag = name;
				stackPanel.Children.Add(cb);
			}

			TextBox textBox1 = new TextBox();
			textBox1.Height = 25;
			textBox1.Width = 175;
			textBox1.BorderBrush = (Brush)bc.ConvertFrom("#FF124b72");
			textBox1.BorderThickness = new Thickness(1);
			textBox1.Background = Brushes.Black;
			textBox1.Margin = new Thickness(0, 0, 4, 0);
			textBox1.Padding = new Thickness(0);
			textBox1.FontSize = 11;
			textBox1.Foreground = Brushes.White;
			textBox1.VerticalContentAlignment = VerticalAlignment.Center;
			textBox1.Name = name.Replace(" ", "_").Replace("&", "_").Replace("+", "_").Replace("-", "_").Replace("(", "_").Replace(")", "_");
			textBox1.Text = useSettings ? MainView.GetSetting(name) : name;
			if (useSettings) textBox1.TextChanged += textBox_TextChanged;
			textBox1.IsReadOnly = !useSettings;
			stackPanel.Children.Add(textBox1);

			if (type == Portfolio.PortfolioType.PRTU)
			{
				//TextBox textBox3 = new TextBox();
				//textBox3.Height = 25;
				//textBox3.Width = 50;
				//textBox3.BorderBrush = (Brush)bc.ConvertFrom("#FF124b72");
				//textBox3.BorderThickness = new Thickness(1);
				//textBox3.Background = Brushes.Black;
				//textBox3.Margin = new Thickness(4, 0, 4, 0);
				//textBox3.Padding = new Thickness(0);
				//textBox3.FontSize = 11;
				//textBox3.Foreground = Brushes.White;
				//textBox3.VerticalContentAlignment = VerticalAlignment.Center;
				//textBox3.Text = (textBox1.Text.Length > 0) ? "D" : "";
				//stackPanel.Children.Add(textBox3);
			}

			TextBoxButton button = new TextBoxButton();
			button.Width = 25;
			button.Height = 25;
			button.BorderBrush = (Brush)bc.ConvertFrom("#FF124b72");
			button.BorderThickness = new Thickness(1);
			button.Background = Brushes.Black;
			button.Foreground = Brushes.Lime;
			button.HorizontalContentAlignment = HorizontalAlignment.Center;
			button.VerticalContentAlignment = VerticalAlignment.Center;

			button.FontSize = 11;
			button.Margin = new Thickness(4, 0, 0, 0);
			button.Padding = new Thickness(0);
			button.Content = "Go";
			button.TextBox = textBox1;
			button.CheckBox = cb;
			if (cb != null)
			{
				cb.Margin = new Thickness(0, 5, 8, 0);
			}
			button.Name = "PRTU";

			if (type == Portfolio.PortfolioType.EQS) button.Name = "EQS";
			else if (type == Portfolio.PortfolioType.Model) button.Name = "Model";
			else if (type == Portfolio.PortfolioType.Peers) button.Name = "Peers";
			else if (type == Portfolio.PortfolioType.YourList) button.Name = "YourLists";
			else if (type == Portfolio.PortfolioType.Worksheet) button.Name = "LIST";

			button.MouseDown += buttonClickEvent;
			button.MouseEnter += Button_MouseEnter;
			button.MouseLeave += Button_MouseLeave;
			button.Cursor = Cursors.Hand;
			stackPanel.Children.Add(button);

			clientPortfolioPanel.Children.Add(stackPanel);
			panel.Children.Add(clientPortfolioPanel);
		}

		private void Button_MouseLeave(object sender, MouseEventArgs e)
		{
			Label button = sender as Label;
			button.Foreground = Brushes.Lime;
		}

		private void Button_MouseEnter(object sender, MouseEventArgs e)
		{
			Label button = sender as Label;
			button.Foreground = Brushes.White;
		}

		void textBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			TextBox textBox = sender as TextBox;
			MainView.SaveSetting(textBox.Name, textBox.Text);
		}

		private List<string> getAlertPortfolios(string groupName)
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
						string[] fields = portfolio.Split(':');
						string ticker = fields[0];
						if (ticker.Length > 0)
						{
							names.Add(groupName + "." + ticker + "." + fields[1]);
						}
					}
				}
			}
			catch (Exception)
			{
			}
			return names;
		}

		private List<string> getModelNames()
		{
			var output = MainView.getModelNames();
			output.Sort();
			return output;
		}

		private List<string> getFactorModelNames()
		{
			var output = MainView.getFactorModelNames();
			output.Remove("_meta");
			output.Sort();
			return output;
		}

		private List<string> getAlertNames()
		{
			var output = new List<string>();
			try
			{
				List<string> names = Directory.EnumerateFiles(MainView.GetDataFolder() + @"\alerts").ToList();
				output = names.Select(x => { var ix = x.LastIndexOf('\\'); return x.Substring(ix + 1); }).ToList();
			}
			catch (Exception x)
			{
			}
			output.Sort();
			return output;
		}
		private List<string> getYourLists()
		{
			var output = new List<string>();
			try
			{
				List<string> names = Directory.EnumerateFiles(MainView.GetDataFolder() + @"\lists").ToList();
				output = names.Select(x => { var ix = x.LastIndexOf('\\'); return x.Substring(ix + 1); }).ToList();
			}
			catch (Exception x)
			{
			}
			output.Sort();
			return output;
		}
		public void setModelNavigationMenu(StackPanel panel, MouseButtonEventHandler go_Click)
		{
			var names = getModelNames();
			for (int ii = 0; ii < names.Count; ii++)
			{
				addPortfolioMenu(Portfolio.PortfolioType.Model, panel, go_Click, names[ii], ii == 0, false);
			}
		}

		public void clearCheckBoxes(string panelName)
		{
			_checkBoxList[panelName] = new List<PortfolioCheckBox>();
		}

		public void setNavigation(StackPanel panel, MouseButtonEventHandler mouseDownEvent, string[] items)
		{
			string panelName = panel.Name;
			List<string> menuItems = new List<string>(items);

			BrushConverter bc = new BrushConverter();

			panel.Children.Clear();

			if (!_checkBoxList.ContainsKey(panelName))
			{
				_checkBoxList[panelName] = new List<PortfolioCheckBox>();
			}
			else
			{
				_checkBoxList[panelName].Clear();
			}

			var activePathList = _activeNavigationPath[_activeNavigationPathIndex].ToList().Select(x => {
				if (x.Length > 0)
				{
					var fields = x.Split(':');
					return fields.Last();
				}
				else
				{
					return x;
				}
			});

			int count = menuItems.Count();
			for (int ii = 0; ii < count; ii++)
			{
				SymbolLabel label1 = new SymbolLabel();

				string item = menuItems[ii];

				bool isLeaf = item.Trim().Length > 0 && !item.Contains(">") && !item.Contains("DEVELOPED") && !item.Contains("EMERGING") && !item.Contains("FRONTIER");

				string[] field = item.Split(';');
				string tooltip = (field.Length == 2) ? field[1] : "";

				item = (field.Length == 2) ? field[0] : item;

				field = item.Split(':');
				string symbol = field[0];
				string label = (field.Length == 2) ? field[1] : field[0];
				string description = (field.Length == 3) ? field[2] : "";

				label1.Symbol = symbol;
				label1.Ticker = (symbol.Contains(" ") || Portfolio.isCQGSymbol(symbol)) ? symbol : symbol + " Index";
				label1.Description = description;
				label1.Content = (description.Length > 0) ? description : label;
				label1.Foreground = activePathList.Contains(label) ? (Brush)bc.ConvertFrom("#FF00ccff") : (Brush)bc.ConvertFrom("#FFffffff");
				label1.Background = Brushes.Transparent;
				label1.Padding = new Thickness(0, 2, 0, 2);
				label1.HorizontalAlignment = HorizontalAlignment.Left;
				label1.VerticalAlignment = VerticalAlignment.Top;
				label1.FontFamily = new FontFamily("Helvetica Neue");
				label1.FontWeight = FontWeights.Normal;
				label1.FontSize = 11;
				label1.MouseEnter += Menu_MouseEnter;
				label1.MouseLeave += Menu_MouseLeave;
				label1.MouseDown += mouseDownEvent;
				if (tooltip.Length > 0) label1.ToolTip = tooltip;

				if (isLeaf && UseCheckBoxes)
				{
					StackPanel subPanel = new StackPanel();
					subPanel.Orientation = Orientation.Horizontal;
					PortfolioCheckBox cb = new PortfolioCheckBox();

					cb.PanelName = panelName;
					cb.Symbol = label;
					cb.Tag = symbol;
					//cb.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
					cb.Background = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
					cb.BorderBrush = new SolidColorBrush(Color.FromRgb(0x00, 0x00, 0x00));
					cb.BorderThickness = new Thickness(1);
					cb.Padding = new Thickness(0, 5, 5, 5);
					cb.Checked += Cb_Checked;
					cb.Unchecked += Cb_Unchecked;
					subPanel.Children.Add(cb);
					subPanel.Children.Add(label1);
					panel.Children.Add(subPanel);
					_checkBoxList[panelName].Add(cb);
				}
				else
				{
					panel.Children.Add(label1);
				}
			}
		}

		private string getNavigationPath()
		{
			var output = "";
			for (var ii = 0; ii < 6; ii++)
			{
				if (_navigationPath[ii] != "")
				{
					if (output.Length > 0) output += "\t";
					output += _navigationPath[ii];
				}
			}
			return output;
		}

		private bool _inCheckProcessing = false;
		private Dictionary<string, List<string>> _selectedPortfolios = new Dictionary<string, List<string>>();

		private void Cb_Unchecked(object sender, RoutedEventArgs e)
		{
			if (!_inCheckProcessing)
			{
				var path = getNavigationPath();
				if (_selectedPortfolios.ContainsKey(path))
				{
					var list = _selectedPortfolios[path];

					var cb = sender as PortfolioCheckBox;
					var panelName = cb.PanelName;
					var symbol = (cb.Tag as string).Trim();

					if (cb.Symbol.Contains(" All Members"))
					{
						var cnt = _checkBoxList[panelName].Count;
						_inCheckProcessing = true;
						for (var ii = 2; ii < cnt; ii++)
						{
							var cb1 = _checkBoxList[panelName][ii];
							cb1.IsChecked = false;
							var symbol1 = (cb1.Tag as string).Trim();
							tickers.Remove(symbol1);
							var index1 = list.FindIndex(x => x == symbol1);
							if (index1 >= 0)
							{
								list.RemoveAt(index1);
								if (list.Count == 0)
								{
									_selectedPortfolios.Remove(path);
									break;
								}
							}
						}
						_inCheckProcessing = false;
					}
					else
					{
						tickers.Remove(symbol);

						var index = list.FindIndex(x => x == symbol);
						if (index >= 0)
						{
							list.RemoveAt(index);
							if (list.Count == 0)
							{
								_selectedPortfolios.Remove(path);
							}
						}

						var allChecked = true;
						var cnt = _checkBoxList[panelName].Count;
						for (var ii = 2; ii < cnt; ii++)
						{
							var cb1 = _checkBoxList[panelName][ii];
							var symbol3 = (cb1.Tag as string).Trim();
							var index3 = list.FindIndex(x => x == symbol3);
							if (index3 == -1)
							{
								allChecked = false;
								break;
							}
						}
						if (_checkBoxList[panelName].Count > 1)
						{
							_inCheckProcessing = true;
							_checkBoxList[panelName][1].IsChecked = allChecked;
							_inCheckProcessing = false;
						}
					}
				}
			}
		}

		private void Cb_Checked(object sender, RoutedEventArgs e)
		{
			if (!_inCheckProcessing)
			{
				_inCheckProcessing = true;
				var path = getNavigationPath();
				if (!_selectedPortfolios.ContainsKey(path))
				{
					_selectedPortfolios[path] = new List<string>();
				}
				var list = _selectedPortfolios[path];

				var cb = sender as PortfolioCheckBox;
				var panelName = cb.PanelName;
				var symbol = (cb.Tag as string).Trim();

				if (cb.Symbol.Contains(" All Members"))
				{
					var cnt = _checkBoxList[panelName].Count;
					for (var ii = 2; ii < cnt; ii++)
					{
						var cb1 = _checkBoxList[panelName][ii];
						cb1.IsChecked = true;
						var symbol1 = (cb1.Tag as string).Trim();
						tickers.Add(symbol1);
						var index1 = list.FindIndex(x => x == symbol1);
						if (index1 == -1)
						{
							list.Add(symbol1);
						}
					}
				}
				else
				{
					var index2 = list.FindIndex(x => x == symbol);
					if (index2 == -1)
					{
						list.Add(symbol);
						tickers.Add(symbol);
					}

					var allChecked = true;
					var cnt = _checkBoxList[panelName].Count;
					for (var ii = 2; ii < cnt; ii++)
					{
						var cb1 = _checkBoxList[panelName][ii];
						var symbol3 = (cb1.Tag as string).Trim();
						var index3 = list.FindIndex(x => x == symbol3);
						if (index3 == -1)
						{
							allChecked = false;
							break;
						}
					}
					if (_checkBoxList[panelName].Count > 1)
					{
						_checkBoxList[panelName][1].IsChecked = allChecked;
					}
				}
				_inCheckProcessing = false;
			}
		}

		private int _activeNavigationPathIndex = 0;

		public bool setNavigationLevel1(string continent, StackPanel panel, MouseButtonEventHandler mouseDownEvent, MouseButtonEventHandler go_Click)
		{
			_activeNavigationPathIndex = 1;
			_navigationPath[0] = continent;
			_navigationPath[1] = "";
			_navigationPath[2] = "";
			_navigationPath[3] = "";
			_navigationPath[4] = "";
			_navigationPath[5] = "";

			BrushConverter bc = new BrushConverter();

			bool ok = true;

			if (continent == "GLOBAL EQ >")
			{
				string[] items = new string[] { "N AMERICA EQ >", " ", "S AMERICA EQ >", " ", "EUROPE EQ >", " ", "MEA EQ >", " ", "ASIA EQ >", " ", "OCEANIA EQ >" };

				setNavigation(panel, mouseDownEvent, items);
			}

			else if (continent == "SPREADS >")
			{
				if (MainView.EnableSpreadScans)
				{
					string[] items = new string[] { "US 100 SPREADS", " ", "OEXNDX SPREADS", " ", "SP 500 SPREADS" };

					setNavigation(panel, mouseDownEvent, items);
				}
			}

			else if (continent == "BLOOMBERG >")
			{
				string[] items = new string[] { "BLOOMBERG EQS >", " ", "BLOOMBERG PRTU >", " ", "BLOOMBERG WORKSHEETS >" };

				setNavigation(panel, mouseDownEvent, items);
				addPortfolioMenu(Portfolio.PortfolioType.Peers, panel, go_Click, "Peers", true);
			}

			else if (continent == "ML PORTFOLIOS >")
			{
				var names = new List<string>();
				if (MainView.EnablePortfolio)
				{
					names = getFactorModelNames();
				}
				else if (MainView.EnableRickStocks)
				{
					names.Add("OEX MT");
				}
				setNavigation(panel, mouseDownEvent, names.ToArray());
				//for (int ii = 0; ii < names.Count; ii++)
				//{
				//    addPortfolioMenu(Portfolio.PortfolioType.Model, panel, go_Click, names[ii], ii == 0, false);
				//}
			}

			else if (continent == "ALPHA NETWORKS >")
			{
				string[] items = new string[] { "CMR", " ", "SQ PT", " ", "TELK", " ", "TIM", " ", "TMSG" };

				setNavigation(panel, mouseDownEvent, items);
			}

			else if (continent == "ATM ALERTS >")
			{
				var names = getAlertNames();
				for (int ii = 0; ii < names.Count; ii++)
				{
					addPortfolioMenu(Portfolio.PortfolioType.Alert, panel, go_Click, names[ii], ii == 0, false);
				}
			}

			else if (continent == "BLOOMBERG EQS >")
			{
				for (int ii = 0; ii < 18; ii++)
				{
					addPortfolioMenu(Portfolio.PortfolioType.EQS, panel, go_Click, "EQS" + (ii + 1).ToString(), ii == 0);
				}
			}

			else if (continent == "BLOOMBERG WORKSHEETS >")
			{
				for (int ii = 0; ii < 18; ii++)
				{
					setWorksheetNavigation(panel, mouseDownEvent);
				}
			}

			else if (continent == "BLOOMBERG PRTU >")
			{
				for (int ii = 0; ii < 18; ii++)
				{
					addPortfolioMenu(Portfolio.PortfolioType.PRTU, panel, go_Click, "PRTU" + (ii + 1).ToString(), ii == 0);
				}
			}

			else if (continent == "CANNABIS >")
			{
				setNavigation(panel, mouseDownEvent, new string[] { "CANNABIS STOCKS" });
			}
			else if (continent == "SPREADS >")
			{
				setNavigation(panel, mouseDownEvent, new string[] { "US 100 SPREADS", " ", "OEXNDX SPREADS", " ", "SP 500 SPREADS" });
			}

			else if (continent == "ETF >")
			{
				if (UseGroup || UseCheckBoxes)
				{
					setNavigation(panel, mouseDownEvent, new string[] {
					"SINGLE CTY ETF >", " ", "US ETF >", " ", "COMDTY ETF >", " ", "COMDTY PROD ETF >", " ", "FIXED INCOME ETF >", " ", "FX ETF >",  " ","US SECTOR ETF >", " ", "SPECIALTY IND ETF >", " ","SPY ETF >"});
				}
				else
				{
					setNavigation(panel, mouseDownEvent, new string[] {
					"SINGLE CTY ETF >", " ", "US ETF >", " ", "COMDTY ETF >", " ", "COMDTY PROD ETF >", " ", "FIXED INCOME ETF >", " ", "FX ETF >",  " ","US SECTOR ETF >", " ", "SPECIALTY IND ETF >", " ","SPY ETF >"});
				}
			}

			else if (continent == "CRYPTO >")
			{
				if (UseGroup || UseCheckBoxes)
				{
					setNavigation(panel, mouseDownEvent, new string[] {
					"CRYPTO CURRENCY >", " ", "CRYPTO FUTURES >", " ", "CRYPTO INDICES >"});
				}
				else
				{
					setNavigation(panel, mouseDownEvent, new string[] {
					"CRYPTO CURRENCY", " ", "CRYPTO FUTURES", " ", "CRYPTO INDICES"});
				}
			}

			//else if (continent == "FUTURES | COM >")
			//{
			//    setNavigationLevel1(panel, mouseDownEvent, new string[] {
			//        "ACTIVE >",              
			//        "",
			//        "GENERIC >"
			//    });
			//}

			else if (continent == "COMMODITIES >")
			{
				setNavigation(panel, mouseDownEvent, new string[] {
					"AGRICULTURE >",
					" ",
					"ENERGY >",
					" ",
					"ENVIRONMENT >",
					 " ",
					"METALS >"
				});
			}

			else if (continent == "INTEREST RATE FUTURES >")
			{
				setNavigation(panel, mouseDownEvent, new string[] {
					"WORLD BOND FUTURES",
				});
			}

			else if (continent == "UTIL SECTOR | RATING > ")
			{
				setNavigation(panel, mouseDownEvent, new string[] {
					"UTIL BBB",
				});
			}

			else if (continent == "INTEREST RATES >")
			{
				setNavigation(panel, mouseDownEvent, new string[] {
					"N AMERICA RATES >",
					" ",
					"S AMERICA RATES >",
					" ",
					"EUROPE RATES >",
					" ",
					"MEA RATES >",
					" ",
					"ASIA RATES >",
					" ",
					"OCEANIA RATES >",
                    //" ",
                    //"WORLD BOND FUTURES >",
                });
			}

			else if (continent == "US INDUSTRIES >")
			{
				setNavigation(panel, mouseDownEvent, new string[] {
                    //"IND | COMMUNICATIONS >",
                    //" ",
                    "IND | CONSUMER DISC >",
					" ",
					"IND | CONSUMER STAPLES >",
					" ",
					"IND | ENERGY >",
                    //" ",
                    //"IND | FINANCIALS >",
                    //" ",
                    //"IND | HEALTH CARE >",
                    " ",
					"IND | INDUSTRIALS >",
					" ",
					"IND | MATERIALS >",
                    //" ",
                    //"IND | REAL ESTATE >",
                    " ",
					"IND | TECHNOLOGY >",
					" ",
					"IND | UTILITIES >",
				});
			}

			else if (continent == "US GOVERNMENT >")
			{
				setNavigation(panel, mouseDownEvent, new string[] {
					"EXECUTIVE OFFICE OF THE PRESIDENT >",
					" ",
					"DEPT OF AGRICULTURE >",
					" ",
					"DEPT OF COMMERCE >",
					 " ",
					"DEPT OF DEFENSE >",
					" ",
					"DEPT OF ENERGY >",
					" ",
					"DEPT OF HOMELAND SECURITY >",
					" ",
					"DEPT OF HOUSING AND URBAN DEVELOPMENT >",
					" ",
					"DEPT OF TRANSPORATION >",
					" ",
					"DEPT OF TREASURY >",
					" ",
					"LEGISLATIVE BRANCH >",
					" ",
					"INDEPENDENT AGENCIES >"
				});
			}

			else if (continent == "USER FEATURE DATA >")
			{
				setNavigation(panel, mouseDownEvent, new string[] {
					"USER FEATURE DATA FILE 1 >",
					" ",
					"USER FEATURE DATA FILE 2 >",
					" ",
					"USER FEATURE DATA FILE 3 >",
				});
			}


			else if (continent == "GLOBAL FUTURES >")
			{
				setNavigation(panel, mouseDownEvent, new string[] {
					"WORLD BOND FUTURES >",
                    //" ",
                    //"WORLD CURRENCY >",
                    " ",
					"WORLD EQUITY FUTURES >"
				});
			}

			else if (continent == "FX & CRYPTO >")
			{
				setNavigation(panel, mouseDownEvent, new string[] {
					"WORLD CURRENCY >",
					"",
					"CRYPTO CURRENCY >",
					"",
					"CRYPTO FUTURES >",
					"",
					"CRYPTO INDICES >"
				});
			}

			else if (continent == "CQG COMMODITIES >")
			{
				setNavigation(panel, mouseDownEvent, new string[] {
					"CQG AGRICULTURE >",
					" ",
					"CQG ENERGY >",
					" ",
					"CQG ENVIRONMENT >",
					" ",
					"CQG HOUSING >",
					 " ",
					"CQG METALS >",
					"",
					"CQG SHIPPING >",
					" ",
					"CQG WATER >",               
                    //" ",
                    //"CQG WEATHER >",
                });
			}

			else if (continent == "CQG EQUITIES >")
			{
				setNavigation(panel, mouseDownEvent, new string[] {
					"CQG NASDAQ >",
					"",
					"CQG NYSE >",
                    //" ",
                    //"CQG TSX >",
                    //" ",
                    //"CQG BATS >",
                    //" ",
                    //"CQG BORSA ITALIAN >",
                    //" ",
                    //"CQG Chi-X >",
                    //"",
                    //"CQG JSE >",
                    //" ",
                    //"CQG MOSCOW >",
                    //"",
                    //"CQG ASX >",
                    //"",
                    //"CQG SGX >",
                });
			}
			else if (continent == "CQG ETF >")
			{
				setNavigation(panel, mouseDownEvent, new string[] {
                    //"NYSE INDEX ETF >",
                    //"",
                    //"NYSE SECTOR ETF >",
                    //" ",
                    "CQG SINGLE CTY ETF >",
					" ",
					"CQG SECTOR ETF >",
					" ",
					"CQG SPY ETF >",
				});
			}
			else if (continent == "CQG FX & CRYPTO >")
			{
				setNavigation(panel, mouseDownEvent, new string[] {
					"CQG WORLD CURRENCY >",
                    // "",
                    //"CQG CRYPTO CURRENCY >",
                     "",
					"CQG CRYPTO FUTURES >",                 
                    //"",
                    //"CQG CRYPTO INDICES >",
                });
			}
			else if (continent == "CQG INTEREST RATES >")
			{
				setNavigation(panel, mouseDownEvent, new string[] {
					"CQG N AMERICA RATES >",
				});
			}
			else if (continent == "CQG STOCK INDICES >")
			{
				setNavigation(panel, mouseDownEvent, new string[] {
					"CQG N AMERICA STK INDICES >",
                    //" ",
                    //"CQG S AMERICA STK INDICES >"
                });
			}
			else if (continent == "ALPHA NETWORKS >")
			{
				setNavigation(panel, mouseDownEvent, new string[] {
					"CMR",
					"",
					"SQ PT",
				});
			}
			else if (continent == "FX")
			{
				setNavigation(panel, mouseDownEvent, new string[] { "FX SPOT", "FX CROSS" });
			}
			else if (continent == "GLOBAL FI YLD")
			{
				setNavigation(panel, mouseDownEvent, new string[] { "30 YR", "10 YR", "5 YR" });
			}
			else if (continent == "GLOBAL ETF")
			{
				setNavigation(panel, mouseDownEvent, new string[] { "US ETF", "US SECTOR ETF", "SINGLE CTY ETF" });
			}
			else if (continent == "GLOBAL SOVR CDS")
			{
				setNavigation(panel, mouseDownEvent, new string[] { "SOVR CDS" });
			}
			else if (continent == "FINANCIAL")
			{
				setNavigation(panel, mouseDownEvent, new string[] { "US FINANCIAL" });
			}
			else if (continent == "COMMUNICATIONS >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[]
				{
					"ADVERTISING >", " ", "CABLE | SATELLITE >", " ", "ENTERTAINMENT >", " ", "INTERNET MEDIA >", " ", "PUBLISHING >", " ", "WIRELESS TELECOM >", " ", "WIRELINE TELECOM >"
				});
				else setNavigation(panel, mouseDownEvent, new string[]
				{
					"ADVERTISING", " ", "CABLE | SATELLITE", " ", "ENTERTAINMENT", " ", "INTERNET MEDIA", " ", "PUBLISHING", " ", "WIRELESS TELECOM", " ", "WIRELINE TELECOM"
				});
				ok = true;
			}
			else if (continent == "CONS DISC >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[]
				{
					"AIRLINES >", " ", "APPAREL | TEXTILE PRODUCTS >", " ", "AUTOMOBILES >", " ", "AUTO PARTS >", " ", "CASINO | GAMING >", " ", "DISTRIBUTORS >", " ", "EDUCATIONAL SERVICES >", " ", "HOME FURNISHING >", " ", "HOME BUILDING >", " ", "HOME IMPROVEMENTS >", " ", "LESIURE PRODUCTS >", " ",  "LODGING >", " ", "RESTAURANTS >"
				});
				else setNavigation(panel, mouseDownEvent, new string[]
				{
					"AIRLINES", " ", "APPAREL | TEXTILE PRODUCTS", " ", "AUTOMOBILES", " ", "AUTO PARTS", " ", "CASINO | GAMING", " ", "DISTRIBUTORS", " ", "EDUCATIONAL SERVICES", " ", "HOME FURNISHING", " ", "HOME BUILDING", " ", "HOME IMPROVEMENTS", " ", "LESIURE PRODUCTS", " ",  "LODGING", " ", "RESTAURANTS"
				});
				ok = true;
			}
			else if (continent == "CONS STAPLES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[]
				{
					"AGRICULTURAL >", " ", "BEVERAGES >", " ", "FOOD >", " ", "HOUSEHOLD PRODUCTS >", " ", "SUPERMARKETS >", " ", "TABACCO >"
				});
				else setNavigation(panel, mouseDownEvent, new string[]
				{
					"AGRICULTURAL", " ", "BEVERAGES", " ", "FOOD", " ", "HOUSEHOLD PRODUCTS", " ", "SUPERMARKETS", " ", "TABACCO"
				});
				ok = true;
			}
			else if (continent == "ENERGY >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[]
				{
					"EQUIPMENT | SERVICES >"," ",  "EXPLORATION | PRODUCTS >", " ", "INTEGRATED OIL | GAS >", " ", "REFINING | MARKETING >"
				});
				else setNavigation(panel, mouseDownEvent, new string[]
				{
					"EQUIPMENT | SERVICES"," ",  "EXPLORATION | PRODUCTS", " ", "INTEGRATED OIL | GAS", " ", "REFINING | MARKETING"
				});
				ok = true;
			}
			else if (continent == "FINANCIALS >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[]
				{
					"BANKING >", " ", "COMMERCIAL FINANCE >", " ", "CONSUMER FINANCE >", " ", "LIFE INSURANCE >", " ", "INVESTMENT BANKING >", " ", "PROPERTY | CASUALTY >", " ", "REAL ESTATE >"
				});
				else setNavigation(panel, mouseDownEvent, new string[]
				{
					"BANKING", " ", "COMMERCIAL FINANCE", " ", "CONSUMER FINANCE", " ", "LIFE INSURANCE", " ", "INVESTMENT BANKING", " ", "PROPERTY | CASUALTY", " ", "REAL ESTATE"
				});
				ok = true;
			}
			else if (continent == "HEALTH CARE >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[]
				{
					"HEALTH CARE FACILITIES | SERVICES >", " ","HEALTH CARE SERVICES >", " ", "MANAGED CARE >", " ", "MEDICAL EQUIP >", " ", "MEDICAL SUPPLIES >", " ", "PHARMACEUTICALS >"
				});
				else setNavigation(panel, mouseDownEvent, new string[]
				{
					"HEALTH CARE FACILITIES | SERVICES", " ","HEALTH CARE SERVICES", " ", "MANAGED CARE", " ", "MEDICAL EQUIP", " ", "MEDICAL SUPPLIES", " ", "PHARMACEUTICALS"
				});
				ok = true;
			}
			else if (continent == "INDUSTRIES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[]
				{
					"AEROSPACE | DEFENSE >", " ", "ELECTRICAL EQUIP >", " ", "MACHINERY >", " ", "RAILROAD >", " ", "TRANSPORTATION | LOGISTICS >"
				});
				else setNavigation(panel, mouseDownEvent, new string[]
				{
					"AEROSPACE | DEFENSE", " ", "ELECTRICAL EQUIP", " ", "MACHINERY", " ", "RAILROAD", " ", "TRANSPORTATION | LOGISTICS"
				});
				ok = true;
			}
			else if (continent == "MATERIALS >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[]
				{
					"CHEMICALS >", " ", "CONSTRUCTION MATERIALS >", " ", "FOREST | PAPER PRODUCTS >", " ", "METALS | MINING >"
				});
				else setNavigation(panel, mouseDownEvent, new string[]
				{
					"CHEMICALS", " ", "CONSTRUCTION MATERIALS", " ", "FOREST | PAPER PRODUCTS", " ", "METALS | MINING"
				});
				ok = true;
			}
			else if (continent == "TECHNOLOGY >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[]
				{
					"COMMUNICATION EQUIP >", " ", "COMPUTER HARDWRE >", " ", "SEMICONDUCTORS >", " ", "SOFTWARE | SERVICES >"
				});
				else setNavigation(panel, mouseDownEvent, new string[]
				{
					"COMMUNICATION EQUIP", " ", "COMPUTER HARDWRE", " ", "SEMICONDUCTORS", " ", "SOFTWARE | SERVICES"
				});
				ok = true;
			}
			else if (continent == "UTILITIES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[]
				{
					"UTILITIES >"
				});
				else setNavigation(panel, mouseDownEvent, new string[]
				{
					"UTILITIES"
				});
				ok = true;
			}
			else if (continent == "GLOBAL MACRO >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[]
				{
					"GLOBAL GENERIC RATES >",
					"",
					"GLOBAL INDICES",
					"",
					"SINGLE CTY ETF >",
					"",
					"SPOT FX >",
				});
				else setNavigation(panel, mouseDownEvent, new string[]
				{
					"GLOBAL GENERIC RATES >",
					"",
					"GLOBAL INDICES >",
					"",
					"SINGLE CTY ETF",
					"",
					"SPOT FX",
				});
				ok = true;
			}
			else if (continent == "TREAS RATES | GLOBAL >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"N AMERICA RATES >",
					"",
					"S AMERICA RATES >",
					"",
					"EUROPE RATES >",
					"",
					"MEA RATES >",
					"",
					"ASIA RATES >",
					"",
					"OCEANIA RATES >",
                    //" ",
                    //"WORLD BOND FUTURES >",
                });
			}
			else if (continent == "US MUNI BONDS >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"GO >",
					"",
					"GO TAXABLE >",
					"",
					"REVENUE >",
					"",
					"REVENUE TAXABLE >",
					"",
					"MUNI SPREADS >",
				});
			}
			else if (continent == "CREDIT | INDUSTRY >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"COMMUNICATION CREDIT >",
					"",
					"CONS DISC CREDIT >",
					"",
					"CONS STAPLES CREDIT >",
					"",
					"ENERGY CREDIT >",
					"",
					"FINANCIALS CREDIT >",
					"",
					"HEALTH CARE CREDIT >",
					"",
					"INDUSTRIAL CREDIT >",
					"",
					"MATERIALS CREDIT >",
					"",
					"TECHNOLOGY CREDIT >",
					"",
					"UTIL CREDIT >",
				});
			}

			else if (continent == "GENERIC FOREST >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"FOREST >",
				});
			}
			else if (continent == "GLOBAL CDS >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"SOVR CDS >",
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"SOVR CDS",
				});
				ok = true;
			}

			else if (continent == "WEI >")
			{

				setNavigation(panel, mouseDownEvent, new string[]
				{
					"GLOBAL INDICES"
				});
			}

			else if (continent == "EQ | AMERICAS >" || continent == "AMERICAS >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
				   "SPTSX:CANADA >",
				   "SPX:USA >",
                    //" ",
                    //"SINGLE CTY ETF >",
                    //" ",
                    //"WORLD EQUITY FUTURES >"
                });
			}

			else if (continent == "IND | COMMUNICATIONS >" || continent == "IND |COMMUNICATIONS")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"S5ADVT Index::ADVERTISING | MARKETING", //S5ADVT
                    "S5CBST Index::CABLE | SATELLITE",  // S5CBST
                    "S5MEDA Index::ENTERTAINMENT CONTENT",  //S5MEDA
                    "S5INRE Index::INTERNET MEDIA",  //S5INRE
                    "S5PUBL Index::PUBLISHING | BROADCASTING",  //S5PUBL
                    " ",
					"S5TELS Index::TELECOM CARRIERS"   //S5TELS
                });
			}

			else if (continent == "IND | CONSUMER DISC >" || continent == "IND | CONSUMER DISC")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"S5COND Index::CONSUMER DISC", //S5COND
                    " ",
					 "S6SUTP Index::AUTO PARTS", //S6SUTP
                     //"S5AUTO Index::AUTOMOBILES", //S5AUTO
                     " ",
					 "S5HOME Index::HOMEBUILDERS", //S5HOME Index
                     " ",
					 "S5TEXA Index::LUXURY GOODS",  //S5TEXA Index
                     " ",
					 "S6APAC Index::SPECIALTY APPAREL", //S6APAC
                     " ",
                     //"S5CASI Index::CASINOS | GAMING", //S5CASI
                     //"S6HOTL Index::LODGING",  //S6HOTL
                     "S6REST Index::RESTAURANTS"  //S6REST

                });
			}
			else if (continent == "IND | CONSUMER STAPLES >" || continent == "IND | CONSUMER STAPLES")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
				   "S5AGRI Index::AGRICULTURE", //S5AGRI
                   "S5BEVG Index::BEVERAGES", //S5BEVG
                   "S5CONS Index::CONSUMER STAPLES",  //S5CONS
                   "S6HOUS Index::HOUSEHOLD PRODUCTS", //S6HOUS
                   "S6PACK Index::PACKAGED FOOD",  //S6PACK
                   "S6TOBAX Index::TOBACCO | CANNABIS",  //S6TOBAX
                     " ",
				   "S6RETL Index::RETAIL STAPLES | WHOLESALE" //S6RETL
                });
			}
			else if (continent == "IND | ENERGY >" || continent == "IND | ENERGY")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"S6ENRS Index::CRUDE OIL | NATURAL GAS",  //S6ENRS
                    "S6OILP Index::CRUDE OIL PRODUCTION", //S6OILP
                    "S5IOIL Index::INTEGRATED OILS",  //S5IOIL Index
                    "S6OILG Index::LIQUEFIED NATURAL GAS", //S6OILG
                    "S5OILP Index::MIDSTREAM OIL | GAS",  //S5OILP Index
                    "S6OILE Index::OIL | GAS SERVICES", //S6OILE
                    "S6OILR Index::REFINING | MARKETING",  //S6OILR
                    " ",
					"SOLAR Index::SOLAR ENERGY EQUIPMENT", //SOLAR Index
                    "WIND Index::WIND ENERGY EQUIPMENT"   //WIND Index
                });
			}
			else if (continent == "IND | FINANCIALS >" || continent == "IND | FINANCIALS")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"MDINVM Index::INVESTMENT MGMT",  //S6INVBB
                     " ",
					"S5BANKX Index::BANKING",  //S5BANKX
                     " ",
					 "FDSFINT Index::FINTECH",  //FDSFINT Index
                     " ",
					"S5INBK Index::INVESTMENT BANKING",  //S5INBK Index
                    " ",
					"S6INSB Index::INSURANCE",  //S6INSB
                    "S6LIFE Index::LIFE INSURANCE",   //S6LIFE
                    "S6PROP Index::P | C INSURANCE",  //S6PROP
                    " ",
					"S5CONF Index::CONSUMER FINANCE", //S5CONF
                });
			}
			else if (continent == "IND | HEALTH CARE >" || continent == "IND | HEALTH CARE")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"S5BIOTX Index::BIOTECH", //S5BIOTX
                    "S6PHAR Index::LARGE PHARMA",   //S6PHAR
                    " ",
					"S6HLTH Index::HEALTH CARE SUPPLY CHAIN",  //S6HLTH
                    "S6MANH Index::MANAGED CARE",  //S6MANH
                    " ",
					"DJSMDQT Index::LIFE SCIENCE EQUIP",   //DJSMDQT Index
                    "DJSMDQT Index::MEDICAL EQUIP"    //DJSMDQT Index
                });
			}
			else if (continent == "IND | INDUSTRIALS >" || continent == "IND | INDUSTRIALS")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"S6AERO Index::AEROSPACE | DEFENSE",  //S6AERO
                     " ",
					"MXWO0CJ Index::CONSTRUCTION",  //MXWO0CJ Index
                     " ",
					"S6ELEQ Index::ELECTRICAL EQUIPMENT",  //S6ELEQ
                     " ",
					"S5INDU Index::INDUSTRIALS",  //S5INDU
                     " ",
					"S5INDM Index::MACHINERY",  //S5INDM
                     " ",
					"S5AIRL Index::AIRLINES", //S5AIRL
                     " ",
					"S5AIRFX Index::LOGISTICS SERVICES", // S5AIRFX
                    "S5MARIX Index::MARINE SHIPPING",  //S5MARIX
                    "S6RAIL Index::RAIL FREIGHT", //S6RAIL
                    "S5TRUC Index::TRUCKING",  //S6TRUC
                });
			}
			else if (continent == "IND | MATERIALS >" || continent == "IND | MATERIALS")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{

					"S5FERT Index::AGRICULTURAL CHEMICALS",   //S5FERT
                    "S5DIVC Index::BASIC | DIVERSIFIED CHEMICALS",  //S5DIVC
                    "S5SPCH Index::SPECIALTY CHEMICALS",  //S5SPCH
                    " ",
					"BUSBUIL Index::BUILDING MATERIALS",  //BUSBUIL Index
                    " ",
					"S5PAFO Index::PAPER | PACKAGING | FOREST PRODUCTS",  //S5PAFO Index
                    " ",
					"S5STEL Index::STEEL PRODUCERS",  //S5STEL Index
                    " ",
					"S5ALUM Index::ALUMINUM",  //S5ALUM Index
                    "S5METL Index::BASE METALS", //S5METL Index
                    "DJMCCOU Index::COAL", //DJMCCOU Index
                    "S5COPP Index::COPPER", //S5COPP Index
                    "BWIRON Index::IRON",  //BWIRON Index
                    "S5PRMETR Index::PRECIOUS METAL MINING"  //S5PRMETR Index
                });
			}
			else if (continent == "IND | REAL ESTATE >" || continent == "IND | REAL ESTATE")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
				   "S5HCRT Index::HEALTH CARE REIT",  //S5HCRT Index
                   "BUORT Index::OFFICE REIT",  //BUORT Index
                   "S5REIT Index::REIT",   //S5REIT Index
                   "S5RERE Index::RESIDENTIAL REIT",  //S5RERE Index
                   "S5RERT Index::RETAIL REIT",  //S5RERT Index
                });
			}
			else if (continent == "IND | TECHNOLOGY >" || continent == "IND | TECHNOLOGY")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"S5CMHW Index::COMPUTER HARDWARE", //S5CMHW Index
                    "S5CMER Index::CONSUMER ELECTRONICS", //S5CMER Index
                    "S5NETW Index::DATA NETWORKING EQUIP",  //S5NETW Index
                     " ",
					"S5SEMI Index::SEMICONDUCTOR DEVICES",   // BISEMI Index
                    "SOX Index::SEMICONDUCTOR MFG",
					 " ",
					"S5APLS Index::APPLICATION SOFTWARE",   // S5APLS Index
                    "BCSUINFS Index::INFRASTRUCTURE SOFTWARE",  //BCSUINFS Index
                     " ",
					"S5ITSV Index::IT SERVICES"
				});
			}
			else if (continent == "IND | UTILITIES >" || continent == "UTILITIES")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
				   "S5ELUTX Index::ELECTRIC UTILITIES",  // S5ELUTX Index
                });
			}
			else if (continent == "US INDUSTRIES >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
				   "SPXL3:SPX >",
				   "MIDL3:MID >",
				   "SMLL3:SML >",
				   "RAYL3:RAY >",
				});
			}
			else if (continent == "CANADIAN INDUSTRIES >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
				   "SPTSX:SPTSX >"
				});
			}
			else if (continent == "N AMERICA EQ >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"SPX:USA >",
					"SPTSX:CANADA >",
					"MEXBOL:MEXICO >"
				});
			}
			else if (continent == "S AMERICA EQ >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"MERVAL:ARGENTINA",
					"IBOV:BRAZIL >",
					"IPSA:CHILE >",
					"COLCAP:COLOMBIA >",
					"SPBL25PT:PERU >"
				});

			}
			else if (continent == "EUROPE EQ >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"UKX:UK >",
					"CAC:FRANCE >",
					"DAX:GERMANY >",
					"ASE:GREECE >",
					"FTSEMIB:ITALY >",
					"IMCEX:RUSSIA >",
					"IBEX:SPAIN >",
					"SMI:SWITZERLAND >",
					"OBX:NORWAY >",
					"OMX:SWEDEN >",
					"HEX:FINLAND >",
					"ATX:AUSTRIA >",
					"BEL20:Belgium",
					"SASX10:Bosnia",
					"SOFIX:Bulgaria",
					"CYSMMAPA:Cyprus",
					"CRO:Croatia",
					"PX:CzechRepublic",
					"KFX:DENMARK >",
					"TALSE:Estonia",
					"VILSE:Lithuania",
					"BUX:HUNGARY >",
					"ISEQ:IRELAND >",
					"ICEXI:Iceland",
					"AEX:NETHERLANDS >",
					"WIG:POLAND >",
					"BVLX:PORTUGAL >",
					"BET:Romania",
					"BELEX15:Serbia",
					"XU100:TURKEY >"
				});

			}
			else if (continent == "MEA EQ >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"JALSH:SOUTH AFRICA >",
					"HERMES:EGYPT >",
					"SECTMIND:KUWAIT >",
					"SASEIDX:SAUDI ARABIA >",
					"BHSEASI:Bahrain >",
					"JOSMGNFF:Jordan >",
					"MSM30:Oman >",
					"DSM:Qatar >",
					"DFMGI:UAE >" });
			}
			else if (continent == "ASIA EQ >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"SHCOMP:CHINA >",
					"HSI:HONG KONG >",
					"NIFTY:INDIA >",
					"JCI:INDONESIA >",
					"NKY:JAPAN >",
					"FBMKLCI:MALAYSIA >",
					"PCOMP:Philippines",
					"FSSTI:SINGAPORE >",
					"KOSPI:SOUTH KOREA >",
					"TWSE:TAIWAN >",
					"SET:THAILAND >",
					"VHINDEX:VIETNAM >",
					"KZKAK:Kazakhstan",
					"LSXC:Laos",
					"MSETOP:Mongolia",
					"KSE100:PAKISTAN >"
				});

			}
			else if (continent == "OCEANIA EQ >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"AS51:AUSTRALIA >",
					"NZSE:NEW ZEALAND >"
				});

			}
			else if (continent == "WORLD >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"BWORLD:WORLD >",
					"BWORLDUS:AMERICAS >",
					"BWORLDEU:EMEA >",
					"BWORLDPR:ASIA PACIFIC >"
				});
			}

			else if (continent == "FACTOR PORTS >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"PDIVYUS:DIVIDENDS",
					"PGRWTHUS:GROWTH",
					"PLEVERUS:LEVERAGE",
					"PMOMENUS:MOMENTUM",
					"PPROFTUS:PROFITABILTY",
					"PSIZEUS:SIZE",
					"PTRADEUS:TRADE ACTIVITY",
					"PVALUEUS:VALUE",
					"PEARNVUS:VARIABILITY",
					"PVOLAUS:VOLATILITY"
				});
			}

			else if (continent == "EQ | EUROPE & MEA >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"UKX:UK >",
					"",
					"EURO EQ >",
					"",
					"HEX:FINLAND >",
					"CAC:FRANCE >",
					"DAX:GERMANY >",
					"FTSEMIB:ITALY >",
					"BVLX:PORTUGAL >",
					"IBEX:SPAIN >",
					"OMX:SWEDEN >",
					"SMI:SWITZERLAND >",
					"SECTMIND:KUWAIT >",
					"SASEIDX:SAUDI ARABIA >",
					"DSM:QATAR >",
					"DFMGI:UAE >",
					"",
					"JALSH:SOUTH AFRICA >",
                    //" ",
                    //"SINGLE CTY ETF >",
                    //" ",
                    //"WORLD EQUITY FUTURES >"
                });

			}

			else if (continent == "EQ | ASIA PACIFIC >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"AS51:AUSTRALIA >",
					"",
					"SHCOMP:CHINA >",
					"HSI:HONG KONG >",
					"JCI:INDONESIA >",
					"NKY:JAPAN >",
					"FSSTI:SINGAPORE >",
					"KOSPI:SOUTH KOREA >",
					"TWSE:TAIWAN >",
					"SET:THAILAND >",
                    //" ",
                    //"SINGLE CTY ETF >",
                    //" ",
                    //"WORLD EQUITY FUTURES >"
                });
			}

			else if (continent == "US MUNI >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"MUNI GO >",
					"MUNI REVENUE >",
					"MUNI STATES >",
					"MUNI TAXABLE >"
				});
			}

			else if (continent == "NA CANADA >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0007 Index:CANADA"
				});
			}

			else if (continent == "NA US >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0025 Index:US"
				});
			}
			else if (continent == "NA MEXICO >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0251 Index:MEXICO"
				});
			}
			else if (continent == "SA ARGENTINA >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0757 Index:ARGENTINA"
				});
			}
			else if (continent == "SA BRAZIL >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0393 Index:BRAZIL"
				});
			}
			else if (continent == "SA CHILE >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0351 Index:CHILE"
				});
			}
			else if (continent == "SA COLOMBIA >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0217 Index:COLOMBIA"
				});
			}
			else if (continent == "SA PERU >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0361 Index:PERU"
				});
			}
			else if (continent == "EU ENGLAND >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0022 Index:ENGLAND"
				});
			}
			else if (continent == "EU FRANCE >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0014 Index:FRANCE"
				});
			}
			else if (continent == "EU GERMANY >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0016 Index:GERMANY"
				});
			}
			else if (continent == "EU ITALY >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0040 Index:ITALY",
				});
			}
			else if (continent == "EU AUSTRIA >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0063 Index:AUSTRIA",
				});
			}
			else if (continent == "EU BELGIUM >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0006 Index:BELGIUM",
				});
			}
			else if (continent == "EU CZECH REP >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0112 Index:CZECH REP",
				});
			}
			else if (continent == "EU DENMARK >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0011 Index:DENMARK",
				});
			}
			else if (continent == "EU EUROZONE >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0013 Index:EUOZONE",
				});
			}
			else if (continent == "EU FINLAND >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0081 Index:FINLAND",
				});
			}
			else if (continent == "EU GREECE >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0156 Index:GREECE",
				});
			}
			else if (continent == "EU HUNGARY >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0165 Index:HUNGARY",
				});
			}
			else if (continent == "EU IRELAND >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0162 Index:IRELAND",
				});
			}
			else if (continent == "EU NETHERLANDS >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0020 Index:NETHERLANDS",
				});
			}
			else if (continent == "EU POLAND >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0177 Index:POLAND",
				});
			}
			else if (continent == "EU PORTUGAL >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0084 Index:EORTUGAL",
				});
			}
			else if (continent == "EU ROMANIA >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0508 Index:ROMANIA",
				});
			}
			else if (continent == "EU SLOVAKIA >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0256 Index:SLOVAKIA",
				});
			}
			else if (continent == "EU SLOVENIA >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0259 Index:SLOVENIA",
				});
			}
			else if (continent == "EU SPAIN >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0061 Index:SPAIN",
				});
			}
			else if (continent == "EU SWEDEN >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0021 Index:SWEDEN",
				});
			}
			else if (continent == "MEA ISRAEL >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0325 Index:ISRAEL"
				});
			}
			else if (continent == "MEA SOUTH AFRICA >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0090 Index:SOUTH AFRICA",
				});
			}
			else if (continent == "AP AUSTRALIA >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0090 Index:AUSTRALIAC",
				});
			}
			else if (continent == "AP CHINA >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0299 Index:CHINA",
				});
			}
			else if (continent == "AP HONG KONG >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0095 Index:HONG KONG",
				});
			}
			else if (continent == "AP INDIA >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0180 Index:INDIA"
				});
			}
			else if (continent == "AP INDONESIA >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0266 Index:INDONESIA"
				});
			}
			else if (continent == "AP JAPAN >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0018 Index:JAPAN"
				});
			}
			else if (continent == "AP MALAYSIA >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0196 Index:MALAYSIA",
				});
			}
			else if (continent == "AP NEW ZEALAND >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0049 Index:NEW ZEALAND"
				});
			}
			else if (continent == "AP PAKISTAN >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0320 Index:PAKISTAN"
				});
			}
			else if (continent == "AP PHILIPPINES >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0105 Index:PHILIPPINES"
				});
			}
			else if (continent == "AP SINGAPORE >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0107 Index:SINGAPORE"
				});
			}
			else if (continent == "AP SOUTH KOREA >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0173 Index:SOUTH KOREA"
				});
			}
			else if (continent == "AP TAIWAN >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0194 Index:TAIWAN"
				});
			}
			else if (continent == "AP THAILAND >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0200 Index:THAILAND"
				});
			}

			else if (continent == "HF2 >")
			{
				List<string> itemList = Portfolio.GetSQPTPortfolios(false);

				setNavigation(panel, mouseDownEvent, itemList.ToArray());
			}

			else ok = false;
			return ok;
		}

		private string[] _navigationPath = { "", "", "", "", "", "" };

		public bool setNavigationLevel2(string country, StackPanel panel, MouseButtonEventHandler mouseDownEvent, MouseButtonEventHandler go_Click)
		{
			_activeNavigationPathIndex = 2;
			_navigationPath[1] = country;
			_navigationPath[2] = "";
			_navigationPath[3] = "";
			_navigationPath[4] = "";
			_navigationPath[5] = "";

			panel.Children.Clear();

			string[] fields = country.Split(':');
			country = (fields.Length > 1) ? fields[1] : fields[0];

			bool ok = false;

			if (country == "SPX >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"SPXL3:SPX INDUSTRIES >",
				 });
				ok = true;
			}
			else if (country == "MID >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"MIDL3:MID INDUSTRIES >",
				 });
				ok = true;
			}
			else if (country == "SML >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"SMLL3:SML INDUSTRIES >",
				 });
				ok = true;
			}
			else if (country == "RAY >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"RAYL2:RAY INDUSTRIES >",
				 });
				ok = true;
			}
			else if (country == "SPTSX >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"SPTSX | INDUSTRIES >:CANADIAN INDUSTRIES >",
				 });
				ok = true;
			}
			else if (country == "N AMERICA EQ >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"SPX:USA EQ >",
					"",
					"SPTSX:CANADA EQ >",
					"",
					"MEXBOL:MEXICO EQ >"
				});

				ok = true;
			}
			else if (country == "S AMERICA EQ >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"MERVAL:ARGENTINA EQ >",
					"",
					"IBOV:BRAZIL EQ >",
					"",
					"IPSA:CHILE EQ >",
					"",
					"COLCAP:COLOMBIA EQ",
					"",
					"SPBL25PT:PERU EQ >"
				});

				ok = true;
			}
			else if (country == "EUROPE EQ >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"SXXP:EURO EQ >",
					"",
					"UKX:UK EQ >",
					"",
					"CAC:FRANCE EQ >",
					"",
					"DAX:GERMANY EQ >",
					"",
					"ASE:GREECE EQ >",
					"",
					"FTSEMIB:ITALY EQ >",
					"",
					"IMOEX:RUSSIA EQ >",
					"",
					"IBEX:SPAIN EQ >",
					"",
					"SMI:SWITZERLAND EQ >",
					"",
					"OBX:NORWAY EQ >",
					"",
					"OMX:SWEDEN EQ >",
					"",
					"HEX:FINLAND EQ >",
					"",
					"ATX:AUSTRIA EQ >",
					"",
					"BEL20:BELGIUM EQ",
					"",
					"SASX10:BOSNIA EQ",
					"",
					"SOFIX:BULGARIA EQ",
					"",
					"CYSMMAPA:CYPRUS EQ",
					"",
					"CRO:CROATIA EQ",
					"",
					"PX:CZECH REP EQ",
					"",
					"KFX:DENMARK EQ >",
					"",
					"TALSE:ESTONIA EQ",
					"",
					"VILSE:LITHUANIA EQ",
					"",
					"BUX:HUNGARY EQ >",
					"",
					"ISEQ:IRELAND EQ >",
					"",
					"ICEXI:ICELAND EQ",
					"",
					"AEX:NETHERLANDS EQ >",
					"",
					"WIG:POLAND EQ >",
					"",
					"BVLX:PORTUGAL EQ >",
					"",
					"BET:ROMANIA EQ",
					"",
					"BELEX15:SERBIA EQ",
					"",
					"XU100:TURKEY EQ >"
				});
				ok = true;
			}
			else if (country == "MEA EQ >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"TA-125:ISRAEL EQ >",
					"",
					"JALSH:SOUTH AFRICA EQ >",
					"",
					"HERMES:EQYPT EQ",
					"",
					"SECTMIND:KUWAIT EQ >",
					"",
					"SASEIDX:SAUDI ARABIA EQ >",
					"",
					"BHSEASI:BAHRAIN EQ",
					"",
					"JOSMGNFF:JORDAN EQ",
					"",
					"MSM30:OMAN EQ",
					"",
					"DSM:QATAR EQ",
					"",
					"DFMGI:UAE EQ >" });

				ok = true;
			}
			else if (country == "ASIA EQ >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"SHCOMP:CHINA EQ >",
					"",
					"HSI:HONG KONG EQ >",
					"",
					"NIFTY:INDIA EQ >",
					"",
					"JCI:INDONESIA EQ >",
					"",
					"NKY:JAPAN EQ >",
					"",
					"FBMKLCI:MALAYSIA EQ >",
					"",
					"PCOMP:PHILIPPINES EQ",
					"",
					"FSSTI:SINGAPORE EQ >",
					"",
					"KOSPI:SOUTH KOREA EQ >",
					"",
					"TWSE:TAIWAN EQ >",
					"",
					"SET:THAILAND EQ >",
					"",
					"VHINDEX:VIETNAM EQ",
					"",
					"KZKAK:KAZAKHSTAN EQ",
					"",
					"LSXC:LAOS EQ",
					"",
					"MSETOP:MONGOLIA EQ",
					"",
					"KSE100:PAKISTAN EQ"
				});
				ok = true;
			}
			else if (country == "OCEANIA EQ >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"AS51:AUSTRALIA EQ >",
					"",
					"NZSE:NEW ZEALAND EQ >"
				});
				ok = true;
			}
			else if (country == "WORLD >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"BWORLD:WORLD >",
					"BWORLDUS:AMERICAS >",
					"BWORLDEU:EMEA >",
					"BWORLDPR:ASIA PACIFIC >"
				});
				ok = true;
			}
			else if (country == "FACTOR PORTS >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"PDIVYUS:DIVIDENDS",
					"PGRWTHUS:GROWTH",
					"PLEVERUS:LEVERAGE",
					"PMOMENUS:MOMENTUM",
					"PPROFTUS:PROFITABILTY",
					"PSIZEUS:SIZE",
					"PTRADEUS:TRADE ACTIVITY",
					"PVALUEUS:VALUE",
					"PEARNVUS:VARIABILITY",
					"PVOLAUS:VOLATILITY"
				});
				ok = true;
			}
			else if (country == "MUNI GO >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"YCGT0493 Index:GO AAA",
					"BVSC1212 Index:GO AA",
					"BVSC1213 Index:GO A",
				});

				ok = true;
			}
			else if (country == "MUNI REVENUE >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"BVSC1270 Index:Higher Ed AAA",
					"BVSC1269 Index:Higher Ed AA",
					"BVSC1268 Index:Higher Ed A",
					"BVSC1333 Index:Higher Ed BBB",
					"",
					"BVSC1275 Index:Hospitals AA",
					"BVSC1274 Index:Hospitals A",
					"BVSC1276 Index:Hospitals BBB",
					"",
					"BVSC1318 Index:Lease Rev AA",
					"BVSC1315 Index:Lease Rev A",
					"",
					"BVSC1256 Index:Public Power AA",
					"BVSC1291 Index:Public Power AA-",
					"",
					"BVSC1320 Index:Rev AAA",
					"BVSC1271 Index:Rev AA",
					"BVSC1208 Index:Rev A",
					"",
					"BVSC1263 Index:Transportation A",
					"",
					"BVSC1283 Index:Water|Sewer AAA",
					"BVSC1306 Index:Water|Sewer AA",
					"BVSC1303 Index:Water|Sewer A",
				});

				ok = true;
			}
			else if (country == "MUNI TAXABLE >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"BVSC1299 Index:GO  AAA",
					"BVSC1298 Index:REV AAA",
				});

				ok = true;
			}

			else if (country == "MUNI STATES >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"BVIS9163 Index:ARKANSAS",
					"BVIS9000 Index:CALIFORNIA",
					"BVIS9015 Index:CONNECTICUT",
					"BVIS9005 Index:FLORIDA",
					"BVIS9012 Index:GEORGIA",
					"BVIS9028 Index:HAWAII",
					"BVIS9003 Index:ILLINOIS",
					"BVIS9157 Index:LOUISIANA",
					"BVIS9014 Index:MARYLAND",
					"BVIS9008 Index:MASSACHUSETTS",
					"BVIS9007 Index:MICHIGAN",
					"BVIS9017 Index:MINNESOTA",
					"BVIS9055 Index:NEVEDA",
					"BVIS9205 Index:NEW HAMPSHIRE",
					"BVIS9006 Index:NEW JERSEY",
					"BVIS9001 Index:NEW YORK",
					"BVIS9018 Index:NORTH CAROLINA",
					"BVIS9011 Index:OHIO",
					"BVIS9029 Index:OREGON",
					"BVIS9004 Index:PENNSYLVANIA",
					"BVIS9207 Index:RHODE ISLAND",
					"BVIS9019 Index:SOUTH CAROLINA",
					"BVIS9021 Index:TENNESSEE",
					"BVIS9002 Index:TEXAS",
					"BVIS9218 Index:UTAH",
					"BVIS9013 Index:VIRGINIA",
					"BVIS9010 Index:WASHINGTON",
					"BVIS9010 Index:WISCONSIN"

			});

				ok = true;
			}

			//else if (country == "US COMMODITIES >" || country == "US COMMODITIES")
			//{
			//    if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
			//        "CBOT FINANCIALS >",
			//        " ",
			//        "CBOT GRAINS >",
			//        " ",
			//        "CME STOCK INDICES >",
			//        " ",
			//        "CME RATES >",
			//        " ",
			//        "CME FX >",
			//        " ",
			//        "CME MEATS >",
			//        " ",
			//        "CME LUMBER >",
			//        " ",
			//        "CME DAIRY >",
			//        " ",
			//        "COMEX METALS >",
			//        " ",
			//        "ICE SOFTS >",
			//        " ",
			//        "NYMEX ENERGY >",
			//        " ",
			//        "NYMEX METALS >",
			//     });

			//    else setNavigation(panel, mouseDownEvent, new string[] {
			//        "CBOT FINANCIALS",
			//        " ",
			//        "CBOT GRAINS",
			//        " ",
			//        "CME STOCK INDICES",
			//        " ",
			//        "CME RATES",
			//        " ",
			//        "CME FX",
			//        " ",
			//        "CME MEATS",
			//        " ",
			//        "CME LUMBER",
			//        " ",
			//        "CME DAIRY",
			//        " ",
			//        "COMEX METALS",
			//        " ",
			//        "ICE SOFTS",
			//        " ",
			//        "NYMEX ENERGY",
			//        " ",
			//        "NYMEX METALS",
			//     });

			//    ok = true;
			//}
			else if (country == "CQG US RATES >" || country == "CQG US RATES")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					 "CBOT US RATES >",
					 "CME US RATES >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					 "CBOT US RATES",
					 "CME US RATES"
				});

				ok = true;
			}


			else if (country == "US RATES >" || country == "US RATES")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					 "US GENERIC >",
					 "US CURVES >",
					 "US BUTTERFLIES >",
					 "US BREAKEVEN >",
					 "US CDS >",
					 "US SPREADS >",
					 "US SWAPS >",
					 " ",
					"US MORTGAGE RATES >",
					" ",
					"AAA MUNI RATES >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"US GENERIC",
					"US CURVES",
					"US BUTTERFLIES",
					"US BREAKEVEN",
					"US CDS",
					"US SPREADS",
					"US SWAPS",
					 " ",
					"US MORTGAGE RATES",
					" ",
					"AAA MUNI RATES"
				});

				ok = true;
			}

			else if (country == "CANADA RATES >" || country == "CANADA RATES")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"CANADA GENERIC >",
					"CANADA CURVES >",
					"CANADA BUTTERFLIES >",
					"CANADA BREAKEVEN >",
					"CANADA CDS >",
					"CANADA SPREADS >",
					"CANADA SWAPS >",
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"CANADA GENERIC",
					"CANADA CURVES",
					"CANADA BUTTERFLIES",
					"CANADA BREAKEVEN",
					"CANADA CDS",
					"CANADA SPREADS",
					"CANADA SWAPS",
				});

				ok = true;
			}

			else if (country == "MUNI BOND INDEX >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"MATURITY",
					"TYPE",
					"QUALITY",
					"MANAGED MONEY",
					"STATE INDICES",
					"CUSTOM"
				});

				ok = true;
			}

			else if (country == "MUNI HIGH YLD >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"HY MATURITY",
					"HY SECTOR",
					"HY COMPOSITES",
					"HY CAPPED"
				});

				ok = true;
			}

			else if (country == "TAXABLE MUNI BOND INDEX >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"TAXABLE MATURITY"
				});

				ok = true;
			}

			else if (country == "CANADA RATES >" || country == "CANADA RATES")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"CANADA GENERIC >",
					"CANADA CURVES >",
					"CANADA BUTTERFLIES >",
					"CANADA BREAKEVEN >",
					"CANADA CDS >",
					"CANADA SPREADS >",
					"CANADA SWAPS >",
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"CANADA GENERIC",
					"CANADA CURVES",
					"CANADA BUTTERFLIES",
					"CANADA BREAKEVEN",
					"CANADA CDS",
					"CANADA SPREADS",
					"CANADA SWAPS",
				});

				ok = true;
			}

			else if (country == "MEXICO RATES >" || country == "MEXICO RATES")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"MEXICO GENERIC >",
					"MEXICO BREAKEVEN >",
					"MEXICO CDS >",
					"MEXICO TIIE SWAPS >",
					"MEXICO TIIE LIBOR SWAPS >",
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"MEXICO GENERIC",
					"MEXICO BREAKEVEN",
					"MEXICO CDS",
					"MEXICO TIIE SWAPS",
					"MEXICO TIIE LIBOR SWAPS",
				});

				ok = true;
			}


			else if (country == "MEDIA >" || country == "MEDIA")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"ADVERTISING | MARKETING >",
					"CABLE | SATELLITE >",
					"ENTERTAINMENT CONTENT >",
					"INTERNET MEDIA >",
					"PUBLISHING | BROADCASTING >",
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"ADVERTISING | MARKETING",
					"CABLE | SATELLITE",
					"ENTERTAINMENT CONTENT",
					"INTERNET MEDIA",
					"PUBLISHING | BROADCASTING",
				});

				ok = true;
			}

			else if (country == "TELECOM >" || country == "TELECOM")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"TELECOM CARRIERS >",
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"TELECOM CARRIERS",
				});

				ok = true;
			}

			else if (country == "APPAREL | TEXTILE >" || country == "APPAREL | TEXTILE")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"CONSUMER DISCRETIONARY >",
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"CONSUMER DISCRETIONARY ",
				});

				ok = true;
			}

			else if (country == "AUTOMOTIVE >" || country == "AUTOMOTIVE")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"AUTO PARTS >", "AUTOMOBILES >",
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
				   "AUTO PARTS", "AUTOMOBILES",
				});

				ok = true;
			}
			//else if (country == "HOME | OFFICE PRODUCTS >" || country == "HOME | OFFICE PRODUCTS")
			//{
			//    if (UseCheckBoxes) setNavigationLevel1(panel, mouseDownEvent, new string[] {
			//        "HOMEBUILDERS >",
			//     });

			//    else setNavigationLevel1(panel, mouseDownEvent, new string[] {
			//       "HOMEBUILDERS",
			//    });

			//    ok = true;
			//}
			else if (country == "LUXURY >" || country == "LUXURY")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"LUXURY GOODS >",
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
				   "LUXURY GOODS",
				});

				ok = true;
			}
			else if (country == "RETAIL | DISC >" || country == "RETAIL | DISC")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"CONSUMBER HARDLINES >", "SPECIALTY APPAREL STORES >",
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"CONSUMBER HARDLINES", "SPECIALTY APPAREL STORES",
				});

				ok = true;
			}
			else if (country == "TRAVEL | LESIURE >" || country == "TRAVEL | LESIURE")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"CASINOS | GAMING >", "LODGING >", "RESTAURANTS >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"CASINOS | GAMING", "LODGING", "RESTAURANTS"
				});

				ok = true;
			}
			else if (country == "TRAVEL | LEISURE >" || country == "TRAVEL | LEISURE")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"CASINOS | GAMING >", "LODGING >", "RESTAURANTS >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"CASINOS | GAMING", "LODGING", "RESTAURANTS"
				});

				ok = true;
			}

			else if (country == "CONSUMER PRODUCTS >" || country == "CONSUMER PRODUCTS")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"AGRICULTURE >", "BEVERAGES >", "CONSUMER STAPLES >", "HOUSEHOLD PRODUCTS >", "PACKAGED FOOD >", "TOBACCO | CANNABIS >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"AGRICULTURE", "BEVERAGES", "CONSUMER STAPLES", "HOUSEHOLD PRODUCTS", "PACKAGED FOOD", "TOBACCO | CANNABIS"
				});

				ok = true;
			}
			else if (country == "RETAIL | CONSUMER STAPLES >" || country == "RETAIL | CONSUMER STAPLES")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"RETAIL STAPLES | WHOLESALE >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"RETAIL STAPLES | WHOLESALE"
				});

				ok = true;
			}
			else if (country == "OIL | GAS | COAL >" || country == "OIL | GAS | COAL")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"CRUDE OIL | NATURAL GAS >", "CRUDE OIL PRODUCTION >", "INTEGRATED OILS >", "LIQUEFIED NATURAL GAS >", "MIDSTREAM OIL | GAS >", "OIL | GAS SERVICES >", "REFINING | MARKETING >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"CRUDE OIL | NATURAL GAS", "CRUDE OIL PRODUCTION", "INTEGRATED OILS", "LIQUEFIED NATURAL GAS", "MIDSTREAM OIL | GAS", "OIL | GAS SERVICES", "REFINING | MARKETING"
				});

				ok = true;
			}
			else if (country == "RENEWABLE ENERGY >" || country == "RENEWABLE ENERGY")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"BIOFUELS >", "SOLAR ENERGY EQUIPMENT >", "WIND ENERGY EQUIPMENT >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"BIOFUELS", "SOLAR ENERGY EQUIPMENT", "WIND ENERGY EQUIPMENT"
				});

				ok = true;
			}
			else if (country == "ASSET MANAGEMENT >" || country == "ASSET MANAGEMENT")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"INVESTMENT MGMT >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"INVESTMENT MGMT"
				});

				ok = true;
			}
			else if (country == "BANKING >" || country == "BANKING")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"BANKING >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"BANKING"
				});

				ok = true;
			}
			else if (country == "INSTITUTIONAL FINANCIAL >" || country == "INSTITUTIONAL FINANCIAL")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"FIANCIAL EXCHANGES >", "INVESTMENT BANKING >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"FIANCIAL EXCHANGES", "INVESTMENT BANKING"
				});

				ok = true;
			}
			else if (country == "INSURANCE >" || country == "INSURANCE")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"FIANCIAL EXCHANGES >", "INVESTMENT BANKING >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"FIANCIAL EXCHANGES", "INVESTMENT BANKING"
				});

				ok = true;
			}
			else if (country == "INSURANCE >" || country == "INSURANCE")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"LIFE INSURANCE >", "P | C INSURANCE >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"LIFE INSURANCE", "P | C INSURANCE"
				});

				ok = true;
			}
			else if (country == "SPECIALTY FINANCE >" || country == "SPECIALTY FINANCE")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"CONSUMER FINANCE >", "FANNIE | FREDDIE >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"CONSUMER FINANCE", "FANNIE | FREDDIE"
				});

				ok = true;
			}
			else if (country == "BIOTECH | PHARMA >" || country == "BIOTECH | PHARMA")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"BIOTECH >", "DISEASE GROUPS >", "GARGE PHARMA >", "SPECIALTY | GENERIC PHARMA >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"BIOTECH", "DISEASE GROUPS", "GARGE PHARMA", "SPECIALTY | GENERIC PHARMA"
				});

				ok = true;
			}
			else if (country == "HEALTH CARE FACILITIES >" || country == "HEALTH CARE FACILITIES")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"HEALTH CARE SUPPLY CHAIN >", "HOSPITALS >", "MANAGED CARE >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"HEALTH CARE SUPPLY CHAIN", "HOSPITALS", "MANAGED CARE"
				});

				ok = true;
			}
			else if (country == "MED EQUIP | DEVICES >" || country == "MED EQUIP | DEVICES")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"LIFE SCIENCE EQUIP >", "MEDICAL EQUIP >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"LIFE SCIENCE EQUIP", "MEDICAL EQUIP"
				});

				ok = true;
			}
			else if (country == "AEROSPACE | DEFENSE >" || country == "AEROSPACE | DEFENSE")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"AEROSPACE | DEFENSE >", "DEFENSE PRIMES >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"AEROSPACE | DEFENSE", "DEFENSE PRIMES"
				});

				ok = true;
			}
			else if (country == "BUSINESS SERVICES >" || country == "BUSINESS SERVICES")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"BUSINESS SERVICES >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"BUSINESS SERVICES"
				});

				ok = true;
			}
			else if (country == "CONSTRUCTION >" || country == "CONSTRUCTION")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"CONSTRUCTION >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"CONSTRUCTION"
				});

				ok = true;
			}
			else if (country == "ELECTRICAL >" || country == "ELECTRICAL")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"ELECTRICAL EQUIPMENT >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"ELECTRICAL EQUIPMENT"
				});

				ok = true;
			}
			else if (country == "INDUSTRIAL DIVERSIFIED >" || country == "INDUSTRIAL DIVERSIFIED")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"INDUSTRIALS >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"INDUSTRIALS"
				});

				ok = true;
			}
			else if (country == "MACHINERY >" || country == "MACHINERY")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"MACHINERY ALL >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"MACHINERY ALL"
				});

				ok = true;
			}
			else if (country == "PASSENGER TRANSPORTATION >" || country == "PASSENGER TRANSPORTATION")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"AIRLINES >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"AIRLINES"
				});

				ok = true;
			}
			else if (country == "TRANSPORTATION | LOGISTICS >" || country == "TRANSPORTATION | LOGISTICS")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"LOGISTICS SERVICES >", "MARINE SHIPPING >", "RAIL FREIGHT >", "TRUCKING >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"LOGISTICS SERVICES", "MARINE SHIPPING", "RAIL FREIGHT", "TRUCKING"
				});

				ok = true;
			}
			else if (country == "TRANSPORTATION EQUIPMENT >" || country == "TRANSPORTATION EQUIPMENT")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"INFRASTRUCTURE >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"INFRASTRUCTURE"
				});

				ok = true;
			}
			else if (country == "CHEMICALS >" || country == "CHEMICALS")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"AGRICULTUREA CHEMICALS >", "BASIC | DIVERSIFIED CHEMICALS >", "SPECIALTY CHEMICALS >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"AGRICULTUREA CHEMICALS", "BASIC | DIVERSIFIED CHEMICALS", "SPECIALTY CHEMICALS"
				});

				ok = true;
			}
			else if (country == "CONSTRUCTION MATERIALS >" || country == "CONSTRUCTION MATERIALS")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"BUILDING MATERIALS >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"BUILDING MATERIALS"
				});

				ok = true;
			}
			else if (country == "CONTAINERS | PACKAGING >" || country == "CONTAINERS | PACKAGING")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"PAPER PACKAGING FOREST >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"PAPER PACKAGING FOREST"
				});

				ok = true;
			}
			else if (country == "IRON | STEEL >" || country == "IRON | STEEL")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"" +
					"" +
					"STEEL PRODUCERS >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"STEEL PRODUCERS"
				});

				ok = true;
			}
			else if (country == "METAL | MINING >" || country == "METAL | MINING")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"ALUMINUM >", "BASE METALS >", "COAL >", "COPPER >", "IRON >",  "PRECIOUS METAL MINING >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"ALUMINUM", "BASE METALS", "COAL", "COPPER", "IRON",  "PRECIOUS METAL MINING"
				});

				ok = true;
			}
			else if (country == "DESIGN | MFG | DISTRIBUTION >" || country == "DESIGN | MFG | DISTRIBUTION")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"EMS | ODM >",
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"EMS | ODM",
				});

				ok = true;
			}
			else if (country == "HARDWARE >" || country == "HARDWARE")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"COMPUTER HARDWARE >", "CONSUMER ELECTRONICS >", "DATA NETWORKING EQUIP >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"COMPUTER HARDWARE", "CONSUMER ELECTRONICS", "DATA NETWORKING EQUIP"
				});

				ok = true;
			}
			else if (country == "SEMICONDUCTORS >" || country == "SEMICONDUCTORS")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"SEMICONDUCTOR DEVICES >", "SEMICONDUCTOR MFG >",
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"SEMICONDUCTOR DEVICES", "SEMICONDUCTOR MFG",
				});

				ok = true;
			}
			else if (country == "SOFTWAARE >" || country == "SOFTWAARE")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"APPLICATION SOFTWARE >", "INFRASTRUCTURE SOFTWARE >",
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"APPLICATION SOFTWARE", "INFRASTRUCTURE SOFTWARE",
				});

				ok = true;
			}
			else if (country == "TECHNOLOGY SERVICES >" || country == "TECHNOLOGY SERVICES")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"IT SERVICES >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"IT SERVICES"
				});

				ok = true;
			}
			else if (country == "UTILITIES ALL >" || country == "UTILITIES ALL")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"ELECTRIC UTILITIES >",  "UTILITY NETWORKS >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"ELECTRIC UTILITIES",  "UTILITY NETWORKS"
				});

				ok = true;
			}
			else if (country == "US EQ >" || country == "USA >" || country == "USA")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {

					"OEX:OEX >",
					"SPX:SPX >",
					"OEXNDX:OEXNDX >",
					"CMR:CMR >",
                    //"SP:SP",
					"SPX:SPX | INDUSTRIES >",
					"MID:MID | INDUSTRIES >",
					"SML:SML | INDUSTRIES >",
					"NDX:NDX | INDUSTRIES >",
                    //"RAY:RAY | INDUSTRIES >",
                    //" ", 
                    //"US ETF >", 
                    //" ", 
                    //"US SECTOR ETF >", 
                    //" ", 
                    //"SPY ETF >"
                    });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"OEX:OEX",
					"CMR:CMR",
					"SPX:SPX | INDUSTRIES >",
                    //"SP",
					"SPXl1:SPXL1",
					"SPXl2:SPXL2",
					"SPXl3:SPXL3",
					"MID:MID | INDUSTRIES >",
					"MID:MIDL1 >",
					"MID:MIDL2 >",
					"MID:MIDL3 >",
					"SML:SML | INDUSTRIES >",
					"SML:SMLL1 >",
					"SML:SMLL2 >",
					"SML:SMLL3 >",
					"NDX:NDX | INDUSTRIES >",
					"NDX:NDXL1 >",
					"NDX:NDXL2 >",
					"NDX:NDXL3 >",
                    //"RAY:RAY | INDUSTRIES >",
                    //" ",
                    //"US ETF",
                    //" ",
                    //"US SECTOR ETF",
                    //" ",
                    //C
                    });
				ok = true;
			}
			else if (country == "ATM Conditions")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"ATM Trend Strength",
					"ATM Trend Condition",
					"ATM Trend Heat",
					"ATM Score" });

				ok = true;
			}

			else if (country == "SPOT >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] { "SPOT FX >" });
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"SPOT FX"
				});

				ok = true;
			}
			else if (country == "GLOBAL FI YLD")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"30 YR",
					"10 YR",
					"5 YR" });

				ok = true;
			}

			else if (country == "SINGLE CTY ETF >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"SPY US Equity::USA",
					"EWC US Equity::CANADA",
					"EWW US Equity::MEXICO",
					"EWZ US Equity::BRAZIL",
					"ARGT US Equity::ARGENTINA",
					"ECH US Equity::CHILE",
					"ICOL US Equity::COLUMBIA",
					"EPU US Equity::PERU",
					"EWQ US Equity::FRANCE",
					"EWG US Equity::GERMANY",
					"EWI US Equity::ITALY",
					"ERUS US Equity::RUSSIA",
					"EWP US Equity::SPAIN",
					"EWL US Equity::SWITZERLAND",
					"EWU US Equity::UK",
					"EWO US Equity::AUSTRIA",
					"EWK US Equity::BELGIUM",
					"EDEN US Equity::DENMARK",
					"EFNL US Equity::FINLAND",
					"GREK US Equity::GREECE",
					"EIRL US Equity::IRELAND",
					"EWN US Equity::NETHERLANDS",
					"ENOR US Equity::NORWAY",
					"EPOL US Equity::POLAND",
					"EWD US Equity::SWEDEN",
					"TUR US Equity::TURKEY",
					"EGPT US Equity::EGYPT",
					"EIS US Equity::ISRAEL",
					"EZA US Equity::SOUTH AFRICA",
					"INDY US Equity::INDIA",
					"EWH US Equity::HONG KONG",
					"FXI US Equity::CHINA",
					"EIDO US Equity::INDONESIA",
					"EWJ US Equity::JAPAN",
					"EWM US Equity::MALAYSIA",
					"EPHE US Equity::PHILIPPINES",
					"EWS US Equity::SINGAPORE",
					"EWY US Equity::S KOREA",
					"EWT US Equity::TAIWAN",
					"THD US Equity::THAILAND",
					"EWA US Equity::AUSTRALIA",
					"ENZL US Equity::NEW ZEALAND"
			});

				ok = true;
			}

			else if (country == "US ETF >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"SPY US Equity::S&P 500",
					"OEF US Equity::OEX 100",
					"QQQ US Equity::QQQ",
					"IWF US Equity::Russell 1000",
					"IWM US Equity::Russell 2000",
					"MDY US Equity::Mid Cap 400",
					"XLC US Equity::Communication",
					"XLY US Equity::Discretionary",
					"XLP US Equity::Staples",
					"XLE US Equity::Energy",
					"XLF US Equity::Financials",
					"XLV US Equity::Health Care",
					"XLI US Equity::Industrial",
					"XLB US Equity::Materials",
					"XLK US Equity::Technology",
					"XLU US Equity::Utilities",
					"XHB US Equity::Home Builders",
					"ITM US Equity::Home Construction",
					"XRT US Equity::Retail",
					"IYC US Equity::Consumer Services",
					"XBI US Equity::Biotech",
					"IBB US Equity::Biotech",
					"KRE US Equity::Regional Bank",
					"KBE US Equity::Bank",
					"IYR US Equity::Real Estate",
					"VNQ US Equity::REITs",
					"TLT US Equity::20+ Y",
					"IEF US Equity::7-10 Y",
					"SHY US Equity::1-3 Y",
					"XME US Equity::Metal Mining",
					"SMH US Equity::Semi Conductors",
					"GDX US Equity::Gold",
					"DUST US Equity::Gold Miners",
					"OIH US Equity::Oil Services",
					"XOP US Equity::Oil Gas Explore",
					"USO US Equity::US Oil Fund",
					"OILNF US Equity::Goldman Sachs Crude",
			});

				ok = true;
			}

			else if (country == "US SECTOR ETF >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"XLC US Equity:COMMUNICATION",
					"XLY US Equity::DISCRETIONARY",
					"XLP US Equity::STAPLES",
					"XLE US Equity::ENERGY",
					"XLF US Equity::FINANCIALS",
					"XLV US Equity::HEALTH CARE",
					"XLI US Equity::INDUSTRIALS",
					"XLB US Equity::MATERIALS",
					"XLK US Equity::TECHNOLOGY",
					"XLU US Equity::UTILITIES",
					"XHB US Equity::HOME BUILDERS",
					"XRT US Equity::RETAIL",
					"XBI US Equity::BIOTECH",
					"KRE US Equity::REGIONAL BANKS",
					"KBE US Equity::BANKS",
					"XME US Equity::METAL MINING",
			});

				ok = true;
			}

			else if (country == "SPY ETF >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"SPY US Equity::SPY"
				});

				ok = true;
			}
			else if (country == "SPECIALTY IND ETF >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"XHB US Equity::Homebuilders",
					"XBI US Equity::Biotech",
					"BLOK US Equity::Transformational Data ",
					"DSI US Equity::MSCI KLD 400 Social ",
					"HACK US Equity::Cybersecurity ",
					"IGE US Equity::NA Natural Resources",
					"IGF US Equity::Global Infrastructure",
					"IWN US Equity::Russell 2000 Value",
					"MJ US Equity::Alternative Harvest",
					"ROBO US Equity::Robotics and Automation ",
					"SOCL US Equity::Social Media ",
					"VNQ US Equity::Real Estate ",
					"VYM US Equity::High Dividend Yield  ",
					"ERTH US Equity::Sustainable Future ",
					"HTEC US Equity::H/C Tech and Innovation"
				});

				ok = true;
			}
			else if (country == "FIXED INCOME ETF >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"AGG US Equity::Core U.S.Aggregate",
					"LQD US Equity::Invest Grade Corp",
					"HYG US Equity::High Yld Corp",
					"SHY US Equity::1-3 Year Treas",
					"IEF US Equity::7-10 Year Treas",
					"TLT US Equity::20+ Year Treas"
				});

				ok = true;
			}
			else if (country == "COMDTY PROD ETF >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"XME US Equity::Metals & Mining ",
					"GDX US Equity::Gold Miners ",
					"SIL US Equity::Silver Miners ",
					"SLX US Equity::Steel ",
					"URA US Equity::Uranium ",
					"PIO US Equity::Water ",
					"PBD US Equity::Clean Energy ",
					"TAN US Equity::Solar",
					"FAN US Equity::Wind Energy ",
					"LIT US Equity::Lithium | Battery",
					"REMX US Equity::Rare Earth"
				});

				ok = true;
			}
			else if (country == "COMDTY ETF >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"USCI US Equity::US Comdty Index",
					"DBC US Equity::Comdty Index",
					"GLD US Equity::SPDR Gold",
					"SLV US Equity::iShares Silver",
					"USD US Equity::Semiconductors",
					"UNG US Equity::US Natural Gas",
					"DBA US Equity::Agriculture"
				});

				ok = true;
			}

			else if (country == "FX ETF >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"UUP US Equity::US Dollar Bullish ",
					"UDN US Equity::US Dollar Bearish ",
					"FXY US Equity::Japanese Yen",
					"FXE US Equity::Euro Currency",
					"FXB US Equity::British Pound ",
					"FXF US Equity::Swiss Franc ",
					"FXC US Equity::Canadian Dollar",
					"FXA US Equity::Australian Dollar"
				});

				ok = true;
			}

			else if (country == "CQG SINGLE CTY ETF >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"NYSE AMERICAN SINGLE CTY ETF >"
				});

				ok = true;
			}

			else if (country == "CQG SECTOR ETF >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"NYSE AMERICAN SECTOR ETF >"
				});

				ok = true;
			}

			else if (country == "CQG SPY ETF >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"NYSE AMERICAN SPY >"
				});

				ok = true;
			}


			else if (country == "CRYPTO INDICES >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"BGCI Index::BBG Galaxy Crypto",
					"BTC Index::BBG Bitcoin",
					"ETH Index::BBG Ethereum",
				});

				ok = true;
			}

			else if (country == "CRYPTO CURRENCY >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"BITCOIN >",
					"BITCOIN CASH >",
					"DASH >",
					"EOS TOKENS >",
					"ETHEREUM >",
					"ETHEREUM CLASSIC >",
					"LITECOIN >",
					"RIPPLE >",
					"ZCASH >",
					"XSOUSD CEX Curncy::AAVE SOLANA",
					"XAVUSD CEX Curncy::AVALANCHE",
					"XADUSD CEX Curncy::CARDANO",
					"XLIUSD CEX Curncy::CHAINLINK",
					"XDGUSD CEX Curncy::DOGECOIN",
					"XMRUSD BGN Curncy::MONERO",
					"XDOUSD CEX Curncy::POLKADOT",
					"XDTUSD CEX Curncy::TETHER",
					"XDTUSD DAR Curncy::TERRA",
					"XUNUSD CEX Curncy::UNISWAP"

				});

				ok = true;
			}

			else if (country == "CRYPTO FUTURES >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"BTC1 Curncy::Bitcoin Futures",
					"DCR1 Curncy::Ether Futures"

				});

				ok = true;
			}

			else if (country == "WORLD CURRENCIES >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"XBTUSD Curncy::BITCOIN",
					" ",
					"CAD Curncy::CANADA",
					"DXY Curncy::USA",
					"MXN Curncy::MEXICO",
					"BRL Curncy::BRAZIL",
					"CLP Curncy::CHILE",
					"COP Curncy::COLUMBIA",
					"PEN Curncy::PERU",
					"GBP Curncy::UK",
					"EUR Curncy::EURO",
					"NOK Curncy::NORWAY",
					"SEK Curncy::SWEDEN",
					"CHF Curncy::SWITZERLAND",
					"DKK Curncy::DENMARK",
					"PLN Curncy::POLAND",
					"RON Curncy::ROMANIA",
					"TRY Curncy::TURKEY",
					"RUB Curncy::RUSSIA",
					"ILS Curncy::ISRAEL",
					"KED Curncy::KUWAIT",
					"ZAR Curncy::SOUTHAFRICA",
					"KRW Curncy::KOREA",
					"JPY Curncy::JAPAN",
					"CNY Curncy::CHINA",
					"HKD Curncy::HONGKONG",
					"IDR Curncy::INDONESIA",
					"MYR Curncy::MALAYSIA",
					"PHP Curncy::PHILIPPINES",
					"SGD Curncy::SINGAPORE",
					"TWD Curncy::TAIWAN",
					"THB Curncy::THAILAND",
					"VND Curncy::VIETNAM",
					"INR Curncy::INDIA",
					"PKR Curncy::PAKISTAN",
					"AUD Curncy::AUSTRALIA",
					"NZD Curncy::NEWZEALAND",
				});

				ok = true;
			}

			else if (country == "CRYPTO >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] { "CRYPTO INDICES >", "CRYPTO CURRENCY", "CRYPTO FUTURES >" });
				else setNavigation(panel, mouseDownEvent, new string[] { "CRYPTO INDICES", "CRYPTO CURRENCY", "CRYPTO FUTURES" });
				ok = true;
			}

			else if (country == "ETF >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] { "US ETF >", "US SECTOR ETF >", "SINGLE CTY ETF >", "SPY >" });
				else setNavigation(panel, mouseDownEvent, new string[] { "US ETF", "US SECTOR ETF", "SINGLE CTY ETF", "SPY" });
				ok = true;
			}

			else if (country == "STATE INDEX >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"STATE INDICES"
				});

				ok = true;
			}
			else if (country == "BLOOMBERG BARCLAYS >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"MUNI BOND INDEX >",
					"MUNI HIGH YLD >",
					"TAXABLE MUNI BOND INDEX >",
				});

				ok = true;
			}

			else if (country == "SPREADS >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"US 100 SPREADS",
					" ",
					"OEXNDX SPREADS",
					" ",
					"SP 500 SPREADS"
				});

				ok = true;
			}

			else if (country == "ML PORTFOLIOS >")
			{
				var names = getFactorModelNames();
				setNavigation(panel, mouseDownEvent, names.ToArray());
				//for (int ii = 0; ii < names.Count; ii++)
				//{
				//    addPortfolioMenu(Portfolio.PortfolioType.Model, panel, go_Click, names[ii], ii == 0, false);
				//}
			}

			else if (country == "ATM ALERTS >")
			{
				var names = getAlertNames();
				for (int ii = 0; ii < names.Count; ii++)
				{
					addPortfolioMenu(Portfolio.PortfolioType.Alert, panel, go_Click, names[ii], ii == 0, false);
				}
			}

			else if (country == "BLOOMBERG EQS >")
			{
				for (int ii = 0; ii < 18; ii++)
				{
					addPortfolioMenu(Portfolio.PortfolioType.EQS, panel, go_Click, "EQS" + (ii + 1).ToString(), ii == 0);
				}

				ok = true;
			}
			else if (country == "BLOOMBERG PRTU >")
			{
				for (int ii = 0; ii < 18; ii++)
				{
					addPortfolioMenu(Portfolio.PortfolioType.PRTU, panel, go_Click, "PRTU" + (ii + 1).ToString(), ii == 0);
				}

				ok = true;
			}

			else if (country == "BLOOMBERG WORKSHEETS >")
			{
				for (int ii = 0; ii < 18; ii++)
				{
					setWorksheetNavigation(panel, mouseDownEvent);
				}

				ok = true;
			}
			else if (country == "CDS by COUNTRY")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"SOVR CDS"
				});

				ok = true;
			}
			else if (country == "CDS by STATE")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"STATE CDS"
				});

				ok = true;
			}

			else if (country == "WEI >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"GLOBAL INDICES",
				});

				ok = true;
			}
			else if (country == "GLOBAL GENERIC RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"GLOBAL 30YR >",
					"GLOBAL 10YR >",
					"GLOBAL 7YR >",
					"GLOBAL 5YR >",
					"GLOBAL 2YR >",
					"GLOBAL 1YR >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"GLOBAL 30YR",
					"GLOBAL 10YR",
					"GLOBAL 7YR",
					"GLOBAL 5YR",
					"GLOBAL 2YR",
					"GLOBAL 1YR"
				});
				ok = true;
			}

			else if (country == "GLOBAL 30YR >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"GCAN30YR Index::CANADA",
					"USGG30YR Index::USA",
					"GMXN30YR Index::MEXICO",
					"GRPE30Y Index::PERU",
					"GUKG30 Index::UK",
					"GIGB30YR Index::IRELAND",
					"GFIN30YR Index::FINLAND",
					"GNOR30YR Index::NORWAY",
					"GSGB30YR Index::SWEDEN",
					"GAGB30YR Index::AUSTRIA",
					"GBGB30YR Index::BELIGUM",
					"GDGB30YR Index::DENMARK",
					"GFRN30 Index::FRANCE",
					"GDBR30 Index::GERMANY",
					"GGGB30YR Index::GREECE",
					"GBTPGR30 Index::ITALY",
					"GNTH30YR Index::NETHERLANDS",
					"GSPT30YR Index::PORTUGAL",
					"GSPG30YR Index::SPAIN",
					"GSWISS30 Index::SWITZERLAND",
					"CZGB30YR Index::CzechRepublic",
					"GHGB30YR Index::HUNGARY",
					"POGB30YR Index::POLAND",
					"GRSK30Y Index::SLOVAKIA",
					"IESM30Y Index::TURKEY",
					"RUGE30Y Index::RUSSIA",
					"GISR30YR Index::ISRAEL",
					"GSAB30YR Index::SOUTHAFRICA",
					"GVSK30YR Index::KOREA",
					"GJGB30 Index::JAPAN",
					"GCNY30YR Index::CHINA",
					"GIDN30YR Index::INDONESIA",
					"MGIY30Y Index::MALAYSIA",
					"GVTW30YR Index::TAIWAN",
					"GVTL30YR Index::THAILAND",
					"GIND30YR Index::INDIA",
					"GACGB30 Index::AUSTRALIA",
					"GNZGB30 Index::NEWZEALAND"
				});

				ok = true;
			}


			else if (country == "GLOBAL 10YR >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"GCAN10YR Index::CANADA",
					"USGG10YR Index::USA",
					"GMXN10YR Index::MEXICO",
					"GRPE10Y Index::PERU",
					"GUKG10 Index::UK",
					"GIGB10YR Index::IRELAND",
					"GFIN10YR Index::FINLAND",
					"GNOR10YR Index::NORWAY",
					"GSGB10YR Index::SWEDEN",
					"GAGB10YR Index::AUSTRIA",
					"GBGB10YR Index::BELIGUM",
					"GDGB10YR Index::DENMARK",
					"GFRN10 Index::FRANCE",
					"GDBR10 Index::GERMANY",
					"GGGB10YR Index::GREECE",
					"GBTPGR10 Index::ITALY",
					"GNTH10YR Index::NETHERLANDS",
					"GSPT10YR Index::PORTUGAL",
					"GSPG10YR Index::SPAIN",
					"GSWISS10 Index::SWITZERLAND",
					"CZGB10YR Index::CzechRepublic",
					"GHGB10YR Index::HUNGARY",
					"POGB10YR Index::POLAND",
					"GRSK10Y Index::SLOVAKIA",
					"IESM10Y Index::TURKEY",
					"RUGE10Y Index::RUSSIA",
					"GISR10YR Index::ISRAEL",
					"GSAB10YR Index::SOUTHAFRICA",
					"GVSK10YR Index::KOREA",
					"GJGB10 Index::JAPAN",
					"GCNY10YR Index::CHINA",
					"GHKGB10Y Index::HONGKONG",
					"GIDN10YR Index::INDONESIA",
					"MGIY10Y Index::MALAYSIA",
					"GVTW10YR Index::TAIWAN",
					"GVTL10YR Index::THAILAND",
					"GIND10YR Index::INDIA",
					"GACGB10 Index::AUSTRALIA",
					"GNZGB10 Index::NEWZEALAND"
				});

				ok = true;
			}

			else if (country == "GO >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"GO AAA >",
					"GO AA >",
					"GO A >",
					"GO BBB >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"GO AAA",
					"GO AA",
					"GO A",
					"GO BBB"
				});

				ok = true;
			}
			else if (country == "GO TAXABLE >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"TAXABLE GO AAA >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"TAXABLE GO AAA"
				});

				ok = true;
			}
			else if (country == "REVENUE >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"REV AAA >",
					"REV AA >",
					"REV A >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"REV AAA",
					"REV AA",
					"REV A"
				});

				ok = true;
			}

			else if (country == "REVENUE TAXABLE >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"TAXABLE REVENUE AAA >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"TAXABLE REVENUE AAA"
				});

				ok = true;
			}

			else if (country == "MUNI SPREADS >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"MUNI TREAS SPREADS >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"MUNI TREAS SPREADS"
				});

				ok = true;
			}

			else if (country == "COMMUNICATION CREDIT >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"COMM A >",
					"COMM BBB >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"COMM A",
					"COMM BBB"
				});

				ok = true;
			}
			else if (country == "CONS DISC CREDIT >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"DISC AA >",
					"DISC A >",
					"DISC BBB >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"DISC AA",
					"DISC A",
					"DISC BBB"
				});

				ok = true;
			}
			else if (country == "CONS STAPLES CREDIT >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"STAPLES AAA >",
					"STAPLES AA >",
					"STAPLES A >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"STAPLES AAA",
					"STAPLES AA",
					"STAPLES A"
				});

				ok = true;
			}
			else if (country == "ENERGY CREDIT >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"ENERGY AAA >",
					"ENERGY AA >",
					"ENERGY A >",
					"ENERGY BBB"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"ENERGY AAA",
					"ENERGY AA",
					"ENERGY A",
					"ENERGY BBB"
				});

				ok = true;
			}
			else if (country == "FINANCIALS CREDIT >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"FINANCIALS AAA >",
					"FINANCIALS AA >",
					"FINANCIALS A >",
					"FINANCIALS BBB >",
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"FINANCIALS AAA",
					"FINANCIALS AA",
					"FINANCIALS A",
					"FINANCIALS BBB",
				});

				ok = true;
			}
			else if (country == "HEALTH CARE CREDIT >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"HEALTH CARE AAA >",
					"HEALTH CARE AA >",
					"HEALTH CARE A >",
					"HEALTH CARE BBB >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"HEALTH CARE AAA",
					"HEALTH CARE AA",
					"HEALTH CARE A",
					"HEALTH CARE BBB"
				});

				ok = true;
			}
			else if (country == "INDUSTRIAL CREDIT >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"INDUSTRIAL AAA >",
					"INDUSTRIAL AA >",
					"INDUSTRIAL A >",
					"INDUSTRIAL BBB >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"INDUSTRIAL AAA",
					"INDUSTRIAL AA",
					"INDUSTRIAL A",
					"INDUSTRIAL BBB"
				});

				ok = true;
			}
			else if (country == "MATERIALS CREDIT >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"MATERIALS A >",
					"MATERIALS BBB >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"MATERIALS A",
					"MATERIALS BBB"
				});

				ok = true;
			}
			else if (country == "TECHNOLOGY CREDIT >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"TECHNOLOGY AAA >",
					"TECHNOLOGY AA >",
					"TECHNOLOGY A >",
					"TECHNOLOGY BBB >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"TECHNOLOGY AAA",
					"TECHNOLOGY AA",
					"TECHNOLOGY A",
					"TECHNOLOGY BBB"
				});

				ok = true;
			}
			else if (country == "UTIL CREDIT >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"UTIL A >",
					"UTIL BBB >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"UTIL A",
					"UTIL BBB"
				});

				ok = true;
			}

			else if (country == "AAA MUNI RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"AAA MUNI RATES >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"AAA MUNI RATES"
				});

				ok = true;
			}

			else if (country == "US MORTGAGE RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"MORTGAGE RATES >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"MORTGAGE RATES"
				});

				ok = true;
			}


			else if (country == "FOREST >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"GENERIC FOREST >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"GENERIC FOREST"
				});

				ok = true;
			}

			else if (country == "US HOME PRICES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"HOME PRICES >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"HOME PRICES"
				});

				ok = true;
			}

			else if (country == "BUILDING COST ITEMS >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"COST ITEMS >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"COST ITEMS"
				});

				ok = true;
			}

			else if (country == "GENERIC FOREST >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"FOREST >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"FOREST"
				});

				ok = true;
			}

			else if (country == "STATE CURVE >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"ARKANSAS",
					"CALIFORNIA",
					"CONNECTICUT",
					"FLORIDA",
					"GEORGIA",
					"HAWAII",
					"ILLINOIS",
					"LOUISIANA",
					"MARYLAND",
					"MASSACHUSETTS",
					"MICHIGAN",
					"MINNESOTA",
					"NEVADA",
					"NEW HAMPSHIRE",
					"NEW JERSEY",
					"NEW YORK",
					"NORTH CAROLINA",
					"OHIO",
					"OREGON",
					"PENNSYLVANIA",
					"RHODE ISLAND",
					"SOUTH CAROLINA",
					"TENNESSEE",
					"TEXAS",
					"UTAH",
					"VIRGINIA",
					"WASHINGTON"
				});

				ok = true;
			}

			else if (country == "FORWARD CROSS >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"USDCAD Forward",
					"USDMXN Forward",
					"",
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
					"",
					"USDJPY Forward",
					"USDCNY Forward",
					"USDHKD Forward",
					"USDMYR Forward",
					"USDSGD Forward",
					"USDTHB Forward",
					"USDVND Forward",
					"",
					"USDAUD Forward",
					"USDNZD Forward",

				});

				ok = true;
			}

			else if (country == "SPOT FX >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"A11 Curncy::A1 ",
					"AD1 Curncy::AD ",
					"AUA1 Curncy::AUA ",
					"AUW1 Curncy::AUW ",
					"B11 Curncy::B1 ",
					"BGA1 Curncy::BGA ",
					"BGW1 Curncy::BGW ",
					"BP1 Curncy::BP ",
					"BY1 Curncy::BY ",
					"BYW1 Curncy::BYW ",
					"CD1 Curncy::CD ",
					"DRA1 Curncy::DRA ",
					"DRR1 Curncy::DRR ",
					"DS1 Curncy::DS ",
					"DUS1 Curncy::DUS ",
					"EC1 Curncy::EC ",
					"INB1 Curncy::EUR INR",
					"INT1 Curncy::USD INR",
					"JAD1 Curncy::JAD ",
					"JAW1 Curncy::JAW ",
					"JY1 Curncy::JY ",
					"KU1 Curncy::KU ",
					"OCT1 Curncy::OCT ",
					"PE1 Curncy::PE ",
					"RED1 Curncy::RED ",
					"RER1 Curncy::RER ",
					"UAW1 Curncy::EC ",
					"UEA1 Curncy::UEA ",
					"UG1 Curncy::UG ",
					"UR1 Curncy::UR ",
					"URA1 Curncy::URA ",
					"VAW1 Curncy::VAW ",
					"VDD1 Curncy::VDD ",
					"VJW1 Curncy::VJW ",
					"VTE1 Curncy::VTE ",
					"VTW1 Curncy::VTW ",
					"VXA1 Curncy::VXA ",
					"VXC1 Curncy::VXC ",
					"WAB1 Curncy::WAB ",
					"WAY1 Curncy::WAY ",
					"WEY1 Curncy::WEY ",
					"WJ1 Curncy::WJ",
					"WJB1 Curncy::WJB ",
					"WJY1 Curncy::WJY ",
					"WLB1 Curncy::WLB ",
					"WLY1 Curncy::WLY ",
					"WT1 Curncy::WT",
					"XID1 Curncy::XID ",
					"XUC1 Curncy::XUC ",
					"YJ1 Curncy::JY ",
					"YT1 Curncy::EC ",
				});

				ok = true;
			}

			//else if (country == "GENERIC >")
			//{
			//    if (UseCheckBoxes) setNavigationLevel1(panel, mouseDownEvent, new string[] {
			//        "GENERIC WORLD EQ FUTURES >", "GENERIC WORLD BOND FUTURES >","GENERIC ENERGY OIL | GAS >", "GENERIC ENERGY REFINED >", "GENERIC ENERGY LPG >", "GENERIC ENERGY COAL >", "GENERIC ENERGY PETRO CHEM >", "GENERIC FX >", "GENERIC METALS >", "GENERIC ALLOYS >", "GENERIC FOREST >", "GENERIC GRAIN >",  "GENERIC LIVESTOCK >", "GENERIC OILSEED >", "GENERIC DAIRY >", "GENERIC SOFTS >"
			//    });
			//    else setNavigationLevel1(panel, mouseDownEvent, new string[] {
			//        "GENERIC WORLD EQ FUTURES", "GENERIC WORLD BOND FUTURES", "GENERIC ENERGY OIL | GAS", "GENERIC ENERGY REFINED", "GENERIC ENERGY LPG", "GENERIC ENERGY COAL", "GENERIC ENERGY PETRO CHEM", "GENERIC FX", "GENERIC METALS", "GENERIC ALLOYS", "GENERIC FOREST", "GENERIC GRAIN", "GENERIC LIVESTOCK", "GENERIC OILSEED", "GENERIC DAIRY", "GENERIC SOFTS"
			//    });

			//    ok = true;
			//}

			else if (country == "COUNTRY EQ INDICES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"WORLD EQ FUTURES >"
				});
				else setNavigation(panel, mouseDownEvent, new string[] {
					"WORLD EQ FUTURES"
				});

				ok = true;
			}


			else if (country == "FX & CRYPTO >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"WORLD CURRENCY >",
					"",
					"CRYPTO CURRENCY >",
					"",
					"CRYPTO FUTURES >",
					"",
					"CRYPTO INDICES >"
				});
				else setNavigation(panel, mouseDownEvent, new string[] {
					"WORLD CURRENCY",
					 "",
					"CRYPTO CURRENCY",
					"",
					"CRYPTO FUTURES",
					"",
					"CRYPTO INDICES"
				});

				ok = true;
			}

			else if (country == "GLOBAL FUTURES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"WORLD BOND FUTURES >",
                    //" ",
                    //"WORLD CURRENCY >",
                    " ",
					"WORLD EQUITY FUTURES >"
				});
				else setNavigation(panel, mouseDownEvent, new string[] {
					"WORLD BOND FUTURES >",
                    //" ",
                    //"WORLD CURRENCY >",
                    " ",
					"WORLD EQUITY FUTURES >"
				});

				ok = true;
			}

			else if (country == "INTEREST RATE FUTURES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"WORLD BOND FUTURES >"
				});
				else setNavigation(panel, mouseDownEvent, new string[] {
					"WORLD BOND FUTURES"
				});

				ok = true;
			}


			else if (country == "COMMODITY GROUPS >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"WORLD BOND FUTURES >",
					" ",
					"WORLD CURRENCIES >",
					" ",
					"WORLD EQ FUTURES >",
					" ",
					"ELECTRICITY >",
					" ",
					"EMISSIONS >",
					" ",
					"ENERGY BIOFUEL >",
					"ENERGY COAL >",
					"ENERGY LPG >",
					"ENERGY OIL | GAS >",
					"ENERGY PETRO CHEM >",
					"ENERGY REFINED >",
					"ENERGY RENEWABLE >",
					" ",
					"METALS | BASE >",
					"METALS | INDUSTRIAL >",
					"METALS | PRECIOUS >",
					" ",
					"FOREST >",
					" ",
					"GRAIN >",
					" ",
					"MEAT | FISH >", 
                    //" ",
                    //"OILSEEDS >", 
                    " ",
					"DAIRY >",
					" ",
					"SOFTS >"
				});
				else setNavigation(panel, mouseDownEvent, new string[] {
					"WORLD BOND FUTURES",
					" ",
					"WORLD CURRENCIES",
					" ",
					"WORLD EQ FUTURES",
					" ",
					"ELECTRICITY",
					" ",
					"EMISSIONS",
					" ",
					"ENERGY BIOFUEL",
					"ENERGY COAL",
					"ENERGY LPG",
					"ENERGY OIL | GAS",
					"ENERGY PETRO CHEM",
					"ENERGY REFINED",
					"ENERGY RENEWABLE",
					" ",
					"METALS | BASE",
					"METALS | INDUSTRIAL",
					"METALS | PRECIOUS",
					" ",
					"FOREST",
					" ",
					"GRAIN",                    
                    //"OILSEEDS",
                    " ",
					"DAIRY",
					"MEAT | FISH",
					" ",
					"SOFTS"
				});

				ok = true;
			}

			else if (country == "COMMODITY CURVES >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"BTCA Curncy::Bitcoin",
					"CLA Comdty::Crude",
					"XBA Comdty::Gasoline",
					"HOA Comdty::Heat Oil",
					"QSA Comdty::Gas Oil",
					"NGA Comdty::Natural Gas",
					"GCA Comdty::Gold",
					"SIA Comdty::Silver",
					"PLA Comdty::Platinum",
					"PAA Comdty::Palladuim",
					"HGA Comdty::Copper",
					"C A Comdty::Corn",
					"W A Comdty::Wheat",
					"S A Comdty::Soybeans",
					"BOA Comdty::Bean Oil",
					"SMA Comdty::Meal",
					"RRA Comdty::Rice",
					"LCA Comdty::Cattle",
					"FCA Comdty::Feeders",
					"LHA Comdty::Hogs",
					"KCA Comdty::Coffee",
					"SBA Comdty::Sugar",
					"CTA Comdty::Cotton",
					"CCA Comdty::Cocoa",
					"JOA Comdty::OJ",
				});

				ok = true;
			}

			//else if (country == "ACTIVE >")
			//{
			//    if (UseCheckBoxes) setNavigationLevel1(panel, mouseDownEvent, new string[] {
			//        "ACTIVE WORLD EQ FUTURES >", "ACTIVE WORLD BOND FUTURES >", "ACTIVE ENERGY OIL | GAS >", "ACTIVE ENERGY REFINED >", "ACTIVE ENERGY LPG >", "ACTIVE ENERGY COAL >", "ACTIVE ENERGY PETRO CHEM >", "ACTIVE FX >", "ACTIVE METALS >", "ACTIVE ALLOYS >", "ACTIVE FOREST >", "ACTIVE GRAIN >", "ACTIVE LIVESTOCK >", "ACTIVE OILSEED >", "ACTIVE DAIRY >", "ACTIVE SOFTS >"
			//     });

			//    else setNavigationLevel1(panel, mouseDownEvent, new string[] {
			//        "ACTIVE WORLD EQ FUTURES", "ACTIVE WORLD BOND FUTURES", "ACTIVE ENERGY OIL | GAS", "ACTIVE ENERGY REFINED", "ACTIVE ENERGY LPG", "ACTIVE ENERGY COAL", "ACTIVE ENERGY PETRO CHEM", "ACTIVE FX", "ACTIVE METALS", "ACTIVE ALLOYS", "ACTIVE FOREST", "ACTIVE GRAIN", "ACTIVE LIVESTOCK", "ACTIVE OILSEED", "ACTIVE DAIRY", "ACTIVE SOFTS"
			//    });

			//    ok = true;
			//}

			else if (country == "BCOM >" || country == "BCOM")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					 "BCOM:BCOM >",
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					 "BCOM:BCOM"
				});
				ok = true;
			}

			else if (country == "AGRICULTURE >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"FERTILIZER >",
					" ",
					"FIBERS >",
					" ",
					"FOODSTUFF >",
					" ",
					"FOREST PRODUCTS >",
					" ",
					"GRAINS >",
					" ",
					"MEATS >",
					" ",
					"OLECHEMICALS >",
					" ",
					"SOFTS >",
				});

				ok = true;
			}

			else if (country == "CQG AGRICULTURE >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"CQG FERTILIZER >",
					" ",
					"CQG FIBERS >",
					" ",
					"CQG FOODSTUFF >",
					" ",
					"CQG FOREST PRODUCTS >",
					" ",
					"CQG GRAINS >",
					" ",
					"CQG MEATS >",
					" ",
					"CQG SOFTS >",
				});

				ok = true;
			}

			else if (country == "ENERGY >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"CRUDE >",
					" ",
					"NATURAL GAS >",
					" ",
					"PETROCHEMICAL >",
					" ",
					"REFINED | HEAVY >",
					" ",
					"REFINED | LIGHT >",
					" ",
					"REFINED | MIDDLE >"
				});

				ok = true;
			}

			else if (country == "CQG ENERGY >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"CQG CRUDE >",
					" ",
					"CQG NATURAL GAS >",
                    //" ",
                    //"CQG PETROCHEMICAL >",
                    " ",
					"CQG FUEL >",
				});

				ok = true;
			}

			else if (country == "ENVIRONMENT >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"EMISSIONS >",
				});

				ok = true;
			}

			else if (country == "CQG ENVIRONMENT >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"CQG EMISSIONS >",
				});

				ok = true;
			}

			else if (country == "CQG HOUSING >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"CME HOUSING >",
				});

				ok = true;
			}

			else if (country == "METALS >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"BASE >",
					" ",
					"BRASS >",
					" ",
					"COKING COAL >",
					" ",
					"FERRO ALLOYS >",
					" ",
					"MINERALS >",
					" ",
					"MINOR >",
					" ",
					"PRECIOUS >",
					" ",
					"RARE EARTHS >",
					" ",
					"STEEL >"
				});

				ok = true;
			}

			else if (country == "CQG METALS >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"CQG BASE >",
                    //" ",
                    //"CQG FERRO ALLOYS >",
                    " ",
					"CQG PRECIOUS >",
					" ",
					"CQG STEEL >"
				});

				ok = true;
			}

			else if (country == "CQG SHIPPING >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"CQG FREIGHT >"
				});

				ok = true;
			}

			else if (country == "CQG WATER >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"CME WATER >",
				});

				ok = true;
			}

			else if (country == "CQG HOUSING >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"CME HOUSING >",
				});

				ok = true;
			}
			//else if (country == "CQG WEATHER >")
			//{
			//    setNavigation(panel, mouseDownEvent, new string[]
			//    {
			//        "CME WEATHER >",
			//    });

			//    ok = true;
			//}

			else if (country == "CQG NYSE >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"CQG NYSE 75 >"
				});

				ok = true;
			}
			else if (country == "CQG NASDAQ >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"CQG NDX 100 >"
				});

				ok = true;
			}

			else if (country == "IND | COMMUNICATIONS >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"ADVERTISING | MARKETING >",
					" ",
					"CABLE | SATELLITE >",
					" ",
					"ENTERTAINMENT CONTENT >",
					" ",
					"INTERNET MEDIA >",
					" ",
					"PUBLISHING | BROADCASTING >",
					" ",
					"TELECOM CARRIERS >",
				});

				ok = true;
			}

			else if (country == "EXECUTIVE OFFICE OF THE PRESIDENT >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"NATIONAL SECURITY COUNCIL >",
					" ",
					"OFFICE OF MANAGEMENT AND BUDGET >",
					" ",
					"OFFICE OF THE US TRADE REPRESENTATIVE >",
					" ",
					"OFFICE OF SCIENCE AND TECHNOLOGY >",
					" ",
					"PRESIDENT'S INTELLIGENCE ADVISORY BOARD >"
				});

				ok = true;
			}
			else if (country == "DEPT OF AGRICULTURE >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"AGRICULTURAL MARKETING SERVICE >",
					" ",
					"ECONOMIC RESEARCH SERVICE >",
					" ",
					"FARM SERVICE AGENCY >",
					" ",
					"FOREIGN ARICULTURAL SERVICE >",
					" ",
					"GRAIN, INSPECTION, PACKERS AND STOCKYARDS >",
					"",
					"NATIONAL AGRICULTURAL STATISTICS SERVICE >",
					" ",
					"NATIONAL INSTITUTE OF FOOD AND AGRICULTURE >",
					" ",
					"RURAL UTILITIES SERVICE >",
					" ",
					"US FOREST SERVICE >",
				});

				ok = true;
			}

			else if (country == "DEPT OF COMMERCE >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"BUREAU OF ECONOMIC ANALYSIS >",
					" ",
					"BUREAU OF INDUSTRY >",
					" ",
					"ECONOMIC DEVELOPPMENT ADIM >",
					" ",
					"INTERNATIONAL TRADE ASSOCIATION >"
				});

				ok = true;
			}

			else if (country == "DEPT OF DEFENSE >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"DEFENSE CONTRACT MANAGEMENT >",
					" ",
					"DEFENSE FINANCE AND ACCOUNTING >",
					" ",
					"DEFENSE INTELLIGENCE AGENCY >",
					" ",
					"DEPARTMENT OF THE AIR FORCE >",
					" ",
					"DEPARTMENT OF THE ARMY >",
					" ",
					"DEPARTMENT OF THE NAVY >",
					" ",
					"MISSILE DEFENSE AGENCY >",
					" ",
					"NATIONAL RECONSSSIANCE OFFICE >",
					" ",
					"OFFICE OF ECONOMIC ADJUSTMENT >",
					" ",
					"US ARMY CORP OF ENGINEERS >",
					" ",
					"US MARINE CORP >",
					" ",
					"US SPECIAL OPERATIONS >",
				});

				ok = true;
			}

			else if (country == "DEPT OF ENERGY >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"FEDERAL ENERGY REGULATORY COMMISSION >",
					" ",
					"NATIONAL RENEWABLE ENERGY LABS >",
					" ",
					"OFFICE OF ELECTRICITY DELIVERY >",
					" ",
					"OFFICE OF FOSSIL ENERGY >",
					" ",
					"OFFICE OF NUCLEAR ENERGY >",
					" ",
					"OFFICE OF SCIENCE >",
				});

				ok = true;
			}

			else if (country == "DEPT OF HOMELAND SECURITY >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"FEDERAL EMERGENCY MANAGEMENT >",
					" ",
					"OFFICE OF INFRASTRUCTURE PROTECTION >",
					" ",
					"OFFICE OF INTELLIGENCE AND ANALYSIS >",
					" ",
					"SCIENCE AND TECHNOLOGY >",
				});

				ok = true;
			}
			else if (country == "DEPT OF HOUSING AND URBAN DEVELOPMENT >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"FANNIE MAE >",
					" ",
					"FEDERAL HOUSING ADIM >",
					" ",
					"FREDDIE MAC >",
					" ",
					"GINNIE MAE >",
					" ",
					"SALLIE MAE >",
				});

				ok = true;
			}
			else if (country == "DEPT OF TRANSPORATION >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"FEDERAL AVIATION ADIM >",
					" ",
					"FEDERAL HIGHWAY ADIM >",
					" ",
					"FEDERAL RAILROAD ADIM >",
					" ",
					"FEDERAL TRANSIT ADIM >",
				});

				ok = true;
			}
			else if (country == "DEPT OF TREASURY >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"BUREAU OF THE FISCAL SERVICE >",
					" ",
					"BUREAU OF PUBLIC DEBT >",
					" ",
					"FEDERAL FINANCING BANK >",
					" ",
					"OFFICE OF FOREIGN ASSETS >",
					" ",
					"OFFICE OF THE COMPTROLLER >",
				});

				ok = true;
			}
			else if (country == "LEGISLATIVE BRANCH >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"CONGRESSIONAL BUDGET OFFICE >",
					" ",
					"GOVERNMENT ACCOUNTABILITY OFFICE >",
					" ",
					"GOVERNMENT PUBLISHING OFFICE >",
					" ",
					"US-CHINA ECONOMIC & SECURITY >",
				});

				ok = true;
			}

			else if (country == "INDEPENDENT AGENCIES >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"AMTRAK >",
					" ",
					"ARMED FORCES RETIREMENT >",
					" ",
					"CFTC >",
					" ",
					"CIA >",
					" ",
					"EXPORT-IMPORT BANK >",
					" ",
					"FDIC >",
					" ",
					"FEDERAL HOUSING FINANCE >",
					" ",
					"FEDERAL RESERVE >",
					" ",
					"FEDERAL TRADE COMMISSION >",
					" ",
					"NASA >",
					" ",
					"NATIONAL CAPITAL PLANNING COMMISSION >",
					" ",
					"NATIONAL SECURITY AGENCY >",
					" ",
					"OFFICE OF NATIONAL INTELLIGENCE >",
					" ",
					"RAILROAD RETIREMENT >",
					" ",
					"SBA >",
					" ",
					"SEC >",
					" ",
					"US AGENCY FOR INTERNATIONAL DEVELOPMENT >"
				});

				ok = true;
			}


			else if (country == "IND | CONSUMER DISC >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"AUTO PARTS >",
					" ",
                    //"AUTOMOBILES >",                   
                    //" ",
                    "CASINOS | GAMING >",
					" ",
					"HOMEBUILDERS >",                                      
                    //" ",
                    //"LODGING >",
                    " ",
					"LUXURY GOODS >",
					" ",
					"RESTAURANTS >",
					" ",
					"SPECIALTY APPAREL >",
				});

				ok = true;
			}

			else if (country == "IND | CONSUMER STAPLES >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"AGRICULTURE >",
					" ",
					"BEVERAGES >",
					" ",
					"HOUSEHOLD PRODUCTS >",
					" ",
					"PACKAGED FOOD >",                                      
                    //" ",
                    //"TOBACCO | CANNABIS >",
                });

				ok = true;
			}


			else if (country == "IND | ENERGY >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"CRUDE OIL | NATURAL GAS >",
					" ",
					"CRUDE OIL PRODUCTION >",                   
                    //" ",
                    //"INTEGRATED OILS >",
                    //" ",
                    //"LIQUEFIED NATURAL GAS >",                                      
                    " ",
					"MIDSTREAM OIL | GAS >",
                    //" ",
                    //"OIL | GAS SERVICES >",
                    " ",
					"REFINING | MARKETING >",
					" ",
					"SOLAR ENERGY EQUIPMENT >",
					" ",
					"WIND ENERGY EQUIPMENT >",
				});

				ok = true;
			}


			else if (country == "IND | FINANCIALS >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"BANKING >",
					" ",
					"CONSUMER FINANCE >",
					" ",
					"FINTECH >",
					" ",
					"INSURANCE >",
					" ",
					"INVESTMENT BANKING >",
					" ",
					"INVESTMENT MGMT >",
					" ",
					"LIFE INSURANCE >",
					" ",
					"P | C INSURANCE >",
				});

				ok = true;
			}

			else if (country == "IND | HEALTH CARE >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"BIOTECH >",
					" ",
					"HEALTH CARE SUPPLY CHAIN >",
					" ",
					"LARGE PHARMA >",
					" ",
					"LIFE SCIENCE EQUIP >",
					" ",
					"MANAGED CARE >",
					" ",
					"MEDICAL EQUIP >",
				});

				ok = true;
			}

			else if (country == "IND | INDUSTRIALS >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"AEROSPACE | DEFENSE >",
					" ",
					"AIRLINES >",
					" ",
					"CONSTRUCTION >",
					" ",
					"ELECTRICAL EQUIPMENT >",
					" ",
					"LOGISTICS SERVICES >",
					" ",
					"MACHINERY >",
					" ",
					"MARINE SHIPPING >",
					" ",
					"RAIL FREIGHT >",
					" ",
					"TRUCKING >",
				});

				ok = true;
			}

			else if (country == "IND | MATERIALS >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"AGRICULTURAL CHEMICALS >",
					" ",
					"ALUMINUM >",
					" ",
					"BASIC | DIVERSIFIED CHEMICALS >",
					" ",
					"BASE METALS >",
					" ",
					"BUILDING MATERIALS >",
					" ",
					"COAL >",
					" ",
					"COPPER >",
					" ",
					"IRON >",
					" ",
					"PAPER PACKAGING FOREST >",
					" ",
					"PRECIOUS METAL MINING >",
					" ",
					"SPECIALTY CHEMICALS >",
					" ",
					"STEEL PRODUCERS >",
				});

				ok = true;
			}

			else if (country == "IND | REAL ESTATE >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"HEALTH CARE REIT >",
					" ",
					"OFFICE REIT >",
					" ",
					"RESIDENTIAL REIT >",
					" ",
					"RETAIL REIT >",
				});

				ok = true;
			}

			else if (country == "IND | TECHNOLOGY >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
                    //"APPLICATION SOFTWARE >",
                    //" ",
                    "COMPUTER HARDWARE >",                   
                    //" ",
                    //"CONSUMER ELECTRONICS >",
                    //" ",
                    //"DATA NETWORKING EQUIP >",                                      
                    //" ",
                    //"INFRASTRUCTURE SOFTWARE >",
                    //" ",
                    //"IT SERVICES >",
                    //" ",
                    //"INFRASTRUCTURE SOFTWARE >",
                    //" ",
                    //"SEMICONDUCTOR DEVICES >",                    
                    " ",
					"SEMICONDUCTOR MFG >",
				});

				ok = true;
			}

			else if (country == "IND | UTILITIES >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"ELECTRIC UTILITIES >"
				});

				ok = true;
			}
			//else if (country == "CQG COMMODITIES >")
			//{
			//setNavigation(panel, mouseDownEvent, new string[]
			//{
			//"US COMMODITIES >",
			//"CBOT FINANCIALS >",
			//" ",
			//"CBOT GRAINS >",
			//" ",
			//"CME STOCK INDICES >",
			//" ",
			//"CME RATES >",
			//" ",
			//"CME FX >",
			//" ",
			//"CME MEATS >",
			//" ",
			//"CME LUMBER >",
			//" ",
			//"CME DAIRY >",
			//" ",
			//"COMEX METALS >",
			//" ",
			//"ICE SOFTS >",
			//" ",
			//"NYMEX ENERGY >",
			//" ",
			//"NYMEX METALS"
			//});

			//ok = true;
			//}

			else if (country == "CQG EQUITIES >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"US EQ NYSE 75 >",
					"",
					"US EQ NDX 100 >",
					" ",
					"CQG EQ NASDAQ >",
					"",
					"CQG EQ NYSE >",   
                    //" ",
                    //"CQG EQ TSX >",
                    //"",
                    //"CQG EQ TORNOTO VENTURE >",
                    //" ",
                    //"CQG EQ BORSA ITALIAN >",
                    //" ",
                    //"CQG EQ BATS >",
                    //" ",
                    //"CQG EQ Chi-X >",
                    //"",
                    //"CQG MOSCOW >",
                    //" ",
                    //"CQG EQ JSE >",
                    //"",
                    //"CQG EQ ASX >",            
                    //"",
                    //"CQG EQ SGX >",
                });

				ok = true;
			}
			else if (country == "CQG ETF >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"NYSE INDEX ETF >",
					"",
					"NYSE SECTOR ETF >",
				});

				ok = true;
			}
			//else if (country == "CQG FX & CRYPTO >")
			//{
			//    setNavigation(panel, mouseDownEvent, new string[]
			//    {
			//        "CME CRYPTO >",
			//        "",
			//        "CME FX >",
			//    });

			//    ok = true;
			//}
			//else if (country == "CQG INTEREST RATES >")
			//{
			//    setNavigation(panel, mouseDownEvent, new string[]
			//    {
			//        "CBOT RATES >",
			//        "",
			//        "CME RATES >",
			//    });

			//    ok = true;
			//}
			//else if (country == "CQG STOCK INDICES >")
			//{
			//    setNavigation(panel, mouseDownEvent, new string[]
			//    {
			//        "CBOT STOCK INDICES >",
			//        "",
			//        "CME STOCK INDICES >",
			//    });

			//    ok = true;
			//}
			else if (country == "N AMERICA RATES >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"US RATES >",
					" ",
					"CANADA RATES >",
					" ",
					"MEXICO RATES >"
				});

				ok = true;
			}
			else if (country == "CQG N AMERICA RATES >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"CQG US RATES >",
                    //" ",
                    //"CQG CANADA RATES >",
                    //" ",
                    //"CQG MEXICO RATES >"
                });

				ok = true;
			}
			else if (country == "CQG N AMERICA STK INDICES >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"CBOT STOCK INDICES >",
					" ",
					"CME STOCK INDICES >"
				});

				ok = true;
			}
			else if (country == "CQG S AMERICA STK INDICES >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"BMF STOCK INDICES >"
				});

				ok = true;
			}
			else if (country == "CQG N AMERICA RATES >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"CQG US RATES >",
					" ",
					"CQG CANADA RATES >",
					" ",
					"CQG MEXICO RATES >"
				});

				ok = true;
			}
			else if (country == "CQG WORLD CURRENCY >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"CME WORLD CURRENCY >",
					"SGX WORLD CURRENCY >",

				});

				ok = true;
			}

			else if (country == "CQG CRYPTO CURRENCY >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"DevX CRYPTO CURRENCY >",
                    //"MARKETS DIRECT FX CRYPTO CURRENCY >",
                    //" ",
                    //"ERIX CRYPTO CURRENCY >"

                });

				ok = true;
			}

			else if (country == "CQG CRYPTO FUTURES >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
                    //"BITNOMIAL CRYPTO FUTURES >",
                    //"CBOE FUT CRYPTO FUTURES >",
                    "CME CRYPTO FUTURES >",
                    //"FAIRX CRYPTO FUTURES >",
                    //"ICE US CRYPTO FUTURES >",
                    //" ",
                    //"EUREX CRYPTO FUTURES >",
                    //" ",
                    //"ASIA PACIFIC CRYPTO FUTURES >",
                    //"ICE SINGAPORE CRYPTO FUTURES >"

                });

				ok = true;
			}

			//else if (country == "CQG CRYPTO INDICES >")
			//{
			//    setNavigation(panel, mouseDownEvent, new string[]
			//    {
			//        "CBOE CRYPTOCURRENCY CRYPTO INDICES >",
			//        " ",
			//        "ICE DATA GLOBAL INDEX CRYPTO INDICES >"

			//    });

			//    ok = true;
			//}

			else if (country == "CQG WATER >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
                    //"BITNOMIAL CRYPTO FUTURES >",
                    //"CBOE FUT CRYPTO FUTURES >",
                    "CME WATER >",
                    //"FAIRX CRYPTO FUTURES >",
                    //"ICE US CRYPTO FUTURES >",
                    //" ",
                    //"EUREX CRYPTO FUTURES >",
                    //" ",
                    //"ASIA PACIFIC CRYPTO FUTURES >",
                    //"ICE SINGAPORE CRYPTO FUTURES >"

                });

				ok = true;
			}
			else if (country == "CQG HOUSING >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
                    //"BITNOMIAL CRYPTO FUTURES >",
                    //"CBOE FUT CRYPTO FUTURES >",
                    "CME HOUSING >",
                    //"FAIRX CRYPTO FUTURES >",
                    //"ICE US CRYPTO FUTURES >",
                    //" ",
                    //"EUREX CRYPTO FUTURES >",
                    //" ",
                    //"ASIA PACIFIC CRYPTO FUTURES >",
                    //"ICE SINGAPORE CRYPTO FUTURES >"

                });

				ok = true;
			}
			//else if (country == "CQG WEATHER >")
			//{
			//    setNavigation(panel, mouseDownEvent, new string[]
			//    {
			//        //"BITNOMIAL CRYPTO FUTURES >",
			//        //"CBOE FUT CRYPTO FUTURES >",
			//        "CME WEATHER >",
			//        //"FAIRX CRYPTO FUTURES >",
			//        //"ICE US CRYPTO FUTURES >",
			//        //" ",
			//        //"EUREX CRYPTO FUTURES >",
			//        //" ",
			//        //"ASIA PACIFIC CRYPTO FUTURES >",
			//        //"ICE SINGAPORE CRYPTO FUTURES >"

			//    });

			//    ok = true;
			//}

			else if (country == "S AMERICA RATES >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"BRAZIL RATES >",
					"",
					"CHILE RATES >",
					"",
					"COLUMBIA RATES >",
					"",
					"PERU RATES >",
				});

				ok = true;
			}

			else if (country == "EUROPE RATES >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"EUROZONE RATES >",
					"",
					"FRANCE RATES >",
					"",
					"GERMANY RATES >",
					"",
					"ITALY RATES >",
					"",
					"ENGLAND RATES >",
					"",
					"AUSTRIA RATES >",
					"",
					"BELGIUM RATES >",
					"",
					"CZECH REP RATES >",
					"",
					"DENMARK RATES >",
					"",
					"FINLAND RATES >",
					"",
					"GREECE RATES >",
					"",
					"HUNGARY RATES >",
					"",
					"IRELAND RATES >",
					"",
					"NETHERLANDS RATES >",
					"",
					"NORWAY RATES >",
					"",
					"POLAND RATES >",
					"",
					"PORTUGAL RATES >",
					"",
					"ROMANIA RATES >",
					"",
					"RUSSIA RATES >",
					"",
					"SLOVAKIA RATES >",
					"",
					"SPAIN RATES >",
					"",
					"SWEDEN RATES >",
					"",
					"SWITZERLAND RATES >",
					"",
					"TURKEY RATES >",
				});

				ok = true;
			}

			else if (country == "MEA RATES >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"ISRAEL RATES >",
					"",
					"SOUTH AFRICA RATES >",
				});

				ok = true;
			}

			else if (country == "ASIA RATES >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"CHINA RATES >",
					"",
					"HONG KONG RATES >",
					"",
					"INDIA RATES >",
					"",
					"INDONESIA RATES >",
					"",
					"JAPAN RATES >",
					"",
					"MALAYSIA RATES >",
					"",
					"PAKISTAN RATES >",
					"",
					"PHILIPPINES RATES >",
					"",
					"SINGAPORE RATES >",
					"",
					"SOUTH KOREA RATES >",
					"",
					"TAIWAN RATES >",
					"",
					"THAILAND RATES >"
				});

				ok = true;
			}
			else if (country == "WORLD BOND FUTURES >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"BOND FUTURES >",
				});

				ok = true;
			}
			else if (country == "WORLD EQUITY FUTURES >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"EQUITY FUTURES >",
				});

				ok = true;
			}

			else if (country == "WORLD CURRENCY >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
                    //"CURRENCY FUTURES >",
                    "USD BASE >",
					"EUR BASE >",
					"GBP BASE >",
					"G 10 >"
				});

				ok = true;
			}


			else if (country == "OCEANIA RATES >")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"AUSTRALIA RATES >",
					"",
					"NEW ZEALAND RATES >"
				});

				ok = true;
			}

			else if (country == "BRAZIL RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"BRAZI GENERIC >",
					"BRAZIL BREAKEVEN >",
					"BRAZIL CDS >",
					"BRAZIL BMF >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"BRAZI GENERIC",
					"BRAZIL BREAKEVEN",
					"BRAZIL CDS",
					"BRAZIL BMF"
				});

				ok = true;
			}

			else if (country == "COLUMBIA RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"COLUMBIA GENERIC >",
					"COLUMBIA CDS >",
					"COLUMBIA SWAPS >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"COLUMBIA GENERIC",
					"COLUMBIA CDS",
					"COLUMBIA SWAPS"
				});

				ok = true;
			}
			else if (country == "PERU RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"PERU GENERIC >",
					"PERU CDS >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"PERU GENERIC",
					"PERU CDS"
				});

				ok = true;
			}
			else if (country == "CHILE RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"CHILE GENERIC >",
					"CHILE CDS >",
					"CHILE SWAPS >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"CHILE GENERIC",
					"CHILE CDS",
					"CHILE SWAPS"
				});

				ok = true;
			}
			else if (country == "EUROZONE RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"EUROZONE GENERIC >",
					"EUROZONE CDS >",
					"EUROZONE SPREADS >",
					"EUROZONE ANNUAL SWAPS >",
					"EUROZONE SWAPS 3M EURIBOR >",
					"EUROZONE SWAPS EONIA >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"EUROZONE GENERIC",
					"EUROZONE CDS",
					"EUROZONE SPREADS",
					"EUROZONE ANNUAL SWAPS",
					"EUROZONE SWAPS 3M EURIBOR",
					"EUROZONE SWAPS EONIA"
				});

				ok = true;
			}
			else if (country == "FRANCE RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"FRANCE GENERIC >",
					"FRANCE CURVES >",
					"FRANCE BUTTERFLIES >",
					"FRANCE BREAKEVEN >",
					"FRANCE CDS >",
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"FRANCE GENERIC",
					"FRANCE CURVES",
					"FRANCE BUTTERFLIES",
					"FRANCE BREAKEVEN",
					"FRANCE CDS",
				});

				ok = true;
			}
			else if (country == "GERMANY RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"GERMANY GENERIC >",
					"GERMANY CURVES >",
					"GERMANY BUTTERFLIES >",
					"GERMANY BREAKEVEN >",
					"GERMANY CDS >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"GERMANY GENERIC",
					"GERMANY CURVES",
					"GERMANY BUTTERFLIES",
					"GERMANY BREAKEVEN",
					"GERMANY CDS"
				});

				ok = true;
			}
			else if (country == "ITALY RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"ITALY GENERIC >",
					"ITALY CURVES >",
					"ITALY BUTTERFLIES >",
					"ITALY BREAKEVEN >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"ITALY GENERIC",
					"ITALY CURVES",
					"ITALY BUTTERFLIES",
					"ITALY BREAKEVEN"
				});

				ok = true;
			}
			else if (country == "ENGLAND RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"ENGLAND GENERIC >",
					"ENGLAND CURVES >",
					"ENGLAND BUTTERFLIES >",
					"ENGLAND BREAKEVEN >",
					"ENGLAND CDS >",
					"ENGLAND SPREADS >",
					"ENGLAND SWAPS >",
					"ENGLAND SONIA OIS >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"ENGLAND GENERIC",
					"ENGLAND CURVES",
					"ENGLAND BUTTERFLIES",
					"ENGLAND BREAKEVEN",
					"ENGLAND CDS",
					"ENGLAND SPREADS",
					"ENGLAND SWAPS",
					"ENGLAND SONIA OIS"
				});

				ok = true;
			}
			else if (country == "AUSTRIA RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"AUSTRIA GENERIC >",
					"AUSTRIA CURVES >",
					"AUSTRIA BUTTERFLIES >",
					"AUSTRIA CDS >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"AUSTRIA GENERIC",
					"AUSTRIA CURVES",
					"AUSTRIA BUTTERFLIES",
					"AUSTRIA CDS"
				});

				ok = true;
			}
			else if (country == "BELGIUM RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"BELGIUM GENERIC >",
					"BELGIUM CURVES >",
					"BELGIUM BUTTERFLIES >",
					"BELGIUM CDS >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"BELGIUM GENERIC",
					"BELGIUM CURVES",
					"BELGIUM BUTTERFLIES",
					"BELGIUM CDS"
				});

				ok = true;
			}
			else if (country == "CZECH REP RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"CZECH REP GENERIC >",
					"CZECH REP CDS >",
					"CZECH REP SWAPS >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"CZECH REP GENERIC",
					"CZECH REP CDS",
					"CZECH REP SWAPS"
				});

				ok = true;
			}
			else if (country == "DENMARK RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"DENMARK GENERIC >",
					"DENMARK CURVES >",
					"DENMARK BUTTERFLIES >",
					"DENMARK BREAKEVEN >",
					"DENMARK CDS >",
					"DENMARK SWAPS >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"DENMARK GENERIC",
					"DENMARK CURVES",
					"DENMARK BUTTERFLIES",
					"DENMARK BREAKEVEN",
					"DENMARK CDS",
					"DENMARK SWAPS"
				});

				ok = true;
			}
			else if (country == "FINLAND RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"FINLAND GENERIC >",
					"FINLAND CURVES >",
					"FINLAND BUTTERFLIES >",
					"FINLAND CDS >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"FINLAND GENERIC",
					"FINLAND CURVES",
					"FINLAND BUTTERFLIES",
					"FINLAND CDS"
				});

				ok = true;
			}
			else if (country == "GREECE RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"GREECE GENERIC >",
					"GREECE CURVES >",
					"GREECE BUTTERFLIES >",
					"GREECE CDS >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"GREECE GENERIC",
					"GREECE CURVES",
					"GREECE BUTTERFLIES",
					"GREECE CDS"
				});

				ok = true;
			}
			else if (country == "HUNGARY RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"HUNGARY GENERIC >",
					"HUNGARY CDS >",
					"HUNGARY SWAPS >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"HUNGARY GENERIC",
					"HUNGARY CDS",
					"HUNGARY SWAPS"
				});

				ok = true;
			}
			else if (country == "IRELAND RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"IRELAND GENERIC >",
					"IRELAND CURVES >",
					"IRELAND BUTTERFLIES >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"IRELAND GENERIC",
					"IRELAND CURVES",
					"IRELAND BUTTERFLIES"
				});

				ok = true;
			}
			else if (country == "NETHERLANDS RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"NETHERLANDS GENERIC >",
					"NETHERLANDS CURVES >",
					"NETHERLANDS BUTTERFLIES >",
					"NETHERLANDS CDS >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"NETHERLANDS GENERIC",
					"NETHERLANDS CURVES",
					"NETHERLANDS BUTTERFLIES",
					"NETHERLANDS CDS"
				});

				ok = true;
			}
			else if (country == "NORWAY RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"NORWAY GENERIC >",
					"NORWAY CURVES >",
					"NORWAY BUTTERFLIES >",
					"NORWAY CDS >",
					"NORWAY SWAPS >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"NORWAY GENERIC",
					"NORWAY CURVES",
					"NORWAY BUTTERFLIES",
					"NORWAY CDS",
					"NORWAY SWAPS"
				});

				ok = true;
			}
			else if (country == "POLAND RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"POLAND GENERIC >",
					"POLAND BREAKEVEN >",
					"POLAND CDS >",
					"POLAND SPREADS >",
					"POLAND SWAPS >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"POLAND GENERIC",
					"POLAND BREAKEVEN",
					"POLAND CDS",
					"POLAND SPREADS",
					"POLAND SWAPS"
				});

				ok = true;
			}
			else if (country == "PORTUGAL RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"PORTUGAL GENERIC >",
					"PORTUGAL CURVES >",
					"PORTUGAL BUTTERFLIES >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"PORTUGAL GENERIC",
					"PORTUGAL CURVES",
					"PORTUGAL BUTTERFLIES"
				});

				ok = true;
			}
			else if (country == "ROMANIA RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"ROMANIA GENERIC >",
					"ROMANIA BREAKEVEN >",
					"ROMANIA CDS >",
					"ROMANIA SWAPS >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"ROMANIA GENERIC",
					"ROMANIA BREAKEVEN",
					"ROMANIA CDS",
					"ROMANIA SWAPS"
				});

				ok = true;
			}
			else if (country == "RUSSIA RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"RUSSIA GENERIC >",
					"RUSSIA BREAKEVEN >",
					"RUSSIA CDS >",
					"RUSSIA SWAPS >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"RUSSIA GENERIC",
					"RUSSIA BREAKEVEN",
					"RUSSIA CDS",
					"RUSSIA SWAPS"
				});

				ok = true;
			}
			else if (country == "SLOVAKIA RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"SLOVAKIA GENERIC >",
					"SLOVAKIA BREAKEVEN >",
					"SLOVAKIA CDS >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"SLOVAKIA GENERIC",
					"SLOVAKIA BREAKEVEN",
					"SLOVAKIA CDS"
				});

				ok = true;
			}
			else if (country == "SPAIN RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"SPAIN GENERIC >",
					"SPAIN CURVES >",
					"SPAIN BUTTERFLIES >",
					"SPAIN BREAKEVEN >",
					"SPAIN CDS >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"SPAIN GENERIC",
					"SPAIN CURVES",
					"SPAIN BUTTERFLIES",
					"SPAIN BREAKEVEN",
					"SPAIN CDS"
				});

				ok = true;
			}
			else if (country == "SWEDEN RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"SWEDEN GENERIC >",
					"SWEDEN CURVES >",
					"SWEDEN BUTTERFLIES >",
					"SWEDEN BREAKEVEN >",
					"SWEDEN CDS >",
					"SWEDEN SWAPS >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"SWEDEN GENERIC",
					"SWEDEN CURVES",
					"SWEDEN BUTTERFLIES",
					"SWEDEN BREAKEVEN",
					"SWEDEN CDS",
					"SWEDEN SWAPS"
				});

				ok = true;
			}
			else if (country == "SWITZERLAND RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"SWITZERLAND GENERIC >",
					"SWITZERLAND CURVES >",
					"SWITZERLAND BUTTERFLIES >",
					"SWITZERLAND SWAPS >",
					"SWITZERLAND SPREADS >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"SWITZERLAND GENERIC",
					"SWITZERLAND CURVES",
					"SWITZERLAND BUTTERFLIES",
					"SWITZERLAND SWAPS",
					"SWITZERLAND SPREADS"
				});

				ok = true;
			}
			else if (country == "TURKEY RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"TURKEY GENERIC >",
					"TURKEY BREAKEVEN >",
					"TURKEY CDS >",
					"TURKEY SWAPS >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"TURKEY GENERIC",
					"TURKEY BREAKEVEN",
					"TURKEY CDS",
					"TURKEY SWAPS"
				});

				ok = true;
			}
			else if (country == "UKRAINE RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"UKRAINE GENERIC >",
					"UKRAINE BREAKEVEN >",
					"UKRAINE CDS >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"UKRAINE GENERIC",
					"UKRAINE BREAKEVEN",
					"UKRAINE CDS"
				});

				ok = true;
			}
			else if (country == "ISRAEL RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"ISRAEL GENERIC >",
					"ISRAEL BREAKEVEN >",
					"ISRAEL SWAPS >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"ISRAEL GENERIC",
					"ISRAEL BREAKEVEN",
					"ISRAEL SWAPS"
				});

				ok = true;
			}
			else if (country == "SOUTH AFRICA RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"SOUTH AFRICA GENERIC >",
					"SOUTH AFRICA BREAKEVEN >",
					"SOUTH AFRICA CDS >",
					"SOUTH AFRICA SWAPS >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"SOUTH AFRICA GENERIC",
					"SOUTH AFRICA BREAKEVEN",
					"SOUTH AFRICA CDS",
					"SOUTH AFRICA SWAPS"
				});

				ok = true;
			}
			else if (country == "JAPAN RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"JAPAN GENERIC >",
					"JAPAN CURVES >",
					"JAPAN BUTTERFLIES >",
					"JAPAN BREAKEVEN >",
					"JAPAN CDS >",
					"JAPAN SPREADS >",
					"JAPAN SWAPS >",
					"JAPAN SWAPS TOKYO CL >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"JAPAN GENERIC",
					"JAPAN CURVES",
					"JAPAN BUTTERFLIES",
					"JAPAN BREAKEVEN",
					"JAPAN CDS",
					"JAPAN SPREADS",
					"JAPAN SWAPS",
					"JAPAN SWAPS TOKYO CL"
				});

				ok = true;
			}
			else if (country == "MALAYSIA RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"MALAYSIA GENERIC >",
					"MALAYSIA CDS >",
					"MALAYSIA SWAPS >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"MALAYSIA GENERIC",
					"MALAYSIA CDS",
					"MALAYSIA SWAPS"
				});

				ok = true;
			}
			else if (country == "PAKISTAN RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"PAKISTAN GENERIC >",
					"PAKISTAN CDS >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"PAKISTAN GENERIC",
					"PAKISTAN CDS"
				});

				ok = true;
			}
			else if (country == "PHILIPPINES RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"PHILIPPINES GENERIC >",
					"PHILIPPINES CDS >",
					"PHILIPPINES SWAPS >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"PHILIPPINES GENERIC",
					"PHILIPPINES CDS",
					"PHILIPPINES SWAPS"
				});

				ok = true;
			}
			else if (country == "SINGAPORE RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"SINGAPORE GENERIC >",
					"SINGAPORE CURVES >",
					"SINGAPORE BUTTERFLIES >",
					"SINGAPORE SWAPS >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"SINGAPORE GENERIC",
					"SINGAPORE CURVES",
					"SINGAPORE BUTTERFLIES",
					"SINGAPORE SWAPS"
				});

				ok = true;
			}
			else if (country == "SOUTH KOREA RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"SOUTH KOREA GENERIC >",
					"SOUTH KOREA CDS >",
					"SOUTH KOREA OFFSHORE KRW USD >",
					"SOUTH KOREA ONSHORE KRW USD >",
					"SOUTH KOREA ONSHORE KRW KRW >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"SOUTH KOREA GENERIC",
					"SOUTH KOREA CDS",
					"SOUTH KOREA OFFSHORE KRW USD",
					"SOUTH KOREA ONSHORE KRW USD",
					"SOUTH KOREA ONSHORE KRW KRW"
				});

				ok = true;
			}
			else if (country == "TAIWAN RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"TAIWAN GENERIC >",
					"TAIWAN ONSHORE TWD TWD >",
					"TAIWAN ONSHORE TWD USD >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"TAIWAN GENERIC",
					"TAIWAN ONSHORE TWD TWD",
					"TAIWAN ONSHORE TWD USD"
				});

				ok = true;
			}
			else if (country == "THAILAND RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"THAILAND GENERIC >",
					"THAILAND CDS >",
					"THAILAND ONSHORE THB THB >",
					"THAILAND ONSHORE THB USD >",
					"THAILAND OFFSHORE THB USD >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"THAILAND GENERIC",
					"THAILAND CDS",
					"THAILAND ONSHORE THB THB",
					"THAILAND ONSHORE THB USD",
					"THAILAND OFFSHORE THB USD"
				});

				ok = true;
			}
			else if (country == "CHINA RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"CHINA GENERIC >",
					"CHINA BREAKEVEN >",
					"CHINA CDS >",
					"CHINA ONSHORE 7D >",
					"CHINA ONSHORE 3M >",
					"CHINA USDCNY Non Del >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"CHINA GENERIC",
					"CHINA BREAKEVEN",
					"CHINA CDS",
					"CHINA ONSHORE 7D",
					"CHINA ONSHORE 3M",
					"CHINA USDCNY Non Del"
				});

				ok = true;
			}
			else if (country == "HONG KONG RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"HONG KONG GENERIC >",
					"HONG KONG CURVES >",
					"HONG KONG BUTTERFLIES >",
					"HONG KONG CDS >",
					"HONG KONG SPREADS >",
					"HONG KONG SWAPS >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"HONG KONG GENERIC",
					"HONG KONG CURVES",
					"HONG KONG BUTTERFLIES",
					"HONG KONG CDS",
					"HONG KONG SPREADS",
					"HONG KONG SWAPS"
				});

				ok = true;
			}
			else if (country == "INDIA RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"INDIA GENERIC >",
					"INDIA CDS >",
					"INDIA SWAPS >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"INDIA GENERIC",
					"INDIA CDS",
					"INDIA SWAPS"
				});

				ok = true;
			}
			else if (country == "INDONESIA RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"INDONESIA GENERIC >",
					"INDONESIA CDS >",
					"INDONESIA SWAPS 3M >",
					"INDONESIA OFFSHORE NDS >",
					"INDONESIA IONA >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"INDONESIA GENERIC",
					"INDONESIA CDS",
					"INDONESIA SWAPS 3M",
					"INDONESIA OFFSHORE NDS",
					"INDONESIA IONA"
				});

				ok = true;
			}
			else if (country == "AUSTRALIA RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"AUSTRALIA GENERIC >",
					"AUSTRALIA CURVES >",
					"AUSTRALIA BUTTERFLIES >",
					"AUSTRALIA BREAKEVEN >",
					"AUSTRALIA CDS >",
					"AUSTRALIA SPREADS >",
					"AUSTRALIA SWAPS >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"AUSTRALIA GENERIC",
					"AUSTRALIA CURVES",
					"AUSTRALIA BUTTERFLIES",
					"AUSTRALIA BREAKEVEN",
					"AUSTRALIA CDS",
					"AUSTRALIA SPREADS",
					"AUSTRALIA SWAPS"
				});

				ok = true;
			}
			else if (country == "NEW ZEALAND RATES >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"NEW ZEALAND GENERIC >",
					"NEW ZEALAND CURVES >",
					"NEW ZEALAND BUTTERFLIES >",
					"NEW ZEALAND CDS >",
					"NEW ZEALAND SPREADS >",
					"NEW ZEALAND SWAPS >"
				 });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"NEW ZEALAND GENERIC",
					"NEW ZEALAND CURVES",
					"NEW ZEALAND BUTTERFLIES",
					"NEW ZEALAND CDS",
					"NEW ZEALAND SPREADS",
					"NEW ZEALAND SWAPS"
				});

				ok = true;
			}

			else if (country == "FINANCIAL")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"US FINANCIAL"
				});

				ok = true;
			}
			else if (country == "WORLD >" || country == "WORLD")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"BWORLD:WORLD | INDUSTRIES >",
				});
				ok = true;
			}
			else if (country == "EQ | AMERICAS >" || country == "AMERICAS")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"BWORLDUS:AMERICAS | INDUSTRIES >",
				});
				ok = true;
			}
			else if (country == "EMEA >" || country == "EMEA")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"BWORLDEU:EMEA | INDUSTRIES >",
				});
				ok = true;
			}
			else if (country == "EQ | ASIA PACIFIC >" || country == "ASIA PACIFIC")
			{
				setNavigation(panel, mouseDownEvent, new string[]
				{
					"BWORLDPR:ASIA PACIFIC | INDUSTRIES >",
				});
				ok = true;
			}
			else if (country == "CANADA EQ >" || country == "CANADA >" || country == "Canada")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"SPTSX:SPTSX | INDUSTRIES >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"SPTSX:SPTSX | INDUSTRIES >"
				});
				ok = true;
			}
			else if (country == "MEXICO EQ >" || country == "MEXICO >" || country == "Mexico")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] { "MEXBOL >", "INMEX >", "IMC30 >" });
				else setNavigation(panel, mouseDownEvent, new string[] { "MEXBOL", "INMEX", "TXEQ" });
				ok = true;
			}
			else if (country == "BRAZIL EQ >" || country == "BRAZIL >" || country == "Brazil")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] { "IBOV >", "IBX >", "IBX50 >" });
				else setNavigation(panel, mouseDownEvent, new string[] { "IBOV", "IBX", "IBX50" });
				ok = true;
			}
			else if (country == "ARGENTINA EQ >" || country == "ARGENTINA >" || country == "Argentina >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] { "MERVAL >", "IBG >" });
				else setNavigation(panel, mouseDownEvent, new string[] { "MERVAL", "IBG" });
				ok = true;
			}
			else if (country == "PERU EQ >" || country == "PERU >" || country == "Peru >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] { "SPBLPGPT >" });
				else setNavigation(panel, mouseDownEvent, new string[] { "SPBLPGPT" });
				ok = true;
			}
			else if (country == "COLOMBIA EQ >" || country == "COLOMBIA >" || country == "Colombia >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] { "COLCAP >" });
				else setNavigation(panel, mouseDownEvent, new string[] { "COLCAP" });
				ok = true;
			}
			else if (country == "CHILE EQ >" || country == "CHILE >" || country == "CHILE >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] { "IPSA >" });
				else setNavigation(panel, mouseDownEvent, new string[] { "IPSA" });
				ok = true;
			}

			else if (country == "UK EQ >" || country == "UK >" || country == "UK")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"UKX:UKX >",
					"AXX:AXX | INDUSTRIES >",
					"ASX:ASX | INDUSTRIES >",
					"NMX:NMX | INDUSTRIES >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"UKX:UKX",
					"AXX:AXX | INDUSTRIES >",
					"ASX:ASX | INDUSTRIES >",
					"NMX:NMX | INDUSTRIES"
				});
				ok = true;
			}

			else if (country == "EURO EQ >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"BE500:BE500 | INDUSTRIES >",
					"SXXE:SXXE | INDUSTRIES >",
					"SXXP:SXXP | INDUSTRIES >",
					"E300:E300 | INDUSTRIES >",
					"SPEU:SPEU | INDUSTRIES >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"BE500:BE500 | INDUSTRIES >",
					"SXXE:SXXE | INDUSTRIES >",
					"SXXP:SXXP | INDUSTRIES >",
					"E300:E300 | INDUSTRIES >",
					"SPEU:SPEU | INDUSTRIES >"
				});
				ok = true;
			}

			else if (country == "FRANCE EQ >" || country == "FRANCE >" || country == "France")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"SBF250:SBF250 | INDUSTRIES >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"SBF250:SBF250 | INDUSTRIES >"
				});
				ok = true;
			}
			else if (country == "GERMANY EQ >" || country == "GERMANY >" || country == "Germany")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"DAX:DAX >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"DAX:DAX"
				});
				ok = true;
			}
			else if (country == "SPAIN EQ >" || country == "SPAIN >" || country == "Spain")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"MADX:MADX | INDUSTRIES >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"MADX:MADX | INDUSTRIES >"
				});
				ok = true;
			}
			else if (country == "SWITZERLAND EQ >" || country == "SWITZERLAND >" || country == "Switzerland")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"SMI:SMI >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"SMI:SMI"
				});
				ok = true;
			}
			else if (country == "ITALY EQ >" || country == "ITALY >" || country == "Italy")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"ITLMS:ITLMS | INDUSTRIES >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"ITLMS:ITLMS | INDUSTRIES >"
				});
				ok = true;
			}
			else if (country == "IRELAND EQ >" || country == "IRELAND >" || country == "Ireland")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"ISEQ:ISEQ | INDUSTRIES >",
					"ISEF:ISEF >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"ISEQ:ISEQ | INDUSTRIES >",
					"ISEF:ISEF"
				});
				ok = true;
			}
			else if (country == "PORTUGAL EQ >" || country == "PORTUGAL >" || country == "Portugal")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"BVLX:BVLX | INDUSTRIES >",
                    //"PSI20:PSI20 >",
                });

				else setNavigation(panel, mouseDownEvent, new string[] {
					"BVLX:BVLX | INDUSTRIES >"
				});
				ok = true;
			}
			else if (country == "BELGIUM EQ >" || country == "BELGIUM >" || country == "Belgium >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"BEL20:BEL20 >",
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"BEL20:BEL20",
				});
				ok = true;
			}
			else if (country == "CROATIA EQ >" || country == "CROATIA >" || country == "Croatia >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"CRO:CRO >",
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"CRO:CRO",
				});
				ok = true;
			}
			else if (country == "CZECH REPUBLIC EQ >" || country == "CZECH REPUBLIC >" || country == "CzechRepublic >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"PX:PX >",
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"PX:PX",
				});
				ok = true;
			}
			else if (country == "SLOVAKIA EQ >" || country == "SLOVAKIA >" || country == "Slovakia >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"SKSM:SKSM >",
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"SKSM:SKSM",
				});
				ok = true;
			}
			else if (country == "ESTONIA EQ >" || country == "ESTONIA >" || country == "Estonia >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"TALSE:TALSE >",
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"TALSE:TALSE",
				});
				ok = true;
			}
			else if (country == "ROMANIA EQ >" || country == "ROMANIA >" || country == "Romania >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"BET:BET >",
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"BET:BET",
				});
				ok = true;
			}
			else if (country == "SLOVENIA EQ >" || country == "SLOVENIA >" || country == "Slovenia >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"SBITOP:SBITOP >",
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"SBITOP:SBITOP",
				});
				ok = true;
			}
			else if (country == "SERBIA EQ >" || country == "SERBIA >" || country == "Serbia >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"BELEX15:BELEX15 >",
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"BELEX15:BELEX15",
				});
				ok = true;
			}
			else if (country == "BOSNIA EQ >" || country == "BOSNIA >" || country == "Bosnia >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"SASX10:SASX10 >",
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"SASX10:SASX10",
				});
				ok = true;
			}
			else if (country == "CYPRUS EQ >" || country == "CYPRUS >" || country == "Cyprus >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"CYSMMAPA:CYSMMAPA >",
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"CYSMMAPA:CYSMMAPA",
				});
				ok = true;
			}
			else if (country == "ICELAND EQ >" || country == "ICELAND >" || country == "Iceland >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"ICEXI:ICEXI >",
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"ICEXI:ICEXI",
				});
				ok = true;
			}
			else if (country == "LATVIA EQ >" || country == "LATVIA >" || country == "Latvia >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"RIGSE:RIGSE >",
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"RIGSE:RIGSE",
				});
				ok = true;
			}
			else if (country == "BULGARIA EQ >" || country == "BULGARIA >" || country == "Bulgaria >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"SOFIX:SOFIX >",
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"SOFIX:SOFIX",
				});
				ok = true;
			}
			else if (country == "LITHUANIA EQ >" || country == "LITHUANIA >" || country == "Lithuania >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"VILSE:VILSE >",
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"VILSE:VILSE",
				});
				ok = true;
			}
			else if (country == "UKRAINE EQ >" || country == "UKRAINE >" || country == "Ukraine >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"PFTS:PFTS >",
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"PFTS:PFTS",
				});
				ok = true;
			}
			else if (country == "KAZAKHSTAN EQ >" || country == "KAZAKHSTAN >" || country == "Kazakhstan >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"KZKAK:KZKAK >",
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"KZKAK:KZKAK",
				});
				ok = true;
			}
			else if (country == "NETHERLANDS EQ >" || country == "NETHERLANDS >" || country == "Netherlands")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"AEX:AEX >",
					"AMX:AMX >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"AEX:AEX",
					"AMX:AMX"
				});
				ok = true;
			}
			else if (country == "DENMARK EQ >" || country == "DENMARK >" || country == "Denmark")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
                    //"KFX:KFX >",
                    "KAX:KAX | INDUSTRIES >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"KAX:KAX | INDUSTRIES >"
				});
				ok = true;
			}
			else if (country == "FINLAND EQ >" || country == "FINLAND >" || country == "Finland")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"HEX:HEX | INDUSTRIES >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"HEX:HEX | INDUSTRIES >"
				});
				ok = true;
			}
			else if (country == "NORWAY EQ >" || country == "NORWAY >" || country == "Norway")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"OBX:OBX >",
					"OBXP:OBXP >",
					"OSEAX:OSEAX >",
					"OSEBX:OSEBX >",
					"OSEFX:OSEFX >",
					"OSESX:OSESX >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"OBX:OBX",
					"OBXP:OBXP",
					"OSEAX:OSEAX",
					"OSEBX:OSEBX",
					"OSEFX:OSEFX",
					"OSESX:OSESX"
				});
				ok = true;
			}
			else if (country == "SWEDEN EQ >" || country == "SWEDEN >" || country == "Sweden")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"SAX:SAX | INDUSTRIES >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"SAX:SAX | INDUSTRIES >"
				});
				ok = true;
			}
			else if (country == "AUSTRIA EQ >" || country == "AUSTRIA >" || country == "Austria")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"ATX:ATX >",
					"WBI:WBI >",
					"ATXPRIME:ATXPRIME >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"ATX:ATX",
					"WBI:WBI",
					"ATXPRIME:ATXPRIME"
				});
				ok = true;
			}
			else if (country == "GREECE EQ >" || country == "GREECE >" || country == "Greece")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"ASE:ASE >",
					"FTASE:FTASE >",
					"FTSEM:FTSEM >"

				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"ASE:ASE",
					"FTASE:FTASE",
					"FTSEM:FTSEM"
				});
				ok = true;
			}
			else if (country == "POLAND EQ >" || country == "POLAND >" || country == "Poland")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"WIG:WIG >",
					"WIG20:WIG20 >",
					"SWIG80:SWIG80 >",
					"MIDWIG:MIDWIG >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"WIG:WIG",
					"WIG20:WIG20",
					"SWIG80:SWIG80",
					"MIDWIG:MIDWIG"
				});
				ok = true;
			}
			else if (country == "RUSSIA EQ >" || country == "RUSSIA >" || country == "Russia")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"IMOEX:IMOEX >",
					"RTSI$:RTSI$ >",
					"CRTX:CRTX >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"IMOEX:IMOEX",
					"RTSI$:RTSI$",
					"CRTX:CRTX"
				});
				ok = true;
			}
			else if (country == "HUNGARY EQ >" || country == "HUNGARY >" || country == "Hungary")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"BUX:BUX >",
					"CHTX:CHTX >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"BUX:BUX",
					"CHTX:CHTX"
				});
				ok = true;
			}
			else if (country == "TURKEY EQ >" || country == "TURKEY >" || country == "Turkey")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"XU100:XU100 >",
					"XUO30:XUO30 >",
					"TR201:TR201 >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"XU100:XU100",
					"XUO30:XUO30",
					"TR201:TR201"
				});
				ok = true;
			}
			else if (country == "BAHRAIN EQ >" || country == "BAHRAIN >" || country == "Bahrain >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"BHSEASI:BHSEASI >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"BHSEASI:BHSEASI"
				});
				ok = true;
			}
			else if (country == "JORDAN EQ >" || country == "JORDAN >" || country == "Jordan >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"JOSMGNFF:JOSMGNFF >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"JOSMGNFF:JOSMGNFF"
				});
				ok = true;
			}
			else if (country == "OMAN EQ >" || country == "OMAN >" || country == "Oman >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"MSM30:MSM30 >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"MSM30:MSM30"
				});
				ok = true;
			}
			else if (country == "LEBANON EQ >" || country == "LEBANON >" || country == "Lebanon >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
				   "BLOM:BLOM >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"BLOM:BLOM"
				});
				ok = true;
			}
			else if (country == "SOUTH AFRICA EQ >" || country == "SOUTH AFRICA >" || country == "SouthAfrica")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"JALSH:JALSH | INDUSTRIES >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"JALSH:JALSH | INDUSTRIES >"
				});
				ok = true;
			}
			else if (country == "EGYPT EQ" || country == "EGYPT >" || country == "Egypt")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"HERMES:HERMES >",
					"CASE:CASE >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"HERMES:HERMES",
					"CASE:CASE"
				});
				ok = true;
			}
			else if (country == "MOROCCO EQ >" || country == "MOROCCO >" || country == "Morocco")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"MOSENEW:MOSENEW >",
					"MOSEMDX:MOSEMDX >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"MOSENEW:MOSENEW",
					"MOSEMDX:MOSEMDX"
				});
				ok = true;
			}
			else if (country == "KUWAIT EQ >" || country == "KUWAIT >" || country == "Kuwait")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"SECTMIND:SECTMIND | INDUSTRIES >",
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"SECTMIND:SECTMIND | INDUSTRIES >",
				});
				ok = true;
			}
			else if (country == "NIGERIA EQ >" || country == "NIGERIA >" || country == "Nigeria >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"NGSE30:NGSE30 >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"NGSE30:NGSE30"
				});
				ok = true;
			}
			else if (country == "TUNISIA EQ >" || country == "TUNISIA >" || country == "Tunisia >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"TUSISE:TUSISE >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"TUSISE:TUSISE"
				});
				ok = true;
			}
			else if (country == "BOTSWANA EQ >" || country == "BOTSWANA >" || country == "Botswana >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"BGSMDC:BGSMDC >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"BGSMDC:BGSMDC "
				});
				ok = true;
			}
			else if (country == "KENYA EQ >" || country == "KENYA >" || country == "Kenya >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"KNSMIDX:KNSMIDX >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"KNSMIDX:KNSMIDX"
				});
				ok = true;
			}
			else if (country == "GHANA EQ >" || country == "GHANA >" || country == "Ghana >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"GGSECI:GGSECI >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"GGSECI:GGSECI"
				});
				ok = true;
			}
			else if (country == "ZIMBABWE EQ >" || country == "ZIMBABWE >" || country == "Zimbabwe >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"ZHINDUSD:ZHINDUSD >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
				   "ZHINDUSD:ZHINDUSD"
				});
				ok = true;
			}
			else if (country == "MAURITIUS EQ >" || country == "MAURITIUS >" || country == "Mauritius >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"SEMDEX:SEMDEX >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
				   "SEMDEX:SEMDEX"
				});
				ok = true;
			}
			else if (country == "ISRAEL EQ >" || country == "ISRAEL >" || country == "Israel")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"TA-25:TA-25 >",
					"TA-125:TA-125 >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
				   "TA-25:TA-25",
					"TA-125:TA-125"
				});
				ok = true;
			}
			else if (country == "UAE EQ >" || country == "UAE >" || country == "UAE")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"DFMGI:DFMGI | INDUSTRIES >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
				   "DFMGI:DFMGI | INDUSTRIES >"
				});
				ok = true;
			}
			else if (country == "CHINA EQ >" || country == "CHINA >" || country == "China")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"SHCOMP:SHCOMP | INDUSTRIES >",
					"SHSZ300:SHSZ300 | INDUSTRIES >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"SHCOMP:SHCOMP | INDUSTRIES >",
					"SHSZ300:SHSZ300 | INDUSTRIES >"
				});
				ok = true;
			}
			else if (country == "HONG KONG EQ >" || country == "HONG KONG >" || country == "HongKong")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"HSI:HSI | INDUSTRIES >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"HSI:HSI | INDUSTRIES >"
				});
				ok = true;
			}
			else if (country == "INDIA EQ >" || country == "INDIA >" || country == "India")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"NIFTY:NIFTY >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"NIFTY:NIFTY"
				});
				ok = true;
			}
			else if (country == "JAPAN EQ >" || country == "JAPAN >" || country == "Japan")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"TPX:TPX | INDUSTRIES >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"TPX:TPX | INDUSTRIES >"
				});
				ok = true;
			}
			else if (country == "TAIWAN EQ >" || country == "TAIWAN >" || country == "Taiwan")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"TWSE:TWSE | INDUSTRIES >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"TWSE:TWSE | INDUSTRIES >"
				});
				ok = true;
			}
			else if (country == "BANGLADESH EQ >" || country == "BANGLADESH >" || country == "BANGLADESH")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"DS30:DS30 >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"DS30:DS30"
				});
				ok = true;
			}
			else if (country == "SOUTH KOREA EQ >" || country == "SOUTH KOREA >" || country == "SouthKorea")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"KOSPI:KOSPI | INDUSTRIES >",
					"KOSDAQ:KOSDAQ | INDUSTRIES >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"KOSPI:KOSPI | INDUSTRIES >",
					"KOSDAQ:KOSDAQ | INDUSTRIES >"
				});
				ok = true;
			}
			else if (country == "VIETNAM EQ" || country == "VIETNAM >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"VNINDEX:VNINDEX >",
					"VHINDEX:VHINDEX >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"VNINDEX:VNINDEX",
					"VHINDEX:VHINDEX"
				});
				ok = true;
			}
			else if (country == "PAKISTAN EQ" || country == "PAKISTAN >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"KSE100:KSE100 >",
					"KSE30:KSE30 >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"KSE100:KSE100",
					"KSE30:KSE30"
				});
				ok = true;
			}
			else if (country == "THAILAND EQ >" || country == "THAILAND >" || country == "Thailand")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"SET:SET | INDUSTRIES >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"SET:SET | INDUSTRIES >"
				});
				ok = true;
			}
			else if (country == "INDONESIA EQ >" || country == "INDONESIA >" || country == "Indonesia")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"JCI:JCI | INDUSTRIES >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"JCI:JCI | INDUSTRIES >"
				});
				ok = true;
			}
			else if (country == "SINGAPORE EQ >" || country == "SINGAPORE >" || country == "Singapore")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"FSTAS:FSTAS | INDUSTRIES >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"FSTAS:FSTAS | INDUSTRIES >"
				});
				ok = true;
			}
			else if (country == "PHILIPPINES EQ >" || country == "PHILIPPINES >" || country == "Philippines >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"PCOMP:PCOMP >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"PCOMP:PCOMP"
				});
				ok = true;
			}
			else if (country == "SRI LANKA EQ >" || country == "SRI LANKA >" || country == "Sri Lanka >")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"CSEALL:CSEALL >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"CSEALL:CSEALL"
				});
				ok = true;
			}
			else if (country == "MALAYSIA EQ >" || country == "MALAYSIA >" || country == "Malaysia")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"FBMKLCI:FBMKLCI >",
					"FBMEMAS:FBMEMAS >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"FBMKLCI:FBMKLCI",
					"FBMEMAS:FBMEMAS"
				});
				ok = true;
			}
			else if (country == "AUSTRALIA EQ >" || country == "AUSTRALIA >" || country == "Australia")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"AS51:AS51 | INDUSTRIES >",
					"AS52:AS52 | INDUSTRIES >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"AS51:AS51 | INDUSTRIES >",
					"AS52:AS52 | INDUSTRIES >"
				});
				ok = true;
			}
			else if (country == "NEW ZEALAND EQ >" || country == "NEW ZEALAND >" || country == "NewZealand")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {

					"NZSE10:NZSE10 >",
					"NZSX15G:NZSX15G >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"NZSE10:NZSE10",
					"NZSX15G:NZSX15G"
				});
				ok = true;
			}
			else if (country == "QATAR EQ >" || country == "QATAR >" || country == "Qatar")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"DSM:DSM | INDUSTRIES >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"DSM:DSM | INDUSTRIES >"
				});
				ok = true;
			}
			else if (country == "SAUDI ARABIA EQ >" || country == "SAUDI ARABIA >" || country == "SaudiArabia")
			{
				if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"SASEIDX:SASEIDX | INDUSTRIES >"
				});

				else setNavigation(panel, mouseDownEvent, new string[] {
					"SASEIDX:SASEIDX | INDUSTRIES >"
				});
				ok = true;
			}
			return ok;
		}

		StackPanel _workSheetPanel;
		MouseButtonEventHandler _workSheetMouseDownEvent;

		private void setWorksheetNavigation(StackPanel panel, MouseButtonEventHandler mouseDownEvent)
		{
			_workSheetPanel = panel;
			_workSheetMouseDownEvent = mouseDownEvent;

			var names = Portfolio.GetWorksheetNames();
			setNavigation(panel, mouseDownEvent, names.ToArray());

			var label = new Label();
			label.Content = "REFRESH WORKSHEETS";
			label.Height = 30;
			label.FontSize = 11;
			label.Margin = new Thickness(0, 0, 0, 0);
			label.Padding = new Thickness(0, 0, 0, 0);
			label.Foreground = Brushes.WhiteSmoke;
			label.Background = Brushes.Transparent;
			label.MouseDown += RefreshWorksheets;
			label.MouseEnter += Button_MouseEnter;
			label.MouseLeave += Button_MouseLeave;
			label.Cursor = Cursors.Hand;
			panel.Children.Insert(0, label);
		}

		private void RefreshWorksheets(object sender, MouseButtonEventArgs e)
		{
			Portfolio.UpdateWorksheets();
			setWorksheetNavigation(_workSheetPanel, _workSheetMouseDownEvent);
		}

		public bool setNavigationLevel3(string country, string group, StackPanel panel, MouseButtonEventHandler mouseDownEvent)
		{
			_activeNavigationPathIndex = 3;
			_navigationPath[2] = group;
			_navigationPath[3] = "";
			_navigationPath[4] = "";
			_navigationPath[5] = "";

			panel.Children.Clear();

			string[] fields = country.Split(':');
			country = (fields.Length > 1) ? fields[1] : fields[0];

			bool ok = false;

			fields = group.Split(':');
			string groupSymbol = fields[0];

			if (country == "SPX >")
			{
				if (groupSymbol == "SPXL3")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
							"S5AEROX:SPX AERO & DEF >",
							"S5AIRFX:SPX AIR FRT & LOG >",
							"S5AIRLX:SPX AIRLINES >",
							"S5AUTC:SPX AUTO COMP >",
							"S5AUTO:SPX AUTOMOBILES >",
							"S5BEVG:SPX BEVERAGES >",
							"S5BIOTX:SPX BIOTECH >",
							"S5BUILX:SPX BLDG PRODS >",
							"S5CAPM:SPX CAPITAL MKTS >",
							"S5CBNK:SPX COMM BANKS >",
							"S5CFINX:SPX CONS FINANCE >",
							"S5CHEM:SPX CHEMICALS >",
							"S5CMPE:SPX COMPUTERS & PER >",
							"S5COMM:SPX COMMUNICATION EQP >",
							"S5COMSX:SPX COMMERCIAL SRBS >",
							"S5CONP:SPX CONTAINER & PKG >",
							"S5CSTEX:SPX CONST & ENG >",
							"S5CSTMX:SPX CONST MATERIAL >",
							"S5DCON:SPX DIVERSIFIED SRVC >",
							"S5DISTX:SPX DISBRIBUTORS >",
							"S5DIVT:SPX DIV TEL SVC >",
							"S5DVFS:SPX DIV FIN SVC >",
							"S5ELEIX:SPX ELECTRONIC EQUP >",
							"S5ELEQ:SPX ELECTRICAL EQUP >",
							"S5ELUTX:SPX ELECTRIC UTL >",
							"S5ENRE:SPX ENERGY EQUP & SV >",
							"S5FDPR:SPX FOOD PROD IND >",
							"S5FDSRX:SPX FOOD & STAPLES RET >",
							"S5GASUX:SPX GAS UTL >",
							"S5HCEQ:SPX HC EQUP & SUP >",
							"S5HCPS:SPX HC PROVIDERS SVC >",
							"S5HCTEX:SPX HC TECHNOLOGY >",
							"S5HODU:SPX HOUSEHOLD DURABLES >",
							"S5HOPRX:SPX HOUSELHOLD PROD >",
							"S5HOTRX:SPX HOTELS REST & LEIS >",
							"S5INCR:SPX INTERNET CATALOG >",
							"S5INDCX:SPX INDUSTRIAL CONGL >",
							"S5INSSX:SPX INTERNET SOFTWARE >",
							"S5INSUX:SPX INSUURANCE IND >",
							"S5IPPEX:SPX INDEP PWR PROD >",
							"S5ITSV:SPX IT SERV IND >",
							"S5LEIS:SPX LEISURE EQUP >",
							"S5LSTSX:SPX LIFE SCI IND >",
							"S5MACH:SPX MACHINERY >",
							"S5MDREX:SPX RE MGM >",
							"S5MEDAX:SPX MEDIA >",
							"S5METL:SPX METAL & MIN >",
							"S5MRET:SPX MULTILINE RET >",
							"S5MUTIX:SPX MULTI UTL >",
							"S5OFFEX:SPX OFFICE ELECT >",
							"S5OILG:SPX OIL GAS FUEL >",
							"S5PAFO:SPX PAPER FORSET PROD >",
							"S5PERSX:SPX PERSONAL PROD >",
							"S5PHARX:SPX PHARMA >",
							"S5PRSV:SPX PROF SRVS >",
							"S5REITS:SPX RE INV TRUSTS >",
							"S5ROAD:SPX ROARD & RAIL >",
							"S5SOFT:SPX SOFTWARE >",
							"S5SPRE:SPX SPECIALTY RET >",
							"S5SSEQ:SPX SEMICOND & EQUP >",
							"S5TEXA:SPX TXTL & APPRL >",
							"S5THMFX:SPX THRIFTS & MORT >",
							"S5TOBAX:SPX TOBACCO >",
							"S5TRADX:SPX TRADING CO & DIS >",
							"S5WIREX:SPX WIRELESS TELECOM >"
						});

					else setNavigation(panel, mouseDownEvent, new string[] {
							"S5AEROX:SPX AERO & DEF",
							"S5AIRFX:SPX AIR FRT & LOG",
							"S5AIRLX:SPX AIRLINES",
							"S5AUTC:SPX AUTO COMP",
							"S5AUTO:SPX AUTOMOBILES",
							"S5BEVG:SPX BEVERAGES",
							"S5BIOTX:SPX BIOTECH",
							"S5BUILX:SPX BLDG PRODS",
							"S5CAPM:SPX CAPITAL MKTS",
							"S5CBNK:SPX COMM BANKS",
							"S5CFINX:SPX CONS FINANCE",
							"S5CHEM:SPX CHEMICALS",
							"S5CMPE:SPX COMPUTERS & PER",
							"S5COMM:SPX COMMUNICATION EQP",
							"S5COMSX:SPX COMMERCIAL SRBS",
							"S5CONP:SPX CONTAINER & PKG",
							"S5CSTEX:SPX CONST & ENG",
							"S5CSTMX:SPX CONST MATERIAL",
							"S5DCON:SPX DIVERSIFIED SRVC",
							"S5DISTX:SPX DISBRIBUTORS",
							"S5DIVT:SPX DIV TEL SVC",
							"S5DVFS:SPX DIV FIN SVC",
							"S5ELEIX:SPX ELECTRONIC EQUP",
							"S5ELEQ:SPX ELECTRICAL EQUP",
							"S5ELUTX:SPX ELECTRIC UTL",
							"S5ENRE:SPX ENERGY EQUP & SV",
							"S5FDPR:SPX FOOD PROD IND",
							"S5FDSRX:SPX FOOD & STAPLES RET",
							"S5GASUX:SPX GAS UTL",
							"S5HCEQ:SPX HC EQUP & SUP",
							"S5HCPS:SPX HC PROVIDERS SVC",
							"S5HCTEX:SPX HC TECHNOLOGY",
							"S5HODU:SPX HOUSEHOLD DURABLES",
							"S5HOPRX:SPX HOUSELHOLD PROD",
							"S5HOTRX:SPX HOTELS REST & LEIS",
							"S5INCR:SPX INTERNET CATALOG",
							"S5INDCX:SPX INDUSTRIAL CONGL",
							"S5INSSX:SPX INTERNET SOFTWARE",
							"S5INSUX:SPX INSUURANCE IND",
							"S5IPPEX:SPX INDEP PWR PROD",
							"S5ITSV:SPX IT SERV IND",
							"S5LEIS:SPX LEISURE EQUP",
							"S5LSTSX:SPX LIFE SCI IND",
							"S5MACH:SPX MACHINERY",
							"S5MDREX:SPX RE MGM",
							"S5MEDAX:SPX MEDIA",
							"S5METL:SPX METAL & MIN",
							"S5MRET:SPX MULTILINE RET",
							"S5MUTIX:SPX MULTI UTL",
							"S5OFFEX:SPX OFFICE ELECT",
							"S5OILG:SPX OIL GAS FUEL",
							"S5PAFO:SPX PAPER FORSET PROD",
							"S5PERSX:SPX PERSONAL PROD",
							"S5PHARX:SPX PHARMA",
							"S5PRSV:SPX PROF SRVS",
							"S5REITS:SPX RE INV TRUSTS",
							"S5ROAD:SPX ROARD & RAIL",
							"S5SOFT:SPX SOFTWARE",
							"S5SPRE:SPX SPECIALTY RET",
							"S5SSEQ:SPX SEMICOND & EQUP",
							"S5TEXA:SPX TXTL & APPRL",
							"S5THMFX:SPX THRIFTS & MORT",
							"S5TOBAX:SPX TOBACCO",
							"S5TRADX:SPX TRADING CO & DIS",
							"S5WIREX:SPX WIRELESS TELECOM"
						});
					ok = true;
				}
			}
			else if (country == "MID >")
			{
				if (groupSymbol == "MIDL3")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
							"S4AEROX:MID AERO & DEF >",
							"S4AIRFX:MID AIR FRT & LOG >",
							"S4AIRLX:MID AIRLINES >",
							"S4AUTC:MID AUTO COMP >",
							"S4AUTO:MID AUTOMOBILES >",
							"S4BEVG:MID BEVERAGES >",
							"S4BIOTX:MID BIOTECH >",
							"S4BUILX:MID BLDG PRODS >",
							"S4CAPM:MID CAPITAL MKTS >",
							"S4CBNK:MID COMM BANKS >",
							"S4CFINX:MID CONS FINANCE >",
							"S4CHEM:MID CHEMICALS >",
							"S4CMPE:MID COMPUTERS & PER >",
							"S4COMM:MID COMMUNICATION EQP >",
							"S4COMSX:MID COMMERCIAL SRBS >",
							"S4CONP:MID CONTAINER & PKG >",
							"S4CSTEX:MID CONST & ENG >",
							"S4CSTMX:MID CONST MATERIAL >",
							"S4DCON:MID DIVERSIFIED SRVC >",
							"S4DISTX:MID DISBRIBUTORS >",
							"S4DIVT:MID DIV TEL SVC >",
							"S4DVFS:MID DIV FIN SVC >",
							"S4ELEIX:MID ELECTRONIC EQUP >",
							"S4ELEQ:MID ELECTRICAL EQUP >",
							"S4ELUTX:MID ELECTRIC UTL >",
							"S4ENRE:MID ENERGY EQUP & SV >",
							"S4FDPR:MID FOOD PROD IND >",
							"S4FDSRX:MID FOOD & STAPLES RET >",
							"S4GASUX:MID GAS UTL >",
							"S4HCEQ:MID HC EQUP & SUP >",
							"S4HCPS:MID HC PROVIDERS SVC >",
							"S4HCTEX:MID HC TECHNOLOGY >",
							"S4HODU:MID HOUSEHOLD DURABLES >",
							"S4HOPRX:MID HOUSELHOLD PROD >",
							"S4HOTRX:MID HOTELS REST & LEIS >",
							"S4INCR:MID INTERNET CATALOG >",
							"S4INDCX:MID INDUSTRIAL CONGL >",
							"S4INSSX:MID INTERNET SOFTWARE >",
							"S4INSUX:MID INSUURANCE IND >",
							"S4ITSV:MID IT SERV IND >",
							"S4LEIS:MID LEISURE EQUP >",
							"S4LSTSX:MID LIFE SCI IND >",
							"S4MACH:MID MACHINERY >",
							"S4MARIX:MID MARINE >",
							"S4MDREX:MID RE MGM >",
							"S4MEDAX:MID MEDIA >",
							"S4METL:MID METAL & MIN >",
							"S4MRET:MID MULTILINE RET >",
							"S4MUTIX:MID MULTI UTL >",
							"S4OFFEX:MID OFFICE ELECT >",
							"S4OILG:MID OIL GAS FUEL >",
							"S4PAFO:MID PAPER FORSET PROD >",
							"S4PHARX:MID PHARMA >",
							"S4PRSV:MID PROF SRVS >",
							"S4REITS:MID RE INV TRUSTS >",
							"S4ROAD:MID ROARD & RAIL >",
							"S4SOFT:MID SOFTWARE >",
							"S4SPRE:MID SPECIALTY RET >",
							"S4SSEQ:MID SEMICOND & EQUP >",
							"S4TEXA:MID TXTL & APPRL >",
							"S4THMFX:MID THRIFTS & MORT >",
							"S4TOBAX:MID TOBACCO >",
							"S4TRADX:MID TRADING CO & DIS >",
							"S4WATUX:MID WATER UTL >",
							"S4WIREX:MID WIRELESS TELECOM >"
						 });

					else setNavigation(panel, mouseDownEvent, new string[] {
							"S4AEROX:MID AERO & DEF",
							"S4AIRFX:MID AIR FRT & LOG",
							"S4AIRLX:MID AIRLINES",
							"S4AUTC:MID AUTO COMP",
							"S4AUTO:MID AUTOMOBILES",
							"S4BEVG:MID BEVERAGES",
							"S4BIOTX:MID BIOTECH",
							"S4BUILX:MID BLDG PRODS",
							"S4CAPM:MID CAPITAL MKTS",
							"S4CBNK:MID COMM BANKS",
							"S4CFINX:MID CONS FINANCE",
							"S4CHEM:MID CHEMICALS",
							"S4CMPE:MID COMPUTERS & PER",
							"S4COMM:MID COMMUNICATION EQP",
							"S4COMSX:MID COMMERCIAL SRBS",
							"S4CONP:MID CONTAINER & PKG",
							"S4CSTEX:MID CONST & ENG",
							"S4CSTMX:MID CONST MATERIAL",
							"S4DCON:MID DIVERSIFIED SRVC",
							"S4DISTX:MID DISBRIBUTORS",
							"S4DIVT:MID DIV TEL SVC",
							"S4DVFS:MID DIV FIN SVC",
							"S4ELEIX:MID ELECTRONIC EQUP",
							"S4ELEQ:MID ELECTRICAL EQUP",
							"S4ELUTX:MID ELECTRIC UTL",
							"S4ENRE:MID ENERGY EQUP & SV",
							"S4FDPR:MID FOOD PROD IND",
							"S4FDSRX:MID FOOD & STAPLES RET",
							"S4GASUX:MID GAS UTL",
							"S4HCEQ:MID HC EQUP & SUP",
							"S4HCPS:MID HC PROVIDERS SVC",
							"S4HCTEX:MID HC TECHNOLOGY",
							"S4HODU:MID HOUSEHOLD DURABLES",
							"S4HOPRX:MID HOUSELHOLD PROD",
							"S4HOTRX:MID HOTELS REST & LEIS",
							"S4INCR:MID INTERNET CATALOG",
							"S4INDCX:MID INDUSTRIAL CONGL",
							"S4INSSX:MID INTERNET SOFTWARE",
							"S4INSUX:MID INSUURANCE IND",
							"S4ITSV:MID IT SERV IND",
							"S4LEIS:MID LEISURE EQUP",
							"S4LSTSX:MID LIFE SCI IND",
							"S4MACH:MID MACHINERY",
							"S4MARIX:MID MARINE",
							"S4MDREX:MID RE MGM",
							"S4MEDAX:MID MEDIA",
							"S4METL:MID METAL & MIN",
							"S4MRET:MID MULTILINE RET",
							"S4MUTIX:MID MULTI UTL",
							"S4OFFEX:MID OFFICE ELECT",
							"S4OILG:MID OIL GAS FUEL",
							"S4PAFO:MID PAPER FORSET PROD",
							"S4PHARX:MID PHARMA",
							"S4PRSV:MID PROF SRVS",
							"S4REITS:MID RE INV TRUSTS",
							"S4ROAD:MID ROARD & RAIL",
							"S4SOFT:MID SOFTWARE",
							"S4SPRE:MID SPECIALTY RET",
							"S4SSEQ:MID SEMICOND & EQUP",
							"S4TEXA:MID TXTL & APPRL",
							"S4THMFX:MID THRIFTS & MORT",
							"S4TOBAX:MID TOBACCO",
							"S4TRADX:MID TRADING CO & DIS",
							"S4WATUX:MID WATER UTL",
							"S4WIREX:MID WIRELESS TELECOM"

						});
					ok = true;
				}
			}

			else if (country == "SML >")
			{
				if (groupSymbol == "SMLL3")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
							"S6AEROX:SML AERO & DEF >",
							"S6AIRFX:SML AIR FRT & LOG >",
							"S6AIRLX:SML AIRLINES >",
							"S6AUTC:SML AUTO COMP >",
							"S6AUTO:SML AUTOMOBILES >",
							"S6BEVG:SML BEVERAGES >",
							"S6BIOTX:SML BIOTECH >",
							"S6BUILX:SML BLDG PRODS >",
							"S6CAPM:SML CAPITAL MKTS >",
							"S6CBNK:SML COMM BANKS >",
							"S6CFINX:SML CONS FINANCE >",
							"S6CHEM:SML CHEMICALS >",
							"S6CMPE:SML COMPUTERS & PER >",
							"S6COMM:SML COMMUNICATION EQP >",
							"S6COMSX:SML COMMERCIAL SRBS >",
							"S6CONP:SML CONTAINER & PKG >",
							"S6CSTEX:SML CONST & ENG >",
							"S6CSTMX:SML CONST MATERIAL >",
							"S6DCON:SML DIVERSIFIED SRVC >",
							"S6DISTX:SML DISBRIBUTORS >",
							"S6DIVT:SML DIV TEL SVC >",
							"S6DVFS:SML DIV FIN SVC >",
							"S6ELEIX:SML ELECTRONIC EQUP >",
							"S6ELEQ:SML ELECTRICAL EQUP >",
							"S6ELUTX:SML ELECTRIC UTL >",
							"S6ENRE:SML ENERGY EQUP & SV >",
							"S6FDPR:SML FOOD PROD IND >",
							"S6FDSRX:SML FOOD & STAPLES RET >",
							"S6GASUX:SML GAS UTL >",
							"S6HCEQ:SML HC EQUP & SUP >",
							"S6HCPS:SML HC PROVIDERS SVC >",
							"S6HCTEX:SML HC TECHNOLOGY >",
							"S6HODU:SML HOUSEHOLD DURABLES >",
							"S6HOPRX:SML HOUSELHOLD PROD >",
							"S6HOTRX:SML HOTELS REST & LEIS >",
							"S6INCR:SML INTERNET CATALOG >",
							"S6INDCX:SML INDUSTRIAL CONGL >",
							"S6INSSX:SML INTERNET SOFTWARE >",
							"S6INSUX:SML INSUURANCE IND >",
							"S6ITSV:SML IT SERV IND >",
							"S6LEIS:SML LEISURE EQUP >",
							"S6LSTSX:SML LIFE SCI IND >",
							"S6MACH:SML MACHINERY >",
							"S6MARIX:SML MARINE >",
							"S6MDREX:SML RE MGM >",
							"S6MEDAX:SML MEDIA >",
							"S6METL:SML METAL & MIN >",
							"S6MRET:SML MULTILINE RET >",
							"S6MUTIX:SML MULTI UTL >",
							"S6OILG:SML OIL GAS FUEL >",
							"S6PAFO:SML PAPER FORSET PROD >",
							"S6PERSX:SML PERSONAL PROD >",
							"S6PHARX:SML PHARMA >",
							"S6PRSV:SML PROF SRVS >",
							"S6REITS:SML RE INV TRUSTS >",
							"S6ROAD:SML ROARD & RAIL >",
							"S6SOFT:SML SOFTWARE >",
							"S6SPRE:SML SPECIALTY RET >",
							"S6SSEQ:SML SEMICOND & EQUP >",
							"S6TEXA:SML TXTL & APPRL >",
							"S6THMFX:SML THRIFTS & MORT >",
							"S6TOBAX:SML TOBACCO >",
							"S6TRADX:SML TRADING CO & DIS >",
							"S6WATUX:SML WATER UTL >",
							"S6WIREX:SML WIRELESS TELECOM >"
						 });

					else setNavigation(panel, mouseDownEvent, new string[] {
							"S6AEROX:SML AERO & DEF",
							"S6AIRFX:SML AIR FRT & LOG",
							"S6AIRLX:SML AIRLINES",
							"S6AUTC:SML AUTO COMP",
							"S6AUTO:SML AUTOMOBILES",
							"S6BEVG:SML BEVERAGES",
							"S6BIOTX:SML BIOTECH",
							"S6BUILX:SML BLDG PRODS",
							"S6CAPM:SML CAPITAL MKTS",
							"S6CBNK:SML COMM BANKS",
							"S6CFINX:SML CONS FINANCE",
							"S6CHEM:SML CHEMICALS",
							"S6CMPE:SML COMPUTERS & PER",
							"S6COMM:SML COMMUNICATION EQP",
							"S6COMSX:SML COMMERCIAL SRBS",
							"S6CONP:SML CONTAINER & PKG",
							"S6CSTEX:SML CONST & ENG",
							"S6CSTMX:SML CONST MATERIAL",
							"S6DCON:SML DIVERSIFIED SRVC",
							"S6DISTX:SML DISBRIBUTORS",
							"S6DIVT:SML DIV TEL SVC",
							"S6DVFS:SML DIV FIN SVC",
							"S6ELEIX:SML ELECTRONIC EQUP",
							"S6ELEQ:SML ELECTRICAL EQUP",
							"S6ELUTX:SML ELECTRIC UTL",
							"S6ENRE:SML ENERGY EQUP & SV",
							"S6FDPR:SML FOOD PROD IND",
							"S6FDSRX:SML FOOD & STAPLES RET",
							"S6GASUX:SML GAS UTL",
							"S6HCEQ:SML HC EQUP & SUP",
							"S6HCPS:SML HC PROVIDERS SVC",
							"S6HCTEX:SML HC TECHNOLOGY",
							"S6HODU:SML HOUSEHOLD DURABLES",
							"S6HOPRX:SML HOUSELHOLD PROD",
							"S6HOTRX:SML HOTELS REST & LEIS",
							"S6INCR:SML INTERNET CATALOG",
							"S6INDCX:SML INDUSTRIAL CONGL",
							"S6INSSX:SML INTERNET SOFTWARE",
							"S6INSUX:SML INSUURANCE IND",
							"S6ITSV:SML IT SERV IND",
							"S6LEIS:SML LEISURE EQUP",
							"S6LSTSX:SML LIFE SCI IND",
							"S6MACH:SML MACHINERY",
							"S6MARIX:SML MARINE",
							"S6MDREX:SML RE MGM",
							"S6MEDAX:SML MEDIA",
							"S6METL:SML METAL & MIN",
							"S6MRET:SML MULTILINE RET",
							"S6MUTIX:SML MULTI UTL",
							"S6OILG:SML OIL GAS FUEL",
							"S6PAFO:SML PAPER FORSET PROD",
							"S6PERSX:SML PERSONAL PROD",
							"S6PHARX:SML PHARMA",
							"S6PRSV:SML PROF SRVS",
							"S6REITS:SML RE INV TRUSTS",
							"S6ROAD:SML ROARD & RAIL",
							"S6SOFT:SML SOFTWARE",
							"S6SPRE:SML SPECIALTY RET",
							"S6SSEQ:SML SEMICOND & EQUP",
							"S6TEXA:SML TXTL & APPRL",
							"S6THMFX:SML THRIFTS & MORT",
							"S6TOBAX:SML TOBACCO",
							"S6TRADX:SML TRADING CO & DIS",
							"S6WATUX:SML WATER UTL",
							"S6WIREX:SML WIRELESS TELECOM"
						});
					ok = true;
				}
			}

			else if (country == "RAY >")
			{
				if (groupSymbol == "RAYL2")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
							"RGUSDAA:RAY AD AGENCIES >",
							"RGUSPAS:RAY AEROSPACE >",
							"RGUSSAF:RAY AG FISH & RNCH >",
							"RGUSPAI:RAY AIR TRANSPORT >",
							"RGUSEAE:RAY ALTER ENERGY >",
							"RGUSMAL:RAY ALUMINUM >",
							"RGUSFAM:RAY ASSET MGT & CUST >",
							"RGUSDAP:RAY AUTO PARTS >",
							"RGUSDAS:RAY AUTO SVC >",
							"RGUSDAU:RAY AUTOMOBILES >",
							"RGUSFBK:RAY BANKS DVSFD >",
							"RGUSFBS:RAY BEV BRW & DSTLR >",
							"RGUSSBD:RAY BEV SOFT DRNK >",
							"RGUSSSD:RAY SOFT DRNK >",
							"RGUSHBT:RAY BIOTEC >",
							"RGUSMCC:RAY BLDG CLIMATE CTRL >",
							"RGUSPBO:RAY BO SUP HR & CONS >",
							"RGUSMBM:RAY BUILDING MATL >",
							"RGUSDCT:RAY CABLE TV SVC >",
							"RGUSDCG:RAY CASINOS & GAMB >",
							"RGUSMCM:RAY CEMENT >",
							"RGUSMCS:RAY CHEM SPEC >",
							"RGUSMCD:RAY CHEM DVFSD >",
							"RGUSECO:RAY CMP SVC SFW & SYS >",
							"RGUSFFM:RAY COAL >",
							"RGUSPCS:RAY COMM SVC >",
							"RGUSPCL:RAY COMM FIN & MORT >",
							"RGUSTCM:RAY COMM SVC RN >",
							"RGUSPCV:RAY COMM TECH >",
							"RGUSTCS:RAY COMM VEH & PRTS >",
							"RGUSTCT:RAY COMPUTER TECH >",
							"RGUSPCN:RAY CONS >",
							"RGUSDCM:RAY CONS SVC  MISC >",
							"RGUSDCE:RAY CONSUMER LEND >",
							"RGUSFCL:RAY CONSUMER ELECT >",
							"RGUSMCP:RAY CONTAINER & PKG >",
							"RGUSMCR:RAY COPPER >",
							"RGUSDCS:RAY COSMETICS >",
							"RGUSDDM:RAY DRUG & GROC CHN >",
							"RGUSSDG:RAY DVSFD FNCL SVC >",
							"RGUSFDF:RAY DVSFD MEDIA >",
							"RGUSDDR:RAY DVSFD RETAIL >",
							"RGUSMDM:RAY DVSFD MAT & PROC >",
							"RGUSPDO:RAY DVSFD MFG OPS >",
							"RGUSDES:RAY EDUCATION SVC >",
							"RGUSTEE:RAY ELECT COMP >",
							"RGUSTEC:RAY ELECT ENT >",
							"RGUSTEL:RAY ELECTRONICS >",
							"RGUSEEQ:RAY ENERGY EQ >",
							"RGUSPEC:RAY ENG & CONTR SVC >",
							"RGUSDEN:RAY ENTERTAINMENT >",
							"RGUSPEN:RAY ENV MN & SEC SVC >",
							"RGUSMFT:RAY FERTILIZERS >",
							"RGUSFFD:RAY FINCL DATA & SYS >",
							"RGUSSFO:RAY FOODS >",
							"RGUSMFP:RAY FOREST PROD >",
							"RGUSPFB:RAY FRM & BLK PRNT SVC >",
							"RGUSSFG:RAY FRUIT & GRN PROC >",
							"RGUSDFU:RAY FUN PARLOR & CEM >",
							"RGUSEGP:RAY GAS PIPELINE >",
							"RGUSMGL:RAY GLASS >",
							"RGUSMGO:RAY GOLD >",
							"RGUSDHE:RAY HHLD EQP & PROD >",
							"RGUSDHF:RAY HHLD FURN >",
							"RGUSHHF:RAY HLTH CARE FAC >",
							"RGUSHHM:RAY HLTH CARE SVC >",
							"RGUSHHS:RAY HLTH C MGT SVC >",
							"RGUSHHC:RAY HLTH C MISC >",
							"RGUSDHB:RAY HOME BUILDING >",
							"RGUSDHO:RAY HOTEL/MOTEL >",
							"RGUSDHA:RAY HOUSEHOLD APPL >",
							"RGUSFIL:RAY INS LIFE >",
							"RGUSFIM:RAY INS MULTI-LN >",
							"RGUSFIP:RAY INS PROP-CAS >",
							"RGUSPIT:RAY INTL TRD & DV LG >",
							"RGUSDLT:RAY LEISURE TIME >",
							"RGUSPMG:RAY MACH & ENG >",
							"RGUSPMI:RAY MACH IND >",
							"RGUSPMT:RAY MACH TOOLS >",
							"RGUSPMA:RAY MACH AG >",
							"RGUSPME:RAY MACH SPECIAL >",
							"RGUSPMS:RAY MCH CONS & HNDL >",
							"RGUSHME:RAY MD & DN INS & SUP >",
							"RGUSHMS:RAY MED EQ >",
							"RGUSHMD:RAY MED SVC >",
							"RGUSMMD:RAY MET & MIN DVFSD >",
							"RGUSMMF:RAY METAL FABRIC >",
							"RGUSDMH:RAY MFG HOUSING >",
							"RGUSSMC:RAY MISC CONS STAPL >",
							"RGUSPOE:RAY OFF SUP & EQ >",
							"RGUSEOF:RAY OFFSHORE DRILL >",
							"RGUSEOI:RAY OIL INTERGATE >",
							"RGUSEOW:RAY OIL CRUDE PROD >",
							"RGUSEOC:RAY OIL REF & MKT >",
							"RGUSEOR:RAY OIL WELL EQ & SVC >",
							"RGUSMPC:RAY PAINT & COATING >",
							"RGUSMPA:RAY PAPER >",
							"RGUSSPC:RAY PERSONAL CARE >",
							"RGUSDPH:RAY PHOTOGRAPHY >",
							"RGUSHPH:RAY PHRM >",
							"RGUSMPL:RAY PLASTICS >",
							"RGUSPOPT:RAY PREC MET & MINL >",
							"RGUSMPM:RAY PRINT & COPY SVC >",
							"RGUSDPC:RAY PROD DUR MISC >",
							"RGUSPPD:RAY PRODUCT TECH EQ >",
							"RGUSTPR:RAY PUBLISHING >",
							"RGUSDPU:RAY PWR TRANSM EQ >",
							"RGUSDRB:RAY RADIO & TV BROAD >",
							"RGUSPRL:RAY RAILROAD EQ >",
							"RGUSPRA:RAY RAILROADS >",
							"RGUSFRE:RAY REAL ESTATE >",
							"RGUSFRI:RAY REIT >",
							"RGUSDRC:RAY RESTAURANTS >",
							"RGUSDRT:RAY ROOF WALL & PLUM >",
							"RGUSMRW:RAY RT & LS SVC CONS >",
							"RGUSDRV:RAY RV & BOATS >",
							"RGUSPSE:RAY SCI INS CTL & FLT >",
							"RGUSPSP:RAY SCI INS POL CTRL >",
							"RGUSPSI:RAY Sci INSTR ELEC >",
							"RGUSFSB:RAY SEC BRKG & SVC >",
							"RGUSTSC:RAY SE COND & COMP >",
							"RGUSPSH:RAY SHIPPING >",
							"RGUSDSR:RAY SPEC RET >",
							"RGUSMST:RAY STEEL >",
							"RGUSMSY:RAY SYN FIBR & CHEM >",
							"RGUSTTM:RAY TECHNOLOGY MISC >",
							"RGUSTTE:RAY TELEG EQ >",
							"RGUSDTX:RAY TEXT APP & SHORES >",
							"RGUSMTP:RAY TEXTILE PROD >",
							"RGUSSTO:RAY TOBACC0 >",
							"RGUSDTY:RAY TOYS >",
							"RGUSPTM:RAY TRANS MISC >",
							"RGUSPTK:RAY TRUCK MISC >",
							"RGUSUUE:RAY UTIL ELEC >",
							"RGUSUUG:RAY UTIL GAS DIST >",
							"RGUSUUT:RAY UTIL TELE >",
							"RGUSUUW:RAY UTIL WATER"

						 });

					else setNavigation(panel, mouseDownEvent, new string[] {
							"RGUSDAA:RAY AD AGENCIES",
							"RGUSPAS:RAY AEROSPACE",
							"RGUSSAF:RAY AG FISH & RNCH",
							"RGUSPAI:RAY AIR TRANSPORT",
							"RGUSEAE:RAY ALTER ENERGY",
							"RGUSMAL:RAY ALUMINUM",
							"RGUSFAM:RAY ASSET MGT & CUST",
							"RGUSDAP:RAY AUTO PARTS",
							"RGUSDAS:RAY AUTO SVC",
							"RGUSDAU:RAY AUTOMOBILES",
							"RGUSFBK:RAY BANKS DVSFD",
							"RGUSFBS:RAY BEV BRW & DSTLR",
							"RGUSSBD:RAY BEV SOFT DRNK",
							"RGUSSSD:RAY SOFT DRNK",
							"RGUSHBT:RAY BIOTEC",
							"RGUSMCC:RAY BLDG CLIMATE CTRL",
							"RGUSPBO:RAY BO SUP HR & CONS",
							"RGUSMBM:RAY BUILDING MATL",
							"RGUSDCT:RAY CABLE TV SVC",
							"RGUSDCG:RAY CASINOS & GAMB",
							"RGUSMCM:RAY CEMENT",
							"RGUSMCS:RAY CHEM SPEC",
							"RGUSMCD:RAY CHEM DVFSD",
							"RGUSECO:RAY CMP SVC SFW & SYS",
							"RGUSFFM:RAY COAL",
							"RGUSPCS:RAY COMM SVC",
							"RGUSPCL:RAY COMM FIN & MORT",
							"RGUSTCM:RAY COMM SVC RN",
							"RGUSPCV:RAY COMM TECH",
							"RGUSTCS:RAY COMM VEH & PRTS",
							"RGUSTCT:RAY COMPUTER TECH",
							"RGUSPCN:RAY CONS",
							"RGUSDCM:RAY CONS SVC  MISC",
							"RGUSDCE:RAY CONSUMER LEND",
							"RGUSFCL:RAY CONSUMER ELECT",
							"RGUSMCP:RAY CONTAINER & PKG",
							"RGUSMCR:RAY COPPER",
							"RGUSDCS:RAY COSMETICS",
							"RGUSDDM:RAY DRUG & GROC CHN",
							"RGUSSDG:RAY DVSFD FNCL SVC",
							"RGUSFDF:RAY DVSFD MEDIA",
							"RGUSDDR:RAY DVSFD RETAIL",
							"RGUSMDM:RAY DVSFD MAT & PROC",
							"RGUSPDO:RAY DVSFD MFG OPS",
							"RGUSDES:RAY EDUCATION SVC",
							"RGUSTEE:RAY ELECT COMP",
							"RGUSTEC:RAY ELECT ENT",
							"RGUSTEL:RAY ELECTRONICS",
							"RGUSEEQ:RAY ENERGY EQ",
							"RGUSPEC:RAY ENG & CONTR SVC",
							"RGUSDEN:RAY ENTERTAINMENT",
							"RGUSPEN:RAY ENV MN & SEC SVC",
							"RGUSMFT:RAY FERTILIZERS",
							"RGUSFFD:RAY FINCL DATA & SYS",
							"RGUSSFO:RAY FOODS",
							"RGUSMFP:RAY FOREST PROD",
							"RGUSPFB:RAY FRM & BLK PRNT SVC",
							"RGUSSFG:RAY FRUIT & GRN PROC",
							"RGUSDFU:RAY FUN PARLOR & CEM",
							"RGUSEGP:RAY GAS PIPELINE",
							"RGUSMGL:RAY GLASS",
							"RGUSMGO:RAY GOLD",
							"RGUSDHE:RAY HHLD EQP & PROD",
							"RGUSDHF:RAY HHLD FURN",
							"RGUSHHF:RAY HLTH CARE FAC",
							"RGUSHHM:RAY HLTH CARE SVC",
							"RGUSHHS:RAY HLTH C MGT SVC",
							"RGUSHHC:RAY HLTH C MISC",
							"RGUSDHB:RAY HOME BUILDING",
							"RGUSDHO:RAY HOTEL/MOTEL",
							"RGUSDHA:RAY HOUSEHOLD APPL",
							"RGUSFIL:RAY INS LIFE",
							"RGUSFIM:RAY INS MULTI-LN",
							"RGUSFIP:RAY INS PROP-CAS",
							"RGUSPIT:RAY INTL TRD & DV LG",
							"RGUSDLT:RAY LEISURE TIME",
							"RGUSPMG:RAY MACH & ENG",
							"RGUSPMI:RAY MACH IND",
							"RGUSPMT:RAY MACH TOOLS",
							"RGUSPMA:RAY MACH AG",
							"RGUSPME:RAY MACH SPECIAL",
							"RGUSPMS:RAY MCH CONS & HNDL",
							"RGUSHME:RAY MD & DN INS & SUP",
							"RGUSHMS:RAY MED EQ",
							"RGUSHMD:RAY MED SVC",
							"RGUSMMD:RAY MET & MIN DVFSD",
							"RGUSMMF:RAY METAL FABRIC",
							"RGUSDMH:RAY MFG HOUSING",
							"RGUSSMC:RAY MISC CONS STAPL",
							"RGUSPOE:RAY OFF SUP & EQ",
							"RGUSEOF:RAY OFFSHORE DRILL",
							"RGUSEOI:RAY OIL INTERGATE",
							"RGUSEOW:RAY OIL CRUDE PROD",
							"RGUSEOC:RAY OIL REF & MKT",
							"RGUSEOR:RAY OIL WELL EQ & SVC",
							"RGUSMPC:RAY PAINT & COATING",
							"RGUSMPA:RAY PAPER",
							"RGUSSPC:RAY PERSONAL CARE",
							"RGUSDPH:RAY PHOTOGRAPHY",
							"RGUSHPH:RAY PHRM",
							"RGUSMPL:RAY PLASTICS",
							"RGUSPOPT:RAY PREC MET & MINL",
							"RGUSMPM:RAY PRINT & COPY SVC",
							"RGUSDPC:RAY PROD DUR MISC",
							"RGUSPPD:RAY PRODUCT TECH EQ",
							"RGUSTPR:RAY PUBLISHING",
							"RGUSDPU:RAY PWR TRANSM EQ",
							"RGUSDRB:RAY RADIO & TV BROAD",
							"RGUSPRL:RAY RAILROAD EQ",
							"RGUSPRA:RAY RAILROADS",
							"RGUSFRE:RAY REAL ESTATE",
							"RGUSFRI:RAY REIT",
							"RGUSDRC:RAY RESTAURANTS",
							"RGUSDRT:RAY ROOF WALL & PLUM",
							"RGUSMRW:RAY RT & LS SVC CONS",
							"RGUSDRV:RAY RV & BOATS",
							"RGUSPSE:RAY SCI INS CTL & FLT",
							"RGUSPSP:RAY SCI INS POL CTRL",
							"RGUSPSI:RAY Sci INSTR ELEC",
							"RGUSFSB:RAY SEC BRKG & SVC",
							"RGUSTSC:RAY SE COND & COMP",
							"RGUSPSH:RAY SHIPPING",
							"RGUSDSR:RAY SPEC RET",
							"RGUSMST:RAY STEEL",
							"RGUSMSY:RAY SYN FIBR & CHEM",
							"RGUSTTM:RAY TECHNOLOGY MISC",
							"RGUSTTE:RAY TELEG EQ",
							"RGUSDTX:RAY TEXT APP & SHORES",
							"RGUSMTP:RAY TEXTILE PROD",
							"RGUSSTO:RAY TOBACC0",
							"RGUSDTY:RAY TOYS",
							"RGUSPTM:RAY TRANS MISC",
							"RGUSPTK:RAY TRUCK MISC",
							"RGUSUUE:RAY UTIL ELEC",
							"RGUSUUG:RAY UTIL GAS DIST",
							"RGUSUUT:RAY UTIL TELE",
							"RGUSUUW:RAY UTIL WATER"
						});

					ok = true;
				}
			}

			else if (country == "CANADA EQ >" || country == "CANADA >" || country == "Canada" || country == "SPTSX >")
			{
				if (groupSymbol == "SPTSX")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] { "SPTSX >", "STCOND >", "STCONS >", "STENRS >", "STFINL >", "STHLTH >", "STINDU >", "STINFT >", "STMATR >", "STTELS >", "STUTIL:UTIL >" });
					else setNavigation(panel, mouseDownEvent, new string[] { "SPTSX", "STCOND", "STCONS", "STENRS", "STFINL", "STHLTH", "STINDU", "STINFT", "STMATR", "STTELS", "STUTIL:UTIL" });
					ok = true;
				}
			}

			else if (country == "US EQ >" || country == "USA >" || country == "USA")
			{
				if (groupSymbol == "SPX")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
                        //"SPX:SPX INDEX >",
                        "SPXL1:SPX SECTORS >",
						"SPXL2:SPX INDUSTRIES >",
						"SPXL3:SPX SUB-INDUSTRIES >"
				});

					else setNavigation(panel, mouseDownEvent, new string[] {
                        //"SPX:SPX INDEX",
                        "SPXL1:SPX SECTORS >",
						"SPXL2:SPX INDUSTRIES >",
						"SPXL3:SPX SUB-INDUSTRIES >"
				});
					ok = true;
				}

				if (groupSymbol == "SPXL3")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"SPXL3:SPX SUB-INDUSTRIES >"
				});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"SPXL3:SPX SUB-INDUSTRIES >"
				});
					ok = true;
				}

				else if (groupSymbol == "SML")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
                        //"SML:SML INDEX >",
                        "SMLL1:SML SECTORS >",
						"SMLL2:SML INDUSTRIES >",
						"SMLL3:SML SUB-INDUSTRIES >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
                        //"SML:SML INDEX",
                        "SMLL1:SML SECTORS >",
						"SMLL2:SML INDUSTRIES >",
						"SMLL3:SML SUB-INDUSTRIES >"
					});
					ok = true;
				}

				else if (groupSymbol == "SPR")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"SPR:SPR INDEX >",
						"SPRL1:SPR SECTORS >",
						"SPRL2:SPR INDUSTRIES >",
						"SPRL3:SPR SUB-INDUSTRIES >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"SPR:SPR INDEX",
						"SPRL1:SPR SECTORS >",
						"SPRL2:SPR INDUSTRIES >",
						"SPRL3:SPR SUB-INDUSTRIES >"
					});
					ok = true;
				}

				else if (groupSymbol == "MID")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
                        //"MID:MID INDEX >",
                        "MIDL1:MID SECTORS >",
						"MIDL2:MID INDUSTRIES >",
						"MIDL3:MID SUB-INDUSTRIES >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
                        //"MID:MID INDEX",
                        "MIDL1:MID SECTORS >",
						"MIDL2:MID INDUSTRIES >",
						"MIDL3:MID SUB-INDUSTRIES >"
					});
					ok = true;
				}

				else if (groupSymbol == "NDX")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"NDX:NDX INDEX >",
						"NDXL1:NDX SECTORS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"NDX:NDX INDEX",
						"NDXL1:NDX SECTORS >"
					});
					ok = true;
				}

				else if (groupSymbol == "RIY")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"RIY:RIY INDEX >",
						"RIYL1:RIY SECTORS >",
						"RIYL2:RIY INDUSTRIES >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"RIY:RIY INDEX",
						"RIYL1:RIY SECTORS >",
						"RIYL2:RIY INDUSTRIES >"
					});
					ok = true;
				}
				else if (groupSymbol == "RLG")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"RLG:RLG INDEX >",
						"RLGL1:RLG SECTORS >",
						"RLGL2:RLG INDUSTRIES >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"RLG:RLG INDEX",
						"RLGL1:RLG SECTORS >",
						"RLGL2:RLG INDUSTRIES >"
					});
					ok = true;
				}
				else if (groupSymbol == "RLV")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"RLV:RLV INDEX >",
						"RLVL1:RLV SECTORS >",
						"RLVL2:RLV INDUSTRIES >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"RLV:RLV INDEX",
						"RLVL1:RLV SECTORS >",
						"RLVL2:RLV INDUSTRIES >"
					});
					ok = true;
				}

				else if (groupSymbol == "RTY")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"RTY:RTY INDEX >",
						"RTYL1:RTY SECTORS >",
						"RTYL2:RTY INDUSTRIES >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"RTY:RTY INDEX",
						"RTYL1:RTY SECTORS >",
						"RTYL2:RTY INDUSTRIES >"
					});
					ok = true;
				}

				else if (groupSymbol == "RUJ")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"RUJ:RUJ INDEX >",
						"RUJL1:RUJ SECTORS >",
						"RUJL2:RUJ INDUSTRIES >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"RUJ:RUJ INDEX",
						"RUJL1:RUJ SECTORS >",
						"RUJL2:RUJ INDUSTRIES >"
					});
					ok = true;
				}

				else if (groupSymbol == "RUO")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"RUO:RUO INDEX >",
						"RUOL1:RUO SECTORS >",
						"RUOL2:RUO INDUSTRIES >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"RUO:RUO INDEX",
						"RUOL1:RUO SECTORS >",
						"RUOL2:RUO INDUSTRIES >"
					});
					ok = true;
				}

				else if (groupSymbol == "RAY")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
                        //"RAY:RAY INDEX",
                        //"RAYL1:RAY SECTORS >",
                        //"RAYL2:RAY INDUSTRIES >"
                    });

					else setNavigation(panel, mouseDownEvent, new string[] {
                        //"RAY:RAY INDEX",
                        //"RAYL1:RAY SECTORS >",
                        //"RAYL2:RAY INDUSTRIES >"
                    });
					ok = true;
				}

				else if (groupSymbol == "RAG")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"RAG:RAG INDEX >",
						"RAGL1:RAG SECTORS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"RAG:RAG INDEX",
						"RAGL1:RAG SECTORS >"
					});
					ok = true;
				}

				else if (groupSymbol == "RAV")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"RAV:RAV INDEX >",
						"RAVL1:RAV SECTORS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"RAV:RAV INDEX",
						"RAVL1:RAV SECTORS >"
					});
					ok = true;
				}

				else if (groupSymbol == "RMC")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"RMC:RMC INDEX >",
						"RMCL1:RMC SECTORS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"RMC:RMC INDEX",
						"RMCL1:RMC SECTORS >"
					});
					ok = true;
				}

				else if (groupSymbol == "RDG")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"RDG:RDG INDEX >",
						"RDGL1:RDG SECTORS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"RDG:RDG INDEX",
						"RDGL1:RDG SECTORS >"
					});
					ok = true;
				}

				else if (groupSymbol == "RMV")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"RMV:RMV INDEX >",
						"RMVL1:RMV SECTORS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"RMV:RMV INDEX",
						"RMVL1:RMV SECTORS >"
					});
					ok = true;
				}

				else if (groupSymbol == "RMICRO")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"RMICRO:RMICRO INDEX >",
						"RMICROL1:RMICRO SECTORS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"RMICRO:RMICRO INDEX",
						"RMICROL1:RMICRO SECTORS >"
					});
					ok = true;
				}

				else if (groupSymbol == "RMICROG")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"RMICROG:RMICRO INDEX >",
						"RMICROGL1:RMICROG SECTORS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"RMICROG:RMICRO INDEX",
						"RMICROGL1:RMICROG SECTORS >"
					});
					ok = true;
				}

				else if (groupSymbol == "RMICROV")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"RMICROV:RMICROV INDEX >",
						"RMICROVL1:RMICROV SECTORS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"RMICROV:RMICROV INDEX",
						"RMICROVL1:RMICROV SECTORS >"
					});
					ok = true;
				}
			}

			else if (country == "WORLD >" || country == "WORLD")
			{
				if (groupSymbol == "BWORLD")
				{
					setNavigation(panel, mouseDownEvent, new string[]
					{
						"BWORLD:BWORLD INDEX >",
						"BWORLDL1:WORLD SECTORS >",
						"WORLD INDUSTRIES:WORLD INDUSTRIES >"  // World Industries not a symbol like the others
                    });
					ok = true;
				}
			}

			else if (country == "EQ | AMERICAS >" || country == "AMERICAS")
			{
				if (groupSymbol == "BWORLDUS")
				{
					setNavigation(panel, mouseDownEvent, new string[]
					{
						"BWORLDUS:BWORLDUS INDEX >",
						"AMERICAS INDUSTRIES:AMERICAS INDUSTRIES >"  // AMERICAS Industries not a symbol like the others
                    });
					ok = true;
				}
			}

			//            else if (country == "ACTIVE >")
			//{
			if (country == "ACTIVE WORLD EQ FUTURES >")
			{
				string[] items = new string[]
				{
					"ESA Index::SP 500 E Mini",
					"DMA Index::DOW Mini",
					"NQA Index::NASDAQ Mini",
					"FAA Index::SP 400 Mini",
					"PTA Index::TSX 60",
					"BZA Index::Bovespa",
					"ISA Index::Mexico Bolsa",
					"VGA Index::Euro Stoxx 50",
					"GXA Index::DAX",
					"Z A Index::FTSE 100",
					"CFA Index::CAC 40",
					"VEA Index::RTS",
					"NKA Index::NIKKEI 225",
					"TPA Index::TOPIX",
					"IFBA Index::CSI 300",
					"FFDA Index::CSI 500",
					"FFBA Index::Shanghai 50",
					"HIA Index::Hang Seng",
					"XPA Index::ASX 200",
					"KMA Index::KOSPI 200",
					"SDA Index::Straits Times",
					"IKA Index::FTSE KLCI",
					"NZA Index::Nifty 50",
					"BCA Index::SET 50",
					"KIWA Index::NZX 20",
				};
				setNavigation(panel, mouseDownEvent, items);
				ok = true;
			}

			else if (country == "WORLD BOND FUTURES >")
			{
				string[] items = new string[]
				{
						"WN1 Comdty::US 30 YR Ultra Bond",
						"US1 Comdty::US 30 YR",
						"UXY1 Comdty::US 10 YR Ultra Note",
						"TY1 Comdty::US 10 YR Note",
						"FV1 Comdty::US 5 YR",
						"3Y1 Comdty::US 3 YR",
						"TU1 Comdty::US 2 YR",
						"ED1 Comdty::Eurodollar",
						"CN1 Comdty::Canada 10 YR",
						"XQ1 Comdty::Canada 5 YR",
						"CV1 Comdty::Canada 2 YR",
						"VYB1 Comdty::Mexico 20 YR",
						"DW1 Comdty::Mexico 10 YR",
						"MA1 Comdty::Mexico Fixed",

						"G 1 Comdty::UK Long Gilt",
						"WB1 Comdty::UK Short Gilt",
						"OAT1 Comdty::France Gov Bond",
						"RX1 Comdty::Euro Bund",
						"UB1 Comdty::Euro Buxl",
						"OE1 Comdty::Euro Bobl",
						"DU1 Comdty::Euro Schatz",
						"BUO1 Comdty::Sweden 10 YR Bond",
						"BTO1 Comdty::Sweden 5 YR Bond",
						"BTL1 Comdty::Sweden 2 YR Bond",
						"KOA1 Comdty::Spain Long Bond",
						"FB1 Comdty::Swiss Bond",

						"TFT1 Comdty::China 10 YR",
						"TFC1 Comdty::China 5 YR",
						"TFS1 Comdty::China 2 YR",
						"JJA1 Comdty::Japan 20 YR",
						"JB1 Comdty::Japan 10 YR",
						"BJ1 Comdty::Japan 10 YR Mini",
						"KAA1 Comdty::Korea 10 YR",
						"KEZ1 Comdty::Korea 3 YR",
						"XM1 Comdty::Aust 10 YR",
						"VTA1 Comdty::Aust 5 YR",
						"YM1 Comdty::Aust 3 YR",
				};
				setNavigation(panel, mouseDownEvent, items);
				ok = true;
			}


			else if (country == "WORLD EQUITY FUTURES >")
			{
				string[] items = new string[]
				{
					"DM1 Index::US Dow Jones Mini",
					"ES1 Index::US SP 500 Mini",
					"NQ1 Index::US Nasdaq Mini",
					"FA1 Index::US SP 400 Mini",
					"RSY1 Index::US Russell 1000 Mini",
					"RTY1 Index::US Russell 2000 Mini",
					"SU1 Index::US SP Citi Value",
					"SG1 Index::US SP Citi Growth",
					"PT1 Index::CANADA TSX 60",
					"BZ1 Index::BRAZIL IBOVESPA",
					"IS1 Index::MEXICO IPC",
					"IPA1 Index::CHILE IPSA",
					"IPB1 Index::CHILE IPSA Mini",
					"VG1 Index::EUR STOXX 50",
					"VH1 Index::STOXX 50",
					"FI1 Index::FTSEUROFIRST 80",
					"EP1 Index::FTSEUROFIRST 100",
					"GX1 Index::GERMANY DAX",
					"DP1 Index::GERMANY TecDAX",
					"MF1 Index::GERMANY MDAX",
					"Z 1 Index::UK FTSE 100",
					"CFX1 Index::FRANCE CAC 40",
					"SW1 Index::ITALY MINI FTSE MIB",
					"ST1 Index::ITALY FTSE MIB",
					"EOX1 Index::NETHERLANDS AEX",
					"QCX1 Index::SWEDEN OMS STKH30",
					"SM1 Index::SWITZERLAND SWISS MKT",
					"IBX1 Index::SPAIN IBEX 35",
					"IDX1 Index::SPAIN IBEX MINI",
					"ATT1 Index::AUSTRA AUSTRIAN",
					"BEX1 Index::BELGIUM BEL20",
					"UO1 Index::HUNGARY BUX",
					"PP1 Index::PORTUGAL PSI 20",
					"VE1 Index::RUSSIA RTS",
					"KRS1 Index::POLAND WIG20",
					"OIX1 Index::NORWAY OBX",
					"OMOX1 Index::NORWAY OMX OSLO 20",
					"OMWX1 Index::DENMARK OMX COPEN 25",
					"OT1 Index::FINLAND OMXH25",
					"AI1 Index::SOUTH AFRICA FTSE JSE TOP 40",
					"KL1 Index::SOUTH AFRICA FTSE JSE 15",
					"AJX1 Index::GREECE FTSE ATHENS 20",
					"A51 Index::TURKEY BIST 30",
					"NK1 Index::JAPAN NIKKEI 225 OSE",
					"NI1 Index::JAPAN NIKKEI 225 SGX",
					"NX1 Index::JAPAN NIKKEI 225 CME",
					"TP1 Index::JAPAN TOPIX",
					"TZ1 Index::JAPAN TPX BANKS",
					"NH1 Index::JAPAN YEN NIKKEI",
					"MNDZ2 Index::JAPAN NIKKEI DIV",
					"NO1 Index::JAPAN NIKKEI 225",
					"JPW1 Index::JAPAN JPX NIKKEI 400",
					"MRO1 Index::JAPAN TSE MOTHERS",
					"IFB1 Index::CHINA CSI 300",
					"FFD1 Index::CHINA CSI 500",
					"XUX1 Index::CHINA FTSE CHINA A50",
					"CESX1 Index::CHINA CES CHINA 120",
					"FFBX1 Index::CHINA SHANGHAI 50 A",
					"XP1 Index::AUSTRALIA ASX 200",
					"VPA1 Index::AUSTRALIA ASX 200 REIT",
					"HIX1 Index::HONG KONG HANG SENG",
					"HCX1 Index::HONG KONG H SHARES",
					"KM1 Index::SOUTH KOREA KOSP12",
					"HJAX1 Index::TAIWAN MSCI TAIWAN",
					"FTX1 Index::TAIWAN TAIEX",
					"TEX1 Index::TAIWAN ELECTRONICS SEC",
					"TBX1 Index::TAIWAN BANK INSUR SEC",
					"QZX1 Index::SINGAPORE MSCI SING",
					"SDX1 Index::SINGAPORE STI INDEX",
					"IKX1 Index::MALAYSIS FTSE KLCI",
					"IHX1 Index::INDIA SGX NIFTY 50",
					"NZX1 Index::INDIA NIFTY 50",
					"AFX1 Index::INDIA NIFTY BANK",
					"BC1 Index::THAILAND SET50",
					"KIW1 Index::NEW ZEALAND NZX 20"
				};
				setNavigation(panel, mouseDownEvent, items);
				ok = true;
			}

			else if (country == "ACTIVE FX >")
			{
				string[] items = new string[]
				{
					"A1A Curncy::A1 ",
					"ADA Curncy::AD ",
					"AUAA Curncy::AUA ",
					"AUWA Curncy::AUW ",
					"B1A Curncy::B1 ",
					"BGAA Curncy::BGA ",
					"BGWA Curncy::BGW ",
					"BPA Curncy::BP ",
					"BYA Curncy::BY ",
					"BYWA Curncy::BYW ",
					"CDA Curncy::CD ",
					"DRAA Curncy::DRA ",
					"DRRA Curncy::DRR ",
					"DSA Curncy::DS ",
					"DUSA Curncy::DUS ",
					"ECA Curncy::EC ",
					"INBA Curncy::EUR INR",
					"INTA Curncy::USD INR",
					"JADA Curncy::JAD ",
					"JAWA Curncy::JAW ",
					"JYA Curncy::JY ",
					"KUA Curncy::KU ",
					"OCTA Curncy::OCT ",
					"PEA Curncy::PE ",
					"REDA Curncy::RED ",
					"RERA Curncy::RER ",
					"UAWA Curncy::EC ",
					"UEAA Curncy::UEA ",
					"UGA Curncy::UG ",
					"URA Curncy::UR ",
					"URAA Curncy::URA ",
					"VAWA Curncy::VAW ",
					"VDDA Curncy::VDD ",
					"VJWA Curncy::VJW ",
					"VTEA Curncy::VTE ",
					"VTWA Curncy::VTW ",
					"VXAA Curncy::VXA ",
					"VXCA Curncy::VXC ",
					"WABA Curncy::WAB ",
					"WAYA Curncy::WAY ",
					"WEYA Curncy::WEY ",
					"WJA Curncy::WJ",
					"WJBA Curncy::WJB ",
					"WJYA Curncy::WJY ",
					"WLBA Curncy::WLB ",
					"WLYA Curncy::WLY ",
					"WTA Curncy::WT",
					"XIDA Curncy::XID ",
					"XUCA Curncy::XUC ",
					"YJA Curncy::JY ",
					"YTA Curncy::EC ",
				};
				setNavigation(panel, mouseDownEvent, items);
				ok = true;
			}


			else if (country == "ACTIVE ENERGY OIL | GAS >")
			{
				string[] items = new string[]
				{
					"CLA Comdty::Crude",
					"XBA Comdty::Gasoline",
					"HOA Comdty::Heat Oil",
					"QSA Comdty::Gas Oil",
					"NGA Comdty::Natural Gas",
				};
				setNavigation(panel, mouseDownEvent, items);
				ok = true;
			}

			else if (country == "ACTIVE METALS >")
			{
				string[] items = new string[]
				{
					"GCA Comdty::Gold",
					"SIA Comdty::Silver",
					"PLA Comdty::Platinum",
					"PAA Comdty::Palladuim",
					"HGA Comdty::Copper",
				};
				setNavigation(panel, mouseDownEvent, items);
				ok = true;
			}

			else if (country == "ACTIVE GRAIN LIVESTOCK >")
			{
				string[] items = new string[]
				{
					"C A Comdty::Corn",
					"W A Comdty::Wheat",
					"S A Comdty::Soybeans",
					"BOA Comdty::Bean Oil",
					"SMA Comdty::Meal",
					"RRA Comdty::Rice",
					"LCA Comdty::Cattle",
					"FCA Comdty::Feeders",
					"LHA Comdty::Hogs",
				};
				setNavigation(panel, mouseDownEvent, items);
				ok = true;
			}

			else if (country == "ACTIVE SOFTS >")
			{
				string[] items = new string[]
				{
					"KCA Comdty::Coffee",
					"SBA Comdty::Sugar",
					"CTA Comdty::Cotton",
					"CCA Comdty::Cocoa",
					"JOA Comdty::OJ"
				};
				setNavigation(panel, mouseDownEvent, items);
				ok = true;
			}

			else if (country == "AGRICULTURE >")
			{
				if (groupSymbol == "MEATS >" || groupSymbol == "MEATS")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"CATTLE >",
					"PORK >",
					"POULTRY >",
					"SEAFOOD >",
					"SHEEP >",
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"CATTLE",
					"PORK",
					"POULTRY",
					"SEAFOOD",
					"SHEEP",
				});

					ok = true;
				}

				else if (groupSymbol == "FERTILIZER >" || groupSymbol == "FERTILIZER")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"UREA >",
						"AMMOFOS >",
						"AMMONIA >",
						"AMMO NITRATE >",
						"AMMO SULFATE >",
						"AQUA AMMONIA >",
						"CALCIUM AMMO NITRATE >",
						"DIAMMOFOS >",
						"MONO AMMO PHOSPHATE >",
						"PHOSPHORIC ROCK >",
						"PHOSPORIC ACID >",
						"POTASH >",
						"POTASSIUM CHLORIDE >",
						"POTASSIUM NITRATE >",
						"POTASSIUM SULFATE >",
						"SODA ASH >",
						"SODIUM MITRATE >",
						"SOP MAGNESIA >",
						"SULFUR >",
						"SULFURIC ACID >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
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
				});

					ok = true;
				}

				else if (groupSymbol == "FIBERS >" || groupSymbol == "FIBERS")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"COTTON >",
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
					"COTTON",
					});

					ok = true;
				}

				else if (groupSymbol == "FOODSTUFF >" || groupSymbol == "FOODSTUFF")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"DAIRY >",
					"EGGS >",
					"FOOD OIL >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
					"DAIRY",
					"EGGS",
					"FOOD OIL"
					});

					ok = true;
				}

				else if (groupSymbol == "FOREST PRODUCTS >" || groupSymbol == "FOREST PRODUCTS")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"LUMBER >",
					"PAPER >",
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
					"LUMBER",
					"PAPER"
					});

					ok = true;
				}

				else if (groupSymbol == "GRAINS >" || groupSymbol == "GRAINS")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"CORN >",
					"SOYBEANS >",
					"WHEAT >",
					"BARLEY >",
					"OATS >",
					"RICE >",
                    //"OILSEED >",
                    });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"CORN",
					"SOYBEANS",
					"WHEAT",
					"BARLEY",
					"OATS",
					"RICE",
                    //"OILSEED",
                    });

					ok = true;
				}

				else if (groupSymbol == "OLECHEMICALS >" || groupSymbol == "OLECHEMICALS")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"FATTY ACID >",
                    //"FATTY ALCOHOL >",
                    "GLYCERINE >",
					"TURPENTINE >",
					"VARNISH >",
                    //"VEGETABLE FAT >"
                    });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"FATTY ACID",
                    //"FATTY ALCOHOL",
                    "GLYCERINE",
					"TURPENTINE",
					"VARNISH",
                    //"VEGETABLE FAT"
                    });

					ok = true;
				}

				else if (groupSymbol == "SOFTS >" || groupSymbol == "SOFTS")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"COFFEE >",
					"SUGAR >",
					"COCOA >",
					"OJ >",
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
					"COFFEE",
					"SUGAR",
					"COCOA",
					"OJ",
					});

					ok = true;
				}
			}

			else if (country == "CQG AGRICULTURE >")
			{
				if (groupSymbol == "CQG MEATS >" || groupSymbol == "CQG MEATS")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"CME MEATS >",
						" ",
						"BMF MEATS >",
                        //"",
                        //"EUREX MEATS >",
                        //"",
                        //"DAILIAN MEATS >"
                 });

					else setNavigation(panel, mouseDownEvent, new string[] {
						"CME MEATS",
						" ",
						"BMF MEATS",
                        //"",
                        //"EUREX MEATS",
                        //"",
                        //"DAILIAN MEATS",
                });

					ok = true;
				}

				else if (groupSymbol == "CQG FERTILIZER >" || groupSymbol == "CQG FERTILIZER")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"CBOT FERTILIZER >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
						"CBOT FERTILIZER"
				});

					ok = true;
				}

				else if (groupSymbol == "CQG FIBERS >" || groupSymbol == "CQG FIBERS")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
                        //"DOW COTTON >",                 
                        //"ICE US COTTON >",
                        "NYMEX COTTON >",
                        //"",
                        //"BORSA ISTANBUL COTTON >",
                        //"",
                        //"ZHENGZHOU COTTON >",
                    });

					else setNavigation(panel, mouseDownEvent, new string[] {
                        //"DOW COTTON",
                        //"ICE US COTTON",
                        "NYMEX COTTON",
                        //"",
                        //"BORSA ISTANBUL COTTON",
                        //"",
                        //"ZHENGZHOU COTTON",
                    });

					ok = true;
				}

				else if (groupSymbol == "CQG FOODSTUFF >" || groupSymbol == "CQG FOODSTUFF")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"CME FOODSTUFF >",
                        //"IFM CME FOODSTUFF >",
                        //" ",
                        //"EEX AGRI FOODSTUFF >",
                        //" ",
                        //"SGX FOODSTUFF >",
                        //"SGX NZ FOODSTUFF >"
                    });

					else setNavigation(panel, mouseDownEvent, new string[] {
						"CME FOODSTUFF",
                        //"IFM CME FOODSTUFF",
                        //" ",
                        //"EEX AGRI FOODSTUFF",
                        //" ",
                        //"SGX FOODSTUFF",
                        //"SGX NZ FOODSTUFF"
                    });

					ok = true;
				}

				else if (groupSymbol == "CQG FOREST PRODUCTS >" || groupSymbol == "CQG FOREST PRODUCTS")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"CME LUMBER >",
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
					"CME LUMBER",
					});

					ok = true;
				}

				else if (groupSymbol == "CQG GRAINS >" || groupSymbol == "CQG GRAINS")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"CBOT GRAINS >",
                        //"ICE US CA GRAINS >",
                        //"KCBOT GRAINS >",          
                        //"MINN GRAINS >",
                        " ",
						"BMF GRAINS >",
                        //"DOW GRAINS >",
                        //"",
                        //"BORSA ISTANBUL GRAINS >",
                        //"EURONEXT GRAINS >",
                        //"JSE GRAINS >",
                        //"",
                        //"ASX24 GRAINS >",
                        //"DAILIAN GRAINS >",
                        //"OSAKA GRAINS >",
                        //"ZHENGZHOU GRAINS >",
                    });

					else setNavigation(panel, mouseDownEvent, new string[] {
						"CBOT GRAINS",
                        //"ICE US CA GRAINS",
                        //"KCBOT GRAINS",
                        //"MINN GRAINS",
                        " ",
						"BMF GRAINS",
                        //"DOW GRAINS",
                        //"",
                        //"BORSA ISTANBUL GRAINS",
                        //"EURONEXT GRAINS",
                        //"JSE GRAINS",
                        //"",
                        //"ASX24 GRAINS",
                        //"DAILIAN GRAINS",
                        //"OSAKA GRAINS",
                        //"ZHENGZHOU GRAINS",
                    });

					ok = true;
				}

				else if (groupSymbol == "CQG SOFTS >" || groupSymbol == "CQG SOFTS")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
                        //"ICE US SOFTS >",
                        "NYMEX SOFTS >",
						" ",
						"BMF SOFTS >"
                        //"DOW SOFTS >",
                        //"",
                        //"ICE EUR SOFTS >",
                        //"EURONEXT INDICES SOFTS >",
                        //"RTS SOFTS >",
                        //"",
                        //"ZHENGZHOU SOFTS >",
                    });

					else setNavigation(panel, mouseDownEvent, new string[] {
                        //"ICE US SOFTS",
                        "NYMEX SOFTS",
						" ",
						"BMF SOFTS"
                        //"DOW SOFTS",
                        //"",
                        //"ICE EUR SOFTS",
                        //"RTS SOFTS",
                        //"",
                        //"ZHENGZHOU SOFTS",
                    });

					ok = true;
				}
			}

			else if (country == "ENERGY >")
			{
				if (groupSymbol == "COKING COAL >" || groupSymbol == "COKING COAL")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"COKE >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
						"COKE"
				});

					ok = true;
				}

				else if (groupSymbol == "CRUDE >" || groupSymbol == "CRUDE")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"CRUDE OIL >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
						"CRUDE OIL"
				});

					ok = true;
				}

				else if (groupSymbol == "NATURAL GAS >" || groupSymbol == "NATURAL GAS")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
                        //"CNG >",
                        "LNG >",
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
                        //"CNG",
                        "LNG"
					});

					ok = true;
				}

				else if (groupSymbol == "PETROCHEMICAL >" || groupSymbol == "PETROCHEMICAL")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"2-ETHYLHEXANOL >",
						"ACETIC ACID >",
						"ACETONE >",
						"ACRYLIC ACID >",
						"ADIPIC ACID >",
						"ALPHA OLEFIN >",
						"AROMA LINEAR ALKYLBENZENE >",
						"AROMA MIXED XYLENES >",
						"AROMA TOLUENE >",
						"BASE OILS >",
						"CAPROLACTAM >",
						"CARBON BLACK >",
						"CYCLOHEXANONE >",
						"EDC >",
						"EPOXIDE RESINS >",
						"ETHYLENE GLYCOL >",
						"ETHYLENE OXIDE >",
						"HDPE >",
						"ISOPROPYL ALCOHOL >",
						"LDPE >",
						"MALEIC ANHYDRIDE >",
						"MDI >",
						"MEG >",
						"MONOMER CAN >",
						"MTBE >",
						"OCTANOL >",
						"OLEFINS BUTADIENE >",
						"OLEFINS ETHYLENE >",
						"OLEFINS PROPYLENE >",
						"PET >",
						"PHENOL >",
						"POLYAMIDE FIBER >",
						"POLYAMIDE RESIN >",
						"POLYESTER RESIN >",
						"POLYMER ABS >",
						"POLYMER EVA >",
						"POLYMER LLDPE >",
						"POLYMER POLYSTYRENE >",
						"PP >",
						"PROPYLENE GLYCOL >",
						"PROPYLENE OXIDE >",
						"PTA >",
						"PVC >",
						"STYRENE",
						"TDI",
						"VCM",
						"VINYL ACETATE MONOMER"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
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
					});

					ok = true;
				}

				else if (groupSymbol == "REFINED | HEAVY >" || groupSymbol == "REFINED | HEAVY")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"BITUMEN >",
					"FUEL OIL >",
					"MARINE DIESEL >",
					"MARINE GASOIL >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
					"BITUMEN",
					"FUEL OIL",
					"MARINE DIESEL",
					"MARINE GASOIL"
					});

					ok = true;
				}

				else if (groupSymbol == "REFINED | LIGHT >" || groupSymbol == "REFINED | LIGHT")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"CONDENSATE >",
					"GASOLINE GASOHOL >",
					"GASOLINE LEAD SUB >",
					"GASOLINE REFORMATE >",
					"NAPHTHA >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
					"CONDENSATE",
					"GASOLINE GASOHOL",
					"GASOLINE LEAD SUB",
					"GASOLINE REFORMATE",
					"NAPHTHA"
					});

					ok = true;
				}

				else if (groupSymbol == "REFINED | MIDDLE >" || groupSymbol == "REFINED | MIDDLE")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"DIESEL >",
					"GASOIL >",
					"JET FUEL >",
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
					"DIESEL",
					"GASOIL",
					"JET FUEL",
					});

					ok = true;
				}
			}

			else if (country == "CQG ENERGY >")
			{
				//if (groupSymbol == "CQG COKING COAL >" || groupSymbol == "CQG COKING COAL")
				//{
				//    if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
				//        "DCE COKE >"
				// });

				//    else setNavigation(panel, mouseDownEvent, new string[] {
				//        "DCE COKE"
				//});

				//    ok = true;
				//}

				if (groupSymbol == "CQG CRUDE >" || groupSymbol == "CQG CRUDE")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
                        //"CBOT CRUDE >",
                        //"COMEX CRUDE >",
                        //"DOW CRUDE >",
                        //"FAIRX CRUDE >",
                        "NYMEX CRUDE >",
                        //"SMALL CRUDE >",
                        //" ",
                        //"BMF CRUDE >",

                        //"BORSA ISTANBUL CRUDE >",
                        //"DUBAI GOLD CRUDE >",
                        //"DUBAI MERC CRUDE >",
                        //"EUREX CRUDE >",
                        //"ICE ABU DHABI CRUDE >",
                        //"ICE EUR CRUDE >",
                        //"JES CRUDE >",
                        //" ",
                        //"ASX 24 CRUDE >",
                        //"DAILIAN CRUDE >",
                        //"HONG KONG FUT CRUDE >",
                        //"ICE SINGAPORE CRUDE >",
                        //"SGX CRUDE >",
                        //"SHANGHAI FUT CRUDE >",
                        //"SHANGHAI INTL ENERGY CRUDE >",
                        //"TAIWAN FUT CRUDE >",
                        //"TOKYO CMDTY CRUDE >",
                        //"ZHENGZHOU CRUDE >"
                 });

					else setNavigation(panel, mouseDownEvent, new string[] {
                        //"CBOT CRUDE",
                        //"COMEX CRUDE",
                        //"DOW CRUDE",
                        //"FAIRX CRUDE",
                        "NYMEX CRUDE",
                        //"SMALL CRUDE",
                        //" ",
                        //"BMF CRUDE",
                        //"BORSA ISTANBUL CRUDE",
                        //"EUREX CRUDE",
                        //"DUBAI GOLD CRUDE",
                        //"DUBAI MERC CRUDE",
                        //"ICE ABU DHABI CRUDE",
                        //"ICE EUR CRUDE",
                        //"JES CRUDE",
                        //" ",
                        //"ASX 24 CRUDE",
                        //"DAILIAN CRUDE",
                        //"HONG KONG FUT CRUDE",
                        //"ICE SINGAPORE CRUDE",
                        //"SGX CRUDE",
                        //"SHANGHAI FUT CRUDE",
                        //"SHANGHAI INTL ENERGY CRUDE",
                        //"TAIWAN FUT CRUDE",
                        //"TOKYO CMDTY CRUDE",
                        //"ZHENGZHOU CRUDE"
                });

					ok = true;
				}

				else if (groupSymbol == "CQG NATURAL GAS >" || groupSymbol == "CQG NATURAL GAS")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"NYMEX METHANOL >",
						"NYMEX NATURAL GAS >",
						"NYMEX ETHYLENE >",
                        //"ICE EU CMDTY NAT GAS >",
                        //"ICE EUR NAT GAS >",
                        //"RTS NAT GAS >",
                        //" ",
                        //"ASX 24 NAT GAS >",
                        //"ASIA PACIFIC NAT GAS >"
                    });

					else setNavigation(panel, mouseDownEvent, new string[] {
						"NYMEX METHANOL",
						"NYMEX NATURAL GAS",
						"NYMEX ETHYLENE",
                        //" ",
                        //"ICE EU CMDTY NAT GAS",
                        //"ICE EUR NAT GAS",
                        //"RTS NAT GAS",
                        //" ",
                        //"ASX 24 NAT GAS",
                        //"ASIA PACIFIC NAT GAS"
                    });

					ok = true;
				}

				else if (groupSymbol == "CQG FUEL >" || groupSymbol == "CQG FUEL")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"NYMEX BUTANE >",
						"NYMEX COAL >",
						"NYMEX FUEL OIL >",
						"NYMEX GASOLINE >",
						"NYMEX PROPANE >",
                        //" ",
                        //"ICE EUR REFINED | HEAVY >",
                        //" ",
                        //"ICE SINGAPORE REFINED | HEAVY >",
                        //"SGX REFINED | HEAVY >",
                        //"SHANGHAI FUT REFINED | HEAVY >",
                    });

					else setNavigation(panel, mouseDownEvent, new string[] {
						"NYMEX BUTANE",
						"NYMEX COAL",
						"NYMEX FUEL OIL",
						"NYMEX GASOLINE",
						"NYMEX PROPANE",
                        //" ",
                        //"ICE EUR REFINED | HEAVY",
                        //" ",
                        //"ICE SINGAPORE REFINED | HEAVY",
                        //"SGX REFINED | HEAVY",
                        //"SHANGHAI FUT REFINED | HEAVY",
                    });

					ok = true;
				}

				else if (groupSymbol == "CQG REFINED | LIGHT >" || groupSymbol == "CQG REFINED | LIGHT")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"NYMEX REFINED | LIGHT >",
                        //"DOW REFINED | LIGHT >",
                        //" ",
                        //"ICE EUR REFINED | LIGHT >",
                        //" ",
                        //"TOKYO CMDTY REFINED | LIGHT >",
                        //"SGX REFINED | LIGHT >"
                    });

					else setNavigation(panel, mouseDownEvent, new string[] {
						"NYMEX REFINED | LIGHT",
                        //"DOW REFINED | LIGHT",
                        //" ",
                        //"ICE EUR REFINED | LIGHT",
                        //" ",
                        //"TOKYO CMDTY REFINED | LIGHT",
                        //"SGX REFINED | LIGHT"
                    });

					ok = true;
				}

				else if (groupSymbol == "CQG REFINED | MIDDLE >" || groupSymbol == "CQG REFINED | MIDDLE")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"NYMEX REFINED | MIDDLE >",
                        //" ",
                        //"ICE EUR REFINED | MIDDLE >",
                    });

					else setNavigation(panel, mouseDownEvent, new string[] {
						"NYMEX REFINED | MIDDLE",
                        //" ",
                        //"ICE EUR REFINED | MIDDLE",
                    });

					ok = true;
				}
			}

			else if (country == "ENVIRONMENT >")
			{
				if (groupSymbol == "EMISSIONS >" || groupSymbol == "EMISSIONS")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"EMISSIONS >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
						"EMISSIONS"
					});

					ok = true;
				}

				else if (groupSymbol == "RENEWABLES >" || groupSymbol == "RENEWABLES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"RENEWABLES >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
						"RENEWABLES"
					});

					ok = true;
				}

				else if (groupSymbol == "WEATHER >" || groupSymbol == "WEATHER")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"WEATHER >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"WEEATHER"
					});

					ok = true;
				}
			}

			else if (country == "CQG ENVIRONMENT >")
			{
				if (groupSymbol == "CQG EMISSIONS >" || groupSymbol == "CQG EMISSIONS")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
                        //"DOW EMISSIONS >",
                        "NYMEX EMISSIONS >",
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
                        //"DOW EMISSIONS",          
                        "NYMEX EMISSIONS",
					});
					ok = true;
				}

				else if (groupSymbol == "CQG CARBON >" || groupSymbol == "CQG CARB0N")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"DOW CARBON >",
						"ICE US CARBON >",
						"NYMEX-G CARBON >",
						" ",
						"EUREX CARBON >",
						"ICE ENDEX CARBON >",
						"ICE EUR CARBON >",
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
						"DOW CARBON",
						"ICE US CARBON",
						"NYMEX-G CARBON",
						" ",
						"EUREX CARBON",
						"ICE ENDEX CARBON",
						"ICE EUR CARBON",
					});
					ok = true;
				}
			}

			else if (country == "METALS >")
			{
				if (groupSymbol == "STEEL >" || groupSymbol == "STEEL")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"ANGLE >",
						"BAR >",
						"BILLET >",
						"CARBON STRUT >",
						"CAST IRON >",
						"CHANNEL >",
						"COKE >",
						"CR COIL >",
						"CR SHEET >",
						"GALVANISED >",
						"HR COIL >",
						"HR SHEET >",
						"HR STRIP >",
						"H BEAM >",
						"LINE PIPE >",
						"LONG PRODUCT >",
						"PIPE >",
						"PLATE >",
						"REBAR >",
						"SCRAP >",
						"STAINLESS BAR >",
						"STAINLESS CR >",
						"STAINLESS HR >",
						"STAINLESS SCRAP >",
						"STAINLESS SEAMLESS >",
						"STAINLESS SHEETS >",
						"STEEL PILING >",
						"WIRE DRAWN >",
						"WIRE ROD >",
						});

					else setNavigation(panel, mouseDownEvent, new string[] {
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
					});

					ok = true;
				}

				else if (groupSymbol == "BASE >" || groupSymbol == "BASE")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"ALUMINUM >",
						"COPPER >",
						"IRON >",
						"LEAD >",
						"NICKEL >",
						"TIN >",
						"URANIUM >",
						"ZINC >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"ALUMINUM",
						"COPPER",
						"IRON",
						"LEAD",
						"NICKEL",
						"TIN",
						"URANIUM",
						"ZINC"
					});
					ok = true;
				}

				else if (groupSymbol == "PRECIOUS >" || groupSymbol == "PRECIOUS")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"GOLD >",
						"SILVER >",
						"PALLADIUM >",
						"PLATINUM >",
						"IRIDIUM >",
						"RHODIUM >",
						"RUTHENIUM >",
						"RHENIUM >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"GOLD",
						"SILVER",
						"PALLADIUM",
						"PLATINUM",
						"IRIDIUM",
						"RHODIUM",
						"RUTHENIUM",
						"RHENIUM"
					});
					ok = true;
				}

				else if (groupSymbol == "FERRO ALLOYS >" || groupSymbol == "FERRO ALOOYS")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"CALCIUM SILICON >",
					"FERRO BORON >",
					"FERRO CHROME >",
					"FERRO DYSPROSIUM >",
					"FERRO GADOLINIUM >",
					"FERRO HOLMIUM >",
					"FERRO MANGANESE >",
					"FERRO MOLYBDENUM >",
					"FERRO NICKEL >",
					"FERRO NIOBIUM >",
					"FERRO PHOSPHORUS >",
					"FERRO SILICON >",
					"FERRO TITANIUM >",
					"FERRO TUNGSTEN >",
					"FERRO VANADIUM >",
					"SILICO MANGANESE >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {

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
					});
					ok = true;
				}

				else if (groupSymbol == "MINOR >" || groupSymbol == "MINOR")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"ANTIMONY >",
						"ARSENIC >",
						"BERYLLIUM >",
						"BISMUTH >",
						"CADMIUM >",
						"CALCIUM >",
						"CARBON >",
						"CHROMIUM >",
						"COBALT >",
						"GALLIUM >",
						"GERMANIUM >",
						"INDIUM >",
						"LITHIUM >",
						"MAGNESIUM >",
						"MANGANESE >",
						"MERCURY >",
						"MOLYBDENUM >",
						"NIOBIUM >",
						"POTASSIUM >",
						"SELENIUM >",
						"SILICON >",
						"SODIUM >",
						"STRONTIUM >",
						"TANTALUM >",
						"TELLURIUM >",
                        //"TITANIUM >",
                        "TUNGSTEN >",
						"VANADIUM >",
						"ZIRCONIUM >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
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
                        //"TITANIUM",
                        "TUNGSTEN",
						"VANADIUM",
						"ZIRCONIUM"
					});
					ok = true;
				}

				else if (groupSymbol == "COKING COAL >" || groupSymbol == "COKING COAL")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"COAL >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"COAL"
					});
					ok = true;
				}

				else if (groupSymbol == "MINERALS >" || groupSymbol == "MINERALS")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"FLUORSPAR >",
						"GODANTI >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"FLUORSPAR",
						"GODANTI"
					});
					ok = true;
				}

				else if (groupSymbol == "BRASS >" || groupSymbol == "BRASS")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"BRASS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"BRASS"
					});
					ok = true;
				}

				else if (groupSymbol == "RARE EARTHS >" || groupSymbol == "RARE EARTHS")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"DYSPROSIUM >",
						"ERBIUM >",
						"EUROPIUM >",
						"GADOLINIUM >",
						"HOLMIUM >",
						"LANTHANUM >",
						"LUTETIUM >",
						"MISCHMETAL >",
						"NEODYMIUM >",
						"PRASEODYMIUM >",
						"SAMARIUM >",
						"SCANDIUM >",
						"TERBIUM >",
						"YTTRIUM >",
						"YTTERBIUM >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
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
					});
					ok = true;
				}
			}

			else if (country == "CQG METALS >")
			{
				if (groupSymbol == "CQG STEEL >" || groupSymbol == "CQG STEEL")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"COMEX STEEL >",
                        //"DOW STEEL >",
                        //"NYMEX STEEL >",
                        //" ",
                        //"BORSA ISTANBUL STEEL >",
                        //"LME SELECT STEEL >",
                        //" ",
                        //"DAILIAN STEEL >",
                        //"FIN ENERGY STEEL >",
                        //"HONG KONG FUT STEEL >",
                        //"SGX STEEL >",
                        //"SHANGHAI FUT STEEL >",
                        });

					else setNavigation(panel, mouseDownEvent, new string[] {
						"COMEX STEEL",
                        //"DOW STEEL",
                        //"NYMEX STEEL",
                        //" ",
                        //"BORSA ISTANBUL STEEL",
                        //"LME SELECT STEEL",
                        //" ",
                        //"DAILIAN STEEL",
                        //"FIN ENERGY STEEL",
                        //"HONG KONG FUT STEEL",
                        //"SGX STEEL",
                        //"SHANGHAI FUT STEEL",                     
                    });

					ok = true;
				}

				else if (groupSymbol == "CQG BASE >" || groupSymbol == "CQG BASE")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"COMEX BASE >",
                        //"DOW BASE >",
                        //"NYMEX BASE >",
                        //" ",
                        //"EUREX BASE >",
                        //"LME SELECT BASE >",
                        //"RTS BASE >",
                        //" ",
                        //"DUBAI GOLD  BASE >",
                        //"JSE BASE >",
                        //" ",
                        //"BURSA MALAYSIA BASE >",
                        //"DAILIAN BASE >",
                        //"FIN ENERGY BASE >",
                        //"HONG KONG FUT BASE >",
                        //"SGX BASE >",
                        //"SHANGHAI FUT BASE >",                                           
                        //"SHANGHAI INTL ENERGY BASE >",                                           
                    });

					else setNavigation(panel, mouseDownEvent, new string[] {
						"COMEX BASE",
                        //"DOW BASE",
                        //"NYMEX BASE",
                        //" ",
                        //"EUREX BASE",
                        //"LME SELECT BASE",
                        //"RTS BASE",
                        //" ",
                        //"DUBAI GOLD  BASE",
                        //"JSE BASE",
                        //" ",
                        //"BURSA MALAYSIA BASE",
                        //"DAILIAN BASE",
                        //"FIN ENERGY BASE",
                        //"HONG KONG FUT BASE",
                        //"SGX BASE",
                        //"SHANGHAI FUT BASE",
                        //"SHANGHAI INTL ENERGY BASE",
                    });
					ok = true;
				}

				else if (groupSymbol == "CQG PRECIOUS >" || groupSymbol == "CQG PRECIOUS")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
                        //"CME PRECIOUS >",
                        "COMEX PRECIOUS >",
						"NYMEX PRECIOUS >",
						" ",
						"BMF PRECIOUS >",
                        //" ",                                             
                        //"BORSA ISTANBUL PRECIOUS >",
                        //"DUBAI GOLD PRECIOUS >",
                        //"EUREX PRECIOUS >",
                        //"ICE EUR FIN PRECIOUS >",
                        //"JSE PRECIOUS >",
                        //"LME SELECT PRECIOUS >",
                        //"RTS PRECIOUS >",
                        //" ",
                        //"ASIA PACIFIC PRECIOUS >",
                        //"BURSA MALAYSIA PRECIOUS >",
                        //"HONG KONG FUT PRECIOUS >",
                        //"OSAKA PRECIOUS >",
                        //"SHANGHAI FUT PRECIOUS >",
                        //"TAIWAN FUT PRECIOUS >"                    
                    });

					else setNavigation(panel, mouseDownEvent, new string[] {
                        //"CME PRECIOUS",
                        "COMEX PRECIOUS",
						"NYMEX PRECIOUS",
						" ",
						"BMF PRECIOUS",
                        //" ",
                        //"BORSA ISTANBUL PRECIOUS",
                        //"DUBAI GOLD PRECIOUS",
                        //"EUREX PRECIOUS",
                        //"ICE EUR FIN PRECIOUS",
                        //"JSE PRECIOUS",
                        //"LME SELECT PRECIOUS",
                        //"RTS PRECIOUS",
                        //" ",
                        //"ASIA PACIFIC PRECIOUS",
                        //"BURSA MALAYSIA PRECIOUS",
                        //"HONG KONG FUT PRECIOUS",
                        //"OSAKA PRECIOUS",
                        //"SHANGHAI FUT PRECIOUS",
                        //"TAIWAN FUT PRECIOUS"
                    });
					ok = true;
				}

				//else if (groupSymbol == "CQG FERRO ALLOYS >" || groupSymbol == "CQG FERRO ALOOYS")
				//{
				//    if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
				//        "COMEX FERRO ALLOYS >",
				//        //"ZHENGZHOU FERRO ALLOYS >"
				//    });

				//    else setNavigation(panel, mouseDownEvent, new string[] {
				//        "COMEX FERRO ALLOYS",
				//        //"ZHENGZHOU FERRO ALLOYS"
				//    });
				//    ok = true;
				//}
			}

			else if (country == "CQG SHIPPING >")
			{
				if (groupSymbol == "CQG FREIGHT >" || groupSymbol == "CQG FREIGHT")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"NYMEX FREIGHT >",                        
                        //"SGX FREIGHT >",
                 });

					else setNavigation(panel, mouseDownEvent, new string[] {
						"NYMEX FREIGHT",
                        //"SGX FREIGHT",
                    });

					ok = true;
				}
			}

			else if (country == "IND | COMMUNICATIONS >")
			{
				if (groupSymbol == "ADVERTISING | MARKETING >" || groupSymbol == "ADVERTISING | MARKETING")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"ADVERTISING COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"ADVERTISING COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "CABLE | SATELLITE >" || groupSymbol == "CABLE | SATELLITE")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"CABLE SATELLITE COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"CABLE SATELLITE COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "ENTERTAINMENT CONTENT >" || groupSymbol == "ENTERTAINMENT CONTENT")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"ENTERTAINMENT COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"ENTERTAINMENT COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "INTERNET MEDIA >" || groupSymbol == "INTERNET MEDIA")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"INTERNET COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"INTERNET COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "PUBLISHING | BROADCASTING >" || groupSymbol == "PUBLISHING | BROADCASTING")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"PUBLISHING COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"PUBLISHING COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "TELECOM CARRIERS >" || groupSymbol == "TELECOM CARRIERS")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"TELECOM COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"TELECOM COST ITEMS"
					});

					ok = true;
				}
			}
			else if (country == "IND | CONSUMER DISC >")
			{
				if (groupSymbol == "AUTO PARTS >" || groupSymbol == "AUTO PARTS")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"AUTO PARTS COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"AUTO PARTS COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "AUTOMOBILES >" || groupSymbol == "AUTOMOBILES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"AUTOMOBILES COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"AUTOMOBILES COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "HOMEBUILDERS >" || groupSymbol == "HOMEBUILDERS")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"HOUSING COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"HOUSING COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "LUXURY GOODS >" || groupSymbol == "LUXURY GOODS")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"LUXURY GOODS COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"LUXURY GOODS COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "SPECIALTY APPAREL >" || groupSymbol == "SPECIALTY APPAREL")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"SPECIALTY APPAREL COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"SPECIALTY APPAREL COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "CASINOS | GAMING >" || groupSymbol == "CASINOS | GAMING")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"CASINOS COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"CASINOS COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "LODGING >" || groupSymbol == "LODGING")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"LODGING COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"LODGING COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "RESTAURANTS >" || groupSymbol == "RESTAURANTS")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"RESTAURANTS COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"RESTAURANTS COST ITEMS"
					});

					ok = true;
				}
			}

			else if (country == "IND | CONSUMER STAPLES >")
			{
				if (groupSymbol == "AGRICULTURE >" || groupSymbol == "AGRICULTURE")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"AGRICULTURE COST ITEMS >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
						"AGRICULTURE COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "BEVERAGES >" || groupSymbol == "BEVERAGES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"BEVERAGES COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"BEVERAGES COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "HOUSEHOLD PRODUCTS >" || groupSymbol == "HOUSEHOLD PRODUCTS")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"HOUSEHOLD PRODUCTS COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"HOUSEHOLD PRODUCTS COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "PACKAGED FOOD >" || groupSymbol == "PACKAGED FOOD")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
                        //"PACKAGED FOODS COST ITEMS >",
                        "BEEF PRICES >",
						"CHICKEN PRICES >",
						"PORK PRICES >",
						"EGGS PRICES >",
						"DIARY PRICES >",
						"SEAFOOD PRICES >",
						"AGRICULTURE PRICES >",
						"PLASTICS PRICES >",
						"ENERGY FUEL PRICES >",
						"FREIGHT PRICES >",
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
                        //"PACKAGED FOODS COST ITEMS",                  
                        "BEEF PRICES",
						"CHICKEN PRICES",
						"PORK PRICES",
						"EGG PRICES",
						"DIARY PRICES",
						"SEAFOOD PRICES",
						"AGRICULTURE PRICES",
						"PLASTIC PRICES",
						"ENERGY FUEL PRICES",
						"FREIGHT PRICES",
					});

					ok = true;
				}
				else if (groupSymbol == "TOBACCO | CANNABIS >" || groupSymbol == "TOBACCO | CANNABIS")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"TOBACCO COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"TOBACCO COST ITEMS"
					});

					ok = true;
				}
			}

			else if (country == "IND | ENERGY >")
			{
				if (groupSymbol == "CRUDE OIL | NATURAL GAS >" || groupSymbol == "CRUDE OIL | NATURAL GAS")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"CRUDE OIL COST ITEMS >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
						"CRUDE OIL COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "CRUDE OIL PRODUCTION >" || groupSymbol == "CRUDE OIL PRODUCTION")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"CRUDE OIL PRODUCTION COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"CRUDE OIL PRODUCTION COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "INTEGRATED OILS >" || groupSymbol == "INTEGRATED OILS")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"INTEGRATED OILS COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"INTEGRATED OILS COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "LIQUEFIED NATURAL GAS >" || groupSymbol == "LIQUEFIED NATURAL GAS")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"LIQUEFIED NATURAL GAS COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"LIQUEFIED NATURAL GAS COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "MIDSTREAM OIL | GAS >" || groupSymbol == "MIDSTREAM OIL | GAS")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"MIDSTREAM OIL COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"MIDSTREAM OIL COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "OIL | GAS SERVICES >" || groupSymbol == "OIL | GAS SERVICES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"OIL GAS SERVICES COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"OIL GAS SERVICES COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "REFINING | MARKETING >" || groupSymbol == "REFINING | MARKETING")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"REFINING MARKETING COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"REFINING MARKETING COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "SOLAR ENERGY EQUIPMENT >" || groupSymbol == "SOLAR ENERGY EQUIPMENT")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"SOLAR ENERGY COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"SOLAR ENERGY COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "WIND ENERGY EQUIPMENT >" || groupSymbol == "WIND ENERGY EQUIPMENT")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"WIND ENERGY COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"WIND ENERGY COST ITEMS"
					});

					ok = true;
				}
			}

			else if (country == "IND | FINANCIALS >")
			{
				if (groupSymbol == "INVESTMENT MGMT >" || groupSymbol == "INVESTMENT MGMT")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"INVESTMENT MGMT COST ITEMS >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
						"INVESTMENT MGMT COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "BANKING >" || groupSymbol == "BANKING")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"BANKING COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"BANKING COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "FINTECH >" || groupSymbol == "FINTECH")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"FINTECH COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"FINTECH COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "INVESTMENT BANKING >" || groupSymbol == "INVESTMENT BANKING")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"INVESTMENT BANKING COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"INVESTMENT BANKING COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "INSURANCE >" || groupSymbol == "INSURANCE")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"INSURANCE COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"INSURANCE COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "LIFE INSURANCE >" || groupSymbol == "LIFE INSURANCE")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"LIFE INSURANCE COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"LIFE INSURANCE COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "P | C INSURANCE >" || groupSymbol == "P | C INSURANCE")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"P | C INSURANCE COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"P | C INSURANCE COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "CONSUMER FINANCE >" || groupSymbol == "CONSUMER FINANCE")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"CONSUMER FINANCE COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"CONSUMER FINANCE COST ITEMS"
					});

					ok = true;
				}
			}

			else if (country == "IND | HEALTH CARE >")
			{
				if (groupSymbol == "BIOTECH >" || groupSymbol == "BIOTECH")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"BIOTECH COST ITEMS >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
						"BIOTECH COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "LARGE PHARMA >" || groupSymbol == "LARGE PHARMA")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"LARGE PHARMA COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"LARGE PHARMA COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "HEALTH CARE SUPPLY CHAIN >" || groupSymbol == "HEALTH CARE SUPPLY CHAIN")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"HEALTHCARE SUPPLY CHAIN COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"HEALTHCARE SUPPLY CHAIN COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "MANAGED CARE >" || groupSymbol == "MANAGED CARE")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"MANAGED CARE COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"MANAGED CARE COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "LIFE SCIENCE EQUIP >" || groupSymbol == "LIFE SCIENCE EQUIP")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"LIFE SCIENCE EQUIP COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"LIFE SCIENCE EQUIP COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "MEDICAL EQUIP >" || groupSymbol == "MEDICAL EQUIP")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"MEDICAL EQUIP COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"MEDICAL EQUIP COST ITEMS"
					});

					ok = true;
				}
			}

			else if (country == "IND | INDUSTRIALS >")
			{
				if (groupSymbol == "AEROSPACE | DEFENSE >" || groupSymbol == "AEROSPACE | DEFENSE")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"AEROSPACE DEFENSE COST ITEMS >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
						"AEROSPACE DEFENSE COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "CONSTRUCTION >" || groupSymbol == "CONSTRUCTION")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"CONSTRUCTION COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"CONSTRUCTION COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "ELECTRICAL EQUIPMENT >" || groupSymbol == "ELECTRICAL EQUIPMENT")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"ELECTRICAL EQUIPMENT COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"ELECTRICAL EQUIPMENT COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "MACHINERY >" || groupSymbol == "MACHINERY")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"MACHINERY COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"MACHINERY COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "AIRLINES >" || groupSymbol == "AIRLINES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"AIRLINES COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"AIRLINES COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "LOGISTICS SERVICES >" || groupSymbol == "LOGISTICS SERVICES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"LOGISTICS SERVICES COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"LOGISTICS SERVICES COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "MARINE SHIPPING >" || groupSymbol == "MARINE SHIPPING")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"MARINE SHIPPING COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"MARINE SHIPPING COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "RAIL FREIGHT >" || groupSymbol == "RAIL FREIGHT")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"RAIL FREIGHT COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"RAIL FREIGHT COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "TRUCKING >" || groupSymbol == "TRUCKING")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"TRUCKING COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"TRUCKING COST ITEMS"
					});

					ok = true;
				}
			}

			else if (country == "IND | MATERIALS >")
			{
				if (groupSymbol == "AGRICULTURAL CHEMICALS >" || groupSymbol == "AGRICULTURAL CHEMICALS")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"AGRICULTURAL CHEMICALS COST ITEMS >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
						"AGRICULTURAL CHEMICALS COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "BASIC | DIVERSIFIED CHEMICALS >" || groupSymbol == "BASIC | DIVERSIFIED CHEMICALS")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"DIVERSIFIED CHEMICALS COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"DIVERSIFIED CHEMICALS COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "SPECIALTY CHEMICALS >" || groupSymbol == "SPECIALTY CHEMICALS")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"SPECIALTY CHEMICALS COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"SPECIALTY CHEMICALS COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "BUILDING MATERIALS >" || groupSymbol == "BUILDING MATERIALS")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"BUILDING MATERIALS COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"BUILDING MATERIALS COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "PAPER PACKAGING FOREST >" || groupSymbol == "PAPER PACKAGING FOREST")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"PAPER PACKAGING FOREST COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"PAPER PACKAGING FOREST COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "STEEL PRODUCERS >" || groupSymbol == "STEEL PRODUCERS")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"STEEL PRODUCERS COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"STEEL PRODUCERS COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "ALUMINUM >" || groupSymbol == "ALUMINUM")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"ALUMINUM COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"ALUMINUM COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "BASE METALS >" || groupSymbol == "BASE METALS")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"BASE METALS COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"BASE METALS COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "COAL >" || groupSymbol == "COAL")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"COAL COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"COAL COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "COPPER >" || groupSymbol == "COPPER")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"COPPER COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"COPPER COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "IRON >" || groupSymbol == "IRON")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"IRON COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"IRON COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "PRECIOUS METAL MINING >" || groupSymbol == "PRECIOUS METAL MINING")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"PRECIOUS METAL MINING COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"PRECIOUS METAL MINING COST ITEMS"
					});

					ok = true;
				}
			}


			else if (country == "IND | REAL ESTATE >")
			{
				if (groupSymbol == "HEALTH CARE REIT >" || groupSymbol == "HEALTH CARE REIT")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"HEALTHCARE REIT COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"HEALTHCARE REIT COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "OFFICE REIT >" || groupSymbol == "OFFICE REIT")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"OFFICE REIT COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"OFFICE REIT COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "RETAIL REIT >" || groupSymbol == "RETAIL REIT")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"RETAIL REIT COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"RETAIL REIT COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "RESIDENTIAL REIT >" || groupSymbol == "RESIDENTIAL REIT")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"RESIDENTIAL REIT COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"RESIDENTIAL REIT COST ITEMS"
					});

					ok = true;
				}
			}

			else if (country == "IND | TECHNOLOGY >")
			{
				if (groupSymbol == "COMPUTER HARDWARE >" || groupSymbol == "COMPUTER HARDWARE")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"COMPUTER HARDWARE COST ITEMS >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
						"COMPUTER HARDWARE COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "CONSUMER ELECTRONICS >" || groupSymbol == "CONSUMER ELECTRONICS")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"CONSUMER ELECTRONICS COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"CONSUMER ELECTRONICS COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "DATA NETWORKING EQUIP >" || groupSymbol == "DATA NETWORKING EQUIP")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"DATA NETWORKING EQUIP COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"DATA NETWORKING EQUIP COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "SEMICONDUCTOR DEVICES >" || groupSymbol == "SEMICONDUCTOR DEVICES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"SEMICONDUCTOR DEVICES COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"SEMICONDUCTOR DEVICES COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "SEMICONDUCTOR MFG >" || groupSymbol == "SEMICONDUCTOR MFG")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"SEMICONDUCTOR MFG COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"SEMICONDUCTOR MFG COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "APPLICATION SOFTWARE >" || groupSymbol == "APPLICATION SOFTWARE")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"APPLICATION SOFTWARE COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"APPLICATION SOFTWARE COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "INFRASTRUCTURE SOFTWARE >" || groupSymbol == "INFRASTRUCTURE SOFTWARE")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"INFRASTRUCTURE SOFTWARE COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"INFRASTRUCTURE SOFTWARE COST ITEMS"
					});

					ok = true;
				}
				else if (groupSymbol == "IT SERVICES >" || groupSymbol == "IT SERVICES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"IT SERVICES COST ITEMS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"IT SERVICES COST ITEMS"
					});

					ok = true;
				}
			}

			else if (country == "IND | UTILITIES >")
			{
				if (groupSymbol == "ELECTRIC UTILITIES >" || groupSymbol == "ELECTRIC UTILITIES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"ELECTRIC UTILITIES COST ITEMS >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
						"ELECTRIC UTILITIES COST ITEMS"
					});

					ok = true;
				}
			}


			else if (country == "CQG COMMODITIES >")
			{
				if (groupSymbol == "US COMMODITIES >" || groupSymbol == "US COMMODITIES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					 "CBOT >",
					 " ",
					 "CME >",
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"CBOT",
					" ",
					"CME",
				});

					ok = true;
				}

				else if (groupSymbol == "CANADA RATES >" || groupSymbol == "CANADA RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"CANADA GENERIC >",
					"CANADA CURVES >",
					"CANADA BUTTERFLIES >",
					"CANADA BREAKEVEN >",
					"CANADA CDS >",
					"CANADA SPREADS >",
					"CANADA SWAPS >",
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
					"CANADA GENERIC",
					"CANADA CURVES",
					"CANADA BUTTERFLIES",
					"CANADA BREAKEVEN",
					"CANADA CDS",
					"CANADA SPREADS",
					"CANADA SWAPS",
					});

					ok = true;
				}
				else if (groupSymbol == "MEXICO RATES >" || groupSymbol == "MEXICO RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"MEXICO GENERIC >",
					"MEXICO BREAKEVEN >",
					"MEXICO CDS >",
					"MEXICO TIIE SWAPS >",
					"MEXICO TIIE LIBOR SWAPS >",
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
					"MEXICO GENERIC",
					"MEXICO BREAKEVEN",
					"MEXICO CDS",
					"MEXICO TIIE SWAPS",
					"MEXICO TIIE LIBOR SWAPS",
					});

					ok = true;
				}
			}

			else if (country == "N AMERICA RATES >")
			{
				if (groupSymbol == "US RATES >" || groupSymbol == "US RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					 "US GENERIC >",
					 "US CURVES >",
					 "US BUTTERFLIES >",
					 "US BREAKEVEN >",
					 "US CDS >",
					 "US SPREADS >",
					 "US SWAPS >",
					 " ",
					"US MORTGAGE RATES >",
					" ",
					"AAA MUNI RATES >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"US GENERIC",
					"US CURVES",
					"US BUTTERFLIES",
					"US BREAKEVEN",
					"US CDS",
					"US SPREADS",
					"US SWAPS",
					 " ",
					"US MORTGAGE RATES",
					" ",
					"AAA MUNI RATES"
				});

					ok = true;
				}

				else if (groupSymbol == "CANADA RATES >" || groupSymbol == "CANADA RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"CANADA GENERIC >",
					"CANADA CURVES >",
					"CANADA BUTTERFLIES >",
					"CANADA BREAKEVEN >",
					"CANADA CDS >",
					"CANADA SPREADS >",
					"CANADA SWAPS >",
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
					"CANADA GENERIC",
					"CANADA CURVES",
					"CANADA BUTTERFLIES",
					"CANADA BREAKEVEN",
					"CANADA CDS",
					"CANADA SPREADS",
					"CANADA SWAPS",
					});

					ok = true;
				}
				else if (groupSymbol == "MEXICO RATES >" || groupSymbol == "MEXICO RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"MEXICO GENERIC >",
					"MEXICO BREAKEVEN >",
					"MEXICO CDS >",
					"MEXICO TIIE SWAPS >",
					"MEXICO TIIE LIBOR SWAPS >",
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
					"MEXICO GENERIC",
					"MEXICO BREAKEVEN",
					"MEXICO CDS",
					"MEXICO TIIE SWAPS",
					"MEXICO TIIE LIBOR SWAPS",
					});

					ok = true;
				}
			}


			else if (country == "CQG N AMERICA RATES >")
			{
				if (groupSymbol == "CQG US RATES >" || groupSymbol == "CQG US RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					 "CBOT US RATES >",
					 "CME US RATES >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"CBOT US RATES",
					"CME US RATES",
				});

					ok = true;
				}

				//else if (groupSymbol == "CANADA RATES >" || groupSymbol == "CANADA RATES")
				//{
				//    if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
				//    "CANADA GENERIC >",
				//    "CANADA CURVES >",
				//    "CANADA BUTTERFLIES >",
				//    "CANADA BREAKEVEN >",
				//    "CANADA CDS >",
				//    "CANADA SPREADS >",
				//    "CANADA SWAPS >",
				//    });

				//    else setNavigation(panel, mouseDownEvent, new string[] {
				//    "CANADA GENERIC",
				//    "CANADA CURVES",
				//    "CANADA BUTTERFLIES",
				//    "CANADA BREAKEVEN",
				//    "CANADA CDS",
				//    "CANADA SPREADS",
				//    "CANADA SWAPS",
				//    });

				//    ok = true;
				//}
				//else if (groupSymbol == "MEXICO RATES >" || groupSymbol == "MEXICO RATES")
				//{
				//    if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
				//    "MEXICO GENERIC >",
				//    "MEXICO BREAKEVEN >",
				//    "MEXICO CDS >",
				//    "MEXICO TIIE SWAPS >",
				//    "MEXICO TIIE LIBOR SWAPS >",
				//    });

				//    else setNavigation(panel, mouseDownEvent, new string[] {
				//    "MEXICO GENERIC",
				//    "MEXICO BREAKEVEN",
				//    "MEXICO CDS",
				//    "MEXICO TIIE SWAPS",
				//    "MEXICO TIIE LIBOR SWAPS",
				//    });

				//    ok = true;
				//}
			}

			else if (country == "S AMERICA RATES >")
			{
				if (groupSymbol == "BRAZIL RATES >" || groupSymbol == "BRAZIL RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"BRAZI GENERIC >",
					"BRAZIL BREAKEVEN >",
					"BRAZIL CDS >",
					"BRAZIL BMF >",
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"BRAZI GENERIC",
					"BRAZIL BREAKEVEN",
					"BRAZIL CDS",
					"BRAZIL BMF",
				});

					ok = true;
				}

				else if (groupSymbol == "CHILE RATES >" || groupSymbol == "CHILE RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"CHILE GENERIC >",
					"CHILE CDS >",
					"CHILE SWAPS >",
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"CHILE GENERIC",
					"CHILE CDS",
					"CHILE SWAPS",
				});

					ok = true;
				}

				else if (groupSymbol == "COLUMBIA RATES >" || groupSymbol == "COLUMBIA RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"COLUMBIA GENERIC >",
					"COLUMBIA CDS >",
					"COLUMBIA SWAPS >",
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"COLUMBIA GENERIC",
					"COLUMBIA CDS",
					"COLUMBIA SWAPS",
				});

					ok = true;
				}

				else if (groupSymbol == "PERU RATES >" || groupSymbol == "PERU RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"PERU GENERIC >",
					"PERU CDS >",
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"PERU GENERIC",
					"PERU CDS",
				});

					ok = true;
				}
			}

			else if (country == "EUROPE RATES >")
			{
				if (groupSymbol == "EUROZONE RATES >" || groupSymbol == "EUROZONE RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"EUROZONE GENERIC >",
					"EUROZONE CDS >",
					"EUROZONE SPREADS >",
					"EUROZONE ANNUAL SWAPS >",
					"EUROZONE SWAPS 3M EURIBOR >",
					"EUROZONE SWAPS EONIA >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"EUROZONE GENERIC",
					"EUROZONE CDS",
					"EUROZONE SPREADS",
					"EUROZONE ANNUAL SWAPS",
					"EUROZONE SWAPS 3M EURIBOR",
					"EUROZONE SWAPS EONIA"
				});

					ok = true;
				}

				else if (groupSymbol == "FRANCE RATES >" || groupSymbol == "FRANCE RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"FRANCE GENERIC >",
					"FRANCE CURVES >",
					"FRANCE BUTTERFLIES >",
					"FRANCE BREAKEVEN >",
					"FRANCE CDS >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"FRANCE GENERIC",
					"FRANCE CURVES",
					"FRANCE BUTTERFLIES",
					"FRANCE BREAKEVEN",
					"FRANCE CDS"
				});

					ok = true;
				}

				else if (groupSymbol == "GERMANY RATES >" || groupSymbol == "GERMANY RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"GERMANY GENERIC >",
					"GERMANY CURVES >",
					"GERMANY BUTTERFLIES >",
					"GERMANY BREAKEVEN >",
					"GERMANY CDS >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"GERMANY GENERIC",
					"GERMANY CURVES",
					"GERMANY BUTTERFLIES",
					"GERMANY BREAKEVEN",
					"GERMANY CDS"
				});

					ok = true;
				}

				else if (groupSymbol == "ITALY RATES >" || groupSymbol == "ITALY  RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"ITALY GENERIC >",
					"ITALY CURVES >",
					"ITALY BUTTERFLIES >",
					"ITALY BREAKEVEN >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"ITALY GENERIC",
					"ITALY CURVES",
					"ITALY BUTTERFLIES",
					"ITALY BREAKEVEN"
				});

					ok = true;
				}

				else if (groupSymbol == "ENGLAND RATES >" || groupSymbol == "ENGLAND  RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"ENGLAND GENERIC >",
					"ENGLAND CURVES >",
					"ENGLAND BUTTERFLIES >",
					"ENGLAND BREAKEVEN >",
					"ENGLAND CDS >",
					"ENGLAND SPREADS >",
					"ENGLAND SWAPS >",
					"ENGLAND SONIA OIS >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"ENGLAND GENERIC",
					"ENGLAND CURVES",
					"ENGLAND BUTTERFLIES",
					"ENGLAND BREAKEVEN",
					"ENGLAND CDS",
					"ENGLAND SPREADS",
					"ENGLAND SWAPS",
					"ENGLAND SONIA OIS"
				});

					ok = true;
				}

				else if (groupSymbol == "AUSTRIA RATES >" || groupSymbol == "AUSTRIA  RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"AUSTRIA GENERIC >",
					"AUSTRIA CURVES >",
					"AUSTRIA BUTTERFLIES >",
					"AUSTRIA CDS >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"AUSTRIA GENERIC",
					"AUSTRIA CURVES",
					"AUSTRIA BUTTERFLIES",
					"AUSTRIA CDS"
				});

					ok = true;
				}

				else if (groupSymbol == "BELGIUM RATES >" || groupSymbol == "BELGIUM  RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"BELGIUM GENERIC >",
					"BELGIUM CURVES >",
					"BELGIUM BUTTERFLIES >",
					"BELGIUM CDS >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"BELGIUM GENERIC",
					"BELGIUM CURVES",
					"BELGIUM BUTTERFLIES",
					"BELGIUM CDS"
				});

					ok = true;
				}

				else if (groupSymbol == "CZECH REP RATES >" || groupSymbol == "CZECH REP  RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"CZECH REP GENERIC >",
					"CZECH REP CDS >",
					"CZECH REP SWAPS >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"CZECH REP GENERIC",
					"CZECH REP CDS",
					"CZECH REP SWAPS"
				});

					ok = true;
				}

				else if (groupSymbol == "DENMARK RATES >" || groupSymbol == "DENMARK  RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"DENMARK GENERIC >",
					"DENMARK CURVES >",
					"DENMARK BUTTERFLIES >",
					"DENMARK BREAKEVEN >",
					"DENMARK CDS >",
					"DENMARK SWAPS >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"DENMARK GENERIC",
					"DENMARK CURVES",
					"DENMARK BUTTERFLIES",
					"DENMARK BREAKEVEN",
					"DENMARK CDS",
					"DENMARK SWAPS"
				});

					ok = true;
				}

				else if (groupSymbol == "FINLAND RATES >" || groupSymbol == "FINLAND  RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"FINLAND GENERIC >",
					"FINLAND CURVES >",
					"FINLAND BUTTERFLIES >",
					"FINLAND CDS >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"FINLAND GENERIC",
					"FINLAND CURVES",
					"FINLAND BUTTERFLIES",
					"FINLAND CDS"
				});

					ok = true;
				}

				else if (groupSymbol == "GREECE RATES >" || groupSymbol == "GREECE  RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"GREECE GENERIC >",
					"GREECE CURVES >",
					"GREECE BUTTERFLIES >",
					"GREECE CDS >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"GREECE GENERIC",
					"GREECE CURVES",
					"GREECE BUTTERFLIES",
					"GREECE CDS"
				});

					ok = true;
				}

				else if (groupSymbol == "HUNGARY RATES >" || groupSymbol == "HUNGARY  RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"HUNGARY GENERIC >",
					"HUNGARY CDS >",
					"HUNGARY SWAPS >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"HUNGARY GENERIC",
					"HUNGARY CDS",
					"HUNGARY SWAPS"
				});

					ok = true;
				}

				else if (groupSymbol == "IRELAND RATES >" || groupSymbol == "IRELAND  RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"IRELAND GENERIC >",
					"IRELAND CURVES >",
					"IRELAND BUTTERFLIES >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"IRELAND GENERIC",
					"IRELAND CURVES",
					"IRELAND BUTTERFLIES"
				});

					ok = true;
				}

				else if (groupSymbol == "NETHERLANDS RATES >" || groupSymbol == "NETHERLANDS  RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"NETHERLANDS GENERIC >",
					"NETHERLANDS CURVES >",
					"NETHERLANDS BUTTERFLIES >",
					"NETHERLANDS CDS >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"NETHERLANDS GENERIC",
					"NETHERLANDS CURVES",
					"NETHERLANDS BUTTERFLIES",
					"NETHERLANDS CDS"
				});

					ok = true;
				}

				else if (groupSymbol == "NORWAY RATES >" || groupSymbol == "NORWAY  RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"NORWAY GENERIC >",
					"NORWAY CURVES >",
					"NORWAY BUTTERFLIES >",
					"NORWAY CDS >",
					"NORWAY SWAPS >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"NORWAY GENERIC",
					"NORWAY CURVES",
					"NORWAY BUTTERFLIES",
					"NORWAY CDS",
					"NORWAY SWAPS"
				});

					ok = true;
				}

				else if (groupSymbol == "POLAND RATES >" || groupSymbol == "POLAND  RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"POLAND GENERIC >",
					"POLAND BREAKEVEN >",
					"POLAND CDS >",
					"POLAND SPREADS >",
					"POLAND SWAPS >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"POLAND GENERIC",
					"POLAND BREAKEVEN",
					"POLAND CDS",
					"POLAND SPREADS",
					"POLAND SWAPS"
				});

					ok = true;
				}

				else if (groupSymbol == "PORTUGAL RATES >" || groupSymbol == "PORTUGAL  RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"PORTUGAL GENERIC >",
					"PORTUGAL CURVES >",
					"PORTUGAL BUTTERFLIES >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"PORTUGAL GENERIC",
					"PORTUGAL CURVES",
					"PORTUGAL BUTTERFLIES"
				});

					ok = true;
				}

				else if (groupSymbol == "ROMANIA RATES >" || groupSymbol == "ROMANIA  RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"ROMANIA GENERIC >",
					"ROMANIA BREAKEVEN >",
					"ROMANIA CDS >",
					"ROMANIA SWAPS >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"ROMANIA GENERIC",
					"ROMANIA BREAKEVEN",
					"ROMANIA CDS",
					"ROMANIA SWAPS"
				});

					ok = true;
				}

				else if (groupSymbol == "RUSSIA RATES >" || groupSymbol == "RUSSIA  RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"RUSSIA GENERIC >",
					"RUSSIA BREAKEVEN >",
					"RUSSIA CDS >",
					"RUSSIA SWAPS >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"RUSSIA GENERIC",
					"RUSSIA BREAKEVEN",
					"RUSSIA CDS",
					"RUSSIA SWAPS"
				});

					ok = true;
				}

				else if (groupSymbol == "SLOVAKIA RATES >" || groupSymbol == "SLOVAKIA  RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"SLOVAKIA GENERIC >",
					"SLOVAKIA BREAKEVEN >",
					"SLOVAKIA CDS >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"SLOVAKIA GENERIC",
					"SLOVAKIA BREAKEVEN",
					"SLOVAKIA CDS"
				});

					ok = true;
				}

				else if (groupSymbol == "SPAIN RATES >" || groupSymbol == "SPAIN  RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"SPAIN GENERIC >",
					"SPAIN CURVES >",
					"SPAIN BUTTERFLIES >",
					"SPAIN BREAKEVEN >",
					"SPAIN CDS >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"SPAIN GENERIC",
					"SPAIN CURVES",
					"SPAIN BUTTERFLIES",
					"SPAIN BREAKEVEN",
					"SPAIN CDS"
				});

					ok = true;
				}

				else if (groupSymbol == "SWEDEN RATES >" || groupSymbol == "SWEDEN  RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"SWEDEN GENERIC >",
					"SWEDEN CURVES >",
					"SWEDEN BUTTERFLIES >",
					"SWEDEN BREAKEVEN >",
					"SWEDEN CDS >",
					"SWEDEN SWAPS >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"SWEDEN GENERIC",
					"SWEDEN CURVES",
					"SWEDEN BUTTERFLIES",
					"SWEDEN BREAKEVEN",
					"SWEDEN CDS",
					"SWEDEN SWAPS"
				});

					ok = true;
				}

				else if (groupSymbol == "SWITZERLAND RATES >" || groupSymbol == "SWITZERLAND  RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"SWITZERLAND GENERIC >",
					"SWITZERLAND CURVES >",
					"SWITZERLAND BUTTERFLIES >",
					"SWITZERLAND SWAPS >",
					"SWITZERLAND SPREADS >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"SWITZERLAND GENERIC",
					"SWITZERLAND CURVES",
					"SWITZERLAND BUTTERFLIES",
					"SWITZERLAND SWAPS",
					"SWITZERLAND SPREADS"
				});

					ok = true;
				}

				else if (groupSymbol == "TURKEY RATES >" || groupSymbol == "TURKEY  RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"TURKEY GENERIC >",
					"TURKEY BREAKEVEN >",
					"TURKEY CDS >",
					"TURKEY SWAPS >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"TURKEY GENERIC",
					"TURKEY BREAKEVEN",
					"TURKEY CDS",
					"TURKEY SWAPS"
				});

					ok = true;
				}
			}

			else if (country == "ASIA RATES >")
			{
				if (groupSymbol == "CHINA RATES >" || groupSymbol == "CHINA RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"CHINA GENERIC >",
					"CHINA BREAKEVEN >",
					"CHINA CDS >",
					"CHINA ONSHORE 7D >",
					"CHINA ONSHORE 3M >",
					"CHINA USDCNY Non Del >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"CHINA GENERIC",
					"CHINA BREAKEVEN",
					"CHINA CDS",
					"CHINA ONSHORE 7D",
					"CHINA ONSHORE 3M",
					"CHINA USDCNY Non Del"
				});

					ok = true;
				}

				else if (groupSymbol == "HONG KONG RATES >" || groupSymbol == "HONG KONG RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"HONG KONG GENERIC >",
					"HONG KONG CURVES >",
					"HONG KONG BUTTERFLIES >",
					"HONG KONG CDS >",
					"HONG KONG SPREADS >",
					"HONG KONG SWAPS >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"HONG KONG GENERIC",
					"HONG KONG CURVES",
					"HONG KONG BUTTERFLIES",
					"HONG KONG CDS",
					"HONG KONG SPREADS",
					"HONG KONG SWAPS"
				});

					ok = true;
				}

				else if (groupSymbol == "INDIA RATES >" || groupSymbol == "INDIA RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"INDIA GENERIC >",
					"INDIA CDS >",
					"INDIA SWAPS >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"INDIA GENERIC",
					"INDIA CDS",
					"INDIA SWAPS"
				});

					ok = true;
				}

				else if (groupSymbol == "INDONESIA RATES >" || groupSymbol == "INDONESIA RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"INDONESIA GENERIC >",
					"INDONESIA CDS >",
					"INDONESIA SWAPS 3M >",
					"INDONESIA OFFSHORE NDS >",
					"INDONESIA IONA >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"INDONESIA GENERIC",
					"INDONESIA CDS",
					"INDONESIA SWAPS 3M",
					"INDONESIA OFFSHORE NDS",
					"INDONESIA IONA"
				});

					ok = true;
				}

				else if (groupSymbol == "JAPAN RATES >" || groupSymbol == "JAPAN RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"JAPAN GENERIC >",
					"JAPAN CURVES >",
					"JAPAN BUTTERFLIES >",
					"JAPAN BREAKEVEN >",
					"JAPAN CDS >",
					"JAPAN SPREADS >",
					"JAPAN SWAPS >",
					"JAPAN SWAPS TOKYO CL >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"JAPAN GENERIC",
					"JAPAN CURVES",
					"JAPAN BUTTERFLIES",
					"JAPAN BREAKEVEN",
					"JAPAN CDS",
					"JAPAN SPREADS",
					"JAPAN SWAPS",
					"JAPAN SWAPS TOKYO CL"
				});

					ok = true;
				}

				else if (groupSymbol == "MALAYSIA RATES >" || groupSymbol == "MALAYSIA RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"MALAYSIA GENERIC >",
					"MALAYSIA CDS >",
					"MALAYSIA SWAPS >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"MALAYSIA GENERIC",
					"MALAYSIA CDS",
					"MALAYSIA SWAPS"
				});

					ok = true;
				}

				else if (groupSymbol == "PAKISTAN RATES >" || groupSymbol == "PAKISTAN RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"PAKISTAN GENERIC >",
					"PAKISTAN CDS >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"PAKISTAN GENERIC",
					"PAKISTAN CDS"
				});

					ok = true;
				}

				else if (groupSymbol == "PHILIPPINES RATES >" || groupSymbol == "PHILIPPINES RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"PHILIPPINES GENERIC >",
					"PHILIPPINES CDS >",
					"PHILIPPINES SWAPS >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"PHILIPPINES GENERIC",
					"PHILIPPINES CDS",
					"PHILIPPINES SWAPS"
				});

					ok = true;
				}

				else if (groupSymbol == "SINGAPORE RATES >" || groupSymbol == "SINGAPORE RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"SINGAPORE GENERIC >",
					"SINGAPORE CURVES >",
					"SINGAPORE BUTTERFLIES >",
					"SINGAPORE SWAPS >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"SINGAPORE GENERIC",
					"SINGAPORE CURVES",
					"SINGAPORE BUTTERFLIES",
					"SINGAPORE SWAPS"
				});

					ok = true;
				}

				else if (groupSymbol == "SOUTH KOREA RATES >" || groupSymbol == "SOUTH KOREA RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"SOUTH KOREA GENERIC >",
					"SOUTH KOREA CDS >",
					"SOUTH KOREA OFFSHORE KRW USD >",
					"SOUTH KOREA ONSHORE KRW USD >",
					"SOUTH KOREA ONSHORE KRW KRW >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"SOUTH KOREA GENERIC",
					"SOUTH KOREA CDS",
					"SOUTH KOREA OFFSHORE KRW USD",
					"SOUTH KOREA ONSHORE KRW USD",
					"SOUTH KOREA ONSHORE KRW KRW"
				});

					ok = true;
				}

				else if (groupSymbol == "TAIWAN RATES >" || groupSymbol == "TAIWAN RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"TAIWAN GENERIC >",
					"TAIWAN ONSHORE TWD TWD >",
					"TAIWAN ONSHORE TWD USD >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"TAIWAN GENERIC",
					"TAIWAN ONSHORE TWD TWD",
					"TAIWAN ONSHORE TWD USD"
				});

					ok = true;
				}

				else if (groupSymbol == "THAILAND RATES >" || groupSymbol == "THAILAND RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"THAILAND GENERIC >",
					"THAILAND CDS >",
					"THAILAND ONSHORE THB THB >",
					"THAILAND ONSHORE THB USD >",
					"THAILAND OFFSHORE THB USD >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"THAILAND GENERIC",
					"THAILAND CDS",
					"THAILAND ONSHORE THB THB",
					"THAILAND ONSHORE THB USD",
					"THAILAND OFFSHORE THB USD"
				});

					ok = true;
				}
			}

			else if (country == "OCEANIA RATES >")
			{
				if (groupSymbol == "AUSTRALIA RATES >" || groupSymbol == "AUSTRALIA RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"AUSTRALIA GENERIC >",
					"AUSTRALIA CURVES >",
					"AUSTRALIA BUTTERFLIES >",
					"AUSTRALIA BREAKEVEN >",
					"AUSTRALIA CDS >",
					"AUSTRALIA SPREADS >",
					"AUSTRALIA SWAPS >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"AUSTRALIA GENERIC",
					"AUSTRALIA CURVES",
					"AUSTRALIA BUTTERFLIES",
					"AUSTRALIA BREAKEVEN",
					"AUSTRALIA CDS",
					"AUSTRALIA SPREADS",
					"AUSTRALIA SWAPS"
				});

					ok = true;
				}

				else if (groupSymbol == "NEW ZEALAND RATES >" || groupSymbol == "NEW ZEALAND RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"NEW ZEALAND GENERIC >",
					"NEW ZEALAND CURVES >",
					"NEW ZEALAND BUTTERFLIES >",
					"NEW ZEALAND CDS >",
					"NEW ZEALAND SPREADS >",
					"NEW ZEALAND SWAPS >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"NEW ZEALAND GENERIC",
					"NEW ZEALAND CURVES",
					"NEW ZEALAND BUTTERFLIES",
					"NEW ZEALAND CDS",
					"NEW ZEALAND SPREADS",
					"NEW ZEALAND SWAPS"
				});

					ok = true;
				}
			}

			else if (country == "MEA RATES >")
			{
				if (groupSymbol == "ISRAEL RATES >" || groupSymbol == "ISRAEL RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"ISRAEL GENERIC >",
					"ISRAEL BREAKEVEN >",
					"ISRAEL SWAPS >"
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"ISRAEL GENERIC",
					"ISRAEL BREAKEVEN",
					"ISRAEL SWAPS"
				});

					ok = true;
				}

				else if (groupSymbol == "SOUTH AFRICA RATES >" || groupSymbol == "SOUTH AFRICA RATES")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
					"SOUTH AFRICA GENERIC >",
					"SOUTH AFRICA BREAKEVEN >",
					"SOUTH AFRICA CDS >",
					"SOUTH AFRICA SWAPS >",
				 });

					else setNavigation(panel, mouseDownEvent, new string[] {
					"SOUTH AFRICA GENERIC",
					"SOUTH AFRICA BREAKEVEN",
					"SOUTH AFRICA CDS",
					"SOUTH AFRICA SWAPS",
				});

					ok = true;
				}
			}

			else if (country == "BLOOMBERG BARCLAYS >")
			{
				if (groupSymbol == "MUNI BOND INDEX >" || groupSymbol == "MUNI BOND INDEX")
				{
					string[] items = new string[]
					{
					"MATURITY",
					"TYPE",
					"QUALITY",
					"MANAGED MONEY",
					"STATE INDICES",
					"CUSTOM"
					};

					setNavigation(panel, mouseDownEvent, items);

					ok = true;
				}
				if (groupSymbol == "TAXABLE MUNI BOND INDEX >" || groupSymbol == "TAXABLE MUNI BOND INDEX")
				{
					string[] items = new string[]
					{
					"TAXABLE MATURITY"
					};

					setNavigation(panel, mouseDownEvent, items);

					ok = true;
				}
				if (groupSymbol == "MUNI HIGH YLD >" || groupSymbol == "MUNI HIGH YLD")
				{
					string[] items = new string[]
					{
					"HY MATURITY",
					"HY SECTOR",
					"HY COMPOSITES",
					"HY CAPPED"
					};

					setNavigation(panel, mouseDownEvent, items);

					ok = true;
				}
			}

			else if (country == "EMEA >" || country == "EMEA")
			{
				if (groupSymbol == "BWORLDEU")
				{
					setNavigation(panel, mouseDownEvent, new string[]
					{
						"BWORLDEU:BWORLDEU INDEX >",
						"EMEA INDUSTRIES:EMEA INDUSTRIES >"  // EMEA Industries not a symbol like the others
                    });
					ok = true;
				}
			}

			else if (country == "EQ | ASIA PACIFIC >" || country == "ASIA PACIFIC")
			{
				if (groupSymbol == "BWORLDPR")
				{
					setNavigation(panel, mouseDownEvent, new string[]
					{
						"BWORLDPR:BWORLDPR INDEX >",
						"ASIA PACIFIC INDUSTRIES:ASIA PACIFIC INDUSTRIES >"  // ASIA PACIFIC Industries not a symbol like the others
                    });
					ok = true;
				}
			}

			else if (country == "EURO EQ >" || country == "EUROPE >" || country == "EUROPE")
			{
				if (groupSymbol == "BE500")
				{
					setNavigation(panel, mouseDownEvent, new string[]
					{
						"BE500:BE500 INDEX >",
						"BEAUTOP:BE500 AUTO PARTS >",
						"BEAUTOS:BE500 AUTOS >",
						"BEBANKS:BE500 BNK & FIN SERV >",
						"BEBEVGS:BE500 BEVERAGES >",
						"BEBULDM:BE500 BUILD MATERIALS >",
						"BECHEMC:BE500 CHEMICALS >",
						"BECOMPH:BE500 COMP HRD & SFW >",
						"BECOMPS:BE500 COMPTR SERVICE >",
						"BECOMSV:BE500 COMM SERVICES >",
						"BECONSP:BE500 CONSUMER PRODS >",
						"BECONST:BE500 CONST & ENGIN >",
						"BEDIVRX:BE500 DIVERSIFIED >",
						"BEELECT:BE500 ELECTRIC >",
						"BEENRGX:BE500 ENERGY >",
						"BEFOOD:BE500 FOOD >",
						"BEFOODR:BE500 FOOD RETAIL >",
						"BEFURNI:BE500 FURN & APPAREL >",
						"BEGAS:BE500 GAS & OIL >",
						"BEHLTHC:BE500 HEALTH CARE >",
						"BEINDUP:BE500 INDUST PRODUCTS >",
						"BEINSUR:BE500 INSURANCE >",
						"BEINVST:BE500 INVESTMT & SVCS >",
						"BEMACHN:BE500 MACHINERY >",
						"BEMANUF:BE500 MANUFACTURING >",
						"BEMEDIA:BE500 MEDIA >",
						"BEMETAL:BE500 METAL & MINE >",
						"BEPAPER:BE500 PAPER & FOREST >",
						"BEPHARM:BE500 PHARMACEUTICAL >",
						"BEREALE:BE500 REAL ESTATE >",
						"BERETAI:BE500 RETAIL >",
						"BESTEEL:BE500 STEEL >",
						"BETELEE:BE500 TELECOM EQUIP >",
						"BETELES:BE500 TELECOM SERVICE >",
						"BETOBAC:BE500 TOBACCO >",
						"BETRANS:BE500 TRANSPORTATION >",
						"BETRAVL:BE500 TRAVEL & LEIS >",
						"BEWATER:BE500 WATER >"  });
					ok = true;
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"BE500:BE500 INDEX",
						"BEAUTOP:BE500 AUTO PARTS >",
						"BEAUTOS:BE500 AUTOS >",
						"BEBANKS:BE500 BNK & FIN SERV >",
						"BEBEVGS:BE500 BEVERAGES >",
						"BEBULDM:BE500 BUILD MATERIALS >",
						"BECHEMC:BE500 CHEMICALS >",
						"BECOMPH:BE500 COMP HRD & SFW >",
						"BECOMPS:BE500 COMPTR SERVICE >",
						"BECOMSV:BE500 COMM SERVICES >",
						"BECONSP:BE500 CONSUMER PRODS >",
						"BECONST:BE500 CONST & ENGIN >",
						"BEDIVRX:BE500 DIVERSIFIED >",
						"BEELECT:BE500 ELECTRIC >",
						"BEENRGX:BE500 ENERGY >",
						"BEFOOD:BE500 FOOD >",
						"BEFOODR:BE500 FOOD RETAIL >",
						"BEFURNI:BE500 FURN & APPAREL >",
						"BEGAS:BE500 GAS & OIL >",
						"BEHLTHC:BE500 HEALTH CARE >",
						"BEINDUP:BE500 INDUST PRODUCTS >",
						"BEINSUR:BE500 INSURANCE >",
						"BEINVST:BE500 INVESTMT & SVCS >",
						"BEMACHN:BE500 MACHINERY >",
						"BEMANUF:BE500 MANUFACTURING >",
						"BEMEDIA:BE500 MEDIA >",
						"BEMETAL:BE500 METAL & MINE >",
						"BEPAPER:BE500 PAPER & FOREST >",
						"BEPHARM:BE500 PHARMACEUTICAL >",
						"BEREALE:BE500 REAL ESTATE >",
						"BERETAI:BE500 RETAIL >",
						"BESTEEL:BE500 STEEL >",
						"BETELEE:BE500 TELECOM EQUIP >",
						"BETELES:BE500 TELECOM SERVICE >",
						"BETOBAC:BE500 TOBACCO >",
						"BETRANS:BE500 TRANSPORTATION >",
						"BETRAVL:BE500 TRAVEL & LEIS >",
						"BEWATER:BE500 WATER >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"BE500:BE500 INDEX",
						"BEAUTOP:BE500 AUTO PARTS",
						"BEAUTOS:BE500 AUTOS",
						"BEBANKS:BE500 BNK & FIN SERV",
						"BEBEVGS:BE500 BEVERAGES",
						"BEBULDM:BE500 BUILD MATERIALS",
						"BECHEMC:BE500 CHEMICALS",
						"BECOMPH:BE500 COMP HRD & SFW",
						"BECOMPS:BE500 COMPTR SERVICE",
						"BECOMSV:BE500 COMM SERVICES",
						"BECONSP:BE500 CONSUMER PRODS",
						"BECONST:BE500 CONST & ENGIN",
						"BEDIVRX:BE500 DIVERSIFIED",
						"BEELECT:BE500 ELECTRIC",
						"BEENRGX:BE500 ENERGY",
						"BEFOOD:BE500 FOOD",
						"BEFOODR:BE500 FOOD RETAIL",
						"BEFURNI:BE500 FURN & APPAREL",
						"BEGAS:BE500 GAS & OIL",
						"BEHLTHC:BE500 HEALTH CARE",
						"BEINDUP:BE500 INDUST PRODUCTS",
						"BEINSUR:BE500 INSURANCE",
						"BEINVST:BE500 INVESTMT & SVCS",
						"BEMACHN:BE500 MACHINERY",
						"BEMANUF:BE500 MANUFACTURING",
						"BEMEDIA:BE500 MEDIA",
						"BEMETAL:BE500 METAL & MINE",
						"BEPAPER:BE500 PAPER & FOREST",
						"BEPHARM:BE500 PHARMACEUTICAL",
						"BEREALE:BE500 REAL ESTATE",
						"BERETAI:BE500 RETAIL",
						"BESTEEL:BE500 STEEL",
						"BETELEE:BE500 TELECOM EQUIP",
						"BETELES:BE500 TELECOM SERVICE",
						"BETOBAC:BE500 TOBACCO",
						"BETRANS:BE500 TRANSPORTATION",
						"BETRAVL:BE500 TRAVEL & LEIS",
						"BEWATER:BE500 WATER"
					});
					ok = true;
				}

				else if (groupSymbol == "SXXE")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"SXXE:SXXE INDEX >",
						"SXAE:SXXE Au&Pt Pr >",
						"SX7E:SXXE Bnk Pr >",
						"SXPE:SXXE BsRs Pr >",
						"SXOE:SXXE Cn&Mt Pr >",
						"SX4E:SXXE Chem Pr >",
						"SX3E:SXXE Fd&Bv Pr >",
						"SXFE:SXXE FnSv Pr >",
						"SXDE:SXXE HeCr Pr >",
						"SXNE:SXXE Ig&S Pr >",
						"SXIE:SXXE Ins Pr >",
						"SXME:SXXE Mda Pr >",
						"SXEE:SXXE Oil&G Pr >",
						"SXQE:SXXE Pr&Ho Pr >",
						"SX86E:SXXE ReEs Pr >",
						"SXRE:SXXE Rtl Pr >",
						"SX8E:SXXE Tech Pr >",
						"SXKE:SXXE Tel Pr >",
						"SXTE:SXXE Tr&Ls Pr >",
						"SX6E:SXXE Util Pr >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"SXXE:SXXE INDEX",
						"SXAE:SXXE Au&Pt Pr",
						"SX7E:SXXE Bnk Pr",
						"SXPE:SXXE BsRs Pr",
						"SXOE:SXXE Cn&Mt Pr",
						"SX4E:SXXE Chem Pr",
						"SX3E:SXXE Fd&Bv Pr",
						"SXFE:SXXE FnSv Pr",
						"SXDE:SXXE HeCr Pr",
						"SXNE:SXXE Ig&S Pr",
						"SXIE:SXXE Ins Pr",
						"SXME:SXXE Mda Pr",
						"SXEE:SXXE Oil&G Pr",
						"SXQE:SXXE Pr&Ho Pr",
						"SX86E:SXXE ReEs Pr",
						"SXRE:SXXE Rtl Pr",
						"SX8E:SXXE Tech Pr",
						"SXKE:SXXE Tel Pr",
						"SXTE:SXXE Tr&Ls Pr",
						"SX6E:SXXE Util Pr"
					});
					ok = true;
				}

				else if (groupSymbol == "SXXP")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"SXXP:SXXP INDEX",
						"SXAP:SXXP Au&Pt Pr >",
						"SX7P:SXXP Bnk Pr >",
						"SXPP:SXXP BsRs Pr >",
						"SX4P:SXXP Chem Pr >",
						"SXOP:SXXP Cn&Mt Pr >",
						"SX3P:SXXP Fd&Bv Pr >",
						"SXFP:SXXP FnSv Pr >",
						"SXDP:SXXP HeCr Pr >",
						"SXNP:SXXP Ig&S Pr >",
						"SXIP:SXXP Ins Pr >",
						"SXMP:SXXP Mda Pr >",
						"SXEP:SXXP Oil&G Pr >",
						"SXQP:SXXP Pr&Ho Pr >",
						"SX86P:SXXP ReEs Pr >",
						"SXRP:SXXP Rtl Pr >",
						"SX8P:SXXP Tech Pr >",
						"SXKP:SXXP Tel Pr >",
						"SXTP:SXXP Tr&Ls Pr >",
						"SX6P:SXXP Util Pr >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"SXXP:SXXP INDEX",
						"SXAP:SXXP Au&Pt Pr",
						"SX7P:SXXP Bnk Pr",
						"SXPP:SXXP BsRs Pr",
						"SX4P:SXXP Chem Pr",
						"SXOP:SXXP Cn&Mt Pr",
						"SX3P:SXXP Fd&Bv Pr",
						"SXFP:SXXP FnSv Pr",
						"SXDP:SXXP HeCr Pr",
						"SXNP:SXXP Ig&S Pr",
						"SXIP:SXXP Ins Pr",
						"SXMP:SXXP Mda Pr",
						"SXEP:SXXP Oil&G Pr",
						"SXQP:SXXP Pr&Ho Pr",
						"SX86P:SXXP ReEs Pr",
						"SXRP:SXXP Rtl Pr",
						"SX8P:SXXP Tech Pr",
						"SXKP:SXXP Tel Pr",
						"SXTP:SXXP Tr&Ls Pr",
						"SX6P:SXXP Util Pr"
					});
					ok = true;
				}

				else if (groupSymbol == "SPEU")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"SPEU:SPEU INDEX",
						"SEUCOND:SPEU CONS DISC >",
						"SEUCONS:SPEU CONS STAPLES >",
						"SEUENRS:SPEU ENERGY >",
						"SEUFINL:SPEU FINANCIALS >",
						"SEUHLTH:SPEU HEALTH CARE >",
						"SEUINDU:SPEU INDUSTRIALS >",
						"SEUINFT:SPEU INFO TECH >",
						"SEUMATR:SPEU MATERIAL >",
						"SEUTELS:SPEU TELECOM SVC >",
						"SEUUTIL:SPEU UTILITIES >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"SPEU:SPEU INDEX",
						"SEUCOND:SPEU CONS DISC",
						"SEUCONS:SPEU CONS STAPLES",
						"SEUENRS:SPEU ENERGY",
						"SEUFINL:SPEU FINANCIALS",
						"SEUHLTH:SPEU HEALTH CARE",
						"SEUINDU:SPEU INDUSTRIALS",
						"SEUINFT:SPEU INFO TECH",
						"SEUMATR:SPEU MATERIAL",
						"SEUTELS:SPEU TELECOM SVC",
						"SEUUTIL:SPEU UTILITIES"
					});
					ok = true;
				}
				else if (groupSymbol == "E300")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"E300:E300 INDEX",
						"E3AERO:E300 AEROSPACE >",
						"E3ALNRG:E300 ALT ENERGY >",
						"E3AUTO:E300 AUTO & PARTS >",
						"E3BANK:E300 BANK >",
						"E3BEVG:E300 BEVERAGE >",
						"E3CHEM:E300 CHEMICALS >",
						"E3CONS:E300 CONST >",
						"E3ELEC:E300 ELEC >",
						"E3ELTR:E300 ELECTONIC ELECTt >",
						"E3OTHR:E300 FINANCIAL >",
						"E3FOOD:E300 FOOD >",
						"E3FDRT:E300 FOOD & DRUG >",
						"E3UTLO:E300 GAS WATER & MULTI UTL >",
						"E3DIND:E300 GEN INDUSTRIALS >",
						"E3RETG:E300 GENl RETAILERS >",
						"E3HLTH:E300 HEALTH CARE EQP >",
						"EFHOUGE:E300 HOUSEHOLD GOODS >",
						"E3HOUS:E300 HOUSING >",
						"E3ENGN:E300 IND ENG >",
						"E3INSU:E300 INSURANCE >",
						"E3INVC:E300 INVEST INST >",
						"E3LIFE:E300 LIFE INS >",
						"E3MEDA:E300 MEDIA >",
						"E3METL:E300 METALS >",
						"E3MNG:E300 MINING >",
						"EFMOBTE:E300 MOBILE TELECOM >",
						"EFNEIIE:E300 NonEQUITY INV >",
						"E3OILG:E300 OIL & GAS >",
						"EFOESDE:E300 OIL EQP SVC >",
						"E3PAPR:E300 PAPER >",
						"E3PERC:E300 PERSONAL GOODS >",
						"E3PHRM:E300 PHARM & BIOTECH >",
						"E3REISV:E300 REAL ESTATE INV >",
						"E3REITS:E300 REITS >",
						"E3SOFT:E300 SOFTWARE & COMP >",
						"E3SUPP:E300 SUPPORT SVC >",
						"E3INFT:E300 TECHNOLOGY H/W >",
						"E3TELE:E300 TELECOM >",
						"E3TOBC:E300 TOBACCO >",
						"E3TRAN:E300 TRANSPORTATION >",
						"E3LEIS:E300 TRAVELl & LEISURE >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"E300:E300 INDEX",
						"E3AERO:E300 AEROSPACE",
						"E3ALNRG:E300 ALT ENERGY",
						"E3AUTO:E300 AUTO & PARTS",
						"E3BANK:E300 BANK",
						"E3BEVG:E300 BEVERAGE",
						"E3CHEM:E300 CHEMICALS",
						"E3CONS:E300 CONST",
						"E3ELEC:E300 ELEC",
						"E3ELTR:E300 ELECTONIC ELECTt",
						"E3OTHR:E300 FINANCIAL",
						"E3FOOD:E300 FOOD",
						"E3FDRT:E300 FOOD & DRUG",
						"E3UTLO:E300 GAS WATER & MULTI UTL",
						"E3DIND:E300 GEN INDUSTRIALS",
						"E3RETG:E300 GENl RETAILERS",
						"E3HLTH:E300 HEALTH CARE EQP",
						"EFHOUGE:E300 HOUSEHOLD GOODS",
						"E3HOUS:E300 HOUSING",
						"E3ENGN:E300 IND ENG",
						"E3INSU:E300 INSURANCE",
						"E3INVC:E300 INVEST INST",
						"E3LIFE:E300 LIFE INS",
						"E3MEDA:E300 MEDIA",
						"E3METL:E300 METALS",
						"E3MNG:E300 MINING",
						"EFMOBTE:E300 MOBILE TELECOM",
						"EFNEIIE:E300 NonEQUITY INV",
						"E3OILG:E300 OIL & GAS",
						"EFOESDE:E300 OIL EQP SVC",
						"E3PAPR:E300 PAPER",
						"E3PERC:E300 PERSONAL GOODS",
						"E3PHRM:E300 PHARM & BIOTECH",
						"E3REISV:E300 REAL ESTATE INV",
						"E3REITS:E300 REITS",
						"E3SOFT:E300 SOFTWARE & COMP",
						"E3SUPP:E300 SUPPORT SVC",
						"E3INFT:E300 TECHNOLOGY H/W",
						"E3TELE:E300 TELECOM",
						"E3TOBC:E300 TOBACCO",
						"E3TRAN:E300 TRANSPORTATION",
						"E3LEIS:E300 TRAVELl & LEISURE"
					});
					ok = true;
				}
			}

			else if (country == "UK EQ >" || country == "UK >" || country == "UK")
			{
				if (groupSymbol == "ASX")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"ASX:ASX INDEX",
						"FAAERO:ASX AEROSPACE & DEF >",
						"FAALNRG:ASX ALT ENERGY >",
						"FAAUTO:ASX AUTO PARTS >",
						"FABANK:ASX BANKS >",
						"FABEVG:ASX BEVERAGES >",
						"FACHEM:ASX CHEMICAL >",
						"FACONS:ASX CONSTRUCTION MAT >",
						"FAELEC:ASX ELECTRICITY >",
						"FAELTR:ASX ELECT/ELECT EQUIP >",
						"FAINVC:ASX EGY INVST INSTR >",
						"FATELE:ASX FIXED LINE TELE >",
						"FAOTHR:ASX FINANCIAL SRVS >",
						"FAFDRT:ASX FOOD DRUG RETAIL >",
						"FAFOOD:ASX FOOD PRODUCERS >",
						"FAPAPR:ASX FORESTRY PAPERS >",
						"FADIND:ASX GEN INDUSTRIALS >",
						"FARETG:ASX GEN RETAILERS >",
						"FAUTLO:ASX GAS WATER UTL >",
						"FAHLTH:ASX HEALTH CARE EQUIP >",
						"FXHOUGE:ASX HOUSE GD & HM >",
						"FAENGN:ASX INDURTRIAL ENG >",
						"FAMETL:ASX INDUST METAL & MINE >",
						"FATRAN:ASX INDUST TRANSPORT >",
						"FAHOUS:ASX LEISURE GOODS >",
						"FALIFE:ASX LIFE INS >",
						"FAMEDA:ASX MEDIA >",
						"FAMNG:ASX MINING >",
						"FXMOBTE:ASX MOBILE TELECOM >",
						"FAINSU:ASX NON LIFE INS >",
						"FXOESDE:ASX OIL EQUIP SERV >",
						"FAOILG:ASX OIL GAS PROD >",
						"FAPHRM:ASX PHARM & BIOTECH >",
						"FAPERC:ASX PERSONAL GOODS >",
						"FAREITS:ASX REITS >",
						"FAREISV:ASX RE INVST SR >V",
						"FASOFT:ASX SOFTWARE COMP SVRS >",
						"FASUPP:ASX SUPPORT SRVS >",
						"FAINFT:ASX TECH HARDWARE >",
						"FATOBC:ASX TOBACCO >",
						"FALEIS:ASX TRAVEL LEISURE >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"ASX:ASX INDEX",
						"FAAERO:ASX AEROSPACE & DEF",
						"FAALNRG:ASX ALT ENERGY",
						"FAAUTO:ASX AUTO PARTS",
						"FABANK:ASX BANKS",
						"FABEVG:ASX BEVERAGES",
						"FACHEM:ASX CHEMICAL",
						"FACONS:ASX CONSTRUCTION MAT",
						"FAELEC:ASX ELECTRICITY",
						"FAELTR:ASX ELECT/ELECT EQUIP",
						"FAINVC:ASX EGY INVST INSTR",
						"FATELE:ASX FIXED LINE TELE",
						"FAOTHR:ASX FINANCIAL SRVS",
						"FAFDRT:ASX FOOD DRUG RETAIL",
						"FAFOOD:ASX FOOD PRODUCERS",
						"FAPAPR:ASX FORESTRY PAPERS",
						"FADIND:ASX GEN INDUSTRIALS",
						"FARETG:ASX GEN RETAILERS",
						"FAUTLO:ASX GAS WATER UTL",
						"FAHLTH:ASX HEALTH CARE EQUIP",
						"FXHOUGE:ASX HOUSE GD & HM",
						"FAENGN:ASX INDURTRIAL ENG",
						"FAMETL:ASX INDUST METAL & MINE",
						"FATRAN:ASX INDUST TRANSPORT",
						"FAHOUS:ASX LEISURE GOODS",
						"FALIFE:ASX LIFE INS",
						"FAMEDA:ASX MEDIA",
						"FAMNG:ASX MINING",
						"FXMOBTE:ASX MOBILE TELECOM",
						"FAINSU:ASX NON LIFE INS",
						"FXOESDE:ASX OIL EQUIP SERV",
						"FAOILG:ASX OIL GAS PROD",
						"FAPHRM:ASX PHARM & BIOTECH",
						"FAPERC:ASX PERSONAL GOODS",
						"FAREITS:ASX REITS",
						"FAREISV:ASX RE INVST SR >V",
						"FASOFT:ASX SOFTWARE COMP SVRS",
						"FASUPP:ASX SUPPORT SRVS",
						"FAINFT:ASX TECH HARDWARE",
						"FATOBC:ASX TOBACCO",
						"FALEIS:ASX TRAVEL LEISURE"
					});
					ok = true;
				}

				else if (groupSymbol == "AXX")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"AXX:AXX INDEX",
						"AXAUP:AXX AUTO & PARTS >",
						"AXBANK:AXX BANKS >",
						"AXBASR:AXX BASIC RESOURCES >",
						"AXCHEM:AXX CHEMICALS >",
						"AXCONS:AXX CONST MATERIALS >",
						"AXFIN:AXX FINANCIAL SERV >",
						"AXFOB:AXX FOOD & BEVERAGE >",
						"AXHEAL:AXX HEALTH CARE >",
						"AXIGS:AXX IND GOOD & SRV >",
						"AXINSU:AXX INSURANCE >",
						"AXMEDI:AXX MEDIA >",
						"AXOIG:AXX OIL & GAS >",
						"AXPERS:AXX PERS & HOUSEHOLD >",
						"AXREAL:AXX REAL ESTATE >",
						"AXRETA:AXX RETAIL >",
						"AXTECH:AXX TECHNOLOGY >",
						"AXTELE:AXX TELECOM >",
						"AXTRAV:AXX TRAVEL LEISURE >",
						"AXUTIL:AXX UTILITIES >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"AXX:AXX INDEX",
						"AXAUP:AXX AUTO & PARTS",
						"AXBANK:AXX BANKS",
						"AXBASR:AXX BASIC RESOURCES",
						"AXCHEM:AXX CHEMICALS",
						"AXCONS:AXX CONST MATERIALS",
						"AXFIN:AXX FINANCIAL SERV",
						"AXFOB:AXX FOOD & BEVERAGE",
						"AXHEAL:AXX HEALTH CARE",
						"AXIGS:AXX IND GOOD & SRV",
						"AXINSU:AXX INSURANCE",
						"AXMEDI:AXX MEDIA",
						"AXOIG:AXX OIL & GAS",
						"AXPERS:AXX PERS & HOUSEHOLD",
						"AXREAL:AXX REAL ESTATE",
						"AXRETA:AXX RETAIL",
						"AXTECH:AXX TECHNOLOGY",
						"AXTELE:AXX TELECOM",
						"AXTRAV:AXX TRAVEL LEISURE",
						"AXUTIL:AXX UTILITIES"
					});
					ok = true;
				}

				else if (groupSymbol == "NMX")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"NMX:NMX INDEX",
						"F3AERO:NMX AEROSPACE & DEF >",
						"F3ALNRG:NMX ALT ENERGY >",
						"F3AUTO:NMX AUTO & PART >",
						"F3BANK:NMX BANKS >",
						"F3BEVG:NMX BEVERAGES >",
						"F3CHEM:NMX CHEMCIALS >",
						"F3CONS:NMX CONST MATERIAL >",
						"F3ELEC:NMX ELECTRICITY >",
						"F3ELTR:NMX ELEC-ELEC EQUIP >",
						"F3INVC:NMX EQT INVEST >",
						"F3FDRT:NMX FOOD DRUG >",
						"F3TELE:NMX FIXED LINE TELECOM >",
						"F3APAPR:NMX FRSTRY-PAPER >",
						"F3FOOD:NMX FOOD PROD >",
						"F3OTHR:NMX FINANCIAL SERV >",
						"F3UTLO:NMX GAS WTR & MULTI >",
						"F3DIND:NMX GEN INDUSTRIAL >",
						"F3RETG:NMX GEN RETAIL >",
						"F3HLTH:NMX HC EQUIP >",
						"F3HOUGE:NMX HOUSE GD & H CON >",
						"F3ENGN:NMX INDUSTRY ENG >",
						"F3METL:NMX INDUST METAL & MINING >",
						"F3TRAN:NMX INDUSTRY TRANSPORT >",
						"F3LIFE:NMX LIFE INS >",
						"F3INSU:NMX NON LIFE INSUR >",
						"F3MEDA:NMX MEDIA >",
						"F3MNG:NMX MINING >",
						"F3MOBTE:NMX MOBILE TEL >",
						"F3OESDE:NMX OIL EQ SVS >",
						"F3OILG:NMX OIL & GAS >",
						"F3PERC:NMX PERSONAL >",
						"F3PHRM:NMX PHARM & BIOTECH >",
						"F3REITS:NMX REITS >",
						"F3REISV:NMX RE INVEST SRV >",
						"F3SOFT:NMX SOFTWARE/PC SRV >",
						"F3SUPP:NMX SUPPORT SERV .",
						"F3TOBC:NMX TOBACCO >",
						"F3INFT:NMX TECH HRD & EQUIP >",
						"F3LEIS:NMX TRAVEL & LEISURE >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"NMX:NMX INDEX",
						"F3AERO:NMX AEROSPACE & DEF",
						"F3ALNRG:NMX ALT ENERGY",
						"F3AUTO:NMX AUTO & PART",
						"F3BANK:NMX BANKS",
						"F3BEVG:NMX BEVERAGES",
						"F3CHEM:NMX CHEMCIALS",
						"F3CONS:NMX CONST MATERIAL",
						"F3ELEC:NMX ELECTRICITY",
						"F3ELTR:NMX ELEC-ELEC EQUIP",
						"F3INVC:NMX EQT INVEST",
						"F3FDRT:NMX FOOD DRUG",
						"F3TELE:NMX FIXED LINE TELECOM",
						"F3APAPR:NMX FRSTRY-PAPER",
						"F3FOOD:NMX FOOD PROD",
						"F3OTHR:NMX FINANCIAL SERV",
						"F3UTLO:NMX GAS WTR & MULTI",
						"F3DIND:NMX GEN INDUSTRIAL",
						"F3RETG:NMX GEN RETAIL",
						"F3HLTH:NMX HC EQUIP",
						"F3HOUGE:NMX HOUSE GD & H CON",
						"F3ENGN:NMX INDUSTRY ENG",
						"F3METL:NMX INDUST METAL & MINING",
						"F3TRAN:NMX INDUSTRY TRANSPORT",
						"F3LIFE:NMX LIFE INS",
						"F3INSU:NMX NON LIFE INSUR",
						"F3MEDA:NMX MEDIA",
						"F3MNG:NMX MINING",
						"F3MOBTE:NMX MOBILE TEL",
						"F3OESDE:NMX OIL EQ SVS",
						"F3OILG:NMX OIL & GAS",
						"F3PERC:NMX PERSONAL",
						"F3PHRM:NMX PHARM & BIOTECH",
						"F3REITS:NMX REITS",
						"F3REISV:NMX RE INVEST SRV",
						"F3SOFT:NMX SOFTWARE/PC SRV",
						"F3SUPP:NMX SUPPORT SERV .",
						"F3TOBC:NMX TOBACCO",
						"F3INFT:NMX TECH HRD & EQUIP",
						"F3LEIS:NMX TRAVEL & LEISURE"
					});
					ok = true;
				}
			}

			else if (country == "FRANCE EQ >" || country == "FRANCE >" || country == "France")
			{
				if (groupSymbol == "SBF250")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"SBF250:SBF250 INDEX",
						"EPBASE:SBF250 BASIC MATERIAL >",
						"EPBCYC:SBF250 CONSUMER GOODS >",
						"EPSCYC:SBF250 CONSUMER SRV >",
						"EPSFIN:SBF250 FINANCIALS >",
						"EPBNCY:SBF250 HEALTH CARE >",
						"EPGENE:SBF250 INDUSTRIALS >",
						"EPRESS:SBF250 OIL & GAS >",
						"EPSNCY:SBF250 TELECOM >",
						"EPTECI:SBF250 TECHNOLOGY >",
						"EPSPUB:SBF250 UTLITIES >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"SBF250:SBF250 INDEX",
						"EPBASE:SBF250 BASIC MATERIAL",
						"EPBCYC:SBF250 CONSUMER GOODS",
						"EPSCYC:SBF250 CONSUMER SRV",
						"EPSFIN:SBF250 FINANCIALS",
						"EPBNCY:SBF250 HEALTH CARE",
						"EPGENE:SBF250 INDUSTRIALS",
						"EPRESS:SBF250 OIL & GAS",
						"EPSNCY:SBF250 TELECOM",
						"EPTECI:SBF250 TECHNOLOGY",
						"EPSPUB:SBF250 UTLITIES"
					});
					ok = true;
				}
			}

			else if (country == "SPAIN EQ >" || country == "SPAIN >" || country == "Spain")
			{
				if (groupSymbol == "MADX")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"MADX:MADX INDEX",
						"MAB2:MADX BASIC MATERIALS >",
						"MAB3:MADX CONSUMER GOODS >",
						"MAS4:MADX CONSUMER SRVS >",
						"MAS5:MADX FINANCIAL SRV & RE >",
						"MAE1:MADX PTRL & PWR >",
						"MAS6T:MADX TECH & TELECOM >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"MADX:MADX INDEX",
						"MAB2:MADX BASIC MATERIALS",
						"MAB3:MADX CONSUMER GOODS",
						"MAS4:MADX CONSUMER SRVS",
						"MAS5:MADX FINANCIAL SRV & RE",
						"MAE1:MADX PTRL & PWR",
						"MAS6T:MADX TECH & TELECOM"
					});
					ok = true;
				}
			}

			else if (country == "ITALY EQ >" || country == "ITALY >" || country == "Italy")
			{
				if (groupSymbol == "ITLMS")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"ITLMS:ITLMS INDEX",
						"IT1000:ITLMS BASIC MATERIAL >",
						"IT3000:ITLMS CONSUMER GDS >",
						"IT5000:ITLMS CONSUMER SER >",
						"IT8000:ITLMS FINANCIALS >",
						"IT4000:ITLMS HEALTH CARE >",
						"IT2000:ITLMS INDUSTRIAL >",
						"IT0001:ITLMS OIL >",
						"IT6000:ITLMS TELECOM >",
						"IT9000:ITLMS TECHNOLOGY >",
						"IT7000:ITLMS UTILITIES >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						 "ITLMS:ITLMS INDEX",
						"IT1000:ITLMS BASIC MATERIAL",
						"IT3000:ITLMS CONSUMER GDS",
						"IT5000:ITLMS CONSUMER SER",
						"IT8000:ITLMS FINANCIALS",
						"IT4000:ITLMS HEALTH CARE",
						"IT2000:ITLMS INDUSTRIAL",
					});
					ok = true;
				}
			}

			else if (country == "SWEDEN EQ >" || country == "SWEDEN >" || country == "Sweden")
			{
				if (groupSymbol == "SAX")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"SAX:SAX INDEX",
						"SX3000PI:SAX CONS GOODS >",
						"SX5000PI:SAX CONS SERVICES >",
						"SX0001PI:SAX ENERGY >",
						"SX8000PI:SAX FINANCIALS >",
						"SX4000PI:SAX HEALTH CARE >",
						"SX2000PI:SAX INDUSTRIALS >",
						"SX1000PI:SAX MATERIALS >",
						"SX9000PI:SAX TECH >",
						"SX9000PI:SAX TELECOM >",
						"SX7000PI:SAX UTILITIES >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"SAX:SAX INDEX",
						"SX3000PI:SAX CONS GOODS",
						"SX5000PI:SAX CONS SERVICES",
						"SX0001PI:SAX ENERGY",
						"SX8000PI:SAX FINANCIALS",
						"SX4000PI:SAX HEALTH CARE",
						"SX2000PI:SAX INDUSTRIALS",
						"SX1000PI:SAX MATERIALS",
						"SX9000PI:SAX TECH",
						"SX9000PI:SAX TELECOM",
						"SX7000PI:SAX UTILITIES"

					});
					ok = true;
				}
			}

			else if (country == "FINLAND EQ >" || country == "FINLAND >" || country == "Finland")
			{
				if (groupSymbol == "HEX")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"HEX:HEX INDEX",
						"HX3000PI:HEX CONS GOODS >",
						"HX5000PI:HEX CONS SERVICES >",
						"HX0001PI:HEX ENERGY >",
						"HX8000PI:HEX FINANCIALS >",
						"HX4000PI:HEX HEALTH CARE >",
						"HX2000PI:HEX INDUSTRIALS >",
						"HX1000PI:HEX MATERIALS >",
						"HX9000PI:HEX TECH >",
						"HX9000PI:HEX TELECOM >",
						"HX7000PI:HEX UTILITIES >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"HEX:HEX INDEX",
						"HX3000PI:HEX CONS GOODS",
						"HX5000PI:HEX CONS SERVICES",
						"HX0001PI:HEX ENERGY",
						"HX8000PI:HEX FINANCIALS",
						"HX4000PI:HEX HEALTH CARE",
						"HX2000PI:HEX INDUSTRIALS",
						"HX1000PI:HEX MATERIALS",
						"HX9000PI:HEX TECH",
						"HX9000PI:HEX TELECOM",
						"HX7000PI:HEX UTILITIES"
					});
					ok = true;
				}
			}

			else if (country == "IRELAND EQ >" || country == "IRELAND >" || country == "Ireland")
			{
				if (groupSymbol == "ISEQ")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"ISEQ:ISEQ INDEX",
						"ISEF:ISEQ FINANCIAL >",
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"ISEQ:ISEQ INDEX",
						"ISEF:ISEQ FINANCIAL",
					});
					ok = true;
				}
			}

			else if (country == "DENMARK EQ >" || country == "DENMARK >" || country == "Denmark")
			{
				if (groupSymbol == "KAX")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"KAX:KAX INDEX",
						"CX3000PI:KAX CONS GOODS >",
						"CX5000PI:KAX CONS SERVICES >",
						"CX0001PI:KAX ENERGY >",
						"CX8000PI:KAX FINANCIALS >",
						"CX4000PI:KAX HEALTH CARE >",
						"CX2000PI:KAX INDUSTRIALS >",
						"CX1000PI:KAX MATERIALS >",
						"CX9000PI:KAX TECH >",
						"CX9000PI:KAX TELECOM >",
						"CX7000PI:KAX UTILITIES >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"KAX:KAX INDEX",
						"CX3000PI:KAX CONS GOODS",
						"CX5000PI:KAX CONS SERVICES",
						"CX0001PI:KAX ENERGY",
						"CX8000PI:KAX FINANCIALS",
						"CX4000PI:KAX HEALTH CARE",
						"CX2000PI:KAX INDUSTRIALS",
						"CX1000PI:KAX MATERIALS",
						"CX9000PI:KAX TECH",
						"CX9000PI:KAX TELECOM",
						"CX7000PI:KAX UTILITIES"
					});
					ok = true;
				}
			}

			else if (country == "PORTUGAL EQ >" || country == "PORTUGAL >" || country == "Portugal")
			{
				if (groupSymbol == "BVLX")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"BVLX:BVLX INDEX >",
						"PSICCG:BVLX CONS GOODS >",
						"PSICSV:BVLX CONS SERVICE >",
						"PSIFIN:BVLX FINANCIALS >",
						"PSIGEN:BVLX INDUSTRIALS >",
						"PSIIND:BVLX BASIC MATERIALS >",
						"PSINSV:BVLX TELECOM >",
						"PSITEC:BVLX TECHNOLOGY >",
						"PSIUTL:BVLX UTILITIES >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"BVLX:BVLX INDEX",
						"PSICCG:BVLX CONS GOODS",
						"PSICSV:BVLX CONS SERVICE",
						"PSIFIN:BVLX FINANCIALS",
						"PSIGEN:BVLX INDUSTRIALS",
						"PSIIND:BVLX BASIC MATERIALS",
						"PSINSV:BVLX TELECOM",
						"PSITEC:BVLX TECHNOLOGY",
						"PSIUTL:BVLX UTILITIES"
					});
					ok = true;
				}
			}

			else if (country == "KUWAIT EQ >" || country == "KUWAIT >" || country == "Kuwait")
			{
				if (groupSymbol == "SECTMIND")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"SECTMIND:SECTMIND INDEX",
						"KWTBANKC:SECTMIND BANKS >",
						"KWTGOODC:SECTMIND CONS GOODS >",
						"KWTSVCC:SECTMIND CONS SERVICE >",
						"KWTFINC:SECTMIND FINANCIAL >",
						"KWTHLTHC:SECTMIND HEALTH CARE >",
						"KWTINDC:SECTMIND INDUSTRIAL >",
						"KWTINSC:SECTMIND INSURANCE >",
						"KWTINVC:SECTMIND INVEST INSTRU >",
						"KWTMATC:SECTMIND MATERIALS >",
						"KWTOILC:SECTMIND OIL & GAS >",
						"KWTPRLLC:SECTMIND PARALLEL MKT >",
						"KWTRESTC:SECTMIND REAL ESTATE >",
						"KWTTECHC:SECTMIND TECHNOLOGY >",
						"KWTTELC:SECTMIND TELECOM >",
						"KWTUTILC:SECTMIND UTIL >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"SECTMIND:SECTMIND INDEX",
						"KWTBANKC:SECTMIND BANKS",
						"KWTGOODC:SECTMIND CONS GOODS",
						"KWTSVCC:SECTMIND CONS SERVICE",
						"KWTFINC:SECTMIND FINANCIAL",
						"KWTHLTHC:SECTMIND HEALTH CARE",
						"KWTINDC:SECTMIND INDUSTRIAL",
						"KWTINSC:SECTMIND INSURANCE",
						"KWTINVC:SECTMIND INVEST INSTRU",
						"KWTMATC:SECTMIND MATERIALS",
						"KWTOILC:SECTMIND OIL & GAS",
						"KWTPRLLC:SECTMIND PARALLEL MKT",
						"KWTRESTC:SECTMIND REAL ESTATE",
						"KWTTECHC:SECTMIND TECHNOLOGY",
						"KWTTELC:SECTMIND TELECOM",
						"KWTUTILC:SECTMIND UTIL"
					});
					ok = true;
				}
			}

			else if (country == "QATAR >" || country == "Qatar")
			{
				if (groupSymbol == "DSM")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"DSM:DSM INDEX",
						"DSMBNKI:DSM BANKING >",
						"DSMINSI:DSM INSURANCE >",
						"DSMINDI:DSM INDUSTRIAL >",
						"DSMSRVI:DSM SERVICES >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"DSM:DSM INDEX",
						"DSMBNKI:DSM BANKING",
						"DSMINSI:DSM INSURANCE",
						"DSMINDI:DSM INDUSTRIAL",
						"DSMSRVI:DSM SERVICES"
					});
					ok = true;
				}
			}

			else if (country == "SAUDI ARABIA EQ >" || country == "SAUDI ARABIA >" || country == "SaudiArabia")
			{
				if (groupSymbol == "SASEIDX")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"SASEIDX:SASEIDX INDEX",
						"SASEAGRI:SASEIDX AGRICULTURE >",
						"SASEBNK:SASEIDX BANK >",
						"SASEBULD:SASEIDX BUILD >",
						"SASECEM:SASEIDX CEMENT >",
						"SASEENER:SASEIDX ENERGY >",
						"SASEHOTE:SASEIDX HOTEL >",
						"SASEINDI:SASEIDX INDUSTRIAL >",
						"SASEINS:SASEIDX INSURANCE >",
						"SASEMEDI:SASEIDX MEDIA >",
						"SASEMINV:SASEIDX MULTILINE INS >",
						"SASEPETR:SASEIDX PETRO >",
						"SASEREAL:SASEIDX REALES >",
						"SASERETL:SASEIDX RETAIL >",
						"SASETEL:SASEIDX TELECOM >",
						"SASETRAN:SASEIDX TRANSPORTATION >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"SASEIDX:SASEIDX INDEX",
						"SASEAGRI:SASEIDX AGRICULTURE",
						"SASEBNK:SASEIDX BANK",
						"SASEBULD:SASEIDX BUILD",
						"SASECEM:SASEIDX CEMENT",
						"SASEENER:SASEIDX ENERGY",
						"SASEHOTE:SASEIDX HOTEL",
						"SASEINDI:SASEIDX INDUSTRIAL",
						"SASEINS:SASEIDX INSURANCE",
						"SASEMEDI:SASEIDX MEDIA",
						"SASEMINV:SASEIDX MULTILINE INS",
						"SASEPETR:SASEIDX PETRO",
						"SASEREAL:SASEIDX REALES",
						"SASERETL:SASEIDX RETAIL",
						"SASETEL:SASEIDX TELECOM",
						"SASETRAN:SASEIDX TRANSPORTATION"
					});
					ok = true;
				}
			}

			else if (country == "UAE EQ >" || country == "UAE >" || country == "UAE")
			{
				if (groupSymbol == "DFMGI")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"DFMGI:DFMGI INDEX",
						"DFIBANK:DFMGI BANKS >",
						"DFCONSTP:DFMGI CONSUMER STAPLES >",
						"DFIINSU:DFMGI INSURANCE >",
						"DFINVEST:DFMGI FINANCIAL INV >",
						"DFMATERL:DFMGI MATERIALS >",
						"DFREALTY:DFMGI REAL ESTATE >",
						"DFTELCO:DFMGI TELECOM >",
						"DFTRANS:DFMGI TRANSPORT >",
						"DFUTIL:DFMGI UTILITIES >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"DFMGI:DFMGI INDEX",
						"DFIBANK:DFMGI BANKS",
						"DFCONSTP:DFMGI CONSUMER STAPLES",
						"DFIINSU:DFMGI INSURANCE",
						"DFINVEST:DFMGI FINANCIAL INV",
						"DFMATERL:DFMGI MATERIALS",
						"DFREALTY:DFMGI REAL ESTATE",
						"DFTELCO:DFMGI TELECOM",
						"DFTRANS:DFMGI TRANSPORT",
						"DFUTIL:DFMGI UTILITIES"
					});
					ok = true;
				}

				else if (groupSymbol == "ADSMI")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"ADSMI:ADSMI INDEX",
						"ADBF:ADSMI BANKS >",
						"ADCM:ADSMI CONSUMER STAPLES >",
						"ADEG:ADSMI ENERGY >",
						"ADFS:ADSMI FIN SVS & INV >",
						"ADCT:ADSMI INDUSTRIAL >",
						"ADII:ADSMI INSURANCE >",
						"ADRE:ADSMI REAL ESTATE >",
						"ADID:ADSMI SERVICES >",
						"ADTL:ADSMI TELECOM >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"ADSMI:ADSMI INDEX",
						"ADBF:ADSMI BANKS",
						"ADCM:ADSMI CONSUMER STAPLES",
						"ADEG:ADSMI ENERGY",
						"ADFS:ADSMI FIN SVS & INV",
						"ADCT:ADSMI INDUSTRIAL",
						"ADII:ADSMI INSURANCE",
						"ADRE:ADSMI REAL ESTATE",
						"ADID:ADSMI SERVICES",
						"ADTL:ADSMI TELECOM"
					});
					ok = true;
				}
			}

			else if (country == "HONG KONG EQ >" || country == "HONG KONG >" || country == "HongKong")
			{
				if (groupSymbol == "HSI")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"HSI:HSI INDEX",
						"HSC:HSI COMM INDU >",
						"HSF:HSI FINANCE >",
						"HSP:HSI PROPERTY >",
						"HSU:HSI UTILITIES >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"HSI:HSI INDEX",
						"HSC:HSI COMM INDU",
						"HSF:HSI FINANCE",
						"HSP:HSI PROPERTY",
						"HSU:HSI UTILITIES"
					});
					ok = true;
				}

				else if (groupSymbol == "HSCI")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"HSCI:HSCI INDEX",
						"HSCICG:HSCI CONS GOODS >",
						"HSCICO:HSCI CONGLOMERATE >",
						"HSCIEN:HSCI ENERGY >",
						"HSCIFN:HSCI FINANCIAL >",
						"HSCIIN:HSCI INDUSTRIAL GOODS >",
						"HSCIIT:HSCI INFO TECH >",
						"HSCIMT:HSCI MATERIALS >",
						"HSCIPC:HSCI PROP & CONST >",
						"HSCISV:HSCI SERVICES >",
						"HSCITC:HSCI TELECOM >",
						"HSCIUT:HSCI UTILITIES >"

					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"HSCI:HSCI INDEX",
						"HSCICG:HSCI CONS GOODS",
						"HSCICO:HSCI CONGLOMERATE",
						"HSCIEN:HSCI ENERGY",
						"HSCIFN:HSCI FINANCIAL",
						"HSCIIN:HSCI INDUSTRIAL GOODS",
						"HSCIIT:HSCI INFO TECH",
						"HSCIMT:HSCI MATERIALS",
						"HSCIPC:HSCI PROP & CONST",
						"HSCISV:HSCI SERVICES",
						"HSCITC:HSCI TELECOM",
						"HSCIUT:HSCI UTILITIES"
					});
					ok = true;
				}
			}

			else if (country == "AUSTRALIA EQ >" || country == "AUSTRALIA >" || country == "Australia")
			{
				if (groupSymbol == "AS51")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"AS51:AS51 INDEX",
						"AS51COND:AS51 CONS DISC >",
						"AS51CONS:AS51 CONS STAPLES >",
						"AS51ENGY:AS51 ENERGY >",
						"AS51FIN:AS51 FINANCIAL >",
						"AS51HC:AS51 HEALTH CARE >",
						"AS51INDU:AS51 INDUSTRIAL >",
						"AS51IT:AS51 INFO TECH >",
						"AS51MATL:AS51 MATERIALS >",
						"AS51TELE:AS51 TELECOM >",
						"AS51UTIL:AS51 UTILITIES >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"AS51:AS51 INDEX",
						"AS51COND:AS51 CONS DISC",
						"AS51CONS:AS51 CONS STAPLES",
						"AS51ENGY:AS51 ENERGY",
						"AS51FIN:AS51 FINANCIAL",
						"AS51HC:AS51 HEALTH CARE",
						"AS51INDU:AS51 INDUSTRIAL",
						"AS51IT:AS51 INFO TECH",
						"AS51MATL:AS51 MATERIALS",
						"AS51TELE:AS51 TELECOM",
						"AS51UTIL:AS51 UTILITIES"
					});
					ok = true;
				}

				else if (groupSymbol == "AS52")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"AS52:AS52 INDEX",
						"AS52COND:AS52 CONS DISC >",
						"AS52CONS:AS52 CONS STAPLES >",
						"AS52ENGY:AS52 ENERGY >",
						"AS52FIN:AS52 FINANCIAL >",
						"AS52HC:AS52 HEALTH CARE >",
						"AS52INDU:AS52 INDUSTRIAL >",
						"AS52IT:AS52 INFO TECH >",
						"AS52MATL:AS52 MATERIALS >",
						"AS52TELE:AS52 TELECOM >",
						"AS52UTIL:AS52 UTILITIES >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"AS52:AS52 INDEX",
						"AS52COND:AS52 CONS DISC",
						"AS52CONS:AS52 CONS STAPLES",
						"AS52ENGY:AS52 ENERGY",
						"AS52FIN:AS52 FINANCIAL",
						"AS52HC:AS52 HEALTH CARE",
						"AS52INDU:AS52 INDUSTRIAL",
						"AS52IT:AS52 INFO TECH",
						"AS52MATL:AS52 MATERIALS",
						"AS52TELE:AS52 TELECOM",
						"AS52UTIL:AS52 UTILITIES"
					});
					ok = true;
				}
			}

			else if (country == "CHINA EQ >" || country == "CHINA >" || country == "China")
			{
				if (groupSymbol == "SHCOMP")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"SHCOMP:SHCOMP INDEX",
						"SHCOMM:SHCOMP COMMERCIAL >",
						"SHCNG:SHCOMP CONGLOMERATE >",
						"SHINDU:SHCOMP INDUSTRY >",
						"SHPROP:SHCOMP PROPERTY >",
						"SHUTIL:SHCOMP UTILITY >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"SHCOMP:SHCOMP INDEX",
						"SHCOMM:SHCOMP COMMERCIAL",
						"SHCNG:SHCOMP CONGLOMERATE",
						"SHINDU:SHCOMP INDUSTRY",
						"SHPROP:SHCOMP PROPERTY",
						"SHUTIL:SHCOMP UTILITY"
					});
					ok = true;
				}

				else if (groupSymbol == "SHSZ300")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"SHSZ300:SHSZ300 INDEX",
						"SZ399911:SHSZ300CONS DISCRETION >",
						"SZ399912:SHSZ300CONS STAPLES >",
						"SZ399908:SHSZ300ENERGY >",
						"SZ399914:SHSZ300FINANCIAL >",
						"SZ399913:SHSZ300HEALTHCARE >",
						"SZ399910:SHSZ300INDUSTRIAL >",
						"SZ399915:SHSZ300INFO TECH >",
						"SZ399909:SHSZ300MATERIAL >",
						"SZ399916:SHSZ300TELECOM >",
						"SZ399917:SHSZ300UTILITIES >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"SHSZ300:SHSZ300 INDEX",
						"SZ399911:SHSZ300CONS DISCRETION",
						"SZ399912:SHSZ300CONS STAPLES",
						"SZ399908:SHSZ300ENERGY",
						"SZ399914:SHSZ300FINANCIAL",
						"SZ399913:SHSZ300HEALTHCARE",
						"SZ399910:SHSZ300INDUSTRIAL",
						"SZ399915:SHSZ300INFO TECH",
						"SZ399909:SHSZ300MATERIAL",
						"SZ399916:SHSZ300TELECOM",
						"SZ399917:SHSZ300UTILITIES"
					});
					ok = true;
				}
			}

			else if (country == "SOUTH AFRICA EQ >" || country == "SOUTH AFRICA >" || country == "SouthAfrica")
			{
				if (groupSymbol == "JALSH")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"JALSH:JALSH INDEX",
						"JAUTO:JALSH AUTOMOBILE >",
						"JBNKS:JALSH BANKS >",
						"JBEVE:JALSH BEVERAGES >",
						"JCHEM:JALSH CHEMICAL >",
						"JCBDM:JALSH CONS & MAT >",
						"JEEEQ:JALSH ELECT EQUIP >",
						"JINVC:JALSH EQY INV >",
						"JFDRT:JALSH FOOD & DRUG >",
						"JTLSV:JALSH FIX LINE TELECOM >",
						"JFRPP:JALSH FOREST & PAPER >",
						"JSPOF:JALSH GEN FINANCE >",
						"JDIND:JALSH GEN INDUSTRY >",
						"JGENR:JALSH GEN RETAIL >",
						"JHLTH:JALSH HC EQUIP >",
						"JFPPS:JALSH HC EQ SRV >",
						"JHOGD:JALSH HOUS GDS >",
						"JSTMT:JALSH INDUTRIAL METAL >",
						"JTRNS:JALSH INDUSTRIAL TRAN >",
						"JEGMC:JALSH INDU ENG >",
						"JREDVSV:JALSH INVEST SRV >",
						"JLFEA:JALSH LIFE ASR >",
						"JMDPT:JALSH MEDIA >",
						"JMNNG:JALSH MINING >",
						"JMOTE:JALSH MOBILE TELE >",
						"JINSR:JALSH NLF INS >",
						"JOLGS:JALSH OIL PRODUCT >",
						"JPBIO:JALSH PHARM & BIO >",
						"JPCHP:JALSH PERSONAL GDS >",
						"JREITS:JALSH REITS >",
						"JSSEV:JALSH SUP SVC >",
						"JLEHT:JALSH TRAVEL & LEISURE >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"JALSH:JALSH INDEX",
						"JAUTO:JALSH AUTOMOBILE",
						"JBNKS:JALSH BANKS",
						"JBEVE:JALSH BEVERAGES",
						"JCHEM:JALSH CHEMICAL",
						"JCBDM:JALSH CONS & MAT",
						"JEEEQ:JALSH ELECT EQUIP",
						"JINVC:JALSH EQY INV",
						"JFDRT:JALSH FOOD & DRUG",
						"JTLSV:JALSH FIX LINE TELECOM",
						"JFRPP:JALSH FOREST & PAPER",
						"JSPOF:JALSH GEN FINANCE",
						"JDIND:JALSH GEN INDUSTRY",
						"JGENR:JALSH GEN RETAIL",
						"JHLTH:JALSH HC EQUIP",
						"JFPPS:JALSH HC EQ SRV",
						"JHOGD:JALSH HOUS GDS",
						"JSTMT:JALSH INDUTRIAL METAL",
						"JTRNS:JALSH INDUSTRIAL TRAN",
						"JEGMC:JALSH INDU ENG",
						"JREDVSV:JALSH INVEST SRV",
						"JLFEA:JALSH LIFE ASR",
						"JMDPT:JALSH MEDIA",
						"JMNNG:JALSH MINING",
						"JMOTE:JALSH MOBILE TELE",
						"JINSR:JALSH NLF INS",
						"JOLGS:JALSH OIL PRODUCT",
						"JPBIO:JALSH PHARM & BIO",
						"JPCHP:JALSH PERSONAL GDS",
						"JREITS:JALSH REITS",
						"JSSEV:JALSH SUP SVC",
						"JLEHT:JALSH TRAVEL & LEISURE"
					});
					ok = true;
				}
			}

			else if (country == "TAIWAN EQ >" || country == "TAIWAN >" || country == "Taiwan")
			{
				if (groupSymbol == "TWSE")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"TWSE:TWSE INDEX",
						"TWSEAUTO:TWSE AUTO >",
						"TWSEBKI:TWSE BK/FIN/INS >",
						"TWSEBMC:TWSE BIO & MED >",
						"TWSECEM:TWSE CEMENT >",
						"TWSECHI:TWSE CHEMICAL >",
						"TWSECII:TWSE COMM & INTERNET >",
						"TWSECON:TWSE CONSTRUCTION >",
						"TWSECPE:TWSE COMP EQUIP >",
						"TWSEDEPT:TWSE DEPT STORES >",
						"TWSEEAW:TWSE ELEC APPLIANCE >",
						"TWSEECI:TWSE ELECTRONIC PARTS >",
						"TWSEEPD:TWSE ELECTRONIC PROD >",
						"TWSEFOOD:TWSE FOOD >",
						"TWSEGLP:TWSE GLASS >",
						"TWSEISI:TWSE INFO SRV >",
						"TWSEMACH:TWSE MACHINERY >",
						"TWSEOEG:TWSE OIL & GAS >",
						"TWSEOEI:TWSE OTHER ELECTORNIC >",
						"TWSEOPE:TWSE OPTOELECTRICAL >",
						"TWSEOTHR:TWSE OTHER >",
						"TWSEPLAS:TWSE PLASTIC >",
						"TWSEPP:TWSE PULP/PAPER >",
						"TWSERUB:TWSE RUBBER >",
						"TWSESCI:TWSE SEMICONDUCTOR >",
						"TWSESTEE:TWSE STEEL >",
						"TWSETEXT:TWSE TEXTILES >",
						"TWSETOUR:TWSE TOURIST >",
						"TWSETRAN:TWSE TRANSPORT >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"TWSE:TWSE INDEX",
						"TWSEAUTO:TWSE AUTO",
						"TWSEBKI:TWSE BK/FIN/INS",
						"TWSEBMC:TWSE BIO & MED",
						"TWSECEM:TWSE CEMENT",
						"TWSECHI:TWSE CHEMICAL",
						"TWSECII:TWSE COMM & INTERNET",
						"TWSECON:TWSE CONSTRUCTION",
						"TWSECPE:TWSE COMP EQUIP",
						"TWSEDEPT:TWSE DEPT STORES",
						"TWSEEAW:TWSE ELEC APPLIANCE",
						"TWSEECI:TWSE ELECTRONIC PARTS",
						"TWSEEPD:TWSE ELECTRONIC PROD",
						"TWSEFOOD:TWSE FOOD",
						"TWSEGLP:TWSE GLASS",
						"TWSEISI:TWSE INFO SRV",
						"TWSEMACH:TWSE MACHINERY",
						"TWSEOEG:TWSE OIL & GAS",
						"TWSEOEI:TWSE OTHER ELECTORNIC",
						"TWSEOPE:TWSE OPTOELECTRICAL",
						"TWSEOTHR:TWSE OTHER",
						"TWSEPLAS:TWSE PLASTIC",
						"TWSEPP:TWSE PULP/PAPER",
						"TWSERUB:TWSE RUBBER",
						"TWSESCI:TWSE SEMICONDUCTOR",
						"TWSESTEE:TWSE STEEL",
						"TWSETEXT:TWSE TEXTILES",
						"TWSETOUR:TWSE TOURIST",
						"TWSETRAN:TWSE TRANSPORT"
					});
					ok = true;
				}
			}

			else if (country == "SOUTH KOREA EQ >" || country == "SOUTH KOREA >" || country == "SouthKorea")
			{
				if (groupSymbol == "KOSPI")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"KOSPI:KOSPI INDEX",
						"KOSPCHEM:KOSPI CHEMICAL PROD >",
						"KOSPCOMM:KOSPI COMMUNICATION >",
						"KOSPCONS:KOSPI CONSTRUCTION >",
						"KOSPELEC:KOSPI ELECTRIC & ELEC EQ >",
						"KOSPELGS:KOSPI ELECT & GAS >",
						"KOSPFBEV:KOSPI FOOD & BEVERAGE >",
						"KOSPFIN:KOSPI FINANCIAL >",
						"KOSPBMET:KOSPI IRON & METAL >",
						"KOSPMACH:KOSPI MACHINERY >",
						"KOSPMED:KOSPI MEDICINE >",
						"KOSPMDEQ:KOSPI MEDICAL PREC >",
						"KOSPMISC:KOSPI MISCELLANEOUS >",
						"KOSPNMET:KOSPI NONMETALLIC MINRL >",
						"KOSPPPRD:KOSPI PAPER & PAPER PRD >",
						"KOSPSERV:KOSPI SERVICES >",
						"KOSPTXAP:KOSPI TEXTILE & APPAREL >",
						"KOSPTREQ:KOSPI TRANS EQUIP >",
						"KOSPTRAN:KOSPI TRANSHPORT & STRGE >",
						"KOSPWHOL:KOSPI WHOLESALE TRADE >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"KOSPI:KOSPI INDEX",
						"KOSPCHEM:KOSPI CHEMICAL PROD",
						"KOSPCOMM:KOSPI COMMUNICATION",
						"KOSPCONS:KOSPI CONSTRUCTION",
						"KOSPELEC:KOSPI ELECTRIC & ELEC EQ",
						"KOSPELGS:KOSPI ELECT & GAS",
						"KOSPFBEV:KOSPI FOOD & BEVERAGE",
						"KOSPFIN:KOSPI FINANCIAL",
						"KOSPBMET:KOSPI IRON & METAL",
						"KOSPMACH:KOSPI MACHINERY",
						"KOSPMED:KOSPI MEDICINE",
						"KOSPMDEQ:KOSPI MEDICAL PREC",
						"KOSPMISC:KOSPI MISCELLANEOUS",
						"KOSPNMET:KOSPI NONMETALLIC MINRL",
						"KOSPPPRD:KOSPI PAPER & PAPER PRD",
						"KOSPSERV:KOSPI SERVICES",
						"KOSPTXAP:KOSPI TEXTILE & APPAREL",
						"KOSPTREQ:KOSPI TRANS EQUIP",
						"KOSPTRAN:KOSPI TRANSHPORT & STRGE",
						"KOSPWHOL:KOSPI WHOLESALE TRADE"
					});
					ok = true;
				}

				else if (groupSymbol == "KOSDAQ")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"KOSDAQ:KOSDAQ INDEX",
						"KOSCNST:KOSDAQ CONSTRUCTION >",
						"KOSDIST:KOSDAQ DISTRIB SRVC >",
						"KOSFINC:KOSDAQ FINANCE >",
						"KOSITCP:KOSDAQ IT COMPOSITE >",
						"KOSMANU:KOSDAQ MANUFACTURING >",
						"KOSOTHR:KOSDAQ OTHERS >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"KOSDAQ:KOSDAQ INDEX",
						"KOSCNST:KOSDAQ CONSTRUCTION",
						"KOSDIST:KOSDAQ DISTRIB SRVC",
						"KOSFINC:KOSDAQ FINANCE",
						"KOSITCP:KOSDAQ IT COMPOSITE",
						"KOSMANU:KOSDAQ MANUFACTURING",
						"KOSOTHR:KOSDAQ OTHERS"
					});
					ok = true;
				}
			}

			else if (country == "JAPAN EQ >" || country == "JAPAN >" || country == "Japan")
			{
				if (groupSymbol == "TPX")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"TPX:TPX INDEX",
						"TPNBNK:TPX BANKS >",
						"TPNCHM:TPX CHEMICALS >",
						"TPCONT:TPX CONSTRUCTION >",
						"TPELMH:TPX ELECTRIC APPL >",
						"TPELEC:TPX ELEC POWR & GAS >",
						"TPFISH:TPX FISH/AGR/FRST >",
						"TPFOOD:TPX FOODS >",
						"TPGLAS:TPX GLASS & CRMC >",
						"TPCOMM:TPX INFO & COMM >",
						"TPINSU:TPX INSURANCE >",
						"TPIRON:TPX IRON & STEEL >",
						"TPLAND:TPX LAND TRANSPORT >",
						"TPMACH:TPX MACHINERY >",
						"TPMART:TPX MARITIME TRAN >",
						"TPMETL:TPX METAL >",
						"TPMINN:TPX MINING >",
						"TPNMET:TPX NONFER METAL >",
						"TPFINC:TPX OTHER FINC BUS >",
						"TPPROD:TPX OTHER PRODUCTS >",
						"TPPHRM:TPX PHARMACEUTICAL >",
						"TPPAPR:TPX PULP & PAPER >",
						"TPPREC:TPX PREC INSTRUMENT >",
						"TPREAL:TPX REAL ESTATE >",
						"TPRETL:TPX RETAIL TRADE >",
						"TPRUBB:TPX RUBBER PRODUCTS >",
						"TPSECR:TPX SEC & CMDTY FUTR >",
						"TPSERV:TPX SERVICES >",
						"TPTEXT:TPX TXTL & APPRL >",
						"TPTRAN:TPX TRANSPORT EQUIP >",
						"TPWARE:TPX WARE&HARB TRNS >",
						"TPWSAL:TPX WHOLESALE TRADE >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"TPX:TPX INDEX",
						"TPNBNK:TPX BANKS",
						"TPNCHM:TPX CHEMICALS",
						"TPCONT:TPX CONSTRUCTION",
						"TPELMH:TPX ELECTRIC APPL",
						"TPELEC:TPX ELEC POWR & GAS",
						"TPFISH:TPX FISH/AGR/FRST",
						"TPFOOD:TPX FOODS",
						"TPGLAS:TPX GLASS & CRMC",
						"TPCOMM:TPX INFO & COMM",
						"TPINSU:TPX INSURANCE",
						"TPIRON:TPX IRON & STEEL",
						"TPLAND:TPX LAND TRANSPORT",
						"TPMACH:TPX MACHINERY",
						"TPMART:TPX MARITIME TRAN",
						"TPMETL:TPX METAL",
						"TPMINN:TPX MINING",
						"TPNMET:TPX NONFER METAL",
						"TPFINC:TPX OTHER FINC BUS",
						"TPPROD:TPX OTHER PRODUCTS",
						"TPPHRM:TPX PHARMACEUTICAL",
						"TPPAPR:TPX PULP & PAPER",
						"TPPREC:TPX PREC INSTRUMENT",
						"TPREAL:TPX REAL ESTATE",
						"TPRETL:TPX RETAIL TRADE",
						"TPRUBB:TPX RUBBER PRODUCTS",
						"TPSECR:TPX SEC & CMDTY FUTR",
						"TPSERV:TPX SERVICES",
						"TPTEXT:TPX TXTL & APPRL",
						"TPTRAN:TPX TRANSPORT EQUIP",
						"TPWARE:TPX WARE&HARB TRNS",
						"TPWSAL:TPX WHOLESALE TRADE"
					});
					ok = true;
				}
			}

			else if (country == "NEW ZEALAND EQ >" || country == "NEW ZEALAND >" || country == "NewZealand")
			{
				if (groupSymbol == "NZSE")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"NZSE:NZSE INDEX",
						"NZAGRI:NZSE AGRI & FISH >",
						"NZBLDM:NZSE BUILDING >",
						"NZCSMR:NZSE CONSUMER >",
						"NZENRG:NZSE ENERGY DIST >",
						"NZFINC:NZSE FINANCE >",
						"NZFOOD:NZSE FOOD & BEV >",
						"NZFRST:NZSE FORESTRY & PROD >",
						"NZINTD:NZSE INTER & DURABLES >",
						"NZINVS:NZSE INVESTMENT >",
						"NZLEIS:NZSE LEISURE & TOURISM >",
						"NZMEDI:NZSE MEDIA & TELECOM >",
						"NZMINE:NZSE MINING >",
						"NZPORT:NZSE PORTS >",
						"NZPROP:NZSE PROPERTY >",
						"NZXTS:NZSE SCI TECH CAPITAL >",
						"NZTEXT:NZSE TEXTILES & APPR >",
						"NZTRAN:NZSE TRANSPORT >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"NZSE:NZSE INDEX",
						"NZAGRI:NZSE AGRI & FISH",
						"NZBLDM:NZSE BUILDING",
						"NZCSMR:NZSE CONSUMER",
						"NZENRG:NZSE ENERGY DIST",
						"NZFINC:NZSE FINANCE",
						"NZFOOD:NZSE FOOD & BEV",
						"NZFRST:NZSE FORESTRY & PROD",
						"NZINTD:NZSE INTER & DURABLES",
						"NZINVS:NZSE INVESTMENT",
						"NZLEIS:NZSE LEISURE & TOURISM",
						"NZMEDI:NZSE MEDIA & TELECOM",
						"NZMINE:NZSE MINING",
						"NZPORT:NZSE PORTS",
						"NZPROP:NZSE PROPERTY",
						"NZXTS:NZSE SCI TECH CAPITAL",
						"NZTEXT:NZSE TEXTILES & APPR",
						"NZTRAN:NZSE TRANSPORT"
					});
					ok = true;
				}
			}

			else if (country == "THAILAND EQ >" || country == "THAILAND >" || country == "Thailand")
			{
				if (groupSymbol == "SET")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"SET:SET INDEX",
						"SETAGRI:SET AGRI >",
						"SETAUTO:SET AUTOMOTIVE >",
						"SETBANK:SET BANKING >",
						"SETCOM:SET COMMERCE >",
						"SETCOMMT:SET CONSTR MATERIAL >",
						"SETETRON:SET ELECTRONIC COMP >",
						"SETENERG:SET ENERGY & UTIL >",
						"SETFASH:SET FASHION >",
						"SETFIN:SET FINANCE >",
						"SETFOOD:SET FOOD & BEV >",
						"SETHELTH:SET HEALTH CARE >",
						"SETHHOLD:SET HOME & OFFICE PRD >",
						"SETCOMUN:SET INFO & COMM >",
						"SETIMM:SET INDU MAT & MACH >",
						"SETINS:SET INSURANCE >",
						"SETENTER:SET MEDIA & PUBLISH >",
						"SETMINE:SET MINING >",
						"SETPKG:SET PACKAGING >",
						"SETPERS:SET PERSONAL PROD >",
						"SETPETRO:SET PETRO CHEMICAL >",
						"SETPAPER:SET PAPERr & PRINT >",
						"SETPROP:SET PROPERTY DEV >",
						"SETPROF:SET PROFESSIONAL SRVv >",
						"SETPFUND:SET PROPERTY FUND >",
						"SETSTEEL:SET STEEL >",
						"SETHOT:SET TOURISM & LEISURE >",
						"SETTRANS:SET TRANSPORT & LOGIST >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"SET:SET INDEX",
						"SETAGRI:SET AGRI",
						"SETAUTO:SET AUTOMOTIVE",
						"SETBANK:SET BANKING",
						"SETCOM:SET COMMERCE",
						"SETCOMMT:SET CONSTR MATERIAL",
						"SETETRON:SET ELECTRONIC COMP",
						"SETENERG:SET ENERGY & UTIL",
						"SETFASH:SET FASHION",
						"SETFIN:SET FINANCE",
						"SETFOOD:SET FOOD & BEV",
						"SETHELTH:SET HEALTH CARE",
						"SETHHOLD:SET HOME & OFFICE PRD",
						"SETCOMUN:SET INFO & COMM",
						"SETIMM:SET INDU MAT & MACH",
						"SETINS:SET INSURANCE",
						"SETENTER:SET MEDIA & PUBLISH",
						"SETMINE:SET MINING",
						"SETPKG:SET PACKAGING",
						"SETPERS:SET PERSONAL PROD",
						"SETPETRO:SET PETRO CHEMICAL",
						"SETPAPER:SET PAPERr & PRINT",
						"SETPROP:SET PROPERTY DEV",
						"SETPROF:SET PROFESSIONAL SRVv",
						"SETPFUND:SET PROPERTY FUND",
						"SETSTEEL:SET STEEL",
						"SETHOT:SET TOURISM & LEISURE",
						"SETTRANS:SET TRANSPORT & LOGIST"
					});
					ok = true;
				}
			}

			else if (country == "SINGAPORE EQ >" || country == "SINGAPORE >" || country == "Singapore")
			{
				if (groupSymbol == "FSTAS")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"FSTAS:FSTAS INDEX",
						"FSTBM:FSTAS BASIC MATERIAL >",
						"FSTCG:FSTAS CONSUMER GOODS >",
						"FSTCS:FSTAS CONSUMER SRV >",
						"FSTFN:FSTAS FINANCIALS >",
						"FSTHC:FSTAS HEALTH CARE >",
						"FSTIN:FSTAS INDUSTRIALS >",
						"FSTOG:FSTAS OIL & GAS >",
						"FSTTC:FSTAS TELECOM >",
						"FSTTG:FSTAS TECHNOLOGY >",
						"FSTUT:FSTAS UTILITIES >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"FSTAS:FSTAS INDEX",
						"FSTBM:FSTAS BASIC MATERIAL",
						"FSTCG:FSTAS CONSUMER GOODS",
						"FSTCS:FSTAS CONSUMER SRV",
						"FSTFN:FSTAS FINANCIALS",
						"FSTHC:FSTAS HEALTH CARE",
						"FSTIN:FSTAS INDUSTRIALS",
						"FSTOG:FSTAS OIL & GAS",
						"FSTTC:FSTAS TELECOM",
						"FSTTG:FSTAS TECHNOLOGY",
						"FSTUT:FSTAS UTILITIES"
					});
					ok = true;
				}
			}

			else if (country == "INDONESIA EQ >" || country == "INDONESIA >" || country == "Indonesia")
			{
				if (groupSymbol == "JCI")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
						"JCI:JCI INDEX",
						"JAKAGRI:JCI AGRI >",
						"JAKBIND:JCI BASIC & CHEMICAL IND >",
						"JAKPROP:JCI CONST PROP & RE >",
						"JAKCONS:JCI CONSUMER GOODS >",
						"JAKFIN:JCI FINANCE >",
						"JAKINFR:JCI INFRA UTILITY & TRANSPORT >",
						"JAKMINE:JCI MINING >",
						"JAKMIND:JCI MISC INDUSTRIES >",
						"JAKTRAD:JCI TRADE & SRV >"
					});

					else setNavigation(panel, mouseDownEvent, new string[] {
						"JCI:JCI INDEX",
						"JAKAGRI:JCI AGRI",
						"JAKBIND:JCI BASIC & CHEMICAL IND",
						"JAKPROP:JCI CONST PROP & RE",
						"JAKCONS:JCI CONSUMER GOODS",
						"JAKFIN:JCI FINANCE",
						"JAKINFR:JCI INFRA UTILITY & TRANSPORT",
						"JAKMINE:JCI MINING",
						"JAKMIND:JCI MISC INDUSTRIES",
						"JAKTRAD:JCI TRADE & SRV"
					});
					ok = true;
				}
			}
			return ok;
		}

		public bool setScenarioNavigation(string[] level, StackPanel panel, MouseButtonEventHandler mouseDownEvent)
		{
			panel.Children.Clear();

			bool ok = true;

			var level1 = level[0];
			var level2 = level[1];
			var level3 = level[2];
			var level4 = level[3];

			if (level1 == "")
			{
				setNavigation(panel, mouseDownEvent, new string[] { "CLOSE >", " ", "OPEN >", " ", "HIGH >", " ", "LOW >" });
				//setNavigationLevel1(panel, mouseDownEvent, new string[] { "CLOSE >", " ", "OPEN >", " ", "HIGH >", " ", "LOW >", " ", "VOLATILITY >" });
			}
			else if (level1 == "CLOSE >")
			{
				if (level2 == "") setNavigation(panel, mouseDownEvent, new string[] { "Cl to Cl >", " ", "Cl to Op >", " ", "Cl to Hi >", " ", "Cl to Lo >" });
				else if (level2 == "Cl to Cl >")
				{
					if (level3 == "") setNavigation(panel, mouseDownEvent, new string[] { "Forecast Cl +1 >" });
					//if (level3 == "") setNavigationLevel1(panel, mouseDownEvent, new string[] { "Forecast Cl +1 >", " ", "Forecast Cl +2 >", " ", "Forecast Cl +3 >", " ", "Forecast Cl +4 >", " ", "Forecast Cl +5 >", " ", "Forecast Cl +6 >", " ", "Forecast Cl +7 >", " ", "Forecast Cl +8 >", " ", "Forecast Cl +9 >" });
					else setNavigation(panel, mouseDownEvent, new string[] { "Reference Cl -1", " ", "Reference Cl -2", " ", "Reference Cl -3", " ", "Reference Cl -4", " ", "Reference Cl -5", " ", "Reference Cl -6", " ", "Reference Cl -7", " ", "Reference Cl -8", " ", "Reference Cl -9" });
				}
				else if (level2 == "Cl to Op >")
				{
					if (level3 == "") setNavigation(panel, mouseDownEvent, new string[] { "Forecast Cl +1 >" });
					//if (level3 == "") setNavigationLevel1(panel, mouseDownEvent, new string[] { "Forecast Cl +1 >", " ", "Forecast Cl +2 >", " ", "Forecast Cl +3 >", " ", "Forecast Cl +4 >", " ", "Forecast Cl +5 >", " ", "Forecast Cl +6 >", " ", "Forecast Cl +7 >", " ", "Forecast Cl +8 >", " ", "Forecast Cl +9 >" });
					else setNavigation(panel, mouseDownEvent, new string[] { "Reference Op -1", " ", "Reference Op -2", " ", "Reference Op -3", " ", "Reference Op -4", " ", "Reference Op -5", " ", "Reference Op -6", " ", "Reference Op -7", " ", "Reference Op -8", " ", "Reference Op -9" });
				}
				else if (level2 == "Cl to Hi >")
				{
					if (level3 == "") setNavigation(panel, mouseDownEvent, new string[] { "Forecast Cl +1 >" });
					//if (level3 == "") setNavigationLevel1(panel, mouseDownEvent, new string[] { "Forecast Cl +1 >", " ", "Forecast Cl +2 >", " ", "Forecast Cl +3 >", " ", "Forecast Cl +4 >", " ", "Forecast Cl +5 >", " ", "Forecast Cl +6 >", " ", "Forecast Cl +7 >", " ", "Forecast Cl +8 >", " ", "Forecast Cl +9 >" });
					else setNavigation(panel, mouseDownEvent, new string[] { "Reference Hi -1", " ", "Reference Hi -2", " ", "Reference Hi -3", " ", "Reference Hi -4", " ", "Reference Hi -5", " ", "Reference Hi -6", " ", "Reference Hi -7", " ", "Reference Hi -8", " ", "Reference Hi -9" });
				}
				else if (level2 == "Cl to Lo >")
				{
					if (level3 == "") setNavigation(panel, mouseDownEvent, new string[] { "Forecast Cl +1 >" });
					//if (level3 == "") setNavigationLevel1(panel, mouseDownEvent, new string[] { "Forecast Cl +1 >", " ", "Forecast Cl +2 >", " ", "Forecast Cl +3 >", " ", "Forecast Cl +4 >", " ", "Forecast Cl +5 >", " ", "Forecast Cl +6 >", " ", "Forecast Cl +7 >", " ", "Forecast Cl +8 >", " ", "Forecast Cl +9 >" });
					else setNavigation(panel, mouseDownEvent, new string[] { "Reference Lo -1", " ", "Reference Lo -2", " ", "Reference Lo -3", " ", "Reference Lo -4", " ", "Reference Lo -5", " ", "Reference Lo -6", " ", "Reference Lo -7", " ", "Reference Lo -8", " ", "Reference Lo -9" });
				}
			}
			else if (level1 == "OPEN >")
			{
				if (level2 == "") setNavigation(panel, mouseDownEvent, new string[] { "Op to Cl >", " ", "Op to Op >", " ", "Op to Hi >", " ", "Op to Lo >" });
				else if (level2 == "Op to Cl >")
				{
					if (level3 == "") setNavigation(panel, mouseDownEvent, new string[] { "Forecast Op +1 >" });
					//if (level3 == "") setNavigationLevel1(panel, mouseDownEvent, new string[] { "Forecast Op +1 >", " ", "Forecast Op +2 >", " ", "Forecast Op +3 >", " ", "Forecast Op +4 >", " ", "Forecast Op +5 >", " ", "Forecast Op +6 >", " ", "Forecast Op +7 >", " ", "Forecast Op +8 >", " ", "Forecast Op +9 >" });
					else setNavigation(panel, mouseDownEvent, new string[] { "Reference Cl -1", " ", "Reference Cl -2", " ", "Reference Cl -3", " ", "Reference Cl -4", " ", "Reference Cl -5", " ", "Reference Cl -6", " ", "Reference Cl -7", " ", "Reference Cl -8", " ", "Reference Cl -9" });
				}
				else if (level2 == "Op to Op >")
				{
					if (level3 == "") setNavigation(panel, mouseDownEvent, new string[] { "Forecast Op +1 >" });
					//if (level3 == "") setNavigationLevel1(panel, mouseDownEvent, new string[] { "Forecast Op +1 >", " ", "Forecast Op +2 >", " ", "Forecast Op +3 >", " ", "Forecast Op +4 >", " ", "Forecast Op +5 >", " ", "Forecast Op +6 >", " ", "Forecast Op +7 >", " ", "Forecast Op +8 >", " ", "Forecast Op +9 >" });
					else setNavigation(panel, mouseDownEvent, new string[] { "Reference Op -1", " ", "Reference Op -2", " ", "Reference Op -3", " ", "Reference Op -4", " ", "Reference Op -5", " ", "Reference Op -6", " ", "Reference Op -7", " ", "Reference Op -8", " ", "Reference Op -9" });
				}
				else if (level2 == "Op to Hi >")
				{
					if (level3 == "") setNavigation(panel, mouseDownEvent, new string[] { "Forecast Op +1 >" });
					//if (level3 == "") setNavigationLevel1(panel, mouseDownEvent, new string[] { "Forecast Op +1 >", " ", "Forecast Op +2 >", " ", "Forecast Op +3 >", " ", "Forecast Op +4 >", " ", "Forecast Op +5 >", " ", "Forecast Op +6 >", " ", "Forecast Op +7 >", " ", "Forecast Op +8 >", " ", "Forecast Op +9 >" });
					else setNavigation(panel, mouseDownEvent, new string[] { "Reference Hi -1", " ", "Reference Hi -2", " ", "Reference Hi -3", " ", "Reference Hi -4", " ", "Reference Hi -5", " ", "Reference Hi -6", " ", "Reference Hi -7", " ", "Reference Hi -8", " ", "Reference Hi -9" });
				}
				else if (level2 == "Op to Lo >")
				{
					if (level3 == "") setNavigation(panel, mouseDownEvent, new string[] { "Forecast Op +1 >" });
					//if (level3 == "") setNavigationLevel1(panel, mouseDownEvent, new string[] { "Forecast Op +1 >", " ", "Forecast Op +2 >", " ", "Forecast Op +3 >", " ", "Forecast Op +4 >", " ", "Forecast Op +5 >", " ", "Forecast Op +6 >", " ", "Forecast Op +7 >", " ", "Forecast Op +8 >", " ", "Forecast Op +9 >" });
					else setNavigation(panel, mouseDownEvent, new string[] { "Reference Lo -1", " ", "Reference Lo -2", " ", "Reference Lo -3", " ", "Reference Lo -4", " ", "Reference Lo -5", " ", "Reference Lo -6", " ", "Reference Lo -7", " ", "Reference Lo -8", " ", "Reference Lo -9" });
				}
			}
			else if (level1 == "HIGH >")
			{
				if (level2 == "") setNavigation(panel, mouseDownEvent, new string[] { "Hi to Cl >", " ", "Hi to Op >", " ", "Hi to Hi >", " ", "Hi to Lo >" });
				else if (level2 == "Hi to Cl >")
				{
					if (level3 == "") setNavigation(panel, mouseDownEvent, new string[] { "Forecast Hi +1 >" });
					//if (level3 == "") setNavigationLevel1(panel, mouseDownEvent, new string[] { "Forecast Hi +1 >", " ", "Forecast Hi +2 >", " ", "Forecast Hi +3 >", " ", "Forecast Hi +4 >", " ", "Forecast Hi +5 >", " ", "Forecast Hi +6 >", " ", "Forecast Hi +7 >", " ", "Forecast Hi +8 >", " ", "Forecast Hi +9 >" });
					else setNavigation(panel, mouseDownEvent, new string[] { "Reference Cl -1", " ", "Reference Cl -2", " ", "Reference Cl -3", " ", "Reference Cl -4", " ", "Reference Cl -5", " ", "Reference Cl -6", " ", "Reference Cl -7", " ", "Reference Cl -8", " ", "Reference Cl -9" });
				}
				else if (level2 == "Hi to Op >")
				{
					if (level3 == "") setNavigation(panel, mouseDownEvent, new string[] { "Forecast Hi +1 >" });
					//if (level3 == "") setNavigationLevel1(panel, mouseDownEvent, new string[] { "Forecast Hi +1 >", " ", "Forecast Hi +2 >", " ", "Forecast Hi +3 >", " ", "Forecast Hi +4 >", " ", "Forecast Hi +5 >", " ", "Forecast Hi +6 >", " ", "Forecast Hi +7 >", " ", "Forecast Hi +8 >", " ", "Forecast Hi +9 >" });
					else setNavigation(panel, mouseDownEvent, new string[] { "Reference Op -1", " ", "Reference Op -2", " ", "Reference Op -3", " ", "Reference Op -4", " ", "Reference Op -5", " ", "Reference Op -6", " ", "Reference Op -7", " ", "Reference Op -8", " ", "Reference Op -9" });
				}
				else if (level2 == "Hi to Hi >")
				{
					if (level3 == "") setNavigation(panel, mouseDownEvent, new string[] { "Forecast Hi +1 >" });
					//if (level3 == "") setNavigationLevel1(panel, mouseDownEvent, new string[] { "Forecast Hi +1 >", " ", "Forecast Hi +2 >", " ", "Forecast Hi +3 >", " ", "Forecast Hi +4 >", " ", "Forecast Hi +5 >", " ", "Forecast Hi +6 >", " ", "Forecast Hi +7 >", " ", "Forecast Hi +8 >", " ", "Forecast Hi +9 >" });
					else setNavigation(panel, mouseDownEvent, new string[] { "Reference Hi -1", " ", "Reference Hi -2", " ", "Reference Hi -3", " ", "Reference Hi -4", " ", "Reference Hi -5", " ", "Reference Hi -6", " ", "Reference Hi -7", " ", "Reference Hi -8", " ", "Reference Hi -9" });
				}
				else if (level2 == "Hi to Lo >")
				{
					if (level3 == "") setNavigation(panel, mouseDownEvent, new string[] { "Forecast Hi +1 >" });
					//if (level3 == "") setNavigationLevel1(panel, mouseDownEvent, new string[] { "Forecast Hi +1 >", " ", "Forecast Hi +2 >", " ", "Forecast Hi +3 >", " ", "Forecast Hi +4 >", " ", "Forecast Hi +5 >", " ", "Forecast Hi +6 >", " ", "Forecast Hi +7 >", " ", "Forecast Hi +8 >", " ", "Forecast Hi +9 >" });
					else setNavigation(panel, mouseDownEvent, new string[] { "Reference Lo -1", " ", "Reference Lo -2", " ", "Reference Lo -3", " ", "Reference Lo -4", " ", "Reference Lo -5", " ", "Reference Lo -6", " ", "Reference Lo -7", " ", "Reference Lo -8", " ", "Reference Lo -9" });
				}
			}
			else if (level1 == "LOW >")
			{
				if (level2 == "") setNavigation(panel, mouseDownEvent, new string[] { "Lo to Cl >", " ", "Lo to Op >", " ", "Lo to Hi >", " ", "Lo to Lo >" });
				else if (level2 == "Lo to Cl >")
				{
					if (level3 == "") setNavigation(panel, mouseDownEvent, new string[] { "Forecast Lo +1 >" });
					//if (level3 == "") setNavigationLevel1(panel, mouseDownEvent, new string[] { "Forecast Lo +1 >", " ", "Forecast Lo +2 >", " ", "Forecast Lo +3 >", " ", "Forecast Lo +4 >", " ", "Forecast Lo +5 >", " ", "Forecast Lo +6 >", " ", "Forecast Lo +7 >", " ", "Forecast Lo +8 >", " ", "Forecast Lo +9 >" });
					else setNavigation(panel, mouseDownEvent, new string[] { "Reference Cl -1", " ", "Reference Cl -2", " ", "Reference Cl -3", " ", "Reference Cl -4", " ", "Reference Cl -5", " ", "Reference Cl -6", " ", "Reference Cl -7", " ", "Reference Cl -8", " ", "Reference Cl -9" });
				}
				else if (level2 == "Lo to Op >")
				{
					if (level3 == "") setNavigation(panel, mouseDownEvent, new string[] { "Forecast Lo +1 >" });
					//if (level3 == "") setNavigationLevel1(panel, mouseDownEvent, new string[] { "Forecast Lo +1 >", " ", "Forecast Lo +2 >", " ", "Forecast Lo +3 >", " ", "Forecast Lo +4 >", " ", "Forecast Lo +5 >", " ", "Forecast Lo +6 >", " ", "Forecast Lo +7 >", " ", "Forecast Lo +8 >", " ", "Forecast Lo +9 >" });
					else setNavigation(panel, mouseDownEvent, new string[] { "Reference Op -1", " ", "Reference Op -2", " ", "Reference Op -3", " ", "Reference Op -4", " ", "Reference Op -5", " ", "Reference Op -6", " ", "Reference Op -7", " ", "Reference Op -8", " ", "Reference Op -9" });
				}
				else if (level2 == "Lo to Hi >")
				{
					if (level3 == "") setNavigation(panel, mouseDownEvent, new string[] { "Forecast Lo +1 >" });
					//if (level3 == "") setNavigationLevel1(panel, mouseDownEvent, new string[] { "Forecast Lo +1 >", " ", "Forecast Lo +2 >", " ", "Forecast Lo +3 >", " ", "Forecast Lo +4 >", " ", "Forecast Lo +5 >", " ", "Forecast Lo +6 >", " ", "Forecast Lo +7 >", " ", "Forecast Lo +8 >", " ", "Forecast Lo +9 >" });
					else setNavigation(panel, mouseDownEvent, new string[] { "Reference Hi -1", " ", "Reference Hi -2", " ", "Reference Hi -3", " ", "Reference Hi -4", " ", "Reference Hi -5", " ", "Reference Hi -6", " ", "Reference Hi -7", " ", "Reference Hi -8", " ", "Reference Hi -9" });
				}
				else if (level2 == "Lo to Lo >")
				{
					if (level3 == "") setNavigation(panel, mouseDownEvent, new string[] { "Forecast Lo +1 >" });
					//if (level3 == "") setNavigationLevel1(panel, mouseDownEvent, new string[] { "Forecast Lo +1 >", " ", "Forecast Lo +2 >", " ", "Forecast Lo +3 >", " ", "Forecast Lo +4 >", " ", "Forecast Lo +5 >", " ", "Forecast Lo +6 >", " ", "Forecast Lo +7 >", " ", "Forecast Lo +8 >", " ", "Forecast Lo +9 >" });
					else setNavigation(panel, mouseDownEvent, new string[] { "Reference Lo -1", " ", "Reference Lo -2", " ", "Reference Lo -3", " ", "Reference Lo -4", " ", "Reference Lo -5", " ", "Reference Lo -6", " ", "Reference Lo -7", " ", "Reference Lo -8", " ", "Reference Lo -9" });
				}
			}
			else if (level1 == "PRICE >")
			{
				if (level2 == "") setNavigation(panel, mouseDownEvent, new string[] { "Cl PX >", " ", "Op PX >", " ", "Hi PX >", " ", "Lo PX >" });
				else if (level2 == "Cl PX >")
				{
					if (level3 == "") setNavigation(panel, mouseDownEvent, new string[] { "Cl PX +1", " ", "Cl PX +2", " ", "Cl PX +3", " ", "Cl PX +4", " ", "Cl PX +5", " ", "Cl PX +6", " ", "Cl PX +7", " ", "Cl PX +8", " ", "Cl PX +9" });
					else ok = false;
				}
				else if (level2 == "Op PX >")
				{
					if (level3 == "") setNavigation(panel, mouseDownEvent, new string[] { "Op PX +1", " ", "Op PX +2", " ", "Op PX +3", " ", "Op PX +4", " ", "Op PX +5", " ", "Op PX +6", " ", "Op PX +7", " ", "Op PX +8", " ", "Op PX +9" });
					else ok = false;
				}
				else if (level2 == "Hi PX >")
				{
					if (level3 == "") setNavigation(panel, mouseDownEvent, new string[] { "Hi PX +1", " ", "Hi PX +2", " ", "Hi PX +3", " ", "Hi PX +4", " ", "Hi PX +5", " ", "Hi PX +6", " ", "Hi PX +7", " ", "Hi PX +8", " ", "Hi PX +9" });
					else ok = false;
				}
				else if (level2 == "Lo PX >")
				{
					if (level3 == "") setNavigation(panel, mouseDownEvent, new string[] { "Lo PX +1", " ", "Lo PX +2", " ", "Lo PX +3", " ", "Lo PX +4", " ", "Lo PX +5", " ", "Lo PX +6", " ", "Lo PX +7", " ", "Lo PX +8", " ", "Lo PX +9" });
					else ok = false;
				}
			}
			//else if (level1 == "VOLATILITY >")
			//{
			//    if (level2 == "") setNavigationLevel1(panel, mouseDownEvent, new string[] { "VOLATILITY 50 >", " ", "VOLATILITY 30 >", " ", "VOLATILITY 20 >", " ", "VOLATILITY 10 >", " ", "VOLATILITY 5 >" });
			//    else if (level2 == "VOLATILITY 50 >")
			//    {
			//        if (level3 == "") setNavigationLevel1(panel, mouseDownEvent, new string[] { "Volatility(50) .5 or Less", " ", "Volatility(50) .5 to 1.0", " ", "Volatility(50) 1.0 to 1.5", " ", "Volatility(50) 1.5 or Higher" });
			//        else ok = false;
			//    }
			//    else if (level2 == "VOLATILITY 30 >")
			//    {
			//        if (level3 == "") setNavigationLevel1(panel, mouseDownEvent, new string[] { "Volatility(30) .5 or Less", " ", "Volatility(30) .5 to 1.5", " ", "Volatility(30) 1.0 to 1.5", " ", "Volatility(30) 1.5 or Higher" });
			//        else ok = false;
			//    }
			//    else if (level2 == "VOLATILITY 20 >")
			//    {
			//        if (level3 == "") setNavigationLevel1(panel, mouseDownEvent, new string[] { "Volatility(20) .5 or Less", " ", "Volatility(20) .5 to 1.5", " ", "Volatility(20) 1.0 to 1.5", " ", "Volatility(20) 1.5 or Higher" });
			//        else ok = false;
			//    }
			//    else if (level2 == "VOLATILITY 10 >")
			//    {
			//        if (level3 == "") setNavigationLevel1(panel, mouseDownEvent, new string[] { "Volatility(10) .5 or Less", " ", "Volatility(10) .5 to 1.5", " ", "Volatility(10) 1.0 to 1.5", " ", "Volatility(10) 1.5 or Higher" });
			//        else ok = false;
			//    }
			//    else if (level2 == "VOLATILITY 5 >")
			//    {
			//        if (level3 == "") setNavigationLevel1(panel, mouseDownEvent, new string[] { "Volatility(5) .5 or Less", " ", "Volatility(5) .5 to 1.5", " ", "Volatility(5) 1.0 to 1.5", " ", "Volatility(5) 1.5 or Higher" });
			//        else ok = false;
			//    }
			//}
			else ok = false;

			return ok;
		}

		public bool setNavigationLevel4(string country, string group, string subgroup, StackPanel panel, MouseButtonEventHandler mouseDownEvent)
		{
			_activeNavigationPathIndex = 4;
			_navigationPath[3] = subgroup;
			_navigationPath[4] = "";
			_navigationPath[5] = "";

			panel.Children.Clear();

			string[] fields = country.Split(':');
			country = (fields.Length > 1) ? fields[1] : fields[0];

			bool ok = false;

			fields = group.Split(':');
			string groupSymbol = fields[0];

			fields = subgroup.Split(':');
			string subgroupSymbol = fields[0];

			if (groupSymbol == "BWORLD" || groupSymbol == "BWORLD >")
			{
				if (subgroupSymbol == "BWORLDL1")
				{
					setNavigation(panel, mouseDownEvent, new string[]
					{
							"BWFINL:WORLD FINANCIAL >",
							"BWCNCY:WORLD CON NON CYC >",
							"BWINDU:WORLD INDUSTRIAL >",
							"BWCCYS:WORLD CON CYC >",
							"BWCOMM:WORLD COMM >",
							"BWTECH:WORLD TECH >",
							"BWENRS:WORLD ENERGY >",
							"BWBMAT:WORLD BASIC MAT >",
							"BWUTIL:WORLD UTILITIES"
					});

					ok = true;
				}

				else if (subgroupSymbol == "WORLD INDUSTRIES")
				{
					setNavigation(panel, mouseDownEvent, new string[]
					{
							 "BWBANK:WORLD BANKS >",
							 "BWOILP:WORLD OIL & GAS PRO >",
							 "BWPHRM:WORLD PHARMACEUT >",
							 "BWITNT:WORLD INTERNET >",
							 "BWRETL:WORLD RETAIL >",
							 "BWTELE:WORLD TELECOM >",
							 "BWINSU:WORLD INSURANCE >",
							 "BWSFTW:WORLD SOFTWARE >",
							 "BWDFIN:WORLD DIV FIN SER >",
							 "BWCOMP:WORLD COMPUTERS >",
							 "BWFOOD:WORLD FOOD >",
							 "BWSEMI:WORLD SEMICONDUCT >",
							 "BWCHEM:WORLD CHEMICALS >",
							 "BWELEC:WORLD ELECTRIC >",
							 "BWREIT:WORLD REIT >",
							 "BWCMMS:WORLD COMMER SER >",
							 "BWREAL:WORLD REAL ESTATE >",
							 "BWBEVG:WORLD BEVERAGES >",
							 "BWHCPR:WORLD HEALTH CARE PR >",
							 "BWELCT:WORLD ELECTRONICS >",
							 "BWTRAN:WORLD TRANSPORT >",
							 "BWAUTM:WORLD AUTO MANUF >",
							 "BWMING:WORLD MINING >",
							 "BWMEDA:WORLD MEDIA >",
							 "BWENGN:WORLD ENGIN & CON >",
							 "BWBIOT:WORLD BIO TECH >",
							 "BWAERO:WORLD AEROSP/DEF >",
							 "BWMMAN:WORLD MISC-MANU >",
							 "BWHCSV:WORLD HEALTH C SV >",
							 "BWBUIL:WORLD BUILDING MA >",
							 "BWAPPR:WORLD APPAREL >",
							 "BWCOSM:WORLD COSM/PER CA >",
							 "BWAGRI:WORLD AGRICULTURE >",
							 "BWMCHD:WORLD MACH-DIVERS >",
							 "BWAUTP:WORLD A PARTS/EQ >",
							 "BWIRON:WORLD IRON/STEEL >",
							 "BWELCM:WORLD ELEC COM/EQ >",
							 "BWDIST:WORLD DIST/WHOLES >",
							 "BWLODG:WORLD LODGING >",
							 "BWHFUR:WORLD HOME FURNIS >",
							 "BWINVS:WORLD INVEST COMP >",
							 "BWGAS:WORLD GAS >",
							 "BWMCHC:WORLD MAC-CONS/MI >",
							 "BWAIRL:WORLD AIRLINES >",
							 "BWENTE:WORLD ENTERTAINMT >",
							 "BWOILS:WORLD OIL & GAS SER >",
							 "BWPIPE:WORLD PIPELINES >",
							 "BWLEIS:WORLD LEISURE TI >",
							 "BWHOUS:WORLD HOSHLD PR/W >",
							 "BWMETL:WORLD MET FAB/HDW >",
							 "BWHBLD:WORLD HOME BUILD >",
							 "BWFRST:WORLD FOR PROD/PA >",
							 "BWTOOL:WORLD HAND/MACH >",
							 "BWENVR:WORLD ENVIR CONTL >",
							 "BWPACK:WORLD PACKAGING >",
							 "BWCOAL:WORLD COAL >",
							 "BWADVT:WORLD ADVERTISING >",
							 "BWENRG:WORLD ENERGY-ATL >",
							 "BWWATR:WORLD WATER >",
							 "BWTEXT:WORLD TEXTILES >",
							 "BWTOYS:WORLD TOY/GAM/HOB >",
							 "BWOFFE:WORLD OFF/BUS EQU >",
							 "BWFSRV:WORLD FOOD SERVIC >",
							 "BWSAVL:WORLD SAV & LOANS >",
							 "BWSHIP:WORLD SHIPBUILDING >",
							 "BWHWAR:WORLD HOUSEWARES >",
							 "BWSTOR:WORLD STOR/WAREH >",
							 "BWOFUR:WORLD OFFICE FURN",
							 "BWTRUC:WORLD TRUCK & LEAS >"
					});

					ok = true;
				}
			}


			if (groupSymbol == "BWORLDUS" || groupSymbol == "BWORLDUS >")
			{
				if (subgroupSymbol == "AMERICAS INDUSTRIES")
				{
					setNavigation(panel, mouseDownEvent, new string[]
					{
							"BUSBANK:AMER BANKS >",
							"BUSSFTW:AMER SOFTWARE >",
							"BUSRETL:AMER RETAIL >",
							"BUSPHRM:AMER PHARMACEUTICAL >",
							"BUSCOMP:AMER COMPUTERS >",
							"BUSOILP:AMER OIL & GAS PROD >",
							"BUSINSU:AMER INSURANCE >",
							"BUSDFIN:AMER DIV FIN SERV >",
							"BUSSEMI:AMER SEMICONDUCTOR >",
							"BUSHCPR:AMER HEALTH-PRODUCT >",
							"BUSTELE:AMER TELECOMM >",
							"BUSCMMS:AMER COMM SERVICE >",
							"BUSELEC:AMER ELECTRIC >",
							"BUSBIOT:AMER BIOTECHNOLOGY >",
							"BUSAERO:AMER AERO/DEFENSE >",
							"BUSHCSV:AMER HEALTH-SERVICE >",
							"BUSMEDA:AMER MEDIA >",
							"BUSCHEM:AMER CHEMICALS >",
							"BUSBEVG:AMER BEVERAGES >",
							"BUSFOOD:AMER FOOD >",
							"BUSTRAN:AMER TRANSPORTATION >",
							"BUSMMAN:AMER MISC-MANUFACT >",
							"BUSELCT:AMER ELECTRONICS >",
							"BUSCOSM:AMER COSMET/PERS >",
							"BUSAGRI:AMER AGRICULTURE >",
							"BUSMING:AMER MINING >",
							"BUSMCHD:AMER MACH-DIVERS >",
							"BUSPIPE:AMER PIPELINES >",
							"BUSAUTM:AMER AUTO MANUFACT >",
							"BUSOILS:AMER OIL & GAS SERV >",
							"BUSAPPR:AMER APPAREL >",
							"BUSLODG:AMER LODGING >",
							"BUSBUIL:AMER BUILDING MAT >",
							"BUSAIRL:AMER AIRLINES >",
							"BUSAUTP:AMER AUTO PART/EQP >",
							"BUSIRON:AMER IRON/STEEL >",
							"BUSINVS:AMER INVESTMENT CO >",
							"BUSELCM:AMER ELEC COMP/EQP >",
							"BUSENVR:AMER ENVIRON CTRL >",
							"BUSLEIS:AMER LEISURE TIME >",
							"BUSGAS:AMER GAS >",
							"BUSPACK:AMER PACK & CONTAIN >",
							"BUSMCHC:AMER MACH-CONST/MIN >",
							"BUSHOUS:AMER HOUSE PRD/WARE >",
							"BUSENTE:AMER ENTERTAINMENT >",
							"BUSENGN:AMER ENGIN & CONST >",
							"BUSDIST:AMER DIST/WHOLE >",
							"BUSHBLD:AMER HOME BUILDERS >",
							"BUSREAL:AMER REAL ESTATE >",
							"BUSFRST:AMER FOR PROD/PAPER >",
							"BUSSAVL:AMER SAV & LOANS >",
							"BUSTOOL:AMER HAND/MACH TOOL >",
							"BUSWATR:AMER WATER >",
							"BUSENRG:AMER ENRG-ALT SRCE >",
							"BUSMETL:AMER METAL FAB/HRD >",
							"BUSADVT:AMER ADVERTISING >",
							"BUSHWAR:AMER HOUSEWARES >",
							"BUSHFUR:AMER HOME FURNISH >",
							"BUSTEXT:AMER TEXTILES >",
							"BUSTOYS:AMER TOY/GAME/HOB >",
							"BUSSTOR:AMER STOR/WAREHOUS >",
							"BUSOFUR:AMER OFFICE FURN >",
							"BUSOFFE:AMER OFFC/BUS EQUP >",
							"BUSCOAL:AMER COAL >",
							"BUSTRUC:AMER TRUCK & LEAS >",
							"BUSFSRV:AMER FOOD SERVICE",
							"BUSSHIP:AMER SHIPBUILDING >"
					});

					ok = true;
				}
			}

			if (groupSymbol == "BWORLDEU" || groupSymbol == "BWORLDEU >")
			{
				if (subgroupSymbol == "EMEA INDUSTRIES")
				{
					setNavigation(panel, mouseDownEvent, new string[]
					{
							"BEUBANK:EMEA BANKS >",
							"BEUOILP:EMEA OIL & GAS PRODC >",
							"BEUPHRM:EMEA PHARMACEUTICALS >",
							"BEUFOOD:EMEA FOOD >",
							"BEUTELE:EMEA TELECOMM >",
							"BEUCHEM:EMEA CHEMICALS >",
							"BEUINSU:EMEA INSURANCE >",
							"BEUELEC:EMEA ELECTRIC >",
							"BEUAPPR:EMEA APPAREL >",
							"BEUBEVG:EMEA BEVERAGES >",
							"BEUMING:EMEA MINING >",
							"BEURETL:EMEA RETAIL >",
							"BEUCMMS:EMEA COMM SERVS >",
							"BEUENGN:EMEA ENGIN & CONSTRU >",
							"BEUSFTW:EMEA SOFTWARE >",
							"BEUMEDA:EMEA MEDIA >",
							"BEUREAL:EMEA REAL ESTATE >",
							"BEUCOSM:EMEA COSM/PER CARE >",
							"BEUAERO:EMEA AERO/DEFENSE >",
							"BEUBUIL:EMEA BUILDING MAT >",
							"BEUAUTM:EMEA AUTO MANUFAC >",
							"BEUINVS:EMEA INVESTMENT CO >",
							"BEUMMAN:EMEA MISCELL-MANU >",
							"BEUDFIN:EMEA DIV FINL SERV >",
							"BEUHCPR:EMEA HEALTH CARE-PRD >",
							"BEUREIT:EMEA REIT >",
							"BEUAGRI:EMEA AGRICULTURE >",
							"BEUTRAN:EMEA TRANSPORTATION >",
							"BEUSEMI:EMEA SEMICONDUCTORS >",
							"BEUMCHD:EMEA MACH- DIV >",
							"BEUHCSV:EMEA HEALTH CARE-SRV >",
							"BEUIRON:EMEA IRON/STEEL >",
							"BEUGAS:EMEA GAS >",
							"BEUELCT:EMEA ELECTRONICS >",
							"BEUAUTP:EMEA AUTO PARTS &EQP >",
							"BEUELCM:EMEA ELEC COMP&EQUIP >",
							"BEUBIOT:EMEA BIOTECHNOLOGY >",
							"BEUFRST:EMEA FOREST PROD/PAP >",
							"BEUMCHC:EMEA MACH- CONST/MIN >",
							"BEUCOMP:EMEA COMPUTER >",
							"BEUAIRL:EMEA AIRLINES >",
							"BEUHOUS:EMEA HOUSEHOLD PRODT >",
							"BEUDIST:EMEA DIST/WHLSALE >",
							"BEUTOOL:EMEA HAND/MACH TOOLS >",
							"BEUMETL:EMEA METAL FAB/HDWR >",
							"BEUADVT:EMEA ADVERTISING >",
							"BEUFSRV:EMEA FOOD SERVICE >",
							"BEULEIS:EMEA LEISURE TIME >",
							"BEUHBLD:EMEA HOME BUILDERS >",
							"BEULODG:EMEA LODGING >",
							"BEUENTE:EMEA ENTERTAINMENT >",
							"BEUWATR:EMEA WATER INDEX >",
							"BEUENRG:EMEA ENERGY-ALT SRC >",
							"BEUOILS:EMEA OIL & GAS SERVS >",
							"BEUHFUR:EMEA HOME FURNISHING >",
							"BEUPACK:EMEA PACK & CONTAINR >",
							"BEUENVR:EMEA ENVIRON CONTR >",
							"BEUITNT:EMEA INTERNET INDEX >",
							"BEUSTOR:EMEA STORAGE/WAREHOU >",
							"BEUTEXT:EMEA TEXTILES >",
							"BEUCOAL:EMEA COAL >",
							"BEUHWAR:EMEA HOUSEWARES >",
							"BEUOFFE:EMEA OFFICE/BUS EQUP >",
							"BEUOFUR:EMEA OFFICE FURNISH >",
							"BEUPIPE:EMEA PIPELINES >",
							"BEUSAVL:EMEA SAVINGS & LOANS >",
							"BEUSHIP:EMEA SHIPBUILDING >",
							"BEUTOYS:EMEA TOYS/GAMES/HOBB >",
							"BEUTRUC:EMEA TRUCKING&LEASIN >"
					});

					ok = true;
				}
			}

			if (groupSymbol == "BWORLDPR" || groupSymbol == "BWORLDPR >")
			{
				if (subgroupSymbol == "ASIA PACIFIC INDUSTRIES")
				{
					setNavigation(panel, mouseDownEvent, new string[]
					{
							"BPRBANK:AP BANKS >",
							"BPRREAL:AP REAL ESTATE >",
							"BPRTELE:AP TELECOMM >",
							"BPRINSU:AP INSURANCE >",
							"BPRDFIN:AP DIVERS FINCL SVCS >",
							"BPRPHRM:AP PHARMACEUTICALS >",
							"BPROILP:AP OIL & GAS PRODUCR >",
							"BPRSEMI:AP SEMICONDUCTORS >",
							"BPRELCT:AP ELECTRONICS >",
							"BPRAUTM:AP AUTO MANUFACTURER >",
							"BPRFOOD:AP FOOD >",
							"BPRCHEM:AP CHEMICALS >",
							"BPRENGN:AP ENGINEER & CONST >",
							"BPRRETL:AP RETAIL >",
							"BPRTRAN:AP TRANSPORTATION >",
							"BPRELEC:AP ELECTRIC >",
							"BPRMING:AP MINING >",
							"BPRAUTP:AP AUTO PTS & EQUIP >",
							"BPRCOMP:AP COMPUTERS >",
							"BPRBEVG:AP BEVERAGES >",
							"BPRHFUR:AP HOME FURNISHINGS >",
							"BPRCMMS:AP COMMERCIAL SVCS >",
							"BPRBUIL:AP BUILDING MATERIAL >",
							"BPRDIST:AP DIST/WHOLESALE >",
							"BPRMCHD:AP MACH-DIVERSIFIED >",
							"BPRMMAN:AP MISC-MANUFACTURE >",
							"BPRAGRI:AP AGRICULTURE >",
							"BPRIRON:AP IRON/STEEL >",
							"BPRELCM:AP ELE COMP & EQUIP >",
							"BPRENTE:AP ENTERTAINMENT >",
							"BPRCOAL:AP COAL >",
							"BPRSFTW:AP SOFTWARE >",
							"BPRLODG:AP LODGING >",
							"BPRBIOT:AP BIOTECH >",
							"BPRCOSM:AP COSMETICS/PER CAR >",
							"BPRMCHC:AP MACH-CNSTR & MINE >",
							"BPRGAS:AP GAS >",
							"BPRHCPR:AP HEALTH CARE-PRODS >",
							"BPRMETL:AP METAL FABR/HARDWR >",
							"BPRLEIS:AP LEISURE TIME >",
							"BPRHOUS:AP HSEHLD PROD/WARES >",
							"BPRAIRL:AP AIRLINES >",
							"BPRHCSV:AP HEALTH CARE-SVCS >",
							"BPRAPPR:AP APPAREL >",
							"BPRTOOL:AP HAND/MACHINE TOOL >",
							"BPRHBLD:AP HOME BUILDERS >",
							"BPROFFE:AP OFFICE/BUS EQUIP >",
							"BPRTEXT:AP TEXTILES >",
							"BPRENVR:AP ENVIRONMTL CONTRL >",
							"BPRTOYS:AP TOYS/GAMES/HOBBY >",
							"BPRPACK:AP PACKAGING & CONT >",
							"BPRENRG:AP ENERGY-ALT SOURCE >",
							"BPRADVT:AP ADVERTISING >",
							"BPRFRST:AP FOREST PRD & PAPR >",
							"BPRINVS:AP INVESTMT COMPANY >",
							"BPRMEDA:AP MEDIA >",
							"BPROILS:AP OIL & GAS SERVICE >",
							"BPRWATR:AP WATER >",
							"BPRSHIP:AP SHIPBUILDING >",
							"BPRHWAR:AP HOUSEWARES >",
							"BPRAERO:AP AEROSPACE/DEFENSE >",
							"BPRSTOR:AP STORAGE/WAREHOUSE >",
							"BPROFUR:AP OFFICE FURNISHING >",
							"BPRFSRV:PR FOOD SERVICE >",
							"BPRPIPE:AP PIPELINES >",
							"BPRSAVL:AP SAVINGS & LOANS >",
							"BPRTRUC:AP TRUCKING/LEASING >"
					});

					ok = true;
				}
			}

			if (country == "US EQ >" || country == "USA" || country == "USA >")
			{
				if (groupSymbol == "SPX")
				{
					if (subgroupSymbol == "SPX")
					{

					}
					else if (subgroupSymbol == "SPXL1")
					{
						if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
							"S5COND:SPX CONS DISC >",
							"S5CONS:SPX CONS STAPLES >",
							"S5ENRS:SPX ENERGY >",
							"S5FINL:SPX FINANCIALS >",
							"S5HLTH:SPX HEALTH CARE >",
							"S5INDU:SPX INDUSTRIALS >",
							"S5INFT:SPX INFO TECH >",
							"S5MATR:SPX MATERIALS >",
							"S5REAL:SPX REAL >",
							"S5TELS:SPX TELECOM >",
							"S5UTIL:SPX UTILITIES >"
						});

						else setNavigation(panel, mouseDownEvent, new string[] {
							"S5COND:SPX CONS DISC",
							"S5CONS:SPX CONS STAPLES",
							"S5ENRS:SPX ENERGY",
							"S5FINL:SPX FINANCIALS",
							"S5HLTH:SPX HEALTH CARE",
							"S5INDU:SPX INDUSTRIALS",
							"S5INFT:SPX INFO TECH",
							"S5MATR:SPX MATERIALS",
							"S5TELS:SPX TELECOM",
							"S5UTIL:SPX UTILITIES"
						});
						ok = true;
					}

					else if (subgroupSymbol == "SPXL2")
					{
						if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
							"S5AUCO:SPX AUTO & COMP >",
							"S5BANKX:SPX BANKS >",
							"S5CODU:SPX CAPITAL GOODS >",
							"S5COMS:SPX COMM & PROF >",
							"S5CPGS:SPX CON DUR & AP >",
							"S5DIVF:SPX DIV FINANCE >",
							"S5ENRSX:SPX ENERGY >",
							"S5FDBT:SPX FOOD/STAPLES >",
							"S5FDSR:SPX FOOD BEV TOB >",
							"S5HCES:SPX HC EQUIP >",
							"S5HOTR:SPX CONS SERV >",
							"S5HOUS:SPX HOUSEHOLD PROD >",
							"S5INSU:SPX INSURANCE >",
							"S5MATRX:SPX MATERIALS >",
							"S5MEDA:SPX MEDIA >",
							"S5PHRM:SPX PHRM BIO & LIFE >",
							"S5REAL:SPX REAL ESTATE >",
							"S5RETL:SPX RETAILING >",
							"S5SSEQX:SPX SEMI & EQP >",
							"S5SFTW:SPX SOFTWARE & SVCS >",
							"S5TECH:SPX TECH HW & EQP >",
							"S5TELSX:SPX TELECOM SVCS >",
							"S5TRAN:SPX SPX TRANSPORT >",
							"S5UTILX:SPX UTILTIES >"
						});

						else setNavigation(panel, mouseDownEvent, new string[] {
							"S5AUCO:SPX AUTO & COMP",
							"S5BANKX:SPX BANKS",
							"S5CODU:SPX CAPITAL GOODS",
							"S5COMS:SPX COMM & PROF",
							"S5CPGS:SPX CON DUR & AP",
							"S5DIVF:SPX DIV FINANCE",
							"S5ENRSX:SPX ENERGY",
							"S5FDBT:SPX FOOD/STAPLES",
							"S5FDSR:SPX FOOD BEV TOB",
							"S5HCES:SPX HC EQUIP",
							"S5HOTR:SPX CONS SERV",
							"S5HOUS:SPX HOUSEHOLD PROD",
							"S5INSU:SPX INSURANCE",
							"S5MATRX:SPX MATERIALS",
							"S5MEDA:SPX MEDIA",
							"S5PHRM:SPX PHRM BIO & LIFE",
							"S5REAL:SPX REAL ESTATE",
							"S5RETL:SPX RETAILING",
							"S5SSEQX:SPX SEMI & EQP",
							"S5SFTW:SPX SOFTWARE & SVCS",
							"S5TECH:SPX TECH HW & EQP",
							"S5TELSX:SPX TELECOM SVCS",
							"S5TRAN:SPX TRANSPORT",
							"S5UTILX:SPX UTILTIES"
						});
						ok = true;
					}

					else if (subgroupSymbol == "SPXL3")
					{
						ok = true;
						if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
							"S5AEROX:SPX AERO & DEF >",
							"S5AIRFX:SPX AIR FRT & LOG >",
							"S5AIRLX:SPX AIRLINES >",
							"S5AUTC:SPX AUTO COMP >",
							"S5AUTO:SPX AUTOMOBILES >",
							"S5BEVG:SPX BEVERAGES >",
							"S5BIOTX:SPX BIOTECH >",
							"S5BUILX:SPX BLDG PRODS >",
							"S5CAPM:SPX CAPITAL MKTS >",
							"S5CBNK:SPX COMM BANKS >",
							"S5CFINX:SPX CONS FINANCE >",
							"S5CHEM:SPX CHEMICALS >",
							"S5CMPE:SPX COMPUTERS & PER >",
							"S5COMM:SPX COMMUNICATION EQP >",
							"S5COMSX:SPX COMMERCIAL SRBS >",
							"S5CONP:SPX CONTAINER & PKG >",
							"S5CSTEX:SPX CONST & ENG >",
							"S5CSTMX:SPX CONST MATERIAL >",
							"S5DCON:SPX DIVERSIFIED SRVC >",
							"S5DISTX:SPX DISBRIBUTORS >",
							"S5DIVT:SPX DIV TEL SVC >",
							"S5DVFS:SPX DIV FIN SVC >",
							"S5ELEIX:SPX ELECTRONIC EQUP >",
							"S5ELEQ:SPX ELECTRICAL EQUP >",
							"S5ELUTX:SPX ELECTRIC UTL >",
							"S5ENRE:SPX ENERGY EQUP & SV >",
							"S5FDPR:SPX FOOD PROD IND >",
							"S5FDSRX:SPX FOOD & STAPLES RET >",
							"S5GASUX:SPX GAS UTL >",
							"S5HCEQ:SPX HC EQUP & SUP >",
							"S5HCPS:SPX HC PROVIDERS SVC >",
							"S5HCTEX:SPX HC TECHNOLOGY >",
							"S5HODU:SPX HOUSEHOLD DURABLES >",
							"S5HOPRX:SPX HOUSELHOLD PROD >",
							"S5HOTRX:SPX HOTELS REST & LEIS >",
							"S5INCR:SPX INTERNET CATALOG >",
							"S5INDCX:SPX INDUSTRIAL CONGL >",
							"S5INSSX:SPX INTERNET SOFTWARE >",
							"S5INSUX:SPX INSUURANCE IND >",
							"S5IPPEX:SPX INDEP PWR PROD >",
							"S5ITSV:SPX IT SERV IND >",
							"S5LEIS:SPX LEISURE EQUP >",
							"S5LSTSX:SPX LIFE SCI IND >",
							"S5MACH:SPX MACHINERY >",
							"S5MDREX:SPX RE MGM >",
							"S5MEDAX:SPX MEDIA >",
							"S5METL:SPX METAL & MIN >",
							"S5MRET:SPX MULTILINE RET >",
							"S5MUTIX:SPX MULTI UTL >",
							"S5OFFEX:SPX OFFICE ELECT >",
							"S5OILG:SPX OIL GAS FUEL >",
							"S5PAFO:SPX PAPER FORSET PROD >",
							"S5PERSX:SPX PERSONAL PROD >",
							"S5PHARX:SPX PHARMA >",
							"S5PRSV:SPX PROF SRVS >",
							"S5REITS:SPX RE INV TRUSTS >",
							"S5ROAD:SPX ROARD & RAIL >",
							"S5SOFT:SPX SOFTWARE >",
							"S5SPRE:SPX SPECIALTY RET >",
							"S5SSEQ:SPX SEMICOND & EQUP >",
							"S5TEXA:SPX TXTL & APPRL >",
							"S5THMFX:SPX THRIFTS & MORT >",
							"S5TOBAX:SPX TOBACCO >",
							"S5TRADX:SPX TRADING CO & DIS >",
							"S5WIREX:SPX WIRELESS TELECOM >"
						});

						else setNavigation(panel, mouseDownEvent, new string[] {
							"S5AEROX:SPX AERO & DEF",
							"S5AIRFX:SPX AIR FRT & LOG",
							"S5AIRLX:SPX AIRLINES",
							"S5AUTC:SPX AUTO COMP",
							"S5AUTO:SPX AUTOMOBILES",
							"S5BEVG:SPX BEVERAGES",
							"S5BIOTX:SPX BIOTECH",
							"S5BUILX:SPX BLDG PRODS",
							"S5CAPM:SPX CAPITAL MKTS",
							"S5CBNK:SPX COMM BANKS",
							"S5CFINX:SPX CONS FINANCE",
							"S5CHEM:SPX CHEMICALS",
							"S5CMPE:SPX COMPUTERS & PER",
							"S5COMM:SPX COMMUNICATION EQP",
							"S5COMSX:SPX COMMERCIAL SRBS",
							"S5CONP:SPX CONTAINER & PKG",
							"S5CSTEX:SPX CONST & ENG",
							"S5CSTMX:SPX CONST MATERIAL",
							"S5DCON:SPX DIVERSIFIED SRVC",
							"S5DISTX:SPX DISBRIBUTORS",
							"S5DIVT:SPX DIV TEL SVC",
							"S5DVFS:SPX DIV FIN SVC",
							"S5ELEIX:SPX ELECTRONIC EQUP",
							"S5ELEQ:SPX ELECTRICAL EQUP",
							"S5ELUTX:SPX ELECTRIC UTL",
							"S5ENRE:SPX ENERGY EQUP & SV",
							"S5FDPR:SPX FOOD PROD IND",
							"S5FDSRX:SPX FOOD & STAPLES RET",
							"S5GASUX:SPX GAS UTL",
							"S5HCEQ:SPX HC EQUP & SUP",
							"S5HCPS:SPX HC PROVIDERS SVC",
							"S5HCTEX:SPX HC TECHNOLOGY",
							"S5HODU:SPX HOUSEHOLD DURABLES",
							"S5HOPRX:SPX HOUSELHOLD PROD",
							"S5HOTRX:SPX HOTELS REST & LEIS",
							"S5INCR:SPX INTERNET CATALOG",
							"S5INDCX:SPX INDUSTRIAL CONGL",
							"S5INSSX:SPX INTERNET SOFTWARE",
							"S5INSUX:SPX INSUURANCE IND",
							"S5IPPEX:SPX INDEP PWR PROD",
							"S5ITSV:SPX IT SERV IND",
							"S5LEIS:SPX LEISURE EQUP",
							"S5LSTSX:SPX LIFE SCI IND",
							"S5MACH:SPX MACHINERY",
							"S5MDREX:SPX RE MGM",
							"S5MEDAX:SPX MEDIA",
							"S5METL:SPX METAL & MIN",
							"S5MRET:SPX MULTILINE RET",
							"S5MUTIX:SPX MULTI UTL",
							"S5OFFEX:SPX OFFICE ELECT",
							"S5OILG:SPX OIL GAS FUEL",
							"S5PAFO:SPX PAPER FORSET PROD",
							"S5PERSX:SPX PERSONAL PROD",
							"S5PHARX:SPX PHARMA",
							"S5PRSV:SPX PROF SRVS",
							"S5REITS:SPX RE INV TRUSTS",
							"S5ROAD:SPX ROARD & RAIL",
							"S5SOFT:SPX SOFTWARE",
							"S5SPRE:SPX SPECIALTY RET",
							"S5SSEQ:SPX SEMICOND & EQUP",
							"S5TEXA:SPX TXTL & APPRL",
							"S5THMFX:SPX THRIFTS & MORT",
							"S5TOBAX:SPX TOBACCO",
							"S5TRADX:SPX TRADING CO & DIS",
							"S5WIREX:SPX WIRELESS TELECOM"
						});
						ok = true;
					}

					else if (subgroupSymbol == "SPXL4")
					{

						if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
							"SPW:SPX Equal Weight Index >",
							"SPXEWTR:SPX Equal Weighted USD Total Return Index >",
							"SPXEW4UP:SPX Equal Weight Communication Services Plus Index >",
							"SPXEW4UT:SPX Equal Weight Communication Services Plus Index TR >",
							"SPXEWCD:SPX Equal Weight Index Consumers Discretionary >",
							"SPXEWCS:SPX Equal Weight Consumer Staples Total Return Index >",
							"SPXEWEN:SPX Equal Weight Energy (Sector) Total Return USD Index >",
							"SPXEWFN:SPX Equal Weight Financials (Sector) Total Return Index >",
							"SPXEWHC:SPX Equal Weighted Health Care Sector USD Total Return Index >",
							"SPXEWIN:SPX Equal Weighted Industrials USD Total Return Index >",
							"SPXEWIT:SPX Equal Weighted InTc USD Total Return Index >",
							"SPXEWMA:SPX Equal Weighted Materials USD Total Return Index >",
							"SPXEREUT:SPX Equal Weight Real Estate Index TR >",
							"SPXEWTS:SPX Equal Weight Telecommunication Services (Sector) Total Return >",
							"SPXEWUT:SPX Equal Weight Utilities (Sector) Total Return USD Index >"
						});

						else setNavigation(panel, mouseDownEvent, new string[] {
							"SPW:SPX Equal Weight Index >",
							"SPXEWTR:SPX Equal Weighted USD Total Return Index >",
							"SPXEW4UP:SPX Equal Weight Communication Services Plus Index >",
							"SPXEW4UT:SPX Equal Weight Communication Services Plus Index TR >",
							"SPXEWCD:SPX Equal Weight Index Consumers Discretionary >",
							"SPXEWCS:SPX Equal Weight Consumer Staples Total Return Index >",
							"SPXEWEN:SPX Equal Weight Energy (Sector) Total Return USD Index >",
							"SPXEWFN:SPX Equal Weight Financials (Sector) Total Return Index >",
							"SPXEWHC:SPX Equal Weighted Health Care Sector USD Total Return Index >",
							"SPXEWIN:SPX Equal Weighted Industrials USD Total Return Index >",
							"SPXEWIT:SPX Equal Weighted InTc USD Total Return Index >",
							"SPXEWMA:SPX Equal Weighted Materials USD Total Return Index >",
							"SPXEREUT:SPX Equal Weight Real Estate Index TR >",
							"SPXEWTS:SPX Equal Weight Telecommunication Services (Sector) Total Return >",
							"SPXEWUT:SPX Equal Weight Utilities (Sector) Total Return USD Index >"
						});
						ok = true;
					}
				}


				else if (groupSymbol == "MID")
				{
					if (subgroupSymbol == "MIDL1")
					{
						if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
							"S4COND:MID CONS DISC >",
							"S4CONS:MID CONS STAPLES >",
							"S4ENRS:MID ENERGY >",
							"S4FINL:MID FINANCIALS >",
							"S4HLTH:MID HEALTH CARE >",
							"S4INDU:MID INDUSTRIALS >",
							"S4INFT:MID INFO TECH >",
							"S4MATR:MID MATERIALS >",
							"S4TELS:MID TELECOM >",
							"S4UTIL:MID UTILITIES >"
						 });

						else setNavigation(panel, mouseDownEvent, new string[] {
							"S4COND:MID CONS DISC",
							"S4CONS:MID CONS STAPLES",
							"S4ENRS:MID ENERGY",
							"S4FINL:MID FINANCIALS",
							"S4HLTH:MID HEALTH CARE",
							"S4INDU:MID INDUSTRIALS",
							"S4INFT:MID INFO TECH",
							"S4MATR:MID MATERIALS",
							"S4TELS:MID TELECOM",
							"S4UTIL:MID UTILITIES"
						});
						ok = true;
					}

					else if (subgroupSymbol == "MIDL2")
					{
						if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
							"S4AUCO:MID AUTO & COMP >",
							"S4BANKX:MID BANKS >",
							"S4CODU:MID CAPITAL GOODS >",
							"S4COMS:MID COMM & PROF >",
							"S4CPGS:MID CON DUR & AP >",
							"S4DIVF:MID DIV FINANCE >",
							"S4ENRSX:MID ENERGY >",
							"S4FDBT:MID FOOD/STAPLES >",
							"S4FDSR:MID FOOD BEV TOB >",
							"S4HCES:MID HC EQUIP >",
							"S4HOTR:MID CONS SERV >",
							"S4HOUS:MID HOUSEHOLD PROD >",
							"S4INSU:MID INSURANCE >",
							"S4MATRX:MID MATERIALS >",
							"S4MEDA:MID MEDIA >",
							"S4PHRM:MID PHRM BIO & LIFE >",
							"S4REAL:MID REAL ESTATE >",
							"S4RETL:MID RETAILING >",
							"S4SSEQX:MID SEMI & EQP >",
							"S4SFTW:MID SOFTWARE & SVCS >",
							"S4TECH:MID TECH HW & EQP >",
							"S4TELSX:MID TELECOM SVCS >",
							"S4TRAN:MID TRANSPORT >",
							"S4UTILX:MID UTILTIES >"
						 });

						else setNavigation(panel, mouseDownEvent, new string[] {
							"S4AUCO:MID AUTO & COMP",
							"S4BANKX:MID BANKS",
							"S4CODU:MID CAPITAL GOODS",
							"S4COMS:MID COMM & PROF",
							"S4CPGS:MID CON DUR & AP",
							"S4DIVF:MID DIV FINANCE",
							"S4ENRSX:MID ENERGY",
							"S4FDBT:MID FOOD/STAPLES",
							"S4FDSR:MID FOOD BEV TOB",
							"S4HCES:MID HC EQUIP",
							"S4HOTR:MID CONS SERV",
							"S4HOUS:MID HOUSEHOLD PROD",
							"S4INSU:MID INSURANCE",
							"S4MATRX:MID MATERIALS",
							"S4MEDA:MID MEDIA",
							"S4PHRM:MID PHRM BIO & LIFE",
							"S4REAL:MID REAL ESTATE",
							"S4RETL:MID RETAILING",
							"S4SSEQX:MID SEMI & EQP",
							"S4SFTW:MID SOFTWARE & SVCS",
							"S4TECH:MID TECH HW & EQP",
							"S4TELSX:MID TELECOM SVCS",
							"S4TRAN:MID TRANSPORT",
							"S4UTILX:MID UTILTIES"
						});
						ok = true;
					}

					else if (subgroupSymbol == "MIDL3")
					{
						if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
							"S4AEROX:MID AERO & DEF >",
							"S4AIRFX:MID AIR FRT & LOG >",
							"S4AIRLX:MID AIRLINES >",
							"S4AUTC:MID AUTO COMP >",
							"S4AUTO:MID AUTOMOBILES >",
							"S4BEVG:MID BEVERAGES >",
							"S4BIOTX:MID BIOTECH >",
							"S4BUILX:MID BLDG PRODS >",
							"S4CAPM:MID CAPITAL MKTS >",
							"S4CBNK:MID COMM BANKS >",
							"S4CFINX:MID CONS FINANCE >",
							"S4CHEM:MID CHEMICALS >",
							"S4CMPE:MID COMPUTERS & PER >",
							"S4COMM:MID COMMUNICATION EQP >",
							"S4COMSX:MID COMMERCIAL SRBS >",
							"S4CONP:MID CONTAINER & PKG >",
							"S4CSTEX:MID CONST & ENG >",
							"S4CSTMX:MID CONST MATERIAL >",
							"S4DCON:MID DIVERSIFIED SRVC >",
							"S4DISTX:MID DISBRIBUTORS >",
							"S4DIVT:MID DIV TEL SVC >",
							"S4DVFS:MID DIV FIN SVC >",
							"S4ELEIX:MID ELECTRONIC EQUP >",
							"S4ELEQ:MID ELECTRICAL EQUP >",
							"S4ELUTX:MID ELECTRIC UTL >",
							"S4ENRE:MID ENERGY EQUP & SV >",
							"S4FDPR:MID FOOD PROD IND >",
							"S4FDSRX:MID FOOD & STAPLES RET >",
							"S4GASUX:MID GAS UTL >",
							"S4HCEQ:MID HC EQUP & SUP >",
							"S4HCPS:MID HC PROVIDERS SVC >",
							"S4HCTEX:MID HC TECHNOLOGY >",
							"S4HODU:MID HOUSEHOLD DURABLES >",
							"S4HOPRX:MID HOUSELHOLD PROD >",
							"S4HOTRX:MID HOTELS REST & LEIS >",
							"S4INCR:MID INTERNET CATALOG >",
							"S4INDCX:MID INDUSTRIAL CONGL >",
							"S4INSSX:MID INTERNET SOFTWARE >",
							"S4INSUX:MID INSUURANCE IND >",
							"S4ITSV:MID IT SERV IND >",
							"S4LEIS:MID LEISURE EQUP >",
							"S4LSTSX:MID LIFE SCI IND >",
							"S4MACH:MID MACHINERY >",
							"S4MARIX:MID MARINE >",
							"S4MDREX:MID RE MGM >",
							"S4MEDAX:MID MEDIA >",
							"S4METL:MID METAL & MIN >",
							"S4MRET:MID MULTILINE RET >",
							"S4MUTIX:MID MULTI UTL >",
							"S4OFFEX:MID OFFICE ELECT >",
							"S4OILG:MID OIL GAS FUEL >",
							"S4PAFO:MID PAPER FORSET PROD >",
							"S4PHARX:MID PHARMA >",
							"S4PRSV:MID PROF SRVS >",
							"S4REITS:MID RE INV TRUSTS >",
							"S4ROAD:MID ROARD & RAIL >",
							"S4SOFT:MID SOFTWARE >",
							"S4SPRE:MID SPECIALTY RET >",
							"S4SSEQ:MID SEMICOND & EQUP >",
							"S4TEXA:MID TXTL & APPRL >",
							"S4THMFX:MID THRIFTS & MORT >",
							"S4TOBAX:MID TOBACCO >",
							"S4TRADX:MID TRADING CO & DIS >",
							"S4WATUX:MID WATER UTL >",
							"S4WIREX:MID WIRELESS TELECOM >"
						 });

						else setNavigation(panel, mouseDownEvent, new string[] {
							"S4AEROX:MID AERO & DEF",
							"S4AIRFX:MID AIR FRT & LOG",
							"S4AIRLX:MID AIRLINES",
							"S4AUTC:MID AUTO COMP",
							"S4AUTO:MID AUTOMOBILES",
							"S4BEVG:MID BEVERAGES",
							"S4BIOTX:MID BIOTECH",
							"S4BUILX:MID BLDG PRODS",
							"S4CAPM:MID CAPITAL MKTS",
							"S4CBNK:MID COMM BANKS",
							"S4CFINX:MID CONS FINANCE",
							"S4CHEM:MID CHEMICALS",
							"S4CMPE:MID COMPUTERS & PER",
							"S4COMM:MID COMMUNICATION EQP",
							"S4COMSX:MID COMMERCIAL SRBS",
							"S4CONP:MID CONTAINER & PKG",
							"S4CSTEX:MID CONST & ENG",
							"S4CSTMX:MID CONST MATERIAL",
							"S4DCON:MID DIVERSIFIED SRVC",
							"S4DISTX:MID DISBRIBUTORS",
							"S4DIVT:MID DIV TEL SVC",
							"S4DVFS:MID DIV FIN SVC",
							"S4ELEIX:MID ELECTRONIC EQUP",
							"S4ELEQ:MID ELECTRICAL EQUP",
							"S4ELUTX:MID ELECTRIC UTL",
							"S4ENRE:MID ENERGY EQUP & SV",
							"S4FDPR:MID FOOD PROD IND",
							"S4FDSRX:MID FOOD & STAPLES RET",
							"S4GASUX:MID GAS UTL",
							"S4HCEQ:MID HC EQUP & SUP",
							"S4HCPS:MID HC PROVIDERS SVC",
							"S4HCTEX:MID HC TECHNOLOGY",
							"S4HODU:MID HOUSEHOLD DURABLES",
							"S4HOPRX:MID HOUSELHOLD PROD",
							"S4HOTRX:MID HOTELS REST & LEIS",
							"S4INCR:MID INTERNET CATALOG",
							"S4INDCX:MID INDUSTRIAL CONGL",
							"S4INSSX:MID INTERNET SOFTWARE",
							"S4INSUX:MID INSUURANCE IND",
							"S4ITSV:MID IT SERV IND",
							"S4LEIS:MID LEISURE EQUP",
							"S4LSTSX:MID LIFE SCI IND",
							"S4MACH:MID MACHINERY",
							"S4MARIX:MID MARINE",
							"S4MDREX:MID RE MGM",
							"S4MEDAX:MID MEDIA",
							"S4METL:MID METAL & MIN",
							"S4MRET:MID MULTILINE RET",
							"S4MUTIX:MID MULTI UTL",
							"S4OFFEX:MID OFFICE ELECT",
							"S4OILG:MID OIL GAS FUEL",
							"S4PAFO:MID PAPER FORSET PROD",
							"S4PHARX:MID PHARMA",
							"S4PRSV:MID PROF SRVS",
							"S4REITS:MID RE INV TRUSTS",
							"S4ROAD:MID ROARD & RAIL",
							"S4SOFT:MID SOFTWARE",
							"S4SPRE:MID SPECIALTY RET",
							"S4SSEQ:MID SEMICOND & EQUP",
							"S4TEXA:MID TXTL & APPRL",
							"S4THMFX:MID THRIFTS & MORT",
							"S4TOBAX:MID TOBACCO",
							"S4TRADX:MID TRADING CO & DIS",
							"S4WATUX:MID WATER UTL",
							"S4WIREX:MID WIRELESS TELECOM"

						});
						ok = true;
					}

					else if (subgroupSymbol == "MIDL4")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"MIDEWI:MID Equal Weighted Index >",
						});

						ok = true;
					}
				}

				if (groupSymbol == "SML")
				{
					if (subgroupSymbol == "SMLL1")
					{
						if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
							"S6COND:SML CONS DISC >",
							"S6CONS:SML CONS STAPLES >",
							"S6ENRS:SML ENERGY >",
							"S6FINL:SML FINANCIALS >",
							"S6HLTH:SML HEALTH CARE >",
							"S6INDU:SML INDUSTRIALS >",
							"S6INFT:SML INFO TECH >",
							"S6MATR:SML MATERIALS >",
							"S6TELS:SML TELECOM >",
							"S6UTIL:SML UTILITIES >"
						 });

						else setNavigation(panel, mouseDownEvent, new string[] {
							"S6COND:SML CONS DISC",
							"S6CONS:SML CONS STAPLES",
							"S6ENRS:SML ENERGY",
							"S6FINL:SML FINANCIALS",
							"S6HLTH:SML HEALTH CARE",
							"S6INDU:SML INDUSTRIALS",
							"S6INFT:SML INFO TECH",
							"S6MATR:SML MATERIALS",
							"S6TELS:SML TELECOM",
							"S6UTIL:SML UTILITIES"
						});
						ok = true;
					}

					else if (subgroupSymbol == "SMLL2")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"S6AUCO:SML AUTO & COMP >",
							"S6BANKX:SML BANKS >",
							"S6CODU:SML CAPITAL GOODS >",
							"S6COMS:SML COMM & PROF >",
							"S6CPGS:SML CON DUR & AP >",
							"S6DIVF:SML DIV FINANCE >",
							"S6ENRSX:SML ENERGY >",
							"S6FDBT:SML FOOD/STAPLES >",
							"S6FDSR:SML FOOD BEV TOB >",
							"S6HCES:SML HC EQUIP >",
							"S6HOTR:SML CONS SERV >",
							"S6HOUS:SML HOUSEHOLD PROD >",
							"S6INSU:SML INSURANCE >",
							"S6MATRX:SML MATERIALS >",
							"S6MEDA:SML MEDIA >",
							"S6PHRM:SML PHRM BIO & LIFE >",
							"S6REAL:SML REAL ESTATE >",
							"S6RETL:SML RETAILING >",
							"S6SSEQX:SML SEMI & EQP >",
							"S6SFTW:SML SOFTWARE & SVCS >",
							"S6TECH:SML TECH HW & EQP >",
							"S6TELSX:SML TELECOM SVCS >",
							"S6TRAN:SML TRANSPORT >",
							"S6UTILX:SML UTILTIES >"
						});
						ok = true;
					}

					else if (subgroupSymbol == "SMLL3")
					{
						if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
							"S6AEROX:SML AERO & DEF >",
							"S6AIRFX:SML AIR FRT & LOG >",
							"S6AIRLX:SML AIRLINES >",
							"S6AUTC:SML AUTO COMP >",
							"S6AUTO:SML AUTOMOBILES >",
							"S6BEVG:SML BEVERAGES >",
							"S6BIOTX:SML BIOTECH >",
							"S6BUILX:SML BLDG PRODS >",
							"S6CAPM:SML CAPITAL MKTS >",
							"S6CBNK:SML COMM BANKS >",
							"S6CFINX:SML CONS FINANCE >",
							"S6CHEM:SML CHEMICALS >",
							"S6CMPE:SML COMPUTERS & PER >",
							"S6COMM:SML COMMUNICATION EQP >",
							"S6COMSX:SML COMMERCIAL SRBS >",
							"S6CONP:SML CONTAINER & PKG >",
							"S6CSTEX:SML CONST & ENG >",
							"S6CSTMX:SML CONST MATERIAL >",
							"S6DCON:SML DIVERSIFIED SRVC >",
							"S6DISTX:SML DISBRIBUTORS >",
							"S6DIVT:SML DIV TEL SVC >",
							"S6DVFS:SML DIV FIN SVC >",
							"S6ELEIX:SML ELECTRONIC EQUP >",
							"S6ELEQ:SML ELECTRICAL EQUP >",
							"S6ELUTX:SML ELECTRIC UTL >",
							"S6ENRE:SML ENERGY EQUP & SV >",
							"S6FDPR:SML FOOD PROD IND >",
							"S6FDSRX:SML FOOD & STAPLES RET >",
							"S6GASUX:SML GAS UTL >",
							"S6HCEQ:SML HC EQUP & SUP >",
							"S6HCPS:SML HC PROVIDERS SVC >",
							"S6HCTEX:SML HC TECHNOLOGY >",
							"S6HODU:SML HOUSEHOLD DURABLES >",
							"S6HOPRX:SML HOUSELHOLD PROD >",
							"S6HOTRX:SML HOTELS REST & LEIS >",
							"S6INCR:SML INTERNET CATALOG >",
							"S6INDCX:SML INDUSTRIAL CONGL >",
							"S6INSSX:SML INTERNET SOFTWARE >",
							"S6INSUX:SML INSUURANCE IND >",
							"S6ITSV:SML IT SERV IND >",
							"S6LEIS:SML LEISURE EQUP >",
							"S6LSTSX:SML LIFE SCI IND >",
							"S6MACH:SML MACHINERY >",
							"S6MARIX:SML MARINE >",
							"S6MDREX:SML RE MGM >",
							"S6MEDAX:SML MEDIA >",
							"S6METL:SML METAL & MIN >",
							"S6MRET:SML MULTILINE RET >",
							"S6MUTIX:SML MULTI UTL >",
							"S6OILG:SML OIL GAS FUEL >",
							"S6PAFO:SML PAPER FORSET PROD >",
							"S6PERSX:SML PERSONAL PROD >",
							"S6PHARX:SML PHARMA >",
							"S6PRSV:SML PROF SRVS >",
							"S6REITS:SML RE INV TRUSTS >",
							"S6ROAD:SML ROARD & RAIL >",
							"S6SOFT:SML SOFTWARE >",
							"S6SPRE:SML SPECIALTY RET >",
							"S6SSEQ:SML SEMICOND & EQUP >",
							"S6TEXA:SML TXTL & APPRL >",
							"S6THMFX:SML THRIFTS & MORT >",
							"S6TOBAX:SML TOBACCO >",
							"S6TRADX:SML TRADING CO & DIS >",
							"S6WATUX:SML WATER UTL >",
							"S6WIREX:SML WIRELESS TELECOM >"
						 });

						else setNavigation(panel, mouseDownEvent, new string[] {
							"S6AEROX:SML AERO & DEF",
							"S6AIRFX:SML AIR FRT & LOG",
							"S6AIRLX:SML AIRLINES",
							"S6AUTC:SML AUTO COMP",
							"S6AUTO:SML AUTOMOBILES",
							"S6BEVG:SML BEVERAGES",
							"S6BIOTX:SML BIOTECH",
							"S6BUILX:SML BLDG PRODS",
							"S6CAPM:SML CAPITAL MKTS",
							"S6CBNK:SML COMM BANKS",
							"S6CFINX:SML CONS FINANCE",
							"S6CHEM:SML CHEMICALS",
							"S6CMPE:SML COMPUTERS & PER",
							"S6COMM:SML COMMUNICATION EQP",
							"S6COMSX:SML COMMERCIAL SRBS",
							"S6CONP:SML CONTAINER & PKG",
							"S6CSTEX:SML CONST & ENG",
							"S6CSTMX:SML CONST MATERIAL",
							"S6DCON:SML DIVERSIFIED SRVC",
							"S6DISTX:SML DISBRIBUTORS",
							"S6DIVT:SML DIV TEL SVC",
							"S6DVFS:SML DIV FIN SVC",
							"S6ELEIX:SML ELECTRONIC EQUP",
							"S6ELEQ:SML ELECTRICAL EQUP",
							"S6ELUTX:SML ELECTRIC UTL",
							"S6ENRE:SML ENERGY EQUP & SV",
							"S6FDPR:SML FOOD PROD IND",
							"S6FDSRX:SML FOOD & STAPLES RET",
							"S6GASUX:SML GAS UTL",
							"S6HCEQ:SML HC EQUP & SUP",
							"S6HCPS:SML HC PROVIDERS SVC",
							"S6HCTEX:SML HC TECHNOLOGY",
							"S6HODU:SML HOUSEHOLD DURABLES",
							"S6HOPRX:SML HOUSELHOLD PROD",
							"S6HOTRX:SML HOTELS REST & LEIS",
							"S6INCR:SML INTERNET CATALOG",
							"S6INDCX:SML INDUSTRIAL CONGL",
							"S6INSSX:SML INTERNET SOFTWARE",
							"S6INSUX:SML INSUURANCE IND",
							"S6ITSV:SML IT SERV IND",
							"S6LEIS:SML LEISURE EQUP",
							"S6LSTSX:SML LIFE SCI IND",
							"S6MACH:SML MACHINERY",
							"S6MARIX:SML MARINE",
							"S6MDREX:SML RE MGM",
							"S6MEDAX:SML MEDIA",
							"S6METL:SML METAL & MIN",
							"S6MRET:SML MULTILINE RET",
							"S6MUTIX:SML MULTI UTL",
							"S6OILG:SML OIL GAS FUEL",
							"S6PAFO:SML PAPER FORSET PROD",
							"S6PERSX:SML PERSONAL PROD",
							"S6PHARX:SML PHARMA",
							"S6PRSV:SML PROF SRVS",
							"S6REITS:SML RE INV TRUSTS",
							"S6ROAD:SML ROARD & RAIL",
							"S6SOFT:SML SOFTWARE",
							"S6SPRE:SML SPECIALTY RET",
							"S6SSEQ:SML SEMICOND & EQUP",
							"S6TEXA:SML TXTL & APPRL",
							"S6THMFX:SML THRIFTS & MORT",
							"S6TOBAX:SML TOBACCO",
							"S6TRADX:SML TRADING CO & DIS",
							"S6WATUX:SML WATER UTL",
							"S6WIREX:SML WIRELESS TELECOM"
						});
						ok = true;
					}
				}
				if (groupSymbol == "SPR")
				{
					if (subgroupSymbol == "SPRL1")
					{
						if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
							"S15COND:SPR CONS DISC >",
							"S15CONS:SPR CONS STAPLES >",
							"S15ENRS:SPR ENERGY >",
							"S15FINL:SPR FINANCIALS >",
							"S15HLTH:SPR HEALTH CARE >",
							"S15INDU:SPR INDUSTRIALS >",
							"S15INFT:SPR INFO TECH >",
							"S15MATR:SPR MATERIALS >",
							"S15TELS:SPR TELECOM >",
							"S15UTIL:SPR UTILITIES >"
						 });

						else setNavigation(panel, mouseDownEvent, new string[] {
							"S15COND:SPR CONS DISC",
							"S15CONS:SPR CONS STAPLES",
							"S15ENRS:SPR ENERGY",
							"S15FINL:SPR FINANCIALS",
							"S15HLTH:SPR HEALTH CARE",
							"S15INDU:SPR INDUSTRIALS",
							"S15INFT:SPR INFO TECH",
							"S15MATR:SPR MATERIALS",
							"S15TELS:SPR TELECOM",
							"S15UTIL:SPR UTILITIES"

						});
						ok = true;
					}

					else if (subgroupSymbol == "SPRL2")
					{
						if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
							"S15AUCO:SPR AUTO & COMP >",
							"S15BANKX:SPR BANKS >",
							"S15CODU:SPR CAPITAL GOODS >",
							"S15COMS:SPR COMM & PROF >",
							"S15CPGS:SPR CON DUR & AP >",
							"S15DIVF:SPR DIV FINANCE >",
							"S15ENRSX:SPR ENERGY >",
							"S15FDBT:SPR FOOD/STAPLES >",
							"S15FDSR:SPR FOOD BEV TOB >",
							"S15HCES:SPR HC EQUIP >",
							"S15HOTR:SPR CONS SERV >",
							"S15HOUS:SPR HOUSEHOLD PROD >",
							"S15INSU:SPR INSURANCE >",
							"S15MATRX:SPR MATERIALS >",
							"S15MEDA:SPR MEDIA >",
							"S15PHRM:SPR PHRM BIO & LIFE >",
							"S15REAL:SPR REAL ESTATE >",
							"S15RETL:SPR RETAILING >",
							"S15SSEQX:SPR SEMI & EQP >",
							"S15SFTW:SPR SOFTWARE & SVCS >",
							"S15TECH:SPR TECH HW & EQP >",
							"S15TELSX:SPR TELECOM SVCS >",
							"S15TRAN:SPR TRANSPORT >",
							"S15UTILX:SPR UTILTIES >"
						 });

						else setNavigation(panel, mouseDownEvent, new string[] {
							"S15AUCO:SPR AUTO & COMP",
							"S15BANKX:SPR BANKS",
							"S15CODU:SPR CAPITAL GOODS",
							"S15COMS:SPR COMM & PROF",
							"S15CPGS:SPR CON DUR & AP",
							"S15DIVF:SPR DIV FINANCE",
							"S15ENRSX:SPR ENERGY",
							"S15FDBT:SPR FOOD/STAPLES",
							"S15FDSR:SPR FOOD BEV TOB",
							"S15HCES:SPR HC EQUIP",
							"S15HOTR:SPR CONS SERV",
							"S15HOUS:SPR HOUSEHOLD PROD",
							"S15INSU:SPR INSURANCE",
							"S15MATRX:SPR MATERIALS",
							"S15MEDA:SPR MEDIA",
							"S15PHRM:SPR PHRM BIO & LIFE",
							"S15REAL:SPR REAL ESTATE",
							"S15RETL:SPR RETAILING",
							"S15SSEQX:SPR SEMI & EQP",
							"S15SFTW:SPR SOFTWARE & SVCS",
							"S15TECH:SPR TECH HW & EQP",
							"S15TELSX:SPR TELECOM SVCS",
							"S15TRAN:SPR TRANSPORT",
							"S15UTILX:SPR UTILTIES"

						});
						ok = true;
					}

					else if (subgroupSymbol == "SPRL3")
					{
						if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
							"S15AEROX:SPR AERO & DEF >",
							"S15AIRFX:SPR AIR FRT & LOG >",
							"S15AIRLX:SPR AIRLINES >",
							"S15AUTC:SPR AUTO COMP >",
							"S15AUTO:SPR AUTOMOBILES >",
							"S15BEVG:SPR BEVERAGES >",
							"S15BIOTX:SPR BIOTECH >",
							"S15BUILX:SPR BLDG PRODS >",
							"S15CAPM:SPR CAPITAL MKTS >",
							"S15CBNK:SPR COMM BANKS >",
							"S15CFINX:SPR CONS FINANCE >",
							"S15CHEM:SPR CHEMICALS >",
							"S15CMPE:SPR COMPUTERS & PER >",
							"S15COMM:SPR COMMUNICATION EQP >",
							"S15COMSX:SPR COMMERCIAL SRBS >",
							"S15CONP:SPR CONTAINER & PKG >",
							"S15CSTEX:SPR CONST & ENG >",
							"S15CSTMX:SPR CONST MATERIAL >",
							"S15DCON:SPR DIVERSIFIED SRVC >",
							"S15DISTX:SPR DISBRIBUTORS >",
							"S15DIVT:SPR DIV TEL SVC >",
							"S15DVFS:SPR DIV FIN SVC >",
							"S15ELEIX:SPR ELECTRONIC EQUP >",
							"S15ELEQ:SPR ELECTRICAL EQUP >",
							"S15ELUTX:SPR ELECTRIC UTL >",
							"S15ENRE:SPR ENERGY EQUP & SV >",
							"S15FDPR:SPR FOOD PROD IND >",
							"S15FDSRX:SPR FOOD & STAPLES RET >",
							"S15GASUX:SPR GAS UTL >",
							"S15HCEQ:SPR HC EQUP & SUP >",
							"S15HCPS:SPR HC PROVIDERS SVC >",
							"S15HCTEX:SPR HC TECHNOLOGY >",
							"S15HODU:SPR HOUSEHOLD DURABLES >",
							"S15HOPRX:SPR HOUSELHOLD PROD >",
							"S15HOTRX:SPR HOTELS REST & LEIS >",
							"S15INCR:SPR INTERNET CATALOG >",
							"S15INDCX:SPR INDUSTRIAL CONGL >",
							"S15INSSX:SPR INTERNET SOFTWARE >",
							"S15INSUX:SPR INSUURANCE IND >",
							"S15IPPEX:SPR INDEP PWR PROD >",
							"S15ITSV:SPR IT SERV IND >",
							"S15LEIS:SPR LEISURE EQUP >",
							"S15LSTSX:SPR LIFE SCI IND >",
							"S15MACH:SPR MACHINERY >",
							"S15MARIX:SPR MARINE >",
							"S15MDREX:SPR RE MGM >",
							"S15MEDAX:SPR MEDIA >",
							"S15METL:SPR METAL & MIN >",
							"S15MRET:SPR MULTILINE RET >",
							"S15MUTIX:SPR MULTI UTL >",
							"S15OFFEX:SPR OFFICE ELECT >",
							"S15OILG:SPR OIL GAS FUEL >",
							"S15PAFO:SPR PAPER FORSET PROD >",
							"S15PERSX:SPR PERSONAL PROD >",
							"S15PHARX:SPR PHARMA >",
							"S15PRSV:SPR PROF SRVS >",
							"S15REITS:SPR RE INV TRUSTS >",
							"S15ROAD:SPR ROARD & RAIL >",
							"S15SOFT:SPR SOFTWARE >",
							"S15SPRE:SPR SPECIALTY RET >",
							"S15SSEQ:SPR SEMICOND & EQUP >",
							"S15TEXA:SPR TXTL & APPRL >",
							"S15THMFX:SPR THRIFTS & MORT >",
							"S15TOBAX:SPR TOBACCO >",
							"S15TRADX:SPR TRADING CO & DIS >",
							"S15WATUX:SPR WATER UTL >",
							"S15WIREX:SPR WIRELESS TELECOM >"
						 });

						else setNavigation(panel, mouseDownEvent, new string[] {
							"S15AEROX:SPR AERO & DEF",
							"S15AIRFX:SPR AIR FRT & LOG",
							"S15AIRLX:SPR AIRLINES",
							"S15AUTC:SPR AUTO COMP",
							"S15AUTO:SPR AUTOMOBILES",
							"S15BEVG:SPR BEVERAGES",
							"S15BIOTX:SPR BIOTECH",
							"S15BUILX:SPR BLDG PRODS",
							"S15CAPM:SPR CAPITAL MKTS",
							"S15CBNK:SPR COMM BANKS",
							"S15CFINX:SPR CONS FINANCE",
							"S15CHEM:SPR CHEMICALS",
							"S15CMPE:SPR COMPUTERS & PER",
							"S15COMM:SPR COMMUNICATION EQP",
							"S15COMSX:SPR COMMERCIAL SRBS",
							"S15CONP:SPR CONTAINER & PKG",
							"S15CSTEX:SPR CONST & ENG",
							"S15CSTMX:SPR CONST MATERIAL",
							"S15DCON:SPR DIVERSIFIED SRVC",
							"S15DISTX:SPR DISBRIBUTORS",
							"S15DIVT:SPR DIV TEL SVC",
							"S15DVFS:SPR DIV FIN SVC",
							"S15ELEIX:SPR ELECTRONIC EQUP",
							"S15ELEQ:SPR ELECTRICAL EQUP",
							"S15ELUTX:SPR ELECTRIC UTL",
							"S15ENRE:SPR ENERGY EQUP & SV",
							"S15FDPR:SPR FOOD PROD IND",
							"S15FDSRX:SPR FOOD & STAPLES RET",
							"S15GASUX:SPR GAS UTL",
							"S15HCEQ:SPR HC EQUP & SUP",
							"S15HCPS:SPR HC PROVIDERS SVC",
							"S15HCTEX:SPR HC TECHNOLOGY",
							"S15HODU:SPR HOUSEHOLD DURABLES",
							"S15HOPRX:SPR HOUSELHOLD PROD",
							"S15HOTRX:SPR HOTELS REST & LEIS",
							"S15INCR:SPR INTERNET CATALOG",
							"S15INDCX:SPR INDUSTRIAL CONGL",
							"S15INSSX:SPR INTERNET SOFTWARE",
							"S15INSUX:SPR INSUURANCE IND",
							"S15IPPEX:SPR INDEP PWR PROD",
							"S15ITSV:SPR IT SERV IND",
							"S15LEIS:SPR LEISURE EQUP",
							"S15LSTSX:SPR LIFE SCI IND",
							"S15MACH:SPR MACHINERY",
							"S15MARIX:SPR MARINE",
							"S15MDREX:SPR RE MGM",
							"S15MEDAX:SPR MEDIA",
							"S15METL:SPR METAL & MIN",
							"S15MRET:SPR MULTILINE RET",
							"S15MUTIX:SPR MULTI UTL",
							"S15OFFEX:SPR OFFICE ELECT",
							"S15OILG:SPR OIL GAS FUEL",
							"S15PAFO:SPR PAPER FORSET PROD",
							"S15PERSX:SPR PERSONAL PROD",
							"S15PHARX:SPR PHARMA",
							"S15PRSV:SPR PROF SRVS",
							"S15REITS:SPR RE INV TRUSTS",
							"S15ROAD:SPR ROARD & RAIL",
							"S15SOFT:SPR SOFTWARE",
							"S15SPRE:SPR SPECIALTY RET",
							"S15SSEQ:SPR SEMICOND & EQUP",
							"S15TEXA:SPR TXTL & APPRL",
							"S15THMFX:SPR THRIFTS & MORT",
							"S15TOBAX:SPR TOBACCO",
							"S15TRADX:SPR TRADING CO & DIS",
							"S15WATUX:SPR WATER UTL",
							"S15WIREX:SPR WIRELESS TELECOM"
						});
						ok = true;
					}

					else if (subgroupSymbol == "SPRL4")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"SPRCEWUP:SPR Equal Weighted Index >"
						});

						ok = true;
					}
				}

				if (subgroupSymbol == "NDXL1")
				{
					if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
							"CBNK:NDX BANK >",
							"NBI:NDX BIOTECHNOLOGY >",
							"IXK:NDX COMPUTER >",
							"NDF:NDX FINANCIAL >",
							"CIND:NDX INDUSTRIAL >",
							"CINS:NDX INSURANCE >",
							"CUTL:NDX TELECOMMUNICATION >",
							"CTRN:NDX TRANSPORTATION >"
						 });

					else setNavigation(panel, mouseDownEvent, new string[] {
							"CBNK:NDX BANK",
							"NBI:NDX BIOTECHNOLOGY",
							"IXK:NDX COMPUTER",
							"NDF:NDX FINANCIAL",
							"CIND:NDX INDUSTRIAL",
							"CINS:NDX INSURANCE",
							"CUTL:NDX TELECOMMUNICATION",
							"CTRN:NDX TRANSPORTATION"
						});
					ok = true;
				}

				if (groupSymbol == "RIY")  //Russell 1000
				{
					if (subgroupSymbol == "RIYL1")
					{
						if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
							"RGUSDL:RIY CONS DISC >",
							"RGUSSL:RIY CONS STAPLES >",
							"RGUSEL:RIY ENERGY >",
							"RGUSFL:RIY FIN SERVICES >",
							"RGUSHL:RIY HEALTH CARE >",
							"RGUSML:RIY MATERIALS >",
							"RGUSPL:RIY PROD DURABLES >",
							"RGUSTL:RIY TECHNOLOGY >",
							"RGUSUL:RIY UTILITIES >"
						 });

						else setNavigation(panel, mouseDownEvent, new string[] {
							"RGUSDL:RIY CONS DISC",
							"RGUSSL:RIY CONS STAPLES",
							"RGUSEL:RIY ENERGY",
							"RGUSFL:RIY FIN SERVICES",
							"RGUSHL:RIY HEALTH CARE",
							"RGUSML:RIY MATERIALS",
							"RGUSPL:RIY PROD DURABLES",
							"RGUSTL:RIY TECHNOLOGY",
							"RGUSUL:RIY UTILITIES"
						});
						ok = true;
					}

					else if (subgroupSymbol == "RIYL2")
					{
						if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
							"RGUSDLAA:RIY AD AGENCIES >",
							"RGUSPLAS:RIY AEROSPACE >",
							"RGUSPLAI:RIY AIR TRANSPORT >",
							"RGUSMLAL:RIY ALUMINUM >",
							"RGUSFLAM:RIY ASSET MGT & CUST >",
							"RGUSDLAP:RIY AUTO PARTS >",
							"RGUSDLAS:RIY AUTO SVC >",
							"RGUSDLAU:RIY AUTOMOBILES >",
							"RGUSFLBK:RIY BANKS DVSFD >",
							"RGUSSLBD:RIY BEV BRW & DSTLR >",
							"RGUSSLSD:RIY BEV SOFT DRNK >",
							"RGUSHLBT:RIY BIOTEC >",
							"RGUSMLCC:RIY BLDG CLIMATE CTRL >",
							"RGUSFLBS:RIY BNK SVG THRF MRT >",
							"RGUSPLBO:RIY BO SUP HR & CONS >",
							"RGUSMLBM:RIY BUILDING MATL >",
							"RGUSDLCT:RIY CABLE TV SVC >",
							"RGUSDLCG:RIY CASINOS & GAMB >",
							"RGUSMLCM:RIY CEMENT >",
							"RGUSMLCS:RIY CHEM SPEC >",
							"RGUSMLCD:RIY CHEM DVFSD >",
							"RGUSTLCS:RIY CMP SVC SFW & SYS >",
							"RGUSELCO:RIY COAL >",
							"RGUSPLCS:RIY COMM SVC >",
							"RGUSFLFM:RIY COMM FIN & MORT >",
							"RGUSPLCL:RIY COMM SVC RN >",
							"RGUSTLCM:RIY COMM TECH >",
							"RGUSPLCV:RIY COMM VEH & PRTS >",
							"RGUSTLCT:RIY COMPUTER TECH >",
							"RGUSPLCN:RIY CONS >",
							"RGUSDLCM:RIY CONS SVC  MISC >",
							"RGUSFLCL:RIY CONSUMER LEND >",
							"RGUSDLCE:RIY CONSUMER ELECT >",
							"RGUSMLCP:RIY CONTAINER & PKG >",
							"RGUSMLCR:RIY COPPER >",
							"RGUSDLCS:RIY COSMETICS >",
							"RGUSSLDG:RIY DRUG & GROC CHN >",
							"RGUSFLDF:RIY DVSFD FNCL SVC >",
							"RGUSDLDM:RIY DVSFD MEDIA >",
							"RGUSDLDR:RIY DVSFD RETAIL >",
							"RGUSMLDM:RIY DVSFD MAT & PROC >",
							"RGUSPLDO:RIY DVSFD MFG OPS >",
							"RGUSDLES:RIY EDUCATION SVC >",
							"RGUSTLEC:RIY ELECT COMP >",
							"RGUSTLEE:RIY ELECT ENT >",
							"RGUSTLEL:RIY ELECTRONICS >",
							"RGUSELEQ:RIY ENERGY EQ >",
							"RGUSPLEC:RIY ENG & CONTR SVC >",
							"RGUSDLEN:RIY ENTERTAINMENT >",
							"RGUSPLEN:RIY ENV MN & SEC SVC >",
							"RGUSMLFT:RIY FERTILIZERS >",
							"RGUSFLFD:RIY FINCL DATA & SYS >",
							"RGUSSLFO:RIY FOODS >",
							"RGUSMLFP:RIY FOREST PROD >",
							"RGUSPLFB:RIY FRM & BLK PRNT SVC >",
							"RGUSSLFG:RIY FRUIT & GRN PROC >",
							"RGUSDLFU:RIY FUN PARLOR & CEM >",
							"RGUSELGP:RIY GAS PIPELINE >",
							"RGUSMLGO:RIY GOLD >",
							"RGUSDLHE:RIY HHLD EQP & PROD >",
							"RGUSDLHF:RIY HHLD FURN >",
							"RGUSHLHF:RIY HLTH CARE FAC >",
							"RGUSHLHS:RIY HLTH CARE SVC >",
							"RGUSHLHM:RIY HLTH C MGT SVC >",
							"RGUSDLHB:RIY HOMEBUILDING >",
							"RGUSDLHO:RIY HOTEL/MOTEL >",
							"RGUSDLHA:RIY HOUSEHOLD APPL >",
							"RGUSFLIL:RIY INS LIFE >",
							"RGUSFLIM:RIY INS MULTI-LN >",
							"RGUSFLIP:RIY INS PROP-CAS >",
							"RGUSPLIT:RIY INTL TRD & DV LG >",
							"RGUSDLLT:RIY LEISURE TIME >",
							"RGUSDLLX:RIY LUXURY ITEMS >",
							"RGUSPLMI:RIY MACH INDU >",
							"RGUSPLMT:RIY MACH TOOLS >",
							"RGUSPLMA:RIY MACH AG >",
							"RGUSPLME:RIY MACH ENGINES >",
							"RGUSPLMS:RIY MACH SPECIAL >",
							"RGUSPLMH:RIY MCH CONS & HNDL >",
							"RGUSHLMD:RIY MD & DN INS & SUP >",
							"RGUSHLME:RIY MED EQ >",
							"RGUSHLMS:RIY MED SVC >",
							"RGUSMLMD:RIY MET & MIN DVFSD >",
							"RGUSMLMF:RIY METAL FABRIC >",
							"RGUSSLMC:RIY MISC CONS STAPLE >",
							"RGUSPLOE:RIY OFF SUP & EQ >",
							"RGUSELOF:RIY OFFSHORE DRILL >",
							"RGUSELOI:RIY OIL INTEGRATE >",
							"RGUSELOC:RIY OIL CRUDE PROD >",
							"RGUSELOR:RIY OIL REF & MKT >",
							"RGUSELOW:RIY OIL WELL EQ & SVC >",
							"RGUSMLPC:RIY PAINT & COATING >",
							"RGUSMLPA:RIY PAPER >",
							"RGUSSLPC:RIY PERSONAL CARE >",
							"RGUSHLPH:RIY PHRM >",
							"RGUSPLPD:RIY PROD DUR MISC >",
							"RGUSTLPR:RIY PRODUCT TECH EQ >",
							"RGUSDLPU:RIY PUBLISHING >",
							"RGUSPLPT:RIY PWR TRANSM EQ >",
							"RGUSDLRB:RIY RADIO & TV BROAD >",
							"RGUSPLRL:RIY RAILROAD EQ >",
							"RGUSPLRA:RIY RAILROADS >",
							"RGUSFLRE:RIY REAL ESTATE >",
							"RGUSFLRI:RIY REIT >",
							"RGUSDLRT:RIY RESTAURANTS >",
							"RGUSMLRW:RIY ROOF WALL & PLUM >",
							"RGUSDLRC:RIY RT & LS SVC CONS >",
							"RGUSDLRV:RIY RV & BOATS >",
							"RGUSPLSI:RIY SCI INS CTL & FLT >",
							"RGUSPLSP:RIY SCI INS POL CTRL >",
							"RGUSPLSG:RIY SCI INST GG & MTR >",
							"RGUSPLSE:RIY SCI INSTR ELEC >",
							"RGUSFLSB:RIY SEC BRKG & SVC >",
							"RGUSTLSC:RIY SEMI COND & COMP >",
							"RGUSPLSH:RIY SHIPPING >",
							"RGUSDLSR:RIY SPEC RET >",
							"RGUSMLST:RIY STEEL >",
							"RGUSTLTM:RIY TECHNO MISC >",
							"RGUSTLTE:RIY TELE EQ >",
							"RGUSDLTX:RIY TEXT APP & SHOES >",
							"RGUSSLTO:RIY TOBACCO >",
							"RGUSDLTY:RIY TOYS >",
							"RGUSPLTM:RIY TRANS MISC >",
							"RGUSPLTK:RIY TRUCKERS >",
							"RGUSULUM:RIY UTIL  MISC >",
							"RGUSULUE:RIY UTIL GAS DIST >",
							"RGUSULUT:RIY UTIL TELE >",
							"RGUSULUW:RIY UTIL WATER >"
						 });

						else setNavigation(panel, mouseDownEvent, new string[] {
							"RGUSDLAA:RIY AD AGENCIES",
							"RGUSPLAS:RIY AEROSPACE",
							"RGUSPLAI:RIY AIR TRANSPORT",
							"RGUSMLAL:RIY ALUMINUM",
							"RGUSFLAM:RIY ASSET MGT & CUST",
							"RGUSDLAP:RIY AUTO PARTS",
							"RGUSDLAS:RIY AUTO SVC",
							"RGUSDLAU:RIY AUTOMOBILES",
							"RGUSFLBK:RIY BANKS DVSFD",
							"RGUSSLBD:RIY BEV BRW & DSTLR",
							"RGUSSLSD:RIY BEV SOFT DRNK",
							"RGUSHLBT:RIY BIOTEC",
							"RGUSMLCC:RIY BLDG CLIMATE CTRL",
							"RGUSFLBS:RIY BNK SVG THRF MRT",
							"RGUSPLBO:RIY BO SUP HR & CONS",
							"RGUSMLBM:RIY BUILDING MATL",
							"RGUSDLCT:RIY CABLE TV SVC",
							"RGUSDLCG:RIY CASINOS & GAMB",
							"RGUSMLCM:RIY CEMENT",
							"RGUSMLCS:RIY CHEM SPEC",
							"RGUSMLCD:RIY CHEM DVFSD",
							"RGUSTLCS:RIY CMP SVC SFW & SYS",
							"RGUSELCO:RIY COAL",
							"RGUSPLCS:RIY COMM SVC",
							"RGUSFLFM:RIY COMM FIN & MORT",
							"RGUSPLCL:RIY COMM SVC RN",
							"RGUSTLCM:RIY COMM TECH",
							"RGUSPLCV:RIY COMM VEH & PRTS",
							"RGUSTLCT:RIY COMPUTER TECH",
							"RGUSPLCN:RIY CONS",
							"RGUSDLCM:RIY CONS SVC  MISC",
							"RGUSFLCL:RIY CONSUMER LEND",
							"RGUSDLCE:RIY CONSUMER ELECT",
							"RGUSMLCP:RIY CONTAINER & PKG",
							"RGUSMLCR:RIY COPPER",
							"RGUSDLCS:RIY COSMETICS",
							"RGUSSLDG:RIY DRUG & GROC CHN",
							"RGUSFLDF:RIY DVSFD FNCL SVC",
							"RGUSDLDM:RIY DVSFD MEDIA",
							"RGUSDLDR:RIY DVSFD RETAIL",
							"RGUSMLDM:RIY DVSFD MAT & PROC",
							"RGUSPLDO:RIY DVSFD MFG OPS",
							"RGUSDLES:RIY EDUCATION SVC",
							"RGUSTLEC:RIY ELECT COMP",
							"RGUSTLEE:RIY ELECT ENT",
							"RGUSTLEL:RIY ELECTRONICS",
							"RGUSELEQ:RIY ENERGY EQ",
							"RGUSPLEC:RIY ENG & CONTR SVC",
							"RGUSDLEN:RIY ENTERTAINMENT",
							"RGUSPLEN:RIY ENV MN & SEC SVC",
							"RGUSMLFT:RIY FERTILIZERS",
							"RGUSFLFD:RIY FINCL DATA & SYS",
							"RGUSSLFO:RIY FOODS",
							"RGUSMLFP:RIY FOREST PROD",
							"RGUSPLFB:RIY FRM & BLK PRNT SVC",
							"RGUSSLFG:RIY FRUIT & GRN PROC",
							"RGUSDLFU:RIY FUN PARLOR & CEM",
							"RGUSELGP:RIY GAS PIPELINE",
							"RGUSMLGO:RIY GOLD",
							"RGUSDLHE:RIY HHLD EQP & PROD",
							"RGUSDLHF:RIY HHLD FURN",
							"RGUSHLHF:RIY HLTH CARE FAC",
							"RGUSHLHS:RIY HLTH CARE SVC",
							"RGUSHLHM:RIY HLTH C MGT SVC",
							"RGUSDLHB:RIY HOMEBUILDING",
							"RGUSDLHO:RIY HOTEL/MOTEL",
							"RGUSDLHA:RIY HOUSEHOLD APPL",
							"RGUSFLIL:RIY INS LIFE",
							"RGUSFLIM:RIY INS MULTI-LN",
							"RGUSFLIP:RIY INS PROP-CAS",
							"RGUSPLIT:RIY INTL TRD & DV LG",
							"RGUSDLLT:RIY LEISURE TIME",
							"RGUSDLLX:RIY LUXURY ITEMS",
							"RGUSPLMI:RIY MACH INDU",
							"RGUSPLMT:RIY MACH TOOLS",
							"RGUSPLMA:RIY MACH AG",
							"RGUSPLME:RIY MACH ENGINES",
							"RGUSPLMS:RIY MACH SPECIAL",
							"RGUSPLMH:RIY MCH CONS & HNDL",
							"RGUSHLMD:RIY MD & DN INS & SUP",
							"RGUSHLME:RIY MED EQ",
							"RGUSHLMS:RIY MED SVC",
							"RGUSMLMD:RIY MET & MIN DVFSD",
							"RGUSMLMF:RIY METAL FABRIC",
							"RGUSSLMC:RIY MISC CONS STAPLE",
							"RGUSPLOE:RIY OFF SUP & EQ",
							"RGUSELOF:RIY OFFSHORE DRILL",
							"RGUSELOI:RIY OIL INTEGRATE",
							"RGUSELOC:RIY OIL CRUDE PROD",
							"RGUSELOR:RIY OIL REF & MKT",
							"RGUSELOW:RIY OIL WELL EQ & SVC",
							"RGUSMLPC:RIY PAINT & COATING",
							"RGUSMLPA:RIY PAPER",
							"RGUSSLPC:RIY PERSONAL CARE",
							"RGUSHLPH:RIY PHRM",
							"RGUSPLPD:RIY PROD DUR MISC",
							"RGUSTLPR:RIY PRODUCT TECH EQ",
							"RGUSDLPU:RIY PUBLISHING",
							"RGUSPLPT:RIY PWR TRANSM EQ",
							"RGUSDLRB:RIY RADIO & TV BROAD",
							"RGUSPLRL:RIY RAILROAD EQ",
							"RGUSPLRA:RIY RAILROADS",
							"RGUSFLRE:RIY REAL ESTATE",
							"RGUSFLRI:RIY REIT",
							"RGUSDLRT:RIY RESTAURANTS",
							"RGUSMLRW:RIY ROOF WALL & PLUM",
							"RGUSDLRC:RIY RT & LS SVC CONS",
							"RGUSDLRV:RIY RV & BOATS",
							"RGUSPLSI:RIY SCI INS CTL & FLT",
							"RGUSPLSP:RIY SCI INS POL CTRL",
							"RGUSPLSG:RIY SCI INST GG & MTR",
							"RGUSPLSE:RIY SCI INSTR ELEC",
							"RGUSFLSB:RIY SEC BRKG & SVC",
							"RGUSTLSC:RIY SEMI COND & COMP",
							"RGUSPLSH:RIY SHIPPING",
							"RGUSDLSR:RIY SPEC RET",
							"RGUSMLST:RIY STEEL",
							"RGUSTLTM:RIY TECHNO MISC",
							"RGUSTLTE:RIY TELE EQ",
							"RGUSDLTX:RIY TEXT APP & SHOES",
							"RGUSSLTO:RIY TOBACCO",
							"RGUSDLTY:RIY TOYS",
							"RGUSPLTM:RIY TRANS MISC",
							"RGUSPLTK:RIY TRUCKERS",
							"RGUSULUM:RIY UTIL  MISC",
							"RGUSULUE:RIY UTIL GAS DIST",
							"RGUSULUT:RIY UTIL TELE",
							"RGUSULUW:RIY UTIL WATER"
						});
						ok = true;
					}
				}

				if (groupSymbol == "RLG")  //Russell 1000 Growth
				{
					if (subgroupSymbol == "RLGL1")
					{
						if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
							"RGUSDLG:RLG CON DISC GROWTH >",
							"RGUSSLG:RLG CONS STAPLES GROWTH >",
							"RGUSELG:RLG ENERGY GROWTH >",
							"RGUSFLG:RLG FIN SERVICES GROWTH >",
							"RGUSHLG:RLG HEALTH CARE GROWTH >",
							"RGUSMLG:RLG MATERIALS GROWTH >",
							"RGUSPLG:RLG PROD DURABLES GROWTH >",
							"RGUSTLG:RLG TECHNOLOGY GROWTH >",
							"RGUSULG:RLG UTILITIES GROWTH >"
						 });

						else setNavigation(panel, mouseDownEvent, new string[] {
							"RGUSDLG:RLG CON DISC GROWTH",
							"RGUSSLG:RLG CONS STAPLES GROWTH",
							"RGUSELG:RLG ENERGY GROWTH",
							"RGUSFLG:RLG FIN SERVICES GROWTH",
							"RGUSHLG:RLG HEALTH CARE GROWTH",
							"RGUSMLG:RLG MATERIALS GROWTH",
							"RGUSPLG:RLG PROD DURABLES GROWTH",
							"RGUSTLG:RLG TECHNOLOGY GROWTH",
							"RGUSULG:RLG UTILITIES GROWTH"
						});
						ok = true;
					}

					else if (subgroupSymbol == "RLGL2")
					{
						if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
							"RGUDLAAG:RLG Ad Age Grw >",
							"RGUDLAPG:RLG AutoPrts Grw >",
							"RGUDLASG:RLG Auto Svc Grw >",
							"RGUDLAUG:RLG Automob Grw >",
							"RGUDLCEG:RLG ConsElect Grw >",
							"RGUDLCGG:RLG Cas&Gamb Grw >",
							"RGUDLCMG:RLG ConSvcMisc Grw >",
							"RGUDLCSG:RLG Cosmetics Grw >",
							"RGUDLCTG:RLG CabTV Svc Grw >",
							"RGUDLDMG:RLG Dvsfd Med Grw >",
							"RGUDLDRG:RLG Dvsfd Ret Grw >",
							"RGUDLENG:RLG Entertain Grw >",
							"RGUDLESG:RLG Edu Svc Grw >",
							"RGUDLFUG:RLG FuPar&Cem Grw >",
							"RGUDLHAG:RLG HholdAppl Grw >",
							"RGUDLHBG:RLG HomeBldg Grw >",
							"RGUDLHEG:RLG HHEq&Pro Grw >",
							"RGUDLHFG:RLG Hhld Furn Grw >",
							"RGUDLHOG:RLG Htel/Mtel Grw >",
							"RGUDLLTG:RLG Leis Time Grw >",
							"RGUDLLXG:RLG Lux Items Grw >",
							"RGUDLMHG:RLG Mfg Hous Grw >",
							"RGUDLPCG:RLG Prnt&CpySv Grw >",
							"RGUDLPHG:RLG Photograph Grw >",
							"RGUDLPUG:RLG Publishing Grw >",
							"RGUDLRBG:RLG Radio&TVBr Grw >",
							"RGUDLRCG:RLG R&LSv Con Grw >",
							"RGUDLRTG:RLG Restaurant Grw >",
							"RGUDLRVG:RLG RV & Boats Grw >",
							"RGUDLSFG:RLG StorageFac Grw >",
							"RGUDLSRG:RLG Spec Ret  Grw >",
							"RGUDLTXG:RLG TxtAp&Shoe Grw >",
							"RGUDLTYG:RLG Toys Grw >",
							"RGUDLVCG:RLG Vend&Cat Grw >",
							"RGUELAEG:RLG AlterEner Grw >",
							"RGUELCOG:RLG Coal Grw >",
							"RGUELEQG:RLG Energy Eq Grw >",
							"RGUELGPG:RLG Gas Pipe Grw >",
							"RGUELOCG:RLG Oil CrudPr Grw >",
							"RGUELOFG:RLG OffshrDri Grw >",
							"RGUELOIG:RLG Oil Integ Grw >",
							"RGUELORG:RLG Oil Ref&Mkt GR >",
							"RGUELOWG:RLG Oil WlEq&Sv Grw >",
							"RGUFLAMG:RLG AstMgt&Cs Grw >",
							"RGUFLBKG:RLG Bnks Dvfd Grw >",
							"RGUFLBSG:RLG BkSvThrfMt Grw >",
							"RGUFLCLG:RLG Cons Lend Grw >",
							"RGUFLDFG:RLG DvfFnlSvc Grw >",
							"RGUFLDRG:RLG DvsfREstateActG >",
							"RGUFLEDG:RLG EquityREIT DivG >",
							"RGUFLEFG:RLG EquityREIT InfG >",
							"RGUFLEHG:RLG EquityREIT HcaG >",
							"RGUFLEIG:RLG EquityREIT IndG >",
							"RGUFLELG:RLG EquityREIT L&RG >",
							"RGUFLEOG:RLG EquityREIT OffG >",
							"RGUFLERG:RLG EquityREIT ResG >",
							"RGUFLESG:RLG EquityREIT StoG >",
							"RGUFLFDG:RLG FinDat&Sy Grw >",
							"RGUFLFMG:RLG ComFn&Mtg Grw >",
							"RGUFLILG:RLG Ins Life Grw >",
							"RGUFLIMG:RLG Ins MulLn Grw >",
							"RGUFLIPG:RLG Ins ProCa Grw >",
							"RGUFLMCG:RLG MortgREIT ComG >",
							"RGUFLMDG:RLG MortgREIT DivG >",
							"RGUFLMSG:RLG MortgREIT ResG >",
							"RGUFLOSG:RLG EquityREIT OSpG >",
							"RGUFLRHG:RLG RealEstate H&DG >",
							"RGUFLRSG:RLG RealEstate SvcG >",
							"RGUFLRTG:RLG EquityREIT RetG >",
							"RGUFLSBG:RLG SecBrk&Svc Grw >",
							"RGUFLTIG:RLG EquityREIT TimG >",
							"RGUHLBTG:RLG Biotec Grw >",
							"RGUHLHCG:RLG HlthC Misc Grw >",
							"RGUHLHFG:RLG HC Fac Grw >",
							"RGUHLHMG:RLG HC MgtSvc Grw >",
							"RGUHLHSG:RLG HCare Svc Grw >",
							"RGUHLMDG:RLG M&DIns&Sp Grw >",
							"RGUHLMEG:RLG Med Eq Grw >",
							"RGUHLMSG:RLG Med Svc Grw >",
							"RGUHLPHG:RLG Phrm Grw >",
							"RGUMLALG:RLG Aluminum Grw >",
							"RGUMLBMG:RLG BldgMatl Grw >",
							"RGUMLCCG:RLG Bld ClCtr Grw >",
							"RGUMLCDG:RLG Chem Dvfd Grw >",
							"RGUMLCMG:RLG Cement Grw >",
							"RGUMLCPG:RLG Cont&Pkg Grw >",
							"RGUMLCRG:RLG Copper Grw >",
							"RGUMLCSG:RLG Chem Spec Grw >",
							"RGUMLDMG:RLG DvfMt&Prc Grw >",
							"RGUMLFPG:RLG ForestPrd Grw >",
							"RGUMLFTG:RLG Fertiliz Grw >",
							"RGUMLGLG:RLG Glass Grw >",
							"RGUMLGOG:RLG Gold Grw >",
							"RGUMLMDG:RLG Met&MinDvf Grw >",
							"RGUMLMFG:RLG MetFabric Grw >",
							"RGUMLPAG:RLG Paper Grw >",
							"RGUMLPCG:RLG Paint&Coat Grw >",
							"RGUMLPLG:RLG Plastics Grw >",
							"RGUMLPMG:RLG PrcMt&Min Grw >",
							"RGUMLRWG:RLG RfWal&Plm Grw >",
							"RGUMLSTG:RLG Steel Grw >",
							"RGUMLSYG:RLG SynFib&Chm Grw >",
							"RGUMLTPG:RLG Text Prod Grw >",
							"RGUPLAIG:RLG Air Trans Grw >",
							"RGUPLASG:RLG Aerospace Grw >",
							"RGUPLBOG:RLG BOSupHR&Cons GR >",
							"RGUPLCLG:RLG CommSvcR&L Grw >",
							"RGUPLCNG:RLG Cons Grw >",
							"RGUPLCVG:RLG CoVeh&Prt Grw >",
							"RGUPLDOG:RLG DvfMfgOps Grw >",
							"RGUPLECG:RLG Eng&CtrSv Grw >",
							"RGUPLENG:RLG EnvMn&SecSvc GR >",
							"RGUPLFBG:RLG FmBlkPrSv Grw >",
							"RGUPLITG:RLG IntlTrd&DvLg GR >",
							"RGUPLMAG:RLG Mach Ag Grw >",
							"RGUPLMEG:RLG Mach Eng Grw >",
							"RGUPLMHG:RLG Mch Cn&Hn Grw >",
							"RGUPLMIG:RLG Mach Indu Grw >",
							"RGUPLMSG:RLG Mach Spec Grw >",
							"RGUPLMTG:RLG Mach Tool Grw >",
							"RGUPLOEG:RLG OffSup&Eq Grw >",
							"RGUPLPDG:RLG PrdDr Misc Grw >",
							"RGUPLPTG:RLG PwrTranEq Grw >",
							"RGUPLRAG:RLG Railroads Grw >",
							"RGUPLRLG:RLG RailroadEq Grw >",
							"RGUPLSEG:RLG SciIn Elc Grw >",
							"RGUPLSGG:RLG ScInGg&Mt Grw >",
							"RGUPLSHG:RLG Shipping Grw >",
							"RGUPLSIG:RLG SciICt&Fl Grw >",
							"RGUPLSPG:RLG SciInPCtrl Grw >",
							"RGUPLTKG:RLG Truckers Grw >",
							"RGUPLTMG:RLG Trans Misc Grw >",
							"RGUSLAFG:RLG AgFsh&Ran Grw >",
							"RGUSLBDG:RLG Bev:Br&Ds Grw >",
							"RGUSLDGG:RLG Drg&GrcCh Grw >",
							"RGUSLFGG:RLG Fru&GrnPrc Grw >",
							"RGUSLFOG:RLG Foods Grw >",
							"RGUSLMCG:RLG MiscConsSt Grw >",
							"RGUSLPCG:RLG Pers Care Grw >",
							"RGUSLSDG:RLG Bev SftDr Grw >",
							"RGUSLSUG:RLG Sugar Grw >",
							"RGUSLTOG:RLG Tobacco Grw >",
							"RGUTLCMG:RLG Comm Tech Grw >",
							"RGUTLCSG:RLG CpSvSw&Sy Grw >",
							"RGUTLCTG:RLG Comp Tech Grw >",
							"RGUTLECG:RLG Elec Comp Grw >",
							"RGUTLEEG:RLG ElecEnt Grw >",
							"RGUTLELG:RLG Elec Grw >",
							"RGUTLPRG:RLG ProdTechEq Grw >",
							"RGUTLSCG:RLG SemiCn&Cp Grw >",
							"RGUTLTEG:RLG Tele Eq Grw >",
							"RGUTLTMG:RLG Tech Misc Grw >",
							"RGUULUEG:RLG Util Elec Grw >",
							"RGUULUGG:RLG Util GasDi Grw >",
							"RGUULUMG:RLG Util  Misc Grw >",
							"RGUULUTG:RLG Util Tele Grw >",
							"RGUULUWG:RLG Util Water Grw >"
						 });

						else setNavigation(panel, mouseDownEvent, new string[] {
							"RGUDLAAG:RLG Ad Age Grw",
							"RGUDLAPG:RLG AutoPrts Grw",
							"RGUDLASG:RLG Auto Svc Grw",
							"RGUDLAUG:RLG Automob Grw",
							"RGUDLCEG:RLG ConsElect Grw",
							"RGUDLCGG:RLG Cas&Gamb Grw",
							"RGUDLCMG:RLG ConSvcMisc Grw",
							"RGUDLCSG:RLG Cosmetics Grw",
							"RGUDLCTG:RLG CabTV Svc Grw",
							"RGUDLDMG:RLG Dvsfd Med Grw",
							"RGUDLDRG:RLG Dvsfd Ret Grw",
							"RGUDLENG:RLG Entertain Grw",
							"RGUDLESG:RLG Edu Svc Grw",
							"RGUDLFUG:RLG FuPar&Cem Grw",
							"RGUDLHAG:RLG HholdAppl Grw",
							"RGUDLHBG:RLG HomeBldg Grw",
							"RGUDLHEG:RLG HHEq&Pro Grw",
							"RGUDLHFG:RLG Hhld Furn Grw",
							"RGUDLHOG:RLG Htel Mtel Grw",
							"RGUDLLTG:RLG Leis Time Grw",
							"RGUDLLXG:RLG Lux Items Grw",
							"RGUDLMHG:RLG Mfg Hous Grw",
							"RGUDLPCG:RLG Prnt&CpySv Grw",
							"RGUDLPHG:RLG Photograph Grw",
							"RGUDLPUG:RLG Publishing Grw",
							"RGUDLRBG:RLG Radio&TVBr Grw",
							"RGUDLRCG:RLG R&LSv Con Grw",
							"RGUDLRTG:RLG Restaurant Grw",
							"RGUDLRVG:RLG RV & Boats Grw",
							"RGUDLSFG:RLG StorageFac Grw",
							"RGUDLSRG:RLG Spec Ret  Grw",
							"RGUDLTXG:RLG TxtAp&Shoe Grw",
							"RGUDLTYG:RLG Toys Grw",
							"RGUDLVCG:RLG Vend&Cat Grw",
							"RGUELAEG:RLG AlterEner Grw",
							"RGUELCOG:RLG Coal Grw",
							"RGUELEQG:RLG Energy Eq Grw",
							"RGUELGPG:RLG Gas Pipe Grw",
							"RGUELOCG:RLG Oil CrudPr Grw",
							"RGUELOFG:RLG OffshrDri Grw",
							"RGUELOIG:RLG Oil Integ Grw",
							"RGUELORG:RLG Oil Ref&Mkt GR",
							"RGUELOWG:RLG OilWlEq&Sv Grw",
							"RGUFLAMG:RLG AstMgt&Cs Grw",
							"RGUFLBKG:RLG Bnks:Dvfd Grw",
							"RGUFLBSG:RLG BkSvThrfMt Grw",
							"RGUFLCLG:RLG Cons Lend Grw",
							"RGUFLDFG:RLG DvfFnlSvc Grw",
							"RGUFLDRG:RLG DvsfREstateActG",
							"RGUFLEDG:RLG EquityREIT DivG",
							"RGUFLEFG:RLG EquityREIT InfG",
							"RGUFLEHG:RLG EquityREIT HcaG",
							"RGUFLEIG:RLG EquityREIT IndG",
							"RGUFLELG:RLG EquityREIT L&RG",
							"RGUFLEOG:RLG EquityREIT OffG",
							"RGUFLERG:RLG EquityREIT ResG",
							"RGUFLESG:RLG EquityREIT StoG",
							"RGUFLFDG:RLG FinDat&Sy Grw",
							"RGUFLFMG:RLG ComFn&Mtg Grw",
							"RGUFLILG:RLG Ins Life Grw",
							"RGUFLIMG:RLG Ins MulLn Grw",
							"RGUFLIPG:RLG Ins ProCa Grw",
							"RGUFLMCG:RLG MortgREIT ComG",
							"RGUFLMDG:RLG MortgREIT DivG",
							"RGUFLMSG:RLG MortgREIT ResG",
							"RGUFLOSG:RLG EquityREIT OSpG",
							"RGUFLRHG:RLG RealEstate H&DG",
							"RGUFLRSG:RLG RealEstate SvcG",
							"RGUFLRTG:RLG EquityREIT RetG",
							"RGUFLSBG:RLG SecBrk&Svc Grw",
							"RGUFLTIG:RLG EquityREIT TimG",
							"RGUHLBTG:RLG Biotec Grw",
							"RGUHLHCG:RLG HlthC Misc Grw",
							"RGUHLHFG:RLG HC Fac Grw",
							"RGUHLHMG:RLG HC MgtSvc Grw",
							"RGUHLHSG:RLG HCare Svc Grw",
							"RGUHLMDG:RLG M&DIns&Sp Grw",
							"RGUHLMEG:RLG Med Eq Grw",
							"RGUHLMSG:RLG Med Svc Grw",
							"RGUHLPHG:RLG Phrm Grw",
							"RGUMLALG:RLG Aluminum Grw",
							"RGUMLBMG:RLG BldgMatl Grw",
							"RGUMLCCG:RLG Bld ClCtr Grw",
							"RGUMLCDG:RLG Chem Dvfd Grw",
							"RGUMLCMG:RLG Cement Grw",
							"RGUMLCPG:RLG Cont&Pkg Grw",
							"RGUMLCRG:RLG Copper Grw",
							"RGUMLCSG:RLG Chem Spec Grw",
							"RGUMLDMG:RLG DvfMt&Prc Grw",
							"RGUMLFPG:RLG ForestPrd Grw",
							"RGUMLFTG:RLG Fertiliz Grw",
							"RGUMLGLG:RLG Glass Grw",
							"RGUMLGOG:RLG Gold Grw",
							"RGUMLMDG:RLG Met&MinDvf Grw",
							"RGUMLMFG:RLG MetFabric Grw",
							"RGUMLPAG:RLG Paper Grw",
							"RGUMLPCG:RLG Paint&Coat Grw",
							"RGUMLPLG:RLG Plastics Grw",
							"RGUMLPMG:RLG PrcMt&Min Grw",
							"RGUMLRWG:RLG RfWal&Plm Grw",
							"RGUMLSTG:RLG Steel Grw",
							"RGUMLSYG:RLG SynFib&Chm Grw",
							"RGUMLTPG:RLG Text Prod Grw",
							"RGUPLAIG:RLG Air Trans Grw",
							"RGUPLASG:RLG Aerospace Grw",
							"RGUPLBOG:RLG BOSupHR&Cons GR",
							"RGUPLCLG:RLG CommSvcR&L Grw",
							"RGUPLCNG:RLG Cons Grw",
							"RGUPLCVG:RLG CoVeh&Prt Grw",
							"RGUPLDOG:RLG DvfMfgOps Grw",
							"RGUPLECG:RLG Eng&CtrSv Grw",
							"RGUPLENG:RLG EnvMn&SecSvc GR",
							"RGUPLFBG:RLG FmBlkPrSv Grw",
							"RGUPLITG:RLG IntlTrd&DvLg GR",
							"RGUPLMAG:RLG Mach Ag Grw",
							"RGUPLMEG:RLG Mach Eng Grw",
							"RGUPLMHG:RLG Mch Cn&Hn Grw",
							"RGUPLMIG:RLG Mach  Indu Grw",
							"RGUPLMSG:RLG Mach Spec Grw",
							"RGUPLMTG:RLG Mach Tool Grw",
							"RGUPLOEG:RLG OffSup&Eq Grw",
							"RGUPLPDG:RLG PrdDr Misc Grw",
							"RGUPLPTG:RLG PwrTranEq Grw",
							"RGUPLRAG:RLG Railroads Grw",
							"RGUPLRLG:RLG RailroadEq Grw",
							"RGUPLSEG:RLG SciIn Elc Grw",
							"RGUPLSGG:RLG ScInGg&Mt Grw",
							"RGUPLSHG:RLG Shipping Grw",
							"RGUPLSIG:RLG SciICt&Fl Grw",
							"RGUPLSPG:RLG SciInPCtrl Grw",
							"RGUPLTKG:RLG Truckers Grw",
							"RGUPLTMG:RLG Trans Misc Grw",
							"RGUSLAFG:RLG AgFsh&Ran Grw",
							"RGUSLBDG:RLG Bev Br&Ds Grw",
							"RGUSLDGG:RLG Drg&GrcCh Grw",
							"RGUSLFGG:RLG Fru&GrnPrc Grw",
							"RGUSLFOG:RLG Foods Grw",
							"RGUSLMCG:RLG MiscConsSt Grw",
							"RGUSLPCG:RLG Pers Care Grw",
							"RGUSLSDG:RLG Bev SftDr Grw",
							"RGUSLSUG:RLG Sugar Grw",
							"RGUSLTOG:RLG Tobacco Grw",
							"RGUTLCMG:RLG Comm Tech Grw",
							"RGUTLCSG:RLG CpSvSw&Sy Grw",
							"RGUTLCTG:RLG Comp Tech Grw",
							"RGUTLECG:RLG Elec Comp Grw",
							"RGUTLEEG:RLG ElecEnt Grw",
							"RGUTLELG:RLG Elec Grw",
							"RGUTLPRG:RLG ProdTechEq Grw",
							"RGUTLSCG:RLG SemiCn&Cp Grw",
							"RGUTLTEG:RLG Tele Eq Grw",
							"RGUTLTMG:RLG Tech Misc Grw",
							"RGUULUEG:RLG Util Elec Grw",
							"RGUULUGG:RLG Util GasDi Grw",
							"RGUULUMG:RLG Util Misc Grw",
							"RGUULUTG:RLG Util Tele Grw",
							"RGUULUWG:RLG Util Water Grw"
						});
						ok = true;
					}
				}


				if (groupSymbol == "RLV")   //Russell 1000 Value
				{
					if (subgroupSymbol == "RLVL1")
					{
						if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
							"RGUSDLV:RLV CON DISC VALUE >",
							"RGUSSLV:RLV CONS STAPLES VALUE >",
							"RGUSELV:RLV ENERGY VALUE >",
							"RGUSFLV:RLV FIN SERVICES VALUE >",
							"RGUSHLV:RLV HEALTH CARE VALUE >",
							"RGUSMLV:RLV MATERIALS VALUE >",
							"RGUSPLV:RLV PROD DURABLES VALUE >",
							"RGUSTLV:RLV TECHNOLOGY VALUE >",
							"RGUSULV:RLV UTILITIES VALUE >"
						 });

						else setNavigation(panel, mouseDownEvent, new string[] {
							"RGUSDLV:RLV CON DISC VALUE",
							"RGUSSLV:RLV CONS STAPLES VALUE",
							"RGUSELV:RLV ENERGY VALUE",
							"RGUSFLV:RLV FIN SERVICES VALUE",
							"RGUSHLV:RLV HEALTH CARE VALUE",
							"RGUSMLV:RLV MATERIALS VALUE",
							"RGUSPLV:RLV PROD DURABLES VALUE",
							"RGUSTLV:RLV TECHNOLOGY VALUE",
							"RGUSULV:RLV UTILITIES VALUE"
						});
						ok = true;
					}

					else if (subgroupSymbol == "RLVL2")
					{
						if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
							"RGUDLAAV:RLV Ad Age Val >",
							"RGUDLAPV:RLV AutoPrts Val >",
							"RGUDLASV:RLV Auto Svc Val >",
							"RGUDLAUV:RLV Automob Val >",
							"RGUDLCEV:RLV ConsElect Val >",
							"RGUDLCGV:RLV Cas&Gamb Val >",
							"RGUDLCMV:RLV ConSvcMisc Val >",
							"RGUDLCSV:RLV Cosmetics Val >",
							"RGUDLCTV:RLV CabTV Svc Val >",
							"RGUDLDMV:RLV Dvsfd Med Val >",
							"RGUDLDRV:RLV Dvsfd Ret Val >",
							"RGUDLENV:RLV Entertain Val >",
							"RGUDLESV:RLV Edu Svc Val >",
							"RGUDLFUV:RLV FuPar&Cem Val >",
							"RGUDLHAV:RLV HholdAppl Val >",
							"RGUDLHBV:RLV HomeBldg Val >",
							"RGUDLHEV:RLV HHEq&Pro Val >",
							"RGUDLHFV:RLV Hhld Furn Val >",
							"RGUDLHOV:RLV Htel/Mtel Val >",
							"RGUDLLTV:RLV Leis Time Val >",
							"RGUDLLXV:RLV Lux Items Val >",
							"RGUDLMHV:RLV Mfg Hous Val >",
							"RGUDLPCV:RLV Prnt&CpySv Val >",
							"RGUDLPHV:RLV Photograph Val >",
							"RGUDLPUV:RLV Publishing Val >",
							"RGUDLRBV:RLV Radio&TVBr Val >",
							"RGUDLRCV:RLV R&LSvCon Val >",
							"RGUDLRTV:RLV Restaurant Val >",
							"RGUDLRVV:RLV RV & Boats Val >",
							"RGUDLSFV:RLV StorageFac Val >",
							"RGUDLSRV:RLV Spec Ret  Val >",
							"RGUDLTXV:RLV TxtAp&Shoe Val >",
							"RGUDLTYV:RLV Toys Val >",
							"RGUDLVCV:RLV Vend&Cat Val >",
							"RGUELAEV:RLV AlterEner Val >",
							"RGUELCOV:RLV Coal Val >",
							"RGUELEQV:RLV Energy Eq Val >",
							"RGUELGPV:RLV Gas Pipe Val >",
							"RGUELOCV:RLV Oil CrudPr Val >",
							"RGUELOFV:RLV OffshrDri Val >",
							"RGUELOIV:RLV Oil Integ Val >",
							"RGUELORV:RLV Oil Ref&Mkt VA",
							"RGUELOWV:RLV OilWlEq&Sv Val >",
							"RGUFLAMV:RLV AstMgt&Cs Val >",
							"RGUFLBKV:RLV Bnks:Dvfd Val >",
							"RGUFLBSV:RLV BkSvThrfMt Val >",
							"RGUFLCLV:RLV Cons Lend Val >",
							"RGUFLDFV:RLV DvfFnlSvc Val >",
							"RGUFLDRV:RLV DvsfREstateActV",
							"RGUFLEDV:RLV EquityREIT DivV",
							"RGUFLEFV:RLV EquityREIT InfV",
							"RGUFLEHV:RLV EquityREIT HcaV",
							"RGUFLEIV:RLV EquityREIT IndV",
							"RGUFLELV:RLV EquityREIT L&RV",
							"RGUFLEOV:RLV EquityREIT OffV",
							"RGUFLERV:RLV EquityREIT ResV",
							"RGUFLESV:RLV EquityREIT StoV",
							"RGUFLFDV:RLV FinDat&Sy Val >",
							"RGUFLFMV:RLV ComFn&Mtg Val >",
							"RGUFLILV:RLV Ins Life Val >",
							"RGUFLIMV:RLV Ins MulLn Val >",
							"RGUFLIPV:RLV Ins ProCa Val >",
							"RGUFLMCV:RLV MortgREIT ComV",
							"RGUFLMDV:RLV MortgREIT DivV",
							"RGUFLMSV:RLV MortgREIT ResV",
							"RGUFLOSV:RLV EquityREIT OSpV",
							"RGUFLRHV:RLV RealEstate H&DV",
							"RGUFLRSV:RLV RealEstate SvcV",
							"RGUFLRTV:RLV EquityREIT RetV",
							"RGUFLSBV:RLV SecBrk&Svc Val >",
							"RGUFLTIV:RLV EquityREIT TimV",
							"RGUHLBTV:RLV Biotec Val >",
							"RGUHLHCV:RLV HlthC:Misc Val >",
							"RGUHLHFV:RLV HC Fac Val >",
							"RGUHLHMV:RLV HC MgtSvc Val >",
							"RGUHLHSV:RLV HCare Svc Val >",
							"RGUHLMDV:RLV M&DIns&Sp Val >",
							"RGUHLMEV:RLV Med Eq Val >",
							"RGUHLMSV:RLV Med Svc Val >",
							"RGUHLPHV:RLV Phrm Val >",
							"RGUMLALV:RLV Aluminum Val >",
							"RGUMLBMV:RLV BldgMatl Val >",
							"RGUMLCCV:RLV Bld ClCtr Val >",
							"RGUMLCDV:RLV Chem Dvfd Val >",
							"RGUMLCMV:RLV Cement Val >",
							"RGUMLCPV:RLV Cont&Pkg Val >",
							"RGUMLCRV:RLV Copper Val >",
							"RGUMLCSV:RLV Chem Spec Val >",
							"RGUMLDMV:RLV DvfMt&Prc Val >",
							"RGUMLFPV:RLV ForestPrd Val >",
							"RGUMLFTV:RLV Fertiliz Val >",
							"RGUMLGLV:RLV Glass Val >",
							"RGUMLGOV:RLV Gold Val >",
							"RGUMLMDV:RLV Met&MinDvf Val >",
							"RGUMLMFV:RLV MetFabric Val >",
							"RGUMLPAV:RLV Paper Val >",
							"RGUMLPCV:RLV Paint&Coat Val >",
							"RGUMLPLV:RLV Plastics Val >",
							"RGUMLPMV:RLV PrcMt&Min Val >",
							"RGUMLRWV:RLV RfWal&Plm Val >",
							"RGUMLSTV:RLV Steel Val >",
							"RGUMLSYV:RLV SynFib&Chm Val >",
							"RGUMLTPV:RLV Text Prod Val >",
							"RGUPLAIV:RLV Air Trans Val >",
							"RGUPLASV:RLV Aerospace Val >",
							"RGUPLBOV:RLV BOSupHR&Cons VA",
							"RGUPLCLV:RLV CommSvcR&L Val >",
							"RGUPLCNV:RLV Cons Val >",
							"RGUPLCVV:RLV CoVeh&Prt Val >",
							"RGUPLDOV:RLV DvfMfgOps Val >",
							"RGUPLECV:RLV Eng&CtrSv Val >",
							"RGUPLENV:RLV EnvMn&SecSvc VA",
							"RGUPLFBV:RLV FmBlkPrSv Val >",
							"RGUPLITV:RLV IntlTrd&DvLg VA",
							"RGUPLMAV:RLV Mach Ag Val >",
							"RGUPLMEV:RLV Mach Eng Val >",
							"RGUPLMHV:RLV Mch Cn&Hn Val >",
							"RGUPLMIV:RLV Mach Indu Val >",
							"RGUPLMSV:RLV Mach Spec Val >",
							"RGUPLMTV:RLV Mach Tool Val >",
							"RGUPLOEV:RLV OffSup&Eq Val >",
							"RGUPLPDV:RLV PrdDr Misc Val >",
							"RGUPLPTV:RLV PwrTranEq Val >",
							"RGUPLRAV:RLV Railroads Val >",
							"RGUPLRLV:RLV RailroadEq Val >",
							"RGUPLSEV:RLV SciIn Elc Val >",
							"RGUPLSGV:RLV ScInGg&Mt Val >",
							"RGUPLSHV:RLV Shipping Val >",
							"RGUPLSIV:RLV SciICt&Fl Val >",
							"RGUPLSPV:RLV SciInPCtrl Val >",
							"RGUPLTKV:RLV Truckers Val >",
							"RGUPLTMV:RLV Trans Misc Val >",
							"RGUSLAFV:RLV AgFsh&Ran Val >",
							"RGUSLBDV:RLV Bev Br&Ds Val >",
							"RGUSLDGV:RLV Drg&GrcCh Val >",
							"RGUSLFGV:RLV Fru&GrnPrc Val >",
							"RGUSLFOV:RLV Foods Val >",
							"RGUSLMCV:RLV MiscConsSt Val >",
							"RGUSLPCV:RLV Pers Care Val >",
							"RGUSLSDV:RLV Bev SftDr Val >",
							"RGUSLSUV:RLV Sugar Val >",
							"RGUSLTOV:RLV Tobacco Val >",
							"RGUTLCMV:RLV Comm Tech Val >",
							"RGUTLCSV:RLV CpSvSw&Sy Val >",
							"RGUTLCTV:RLV Comp Tech Val >",
							"RGUTLECV:RLV Elec Comp Val >",
							"RGUTLEEV:RLV ElecEnt Val >",
							"RGUTLELV:RLV Elec Val >",
							"RGUTLPRV:RLV ProdTechEq Val >",
							"RGUTLSCV:RLV SemiCn&Cp Val >",
							"RGUTLTEV:RLV Tele Eq Val >",
							"RGUTLTMV:RLV Tech Misc Val >",
							"RGUULUEV:RLV Util Elec Val >",
							"RGUULUGV:RLV Util GasDi Val >",
							"RGUULUMV:RLV Util Misc Val >",
							"RGUULUTV:RLV Util Tele Val >",
							"RGUULUWV:RLV Util Water Val >"
						 });

						else setNavigation(panel, mouseDownEvent, new string[] {
							"RGUDLAAV:RLV Ad Age Val",
							"RGUDLAPV:RLV AutoPrts Val",
							"RGUDLASV:RLV Auto Svc Val",
							"RGUDLAUV:RLV Automob Val",
							"RGUDLCEV:RLV ConsElect Val",
							"RGUDLCGV:RLV Cas&Gamb Val",
							"RGUDLCMV:RLV ConSvcMisc Val",
							"RGUDLCSV:RLV Cosmetics Val",
							"RGUDLCTV:RLV CabTV Svc Val",
							"RGUDLDMV:RLV Dvsfd Med Val",
							"RGUDLDRV:RLV Dvsfd Ret Val",
							"RGUDLENV:RLV Entertain Val",
							"RGUDLESV:RLV Edu Svc Val",
							"RGUDLFUV:RLV FuPar&Cem Val",
							"RGUDLHAV:RLV HholdAppl Val",
							"RGUDLHBV:RLV HomeBldg Val",
							"RGUDLHEV:RLV HHEq&Pro Val",
							"RGUDLHFV:RLV Hhld Furn Val",
							"RGUDLHOV:RLV Htel Mtel Val",
							"RGUDLLTV:RLV Leis Time Val",
							"RGUDLLXV:RLV Lux Items Val",
							"RGUDLMHV:RLV Mfg Hous Val",
							"RGUDLPCV:RLV Prnt Cpy Sv Val",
							"RGUDLPHV:RLV Photograph Val",
							"RGUDLPUV:RLV Publishing Val",
							"RGUDLRBV:RLV Radio&TVBr Val",
							"RGUDLRCV:RLV R L Sv Con Val",
							"RGUDLRTV:RLV Restaurant Val",
							"RGUDLRVV:RLV RV & Boats Val",
							"RGUDLSFV:RLV StorageFac Val",
							"RGUDLSRV:RLV Spec Ret  Val",
							"RGUDLTXV:RLV Txt Ap Shoe Val",
							"RGUDLTYV:RLV Toys Val",
							"RGUDLVCV:RLV Vend Cat Val",
							"RGUELAEV:RLV AlterEner Val",
							"RGUELCOV:RLV Coal Val",
							"RGUELEQV:RLV Energy Eq Val",
							"RGUELGPV:RLV Gas Pipe Val",
							"RGUELOCV:RLV Oil CrudPr Val",
							"RGUELOFV:RLV Offshr Dri Val",
							"RGUELOIV:RLV Oil Integ Val",
							"RGUELORV:RLV Oil Ref Mkt VA",
							"RGUELOWV:RLV OilWlEq Sv Val",
							"RGUFLAMV:RLV Ast Mgt Cs Val",
							"RGUFLBKV:RLV Bnks Dvfd Val",
							"RGUFLBSV:RLV BkSvThrfMt Val",
							"RGUFLCLV:RLV Cons Lend Val",
							"RGUFLDFV:RLV DvfFnlSvc Val",
							"RGUFLDRV:RLV DvsfREstateActV",
							"RGUFLEDV:RLV EquityREIT DivV",
							"RGUFLEFV:RLV EquityREIT InfV",
							"RGUFLEHV:RLV EquityREIT HcaV",
							"RGUFLEIV:RLV EquityREIT IndV",
							"RGUFLELV:RLV EquityREIT L&RV",
							"RGUFLEOV:RLV EquityREIT OffV",
							"RGUFLERV:RLV EquityREIT ResV",
							"RGUFLESV:RLV EquityREIT StoV",
							"RGUFLFDV:RLV FinDat&Sy Val",
							"RGUFLFMV:RLV ComFn&Mtg Val",
							"RGUFLILV:RLV Ins Life Val",
							"RGUFLIMV:RLV Ins MulLn Val",
							"RGUFLIPV:RLV Ins ProCa Val",
							"RGUFLMCV:RLV MortgREIT ComV",
							"RGUFLMDV:RLV MortgREIT DivV",
							"RGUFLMSV:RLV MortgREIT ResV",
							"RGUFLOSV:RLV EquityREIT OSpV",
							"RGUFLRHV:RLV RealEstate H&DV",
							"RGUFLRSV:RLV RealEstate SvcV",
							"RGUFLRTV:RLV EquityREIT RetV",
							"RGUFLSBV:RLV SecBrk&Svc Val",
							"RGUFLTIV:RLV EquityREIT TimV",
							"RGUHLBTV:RLV Biotec Val",
							"RGUHLHCV:RLV HlthC Misc Val",
							"RGUHLHFV:RLV HC Fac Val",
							"RGUHLHMV:RLV HC MgtSvc Val",
							"RGUHLHSV:RLV HCare Svc Val",
							"RGUHLMDV:RLV M&DIns&Sp Val",
							"RGUHLMEV:RLV Med Eq Val",
							"RGUHLMSV:RLV Med Svc Val",
							"RGUHLPHV:RLV Phrm Val",
							"RGUMLALV:RLV Aluminum Val",
							"RGUMLBMV:RLV BldgMatl Val",
							"RGUMLCCV:RLV Bld ClCtr Val",
							"RGUMLCDV:RLV Chem Dvfd Val",
							"RGUMLCMV:RLV Cement Val",
							"RGUMLCPV:RLV Cont&Pkg Val",
							"RGUMLCRV:RLV Copper Val",
							"RGUMLCSV:RLV Chem Spec Val",
							"RGUMLDMV:RLV DvfMt&Prc Val",
							"RGUMLFPV:RLV ForestPrd Val",
							"RGUMLFTV:RLV Fertiliz Val",
							"RGUMLGLV:RLV Glass Val",
							"RGUMLGOV:RLV Gold Val",
							"RGUMLMDV:RLV Met&MinDvf Val",
							"RGUMLMFV:RLV MetFabric Val",
							"RGUMLPAV:RLV Paper Val",
							"RGUMLPCV:RLV Paint&Coat Val",
							"RGUMLPLV:RLV Plastics Val",
							"RGUMLPMV:RLV PrcMt&Min Val",
							"RGUMLRWV:RLV RfWal&Plm Val",
							"RGUMLSTV:RLV Steel Val",
							"RGUMLSYV:RLV SynFib&Chm Val",
							"RGUMLTPV:RLV Text Prod Val",
							"RGUPLAIV:RLV Air Trans Val",
							"RGUPLASV:RLV Aerospace Val",
							"RGUPLBOV:RLV BOSupHR&Cons VA",
							"RGUPLCLV:RLV CommSvcR&L Val",
							"RGUPLCNV:RLV Cons Val",
							"RGUPLCVV:RLV CoVeh&Prt Val",
							"RGUPLDOV:RLV DvfMfgOps Val",
							"RGUPLECV:RLV Eng&CtrSv Val",
							"RGUPLENV:RLV EnvMn&SecSvc VA",
							"RGUPLFBV:RLV FmBlkPrSv Val",
							"RGUPLITV:RLV IntlTrd&DvLg VA",
							"RGUPLMAV:RLV Mach Ag Val",
							"RGUPLMEV:RLV Mach Eng Val",
							"RGUPLMHV:RLV Mch Cn&Hn Val",
							"RGUPLMIV:RLV Mach  Indu Val",
							"RGUPLMSV:RLV Mach Spec Val",
							"RGUPLMTV:RLV Mach Tool Val",
							"RGUPLOEV:RLV OffSup&Eq Val",
							"RGUPLPDV:RLV PrdDr Misc Val",
							"RGUPLPTV:RLV PwrTranEq Val",
							"RGUPLRAV:RLV Railroads Val",
							"RGUPLRLV:RLV RailroadEq Val",
							"RGUPLSEV:RLV SciIn Elc Val",
							"RGUPLSGV:RLV ScInGg&Mt Val",
							"RGUPLSHV:RLV Shipping Val",
							"RGUPLSIV:RLV SciICt&Fl Val",
							"RGUPLSPV:RLV SciInPCtrl Val",
							"RGUPLTKV:RLV Truckers Val",
							"RGUPLTMV:RLV Trans Misc Val",
							"RGUSLAFV:RLV AgFsh&Ran Val",
							"RGUSLBDV:RLV Bev Br&Ds Val",
							"RGUSLDGV:RLV Drg&GrcCh Val",
							"RGUSLFGV:RLV Fru&GrnPrc Val",
							"RGUSLFOV:RLV Foods Val",
							"RGUSLMCV:RLV MiscConsSt Val",
							"RGUSLPCV:RLV Pers Care Val",
							"RGUSLSDV:RLV Bev SftDr Val",
							"RGUSLSUV:RLV Sugar Val",
							"RGUSLTOV:RLV Tobacco Val",
							"RGUTLCMV:RLV Comm Tech Val",
							"RGUTLCSV:RLV CpSvSw&Sy Val",
							"RGUTLCTV:RLV Comp Tech Val",
							"RGUTLECV:RLV Elec Comp Val",
							"RGUTLEEV:RLV ElecEnt Val",
							"RGUTLELV:RLV Elec Val",
							"RGUTLPRV:RLV ProdTechEq Val",
							"RGUTLSCV:RLV SemiCn&Cp Val",
							"RGUTLTEV:RLV Tele Eq Val",
							"RGUTLTMV:RLV Tech Misc Val",
							"RGUULUEV:RLV Util Elec Val",
							"RGUULUGV:RLV Util GasDi Val",
							"RGUULUMV:RLV Util Misc Val",
							"RGUULUTV:RLV Util Tele Val",
							"RGUULUWV:RLV Util:Water Val"
						});

						ok = true;
					}
				}

				if (groupSymbol == "RTY")  //Russell 2000
				{
					if (subgroupSymbol == "RTYL1")
					{
						if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
							"RGUSDS:RTY CONS DISC >",
							"RGUSSS:RTY CONS STAPLES >",
							"RGUSES:RTY ENERGY >",
							"RGUSFS:RTY FIN SERVICES >",
							"RGUSHS:RTY HEALTH CARE >",
							"RGUSMS:RTY MATERIALS >",
							"RGUSPS:RTY PROD DURABLES >",
							"RGUSTS:RTY TECHNOLOGY >",
							"RGUSUS:RTY UTILITIES >"
						 });

						else setNavigation(panel, mouseDownEvent, new string[] {
							"RGUSDS:RTY CONS DISC",
							"RGUSSS:RTY CONS STAPLES",
							"RGUSES:RTY ENERGY",
							"RGUSFS:RTY FIN SERVICES",
							"RGUSHS:RTY HEALTH CARE",
							"RGUSMS:RTY MATERIALS",
							"RGUSPS:RTY PROD DURABLES",
							"RGUSTS:RTY TECHNOLOGY",
							"RGUSUS:RTY UTILITIES"
						});

						ok = true;
					}

					else if (subgroupSymbol == "RTYL2")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"RGUSDSAA:RTY AD AGENCIES",
							"RGUSPSAS:RTY AEROSPACE",
							"RGUSSSAF:RTY AG FISH & RNCH",
							"RGUSPSAI:RTY AIR TRANSPORT",
							"RGUSESAE:RTY ALTER ENERGY",
							"RGUSMSAL:RTY ALUMINUM",
							"RGUSFSAM:RTY ASSET MGT & CUST",
							"RGUSDSAP:RTY AUTO PARTS",
							"RGUSDSAS:RTY AUTO SVC",
							"RGUSFSBK:RTY BANKS DVSFD",
							"RGUSSSBD:RTY BEV BRW & DSTLR",
							"RGUSSSSD:RTY BEV SOFT DRNK",
							"RGUSHSBT:RTY BIOTEC",
							"RGUSMSCC:RTY BLDG CLIMATE CTRL",
							"RGUSFSBS:RTY BNK SVG THRF MRT",
							"RGUSPSBO:RTY BO SUP HR & CONS",
							"RGUSMSBM:RTY BUILDING MATL",
							"RGUSDSCT:RTY CABLE TV SVC",
							"RGUSDSCG:RTY CASINOS & GAMB",
							"RGUSMSCM:RTY CEMENT",
							"RGUSMSCS:RTY CHEM SPEC",
							"RGUSMSCD:RTY CHEM DVFSD",
							"RGUSTSCS:RTY CMP SVC SFW & SYS",
							"RGUSESCO:RTY COAL",
							"RGUSFSFM:RTY COMM SVC",
							"RGUSPSCS:RTY COMM FIN & MORT",
							"RGUSPSCL:RTY COMM SVC RN",
							"RGUSTSCM:RTY COMM TECH",
							"RGUSPSCV:RTY COMM VEH & PRTS",
							"RGUSTSCT:RTY COMPUTER TECH",
							"RGUSPSCN:RTY CONS",
							"RGUSDSCM:RTY CONS SVC  MISC",
							"RGUSFSCL:RTY CONSUMER LEND",
							"RGUSDSCE:RTY CONSUMER ELECT",
							"RGUSMSCP:RTY CONTAINER & PKG",
							"RGUSMSCR:RTY COPPER",
							"RGUSDSCS:RTY COSMETICS",
							"RGUSSSDG:RTY DRUG & GROC CHN",
							"RGUSFSDF:RTY DVSFD FNCL SVC",
							"RGUSDSDM:RTY DVSFD MEDIA",
							"RGUSDSDR:RTY DVSFD RETAIL",
							"RGUSMSDM:RTY DVSFD MAT & PROC",
							"RGUSPSDO:RTY DVSFD MFG OPS",
							"RGUSDSES:RTY EDUCATION SVC",
							"RGUSTSEC:RTY ELECT COMP",
							"RGUSTSEE:RTY ELECT ENT",
							"RGUSTSEL:RTY ELECTRONICS",
							"RGUSESEQ:RTY ENERGY EQ",
							"RGUSPSEC:RTY ENG & CONTR SVC",
							"RGUSDSEN:RTY ENTERTAINMENT",
							"RGUSPSEN:RTY ENV MN & SEC SVC",
							"RGUSMSFT:RTY FERTILIZERS",
							"RGUSFSFD:RTY FINCL DATA & SYS",
							"RGUSSSFO:RTY FOODS",
							"RGUSMSFP:RTY FOREST PROD",
							"RGUSPSFB:RTY FRM & BLK PRNT SVC",
							"RGUSDSFU:RTY FUN PARLOR & CEM",
							"RGUSESGP:RTY GAS PIPELINE",
							"RGUSMSGL:RTY GLASS",
							"RGUSMSGO:RTY GOLD",
							"RGUSDSHE:RTY HHLD EQP & PROD",
							"RGUSDSHF:RTY HHLD FURN",
							"RGUSHSHF:RTY HLTH CARE FAC",
							"RGUSHSHS:RTY HLTH CARE SVC",
							"RGUSHSHM:RTY HLTH C MGT SVC",
							"RGUSHSHC:RTY HLTH C MISC",
							"RGUSDSHB:RTY HOMEBUILDING",
							"RGUSDSHO:RTY HOTEL MOTEL",
							"RGUSDSHA:RTY HOUSEHOLD APPL",
							"RGUSFSIL:RTY INS LIFE",
							"RGUSFSIM:RTY INS MULTI-LN",
							"RGUSFSIP:RTY INS PROP-CAS",
							"RGUSPSIT:RTY INTL TRD & DV LG",
							"RGUSDSLT:RTY LEISURE TIME",
							"RGUSDSLX:RTY LUXURY ITEMS",
							"RGUSPSMI:RTY MACH INDU",
							"RGUSPSMA:RTY MACH AG",
							"RGUSPSME:RTY MACH ENGINES",
							"RGUSPSMS:RTY MACH SPECIAL",
							"RGUSPSMH:RTY MCH CONS & HNDL",
							"RGUSHSMD:RTY MD & DN INS & SUP",
							"RGUSHSME:RTY MED EQ",
							"RGUSHSMS:RTY MED SVC",
							"RGUSMSMD:RTY MET & MIN DVFSD",
							"RGUSMSMF:RTY METAL FABRIC",
							"RGUSDSMH:RTY MFG HOUSING",
							"RGUSSSMC:RTY MISC CONS STAPLE",
							"RGUSPSOE:RTY OFF SUP & EQ",
							"RGUSESOF:RTY OFFSHORE DRILL",
							"RGUSESOI:RTY OIL INTEGRATE",
							"RGUSESOC:RTY OIL CRUDE PROD",
							"RGUSESOR:RTY OIL REF & MKT",
							"RGUSESOW:RTY OIL WELL EQ & SVC",
							"RGUSMSPC:RTY PAINT & COATING",
							"RGUSMSPA:RTY PAPER",
							"RGUSSSPC:RTY PERSONAL CARE",
							"RGUSHSPH:RTY PHRM",
							"RGUSMSPL:RTY PLASTICS",
							"RGUSPSPT:RTY PREC MET & MINL",
							"RGUSMSPM:RTY PRINT & COPY SVC",
							"RGUSDSPC:RTY PROD DUR MISC",
							"RGUSPSPD:RTY PRODUCT TECH EQ",
							"RGUSTSPR:RTY PUBLISHING",
							"RGUSDSPU:RTY PWR TRANSM EQ",
							"RGUSDSRB:RTY RADIO & TV BROAD",
							"RGUSPSRL:RTY RAILROAD EQ",
							"RGUSPSRA:RTY RAILROADS",
							"RGUSFSRE:RTY REAL ESTATE",
							"RGUSFSRI:RTY REIT",
							"RGUSDSRT:RTY RESTAURANTS",
							"RGUSMSRW:RTY ROOF WALL & PLUM",
							"RGUSDSRC:RTY RT & LS SVC CONS",
							"RGUSDSRV:RTY RV & BOATS",
							"RGUSPSSI:RTY SCI INS CTL & FLT",
							"RGUSPSSP:RTY SCI INS POL CTRL",
							"RGUSPSSG:RTY SCI INST GG & MTR",
							"RGUSPSSE:RTY SCI INSTR ELEC",
							"RGUSFSSB:RTY SEC BRKG & SVC",
							"RGUSTSSC:RTY SEMI COND & COMP",
							"RGUSPSSH:RTY SHIPPING",
							"RGUSDSSR:RTY SPEC RET",
							"RGUSMSST:RTY STEEL",
							"RGUSMSSY:RTY SYN FIBR & CHEM",
							"RGUSTSTM:RTY TECHNO MISC",
							"RGUSTSTE:RTY TELE EQ",
							"RGUSDSTX:RTY TEXT APP & SHOES",
							"RGUSMSTP:RTY TEXTILE PROD",
							"RGUSSSTO:RTY TOBACCO",
							"RGUSDSTY:RTY TOYS",
							"RGUSPSTM:RTY TRANS MISC",
							"RGUSPSTK:RTY TRUCKERS",
							"RGUSUSUM:RTY UTIL  MISC",
							"RGUSUSUE:RTY UTIL ELEC",
							"RGUSUSUG:RTY UTIL GAS DIST",
							"RGUSUSUT:RTY UTIL TELE",
							"RGUSUSUW:RTY UTIL WATER"
						});

						ok = true;

						if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
							"RGUSDSAA:RTY AD AGENCIES >",
							"RGUSPSAS:RTY AEROSPACE >",
							"RGUSSSAF:RTY AG FISH & RNCH >",
							"RGUSPSAI:RTY AIR TRANSPORT >",
							"RGUSESAE:RTY ALTER ENERGY >",
							"RGUSMSAL:RTY ALUMINUM >",
							"RGUSFSAM:RTY ASSET MGT & CUST >",
							"RGUSDSAP:RTY AUTO PARTS >",
							"RGUSDSAS:RTY AUTO SVC >",
							"RGUSFSBK:RTY BANKS DVSFD >",
							"RGUSSSBD:RTY BEV BRW & DSTLR >",
							"RGUSSSSD:RTY BEV SOFT DRNK >",
							"RGUSHSBT:RTY BIOTEC >",
							"RGUSMSCC:RTY BLDG CLIMATE CTRL >",
							"RGUSFSBS:RTY BNK SVG THRF MRT >",
							"RGUSPSBO:RTY BO SUP HR & CONS >",
							"RGUSMSBM:RTY BUILDING MATL >",
							"RGUSDSCT:RTY CABLE TV SVC >",
							"RGUSDSCG:RTY CASINOS & GAMB >",
							"RGUSMSCM:RTY CEMENT >",
							"RGUSMSCS:RTY CHEM SPEC >",
							"RGUSMSCD:RTY CHEM DVFSD >",
							"RGUSTSCS:RTY CMP SVC SFW & SYS >",
							"RGUSESCO:RTY COAL >",
							"RGUSFSFM:RTY COMM SVC >",
							"RGUSPSCS:RTY COMM FIN & MORT >",
							"RGUSPSCL:RTY COMM SVC RN >",
							"RGUSTSCM:RTY COMM TECH >",
							"RGUSPSCV:RTY COMM VEH & PRTS >",
							"RGUSTSCT:RTY COMPUTER TECH >",
							"RGUSPSCN:RTY CONS >",
							"RGUSDSCM:RTY CONS SVC  MISC >",
							"RGUSFSCL:RTY CONSUMER LEND >",
							"RGUSDSCE:RTY CONSUMER ELECT >",
							"RGUSMSCP:RTY CONTAINER & PKG >",
							"RGUSMSCR:RTY COPPER >",
							"RGUSDSCS:RTY COSMETICS >",
							"RGUSSSDG:RTY DRUG & GROC CHN >",
							"RGUSFSDF:RTY DVSFD FNCL SVC >",
							"RGUSDSDM:RTY DVSFD MEDIA >",
							"RGUSDSDR:RTY DVSFD RETAIL >",
							"RGUSMSDM:RTY DVSFD MAT & PROC >",
							"RGUSPSDO:RTY DVSFD MFG OPS >",
							"RGUSDSES:RTY EDUCATION SVC >",
							"RGUSTSEC:RTY ELECT COMP >",
							"RGUSTSEE:RTY ELECT ENT >",
							"RGUSTSEL:RTY ELECTRONICS >",
							"RGUSESEQ:RTY ENERGY EQ >",
							"RGUSPSEC:RTY ENG & CONTR SVC >",
							"RGUSDSEN:RTY ENTERTAINMENT >",
							"RGUSPSEN:RTY ENV MN & SEC SVC >",
							"RGUSMSFT:RTY FERTILIZERS >",
							"RGUSFSFD:RTY FINCL DATA & SYS >",
							"RGUSSSFO:RTY FOODS >",
							"RGUSMSFP:RTY FOREST PROD >",
							"RGUSPSFB:RTY FRM & BLK PRNT SVC >",
							"RGUSDSFU:RTY FUN PARLOR & CEM >",
							"RGUSESGP:RTY GAS PIPELINE >",
							"RGUSMSGL:RTY GLASS >",
							"RGUSMSGO:RTY GOLD >",
							"RGUSDSHE:RTY HHLD EQP & PROD >",
							"RGUSDSHF:RTY HHLD FURN >",
							"RGUSHSHF:RTY HLTH CARE FAC >",
							"RGUSHSHS:RTY HLTH CARE SVC >",
							"RGUSHSHM:RTY HLTH C MGT SVC >",
							"RGUSHSHC:RTY HLTH C MISC >",
							"RGUSDSHB:RTY HOMEBUILDING >",
							"RGUSDSHO:RTY HOTEL/MOTEL >",
							"RGUSDSHA:RTY HOUSEHOLD APPL >",
							"RGUSFSIL:RTY INS LIFE >",
							"RGUSFSIM:RTY INS MULTI-LN >",
							"RGUSFSIP:RTY INS PROP-CAS >",
							"RGUSPSIT:RTY INTL TRD & DV LG >",
							"RGUSDSLT:RTY LEISURE TIME >",
							"RGUSDSLX:RTY LUXURY ITEMS >",
							"RGUSPSMI:RTY MACH INDU >",
							"RGUSPSMA:RTY MACH AG >",
							"RGUSPSME:RTY MACH ENGINES >",
							"RGUSPSMS:RTY MACH SPECIAL >",
							"RGUSPSMH:RTY MCH CONS & HNDL >",
							"RGUSHSMD:RTY MD & DN INS & SUP >",
							"RGUSHSME:RTY MED EQ >",
							"RGUSHSMS:RTY MED SVC >",
							"RGUSMSMD:RTY MET & MIN DVFSD >",
							"RGUSMSMF:RTY METAL FABRIC >",
							"RGUSDSMH:RTY MFG HOUSING >",
							"RGUSSSMC:RTY MISC CONS STAPLE >",
							"RGUSPSOE:RTY OFF SUP & EQ >",
							"RGUSESOF:RTY OFFSHORE DRILL >",
							"RGUSESOI:RTY OIL INTEGRATE >",
							"RGUSESOC:RTY OIL CRUDE PROD >",
							"RGUSESOR:RTY OIL REF & MKT >",
							"RGUSESOW:RTY OIL WELL EQ & SVC >",
							"RGUSMSPC:RTY PAINT & COATING >",
							"RGUSMSPA:RTY PAPER >",
							"RGUSSSPC:RTY PERSONAL CARE >",
							"RGUSHSPH:RTY PHRM >",
							"RGUSMSPL:RTY PLASTICS >",
							"RGUSPSPT:RTY PREC MET & MINL >",
							"RGUSMSPM:RTY PRINT & COPY SVC >",
							"RGUSDSPC:RTY PROD DUR MISC >",
							"RGUSPSPD:RTY PRODUCT TECH EQ >",
							"RGUSTSPR:RTY PUBLISHING >",
							"RGUSDSPU:RTY PWR TRANSM EQ >",
							"RGUSDSRB:RTY RADIO & TV BROAD >",
							"RGUSPSRL:RTY RAILROAD EQ >",
							"RGUSPSRA:RTY RAILROADS >",
							"RGUSFSRE:RTY REAL ESTATE >",
							"RGUSFSRI:RTY REIT >",
							"RGUSDSRT:RTY RESTAURANTS >",
							"RGUSMSRW:RTY ROOF WALL & PLUM >",
							"RGUSDSRC:RTY RT & LS SVC CONS >",
							"RGUSDSRV:RTY RV & BOATS >",
							"RGUSPSSI:RTY SCI INS CTL & FLT >",
							"RGUSPSSP:RTY SCI INS POL CTRL >",
							"RGUSPSSG:RTY SCI INST GG & MTR >",
							"RGUSPSSE:RTY SCI INSTR ELEC >",
							"RGUSFSSB:RTY SEC BRKG & SVC >",
							"RGUSTSSC:RTY SEMI COND & COMP >",
							"RGUSPSSH:RTY SHIPPING >",
							"RGUSDSSR:RTY SPEC RET >",
							"RGUSMSST:RTY STEEL >",
							"RGUSMSSY:RTY SYN FIBR & CHEM >",
							"RGUSTSTM:RTY TECHNO MISC >",
							"RGUSTSTE:RTY TELE EQ >",
							"RGUSDSTX:RTY TEXT APP & SHOES >",
							"RGUSMSTP:RTY TEXTILE PROD >",
							"RGUSSSTO:RTY TOBACCO >",
							"RGUSDSTY:RTY TOYS >",
							"RGUSPSTM:RTY TRANS MISC >",
							"RGUSPSTK:RTY TRUCKERS >",
							"RGUSUSUM:RTY UTIL  MISC >",
							"RGUSUSUE:RTY UTIL ELEC >",
							"RGUSUSUG:RTY UTIL GAS DIST >",
							"RGUSUSUT:RTY UTIL TELE >",
							"RGUSUSUW:RTY UTIL WATER"
						 });

						else setNavigation(panel, mouseDownEvent, new string[] {
							"RGUSDSAA:RTY AD AGENCIES",
							"RGUSPSAS:RTY AEROSPACE",
							"RGUSSSAF:RTY AG FISH & RNCH",
							"RGUSPSAI:RTY AIR TRANSPORT",
							"RGUSESAE:RTY ALTER ENERGY",
							"RGUSMSAL:RTY ALUMINUM",
							"RGUSFSAM:RTY ASSET MGT & CUST",
							"RGUSDSAP:RTY AUTO PARTS",
							"RGUSDSAS:RTY AUTO SVC",
							"RGUSFSBK:RTY BANKS DVSFD",
							"RGUSSSBD:RTY BEV BRW & DSTLR",
							"RGUSSSSD:RTY BEV SOFT DRNK",
							"RGUSHSBT:RTY BIOTEC",
							"RGUSMSCC:RTY BLDG CLIMATE CTRL",
							"RGUSFSBS:RTY BNK SVG THRF MRT",
							"RGUSPSBO:RTY BO SUP HR & CONS",
							"RGUSMSBM:RTY BUILDING MATL",
							"RGUSDSCT:RTY CABLE TV SVC",
							"RGUSDSCG:RTY CASINOS & GAMB",
							"RGUSMSCM:RTY CEMENT",
							"RGUSMSCS:RTY CHEM SPEC",
							"RGUSMSCD:RTY CHEM DVFSD",
							"RGUSTSCS:RTY CMP SVC SFW & SYS",
							"RGUSESCO:RTY COAL",
							"RGUSFSFM:RTY COMM SVC",
							"RGUSPSCS:RTY COMM FIN & MORT",
							"RGUSPSCL:RTY COMM SVC RN",
							"RGUSTSCM:RTY COMM TECH",
							"RGUSPSCV:RTY COMM VEH & PRTS",
							"RGUSTSCT:RTY COMPUTER TECH",
							"RGUSPSCN:RTY CONS",
							"RGUSDSCM:RTY CONS SVC  MISC",
							"RGUSFSCL:RTY CONSUMER LEND",
							"RGUSDSCE:RTY CONSUMER ELECT",
							"RGUSMSCP:RTY CONTAINER & PKG",
							"RGUSMSCR:RTY COPPER",
							"RGUSDSCS:RTY COSMETICS",
							"RGUSSSDG:RTY DRUG & GROC CHN",
							"RGUSFSDF:RTY DVSFD FNCL SVC",
							"RGUSDSDM:RTY DVSFD MEDIA",
							"RGUSDSDR:RTY DVSFD RETAIL",
							"RGUSMSDM:RTY DVSFD MAT & PROC",
							"RGUSPSDO:RTY DVSFD MFG OPS",
							"RGUSDSES:RTY EDUCATION SVC",
							"RGUSTSEC:RTY ELECT COMP",
							"RGUSTSEE:RTY ELECT ENT",
							"RGUSTSEL:RTY ELECTRONICS",
							"RGUSESEQ:RTY ENERGY EQ",
							"RGUSPSEC:RTY ENG & CONTR SVC",
							"RGUSDSEN:RTY ENTERTAINMENT",
							"RGUSPSEN:RTY ENV MN & SEC SVC",
							"RGUSMSFT:RTY FERTILIZERS",
							"RGUSFSFD:RTY FINCL DATA & SYS",
							"RGUSSSFO:RTY FOODS",
							"RGUSMSFP:RTY FOREST PROD",
							"RGUSPSFB:RTY FRM & BLK PRNT SVC",
							"RGUSDSFU:RTY FUN PARLOR & CEM",
							"RGUSESGP:RTY GAS PIPELINE",
							"RGUSMSGL:RTY GLASS",
							"RGUSMSGO:RTY GOLD",
							"RGUSDSHE:RTY HHLD EQP & PROD",
							"RGUSDSHF:RTY HHLD FURN",
							"RGUSHSHF:RTY HLTH CARE FAC",
							"RGUSHSHS:RTY HLTH CARE SVC",
							"RGUSHSHM:RTY HLTH C MGT SVC",
							"RGUSHSHC:RTY HLTH C MISC",
							"RGUSDSHB:RTY HOMEBUILDING",
							"RGUSDSHO:RTY HOTEL/MOTEL",
							"RGUSDSHA:RTY HOUSEHOLD APPL",
							"RGUSFSIL:RTY INS LIFE",
							"RGUSFSIM:RTY INS MULTI-LN",
							"RGUSFSIP:RTY INS PROP-CAS",
							"RGUSPSIT:RTY INTL TRD & DV LG",
							"RGUSDSLT:RTY LEISURE TIME",
							"RGUSDSLX:RTY LUXURY ITEMS",
							"RGUSPSMI:RTY MACH INDU",
							"RGUSPSMA:RTY MACH AG",
							"RGUSPSME:RTY MACH ENGINES",
							"RGUSPSMS:RTY MACH SPECIAL",
							"RGUSPSMH:RTY MCH CONS & HNDL",
							"RGUSHSMD:RTY MD & DN INS & SUP",
							"RGUSHSME:RTY MED EQ",
							"RGUSHSMS:RTY MED SVC",
							"RGUSMSMD:RTY MET & MIN DVFSD",
							"RGUSMSMF:RTY METAL FABRIC",
							"RGUSDSMH:RTY MFG HOUSING",
							"RGUSSSMC:RTY MISC CONS STAPLE",
							"RGUSPSOE:RTY OFF SUP & EQ",
							"RGUSESOF:RTY OFFSHORE DRILL",
							"RGUSESOI:RTY OIL INTEGRATE",
							"RGUSESOC:RTY OIL CRUDE PROD",
							"RGUSESOR:RTY OIL REF & MKT",
							"RGUSESOW:RTY OIL WELL EQ & SVC",
							"RGUSMSPC:RTY PAINT & COATING",
							"RGUSMSPA:RTY PAPER",
							"RGUSSSPC:RTY PERSONAL CARE",
							"RGUSHSPH:RTY PHRM",
							"RGUSMSPL:RTY PLASTICS",
							"RGUSPSPT:RTY PREC MET & MINL",
							"RGUSMSPM:RTY PRINT & COPY SVC",
							"RGUSDSPC:RTY PROD DUR MISC",
							"RGUSPSPD:RTY PRODUCT TECH EQ",
							"RGUSTSPR:RTY PUBLISHING",
							"RGUSDSPU:RTY PWR TRANSM EQ",
							"RGUSDSRB:RTY RADIO & TV BROAD",
							"RGUSPSRL:RTY RAILROAD EQ",
							"RGUSPSRA:RTY RAILROADS",
							"RGUSFSRE:RTY REAL ESTATE",
							"RGUSFSRI:RTY REIT",
							"RGUSDSRT:RTY RESTAURANTS",
							"RGUSMSRW:RTY ROOF WALL & PLUM",
							"RGUSDSRC:RTY RT & LS SVC CONS",
							"RGUSDSRV:RTY RV & BOATS",
							"RGUSPSSI:RTY SCI INS CTL & FLT",
							"RGUSPSSP:RTY SCI INS POL CTRL",
							"RGUSPSSG:RTY SCI INST GG & MTR",
							"RGUSPSSE:RTY SCI INSTR ELEC",
							"RGUSFSSB:RTY SEC BRKG & SVC",
							"RGUSTSSC:RTY SEMI COND & COMP",
							"RGUSPSSH:RTY SHIPPING",
							"RGUSDSSR:RTY SPEC RET",
							"RGUSMSST:RTY STEEL",
							"RGUSMSSY:RTY SYN FIBR & CHEM",
							"RGUSTSTM:RTY TECHNO MISC",
							"RGUSTSTE:RTY TELE EQ",
							"RGUSDSTX:RTY TEXT APP & SHOES",
							"RGUSMSTP:RTY TEXTILE PROD",
							"RGUSSSTO:RTY TOBACCO",
							"RGUSDSTY:RTY TOYS",
							"RGUSPSTM:RTY TRANS MISC",
							"RGUSPSTK:RTY TRUCKERS",
							"RGUSUSUM:RTY UTIL  MISC",
							"RGUSUSUE:RTY UTIL ELEC",
							"RGUSUSUG:RTY UTIL GAS DIST",
							"RGUSUSUT:RTY UTIL TELE",
							"RGUSUSUW:RTY UTIL WATER"
						});
						ok = true;
					}
				}

				if (groupSymbol == "RUJ")  //Russell 2000
				{
					if (subgroupSymbol == "RUJL1")
					{
						if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
							"RGUSDSV:RUJ CONS DISC >",
							"RGUSSSV:RUJ CONS STAPLES >",
							"RGUSESV:RUJ ENERGY >",
							"RGUSFSV:RUJ FIN SERVICES >",
							"RGUSHSV:RUJ HEALTH CARE >",
							"RGUSMSV:RUJ MATERIALS >",
							"RGUSPSV:RUJ PROD DURABLES >",
							"RGUSTSV:RUJ TECHNOLOGY >",
							"RGUSUSV:RUJ UTILITIES >"
						 });

						else setNavigation(panel, mouseDownEvent, new string[] {
							"RGUSDSV:RUJ CONS DISC",
							"RGUSSSV:RUJ CONS STAPLES",
							"RGUSESV:RUJ ENERGY",
							"RGUSFSV:RUJ FIN SERVICES",
							"RGUSHSV:RUJ HEALTH CARE",
							"RGUSMSV:RUJ MATERIALS",
							"RGUSPSV:RUJ PROD DURABLES",
							"RGUSTSV:RUJ TECHNOLOGY",
							"RGUSUSV:RUJ UTILITIES"
						});

						ok = true;
					}

					else if (subgroupSymbol == "RUJL2")
					{
						if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
							"RGUDSAAV:RUJ Ad Age Val >",
							"RGUDSAPV:RUJ AutoPrts Val >",
							"RGUDSASV:RUJ Auto Svc Val >",
							"RGUDSAUV:RUJ Automob Val >",
							"RGUDSCEV:RUJ ConsElect Val >",
							"RGUDSCGV:RUJ Cas&Gamb Val >",
							"RGUDSCMV:RUJ ConSvcMisc Val >",
							"RGUDSCSV:RUJ Cosmetics Val >",
							"RGUDSCTV:RUJ CabTV Svc Val >",
							"RGUDSDMV:RUJ Dvsfd Med Val >",
							"RGUDSDRV:RUJ Dvsfd Ret Val >",
							"RGUDSENV:RUJ Entertain Val >",
							"RGUDSESV:RUJ Edu Svc Val >",
							"RGUDSFUV:RUJ FuPar&Cem Val >",
							"RGUDSHAV:RUJ HholdAppl Val >",
							"RGUDSHBV:RUJ HomeBldg Val >",
							"RGUDSHEV:RUJ HHEq&Pro Val >",
							"RGUDSHFV:RUJ Hhld Furn Val >",
							"RGUDSHOV:RUJ Htel/Mtel Val >",
							"RGUDSLTV:RUJ Leis Time Val >",
							"RGUDSLXV:RUJ Lux Items Val >",
							"RGUDSMHV:RUJ Mfg Hous Val >",
							"RGUDSPCV:RUJ Prnt&CpySv Val >",
							"RGUDSPHV:RUJ Photograph Val >",
							"RGUDSPUV:RUJ Publishing Val >",
							"RGUDSRBV:RUJ Radio&TVBr Val >",
							"RGUDSRCV:RUJ R&LSv Con Val >",
							"RGUDSRTV:RUJ Restaurant Val >",
							"RGUDSRVV:RUJ RV & Boats Val >",
							"RGUDSSFV:RUJ StorageFac Val >",
							"RGUDSSRV:RUJ Spec Ret  Val >",
							"RGUDSTXV:RUJ TxtAp&Shoe Val >",
							"RGUDSTYV:RUJ Toys Val >",
							"RGUDSVCV:RUJ Vend&Cat Val >",
							"RGUESAEV:RUJ AlterEner Val >",
							"RGUESCOV:RUJ Coal Val >",
							"RGUESEQV:RUJ Energy Eq Val >",
							"RGUESGPV:RUJ Gas Pipe Val >",
							"RGUESOCV:RUJ Oil CrudPr Val >",
							"RGUESOFV:RUJ OffshrDri Val >",
							"RGUESOIV:RUJ Oil Integ Val >",
							"RGUESORV:RUJ Oil Ref&Mkt VA >",
							"RGUESOWV:RUJ OilWlEq&Sv Val >",
							"RGUFSAMV:RUJ AstMgt&Cs Val >",
							"RGUFSBKV:RUJ Bnks Dvfd Val >",
							"RGUFSBSV:RUJ BkSvThrfMt Val >",
							"RGUFSCLV:RUJ Cons Lend Val >",
							"RGUFSDFV:RUJ DvfFnlSvc Val >",
							"RGUFSDRV:RUJ DvsfREstateActV >",
							"RGUFSEDV:RUJ EquityREIT DivV >",
							"RGUFSEFV:RUJ EquityREIT InfV >",
							"RGUFSEHV:RUJ EquityREIT HcaV >",
							"RGUFSEIV:RUJ EquityREIT IndV >",
							"RGUFSELV:RUJ EquityREIT L&RV >",
							"RGUFSEOV:RUJ EquityREIT OffV >",
							"RGUFSERV:RUJ EquityREIT ResV >",
							"RGUFSESV:RUJ EquityREIT StoV >",
							"RGUFSFDV:RUJ FinDat&Sy Val >",
							"RGUFSFMV:RUJ ComFn&Mtg Val >",
							"RGUFSILV:RUJ Ins Life Val >",
							"RGUFSIMV:RUJ Ins MulLn Val >",
							"RGUFSIPV:RUJ Ins ProCa Val >",
							"RGUFSMCV:RUJ MortgREIT ComV >",
							"RGUFSMDV:RUJ MortgREIT DivV >",
							"RGUFSMSV:RUJ MortgREIT ResV >",
							"RGUFSOSV:RUJ EquityREIT OSpV >",
							"RGUFSRHV:RUJ RealEstate H&DV >",
							"RGUFSRSV:RUJ RealEstate SvcV >",
							"RGUFSRTV:RUJ EquityREIT RetV >",
							"RGUFSSBV:RUJ SecBrk&Svc Val >",
							"RGUFSTIV:RUJ EquityREIT TimV >",
							"RGUHSBTV:RUJ Biotec Val >",
							"RGUHSHCV:RUJ HlthC Misc Val >",
							"RGUHSHFV:RUJ HC Fac Val >",
							"RGUHSHMV:RUJ HC MgtSvc Val >",
							"RGUHSHSV:RUJ HCare Svc Val >",
							"RGUHSMDV:RUJ M&DIns&Sp Val >",
							"RGUHSMEV:RUJ Med Eq Val >",
							"RGUHSMSV:RUJ Med Svc Val >",
							"RGUHSPHV:RUJ Phrm Val >",
							"RGUMSALV:RUJ Aluminum Val >",
							"RGUMSBMV:RUJ BldgMatl Val >",
							"RGUMSCCV:RUJ Bld ClCtr Val >",
							"RGUMSCDV:RUJ Chem Dvfd Val >",
							"RGUMSCMV:RUJ Cement Val >",
							"RGUMSCPV:RUJ Cont&Pkg Val >",
							"RGUMSCRV:RUJ Copper Val >",
							"RGUMSCSV:RUJ Chem Spec Val >",
							"RGUMSDMV:RUJ DvfMt&Prc Val >",
							"RGUMSFPV:RUJ ForestPrd Val >",
							"RGUMSFTV:RUJ Fertiliz Val >",
							"RGUMSGLV:RUJ Glass Val >",
							"RGUMSGOV:RUJ Gold Val >",
							"RGUMSMDV:RUJ Met&MinDvf Val >",
							"RGUMSMFV:RUJ MetFabric Val >",
							"RGUMSPAV:RUJ Paper Val >",
							"RGUMSPCV:RUJ Paint&Coat Val >",
							"RGUMSPLV:RUJ Plastics Val >",
							"RGUMSPMV:RUJ PrcMt&Min Val >",
							"RGUMSRWV:RUJ RfWal&Plm Val >",
							"RGUMSSTV:RUJ Steel Val >",
							"RGUMSSYV:RUJ SynFib&Chm Val >",
							"RGUMSTPV:RUJ Text Prod Val >",
							"RGUPSAIV:RUJ Air Trans Val >",
							"RGUPSASV:RUJ Aerospace Val >",
							"RGUPSBOV:RUJ BOSupHR&Cons VA >",
							"RGUPSCLV:RUJ CommSvcR&L Val >",
							"RGUPSCNV:RUJ Cons Val >",
							"RGUPSCVV:RUJ CoVeh&Prt Val >",
							"RGUPSDOV:RUJ DvfMfgOps Val >",
							"RGUPSECV:RUJ Eng&CtrSv Val >",
							"RGUPSENV:RUJ EnvMn&SecSvc VA >",
							"RGUPSFBV:RUJ FmBlkPrSv Val >",
							"RGUPSITV:RUJ IntlTrd&DvLg VA >",
							"RGUPSMAV:RUJ Mach Ag Val >",
							"RGUPSMEV:RUJ Mach Eng Val >",
							"RGUPSMHV:RUJ Mch Cn&Hn Val >",
							"RGUPSMIV:RUJ Mach Indu Val >",
							"RGUPSMSV:RUJ Mach Spec Val >",
							"RGUPSMTV:RUJ Mach Tool Val >",
							"RGUPSOEV:RUJ OffSup&Eq Val >",
							"RGUPSPDV:RUJ PrdDr Misc Val >",
							"RGUPSPTV:RUJ PwrTranEq Val >",
							"RGUPSRAV:RUJ Railroads Val >",
							"RGUPSRLV:RUJ RailroadEq Val >",
							"RGUPSSEV:RUJ SciIn Elc Val >",
							"RGUPSSGV:RUJ ScInGg&Mt Val >",
							"RGUPSSHV:RUJ Shipping Val >",
							"RGUPSSIV:RUJ SciICt&Fl Val >",
							"RGUPSSPV:RUJ SciInPCtrl Val >",
							"RGUPSTKV:RUJ Truckers Val >",
							"RGUPSTMV:RUJ Trans Misc Val >",
							"RGUSSAFV:RUJ AgFsh&Ran Val >",
							"RGUSSBDV:RUJ Bev Br&Ds Val >",
							"RGUSSDGV:RUJ Drg&GrcCh Val >",
							"RGUSSFGV:RUJ Fru&GrnPrc Val >",
							"RGUSSFOV:RUJ Foods Val >",
							"RGUSSMCV:RUJ MiscConsSt Val >",
							"RGUSSPCV:RUJ Pers Care Val >",
							"RGUSSSDV:RUJ Bev SftDr Val >",
							"RGUSSSUV:RUJ Sugar Val >",
							"RGUSSTOV:RUJ Tobacco Val >",
							"RGUTSCMV:RUJ Comm Tech Val >",
							"RGUTSCSV:RUJ CpSvSw&Sy Val >",
							"RGUTSCTV:RUJ Comp Tech Val >",
							"RGUTSECV:RUJ Elec Comp Val >",
							"RGUTSEEV:RUJ ElecEnt Val >",
							"RGUTSELV:RUJ Elec Val >",
							"RGUTSPRV:RUJ ProdTechEq Val >",
							"RGUTSSCV:RUJ SemiCn&Cp Val >",
							"RGUTSTEV:RUJ Tele Eq Val >",
							"RGUTSTMV:RUJ Tech Misc Val >",
							"RGUUSUEV:RUJ Util Elec Val >",
							"RGUUSUGV:RUJ Util GasDi Val >",
							"RGUUSUMV:RUJ Util Misc Val >",
							"RGUUSUTV:RUJ Util Tele Val >",
							"RGUUSUWV:RUJ Util Water Val"
						 });

						else setNavigation(panel, mouseDownEvent, new string[] {
							"RGUDSAAV:RUJ Ad Age Val",
							"RGUDSAPV:RUJ AutoPrts Val",
							"RGUDSASV:RUJ Auto Svc Val",
							"RGUDSAUV:RUJ Automob Val",
							"RGUDSCEV:RUJ ConsElect Val",
							"RGUDSCGV:RUJ Cas&Gamb Val",
							"RGUDSCMV:RUJ ConSvcMisc Val",
							"RGUDSCSV:RUJ Cosmetics Val",
							"RGUDSCTV:RUJ CabTV Svc Val",
							"RGUDSDMV:RUJ Dvsfd Med Val",
							"RGUDSDRV:RUJ Dvsfd Ret Val",
							"RGUDSENV:RUJ Entertain Val",
							"RGUDSESV:RUJ Edu Svc Val",
							"RGUDSFUV:RUJ FuPar&Cem Val",
							"RGUDSHAV:RUJ HholdAppl Val",
							"RGUDSHBV:RUJ HomeBldg Val",
							"RGUDSHEV:RUJ HHEq&Pro Val",
							"RGUDSHFV:RUJ Hhld Furn Val",
							"RGUDSHOV:RUJ Htel/Mtel Val",
							"RGUDSLTV:RUJ Leis Time Val",
							"RGUDSLXV:RUJ Lux Items Val",
							"RGUDSMHV:RUJ Mfg Hous Val",
							"RGUDSPCV:RUJ Prnt&CpySv Val",
							"RGUDSPHV:RUJ Photograph Val",
							"RGUDSPUV:RUJ Publishing Val",
							"RGUDSRBV:RUJ Radio&TVBr Val",
							"RGUDSRCV:RUJ R&LSv Con Val",
							"RGUDSRTV:RUJ Restaurant Val",
							"RGUDSRVV:RUJ RV & Boats Val",
							"RGUDSSFV:RUJ StorageFac Val",
							"RGUDSSRV:RUJ Spec Ret  Val",
							"RGUDSTXV:RUJ TxtAp&Shoe Val",
							"RGUDSTYV:RUJ Toys Val",
							"RGUDSVCV:RUJ Vend&Cat Val",
							"RGUESAEV:RUJ AlterEner Val",
							"RGUESCOV:RUJ Coal Val",
							"RGUESEQV:RUJ Energy Eq Val",
							"RGUESGPV:RUJ Gas Pipe Val",
							"RGUESOCV:RUJ Oil CrudPr Val",
							"RGUESOFV:RUJ OffshrDri Val",
							"RGUESOIV:RUJ Oil Integ Val",
							"RGUESORV:RUJ Oil Ref&Mkt VA",
							"RGUESOWV:RUJ OilWlEq&Sv Val",
							"RGUFSAMV:RUJ AstMgt&Cs Val",
							"RGUFSBKV:RUJ Bnks Dvfd Val",
							"RGUFSBSV:RUJ BkSvThrfMt Val",
							"RGUFSCLV:RUJ Cons Lend Val",
							"RGUFSDFV:RUJ DvfFnlSvc Val",
							"RGUFSDRV:RUJ DvsfREstateActV",
							"RGUFSEDV:RUJ EquityREIT DivV",
							"RGUFSEFV:RUJ EquityREIT InfV",
							"RGUFSEHV:RUJ EquityREIT HcaV",
							"RGUFSEIV:RUJ EquityREIT IndV",
							"RGUFSELV:RUJ EquityREIT L&RV",
							"RGUFSEOV:RUJ EquityREIT OffV",
							"RGUFSERV:RUJ EquityREIT ResV",
							"RGUFSESV:RUJ EquityREIT StoV",
							"RGUFSFDV:RUJ FinDat&Sy Val",
							"RGUFSFMV:RUJ ComFn&Mtg Val",
							"RGUFSILV:RUJ Ins Life Val",
							"RGUFSIMV:RUJ Ins MulLn Val",
							"RGUFSIPV:RUJ Ins ProCa Val",
							"RGUFSMCV:RUJ MortgREIT ComV",
							"RGUFSMDV:RUJ MortgREIT DivV",
							"RGUFSMSV:RUJ MortgREIT ResV",
							"RGUFSOSV:RUJ EquityREIT OSpV",
							"RGUFSRHV:RUJ RealEstate H&DV",
							"RGUFSRSV:RUJ RealEstate SvcV",
							"RGUFSRTV:RUJ EquityREIT:RetV",
							"RGUFSSBV:RUJ SecBrk&Svc Val",
							"RGUFSTIV:RUJ EquityREIT TimV",
							"RGUHSBTV:RUJ Biotec Val",
							"RGUHSHCV:RUJ HlthC Misc Val",
							"RGUHSHFV:RUJ HC Fac Val",
							"RGUHSHMV:RUJ HC MgtSvc Val",
							"RGUHSHSV:RUJ HCare Svc Val",
							"RGUHSMDV:RUJ M&DIns&Sp Val",
							"RGUHSMEV:RUJ Med Eq Val",
							"RGUHSMSV:RUJ Med Svc Val",
							"RGUHSPHV:RUJ Phrm Val",
							"RGUMSALV:RUJ Aluminum Val",
							"RGUMSBMV:RUJ BldgMatl Val",
							"RGUMSCCV:RUJ Bld ClCtr Val",
							"RGUMSCDV:RUJ Chem Dvfd Val",
							"RGUMSCMV:RUJ Cement Val",
							"RGUMSCPV:RUJ Cont&Pkg Val",
							"RGUMSCRV:RUJ Copper Val",
							"RGUMSCSV:RUJ Chem Spec Val",
							"RGUMSDMV:RUJ DvfMt&Prc Val",
							"RGUMSFPV:RUJ ForestPrd Val",
							"RGUMSFTV:RUJ Fertiliz Val",
							"RGUMSGLV:RUJ Glass Val",
							"RGUMSGOV:RUJ Gold Val",
							"RGUMSMDV:RUJ Met&MinDvf Val",
							"RGUMSMFV:RUJ MetFabric Val",
							"RGUMSPAV:RUJ Paper Val",
							"RGUMSPCV:RUJ Paint&Coat Val",
							"RGUMSPLV:RUJ Plastics Val",
							"RGUMSPMV:RUJ PrcMt&Min Val",
							"RGUMSRWV:RUJ RfWal&Plm Val",
							"RGUMSSTV:RUJ Steel Val",
							"RGUMSSYV:RUJ SynFib&Chm Val",
							"RGUMSTPV:RUJ Text Prod Val",
							"RGUPSAIV:RUJ Air Trans Val",
							"RGUPSASV:RUJ Aerospace Val",
							"RGUPSBOV:RUJ BOSupHR&Cons VA",
							"RGUPSCLV:RUJ CommSvcR&L Val",
							"RGUPSCNV:RUJ Cons Val",
							"RGUPSCVV:RUJ CoVeh&Prt Val",
							"RGUPSDOV:RUJ DvfMfgOps Val",
							"RGUPSECV:RUJ Eng&CtrSv Val",
							"RGUPSENV:RUJ EnvMn&SecSvc VA",
							"RGUPSFBV:RUJ FmBlkPrSv Val",
							"RGUPSITV:RUJ IntlTrd&DvLg VA",
							"RGUPSMAV:RUJ Mach Ag Val",
							"RGUPSMEV:RUJ Mach Eng Val",
							"RGUPSMHV:RUJ Mch Cn&Hn Val",
							"RGUPSMIV:RUJ Mach Indu Val",
							"RGUPSMSV:RUJ Mach Spec Val",
							"RGUPSMTV:RUJ Mach Tool Val",
							"RGUPSOEV:RUJ OffSup&Eq Val",
							"RGUPSPDV:RUJ PrdDr Misc Val",
							"RGUPSPTV:RUJ PwrTranEq Val",
							"RGUPSRAV:RUJ Railroads Val",
							"RGUPSRLV:RUJ RailroadEq Val",
							"RGUPSSEV:RUJ SciIn Elc Val",
							"RGUPSSGV:RUJ ScInGg&Mt Val",
							"RGUPSSHV:RUJ Shipping Val",
							"RGUPSSIV:RUJ SciICt&Fl Val",
							"RGUPSSPV:RUJ SciInPCtrl Val",
							"RGUPSTKV:RUJ Truckers Val",
							"RGUPSTMV:RUJ Trans Misc Val",
							"RGUSSAFV:RUJ AgFsh&Ran Val",
							"RGUSSBDV:RUJ Bev:Br&Ds Val",
							"RGUSSDGV:RUJ Drg&GrcCh Val",
							"RGUSSFGV:RUJ Fru&GrnPrc Val",
							"RGUSSFOV:RUJ Foods Val",
							"RGUSSMCV:RUJ MiscConsSt Val",
							"RGUSSPCV:RUJ Pers Care Val",
							"RGUSSSDV:RUJ Bev SftDr Val",
							"RGUSSSUV:RUJ Sugar Val",
							"RGUSSTOV:RUJ Tobacco Val",
							"RGUTSCMV:RUJ Comm Tech Val",
							"RGUTSCSV:RUJ CpSvSw&Sy Val",
							"RGUTSCTV:RUJ Comp Tech Val",
							"RGUTSECV:RUJ Elec Comp Val",
							"RGUTSEEV:RUJ ElecEnt Val",
							"RGUTSELV:RUJ Elec Val",
							"RGUTSPRV:RUJ ProdTechEq Val",
							"RGUTSSCV:RUJ SemiCn&Cp Val",
							"RGUTSTEV:RUJ Tele Eq Val",
							"RGUTSTMV:RUJ Tech Misc Val",
							"RGUUSUEV:RUJ Util Elec Val",
							"RGUUSUGV:RUJ Util GasDi Val",
							"RGUUSUMV:RUJ Util Misc Val",
							"RGUUSUTV:RUJ Util Tele Val",
							"RGUUSUWV:RUJ Util Water Val"
						});

						ok = true;
					}
				}

				if (groupSymbol == "RUO")  //Russell 2000
				{
					if (subgroupSymbol == "RUOL1")
					{
						if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
							"RGUSDSG:RUO CONS DISC >",
							"RGUSSSG:RUO CONS STAPLES >",
							"RGUSESG:RUO ENERGY >",
							"RGUSFSG:RUO FIN SERVICES >",
							"RGUSHSG:RUO HEALTH CARE >",
							"RGUSMSG:RUO MATERIALS >",
							"RGUSPSG:RUO PROD DURABLES >",
							"RGUSTSG:RUO TECHNOLOGY >",
							"RGUSUSG:RUO UTILITIES >"
						 });

						else setNavigation(panel, mouseDownEvent, new string[] {
							"RGUSDSG:RUO CONS DISC",
							"RGUSSSG:RUO CONS STAPLES",
							"RGUSESG:RUO ENERGY",
							"RGUSFSG:RUO FIN SERVICES",
							"RGUSHSG:RUO HEALTH CARE",
							"RGUSMSG:RUO MATERIALS",
							"RGUSPSG:RUO PROD DURABLES",
							"RGUSTSG:RUO TECHNOLOGY",
							"RGUSUSG:RUO UTILITIES"
						});

						ok = true;
					}

					else if (subgroupSymbol == "RUOL2")
					{
						if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
							"RGUDSAAG:RUO Ad Age Grw >",
							"RGUDSAPG:RUO AutoPrts Grw >",
							"RGUDSASG:RUO Auto Svc Grw >",
							"RGUDSAUG:RUO Automob Grw >",
							"RGUDSCEG:RUO ConsElect Grw >",
							"RGUDSCGG:RUO Cas&Gamb Grw >",
							"RGUDSCMG:RUO ConSvcMisc Grw >",
							"RGUDSCSG:RUO Cosmetics Grw >",
							"RGUDSCTG:RUO CabTV Svc Grw >",
							"RGUDSDMG:RUO Dvsfd Med Grw >",
							"RGUDSDRG:RUO Dvsfd Ret Grw >",
							"RGUDSENG:RUO Entertain Grw >",
							"RGUDSESG:RUO Edu Svc Grw >",
							"RGUDSFUG:RUO FuPar&Cem Grw >",
							"RGUDSHAG:RUO HholdAppl Grw >",
							"RGUDSHBG:RUO HomeBldg Grw >",
							"RGUDSHEG:RUO HHEq&Pro Grw >",
							"RGUDSHFG:RUO Hhld Furn Grw >",
							"RGUDSHOG:RUO Htel/Mtel Grw >",
							"RGUDSLTG:RUO Leis Time Grw >",
							"RGUDSLXG:RUO Lux Items Grw >",
							"RGUDSMHG:RUO Mfg Hous Grw >",
							"RGUDSPCG:RUO Prnt&CpySv Grw >",
							"RGUDSPHG:RUO Photograph Grw >",
							"RGUDSPUG:RUO Publishing Grw >",
							"RGUDSRBG:RUO Radio&TVBr Grw >",
							"RGUDSRCG:RUO R&LSv Con Grw >",
							"RGUDSRTG:RUO Restaurant Grw >",
							"RGUDSRVG:RUO RV & Boats Grw >",
							"RGUDSSFG:RUO StorageFac Grw >",
							"RGUDSSRG:RUO Spec Ret  Grw >",
							"RGUDSTXG:RUO TxtAp&Shoe Grw >",
							"RGUDSTYG:RUO Toys Grw >",
							"RGUDSVCG:RUO Vend&Cat Grw >",
							"RGUESAEG:RUO AlterEner Grw >",
							"RGUESCOG:RUO Coal Grw >",
							"RGUESEQG:RUO Energy Eq Grw >",
							"RGUESGPG:RUO Gas Pipe Grw >",
							"RGUESOCG:RUO Oil CrudPr Grw >",
							"RGUESOFG:RUO OffshrDri Grw >",
							"RGUESOIG:RUO Oil Integ Grw >",
							"RGUESORG:RUO Oil Ref&Mkt GR >",
							"RGUESOWG:RUO OilWlEq&Sv Grw >",
							"RGUFSAMG:RUO AstMgt&Cs Grw >",
							"RGUFSBKG:RUO Bnks Dvfd Grw >",
							"RGUFSBSG:RUO BkSvThrfMt Grw >",
							"RGUFSCLG:RUO Cons Lend Grw >",
							"RGUFSDFG:RUO DvfFnlSvc Grw >",
							"RGUFSDRG:RUO DvsfREstateActG >",
							"RGUFSEDG:RUO EquityREIT DivG >",
							"RGUFSEFG:RUO EquityREIT InfG >",
							"RGUFSEHG:RUO EquityREIT HcaG >",
							"RGUFSEIG:RUO EquityREIT IndG >",
							"RGUFSELG:RUO EquityREIT L&RG >",
							"RGUFSEOG:RUO EquityREIT OffG >",
							"RGUFSERG:RUO EquityREIT ResG >",
							"RGUFSESG:RUO EquityREIT StoG >",
							"RGUFSFDG:RUO FinDat&Sy Grw >",
							"RGUFSFMG:RUO ComFn&Mtg Grw >",
							"RGUFSILG:RUO Ins:Life Grw >",
							"RGUFSIMG:RUO Ins:MulLn Grw >",
							"RGUFSIPG:RUO Ins:ProCa Grw >",
							"RGUFSMCG:RUO MortgREIT ComG >",
							"RGUFSMDG:RUO MortgREIT DivG >",
							"RGUFSMSG:RUO MortgREIT ResG >",
							"RGUFSOSG:RUO EquityREIT OSpG >",
							"RGUFSRHG:RUO RealEstate H&DG >",
							"RGUFSRSG:RUO RealEstate SvcG >",
							"RGUFSRTG:RUO EquityREIT RetG >",
							"RGUFSSBG:RUO SecBrk&Svc Grw >",
							"RGUFSTIG:RUO EquityREIT TimG >",
							"RGUHSBTG:RUO Biotec Grw >",
							"RGUHSHCG:RUO HlthC Misc Grw >",
							"RGUHSHFG:RUO HC Fac Grw >",
							"RGUHSHMG:RUO HC MgtSvc Grw >",
							"RGUHSHSG:RUO HCare Svc Grw >",
							"RGUHSMDG:RUO M&DIns&Sp Grw >",
							"RGUHSMEG:RUO Med Eq Grw >",
							"RGUHSMSG:RUO Med Svc Grw >",
							"RGUHSPHG:RUO Phrm Grw >",
							"RGUMSALG:RUO Aluminum Grw >",
							"RGUMSBMG:RUO BldgMatl Grw >",
							"RGUMSCCG:RUO Bld ClCtr Grw >",
							"RGUMSCDG:RUO Chem Dvfd Grw >",
							"RGUMSCMG:RUO Cement Grw >",
							"RGUMSCPG:RUO Cont&Pkg Grw >",
							"RGUMSCRG:RUO Copper Grw >",
							"RGUMSCSG:RUO Chem Spec Grw >",
							"RGUMSDMG:RUO DvfMt&Prc Grw >",
							"RGUMSFPG:RUO ForestPrd Grw >",
							"RGUMSFTG:RUO Fertiliz Grw >",
							"RGUMSGLG:RUO Glass Grw >",
							"RGUMSGOG:RUO Gold Grw >",
							"RGUMSMDG:RUO Met&MinDvf Grw >",
							"RGUMSMFG:RUO MetFabric Grw >",
							"RGUMSPAG:RUO Paper Grw >",
							"RGUMSPCG:RUO Paint&Coat Grw >",
							"RGUMSPLG:RUO Plastics Grw >",
							"RGUMSPMG:RUO PrcMt&Min Grw >",
							"RGUMSRWG:RUO RfWal&Plm Grw >",
							"RGUMSSTG:RUO Steel Grw >",
							"RGUMSSYG:RUO SynFib&Chm Grw >",
							"RGUMSTPG:RUO Text Prod Grw >",
							"RGUPSAIG:RUO Air Trans Grw >",
							"RGUPSASG:RUO Aerospace Grw >",
							"RGUPSBOG:RUO BOSupHR&Cons GR >",
							"RGUPSCLG:RUO CommSvcR&L Grw >",
							"RGUPSCNG:RUO Cons Grw >",
							"RGUPSCVG:RUO CoVeh&Prt Grw >",
							"RGUPSDOG:RUO DvfMfgOps Grw >",
							"RGUPSECG:RUO Eng&CtrSv Grw >",
							"RGUPSENG:RUO EnvMn&SecSvc GR >",
							"RGUPSFBG:RUO FmBlkPrSv Grw >",
							"RGUPSITG:RUO IntlTrd&DvLg GR >",
							"RGUPSMAG:RUO Mach Ag Grw >",
							"RGUPSMEG:RUO Mach Eng Grw >",
							"RGUPSMHG:RUO Mch Cn&Hn Grw >",
							"RGUPSMIG:RUO Mach Indu Grw >",
							"RGUPSMSG:RUO Mach Spec Grw >",
							"RGUPSMTG:RUO Mach Tool Grw >",
							"RGUPSOEG:RUO OffSup&Eq Grw >",
							"RGUPSPDG:RUO PrdDr Misc Grw >",
							"RGUPSPTG:RUO PwrTranEq Grw >",
							"RGUPSRAG:RUO Railroads Grw >",
							"RGUPSRLG:RUO RailroadEq Grw >",
							"RGUPSSEG:RUO SciIn Elc Grw >",
							"RGUPSSGG:RUO ScInGg&Mt Grw >",
							"RGUPSSHG:RUO Shipping Grw >",
							"RGUPSSIG:RUO SciICt&Fl Grw >",
							"RGUPSSPG:RUO SciInPCtrl Grw >",
							"RGUPSTKG:RUO Truckers Grw >",
							"RGUPSTMG:RUO Trans Misc Grw >",
							"RGUSSAFG:RUO AgFsh&Ran Grw >",
							"RGUSSBDG:RUO Bev Br&Ds Grw >",
							"RGUSSDGG:RUO Drg&GrcCh Grw >",
							"RGUSSFGG:RUO Fru&GrnPrc Grw >",
							"RGUSSFOG:RUO Foods Grw >",
							"RGUSSGSD:RUO Bev SftDr Grw >",
							"RGUSSMCG:RUO MiscConsSt Grw >",
							"RGUSSPCG:RUO Pers Care Grw >",
							"RGUSSSUG:RUO Sugar Grw >",
							"RGUSSTOG:RUO Tobacco Grw >",
							"RGUTSCMG:RUO Comm Tech Grw >",
							"RGUTSCSG:RUO CpSvSw&Sy Grw >",
							"RGUTSCTG:RUO Comp Tech Grw >",
							"RGUTSECG:RUO Elec Comp Grw >",
							"RGUTSEEG:RUO ElecEnt Grw >",
							"RGUTSELG:RUO Elec Grw >",
							"RGUTSPRG:RUO ProdTechEq Grw >",
							"RGUTSSCG:RUO SemiCn&Cp Grw >",
							"RGUTSTEG:RUO Tele Eq Grw >",
							"RGUTSTMG:RUO Tech Misc Grw >",
							"RGUUSUEG:RUO Util Elec Grw >",
							"RGUUSUGG:RUO Util GasDi Grw >",
							"RGUUSUMG:RUO Util Misc Grw >",
							"RGUUSUTG:RUO Util Tele Grw >",
							"RGUUSUWG:RUO UtilWater Grw"

						 });

						else setNavigation(panel, mouseDownEvent, new string[] {
							"RGUDSAAG:RUO Ad Age Grw",
							"RGUDSAPG:RUO AutoPrts Grw",
							"RGUDSASG:RUO Auto Svc Grw",
							"RGUDSAUG:RUO Automob Grw",
							"RGUDSCEG:RUO ConsElect Grw",
							"RGUDSCGG:RUO Cas&Gamb Grw",
							"RGUDSCMG:RUO ConSvcMisc Grw",
							"RGUDSCSG:RUO Cosmetics Grw",
							"RGUDSCTG:RUO CabTV Svc Grw",
							"RGUDSDMG:RUO Dvsfd Med Grw",
							"RGUDSDRG:RUO Dvsfd Ret Grw",
							"RGUDSENG:RUO Entertain Grw",
							"RGUDSESG:RUO Edu Svc Grw",
							"RGUDSFUG:RUO FuPar&Cem Grw",
							"RGUDSHAG:RUO HholdAppl Grw",
							"RGUDSHBG:RUO HomeBldg Grw",
							"RGUDSHEG:RUO HHEq&Pro Grw",
							"RGUDSHFG:RUO Hhld Furn Grw",
							"RGUDSHOG:RUO Htel/Mtel Grw",
							"RGUDSLTG:RUO Leis Time Grw",
							"RGUDSLXG:RUO Lux Items Grw",
							"RGUDSMHG:RUO Mfg Hous Grw",
							"RGUDSPCG:RUO Prnt&CpySv Grw",
							"RGUDSPHG:RUO Photograph Grw",
							"RGUDSPUG:RUO Publishing Grw",
							"RGUDSRBG:RUO Radio&TVBr Grw",
							"RGUDSRCG:RUO R&LSv Con Grw",
							"RGUDSRTG:RUO Restaurant Grw",
							"RGUDSRVG:RUO RV & Boats Grw",
							"RGUDSSFG:RUO StorageFac Grw",
							"RGUDSSRG:RUO Spec Ret  Grw",
							"RGUDSTXG:RUO TxtAp&Shoe Grw",
							"RGUDSTYG:RUO Toys Grw",
							"RGUDSVCG:RUO Vend&Cat Grw",
							"RGUESAEG:RUO AlterEner Grw",
							"RGUESCOG:RUO Coal Grw",
							"RGUESEQG:RUO Energy Eq Grw",
							"RGUESGPG:RUO Gas Pipe Grw",
							"RGUESOCG:RUO Oil CrudPr Grw",
							"RGUESOFG:RUO OffshrDri Grw",
							"RGUESOIG:RUO Oil Integ Grw",
							"RGUESORG:RUO Oil Ref&Mkt GR",
							"RGUESOWG:RUO OilWlEq&Sv Grw",
							"RGUFSAMG:RUO AstMgt&Cs Grw",
							"RGUFSBKG:RUO Bnks Dvfd Grw",
							"RGUFSBSG:RUO BkSvThrfMt Grw",
							"RGUFSCLG:RUO Cons Lend Grw",
							"RGUFSDFG:RUO DvfFnlSvc Grw",
							"RGUFSDRG:RUO DvsfREstateActG",
							"RGUFSEDG:RUO EquityREIT DivG",
							"RGUFSEFG:RUO EquityREIT InfG",
							"RGUFSEHG:RUO EquityREIT HcaG",
							"RGUFSEIG:RUO EquityREIT IndG",
							"RGUFSELG:RUO EquityREIT L&RG",
							"RGUFSEOG:RUO EquityREIT OffG",
							"RGUFSERG:RUO EquityREIT ResG",
							"RGUFSESG:RUO EquityREIT StoG",
							"RGUFSFDG:RUO FinDat&Sy Grw",
							"RGUFSFMG:RUO ComFn&Mtg Grw",
							"RGUFSILG:RUO Ins Life Grw",
							"RGUFSIMG:RUO Ins MulLn Grw",
							"RGUFSIPG:RUO Ins ProCa Grw",
							"RGUFSMCG:RUO MortgREIT ComG",
							"RGUFSMDG:RUO MortgREIT DivG",
							"RGUFSMSG:RUO MortgREIT ResG",
							"RGUFSOSG:RUO EquityREIT OSpG",
							"RGUFSRHG:RUO RealEstate H&DG",
							"RGUFSRSG:RUO RealEstate SvcG",
							"RGUFSRTG:RUO EquityREIT RetG",
							"RGUFSSBG:RUO SecBrk&Svc Grw",
							"RGUFSTIG:RUO EquityREIT TimG",
							"RGUHSBTG:RUO Biotec Grw",
							"RGUHSHCG:RUO HlthC:Misc Grw",
							"RGUHSHFG:RUO HC Fac Grw",
							"RGUHSHMG:RUO HC MgtSvc Grw",
							"RGUHSHSG:RUO HCare Svc Grw",
							"RGUHSMDG:RUO M&DIns&Sp Grw",
							"RGUHSMEG:RUO Med Eq Grw",
							"RGUHSMSG:RUO Med Svc Grw",
							"RGUHSPHG:RUO Phrm Grw",
							"RGUMSALG:RUO Aluminum Grw",
							"RGUMSBMG:RUO BldgMatl Grw",
							"RGUMSCCG:RUO Bld ClCtr Grw",
							"RGUMSCDG:RUO Chem Dvfd Grw",
							"RGUMSCMG:RUO Cement Grw",
							"RGUMSCPG:RUO Cont&Pkg Grw",
							"RGUMSCRG:RUO Copper Grw",
							"RGUMSCSG:RUO Chem Spec Grw",
							"RGUMSDMG:RUO DvfMt&Prc Grw",
							"RGUMSFPG:RUO ForestPrd Grw",
							"RGUMSFTG:RUO Fertiliz Grw",
							"RGUMSGLG:RUO Glass Grw",
							"RGUMSGOG:RUO Gold Grw",
							"RGUMSMDG:RUO Met&MinDvf Grw",
							"RGUMSMFG:RUO MetFabric Grw",
							"RGUMSPAG:RUO Paper Grw",
							"RGUMSPCG:RUO Paint&Coat Grw",
							"RGUMSPLG:RUO Plastics Grw",
							"RGUMSPMG:RUO PrcMt&Min Grw",
							"RGUMSRWG:RUO RfWal&Plm Grw",
							"RGUMSSTG:RUO Steel Grw",
							"RGUMSSYG:RUO SynFib&Chm Grw",
							"RGUMSTPG:RUO Text Prod Grw",
							"RGUPSAIG:RUO Air Trans Grw",
							"RGUPSASG:RUO Aerospace Grw",
							"RGUPSBOG:RUO BOSupHR&Cons GR",
							"RGUPSCLG:RUO CommSvcR&L Grw",
							"RGUPSCNG:RUO Cons Grw",
							"RGUPSCVG:RUO CoVeh&Prt Grw",
							"RGUPSDOG:RUO DvfMfgOps Grw",
							"RGUPSECG:RUO Eng&CtrSv Grw",
							"RGUPSENG:RUO EnvMn&SecSvc GR",
							"RGUPSFBG:RUO FmBlkPrSv Grw",
							"RGUPSITG:RUO IntlTrd&DvLg GR",
							"RGUPSMAG:RUO Mach Ag Grw",
							"RGUPSMEG:RUO Mach Eng Grw",
							"RGUPSMHG:RUO Mch Cn&Hn Grw",
							"RGUPSMIG:RUO Mach Indu Grw",
							"RGUPSMSG:RUO Mach Spec Grw",
							"RGUPSMTG:RUO Mach Tool Grw",
							"RGUPSOEG:RUO OffSup&Eq Grw",
							"RGUPSPDG:RUO PrdDr Misc Grw",
							"RGUPSPTG:RUO PwrTranEq Grw",
							"RGUPSRAG:RUO Railroads Grw",
							"RGUPSRLG:RUO RailroadEq Grw",
							"RGUPSSEG:RUO SciIn Elc Grw",
							"RGUPSSGG:RUO ScInGg&Mt Grw",
							"RGUPSSHG:RUO Shipping Grw",
							"RGUPSSIG:RUO SciICt&Fl Grw",
							"RGUPSSPG:RUO SciInPCtrl Grw",
							"RGUPSTKG:RUO Truckers Grw",
							"RGUPSTMG:RUO Trans Misc Grw",
							"RGUSSAFG:RUO AgFsh&Ran Grw",
							"RGUSSBDG:RUO Bev Br&Ds Grw",
							"RGUSSDGG:RUO Drg&GrcCh Grw",
							"RGUSSFGG:RUO Fru&GrnPrc Grw",
							"RGUSSFOG:RUO Foods Grw",
							"RGUSSGSD:RUO Bev SftDr Grw",
							"RGUSSMCG:RUO MiscConsSt Grw",
							"RGUSSPCG:RUO Pers Care Grw",
							"RGUSSSUG:RUO Sugar Grw",
							"RGUSSTOG:RUO Tobacco Grw",
							"RGUTSCMG:RUO Comm Tech Grw",
							"RGUTSCSG:RUO CpSvSw&Sy Grw",
							"RGUTSCTG:RUO Comp Tech Grw",
							"RGUTSECG:RUO Elec Comp Grw",
							"RGUTSEEG:RUO ElecEnt Grw",
							"RGUTSELG:RUO Elec Grw",
							"RGUTSPRG:RUO ProdTechEq Grw",
							"RGUTSSCG:RUO SemiCn&Cp Grw",
							"RGUTSTEG:RUO Tele Eq Grw",
							"RGUTSTMG:RUO Tech Misc Grw",
							"RGUUSUEG:RUO Util Elec Grw",
							"RGUUSUGG:RUO Util GasDi Grw",
							"RGUUSUMG:RUO Util Misc Grw",
							"RGUUSUTG:RUO Util Tele Grw",
							"RGUUSUWG:RUO Util Water Grw"
						});

						ok = true;
					}
				}

				if (groupSymbol == "RAG")  //Russell 2000 Growth
				{
					if (subgroupSymbol == "RAGL1")
					{
						if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
							"RGUSDG:RAG CONS DISC >",
							"RGUSSG:RAG CONS STAPLES >",
							"RGUSEG:RAG ENERGY >",
							"RGUSFG:RAG FIN SERVICES >",
							"RGUSHG:RAG HEALTH CARE >",
							"RGUSMG:RAG MATERIALS >",
							"RGUSPG:RAG PROD DURABLES >",
							"RGUSTG:RAG TECHNOLOGY >",
							"RGUSUG:RAG UTILITIES >"
						 });

						else setNavigation(panel, mouseDownEvent, new string[] {
							"RGUSDG:RAG CONS DISC",
							"RGUSSG:RAG CONS STAPLES",
							"RGUSEG:RAG ENERGY",
							"RGUSFG:RAG FIN SERVICES",
							"RGUSHG:RAG HEALTH CARE",
							"RGUSMG:RAG MATERIALS",
							"RGUSPG:RAG PROD DURABLES",
							"RGUSTG:RAG TECHNOLOGY",
							"RGUSUG:RAG UTILITIES"
						});

						ok = true;
					}
				}
				if (groupSymbol == "RAV")  //Russell 2000 Value
				{
					if (subgroupSymbol == "RAVL1")
					{
						if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
							"RGUSDV:RAV CONS DISC >",
							"RGUSSV:RAV CONS STAPLES >",
							"RGUSEV:RAV ENERGY >",
							"RGUSFV:RAV FIN SERVICES >",
							"RGUSHV:RAV HEALTH CARE >",
							"RGUSMV:RAV MATERIALS >",
							"RGUSPV:RAV PROD DURABLES >",
							"RGUSTV:RAV TECHNOLOGY >",
							"RGUSUV:RAV UTILITIES >"
						 });

						else setNavigation(panel, mouseDownEvent, new string[] {
							"RGUSDV:RAV CONS DISC",
							"RGUSSV:RAV CONS STAPLES",
							"RGUSEV:RAV ENERGY",
							"RGUSFV:RAV FIN SERVICES",
							"RGUSHV:RAV HEALTH CARE",
							"RGUSMV:RAV MATERIALS",
							"RGUSPV:RAV PROD DURABLES",
							"RGUSTV:RAV TECHNOLOGY",
							"RGUSUV:RAV UTILITIES"
						});

						ok = true;
					}
				}
				if (groupSymbol == "RMC")  //Russell Mid Cap Growth
				{
					if (subgroupSymbol == "RMCL1")
					{
						if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
							"RGUMCD:RMC CONS DISC >",
							"RGUMCS:RMC CONS STAPLES >",
							"RGUMCE:RMC ENERGY >",
							"RGUMCF:RMC FIN SERVICES >",
							"RGUMCH:RMC HEALTH CARE >",
							"RGUMCM:RMC MATERIALS >",
							"RGUMCP:RMC PROD DURABLES >",
							"RGUMCT:RMC TECHNOLOGY >",
							"RGUMCU:RMC UTILITIES >"
						 });

						else setNavigation(panel, mouseDownEvent, new string[] {
							"RGUMCD:RMC CONS DISC",
							"RGUMCS:RMC CONS STAPLES",
							"RGUMCE:RMC ENERGY",
							"RGUMCF:RMC FIN SERVICES",
							"RGUMCH:RMC HEALTH CARE",
							"RGUMCM:RMC MATERIALS",
							"RGUMCP:RMC PROD DURABLES",
							"RGUMCT:RMC TECHNOLOGY",
							"RGUMCU:RMC UTILITIES"
						});

						ok = true;
					}
				}

				if (groupSymbol == "RDG")  //Russell Mid Cap Growth
				{
					if (subgroupSymbol == "RDGL1")
					{
						if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
							"RGUMCDG:RDG CONS DISC >",
							"RGUMCSG:RDG CONS STAPLES >",
							"RGUMCEG:RDG ENERGY >",
							"RGUMCFG:RDG FIN SERVICES >",
							"RGUMCHG:RDG HEALTH CARE >",
							"RGUMCMG:RDG MATERIALS >",
							"RGUMCPG:RDG PROD DURABLES >",
							"RGUMCTG:RDG TECHNOLOGY >",
							"RGUMCUG:RDG UTILITIES >"
						 });

						else setNavigation(panel, mouseDownEvent, new string[] {
							"RGUMCDG:RDG CONS DISC",
							"RGUMCSG:RDG CONS STAPLES",
							"RGUMCEG:RDG ENERGY",
							"RGUMCFG:RDG FIN SERVICES",
							"RGUMCHG:RDG HEALTH CARE",
							"RGUMCMG:RDG MATERIALS",
							"RGUMCPG:RDG PROD DURABLES",
							"RGUMCTG:RDG TECHNOLOGY",
							"RGUMCUG:RDG UTILITIES"
						});

						ok = true;
					}
				}
				if (groupSymbol == "RMV")  //Russell Mid Cap Value
				{
					if (subgroupSymbol == "RMVL1")
					{
						if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
							"RGUMCDV:RMV CONS DISC >",
							"RGUMCSV:RMV CONS STAPLES >",
							"RGUMCEV:RMV ENERGY >",
							"RGUMCFV:RMV FIN SERVICES >",
							"RGUMCHV:RMV HEALTH CARE >",
							"RGUMCMV:RMV MATERIALS >",
							"RGUMCPV:RMV PROD DURABLES >",
							"RGUMCTV:RMV TECHNOLOGY >",
							"RGUMCUV:RMV UTILITIES >"
						 });

						else setNavigation(panel, mouseDownEvent, new string[] {
							"RGUMCDV:RMV CONS DISC",
							"RGUMCSV:RMV CONS STAPLES",
							"RGUMCEV:RMV ENERGY",
							"RGUMCFV:RMV FIN SERVICES",
							"RGUMCHV:RMV HEALTH CARE",
							"RGUMCMV:RMV MATERIALS",
							"RGUMCPV:RMV PROD DURABLES",
							"RGUMCTV:RMV TECHNOLOGY",
							"RGUMCUV:RMV UTILITIES"
						});

						ok = true;
					}
				}
				if (groupSymbol == "RMICRO")  //Russell MicroCap 
				{
					if (subgroupSymbol == "RMICROL1")
					{
						if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
							"RGMICD:RMICRO CONS DISC >",
							"RGMICS:RMICRO CONS STAPLES >",
							"RGMICE:RMICRO ENERGY >",
							"RGMICF:RMICRO FIN SERVICES >",
							"RGMICH:RMICRO HEALTH CARE >",
							"RGMICM:RMICRO MATERIALS >",
							"RGMICP:RMICRO PROD DURABLES >",
							"RGMICT:RMICRO TECHNOLOGY >",
							"RGMICU:RMICRO UTILITIES >"
						 });

						else setNavigation(panel, mouseDownEvent, new string[] {
							"RGMICD:RMICRO CONS DISC",
							"RGMICS:RMICRO CONS STAPLES",
							"RGMICE:RMICRO ENERGY",
							"RGMICF:RMICRO FIN SERVICES",
							"RGMICH:RMICRO HEALTH CARE",
							"RGMICM:RMICRO MATERIALS",
							"RGMICP:RMICRO PROD DURABLES",
							"RGMICT:RMICRO TECHNOLOGY",
							"RGMICU:RMICRO UTILITIES"
						});

						ok = true;
					}
				}

				if (groupSymbol == "RMICROG")  //Russell MicroCap Growth
				{
					if (subgroupSymbol == "RMICROGL1")
					{
						if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
							"RGMICDG:RMICROG CONS DISC >",
							"RGMICSG:RMICROG CONS STAPLES >",
							"RGMICEG:RMICROG ENERGY >",
							"RGMICFG:RMICROG FIN SERVICES >",
							"RGMICHG:RMICROG HEALTH CARE >",
							"RGMICMG:RMICROG MATERIALS >",
							"RGMICPG:RMICROG PROD DURABLES >",
							"RGMICTG:RMICROG TECHNOLOGY >",
							"RGMICUG:RMICROG UTILITIES >"
						 });

						else setNavigation(panel, mouseDownEvent, new string[] {
							"RGMICDG:RMICROG CONS DISC",
							"RGMICSG:RMICROG CONS STAPLES",
							"RGMICEG:RMICROG ENERGY",
							"RGMICFG:RMICROG FIN SERVICES",
							"RGMICHG:RMICROG HEALTH CARE",
							"RGMICMG:RMICROG MATERIALS",
							"RGMICPG:RMICROG PROD DURABLES",
							"RGMICTG:RMICROG TECHNOLOGY",
							"RGMICUG:RMICROG UTILITIES"
						});

						ok = true;
					}
				}
				if (groupSymbol == "RMICROV")  //Russell MicroCap Value
				{
					if (subgroupSymbol == "RMICROVL1")
					{
						if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
							"RGMICDV:RMICROV CONS DISC >",
							"RGMICSV:RMICROV CONS STAPLES >",
							"RGMICEV:RMICROV ENERGY >",
							"RGMICFV:RMICROV FIN SERVICES >",
							"RGMICHV:RMICROV HEALTH CARE >",
							"RGMICMV:RMICROV MATERIALS >",
							"RGMICPV:RMICROV PROD DURABLES >",
							"RGMICTV:RMICROV TECHNOLOGY >",
							"RGMICUV:RMICROV UTILITIES >"
						 });

						else setNavigation(panel, mouseDownEvent, new string[] {
							"RGMICDV:RMICROV CONS DISC",
							"RGMICSV:RMICROV CONS STAPLES",
							"RGMICEV:RMICROV ENERGY",
							"RGMICFV:RMICROV FIN SERVICES",
							"RGMICHV:RMICROV HEALTH CARE",
							"RGMICMV:RMICROV MATERIALS",
							"RGMICPV:RMICROV PROD DURABLES",
							"RGMICTV:RMICROV TECHNOLOGY",
							"RGMICUV:RMICROV UTILITIES"
						});

						ok = true;
					}
				}
				if (groupSymbol == "R2500")  //Russell 2500 
				{
					if (subgroupSymbol == "R2500L1")
					{
						if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
							"RGU25D:R2500 CONS DISC >",
							"RGU25S:R2500 CONS STAPLES >",
							"RGU25E:R2500 ENERGY >",
							"RGU25F:R2500 FIN SERVICES >",
							"RGU25H:R2500 HEALTH CARE >",
							"RGU25M:R2500 MATERIALS >",
							"RGU25P:R2500 PROD DURABLES >",
							"RGU25T:R2500 TECHNOLOGY >",
							"RGU25U:R2500 UTILITIES >"
						 });

						else setNavigation(panel, mouseDownEvent, new string[] {
							"RGU25D:R2500 CONS DISC",
							"RGU25S:R2500 CONS STAPLES",
							"RGU25E:R2500 ENERGY",
							"RGU25F:R2500 FIN SERVICES",
							"RGU25H:R2500 HEALTH CARE",
							"RGU25M:R2500 MATERIALS",
							"RGU25P:R2500 PROD DURABLES",
							"RGU25T:R2500 TECHNOLOGY",
							"RGU25U:R2500 UTILITIES"
						});

						ok = true;
					}
				}

				if (groupSymbol == "R2500G")  //Russell 2500 Growth
				{
					if (subgroupSymbol == "R2500GL1")
					{
						if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
							"RGU25DG:R2500G CONS DISC >",
							"RGU25SG:R2500G CONS STAPLES >",
							"RGU25EG:R2500G ENERGY >",
							"RGU25FG:R2500G FIN SERVICES >",
							"RGU25HG:R2500G HEALTH CARE >",
							"RGU25MG:R2500G MATERIALS >",
							"RGU25PG:R2500G PROD DURABLES >",
							"RGU25TG:R2500G TECHNOLOGY >",
							"RGU25UG:R2500G UTILITIES >"
						 });

						else setNavigation(panel, mouseDownEvent, new string[] {
							"RGU25DG:R2500G CONS DISC",
							"RGU25SG:R2500G CONS STAPLES",
							"RGU25EG:R2500G ENERGY",
							"RGU25FG:R2500G FIN SERVICES",
							"RGU25HG:R2500G HEALTH CARE",
							"RGU25MG:R2500G MATERIALS",
							"RGU25PG:R2500G PROD DURABLES",
							"RGU25TG:R2500G TECHNOLOGY",
							"RGU25UG:R2500G UTILITIES"
						});

						ok = true;
					}
				}
				if (groupSymbol == "R2500V")  //Russell 2500 Value
				{
					if (subgroupSymbol == "R2500VL1")
					{
						if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
							"RGU25DV:R2500V CONS DISC >",
							"RGU25SV:R2500V CONS STAPLES >",
							"RGU25EV:R2500V ENERGY >",
							"RGU25FV:R2500V FIN SERVICES >",
							"RGU25HV:R2500V HEALTH CARE >",
							"RGU25MV:R2500V MATERIALS >",
							"RGU25PV:R2500V PROD DURABLES >",
							"RGU25TV:R2500V TECHNOLOGY >",
							"RGU25UV:R2500V UTILITIES >"
						 });

						else setNavigation(panel, mouseDownEvent, new string[] {
							"RGU25DV:R2500V CONS DISC",
							"RGU25SV:R2500V CONS STAPLES",
							"RGU25EV:R2500V ENERGY",
							"RGU25FV:R2500V FIN SERVICES",
							"RGU25HV:R2500V HEALTH CARE",
							"RGU25MV:R2500V MATERIALS",
							"RGU25PV:R2500V PROD DURABLES",
							"RGU25TV:R2500V TECHNOLOGY",
							"RGU25UV:R2500V UTILITIES"
						});

						ok = true;
					}
				}

				if (groupSymbol == "RAY") //Russell 3000
				{
					if (subgroupSymbol == "RAYL1")
					{
						if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
							"RGUSD:RAY CONS DISC >",
							"RGUSS:RAY CINS STAPLES >",
							"RGUSE:RAY ENERGY >",
							"RGUSF:RAY FIN SERVICES >",
							"RGUSH:RAY HEALTH CARE >",
							"RGUSM:RAY MATERIALS >",
							"RGUSP:RAY PROD DURABLES >",
							"RGUST:RAY TECHNOLOGY >",
							"RGUSU:RAY UTILITIES >"
						 });

						else setNavigation(panel, mouseDownEvent, new string[] {
							"RGUSD:RAY CONS DISC",
							"RGUSS:RAY CINS STAPLES",
							"RGUSE:RAY ENERGY",
							"RGUSF:RAY FIN SERVICES",
							"RGUSH:RAY HEALTH CARE",
							"RGUSM:RAY MATERIALS",
							"RGUSP:RAY PROD DURABLES",
							"RGUST:RAY TECHNOLOGY",
							"RGUSU:RAY UTILITIES"
						});

						ok = true;
					}

					else if (subgroupSymbol == "RAYL2")
					{
						if (UseCheckBoxes) setNavigation(panel, mouseDownEvent, new string[] {
							"RGUSDAA:RAY AD AGENCIES >",
							"RGUSPAS:RAY AEROSPACE >",
							"RGUSSAF:RAY AG FISH & RNCH >",
							"RGUSPAI:RAY AIR TRANSPORT >",
							"RGUSEAE:RAY ALTER ENERGY >",
							"RGUSMAL:RAY ALUMINUM >",
							"RGUSFAM:RAY ASSET MGT & CUST >",
							"RGUSDAP:RAY AUTO PARTS >",
							"RGUSDAS:RAY AUTO SVC >",
							"RGUSDAU:RAY AUTOMOBILES >",
							"RGUSFBK:RAY BANKS DVSFD >",
							"RGUSFBS:RAY BEV BRW & DSTLR >",
							"RGUSSBD:RAY BEV SOFT DRNK >",
							"RGUSSSD:RAY SOFT DRNK >",
							"RGUSHBT:RAY BIOTEC >",
							"RGUSMCC:RAY BLDG CLIMATE CTRL >",
							"RGUSPBO:RAY BO SUP HR & CONS >",
							"RGUSMBM:RAY BUILDING MATL >",
							"RGUSDCT:RAY CABLE TV SVC >",
							"RGUSDCG:RAY CASINOS & GAMB >",
							"RGUSMCM:RAY CEMENT >",
							"RGUSMCS:RAY CHEM SPEC >",
							"RGUSMCD:RAY CHEM DVFSD >",
							"RGUSECO:RAY CMP SVC SFW & SYS >",
							"RGUSFFM:RAY COAL >",
							"RGUSPCS:RAY COMM SVC >",
							"RGUSPCL:RAY COMM FIN & MORT >",
							"RGUSTCM:RAY COMM SVC RN >",
							"RGUSPCV:RAY COMM TECH >",
							"RGUSTCS:RAY COMM VEH & PRTS >",
							"RGUSTCT:RAY COMPUTER TECH >",
							"RGUSPCN:RAY CONS >",
							"RGUSDCM:RAY CONS SVC  MISC >",
							"RGUSDCE:RAY CONSUMER LEND >",
							"RGUSFCL:RAY CONSUMER ELECT >",
							"RGUSMCP:RAY CONTAINER & PKG >",
							"RGUSMCR:RAY COPPER >",
							"RGUSDCS:RAY COSMETICS >",
							"RGUSDDM:RAY DRUG & GROC CHN >",
							"RGUSSDG:RAY DVSFD FNCL SVC >",
							"RGUSFDF:RAY DVSFD MEDIA >",
							"RGUSDDR:RAY DVSFD RETAIL >",
							"RGUSMDM:RAY DVSFD MAT & PROC >",
							"RGUSPDO:RAY DVSFD MFG OPS >",
							"RGUSDES:RAY EDUCATION SVC >",
							"RGUSTEE:RAY ELECT COMP >",
							"RGUSTEC:RAY ELECT ENT >",
							"RGUSTEL:RAY ELECTRONICS >",
							"RGUSEEQ:RAY ENERGY EQ >",
							"RGUSPEC:RAY ENG & CONTR SVC >",
							"RGUSDEN:RAY ENTERTAINMENT >",
							"RGUSPEN:RAY ENV MN & SEC SVC >",
							"RGUSMFT:RAY FERTILIZERS >",
							"RGUSFFD:RAY FINCL DATA & SYS >",
							"RGUSSFO:RAY FOODS >",
							"RGUSMFP:RAY FOREST PROD >",
							"RGUSPFB:RAY FRM & BLK PRNT SVC >",
							"RGUSSFG:RAY FRUIT & GRN PROC >",
							"RGUSDFU:RAY FUN PARLOR & CEM >",
							"RGUSEGP:RAY GAS PIPELINE >",
							"RGUSMGL:RAY GLASS >",
							"RGUSMGO:RAY GOLD >",
							"RGUSDHE:RAY HHLD EQP & PROD >",
							"RGUSDHF:RAY HHLD FURN >",
							"RGUSHHF:RAY HLTH CARE FAC >",
							"RGUSHHM:RAY HLTH CARE SVC >",
							"RGUSHHS:RAY HLTH C MGT SVC >",
							"RGUSHHC:RAY HLTH C MISC >",
							"RGUSDHB:RAY HOME BUILDING >",
							"RGUSDHO:RAY HOTEL/MOTEL >",
							"RGUSDHA:RAY HOUSEHOLD APPL >",
							"RGUSFIL:RAY INS LIFE >",
							"RGUSFIM:RAY INS MULTI-LN >",
							"RGUSFIP:RAY INS PROP-CAS >",
							"RGUSPIT:RAY INTL TRD & DV LG >",
							"RGUSDLT:RAY LEISURE TIME >",
							"RGUSPMG:RAY MACH & ENG >",
							"RGUSPMI:RAY MACH IND >",
							"RGUSPMT:RAY MACH TOOLS >",
							"RGUSPMA:RAY MACH AG >",
							"RGUSPME:RAY MACH SPECIAL >",
							"RGUSPMS:RAY MCH CONS & HNDL >",
							"RGUSHME:RAY MD & DN INS & SUP >",
							"RGUSHMS:RAY MED EQ >",
							"RGUSHMD:RAY MED SVC >",
							"RGUSMMD:RAY MET & MIN DVFSD >",
							"RGUSMMF:RAY METAL FABRIC >",
							"RGUSDMH:RAY MFG HOUSING >",
							"RGUSSMC:RAY MISC CONS STAPL >",
							"RGUSPOE:RAY OFF SUP & EQ >",
							"RGUSEOF:RAY OFFSHORE DRILL >",
							"RGUSEOI:RAY OIL INTERGATE >",
							"RGUSEOW:RAY OIL CRUDE PROD >",
							"RGUSEOC:RAY OIL REF & MKT >",
							"RGUSEOR:RAY OIL WELL EQ & SVC >",
							"RGUSMPC:RAY PAINT & COATING >",
							"RGUSMPA:RAY PAPER >",
							"RGUSSPC:RAY PERSONAL CARE >",
							"RGUSDPH:RAY PHOTOGRAPHY >",
							"RGUSHPH:RAY PHRM >",
							"RGUSMPL:RAY PLASTICS >",
							"RGUSPOPT:RAY PREC MET & MINL >",
							"RGUSMPM:RAY PRINT & COPY SVC >",
							"RGUSDPC:RAY PROD DUR MISC >",
							"RGUSPPD:RAY PRODUCT TECH EQ >",
							"RGUSTPR:RAY PUBLISHING >",
							"RGUSDPU:RAY PWR TRANSM EQ >",
							"RGUSDRB:RAY RADIO & TV BROAD >",
							"RGUSPRL:RAY RAILROAD EQ >",
							"RGUSPRA:RAY RAILROADS >",
							"RGUSFRE:RAY REAL ESTATE >",
							"RGUSFRI:RAY REIT >",
							"RGUSDRC:RAY RESTAURANTS >",
							"RGUSDRT:RAY ROOF WALL & PLUM >",
							"RGUSMRW:RAY RT & LS SVC CONS >",
							"RGUSDRV:RAY RV & BOATS >",
							"RGUSPSE:RAY SCI INS CTL & FLT >",
							"RGUSPSP:RAY SCI INS POL CTRL >",
							"RGUSPSI:RAY Sci INSTR ELEC >",
							"RGUSFSB:RAY SEC BRKG & SVC >",
							"RGUSTSC:RAY SE COND & COMP >",
							"RGUSPSH:RAY SHIPPING >",
							"RGUSDSR:RAY SPEC RET >",
							"RGUSMST:RAY STEEL >",
							"RGUSMSY:RAY SYN FIBR & CHEM >",
							"RGUSTTM:RAY TECHNOLOGY MISC >",
							"RGUSTTE:RAY TELEG EQ >",
							"RGUSDTX:RAY TEXT APP & SHORES >",
							"RGUSMTP:RAY TEXTILE PROD >",
							"RGUSSTO:RAY TOBACC0 >",
							"RGUSDTY:RAY TOYS >",
							"RGUSPTM:RAY TRANS MISC >",
							"RGUSPTK:RAY TRUCK MISC >",
							"RGUSUUE:RAY UTIL ELEC >",
							"RGUSUUG:RAY UTIL GAS DIST >",
							"RGUSUUT:RAY UTIL TELE >",
							"RGUSUUW:RAY UTIL WATER"

						 });

						else setNavigation(panel, mouseDownEvent, new string[] {
							"RGUSDAA:RAY AD AGENCIES",
							"RGUSPAS:RAY AEROSPACE",
							"RGUSSAF:RAY AG FISH & RNCH",
							"RGUSPAI:RAY AIR TRANSPORT",
							"RGUSEAE:RAY ALTER ENERGY",
							"RGUSMAL:RAY ALUMINUM",
							"RGUSFAM:RAY ASSET MGT & CUST",
							"RGUSDAP:RAY AUTO PARTS",
							"RGUSDAS:RAY AUTO SVC",
							"RGUSDAU:RAY AUTOMOBILES",
							"RGUSFBK:RAY BANKS DVSFD",
							"RGUSFBS:RAY BEV BRW & DSTLR",
							"RGUSSBD:RAY BEV SOFT DRNK",
							"RGUSSSD:RAY SOFT DRNK",
							"RGUSHBT:RAY BIOTEC",
							"RGUSMCC:RAY BLDG CLIMATE CTRL",
							"RGUSPBO:RAY BO SUP HR & CONS",
							"RGUSMBM:RAY BUILDING MATL",
							"RGUSDCT:RAY CABLE TV SVC",
							"RGUSDCG:RAY CASINOS & GAMB",
							"RGUSMCM:RAY CEMENT",
							"RGUSMCS:RAY CHEM SPEC",
							"RGUSMCD:RAY CHEM DVFSD",
							"RGUSECO:RAY CMP SVC SFW & SYS",
							"RGUSFFM:RAY COAL",
							"RGUSPCS:RAY COMM SVC",
							"RGUSPCL:RAY COMM FIN & MORT",
							"RGUSTCM:RAY COMM SVC RN",
							"RGUSPCV:RAY COMM TECH",
							"RGUSTCS:RAY COMM VEH & PRTS",
							"RGUSTCT:RAY COMPUTER TECH",
							"RGUSPCN:RAY CONS",
							"RGUSDCM:RAY CONS SVC  MISC",
							"RGUSDCE:RAY CONSUMER LEND",
							"RGUSFCL:RAY CONSUMER ELECT",
							"RGUSMCP:RAY CONTAINER & PKG",
							"RGUSMCR:RAY COPPER",
							"RGUSDCS:RAY COSMETICS",
							"RGUSDDM:RAY DRUG & GROC CHN",
							"RGUSSDG:RAY DVSFD FNCL SVC",
							"RGUSFDF:RAY DVSFD MEDIA",
							"RGUSDDR:RAY DVSFD RETAIL",
							"RGUSMDM:RAY DVSFD MAT & PROC",
							"RGUSPDO:RAY DVSFD MFG OPS",
							"RGUSDES:RAY EDUCATION SVC",
							"RGUSTEE:RAY ELECT COMP",
							"RGUSTEC:RAY ELECT ENT",
							"RGUSTEL:RAY ELECTRONICS",
							"RGUSEEQ:RAY ENERGY EQ",
							"RGUSPEC:RAY ENG & CONTR SVC",
							"RGUSDEN:RAY ENTERTAINMENT",
							"RGUSPEN:RAY ENV MN & SEC SVC",
							"RGUSMFT:RAY FERTILIZERS",
							"RGUSFFD:RAY FINCL DATA & SYS",
							"RGUSSFO:RAY FOODS",
							"RGUSMFP:RAY FOREST PROD",
							"RGUSPFB:RAY FRM & BLK PRNT SVC",
							"RGUSSFG:RAY FRUIT & GRN PROC",
							"RGUSDFU:RAY FUN PARLOR & CEM",
							"RGUSEGP:RAY GAS PIPELINE",
							"RGUSMGL:RAY GLASS",
							"RGUSMGO:RAY GOLD",
							"RGUSDHE:RAY HHLD EQP & PROD",
							"RGUSDHF:RAY HHLD FURN",
							"RGUSHHF:RAY HLTH CARE FAC",
							"RGUSHHM:RAY HLTH CARE SVC",
							"RGUSHHS:RAY HLTH C MGT SVC",
							"RGUSHHC:RAY HLTH C MISC",
							"RGUSDHB:RAY HOME BUILDING",
							"RGUSDHO:RAY HOTEL/MOTEL",
							"RGUSDHA:RAY HOUSEHOLD APPL",
							"RGUSFIL:RAY INS LIFE",
							"RGUSFIM:RAY INS MULTI-LN",
							"RGUSFIP:RAY INS PROP-CAS",
							"RGUSPIT:RAY INTL TRD & DV LG",
							"RGUSDLT:RAY LEISURE TIME",
							"RGUSPMG:RAY MACH & ENG",
							"RGUSPMI:RAY MACH IND",
							"RGUSPMT:RAY MACH TOOLS",
							"RGUSPMA:RAY MACH AG",
							"RGUSPME:RAY MACH SPECIAL",
							"RGUSPMS:RAY MCH CONS & HNDL",
							"RGUSHME:RAY MD & DN INS & SUP",
							"RGUSHMS:RAY MED EQ",
							"RGUSHMD:RAY MED SVC",
							"RGUSMMD:RAY MET & MIN DVFSD",
							"RGUSMMF:RAY METAL FABRIC",
							"RGUSDMH:RAY MFG HOUSING",
							"RGUSSMC:RAY MISC CONS STAPL",
							"RGUSPOE:RAY OFF SUP & EQ",
							"RGUSEOF:RAY OFFSHORE DRILL",
							"RGUSEOI:RAY OIL INTERGATE",
							"RGUSEOW:RAY OIL CRUDE PROD",
							"RGUSEOC:RAY OIL REF & MKT",
							"RGUSEOR:RAY OIL WELL EQ & SVC",
							"RGUSMPC:RAY PAINT & COATING",
							"RGUSMPA:RAY PAPER",
							"RGUSSPC:RAY PERSONAL CARE",
							"RGUSDPH:RAY PHOTOGRAPHY",
							"RGUSHPH:RAY PHRM",
							"RGUSMPL:RAY PLASTICS",
							"RGUSPOPT:RAY PREC MET & MINL",
							"RGUSMPM:RAY PRINT & COPY SVC",
							"RGUSDPC:RAY PROD DUR MISC",
							"RGUSPPD:RAY PRODUCT TECH EQ",
							"RGUSTPR:RAY PUBLISHING",
							"RGUSDPU:RAY PWR TRANSM EQ",
							"RGUSDRB:RAY RADIO & TV BROAD",
							"RGUSPRL:RAY RAILROAD EQ",
							"RGUSPRA:RAY RAILROADS",
							"RGUSFRE:RAY REAL ESTATE",
							"RGUSFRI:RAY REIT",
							"RGUSDRC:RAY RESTAURANTS",
							"RGUSDRT:RAY ROOF WALL & PLUM",
							"RGUSMRW:RAY RT & LS SVC CONS",
							"RGUSDRV:RAY RV & BOATS",
							"RGUSPSE:RAY SCI INS CTL & FLT",
							"RGUSPSP:RAY SCI INS POL CTRL",
							"RGUSPSI:RAY Sci INSTR ELEC",
							"RGUSFSB:RAY SEC BRKG & SVC",
							"RGUSTSC:RAY SE COND & COMP",
							"RGUSPSH:RAY SHIPPING",
							"RGUSDSR:RAY SPEC RET",
							"RGUSMST:RAY STEEL",
							"RGUSMSY:RAY SYN FIBR & CHEM",
							"RGUSTTM:RAY TECHNOLOGY MISC",
							"RGUSTTE:RAY TELEG EQ",
							"RGUSDTX:RAY TEXT APP & SHORES",
							"RGUSMTP:RAY TEXTILE PROD",
							"RGUSSTO:RAY TOBACC0",
							"RGUSDTY:RAY TOYS",
							"RGUSPTM:RAY TRANS MISC",
							"RGUSPTK:RAY TRUCK MISC",
							"RGUSUUE:RAY UTIL ELEC",
							"RGUSUUG:RAY UTIL GAS DIST",
							"RGUSUUT:RAY UTIL TELE",
							"RGUSUUW:RAY UTIL WATER"
						});

						ok = true;
					}
				}
			}

			return ok;
		}

		public bool setNavigationLevel5(string country, string group, string industry, StackPanel panel, MouseButtonEventHandler mouseDownEvent)
		{
			_activeNavigationPathIndex = 5;
			_navigationPath[4] = industry;
			_navigationPath[5] = "";

			panel.Children.Clear();

			string[] fields = country.Split(':');
			country = (fields.Length > 1) ? fields[1] : fields[0];

			bool ok = false;

			fields = group.Split(':');
			string groupSymbol = fields[0];

			fields = industry.Split(':');
			string subgroupSymbol = fields[0];

			if (groupSymbol == "BWORLD" || groupSymbol == "BWORLD >")
			{
				if (subgroupSymbol == "BWORLDL1")
				{
					setNavigation(panel, mouseDownEvent, new string[]
					{
							"BWFINL:WORLD FINANCIAL",
							"BWCNCY:WORLD CON NON CYC",
							"BWINDU:WORLD INDUSTRIAL",
							"BWCCYS:WORLD CON CYC",
							"BWCOMM:WORLD COMM",
							"BWTECH:WORLD TECH",
							"BWENRS:WORLD ENERGY",
							"BWBMAT:WORLD BASIC MAT",
							"BWUTIL:WORLD UTILITIES"
					});

					ok = true;
				}

				else if (subgroupSymbol == "WORLD INDUSTRIES")
				{
					setNavigation(panel, mouseDownEvent, new string[]
					{
							 "BWBANK:WORLD BANKS",
							 "BWOILP:WORLD OIL & GAS PRO",
							 "BWPHRM:WORLD PHARMACEUT",
							 "BWITNT:WORLD INTERNET",
							 "BWRETL:WORLD RETAIL",
							 "BWTELE:WORLD TELECOM",
							 "BWINSU:WORLD INSURANCE",
							 "BWSFTW:WORLD SOFTWARE",
							 "BWDFIN:WORLD DIV FIN SER",
							 "BWCOMP:WORLD COMPUTERS",
							 "BWFOOD:WORLD FOOD",
							 "BWSEMI:WORLD SEMICONDUCT",
							 "BWCHEM:WORLD CHEMICALS",
							 "BWELEC:WORLD ELECTRIC",
							 "BWREIT:WORLD REIT",
							 "BWCMMS:WORLD COMMER SER",
							 "BWREAL:WORLD REAL ESTATE",
							 "BWBEVG:WORLD BEVERAGES",
							 "BWHCPR:WORLD HEALTH CARE PR",
							 "BWELCT:WORLD ELECTRONICS",
							 "BWTRAN:WORLD TRANSPORT",
							 "BWAUTM:WORLD AUTO MANUF",
							 "BWMING:WORLD MINING",
							 "BWMEDA:WORLD MEDIA",
							 "BWENGN:WORLD ENGIN & CON",
							 "BWBIOT:WORLD BIO TECH",
							 "BWAERO:WORLD AEROSP/DEF",
							 "BWMMAN:WORLD MISC-MANU",
							 "BWHCSV:WORLD HEALTH C SV",
							 "BWBUIL:WORLD BUILDING MA",
							 "BWAPPR:WORLD APPAREL",
							 "BWCOSM:WORLD COSM/PER CA",
							 "BWAGRI:WORLD AGRICULTURE",
							 "BWMCHD:WORLD MACH-DIVERS",
							 "BWAUTP:WORLD A PARTS/EQ",
							 "BWIRON:WORLD IRON/STEEL",
							 "BWELCM:WORLD ELEC COM/EQ",
							 "BWDIST:WORLD DIST/WHOLES",
							 "BWLODG:WORLD LODGING",
							 "BWHFUR:WORLD HOME FURNIS",
							 "BWINVS:WORLD INVEST COMP",
							 "BWGAS:WORLD GAS",
							 "BWMCHC:WORLD MAC-CONS/MI",
							 "BWAIRL:WORLD AIRLINES",
							 "BWENTE:WORLD ENTERTAINMT",
							 "BWOILS:WORLD OIL & GAS SER",
							 "BWPIPE:WORLD PIPELINES",
							 "BWLEIS:WORLD LEISURE TI",
							 "BWHOUS:WORLD HOSHLD PR/W",
							 "BWMETL:WORLD MET FAB/HDW",
							 "BWHBLD:WORLD HOME BUILD",
							 "BWFRST:WORLD FOR PROD/PA",
							 "BWTOOL:WORLD HAND/MACH",
							 "BWENVR:WORLD ENVIR CONTL",
							 "BWPACK:WORLD PACKAGING",
							 "BWCOAL:WORLD COAL",
							 "BWADVT:WORLD ADVERTISING",
							 "BWENRG:WORLD ENERGY-ATL",
							 "BWWATR:WORLD WATER",
							 "BWTEXT:WORLD TEXTILES",
							 "BWTOYS:WORLD TOY/GAM/HOB",
							 "BWOFFE:WORLD OFF/BUS EQU",
							 "BWFSRV:WORLD FOOD SERVIC",
							 "BWSAVL:WORLD SAV & LOANS",
							 "BWSHIP:WORLD SHIPBUILDING",
							 "BWHWAR:WORLD HOUSEWARES",
							 "BWSTOR:WORLD STOR/WAREH",
							 "BWOFUR:WORLD OFFICE FURN",
							 "BWTRUC:WORLD TRUCK & LEAS"
					});

					ok = true;
				}
			}


			if (groupSymbol == "BWORLDUS" || groupSymbol == "BWORLDUS >")
			{
				if (subgroupSymbol == "AMERICAS INDUSTRIES")
				{
					setNavigation(panel, mouseDownEvent, new string[]
					{
							"BUSBANK:AMER BANKS",
							"BUSSFTW:AMER SOFTWARE",
							"BUSRETL:AMER RETAIL",
							"BUSPHRM:AMER PHARMACEUTICAL",
							"BUSCOMP:AMER COMPUTERS",
							"BUSOILP:AMER OIL & GAS PROD",
							"BUSINSU:AMER INSURANCE",
							"BUSDFIN:AMER DIV FIN SERV",
							"BUSSEMI:AMER SEMICONDUCTOR",
							"BUSHCPR:AMER HEALTH-PRODUCT",
							"BUSTELE:AMER TELECOMM",
							"BUSCMMS:AMER COMM SERVICE",
							"BUSELEC:AMER ELECTRIC",
							"BUSBIOT:AMER BIOTECHNOLOGY",
							"BUSAERO:AMER AERO/DEFENSE",
							"BUSHCSV:AMER HEALTH-SERVICE",
							"BUSMEDA:AMER MEDIA",
							"BUSCHEM:AMER CHEMICALS",
							"BUSBEVG:AMER BEVERAGES",
							"BUSFOOD:AMER FOOD",
							"BUSTRAN:AMER TRANSPORTATION",
							"BUSMMAN:AMER MISC-MANUFACT",
							"BUSELCT:AMER ELECTRONICS",
							"BUSCOSM:AMER COSMET/PERS",
							"BUSAGRI:AMER AGRICULTURE",
							"BUSMING:AMER MINING",
							"BUSMCHD:AMER MACH-DIVERS",
							"BUSPIPE:AMER PIPELINES",
							"BUSAUTM:AMER AUTO MANUFACT",
							"BUSOILS:AMER OIL & GAS SERV",
							"BUSAPPR:AMER APPAREL",
							"BUSLODG:AMER LODGING",
							"BUSBUIL:AMER BUILDING MAT",
							"BUSAIRL:AMER AIRLINES",
							"BUSAUTP:AMER AUTO PART/EQP",
							"BUSIRON:AMER IRON/STEEL",
							"BUSINVS:AMER INVESTMENT CO",
							"BUSELCM:AMER ELEC COMP/EQP",
							"BUSENVR:AMER ENVIRON CTRL",
							"BUSLEIS:AMER LEISURE TIME",
							"BUSGAS:AMER GAS",
							"BUSPACK:AMER PACK & CONTAIN",
							"BUSMCHC:AMER MACH-CONST/MIN",
							"BUSHOUS:AMER HOUSE PRD/WARE",
							"BUSENTE:AMER ENTERTAINMENT",
							"BUSENGN:AMER ENGIN & CONST",
							"BUSDIST:AMER DIST/WHOLE",
							"BUSHBLD:AMER HOME BUILDERS",
							"BUSREAL:AMER REAL ESTATE",
							"BUSFRST:AMER FOR PROD/PAPER",
							"BUSSAVL:AMER SAV & LOANS",
							"BUSTOOL:AMER HAND/MACH TOOL",
							"BUSWATR:AMER WATER",
							"BUSENRG:AMER ENRG-ALT SRCE",
							"BUSMETL:AMER METAL FAB/HRD",
							"BUSADVT:AMER ADVERTISING",
							"BUSHWAR:AMER HOUSEWARES",
							"BUSHFUR:AMER HOME FURNISH",
							"BUSTEXT:AMER TEXTILES",
							"BUSTOYS:AMER TOY/GAME/HOB",
							"BUSSTOR:AMER STOR/WAREHOUS",
							"BUSOFUR:AMER OFFICE FURN",
							"BUSOFFE:AMER OFFC/BUS EQUP",
							"BUSCOAL:AMER COAL",
							"BUSTRUC:AMER TRUCK & LEAS",
							"BUSFSRV:AMER FOOD SERVICE",
							"BUSSHIP:AMER SHIPBUILDING"
					});

					ok = true;
				}
			}

			if (groupSymbol == "BWORLDEU" || groupSymbol == "BWORLDEU >")
			{
				if (subgroupSymbol == "EMEA INDUSTRIES")
				{
					setNavigation(panel, mouseDownEvent, new string[]
					{
							"BEUBANK:EMEA BANKS",
							"BEUOILP:EMEA OIL & GAS PRODC",
							"BEUPHRM:EMEA PHARMACEUTICALS",
							"BEUFOOD:EMEA FOOD",
							"BEUTELE:EMEA TELECOMM",
							"BEUCHEM:EMEA CHEMICALS",
							"BEUINSU:EMEA INSURANCE",
							"BEUELEC:EMEA ELECTRIC",
							"BEUAPPR:EMEA APPAREL",
							"BEUBEVG:EMEA BEVERAGES",
							"BEUMING:EMEA MINING",
							"BEURETL:EMEA RETAIL",
							"BEUCMMS:EMEA COMM SERVS",
							"BEUENGN:EMEA ENGIN & CONSTRU",
							"BEUSFTW:EMEA SOFTWARE",
							"BEUMEDA:EMEA MEDIA",
							"BEUREAL:EMEA REAL ESTATE",
							"BEUCOSM:EMEA COSM/PER CARE",
							"BEUAERO:EMEA AERO/DEFENSE",
							"BEUBUIL:EMEA BUILDING MAT",
							"BEUAUTM:EMEA AUTO MANUFAC",
							"BEUINVS:EMEA INVESTMENT CO",
							"BEUMMAN:EMEA MISCELL-MANU",
							"BEUDFIN:EMEA DIV FINL SERV",
							"BEUHCPR:EMEA HEALTH CARE-PRD",
							"BEUREIT:EMEA REIT",
							"BEUAGRI:EMEA AGRICULTURE",
							"BEUTRAN:EMEA TRANSPORTATION",
							"BEUSEMI:EMEA SEMICONDUCTORS",
							"BEUMCHD:EMEA MACH- DIV",
							"BEUHCSV:EMEA HEALTH CARE-SRV",
							"BEUIRON:EMEA IRON/STEEL",
							"BEUGAS:EMEA GAS",
							"BEUELCT:EMEA ELECTRONICS",
							"BEUAUTP:EMEA AUTO PARTS &EQP",
							"BEUELCM:EMEA ELEC COMP&EQUIP",
							"BEUBIOT:EMEA BIOTECHNOLOGY",
							"BEUFRST:EMEA FOREST PROD/PAP",
							"BEUMCHC:EMEA MACH- CONST/MIN",
							"BEUCOMP:EMEA COMPUTER",
							"BEUAIRL:EMEA AIRLINES",
							"BEUHOUS:EMEA HOUSEHOLD PRODT",
							"BEUDIST:EMEA DIST/WHLSALE",
							"BEUTOOL:EMEA HAND/MACH TOOLS",
							"BEUMETL:EMEA METAL FAB/HDWR",
							"BEUADVT:EMEA ADVERTISING",
							"BEUFSRV:EMEA FOOD SERVICE",
							"BEULEIS:EMEA LEISURE TIME",
							"BEUHBLD:EMEA HOME BUILDERS",
							"BEULODG:EMEA LODGING",
							"BEUENTE:EMEA ENTERTAINMENT",
							"BEUWATR:EMEA WATER INDEX",
							"BEUENRG:EMEA ENERGY-ALT SRC",
							"BEUOILS:EMEA OIL & GAS SERVS",
							"BEUHFUR:EMEA HOME FURNISHING",
							"BEUPACK:EMEA PACK & CONTAINR",
							"BEUENVR:EMEA ENVIRON CONTR",
							"BEUITNT:EMEA INTERNET INDEX",
							"BEUSTOR:EMEA STORAGE/WAREHOU",
							"BEUTEXT:EMEA TEXTILES",
							"BEUCOAL:EMEA COAL",
							"BEUHWAR:EMEA HOUSEWARES",
							"BEUOFFE:EMEA OFFICE/BUS EQUP",
							"BEUOFUR:EMEA OFFICE FURNISH",
							"BEUPIPE:EMEA PIPELINES",
							"BEUSAVL:EMEA SAVINGS & LOANS",
							"BEUSHIP:EMEA SHIPBUILDING",
							"BEUTOYS:EMEA TOYS/GAMES/HOBB",
							"BEUTRUC:EMEA TRUCKING&LEASIN"
					});

					ok = true;
				}
			}

			if (groupSymbol == "BWORLDPR" || groupSymbol == "BWORLDPR >")
			{
				if (subgroupSymbol == "ASIA PACIFIC INDUSTRIES")
				{
					setNavigation(panel, mouseDownEvent, new string[]
					{
							"BPRBANK:AP BANKS",
							"BPRREAL:AP REAL ESTATE",
							"BPRTELE:AP TELECOMM",
							"BPRINSU:AP INSURANCE",
							"BPRDFIN:AP DIVERS FINCL SVCS",
							"BPRPHRM:AP PHARMACEUTICALS",
							"BPROILP:AP OIL & GAS PRODUCR",
							"BPRSEMI:AP SEMICONDUCTORS",
							"BPRELCT:AP ELECTRONICS",
							"BPRAUTM:AP AUTO MANUFACTURER",
							"BPRFOOD:AP FOOD",
							"BPRCHEM:AP CHEMICALS",
							"BPRENGN:AP ENGINEER & CONST",
							"BPRRETL:AP RETAIL",
							"BPRTRAN:AP TRANSPORTATION",
							"BPRELEC:AP ELECTRIC",
							"BPRMING:AP MINING",
							"BPRAUTP:AP AUTO PTS & EQUIP",
							"BPRCOMP:AP COMPUTERS",
							"BPRBEVG:AP BEVERAGES",
							"BPRHFUR:AP HOME FURNISHINGS",
							"BPRCMMS:AP COMMERCIAL SVCS",
							"BPRBUIL:AP BUILDING MATERIAL",
							"BPRDIST:AP DIST/WHOLESALE",
							"BPRMCHD:AP MACH-DIVERSIFIED",
							"BPRMMAN:AP MISC-MANUFACTURE",
							"BPRAGRI:AP AGRICULTURE",
							"BPRIRON:AP IRON/STEEL",
							"BPRELCM:AP ELE COMP & EQUIP",
							"BPRENTE:AP ENTERTAINMENT",
							"BPRCOAL:AP COAL",
							"BPRSFTW:AP SOFTWARE",
							"BPRLODG:AP LODGING",
							"BPRBIOT:AP BIOTECH",
							"BPRCOSM:AP COSMETICS/PER CAR",
							"BPRMCHC:AP MACH-CNSTR & MINE",
							"BPRGAS:AP GAS",
							"BPRHCPR:AP HEALTH CARE-PRODS",
							"BPRMETL:AP METAL FABR/HARDWR",
							"BPRLEIS:AP LEISURE TIME",
							"BPRHOUS:AP HSEHLD PROD/WARES",
							"BPRAIRL:AP AIRLINES",
							"BPRHCSV:AP HEALTH CARE-SVCS",
							"BPRAPPR:AP APPAREL",
							"BPRTOOL:AP HAND/MACHINE TOOL",
							"BPRHBLD:AP HOME BUILDERS",
							"BPROFFE:AP OFFICE/BUS EQUIP",
							"BPRTEXT:AP TEXTILES",
							"BPRENVR:AP ENVIRONMTL CONTRL",
							"BPRTOYS:AP TOYS/GAMES/HOBBY",
							"BPRPACK:AP PACKAGING & CONT",
							"BPRENRG:AP ENERGY-ALT SOURCE",
							"BPRADVT:AP ADVERTISING",
							"BPRFRST:AP FOREST PRD & PAPR",
							"BPRINVS:AP INVESTMT COMPANY",
							"BPRMEDA:AP MEDIA",
							"BPROILS:AP OIL & GAS SERVICE",
							"BPRWATR:AP WATER",
							"BPRSHIP:AP SHIPBUILDING",
							"BPRHWAR:AP HOUSEWARES",
							"BPRAERO:AP AEROSPACE/DEFENSE",
							"BPRSTOR:AP STORAGE/WAREHOUSE",
							"BPROFUR:AP OFFICE FURNISHING",
							"BPRFSRV:PR FOOD SERVICE",
							"BPRPIPE:AP PIPELINES",
							"BPRSAVL:AP SAVINGS & LOANS",
							"BPRTRUC:AP TRUCKING/LEASING"
					});

					ok = true;
				}
			}

			if (country == "US EQ >" || country == "USA")
			{
				if (groupSymbol == "SPX")
				{
					if (subgroupSymbol == "SPXL1")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"S5COND:SPX CONS DISC",
							"S5CONS:SPX CONS STAPLES",
							"S5ENRS:SPX ENERGY",
							"S5FINL:SPX FINANCIALS",
							"S5HLTH:SPX HEALTH CARE",
							"S5INDU:SPX INDUSTRIALS",
							"S5INFT:SPX INFO TECH",
							"S5MATR:SPX MATERIALS",
							"S5TELS:SPX TELECOM",
							"S5UTIL:SPX UTILITIES" });

						ok = true;
					}

					else if (subgroupSymbol == "SPXL2")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"S5AUCO:SPX AUTO & COMP",
							"S5BANKX:SPX BANKS",
							"S5CODU:SPX CAPITAL GOODS",
							"S5COMS:SPX COMM & PROF",
							"S5CPGS:SPX CON DUR & AP",
							"S5DIVF:SPX DIV FINANCE",
							"S5ENRSX:SPX ENERGY",
							"S5FDBT:SPX FOOD/STAPLES",
							"S5FDSR:SPX FOOD BEV TOB",
							"S5HCES:SPX HC EQUIP",
							"S5HOTR:SPX CONS SERV",
							"S5HOUS:SPX HOUSEHOLD PROD",
							"S5INSU:SPX INSURANCE",
							"S5MATRX:SPX MATERIALS",
							"S5MEDA:SPX MEDIA",
							"S5PHRM:SPX PHRM BIO & LIFE",
							"S5REAL:SPX REAL ESTATE",
							"S5RETL:SPX RETAILING",
							"S5SSEQX:SPX SEMI & EQP",
							"S5SFTW:SPX SOFTWARE & SVCS",
							"S5TECH:SPX TECH HW & EQP",
							"S5TELSX:SPX TELECOM SVCS",
							"S5TRAN:SPX TRANSPORT",
							"S5UTILX:SPX UTILTIES"
						});

						ok = true;
					}

					else if (subgroupSymbol == "SPXL3")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"S5AEROX:SPX AERO & DEF",
							"S5AIRFX:SPX AIR FRT & LOG",
							"S5AIRLX:SPX AIRLINES",
							"S5AUTC:SPX AUTO COMP",
							"S5AUTO:SPX AUTOMOBILES",
							"S5BEVG:SPX BEVERAGES",
							"S5BIOTX:SPX BIOTECH",
							"S5BUILX:SPX BLDG PRODS",
							"S5CAPM:SPX CAPITAL MKTS",
							"S5CBNK:SPX COMM BANKS",
							"S5CFINX:SPX CONS FINANCE",
							"S5CHEM:SPX CHEMICALS",
							"S5CMPE:SPX COMPUTERS & PER",
							"S5COMM:SPX COMMUNICATION EQP",
							"S5COMSX:SPX COMMERCIAL SRBS",
							"S5CONP:SPX CONTAINER & PKG",
							"S5CSTEX:SPX CONST & ENG",
							"S5CSTMX:SPX CONST MATERIAL",
							"S5DCON:SPX DIVERSIFIED SRVC",
							"S5DISTX:SPX DISBRIBUTORS",
							"S5DIVT:SPX DIV TEL SVC",
							"S5DVFS:SPX DIV FIN SVC",
							"S5ELEIX:SPX ELECTRONIC EQUP",
							"S5ELEQ:SPX ELECTRICAL EQUP",
							"S5ELUTX:SPX ELECTRIC UTL",
							"S5ENRE:SPX ENERGY EQUP & SV",
							"S5FDPR:SPX FOOD PROD IND",
							"S5FDSRX:SPX FOOD & STAPLES RET",
							"S5GASUX:SPX GAS UTL",
							"S5HCEQ:SPX HC EQUP & SUP",
							"S5HCPS:SPX HC PROVIDERS SVC",
							"S5HCTEX:SPX HC TECHNOLOGY",
							"S5HODU:SPX HOUSEHOLD DURABLES",
							"S5HOPRX:SPX HOUSELHOLD PROD",
							"S5HOTRX:SPX HOTELS REST & LEIS",
							"S5INCR:SPX INTERNET CATALOG",
							"S5INDCX:SPX INDUSTRIAL CONGL",
							"S5INSSX:SPX INTERNET SOFTWARE",
							"S5INSUX:SPX INSUURANCE IND",
							"S5IPPEX:SPX INDEP PWR PROD",
							"S5ITSV:SPX IT SERV IND",
							"S5LEIS:SPX LEISURE EQUP",
							"S5LSTSX:SPX LIFE SCI IND",
							"S5MACH:SPX MACHINERY",
							"S5MDREX:SPX RE MGM",
							"S5MEDAX:SPX MEDIA",
							"S5METL:SPX METAL & MIN",
							"S5MRET:SPX MULTILINE RET",
							"S5MUTIX:SPX MULTI UTL",
							"S5OFFEX:SPX OFFICE ELECT",
							"S5OILG:SPX OIL GAS FUEL",
							"S5PAFO:SPX PAPER FORSET PROD",
							"S5PERSX:SPX PERSONAL PROD",
							"S5PHARX:SPX PHARMA",
							"S5PRSV:SPX PROF SRVS",
							"S5REITS:SPX RE INV TRUSTS",
							"S5ROAD:SPX ROARD & RAIL",
							"S5SOFT:SPX SOFTWARE",
							"S5SPRE:SPX SPECIALTY RET",
							"S5SSEQ:SPX SEMICOND & EQUP",
							"S5TEXA:SPX TXTL & APPRL",
							"S5THMFX:SPX THRIFTS & MORT",
							"S5TOBAX:SPX TOBACCO",
							"S5TRADX:SPX TRADING CO & DIS",
							"S5WIREX:SPX WIRELESS TELECOM"
						});

						ok = true;
					}

					else if (subgroupSymbol == "SPXL4")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"SPW:SPX Equal Weight Index",
							"SPXEWTR:SPX Equal Weighted USD Total Return Index",
							"SPXEW4UP:SPX Equal Weight Communication Services Plus Index",
							"SPXEW4UT:SPX Equal Weight Communication Services Plus Index TR",
							"SPXEWCD:SPX Equal Weight Index Consumers Discretionary",
							"SPXEWCS:SPX Equal Weight Consumer Staples Total Return Index",
							"SPXEWEN:SPX Equal Weight Energy (Sector) Total Return USD Index",
							"SPXEWFN:SPX Equal Weight Financials (Sector) Total Return Index",
							"SPXEWHC:SPX Equal Weighted Health Care Sector USD Total Return Index",
							"SPXEWIN:SPX Equal Weighted Industrials USD Total Return Index",
							"SPXEWIT:SPX Equal Weighted InTc USD Total Return Index",
							"SPXEWMA:SPX Equal Weighted Materials USD Total Return Index",
							"SPXEREUT:SPX Equal Weight Real Estate Index TR",
							"SPXEWTS:SPX Equal Weight Telecommunication Services (Sector) Total Return",
							"SPXEWUT:SPX Equal Weight Utilities (Sector) Total Return USD Index "
					});

						ok = true;
					}
				}

				else if (groupSymbol == "MID")
				{
					if (subgroupSymbol == "MIDL1")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"S4COND:MID CONS DISC",
							"S4CONS:MID CONS STAPLES",
							"S4ENRS:MID ENERGY",
							"S4FINL:MID FINANCIALS",
							"S4HLTH:MID HEALTH CARE",
							"S4INDU:MID INDUSTRIALS",
							"S4INFT:MID INFO TECH",
							"S4MATR:MID MATERIALS",
							"S4TELS:MID TELECOM",
							"S4UTIL:MID UTILITIES"
						});

						ok = true;
					}

					else if (subgroupSymbol == "MIDL2")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"S4AUCO:MID AUTO & COMP",
							"S4BANKX:MID BANKS",
							"S4CODU:MID CAPITAL GOODS",
							"S4COMS:MID COMM & PROF",
							"S4CPGS:MID CON DUR & AP",
							"S4DIVF:MID DIV FINANCE",
							"S4ENRSX:MID ENERGY",
							"S4FDBT:MID FOOD/STAPLES",
							"S4FDSR:MID FOOD BEV TOB",
							"S4HCES:MID HC EQUIP",
							"S4HOTR:MID CONS SERV",
							"S4HOUS:MID HOUSEHOLD PROD",
							"S4INSU:MID INSURANCE",
							"S4MATRX:MID MATERIALS",
							"S4MEDA:MID MEDIA",
							"S4PHRM:MID PHRM BIO & LIFE",
							"S4REAL:MID REAL ESTATE",
							"S4RETL:MID RETAILING",
							"S4SSEQX:MID SEMI & EQP",
							"S4SFTW:MID SOFTWARE & SVCS",
							"S4TECH:MID TECH HW & EQP",
							"S4TELSX:MID TELECOM SVCS",
							"S4TRAN:MID TRANSPORT",
							"S4UTILX:MID UTILTIES"
						});

						ok = true;
					}

					else if (subgroupSymbol == "MIDL3")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"S4AEROX:MID AERO & DEF",
							"S4AIRFX:MID AIR FRT & LOG",
							"S4AIRLX:MID AIRLINES",
							"S4AUTC:MID AUTO COMP",
							"S4AUTO:MID AUTOMOBILES",
							"S4BEVG:MID BEVERAGES",
							"S4BIOTX:MID BIOTECH",
							"S4BUILX:MID BLDG PRODS",
							"S4CAPM:MID CAPITAL MKTS",
							"S4CBNK:MID COMM BANKS",
							"S4CFINX:MID CONS FINANCE",
							"S4CHEM:MID CHEMICALS",
							"S4CMPE:MID COMPUTERS & PER",
							"S4COMM:MID COMMUNICATION EQP",
							"S4COMSX:MID COMMERCIAL SRBS",
							"S4CONP:MID CONTAINER & PKG",
							"S4CSTEX:MID CONST & ENG",
							"S4CSTMX:MID CONST MATERIAL",
							"S4DCON:MID DIVERSIFIED SRVC",
							"S4DISTX:MID DISBRIBUTORS",
							"S4DIVT:MID DIV TEL SVC",
							"S4DVFS:MID DIV FIN SVC",
							"S4ELEIX:MID ELECTRONIC EQUP",
							"S4ELEQ:MID ELECTRICAL EQUP",
							"S4ELUTX:MID ELECTRIC UTL",
							"S4ENRE:MID ENERGY EQUP & SV",
							"S4FDPR:MID FOOD PROD IND",
							"S4FDSRX:MID FOOD & STAPLES RET",
							"S4GASUX:MID GAS UTL",
							"S4HCEQ:MID HC EQUP & SUP",
							"S4HCPS:MID HC PROVIDERS SVC",
							"S4HCTEX:MID HC TECHNOLOGY",
							"S4HODU:MID HOUSEHOLD DURABLES",
							"S4HOPRX:MID HOUSELHOLD PROD",
							"S4HOTRX:MID HOTELS REST & LEIS",
							"S4INCR:MID INTERNET CATALOG",
							"S4INDCX:MID INDUSTRIAL CONGL",
							"S4INSSX:MID INTERNET SOFTWARE",
							"S4INSUX:MID INSUURANCE IND",
							"S4ITSV:MID IT SERV IND",
							"S4LEIS:MID LEISURE EQUP",
							"S4LSTSX:MID LIFE SCI IND",
							"S4MACH:MID MACHINERY",
							"S4MARIX:MID MARINE",
							"S4MDREX:MID RE MGM",
							"S4MEDAX:MID MEDIA",
							"S4METL:MID METAL & MIN",
							"S4MRET:MID MULTILINE RET",
							"S4MUTIX:MID MULTI UTL",
							"S4OFFEX:MID OFFICE ELECT",
							"S4OILG:MID OIL GAS FUEL",
							"S4PAFO:MID PAPER FORSET PROD",
							"S4PHARX:MID PHARMA",
							"S4PRSV:MID PROF SRVS",
							"S4REITS:MID RE INV TRUSTS",
							"S4ROAD:MID ROARD & RAIL",
							"S4SOFT:MID SOFTWARE",
							"S4SPRE:MID SPECIALTY RET",
							"S4SSEQ:MID SEMICOND & EQUP",
							"S4TEXA:MID TXTL & APPRL",
							"S4THMFX:MID THRIFTS & MORT",
							"S4TOBAX:MID TOBACCO",
							"S4TRADX:MID TRADING CO & DIS",
							"S4WATUX:MID WATER UTL",
							"S4WIREX:MID WIRELESS TELECOM"
						});

						ok = true;
					}
					else if (subgroupSymbol == "MIDL4")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"MIDEWI:MID Equal Weighted Index",
						});

						ok = true;
					}
				}

				if (groupSymbol == "SML")
				{
					if (subgroupSymbol == "SMLL1")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"S6COND:SML CONS DISC >",
							"S6CONS:SML CONS STAPLES >",
							"S6ENRS:SML ENERGY >",
							"S6FINL:SML FINANCIALS >",
							"S6HLTH:SML HEALTH CARE >",
							"S6INDU:SML INDUSTRIALS >",
							"S6INFT:SML INFO TECH >",
							"S6MATR:SML MATERIALS >",
							"S6TELS:SML TELECOM >",
							"S6UTIL:SML UTILITIES >"
						});

						ok = true;
					}

					else if (subgroupSymbol == "SMLL2")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"S6AUCO:SML AUTO & COMP >",
							"S6BANKX:SML BANKS >",
							"S6CODU:SML CAPITAL GOODS >",
							"S6COMS:SML COMM & PROF >",
							"S6CPGS:SML CON DUR & AP >",
							"S6DIVF:SML DIV FINANCE >",
							"S6ENRSX:SML ENERGY >",
							"S6FDBT:SML FOOD/STAPLES >",
							"S6FDSR:SML FOOD BEV TOB >",
							"S6HCES:SML HC EQUIP >",
							"S6HOTR:SML CONS SERV >",
							"S6HOUS:SML HOUSEHOLD PROD >",
							"S6INSU:SML INSURANCE >",
							"S6MATRX:SML MATERIALS >",
							"S6MEDA:SML MEDIA >",
							"S6PHRM:SML PHRM BIO & LIFE >",
							"S6REAL:SML REAL ESTATE >",
							"S6RETL:SML RETAILING >",
							"S6SSEQX:SML SEMI & EQP >",
							"S6SFTW:SML SOFTWARE & SVCS >",
							"S6TECH:SML TECH HW & EQP >",
							"S6TELSX:SML TELECOM SVCS >",
							"S6TRAN:SML TRANSPORT >",
							"S6UTILX:SML UTILTIES >"
						});

						ok = true;
					}

					else if (subgroupSymbol == "SMLL3")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"S6AEROX:SML AERO & DEF >",
							"S6AIRFX:SML AIR FRT & LOG >",
							"S6AIRLX:SML AIRLINES >",
							"S6AUTC:SML AUTO COMP >",
							"S6AUTO:SML AUTOMOBILES >",
							"S6BEVG:SML BEVERAGES >",
							"S6BIOTX:SML BIOTECH > >",
							"S6BUILX:SML BLDG PRODS >",
							"S6CAPM:SML CAPITAL MKTS >",
							"S6CBNK:SML COMM BANKS >",
							"S6CFINX:SML CONS FINANCE >",
							"S6CHEM:SML CHEMICALS >",
							"S6CMPE:SML COMPUTERS & PER >",
							"S6COMM:SML COMMUNICATION EQP >",
							"S6COMSX:SML COMMERCIAL SRBS >",
							"S6CONP:SML CONTAINER & PKG >",
							"S6CSTEX:SML CONST & ENG >",
							"S6CSTMX:SML CONST MATERIAL >",
							"S6DCON:SML DIVERSIFIED SRVC >",
							"S6DISTX:SML DISBRIBUTORS >",
							"S6DIVT:SML DIV TEL SVC >",
							"S6DVFS:SML DIV FIN SVC >",
							"S6ELEIX:SML ELECTRONIC EQUP >",
							"S6ELEQ:SML ELECTRICAL EQUP >",
							"S6ELUTX:SML ELECTRIC UTL >",
							"S6ENRE:SML ENERGY EQUP & SV >",
							"S6FDPR:SML FOOD PROD IND >",
							"S6FDSRX:SML FOOD & STAPLES RET >",
							"S6GASUX:SML GAS UTL >",
							"S6HCEQ:SML HC EQUP & SUP >",
							"S6HCPS:SML HC PROVIDERS SVC >",
							"S6HCTEX:SML HC TECHNOLOGY >",
							"S6HODU:SML HOUSEHOLD DURABLES >",
							"S6HOPRX:SML HOUSELHOLD PROD >",
							"S6HOTRX:SML HOTELS REST & LEIS >",
							"S6INCR:SML INTERNET CATALOG >",
							"S6INDCX:SML INDUSTRIAL CONGL >",
							"S6INSSX:SML INTERNET SOFTWARE >",
							"S6INSUX:SML INSUURANCE IND >",
							"S6ITSV:SML IT SERV IND >",
							"S6LEIS:SML LEISURE EQUP >",
							"S6LSTSX:SML LIFE SCI IND >",
							"S6MACH:SML MACHINERY >",
							"S6MARIX:SML MARINE >",
							"S6MDREX:SML RE MGM >",
							"S6MEDAX:SML MEDIA >",
							"S6METL:SML METAL & MIN >",
							"S6MRET:SML MULTILINE RET >",
							"S6MUTIX:SML MULTI UTL >",
							"S6OILG:SML OIL GAS FUEL >",
							"S6PAFO:SML PAPER FORSET PROD >",
							"S6PERSX:SML PERSONAL PROD >",
							"S6PHARX:SML PHARMA >",
							"S6PRSV:SML PROF SRVS >",
							"S6REITS:SML RE INV TRUSTS >",
							"S6ROAD:SML ROARD & RAIL >",
							"S6SOFT:SML SOFTWARE >",
							"S6SPRE:SML SPECIALTY RET >",
							"S6SSEQ:SML SEMICOND & EQUP >",
							"S6TEXA:SML TXTL & APPRL >",
							"S6THMFX:SML THRIFTS & MORT >",
							"S6TOBAX:SML TOBACCO >",
							"S6TRADX:SML TRADING CO & DIS >",
							"S6WATUX:SML WATER UTL >",
							"S6WIREX:SML WIRELESS TELECOM >"
						});

						ok = true;
					}
					else if (subgroupSymbol == "SMLL4")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"SMLEWI:SML Equal Weighted Index >",
						});

						ok = true;
					}
				}
				if (groupSymbol == "SPR")
				{
					if (subgroupSymbol == "SPRL1")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"S15COND:SPR CONS DISC >",
							"S15CONS:SPR CONS STAPLES >",
							"S15ENRS:SPR ENERGY >",
							"S15FINL:SPR FINANCIALS >",
							"S15HLTH:SPR HEALTH CARE >",
							"S15INDU:SPR INDUSTRIALS >",
							"S15INFT:SPR INFO TECH >",
							"S15MATR:SPR MATERIALS >",
							"S15TELS:SPR TELECOM >",
							"S15UTIL:SPR UTILITIES >"
						});

						ok = true;
					}

					else if (subgroupSymbol == "SPRL2")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"S15AUCO:SPR AUTO & COMP",
							"S15BANKX:SPR BANKS",
							"S15CODU:SPR CAPITAL GOODS",
							"S15COMS:SPR COMM & PROF",
							"S15CPGS:SPR CON DUR & AP",
							"S15DIVF:SPR DIV FINANCE",
							"S15ENRSX:SPR ENERGY",
							"S15FDBT:SPR FOOD/STAPLES",
							"S15FDSR:SPR FOOD BEV TOB",
							"S15HCES:SPR HC EQUIP",
							"S15HOTR:SPR CONS SERV",
							"S15HOUS:SPR HOUSEHOLD PROD",
							"S15INSU:SPR INSURANCE",
							"S15MATRX:SPR MATERIALS",
							"S15MEDA:SPR MEDIA",
							"S15PHRM:SPR PHRM BIO & LIFE",
							"S15REAL:SPR REAL ESTATE",
							"S15RETL:SPR RETAILING",
							"S15SSEQX:SPR SEMI & EQP",
							"S15SFTW:SPR SOFTWARE & SVCS",
							"S15TECH:SPR TECH HW & EQP",
							"S15TELSX:SPR TELECOM SVCS",
							"S15TRAN:SPR TRANSPORT",
							"S15UTILX:SPR UTILTIES"
						});

						ok = true;
					}

					else if (subgroupSymbol == "SPRL3")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"S15AEROX:SPR AERO & DEF",
							"S15AIRFX:SPR AIR FRT & LOG",
							"S15AIRLX:SPR AIRLINES",
							"S15AUTC:SPR AUTO COMP",
							"S15AUTO:SPR AUTOMOBILES",
							"S15BEVG:SPR BEVERAGES",
							"S15BIOTX:SPR BIOTECH",
							"S15BUILX:SPR BLDG PRODS",
							"S15CAPM:SPR CAPITAL MKTS",
							"S15CBNK:SPR COMM BANKS",
							"S15CFINX:SPR CONS FINANCE",
							"S15CHEM:SPR CHEMICALS",
							"S15CMPE:SPR COMPUTERS & PER",
							"S15COMM:SPR COMMUNICATION EQP",
							"S15COMSX:SPR COMMERCIAL SRBS",
							"S15CONP:SPR CONTAINER & PKG",
							"S15CSTEX:SPR CONST & ENG",
							"S15CSTMX:SPR CONST MATERIAL",
							"S15DCON:SPR DIVERSIFIED SRVC",
							"S15DISTX:SPR DISBRIBUTORS",
							"S15DIVT:SPR DIV TEL SVC",
							"S15DVFS:SPR DIV FIN SVC",
							"S15ELEIX:SPR ELECTRONIC EQUP",
							"S15ELEQ:SPR ELECTRICAL EQUP",
							"S15ELUTX:SPR ELECTRIC UTL",
							"S15ENRE:SPR ENERGY EQUP & SV",
							"S15FDPR:SPR FOOD PROD IND",
							"S15FDSRX:SPR FOOD & STAPLES RET",
							"S15GASUX:SPR GAS UTL",
							"S15HCEQ:SPR HC EQUP & SUP",
							"S15HCPS:SPR HC PROVIDERS SVC",
							"S15HCTEX:SPR HC TECHNOLOGY",
							"S15HODU:SPR HOUSEHOLD DURABLES",
							"S15HOPRX:SPR HOUSELHOLD PROD",
							"S15HOTRX:SPR HOTELS REST & LEIS",
							"S15INCR:SPR INTERNET CATALOG",
							"S15INDCX:SPR INDUSTRIAL CONGL",
							"S15INSSX:SPR INTERNET SOFTWARE",
							"S15INSUX:SPR INSUURANCE IND",
							"S15IPPEX:SPR INDEP PWR PROD",
							"S15ITSV:SPR IT SERV IND",
							"S15LEIS:SPR LEISURE EQUP",
							"S15LSTSX:SPR LIFE SCI IND",
							"S15MACH:SPR MACHINERY",
							"S15MARIX:SPR MARINE",
							"S15MDREX:SPR RE MGM",
							"S15MEDAX:SPR MEDIA",
							"S15METL:SPR METAL & MIN",
							"S15MRET:SPR MULTILINE RET",
							"S15MUTIX:SPR MULTI UTL",
							"S15OFFEX:SPR OFFICE ELECT",
							"S15OILG:SPR OIL GAS FUEL",
							"S15PAFO:SPR PAPER FORSET PROD",
							"S15PERSX:SPR PERSONAL PROD",
							"S15PHARX:SPR PHARMA",
							"S15PRSV:SPR PROF SRVS",
							"S15REITS:SPR RE INV TRUSTS",
							"S15ROAD:SPR ROARD & RAIL",
							"S15SOFT:SPR SOFTWARE",
							"S15SPRE:SPR SPECIALTY RET",
							"S15SSEQ:SPR SEMICOND & EQUP",
							"S15TEXA:SPR TXTL & APPRL",
							"S15THMFX:SPR THRIFTS & MORT",
							"S15TOBAX:SPR TOBACCO",
							"S15TRADX:SPR TRADING CO & DIS",
							"S15WATUX:SPR WATER UTL",
							"S15WIREX:SPR WIRELESS TELECOM"
						});

						ok = true;
					}
					else if (subgroupSymbol == "SPRL4")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"SPRCEWUP:SPR Equal Weighted Index"
						});

						ok = true;
					}
				}
				if (subgroupSymbol == "NDXL1")
				{
					setNavigation(panel, mouseDownEvent, new string[]
					{
							"CBNK:NDX BANK",
							"NBI:NDX BIOTECHNOLOGY",
							"IXK:NDX COMPUTER",
							"NDF:NDX FINANCIAL",
							"CIND:NDX INDUSTRIAL",
							"CINS:NDX INSURANCE",
							"CUTL:NDX TELECOMMUNICATION",
							"CTRN:NDX TRANSPORTATION"
					});

					ok = true;
				}

				if (groupSymbol == "RIY")  //Russell 1000
				{
					if (subgroupSymbol == "RIYL1")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"RGUSDL:RIY CONS DISC",
							"RGUSSL:RIY CONS STAPLES",
							"RGUSEL:RIY ENERGY",
							"RGUSFL:RIY FIN SERVICES",
							"RGUSHL:RIY HEALTH CARE",
							"RGUSML:RIY MATERIALS",
							"RGUSPL:RIY PROD DURABLES",
							"RGUSTL:RIY TECHNOLOGY",
							"RGUSUL:RIY UTILITIES"
						});

						ok = true;
					}

					else if (subgroupSymbol == "RIYL2")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"RGUSDLAA:RIY AD AGENCIES",
							"RGUSPLAS:RIY AEROSPACE",
							"RGUSPLAI:RIY AIR TRANSPORT",
							"RGUSMLAL:RIY ALUMINUM",
							"RGUSFLAM:RIY ASSET MGT & CUST",
							"RGUSDLAP:RIY AUTO PARTS",
							"RGUSDLAS:RIY AUTO SVC",
							"RGUSDLAU:RIY AUTOMOBILES",
							"RGUSFLBK:RIY BANKS DVSFD",
							"RGUSSLBD:RIY BEV BRW & DSTLR",
							"RGUSSLSD:RIY BEV SOFT DRNK",
							"RGUSHLBT:RIY BIOTEC",
							"RGUSMLCC:RIY BLDG CLIMATE CTRL",
							"RGUSFLBS:RIY BNK SVG THRF MRT",
							"RGUSPLBO:RIY BO SUP HR & CONS",
							"RGUSMLBM:RIY BUILDING MATL",
							"RGUSDLCT:RIY CABLE TV SVC",
							"RGUSDLCG:RIY CASINOS & GAMB",
							"RGUSMLCM:RIY CEMENT",
							"RGUSMLCS:RIY CHEM SPEC",
							"RGUSMLCD:RIY CHEM DVFSD",
							"RGUSTLCS:RIY CMP SVC SFW & SYS",
							"RGUSELCO:RIY COAL",
							"RGUSPLCS:RIY COMM SVC",
							"RGUSFLFM:RIY COMM FIN & MORT",
							"RGUSPLCL:RIY COMM SVC RN",
							"RGUSTLCM:RIY COMM TECH",
							"RGUSPLCV:RIY COMM VEH & PRTS",
							"RGUSTLCT:RIY COMPUTER TECH",
							"RGUSPLCN:RIY CONS",
							"RGUSDLCM:RIY CONS SVC  MISC",
							"RGUSFLCL:RIY CONSUMER LEND",
							"RGUSDLCE:RIY CONSUMER ELECT",
							"RGUSMLCP:RIY CONTAINER & PKG",
							"RGUSMLCR:RIY COPPER",
							"RGUSDLCS:RIY COSMETICS",
							"RGUSSLDG:RIY DRUG & GROC CHN",
							"RGUSFLDF:RIY DVSFD FNCL SVC",
							"RGUSDLDM:RIY DVSFD MEDIA",
							"RGUSDLDR:RIY DVSFD RETAIL",
							"RGUSMLDM:RIY DVSFD MAT & PROC",
							"RGUSPLDO:RIY DVSFD MFG OPS",
							"RGUSDLES:RIY EDUCATION SVC",
							"RGUSTLEC:RIY ELECT COMP",
							"RGUSTLEE:RIY ELECT ENT",
							"RGUSTLEL:RIY ELECTRONICS",
							"RGUSELEQ:RIY ENERGY EQ",
							"RGUSPLEC:RIY ENG & CONTR SVC",
							"RGUSDLEN:RIY ENTERTAINMENT",
							"RGUSPLEN:RIY ENV MN & SEC SVC",
							"RGUSMLFT:RIY FERTILIZERS",
							"RGUSFLFD:RIY FINCL DATA & SYS",
							"RGUSSLFO:RIY FOODS",
							"RGUSMLFP:RIY FOREST PROD",
							"RGUSPLFB:RIY FRM & BLK PRNT SVC",
							"RGUSSLFG:RIY FRUIT & GRN PROC",
							"RGUSDLFU:RIY FUN PARLOR & CEM",
							"RGUSELGP:RIY GAS PIPELINE",
							"RGUSMLGO:RIY GOLD",
							"RGUSDLHE:RIY HHLD EQP & PROD",
							"RGUSDLHF:RIY HHLD FURN",
							"RGUSHLHF:RIY HLTH CARE FAC",
							"RGUSHLHS:RIY HLTH CARE SVC",
							"RGUSHLHM:RIY HLTH C MGT SVC",
							"RGUSDLHB:RIY HOMEBUILDING",
							"RGUSDLHO:RIY HOTEL/MOTEL",
							"RGUSDLHA:RIY HOUSEHOLD APPL",
							"RGUSFLIL:RIY INS LIFE",
							"RGUSFLIM:RIY INS MULTI-LN",
							"RGUSFLIP:RIY INS PROP-CAS",
							"RGUSPLIT:RIY INTL TRD & DV LG",
							"RGUSDLLT:RIY LEISURE TIME",
							"RGUSDLLX:RIY LUXURY ITEMS",
							"RGUSPLMI:RIY MACH INDU",
							"RGUSPLMT:RIY MACH TOOLS",
							"RGUSPLMA:RIY MACH AG",
							"RGUSPLME:RIY MACH ENGINES",
							"RGUSPLMS:RIY MACH SPECIAL",
							"RGUSPLMH:RIY MCH CONS & HNDL",
							"RGUSHLMD:RIY MD & DN INS & SUP",
							"RGUSHLME:RIY MED EQ",
							"RGUSHLMS:RIY MED SVC",
							"RGUSMLMD:RIY MET & MIN DVFSD",
							"RGUSMLMF:RIY METAL FABRIC",
							"RGUSSLMC:RIY MISC CONS STAPLE",
							"RGUSPLOE:RIY OFF SUP & EQ",
							"RGUSELOF:RIY OFFSHORE DRILL",
							"RGUSELOI:RIY OIL INTEGRATE",
							"RGUSELOC:RIY OIL CRUDE PROD",
							"RGUSELOR:RIY OIL REF & MKT",
							"RGUSELOW:RIY OIL WELL EQ & SVC",
							"RGUSMLPC:RIY PAINT & COATING",
							"RGUSMLPA:RIY PAPER",
							"RGUSSLPC:RIY PERSONAL CARE",
							"RGUSHLPH:RIY PHRM",
							"RGUSPLPD:RIY PROD DUR MISC",
							"RGUSTLPR:RIY PRODUCT TECH EQ",
							"RGUSDLPU:RIY PUBLISHING",
							"RGUSPLPT:RIY PWR TRANSM EQ",
							"RGUSDLRB:RIY RADIO & TV BROAD",
							"RGUSPLRL:RIY RAILROAD EQ",
							"RGUSPLRA:RIY RAILROADS",
							"RGUSFLRE:RIY REAL ESTATE",
							"RGUSFLRI:RIY REIT",
							"RGUSDLRT:RIY RESTAURANTS",
							"RGUSMLRW:RIY ROOF WALL & PLUM",
							"RGUSDLRC:RIY RT & LS SVC CONS",
							"RGUSDLRV:RIY RV & BOATS",
							"RGUSPLSI:RIY SCI INS CTL & FLT",
							"RGUSPLSP:RIY SCI INS POL CTRL",
							"RGUSPLSG:RIY SCI INST GG & MTR",
							"RGUSPLSE:RIY SCI INSTR ELEC",
							"RGUSFLSB:RIY SEC BRKG & SVC",
							"RGUSTLSC:RIY SEMI COND & COMP",
							"RGUSPLSH:RIY SHIPPING",
							"RGUSDLSR:RIY SPEC RET",
							"RGUSMLST:RIY STEEL",
							"RGUSTLTM:RIY TECHNO MISC",
							"RGUSTLTE:RIY TELE EQ",
							"RGUSDLTX:RIY TEXT APP & SHOES",
							"RGUSSLTO:RIY TOBACCO",
							"RGUSDLTY:RIY TOYS",
							"RGUSPLTM:RIY TRANS MISC",
							"RGUSPLTK:RIY TRUCKERS",
							"RGUSULUM:RIY UTIL  MISC",
							"RGUSULUE:RIY UTIL GAS DIST",
							"RGUSULUT:RIY UTIL TELE",
							"RGUSULUW:RIY UTIL WATER"
						});

						ok = true;
					}
				}

				if (groupSymbol == "RLG")  //Russell 1000 Growth
				{
					if (subgroupSymbol == "RLGL1")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"RGUSDLG:RLG CON DISC GROWTH",
							"RGUSSLG:RLG CONS STAPLES GROWTH",
							"RGUSELG:RLG ENERGY GROWTH",
							"RGUSFLG:RLG FIN SERVICES GROWTH",
							"RGUSHLG:RLG HEALTH CARE GROWTH",
							"RGUSMLG:RLG MATERIALS GROWTH",
							"RGUSPLG:RLG PROD DURABLES GROWTH",
							"RGUSTLG:RLG TECHNOLOGY GROWTH",
							"RGUSULG:RLG UTILITIES GROWTH"
						});

						ok = true;
					}

					else if (subgroupSymbol == "RLGL2")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"RGUDLAAG:RLG Ad Age Grw",
							"RGUDLAPG:RLG AutoPrts Grw",
							"RGUDLASG:RLG Auto Svc Grw",
							"RGUDLAUG:RLG Automob Grw",
							"RGUDLCEG:RLG ConsElect Grw",
							"RGUDLCGG:RLG Cas&Gamb Grw",
							"RGUDLCMG:RLG ConSvcMisc Grw",
							"RGUDLCSG:RLG Cosmetics Grw",
							"RGUDLCTG:RLG CabTV Svc Grw",
							"RGUDLDMG:RLG Dvsfd Med Grw",
							"RGUDLDRG:RLG Dvsfd Ret Grw",
							"RGUDLENG:RLG Entertain Grw",
							"RGUDLESG:RLG Edu Svc Grw",
							"RGUDLFUG:RLG FuPar&Cem Grw",
							"RGUDLHAG:RLG HholdAppl Grw",
							"RGUDLHBG:RLG HomeBldg Grw",
							"RGUDLHEG:RLG HHEq&Pro Grw",
							"RGUDLHFG:RLG Hhld Furn Grw",
							"RGUDLHOG:RLG Htel/Mtel Grw",
							"RGUDLLTG:RLG Leis Time Grw",
							"RGUDLLXG:RLG Lux Items Grw",
							"RGUDLMHG:RLG Mfg Hous Grw",
							"RGUDLPCG:RLG Prnt&CpySv Grw",
							"RGUDLPHG:RLG Photograph Grw",
							"RGUDLPUG:RLG Publishing Grw",
							"RGUDLRBG:RLG Radio&TVBr Grw",
							"RGUDLRCG:RLG R&LSv Con Grw",
							"RGUDLRTG:RLG Restaurant Grw",
							"RGUDLRVG:RLG RV & Boats Grw",
							"RGUDLSFG:RLG StorageFac Grw",
							"RGUDLSRG:RLG Spec Ret  Grw",
							"RGUDLTXG:RLG TxtAp&Shoe Grw",
							"RGUDLTYG:RLG Toys Grw",
							"RGUDLVCG:RLG Vend&Cat Grw",
							"RGUELAEG:RLG AlterEner Grw",
							"RGUELCOG:RLG Coal Grw",
							"RGUELEQG:RLG Energy Eq Grw",
							"RGUELGPG:RLG Gas Pipe Grw",
							"RGUELOCG:RLG Oil CrudPr Grw",
							"RGUELOFG:RLG OffshrDri Grw",
							"RGUELOIG:RLG Oil Integ Grw",
							"RGUELORG:RLG Oil Ref&Mkt GR",
							"RGUELOWG:RLG OilWlEq&Sv Grw",
							"RGUFLAMG:RLG AstMgt&Cs Grw",
							"RGUFLBKG:RLG Bnks Dvfd Grw",
							"RGUFLBSG:RLG BkSvThrfMt Grw",
							"RGUFLCLG:RLG Cons Lend Grw",
							"RGUFLDFG:RLG DvfFnlSvc Grw",
							"RGUFLDRG:RLG DvsfREstateActG",
							"RGUFLEDG:RLG EquityREIT DivG",
							"RGUFLEFG:RLG EquityREIT InfG",
							"RGUFLEHG:RLG EquityREIT HcaG",
							"RGUFLEIG:RLG EquityREIT IndG",
							"RGUFLELG:RLG EquityREIT L&RG",
							"RGUFLEOG:RLG EquityREIT OffG",
							"RGUFLERG:RLG EquityREIT ResG",
							"RGUFLESG:RLG EquityREIT StoG",
							"RGUFLFDG:RLG FinDat&Sy Grw",
							"RGUFLFMG:RLG ComFn&Mtg Grw",
							"RGUFLILG:RLG Ins Life Grw",
							"RGUFLIMG:RLG Ins MulLn Grw",
							"RGUFLIPG:RLG Ins ProCa Grw",
							"RGUFLMCG:RLG MortgREIT ComG",
							"RGUFLMDG:RLG MortgREIT DivG",
							"RGUFLMSG:RLG MortgREIT ResG",
							"RGUFLOSG:RLG EquityREIT OSpG",
							"RGUFLRHG:RLG RealEstate H&DG",
							"RGUFLRSG:RLG RealEstate SvcG",
							"RGUFLRTG:RLG EquityREIT RetG",
							"RGUFLSBG:RLG SecBrk&Svc Grw",
							"RGUFLTIG:RLG EquityREIT TimG",
							"RGUHLBTG:RLG Biotec Grw",
							"RGUHLHCG:RLG HlthC Misc Grw",
							"RGUHLHFG:RLG HC Fac Grw",
							"RGUHLHMG:RLG HC MgtSvc Grw",
							"RGUHLHSG:RLG HCare Svc Grw",
							"RGUHLMDG:RLG M&DIns&Sp Grw",
							"RGUHLMEG:RLG Med Eq Grw",
							"RGUHLMSG:RLG Med Svc Grw",
							"RGUHLPHG:RLG Phrm Grw",
							"RGUMLALG:RLG Aluminum Grw",
							"RGUMLBMG:RLG BldgMatl Grw",
							"RGUMLCCG:RLG Bld ClCtr Grw",
							"RGUMLCDG:RLG Chem  Dvfd Grw",
							"RGUMLCMG:RLG Cement Grw",
							"RGUMLCPG:RLG Cont&Pkg Grw",
							"RGUMLCRG:RLG Copper Grw",
							"RGUMLCSG:RLG Chem Spec Grw",
							"RGUMLDMG:RLG DvfMt&Prc Grw",
							"RGUMLFPG:RLG ForestPrd Grw",
							"RGUMLFTG:RLG Fertiliz Grw",
							"RGUMLGLG:RLG Glass Grw",
							"RGUMLGOG:RLG Gold Grw",
							"RGUMLMDG:RLG Met&MinDvf Grw",
							"RGUMLMFG:RLG MetFabric Grw",
							"RGUMLPAG:RLG Paper Grw",
							"RGUMLPCG:RLG Paint&Coat Grw",
							"RGUMLPLG:RLG Plastics Grw",
							"RGUMLPMG:RLG PrcMt&Min Grw",
							"RGUMLRWG:RLG RfWal&Plm Grw",
							"RGUMLSTG:RLG Steel Grw",
							"RGUMLSYG:RLG SynFib&Chm Grw",
							"RGUMLTPG:RLG Text Prod Grw",
							"RGUPLAIG:RLG Air Trans Grw",
							"RGUPLASG:RLG Aerospace Grw",
							"RGUPLBOG:RLG BOSupHR&Cons GR",
							"RGUPLCLG:RLG CommSvcR&L Grw",
							"RGUPLCNG:RLG Cons Grw",
							"RGUPLCVG:RLG CoVeh&Prt Grw",
							"RGUPLDOG:RLG DvfMfgOps Grw",
							"RGUPLECG:RLG Eng&CtrSv Grw",
							"RGUPLENG:RLG EnvMn&SecSvc GR",
							"RGUPLFBG:RLG FmBlkPrSv Grw",
							"RGUPLITG:RLG IntlTrd&DvLg GR",
							"RGUPLMAG:RLG Mach Ag Grw",
							"RGUPLMEG:RLG Mach Eng Grw",
							"RGUPLMHG:RLG Mch Cn&Hn Grw",
							"RGUPLMIG:RLG Mach Indu Grw",
							"RGUPLMSG:RLG Mach Spec Grw",
							"RGUPLMTG:RLG Mach Tool Grw",
							"RGUPLOEG:RLG OffSup&Eq Grw",
							"RGUPLPDG:RLG PrdDr Misc Grw",
							"RGUPLPTG:RLG PwrTranEq Grw",
							"RGUPLRAG:RLG Railroads Grw",
							"RGUPLRLG:RLG RailroadEq Grw",
							"RGUPLSEG:RLG SciIn Elc Grw",
							"RGUPLSGG:RLG ScInGg&Mt Grw",
							"RGUPLSHG:RLG Shipping Grw",
							"RGUPLSIG:RLG SciICt&Fl Grw",
							"RGUPLSPG:RLG SciInPCtrl Grw",
							"RGUPLTKG:RLG Truckers Grw",
							"RGUPLTMG:RLG Trans Misc Grw",
							"RGUSLAFG:RLG AgFsh&Ran Grw",
							"RGUSLBDG:RLG Bev Br&Ds Grw",
							"RGUSLDGG:RLG Drg&GrcCh Grw",
							"RGUSLFGG:RLG Fru&GrnPrc Grw",
							"RGUSLFOG:RLG Foods Grw",
							"RGUSLMCG:RLG MiscConsSt Grw",
							"RGUSLPCG:RLG Pers Care Grw",
							"RGUSLSDG:RLG Bev SftDr Grw",
							"RGUSLSUG:RLG Sugar Grw",
							"RGUSLTOG:RLG Tobacco Grw",
							"RGUTLCMG:RLG Comm Tech Grw",
							"RGUTLCSG:RLG CpSvSw&Sy Grw",
							"RGUTLCTG:RLG Comp Tech Grw",
							"RGUTLECG:RLG Elec Comp Grw",
							"RGUTLEEG:RLG ElecEnt Grw",
							"RGUTLELG:RLG Elec Grw",
							"RGUTLPRG:RLG ProdTechEq Grw",
							"RGUTLSCG:RLG SemiCn&Cp Grw",
							"RGUTLTEG:RLG Tele Eq Grw",
							"RGUTLTMG:RLG Tech Misc Grw",
							"RGUULUEG:RLG Util Elec Grw",
							"RGUULUGG:RLG Util GasDi Grw",
							"RGUULUMG:RLG Util Misc Grw",
							"RGUULUTG:RLG Util Tele Grw",
							"RGUULUWG:RLG Util Water Grw"
						});

						ok = true;
					}
				}

				if (groupSymbol == "RLV")   //Russell 1000 Value
				{
					if (subgroupSymbol == "RLVL1")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"RGUSDLV:RLV CON DISC VALUE",
							"RGUSSLV:RLV CONS STAPLES VALUE",
							"RGUSELV:RLV ENERGY VALUE",
							"RGUSFLV:RLV FIN SERVICES VALUE",
							"RGUSHLV:RLV HEALTH CARE VALUE",
							"RGUSMLV:RLV MATERIALS VALUE",
							"RGUSPLV:RLV PROD DURABLES VALUE",
							"RGUSTLV:RLV TECHNOLOGY VALUE",
							"RGUSULV:RLV UTILITIES VALUE"
						});

						ok = true;
					}

					else if (subgroupSymbol == "RLVL2")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"RGUDLAAV:RLV Ad Age Val",
							"RGUDLAPV:RLV AutoPrts Val",
							"RGUDLASV:RLV Auto Svc Val",
							"RGUDLAUV:RLV Automob Val",
							"RGUDLCEV:RLV ConsElect Val",
							"RGUDLCGV:RLV Cas&Gamb Val",
							"RGUDLCMV:RLV ConSvcMisc Val",
							"RGUDLCSV:RLV Cosmetics Val",
							"RGUDLCTV:RLV CabTV Svc Val",
							"RGUDLDMV:RLV Dvsfd Med Val",
							"RGUDLDRV:RLV Dvsfd Ret Val",
							"RGUDLENV:RLV Entertain Val",
							"RGUDLESV:RLV Edu Svc Val",
							"RGUDLFUV:RLV FuPar&Cem Val",
							"RGUDLHAV:RLV HholdAppl Val",
							"RGUDLHBV:RLV HomeBldg Val",
							"RGUDLHEV:RLV HHEq&Pro Val",
							"RGUDLHFV:RLV Hhld Furn Val",
							"RGUDLHOV:RLV Htel/Mtel Val",
							"RGUDLLTV:RLV Leis Time Val",
							"RGUDLLXV:RLV Lux Items Val",
							"RGUDLMHV:RLV Mfg Hous Val",
							"RGUDLPCV:RLV Prnt&CpySv Val",
							"RGUDLPHV:RLV Photograph Val",
							"RGUDLPUV:RLV Publishing Val",
							"RGUDLRBV:RLV Radio&TVBr Val",
							"RGUDLRCV:RLV R&LSv Con Val",
							"RGUDLRTV:RLV Restaurant Val",
							"RGUDLRVV:RLV RV & Boats Val",
							"RGUDLSFV:RLV StorageFac Val",
							"RGUDLSRV:RLV Spec Ret  Val",
							"RGUDLTXV:RLV TxtAp&Shoe Val",
							"RGUDLTYV:RLV Toys Val",
							"RGUDLVCV:RLV Vend&Cat Val",
							"RGUELAEV:RLV AlterEner Val",
							"RGUELCOV:RLV Coal Val",
							"RGUELEQV:RLV Energy Eq Val",
							"RGUELGPV:RLV Gas Pipe Val",
							"RGUELOCV:RLV Oil CrudPr Val",
							"RGUELOFV:RLV OffshrDri Val",
							"RGUELOIV:RLV Oil Integ Val",
							"RGUELORV:RLV Oil Ref&Mkt VA",
							"RGUELOWV:RLV OilWlEq&Sv Val",
							"RGUFLAMV:RLV AstMgt&Cs Val",
							"RGUFLBKV:RLV Bnks Dvfd Val",
							"RGUFLBSV:RLV BkSvThrfMt Val",
							"RGUFLCLV:RLV Cons Lend Val",
							"RGUFLDFV:RLV DvfFnlSvc Val",
							"RGUFLDRV:RLV DvsfREstateActV",
							"RGUFLEDV:RLV EquityREIT:DivV",
							"RGUFLEFV:RLV EquityREIT InfV",
							"RGUFLEHV:RLV EquityREIT HcaV",
							"RGUFLEIV:RLV EquityREIT IndV",
							"RGUFLELV:RLV EquityREIT L&RV",
							"RGUFLEOV:RLV EquityREIT OffV",
							"RGUFLERV:RLV EquityREIT ResV",
							"RGUFLESV:RLV EquityREIT StoV",
							"RGUFLFDV:RLV FinDat&Sy Val",
							"RGUFLFMV:RLV ComFn&Mtg Val",
							"RGUFLILV:RLV Ins Life Val",
							"RGUFLIMV:RLV Ins MulLn Val",
							"RGUFLIPV:RLV Ins ProCa Val",
							"RGUFLMCV:RLV MortgREIT ComV",
							"RGUFLMDV:RLV MortgREIT DivV",
							"RGUFLMSV:RLV MortgREIT ResV",
							"RGUFLOSV:RLV EquityREIT OSpV",
							"RGUFLRHV:RLV RealEstate H&DV",
							"RGUFLRSV:RLV RealEstate SvcV",
							"RGUFLRTV:RLV EquityREIT RetV",
							"RGUFLSBV:RLV SecBrk&Svc Val",
							"RGUFLTIV:RLV EquityREIT TimV",
							"RGUHLBTV:RLV Biotec Val",
							"RGUHLHCV:RLV HlthC Misc Val",
							"RGUHLHFV:RLV HC Fac Val",
							"RGUHLHMV:RLV HC MgtSvc Val",
							"RGUHLHSV:RLV HCare Svc Val",
							"RGUHLMDV:RLV M&DIns&Sp Val",
							"RGUHLMEV:RLV Med Eq Val",
							"RGUHLMSV:RLV Med Svc Val",
							"RGUHLPHV:RLV Phrm Val",
							"RGUMLALV:RLV Aluminum Val",
							"RGUMLBMV:RLV BldgMatl Val",
							"RGUMLCCV:RLV Bld ClCtr Val",
							"RGUMLCDV:RLV Chem Dvfd Val",
							"RGUMLCMV:RLV Cement Val",
							"RGUMLCPV:RLV Cont&Pkg Val",
							"RGUMLCRV:RLV Copper Val",
							"RGUMLCSV:RLV Chem Spec Val",
							"RGUMLDMV:RLV DvfMt&Prc Val",
							"RGUMLFPV:RLV ForestPrd Val",
							"RGUMLFTV:RLV Fertiliz Val",
							"RGUMLGLV:RLV Glass Val",
							"RGUMLGOV:RLV Gold Val",
							"RGUMLMDV:RLV Met&MinDvf Val",
							"RGUMLMFV:RLV MetFabric Val",
							"RGUMLPAV:RLV Paper Val",
							"RGUMLPCV:RLV Paint&Coat Val",
							"RGUMLPLV:RLV Plastics Val",
							"RGUMLPMV:RLV PrcMt&Min Val",
							"RGUMLRWV:RLV RfWal&Plm Val",
							"RGUMLSTV:RLV Steel Val",
							"RGUMLSYV:RLV SynFib&Chm Val",
							"RGUMLTPV:RLV Text Prod Val",
							"RGUPLAIV:RLV Air Trans Val",
							"RGUPLASV:RLV Aerospace Val",
							"RGUPLBOV:RLV BOSupHR&Cons VA",
							"RGUPLCLV:RLV CommSvcR&L Val",
							"RGUPLCNV:RLV Cons Val",
							"RGUPLCVV:RLV CoVeh&Prt Val",
							"RGUPLDOV:RLV DvfMfgOps Val",
							"RGUPLECV:RLV Eng&CtrSv Val",
							"RGUPLENV:RLV EnvMn&SecSvc VA",
							"RGUPLFBV:RLV FmBlkPrSv Val",
							"RGUPLITV:RLV IntlTrd&DvLg VA",
							"RGUPLMAV:RLV Mach Ag Val",
							"RGUPLMEV:RLV Mach Eng Val",
							"RGUPLMHV:RLV Mch Cn&Hn Val",
							"RGUPLMIV:RLV Mach Indu Val",
							"RGUPLMSV:RLV Mach Spec Val",
							"RGUPLMTV:RLV Mach Tool Val",
							"RGUPLOEV:RLV OffSup&Eq Val",
							"RGUPLPDV:RLV PrdDr Misc Val",
							"RGUPLPTV:RLV PwrTranEq Val",
							"RGUPLRAV:RLV Railroads Val",
							"RGUPLRLV:RLV RailroadEq Val",
							"RGUPLSEV:RLV SciIn Elc Val",
							"RGUPLSGV:RLV ScInGg&Mt Val",
							"RGUPLSHV:RLV Shipping Val",
							"RGUPLSIV:RLV SciICt&Fl Val",
							"RGUPLSPV:RLV SciInPCtrl Val",
							"RGUPLTKV:RLV Truckers Val",
							"RGUPLTMV:RLV Trans Misc Val",
							"RGUSLAFV:RLV AgFsh&Ran Val",
							"RGUSLBDV:RLV Bev Br&Ds Val",
							"RGUSLDGV:RLV Drg&GrcCh Val",
							"RGUSLFGV:RLV Fru&GrnPrc Val",
							"RGUSLFOV:RLV Foods Val",
							"RGUSLMCV:RLV MiscConsSt Val",
							"RGUSLPCV:RLV Pers Care Val",
							"RGUSLSDV:RLV Bev SftDr Val",
							"RGUSLSUV:RLV Sugar Val",
							"RGUSLTOV:RLV Tobacco Val",
							"RGUTLCMV:RLV Comm Tech Val",
							"RGUTLCSV:RLV CpSvSw&Sy Val",
							"RGUTLCTV:RLV Comp Tech Val",
							"RGUTLECV:RLV Elec Comp Val",
							"RGUTLEEV:RLV ElecEnt Val",
							"RGUTLELV:RLV Elec Val",
							"RGUTLPRV:RLV ProdTechEq Val",
							"RGUTLSCV:RLV SemiCn&Cp Val",
							"RGUTLTEV:RLV Tele Eq Val",
							"RGUTLTMV:RLV Tech Misc Val",
							"RGUULUEV:RLV Util Elec Val",
							"RGUULUGV:RLV Util GasDi Val",
							"RGUULUMV:RLV Util Misc Val",
							"RGUULUTV:RLV Util Tele Val",
							"RGUULUWV:RLV Util Water Val"
						});

						ok = true;
					}
				}

				if (groupSymbol == "RTY")  //Russell 2000
				{
					if (subgroupSymbol == "RTYL1")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"RGUSDS:RTY CONS DISC",
							"RGUSSS:RTY CONS STAPLES",
							"RGUSES:RTY ENERGY",
							"RGUSFS:RTY FIN SERVICES",
							"RGUSHS:RTY HEALTH CARE",
							"RGUSMS:RTY MATERIALS",
							"RGUSPS:RTY PROD DURABLES",
							"RGUSTS:RTY TECHNOLOGY",
							"RGUSUS:RTY UTILITIES"
						});

						ok = true;
					}

					else if (subgroupSymbol == "RTYL2")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"RGUSDSAA:RTY AD AGENCIES",
							"RGUSPSAS:RTY AEROSPACE",
							"RGUSSSAF:RTY AG FISH & RNCH",
							"RGUSPSAI:RTY AIR TRANSPORT",
							"RGUSESAE:RTY ALTER ENERGY",
							"RGUSMSAL:RTY ALUMINUM",
							"RGUSFSAM:RTY ASSET MGT & CUST",
							"RGUSDSAP:RTY AUTO PARTS",
							"RGUSDSAS:RTY AUTO SVC",
							"RGUSFSBK:RTY BANKS DVSFD",
							"RGUSSSBD:RTY BEV BRW & DSTLR",
							"RGUSSSSD:RTY BEV SOFT DRNK",
							"RGUSHSBT:RTY BIOTEC",
							"RGUSMSCC:RTY BLDG CLIMATE CTRL",
							"RGUSFSBS:RTY BNK SVG THRF MRT",
							"RGUSPSBO:RTY BO SUP HR & CONS",
							"RGUSMSBM:RTY BUILDING MATL",
							"RGUSDSCT:RTY CABLE TV SVC",
							"RGUSDSCG:RTY CASINOS & GAMB",
							"RGUSMSCM:RTY CEMENT",
							"RGUSMSCS:RTY CHEM SPEC",
							"RGUSMSCD:RTY CHEM DVFSD",
							"RGUSTSCS:RTY CMP SVC SFW & SYS",
							"RGUSESCO:RTY COAL",
							"RGUSFSFM:RTY COMM SVC",
							"RGUSPSCS:RTY COMM FIN & MORT",
							"RGUSPSCL:RTY COMM SVC RN",
							"RGUSTSCM:RTY COMM TECH",
							"RGUSPSCV:RTY COMM VEH & PRTS",
							"RGUSTSCT:RTY COMPUTER TECH",
							"RGUSPSCN:RTY CONS",
							"RGUSDSCM:RTY CONS SVC  MISC",
							"RGUSFSCL:RTY CONSUMER LEND",
							"RGUSDSCE:RTY CONSUMER ELECT",
							"RGUSMSCP:RTY CONTAINER & PKG",
							"RGUSMSCR:RTY COPPER",
							"RGUSDSCS:RTY COSMETICS",
							"RGUSSSDG:RTY DRUG & GROC CHN",
							"RGUSFSDF:RTY DVSFD FNCL SVC",
							"RGUSDSDM:RTY DVSFD MEDIA",
							"RGUSDSDR:RTY DVSFD RETAIL",
							"RGUSMSDM:RTY DVSFD MAT & PROC",
							"RGUSPSDO:RTY DVSFD MFG OPS",
							"RGUSDSES:RTY EDUCATION SVC",
							"RGUSTSEC:RTY ELECT COMP",
							"RGUSTSEE:RTY ELECT ENT",
							"RGUSTSEL:RTY ELECTRONICS",
							"RGUSESEQ:RTY ENERGY EQ",
							"RGUSPSEC:RTY ENG & CONTR SVC",
							"RGUSDSEN:RTY ENTERTAINMENT",
							"RGUSPSEN:RTY ENV MN & SEC SVC",
							"RGUSMSFT:RTY FERTILIZERS",
							"RGUSFSFD:RTY FINCL DATA & SYS",
							"RGUSSSFO:RTY FOODS",
							"RGUSMSFP:RTY FOREST PROD",
							"RGUSPSFB:RTY FRM & BLK PRNT SVC",
							"RGUSDSFU:RTY FUN PARLOR & CEM",
							"RGUSESGP:RTY GAS PIPELINE",
							"RGUSMSGL:RTY GLASS",
							"RGUSMSGO:RTY GOLD",
							"RGUSDSHE:RTY HHLD EQP & PROD",
							"RGUSDSHF:RTY HHLD FURN",
							"RGUSHSHF:RTY HLTH CARE FAC",
							"RGUSHSHS:RTY HLTH CARE SVC",
							"RGUSHSHM:RTY HLTH C MGT SVC",
							"RGUSHSHC:RTY HLTH C MISC",
							"RGUSDSHB:RTY HOMEBUILDING",
							"RGUSDSHO:RTY HOTEL/MOTEL",
							"RGUSDSHA:RTY HOUSEHOLD APPL",
							"RGUSFSIL:RTY INS LIFE",
							"RGUSFSIM:RTY INS MULTI-LN",
							"RGUSFSIP:RTY INS PROP-CAS",
							"RGUSPSIT:RTY INTL TRD & DV LG",
							"RGUSDSLT:RTY LEISURE TIME",
							"RGUSDSLX:RTY LUXURY ITEMS",
							"RGUSPSMI:RTY MACH INDU",
							"RGUSPSMA:RTY MACH AG",
							"RGUSPSME:RTY MACH ENGINES",
							"RGUSPSMS:RTY MACH SPECIAL",
							"RGUSPSMH:RTY MCH CONS & HNDL",
							"RGUSHSMD:RTY MD & DN INS & SUP",
							"RGUSHSME:RTY MED EQ",
							"RGUSHSMS:RTY MED SVC",
							"RGUSMSMD:RTY MET & MIN DVFSD",
							"RGUSMSMF:RTY METAL FABRIC",
							"RGUSDSMH:RTY MFG HOUSING",
							"RGUSSSMC:RTY MISC CONS STAPLE",
							"RGUSPSOE:RTY OFF SUP & EQ",
							"RGUSESOF:RTY OFFSHORE DRILL",
							"RGUSESOI:RTY OIL INTEGRATE",
							"RGUSESOC:RTY OIL CRUDE PROD",
							"RGUSESOR:RTY OIL REF & MKT",
							"RGUSESOW:RTY OIL WELL EQ & SVC",
							"RGUSMSPC:RTY PAINT & COATING",
							"RGUSMSPA:RTY PAPER",
							"RGUSSSPC:RTY PERSONAL CARE",
							"RGUSHSPH:RTY PHRM",
							"RGUSMSPL:RTY PLASTICS",
							"RGUSPSPT:RTY PREC MET & MINL",
							"RGUSMSPM:RTY PRINT & COPY SVC",
							"RGUSDSPC:RTY PROD DUR MISC",
							"RGUSPSPD:RTY PRODUCT TECH EQ",
							"RGUSTSPR:RTY PUBLISHING",
							"RGUSDSPU:RTY PWR TRANSM EQ",
							"RGUSDSRB:RTY RADIO & TV BROAD",
							"RGUSPSRL:RTY RAILROAD EQ",
							"RGUSPSRA:RTY RAILROADS",
							"RGUSFSRE:RTY REAL ESTATE",
							"RGUSFSRI:RTY REIT",
							"RGUSDSRT:RTY RESTAURANTS",
							"RGUSMSRW:RTY ROOF WALL & PLUM",
							"RGUSDSRC:RTY RT & LS SVC CONS",
							"RGUSDSRV:RTY RV & BOATS",
							"RGUSPSSI:RTY SCI INS CTL & FLT",
							"RGUSPSSP:RTY SCI INS POL CTRL",
							"RGUSPSSG:RTY SCI INST GG & MTR",
							"RGUSPSSE:RTY SCI INSTR ELEC",
							"RGUSFSSB:RTY SEC BRKG & SVC",
							"RGUSTSSC:RTY SEMI COND & COMP",
							"RGUSPSSH:RTY SHIPPING",
							"RGUSDSSR:RTY SPEC RET",
							"RGUSMSST:RTY STEEL",
							"RGUSMSSY:RTY SYN FIBR & CHEM",
							"RGUSTSTM:RTY TECHNO MISC",
							"RGUSTSTE:RTY TELE EQ",
							"RGUSDSTX:RTY TEXT APP & SHOES",
							"RGUSMSTP:RTY TEXTILE PROD",
							"RGUSSSTO:RTY TOBACCO",
							"RGUSDSTY:RTY TOYS",
							"RGUSPSTM:RTY TRANS MISC",
							"RGUSPSTK:RTY TRUCKERS",
							"RGUSUSUM:RTY UTIL  MISC",
							"RGUSUSUE:RTY UTIL ELEC",
							"RGUSUSUG:RTY UTIL GAS DIST",
							"RGUSUSUT:RTY UTIL TELE",
							"RGUSUSUW:RTY UTIL WATER"
						});

						ok = true;
					}
				}

				if (groupSymbol == "RUJ")  //Russell 2000
				{
					if (subgroupSymbol == "RUJL1")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"RGUSDSV:RUJ CONS DISC",
							"RGUSSSV:RUJ CONS STAPLES",
							"RGUSESV:RUJ ENERGY",
							"RGUSFSV:RUJ FIN SERVICES",
							"RGUSHSV:RUJ HEALTH CARE",
							"RGUSMSV:RUJ MATERIALS",
							"RGUSPSV:RUJ PROD DURABLES",
							"RGUSTSV:RUJ TECHNOLOGY",
							"RGUSUSV:RUJ UTILITIES"
						});

						ok = true;
					}

					else if (subgroupSymbol == "RUJL2")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"RGUDSAAV:RUJ Ad Age Val",
							"RGUDSAPV:RUJ AutoPrts Val",
							"RGUDSASV:RUJ Auto Svc Val",
							"RGUDSAUV:RUJ Automob Val",
							"RGUDSCEV:RUJ ConsElect Val",
							"RGUDSCGV:RUJ Cas&Gamb Val",
							"RGUDSCMV:RUJ ConSvcMisc Val",
							"RGUDSCSV:RUJ Cosmetics Val",
							"RGUDSCTV:RUJ CabTV Svc Val",
							"RGUDSDMV:RUJ Dvsfd Med Val",
							"RGUDSDRV:RUJ Dvsfd Ret Val",
							"RGUDSENV:RUJ Entertain Val",
							"RGUDSESV:RUJ Edu Svc Val",
							"RGUDSFUV:RUJ FuPar&Cem Val",
							"RGUDSHAV:RUJ HholdAppl Val",
							"RGUDSHBV:RUJ HomeBldg Val",
							"RGUDSHEV:RUJ HHEq&Pro Val",
							"RGUDSHFV:RUJ Hhld Furn Val",
							"RGUDSHOV:RUJ Htel/Mtel Val",
							"RGUDSLTV:RUJ Leis Time Val",
							"RGUDSLXV:RUJ Lux Items Val",
							"RGUDSMHV:RUJ Mfg Hous Val",
							"RGUDSPCV:RUJ Prnt&CpySv Val",
							"RGUDSPHV:RUJ Photograph Val",
							"RGUDSPUV:RUJ Publishing Val",
							"RGUDSRBV:RUJ Radio&TVBr Val",
							"RGUDSRCV:RUJ R&LSv Con Val",
							"RGUDSRTV:RUJ Restaurant Val",
							"RGUDSRVV:RUJ RV & Boats Val",
							"RGUDSSFV:RUJ StorageFac Val",
							"RGUDSSRV:RUJ Spec Ret  Val",
							"RGUDSTXV:RUJ TxtAp&Shoe Val",
							"RGUDSTYV:RUJ Toys Val",
							"RGUDSVCV:RUJ Vend&Cat Val",
							"RGUESAEV:RUJ AlterEner Val",
							"RGUESCOV:RUJ Coal Val",
							"RGUESEQV:RUJ Energy Eq Val",
							"RGUESGPV:RUJ Gas Pipe Val",
							"RGUESOCV:RUJ Oil CrudPr Val",
							"RGUESOFV:RUJ OffshrDri Val",
							"RGUESOIV:RUJ Oil Integ Val",
							"RGUESORV:RUJ Oil Ref&Mkt VA",
							"RGUESOWV:RUJ OilWlEq&Sv Val",
							"RGUFSAMV:RUJ AstMgt&Cs Val",
							"RGUFSBKV:RUJ Bnks:Dvfd Val",
							"RGUFSBSV:RUJ BkSvThrfMt Val",
							"RGUFSCLV:RUJ Cons Lend Val",
							"RGUFSDFV:RUJ DvfFnlSvc Val",
							"RGUFSDRV:RUJ DvsfREstateActV",
							"RGUFSEDV:RUJ EquityREIT DivV",
							"RGUFSEFV:RUJ EquityREIT InfV",
							"RGUFSEHV:RUJ EquityREIT HcaV",
							"RGUFSEIV:RUJ EquityREIT IndV",
							"RGUFSELV:RUJ EquityREIT L&RV",
							"RGUFSEOV:RUJ EquityREIT OffV",
							"RGUFSERV:RUJ EquityREIT ResV",
							"RGUFSESV:RUJ EquityREIT StoV",
							"RGUFSFDV:RUJ FinDat&Sy Val",
							"RGUFSFMV:RUJ ComFn&Mtg Val",
							"RGUFSILV:RUJ Ins Life Val",
							"RGUFSIMV:RUJ Ins  MulLn Val",
							"RGUFSIPV:RUJ Ins ProCa Val",
							"RGUFSMCV:RUJ MortgREIT ComV",
							"RGUFSMDV:RUJ MortgREIT DivV",
							"RGUFSMSV:RUJ MortgREIT ResV",
							"RGUFSOSV:RUJ EquityREIT OSpV",
							"RGUFSRHV:RUJ RealEstate H&DV",
							"RGUFSRSV:RUJ RealEstate SvcV",
							"RGUFSRTV:RUJ EquityREIT RetV",
							"RGUFSSBV:RUJ SecBrk&Svc Val",
							"RGUFSTIV:RUJ EquityREIT TimV",
							"RGUHSBTV:RUJ Biotec Val",
							"RGUHSHCV:RUJ HlthC Misc Val",
							"RGUHSHFV:RUJ HC Fac Val",
							"RGUHSHMV:RUJ HC MgtSvc Val",
							"RGUHSHSV:RUJ HCare Svc Val",
							"RGUHSMDV:RUJ M&DIns&Sp Val",
							"RGUHSMEV:RUJ Med Eq Val",
							"RGUHSMSV:RUJ Med Svc Val",
							"RGUHSPHV:RUJ Phrm Val",
							"RGUMSALV:RUJ Aluminum Val",
							"RGUMSBMV:RUJ BldgMatl Val",
							"RGUMSCCV:RUJ Bld ClCtr Val",
							"RGUMSCDV:RUJ Chem Dvfd Val",
							"RGUMSCMV:RUJ Cement Val",
							"RGUMSCPV:RUJ Cont&Pkg Val",
							"RGUMSCRV:RUJ Copper Val",
							"RGUMSCSV:RUJ Chem Spec Val",
							"RGUMSDMV:RUJ DvfMt&Prc Val",
							"RGUMSFPV:RUJ ForestPrd Val",
							"RGUMSFTV:RUJ Fertiliz Val",
							"RGUMSGLV:RUJ Glass Val",
							"RGUMSGOV:RUJ Gold Val",
							"RGUMSMDV:RUJ Met&MinDvf Val",
							"RGUMSMFV:RUJ MetFabric Val",
							"RGUMSPAV:RUJ Paper Val",
							"RGUMSPCV:RUJ Paint&Coat Val",
							"RGUMSPLV:RUJ Plastics Val",
							"RGUMSPMV:RUJ PrcMt&Min Val",
							"RGUMSRWV:RUJ RfWal&Plm Val",
							"RGUMSSTV:RUJ Steel Val",
							"RGUMSSYV:RUJ SynFib&Chm Val",
							"RGUMSTPV:RUJ Text Prod Val",
							"RGUPSAIV:RUJ Air Trans Val",
							"RGUPSASV:RUJ Aerospace Val",
							"RGUPSBOV:RUJ BOSupHR&Cons VA",
							"RGUPSCLV:RUJ CommSvcR&L Val",
							"RGUPSCNV:RUJ Cons Val",
							"RGUPSCVV:RUJ CoVeh&Prt Val",
							"RGUPSDOV:RUJ DvfMfgOps Val",
							"RGUPSECV:RUJ Eng&CtrSv Val",
							"RGUPSENV:RUJ EnvMn&SecSvc VA",
							"RGUPSFBV:RUJ FmBlkPrSv Val",
							"RGUPSITV:RUJ IntlTrd&DvLg VA",
							"RGUPSMAV:RUJ Mach Ag Val",
							"RGUPSMEV:RUJ Mach Eng Val",
							"RGUPSMHV:RUJ Mch Cn&Hn Val",
							"RGUPSMIV:RUJ Mach Indu Val",
							"RGUPSMSV:RUJ Mach Spec Val",
							"RGUPSMTV:RUJ Mach Tool Val",
							"RGUPSOEV:RUJ OffSup&Eq Val",
							"RGUPSPDV:RUJ PrdDr Misc Val",
							"RGUPSPTV:RUJ PwrTranEq Val",
							"RGUPSRAV:RUJ Railroads Val",
							"RGUPSRLV:RUJ RailroadEq Val",
							"RGUPSSEV:RUJ SciIn:Elc Val",
							"RGUPSSGV:RUJ ScInGg&Mt Val",
							"RGUPSSHV:RUJ Shipping Val",
							"RGUPSSIV:RUJ SciICt&Fl Val",
							"RGUPSSPV:RUJ SciInPCtrl Val",
							"RGUPSTKV:RUJ Truckers Val",
							"RGUPSTMV:RUJ Trans Misc Val",
							"RGUSSAFV:RUJ AgFsh&Ran Val",
							"RGUSSBDV:RUJ Bev Br&Ds Val",
							"RGUSSDGV:RUJ Drg&GrcCh Val",
							"RGUSSFGV:RUJ Fru&GrnPrc Val",
							"RGUSSFOV:RUJ Foods Val",
							"RGUSSMCV:RUJ MiscConsSt Val",
							"RGUSSPCV:RUJ Pers Care Val",
							"RGUSSSDV:RUJ Bev SftDr Val",
							"RGUSSSUV:RUJ Sugar Val",
							"RGUSSTOV:RUJ Tobacco Val",
							"RGUTSCMV:RUJ Comm Tech Val",
							"RGUTSCSV:RUJ CpSvSw&Sy Val",
							"RGUTSCTV:RUJ Comp Tech Val",
							"RGUTSECV:RUJ Elec Comp Val",
							"RGUTSEEV:RUJ ElecEnt Val",
							"RGUTSELV:RUJ Elec Val",
							"RGUTSPRV:RUJ ProdTechEq Val",
							"RGUTSSCV:RUJ SemiCn&Cp Val",
							"RGUTSTEV:RUJ Tele Eq Val",
							"RGUTSTMV:RUJ Tech Misc Val",
							"RGUUSUEV:RUJ Util Elec Val",
							"RGUUSUGV:RUJ Util GasDi Val",
							"RGUUSUMV:RUJ Util  Misc Val",
							"RGUUSUTV:RUJ Util Tele Val",
							"RGUUSUWV:RUJ Util Water Val"
						});

						ok = true;
					}
				}

				if (groupSymbol == "RUO")  //Russell 2000
				{
					if (subgroupSymbol == "RUOL1")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"RGUSDSG:RUO CONS DISC",
							"RGUSSSG:RUO CONS STAPLES",
							"RGUSESG:RUO ENERGY",
							"RGUSFSG:RUO FIN SERVICES",
							"RGUSHSG:RUO HEALTH CARE",
							"RGUSMSG:RUO MATERIALS",
							"RGUSPSG:RUO PROD DURABLES",
							"RGUSTSG:RUO TECHNOLOGY",
							"RGUSUSG:RUO UTILITIES"
						});

						ok = true;
					}

					else if (subgroupSymbol == "RUOL2")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"RGUDSAAG:RUO Ad Age Grw",
							"RGUDSAPG:RUO AutoPrts Grw",
							"RGUDSASG:RUO Auto Svc Grw",
							"RGUDSAUG:RUO Automob Grw",
							"RGUDSCEG:RUO ConsElect Grw",
							"RGUDSCGG:RUO Cas&Gamb Grw",
							"RGUDSCMG:RUO ConSvcMisc Grw",
							"RGUDSCSG:RUO Cosmetics Grw",
							"RGUDSCTG:RUO CabTV Svc Grw",
							"RGUDSDMG:RUO Dvsfd Med Grw",
							"RGUDSDRG:RUO Dvsfd Ret Grw",
							"RGUDSENG:RUO Entertain Grw",
							"RGUDSESG:RUO Edu Svc Grw",
							"RGUDSFUG:RUO FuPar&Cem Grw",
							"RGUDSHAG:RUO HholdAppl Grw",
							"RGUDSHBG:RUO HomeBldg Grw",
							"RGUDSHEG:RUO HHEq&Pro Grw",
							"RGUDSHFG:RUO Hhld Furn Grw",
							"RGUDSHOG:RUO Htel/Mtel Grw",
							"RGUDSLTG:RUO Leis Time Grw",
							"RGUDSLXG:RUO Lux Items Grw",
							"RGUDSMHG:RUO Mfg Hous Grw",
							"RGUDSPCG:RUO Prnt&CpySv Grw",
							"RGUDSPHG:RUO Photograph Grw",
							"RGUDSPUG:RUO Publishing Grw",
							"RGUDSRBG:RUO Radio&TVBr Grw",
							"RGUDSRCG:RUO R&LSv Con Grw",
							"RGUDSRTG:RUO Restaurant Grw",
							"RGUDSRVG:RUO RV & Boats Grw",
							"RGUDSSFG:RUO StorageFac Grw",
							"RGUDSSRG:RUO Spec Ret  Grw",
							"RGUDSTXG:RUO TxtAp&Shoe Grw",
							"RGUDSTYG:RUO Toys Grw",
							"RGUDSVCG:RUO Vend&Cat Grw",
							"RGUESAEG:RUO AlterEner Grw",
							"RGUESCOG:RUO Coal Grw",
							"RGUESEQG:RUO Energy Eq Grw",
							"RGUESGPG:RUO Gas Pipe Grw",
							"RGUESOCG:RUO Oil CrudPr Grw",
							"RGUESOFG:RUO OffshrDri Grw",
							"RGUESOIG:RUO Oil Integ Grw",
							"RGUESORG:RUO Oil Ref&Mkt GR",
							"RGUESOWG:RUO OilWlEq&Sv Grw",
							"RGUFSAMG:RUO AstMgt&Cs Grw",
							"RGUFSBKG:RUO Bnks:Dvfd Grw",
							"RGUFSBSG:RUO BkSvThrfMt Grw",
							"RGUFSCLG:RUO Cons Lend Grw",
							"RGUFSDFG:RUO DvfFnlSvc Grw",
							"RGUFSDRG:RUO DvsfREstateActG",
							"RGUFSEDG:RUO EquityREIT DivG",
							"RGUFSEFG:RUO EquityREIT InfG",
							"RGUFSEHG:RUO EquityREIT HcaG",
							"RGUFSEIG:RUO EquityREIT IndG",
							"RGUFSELG:RUO EquityREIT L&RG",
							"RGUFSEOG:RUO EquityREIT OffG",
							"RGUFSERG:RUO EquityREIT ResG",
							"RGUFSESG:RUO EquityREIT StoG",
							"RGUFSFDG:RUO FinDat&Sy Grw",
							"RGUFSFMG:RUO ComFn&Mtg Grw",
							"RGUFSILG:RUO Ins:Life Grw",
							"RGUFSIMG:RUO Ins:MulLn Grw",
							"RGUFSIPG:RUO Ins:ProCa Grw",
							"RGUFSMCG:RUO MortgREIT ComG",
							"RGUFSMDG:RUO MortgREIT DivG",
							"RGUFSMSG:RUO MortgREIT ResG",
							"RGUFSOSG:RUO EquityREIT OSpG",
							"RGUFSRHG:RUO RealEstate H&DG",
							"RGUFSRSG:RUO RealEstate SvcG",
							"RGUFSRTG:RUO EquityREIT RetG",
							"RGUFSSBG:RUO SecBrk&Svc Grw",
							"RGUFSTIG:RUO EquityREIT TimG",
							"RGUHSBTG:RUO Biotec Grw",
							"RGUHSHCG:RUO HlthC Misc Grw",
							"RGUHSHFG:RUO HC Fac Grw",
							"RGUHSHMG:RUO HC MgtSvc Grw",
							"RGUHSHSG:RUO HCare Svc Grw",
							"RGUHSMDG:RUO M&DIns&Sp Grw",
							"RGUHSMEG:RUO Med Eq Grw",
							"RGUHSMSG:RUO Med Svc Grw",
							"RGUHSPHG:RUO Phrm Grw",
							"RGUMSALG:RUO Aluminum Grw",
							"RGUMSBMG:RUO BldgMatl Grw",
							"RGUMSCCG:RUO Bld ClCtr Grw",
							"RGUMSCDG:RUO Chem Dvfd Grw",
							"RGUMSCMG:RUO Cement Grw",
							"RGUMSCPG:RUO Cont&Pkg Grw",
							"RGUMSCRG:RUO Copper Grw",
							"RGUMSCSG:RUO Chem Spec Grw",
							"RGUMSDMG:RUO DvfMt&Prc Grw",
							"RGUMSFPG:RUO ForestPrd Grw",
							"RGUMSFTG:RUO Fertiliz Grw",
							"RGUMSGLG:RUO Glass Grw",
							"RGUMSGOG:RUO Gold Grw",
							"RGUMSMDG:RUO Met&MinDvf Grw",
							"RGUMSMFG:RUO MetFabric Grw",
							"RGUMSPAG:RUO Paper Grw",
							"RGUMSPCG:RUO Paint&Coat Grw",
							"RGUMSPLG:RUO Plastics Grw",
							"RGUMSPMG:RUO PrcMt&Min Grw",
							"RGUMSRWG:RUO RfWal&Plm Grw",
							"RGUMSSTG:RUO Steel Grw",
							"RGUMSSYG:RUO SynFib&Chm Grw",
							"RGUMSTPG:RUO Text Prod Grw",
							"RGUPSAIG:RUO Air Trans Grw",
							"RGUPSASG:RUO Aerospace Grw",
							"RGUPSBOG:RUO BOSupHR&Cons GR",
							"RGUPSCLG:RUO CommSvcR&L Grw",
							"RGUPSCNG:RUO Cons Grw",
							"RGUPSCVG:RUO CoVeh&Prt Grw",
							"RGUPSDOG:RUO DvfMfgOps Grw",
							"RGUPSECG:RUO Eng&CtrSv Grw",
							"RGUPSENG:RUO EnvMn&SecSvc GR",
							"RGUPSFBG:RUO FmBlkPrSv Grw",
							"RGUPSITG:RUO IntlTrd&DvLg GR",
							"RGUPSMAG:RUO Mach Ag Grw",
							"RGUPSMEG:RUO Mach Eng Grw",
							"RGUPSMHG:RUO Mch Cn&Hn Grw",
							"RGUPSMIG:RUO Mach Indu Grw",
							"RGUPSMSG:RUO Mach Spec Grw",
							"RGUPSMTG:RUO Mach Tool Grw",
							"RGUPSOEG:RUO OffSup&Eq Grw",
							"RGUPSPDG:RUO PrdDr Misc Grw",
							"RGUPSPTG:RUO PwrTranEq Grw",
							"RGUPSRAG:RUO Railroads Grw",
							"RGUPSRLG:RUO RailroadEq Grw",
							"RGUPSSEG:RUO SciIn Elc Grw",
							"RGUPSSGG:RUO ScInGg&Mt Grw",
							"RGUPSSHG:RUO Shipping Grw",
							"RGUPSSIG:RUO SciICt&Fl Grw",
							"RGUPSSPG:RUO SciInPCtrl Grw",
							"RGUPSTKG:RUO Truckers Grw",
							"RGUPSTMG:RUO Trans Misc Grw",
							"RGUSSAFG:RUO AgFsh&Ran Grw",
							"RGUSSBDG:RUO Bev Br&Ds Grw",
							"RGUSSDGG:RUO Drg&GrcCh Grw",
							"RGUSSFGG:RUO Fru&GrnPrc Grw",
							"RGUSSFOG:RUO Foods Grw",
							"RGUSSGSD:RUO Bev SftDr Grw",
							"RGUSSMCG:RUO MiscConsSt Grw",
							"RGUSSPCG:RUO Pers Care Grw",
							"RGUSSSUG:RUO Sugar Grw",
							"RGUSSTOG:RUO Tobacco Grw",
							"RGUTSCMG:RUO Comm Tech Grw",
							"RGUTSCSG:RUO CpSvSw&Sy Grw",
							"RGUTSCTG:RUO Comp Tech Grw",
							"RGUTSECG:RUO Elec Comp Grw",
							"RGUTSEEG:RUO ElecEnt Grw",
							"RGUTSELG:RUO Elec Grw",
							"RGUTSPRG:RUO ProdTechEq Grw",
							"RGUTSSCG:RUO SemiCn&Cp Grw",
							"RGUTSTEG:RUO Tele Eq Grw",
							"RGUTSTMG:RUO Tech Misc Grw",
							"RGUUSUEG:RUO Util Elec Grw",
							"RGUUSUGG:RUO Util GasDi Grw",
							"RGUUSUMG:RUO Util  Misc Grw",
							"RGUUSUTG:RUO Util Tele Grw",
							"RGUUSUWG:RUO Util Water Grw"
						});

						ok = true;
					}
				}

				if (groupSymbol == "RAG")  //Russell 2000 Growth
				{
					if (subgroupSymbol == "RAGL1")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"RGUSDG:RAG CONS DISC",
							"RGUSSG:RAG CONS STAPLES",
							"RGUSEG:RAG ENERGY",
							"RGUSFG:RAG FIN SERVICES",
							"RGUSHG:RAG HEALTH CARE",
							"RGUSMG:RAG MATERIALS",
							"RGUSPG:RAG PROD DURABLES",
							"RGUSTG:RAG TECHNOLOGY",
							"RGUSUG:RAG UTILITIES"
						});

						ok = true;
					}
				}
				if (groupSymbol == "RAV")  //Russell 2000 Value
				{
					if (subgroupSymbol == "RAVL1")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"RGUSDV:RAV CONS DISC",
							"RGUSSV:RAV CONS STAPLES",
							"RGUSEV:RAV ENERGY",
							"RGUSFV:RAV FIN SERVICES",
							"RGUSHV:RAV HEALTH CARE",
							"RGUSMV:RAV MATERIALS",
							"RGUSPV:RAV PROD DURABLES",
							"RGUSTV:RAV TECHNOLOGY",
							"RGUSUV:RAV UTILITIES"
						});

						ok = true;
					}
				}
				if (groupSymbol == "RMC")  //Russell Mid Cap Growth
				{
					if (subgroupSymbol == "RMCL1")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"RGUMCD:RMC CONS DISC",
							"RGUMCS:RMC CONS STAPLES",
							"RGUMCE:RMC ENERGY",
							"RGUMCF:RMC FIN SERVICES",
							"RGUMCH:RMC HEALTH CARE",
							"RGUMCM:RMC MATERIALS",
							"RGUMCP:RMC PROD DURABLES",
							"RGUMCT:RMC TECHNOLOGY",
							"RGUMCU:RMC UTILITIES"
						});

						ok = true;
					}
				}

				if (groupSymbol == "RDG")  //Russell Mid Cap Growth
				{
					if (subgroupSymbol == "RDGL1")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"RGUMCDG:RDG CONS DISC",
							"RGUMCSG:RDG CONS STAPLES",
							"RGUMCEG:RDG ENERGY",
							"RGUMCFG:RDG FIN SERVICES",
							"RGUMCHG:RDG HEALTH CARE",
							"RGUMCMG:RDG MATERIALS",
							"RGUMCPG:RDG PROD DURABLES",
							"RGUMCTG:RDG TECHNOLOGY",
							"RGUMCUG:RDG UTILITIES"
						});

						ok = true;
					}
				}
				if (groupSymbol == "RMV")  //Russell Mid Cap Value
				{
					if (subgroupSymbol == "RMVL1")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"RGUMCDV:RMV CONS DISC",
							"RGUMCSV:RMV CONS STAPLES",
							"RGUMCEV:RMV ENERGY",
							"RGUMCFV:RMV FIN SERVICES",
							"RGUMCHV:RMV HEALTH CARE",
							"RGUMCMV:RMV MATERIALS",
							"RGUMCPV:RMV PROD DURABLES",
							"RGUMCTV:RMV TECHNOLOGY",
							"RGUMCUV:RMV UTILITIES"
						});

						ok = true;
					}
				}
				if (groupSymbol == "RMICRO")  //Russell MicroCap 
				{
					if (subgroupSymbol == "RMICROL1")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"RGMICD:RMICRO CONS DISC",
							"RGMICS:RMICRO CONS STAPLES",
							"RGMICE:RMICRO ENERGY",
							"RGMICF:RMICRO FIN SERVICES",
							"RGMICH:RMICRO HEALTH CARE",
							"RGMICM:RMICRO MATERIALS",
							"RGMICP:RMICRO PROD DURABLES",
							"RGMICT:RMICRO TECHNOLOGY",
							"RGMICU:RMICRO UTILITIES"
						});

						ok = true;
					}
				}

				if (groupSymbol == "RMICROG")  //Russell MicroCap Growth
				{
					if (subgroupSymbol == "RMICROGL1")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"RGMICDG:RMICROG CONS DISC",
							"RGMICSG:RMICROG CONS STAPLES",
							"RGMICEG:RMICROG ENERGY",
							"RGMICFG:RMICROG FIN SERVICES",
							"RGMICHG:RMICROG HEALTH CARE",
							"RGMICMG:RMICROG MATERIALS",
							"RGMICPG:RMICROG PROD DURABLES",
							"RGMICTG:RMICROG TECHNOLOGY",
							"RGMICUG:RMICROG UTILITIES"
						});

						ok = true;
					}
				}
				if (groupSymbol == "RMICROV")  //Russell MicroCap Value
				{
					if (subgroupSymbol == "RMICROVL1")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"RGMICDV:RMICROV CONS DISC",
							"RGMICSV:RMICROV CONS STAPLES",
							"RGMICEV:RMICROV ENERGY",
							"RGMICFV:RMICROV FIN SERVICES",
							"RGMICHV:RMICROV HEALTH CARE",
							"RGMICMV:RMICROV MATERIALS",
							"RGMICPV:RMICROV PROD DURABLES",
							"RGMICTV:RMICROV TECHNOLOGY",
							"RGMICUV:RMICROV UTILITIES"
						});

						ok = true;
					}
				}
				if (groupSymbol == "R2500")  //Russell 2500 
				{
					if (subgroupSymbol == "R2500L1")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"RGU25D:R2500 CONS DISC",
							"RGU25S:R2500 CONS STAPLES",
							"RGU25E:R2500 ENERGY",
							"RGU25F:R2500 FIN SERVICES",
							"RGU25H:R2500 HEALTH CARE",
							"RGU25M:R2500 MATERIALS",
							"RGU25P:R2500 PROD DURABLES",
							"RGU25T:R2500 TECHNOLOGY",
							"RGU25U:R2500 UTILITIES"
						});

						ok = true;
					}
				}

				if (groupSymbol == "R2500G")  //Russell 2500 Growth
				{
					if (subgroupSymbol == "R2500GL1")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"RGU25DG:R2500G CONS DISC",
							"RGU25SG:R2500G CONS STAPLES",
							"RGU25EG:R2500G ENERGY",
							"RGU25FG:R2500G FIN SERVICES",
							"RGU25HG:R2500G HEALTH CARE",
							"RGU25MG:R2500G MATERIALS",
							"RGU25PG:R2500G PROD DURABLES",
							"RGU25TG:R2500G TECHNOLOGY",
							"RGU25UG:R2500G UTILITIES"
						});

						ok = true;
					}
				}
				if (groupSymbol == "R2500V")  //Russell 2500 Value
				{
					if (subgroupSymbol == "R2500VL1")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"RGU25DV:R2500V CONS DISC",
							"RGU25SV:R2500V CONS STAPLES",
							"RGU25EV:R2500V ENERGY",
							"RGU25FV:R2500V FIN SERVICES",
							"RGU25HV:R2500V HEALTH CARE",
							"RGU25MV:R2500V MATERIALS",
							"RGU25PV:R2500V PROD DURABLES",
							"RGU25TV:R2500V TECHNOLOGY",
							"RGU25UV:R2500V UTILITIES"
						});

						ok = true;
					}
				}

				if (groupSymbol == "RAY") //Russell 3000
				{
					if (subgroupSymbol == "RAYL1")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"RGUSD:RAY CONS DISC",
							"RGUSS:RAY CINS STAPLES",
							"RGUSE:RAY ENERGY",
							"RGUSF:RAY FIN SERVICES",
							"RGUSH:RAY HEALTH CARE",
							"RGUSM:RAY MATERIALS",
							"RGUSP:RAY PROD DURABLES",
							"RGUST:RAY TECHNOLOGY",
							"RGUSU:RAY UTILITIES"
						});

						ok = true;
					}

					else if (subgroupSymbol == "RAYL2")
					{
						setNavigation(panel, mouseDownEvent, new string[]
						{
							"RGUSDAA:RAY AD AGENCIES",
							"RGUSPAS:RAY AEROSPACE",
							"RGUSSAF:RAY AG FISH & RNCH",
							"RGUSPAI:RAY AIR TRANSPORT",
							"RGUSEAE:RAY ALTER ENERGY",
							"RGUSMAL:RAY ALUMINUM",
							"RGUSFAM:RAY ASSET MGT & CUST",
							"RGUSDAP:RAY AUTO PARTS",
							"RGUSDAS:RAY AUTO SVC",
							"RGUSDAU:RAY AUTOMOBILES",
							"RGUSFBK:RAY BANKS DVSFD",
							"RGUSFBS:RAY BEV BRW & DSTLR",
							"RGUSSBD:RAY BEV SOFT DRNK",
							"RGUSSSD:RAY SOFT DRNK",
							"RGUSHBT:RAY BIOTEC",
							"RGUSMCC:RAY BLDG CLIMATE CTRL",
							"RGUSPBO:RAY BO SUP HR & CONS",
							"RGUSMBM:RAY BUILDING MATL",
							"RGUSDCT:RAY CABLE TV SVC",
							"RGUSDCG:RAY CASINOS & GAMB",
							"RGUSMCM:RAY CEMENT",
							"RGUSMCS:RAY CHEM SPEC",
							"RGUSMCD:RAY CHEM DVFSD",
							"RGUSECO:RAY CMP SVC SFW & SYS",
							"RGUSFFM:RAY COAL",
							"RGUSPCS:RAY COMM SVC",
							"RGUSPCL:RAY COMM FIN & MORT",
							"RGUSTCM:RAY COMM SVC RN",
							"RGUSPCV:RAY COMM TECH",
							"RGUSTCS:RAY COMM VEH & PRTS",
							"RGUSTCT:RAY COMPUTER TECH",
							"RGUSPCN:RAY CONS",
							"RGUSDCM:RAY CONS SVC  MISC",
							"RGUSDCE:RAY CONSUMER LEND",
							"RGUSFCL:RAY CONSUMER ELECT",
							"RGUSMCP:RAY CONTAINER & PKG",
							"RGUSMCR:RAY COPPER",
							"RGUSDCS:RAY COSMETICS",
							"RGUSDDM:RAY DRUG & GROC CHN",
							"RGUSSDG:RAY DVSFD FNCL SVC",
							"RGUSFDF:RAY DVSFD MEDIA",
							"RGUSDDR:RAY DVSFD RETAIL",
							"RGUSMDM:RAY DVSFD MAT & PROC",
							"RGUSPDO:RAY DVSFD MFG OPS",
							"RGUSDES:RAY EDUCATION SVC",
							"RGUSTEE:RAY ELECT COMP",
							"RGUSTEC:RAY ELECT ENT",
							"RGUSTEL:RAY ELECTRONICS",
							"RGUSEEQ:RAY ENERGY EQ",
							"RGUSPEC:RAY ENG & CONTR SVC",
							"RGUSDEN:RAY ENTERTAINMENT",
							"RGUSPEN:RAY ENV MN & SEC SVC",
							"RGUSMFT:RAY FERTILIZERS",
							"RGUSFFD:RAY FINCL DATA & SYS",
							"RGUSSFO:RAY FOODS",
							"RGUSMFP:RAY FOREST PROD",
							"RGUSPFB:RAY FRM & BLK PRNT SVC",
							"RGUSSFG:RAY FRUIT & GRN PROC",
							"RGUSDFU:RAY FUN PARLOR & CEM",
							"RGUSEGP:RAY GAS PIPELINE",
							"RGUSMGL:RAY GLASS",
							"RGUSMGO:RAY GOLD",
							"RGUSDHE:RAY HHLD EQP & PROD",
							"RGUSDHF:RAY HHLD FURN",
							"RGUSHHF:RAY HLTH CARE FAC",
							"RGUSHHM:RAY HLTH CARE SVC",
							"RGUSHHS:RAY HLTH C MGT SVC",
							"RGUSHHC:RAY HLTH C MISC",
							"RGUSDHB:RAY HOME BUILDING",
							"RGUSDHO:RAY HOTEL/MOTEL",
							"RGUSDHA:RAY HOUSEHOLD APPL",
							"RGUSFIL:RAY INS LIFE",
							"RGUSFIM:RAY INS MULTI-LN",
							"RGUSFIP:RAY INS PROP-CAS",
							"RGUSPIT:RAY INTL TRD & DV LG",
							"RGUSDLT:RAY LEISURE TIME",
							"RGUSPMG:RAY MACH & ENG",
							"RGUSPMI:RAY MACH IND",
							"RGUSPMT:RAY MACH TOOLS",
							"RGUSPMA:RAY MACH AG",
							"RGUSPME:RAY MACH SPECIAL",
							"RGUSPMS:RAY MCH CONS & HNDL",
							"RGUSHME:RAY MD & DN INS & SUP",
							"RGUSHMS:RAY MED EQ",
							"RGUSHMD:RAY MED SVC",
							"RGUSMMD:RAY MET & MIN DVFSD",
							"RGUSMMF:RAY METAL FABRIC",
							"RGUSDMH:RAY MFG HOUSING",
							"RGUSSMC:RAY MISC CONS STAPL",
							"RGUSPOE:RAY OFF SUP & EQ",
							"RGUSEOF:RAY OFFSHORE DRILL",
							"RGUSEOI:RAY OIL INTERGATE",
							"RGUSEOW:RAY OIL CRUDE PROD",
							"RGUSEOC:RAY OIL REF & MKT",
							"RGUSEOR:RAY OIL WELL EQ & SVC",
							"RGUSMPC:RAY PAINT & COATING",
							"RGUSMPA:RAY PAPER",
							"RGUSSPC:RAY PERSONAL CARE",
							"RGUSDPH:RAY PHOTOGRAPHY",
							"RGUSHPH:RAY PHRM",
							"RGUSMPL:RAY PLASTICS",
							"RGUSPOPT:RAY PREC MET & MINL",
							"RGUSMPM:RAY PRINT & COPY SVC",
							"RGUSDPC:RAY PROD DUR MISC",
							"RGUSPPD:RAY PRODUCT TECH EQ",
							"RGUSTPR:RAY PUBLISHING",
							"RGUSDPU:RAY PWR TRANSM EQ",
							"RGUSDRB:RAY RADIO & TV BROAD",
							"RGUSPRL:RAY RAILROAD EQ",
							"RGUSPRA:RAY RAILROADS",
							"RGUSFRE:RAY REAL ESTATE",
							"RGUSFRI:RAY REIT",
							"RGUSDRC:RAY RESTAURANTS",
							"RGUSDRT:RAY ROOF WALL & PLUM",
							"RGUSMRW:RAY RT & LS SVC CONS",
							"RGUSDRV:RAY RV & BOATS",
							"RGUSPSE:RAY SCI INS CTL & FLT",
							"RGUSPSP:RAY SCI INS POL CTRL",
							"RGUSPSI:RAY Sci INSTR ELEC",
							"RGUSFSB:RAY SEC BRKG & SVC",
							"RGUSTSC:RAY SE COND & COMP",
							"RGUSPSH:RAY SHIPPING",
							"RGUSDSR:RAY SPEC RET",
							"RGUSMST:RAY STEEL",
							"RGUSMSY:RAY SYN FIBR & CHEM",
							"RGUSTTM:RAY TECHNOLOGY MISC",
							"RGUSTTE:RAY TELEG EQ",
							"RGUSDTX:RAY TEXT APP & SHORES",
							"RGUSMTP:RAY TEXTILE PROD",
							"RGUSSTO:RAY TOBACC0",
							"RGUSDTY:RAY TOYS",
							"RGUSPTM:RAY TRANS MISC",
							"RGUSPTK:RAY TRUCK MISC",
							"RGUSUUE:RAY UTIL ELEC",
							"RGUSUUG:RAY UTIL GAS DIST",
							"RGUSUUT:RAY UTIL TELE",
							"RGUSUUW:RAY UTIL WATER"
						});

						ok = true;
					}
				}
			}
			return ok;
		}

		public string getGroup(string country)
		{
			string group = "";
			if (country == "Peru") group = "IGBVL";
			else if (country == "Colombia") group = "IGBC";
			else if (country == "Belgium") group = "BEL20";
			else if (country == "Estonia") group = "TALSE";
			else if (country == "Romania") group = "BET";
			else if (country == "Serbia") group = "BELEX15";
			else if (country == "Bosnia") group = "SASX10";
			else if (country == "Lithuania") group = "VILSE";
			else if (country == "Ukraine") group = "PFTS";
			else if (country == "Ghana") group = "GGSECI";
			else if (country == "Bahrain") group = "BHSEASI";
			else if (country == "Jordan") group = "JOSMGNFF";
			else if (country == "Croatia") group = "CRO";
			//else if (country == "Lebanon") group = "BLOM";
			else if (country == "Jordan") group = "JOSMGNFF";
			else if (country == "Oman") group = "MSM30";
			else if (country == "Botswana") group = "BGSMDC";
			else if (country == "Kenya") group = "KNSMIDX";
			else if (country == "Maurituius") group = "SEMDEX";
			else if (country == "Namibia") group = "FTN098";
			else if (country == "Nigeria") group = "NGSE30";
			else if (country == "Zambia") group = "LUSEIDX";
			else if (country == "Tunisia") group = "TUSISE";
			else if (country == "Philippines") group = "PCOMP";
			else if (country == "Kazakhstan") group = "KZKAK";
			else if (country == "Laos") group = "LSXC";
			else if (country == "Mongolia") group = "MSETOP";
			//else if (country == "SriLanka") group = "CSEALL";
			else if (country == "Bulgaria") group = "SOFIX";
			else if (country == "Cyprus") group = "CYSMMAPA";
			else if (country == "CzechRepublic") group = "PX";
			else if (country == "Iceland") group = "ICEXI";
			else if (country == "Latvia") group = "RIGSE";
			else if (country == "Slovakia") group = "SKSM";
			else if (country == "WORLD EQUITY INDICES") group = "WORLD EQUITY INDICES";
			else if (country == "FX SPOT") group = "FX SPOT";
			else if (country == "FX CROSS") group = "FX CROSS";
			else if (country == "10 YR") group = "10 YR";
			else if (country == "30 YR") group = "30 YR";
			else if (country == "5 YR") group = "5 YR";
			else if (country == "SINGLE CTY ETF") group = "SINGLE CTY ETF";
			else if (country == "US ETF") group = "US ETF";
			else if (country == "US_SECTOR ETF") group = "US_SECTOR ETF";
			else if (country == "SOVR CDS") group = "SOVR CDS";
			else if (country == "SPY ETF") group = "SPY ETF";
			else if (country == "CQG COMMODITIES >") group = "CQG COMMODITIES >";
			else if (country == "CQG EQUITIES >") group = "CQG EQUITIES >";
			else if (country == "CQG ETF >") group = "CQG ETF >";
			else if (country == "CQG INTEREST RATES >") group = "CQG INTEREST RATES >";
			else if (country == "CQG FX & CRYPTO >") group = "CQG FX & CRYPTO >";
			else if (country == "CQG STOCK INDICES >") group = "CQG STOCK INDICES >";
			else if (country == "CQG EQUITY SPREADS") group = "CQG EQUITY SPREADS";
			else if (country == "US FINANCIAL") group = "US FINANCIAL";
			else if (country == "ALPHA NETWORKS") group = "ALPHA NETWORKS";

			return group;
		}
	}
}