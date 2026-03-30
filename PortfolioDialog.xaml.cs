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
    /// Interaction logic for PortfolioDialog.xaml
    /// </summary>
    public partial class PortfolioDialog : UserControl
    {
        public event DialogEventHandler DialogEvent;
        
        public PortfolioDialog()
        {
            InitializeComponent();

            initPortfolioTree();
        }

        private void initPortfolioTree()
        {
            PortfolioTree.Background = Brushes.White;
            PortfolioTree.Foreground = Brushes.Black;

            TreeViewItem group1 = new TreeViewItem();
            group1.Header = "Global Macro";
            PortfolioTree.Items.Add(group1);

            addPortfolio("GLOBAL 30YR", group1);
            addPortfolio("GLOBAL 10YR", group1);
            addPortfolio("GLOBAL 7YR", group1);
            addPortfolio("GLOBAL 51YR", group1);
            addPortfolio("GLOBAL 2YR", group1);
            addPortfolio("GLOBAL 1YR", group1);
            addPortfolio("SPOT FX", group1);
            addPortfolio("COMMODITIES", group1);

            TreeViewItem group2 = new TreeViewItem();
            group2.Header = "Global Industries by Region";
            PortfolioTree.Items.Add(group2);

            addPortfolio("N AMERICAN IND", group2);
            addPortfolio("EUROPEAN IND", group2);
            addPortfolio("ASIAN IND", group2);

            TreeViewItem group3 = new TreeViewItem();
            group3.Header = "Global Industries by Country / Index";
            PortfolioTree.Items.Add(group3);

            addPortfolio("US IND", group3);
            addPortfolio("CA IND", group3);
            addPortfolio("EU IND", group3);
            addPortfolio("HK IND", group3);
            addPortfolio("JP IND", group3);
            addPortfolio("KR IND", group3);
            addPortfolio("TW IND", group3);
            addPortfolio("AU IND", group3);
        }

        private void addPortfolio(string name, ItemsControl parent)
        {
            TreeViewItem item = new TreeViewItem();

            StackPanel panel = new StackPanel();

            panel.Orientation = Orientation.Horizontal;
            panel.Margin = new Thickness(0);
            CheckBox checkBox = new CheckBox();
            checkBox.Margin = new Thickness(0, 2, 2, 0);
            panel.Children.Add(checkBox);

            TextBlock textBlock = new TextBlock();
            textBlock.FontSize = 10;
            textBlock.Width = 128;
            textBlock.Padding = new Thickness(0);
            textBlock.Text = name;
            panel.Children.Add(textBlock);

            item.Header = panel;

            parent.Items.Add(item);
        }

        public void SetPortfolios(List<string> portfolios)
        {
            foreach (TreeViewItem parent in PortfolioTree.Items)
            {
                foreach (TreeViewItem child in parent.Items)
                {
                    StackPanel panel = child.Header as StackPanel;
                    if (panel != null)
                    {
                        TextBlock textBlock = panel.Children[1] as TextBlock;
                        string portfolio1 = textBlock.Text;
                        bool found = false;
                        foreach (string portfolio2 in portfolios)
                        {
                            if (portfolio2.CompareTo(portfolio1) == 0)
                            {
                                found = true;
                                parent.IsExpanded = true;
                                break;
                            }
                        }
                        CheckBox checkBox = panel.Children[0] as CheckBox;
                        checkBox.IsChecked = found;
                    }
                }
            }

            foreach (string portfolio in portfolios)
            {
            }
        }

        public List<string> GetPortfolios()
        {
            List<string> portfolios = new List<string>();

            foreach (TreeViewItem parent in PortfolioTree.Items)
            {
                foreach (TreeViewItem child in parent.Items)
                {
                    StackPanel panel = child.Header as StackPanel;
                    if (panel != null)
                    {
                        CheckBox checkBox = panel.Children[0] as CheckBox;
                        if (checkBox.IsChecked == true)
                        {
                            TextBlock textBlock = panel.Children[1] as TextBlock;
                            portfolios.Add(textBlock.Text);
                        }
                    }
                }
            }
            return portfolios;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
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

    }
}
