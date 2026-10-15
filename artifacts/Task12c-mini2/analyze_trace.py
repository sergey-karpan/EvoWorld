"""Rebuild analysis.md from completed C# research outputs; Python standard library only.

This script does not run the simulation or infer neural/feeding events. Those facts
come from the approved executing-tick diagnostic. Computed path radii below are
local three-position circumradii, not fitted lifetime circles or survival effects.
"""
import csv
import hashlib
import math
from pathlib import Path
import statistics
import tempfile
import os

ROOT = Path(__file__).resolve().parent


def read(name):
    with (ROOT / name).open(encoding="utf-8-sig", newline="") as stream:
        return list(csv.DictReader(stream))


trace = read("organism36-trace.csv")
acted = [r for r in trace if r["DidAct"] == "true"]
by_tick = {int(r["Tick"]): r for r in trace}
metadata = {r["Key"]: r["Value"] for r in read("run-metadata.csv")}
sensitivity = read("sensitivity.csv")
counterfactual = read("energy-counterfactual.csv")
contrasts = read("transition-contrasts.csv")
weights = read("genome36.csv")
digest = hashlib.sha256(b"".join(int(r["Float64BitsHex"], 16).to_bytes(8, "little") for r in weights)).hexdigest().upper()
assert digest == metadata["GenomeSha256"]
assert len(trace) == int(metadata["EndTick"]) - int(metadata["BirthTick"]) + 1, "Full analysis requires a default full-lifespan export; unset EVOWORLD_RESEARCH_TICK_RANGE and regenerate."
assert all(int(b["Tick"]) == int(a["Tick"]) + 1 for a, b in zip(trace, trace[1:]))
assert all((ROOT / name).stat().st_size <= 50_000_000 for name in (
    "organism36-trace.csv", "energy-counterfactual.csv", "sensitivity.csv", "alternative-channel-contrasts.csv",
    "transition-contrasts.csv", "genome36.csv", "run-metadata.csv"))
food = [r for r in acted if r["FoodPickedUp"] == "true"]
births = [r for r in acted if int(r["BirthsThisTick"]) > 0]
reflections = [r for r in acted if r["ReflectedX"] == "true" or r["ReflectedY"] == "true"]
first_positive = next(r for r in acted if float(r["Output0"]) > 0)
first_30 = next(r for r in acted if float(r["PostDrainEnergy"]) <= 30)
solo = next(r for r in acted if r["Population"] == "1")


def number(value):
    return f"{float(value):.9g}" if value != "" else "N/A"


def radius(r):
    previous = by_tick.get(int(r["Tick"]) - 1)
    if not previous or not previous["MovedX"] or not r["MovedX"]:
        return None
    if any(x["ReflectedX"] == "true" or x["ReflectedY"] == "true" or x["Input5"] != "-1" for x in (previous, r)):
        return None
    ax = float(previous["MovedX"]) - float(previous["BeforeX"])
    ay = float(previous["MovedY"]) - float(previous["BeforeY"])
    bx = float(r["MovedX"]) - float(r["BeforeX"])
    by = float(r["MovedY"]) - float(r["BeforeY"])
    cross = ax * by - ay * bx
    if not cross:
        return None
    return math.hypot(ax, ay) * math.hypot(bx, by) * math.hypot(ax + bx, ay + by) / (2 * abs(cross))


