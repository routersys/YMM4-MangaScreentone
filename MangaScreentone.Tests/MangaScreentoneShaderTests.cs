using ComputeWeave;

namespace MangaScreentone.Tests;

[Collection("Direct3D12")]
public sealed class MangaScreentoneShaderTests
{
    static readonly bool Direct3D12IsAvailable = GraphicsDevice.EnumerateDevices().Any();

    static GraphicsDevice Device()
    {
        if (!Direct3D12IsAvailable)
            Assert.Skip("Direct3D 12 is unavailable.");
        return GraphicsDevice.GetDefault();
    }

    static ReadWriteBuffer<T> Upload<T>(GraphicsDevice device, T[] values) where T : unmanaged
    {
        var buffer = device.AllocateReadWriteBuffer<T>(values.Length);
        buffer.CopyFrom(values);
        return buffer;
    }

    static T[] Download<T>(ReadWriteBuffer<T> buffer) where T : unmanaged
    {
        var values = new T[buffer.Length];
        buffer.CopyTo(values);
        return values;
    }

    static uint Hash(uint value)
    {
        value ^= value >> 16;
        value *= 0x7FEB352Du;
        value ^= value >> 15;
        value *= 0x846CA68Bu;
        value ^= value >> 16;
        return value;
    }

    static double Unit(int index, uint salt) => Hash((uint)index * 0x9E3779B9u ^ salt) / 4294967296d;

    static uint[] Speckle(int width, int height, double density, uint salt)
        => Enumerable.Range(0, width * height).Select(index => Unit(index, salt) < density ? 0u : MangaScreentoneSettings.InactiveClass).ToArray();

