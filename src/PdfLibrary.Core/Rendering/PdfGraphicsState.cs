namespace PdfLibrary.Core.Rendering;

public sealed class PdfGraphicsState
{
    public PdfRenderMatrix CurrentTransformationMatrix { get; private set; } = PdfRenderMatrix.Identity;

    public PdfGraphicsState Clone()
        => new()
        {
            CurrentTransformationMatrix = CurrentTransformationMatrix,
        };

    public void ConcatMatrix(PdfRenderMatrix matrix)
        => CurrentTransformationMatrix = CurrentTransformationMatrix.Multiply(matrix);
}
