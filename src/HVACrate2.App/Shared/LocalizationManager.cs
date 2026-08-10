using System.Windows;

namespace HVACrate2.App.Shared;

public static class LocalizationManager
{
    public static event Action<bool>? LanguageChanged;

    public static bool IsBulgarian { get; private set; }

    public static void SetLanguage(bool bulgarian)
    {
        IsBulgarian = bulgarian;
        string source = bulgarian ? "Shared/Strings.Bg.xaml" : "Shared/Strings.En.xaml";

        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var stringsDict = dictionaries.FirstOrDefault(d =>
            d.Source != null && d.Source.OriginalString.Contains("Strings."));

        var newDict = new ResourceDictionary { Source = new Uri(source, UriKind.Relative) };

        if (stringsDict != null)
        {
            int index = dictionaries.IndexOf(stringsDict);
            dictionaries[index] = newDict;
        }
        else
        {
            dictionaries.Add(newDict);
        }

        LanguageChanged?.Invoke(bulgarian);
    }

    public static void Toggle() => SetLanguage(!IsBulgarian);
}
