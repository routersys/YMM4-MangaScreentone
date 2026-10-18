namespace MangaScreentone;

internal static class MangaScreentoneSettings
{
    public const double ReferenceWidth = 1024d;
    public const float PrintAngleRadians = 0.7853982f;
    public const int PrintSupersample = 3;
    public const float SolidBlurSigma = 1.5f;
    public const float SolidThreshold = 0.22f;
    public const float LineBlurSigma = 0.7f;
    public const float LineMeanSize = 15f;
    public const float LineContrast = 0.07f;
    public const float LineCeiling = 0.65f;
    public const float StrokeMinLength = 10f;
    public const float StrokeMinArea = 12f;
    public const float StrokeFillRatio = 0.6f;
    public const float StrokeFillBias = 40f;
    public const float StrokeLargeArea = 400f;
    public const float InkMinArea = 6f;
    public const float ValueBlurSigma = 2.5f;
    public const float ValueMinWeight = 1e-3f;
    public const float BilateralWindow = 9f;
    public const float BilateralSigmaColor = 0.10f;
    public const float BilateralSigmaSpace = 9f;
    public const float PlanMedianSize = 11f;
    public const float PlanFillMedianSize = 21f;
    public const float PlanMinRegion = 600f;
    public const float SpreadBlurSigma = 3f;
    public const float SpreadFloor = 0.03f;
    public const float SpreadRange = 0.05f;
    public const float SpreadSmoothSigma = 4f;
    public const float BetaSize = 3f;
    public const float CutMinLength = 40f;
    public const float CutErodeThreshold = 1.6f;
    public const float InkSpreadSigma = 0.6f;
    public const float InkSpreadFloor = 0.35f;
    public const float InkSpreadRange = 0.4f;
    public const float InkMottleDepth = 0.05f;
    public const float InkMottleCoarse = 64f;
    public const float InkMottleFine = 16f;
    public const float InkMottleFineWeight = 0.5f;
    public const float FibreCoarse = 3f;
    public const float FibreFine = 1f;
    public const float FibreFineWeight = 0.6f;
    public const float MinimumToneGap = 0.02f;
    public const float MinimumLevelGap = 1e-3f;
    public const float MinimumScreenLines = 1f;
    public const int MaximumCanvasSize = 8192;
    public const int MaximumMargin = 4096;
    public const int NoiseSeedInk = 1;
    public const int NoiseSeedFibre = 2;
    public const int ScratchLength = 8;
    public const int ScratchHashSum = 0;
    public const int ScratchHashMix = 1;
    public const int MaximumPendingSubmissions = 32;
    public const uint InactiveClass = 0xFFFFFFFFu;
    public const int MaximumUnionIterations = 1 << 20;

    public static readonly (float R, float G, float B) InkColor = (26f / 255f, 25f / 255f, 28f / 255f);

    public static (float R, float G, float B, float Fibre) GetPaper(MangaScreentonePaper paper)
        => paper switch
        {
            MangaScreentonePaper.OffWhite => (251f / 255f, 250f / 255f, 247f / 255f, 1.5f / 255f),
            MangaScreentonePaper.Cream => (246f / 255f, 243f / 255f, 236f / 255f, 3f / 255f),
            _ => (1f, 1f, 1f, 0f),
        };

    public static int OddKernel(double size)
        => (int)size | 1;

    public static int GaussianRadius(double sigma)
        => sigma <= 0d ? 0 : (((int)Math.Round(sigma * 8d + 1d, MidpointRounding.ToEven)) | 1) / 2;

    public static (float White, float Black) NormalizeTonePoints(float white, float black)
    {
        var clampedWhite = Math.Clamp(white, 0.3f, 1f);
        var clampedBlack = Math.Clamp(black, 0f, 0.7f);
        return (Math.Max(clampedWhite, clampedBlack + MinimumToneGap), clampedBlack);
    }

    public static (float Black, float White, float Gamma) NormalizeLevels(float black, float white, float gamma)
    {
        var clampedBlack = Math.Clamp(black, 0f, 0.9f);
        var clampedWhite = Math.Clamp(white, 0.1f, 1f);
        return (clampedBlack, Math.Max(clampedWhite, clampedBlack + MinimumLevelGap), Math.Clamp(gamma, 0.3f, 3f));
    }

