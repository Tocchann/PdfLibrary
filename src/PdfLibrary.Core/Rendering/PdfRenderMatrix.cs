namespace PdfLibrary.Core.Rendering;

public readonly record struct PdfRenderMatrix(
    double A,
    double B,
    double C,
    double D,
    double E,
    double F)
{
    public static PdfRenderMatrix Identity { get; } = new(1, 0, 0, 1, 0, 0);

    public PdfRenderPoint Transform(double x, double y)
        => new((A * x) + (C * y) + E, (B * x) + (D * y) + F);

    public PdfRenderMatrix Multiply(PdfRenderMatrix other)
        => new(
            (A * other.A) + (B * other.C),
            (A * other.B) + (B * other.D),
            (C * other.A) + (D * other.C),
            (C * other.B) + (D * other.D),
            (E * other.A) + (F * other.C) + other.E,
            (E * other.B) + (F * other.D) + other.F);
}
