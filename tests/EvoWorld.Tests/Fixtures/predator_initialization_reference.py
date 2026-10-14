"""Independent Task 12a initialization reference; Python standard library only.

From repository root:
  python tests/EvoWorld.Tests/Fixtures/predator_initialization_reference.py > tests/EvoWorld.Tests/Fixtures/PredatorInitializationGolden.cs

Imports no EvoWorld code and does not import previous task reference scripts.
PCG-XSH-RR setseq: seed=42, stream=1 (increment=3), modulo 2**64 state.
H=1, P=1, F=1; world=100x80; herbivore energy=100, predator energy=125.
Placement: H then P (x,y,heading), then F (x,y).
Task 8: speed=10 +/-10, metabolism=1 +/-.5, inclusive bounds 0..200/0..5.
Task 9: sensing=30 +/-10, steering=2 +/-.5, bounds 0..100/0..5.
Neural: 74 parameters per organism, signed unit*.5, bounds -1..1.
All groups active; 8 placement + 4 Task 8 + 4 Task 9 + 148 neural = 164 draws.
Binary64 scalar calculations, no trigonometric evaluation except the literal pi.
The fixture checks all coordinates, traits, 148 parameters, and final state exactly.
"""

import math

state = 0
mask = (1 << 64) - 1
draws = 0


def next_uint():
    global state, draws
    previous = state
    state = (previous * 6364136223846793005 + 3) & mask
    shifted = (((previous >> 18) ^ previous) >> 27) & 0xFFFFFFFF
    rotation = previous >> 59
    draws += 1
    return ((shifted >> rotation) | (shifted << ((-rotation) & 31))) & 0xFFFFFFFF


next_uint()
state = (state + 42) & mask
next_uint()
draws = 0


def scaled(upper):
    value = (next_uint() / 4294967296.0) * upper
    return value if value < upper else math.nextafter(upper, 0)


def variation(center, amplitude, lower, upper):
    signed = 2.0 * (next_uint() / 4294967296.0) - 1.0
    return min(max(center + signed * amplitude, lower), upper)


organisms = [[scaled(100), scaled(80), scaled(2 * math.pi), energy]
             for energy in (100.0, 125.0)]
food = [scaled(100), scaled(80), 20.0]
for organism in organisms:
    organism.extend([variation(10, 10, 0, 200), variation(1, .5, 0, 5)])
for organism in organisms:
    organism.extend([variation(30, 10, 0, 100), variation(2, .5, 0, 5)])
brains = []
for organism in organisms:
    brains.append([min(max((2.0 * (next_uint() / 4294967296.0) - 1.0) * .5, -1), 1)
                   for _ in range(74)])
assert draws == 164


def row(values):
    return "[" + ", ".join(repr(value) for value in values) + "]"


print("// Generated only by predator_initialization_reference.py; do not hand-edit.")
print("namespace EvoWorld.Tests.Fixtures;")
print("internal static class PredatorInitializationGolden\n{")
print("    internal static readonly double[][] Organisms = [" + ", ".join(map(row, organisms)) + "];")
print("    internal static readonly double[] Food = " + row(food) + ";")
print("    internal static readonly double[][] Brains = [" + ", ".join(map(row, brains)) + "];")
print(f"    internal const ulong FinalState = {state}UL;")
print("    internal const int Draws = 164;\n}")
