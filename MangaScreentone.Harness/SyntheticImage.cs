namespace MangaScreentone.Harness;

internal static class SyntheticImage
{
    const float TransparentStrip = 0.02f;

    public static byte[] Create(int width, int height)
    {
        var pixels = new byte[width * height * HarnessImage.BytesPerPixel];
        var unit = MathF.Min(width, height);
        var strokeWidth = MathF.Max(unit * 0.005f, 1f);
        var speckleCell = MathF.Max(unit * 0.03f, 4f);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var u = (x + 0.5f) / width;
                var v = (y + 0.5f) / height;
                var luminance = 0.97f - 0.12f * v;
                var tint = (0.94f, 0.97f, 1.0f);

                if (u >= 0.06f && u <= 0.30f && v >= 0.18f && v <= 0.92f)
                {
                    luminance = 0.33f;
                    tint = (1f, 1f, 1f);
                    var seam = MathF.Abs(u - 0.14f) * width < strokeWidth || MathF.Abs(u - 0.22f + (v - 0.5f) * 0.08f) * width < strokeWidth * 0.8f || MathF.Abs(v - 0.55f) * height < strokeWidth * 0.7f;
                    if (seam)
                        luminance = 0.15f;
                }

                var sphereX = (u - 0.52f) * width;
                var sphereY = (v - 0.55f) * height;
                var sphereRadius = unit * 0.24f;
                var sphereDistance = MathF.Sqrt(sphereX * sphereX + sphereY * sphereY);
                if (sphereDistance < sphereRadius)
                {
                    var lightX = (sphereX + sphereRadius * 0.35f) / sphereRadius;
                    var lightY = (sphereY + sphereRadius * 0.35f) / sphereRadius;
                    var shade = Math.Clamp(1f - MathF.Sqrt(lightX * lightX + lightY * lightY) * 0.62f, 0f, 1f);
                    luminance = 0.12f + 0.80f * shade;
                    tint = (1f, 0.92f, 0.85f);
                }
                else if (MathF.Abs(sphereDistance - sphereRadius) < strokeWidth * 1.4f)
                {
                    luminance = 0.08f;
                    tint = (1f, 1f, 1f);
                }

                if (u >= 0.76f && u <= 0.96f && v >= 0.14f && v <= 0.44f)
                {
                    luminance = 0.46f;
                    tint = (1f, 1f, 1f);
                }

                if (u >= 0.76f && u <= 0.96f && v >= 0.54f && v <= 0.90f)
                {
                    luminance = 0.33f;
                    tint = (0.9f, 0.95f, 1f);
                }

                var faceX = (u - 0.52f) * width;
                var faceY = (v - 0.18f) * height;
                var faceRadius = unit * 0.11f;
                var faceDistance = MathF.Sqrt(faceX * faceX + faceY * faceY);
                if (faceDistance < faceRadius)
                {
                    luminance = 0.80f;
                    tint = (1f, 0.86f, 0.76f);
                }
                else if (MathF.Abs(faceDistance - faceRadius) < strokeWidth)
                {
                    luminance = 0.06f;
                    tint = (1f, 1f, 1f);
                }

                var cellX = (int)MathF.Floor(x / speckleCell);
                var cellY = (int)MathF.Floor(y / speckleCell);
                var key = (uint)(cellX * 73856093) ^ (uint)(cellY * 19349663);
                key ^= key >> 13;
                key *= 0x5BD1E995u;
                key ^= key >> 15;
                if ((key & 15u) == 0u && u > 0.32f && u < 0.74f && v > 0.80f)
                {
                    var localX = x - (cellX + 0.5f) * speckleCell;
                    var localY = y - (cellY + 0.5f) * speckleCell;
                    if (localX * localX + localY * localY < strokeWidth * strokeWidth * 2.5f)
                    {
                        luminance = 0.15f;
                        tint = (1f, 1f, 1f);
                    }
                }

                var alpha = u < TransparentStrip ? 0f : 1f;
                var offset = (y * width + x) * HarnessImage.BytesPerPixel;
                pixels[offset] = ToByte(luminance * tint.Item3 * alpha);
                pixels[offset + 1] = ToByte(luminance * tint.Item2 * alpha);
                pixels[offset + 2] = ToByte(luminance * tint.Item1 * alpha);
                pixels[offset + 3] = ToByte(alpha);
            }
        }

        return pixels;
    }

    static byte ToByte(float premultiplied) => (byte)MathF.Round(Math.Clamp(premultiplied, 0f, 1f) * byte.MaxValue);
}
