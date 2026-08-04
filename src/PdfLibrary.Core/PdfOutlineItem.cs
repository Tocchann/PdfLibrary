namespace PdfLibrary.Core;

public sealed class PdfOutlineItem
{
    private string _title = string.Empty;

    public PdfOutlineItem(string title)
    {
        Title = title;
    }

    public string Title
    {
        get => _title;
        set => _title = string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("タイトルは必須です。", nameof(value)) : value;
    }

    public PdfValue? Destination { get; set; }

    public List<PdfOutlineItem> Children { get; } = [];
}
