using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;

namespace MangaScreentone.Tests;

[Collection("Direct2D")]
public sealed class MangaScreentoneCustomEffectTests
{
    const int AmountIndex = 0;
    const int KeepTransparentIndex = 1;
    const int Width = 40;
    const int Height = 24;

    static readonly Bgra Blue = Bgra.Opaque(255, 0, 0);
    static readonly Bgra HalfPage = new(0, 0, 0, 128);

    static Rendering Render(IGraphicsDevicesAndContext devices, ID2D1Image source, ID2D1Image page, float amount, bool keepTransparent = false)
    {
        using var effect = new MangaScreentoneCustomEffect(devices);
        effect.SetInput(0, source, true);
        effect.SetInput(1, page, true);
        effect.Amount = amount;
        effect.KeepTransparent = keepTransparent;
        using var output = effect.Output;
        return Rendering.Capture(devices, output);
    }

    static AffineTransform2D Translate(IGraphicsDevicesAndContext devices, ID2D1Image image, float dx, float dy)
    {
        var transform = new AffineTransform2D(devices.DeviceContext)
        {
            InterPolationMode = AffineTransform2DInterpolationMode.NearestNeighbor,
            BorderMode = BorderMode.Hard,
            TransformMatrix = Matrix3x2.CreateTranslation(dx, dy),
        };
        transform.SetInput(0, image, true);
        return transform;
    }

    static bool WithinRounding(Bgra expected, Bgra actual)
        => Math.Abs(expected.Blue - actual.Blue) <= 1 && Math.Abs(expected.Green - actual.Green) <= 1 && Math.Abs(expected.Red - actual.Red) <= 1 && Math.Abs(expected.Alpha - actual.Alpha) <= 1;

    static Bgra Mixed(Bgra page, Bgra source, float amount)
    {
        byte Channel(byte pageValue, byte sourceValue) => (byte)Math.Round(pageValue * amount + sourceValue * (1d - amount), MidpointRounding.AwayFromZero);
        return new Bgra(Channel(page.Blue, source.Blue), Channel(page.Green, source.Green), Channel(page.Red, source.Red), Channel(page.Alpha, source.Alpha));
    }

    [Fact]
    public void TheEffectIsEnabledOnceCreated()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new MangaScreentoneCustomEffect(context);

