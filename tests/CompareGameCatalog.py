"""Compare all five Build Maker part groups with local game tables; no game control or game-file writes."""
import argparse
import json
import re
from collections import defaultdict
from pathlib import Path

import UnityPy
from bs4 import BeautifulSoup

parser = argparse.ArgumentParser()
parser.add_argument("--game-path", type=Path, required=True)
parser.add_argument("--output", type=Path, required=True)
parser.add_argument("--calculator-catalog", type=Path, default=Path(__file__).resolve().parents[1] / "build/game-catalog/helper-catalog.json")
args = parser.parse_args()
project = Path(__file__).resolve().parents[1]
source = json.loads(args.calculator_catalog.read_text(encoding="utf-8-sig"))
env = UnityPy.load(str(args.game_path / "PCBS_Data/sharedassets1.assets"))
catalog = []
table_kinds = {"CPU": "cpu", "GPU": "gpu", "Water Cooled GPU": "gpu", "Cases": "case", "RAM": "ram", "Motherboard": "motherboard"}
for obj in env.objects:
    if obj.type.name != "TextAsset":
        continue
    data = obj.read()
    if data.m_Name not in table_kinds:
        continue
    rows = BeautifulSoup(data.m_Script, "html.parser").find_all("tr")
    cells = lambda row: [cell.get_text(" ", strip=True) for cell in row.find_all(["td", "th"], recursive=False)]
    header_index = next(i for i, row in enumerate(rows) if "ID" in cells(row) and "Part Name" in cells(row))
    headers = cells(rows[header_index])
    for row in rows[header_index + 1:]:
        record = dict(zip(headers, cells(row)))
        if not record.get("ID"):
            continue
        catalog.append({
            "id": record["ID"], "kind": table_kinds[data.m_Name],
            "manufacturer": record.get("Manufacturer", ""), "partName": record.get("Part Name", ""),
            "inShop": record.get("In Shop") == "Yes", "dummy": record.get("Class") == "Dummy",
            "sizeGb": int(record.get("Size each (GB)") or 0), "speedMhz": int(record.get("Frequency") or 0),
        })
index = defaultdict(list)
def normalize(name):
    name = name.replace("\u2122", "").replace("\u00ae", "").replace("\ufffd", "")
    return " ".join(re.sub(r"(?<=\s)[\u2013\u2014](?=\s)", "", name).split()).casefold()
for part in catalog:
    if part["inShop"] and not part["dummy"]:
        name = f'{part["manufacturer"]} {part["partName"]}'
        if part["kind"] == "ram":
            name += f' {part["sizeGb"]} GB {part["speedMhz"]} MHz'
        index[(part["kind"], normalize(name))].append(part["id"])
expected = []
unmatched = []
for kind, group in [("cpu", "cpus"), ("gpu", "gpus"), ("case", "pcCases"), ("ram", "rams"), ("motherboard", "mobos")]:
    matched = 0
    failures = []
    for name, part in source[group].items():
        if part["inShop"] != "Yes" or part.get("isHEMPart"):
            continue
        ids = index[(kind, normalize(name))]
        if len(ids) == 1:
            matched += 1
            expected.append({"kind": kind, "name": name, "id": ids[0]})
        else:
            failures.append({"name": name, "ids": ids})
    print(json.dumps({"kind": kind, "matched": matched, "conflicts": len(failures), "examples": failures[:5]}, ensure_ascii=True))
    unmatched.extend(failures)
args.output.parent.mkdir(parents=True, exist_ok=True)
args.output.write_text(json.dumps({"catalog": catalog, "expected": expected, "unmatched": unmatched}), encoding="utf-8")
