using System.Windows;
using Telemetry;

namespace MangaScreentone.Tests;

public sealed class HostIntegrationTests
{
    [Fact]
    public void OutsideAWpfApplicationNoTelemetryIsStartedOrSent()
    {
        Assert.Null(Application.Current);

        MangaScreentoneTelemetry.EnsureStartedOnce();
        MangaScreentoneTelemetry.Report(new InvalidOperationException());

        Assert.Null(ProcessState.Read("DrainClaimed"));
        Assert.Null(ProcessState.Read("SentCount"));
    }

    [Fact]
    public void OutsideAWpfApplicationTheEffectCanStillBeCreated()
    {
        Assert.Null(Application.Current);

        var effect = new MangaScreentoneEffect();

        Assert.Equal(Texts.MangaScreentone, effect.Label);
        Assert.Null(ProcessState.Read("DrainClaimed"));
    }
}
