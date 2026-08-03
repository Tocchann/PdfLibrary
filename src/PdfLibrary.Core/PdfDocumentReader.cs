using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PdfLibrary.Core;

internal static class PdfDocumentReader
{
    private static readonly Regex ObjectRegex = new(
        @"(?ms)(?<num>\d+)\s+(?<gen>\d+)\s+obj\s*(?<body>.*?)\s*endobj",
        RegexOptions.Compiled);

    private static readonly Regex TrailerRegex = new(
        @"(?ms)trailer\s*<<(?<body>.*?)>>",
        RegexOptions.Compiled);

    private static readonly Regex StartXrefRegex = new(
        @"(?ms)startxref\s*(?<value>\d+)",
        RegexOptions.Compiled);

    public static PdfDocument Read(byte[] bytes)
    {
        var text = Encoding.UTF8.GetString(bytes);
        var objects = new Dictionary<int, PdfIndirectObject>();

        foreach (Match match in ObjectRegex.Matches(text))
        {
            var objectNumber = int.Parse(match.Groups["num"].Value, CultureInfo.InvariantCulture);
            var generationNumber = int.Parse(match.Groups["gen"].Value, CultureInfo.InvariantCulture);
            var body = match.Groups["body"].Value.Trim();
            var value = ParseValue(body);
            objects[objectNumber] = new PdfIndirectObject(objectNumber, generationNumber, value);
        }

        var document = PdfDocument.Create();

        foreach (var indirect in objects.Values.OrderBy(item => item.ObjectNumber))
        {
            if (indirect.ObjectNumber > 2)
            {
                document.MutableObjects.Add(indirect);
            }
        }

        if (objects.TryGetValue(1, out var catalogObject) && catalogObject.Value is PdfDictionary catalog)
        {
            document.Catalog.Value = catalog;
        }

        if (objects.TryGetValue(2, out var pagesObject) && pagesObject.Value is PdfDictionary pages)
        {
            document.Pages.Value = pages;
        }

        var trailer = TrailerRegex.Matches(text).Cast<Match>().LastOrDefault();
        if (trailer is not null)
        {
            var trailerDict = ParseDictionary(trailer.Groups["body"].Value);
            document.SetEncryptionState(trailerDict.ContainsKey("Encrypt"));
            if (trailerDict.TryGetValue("Info", out var infoValue) && infoValue is PdfReference infoReference && objects.TryGetValue(infoReference.ObjectNumber, out var infoObject))
            {
                document.Info = infoObject;
            }
        }

        document.RebuildPageTree();
        document.SetOriginalState(bytes, TryReadStartXref(bytes));
        return document;
    }

