namespace PdfLibrary.Core;

public sealed class PdfReference : PdfValue, IEquatable<PdfReference>
{
    public PdfReference(int objectNumber, int generationNumber = 0)
    {
        if (objectNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(objectNumber));
        }

        if (generationNumber < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(generationNumber));
        }

        ObjectNumber = objectNumber;
        GenerationNumber = generationNumber;
    }

    public int ObjectNumber { get; }

    public int GenerationNumber { get; }

    public override PdfValueKind Kind => PdfValueKind.Reference;

    public bool Equals(PdfReference? other) => other is not null && ObjectNumber == other.ObjectNumber && GenerationNumber == other.GenerationNumber;

    public override bool Equals(object? obj) => obj is PdfReference other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(ObjectNumber, GenerationNumber);
}
