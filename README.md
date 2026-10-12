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
- SkiaSharp.Views.WPF 4.152.2 (dependency only; rendering is not implemented)
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

The shell displays **EvoWorld — Simulation Not Started**.

## Architecture Decisions

These decisions govern future simulation work; they are not implemented yet.

- Core will own world state, entities, rules, simulation time, RNG, energy, and
  collision calculations. Desktop will own controls, rendering, viewport
  transformations, and playback scheduling. Rendering will never modify state
  or consume simulation RNG values. Initial execution may use the WPF UI thread.
- For a given implementation and supported execution environment, identical
  initial conditions, configuration, seed, and tick count must reproduce results.
  This is not a guarantee of bitwise equality across all hardware or runtimes.
- Random generation will use **PCG-XSH-RR 64/32, setseq (PCG32)** rather than
  `System.Random`. The simulation engine will own the RNG. The `ulong` seed is
  `initstate`, stored in `SimulationSettings`; the fixed stream is `initseq = 1`.
  Zero is a valid seed and receives the standard initialization sequence.
- PCG32 uses 64-bit state, 32-bit output, multiplier `6364136223846793005`,
  increment `(initseq << 1) | 1`, unchecked 64-bit arithmetic, and the XSH-RR
  permutation of the previous state. Initialization sets state to zero, sets the
  odd increment, advances once, adds `initstate` unchecked, then advances again.
  Complete RNG state capture and restoration will eventually support persistence.
  See the [PCG reference specification](https://www.pcg-random.org/download.html).
- Task 2 will begin with independently verified golden-sequence tests, including
  initialization and seed zero. Organisms and rendering will not own RNGs, and
  seeds will not be derived from wall-clock time.
- Simulation will use a configurable fixed timestep, initially `1/30` simulated
  second, independent of rendering. Stable entity and event ordering, explicit
  resource tie-breaking, and no outcome-affecting unordered or parallel processing
  will support reproducibility. Core will not depend on wall-clock time.
- Playback speed will control steps per real second, never the physical timestep.
  Pause will stop advancement; resume will discard paused elapsed time. Reset with
  identical settings and seed will reproduce the initial state and subsequent
  sequence. A future `SimulationRunner` will manage accumulation and catch-up limits.

## Development Status

Task 1 establishes the solution structure, WPF application shell, dependency
configuration, and testing infrastructure. Simulation behavior, organisms, food,
PCG32, and rendering will be implemented in subsequent milestones.

Task 1.5 verified native SkiaSharp loading, CPU drawing with exact pixel checks,
and WPF `SKElement` paint callbacks before and after a programmatic resize.
The shell started with the correct window title and closed normally. Temporary
probes were removed. Visual appearance, placeholder visibility, and visual
resize behavior still require manual verification; GPU rendering was not tested.
