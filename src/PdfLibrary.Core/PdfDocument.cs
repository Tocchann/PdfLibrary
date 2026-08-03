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
        pageDictionary["Type"] = new PdfName("Page");
        pageDictionary["Parent"] = Pages.Reference;
        var page = AddObjectCore(pageDictionary);
        var kids = (PdfArray)PagesDictionary["Kids"];
        kids.Add(page.Reference);
        RebuildPageTree();
        return page;
    }

    public void RemovePageAt(int index)
    {
        var kids = (PdfArray)PagesDictionary["Kids"];
        kids.RemoveAt(index);
        RebuildPageTree();
    }

    public PdfIndirectObject SetInfo(PdfDictionary info)
    {
        Info = AddObjectCore(info);
        return Info;
    }

    public void RebuildPageTree()
    {
        var kids = (PdfArray)PagesDictionary["Kids"];
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

    private PdfIndirectObject AddObjectCore(PdfValue value)
    {
        var objectNumber = _objects.Count == 0 ? 1 : _objects.Max(item => item.ObjectNumber) + 1;
        var indirect = new PdfIndirectObject(objectNumber, 0, value);
        _objects.Add(indirect);
        return indirect;
    }
}
