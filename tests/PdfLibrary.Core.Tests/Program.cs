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

        // --- Wave 4: SIGN-001 署名フィールド管理 ---
        TestSigning();

        // --- XMP Metadata (14.3) ---
        TestXmpMetadata();

        Console.WriteLine("PdfLibrary.Core tests passed.");
    }

    private static void TestSigning()
    {
        // --- Wave 4: SIGN-001 署名フィールド管理 ---

        // 基本的な署名準備フロー
        var sigDoc = PdfDocument.Create();
        sigDoc.AddPage(new PdfDictionary
        {
            ["MediaBox"] = new PdfArray { new PdfNumber(0), new PdfNumber(0), new PdfNumber(595), new PdfNumber(842) },
        });

        var options = new PdfLibrary.Extensions.Signing.PdfSignatureOptions
        {
            FieldName = "Sig1",
            PageIndex = 0,
            ContentsReserveSize = 1024,
        };

        var context = PdfLibrary.Extensions.Signing.PdfSigningSession.Prepare(sigDoc, options);

        Assert.True(context.PreparedBytes.Length > 0, "準備済みバイト列が空です。");
        Assert.Equal(4, context.ByteRange.Length, "ByteRange の要素数が 4 でありません。");
        Assert.Equal(0L, context.ByteRange[0], "ByteRange[0] は 0 でなければなりません。");
        Assert.True(context.ByteRange[1] > 0, "ByteRange[1] は正の値でなければなりません。");
        Assert.True(context.ByteRange[2] > context.ByteRange[1], "ByteRange[2] > ByteRange[1] でなければなりません。");
        Assert.True(context.ByteRange[3] > 0, "ByteRange[3] は正の値でなければなりません。");
        Assert.Equal(
            (long)context.PreparedBytes.Length,
            context.ByteRange[2] + context.ByteRange[3],
            "ByteRange[2] + ByteRange[3] はファイルサイズでなければなりません。");
        Assert.Equal(context.ContentsHexLength, options.ContentsReserveSize * 2, "ContentsHexLength が一致しません。");

        // /Contents 位置の検証
        Assert.True(context.ContentsDataStart > 0, "ContentsDataStart が 0 です。");
        Assert.Equal(context.ByteRange[1], context.ContentsDataStart - 1, "ContentsDataStart は ByteRange[1]+1 でなければなりません。");

        // 署名対象バイト列の検証
        var signedContent = PdfLibrary.Extensions.Signing.PdfSigningSession.ExtractSignedContent(context);
        Assert.True(signedContent.Length > 0, "署名対象バイト列が空です。");
        Assert.Equal(
            context.ByteRange[1] + context.ByteRange[3],
            (long)signedContent.Length,
            "署名対象バイト列の長さが ByteRange と一致しません。");

        // Apply: モック CMS バイト列で署名適用
        var fakeCms = new byte[] { 0x30, 0x82, 0x01, 0x00 };
        var signedBytes = PdfLibrary.Extensions.Signing.PdfSigningSession.Apply(context, fakeCms);

        Assert.Equal(context.PreparedBytes.Length, signedBytes.Length, "署名済みバイト列の長さが変わりました。");
        var signedText = Encoding.ASCII.GetString(signedBytes, (int)context.ContentsDataStart, fakeCms.Length * 2);
        Assert.Equal(Convert.ToHexString(fakeCms), signedText, "署名済みバイト列内の /Contents が一致しません。");

        // プレースホルダ超過エラー
        var tooLargeCms = new byte[options.ContentsReserveSize + 1];
        Assert.Throws<PdfLibrary.Extensions.Signing.PdfSigningException>(
            () => PdfLibrary.Extensions.Signing.PdfSigningSession.Apply(context, tooLargeCms),
            "プレースホルダを超える CMS で例外が発生しませんでした。");

        // ページ範囲外エラー
        var badOptions = new PdfLibrary.Extensions.Signing.PdfSignatureOptions { PageIndex = 99 };
        Assert.Throws<PdfLibrary.Extensions.Signing.PdfSigningException>(
            () => PdfLibrary.Extensions.Signing.PdfSigningSession.Prepare(sigDoc, badOptions),
            "ページ範囲外で例外が発生しませんでした。");

        // PdfHexString の読み書き round-trip
        var hexDoc = PdfDocument.Create();
        hexDoc.AddPage(new PdfDictionary
        {
            ["MediaBox"] = new PdfArray { new PdfNumber(0), new PdfNumber(0), new PdfNumber(595), new PdfNumber(842) },
        });
        var hexData = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        hexDoc.CatalogDictionary["TestHex"] = new PdfHexString(hexData);
        var hexBytes = hexDoc.Save();
        var hexText = Encoding.UTF8.GetString(hexBytes);
        Assert.True(hexText.Contains("<DEADBEEF>", StringComparison.OrdinalIgnoreCase), "/TestHex の hex 文字列が見つかりません。");

        var reloadedHex = PdfDocument.Load(hexBytes);
        Assert.True(
            reloadedHex.CatalogDictionary.TryGetValue("TestHex", out var reloadedValue) && reloadedValue is PdfHexString,
            "PdfHexString の round-trip に失敗しました。");
        var reloadedHexData = ((PdfHexString)reloadedValue!).Data;
        Assert.True(reloadedHexData.SequenceEqual(hexData), "PdfHexString の round-trip 後のデータが一致しません。");
    }

    private static void TestXmpMetadata()
    {
        // SetXmpMetadata / GetXmpMetadata の round-trip
        var xmpDoc = PdfDocument.Create();
        xmpDoc.AddPage(new PdfDictionary
        {
            ["MediaBox"] = new PdfArray { new PdfNumber(0), new PdfNumber(0), new PdfNumber(595), new PdfNumber(842) },
        });

        Assert.True(xmpDoc.GetXmpMetadata() is null, "初期状態で XMP が存在しています。");

        var xmpXml = Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><x:xmpmeta xmlns:x=\"adobe:ns:meta/\"><rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\"><rdf:Description rdf:about=\"\"/></rdf:RDF></x:xmpmeta>");
        xmpDoc.SetXmpMetadata(xmpXml);

        var retrieved = xmpDoc.GetXmpMetadata();
        Assert.True(retrieved is not null, "SetXmpMetadata 後に GetXmpMetadata が null を返しました。");
        var retrievedBytes = retrieved!;
        Assert.True(retrievedBytes.SequenceEqual(xmpXml), "GetXmpMetadata のデータが一致しません。");
        Assert.True(xmpDoc.CatalogDictionary.ContainsKey("Metadata"), "/Catalog/Metadata が設定されていません。");
        retrievedBytes[0] ^= 0x01;
        Assert.True(xmpDoc.GetXmpMetadata()!.SequenceEqual(xmpXml), "GetXmpMetadata が内部配列を露出しています。");

        // 保存 → 読み込み round-trip
        var xmpBytes = xmpDoc.Save();
        var xmpText = Encoding.UTF8.GetString(xmpBytes);
        Assert.True(xmpText.Contains("x:xmpmeta", StringComparison.Ordinal), "保存された PDF に XMP ストリームがありません。");

        var reloadedXmp = PdfDocument.Load(xmpBytes);
        Assert.True(reloadedXmp.Metadata is not null, "読み込み後に Metadata が null です。");
        var reloadedData = reloadedXmp.GetXmpMetadata();
        Assert.True(reloadedData is not null, "読み込み後の GetXmpMetadata が null です。");
        Assert.True(Encoding.UTF8.GetString(reloadedData!).Contains("x:xmpmeta", StringComparison.Ordinal), "読み込んだ XMP データが不正です。");

        // SyncXmpFromInfo テスト
        var syncDoc = PdfDocument.Create();
        syncDoc.AddPage(new PdfDictionary
        {
            ["MediaBox"] = new PdfArray { new PdfNumber(0), new PdfNumber(0), new PdfNumber(595), new PdfNumber(842) },
        });
        syncDoc.SetInfo(new PdfDictionary
        {
            ["Title"] = new PdfString("テストドキュメント"),
            ["Author"] = new PdfString("テスト太郎"),
            ["Producer"] = new PdfString("PdfLibrary"),
        });

        syncDoc.SyncXmpFromInfo();

        var syncedXmp = syncDoc.GetXmpMetadata();
        Assert.True(syncedXmp is not null, "SyncXmpFromInfo 後に GetXmpMetadata が null です。");
        var syncedText = Encoding.UTF8.GetString(syncedXmp!);
        Assert.True(syncedText.Contains("テストドキュメント", StringComparison.Ordinal), "XMP に Title が含まれていません。");
        Assert.True(syncedText.Contains("テスト太郎", StringComparison.Ordinal), "XMP に Author が含まれていません。");
        Assert.True(syncedText.Contains("PdfLibrary", StringComparison.Ordinal), "XMP に Producer が含まれていません。");
        Assert.True(syncedText.Contains("xmpmeta", StringComparison.Ordinal), "生成された XMP が x:xmpmeta を含みません。");

        // Info なしで SyncXmpFromInfo を呼んでも例外が出ないことを確認
        var noInfoDoc = PdfDocument.Create();
        noInfoDoc.AddPage(new PdfDictionary
        {
            ["MediaBox"] = new PdfArray { new PdfNumber(0), new PdfNumber(0), new PdfNumber(595), new PdfNumber(842) },
        });
        noInfoDoc.SyncXmpFromInfo();
        Assert.True(noInfoDoc.GetXmpMetadata() is null, "Info なしの SyncXmpFromInfo が XMP を生成しました。");

        // SetXmpMetadata の上書き確認
        var updatedXml = Encoding.UTF8.GetBytes("<updated/>");
        xmpDoc.SetXmpMetadata(updatedXml);
        Assert.True(xmpDoc.GetXmpMetadata()!.SequenceEqual(updatedXml), "SetXmpMetadata の上書きが反映されていません。");
        updatedXml[0] = (byte)'X';
        Assert.True(Encoding.UTF8.GetString(xmpDoc.GetXmpMetadata()!).StartsWith("<updated/>", StringComparison.Ordinal), "SetXmpMetadata が入力配列を保持しています。");

        // Catalog の Metadata 参照が消えていても SetXmpMetadata で再同期されることを確認
        xmpDoc.CatalogDictionary.Remove("Metadata");
        xmpDoc.SetXmpMetadata(Encoding.UTF8.GetBytes("<resynced/>"));
        Assert.True(xmpDoc.CatalogDictionary.ContainsKey("Metadata"), "SetXmpMetadata が /Catalog/Metadata を再同期していません。");
        Assert.True(Encoding.UTF8.GetString(xmpDoc.GetXmpMetadata()!).StartsWith("<resynced/>", StringComparison.Ordinal), "Catalog 再同期後の Metadata が一致しません。");

        // PdfHexString の Info 値も XMP 同期できることを確認
        var hexInfoDoc = PdfDocument.Create();
        hexInfoDoc.AddPage(new PdfDictionary
        {
            ["MediaBox"] = new PdfArray { new PdfNumber(0), new PdfNumber(0), new PdfNumber(595), new PdfNumber(842) },
        });
        hexInfoDoc.SetInfo(new PdfDictionary
        {
            ["Title"] = new PdfHexString(Encoding.UTF8.GetBytes("16進タイトル")),
            ["Author"] = new PdfHexString(Encoding.UTF8.GetBytes("16進著者")),
        });
        hexInfoDoc.SyncXmpFromInfo();
        var hexSyncedText = Encoding.UTF8.GetString(hexInfoDoc.GetXmpMetadata()!);
        Assert.True(hexSyncedText.Contains("16進タイトル", StringComparison.Ordinal), "PdfHexString の Title が XMP に同期されていません。");
        Assert.True(hexSyncedText.Contains("16進著者", StringComparison.Ordinal), "PdfHexString の Author が XMP に同期されていません。");

        // ClearXmpMetadata で Metadata と /Catalog/Metadata 参照が掃除されることを確認
        var removeDoc = PdfDocument.Create();
        removeDoc.AddPage(new PdfDictionary
        {
            ["MediaBox"] = new PdfArray { new PdfNumber(0), new PdfNumber(0), new PdfNumber(595), new PdfNumber(842) },
        });
        removeDoc.SetXmpMetadata(Encoding.UTF8.GetBytes("<meta/>"));
        var removed = removeDoc.ClearXmpMetadata();
        Assert.True(removed, "Metadata オブジェクトの削除に失敗しました。");
        Assert.True(removeDoc.Metadata is null, "Metadata 状態がクリアされていません。");
        Assert.True(!removeDoc.CatalogDictionary.ContainsKey("Metadata"), "/Catalog/Metadata が削除されていません。");
        Assert.True(!removeDoc.ClearXmpMetadata(), "Metadata がない状態で ClearXmpMetadata が true を返しました。");

        // Metadata 状態がなくても /Catalog/Metadata の孤立参照を掃除した場合は true を返すことを確認
        var orphanDoc = PdfDocument.Create();
        orphanDoc.AddPage(new PdfDictionary
        {
            ["MediaBox"] = new PdfArray { new PdfNumber(0), new PdfNumber(0), new PdfNumber(595), new PdfNumber(842) },
        });
        orphanDoc.CatalogDictionary["Metadata"] = new PdfReference(9999);
        Assert.True(orphanDoc.ClearXmpMetadata(), "孤立した /Catalog/Metadata の削除が true を返しません。");
        Assert.True(!orphanDoc.CatalogDictionary.ContainsKey("Metadata"), "孤立した /Catalog/Metadata が削除されていません。");

        // /Type /Subtype が一致しない stream は Metadata として採用しないことを確認
        var invalidMetadataDoc = PdfDocument.Create();
        invalidMetadataDoc.AddPage(new PdfDictionary
        {
            ["MediaBox"] = new PdfArray { new PdfNumber(0), new PdfNumber(0), new PdfNumber(595), new PdfNumber(842) },
        });
        var invalidMetadataStream = new PdfStream(
            new PdfDictionary
            {
                ["Type"] = new PdfName("NotMetadata"),
                ["Subtype"] = new PdfName("XML"),
            },
            Encoding.UTF8.GetBytes("<not-xmp/>"));
        var invalidMetadataObject = invalidMetadataDoc.AddObject(invalidMetadataStream);
        invalidMetadataDoc.CatalogDictionary["Metadata"] = invalidMetadataObject.Reference;
        var invalidMetadataBytes = invalidMetadataDoc.Save();
        var reloadedInvalidMetadataDoc = PdfDocument.Load(invalidMetadataBytes);
        Assert.True(reloadedInvalidMetadataDoc.Metadata is null, "不正な Metadata stream が Metadata として採用されました。");
        Assert.True(reloadedInvalidMetadataDoc.GetXmpMetadata() is null, "不正な Metadata stream のデータが取得できてしまいます。");

        Assert.True(syncedText.Contains("begin=\"\uFEFF\"", StringComparison.Ordinal), "xpacket begin に UTF-8 BOM 文字が設定されていません。");
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
