using System.Text;

namespace PamGateway.Api;

public static class LabelExpressionEvaluator
{
    public static bool Evaluate(string expression, IReadOnlyDictionary<string, string>? labels)
    {
        if (labels is null || labels.Count == 0 || string.IsNullOrWhiteSpace(expression))
        {
            return false;
        }

        try
        {
            var parser = new Parser(expression);
            return parser.ParseExpression().Evaluate(labels);
        }
        catch
        {
            return false;
        }
    }

    private sealed class Parser
    {
        private readonly List<Token> _tokens;
        private int _index;

        public Parser(string input)
        {
            _tokens = new Lexer(input).Tokenize();
        }

        public Expr ParseExpression() => ParseOr();

        private Expr ParseOr()
        {
            var left = ParseAnd();
            while (Match(TokenType.Or))
            {
                var right = ParseAnd();
                left = new OrExpr(left, right);
            }

            return left;
        }

        private Expr ParseAnd()
        {
            var left = ParseUnary();
            while (Match(TokenType.And))
            {
                var right = ParseUnary();
                left = new AndExpr(left, right);
            }

            return left;
        }

        private Expr ParseUnary()
        {
            if (Match(TokenType.Not))
            {
                return new NotExpr(ParseUnary());
            }

            return ParsePrimary();
        }

        private Expr ParsePrimary()
        {
            if (Match(TokenType.LeftParen))
            {
                var expr = ParseExpression();
                Expect(TokenType.RightParen);
                return expr;
            }

            var key = Expect(TokenType.Identifier).Text;
            if (Match(TokenType.Equal))
            {
                var value = Expect(TokenType.Identifier, TokenType.String).Text;
                return new CompareExpr(key, value, false);
            }

            if (Match(TokenType.NotEqual))
            {
                var value = Expect(TokenType.Identifier, TokenType.String).Text;
                return new CompareExpr(key, value, true);
            }

            return new ExistsExpr(key);
        }

        private bool Match(TokenType type)
        {
            if (Peek().Type != type)
            {
                return false;
            }

            _index++;
            return true;
        }

        private Token Expect(params TokenType[] types)
        {
            var token = Peek();
            if (!types.Contains(token.Type))
            {
                throw new InvalidOperationException($"Unexpected token {token.Type}");
            }

            _index++;
            return token;
        }

        private Token Peek()
        {
            if (_index >= _tokens.Count)
            {
                return new Token(TokenType.End, string.Empty);
            }

            return _tokens[_index];
        }
    }

    private sealed class Lexer
    {
        private readonly string _input;
        private int _index;

        public Lexer(string input)
        {
            _input = input;
        }

        public List<Token> Tokenize()
        {
            var tokens = new List<Token>();
            while (_index < _input.Length)
            {
                var ch = _input[_index];
                if (char.IsWhiteSpace(ch))
                {
                    _index++;
                    continue;
                }

                if (ch == '&' && PeekNext() == '&')
                {
                    tokens.Add(new Token(TokenType.And, "&&"));
                    _index += 2;
                    continue;
                }

                if (ch == '|' && PeekNext() == '|')
                {
                    tokens.Add(new Token(TokenType.Or, "||"));
                    _index += 2;
                    continue;
                }

                if (ch == '!' && PeekNext() == '=')
                {
                    tokens.Add(new Token(TokenType.NotEqual, "!="));
                    _index += 2;
                    continue;
                }

                if (ch == '!')
                {
                    tokens.Add(new Token(TokenType.Not, "!"));
                    _index++;
                    continue;
                }

                if (ch == '=')
                {
                    tokens.Add(new Token(TokenType.Equal, "="));
                    _index++;
                    continue;
                }

                if (ch == '(')
                {
                    tokens.Add(new Token(TokenType.LeftParen, "("));
                    _index++;
                    continue;
                }

                if (ch == ')')
                {
                    tokens.Add(new Token(TokenType.RightParen, ")"));
                    _index++;
                    continue;
                }

                if (ch == '"')
                {
                    tokens.Add(new Token(TokenType.String, ReadString()));
                    continue;
                }

                tokens.Add(new Token(TokenType.Identifier, ReadIdentifier()));
            }

            tokens.Add(new Token(TokenType.End, string.Empty));
            return tokens;
        }

        private char PeekNext() => _index + 1 < _input.Length ? _input[_index + 1] : '\0';

        private string ReadIdentifier()
        {
            var start = _index;
            while (_index < _input.Length)
            {
                var ch = _input[_index];
                if (!(char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' || ch == '.' || ch == ':'))
                {
                    break;
                }

                _index++;
            }

            return _input[start.._index];
        }

        private string ReadString()
        {
            _index++;
            var sb = new StringBuilder();
            while (_index < _input.Length)
            {
                var ch = _input[_index++];
                if (ch == '"')
                {
                    break;
                }

                if (ch == '\\' && _index < _input.Length)
                {
                    var next = _input[_index++];
                    sb.Append(next);
                    continue;
                }

                sb.Append(ch);
            }

            return sb.ToString();
        }
    }

    private enum TokenType
    {
        Identifier,
        String,
        And,
        Or,
        Not,
        Equal,
        NotEqual,
        LeftParen,
        RightParen,
        End
    }

    private sealed record Token(TokenType Type, string Text);

    private abstract class Expr
    {
        public abstract bool Evaluate(IReadOnlyDictionary<string, string> labels);
    }

    private sealed class AndExpr : Expr
    {
        private readonly Expr _left;
        private readonly Expr _right;

        public AndExpr(Expr left, Expr right)
        {
            _left = left;
            _right = right;
        }

        public override bool Evaluate(IReadOnlyDictionary<string, string> labels)
            => _left.Evaluate(labels) && _right.Evaluate(labels);
    }

    private sealed class OrExpr : Expr
    {
        private readonly Expr _left;
        private readonly Expr _right;

        public OrExpr(Expr left, Expr right)
        {
            _left = left;
            _right = right;
        }

        public override bool Evaluate(IReadOnlyDictionary<string, string> labels)
            => _left.Evaluate(labels) || _right.Evaluate(labels);
    }

    private sealed class NotExpr : Expr
    {
        private readonly Expr _inner;

        public NotExpr(Expr inner)
        {
            _inner = inner;
        }

        public override bool Evaluate(IReadOnlyDictionary<string, string> labels)
            => !_inner.Evaluate(labels);
    }

    private sealed class CompareExpr : Expr
    {
        private readonly string _key;
        private readonly string _value;
        private readonly bool _negate;

        public CompareExpr(string key, string value, bool negate)
        {
            _key = key;
            _value = value;
            _negate = negate;
        }

        public override bool Evaluate(IReadOnlyDictionary<string, string> labels)
        {
            if (!labels.TryGetValue(_key, out var actual))
            {
                return false;
            }

            var equals = string.Equals(actual, _value, StringComparison.OrdinalIgnoreCase);
            return _negate ? !equals : equals;
        }
    }

    private sealed class ExistsExpr : Expr
    {
        private readonly string _key;

        public ExistsExpr(string key)
        {
            _key = key;
        }

        public override bool Evaluate(IReadOnlyDictionary<string, string> labels)
            => labels.ContainsKey(_key);
    }
}
