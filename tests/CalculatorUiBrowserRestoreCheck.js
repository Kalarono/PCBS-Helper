(function () {
    var checks = 0;
    function check(value, message) { checks++; if (!value) throw new Error(message); }
    var form = document.getElementById('formBuildUpgrader');
    check(form.inputBuildUpgraderResultsRequested.value === '7', 'A saved explicit result count overrides the default.');
    check(form.pcbsFixedCpu.value === 'Intel Pentium G4520' && form.pcbsFixedCpu.closest('tr').hidden, 'Imported exact parts restore while remaining hidden.');
    BuildUpgraderGetUpgrades();
    check(document.getElementById('tableBuildUpgraderResults').rows.length > 1, 'Restored hidden constraints still allow a valid real upgrade.');
    pcbsClearCurrentTab();
    check(form.inputBuildUpgraderResultsRequested.value === '10' && form.pcbsFixedCpu.value === '' && form.pcbsJobMetadata.value === '', 'Clear resets the result count to ten and removes imported constraints.');
    check(pcbsErrors.length === 0 && pcbsFixtureAlerts.length === 0, 'Reopening and Clear work without errors.');
    return { passed: checks };
})();