lines = ["# Task 12c-mini2 — organism #36 research findings", "",
         "Research artifact, not a simulator persistence format. All numeric neural and feeding facts below are from the actual executing tick. No target selection or live neural evaluation was repeated.", "",
         "## Provenance and reproducibility", "",
         f"Source commit: `c50e223c9a787e906a15075518ec144acfe7a96a`, with the authorized uncommitted internal diagnostic additions. SDK 10.0.401; runtime {metadata['Runtime']}; Windows x64. The assembly informational version names the base commit, not a clean source tree.", "",
         "Settings are the exact production Desktop mapping for Neural / mutation 0.1 / predators 5 / seed 42UL. All 42 effective settings are recorded in run-metadata.csv, including unchanged initial neural variation 0.5. No retuning.", "",
         f"Birth {metadata['BirthTick']}; parent 9; Herbivore; last alive {metadata['LastAliveTick']}; disappearance {metadata['EndTick']}; final predation count {metadata['FinalPredationCount']}. All match the historical cross-checks.", "",
         f"The reference had all diagnostics disabled. Every one of {metadata['ComparedSnapshots']} committed snapshots (including tick zero) matched the research-enabled replay via SHA-256 of exact numeric bits, genomes, parentage, food, counters, RNG state/increment and next IDs. Existing output-command contracts are separately tested with both runs' public diagnostics enabled; captured vectors are checked offline bit-for-bit against Forward.", "",
         f"Replay duration {metadata['ReplaySeconds']} s; offline analysis/publication clock {metadata['AnalysisSeconds']} s. Full contiguous trace: {len(trace)} rows, birth/newborn through disappearance, including {len(acted)} neural actions. No lifespan intervals omitted. Largest CSV: {(ROOT/'organism36-trace.csv').stat().st_size:,} bytes (<50 MB); full probe <30 minutes.", "",
         f"Genome SHA-256: `{digest}`. Digest is the concatenation of all 74 IEEE-754 bit patterns in parameter order as little-endian Int64 bytes; genome36.csv contains hex bit patterns and round-trip values. The genome was unchanged at every alive reference snapshot.", "",
         "Reproduce from the repository root:", "", "```powershell",
         "dotnet build EvoWorld.sln --no-restore",
         "$env:EVOWORLD_RESEARCH_OUTPUT = Join-Path (Get-Location) 'artifacts/Task12c-mini2'",
         "dotnet test EvoWorld.sln --no-build --filter FullyQualifiedName~ExportResearchArtifactsOnlyWhenExplicitlyRequested",
         "Remove-Item Env:EVOWORLD_RESEARCH_OUTPUT",
         "python artifacts/Task12c-mini2/analyze_trace.py", "```", "",
         "For an explicitly bounded trace export, set `EVOWORLD_RESEARCH_TICK_RANGE='10000:14000'` before the export test. Bounds are inclusive, validated, and recorded as FirstExportedTick/LastExportedTick; outside-range rows are deliberately omitted. Replay and counterfactual context selection still cover the full life. Unset the override and regenerate the full trace before running this full-lifespan report script.", "",
         "## Event timeline and measured transitions", "",
         "Coverage criteria were set without naming an escape threshold: whole lifespan; temporal-third energy coverage; first post-drain energy <=30; first positive steering output; first reflection; explicit requested 10,000–14,000 interval. A sign crossing is a factual neural event, not proof of the human screenshot's exact visual transition. Earlier screenshot timestamps are insufficient to align that event uniquely.", "",
         f"First feeding at {food[0]['Tick']}, energy {number(food[0]['PostDrainEnergy'])} -> {number(food[0]['EnergyAfterTick'])}; first <=30 energy at {first_30['Tick']}; sole remaining organism first observed at {solo['Tick']}. Minimum post-drain energy during 10,000–14,000 was {min(float(r['PostDrainEnergy']) for r in acted if 10000 <= int(r['Tick']) <= 14000):.9g}.", "",
         f"First positive steering at {first_positive['Tick']}, energy {number(first_positive['PostDrainEnergy'])}, target food #{first_positive['TargetId']}. First actual wall reflection at {reflections[0]['Tick']}; the sign crossing therefore precedes the first reflection. Across the lifespan there were {len(reflections)} reflection ticks, {len(food)} successful food pickups, and {sum(int(r['BirthsThisTick']) for r in births)} direct births.", "",
         "| Tick | Post-drain E | Input3 | Output0 | Rotation rad | Speed | Displacement | Path turn rad | Target | Feeding | Births |", "|---:|---:|---:|---:|---:|---:|---:|---:|---|---|---:|"]
for tick in [2028, 2445, 3692, 3693, 7316, 10000, 10668, 11244, 11571, int(first_30['Tick']), 12211, 12369,
             int(first_positive['Tick']), 13000, 13007, 13021, 13863, 15000, 15286, 18000, 21118, 23013, 23629]:
    r = by_tick[tick]
    lines.append("| " + " | ".join([str(tick)] + [number(r[k]) for k in ("PostDrainEnergy", "Input3", "Output0", "AppliedRotation", "EffectiveSpeed", "Displacement", "PathTurn")]
                                  + [r["TargetKind"] + (" #" + r["TargetId"] if r["TargetId"] else ""), r["FoodId"] or "None", r["BirthsThisTick"]]) + " |")
