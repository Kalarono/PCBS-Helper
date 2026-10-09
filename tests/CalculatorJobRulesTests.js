'use strict';
const assert = require('node:assert/strict');
const rules = require('../assets/calculator-job-rules.js');
let checks = 0;
function check(value, message) { checks++; assert.ok(value, message); }
const db = {
    cpus: { Intel: { manufacturer: 'Intel', fullName: 'Intel CPU', canOverclock: 'Yes' },
        AMD: { manufacturer: 'AMD', fullName: 'AMD CPU', canOverclock: 'No' } },
    gpus: { Nvidia: { manufacturer: 'MSI', fullName: 'MSI GeForce', gpuType: 'Air' },
        Radeon: { manufacturer: 'MSI', fullName: 'MSI Radeon', gpuType: 'Air' },
        Water: { manufacturer: 'AMD', fullName: 'AMD Radeon Water', gpuType: 'Water' } },
    mobos: { Board: { manufacturer: 'MSI', canOverclock: 'Yes' }, Locked: { manufacturer: 'MSI', canOverclock: 'No' } }, pcCases: { Case: { manufacturer: 'NZXT' } },
    rams: { RAM: { manufacturer: 'CORSAIR' } }
};
const build = { cpu: 'Intel', gpu: 'Nvidia', gpuCount: 1, mobo: 'Board', pcCase: 'Case', ram: 'RAM', ramChannel: 2 };
check(rules.valid(build, {}, db), 'Unconstrained builds remain valid.');
check(rules.valid(build, { cpuBrand: 'Intel' }, db), 'Intel requirement accepts an Intel CPU.');
check(!rules.valid(build, { cpuBrand: 'AMD' }, db), 'AMD CPU requirement rejects Intel.');
check(rules.valid(build, { gpuBrand: 'NVIDIA' }, db), 'GPU vendor is distinct from board manufacturer.');
check(!rules.valid(build, { gpuBrand: 'AMD' }, db), 'AMD GPU vendor rejects GeForce.');
check(rules.valid({ ...build, gpu: 'Radeon' }, { gpuBrand: 'AMD' }, db), 'AMD GPU vendor accepts third-party Radeon.');
check(!rules.valid(build, { gpuCount: '2' }, db), 'Two-GPU goal rejects a single card.');
check(rules.valid({ ...build, gpuCount: '1 + 1' }, { gpuCount: '2' }, db), 'One-plus-one upgrade produces two installed GPUs.');
check(!rules.valid(build, { gpuType: 'Water' }, db), 'Water goal rejects air-cooled GPUs.');
check(rules.valid({ ...build, gpu: 'Water' }, { gpuType: 'Water' }, db), 'Water goal accepts water-cooled GPU.');
check(!rules.valid({ ...build, cpu: 'AMD' }, { cpuOverclock: true }, db), 'Overclock goal rejects a locked CPU.');
check(!rules.valid({ ...build, mobo: 'Locked' }, { cpuOverclock: true }, db), 'Overclock goal also requires a compatible overclock-capable motherboard.');
check(rules.valid(build, { fixed: { cpu: 'Intel', pcCase: 'Case' } }, db), 'Exact part constraints allow a match.');
check(!rules.valid(build, { fixed: { cpu: 'AMD' } }, db), 'Exact CPU constraint rejects other parts.');
check(!rules.valid(build, { fixed: { ram: 'Different RAM' } }, db), 'Exact RAM constraint rejects other parts.');
const original = { cpu: 'Intel', gpu: 'Nvidia', gpuCount: 1, mobo: 'Board', pcCase: 'Case' };
const hyperX = { brand: 'HyperX', count: 1, newOnly: true };
const intelGoal = { brand: 'Intel', count: 1, newOnly: true };
const msiGoal = { brand: 'MSI', count: 2, newOnly: true };
const split = rules.upgradeBrandRules([hyperX, intelGoal, msiGoal], db);
check(split.deferred.length === 1 && split.deferred[0] === hyperX, 'A HyperX RAM goal is deferred outside the CPU/GPU/motherboard upgrade search.');
check(split.applied.length === 2 && split.applied[0] === intelGoal && split.applied[1] === msiGoal, 'CPU and GPU/motherboard manufacturer goals remain enforced.');
check(rules.valid({ cpu: 'Intel' }, { upgrade: true, system: original, brands: split.applied.slice(0, 1) }, db), 'A deferred ancillary brand goal no longer rejects valid CPU-only score upgrades.');
check(rules.upgradeBrandRules([{ brand: 'NVIDIA', count: 1, newOnly: true }], db).deferred.length === 1, 'Manufacturer goals still distinguish a GPU vendor from its board manufacturer.');
check(rules.upgradeBrandRules([], db).applied.length === 0, 'Manual upgrades with no job brand rules stay unconstrained.');
check(!rules.valid({ gpu: 'Water', gpuCount: 1 }, { upgrade: true, system: original, cpuBrand: 'AMD' }, db), 'GPU-only upgrades cannot bypass a CPU brand goal.');
check(!rules.valid({ cpu: 'AMD' }, { upgrade: true, system: original, gpuCount: '2' }, db), 'CPU-only upgrades cannot bypass two-GPU goal.');
check(!rules.valid({ cpu: 'AMD' }, { upgrade: true, system: original, gpuType: 'Water' }, db), 'CPU-only upgrades cannot bypass GPU cooling goal.');
check(rules.valid({ ...build, gpu: 'Water' }, { brands: [{ brand: 'AMD', count: 1, newOnly: true }] }, db), 'General AMD brand condition can be met by a GPU, without forcing AMD CPU.');
check(!rules.valid({ ...build, gpu: 'Radeon' }, { brands: [{ brand: 'AMD', count: 1, newOnly: true }] }, db), 'General manufacturer goal does not mistake MSI Radeon for an AMD-made part.');
check(!rules.valid({ gpu: 'Water', gpuCount: '1 + 1' }, { upgrade: true, system: original,
    brands: [{ brand: 'AMD', count: 2, newOnly: true }] }, db), 'Adding one card counts only one new brand component.');
check(rules.valid(build, { brands: [{ brand: 'CORSAIR', count: 2, newOnly: true }] }, db), 'Two RAM modules count as two new parts.');
check(!rules.valid({ gpu: 'Water' }, { upgrade: true, system: original,
    brands: [{ brand: 'Intel', count: 1, newOnly: true }] }, db), 'An unchanged Intel CPU does not satisfy a new-part brand requirement.');
check(rules.valid({ gpu: 'Water' }, { upgrade: true, system: original,
    brands: [{ brand: 'Intel', count: 1, newOnly: false }] }, db), 'A DLC installed-brand goal may use retained hardware.');
console.log('Calculator job rules: ' + checks + ' assertions passed.');
