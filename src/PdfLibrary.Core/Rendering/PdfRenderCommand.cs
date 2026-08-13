namespace PdfLibrary.Core.Rendering;

/// <summary>
/// 描画コマンドの基底クラス。
/// </summary>
public abstract record PdfRenderCommand;

/// <summary>
/// パス描画コマンド。
/// </summary>
public sealed record PdfPathRenderCommand(PdfPathPaintingOperator Operator, PdfRenderPath Path) : PdfRenderCommand;
