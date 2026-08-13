namespace PdfLibrary.Core.Rendering;

/// <summary>
/// テキスト描画コマンド。パスと異なるコマンドタイプであることを明示。
/// </summary>
public sealed record PdfTextRenderCommand(PdfTextRun TextRun, PdfColor AppliedColor) : PdfRenderCommand;
