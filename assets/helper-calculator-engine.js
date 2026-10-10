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
  function finish(results, options) {
    results.sort(function (a, b) {
      return (
        a.price - b.price || a.score - b.score || a.cpu.localeCompare(b.cpu)
      );
    });
    return results.slice(0, limit(options));
  }
  function maker(db, options, filters) {
    if (db.geometryReady === false)
      throw new Error("Preparing component compatibility. Please wait.");
    var cpus = sorted(db.cpus, filters).filter(function (p) {
      return (
        matches(p, options.fixed.cpu, options.cpuBrand, false) &&
        (!options.cpuOverclock || p.canOverclock === "Yes") &&
        (!options.socket ||
          options.socket === "Any" ||
          p.socket === options.socket)
      );
    });
    var gpus = sorted(db.gpus, filters).filter(function (p) {
      return (
        matches(p, options.fixed.gpu, options.gpuBrand, true) &&
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
      results = [],
      upper = options.target + options.offset;
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
            0,
            0,
            db.constants,
          );
      });
    });
    var caseCache = Object.create(null);
    cpus.forEach(function (cpu) {
      var cpuScores = Object.create(null);
      gpus.forEach(function (gpu) {
        counts.forEach(function (count) {
          if (count === 2 && !gpu.multiGpu) return;
          var basePrice = cpu.price + gpu.price * count;
          if (basePrice + options.reserve > options.budget) return;
          // Keep the cheapest complete supporting set for each CPU/GPU/RAM configuration.
          var supporting = Object.create(null);
          (plans[cpu.socket] || []).forEach(function (plan) {
            var board = plan.board,
              ram = plan.ram,
              speed = plan.speed,
              sticks = plan.sticks;
            if (sticks > cpu.maxChannels) return;
            var supportPrice =
              (options.includeMobo ? board.price : 0) +
              (options.includeRam ? ram.price * sticks : 0);
            if (basePrice + supportPrice + options.reserve > options.budget)
              return;
            var caseKey =
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
              caseCache[caseKey] =
                plan.cases.find(function (p) {
                  return compatible(cpu, gpu, board, p, count, 1, null);
                }) || null;
            var casePart = caseCache[caseKey];
            if (!casePart) return;
            var cpuKey = speed + ":" + sticks;
            if (!cpuScores[cpuKey])
              cpuScores[cpuKey] = cpuScore(cpu, speed, sticks, 0, db.constants);
            var scoreCpu = cpuScores[cpuKey],
              scoreGpu = gpuScores[gpu.id + ":" + count];
            var total = combine(scoreCpu, scoreGpu);
            if (total < options.target || total > upper) return;
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
              watts: cpu.watts + gpu.watts * count + 30,
              price:
                basePrice +
                (options.includeMobo ? board.price : 0) +
                (options.includeCase ? casePart.price : 0) +
                (options.includeRam ? ram.price * sticks : 0),
            };
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
                return (
                  candidate.price + options.reserve <= options.budget &&
                  root.PCBSJobRules.valid(candidate, options, db)
                );
              });
              if (!otherCase) return;
            }
            if (
              candidate.price + options.reserve > options.budget ||
              !root.PCBSJobRules.valid(candidate, options, db)
            )
              return;
            var key = ram.fullName + ":" + sticks + ":" + speed;
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
          (!options.cpuOverclock || p.canOverclock === "Yes")
        );
      })
      .forEach(function (cpu) {
        gpus
          .filter(function (p) {
            return (
              matches(p, options.fixed.gpu, options.gpuBrand, true) &&
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
                    cpu === oldCpu ? system.cpuMhz : 0,
                    db.constants,
                  ),
                  gpuScore(
                    gpu,
                    count,
                    gpu === oldGpu ? system.gpuMhz : 0,
                    gpu === oldGpu ? system.gpuMemoryMhz : 0,
                    db.constants,
                  ),
                );
                var price =
                  (cpu === oldCpu ? 0 : cpu.price) +
                  gpuPrice +
                  (board === oldBoard ? 0 : board.price);
                if (
                  !price ||
                  price + options.reserve > options.budget ||
                  total < options.target ||
                  total > options.target + options.offset
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
                  watts: cpu.watts + gpu.watts * count + 30,
                  changedCpu: cpu !== oldCpu,
                  changedGpu: gpuPrice !== 0,
                  changedMobo: board !== oldBoard,
                };
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
    maker: maker,
    upgrader: upgrader,
    replacer: replacer,
  };
  if (typeof module !== "undefined" && module.exports) {
    root.PCBSJobRules = require("./calculator-job-rules.js");
    module.exports = api;
  } else root.PCBSHelperCalculator = api;
})(typeof window !== "undefined" ? window : globalThis);
