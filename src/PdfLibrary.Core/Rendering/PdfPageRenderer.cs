namespace PdfLibrary.Core.Rendering;

public sealed class PdfPageRenderer
{
    public PdfPageRenderResult Render(PdfDictionary pageDictionary, Func<PdfReference, PdfValue?> objectResolver)
    {
        ArgumentNullException.ThrowIfNull(pageDictionary);
        ArgumentNullException.ThrowIfNull(objectResolver);

        if (!pageDictionary.TryGetValue("MediaBox", out var mediaBoxValue) || mediaBoxValue is not PdfArray mediaBox)
        {
            throw new InvalidOperationException("ページ辞書に MediaBox が存在しません。");
        }

        var context = new PdfRenderContext(mediaBox);
        var builder = new PdfPathBuilder(context);
        var contentBytes = ResolveContentBytes(pageDictionary, objectResolver);
        Execute(contentBytes, context, builder);
        return new PdfPageRenderResult(mediaBox, context.Commands.ToArray());
    }

    private static byte[] ResolveContentBytes(PdfDictionary pageDictionary, Func<PdfReference, PdfValue?> objectResolver)
    {
        if (!pageDictionary.TryGetValue("Contents", out var contentsValue))
        {
            return [];
        }

        return contentsValue switch
        {
            PdfStream stream => stream.Data.ToArray(),
            PdfReference reference => ResolveStream(reference, objectResolver).Data.ToArray(),
            PdfArray array => ResolveArray(array, objectResolver),
            _ => throw new InvalidOperationException("Contents の型が未対応です。"),
        };
    }

    private static byte[] ResolveArray(PdfArray array, Func<PdfReference, PdfValue?> objectResolver)
    {
        using var stream = new MemoryStream();
        for (var i = 0; i < array.Count; i++)
        {
            var item = array[i];
            var contentStream = item switch
            {
                PdfStream directStream => directStream,
                PdfReference reference => ResolveStream(reference, objectResolver),
                _ => throw new InvalidOperationException("Contents 配列要素は stream または参照である必要があります。"),
            };

            stream.Write(contentStream.Data, 0, contentStream.Data.Length);
            if (i + 1 < array.Count)
            {
                stream.WriteByte((byte)'\n');
            }
        }

        return stream.ToArray();
    }

    private static PdfStream ResolveStream(PdfReference reference, Func<PdfReference, PdfValue?> objectResolver)
    {
        var resolved = objectResolver(reference);
        if (resolved is not PdfStream stream)
        {
            throw new InvalidOperationException($"参照先 {reference.ObjectNumber} {reference.GenerationNumber} R は stream ではありません。");
        }

        return stream;
    }

    private static void Execute(byte[] contentBytes, PdfRenderContext context, PdfPathBuilder builder)
    {
        var tokens = PdfContentStreamTokenizer.Tokenize(contentBytes);
        var operands = new Stack<string>();
        foreach (var token in tokens)
        {
            switch (token)
            {
                case "q":
                    context.SaveGraphicsState();
                    break;
                case "Q":
                    context.RestoreGraphicsState();
                    break;
                case "cm":
                    ApplyMatrix(context, operands);
                    break;
                case "m":
                    builder.MoveTo(PopNumber(operands), PopNumber(operands));
                    break;
                case "l":
                    builder.LineTo(PopNumber(operands), PopNumber(operands));
                    break;
                case "c":
                    builder.CurveTo(
                        PopNumber(operands),
                        PopNumber(operands),
                        PopNumber(operands),
                        PopNumber(operands),
                        PopNumber(operands),
                        PopNumber(operands));
                    break;
                case "h":
                    builder.ClosePath();
                    break;
                case "re":
                    ApplyRectangle(builder, operands);
                    break;
                case "S":
                    context.RecordPath(PdfPathPaintingOperator.Stroke);
                    break;
                case "f":
                    context.RecordPath(PdfPathPaintingOperator.Fill);
                    break;
                case "B":
                    context.RecordPath(PdfPathPaintingOperator.FillAndStroke);
                    break;
                default:
                    if (double.TryParse(token, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
                    {
                        operands.Push(token);
                        break;
                    }

                    throw new NotSupportedException($"未対応の演算子です: {token}");
            }
        }
    }

    private static void ApplyMatrix(PdfRenderContext context, Stack<string> operands)
    {
        var f = PopNumber(operands);
        var e = PopNumber(operands);
        var d = PopNumber(operands);
        var c = PopNumber(operands);
        var b = PopNumber(operands);
        var a = PopNumber(operands);
        context.GraphicsState.ConcatMatrix(new PdfRenderMatrix(a, b, c, d, e, f));
    }

    private static void ApplyRectangle(PdfPathBuilder builder, Stack<string> operands)
    {
        var height = PopNumber(operands);
        var width = PopNumber(operands);
        var y = PopNumber(operands);
        var x = PopNumber(operands);
        builder.Rectangle(x, y, width, height);
    }

    private static double PopNumber(Stack<string> operands)
    {
        if (!operands.TryPop(out var token))
        {
            throw new InvalidOperationException("演算子に必要な数値オペランドが不足しています。");
        }

        return PdfContentStreamTokenizer.ParseNumber(token);
    }
}
