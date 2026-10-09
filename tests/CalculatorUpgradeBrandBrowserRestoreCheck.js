(function () {
    'use strict';
    var checks = 0;
    function check(value, message) { checks++; if (!value) throw new Error(message); }
    pcbsSetLanguage('ru');
    var form = document.getElementById('formBuildUpgrader'), table = document.getElementById('tableBuildUpgraderResults');
    check(JSON.parse(form.pcbsJobMetadata.value).brands[0].brand === 'HyperX', 'The HyperX objective restores with the imported job.');
    check(document.getElementById('divBuildUpgraderJobReview').textContent.indexOf('Условие по марке HyperX') >= 0,
        'The separate-verification explanation restores on reopening.');
    check(table.rows.length > 1 && table.rows[1].cells[0].textContent === 'Intel Pentium G4520', 'Score upgrades persist after reopening.');
    var alerts = [], originalAlert = window.alert;
    window.alert = function (message) { alerts.push(message); };
    try {
        BuildUpgraderGetUpgrades();
        check(alerts.length === 0 && table.rows.length > 1, 'Restored metadata cannot reintroduce the empty-results bug.');
        pcbsClearCurrentTab();
        check(form.pcbsJobMetadata.value === '' && document.getElementById('divBuildUpgraderJobReview').style.display === 'none',
            'Clear removes the job and deferred-constraint explanation.');
        check(pcbsErrors.length === 0, 'Restore and Clear produce no JavaScript errors.');
        return { passed: checks };
    } finally { window.alert = originalAlert; }
})();
