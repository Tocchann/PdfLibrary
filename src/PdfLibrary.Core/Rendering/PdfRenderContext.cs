namespace PdfLibrary.Core.Rendering;

public sealed class PdfRenderContext
{
    private readonly Stack<PdfGraphicsState> _stateStack = [];
    private readonly Stack<PdfTextState> _textStateStack = [];
    private readonly Stack<PdfColorSpace> _strokingColorSpaceStack = [];
    private readonly Stack<PdfColorSpace> _nonStrokingColorSpaceStack = [];
    private readonly List<PdfRenderCommand> _commands = [];

    public PdfRenderContext(PdfArray mediaBox)
    {
        ArgumentNullException.ThrowIfNull(mediaBox);
        MediaBox = mediaBox;
        InitializeColors();
    }

    public PdfArray MediaBox { get; }

    public PdfGraphicsState GraphicsState { get; private set; } = new();

    public PdfRenderPath CurrentPath { get; } = new();

    /// <summary>
    /// テキスト描画状態。
    /// </summary>
    public PdfTextState TextState { get; private set; } = new();

    /// <summary>
    /// ストロークカラー（線色）。
    /// </summary>
    public PdfColor StrokingColor { get; set; } = null!;

    /// <summary>
    /// 非ストロークカラー（塗り色）。
    /// </summary>
    public PdfColor NonStrokingColor { get; set; } = null!;

    private void InitializeColors()
    {
        var grayColorSpace = new PdfDeviceGrayColorSpace();
        StrokingColor = new PdfColor { ColorSpace = grayColorSpace };
        StrokingColor.SetComponents(0.0);
        NonStrokingColor = new PdfColor { ColorSpace = grayColorSpace };
        NonStrokingColor.SetComponents(0.0);
    }

    public IReadOnlyList<PdfRenderCommand> Commands => _commands.AsReadOnly();

    public void SaveGraphicsState()
    {
        _stateStack.Push(GraphicsState.Clone());
        _textStateStack.Push(TextState.Clone());
        _strokingColorSpaceStack.Push(StrokingColor.ColorSpace.Clone());
        _nonStrokingColorSpaceStack.Push(NonStrokingColor.ColorSpace.Clone());
    }

    public void RestoreGraphicsState()
    {
        if (_stateStack.Count == 0)
        {
            throw new InvalidOperationException("graphics state stack が空のため Q を適用できません。");
        }

        GraphicsState = _stateStack.Pop();
        TextState = _textStateStack.Pop();

        if (_strokingColorSpaceStack.Count > 0)
        {
            var colorSpace = _strokingColorSpaceStack.Pop();
            StrokingColor = new PdfColor { ColorSpace = colorSpace };
        }

        if (_nonStrokingColorSpaceStack.Count > 0)
        {
            var colorSpace = _nonStrokingColorSpaceStack.Pop();
            NonStrokingColor = new PdfColor { ColorSpace = colorSpace };
        }
    }

    public void RecordPath(PdfPathPaintingOperator paintingOperator)
    {
        var snapshot = new PdfRenderPath();
        foreach (var segment in CurrentPath.Segments)
        {
            snapshot.Add(segment);
        }

        _commands.Add(new PdfPathRenderCommand(paintingOperator, snapshot));
        CurrentPath.Clear();
    }

    /// <summary>
    /// テキスト実行をコマンドとして記録します。
    /// </summary>
    /// <param name="textRun">テキスト実行。</param>
    public void RecordTextRun(PdfTextRun textRun)
    {
        ArgumentNullException.ThrowIfNull(textRun);
        _commands.Add(new PdfTextRenderCommand(textRun, NonStrokingColor.Clone()));
    }
}
