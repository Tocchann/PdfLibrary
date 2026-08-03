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
}