        Assert.True(effect.IsEnabled);
    }

    [Fact]
    public void TheAmountStartsFromZero()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new MangaScreentoneCustomEffect(context);

        Assert.Equal(0f, effect.GetFloatValue(AmountIndex));
    }

    [Theory]
    [InlineData(-0.5f, 0f)]
    [InlineData(0f, 0f)]
    [InlineData(0.4f, 0.4f)]
    [InlineData(1f, 1f)]
    [InlineData(1.5f, 1f)]
    [InlineData(float.MaxValue, 1f)]
    public void TheAmountIsClampedToTheUnitRange(float value, float expected)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new MangaScreentoneCustomEffect(context);

        effect.Amount = value;

        Assert.Equal(expected, effect.GetFloatValue(AmountIndex));
    }

    [Fact]
    public void TheOutputBoundsCoverTheSourceAndThePage()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = SourceImage.Solid(context, Width, Height, Blue);
        using var page = SourceImage.Solid(context, 8, 8, HalfPage);
        using var moved = Translate(context, page.Bitmap, 50f, -6f);
        using var movedOutput = moved.Output;
        using var effect = new MangaScreentoneCustomEffect(context);
        effect.SetInput(0, source.Bitmap, true);
        effect.SetInput(1, movedOutput, true);
        using var output = effect.Output;

        var bounds = context.DeviceContext.GetImageLocalBounds(output);

        Assert.Equal(0f, bounds.Left);
        Assert.Equal(-6f, bounds.Top);
        Assert.Equal(58f, bounds.Right);
        Assert.Equal((float)Height, bounds.Bottom);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void WithoutAmountTheSourcePassesThroughUntouched(float amount)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = SourceImage.Solid(context, Width, Height, Blue);
        using var page = SourceImage.Solid(context, Width, Height, HalfPage);

        var rendering = Render(context, source.Bitmap, page.Bitmap, amount);

        Assert.All(rendering.Coordinates(), point => Assert.Equal(source[point.X, point.Y], rendering[point.X, point.Y]));
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(0.5f)]
    [InlineData(0.25f)]
    public void ThePageReplacesTheSourceInProportionToTheAmount(float amount)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = SourceImage.Solid(context, Width, Height, Blue);
        using var page = SourceImage.Solid(context, Width, Height, HalfPage);
        var expected = Mixed(HalfPage.Premultiplied(), Blue.Premultiplied(), amount);

        var rendering = Render(context, source.Bitmap, page.Bitmap, amount);

        Assert.All(rendering.Coordinates(), point => Assert.True(WithinRounding(expected, rendering[point.X, point.Y]), $"({point.X}, {point.Y}) {rendering[point.X, point.Y]}"));
    }

    [Fact]
    public void AnOpaquePageAtFullAmountHidesTheSourceCompletely()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = SourceImage.Solid(context, Width, Height, Blue);
        using var page = SourceImage.Solid(context, Width, Height, Bgra.Opaque(10, 20, 30));

        var rendering = Render(context, source.Bitmap, page.Bitmap, 1f);

        Assert.All(rendering.Coordinates(), point => Assert.Equal(Bgra.Opaque(10, 20, 30), rendering[point.X, point.Y]));
    }

    [Fact]
    public void ThePageOutsideTheSourceIsDrawnOnItsOwnScaledByTheAmount()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = SourceImage.Solid(context, Width, Height, Blue);
        using var page = SourceImage.Solid(context, 8, 8, HalfPage);
        using var moved = Translate(context, page.Bitmap, 50f, 4f);
        using var movedOutput = moved.Output;
        var expected = Mixed(HalfPage.Premultiplied(), Bgra.Transparent, 0.5f);

        var rendering = Render(context, source.Bitmap, movedOutput, 0.5f);

        Assert.True(WithinRounding(expected, rendering[53, 7]), $"{rendering[53, 7]}");
        Assert.True(WithinRounding(Mixed(Bgra.Transparent, Blue.Premultiplied(), 0.5f), rendering[10, 10]), $"{rendering[10, 10]}");
        Assert.Equal(Bgra.Transparent, rendering[45, 10]);
    }

    [Fact]
    public void TransparencyIsNotKeptUntilItIsTurnedOn()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new MangaScreentoneCustomEffect(context);

        Assert.Equal(0f, effect.GetFloatValue(KeepTransparentIndex));
    }

    [Theory]
    [InlineData(true, 1f)]
    [InlineData(false, 0f)]
    public void KeepingTransparencyIsReportedAsAFlag(bool keep, float expected)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new MangaScreentoneCustomEffect(context);
        effect.KeepTransparent = !keep;

        effect.KeepTransparent = keep;

        Assert.Equal(expected, effect.GetFloatValue(KeepTransparentIndex));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnOpaquePageCoversTheTransparentPartsOfTheSourceOnlyWhenTransparencyIsNotKept(bool keep)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var pageColor = Bgra.Opaque(10, 20, 30);
        using var source = new SourceImage(context, Width, Height, (x, _) => x < 10 ? Bgra.Transparent : Blue);
        using var page = SourceImage.Solid(context, Width, Height, pageColor);

        var rendering = Render(context, source.Bitmap, page.Bitmap, 1f, keep);

        Assert.All(rendering.Coordinates(), point => Assert.Equal(point.X < 10 && keep ? Bgra.Transparent : pageColor, rendering[point.X, point.Y]));
    }

    [Fact]
    public void ASemiTransparentSourceScalesThePageByItsOpacityWhenTransparencyIsKept()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = SourceImage.Solid(context, Width, Height, new Bgra(0, 0, 255, 128));
        using var page = SourceImage.Solid(context, Width, Height, Bgra.Opaque(200, 100, 50));
        var expected = new Bgra(100, 50, 25, 128);

        var rendering = Render(context, source.Bitmap, page.Bitmap, 1f, true);

        Assert.All(rendering.Coordinates(), point => Assert.True(WithinRounding(expected, rendering[point.X, point.Y]), $"({point.X}, {point.Y}) {rendering[point.X, point.Y]}"));
    }

    [Fact]
    public void ThePageOutsideTheSourceIsNotDrawnWhenTransparencyIsKept()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = SourceImage.Solid(context, Width, Height, Blue);
        using var page = SourceImage.Solid(context, 8, 8, Bgra.Opaque(10, 20, 30));
        using var moved = Translate(context, page.Bitmap, 50f, 4f);
        using var movedOutput = moved.Output;

        var rendering = Render(context, source.Bitmap, movedOutput, 1f, true);

        Assert.Equal(Bgra.Transparent, rendering[53, 7]);
        Assert.Equal(Bgra.Transparent, rendering[45, 10]);
    }

    [Fact]
    public void ThePartialAmountKeepsTransparentPartsTransparentWhenTransparencyIsKept()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var pageColor = Bgra.Opaque(10, 20, 30);
        using var source = new SourceImage(context, Width, Height, (x, _) => x < 10 ? Bgra.Transparent : Blue);
        using var page = SourceImage.Solid(context, Width, Height, pageColor);

        var rendering = Render(context, source.Bitmap, page.Bitmap, 0.5f, true);

        Assert.Equal(Bgra.Transparent, rendering[3, 5]);
        Assert.True(WithinRounding(Mixed(pageColor, Blue.Premultiplied(), 0.5f), rendering[20, 5]), $"{rendering[20, 5]}");
    }

    [Fact]
    public void WithoutAmountTheSourcePassesThroughEvenWhenTransparencyIsKept()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, (x, _) => x < 10 ? Bgra.Transparent : Blue);
        using var page = SourceImage.Solid(context, Width, Height, Bgra.Opaque(10, 20, 30));

        var rendering = Render(context, source.Bitmap, page.Bitmap, 0f, true);

        Assert.All(rendering.Coordinates(), point => Assert.Equal(source[point.X, point.Y], rendering[point.X, point.Y]));
    }
}