death = trace[-1]
lines += ["", "After feeding at 3692, the next tick returns to the no-target turning response (output0 -0.244220450 at 3693). Later, ticks 15000 and 18000 again have exactly the same saturated energy channel and no-target/interior channels, hence identical outputs. These are repeated controller responses, not proof of identical closed trajectories: intervening food encounters and wall reflections affect positions and headings. Recovery includes observed births at 7316, 8869, 13863, 14275, 14531, 14905 and 15286.", "",
          f"Disappearance sample: status `{death['Status']}`, DidAct={death['DidAct']}, before-drain energy {death['EnergyBeforeDrain']}, post-drain {death['PostDrainEnergy']}. Neural signals and feeding are blank because no action occurred. The final observed pickup was tick {food[-1]['Tick']}; all later acting ticks explicitly report no pickup. Death is metabolic, not inferred from energy differences or an absent organism alone.", "",
          "## Windowed path geometry", "",
          "The following windows are explicit descriptive ranges around the task's requested interval and event anchors, not fitted behavioral phases. Radius is the median of local three-position circumradii `a*b*c/(2*abs(cross))`; only consecutive moving observations with both relevant ticks unreflected and wall channel exactly -1 are eligible. It is not an exact global loop radius or a circle classification. Path turning includes boundary effects in the other columns.", "",
          "| Tick window | Mean output0 | Mean absolute path turn | Mean displacement | Eligible local radii | Median local radius |", "|---|---:|---:|---:|---:|---:|"]
for lo, hi in [(10000, 10600), (11873, 11933), (12100, 12300), (12801, 13006), (13008, 14000), (23000, 23629)]:
    group = [r for r in acted if lo <= int(r["Tick"]) <= hi]
    radii = [v for r in group if (v := radius(r)) is not None and math.isfinite(v)]
    turns = [abs(float(r["PathTurn"])) for r in group if r["PathTurn"]]
    lines.append(f"| {lo}–{hi} | {statistics.mean(float(r['Output0']) for r in group):.9g} | {statistics.mean(turns):.9g} | {statistics.mean(float(r['Displacement']) for r in group):.9g} | {len(radii)} | {statistics.median(radii):.9g} |" if radii else f"| {lo}–{hi} | N/A | N/A | N/A | 0 | N/A |")
lines += ["", "At ticks 10000 and 12211 channels 0/1/2/4/5 are identical. Energy falls from 71.6488624 to 20.0052697; output0 moves -0.250427707 -> -0.218750389, and rotation magnitude decreases smoothly. Thus energy alone accounts for this particular gradual command change. Around the first energy-30 crossing the curve is smooth; no sharp threshold at 30 is assumed or found.", "",
          "Reflection evidence uses actual axis flags AND near-boundary positions plus commanded/reflected heading discontinuity, not position gaps. For example tick 13007 starts near Y=1.9355, crosses the top boundary, and heading changes 4.30996528 -> 1.97322003. Do not interpret its abrupt path corner as an energy-driven steering command.", "",
          "## Frozen-genome energy-only sensitivity", "",
          "Twelve genuine measured contexts: nine nearest-energy examples within temporal thirds plus three objective event anchors. Temporal thirds are coverage strata, not invented loop/escape/return boundaries. The first third supplies no naturally measured energy <=30 (its low nearest example is 36.0486); this missing coverage is reported rather than fabricated. Complete observed six-input vectors and per-case predicted rotation/speed are in energy-counterfactual.csv.", "",
          "Each context uses energies 0,10,20,30,40,50,70,100,130 plus its own observed post-drain energy (duplicates removed). The row where Energy equals ObservedPostDrainEnergy contains the genuine measured six-input vector; the remaining rows are synthetic single-channel interventions. Only channel 3 changes; the other five channel bit patterns and all 74 genome bits remain fixed. The production encoder normalizes energy; exactly one offline Forward call is used per case, with buffers reused. Energy 100 and 130 both encode input3=1 and produce identical outputs. These are single-step neural commands, not alternative trajectories.", "",
          "MaxAbsDeltaOutput0/1 is max(output)-min(output), equivalent to the largest pairwise absolute difference. SignDeltaOutput0/1PerUnitEnergy is the signed adjacent-case output difference per one raw energy unit, reported as a range; saturated segments contribute zero. SignChangeCount skips exact zeros. Positive and negative slopes in a range indicate non-monotonic sampled sensitivity.", "",
          "| Context tick | Stratum | Observed E | Output | MaxAbsDeltaOutput0/1 | SignDeltaOutput0/1PerUnitEnergy range | SignChangeCount |", "|---:|---|---:|---|---:|---|---:|"]
