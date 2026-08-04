namespace PdfLibrary.Extensions.Signing;

/// <summary>署名関連の例外。</summary>
public sealed class PdfSigningException : Exception
{
    public PdfSigningException(string message) : base(message)
    {
    }

    public PdfSigningException(string message, Exception inner) : base(message, inner)
    {
    }
}
