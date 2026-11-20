# Standing observation and the remaining neuronal connection

The existing Unity `--standing-trial` polls `/api/v1/neuronal-motor`, converts
population-level commands through the existing muscle plant and sends periodic
body/contact requests. It does not select the recently developed observed
afferent source, convergent nucleus owners, declared descending projection,
detailed motor-cell pool or motor-axon muscle/body bridge. Starting that trial
cannot establish that those newer pathways now control the full Avatar.

The physical balance diagnostic already measures pelvis tilt, support contacts,
centre of mass and its velocity, extrapolated centre of mass, support margin
and foot loads. `balanceError` is a derived physical diagnostic. It is not proof
that a biological sensory nerve fired or that DNNE commanded a correction.

The observation changes retain the actual baseline before the first physical
step and save every subsequent endpoint in `standing-trace.jsonl`. Previously
the file retained approximately one sample per 0.2 simulation seconds; now it
retains each 0.01-second physics endpoint. Progress updates and flushes remain
at approximately 0.2 seconds, with an immediate baseline flush and final close.
A hard process loss can therefore leave a buffered tail; such a run cannot be
treated as a completed trace. Signed pitch/roll, physical state, foot loads,
observed command and the command applied for that step remain in each record.
The baseline explicitly has no applied command. Simulation timestamps come
from the physical step difference, not a wall clock.

`StandingBalanceEvidence` records first negative support margin, distinct exits
from support, loss of both loaded feet, first physically classified fallen pose,
and maximum diagnostic error/tilt. An initially unsupported spawn remains an
initial condition rather than an invented loss-of-support transition; unloading
one foot during weight transfer is not loss of both feet. Duplicate, skipped,
reordered or invalid observations cannot count as complete evidence. No new
balance controller, torque, sensory stimulus or learning rule is added.

`standing-body-delivery.jsonl` saves each exact serialized outgoing body request,
its measured physics step and sequence, and the raw HTTP response with completion
step/status. This permits later reconciliation of which physical disturbance
actually reached the API. HTTP acceptance or generated-spike counters still do
not prove receiving-neuron delivery. Disk recording errors pause the trial.

The final report identifies `legacy-population-command`, requires complete
per-step and request/response accounting for its existing integration checks,
and explicitly leaves neuronal balance detection, corrective-muscle causality
and direct motor-axon control unassessed. Existing standing/recovery thresholds
are unchanged. World startup stays paused and the operator presses Play.

The .NET test project builds cleanly. The new twelve diagnostic controls and
25 prior standing/body-sensor controls are authored for the next visible window;
no tests were run inline. These controls alone cannot compile/qualify the Unity
glue or demonstrate the whole loop. A separate native build/observation is next,
followed by connection of the actual neuronal pathway and matched causal controls.
Keep observation, actual source firing, directional correction and sustained
standing/recovery as separately reported outcomes.

