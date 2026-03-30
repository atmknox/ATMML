using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ATMML
{
    class ModelPredictions
    {
        private Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<DateTime, double>>>> _predictions = new Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<DateTime, double>>>>();  // key = modelName:ticker:interval:ago  
        private Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<DateTime, double>>>> _actuals = new Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<DateTime, double>>>>();  // key = modelName:ticker:interval:ago  

        public void predict(string _symbol, string _interval, List<string> _modelNames, BarCache _barCache)
        {
            var tickers = new List<string>();
            tickers.Add(_symbol);

            string interval = Study.getForecastInterval(_interval, 0);

            var intervals = new List<string>();
            intervals.Add(interval);

            for (int ii = 0; ii < _modelNames.Count; ii++)
            {
                var modelName = _modelNames[ii];
                var model = MainView.GetModel(modelName);
                if (model != null)
                {
                    var pathName = @"senarios\" + model.Name + @"\" + interval + (model.UseTicker ? @"\" + MainView.ToPath(_symbol) : "");

                    var split = (100 - int.Parse(model.MLSplit.Substring(0, 2))) / 100.0;
                    var count = getBarCount(pathName); // int.Parse(model.MLMaxBars)
                    var predictionAgoCount = (int)(split * count) + 1;

                    var data = atm.getModelData(model.FeatureNames, model.Scenario, _barCache, tickers, interval, predictionAgoCount, model.MLSplit, false, false);
                    atm.saveModelData(pathName + @"\test.csv", data);
                    _predictions[modelName] = MainView.AutoMLPredict(pathName, MainView.GetSenarioLabel(model.Scenario));
                    _actuals[modelName] = MainView.getActuals(pathName);
                }
            }
        }

        int getBarCount(string pathName)
        {
            var filePath2 = pathName + @"\train.csv";
            var text = MainView.LoadUserData(filePath2);
            var rows = text.Split('\n');
            int rowCount = rows.Length - 1;
            return rowCount;
        }

        public Dictionary<DateTime, double> getPredictions(string _symbol, string _interval, string modelName)
        {
            var output = new Dictionary<DateTime, double>();

            var ticker = _symbol;
            var interval = _interval;

            if (_predictions != null && _predictions.ContainsKey(modelName))
            {
                if (_predictions[modelName].ContainsKey(ticker))
                {
                    if (_predictions[modelName][ticker].ContainsKey(interval))
                    {
                        output = _predictions[modelName][ticker][interval];
                    }
                }
            }
            return output;
        }

        public Dictionary<DateTime, double> getActuals(string _symbol, string _interval, string modelName)
        {
            var output = new Dictionary<DateTime, double>();

            var ticker = _symbol;
            var interval = _interval;

            if (_actuals != null && _actuals.ContainsKey(modelName))
            {
                if (_actuals[modelName].ContainsKey(ticker))
                {
                    if (_actuals[modelName][ticker].ContainsKey(interval))
                    {
                        output = _actuals[modelName][ticker][interval];
                    }
                }
            }
            return output;
        }
    }
}
