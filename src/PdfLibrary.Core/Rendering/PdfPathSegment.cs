namespace PdfLibrary.Core.Rendering;

public enum PdfPathSegmentKind
{
    MoveTo,
    LineTo,
    CubicBezierTo,
    ClosePath,
}

public sealed record PdfPathSegment(PdfPathSegmentKind Kind, IReadOnlyList<PdfRenderPoint> Points);