    public static long? TryReadStartXref(byte[] bytes)
    {
        var text = Encoding.UTF8.GetString(bytes);
        var match = StartXrefRegex.Matches(text).Cast<Match>().LastOrDefault();
        if (match is null || !match.Success)
        {
            return null;
        }

        return long.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture);
    }

    private static PdfValue ParseValue(string text)
    {
        var parser = new TokenParser(text);
        return parser.ParseValue();
    }

    private static PdfDictionary ParseDictionary(string text)
    {
        var parser = new TokenParser("<<" + text + ">>");
        return (PdfDictionary)parser.ParseValue();
    }

    private sealed class TokenParser
    {
        private readonly string _text;
        private int _index;

        public TokenParser(string text) => _text = text;

        public PdfValue ParseValue()
        {
            SkipWhitespace();
            if (_index >= _text.Length)
            {
                return PdfNull.Instance;
            }

            if (Peek("<<"))
            {
                return ParseDictionary();
            }

            if (_text[_index] == '[')
            {
                return ParseArray();
            }

            if (_text[_index] == '(')
            {
                return new PdfString(ParseLiteralString());
            }

            if (_text[_index] == '/')
            {
                return new PdfName(ParseName());
            }

            if (Peek("true"))
            {
                _index += 4;
                return new PdfBoolean(true);
            }

            if (Peek("false"))
            {
                _index += 5;
                return new PdfBoolean(false);
            }

            if (Peek("null"))
            {
                _index += 4;
                return PdfNull.Instance;
            }

            if (char.IsDigit(_text[_index]) || _text[_index] == '-' || _text[_index] == '+')
            {
                var first = ParseNumberToken();
                SkipWhitespace();
                var save = _index;
                if (TryParseInt(first, out var objectNumber))
                {
                    var secondStart = _index;
                    var second = ParseOptionalNumberToken();
                    if (second is not null)
                    {
                        SkipWhitespace();
                        if (Peek("R"))
                        {
                            _index++;
                            if (TryParseInt(second, out var generationNumber))
                            {
                                return new PdfReference(objectNumber, generationNumber);
                            }
                        }
                    }

                    _index = secondStart;
                }

                _index = save;
                return new PdfNumber(double.Parse(first, CultureInfo.InvariantCulture));
            }

            throw new InvalidOperationException($"Unsupported PDF token near index {_index}.");
        }

        private PdfValue ParseDictionary()
        {
            Expect("<<");
            var dictionary = new PdfDictionary();
            while (!Peek(">>"))
            {
                SkipWhitespace();
                var key = ParseName();
                SkipWhitespace();
                var value = ParseValue();
                dictionary.Add(key, value);
                SkipWhitespace();
            }

            Expect(">>");
            SkipWhitespace();
            if (Peek("stream"))
            {
                Expect("stream");
                if (_index < _text.Length && _text[_index] == '\r')
                {
                    _index++;
                }

                if (_index < _text.Length && _text[_index] == '\n')
                {
                    _index++;
                }

                var end = _text.IndexOf("endstream", _index, StringComparison.Ordinal);
                if (end < 0)
                {
                    throw new InvalidOperationException("stream の終端が見つかりません。");
                }

                var dataText = _text[_index..end];
                _index = end + "endstream".Length;
                return new PdfStream(dictionary, Encoding.UTF8.GetBytes(dataText));
            }

            return dictionary;
        }

        private PdfArray ParseArray()
        {
            Expect("[");
            var array = new PdfArray();
            while (true)
            {
                SkipWhitespace();
                if (Peek("]"))
                {
                    break;
                }

                array.Add(ParseValue());
            }

            Expect("]");
            return array;
        }

        private string ParseLiteralString()
        {
            Expect("(");
            var builder = new StringBuilder();
            var nesting = 1;
            while (_index < _text.Length && nesting > 0)
            {
                var ch = _text[_index++];
                if (ch == '\\' && _index < _text.Length)
                {
                    builder.Append(_text[_index++]);
                    continue;
                }

                if (ch == '(')
                {
                    nesting++;
                    builder.Append(ch);
                    continue;
                }

                if (ch == ')')
                {
                    nesting--;
                    if (nesting > 0)
                    {
                        builder.Append(ch);
                    }
                    continue;
                }

                builder.Append(ch);
            }

            return builder.ToString();
        }

        private string ParseName()
        {
            Expect("/");
            var start = _index;
            while (_index < _text.Length && !char.IsWhiteSpace(_text[_index]) && "[]<>/()".IndexOf(_text[_index]) < 0)
            {
                _index++;
            }

            return _text[start.._index];
        }

        private string ParseNumberToken()
        {
            var start = _index;
            if (_text[_index] is '+' or '-')
            {
                _index++;
            }

            while (_index < _text.Length && (char.IsDigit(_text[_index]) || _text[_index] == '.'))
            {
                _index++;
            }

            return _text[start.._index];
        }

        private string? ParseOptionalNumberToken()
        {
            SkipWhitespace();
            if (_index >= _text.Length || !(char.IsDigit(_text[_index]) || _text[_index] is '+' or '-'))
            {
                return null;
            }

            return ParseNumberToken();
        }

        private bool TryParseInt(string text, out int value) => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

        private void SkipWhitespace()
        {
            while (_index < _text.Length && char.IsWhiteSpace(_text[_index]))
            {
                _index++;
            }
        }

        private bool Peek(string value)
            => _index + value.Length <= _text.Length && string.CompareOrdinal(_text, _index, value, 0, value.Length) == 0;

        private void Expect(string value)
        {
            if (!Peek(value))
            {
                throw new InvalidOperationException($"Expected '{value}' near index {_index}.");
            }

            _index += value.Length;
        }
    }
}
