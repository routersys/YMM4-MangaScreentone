using System.ComponentModel;
using System.Numerics;
using ComputeWeave;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace MangaScreentone;

internal sealed class MangaScreentoneEffectProcessor : VideoEffectProcessorBase
{
    private readonly IGraphicsDevicesAndContext _devices;
    private readonly MangaScreentoneEffect _item;
    private ComputeExternalQueueScheduler? _scheduler;
    private MangaScreentoneInteropProvider? _interopProvider;
    private ComputeInteropDomain? _interopDomain;
    private MangaScreentoneResourceSet? _resourceSet;
    private ExternalTextureLease<ExternalDirect3D11TextureView>? _outputLease;
    private MangaScreentonePipeline? _pipeline;
    private MangaScreentoneCustomEffect? _effect;
    private Crop? _outputCrop;
    private ID2D1Image? _outputCropOutput;
    private AffineTransform2D? _outputTransform;
    private ID2D1Image? _outputTransformOutput;
    private bool _isFirst = true;
    private bool _hasOutput;
    private bool _hasOutputOffset;
    private bool _hasCropRect;
    private float _amount;
    private Vector2 _outputOffset;
    private Vector4 _cropRect;

    public MangaScreentoneEffectProcessor(IGraphicsDevicesAndContext devices, MangaScreentoneEffect item)
        : base(devices)
    {
        _devices = devices;
        _item = item;
    }

    public override DrawDescription Update(EffectDescription effectDescription)
    {
        try
        {
            return UpdateCore(effectDescription);
        }
        catch (Exception exception)
        {
            MangaScreentoneTelemetry.Report(exception);
            throw;
        }
    }

