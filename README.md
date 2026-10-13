# EvoWorld

A 2D artificial-life and evolution simulator built with C# and .NET 10.

## Overview

EvoWorld explores how complex behavior and evolutionary adaptation can emerge
from simple rules governing virtual organisms and their environment.
The long-term goal is an ecosystem where organisms move, consume energy, find
food, reproduce, inherit traits, mutate, and evolve through natural selection.

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
Reset stops playback and recreates the original world with the same settings and
seed, at tick zero, ready to start. Closing stops and detaches the timer.

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

## Architecture Decisions

PCG32, immutable configuration, world creation, and fixed-step movement are
implemented. Further ecosystem behavior remains planned. Basic dispatcher
playback is implemented.

- Core will own world state, entities, rules, simulation time, RNG, energy, and
  collision calculations. Desktop will own controls, rendering, viewport
  transformations, and playback scheduling. Rendering will never modify state
  or consume simulation RNG values. Initial execution may use the WPF UI thread.
- For a given implementation and supported execution environment, identical
  initial conditions, configuration, seed, and tick count must reproduce results.
  This is not a guarantee of bitwise equality across all hardware or runtimes.
- Random generation uses **PCG-XSH-RR 64/32, setseq (PCG32)** rather than
  `System.Random`. The simulation engine will own the RNG. The `ulong` seed is
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
  independently of the production C# implementation. Organisms and rendering will not own RNGs, and
  seeds will not be derived from wall-clock time.
- Simulation uses a configurable fixed timestep, initially `1/30` simulated
  second, independent of rendering. Stable entity and event ordering, explicit
  resource tie-breaking, and no outcome-affecting unordered or parallel processing
  will support reproducibility. Core will not depend on wall-clock time.
- `SimulationSettings` is an immutable sealed class holding a `ulong` seed,
  tick duration in seconds, and world width/height as doubles. Continuous values
  must be finite and positive; zero is a valid seed. `CreateDefault()` uses seed
  42, timestep `1.0 / 30.0`, and world dimensions 1000 by 700.
  Initial counts default to 20 organisms and 100 food items; zero is supported
  and negative counts are rejected. The four-argument constructor retains its
  existing values and delegates using these same default counts.
  Constant `OrganismSpeed` defaults to 50 world units per simulated second.
  Four- and six-argument constructors retain this default; a seven-argument
  constructor accepts explicit finite, nonnegative speed, including zero.
  Simulation time is represented by a `long TickCount` and the configured timestep.
- Playback speed will control steps per real second, never the physical timestep.
  Pause will stop advancement; resume will discard paused elapsed time. Reset with
  identical settings and seed will reproduce the initial state and subsequent
  sequence. A future `SimulationRunner` will manage accumulation and catch-up limits.

## Initial World Creation

`WorldInitializer.Create(settings)` produces an immutable `WorldState` with
organisms and food. Entities are immutable sealed classes with reference equality
and stable IDs starting at zero separately in each category. World collections
own defensive copies behind read-only views; ordinary reads do not copy.

A fresh PCG32 consumes X, Y, heading for each organism in ascending ID order,
then X, Y for each food item in ascending ID order. The total is exactly
`3 * InitialOrganismCount + 2 * InitialFoodCount` outputs, with no placement retries.
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
regeneration consumes two per successful spawn. The engine is
single-threaded and not thread-safe. `State` exposes an immutable `SimulationState`
with `World` and `TickCount`; previous world and simulation snapshots remain valid.

Movement uses `distance = speed * timestep`, then cosine/sine of the heading
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
allows explicit values, including zero. Initial energies remain 100 and 20.

Each tick processes organisms in ascending stable ID order:

1. Subtract drain rate times configured timestep, even when stationary.
2. Remove organisms with energy at or below zero; they neither move nor feed.
3. Move surviving organisms from their previous positions and reflect boundaries.
4. At the resulting position, claim at most one currently available food item
   within the inclusive pickup radius: nearest distance, then smallest food ID
   when computed distances are exactly equal.
5. Add that food's energy without an energy cap and create a replacement organism.
6. Build surviving-organism and unconsumed-food collections, regenerate food, then publish one
   complete immutable snapshot with the incremented tick count.

An exhausted organism cannot be rescued by food that tick. Earlier organism IDs
get first opportunity at contested food; later organisms search only unclaimed
food. Movement never depends on positions updated earlier in the tick. Food stays
stationary until consumed. Unconsumed food objects are reused. IDs remain stable,
may have gaps, and are never renumbered. An extinct world continues ticking.
The internal test constructor accepts shrinking counts and strictly ascending,
unique, nonnegative IDs. Organisms cannot exceed the initial count; food cannot
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
without seeding or initial draws. The world snapshot, stream state, and next ID
are held in one private immutable advancement record. All construction finishes
before replacing that single reference. Failures after tentative draws or feeding
leave every committed component unchanged, and retries start from the same stream.
The application remains single-threaded; no locks or background scheduling exist.

Food regeneration supplies a renewable resource but does not guarantee a living
population: organisms still have no food-seeking behavior or reproduction.
Existing movement/reflection and binary64 distance limitations still apply.

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
Reproduction, genetic inheritance, mutation, steering and food-seeking, advanced
metabolism, evolution, persistence, and interactive configuration remain deferred.

Task 1.5 verified native SkiaSharp loading, CPU drawing with exact pixel checks,
and WPF `SKElement` paint callbacks before and after a programmatic resize.
The shell started with the correct window title and closed normally. Temporary
probes were removed. Visual appearance, placeholder visibility, and visual
resize behavior still require manual verification; GPU rendering was not tested.

Task 4.5 programmatic checks verified paint callbacks, organism/food/heading
pixels, timer controls, deterministic reset, resize repaint, error recovery, and
shutdown. Manual visual inspection of appearance and reflections remains pending.
Pure viewport math is source-linked into Tests without referencing WPF or Desktop.
