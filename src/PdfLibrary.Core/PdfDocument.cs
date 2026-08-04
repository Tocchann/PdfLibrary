namespace PdfLibrary.Core;

public sealed class PdfDocument
{
    private readonly List<PdfIndirectObject> _objects = [];

    private PdfDocument()
    {
        Catalog = AddObjectCore(new PdfDictionary
        {
            ["Type"] = new PdfName("Catalog"),
            ["Pages"] = new PdfReference(2),
        });

        Pages = AddObjectCore(new PdfDictionary
        {
            ["Type"] = new PdfName("Pages"),
            ["Kids"] = new PdfArray(),
            ["Count"] = new PdfNumber(0),
        });

        CatalogDictionary["Pages"] = Pages.Reference;
    }

    public PdfIndirectObject Catalog { get; }

    public PdfIndirectObject Pages { get; }

    public PdfIndirectObject? Info { get; internal set; }

    public PdfIndirectObject? Outlines { get; private set; }

    public IReadOnlyList<PdfIndirectObject> Objects => _objects;

    internal List<PdfIndirectObject> MutableObjects => _objects;

    public byte[]? OriginalBytes { get; private set; }

    public long? OriginalStartXref { get; private set; }

    public bool HasEncryption { get; private set; }

    public PdfDictionary CatalogDictionary => (PdfDictionary)Catalog.Value;

    public PdfDictionary PagesDictionary => (PdfDictionary)Pages.Value;

    public static PdfDocument Create() => new();

    public PdfIndirectObject AddObject(PdfValue value) => AddObjectCore(value);

    public PdfIndirectObject AddPage(PdfDictionary pageDictionary)
    {
        ArgumentNullException.ThrowIfNull(pageDictionary);

        pageDictionary["Type"] = new PdfName("Page");
        pageDictionary["Parent"] = Pages.Reference;

        var page = AddObjectCore(pageDictionary);
        var kids = GetKidsArray();
        kids.Add(page.Reference);
        RebuildPageTree();
        return page;
    }

    public void RemovePageAt(int index)
    {
        var kids = GetKidsArray();
        var pageReference = GetPageReferenceAt(index);
        var page = GetObject(pageReference);
        var annotationReferences = GetAnnotationReferences(page).ToArray();

        kids.RemoveAt(index);
        RemoveObjectCore(pageReference);

        foreach (var annotationReference in annotationReferences)
        {
            if (!IsReferencedElsewhere(annotationReference, annotationReference.ObjectNumber))
            {
                RemoveObjectCore(annotationReference);
            }
        }

        RebuildPageTree();
    }

    public PdfIndirectObject SetInfo(PdfDictionary info)
    {
        ArgumentNullException.ThrowIfNull(info);

        Info = AddObjectCore(info);
        return Info;
    }

    public PdfIndirectObject AddAnnotation(int pageIndex, PdfDictionary annotationDictionary)
    {
        ArgumentNullException.ThrowIfNull(annotationDictionary);

        var page = GetPageAt(pageIndex);
        var pageDictionary = (PdfDictionary)page.Value;
        annotationDictionary["Type"] = new PdfName("Annot");

        var annotation = AddObjectCore(annotationDictionary);
        var annots = GetOrCreateAnnotsArray(pageDictionary);
        annots.Add(annotation.Reference);
        return annotation;
    }

    public IReadOnlyList<PdfIndirectObject> GetAnnotations(int pageIndex)
    {
        var page = GetPageAt(pageIndex);
        return GetAnnotationReferences(page)
            .Select(reference => GetObject(reference))
            .ToArray();
    }

    public bool RemoveAnnotation(int pageIndex, PdfReference annotationReference)
    {
        var page = GetPageAt(pageIndex);
        var pageDictionary = (PdfDictionary)page.Value;
        if (!pageDictionary.TryGetValue("Annots", out var annotsValue) || annotsValue is not PdfArray annots)
        {
            return false;
        }

        var removed = false;
        for (var index = 0; index < annots.Count; index++)
        {
            if (annots[index] is PdfReference reference && reference.Equals(annotationReference))
            {
                annots.RemoveAt(index);
                removed = true;
                break;
            }
        }

        if (!removed)
        {
            return false;
        }

        if (annots.Count == 0)
        {
            pageDictionary.Remove("Annots");
        }

        if (!IsReferencedElsewhere(annotationReference, annotationReference.ObjectNumber))
        {
            RemoveObjectCore(annotationReference);
        }

        return true;
    }

