(function () {
    var checks = 0;
    function check(value, message) { checks++; if (!value) throw new Error(message); }
    var table = document.getElementById('tableBuildMakerResults');
    check(table.rows.length > 1 && table.rows[1].cells[6].textContent === 'Air', 'Saved results restore with canonical cooling values.');
    pcbsSetLanguage('ru');
    check(table.rows[1].cells[6].getAttribute('data-pcbs-display') === 'Воздушное' && table.rows[1].querySelector('.buttonSave').textContent === 'Сохранить', 'Restored result labels and save buttons display in Russian.');
    check(document.getElementById('tableBuildMakerSaves').rows[1].cells[13].textContent === 'Air', 'User comments survive restoration without translation.');
    pcbsSetLanguage('en');
    check(!table.rows[1].cells[6].hasAttribute('data-pcbs-display'), 'English restores the original result display after reopening.');
    pcbsClearCurrentTab();
    check(table.rows.length === 1 && document.getElementById('formBuildMaker').pcbsJobMetadata.value === '', 'Clear works after localization and restoration.');
    check(pcbsErrors.length === 0, 'Language switching and Clear after restoration produce no JavaScript errors.');
    return { passed: checks };
})();
