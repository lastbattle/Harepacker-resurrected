using System;
using System.Globalization;

namespace HaCreator.GUI.Skill;

public sealed record SkillFormulaResult(bool Succeeded, double Value, string Error)
{
    public static SkillFormulaResult Success(double value) => new(true, value, null);
    public static SkillFormulaResult Failure(string error) => new(false, 0, error);
}

/// <summary>Side-effect-free evaluator for the post-Big Bang skill expression grammar.</summary>
public static class SkillFormulaEvaluator
{
    public static SkillFormulaResult Evaluate(string expression, int level)
    {
        try
        {
            var parser = new Parser(expression ?? string.Empty, level);
            double value = parser.ParseExpression();
            parser.ExpectEnd();
            if (double.IsInfinity(value) || double.IsNaN(value))
                return SkillFormulaResult.Failure("The formula produced a non-finite value.");
            return SkillFormulaResult.Success(value);
        }
        catch (FormulaException exception)
        {
            return SkillFormulaResult.Failure(exception.Message);
        }
    }

    private sealed class Parser
    {
        private readonly string _text;
        private readonly int _level;
        private int _position;

        public Parser(string text, int level) { _text = text; _level = level; }

        public double ParseExpression()
        {
            double value = ParseTerm();
            while (true)
            {
                SkipSpace();
                if (Take('+')) value += ParseTerm();
                else if (Take('-')) value -= ParseTerm();
                else return value;
            }
        }

        private double ParseTerm()
        {
            double value = ParseUnary();
            while (true)
            {
                SkipSpace();
                if (Take('*')) value *= ParseUnary();
                else if (Take('/'))
                {
                    double divisor = ParseUnary();
                    if (divisor == 0) throw Error("Division by zero.");
                    value /= divisor;
                }
                else return value;
            }
        }

        private double ParseUnary()
        {
            SkipSpace();
            if (Take('+')) return ParseUnary();
            if (Take('-')) return -ParseUnary();
            return ParsePrimary();
        }

        private double ParsePrimary()
        {
            SkipSpace();
            if (Take('('))
            {
                double value = ParseExpression();
                SkipSpace();
                if (!Take(')')) throw Error("Expected ')'.");
                return value;
            }
            if (_position < _text.Length && (_text[_position] == 'x' || _text[_position] == 'X'))
            {
                _position++;
                return _level;
            }
            if (_position < _text.Length && (_text[_position] == 'u' || _text[_position] == 'd'))
            {
                char function = char.ToLowerInvariant(_text[_position++]);
                SkipSpace();
                if (!Take('(')) throw Error($"Expected '(' after {function}.");
                double value = ParseExpression();
                SkipSpace();
                if (!Take(')')) throw Error("Expected ')'.");
                return function == 'u' ? Math.Ceiling(value) : Math.Floor(value);
            }

            int start = _position;
            while (_position < _text.Length && char.IsDigit(_text[_position])) _position++;
            if (start == _position) throw Error("Expected a number, x, u(...), d(...), or parenthesized expression.");
            string token = _text[start.._position];
            if (!double.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out double number))
                throw Error($"Invalid number '{token}'.");
            return number;
        }

        public void ExpectEnd()
        {
            SkipSpace();
            if (_position != _text.Length) throw Error($"Unexpected token '{_text[_position]}'.");
        }

        private bool Take(char value)
        {
            if (_position >= _text.Length || _text[_position] != value) return false;
            _position++;
            return true;
        }

        private void SkipSpace() { while (_position < _text.Length && char.IsWhiteSpace(_text[_position])) _position++; }
        private FormulaException Error(string message) => new($"{message} (position {_position + 1})");
    }

    private sealed class FormulaException : Exception { public FormulaException(string message) : base(message) { } }
}
