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

    public PdfIndirectObject? Metadata { get; private set; }

    public PdfIndirectObject? Outlines { get; private set; }

    public PdfIndirectObject? AcroForm { get; private set; }

    public PdfIndirectObject? EmbeddedFilesNameTree { get; private set; }

    public IReadOnlyList<PdfIndirectObject> Objects => _objects;

    internal List<PdfIndirectObject> MutableObjects => _objects;

    public byte[]? OriginalBytes { get; private set; }

    public long? OriginalStartXref { get; private set; }

    public bool HasEncryption { get; private set; }

    public PdfDictionary CatalogDictionary => (PdfDictionary)Catalog.Value;

    public PdfDictionary PagesDictionary => (PdfDictionary)Pages.Value;

    public int PageCount => PagesDictionary.TryGetValue("Count", out var countValue) && countValue is PdfNumber count
        ? (int)count.Value
        : 0;

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

    /// <summary>XMP Metadata ストリームのバイト列（UTF-8 XML）を返します。存在しない場合は null。</summary>
    public byte[]? GetXmpMetadata()
    {
        if (Metadata?.Value is PdfStream stream)
        {
            if (TryGetLengthFromStream(stream, out var length))
            {
                if (length >= 0 && length <= stream.Data.Length)
                {
                    var sliced = new byte[length];
                    Array.Copy(stream.Data, 0, sliced, 0, length);
                    return sliced;
                }
            }

            return (byte[])stream.Data.Clone();
        }

        return null;
    }

    /// <summary>XMP Metadata ストリームを設定します。xmpData は UTF-8 エンコードの XMP パケット XML です。</summary>
    public void SetXmpMetadata(byte[] xmpData)
    {
        ArgumentNullException.ThrowIfNull(xmpData);
        var metadataBytes = (byte[])xmpData.Clone();

        var streamDict = new PdfDictionary
        {
            ["Type"] = new PdfName("Metadata"),
            ["Subtype"] = new PdfName("XML"),
        };

        PdfIndirectObject? targetMetadata = Metadata;
        if (targetMetadata is null &&
            CatalogDictionary.TryGetValue("Metadata", out var metadataValue) &&
            metadataValue is PdfReference metadataReference &&
            TryGetObject(metadataReference, out var metadataObject) &&
            metadataObject?.Value is PdfStream existingStream &&
            existingStream.Dictionary.TryGetValue("Type", out var typeValue) &&
            typeValue is PdfName typeName &&
            string.Equals(typeName.Value, "Metadata", StringComparison.Ordinal) &&
            existingStream.Dictionary.TryGetValue("Subtype", out var subtypeValue) &&
            subtypeValue is PdfName subtypeName &&
            string.Equals(subtypeName.Value, "XML", StringComparison.Ordinal) &&
            !existingStream.Dictionary.ContainsKey("Filter") &&
            !existingStream.Dictionary.ContainsKey("DecodeParms"))
        {
            targetMetadata = metadataObject;
            Metadata = metadataObject;
        }

        if (targetMetadata is null)
        {
            targetMetadata = AddObjectCore(new PdfStream(streamDict, metadataBytes));
            Metadata = targetMetadata;
        }
        else
        {
            targetMetadata.Value = new PdfStream(streamDict, metadataBytes);
        }

        CatalogDictionary["Metadata"] = targetMetadata.Reference;
    }

    /// <summary>XMP Metadata ストリームを削除します。存在した場合は true を返します。</summary>
    public bool ClearXmpMetadata()
    {
        if (Metadata is null)
        {
            if (!(CatalogDictionary.TryGetValue("Metadata", out var metadataValue) && metadataValue is PdfReference metadataReference))
            {
                return false;
            }

            CatalogDictionary.Remove("Metadata");
            if (TryGetObject(metadataReference, out var metadataObject) &&
                metadataObject is not null &&
                !IsReferencedElsewhere(metadataReference, metadataReference.ObjectNumber))
            {
                RemoveObjectCore(metadataReference);
            }

            return true;
        }

        return RemoveObjectCore(Metadata.Reference);
    }

    /// <summary>
    /// /Info 辞書の内容から最小限の XMP パケットを生成して SetXmpMetadata() を呼び出します。
    /// Info が設定されていない場合は何もしません。
    /// </summary>
    public void SyncXmpFromInfo()
    {
        if (Info?.Value is not PdfDictionary infoDict)
        {
            return;
        }

        var xml = BuildMinimalXmp(infoDict);
        SetXmpMetadata(System.Text.Encoding.UTF8.GetBytes(xml));
    }

    private static string BuildMinimalXmp(PdfDictionary info)
    {
        static string? GetStr(PdfDictionary d, string key)
        {
            if (!d.TryGetValue(key, out var value))
            {
                return null;
            }

            return value switch
            {
                PdfString s => s.Value,
                PdfHexString hex => DecodePdfHexText(hex.Data),
                _ => null,
            };
        }

        var title = GetStr(info, "Title");
        var author = GetStr(info, "Author");
        var subject = GetStr(info, "Subject");
        var keywords = GetStr(info, "Keywords");
        var creator = GetStr(info, "Creator");
        var producer = GetStr(info, "Producer");
        var creationDate = GetStr(info, "CreationDate");
        var modDate = GetStr(info, "ModDate");

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<?xpacket begin=\"\uFEFF\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?>");
        sb.AppendLine("<x:xmpmeta xmlns:x=\"adobe:ns:meta/\">");
        sb.AppendLine("  <rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\">");
        sb.AppendLine("    <rdf:Description rdf:about=\"\"");
        sb.AppendLine("        xmlns:dc=\"http://purl.org/dc/elements/1.1/\"");
        sb.AppendLine("        xmlns:xmp=\"http://ns.adobe.com/xap/1.0/\"");
        sb.AppendLine("        xmlns:pdf=\"http://ns.adobe.com/pdf/1.3/\">");

        if (title is not null)
        {
            sb.AppendLine($"      <dc:title><rdf:Alt><rdf:li xml:lang=\"x-default\">{EscapeXml(title)}</rdf:li></rdf:Alt></dc:title>");
        }

        if (author is not null)
        {
            sb.AppendLine($"      <dc:creator><rdf:Seq><rdf:li>{EscapeXml(author)}</rdf:li></rdf:Seq></dc:creator>");
        }

        if (subject is not null)
        {
            sb.AppendLine($"      <dc:description><rdf:Alt><rdf:li xml:lang=\"x-default\">{EscapeXml(subject)}</rdf:li></rdf:Alt></dc:description>");
        }

        if (keywords is not null)
        {
            sb.AppendLine($"      <pdf:Keywords>{EscapeXml(keywords)}</pdf:Keywords>");
        }

        if (creator is not null)
        {
            sb.AppendLine($"      <xmp:CreatorTool>{EscapeXml(creator)}</xmp:CreatorTool>");
        }

        if (producer is not null)
        {
            sb.AppendLine($"      <pdf:Producer>{EscapeXml(producer)}</pdf:Producer>");
        }

        if (creationDate is not null && IsIso8601Date(creationDate))
        {
            sb.AppendLine($"      <xmp:CreateDate>{EscapeXml(creationDate)}</xmp:CreateDate>");
        }

        if (modDate is not null && IsIso8601Date(modDate))
        {
            sb.AppendLine($"      <xmp:ModifyDate>{EscapeXml(modDate)}</xmp:ModifyDate>");
        }

        sb.AppendLine("    </rdf:Description>");
        sb.AppendLine("  </rdf:RDF>");
        sb.AppendLine("</x:xmpmeta>");
        sb.AppendLine("<?xpacket end=\"w\"?>");

        return sb.ToString();
    }

    private static string DecodePdfHexText(byte[] data)
    {
        if (data.Length >= 2)
        {
            if (data[0] == 0xFE && data[1] == 0xFF)
            {
                return System.Text.Encoding.BigEndianUnicode.GetString(data, 2, data.Length - 2);
            }

            if (data[0] == 0xFF && data[1] == 0xFE)
            {
                return System.Text.Encoding.Unicode.GetString(data, 2, data.Length - 2);
            }
        }

        return System.Text.Encoding.UTF8.GetString(data);
    }

    private static bool IsIso8601Date(string value)
        => DateTimeOffset.TryParseExact(
            value,
            ["yyyy-MM-dd", "yyyy-MM-dd'T'HH:mm:ssK", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK"],
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out _);

    private bool TryGetLengthFromStream(PdfStream stream, out int length)
    {
        length = 0;
        if (!stream.Dictionary.TryGetValue("Length", out var lengthValue))
        {
            return false;
        }

        if (lengthValue is PdfNumber directLength)
        {
            length = (int)directLength.Value;
            return true;
        }

        if (lengthValue is PdfReference lengthReference &&
            TryGetObject(lengthReference, out var lengthObject) &&
            lengthObject?.Value is PdfNumber indirectLength)
        {
            length = (int)indirectLength.Value;
            return true;
        }

        return false;
    }

    private static string EscapeXml(string value)
        => value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

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

    /// <summary>Widget アノテーションとして追加し、AcroForm にも登録します。</summary>
    public PdfIndirectObject RegisterFormField(int pageIndex, PdfDictionary fieldDictionary)
    {
        ArgumentNullException.ThrowIfNull(fieldDictionary);

        var field = AddAnnotation(pageIndex, fieldDictionary);
        EnsureAcroFormField(field);
        return field;
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

    internal void SetAcroFormState(PdfIndirectObject? acroForm)
    {
        AcroForm = acroForm;
    }

    internal void SetEmbeddedFilesNameTreeState(PdfIndirectObject? nameTree)
    {
        EmbeddedFilesNameTree = nameTree;
    }

    internal void SetMetadataState(PdfIndirectObject? metadata)
    {
        Metadata = metadata;
    }

    public PdfIndirectObject AddFormField(PdfFormField field)
    {
        ArgumentNullException.ThrowIfNull(field);

        var fieldTypeName = field.FieldType switch
        {
            PdfFormFieldType.Text => "Tx",
            PdfFormFieldType.Button => "Btn",
            PdfFormFieldType.Choice => "Ch",
            _ => throw new ArgumentOutOfRangeException(nameof(field)),
        };

        var fieldDictionary = new PdfDictionary
        {
            ["Type"] = new PdfName("Annot"),
            ["Subtype"] = new PdfName("Widget"),
            ["FT"] = new PdfName(fieldTypeName),
            ["T"] = new PdfString(field.Name),
        };

        if (field.Value is not null)
        {
            fieldDictionary["V"] = new PdfString(field.Value);
        }

        if (field.Rect is not null)
        {
            fieldDictionary["Rect"] = field.Rect;
        }
        else
        {
            fieldDictionary["Rect"] = new PdfArray
            {
                new PdfNumber(0),
                new PdfNumber(0),
                new PdfNumber(0),
                new PdfNumber(0),
            };
        }

        if (field.PageIndex.HasValue)
        {
            var page = GetPageAt(field.PageIndex.Value);
            fieldDictionary["P"] = page.Reference;
            var annots = GetOrCreateAnnotsArray((PdfDictionary)page.Value);
            var fieldObject = AddObjectCore(fieldDictionary);
            annots.Add(fieldObject.Reference);
            EnsureAcroFormField(fieldObject);
            return fieldObject;
        }

        var fieldObj = AddObjectCore(fieldDictionary);
        EnsureAcroFormField(fieldObj);
        return fieldObj;
    }

    public IReadOnlyList<PdfIndirectObject> GetFormFields()
    {
        if (AcroForm is null)
        {
            return [];
        }

        if (AcroForm.Value is not PdfDictionary acroFormDictionary)
        {
            return [];
        }

        if (!acroFormDictionary.TryGetValue("Fields", out var fieldsValue) || fieldsValue is not PdfArray fields)
        {
            return [];
        }

        return fields
            .OfType<PdfReference>()
            .Select(reference => _objects.FirstOrDefault(item => item.ObjectNumber == reference.ObjectNumber && item.GenerationNumber == reference.GenerationNumber))
            .Where(item => item is not null)
            .ToArray()!;
    }

    public bool SetFieldValue(PdfReference fieldReference, string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var fieldObject = _objects.FirstOrDefault(item => item.ObjectNumber == fieldReference.ObjectNumber && item.GenerationNumber == fieldReference.GenerationNumber);
        if (fieldObject is null || fieldObject.Value is not PdfDictionary fieldDictionary)
        {
            return false;
        }

        fieldDictionary["V"] = new PdfString(value);
        return true;
    }

    public bool RemoveFormField(PdfReference fieldReference)
    {
        var fieldObject = _objects.FirstOrDefault(item => item.ObjectNumber == fieldReference.ObjectNumber && item.GenerationNumber == fieldReference.GenerationNumber);
        if (fieldObject is null)
        {
            return false;
        }

        if (AcroForm?.Value is PdfDictionary acroFormDictionary &&
            acroFormDictionary.TryGetValue("Fields", out var fieldsValue) &&
            fieldsValue is PdfArray fields)
        {
            for (var index = 0; index < fields.Count; index++)
            {
                if (fields[index] is PdfReference reference && reference.Equals(fieldReference))
                {
                    fields.RemoveAt(index);
                    break;
                }
            }

            if (fields.Count == 0)
            {
                CatalogDictionary.Remove("AcroForm");
                RemoveObjectCore(AcroForm!.Reference);
                AcroForm = null;
            }
        }

        if (fieldObject.Value is PdfDictionary fd && fd.TryGetValue("P", out var pageRefValue) && pageRefValue is PdfReference pageRef)
        {
            var page = _objects.FirstOrDefault(item => item.ObjectNumber == pageRef.ObjectNumber);
            if (page?.Value is PdfDictionary pageDictionary &&
                pageDictionary.TryGetValue("Annots", out var annotsValue) &&
                annotsValue is PdfArray annots)
            {
                for (var i = 0; i < annots.Count; i++)
                {
                    if (annots[i] is PdfReference r && r.Equals(fieldReference))
                    {
                        annots.RemoveAt(i);
                        break;
                    }
                }

                if (annots.Count == 0)
                {
                    pageDictionary.Remove("Annots");
                }
            }
        }

        RemoveObjectCore(fieldReference);
        return true;
    }

    public PdfIndirectObject AddEmbeddedFile(string name, byte[] data, string mimeType = "application/octet-stream")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrWhiteSpace(mimeType);

        var embeddedStreamDictionary = new PdfDictionary
        {
            ["Type"] = new PdfName("EmbeddedFile"),
            ["Subtype"] = new PdfName(mimeType.Replace("/", "#2F")),
        };
        var embeddedStream = AddObjectCore(new PdfStream(embeddedStreamDictionary, data));

        var fileSpecDictionary = new PdfDictionary
        {
            ["Type"] = new PdfName("Filespec"),
            ["F"] = new PdfString(name),
            ["UF"] = new PdfString(name),
            ["EF"] = new PdfDictionary
            {
                ["F"] = embeddedStream.Reference,
                ["UF"] = embeddedStream.Reference,
            },
        };
        var fileSpec = AddObjectCore(fileSpecDictionary);

        EnsureEmbeddedFilesNameTree(name, fileSpec.Reference);
        return fileSpec;
    }

    public IReadOnlyList<string> GetEmbeddedFileNames()
    {
        if (EmbeddedFilesNameTree is null)
        {
            return [];
        }

        if (EmbeddedFilesNameTree.Value is not PdfDictionary nameTreeDictionary)
        {
            return [];
        }

        if (!nameTreeDictionary.TryGetValue("Names", out var namesValue) || namesValue is not PdfArray names)
        {
            return [];
        }

        var result = new List<string>();
        for (var index = 0; index < names.Count - 1; index += 2)
        {
            if (names[index] is PdfString nameString)
            {
                result.Add(nameString.Value);
            }
        }

        return result;
    }

    public bool RemoveEmbeddedFile(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (EmbeddedFilesNameTree is null ||
            EmbeddedFilesNameTree.Value is not PdfDictionary nameTreeDictionary ||
            !nameTreeDictionary.TryGetValue("Names", out var namesValue) ||
            namesValue is not PdfArray names)
        {
            return false;
        }

        for (var index = 0; index < names.Count - 1; index += 2)
        {
            if (names[index] is PdfString nameString && string.Equals(nameString.Value, name, StringComparison.Ordinal))
            {
                PdfReference? fileSpecRef = names[index + 1] as PdfReference;
                names.RemoveAt(index + 1);
                names.RemoveAt(index);

                if (fileSpecRef is not null)
                {
                    var fileSpecObject = _objects.FirstOrDefault(item => item.ObjectNumber == fileSpecRef.ObjectNumber);
                    if (fileSpecObject?.Value is PdfDictionary fsd &&
                        fsd.TryGetValue("EF", out var efValue) &&
                        efValue is PdfDictionary ef &&
                        ef.TryGetValue("F", out var embeddedRef) &&
                        embeddedRef is PdfReference embRef)
                    {
                        RemoveObjectCore(embRef);
                    }

                    RemoveObjectCore(fileSpecRef);
                }

                if (names.Count == 0)
                {
                    CleanupEmbeddedFilesNameTree();
                }

                return true;
            }
        }

        return false;
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

        if (AcroForm is not null && AcroForm.Reference.Equals(reference))
        {
            AcroForm = null;
        }

        if (EmbeddedFilesNameTree is not null && EmbeddedFilesNameTree.Reference.Equals(reference))
        {
            EmbeddedFilesNameTree = null;
        }

        if (Metadata is not null && Metadata.Reference.Equals(reference))
        {
            CatalogDictionary.Remove("Metadata");
            Metadata = null;
        }

        return true;
    }

    private void EnsureAcroFormField(PdfIndirectObject fieldObject)
    {
        if (AcroForm is null)
        {
            var acroFormDictionary = new PdfDictionary
            {
                ["Fields"] = new PdfArray { fieldObject.Reference },
            };
            AcroForm = AddObjectCore(acroFormDictionary);
            CatalogDictionary["AcroForm"] = AcroForm.Reference;
        }
        else
        {
            if (AcroForm.Value is PdfDictionary acroFormDictionary)
            {
                if (!acroFormDictionary.TryGetValue("Fields", out var fieldsValue) || fieldsValue is not PdfArray fields)
                {
                    fields = new PdfArray();
                    acroFormDictionary["Fields"] = fields;
                }

                fields.Add(fieldObject.Reference);
            }
        }
    }

    private void EnsureEmbeddedFilesNameTree(string name, PdfReference fileSpecReference)
    {
        if (EmbeddedFilesNameTree is null)
        {
            var nameTreeDictionary = new PdfDictionary
            {
                ["Names"] = new PdfArray
                {
                    new PdfString(name),
                    fileSpecReference,
                },
            };
            EmbeddedFilesNameTree = AddObjectCore(nameTreeDictionary);
            EnsureNamesEntry("EmbeddedFiles", EmbeddedFilesNameTree.Reference);
        }
        else
        {
            if (EmbeddedFilesNameTree.Value is PdfDictionary nameTreeDictionary)
            {
                if (!nameTreeDictionary.TryGetValue("Names", out var namesValue) || namesValue is not PdfArray names)
                {
                    names = new PdfArray();
                    nameTreeDictionary["Names"] = names;
                }

                names.Add(new PdfString(name));
                names.Add(fileSpecReference);
            }
        }
    }

    private void EnsureNamesEntry(string key, PdfReference valueReference)
    {
        if (!CatalogDictionary.TryGetValue("Names", out var namesValue) || namesValue is not PdfReference namesRef)
        {
            var namesDictionary = new PdfDictionary
            {
                [key] = valueReference,
            };
            var namesObject = AddObjectCore(namesDictionary);
            CatalogDictionary["Names"] = namesObject.Reference;
            return;
        }

        var namesObject2 = _objects.FirstOrDefault(item => item.ObjectNumber == namesRef.ObjectNumber);
        if (namesObject2?.Value is PdfDictionary existingNamesDictionary)
        {
            existingNamesDictionary[key] = valueReference;
        }
    }

    private void CleanupEmbeddedFilesNameTree()
    {
        if (EmbeddedFilesNameTree is null)
        {
            return;
        }

        if (CatalogDictionary.TryGetValue("Names", out var namesValue) && namesValue is PdfReference namesRef)
        {
            var namesObject = _objects.FirstOrDefault(item => item.ObjectNumber == namesRef.ObjectNumber);
            if (namesObject?.Value is PdfDictionary namesDictionary)
            {
                namesDictionary.Remove("EmbeddedFiles");
                if (!namesDictionary.Any())
                {
                    CatalogDictionary.Remove("Names");
                    RemoveObjectCore(namesRef);
                }
            }
        }

        RemoveObjectCore(EmbeddedFilesNameTree.Reference);
        EmbeddedFilesNameTree = null;
    }
}
