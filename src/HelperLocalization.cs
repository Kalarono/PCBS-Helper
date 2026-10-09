using System;
using System.Collections.Generic;

namespace PCBSCalculatorHelper
{
    internal static class HelperLocalization
    {
        internal static bool Russian
        { get { return (I2.Loc.LocalizationManager.CurrentLanguageCode ?? "").StartsWith("ru", StringComparison.OrdinalIgnoreCase); } }
        internal static string Code { get { return Russian ? "ru" : "en"; } }
        internal static string Text(string english)
        { string translated; return Russian && Ru.TryGetValue(english, out translated) ? translated : english; }

        private static readonly Dictionary<string, string> Ru = new Dictionary<string, string> {
            { "Build Helper", "Помощник по сборке" },
            { " / Esc — hide • Offline", " / Esc — скрыть • Офлайн" },
            { "/Esc — close window", "/Esc — закрыть окно" },
            { "Compact", "Свернуть" }, { "Expand", "Развернуть" },
            { "Clear", "Очистить" }, { "Close", "Закрыть" },
            { "Loading the offline calculator...", "Загрузка офлайн-калькулятора…" },
            { "Compact returns to the saved side-panel position", "«Свернуть» возвращает панель на сохранённое место" },
            { "Alt — show cursor", "Alt — показать курсор" }, { "Alt — hide cursor", "Alt — убрать курсор" },
            { "Input protection could not be initialized. See BepInEx/LogOutput.log.", "Не удалось включить защиту ввода. Подробнее: BepInEx/LogOutput.log." },
            { "The calculator browser stopped. Restart the game to retry.", "Браузер калькулятора остановился. Перезапустите игру." },
            { "Could not open the calculator. See BepInEx/LogOutput.log.", "Не удалось открыть калькулятор. Подробнее: BepInEx/LogOutput.log." },
            { "Install All Apps", "Устанавливать все приложения" }, { "Instant 3DMark", "Мгновенный 3DMark" },
            { "Click Purchase", "Покупка по клику" },
            { "Added to cart: ", "Добавлено в корзину: " },
            { "This item is already owned or in the cart.", "Этот товар уже куплен или находится в корзине." },
            { "Load the workshop to add parts to the cart.", "Загрузите мастерскую, чтобы добавлять детали в корзину." },
            { "This part is not available in the shop.", "Эта деталь недоступна в магазине." },
            { "Could not add the part to the cart.", "Не удалось добавить деталь в корзину." },
            { "Show Diagnose Objectives", "Показывать цели диагностики" }, { "Show Hidden Objectives", "Показывать скрытые цели" },
            { "Will it Run? Helper", "Помощник Will it Run?" },
            { "[ON]   ", "[ВКЛ]   " }, { "[OFF]  ", "[ВЫКЛ]  " },
            { " — hide", " — скрыть" }, { " — pin", " — закрепить" },
            { "Choose game", "Выбрать игру" }, { "From job", "Из задания" }, { "Search games", "Поиск игры" },
            { "Point at a job PC or open a job email.", "Наведитесь на ПК с заданием или откройте письмо с заказом." },
            { "This job has no visible Will it Run? requirements.", "В выбранном задании нет открытых требований Will it Run?." },
            { "Load the workshop to read game requirements.", "Загрузите мастерскую, чтобы прочитать требования игры." },
            { "\n\nYou can select a game manually with Choose game.", "\n\nВы можете выбрать игру вручную, нажав «Выбрать игру»." },
            { " • selected game", " • выбранная игра" }, { " • job #", " • задание №" },
            { "Next job objective", "Следующая цель задания" },
            { "RAM", "ОЗУ" }, { "VRAM", "Видеопамять" }, { "GPU", "Видеокарта" },
            { "CPU", "Процессор" }, { "Storage", "Диск" }, { " GB", " ГБ" },
            { "CPU alternative • ", "Аналог CPU • " },
            { "No suitable alternatives at the current level.", "На текущем уровне подходящих аналогов нет." },
            { "No suitable CPUs from the other brand.", "Подходящих CPU другой марки нет." },
            { "At least as fast in Will it Run?.\n", "Не слабее по оценке Will it Run?.\n" },
            { "Unlocked parts only. ", "Только открытые детали. " },
            { "No level restriction. ", "Без ограничения по уровню. " }, { "Check the socket.", "Проверь сокет." },
            { "Recommended", "Рекомендуемые" }, { "Minimum", "Минимальные" }, { "Helper", "Помощник" }
        };
    }
}
