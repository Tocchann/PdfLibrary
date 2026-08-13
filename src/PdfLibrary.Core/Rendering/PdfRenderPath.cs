namespace PdfLibrary.Core.Rendering;

public sealed class PdfRenderPath
{
    private readonly List<PdfPathSegment> _segments = [];

    public IReadOnlyList<PdfPathSegment> Segments => _segments.AsReadOnly();

    internal void Add(PdfPathSegment segment) => _segments.Add(segment);

    internal void Clear() => _segments.Clear();
}
