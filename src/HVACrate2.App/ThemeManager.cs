using System.Windows;

namespace HVACrate2.App;

public static class ThemeManager
{
    public static event Action<bool>? ThemeChanged;

    public static bool IsDark { get; private set; }

    public static void SetTheme(bool dark)
    {
        IsDark = dark;
        string source = dark ? "Resources/Theme.Dark.xaml" : "Resources/Theme.Light.xaml";

        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var themeDict = dictionaries.FirstOrDefault(d =>
            d.Source != null && d.Source.OriginalString.Contains("Theme."));

        var newDict = new ResourceDictionary { Source = new Uri(source, UriKind.Relative) };

        if (themeDict != null)
        {
            int index = dictionaries.IndexOf(themeDict);
            dictionaries[index] = newDict;
        }
        else
        {
            dictionaries.Add(newDict);
        }

        ThemeChanged?.Invoke(dark);
    }

    public static void Toggle() => SetTheme(!IsDark);
}
