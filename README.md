# EvoWorld

A 2D artificial-life and evolution simulator built with C# and .NET 10.

## Overview

EvoWorld explores how complex behavior and evolutionary adaptation can emerge
from simple rules governing virtual organisms and their environment.
Organisms move, consume energy, find food, reproduce, inherit traits, and mutate,
allowing evolution through natural selection. Richer ecosystems and emergent
behavior remain long-term goals.

## Technology Stack

- C# / .NET 10 (SDK 10.0.401; `latestFeature` roll-forward, no prerelease SDKs)
- WPF (Windows desktop application)
- SkiaSharp.Views.WPF 4.152.2 (CPU canvas rendering)
- xUnit v3 4.0.2, xunit.runner.visualstudio 4.0.1, Microsoft.NET.Test.Sdk 18.10.1
- NuGet lock files for reproducible dependency resolution

## Solution Structure

- `EvoWorld.Core` — UI-independent simulation engine foundation, targeting `net10.0`
- `EvoWorld.Desktop` — WPF application shell, targeting `net10.0-windows10.0.19041`
- `EvoWorld.Tests` — automated tests, targeting `net10.0`

Desktop and Tests reference Core. Core references neither project and has no UI
dependencies. `AssemblyMarker` is a minimal infrastructure type used by the smoke
test; it contains no simulation behavior. Common C# build properties are defined
in `Directory.Build.props`; framework, output, WPF, and lock settings stay local
to each project.

## Building

Build the full solution on Windows 10 version 2004 (build 19041) or later,
with .NET SDK 10.0.401 or a compatible newer
.NET 10 feature band:

```powershell
dotnet restore EvoWorld.sln
dotnet build EvoWorld.sln --no-restore
```

Desktop and Tests generate `packages.lock.json` during restore. Commit these
files and review intentional dependency updates. After initial lock generation,
`dotnet restore EvoWorld.sln --locked-mode` verifies the recorded dependency graph.

Core can also be built independently on non-Windows platforms:

```powershell
dotnet build src/EvoWorld.Core/EvoWorld.Core.csproj
```

## Testing

```powershell
dotnet test EvoWorld.sln --no-build
```

Tests are xUnit v3 executables. `dotnet test` uses the SDK's default VSTest path
through `xunit.runner.visualstudio` and `Microsoft.NET.Test.Sdk`.
Microsoft.Testing.Platform is not selected as the `dotnet test` runner.
`IsTestingPlatformApplication=false` overrides xunit.v3 4.x's MTP application
default so .NET 10 permits execution through the VSTest adapter.

Desktop targets Windows SDK 10.0.19041 so SkiaSharp.Views.WPF 4.152.2 selects
its modern .NET asset group. This eliminates the earlier .NET Framework fallback
and `NU1701` warnings without changing direct package versions. The selected
group brings OpenTK 4.3.0 and OpenTK.GLWpfControl 4.2.3 transitively.

## Running

```powershell
dotnet run --project src/EvoWorld.Desktop/EvoWorld.Desktop.csproj --no-build
```

The window initially displays the seed-42 world at tick zero: 20 cyan organisms
with heading indicators and 100 green food items inside a dark world
rectangle. Organism radius is 5 world units, food radius is 3, and heading length
is 10. Draw order is background, boundary, food, organisms, then headings.

Start enables one UI-thread dispatcher timer; its first callback advances one
fixed simulation step. Pause preserves the current world. Start resumes it.
Reset stops playback and recreates the world from the selected controller and
mutation and initial-predator settings with the same seed, at tick zero, ready to start.
Closing stops and detaches the timer.

Task 11d adds the toolbar choices `Rule-based (Task 9)` and
`Neural (Tasks 11b–11c)`. The initial active mode is rule-based. Neural mutation
choices are 0 (off), 0.05, 0.1 and 0.5, initially selecting 0.1 for the demonstration;
the amplitude selector is enabled only while Neural is selected. Active mode,
neural enable state and the mutation setting are shown separately from pending
selections. The controller tooltip says `Changes take effect on Reset.`

Selections never change the current simulation, world, RNG or tick count.
`Pending Reset` appears when the effective selection differs from the active
configuration. Start/Resume and Pause operate on the active experiment and keep
changes pending. Reset always constructs a new simulation, even with unchanged
selections, and leaves it paused at tick zero. A construction failure preserves
the prior simulation and pending choices, reports an inline error, and requires
a successful Reset to recover. Reverting selections to the active configuration
removes the pending difference. Trait coloring and resize never restart the run.

Desktop forwards Core defaults through one explicit settings mapper, overriding
only neural enable, birth-time mutation and initial predator count. Rule-based mode uses mutation 0;
Neural mode retains Core's actual positive initial-weight variation (currently
0.5), including when birth-time mutation is 0. All other settings and the seed
remain the same. Identical selections and Reset reproduce histories on the
supported runtime; modes can have different histories because neural initialization
consumes extra draws. The mutation readout is a setting, not evidence of births
or observed mutation. Weight mutation at birth is not individual learning;
Desktop changes no Core defaults and performs no neural training.

Settings mapping and pending/active ownership are source-linked into Tests using
the existing viewport-helper pattern, without a WPF or Desktop assembly reference.
A narrow Desktop-only construction delegate tests failed Reset without modifying
Core or adding a UI testing framework.

The nominal interval is 1/30 real second. Delayed callbacks skip missed intervals;
there is intentionally no catch-up or elapsed-time accumulation. Each callback
advances exactly one configured simulated timestep. Painting only reads a single
immutable snapshot and never advances the engine or consumes random values.

The canvas uses top-left coordinates with positive Y downward, matching movement.
It scales the full world uniformly and centers it with margins when needed.
Radii, strokes, and headings scale with world units. Resize repaints without
advancing or resetting. Dimensions that cannot safely fit Skia float arithmetic,
or whose short axis would occupy less than one canvas pixel, display an empty
background and a status message instead of an invalid transformation.

Simulation or drawing errors stop playback and show a single inline error state.
Reset permits recovery; deterministic failures are not automatically retried.
Task 4.5 adds visualization and playback, without changing simulation behavior.

### Inspect an organism

Click an organism to inspect; click empty space to clear. The gold selection ring
and translucent gold trajectory are identified by the inspector legend. Selection
uses the nearest visible symbol, with smallest-ID ties, and follows that ID across
immutable snapshots. The compact panel collapses when selection is cleared.

The inspector shows alive/deceased state, tick, position, energy, inherited Speed,
Metabolism, SensingRadius, SteeringRate, heading, controller, and trail sample count.
The FIFO trail holds up to 500 committed positions, beginning at selection time,
then appending once per successful tick. Painting, pausing and resizing add no
points. Lines connect sampled committed positions, not continuous movement;
reflections can produce straight segments crossing the world edge. Selecting a
newborn after birth starts at its current committed position without reconstructing
its earlier history.

Neural outputs and commanded rotation (rad/tick) and effective speed (world units/s)
come from the executing tick, after energy drain and before movement, feeding and
reflection. Inherited Speed is a separate trait. Final reflected heading and sampled
displacement are not command proxies. Every command includes its producing tick;
rule-based commands display N/A (Rule-based controller). Other unavailable values
identify diagnostics disabled, no sample yet, did not act this tick, or organism not
found. Core commits null commands for absent, newborn or pre-movement deaths,
without reusing old output values. Diagnostics consume no RNG or extra forward pass.

Death freezes the trace and retains the historical last observed alive state,
including its timestamp and known energy; this is not energy measured at death.
The inspector says deceased and separately labels any last acted sample with its
original tick. Latest Core samples continue to report that the organism did not act.
Successful Reset clears selection, diagnostics and history. Failed Reset preserves
them, while pending controller choices leave the active inspection unchanged.
Pause/resume, trait coloring and resize preserve selection.

A brain is fixed during an organism's lifetime; weight mutation applies only to
offspring. A repeated circular path can result from stable controller outputs
rather than learning. Observed adaptation and population survival are not guaranteed.

## Desktop predator–prey demonstration

Task 12b3 adds the **Predators:** selector with exactly 0, 2, 5 and 10; startup
selects 0. This is the initial predator count added to the default 20 herbivores,
not the current living population. Changes remain pending alongside controller
and mutation choices until Reset. Start, Pause, trait coloring, resizing and
inspection use the active run. A successful Reset applies all effective choices,
clears inspection and history, and stays paused at tick zero. Failed construction
preserves the active run, inspection and pending choices for retry.

Predators have a purple outline independent of their existing trait-gradient fill.
Herbivores keep their original appearance; gold remains reserved for selection
and sampled trajectories. The adjacent legend identifies both species, green
food and gold inspection cues without drawing over the world. Outline strokes
are scaled and bounded to 1.5–3 device pixels; geometry and hit testing are unchanged.
Toolbar labels stay with their selectors, and wrapping readouts remain visible
at the 600×500 minimum window size. The existing inspector still scrolls.

Live readouts show **Herbivores**, **Predators** and cumulative **Predation** from
one committed statistics snapshot, alongside tick, total organisms, food, trait
mean/deviation, births and deaths. The active **Initial predators** readout is a
configuration value; living counts can change through births and deaths.

