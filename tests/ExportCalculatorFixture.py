"""Read PCBS's own TextAssets for offline score checks. Generated data stays under build/.

Geometry in this offline fixture is synthetic; actual slot layout is read by the plugin
from PartsDatabase.GetSlots. This fixture is not used or distributed by the helper.
"""
import argparse
import json
from pathlib import Path
import UnityPy
from bs4 import BeautifulSoup

parser = argparse.ArgumentParser()
parser.add_argument('--game-path', type=Path, required=True)
parser.add_argument('--output', type=Path, required=True)
args = parser.parse_args()
db = {key: {} for key in ['cpus','gpus','mobos','pcCases','rams','storages','coolers','psus']}
def rows(html):
    records = []
    header = None
    for tr in BeautifulSoup(html,'html.parser').find_all('tr'):
        cells = [c.get_text(' ',strip=True) for c in tr.find_all(['td','th'],recursive=False)]
        if 'ID' in cells or ('Global' in cells and 'Member' in cells):
            header = cells
        elif header:
            records.append(dict(zip(header,cells)))
    return records
def number(r,key,default=0):
    try: return float(r.get(key) or default)
    except ValueError: return default
def yes(r,key): return r.get(key)=='Yes'
def socket(value):
    return {'LGA 1151 (Kaby Lake)':'LGA_1151_SKY_KABY','LGA 1151 (Skylake)':'LGA_1151_SKY_KABY','LGA 1151 (Coffee Lake)':'LGA_1151_COFFEE'}.get(value,value.replace(' ','_'))
def size(value):
    return {'Mini-ITX':'MINI_ITX','Micro-ATX':'MICRO_ATX','S-ATX':'S_ATX','E-ATX':'E_ATX','XL-ATX':'XL_ATX','Mini-DTX':'MINI_DTX','SSI-EEB':'SSI_EEB','SSI-CEB':'SSI_CEB','Intel NUC':'INTEL_NUC','Intel Mini-STX':'INTEL_MINI_STX'}.get(value,value)
names={'CPU':'cpus','GPU':'gpus','Water Cooled GPU':'gpus','Motherboard':'mobos','Cases':'pcCases','RAM':'rams','Storage':'storages','Coolers':'coolers','PSU':'psus'}
env=UnityPy.load(str(args.game_path/'PCBS_Data/sharedassets1.assets'))
for obj in env.objects:
    if obj.type.name=='TextAsset':
        a=obj.read()
        if a.m_Name=='Constants': constants={r['Member']:number(r,'Value') for r in rows(a.m_Script)}