    private DrawDescription UpdateCore(EffectDescription effectDescription)
    {
        if (IsPassThroughEffect || _effect is null || _outputCrop is null || _outputTransform is null || _outputTransformOutput is null || _resourceSet is null || _interopProvider is null || _pipeline is null || input is null)
            return effectDescription.DrawDescription;

        var frame = effectDescription.ItemPosition.Frame;
        var length = effectDescription.ItemDuration.Frame;
        var fps = effectDescription.FPS;
        var amount = (float)(_item.Amount.GetValue(frame, length, fps) / 100.0);

        if (_isFirst || _amount != amount)
            _effect.Amount = amount;
        _amount = amount;

        if (amount <= 0f)
        {
            _effect.Amount = 0f;
            _isFirst = true;
            return effectDescription.DrawDescription;
        }

        var bounds = _devices.DeviceContext.GetImageLocalBounds(input);
        var widthValue = Math.Ceiling((double)bounds.Right - bounds.Left);
        var heightValue = Math.Ceiling((double)bounds.Bottom - bounds.Top);
        if (!double.IsFinite(widthValue) || !double.IsFinite(heightValue) ||
            !float.IsFinite(bounds.Left) || !float.IsFinite(bounds.Top) ||
            widthValue <= 0d || heightValue <= 0d ||
            widthValue > MangaScreentoneSettings.MaximumCanvasSize || heightValue > MangaScreentoneSettings.MaximumCanvasSize)
        {
            _effect.Amount = 0f;
            _isFirst = true;
            return effectDescription.DrawDescription;
        }

        var itemWidth = (int)widthValue;
        var itemHeight = (int)heightValue;
        var parameters = new MangaScreentoneSettings.Parameters(
            (float)_item.ScreenLines.GetValue(frame, length, fps),
            (float)(_item.WhitePoint.GetValue(frame, length, fps) / 100.0),
            (float)(_item.BlackPoint.GetValue(frame, length, fps) / 100.0),
            (float)(_item.LightTone.GetValue(frame, length, fps) / 100.0),
            (float)(_item.DarkTone.GetValue(frame, length, fps) / 100.0),
            (float)(_item.Gradation.GetValue(frame, length, fps) / 100.0),
            _item.WhiteLines,
            (float)(_item.BlackLevel.GetValue(frame, length, fps) / 100.0),
            (float)(_item.WhiteLevel.GetValue(frame, length, fps) / 100.0),
            (float)_item.Gamma.GetValue(frame, length, fps),
            (float)_item.Border.GetValue(frame, length, fps),
            (float)_item.Margin.GetValue(frame, length, fps),
            _item.Paper,
            (float)(_item.Ink.GetValue(frame, length, fps) / 100.0));

        var (canvasWidth, canvasHeight) = MangaScreentonePipeline.GetCanvasSize(itemWidth, itemHeight, in parameters);
        if (!_resourceSet.TryEnsureSource(itemWidth, itemHeight, out _))
        {
            _effect.Amount = 0f;
            _isFirst = true;
            return effectDescription.DrawDescription;
        }

        RenderInput(new Vortice.RawRectF(bounds.Left, bounds.Top, bounds.Left + itemWidth, bounds.Top + itemHeight));

        if (!OutputCovers(canvasWidth, canvasHeight))
            _outputCrop.SetInput(0, null, true);
        if (!EnsureOutput(canvasWidth, canvasHeight, out var outputChanged))
        {
            _effect.Amount = 0f;
            _isFirst = true;
            return effectDescription.DrawDescription;
        }

        var outcome = _pipeline.Render(
            _resourceSet.GetSourceComputeBinding(),
            _resourceSet.GetOutputComputeBinding(),
            itemWidth,
            itemHeight,
            in parameters,
            outputChanged || !_hasOutput);
        if (outcome == MangaScreentonePipeline.RenderOutcome.Failed)
        {
            _effect.Amount = 0f;
            _isFirst = true;
            return effectDescription.DrawDescription;
        }

        _outputLease ??= _resourceSet.AcquireOutputExternalViewLease();

        if (outputChanged || !_hasOutput)
        {
            using var outputBitmap = new ID2D1Bitmap1(_outputLease.DangerousGetView().AddRefBitmap());
            _outputCrop.SetInput(0, outputBitmap, true);
            _effect.SetInput(1, _outputTransformOutput, true);
        }
        var cropRect = new Vector4(0f, 0f, canvasWidth, canvasHeight);
        if (!_hasCropRect || _cropRect != cropRect)
        {
            _outputCrop.Rectangle = cropRect;
            _cropRect = cropRect;
            _hasCropRect = true;
        }
        var margin = (canvasWidth - itemWidth) / 2;
        var outputOffset = new Vector2(bounds.Left - margin, bounds.Top - margin);
        if (!_hasOutputOffset || _outputOffset != outputOffset)
        {
            _outputTransform.TransformMatrix = Matrix3x2.CreateTranslation(outputOffset);
            _outputOffset = outputOffset;
            _hasOutputOffset = true;
        }
        _hasOutput = true;
        _isFirst = false;
        return effectDescription.DrawDescription;
    }

    private bool OutputCovers(int width, int height)
        => _outputLease is { IsDisposed: false } lease &&
           lease.Width >= width &&
           lease.Height >= height;

    private bool EnsureOutput(int width, int height, out bool changed)
    {
        changed = false;
        if (OutputCovers(width, height))
            return true;

        _outputLease?.Dispose();
        _outputLease = null;
        return _resourceSet!.TryEnsureOutput(width, height, out changed);
    }

    private void RenderInput(Vortice.RawRectF bounds)
    {
        var renderContext = _interopProvider!.RenderContext;
        using var borrow = _resourceSet!.BeginSourceExternalOperation();
        var previousTarget = renderContext.Target;
        using var sourceBitmap = new ID2D1Bitmap1(borrow.DangerousGetView().AddRefBitmap());
        renderContext.Target = sourceBitmap;
        renderContext.BeginDraw();
        renderContext.Clear(null);
        renderContext.DrawImage(
            input,
            new Vector2(-bounds.Left, -bounds.Top),
            null,
            InterpolationMode.NearestNeighbor,
            CompositeMode.SourceCopy);
        renderContext.EndDraw();
        renderContext.Target = previousTarget;
    }

