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
const html = fs.readFileSync(path.join(__dirname, '../assets/calculator.html'), 'utf8');
const ui = html.slice(0, html.indexOf('<script id="Source Data">')).replace(/<(style|script)\b[\s\S]*?<\/\1>/g, '');
const unchanged = new Set(['CPU', 'GPU', 'GPU 1', 'GPU 2', 'M.2', '#',
    'PC Building Simulator - HTML Calculator v1.15.3 Build 1 - 3DMark Scores, Builds, Upgrades']);
const labels = new Set(Array.from(ui.matchAll(/>([^<>]+)</g), match => match[1].trim()).filter(Boolean));
labels.forEach(label => check(unchanged.has(label) || Object.hasOwn(locale.ru, label), 'Every upstream UI label is translated or intentionally canonical: ' + label));
const tooltips = new Set(Array.from(ui.matchAll(/(?:title|placeholder)="([^"]+)"/g), match => match[1]));
tooltips.forEach(label => check(/^\d+$/.test(label) || Object.hasOwn(locale.ru, label), 'Every tooltip and text placeholder is covered: ' + label));
const messages = new Set(Array.from(html.matchAll(/(?:alertMessage =|alert\()\s*"((?:\\.|[^"\\\r\n])*)"/g), match => JSON.parse('"' + match[1] + '"')).filter(Boolean));
messages.forEach(label => check(label === 'CPU only supports ' || Object.hasOwn(locale.ru, label), 'Every upstream validation alert is covered: ' + label));
console.log('Calculator localization: ' + checks + ' assertions passed.');
