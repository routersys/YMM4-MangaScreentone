using ComputeWeave;

namespace MangaScreentone.Tests;

[Collection("Direct3D12")]
public sealed class MangaScreentonePipelineTests
{
    static readonly bool Direct3D12IsAvailable = GraphicsDevice.EnumerateDevices().Any();

    static MangaScreentonePipeline CreatePipeline()
    {
        if (!Direct3D12IsAvailable)
            Assert.Skip("Direct3D 12 is unavailable.");
        var pipeline = MangaScreentonePipeline.TryCreate();
        Assert.NotNull(pipeline);
        return pipeline;
    }

    static MangaScreentoneSettings.Parameters Parameters(
        float screenLines = 200f,
        float whitePoint = 0.8f,
        float blackPoint = 0.3f,
        float lightTone = 0.18f,
        float darkTone = 0.42f,
        float gradation = 1f,
        bool whiteLines = true,
        float blackLevel = 0.2f,
        float whiteLevel = 0.7f,
        float gamma = 1.3f,
        float border = 5f,
        float margin = 0f,
        MangaScreentonePaper paper = MangaScreentonePaper.White,
        float ink = 0.5f)
        => new(screenLines, whitePoint, blackPoint, lightTone, darkTone, gradation, whiteLines, blackLevel, whiteLevel, gamma, border, margin, paper, ink);

    static int[] Scene(int width, int height, double shift = 0d) => Scenes.Packed(width, height, shift);

    static int[] Render(MangaScreentonePipeline pipeline, int[] source, int width, int height, MangaScreentoneSettings.Parameters parameters)
    {
        var (canvasWidth, canvasHeight) = MangaScreentonePipeline.GetCanvasSize(width, height, in parameters);
        var destination = new int[canvasWidth * canvasHeight];
        var outcome = pipeline.Process(source, destination, width, height, in parameters);
        Assert.Equal(MangaScreentonePipeline.RenderOutcome.Rendered, outcome);
        return destination;
    }

    static int Channel(int pixel, int shift) => (pixel >> shift) & 255;

    static double Mismatch(int[] actual, (double R, double G, double B, double A)[] expected, int tolerance)
    {
        Assert.Equal(expected.Length, actual.Length);
        var mismatches = 0;
        for (var index = 0; index < actual.Length; index++)
        {
            var (red, green, blue, alpha) = expected[index];
            var close = Math.Abs(Channel(actual[index], 16) - red * 255d) <= tolerance
                && Math.Abs(Channel(actual[index], 8) - green * 255d) <= tolerance
                && Math.Abs(Channel(actual[index], 0) - blue * 255d) <= tolerance
                && Math.Abs(Channel(actual[index], 24) - alpha * 255d) <= tolerance;
            if (!close)
                mismatches++;
        }

        return mismatches / (double)actual.Length;
    }

    static readonly Dictionary<string, (int Width, int Height, MangaScreentoneSettings.Parameters Parameters)> ReferenceSettings = new()
    {
        ["defaults"] = (256, 192, Parameters()),
        ["dense-screen"] = (256, 192, Parameters(screenLines: 400f)),
        ["coarse-screen"] = (256, 192, Parameters(screenLines: 80f, lightTone: 0.5f, darkTone: 0.8f)),
        ["no-gradation"] = (256, 192, Parameters(gradation: 0f)),
        ["white-lines"] = (256, 192, Parameters(blackPoint: 0.6f)),
        ["no-white-lines"] = (256, 192, Parameters(blackPoint: 0.6f, whiteLines: false)),
        ["levels"] = (256, 192, Parameters(blackLevel: 0.1f, whiteLevel: 0.9f, gamma: 0.8f)),
        ["tone-points"] = (256, 192, Parameters(whitePoint: 0.6f, blackPoint: 0.4f)),
        ["margin-and-border"] = (256, 192, Parameters(border: 20f, margin: 40f)),
        ["off-white"] = (256, 192, Parameters(paper: MangaScreentonePaper.OffWhite, ink: 1f)),
        ["cream"] = (256, 192, Parameters(paper: MangaScreentonePaper.Cream, ink: 0.2f)),
        ["transparent"] = (256, 192, Parameters(paper: MangaScreentonePaper.Transparent, margin: 24f)),
        ["no-ink"] = (256, 192, Parameters(ink: 0f, border: 0f)),
        ["medium"] = (512, 384, Parameters(margin: 30f)),
    };

    public static TheoryData<string> ReferenceCaseNames => new(ReferenceSettings.Keys);

