namespace PdfLibrary.Core.Rendering;

/// <summary>
/// フォントタイプの列挙。
/// </summary>
public enum PdfFontType
{
    /// <summary>単純フォント（Type1, TrueType）。</summary>
    SimpleFont,

    /// <summary>複合フォント（Type0/CIDFont）。</summary>
    CompositeFont,

    /// <summary>未対応またはタイプが確定できない。</summary>
    Unknown,
}

/// <summary>
/// PDF フォント辞書を解決し、フォント情報を取得するクラス。
/// </summary>
public sealed class PdfFontResolver
{
    private readonly Func<PdfReference, PdfValue?> _objectResolver;

    public PdfFontResolver(Func<PdfReference, PdfValue?> objectResolver)
    {
        ArgumentNullException.ThrowIfNull(objectResolver);
        _objectResolver = objectResolver;
    }

    /// <summary>
    /// リソース辞書からフォント名で Font 辞書を解決します。
    /// </summary>
    /// <param name="resourceDictionary">ページリソース辞書。</param>
    /// <param name="fontName">フォント参照名（例："F1"）。</param>
    /// <returns>解決された Font 辞書、またはnull（見つからない場合）。</returns>
    public PdfDictionary? ResolveFontByName(PdfDictionary resourceDictionary, string fontName)
    {
        ArgumentNullException.ThrowIfNull(resourceDictionary);
        ArgumentNullException.ThrowIfNull(fontName);

        if (!resourceDictionary.TryGetValue("Font", out var fontDictValue))
        {
            return null;
        }

        var fontDict = fontDictValue switch
        {
            PdfDictionary dict => dict,
            PdfReference reference => _objectResolver(reference) as PdfDictionary,
            _ => null,
        };

        if (fontDict == null)
        {
            return null;
        }

        if (!fontDict.TryGetValue(fontName, out var fontRefValue))
        {
            return null;
        }

        return fontRefValue switch
        {
            PdfDictionary dict => dict,
            PdfReference reference => _objectResolver(reference) as PdfDictionary,
            _ => null,
        };
    }

    /// <summary>
    /// フォント辞書のタイプを判定します。
    /// </summary>
    /// <param name="fontDictionary">フォント辞書。</param>
    /// <returns>フォントタイプ。</returns>
    public PdfFontType DetermineFontType(PdfDictionary fontDictionary)
    {
        ArgumentNullException.ThrowIfNull(fontDictionary);

        if (!fontDictionary.TryGetValue("Subtype", out var subtypeValue) || subtypeValue is not PdfName subtype)
        {
            return PdfFontType.Unknown;
        }

        if (subtype.Value == "Type0")
        {
            return PdfFontType.CompositeFont;
        }

        if (subtype.Value is "Type1" or "TrueType")
        {
            return PdfFontType.SimpleFont;
        }

        return PdfFontType.Unknown;
    }

    /// <summary>
    /// フォント辞書から BaseFont 名を取得します。
    /// </summary>
    /// <param name="fontDictionary">フォント辞書。</param>
    /// <returns>BaseFont 名、またはnull（見つからない場合）。</returns>
    public string? GetBaseFontName(PdfDictionary fontDictionary)
    {
        ArgumentNullException.ThrowIfNull(fontDictionary);

        if (!fontDictionary.TryGetValue("BaseFont", out var baseFontValue) || baseFontValue is not PdfName baseFont)
        {
            return null;
        }

        return baseFont.Value;
    }

    /// <summary>
    /// フォント辞書から Encoding を取得します（単純フォント用）。
    /// Encoding が未指定の場合は "WinAnsiEncoding" を返します。
    /// </summary>
    /// <param name="fontDictionary">フォント辞書。</param>
    /// <returns>エンコーディング名。</returns>
    public string GetEncoding(PdfDictionary fontDictionary)
    {
        ArgumentNullException.ThrowIfNull(fontDictionary);

        if (!fontDictionary.TryGetValue("Encoding", out var encodingValue))
        {
            return "WinAnsiEncoding";
        }

        if (encodingValue is PdfName encodingName)
        {
            return encodingName.Value;
        }

        // Encoding が辞書（差分エンコーディング）の場合は "WinAnsiEncoding" を基本とする
        return "WinAnsiEncoding";
    }

    /// <summary>
    /// 複合フォント（Type0）の DescendantFonts から CIDFont 辞書を取得します。
    /// </summary>
    /// <param name="fontDictionary">Type0 フォント辞書。</param>
    /// <returns>CIDFont 辞書、またはnull（見つからない場合）。</returns>
    public PdfDictionary? GetCIDFontDictionary(PdfDictionary fontDictionary)
    {
        ArgumentNullException.ThrowIfNull(fontDictionary);

        if (!fontDictionary.TryGetValue("DescendantFonts", out var descendantValue) || descendantValue is not PdfArray descendantArray)
        {
            return null;
        }

        if (descendantArray.Count == 0)
        {
            return null;
        }

        var firstDescendant = descendantArray[0];
        return firstDescendant switch
        {
            PdfDictionary dict => dict,
            PdfReference reference => _objectResolver(reference) as PdfDictionary,
            _ => null,
        };
    }

    /// <summary>
    /// Type0 フォント辞書から ToUnicode CMap（byte[]）を取得します。
    /// </summary>
    /// <param name="fontDictionary">Type0 フォント辞書。</param>
    /// <returns>ToUnicode CMap のバイト列、またはnull（見つからない場合）。</returns>
    public byte[]? GetToUnicodeCMap(PdfDictionary fontDictionary)
    {
        ArgumentNullException.ThrowIfNull(fontDictionary);

        if (!fontDictionary.TryGetValue("ToUnicode", out var toUnicodeValue))
        {
            return null;
        }

        var cmapStream = toUnicodeValue switch
        {
            PdfStream stream => stream,
            PdfReference reference => _objectResolver(reference) as PdfStream,
            _ => null,
        };

        return cmapStream?.Data.ToArray();
    }
}
