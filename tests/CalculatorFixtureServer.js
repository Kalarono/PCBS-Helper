'use strict';
// An isolated headless-browser fixture; never connects to the running game or user data.
const fs = require('node:fs');
const path = require('node:path');
const http = require('node:http');
const project = path.resolve(__dirname, '..');
function asset(name) { return fs.readFileSync(path.join(project, 'assets', name), 'utf8'); }
function pageHtml() {
let html = asset('helper-calculator.html');
html = html.replace('<head>', '<head><script>window.pcbsErrors=[];window.addEventListener("error",function(e){pcbsErrors.push(e.message);});</script>');
const bridge = 'window.pcbsInitialState=JSON.parse(sessionStorage.getItem("pcbs-fixture-state")||"null");window.pcbsInitialCareer={valid:true,applicable:true,level:13,percent:25.123456};' +
    'window.pcbsGameCatalog=' + fs.readFileSync(path.join(project,'build/game-catalog/helper-catalog.json'),'utf8') + ';' +
    'window.pcbsFixtureAlerts=[];window.alert=function(message){pcbsFixtureAlerts.push(message);};' +
    'window.pcbsSaveCalculator=function(json){window.pcbsCaptured=JSON.parse(json);};window.pcbsCalculatorTab=function(allowed){window.pcbsClearEnabled=allowed;};';
html = html.replace('</body>', '<script>' + bridge + '</script><script>window.pcbsInitialLanguage=new URLSearchParams(location.search).get("lang")||"en";</script><script>' +
    asset('calculator-state-codec.js') + '</script><script>' + asset('calculator-job-rules.js') + '</script><script>' +
    asset('helper-calculator-engine.js') + '</script><script>' + asset('helper-calculator-app.js') + '</script><script>' + asset('embedded-ui.js') + '</script><script>' + asset('calculator-state.js') + '</script><script>' + asset('calculator-localization.js') + '</script><script>' + asset('calculator-build-order.js') + '</script></body>');
return html;
}
const server = http.createServer((req, res) => { res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' }); res.end(pageHtml()); });
server.listen(0, '127.0.0.1', () => console.log('Fixture URL: http://127.0.0.1:' + server.address().port + '/'));
