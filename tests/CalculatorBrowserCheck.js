(function () {
    var checks = 0;
    function check(value, text) { checks++; if (!value) throw new Error(text); }
    function field(form, name, value) {
        var node = document.getElementById(form).elements.namedItem(name);
        if (!node) throw new Error('Missing fixture field ' + name);
        node.value = value; node.dispatchEvent(new Event('change', { bubbles: true }));
    }
    check(pcbsErrors.length === 0, 'Fresh initialization must not throw.');
    var levelForm = document.getElementById('formLevel');
    check(levelForm.inputLevel.value === '13' && levelForm.inputLevelPercentThrough.value === '25', 'Native career data initializes the page with whole percent progress.');
    field('form3DMarkScoreCalculator', 'input3DMarkCpu', 'Intel Core i7-7700K');
    field('form3DMarkScoreCalculator', 'input3DMarkGpu', 'EVGA GeForce GTX 1080 GAMING');
    field('formBuildMaker', 'inputBuildMakerBudget', '1500');
    field('formBuildUpgrader', 'inputBuildUpgraderBudget', '2400');
    field('formPartReplacer', 'inputPartReplacerBudget', '900');
    CalculatorCalculate();
    check(document.getElementById('td3DMarkSystemScore').textContent === '6495', 'The original calculator still produces its expected score.');
    pcbsFlush();
    check(pcbsCaptured.tabs.div3DMarkScoreCalculator.texts.td3DMarkSystemScore === '6495', 'Computed results are captured for persistence.');
    showPage('divBuildMaker');
    check(pcbsClearCurrentTab() === true, 'Clear is available on calculation pages.');
    check(document.getElementById('formBuildMaker').inputBuildMakerBudget.value === '', 'Clear erases the active page input.');
    check(document.getElementById('formBuildUpgrader').inputBuildUpgraderBudget.value === '2400' &&
        document.getElementById('formPartReplacer').inputPartReplacerBudget.value === '900', 'Clear preserves other calculation pages.');
    check(document.getElementById('td3DMarkSystemScore').textContent === '6495', 'Clear of Build Maker preserves the 3DMark result.');
    check(levelForm.inputLevel.value === '13', 'Clear preserves career metadata.');
    showPage('divHistoryAndSaves');
    check(pcbsClearEnabled === false && pcbsClearCurrentTab() === false, 'History is not erased by the current-page Clear button.');
    document.querySelector('[name="inputOptionAllPartsUnlocked"]').click();
    pcbsUpdateCareer({ valid: true, applicable: true, level: 14, percent: 51.123456 });
    check(document.querySelector('[name="inputOptionAllPartsUnlocked"]').checked && allPartsUnlocked, 'Career synchronization preserves the user override.');
    check(levelForm.inputLevel.value === '14' && levelForm.inputLevelPercentThrough.value === '51', 'Live career changes update both fields with whole percent progress.');
    check(pcbsErrors.length === 0, 'Calculation and Clear must not introduce browser errors.');
    pcbsFlush();
    sessionStorage.setItem('pcbs-fixture-state', JSON.stringify(pcbsCaptured));
    return { passed: checks, snapshotReadyForReload: true };
})();
