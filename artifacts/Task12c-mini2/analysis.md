# Task 12c-mini2 — organism #36 research findings

Research artifact, not a simulator persistence format. All numeric neural and feeding facts below are from the actual executing tick. No target selection or live neural evaluation was repeated.

## Provenance and reproducibility

Source commit: `c50e223c9a787e906a15075518ec144acfe7a96a`, with the authorized uncommitted internal diagnostic additions. SDK 10.0.401; runtime 10.0.12; Windows x64. The assembly informational version names the base commit, not a clean source tree.

Settings are the exact production Desktop mapping for Neural / mutation 0.1 / predators 5 / seed 42UL. All 42 effective settings are recorded in run-metadata.csv, including unchanged initial neural variation 0.5. No retuning.

Birth 2027; parent 9; Herbivore; last alive 23629; disappearance 23630; final predation count 24. All match the historical cross-checks.

The reference had all diagnostics disabled. Every one of 23631 committed snapshots (including tick zero) matched the research-enabled replay via SHA-256 of exact numeric bits, genomes, parentage, food, counters, RNG state/increment and next IDs. Existing output-command contracts are separately tested with both runs' public diagnostics enabled; captured vectors are checked offline bit-for-bit against Forward.

Replay duration 5.1808497 s; offline analysis/publication clock 0.2096615 s. Full contiguous trace: 21604 rows, birth/newborn through disappearance, including 21602 neural actions. No lifespan intervals omitted. Largest CSV: 12,208,900 bytes (<50 MB); full probe <30 minutes.

Genome SHA-256: `BA067A7A1EBE93F3D378C9F879AE44BB247A07AB6FC82711C8CCBA47AAE0B63B`. Digest is the concatenation of all 74 IEEE-754 bit patterns in parameter order as little-endian Int64 bytes; genome36.csv contains hex bit patterns and round-trip values. The genome was unchanged at every alive reference snapshot.

Reproduce from the repository root:

```powershell
dotnet build EvoWorld.sln --no-restore
$env:EVOWORLD_RESEARCH_OUTPUT = Join-Path (Get-Location) 'artifacts/Task12c-mini2'
dotnet test EvoWorld.sln --no-build --filter FullyQualifiedName~ExportResearchArtifactsOnlyWhenExplicitlyRequested
Remove-Item Env:EVOWORLD_RESEARCH_OUTPUT
python artifacts/Task12c-mini2/analyze_trace.py
```

For an explicitly bounded trace export, set `EVOWORLD_RESEARCH_TICK_RANGE='10000:14000'` before the export test. Bounds are inclusive, validated, and recorded as FirstExportedTick/LastExportedTick; outside-range rows are deliberately omitted. Replay and counterfactual context selection still cover the full life. Unset the override and regenerate the full trace before running this full-lifespan report script.

## Event timeline and measured transitions

Coverage criteria were set without naming an escape threshold: whole lifespan; temporal-third energy coverage; first post-drain energy <=30; first positive steering output; first reflection; explicit requested 10,000–14,000 interval. A sign crossing is a factual neural event, not proof of the human screenshot's exact visual transition. Earlier screenshot timestamps are insufficient to align that event uniquely.

First feeding at 2445, energy 36.4554402 -> 56.4554402; first <=30 energy at 11903; sole remaining organism first observed at 11571. Minimum post-drain energy during 10,000–14,000 was 13.7586346.

First positive steering at 12899, energy 17.7118315, target food #223. First actual wall reflection at 13007; the sign crossing therefore precedes the first reflection. Across the lifespan there were 51 reflection ticks, 50 successful food pickups, and 7 direct births.

