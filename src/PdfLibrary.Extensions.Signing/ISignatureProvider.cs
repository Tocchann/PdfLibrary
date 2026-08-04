namespace PdfLibrary.Extensions.Signing;

/// <summary>PDF 署名の CMS/PKCS#7 バイト列生成を抽象化するインターフェース。</summary>
public interface ISignatureProvider
{
    /// <summary>署名対象のバイト列を受け取り、CMS 署名バイト列を返します。</summary>
    byte[] Sign(byte[] dataToSign);
}
