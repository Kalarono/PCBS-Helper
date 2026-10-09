(function () {
    var checks = 0;
    function check(value, message) { checks++; if (!value) throw new Error(message); }
    var maker = document.getElementById('formBuildMaker'), upgrade = document.getElementById('formBuildUpgrader');
    check(maker.pcbsCpuBrand.value === 'Intel' && maker.pcbsGpuBrand.value === 'NVIDIA', 'Added brand controls restore across restart.');
    check(maker.selectBuildMakerGpuCount.value === '1' && maker.selectBuildMakerGpuType.value === 'Air', 'Imported native constraints restore.');
    check(maker.pcbsFixedCase.value === 'ADATA XPG Battlecruiser (Black)' && document.getElementById('tableBuildMakerResults').rows.length > 1, 'Required case and new-build results restore.');
    check(document.getElementById('divBuildMakerJobReview').textContent.indexOf('Build a new PC') >= 0, 'Job context restores.');
    check(upgrade.pcbsFixedCpu.value === 'Intel Core i7-7700K' && document.getElementById('tableBuildUpgraderResults').rows.length > 1, 'Exact required parts and upgrade results restore.');
    pcbsClearCurrentTab();
    check(maker.pcbsCpuBrand.value === 'Any' && maker.pcbsJobMetadata.value === '' && document.getElementById('divBuildMakerJobReview').style.display === 'none', 'Clear removes only current imported job and its constraints.');
    check(upgrade.pcbsJobMetadata.value !== '' && document.getElementById('tableBuildUpgraderResults').rows.length > 1, 'Clear does not erase the other imported job or its results.');
    check(pcbsErrors.length === 0, 'Import restoration and Clear introduce no browser errors.');
    return { passed: checks };
})();
