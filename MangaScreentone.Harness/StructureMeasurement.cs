using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MangaScreentone.Harness;

internal static class StructureMeasurement
{
    const int Warmup = 2;
    const int Repeats = 9;

    public static void Run(HarnessImage image)
    {
        var pipeline = MangaScreentonePipeline.TryCreate() ?? throw new HarnessException("パイプラインを作れません。");
        try
        {
            var source = MemoryMarshal.Cast<byte, int>(image.Pixels).ToArray();
            var baseline = new MangaScreentoneSettings.Parameters(200f, 0.8f, 0.3f, 0.18f, 0.42f, 1f, true, 0.2f, 0.7f, 1.3f, 5f, 0f, MangaScreentonePaper.White, 0.5f);
            var variants = new (string Name, Func<int, MangaScreentoneSettings.Parameters> Make)[]
            {
                ("cached", _ => baseline),
                ("panel", step => baseline with { Ink = step % 2 == 0 ? 0.5f : 0.25f }),
                ("print", step => baseline with { LightTone = step % 2 == 0 ? 0.18f : 0.3f }),
                ("tone", step => baseline with { WhitePoint = step % 2 == 0 ? 0.8f : 0.7f }),
                ("analysis", step => baseline with { Gamma = step % 2 == 0 ? 1.3f : 1.2f }),
                ("analysis-no-white-lines", step => baseline with { Gamma = step % 2 == 0 ? 1.3f : 1.2f, WhiteLines = false }),
            };

            Console.WriteLine($"{image.Width}x{image.Height}");
            foreach (var (name, make) in variants)
            {
                var canvas = MangaScreentonePipeline.GetCanvasSize(image.Width, image.Height, make(0));
                var destination = new int[canvas.Width * canvas.Height];
                for (var step = 0; step < Warmup; step++)
                    Require(pipeline.Process(source, destination, image.Width, image.Height, make(step)));

                var timings = new double[Repeats];
                for (var repeat = 0; repeat < Repeats; repeat++)
                {
                    var parameters = make(repeat + Warmup);
                    var elapsed = Stopwatch.StartNew();
                    Require(pipeline.Process(source, destination, image.Width, image.Height, in parameters));
                    elapsed.Stop();
                    timings[repeat] = elapsed.Elapsed.TotalMilliseconds;
                }

                Array.Sort(timings);
                Console.WriteLine($"{name,-26} min={timings[0],8:F2} median={timings[Repeats / 2],8:F2} ms");
            }
        }
        finally
        {
            pipeline.Dispose();
        }
    }

    static void Require(MangaScreentonePipeline.RenderOutcome outcome)
    {
        if (outcome == MangaScreentonePipeline.RenderOutcome.Failed)
            throw new HarnessException("パイプラインの描画に失敗しました。");
    }
}
