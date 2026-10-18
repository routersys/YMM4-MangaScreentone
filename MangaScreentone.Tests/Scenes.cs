namespace MangaScreentone.Tests;

internal static class Scenes
{
    public static Bgra Pixel(int x, int y, int width, int height, double shift = 0d)
    {
        var unit = Math.Min(width, height);
        var stroke = Math.Max(unit * 0.012, 1.2);
        var u = (x + 0.5) / width;
        var v = (y + 0.5) / height;
        var luminance = 0.97 - 0.12 * v;
        var tint = (Red: 0.94, Green: 0.97, Blue: 1.0);
        if (u is >= 0.06 and <= 0.30 && v is >= 0.18 and <= 0.92)
        {
            luminance = 0.33 + shift;
            tint = (1.0, 1.0, 1.0);
            if (Math.Abs(u - 0.14) * width < stroke || Math.Abs(v - 0.55) * height < stroke * 0.8)
                luminance = 0.15;
        }

        var sphereX = (u - 0.52) * width;
        var sphereY = (v - 0.55) * height;
        var radius = unit * 0.24;
        var distance = Math.Sqrt(sphereX * sphereX + sphereY * sphereY);
        if (distance < radius)
        {
            var lightX = (sphereX + radius * 0.35) / radius;
            var lightY = (sphereY + radius * 0.35) / radius;
            luminance = 0.12 + 0.80 * Math.Clamp(1d - Math.Sqrt(lightX * lightX + lightY * lightY) * 0.62, 0d, 1d);
            tint = (1.0, 0.92, 0.85);
        }
        else if (Math.Abs(distance - radius) < stroke)
        {
            luminance = 0.08;
            tint = (1.0, 1.0, 1.0);
        }

        if (u is >= 0.76 and <= 0.96 && v is >= 0.14 and <= 0.44)
            luminance = 0.46;
        if (u is >= 0.76 and <= 0.96 && v is >= 0.54 and <= 0.90)
            luminance = 0.33;

        static byte Byte(double value) => (byte)Math.Round(Math.Clamp(value, 0d, 1d) * 255d);
        return new Bgra(Byte(luminance * tint.Blue), Byte(luminance * tint.Green), Byte(luminance * tint.Red), u < 0.02 ? (byte)0 : byte.MaxValue);
    }

    public static int[] Packed(int width, int height, double shift = 0d)
    {
        var pixels = new int[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var (blue, green, red, alpha) = Pixel(x, y, width, height, shift).Premultiplied();
                pixels[y * width + x] = blue | (green << 8) | (red << 16) | (alpha << 24);
            }
        }

        return pixels;
    }
}
