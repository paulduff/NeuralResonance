# Named tibialis-anterior populations at the measured body geometry

The reviewed native study `998900` established the force-delivery path for ten
named muscles. Actual muscle dynamics require more than an applied 40 N test
force: each muscle needs an explicit fiber/tendon geometry, unit population and
initial condition. This study prepares both tibialis-anterior muscles using
all **147 distinct slow-unit rows** from the preserved Nagamori source.

## Four declared initial conditions

The left and right path lengths and body timestamps are copied from the actual
native quiet case after its one passive prelude step. They are not interpolated
or recomputed from a future pose. The preserved source anatomy supplies the
matching muscle names, original fiber lengths, nominal tendon slack lengths
and pennation values. The prior frame mapping supplies each side's declared
paired-knee/ankle length scale.

Each side is evaluated with two geometries:

1. The unmodified Nagamori tibialis-anterior reference dimensions, as a control.
2. An explicitly proposed set of dimensions: OpenSim source fiber and nominal
   tendon lengths multiplied by that side's declared shin scale. Reference
   tendon length is nominal slack length times the original Nagamori ratio
   `28.875 / 27.5`; it is not silently equated with OpenSim's slack length.
   The source pennation-at-optimal value becomes the candidate's constant
   pennation assumption. Fiber mass changes in proportion to optimal fiber
   length, preserving the reference cross-sectional area/density relation.

Both choices retain the original Nagamori whole-muscle force coefficient,
passive-length scale and every slow-unit force/calcium/length-gain value.
There is no output-force multiplier. The candidate does **not** import the
OpenSim maximum-force value, activation floor, controllers or Millard muscle
dynamics. This is a declared combination of anatomical dimensions and the
existing physiological model, not an anatomical calibration result.

The source-dimension candidates are approximately:

| Side | Optimal fiber length | Reference tendon length | Fiber mass |
| --- | ---: | ---: | ---: |
| Left | 0.064936865 m | 0.240051603 m | 0.143243084 kg |
| Right | 0.065326697 m | 0.241492691 m | 0.144103008 kg |

These dimensions were declared before solving. They must not be adjusted after
the outcome just to obtain a passing balance. Foot scale remains a shin-ratio
proxy, constant pennation is an approximation, and the tendon-reference transfer
needs physiological calibration. Fast units remain excluded, retaining their
omitted capacity explicitly. This does not yet populate soleus or the other
muscles with a tibialis-anterior template.

## Execution and comparison

`fresh_motor_epoch_study.py prepare --population-initial` freezes all sources,
the complete population data, anatomical source, reviewed native receipts and
original numerical limits. Its independent visible PowerShell window runs
eight packaging controls and **379 existing managed regression cases** (359
from the named-path workflow plus 20 fresh-initialization controls). The eight
original initializer cases retain their independent Decimal comparison.

`MusclePopulationInitialStudy` then creates distinct motor contacts for all 147
units in each of four fresh diagnostic populations. The original
`AvatarMuscleInitialBalance` solver uses a prospective bracket of 0.4–1.4 optimal
fiber lengths, a 1e-13 m bracket tolerance, and the unchanged force tolerance
`1e-9 N + 1e-12 * referenceForceNewtons`. Every calcium, activation and release
state starts at zero; velocity starts at zero and slow-unit yield at one.
The recorded body timestamp is retained as the fresh muscle epoch. No evolved
tissue is reset and no original neuronal events are retimed or generated.

Four complete native initializer records, including all unit states and
definitions, are retained. An independent Decimal solver checks the same
continuous passive/tendon equations at 80 and 110 digits. Reference agreement
must remain within 1e-60 m, native fiber position within 1e-11 m, and independent
force residual within the original native force allowance. Original source rows
and every declared parameter must survive in the returned muscle definition.
Failed cases retain their errors and do not prevent recording the other cases.

The unchanged biological equations are used, not replaced. The current native
player is not run in this study. Passing establishes numerical initial balance
and population parameter ownership; active population dynamics, independent
moving-muscle accuracy, anatomical fit, actual neuronal recruitment and Avatar
standing remain further requirements.

The authoring build completed with zero warnings/errors, and Python/PowerShell
syntax plus all nine source-test inventories were checked. No new initializer,
test or reference was run inline. Original saved evidence and its D: copies
remain untouched.
