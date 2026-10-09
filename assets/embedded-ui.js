// Compatibility UI for native select/datalist popups in the game's offscreen Chromium.
// The calculator data, formulas, original inputs and event handlers remain unchanged.
(function () {
    'use strict';
    var popup = null, owner = null, rows = [], active = -1;
    var choosing = false;
    var style = document.createElement('style');
    style.textContent = '.pcbs-options{position:fixed;z-index:2147483647;background:#fff;color:#111;' +
        'border:2px solid #538bff;box-shadow:0 6px 24px #0008;overflow:auto;max-height:300px;' +
        'font:14px Arial,sans-serif;text-align:left;box-sizing:border-box;}' +
        '.pcbs-option{display:block;width:100%;box-sizing:border-box;background:#fff;color:#111;' +
        'border:0;border-radius:0;padding:7px 10px;min-height:30px;text-align:left;font:inherit;' +
        'white-space:normal;cursor:pointer;}' +
        '.pcbs-option:hover,.pcbs-option[aria-selected="true"]{background:#164cba;color:#fff;}' +
        'input:focus,select:focus,button:focus{outline:2px solid #77a9ff;outline-offset:1px;}';
    document.head.appendChild(style);

    function close() {
        if (popup) popup.remove();
        if (owner) owner.setAttribute('aria-expanded', 'false');
        popup = null; owner = null; rows = []; active = -1;
    }
    window.pcbsCloseOptions = close;
    function highlight(index) {
        if (!rows.length) return;
        active = Math.max(0, Math.min(index, rows.length - 1));
        rows.forEach(function (row, i) { row.setAttribute('aria-selected', String(i === active)); });
        rows[active].scrollIntoView({ block: 'nearest' });
    }
    function choose(value) {
        var input = owner;
        choosing = true;
        close();
        input.value = value;
        input.dispatchEvent(new Event('input', { bubbles: true }));
        input.dispatchEvent(new Event('change', { bubbles: true }));
        input.focus();
        choosing = false;
    }
    function show(input) {
        close();
        var isSelect = input.tagName === 'SELECT';
        var list = isSelect ? input : document.getElementById(input.getAttribute('data-pcbs-list'));
        if (!list || input.disabled) return;
        var query = isSelect ? '' : input.value.toLowerCase();
        var options = Array.prototype.slice.call(list.options).filter(function (o) {
            return !o.disabled && (isSelect || o.value.toLowerCase().indexOf(query) !== -1);
        });
        if (!options.length) return;
        owner = input;
        input.setAttribute('aria-expanded', 'true');
        popup = document.createElement('div');
        popup.className = 'pcbs-options';
        popup.setAttribute('role', 'listbox');
        var rect = input.getBoundingClientRect();
        var below = window.innerHeight - rect.bottom - 8;
        var above = rect.top - 8;
        var height = Math.min(300, Math.max(below, above), options.length * 31 + 4);
        popup.style.left = Math.max(4, Math.min(rect.left, window.innerWidth - rect.width - 4)) + 'px';
        popup.style.width = rect.width + 'px';
        popup.style.maxHeight = height + 'px';
        if (below >= Math.min(height, 150)) popup.style.top = rect.bottom + 'px';
        else popup.style.bottom = window.innerHeight - rect.top + 'px';
        // Datalist suggestions are capped; typing more narrows the complete original list.
        options.slice(0, isSelect ? options.length : 200).forEach(function (option) {
            var row = document.createElement('button');
            row.type = 'button'; row.className = 'pcbs-option'; row.tabIndex = -1;
            row.setAttribute('role', 'option');
            row.setAttribute('aria-selected', 'false');
            row.textContent = option.label || option.textContent || option.value;
            row.addEventListener('mousedown', function (event) { event.preventDefault(); });
            row.addEventListener('click', function () { choose(option.value); });
            popup.appendChild(row); rows.push(row);
        });
        document.body.appendChild(popup);
        if (isSelect) {
            var selected = options.findIndex(function (option) { return option.value === input.value; });
            highlight(Math.max(0, selected));
        }
    }
    function setup() {
        Array.prototype.forEach.call(document.querySelectorAll('input[list]'), function (input) {
            input.setAttribute('data-pcbs-list', input.getAttribute('list'));
            input.removeAttribute('list');
            input.setAttribute('autocomplete', 'off');
            input.setAttribute('aria-autocomplete', 'list');
            input.addEventListener('input', function () { if (!choosing) show(input); });
            input.addEventListener('click', function () { show(input); });
        });
        document.addEventListener('mousedown', function (event) {
            var input = event.target;
            if (input.tagName === 'SELECT') {
                event.preventDefault();
                var wasOpen = owner === input;
                close(); input.focus();
                if (!wasOpen) show(input);
            } else if (popup && input !== owner && !popup.contains(input)) close();
        }, true);
        document.addEventListener('keydown', function (event) {
            var input = event.target;
            if (!popup && input.tagName === 'SELECT' &&
                (event.key === 'Enter' || event.key === ' ' || event.key === 'ArrowDown')) {
                event.preventDefault(); show(input); return;
            }
            if (!popup || input !== owner) return;
            if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
                event.preventDefault(); highlight(active + (event.key === 'ArrowDown' ? 1 : -1));
            } else if (event.key === 'Enter' && active >= 0) {
                event.preventDefault(); rows[active].click();
            } else if (event.key === 'Escape' || event.key === 'Tab') close();
        }, true);
        window.addEventListener('resize', close);
        window.addEventListener('blur', close);
        document.addEventListener('scroll', function (event) {
            if (popup && event.target !== popup && !popup.contains(event.target)) close();
        }, true);
        console.log('PCBS embedded select and datalist compatibility UI ready.');
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', setup);
    else setup();
})();