Rule-based and neural modes and the existing inspector remain available. The
explicit mapper forwards all 42 Core settings, overriding only controller enable,
effective birth-time neural mutation and initial predator count. Predator energy
remains 100 and the prey-energy transfer ratio remains 0.8. All other fields,
including the seed and initial neural variation, retain Core defaults.

Pursuit, attacks and consumption are produced solely by Core. Identical effective
settings and seed reproduce runs on the supported runtime; UI events and rendering
add no RNG draws. Captures within a particular horizon, predator survival,
population coexistence and adaptive success are not guaranteed. Species-specific
traits, prey evasion and ecological tuning remain deferred to later work.

## Population history and CSV export

Task 12b4 adds **Population history...** beside the playback status. It opens a
separate nonmodal window, closed by default, so the main world and scrolling
inspector keep their space. Cyan solid and purple dashed curves show living
herbivores and predators against **Tick**, including zero baselines. Lines connect
recorded points; they do not reconstruct events between samples. The view shows
the latest sample tick, count range, retained count, sampling interval and missed
boundaries. Resizing changes the chart only; there is no second timer.

Each active run records tick zero, then only actually observed committed ticks
divisible by the fixed `SamplingIntervalTicks = 100`. Repeated observations,
pending choices, paint, pause, inspection and off-boundary observations add no
records. Skipped scheduled boundaries are not synthesized: their count appears
in the next observed scheduled sample. A failed tick cannot publish history.
Observation errors retain previous records, are reported separately, and do not
undo a committed Core tick or silently stop playback.

Records contain scalar summaries, not retained worlds or organisms. Population,
food, cumulative births/deaths/predation, species traits and genomes come from one
committed snapshot. Interval event counts are checked differences from the
previous observed sample, covering the entire elapsed interval when observations
were missed; they are not guaranteed to describe exactly 100 ticks. Tick-zero
deltas are zero. Deaths use Core's cumulative counter, not a population difference.

Species-specific Speed, Metabolism, SensingRadius and SteeringRate summaries use
Core's ordered, scaled, compensated population-statistics semantics. Each includes
Count, Min, Max, Mean and StandardDeviation (population denominator). Empty species
have zero trait fields. Mean parameter variance is the arithmetic mean of the
74 population variances across living genomes of each species. One genome or
identical genomes gives zero; an empty species gives null. In Rule-based mode,
genome data is not accessed and both species' diversity values are null (blank
in CSV), explicitly labelled **inactive**. Dispersion does not
measure fitness, learning, adaptation, lineage or individual reproductive success.

Storage retains at most **50,000 samples** in a deterministic FIFO. The chart
reports **HISTORY TRUNCATED** and the dropped-record count when older records are
removed. Missed sampling boundaries and dropped retained records are separate
quantities. Extinct worlds continue scheduled observations. Reset constructs a
new simulation and tick-zero history before installing either; failed Reset keeps
the previous run, history, selection and pending choices. Successful Reset discards
the previous in-memory population history. **Export each run before Reset** to
compare experiments; history is not persistent storage.

The history window's **Export history CSV...** command exports a frozen copy of
the active run's retained samples. Cancellation is a no-op. The file uses
UTF-8 with BOM (0xEF 0xBB 0xBF), CRLF row separators, one header and ascending
exact Int64 ticks. Excel opens the file without manual encoding selection. Metadata
is repeated in constant columns; there is no preamble or automatic sidecar file.
Floating-point fields use invariant-culture round-trip `R` formatting; nullable
numbers are blank, never NaN or Infinity. `ModelTimeSeconds` is tick × configured
timestep when finite, otherwise blank. Metadata text uses RFC-4180 quoting when
needed. Seed remains an exact UInt64; no wall-clock timestamps are exported.

The fixed **66-column order** is shown below in groups for readability. In the
file these groups form one comma-separated header line:

```text
Tick,ModelTimeSeconds,OrganismCount,HerbivoreCount,PredatorCount,FoodCount,TotalBirths,TotalDeaths,TotalPredationEvents,BirthsSincePreviousSample,DeathsSincePreviousSample,PredationEventsSincePreviousSample,MissedSamplingBoundaries
HerbivoreSpeedCount,HerbivoreSpeedMin,HerbivoreSpeedMax,HerbivoreSpeedMean,HerbivoreSpeedStandardDeviation,HerbivoreMetabolismCount,HerbivoreMetabolismMin,HerbivoreMetabolismMax,HerbivoreMetabolismMean,HerbivoreMetabolismStandardDeviation,HerbivoreSensingRadiusCount,HerbivoreSensingRadiusMin,HerbivoreSensingRadiusMax,HerbivoreSensingRadiusMean,HerbivoreSensingRadiusStandardDeviation,HerbivoreSteeringRateCount,HerbivoreSteeringRateMin,HerbivoreSteeringRateMax,HerbivoreSteeringRateMean,HerbivoreSteeringRateStandardDeviation,HerbivoreMeanParameterVariance
PredatorSpeedCount,PredatorSpeedMin,PredatorSpeedMax,PredatorSpeedMean,PredatorSpeedStandardDeviation,PredatorMetabolismCount,PredatorMetabolismMin,PredatorMetabolismMax,PredatorMetabolismMean,PredatorMetabolismStandardDeviation,PredatorSensingRadiusCount,PredatorSensingRadiusMin,PredatorSensingRadiusMax,PredatorSensingRadiusMean,PredatorSensingRadiusStandardDeviation,PredatorSteeringRateCount,PredatorSteeringRateMin,PredatorSteeringRateMax,PredatorSteeringRateMean,PredatorSteeringRateStandardDeviation,PredatorMeanParameterVariance
Seed,Controller,NeuralGenomeActive,NeuralMutationAmplitude,InitialPredatorCount,TickDurationSeconds,SamplingIntervalTicks,DroppedSamples,TotalMissedSamplingBoundaries,CoreVersion,RuntimeVersion
```

`Controller` is `RuleBased` or `Neural`; `NeuralGenomeActive` is `true` or `false`.
Mutation and initial predator columns describe the effective active configuration.
`DroppedSamples` and total missed boundaries describe the frozen export; per-row
`MissedSamplingBoundaries` describes the gap before that row. A retained first row
may have nonzero interval deltas referring to an earlier, now-dropped record.
Core informational version (including source revision when available) and runtime
version identify the supported execution context; cross-runtime bitwise equivalence
is not promised.

Export writes and flushes a unique temporary sibling. A new target is published
by a non-overwriting move; an existing target uses atomic replacement. Failed
replacement has no overwrite fallback and preserves the previous file; cleanup
targets only the temporary sibling. I/O errors are shown in the history window
without altering the simulation or its history. No automatic exports, uploads,
database, learning or ecological tuning are introduced.

## Population Statistics

Task 10 makes evolutionary dynamics observable without producing new behavior.
`Simulation.GetStatistics()` returns an immutable `PopulationStatistics` value:
committed `TickCount`, current `OrganismCount` and `FoodCount`, `TotalBirths` and
`TotalDeaths`, cumulative `TotalPredationEvents` (Task 12b2), plus `TraitStatistics` for Speed, Metabolism, SensingRadius and
SteeringRate. Each trait contains `Min`, `Max`, `Mean`, population
`StandardDeviation` (denominator `Count`) and `Count`. An empty population has
exact zero values in every trait field; identical values have exact zero deviation.

Births are current next-organism ID minus the immutable starting next-ID baseline.
Deaths are actual starting organism count plus births minus current count. Both
baselines are captured after constructor overrides: sparse IDs, high injected
counters and a custom population differing from configured counts do not imply
events before injection. Counters describe events since this instance's starting
snapshot. Checked long arithmetic and nonnegative invariant checks reject impossible
histories. Baselines never change during ticks.

The calculation captures one advancement reference and consumes zero RNG outputs.
It uses three ordered passes per trait: extrema, compensated mean of offsets scaled
by the range, then compensated squared deviations in that scaled space. Scaling
back only at the end avoids overflowing sums and squares, including near
`double.MaxValue`, and retains small spreads near large values. Computation is
O(n), with constant temporary storage and no per-organism allocations, sorting,
cache or trait tracking in the tick pipeline. Like advancement, reading is single-threaded and
supported between completed ticks, with no concurrent advancement guarantee.
`GetStatistics(out SimulationState snapshot)` also returns the exact snapshot
used, allowing rendering to pair world and observations consistently.

Desktop's toolbar selector offers None (original cyan), Speed, Metabolism,
SensingRadius, SteeringRate and Energy. Selected values use the current population's
range for a blue-to-red gradient; equal values use neutral cyan. Genetic ranges
come from matching statistics; energy bounds come directly from the same captured
world. The compact summary shows the selected genetic trait's mean and deviation
(Speed for None/Energy), births and deaths, including after paused Reset.
These controls only observe the simulation.

There is no learning or fitness tracking. Task 12c-mini records parentage and
observed direct-child counts. Histograms, correlations, lineage trees and
generational statistics, detailed per-parameter neural weight statistics, detailed predation statistics and
advanced visualization remain deferred. Measurement does not itself
cause evolution or guarantee an adaptive result.

## Optional Neural Control

