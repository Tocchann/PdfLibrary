namespace PdfLibrary.Core.Rendering;

public sealed record PdfRenderCommand(PdfPathPaintingOperator Operator, PdfRenderPath Path);
