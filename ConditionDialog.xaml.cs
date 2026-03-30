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
    /// Interaction logic for ConditionDialog.xaml
    /// </summary>
    public partial class ConditionDialog : Window
    {
        public event DialogEventHandler DialogEvent;

        private MainView _mainView = null;
        private int _selectedCondition = 0;
        private string[] _condition = { "", "", "", "", "", "" };
        private string[] _conditionType = { "1", "1", "1", "1", "1", "1" };
        private int _horizon = 0;
     
        public ConditionDialog(MainView mainView)
        {
            _mainView = mainView;
            InitializeComponent();

            ResourceDictionary dictionary = new ResourceDictionary();
            dictionary.Source = new Uri("pack://application:,,,/ATMML;component/StyleDictionary.xaml");
            this.Resources.MergedDictionaries.Add(dictionary);

            initConditionTree();

            setConditionType();
        }

        public string[] Condition
        {
            get { return _condition; }
            set { setCondition(value); }
        }

        public int Horizon
        {
            get { return _horizon; }
            set { _horizon = value; }
        }

        private string[] getIntervalList()
        {
            string[] list = { "Long Term", "Mid Term", "Short Term"};
            return list;
        }

        private void setCondition(string[] input)
        {
            _condition = input;
            setCondition(_condition[_selectedCondition]);
        }

        private void setCondition(string input)
        {
            if (ConditionTree != null)
            {
                resetTree(ConditionTree);

                string[] conditions = input.Split('\u0001');
                for (int ii = 0; ii < conditions.Length; ii++)
                {
                    var condition = conditions[ii];
                    if (condition.Length > 0)
                    {
                        string[] field = condition.Split('\u0002');
                        string name = field[0];
                        string interval = (field.Length > 1) ? field[1] : "Daily";
                        string ago = (field.Length > 2) ? field[2] : "10000";
                        string type = (field.Length > 3) ? field[3] : "1";
                        ago = "10000"; // temporary

                        CheckboxTreeViewItem item = getIntervalItem(ConditionTree, name, interval);
                        if (item != null)
                        {
                            item.IsExpanded = true;
                            //for (int ii = 0; ii < ago.Length; ii++)
                            for (int jj = 0; jj < 1; jj++)
                            {
                                if (ago[jj] == '1')
                                {
                                    item.CheckBox[jj].IsChecked = true;
                                }
                            }
                        }

                        _conditionType[ii] = type;
                    }
                }
            }
        }

        private void resetTree(ItemsControl tree)
        {
            foreach (TreeViewItem level1 in tree.Items)
            {
                level1.IsExpanded = false;
                foreach (TreeViewItem level2 in level1.Items)
                {
                    level2.IsExpanded = false;
                    foreach (TreeViewItem level3 in level2.Items)
                    {
                        level3.IsExpanded = false;
                        CheckboxTreeViewItem intervalItem = level3 as CheckboxTreeViewItem;
                        if (intervalItem != null)
                        {
                            foreach (var checkBox in intervalItem.CheckBox)
                            {
                                checkBox.IsChecked = false;
                            }
                        }
                    }
                }
            }
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
                            label.FontSize = 10;
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
                label.Foreground = new SolidColorBrush(Colors.White);
                label.FontSize = 10;
                label.Padding = new Thickness(0);
                label.Width = 64;
                label.Content = interval + ":";
                panel.Children.Add(label);

                panel.Orientation = Orientation.Horizontal;
                panel.Margin = new Thickness(0);

                CheckBox checkBox = new CheckBox();
                item.CheckBox.Add(checkBox);
                checkBox.Margin = new Thickness(0, 2, 2, 0);
                panel.Children.Add(checkBox);

                item.Header = panel;

                parent.Items.Add(item);
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
                        label.Foreground = new SolidColorBrush(Colors.White);
                        label.FontSize = 10;
                        conditionName = label.Content as string;

                        StackPanel panel = checkBoxItem.Header as StackPanel;
                        label = panel.Children[0] as Label;
                        label.Foreground = new SolidColorBrush(Colors.White);
                        label.FontSize = 10;
                        interval = label.Content as string;
                        interval = interval.Replace(":", "");
                    }
                    else
                    {
                        StackPanel panel = checkBoxItem.Header as StackPanel;
                        Label label = panel.Children[0] as Label;
                        label.Foreground = new SolidColorBrush(Colors.White);
                        label.FontSize = 10;
                        conditionName = label.Content as string;
                    }

                    int type = (AllBars.IsChecked == true) ? 1 : 2;

                    condition = conditionName + "\u0002" + interval + "\u0002" + ago + "\u0002" + type;
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
                    label.Foreground = new SolidColorBrush(Colors.White);
                    label.FontSize = 10;
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
                            subGroupItem.Items.Add(conditionItem);
                            addIntervalTree(conditionItem);
                        }
                    }
                }
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            _condition[_selectedCondition] = getCondition(ConditionTree);

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

        private void Condition_Checked(object sender, RoutedEventArgs e)
        {
            RadioButton button = sender as RadioButton;
            string name = (string)button.Content;
            int index = int.Parse(name.Substring(name.Length - 1)) - 1;
            radioButtonChange(index, sender as RadioButton);
        }
        private void ConditionType_Checked(object sender, RoutedEventArgs e)
        {
            RadioButton button = sender as RadioButton;
            string name = (string)button.Content;
        }

        private void radioButtonChange(int index, RadioButton button)
        {
            if (button.IsChecked.Value)
            {

                _condition[_selectedCondition] = getCondition(ConditionTree);
                _selectedCondition = index;
                setCondition(_condition[_selectedCondition]);
                setConditionType();
            }
        }

        private void setConditionType()
        {
            string type = _conditionType[_selectedCondition];
            if (AllBars != null && LastBar != null)
            {
                if (type == "1") AllBars.IsChecked = true;
                else if (type == "2") LastBar.IsChecked = true;
            }
        }
    }
}
