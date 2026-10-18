using System.ComponentModel.DataAnnotations;
using System.Reflection;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Json;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Project;

namespace MangaScreentone.Tests;

public sealed class MangaScreentoneEffectTests
{
    static PropertyInfo Property(string name) => typeof(MangaScreentoneEffect).GetProperty(name)!;

    static T Attribute<T>(string property) where T : Attribute => Property(property).GetCustomAttribute<T>()!;

    static Animation[] Animations(MangaScreentoneEffect effect)
        => [effect.Amount, effect.ScreenLines, effect.WhitePoint, effect.BlackPoint, effect.LightTone, effect.DarkTone, effect.Gradation, effect.BlackLevel, effect.WhiteLevel, effect.Gamma, effect.Border, effect.Margin, effect.Ink];

    [Theory]
    [InlineData(nameof(MangaScreentoneEffect.Amount), 100d, 0d, 100d)]
    [InlineData(nameof(MangaScreentoneEffect.ScreenLines), 200d, 40d, 600d)]
    [InlineData(nameof(MangaScreentoneEffect.WhitePoint), 80d, 30d, 100d)]
    [InlineData(nameof(MangaScreentoneEffect.BlackPoint), 30d, 0d, 70d)]
    [InlineData(nameof(MangaScreentoneEffect.LightTone), 18d, 2d, 90d)]
    [InlineData(nameof(MangaScreentoneEffect.DarkTone), 42d, 2d, 95d)]
    [InlineData(nameof(MangaScreentoneEffect.Gradation), 100d, 0d, 100d)]
    [InlineData(nameof(MangaScreentoneEffect.BlackLevel), 20d, 0d, 90d)]
    [InlineData(nameof(MangaScreentoneEffect.WhiteLevel), 70d, 10d, 100d)]
    [InlineData(nameof(MangaScreentoneEffect.Gamma), 1.3d, 0.3d, 3d)]
    [InlineData(nameof(MangaScreentoneEffect.Border), 5d, 0d, 40d)]
    [InlineData(nameof(MangaScreentoneEffect.Margin), 0d, 0d, 400d)]
    [InlineData(nameof(MangaScreentoneEffect.Ink), 50d, 0d, 100d)]
    public void AnimatedParametersStartFromTheirDefaultsWithinTheirRange(string name, double defaultValue, double minimum, double maximum)
    {
        var effect = new MangaScreentoneEffect();

        var animation = (Animation)Property(name).GetValue(effect)!;

        Assert.Equal(defaultValue, animation.DefaultValue);
        Assert.Equal(minimum, animation.MinValue);
        Assert.Equal(maximum, animation.MaxValue);
        Assert.Equal(defaultValue, animation.GetValue(0, 1, EffectDescriptions.Fps));
    }

    [Fact]
    public void WhiteLinesAndPaperStartFromTheirDefaults()
    {
        var effect = new MangaScreentoneEffect();

        Assert.True(effect.WhiteLines);
        Assert.Equal(MangaScreentonePaper.White, effect.Paper);
    }

    [Fact]
    public void ChangingWhiteLinesOrPaperNotifiesTheEditor()
    {
        var effect = new MangaScreentoneEffect();
        var changed = new List<string?>();
        effect.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        effect.WhiteLines = false;
        effect.Paper = MangaScreentonePaper.Cream;

        Assert.Equal([nameof(MangaScreentoneEffect.WhiteLines), nameof(MangaScreentoneEffect.Paper)], changed);
    }

    [Fact]
    public void AssigningAnUnchangedValueDoesNotNotify()
    {
        var effect = new MangaScreentoneEffect();
        var changed = new List<string?>();
        effect.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        effect.WhiteLines = true;
        effect.Paper = MangaScreentonePaper.White;

        Assert.Empty(changed);
    }

    [Fact]
    public void TheLabelIsTheLocalizedEffectName()
    {
        var effect = new MangaScreentoneEffect();

        Assert.Equal(Texts.MangaScreentone, effect.Label);
    }

