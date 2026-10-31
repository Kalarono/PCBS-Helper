// PCBS-Helper's independent calculator. Inputs are loaded from the installed game.
(function (root) {
  "use strict";
  var f = Math.fround;
  function positive(value, fallback) {
    var n = Number(value);
    return isFinite(n) && n > 0 ? n : fallback;
  }
  function integer(value, fallback) {
    var n = Number(value);
    return Number.isInteger(n) && n > 0 ? n : fallback;
  }
  function cpuScore(cpu, ramMhz, channels, cpuMhz, constants) {
    if (!cpu) throw new Error("CPU not found.");
    channels = integer(channels, 1);
    if (channels > cpu.maxChannels)
      throw new Error(
        "CPU only supports " + cpu.maxChannels + " RAM Channels.",
      );
    var c = cpu.coefficients;
    var fps = f(
      f(
        f(f(c[0]) * f(positive(cpuMhz, cpu.baseMhz))) +
          f(f(c[1]) * f(channels)),
      ) + f(f(c[2]) * f(positive(ramMhz, cpu.defaultRamMhz))),
    );
    fps = Math.max(f(fps + f(c[3])), f(constants.minFps));
    return Math.max(Math.trunc(f(f(298) * fps)), constants.minCpu);
  }
  function gpuScore(gpu, count, coreMhz, memoryMhz, constants) {
    if (!gpu) throw new Error("GPU not found.");
    count = integer(count, 1);
    if (count > 2 || (count === 2 && !gpu.multiGpu))
      throw new Error("Selected GPU does not support multi-GPU.");
    var fps = [0, 1].map(function (test) {
      var c = gpu.tests[test * 2 + count - 1];
      return f(
        f(
          f(f(c[0]) * f(positive(coreMhz, gpu.baseMhz))) +
            f(f(c[1]) * f(positive(memoryMhz, gpu.memoryMhz))),
        ) + f(c[2]),
      );
    });
    if (
      fps.some(function (value) {
        return !(value > 0) || !isFinite(value);
      })
    )
      throw new Error("Invalid GPU clocks.");
    return Math.max(
      constants.minGpu,
      Math.trunc(f(f(328) / f(f(f(1) / fps[0]) + f(f(1) / fps[1])))),
    );
  }
  function combine(cpu, gpu) {
    return Math.trunc(f(f(1) / f(f(f(0.85) / f(gpu)) + f(f(0.15) / f(cpu)))));
  }
  function score(db, input) {
    var cpu = db.cpus[input.cpu],
      gpu = db.gpus[input.gpu];
    var count = integer(input.gpuCount, 1),
      constants = db.constants;
    var c = cpuScore(
      cpu,
      input.ramSpeed,
      input.ramChannel,
      input.cpuMhz,
      constants,
    );
    var g = gpuScore(gpu, count, input.gpuMhz, input.gpuMemoryMhz, constants);
    return {
      cpu: c,
      gpu: g,
      total: combine(c, g),
      watts: cpu.watts + count * gpu.watts + 30,
      cpuPrice: cpu.price,
      cpuUsed: cpu.sellPrice,
      gpuPrice: count * gpu.price,
      gpuUsed: count * gpu.sellPrice,
      gpuType: gpu.gpuType,
    };
  }
  function available(part, filters) {
    return (
      (filters.notInShop || part.inShop === "Yes") &&
      (filters.allUnlocked || part.unlocked)
    );
  }
  function sorted(group, filters) {
    return Object.keys(group)
      .map(function (key) {
        return group[key];
      })
      .filter(function (p) {
        return available(p, filters);
      })
      .sort(function (a, b) {
        return a.price - b.price || a.fullName.localeCompare(b.fullName);
      });
  }
  function compatible(cpu, gpu, board, pcCase, count, sticks, disk) {
    if (!cpu || !gpu || !board || cpu.socket !== board.socket) return false;
    if (sticks > board.ramSlots || board.gpuSlots < count) return false;
    if (
      count === 2 &&
      (!gpu.multiGpu || (gpu.sli ? !board.sli : !board.crossfire))
    )
      return false;
    if (
      pcCase &&
      (pcCase.sizes.indexOf(board.size) < 0 ||
        (pcCase.maxGpuLength > 0 && gpu.length > pcCase.maxGpuLength))
    )
      return false;
    if (pcCase && pcCase.useCaseGpuSlots && pcCase.gpuSlots < count)
      return false;
    var positions =
      pcCase && pcCase.useCaseGpuSlots
        ? pcCase.gpuPositions
        : board.gpuPositions;
    if (
      pcCase &&
      !pcCase.useCaseGpuSlots &&
      pcCase.pciSlots &&
      positions.slice(0, count).some(function (pos) {
        return pos + gpu.slots - 1 > pcCase.pciSlots;
      })
    )
      return false;
    if (
      count === 2 &&
      positions.length > 1 &&
      Math.abs(positions[1] - positions[0]) < gpu.slots
    )
      return false;
    if (
      disk &&
      (!board.m2Slots ||
        !board.m2Heatsinks.some(function (height) {
          return !height || disk.heatsink <= height;
        }))
    )
      return false;
    return true;
  }
  function matches(part, fixed, brand, gpu) {
    return (
      (!fixed || part.fullName === fixed) &&
      root.PCBSJobRules.brand(part, brand, gpu)
    );
  }
  function limit(options) {
    return Math.max(1, Math.min(200, integer(options.results, 10)));
  }
  function cpuClock(cpu, target, constants) {
    target = positive(target, cpu.baseMhz);
    if (target <= cpu.baseMhz) return cpu.baseMhz;
    var base = positive(constants.baseClock, 100), step = positive(cpu.multiplierStep, 0);
    if (cpu.canOverclock !== "Yes" || !step || !positive(cpu.maxMhz, 0)) return 0;
    var multiplier = Math.ceil(target / base / step) * step;
    var maxMultiplier = Math.trunc(Math.trunc(cpu.baseMhz * positive(constants.maxMultiplierPercent, 200) / 100) / base);
    var mhz = Math.trunc(f(f(base) * f(multiplier)));
    return multiplier <= maxMultiplier && mhz >= target && mhz <= cpu.maxMhz ? mhz : 0;
  }
  function gpuPowerFactor(gpu, core, memory, constants) {
    var h = constants && constants.heat || {};
    function factor(value, base, maximum) {
      var n = f((value - base) / (maximum + 1 - base));
      return n > 1 ? f(1 + f(f(n - 1) * positive(h.gpuOverclockPower, 3))) : n < 0 ? f(n * positive(h.gpuUnderclockPower, 0.1)) : n;
    }
    return f(f(factor(core, gpu.baseMhz, gpu.maxMhz) + factor(positive(memory, gpu.memoryMhz), gpu.memoryMhz, gpu.maxMemoryMhz)) / 2);
  }
  function gpuVoltage(gpu, core, memory, constants) {
    return Math.max(100, Math.ceil(f(100 + f(gpu.maxVoltageIncrease * gpuPowerFactor(gpu, core, memory, constants)))));
  }
  function gpuClock(gpu, target, constants, memory) {
    target = positive(target, gpu.baseMhz);
    if (target <= gpu.baseMhz) return gpu.baseMhz;
    return Number.isInteger(target) && positive(gpu.maxMhz, 0) && target <= gpu.tunerMaxMhz &&
      gpuVoltage(gpu, target, memory, constants) <= 100 + gpu.maxVoltageIncrease ? target : 0;
  }
  function cpuHeatFactor(cpu, mhz, constants) {
    var h = constants.heat, oc = f((mhz - cpu.baseMhz) / (cpu.maxMhz + 1 - cpu.baseMhz));
    var reference = f(f(cpu.loadTemp / cpu.dies) * f(1 - h.referenceCooling));
    return f(f(reference + f(f(cpu.thermalLimit - reference) * oc)) / reference);
  }
  function peakWatts(cpu, gpu, count, cpuMhz, gpuMhz, memory, constants) {
    var cpuHeat = cpuHeatFactor(cpu, cpuMhz, constants);
    var gpuHeat = f(f(gpu.loadTemp + f(f(gpu.thermalLimit - gpu.loadTemp) * gpuPowerFactor(gpu, gpuMhz, memory, constants))) / gpu.loadTemp);
    return Math.trunc(f(f(30 + f(cpu.watts * cpuHeat)) + f(f(gpu.watts * gpuHeat) * count)));
  }
  function psuPlan(psus, pcCase, watts) {
    return psus.find(function (p) {
      return p.watts >= Math.ceil(watts * 1.1) && pcCase.psuSizes.indexOf(p.size) >= 0 &&
        (!pcCase.maxPsuLength || p.length <= pcCase.maxPsuLength);
    });
  }
  function scoreMatches(total, options) {
    var offset = options.offset == null || options.offset === "" ? Infinity : Number(options.offset);
    return !positive(options.target, 0) ||
      total >= options.target && total <= options.target + offset;
  }
  function coolerFits(cooler, cpu, pcCase) {
    if (!cooler || !pcCase || cooler.sockets.indexOf(cpu.socket) < 0) return false;
    return cooler.water
      ? cooler.slots > 0 && cooler.slots <= (cooler.size === 120 ? pcCase.radiator120 : cooler.size === 140 ? pcCase.radiator140 : 0)
      : cooler.height > 0 && cooler.height <= pcCase.maxCoolerHeight;
  }
  function cpuThermals(cpu, gpu, count, cpuMhz, gpuMhz, memoryMhz, pcCase, cooler, constants) {
    var h = constants.heat;
    if (!h || !(cpu.dies > 0) || !(cpu.loadTemp > 0) || !(cpu.thermalLimit > 0)) return null;
    // Native heat model, with baseline silicon (1.0), thermal paste and no
    // assumed extra case fans. This is a conservative full-load estimate.
    var oc = f((cpuMhz - cpu.baseMhz) / (cpu.maxMhz + 1 - cpu.baseMhz));
    var heatFactor = cpuHeatFactor(cpu, cpuMhz, constants);
    var cooled = Math.max(f(f(cpu.idleTemp * h.idleFloor) / cpu.dies),
      f(f(f(cpu.loadTemp / cpu.dies) * heatFactor) * f(1 - cooler.cooling)));
    var gpuOc = gpuPowerFactor(gpu, gpuMhz, memoryMhz, constants);
    var gpuHeat = f(f(gpu.loadTemp + f(f(gpu.thermalLimit - gpu.loadTemp) * gpuOc)) / gpu.loadTemp);
    var caseHeat = f(f(h.psuLoad + f(cpu.loadTemp * heatFactor)) + f(f(gpu.watts * h.gpuWattToHeat) * f(count * gpuHeat)));
    var caseCooling = f(pcCase.cooling + (cooler.water ? cooler.caseCooling : 0));
    var caseTemp = f(h.room + f(f(caseHeat * h.caseHeat) * Math.max(0, f(1 - Math.abs(caseCooling)))));
    var feedback = f(Math.max(0, f(caseTemp - h.feedbackThreshold)) * h.feedbackFactor);
    var temp = f(cooled + feedback), gpuTemp = f(f(gpu.loadTemp * gpuHeat) + feedback);
    var voltage = cpuMhz <= cpu.baseMhz ? cpu.voltage :
      cpu.voltage + Math.ceil((f(cpu.voltage + f(f(cpu.maxVoltage - cpu.voltage) * oc)) - cpu.voltage) / 0.05 - 0.00001) * 0.05;
    return isFinite(temp) && isFinite(voltage) ? { temperature: temp, gpuTemperature: gpuTemp, voltage: Math.round(voltage * 100) / 100 } : null;
  }
  function coolingPlan(db, options, filters, cpu, gpu, count, cpuMhz, gpuMhz, pcCase, availableCoolers, candidate) {
    if (options.upgrade && !positive(options.targetCpuMhz, 0)) return { cooler: "", coolerPrice: 0, remainingReserve: options.reserve };
    var current = options.upgrade && options.system && db.coolers && db.coolers[options.system.cooler];
    var choices = availableCoolers;
    if (current) choices = [current].concat(availableCoolers);
    for (var i = 0; i < choices.length; i++) {
      var cooler = choices[i];
      if (!coolerFits(cooler, cpu, pcCase)) continue;
      var estimate = cpuThermals(cpu, gpu, count, cpuMhz, gpuMhz,
        options.upgrade && options.system.gpu === gpu.fullName ? options.system.gpuMemoryMhz : 0, pcCase, cooler, db.constants);
      if (!estimate || estimate.temperature > cpu.thermalLimit - 5 || estimate.voltage > cpu.maxVoltage) continue;
      if (gpu.gpuType === "Air" && estimate.gpuTemperature > gpu.thermalLimit - 3) continue;
      var keep = !!current && i === 0;
      var cost = keep ? 0 : cooler.price;
      var plan = { cooler: cooler.fullName, coolerPrice: cost, changedCooler: !keep, cpuTemp: Math.ceil(estimate.temperature * 10) / 10,
        cpuVoltage: estimate.voltage, remainingReserve: Math.max(0, options.reserve - cost) };
      if (candidate && !root.PCBSJobRules.valid(Object.assign({}, candidate, plan), options, db)) continue;
      return plan;
    }
    return null;
  }
  function finish(results, options) {
    results.sort(function (a, b) {
      return (
        a.price - b.price || a.score - b.score || a.cpu.localeCompare(b.cpu)
      );
    });
    var seen = Object.create(null);
    return results.filter(function (r) {
      var key = JSON.stringify([r.cpu, r.gpu, r.mobo]);
      if (seen[key]) return false;
      seen[key] = true; return true;
    }).slice(0, limit(options));
  }
  function maker(db, options, filters) {
    if (db.geometryReady === false)
      throw new Error("Preparing component compatibility. Please wait.");
    var coolers = sorted(db.coolers || {}, filters);
    // A brand sold only as CPU cooling must be selected here, rather than
    // retried for every RAM/case permutation after choosing a different cooler.
    var coolerBrands = (options.brands || []).filter(function (rule) {
      return (db.coolers && Object.values(db.coolers).some(function (p) { return p.manufacturer === rule.brand; })) &&
        ![db.cpus,db.gpus,db.mobos,db.pcCases,db.rams,db.psus].some(function (group) {
          return Object.values(group || {}).some(function (p) { return p.manufacturer === rule.brand; });
        });
    });
    coolers = coolers.filter(function (p) { return coolerBrands.every(function (r) { return p.manufacturer === r.brand; }); });
    var psus = sorted(db.psus || {}, filters);
    if (!db.psus || !Object.keys(db.psus).length) throw new Error("PSU data is unavailable. Reopen the helper.");
    if (!psus.length) return [];
    if (options.gpuType === "Water" || db.gpus[options.fixed.gpu] && (db.gpus[options.fixed.gpu].gpuType === "Water" || db.gpus[options.fixed.gpu].aio))
      throw new Error("Automatic builds currently use air-cooled GPUs. GPU radiators and custom loops must be planned separately.");
    if (options.targetCpuMhz && (!db.constants.heat || !db.coolers || !Object.keys(db.coolers).length))
      throw new Error("CPU cooling data is unavailable. Reopen the helper.");
    var cpus = sorted(db.cpus, filters).filter(function (p) {
      return (
        matches(p, options.fixed.cpu, options.cpuBrand, false) &&
        !!cpuClock(p, options.targetCpuMhz, db.constants) &&
        (!options.cpuOverclock || p.canOverclock === "Yes") &&
        (!options.socket ||
          options.socket === "Any" ||
          p.socket === options.socket)
      );
    });
    var gpus = sorted(db.gpus, filters).filter(function (p) {
      return (
        matches(p, options.fixed.gpu, options.gpuBrand, true) &&
        p.gpuType !== "Water" && !p.aio && !!gpuClock(p, options.targetGpuMhz, db.constants) &&
        (!options.gpuType ||
          options.gpuType === "Any" ||
          p.gpuType === options.gpuType)
      );
    });
    var boards = sorted(db.mobos, filters).filter(function (p) {
      return (
        (!options.fixed.mobo || p.fullName === options.fixed.mobo) &&
        (!options.cpuOverclock || p.canOverclock === "Yes")
      );
    });
    var cases = sorted(db.pcCases, filters).filter(function (p) {
      return !options.fixed.pcCase || p.fullName === options.fixed.pcCase;
    });
    var rams = sorted(db.rams, filters).filter(function (p) {
      return !options.fixed.ram || p.fullName === options.fixed.ram;
    });
    var counts =
        options.gpuCount === "Any" ? [1, 2] : [integer(options.gpuCount, 1)],
      results = [];
    // Enumerate supporting memory/boards once, then reuse them across CPU/GPU pairs.
    // RAM models with the same type, effective speed and manufacturer compete on price.
    var plans = Object.create(null);
    boards.forEach(function (board) {
      var cheapest = Object.create(null);
      rams.forEach(function (ram) {
        if (ram.ramType !== board.ramType) return;
        var speed = board.memorySpeeds
          .filter(function (mhz) {
            return mhz <= ram.speedMhz;
          })
          .pop();
        if (!speed) return;
        var key =
          speed +
          ":" +
          (options.brands && options.brands.length ? ram.manufacturer : "");
        if (!cheapest[key] || ram.price < cheapest[key].ram.price)
          cheapest[key] = { ram: ram, speed: speed };
      });
      var caseChoices = cases.filter(function (p) {
        return p.sizes.indexOf(board.size) >= 0;
      });
      if (!caseChoices.length) return;
      if (!plans[board.socket]) plans[board.socket] = [];
      Object.keys(cheapest).forEach(function (key) {
        for (var sticks = 1; sticks <= Math.min(board.ramSlots, 4); sticks++)
          plans[board.socket].push({
            board: board,
            ram: cheapest[key].ram,
            speed: cheapest[key].speed,
            sticks: sticks,
            cases: caseChoices,
          });
      });
    });
    var gpuScores = Object.create(null);
    gpus.forEach(function (gpu) {
      counts.forEach(function (count) {
        if (count === 1 || gpu.multiGpu)
          gpuScores[gpu.id + ":" + count] = gpuScore(
            gpu,
            count,
            gpuClock(gpu, options.targetGpuMhz, db.constants),
            0,
            db.constants,
          );
      });
    });
    var caseCache = Object.create(null), coolingCache = Object.create(null);
    cpus.forEach(function (cpu) {
      var cpuMhz = cpuClock(cpu, options.targetCpuMhz, db.constants);
      var cpuScores = Object.create(null);
      gpus.forEach(function (gpu) {
        counts.forEach(function (count) {
          if (count === 2 && !gpu.multiGpu) return;
          var basePrice = cpu.price + gpu.price * count;
          var gpuMhz = gpuClock(gpu, options.targetGpuMhz, db.constants);
          var demand = peakWatts(cpu, gpu, count, cpuMhz, gpuMhz, 0, db.constants);
          if (basePrice + options.reserve > options.budget) return;
          // Keep the cheapest complete supporting set for each CPU/GPU/RAM configuration.
          var supporting = Object.create(null);
          (plans[cpu.socket] || []).forEach(function (plan) {
            var board = plan.board,
              ram = plan.ram,
              speed = plan.speed,
              sticks = plan.sticks;
            if (cpuMhz > cpu.baseMhz && board.canOverclock !== "Yes") return;
            if (sticks > cpu.maxChannels) return;
            var supportPrice =
              (options.includeMobo ? board.price : 0) +
              (options.includeRam ? ram.price * sticks : 0);
            if (basePrice + supportPrice + options.reserve > options.budget)
              return;
            var cpuKey = speed + ":" + sticks;
            if (!cpuScores[cpuKey]) cpuScores[cpuKey] = cpuScore(cpu, speed, sticks, cpuMhz, db.constants);
            var total = combine(cpuScores[cpuKey], gpuScores[gpu.id + ":" + count]);
            if (!scoreMatches(total, options)) return;
            var caseKey = cpu.id + ":" + cpuMhz + ":" + gpu.id + ":" +
              board.id +
              ":" +
              gpu.length +
              ":" +
              gpu.slots +
              ":" +
              gpu.sli +
              ":" +
              count;
            if (!Object.prototype.hasOwnProperty.call(caseCache, caseKey))
              caseCache[caseKey] = plan.cases.map(function (p) {
                if (!compatible(cpu, gpu, board, p, count, 1, null)) return null;
                var psu = psuPlan(psus, p, demand);
                if (!psu) return null;
                var thermalKey = cpu.id + ":" + gpu.id + ":" + count + ":" + p.id;
                if (!Object.prototype.hasOwnProperty.call(coolingCache, thermalKey))
                  coolingCache[thermalKey] = coolingPlan(db, options, filters, cpu, gpu, count, cpuMhz, gpuMhz, p, coolers);
                var cooling = coolingCache[thermalKey];
                if (!cooling) return null;
                var cost = (options.includeCase ? p.price : 0) + cooling.coolerPrice + psu.price;
                return { part: p, cooling: cooling, psu: psu, cost: cost };
              }).filter(Boolean).sort(function (a,b) { return a.cost-b.cost; });
            var caseChoice = caseCache[caseKey].find(function (choice) {
              return basePrice + supportPrice + choice.cost + Math.max(0, options.reserve-choice.cooling.coolerPrice-choice.psu.price) <= options.budget;
            });
            if (!caseChoice) return;
            var casePart = caseChoice.part;
            var candidate = {
              cpu: cpu.fullName,
              gpu: gpu.fullName,
              gpuCount: count,
              ramChannel: sticks,
              ramSpeed: speed,
              ram: ram.fullName,
              mobo: board.fullName,
              pcCase: casePart.fullName,
              gpuType: gpu.gpuType,
              score: total,
              cpuMhz: cpuMhz,
              gpuMhz: gpuMhz,
              gpuVoltage: gpuVoltage(gpu, gpuMhz, 0, db.constants),
              psu: caseChoice.psu.fullName,
              psuPrice: caseChoice.psu.price,
              watts: demand,
              price:
                basePrice +
                (options.includeMobo ? board.price : 0) +
                (options.includeCase ? casePart.price : 0) +
                (options.includeRam ? ram.price * sticks : 0),
            };
            Object.assign(candidate, caseChoice.cooling);
            candidate.price += candidate.coolerPrice + candidate.psuPrice;
            candidate.remainingReserve = Math.max(0, options.reserve - candidate.coolerPrice - candidate.psuPrice);
            if (
              !root.PCBSJobRules.valid(candidate, options, db) &&
              options.brands &&
              options.brands.length
            ) {
              var otherCase = plan.cases.find(function (p) {
                if (!compatible(cpu, gpu, board, p, count, sticks, null))
                  return false;
                candidate.pcCase = p.fullName;
                candidate.price =
                  basePrice +
                  supportPrice +
                  (options.includeCase ? p.price : 0);
                var psu = psuPlan(psus, p, demand);
                if (!psu) return false;
                candidate.psu = psu.fullName; candidate.psuPrice = psu.price;
                var cooling = coolingPlan(db, options, filters, cpu, gpu, count, cpuMhz, candidate.gpuMhz, p, coolers, candidate);
                if (!cooling) return false;
                Object.assign(candidate, cooling); candidate.price += cooling.coolerPrice + psu.price;
                candidate.remainingReserve = Math.max(0, options.reserve - cooling.coolerPrice - psu.price);
                return (
                  candidate.price + candidate.remainingReserve <= options.budget &&
                  root.PCBSJobRules.valid(candidate, options, db)
                );
              });
              if (!otherCase) return;
            }
            if (
              candidate.price + candidate.remainingReserve > options.budget ||
              !root.PCBSJobRules.valid(candidate, options, db)
            )
              return;
            var key = board.fullName;
            if (!supporting[key] || supporting[key].price > candidate.price)
              supporting[key] = candidate;
          });
          Object.keys(supporting).forEach(function (key) {
            results.push(supporting[key]);
          });
          if (results.length > limit(options) * 4)
            results = finish(results, options);
        });
      });
    });
    return finish(results, options);
  }
  function upgrader(db, options, filters) {
    if (db.geometryReady === false)
      throw new Error("Preparing component compatibility. Please wait.");
    var coolers = sorted(db.coolers || {}, filters);
    if (options.targetCpuMhz && (!db.constants.heat || !db.coolers || !Object.keys(db.coolers).length))
      throw new Error("CPU cooling data is unavailable. Reopen the helper.");
    var system = options.system,
      oldCpu = db.cpus[system.cpu],
      oldGpu = db.gpus[system.gpu],
      oldBoard = db.mobos[system.mobo],
      pcCase = db.pcCases[system.pcCase];
    if (!oldCpu) throw new Error("CPU not found.");
    if (!oldGpu) throw new Error("GPU not found.");
    if (!oldBoard)
      throw new Error("Selected CPU and Motherboard are incompatible.");
    if (system.pcCase && !pcCase) throw new Error("Case not found.");
    if (!integer(system.ramSticks, 0))
      throw new Error("RAM Sticks is required.");
    var disk = system.m2 ? db.storages[system.m2] : null;
    if (system.m2 && !disk) throw new Error("M.2 not found.");
    var cpus = sorted(db.cpus, filters),
      gpus = sorted(db.gpus, filters),
      boards = sorted(db.mobos, filters),
      results = [];
    if (cpus.indexOf(oldCpu) < 0) cpus.push(oldCpu);
    if (gpus.indexOf(oldGpu) < 0) gpus.push(oldGpu);
    if (boards.indexOf(oldBoard) < 0) boards.push(oldBoard);
    cpus
      .filter(function (p) {
        return (
          matches(p, options.fixed.cpu, options.cpuBrand, false) &&
          (!options.targetCpuMhz || !!cpuClock(p, options.targetCpuMhz, db.constants)) &&
          (!options.cpuOverclock || p.canOverclock === "Yes")
        );
      })
      .forEach(function (cpu) {
        gpus
          .filter(function (p) {
            return (
              matches(p, options.fixed.gpu, options.gpuBrand, true) &&
              (p.gpuType !== "Water" && !p.aio || p === oldGpu) &&
              (!options.targetGpuMhz || !!gpuClock(p, options.targetGpuMhz, db.constants, p === oldGpu ? system.gpuMemoryMhz : 0)) &&
              (options.gpuType === "Any" ||
                !options.gpuType ||
                p.gpuType === options.gpuType)
            );
          })
          .forEach(function (gpu) {
            (options.gpuCount === "Any"
              ? [1, 2]
              : [integer(options.gpuCount, 1)]
            ).forEach(function (count) {
              if (count === 2 && !gpu.multiGpu) return;
              var addingSecond =
                gpu === oldGpu && Number(system.gpuCount) === 1 && count === 2;
              var gpuPrice =
                gpu === oldGpu && count === Number(system.gpuCount)
                  ? 0
                  : gpu.price * (addingSecond ? 1 : count);
              if (gpuPrice > 0 && !available(gpu, filters)) return;
              boards.forEach(function (board) {
                if (options.fixed.mobo && board.fullName !== options.fixed.mobo)
                  return;
                if (options.cpuOverclock && board.canOverclock !== "Yes")
                  return;
                var cpuMhz = options.targetCpuMhz
                  ? cpuClock(cpu, options.targetCpuMhz, db.constants)
                  : cpu === oldCpu ? positive(system.cpuMhz, cpu.baseMhz) : cpu.baseMhz;
                var gpuMhz = options.targetGpuMhz
                  ? gpuClock(gpu, options.targetGpuMhz, db.constants, gpu === oldGpu ? system.gpuMemoryMhz : 0)
                  : gpu === oldGpu ? positive(system.gpuMhz, gpu.baseMhz) : gpu.baseMhz;
                if (!cpuMhz || !gpuMhz || cpuMhz > cpu.baseMhz && board.canOverclock !== "Yes") return;
                if (
                  board.ramType !== oldBoard.ramType ||
                  !compatible(
                    cpu,
                    gpu,
                    board,
                    pcCase,
                    count,
                    Number(system.ramSticks),
                    disk,
                  )
                )
                  return;
                // A BIOS speed already imported from the job is kept when supported.
                var speed = board.memorySpeeds
                  .filter(function (mhz) {
                    return mhz <= Number(system.ramSpeed);
                  })
                  .pop();
                if (!speed) return;
                var total = combine(
                  cpuScore(
                    cpu,
                    speed,
                    Math.min(Number(system.ramSticks), cpu.maxChannels),
                    cpuMhz,
                    db.constants,
                  ),
                  gpuScore(
                    gpu,
                    count,
                    gpuMhz,
                    gpu === oldGpu ? system.gpuMemoryMhz : 0,
                    db.constants,
                  ),
                );
                var price =
                  (cpu === oldCpu ? 0 : cpu.price) +
                  gpuPrice +
                  (board === oldBoard ? 0 : board.price);
                if (
                  (!price && !options.targetCpuMhz && !options.targetGpuMhz) ||
                  !scoreMatches(total, options)
                )
                  return;
                var candidate = {
                  cpu: cpu.fullName,
                  gpu: gpu.fullName,
                  mobo: board.fullName,
                  pcCase: system.pcCase,
                  gpuCount: addingSecond ? "1 + 1" : count,
                  ramChannel: Math.min(
                    Number(system.ramSticks),
                    cpu.maxChannels,
                  ),
                  ramSpeed: speed,
                  gpuType: gpu.gpuType,
                  price: price,
                  score: total,
                  cpuMhz: cpuMhz,
                  gpuMhz: gpuMhz,
                  watts: peakWatts(cpu, gpu, count, cpuMhz, gpuMhz, gpu === oldGpu ? system.gpuMemoryMhz : 0, db.constants),
                  gpuVoltage: gpuVoltage(gpu, gpuMhz, gpu === oldGpu ? system.gpuMemoryMhz : 0, db.constants),
                  changedCpu: cpu !== oldCpu,
                  changedGpu: gpuPrice !== 0,
                  changedMobo: board !== oldBoard,
                };
                var cooling = coolingPlan(db, options, filters, cpu, gpu, count, cpuMhz, gpuMhz, pcCase, coolers, candidate);
                if (!cooling || price + cooling.coolerPrice + cooling.remainingReserve > options.budget) return;
                Object.assign(candidate, cooling);
                candidate.price += cooling.coolerPrice;
                if (root.PCBSJobRules.valid(candidate, options, db))
                  results.push(candidate);
              });
            });
          });
      });
    return finish(results, options);
  }
  function replacer(db, options, filters) {
    if (db.geometryReady === false)
      throw new Error("Preparing component compatibility. Please wait.");
    var sys = options.system,
      requests = options.requests;
    var oldCpu = db.cpus[sys.cpu],
      oldBoard = db.mobos[sys.mobo],
      oldGpu = db.gpus[sys.gpu],
      oldGpu2 = db.gpus[sys.gpu2],
      pcCase = db.pcCases[sys.pcCase];
    if (!oldCpu) throw new Error("CPU not found.");
    if (!oldBoard)
      throw new Error("Selected CPU and Motherboard are incompatible.");
    if (sys.pcCase && !pcCase) throw new Error("Case not found.");
    if (sys.gpu && !oldGpu) throw new Error("GPU 1 not found.");
    if (sys.gpu2 && !oldGpu2) throw new Error("GPU 2 not found.");
    if (
      !Object.keys(requests).some(function (k) {
        return requests[k] && requests[k] !== "Any";
      })
    )
      throw new Error("At least 1 part must have a request.");
    if (requests.gpu2 === "Add Dual-GPU" && oldGpu2)
      throw new Error(
        "GPU 2 must be blank to use the request to Add Dual-GPU.",
      );
    var disk = sys.m2 ? db.storages[sys.m2] : null;
    if (sys.m2 && !disk) throw new Error("M.2 not found.");
    function strength(p, kind) {
      if (kind === "cpu")
        return cpuScore(p, p.defaultRamMhz, p.maxChannels, 0, db.constants);
      if (kind === "gpu" || kind === "gpu2")
        return gpuScore(p, 1, 0, 0, db.constants);
      return p.price;
    }
    function choices(kind, group, original) {
      var request = requests[kind];
      if (!request || request === "Any") return [original || null];
      return sorted(db[group], filters).filter(function (p) {
        return (
          request !== "Upgrade" ||
          !original ||
          strength(p, kind) > strength(original, kind)
        );
      });
    }
    var cpus = choices("cpu", "cpus", oldCpu),
      gpus = choices("gpu", "gpus", oldGpu),
      secondGpus = choices("gpu2", "gpus", oldGpu2),
      boards = choices("mobo", "mobos", oldBoard),
      results = [];
    function cost(p, kind) {
      return requests[kind] && requests[kind] !== "Any" && p ? p.price : 0;
    }
    var sticks = integer(sys.ramSticks, 1);
    cpus.forEach(function (cpu) {
      boards.forEach(function (board) {
        if (
          board.ramType !== oldBoard.ramType ||
          board.socket !== cpu.socket ||
          sticks > board.ramSlots
        )
          return;
        var speed = board.memorySpeeds
          .filter(function (mhz) {
            return mhz <= Number(sys.ramSpeed);
          })
          .pop();
        if (!speed) return;
        var basePrice = cost(cpu, "cpu") + cost(board, "mobo");
        if (options.budget > 0 && basePrice > options.budget) return;
        var cpuResult = cpuScore(
          cpu,
          speed,
          Math.min(sticks, cpu.maxChannels),
          0,
          db.constants,
        );
        gpus.forEach(function (gpu) {
          if (!gpu) return;
          secondGpus.forEach(function (second) {
            if (
              second &&
              (!gpu.multiGpu ||
                !second.multiGpu ||
                gpu.chipset !== second.chipset ||
                gpu.vram !== second.vram)
            )
              return;
            var count = second ? 2 : 1;
            if (!compatible(cpu, gpu, board, pcCase, count, sticks, disk))
              return;
            if (
              second &&
              !compatible(cpu, second, board, pcCase, count, sticks, disk)
            )
              return;
            var price = basePrice + cost(gpu, "gpu") + cost(second, "gpu2");
            if (!price || (options.budget > 0 && price > options.budget))
              return;
            // PCBS uses the card with the lower stock core clock as the active GPU.
            var active = second && second.baseMhz < gpu.baseMhz ? second : gpu;
            var candidate = {
              cpu: cpu.fullName,
              gpu: gpu.fullName,
              gpu2: second ? second.fullName : "",
              mobo: board.fullName,
              price: price,
              score: combine(
                cpuResult,
                gpuScore(active, count, 0, 0, db.constants),
              ),
              changedCpu: !!requests.cpu && requests.cpu !== "Any",
              changedGpu: !!requests.gpu && requests.gpu !== "Any",
              changedGpu2: !!requests.gpu2 && requests.gpu2 !== "Any",
              changedMobo: !!requests.mobo && requests.mobo !== "Any",
            };
            results.push(candidate);
            if (results.length > limit(options) * 4)
              results = finish(results, options);
          });
        });
      });
    });
    return finish(results, options);
  }
  var api = {
    cpuScore: cpuScore,
    gpuScore: gpuScore,
    combine: combine,
    score: score,
    available: available,
    compatible: compatible,
    cpuClock: cpuClock,
    gpuClock: gpuClock,
    gpuPowerFactor: gpuPowerFactor,
    gpuVoltage: gpuVoltage,
    peakWatts: peakWatts,
    coolerFits: coolerFits,
    cpuThermals: cpuThermals,
    maker: maker,
    upgrader: upgrader,
    replacer: replacer,
  };
  if (typeof module !== "undefined" && module.exports) {
    root.PCBSJobRules = require("./calculator-job-rules.js");
    module.exports = api;
  } else root.PCBSHelperCalculator = api;
})(typeof window !== "undefined" ? window : globalThis);
