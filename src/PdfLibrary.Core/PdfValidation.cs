namespace PdfLibrary.Core;

public sealed record PdfValidationIssue(string CheckId, string Message);

public sealed class PdfPreSaveValidator
{
    public IReadOnlyList<PdfValidationIssue> Validate(PdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var issues = new List<PdfValidationIssue>();

        if (!(document.CatalogDictionary.TryGetValue("Pages", out var pagesValue) && pagesValue is PdfReference pagesRef && pagesRef.ObjectNumber == document.Pages.ObjectNumber))
        {
            issues.Add(new PdfValidationIssue("CHK-001", "Catalog から Pages への参照が解決できません。"));
        }

        if (document.PagesDictionary.TryGetValue("Count", out var countValue) && countValue is PdfNumber countNumber)
        {
            var kids = (PdfArray)document.PagesDictionary["Kids"];
            if ((int)countNumber.Value != kids.Count)
            {
                issues.Add(new PdfValidationIssue("CHK-002", "Page Tree の Count が Kids 数と一致しません。"));
            }
        }
        else
        {
            issues.Add(new PdfValidationIssue("CHK-002", "Page Tree の Count が見つかりません。"));
        }

        var objectNumberSet = document.Objects.Select(item => item.ObjectNumber).ToHashSet();
        var objectsByReference = document.Objects.ToDictionary(item => (item.ObjectNumber, item.GenerationNumber));
        foreach (var indirect in document.Objects)
        {
            foreach (var reference in EnumerateReferences(indirect.Value))
            {
                if (!objectNumberSet.Contains(reference.ObjectNumber))
                {
                    issues.Add(new PdfValidationIssue("CHK-003", $"Object {indirect.ObjectNumber} から参照される Object {reference.ObjectNumber} が解決できません。"));
                }
            }

            if (indirect.Value is PdfDictionary dictionary)
            {
                ValidateAnnotation(indirect.ObjectNumber, dictionary, issues);
                ValidateSignature(dictionary, issues);
            }
        }

        if (document.HasEncryption)
        {
            issues.Add(new PdfValidationIssue("CHK-006", "暗号化文書の編集は未対応です。"));
        }

        ValidateOutlines(document, objectsByReference, issues);
        ValidateAcroForm(document, objectsByReference, issues);
        ValidateEmbeddedFiles(document, objectsByReference, issues);

        return issues;
    }

    private static IEnumerable<PdfReference> EnumerateReferences(PdfValue value)
    {
        if (value is PdfReference reference)
        {
            yield return reference;
            yield break;
        }

        if (value is PdfArray array)
        {
            foreach (var item in array)
            {
                foreach (var childReference in EnumerateReferences(item))
                {
                    yield return childReference;
                }
            }

            yield break;
        }

        if (value is PdfDictionary dictionary)
        {
            foreach (var entry in dictionary)
            {
                foreach (var childReference in EnumerateReferences(entry.Value))
                {
                    yield return childReference;
                }
            }

            yield break;
        }

        if (value is PdfStream stream)
        {
            foreach (var childReference in EnumerateReferences(stream.Dictionary))
            {
                yield return childReference;
            }
        }
    }

    private static void ValidateAnnotation(int objectNumber, PdfDictionary dictionary, List<PdfValidationIssue> issues)
    {
        if (!(dictionary.TryGetValue("Type", out var typeValue) &&
              typeValue is PdfName typeName &&
              string.Equals(typeName.Value, "Annot", StringComparison.Ordinal)))
        {
            return;
        }

        if (!(dictionary.TryGetValue("Subtype", out var subtypeValue) && subtypeValue is PdfName subtypeName))
        {
            issues.Add(new PdfValidationIssue("CHK-004", $"Object {objectNumber} の注釈に /Subtype がありません。"));
            return;
        }

        if (!(dictionary.TryGetValue("Rect", out var rectValue) &&
              rectValue is PdfArray rectArray &&
              rectArray.Count == 4 &&
              rectArray.All(item => item is PdfNumber)))
        {
            issues.Add(new PdfValidationIssue("CHK-004", $"Object {objectNumber} の注釈 /Rect は4要素の数値配列である必要があります。"));
        }

        if (string.Equals(subtypeName.Value, "Link", StringComparison.Ordinal) &&
            !(dictionary.ContainsKey("A") || dictionary.ContainsKey("Dest")))
        {
            issues.Add(new PdfValidationIssue("CHK-004", $"Object {objectNumber} の Link 注釈は /A または /Dest を持つ必要があります。"));
        }
    }

