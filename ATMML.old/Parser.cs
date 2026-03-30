using System;
using System.Collections.Generic;

namespace ATMML
{
	//class Program
	//{
	//	static void Main(string[] args)
	//	{
	//		string input = "(true & !false) & (false | true)";
	//		Expression expression = Tokenizer.Split(input);
	//		Stack<Token> tokens = null;
	//		bool ok = Parser.InfixToPostfix(expression, out tokens);
	//		if (ok)
	//		{
	//			Token output = Parser.Evaluate(tokens);
	//			Console.WriteLine((output.TokenObject as Condition).Name);
	//		}
	//		Console.ReadKey();
	//	}

	//}

	public class Parser
	{
		public Token Evaluate(Stack<Token> input)
		{
			Token output = input.Pop();
			if (output.Type == TokenType.Operator)
			{
				Operator op = output.TokenObject as Operator;
				if (op != null)
				{
					Token arg1 = Evaluate(input);
					Token arg2 = (op.OperandCount == 2) ? Evaluate(input) : null;

					output = operate(output, arg1, arg2);
				}
			}
			else if (output.Type == TokenType.Condition)
			{
				output = calculate(output);
			}
			return output;
		}

		public virtual Token operate(Token op, Token arg1, Token arg2)
		{
			Token output = null;

			var a1 = (arg1 != null) ? arg1.TokenObject as Condition : null;
			var a2 = (arg2 != null) ? arg2.TokenObject as Condition : null;

			bool val1 = (a1 != null && a1.Name == "true");
			bool val2 = (a2 != null && a2.Name == "true");

			if (op.TokenObject == AllOperators.Find(OperatorSymbol.And))
			{
				output = new Token(TokenType.Condition, new Condition() { Name = (val1 && val2) ? "true" : "false" });
			}
			else if (op.TokenObject == AllOperators.Find(OperatorSymbol.Or))
			{
				output = new Token(TokenType.Condition, new Condition() { Name = (val1 || val2) ? "true" : "false" });
			}
			else if (op.TokenObject == AllOperators.Find(OperatorSymbol.Not))
			{
				output = new Token(TokenType.Condition, new Condition() { Name = (!val1) ? "true" : "false" });
			}

			return output;
		}

		public virtual Token calculate(Token input)
		{
			return input;
		}

		public bool InfixToPostfix(Expression inputExpression, out Stack<Token> postfixExpression)
		{
			List<Token> postfixTokens = new List<Token>();
			Stack<Token> postfixStack = new Stack<Token>();

			//process all tokens in input-expression, one by one
			foreach (Token token in inputExpression.Tokens)
			{
				if (token.Type == TokenType.Condition) //handle conditions
				{
					postfixTokens.Add(token);
				}
				else if (token.Type == TokenType.Operator) //handle operators
				{
					if (ExpressionUtility.IsOpenParenthesis(token)) //handle open-parenthesis
					{
						postfixStack.Push(token);
					}
					else if (ExpressionUtility.IsCloseParenthesis(token)) //handle close-parenthesis
					{
						//pop all operators off the stack onto the output (until left parenthesis)
						while (true)
						{
							if (postfixStack.Count == 0)
							{
								postfixExpression = null; //error: mismatched parenthesis
								return (false);
							}

							Token top = postfixStack.Pop();
							if (ExpressionUtility.IsOpenParenthesis(top)) break;
							else postfixTokens.Add(top);
						}
					}
					else //handle arithmetic operators
					{
						Operator currentOperator = AllOperators.Find(token.LinearToken);

						if (postfixStack.Count > 0)
						{
							Token top = postfixStack.Peek();
							if (ExpressionUtility.IsArithmeticOperator(top))
							{
								//'>' operator implies less precedence
								Operator stackOperator = AllOperators.Find(top.LinearToken);
								if ((currentOperator.Associativity == OperatorAssociativity.LeftToRight &&
									 currentOperator.PrecedenceLevel >= stackOperator.PrecedenceLevel) ||
									(currentOperator.Associativity == OperatorAssociativity.RightToLeft &&
									 currentOperator.PrecedenceLevel > stackOperator.PrecedenceLevel))
								{
									postfixStack.Pop();
									postfixTokens.Add(top);
								}
							}
						}

						postfixStack.Push(token); //push operator to stack
					}
				}
			}

			//after reading all tokens, pop entire stack to output
			while (postfixStack.Count > 0)
			{
				Token top = postfixStack.Pop();
				if (ExpressionUtility.IsOpenParenthesis(top) || ExpressionUtility.IsCloseParenthesis(top))
				{
					postfixExpression = null; //error: mismatched parenthesis
					return (false);
				}
				else
				{
					postfixTokens.Add(top);
				}
			}

			postfixExpression = new Stack<Token>(postfixTokens);
			return (true);
		}
	}

	public enum OperatorAssociativity
	{
		None = 0,
		LeftToRight = 1,
		RightToLeft = 2
	}

	public interface ITokenObject
	{
	}

    public class Condition : ITokenObject
    {
        public string Name { get; set; }

        public Series Data { get; set; }

		public override string ToString()
		{
			return Name;
		}
	}

	public enum OperatorSymbol
	{
		None = 0,
		And = 1,
		Or = 2,
		Not = 3,
		OpenParenthesis = 4,
		CloseParenthesis = 5,
        Add = 6
	}

	public class Operator : ITokenObject
	{
		public OperatorSymbol Symbol { get; set; }
		public string SymbolText { get; set; }
		public int OperandCount { get; set; }
		public int PrecedenceLevel { get; set; }
		public OperatorAssociativity Associativity { get; set; }

