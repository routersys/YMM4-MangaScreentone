using System.Globalization;
using System.Numerics;
using ComputeWeave;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Json;
using YukkuriMovieMaker.Player.Video;

namespace MangaScreentone.Tests;

[Collection("Direct2D")]
public sealed class MangaScreentoneEffectProcessorTests
{
    const int Width = 96;
    const int Height = 72;
    const int Length = 30;

    static SourceImage CreateSource(IGraphicsDevicesAndContext devices, double shift = 0d)
        => new(devices, Width, Height, (x, y) => Scenes.Pixel(x, y, Width, Height, shift));

    static void RequireInterop(IGraphicsDevicesAndContext devices)
    {
        using var scheduler = ComputeExternalQueueScheduler.Create();
        using var provider = MangaScreentoneInteropProvider.TryCreate(devices, scheduler, out var device);
        if (provider is null || device is null)
            Assert.Skip("Direct3D 11 and Direct3D 12 sharing is unavailable.");
    }

    static Animation Linear(double from, double to)
        => Json.LoadFromText<Animation>(string.Create(CultureInfo.InvariantCulture, $$"""{"AnimationType":"直線移動","Values":[{"Value":{{from}}},{"Value":{{to}}}]}"""))!;

    static Rendering RenderFrame(IGraphicsDevicesAndContext devices, IVideoEffectProcessor processor, int frame)
    {
        processor.Update(EffectDescriptions.At(frame, Length));
        return Rendering.Capture(devices, processor.Output);
    }

    static void AssertSameAsSource(Rendering rendering, SourceImage source)
    {
        Assert.Equal((0, 0, source.Width, source.Height), (rendering.Left, rendering.Top, rendering.Width, rendering.Height));
        Assert.All(rendering.Coordinates(), point => Assert.Equal(source[point.X, point.Y], rendering[point.X, point.Y]));
    }

    static MangaScreentoneEffect Plain()
    {
        var effect = new MangaScreentoneEffect();
        effect.Ink.Values[0].Value = 0d;
        effect.Border.Values[0].Value = 0d;
        return effect;
    }

