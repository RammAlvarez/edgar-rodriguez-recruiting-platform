using PdfSharp.Fonts;

namespace Platform.Infrastructure.Reports;

/// <summary>
/// PDFsharp finds no system fonts on Linux or in a container, so every PDF uses Lato (SIL Open Font License, <c>Fonts/OFL.txt</c>)
/// embedded in this assembly: regular and bold; italic falls back to them. Any family name resolves to Lato.
/// </summary>
internal sealed class EmbeddedFontResolver : IFontResolver
{
    private const string Regular = "Lato-Regular";
    private const string Bold = "Lato-Bold";

    public FontResolverInfo ResolveTypeface(string familyName, bool bold, bool italic) =>
        new(bold ? Bold : Regular, mustSimulateBold: false, mustSimulateItalic: italic);

    public byte[]? GetFont(string faceName)
    {
        using var stream = typeof(EmbeddedFontResolver).Assembly
            .GetManifestResourceStream($"Platform.Infrastructure.Reports.Fonts.{faceName}.ttf");
        if (stream is null)
            return null;
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }
}
