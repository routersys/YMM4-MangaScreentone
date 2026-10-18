using YukkuriMovieMaker.Plugin.Effects;

namespace MangaScreentone.Harness;

internal static class HarnessCases
{
    public static IEnumerable<(string Name, IVideoEffect Effect, IReadOnlyList<int> Frames)> All()
    {
        yield return ("default", Create(), [0]);
        yield return ("default-frames-0-3", Create(), [0, 1, 2, 3]);
        yield return ("plain", Create(effect =>
        {
            effect.Border.Values[0].Value = 0;
            effect.Ink.Values[0].Value = 0;
        }), [0]);
        yield return ("amount-0", Create(effect => effect.Amount.Values[0].Value = 0), [0]);
        yield return ("amount-50", Create(effect => effect.Amount.Values[0].Value = 50), [0]);
        yield return ("screen-lines-100", Create(effect => effect.ScreenLines.Values[0].Value = 100), [0]);
        yield return ("screen-lines-400", Create(effect => effect.ScreenLines.Values[0].Value = 400), [0]);
        yield return ("white-point-50", Create(effect => effect.WhitePoint.Values[0].Value = 50), [0]);
        yield return ("black-point-60", Create(effect => effect.BlackPoint.Values[0].Value = 60), [0]);
        yield return ("light-tone-60", Create(effect => effect.LightTone.Values[0].Value = 60), [0]);
        yield return ("dark-tone-80", Create(effect => effect.DarkTone.Values[0].Value = 80), [0]);
        yield return ("gradation-0", Create(effect => effect.Gradation.Values[0].Value = 0), [0]);
        yield return ("white-lines-off", Create(effect =>
        {
            effect.BlackPoint.Values[0].Value = 60;
            effect.WhiteLines = false;
        }), [0]);
        yield return ("black-level-40", Create(effect => effect.BlackLevel.Values[0].Value = 40), [0]);
        yield return ("white-level-100", Create(effect => effect.WhiteLevel.Values[0].Value = 100), [0]);
        yield return ("gamma-3", Create(effect => effect.Gamma.Values[0].Value = 3), [0]);
        yield return ("border-0", Create(effect => effect.Border.Values[0].Value = 0), [0]);
        yield return ("border-30", Create(effect => effect.Border.Values[0].Value = 30), [0]);
        yield return ("margin-60", Create(effect => effect.Margin.Values[0].Value = 60), [0]);
        yield return ("paper-off-white", Create(effect => effect.Paper = MangaScreentonePaper.OffWhite), [0]);
        yield return ("paper-cream", Create(effect => effect.Paper = MangaScreentonePaper.Cream), [0]);
        yield return ("paper-transparent", Create(effect => effect.Paper = MangaScreentonePaper.Transparent), [0]);
        yield return ("ink-0", Create(effect => effect.Ink.Values[0].Value = 0), [0]);
        yield return ("ink-100", Create(effect => effect.Ink.Values[0].Value = 100), [0]);
        yield return ("keep-transparent", Create(effect => effect.KeepTransparent = true), [0]);
        yield return ("keep-transparent-margin-60", Create(effect =>
        {
            effect.KeepTransparent = true;
            effect.Margin.Values[0].Value = 60;
        }), [0]);
    }

    public static IEnumerable<(string Name, Func<IVideoEffect> Create, Action<IVideoEffect> Change, int Frame)> Transitions()
    {
        yield return ("screen-lines-200-to-100", () => Create(), effect => Of(effect).ScreenLines.Values[0].Value = 100, 0);
        yield return ("white-point-80-to-50", () => Create(), effect => Of(effect).WhitePoint.Values[0].Value = 50, 0);
        yield return ("black-point-30-to-60", () => Create(), effect => Of(effect).BlackPoint.Values[0].Value = 60, 0);
        yield return ("light-tone-18-to-60", () => Create(), effect => Of(effect).LightTone.Values[0].Value = 60, 0);
        yield return ("gradation-100-to-0", () => Create(), effect => Of(effect).Gradation.Values[0].Value = 0, 0);
        yield return ("white-lines-on-to-off", () => Create(effect => effect.BlackPoint.Values[0].Value = 60), effect => Of(effect).WhiteLines = false, 0);
        yield return ("black-level-20-to-40", () => Create(), effect => Of(effect).BlackLevel.Values[0].Value = 40, 0);
        yield return ("gamma-1-3-to-3", () => Create(), effect => Of(effect).Gamma.Values[0].Value = 3, 0);
        yield return ("border-5-to-30", () => Create(), effect => Of(effect).Border.Values[0].Value = 30, 0);
        yield return ("margin-0-to-60", () => Create(), effect => Of(effect).Margin.Values[0].Value = 60, 0);
        yield return ("paper-white-to-cream", () => Create(), effect => Of(effect).Paper = MangaScreentonePaper.Cream, 0);
        yield return ("ink-50-to-0", () => Create(), effect => Of(effect).Ink.Values[0].Value = 0, 0);
        yield return ("amount-100-to-0", () => Create(), effect => Of(effect).Amount.Values[0].Value = 0, 0);
        yield return ("keep-transparent-off-to-on", () => Create(), effect => Of(effect).KeepTransparent = true, 0);
    }

    public static IEnumerable<(string Name, IVideoEffect Effect)> Benchmarks()
    {
        yield return ("default", Create());
        yield return ("white-lines-off", Create(effect => effect.WhiteLines = false));
        yield return ("amount-0", Create(effect => effect.Amount.Values[0].Value = 0));
    }

    public static MangaScreentoneEffect Create(Action<MangaScreentoneEffect>? configure = null)
    {
        var effect = new MangaScreentoneEffect();
        configure?.Invoke(effect);
        return effect;
    }

    static MangaScreentoneEffect Of(IVideoEffect effect) => (MangaScreentoneEffect)effect;
}