    private static void ValidateSignature(PdfDictionary dictionary, List<PdfValidationIssue> issues)
    {
        if (!(dictionary.TryGetValue("Type", out var typeValue) &&
              typeValue is PdfName typeName &&
              string.Equals(typeName.Value, "Sig", StringComparison.Ordinal)))
        {
            return;
        }

        if (!(dictionary.TryGetValue("ByteRange", out var byteRangeValue) && byteRangeValue is PdfArray byteRangeArray))
        {
            issues.Add(new PdfValidationIssue("CHK-005", "署名辞書に /ByteRange がありません。"));
            return;
        }

        if (byteRangeArray.Count < 4 || byteRangeArray.Count % 2 != 0)
        {
            issues.Add(new PdfValidationIssue("CHK-005", "署名辞書の /ByteRange は4要素以上の偶数個である必要があります。"));
            return;
        }

        var ranges = new List<(long Start, long End)>(byteRangeArray.Count / 2);
        for (var index = 0; index < byteRangeArray.Count; index += 2)
        {
            if (!(byteRangeArray[index] is PdfNumber startNumber && byteRangeArray[index + 1] is PdfNumber lengthNumber))
            {
                issues.Add(new PdfValidationIssue("CHK-005", "署名辞書の /ByteRange は数値配列である必要があります。"));
                return;
            }

            var start = (long)startNumber.Value;
            var length = (long)lengthNumber.Value;
            if (start < 0 || length < 0)
            {
                issues.Add(new PdfValidationIssue("CHK-005", "署名辞書の /ByteRange は非負値である必要があります。"));
                return;
            }

            ranges.Add((start, start + length));
        }

        var orderedRanges = ranges.OrderBy(item => item.Start).ToArray();
        for (var index = 1; index < orderedRanges.Length; index++)
        {
            if (orderedRanges[index].Start < orderedRanges[index - 1].End)
            {
                issues.Add(new PdfValidationIssue("CHK-005", "署名辞書の /ByteRange が重複しています。"));
                return;
            }
        }
    }

    private static void ValidateOutlines(
        PdfDocument document,
        IReadOnlyDictionary<(int ObjectNumber, int GenerationNumber), PdfIndirectObject> objectsByReference,
        List<PdfValidationIssue> issues)
    {
        if (!(document.CatalogDictionary.TryGetValue("Outlines", out var outlinesValue) && outlinesValue is PdfReference outlinesReference))
        {
            return;
        }

        if (!TryGetObject(objectsByReference, outlinesReference, out var outlinesObject) || outlinesObject is null)
        {
            issues.Add(new PdfValidationIssue("CHK-003", "Catalog から Outlines への参照が解決できません。"));
            return;
        }

        if (outlinesObject.Value is not PdfDictionary outlinesDictionary)
        {
            issues.Add(new PdfValidationIssue("CHK-003", "Outlines ルートが辞書ではありません。"));
            return;
        }

        if (!(outlinesDictionary.TryGetValue("Type", out var typeValue) &&
              typeValue is PdfName typeName &&
              string.Equals(typeName.Value, "Outlines", StringComparison.Ordinal)))
        {
            issues.Add(new PdfValidationIssue("CHK-003", "Outlines ルートの /Type が不正です。"));
            return;
        }

        var visited = new HashSet<(int ObjectNumber, int GenerationNumber)>();
        var totalCount = ValidateOutlineChain(outlinesDictionary, outlinesReference, objectsByReference, visited, issues);

        if (outlinesDictionary.TryGetValue("Count", out var countValue) &&
            countValue is PdfNumber countNumber &&
            (int)countNumber.Value != totalCount)
        {
            issues.Add(new PdfValidationIssue("CHK-003", "Outlines ルートの /Count が実際の件数と一致しません。"));
        }
    }