| Tick | Post-drain E | Input3 | Output0 | Rotation rad | Speed | Displacement | Path turn rad | Target | Feeding | Births |
|---:|---:|---:|---:|---:|---:|---:|---:|---|---|---:|
| 2028 | 49.9675967 | -0.000648065063 | -0.240925182 | -0.0160207604 | 53.1946851 | 1.77315617 | N/A | None | None | 0 |
| 2445 | 36.4554402 | -0.270891197 | -0.210939816 | -0.0140268286 | 51.0975482 | 1.70325161 | -0.0140268286 | Food #82 | 82 | 0 |
| 3692 | 36.0485835 | -0.279028331 | -0.14189329 | -0.00943545369 | 44.5439876 | 1.48479959 | -0.00943545369 | Food #132 | 132 | 0 |
| 3693 | 56.0161802 | 0.120323604 | -0.24422045 | -0.0162398853 | 53.1720419 | 1.7724014 | -0.0162398853 | None | None | 0 |
| 7316 | 138.619194 | 1 | -0.234177738 | -0.0155720768 | 45.106698 | 1.5035566 | -0.0155720768 | Food #189 | 189 | 1 |
| 10000 | 71.6488624 | 0.432977249 | -0.250427707 | -0.0166526481 | 53.0967744 | 1.76989248 | -0.0166526481 | None | None | 0 |
| 10668 | 50.0034893 | 6.97865904e-05 | -0.240946103 | -0.0160221516 | 53.1945536 | 1.77315179 | -0.0160221516 | None | None | 0 |
| 11244 | 31.3392155 | -0.37321569 | -0.22214746 | -0.0147721014 | 42.2674205 | 1.40891402 | -0.0147721014 | Food #285 | 285 | 0 |
| 11571 | 40.7433517 | -0.185132966 | -0.235043332 | -0.015629636 | 53.2298214 | 1.77432738 | -0.015629636 | None | None | 0 |
| 11903 | 29.9854717 | -0.400290567 | -0.227047404 | -0.0150979322 | 53.2832279 | 1.7761076 | -0.0150979322 | None | None | 0 |
| 12211 | 20.0052697 | -0.599894606 | -0.218750389 | -0.014546207 | 53.3583052 | 1.77861017 | -0.014546207 | None | None | 0 |
| 12369 | 14.8855557 | -0.702288886 | -0.256078786 | -0.017028427 | 43.3857629 | 1.4461921 | -0.017028427 | Food #297 | 297 | 0 |
| 12899 | 17.7118315 | -0.64576337 | 0.0015705513 | 0.000104436679 | 60.0723084 | 2.00241028 | 0.000104436679 | Food #223 | None | 0 |
| 13000 | 14.4391029 | -0.711217941 | -0.0091239683 | -0.00060671495 | 61.1952047 | 2.03984016 | -0.00060671495 | Food #288 | None | 0 |
| 13007 | 14.2122802 | -0.715754397 | 0.00622064463 | 0.000413653136 | 67.122195 | 2.01305834 | -0.0475313663 | Food #288 | None | 0 |
| 13021 | 13.7586346 | -0.724827308 | -0.28388356 | -0.0188773563 | 44.276627 | 1.47588757 | -0.0188773563 | Food #288 | 288 | 0 |
| 13863 | 146.475095 | 1 | -0.240256796 | -0.0159763148 | 43.0109497 | 1.43369832 | -0.0159763148 | Food #255 | 255 | 1 |
| 15000 | 129.632597 | 1 | -0.252417313 | -0.0167849506 | 52.8286708 | 1.76095569 | -0.0167849506 | None | None | 0 |
| 15286 | 140.365266 | 1 | -0.248662926 | -0.0165352958 | 42.9581571 | 1.43193857 | -0.0165352958 | Food #320 | 320 | 1 |
| 18000 | 122.422837 | 1 | -0.252417313 | -0.0167849506 | 52.8286708 | 1.76095569 | -0.0167849506 | None | None | 0 |
| 21118 | 61.3894937 | 0.227789873 | -0.279934218 | -0.0186147376 | 49.1636052 | 1.63878684 | -0.0186147376 | Food #402 | 402 | 0 |
| 23013 | 19.9853289 | -0.600293422 | -0.218733116 | -0.0145450584 | 53.3584889 | 1.7786163 | -0.0145450584 | None | None | 0 |
| 23629 | 0.0249249504 | -0.999501501 | -0.200498881 | -0.0133325396 | 53.6336222 | 1.78778741 | -0.0133325396 | None | None | 0 |

After feeding at 3692, the next tick returns to the no-target turning response (output0 -0.244220450 at 3693). Later, ticks 15000 and 18000 again have exactly the same saturated energy channel and no-target/interior channels, hence identical outputs. These are repeated controller responses, not proof of identical closed trajectories: intervening food encounters and wall reflections affect positions and headings. Recovery includes observed births at 7316, 8869, 13863, 14275, 14531, 14905 and 15286.

Disappearance sample: status `MetabolicDeath`, DidAct=false, before-drain energy 0.024924950381450886, post-drain -0.007478302791849488. Neural signals and feeding are blank because no action occurred. The final observed pickup was tick 21118; all later acting ticks explicitly report no pickup. Death is metabolic, not inferred from energy differences or an absent organism alone.

## Windowed path geometry

The following windows are explicit descriptive ranges around the task's requested interval and event anchors, not fitted behavioral phases. Radius is the median of local three-position circumradii `a*b*c/(2*abs(cross))`; only consecutive moving observations with both relevant ticks unreflected and wall channel exactly -1 are eligible. It is not an exact global loop radius or a circle classification. Path turning includes boundary effects in the other columns.

