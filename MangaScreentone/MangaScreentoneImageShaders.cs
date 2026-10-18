// Stryker disable all
using ComputeWeave;

namespace MangaScreentone;

[ThreadGroupSize(DefaultThreadGroupSizes.X)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct ScratchResetShader(
    ReadWriteBuffer<int> scratch,
    int length) : IComputeShader
{
    private readonly ReadWriteBuffer<int> scratch = scratch;
    private readonly int length = length;

    public void Execute()
    {
        if (ThreadIds.X < length)
            scratch[ThreadIds.X] = 0;
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct GrayShader(
    ReadWriteTexture2D<Bgra32, Float4> source,
    ReadWriteBuffer<float> gray,
    ReadWriteBuffer<int> scratch,
    int width,
    int height,
    float levelBlack,
    float levelRange,
    float inverseGamma) : IComputeShader
{
    private readonly ReadWriteTexture2D<Bgra32, Float4> source = source;
    private readonly ReadWriteBuffer<float> gray = gray;
    private readonly ReadWriteBuffer<int> scratch = scratch;
    private readonly int width = width;
    private readonly int height = height;
    private readonly float levelBlack = levelBlack;
    private readonly float levelRange = levelRange;
    private readonly float inverseGamma = inverseGamma;

    [GroupShared(2)]
    private static readonly int[] groupHash = null!;

    public void Execute()
    {
        if (GroupIds.Index == 0)
        {
            groupHash[0] = 0;
            groupHash[1] = 0;
        }
        Hlsl.GroupMemoryBarrierWithGroupSync();

        var x = ThreadIds.X;
        var y = ThreadIds.Y;
        if (x < width && y < height)
        {
            var index = y * width + x;
            var pixel = source[new Int2(x, y)];
            var clear = 1f - pixel.W;
            var luminance = 0.299f * (pixel.X + clear) + 0.587f * (pixel.Y + clear) + 0.114f * (pixel.Z + clear);
            var leveled = Hlsl.Saturate((luminance - levelBlack) / levelRange);
            gray[index] = Hlsl.Pow(leveled, inverseGamma);

            var mixed = MangaScreentoneShaderMath.MixTexel((uint)index, pixel);
            Hlsl.InterlockedAdd(ref groupHash[0], (int)mixed);
            Hlsl.InterlockedXor(ref groupHash[1], (int)(mixed * 0xC2B2AE35u));
        }
        Hlsl.GroupMemoryBarrierWithGroupSync();

        if (GroupIds.Index != 0)
            return;
        Hlsl.InterlockedAdd(ref scratch[0], groupHash[0]);
        Hlsl.InterlockedXor(ref scratch[1], groupHash[1]);
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct GaussianBlurShader(
    ReadWriteBuffer<float> input,
    ReadWriteBuffer<float> output,
    int width,
    int height,
    float sigma,
    int radius,
    int vertical) : IComputeShader
{
    private readonly ReadWriteBuffer<float> input = input;
    private readonly ReadWriteBuffer<float> output = output;
    private readonly int width = width;
    private readonly int height = height;
    private readonly float sigma = sigma;
    private readonly int radius = radius;
    private readonly int vertical = vertical;

    public void Execute()
    {
        var x = ThreadIds.X;
        var y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        var factor = -0.5f / (sigma * sigma);
        var sum = 0f;
        var weighted = 0f;
        for (var offset = -radius; offset <= radius; offset++)
        {
            var weight = Hlsl.Exp(offset * offset * factor);
            var sampleX = vertical == 0 ? MangaScreentoneShaderMath.Reflect(x + offset, width) : x;
            var sampleY = vertical == 0 ? y : MangaScreentoneShaderMath.Reflect(y + offset, height);
            weighted += weight * input[sampleY * width + sampleX];
            sum += weight;
        }
        output[y * width + x] = weighted / sum;
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct BoxBlurShader(
    ReadWriteBuffer<float> input,
    ReadWriteBuffer<float> output,
    int width,
    int height,
    int radius,
    int vertical) : IComputeShader
{
    private readonly ReadWriteBuffer<float> input = input;
    private readonly ReadWriteBuffer<float> output = output;
    private readonly int width = width;
    private readonly int height = height;
    private readonly int radius = radius;
    private readonly int vertical = vertical;

    public void Execute()
    {
        var x = ThreadIds.X;
        var y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        var sum = 0f;
        for (var offset = -radius; offset <= radius; offset++)
        {
            var sampleX = vertical == 0 ? MangaScreentoneShaderMath.Reflect(x + offset, width) : x;
            var sampleY = vertical == 0 ? y : MangaScreentoneShaderMath.Reflect(y + offset, height);
            sum += input[sampleY * width + sampleX];
        }
        output[y * width + x] = sum / (2 * radius + 1);
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct MaskedBlurHorizontalShader(
    ReadWriteBuffer<float> gray,
    ReadWriteBuffer<uint> ink,
    ReadWriteBuffer<float> numerator,
    ReadWriteBuffer<float> denominator,
    int width,
    int height,
    float sigma,
    int radius) : IComputeShader
{
    private readonly ReadWriteBuffer<float> gray = gray;
    private readonly ReadWriteBuffer<uint> ink = ink;
    private readonly ReadWriteBuffer<float> numerator = numerator;
    private readonly ReadWriteBuffer<float> denominator = denominator;
    private readonly int width = width;
    private readonly int height = height;
    private readonly float sigma = sigma;
    private readonly int radius = radius;

    public void Execute()
    {
        var x = ThreadIds.X;
        var y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        var factor = -0.5f / (sigma * sigma);
        var sum = 0f;
        var weightedGray = 0f;
        var weightedMask = 0f;
        for (var offset = -radius; offset <= radius; offset++)
        {
            var weight = Hlsl.Exp(offset * offset * factor);
            var index = y * width + MangaScreentoneShaderMath.Reflect(x + offset, width);
            var open = ink[index] == 0u ? 1f : 0f;
            weightedGray += weight * gray[index] * open;
            weightedMask += weight * open;
            sum += weight;
        }
        numerator[y * width + x] = weightedGray / sum;
        denominator[y * width + x] = weightedMask / sum;
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct MaskedBlurVerticalShader(
    ReadWriteBuffer<float> numerator,
    ReadWriteBuffer<float> denominator,
    ReadWriteBuffer<float> output,
    int width,
    int height,
    float sigma,
    int radius,
    float minimumWeight) : IComputeShader
{
    private readonly ReadWriteBuffer<float> numerator = numerator;
    private readonly ReadWriteBuffer<float> denominator = denominator;
    private readonly ReadWriteBuffer<float> output = output;
    private readonly int width = width;
    private readonly int height = height;
    private readonly float sigma = sigma;
    private readonly int radius = radius;
    private readonly float minimumWeight = minimumWeight;

    public void Execute()
    {
        var x = ThreadIds.X;
        var y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        var factor = -0.5f / (sigma * sigma);
        var sum = 0f;
        var weightedNumerator = 0f;
        var weightedDenominator = 0f;
        for (var offset = -radius; offset <= radius; offset++)
        {
            var weight = Hlsl.Exp(offset * offset * factor);
            var index = MangaScreentoneShaderMath.Reflect(y + offset, height) * width + x;
            weightedNumerator += weight * numerator[index];
            weightedDenominator += weight * denominator[index];
            sum += weight;
        }
        output[y * width + x] = (weightedNumerator / sum) / Hlsl.Max(weightedDenominator / sum, minimumWeight);
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct BilateralShader(
    ReadWriteBuffer<float> input,
    ReadWriteBuffer<float> output,
    int width,
    int height,
    int radius,
    float spaceCoefficient,
    float colorCoefficient) : IComputeShader
{
    private readonly ReadWriteBuffer<float> input = input;
    private readonly ReadWriteBuffer<float> output = output;
    private readonly int width = width;
    private readonly int height = height;
    private readonly int radius = radius;
    private readonly float spaceCoefficient = spaceCoefficient;
    private readonly float colorCoefficient = colorCoefficient;

    public void Execute()
    {
        var x = ThreadIds.X;
        var y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        var center = input[y * width + x];
        var radiusSquared = radius * radius;
        var sum = 0f;
        var weighted = 0f;
        for (var dy = -radius; dy <= radius; dy++)
        {
            var row = MangaScreentoneShaderMath.Reflect(y + dy, height) * width;
            for (var dx = -radius; dx <= radius; dx++)
            {
                var distance = dx * dx + dy * dy;
                if (distance > radiusSquared)
                    continue;
                var value = input[row + MangaScreentoneShaderMath.Reflect(x + dx, width)];
                var difference = value - center;
                var weight = Hlsl.Exp(distance * spaceCoefficient + difference * difference * colorCoefficient);
                weighted += weight * value;
                sum += weight;
            }
        }
        output[y * width + x] = weighted / sum;
    }
}
