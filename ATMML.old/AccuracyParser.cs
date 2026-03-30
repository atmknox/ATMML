using System;
using System.Collections.Generic;

namespace ATMML
{
    class AccuracyParser : Parser
    {

        private BarCache _barCache;
        private string _interval;
        private Dictionary<string, object> _referenceData;
        private string _currentSymbol;

        public AccuracyParser(BarCache barCache, string interval, Dictionary<string, object> referenceData)
        {
            _barCache = barCache;
            _interval = interval;
            _referenceData = referenceData;
        }

        public override Token operate(Token op, Token arg1, Token arg2)
        {
            Token output = null;

            var a1 = (arg1 != null) ? arg1.TokenObject as Condition : null;
            var a2 = (arg2 != null) ? arg2.TokenObject as Condition : null;

            Series val1 = (a1 != null) ? a1.Data : null;
            Series val2 = (a2 != null) ? a2.Data : null; 

            if (op.TokenObject == AllOperators.Find(OperatorSymbol.And))
            {
                if (val1 != null && val2 != null)
                {
                    output = new Token(TokenType.Condition, new Condition() { Data = val1.And(val2) });
                }
            }
            else if (op.TokenObject == AllOperators.Find(OperatorSymbol.Or))
            {
                if (val1 != null && val2 != null)
                {
                    output = new Token(TokenType.Condition, new Condition() { Data = val1.Or(val2) });
                }
            }
            else if (op.TokenObject == AllOperators.Find(OperatorSymbol.Not))
            {
                if (val1 != null)
                {
                    output = new Token(TokenType.Condition, new Condition() { Data = val1 < 0.5 });
                }
            }
            else if (op.TokenObject == AllOperators.Find(OperatorSymbol.Add))
            {
                if (val1 != null && val2 != null)
                {
                    output = new Token(TokenType.Condition, new Condition() { Data = val1 + val2 });
                }
            }

            return output;
        }

        public override Token calculate(Token input)
        {
            string condition = input.TokenObject.ToString();
            string conditionName = getConditionName(condition);
            string interval = getConditionInterval(condition);
            string[] intervalList = { interval, Study.getForecastInterval(interval, 1) };

            Series signals = getSignals(_currentSymbol, conditionName, intervalList);

            Token output = new Token(TokenType.Condition, new Condition() { Name = input.ToString(), Data = signals });

            return output;
        }

        public Series GetSignals(string symbol, string input)
        {
            Series output = null;
            _currentSymbol = symbol;
            Expression expression = Tokenizer.Split(input);
            Stack<Token> tokens = null;
            bool ok = InfixToPostfix(expression, out tokens);
            if (ok)
            {
                Token token1 = Evaluate(tokens);
                if (token1 != null)
                {
                    Condition token2 = token1.TokenObject as Condition;
                    if (token2 != null)
                    {
                        output = token2.Data;
                    }
                }
                
            }
            return output;
        }

        private Series getSignals(string symbol, string conditionName, string[] intervalList)
        {
            Series output = null;

            bool ok = true;
            Dictionary<string, List<DateTime>> times = new Dictionary<string, List<DateTime>>();
            Dictionary<string, Series[]> bars = new Dictionary<string, Series[]>();
            for (int ii = 0; ii < intervalList.Length; ii++)
            {
                string interval = intervalList[ii];
                times[interval] = (_barCache.GetTimes(symbol, interval, 0));
                bars[interval] = _barCache.GetSeries(symbol, interval, new string[] { "Open", "High", "Low", "Close" }, 0);
                if (times[interval] == null || times[interval].Count == 0)
                {
                    ok = false;
                    break;
                }
            }

            if (ok)
            {
                int barCount = times[intervalList[0]].Count;
                output = Conditions.Calculate(conditionName, symbol, intervalList, barCount, times, bars, _referenceData);
            }

            // sync
            if (output != null && intervalList[0] != _interval)
            {
                var time1 = times[intervalList[0]];
                var time2 = _barCache.GetTimes(symbol, _interval, 0);
                output = atm.sync(output, intervalList[0], _interval, time1, time2);
            }

            return output;
        }

        private string getConditionName(string input)
        {
            int index1 = 0;
            int index2 = input.IndexOf(" on ");
            string name = input.Substring(index1, index2 - index1);
            return name;
        }

        private string getConditionInterval(string input)
        {
            string output = _interval;
            int index1 = input.IndexOf(" on ");
            int index2 = input.Length;
            string name = input.Substring(index1 + 4, index2 - index1 - 4);
            if (name == "Mid Term") output = Study.getForecastInterval(_interval, 1);
            else if (name == "Long Term") output = Study.getForecastInterval(_interval, 2);
            return output;
        }

    }
}
