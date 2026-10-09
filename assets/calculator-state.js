(function () {
    'use strict';
    var codec = window.PCBSCalculatorStateCodec;
    var forms = {
        div3DMarkScoreCalculator: 'form3DMarkScoreCalculator', divBuildMaker: 'formBuildMaker',
        divBuildUpgrader: 'formBuildUpgrader', divPartReplacer: 'formPartReplacer'
    };
    var tables = { divBuildMaker: 'tableBuildMakerResults', divBuildUpgrader: 'tableBuildUpgraderResults', divPartReplacer: 'tablePartReplacerResults' };
    var scoreIds = ['td3DMarkCpuScore', 'td3DMarkGpuScore', 'td3DMarkSystemScore', 'td3DMarkGpuType', 'td3DMarkSystemWatts',
        'td3DMarkCpuPriceNew', 'td3DMarkCpuPriceUsed', 'td3DMarkGpusPriceNew', 'td3DMarkGpusPriceUsed'];
    var active = codec.pages[0], restoring = true, lastSaved = '', timer = null, reportedPage = null;
    var career = window.pcbsInitialCareer || null;
    function controls(form) { return form ? Array.prototype.slice.call(form.elements).filter(function (c) { return c.name && c.tagName !== 'BUTTON'; }) : []; }
    function captureFields(form) {
        var fields = Object.create(null);
        controls(form).forEach(function (c) { fields[c.name] = { value: c.value || '', checked: !!c.checked }; });
        return fields;
    }
    function restoreFields(form, fields) {
        controls(form).forEach(function (c) {
            var saved = fields[c.name]; if (!saved) return;
            if (c.type === 'checkbox') c.checked = saved.checked;
            else {
                if (c.tagName === 'SELECT' && saved.value && !Array.prototype.some.call(c.options, function (o) { return o.value === saved.value; }))
                    c.add(new Option(saved.value, saved.value));
                c.value = saved.value;
            }
        });
    }
    function restoreTabFields(page, fields) {
        restoreFields(document.getElementById(forms[page]), fields);
        if (page === 'divBuildMaker') restoreFields(document.getElementById('formBuildMakerSave'), fields);
    }
    function captureHistory() {
        var entries = [];
        Object.keys(sessionStorage).forEach(function (key) {
            try {
                var value = JSON.parse(sessionStorage.getItem(key));
                if (value && (value.type === 'build' || value.type === 'div3DMarkScoreCalculatorForm')) entries.push({ key: key, value: value });
            } catch (_) { }
        });
        return entries;
    }
    function restoreHistory(entries) {
        captureHistory().forEach(function (entry) { sessionStorage.removeItem(entry.key); });
        entries.forEach(function (entry) { sessionStorage.setItem(entry.key, JSON.stringify(entry.value)); });
        if (typeof updateHistory === 'function') updateHistory();
    }
    function captureTab(page) {
        var out = { fields: captureFields(document.getElementById(forms[page])), texts: {}, rows: [] };
        if (page === 'divBuildMaker') Object.assign(out.fields, captureFields(document.getElementById('formBuildMakerSave')));
        if (page === 'div3DMarkScoreCalculator') scoreIds.forEach(function (id) { out.texts[id] = document.getElementById(id).textContent; });
        var table = document.getElementById(tables[page]);
        if (table) Array.prototype.slice.call(table.rows, 1).forEach(function (row) {
            out.rows.push(Array.prototype.map.call(row.cells, function (cell) {
                var save = !!cell.querySelector('.buttonSave');
                return { text: save ? '' : cell.textContent, css: cell.className, save: save };
            }));
        });
        return out;
    }
    function capture() {
        var tabs = {};
        codec.pages.forEach(function (page) { tabs[page] = captureTab(page); });
        var shared = captureFields(document.getElementById('formOptions'));
        // formOptions is named differently in some calculator versions; use the containing form.
        var options = document.querySelector('#divOptions form');
        Object.assign(shared, captureFields(options), captureFields(document.getElementById('formLevel')));
        return codec.normalize({ version: 1, activeTab: active, shared: shared, tabs: tabs, history: captureHistory() });
    }
    function restoreOutputs(page, tab) {
        if (page === 'div3DMarkScoreCalculator') scoreIds.forEach(function (id) { document.getElementById(id).textContent = tab.texts[id] || ''; });
        var table = document.getElementById(tables[page]);
        if (!table) return;
        resetResultsTable(table, 0, 0);
        tab.rows.forEach(function (cells, i) {
            var row = table.insertRow(-1);
            cells.forEach(function (saved) {
                var cell = row.insertCell(-1); cell.textContent = saved.text; cell.className = saved.css;
                if (saved.save && page === 'divBuildMaker') addSaveButton(cell, i + 1);
            });
        });
    }
    function syncFilters() {
        var hem = document.querySelector('[name="inputOptionHem"]');
        var shop = document.querySelector('[name="inputOptionPartsNotInShop"]');
        var all = document.querySelector('[name="inputOptionAllPartsUnlocked"]');
        includeHemParts = hem.checked; includePartsNotInShop = shop.checked;
        allPartsUnlocked = all.checked || !!(career && career.valid && !career.applicable);
        var form = document.getElementById('formLevel');
        level = numberOrDefault(form.inputLevel.value, 0);
        levelPercentThroughSet = form.inputLevelPercentThrough.value !== '' && isFinite(Number(form.inputLevelPercentThrough.value));
        levelPercentThrough = numberOrDefault(form.inputLevelPercentThrough.value, 0);
        setData(); setListOptions(); setSelectOptions();
        refreshDependentOptions();
    }
    function refreshDependentOptions() {
        var form = document.getElementById('form3DMarkScoreCalculator');
        if (data3DMark.cpus[form.input3DMarkCpu.value]) { updateRamSpeedOptions3DMark(); updateRamChannelOptions3DMark(); }
        else [form.select3DMarkRamSpeed, form.select3DMarkRamChannel].forEach(function (select) {
            Array.prototype.forEach.call(select.options, function (option) { option.style.display = 'block'; });
        });
        var upgrader = document.getElementById('formBuildUpgrader');
        if (data.cpus[upgrader.inputBuildUpgraderOriginalSystemCpu.value]) updateMoboOptionsBuildUpgrader();
        var replacer = document.getElementById('formPartReplacer');
        if (data.cpus[replacer.inputPartReplacerOriginalSystemCpu.value]) updateMoboOptionsPartReplacer();
    }
    function applyCareer(value) {
        if (!value || !value.valid) return;
        career = value;
        var form = document.getElementById('formLevel');
        var saved = {};
        codec.pages.forEach(function (page) { saved[page] = captureFields(document.getElementById(forms[page])); });
        form.inputLevel.value = value.applicable ? String(value.level) : '';
        // Use whole percentages without rounding up an unlock threshold early.
        form.inputLevelPercentThrough.value = value.applicable ? String(Math.floor(value.percent)) : '';
        form.inputLevel.readOnly = true; form.inputLevelPercentThrough.readOnly = true;
        form.inputLevel.title = value.applicable ? 'Career Level from the current game' : 'Career levels do not restrict this game mode';
        form.inputLevelPercentThrough.title = value.applicable ? 'Experience progress from the current game' : 'Not applicable in this game mode';
        syncFilters();
        codec.pages.forEach(function (page) { restoreFields(document.getElementById(forms[page]), saved[page]); });
    }
    window.pcbsUpdateCareer = function (value) { if (!restoring) { applyCareer(value); schedule(); } else career = value; };
    function notifyPage() {
        if (typeof window.pcbsCalculatorTab !== 'function' || reportedPage === active) return;
        window.pcbsCalculatorTab(codec.pages.indexOf(active) >= 0); reportedPage = active;
    }
    function flush() {
        if (restoring) return;
        notifyPage();
        if (typeof window.pcbsSaveCalculator !== 'function') return;
        var json = JSON.stringify(capture());
        if (json !== lastSaved) { window.pcbsSaveCalculator(json); lastSaved = json; }
    }
    window.pcbsFlush = flush;
    function schedule() { if (restoring) return; clearTimeout(timer); timer = setTimeout(flush, 150); }
    window.pcbsClearCurrentTab = function () {
        if (codec.pages.indexOf(active) < 0) return false;
        if (typeof window.pcbsCloseOptions === 'function') window.pcbsCloseOptions();
        var form = document.getElementById(forms[active]);
        form.reset();
        controls(form).forEach(function (c) {
            if (c.tagName === 'INPUT' && c.type !== 'checkbox') c.value = '';
            if (c.tagName === 'SELECT') c.selectedIndex = c.options.length ? 0 : -1;
        });
        if (active === 'div3DMarkScoreCalculator') {
            refreshDependentOptions();
        }
        if (active === 'divBuildMaker') {
            hideSaver(document.getElementById('divBuildMakerSaveForm'));
            unblurBackground(document.getElementById('divBuildMakerForm'));
            unblurBackground(document.getElementById('divBuildMakerResults'));
            document.getElementById('formBuildMakerSave').reset();
        }
        if (active === 'divPartReplacer' && typeof partReplacerResults !== 'undefined') partReplacerResults = [];
        restoreOutputs(active, codec.clearTab(capture(), active).tabs[active]);
        if (typeof window.pcbsResetJobReview === 'function') window.pcbsResetJobReview(active);
        flush();
        return true;
    };
    function setup() {
        var state = codec.normalize(window.pcbsInitialState);
        restoreFields(document.querySelector('#divOptions form'), state.shared);
        restoreFields(document.getElementById('formLevel'), state.shared);
        syncFilters();
        codec.pages.forEach(function (page) { restoreTabFields(page, state.tabs[page].fields); });
        refreshDependentOptions();
        codec.pages.forEach(function (page) {
            restoreTabFields(page, state.tabs[page].fields);
            restoreOutputs(page, state.tabs[page]);
        });
        restoreHistory(state.history);
        var originalPage = window.showPage;
        window.showPage = function (page) { originalPage(page); active = page; notifyPage(); schedule(); };
        var originalLevelSet = window.isLevelSet;
        window.isLevelSet = function () { return career && career.valid && !career.applicable ? true : originalLevelSet(); };
        window.filterPartsForLevel = syncFilters;
        // The upstream score calculator intentionally uses the unrestricted data3DMark,
        // but its compatibility helper reads the global career-filtered data variable.
        var originalCalculate = window.CalculatorCalculate;
        window.CalculatorCalculate = function () {
            var careerData = data;
            data = data3DMark;
            try { return originalCalculate(); }
            finally { data = careerData; schedule(); }
        };
        showPage(state.activeTab);
        applyCareer(career);
        if (typeof window.pcbsRestoreJobReview === 'function') window.pcbsRestoreJobReview();
        restoring = false;
        ['input', 'change'].forEach(function (name) { document.addEventListener(name, function (event) {
            if (event.target.closest('#divOptions, #divLevel') && career && career.valid) applyCareer(career);
            schedule();
        }); });
        document.addEventListener('click', schedule);
        window.addEventListener('beforeunload', flush);
        window.addEventListener('blur', flush);
        setInterval(flush, 1000);
        console.log('PCBS calculator session restoration ready.');
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', setup);
    else setup();
})();
