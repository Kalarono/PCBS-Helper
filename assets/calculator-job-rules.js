(function (root) {
    'use strict';
    function count(value) { return value === '1 + 1' ? 2 : Number(value) || 0; }
    function same(a, b) { return String(a || '').toLowerCase() === String(b || '').toLowerCase(); }
    function brand(part, wanted, gpu) {
        if (!wanted || wanted === 'Any') return true;
        if (!part) return false;
        if (same(part.manufacturer, wanted)) return true;
        var name = (part.fullName || '') + ' ' + (part.chipset || '');
        if (gpu && same(wanted, 'NVIDIA')) return /GeForce|TITAN|Quadro/i.test(name);
        if (gpu && same(wanted, 'AMD')) return /Radeon/i.test(name);
        return false;
    }
    function valid(candidate, options, db) {
        var system = options.system || {}, isUpgrade = options.upgrade;
        var cpuName = candidate.cpu || (isUpgrade ? system.cpu : '');
        var gpuName = candidate.gpu || (isUpgrade ? system.gpu : '');
        var gpuCount = count(candidate.gpuCount || (isUpgrade ? system.gpuCount : 0));
        var cpu = db.cpus[cpuName], gpu = db.gpus[gpuName];
        if (!brand(cpu, options.cpuBrand, false) || !brand(gpu, options.gpuBrand, true)) return false;
        if (options.gpuCount && options.gpuCount !== 'Any' && gpuCount !== count(options.gpuCount)) return false;
        if (options.gpuType && options.gpuType !== 'Any' && (!gpu || gpu.gpuType !== options.gpuType)) return false;
        if (options.cpuOverclock && (!cpu || cpu.canOverclock !== 'Yes')) return false;
        var motherboard = db.mobos[candidate.mobo || system.mobo];
        if (options.cpuOverclock && (!motherboard || motherboard.canOverclock !== 'Yes')) return false;
        var fixed = options.fixed || {};
        if ((fixed.cpu && !same(cpuName, fixed.cpu)) || (fixed.gpu && !same(gpuName, fixed.gpu))) return false;
        if ((fixed.pcCase && !same(candidate.pcCase || system.pcCase, fixed.pcCase)) ||
            (fixed.mobo && !same(candidate.mobo || system.mobo, fixed.mobo)) ||
            (fixed.ram && !same(candidate.ram, fixed.ram))) return false;
        return (options.brands || []).every(function (rule) {
            var n = 0;
            var parts = [ ['cpus', 'cpu', 1], ['gpus', 'gpu', gpuCount], ['mobos', 'mobo', 1],
                ['pcCases', 'pcCase', 1], ['rams', 'ram', count(candidate.ramChannel)] ];
            parts.forEach(function (spec) {
                var name = candidate[spec[1]] || (!rule.newOnly && isUpgrade ? system[spec[1]] : '');
                var part = db[spec[0]] && db[spec[0]][name];
                var quantity = spec[2];
                // Adding one second card counts as one new component, not two purchases.
                if (spec[1] === 'gpu' && candidate.gpuCount === '1 + 1' && rule.newOnly) quantity = 1;
                if (part && same(part.manufacturer, rule.brand)) n += quantity;
            });
            if (!rule.newOnly && isUpgrade) (system.otherParts || []).forEach(function (part) {
                if (same(part.manufacturer, rule.brand)) n++;
            });
            return n >= Math.max(1, Number(rule.count) || 1);
        });
    }
    var api = { brand: brand, valid: valid };
    if (typeof module !== 'undefined' && module.exports) module.exports = api;
    else root.PCBSJobRules = api;
})(typeof window !== 'undefined' ? window : this);