    [Theory]
    [MemberData(nameof(ReferenceCaseNames))]
    public void TheOutputMatchesTheCpuReferenceImplementation(string name)
    {
        var (width, height, parameters) = ReferenceSettings[name];
        using var pipeline = CreatePipeline();
        var source = Scene(width, height);

        var actual = Render(pipeline, source, width, height, parameters);

        var mismatch = Mismatch(actual, MangaScreentoneReference.Render(source, width, height, parameters), 3);
        Assert.True(mismatch <= 1e-4, $"{name}: {mismatch:P4}");
    }

    [Fact]
    public void ATransparentSourceBecomesBlankPaperInsideTheFrame()
    {
        using var pipeline = CreatePipeline();
        var parameters = Parameters(border: 0f, ink: 0f);

        var rendering = Render(pipeline, new int[96 * 64], 96, 64, parameters);

        Assert.All(rendering, pixel => Assert.Equal(unchecked((int)0xFFFFFFFF), pixel));
    }

    [Fact]
    public void TheBorderFramesTheOutputInInkAndLeavesTheInsideAlone()
    {
        using var pipeline = CreatePipeline();
        var parameters = Parameters(border: 10f, ink: 0f);
        var derived = MangaScreentoneSettings.Derive(160, 120, parameters);

        var rendering = Render(pipeline, new int[160 * 120], 160, 120, parameters);

        var inkColor = unchecked((int)0xFF000000);
        Assert.Equal(inkColor, rendering[0]);
        Assert.Equal(inkColor, rendering[(derived.BorderPixels - 1) * 160 + 80]);
        Assert.Equal(inkColor, rendering[60 * 160 + derived.BorderPixels - 1]);
        Assert.Equal(inkColor, rendering[119 * 160 + 159]);
        Assert.Equal(unchecked((int)0xFFFFFFFF), rendering[derived.BorderPixels * 160 + derived.BorderPixels]);
        Assert.Equal(unchecked((int)0xFFFFFFFF), rendering[60 * 160 + 80]);
    }

    [Fact]
    public void TheMarginAddsPaperAroundTheImageAndTheCanvasGrowsByTwiceTheMargin()
    {
        using var pipeline = CreatePipeline();
        var parameters = Parameters(border: 0f, margin: 40f, ink: 0f);
        var (canvasWidth, canvasHeight) = MangaScreentonePipeline.GetCanvasSize(256, 192, in parameters);
        var margin = (canvasWidth - 256) / 2;

        var rendering = Render(pipeline, Scene(256, 192), 256, 192, parameters);

        Assert.Equal(256 + 2 * margin, canvasWidth);
        Assert.Equal(192 + 2 * margin, canvasHeight);
        Assert.True(margin > 0);
        for (var y = 0; y < canvasHeight; y++)
        {
            for (var x = 0; x < canvasWidth; x++)
            {
                if (x >= margin && x < margin + 256 && y >= margin && y < margin + 192)
                    continue;
                Assert.Equal(unchecked((int)0xFFFFFFFF), rendering[y * canvasWidth + x]);
            }
        }
    }

    [Fact]
    public void ThePaperColorFillsTheBlankPage()
    {
        using var pipeline = CreatePipeline();
        var parameters = Parameters(border: 0f, ink: 0f, paper: MangaScreentonePaper.Cream);

        var rendering = Render(pipeline, new int[128 * 96], 128, 96, parameters);

        Assert.All(rendering, pixel =>
        {
            Assert.Equal(255, Channel(pixel, 24));
            Assert.InRange(Channel(pixel, 16), 246 - 14, 246 + 14);
            Assert.InRange(Channel(pixel, 8), 243 - 14, 243 + 14);
            Assert.InRange(Channel(pixel, 0), 236 - 14, 236 + 14);
        });
        Assert.InRange(rendering.Average(pixel => Channel(pixel, 16)), 246 - 1.5, 246 + 1.5);
        Assert.InRange(rendering.Average(pixel => Channel(pixel, 8)), 243 - 1.5, 243 + 1.5);
        Assert.InRange(rendering.Average(pixel => Channel(pixel, 0)), 236 - 1.5, 236 + 1.5);
        Assert.True(rendering.Distinct().Count() > 4);
    }

    [Fact]
    public void ATransparentPaperKeepsOnlyTheInkAndStaysPremultiplied()
    {
        using var pipeline = CreatePipeline();
        var parameters = Parameters(paper: MangaScreentonePaper.Transparent);

        var rendering = Render(pipeline, Scene(256, 192), 256, 192, parameters);

        Assert.Contains(rendering, pixel => Channel(pixel, 24) == 0);
        Assert.Contains(rendering, pixel => Channel(pixel, 24) > 200);
        Assert.All(rendering, pixel =>
        {
            Assert.InRange(Channel(pixel, 16), 0, Channel(pixel, 24));
            Assert.InRange(Channel(pixel, 8), 0, Channel(pixel, 24));
            Assert.InRange(Channel(pixel, 0), 0, Channel(pixel, 24));
        });
    }