    private static int ValidateOutlineChain(
        PdfDictionary parentDictionary,
        PdfReference parentReference,
        IReadOnlyDictionary<(int ObjectNumber, int GenerationNumber), PdfIndirectObject> objectsByReference,
        HashSet<(int ObjectNumber, int GenerationNumber)> visited,
        List<PdfValidationIssue> issues)
    {
        var totalCount = 0;
        var currentReference = GetFirstReference(parentDictionary);
        PdfReference? previousReference = null;

        while (currentReference is not null)
        {
            if (!visited.Add((currentReference.ObjectNumber, currentReference.GenerationNumber)))
            {
                issues.Add(new PdfValidationIssue("CHK-003", $"しおりの循環参照が検出されました: {currentReference.ObjectNumber} {currentReference.GenerationNumber} R"));
                return totalCount;
            }

            if (!TryGetObject(objectsByReference, currentReference, out var currentObject) || currentObject is null)
            {
                issues.Add(new PdfValidationIssue("CHK-003", $"しおり項目 {currentReference.ObjectNumber} {currentReference.GenerationNumber} R が解決できません。"));
                return totalCount;
            }

            if (currentObject.Value is not PdfDictionary currentDictionary)
            {
                issues.Add(new PdfValidationIssue("CHK-003", $"しおり項目 {currentReference.ObjectNumber} {currentReference.GenerationNumber} R が辞書ではありません。"));
                return totalCount;
            }

            if (!(currentDictionary.TryGetValue("Title", out var titleValue) && titleValue is PdfString))
            {
                issues.Add(new PdfValidationIssue("CHK-003", $"しおり項目 {currentReference.ObjectNumber} {currentReference.GenerationNumber} R に /Title がありません。"));
            }

            if (!(currentDictionary.TryGetValue("Parent", out var parentValue) &&
                  parentValue is PdfReference actualParent &&
                  actualParent.Equals(parentReference)))
            {
                issues.Add(new PdfValidationIssue("CHK-003", $"しおり項目 {currentReference.ObjectNumber} {currentReference.GenerationNumber} R の /Parent が不正です。"));
            }

            if (previousReference is null)
            {
                if (currentDictionary.ContainsKey("Prev"))
                {
                    issues.Add(new PdfValidationIssue("CHK-003", $"しおり項目 {currentReference.ObjectNumber} {currentReference.GenerationNumber} R の先頭要素に /Prev があります。"));
                }
            }
            else if (!(currentDictionary.TryGetValue("Prev", out var prevValue) &&
                       prevValue is PdfReference previousValueReference &&
                       previousValueReference.Equals(previousReference)))
            {
                issues.Add(new PdfValidationIssue("CHK-003", $"しおり項目 {currentReference.ObjectNumber} {currentReference.GenerationNumber} R の /Prev が不正です。"));
            }

            var childCount = 0;
            var childFirstReference = GetFirstReference(currentDictionary);
            if (childFirstReference is not null)
            {
                if (!(currentDictionary.TryGetValue("Last", out var lastValue) && lastValue is PdfReference))
                {
                    issues.Add(new PdfValidationIssue("CHK-003", $"しおり項目 {currentReference.ObjectNumber} {currentReference.GenerationNumber} R の /Last がありません。"));
                }

                if (!(currentDictionary.TryGetValue("Count", out var countValue) &&
                      countValue is PdfNumber countNumber))
                {
                    issues.Add(new PdfValidationIssue("CHK-003", $"しおり項目 {currentReference.ObjectNumber} {currentReference.GenerationNumber} R の /Count がありません。"));
                }

                childCount = ValidateOutlineChain(currentDictionary, currentReference, objectsByReference, visited, issues);

                if (currentDictionary.TryGetValue("Count", out var validatedCountValue) &&
                    validatedCountValue is PdfNumber validatedCountNumber &&
                    (int)validatedCountNumber.Value != childCount)
                {
                    issues.Add(new PdfValidationIssue("CHK-003", $"しおり項目 {currentReference.ObjectNumber} {currentReference.GenerationNumber} R の /Count が実際の子孫数と一致しません。"));
                }
            }

            totalCount += 1 + childCount;
            previousReference = currentReference;
            currentReference = GetNextReference(currentDictionary);
        }

        return totalCount;
    }

