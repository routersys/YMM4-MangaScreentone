using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace MangaScreentone;

internal sealed class MangaScreentoneCustomEffect(IGraphicsDevicesAndContext devices)
    : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
{
    public float Amount { set => SetValue((int)EffectImpl.Properties.Amount, value); }

    public bool KeepTransparent { set => SetValue((int)EffectImpl.Properties.KeepTransparent, value ? 1f : 0f); }

    [CustomEffect(2)]
    private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
    {
        private ConstantBuffer _cb;

        [CustomEffectProperty(PropertyType.Float, (int)Properties.Amount)]
        public float Amount
        {
            get => _cb.Amount;
            set
            {
                _cb.Amount = Math.Clamp(value, 0f, 1f);
                UpdateConstants();
            }
        }

        [CustomEffectProperty(PropertyType.Float, (int)Properties.KeepTransparent)]
        public float KeepTransparent
        {
            get => _cb.KeepTransparent;
            set
            {
                _cb.KeepTransparent = value > 0.5f ? 1f : 0f;
                UpdateConstants();
            }
        }

        public EffectImpl() : base(ShaderResourceUri.Get("MangaScreentone"))
        {
        }

        protected override void UpdateConstants()
        {
            if (drawInformation is null)
                return;

            try
            {
                drawInformation.SetPixelShaderConstantBuffer(_cb);
            }
            catch (Exception exception)
            {
                MangaScreentoneTelemetry.Report(exception);
                throw;
            }
        }

        public override void MapInputRectsToOutputRect(
            RawRect[] inputRects,
            RawRect[] inputOpaqueSubRects,
            out RawRect outputRect,
            out RawRect outputOpaqueSubRect)
        {
            if (inputRects.Length == 0)
            {
                outputRect = default;
                outputOpaqueSubRect = default;
                return;
            }

            var union = inputRects[0];
            for (var i = 1; i < inputRects.Length; i++)
            {
                var rect = inputRects[i];
                union = new RawRect(
                    Math.Min(union.Left, rect.Left),
                    Math.Min(union.Top, rect.Top),
                    Math.Max(union.Right, rect.Right),
                    Math.Max(union.Bottom, rect.Bottom));
            }
            outputRect = union;
            outputOpaqueSubRect = default;
        }

        public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
        {
            for (var i = 0; i < inputRects.Length; i++)
                inputRects[i] = outputRect;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ConstantBuffer
        {
            public float Amount;
            public float KeepTransparent;
            public float Pad1;
            public float Pad2;
        }

        public enum Properties
        {
            Amount = 0,
            KeepTransparent = 1,
        }
    }
}
