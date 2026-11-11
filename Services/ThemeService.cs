using System.Windows;

namespace NusaLite.Services;

public static class ThemeService
{
    private const string DefaultTheme = "Graphite";

    private static readonly Dictionary<string, string> Sources = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Void"] = "/NusaLite;component/Themes/Theme.Void.xaml",
        [DefaultTheme] = "/NusaLite;component/Themes/Theme.Graphite.xaml",
        ["Light"] = "/NusaLite;component/Themes/Theme.Light.xaml"
    };

    public static string Current { get; private set; } = DefaultTheme;

    public static event EventHandler? ThemeChanged;

    public static void Apply(string themeName)
    {
        var key = Resolve(themeName) ?? DefaultTheme;
        var source = new Uri(Sources[key], UriKind.Relative);

        var dictionaries = Application.Current?.Resources.MergedDictionaries;
        if (dictionaries is not null)
        {
            Put(dictionaries, new ResourceDictionary { Source = source });
        }

        Current = key;
        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    private static string? Resolve(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        foreach (var key in Sources.Keys)
        {
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            {
                return key;
            }
        }

        return null;
    }

    private static void Put(IList<ResourceDictionary> dictionaries, ResourceDictionary theme)
    {
        for (var i = 0; i < dictionaries.Count; i++)
        {
            var source = dictionaries[i].Source?.OriginalString;
            if (source is not null && source.Contains("Theme.", StringComparison.OrdinalIgnoreCase))
            {
                dictionaries[i] = theme;
                return;
            }
        }

        dictionaries.Insert(0, theme);
    }
}
