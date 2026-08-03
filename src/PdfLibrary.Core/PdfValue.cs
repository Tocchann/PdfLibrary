namespace PdfLibrary.Core;

public abstract class PdfValue
{
    public abstract PdfValueKind Kind { get; }
}

public enum PdfValueKind
{
    Null,
    Boolean,
    Number,
    Name,
    String,
    Array,
    Dictionary,
    Stream,
    Reference,
}