    private void ReleaseInterop()
    {
        _outputLease?.Dispose();
        _outputLease = null;
        _pipeline?.Dispose();
        _pipeline = null;
        _resourceSet?.Dispose();
        _resourceSet?.WaitForDisposal();
        _resourceSet = null;
        _interopDomain?.Dispose();
        _interopDomain?.WaitForDisposal();
        _interopDomain = null;
        _interopProvider?.Dispose();
        _interopProvider = null;
        _scheduler?.Dispose();
        _scheduler = null;
    }

    protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
    {
        var scheduler = ComputeExternalQueueScheduler.Create();
        var interopProvider = MangaScreentoneInteropProvider.TryCreate(devices, scheduler, out var interopDevice);
        if (interopProvider is null || interopDevice is null)
        {
            scheduler.Dispose();
            return null;
        }

        _scheduler = scheduler;

        try
        {
            _interopProvider = interopProvider;
            _interopDomain = interopDevice.RegisterExternalDomain(interopProvider);
            _resourceSet = MangaScreentoneResourceSet.Create(interopDevice, _interopDomain);
            _pipeline = MangaScreentonePipeline.TryCreate(interopDevice);
        }
        catch (Win32Exception)
        {
            ReleaseInterop();
            return null;
        }
        catch
        {
            ReleaseInterop();
            throw;
        }

        if (_pipeline is null)
        {
            ReleaseInterop();
            return null;
        }

        MangaScreentoneCustomEffect? effect = null;
        Crop? outputCrop = null;
        ID2D1Image? outputCropOutput = null;
        AffineTransform2D? outputTransform = null;
        ID2D1Image? outputTransformOutput = null;
        ID2D1Image? output = null;
        try
        {
            effect = new MangaScreentoneCustomEffect(devices);
            if (!effect.IsEnabled)
            {
                effect.Dispose();
                ReleaseInterop();
                return null;
            }
            outputCrop = new Crop(devices.DeviceContext);
            outputCropOutput = outputCrop.Output;
            outputTransform = new AffineTransform2D(devices.DeviceContext)
            {
                BorderMode = BorderMode.Hard,
            };
            outputTransform.SetInput(0, outputCropOutput, true);
            outputTransformOutput = outputTransform.Output;
            output = effect.Output;
            _effect = effect;
            _outputCrop = outputCrop;
            _outputCropOutput = outputCropOutput;
            _outputTransform = outputTransform;
            _outputTransformOutput = outputTransformOutput;
            disposer.Collect(effect);
            disposer.Collect(outputCrop);
            disposer.Collect(outputCropOutput);
            disposer.Collect(outputTransform);
            disposer.Collect(outputTransformOutput);
            disposer.Collect(output);
            return output;
        }
        catch
        {
            output?.Dispose();
            outputTransformOutput?.Dispose();
            outputTransform?.Dispose();
            outputCropOutput?.Dispose();
            outputCrop?.Dispose();
            effect?.Dispose();
            ReleaseInterop();
            throw;
        }
    }

    protected override void setInput(ID2D1Image? inputImage)
    {
        try
        {
            SetInputCore(inputImage);
        }
        catch (Exception exception)
        {
            MangaScreentoneTelemetry.Report(exception);
            throw;
        }
    }

    private void SetInputCore(ID2D1Image? inputImage)
    {
        _effect?.SetInput(0, inputImage, true);
        if (!_hasOutput)
            _effect?.SetInput(1, inputImage, true);
    }

    protected override void ClearEffectChain()
    {
        _effect?.SetInput(0, null, true);
        _effect?.SetInput(1, null, true);
        _outputCrop?.SetInput(0, null, true);
        _isFirst = true;
        _hasOutput = false;
        _hasOutputOffset = false;
        _hasCropRect = false;
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            if (disposing)
            {
                ClearEffectChain();
                ReleaseInterop();
            }
        }
        catch (Exception exception)
        {
            MangaScreentoneTelemetry.Report(exception);
            throw;
        }
        finally
        {
            base.Dispose(disposing);
        }
    }
}
