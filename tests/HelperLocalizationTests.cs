using System;
using PCBSCalculatorHelper;

// Isolated language source; tests do not load or control the running game.
namespace I2.Loc { internal static class LocalizationManager { internal static string CurrentLanguageCode; } }
internal static class HelperLocalizationTests
{
    private static int checks;
    private static void Check(bool value, string message)
    { checks++; if (!value) throw new Exception(message); }
    private static void Main()
    {
        I2.Loc.LocalizationManager.CurrentLanguageCode = "ru-RU";
        Check(HelperLocalization.Code == "ru", "Russian regional codes are supported.");
        Check(HelperLocalization.Text("Build Helper") == "Помощник по сборке", "The build helper title is localized.");
        Check(HelperLocalization.Text("Will it Run? Helper") == "Помощник Will it Run?", "Requirements panel title is localized.");
        Check(HelperLocalization.Text("Install All Apps") == "Устанавливать все приложения", "Tweaker settings are localized.");
        Check(HelperLocalization.Text("Intel Core i7-7700K") == "Intel Core i7-7700K", "Hardware model names stay unchanged.");
        I2.Loc.LocalizationManager.CurrentLanguageCode = "en-US";
        Check(HelperLocalization.Code == "en" && HelperLocalization.Text("Build Helper") == "Build Helper", "Native labels switch live to English.");
        I2.Loc.LocalizationManager.CurrentLanguageCode = "de";
        Check(HelperLocalization.Text("Will it Run? Helper") == "Will it Run? Helper", "Unsupported game languages fall back to English.");
        I2.Loc.LocalizationManager.CurrentLanguageCode = null;
        Check(HelperLocalization.Code == "en", "Language startup tolerates a missing game locale.");
        Console.WriteLine("Native localization: " + checks + " assertions passed.");
    }
}