| Tick window | Mean output0 | Mean absolute path turn | Mean displacement | Eligible local radii | Median local radius |
|---|---:|---:|---:|---:|---:|
| 10000–10600 | -0.246752288 | 0.0164082444 | 1.7715167 | 601 | 107.873605 |
| 11873–11933 | -0.227046029 | 0.0150978407 | 1.77610895 | 61 | 117.64003 |
| 12100–12300 | -0.203104826 | 0.0135058267 | 1.8525242 | 201 | 122.306619 |
| 12801–13006 | -0.133773775 | 0.00894549751 | 1.85865595 | 191 | 163.250967 |
| 13008–14000 | -0.214641126 | 0.0260441606 | 1.78071003 | 843 | 104.914003 |
| 23000–23629 | -0.21361055 | 0.0177639869 | 1.80103454 | 532 | 127.664732 |

At ticks 10000 and 12211 channels 0/1/2/4/5 are identical. Energy falls from 71.6488624 to 20.0052697; output0 moves -0.250427707 -> -0.218750389, and rotation magnitude decreases smoothly. Thus energy alone accounts for this particular gradual command change. Around the first energy-30 crossing the curve is smooth; no sharp threshold at 30 is assumed or found.

Reflection evidence uses actual axis flags AND near-boundary positions plus commanded/reflected heading discontinuity, not position gaps. For example tick 13007 starts near Y=1.9355, crosses the top boundary, and heading changes 4.30996528 -> 1.97322003. Do not interpret its abrupt path corner as an energy-driven steering command.

## Frozen-genome energy-only sensitivity

Twelve genuine measured contexts: nine nearest-energy examples within temporal thirds plus three objective event anchors. Temporal thirds are coverage strata, not invented loop/escape/return boundaries. The first third supplies no naturally measured energy <=30 (its low nearest example is 36.0486); this missing coverage is reported rather than fabricated. Complete observed six-input vectors and per-case predicted rotation/speed are in energy-counterfactual.csv.

Each context uses energies 0,10,20,30,40,50,70,100,130 plus its own observed post-drain energy (duplicates removed). The row where Energy equals ObservedPostDrainEnergy contains the genuine measured six-input vector; the remaining rows are synthetic single-channel interventions. Only channel 3 changes; the other five channel bit patterns and all 74 genome bits remain fixed. The production encoder normalizes energy; exactly one offline Forward call is used per case, with buffers reused. Energy 100 and 130 both encode input3=1 and produce identical outputs. These are single-step neural commands, not alternative trajectories.

MaxAbsDeltaOutput0/1 is max(output)-min(output), equivalent to the largest pairwise absolute difference. SignDeltaOutput0/1PerUnitEnergy is the signed adjacent-case output difference per one raw energy unit, reported as a range; saturated segments contribute zero. SignChangeCount skips exact zeros. Positive and negative slopes in a range indicate non-monotonic sampled sensitivity.

| Context tick | Stratum | Observed E | Output | MaxAbsDeltaOutput0/1 | SignDeltaOutput0/1PerUnitEnergy range | SignChangeCount |
|---:|---|---:|---|---:|---|---:|
| 7582 | TemporalThird1 | 99.9999286 | Output0 | 0.0519420341 | [-0.00093355644, 0.00014388547] | 0 |
| 7582 | TemporalThird1 | 99.9999286 | Output1 | 0.032522358 | [-0.000656574394, 0] | 0 |
| 3261 | TemporalThird1 | 50.0143856 | Output0 | 0.0778926683 | [-0.000907570897, 0] | 0 |
| 3261 | TemporalThird1 | 50.0143856 | Output1 | 0.0457462602 | [-0.00163584643, 0.000454677032] | 0 |
| 3692 | TemporalThird1 | 36.0485835 | Output0 | 0.0676142779 | [-0.000831274878, 0] | 0 |
| 3692 | TemporalThird1 | 36.0485835 | Output1 | 0.0316124345 | [-0.00134155617, 0.000630993616] | 0 |
| 13446 | TemporalThird2 | 99.987252 | Output0 | 0.0214410923 | [-0.000697395996, 0.000514194217] | 0 |
| 13446 | TemporalThird2 | 99.987252 | Output1 | 0.063563541 | [-0.00146885938, 0.000129558848] | 0 |
| 10668 | TemporalThird2 | 50.0034893 | Output0 | 0.0519420238 | [-0.00093355644, 0] | 0 |
| 10668 | TemporalThird2 | 50.0034893 | Output1 | 0.032522358 | [-0.000656574394, 0] | 0 |
| 12211 | TemporalThird2 | 20.0052697 | Output0 | 0.0519420238 | [-0.00093355644, 0] | 0 |
| 12211 | TemporalThird2 | 20.0052697 | Output1 | 0.032522358 | [-0.000656574394, 0] | 0 |
| 18692 | TemporalThird3 | 99.9997859 | Output0 | 0.0519420546 | [-0.00093355644, 0.000143884413] | 0 |
| 18692 | TemporalThird3 | 99.9997859 | Output1 | 0.032522358 | [-0.000656574394, 0] | 0 |
| 20852 | TemporalThird3 | 50.008759 | Output0 | 0.0519420238 | [-0.00093355644, 0] | 0 |
| 20852 | TemporalThird3 | 50.008759 | Output1 | 0.032522358 | [-0.000656574394, 0] | 0 |
| 23013 | TemporalThird3 | 19.9853289 | Output0 | 0.0519420238 | [-0.00093355644, 0] | 0 |
| 23013 | TemporalThird3 | 19.9853289 | Output1 | 0.032522358 | [-0.000656574394, 0] | 0 |
| 11903 | FirstEnergyAtMost30 | 29.9854717 | Output0 | 0.0519420238 | [-0.00093355644, 0] | 0 |
| 11903 | FirstEnergyAtMost30 | 29.9854717 | Output1 | 0.032522358 | [-0.000656574394, 0] | 0 |
| 12899 | FirstPositiveSteering | 17.7118315 | Output0 | 0.106946193 | [-0.00116715063, 0] | 1 |
| 12899 | FirstPositiveSteering | 17.7118315 | Output1 | 0.0603653587 | [-0.00118561082, 0] | 0 |
| 13007 | FirstReflection | 14.2122802 | Output0 | 0.0503535403 | [-0.00059210302, 0] | 1 |
| 13007 | FirstReflection | 14.2122802 | Output1 | 0.0400186969 | [0, 0.000524504706] | 0 |

