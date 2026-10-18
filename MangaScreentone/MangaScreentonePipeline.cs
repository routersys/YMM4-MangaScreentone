using System.ComponentModel;
using System.Runtime.InteropServices;
using ComputeWeave;

namespace MangaScreentone;

internal sealed class MangaScreentonePipeline : IDisposable
{
    private readonly GraphicsDevice _device;
    private readonly MangaScreentonePipelineHost _host;
    private readonly ReadWriteBuffer<int> _scratch;
    private readonly ReadBackBuffer<int> _scratchReadBack;
    private ReadWriteTexture2D<Bgra32, Float4>? _packedSource;
    private ReadWriteTexture2D<Bgra32, Float4>? _packedOutput;
    private int _packedSourceWidth;
    private int _packedSourceHeight;
    private int _packedOutputWidth;
    private int _packedOutputHeight;
    private int _gridLength;
    private AnalysisKey? _analysisKey;
    private ToneKey? _toneKey;
    private PrintKey? _printKey;
    private PanelKey? _panelKey;
    private int _analysisVersion;
    private int _cutVersion = -1;
    private int _toneVersion;
    private int _printVersion;

    internal int AnalysisRuns { get; private set; }

    internal int CutRuns { get; private set; }

    internal int ToneRuns { get; private set; }

    internal int PrintRuns { get; private set; }

    internal int PanelRuns { get; private set; }

    private MangaScreentonePipeline(GraphicsDevice device, MangaScreentonePipelineHost host)
    {
        _device = device;
        _host = host;
        _scratch = device.AllocateReadWriteBuffer<int>(MangaScreentoneSettings.ScratchLength);
        _scratchReadBack = device.AllocateReadBackBuffer<int>(MangaScreentoneSettings.ScratchLength);
    }

    public static MangaScreentonePipeline? TryCreate()
    {
        try
        {
            return TryCreate(GraphicsDevice.GetDefault());
        }
        catch
        {
            return null;
        }
    }

    public static MangaScreentonePipeline? TryCreate(GraphicsDevice device)
    {
        MangaScreentonePipelineHost? host = null;
        try
        {
            host = MangaScreentonePipelineHost.Create(device, MangaScreentoneSettings.MaximumPendingSubmissions);
            var pipeline = new MangaScreentonePipeline(device, host);
            host = null;
            return pipeline;
        }
        catch (Win32Exception)
        {
            return null;
        }
        finally
        {
            host?.Dispose();
            host?.WaitForDisposal();
        }
    }

    public static (int Width, int Height) GetCanvasSize(int width, int height, in MangaScreentoneSettings.Parameters parameters)
    {
        var derived = MangaScreentoneSettings.Derive(width, height, in parameters);
        return (width + derived.MarginPixels * 2, height + derived.MarginPixels * 2);
    }

    public RenderOutcome Process(ReadOnlySpan<int> source, Span<int> destination, int width, int height, in MangaScreentoneSettings.Parameters parameters)
    {
        var sourceLength = checked(width * height);
        var (canvasWidth, canvasHeight) = GetCanvasSize(width, height, in parameters);
        var canvasLength = checked(canvasWidth * canvasHeight);
        EnsurePackedTextures(width, height, canvasWidth, canvasHeight);
        var sourceTexture = _packedSource!;
        var outputTexture = _packedOutput!;
        sourceTexture.CopyFrom(MemoryMarshal.Cast<int, Bgra32>(source[..sourceLength]));
        var outcome = Process(sourceTexture, outputTexture, width, height, in parameters);
        if (outcome == RenderOutcome.Failed)
            return outcome;
        outputTexture.CopyTo(MemoryMarshal.Cast<int, Bgra32>(destination[..canvasLength]));
        return outcome;
    }

    public RenderOutcome Process(
        ReadWriteTexture2D<Bgra32, Float4> source,
        ReadWriteTexture2D<Bgra32, Float4> destination,
        int width,
        int height,
        in MangaScreentoneSettings.Parameters parameters)
    {
        if (!EnsureGrid(width, height))
            return RenderOutcome.Failed;

        var derived = MangaScreentoneSettings.Derive(width, height, in parameters);
        _host.RecordSource(source, _scratch, width, height, in derived).Wait();
        if (!RunStages(width, height, in derived, true, out var panelKey))
            return RenderOutcome.Unchanged;

        _host.RecordPanel(destination, width, height, in derived).Wait();
        CompletePanel(panelKey);
        return RenderOutcome.Rendered;
    }

    internal RenderOutcome Render(
        ComputeResourceBinding<ReadWriteTexture2D<Bgra32, Float4>> source,
        ComputeResourceBinding<ReadWriteTexture2D<Bgra32, Float4>> output,
        int width,
        int height,
        in MangaScreentoneSettings.Parameters parameters,
        bool outputFresh)
    {
        if (!EnsureGrid(width, height))
            return RenderOutcome.Failed;

        var derived = MangaScreentoneSettings.Derive(width, height, in parameters);
        _host.RecordSharedSource(source, _scratch, width, height, in derived).Wait();
        if (!RunStages(width, height, in derived, outputFresh, out var panelKey))
            return RenderOutcome.Unchanged;

        _host.RecordSharedPanel(output, width, height, in derived).Wait();
        CompletePanel(panelKey);
        return RenderOutcome.Rendered;
    }