    [Fact]
    public void TheThirteenNumericParametersReceiveTheAnimationParameters()
    {
        var effect = new MangaScreentoneEffect();

        effect.SetAnimationParameters(120, EffectDescriptions.Fps);

        Assert.Equal(13, Animations(effect).Length);
        Assert.All(Animations(effect), animation => Assert.Equal(120, animation.Length));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void NoExoFilterIsWrittenForAviUtl(int keyFrameIndex)
    {
        var effect = new MangaScreentoneEffect();

        var description = new ExoOutputDescription(new VideoInfo(), string.Empty, new AviUtlDirectories(string.Empty, string.Empty));

        Assert.Empty(effect.CreateExoVideoFilters(keyFrameIndex, description));
    }

    [Fact]
    public void TheEffectIsRegisteredForFilteringAndDecorationWithoutAviUtlSupport()
    {
        var attribute = typeof(MangaScreentoneEffect).GetCustomAttribute<VideoEffectAttribute>()!;

        Assert.Equal(nameof(Texts.MangaScreentone), attribute.Name);
        Assert.Equal([VideoEffectCategories.Filtering, VideoEffectCategories.Decoration], attribute.Categories);
        Assert.Equal([nameof(Texts.TagManga), nameof(Texts.TagMonochrome), nameof(Texts.TagScreentone)], attribute.Keywords);
        Assert.False(attribute.IsAviUtlSupported);
        Assert.True(attribute.IsEffectItemSupported);
        Assert.Equal(typeof(Texts), attribute.ResourceType);
        Assert.Equal(Texts.MangaScreentone, attribute.GetName());
    }

    [Theory]
    [InlineData(nameof(MangaScreentoneEffect.Amount), nameof(Texts.BasicGroup), nameof(Texts.Amount), nameof(Texts.AmountDescription), 0)]
    [InlineData(nameof(MangaScreentoneEffect.ScreenLines), nameof(Texts.BasicGroup), nameof(Texts.ScreenLines), nameof(Texts.ScreenLinesDescription), 1)]
    [InlineData(nameof(MangaScreentoneEffect.WhitePoint), nameof(Texts.ToneGroup), nameof(Texts.WhitePoint), nameof(Texts.WhitePointDescription), 10)]
    [InlineData(nameof(MangaScreentoneEffect.BlackPoint), nameof(Texts.ToneGroup), nameof(Texts.BlackPoint), nameof(Texts.BlackPointDescription), 11)]
    [InlineData(nameof(MangaScreentoneEffect.LightTone), nameof(Texts.ToneGroup), nameof(Texts.LightTone), nameof(Texts.LightToneDescription), 12)]
    [InlineData(nameof(MangaScreentoneEffect.DarkTone), nameof(Texts.ToneGroup), nameof(Texts.DarkTone), nameof(Texts.DarkToneDescription), 13)]
    [InlineData(nameof(MangaScreentoneEffect.Gradation), nameof(Texts.ToneGroup), nameof(Texts.Gradation), nameof(Texts.GradationDescription), 14)]
    [InlineData(nameof(MangaScreentoneEffect.WhiteLines), nameof(Texts.ToneGroup), nameof(Texts.WhiteLines), nameof(Texts.WhiteLinesDescription), 15)]
    [InlineData(nameof(MangaScreentoneEffect.BlackLevel), nameof(Texts.LevelsGroup), nameof(Texts.BlackLevel), nameof(Texts.BlackLevelDescription), 20)]
    [InlineData(nameof(MangaScreentoneEffect.WhiteLevel), nameof(Texts.LevelsGroup), nameof(Texts.WhiteLevel), nameof(Texts.WhiteLevelDescription), 21)]
    [InlineData(nameof(MangaScreentoneEffect.Gamma), nameof(Texts.LevelsGroup), nameof(Texts.Gamma), nameof(Texts.GammaDescription), 22)]
    [InlineData(nameof(MangaScreentoneEffect.Border), nameof(Texts.PageGroup), nameof(Texts.Border), nameof(Texts.BorderDescription), 30)]
    [InlineData(nameof(MangaScreentoneEffect.Margin), nameof(Texts.PageGroup), nameof(Texts.Margin), nameof(Texts.MarginDescription), 31)]
    [InlineData(nameof(MangaScreentoneEffect.Paper), nameof(Texts.PageGroup), nameof(Texts.Paper), nameof(Texts.PaperDescription), 32)]
    [InlineData(nameof(MangaScreentoneEffect.Ink), nameof(Texts.PageGroup), nameof(Texts.Ink), nameof(Texts.InkDescription), 33)]
    public void EveryParameterIsDisplayedInItsGroupInOrder(string property, string group, string name, string description, int order)
    {
        var display = Attribute<DisplayAttribute>(property);

        Assert.Equal(group, display.GroupName);
        Assert.Equal(name, display.Name);
        Assert.Equal(description, display.Description);
        Assert.Equal(order, display.Order);
        Assert.Equal(typeof(Texts), display.ResourceType);
    }

    [Theory]
    [InlineData(nameof(MangaScreentoneEffect.Amount), "F1", "%", 0d, 100d)]
    [InlineData(nameof(MangaScreentoneEffect.ScreenLines), "F0", "", 40d, 600d)]
    [InlineData(nameof(MangaScreentoneEffect.WhitePoint), "F1", "%", 30d, 100d)]
    [InlineData(nameof(MangaScreentoneEffect.BlackPoint), "F1", "%", 0d, 70d)]
    [InlineData(nameof(MangaScreentoneEffect.LightTone), "F1", "%", 2d, 90d)]
    [InlineData(nameof(MangaScreentoneEffect.DarkTone), "F1", "%", 2d, 95d)]
    [InlineData(nameof(MangaScreentoneEffect.Gradation), "F1", "%", 0d, 100d)]
    [InlineData(nameof(MangaScreentoneEffect.BlackLevel), "F1", "%", 0d, 90d)]
    [InlineData(nameof(MangaScreentoneEffect.WhiteLevel), "F1", "%", 10d, 100d)]
    [InlineData(nameof(MangaScreentoneEffect.Gamma), "F2", "", 0.3d, 3d)]
    [InlineData(nameof(MangaScreentoneEffect.Border), "F1", "px", 0d, 40d)]
    [InlineData(nameof(MangaScreentoneEffect.Margin), "F1", "px", 0d, 400d)]
    [InlineData(nameof(MangaScreentoneEffect.Ink), "F1", "%", 0d, 100d)]
    public void AnimatedParametersAreEditedWithAnimationSliders(string property, string format, string unit, double minimum, double maximum)
    {
        var slider = Attribute<AnimationSliderAttribute>(property);

        Assert.Equal(format, slider.StringFormat);
        Assert.Equal(unit, slider.UnitText);
        Assert.Equal(minimum, slider.DefaultMin);
        Assert.Equal(maximum, slider.DefaultMax);
    }

    [Fact]
    public void TheSliderRangeStaysInsideTheAllowedRange()
    {
        var effect = new MangaScreentoneEffect();

        foreach (var property in typeof(MangaScreentoneEffect).GetProperties().Where(property => property.PropertyType == typeof(Animation)))
        {
            var animation = (Animation)property.GetValue(effect)!;
            var slider = property.GetCustomAttribute<AnimationSliderAttribute>()!;

            Assert.True(animation.MinValue <= slider.DefaultMin, property.Name);
            Assert.True(animation.MaxValue >= slider.DefaultMax, property.Name);
            Assert.InRange(animation.DefaultValue, animation.MinValue, animation.MaxValue);
        }
    }

    [Fact]
    public void TheWhiteLinesAreSwitchedWithAToggle()
    {
        Assert.NotNull(Attribute<ToggleSliderAttribute>(nameof(MangaScreentoneEffect.WhiteLines)));
    }

    [Fact]
    public void ThePaperIsChosenFromACombo()
    {
        Assert.NotNull(Attribute<EnumComboBoxAttribute>(nameof(MangaScreentoneEffect.Paper)));
        Assert.Equal(
            [MangaScreentonePaper.White, MangaScreentonePaper.OffWhite, MangaScreentonePaper.Cream, MangaScreentonePaper.Transparent],
            Enum.GetValues<MangaScreentonePaper>());
    }

    [Theory]
    [InlineData(MangaScreentonePaper.White, nameof(Texts.PaperWhite), nameof(Texts.PaperWhiteDescription))]
    [InlineData(MangaScreentonePaper.OffWhite, nameof(Texts.PaperOffWhite), nameof(Texts.PaperOffWhiteDescription))]
    [InlineData(MangaScreentonePaper.Cream, nameof(Texts.PaperCream), nameof(Texts.PaperCreamDescription))]
    [InlineData(MangaScreentonePaper.Transparent, nameof(Texts.PaperTransparent), nameof(Texts.PaperTransparentDescription))]
    public void EveryPaperIsDisplayedWithItsLocalizedName(MangaScreentonePaper paper, string name, string description)
    {
        var display = typeof(MangaScreentonePaper).GetField(paper.ToString())!.GetCustomAttribute<DisplayAttribute>()!;

        Assert.Equal(name, display.Name);
        Assert.Equal(description, display.Description);
        Assert.Equal(typeof(Texts), display.ResourceType);
    }

    [Fact]
    public void EverySettingSurvivesAProjectRoundTrip()
    {
        var effect = new MangaScreentoneEffect { WhiteLines = false, Paper = MangaScreentonePaper.Cream };
        var values = new[] { 55d, 320d, 70d, 40d, 25d, 66d, 15d, 35d, 90d, 2.5d, 12d, 80d, 35d };
        foreach (var (animation, value) in Animations(effect).Zip(values))
            animation.Values[0].Value = value;

        var clone = Json.GetClone(effect)!;

        Assert.NotSame(effect, clone);
        Assert.False(clone.WhiteLines);
        Assert.Equal(MangaScreentonePaper.Cream, clone.Paper);
        Assert.Equal(values, Animations(clone).Select(animation => animation.GetValue(0, 1, EffectDescriptions.Fps)));
    }
}
