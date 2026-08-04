using PdfLibrary.Core;

namespace PdfLibrary.Extensions.Signing;

/// <summary>
/// PDF 署名の2ステップフロー（準備・適用）を提供します。
///
/// 使用方法:
/// 1. <see cref="Prepare"/> で /ByteRange と /Contents プレースホルダ付きの PDF を生成する
/// 2. 外部の <see cref="ISignatureProvider"/> で署名対象バイト列に署名する
/// 3. <see cref="Apply"/> で CMS バイト列を /Contents に埋め込む
///
/// 対象外（外部責務）: CMS/PKCS#7 生成・証明書検証・タイムスタンプ・長期署名（PAdES）
/// </summary>
public static class PdfSigningSession
{
    /// <summary>ByteRange プレースホルダに使用する固定幅（各数値 10 桁）。</summary>
    private const int ByteRangeFieldWidth = 10;

    /// <summary>ByteRange プレースホルダに使用する識別子。</summary>
    private const double ByteRangePlaceholderValue = 9876543210.0;

    /// <summary>
    /// 署名フィールドと /Contents プレースホルダを含む PDF バイト列を生成します。
    /// </summary>
    public static PdfSigningContext Prepare(PdfDocument document, PdfSignatureOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        options ??= new PdfSignatureOptions();

        if (options.ContentsReserveSize <= 0)
        {
            throw new PdfSigningException("ContentsReserveSize は正の値である必要があります。");
        }

        if (options.PageIndex < 0 || options.PageIndex >= document.PageCount)
        {
            throw new PdfSigningException($"PageIndex {options.PageIndex} はページ範囲外です。");
        }

        var placeholderContents = new byte[options.ContentsReserveSize];
        var byteRangePlaceholder = new PdfArray
        {
            new PdfNumber(0),
            new PdfNumber(ByteRangePlaceholderValue),
            new PdfNumber(ByteRangePlaceholderValue),
            new PdfNumber(ByteRangePlaceholderValue),
        };

        var sigDict = new PdfDictionary
        {
            ["Type"] = new PdfName("Sig"),
            ["Filter"] = new PdfName(options.Filter),
            ["SubFilter"] = new PdfName(options.SubFilter),
            ["ByteRange"] = byteRangePlaceholder,
            ["Contents"] = new PdfHexString(placeholderContents),
        };

        var sigObject = document.AddObject(sigDict);

        var fieldDict = new PdfDictionary
        {
            ["Subtype"] = new PdfName("Widget"),
            ["FT"] = new PdfName("Sig"),
            ["T"] = new PdfString(options.FieldName),
            ["V"] = sigObject.Reference,
            ["Rect"] = BuildRectArray(options.Rect),
        };

        document.RegisterFormField(options.PageIndex, fieldDict);

        var saveOptions = document.OriginalBytes is not null
            ? new PdfSaveOptions { Mode = PdfSaveMode.Append }
            : PdfSaveOptions.Default;

        var preparedBytes = document.Save(saveOptions);
        var contentsHexLength = options.ContentsReserveSize * 2;

        var contentsAngle = FindContentsPlaceholder(preparedBytes, contentsHexLength);
        if (contentsAngle < 0)
        {
            throw new PdfSigningException("準備済み PDF 内に /Contents プレースホルダが見つかりませんでした。");
        }

        // '<' の次が hex データの開始位置
        var contentsDataStart = contentsAngle + 1;
        // '>' の位置
        var contentsEnd = contentsDataStart + contentsHexLength;
        var totalLen = preparedBytes.Length;

        var byteRange = new long[] { 0, contentsAngle, contentsEnd + 1, totalLen - contentsEnd - 1 };

        PatchByteRange(preparedBytes, byteRange);

        return new PdfSigningContext
        {
            PreparedBytes = preparedBytes,
            ByteRange = byteRange,
            ContentsDataStart = contentsDataStart,
            ContentsHexLength = contentsHexLength,
        };
    }

    /// <summary>
    /// CMS 署名バイト列を /Contents に埋め込んで署名済み PDF バイト列を返します。
    /// </summary>
    public static byte[] Apply(PdfSigningContext context, byte[] cmsBytes)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(cmsBytes);

