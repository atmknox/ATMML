using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ATMML
{
    // Stage 2 / increment 1: AOD "pages" — named sets of monitors with a tab bar.
    // Adapted from the ATM-PRO MarketMonitor to feed ATMML's existing _aods / drawAod()
    // / engine, rather than the ATM-PRO per-page draw loop. Persistence matches ATM-PRO:
    //   "aod pages"        -> tab-separated list of page GUIDs
    //   "aod {guid}"       -> line 0 = page name, then symbol\tdesc\tinterval\tmodel per monitor
    public partial class MarketMonitor
    {
        Guid _aodPageId;
        List<AODPage> _aodPages = new List<AODPage>();

        private AODPage getAodPage(Guid id) { return _aodPages.Find(x => x.Id == id); }
        private AODPage getActivePage() { return getAodPage(_aodPageId); }

        // Parse monitors for a page from a user-data tag (name line is skipped via >=4 fields).
        private List<AOD3> getPageAods(string tag)
        {
            var aods = new List<AOD3>();
            var text = (tag.Length > 0) ? MainView.LoadUserData(tag) : "";
            foreach (var line in text.Split('\n'))
            {
                if (line.Length == 0) continue;
                var fields = line.Split('\t');
                if (fields.Length >= 4)
                {
                    var aod1 = new AOD3();
                    aod1.Symbol = fields[0];
                    aod1.Description = fields[1];
                    aod1.Interval = (fields.Length > 2) ? fields[2] : "D";
                    aod1.ModelName = (fields.Length > 3) ? fields[3] : "";
                    if (aod1.ModelName == "") aod1.ModelName = _modelName;
                    aod1.PoP = "1.0";
                    aod1.MouseRightButtonUp += AOD_Capture;
                    aods.Add(aod1);
                }
            }
            return aods;
        }

        private string getDefaultAodPageName()
        {
            var names = _aodPages.Select(x => x.Name).ToList();
            for (int ii = 1; ii < 1000; ii++)
            {
                var name = "aods " + ii;
                if (!names.Contains(name)) return name;
            }
            return "aods";
        }

        private AODPage loadAODPage(Guid id)
        {
            var tag = "aod " + id.ToString();
            var text = MainView.LoadUserData(tag);
            var fields = text.Split('\n');
            var name = (fields.Length >= 1 && fields[0].Length > 0) ? fields[0] : getDefaultAodPageName();
            var page = new AODPage(name);
            page.Id = id;
            page.Aods = getPageAods(tag);
            return page;
        }

        private void saveAODPage(Guid id)
        {
            var page = getAodPage(id);
            if (page == null) return;
            var text = page.Name + "\n";
            page.Aods.ForEach(x => text += x.Symbol + "\t" + x.Description + "\t" + x.Interval + "\t" + x.ModelName + "\n");
            MainView.SaveUserData("aod " + id.ToString(), text);
        }

        private void saveAODPages()
        {
            var text = "";
            foreach (var page in _aodPages) { text += page.Id.ToString() + "\t"; saveAODPage(page.Id); }
            MainView.SaveUserData("aod pages", text);
        }

        // A fresh page opens with a single SPX Index card (per the AOD design). The card's
        // own "+" (AodEventType.Add) adds more; its "x" (Close) removes them -- both already
        // handled in handleAodEvent.
        private AOD3 newDefaultCard()
        {
            var aod = new AOD3();
            aod.Symbol = "SPX Index";
            aod.Description = "S&P Index";
            aod.Interval = "D";
            aod.ModelName = _modelName;
            aod.PoP = "";
            aod.PxProj = "";
            aod.MouseRightButtonUp += AOD_Capture;
            return aod;
        }

        private void createNewPage(string tag)
        {
            var page = new AODPage(getDefaultAodPageName());
            page.Aods = (tag.Length > 0 && tag != "aods") ? getPageAods(tag) : new List<AOD3>();
            if (page.Aods.Count == 0) page.Aods.Add(newDefaultCard());   // fresh page -> one SPX Index card
            _aodPageId = page.Id;
            _aodPages.Add(page);
            saveAODPages();
        }

        private void activateAODs()
        {
            var page = getActivePage();
            _aods = (page != null) ? page.Aods : new List<AOD3>();
            _aods.ForEach(x => { x.AodEvent -= handleAodEvent; x.AodEvent += handleAodEvent; });
        }

        private void deactivateAODs()
        {
            _aods.ForEach(x => x.AodEvent -= handleAodEvent);
        }

        private void loadAODPages()
        {
            _aodPages.Clear();

            // one-time reset: clear stale pages from earlier rounds so a fresh single-SPX page appears
            if (MainView.LoadUserData("aod reset v3").Length == 0)
            {
                MainView.SaveUserData("aod pages", "");
                MainView.SaveUserData("aod reset v3", "done");
            }

            var text = MainView.LoadUserData("aod pages");
            foreach (var id in text.Split('\t'))
            {
                if (id.Length == 0) continue;
                Guid g;
                if (Guid.TryParse(id, out g))
                {
                    var page = loadAODPage(g);
                    if (page != null) _aodPages.Add(page);
                }
            }
            if (_aodPages.Count == 0) createNewPage("aods");

            _aodPageId = _aodPages[0].Id;
            loadSCAddEnbs();
            activateAODs();
            drawAodPageList();
            drawAod();
            requestAODIndexBars();
            updateMonitorList();
            updateAods();
        }

        private void changeSelectedAODPage(Guid id)
        {
            deactivateAODs();
            _aodPageId = id;
            activateAODs();
            drawAodPageList();
            drawAod();
            requestAODIndexBars();
            updateMonitorList();
            updateAods();
        }

        private void drawAodPageList()
        {
            var active = getActivePage();
            if (active != null && PageName != null) PageName.Content = active.Name;

            if (PageList == null) return;
            PageList.Children.Clear();
            foreach (var page in _aodPages.OrderBy(x => x.Name))
            {
                var l1 = new Label();
                l1.Foreground = (page.Id == _aodPageId)
                    ? new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff))
                    : Brushes.White;
                l1.FontSize = 12;
                l1.FontWeight = FontWeights.Medium;
                l1.VerticalAlignment = VerticalAlignment.Top;
                l1.Margin = new Thickness(0, 0, 5, 0);
                l1.Content = page.Name;
                l1.Cursor = Cursors.Hand;
                l1.Tag = page.Id;
                l1.MouseDown += AODPageMenu_MouseDown;
                PageList.Children.Add(l1);
            }
        }

        private void AODPageMenu_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var label = sender as Label;
            if (label == null || !(label.Tag is Guid)) return;
            changeSelectedAODPage((Guid)label.Tag);
        }

        private void AODPageSetup_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (SourceNavigation.Visibility == Visibility.Collapsed)
            {
                SourceNavigation.Visibility = Visibility.Visible;
                SourceLevel1.Visibility = Visibility.Visible;
                AODScrollViewer.Visibility = Visibility.Collapsed;

                _aodChartVisibility = AODChartGrid.Visibility;
                AODChartGrid.Visibility = Visibility.Collapsed;

                drawPageSetup();
            }
            else
            {
                SourceNavigation.Visibility = Visibility.Collapsed;
                AODScrollViewer.Visibility = Visibility.Visible;
                AODChartGrid.Visibility = _aodChartVisibility;
            }
        }

        private Button makeSetupButton(string text, Brush lineBrush)
        {
            var b = new Button();
            b.Height = 25;
            b.BorderBrush = lineBrush;
            b.BorderThickness = new Thickness(1);
            b.Background = Brushes.Black;
            b.Foreground = Brushes.WhiteSmoke;
            b.HorizontalContentAlignment = HorizontalAlignment.Center;
            b.VerticalContentAlignment = VerticalAlignment.Center;
            b.FontSize = 11;
            b.Margin = new Thickness(0);
            b.Padding = new Thickness(0);
            b.Content = text;
            b.Cursor = Cursors.Hand;
            return b;
        }

        private void drawPageSetup()
        {
            SourceLevel1.Children.Clear();

            var bc = new BrushConverter();
            var lineBrush = (Brush)bc.ConvertFrom("#FF124b72");

            var p1 = new Grid();
            p1.RowDefinitions.Add(new RowDefinition { Height = new GridLength(25, GridUnitType.Pixel) });
            p1.RowDefinitions.Add(new RowDefinition { Height = new GridLength(25, GridUnitType.Pixel) });
            p1.RowDefinitions.Add(new RowDefinition { Height = new GridLength(100, GridUnitType.Star) });
            p1.RowDefinitions.Add(new RowDefinition { Height = new GridLength(25, GridUnitType.Pixel) });
            p1.HorizontalAlignment = HorizontalAlignment.Left;
            p1.VerticalAlignment = VerticalAlignment.Top;
            p1.Margin = new Thickness(0, 2, 2, 2);
            p1.Width = 320;

            var t1 = new TextBlock();
            t1.Background = Brushes.Black;
            t1.Foreground = Brushes.PaleGreen;
            t1.Text = "To change a page name, highlight it, then type your new name.";
            t1.FontSize = 11;
            t1.TextWrapping = TextWrapping.Wrap;
            Grid.SetRow(t1, 0);
            p1.Children.Add(t1);

            var bAdd = makeSetupButton("Add New Page", lineBrush);
            Grid.SetRow(bAdd, 1);
            bAdd.Click += AddAODPage_Click;
            p1.Children.Add(bAdd);

            var sv1 = new ScrollViewer();
            Grid.SetRow(sv1, 2);
            sv1.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            var p2 = new StackPanel { Orientation = Orientation.Vertical };
            sv1.Content = p2;
            p1.Children.Add(sv1);

            var bClose = makeSetupButton("Close", lineBrush);
            Grid.SetRow(bClose, 3);
            bClose.Click += CloseAODPageSetup_Click;
            p1.Children.Add(bClose);

            foreach (var page in _aodPages.OrderBy(x => x.Name))
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal };

                var tb = new TextBox();
                tb.Height = 25;
                tb.Width = 230;
                tb.BorderBrush = lineBrush;
                tb.BorderThickness = new Thickness(1);
                tb.Background = Brushes.Black;
                tb.Margin = new Thickness(0);
                tb.Padding = new Thickness(0);
                tb.FontSize = 10;
                tb.Foreground = Brushes.WhiteSmoke;
                tb.VerticalContentAlignment = VerticalAlignment.Center;
                tb.Text = page.Name;
                tb.Tag = page.Id;
                tb.TextChanged += AODPageName_TextChanged;
                row.Children.Add(tb);

                var bDel = makeSetupButton("Delete", lineBrush);
                bDel.Width = 75;
                bDel.Tag = page.Id;
                bDel.Click += DeleteAODPage_Click;
                row.Children.Add(bDel);

                p2.Children.Add(row);
            }

            SourceLevel1.Children.Add(p1);
        }

        private void AddAODPage_Click(object sender, RoutedEventArgs e)
        {
            createNewPage("");
            drawAodPageList();
            drawAod();
            updateMonitorList();
            requestAODIndexBars();
            drawPageSetup();
            saveAODPages();
        }

        private void DeleteAODPage_Click(object sender, RoutedEventArgs e)
        {
            var b = sender as Button;
            if (b == null || !(b.Tag is Guid)) return;
            var id = (Guid)b.Tag;

            var index = _aodPages.FindIndex(x => x.Id == id);
            if (_aodPages.Count > 1 && index >= 0)
            {
                if (_aodPageId == id)
                {
                    var idx = (index > 0) ? index - 1 : index + 1;
                    changeSelectedAODPage(_aodPages[idx].Id);
                }
                _aodPages.RemoveAll(x => x.Id == id);
            }
            drawPageSetup();
            drawAodPageList();
            saveAODPages();
        }

        private void AODPageName_TextChanged(object sender, TextChangedEventArgs e)
        {
            var tb = sender as TextBox;
            if (tb == null || !(tb.Tag is Guid)) return;
            var page = getAodPage((Guid)tb.Tag);
            if (page == null) return;
            page.Name = tb.Text;
            if ((Guid)tb.Tag == _aodPageId && PageName != null) PageName.Content = page.Name;
            drawAodPageList();
        }

        private void CloseAODPageSetup_Click(object sender, RoutedEventArgs e)
        {
            SourceNavigation.Visibility = Visibility.Collapsed;
            AODScrollViewer.Visibility = Visibility.Visible;
            AODChartGrid.Visibility = _aodChartVisibility;
            saveAODPages();
        }

        // ---- Page source via the SECURITY LIST navigation chooser (BLOOMBERG ... ML PORTFOLIOS) ----
        bool _aodPageSourceMode = false;

        private void aodLog(string msg)
        {
            try
            {
                var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ATMML_AOD.log");
                System.IO.File.AppendAllText(path, DateTime.Now.ToString("HH:mm:ss.fff") + "  " + msg + Environment.NewLine);
            }
            catch { }
        }

        // ---- Left panel: ADD MONITORS + monitor list + ATM Analysis Settings ----
        private void AddMonitor_MouseDown(object sender, MouseButtonEventArgs e)
        {
            addMonitor();
        }

        private void addMonitor(AOD3 after = null)
        {
            aodLog("addMonitor");
            var page = getActivePage();
            if (page == null) return;
            if (page.Aods.Count >= 16) { MessageBox.Show("Monitor limit of 16 reached for this page."); return; }

            var aod = newDefaultCard();
            aod.AodEvent += handleAodEvent;
            var idx = (after != null) ? page.Aods.FindIndex(x => x == after) : page.Aods.Count - 1;
            page.Aods.Insert(idx + 1, aod);
            _aods = page.Aods;

            drawAod();
            updateMonitorList();
            requestAODIndexBars();
            saveAODPage(_aodPageId);
        }

        private void deleteMonitor(AOD3 aod)
        {
            var page = getActivePage();
            if (page == null || page.Aods.Count <= 1) return;
            aod.AodEvent -= handleAodEvent;
            page.Aods.Remove(aod);
            _aods = page.Aods;

            drawAod();
            updateMonitorList();
            saveAODPage(_aodPageId);
        }

        private string abbrevInterval(string iv)
        {
            iv = (iv ?? "").Replace(" Min", "");
            switch (iv)
            {
                case "1D": case "Daily": return "D";
                case "1W": case "Weekly": return "W";
                case "1M": case "Monthly": return "M";
                case "1Q": case "Quarterly": return "Q";
                case "1S": case "SemiAnually": return "S";
                case "1Y": case "Yearly": return "Y";
                default: return iv;
            }
        }

        Dictionary<string, bool> _scAddEnbsCache = null;

        private string toChartInterval(string iv)
        {
            switch ((iv ?? "").Trim())
            {
                case "D": return "Daily";
                case "W": return "Weekly";
                case "M": return "Monthly";
                case "Q": return "Quarterly";
                case "Y": return "Yearly";
                default: return iv;
            }
        }

        private void loadSCAddEnbs()
        {
            var d = new Dictionary<string, bool>();
            d["New Trend"] = true; d["Pressure"] = true; d["Add"] = true; d["Retrace"] = true; d["Exh"] = true; d["ATR"] = false;
            try
            {
                var text = MainView.LoadUserData("aod scadds");
                foreach (var p in text.Split(';'))
                {
                    var kv = p.Split('=');
                    if (kv.Length == 2) d[kv[0]] = (kv[1] == "True");
                }
            }
            catch { }
            _scAddEnbsCache = d;
            try
            {
                EnableNewTrend.IsChecked = d["New Trend"];
                EnablePAlert.IsChecked = d["Pressure"];
                EnableAddAlert.IsChecked = d["Add"];
                EnableRegressionAlert.IsChecked = d["Retrace"];
                EnableExhAlert.IsChecked = d["Exh"];
            }
            catch { }
        }

        private void applySCAddEnbs()
        {
            var d = new Dictionary<string, bool>();
            d["New Trend"] = (EnableNewTrend.IsChecked == true);
            d["Pressure"] = (EnablePAlert.IsChecked == true);
            d["Add"] = (EnableAddAlert.IsChecked == true);
            d["Retrace"] = (EnableRegressionAlert.IsChecked == true);
            d["Exh"] = (EnableExhAlert.IsChecked == true);
            d["ATR"] = false;
            _scAddEnbsCache = d;
            var text = "";
            foreach (var kv in d) text += kv.Key + "=" + kv.Value + ";";
            try { MainView.SaveUserData("aod scadds", text); } catch { }
        }

        private void updateMonitorList()
        {
            aodLog("updateMonitorList panelNull=" + (MonitorPanel == null) + " pageAods=" + ((getActivePage() != null) ? getActivePage().Aods.Count : -1));
            if (MonitorPanel == null) return;
            MonitorPanel.Children.Clear();
            var page = getActivePage();
            if (page == null) return;

            foreach (var aod in page.Aods)
            {
                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });   // Symbol
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(23) });   // TF
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(29) });   // (spacer)
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });   // X

                var shortSym = aod.Symbol ?? ""; int _sp = shortSym.IndexOf(' '); if (_sp > 0) shortSym = shortSym.Substring(0, _sp);
                var sym = new Label { Content = shortSym, Foreground = Brushes.White, FontFamily = new FontFamily("Helvetica Neue"), FontSize = 9, Padding = new Thickness(0), Cursor = Cursors.Hand, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(9,0,0,0) };
                Grid.SetColumn(sym, 0);
                var capturedSym = aod;
                sym.MouseDown += delegate { openAodChartFor(capturedSym); };
                sym.MouseEnter += OurView_MouseEnter;
                sym.MouseLeave += OurView_MouseLeave;

                var tf = new Label { Content = abbrevInterval(aod.Interval), FontFamily = new FontFamily("Helvetica Neue"), FontSize = 9, Padding = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Right };
                tf.Foreground = (aod.Direction > 0) ? Brushes.PaleGreen : (aod.Direction < 0) ? Brushes.Red : Brushes.White;
                Grid.SetColumn(tf, 1);

                var del = new Label { Content = "x", Foreground = Brushes.White, FontFamily = new FontFamily("Helvetica Neue"), FontSize = 12, Cursor = Cursors.Hand, Padding = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Center };
                Grid.SetColumn(del, 3);
                var captured = aod;
                del.MouseDown += delegate { deleteMonitor(captured); };
                del.MouseEnter += OurView_MouseEnter;
                del.MouseLeave += OurView_MouseLeave;

                row.Children.Add(sym);
                row.Children.Add(tf);
                row.Children.Add(del);
                MonitorPanel.Children.Add(row);
            }
            aodLog("updateMonitorList done children=" + MonitorPanel.Children.Count);
        }

        // Click a monitor symbol to load it into the AOD chart (mirrors AodEventType.Chart).
        private void openAodChartFor(AOD3 aod)
        {
            if (aod == null || _AODchart1 == null) return;
            showAODChart();
            _aodChartVisibility = Visibility.Visible;
            AODChartGrid.Visibility = _aodChartVisibility;
            _chartSymbol = aod.Symbol;
            _AODchart1.Change(aod.Symbol, toChartInterval(aod.Interval));
            foreach (var a0 in _aods) a0.SetSelected(a0 == aod);
            var modelNames = new List<string>(); modelNames.Add(aod.ModelName);
            _AODchart1.ModelNames = modelNames;
        }

        private void ApplySCAdds(object sender, MouseButtonEventArgs e)
        {
            // The checkbox selection will feed the analysis filter in the next pass;
            // for now, applying redraws the cards with the current selection.
            applySCAddEnbs();
            foreach (var a1 in _aods) requestUpdate(a1);
            drawAod();
        }

        private void openPageSource()
        {
            _aodPageSourceMode = true;
            aodLog("openPageSource");

            AODScrollViewer.Visibility = Visibility.Collapsed;
            SourceNavigation.Visibility = Visibility.Visible;

            _aodChartVisibility = AODChartGrid.Visibility;
            AODChartGrid.Visibility = Visibility.Collapsed;

            srcNav.UseCheckBoxes = false;
            srcNav.UseGroup = true;

            List<string> items = new List<string>();
            if (BarServer.ConnectedToBloomberg() || !BarServer.ConnectedToCQG())
                items.AddRange(new string[] { "BLOOMBERG >", " ", "COMMODITIES >", " ", "EQ | AMERICAS >", " ", "EQ | ASIA PACIFIC >", " ", "EQ | EUROPE & MEA >", " ", "ETF >", " ", "FX & CRYPTO >", " ", "GLOBAL FUTURES >", " ", "INTEREST RATES >", " ", "ML PORTFOLIOS >" });
            if (BarServer.ConnectedToCQG())
                items.AddRange(new string[] { " ", "CQG COMMODITIES >", " ", "CQG EQUITIES >", " ", "CQG ETF >", " ", "CQG FX & CRYPTO >", " ", "CQG INTEREST RATES >", " ", "CQG STOCK INDICES >" });

            srcNav.setNavigation(SourceLevel1, SourceLevel1_MouseDown, items.ToArray());

            highlightButton(SourceLevel1, _sourceLevel1);
            highlightButton(SourceLevel2, _sourceLevel2);
            highlightButton(SourceLevel3, _sourceLevel3);
            highlightButton(SourceLevel4, _sourceLevel4);
            highlightButton(SourceLevel5, _sourceLevel5);
            highlightButton(SourceLevel6, _sourceLevel6);
        }

        // Called from loadMemberPanel (patched) when a portfolio's members have loaded into _portfolio1 in page mode.
        private void buildPageFromMembers()
        {
            _aodPageSourceMode = false;

            var members = _portfolio1.GetSymbols();
            aodLog("page-source members=" + (members == null ? -1 : members.Count));

            AODScrollViewer.Visibility = Visibility.Visible;
            SourceNavigation.Visibility = Visibility.Collapsed;
            AODChartGrid.Visibility = _aodChartVisibility;

            if (members == null || members.Count == 0) return;

            var page = getActivePage();
            if (page == null) return;

            deactivateAODs();

            var monitors = new List<AOD3>();
            foreach (var sym in members)
            {
                var aod = new AOD3();
                aod.Symbol = sym.Ticker;
                aod.Description = sym.Description;
                aod.Interval = "D";
                aod.ModelName = _modelName;
                aod.PoP = "1.0";
                aod.MouseRightButtonUp += AOD_Capture;
                monitors.Add(aod);
            }

            page.Aods = monitors;

            activateAODs();
            drawAodPageList();
            drawAod();
            requestAODIndexBars();
            saveAODPages();
        }

        private void Legend_MouseEnter(object sender, MouseEventArgs e)
        {
            var label = sender as Control; if (label != null) label.Foreground = new SolidColorBrush(Color.FromRgb(0x00,0xcc,0xff));
        }

        private void Legend_MouseLeave(object sender, MouseEventArgs e)
        {
            var label = sender as Control; if (label != null) label.Foreground = new SolidColorBrush(Color.FromRgb(0xff,0xff,0xff));
        }
    }

    public class AODPage
    {
        public AODPage(string name)
        {
            Id = Guid.NewGuid();
            Name = name;
        }

        public Guid Id;
        public string Name;
        public List<AOD3> Aods = new List<AOD3>();
    }
}