    public static Derived Derive(int width, int height, in Parameters parameters)
    {
        var k = width / ReferenceWidth;
        var squared = k * k;
        var (white, black) = NormalizeTonePoints(parameters.WhitePoint, parameters.BlackPoint);
        var (levelBlack, levelWhite, gamma) = NormalizeLevels(parameters.BlackLevel, parameters.WhiteLevel, parameters.Gamma);
        var lines = Math.Max(parameters.ScreenLines, MinimumScreenLines);
        var bilateralWindow = OddKernel(BilateralWindow * k);
        var bilateralSpace = BilateralSigmaSpace * k;
        var bilateralColor = (double)BilateralSigmaColor;
        var borderPixels = Math.Max((int)Math.Round(Math.Clamp(parameters.Border, 0f, 40f) * k, MidpointRounding.ToEven), 0);
        var marginPixels = Math.Clamp((int)Math.Round(Math.Clamp(parameters.Margin, 0f, 400f) * k, MidpointRounding.ToEven), 0, MaximumMargin);
        var ink = Math.Clamp(parameters.Ink, 0f, 1f);
        var inkSigma = InkSpreadSigma * k * ink;
        return new Derived(
            (float)k,
            (float)(SolidBlurSigma * k),
            GaussianRadius(SolidBlurSigma * k),
            (float)(LineBlurSigma * k),
            GaussianRadius(LineBlurSigma * k),
            OddKernel(LineMeanSize * k),
            (float)(StrokeMinLength * k),
            (float)(StrokeMinArea * squared),
            (float)(StrokeFillBias * squared),
            (float)(StrokeLargeArea * squared),
            (float)(InkMinArea * squared),
            (float)(ValueBlurSigma * k),
            GaussianRadius(ValueBlurSigma * k),
            bilateralWindow / 2,
            (float)(-0.5d / (bilateralSpace * bilateralSpace)),
            (float)(-0.5d / (bilateralColor * bilateralColor)),
            OddKernel(PlanMedianSize * k),
            OddKernel(PlanFillMedianSize * k),
            (float)(PlanMinRegion * squared),
            (float)(SpreadBlurSigma * k),
            GaussianRadius(SpreadBlurSigma * k),
            (float)(SpreadSmoothSigma * k),
            GaussianRadius(SpreadSmoothSigma * k),
            OddKernel(BetaSize * k),
            (float)(CutMinLength * k),
            k < CutErodeThreshold ? 2 : 3,
            (float)(width / (double)lines),
            white,
            black,
            levelBlack,
            levelWhite,
            gamma,
            borderPixels,
            marginPixels,
            (float)inkSigma,
            GaussianRadius(inkSigma),
            Math.Max((int)(InkMottleCoarse * k), 1),
            Math.Max((int)(InkMottleFine * k), 1));
    }

    internal readonly record struct Derived(
        float Scale,
        float SolidSigma,
        int SolidRadius,
        float LineSigma,
        int LineRadius,
        int LineMeanSize,
        float StrokeMinLength,
        float StrokeMinArea,
        float StrokeFillBias,
        float StrokeLargeArea,
        float InkMinArea,
        float ValueSigma,
        int ValueRadius,
        int BilateralRadius,
        float BilateralSpaceCoefficient,
        float BilateralColorCoefficient,
        int PlanMedianSize,
        int PlanFillMedianSize,
        float PlanMinRegion,
        float SpreadSigma,
        int SpreadRadius,
        float SmoothSigma,
        int SmoothRadius,
        int BetaSize,
        float CutMinLength,
        int CutErodeSize,
        float Pitch,
        float White,
        float Black,
        float LevelBlack,
        float LevelWhite,
        float Gamma,
        int BorderPixels,
        int MarginPixels,
        float InkSigma,
        int InkRadius,
        int InkMottleCoarse,
        int InkMottleFine);

    internal readonly record struct Parameters(
        float ScreenLines,
        float WhitePoint,
        float BlackPoint,
        float LightTone,
        float DarkTone,
        float Gradation,
        bool WhiteLines,
        float BlackLevel,
        float WhiteLevel,
        float Gamma,
        float Border,
        float Margin,
        MangaScreentonePaper Paper,
        float Ink);
}
