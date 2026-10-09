(function () {
    'use strict';
    var checks = 0;
    function check(value, message) { checks++; if (!value) throw new Error(message); }
    pcbsSetLanguage('ru-RU');
    check(document.documentElement.lang === 'ru' && document.querySelector('.tdNavigationBar').textContent === 'Балл 3DMark', 'Russian locale translates navigation and document language.');
    var score = document.getElementById('form3DMarkScoreCalculator');
    score.input3DMarkCpu.value = 'Intel Core i7-7700K'; score.input3DMarkGpu.value = 'EVGA GeForce GTX 1080 GAMING';
    CalculatorCalculate();
    check(document.getElementById('td3DMarkSystemScore').textContent === '6495', 'Russian display produces the unchanged 3DMark score.');
    var cooling = document.getElementById('td3DMarkGpuType');
    check(cooling.textContent === 'Air' && cooling.getAttribute('data-pcbs-display') === 'Воздушное', 'Cooling is translated visually while the saved value stays Air.');
    check(score.select3DMarkGpuCount.value === '1' && score.select3DMarkGpuCount.options[0].textContent === '1', 'GPU count labels stay numeric without changing option values.');
    var values = Array.prototype.map.call(score.elements, function (field) { return field.value; }).join('|');
    pcbsSetLanguage('en');
    check(document.querySelector('.tdNavigationBar').textContent === '3DMark Score' && !cooling.hasAttribute('data-pcbs-display'), 'Switching to English restores original labels.');
    check(Array.prototype.map.call(score.elements, function (field) { return field.value; }).join('|') === values && document.getElementById('td3DMarkSystemScore').textContent === '6495', 'Language changes preserve fields and calculated results.');
    pcbsSetLanguage('ru');
    window.alert('CPU not found.');
    check(pcbsFixtureAlerts.pop() === 'Процессор не найден.', 'Validation alerts reach the browser in Russian.');
    window.alert('CPU only supports 2 RAM Channels.');
    check(pcbsFixtureAlerts.pop() === 'Процессор поддерживает только 2 канала ОЗУ.', 'Parameterized alerts are localized.');
    var all = document.querySelector('[name="inputOptionAllPartsUnlocked"]'); if (!all.checked) all.click();
    var job = { tab: 'divBuildMaker', ru: false, jobId: 1, title: 'Fixture title', budget: 5000, target: 6400,
        system: {}, requirements: ['A game-provided objective'], brands: [],
        notes: ['Job data imported. Run Get Builds / Get Upgrades when ready. The calculator selects CPU/GPU/motherboard combinations; repair, storage, cooling and overclock objectives still need your review. All objectives are listed below.'],
        fields: { cpuBrand: 'Intel', gpuBrand: 'NVIDIA', gpuCount: '1', gpuType: 'Air',
            fixedCpu: 'Intel Core i7-7700K', fixedGpu: 'EVGA GeForce GTX 1080 GAMING', fixedCase: 'ADATA XPG Battlecruiser (Black)',
            fixedMobo: 'Mortoni H170 Mtech', fixedRam: 'ADATA Premier Series 4 GB 2133 MHz' } };
    pcbsImportJob(job); BuildMakerGetBuilds();
    var form = document.getElementById('formBuildMaker'), table = document.getElementById('tableBuildMakerResults');
    check(table.rows.length > 1 && form.selectBuildMakerGpuType.value === 'Air' && form.pcbsCpuBrand.value === 'Intel', 'Russian build search preserves canonical brand and cooling filters.');
    check(table.rows[1].querySelector('.buttonSave').textContent === 'Сохранить', 'Dynamically generated Save buttons are localized synchronously.');
    check(table.rows[1].cells[6].textContent === 'Air' && table.rows[1].cells[6].getAttribute('data-pcbs-display') === 'Воздушное', 'Result type labels keep canonical innerHTML for the upstream saver.');
    var review = document.getElementById('divBuildMakerJobReview');
    check(review.querySelector('strong').textContent === 'Задание №1: Fixture title' && review.textContent.indexOf('Данные задания перенесены.') >= 0, 'Imported helper prose uses the interface locale, even for an English import.');
    check(review.textContent.indexOf('A game-provided objective') >= 0, 'Game-provided objective text is preserved.');
    table.rows[1].querySelector('.buttonSave').click();
    document.getElementById('formBuildMakerSave').inputBuildMakerSaveFormComment.value = 'Air';
    ProcessBuildMakerSave(true); updateHistory();
    var saved = document.getElementById('tableBuildMakerSaves');
    check(saved.rows[1].cells[6].textContent === 'Air' && saved.rows[1].cells[13].textContent === 'Air', 'Saving in Russian preserves canonical cooling and the user comment.');
    pcbsSetLanguage('en-US');
    check(review.querySelector('strong').textContent === 'Job #1: Fixture title' && review.textContent.indexOf('Job data imported.') >= 0, 'Saved job context switches back to English.');
    check(table.rows[1].querySelector('.buttonSave').textContent === 'Save' && saved.rows[1].cells[13].textContent === 'Air', 'Dynamic buttons switch back and user comments remain untouched.');
    pcbsSetLanguage('ru'); pcbsFlush();
    check(pcbsCaptured.tabs.div3DMarkScoreCalculator.texts.td3DMarkGpuType === 'Air' && pcbsCaptured.tabs.divBuildMaker.rows[0][6].text === 'Air', 'Persistent state remains independent of display language.');
    sessionStorage.setItem('pcbs-fixture-state', JSON.stringify(pcbsCaptured));
    check(pcbsErrors.length === 0, 'Localization and both calculation paths produce no JavaScript errors.');
    return { passed: checks, builds: table.rows.length - 1 };
})();
