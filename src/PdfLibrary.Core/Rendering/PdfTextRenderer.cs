namespace PdfLibrary.Core.Rendering;

/// <summary>
/// テキスト描画演算子を処理するクラス。
/// BT, ET, Tf, Td, TJ, Tj 等を実装。
/// </summary>
public sealed class PdfTextRenderer
{
    private readonly PdfRenderContext _context;
    private readonly PdfFontResolver _fontResolver;
    private readonly Func<PdfReference, PdfValue?> _objectResolver;
    private readonly PdfDictionary? _resourceDictionary;

    public PdfTextRenderer(
        PdfRenderContext context,
        PdfFontResolver fontResolver,
        Func<PdfReference, PdfValue?> objectResolver,
        PdfDictionary? resourceDictionary)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(fontResolver);
        ArgumentNullException.ThrowIfNull(objectResolver);

        _context = context;
        _fontResolver = fontResolver;
        _objectResolver = objectResolver;
        _resourceDictionary = resourceDictionary;
    }

    /// <summary>
    /// BT (Begin Text) - テキストオブジェクトの開始。
    /// テキスト行列と行列をリセット。
    /// </summary>
    public void BeginText()
    {
        _context.TextState.TextLineMatrix = PdfRenderMatrix.Identity;
        _context.TextState.TextMatrix = PdfRenderMatrix.Identity;
    }

    /// <summary>
    /// ET (End Text) - テキストオブジェクトの終了。
    /// 特に処理は不要。
    /// </summary>
    public void EndText()
    {
        // テキストレンダリング終了。次の BT まで演算子は無効。
    }

    /// <summary>
    /// Tf (Set Font) - フォントとサイズを設定。
    /// 形式: fontName fontSize Tf
    /// </summary>
    /// <param name="fontName">フォント参照名（例: "F1"）。</param>
    /// <param name="fontSize">フォントサイズ（pt）。</param>
    public void SetFont(string fontName, double fontSize)
    {
        ArgumentNullException.ThrowIfNull(fontName);

        if (fontSize <= 0)
        {
            throw new InvalidOperationException($"フォントサイズは正の値である必要があります: {fontSize}");
        }

        if (_resourceDictionary == null)
        {
            throw new InvalidOperationException("リソース辞書が設定されていません。");
        }

        var fontDict = _fontResolver.ResolveFontByName(_resourceDictionary, fontName);
        if (fontDict == null)
        {
            throw new InvalidOperationException($"フォント '{fontName}' をリソースから解決できません。");
        }

        _context.TextState.FontDictionary = fontDict;
        _context.TextState.FontSize = fontSize;
    }

    /// <summary>
    /// Td (Text Position) - テキスト行列を移動（相対指定）。
    /// 形式: tx ty Td
    /// 効果: 新しいテキスト行行列 = [1 0 0 1 tx ty] * 前の Tlm
    /// </summary>
    /// <param name="tx">水平移動（単位: 1/1000 フォントサイズ）。</param>
    /// <param name="ty">垂直移動（単位: 1/1000 フォントサイズ）。</param>
    public void MoveTextPosition(double tx, double ty)
    {
        var translationMatrix = PdfRenderMatrix.Identity with { E = tx, F = ty };
        _context.TextState.TextLineMatrix = translationMatrix.Multiply(_context.TextState.TextLineMatrix);
        _context.TextState.TextMatrix = _context.TextState.TextLineMatrix;
    }

    /// <summary>
    /// TD (Text Position and Leading) - テキスト行列を移動し、行間を設定。
    /// 形式: tx ty TD
    /// 効果: Td と同じ + TL に -ty を設定
    /// </summary>
    /// <param name="tx">水平移動。</param>
    /// <param name="ty">垂直移動。</param>
    public void MoveTextPositionWithLeading(double tx, double ty)
    {
        _context.TextState.Leading = -ty;
        MoveTextPosition(tx, ty);
    }

    /// <summary>
    /// T* (Move to Next Line) - テキスト行を次行へ移動。
    /// 形式: T*
    /// 効果: Td(0, -TL) と同等。
    /// </summary>
    public void MoveToNextLine()
    {
        MoveTextPosition(0, -_context.TextState.Leading);
    }

    /// <summary>
    /// Tm (Set Text Matrix) - テキスト行列を絶対指定。
    /// 形式: a b c d e f Tm
    /// </summary>
    /// <param name="a">行列の a 成分。</param>
    /// <param name="b">行列の b 成分。</param>
    /// <param name="c">行列の c 成分。</param>
    /// <param name="d">行列の d 成分。</param>
    /// <param name="e">行列の e 成分（x 移動）。</param>
    /// <param name="f">行列の f 成分（y 移動）。</param>
    public void SetTextMatrix(double a, double b, double c, double d, double e, double f)
    {
        _context.TextState.TextLineMatrix = new PdfRenderMatrix(a, b, c, d, e, f);
        _context.TextState.TextMatrix = _context.TextState.TextLineMatrix;
    }

    /// <summary>
    /// Tw (Word Spacing) - 単語間隔を設定。
    /// </summary>
    /// <param name="spacing">間隔（単位: 1/1000 フォントサイズ）。</param>
    public void SetWordSpacing(double spacing)
    {
        _context.TextState.WordSpacing = spacing;
    }

    /// <summary>
    /// Tc (Character Spacing) - 文字間隔を設定。
    /// </summary>
    /// <param name="spacing">間隔（単位: 1/1000 フォントサイズ）。</param>
    public void SetCharacterSpacing(double spacing)
    {
        _context.TextState.CharacterSpacing = spacing;
    }

    /// <summary>
    /// TL (Leading) - 行間を設定。
    /// </summary>
    /// <param name="leading">行間（単位: 1/1000 フォントサイズ）。</param>
    public void SetLeading(double leading)
    {
        _context.TextState.Leading = leading;
    }

    /// <summary>
    /// Tz (Horizontal Scaling) - 水平スケーリングを設定。
    /// </summary>
    /// <param name="scaling">スケーリング比率（%）。既定値 100。</param>
    public void SetHorizontalScaling(double scaling)
    {
        if (scaling <= 0)
        {
            throw new InvalidOperationException($"水平スケーリングは正の値である必要があります: {scaling}");
        }

        _context.TextState.HorizontalScaling = scaling;
    }

    /// <summary>
    /// Tj (Text Show) - 文字列を表示。
    /// 形式: (text) Tj または <hextext> Tj
    /// </summary>
    /// <param name="textBytes">表示テキストのバイト列。</param>
    public void ShowText(byte[] textBytes)
    {
        ArgumentNullException.ThrowIfNull(textBytes);

        if (_context.TextState.FontDictionary == null)
        {
            throw new InvalidOperationException("フォントが設定されていません。");
        }

        var text = DecodeText(textBytes, _context.TextState.FontDictionary);
        var advances = CalculateGlyphAdvances(textBytes, _context.TextState.FontDictionary);

        var textRun = new PdfTextRun
        {
            TextMatrix = _context.TextState.TextMatrix,
            FontDictionary = _context.TextState.FontDictionary,
            FontSize = _context.TextState.FontSize,
            HorizontalScaling = _context.TextState.HorizontalScaling,
            Text = text,
            GlyphAdvances = advances,
        };

        _context.RecordTextRun(textRun);

        // テキス行列を前に進める
        UpdateTextMatrixAfterShowText(textBytes);
    }

    /// <summary>
    /// TJ (Text Show with Adjustments) - テキストと位置調整を交互に適用。
    /// 形式: [(text1) -50 (text2) ...] TJ
    /// </summary>
    /// <param name="adjustmentArray">テキストと調整値の配列。</param>
    public void ShowTextWithAdjustments(PdfArray adjustmentArray)
    {
        ArgumentNullException.ThrowIfNull(adjustmentArray);

        if (_context.TextState.FontDictionary == null)
        {
            throw new InvalidOperationException("フォントが設定されていません。");
        }

        for (var i = 0; i < adjustmentArray.Count; i++)
        {
            var item = adjustmentArray[i];

            if (item is PdfString textItem)
            {
                // PdfString の値が Hex 形式 (<...>) か通常形式 (...) かを判定する
                // 簡易版：値そのままをバイト列に変換
                var textBytes = System.Text.Encoding.Latin1.GetBytes(textItem.Value);
                ShowText(textBytes);
            }
            else if (item is PdfNumber adjustment)
            {
                // 調整値: テキスト行列を移動。単位は 1/1000 フォントサイズ。
                var adjustmentValue = adjustment.Value / 1000.0 * _context.TextState.FontSize;
                _context.TextState.TextMatrix = _context.TextState.TextMatrix with
                {
                    E = _context.TextState.TextMatrix.E - (adjustmentValue * _context.TextState.HorizontalScaling / 100.0),
                };
            }
        }
    }

    private string DecodeText(byte[] textBytes, PdfDictionary fontDictionary)
    {
        var fontType = _fontResolver.DetermineFontType(fontDictionary);

        if (fontType == PdfFontType.SimpleFont)
        {
            return DecodeSimpleFontText(textBytes, fontDictionary);
        }

        if (fontType == PdfFontType.CompositeFont)
        {
            return DecodeCompositeFontText(textBytes, fontDictionary);
        }

        // フォールバック: バイト列をそのまま Latin-1 として解釈
        return System.Text.Encoding.Latin1.GetString(textBytes);
    }

    private string DecodeSimpleFontText(byte[] textBytes, PdfDictionary fontDictionary)
    {
        // 単純フォント: 各バイトが1文字。Encoding に従って文字を解釈。
        var encoding = _fontResolver.GetEncoding(fontDictionary);

        // 当面は WinAnsiEncoding と Latin-1 を同じように扱う。
        // 実装が複雑になるため、簡易版。
        return System.Text.Encoding.Latin1.GetString(textBytes);
    }

    private string DecodeCompositeFontText(byte[] textBytes, PdfDictionary fontDictionary)
    {
        // 複合フォント（Type0/CIDFont）: バイト列を2バイトまたは可変長CIDに分解。
        // ToUnicode CMap があれば利用、なければフォールバック。

        var toUnicodeBytes = _fontResolver.GetToUnicodeCMap(fontDictionary);
        if (toUnicodeBytes != null)
        {
            // ToUnicode CMap を解析して文字列を生成。
            // 簡易版：当面未実装。
        }

        // フォールバック: バイト列を 2 バイト単位で CID と解釈（簡易版）。
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < textBytes.Length; i += 2)
        {
            if (i + 1 < textBytes.Length)
            {
                var cid = (textBytes[i] << 8) | textBytes[i + 1];
                // CID を Unicode 文字に変換。
                // 簡易版：CID そのものを Char に強制キャスト（不完全）。
                sb.Append((char)(cid & 0xFFFF));
            }
        }

        return sb.ToString();
    }

    private byte[] DecodeHexString(string hexString)
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

    private List<double> CalculateGlyphAdvances(byte[] textBytes, PdfDictionary fontDictionary)
    {
        // グリフアドバンス（幅）を計算。
        // 当面は簡易版：すべてのグリフの幅を均等と仮定（500/1000 フォントサイズ）。
        // 実装が複雑になるため、フォントメトリクスの取得は Wave 7+ へ。

        var advances = new List<double>();
        var charCount = EstimateCharacterCount(textBytes, fontDictionary);
        for (var i = 0; i < charCount; i++)
        {
            advances.Add(500.0); // 既定グリフ幅
        }

        return advances;
    }

    private int EstimateCharacterCount(byte[] textBytes, PdfDictionary fontDictionary)
    {
        var fontType = _fontResolver.DetermineFontType(fontDictionary);
        if (fontType == PdfFontType.CompositeFont)
        {
            return textBytes.Length / 2; // 複合フォント：2 バイト/文字
        }

        return textBytes.Length; // 単純フォント：1 バイト/文字
    }

    private void UpdateTextMatrixAfterShowText(byte[] textBytes)
    {
        // テキスト行列を前に進める。
        // 簡易版：グリフアドバンスの合計で水平移動。

        var advances = CalculateGlyphAdvances(textBytes, _context.TextState.FontDictionary!);
        var totalAdvance = advances.Sum();

        // スケーリングと単位変換を適用
        var scaledAdvance = (totalAdvance / 1000.0) * _context.TextState.FontSize * _context.TextState.HorizontalScaling / 100.0;

        _context.TextState.TextMatrix = _context.TextState.TextMatrix with
        {
            E = _context.TextState.TextMatrix.E + scaledAdvance,
        };
    }
}
