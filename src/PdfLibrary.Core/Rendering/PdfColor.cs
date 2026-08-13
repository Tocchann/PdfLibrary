namespace PdfLibrary.Core.Rendering;

/// <summary>
/// PDF 色を表し、成分の正規化・色空間変換を行うクラス。
/// </summary>
public sealed class PdfColor
{
    private readonly List<double> _components;

    /// <summary>
    /// 色空間。
    /// </summary>
    public required PdfColorSpace ColorSpace { get; init; }

    /// <summary>
    /// 色成分（0.0 ～ 1.0 の範囲に正規化）。
    /// </summary>
    public IReadOnlyList<double> Components => _components.AsReadOnly();

    public PdfColor()
    {
        _components = [];
    }

    private PdfColor(PdfColorSpace colorSpace, List<double> components)
    {
        ColorSpace = colorSpace;
        _components = new List<double>(components);
    }

    /// <summary>
    /// 色成分を設定します。検証と正規化を行います。
    /// </summary>
    /// <param name="components">色成分の値。色空間に応じた数が必須。</param>
    /// <exception cref="ArgumentException">成分数が色空間と一致しない場合。</exception>
    public void SetComponents(params double[] components)
    {
        if (components.Length != ColorSpace.ComponentCount)
        {
            throw new ArgumentException(
                $"色空間 {ColorSpace.Name} には {ColorSpace.ComponentCount} 成分が必須ですが、{components.Length} が指定されました。",
                nameof(components));
        }

        _components.Clear();
        foreach (var component in components)
        {
            _components.Add(Math.Clamp(component, 0.0, 1.0));
        }
    }

    /// <summary>
    /// 色をクローンします。
    /// </summary>
    public PdfColor Clone()
    {
        var clonedColorSpace = ColorSpace.Clone();
        var clonedColor = new PdfColor(clonedColorSpace, _components)
        {
            ColorSpace = clonedColorSpace,
        };
        return clonedColor;
    }

    /// <summary>
    /// CMYK 色を RGB に変換します（簡易線形変換）。
    /// </summary>
    /// <param name="cmyk">CMYK 色（各成分 0.0～1.0）。</param>
    /// <returns>RGB 色（各成分 0.0～1.0）。</returns>
    public static (double R, double G, double B) ConvertCmykToRgb(double c, double m, double y, double k)
    {
        // K = 黒版。RGB = (1 - C) * (1 - K) 等
        var r = (1.0 - c) * (1.0 - k);
        var g = (1.0 - m) * (1.0 - k);
        var b = (1.0 - y) * (1.0 - k);

        return (
            Math.Clamp(r, 0.0, 1.0),
            Math.Clamp(g, 0.0, 1.0),
            Math.Clamp(b, 0.0, 1.0)
        );
    }

    /// <summary>
    /// RGB 色を CMYK に変換します（簡易線形変換）。
    /// </summary>
    /// <param name="r">赤成分（0.0～1.0）。</param>
    /// <param name="g">緑成分（0.0～1.0）。</param>
    /// <param name="b">青成分（0.0～1.0）。</param>
    /// <returns>CMYK 色（各成分 0.0～1.0）。</returns>
    public static (double C, double M, double Y, double K) ConvertRgbToCmyk(double r, double g, double b)
    {
        var k = 1.0 - Math.Max(r, Math.Max(g, b));
        double c, m, y;

        if (k >= 1.0)
        {
            // 黒
            c = m = y = 0.0;
        }
        else
        {
            var divisor = 1.0 - k;
            c = (1.0 - r - k) / divisor;
            m = (1.0 - g - k) / divisor;
            y = (1.0 - b - k) / divisor;
        }

        return (
            Math.Clamp(c, 0.0, 1.0),
            Math.Clamp(m, 0.0, 1.0),
            Math.Clamp(y, 0.0, 1.0),
            Math.Clamp(k, 0.0, 1.0)
        );
    }

    /// <summary>
    /// グレースケール色を RGB に拡張します。
    /// </summary>
    /// <param name="gray">グレー値（0.0～1.0）。</param>
    /// <returns>RGB 色。</returns>
    public static (double R, double G, double B) ConvertGrayToRgb(double gray)
    {
        var normalized = Math.Clamp(gray, 0.0, 1.0);
        return (normalized, normalized, normalized);
    }

    /// <summary>
    /// 色が黒に近い場合は true を返します（すべての RGB 成分が 0.1 未満）。
    /// </summary>
    public bool IsNearBlack()
    {
        if (ColorSpace is PdfDeviceRGBColorSpace && Components.Count == 3)
        {
            return Components[0] < 0.1 && Components[1] < 0.1 && Components[2] < 0.1;
        }

        if (ColorSpace is PdfDeviceCMYKColorSpace && Components.Count == 4)
        {
            // K > 0.9 なら黒に近い
            return Components[3] > 0.9;
        }

        return false;
    }

    /// <summary>
    /// 色が白に近い場合は true を返します（すべての RGB 成分が 0.9 以上）。
    /// </summary>
    public bool IsNearWhite()
    {
        if (ColorSpace is PdfDeviceRGBColorSpace && Components.Count == 3)
        {
            return Components[0] > 0.9 && Components[1] > 0.9 && Components[2] > 0.9;
        }

        if (ColorSpace is PdfDeviceCMYKColorSpace && Components.Count == 4)
        {
            // K < 0.1 かつ C,M,Y が小さい
            return Components[3] < 0.1 && Components[0] < 0.1 && Components[1] < 0.1 && Components[2] < 0.1;
        }

        return false;
    }
}
