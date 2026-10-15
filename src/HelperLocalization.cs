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
            { "Automatic thermal paste", "Автоматическая термопаста" },
            { "Shop inventory counters", "Наличие деталей в магазине" },
            { "Part stock counters", "Счётчики деталей" },
            { "Convenience settings", "Настройки удобства" },
            { "ON", "ВКЛ" }, { "OFF", "ВЫКЛ" },
            { "Esc — close", "Esc — закрыть" },
            { "Remove dependencies while holding LMB", "Снимать зависимости при удержании ЛКМ" },
            { "Replace all customizable cables", "Замена всех доступных цветных кабелей" },
            { "Inventory sorting", "Сортировка инвентаря" },
            { "Price: low first", "Цена: сначала дешёвые" },
            { "Price: high first", "Цена: сначала дорогие" },
            { "Arrival: newest first", "По дате: сначала новые" }, { "Arrival: oldest first", "По дате: сначала старые" },
            { "Hold LMB to remove dependencies • release to pause • Esc to cancel", "Удерживайте ЛКМ для снятия зависимостей • отпустите для паузы • Esc — отмена" },
            { "Dependency removal complete.", "Снятие зависимостей завершено." },
            { "A dependency needs manual removal.", "Зависимость требует ручного снятия." },
            { "Dependency removal stopped. See BepInEx/LogOutput.log.", "Автоснятие остановлено. Причина в BepInEx/LogOutput.log." },
            { "Empty the full water loop before removing this dependency.", "Перед снятием этой зависимости нужно слить заполненный водяной контур." },
            { "Customizable cables replaced.", "Доступные цветные кабели заменены." },
            { "Some cables could not be replaced. See the helper log.", "Часть кабелей не удалось заменить. Подробнее в логе хелпера." },
            { "Cable replacement failed. See the helper log.", "Не удалось заменить кабели. Подробнее в логе хелпера." },
            { "Customizable cables already use this style.", "Доступные цветные кабели уже используют этот стиль." },
            { "No connected cables support this style. Fixed cables keep their original appearance.", "Нет подключённых кабелей для замены. Кабели с фиксированным оформлением сохраняют свой вид." },
            { "No PC available for cable replacement.", "Нет ПК для замены кабелей." },
            { "Workbench action stopped. See the helper log.", "Действие помощника остановлено. Подробнее в логе хелпера." },
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
            { "Job clarifications", "Уточнения заказа" },
            { "Could not read the job clarifications. Check the order email.", "Не удалось прочитать уточнения заказа. Проверьте письмо клиента." },
            { "Suitable replacement • ", "Подходящая замена • " },
            { "Brand alternatives • ", "Варианты для бренда • " },
            { "One matching new part is enough; choose one of the alternatives below.", "Достаточно одной новой детали этого бренда. Выберите один из вариантов ниже." },
            { "Required new parts: ", "Новых деталей требуется: " },
            { "Select the combination manually to meet the part count.", "Подберите сочетание деталей вручную с учётом нужного количества." },
            { "Alternatives meet Will it Run? for their category. Check socket, case and power compatibility.", "Варианты соответствуют Will it Run? для своей категории. Проверьте сокет, размеры и мощность БП." },
            { "No suitable parts meet all these requirements.", "Нет подходящих деталей с учётом этих условий." },
            { "Select this part manually to meet all additional job conditions.", "Подберите деталь вручную с учётом остальных условий заказа." },
            { "Meets Will it Run? and the listed part constraints. Check socket, case and power compatibility.", "Соответствует Will it Run? и ограничениям на деталь. Проверьте сокет, размеры и мощность БП." },
            { " VRAM", " видеопамяти" },
            { "No suitable alternatives at the current level.", "На текущем уровне подходящих аналогов нет." },
            { "No suitable CPUs from the other brand.", "Подходящих CPU другой марки нет." },
            { "At least as fast in Will it Run?.\n", "Не слабее по оценке Will it Run?.\n" },
            { "Unlocked parts only. ", "Только открытые детали. " },
            { "No level restriction. ", "Без ограничения по уровню. " }, { "Check the socket.", "Проверь сокет." },
            { "Recommended", "Рекомендуемые" }, { "Minimum", "Минимальные" }, { "Helper", "Помощник" }
        };
    }
}
