(function () {
    var checks = 0, requests = [], oldBridge = window.pcbsOrderBuild;
    function check(value, message) { checks++; if (!value) throw new Error(message); }
    try {
        pcbsSetLanguage('ru');
        var all = document.querySelector('[name="inputOptionAllPartsUnlocked"]'); if (!all.checked) all.click();
        pcbsImportJob({ tab: 'divBuildMaker', budget: 10000, target: 6400, system: {}, fields: {
            fixedCpu: 'Intel Core i7-7700K', fixedGpu: 'EVGA GeForce GTX 1080 GAMING', gpuCount: '2',
            fixedCase: 'ADATA XPG Battlecruiser (Black)', fixedMobo: 'MSI Z270 Gaming Pro Carbon',
            fixedRam: 'ADATA Premier Series 4 GB 2133 MHz' }, brands: [], requirements: [], notes: [] });
        document.getElementById('formBuildMaker').inputBuildMakerTarget3DMarkScoreOffset.value = '50000';
        BuildMakerGetBuilds();
        window.pcbsOrderBuild = function (json) { requests.push(JSON.parse(json)); };
        var table = document.getElementById('tableBuildMakerResults'), button = table.rows[1].querySelector('.pcbs-order-button');
        button.click();
        var skipped = [{ name: 'Unavailable exact case', quantity: 1, reason: 'missing' },
            { name: 'RAM exact model', quantity: 2, reason: 'unavailable' }];
        pcbsBuildOrderResult({ id: requests[0].id, ok: true, code: 'partial', count: 4, skipped: skipped });
        var status = document.querySelector('.pcbs-order-status');
        check(button.disabled && button.textContent === 'Частично в корзине', 'Partial success confirms the accepted parts without allowing duplicate ordering.');
        check(status.textContent.indexOf('Деталей добавлено в корзину: 4') === 0, 'The actual added count is shown.');
        check(status.querySelectorAll('li').length === 2, 'Every skipped model gets a separate warning.');
        check(status.querySelectorAll('li')[0].textContent === '1 × Unavailable exact case — модель не найдена', 'The missing model and its quantity are preserved.');
        check(status.querySelectorAll('li')[1].textContent === '2 × RAM exact model — недоступно в магазине', 'Shop restrictions have a distinct reason.');
        check(status.textContent.indexOf('Добавьте эти детали вручную:') >= 0, 'The user knows which parts still need manual ordering.');
        button.click();
        check(requests.length === 1, 'Clicking a partially completed order cannot duplicate its CPU/GPU.');
        pcbsSetLanguage('en');
        check(button.textContent === 'Partly in cart' && status.textContent.indexOf('Parts added to cart: 4') === 0, 'Partial confirmation switches to English.');
        check(status.querySelectorAll('li')[1].textContent === '2 × RAM exact model — unavailable in the shop', 'Warning reasons switch language without translating model names.');
        var second = table.rows[2].querySelector('.pcbs-order-button'); second.click();
        pcbsBuildOrderResult({ id: requests[1].id, ok: false, code: 'failed', count: 0,
            skipped: [{ name: 'Unknown CPU', quantity: 1, reason: 'ambiguous' }, { name: 'Unknown GPU', quantity: 2, reason: 'error' }] });
        check(!second.disabled && second.textContent === 'Order', 'A zero-added response remains retryable.');
        check(status.textContent.indexOf('No parts were added.') === 0 && status.querySelectorAll('li').length === 2, 'Total failure lists all skipped parts instead of stopping at the first one.');
        check(status.querySelectorAll('li')[0].textContent.indexOf('model is ambiguous') >= 0, 'Real ambiguity remains an explicit failure.');
        second.click();
        pcbsBuildOrderResult({ id: requests[2].id, ok: false, code: 'failed', count: 0,
            skipped: [{ name: '<img src=x onerror=alert(1)>', quantity: 1, reason: 'missing' }] });
        check(!status.querySelector('img') && status.querySelector('li').textContent.indexOf('<img') >= 0, 'Returned model names cannot inject HTML into the warnings.');
        BuildMakerGetBuilds();
        check(!status.textContent && !status.querySelector('li'), 'A new calculation clears both confirmation and warning rows.');
        check(pcbsErrors.length === 0, 'Partial order handling introduces no browser errors.');
        return { passed: checks };
    } finally { window.pcbsOrderBuild = oldBridge; }
})();
