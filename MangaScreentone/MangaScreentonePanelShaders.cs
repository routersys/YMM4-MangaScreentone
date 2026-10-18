// Stryker disable all
using ComputeWeave;

namespace MangaScreentone;

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct PanelShader(
    ReadWriteBuffer<uint> page,
    ReadWriteTexture2D<Bgra32, Float4> output,
    int width,
    int height,
    int canvasWidth,
    int canvasHeight,
    int margin,
    int border,
    int inkRadius,
    float inkSigma,
    float inkStrength,
    int mottleCoarse,
    int mottleFine,
    float paperRed,
    float paperGreen,
    float paperBlue,
    float fibre,
    int transparent,
    float inkRed,
    float inkGreen,
    float inkBlue) : IComputeShader
{
    private readonly ReadWriteBuffer<uint> page = page;
    private readonly ReadWriteTexture2D<Bgra32, Float4> output = output;
    private readonly int width = width;
    private readonly int height = height;
    private readonly int canvasWidth = canvasWidth;
    private readonly int canvasHeight = canvasHeight;
    private readonly int margin = margin;
    private readonly int border = border;
    private readonly int inkRadius = inkRadius;
    private readonly float inkSigma = inkSigma;
    private readonly float inkStrength = inkStrength;
    private readonly int mottleCoarse = mottleCoarse;
    private readonly int mottleFine = mottleFine;
    private readonly float paperRed = paperRed;
    private readonly float paperGreen = paperGreen;
    private readonly float paperBlue = paperBlue;
    private readonly float fibre = fibre;
    private readonly int transparent = transparent;
    private readonly float inkRed = inkRed;
    private readonly float inkGreen = inkGreen;
    private readonly float inkBlue = inkBlue;

    private float PageValue(int canvasX, int canvasY)
    {
        var x = canvasX - margin;
        var y = canvasY - margin;
        if (x < 0 || y < 0 || x >= width || y >= height)
            return 0f;
        if (border > 0 && (x < border || y < border || x >= width - border || y >= height - border))
            return 1f;
        return page[y * width + x] != 0u ? 1f : 0f;
    }

    public void Execute()
    {
        var x = ThreadIds.X;
        var y = ThreadIds.Y;
        if (x >= canvasWidth || y >= canvasHeight)
            return;

        var coverage = PageValue(x, y);
        if (inkStrength > 0f)
        {
            if (inkRadius > 0)
            {
                var factor = -0.5f / (inkSigma * inkSigma);
                var sum = 0f;
                var weighted = 0f;
                for (var dy = -inkRadius; dy <= inkRadius; dy++)
                {
                    var sampleY = MangaScreentoneShaderMath.Reflect(y + dy, canvasHeight);
                    for (var dx = -inkRadius; dx <= inkRadius; dx++)
                    {
                        var weight = Hlsl.Exp((dx * dx + dy * dy) * factor);
                        weighted += weight * PageValue(MangaScreentoneShaderMath.Reflect(x + dx, canvasWidth), sampleY);
                        sum += weight;
                    }
                }
                coverage = weighted / sum;
            }
            coverage = Hlsl.Saturate((coverage - MangaScreentoneSettings.InkSpreadFloor) / MangaScreentoneSettings.InkSpreadRange);
            if (coverage > 0f)
            {
                var mottle = MangaScreentoneShaderMath.CubicNoise(x, y, canvasWidth, canvasHeight, mottleCoarse, MangaScreentoneSettings.NoiseSeedInk, 0x51ED270Bu)
                    + MangaScreentoneSettings.InkMottleFineWeight * MangaScreentoneShaderMath.CubicNoise(x, y, canvasWidth, canvasHeight, mottleFine, MangaScreentoneSettings.NoiseSeedInk, 0x2C1B3C6Du);
                coverage *= 1f - MangaScreentoneSettings.InkMottleDepth * inkStrength * (0.5f + 0.5f * Hlsl.Tanh(mottle));
            }
        }

        var red = inkRed * inkStrength;
        var green = inkGreen * inkStrength;
        var blue = inkBlue * inkStrength;
        if (transparent != 0)
        {
            output[ThreadIds.XY] = new Float4(red * coverage, green * coverage, blue * coverage, coverage);
            return;
        }

        var grain = 0f;
        if (fibre > 0f)
        {
            grain = fibre * (MangaScreentoneShaderMath.CubicNoise(x, y, canvasWidth, canvasHeight, 3, MangaScreentoneSettings.NoiseSeedFibre, 0x297A2D39u)
                + MangaScreentoneSettings.FibreFineWeight * MangaScreentoneShaderMath.CubicNoise(x, y, canvasWidth, canvasHeight, 1, MangaScreentoneSettings.NoiseSeedFibre, 0x68E31DA4u));
        }
        var open = 1f - coverage;
        output[ThreadIds.XY] = new Float4(
            Hlsl.Saturate((paperRed + grain) * open + red * coverage),
            Hlsl.Saturate((paperGreen + grain) * open + green * coverage),
            Hlsl.Saturate((paperBlue + grain) * open + blue * coverage),
            1f);
    }
}