for obj in env.objects:
    if obj.type.name!='TextAsset': continue
    asset=obj.read()
    if asset.m_Name=='Constants':
        constants={r['Member']:number(r,'Value') for r in rows(asset.m_Script)}
        db['constants']={'minCpu':constants['s_min3DMarkCPU'],'minGpu':constants['s_min3DMarkGPU'],'minFps':constants['s_min3DMarkFPS'],'baseClock':constants['s_defaultBaseClock'],'maxMultiplierPercent':constants['s_maxCPUMultiplierPercentage']}
        db['constants']['heat']={'referenceCooling':constants['s_fanBaseCooling']+constants['s_refCpuCoolingAF']*constants['s_fanAirFlowToCPUCoolingFactor'],'idleFloor':constants['s_maxCPUCoolingFactorToIdle'],'room':constants['s_roomTemperature'],'psuLoad':constants['s_psuLoadTemp'],'gpuWattToHeat':constants['s_gpuWattToCelsius'],'caseHeat':constants['s_componentHeatToCaseHeat'],'feedbackThreshold':constants['s_caseTempFeedbackThreshold'],'feedbackFactor':constants['s_caseTempFeedbackFactor']}
        db['constants']['heat']['native']=constants
        db['constants']['heat'].update(gpuOverclockPower=constants['s_gpuOverclockPowerIncrease'],gpuUnderclockPower=constants['s_gpuUnderclockPowerDecrease'])
    if asset.m_Name not in names: continue
    group=names[asset.m_Name]
    for r in rows(asset.m_Script):
        if not r.get('ID') or r.get('Class')=='Dummy' or not yes(r,'In Game'): continue
        name=(r.get('Manufacturer','')+' '+r.get('Part Name','')).strip()
        p={'id':r['ID'],'fullName':name,'displayName':name,'manufacturer':r.get('Manufacturer',''),'price':number(r,'Price'),'sellPrice':number(r,'SellPrice'),'level':number(r,'Level'),'unlocked':number(r,'Level')<=13,'inShop':r.get('In Shop','No')}
        if group=='cpus':
            p.update(loadTemp=constants['s_cpuWattStartCelsius']+(number(r,'Wattage')-constants['s_cpuWattStartScale'])*constants['s_cpuWattToCelsius'],idleTemp=constants['s_cpuWattStartCelsius']+(number(r,'Wattage')-constants['s_cpuWattStartScale'])*constants['s_cpuWattToIdleCelsius'],dies=number(r,'Number of dies'),thermalLimit=number(r,'Thermal Throttling'),voltage=number(r,'Voltage'),maxVoltage=number(r,'Max Voltage'))
            p.update(socket=socket(r['CPU Socket']),baseMhz=number(r,'Frequency'),maxMhz=number(r,'Max Freq'),multiplierStep=number(r,'Multiplier Step'),maxChannels=number(r,'Max Memory Channels'),defaultRamMhz=number(r,'Default Memory Speed'),watts=number(r,'Wattage'),canOverclock=r['Can Overclock'],coefficients=[number(r,k) for k in ['CoreClockMultiplier','MemChannelsMultiplier','MemClockMultiplier','FinalAdjustment']])
        elif group=='gpus':
            p['maxVoltageIncrease']=number(r,'GPU % Power Increase')
            p['aio']=yes(r,'Is AIO')
            p.update(maxMemoryMhz=number(r,'GPU max mem clock'),loadTemp=50,thermalLimit=number(r,'GPU Thermal Throttling'))
            p.update(chipset=r['Chipset'],vendor=r['Chipset Brand'],vram=number(r,'VRAM (GB)'),baseMhz=number(r,'Base Core Clock Freq'),maxMhz=number(r,'GPU max clock'),tunerMaxMhz=(int(number(r,'GPU max clock')+int((number(r,'GPU max clock')-number(r,'Base Core Clock Freq'))*0.25))//100+1)*100,memoryMhz=number(r,'Base Mem Clock Freq'),watts=number(r,'Wattage'),length=number(r,'Length'),slots=number(r,'Slot Size'),multiGpu=yes(r,'Double GPU supported'),sli=yes(r,'Double GPU SLI'),gpuType='Water' if asset.m_Name=='Water Cooled GPU' else 'Air',tests=[[number(r,f'{test} {count} {term}') for term in ['Core Clock Multiplier','Mem Clock Multiplier','Benchmark Adjustment']] for test in ['GT1','GT2'] for count in ['Single','Dual']])
        elif group=='mobos':
            p.update(socket=socket(r['CPU Socket']),size=size(r['Motherboard Size']),ramType=r['Ram Type'],canOverclock=r['Can Overclock'],sli=yes(r,'Support SLI'),crossfire=yes(r,'Support Crossfire'),memorySpeeds=sorted(int(s) for s in r['Memory Speed Steps'].split(',') if s),ramSlots=4,gpuSlots=2,gpuPositions=[0,3],m2Slots=1,m2Heatsinks=[0],pciSlots=7)
        elif group=='pcCases':
            p.update(maxPsuLength=number(r,'Max PSU length'),psuSizes=[v.strip().replace('-','_') for v in r['PSU size'].split(',') if v.strip()])
            p.update(maxCoolerHeight=number(r,'Max CPU Fan Height'),radiator120=number(r,'MaxRadiatorLength120mm'),radiator140=number(r,'MaxRadiatorLength140mm'),cooling=number(r,'Inherent Cooling'))
            p.update(sizes=[size(s.strip()) for s in r['Motherboard Size'].split(',') if s.strip()],maxGpuLength=number(r,'Max GPU length'),gpuPositions=[0,3],pciSlots=7)
        elif group=='coolers':
            p['airflow']=number(r,'Air Flow')
            p.update(water=r['Part Type']=='Liquid Cooler',height=number(r,'Height'),size=number(r,'Size'),slots=number(r,'Slots'),cooling=(constants['s_fanBaseCooling']+number(r,'Air Flow')*constants['s_fanAirFlowToCPUCoolingFactor'])*(constants['s_waterCoolingBonus'] if r['Part Type']=='Liquid Cooler' else 1),caseCooling=number(r,'Air Flow')*constants['s_fanAirFlowToCaseCoolingFactor'],sockets=[socket(v.strip()) for v in r['CPU Socket List'].split(',') if v.strip()])
        elif group=='psus':
            p.update(watts=number(r,'Wattage'),length=number(r,'Length'),size=r['Size'].replace('-','_'))
        elif group=='rams':
            p.update(sizeGb=number(r,'Size each (GB)'),speedMhz=number(r,'Frequency'),ramType=r['Ram Type'])
            name+=f' {int(p["sizeGb"])} GB {int(p["speedMhz"])} MHz'
        else:
            p.update(sizeGb=number(r,'Size (GB)'),heatsink=number(r,'Heatsink Thickness'),storageType=r.get('Type'))
        p['fullName']=name
        if name in db[group]: name+=' ['+p['id']+']';p['fullName']=name
        db[group][name]=p
args.output.parent.mkdir(parents=True,exist_ok=True)
args.output.write_text(json.dumps(db,ensure_ascii=True),encoding='utf-8')
print(json.dumps({k:len(v) for k,v in db.items()}))
