(function () {
    var checks = 0;
    function check(value, message) { checks++; if (!value) throw new Error(message); }
    var alerts = [], originalAlert = window.alert;
    window.alert = function (message) { alerts.push(message); };
    try {
        pcbsUpdateCareer({ valid: true, applicable: true, level: 13, percent: 25.5 });
        var score = document.getElementById('td3DMarkSystemScore').textContent;
        var maker = document.getElementById('formBuildMaker'), upgrade = document.getElementById('formBuildUpgrader');
        maker.inputBuildMakerBudget.value = '1234';
        var job = { tab: 'divBuildUpgrader', ru: true, jobId: 15, title: 'НАУКА требует больших мощностей!', budget: 540, target: 4800,
            system: { pcCase: 'ADATA XPG Battlecruiser (Black)', cpu: 'Intel Celeron G3900', mobo: 'Mortoni H170 Mtech',
                gpus: ['Shean GeForce GTX 750 Ti GAMER'], ram: ['ADATA Premier Series 4 GB 2133 MHz'],
                ramSticks: 1, ramSpeed: 2133, m2s: [], disks: ['A test drive'], cooler: 'A test cooler', psu: 'A test PSU' },
            fields: { cpuBrand: 'Any', gpuBrand: 'Any', gpuCount: 'Any', gpuType: 'Any' },
            requirements: ['Балл 3DMark: 4800'], notes: ['Нажмите Get Upgrades для расчёта.'], brands: [] };
        pcbsImportJob(job);
        check(upgrade.inputBuildUpgraderBudget.value === '540' && upgrade.inputBuildUpgraderTarget3DMarkScore.value === '4800', 'Upgrade import uses actual job budget and score.');
        check(upgrade.inputBuildUpgraderOriginalSystemCpu.value === job.system.cpu && upgrade.inputBuildUpgraderOriginalSystemCase.value === job.system.pcCase, 'Current CPU and case are imported.');
        check(upgrade.inputBuildUpgraderOriginalSystemGpu.value === job.system.gpus[0] && upgrade.selectBuildUpgraderOriginalSystemMobo.value === job.system.mobo, 'GPU and motherboard are imported.');
        check(upgrade.inputBuildUpgraderRamSticks.value === '1' && upgrade.selectBuildUpgraderOriginalSystemRamSpeed.value === '2133', 'RAM speed and module count are imported.');
        check(document.getElementById('divBuildUpgrader').style.display === 'block' && document.getElementById('tableBuildUpgraderResults').rows.length === 1, 'Upgrade mode opens without starting calculation.');
        check(maker.inputBuildMakerBudget.value === '1234' && document.getElementById('td3DMarkSystemScore').textContent === score, 'Import does not erase unrelated tabs.');
        check(document.getElementById('divBuildUpgraderJobReview').textContent.indexOf('A test PSU') >= 0, 'All original hardware is reviewable, including PSU and cooler.');
        check(pcbsCaptured.tabs.divBuildUpgrader.fields.pcbsJobMetadata.value.indexOf('4800') >= 0, 'Imported job metadata joins normal persistent state.');
        // A narrow real calculation verifies wrappers and above-level installed parts.
        upgrade.inputBuildUpgraderBudget.value = '5000'; upgrade.inputBuildUpgraderTarget3DMarkScore.value = '6400';
        upgrade.elements.namedItem('pcbsFixedCpu').value = 'Intel Core i7-7700K';
        upgrade.elements.namedItem('pcbsFixedGpu').value = 'EVGA GeForce GTX 1080 GAMING';
        var all = document.querySelector('[name="inputOptionAllPartsUnlocked"]'); if (!all.checked) all.click();
        var before = data;
        BuildUpgraderGetUpgrades();
        var table = document.getElementById('tableBuildUpgraderResults');
        check(data === before, 'Calculation restores shared databases afterward.');
        check(table.rows.length > 1 && alerts.length === 0, 'A real upgrade calculation works on the imported system.');
        check(Array.prototype.slice.call(table.rows, 1).every(function (row) {
            return row.cells[0].textContent === 'Intel Core i7-7700K' && row.cells[3].textContent === 'EVGA GeForce GTX 1080 GAMING';
        }), 'Exact CPU/GPU constraints apply to every result.');
        var newJob = { tab: 'divBuildMaker', ru: false, jobId: 16, title: 'Build a new PC', budget: 3000, target: 6400,
            system: {}, fields: { cpuBrand: 'Intel', gpuBrand: 'NVIDIA', gpuCount: '2', gpuType: 'Water', cpuOverclock: true },
            requirements: ['Two water-cooled GPUs'], notes: ['Review the parts'], brands: [] };
        var kept = upgrade.inputBuildUpgraderBudget.value;
        pcbsImportJob(newJob);
        check(document.getElementById('divBuildMaker').style.display === 'block' && maker.inputBuildMakerBudget.value === '3000', 'New builds go to Build Maker.');
        check(upgrade.inputBuildUpgraderBudget.value === kept, 'New build import preserves the upgrade tab.');
        check(maker.selectBuildMakerGpuCount.value === '2' && maker.selectBuildMakerGpuType.value === 'Water' && maker.pcbsCpuBrand.value === 'Intel', 'GPU count/cooling and CPU brand come from typed objectives.');
        check(maker.inputBuildMakerIncludeCase.checked && maker.inputBuildMakerIncludeMotherboard.checked && maker.inputBuildMakerIncludeRamPart.checked && maker.inputBuildMakerNeedCpuOverclock.checked, 'New builds include case/motherboard/RAM and retain CPU overclock condition.');
        // Run a small exact-parts build to exercise the new-build path as well.
        newJob.budget = 5000;
        newJob.fields = { cpuBrand: 'Intel', gpuBrand: 'NVIDIA', gpuCount: '1', gpuType: 'Air',
            fixedCpu: 'Intel Core i7-7700K', fixedGpu: 'EVGA GeForce GTX 1080 GAMING',
            fixedCase: 'ADATA XPG Battlecruiser (Black)', fixedMobo: 'Mortoni H170 Mtech', fixedRam: 'ADATA Premier Series 4 GB 2133 MHz' };
        pcbsImportJob(newJob); BuildMakerGetBuilds();
        var buildTable = document.getElementById('tableBuildMakerResults');
        check(buildTable.rows.length > 1, 'A real new-build calculation works with imported exact parts.');
        check(Array.prototype.slice.call(buildTable.rows, 1).every(function (row) {
            return row.cells[0].textContent === newJob.fields.fixedCase && row.cells[1].textContent === newJob.fields.fixedCpu &&
                row.cells[4].textContent === newJob.fields.fixedRam && row.cells[7].textContent === newJob.fields.fixedGpu;
        }), 'Every proposed build obeys the required case, CPU, RAM and GPU.');
        check(alerts.length === 0, 'Imported calculation paths do not introduce validation alerts.');
        pcbsFlush(); sessionStorage.setItem('pcbs-fixture-state', JSON.stringify(pcbsCaptured));
        check(pcbsErrors.length === 0, 'Import and real calculation introduce no JavaScript errors.');
        return { passed: checks, upgradeResults: table.rows.length - 1 };
    } finally { window.alert = originalAlert; }
})();
