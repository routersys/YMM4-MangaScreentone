using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Plugin.Effects;

namespace MangaScreentone;

[PluginDetails(AuthorName = "routersys")]
[VideoEffect(nameof(Texts.MangaScreentone), [VideoEffectCategories.Filtering, VideoEffectCategories.Decoration], [nameof(Texts.TagManga), nameof(Texts.TagMonochrome), nameof(Texts.TagScreentone)], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
public sealed class MangaScreentoneEffect : VideoEffectBase
{
    public override string Label => Texts.MangaScreentone;

    public MangaScreentoneEffect()
    {
        MangaScreentoneTelemetry.EnsureStartedOnce();
        MangaScreentoneUpdateNotifier.EnsureCheckedOnce();
    }

    [Display(GroupName = nameof(Texts.BasicGroup), Name = nameof(Texts.Amount), Description = nameof(Texts.AmountDescription), Order = 0, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", 0, 100)]
    public Animation Amount { get; } = new Animation(100, 0, 100);

    [Display(GroupName = nameof(Texts.BasicGroup), Name = nameof(Texts.ScreenLines), Description = nameof(Texts.ScreenLinesDescription), Order = 1, ResourceType = typeof(Texts))]
    [AnimationSlider("F0", "", 40, 600)]
    public Animation ScreenLines { get; } = new Animation(200, 40, 600);

    [Display(GroupName = nameof(Texts.ToneGroup), Name = nameof(Texts.WhitePoint), Description = nameof(Texts.WhitePointDescription), Order = 10, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", 30, 100)]
    public Animation WhitePoint { get; } = new Animation(80, 30, 100);

    [Display(GroupName = nameof(Texts.ToneGroup), Name = nameof(Texts.BlackPoint), Description = nameof(Texts.BlackPointDescription), Order = 11, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", 0, 70)]
    public Animation BlackPoint { get; } = new Animation(30, 0, 70);

    [Display(GroupName = nameof(Texts.ToneGroup), Name = nameof(Texts.LightTone), Description = nameof(Texts.LightToneDescription), Order = 12, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", 2, 90)]
    public Animation LightTone { get; } = new Animation(18, 2, 90);

    [Display(GroupName = nameof(Texts.ToneGroup), Name = nameof(Texts.DarkTone), Description = nameof(Texts.DarkToneDescription), Order = 13, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", 2, 95)]
    public Animation DarkTone { get; } = new Animation(42, 2, 95);

    [Display(GroupName = nameof(Texts.ToneGroup), Name = nameof(Texts.Gradation), Description = nameof(Texts.GradationDescription), Order = 14, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", 0, 100)]
    public Animation Gradation { get; } = new Animation(100, 0, 100);

    [Display(GroupName = nameof(Texts.ToneGroup), Name = nameof(Texts.WhiteLines), Description = nameof(Texts.WhiteLinesDescription), Order = 15, ResourceType = typeof(Texts))]
    [ToggleSlider]
    public bool WhiteLines { get => _whiteLines; set => Set(ref _whiteLines, value); }
    private bool _whiteLines = true;

    [Display(GroupName = nameof(Texts.LevelsGroup), Name = nameof(Texts.BlackLevel), Description = nameof(Texts.BlackLevelDescription), Order = 20, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", 0, 90)]
    public Animation BlackLevel { get; } = new Animation(20, 0, 90);

    [Display(GroupName = nameof(Texts.LevelsGroup), Name = nameof(Texts.WhiteLevel), Description = nameof(Texts.WhiteLevelDescription), Order = 21, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", 10, 100)]
    public Animation WhiteLevel { get; } = new Animation(70, 10, 100);

    [Display(GroupName = nameof(Texts.LevelsGroup), Name = nameof(Texts.Gamma), Description = nameof(Texts.GammaDescription), Order = 22, ResourceType = typeof(Texts))]
    [AnimationSlider("F2", "", 0.3, 3)]
    public Animation Gamma { get; } = new Animation(1.3, 0.3, 3);

    [Display(GroupName = nameof(Texts.PageGroup), Name = nameof(Texts.Border), Description = nameof(Texts.BorderDescription), Order = 30, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "px", 0, 40)]
    public Animation Border { get; } = new Animation(5, 0, 40);

    [Display(GroupName = nameof(Texts.PageGroup), Name = nameof(Texts.Margin), Description = nameof(Texts.MarginDescription), Order = 31, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "px", 0, 400)]
    public Animation Margin { get; } = new Animation(0, 0, 400);

    [Display(GroupName = nameof(Texts.PageGroup), Name = nameof(Texts.Paper), Description = nameof(Texts.PaperDescription), Order = 32, ResourceType = typeof(Texts))]
    [EnumComboBox]
    public MangaScreentonePaper Paper { get => _paper; set => Set(ref _paper, value); }
    private MangaScreentonePaper _paper = MangaScreentonePaper.White;

    [Display(GroupName = nameof(Texts.PageGroup), Name = nameof(Texts.Ink), Description = nameof(Texts.InkDescription), Order = 33, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", 0, 100)]
    public Animation Ink { get; } = new Animation(50, 0, 100);

    private IAnimatable[]? _animatables;

    public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

    public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
    {
        try
        {
            return new MangaScreentoneEffectProcessor(devices, this);
        }
        catch (Exception exception)
        {
            MangaScreentoneTelemetry.Report(exception);
            throw;
        }
    }

    protected override IEnumerable<IAnimatable> GetAnimatables()
        => _animatables ??= [Amount, ScreenLines, WhitePoint, BlackPoint, LightTone, DarkTone, Gradation, BlackLevel, WhiteLevel, Gamma, Border, Margin, Ink];
}
