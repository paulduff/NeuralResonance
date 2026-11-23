# Explicit tendon-body mechanics — candidate

The completed full-path experiment identifies the engine's original joint
friction as a major suppressor of prescribed motion. Removing it changes the
largest response from 2.53e-6 to 0.4343 radians while preserving force accounting
and the existing angular-limit guard. The actual full-body/head bridge also
passed its separate original-setting integration check. Their reviewed evidence
now supports an explicit candidate choice for the isolated tendon-driven body.

The shared bridge protocol is `full-avatar-tendon-wire-v3`. Every configuration
must declare `mechanicsMode` as `original` or `no-joint-friction`. Missing modes,
old protocol versions and the other mechanical diagnostic interventions reject;
there is no inferred default. The first retains the recorded .05f engine value;
the second applies the already tested zero engine-friction intervention.

The bridge uses the original diagnostic setting owner, preserves its before/
intervention/owned settings in every reply, and verifies them at every command
boundary. The host checks each link's observed friction and the selected mode.
The complete mechanical evidence must remain unchanged throughout the session.
Gravity, body damping, contact friction, masses, inertia, angular constraints and
the original tendon forces remain governed by the existing native owner.
The live world/editor defaults are unchanged.

Zero extra engine joint friction is an explicit mechanical approximation,
not a physiological claim that real joints have no resistance. The body's
muscle/tendon geometry, passive resistance, active cells and sensory feedback
still need their own consistent full-body integration. Repeating the tibialis
parameter set over synthetic paths remains an anatomical limitation.

## Required next run

The visible workflow runs **341 exact component cases**: ten new selection/
admission controls and the previous 331 regressions. Repetition is justified by
the changed shared wire and native host admission. The new controls cover both
choices, missing/unreviewed choices, mismatched observed friction, changed
mechanical provenance and rejection of legacy protocol configurations.

After those pass, build a separate Unity player and run the six actual head
delivery conditions under each choice: **12 sessions, 96 physical steps,
108 endpoints, 324 head receipts and 108 extra inspections**. Each mode's quiet
trajectory must exactly reproduce its respective completed `9d34cc` quiet
capture. The exact scalar encoding and original initial physical clock
remain unchanged in behavior. Only the protocol declaration and retained
mechanical choice are new.

All 240 native receipts must retain the declared setting transformation.
The independently reconstructed head derivatives retain the original bounds
and all 2,592 scalar comparisons. Each mode's inspection pairs retain exact
physical trajectories and original head payloads. All 2,052 active-angle
observations also retain the existing 0.05f-radian limit-violation guard.
Preserve failures and original bounds; no movement or anatomical threshold is
invented. This run does not produce neuronal or muscle-cell activity.

`native_body_mechanics_study.py prepare` freezes clean pushed source, selected
original quiet captures, both reviewed prerequisites, attributed rig assets,
executables and required test inventory. It explicitly limits implementation
changes to the three native bridge/setting files, host/dispatcher/study and two
test files. All neuronal, learning, sensory and muscle equations remain unchanged.
`start-declared-body-mechanics-window.ps1` performs the actual builds, tests,
captures and saved analysis in one visible independent PowerShell window, then
verifies the D: backup. No live monitoring is permitted.

Authoring builds passed at
`D:/DNNE-build-work/declared-body-mechanics-authoring-20261123-v1`: host and tests
have zero warnings/errors; the Unity API compilation has zero errors and seven
existing reflection-field warnings. Python/PowerShell syntax and the static
341-case / 215-method inventory pass. No test/model was executed by authoring.
Actual execution remains required at this checkpoint.

## Visible launch

`D:/DNNE-build-work/declared-body-mechanics-20261123-203560` is frozen at pushed
`abec8aab441815fed37a800fd94f98739737f572`. Preparation/preflight passed and
both initial 2,255-file copies plus manifest verify against its same-name
`D:/DNNE-desktop-runs/UnityWorld` backup. Interactive task
`DNNE-Declared-Body-Mechanics-4626d18d0885` started the visible workflow.
No post-launch output, status or process was inspected. Await Paul's final
report; this is the only pending run. The preceding actual head and full-path
friction results are completed, reviewed and D:-verified.
