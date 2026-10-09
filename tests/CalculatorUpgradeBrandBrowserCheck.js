(function () {
    'use strict';
    var checks = 0;
    function check(value, message) { checks++; if (!value) throw new Error(message); }
    var alerts = [], originalAlert = window.alert;
    window.alert = function (message) { alerts.push(message); };
    try {
        pcbsSetLanguage('ru');
        // Regression: the Helper's HyperX objective used to remove every result,
        // although this search changes only the CPU, GPU and motherboard.
        var all = document.querySelector('[name="inputOptionAllPartsUnlocked"]');
        if (all.checked) all.click();
        pcbsUpdateCareer({ valid: true, applicable: true, level: 13, percent: 40 });
        var job = { tab: 'divBuildUpgrader', ru: true, jobId: 410, title: 'Upgrade brand regression', budget: 1300, target: 4831,
            system: { pcCase: 'MSI MPG Gungnir 110R', cpu: 'Intel Pentium G4400', mobo: 'GIGABYTE GA-B250M-Gaming 5',
                gpus: ['NVIDIA GeForce GTX 1080 Founders Edition'], ramSticks: 2, ramSpeed: 2400, m2s: [] },
            fields: { cpuBrand: 'Any', gpuBrand: 'Any', gpuCount: 'Any', gpuType: 'Any' },
            brands: [{ brand: 'HyperX', count: 1, newOnly: true }], requirements: ['Установить комплектующие HyperX'], notes: [] };
        pcbsImportJob(job);
        var form = document.getElementById('formBuildUpgrader'), table = document.getElementById('tableBuildUpgraderResults');
        var before = data;
        check(!allPartsUnlocked && level === 13, 'The calculation keeps the career purchase filter from the reported case.');
        BuildUpgraderGetUpgrades();
        check(data === before, 'The filtered calculation restores the shared catalog.');
        check(alerts.length === 0 && table.rows.length > 1, 'An imported HyperX objective does not erase all score upgrades.');
        var first = Array.prototype.map.call(table.rows[1].cells, function (cell) { return cell.textContent; });
        check(first[0] === 'Intel Pentium G4520' && first[5] === '90' && first[7] === '4831', 'The cheapest upgrade matches the original calculator: G4520, price 90, score 4831.');
        var results = table.rows.length;
        check(document.getElementById('divBuildUpgraderJobReview').textContent.indexOf('Условие по марке HyperX') >= 0,
            'The deferred HyperX objective is visibly explained for separate verification.');
        check(JSON.parse(form.pcbsJobMetadata.value).brands[0].brand === 'HyperX', 'The objective stays in the saved job metadata.');
        form.pcbsJobMetadata.value = '';
        BuildUpgraderGetUpgrades();
        check(table.rows.length === results && table.rows[1].cells[0].textContent === first[0], 'Manual and Helper search return the same number of upgrades and cheapest CPU.');
        job.brands = [{ brand: 'Intel', count: 1, newOnly: true }];
        pcbsImportJob(job); BuildUpgraderGetUpgrades();
        check(alerts.length === 0 && table.rows.length > 1, 'A supported CPU manufacturer objective still finds valid upgrades.');
        check(Array.prototype.slice.call(table.rows, 1).every(function (row) {
            var cpu = sourceData.cpus[row.cells[0].textContent];
            return cpu && cpu.manufacturer === 'Intel';
        }), 'A new Intel requirement excludes GPU-only upgrades that merely retain the original CPU.');
        check(document.getElementById('divBuildUpgraderJobReview').textContent.indexOf('Условие по марке Intel') < 0,
            'Supported manufacturer goals are enforced instead of marked for separate verification.');
        ['AMD', 'NVIDIA', 'MSI', 'GIGABYTE', 'ASUS', 'EVGA'].forEach(function (wanted) {
            check(PCBSJobRules.upgradeBrandRules([{ brand: wanted, count: 1, newOnly: true }], sourceData).applied.length === 1,
                'The full catalog keeps the supported ' + wanted + ' manufacturer goal.');
        });
        job.brands = [];
        ['AMD', 'NVIDIA', 'MSI', 'GIGABYTE', 'ASUS', 'EVGA'].forEach(function (wanted) {
            job.fields.gpuBrand = wanted;
            pcbsImportJob(job); BuildUpgraderGetUpgrades();
            check(alerts.length === 0 && table.rows.length > 1 && Array.prototype.slice.call(table.rows, 1).every(function (row) {
                return PCBSJobRules.brand(sourceData.gpus[row.cells[3].textContent || job.system.gpus[0]], wanted, true);
            }), 'Every upgrade obeys the imported ' + wanted + ' GPU vendor/manufacturer requirement.');
        });
        job.fields.gpuBrand = 'Any';
        ['AMD', 'Intel'].forEach(function (wanted) {
            job.fields.cpuBrand = wanted;
            pcbsImportJob(job); BuildUpgraderGetUpgrades();
            check(alerts.length === 0 && table.rows.length > 1 && Array.prototype.slice.call(table.rows, 1).every(function (row) {
                return sourceData.cpus[row.cells[0].textContent || job.system.cpu].manufacturer === wanted;
            }), 'Every upgrade obeys the imported ' + wanted + ' CPU requirement.');
        });
        job.fields.cpuBrand = 'Any';
        job.brands = [{ brand: 'HyperX', count: 1, newOnly: true }];
        pcbsUpdateCareer({ valid: true, applicable: true, level: 12, percent: 40 });
        pcbsImportJob(job); BuildUpgraderGetUpgrades();
        check(!data.gpus[job.system.gpus[0]] && alerts.length === 0 && table.rows.length > 1,
            'An above-level installed GPU remains usable for scoring without unlocking purchases.');
        pcbsUpdateCareer({ valid: true, applicable: true, level: 13, percent: 40 });
        pcbsImportJob(job); BuildUpgraderGetUpgrades();
        pcbsFlush(); sessionStorage.setItem('pcbs-fixture-state', JSON.stringify(pcbsCaptured));
        check(pcbsErrors.length === 0, 'The regression produces no JavaScript errors.');
        return { passed: checks, upgrades: table.rows.length - 1, cheapest: first };
    } finally { window.alert = originalAlert; }
})();
