# Motor-unit population source — 23 November 2026

The next named-muscle connection needs explicit muscle capacity and distinct
neuronal/endplate ownership. The preserved single-unit reference is only the
smallest unit of a larger tibialis-anterior population. Repeating that row is
not a source-backed muscle population, and multiplying its output would bypass
the intended recruitment and unit-specific calcium dynamics.

## Preserved source data

`tools/motor_unit_population_source.py` decodes the already preserved
[Nagamori author parameters](https://github.com/anagamori/MU-Population-Model-Nagamori-2020/tree/c317be803f01965a1ca8fc4cbad1ba9156433af8).
The [associated paper](https://journals.plos.org/ploscompbiol/article?id=10.1371/journal.pcbi.1008707)
describes a 200-unit tibialis-anterior model, with different slow/fast mechanical
properties and calcium/activation parameters. The complete pinned source
archive, including its original recruitment/spike-generation code, is retained;
decoding it does not adopt that code as DNNE neuronal authority.

Both original source copies and their manifest were verified before capture.
`artifacts/motor-population-source-20261123` contains four files plus its verified
D: backup manifest: the original archive, decoded population, capture script,
and review. All 200 source row identities and every MAT field remain present.
The 14 parameters and one force coefficient per row retain all **3,000 original
binary64 values** across the JSON round trip. The archive SHA-256 is
`E89CFA1394BD0137E522E0C656D1CFCB981BC76B25C87021F8EEACACFABC8D6D`;
the MAT SHA-256 is
`01123AEC76D1ACC18B306AE1BD9DAABFD29FE9287B54212AEF5DB3F15F719EBD`.

| Source quantity | Original value, rounded here only |
| --- | ---: |
| Slow rows | 1–147 |
| Fast rows | 148–200 |
| Smallest unit force coefficient | 0.4620037750784163 N |
| Sum of slow-unit force coefficients | 266.1767346837521 N |
| Sum of fast-unit force coefficients | 395.5879711986010 N |
| Whole-muscle reference coefficient | 661.7647058823529 N |

Exact sums of the stored binary64 coefficients are recorded with Decimal
arithmetic. The all-unit sum exceeds the stored whole-muscle value by about
2.1111e-13 N. That source rounding difference is retained, not normalized away.
These numbers are parameter capacities, not observed contractions or evidence
that Avatar can support its weight. The partial slow population keeps the
original whole-muscle passive geometry and does not receive the omitted fast
units' force capacity.

## Integration boundary

The current `AvatarDynamicMuscle` already owns independent calcium/release states
for multiple identified slow units, sharing one fiber/tendon mechanical state.
`MotorAxonAnatomy` already requires one distinct motor axon per motor unit, and
`AvatarCausalMotorPlant` requires one owning muscle per unit. Those mechanisms
should be reused; no second recruitment decoder or muscle-output multiplier is
needed to populate them.

The source has **53 fast units whose force curves and sag are not implemented**
in that slow kernel. The author's sag implementation uses a discharge-rate
variable from its rate generator. Copying it directly would introduce a
different authority path. An event-driven fast-unit extension needs an explicit
physiological definition and its own numerical comparison before use.

The data helper `slow_parameter_rows` accepts explicit, unique author rows
1–147 and rejects fast/duplicate selections. Its `sourceRow` retains the
one-based author identity; `row` is the zero-based selected-array slot expected
by the existing comparison format. No contact, membrane, spike or live state is
created. The frozen capture script predates this slot/identity clarification;
the captured population itself is unchanged and keeps the author row numbering.
The helper is preparation code, not a qualified population simulation.

The 147 slow rows need a complete population comparison using the original
equations, mixed arrival times and shared moving mechanics. Motor-cell size and
recruitment diversity also remain explicit work: homogeneous neurons fed the
same contacts would not establish physiological recruitment simply because the
muscle parameters differ. Tibialis-anterior parameters must not be silently
applied to soleus or every other muscle. Muscle-specific capacity, geometry,
native coupled accuracy, sensory feedback and standing remain open.

This source review executed no model, test or benchmark and accessed no pending
Unity capture. Paul subsequently reported `named-muscle-native-20261123-998900`
complete; its separate reviewed result qualifies the prescribed native force
route. There are no pending windows at this checkpoint. The measured left/right
tibialis-anterior path lengths after the passive prelude are
0.2908102148570971 and 0.29255618136815514 m. Muscle dimensions must be explicitly
bound to those paths before connecting the active population.
