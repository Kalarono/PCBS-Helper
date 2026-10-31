'use strict';
const assert = require('node:assert/strict');
const db = require('../build/game-catalog/helper-catalog.json');
const oracle = require('../build/game-catalog/game-thermal-oracle.json');
const engine = require('../assets/helper-calculator-engine.js');
let checks = 0;
function check(value, message) { checks++; assert.ok(value, message); }
for (const sample of oracle) {
    const result = engine.cpuThermals(db.cpus[sample.cpu], db.gpus[sample.gpu], sample.count,
        sample.cpuMhz, sample.gpuMhz, sample.memoryMhz, db.pcCases[sample.pcCase], db.coolers[sample.cooler], db.constants);
    check(Math.abs(result.temperature - sample.temperature) < 0.001, 'The JS estimate matches native ComputerSave.GetCPULoadInternalTemp.');
    check(Math.abs(result.gpuTemperature-sample.gpuTemperature)<0.001,'Air GPU temperature matches the native full-load model.');
    check(engine.gpuVoltage(db.gpus[sample.gpu],sample.gpuMhz,sample.memoryMhz,db.constants)===Math.max(100,Math.ceil(sample.gpuVoltage)),'Required GPU power percentage matches the native combined clock model.');
    check(engine.peakWatts(db.cpus[sample.cpu],db.gpus[sample.gpu],sample.count,sample.cpuMhz,sample.gpuMhz,sample.memoryMhz,db.constants)===sample.watts,'PSU sizing uses the native peak consumption including overclocking.');
}
for (const group of ['cpus','gpus','mobos','pcCases','rams','coolers'])
    for (const p of Object.values(db[group])) p.unlocked = p.level <= 16;
const cpu = db.cpus['Intel Core i3-8350K'], gpu = db.gpus['MSI Radeon R9 390 GAMING 8G'];
const pcCase = db.pcCases['SilverStone PS14 (Black + Window)'];
const z63 = db.coolers['NZXT Kraken Z63'], x72 = db.coolers['NZXT Kraken X72'];
check(engine.coolerFits(z63, cpu, pcCase), 'A 280mm Z63 fits the reported PS14 case.');
check(!engine.coolerFits(x72, cpu, pcCase), 'A 360mm X72 is excluded from the PS14 case.');
check(!engine.coolerFits({...z63,sockets:['AM4']}, cpu, pcCase), 'The cooler must support the CPU socket.');
check(!engine.coolerFits({...z63,water:false,height:166}, cpu, pcCase), 'Air cooler height must fit the case.');
check(engine.cpuThermals(cpu,gpu,2,4800,gpu.baseMhz,0,pcCase,z63,db.constants).temperature > cpu.thermalLimit,
    'The reported 8350K / Z63 / dual R9 390 at 4800 MHz is recognized as overheating.');
const options = {target:7025,targetCpuMhz:4800,targetGpuMhz:0,budget:2500,reserve:400,results:5,offset:500,
    fixed:{},brands:[{brand:'Intel',count:1}],includeCase:true,includeMobo:true,includeRam:true};
const filters = {allUnlocked:false,notInShop:false};
const builds = engine.maker(db, options, filters);
check(builds.length > 0, 'The exact score / frequency / Intel / level16 quest still has solutions.');
for (const b of builds) {
    const p = db.cpus[b.cpu], cooler = db.coolers[b.cooler];
    check(engine.coolerFits(cooler,p,db.pcCases[b.pcCase]), 'Each recommendation includes compatible cooling.');
    check(b.cpuTemp <= p.thermalLimit - 5 && b.cpuVoltage <= p.maxVoltage, 'Every plan retains thermal and voltage headroom.');
    check(cooler.unlocked && cooler.inShop === 'Yes', 'Unavailable coolers cannot rescue an OC plan.');
    check(b.price + b.remainingReserve <= options.budget && b.remainingReserve === Math.max(0, options.reserve - cooler.price - db.psus[b.psu].price),
        'Cooling is paid from the reserve once.');
    check(b.cpuMhz >= 4800 && b.score >= 7025 && p.manufacturer === 'Intel', 'Thermal selection preserves all job goals.');
}
const first = builds[0];
const fixed = {cpu:first.cpu,gpu:first.gpu,mobo:first.mobo,pcCase:first.pcCase,ram:first.ram};
const one = engine.maker(db,{...options,fixed},filters)[0];
check(engine.maker({...db,coolers:{}},{...options,fixed,targetCpuMhz:0,target:1,offset:20000},filters).length === 0, 'A priced complete build cannot omit its CPU cooler.');
const locked = {...db,coolers:Object.fromEntries(Object.entries(db.coolers).map(([key,p])=>[key,{...p,unlocked:false}]))};
check(engine.maker(locked,{...options,fixed},filters).length === 0, 'Locked cooling rejects an otherwise valid OC plan.');
check(engine.maker(db,{...options,fixed,budget:one.price+one.remainingReserve-1},filters).length === 0,
    'A budget one dollar below the exact complete plan is rejected.');
check(engine.maker(db,{...options,fixed,budget:one.price+one.remainingReserve},filters).length > 0,
    'The exact budget remains valid without double-charging cooling.');
for(const group of ['cpus','gpus','mobos','pcCases','rams','coolers'])
    for(const p of Object.values(db[group])) p.unlocked=p.level<=17;
const arcticJob={...options,target:9191,targetCpuMhz:4747,targetGpuMhz:2071,budget:2250,reserve:0,offset:undefined,
    cpuOverclock:true,gpuCount:'Any',gpuType:'Any',results:3,brands:[{brand:'ARCTIC',count:1,newOnly:true}]};
