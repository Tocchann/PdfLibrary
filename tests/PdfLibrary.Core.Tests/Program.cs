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
}
