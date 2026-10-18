namespace MangaScreentone;

internal static class ShaderResourceUri
{
    public static Uri Get(string shaderName) => new($"pack://application:,,,/MangaScreentone;component/Resources/Shader/{shaderName}.cso", UriKind.Absolute);
}
