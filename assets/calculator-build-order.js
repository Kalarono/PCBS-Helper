// Add the displayed Build Maker variant to the game's cart through the native bridge.
(function (root) {
    'use strict';
    var pending = Object.create(null), sequence = 0, status, statusKey, statusSuffix, statusSkipped;
    function text(key) { return typeof root.pcbsTranslate === 'function' ? root.pcbsTranslate(key) : key; }
    function cell(row, css) { var element = row.querySelector('.' + css); return element ? element.textContent.trim() : ''; }
    function count(value, maximum) {
        var number = Number(value);
        if (!Number.isInteger(number) || number < 1 || number > maximum) throw new Error('Invalid result quantity.');
        return number;
    }
    function readRow(row) {
        var items = [];
        [['case', 'tdCase', null, 1], ['cpu', 'tdCpu', null, 1], ['ram', 'tdRamPart', 'tdramChannel', 4],
            ['gpu', 'tdGpuPart', 'tdGpuCount', 2], ['motherboard', 'tdMobo', null, 1]].forEach(function (entry) {
            var name = cell(row, entry[1]);
            if (name) items.push({ kind: entry[0], name: name, quantity: entry[2] ? count(cell(row, entry[2]), entry[3]) : 1 });
        });
        if (!items.length) throw new Error('Empty result.');
        return items;
    }
    function renderMessage() {
        if (!status || !statusKey) return;
        status.textContent = text(statusKey) + (statusSuffix ? ' ' + statusSuffix : '');
        if (statusSkipped && statusSkipped.length) {
            var list = document.createElement('ul');
            statusSkipped.forEach(function (item) {
                var row = document.createElement('li');
                var reason = item.reason === 'missing' ? 'model not found' : item.reason === 'ambiguous' ? 'model is ambiguous' :
                    item.reason === 'owned' ? 'already owned or in cart' : item.reason === 'unavailable' ? 'unavailable in the shop' : 'could not add';
                row.textContent = item.quantity + ' × ' + item.name + ' — ' + text(reason);
                list.appendChild(row);
            });
            var warning = document.createElement('div'); warning.textContent = text('Order these parts manually:');
            status.appendChild(warning); status.appendChild(list);
        }
    }
    function message(key, suffix, skipped) { statusKey = key; statusSuffix = suffix; statusSkipped = skipped; renderMessage(); }
    function label(button, key) { button.textContent = text(key); button.setAttribute('aria-busy', String(key === 'Adding...')); }
    function order(button) {
        if (button.disabled) return;
        if (typeof root.pcbsOrderBuild !== 'function') { message('The game cart is not connected.'); return; }
        var items;
        try { items = readRow(button.closest('tr')); }
        catch (_) { message('Could not read the build parts.'); return; }
        var id = button.pcbsOrderId || (Date.now() + '-' + (++sequence));
        button.pcbsOrderId = id;
        button.disabled = true; label(button, 'Adding...');
        var timer = root.setTimeout(function () {
            if (!pending[id]) return;
            delete pending[id]; button.disabled = false; label(button, 'Order');
            message('No cart confirmation received. Try again.');
        }, 15000);
        pending[id] = { button: button, timer: timer };
        try { root.pcbsOrderBuild(JSON.stringify({ version: 1, id: id, items: items })); }
        catch (_) {
            root.clearTimeout(timer); delete pending[id]; button.disabled = false; label(button, 'Order');
            message('The game cart is not connected.');
        }
    }
    root.pcbsBuildOrderResult = function (reply) {
        var entry = reply && pending[reply.id];
        if (!entry) return;
        root.clearTimeout(entry.timer); delete pending[reply.id];
        if (reply.ok) {
            label(entry.button, reply.skipped && reply.skipped.length ? 'Partly in cart' : 'In cart');
            message('Parts added to cart:', String(reply.count), reply.skipped);
        } else {
            entry.button.disabled = false; label(entry.button, 'Order');
            if (reply.skipped && reply.skipped.length) {
                message('No parts were added.', null, reply.skipped); return;
            }
            var key = reply.code === 'missing' || reply.code === 'ambiguous' ? 'Could not identify the part:' :
                reply.code === 'nocareer' ? 'Load the workshop to order a build.' :
                reply.code === 'unavailable' ? 'Some build parts are unavailable. The cart was not changed.' : 'Could not read the build parts.';
            message(key, reply.part);
        }
    };
    function convert(button) {
        button.type = 'button'; button.classList.add('pcbs-order-button');
        button.onclick = function () { order(button); };
        label(button, 'Order');
    }
    function setup() {
        var table = document.getElementById('tableBuildMakerResults');
        if (!table) return;
        table.rows[0].cells[13].textContent = text('Order');
        status = document.createElement('div'); status.className = 'pcbs-order-status';
        status.setAttribute('role', 'status'); status.setAttribute('aria-live', 'polite');
        table.parentNode.insertBefore(status, table);
        Array.prototype.forEach.call(table.querySelectorAll('.buttonSave'), convert);
        ['BuildMakerGetBuilds', 'pcbsClearCurrentTab'].forEach(function (name) {
            var original = root[name];
            if (typeof original !== 'function') return;
            root[name] = function () {
                statusKey = statusSuffix = statusSkipped = null; status.textContent = '';
                return original.apply(this, arguments);
            };
        });
        var setLanguage = root.pcbsSetLanguage;
        if (typeof setLanguage === 'function') root.pcbsSetLanguage = function () {
            setLanguage.apply(this, arguments); renderMessage();
        };
        root.addSaveButton = function (target, id) {
            var button = document.createElement('button'); button.id = id; button.className = 'buttonSave';
            convert(button); target.appendChild(button);
        };
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', setup);
    else setup();
})(typeof window !== 'undefined' ? window : this);
