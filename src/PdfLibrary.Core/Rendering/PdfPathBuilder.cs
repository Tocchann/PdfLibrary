namespace PdfLibrary.Core.Rendering;

internal sealed class PdfPathBuilder
{
    private readonly PdfRenderContext _context;
    private PdfRenderPoint? _currentPoint;
    private PdfRenderPoint? _subpathStart;

    public PdfPathBuilder(PdfRenderContext context)
        => _context = context ?? throw new ArgumentNullException(nameof(context));

    public void MoveTo(double x, double y)
    {
        var point = Transform(x, y);
        _context.CurrentPath.Add(new PdfPathSegment(PdfPathSegmentKind.MoveTo, [point]));
        _currentPoint = point;
        _subpathStart = point;
    }

    public void LineTo(double x, double y)
    {
        EnsureCurrentPoint();
        var point = Transform(x, y);
        _context.CurrentPath.Add(new PdfPathSegment(PdfPathSegmentKind.LineTo, [point]));
        _currentPoint = point;
    }

    public void CurveTo(double x1, double y1, double x2, double y2, double x3, double y3)
    {
        EnsureCurrentPoint();
        var p1 = Transform(x1, y1);
        var p2 = Transform(x2, y2);
        var p3 = Transform(x3, y3);
        _context.CurrentPath.Add(new PdfPathSegment(PdfPathSegmentKind.CubicBezierTo, [p1, p2, p3]));
        _currentPoint = p3;
    }

    public void Rectangle(double x, double y, double width, double height)
    {
        MoveTo(x, y);
        LineTo(x + width, y);
        LineTo(x + width, y + height);
        LineTo(x, y + height);
        ClosePath();
    }

    public void ClosePath()
    {
        EnsureCurrentPoint();
        if (_subpathStart is null)
        {
            throw new InvalidOperationException("開始点が存在しないため h を適用できません。");
        }

        _context.CurrentPath.Add(new PdfPathSegment(PdfPathSegmentKind.ClosePath, [_subpathStart.Value]));
        _currentPoint = _subpathStart;
    }

    private PdfRenderPoint Transform(double x, double y)
        => _context.GraphicsState.CurrentTransformationMatrix.Transform(x, y);

    private void EnsureCurrentPoint()
    {
        if (_currentPoint is null)
        {
            throw new InvalidOperationException("現在のパス開始点が存在しません。");
        }
    }
}