        if (context.PreparedBytes.Length == 0)
        {
            throw new PdfSigningException("PdfSigningContext が空です。Prepare() を先に呼び出してください。");
        }

        var maxCmsBytes = context.ContentsHexLength / 2;
        if (cmsBytes.Length > maxCmsBytes)
        {
            throw new PdfSigningException(
                $"CMS バイト列（{cmsBytes.Length} バイト）が /Contents プレースホルダ（{maxCmsBytes} バイト）を超えています。" +
                " ContentsReserveSize を大きくしてください。");
        }

        var result = (byte[])context.PreparedBytes.Clone();

        // hex エンコードして残りをゼロ埋め
        var hexStr = Convert.ToHexString(cmsBytes);
        var paddedHex = hexStr.PadRight(context.ContentsHexLength, '0');
        var hexBytes = System.Text.Encoding.ASCII.GetBytes(paddedHex);
        Array.Copy(hexBytes, 0, result, context.ContentsDataStart, hexBytes.Length);

        return result;
    }

    /// <summary>署名対象のバイト列（ByteRange で指定される範囲）を連結して返します。</summary>
    public static byte[] ExtractSignedContent(PdfSigningContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var br = context.ByteRange;
        var part1 = context.PreparedBytes.AsSpan((int)br[0], (int)br[1]);
        var part2 = context.PreparedBytes.AsSpan((int)br[2], (int)br[3]);
        var result = new byte[part1.Length + part2.Length];
        part1.CopyTo(result);
        part2.CopyTo(result.AsSpan(part1.Length));
        return result;
    }

    private static PdfArray BuildRectArray(double[] rect)
    {
        var arr = new PdfArray();
        var values = rect.Length >= 4 ? rect : [0.0, 0.0, 0.0, 0.0];
        foreach (var v in values.Take(4))
        {
            arr.Add(new PdfNumber(v));
        }

        return arr;
    }

    /// <summary>プレースホルダ用の '<' の位置を返す。見つからなければ -1。</summary>
    private static int FindContentsPlaceholder(byte[] bytes, int hexLength)
    {
        // '<' + hexLength 個の '0' を探す
        if (hexLength <= 0)
        {
            return -1;
        }

        var span = bytes.AsSpan();
        for (var i = 0; i <= bytes.Length - hexLength - 2; i++)
        {
            if (span[i] != (byte)'<')
            {
                continue;
            }

            var candidate = span.Slice(i + 1, hexLength);
            var allZero = true;
            foreach (var b in candidate)
            {
                if (b != (byte)'0')
                {
                    allZero = false;
                    break;
                }
            }

            if (allZero && i + 1 + hexLength < bytes.Length && span[i + 1 + hexLength] == (byte)'>')
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>ByteRange プレースホルダを実際の値でパッチする。</summary>
    private static void PatchByteRange(byte[] bytes, long[] byteRange)
    {
        // プレースホルダ: "9876543210 9876543210 9876543210"
        var placeholder = System.Text.Encoding.ASCII.GetBytes(
            $"{ByteRangePlaceholderValue:F0} {ByteRangePlaceholderValue:F0} {ByteRangePlaceholderValue:F0}");

        var pos = bytes.AsSpan().IndexOf(placeholder.AsSpan());
        if (pos < 0)
        {
            throw new PdfSigningException("ByteRange プレースホルダが見つかりませんでした。");
        }

        // 各フィールドを 10 桁に揃えて上書き
        var patch = System.Text.Encoding.ASCII.GetBytes(
            $"{byteRange[1],-ByteRangeFieldWidth} {byteRange[2],-ByteRangeFieldWidth} {byteRange[3],-ByteRangeFieldWidth}");

        if (patch.Length != placeholder.Length)
        {
            throw new PdfSigningException(
                $"ByteRange パッチサイズが一致しません（expected {placeholder.Length}, got {patch.Length}）。");
        }

        patch.CopyTo(bytes.AsSpan(pos));
    }
}