    private static PdfReference? GetFirstReference(PdfDictionary dictionary)
        => dictionary.TryGetValue("First", out var firstValue) && firstValue is PdfReference firstReference ? firstReference : null;

    private static PdfReference? GetNextReference(PdfDictionary dictionary)
        => dictionary.TryGetValue("Next", out var nextValue) && nextValue is PdfReference nextReference ? nextReference : null;

    private static bool TryGetObject(
        IReadOnlyDictionary<(int ObjectNumber, int GenerationNumber), PdfIndirectObject> objectsByReference,
        PdfReference reference,
        out PdfIndirectObject? indirectObject)
        => objectsByReference.TryGetValue((reference.ObjectNumber, reference.GenerationNumber), out indirectObject);

    private static void ValidateAcroForm(
        PdfDocument document,
        IReadOnlyDictionary<(int ObjectNumber, int GenerationNumber), PdfIndirectObject> objectsByReference,
        List<PdfValidationIssue> issues)
    {
        if (!(document.CatalogDictionary.TryGetValue("AcroForm", out var acroFormValue) && acroFormValue is PdfReference acroFormReference))
        {
            return;
        }

        if (!TryGetObject(objectsByReference, acroFormReference, out var acroFormObject) || acroFormObject is null)
        {
            issues.Add(new PdfValidationIssue("CHK-003", "Catalog から AcroForm への参照が解決できません。"));
            return;
        }

        if (acroFormObject.Value is not PdfDictionary acroFormDictionary)
        {
            issues.Add(new PdfValidationIssue("CHK-003", "AcroForm が辞書ではありません。"));
            return;
        }

        if (!(acroFormDictionary.TryGetValue("Fields", out var fieldsValue) && fieldsValue is PdfArray fields))
        {
            issues.Add(new PdfValidationIssue("CHK-003", "AcroForm に /Fields がありません。"));
            return;
        }

        foreach (var item in fields)
        {
            if (item is not PdfReference fieldReference)
            {
                issues.Add(new PdfValidationIssue("CHK-003", "AcroForm の /Fields に参照以外の要素があります。"));
                continue;
            }

            if (!TryGetObject(objectsByReference, fieldReference, out var fieldObject) || fieldObject is null)
            {
                issues.Add(new PdfValidationIssue("CHK-003", $"AcroForm のフィールド {fieldReference.ObjectNumber} {fieldReference.GenerationNumber} R が解決できません。"));
                continue;
            }

            if (fieldObject.Value is not PdfDictionary fieldDictionary)
            {
                issues.Add(new PdfValidationIssue("CHK-003", $"AcroForm のフィールド {fieldReference.ObjectNumber} {fieldReference.GenerationNumber} R が辞書ではありません。"));
                continue;
            }

            if (!(fieldDictionary.TryGetValue("FT", out var ftValue) && ftValue is PdfName ftName &&
                  (string.Equals(ftName.Value, "Tx", StringComparison.Ordinal) ||
                   string.Equals(ftName.Value, "Btn", StringComparison.Ordinal) ||
                   string.Equals(ftName.Value, "Ch", StringComparison.Ordinal) ||
                   string.Equals(ftName.Value, "Sig", StringComparison.Ordinal))))
            {
                issues.Add(new PdfValidationIssue("CHK-003", $"AcroForm のフィールド {fieldReference.ObjectNumber} {fieldReference.GenerationNumber} R に有効な /FT がありません。"));
            }

            if (!(fieldDictionary.TryGetValue("T", out var tValue) && tValue is PdfString))
            {
                issues.Add(new PdfValidationIssue("CHK-003", $"AcroForm のフィールド {fieldReference.ObjectNumber} {fieldReference.GenerationNumber} R に /T（フィールド名）がありません。"));
            }
        }
    }