    [Fact]
    public void WithoutInkAndWithAWhitePaperTheOutputIsPureBlackAndWhite()
    {
        using var pipeline = CreatePipeline();
        var parameters = Parameters(ink: 0f);

        var rendering = Render(pipeline, Scene(256, 192), 256, 192, parameters);

        Assert.All(rendering, pixel => Assert.True(pixel == unchecked((int)0xFF000000) || pixel == unchecked((int)0xFFFFFFFF), $"{pixel:X8}"));
        Assert.Contains(rendering, pixel => pixel == unchecked((int)0xFF000000));
        Assert.Contains(rendering, pixel => pixel == unchecked((int)0xFFFFFFFF));
    }

    [Fact]
    public void TheSameInputGivesTheSameOutputEveryTime()
    {
        using var first = CreatePipeline();
        using var second = CreatePipeline();
        var source = Scene(256, 192);
        var parameters = Parameters();

        var expected = Render(first, source, 256, 192, parameters);

        Assert.Equal(expected, Render(second, source, 256, 192, parameters));
        Assert.Equal(expected, Render(first, source, 256, 192, parameters));
    }

    [Fact]
    public void ADifferentImageGivesADifferentOutput()
    {
        using var pipeline = CreatePipeline();
        var parameters = Parameters();

        var plain = Render(pipeline, Scene(256, 192), 256, 192, parameters);
        var shifted = Render(pipeline, Scene(256, 192, 0.2d), 256, 192, parameters);

        Assert.NotEqual(plain, shifted);
    }

    [Fact]
    public void ARepeatedFrameRunsNoStageAgainButARepaintStillWritesThePanel()
    {
        using var pipeline = CreatePipeline();
        var source = Scene(256, 192);
        var parameters = Parameters();

        Render(pipeline, source, 256, 192, parameters);
        Render(pipeline, source, 256, 192, parameters);

        Assert.Equal((1, 1, 1, 1, 2), (pipeline.AnalysisRuns, pipeline.ToneRuns, pipeline.CutRuns, pipeline.PrintRuns, pipeline.PanelRuns));
    }

    public static readonly TheoryData<string> DependencyNames = ["ink", "paper", "border", "margin", "light-tone", "dark-tone", "gradation", "screen-lines", "white-lines-off", "white-point", "black-point", "gamma", "black-level", "white-level", "source"];

    static (MangaScreentoneSettings.Parameters Parameters, int[] Source, (int Analysis, int Tone, int Cut, int Print, int Panel) Runs) Change(string name, int[] source, MangaScreentoneSettings.Parameters baseline)
        => name switch
        {
            "ink" => (baseline with { Ink = 0.9f }, source, (0, 0, 0, 0, 1)),
            "paper" => (baseline with { Paper = MangaScreentonePaper.Cream }, source, (0, 0, 0, 0, 1)),
            "border" => (baseline with { Border = 12f }, source, (0, 0, 0, 0, 1)),
            "margin" => (baseline with { Margin = 16f }, source, (0, 0, 0, 0, 1)),
            "light-tone" => (baseline with { LightTone = 0.3f }, source, (0, 0, 0, 1, 1)),
            "dark-tone" => (baseline with { DarkTone = 0.6f }, source, (0, 0, 0, 1, 1)),
            "gradation" => (baseline with { Gradation = 0.4f }, source, (0, 0, 0, 1, 1)),
            "screen-lines" => (baseline with { ScreenLines = 120f }, source, (0, 0, 0, 1, 1)),
            "white-lines-off" => (baseline with { WhiteLines = false }, source, (0, 0, 0, 1, 1)),
            "white-point" => (baseline with { WhitePoint = 0.7f }, source, (0, 1, 0, 1, 1)),
            "black-point" => (baseline with { BlackPoint = 0.4f }, source, (0, 1, 0, 1, 1)),
            "gamma" => (baseline with { Gamma = 1.6f }, source, (1, 1, 1, 1, 1)),
            "black-level" => (baseline with { BlackLevel = 0.1f }, source, (1, 1, 1, 1, 1)),
            "white-level" => (baseline with { WhiteLevel = 0.8f }, source, (1, 1, 1, 1, 1)),
            "source" => (baseline, Scene(256, 192, 0.1d), (1, 1, 1, 1, 1)),
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };

