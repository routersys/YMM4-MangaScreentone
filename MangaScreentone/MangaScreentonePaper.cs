using System.ComponentModel.DataAnnotations;

namespace MangaScreentone;

public enum MangaScreentonePaper
{
    [Display(Name = nameof(Texts.PaperWhite), Description = nameof(Texts.PaperWhiteDescription), ResourceType = typeof(Texts))]
    White,
    [Display(Name = nameof(Texts.PaperOffWhite), Description = nameof(Texts.PaperOffWhiteDescription), ResourceType = typeof(Texts))]
    OffWhite,
    [Display(Name = nameof(Texts.PaperCream), Description = nameof(Texts.PaperCreamDescription), ResourceType = typeof(Texts))]
    Cream,
    [Display(Name = nameof(Texts.PaperTransparent), Description = nameof(Texts.PaperTransparentDescription), ResourceType = typeof(Texts))]
    Transparent,
}
