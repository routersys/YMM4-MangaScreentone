// Stryker disable all
using ComputeWeave;

namespace MangaScreentone;

internal static class MangaScreentoneShaderMath
{
    public static int Reflect(int index, int length)
    {
        if (index >= 0 && index < length)
            return index;
        if (length <= 1)
            return 0;
        var period = 2 * (length - 1);
        var wrapped = index % period;
        if (wrapped < 0)
            wrapped += period;
        return wrapped < length ? wrapped : period - wrapped;
    }

    public static int Clamp(int index, int length)
        => Hlsl.Clamp(index, 0, length - 1);

    public static uint MixTexel(uint index, Float4 pixel)
    {
        var red = (uint)(Hlsl.Saturate(pixel.X) * 255f + 0.5f);
        var green = (uint)(Hlsl.Saturate(pixel.Y) * 255f + 0.5f);
        var blue = (uint)(Hlsl.Saturate(pixel.Z) * 255f + 0.5f);
        var alpha = (uint)(Hlsl.Saturate(pixel.W) * 255f + 0.5f);
        var packed = red | (green << 8) | (blue << 16) | (alpha << 24);
        var mixed = (index * 0x9E3779B9u) ^ (packed * 0x85EBCA6Bu);
        mixed ^= mixed >> 16;
        mixed *= 0x85EBCA6Bu;
        mixed ^= mixed >> 13;
        return mixed;
    }

    public static float Hash01(uint value)
    {
        value ^= value >> 16;
        value *= 0x7FEB352Du;
        value ^= value >> 15;
        value *= 0x846CA68Bu;
        value ^= value >> 16;
        return value * 2.3283064e-10f;
    }

    public static float LatticeGaussian(int x, int y, uint seed, uint salt)
    {
        var key = ((uint)x * 0x9E3779B9u) ^ ((uint)y * 0x85EBCA6Bu) ^ (seed * 0xC2B2AE35u) ^ salt;
        var sum = Hash01(key) + Hash01(key ^ 0x27D4EB2Fu) + Hash01(key ^ 0x165667B1u) + Hash01(key ^ 0x1B873593u);
        return (sum - 2f) * 1.7320508f;
    }

    public static float CubicWeight(int tap, float t)
    {
        const float a = -0.75f;
        if (tap == 0)
        {
            var s = t + 1f;
            return ((a * s - 5f * a) * s + 8f * a) * s - 4f * a;
        }
        if (tap == 1)
            return ((a + 2f) * t - (a + 3f)) * t * t + 1f;
        if (tap == 2)
        {
            var u = 1f - t;
            return ((a + 2f) * u - (a + 3f)) * u * u + 1f;
        }
        var s0 = t + 1f;
        var w0 = ((a * s0 - 5f * a) * s0 + 8f * a) * s0 - 4f * a;
        var w1 = ((a + 2f) * t - (a + 3f)) * t * t + 1f;
        var v = 1f - t;
        var w2 = ((a + 2f) * v - (a + 3f)) * v * v + 1f;
        return 1f - w0 - w1 - w2;
    }

    public static float CubicNoise(int x, int y, int width, int height, int cell, uint seed, uint salt)
    {
        var latticeWidth = Hlsl.Max(width / cell, 2);
        var latticeHeight = Hlsl.Max(height / cell, 2);
        var sourceX = (x + 0.5f) * latticeWidth / width - 0.5f;
        var sourceY = (y + 0.5f) * latticeHeight / height - 0.5f;
        var baseX = (int)Hlsl.Floor(sourceX);
        var baseY = (int)Hlsl.Floor(sourceY);
        var fractionX = sourceX - baseX;
        var fractionY = sourceY - baseY;
        var value = 0f;
        for (var row = 0; row < 4; row++)
        {
            var wy = CubicWeight(row, fractionY);
            var lattice = Hlsl.Clamp(baseY + row - 1, 0, latticeHeight - 1);
            var line = 0f;
            for (var column = 0; column < 4; column++)
            {
                var wx = CubicWeight(column, fractionX);
                var latticeX = Hlsl.Clamp(baseX + column - 1, 0, latticeWidth - 1);
                line += wx * LatticeGaussian(latticeX, lattice, seed, salt);
            }
            value += wy * line;
        }
        return value;
    }
}
