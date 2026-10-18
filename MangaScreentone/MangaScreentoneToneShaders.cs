// Stryker disable all
using ComputeWeave;

namespace MangaScreentone;

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct SolidShader(
    ReadWriteBuffer<float> blurred,
    ReadWriteBuffer<uint> solid,
    int width,
    int height,
    float threshold) : IComputeShader
{
    private readonly ReadWriteBuffer<float> blurred = blurred;
    private readonly ReadWriteBuffer<uint> solid = solid;
    private readonly int width = width;
    private readonly int height = height;
    private readonly float threshold = threshold;

    public void Execute()
    {
        var x = ThreadIds.X;
        var y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        var index = y * width + x;
        solid[index] = blurred[index] < threshold ? 1u : 0u;
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct LineMaskShader(
    ReadWriteBuffer<float> line,
    ReadWriteBuffer<float> mean,
    ReadWriteBuffer<uint> classes,
    int width,
    int height,
    float contrast,
    float ceiling) : IComputeShader
{
    private readonly ReadWriteBuffer<float> line = line;
    private readonly ReadWriteBuffer<float> mean = mean;
    private readonly ReadWriteBuffer<uint> classes = classes;
    private readonly int width = width;
    private readonly int height = height;
    private readonly float contrast = contrast;
    private readonly float ceiling = ceiling;

    public void Execute()
    {
        var x = ThreadIds.X;
        var y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        var index = y * width + x;
        var value = line[index];
        var isLine = value < mean[index] - contrast && value < ceiling;
        classes[index] = isLine ? 0u : MangaScreentoneSettings.InactiveClass;
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct LineKeepShader(
    ReadWriteBuffer<uint> classes,
    ReadWriteBuffer<uint> labels,
    ReadWriteBuffer<uint> area,
    ReadWriteBuffer<uint> minX,
    ReadWriteBuffer<uint> minY,
    ReadWriteBuffer<uint> maxX,
    ReadWriteBuffer<uint> maxY,
    ReadWriteBuffer<uint> solid,
    int width,
    int height,
    float minLength,
    float minArea,
    float fillRatio,
    float fillBias,
    float largeArea) : IComputeShader
{
    private readonly ReadWriteBuffer<uint> classes = classes;
    private readonly ReadWriteBuffer<uint> labels = labels;
    private readonly ReadWriteBuffer<uint> area = area;
    private readonly ReadWriteBuffer<uint> minX = minX;
    private readonly ReadWriteBuffer<uint> minY = minY;
    private readonly ReadWriteBuffer<uint> maxX = maxX;
    private readonly ReadWriteBuffer<uint> maxY = maxY;
    private readonly ReadWriteBuffer<uint> solid = solid;
    private readonly int width = width;
    private readonly int height = height;
    private readonly float minLength = minLength;
    private readonly float minArea = minArea;
    private readonly float fillRatio = fillRatio;
    private readonly float fillBias = fillBias;
    private readonly float largeArea = largeArea;

    public void Execute()
    {
        var x = ThreadIds.X;
        var y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        var index = y * width + x;
        var keep = solid[index] != 0u;
        if (!keep && classes[index] != MangaScreentoneSettings.InactiveClass)
        {
            var root = (int)labels[index];
            var componentArea = (float)area[root];
            var componentWidth = (float)(maxX[root] - minX[root] + 1u);
            var componentHeight = (float)(maxY[root] - minY[root] + 1u);
            var longSide = Hlsl.Max(componentWidth, componentHeight);
            var stroke = longSide >= minLength
                && componentArea >= minArea
                && componentArea < fillRatio * componentWidth * componentHeight + fillBias;
            keep = stroke || componentArea >= largeArea;
        }
        classes[index] = keep ? 0u : MangaScreentoneSettings.InactiveClass;
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct InkFinalShader(
    ReadWriteBuffer<uint> classes,
    ReadWriteBuffer<uint> labels,
    ReadWriteBuffer<uint> area,
    ReadWriteBuffer<uint> ink,
    int width,
    int height,
    float minArea) : IComputeShader
{
    private readonly ReadWriteBuffer<uint> classes = classes;
    private readonly ReadWriteBuffer<uint> labels = labels;
    private readonly ReadWriteBuffer<uint> area = area;
    private readonly ReadWriteBuffer<uint> ink = ink;
    private readonly int width = width;
    private readonly int height = height;
    private readonly float minArea = minArea;

    public void Execute()
    {
        var x = ThreadIds.X;
        var y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        var index = y * width + x;
        var kept = classes[index] != MangaScreentoneSettings.InactiveClass && (float)area[(int)labels[index]] >= minArea;
        ink[index] = kept ? 1u : 0u;
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct DigitizeShader(
    ReadWriteBuffer<float> value,
    ReadWriteBuffer<uint> level,
    int width,
    int height,
    float white,
    float split,
    float black) : IComputeShader
{
    private readonly ReadWriteBuffer<float> value = value;
    private readonly ReadWriteBuffer<uint> level = level;
    private readonly int width = width;
    private readonly int height = height;
    private readonly float white = white;
    private readonly float split = split;
    private readonly float black = black;

    public void Execute()
    {
        var x = ThreadIds.X;
        var y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        var index = y * width + x;
        var sample = value[index];
        var result = 0u;
        if (sample <= white)
            result++;
        if (sample <= split)
            result++;
        if (sample <= black)
            result++;
        level[index] = result;
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct MedianCountShader(
    ReadWriteBuffer<uint> level,
    ReadWriteBuffer<uint> countLow,
    ReadWriteBuffer<uint> countHigh,
    int width,
    int height,
    int radius) : IComputeShader
{
    private readonly ReadWriteBuffer<uint> level = level;
    private readonly ReadWriteBuffer<uint> countLow = countLow;
    private readonly ReadWriteBuffer<uint> countHigh = countHigh;
    private readonly int width = width;
    private readonly int height = height;
    private readonly int radius = radius;

    public void Execute()
    {
        var x = ThreadIds.X;
        var y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        var low = 0u;
        var high = 0u;
        var row = y * width;
        for (var offset = -radius; offset <= radius; offset++)
        {
            var value = level[row + MangaScreentoneShaderMath.Clamp(x + offset, width)];
            if (value == 0u)
                low += 1u;
            else if (value == 1u)
                low += MangaScreentoneShaderMath.LaneUnit;
            else if (value == 2u)
                high += 1u;
            else
                high += MangaScreentoneShaderMath.LaneUnit;
        }
        countLow[row + x] = low;
        countHigh[row + x] = high;
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct MedianResolveShader(
    ReadWriteBuffer<uint> countLow,
    ReadWriteBuffer<uint> countHigh,
    ReadWriteBuffer<uint> output,
    int width,
    int height,
    int radius) : IComputeShader
{
    private readonly ReadWriteBuffer<uint> countLow = countLow;
    private readonly ReadWriteBuffer<uint> countHigh = countHigh;
    private readonly ReadWriteBuffer<uint> output = output;
    private readonly int width = width;
    private readonly int height = height;
    private readonly int radius = radius;

    public void Execute()
    {
        var x = ThreadIds.X;
        var y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        var low = 0u;
        var high = 0u;
        for (var offset = -radius; offset <= radius; offset++)
        {
            var index = MangaScreentoneShaderMath.Clamp(y + offset, height) * width + x;
            low += countLow[index];
            high += countHigh[index];
        }
        output[y * width + x] = MangaScreentoneShaderMath.Median(low, high, (uint)((2 * radius + 1) * (2 * radius + 1)));
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct ClassFromLevelShader(
    ReadWriteBuffer<uint> level,
    ReadWriteBuffer<uint> classes,
    int width,
    int height,
    uint target) : IComputeShader
{
    private readonly ReadWriteBuffer<uint> level = level;
    private readonly ReadWriteBuffer<uint> classes = classes;
    private readonly int width = width;
    private readonly int height = height;
    private readonly uint target = target;

    public void Execute()
    {
        var x = ThreadIds.X;
        var y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        var index = y * width + x;
        classes[index] = level[index] == target ? 0u : MangaScreentoneSettings.InactiveClass;
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct HoleMarkShader(
    ReadWriteBuffer<uint> level,
    ReadWriteBuffer<uint> labels,
    ReadWriteBuffer<uint> area,
    ReadWriteBuffer<uint> marked,
    int width,
    int height,
    uint target,
    float minRegion) : IComputeShader
{
    private readonly ReadWriteBuffer<uint> level = level;
    private readonly ReadWriteBuffer<uint> labels = labels;
    private readonly ReadWriteBuffer<uint> area = area;
    private readonly ReadWriteBuffer<uint> marked = marked;
    private readonly int width = width;
    private readonly int height = height;
    private readonly uint target = target;
    private readonly float minRegion = minRegion;

    public void Execute()
    {
        var x = ThreadIds.X;
        var y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        var index = y * width + x;
        var value = level[index];
        var hole = value == target && (float)area[(int)labels[index]] < minRegion;
        marked[index] = hole ? MangaScreentoneSettings.HoleMarker : value;
    }
}

[ThreadGroupSize(MangaScreentoneSettings.BlurGroupSize, MangaScreentoneSettings.BlurGroupSize, 1)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct HoleCountShader(
    ReadWriteBuffer<uint> marked,
    ReadWriteBuffer<uint> countLow,
    ReadWriteBuffer<uint> countHigh,
    int width,
    int height,
    int radius) : IComputeShader
{
    private readonly ReadWriteBuffer<uint> marked = marked;
    private readonly ReadWriteBuffer<uint> countLow = countLow;
    private readonly ReadWriteBuffer<uint> countHigh = countHigh;
    private readonly int width = width;
    private readonly int height = height;
    private readonly int radius = radius;

    [GroupShared(1)]
    private static readonly int[] holeNearby = null!;

    public void Execute()
    {
        if (GroupIds.Index == 0)
            holeNearby[0] = 0;
        Hlsl.GroupMemoryBarrierWithGroupSync();

        var left = ThreadIds.X - GroupIds.X;
        var top = ThreadIds.Y - GroupIds.Y;
        var span = MangaScreentoneSettings.BlurGroupSize + 2 * radius;
        for (var cell = GroupIds.Index; cell < MangaScreentoneSettings.BlurGroupSize * span; cell += MangaScreentoneSettings.BlurGroupSize * MangaScreentoneSettings.BlurGroupSize)
        {
            var column = left + cell % MangaScreentoneSettings.BlurGroupSize;
            var line = top - radius + cell / MangaScreentoneSettings.BlurGroupSize;
            if (column < width && line >= 0 && line < height && marked[line * width + column] == MangaScreentoneSettings.HoleMarker)
                Hlsl.InterlockedOr(ref holeNearby[0], 1);
        }
        Hlsl.GroupMemoryBarrierWithGroupSync();
        if (holeNearby[0] == 0)
            return;

        var x = ThreadIds.X;
        var y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        var low = 0u;
        var high = 0u;
        var row = y * width;
        for (var offset = -radius; offset <= radius; offset++)
        {
            var value = marked[row + MangaScreentoneShaderMath.Clamp(x + offset, width)];
            if (value == 0u)
                low += 1u;
            else if (value == 1u)
                low += MangaScreentoneShaderMath.LaneUnit;
            else if (value == 2u)
                high += 1u;
            else if (value == 3u)
                high += MangaScreentoneShaderMath.LaneUnit;
        }
        countLow[row + x] = low;
        countHigh[row + x] = high;
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct HoleResolveShader(
    ReadWriteBuffer<uint> level,
    ReadWriteBuffer<uint> marked,
    ReadWriteBuffer<uint> countLow,
    ReadWriteBuffer<uint> countHigh,
    ReadWriteBuffer<uint> output,
    int width,
    int height,
    int radius) : IComputeShader
{
    private readonly ReadWriteBuffer<uint> level = level;
    private readonly ReadWriteBuffer<uint> marked = marked;
    private readonly ReadWriteBuffer<uint> countLow = countLow;
    private readonly ReadWriteBuffer<uint> countHigh = countHigh;
    private readonly ReadWriteBuffer<uint> output = output;
    private readonly int width = width;
    private readonly int height = height;
    private readonly int radius = radius;

    public void Execute()
    {
        var x = ThreadIds.X;
        var y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        var index = y * width + x;
        var current = level[index];
        if (marked[index] != MangaScreentoneSettings.HoleMarker)
        {
            output[index] = current;
            return;
        }

        var low = 0u;
        var high = 0u;
        for (var offset = -radius; offset <= radius; offset++)
        {
            var sample = MangaScreentoneShaderMath.Clamp(y + offset, height) * width + x;
            low += countLow[sample];
            high += countHigh[sample];
        }
        var fill = MangaScreentoneShaderMath.Median(low, high, (uint)((2 * radius + 1) * (2 * radius + 1)));
        output[index] = fill != MangaScreentoneSettings.HoleMarker ? fill : current;
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct SpreadResolveShader(
    ReadWriteBuffer<uint> level,
    ReadWriteBuffer<uint> labels,
    ReadWriteBuffer<uint> area,
    ReadWriteBuffer<uint> sum,
    ReadWriteBuffer<uint> squareLow,
    ReadWriteBuffer<uint> squareHigh,
    ReadWriteBuffer<float> output,
    int width,
    int height,
    float floor,
    float range) : IComputeShader
{
    private readonly ReadWriteBuffer<uint> level = level;
    private readonly ReadWriteBuffer<uint> labels = labels;
    private readonly ReadWriteBuffer<uint> area = area;
    private readonly ReadWriteBuffer<uint> sum = sum;
    private readonly ReadWriteBuffer<uint> squareLow = squareLow;
    private readonly ReadWriteBuffer<uint> squareHigh = squareHigh;
    private readonly ReadWriteBuffer<float> output = output;
    private readonly int width = width;
    private readonly int height = height;
    private readonly float floor = floor;
    private readonly float range = range;

    public void Execute()
    {
        var x = ThreadIds.X;
        var y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        var index = y * width + x;
        var root = (int)labels[index];
        var count = Hlsl.Max((float)area[root], 1f);
        var mean = (float)sum[root] / count;
        var meanSquare = ((float)squareHigh[root] * 256f + (float)squareLow[root]) / count;
        var variance = Hlsl.Max(meanSquare - mean * mean, 0f);
        var deviation = Hlsl.Sqrt(variance) / 255f;
        output[index] = Hlsl.Saturate((deviation - floor) / range);
    }
}
