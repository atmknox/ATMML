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
    public class Strategy : IComparable<Strategy>
    {
        public string LongEntry { get; set; }
        public string LongExit { get; set; }
        public string ShortEntry { get; set; }
        public string ShortExit { get; set; }

        public int CompareTo(Strategy other)
        {
            int result = LongEntry.CompareTo(other.LongEntry);
            if (result == 0) result = LongExit.CompareTo(other.LongExit);
            if (result == 0) result = ShortEntry.CompareTo(other.ShortEntry);
            if (result == 0) result = ShortExit.CompareTo(other.ShortExit);
            return result;
        }
    }

    public partial class StrategyBuilder : UserControl
    {

        public event DialogEventHandler DialogEvent;

        private MainView _mainView;
        private string _selectedStrategy = "";
        private string _selectedAction = "Long Entry";
        private Dictionary<string, Strategy> _strategies = new Dictionary<string, Strategy>();

        public Dictionary<string, Strategy> Strategies
        {
            get
            {
                return _strategies;
            }
            set {
                _strategies = value;
                var list1 = _strategies.Keys.ToList();
                if (list1.Count > 0)
                {
                    _selectedStrategy = list1[0];
                }
                initStrategies();
            }
        }
        public StrategyBuilder(MainView mainView)
        {
            _mainView = mainView;

            InitializeComponent();

            ResourceDictionary dictionary = new ResourceDictionary();
            dictionary.Source = new Uri("pack://application:,,,/ATMML;component/StyleDictionary.xaml");
            this.Resources.MergedDictionaries.Add(dictionary);

            initConditionTree();
        }

        private void initStrategies()
        {
            StrategyPanel.Children.Clear();
            foreach (KeyValuePair<string, Strategy> kvp in _strategies) 
            {
                var rb = new RadioButton();
                rb.GroupName = "Strategy";
                rb.Background = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
                rb.Foreground = new SolidColorBrush(Colors.White);
                rb.FontSize = 11;
                rb.Margin = new Thickness(0, 5, 0, 0);
                rb.Padding = new Thickness(5, 0, 0, 0);
                rb.Content = kvp.Key;

                rb.Click += StrategySelection;
                if (kvp.Key == _selectedStrategy)
                {
                    rb.IsChecked = true;
                }
                StrategyPanel.Children.Add(rb);
            }
            initStrategy();
         }

        private void initStrategy()
        {
            if (StrategyTitle != null)
            {
                StrategyTitle.Text = _selectedStrategy;
                Strategy strategy;
                if (_strategies.TryGetValue(_selectedStrategy, out strategy))
                {
                    if (_selectedAction == "Long Entry") Editor.Text = strategy.LongEntry;
                    else if (_selectedAction == "Long Exit") Editor.Text = strategy.LongExit;
                    else if (_selectedAction == "Short Entry") Editor.Text = strategy.ShortEntry;
                    else if (_selectedAction == "Short Exit") Editor.Text = strategy.ShortExit;
                }
            }
        }

        private string[] getIntervalList()
        {
            string[] list = { "Long Term", "Mid Term", "Short Term" };
            return list;
        }

        private CheckboxTreeViewItem getIntervalItem(ItemsControl parent, string name, string interval)
        {
            CheckboxTreeViewItem item = null;
            foreach (TreeViewItem child in parent.Items)
            {
                Label label = child.Header as Label;

                string childName = (label != null) ? label.Content as string : "";
                if (childName == name)
                {
                    foreach (TreeViewItem treeItem in child.Items)
                    {
                        CheckboxTreeViewItem intervalItem = treeItem as CheckboxTreeViewItem;
                        if (intervalItem != null)
                        {
                            StackPanel panel = intervalItem.Header as StackPanel;
                            label = panel.Children[0] as Label;
                            label.Foreground = new SolidColorBrush(Colors.White);
                            label.FontSize = 11;
                            
                            string childInterval = label.Content as string;
                            childInterval = childInterval.Replace(":", "");
                            if (childInterval == interval)
                            {
                                child.IsExpanded = true;
                                item = intervalItem;
                                break;
                            }
                        }
                    }
                    break;
                }
                else
                {
                    item = getIntervalItem(child, name, interval);
                    if (item != null)
                    {
                        child.IsExpanded = true;
                        break;
                    }
                }
            }
            return item;
        }

        private void addIntervalTree(TreeViewItem parent)
        {
            string[] intervals = getIntervalList();
            foreach (string interval in intervals)
            {
                CheckboxTreeViewItem item = new CheckboxTreeViewItem();
                item.HasInterval = true;

                StackPanel panel = new StackPanel();

                Label label = new Label();
                label.Padding = new Thickness(0);
                label.Width = 64;
                label.Foreground = new SolidColorBrush(Colors.White);
                label.FontSize = 11;
                label.Content = interval + ":";
                panel.Children.Add(label);

                panel.Orientation = Orientation.Horizontal;
                panel.Margin = new Thickness(0);

                CheckBox checkBox = new CheckBox();

                checkBox.Click += ConditionSelected;

                item.CheckBox.Add(checkBox);
                checkBox.Margin = new Thickness(0, 2, 2, 0);
                panel.Children.Add(checkBox);

                item.Header = panel;

                parent.Items.Add(item);
            }
        }

        private void ConditionSelected(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = sender as CheckBox;
            if (checkBox.IsChecked == true)
            {
                clearAllChecks(ConditionTree);
                checkBox.IsChecked = true;
            }
        }

        private void clearAllChecks(ItemsControl item)
        {
            CheckboxTreeViewItem checkBoxItem = item as CheckboxTreeViewItem;
            if (checkBoxItem != null)
            {
                int count = checkBoxItem.CheckBox.Count;
                if (count > 0)
                {
                    for (int ii = 0; ii < count; ii++)
                    {
                        checkBoxItem.CheckBox[ii].IsChecked = false;
                    }
                }
            }

            foreach (ItemsControl child in item.Items)
            {
                clearAllChecks(child);
            }
        }

        private string getCondition(ItemsControl item)
        {
            string condition = "";

            CheckboxTreeViewItem checkBoxItem = item as CheckboxTreeViewItem;
            if (item != null)
            {

                string ago = "";
                if (checkBoxItem != null)
                {
                    for (int ii = 0; ii < checkBoxItem.CheckBox.Count; ii++)
                    {
                        ago += (checkBoxItem.CheckBox[ii].IsChecked == true) ? "1" : "0";
                    }
                }

                bool isChecked = (ago.Length > 0 && int.Parse(ago) != 0);

                if (isChecked)
                {
                    bool hasInterval = checkBoxItem.HasInterval;
                    string conditionName = "";
                    string interval = "";
                    if (hasInterval)
                    {
                        TreeViewItem conditionItem = checkBoxItem.Parent as TreeViewItem;
                        Label label = conditionItem.Header as Label;
                        conditionName = label.Content as string;

                        StackPanel panel = checkBoxItem.Header as StackPanel;
                        label = panel.Children[0] as Label;
                        interval = label.Content as string;
                        interval = interval.Replace(":", "");
                    }
                    else
                    {
                        StackPanel panel = checkBoxItem.Header as StackPanel;
                        Label label = panel.Children[0] as Label;
                        conditionName = label.Content as string;
                    }

                    condition = conditionName + " on " + interval;
                }
                else
                {
                    foreach (ItemsControl child in item.Items)
                    {
                        string childCondition = getCondition(child);
                        if (childCondition.Length > 0)
                        {
                            if (condition.Length > 0)
                            {
                                condition += "\u0001";
                            }
                            condition += childCondition;
                        }
                    }
                }
            }
            return condition;
        }

        private void initConditionTree()
        {
            string[] groups = Conditions.GetConditionList();
            foreach (string group in groups)
            {
                TreeViewItem groupItem = new TreeViewItem();
                groupItem.Header = group;
                groupItem.Foreground = new SolidColorBrush(Colors.White);
                groupItem.FontSize = 10;
                ConditionTree.Items.Add(groupItem);

                string[] subgroups = Conditions.GetConditionList(group);
                foreach (string subgroup in subgroups)
                {
                    string[] field = subgroup.Split('\u0003');
                    string name = field[0];
                    string tooltip = (field.Length > 1) ? field[1] : "";

                    TreeViewItem subGroupItem = new TreeViewItem();

                    Label label = new Label();
                    label.Content = name;
                    label.Foreground = new SolidColorBrush(Colors.White);
                    label.FontSize = 10;
                    label.Padding = new Thickness(0);
                    if (tooltip.Length > 0) label.ToolTip = tooltip;

                    subGroupItem.Header = label;
                    groupItem.Items.Add(subGroupItem);

                    string[] conditions = Conditions.GetConditionList(subgroup);
                    if (conditions.Length == 0)
                    {
                        addIntervalTree(subGroupItem);
                    }
                    else
                    {
                        foreach (string condition in conditions)
                        {
                            TreeViewItem conditionItem = new TreeViewItem();

                            field = condition.Split('\u0003');
                            name = field[0];
                            tooltip = (field.Length > 1) ? field[1] : "";

                            label = new Label();
                            label.Content = name;
                            label.Foreground = new SolidColorBrush(Colors.White);
                            label.FontSize = 10;
                            label.Padding = new Thickness(0);

                            if (tooltip.Length > 0) label.ToolTip = tooltip;

                            conditionItem.Header = label;
                            label.Foreground = new SolidColorBrush(Colors.White);
                            label.FontSize = 10;

                            subGroupItem.Items.Add(conditionItem);
                            addIntervalTree(conditionItem);
                        }
                    }
                }
            }
        }

        private void ChangeStrategyName(object sender, RoutedEventArgs e)
        {
            string oldName = _selectedStrategy;
            string newName = StrategyTitle.Text;
            if (!_strategies.ContainsKey(newName))
            {
                var strategy = _strategies[oldName];
                _strategies.Remove(oldName);
                _strategies[newName] = strategy;
                _selectedStrategy = newName;
                initStrategies();
            }
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

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            for (int ii = 1; ii < 100; ii++)
            {
                string name = "Strategy " + ii.ToString();
                if (!_strategies.ContainsKey(name))
                {
                    _strategies[name] = new Strategy();
                    break;
                }
            }
            initStrategies();
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            string name = _selectedStrategy;
            if (_strategies.ContainsKey(name))
            {
                _strategies.Remove(name);
                var list1 = _strategies.Keys.ToList();
                _selectedStrategy = (list1.Count > 0) ? list1[0] : "";
            }
            initStrategies();
        }

        private void StrategySelection(object sender, RoutedEventArgs e)
        {
            RadioButton button = sender as RadioButton;
             radioButtonChange(sender as RadioButton);
        }

        private void radioButtonChange(RadioButton button)
        {
            if (button.IsChecked.Value)
            {
                _selectedStrategy = button.Content as string;
                initStrategy();
            }
        }

        private void ActionSelection(object sender, RoutedEventArgs e)
        {
            RadioButton rb = sender as RadioButton;
            if (rb.IsChecked == true)
            {
                _selectedAction = rb.Content as string;
                initStrategy();
            }
        }

        private void AddCondition(object sender, RoutedEventArgs e)
        {
            string condition = getCondition(ConditionTree);
            Editor.AppendText(condition);
            clearAllChecks(ConditionTree);
            Editor.Focus();
        }

        private void ChangeStrategy(object sender, RoutedEventArgs e)
        {
            string name = _selectedStrategy;
            string action = _selectedAction;
            Strategy strategy;
            if (_strategies.TryGetValue(name, out strategy))
            {
                if (_selectedAction == "Long Entry") strategy.LongEntry = Editor.Text;
                else if (_selectedAction == "Long Exit") strategy.LongExit = Editor.Text;
                else if (_selectedAction == "Short Entry") strategy.ShortEntry = Editor.Text;
                else if (_selectedAction == "Short Exit") strategy.ShortExit = Editor.Text;
            }
        }
    }
}
