namespace PdfLibrary.Core;

public enum PdfFormFieldType
{
    Text,
    Button,
    Choice,
}

public sealed class PdfFormField
{
    public PdfFormField(string name, PdfFormFieldType fieldType)
    {
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("フィールド名は必須です。", nameof(name)) : name;
        FieldType = fieldType;
    }

    public string Name { get; set; }

    public PdfFormFieldType FieldType { get; }

    public string? Value { get; set; }

    public PdfArray? Rect { get; set; }

    public int? PageIndex { get; set; }
}
