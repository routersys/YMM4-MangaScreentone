namespace MangaScreentone.Tests;

public sealed class MangaScreentoneSettingsTests
{
    static MangaScreentoneSettings.Parameters Parameters(
        float screenLines = 200f,
        float whitePoint = 0.8f,
        float blackPoint = 0.3f,
        float blackLevel = 0.2f,
        float whiteLevel = 0.7f,
        float gamma = 1.3f,
        float border = 5f,
        float margin = 0f,
        float ink = 0.5f)
        => new(screenLines, whitePoint, blackPoint, 0.18f, 0.42f, 1f, true, blackLevel, whiteLevel, gamma, border, margin, MangaScreentonePaper.White, ink);

    [Theory]
    [InlineData(15.0, 15)]
    [InlineData(28.125, 29)]
    [InlineData(20.625, 21)]
    [InlineData(0.7, 1)]
    [InlineData(2.0, 3)]
    [InlineData(5.625, 5)]
    public void AKernelSizeIsTheTruncatedValueMadeOdd(double size, int expected)
    {
        Assert.Equal(expected, MangaScreentoneSettings.OddKernel(size));
    }

    [Theory]
    [InlineData(2.8125, 12)]
    [InlineData(0.7, 3)]
    [InlineData(0.35, 2)]
    [InlineData(1.0, 4)]
    [InlineData(0.0, 0)]
    [InlineData(-1.0, 0)]
    public void AGaussianRadiusFollowsTheKernelSizeOfFourSigmaEachWay(double sigma, int expected)
    {
        Assert.Equal(expected, MangaScreentoneSettings.GaussianRadius(sigma));
    }

    [Theory]
    [InlineData(0.8f, 0.3f, 0.8f, 0.3f)]
    [InlineData(2f, -1f, 1f, 0f)]
    [InlineData(0.1f, 0.5f, 0.52f, 0.5f)]
    [InlineData(0.3f, 0.7f, 0.72f, 0.7f)]
    public void TonePointsStayInRangeAndKeepAGapBetweenWhiteAndBlack(float white, float black, float expectedWhite, float expectedBlack)
    {
        var (actualWhite, actualBlack) = MangaScreentoneSettings.NormalizeTonePoints(white, black);

        Assert.Equal(expectedWhite, actualWhite, 6);
        Assert.Equal(expectedBlack, actualBlack, 6);
        Assert.True(actualWhite - actualBlack >= MangaScreentoneSettings.MinimumToneGap - 1e-6f);
    }

    [Theory]
    [InlineData(0.2f, 0.7f, 1.3f, 0.2f, 0.7f, 1.3f)]
    [InlineData(2f, -3f, 9f, 0.9f, 0.901f, 3f)]
    [InlineData(0.9f, 0.1f, 0.1f, 0.9f, 0.901f, 0.3f)]
    [InlineData(-1f, 5f, 1f, 0f, 1f, 1f)]
    public void LevelsStayInRangeAndTheWhiteLevelStaysAboveTheBlackLevel(float black, float white, float gamma, float expectedBlack, float expectedWhite, float expectedGamma)
    {
        var (actualBlack, actualWhite, actualGamma) = MangaScreentoneSettings.NormalizeLevels(black, white, gamma);

        Assert.Equal(expectedBlack, actualBlack, 6);
        Assert.Equal(expectedWhite, actualWhite, 6);
        Assert.Equal(expectedGamma, actualGamma, 6);
    }

    [Fact]
    public void EverySizeScalesWithTheWidthRelativeTo1024()
    {
        var narrow = MangaScreentoneSettings.Derive(1024, 768, Parameters());
        var wide = MangaScreentoneSettings.Derive(2048, 1536, Parameters());

        Assert.Equal(1f, narrow.Scale);
        Assert.Equal(2f, wide.Scale);
        Assert.Equal(narrow.SolidSigma * 2f, wide.SolidSigma);
        Assert.Equal(narrow.StrokeMinLength * 2f, wide.StrokeMinLength);
        Assert.Equal(narrow.StrokeMinArea * 4f, wide.StrokeMinArea);
        Assert.Equal(narrow.PlanMinRegion * 4f, wide.PlanMinRegion);
        Assert.Equal(narrow.CutMinLength * 2f, wide.CutMinLength);
        Assert.Equal(narrow.BorderPixels * 2, wide.BorderPixels);
    }

    [Fact]
    public void TheDerivedSizesAtFullHdFollowTheUpstreamRules()
    {
        var derived = MangaScreentoneSettings.Derive(1920, 1080, Parameters(margin: 60f));

        Assert.Equal(1.875f, derived.Scale);
        Assert.Equal(29, derived.LineMeanSize);
        Assert.Equal(8, derived.BilateralRadius);
        Assert.Equal(21, derived.PlanMedianSize);
        Assert.Equal(39, derived.PlanFillMedianSize);
        Assert.Equal(5, derived.BetaSize);
        Assert.Equal(3, derived.CutErodeSize);
        Assert.Equal(9, derived.BorderPixels);
        Assert.Equal(112, derived.MarginPixels);
        Assert.Equal(1920f / 200f, derived.Pitch);
        Assert.Equal(1.875f * 1.875f * 600f, derived.PlanMinRegion, 3);
    }

