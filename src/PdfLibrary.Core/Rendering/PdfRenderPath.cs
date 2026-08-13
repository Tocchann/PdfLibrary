namespace PdfLibrary.Core.Rendering;

public sealed class PdfRenderPath
{
    private readonly List<PdfPathSegment> _segments = [];

    public IReadOnlyList<PdfPathSegment> Segments => _segments;

    public void Add(PdfPathSegment segment) => _segments.Add(segment);

    public void Clear() => _segments.Clear();
}