const arcticBuilds=engine.maker(db,arcticJob,filters);
check(arcticBuilds.length>0,'The second screenshot quest finds ARCTIC cooling without a hidden score ceiling.');
for(const b of arcticBuilds) {
    check(db.coolers[b.cooler].manufacturer==='ARCTIC','The selected cooler really satisfies the imported ARCTIC goal.');
    check(b.score>=9191 && b.cpuMhz>=4747 && b.gpuMhz>=2071,'All three score/frequency goals remain enforced.');
    check(b.price<=2250 && b.cpuTemp<=db.cpus[b.cpu].thermalLimit-5,'Brand selection retains the budget and thermal margin.');
}
check(engine.maker(db,{...arcticJob,offset:500},filters).every(b=>b.score<=9691),'An explicitly entered score ceiling is still respected.');
const ab=arcticBuilds[0];
const arcticUpgrade={...arcticJob,upgrade:true,fixed:{cpu:ab.cpu,gpu:ab.gpu,mobo:ab.mobo},system:{cpu:ab.cpu,gpu:ab.gpu,
    mobo:ab.mobo,pcCase:ab.pcCase,gpuCount:ab.gpuCount,ramSticks:ab.ramChannel,ramSpeed:ab.ramSpeed,cooler:'Mortoni Thermal 100'}};
const au=engine.upgrader(db,arcticUpgrade,filters);
check(au.length>0 && db.coolers[au[0].cooler].manufacturer==='ARCTIC' && au[0].changedCooler,'The upgrade path can replace cooling to meet a new ARCTIC requirement.');
const replaceSame=engine.upgrader({...db,coolers:{[ab.cooler]:db.coolers[ab.cooler]}},
    {...arcticUpgrade,system:{...arcticUpgrade.system,cooler:ab.cooler}},filters);
check(replaceSame.length>0 && replaceSame[0].changedCooler && replaceSame[0].price===db.coolers[ab.cooler].price,'A new-only brand requirement can buy a new unit of the same cooler model.');
check(new Set(arcticBuilds.map(b=>JSON.stringify([b.cpu,b.gpu,b.mobo]))).size===arcticBuilds.length,'RAM variants cannot duplicate a CPU/GPU/motherboard trio.');
check(arcticBuilds.every(b=>b.gpuType==='Air'),'Ordinary build results never include unpriced custom-loop GPUs.');
for(const b of arcticBuilds) {
 const p=db.psus[b.psu],cs=db.pcCases[b.pcCase];
 check(p.watts>=Math.ceil(b.watts*1.1)&&cs.psuSizes.includes(p.size)&&p.length<=cs.maxPsuLength,'The PSU fits the case and has a power margin.');
 const total=db.cpus[b.cpu].price+db.gpus[b.gpu].price*b.gpuCount+db.mobos[b.mobo].price+db.pcCases[b.pcCase].price+db.rams[b.ram].price*b.ramChannel+db.coolers[b.cooler].price+p.price;
 check(b.price===total,'The displayed build price equals every selected purchase including PSU and cooler.');
}
const proven=engine.maker(db,{...arcticJob,results:10,fixed:{cpu:'Intel Core i7-8700K',gpu:'EVGA GeForce GTX 1070 FTW2 GAMING',mobo:'ASUS TUF Z370-PRO GAMING'}},filters);
check(proven.length===1 && proven[0].gpuMhz>=2071 && proven[0].price<=2250,'The user-proven air-cooled GTX1070 build is found exactly once within budget.');
const ftw=db.gpus['EVGA GeForce GTX 1070 FTW2 GAMING'];
check(engine.gpuClock(ftw,2071,db.constants)===2071,'2071 MHz on an air GTX1070 is feasible with stock VRAM despite exceeding the nominal core OC point.');
check(engine.gpuClock(ftw,2071,db.constants,2300)===0,'Raising both core and memory beyond the voltage envelope is rejected.');
const caseA={...db.pcCases[ab.pcCase],id:'tiny-case',fullName:'Tiny',price:33,maxPsuLength:100};
const caseB={...caseA,id:'roomy-case',fullName:'Roomy',price:50,maxPsuLength:200};
const pricedDb={...db,pcCases:{Tiny:caseA,Roomy:caseB},psus:{Tiny:{id:'tiny-psu',fullName:'Tiny PSU',size:'ATX',length:80,watts:1000,price:200,unlocked:true,inShop:'Yes'},Big:{id:'big-psu',fullName:'Big PSU',size:'ATX',length:180,watts:1000,price:100,unlocked:true,inShop:'Yes'}}};
const priceOptions={...arcticJob,target:1,targetCpuMhz:0,targetGpuMhz:0,cpuOverclock:false,brands:[],reserve:400,budget:10000,
    fixed:{cpu:ab.cpu,gpu:ab.gpu,mobo:ab.mobo,ram:ab.ram}};
const roomy=engine.maker(pricedDb,priceOptions,filters)[0];
const base=roomy.price-caseB.price-100-roomy.coolerPrice;
const tight=engine.maker(pricedDb,{...priceOptions,budget:base+440},filters);
check(tight.length>0&&tight[0].pcCase==='Tiny','When PSU prices consume the reserve, a feasible case/PSU combination is not lost behind a cheaper but budget-infeasible alternative.');
console.log('CPU/GPU thermals, complete prices and distinct builds: '+checks+' assertions passed.');
