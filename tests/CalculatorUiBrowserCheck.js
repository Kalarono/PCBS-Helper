(function () {
    'use strict';
    var checks = 0;
    function check(value, message) { checks++; if (!value) throw new Error(message); }
    pcbsSetLanguage('ru');
    var upgrade = document.getElementById('formBuildUpgrader'), maker = document.getElementById('formBuildMaker');
    check(upgrade.inputBuildUpgraderResultsRequested.value === '10' && maker.inputBuildMakerResultsRequested.value === '10', 'Both build searches start with ten requested results.');
    check(upgrade.inputBuildUpgraderResultsRequested.placeholder === '10', 'The result-count hint shows ten.');
    check(upgrade.inputBuildUpgraderTarget3DMarkScore.closest('tr').cells[0].textContent === 'Балл 3DMark', 'The target-score label uses 3DMark.');
    check(upgrade.selectBuildUpgraderOriginalSystemGpuCount.closest('tr').cells[0].textContent.trim() === 'Кол-во видеокарт', 'The original GPU count label is explicit.');
    Array.prototype.forEach.call(document.querySelectorAll('select[name$="GpuCount"]'), function (select) {
        check(Array.prototype.every.call(select.options, function (option) { return !/^(1|2)$/.test(option.value) || option.textContent === option.value; }), 'GPU count choices display only 1 or 2: ' + select.name);
    });
    check(upgrade.selectBuildUpgraderOptionsGpuCount.options[0].textContent === 'Любое' && upgrade.selectBuildUpgraderOptionsGpuType.options[0].textContent === 'Любое', 'Count and cooling selectors use the neutral Any label.');
    check(upgrade.pcbsCpuBrand.options[0].textContent === 'Любой' && upgrade.selectBuildUpgraderOptionsGpuType.value === 'Any', 'Other brand labels and internal Any values stay correct.');
    check(Array.prototype.every.call(document.querySelectorAll('[data-pcbs-required]'), function (row) { return row.hidden && row.getBoundingClientRect().height === 0; }), 'All exact-part controls are hidden from layout.');
    var job = { tab: 'divBuildUpgrader', ru: true, jobId: 410, title: 'Hidden constraints fixture', budget: 1300, target: 4831,
        system: { pcCase: 'MSI MPG Gungnir 110R', cpu: 'Intel Pentium G4400', mobo: 'GIGABYTE GA-B250M-Gaming 5',
            gpus: ['NVIDIA GeForce GTX 1080 Founders Edition'], ramSticks: 2, ramSpeed: 2400, m2s: [] },
        fields: {}, brands: [{ brand: 'HyperX', count: 1, newOnly: true }], requirements: [], notes: [] };
    var table = document.getElementById('tableBuildUpgraderResults');
    pcbsImportJob(job); BuildUpgraderGetUpgrades();
    check(table.rows.length === 11 && table.rows[1].cells[0].textContent === 'Intel Pentium G4520', 'The default real upgrade search returns ten results and preserves the cheapest valid CPU.');
    upgrade.inputBuildUpgraderResultsRequested.value = '';
    BuildUpgraderGetUpgrades();
    check(table.rows.length === 11 && upgrade.inputBuildUpgraderResultsRequested.value === '10', 'An emptied result count falls back to ten instead of the upstream 200.');
    upgrade.inputBuildUpgraderResultsRequested.value = '7'; BuildUpgraderGetUpgrades();
    check(table.rows.length === 8, 'An explicit user result count still applies.');
    job.fields = { fixedCpu: 'Intel Pentium G4520', fixedGpu: job.system.gpus[0], fixedMobo: job.system.mobo };
    pcbsImportJob(job); BuildUpgraderGetUpgrades();
    check(table.rows.length > 1 && Array.prototype.slice.call(table.rows, 1).every(function (row) {
        return row.cells[0].textContent === job.fields.fixedCpu &&
            (!row.cells[3].textContent || row.cells[3].textContent === job.fields.fixedGpu) &&
            (!row.cells[4].textContent || row.cells[4].textContent === job.fields.fixedMobo);
    }), 'Hidden CPU/GPU/motherboard requirements still constrain every Helper result.');
    check(upgrade.pcbsFixedCpu.value === job.fields.fixedCpu && upgrade.pcbsFixedGpu.value === job.fields.fixedGpu && upgrade.pcbsFixedMobo.value === job.fields.fixedMobo, 'Exact imported part values remain available to the calculation.');
    pcbsSetLanguage('en');
    check(upgrade.inputBuildUpgraderTarget3DMarkScore.closest('tr').cells[0].textContent === '3DMark Score' && upgrade.selectBuildUpgraderOriginalSystemGpuCount.options[0].textContent === '1', 'Renamed score and numeric choices also work in English.');
    check(upgrade.selectBuildUpgraderOptionsGpuCount.options[0].textContent === 'Any' && upgrade.selectBuildUpgraderOptionsGpuType.options[0].textContent === 'Any', 'Contextual neutral labels restore to English.');
    upgrade.inputBuildUpgraderResultsRequested.value = '7';
    pcbsFlush(); sessionStorage.setItem('pcbs-fixture-state', JSON.stringify(pcbsCaptured));
    check(pcbsErrors.length === 0 && pcbsFixtureAlerts.length === 0, 'UI refinements introduce no JavaScript errors or calculation alerts.');
    return { passed: checks };
})();