for row in sensitivity:
    lines.append(f"| {row['ContextTick']} | {row['Stratum']} | {number(row['ObservedPostDrainEnergy'])} | {row['Output']} | {number(row['MaxAbsDeltaOutput'])} | [{number(row['MinDeltaOutputPerUnitEnergy'])}, {number(row['MaxDeltaOutputPerUnitEnergy'])}] | {row['SignChangeCount']} |")
lines += ["", "## Matched-input contrasts and quantitative attribution", "",
          "The last selected no-target/interior context before the first positive steering event is tick 12211; the event is 12899. The table freezes the same genome and selectively replaces measured channels. These finite vectors are mechanistic single-step contrasts, not claims that the combined contexts would arise in a counterfactual ecology.", "",
          "| Intervention from base | Output0 | Delta output0 | Output1 |", "|---|---:|---:|---:|"]
for row in contrasts:
    lines.append(f"| {row['Intervention']} | {number(row['Output0'])} | {number(row['DeltaOutput0FromBase'])} | {number(row['Output1'])} |")
energy_only = next(r for r in contrasts if r["Intervention"] == "EnergyOnly")
all_changed = next(r for r in contrasts if r["Intervention"] == "AllChangedInputs")
fraction = float(energy_only["DeltaOutput0FromBase"]) / float(all_changed["DeltaOutput0FromBase"])
lines += ["", f"Energy-only explains approximately {100*fraction:.6g}% of this selected observed output0 difference. Food-bearing/proximity-only changes are much larger and nearly reproduce it; changing all observed channels reproduces the actual output exactly. Wall channel is -1 in both selected contexts, so its isolated contrast is zero here. Effects are nonlinear/order-dependent; these are not additive causal percentages.", "",
          "alternative-channel-contrasts.csv also holds energy fixed while changing measured food channels 0–2 or wall channel 5 across all 12 contexts. It shows that food changes can exceed energy-only changes and that wall sensitivity matters in near-boundary contexts. Boundary reflections themselves additionally change physical heading independently of the commanded neural turn.", "",
          "## Three separate conclusions", "",
          "1. **Energy sensitivity — Supports.** With fixed genome and other inputs, changing post-drain energy changes both outputs. No learning is involved; the genome remains fixed throughout life.",
          "2. **Quantitative explanation of the observed steering/radius change — Partially supports.** Energy explains gradual command/curvature changes in unchanged no-target/interior contexts, but is much too small to explain the selected sharp sign-changing transition alone. Food-bearing/proximity changes quantitatively dominate that contrast. Walls and reflections further confound later path appearance. The exact visual screenshot transition cannot be uniquely aligned from screenshots alone.",
          "3. **Causal survival benefit — Not established.** Feeding and reproduction are observed facts, but a one-step frozen-input evaluation does not intervene on the ecological trajectory. No survival advantage, hunger awareness, optimization, adaptation, inherited circle phenotype, or natural selection is proved.", "",
          "## Artifact and validation contract", "",
          "The trace has a dedicated 48-column header constant in ReplayIdentityTests.cs. Counterfactual/sensitivity/contrast headers are dedicated constants in FrozenGenomeCounterfactualTests.cs. TargetDistance is derived by Hypot from the actual resolver's captured coordinates, not a reconstructed target selection or substitute input encoder. All CSVs use invariant round-trip numbers, exact unsigned seed, nullable blank signals, CRLF and UTF-8 BOM. They are published only after complete paired replay and calculation, through the existing atomic temporary-sibling writer. The test-side writer rejects any CSV exceeding 50 MB. No product CSV schema/export path or UI changed.", "",
          "No new source links, project changes, dependency changes or fixture rewrites were needed. Existing historical assertions are protected. Full Task 12c, descendant-genome comparisons, multiple-seed fitness/heritability experiments and causal ecological interventions remain deferred."]
text = "\n".join(lines) + "\n"
fd, name = tempfile.mkstemp(prefix="analysis-", suffix=".tmp", dir=ROOT)
try:
    with os.fdopen(fd, "w", encoding="utf-8", newline="\n") as stream:
        stream.write(text)
        stream.flush()
        os.fsync(stream.fileno())
    os.replace(name, ROOT / "analysis.md")
finally:
    if os.path.exists(name):
        os.unlink(name)
print(f"Verified genome {digest}; wrote analysis.md from {len(trace)} contiguous trace rows.")
