// Stryker disable all
using ComputeWeave;

namespace MangaScreentone;

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct ComponentInitShader(
    ReadWriteBuffer<uint> classes,
    ReadWriteBuffer<uint> labels,
    int width,
    int height,
    int diagonal) : IComputeShader
{
    private readonly ReadWriteBuffer<uint> classes = classes;
    private readonly ReadWriteBuffer<uint> labels = labels;
    private readonly int width = width;
    private readonly int height = height;
    private readonly int diagonal = diagonal;

    public void Execute()
    {
        var x = ThreadIds.X;
        var y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        var index = y * width + x;
        var own = classes[index];
        if (own == MangaScreentoneSettings.InactiveClass)
        {
            labels[index] = MangaScreentoneSettings.InactiveClass;
            return;
        }

        var parent = (uint)index;
        if (x > 0 && classes[index - 1] == own)
            parent = Hlsl.Min(parent, (uint)(index - 1));
        if (y > 0 && classes[index - width] == own)
            parent = Hlsl.Min(parent, (uint)(index - width));
        if (diagonal != 0 && y > 0)
        {
            if (x > 0 && classes[index - width - 1] == own)
                parent = Hlsl.Min(parent, (uint)(index - width - 1));
            if (x + 1 < width && classes[index - width + 1] == own)
                parent = Hlsl.Min(parent, (uint)(index - width + 1));
        }
        labels[index] = parent;
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct ComponentUnionShader(
    ReadWriteBuffer<uint> classes,
    ReadWriteBuffer<uint> labels,
    int width,
    int height,
    int diagonal) : IComputeShader
{
    private readonly ReadWriteBuffer<uint> classes = classes;
    private readonly ReadWriteBuffer<uint> labels = labels;
    private readonly int width = width;
    private readonly int height = height;
    private readonly int diagonal = diagonal;

    private uint Find(uint start)
    {
        var node = start;
        for (var guard = 0; guard < MangaScreentoneSettings.MaximumUnionIterations; guard++)
        {
            var parent = labels[(int)node];
            if (parent == node)
                return node;
            var grandparent = labels[(int)parent];
            labels[(int)node] = grandparent;
            node = grandparent;
        }
        return node;
    }

    private void Union(uint first, uint second)
    {
        var a = first;
        var b = second;
        for (var guard = 0; guard < MangaScreentoneSettings.MaximumUnionIterations; guard++)
        {
            a = Find(a);
            b = Find(b);
            if (a == b)
                return;
            if (a < b)
            {
                var swap = a;
                a = b;
                b = swap;
            }
            uint previous;
            Hlsl.InterlockedCompareExchange(ref labels[(int)a], a, b, out previous);
            if (previous == a)
                return;
        }
    }

    public void Execute()
    {
        var x = ThreadIds.X;
        var y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        var index = y * width + x;
        var own = classes[index];
        if (own == MangaScreentoneSettings.InactiveClass)
            return;

        if (x > 0 && classes[index - 1] == own)
            Union((uint)index, (uint)(index - 1));
        if (y > 0 && classes[index - width] == own)
            Union((uint)index, (uint)(index - width));
        if (diagonal != 0 && y > 0)
        {
            if (x > 0 && classes[index - width - 1] == own)
                Union((uint)index, (uint)(index - width - 1));
            if (x + 1 < width && classes[index - width + 1] == own)
                Union((uint)index, (uint)(index - width + 1));
        }
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct ComponentFlattenShader(
    ReadWriteBuffer<uint> classes,
    ReadWriteBuffer<uint> labels,
    ReadWriteBuffer<uint> statA,
    ReadWriteBuffer<uint> statB,
    ReadWriteBuffer<uint> statC,
    ReadWriteBuffer<uint> statD,
    ReadWriteBuffer<uint> statE,
    int width,
    int height,
    int bounds) : IComputeShader
{
    private readonly ReadWriteBuffer<uint> classes = classes;
    private readonly ReadWriteBuffer<uint> labels = labels;
    private readonly ReadWriteBuffer<uint> statA = statA;
    private readonly ReadWriteBuffer<uint> statB = statB;
    private readonly ReadWriteBuffer<uint> statC = statC;
    private readonly ReadWriteBuffer<uint> statD = statD;
    private readonly ReadWriteBuffer<uint> statE = statE;
    private readonly int width = width;
    private readonly int height = height;
    private readonly int bounds = bounds;

    private uint Find(uint start)
    {
        var node = start;
        for (var guard = 0; guard < MangaScreentoneSettings.MaximumUnionIterations; guard++)
        {
            var parent = labels[(int)node];
            if (parent == node)
                return node;
            var grandparent = labels[(int)parent];
            labels[(int)node] = grandparent;
            node = grandparent;
        }
        return node;
    }

    public void Execute()
    {
        var x = ThreadIds.X;
        var y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        var index = y * width + x;
        if (classes[index] == MangaScreentoneSettings.InactiveClass)
            return;

        var root = Find((uint)index);
        labels[index] = root;
        if (root != (uint)index)
            return;

        statA[index] = 0u;
        statB[index] = bounds != 0 ? 0xFFFFFFFFu : 0u;
        statC[index] = bounds != 0 ? 0xFFFFFFFFu : 0u;
        statD[index] = 0u;
        statE[index] = 0u;
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct ComponentAreaShader(
    ReadWriteBuffer<uint> classes,
    ReadWriteBuffer<uint> labels,
    ReadWriteBuffer<uint> area,
    int width,
    int height) : IComputeShader
{
    private readonly ReadWriteBuffer<uint> classes = classes;
    private readonly ReadWriteBuffer<uint> labels = labels;
    private readonly ReadWriteBuffer<uint> area = area;
    private readonly int width = width;
    private readonly int height = height;

    public void Execute()
    {
        var x = ThreadIds.X;
        var y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        var index = y * width + x;
        if (classes[index] == MangaScreentoneSettings.InactiveClass)
            return;
        Hlsl.InterlockedAdd(ref area[(int)labels[index]], 1u);
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct ComponentBoundsShader(
    ReadWriteBuffer<uint> classes,
    ReadWriteBuffer<uint> labels,
    ReadWriteBuffer<uint> area,
    ReadWriteBuffer<uint> minX,
    ReadWriteBuffer<uint> minY,
    ReadWriteBuffer<uint> maxX,
    ReadWriteBuffer<uint> maxY,
    int width,
    int height) : IComputeShader
{
    private readonly ReadWriteBuffer<uint> classes = classes;
    private readonly ReadWriteBuffer<uint> labels = labels;
    private readonly ReadWriteBuffer<uint> area = area;
    private readonly ReadWriteBuffer<uint> minX = minX;
    private readonly ReadWriteBuffer<uint> minY = minY;
    private readonly ReadWriteBuffer<uint> maxX = maxX;
    private readonly ReadWriteBuffer<uint> maxY = maxY;
    private readonly int width = width;
    private readonly int height = height;

    public void Execute()
    {
        var x = ThreadIds.X;
        var y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        var index = y * width + x;
        if (classes[index] == MangaScreentoneSettings.InactiveClass)
            return;

        var root = (int)labels[index];
        Hlsl.InterlockedAdd(ref area[root], 1u);
        Hlsl.InterlockedMin(ref minX[root], (uint)x);
        Hlsl.InterlockedMin(ref minY[root], (uint)y);
        Hlsl.InterlockedMax(ref maxX[root], (uint)x);
        Hlsl.InterlockedMax(ref maxY[root], (uint)y);
    }
}

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
internal readonly partial struct ComponentMomentShader(
    ReadWriteBuffer<uint> classes,
    ReadWriteBuffer<uint> labels,
    ReadWriteBuffer<float> values,
    ReadWriteBuffer<uint> area,
    ReadWriteBuffer<uint> sum,
    ReadWriteBuffer<uint> squareLow,
    ReadWriteBuffer<uint> squareHigh,
    int width,
    int height) : IComputeShader
{
    private readonly ReadWriteBuffer<uint> classes = classes;
    private readonly ReadWriteBuffer<uint> labels = labels;
    private readonly ReadWriteBuffer<float> values = values;
    private readonly ReadWriteBuffer<uint> area = area;
    private readonly ReadWriteBuffer<uint> sum = sum;
    private readonly ReadWriteBuffer<uint> squareLow = squareLow;
    private readonly ReadWriteBuffer<uint> squareHigh = squareHigh;
    private readonly int width = width;
    private readonly int height = height;

    public void Execute()
    {
        var x = ThreadIds.X;
        var y = ThreadIds.Y;
        if (x >= width || y >= height)
            return;

        var index = y * width + x;
        if (classes[index] == MangaScreentoneSettings.InactiveClass)
            return;

        var root = (int)labels[index];
        var quantized = (uint)(Hlsl.Saturate(values[index]) * 255f + 0.5f);
        var square = quantized * quantized;
        Hlsl.InterlockedAdd(ref area[root], 1u);
        Hlsl.InterlockedAdd(ref sum[root], quantized);
        Hlsl.InterlockedAdd(ref squareLow[root], square & 255u);
        Hlsl.InterlockedAdd(ref squareHigh[root], square >> 8);
    }
}
