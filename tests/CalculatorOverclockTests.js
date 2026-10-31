"use strict";
const assert = require("node:assert/strict");
const db = require("../build/game-catalog/helper-catalog.json");
const engine = require("../assets/helper-calculator-engine.js");
const filters = { allUnlocked: false, notInShop: false };
Object.values(db).forEach(group => Object.values(group).forEach(p => {
  if (p && p.id) p.unlocked = p.level <= 16;
}));
const options = {
  target: 0, targetCpuMhz: 4700, targetGpuMhz: 2071, offset: 500,
  budget: 2000, reserve: 400, results: 10, fixed: {}, brands: [],
  gpuCount: "Any", gpuType: "Any", includeCase: true, includeMobo: true, includeRam: true
};
let checks = 0;
function check(value, label) { checks++; assert.ok(value, label); }
const builds = engine.maker(db, options, filters);
check(builds.length > 0, "The screenshot quest has solutions at level 16.");
for (const b of builds) {
  const cpu = db.cpus[b.cpu], gpu = db.gpus[b.gpu], board = db.mobos[b.mobo];
  check(b.price + b.remainingReserve <= options.budget, "The parts, CPU cooling and remaining reserve fit the quest budget.");
  check(b.cpuMhz >= 4700 && b.gpuMhz >= 2071, "Both frequency goals are met.");
  check(b.cpuMhz <= cpu.maxMhz && b.gpuMhz <= gpu.tunerMaxMhz && b.gpuVoltage<=100+gpu.maxVoltageIncrease, "Native tuner and combined GPU voltage limits are used without a silicon bonus.");
  check(b.cpuMhz <= cpu.baseMhz || cpu.canOverclock === "Yes" && board.canOverclock === "Yes", "CPU overclock needs an unlocked CPU and board.");
  check([cpu, gpu, board, db.rams[b.ram], db.pcCases[b.pcCase]].every(p => p.unlocked && p.inShop === "Yes"), "Locked or unavailable hardware is excluded.");
  check(b.score === engine.score(db, { ...b, cpu: b.cpu, gpu: b.gpu }).total, "Score is calculated at the proposed clocks.");
}
const first = builds[0], cpu = db.cpus[first.cpu], gpu = db.gpus[first.gpu];
check(engine.cpuClock(cpu, 4701, db.constants) >= 4701, "CPU target rounds up to the native multiplier step.");
check(engine.cpuClock({ ...cpu, maxMhz: 4701 }, 4701, db.constants) === 0, "Rounded frequency must still fit the part's limit.");
check(engine.cpuClock({ ...cpu, multiplierStep: 0 }, 4700, db.constants) === 0, "A CPU without multiplier adjustment cannot be selected for OC.");
check(engine.cpuClock({ ...cpu, canOverclock: "No" }, 4700, db.constants) === 0, "Locked CPUs are rejected above stock.");
check(engine.cpuClock({ ...cpu, baseMhz: 4800, canOverclock: "No" }, 4700, db.constants) === 4800, "A native frequency goal can be met at stock clocks.");
check(engine.cpuClock({ ...cpu, maxMhz: 20000 }, 10000, db.constants) === 0, "BIOS multiplier cap is checked independently of silicon limits.");
check(engine.gpuClock({ ...gpu, tunerMaxMhz: 2000 }, 2071) === 0, "GPU Tuner range is enforced.");
check(engine.gpuClock({ ...gpu, maxMhz: undefined }, 2071) === 0, "Unknown OC capability is not guessed.");
const exact = { ...options, fixed: { cpu: first.cpu, gpu: first.gpu, mobo: first.mobo, pcCase: first.pcCase, ram: first.ram } };
check(engine.maker(db, { ...exact, budget: first.price + first.remainingReserve - 1 }, filters).length === 0, "Budget includes the remaining-component reserve without charging the cooler twice.");
const lockedBoard = { ...db, mobos: { ...db.mobos, [first.mobo]: { ...db.mobos[first.mobo], canOverclock: "No" } } };
check(engine.maker(lockedBoard, exact, filters).length === 0, "A non-OC motherboard cannot sneak through a fixed part constraint.");
check(engine.maker(db, { ...exact, targetCpuMhz: 100000 }, filters).length === 0, "Impossible CPU targets produce no results.");
check(engine.maker(db, { ...exact, targetGpuMhz: 100000 }, filters).length === 0, "Impossible GPU targets produce no results.");
check(engine.maker(db, { ...exact, brands: [{ brand: "Nonexistent", count: 1 }] }, filters).length === 0, "Brand constraints still apply to OC jobs.");
const mixed = engine.maker(db, { ...exact, target: first.score, offset: 0 }, filters);
check(mixed.length > 0 && mixed.every(b => b.score === first.score), "Mixed 3DMark and OC goals apply together.");
check(engine.maker(db, { ...exact, target: first.score + 100000 }, filters).length === 0, "An OC match does not bypass the score objective.");
for (const targets of [{ targetCpuMhz: 4700, targetGpuMhz: 0 }, { targetCpuMhz: 0, targetGpuMhz: 2071 }])
  check(engine.maker(db, { ...exact, ...targets }, filters).length > 0, "Single-component OC jobs work independently.");
const system = { cpu: first.cpu, gpu: first.gpu, mobo: first.mobo, pcCase: first.pcCase,
  gpuCount: 1, ramSticks: 1, ramSpeed: first.ramSpeed, cpuMhz: cpu.baseMhz, gpuMhz: gpu.baseMhz, cooler: first.cooler };
const upgradeOptions = { ...exact, fixed: { ...exact.fixed, ram: "" }, upgrade: true, system };
const up = engine.upgrader(db, upgradeOptions, filters);
check(up.length > 0 && up[0].price === 0, "Existing OC-capable hardware can meet the job without a purchase.");
check(up[0].cpuMhz >= 4700 && up[0].gpuMhz >= 2071, "A zero-cost upgrade still provides the requested clock plan.");
check(engine.upgrader(lockedBoard, upgradeOptions, filters).length === 0, "The upgrade path also rejects a locked motherboard.");
console.log("Overclock quest selection: " + checks + " assertions passed.");
