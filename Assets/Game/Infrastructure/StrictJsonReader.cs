using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Game.Infrastructure
{
    /// <summary>Small dependency-free JSON reader used for authored array-based map coordinates.</summary>
    internal static class StrictJsonReader
    {
        public static IDictionary<string, object> ParseObject(string json, string sourceName)
        {
            try
            {
                var parser = new Parser(json ?? throw new ArgumentNullException(nameof(json)));
                var value = parser.ParseValue();
                parser.RequireEnd();
                if (!(value is IDictionary<string, object> result))
                    throw new InvalidDataException("The JSON root must be an object.");
                return result;
            }
            catch (Exception exception) when (!(exception is InvalidDataException))
            {
                throw new InvalidDataException($"Invalid JSON in {sourceName}.", exception);
            }
            catch (InvalidDataException exception)
            {
                throw new InvalidDataException($"Invalid JSON in {sourceName}: {exception.Message}", exception);
            }
        }

        private sealed class Parser
        {
            private readonly string text;
            private int index;

            public Parser(string text) { this.text = text; }

            public object ParseValue()
            {
                SkipWhiteSpace();
                if (index >= text.Length) throw Error("Unexpected end of input.");
                switch (text[index])
                {
                    case '{': return ParseObjectValue();
                    case '[': return ParseArray();
                    case '"': return ParseString();
                    case 't': RequireLiteral("true"); return true;
                    case 'f': RequireLiteral("false"); return false;
                    case 'n': RequireLiteral("null"); return null;
                    default:
                        if (text[index] == '-' || char.IsDigit(text[index])) return ParseNumber();
                        throw Error($"Unexpected character '{text[index]}'.");
                }
            }

            public void RequireEnd()
            {
                SkipWhiteSpace();
                if (index != text.Length) throw Error("Trailing content is not allowed.");
            }

            private IDictionary<string, object> ParseObjectValue()
            {
                index++;
                var result = new Dictionary<string, object>(StringComparer.Ordinal);
                SkipWhiteSpace();
                if (Consume('}')) return result;
                while (true)
                {
                    SkipWhiteSpace();
                    if (index >= text.Length || text[index] != '"') throw Error("Object key must be a string.");
                    var key = ParseString();
                    if (!result.TryAdd(key, null)) throw Error($"Duplicate property '{key}'.");
                    SkipWhiteSpace();
                    Require(':');
                    result[key] = ParseValue();
                    SkipWhiteSpace();
                    if (Consume('}')) return result;
                    Require(',');
                }
            }

            private IList<object> ParseArray()
            {
                index++;
                var result = new List<object>();
                SkipWhiteSpace();
                if (Consume(']')) return result;
                while (true)
                {
                    result.Add(ParseValue());
                    SkipWhiteSpace();
                    if (Consume(']')) return result;
                    Require(',');
                }
            }

            private string ParseString()
            {
                Require('"');
                var builder = new StringBuilder();
                while (index < text.Length)
                {
                    var character = text[index++];
                    if (character == '"') return builder.ToString();
                    if (character < 0x20) throw Error("Control characters are not allowed in strings.");
                    if (character != '\\')
                    {
                        builder.Append(character);
                        continue;
                    }
                    if (index >= text.Length) throw Error("Incomplete string escape.");
                    switch (text[index++])
                    {
                        case '"': builder.Append('"'); break;
                        case '\\': builder.Append('\\'); break;
                        case '/': builder.Append('/'); break;
                        case 'b': builder.Append('\b'); break;
                        case 'f': builder.Append('\f'); break;
                        case 'n': builder.Append('\n'); break;
                        case 'r': builder.Append('\r'); break;
                        case 't': builder.Append('\t'); break;
                        case 'u': builder.Append(ParseUnicode()); break;
                        default: throw Error("Unknown string escape.");
                    }
                }
                throw Error("Unterminated string.");
            }

            private char ParseUnicode()
            {
                if (index + 4 > text.Length) throw Error("Incomplete unicode escape.");
                if (!ushort.TryParse(text.Substring(index, 4), NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture, out var value))
                    throw Error("Invalid unicode escape.");
                index += 4;
                return (char)value;
            }

            private double ParseNumber()
            {
                var start = index;
                if (Consume('-') && index >= text.Length) throw Error("Incomplete number.");
                if (Consume('0'))
                {
                    if (index < text.Length && char.IsDigit(text[index])) throw Error("Leading zero in number.");
                }
                else
                {
                    RequireDigit();
                    while (index < text.Length && char.IsDigit(text[index])) index++;
                }
                if (Consume('.'))
                {
                    RequireDigit();
                    while (index < text.Length && char.IsDigit(text[index])) index++;
                }
                if (index < text.Length && (text[index] == 'e' || text[index] == 'E'))
                {
                    index++;
                    if (index < text.Length && (text[index] == '+' || text[index] == '-')) index++;
                    RequireDigit();
                    while (index < text.Length && char.IsDigit(text[index])) index++;
                }
                if (!double.TryParse(text.Substring(start, index - start), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out var result) || double.IsNaN(result) || double.IsInfinity(result))
                    throw Error("Invalid finite number.");
                return result;
            }

            private void RequireDigit()
            {
                if (index >= text.Length || !char.IsDigit(text[index])) throw Error("Digit expected.");
            }

            private void RequireLiteral(string literal)
            {
                if (index + literal.Length > text.Length ||
                    string.CompareOrdinal(text, index, literal, 0, literal.Length) != 0)
                    throw Error($"Expected '{literal}'.");
                index += literal.Length;
            }

            private void SkipWhiteSpace()
            {
                while (index < text.Length && char.IsWhiteSpace(text[index])) index++;
            }

            private bool Consume(char expected)
            {
                if (index >= text.Length || text[index] != expected) return false;
                index++;
                return true;
            }

            private void Require(char expected)
            {
                if (!Consume(expected)) throw Error($"Expected '{expected}'.");
            }

            private InvalidDataException Error(string message) =>
                new InvalidDataException($"{message} Offset {index}.");
        }
    }
}