## Matched-input contrasts and quantitative attribution

The last selected no-target/interior context before the first positive steering event is tick 12211; the event is 12899. The table freezes the same genome and selectively replaces measured channels. These finite vectors are mechanistic single-step contrasts, not claims that the combined contexts would arise in a counterfactual ecology.

| Intervention from base | Output0 | Delta output0 | Output1 |
|---|---:|---:|---:|
| None | -0.218750389 | 0 | 0.154558114 |
| EnergyOnly | -0.216748097 | 0.0020022916 | 0.155452239 |
| FoodOnly | -0.000937156637 | 0.217813232 | 0.423494494 |
| WallOnly | -0.218750389 | 0 | 0.154558114 |
| FoodAndWall | -0.000937156637 | 0.217813232 | 0.423494494 |
| AllChangedInputs | 0.0015705513 | 0.22032094 | 0.425663236 |

Energy-only explains approximately 0.908807% of this selected observed output0 difference. Food-bearing/proximity-only changes are much larger and nearly reproduce it; changing all observed channels reproduces the actual output exactly. Wall channel is -1 in both selected contexts, so its isolated contrast is zero here. Effects are nonlinear/order-dependent; these are not additive causal percentages.

alternative-channel-contrasts.csv also holds energy fixed while changing measured food channels 0–2 or wall channel 5 across all 12 contexts. It shows that food changes can exceed energy-only changes and that wall sensitivity matters in near-boundary contexts. Boundary reflections themselves additionally change physical heading independently of the commanded neural turn.

## Three separate conclusions

1. **Energy sensitivity — Supports.** With fixed genome and other inputs, changing post-drain energy changes both outputs. No learning is involved; the genome remains fixed throughout life.
2. **Quantitative explanation of the observed steering/radius change — Partially supports.** Energy explains gradual command/curvature changes in unchanged no-target/interior contexts, but is much too small to explain the selected sharp sign-changing transition alone. Food-bearing/proximity changes quantitatively dominate that contrast. Walls and reflections further confound later path appearance. The exact visual screenshot transition cannot be uniquely aligned from screenshots alone.
3. **Causal survival benefit — Not established.** Feeding and reproduction are observed facts, but a one-step frozen-input evaluation does not intervene on the ecological trajectory. No survival advantage, hunger awareness, optimization, adaptation, inherited circle phenotype, or natural selection is proved.

## Artifact and validation contract

The trace has a dedicated 48-column header constant in ReplayIdentityTests.cs. Counterfactual/sensitivity/contrast headers are dedicated constants in FrozenGenomeCounterfactualTests.cs. TargetDistance is derived by Hypot from the actual resolver's captured coordinates, not a reconstructed target selection or substitute input encoder. All CSVs use invariant round-trip numbers, exact unsigned seed, nullable blank signals, CRLF and UTF-8 BOM. They are published only after complete paired replay and calculation, through the existing atomic temporary-sibling writer. The test-side writer rejects any CSV exceeding 50 MB. No product CSV schema/export path or UI changed.

No new source links, project changes, dependency changes or fixture rewrites were needed. Existing historical assertions are protected. Full Task 12c, descendant-genome comparisons, multiple-seed fitness/heritability experiments and causal ecological interventions remain deferred.