    private bool RunStages(
        int width,
        int height,
        in MangaScreentoneSettings.Derived derived,
        bool outputFresh,
        out PanelKey panelKey)
    {
        _scratchReadBack.CopyFrom(_scratch);
        var hashed = _scratchReadBack.Span;
        var analysisKey = new AnalysisKey(
            hashed[MangaScreentoneSettings.ScratchHashSum],
            hashed[MangaScreentoneSettings.ScratchHashMix],
            width,
            height,
            derived.LevelBlack,
            derived.LevelWhite,
            derived.Gamma);
        if (_analysisKey != analysisKey)
        {
            _ = _host.RecordAnalysis(width, height, in derived);
            _analysisKey = analysisKey;
            _analysisVersion++;
            AnalysisRuns++;
        }

        var toneKey = new ToneKey(_analysisVersion, derived.White, derived.Black);
        if (_toneKey != toneKey)
        {
            _ = _host.RecordTonePlan(width, height, in derived);
            _toneKey = toneKey;
            _toneVersion++;
            ToneRuns++;
        }

        if (derived.WhiteLines && _cutVersion != _analysisVersion)
        {
            _ = _host.RecordCut(width, height, in derived);
            _cutVersion = _analysisVersion;
            CutRuns++;
        }

        var printKey = new PrintKey(
            _toneVersion,
            derived.WhiteLines ? _cutVersion : -1,
            derived.Pitch,
            derived.LightTone,
            derived.DarkTone,
            derived.Gradation,
            derived.WhiteLines);
        if (_printKey != printKey)
        {
            _ = _host.RecordPrint(width, height, in derived);
            _printKey = printKey;
            _printVersion++;
            PrintRuns++;
        }

        panelKey = new PanelKey(
            _printVersion,
            width,
            height,
            derived.MarginPixels,
            derived.BorderPixels,
            derived.Paper,
            derived.InkStrength);
        return outputFresh || _panelKey != panelKey;
    }

    private void CompletePanel(PanelKey panelKey)
    {
        _panelKey = panelKey;
        PanelRuns++;
    }

    private bool EnsureGrid(int width, int height)
    {
        var length = checked(width * height);
        if (!_host.TryEnsureGrid(MangaScreentoneResources.PerPixel(length), out var changed))
            return false;

        if (changed || _gridLength != length)
            InvalidateKeys();
        _gridLength = length;
        return true;
    }

    private void InvalidateKeys()
    {
        _analysisKey = null;
        _toneKey = null;
        _printKey = null;
        _panelKey = null;
        _cutVersion = -1;
    }

    private void EnsurePackedTextures(int width, int height, int canvasWidth, int canvasHeight)
    {
        if (_packedSourceWidth != width || _packedSourceHeight != height)
        {
            _packedSource?.Dispose();
            _packedSource = _device.AllocateReadWriteTexture2D<Bgra32, Float4>(width, height);
            _packedSourceWidth = width;
            _packedSourceHeight = height;
        }
        if (_packedOutputWidth != canvasWidth || _packedOutputHeight != canvasHeight)
        {
            _packedOutput?.Dispose();
            _packedOutput = _device.AllocateReadWriteTexture2D<Bgra32, Float4>(canvasWidth, canvasHeight);
            _packedOutputWidth = canvasWidth;
            _packedOutputHeight = canvasHeight;
        }
    }

    public void Dispose()
    {
        _host.Dispose();
        _host.WaitForDisposal();
        _packedSource?.Dispose();
        _packedOutput?.Dispose();
        _packedSource = null;
        _packedOutput = null;
        _packedSourceWidth = 0;
        _packedSourceHeight = 0;
        _packedOutputWidth = 0;
        _packedOutputHeight = 0;
        _scratchReadBack.Dispose();
        _scratch.Dispose();
    }

    internal enum RenderOutcome
    {
        Failed,
        Unchanged,
        Rendered,
    }

    private readonly record struct AnalysisKey(
        int HashSum,
        int HashMix,
        int Width,
        int Height,
        float LevelBlack,
        float LevelWhite,
        float Gamma);

    private readonly record struct ToneKey(
        int AnalysisVersion,
        float White,
        float Black);

    private readonly record struct PrintKey(
        int ToneVersion,
        int CutVersion,
        float Pitch,
        float LightTone,
        float DarkTone,
        float Gradation,
        bool WhiteLines);

    private readonly record struct PanelKey(
        int PrintVersion,
        int Width,
        int Height,
        int Margin,
        int Border,
        MangaScreentonePaper Paper,
        float Ink);
}
