(function () {
    var checks = 0;
    function check(value, message) { checks++; if (!value) throw new Error(message); }
    var table = document.getElementById('tableBuildMakerResults');
    check(table.rows.length > 1, 'Calculated result rows restore after reload.');
    check(Array.prototype.slice.call(table.rows, 1).every(function (row) {
        var button = row.querySelector('.pcbs-order-button'); return button && !button.disabled && button.textContent === 'Заказать';
    }), 'Restored action cells contain functioning Order buttons, not Save buttons.');
    var sent;
    window.pcbsOrderBuild = function (json) { sent = JSON.parse(json); };
    table.rows[1].querySelector('.pcbs-order-button').click();
    check(sent && sent.items.length === 2 && sent.items[0].kind === 'cpu' && sent.items[1].quantity === 2,
        'A restored row orders its actual displayed parts and counts.');
    pcbsBuildOrderResult({ id: sent.id, ok: true, count: 3 });
    check(table.rows[1].querySelector('.pcbs-order-button').textContent === 'В корзине', 'Restored buttons handle native confirmation.');
    check(pcbsErrors.length === 0, 'Restore introduces no browser errors.');
    return { passed: checks };
})();
