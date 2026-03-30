using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace ATMML
{

    public class TreeNode : INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _isExpanded;
        private bool _isChecked;
        private List<TreeNode> _children = null;
        private static string[] _defaultIntervals = new string[] { "Quarterly", "Monthly", "Weekly", "Daily", "240 Min", "120 Min", "60 Min", "30 Min", "15 Min", "5 Min" };
        public event PropertyChangedEventHandler PropertyChanged;
        private bool _useIntervals = false;
        private string[] _intervals;
        private Indicator _indicator = null;
        private Parameter _parameter = null;
        private TreeNode _parent = null;

        public TreeNode(string name, string[] intervals = null, bool useIntervals = true)
        {
            Name = name;

            _intervals = (intervals == null) ? _defaultIntervals : intervals;
            _useIntervals = useIntervals;

            var indicator = Conditions.CreateIndicator(Name);
            if (indicator.Parameters.Count > 0)
            {
                _indicator = indicator;
            }
        }

        public void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }

        public bool IsExpanded
        {
            get
            {
                return _isExpanded;
            }

            set
            {
                if (value != _isExpanded)
                {
                    _isExpanded = value;
                    this.OnPropertyChanged("IsExpanded");
                }
            }
        }

        public bool IsSelected
        {
            get
            {
                return _isSelected;
            }

            set
            {
                if (value != _isSelected)
                {
                    _isSelected = value;
                    this.OnPropertyChanged("IsSelected");
                }
            }
        }

        public bool IsChecked
        {
            get
            {
                return _isChecked;
            }

            set
            {
                if (value != _isChecked)
                {
                    _isChecked = value;
                    if (IsBooleanParameter)
                    {
                        ParameterValue = _isChecked ? "True" : "False";
                        _parent.IsSelected = true;
                    }
                    this.OnPropertyChanged("IsChecked");
                }
            }
        }

        public void SetValue(string fieldName, object fieldValue)
        {
            if (IsFundamental && IsSelected && Name == fieldName)
            {
                var text = fieldValue.ToString();
                if (text.Length > 0)
                {
                    double value;
                    if (double.TryParse(text, out value))
                    {
                        Value = value.ToString("#.00");
                    }
                    else
                    {
                        Value = text;
                    }
                }
            }
            else if (IsGroup)
            {
                foreach (var child in _children)
                {
                    child.SetValue(fieldName, fieldValue);
                }
            }
        }

        public bool HasChildren
        {
            get { return _children != null && _children.Count > 0; }
        }

        public ICollection<TreeNode> Children
        {
            get
            {
                if (_children == null)
                {
                    updateChildren();
                }
                return _children;
            }
        }

        private void updateChildren()
        {
            var group = (Level == 1) ? Name : Group;

            string[] names = IsParameter ? null : Conditions.GetConditionList(Name);

            if (names != null && names.Length > 0) // IsGroup
            {
                if (names.Length > 0)
                {
                    _children = new List<TreeNode>();
                    foreach (var name in names)
                    {
                        string[] fields = name.Split(':');

                        var node = new TreeNode(name, _intervals, _useIntervals);
                        node.Level = Level + 1;
                        node.Group = group;
                        node.Name = (fields.Length >= 2) ? fields[0] : name;
                        node.Relationship = node.IsFundamental ? "<" : null;
                        node.Value = node.IsFundamental ? "" : null;
                        _children.Add(node);
                    }
                }
            }
            else if (IsIndicator && _useIntervals)
            {
                _children = new List<TreeNode>();
                foreach (var interval in _intervals)
                {
                    var node = new TreeNode(interval);
                    node._indicator = _indicator;
                    node.Level = Level + 1;
                    node.Group = group;
                    node.Relationship = null;
                    node.Value = null;
                    node.Interval = interval;
                    _children.Add(node);
                }
            }
            else if (IsIndicatorInterval || IsIndicator && !_useIntervals)
            {
                _children = new List<TreeNode>();
                foreach (var kvp in _indicator.Parameters)
                {
                    var parameter = kvp.Value;
                    if (!(parameter is ColorParameter))
                    {
                        var node = new TreeNode(parameter.Name);
                        node._parent = this;
                        node._indicator = _indicator;
                        node._parameter = parameter;
                        node.Level = Level + 1;
                        node.Group = group;
                        node.Relationship = null;
                        node.Value = null;
                        _children.Add(node);
                    }
                }
            }
            else if (IsCondition && _useIntervals && Interval == null)
            {
                _children = new List<TreeNode>();
                foreach (var interval in _intervals)
                {
                    var node = new TreeNode(interval);
                    node.Level = Level + 1;
                    node.Group = group;
                    node.Name = Name;
                    node.Relationship = null;
                    node.Value = null;
                    node.Interval = interval;
                    _children.Add(node);
                }
            }
        }

        private string _value = "1";

        public int Level { get; set; }

        public string Group { get; set; }

        public string Name { get; set; }

        public string Relationship { get; set; }

        public string Value
        {
            get { return _value; }
            set { _value = value; this.OnPropertyChanged("Value"); }
        }

        public string Interval { get; set; }

        public bool IsGroup
        {
            get { return HasChildren && !IsIndicator && !IsIndicatorInterval; }
        }

        public bool IsFundamental
        {
            get
            {
                return !IsGroup && Group != null && !Group.Contains("ATM") && !Group.Contains("PRICE COMPARISONS") && !Group.Contains("NORMALIZED BARS") &&
                  !Group.Contains("PRICE PATTERNS") && !Group.Contains("STANDARD TECHNICALS") && !IsUserData && !IsRemoveSymbol && !IsRemoveIndustry && !IsRemoveSector && !IsIndicator && !IsIndicatorInterval && !IsParameter;
            }
        }

        public bool IsUserData
        {
            get
            {
                return !IsGroup && Group != null && Group.Contains("USER FEATURE DATA");
            }
        }

        public bool IsCondition
        {
            get { return !IsGroup && !IsModel && !IsFundamental && !IsUserData && !IsRemoveSymbol && !IsRemoveIndustry && !IsRemoveSector && !IsDefault && !IsIndicator &&  !IsIndicatorInterval && !IsParameter; }
        }

        public bool IsIndicator
        {
            get { return _indicator != null && Interval == null && _parameter == null; }
        }

        public bool IsIndicatorInterval
        {
            get { return _indicator != null && Interval != null && _parameter == null; }
        }

        public bool IsParameter
        {
            get { return _parameter != null; }
        }

        public bool IsBooleanParameter
        {
            get { return IsParameter && _parameter is BooleanParameter; }
        }

        public bool IsNumberParameter
        {
            get { return IsParameter && _parameter is NumberParameter; }
        }
        public bool IsStudyNumberParameter
        {
            get { return IsParameter && _parameter is NumberParameter && !_parameter.Display; }
        }

        public bool IsChoiceParameter
        {
            get { return IsParameter && _parameter is ChoiceParameter; }
        }
        public bool IsStudyChoiceParameter
        {
            get { return IsParameter && _parameter is ChoiceParameter && !_parameter.Display; }
        }

        public List<string> ParameterChoices
        {
            get
            {
                var output = new List<string>();
                if (IsChoiceParameter)
                {
                    var choiceParameter = _parameter as ChoiceParameter;
                    foreach (var kvp in choiceParameter.Choices)
                    {
                        output.Add(kvp.Value);
                    }
                }
                return output;
            }
        }

        public bool IsColorParameter
        {
            get { return IsParameter && _parameter is ColorParameter; }
        }

        public string ParameterValue
        {
            get
            {
                if (IsNumberParameter) return (_parameter as NumberParameter).Value.ToString();
                else if (IsChoiceParameter) return (_parameter as ChoiceParameter).Value.ToString();
                else if (IsBooleanParameter)  return (_parameter as BooleanParameter).Value.ToString();
                else return "";
            }

            set
            {
                if (IsNumberParameter) (_parameter as NumberParameter).FromString(value);
                else if (IsChoiceParameter) (_parameter as ChoiceParameter).FromString(value);
                else if (IsBooleanParameter) (_parameter as BooleanParameter).FromString(value);
            }
        }

        public bool IsModel
        {
            get { return !IsGroup && Group != null && Group.Contains("ML PX MODELS"); }
        }

        public bool IsRemoveSector
        {
            get { return Name.Contains("REMOVE SECTOR"); }
        }
        public bool IsRemoveIndustry
        {
            get { return Name.Contains("REMOVE INDUSTRY GROUP"); }
        }
        public bool IsRemoveSymbol
        {
            get { return Name.Contains("REMOVE SYMBOL"); }
        }

        public bool IsDefault
        {
            get { return Name.Contains("Default"); }
        }

        public string GetDescription()
        {
            string output = "";

            if (IsGroup)
            {
                output = Name;
            }
            else if (IsFundamental && Value != "NA")
            {
                if (Value.Length > 0)
                {
                    output = Name + " \u0004" + Relationship + " \u0004" + Value;
                }
                else
                {
                    output = Name + "\u0002" + Interval;
                }
            }
            else if (IsRemoveSector)
            {
                string sector = Value;

                output = Name + " \u0004" + sector;
            }
            else if (IsRemoveIndustry)
            {
                string industry = Value;

                output = Name + " \u0004" + industry;
            }
            else if (IsRemoveSymbol)
            {
                string symbol = Value;

                output = Name + " \u0004" + symbol;
            }
            else if (IsCondition)
            {
                output += Name + "\u0002" + Interval;
            }
            else if (IsUserData)
            {
                output += Name;
            }
            else if (IsIndicator || IsIndicatorInterval)
            {
                output += _indicator.Name;
                if (_children.Count > 0)
                {
                    output += "(";
                    for (var ii = 0; ii < _children.Count; ii++)
                    {
                        var child = _children[ii];
                        var parameter = child._parameter;
                        output += parameter.Name + "=" + parameter.ToString() + ((ii < _children.Count - 1) ? ";" : "");

                    }
                    output += ")";
                }
                output += "\u0002" + Interval;
            }

            return output;
        }
    }
}
