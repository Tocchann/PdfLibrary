using System.Text;

namespace PdfLibrary.Core;

internal static class PdfDocumentWriter
{
    public static void Save(PdfDocument document, Stream stream, PdfSaveOptions options)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);

        var validator = new PdfPreSaveValidator();
        var issues = validator.Validate(document);
        if (issues.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, issues.Select(item => $"{item.CheckId}: {item.Message}")));
        }

        if (options.Mode == PdfSaveMode.Append && document.OriginalBytes is null)
        {
            throw new InvalidOperationException("追記モードは読み込まれた文書に対してのみ使用できます。");
        }

        if (options.Mode == PdfSaveMode.Append && document.OriginalBytes is not null)
        {
            SaveAppend(document, stream);
            return;
        }

        SaveOverwrite(document, stream);
    }

    private static void SaveOverwrite(PdfDocument document, Stream stream)
    {
        using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
        writer.WriteLine("%PDF-1.7");
        writer.Flush();

        var offsets = new Dictionary<int, long>();
        foreach (var indirect in document.Objects)
        {
            offsets[indirect.ObjectNumber] = stream.Position;
            writer.Write(indirect.ObjectNumber);
            writer.Write(' ');
            writer.Write(indirect.GenerationNumber);
            writer.WriteLine(" obj");
            WriteValue(writer, indirect.Value);
            writer.WriteLine();
            writer.WriteLine("endobj");
            writer.Flush();
        }

        var xrefStart = stream.Position;
        writer.WriteLine("xref");
        writer.WriteLine($"0 {document.Objects.Count + 1}");
        writer.WriteLine("0000000000 65535 f ");
        foreach (var indirect in document.Objects)
        {
            var offset = offsets[indirect.ObjectNumber];
            writer.WriteLine($"{offset:0000000000} {indirect.GenerationNumber:00000} n ");
        }

        writer.WriteLine("trailer");
        writer.WriteLine("<<");
        writer.WriteLine($"/Size {document.Objects.Count + 1}");
        writer.WriteLine($"/Root {document.Catalog.ObjectNumber} {document.Catalog.GenerationNumber} R");
        if (document.Info is not null)
        {
            writer.WriteLine($"/Info {document.Info.ObjectNumber} {document.Info.GenerationNumber} R");
        }
        writer.WriteLine(">>");
        writer.WriteLine("startxref");
        writer.WriteLine(xrefStart);
        writer.WriteLine("%%EOF");
        writer.Flush();
    }

    private static void SaveAppend(PdfDocument document, Stream stream)
    {
        stream.Write(document.OriginalBytes!, 0, document.OriginalBytes!.Length);

        using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
        var offsets = new Dictionary<int, long>();
        foreach (var indirect in document.Objects)
        {
            offsets[indirect.ObjectNumber] = stream.Position;
            writer.WriteLine();
            writer.Write(indirect.ObjectNumber);
            writer.Write(' ');
            writer.Write(indirect.GenerationNumber);
            writer.WriteLine(" obj");
            WriteValue(writer, indirect.Value);
            writer.WriteLine();
            writer.WriteLine("endobj");
            writer.Flush();
        }

        var xrefStart = stream.Position;
        writer.WriteLine("xref");
        writer.WriteLine($"0 {document.Objects.Count + 1}");
        writer.WriteLine("0000000000 65535 f ");
        foreach (var indirect in document.Objects)
        {
            var offset = offsets[indirect.ObjectNumber];
            writer.WriteLine($"{offset:0000000000} {indirect.GenerationNumber:00000} n ");
        }

        writer.WriteLine("trailer");
        writer.WriteLine("<<");
        writer.WriteLine($"/Size {document.Objects.Count + 1}");
        writer.WriteLine($"/Root {document.Catalog.ObjectNumber} {document.Catalog.GenerationNumber} R");
        if (document.Info is not null)
        {
            writer.WriteLine($"/Info {document.Info.ObjectNumber} {document.Info.GenerationNumber} R");
        }
        writer.WriteLine($"/Prev {document.OriginalStartXref ?? 0}");
        writer.WriteLine(">>");
        writer.WriteLine("startxref");
        writer.WriteLine(xrefStart);
        writer.WriteLine("%%EOF");
        writer.Flush();
    }

    private static void WriteValue(StreamWriter writer, PdfValue value)
    {
        switch (value)
        {
            case PdfNull:
                writer.Write("null");
                return;
            case PdfBoolean booleanValue:
                writer.Write(booleanValue.Value ? "true" : "false");
                return;
            case PdfNumber numberValue:
                writer.Write(numberValue.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                return;
            case PdfName nameValue:
                writer.Write('/');
                writer.Write(EscapeName(nameValue.Value));
                return;
            case PdfString stringValue:
                writer.Write('(');
                writer.Write(EscapeLiteralString(stringValue.Value));
                writer.Write(')');
                return;
            case PdfReference reference:
                writer.Write(reference.ObjectNumber);
                writer.Write(' ');
                writer.Write(reference.GenerationNumber);
                writer.Write(" R");
                return;
            case PdfArray array:
                writer.Write('[');
                foreach (var item in array)
                {
                    writer.Write(' ');
                    WriteValue(writer, item);
                }
                if (array.Count > 0)
                {
                    writer.Write(' ');
                }
                writer.Write(']');
                return;
            case PdfDictionary dictionary:
                writer.Write("<<");
                foreach (var entry in dictionary)
                {
                    writer.Write(' ');
                    writer.Write('/');
                    writer.Write(EscapeName(entry.Key));
                    writer.Write(' ');
                    WriteValue(writer, entry.Value);
                }
                if (dictionary.Count > 0)
                {
                    writer.Write(' ');
                }
                writer.Write(">>");
                return;
            case PdfStream stream:
                var dictionaryCopy = stream.Dictionary.Clone();
                dictionaryCopy["Length"] = new PdfNumber(stream.Data.Length);
                WriteValue(writer, dictionaryCopy);
                writer.WriteLine();
                writer.WriteLine("stream");
                writer.Flush();
                if (writer.BaseStream is not null)
                {
                    var bytes = Encoding.UTF8.GetBytes(Environment.NewLine == "\r\n" ? "\r\n" : "\n");
                    writer.BaseStream.Write(stream.Data, 0, stream.Data.Length);
                    writer.BaseStream.Write(bytes, 0, bytes.Length);
                }
                writer.WriteLine("endstream");
                return;
            default:
                throw new NotSupportedException($"Unsupported PDF value: {value.GetType().Name}");
        }
    }

    private static string EscapeName(string value) => value.Replace("#", "#23").Replace(" ", "#20");

    private static string EscapeLiteralString(string value)
        => value.Replace(@"\", @"\\").Replace("(", @"\(").Replace(")", @"\)");
}