		public override string ToString()
		{
			return SymbolText;
		}
	}

	public enum TokenType
	{
		None = 0,
		Condition = 1,
		Operator = 2
	}

	public class Token
	{
		#region Properties

		public TokenType Type { get; set; }
		public ITokenObject TokenObject { get; set; }
		public int Index { get; set; }

		//returns printable token
		public string LinearToken
		{
			get
			{
				return (this.TokenObject.ToString());
			}
		}

		#endregion

		#region Constructors

		public Token()
		{
			Type = TokenType.None;
			TokenObject = null;
			Index = -1;
		}

		public Token(TokenType type, ITokenObject tokenObject)
		{
			this.Type = type;
			this.TokenObject = tokenObject;
			this.Index = -1;
		}

		#endregion

		#region Static-Methods

		public static Token Resolve(string text)
		{
			//check if operator
			Operator op = AllOperators.Find(text);
			if (op != null)
			{
				return (new Token(TokenType.Operator, op));
			}

			//this token must be a condition
			Condition condition = new Condition() { Name = text };
			return (new Token(TokenType.Condition, condition));
		}

		#endregion
	}

	public class ExpressionUtility
	{
		public static bool IsOpenParenthesis(Token token)
		{ 
			return (token.Type == TokenType.Operator && token.LinearToken.Equals(AllOperators.Find(OperatorSymbol.OpenParenthesis).SymbolText));
		}

		public static bool IsCloseParenthesis(Token token)
		{
			return (token.Type == TokenType.Operator && token.LinearToken.Equals(AllOperators.Find(OperatorSymbol.CloseParenthesis).SymbolText));
		}

		public static bool IsArithmeticOperator(Token token)
		{
			return (token.Type == TokenType.Operator && !IsOpenParenthesis(token) && !IsCloseParenthesis(token));
		}

		//checks whether the input expression contains only operators.
		public static bool IsArithmeticExpression(Expression expression)
		{
			foreach (Token token in expression.Tokens)
			{
				if (!(token.Type == TokenType.Operator))
				{
					return (false);
				}
			}

			return (true);
		}

		public static Token CreateOperatorToken(char op)
		{
			Operator operatorObject = AllOperators.Find("" + op);
			return (new Token(TokenType.Operator, operatorObject));
		}
	}

	public static class AllOperators
	{
		public static List<Operator> Operators { get; set; }

		#region Constructors

		static AllOperators()
		{
			Operators = new List<Operator>();

			Operators.Add(new Operator()
			{
				Symbol = OperatorSymbol.Not,
				SymbolText = "!",
				OperandCount = 1,
				PrecedenceLevel = 2,
				Associativity = OperatorAssociativity.LeftToRight
			});

			Operators.Add(new Operator()
			{
				Symbol = OperatorSymbol.And,
				SymbolText = "&",
				OperandCount = 2,
				PrecedenceLevel = 3,
				Associativity = OperatorAssociativity.LeftToRight
			});

			Operators.Add(new Operator()
			{
				Symbol = OperatorSymbol.Or,
				SymbolText = "|",
				OperandCount = 2,
				PrecedenceLevel = 4,
				Associativity = OperatorAssociativity.LeftToRight
			});

            Operators.Add(new Operator()
            {
                Symbol = OperatorSymbol.Add,
                SymbolText = "+",
                OperandCount = 2,
                PrecedenceLevel = 3,
                Associativity = OperatorAssociativity.LeftToRight
            });

            Operators.Add(new Operator()
			{
				Symbol = OperatorSymbol.OpenParenthesis,
				SymbolText = "(",
				OperandCount = 0,
				PrecedenceLevel = 1,
				Associativity = OperatorAssociativity.LeftToRight
			});

			Operators.Add(new Operator()
			{
				Symbol = OperatorSymbol.CloseParenthesis,
				SymbolText = ")",
				OperandCount = 0,
				PrecedenceLevel = 1,
				Associativity = OperatorAssociativity.LeftToRight
			});
		}

		#endregion

		#region Methods

		public static Operator Find(OperatorSymbol symbol)
		{
			foreach (Operator op in Operators)
			{
				if (op.Symbol == symbol)
				{
					return (op);
				}
			}
			return (null);
		}

		public static Operator Find(string symbolText)
		{
			foreach (Operator op in Operators)
			{
				if (op.SymbolText.Equals(symbolText))
				{
					return (op);
				}
			}
			return (null);
		}

		#endregion
	}

	public class Expression
	{
		public List<Token> Tokens { get; set; }
	}

	public class Tokenizer
	{
		public static Expression Split(string expressionString)
		{
			List<Token> tokens = new List<Token>();

			//read the input-expression letter-by-letter and build tokens
			string alphaNumericString = string.Empty;
			for (int index = 0; index < expressionString.Length; index++)
			{
				char currentChar = expressionString[index];

				if (AllOperators.Find("" + currentChar) != null) //if operator
				{
					string text1 = alphaNumericString.Trim();
					if (text1.Length > 0)
					{
						tokens.Add(Token.Resolve(text1));
						alphaNumericString = string.Empty; //reset token string
					}

					tokens.Add(ExpressionUtility.CreateOperatorToken(currentChar));
				}
				else if (Char.IsLetterOrDigit(currentChar) || currentChar == '.' || currentChar == ' ')
				{
					//if alphabet or digit or dot
					alphaNumericString += currentChar;
				}
			}

			//check if any token at last
			string text2 = alphaNumericString.Trim();
			if (text2.Length > 0)
			{
				tokens.Add(Token.Resolve(text2));
			}

			return (new Expression() { Tokens = tokens });
		}
	}
}
