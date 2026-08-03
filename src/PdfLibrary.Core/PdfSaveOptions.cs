namespace PdfLibrary.Core;

public sealed class PdfSaveOptions
{
    public static PdfSaveOptions Default { get; } = new();

    public PdfSaveMode Mode { get; init; } = PdfSaveMode.Overwrite;
}
