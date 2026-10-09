(function () {
    'use strict';
    var pages = ['divBuildMaker', 'divBuildUpgrader'];
    var activeRules = null;
    function getForm(page) { return document.getElementById(page === pages[0] ? 'formBuildMaker' : 'formBuildUpgrader'); }
    function field(form, name, value) {
        var control = form.elements.namedItem(name);
        if (!control) return;
        if (control.type === 'checkbox') control.checked = !!value;
        else {
            if (control.tagName === 'SELECT' && !Array.prototype.some.call(control.options, function (o) { return o.value === String(value); }))
                control.add(new Option(String(value), String(value)));
            control.value = value == null ? '' : String(value);
        }
    }
    function canon(name, group) {
        if (!name || sourceData[group][name]) return name || '';
        function norm(s) { return String(s).toLowerCase().replace(/[®™]/g, '').replace(/\s+/g, ' ').trim(); }
        return Object.keys(sourceData[group]).filter(function (key) { return norm(key) === norm(name); })[0] || name;
    }
    function extraControl(form, name, label, values) {
        var tr = document.createElement('tr'), title = document.createElement('td'), cell = document.createElement('td');
        var select = document.createElement('select'); select.name = name;
        title.textContent = label;
        values.forEach(function (value) { select.add(new Option(value, value)); });
        cell.appendChild(select); tr.appendChild(title); tr.appendChild(cell);
        form.querySelector('tbody').insertBefore(tr, form.querySelector('tbody').lastElementChild);
    }
    function setupControls() {
        pages.forEach(function (page) {
            var form = getForm(page);
            extraControl(form, 'pcbsCpuBrand', 'CPU Brand', ['Any', 'Intel', 'AMD']);
            var brands = ['Any', 'NVIDIA', 'AMD'];
            Object.keys(sourceData.gpus).forEach(function (name) {
                var brand = sourceData.gpus[name].manufacturer;
                if (brands.indexOf(brand) < 0) brands.push(brand);
            });
            extraControl(form, 'pcbsGpuBrand', 'GPU Vendor / Brand', brands);
            [['Cpu', 'CPU', 'listCpus'], ['Gpu', 'GPU', 'listGpus'], ['Case', 'Case', 'listCases'],
                ['Mobo', 'Motherboard', 'listMobos'], ['Ram', 'RAM', '']].forEach(function (spec) {
                if (page === pages[1] && (spec[0] === 'Case' || spec[0] === 'Ram')) return;
                var row = document.createElement('tr'), label = document.createElement('td'), cell = document.createElement('td');
                var control = document.createElement('input'); control.name = 'pcbsFixed' + spec[0];
                row.hidden = true;
                row.setAttribute('data-pcbs-required', '');
                control.placeholder = 'Any'; if (spec[2]) control.setAttribute('list', spec[2]);
                label.textContent = 'Required ' + spec[1]; cell.appendChild(control); row.appendChild(label); row.appendChild(cell);
                form.querySelector('tbody').insertBefore(row, form.querySelector('tbody').lastElementChild);
            });
            var metadata = document.createElement('input'); metadata.type = 'hidden'; metadata.name = 'pcbsJobMetadata';
            form.appendChild(metadata);
            var review = document.createElement('div'); review.id = page + 'JobReview'; review.className = 'pcbs-job-review';
            form.parentNode.insertBefore(review, form);
        });
    }
    function metadata(page) {
        try { return JSON.parse(getForm(page).elements.namedItem('pcbsJobMetadata').value || 'null'); } catch (_) { return null; }
    }
    function render(page, extra) {
        var job = metadata(page), review = document.getElementById(page + 'JobReview');
        review.textContent = ''; review.style.display = job ? 'block' : 'none';
        if (!job) return;
        var russian = typeof window.pcbsLanguage === 'string' ? window.pcbsLanguage === 'ru' : !!job.ru;
        function translate(text) { return typeof window.pcbsTranslate === 'function' ? window.pcbsTranslate(text) : text; }
        var heading = document.createElement('strong');
        heading.textContent = (russian ? 'Задание №' : 'Job #') + job.jobId + ': ' + job.title;
        review.appendChild(heading);
        var container = review;
        function line(text) { var p = document.createElement('p'); p.textContent = translate(text); container.appendChild(p); }
        line((russian ? 'Режим: ' : 'Mode: ') + translate(page === pages[0] ? 'Build Maker' : 'Build Upgrader'));
        var conditions = document.createElement('details'), conditionTitle = document.createElement('summary');
        conditionTitle.textContent = translate('Job objectives and calculation limits');
        conditions.appendChild(conditionTitle); review.appendChild(conditions); container = conditions;
        (job.notes || []).concat(extra || []).forEach(line);
        if (page === pages[1]) PCBSJobRules.upgradeBrandRules(job.brands, sourceData).deferred.forEach(function (rule) {
            line(russian
                ? 'Условие по марке ' + rule.brand + ' (' + Math.max(1, Number(rule.count) || 1) + '): проверьте отдельно в игре. Подбор апгрейда заменяет только CPU, GPU и плату; это условие не ограничивает варианты апгрейда.'
                : 'Brand objective ' + rule.brand + ' (' + Math.max(1, Number(rule.count) || 1) + '): check separately in the game. Build Upgrader selects only CPU, GPU and motherboard; this objective does not restrict score upgrades.');
        });
        var list = document.createElement('ul');
        (job.requirements || []).forEach(function (text) { var item = document.createElement('li'); item.textContent = text; list.appendChild(item); });
        container.appendChild(list);
        var system = job.system || {}, parts = [];
        ['pcCase', 'cpu', 'mobo', 'cooler', 'psu'].forEach(function (key) { if (system[key]) parts.push(system[key]); });
        ['ram', 'gpus', 'm2s', 'disks'].forEach(function (key) { parts = parts.concat(system[key] || []); });
        if (parts.length) {
            var details = document.createElement('details'), summary = document.createElement('summary');
            summary.textContent = translate('All original hardware'); details.appendChild(summary);
            if (system.cpuMhz) parts.push('CPU: ' + system.cpuMhz + (russian ? ' МГц' : ' MHz'));
            if (system.gpuMhz) parts.push('GPU: ' + system.gpuMhz + (russian ? ' МГц' : ' MHz'));
            var p = document.createElement('p'); p.textContent = parts.join('\n'); p.style.whiteSpace = 'pre-line'; details.appendChild(p); container.appendChild(details);
        }
    }
    window.pcbsRestoreJobReview = function () { pages.forEach(function (page) { render(page); }); };
    window.pcbsResetJobReview = function (page) { if (pages.indexOf(page) >= 0) render(page); };
    window.pcbsImportJob = function (job) {
        if (!job || pages.indexOf(job.tab) < 0) return;
        showPage(job.tab); pcbsClearCurrentTab();
        var form = getForm(job.tab), options = job.fields || {}, system = job.system || {};
        var maker = job.tab === pages[0], prefix = maker ? 'BuildMaker' : 'BuildUpgrader';
        field(form, 'input' + prefix + 'Budget', job.budget);
        field(form, 'input' + prefix + 'Target3DMarkScore', job.target);
        field(form, 'input' + prefix + 'ReservedBudget', maker ? 400 : 0);
        field(form, 'pcbsCpuBrand', options.cpuBrand || 'Any'); field(form, 'pcbsGpuBrand', options.gpuBrand || 'Any');
        [['Cpu', 'cpus'], ['Gpu', 'gpus'], ['Case', 'pcCases'], ['Mobo', 'mobos'], ['Ram', 'rams']].forEach(function (spec) {
            field(form, 'pcbsFixed' + spec[0], canon(options['fixed' + spec[0]], spec[1]));
        });
        field(form, maker ? 'selectBuildMakerGpuCount' : 'selectBuildUpgraderOptionsGpuCount', options.gpuCount || 'Any');
        field(form, maker ? 'selectBuildMakerGpuType' : 'selectBuildUpgraderOptionsGpuType', options.gpuType || 'Any');
        if (maker) {
            ['Motherboard', 'Case', 'RamPart'].forEach(function (suffix) { field(form, 'inputBuildMakerInclude' + suffix, true); });
            field(form, 'inputBuildMakerNeedCpuOverclock', options.cpuOverclock);
        } else {
            field(form, 'inputBuildUpgraderOriginalSystemCase', canon(system.pcCase, 'pcCases'));
            field(form, 'inputBuildUpgraderOriginalSystemM2', canon((system.m2s || [])[0], 'storages'));
            field(form, 'inputBuildUpgraderOriginalSystemCpu', canon(system.cpu, 'cpus'));
            field(form, 'inputBuildUpgraderOriginalSystemGpu', canon((system.gpus || [])[0], 'gpus'));
            field(form, 'inputBuildUpgraderRamSticks', system.ramSticks || '');
            field(form, 'selectBuildUpgraderOriginalSystemGpuCount', (system.gpus || []).length || 1);
            field(form, 'selectBuildUpgraderOriginalSystemRamSpeed', system.ramSpeed || '');
            field(form, 'selectBuildUpgraderOriginalSystemMobo', canon(system.mobo, 'mobos'));
        }
        field(form, 'pcbsJobMetadata', JSON.stringify(job));
        if (!maker && sourceData.cpus[form.inputBuildUpgraderOriginalSystemCpu.value] && sourceData.gpus[form.inputBuildUpgraderOriginalSystemGpu.value]) {
            var old = data;
            try { data = calculationData(optionsFor(job.tab)); updateMoboOptionsBuildUpgrader(); }
            finally { data = old; }
        }
        render(job.tab);
        pcbsFlush();
    };
    function optionsFor(page) {
        var form = getForm(page), job = metadata(page) || {}, options = job.fields || {};
        var upgrade = page === pages[1];
        var fixed = {};
        [['Cpu', 'cpu'], ['Gpu', 'gpu'], ['Case', 'pcCase'], ['Mobo', 'mobo'], ['Ram', 'ram']].forEach(function (spec) {
            var control = form.elements.namedItem('pcbsFixed' + spec[0]); fixed[spec[1]] = control ? control.value : '';
        });
        return { upgrade: upgrade, brands: upgrade ? PCBSJobRules.upgradeBrandRules(job.brands, sourceData).applied : job.brands || [], fixed: fixed, cpuBrand: form.elements.namedItem('pcbsCpuBrand').value,
            gpuBrand: form.elements.namedItem('pcbsGpuBrand').value,
            gpuCount: form.elements.namedItem(upgrade ? 'selectBuildUpgraderOptionsGpuCount' : 'selectBuildMakerGpuCount').value,
            gpuType: form.elements.namedItem(upgrade ? 'selectBuildUpgraderOptionsGpuType' : 'selectBuildMakerGpuType').value,
            cpuOverclock: upgrade ? !!options.cpuOverclock : form.inputBuildMakerNeedCpuOverclock.checked,
            system: upgrade ? { cpu: form.inputBuildUpgraderOriginalSystemCpu.value,
                gpu: form.inputBuildUpgraderOriginalSystemGpu.value, gpuCount: form.selectBuildUpgraderOriginalSystemGpuCount.value,
                mobo: form.selectBuildUpgraderOriginalSystemMobo.value, pcCase: form.inputBuildUpgraderOriginalSystemCase.value,
                otherParts: (job.system || {}).otherParts || [] } : {} };
    }
    function calculationData(options) {
        var result = Object.assign({}, data);
        ['cpus', 'gpus', 'mobos', 'pcCases', 'storages', 'rams'].forEach(function (group) {
            result[group] = Object.create(null);
            Object.keys(data[group]).forEach(function (name) {
                var allowed = group === 'cpus' ? PCBSJobRules.brand(data[group][name], options.cpuBrand, false) :
                    group === 'gpus' ? PCBSJobRules.brand(data[group][name], options.gpuBrand, true) : true;
                var key = { cpus: 'cpu', gpus: 'gpu', mobos: 'mobo', pcCases: 'pcCase', rams: 'ram' }[group];
                if (key && options.fixed[key] && name !== options.fixed[key]) allowed = false;
                if (allowed) result[group][name] = data[group][name];
            });
        });
        if (options.upgrade) {
            var form = getForm(pages[1]);
            [['cpus', form.inputBuildUpgraderOriginalSystemCpu.value], ['gpus', form.inputBuildUpgraderOriginalSystemGpu.value],
                ['mobos', form.selectBuildUpgraderOriginalSystemMobo.value], ['pcCases', form.inputBuildUpgraderOriginalSystemCase.value],
                ['storages', form.inputBuildUpgraderOriginalSystemM2.value]].forEach(function (entry) {
                // Installed hardware may be above the current level, or outside a requested brand.
                // It remains readable for scoring, but is not enumerated as a new purchase.
                if (entry[1] && !result[entry[0]][entry[1]] && sourceData[entry[0]][entry[1]])
                    Object.defineProperty(result[entry[0]], entry[1], { value: sourceData[entry[0]][entry[1]], enumerable: false });
            });
        }
        return result;
    }
    function wrap(name, page) {
        var original = window[name];
        window[name] = function () {
            var oldData = data, oldRules = activeRules, options = optionsFor(page);
            data = calculationData(options); activeRules = options;
            try {
                var result = original.apply(this, arguments);
                var table = document.getElementById(page === pages[0] ? 'tableBuildMakerResults' : 'tableBuildUpgraderResults');
                var removed = 0;
                if (page === pages[0]) Array.prototype.slice.call(table.rows, 1).forEach(function (row) {
                    var c = row.cells;
                    var candidate = { pcCase: c[0].textContent, cpu: c[1].textContent, ramChannel: c[2].textContent,
                        ram: c[4].textContent, gpuCount: c[5].textContent, gpu: c[7].textContent, mobo: c[8].textContent };
                    if (!PCBSJobRules.valid(candidate, options, data)) { row.parentNode.removeChild(row); removed++; }
                });
                if (page === pages[0]) Array.prototype.slice.call(table.rows, 1).forEach(function (row, i) {
                    var save = row.querySelector('.buttonSave'); if (save) save.id = i + 1;
                });
                if (removed) render(page, [metadata(page) && metadata(page).ru
                    ? 'Скрыты варианты, не проходящие дополнительные условия по марке. При необходимости увеличьте # Results.'
                    : 'Results failing additional brand conditions were hidden. Increase # Results if needed.']);
                pcbsFlush(); return result;
            } finally { data = oldData; activeRules = oldRules; }
        };
    }
    function setup() {
        setupControls();
        var originalLimit = window.limitResultsToFirstLastAndRandomMiddle;
        window.limitResultsToFirstLastAndRandomMiddle = function (results, n) {
            if (activeRules) results = results.filter(function (candidate) { return PCBSJobRules.valid(candidate, activeRules, data); });
            return originalLimit(results, n);
        };
        wrap('BuildMakerGetBuilds', pages[0]); wrap('BuildUpgraderGetUpgrades', pages[1]);
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', setup);
    else setup();
})();