    [Theory]
    [InlineData(1024, 2)]
    [InlineData(1500, 2)]
    [InlineData(1638, 2)]
    [InlineData(1639, 3)]
    [InlineData(1700, 3)]
    public void TheCutErodeKernelIsTwoBelowAScaleOfOnePointSix(int width, int expected)
    {
        Assert.Equal(expected, MangaScreentoneSettings.Derive(width, 100, Parameters()).CutErodeSize);
    }

    [Fact]
    public void TheMedianKernelsNeverExceedWhatTheCountsCanHold()
    {
        var derived = MangaScreentoneSettings.Derive(40000, 100, Parameters());

        Assert.Equal(MangaScreentoneSettings.MaximumMedianSize, derived.PlanMedianSize);
        Assert.Equal(MangaScreentoneSettings.MaximumMedianSize, derived.PlanFillMedianSize);
        Assert.True(derived.PlanFillMedianSize * derived.PlanFillMedianSize < 65536);
    }

    [Theory]
    [InlineData(8000, 100, 96)]
    [InlineData(6000, 6000, 1096)]
    [InlineData(8192, 100, 0)]
    public void TheMarginNeverGrowsTheCanvasBeyondTheMaximum(int width, int height, int expected)
    {
        var derived = MangaScreentoneSettings.Derive(width, height, Parameters(margin: 400f));

        Assert.Equal(expected, derived.MarginPixels);
        Assert.True(Math.Max(width, height) + derived.MarginPixels * 2 <= MangaScreentoneSettings.MaximumCanvasSize);
    }

    [Theory]
    [InlineData(-5f, 0)]
    [InlineData(0f, 0)]
    [InlineData(5f, 5)]
    [InlineData(40f, 40)]
    [InlineData(99f, 40)]
    public void TheBorderIsInPixelsAt1024AndIsClamped(float border, int expected)
    {
        Assert.Equal(expected, MangaScreentoneSettings.Derive(1024, 768, Parameters(border: border)).BorderPixels);
    }

    [Theory]
    [InlineData(0f, 0, 0f)]
    [InlineData(1f, 3, 0.6f)]
    public void TheInkSpreadFollowsTheInkAmount(float ink, int expectedRadius, float expectedSigma)
    {
        var derived = MangaScreentoneSettings.Derive(1024, 768, Parameters(ink: ink));

        Assert.Equal(expectedSigma, derived.InkSigma, 5);
        Assert.Equal(expectedRadius, derived.InkRadius);
    }

    [Theory]
    [InlineData(40f, 25.6f)]
    [InlineData(200f, 5.12f)]
    [InlineData(600f, 1024f / 600f)]
    [InlineData(0f, 1024f)]
    public void TheDotPitchIsTheWidthDividedByTheScreenLines(float lines, float expected)
    {
        Assert.Equal(expected, MangaScreentoneSettings.Derive(1024, 768, Parameters(screenLines: lines)).Pitch, 4);
    }

    [Theory]
    [InlineData(MangaScreentonePaper.White, 1f, 1f, 1f, 0f)]
    [InlineData(MangaScreentonePaper.OffWhite, 251f, 250f, 247f, 1.5f)]
    [InlineData(MangaScreentonePaper.Cream, 246f, 243f, 236f, 3f)]
    [InlineData(MangaScreentonePaper.Transparent, 1f, 1f, 1f, 0f)]
    public void EveryPaperHasItsColorAndFibre(MangaScreentonePaper paper, float red, float green, float blue, float fibre)
    {
        var (actualRed, actualGreen, actualBlue, actualFibre) = MangaScreentoneSettings.GetPaper(paper);
        var scale = red > 1f ? 255f : 1f;

        Assert.Equal(red / scale, actualRed, 6);
        Assert.Equal(green / scale, actualGreen, 6);
        Assert.Equal(blue / scale, actualBlue, 6);
        Assert.Equal(fibre / 255f, actualFibre, 6);
    }

    [Fact]
    public void ThePrintAngleIsFortyFiveDegrees()
    {
        Assert.Equal(Math.PI / 4d, MangaScreentoneSettings.PrintAngleRadians, 6);
    }

    [Fact]
    public void ThePrintSupersampleDividesIntoAnOddCountOfSubSamples()
    {
        Assert.Equal(3, MangaScreentoneSettings.PrintSupersample);
        Assert.Equal(1, MangaScreentoneSettings.PrintSupersample * MangaScreentoneSettings.PrintSupersample % 2);
    }

    [Theory]
    [InlineData(-5f, 0)]
    [InlineData(0f, 0)]
    [InlineData(80f, 80)]
    [InlineData(400f, 400)]
    [InlineData(999f, 400)]
    public void TheMarginIsInPixelsAt1024AndIsClamped(float margin, int expected)
    {
        Assert.Equal(expected, MangaScreentoneSettings.Derive(1024, 768, Parameters(margin: margin)).MarginPixels);
    }
}
