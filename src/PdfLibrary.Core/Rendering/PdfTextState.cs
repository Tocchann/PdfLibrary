namespace PdfLibrary.Core.Rendering;

/// <summary>
/// テキスト描画状態を管理するクラス。
/// フォント参照、サイズ、行列、スケーリング、行間等を保持。
/// </summary>
public sealed class PdfTextState
{
    /// <summary>
    /// フォント辞書への参照。
    /// </summary>
    public PdfDictionary? FontDictionary { get; set; }

    /// <summary>
    /// フォントサイズ（単位: pt）。
    /// </summary>
    public double FontSize { get; set; } = 12.0;

    /// <summary>
    /// 水平スケーリング（単位: %）。既定値 100。
    /// </summary>
    public double HorizontalScaling { get; set; } = 100.0;

    /// <summary>
    /// 文字間隔（character spacing）。
    /// </summary>
    public double CharacterSpacing { get; set; } = 0.0;

    /// <summary>
    /// 単語間隔（word spacing）。
    /// </summary>
    public double WordSpacing { get; set; } = 0.0;

    /// <summary>
    /// 行送り（leading）。テキスト移動に使用。
    /// </summary>
    public double Leading { get; set; } = 0.0;

    /// <summary>
    /// テキスト行列 (Tlm)。現在のテキスト行の左下隅位置。
    /// </summary>
    public PdfRenderMatrix TextLineMatrix { get; set; } = PdfRenderMatrix.Identity;

    /// <summary>
    /// テキスト行列 (Tm)。テキスト描画位置。
    /// </summary>
    public PdfRenderMatrix TextMatrix { get; set; } = PdfRenderMatrix.Identity;

    /// <summary>
    /// テキスト状態をディープクローンします。
    /// </summary>
    public PdfTextState Clone()
    {
        return new PdfTextState
        {
            FontDictionary = FontDictionary,
            FontSize = FontSize,
            HorizontalScaling = HorizontalScaling,
            CharacterSpacing = CharacterSpacing,
            WordSpacing = WordSpacing,
            Leading = Leading,
            TextLineMatrix = TextLineMatrix,
            TextMatrix = TextMatrix,
        };
    }

    /// <summary>
    /// テキスト状態をリセットします。
    /// </summary>
    public void Reset()
    {
        FontDictionary = null;
        FontSize = 12.0;
        HorizontalScaling = 100.0;
        CharacterSpacing = 0.0;
        WordSpacing = 0.0;
        Leading = 0.0;
        TextLineMatrix = PdfRenderMatrix.Identity;
        TextMatrix = PdfRenderMatrix.Identity;
    }
}