Task 11a introduced `NeuralGenome`, an immutable fixed 6 → 8 → 2 tanh network
with 74 defensively copied weights and biases in [-8,8]. `Weights` exposes a
read-only span, `CreateZero()` creates zero parameters, and `Forward(inputs,
outputs)` evaluates normalized inputs into caller-provided storage without
per-call heap allocation. Task 11b integrates this controller into the simulation.

`EnableNeuralControl` defaults to false, preserving Task 9 steering, Task 10
behavior and historical RNG contracts. Every organism has a non-null `Brain`;
legacy constructors share an immutable zero brain. When enabled, neural control
replaces Task 9 steering completely, including when the brain is zero or no food
is sensed. Zero brains preserve heading before reflection and inherited movement
speed. Adult replacements retain the exact brain reference. Newborns inherit
that reference when neural mutation is inactive; otherwise they receive a new
immutable genome derived from their own parent. Newborns first act next tick.

The 38-argument settings constructor appends the first five settings below;
Task 11c adds a 39-argument constructor appending the mutation amplitude:

| Setting | Default | Validation |
|---|---:|---|
| EnableNeuralControl | false | Explicit opt-in |
| NeuralWeightMin | -1 | Finite, within [-8,8], at or below zero |
| NeuralWeightMax | 1 | Finite, within [-8,8], at or above zero, greater than minimum |
| InitialNeuralWeightVariationAmplitude | 0.5 | Finite, nonnegative |
| SpeedModulationAmplitude | 0.5 | Finite, nonnegative, strictly less than 1 |
| NeuralWeightMutationAmplitude | 0 | Finite, nonnegative; no finite upper limit |

All older signatures retain these defaults. Neural settings are validated even
when disabled; custom brains need only satisfy `NeuralGenome`'s [-8,8] bounds.

After drain and before movement/feeding, survivors encode these six channels
from their starting position, heading and inherited traits. Food-bearing terms
below describe herbivores; predators use the corresponding prey-bearing terms
from the previous snapshot, with indices 3–5 unchanged:

| Input | Meaning |
|---|---|
| 0 | Sine of shortest signed relative food bearing; exact half-turn uses +pi |
| 1 | Cosine of that bearing (alignment) |
| 2 | `1 - 2 * clamp(distance / SensingRadius, 0, 1)` |
| 3 | `2 * clamp(post-drain energy / 100, 0, 1) - 1` |
| 4 | `2 * clamp(fraction(Speed, MinSpeed, MaxSpeed), 0, 1) - 1` |
| 5 | `1 - 2 * clamp(nearest wall distance / SensingRadius, 0, 1)` |

Herbivore food eligibility and ties use Task 9's nearest not-yet-consumed food and smallest
ID. No food gives `(0,1,-1)` for channels 0–2; positive-radius coincidence gives
`(0,1,1)`. Zero radius disables sensing and gives channel 5=-1. Food regenerated
this tick remains unavailable. Distance ratios use `FoodDistance`'s scaled
representation; speed fractions use power-of-two scaling and saturate custom
out-of-range traits. A tiny positive distance ratio may round to zero without
changing direction into the coincidence convention. All channels are checked
finite and within [-1,1].

Output 0 commands signed rotation, multiplied by the overflow-safe limit
`min(SteeringRate * timestep, pi)`, before ordinary movement and reflection.
Zero rotation preserves the exact heading. Output 1 gives multiplier
`1 + SpeedModulationAmplitude * output1`; at the default amplitude, speed lies
within 0.5–1.5 times inherited Speed. `Math.Tanh` can round to exactly +/-1;
the strict amplitude bound still keeps the multiplier positive. Modulation
affects movement only, preserving inherited Speed and metabolic drain. Effective
speed and movement-distance overflow throw before publication, retaining the
existing reflection limitations and transactional rollback. This remains
deterministic on the supported runtime, without a cross-platform bitwise promise.

Initialization completes placement, Task 8 variation and Task 9 variation before
neural weights. With neural control enabled and positive initial amplitude,
each organism consumes exactly 74 draws in parameter order, mapping
`(2 * output / 2^32 - 1) * amplitude` and clamping to the configured weight range.
Disabled control or zero amplitude assigns shared zero brains with no neural draws.
Let N be initial organisms and F initial food; Neural On below means that phase
is active, requiring both enable=true and amplitude>0:

| Task 8 variation | Task 9 variation | Neural | Total initialization draws |
|---|---|---|---|
| Off | Off | Off | 3N + 2F |
| On | Off | Off | 5N + 2F |
| Off | On | Off | 5N + 2F |
| On | On | Off | 7N + 2F |
| Off | Off | On | 3N + 2F + 74N |
| On | Off | On | 5N + 2F + 74N |
| Off | On | On | 5N + 2F + 74N |
| On | On | On | 7N + 2F + 74N |

Each Task 8/9 group is inactive when both amplitudes are zero; otherwise it draws
both channels per organism. Encoding and forward evaluation consume zero RNG.
Food regeneration still draws two per spawn before reproduction. Each birth
first draws position, heading, Speed, Metabolism, SensingRadius and SteeringRate.
Inactive neural mutation adds no draws; active mutation adds 74 in ascending
parameter order, even if every result clamps or rounds to the parent value.

Task 11c activates mutation only when neural control is enabled and
`NeuralWeightMutationAmplitude > 0`. Each parameter uses
`delta = (2 * (output / 2^32) - 1) * amplitude`, followed by bounded addition
to the raw parent weight and clamping to `NeuralWeightMin..NeuralWeightMax`.
Amplitude is uniform variation, not a standard deviation. The parent genome
is unchanged, including custom parents outside the narrower production bounds.
Every active birth constructs a fresh genome; identical child values are allowed.

Addition compares delta against `upper - parent` and `lower - parent` before
forming a candidate. The positive comparison saturates at upper, the negative
comparison at lower; otherwise ordinary addition and clamping apply. Endpoint
differences are bounded by 16, so amplitudes up to `double.MaxValue` safely
saturate without non-finite candidates. This uses ordinary binary64 arithmetic,
without FMA or epsilon comparisons. Actual failures still roll back the complete
transaction, including tentative genomes, RNG and both ID counters.

Initialization remains governed solely by the initial amplitude; the eight-mode
matrix above is unchanged. The combined neural initialization/birth contract is:

| Neural control | Initial amplitude | Mutation amplitude | Neural init draws per organism | Draws per successful birth |
|---|---|---|---:|---:|
| Disabled | Any valid | Any valid | 0 | 6 |
| Enabled | 0 | 0 | 0 | 6 |
| Enabled | Positive | 0 | 74 | 6 |
| Enabled | 0 | Positive | 0 | 80 |
| Enabled | Positive | Positive | 74 | 80 |

Skipped births consume zero draws. One food spawn and two births consume
fourteen outputs when mutation is inactive, or 162 when active. The default
mutation amplitude zero preserves Task 11b's exact reference inheritance and
RNG contract. Equivalent settings, seed and runtime reproduce complete genomes
and subsequent behavior; adaptive improvement is not guaranteed. Independent
Task 11c golden calculations and their invocation are stored in
`tests/EvoWorld.Tests/Fixtures/neural_mutation_reference.py`.

Statistics retain their existing API and inherited-trait aggregation; measured
populations may differ because neural control and mutation change behavior.
Learning, backpropagation, memory and detailed per-parameter neural weight statistics remain deferred.

## Species Infrastructure

Task 12a adds immutable `Organism.Species`: `Herbivore = 0` and `Predator = 1`.
Legacy organism constructors use Herbivore; undefined species are rejected.
Adult replacements preserve species and brain, and children inherit their parent's
species exactly. Mutation never changes species.

`SimulationSettings` adds `InitialPredatorCount` (default 0, nonnegative) and
`InitialPredatorEnergy` (default 100, finite and positive), appended in that order
by a 41-argument constructor. All prior signatures and defaults remain available.
`InitialOrganismCount` still counts herbivores. Widened arithmetic rejects a combined
population outside the integer ID/array-length domain; initialization checks the
runtime array limit before allocation or random draws. Counts within that limit
remain subject to available memory.

Initialization places H herbivores, then P predators, then F food items. Herbivores
receive IDs 0..H-1 and energy 100; predators receive IDs H..H+P-1 and configured
energy. The shared next-organism ID starts at H+P. Placement consumes
`3 * (H + P) + 2 * F` PCG32 outputs, X/Y/heading for each organism and X/Y for food.
Existing coordinate mapping and half-open bounds apply to both species.

Optional Speed/Metabolism and SensingRadius/SteeringRate variation phases follow,
then neural initialization, each processing all organisms in ascending ID order.
Each active trait group consumes two outputs per organism; active neural
initialization consumes 74. Trait baselines, bounds and brain topology are shared.
Births still consume six outputs, or 80 with active neural mutation; food
regeneration still consumes two. The population ceiling counts both species,
including when the initial population exceeds it.

`PopulationStatistics` adds current `HerbivoreCount` and `PredatorCount`, whose
sum equals `OrganismCount`. Existing trait moments and derived birth/death totals
cover all organisms. Species counts use the same committed snapshot and zero RNG.

Task 12a initially gave both species the same behavior. Task 12b1 now makes
predators pursue herbivores and skip food pickup; drain, movement, reproduction
and death retain their shared rules. Task 12b2 adds attack and prey consumption;
species-specific trait baselines remain deferred to Task 12c.

