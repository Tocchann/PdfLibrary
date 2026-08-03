namespace PdfLibrary.Core;

public sealed class PdfIndirectObject
{
    public PdfIndirectObject(int objectNumber, int generationNumber, PdfValue value)
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
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public int ObjectNumber { get; }

    public int GenerationNumber { get; }

    public PdfValue Value { get; set; }

    public PdfReference Reference => new(ObjectNumber, GenerationNumber);
}
