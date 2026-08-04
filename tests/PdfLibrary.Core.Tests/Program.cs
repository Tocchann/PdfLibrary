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

        var annotationDocument = PdfDocument.Create();
        annotationDocument.AddPage(new PdfDictionary
        {
            ["MediaBox"] = new PdfArray
            {
                new PdfNumber(0),
                new PdfNumber(0),
                new PdfNumber(220),
                new PdfNumber(220),
            },
        });
        var annotation = annotationDocument.AddAnnotation(0, new PdfDictionary
        {
            ["Subtype"] = new PdfName("Text"),
            ["Rect"] = new PdfArray
            {
                new PdfNumber(10),
                new PdfNumber(10),
                new PdfNumber(50),
                new PdfNumber(50),
            },
            ["Contents"] = new PdfString("メモ"),
        });
        var annotationDocumentText = Encoding.UTF8.GetString(annotationDocument.Save());
        Assert.True(annotationDocumentText.Contains("/Annots", StringComparison.Ordinal), "注釈の保存に Annots がありません。");
        Assert.True(annotationDocumentText.Contains("/Subtype /Text", StringComparison.Ordinal), "注釈の保存に Subtype がありません。");
        Assert.Equal(1, annotationDocument.GetAnnotations(0).Count, "注釈の取得件数が一致しません。");
        Assert.True(annotationDocument.RemoveAnnotation(0, annotation.Reference), "注釈の削除に失敗しました。");
        Assert.Equal(0, annotationDocument.GetAnnotations(0).Count, "注釈削除後も Annots が残っています。");

        var removedPageDocument = PdfDocument.Create();
        removedPageDocument.AddPage(new PdfDictionary
        {
            ["MediaBox"] = new PdfArray
            {
                new PdfNumber(0),
                new PdfNumber(0),
                new PdfNumber(200),
                new PdfNumber(200),
            },
        });
        removedPageDocument.AddAnnotation(0, new PdfDictionary
        {
            ["Subtype"] = new PdfName("Text"),
            ["Rect"] = new PdfArray
            {
                new PdfNumber(5),
                new PdfNumber(5),
                new PdfNumber(20),
                new PdfNumber(20),
            },
            ["Contents"] = new PdfString("削除対象"),
        });
        removedPageDocument.RemovePageAt(0);
        var removedPageText = Encoding.UTF8.GetString(removedPageDocument.Save());
        Assert.True(!removedPageText.Contains("削除対象", StringComparison.Ordinal), "削除したページの注釈が残っています。");

        var outlinePageDocument = PdfDocument.Create();
        var outlinePage = outlinePageDocument.AddPage(new PdfDictionary
        {
            ["MediaBox"] = new PdfArray
            {
                new PdfNumber(0),
                new PdfNumber(0),
                new PdfNumber(595),
                new PdfNumber(842),
            },
        });
        outlinePageDocument.SetOutlines(new[]
        {
            new PdfOutlineItem("Chapter 1")
            {
                Destination = new PdfArray
                {
                    outlinePage.Reference,
                    new PdfName("Fit"),
                },
            },
            new PdfOutlineItem("Chapter 2")
            {
                Destination = new PdfArray
                {
                    outlinePage.Reference,
                    new PdfName("Fit"),
                },
                Children =
                {
                    new PdfOutlineItem("Section 2.1")
                    {
                        Destination = new PdfArray
                        {
                            outlinePage.Reference,
                            new PdfName("Fit"),
                        },
                    },
                },
            },
        });
        var outlineBytes = outlinePageDocument.Save();
        var outlineText = Encoding.UTF8.GetString(outlineBytes);
        Assert.True(outlineText.Contains("/Outlines", StringComparison.Ordinal), "しおりの保存に Outlines がありません。");
        Assert.True(outlineText.Contains("(Chapter 1)", StringComparison.Ordinal), "しおりの保存にタイトルがありません。");

        var loadedOutlineDocument = PdfDocument.Load(outlineBytes);
        Assert.True(
            loadedOutlineDocument.CatalogDictionary.TryGetValue("Outlines", out var outlinesValue) && outlinesValue is PdfReference,
            "しおりの読込に失敗しました。");
        loadedOutlineDocument.ClearOutlines();
        var clearedOutlineText = Encoding.UTF8.GetString(loadedOutlineDocument.Save());
        Assert.True(!clearedOutlineText.Contains("/Outlines", StringComparison.Ordinal), "しおり削除後も Outlines が残っています。");

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

        // AcroForm: テキストフィールドの追加・取得・値変更・削除
        var formDocument = PdfDocument.Create();
        var formPage = formDocument.AddPage(new PdfDictionary
        {
            ["MediaBox"] = new PdfArray
            {
                new PdfNumber(0),
                new PdfNumber(0),
                new PdfNumber(595),
                new PdfNumber(842),
            },
        });
        var textField = formDocument.AddFormField(new PdfFormField("名前", PdfFormFieldType.Text)
        {
            Value = "テスト",
            PageIndex = 0,
            Rect = new PdfArray
            {
                new PdfNumber(50),
                new PdfNumber(700),
                new PdfNumber(250),
                new PdfNumber(720),
            },
        });
        formDocument.AddFormField(new PdfFormField("チェック1", PdfFormFieldType.Button)
        {
            PageIndex = 0,
            Rect = new PdfArray
            {
                new PdfNumber(50),
                new PdfNumber(650),
                new PdfNumber(70),
                new PdfNumber(670),
            },
        });
        var formBytes = formDocument.Save();
        var formText = Encoding.UTF8.GetString(formBytes);
        Assert.True(formText.Contains("/AcroForm", StringComparison.Ordinal), "フォームの保存に AcroForm がありません。");
        Assert.True(formText.Contains("/FT /Tx", StringComparison.Ordinal), "テキストフィールドの保存に FT がありません。");
        Assert.Equal(2, formDocument.GetFormFields().Count, "フォームフィールドの取得件数が一致しません。");

        Assert.True(formDocument.SetFieldValue(textField.Reference, "更新値"), "フィールド値の更新に失敗しました。");
        var updatedFormText = Encoding.UTF8.GetString(formDocument.Save());
        Assert.True(updatedFormText.Contains("更新値", StringComparison.Ordinal), "フィールド値の更新が保存されていません。");

        Assert.True(formDocument.RemoveFormField(textField.Reference), "フォームフィールドの削除に失敗しました。");
        Assert.Equal(1, formDocument.GetFormFields().Count, "フィールド削除後の件数が一致しません。");

        // AcroForm round-trip
        var loadedFormDocument = PdfDocument.Load(formBytes);
        Assert.True(
            loadedFormDocument.CatalogDictionary.TryGetValue("AcroForm", out var loadedAcroFormValue) && loadedAcroFormValue is PdfReference,
            "フォームの読込に失敗しました。");
        Assert.True(loadedFormDocument.AcroForm is not null, "AcroForm の内部状態が読み込まれていません。");

        // AcroForm 保存前検証エラー（不正なフィールド）
        var invalidFormDocument = PdfDocument.Create();
        invalidFormDocument.AddPage(new PdfDictionary
        {
            ["MediaBox"] = new PdfArray { new PdfNumber(0), new PdfNumber(0), new PdfNumber(200), new PdfNumber(200) },
        });
        var invalidAcroForm = invalidFormDocument.AddObject(new PdfDictionary
        {
            ["Fields"] = new PdfArray
            {
                invalidFormDocument.AddObject(new PdfDictionary
                {
                    ["FT"] = new PdfName("Tx"),
                }).Reference,
            },
        });
        ((PdfDictionary)invalidFormDocument.CatalogDictionary)["AcroForm"] = invalidAcroForm.Reference;
        var invalidFormIssues = validator.Validate(invalidFormDocument);
        Assert.ContainsIssue(invalidFormIssues, "CHK-003", "フィールド名なしの CHK-003 検出に失敗しました。");

        // 添付ファイル: 追加・取得・削除
        var attachDocument = PdfDocument.Create();
        attachDocument.AddPage(new PdfDictionary
        {
            ["MediaBox"] = new PdfArray
            {
                new PdfNumber(0),
                new PdfNumber(0),
                new PdfNumber(595),
                new PdfNumber(842),
            },
        });
        attachDocument.AddEmbeddedFile("サンプル.txt", Encoding.UTF8.GetBytes("hello"), "text/plain");
        var attachBytes = attachDocument.Save();
        var attachText = Encoding.UTF8.GetString(attachBytes);
        Assert.True(attachText.Contains("/EmbeddedFiles", StringComparison.Ordinal), "添付ファイルの保存に EmbeddedFiles がありません。");
        Assert.Equal(1, attachDocument.GetEmbeddedFileNames().Count, "添付ファイルの取得件数が一致しません。");
        Assert.Equal("サンプル.txt", attachDocument.GetEmbeddedFileNames()[0], "添付ファイル名が一致しません。");

        Assert.True(attachDocument.RemoveEmbeddedFile("サンプル.txt"), "添付ファイルの削除に失敗しました。");
        Assert.Equal(0, attachDocument.GetEmbeddedFileNames().Count, "削除後も添付ファイルが残っています。");
        var removedAttachText = Encoding.UTF8.GetString(attachDocument.Save());
        Assert.True(!removedAttachText.Contains("/EmbeddedFiles", StringComparison.Ordinal), "削除後も EmbeddedFiles が残っています。");

        // 添付ファイル round-trip
        var loadedAttachDocument = PdfDocument.Load(attachBytes);
        Assert.True(
            loadedAttachDocument.CatalogDictionary.TryGetValue("Names", out _),
            "添付ファイルの読込に失敗しました。");
        Assert.True(loadedAttachDocument.EmbeddedFilesNameTree is not null, "EmbeddedFilesNameTree の内部状態が読み込まれていません。");

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