With zero predators, historical placement, traits, genomes, RNG contracts and
behavior remain unchanged. Equivalent settings and seeds reproduce mixed worlds
and subsequent ticks on the supported runtime. Observer configuration and diagnostic
publication remain atomic; Task 12b1 adds the sensed `TargetKind` to
`OrganismDiagnosticSample`. Species is still resolved from the world. Desktop defaults
remain herbivore-only, and controls, inspector, trajectory and pending/active
semantics are unchanged. Task 12b3 adds a predator outline and demonstration selector.

The independent mixed initialization reference and generated fixture are stored
under `tests/EvoWorld.Tests/Fixtures/`. Reproduce the fixture from the repository root:

```powershell
python tests/EvoWorld.Tests/Fixtures/predator_initialization_reference.py > tests/EvoWorld.Tests/Fixtures/PredatorInitializationGolden.cs
```

## Prey Detection and Pursuit

Task 12b1 resolves one executing-tick sensing target for each acting organism.
Herbivores retain nearest available food targeting and food pickup; predators
sense only `Species.Herbivore` organisms and never consume food. Both use the
existing inclusive SensingRadius and deterministic distance/smallest-ID ties.
Radius zero disables sensing, including coincident targets. Predators cannot
target themselves or other predators, irrespective of ID ranges or list position.

Prey positions and eligibility come from the single previous committed snapshot.
Earlier movement and metabolic deaths do not alter this tick's target selection:
a herbivore that dies during the tick remains eligible for sensing in that tick.
Newborns become eligible only in the next committed snapshot. Herbivore sensing
still excludes food consumed by earlier IDs; regenerated food is sensed next tick.

Rule-based pursuit reuses the existing shortest-angle, positive half-turn,
rate-limited steering and reflection rules. Neural control retains its 6→8→2
network and the same zero-based channels:

| Index | Herbivore | Predator |
|---|---|---|
| 0 | Sine of relative food bearing | Sine of relative prey bearing |
| 1 | Cosine of relative food bearing | Cosine of relative prey bearing |
| 2 | Normalized food proximity | Normalized prey proximity |
| 3 | Normalized post-drain energy | Unchanged |
| 4 | Normalized inherited speed | Unchanged |
| 5 | Normalized nearest-wall proximity | Unchanged |

No-target and coincident-target conventions and all numerical formulas remain
unchanged. Prey ranking reuses the scaled FoodDistance representation, preserving
large-coordinate and subnormal-radius safety without squared-distance overflow.

`OrganismDiagnosticSample.TargetKind` is immutable: None=0, Food=1, Prey=2. It
records the exact sensing choice used by the executing controller at the sample's
tick, including rule-based actors with zero SteeringRate. It describes neither
pickup nor prey capture; a target can be missed, eaten later, or die during the tick.
DidAct=false always reports None. No target ID/distance is retained or reconstructed.
The sample commits with world, RNG and IDs; failure preserves the previous sample.

Target selection, pursuit and telemetry add zero RNG draws or extra forward passes.
Initialization, regeneration and birth draw contracts are unchanged. Zero-predator
worlds preserve historical herbivore behavior and goldens; identical mixed runs
remain reproducible on the supported runtime. Task 12b1 introduced pursuit alone;
Task 12b2 adds contact consumption as described below. Prey evasion,
species-specific traits and further ecological behavior remain deferred.

Task 12b1 left Desktop and its inspector unchanged; TargetKind is available through
Core telemetry. Independent scalar input/output and herbivore regression oracles are in
`tests/EvoWorld.Tests/Fixtures/PredatorInputReference.cs` and
`tests/EvoWorld.Tests/Fixtures/HerbivorePursuitReference.cs`; no external tooling is
needed to reproduce these calculations.

## Attack and Consumption

Task 12b2 lets surviving predators consume at most one eligible herbivore after
their movement and reflection, in ascending stable organism ID order. Attack
range reuses `FoodPickupRadius`, including its boundary. Zero disables attacks
even at exact overlap; herbivore zero-radius food pickup is unchanged. Attacks
are independent of sensing radius and target, and work in both controller modes.

Candidates must exist in the previous committed snapshot and survive this tick's
metabolic drain. Lower-ID prey use their actual post-movement/feeding survivor
position; higher-ID prey use their previous position and a non-mutating check of
`Energy - Metabolism * TickDurationSeconds`. Non-finite required arithmetic fails
the tick. The nearest eligible prey wins, with smallest-ID distance ties using
the existing scaled `FoodDistance` comparisons.

Capture immediately excludes prey from further attacks and final survivors.
Higher-ID captured prey skip their entire action; lower-ID prey keep already
completed actions, including food consumption, but cannot reproduce. Nothing
rewinds. Multiple predators cannot consume the same prey: the first processing
ID wins. Newborns cannot be attacked until the following tick. Sensing continues
to read the previous snapshot, including prey already captured in this tick.

The immutable `PredationEnergyTransferRatio` defaults to 0.8 and must be finite
in [0,1]. A new 42-argument constructor appends it; all historical signatures and
defaults remain available. Gain is `ratio * prey.Energy`, strictly using the
prey's committed energy before this tick's drain or feeding. Thus gross gain is
independent of prey processing order; the model is not strict energy conservation.
Ratio zero still captures prey and records zero gain. Non-finite gain or final
predator energy throws `OverflowException` before publication.

Executing-tick diagnostics add nullable `PreyConsumedId` and
`EnergyGainedFromPrey`, populated after successful attacks in either controller
mode, while retaining the original neural commands and sensed `TargetKind`.
No attack means null outcomes. A tracked prey captured before acting reports
DidAct=false; one captured after acting retains its own same-tick commands.
The inspector determines death from the committed world and freezes its history.
Attack outcomes are exposed through Core; Task 12b3 makes initial predator choices,
species outlines and live counts visible without changing these Core mechanics.

`TotalPredationEvents` is a checked cumulative long counter, incremented once per
capture and retained after extinction. `TotalDeaths` still counts each disappearance
once. The counter, diagnostics, world, RNG and entity IDs publish atomically through
one advancement replacement; any failure preserves all previous components.
Attacks add zero RNG draws or extra neural evaluations. Regeneration still precedes
reproduction, whose draw contracts are unchanged, although mixed-world birth
histories can change. Zero-predator behavior and historical goldens are preserved;
equivalent runs reproduce on the supported runtime. Independent energy expectations
are scalar calculations in `PredatorEnergyTransferTests.cs`, without external tooling.

Species-specific traits, a separate `PredatorAttackRadius`, prey evasion,
cooperative hunting and ecological tuning remain deferred. This is contact
consumption, not a claim of full predator-prey dynamics or ecological coexistence.

## Architecture Decisions

PCG32, immutable configuration, world creation, fixed-step movement, survival,
food regeneration, reproduction, trait mutation, reactive food seeking, prey pursuit and consumption, and
population statistics and optional neural control are implemented. Desktop
provides dispatcher playback and trait coloring. Further ecosystem behavior remains planned.

- Core owns world state, entities, rules, simulation time, RNG, energy, and
  collision calculations. Desktop owns controls, rendering, viewport
  transformations, and playback scheduling. Rendering does not modify state
  or consume simulation RNG values. Desktop simulation execution uses the WPF UI thread.
- For a given implementation and supported execution environment, identical
  initial conditions, configuration, seed, and tick count must reproduce results.
  This is not a guarantee of bitwise equality across all hardware or runtimes.
- Random generation uses **PCG-XSH-RR 64/32, setseq (PCG32)** rather than
  `System.Random`. The simulation engine owns the RNG. The `ulong` seed is
  `initstate`, stored in `SimulationSettings`; the fixed stream is `initseq = 1`.
  Zero is a valid seed and receives the standard initialization sequence.
