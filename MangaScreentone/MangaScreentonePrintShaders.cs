// Stryker disable all
using ComputeWeave;

namespace MangaScreentone;

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct StrokeClassShader(
    ReadWriteBuffer<uint> ink,
    ReadWriteBuffer<uint> solid,
    ReadWriteBuffer<uint> classes,
    int width,
    int height) : IComputeShader
{
    private readonly ReadWriteBuffer<uint> ink = ink;
    private readonly ReadWriteBuffer<uint> solid = solid;
    private readonly ReadWriteBuffer<uint> classes = classes;
    private readonly int width = width;
    private readonly int height = height;

    public void Execute()
    {
        var x = ThreadIds.X;
        var y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        var index = y * width + x;
        var stroke = ink[index] != 0u && solid[index] == 0u;
        classes[index] = stroke ? 0u : MangaScreentoneSettings.InactiveClass;
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct CutKeepShader(
    ReadWriteBuffer<uint> classes,
    ReadWriteBuffer<uint> labels,
    ReadWriteBuffer<uint> minX,
    ReadWriteBuffer<uint> minY,
    ReadWriteBuffer<uint> maxX,
    ReadWriteBuffer<uint> maxY,
    ReadWriteBuffer<uint> keep,
    int width,
    int height,
    float minLength) : IComputeShader
{
    private readonly ReadWriteBuffer<uint> classes = classes;
    private readonly ReadWriteBuffer<uint> labels = labels;
    private readonly ReadWriteBuffer<uint> minX = minX;
    private readonly ReadWriteBuffer<uint> minY = minY;
    private readonly ReadWriteBuffer<uint> maxX = maxX;
    private readonly ReadWriteBuffer<uint> maxY = maxY;
    private readonly ReadWriteBuffer<uint> keep = keep;
    private readonly int width = width;
    private readonly int height = height;
    private readonly float minLength = minLength;

    public void Execute()
    {
        var x = ThreadIds.X;
        var y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        var index = y * width + x;
        var kept = false;
        if (classes[index] != MangaScreentoneSettings.InactiveClass)
        {
            var root = (int)labels[index];
            var componentWidth = (float)(maxX[root] - minX[root] + 1u);
            var componentHeight = (float)(maxY[root] - minY[root] + 1u);
            kept = Hlsl.Max(componentWidth, componentHeight) >= minLength;
        }
        keep[index] = kept ? 1u : 0u;
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct ErodeShader(
    ReadWriteBuffer<uint> source,
    ReadWriteBuffer<uint> destination,
    int width,
    int height,
    int size,
    uint target) : IComputeShader
{
    private readonly ReadWriteBuffer<uint> source = source;
    private readonly ReadWriteBuffer<uint> destination = destination;
    private readonly int width = width;
    private readonly int height = height;
    private readonly int size = size;
    private readonly uint target = target;

    public void Execute()
    {
        var x = ThreadIds.X;
        var y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        var anchor = size / 2;
        var eroded = 1u;
        for (var row = 0; row < size && eroded != 0u; row++)
        {
            var sampleY = y + row - anchor;
            if (sampleY < 0 || sampleY >= height)
                continue;
            for (var column = 0; column < size; column++)
            {
                var sampleX = x + column - anchor;
                if (sampleX < 0 || sampleX >= width)
                    continue;
                if (source[sampleY * width + sampleX] != target)
                {
                    eroded = 0u;
                    break;
                }
            }
        }
        destination[y * width + x] = eroded;
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct CoverShader(
    ReadWriteBuffer<uint> level,
    ReadWriteBuffer<float> smooth,
    ReadWriteBuffer<float> accent,
    ReadWriteBuffer<float> cover,
    int width,
    int height,
    float white,
    float black,
    float lightTone,
    float darkTone,
    float gradation) : IComputeShader
{
    private readonly ReadWriteBuffer<uint> level = level;
    private readonly ReadWriteBuffer<float> smooth = smooth;
    private readonly ReadWriteBuffer<float> accent = accent;
    private readonly ReadWriteBuffer<float> cover = cover;
    private readonly int width = width;
    private readonly int height = height;
    private readonly float white = white;
    private readonly float black = black;
    private readonly float lightTone = lightTone;
    private readonly float darkTone = darkTone;
    private readonly float gradation = gradation;

    public void Execute()
    {
        var x = ThreadIds.X;
        var y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        var index = y * width + x;
        var tone = level[index];
        var flat = 0f;
        if (tone == 1u)
            flat = lightTone;
        else if (tone == 2u)
            flat = darkTone;
        else if (tone == 3u)
            flat = 1f;

        var result = flat;
        if (gradation > 0f)
        {
            var mix = accent[index] * gradation;
            var ramp = Hlsl.Saturate((white - smooth[index]) / (white - black));
            result = flat * (1f - mix) + ramp * mix;
        }
        cover[index] = result;
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct PrintShader(
    ReadWriteBuffer<float> cover,
    ReadWriteBuffer<uint> page,
    int width,
    int height,
    float pitch) : IComputeShader
{
    private readonly ReadWriteBuffer<float> cover = cover;
    private readonly ReadWriteBuffer<uint> page = page;
    private readonly int width = width;
    private readonly int height = height;
    private readonly float pitch = pitch;

    public void Execute()
    {
        var x = ThreadIds.X;
        var y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        var cosine = Hlsl.Cos(MangaScreentoneSettings.PrintAngleRadians);
        var sine = Hlsl.Sin(MangaScreentoneSettings.PrintAngleRadians);
        var scale = 1f / (pitch * MangaScreentoneSettings.PrintSupersample);
        var dots = 0;
        for (var subY = 0; subY < MangaScreentoneSettings.PrintSupersample; subY++)
        {
            for (var subX = 0; subX < MangaScreentoneSettings.PrintSupersample; subX++)
            {
                var sampleX = x * MangaScreentoneSettings.PrintSupersample + subX;
                var sampleY = y * MangaScreentoneSettings.PrintSupersample + subY;
                var sourceX = (sampleX + 0.5f) / MangaScreentoneSettings.PrintSupersample - 0.5f;
                var sourceY = (sampleY + 0.5f) / MangaScreentoneSettings.PrintSupersample - 0.5f;
                var left = (int)Hlsl.Floor(sourceX);
                var top = (int)Hlsl.Floor(sourceY);
                var fractionX = sourceX - left;
                var fractionY = sourceY - top;
                var x0 = MangaScreentoneShaderMath.Clamp(left, width);
                var x1 = MangaScreentoneShaderMath.Clamp(left + 1, width);
                var y0 = MangaScreentoneShaderMath.Clamp(top, height) * width;
                var y1 = MangaScreentoneShaderMath.Clamp(top + 1, height) * width;
                var upper = Hlsl.Lerp(cover[y0 + x0], cover[y0 + x1], fractionX);
                var lower = Hlsl.Lerp(cover[y1 + x0], cover[y1 + x1], fractionX);
                var coverage = Hlsl.Lerp(upper, lower, fractionY);

                var u = (sampleX * cosine + sampleY * sine) * scale;
                var v = (-sampleX * sine + sampleY * cosine) * scale;
                var spot = 1f - (Hlsl.Cos(MangaScreentoneShaderMath.TwoPi * Hlsl.Frac(u)) + Hlsl.Cos(MangaScreentoneShaderMath.TwoPi * Hlsl.Frac(v)) + 2f) * 0.25f;
                if (spot < coverage)
                    dots++;
            }
        }
        page[y * width + x] = dots * 2 > MangaScreentoneSettings.PrintSupersample * MangaScreentoneSettings.PrintSupersample ? 1u : 0u;
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct PageShader(
    ReadWriteBuffer<uint> page,
    ReadWriteBuffer<uint> ink,
    ReadWriteBuffer<uint> beta,
    ReadWriteBuffer<uint> cut,
    int width,
    int height,
    int whiteLines) : IComputeShader
{
    private readonly ReadWriteBuffer<uint> page = page;
    private readonly ReadWriteBuffer<uint> ink = ink;
    private readonly ReadWriteBuffer<uint> beta = beta;
    private readonly ReadWriteBuffer<uint> cut = cut;
    private readonly int width = width;
    private readonly int height = height;
    private readonly int whiteLines = whiteLines;

    public void Execute()
    {
        var x = ThreadIds.X;
        var y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        var index = y * width + x;
        var printed = page[index] != 0u || ink[index] != 0u;
        if (whiteLines != 0)
        {
            var inside = beta[index] != 0u;
            printed = (printed || inside) && !(inside && cut[index] != 0u);
        }
        page[index] = printed ? 1u : 0u;
    }
}
