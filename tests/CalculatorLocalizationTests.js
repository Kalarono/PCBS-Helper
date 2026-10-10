'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const locale = require('../assets/calculator-localization.js');
let checks = 0;
function check(value, message) { checks++; assert.ok(value, message); }
check(locale.language('ru-RU') === 'ru' && locale.language('RU') === 'ru', 'Russian game locale variants are supported.');
check(locale.language('en-US') === 'en' && locale.language('de') === 'en', 'Other game languages fall back to English.');
check(locale.text('  Budget\n', 'ru') === '  Бюджет\n', 'Translation preserves surrounding whitespace.');
check(locale.text('CPU only supports 2 RAM Channels.', 'ru') === 'Процессор поддерживает только 2 канала ОЗУ.', 'Parameterized validation messages are translated.');
check(locale.text('CPU only supports 1 RAM Channels.', 'ru') === 'Процессор поддерживает только 1 канал ОЗУ.', 'A single channel uses the singular Russian form.');
check(locale.text('CPU only supports 14 RAM Channels.', 'ru') === 'Процессор поддерживает только 14 каналов ОЗУ.', 'Russian number inflection handles teen values.');
check(locale.text('Intel Core i7-7700K', 'ru') === 'Intel Core i7-7700K', 'Hardware model names are unchanged.');
check(locale.text('constructor', 'ru') === 'constructor', 'Unknown text cannot resolve inherited dictionary properties.');
Object.entries(locale.ru).forEach(([english, russian]) => {
    check(locale.text(english, 'ru') === russian, 'Russian translation: ' + english);
    check(locale.text(russian, 'en') === english, 'English round trip: ' + english);
});
['CPU Clock (MHz)','GPU Clock (MHz)','GPU Memory Clock (MHz)','Load','Delete','Part',
    'CPU Request','GPU 1 Request','GPU 2 Request','Motherboard Request'].forEach(label=>check(Object.hasOwn(locale.ru,label),'New calculator labels are translated: '+label));
console.log('Calculator localization: ' + checks + ' assertions passed.');
