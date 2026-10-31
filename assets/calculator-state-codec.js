(function (root) {
    'use strict';
    var pages = ['div3DMarkScoreCalculator', 'divBuildMaker', 'divBuildUpgrader', 'divPartReplacer'];
    var historyPage = 'divHistoryAndSaves';
    function object(value) { return value && typeof value === 'object' && !Array.isArray(value); }
    function text(value, limit) { return typeof value === 'string' ? value.slice(0, limit || 8192) : ''; }
    function safeKey(key) { return key !== '__proto__' && key !== 'constructor' && key !== 'prototype' && key.length < 200; }
    function fields(value) {
        var out = Object.create(null);
        if (object(value)) Object.keys(value).forEach(function (key) {
            if (!safeKey(key) || !object(value[key])) return;
            out[key] = { value: text(value[key].value), checked: value[key].checked === true };
        });
        return out;
    }
    function tab(value) {
        value = object(value) ? value : {};
        var texts = Object.create(null);
        if (object(value.texts)) Object.keys(value.texts).forEach(function (key) {
            if (safeKey(key)) texts[key] = text(value.texts[key]);
        });
        var rows = Array.isArray(value.rows) ? value.rows.slice(0, 10000).map(function (row) {
            return Array.isArray(row) ? row.slice(0, 20).map(function (cell) {
                cell = object(cell) ? cell : {};
                var css = text(cell.css, 80);
                return { text: text(cell.text), css: /^[a-zA-Z0-9 _-]*$/.test(css) ? css : '', save: cell.save === true };
            }) : [];
        }) : [];
        var columns = Array.isArray(value.columns) ? value.columns.slice(0, 20).filter(function (name) {
            return typeof name === 'string' && /^[a-zA-Z][a-zA-Z0-9]*$/.test(name) && name.length < 40;
        }) : [];
        return { fields: fields(value.fields), texts: texts, rows: rows, columns: columns };
    }
    function resultRows(page, value, expected) {
        var saved = tab(value);
        var optional = {cpuMhz:'tdCpuMhz',gpuMhz:'tdGpuMhz',cooler:'tdCoolerPart',cpuThermal:'tdCpuThermal',gpuVoltage:'tdGpuVoltage'};
        // Previous build prices omitted PSUs and could contain incomplete water
        // loops. Keep the form/history, but recalculate those obsolete results.
        if (page === 'divBuildMaker' && expected.indexOf('psu') >= 0 && saved.columns.indexOf('psu') < 0) return [];
        function blank(name) { return {text:'',css:optional[name] || '',save:false}; }
        function extend(row) { return row.concat(expected.slice(row.length).map(blank)); }
        if (saved.columns.length) {
            // Clock columns were added after 1.0.2. Keep previous results, leaving
            // their unknown planned frequencies blank until the next calculation.
            if (expected.some(function (name) { return saved.columns.indexOf(name) < 0 && !optional[name]; })) return [];
            return saved.rows.filter(function (row) { return row.length === saved.columns.length; }).map(function (row) {
                return expected.map(function (name) { return row[saved.columns.indexOf(name)] || blank(name); });
            });
        }
        // Legacy upgrade rows use GPU count / cooling. The first independent UI
        // temporarily used RAM MHz / GPU count with the same nine-column width.
        if (page === 'divBuildUpgrader') return saved.rows.filter(function (row) { return row.length === 9 || row.length === 11 || row.length === 13 || row.length === expected.length; }).map(function (row) {
            if (/^\d+$/.test(row[1].text) && Number(row[1].text) >= 100 && !/\btdGpuCount\b/.test(row[1].css))
                row = [row[0], row[2], { text: '', css: 'tdGpuType', save: false }].concat(row.slice(3));
            return extend(row);
        });
        if (page === 'divBuildMaker' && expected.length >= 16) return saved.rows.filter(function (row) { return row.length === 14 || row.length === 16 || row.length === expected.length; }).map(function (row) {
            return extend(row);
        });
        return saved.rows.filter(function (row) { return row.length === expected.length; });
    }
    function history(value) {
        if (!Array.isArray(value)) return [];
        return value.slice(0, 10000).filter(function (entry) {
            return object(entry) && /^\d{1,24}$/.test(entry.key) && object(entry.value) &&
                (entry.value.type === 'build' || entry.value.type === 'div3DMarkScoreCalculatorForm');
        }).map(function (entry) {
            var record = Object.create(null);
            Object.keys(entry.value).forEach(function (key) {
                if (!safeKey(key)) return;
                var v = entry.value[key];
                if (typeof v === 'string') record[key] = text(v);
                else if (typeof v === 'number' && isFinite(v)) record[key] = v;
            });
            return { key: entry.key, value: record };
        });
    }
    function normalize(value) {
        value = object(value) && value.version === 1 ? value : {};
        var tabs = Object.create(null);
        pages.forEach(function (page) { tabs[page] = tab(value.tabs && value.tabs[page]); });
        var active = pages.indexOf(value.activeTab) >= 0 || value.activeTab === historyPage ? value.activeTab : pages[0];
        return { version: 1, activeTab: active, shared: fields(value.shared), tabs: tabs, history: history(value.history) };
    }
    function clearTab(value, page) {
        var out = normalize(value);
        if (pages.indexOf(page) >= 0) out.tabs[page] = tab(null);
        return out;
    }
    var api = { pages: pages, historyPage: historyPage, normalize: normalize, clearTab: clearTab, resultRows: resultRows };
    if (typeof module !== 'undefined' && module.exports) module.exports = api;
    else root.PCBSCalculatorStateCodec = api;
})(typeof window !== 'undefined' ? window : this);
