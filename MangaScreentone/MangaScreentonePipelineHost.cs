using ComputeWeave;
using ComputeWeave.Descriptors;

namespace MangaScreentone;

[ComputeResourceGroup]
internal sealed partial class MangaScreentoneResources
{
    [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
    internal ReadWriteBuffer<float> Gray { get; }

    [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
    internal ReadWriteBuffer<float> BlurA { get; }

    [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
    internal ReadWriteBuffer<float> BlurB { get; }

    [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
    internal ReadWriteBuffer<float> ValueA { get; }

    [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
    internal ReadWriteBuffer<float> ValueB { get; }

    [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
    internal ReadWriteBuffer<float> Smooth { get; }

    [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
    internal ReadWriteBuffer<float> Accent { get; }

    [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
    internal ReadWriteBuffer<uint> Classes { get; }

    [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
    internal ReadWriteBuffer<uint> Labels { get; }

    [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
    internal ReadWriteBuffer<uint> StatA { get; }

    [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
    internal ReadWriteBuffer<uint> StatB { get; }

    [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
    internal ReadWriteBuffer<uint> StatC { get; }

    [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
    internal ReadWriteBuffer<uint> StatD { get; }

    [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
    internal ReadWriteBuffer<uint> StatE { get; }

    [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
    internal ReadWriteBuffer<uint> Solid { get; }

    [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
    internal ReadWriteBuffer<uint> Ink { get; }

    [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
    internal ReadWriteBuffer<uint> Level { get; }

    [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
    internal ReadWriteBuffer<uint> LevelNext { get; }

    [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
    internal ReadWriteBuffer<uint> Page { get; }

    [ComputePipelineResource(ComputeResourceAccess.ReadWrite)]
    internal ReadWriteBuffer<uint> Cut { get; }

    internal static Plan PerPixel(int length)
        => new(
            accentLength: length,
            blurALength: length,
            blurBLength: length,
            classesLength: length,
            cutLength: length,
            grayLength: length,
            inkLength: length,
            labelsLength: length,
            levelLength: length,
            levelNextLength: length,
            pageLength: length,
            smoothLength: length,
            solidLength: length,
            statALength: length,
            statBLength: length,
            statCLength: length,
            statDLength: length,
            statELength: length,
            valueALength: length,
            valueBLength: length);
}

[ComputePipelineHost("_device", 1)]
internal sealed partial class MangaScreentonePipelineHost
{
    private static readonly uint[] PlanOrder = [1u, 2u, 3u, 0u];

    private readonly GraphicsDevice _device;

    [ComputePipelineResource(ComputeResourceAccess.ReadWrite, ComputeResourceRecovery.Recompute)]
    private readonly ComputeResourceGroupSlot<MangaScreentoneResources> _grid = new();

    [ComputePipeline]
    private void RecordSource(
        in ComputeContext context,
        [ComputeOwnedResource(nameof(_grid))] MangaScreentoneResources grid,
        [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteTexture2D<Bgra32, Float4> source,
        [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<int> scratch,
        int width,
        int height,
        in MangaScreentoneSettings.Derived derived)
    {
        _ = _device;

        RecordSourceStage(in context, grid, source, scratch, width, height, in derived);
    }

    [ComputePipeline]
    [ComputeInterop]
    private void RecordSharedSource(
        in ComputeContext context,
        [ComputeOwnedResource(nameof(_grid))] MangaScreentoneResources grid,
        [ComputeResource(ComputeResourceAccess.ReadWrite, Sharing = ComputeResourceSharing.External)] ReadWriteTexture2D<Bgra32, Float4> source,
        [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteBuffer<int> scratch,
        int width,
        int height,
        in MangaScreentoneSettings.Derived derived)
    {
        _ = _device;

        RecordSourceStage(in context, grid, source, scratch, width, height, in derived);
    }

    [ComputePipeline]
    private void RecordAnalysis(
        in ComputeContext context,
        [ComputeOwnedResource(nameof(_grid))] MangaScreentoneResources grid,
        int width,
        int height,
        in MangaScreentoneSettings.Derived derived)
    {
        _ = _device;

        RecordAnalysisStage(in context, grid, width, height, in derived);
    }

    [ComputePipeline]
    private void RecordCut(
        in ComputeContext context,
        [ComputeOwnedResource(nameof(_grid))] MangaScreentoneResources grid,
        int width,
        int height,
        in MangaScreentoneSettings.Derived derived)
    {
        _ = _device;

        RecordCutStage(in context, grid, width, height, in derived);
    }

    [ComputePipeline]
    private void RecordTonePlan(
        in ComputeContext context,
        [ComputeOwnedResource(nameof(_grid))] MangaScreentoneResources grid,
        int width,
        int height,
        in MangaScreentoneSettings.Derived derived)
    {
        _ = _device;

        RecordTonePlanStage(in context, grid, width, height, in derived);
    }

    [ComputePipeline]
    private void RecordPrint(
        in ComputeContext context,
        [ComputeOwnedResource(nameof(_grid))] MangaScreentoneResources grid,
        int width,
        int height,
        in MangaScreentoneSettings.Derived derived)
    {
        _ = _device;

        RecordPrintStage(in context, grid, width, height, in derived);
    }

    [ComputePipeline]
    private void RecordPanel(
        in ComputeContext context,
        [ComputeOwnedResource(nameof(_grid))] MangaScreentoneResources grid,
        [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteTexture2D<Bgra32, Float4> output,
        int width,
        int height,
        in MangaScreentoneSettings.Derived derived)
    {
        _ = _device;

        RecordPanelStage(in context, grid, output, width, height, in derived);
    }

    [ComputePipeline]
    [ComputeInterop]
    private void RecordSharedPanel(
        in ComputeContext context,
        [ComputeOwnedResource(nameof(_grid))] MangaScreentoneResources grid,
        [ComputeResource(ComputeResourceAccess.ReadWrite, Sharing = ComputeResourceSharing.External)] ReadWriteTexture2D<Bgra32, Float4> output,
        int width,
        int height,
        in MangaScreentoneSettings.Derived derived)
    {
        _ = _device;

        RecordPanelStage(in context, grid, output, width, height, in derived);
    }

    private static void ForAligned<TShader>(in ComputeContext context, int width, int height, in TShader shader)
        where TShader : struct, IComputeShader, IComputeShaderDescriptor<TShader>
    {
        context.For(ThreadGroupAlignment.AlignX<TShader>(width), ThreadGroupAlignment.AlignY<TShader>(height), in shader);
    }

    private static void RecordSourceStage(
        in ComputeContext context,
        MangaScreentoneResources grid,
        ReadWriteTexture2D<Bgra32, Float4> source,
        ReadWriteBuffer<int> scratch,
        int width,
        int height,
        in MangaScreentoneSettings.Derived derived)
    {
        context.For(MangaScreentoneSettings.ScratchLength, new ScratchResetShader(scratch, MangaScreentoneSettings.ScratchLength));
        context.Barrier(scratch);
        ForAligned(
            in context,
            width,
            height,
            new GrayShader(
                source,
                grid.Gray,
                scratch,
                width,
                height,
                derived.LevelBlack,
                Math.Max(derived.LevelWhite - derived.LevelBlack, MangaScreentoneSettings.MinimumLevelGap),
                1f / derived.Gamma));
        context.Barrier(grid.Gray);
        context.Barrier(scratch);
    }

    private static void RecordBlur(
        in ComputeContext context,
        ReadWriteBuffer<float> input,
        ReadWriteBuffer<float> temporary,
        ReadWriteBuffer<float> output,
        int width,
        int height,
        float sigma,
        int radius)
    {
        ForAligned(in context, width, height, new GaussianBlurShader(input, temporary, width, height, sigma, radius, 0));
        context.Barrier(temporary);
        ForAligned(in context, width, height, new GaussianBlurShader(temporary, output, width, height, sigma, radius, 1));
        context.Barrier(output);
    }

    private static void RecordBilateral(
        in ComputeContext context,
        ReadWriteBuffer<float> input,
        ReadWriteBuffer<float> output,
        int width,
        int height,
        in MangaScreentoneSettings.Derived derived)
    {
        ForAligned(
            in context,
            width,
            height,
            new BilateralShader(input, output, width, height, derived.BilateralRadius, derived.BilateralSpaceCoefficient, derived.BilateralColorCoefficient));
        context.Barrier(output);
    }

    private static void RecordComponents(
        in ComputeContext context,
        MangaScreentoneResources grid,
        ReadWriteBuffer<uint> classes,
        int width,
        int height,
        bool diagonal,
        ComponentStatistic statistic)
    {
        context.For(width, height, new ComponentInitShader(classes, grid.Labels, width, height, diagonal ? 1 : 0));
        context.Barrier(grid.Labels);
        context.For(width, height, new ComponentUnionShader(classes, grid.Labels, width, height, diagonal ? 1 : 0));
        context.Barrier(grid.Labels);
        context.For(width, height, new ComponentFlattenShader(
            classes, grid.Labels, grid.StatA, grid.StatB, grid.StatC, grid.StatD, grid.StatE, width, height, statistic == ComponentStatistic.Bounds ? 1 : 0));
        context.Barrier(grid.Labels);
        context.Barrier(grid.StatA);
        context.Barrier(grid.StatB);
        context.Barrier(grid.StatC);
        context.Barrier(grid.StatD);
        context.Barrier(grid.StatE);
        if (statistic == ComponentStatistic.Bounds)
        {
            ForAligned(
                in context,
                width,
                height,
                new ComponentBoundsShader(classes, grid.Labels, grid.StatA, grid.StatB, grid.StatC, grid.StatD, grid.StatE, width, height));
            context.Barrier(grid.StatB);
            context.Barrier(grid.StatC);
            context.Barrier(grid.StatD);
            context.Barrier(grid.StatE);
        }
        else if (statistic == ComponentStatistic.Moments)
        {
            ForAligned(
                in context,
                width,
                height,
                new ComponentMomentShader(classes, grid.Labels, grid.Smooth, grid.StatA, grid.StatB, grid.StatC, grid.StatD, width, height));
            context.Barrier(grid.StatB);
            context.Barrier(grid.StatC);
            context.Barrier(grid.StatD);
        }
        else
        {
            ForAligned(in context, width, height, new ComponentAreaShader(classes, grid.Labels, grid.StatA, width, height));
        }
        context.Barrier(grid.StatA);
    }

    private static void RecordAnalysisStage(
        in ComputeContext context,
        MangaScreentoneResources grid,
        int width,
        int height,
        in MangaScreentoneSettings.Derived derived)
    {
        RecordSolid(in context, grid, width, height, in derived);
        RecordInk(in context, grid, width, height, in derived);
        RecordValueMap(in context, grid, width, height, in derived);
    }

    private static void RecordSolid(
        in ComputeContext context,
        MangaScreentoneResources grid,
        int width,
        int height,
        in MangaScreentoneSettings.Derived derived)
    {
        RecordBlur(in context, grid.Gray, grid.BlurA, grid.BlurB, width, height, derived.SolidSigma, derived.SolidRadius);
        context.For(width, height, new SolidShader(grid.BlurB, grid.Solid, width, height, MangaScreentoneSettings.SolidThreshold));
        context.Barrier(grid.Solid);
    }

    private static void RecordInk(
        in ComputeContext context,
        MangaScreentoneResources grid,
        int width,
        int height,
        in MangaScreentoneSettings.Derived derived)
    {
        RecordBlur(in context, grid.Gray, grid.BlurA, grid.BlurB, width, height, derived.LineSigma, derived.LineRadius);
        var meanRadius = derived.LineMeanSize / 2;
        context.For(width, height, new BoxBlurShader(grid.BlurB, grid.BlurA, width, height, meanRadius, 0));
        context.Barrier(grid.BlurA);
        context.For(width, height, new BoxBlurShader(grid.BlurA, grid.ValueB, width, height, meanRadius, 1));
        context.Barrier(grid.ValueB);
        context.For(width, height, new LineMaskShader(
            grid.BlurB, grid.ValueB, grid.Classes, width, height, MangaScreentoneSettings.LineContrast, MangaScreentoneSettings.LineCeiling));
        context.Barrier(grid.Classes);

        RecordComponents(in context, grid, grid.Classes, width, height, true, ComponentStatistic.Bounds);
        context.For(width, height, new LineKeepShader(
            grid.Classes, grid.Labels, grid.StatA, grid.StatB, grid.StatC, grid.StatD, grid.StatE, grid.Solid, width, height,
            derived.StrokeMinLength, derived.StrokeMinArea, MangaScreentoneSettings.StrokeFillRatio, derived.StrokeFillBias, derived.StrokeLargeArea));
        context.Barrier(grid.Classes);

        RecordComponents(in context, grid, grid.Classes, width, height, true, ComponentStatistic.Area);
        context.For(width, height, new InkFinalShader(grid.Classes, grid.Labels, grid.StatA, grid.Ink, width, height, derived.InkMinArea));
        context.Barrier(grid.Ink);
    }

    private static void RecordValueMap(
        in ComputeContext context,
        MangaScreentoneResources grid,
        int width,
        int height,
        in MangaScreentoneSettings.Derived derived)
    {
        ForAligned(
            in context,
            width,
            height,
            new MaskedBlurHorizontalShader(grid.Gray, grid.Ink, grid.BlurA, grid.BlurB, width, height, derived.ValueSigma, derived.ValueRadius));
        context.Barrier(grid.BlurA);
        context.Barrier(grid.BlurB);
        ForAligned(
            in context,
            width,
            height,
            new MaskedBlurVerticalShader(grid.BlurA, grid.BlurB, grid.ValueA, width, height, derived.ValueSigma, derived.ValueRadius, MangaScreentoneSettings.ValueMinWeight));
        context.Barrier(grid.ValueA);
        RecordBilateral(in context, grid.ValueA, grid.ValueB, width, height, in derived);
        RecordBilateral(in context, grid.ValueB, grid.ValueA, width, height, in derived);
    }

    private static void RecordCutStage(
        in ComputeContext context,
        MangaScreentoneResources grid,
        int width,
        int height,
        in MangaScreentoneSettings.Derived derived)
    {
        context.For(width, height, new StrokeClassShader(grid.Ink, grid.Solid, grid.Classes, width, height));
        context.Barrier(grid.Classes);
        RecordComponents(in context, grid, grid.Classes, width, height, true, ComponentStatistic.Bounds);
        context.For(width, height, new CutKeepShader(
            grid.Classes, grid.Labels, grid.StatB, grid.StatC, grid.StatD, grid.StatE, grid.LevelNext, width, height, derived.CutMinLength));
        context.Barrier(grid.LevelNext);
        context.For(width, height, new ErodeShader(grid.LevelNext, grid.Cut, width, height, derived.CutErodeSize, 1u));
        context.Barrier(grid.Cut);
    }

    private static void RecordTonePlanStage(
        in ComputeContext context,
        MangaScreentoneResources grid,
        int width,
        int height,
        in MangaScreentoneSettings.Derived derived)
    {
        RecordToneLevels(in context, grid, width, height, in derived);
        var level = RecordSmallRegionFill(in context, grid, width, height, in derived);
        RecordRegionSpread(in context, grid, level, width, height, in derived);
    }

    private static void RecordToneLevels(
        in ComputeContext context,
        MangaScreentoneResources grid,
        int width,
        int height,
        in MangaScreentoneSettings.Derived derived)
    {
        context.For(width, height, new DigitizeShader(
            grid.ValueA, grid.Level, width, height, derived.White, (derived.White + derived.Black) * 0.5f, derived.Black));
        context.Barrier(grid.Level);
        var planRadius = derived.PlanMedianSize / 2;
        context.For(width, height, new MedianCountShader(grid.Level, grid.StatB, grid.StatC, width, height, planRadius));
        context.Barrier(grid.StatB);
        context.Barrier(grid.StatC);
        context.For(width, height, new MedianResolveShader(grid.StatB, grid.StatC, grid.Level, width, height, planRadius));
        context.Barrier(grid.Level);
    }

    private static ReadWriteBuffer<uint> RecordSmallRegionFill(
        in ComputeContext context,
        MangaScreentoneResources grid,
        int width,
        int height,
        in MangaScreentoneSettings.Derived derived)
    {
        var fillRadius = derived.PlanFillMedianSize / 2;
        var current = grid.Level;
        var next = grid.LevelNext;
        foreach (var target in PlanOrder)
        {
            context.For(width, height, new ClassFromLevelShader(current, grid.Classes, width, height, target));
            context.Barrier(grid.Classes);
            RecordComponents(in context, grid, grid.Classes, width, height, false, ComponentStatistic.Area);
            context.For(width, height, new HoleMarkShader(current, grid.Labels, grid.StatA, grid.Classes, width, height, target, derived.PlanMinRegion));
            context.Barrier(grid.Classes);
            ForAligned(in context, width, height, new HoleCountShader(grid.Classes, grid.StatB, grid.StatC, width, height, fillRadius));
            context.Barrier(grid.StatB);
            context.Barrier(grid.StatC);
            context.For(width, height, new HoleResolveShader(current, grid.Classes, grid.StatB, grid.StatC, next, width, height, fillRadius));
            context.Barrier(next);
            (current, next) = (next, current);
        }

        return current;
    }

    private static void RecordRegionSpread(
        in ComputeContext context,
        MangaScreentoneResources grid,
        ReadWriteBuffer<uint> level,
        int width,
        int height,
        in MangaScreentoneSettings.Derived derived)
    {
        RecordBlur(in context, grid.ValueA, grid.BlurA, grid.Smooth, width, height, derived.SpreadSigma, derived.SpreadRadius);
        RecordComponents(in context, grid, level, width, height, false, ComponentStatistic.Moments);
        context.For(width, height, new SpreadResolveShader(
            level, grid.Labels, grid.StatA, grid.StatB, grid.StatC, grid.StatD, grid.BlurA, width, height,
            MangaScreentoneSettings.SpreadFloor, MangaScreentoneSettings.SpreadRange));
        context.Barrier(grid.BlurA);
        RecordBlur(in context, grid.BlurA, grid.BlurB, grid.Accent, width, height, derived.SmoothSigma, derived.SmoothRadius);
    }

    private static void RecordPrintStage(
        in ComputeContext context,
        MangaScreentoneResources grid,
        int width,
        int height,
        in MangaScreentoneSettings.Derived derived)
    {
        context.For(width, height, new CoverShader(
            grid.Level, grid.Smooth, grid.Accent, grid.BlurA, width, height, derived.White, derived.Black,
            derived.LightTone, derived.DarkTone, derived.Gradation));
        context.Barrier(grid.BlurA);
        context.For(width, height, new PrintShader(grid.BlurA, grid.Page, width, height, derived.Pitch));
        context.Barrier(grid.Page);
        if (derived.WhiteLines)
        {
            context.For(width, height, new ErodeShader(grid.Level, grid.LevelNext, width, height, derived.BetaSize, 3u));
            context.Barrier(grid.LevelNext);
        }
        context.For(width, height, new PageShader(grid.Page, grid.Ink, grid.LevelNext, grid.Cut, width, height, derived.WhiteLines ? 1 : 0));
        context.Barrier(grid.Page);
    }

    private static void RecordPanelStage(
        in ComputeContext context,
        MangaScreentoneResources grid,
        ReadWriteTexture2D<Bgra32, Float4> output,
        int width,
        int height,
        in MangaScreentoneSettings.Derived derived)
    {
        var paper = MangaScreentoneSettings.GetPaper(derived.Paper);
        var canvasWidth = width + derived.MarginPixels * 2;
        var canvasHeight = height + derived.MarginPixels * 2;
        context.For(canvasWidth, canvasHeight, new PanelShader(
            grid.Page,
            output,
            width,
            height,
            canvasWidth,
            canvasHeight,
            derived.MarginPixels,
            derived.BorderPixels,
            derived.InkRadius,
            derived.InkSigma,
            derived.InkStrength,
            derived.InkMottleCoarse,
            derived.InkMottleFine,
            paper.R,
            paper.G,
            paper.B,
            paper.Fibre,
            derived.Paper == MangaScreentonePaper.Transparent ? 1 : 0,
            MangaScreentoneSettings.InkColor.R,
            MangaScreentoneSettings.InkColor.G,
            MangaScreentoneSettings.InkColor.B));
    }

    private enum ComponentStatistic
    {
        Area,
        Bounds,
        Moments,
    }
}
