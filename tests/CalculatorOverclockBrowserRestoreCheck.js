(function () {
    var checks = 0;
    function check(value, message) { checks++; if (!value) throw new Error(message); }
    var maker = document.getElementById('formBuildMaker'), upgrade = document.getElementById('formBuildUpgrader'),
        builds = document.getElementById('tableBuildMakerResults'), upgrades = document.getElementById('tableBuildUpgraderResults');
    check(maker.inputBuildMakerTargetCpuMhz.value === '4700' && maker.inputBuildMakerTargetGpuMhz.value === '2071', 'Both frequency goals restore after reload.');
    check(maker.inputBuildMakerTarget3DMarkScore.value === '', 'An OC-only job does not gain a score goal after restoration.');
    check(builds.rows.length > 1 && builds.rows[1].querySelector('.tdCpuMhz').textContent === '4700' && builds.rows[1].querySelector('.tdGpuMhz').textContent === '2071', 'Restored result clocks stay under the correct headers.');
    check(upgrades.rows.length > 1 && upgrades.rows[1].querySelector('.tdPrice').textContent === '0', 'The independent upgrade result survives reload.');
    pcbsSetLanguage('en');
    check(document.getElementById('inputBuildMakerTargetCpuMhz-label').textContent === 'Target CPU Clock (MHz)', 'Changing language translates the new controls.');
    check(maker.inputBuildMakerTargetCpuMhz.value === '4700', 'Changing language preserves MHz input values.');
    showPage('divBuildMaker'); pcbsClearCurrentTab();
    check(maker.inputBuildMakerTargetCpuMhz.value === '' && maker.inputBuildMakerTargetGpuMhz.value === '' && builds.rows.length === 1, 'Clear removes current frequency goals and results.');
    check(upgrade.inputBuildUpgraderTargetGpuMhz.value === '2071' && upgrades.rows.length > 1, 'Clearing one tab leaves the other OC job intact.');
    check(pcbsErrors.length === 0, 'Restoration, localization and clear produce no JavaScript errors.');
    return { passed: checks };
})();