    public void ClearOutlines()
    {
        if (Outlines is null)
        {
            CatalogDictionary.Remove("Outlines");
            return;
        }

        foreach (var reference in CollectOutlineReferences(Outlines))
        {
            RemoveObjectCore(reference);
        }

        CatalogDictionary.Remove("Outlines");
        Outlines = null;
    }

    public void SetOutlines(IEnumerable<PdfOutlineItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var outlineItems = items.ToList();
        ClearOutlines();
        if (outlineItems.Count == 0)
        {
            return;
        }

        var root = AddObjectCore(new PdfDictionary
        {
            ["Type"] = new PdfName("Outlines"),
        });

        var rootItems = BuildOutlineLevel(outlineItems, root.Reference);
        var rootDictionary = (PdfDictionary)root.Value;
        rootDictionary["First"] = rootItems[0].Item.Reference;
        rootDictionary["Last"] = rootItems[^1].Item.Reference;
        rootDictionary["Count"] = new PdfNumber(rootItems.Sum(item => 1 + item.DescendantCount));

        CatalogDictionary["Outlines"] = root.Reference;
        Outlines = root;
    }

    public void RebuildPageTree()
    {
        var kids = GetKidsArray();
        PagesDictionary["Count"] = new PdfNumber(kids.Count);
        CatalogDictionary["Pages"] = Pages.Reference;
    }

    public byte[] Save(PdfSaveOptions? options = null)
    {
        using var stream = new MemoryStream();
        Save(stream, options);
        return stream.ToArray();
    }

    public void Save(Stream stream, PdfSaveOptions? options = null)
    {
        PdfDocumentWriter.Save(this, stream, options ?? PdfSaveOptions.Default);
    }

    public static PdfDocument Load(byte[] bytes)
    {
        var document = PdfDocumentReader.Read(bytes);
        document.OriginalBytes = bytes.ToArray();
        document.OriginalStartXref = PdfDocumentReader.TryReadStartXref(bytes);
        return document;
    }

    internal void SetOriginalState(byte[] bytes, long? startXref)
    {
        OriginalBytes = bytes;
        OriginalStartXref = startXref;
    }

    internal void SetEncryptionState(bool hasEncryption)
    {
        HasEncryption = hasEncryption;
    }

    internal void SetOutlinesState(PdfIndirectObject? outlines)
    {
        Outlines = outlines;
    }

    private PdfIndirectObject AddObjectCore(PdfValue value)
    {
        var objectNumber = _objects.Count == 0 ? 1 : _objects.Max(item => item.ObjectNumber) + 1;
        var indirect = new PdfIndirectObject(objectNumber, 0, value);
        _objects.Add(indirect);
        return indirect;
    }

    private PdfIndirectObject GetObject(PdfReference reference)
        => _objects.First(item => item.ObjectNumber == reference.ObjectNumber && item.GenerationNumber == reference.GenerationNumber);

    private bool TryGetObject(PdfReference reference, out PdfIndirectObject? indirectObject)
    {
        indirectObject = _objects.FirstOrDefault(item => item.ObjectNumber == reference.ObjectNumber && item.GenerationNumber == reference.GenerationNumber);
        return indirectObject is not null;
    }

    private PdfIndirectObject GetPageAt(int index)
        => GetObject(GetPageReferenceAt(index));

