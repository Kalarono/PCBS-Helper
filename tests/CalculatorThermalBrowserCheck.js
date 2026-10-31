(function () {
    var checks=0;
    function check(value,message) { checks++; if(!value) throw new Error(message); }
    var availability={};
    ['cpus','gpus','mobos','pcCases','rams','coolers','psus'].forEach(function(group) {
        Object.keys(sourceData[group]).forEach(function(name) { var part=sourceData[group][name]; availability[part.id]=part.level<=16; });
    });
    pcbsSetCatalogAvailability(availability);
    pcbsUpdateCareer({valid:true,applicable:true,level:16,percent:0});
    var all=document.querySelector('[name="inputOptionAllPartsUnlocked"]'); if(all.checked) all.click();
    pcbsImportJob({tab:'divBuildMaker',jobId:105,title:'4800 МГц и 7025 баллов',budget:2500,target:7025,
        fields:{targetCpuMhz:4800,allNewParts:true},system:{},brands:[{brand:'Intel',count:1}],requirements:[],notes:[]});
    BuildMakerGetBuilds();
    var rows=Array.prototype.slice.call(document.getElementById('tableBuildMakerResults').rows,1);
    check(rows.length>0,'The reported mixed score/CPU quest has visible results.');
    rows.forEach(function(row) {
        var cpu=sourceData.cpus[row.querySelector('.tdCpu').textContent],cooler=sourceData.coolers[row.querySelector('.tdCoolerPart').textContent];
        check(cpu.manufacturer==='Intel' && Number(row.querySelector('.tdCpuMhz').textContent)>=4800 && Number(row.querySelector('.tdScore').textContent)>=7025,'The UI retains all three hardware targets.');
        check(cooler && row.querySelector('.tdCpuThermal').textContent.indexOf('°C')>=0,'Cooling and temperature are included in every result.');
    });
    var request;
    window.pcbsOrderBuild=function(json) { request=JSON.parse(json); pcbsBuildOrderResult({id:request.id,ok:true,count:request.items.reduce(function(n,p){return n+p.quantity;},0)}); };
    rows[0].querySelector('.pcbs-order-button').click();
    check(request.items.length===7 && request.items.some(function(p){return p.kind==='psu';}),'The complete seven-role order includes cooling and a PSU.');
    pcbsImportJob({tab:'divBuildMaker',jobId:595,title:'4747 / 2071 MHz, ARCTIC',budget:2250,target:9191,
        fields:{targetCpuMhz:4747,targetGpuMhz:2071},system:{},brands:[{brand:'ARCTIC',count:1,newOnly:true}],requirements:[],notes:[]});
    var form=document.getElementById('formBuildMaker');
    form.inputBuildMakerReservedBudget.value='400'; form.inputBuildMakerNeedCpuOverclock.checked=true;
    form.inputBuildMakerTarget3DMarkScoreOffset.value='';
    BuildMakerGetBuilds();
    check(document.getElementById('tableBuildMakerResults').rows.length>1,'Air-cooled GPU recommendations also fit with the normal $400 reserve.');
    form.inputBuildMakerBudget.value='100'; BuildMakerGetBuilds();
    check(pcbsFixtureAlerts[pcbsFixtureAlerts.length-1].indexOf('Резерв бюджета тоже ограничивает подбор.')>=0,'No-results feedback explains that the reserve also limits selection.');
    form.inputBuildMakerReservedBudget.value='';
    pcbsImportJob({tab:'divBuildMaker',jobId:595,title:'4747 / 2071 MHz, ARCTIC',budget:2250,target:9191,
        fields:{targetCpuMhz:4747,targetGpuMhz:2071,cpuOverclock:true},system:{},brands:[{brand:'ARCTIC',count:1,newOnly:true}],requirements:[],notes:[]});
    check(form.inputBuildMakerReservedBudget.value==='','Importing the same quest preserves a deliberately empty reserve.');
    BuildMakerGetBuilds();
    rows=Array.prototype.slice.call(document.getElementById('tableBuildMakerResults').rows,1);
    check(rows.length>0,'The ARCTIC quest from the second screenshot has visible results.');
    var keys=rows.map(function(row){return ['.tdCpu','.tdGpuPart','.tdMobo'].map(function(css){return row.querySelector(css).textContent;}).join('|');});
    check(new Set(keys).size===keys.length,'The UI never repeats a CPU/GPU/motherboard trio with other RAM.');
    rows.forEach(function(row) {
        var cooler=sourceData.coolers[row.querySelector('.tdCoolerPart').textContent];
        check(cooler.manufacturer==='ARCTIC','Every result includes an ARCTIC cooler.');
        check(row.querySelector('.tdGpuType').textContent==='Air' && row.querySelector('.tdPsuPart').textContent && /^\$\d+/.test(row.querySelector('.tdPrice').textContent),'Air cooling, PSU and an explicit purchase price are present.');
        check(Number(row.querySelector('.tdCpuMhz').textContent)>=4747 && Number(row.querySelector('.tdGpuMhz').textContent)>=2071 && Number(row.querySelector('.tdScore').textContent)>=9191,'ARCTIC does not bypass any clock or score goal.');
    });
    rows[0].querySelector('.pcbs-order-button').click();
    check(request.items.some(function(p){return p.kind==='cooler' && sourceData.coolers[p.name].manufacturer==='ARCTIC';}),'The order includes the required manufacturer.');
    form.inputBuildMakerReservedBudget.value='300';
    pcbsImportJob({tab:'divBuildMaker',jobId:596,title:'Another job',budget:2250,target:9191,
        fields:{targetCpuMhz:4747,targetGpuMhz:2071,cpuOverclock:true},system:{},brands:[{brand:'ARCTIC',count:1,newOnly:true}],requirements:[],notes:[]});
    check(form.inputBuildMakerReservedBudget.value==='300','A custom non-zero reserve is preserved across different jobs as well.');
    BuildMakerGetBuilds();
    rows=Array.prototype.slice.call(document.getElementById('tableBuildMakerResults').rows,1);
    check(rows.length>0,'The same quest also fits with a deliberate $300 reserve for the remaining parts.');
    showPage('divBuildMaker'); pcbsFlush();
    sessionStorage.setItem('pcbs-fixture-state',JSON.stringify(pcbsCaptured));
    check(pcbsErrors.length===0,'The thermal result has no browser errors.');
    return {passed:checks,cpu:rows[0].querySelector('.tdCpu').textContent,cooler:rows[0].querySelector('.tdCoolerPart').textContent,thermal:rows[0].querySelector('.tdCpuThermal').textContent};
})();
