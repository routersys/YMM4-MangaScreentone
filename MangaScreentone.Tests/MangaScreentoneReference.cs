namespace MangaScreentone.Tests;

internal static class MangaScreentoneReference
{
    public const uint Inactive = MangaScreentoneSettings.InactiveClass;

    public sealed record Components(int[] Labels, int[] Area, int[] MinX, int[] MinY, int[] MaxX, int[] MaxY);

    public sealed record Analysis(
        double[] Gray,
        bool[] Solid,
        bool[] Ink,
        double[] Value,
        uint[] Level,
        double[] Smooth,
        double[] Accent);

    public static int Reflect(int index, int length)
    {
        if (length <= 1)
            return 0;
        var period = 2 * (length - 1);
        var wrapped = ((index % period) + period) % period;
        return wrapped < length ? wrapped : period - wrapped;
    }

    public static double[] Gray(ReadOnlySpan<int> source, int width, int height, MangaScreentoneSettings.Derived derived)
    {
        var gray = new double[width * height];
        var range = Math.Max(derived.LevelWhite - derived.LevelBlack, MangaScreentoneSettings.MinimumLevelGap);
        for (var index = 0; index < gray.Length; index++)
        {
            var pixel = (uint)source[index];
            var blue = (pixel & 255u) / 255d;
            var green = ((pixel >> 8) & 255u) / 255d;
            var red = ((pixel >> 16) & 255u) / 255d;
            var clear = 1d - (pixel >> 24) / 255d;
            var luminance = 0.299d * (red + clear) + 0.587d * (green + clear) + 0.114d * (blue + clear);
            var leveled = Math.Clamp((luminance - derived.LevelBlack) / range, 0d, 1d);
            gray[index] = Math.Pow(leveled, 1d / derived.Gamma);
        }

        return gray;
    }

