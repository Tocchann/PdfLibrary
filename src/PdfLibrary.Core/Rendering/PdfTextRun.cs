namespace PdfLibrary.Core.Rendering;

/// <summary>
/// テキスト描画コマンド内の1つのテキスト実行（テキスト部分）を表します。
/// グリフの位置、フォント情報、実際の文字列を保持。
/// </summary>
public sealed record PdfTextRun
{
    /// <summary>
    /// テキスト描画時の行列状態。
    /// </summary>
    public required PdfRenderMatrix TextMatrix { get; init; }

    /// <summary>
    /// フォント辞書への参照（デバッグ用）。
    /// </summary>
    public PdfDictionary? FontDictionary { get; init; }

    /// <summary>
    /// 適用されたフォントサイズ（単位: pt）。
    /// </summary>
    public required double FontSize { get; init; }

    /// <summary>
    /// 水平スケーリング（単位: %）。
    /// </summary>
    public required double HorizontalScaling { get; init; }

    /// <summary>
    /// 表示文字列（元のバイト列または正規化済み Unicode）。
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// 各グリフの水平位置調整（advances）。
    /// 要素数は Text.Length に対応。単位: 1/1000 フォントサイズ。
    /// </summary>
    public required IReadOnlyList<double> GlyphAdvances { get; init; }
}
