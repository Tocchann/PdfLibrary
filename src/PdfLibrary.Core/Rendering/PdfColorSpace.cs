namespace PdfLibrary.Core.Rendering;

/// <summary>
/// PDF色空間の基底抽象クラス。
/// </summary>
public abstract class PdfColorSpace
{
    /// <summary>
    /// 色空間の名前またはタイプ（例: "DeviceRGB", "DeviceCMYK"）。
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// この色空間で必要な色成分数。
    /// </summary>
    public abstract int ComponentCount { get; }

    /// <summary>
    /// 色空間を表す PDF 値（ Name または Array）を返します。
    /// </summary>
    public abstract PdfValue ToPdfValue();

    /// <summary>
    /// 色空間をクローンします。
    /// </summary>
    public abstract PdfColorSpace Clone();

    /// <summary>
    /// 色空間を PDF 辞書から解決します。
    /// </summary>
    /// <param name="colorSpaceValue">色空間を表す PDF 値（Name または Array）。</param>
    /// <param name="objectResolver">参照解決用のコールバック。</param>
    /// <returns>解決された色空間、またはnull（不正または未対応）。</returns>
    public static PdfColorSpace? Resolve(PdfValue colorSpaceValue, Func<PdfReference, PdfValue?>? objectResolver = null)
    {
        return colorSpaceValue switch
        {
            PdfName name => ResolveByName(name),
            PdfArray array => ResolveByArray(array, objectResolver),
            _ => null,
        };
    }

    private static PdfColorSpace? ResolveByName(PdfName name)
    {
        return name.Value switch
        {
            "DeviceRGB" => new PdfDeviceRGBColorSpace(),
            "DeviceCMYK" => new PdfDeviceCMYKColorSpace(),
            "DeviceGray" => new PdfDeviceGrayColorSpace(),
            _ => null,
        };
    }

    private static PdfColorSpace? ResolveByArray(PdfArray array, Func<PdfReference, PdfValue?>? objectResolver)
    {
        if (array.Count == 0)
        {
            return null;
        }

        var firstElement = array[0];
        if (firstElement is not PdfName arrayTypeName)
        {
            return null;
        }

        if (arrayTypeName.Value == "CalRGB" && array.Count >= 2)
        {
            return PdfCalRGBColorSpace.TryParse(array);
        }

        if (arrayTypeName.Value == "CalGray" && array.Count >= 2)
        {
            return PdfCalGrayColorSpace.TryParse(array);
        }

        // Pattern, Separation, DeviceN 等は当面未対応
        return null;
    }
}

/// <summary>
/// DeviceRGB 色空間（RGB 3成分）。
/// </summary>
public sealed class PdfDeviceRGBColorSpace : PdfColorSpace
{
    public override string Name => "DeviceRGB";
    public override int ComponentCount => 3;
    public override PdfValue ToPdfValue() => new PdfName("DeviceRGB");
    public override PdfColorSpace Clone() => new PdfDeviceRGBColorSpace();
}

/// <summary>
/// DeviceCMYK 色空間（CMYK 4成分）。
/// </summary>
public sealed class PdfDeviceCMYKColorSpace : PdfColorSpace
{
    public override string Name => "DeviceCMYK";
    public override int ComponentCount => 4;
    public override PdfValue ToPdfValue() => new PdfName("DeviceCMYK");
    public override PdfColorSpace Clone() => new PdfDeviceCMYKColorSpace();
}

/// <summary>
/// DeviceGray 色空間（グレースケール 1成分）。
/// </summary>
public sealed class PdfDeviceGrayColorSpace : PdfColorSpace
{
    public override string Name => "DeviceGray";
    public override int ComponentCount => 1;
    public override PdfValue ToPdfValue() => new PdfName("DeviceGray");
    public override PdfColorSpace Clone() => new PdfDeviceGrayColorSpace();
}

/// <summary>
/// CalRGB 色空間（CIE-based RGB）。
/// </summary>
public sealed class PdfCalRGBColorSpace : PdfColorSpace
{
    public override string Name => "CalRGB";
    public override int ComponentCount => 3;
    private readonly PdfArray _definition;

    public PdfCalRGBColorSpace(PdfArray definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _definition = definition;
    }

    public override PdfValue ToPdfValue() => _definition;

    public override PdfColorSpace Clone() => new PdfCalRGBColorSpace(_definition);

    public static PdfCalRGBColorSpace? TryParse(PdfArray array)
    {
        ArgumentNullException.ThrowIfNull(array);

        if (array.Count < 2 || array[1] is not PdfDictionary)
        {
            return null;
        }

        try
        {
            return new PdfCalRGBColorSpace(array);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// CalGray 色空間（CIE-based グレースケール）。
/// </summary>
public sealed class PdfCalGrayColorSpace : PdfColorSpace
{
    public override string Name => "CalGray";
    public override int ComponentCount => 1;
    private readonly PdfArray _definition;

    public PdfCalGrayColorSpace(PdfArray definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _definition = definition;
    }

    public override PdfValue ToPdfValue() => _definition;

    public override PdfColorSpace Clone() => new PdfCalGrayColorSpace(_definition);

    public static PdfCalGrayColorSpace? TryParse(PdfArray array)
    {
        ArgumentNullException.ThrowIfNull(array);

        if (array.Count < 2 || array[1] is not PdfDictionary)
        {
            return null;
        }

        try
        {
            return new PdfCalGrayColorSpace(array);
        }
        catch
        {
            return null;
        }
    }
}
