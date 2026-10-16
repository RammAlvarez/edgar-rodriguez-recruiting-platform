using AngleSharp.Dom;
using Ganss.Xss;

namespace Platform.Recruiting.Candidates;

/// <summary>
/// The server side of rich-text notes (DATA_MODEL §4.4b, PLAN D74): only h1 h2 h3 p strong em ol ul li and a[href]; any
/// other tag is unwrapped (its text kept), links accept http, https and mailto and always open in a new tab.
/// </summary>
public static class NoteHtml
{
    private static readonly HtmlSanitizer Sanitizer = Build();

    public static string Sanitize(string html) => Sanitizer.Sanitize(html).Trim();

    /// <summary>True when the sanitized note has no visible text (an empty editor sends <c>&lt;p&gt;&lt;/p&gt;</c>).</summary>
    public static bool IsBlank(string sanitized) =>
        string.IsNullOrWhiteSpace(System.Text.RegularExpressions.Regex.Replace(sanitized, "<[^>]*>", "").Replace("&nbsp;", " "));

    private static HtmlSanitizer Build()
    {
        var sanitizer = new HtmlSanitizer(new HtmlSanitizerOptions
        {
            AllowedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "h1", "h2", "h3", "p", "strong", "em", "ol", "ul", "li", "a" },
            AllowedAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "href" },
            AllowedSchemes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "http", "https", "mailto" },
            UriAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "href" },
            AllowedCssProperties = new HashSet<string>(),
            AllowedCssClasses = new HashSet<string>(),
        })
        {
            KeepChildNodes = true,
        };
        // Children are kept when a tag is unwrapped — except for elements whose content is code, not text.
        sanitizer.RemovingTag += (_, e) =>
        {
            if (e.Tag.LocalName is "script" or "style" or "template" or "noscript" or "iframe" or "object" or "svg" or "math")
                e.Tag.InnerHtml = "";
        };
        sanitizer.PostProcessNode += (_, e) =>
        {
            if (e.Node is IElement { LocalName: "a" } a)
            {
                a.SetAttribute("target", "_blank");
                a.SetAttribute("rel", "noopener");
            }
        };
        return sanitizer;
    }
}
