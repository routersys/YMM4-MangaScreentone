using System.Numerics;
using ComputeWeave;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;

namespace MangaScreentone.Tests;

[Collection("Direct2D")]
public sealed class MangaScreentoneInteropTests
{
    static MangaScreentoneSettings.Parameters Parameters()
        => new(200f, 0.8f, 0.3f, 0.18f, 0.42f, 1f, true, 0.2f, 0.7f, 1.3f, 5f, 0f, MangaScreentonePaper.White, 0f);

    sealed class Interop : IDisposable
    {
        readonly ComputeExternalQueueScheduler scheduler;
        readonly MangaScreentoneInteropProvider provider;
        readonly ComputeInteropDomain domain;

        public MangaScreentoneResourceSet Resources { get; }

        public MangaScreentonePipeline Pipeline { get; }

        Interop(ComputeExternalQueueScheduler scheduler, MangaScreentoneInteropProvider provider, GraphicsDevice device)
        {
            this.scheduler = scheduler;
            this.provider = provider;
            domain = device.RegisterExternalDomain(provider);
            Resources = MangaScreentoneResourceSet.Create(device, domain);
            Pipeline = MangaScreentonePipeline.TryCreate(device)!;
        }

        public static Interop Create(IGraphicsDevicesAndContext devices)
        {
            var scheduler = ComputeExternalQueueScheduler.Create();
            var provider = MangaScreentoneInteropProvider.TryCreate(devices, scheduler, out var device);
            if (provider is null || device is null)
            {
                scheduler.Dispose();
                Assert.Skip("Direct3D 11 and Direct3D 12 sharing is unavailable.");
            }

            return new Interop(scheduler, provider, device);
        }

        public void Draw(ID2D1Image image)
        {
            var context = provider.RenderContext;
            using var borrow = Resources.BeginSourceExternalOperation();
            var previousTarget = context.Target;
            using var target = new ID2D1Bitmap1(borrow.DangerousGetView().AddRefBitmap());
            context.Target = target;
            context.BeginDraw();
            context.Clear(null);
            context.DrawImage(image, Vector2.Zero, null, InterpolationMode.NearestNeighbor, CompositeMode.SourceCopy);
            context.EndDraw();
            context.Target = previousTarget;
        }

        public Rendering CaptureOutput(IGraphicsDevicesAndContext devices, out int width, out int height)
        {
            using var lease = Resources.AcquireOutputExternalViewLease();
            width = lease.Width;
            height = lease.Height;
            using var bitmap = new ID2D1Bitmap1(lease.DangerousGetView().AddRefBitmap());
            return Rendering.Capture(devices, bitmap);
        }

        public void Dispose()
        {
            Pipeline.Dispose();
            Resources.Dispose();
            Resources.WaitForDisposal();
            domain.Dispose();
            domain.WaitForDisposal();
            provider.Dispose();
            scheduler.Dispose();
        }
    }

    static (int Black, int White) CountInside(Rendering rendering, int width, int height)
    {
        var (black, white) = (0, 0);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pixel = rendering[x, y];
                if (pixel == Bgra.Opaque(0, 0, 0))
                    black++;
                else if (pixel == Bgra.Opaque(255, 255, 255))
                    white++;
            }
        }

        return (black, white);
    }

    [Fact]
    public void ThePageIsDrawnAfterTheOutputGrowsToFullHd()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var interop = Interop.Create(context);
        using var source = new SourceImage(context, 96, 72, (x, y) => Scenes.Pixel(x, y, 96, 72));
        var parameters = Parameters();
        Assert.True(interop.Resources.TryEnsureSource(96, 72, out _));

        void RenderInto(int width, int height)
        {
            interop.Draw(source.Bitmap);
            Assert.True(interop.Resources.TryEnsureOutput(width, height, out _));
            var outcome = interop.Pipeline.Render(
                interop.Resources.GetSourceComputeBinding(), interop.Resources.GetOutputComputeBinding(), 96, 72, in parameters, true);
            Assert.Equal(MangaScreentonePipeline.RenderOutcome.Rendered, outcome);
        }

        RenderInto(96, 72);
        interop.CaptureOutput(context, out var firstWidth, out var firstHeight);
        RenderInto(1920, 1080);
        var output = interop.CaptureOutput(context, out var secondWidth, out var secondHeight);

        Assert.Equal((96, 72), (firstWidth, firstHeight));
        Assert.Equal((1920, 1080), (secondWidth, secondHeight));
        var (black, white) = CountInside(output, 96, 72);
        Assert.True(black > 100 && white > 100 && black + white == 96 * 72, $"{black} {white}");
    }

    [Fact]
    public void ThePageIsDrawnAfterTheSourceIsReplacedByALargerOne()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var interop = Interop.Create(context);
        var parameters = Parameters();
        var previous = 0;

        foreach (var (width, height) in new[] { (96, 72), (160, 120) })
        {
            using var source = new SourceImage(context, width, height, (x, y) => Scenes.Pixel(x, y, width, height));
            Assert.True(interop.Resources.TryEnsureSource(width, height, out var sourceChanged));
            interop.Draw(source.Bitmap);
            Assert.True(interop.Resources.TryEnsureOutput(width, height, out _));
            var outcome = interop.Pipeline.Render(
                interop.Resources.GetSourceComputeBinding(), interop.Resources.GetOutputComputeBinding(), width, height, in parameters, true);
            var output = interop.CaptureOutput(context, out var outputWidth, out var outputHeight);

            Assert.True(sourceChanged);
            Assert.Equal(MangaScreentonePipeline.RenderOutcome.Rendered, outcome);
            Assert.True(outputWidth * outputHeight > previous);
            var (black, white) = CountInside(output, width, height);
            Assert.Equal(width * height, black + white);
            Assert.True(black > 100 && white > 100, $"{black} {white}");
            previous = outputWidth * outputHeight;
        }
    }

    [Fact]
    public void AnUnchangedFrameLeavesTheOutputUntouchedUnlessItIsFresh()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var interop = Interop.Create(context);
        using var source = new SourceImage(context, 96, 72, (x, y) => Scenes.Pixel(x, y, 96, 72));
        var parameters = Parameters();
        Assert.True(interop.Resources.TryEnsureSource(96, 72, out _));
        Assert.True(interop.Resources.TryEnsureOutput(96, 72, out _));
        interop.Draw(source.Bitmap);

        var first = interop.Pipeline.Render(interop.Resources.GetSourceComputeBinding(), interop.Resources.GetOutputComputeBinding(), 96, 72, in parameters, true);
        interop.Draw(source.Bitmap);
        var second = interop.Pipeline.Render(interop.Resources.GetSourceComputeBinding(), interop.Resources.GetOutputComputeBinding(), 96, 72, in parameters, false);
        interop.Draw(source.Bitmap);
        var third = interop.Pipeline.Render(interop.Resources.GetSourceComputeBinding(), interop.Resources.GetOutputComputeBinding(), 96, 72, in parameters, true);

        Assert.Equal(MangaScreentonePipeline.RenderOutcome.Rendered, first);
        Assert.Equal(MangaScreentonePipeline.RenderOutcome.Unchanged, second);
        Assert.Equal(MangaScreentonePipeline.RenderOutcome.Rendered, third);
    }
}
