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
console.log('Calculator state: ' + checks + ' assertions passed.');
