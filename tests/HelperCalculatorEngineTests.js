"use strict";
const assert = require("node:assert/strict"),
  fs = require("node:fs"),
  path = require("node:path");
const engine = require("../assets/helper-calculator-engine.js");
const db = JSON.parse(
  fs.readFileSync(
    path.join(__dirname, "../build/game-catalog/helper-catalog.json"),
    "utf8",
  ),
);
const cases = JSON.parse(
  fs.readFileSync(
    path.join(__dirname, "../build/game-catalog/game-score-oracle.json"),
    "utf8",
  ),
);
let checks = 0;
for (const sample of cases) {
  const actual =
    sample.kind === "cpu"
      ? engine.cpuScore(
          db.cpus[sample.name],
          sample.speed,
          sample.channels,
          sample.mhz,
          db.constants,
        )
      : engine.gpuScore(
          db.gpus[sample.name],
          sample.count,
          sample.mhz,
          sample.memory,
          db.constants,
        );
  assert.equal(actual, sample.score, JSON.stringify(sample));
  checks++;
}
const cpu = "Intel Core i7-7700K",
  gpu = "EVGA GeForce GTX 1080 GAMING";
const result = engine.score(db, {
  cpu,
  gpu,
  ramSpeed: 2133,
  ramChannel: 1,
  gpuCount: 1,
});
assert.equal(result.total, 6495);
checks++;
assert.throws(
  () =>
    engine.score(db, { cpu, gpu, ramSpeed: 2133, ramChannel: 4, gpuCount: 1 }),
  /RAM Channels/,
);
checks++;
const noDual = Object.values(db.gpus).find((p) => !p.multiGpu);
assert.throws(
  () => engine.gpuScore(noDual, 2, 0, 0, db.constants),
  /multi-GPU/,
);
checks++;
const filters = { allUnlocked: true, notInShop: false };
const options = {
  target: 6400,
  offset: 500,
  budget: 5000,
  reserve: 400,
  results: 10,
  fixed: {
    cpu,
    gpu,
    pcCase: "ADATA XPG Battlecruiser (Black)",
    mobo: "Mortoni H170 Mtech",
    ram: "ADATA Premier Series 4 GB 2133 MHz",
  },
  brands: [],
  cpuBrand: "Intel",
  gpuBrand: "NVIDIA",
  gpuCount: "1",
  gpuType: "Air",
  includeCase: true,
  includeMobo: true,
  includeRam: true,
};
const builds = engine.maker(db, options, filters);
assert.ok(builds.length);
checks++;
for (const b of builds) {
  assert.equal(b.cpu, cpu);
  assert.equal(b.gpu, gpu);
  assert.ok(b.price + 400 <= 5000);
  assert.ok(b.score >= 6400 && b.score <= 6900);
  assert.equal(b.pcCase, options.fixed.pcCase);
  checks += 5;
}
const up = {
  target: 4831,
  offset: 500,
  budget: 1300,
  reserve: 0,
  results: 10,
  upgrade: true,
  fixed: {},
  brands: [],
  cpuBrand: "Any",
  gpuBrand: "Any",
  gpuCount: "Any",
  gpuType: "Any",
  system: {
    cpu: "Intel Pentium G4400",
    gpu: "NVIDIA GeForce GTX 1080 Founders Edition",
    mobo: "GIGABYTE GA-B250M-Gaming 5",
    pcCase: "MSI MPG Gungnir 110R",
    ramSticks: 2,
    ramSpeed: 2400,
    gpuCount: 1,
  },
};
const upgrades = engine.upgrader(db, up, filters);
assert.equal(upgrades.length, 10);
checks++;
assert.equal(upgrades[0].cpu, "Intel Pentium G4520");
checks++;
assert.equal(upgrades[0].price, 90);
checks++;
assert.ok(upgrades.every((b) => b.score >= 4831 && b.price <= 1300));
checks++;
const pending = { ...db, geometryReady: false };
assert.throws(() => engine.maker(pending, options, filters), /Preparing/);
checks++;
assert.equal(
  engine.score(pending, {
    cpu,
    gpu,
    ramSpeed: 2133,
    ramChannel: 1,
    gpuCount: 1,
  }).total,
  6495,
);
checks++;
const branded = engine.maker(
  db,
  {
    ...options,
    fixed: { ...options.fixed, pcCase: "" },
    brands: [{ brand: "NZXT", count: 1, newOnly: true }],
  },
  filters,
);
assert.ok(
  branded.length &&
    branded.every((b) => db.pcCases[b.pcCase].manufacturer === "NZXT"),
);
checks++;
const narrow = { ...db, cpus: {}, mobos: {} };
for (const name of [up.system.cpu, "AMD Ryzen 5 Quad Core 1500X"])
  narrow.cpus[name] = db.cpus[name];
for (const name of [up.system.mobo, "GIGABYTE GA-AB350-Gaming 3"])
  narrow.mobos[name] = db.mobos[name];
const replaced = engine.replacer(
  narrow,
  {
    system: up.system,
    results: 10,
    budget: 600,
    requests: {
      cpu: "Upgrade",
      gpu: "Any",
      gpu2: "Any",
      mobo: "Replace / Fix",
    },
  },
  filters,
);
assert.ok(
  replaced.length &&
    replaced.every(
      (r) =>
        r.cpu === "AMD Ryzen 5 Quad Core 1500X" &&
        r.mobo === "GIGABYTE GA-AB350-Gaming 3" &&
        r.price <= 600,
    ),
);
checks++;
const locked = db.gpus[up.system.gpu];
const lockedDb = {
  ...db,
  gpus: { ...db.gpus, [up.system.gpu]: { ...locked, unlocked: false } },
};
const lockedUp = engine.upgrader(
  lockedDb,
  { ...up, gpuCount: "2", fixed: { gpu: up.system.gpu } },
  { allUnlocked: false, notInShop: false },
);
assert.equal(lockedUp.length, 0);
checks++;
const rules = require("../assets/calculator-job-rules.js");
assert.equal(
  rules.valid(
    {
      cpu: up.system.cpu,
      gpu: up.system.gpu,
      gpuCount: 1,
      mobo: up.system.mobo,
      changedCpu: false,
      changedGpu: true,
      changedMobo: false,
    },
    {
      upgrade: true,
      system: up.system,
      brands: [{ brand: "Intel", count: 1, newOnly: true }],
    },
    db,
  ),
  false,
);
checks++;
console.log("Independent calculator engine: " + checks + " assertions passed.");
