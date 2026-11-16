# Observed neuronal source to moving native muscles

The next isolated experiment connects two running reference motor neurons to
Avatar's existing dynamic muscle/calcium/causal-body implementation and Unity's
checked native point-force hinge. The reviewed source adapter and native
refinement results are prerequisites. Prescribed motor events are removed from
this experiment. Each muscle receives only its connected neuron's observed
fractional crossings, delivered through the existing replay buffer and receiver.

## Explicit initialization and clocks

Each neuron starts fresh and integrates the existing quiet preperiod from 0 to
1,000 neural ms on its own 0.1 ms recording grid, with the unchanged 0.003125 ms
maximum RK4 step and endpoint-difference scheduling. All prehistory states are
saved. At 1,000 ms the rig requires zero crossings and zero emissions, then
attaches a fresh paused body with physical epoch 0 and neural epoch 1,000 ms.
No neuron is reset, no timestamp is rewritten and no prehistory is discarded.
There was no evolving body before this declared attachment.

The lane now accepts this explicit quiet attachment as well as its original
zero-time constructor. It requires the source's exact current time as the neural
epoch, refuses an active prehistory, and claims the only source advancement
delegate. A second lane or direct advancement outside the bound lane is rejected.
Two additional ownership tests cover state-preserving attachment, exact epoch,
failed re-anchoring and duplicate ownership. Original cell/channel equations,
numerical integration and Avatar/Unity dynamics remain unchanged.

The body executes 2,048 steps at the checked finer physical resolution with two
muscle substeps per physical step. The actual promoted binary32 clock is retained;
the body horizon is 128.00000607967377 ms. The source continues through sample
11,281 (1,128.1 ms), the first source sample covering that endpoint. Every source
row, including prehistory and that final coverage sample, must match the original
reference CSV prefix exactly. The original train has two crossings in this
interval, at neural times 1,018.30492185131 and 1,042.99427541307 ms. Their declared
2.0625 ms conduction delay is added after the explicit epoch mapping.

This is an open-loop source experiment: fixed diagnostic current drives the
reference cells; body sensations do not feed back into them. Packet lookahead
is therefore allowed for this specific source. It is not an implementation of
the timing needed by a closed sensory/neuronal loop.

## Prospective matrix and gates

Seven conditions use fresh neurons and a fresh paused native body: quiet, left,
right and balanced drive with 10 ms packets; left drive with 1 ms packets;
left drive with 50 ms packets; and left drive with 10 ms packets and deliberate
retries every seventh packet. Total native coverage is 14,336 physical steps.
There is no posture target, rate-to-spike generator, gait or hidden force source.

Before every publication group, an attempted body step must wait for source
completeness. After delivering only one source, it must still wait. Retained
output prevents advancing the affected neuron. Retries reuse the exact original
batch, preserve neuronal and muscle state, and must not cause another native
step. Body pause and uncertain native-command failure retain their existing
semantics. Every native command/reply, geometry/effort receipt, source state,
packet and arrival is preserved. The native exact-scalar preflight and deliberate
completed-reply lock are retained too.

The combined visible run has 160 focused tests: the checked 93 geometry/motor/
clock/read tests plus eight source-owner, 32 transport, 16 replay and 11 motor-cell
tests. Exact method coverage is required. The newly built source adapter must
then repeat the entire five-condition six-second source gate before any neuron
is connected to native physics. This is necessary because explicit quiet
attachment changes source ownership even though its numerical core is unchanged.

The independent saved-data audit reconstructs expected event IDs using the stable
identity algorithm, original crossing times, source/cell/projection/synapse
identity, epoch and delay. It verifies every source-prefix row, packet boundary,
receipt, wait/retry count and actual physical delivery. It also verifies native
command sequencing, geometry, state hashes, axial forces and individual impulse
checks with their existing bounds. The three alternate left packet/retry cases
must have bitwise-equal observed state, force, path, release and arrival-count
arrays to the standard left condition.

Twelve new independent adaptive references cover the four distinct physical
inputs with the existing DOP853/tight-DOP853/Radau settings. The reference
recomputes nonlinear muscle lengths and moments at each adaptive trial state,
using the actual neuronal arrival times; it does not interpolate native forcing.
Every one of the 2,049 native endpoints is compared, including the finer half
steps. Existing reference/candidate error bounds and calcium, fiber momentum,
fiber energy and reference tendon/joint accounting bounds remain unchanged.
Numerical dependencies and reference/core source identity must match their
qualified versions before and after the study. Partial failures remain saved.

## Execution and scope

`tools/prepare-native-motor-bridge.py --neuronal` freezes clean pushed code,
verified completed reviews and source/reference inputs. Its visible launcher
builds an isolated native player, runs the gates, executes the seven physical
conditions, solves the 12 references, audits the evidence and verifies D: backup.
The driver is `ObservedMotorBodyStudy.cs`; the independent analyzer is
`tools/analyse-observed-native-body.py`. Do not run tests or solves inline or
monitor the launched window. Inspect its saved results only after the operator
reports completion.

Authoring builds of the driver and test assembly pass with zero warnings and
zero errors. Python/PowerShell syntax and the declared 160-method-case coverage
parse successfully. The independent ID reconstruction reproduces all 28 GUIDs
from the completed source capture. These are build/static/saved-data checks;
no new test, neuronal trace, native simulation or reference solve has run during
authoring. Consult the subsequent launch record for execution status.

The source advancement method body is text-identical to the reviewed adapter;
the new wrapper changes ownership only. These static findings and both build
logs are preserved in `artifacts/observed-native-preparation-20261116`, with a
verified three-file-plus-manifest D: backup. They do not qualify the new attachment
or coupled trial; the visible window must execute those checks.

Success would demonstrate this reference neuron's causal connection to a
numerically checked moving native muscle pair. Physical refinement for this
specific neuronal input, long-horizon behavior, anatomical calibration, active
axonal/NMJ physiology, sensory return, live DNNE recruitment, balance and fall
recovery remain separate requirements. The reference pairing is still cat MG
neurons with a human TA slow-unit parameter row and a synthetic hinge. No live
DNNE, Avatar, World, editor or old save is selected or changed.

## Launch record

`observed-native-body-20261116-bb10dc`, frozen revision
`f9322f1c222186084b156bd34306e58582761cd8`, is launched in independent visible
PowerShell task `DNNE-Observed-Native-Body-b834d2a0e6db`. All 1,666 initial files
plus manifest are D: verified and rechecked by the launcher. Preparation verified
the reviewed source/native captures, unchanged cell/muscle/native/reference
cores, exact numerical dependency environment, Unity metadata and 160-case
method coverage. This is the only pending operator report. No post-launch test,
source, native or reference output has been inspected; do not monitor.
