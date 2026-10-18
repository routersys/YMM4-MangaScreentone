using ComputeWeave;

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
}

[ComputePipelineHost("_device", 1)]
internal sealed partial class MangaScreentonePipelineHost
{
    private const int AreaOnly = 0;
    private const int Bounds = 1;
    private const int Moments = 2;
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
        in MangaScreentoneSettings.Derived derived,
        in MangaScreentoneSettings.Parameters parameters)
    {
        _ = _device;

        RecordPrintStage(in context, grid, width, height, in derived, in parameters);
    }

    [ComputePipeline]
    private void RecordPanel(
        in ComputeContext context,
        [ComputeOwnedResource(nameof(_grid))] MangaScreentoneResources grid,
        [ComputeResource(ComputeResourceAccess.ReadWrite)] ReadWriteTexture2D<Bgra32, Float4> output,
        int width,
        int height,
        in MangaScreentoneSettings.Derived derived,
        in MangaScreentoneSettings.Parameters parameters)
    {
        _ = _device;

        RecordPanelStage(in context, grid, output, width, height, in derived, in parameters);
    }

    [ComputePipeline]
    [ComputeInterop]
    private void RecordSharedPanel(
        in ComputeContext context,
        [ComputeOwnedResource(nameof(_grid))] MangaScreentoneResources grid,
        [ComputeResource(ComputeResourceAccess.ReadWrite, Sharing = ComputeResourceSharing.External)] ReadWriteTexture2D<Bgra32, Float4> output,
        int width,
        int height,
        in MangaScreentoneSettings.Derived derived,
        in MangaScreentoneSettings.Parameters parameters)
    {
        _ = _device;

        RecordPanelStage(in context, grid, output, width, height, in derived, in parameters);
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
        context.For(
            ThreadGroupAlignment.AlignX<GrayShader>(width),
            ThreadGroupAlignment.AlignY<GrayShader>(height),
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
        context.For(width, height, new GaussianBlurShader(input, temporary, width, height, sigma, radius, 0));
        context.Barrier(temporary);
        context.For(width, height, new GaussianBlurShader(temporary, output, width, height, sigma, radius, 1));
        context.Barrier(output);
    }

    private static void RecordComponents(
        in ComputeContext context,
        MangaScreentoneResources grid,
        ReadWriteBuffer<uint> classes,
        int width,
        int height,
        int diagonal,
        int mode)
    {
        context.For(width, height, new ComponentInitShader(classes, grid.Labels, width, height, diagonal));
        context.Barrier(grid.Labels);
        context.For(width, height, new ComponentUnionShader(classes, grid.Labels, width, height, diagonal));
        context.Barrier(grid.Labels);
        context.For(width, height, new ComponentFlattenShader(
            classes, grid.Labels, grid.StatA, grid.StatB, grid.StatC, grid.StatD, grid.StatE, width, height, mode == Bounds ? 1 : 0));
        context.Barrier(grid.Labels);
        context.Barrier(grid.StatA);
        context.Barrier(grid.StatB);
        context.Barrier(grid.StatC);
        context.Barrier(grid.StatD);
        context.Barrier(grid.StatE);
        if (mode == Bounds)
        {
            context.For(
                ThreadGroupAlignment.AlignX<ComponentBoundsShader>(width),
                ThreadGroupAlignment.AlignY<ComponentBoundsShader>(height),
                new ComponentBoundsShader(classes, grid.Labels, grid.StatA, grid.StatB, grid.StatC, grid.StatD, grid.StatE, width, height));
            context.Barrier(grid.StatB);
            context.Barrier(grid.StatC);
            context.Barrier(grid.StatD);
            context.Barrier(grid.StatE);
        }
        else if (mode == Moments)
        {
            context.For(
                ThreadGroupAlignment.AlignX<ComponentMomentShader>(width),
                ThreadGroupAlignment.AlignY<ComponentMomentShader>(height),
                new ComponentMomentShader(classes, grid.Labels, grid.Smooth, grid.StatA, grid.StatB, grid.StatC, grid.StatD, width, height));
            context.Barrier(grid.StatB);
            context.Barrier(grid.StatC);
            context.Barrier(grid.StatD);
        }
        else
        {
            context.For(
                ThreadGroupAlignment.AlignX<ComponentAreaShader>(width),
                ThreadGroupAlignment.AlignY<ComponentAreaShader>(height),
                new ComponentAreaShader(classes, grid.Labels, grid.StatA, width, height));
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
        RecordBlur(in context, grid.Gray, grid.BlurA, grid.BlurB, width, height, derived.SolidSigma, derived.SolidRadius);
        context.For(width, height, new SolidShader(grid.BlurB, grid.Solid, width, height, MangaScreentoneSettings.SolidThreshold));
        context.Barrier(grid.Solid);

        RecordBlur(in context, grid.Gray, grid.BlurA, grid.BlurB, width, height, derived.LineSigma, derived.LineRadius);
        var meanRadius = derived.LineMeanSize / 2;
        context.For(width, height, new BoxBlurShader(grid.BlurB, grid.BlurA, width, height, meanRadius, 0));
        context.Barrier(grid.BlurA);
        context.For(width, height, new BoxBlurShader(grid.BlurA, grid.ValueB, width, height, meanRadius, 1));
        context.Barrier(grid.ValueB);
        context.For(width, height, new LineMaskShader(
            grid.BlurB, grid.ValueB, grid.Classes, width, height, MangaScreentoneSettings.LineContrast, MangaScreentoneSettings.LineCeiling));
        context.Barrier(grid.Classes);

        RecordComponents(in context, grid, grid.Classes, width, height, 1, Bounds);
        context.For(width, height, new LineKeepShader(
            grid.Classes, grid.Labels, grid.StatA, grid.StatB, grid.StatC, grid.StatD, grid.StatE, grid.Solid, width, height,
            derived.StrokeMinLength, derived.StrokeMinArea, MangaScreentoneSettings.StrokeFillRatio, derived.StrokeFillBias, derived.StrokeLargeArea));
        context.Barrier(grid.Classes);

        RecordComponents(in context, grid, grid.Classes, width, height, 1, AreaOnly);
        context.For(width, height, new InkFinalShader(grid.Classes, grid.Labels, grid.StatA, grid.Ink, width, height, derived.InkMinArea));
        context.Barrier(grid.Ink);

        context.For(width, height, new MaskedBlurHorizontalShader(
            grid.Gray, grid.Ink, grid.BlurA, grid.BlurB, width, height, derived.ValueSigma, derived.ValueRadius));
        context.Barrier(grid.BlurA);
        context.Barrier(grid.BlurB);
        context.For(width, height, new MaskedBlurVerticalShader(
            grid.BlurA, grid.BlurB, grid.ValueA, width, height, derived.ValueSigma, derived.ValueRadius, MangaScreentoneSettings.ValueMinWeight));
        context.Barrier(grid.ValueA);
        for (var pass = 0; pass < 2; pass++)
        {
            var reading = pass == 0 ? grid.ValueA : grid.ValueB;
            var writing = pass == 0 ? grid.ValueB : grid.ValueA;
            context.For(width, height, new BilateralShader(
                reading, writing, width, height, derived.BilateralRadius, derived.BilateralSpaceCoefficient, derived.BilateralColorCoefficient));
            context.Barrier(writing);
        }
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
        RecordComponents(in context, grid, grid.Classes, width, height, 1, Bounds);
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
        context.For(width, height, new DigitizeShader(
            grid.ValueA, grid.Level, width, height, derived.White, (derived.White + derived.Black) * 0.5f, derived.Black));
        context.Barrier(grid.Level);
        var planRadius = derived.PlanMedianSize / 2;
        context.For(width, height, new MedianCountShader(grid.Level, grid.StatB, grid.StatC, width, height, planRadius));
        context.Barrier(grid.StatB);
        context.Barrier(grid.StatC);
        context.For(width, height, new MedianResolveShader(grid.StatB, grid.StatC, grid.Level, width, height, planRadius));
        context.Barrier(grid.Level);

        var fillRadius = derived.PlanFillMedianSize / 2;
        var current = grid.Level;
        var next = grid.LevelNext;
        foreach (var target in PlanOrder)
        {
            context.For(width, height, new ClassFromLevelShader(current, grid.Classes, width, height, target));
            context.Barrier(grid.Classes);
            RecordComponents(in context, grid, grid.Classes, width, height, 0, AreaOnly);
            context.For(width, height, new HoleCountShader(
                current, grid.Labels, grid.StatA, grid.StatB, grid.StatC, width, height, fillRadius, target, derived.PlanMinRegion));
            context.Barrier(grid.StatB);
            context.Barrier(grid.StatC);
            context.For(width, height, new HoleResolveShader(
                current, grid.Labels, grid.StatA, grid.StatB, grid.StatC, next, width, height, fillRadius, target, derived.PlanMinRegion));
            context.Barrier(next);
            (current, next) = (next, current);
        }

        RecordBlur(in context, grid.ValueA, grid.BlurA, grid.Smooth, width, height, derived.SpreadSigma, derived.SpreadRadius);
        RecordComponents(in context, grid, current, width, height, 0, Moments);
        context.For(width, height, new SpreadResolveShader(
            current, grid.Labels, grid.StatA, grid.StatB, grid.StatC, grid.StatD, grid.BlurA, width, height,
            MangaScreentoneSettings.SpreadFloor, MangaScreentoneSettings.SpreadRange));
        context.Barrier(grid.BlurA);
        RecordBlur(in context, grid.BlurA, grid.BlurB, grid.Accent, width, height, derived.SmoothSigma, derived.SmoothRadius);
    }

    private static void RecordPrintStage(
        in ComputeContext context,
        MangaScreentoneResources grid,
        int width,
        int height,
        in MangaScreentoneSettings.Derived derived,
        in MangaScreentoneSettings.Parameters parameters)
    {
        context.For(width, height, new CoverShader(
            grid.Level, grid.Smooth, grid.Accent, grid.BlurA, width, height, derived.White, derived.Black,
            Math.Clamp(parameters.LightTone, 0f, 1f), Math.Clamp(parameters.DarkTone, 0f, 1f), Math.Clamp(parameters.Gradation, 0f, 1f)));
        context.Barrier(grid.BlurA);
        context.For(width, height, new PrintShader(grid.BlurA, grid.Page, width, height, derived.Pitch));
        context.Barrier(grid.Page);
        if (parameters.WhiteLines)
        {
            context.For(width, height, new ErodeShader(grid.Level, grid.LevelNext, width, height, derived.BetaSize, 3u));
            context.Barrier(grid.LevelNext);
        }
        context.For(width, height, new PageShader(grid.Page, grid.Ink, grid.LevelNext, grid.Cut, width, height, parameters.WhiteLines ? 1 : 0));
        context.Barrier(grid.Page);
    }

    private static void RecordPanelStage(
        in ComputeContext context,
        MangaScreentoneResources grid,
        ReadWriteTexture2D<Bgra32, Float4> output,
        int width,
        int height,
        in MangaScreentoneSettings.Derived derived,
        in MangaScreentoneSettings.Parameters parameters)
    {
        var paper = MangaScreentoneSettings.GetPaper(parameters.Paper);
        var ink = Math.Clamp(parameters.Ink, 0f, 1f);
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
            ink,
            derived.InkMottleCoarse,
            derived.InkMottleFine,
            paper.R,
            paper.G,
            paper.B,
            paper.Fibre,
            parameters.Paper == MangaScreentonePaper.Transparent ? 1 : 0,
            MangaScreentoneSettings.InkColor.R,
            MangaScreentoneSettings.InkColor.G,
            MangaScreentoneSettings.InkColor.B));
    }
}
