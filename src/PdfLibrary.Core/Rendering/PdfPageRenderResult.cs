namespace PdfLibrary.Core.Rendering;

public sealed class PdfPageRenderResult
{
    public PdfPageRenderResult(PdfArray mediaBox, IReadOnlyList<PdfRenderCommand> commands)
    {
        MediaBox = mediaBox ?? throw new ArgumentNullException(nameof(mediaBox));
        Commands = commands ?? throw new ArgumentNullException(nameof(commands));
    }

    public PdfArray MediaBox { get; }

    public IReadOnlyList<PdfRenderCommand> Commands { get; }
}
