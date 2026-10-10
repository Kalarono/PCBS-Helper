'use strict';
const assert = require('node:assert/strict');
const codec = require('../assets/calculator-state-codec.js');
let checks = 0;
function check(condition, message) { checks++; assert.ok(condition, message); }
const input = { version: 1, activeTab: codec.pages[1], shared: { inputOptionAllPartsUnlocked: { value: 'on', checked: true } }, tabs: {}, history: [
    { key: '1700000000000', value: { type: 'build', cpu: 'Saved CPU', comment: 'Keep this build' } }
] };
codec.pages.forEach((page, i) => { input.tabs[page] = { fields: { budget: { value: String(1000 + i), checked: false } },
    texts: { score: String(i * 100) }, rows: [[{ text: 'Result ' + i, css: 'tdCpu', save: i === 1 }]] }; });
const restored = codec.normalize(JSON.parse(JSON.stringify(codec.normalize(input))));
check(restored.activeTab === codec.pages[1], 'Active tab survives a save/restart roundtrip.');
codec.pages.forEach((page, i) => check(restored.tabs[page].fields.budget.value === String(1000 + i) && restored.tabs[page].rows[0][0].text === 'Result ' + i,
    'Every calculation tab preserves its own fields and results.'));
const cleared = codec.clearTab(restored, codec.pages[1]);
check(Object.keys(cleared.tabs[codec.pages[1]].fields).length === 0 && cleared.tabs[codec.pages[1]].rows.length === 0, 'Clear removes the selected tab only.');
[0, 2, 3].forEach(i => check(JSON.stringify(cleared.tabs[codec.pages[i]]) === JSON.stringify(restored.tabs[codec.pages[i]]), 'Clear must preserve other tabs.'));
check(cleared.shared.inputOptionAllPartsUnlocked.checked && cleared.history[0].value.comment === 'Keep this build', 'Clear preserves shared filters and saved history.');
check(JSON.stringify(codec.clearTab(restored, codec.historyPage)) === JSON.stringify(restored), 'History is not one of the four Clear targets.');
check(codec.normalize({ version: 999 }).activeTab === codec.pages[0], 'Unknown state versions fall back safely.');
const malformed = codec.normalize({ version: 1, activeTab: 'bad', tabs: { [codec.pages[0]]: { rows: [[{ text: '<b>text</b>', css: 'x" onclick="bad()', save: true }]] } },
    history: [{ key: 'not-a-timestamp', value: { type: 'build' } }] });
check(malformed.activeTab === codec.pages[0] && malformed.history.length === 0 && malformed.tabs[codec.pages[0]].rows[0][0].css === '', 'Malformed storage is bounded and sanitized.');
const upgradeColumns=['cpu','gpuCount','gpuType','gpu','mobo','price','remaining','score','watts'];
const cell=(text,css='')=>({text,css,save:false});
const legacy=[cell('Intel Pentium G4600','tdCpu'),cell('1 + 1','tdGpuCount'),cell('Air','tdGpuType'),cell('GIGABYTE AORUS GeForce GTX 1070 8G'),cell(''),cell('640'),cell('860'),cell('6993'),cell('381')];
let rows=codec.resultRows(codec.pages[2],{rows:[legacy]},upgradeColumns);
check(rows[0][1].text==='1 + 1'&&rows[0][2].text==='Air','The user-reported legacy GPU quantity/cooling retain their correct columns.');
const interim=[cell('CPU'),cell('2400'),cell('2'),cell('GPU'),cell('Board'),cell('100'),cell('200'),cell('5000'),cell('300')];
rows=codec.resultRows(codec.pages[2],{rows:[interim]},upgradeColumns);
check(rows[0][1].text==='2'&&rows[0][2].css==='tdGpuType'&&rows[0][3].text==='GPU','The interim RAM-MHz/GPU-count layout is migrated without shifting GPU, price or score.');
check(interim[1].text==='2400','Migration does not mutate the original saved snapshot.');
const reversed={columns:upgradeColumns.slice().reverse(),rows:[legacy.slice().reverse()]};
rows=codec.resultRows(codec.pages[2],reversed,upgradeColumns);
check(rows[0][1].text==='1 + 1'&&rows[0][2].text==='Air','Named columns restore correctly regardless of stored order.');
check(codec.resultRows(codec.pages[2],{columns:['cpu'],rows:[[cell('CPU')]]},upgradeColumns).length===0,'Unknown result layouts are not rendered under unrelated headings.');
check(codec.resultRows(codec.pages[3],{rows:[legacy]},['cpu','gpu','gpu2','mobo','price','remaining','score']).length===0,'Old replacement results with incompatible widths require recalculation.');
console.log('Calculator state: ' + checks + ' assertions passed.');
