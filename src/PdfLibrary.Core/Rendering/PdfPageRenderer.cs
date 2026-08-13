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
        var fontResolver = new PdfFontResolver(objectResolver);
        var textRenderer = new PdfTextRenderer(context, fontResolver, objectResolver, GetResourceDictionary(pageDictionary, objectResolver));
        var contentBytes = ResolveContentBytes(pageDictionary, objectResolver);
        Execute(contentBytes, context, builder, textRenderer);
        return new PdfPageRenderResult(mediaBox, context.Commands.ToArray());
    }

    private static PdfDictionary? GetResourceDictionary(PdfDictionary pageDictionary, Func<PdfReference, PdfValue?> objectResolver)
    {
        if (!pageDictionary.TryGetValue("Resources", out var resourceValue))
        {
            return null;
        }

        return resourceValue switch
        {
            PdfDictionary dict => dict,
            PdfReference reference => objectResolver(reference) as PdfDictionary,
            _ => null,
        };
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

    private static void Execute(byte[] contentBytes, PdfRenderContext context, PdfPathBuilder builder, PdfTextRenderer textRenderer)
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
                    ApplyMoveTo(builder, operands);
                    break;
                case "l":
                    ApplyLineTo(builder, operands);
                    break;
                case "c":
                    ApplyCurveTo(builder, operands);
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
                // テキスト演算子
                case "BT":
                    textRenderer.BeginText();
                    break;
                case "ET":
                    textRenderer.EndText();
                    break;
                case "Tf":
                    ApplySetFont(textRenderer, operands);
                    break;
                case "Td":
                    ApplyMoveTextPosition(textRenderer, operands);
                    break;
                case "TD":
                    ApplyMoveTextPositionWithLeading(textRenderer, operands);
                    break;
                case "T*":
                    textRenderer.MoveToNextLine();
                    break;
                case "Tm":
                    ApplySetTextMatrix(textRenderer, operands);
                    break;
                case "Tw":
                    ApplySetWordSpacing(textRenderer, operands);
                    break;
                case "Tc":
                    ApplySetCharacterSpacing(textRenderer, operands);
                    break;
                case "TL":
                    ApplySetLeading(textRenderer, operands);
                    break;
                case "Tz":
                    ApplySetHorizontalScaling(textRenderer, operands);
                    break;
                case "Tj":
                    ApplyShowText(textRenderer, operands);
                    break;
                case "TJ":
                    ApplyShowTextWithAdjustments(textRenderer, operands);
                    break;
                // 色空間・色演算子
                case "cs":
                    ApplySetNonStrokingColorSpace(context, operands);
                    break;
                case "CS":
                    ApplySetStrokingColorSpace(context, operands);
                    break;
                case "sc":
                    ApplySetNonStrokingColor(context, operands);
                    break;
                case "SC":
                    ApplySetStrokingColor(context, operands);
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

    private static void ApplyMoveTo(PdfPathBuilder builder, Stack<string> operands)
    {
        var y = PopNumber(operands);
        var x = PopNumber(operands);
        builder.MoveTo(x, y);
    }

    private static void ApplyLineTo(PdfPathBuilder builder, Stack<string> operands)
    {
        var y = PopNumber(operands);
        var x = PopNumber(operands);
        builder.LineTo(x, y);
    }

    private static void ApplyCurveTo(PdfPathBuilder builder, Stack<string> operands)
    {
        var y3 = PopNumber(operands);
        var x3 = PopNumber(operands);
        var y2 = PopNumber(operands);
        var x2 = PopNumber(operands);
        var y1 = PopNumber(operands);
        var x1 = PopNumber(operands);
        builder.CurveTo(x1, y1, x2, y2, x3, y3);
    }

    private static double PopNumber(Stack<string> operands)
    {
        if (!operands.TryPop(out var token))
        {
            throw new InvalidOperationException("演算子に必要な数値オペランドが不足しています。");
        }

        return PdfContentStreamTokenizer.ParseNumber(token);
    }

    private static string PopName(Stack<string> operands)
    {
        if (!operands.TryPop(out var token))
        {
            throw new InvalidOperationException("演算子に必要な名前オペランドが不足しています。");
        }

        return token.StartsWith('/') ? token[1..] : token;
    }

    // テキスト演算子ヘルパー
    private static void ApplySetFont(PdfTextRenderer textRenderer, Stack<string> operands)
    {
        var fontSize = PopNumber(operands);
        var fontName = PopName(operands);
        textRenderer.SetFont(fontName, fontSize);
    }

    private static void ApplyMoveTextPosition(PdfTextRenderer textRenderer, Stack<string> operands)
    {
        var ty = PopNumber(operands);
        var tx = PopNumber(operands);
        textRenderer.MoveTextPosition(tx, ty);
    }

    private static void ApplyMoveTextPositionWithLeading(PdfTextRenderer textRenderer, Stack<string> operands)
    {
        var ty = PopNumber(operands);
        var tx = PopNumber(operands);
        textRenderer.MoveTextPositionWithLeading(tx, ty);
    }

    private static void ApplySetTextMatrix(PdfTextRenderer textRenderer, Stack<string> operands)
    {
        var f = PopNumber(operands);
        var e = PopNumber(operands);
        var d = PopNumber(operands);
        var c = PopNumber(operands);
        var b = PopNumber(operands);
        var a = PopNumber(operands);
        textRenderer.SetTextMatrix(a, b, c, d, e, f);
    }

    private static void ApplySetWordSpacing(PdfTextRenderer textRenderer, Stack<string> operands)
    {
        var spacing = PopNumber(operands);
        textRenderer.SetWordSpacing(spacing);
    }

    private static void ApplySetCharacterSpacing(PdfTextRenderer textRenderer, Stack<string> operands)
    {
        var spacing = PopNumber(operands);
        textRenderer.SetCharacterSpacing(spacing);
    }

    private static void ApplySetLeading(PdfTextRenderer textRenderer, Stack<string> operands)
    {
        var leading = PopNumber(operands);
        textRenderer.SetLeading(leading);
    }

    private static void ApplySetHorizontalScaling(PdfTextRenderer textRenderer, Stack<string> operands)
    {
        var scaling = PopNumber(operands);
        textRenderer.SetHorizontalScaling(scaling);
    }

    private static void ApplyShowText(PdfTextRenderer textRenderer, Stack<string> operands)
    {
        if (!operands.TryPop(out var token))
        {
            throw new InvalidOperationException("Tj に必要なテキストオペランドが不足しています。");
        }

        byte[] textBytes;
        if (token.StartsWith('<') && token.EndsWith('>'))
        {
            var hexString = token[1..^1];
            textBytes = DecodeHexString(hexString);
        }
        else if (token.StartsWith('(') && token.EndsWith(')'))
        {
            var text = token[1..^1];
            textBytes = System.Text.Encoding.Latin1.GetBytes(text);
        }
        else
        {
            throw new InvalidOperationException($"Tj のテキストが不正な形式です: {token}");
        }

        textRenderer.ShowText(textBytes);
    }

    private static void ApplyShowTextWithAdjustments(PdfTextRenderer textRenderer, Stack<string> operands)
    {
        if (!operands.TryPop(out var token) || token != "]")
        {
            throw new InvalidOperationException("TJ のオペランドが正しくありません。");
        }

        // 配列を再構築。スタックに積まれているのは逆順。
        var array = new PdfArray();
        while (operands.Count > 0)
        {
            var item = operands.Pop();
            if (item == "[")
            {
                break;
            }

            if (double.TryParse(item, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var num))
            {
                array.Insert(0, new PdfNumber(num));
            }
            else if (item.StartsWith('(') && item.EndsWith(')'))
            {
                var text = item[1..^1];
                array.Insert(0, new PdfString(text));
            }
            else if (item.StartsWith('<') && item.EndsWith('>'))
            {
                var hexString = item[1..^1];
                array.Insert(0, new PdfString(hexString));
            }
        }

        textRenderer.ShowTextWithAdjustments(array);
    }

    private static byte[] DecodeHexString(string hexString)
    {
        if (string.IsNullOrEmpty(hexString) || hexString.Length % 2 != 0)
        {
            return [];
        }

        var result = new byte[hexString.Length / 2];
        for (var i = 0; i < hexString.Length; i += 2)
        {
            if (byte.TryParse(hexString.Substring(i, 2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var b))
            {
                result[i / 2] = b;
            }
        }

        return result;
    }

    // 色空間・色演算子ヘルパー
    private static void ApplySetStrokingColorSpace(PdfRenderContext context, Stack<string> operands)
    {
        var colorSpaceName = PopName(operands);
        var colorSpace = PdfColorSpace.Resolve(new PdfName(colorSpaceName)) ?? new PdfDeviceGrayColorSpace();
        context.StrokingColor = new PdfColor { ColorSpace = colorSpace };
    }

    private static void ApplySetNonStrokingColorSpace(PdfRenderContext context, Stack<string> operands)
    {
        var colorSpaceName = PopName(operands);
        var colorSpace = PdfColorSpace.Resolve(new PdfName(colorSpaceName)) ?? new PdfDeviceGrayColorSpace();
        context.NonStrokingColor = new PdfColor { ColorSpace = colorSpace };
    }

    private static void ApplySetStrokingColor(PdfRenderContext context, Stack<string> operands)
    {
        var components = new List<double>();
        while (operands.Count > 0)
        {
            components.Insert(0, PopNumber(operands));
        }

        try
        {
            context.StrokingColor.SetComponents(components.ToArray());
        }
        catch
        {
            // 色成分数が不一致。スキップまたは既定値を使用。
        }
    }

    private static void ApplySetNonStrokingColor(PdfRenderContext context, Stack<string> operands)
    {
        var components = new List<double>();
        while (operands.Count > 0)
        {
            components.Insert(0, PopNumber(operands));
        }

        try
        {
            context.NonStrokingColor.SetComponents(components.ToArray());
        }
        catch
        {
            // 色成分数が不一致。スキップまたは既定値を使用。
        }
    }
}
