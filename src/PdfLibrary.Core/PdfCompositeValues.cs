namespace PdfLibrary.Core;

public sealed class PdfArray : PdfValue, IList<PdfValue>
{
    private readonly List<PdfValue> _items = [];

    public override PdfValueKind Kind => PdfValueKind.Array;

    public PdfValue this[int index] { get => _items[index]; set => _items[index] = value; }

    public int Count => _items.Count;

    public bool IsReadOnly => false;

    public void Add(PdfValue item) => _items.Add(item);

    public void Clear() => _items.Clear();

    public bool Contains(PdfValue item) => _items.Contains(item);

    public void CopyTo(PdfValue[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

    public IEnumerator<PdfValue> GetEnumerator() => _items.GetEnumerator();

    public int IndexOf(PdfValue item) => _items.IndexOf(item);

    public void Insert(int index, PdfValue item) => _items.Insert(index, item);

    public bool Remove(PdfValue item) => _items.Remove(item);

    public void RemoveAt(int index) => _items.RemoveAt(index);

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _items.GetEnumerator();
}

public sealed class PdfDictionary : PdfValue, IEnumerable<KeyValuePair<string, PdfValue>>
{
    private readonly Dictionary<string, PdfValue> _entries = new(StringComparer.Ordinal);

    public override PdfValueKind Kind => PdfValueKind.Dictionary;

    public PdfValue this[string key]
    {
        get => _entries[key];
        set => _entries[key] = value;
    }

    public ICollection<string> Keys => _entries.Keys;

    public ICollection<PdfValue> Values => _entries.Values;

    public int Count => _entries.Count;

    public void Add(string key, PdfValue value) => _entries.Add(NormalizeKey(key), value);

    public bool ContainsKey(string key) => _entries.ContainsKey(NormalizeKey(key));

    public bool TryGetValue(string key, out PdfValue value) => _entries.TryGetValue(NormalizeKey(key), out value!);

    public bool Remove(string key) => _entries.Remove(NormalizeKey(key));

    public IEnumerator<KeyValuePair<string, PdfValue>> GetEnumerator() => _entries.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    public PdfDictionary Clone()
    {
        var clone = new PdfDictionary();
        foreach (var entry in _entries)
        {
            clone._entries.Add(entry.Key, entry.Value);
        }

        return clone;
    }

    private static string NormalizeKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return key[0] == '/' ? key[1..] : key;
    }
}

public sealed class PdfStream : PdfValue
{
    public PdfStream(PdfDictionary dictionary, byte[] data)
    {
        Dictionary = dictionary ?? throw new ArgumentNullException(nameof(dictionary));
        Data = data ?? throw new ArgumentNullException(nameof(data));
    }

    public PdfDictionary Dictionary { get; }

    public byte[] Data { get; }

    public override PdfValueKind Kind => PdfValueKind.Stream;
}
