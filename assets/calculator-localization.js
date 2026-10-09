(function (root) {
    'use strict';
    // Display text only. Part names, form values and saved calculation data remain canonical.
    var ru = {
        '3DMark Score': 'Балл 3DMark', '3DMark Score Calculator': 'Расчёт 3DMark',
        'Build Maker': 'Подбор сборки', 'Build Upgrader': 'Подбор апгрейда',
        'Part Replacer': 'Замена деталей', 'History & Saves': 'История и сохранения',
        '3DMark Score Calculator History': 'История расчётов 3DMark', 'Build Maker Saves': 'Сохранённые сборки',
        'Include Hardware Expansion Mod (HEM) parts?': 'Учитывать детали Hardware Expansion Mod (HEM)?',
        'Include parts not in the Shop?': 'Учитывать детали вне магазина?', 'All Parts Unlocked?': 'Все детали открыты?',
        'Career Level:': 'Уровень карьеры:', '% Through:': '% опыта:',
        'CPU (Processor)': 'Процессор (CPU)', 'RAM Speed': 'Частота ОЗУ', 'RAM Channels': 'Каналы ОЗУ',
        'RAM Sticks': 'Модули ОЗУ', 'GPU (Graphics Card)': 'Видеокарта (GPU)',
        'GPU (Graphics Card) 1': 'Видеокарта (GPU) 1', 'GPU (Graphics Card) 2': 'Видеокарта (GPU) 2',
        '# GPUs': 'Кол-во видеокарт', 'Calculate': 'Рассчитать', 'CPU Score': 'Балл CPU', 'GPU Score': 'Балл GPU',
        'GPU Type': 'Охлаждение GPU', 'System Watts': 'Мощность системы, Вт',
        'CPU Price New': 'Цена нового CPU', 'CPU Price Used': 'Цена б/у CPU',
        'GPU(s) Price New': 'Цена новых GPU', 'GPU(s) Price Used': 'Цена б/у GPU',
        'Requirements:': 'Требования:', 'Budget': 'Бюджет', 'Reserved Budget': 'Резерв бюджета',
        'Target Score': 'Целевой балл', 'Score Offset': 'Запас по баллам', '# Results': 'Вариантов',
        'Options:': 'Параметры:', 'Original System:': 'Исходный ПК:',
        'Include Motherboard': 'Учитывать материнскую плату', 'Need CPU Overclock': 'Нужен разгон CPU',
        'CPU Socket': 'Сокет CPU', 'Include Case': 'Учитывать корпус', 'Include RAM Part': 'Учитывать модули ОЗУ',
        'Get Builds': 'Подобрать сборки', 'Get Upgrades': 'Подобрать апгрейды', 'Get Parts': 'Подобрать детали',
        'Case': 'Корпус', 'Motherboard': 'Материнская плата', 'RAM': 'ОЗУ', 'Speed': 'Частота',
        'Type': 'Охлаждение', 'Price': 'Цена', 'Remaining': 'Остаток', 'Score': 'Балл', 'Watts': 'Вт',
        'Save?': 'Сохранить?', 'Save': 'Сохранить', 'Cancel': 'Отмена', 'Comment': 'Комментарий',
        'Order': 'Заказать', 'Adding...': 'Добавляю...', 'In cart': 'В корзине',
        'Partly in cart': 'Частично в корзине', 'No parts were added.': 'Детали не добавлены.',
        'Order these parts manually:': 'Добавьте эти детали вручную:',
        'model not found': 'модель не найдена', 'model is ambiguous': 'название модели неоднозначно',
        'already owned or in cart': 'уже куплено или есть в корзине', 'unavailable in the shop': 'недоступно в магазине',
        'could not add': 'не удалось добавить',
        'Parts added to cart:': 'Деталей добавлено в корзину:',
        'The game cart is not connected.': 'Нет связи с корзиной игры.',
        'Could not read the build parts.': 'Не удалось прочитать детали сборки.',
        'No cart confirmation received. Try again.': 'Нет подтверждения от корзины. Попробуйте ещё раз.',
        'Could not identify the part:': 'Не удалось определить деталь:',
        'Load the workshop to order a build.': 'Загрузите мастерскую, чтобы заказать сборку.',
        'Some build parts are unavailable. The cart was not changed.': 'Некоторые детали сборки недоступны. Корзина не изменена.',
        'Request': 'Действие', 'Replace / Fix': 'Заменить / починить', 'Upgrade': 'Улучшить', 'Add Dual-GPU': 'Добавить вторую GPU',
        'Any': 'Любой', 'Air': 'Воздушное', 'Water': 'Водяное',
        '1: Single-channel': '1: Один канал', '2: Dual-channel': '2: Два канала',
        '3: Triple-channel': '3: Три канала', '4: Quad-channel': '4: Четыре канала',
        '1: Single-GPU': '1: Одна видеокарта', '2: Dual-GPUs': '2: Две видеокарты',
        'CPU Brand': 'Производитель CPU', 'GPU Vendor / Brand': 'Производитель / марка GPU',
        'Required CPU': 'Обязательный CPU', 'Required GPU': 'Обязательная GPU',
        'Required Case': 'Обязательный корпус', 'Required Motherboard': 'Обязательная плата', 'Required RAM': 'Обязательная ОЗУ',
        'Total amount you can spend.': 'Общая сумма на покупку деталей.',
        'Amount you want to reserve for other parts, like: case, fans, storage, PSU, etc.': 'Резерв на другие детали: корпус, вентиляторы, накопители, БП и т. д.',
        'Target 3DMark score.': 'Требуемый результат 3DMark.',
        '3DMark score offset, to allow results that have higher 3DMark scores.': 'Допустимое превышение целевого результата 3DMark.',
        '# of results to return.': 'Количество вариантов в результатах.',
        '# of GPUs allowed for each result.': 'Допустимое количество видеокарт в каждом варианте.',
        'If the Original System has an M.2, enter it here to ensure compatibility.': 'Укажите установленный M.2 для проверки совместимости.',
        'Include the Case in the result set.': 'Подбирать корпус вместе с остальными деталями.',
        'Include the Motherboard in the result set.': 'Подбирать материнскую плату вместе с остальными деталями.',
        'Include the RAM part in the result set.': 'Подбирать модули ОЗУ вместе с остальными деталями.',
        'Only list CPUs and Motherboards that can be used for overclocking.': 'Подбирать только CPU и платы с поддержкой разгона.',
        'Limit to a certain CPU Socket. Useful for targeting Coffee Lake CPUs for higher clocks.': 'Ограничить сокет CPU. Например, для подбора Coffee Lake с более высокой частотой.',
        'Limit to a certain GPU Type.': 'Ограничить тип охлаждения видеокарты.',
        'Determine which parts are available. Levels are only applicable to Career mode. ESports DLC and IT Expansion have all parts unlocked.': 'Уровень ограничивает детали только в карьере. В Esports DLC и IT Expansion все детали открыты.',
        'Career Level from the current game': 'Уровень карьеры из текущей игры',
        'Experience progress from the current game': 'Прогресс опыта из текущей игры',
        'Career levels do not restrict this game mode': 'В этом режиме нет ограничения по уровню',
        'Not applicable in this game mode': 'Не применяется в этом режиме игры',
        'Select "All Parts Unlocked", or set a Level and % Through.': 'Включите «Все детали открыты» или укажите уровень и процент опыта.',
        'Budget is required.': 'Укажите бюджет.', 'Score is required.': 'Укажите целевой балл.',
        'CPU not found.': 'Процессор не найден.', 'GPU not found.': 'Видеокарта не найдена.',
        'RAM Sticks is required.': 'Укажите количество модулей ОЗУ.',
        'CPU does not support GPU.': 'Процессор несовместим с видеокартой.',
        'Selected GPU does not support multi-GPU.': 'Выбранная видеокарта не поддерживает несколько GPU.',
        'Selected CPU and Motherboard are incompatible.': 'Выбранные процессор и материнская плата несовместимы.',
        'Motherboard does not support this Multi-GPU type.': 'Материнская плата не поддерживает этот режим нескольких GPU.',
        'Motherboard does not support this many RAM Sticks.': 'На материнской плате недостаточно слотов ОЗУ.',
        'Case not found.': 'Корпус не найден.', 'Case does not support motherboard.': 'Материнская плата не подходит к корпусу.',
        'Case does not support GPU(s).': 'Видеокарта или видеокарты не помещаются в корпус.',
        'M.2 not found.': 'Накопитель M.2 не найден.', 'Motherboard does not support this M.2.': 'Материнская плата не поддерживает этот M.2.',
        'No builds found.': 'Подходящих сборок не найдено.', 'No upgrades found.': 'Подходящих апгрейдов не найдено.',
        'No replacements found.': 'Подходящих замен не найдено.',
        'No upgrade is needed. However, the calculator will still list results for part replacements.': 'Апгрейд не требуется. Калькулятор всё равно покажет варианты замены деталей.',
        'GPU 1 not found.': 'Видеокарта 1 не найдена.', 'GPU 2 not found.': 'Видеокарта 2 не найдена.',
        'At least 1 GPU is required.': 'Укажите хотя бы одну видеокарту.',
        'CPU does not support GPU 1.': 'Процессор несовместим с видеокартой 1.',
        'CPU does not support GPU 2.': 'Процессор несовместим с видеокартой 2.',
        'GPU chipsets must be the same.': 'Видеокарты должны иметь одинаковый чипсет.',
        'GPU 1 does not support Multi-GPU.': 'Видеокарта 1 не поддерживает несколько GPU.',
        'GPU 2 does not support Multi-GPU.': 'Видеокарта 2 не поддерживает несколько GPU.',
        'GPU 2 must be blank to use the request to Add Dual-GPU.': 'Для добавления второй GPU оставьте поле «Видеокарта 2» пустым.',
        'Case does not support GPU 1.': 'Видеокарта 1 не помещается в корпус.',
        'Case does not support GPU 2.': 'Видеокарта 2 не помещается в корпус.',
        'Case does not support these GPUs.': 'Эти видеокарты не помещаются в корпус.',
        'At least 1 part must have a request.': 'Выберите действие хотя бы для одной детали.',
        'Job objectives and calculation limits': 'Условия задания и ограничения расчёта',
        'All original hardware': 'Все исходные комплектующие',
        'The current PC is unavailable; enter its hardware manually.': 'Исходный ПК сейчас недоступен: заполните его характеристики вручную.',
        'Different GPU models: Build Upgrader models identical cards only. Review the original GPUs manually.': 'Две разные модели GPU: подбор апгрейда учитывает только одинаковые карты. Проверьте исходные GPU вручную.',
        'Several M.2 drives are installed; the calculator checks the first. Review compatibility of the others.': 'В ПК несколько M.2: калькулятор учитывает первый, совместимость остальных проверьте отдельно.',
        'RAM Speed uses the current BIOS. The calculator has its own XMP model; overclocked estimates can differ from the game.': 'Частота ОЗУ перенесена из текущего BIOS. Калькулятор использует собственную модель XMP; расчёт после разгона может отличаться от игры.',
        'Job data imported. Run Get Builds / Get Upgrades when ready. The calculator selects CPU/GPU/motherboard combinations; repair, storage, cooling and overclock objectives still need your review. All objectives are listed below.': 'Данные задания перенесены. Нажмите «Подобрать сборки» или «Подобрать апгрейды». Калькулятор подбирает CPU, GPU и плату; условия по ремонту, накопителям, охлаждению и разгону проверьте отдельно. Все цели перечислены ниже.',
        "Reserved Budget = 400 is the calculator's default allowance for PSU, cooling and storage. Adjust it for your parts.": 'Резерв бюджета = 400: стандартная сумма на БП, охлаждение и накопители. Измените её под выбранные детали.',
        'Results failing additional brand conditions were hidden. Increase # Results if needed.': 'Скрыты варианты, не проходящие дополнительные условия по марке. При необходимости увеличьте число вариантов.'
    };
    // Old job metadata may have been imported and saved in either language.
    var aliases = {
        'Две разные модели GPU: Build Upgrader умеет считать только одинаковые карты. Проверьте исходные GPU вручную.': 'Different GPU models: Build Upgrader models identical cards only. Review the original GPUs manually.',
        'RAM Speed перенесён из текущего BIOS. Калькулятор использует собственную модель XMP; расчёт после разгона может отличаться от игры.': 'RAM Speed uses the current BIOS. The calculator has its own XMP model; overclocked estimates can differ from the game.',
        'Перенесены данные задания. Нажмите Get Builds / Get Upgrades, когда будете готовы. Калькулятор подбирает CPU/GPU/плату, но не выполняет задания по ремонту, накопителям, охлаждению или разгону — все условия перечислены ниже.': 'Job data imported. Run Get Builds / Get Upgrades when ready. The calculator selects CPU/GPU/motherboard combinations; repair, storage, cooling and overclock objectives still need your review. All objectives are listed below.',
        'Reserved Budget = 400 — стандартный резерв калькулятора на БП, охлаждение и накопители. Измените его под выбранные детали.': "Reserved Budget = 400 is the calculator's default allowance for PSU, cooling and storage. Adjust it for your parts.",
        'Скрыты варианты, не проходящие дополнительные условия по марке. При необходимости увеличьте # Results.': 'Results failing additional brand conditions were hidden. Increase # Results if needed.'
    };
    Object.keys(ru).forEach(function (key) { aliases[ru[key]] = key; });
    function has(object, key) { return Object.prototype.hasOwnProperty.call(object, key); }
    function language(code) { return /^ru(?:[-_]|$)/i.test(code || '') ? 'ru' : 'en'; }
    function text(value, code) {
        value = String(value == null ? '' : value);
        var key = value.trim(), canonical = has(aliases, key) ? aliases[key] : key;
        var translated = language(code) === 'ru' && has(ru, canonical) ? ru[canonical] : canonical;
        var channels = /^CPU only supports (\d+) RAM Channels\.$/.exec(canonical);
        if (channels && language(code) === 'ru') {
            var n = Number(channels[1]), last = n % 10, teen = n % 100 >= 11 && n % 100 <= 14;
            var word = !teen && last === 1 ? 'канал' : !teen && last >= 2 && last <= 4 ? 'канала' : 'каналов';
            translated = 'Процессор поддерживает только ' + channels[1] + ' ' + word + ' ОЗУ.';
        }
        return value.replace(key, translated);
    }
    var api = { language: language, text: text, ru: ru };
    if (typeof module !== 'undefined' && module.exports) { module.exports = api; return; }
    root.pcbsLanguage = language(root.pcbsInitialLanguage);
    root.pcbsTranslate = function (value) { return text(value, root.pcbsLanguage); };
    var observer;
    function resultCell(element) {
        var cell = element.closest('td'), table = cell && cell.closest('table');
        if (cell && cell.id === 'td3DMarkGpuType') return cell;
        return cell && table && /(?:Results|History|Saves)$/.test(table.id) && cell.parentNode.rowIndex > 0 ? cell : null;
    }
    function translateElement(element) {
        if (/^(SCRIPT|STYLE|TEXTAREA)$/.test(element.tagName) || element.closest('.pcbs-job-review')) return;
        var cell = resultCell(element);
        if (cell && element.tagName !== 'BUTTON' && !element.closest('button')) {
            // CSS provides the translated cooling label while innerHTML/textContent
            // stay Air/Water for the upstream save and calculation code.
            if (cell.id === 'td3DMarkGpuType' || /^tdGpu(?:\d)?Type$/.test(cell.className)) {
                var label = root.pcbsTranslate(cell.textContent);
                if (label !== cell.textContent) cell.setAttribute('data-pcbs-display', label);
                else cell.removeAttribute('data-pcbs-display');
            }
            if (element === cell) Array.prototype.forEach.call(cell.querySelectorAll('button'), translateElement);
            return;
        }
        ['title', 'placeholder'].forEach(function (name) {
            if (!element.hasAttribute(name)) return;
            var old = element.getAttribute(name), next = root.pcbsTranslate(old);
            if (old !== next) element.setAttribute(name, next);
        });
        if (element.tagName === 'OPTION' && !element.hasAttribute('value')) element.setAttribute('value', element.value);
        if (element.tagName === 'OPTION' && element.parentNode.tagName === 'SELECT') {
            var name = element.parentNode.name;
            if (/GpuCount$/.test(name) && /^(1|2)$/.test(element.value)) {
                if (element.textContent !== element.value) element.textContent = element.value;
                return;
            }
            if (/Gpu(?:Count|Type)$/.test(name) && element.value === 'Any') {
                var any = root.pcbsLanguage === 'ru' ? 'Любое' : 'Any';
                if (element.textContent !== any) element.textContent = any;
                return;
            }
        }
        Array.prototype.forEach.call(element.childNodes, function (node) {
            if (node.nodeType === 3) {
                // Both locales use the same renamed target-score label.
                var original = node.nodeValue.replace(/Target Score|Целевой балл/g, '3DMark Score');
                var next = root.pcbsTranslate(original);
                if (next !== node.nodeValue) node.nodeValue = next;
            } else if (node.nodeType === 1) translateElement(node);
        });
    }
    function refresh() {
        if (!document.body) return;
        if (observer) observer.disconnect();
        translateElement(document.body);
        document.documentElement.lang = root.pcbsLanguage;
        document.title = root.pcbsLanguage === 'ru' ? 'PCBS Helper — Помощник по сборке' : 'PCBS Helper — Build Helper';
        if (observer) observer.observe(document.body, { childList: true, subtree: true, characterData: true,
            attributes: true, attributeFilter: ['title', 'placeholder'] });
    }
    root.pcbsLocalize = refresh;
    root.pcbsSetLanguage = function (code) {
        root.pcbsLanguage = language(code);
        if (typeof root.pcbsCloseOptions === 'function') root.pcbsCloseOptions();
        if (typeof root.pcbsRestoreJobReview === 'function') root.pcbsRestoreJobReview();
        refresh();
    };
    function setup() {
        var originalAlert = root.alert;
        root.alert = function (message) { return originalAlert.call(root, root.pcbsTranslate(message)); };
        // Keep each public operation synchronous; translation cannot change any values.
        ['CalculatorCalculate', 'BuildMakerGetBuilds', 'BuildUpgraderGetUpgrades', 'PartReplacerGetParts',
            'updateHistory', 'setSelectOptions', 'pcbsImportJob', 'pcbsClearCurrentTab', 'pcbsUpdateCareer'].forEach(function (name) {
            var original = root[name];
            if (typeof original !== 'function') return;
            root[name] = function () {
                if (/Get(?:Builds|Upgrades|Parts)$/.test(name)) root.pcbsApplyCalculatorDefaults();
                try { return original.apply(this, arguments); } finally { refresh(); }
            };
        });
        observer = new MutationObserver(refresh);
        root.pcbsSetLanguage(root.pcbsLanguage);
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', setup);
    else setup();
})(typeof window !== 'undefined' ? window : this);
