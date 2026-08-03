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

        foreach (var indirect in document.Objects)
        {
            if (indirect.Value is PdfDictionary dictionary && dictionary.TryGetValue("Parent", out var parentValue) && parentValue is PdfReference parentReference)
            {
                if (!document.Objects.Any(item => item.ObjectNumber == parentReference.ObjectNumber))
                {
                    issues.Add(new PdfValidationIssue("CHK-003", $"Object {indirect.ObjectNumber} の Parent 参照が解決できません。"));
                }
            }
        }

        return issues;
    }
}
