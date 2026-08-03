using System.Globalization;

namespace PdfLibrary.Core;

public sealed class PdfNull : PdfValue
{
    public static PdfNull Instance { get; } = new();

    private PdfNull()
    {
    }

    public override PdfValueKind Kind => PdfValueKind.Null;
}

public sealed class PdfBoolean : PdfValue
{
    public PdfBoolean(bool value) => Value = value;

    public bool Value { get; }

    public override PdfValueKind Kind => PdfValueKind.Boolean;
}

public sealed class PdfNumber : PdfValue
{
    public PdfNumber(double value) => Value = value;

    public double Value { get; }

    public override PdfValueKind Kind => PdfValueKind.Number;

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

public sealed class PdfName : PdfValue, IEquatable<PdfName>
{
    public PdfName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value[0] == '/' ? value[1..] : value;
    }

    public string Value { get; }

    public override PdfValueKind Kind => PdfValueKind.Name;

    public override string ToString() => "/" + Value;

    public bool Equals(PdfName? other) => other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is PdfName other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public static implicit operator PdfName(string value) => new(value);
}

public sealed class PdfString : PdfValue
{
    public PdfString(string value) => Value = value ?? throw new ArgumentNullException(nameof(value));

    public string Value { get; }

    public override PdfValueKind Kind => PdfValueKind.String;
}