    [Fact]
    public void TheProcessorHandsTheDrawDescriptionBackUnchanged()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = CreateSource(context);
        using var processor = new MangaScreentoneEffect().CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);
        var description = EffectDescriptions.At(0, Length);

        var draw = processor.Update(description);

        Assert.Same(description.DrawDescription, draw);
    }

    [Fact]
    public void TheImageBecomesPureBlackAndWhiteOfTheSameSize()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        RequireInterop(context);
        using var source = CreateSource(context);
        using var processor = Plain().CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var rendering = RenderFrame(context, processor, 0);

        Assert.Equal((0, 0, Width, Height), (rendering.Left, rendering.Top, rendering.Width, rendering.Height));
        Assert.All(rendering.Coordinates(), point => Assert.True(rendering[point.X, point.Y] == Bgra.Opaque(0, 0, 0) || rendering[point.X, point.Y] == Bgra.Opaque(255, 255, 255), $"({point.X}, {point.Y}) {rendering[point.X, point.Y]}"));
        Assert.Contains(rendering.Coordinates(), point => rendering[point.X, point.Y] == Bgra.Opaque(0, 0, 0));
        Assert.Contains(rendering.Coordinates(), point => rendering[point.X, point.Y] == Bgra.Opaque(255, 255, 255));
    }

    [Fact]
    public void TheMarginGrowsTheOutputOnEverySideAroundTheImage()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        RequireInterop(context);
        using var source = CreateSource(context);
        var effect = Plain();
        effect.Margin.Values[0].Value = 100d;
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);
        var margin = MangaScreentonePipeline.GetCanvasSize(Width, Height, new MangaScreentoneSettings.Parameters(200f, 0.8f, 0.3f, 0.18f, 0.42f, 1f, true, 0.2f, 0.7f, 1.3f, 0f, 100f, MangaScreentonePaper.White, 0f)).Width - Width;

        var rendering = RenderFrame(context, processor, 0);

        Assert.True(margin > 0);
        Assert.Equal((-margin / 2, -margin / 2, Width + margin, Height + margin), (rendering.Left, rendering.Top, rendering.Width, rendering.Height));
        Assert.Equal(Bgra.Opaque(255, 255, 255), rendering[-margin / 2, -margin / 2]);
        Assert.Equal(Bgra.Opaque(255, 255, 255), rendering[Width + margin / 2 - 1, Height + margin / 2 - 1]);
    }

    [Fact]
    public void ATransparentPaperLeavesTheMarginTransparent()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        RequireInterop(context);
        using var source = CreateSource(context);
        var effect = Plain();
        effect.Margin.Values[0].Value = 100d;
        effect.Paper = MangaScreentonePaper.Transparent;
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var rendering = RenderFrame(context, processor, 0);

        Assert.Equal(Bgra.Transparent, rendering[rendering.Left, rendering.Top]);
        Assert.Contains(rendering.Coordinates(), point => rendering[point.X, point.Y].Alpha == byte.MaxValue);
    }

    [Theory]
    [InlineData(100, 50)]
    [InlineData(-37, 21)]
    public void TheMangaTravelsWithTheImage(int dx, int dy)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        RequireInterop(context);
        using var source = CreateSource(context);
        using var moved = new AffineTransform2D(context.DeviceContext)
        {
            InterPolationMode = AffineTransform2DInterpolationMode.NearestNeighbor,
            BorderMode = BorderMode.Hard,
            TransformMatrix = Matrix3x2.CreateTranslation(dx, dy),
        };
        moved.SetInput(0, source.Bitmap, true);
        using var movedOutput = moved.Output;
        using var inPlaceProcessor = new MangaScreentoneEffect().CreateVideoEffect(context);
        inPlaceProcessor.SetInput(source.Bitmap);
        var inPlace = RenderFrame(context, inPlaceProcessor, 0);
        using var travelledProcessor = new MangaScreentoneEffect().CreateVideoEffect(context);
        travelledProcessor.SetInput(movedOutput);

        var travelled = RenderFrame(context, travelledProcessor, 0);

        Assert.Equal((inPlace.Left + dx, inPlace.Top + dy, inPlace.Width, inPlace.Height), (travelled.Left, travelled.Top, travelled.Width, travelled.Height));
        Assert.All(inPlace.Coordinates(), point => Assert.True(inPlace[point.X, point.Y] == travelled[point.X + dx, point.Y + dy], $"({point.X}, {point.Y})"));
    }

    public static readonly TheoryData<string, Action<MangaScreentoneEffect>> PassThroughSettings = new()
    {
        { nameof(MangaScreentoneEffect.Amount), effect => effect.Amount.Values[0].Value = 0d },
    };

    [Theory]
    [MemberData(nameof(PassThroughSettings))]
    public void AZeroAmountPassesTheImageThrough(string setting, Action<MangaScreentoneEffect> configure)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = CreateSource(context);
        var effect = new MangaScreentoneEffect();
        configure(effect);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var rendering = RenderFrame(context, processor, 0);

        Assert.Equal((0, 0, Width, Height), (rendering.Left, rendering.Top, rendering.Width, rendering.Height));
        Assert.All(rendering.Coordinates(), point => Assert.True(source[point.X, point.Y] == rendering[point.X, point.Y], $"{setting} ({point.X}, {point.Y})"));
    }

    [Fact]
    public void HalfTheAmountMixesTheMangaHalfwayWithTheSource()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        RequireInterop(context);
        using var source = CreateSource(context);
        var full = Plain();
        var half = Plain();
        half.Amount.Values[0].Value = 50d;
        using var fullProcessor = full.CreateVideoEffect(context);
        fullProcessor.SetInput(source.Bitmap);
        using var halfProcessor = half.CreateVideoEffect(context);
        halfProcessor.SetInput(source.Bitmap);
        var manga = RenderFrame(context, fullProcessor, 0);

        var mixed = RenderFrame(context, halfProcessor, 0);

        Assert.All(mixed.Coordinates(), point =>
        {
            var expected = (source[point.X, point.Y].Red + manga[point.X, point.Y].Red) / 2d;
            Assert.InRange(mixed[point.X, point.Y].Red, expected - 2d, expected + 2d);
        });
    }

    [Fact]
    public void ThePremultipliedColorStaysWithinItsAlpha()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        RequireInterop(context);
        using var source = CreateSource(context);
        var effect = new MangaScreentoneEffect { Paper = MangaScreentonePaper.Transparent };
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var rendering = RenderFrame(context, processor, 0);

        Assert.All(rendering.Coordinates(), point =>
        {
            var pixel = rendering[point.X, point.Y];
            Assert.InRange(pixel.Blue, 0, pixel.Alpha);
            Assert.InRange(pixel.Green, 0, pixel.Alpha);
            Assert.InRange(pixel.Red, 0, pixel.Alpha);
        });
    }

    [Fact]
    public void ReturningToAFrameReproducesItExactly()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        RequireInterop(context);
        using var source = CreateSource(context);
        using var processor = new MangaScreentoneEffect().CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var first = RenderFrame(context, processor, 4);
        RenderFrame(context, processor, 9);
        var again = RenderFrame(context, processor, 4);

        Assert.True(first.SamePixelsAs(again));
    }

    [Fact]
    public void ANewProcessorDrawsTheSameManga()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        RequireInterop(context);
        using var source = CreateSource(context);
        var effect = new MangaScreentoneEffect();
        using var firstProcessor = effect.CreateVideoEffect(context);
        firstProcessor.SetInput(source.Bitmap);
        var first = RenderFrame(context, firstProcessor, 0);
        using var secondProcessor = effect.CreateVideoEffect(context);
        secondProcessor.SetInput(source.Bitmap);

        var second = RenderFrame(context, secondProcessor, 0);

        Assert.True(first.SamePixelsAs(second));
    }

    [Fact]
    public void AnimatedAmountIsReadAtEachFrame()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        RequireInterop(context);
        using var source = CreateSource(context);
        var effect = Plain();
        effect.Amount.CopyFrom(Linear(0d, 100d));
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        using var settled = Plain().CreateVideoEffect(context);
        settled.SetInput(source.Bitmap);
        var manga = RenderFrame(context, settled, 0);

        var start = RenderFrame(context, processor, 0);
        var middle = RenderFrame(context, processor, Length / 2);
        var end = RenderFrame(context, processor, Length - 1);

        AssertSameAsSource(start, source);
        double Distance(Rendering rendering, Func<int, int, Bgra> reference) => rendering.Coordinates().Average(point => Math.Abs(rendering[point.X, point.Y].Red - reference(point.X, point.Y).Red));
        Assert.True(Distance(end, (x, y) => manga[x, y]) < Distance(end, (x, y) => source[x, y]));
        Assert.True(Distance(start, (x, y) => manga[x, y]) > Distance(middle, (x, y) => manga[x, y]));
        Assert.True(Distance(middle, (x, y) => manga[x, y]) > Distance(end, (x, y) => manga[x, y]));
    }

    [Theory]
    [InlineData(60d, 80d)]
    [InlineData(80d, 60d)]
    public void AProcessorThatDrewAnotherWhitePointDrawsLikeAFreshOne(double firstValue, double secondValue)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        RequireInterop(context);
        using var source = CreateSource(context);
        var effect = new MangaScreentoneEffect();
        effect.WhitePoint.Values[0].Value = firstValue;
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);
        RenderFrame(context, processor, 0);
        effect.WhitePoint.Values[0].Value = secondValue;
        using var fresh = effect.CreateVideoEffect(context);
        fresh.SetInput(source.Bitmap);
        var expected = RenderFrame(context, fresh, 0);

        var reused = RenderFrame(context, processor, 0);

        Assert.True(reused.SamePixelsAs(expected));
    }

    public static readonly TheoryData<string, Action<MangaScreentoneEffect>> LaterChanges = new()
    {
        { nameof(MangaScreentoneEffect.Amount), effect => effect.Amount.Values[0].Value = 50d },
        { nameof(MangaScreentoneEffect.ScreenLines), effect => effect.ScreenLines.Values[0].Value = 90d },
        { nameof(MangaScreentoneEffect.WhitePoint), effect => effect.WhitePoint.Values[0].Value = 50d },
        { nameof(MangaScreentoneEffect.BlackPoint), effect => effect.BlackPoint.Values[0].Value = 50d },
        { nameof(MangaScreentoneEffect.LightTone), effect => effect.LightTone.Values[0].Value = 60d },
        { nameof(MangaScreentoneEffect.DarkTone), effect => effect.DarkTone.Values[0].Value = 90d },
        { nameof(MangaScreentoneEffect.Gradation), effect => effect.Gradation.Values[0].Value = 0d },
        { nameof(MangaScreentoneEffect.BlackLevel), effect => effect.BlackLevel.Values[0].Value = 40d },
        { nameof(MangaScreentoneEffect.WhiteLevel), effect => effect.WhiteLevel.Values[0].Value = 100d },
        { nameof(MangaScreentoneEffect.Gamma), effect => effect.Gamma.Values[0].Value = 3d },
        { nameof(MangaScreentoneEffect.Border), effect => effect.Border.Values[0].Value = 20d },
        { nameof(MangaScreentoneEffect.Margin), effect => effect.Margin.Values[0].Value = 80d },
        { nameof(MangaScreentoneEffect.Paper), effect => effect.Paper = MangaScreentonePaper.Cream },
        { nameof(MangaScreentoneEffect.Ink), effect => effect.Ink.Values[0].Value = 100d },
    };

    [Theory]
    [MemberData(nameof(LaterChanges))]
    public void EverySettingChangedAfterTheFirstFrameReachesTheEffect(string setting, Action<MangaScreentoneEffect> change)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        RequireInterop(context);
        using var source = CreateSource(context);
        var effect = new MangaScreentoneEffect();
        effect.BlackPoint.Values[0].Value = 60d;
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var before = RenderFrame(context, processor, 0);
        change(effect);
        var after = RenderFrame(context, processor, 0);

        Assert.False(before.SamePixelsAs(after), setting);
    }

    [Theory]
    [MemberData(nameof(LaterChanges))]
    public void AProcessorThatDrewAnotherSettingDrawsLikeAFreshOne(string setting, Action<MangaScreentoneEffect> change)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        RequireInterop(context);
        using var source = CreateSource(context);
        var effect = new MangaScreentoneEffect();
        effect.BlackPoint.Values[0].Value = 60d;
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);
        RenderFrame(context, processor, 0);
        change(effect);
        using var fresh = effect.CreateVideoEffect(context);
        fresh.SetInput(source.Bitmap);
        var expected = RenderFrame(context, fresh, 0);

        var reused = RenderFrame(context, processor, 0);

        Assert.True(reused.SamePixelsAs(expected), setting);
    }

    [Fact]
    public void TurningTheWhiteLinesOffReachesTheEffect()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        RequireInterop(context);
        using var source = new SourceImage(context, 512, 384, (x, y) => Scenes.Pixel(x, y, 512, 384));
        var effect = Plain();
        effect.BlackPoint.Values[0].Value = 60d;
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var on = RenderFrame(context, processor, 0);
        effect.WhiteLines = false;
        var off = RenderFrame(context, processor, 0);

        Assert.False(on.SamePixelsAs(off));
    }

    [Theory]
    [InlineData(32, 96)]
    [InlineData(96, 32)]
    public void AResizedSourceKeepsProducingManga(int firstSize, int secondSize)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        RequireInterop(context);
        using var first = new SourceImage(context, firstSize, firstSize, (x, y) => Scenes.Pixel(x, y, firstSize, firstSize));
        using var second = new SourceImage(context, secondSize, secondSize, (x, y) => Scenes.Pixel(x, y, secondSize, secondSize));
        using var processor = Plain().CreateVideoEffect(context);
        processor.SetInput(first.Bitmap);
        var before = RenderFrame(context, processor, 0);

        processor.SetInput(second.Bitmap);
        var after = RenderFrame(context, processor, 0);

        Assert.Equal((firstSize, firstSize), (before.Width, before.Height));
        Assert.Equal((secondSize, secondSize), (after.Width, after.Height));
        Assert.Contains(after.Coordinates(), point => after[point.X, point.Y] == Bgra.Opaque(0, 0, 0));
    }

    [Fact]
    public void AnImageTooLargeForTheCanvasPassesThroughUntouched()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = SourceImage.Solid(context, MangaScreentoneSettings.MaximumCanvasSize + 8, 8, Bgra.Opaque(40, 90, 200));
        using var processor = new MangaScreentoneEffect().CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var rendering = RenderFrame(context, processor, 0);

        AssertSameAsSource(rendering, source);
    }

    [Fact(Timeout = 60000)]
    public async Task AProcessorDrawsLikeAFreshOneAfterTheDeviceReclaimsIdleMemory()
    {
        await Task.Run(() =>
        {
            using var devices = new GraphicsDevices();
            using var context = devices.CreateContext();
            RequireInterop(context);
            using var source = CreateSource(context);
            var effect = new MangaScreentoneEffect();
            using var processor = effect.CreateVideoEffect(context);
            processor.SetInput(source.Bitmap);
            RenderFrame(context, processor, 0);
            GraphicsDevice.GetDefault().TrimMemory();

            var reclaimed = RenderFrame(context, processor, 0);
            using var fresh = effect.CreateVideoEffect(context);
            fresh.SetInput(source.Bitmap);
            var expected = RenderFrame(context, fresh, 0);

            Assert.True(reclaimed.SamePixelsAs(expected));
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public void AFailureWhileUpdatingIsNotSwallowed()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var processor = new MangaScreentoneEffect().CreateVideoEffect(context);

        Assert.ThrowsAny<Exception>(() => processor.Update(null!));
    }

    static readonly MangaScreentoneSettings.Parameters DefaultParameters = new(200f, 0.8f, 0.3f, 0.18f, 0.42f, 1f, true, 0.2f, 0.7f, 1.3f, 5f, 0f, MangaScreentonePaper.White, 0.5f);

    public static readonly TheoryData<string, Action<MangaScreentoneEffect>> ShownChanges = new()
    {
        { nameof(MangaScreentoneEffect.ScreenLines), effect => effect.ScreenLines.Values[0].Value = 90d },
        { nameof(MangaScreentoneEffect.WhitePoint), effect => effect.WhitePoint.Values[0].Value = 60d },
        { nameof(MangaScreentoneEffect.BlackPoint), effect => effect.BlackPoint.Values[0].Value = 50d },
        { nameof(MangaScreentoneEffect.LightTone), effect => effect.LightTone.Values[0].Value = 60d },
        { nameof(MangaScreentoneEffect.DarkTone), effect => effect.DarkTone.Values[0].Value = 90d },
        { nameof(MangaScreentoneEffect.Gradation), effect => effect.Gradation.Values[0].Value = 40d },
        { nameof(MangaScreentoneEffect.WhiteLines), effect => effect.WhiteLines = false },
        { nameof(MangaScreentoneEffect.BlackLevel), effect => effect.BlackLevel.Values[0].Value = 40d },
        { nameof(MangaScreentoneEffect.WhiteLevel), effect => effect.WhiteLevel.Values[0].Value = 90d },
        { nameof(MangaScreentoneEffect.Gamma), effect => effect.Gamma.Values[0].Value = 2d },
        { nameof(MangaScreentoneEffect.Border), effect => effect.Border.Values[0].Value = 20d },
        { nameof(MangaScreentoneEffect.Margin), effect => effect.Margin.Values[0].Value = 80d },
        { nameof(MangaScreentoneEffect.Paper), effect => effect.Paper = MangaScreentonePaper.Cream },
        { nameof(MangaScreentoneEffect.Ink), effect => effect.Ink.Values[0].Value = 70d },
    };

    static readonly Dictionary<string, MangaScreentoneSettings.Parameters> ShownParameters = new()
    {
        [nameof(MangaScreentoneEffect.ScreenLines)] = DefaultParameters with { ScreenLines = 90f },
        [nameof(MangaScreentoneEffect.WhitePoint)] = DefaultParameters with { WhitePoint = 0.6f },
        [nameof(MangaScreentoneEffect.BlackPoint)] = DefaultParameters with { BlackPoint = 0.5f },
        [nameof(MangaScreentoneEffect.LightTone)] = DefaultParameters with { LightTone = 0.6f },
        [nameof(MangaScreentoneEffect.DarkTone)] = DefaultParameters with { DarkTone = 0.9f },
        [nameof(MangaScreentoneEffect.Gradation)] = DefaultParameters with { Gradation = 0.4f },
        [nameof(MangaScreentoneEffect.WhiteLines)] = DefaultParameters with { WhiteLines = false },
        [nameof(MangaScreentoneEffect.BlackLevel)] = DefaultParameters with { BlackLevel = 0.4f },
        [nameof(MangaScreentoneEffect.WhiteLevel)] = DefaultParameters with { WhiteLevel = 0.9f },
        [nameof(MangaScreentoneEffect.Gamma)] = DefaultParameters with { Gamma = 2f },
        [nameof(MangaScreentoneEffect.Border)] = DefaultParameters with { Border = 20f },
        [nameof(MangaScreentoneEffect.Margin)] = DefaultParameters with { Margin = 80f },
        [nameof(MangaScreentoneEffect.Paper)] = DefaultParameters with { Paper = MangaScreentonePaper.Cream },
        [nameof(MangaScreentoneEffect.Ink)] = DefaultParameters with { Ink = 0.7f },
    };

    [Theory]
    [MemberData(nameof(ShownChanges))]
    public void EverySettingReachesThePipelineAsTheValueItShows(string setting, Action<MangaScreentoneEffect> change)
    {
        var expectedParameters = ShownParameters[setting];
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        RequireInterop(context);
        using var pipeline = MangaScreentonePipeline.TryCreate();
        Assert.NotNull(pipeline);
        using var source = CreateSource(context);
        var effect = new MangaScreentoneEffect();
        change(effect);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);
        var (canvasWidth, canvasHeight) = MangaScreentonePipeline.GetCanvasSize(Width, Height, in expectedParameters);
        var expected = new int[canvasWidth * canvasHeight];
        pipeline.Process(Scenes.Packed(Width, Height), expected, Width, Height, in expectedParameters);

        var rendering = RenderFrame(context, processor, 0);

        var margin = (canvasWidth - Width) / 2;
        Assert.Equal((-margin, -margin, canvasWidth, canvasHeight), (rendering.Left, rendering.Top, rendering.Width, rendering.Height));
        Assert.All(rendering.Coordinates(), point =>
        {
            var pixel = rendering[point.X, point.Y];
            var packed = pixel.Blue | (pixel.Green << 8) | (pixel.Red << 16) | (pixel.Alpha << 24);
            Assert.True(expected[(point.Y + margin) * canvasWidth + point.X + margin] == packed, $"{setting} ({point.X}, {point.Y})");
        });
    }
}