    [Theory]
    [MemberData(nameof(DependencyNames))]
    public void OnlyTheStagesThatDependOnAChangedSettingRunAgain(string name)
    {
        using var pipeline = CreatePipeline();
        var source = Scene(256, 192);
        var baseline = Parameters();
        Render(pipeline, source, 256, 192, baseline);
        var before = (pipeline.AnalysisRuns, pipeline.ToneRuns, pipeline.CutRuns, pipeline.PrintRuns, pipeline.PanelRuns);
        var (changed, changedSource, runs) = Change(name, source, baseline);

        Render(pipeline, changedSource, 256, 192, changed);

        var after = (pipeline.AnalysisRuns, pipeline.ToneRuns, pipeline.CutRuns, pipeline.PrintRuns, pipeline.PanelRuns);
        Assert.Equal((before.AnalysisRuns + runs.Analysis, before.ToneRuns + runs.Tone, before.CutRuns + runs.Cut, before.PrintRuns + runs.Print, before.PanelRuns + runs.Panel), after);
    }

    [Theory]
    [MemberData(nameof(DependencyNames))]
    public void APipelineThatDrewAnotherSettingDrawsLikeAFreshOne(string name)
    {
        using var used = CreatePipeline();
        using var fresh = CreatePipeline();
        var source = Scene(256, 192);
        var baseline = Parameters();
        var (changed, changedSource, _) = Change(name, source, baseline);

        Render(used, source, 256, 192, baseline);
        var actual = Render(used, changedSource, 256, 192, changed);

        Assert.Equal(Render(fresh, changedSource, 256, 192, changed), actual);
    }

    [Fact]
    public void TurningTheWhiteLinesOnAfterTheyWereOffComputesTheCutOnlyThen()
    {
        using var pipeline = CreatePipeline();
        var source = Scene(256, 192);
        var off = Parameters(blackPoint: 0.6f, whiteLines: false);

        Render(pipeline, source, 256, 192, off);
        var cutBefore = pipeline.CutRuns;
        var turnedOn = Render(pipeline, source, 256, 192, off with { WhiteLines = true });

        Assert.Equal(0, cutBefore);
        Assert.Equal(1, pipeline.CutRuns);
        using var fresh = CreatePipeline();
        Assert.Equal(Render(fresh, source, 256, 192, off with { WhiteLines = true }), turnedOn);
    }

    [Fact]
    public void ThePaperAndTheMarginComeBackToTheirEarlierLookWhenRestored()
    {
        using var pipeline = CreatePipeline();
        var source = Scene(192, 144);
        var original = Parameters(margin: 20f);

        var first = Render(pipeline, source, 192, 144, original);
        Render(pipeline, source, 192, 144, original with { Margin = 0f, Paper = MangaScreentonePaper.Cream });
        var restored = Render(pipeline, source, 192, 144, original);

        Assert.Equal(first, restored);
    }

    [Fact]
    public void APipelineUsedAtAnotherSizeDrawsLikeAFreshOne()
    {
        using var used = CreatePipeline();
        using var fresh = CreatePipeline();
        var parameters = Parameters();

        Render(used, Scene(320, 200), 320, 200, parameters);
        var actual = Render(used, Scene(256, 192), 256, 192, parameters);

        Assert.Equal(Render(fresh, Scene(256, 192), 256, 192, parameters), actual);
    }

    [Fact]
    public void AnImageOfOnlyOneRowOrColumnStillRenders()
    {
        using var pipeline = CreatePipeline();
        var parameters = Parameters(border: 0f);

        Assert.Equal(300, Render(pipeline, Enumerable.Repeat(unchecked((int)0xFF404040), 300).ToArray(), 300, 1, parameters).Length);
        Assert.Equal(300, Render(pipeline, Enumerable.Repeat(unchecked((int)0xFF404040), 300).ToArray(), 1, 300, parameters).Length);
    }

    [Fact]
    public void APipelineWhoseGridWasReclaimedDrawsLikeAFreshOne()
    {
        using var used = CreatePipeline();
        using var fresh = CreatePipeline();
        var source = Scene(256, 192);
        var parameters = Parameters();
        Render(used, source, 256, 192, parameters);

        GraphicsDevice.GetDefault().TrimMemory();
        var actual = Render(used, source, 256, 192, parameters);

        Assert.Equal(Render(fresh, source, 256, 192, parameters), actual);
    }

    [Fact]
    public void AReclaimedGridIsAnalyzedAgain()
    {
        using var pipeline = CreatePipeline();
        var source = Scene(256, 192);
        var parameters = Parameters();
        Render(pipeline, source, 256, 192, parameters);
        Render(pipeline, source, 256, 192, parameters);
        var before = pipeline.AnalysisRuns;

        GraphicsDevice.GetDefault().TrimMemory();
        Render(pipeline, source, 256, 192, parameters);

        Assert.Equal(1, before);
        Assert.Equal(2, pipeline.AnalysisRuns);
    }
}
