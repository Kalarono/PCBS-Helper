/* Independent UI with the existing helper's bridge and version-1 session field names. */
var sourceData = window.pcbsGameCatalog,
  data3DMark = sourceData,
  data = sourceData;
var includeHemParts = false,
  includePartsNotInShop = false,
  allPartsUnlocked = false,
  level = 0,
  levelPercentThrough = 0,
  levelPercentThroughSet = false,
  partReplacerResults = [];
(function (root) {
  "use strict";
  var engine = root.PCBSHelperCalculator,
    pages = [
      "div3DMarkScoreCalculator",
      "divBuildMaker",
      "divBuildUpgrader",
      "divPartReplacer",
      "divHistoryAndSaves",
    ];
  var forms = [
    "form3DMarkScoreCalculator",
    "formBuildMaker",
    "formBuildUpgrader",
    "formPartReplacer",
  ];
  var titles = [
    "3DMark Score",
    "Build Maker",
    "Build Upgrader",
    "Part Replacer",
    "History & Saves",
  ];
  var resultClasses = [
    "tdCase",
    "tdCpu",
    "tdramChannel",
    "tdRamSpeed",
    "tdRamPart",
    "tdGpuCount",
    "tdGpuType",
    "tdGpuPart",
    "tdMobo",
    "tdPrice",
    "tdRemaining",
    "tdScore",
    "tdWatts",
    "tdAction",
  ];
  var resultHeaders = [
    "Case",
    "CPU",
    "RAM Channels",
    "RAM Speed",
    "RAM",
    "# GPUs",
    "Type",
    "GPU",
    "Motherboard",
    "Price",
    "Remaining",
    "Score",
    "Watts",
    "Order",
  ];
  var currentSaveRow = null,
    loadingHistory = false;
  var calculationStatus = el("p", document.getElementById("divPages"));
  calculationStatus.className = "hint";
  calculationStatus.setAttribute("role", "status");
  function el(tag, parent, text) {
    var n = document.createElement(tag);
    if (text != null) n.textContent = text;
    if (parent) parent.appendChild(n);
    return n;
  }
  function control(parent, name, type, values, value) {
    var n = el(type === "select" ? "select" : "input", parent);
    n.name = name;
    if (type === "select")
      (values || []).forEach(function (v) {
        n.add(new Option(String(v), String(v)));
      });
    else {
      n.type = type || "text";
      if (n.type === "number") {
        n.min = "0";
        n.step = "1";
      }
    }
    if (type === "checkbox") {
      n.checked = !!value;
      n.defaultChecked = !!value;
    } else if (value != null) {
      n.value = value;
      n.defaultValue = value;
    }
    return n;
  }
  function row(form, name, label, type, values, value, list) {
    var body = form.querySelector("tbody"),
      tr = el("tr", body),
      title = el("td", tr, label),
      cell = el("td", tr);
    var n = control(cell, name, type, values, value);
    n.id = name;
    var id = name + "-label";
    title.id = id;
    n.setAttribute("aria-labelledby", id);
    if (list) n.setAttribute("list", list);
    return n;
  }
  function table(parent, id, headers) {
    var t = el("table", parent);
    t.id = id;
    var tr = el("tr", el("tbody", t));
    headers.forEach(function (s) {
      el("th", tr, s);
    });
    return t;
  }
  function form(page, id) {
    var p = el("div", page);
    p.className = "panel";
    p.id = page.id + "Form";
    var f = el("form", p);
    f.id = id;
    el("tbody", el("table", f));
    f.onsubmit = function (e) {
      e.preventDefault();
    };
    return f;
  }
  function clickButton(form, label, fn) {
    var b = el("button", form, label);
    b.type = "button";
    b.onclick = function () {
      root[fn]();
    };
    return b;
  }
  function value(form, name) {
    var c = form.elements.namedItem(name);
    return c ? (c.type === "checkbox" ? c.checked : c.value) : "";
  }
  function number(form, name, fallback) {
    var v = value(form, name);
    return v !== "" && isFinite(Number(v)) ? Number(v) : fallback;
  }
  function getForm(page) {
    return document.getElementById(forms[pages.indexOf(page)]);
  }
  function fill(form, name, v) {
    var c = form.elements.namedItem(name);
    if (!c) return;
    if (c.type === "checkbox") c.checked = !!v;
    else {
      if (
        c.tagName === "SELECT" &&
        !Array.from(c.options).some(function (o) {
          return o.value === String(v);
        })
      )
        c.add(new Option(String(v), String(v)));
      c.value = v == null ? "" : v;
    }
  }
  function filters() {
    return { notInShop: includePartsNotInShop, allUnlocked: allPartsUnlocked };
  }
  function safe(fn) {
    try {
      calculationStatus.textContent = "";
      fn();
    } catch (error) {
      calculationStatus.textContent = root.pcbsTranslate
        ? root.pcbsTranslate(error.message)
        : error.message;
      root.alert(error.message);
    }
  }
  function clearTable(t) {
    while (t.rows.length > 1) t.deleteRow(1);
  }
  function options(form, mode) {
    var prefix = mode === "maker" ? "BuildMaker" : "BuildUpgrader",
      job;
    try {
      job = JSON.parse(value(form, "pcbsJobMetadata") || "null");
    } catch (_) {
      job = null;
    }
    var target = number(form, "input" + prefix + "Target3DMarkScore", 0),
      budget = number(form, "input" + prefix + "Budget", 0);
    if (!budget) throw new Error("Budget is required.");
    if (!target) throw new Error("Score is required.");
    var o = {
      target: target,
      budget: budget,
      reserve: number(form, "input" + prefix + "ReservedBudget", 0),
      offset: number(form, "input" + prefix + "Target3DMarkScoreOffset", 500),
      results: number(form, "input" + prefix + "ResultsRequested", 10),
      fixed: {},
      brands: job ? job.brands || [] : [],
      cpuBrand: value(form, "pcbsCpuBrand"),
      gpuBrand: value(form, "pcbsGpuBrand"),
      gpuCount: value(
        form,
        mode === "maker"
          ? "selectBuildMakerGpuCount"
          : "selectBuildUpgraderOptionsGpuCount",
      ),
      gpuType: value(
        form,
        mode === "maker"
          ? "selectBuildMakerGpuType"
          : "selectBuildUpgraderOptionsGpuType",
      ),
      cpuOverclock:
        mode === "maker"
          ? value(form, "inputBuildMakerNeedCpuOverclock")
          : !!(job && job.fields && job.fields.cpuOverclock),
      upgrade: mode !== "maker",
    };
    [
      ["Cpu", "cpu"],
      ["Gpu", "gpu"],
      ["Case", "pcCase"],
      ["Mobo", "mobo"],
      ["Ram", "ram"],
    ].forEach(function (s) {
      o.fixed[s[1]] = value(form, "pcbsFixed" + s[0]);
    });
    if (mode === "maker") {
      o.includeCase = value(form, "inputBuildMakerIncludeCase");
      o.includeMobo = value(form, "inputBuildMakerIncludeMotherboard");
      o.includeRam = value(form, "inputBuildMakerIncludeRamPart");
      o.socket = value(form, "selectBuildMakerCpuSocket");
    } else {
      o.system = system(form, "BuildUpgrader");
      o.system.otherParts =
        job && job.system ? job.system.otherParts || [] : [];
      o.brands = root.PCBSJobRules.upgradeBrandRules(
        o.brands,
        sourceData,
      ).applied;
    }
    return o;
  }
  function system(form, prefix) {
    var p = prefix + "OriginalSystem";
    var job;
    try {
      job = JSON.parse(value(form, "pcbsJobMetadata") || "null");
    } catch (_) {
      job = null;
    }
    return {
      cpu: value(form, "input" + p + "Cpu"),
      gpu:
        value(form, "input" + p + "Gpu") || value(form, "input" + p + "Gpu1"),
      gpu2: value(form, "input" + p + "Gpu2"),
      mobo: value(form, "select" + p + "Mobo"),
      pcCase: value(form, "input" + p + "Case"),
      m2: value(form, "input" + p + "M2"),
      gpuCount: number(form, "select" + p + "GpuCount", 1),
      ramSpeed: number(form, "select" + p + "RamSpeed", 2133),
      ramSticks: number(form, "input" + prefix + "RamSticks", 1),
      cpuMhz: job && job.system ? job.system.cpuMhz : 0,
      gpuMhz: job && job.system ? job.system.gpuMhz : 0,
      gpuMemoryMhz: job && job.system ? job.system.gpuMemoryMhz : 0,
    };
  }
  function canonical(name, group) {
    if (!name || sourceData[group][name]) return name || "";
    var norm = CalculatorName;
    return (
      Object.keys(sourceData[group]).find(function (s) {
        return (
          norm(s) === norm(name) ||
          norm(sourceData[group][s].displayName) === norm(name)
        );
      }) || name
    );
  }
  function CalculatorName(s) {
    return String(s || "")
      .toLowerCase()
      .replace(/[®™]/g, "")
      .replace(/\s+/g, " ")
      .trim();
  }
  root.showPage = function (page) {
    pages.forEach(function (id) {
      document.getElementById(id).style.display =
        id === page ? "block" : "none";
    });
  };
  root.numberOrDefault = function (v, fallback) {
    return v !== "" && isFinite(Number(v)) ? Number(v) : fallback;
  };
  root.setData = function () {
    data = Object.assign({}, sourceData);
    ["cpus", "gpus", "mobos", "pcCases", "rams", "storages"].forEach(
      function (group) {
        data[group] = {};
        Object.keys(sourceData[group]).forEach(function (name) {
          if (engine.available(sourceData[group][name], filters()))
            data[group][name] = sourceData[group][name];
        });
      },
    );
  };
  root.isLevelSet = function () {
    return allPartsUnlocked || level > 0;
  };
  root.pcbsSetCatalogAvailability = function (unlocked) {
    ["cpus", "gpus", "mobos", "pcCases", "rams", "storages"].forEach(
      function (group) {
        Object.keys(sourceData[group]).forEach(function (name) {
          var part = sourceData[group][name];
          if (typeof unlocked[part.id] === "boolean")
            part.unlocked = unlocked[part.id];
        });
      },
    );
    root.setData();
  };
  root.pcbsSetCatalogGeometry = function (catalog) {
    sourceData = catalog;
    data3DMark = catalog;
    root.setData();
    document.querySelectorAll("[data-pcbs-search]").forEach(function (button) {
      button.disabled = false;
    });
    var hint = document.getElementById("pcbsGeometryStatus");
    if (hint) hint.hidden = true;
  };
  root.resetResultsTable = function (t) {
    clearTable(t);
  };
  root.hideSaver = function (n) {
    n.style.display = "none";
    n.hidden = true;
    currentSaveRow = null;
  };
  root.unblurBackground = function () {};
  root.setListOptions = function () {
    [
      ["listCpus", "cpus"],
      ["listGpus", "gpus"],
      ["listCases", "pcCases"],
      ["listMobos", "mobos"],
      ["listRams", "rams"],
      ["listStorage", "storages"],
    ].forEach(function (s) {
      var list = document.getElementById(s[0]);
      list.textContent = "";
      Object.keys(sourceData[s[1]]).forEach(function (name) {
        var option = new Option(name, name);
        list.appendChild(option);
      });
    });
  };
  root.setSelectOptions = function () {
    root.updateRamSpeedOptions3DMark();
    root.updateRamChannelOptions3DMark();
    root.updateMoboOptionsBuildUpgrader();
    root.updateMoboOptionsPartReplacer();
  };
  function selectOptions(c, values) {
    var old = c.value;
    c.textContent = "";
    values.forEach(function (v) {
      c.add(new Option(String(v), String(v)));
    });
    if (values.indexOf(Number(old)) >= 0 || values.indexOf(old) >= 0)
      c.value = old;
  }
  root.updateRamSpeedOptions3DMark = function () {
    var f = document.getElementById(forms[0]),
      cpu = sourceData.cpus[f.input3DMarkCpu.value];
    var speeds = [];
    Object.keys(sourceData.mobos).forEach(function (n) {
      var b = sourceData.mobos[n];
      if (!cpu || b.socket === cpu.socket)
        speeds = speeds.concat(b.memorySpeeds);
    });
    selectOptions(
      f.select3DMarkRamSpeed,
      Array.from(new Set(speeds)).sort(function (a, b) {
        return a - b;
      }),
    );
  };
  root.updateRamChannelOptions3DMark = function () {
    var f = document.getElementById(forms[0]),
      cpu = sourceData.cpus[f.input3DMarkCpu.value];
    selectOptions(
      f.select3DMarkRamChannel,
      Array.from(
        { length: Math.min(4, cpu ? cpu.maxChannels : 4) },
        function (_, i) {
          return i + 1;
        },
      ),
    );
  };
  function boardOptions(prefix, index) {
    var f = document.getElementById(forms[index]),
      cpu = sourceData.cpus[value(f, "input" + prefix + "OriginalSystemCpu")];
    selectOptions(
      f.elements.namedItem("select" + prefix + "OriginalSystemMobo"),
      Object.keys(sourceData.mobos).filter(function (n) {
        return !cpu || sourceData.mobos[n].socket === cpu.socket;
      }),
    );
  }
  root.updateMoboOptionsBuildUpgrader = function () {
    boardOptions("BuildUpgrader", 2);
  };
  root.updateMoboOptionsPartReplacer = function () {
    boardOptions("PartReplacer", 3);
  };
  root.addSaveButton = function (cell, id) {
    var b = el("button", cell, "Order");
    b.type = "button";
    b.className = "buttonSave";
    b.id = id;
    root.pcbsAddBuildSaveButton(cell);
  };
  root.pcbsAddBuildSaveButton = function (cell) {
    var save = el("button", cell, "Save");
    save.type = "button";
    save.onclick = function () {
      currentSaveRow = cell.parentNode;
      var saver = document.getElementById("divBuildMakerSaveForm");
      saver.hidden = false;
      saver.style.display = "block";
      saver.scrollIntoView({ block: "nearest" });
      document.getElementById("inputBuildMakerSaveComment").focus();
    };
  };
  root.CalculatorCalculate = function () {
    safe(function () {
      var f = document.getElementById(forms[0]);
      var input = {
        cpu: f.input3DMarkCpu.value,
        gpu: f.input3DMarkGpu.value,
        ramSpeed: f.select3DMarkRamSpeed.value,
        ramChannel: f.select3DMarkRamChannel.value,
        gpuCount: f.select3DMarkGpuCount.value,
        cpuMhz: f.pcbsCpuMhz.value,
        gpuMhz: f.pcbsGpuMhz.value,
        gpuMemoryMhz: f.pcbsGpuMemoryMhz.value,
      };
      var result = engine.score(sourceData, input);
      [
        "CpuScore",
        "GpuScore",
        "SystemScore",
        "GpuType",
        "SystemWatts",
        "CpuPriceNew",
        "CpuPriceUsed",
        "GpusPriceNew",
        "GpusPriceUsed",
      ].forEach(function (s, i) {
        document.getElementById("td3DMark" + s).textContent = [
          result.cpu,
          result.gpu,
          result.total,
          result.gpuType,
          result.watts,
          result.cpuPrice,
          result.cpuUsed,
          result.gpuPrice,
          result.gpuUsed,
        ][i];
      });
      var record = {
        type: "div3DMarkScoreCalculatorForm",
        cpu: input.cpu,
        gpu: input.gpu,
        score: result.total,
      };
      Array.from(f.elements).forEach(function (c) {
        if (c.name) record[c.name] = c.value;
      });
      if (!loadingHistory) saveHistory(record);
    });
  };
  function saveHistory(record) {
    var key = String(Date.now());
    while (sessionStorage.getItem(key)) key = String(Number(key) + 1);
    sessionStorage.setItem(key, JSON.stringify(record));
    root.updateHistory();
  }
  root.BuildMakerGetBuilds = function () {
    safe(function () {
      var f = document.getElementById(forms[1]),
        o = options(f, "maker"),
        rows = engine.maker(sourceData, o, filters()),
        t = document.getElementById("tableBuildMakerResults");
      clearTable(t);
      rows.forEach(function (r, i) {
        var tr = t.insertRow(-1);
        tr.pcbsBuild = r;
        var vals = [
          o.includeCase ? r.pcCase : "",
          r.cpu,
          r.ramChannel,
          r.ramSpeed,
          o.includeRam ? r.ram : "",
          r.gpuCount,
          r.gpuType,
          r.gpu,
          o.includeMobo ? r.mobo : "",
          r.price,
          o.budget - r.price,
          r.score,
          r.watts,
          "",
        ];
        vals.forEach(function (v, j) {
          var c = tr.insertCell(-1);
          c.className = resultClasses[j];
          c.textContent = v;
          if (j === 13) root.addSaveButton(c, i + 1);
        });
      });
      if (!rows.length) root.alert("No builds found.");
      root.pcbsFlush();
    });
  };
  root.BuildUpgraderGetUpgrades = function () {
    safe(function () {
      var f = document.getElementById(forms[2]),
        o = options(f, "upgrade"),
        rows = engine.upgrader(sourceData, o, filters()),
        t = document.getElementById("tableBuildUpgraderResults");
      clearTable(t);
      rows.forEach(function (r) {
        var tr = t.insertRow(-1);
        [
          r.changedCpu ? r.cpu : "",
          r.gpuCount,
          r.gpuType,
          r.changedGpu ? r.gpu : "",
          r.changedMobo ? r.mobo : "",
          r.price,
          o.budget - r.price,
          r.score,
          r.watts,
        ].forEach(function (v, i) {
          var cell=tr.insertCell(-1);cell.textContent=v;
          cell.className=['tdCpu','tdGpuCount','tdGpuType','tdGpuPart','tdMobo','tdPrice','tdRemaining','tdScore','tdWatts'][i];
        });
      });
      if (!rows.length) root.alert("No upgrades found.");
      root.pcbsFlush();
    });
  };
  root.PartReplacerGetParts = function () {
    safe(function () {
      var f = document.getElementById(forms[3]),
        o = {
          system: system(f, "PartReplacer"),
          budget: number(f, "inputPartReplacerBudget", 0),
          results: number(f, "inputPartReplacerResultsRequested", 10),
          requests: {
            cpu: value(f, "pcbsReplaceCpu"),
            gpu: value(f, "pcbsReplaceGpu"),
            gpu2: value(f, "pcbsReplaceGpu2"),
            mobo: value(f, "pcbsReplaceMobo"),
          },
        };
      partReplacerResults = engine.replacer(sourceData, o, filters());
      var t = document.getElementById("tablePartReplacerResults");
      clearTable(t);
      partReplacerResults.forEach(function (r) {
        var tr = t.insertRow(-1);
        [
          r.changedCpu ? r.cpu : "",
          r.changedGpu ? r.gpu : "",
          r.changedGpu2 ? r.gpu2 : "",
          r.changedMobo ? r.mobo : "",
          r.price,
          o.budget - r.price,
          r.score,
        ].forEach(function (v) {
          tr.insertCell(-1).textContent = v;
        });
      });
      if (!partReplacerResults.length) root.alert("No replacements found.");
      root.pcbsFlush();
    });
  };
  root.updateHistory = function () {
    ["table3DMarkScoreCalculatorHistory", "tableBuildMakerSaves"].forEach(
      function (id) {
        clearTable(document.getElementById(id));
      },
    );
    Object.keys(sessionStorage)
      .sort()
      .reverse()
      .forEach(function (key) {
        var record;
        try {
          record = JSON.parse(sessionStorage.getItem(key));
        } catch (_) {
          return;
        }
        if (
          !record ||
          !["build", "div3DMarkScoreCalculatorForm"].includes(record.type)
        )
          return;
        var isBuild = record.type === "build",
          t = document.getElementById(
            isBuild
              ? "tableBuildMakerSaves"
              : "table3DMarkScoreCalculatorHistory",
          ),
          tr = t.insertRow(-1);
        [
          record.cpu || record.input3DMarkCpu || record.input3DMarkCPU || "",
          record.gpu || record.input3DMarkGpu || "",
          record.score || record.systemScore || "",
          record.comment || "",
        ].forEach(function (v) {
          tr.insertCell(-1).textContent = v;
        });
        var actions = tr.insertCell(-1),
          load = el("button", actions, "Load"),
          remove = el("button", actions, "Delete");
        load.type = remove.type = "button";
        load.onclick = function () {
          root.showPage(pages[0]);
          var f = document.getElementById(forms[0]);
          if (record.cpu && record.gpu) {
            fill(f, "input3DMarkCpu", canonical(record.cpu, "cpus"));
            fill(f, "input3DMarkGpu", canonical(record.gpu, "gpus"));
            root.updateRamSpeedOptions3DMark();
            root.updateRamChannelOptions3DMark();
            fill(f, "select3DMarkRamChannel", record.ramChannel);
            fill(f, "select3DMarkRamSpeed", record.ramSpeed);
            fill(f, "select3DMarkGpuCount", record.gpuCount);
          }
          Object.keys(record).forEach(function (name) {
            fill(f, name, record[name]);
          });
          loadingHistory = true;
          try {
            root.CalculatorCalculate();
          } finally {
            loadingHistory = false;
          }
        };
        remove.onclick = function () {
          sessionStorage.removeItem(key);
          root.updateHistory();
          root.pcbsFlush();
        };
      });
  };
  root.pcbsRestoreJobReview = function () {
    [1, 2].forEach(function (index) {
      var f = document.getElementById(forms[index]),
        review = document.getElementById(pages[index] + "JobReview"),
        job;
      try {
        job = JSON.parse(value(f, "pcbsJobMetadata") || "null");
      } catch (_) {
        job = null;
      }
      review.textContent = "";
      review.hidden = !job;
      if (!job) return;
      el(
        "strong",
        review,
        (root.pcbsLanguage === "ru" ? "Задание №" : "Job #") +
          job.jobId +
          ": " +
          job.title,
      );
      var details = el("details", review);
      el(
        "summary",
        details,
        root.pcbsTranslate
          ? root.pcbsTranslate("Job objectives and calculation limits")
          : "Job objectives and calculation limits",
      );
      (job.notes || []).concat(job.requirements || []).forEach(function (s) {
        el("p", details, (job.notes||[]).indexOf(s)>=0 && root.pcbsTranslate ? root.pcbsTranslate(s) : s);
      });
      if (index === 2)
        root.PCBSJobRules.upgradeBrandRules(
          job.brands,
          sourceData,
        ).deferred.forEach(function (rule) {
          el(
            "p",
            details,
            root.pcbsLanguage === "ru"
              ? "Условие по марке " +
                  rule.brand +
                  " (" +
                  rule.count +
                  "): проверьте отдельно в игре. Апгрейд заменяет только CPU, GPU и плату."
              : "Brand objective " +
                  rule.brand +
                  " (" +
                  rule.count +
                  "): check separately in the game. Upgrades replace CPU, GPU and motherboard only.",
          );
        });
      var hardware = el("details", review);
      el(
        "summary",
        hardware,
        root.pcbsTranslate
          ? root.pcbsTranslate("All original hardware")
          : "All original hardware",
      );
      var system = job.system || {},
        parts = [];
      ["pcCase", "cpu", "mobo", "cooler", "psu"].forEach(function (key) {
        if (system[key]) parts.push(system[key]);
      });
      ["gpus", "ram", "m2s", "disks"].forEach(function (key) {
        parts = parts.concat(system[key] || []);
      });
      if (system.cpuMhz) parts.push("CPU: " + system.cpuMhz + " MHz");
      if (system.gpuMhz) parts.push("GPU: " + system.gpuMhz + " MHz");
      el("p", hardware, parts.join("\n")).style.whiteSpace = "pre-wrap";
    });
  };
  root.pcbsResetJobReview = root.pcbsRestoreJobReview;
  root.pcbsImportJob = function (job) {
    if (!job || ![pages[1], pages[2]].includes(job.tab)) return;
    root.showPage(job.tab);
    root.pcbsClearCurrentTab();
    var f = getForm(job.tab),
      maker = job.tab === pages[1],
      prefix = maker ? "BuildMaker" : "BuildUpgrader",
      fields = job.fields || {},
      sys = job.system || {};
    fill(f, "input" + prefix + "Budget", job.budget);
    fill(f, "input" + prefix + "Target3DMarkScore", job.target);
    fill(f, "input" + prefix + "ReservedBudget", maker ? 400 : 0);
    fill(f, "pcbsCpuBrand", fields.cpuBrand || "Any");
    fill(f, "pcbsGpuBrand", fields.gpuBrand || "Any");
    [
      ["Cpu", "cpus"],
      ["Gpu", "gpus"],
      ["Case", "pcCases"],
      ["Mobo", "mobos"],
      ["Ram", "rams"],
    ].forEach(function (s) {
      fill(f, "pcbsFixed" + s[0], canonical(fields["fixed" + s[0]], s[1]));
    });
    fill(
      f,
      maker ? "selectBuildMakerGpuCount" : "selectBuildUpgraderOptionsGpuCount",
      fields.gpuCount || "Any",
    );
    fill(
      f,
      maker ? "selectBuildMakerGpuType" : "selectBuildUpgraderOptionsGpuType",
      fields.gpuType || "Any",
    );
    fill(f, "pcbsJobMetadata", JSON.stringify(job));
    if (maker) {
      ["Case", "Motherboard", "RamPart"].forEach(function (s) {
        fill(f, "inputBuildMakerInclude" + s, true);
      });
      fill(f, "inputBuildMakerNeedCpuOverclock", fields.cpuOverclock);
    } else {
      fill(
        f,
        "inputBuildUpgraderOriginalSystemCpu",
        canonical(sys.cpu, "cpus"),
      );
      root.updateMoboOptionsBuildUpgrader();
      fill(
        f,
        "selectBuildUpgraderOriginalSystemMobo",
        canonical(sys.mobo, "mobos"),
      );
      fill(
        f,
        "inputBuildUpgraderOriginalSystemCase",
        canonical(sys.pcCase, "pcCases"),
      );
      fill(
        f,
        "inputBuildUpgraderOriginalSystemM2",
        canonical((sys.m2s || [])[0], "storages"),
      );
      fill(
        f,
        "inputBuildUpgraderOriginalSystemGpu",
        canonical((sys.gpus || [])[0], "gpus"),
      );
      fill(
        f,
        "selectBuildUpgraderOriginalSystemGpuCount",
        (sys.gpus || []).length || 1,
      );
      fill(
        f,
        "selectBuildUpgraderOriginalSystemRamSpeed",
        sys.ramSpeed || 2133,
      );
      fill(f, "inputBuildUpgraderRamSticks", sys.ramSticks || "");
    }
    root.pcbsRestoreJobReview();
    root.pcbsFlush();
  };
  // Build an original compact interface. Names below preserve the helper's own saved-state contract.
  pages.forEach(function (id, i) {
    var b = el(
      "button",
      document.getElementById("divNavigationBar"),
      titles[i],
    );
    b.type = "button";
    b.className = "tdNavigationBar";
    b.onclick = function () {
      root.showPage(id);
    };
    var page = el("section", document.getElementById("divPages"));
    page.id = id;
    el("h1", page, titles[i]);
  });
  var opt = document.getElementById("formOptions");
  var geometryHint = el(
    "p",
    document.getElementById("divOptions"),
    "Preparing component compatibility. Please wait.",
  );
  geometryHint.id = "pcbsGeometryStatus";
  geometryHint.className = "hint";
  geometryHint.hidden = sourceData.geometryReady !== false;
  [
    ["inputOptionPartsNotInShop", "Include parts not in the Shop?"],
    ["inputOptionAllPartsUnlocked", "All Parts Unlocked?"],
  ].forEach(function (s) {
    var label = el("label", opt);
    control(label, s[0], "checkbox");
    label.appendChild(document.createTextNode(s[1]));
  });
  var hem = control(opt, "inputOptionHem", "checkbox");
  hem.hidden = true;
  var lev = document.getElementById("formLevel");
  [
    ["inputLevel", "Career Level:"],
    ["inputLevelPercentThrough", "% Through:"],
  ].forEach(function (s) {
    var label = el("label", lev, s[1]);
    control(label, s[0], "number");
  });
  [
    ["listCpus", "cpus"],
    ["listGpus", "gpus"],
    ["listCases", "pcCases"],
    ["listMobos", "mobos"],
    ["listRams", "rams"],
    ["listStorage", "storages"],
  ].forEach(function (s) {
    var list = el("datalist", document.body);
    list.id = s[0];
  });
  var score = form(document.getElementById(pages[0]), forms[0]);
  row(
    score,
    "input3DMarkCpu",
    "CPU (Processor)",
    "text",
    null,
    null,
    "listCpus",
  );
  row(score, "select3DMarkRamSpeed", "RAM Speed", "select", []);
  row(score, "select3DMarkRamChannel", "RAM Channels", "select", [1, 2, 3, 4]);
  row(
    score,
    "input3DMarkGpu",
    "GPU (Graphics Card)",
    "text",
    null,
    null,
    "listGpus",
  );
  row(score, "select3DMarkGpuCount", "# GPUs", "select", [1, 2]);
  row(score, "pcbsCpuMhz", "CPU Clock (MHz)", "number");
  row(score, "pcbsGpuMhz", "GPU Clock (MHz)", "number");
  row(score, "pcbsGpuMemoryMhz", "GPU Memory Clock (MHz)", "number");
  clickButton(score, "Calculate", "CalculatorCalculate");
  el(
    "p",
    score,
    "Scores assume stable clocks without thermal throttling. Empty clock fields use stock frequencies.",
  ).className = "hint";
  var outputs = el("div", document.getElementById(pages[0]));
  outputs.className = "panel";
  var outputTable = el("table", outputs);
  [
    "CpuScore",
    "GpuScore",
    "SystemScore",
    "GpuType",
    "SystemWatts",
    "CpuPriceNew",
    "CpuPriceUsed",
    "GpusPriceNew",
    "GpusPriceUsed",
  ].forEach(function (s, i) {
    var tr = el("tr", outputTable);
    el(
      "td",
      tr,
      [
        "CPU Score",
        "GPU Score",
        "3DMark Score",
        "GPU Type",
        "System Watts",
        "CPU Price New",
        "CPU Price Used",
        "GPU(s) Price New",
        "GPU(s) Price Used",
      ][i],
    );
    var c = el("td", tr);
    c.id = "td3DMark" + s;
    if (i === 2) c.className = "score-total";
  });
  [1, 2, 3].forEach(function (index) {
    var page = document.getElementById(pages[index]),
      review = el("div", page);
    review.id = page.id + "JobReview";
    review.className = "pcbs-job-review";
    review.hidden = true;
    var f = form(page, forms[index]),
      prefix = ["", "BuildMaker", "BuildUpgrader", "PartReplacer"][index];
    row(f, "input" + prefix + "Budget", "Budget", "number");
    if (index < 3) {
      row(
        f,
        "input" + prefix + "ReservedBudget",
        "Reserved Budget",
        "number",
        null,
        index === 1 ? 400 : 0,
      );
      row(f, "input" + prefix + "Target3DMarkScore", "3DMark Score", "number");
      row(
        f,
        "input" + prefix + "Target3DMarkScoreOffset",
        "Score Offset",
        "number",
        null,
        500,
      );
    }
    row(
      f,
      "input" + prefix + "ResultsRequested",
      "# Results",
      "number",
      null,
      10,
    ).placeholder = "10";
    if (index === 1) {
      ["Motherboard", "Case", "RamPart"].forEach(function (s) {
        row(
          f,
          "inputBuildMakerInclude" + s,
          s === "RamPart" ? "Include RAM Part" : "Include " + s,
          "checkbox",
          null,
          true,
        );
      });
      row(
        f,
        "inputBuildMakerNeedCpuOverclock",
        "Need CPU Overclock",
        "checkbox",
      );
      row(
        f,
        "selectBuildMakerCpuSocket",
        "CPU Socket",
        "select",
        ["Any"].concat(
          Array.from(
            new Set(
              Object.keys(sourceData.cpus).map(function (n) {
                return sourceData.cpus[n].socket;
              }),
            ),
          ),
        ),
      );
    }
    if (index > 1) {
      row(
        f,
        "input" + prefix + "OriginalSystemCase",
        "Case",
        "text",
        null,
        null,
        "listCases",
      );
      row(
        f,
        "input" + prefix + "OriginalSystemM2",
        "M.2",
        "text",
        null,
        null,
        "listStorage",
      );
      row(
        f,
        "input" + prefix + "OriginalSystemCpu",
        "CPU (Processor)",
        "text",
        null,
        null,
        "listCpus",
      );
      row(
        f,
        "input" + prefix + "OriginalSystem" + (index === 3 ? "Gpu1" : "Gpu"),
        "GPU (Graphics Card)",
        "text",
        null,
        null,
        "listGpus",
      );
      if (index === 3)
        row(
          f,
          "input" + prefix + "OriginalSystemGpu2",
          "GPU (Graphics Card) 2",
          "text",
          null,
          null,
          "listGpus",
        );
      row(
        f,
        "select" + prefix + "OriginalSystemGpuCount",
        "# GPUs",
        "select",
        [1, 2],
      );
      row(f, "input" + prefix + "RamSticks", "RAM Sticks", "number");
      row(
        f,
        "select" + prefix + "OriginalSystemRamSpeed",
        "RAM Speed",
        "select",
        Array.from(
          new Set(
            Object.keys(sourceData.mobos).reduce(function (arr, n) {
              return arr.concat(sourceData.mobos[n].memorySpeeds);
            }, []),
          ),
        ).sort(function (a, b) {
          return a - b;
        }),
      );
      row(
        f,
        "select" + prefix + "OriginalSystemMobo",
        "Motherboard",
        "select",
        Object.keys(sourceData.mobos),
      );
    }
    if (index < 3) {
      row(
        f,
        index === 1
          ? "selectBuildMakerGpuCount"
          : "selectBuildUpgraderOptionsGpuCount",
        "# GPUs",
        "select",
        ["Any", 1, 2],
      );
      row(
        f,
        index === 1
          ? "selectBuildMakerGpuType"
          : "selectBuildUpgraderOptionsGpuType",
        "GPU Type",
        "select",
        ["Any", "Air", "Water"],
      );
      row(f, "pcbsCpuBrand", "CPU Brand", "select", ["Any", "Intel", "AMD"]);
      row(
        f,
        "pcbsGpuBrand",
        "GPU Vendor / Brand",
        "select",
        Array.from(
          new Set(
            ["Any", "NVIDIA", "AMD"].concat(
              Object.keys(sourceData.gpus).map(function (n) {
                return sourceData.gpus[n].manufacturer;
              }),
            ),
          ),
        ),
      );
    }
    [
      ["Cpu", "CPU", "listCpus"],
      ["Gpu", "GPU", "listGpus"],
      ["Case", "Case", "listCases"],
      ["Mobo", "Motherboard", "listMobos"],
      ["Ram", "RAM", "listRams"],
    ].forEach(function (s) {
      var c = row(
        f,
        "pcbsFixed" + s[0],
        "Required " + s[1],
        "text",
        null,
        null,
        s[2],
      );
      c.closest("tr").hidden = true;
      c.closest("tr").setAttribute("data-pcbs-required", "");
    });
    control(f, "pcbsJobMetadata", "hidden");
    if (index === 3) {
      [
        ["Cpu", "CPU"],
        ["Gpu", "GPU 1"],
        ["Gpu2", "GPU 2"],
        ["Mobo", "Motherboard"],
      ].forEach(function (s) {
        row(
          f,
          "pcbsReplace" + s[0],
          s[1] + " Request",
          "select",
          ["Any", "Replace / Fix", "Upgrade"].concat(
            s[0] === "Gpu2" ? ["Add Dual-GPU"] : [],
          ),
        );
      });
    }
    var searchButton = clickButton(
      f,
      ["", "Get Builds", "Get Upgrades", "Get Parts"][index],
      [
        "",
        "BuildMakerGetBuilds",
        "BuildUpgraderGetUpgrades",
        "PartReplacerGetParts",
      ][index],
    );
    searchButton.setAttribute("data-pcbs-search", "");
    searchButton.disabled = sourceData.geometryReady === false;
    var res = el("div", page);
    res.id = page.id + "Results";
    res.className = "results";
    table(
      res,
      [
        "",
        "tableBuildMakerResults",
        "tableBuildUpgraderResults",
        "tablePartReplacerResults",
      ][index],
      index === 1
        ? resultHeaders
        : index === 2
          ? [
              "CPU",
              "# GPUs",
              "GPU Type",
              "GPU",
              "Motherboard",
              "Price",
              "Remaining",
              "Score",
              "Watts",
            ]
          : [
              "CPU",
              "GPU 1",
              "GPU 2",
              "Motherboard",
              "Price",
              "Remaining",
              "Score",
            ],
    );
  });
  var saveForm = el("div", document.getElementById(pages[1]));
  saveForm.id = "divBuildMakerSaveForm";
  saveForm.hidden = true;
  var sf = el("form", saveForm);
  sf.id = "formBuildMakerSave";
  saveForm.className = "panel";
  var commentLabel = el("label", sf, "Comment");
  var commentInput = control(
    commentLabel,
    "inputBuildMakerSaveComment",
    "text",
  );
  commentInput.id = "inputBuildMakerSaveComment";
  commentInput.maxLength = 2000;
  sf.onsubmit = function (event) {
    event.preventDefault();
  };
  var confirmSave = el("button", sf, "Save");
  confirmSave.type = "button";
  confirmSave.onclick = function () {
    if (!currentSaveRow || !currentSaveRow.isConnected) return;
    var record = { type: "build", comment: commentInput.value };
    [
      "pcCase",
      "cpu",
      "ramChannel",
      "ramSpeed",
      "ram",
      "gpuCount",
      "gpuType",
      "gpu",
      "mobo",
      "price",
      "budgetleft",
      "score",
      "watts",
    ].forEach(function (key, i) {
      record[key] = currentSaveRow.cells[i].textContent;
    });
    saveHistory(record);
    root.hideSaver(saveForm);
    root.pcbsFlush();
  };
  var cancelSave = el("button", sf, "Cancel");
  cancelSave.type = "button";
  cancelSave.onclick = function () {
    root.hideSaver(saveForm);
  };
  var history = document.getElementById(pages[4]);
  table(history, "table3DMarkScoreCalculatorHistory", [
    "CPU",
    "GPU",
    "Score",
    "Comment",
    "Request",
  ]);
  table(history, "tableBuildMakerSaves", [
    "CPU",
    "GPU",
    "Score",
    "Comment",
    "Request",
  ]);
  root.setListOptions();
  root.setSelectOptions();
  root.showPage(pages[0]);
  document.addEventListener("change", function (e) {
    if (e.target.name === "input3DMarkCpu") {
      root.updateRamSpeedOptions3DMark();
      root.updateRamChannelOptions3DMark();
    }
    if (e.target.name === "inputBuildUpgraderOriginalSystemCpu")
      root.updateMoboOptionsBuildUpgrader();
    if (e.target.name === "inputPartReplacerOriginalSystemCpu")
      root.updateMoboOptionsPartReplacer();
  });
})(window);
