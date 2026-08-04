namespace PdfLibrary.Extensions.Signing;

/// <summary>署名フィールドの構成オプション。</summary>
public sealed class PdfSignatureOptions
{
    /// <summary>署名フィールド名（/T）。既定値は "Signature1"。</summary>
    public string FieldName { get; set; } = "Signature1";

    /// <summary>署名フィールドを配置するページのインデックス（0 始まり）。</summary>
    public int PageIndex { get; set; }

    /// <summary>署名フィールドの矩形 [x1, y1, x2, y2]。既定値は [0, 0, 0, 0]（不可視）。</summary>
    public double[] Rect { get; set; } = [0, 0, 0, 0];

    /// <summary>/Filter の値。既定値は "Adobe.PPKLite"。</summary>
    public string Filter { get; set; } = "Adobe.PPKLite";

    /// <summary>/SubFilter の値。既定値は "adbe.pkcs7.detached"。</summary>
    public string SubFilter { get; set; } = "adbe.pkcs7.detached";

    /// <summary>/Contents プレースホルダのバイト数。CMS がこのサイズを超えるとエラーになります。既定値は 8192。</summary>
    public int ContentsReserveSize { get; set; } = 8192;
}
