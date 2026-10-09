(function () {
    var checks = 0;
    function check(value, text) { checks++; if (!value) throw new Error(text); }
    check(pcbsErrors.length === 0, 'Restoring saved state must not throw.');
    var score = document.getElementById('form3DMarkScoreCalculator');
    check(score.input3DMarkCpu.value === 'Intel Core i7-7700K' && score.input3DMarkGpu.value === 'EVGA GeForce GTX 1080 GAMING', 'Saved hardware inputs restore.');
    check(document.getElementById('td3DMarkSystemScore').textContent === '6495', 'The saved calculated result restores without recalculation.');
    check(document.getElementById('formBuildMaker').inputBuildMakerBudget.value === '' &&
        document.getElementById('formBuildUpgrader').inputBuildUpgraderBudget.value === '2400' &&
        document.getElementById('formPartReplacer').inputPartReplacerBudget.value === '900', 'All four tabs preserve independent cleared/saved state.');
    var level = document.getElementById('formLevel');
    check(level.inputLevel.value === '13' && level.inputLevelPercentThrough.value === '25', 'Current game data replaces the last session level.');
    check(document.querySelector('[name="inputOptionAllPartsUnlocked"]').checked, 'All Parts Unlocked is remembered across a reload.');
    check(document.getElementById('divHistoryAndSaves').style.display === 'block', 'The active tab restores.');
    check(document.getElementById('table3DMarkScoreCalculatorHistory').rows.length > 1, 'Calculator history restores.');
    document.querySelector('[name="inputOptionAllPartsUnlocked"]').click();
    pcbsUpdateCareer({ valid: true, applicable: false });
    check(!document.querySelector('[name="inputOptionAllPartsUnlocked"]').checked && isLevelSet(), 'DLC/freebuild ignores level without forcing the user checkbox.');
    check(level.inputLevel.value === '' && level.inputLevelPercentThrough.value === '', 'Non-career modes do not show a made-up career level.');
    check(!!data.cpus['Intel Core i7-7700K'], 'Non-career mode exposes higher-level parts with the checkbox still unchecked.');
    return { passed: checks };
})();
