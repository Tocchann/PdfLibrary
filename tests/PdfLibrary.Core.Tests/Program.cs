using System.Text;
using PdfLibrary.Core;

internal static class Program
{
    private static void Main()
    {
        var document = PdfDocument.Create();
        document.CatalogDictionary["Lang"] = new PdfString("ja-JP");

        var info = new PdfDictionary
        {
            ["Producer"] = new PdfString("PdfLibrary"),
        };
        document.SetInfo(info);

        document.AddPage(new PdfDictionary
        {
            ["MediaBox"] = new PdfArray
            {
                new PdfNumber(0),
                new PdfNumber(0),
                new PdfNumber(595),
                new PdfNumber(842),
            },
        });

        var overwriteBytes = document.Save(new PdfSaveOptions { Mode = PdfSaveMode.Overwrite });
        var overwriteText = Encoding.UTF8.GetString(overwriteBytes);

        Assert.True(overwriteText.Contains("%PDF-1.7", StringComparison.Ordinal), "PDF ヘッダがありません。");
        Assert.True(overwriteText.Contains("xref", StringComparison.Ordinal), "xref がありません。");
        Assert.True(overwriteText.Contains("startxref", StringComparison.Ordinal), "startxref がありません。");

        var loaded = PdfDocument.Load(overwriteBytes);
        Assert.True(Math.Abs(((PdfNumber)loaded.PagesDictionary["Count"]).Value - 1) < 0.0001, "ページ数が一致しません。");
        Assert.Equal("PdfLibrary", ((PdfString)((PdfDictionary)loaded.Info!.Value)["Producer"]).Value, "Info が復元できません。");

        loaded.AddPage(new PdfDictionary
        {
            ["MediaBox"] = new PdfArray
            {
                new PdfNumber(0),
                new PdfNumber(0),
                new PdfNumber(612),
                new PdfNumber(792),
            },
        });

        var appendBytes = loaded.Save(new PdfSaveOptions { Mode = PdfSaveMode.Append });
        var appendText = Encoding.UTF8.GetString(appendBytes);

        Assert.True(appendText.Contains("/Prev", StringComparison.Ordinal), "追記保存に /Prev がありません。");
        Assert.True(appendText.Split("startxref", StringSplitOptions.None).Length >= 3, "追記保存が複数リビジョンになっていません。");

        var validator = new PdfPreSaveValidator();
        Assert.Equal(0, validator.Validate(document).Count, "保存前検証で想定外のエラーが出ました。");

        var sparsePdfText = "%PDF-1.7\n" +
                            "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n" +
                            "2 0 obj\n<< /Type /Pages /Kids [10 0 R] /Count 1 >>\nendobj\n" +
                            "10 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] >>\nendobj\n" +
                            "xref\n0 11\n" +
                            "0000000000 65535 f \n" +
                            "trailer\n<< /Size 11 /Root 1 0 R >>\n" +
                            "startxref\n0\n%%EOF\n";

        var sparseDocument = PdfDocument.Load(Encoding.UTF8.GetBytes(sparsePdfText));
        sparseDocument.AddPage(new PdfDictionary
        {
            ["MediaBox"] = new PdfArray
            {
                new PdfNumber(0),
                new PdfNumber(0),
                new PdfNumber(400),
                new PdfNumber(400),
            },
        });

        var sparseOverwriteBytes = sparseDocument.Save(new PdfSaveOptions { Mode = PdfSaveMode.Overwrite });
        var sparseOverwriteText = Encoding.UTF8.GetString(sparseOverwriteBytes);
        Assert.True(sparseOverwriteText.Contains("11 0 obj", StringComparison.Ordinal), "疎なオブジェクト番号の次番号採番に失敗しています。");
        Assert.True(sparseOverwriteText.Contains("/Size 12", StringComparison.Ordinal), "xref の Size が最大オブジェクト番号基準になっていません。");

        var noStartXrefText = "%PDF-1.7\n1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n2 0 obj\n<< /Type /Pages /Kids [] /Count 0 >>\nendobj\n%%EOF\n";
        var noStartXrefDocument = PdfDocument.Load(Encoding.UTF8.GetBytes(noStartXrefText));
        Assert.Throws<InvalidOperationException>(
            () => noStartXrefDocument.Save(new PdfSaveOptions { Mode = PdfSaveMode.Append }),
            "startxref がない文書で追記保存できてしまいました。");

        var invalidAnnotationDocument = PdfDocument.Create();
        var annotationPage = invalidAnnotationDocument.AddPage(new PdfDictionary
        {
            ["MediaBox"] = new PdfArray
            {
                new PdfNumber(0),
                new PdfNumber(0),
                new PdfNumber(200),
                new PdfNumber(200),
            },
        });
        var invalidAnnot = invalidAnnotationDocument.AddObject(new PdfDictionary
        {
            ["Type"] = new PdfName("Annot"),
            ["Subtype"] = new PdfName("Text"),
            ["Rect"] = new PdfArray
            {
                new PdfNumber(0),
                new PdfNumber(0),
                new PdfNumber(100),
            },
        });
        ((PdfDictionary)annotationPage.Value)["Annots"] = new PdfArray { invalidAnnot.Reference };
        var invalidAnnotationIssues = validator.Validate(invalidAnnotationDocument);
        Assert.ContainsIssue(invalidAnnotationIssues, "CHK-004", "不正な注釈の CHK-004 検出に失敗しました。");

        var invalidSignatureDocument = PdfDocument.Create();
        invalidSignatureDocument.AddObject(new PdfDictionary
        {
            ["Type"] = new PdfName("Sig"),
            ["ByteRange"] = new PdfArray
            {
                new PdfNumber(0),
                new PdfNumber(100),
                new PdfNumber(50),
                new PdfNumber(100),
            },
            ["Contents"] = new PdfString("00"),
        });
        var invalidSignatureIssues = validator.Validate(invalidSignatureDocument);
        Assert.ContainsIssue(invalidSignatureIssues, "CHK-005", "不正な ByteRange の CHK-005 検出に失敗しました。");

        var encryptedPdfText = "%PDF-1.7\n" +
                               "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n" +
                               "2 0 obj\n<< /Type /Pages /Kids [] /Count 0 >>\nendobj\n" +
                               "5 0 obj\n<< /Filter /Standard >>\nendobj\n" +
                               "trailer\n<< /Size 6 /Root 1 0 R /Encrypt 5 0 R >>\n" +
                               "startxref\n0\n%%EOF\n";
        var encryptedDocument = PdfDocument.Load(Encoding.UTF8.GetBytes(encryptedPdfText));
        var encryptedIssues = validator.Validate(encryptedDocument);
        Assert.ContainsIssue(encryptedIssues, "CHK-006", "暗号化文書の CHK-006 検出に失敗しました。");

        Console.WriteLine("PdfLibrary.Core tests passed.");
    }
}

internal static class Assert
{
    public static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void Equal<T>(T expected, T actual, string message)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message} Expected: {expected}, Actual: {actual}");
        }
    }

    public static void Throws<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    public static void ContainsIssue(IReadOnlyList<PdfValidationIssue> issues, string checkId, string message)
    {
        if (!issues.Any(item => string.Equals(item.CheckId, checkId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(message);
        }
    }
}