- PCG32 uses 64-bit state, 32-bit output, multiplier `6364136223846793005`,
  increment `(initseq << 1) | 1`, unchecked 64-bit arithmetic, and the XSH-RR
  permutation of the previous state. Initialization sets state to zero, sets the
  odd increment, advances once, adds `initstate` unchecked, then advances again.
  `CaptureState()` and `RestoreState()` preserve the complete state and increment
  without consuming outputs or reseeding. Restoration accepts any internal state,
  including zero, but rejects increments other than 3 without changing the RNG.
  The generator is not thread-safe; persistence remains future work.
  See the [PCG reference specification](https://www.pcg-random.org/download.html).
- Golden-sequence tests verify initialization and outputs for seeds 0, 1, 42,
  12345678901234567890, and `ulong.MaxValue`. Values were generated by compiling
  the official [PCG C basic reference](https://github.com/imneme/pcg-c-basic/tree/bc39cd76ac3d541e618606bcc6e1e5ba5e5e6aa3),
  independently of the production C# implementation. Organisms and rendering do not own RNGs, and
  seeds are not derived from wall-clock time.
- Simulation uses a configurable fixed timestep, initially `1/30` simulated
  second, independent of rendering. Stable entity and event ordering, explicit
  resource tie-breaking, and no outcome-affecting unordered or parallel processing
  support reproducibility. Core does not depend on wall-clock time.
- `SimulationSettings` is an immutable sealed class holding a `ulong` seed,
  tick duration in seconds, and world width/height as doubles. Continuous values
  for timestep and world dimensions must be finite and positive; traits and
  variation amplitudes permit zero. Zero is a valid seed. `CreateDefault()` uses seed
  42, timestep `1.0 / 30.0`, and world dimensions 1000 by 700.
  Initial counts default to 20 organisms and 100 food items; zero is supported
  and negative counts are rejected. The four-argument constructor retains its
  existing values and delegates using these same default counts.
  Baseline `OrganismSpeed` defaults to 50 world units per simulated second;
  movement uses each organism's inherited and potentially mutated Speed.
  Four- and six-argument constructors retain this default; a seven-argument
  constructor accepts explicit finite, nonnegative speed, including zero.
  Simulation time is represented by a `long TickCount` and the configured timestep.
- Future playback-speed controls will change steps per real second, never the physical timestep.
  Pause stops advancement; resume discards paused elapsed time. Reset with
  identical settings and seed reproduces the initial state and subsequent
  sequence. A future `SimulationRunner` will manage accumulation and catch-up limits.

## Initial World Creation

`WorldInitializer.Create(settings)` produces an immutable `WorldState` with
organisms and food. Entities are immutable sealed classes with reference equality
and stable IDs starting at zero separately in each category. World collections
own defensive copies behind read-only views; ordinary reads do not copy.

A fresh PCG32 consumes X, Y, heading for each organism in ascending ID order,
then X, Y for each food item in ascending ID order. Placement consumes exactly
`3 * InitialOrganismCount + 2 * InitialFoodCount` outputs, with no retries.
This is also the total when all initial trait variation amplitudes are zero
and the neural initialization phase is inactive;
enabled variation phases add the draws documented below.
IDs and initial energies (organisms: 100; food: 20) consume no random outputs.
Overlaps are allowed. Coordinates use `output / 4294967296.0 * dimension`;
headings use the same conversion scaled by `2 * Math.PI`.
If rounding reaches an exclusive upper bound, `Math.BitDecrement` corrects it
without another RNG call, including for subnormal dimensions.

Equivalent settings reproduce the same initial values in the supported execution
environment. A seed-42, 100-by-80, two-organism/two-food fixture verifies exact
double bits calculated independently in Python from Task 2 golden outputs.

## Fixed-Step Movement

`new Simulation(settings)` owns one persistent PCG32 stream. Initialization uses
the seeded generator and retains its stream state. `AdvanceTick()` advances
exactly one configured timestep. Movement and survival consume zero random outputs;
regeneration consumes two per successful spawn and reproduction six per birth,
or eighty with active neural mutation. The engine is
single-threaded and not thread-safe. `State` exposes an immutable `SimulationState`
with `World` and `TickCount`; previous world and simulation snapshots remain valid.

Movement uses `distance = organism.Speed * timestep`, then cosine/sine of the heading
for X/Y displacement. Exact cardinal headings use exact axis components to avoid
spurious perpendicular motion along walls. Reflection reverses the perpendicular
direction component; headings without a net reversal are preserved exactly.
Exact endpoint hits point inward. Upper-wall positions use `Math.BitDecrement`
to preserve the half-open world bounds. Surviving organisms retain these movement rules.

Reflection has bounded work independent of bounce count. Interior candidates use
ordinary floating-point addition. Boundary calculations align the original
coordinate, signed displacement magnitude, and dimension as exact dyadic integers,
then use a `UInt128` remainder with the conceptual period `2L`. Each aligned input
must occupy at most 126 bits; this leaves room for addition and the doubled period.
Parity therefore does not depend on a rounded candidate or floating-point quotient.
Subnormal and maximum dimensions work when the calculation fits this domain.
Excessive exponent separation (for example, unit movement in an epsilon-sized
world) is rejected rather than approximating reflection direction. No arbitrary-
precision arithmetic or per-bounce loops are used.

Non-finite distance, displacement, candidate, unsafe exact alignment, or tick-count
overflow throws `OverflowException`. Valid settings remain constructible even if
a particular tick cannot be represented. All next-state construction happens
before replacing one state reference, so failed ticks preserve world, count, and
RNG. Floating-point/trigonometric behavior outside the supported execution
environment is not claimed to be universally bitwise identical.

## Survival Rules

`OrganismEnergyDrainPerSecond` defaults to 1 energy unit per simulated second;
`FoodPickupRadius` defaults to 8 world units between centers. Both are immutable,
finite, and nonnegative. Four-, six-, and seven-argument settings constructors
retain their earlier parameters and use these defaults. The nine-argument overload
allows explicit values, including zero. Herbivore and food initial energies remain
100 and 20; predators use InitialPredatorEnergy.

Each tick processes organisms in ascending stable ID order:

1. Skip prey captured by earlier predators; otherwise subtract individual Metabolism
   times configured timestep, even when stationary.
2. Remove organisms with energy at or below zero; they neither move nor feed.
3. Resolve the species-specific target, steer, move survivors from previous positions,
   and reflect boundaries. Predators sense herbivores from the previous snapshot.
4. Herbivores at the resulting position claim at most one currently available food item
   within the inclusive pickup radius: nearest distance, then smallest food ID
   when computed distances are exactly equal.
5. Add any claimed food's energy without an energy cap and create a replacement organism.
   Predators skip food pickup and instead resolve contact consumption as described above.
6. Build surviving-organism and unconsumed-food collections, regenerate food, reproduce, then publish one
   complete immutable snapshot with the incremented tick count.

An exhausted organism cannot be rescued by food that tick. Earlier herbivore IDs
get first opportunity at contested food; later herbivores search only unclaimed
food. Movement never depends on positions updated earlier in the tick. Food stays
stationary until consumed. Unconsumed food objects are reused. IDs remain stable,
may have gaps, and are never renumbered. An extinct world continues ticking.
The internal test constructor accepts shrinking counts and strictly ascending,
unique, nonnegative IDs. Organisms cannot exceed the greater of combined initial count and
reproduction ceiling; food cannot
exceed the greater of initial food count and regeneration ceiling.

Energy cost, subtraction, and gain must remain finite; invalid arithmetic throws
`OverflowException` before publication. A failure after earlier deaths or food
claims leaves state, collections, tick count, and RNG unchanged. Survival consumes
zero random outputs and does not change initialization or movement fixtures.
Pause consumes no energy; Reset restores initial energies and populations.
The Desktop readout uses committed tick, organism, and food counts. The existing
renderer naturally omits consumed food and dead organisms.

Distances use .NET 10 `double.Hypot` on power-of-two-scaled coordinate differences,
retaining a separate exponent for comparisons. This avoids squaring overflow,
final-distance overflow, and subnormal rounding to a false coincidence or tie.
Zero radius requires identical coordinates. Comparisons use binary64 precision
without epsilon tolerances; this is not arbitrary-precision geometry or a promise
of bitwise portability across runtimes. Any non-finite normalized distance fails
atomically. Task 4's documented reflection limitations still apply.

## Renewable Food Supply

`FoodRegenerationIntervalSeconds` defaults to 2 simulated seconds and
`MaxFoodCount` to 200. All earlier settings constructors retain these defaults;
the eleven-argument overload appends explicit values. Intervals must be positive
and finite; the ceiling is nonnegative. Zero ceiling disables regeneration while
preserving initial food. Initial food may exceed the ceiling without being deleted.

The interval is converted once to a whole-tick period, rounding the exact ratio
of the supplied binary64 values to nearest with halfway values upward, with a
minimum of one tick. The default period is 60: events occur at ticks 60, 120,
180, etc., never tick zero. A shorter interval still creates at most one food per
tick. Nonintegral requests are approximated by the resulting whole-tick cadence.
For example, supplied 0.26 / 0.1 rounds to 3 ticks. Supplied 0.25 / 0.1 is slightly
below 2.5 mathematically and rounds to 2, unlike rounded floating-point division.
A focused UInt128 calculation avoids quotient overflow and unsafe long casts.
Periods beyond the positive long tick range have no events during the simulation.

After all Task 5 consumption, a due event adds exactly one food if the remaining
count is below the ceiling. Skipped events consume no RNG, allocate no IDs, and
are never recovered. Newly created food cannot be eaten until the next tick.
Regeneration continues in empty or extinct worlds, subject to the same ceiling.

Spawn coordinates reuse Task 3's exact mapping and upper-bound correction: two
PCG32 outputs, X then Y, each divided by 2^32 and multiplied by its dimension.
Food energy remains 20. Overlaps are allowed and require no retry draws.
Initialization and PCG32 golden sequences remain unchanged.

A long next-food-ID counter starts at the initial food count and never reuses IDs.
The internal test-world constructor accounts for both initial allocation and the
highest existing ID, and optionally accepts explicit removed-ID history.
The final allocatable ID is int.MaxValue; further permitted spawns throw
OverflowException. Cap-blocked events do not attempt allocation, even when IDs
are exhausted. Tick-count overflow also fails without committing changes.

Each permitted spawn uses a temporary copy of the exact current PCG32 state,
without seeding or initial draws. The world snapshot, stream state, and both next IDs
are held in one private immutable advancement record. All construction finishes
before replacing that single reference. Failures after tentative draws or feeding
leave every committed component unchanged, and retries start from the same stream.
The application remains single-threaded; no locks or background scheduling exist.

Food regeneration supplies a renewable resource. Task 9 implemented reactive
food seeking, but regeneration alone does not guarantee population survival.
Existing movement/reflection and binary64 distance limitations still apply.

## Asexual Reproduction

Surviving parents can produce one offspring per tick after feeding and food
regeneration. The immutable settings default to threshold 150, cost 50,
offspring energy 50, and population ceiling 500. All older constructor signatures
remain supported; the fifteen-argument constructor appends these values.
Threshold, cost, and offspring energy must be finite and positive; offspring
energy cannot exceed cost. A nonnegative ceiling of zero disables births without
deleting initial organisms, even when the initial population exceeds the ceiling.

Parents are considered in ascending ID order using their post-feeding energy.
Energy must meet the threshold, and subtracting cost must leave strictly positive
finite energy. Population capacity uses all survivors plus earlier births, so
deaths open slots and lower parent IDs get first opportunity. A skipped birth
deducts no cost, allocates no ID, and consumes no random values. Default births
conserve energy; a smaller offspring energy loses the difference as overhead.

The first six outputs of each successful birth use the same tentative PCG32 stream,
after any food-regeneration draws. First, output / 2^32 is multiplied by 2*pi;
cosine and sine of this angle are each multiplied by fixed radius 5 and added
to the parent's post-movement coordinates. Negative candidates clamp to zero;
candidates at or above a dimension clamp to Math.BitDecrement(dimension),
including a positive overflowing candidate. Placement never reflects or retries;
overlap is allowed. Second, output / 2^32 produces delta=(2*unit-1)*0.1 radians,
which is added to the parent's heading and normalized using Task 4's convention.
The parent heading is unchanged. Calculations follow this order using .NET math;
cross-runtime bitwise equivalence is not promised.
Third and fourth outputs determine Speed and Metabolism mutation, respectively.
They are consumed even when mutation amplitudes are zero.
Fifth and sixth outputs determine SensingRadius and SteeringRate mutation.
Active neural mutation then consumes 74 weight draws before offspring construction.

The child receives exactly configured offspring energy and a monotonic ID.
Its long allocation counter starts at the initial population; custom internal
test worlds also account for the highest ID and optional removed-ID history.
int.MaxValue is the final allocatable ID; another permitted birth throws
OverflowException. Skipped births at an exhausted counter remain valid.

Only the original survivors participate in reproduction. Children are appended
in ID order and appear in the birth snapshot, but do not move, drain energy,
feed, or reproduce until the next tick. Regenerated food is also unavailable
during its creation tick. World, RNG, and both allocation counters commit through
the same single immutable advancement-record replacement. Failure after any
tentative births or draws preserves every previous committed component.

## Lineage and observed sampled-path turning

Task 12c-mini adds immutable Core `Organism.ParentId`. Null identifies a founder
(including an explicit test-world original with unspecified parentage); a birth
records its exact parent's ID. Adult replacements preserve it even after the
parent dies. Provenance is never inferred from positions, ID adjacency or genomes.

Desktop observes each committed tick in a separate run-scoped scalar lineage
ledger. Founders observed at initialization have birth tick zero; adjacent
observations give exact birth and first-absent death ticks. Repeated ticks are
no-ops. A gap never creates interpolated events: new birth times and disappeared
organisms' death times are unknown. Alive-at-last-observation is explicit.
`DirectChildCount = 0` is an exact zero; null means unavailable or incomplete.
A gap conservatively invalidates every previously retained count and the counts
of newly seen organisms that could have reproduced during the gap. Later births
do not restore an unknown lifetime count. The inspector reports the ledger's
observation tick, so an observer failure cannot present stale data as current.

At **100,000 records**, only the oldest deceased records are evicted, in first
observation order. Living records are never evicted. Parent references and known
counts survive eviction of related records. `DroppedLineageRecords` and
`CapacityTruncated` report run-level loss; unrelated retained rows are not marked
incomplete solely because another row was evicted. If no safe eviction exists,
the named capacity error preserves the prior ledger; Core keeps advancing and
the existing observation-error area reports it. Recovery follows gap rules.
Reset constructs the replacement run and ledger before publishing either; failed
Reset retains the old run. Export before Reset to retain previous observations.

The selected-organism inspector also reports **Observed sampled-path turning
(retained selection trail; max 500 positions)**. Adjacent nonzero displacement
vectors give signed vertex angles using scaled unit vectors and `atan2(cross,
dot)`. X increases right and Y down: positive means clockwise on screen. Signed
mean and mean absolute turn use the same valid-turn denominator. Direction
consistency is `max(positive, negative)/(positive+negative)`, excluding exact zero
turns; it is unavailable when no nonzero turn exists. Insufficient data are N/A.
Stationary segments invalidate adjacent turns without bridging. Selection resets
and death-frozen trails retain their existing storage and drawing semantics.

This measures the retained selection window, not lifetime or population behavior.
The descriptor does not use per-point tick IDs to detect missed observations;
neighboring retained positions are treated as neighboring observations. Boundary
reflections are included, sparse sampling can alias loops, and reflection filtering,
curvature and radius estimates are deferred. A shared trajectory appearance cannot
establish ancestry or adaptation. Lineage confirms parentage, but correlations,
causation and selection still require comparative multi-seed experiments.

**Export lineage CSV...** explicitly freezes the scalar ledger and active metadata
into a separate file. Cancellation is a no-op. It uses UTF-8 BOM `EF BB BF`, CRLF,
invariant round-trip numbers, exact unsigned UInt64 seed, and the same temporary
sibling/atomic-replacement writer as population history. Nulls are blank. Flags
are `Complete`, or issues joined in this order: `MissingBirthTick`,
`MissingDeathTick`, `CapacityTruncated`, `ObservationGap`. Complete means complete
through the last observation, not necessarily deceased. The fixed 15 columns are:

```text
Seed,EffectiveMode,EffectiveNeuralMutationAmplitude,InitialPredatorCount,TimestepSeconds,DroppedLineageRecords,CapacityTruncated,OrganismId,Species,ParentId,BirthTick,DeathTick,IsAliveAtLastObservation,DirectChildCount,CompletenessFlag
```

`EffectiveMode` is `Rule-based` or `Neural`. `RuntimeVersion` is intentionally
omitted from lineage CSV; population-history CSV retains it and its unchanged
66-column contract and chart. Lineage CSV includes neither trajectories nor
lifetime turning metrics, phenotype–fitness correlations or per-individual feeding
success. Per-organism food-pickup counters and full Task 12c remain deferred.

## Energy-Dependent Behavior Research

Task 12c-mini2 provides a test-side research probe for organism **#36** in the
Seed 42 / Neural / birth mutation 0.1 / initial predators 5 experiment. It compares
an unobserved reference replay with a research-enabled replay, records executing
controller facts, and evaluates energy-only counterfactuals with the organism's
fixed genome. This is an explicit analysis workflow, not a Desktop control,
learning mechanism, general persistence format or ecological intervention.

### Internal diagnostic API

Core's `Simulation.CaptureResearchTrace` and `Simulation.LastResearchSample` are
**internal**, available to the existing friend test assembly. Capture is disabled
by default and uses the same organism ID as the existing public diagnostics:

```csharp
simulation.EnableDiagnosticsFor(36);
simulation.CaptureResearchTrace = true;
simulation.AdvanceTick();
OrganismResearchSample? sample = simulation.LastResearchSample;

simulation.CaptureResearchTrace = false; // Disable research capture only.
// simulation.DisableDiagnostics();     // Also clear the shared tracked ID.
```

Read after a successfully committed tick. Core retains only the latest immutable
scalar sample; the test harness retains the longer trace. Enabling capture or
changing the tracked ID does not expose a stale preceding sample. When research
capture is disabled or no ID is tracked, the getter returns null. Failed ticks
preserve the previous committed sample along with world, RNG, IDs and existing
diagnostics. Capture consumes no RNG and performs no second live neural evaluation.

### Captured facts and tick timing

`TickCount` identifies the executing tick's completed transaction, not a paint or
wall-clock observation. The pipeline timing is:

1. `BeforeX`, `BeforeY`, `BeforeHeading` and `EnergyBeforeDrain` come from the
   previous committed organism. Earlier-ID actions can already have consumed food
   or captured the tracked organism before its turn.
2. `PostDrainEnergy` is energy after this tick's metabolic drain, before control,
   feeding and reproduction. Nonpositive energy prevents action. A capture before
   action prevents even this organism's drain, leaving that value unavailable.
3. Target kind, ID and coordinates come from the **same resolver invocation**
   used for control, respecting the current consumed-food mask and snapshot-based
   prey sensing. No target is selected again for the trace.
4. Six inputs and two outputs are copied from the actual executing
   `NeuralControl.Evaluate` buffers. Input 0/1 are sine/cosine of relative target
   bearing, input 2 is normalized target proximity, input 3 is
   `2 * min(PostDrainEnergy / 100, 1) - 1`, input 4 is normalized inherited speed,
   and input 5 is normalized nearest-wall proximity. Without a sensed target,
   channels 0/1/2 are `0, 1, -1`. Outputs 0/1 control steering and speed modulation.
5. `AppliedRotation` and `CommandedHeading` precede movement and reflection.
   `EffectiveSpeed` is the executing speed command. `MovedX`, `MovedY`,
   `MovedHeading` and axis reflection flags describe the actual movement step.
6. `FoodPickedUp`, `FoodId` and `FoodEnergy` are recorded only at the real
   post-movement consumption branch. For an acting organism, false means no
   pickup; unavailable/non-action feeding is null, not an inferred false.
7. `BirthsThisTick` counts successful births by this organism in the actual
   reproduction loop. `CommittedX/Y/Heading` and `EnergyAfterTick` describe the
   final surviving organism, including feeding and reproduction energy cost.
   They are null if it was removed, even if it acted earlier in the tick.

Statuses distinguish `Absent`, `Newborn`, `MetabolicDeath`,
`CapturedBeforeAction`, `ActedCaptured` and `ActedSurvived`. `DidAct = false` is
different from a valid neural action producing zero outputs. Neural buffers and
commands are unavailable for non-actions; Rule-based actions also have no neural
buffers. `AliveAfterTick` independently records final snapshot membership.

The research CSV derives `TargetDistance` with `Hypot` from the actual captured
target coordinates; it is not a substitute target resolver or reconstructed neural
input. `Displacement` and `PathTurn` are measured from adjacent movement positions.
Physical path turning can differ from commanded steering at reflections.

### Reproducing the experiment and probe

The harness calls `ExperimentSettings.Create(new(ControllerMode.Neural, 0.1, 5))`:
seed **42UL**, 20 initial herbivores plus five predators, initial neural weight
variation **0.5**, and every other setting unchanged from the effective Desktop
mapping. Its identity gate requires #36 to be a Herbivore with `ParentId = 9`,
first appearing within ticks **2022–2032**; divergence fails instead of retuning.
The verified replay reproduced birth **2027**, last alive tick **23629**,
disappearance **23630**, and final predation count **24**. This reproducibility
applies to matching source/runtime conditions, not arbitrary platforms or runtimes.

From the repository root, after the normal restore/build prerequisites:

```powershell
dotnet build EvoWorld.sln --no-restore
dotnet test EvoWorld.sln --no-build --filter "FullyQualifiedName~ReplayIdentityTests|FullyQualifiedName~NeuralInputTraceTests|FullyQualifiedName~ObservationNeutralityTests|FullyQualifiedName~FrozenGenomeCounterfactualTests"

$env:EVOWORLD_RESEARCH_OUTPUT = Join-Path (Get-Location) 'artifacts/Task12c-mini2'
dotnet test EvoWorld.sln --no-build --filter "FullyQualifiedName~ExportResearchArtifactsOnlyWhenExplicitlyRequested"
Remove-Item Env:EVOWORLD_RESEARCH_OUTPUT
```

Ordinary test runs do not export files unless `EVOWORLD_RESEARCH_OUTPUT` is set.
The output directory is explicit; `artifacts/` is ignored by the repository's
existing rules. The default trace covers the full lifespan, including newborn
and disappearance ticks. A manual inclusive override is available:
`$env:EVOWORLD_RESEARCH_TICK_RANGE = '10000:14000'` before the export test.
It limits exported trace rows only; full replay and counterfactual context
selection still run. Metadata reports the actual exported bounds and deliberate
omissions. Remove the override with `Remove-Item Env:EVOWORLD_RESEARCH_TICK_RANGE`
to restore full output. Invalid ranges fail rather than silently truncating.

The paired reference/research replay compares exact state digests at every
committed snapshot, including tick zero, with RNG state, IDs, parentage, genomes,
food and counters. Separate tests preserve existing public diagnostic commands,
verify rollback, and check captured vectors offline against `NeuralGenome.Forward`.
The full probe is limited to 30 minutes and each CSV to 50,000,000 bytes. CSV
publication follows completed replay/calculation and uses the existing atomic
temporary-sibling writer, UTF-8 BOM `EF BB BF`, CRLF, invariant round-trip numbers
and blank nullable values. The historical 66-column population-history and
15-column lineage exports are unchanged.

When the generated research companion `artifacts/Task12c-mini2/analyze_trace.py`
is available, `python artifacts/Task12c-mini2/analyze_trace.py` rebuilds
`analysis.md` from the CSVs using Python's standard library. It verifies the genome
digest and requires the default full-lifespan trace. This companion and generated
reports are research artifacts in the ignored output directory, not product UI
features or automatically generated by ordinary tests.

### Research CSV schemas

The probe emits seven separate research files. Their dedicated headers are
defined in `ReplayIdentityTests.cs` and `FrozenGenomeCounterfactualTests.cs`;
the principal schema/order contracts have automated coverage. Field order is:

**`organism36-trace.csv` — one observed committed tick per row (48 columns):**

```text
Seed,OrganismId,ParentId,Tick,Status,DidAct,AliveAfterTick,BeforeX,BeforeY,BeforeHeading,EnergyBeforeDrain,PostDrainEnergy,Input0,Input1,Input2,Input3,Input4,Input5,Output0,Output1,TargetKind,TargetId,TargetX,TargetY,TargetDistance,AppliedRotation,EffectiveSpeed,CommandedHeading,MovedX,MovedY,MovedHeading,ReflectedX,ReflectedY,Displacement,PathTurn,CommittedX,CommittedY,CommittedHeading,EnergyAfterTick,FoodPickedUp,FoodId,FoodEnergy,BirthsThisTick,Population,Herbivores,FoodCount,PredationCount,GenomeSha256
```

**`energy-counterfactual.csv` — frozen-context single-step commands:**

```text
ContextTick,Stratum,ObservedPostDrainEnergy,Energy,Input0,Input1,Input2,Input3,Input4,Input5,Output0,Output1,PredictedRotation,PredictedEffectiveSpeed,GenomeSha256
```

**`sensitivity.csv` — one output's sensitivity per measured context:**

```text
ContextTick,Stratum,ObservedPostDrainEnergy,Output,MaxAbsDeltaOutput,MinDeltaOutputPerUnitEnergy,MaxDeltaOutputPerUnitEnergy,SignChangeCount
```

**`alternative-channel-contrasts.csv` — energy held fixed:**

```text
BaseTick,DonorTick,ChangedChannels,FixedInput3,BaseOutput0,ContrastOutput0,DeltaOutput0,BaseOutput1,ContrastOutput1,DeltaOutput1
```

**`transition-contrasts.csv` — selective changes around the first positive steering:**

```text
BaseTick,TransitionTick,Intervention,Input0,Input1,Input2,Input3,Input4,Input5,Output0,Output1,DeltaOutput0FromBase,ActualTransitionOutput0
```

**`genome36.csv` — all 74 parameters:**

```text
ParameterIndex,Float64BitsHex,RoundTripValue
```

**`run-metadata.csv` — identity, coverage, settings and execution provenance:**

```text
Key,Value
```

The genome digest is SHA-256 of all 74 IEEE-754 parameter bit patterns in order,
serialized as little-endian Int64 bytes. Metadata records the assembly's base
source revision, authorized uncommitted-source status, runtime, all 42 effective
settings, replay timings, identity and exported range. An informational-version
commit suffix does not imply a clean source tree.

### Frozen-genome methodology and interpretation

The probe copies #36's actual genome from its first committed alive snapshot and
verifies it remains unchanged. It selects nine genuine contexts near energies
100/50/20 across temporal thirds, plus available first-energy-at-most-30,
first-positive-steering and first-reflection anchors. Temporal thirds are coverage
strata, not asserted loop/escape/return phases. Missing natural energy coverage is
reported rather than fabricated.

For each context, channels **0, 1, 2, 4 and 5** are frozen bit-for-bit. Only
channel 3 varies, using the production energy encoder for energies
0, 10, 20, 30, 40, 50, 70, 100, 130 and the actual observed energy, with duplicates
removed. Each case makes exactly one **offline** `Forward` call with reused
buffers. The row where `Energy == ObservedPostDrainEnergy` contains the genuine
measured vector. Energies 100 and 130 both clamp to input3=1; these cases cannot be
distinguished by that channel. Zero-energy cases are offline evaluations, not
execution of organisms that should die before acting.

Sensitivity reports the maximum pairwise output difference, signed adjacent-case
slopes per raw energy unit (minimum/maximum for non-monotonic curves), and sign
changes excluding exact zeros, for both outputs. Supplementary contrasts hold
input3 fixed while changing measured food-bearing/proximity channels 0–2 or wall
channel 5. These synthetic vectors are finite network experiments, not alternative
ecological trajectories. Predicted rotation and speed use the measured context's
steering limit, inherited speed and production timestep/modulation.

The completed analysis supports energy sensitivity and quantitatively explains
some gradual changes in unchanged no-target/interior contexts. It only partially
supports energy as an explanation of the sharper observed change: in the selected
tick 12211-to-12899 contrast, energy-only variation accounts for about 0.91% of the
steering-output difference, while food-bearing/proximity changes are much larger.
That ratio is not an additive causal attribution. Wall reflections further affect
later paths; the exact earlier screenshot transition cannot be uniquely aligned.

**Energy sensitivity is not proof of learning, adaptation or survival causality.**
The genome is fixed during life; observed feeding and reproduction do not show
that the energy response caused survival. No sharp threshold at energy 30, hunger
awareness, inherited circle phenotype or fitness advantage is established. Full
Task 12c, multi-seed heritability/fitness analysis and causal ecological
interventions remain deferred.

## Individual Traits and Mutation

Each immutable organism has finite, nonnegative Speed and Metabolism. Movement
and metabolic drain use those individual values; zero speed and zero metabolism
are valid. The legacy entity constructor uses Speed=50 and Metabolism=1, while
production initialization explicitly uses OrganismSpeed and
OrganismEnergyDrainPerSecond as baseline traits. Adult replacements preserve
their traits exactly during movement, feeding, drain, and reproduction.
Food pickup radius remains global.

The 23-argument settings constructor appends eight values: initial speed and
metabolism variation amplitudes (both default zero), MinSpeed=0, MaxSpeed=200,
MinMetabolism=0, MaxMetabolism=5, SpeedMutationAmplitude=1, and
MetabolismMutationAmplitude=0.05. Bounds and amplitudes must be finite and
nonnegative, each minimum must be below its maximum, and baselines must lie
within explicitly supplied ranges. Earlier constructors expand upper bounds to
include large legacy baselines instead of clamping or rejecting them.

With both initial amplitudes zero, sensing/steering variation disabled,
and neural initialization inactive,
original Task 3 placement, headings, food,
and RNG state are preserved: 3 draws per organism and 2 per food. If either
amplitude is nonzero, complete all original placement first, then draw speed
and metabolism variation for each organism in ID order. This consumes two extra
draws per organism even when one amplitude is zero, without changing positions.

Initial variation and mutation use unit=output/2^32, signed=2*unit-1, then
candidate=center+signed*amplitude, clamped to inclusive configured bounds.
Variation is bounded uniform before clamping; amplitude is not a standard
deviation. A zero amplitude preserves the exact center when in range. Custom
adult values outside bounds remain valid and unchanged; their offspring values
clamp to bounds, including when amplitude is zero. Positive candidate overflow
unambiguously clamps to the upper bound. Invalid trait construction and
non-finite required movement or metabolic products fail the complete transaction.

Children inherit the specific parent's values, then mutate only at birth.
Food draws precede births; every birth draws position, heading, speed mutation,
metabolism mutation, sensing mutation, then steering mutation, followed by 74
neural weight draws when active. One food and two children consume fourteen
outputs with neural mutation inactive, or 162 when active. Later reproduction histories differ from Task 7 because of these extra
draws, while the PCG algorithm and homogeneous initial-world fixtures are unchanged.
All traits, RNG, and ID counters publish together; failed ticks leak no mutations
or random draws, and retries start from the same committed state.

Heritable variation may affect survival and reproductive success, allowing
selection to emerge. Adaptation, improvement, and population survival are not
guaranteed. No generic genome exists. Memory, learning,
sexual reproduction, crossover, speciation, and persistence remain deferred.
Task 10 implements basic trait-based coloring; advanced trait visualization
remains deferred.

## Reactive Sensing and Food Seeking

Organisms now also carry immutable, finite, nonnegative SensingRadius (world
units) and SteeringRate (radians per simulated second). Legacy entity constructors
default both to zero. Production initialization explicitly uses configured
baselines, defaulting to radius 30 and rate 2. The new 33-argument settings
constructor appends ten values in this order:

| Setting | Default |
|---|---:|
| OrganismSensingRadius | 30 |
| OrganismSteeringRate | 2 |
| InitialSensingRadiusVariationAmplitude | 0 |
| InitialSteeringRateVariationAmplitude | 0 |
| MinSensingRadius | 0 |
| MaxSensingRadius | 100 |
| MinSteeringRate | 0 |
| MaxSteeringRate | 5 |
| SensingRadiusMutationAmplitude | 2 |
| SteeringRateMutationAmplitude | 0.1 |

All values must be finite and nonnegative, minima below maxima, and explicit
baselines within bounds. Older constructors retain their signatures and defaults.
Mutation reuses Task 8's bounded uniform variation and clamping rules; adults
preserve all four traits. Newborns do not sense or steer until the following tick.

Initialization always completes original placement first. The optional Task 8
speed/metabolism phase follows, then a separate optional Task 9 sensing/steering
phase. Each enabled group consumes two outputs per organism, even if only one
amplitude is nonzero. With N organisms and F food items, the four modes consume
3N+2F, 5N+2F (old only), 5N+2F (new only), or 7N+2F (both). Old-only Task 8 values
and stream states remain unchanged; no traits are interleaved with placement.

With neural control disabled, after metabolic drain each herbivore searches food
not consumed by earlier IDs, using its starting position. FoodDistance selects the nearest within the inclusive
sensing radius, with smallest-ID ties. Radius zero disables sensing entirely,
including coincidence. Positive-radius coincident food is a valid nearest target
but preserves heading; pickup still happens only after movement. Regenerated
food is unavailable until the next tick.

For a noncoincident target, normalize Atan2(dy,dx), then use the shortest signed
difference in (-pi,pi], choosing positive rotation for an exact half-turn.
Limit rotation to SteeringRate*timestep without overshooting. Because no shortest
turn exceeds pi, larger products safely saturate at pi; overflowing steering
products are not errors. Movement/metabolic product overflow policies remain
unchanged. No target, zero rate, or zero rotation preserves the exact heading.
Steering precedes movement, and reflection determines the final heading without
a second steering pass. Stationary organisms may turn. Sensing and steering
consume no RNG and add no separate energy cost.

Availability is resolved sequentially, but other organisms' updated positions
never influence steering. All changes, draws, and ID allocations remain tentative
until the existing single-reference commit; failures and retries preserve the
complete prior transaction. The six historical outputs per successful birth extend
Task 8's first four draws and precede 74 neural mutation draws when active;
later reproduction histories shift intentionally. Skipped
births consume zero, and food regeneration still consumes two per successful spawn.

Food seeking is reactive: there is no stored target, memory, route planning, or
learning. Selection may affect sensory traits over generations, without guaranteed
adaptation. Multiple sensors, obstacle avoidance, flocking,
pathfinding, learning, sexual reproduction, advanced visualization, and persistence
remain deferred.

## Development Status

Task 1 established the solution structure, WPF application shell, dependency
configuration, and testing infrastructure. Task 2 implements PCG32, immutable
state snapshots, capture/restore, and validated immutable `SimulationSettings`,
with golden-sequence and configuration tests. Task 3 adds immutable organisms,
food, world snapshots, population configuration, and deterministic initialization.
Task 4 adds constant-speed fixed-step movement, reflection, and immutable
simulation snapshots. Task 4.5 adds world rendering and basic playback controls.
Task 5 adds baseline metabolism, food consumption, energy recovery, and death.
Task 6 adds deterministic food regeneration with transactional RNG and ID history.
Task 7 adds asexual reproduction, energy transfer, and bounded heading variation.
Task 8 adds individual Speed and Metabolism with inheritance and bounded mutation.
Task 9 adds individual sensing and steering with reactive food seeking.
Task 10 adds population statistics, derived birth/death counters since each
simulation's starting snapshot, and Desktop trait coloring and statistics display.
Task 11a adds the immutable 6→8→2 NeuralGenome infrastructure with validated
weights, deterministic forward evaluation, and comprehensive numerical tests.
Task 11b adds optional neural steering and speed modulation, deterministic
initial weights, and exact brain inheritance. Task 11c adds optional deterministic
neural weight mutation at birth with overflow-safe clamping and transactional RNG.
Task 11d adds Desktop controller and mutation choices with explicit Reset-to-apply
configuration and reproducible successive experiments.
Task 11e adds selected-organism inspection, a bounded sampled trajectory, and
exact executing-tick neural telemetry published with the simulation transaction.
Task 12a adds species identity, deterministic predator initialization, and population
species counts while preserving the existing shared behavior.
Task 12b1 adds snapshot-based prey detection and pursuit, herbivore-only food
pickup, and executing-tick TargetKind diagnostics.
Task 12b2 adds sequential contact consumption, snapshot-based prey energy transfer,
atomic predation counters and nullable executing-tick attack outcomes.
Task 12b3 adds Desktop predator choices with pending Reset semantics, purple
predator outlines, a legend, and live species and cumulative predation counts.
Task 12b4 adds bounded sampled population history, species trait and descriptive
genome dispersion summaries, population curves and explicit atomic CSV export.
Task 12c-mini adds authoritative parentage, bounded per-tick lineage observations,
selected-trail turning descriptors and a separate explicit lineage CSV export.
Full Task 12c ecosystem evolution remains deferred.

Additional traits, persistence, and a full configuration editor remain deferred.

Task 1.5 verified native SkiaSharp loading, CPU drawing with exact pixel checks,
and WPF `SKElement` paint callbacks before and after a programmatic resize.
The shell started with the correct window title and closed normally. Temporary
probes were removed. Visual appearance, placeholder visibility, and visual
resize behavior still require manual verification; GPU rendering was not tested.

Task 4.5 programmatic checks verified paint callbacks, organism/food/heading
pixels, timer controls, deterministic reset, resize repaint, error recovery, and
shutdown. Manual visual inspection of appearance and reflections remains pending.
Pure viewport math is source-linked into Tests without referencing WPF or Desktop.