    private PdfReference GetPageReferenceAt(int index)
    {
        var kids = GetKidsArray();
        if (index < 0 || index >= kids.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        if (kids[index] is not PdfReference reference)
        {
            throw new InvalidOperationException("ページツリーの Kids は参照である必要があります。");
        }

        return reference;
    }

    private PdfArray GetKidsArray()
    {
        if (!PagesDictionary.TryGetValue("Kids", out var kidsValue) || kidsValue is not PdfArray kids)
        {
            throw new InvalidOperationException("Page Tree の Kids が見つかりません。");
        }

        return kids;
    }

    private static PdfArray GetOrCreateAnnotsArray(PdfDictionary pageDictionary)
    {
        if (pageDictionary.TryGetValue("Annots", out var annotsValue) && annotsValue is PdfArray annots)
        {
            return annots;
        }

        var created = new PdfArray();
        pageDictionary["Annots"] = created;
        return created;
    }

    private static IEnumerable<PdfReference> GetAnnotationReferences(PdfIndirectObject page)
    {
        if (page.Value is not PdfDictionary pageDictionary)
        {
            yield break;
        }

        if (!pageDictionary.TryGetValue("Annots", out var annotsValue) || annotsValue is not PdfArray annots)
        {
            yield break;
        }

        foreach (var item in annots)
        {
            if (item is PdfReference reference)
            {
                yield return reference;
            }
        }
    }

    private List<(PdfIndirectObject Item, int DescendantCount)> BuildOutlineLevel(IReadOnlyList<PdfOutlineItem> items, PdfReference parent)
    {
        var built = new List<(PdfIndirectObject Item, int DescendantCount)>(items.Count);
        PdfIndirectObject? previous = null;

        foreach (var item in items)
        {
            ArgumentNullException.ThrowIfNull(item);

            var current = BuildOutlineItem(item, parent);
            if (previous is not null)
            {
                ((PdfDictionary)previous.Value)["Next"] = current.Item.Reference;
                ((PdfDictionary)current.Item.Value)["Prev"] = previous.Reference;
            }

            built.Add(current);
            previous = current.Item;
        }

        return built;
    }

    private (PdfIndirectObject Item, int DescendantCount) BuildOutlineItem(PdfOutlineItem item, PdfReference parent)
    {
        var dictionary = new PdfDictionary
        {
            ["Title"] = new PdfString(item.Title),
            ["Parent"] = parent,
        };

        if (item.Destination is not null)
        {
            dictionary["Dest"] = item.Destination;
        }

        var outlineItem = AddObjectCore(dictionary);
        var children = item.Children.Count == 0
            ? new List<(PdfIndirectObject Item, int DescendantCount)>()
            : BuildOutlineLevel(item.Children, outlineItem.Reference);
        if (children.Count > 0)
        {
            dictionary["First"] = children[0].Item.Reference;
            dictionary["Last"] = children[^1].Item.Reference;
            var descendantCount = children.Sum(child => 1 + child.DescendantCount);
            dictionary["Count"] = new PdfNumber(descendantCount);
            return (outlineItem, descendantCount);
        }

        return (outlineItem, 0);
    }

    private List<PdfReference> CollectOutlineReferences(PdfIndirectObject root)
    {
        var result = new List<PdfReference>();
        var visited = new HashSet<int>();

        void Visit(PdfReference reference)
        {
            if (!visited.Add(reference.ObjectNumber))
            {
                return;
            }

            if (!TryGetObject(reference, out var outlineObject) || outlineObject is null)
            {
                return;
            }

            result.Add(reference);
            if (outlineObject.Value is not PdfDictionary dictionary)
            {
                return;
            }

            if (dictionary.TryGetValue("First", out var firstValue) && firstValue is PdfReference firstReference)
            {
                Visit(firstReference);
            }

            if (dictionary.TryGetValue("Next", out var nextValue) && nextValue is PdfReference nextReference)
            {
                Visit(nextReference);
            }
        }

        Visit(root.Reference);
        return result;
    }

    private bool IsReferencedElsewhere(PdfReference reference, int? excludedObjectNumber = null)
        => _objects.Any(item => item.ObjectNumber != excludedObjectNumber && ContainsReference(item.Value, reference));

    private static bool ContainsReference(PdfValue value, PdfReference target)
    {
        switch (value)
        {
            case PdfReference reference:
                return reference.Equals(target);
            case PdfArray array:
                return array.Any(item => ContainsReference(item, target));
            case PdfDictionary dictionary:
                return dictionary.Any(entry => ContainsReference(entry.Value, target));
            case PdfStream stream:
                return ContainsReference(stream.Dictionary, target);
            default:
                return false;
        }
    }

    private bool RemoveObjectCore(PdfReference reference)
    {
        var removed = _objects.RemoveAll(item => item.ObjectNumber == reference.ObjectNumber && item.GenerationNumber == reference.GenerationNumber) > 0;
        if (!removed)
        {
            return false;
        }

        if (Info is not null && Info.Reference.Equals(reference))
        {
            Info = null;
        }

        if (Outlines is not null && Outlines.Reference.Equals(reference))
        {
            Outlines = null;
        }

        return true;
    }
}