    private static void ValidateEmbeddedFiles(
        PdfDocument document,
        IReadOnlyDictionary<(int ObjectNumber, int GenerationNumber), PdfIndirectObject> objectsByReference,
        List<PdfValidationIssue> issues)
    {
        if (!(document.CatalogDictionary.TryGetValue("Names", out var namesValue) && namesValue is PdfReference namesReference))
        {
            return;
        }

        if (!TryGetObject(objectsByReference, namesReference, out var namesObject) || namesObject is null)
        {
            issues.Add(new PdfValidationIssue("CHK-003", "Catalog から Names への参照が解決できません。"));
            return;
        }

        if (namesObject.Value is not PdfDictionary namesDictionary)
        {
            issues.Add(new PdfValidationIssue("CHK-003", "Names が辞書ではありません。"));
            return;
        }

        if (!(namesDictionary.TryGetValue("EmbeddedFiles", out var embeddedFilesValue) && embeddedFilesValue is PdfReference embeddedFilesReference))
        {
            return;
        }

        if (!TryGetObject(objectsByReference, embeddedFilesReference, out var embeddedFilesObject) || embeddedFilesObject is null)
        {
            issues.Add(new PdfValidationIssue("CHK-003", "Names の EmbeddedFiles への参照が解決できません。"));
            return;
        }

        if (embeddedFilesObject.Value is not PdfDictionary embeddedFilesDictionary)
        {
            issues.Add(new PdfValidationIssue("CHK-003", "EmbeddedFiles ネームツリーが辞書ではありません。"));
            return;
        }

        if (!(embeddedFilesDictionary.TryGetValue("Names", out var namesArrayValue) && namesArrayValue is PdfArray namesArray))
        {
            issues.Add(new PdfValidationIssue("CHK-003", "EmbeddedFiles ネームツリーに /Names がありません。"));
            return;
        }

        if (namesArray.Count % 2 != 0)
        {
            issues.Add(new PdfValidationIssue("CHK-003", "EmbeddedFiles ネームツリーの /Names は偶数個の要素である必要があります。"));
            return;
        }

        for (var index = 0; index < namesArray.Count; index += 2)
        {
            if (namesArray[index] is not PdfString)
            {
                issues.Add(new PdfValidationIssue("CHK-003", $"EmbeddedFiles ネームツリーのキー（index {index}）が文字列ではありません。"));
            }

            if (namesArray[index + 1] is PdfReference fileSpecRef)
            {
                if (!TryGetObject(objectsByReference, fileSpecRef, out var fileSpecObject) || fileSpecObject is null)
                {
                    issues.Add(new PdfValidationIssue("CHK-003", $"EmbeddedFiles の FileSpec {fileSpecRef.ObjectNumber} {fileSpecRef.GenerationNumber} R が解決できません。"));
                }
            }
            else
            {
                issues.Add(new PdfValidationIssue("CHK-003", $"EmbeddedFiles ネームツリーの値（index {index + 1}）が参照ではありません。"));
            }
        }
    }
}
