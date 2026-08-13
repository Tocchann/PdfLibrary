namespace PdfLibrary.Core.Rendering;

public sealed class PdfRenderContext
{
    private readonly Stack<PdfGraphicsState> _stateStack = [];
    private readonly List<PdfRenderCommand> _commands = [];

    public PdfRenderContext(PdfArray mediaBox)
    {
        ArgumentNullException.ThrowIfNull(mediaBox);
        MediaBox = mediaBox;
    }

    public PdfArray MediaBox { get; }

    public PdfGraphicsState GraphicsState { get; private set; } = new();

    public PdfRenderPath CurrentPath { get; } = new();

    public IReadOnlyList<PdfRenderCommand> Commands => _commands;

    public void SaveGraphicsState() => _stateStack.Push(GraphicsState.Clone());

    public void RestoreGraphicsState()
    {
        if (_stateStack.Count == 0)
        {
            throw new InvalidOperationException("graphics state stack が空のため Q を適用できません。");
        }

        GraphicsState = _stateStack.Pop();
    }

    public void RecordPath(PdfPathPaintingOperator paintingOperator)
    {
        var snapshot = new PdfRenderPath();
        foreach (var segment in CurrentPath.Segments)
        {
            snapshot.Add(segment);
        }

        _commands.Add(new PdfRenderCommand(paintingOperator, snapshot));
        CurrentPath.Clear();
    }
}