    public static double[] Gaussian(double[] source, int width, int height, double sigma, int radius)
    {
        var horizontal = new double[source.Length];
        var result = new double[source.Length];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                double sum = 0, weighted = 0;
                for (var offset = -radius; offset <= radius; offset++)
                {
                    var weight = Math.Exp(-0.5d * offset * offset / (sigma * sigma));
                    weighted += weight * source[y * width + Reflect(x + offset, width)];
                    sum += weight;
                }
                horizontal[y * width + x] = weighted / sum;
            }
        }

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                double sum = 0, weighted = 0;
                for (var offset = -radius; offset <= radius; offset++)
                {
                    var weight = Math.Exp(-0.5d * offset * offset / (sigma * sigma));
                    weighted += weight * horizontal[Reflect(y + offset, height) * width + x];
                    sum += weight;
                }
                result[y * width + x] = weighted / sum;
            }
        }

        return result;
    }

    public static double[] Box(double[] source, int width, int height, int size)
    {
        var radius = size / 2;
        var result = new double[source.Length];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                double sum = 0;
                for (var dy = -radius; dy <= radius; dy++)
                {
                    for (var dx = -radius; dx <= radius; dx++)
                        sum += source[Reflect(y + dy, height) * width + Reflect(x + dx, width)];
                }
                result[y * width + x] = sum / ((double)size * size);
            }
        }

        return result;
    }

    public static double[] MaskedValue(double[] gray, bool[] ink, int width, int height, double sigma, int radius)
    {
        var masked = new double[gray.Length];
        var open = new double[gray.Length];
        for (var index = 0; index < gray.Length; index++)
        {
            open[index] = ink[index] ? 0d : 1d;
            masked[index] = gray[index] * open[index];
        }

        var numerator = Gaussian(masked, width, height, sigma, radius);
        var denominator = Gaussian(open, width, height, sigma, radius);
        var result = new double[gray.Length];
        for (var index = 0; index < result.Length; index++)
            result[index] = numerator[index] / Math.Max(denominator[index], MangaScreentoneSettings.ValueMinWeight);
        return result;
    }

    public static double[] Bilateral(double[] source, int width, int height, int radius, double spaceCoefficient, double colorCoefficient)
    {
        var result = new double[source.Length];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var center = source[y * width + x];
                double sum = 0, weighted = 0;
                for (var dy = -radius; dy <= radius; dy++)
                {
                    for (var dx = -radius; dx <= radius; dx++)
                    {
                        var distance = dx * dx + dy * dy;
                        if (distance > radius * radius)
                            continue;
                        var value = source[Reflect(y + dy, height) * width + Reflect(x + dx, width)];
                        var weight = Math.Exp(distance * spaceCoefficient + (value - center) * (value - center) * colorCoefficient);
                        weighted += weight * value;
                        sum += weight;
                    }
                }
                result[y * width + x] = weighted / sum;
            }
        }

        return result;
    }

    public static Components Label(uint[] classes, int width, int height, bool diagonal)
    {
        var length = classes.Length;
        var parent = new int[length];
        for (var index = 0; index < length; index++)
            parent[index] = index;

        int Find(int node)
        {
            while (parent[node] != node)
            {
                parent[node] = parent[parent[node]];
                node = parent[node];
            }
            return node;
        }

        void Union(int first, int second)
        {
            var a = Find(first);
            var b = Find(second);
            if (a == b)
                return;
            if (a < b)
                parent[b] = a;
            else
                parent[a] = b;
        }

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = y * width + x;
                if (classes[index] == Inactive)
                    continue;
                if (x > 0 && classes[index - 1] == classes[index])
                    Union(index, index - 1);
                if (y > 0 && classes[index - width] == classes[index])
                    Union(index, index - width);
                if (diagonal && y > 0 && x > 0 && classes[index - width - 1] == classes[index])
                    Union(index, index - width - 1);
                if (diagonal && y > 0 && x + 1 < width && classes[index - width + 1] == classes[index])
                    Union(index, index - width + 1);
            }
        }

        var labels = new int[length];
        var area = new int[length];
        var minX = new int[length];
        var minY = new int[length];
        var maxX = new int[length];
        var maxY = new int[length];
        Array.Fill(minX, int.MaxValue);
        Array.Fill(minY, int.MaxValue);
        Array.Fill(maxX, -1);
        Array.Fill(maxY, -1);
        for (var index = 0; index < length; index++)
        {
            if (classes[index] == Inactive)
            {
                labels[index] = -1;
                continue;
            }

            var root = Find(index);
            labels[index] = root;
            area[root]++;
            var x = index % width;
            var y = index / width;
            minX[root] = Math.Min(minX[root], x);
            minY[root] = Math.Min(minY[root], y);
            maxX[root] = Math.Max(maxX[root], x);
            maxY[root] = Math.Max(maxY[root], y);
        }

        return new Components(labels, area, minX, minY, maxX, maxY);
    }

    public static uint[] ClassesOf(bool[] mask)
        => mask.Select(value => value ? 0u : Inactive).ToArray();

    public static bool[] InkLayer(double[] gray, bool[] solid, int width, int height, MangaScreentoneSettings.Derived derived)
    {
        var line = Gaussian(gray, width, height, derived.LineSigma, derived.LineRadius);
        var mean = Box(line, width, height, derived.LineMeanSize);
        var lineMask = new bool[gray.Length];
        for (var index = 0; index < lineMask.Length; index++)
            lineMask[index] = line[index] < mean[index] - MangaScreentoneSettings.LineContrast && line[index] < MangaScreentoneSettings.LineCeiling;

        var first = Label(ClassesOf(lineMask), width, height, true);
        var keep = new bool[gray.Length];
        for (var index = 0; index < keep.Length; index++)
        {
            var kept = solid[index];
            if (!kept && lineMask[index])
            {
                var root = first.Labels[index];
                double area = first.Area[root];
                double componentWidth = first.MaxX[root] - first.MinX[root] + 1;
                double componentHeight = first.MaxY[root] - first.MinY[root] + 1;
                var stroke = Math.Max(componentWidth, componentHeight) >= derived.StrokeMinLength
                    && area >= derived.StrokeMinArea
                    && area < MangaScreentoneSettings.StrokeFillRatio * componentWidth * componentHeight + derived.StrokeFillBias;
                kept = stroke || area >= derived.StrokeLargeArea;
            }
            keep[index] = kept;
        }

        var second = Label(ClassesOf(keep), width, height, true);
        var ink = new bool[gray.Length];
        for (var index = 0; index < ink.Length; index++)
            ink[index] = keep[index] && second.Area[second.Labels[index]] >= derived.InkMinArea;
        return ink;
    }

    public static uint[] Digitize(double[] value, double white, double split, double black)
    {
        var level = new uint[value.Length];
        for (var index = 0; index < level.Length; index++)
            level[index] = (value[index] <= white ? 1u : 0u) + (value[index] <= split ? 1u : 0u) + (value[index] <= black ? 1u : 0u);
        return level;
    }

    public static uint[] Median(uint[] values, int width, int height, int size, Func<int, bool>? hole = null)
    {
        var radius = size / 2;
        var result = new uint[values.Length];
        var window = new List<uint>(size * size);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                window.Clear();
                for (var dy = -radius; dy <= radius; dy++)
                {
                    for (var dx = -radius; dx <= radius; dx++)
                    {
                        var index = Math.Clamp(y + dy, 0, height - 1) * width + Math.Clamp(x + dx, 0, width - 1);
                        window.Add(hole is not null && hole(index) ? 255u : values[index]);
                    }
                }
                window.Sort();
                result[y * width + x] = window[window.Count / 2];
            }
        }

        return result;
    }

    public static uint[] TonePlan(double[] value, int width, int height, MangaScreentoneSettings.Derived derived)
    {
        var level = Median(Digitize(value, derived.White, (derived.White + derived.Black) * 0.5d, derived.Black), width, height, derived.PlanMedianSize);
        foreach (var target in new[] { 1u, 2u, 3u, 0u })
        {
            var classes = level.Select(item => item == target ? 0u : Inactive).ToArray();
            var components = Label(classes, width, height, false);
            var current = level;
            bool Hole(int index) => current[index] == target && components.Area[components.Labels[index]] < derived.PlanMinRegion;
            var fill = Median(current, width, height, derived.PlanFillMedianSize, Hole);
            var next = (uint[])current.Clone();
            for (var index = 0; index < next.Length; index++)
            {
                if (Hole(index) && fill[index] != 255u)
                    next[index] = fill[index];
            }
            level = next;
        }

        return level;
    }

    public static double[] Spread(uint[] level, double[] smooth, int width, int height)
    {
        var components = Label(level, width, height, false);
        var sum = new double[level.Length];
        var squares = new double[level.Length];
        for (var index = 0; index < level.Length; index++)
        {
            var quantized = Math.Floor(Math.Clamp(smooth[index], 0d, 1d) * 255d + 0.5d);
            var root = components.Labels[index];
            sum[root] += quantized;
            squares[root] += quantized * quantized;
        }

        var result = new double[level.Length];
        for (var index = 0; index < level.Length; index++)
        {
            var root = components.Labels[index];
            double count = Math.Max(components.Area[root], 1);
            var mean = sum[root] / count;
            var deviation = Math.Sqrt(Math.Max(squares[root] / count - mean * mean, 0d)) / 255d;
            result[index] = Math.Clamp((deviation - MangaScreentoneSettings.SpreadFloor) / MangaScreentoneSettings.SpreadRange, 0d, 1d);
        }

        return result;
    }

    public static Analysis Analyze(ReadOnlySpan<int> source, int width, int height, MangaScreentoneSettings.Derived derived)
    {
        var gray = Gray(source, width, height, derived);
        var solidBlur = Gaussian(gray, width, height, derived.SolidSigma, derived.SolidRadius);
        var solid = solidBlur.Select(value => value < MangaScreentoneSettings.SolidThreshold).ToArray();
        var ink = InkLayer(gray, solid, width, height, derived);
        var value = MaskedValue(gray, ink, width, height, derived.ValueSigma, derived.ValueRadius);
        for (var pass = 0; pass < 2; pass++)
            value = Bilateral(value, width, height, derived.BilateralRadius, derived.BilateralSpaceCoefficient, derived.BilateralColorCoefficient);
        var level = TonePlan(value, width, height, derived);
        var smooth = Gaussian(value, width, height, derived.SpreadSigma, derived.SpreadRadius);
        var accent = Gaussian(Spread(level, smooth, width, height), width, height, derived.SmoothSigma, derived.SmoothRadius);
        return new Analysis(gray, solid, ink, value, level, smooth, accent);
    }

    public static double[] Cover(Analysis analysis, MangaScreentoneSettings.Derived derived, MangaScreentoneSettings.Parameters parameters)
    {
        var cover = new double[analysis.Level.Length];
        for (var index = 0; index < cover.Length; index++)
        {
            var flat = analysis.Level[index] switch
            {
                1u => Math.Clamp(parameters.LightTone, 0f, 1f),
                2u => Math.Clamp(parameters.DarkTone, 0f, 1f),
                3u => 1d,
                _ => 0d,
            };
            var gradation = Math.Clamp(parameters.Gradation, 0f, 1f);
            if (gradation > 0d)
            {
                var mix = analysis.Accent[index] * gradation;
                var ramp = Math.Clamp((derived.White - analysis.Smooth[index]) / (derived.White - derived.Black), 0d, 1d);
                flat = flat * (1d - mix) + ramp * mix;
            }
            cover[index] = flat;
        }

        return cover;
    }

    public static bool[] Print(double[] cover, int width, int height, double pitch)
    {
        var page = new bool[cover.Length];
        var angle = Math.PI / 4d;
        var cosine = Math.Cos(angle);
        var sine = Math.Sin(angle);
        var scale = 1d / (pitch * 3d);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var dots = 0;
                for (var subY = 0; subY < 3; subY++)
                {
                    for (var subX = 0; subX < 3; subX++)
                    {
                        var sampleX = x * 3 + subX;
                        var sampleY = y * 3 + subY;
                        var sourceX = (sampleX + 0.5d) / 3d - 0.5d;
                        var sourceY = (sampleY + 0.5d) / 3d - 0.5d;
                        var left = (int)Math.Floor(sourceX);
                        var top = (int)Math.Floor(sourceY);
                        var fractionX = sourceX - left;
                        var fractionY = sourceY - top;
                        double At(int column, int row) => cover[Math.Clamp(row, 0, height - 1) * width + Math.Clamp(column, 0, width - 1)];
                        var upper = At(left, top) * (1d - fractionX) + At(left + 1, top) * fractionX;
                        var lower = At(left, top + 1) * (1d - fractionX) + At(left + 1, top + 1) * fractionX;
                        var coverage = upper * (1d - fractionY) + lower * fractionY;
                        var u = (sampleX * cosine + sampleY * sine) * scale;
                        var v = (-sampleX * sine + sampleY * cosine) * scale;
                        var spot = 1d - (Math.Cos(2d * Math.PI * u) + Math.Cos(2d * Math.PI * v) + 2d) / 4d;
                        if (spot < coverage)
                            dots++;
                    }
                }
                page[y * width + x] = dots * 2 > 9;
            }
        }

        return page;
    }

    public static bool[] Erode(bool[] source, int width, int height, int size)
    {
        var anchor = size / 2;
        var result = new bool[source.Length];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var eroded = true;
                for (var row = 0; row < size && eroded; row++)
                {
                    for (var column = 0; column < size; column++)
                    {
                        var sampleX = x + column - anchor;
                        var sampleY = y + row - anchor;
                        if (sampleX < 0 || sampleY < 0 || sampleX >= width || sampleY >= height)
                            continue;
                        if (!source[sampleY * width + sampleX])
                        {
                            eroded = false;
                            break;
                        }
                    }
                }
                result[y * width + x] = eroded;
            }
        }

        return result;
    }

    public static bool[] CutMask(Analysis analysis, int width, int height, MangaScreentoneSettings.Derived derived)
    {
        var strokes = new bool[analysis.Ink.Length];
        for (var index = 0; index < strokes.Length; index++)
            strokes[index] = analysis.Ink[index] && !analysis.Solid[index];
        var components = Label(ClassesOf(strokes), width, height, true);
        var keep = new bool[strokes.Length];
        for (var index = 0; index < keep.Length; index++)
        {
            if (!strokes[index])
                continue;
            var root = components.Labels[index];
            keep[index] = Math.Max(components.MaxX[root] - components.MinX[root] + 1, components.MaxY[root] - components.MinY[root] + 1) >= derived.CutMinLength;
        }

        return Erode(keep, width, height, derived.CutErodeSize);
    }

    public static bool[] Page(Analysis analysis, int width, int height, MangaScreentoneSettings.Derived derived, MangaScreentoneSettings.Parameters parameters)
    {
        var printed = Print(Cover(analysis, derived, parameters), width, height, derived.Pitch);
        var page = new bool[printed.Length];
        for (var index = 0; index < page.Length; index++)
            page[index] = printed[index] || analysis.Ink[index];

        if (!parameters.WhiteLines)
            return page;

        var beta = Erode(analysis.Level.Select(level => level == 3u).ToArray(), width, height, derived.BetaSize);
        var cut = CutMask(analysis, width, height, derived);
        for (var index = 0; index < page.Length; index++)
            page[index] = (page[index] || beta[index]) && !(beta[index] && cut[index]);
        return page;
    }

    static double Hash01(uint value)
    {
        value ^= value >> 16;
        value *= 0x7FEB352Du;
        value ^= value >> 15;
        value *= 0x846CA68Bu;
        value ^= value >> 16;
        return value * 2.3283064e-10d;
    }

    static double Lattice(int x, int y, uint seed, uint salt)
    {
        var key = ((uint)x * 0x9E3779B9u) ^ ((uint)y * 0x85EBCA6Bu) ^ (seed * 0xC2B2AE35u) ^ salt;
        var sum = Hash01(key) + Hash01(key ^ 0x27D4EB2Fu) + Hash01(key ^ 0x165667B1u) + Hash01(key ^ 0x1B873593u);
        return (sum - 2d) * Math.Sqrt(3d);
    }

    static double[] Cubic(double fraction)
    {
        const double a = -0.75d;
        var first = ((a * (fraction + 1d) - 5d * a) * (fraction + 1d) + 8d * a) * (fraction + 1d) - 4d * a;
        var second = ((a + 2d) * fraction - (a + 3d)) * fraction * fraction + 1d;
        var third = ((a + 2d) * (1d - fraction) - (a + 3d)) * (1d - fraction) * (1d - fraction) + 1d;
        return [first, second, third, 1d - first - second - third];
    }

    public static double Noise(int x, int y, int width, int height, int cell, uint seed, uint salt)
    {
        var latticeWidth = Math.Max(width / cell, 2);
        var latticeHeight = Math.Max(height / cell, 2);
        var sourceX = (x + 0.5d) * latticeWidth / width - 0.5d;
        var sourceY = (y + 0.5d) * latticeHeight / height - 0.5d;
        var baseX = (int)Math.Floor(sourceX);
        var baseY = (int)Math.Floor(sourceY);
        var weightsX = Cubic(sourceX - baseX);
        var weightsY = Cubic(sourceY - baseY);
        double value = 0;
        for (var row = 0; row < 4; row++)
        {
            var latticeY = Math.Clamp(baseY + row - 1, 0, latticeHeight - 1);
            double line = 0;
            for (var column = 0; column < 4; column++)
                line += weightsX[column] * Lattice(Math.Clamp(baseX + column - 1, 0, latticeWidth - 1), latticeY, seed, salt);
            value += weightsY[row] * line;
        }

        return value;
    }

    public static (double R, double G, double B, double A)[] Panel(bool[] page, int width, int height, MangaScreentoneSettings.Derived derived, MangaScreentoneSettings.Parameters parameters)
    {
        var canvasWidth = width + derived.MarginPixels * 2;
        var canvasHeight = height + derived.MarginPixels * 2;
        var ink = Math.Clamp(parameters.Ink, 0f, 1f);
        var paper = MangaScreentoneSettings.GetPaper(parameters.Paper);
        var transparent = parameters.Paper == MangaScreentonePaper.Transparent;

        double PageValue(int canvasX, int canvasY)
        {
            var x = canvasX - derived.MarginPixels;
            var y = canvasY - derived.MarginPixels;
            if (x < 0 || y < 0 || x >= width || y >= height)
                return 0d;
            if (derived.BorderPixels > 0 && (x < derived.BorderPixels || y < derived.BorderPixels || x >= width - derived.BorderPixels || y >= height - derived.BorderPixels))
                return 1d;
            return page[y * width + x] ? 1d : 0d;
        }

        var result = new (double R, double G, double B, double A)[canvasWidth * canvasHeight];
        for (var y = 0; y < canvasHeight; y++)
        {
            for (var x = 0; x < canvasWidth; x++)
            {
                var coverage = PageValue(x, y);
                if (ink > 0f)
                {
                    if (derived.InkRadius > 0)
                    {
                        double sum = 0, weighted = 0;
                        for (var dy = -derived.InkRadius; dy <= derived.InkRadius; dy++)
                        {
                            for (var dx = -derived.InkRadius; dx <= derived.InkRadius; dx++)
                            {
                                var weight = Math.Exp(-0.5d * (dx * dx + dy * dy) / ((double)derived.InkSigma * derived.InkSigma));
                                weighted += weight * PageValue(Reflect(x + dx, canvasWidth), Reflect(y + dy, canvasHeight));
                                sum += weight;
                            }
                        }
                        coverage = weighted / sum;
                    }

                    coverage = Math.Clamp((coverage - MangaScreentoneSettings.InkSpreadFloor) / MangaScreentoneSettings.InkSpreadRange, 0d, 1d);
                    var mottle = Noise(x, y, canvasWidth, canvasHeight, derived.InkMottleCoarse, MangaScreentoneSettings.NoiseSeedInk, 0x51ED270Bu)
                        + MangaScreentoneSettings.InkMottleFineWeight * Noise(x, y, canvasWidth, canvasHeight, derived.InkMottleFine, MangaScreentoneSettings.NoiseSeedInk, 0x2C1B3C6Du);
                    coverage *= 1d - MangaScreentoneSettings.InkMottleDepth * ink * (0.5d + 0.5d * Math.Tanh(mottle));
                }

                var red = MangaScreentoneSettings.InkColor.R * (double)ink;
                var green = MangaScreentoneSettings.InkColor.G * (double)ink;
                var blue = MangaScreentoneSettings.InkColor.B * (double)ink;
                if (transparent)
                {
                    result[y * canvasWidth + x] = (red * coverage, green * coverage, blue * coverage, coverage);
                    continue;
                }

                var grain = 0d;
                if (paper.Fibre > 0f)
                {
                    grain = paper.Fibre * (Noise(x, y, canvasWidth, canvasHeight, 3, MangaScreentoneSettings.NoiseSeedFibre, 0x297A2D39u)
                        + MangaScreentoneSettings.FibreFineWeight * Noise(x, y, canvasWidth, canvasHeight, 1, MangaScreentoneSettings.NoiseSeedFibre, 0x68E31DA4u));
                }

                var open = 1d - coverage;
                result[y * canvasWidth + x] = (
                    Math.Clamp((paper.R + grain) * open + red * coverage, 0d, 1d),
                    Math.Clamp((paper.G + grain) * open + green * coverage, 0d, 1d),
                    Math.Clamp((paper.B + grain) * open + blue * coverage, 0d, 1d),
                    1d);
            }
        }

        return result;
    }

    public static (double R, double G, double B, double A)[] Render(ReadOnlySpan<int> source, int width, int height, MangaScreentoneSettings.Parameters parameters)
    {
        var derived = MangaScreentoneSettings.Derive(width, height, parameters);
        var analysis = Analyze(source, width, height, derived);
        return Panel(Page(analysis, width, height, derived, parameters), width, height, derived, parameters);
    }
}
