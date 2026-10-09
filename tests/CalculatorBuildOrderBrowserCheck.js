(function () {
    var checks = 0, requests = [], oldBridge = window.pcbsOrderBuild;
    function check(value, message) { checks++; if (!value) throw new Error(message); }
    try {
        pcbsSetLanguage('ru');
        var all = document.querySelector('[name="inputOptionAllPartsUnlocked"]'); if (!all.checked) all.click();
        var job = { tab: 'divBuildMaker', budget: 10000, target: 6400, system: {}, fields: {
            fixedCpu: 'Intel Core i7-7700K', fixedGpu: 'EVGA GeForce GTX 1080 GAMING', gpuCount: '2',
            fixedCase: 'ADATA XPG Battlecruiser (Black)', fixedMobo: 'MSI Z270 Gaming Pro Carbon',
            fixedRam: 'ADATA Premier Series 4 GB 2133 MHz' }, brands: [], requirements: [], notes: [] };
        pcbsImportJob(job);
        var form = document.getElementById('formBuildMaker'); form.inputBuildMakerTarget3DMarkScoreOffset.value = '50000';
        BuildMakerGetBuilds();
        var table = document.getElementById('tableBuildMakerResults'), row = table.rows[1];
        check(table.rows.length > 2, 'A real exact-parts build calculation generates multiple orderable variants.');
        check(table.rows[0].cells[13].textContent === 'Заказать', 'The result action header is Order in Russian.');
        var button = row.querySelector('.pcbs-order-button');
        check(button && button.textContent === 'Заказать', 'The original Save action is replaced by Order.');
        button.click();
        check(!button.disabled && document.querySelector('.pcbs-order-status').textContent === 'Нет связи с корзиной игры.',
            'An unavailable native bridge reports a problem without pretending an order succeeded.');
        window.pcbsOrderBuild = function (json) { requests.push(JSON.parse(json)); };
        button.click();
        check(requests.length === 1 && button.disabled && button.textContent === 'Добавляю...', 'Clicking sends one request and prevents a second pending click.');
        check(button.getAttribute('aria-busy') === 'true', 'The pending state is accessible.');
        button.click();
        check(requests.length === 1, 'A second click while waiting cannot duplicate the build.');
        var order = requests[0], byKind = {};
        order.items.forEach(function (item) { byKind[item.kind] = item; });
        check(order.version === 1 && typeof order.id === 'string' && order.items.length === 5, 'The bridge request contains all five displayed part roles.');
        check(byKind.case.name === row.cells[0].textContent && byKind.cpu.name === row.cells[1].textContent && byKind.motherboard.name === row.cells[8].textContent,
            'Case, CPU and motherboard come from the clicked result row.');
        check(byKind.ram.name === row.cells[4].textContent && byKind.ram.quantity === Number(row.cells[2].textContent), 'RAM model and module count match the result.');
        check(byKind.gpu.name === row.cells[7].textContent && byKind.gpu.quantity === 2, 'Both recommended GPUs are requested.');
        check(!byKind.psu && !byKind.storage, 'PSU and storage are never auto-ordered.');
        pcbsBuildOrderResult({ id: 'unrelated', ok: true, count: 99 });
        check(button.textContent === 'Добавляю...', 'Unrelated bridge replies cannot confirm another row.');
        pcbsBuildOrderResult({ id: order.id, ok: true, count: order.items.reduce(function (sum, item) { return sum + item.quantity; }, 0) });
        check(button.disabled && button.textContent === 'В корзине' && button.getAttribute('aria-busy') === 'false', 'Native success confirms the order without re-enabling accidental duplicate clicks.');
        check(document.getElementById('divBuildMakerSaveForm').style.display !== 'block', 'Ordering never opens the previous Save dialog.');
        pcbsSetLanguage('en');
        check(button.textContent === 'In cart' && document.querySelector('.pcbs-order-status').textContent.indexOf('Parts added to cart:') === 0,
            'Confirmation and buttons switch languages live.');
        var second = table.rows[2].querySelector('.pcbs-order-button'); second.click();
        pcbsBuildOrderResult({ id: requests[1].id, ok: false, code: 'unavailable', count: 0 });
        check(!second.disabled && second.textContent === 'Order', 'Failure lets the user retry without marking the row purchased.');
        check(document.querySelector('.pcbs-order-status').textContent.indexOf('The cart was not changed.') >= 0, 'Availability failure explains that no partial order was made.');
        var retryId = requests[1].id; second.click();
        check(requests[2].id === retryId, 'Retries reuse the request ID for native duplicate protection.');
        pcbsBuildOrderResult({ id: retryId, ok: false, code: 'missing', part: 'Unknown exact variant' });
        check(document.querySelector('.pcbs-order-status').textContent.indexOf('Unknown exact variant') >= 0, 'Unknown variants are identified explicitly instead of substituting a model.');
        pcbsSetLanguage('ru');
        form.inputBuildMakerIncludeCase.checked = false; form.inputBuildMakerIncludeMotherboard.checked = false;
        form.inputBuildMakerIncludeRamPart.checked = false;
        ['pcbsFixedCase', 'pcbsFixedMobo', 'pcbsFixedRam'].forEach(function (name) { form.elements.namedItem(name).value = ''; });
        BuildMakerGetBuilds();
        check(document.querySelector('.pcbs-order-status').textContent === '', 'A new calculation clears the previous order status.');
        var reduced = table.rows[1].querySelector('.pcbs-order-button'); reduced.click();
        var last = requests[requests.length - 1];
        check(last.items.length === 2 && last.items[0].kind === 'cpu' && last.items[1].kind === 'gpu', 'Excluded case, RAM and motherboard are omitted from the order.');
        pcbsBuildOrderResult({ id: last.id, ok: false, code: 'unavailable' });
        pcbsFlush(); sessionStorage.setItem('pcbs-fixture-state', JSON.stringify(pcbsCaptured));
        check(pcbsCaptured.tabs.divBuildMaker.rows[0][13].save === true, 'Order buttons retain backward-compatible result persistence.');
        check(pcbsErrors.length === 0, 'Ordering and language changes do not introduce browser errors.');
        return { passed: checks, results: table.rows.length - 1, requests: requests.length };
    } finally { window.pcbsOrderBuild = oldBridge; }
})();
