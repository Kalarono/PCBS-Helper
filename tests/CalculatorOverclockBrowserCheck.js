(function () {
    var checks = 0;
    function check(value, message) { checks++; if (!value) throw new Error(message); }
    // Emulate the native career availability snapshot at the screenshot's level.
    var availability = {};
    ['cpus','gpus','mobos','pcCases','rams','storages'].forEach(function (group) {
        Object.keys(sourceData[group]).forEach(function (key) {
            var p = sourceData[group][key]; availability[p.id] = p.level <= 16;
        });
    });
    pcbsSetCatalogAvailability(availability);
    pcbsUpdateCareer({ valid: true, applicable: true, level: 16, percent: 0 });
    var all = document.querySelector('[name="inputOptionAllPartsUnlocked"]');
    if (all.checked) all.click();
    pcbsSetLanguage('ru');
    var job = { tab: 'divBuildMaker', jobId: 103, title: 'Разгон CPU и GPU', budget: 2000, target: 0,
        system: {}, fields: { targetCpuMhz: 4700, targetGpuMhz: 2071, allNewParts: true },
        requirements: ['Разогнать ЦП до 4700 МГц', 'Разогнать видеокарту до 2071 МГц', 'Никаких б/у частей!'],
        brands: [], notes: [] };
    pcbsImportJob(job);
    var maker = document.getElementById('formBuildMaker');
    check(maker.inputBuildMakerBudget.value === '2000' && maker.inputBuildMakerTarget3DMarkScore.value === '', 'OC-only job imports the real budget and no score target.');
    check(maker.inputBuildMakerTargetCpuMhz.value === '4700' && maker.inputBuildMakerTargetGpuMhz.value === '2071', 'Both targets are visible editable fields.');
    check(document.getElementById('divBuildMakerJobReview').textContent.indexOf('Никаких б/у частей!') >= 0, 'Other customer requirements remain visible.');
    check(document.getElementById('inputBuildMakerTargetCpuMhz-label').textContent === 'Цель CPU (МГц)', 'Clock goal labels are localized.');
    var startAlerts = pcbsFixtureAlerts.length;
    BuildMakerGetBuilds();
    var table = document.getElementById('tableBuildMakerResults'), rows = Array.prototype.slice.call(table.rows, 1);
    check(rows.length > 0 && pcbsFixtureAlerts.length === startAlerts, 'OC-only selection works without a required-score alert.');
    check(rows.every(function (r) { return Number(r.querySelector('.tdCpuMhz').textContent) >= 4700 && Number(r.querySelector('.tdGpuMhz').textContent) >= 2071; }), 'Every visible result includes both planned frequencies.');
    var first = rows[0], cpu = first.querySelector('.tdCpu').textContent, gpu = first.querySelector('.tdGpuPart').textContent,
        board = first.querySelector('.tdMobo').textContent, pcCase = first.querySelector('.tdCase').textContent;
    var order;
    window.pcbsOrderBuild = function (json) {
        order = JSON.parse(json); pcbsBuildOrderResult({ id: order.id, ok: true, count: 5 });
    };
    first.querySelector('.pcbs-order-button').click();
    check(order.items.some(function (p) { return p.name === cpu; }) && order.items.some(function (p) { return p.name === gpu; }), 'Added frequency columns do not change cart model names.');
    job = { tab: 'divBuildUpgrader', jobId: 104, title: 'Разгон установленного ПК', budget: 2000, target: 0,
        system: { cpu: cpu, gpus: [gpu], mobo: board, pcCase: pcCase, ramSticks: 1,
            ramSpeed: Number(first.querySelector('.tdRamSpeed').textContent), ram: [], m2s: [] },
        fields: { targetCpuMhz: 4700, targetGpuMhz: 2071, fixedCpu: cpu, fixedGpu: gpu, fixedMobo: board },
        requirements: [], brands: [], notes: [] };
    pcbsImportJob(job); BuildUpgraderGetUpgrades();
    var upgrade = document.getElementById('formBuildUpgrader'), upgrades = document.getElementById('tableBuildUpgraderResults');
    check(upgrades.rows.length > 1 && upgrades.rows[1].querySelector('.tdPrice').textContent === '0', 'A suitable current PC receives a zero-purchase OC plan.');
    check(upgrades.rows[1].querySelector('.tdCpu').textContent === cpu && upgrades.rows[1].querySelector('.tdGpuPart').textContent === gpu, 'The OC plan names retained parts as well as replacement parts.');
    check(upgrades.rows[1].querySelector('.tdCpuMhz').textContent === '4700' && upgrades.rows[1].querySelector('.tdGpuMhz').textContent === '2071', 'Upgrade result shows the actual target clocks.');
    upgrade.inputBuildUpgraderTargetCpuMhz.value = '100000'; BuildUpgraderGetUpgrades();
    check(upgrades.rows.length === 1 && pcbsFixtureAlerts.length === startAlerts + 1, 'An edited impossible frequency clears stale results and explains no matches.');
    upgrade.inputBuildUpgraderTargetCpuMhz.value = '4700'; BuildUpgraderGetUpgrades();
    showPage('divBuildMaker'); pcbsFlush();
    check(pcbsCaptured.tabs.divBuildMaker.columns.indexOf('cpuMhz') >= 0 && pcbsCaptured.tabs.divBuildMaker.rows[0][14].text === '4700', 'Frequency results join persistent state with named columns.');
    sessionStorage.setItem('pcbs-fixture-state', JSON.stringify(pcbsCaptured));
    check(pcbsErrors.length === 0, 'OC import, selection and cart preview produce no browser errors.');
    return { passed: checks, builds: rows.length, upgrades: upgrades.rows.length - 1 };
})();
