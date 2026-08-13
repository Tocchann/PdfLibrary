using System.Globalization;
using System.Text;

namespace PdfLibrary.Core.Rendering;

internal static class PdfContentStreamTokenizer
{
    public static IReadOnlyList<string> Tokenize(byte[] contentBytes)
    {
        ArgumentNullException.ThrowIfNull(contentBytes);

        var text = Encoding.ASCII.GetString(contentBytes);
        var tokens = new List<string>();
        var builder = new StringBuilder();
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch))
            {
                Flush(builder, tokens);
                continue;
            }

            builder.Append(ch);
        }

        Flush(builder, tokens);
        return tokens;
    }

    public static double ParseNumber(string token)
        => double.Parse(token, CultureInfo.InvariantCulture);

    private static void Flush(StringBuilder builder, List<string> tokens)
    {
        if (builder.Length == 0)
        {
            return;
        }

        tokens.Add(builder.ToString());
        builder.Clear();
    }
}
