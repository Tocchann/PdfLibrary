namespace PdfLibrary.Extensions.Signing;

/// <summary>署名準備の結果。署名済みバイト列を生成するために必要な情報を保持します。</summary>
public sealed class PdfSigningContext
{
    /// <summary>/Contents プレースホルダを含む準備済み PDF バイト列。</summary>
    public byte[] PreparedBytes { get; internal set; } = [];

    /// <summary>PDF spec 12.8 に従う ByteRange 配列 [offset1, length1, offset2, length2]。</summary>
    public long[] ByteRange { get; internal set; } = [];

    /// <summary>PreparedBytes 内の /Contents 値開始位置（'&lt;' の直後のオフセット）。</summary>
    public long ContentsDataStart { get; internal set; }

    /// <summary>/Contents プレースホルダの hex 文字数（= ContentsReserveSize * 2）。</summary>
    public int ContentsHexLength { get; internal set; }
}
