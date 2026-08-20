using System.Windows;

namespace HVACrate2.App.Shared;

/// <summary>Code-behind lookup for the current language's strings — for cases DynamicResource can't
/// reach (dynamically-built text, MessageBox content, ToString overrides).</summary>
public static class Loc
{
    public static string Get(string key) => (string)Application.Current.FindResource(key);

    public static string Get(string key, params object[] args) => string.Format(Get(key), args);
}