    static uint[] Comb(int width, int height)
    {
        var classes = Enumerable.Repeat(MangaScreentoneSettings.InactiveClass, width * height).ToArray();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (y == 1 || x % 4 == 1 && y > 1)
                    classes[y * width + x] = 0u;
            }
        }

        return classes;
    }

    static uint[] Spiral(int width, int height)
    {
        var classes = Enumerable.Repeat(MangaScreentoneSettings.InactiveClass, width * height).ToArray();
        int left = 0, top = 0, right = width - 1, bottom = height - 1;
        while (left < right && top < bottom)
        {
            for (var x = left; x <= right; x++)
                classes[top * width + x] = 0u;
            for (var y = top; y <= bottom; y++)
                classes[y * width + right] = 0u;
            for (var x = right; x >= left; x--)
                classes[bottom * width + x] = 0u;
            for (var y = bottom - 1; y > top + 1; y--)
                classes[y * width + left] = 0u;
            left += 2;
            top += 2;
            right -= 2;
            bottom -= 2;
        }

        return classes;
    }

    static uint[] Levels(int width, int height, uint salt)
        => Enumerable.Range(0, width * height).Select(index =>
        {
            var x = index % width;
            var y = index / width;
            var coarse = Hash((uint)((x / 5) * 73856093 ^ (y / 4) * 19349663) ^ salt);
            return coarse % 4u;
        }).ToArray();

    sealed record Labelled(uint[] Labels, uint[] Area, uint[] MinX, uint[] MinY, uint[] MaxX, uint[] MaxY);

    static Labelled RunComponents(GraphicsDevice device, uint[] classes, int width, int height, bool diagonal)
    {
        var length = width * height;
        using var classBuffer = Upload(device, classes);
        using var labels = device.AllocateReadWriteBuffer<uint>(length);
        using var area = device.AllocateReadWriteBuffer<uint>(length);
        using var minX = device.AllocateReadWriteBuffer<uint>(length);
        using var minY = device.AllocateReadWriteBuffer<uint>(length);
        using var maxX = device.AllocateReadWriteBuffer<uint>(length);
        using var maxY = device.AllocateReadWriteBuffer<uint>(length);
        var connectivity = diagonal ? 1 : 0;
        device.For(width, height, new ComponentInitShader(classBuffer, labels, width, height, connectivity));
        device.For(width, height, new ComponentUnionShader(classBuffer, labels, width, height, connectivity));
        device.For(width, height, new ComponentFlattenShader(classBuffer, labels, area, minX, minY, maxX, maxY, width, height, 1));
        device.For(ThreadGroupAlignment.AlignX<ComponentBoundsShader>(width), ThreadGroupAlignment.AlignY<ComponentBoundsShader>(height), new ComponentBoundsShader(classBuffer, labels, area, minX, minY, maxX, maxY, width, height));
        return new Labelled(Download(labels), Download(area), Download(minX), Download(minY), Download(maxX), Download(maxY));
    }

    static void AssertSameComponents(uint[] classes, int width, int height, bool diagonal)
    {
        var device = Device();
        var expected = MangaScreentoneReference.Label(classes, width, height, diagonal);

        var actual = RunComponents(device, classes, width, height, diagonal);

        for (var index = 0; index < classes.Length; index++)
        {
            if (classes[index] == MangaScreentoneSettings.InactiveClass)
                continue;
            var root = expected.Labels[index];
            Assert.True((int)actual.Labels[index] == root, $"label {index}: expected {root}, actual {actual.Labels[index]}");
            if (root != index)
                continue;
            Assert.Equal(expected.Area[root], (int)actual.Area[root]);
            Assert.Equal(expected.MinX[root], (int)actual.MinX[root]);
            Assert.Equal(expected.MinY[root], (int)actual.MinY[root]);
            Assert.Equal(expected.MaxX[root], (int)actual.MaxX[root]);
            Assert.Equal(expected.MaxY[root], (int)actual.MaxY[root]);
        }
    }

    [Theory]
    [InlineData(37, 29, 0.35, false)]
    [InlineData(37, 29, 0.35, true)]
    [InlineData(64, 48, 0.55, false)]
    [InlineData(64, 48, 0.55, true)]
    [InlineData(61, 53, 0.65, false)]
    [InlineData(61, 53, 0.45, true)]
    [InlineData(200, 120, 0.5, true)]
    public void ComponentsOfScatteredPixelsMatchTheReferenceExactly(int width, int height, double density, bool diagonal)
    {
        AssertSameComponents(Speckle(width, height, density, 0x1234u), width, height, diagonal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ALongCombIsOneComponentEvenWhenItsPathIsLong(bool diagonal)
    {
        AssertSameComponents(Comb(130, 41), 130, 41, diagonal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ASpiralIsOneComponentEvenWhenItsPathIsLong(bool diagonal)
    {
        AssertSameComponents(Spiral(91, 77), 91, 77, diagonal);
    }

    [Fact]
    public void AFullImageIsOneComponentRootedAtTheOrigin()
    {
        var classes = new uint[70 * 50];

        var actual = RunComponents(Device(), classes, 70, 50, false);

        Assert.All(actual.Labels, label => Assert.Equal(0u, label));
        Assert.Equal((uint)classes.Length, actual.Area[0]);
        Assert.Equal((0u, 0u, 69u, 49u), (actual.MinX[0], actual.MinY[0], actual.MaxX[0], actual.MaxY[0]));
    }

    [Fact]
    public void AnEmptyImageHasNoComponents()
    {
        var classes = Enumerable.Repeat(MangaScreentoneSettings.InactiveClass, 33 * 21).ToArray();

        var actual = RunComponents(Device(), classes, 33, 21, true);

        Assert.All(actual.Labels, label => Assert.Equal(MangaScreentoneSettings.InactiveClass, label));
    }

    [Fact]
    public void PixelsOfDifferentClassesNeverJoin()
    {
        var classes = Levels(60, 40, 0x55u);
        AssertSameComponents(classes, 60, 40, false);
        var actual = RunComponents(Device(), classes, 60, 40, false);

        for (var index = 0; index < classes.Length; index++)
            Assert.Equal(classes[index], classes[(int)actual.Labels[index]]);
    }

    [Fact]
    public void TheDiagonalConnectivityJoinsWhatTheFourNeighborhoodSeparates()
    {
        var classes = Enumerable.Repeat(MangaScreentoneSettings.InactiveClass, 4 * 4).ToArray();
        classes[0] = 0u;
        classes[5] = 0u;
        classes[10] = 0u;
        var device = Device();

        var four = RunComponents(device, classes, 4, 4, false);
        var eight = RunComponents(device, classes, 4, 4, true);

        Assert.Equal([0u, 5u, 10u], new[] { four.Labels[0], four.Labels[5], four.Labels[10] });
        Assert.Equal([0u, 0u, 0u], new[] { eight.Labels[0], eight.Labels[5], eight.Labels[10] });
    }

    static double[] Smooth(int width, int height, uint salt)
        => Enumerable.Range(0, width * height).Select(index =>
        {
            var x = index % width;
            var y = index / width;
            return 0.5 + 0.35 * Math.Sin(x * 0.21 + salt) * Math.Cos(y * 0.17) + 0.15 * (Unit(index, salt) - 0.5);
        }).ToArray();

    static float[] Floats(double[] values) => values.Select(value => (float)value).ToArray();

    static void AssertClose(double[] expected, float[] actual, double tolerance)
    {
        Assert.Equal(expected.Length, actual.Length);
        var worst = 0d;
        for (var index = 0; index < expected.Length; index++)
            worst = Math.Max(worst, Math.Abs(expected[index] - actual[index]));
        Assert.True(worst <= tolerance, $"worst difference {worst}");
    }

    [Theory]
    [InlineData(43, 31, 1.4, 6)]
    [InlineData(43, 31, 0.35, 2)]
    [InlineData(20, 12, 3.0, 12)]
    [InlineData(64, 40, 2.8125, 12)]
    public void TheGaussianBlurMatchesTheReferenceWithReflectedBorders(int width, int height, double sigma, int radius)
    {
        var device = Device();
        var source = Smooth(width, height, 3u);
        using var input = Upload(device, Floats(source));
        using var temporary = device.AllocateReadWriteBuffer<float>(source.Length);
        using var output = device.AllocateReadWriteBuffer<float>(source.Length);

        device.For(ThreadGroupAlignment.AlignX<GaussianBlurShader>(width), ThreadGroupAlignment.AlignY<GaussianBlurShader>(height), new GaussianBlurShader(input, temporary, width, height, (float)sigma, radius, 0));
        device.For(ThreadGroupAlignment.AlignX<GaussianBlurShader>(width), ThreadGroupAlignment.AlignY<GaussianBlurShader>(height), new GaussianBlurShader(temporary, output, width, height, (float)sigma, radius, 1));

        AssertClose(MangaScreentoneReference.Gaussian(source, width, height, sigma, radius), Download(output), 2e-6);
    }

    [Theory]
    [InlineData(41, 29, 3)]
    [InlineData(41, 29, 15)]
    [InlineData(10, 8, 29)]
    public void TheBoxBlurMatchesTheReferenceWithReflectedBorders(int width, int height, int size)
    {
        var device = Device();
        var source = Smooth(width, height, 5u);
        using var input = Upload(device, Floats(source));
        using var temporary = device.AllocateReadWriteBuffer<float>(source.Length);
        using var output = device.AllocateReadWriteBuffer<float>(source.Length);

        device.For(width, height, new BoxBlurShader(input, temporary, width, height, size / 2, 0));
        device.For(width, height, new BoxBlurShader(temporary, output, width, height, size / 2, 1));

        AssertClose(MangaScreentoneReference.Box(source, width, height, size), Download(output), 2e-6);
    }

    [Fact]
    public void TheMaskedBlurIgnoresInkPixelsAndFillsFromTheirSurroundings()
    {
        const int width = 47;
        const int height = 33;
        var device = Device();
        var gray = Smooth(width, height, 7u);
        var ink = Enumerable.Range(0, width * height).Select(index => index % width is > 10 and < 22 && index / width is > 8 and < 20).ToArray();
        using var grayBuffer = Upload(device, Floats(gray));
        using var inkBuffer = Upload(device, ink.Select(value => value ? 1u : 0u).ToArray());
        using var numerator = device.AllocateReadWriteBuffer<float>(gray.Length);
        using var denominator = device.AllocateReadWriteBuffer<float>(gray.Length);
        using var output = device.AllocateReadWriteBuffer<float>(gray.Length);

        device.For(ThreadGroupAlignment.AlignX<MaskedBlurHorizontalShader>(width), ThreadGroupAlignment.AlignY<MaskedBlurHorizontalShader>(height), new MaskedBlurHorizontalShader(grayBuffer, inkBuffer, numerator, denominator, width, height, 2.5f, 10));
        device.For(ThreadGroupAlignment.AlignX<MaskedBlurVerticalShader>(width), ThreadGroupAlignment.AlignY<MaskedBlurVerticalShader>(height), new MaskedBlurVerticalShader(numerator, denominator, output, width, height, 2.5f, 10, MangaScreentoneSettings.ValueMinWeight));

        AssertClose(MangaScreentoneReference.MaskedValue(gray, ink, width, height, 2.5, 10), Download(output), 5e-6);
    }

    [Theory]
    [InlineData(37, 27, 2)]
    [InlineData(37, 27, 4)]
    [InlineData(9, 7, 8)]
    public void TheBilateralFilterMatchesTheReferenceWithinACircularWindow(int width, int height, int radius)
    {
        var device = Device();
        var source = Smooth(width, height, 11u);
        var space = -0.5 / (9.0 * 9.0);
        var color = -0.5 / (0.1 * 0.1);
        using var input = Upload(device, Floats(source));
        using var output = device.AllocateReadWriteBuffer<float>(source.Length);

        device.For(ThreadGroupAlignment.AlignX<BilateralShader>(width), ThreadGroupAlignment.AlignY<BilateralShader>(height), new BilateralShader(input, output, width, height, radius, (float)space, (float)color));

        AssertClose(MangaScreentoneReference.Bilateral(source, width, height, radius, space, color), Download(output), 5e-6);
    }

    [Theory]
    [InlineData(0.8, 0.55, 0.3)]
    [InlineData(1.0, 0.5, 0.0)]
    public void DigitizingPlacesEveryValueIntoOneOfFourBandsWithTheUpperBoundsIncluded(double white, double split, double black)
    {
        var device = Device();
        var values = new double[] { 0d, black, black + 1e-4, split, split + 1e-4, white, white + 1e-4, 1d }.Select(value => (float)value).ToArray();
        using var input = Upload(device, values);
        using var output = device.AllocateReadWriteBuffer<uint>(values.Length);

        device.For(values.Length, 1, new DigitizeShader(input, output, values.Length, 1, (float)white, (float)split, (float)black));

        var expected = MangaScreentoneReference.Digitize(values.Select(value => (double)value).ToArray(), (float)white, (float)split, (float)black);
        Assert.Equal(expected, Download(output));
    }

    static uint[] RunMedian(GraphicsDevice device, uint[] levels, int width, int height, int size)
    {
        using var input = Upload(device, levels);
        using var low = device.AllocateReadWriteBuffer<uint>(levels.Length);
        using var high = device.AllocateReadWriteBuffer<uint>(levels.Length);
        using var output = device.AllocateReadWriteBuffer<uint>(levels.Length);
        device.For(width, height, new MedianCountShader(input, low, high, width, height, size / 2));
        device.For(width, height, new MedianResolveShader(low, high, output, width, height, size / 2));
        return Download(output);
    }

    [Theory]
    [InlineData(45, 31, 3)]
    [InlineData(45, 31, 7)]
    [InlineData(45, 31, 21)]
    [InlineData(12, 9, 39)]
    public void TheMedianOfFourLevelsMatchesTheSortedWindowWithReplicatedBorders(int width, int height, int size)
    {
        var levels = Levels(width, height, 0x99u);

        var actual = RunMedian(Device(), levels, width, height, size);

        Assert.Equal(MangaScreentoneReference.Median(levels, width, height, size), actual);
    }

    [Theory]
    [InlineData(45, 31, 5, 1u)]
    [InlineData(45, 31, 13, 2u)]
    [InlineData(45, 31, 13, 3u)]
    [InlineData(45, 31, 13, 0u)]
    [InlineData(14, 11, 39, 1u)]
    public void SmallRegionsOfTheTargetLevelAreFilledFromTheirSurroundingsAndTheRestStays(int width, int height, int size, uint target)
    {
        AssertSmallRegionsFilled(Levels(width, height, 0x42u), width, height, size, target);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(13)]
    [InlineData(21)]
    public void SmallRegionsFarApartAreFilledWhileTheGroupsBetweenThemHaveNoHolesToCount(int size)
    {
        const int width = 130;
        const int height = 100;
        var levels = Enumerable.Repeat(3u, width * height).ToArray();
        foreach (var (left, top, regionWidth, regionHeight) in new[] { (9, 7, 3, 3), (118, 90, 2, 4), (64, 2, 1, 1), (3, 60, 4, 2) })
        {
            for (var y = top; y < top + regionHeight; y++)
            {
                for (var x = left; x < left + regionWidth; x++)
                    levels[y * width + x] = 1u;
            }
        }

        AssertSmallRegionsFilled(levels, width, height, size, 1u);
    }

    static void AssertSmallRegionsFilled(uint[] levels, int width, int height, int size, uint target)
    {
        var device = Device();
        var components = MangaScreentoneReference.Label(levels.Select(level => level == target ? 0u : MangaScreentoneSettings.InactiveClass).ToArray(), width, height, false);
        const float minimumRegion = 24f;
        var expectedMedian = MangaScreentoneReference.Median(levels, width, height, size, index => levels[index] == target && components.Area[components.Labels[index]] < minimumRegion);
        var expected = levels.Select((level, index) => level == target && components.Area[components.Labels[index]] < minimumRegion && expectedMedian[index] != 255u ? expectedMedian[index] : level).ToArray();
        var length = levels.Length;
        var classes = levels.Select(level => level == target ? 0u : MangaScreentoneSettings.InactiveClass).ToArray();
        using var levelBuffer = Upload(device, levels);
        using var classBuffer = Upload(device, classes);
        using var labels = device.AllocateReadWriteBuffer<uint>(length);
        using var area = device.AllocateReadWriteBuffer<uint>(length);
        using var low = device.AllocateReadWriteBuffer<uint>(length);
        using var high = device.AllocateReadWriteBuffer<uint>(length);
        using var unusedC = device.AllocateReadWriteBuffer<uint>(length);
        using var unusedD = device.AllocateReadWriteBuffer<uint>(length);
        using var unusedE = device.AllocateReadWriteBuffer<uint>(length);
        using var output = device.AllocateReadWriteBuffer<uint>(length);
        device.For(width, height, new ComponentInitShader(classBuffer, labels, width, height, 0));
        device.For(width, height, new ComponentUnionShader(classBuffer, labels, width, height, 0));
        device.For(width, height, new ComponentFlattenShader(classBuffer, labels, area, unusedC, unusedD, unusedE, high, width, height, 0));
        device.For(ThreadGroupAlignment.AlignX<ComponentAreaShader>(width), ThreadGroupAlignment.AlignY<ComponentAreaShader>(height), new ComponentAreaShader(classBuffer, labels, area, width, height));

        device.For(width, height, new HoleMarkShader(levelBuffer, labels, area, classBuffer, width, height, target, minimumRegion));
        device.For(ThreadGroupAlignment.AlignX<HoleCountShader>(width), ThreadGroupAlignment.AlignY<HoleCountShader>(height), new HoleCountShader(classBuffer, low, high, width, height, size / 2));
        device.For(width, height, new HoleResolveShader(levelBuffer, classBuffer, low, high, output, width, height, size / 2));

        Assert.Equal(expected, Download(output));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    public void ErodingKeepsOnlyPixelsWhoseWholeWindowIsInsideTheTargetAndIgnoresOutsideTheImage(int size)
    {
        const int width = 41;
        const int height = 23;
        var device = Device();
        var levels = Enumerable.Range(0, width * height).Select(index => Unit(index, 0x77u) < 0.8 || index % width < 3 ? 3u : 1u).ToArray();
        using var input = Upload(device, levels);
        using var output = device.AllocateReadWriteBuffer<uint>(levels.Length);

        device.For(width, height, new ErodeShader(input, output, width, height, size, 3u));

        var expected = MangaScreentoneReference.Erode(levels.Select(level => level == 3u).ToArray(), width, height, size);
        Assert.Equal(expected.Select(value => value ? 1u : 0u), Download(output));
    }

    [Fact]
    public void TheRegionSpreadIsTheDeviationOfAFlatRegionAndTheRangeOfARampRegion()
    {
        const int width = 64;
        const int height = 32;
        var device = Device();
        var level = Enumerable.Range(0, width * height).Select(index => index % width < 32 ? 0u : 1u).ToArray();
        var smooth = Enumerable.Range(0, width * height).Select(index => index % width < 32 ? 0.6 : 0.2 + 0.6 * (index % width - 32) / 31d).ToArray();
        var length = width * height;
        using var levelBuffer = Upload(device, level);
        using var values = Upload(device, Floats(smooth));
        using var labels = device.AllocateReadWriteBuffer<uint>(length);
        using var area = device.AllocateReadWriteBuffer<uint>(length);
        using var sum = device.AllocateReadWriteBuffer<uint>(length);
        using var low = device.AllocateReadWriteBuffer<uint>(length);
        using var high = device.AllocateReadWriteBuffer<uint>(length);
        using var unused = device.AllocateReadWriteBuffer<uint>(length);
        using var output = device.AllocateReadWriteBuffer<float>(length);
        device.For(width, height, new ComponentInitShader(levelBuffer, labels, width, height, 0));
        device.For(width, height, new ComponentUnionShader(levelBuffer, labels, width, height, 0));
        device.For(width, height, new ComponentFlattenShader(levelBuffer, labels, area, sum, low, high, unused, width, height, 0));
        device.For(ThreadGroupAlignment.AlignX<ComponentMomentShader>(width), ThreadGroupAlignment.AlignY<ComponentMomentShader>(height), new ComponentMomentShader(levelBuffer, labels, values, area, sum, low, high, width, height));

        device.For(width, height, new SpreadResolveShader(levelBuffer, labels, area, sum, low, high, output, width, height, 0.03f, 0.05f));

        var expected = MangaScreentoneReference.Spread(level, smooth, width, height);
        AssertClose(expected, Download(output), 1e-3);
        var actual = Download(output);
        Assert.Equal(0f, actual[5]);
        Assert.Equal(1f, actual[40]);
    }

    [Fact]
    public void TheCoverFollowsTheToneWithAGradationBlendTowardTheDarknessRamp()
    {
        const int width = 33;
        const int height = 17;
        var device = Device();
        var level = Levels(width, height, 0x31u);
        var smooth = Smooth(width, height, 13u);
        var accent = Enumerable.Range(0, width * height).Select(index => Unit(index, 0x17u)).ToArray();
        var length = width * height;
        using var levelBuffer = Upload(device, level);
        using var smoothBuffer = Upload(device, Floats(smooth));
        using var accentBuffer = Upload(device, Floats(accent));
        using var output = device.AllocateReadWriteBuffer<float>(length);
        var parameters = new MangaScreentoneSettings.Parameters(200f, 0.8f, 0.3f, 0.18f, 0.42f, 0.6f, true, 0.2f, 0.7f, 1.3f, 5f, 0f, MangaScreentonePaper.White, 0.5f);
        var derived = MangaScreentoneSettings.Derive(width, height, parameters);
        var analysis = new MangaScreentoneReference.Analysis([], [], [], [], level, smooth, accent);

        device.For(width, height, new CoverShader(levelBuffer, smoothBuffer, accentBuffer, output, width, height, derived.White, derived.Black, 0.18f, 0.42f, 0.6f));

        AssertClose(MangaScreentoneReference.Cover(analysis, derived, parameters), Download(output), 2e-6);
    }

    [Theory]
    [InlineData(0.0, 4.0)]
    [InlineData(0.18, 5.12)]
    [InlineData(0.42, 3.3)]
    [InlineData(0.8, 7.0)]
    [InlineData(1.0, 5.12)]
    public void ThePrintedDotsAgreeWithTheReferenceForAUniformCoverage(double coverage, double pitch)
    {
        const int width = 96;
        const int height = 64;
        var device = Device();
        var cover = Enumerable.Repeat((float)coverage, width * height).ToArray();
        using var coverBuffer = Upload(device, cover);
        using var page = device.AllocateReadWriteBuffer<uint>(cover.Length);

        device.For(width, height, new PrintShader(coverBuffer, page, width, height, (float)pitch));

        var expected = MangaScreentoneReference.Print(cover.Select(value => (double)value).ToArray(), width, height, (float)pitch);
        var actual = Download(page);
        var mismatches = expected.Where((value, index) => (actual[index] != 0u) != value).Count();
        Assert.True(mismatches <= width * height / 500, $"{mismatches} of {width * height} differ");
        var printed = actual.Count(value => value != 0u) / (double)actual.Length;
        if (coverage == 0d)
            Assert.Equal(0d, printed);
        if (coverage == 1d)
            Assert.True(printed > 0.995d, $"{printed}");
        if (coverage is > 0d and < 1d)
            Assert.InRange(printed, coverage - 0.12d, coverage + 0.12d);
    }

    [Fact]
    public void ThePrintedDotsFollowACoverageRampFromLightToDark()
    {
        const int width = 160;
        const int height = 48;
        var device = Device();
        var cover = Enumerable.Range(0, width * height).Select(index => index % width / (float)(width - 1)).ToArray();
        using var coverBuffer = Upload(device, cover);
        using var page = device.AllocateReadWriteBuffer<uint>(cover.Length);

        device.For(width, height, new PrintShader(coverBuffer, page, width, height, 5.12f));

        var actual = Download(page);
        var expected = MangaScreentoneReference.Print(cover.Select(value => (double)value).ToArray(), width, height, 5.12f);
        var mismatches = expected.Where((value, index) => (actual[index] != 0u) != value).Count();
        Assert.True(mismatches <= width * height / 250, $"{mismatches} of {width * height} differ");
        double Share(int from, int to) => Enumerable.Range(0, height).SelectMany(y => Enumerable.Range(from, to - from).Select(x => actual[y * width + x])).Count(value => value != 0u) / (double)(height * (to - from));
        Assert.True(Share(0, 40) < Share(60, 100) && Share(60, 100) < Share(120, 160));
    }

    [Fact]
    public void ThePageCombinesPrintAndInkAndCutsWhiteLinesOnlyInsideTheBeta()
    {
        const int width = 8;
        const int height = 1;
        var device = Device();
        var page = new uint[] { 0, 1, 0, 1, 0, 1, 0, 1 };
        var ink = new uint[] { 0, 0, 1, 1, 0, 0, 1, 1 };
        var beta = new uint[] { 0, 0, 0, 0, 1, 1, 1, 1 };
        var cut = new uint[] { 1, 1, 1, 1, 0, 1, 0, 1 };
        using var plain = Upload(device, page);
        using var inkBuffer = Upload(device, ink);
        using var betaBuffer = Upload(device, beta);
        using var cutBuffer = Upload(device, cut);

        device.For(width, height, new PageShader(plain, inkBuffer, betaBuffer, cutBuffer, width, height, 0));

        Assert.Equal([0u, 1u, 1u, 1u, 0u, 1u, 1u, 1u], Download(plain));

        using var withLines = Upload(device, page);
        device.For(width, height, new PageShader(withLines, inkBuffer, betaBuffer, cutBuffer, width, height, 1));

        Assert.Equal([0u, 1u, 1u, 1u, 1u, 0u, 1u, 0u], Download(withLines));
    }

    [Fact]
    public void TheNoiseHasUnitVarianceAndNoLargeStructureAndRepeatsExactly()
    {
        const int width = 256;
        const int height = 256;
        var first = new double[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
                first[y * width + x] = MangaScreentoneReference.Noise(x, y, width, height, 1, 2u, 0x68E31DA4u);
        }

        var mean = first.Average();
        var variance = first.Select(value => (value - mean) * (value - mean)).Average();

        Assert.InRange(mean, -0.1, 0.1);
        Assert.InRange(variance, 0.8, 1.2);
        Assert.Equal(first[1234], MangaScreentoneReference.Noise(1234 % width, 1234 / width, width, height, 1, 2u, 0x68E31DA4u));
    }
}
