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
        Check(HelperLocalization.Text("Job clarifications") == "Уточнения заказа", "The conditional F3 notes title is localized.");
        Check(HelperLocalization.Text("Suitable replacement • ") == "Подходящая замена • ", "CPU/GPU recommendation labels are localized.");
        Check(HelperLocalization.Text("Brand alternatives • ") == "Варианты для бренда • ", "General-brand recommendations have a localized heading.");
        Check(HelperLocalization.Text("One matching new part is enough; choose one of the alternatives below.") ==
            "Достаточно одной новой детали этого бренда. Выберите один из вариантов ниже.", "Brand choices must not imply that both CPU and GPU are required.");
        Check(HelperLocalization.Text("Required new parts: ") == "Новых деталей требуется: ", "Brand counts remain visible in Russian.");
        Check(HelperLocalization.Text("Select the combination manually to meet the part count.") ==
            "Подберите сочетание деталей вручную с учётом нужного количества.", "Multi-part brand goals explain manual combination selection.");
        Check(HelperLocalization.Text("Could not read the job clarifications. Check the order email.") == "Не удалось прочитать уточнения заказа. Проверьте письмо клиента.", "Missing job data must have a readable localized message.");
        Check(HelperLocalization.Text("Install All Apps") == "Устанавливать все приложения", "Tweaker settings are localized.");
        Check(HelperLocalization.Text("A dependency needs manual removal.") == "Зависимость требует ручного снятия.", "Unknown dependencies must not falsely report a water loop.");
        Check(HelperLocalization.Text("Dependency removal stopped. See BepInEx/LogOutput.log.") == "Автоснятие остановлено. Причина в BepInEx/LogOutput.log.", "Stopped queues point to the log containing the visited dependency chain.");
        Check(HelperLocalization.Text("Empty the full water loop before removing this dependency.") == "Перед снятием этой зависимости нужно слить заполненный водяной контур.", "Actual full water loops have a separate localized explanation.");
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
