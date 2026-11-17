using Microsoft.AspNetCore.Razor.TagHelpers;

namespace CookShare.TagHelpers;

// Preserve the existing state classes used by like/save/rating scripts. The
// presentation is a local SVG subset, with no icon font or client-side renderer.
[HtmlTargetElement("i", Attributes = "class")]
public sealed class SocialIconTagHelper : TagHelper
{
    private static readonly Dictionary<string, string> _icons = new(StringComparer.Ordinal)
    {
        ["fa-angle-right"] = "chevron-right",
        ["fa-arrow-down"] = "arrow-down",
        ["fa-arrow-left"] = "arrow-left",
        ["fa-arrow-right"] = "arrow-right",
        ["fa-arrow-up"] = "arrow-up",
        ["fa-bars"] = "menu",
        ["fa-bell"] = "bell",
        ["fa-book-open"] = "book-open",
        ["fa-bookmark"] = "bookmark",
        ["fa-bowl-food"] = "cooking-pot",
        ["fa-bowl-rice"] = "soup",
        ["fa-cake-candles"] = "cake-slice",
        ["fa-calendar"] = "calendar",
        ["fa-camera"] = "camera",
        ["fa-check"] = "check",
        ["fa-clock"] = "clock",
        ["fa-comment"] = "message-circle",
        ["fa-compass"] = "compass",
        ["fa-earth-asia"] = "earth",
        ["fa-flag"] = "flag",
        ["fa-folder"] = "folder",
        ["fa-folder-open"] = "folder-open",
        ["fa-heart"] = "heart",
        ["fa-house"] = "house",
        ["fa-leaf"] = "leaf",
        ["fa-lock"] = "lock",
        ["fa-magnifying-glass"] = "search",
        ["fa-martini-glass-citrus"] = "martini",
        ["fa-minus"] = "minus",
        ["fa-paper-plane"] = "send",
        ["fa-pen"] = "pencil",
        ["fa-pen-to-square"] = "square-pen",
        ["fa-pizza-slice"] = "pizza",
        ["fa-plus"] = "plus",
        ["fa-share-nodes"] = "share-2",
        ["fa-sliders"] = "sliders-horizontal",
        ["fa-star"] = "star",
        ["fa-thumbtack"] = "pin",
        ["fa-trash-can"] = "trash",
        ["fa-user"] = "user",
        ["fa-user-check"] = "user-check",
        ["fa-user-plus"] = "user-plus",
        ["fa-users"] = "users",
        ["fa-utensils"] = "utensils",
        ["fa-wand-magic-sparkles"] = "sparkles"
    };

    public string Class { get; set; } = string.Empty;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        string? name = Class.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(token => _icons.GetValueOrDefault(token)).FirstOrDefault(value => value != null);
        if (name == null) return;
        output.TagName = "svg";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", "icon " + Class);
        output.Attributes.SetAttribute("viewBox", "0 0 24 24");
        output.Attributes.SetAttribute("aria-hidden", "true");
        output.Attributes.SetAttribute("focusable", "false");
        output.Content.SetHtmlContent($"<use href=\"/img/icons.svg#{name}\"></use>");
    }
}
