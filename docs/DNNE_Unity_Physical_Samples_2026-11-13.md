# Actual Unity stepping and body sample integration candidate

The producer's 39 focused checks passed against fixtures and a recorded body
frame. This next study connects that **unchanged** producer/outbox/receiver to
actual Unity physics. No live DNNE input or output path selects this adapter.

## Physical adapter and ownership

`PhysicalSampleQualification.cs` supplies an isolated Unity-side bridge.
`tools/UnityPhysicalSampleProbe` supplies its .NET body adapter and experiment.
The bridge is selected only by `--physical-sample-qualify`, mutually exclusive
with the other qualification modes. It requires a fresh, paused Avatar with its
existing 38 muscles and 15 physical links. It cannot connect to DNNE. No saved
brain, legacy world lease or diagnostic standing controller is used.

The .NET producer requests one actual fixed step at a time. Unity performs the
existing `WorldController` step, including Avatar muscles, physics and physiology,
then captures the body and every link's encoded contact observations on its main
thread. The reported counter is Unity's actual `physicsSteps`, and duration is
`(double)StepSeconds * 1000`, retaining its float representation. The test's
physical epoch is zero and neural epoch is 1000 ms: this is an explicitly
isolated mapping, not a live DNNE clock handshake.

`WorldController` now has a scoped physical-sample owner. While claimed, direct
`Step` calls, wrong-owner calls, diagnostic resets and GUI Play cannot compete
with the bridge. Ordinary operation retains its existing step implementation.
The study checks that competing calls are rejected before any physical movement.
The world remains operationally paused while the isolated owner requests manual
steps; wall waiting does not advance physics.

Requests and replies are fresh, atomically completed JSON files in the study's
private directory. They carry session, command sequence and expected physical
step. All are preserved. A missing, invalid or timed-out reply faults the adapter;
it never retries an uncertain step. This file bridge is an evidence-producing
diagnostic adapter, not the planned high-throughput live transport or a durable
cross-process recovery protocol. The program owns and stops only its dedicated
Unity child.

Unity's original JSON remains in each reply. The body adapter strictly decodes
the existing shared body request, rejects unknown fields and retains the typed
observation through the reviewed transport. Its canonical JSON need not be
byte-identical to Unity's serializer; original wire bytes remain separately
available. No legacy body descriptor, rectification or afferent event generator
is invoked. All 15 encoded contacts remain in the raw source replies, including
zero-load links. They are **not transported by the body-only v1 lane**. Extending
the contact lane and its completeness contract remains required for live use.
Existing sensor saturation/encoding also remains; these are not newly exposed
raw collision micro-events between physics endpoints.

## Prespecified experiment

1. Run the 16 producer, 22 transport and one solution-manifest tests on frozen
   source. Exact class counts, unique names and passing outcomes are required.
2. Build a dedicated Unity player from a separate writable copy of the frozen
   project and the already installed, attributed Avatar assets. Do not modify
   frozen source or download another model. Verify all compiled C# sources after
   build; retain generated Assets/ProjectSettings/Packages and player hashes.
3. Capture the unevolved step-zero body, then steps 1 through 200 using the
   producer with a four-sample capacity. Retain all 201 original body samples.
   Repeated calls against a full buffer must send no Unity step request. Drain
   using packet sizes 1, 3 and 4, with receipt/acceptance retries and repeated
   consumption checks. All original samples must arrive exactly once.
4. Introduce 250 ms wall waits at initial pause, full buffer and the explicit
   pause at step 80. Query Unity independently after each wait: both its actual
   counter and complete typed body observation must remain unchanged, ignoring
   only the inspection's wall timestamp. Original capture timestamps are never
   removed or restamped in transport.
5. Require finite physical state, more than 0.05 m of pelvis displacement and
   nonzero measured contact force in steps 0–200. These gates establish that
   actual physical motion/contact occurred; they do not assess standing or
   recovery. Preserve a failed gate without changing its bound.
6. After draining valid observations, request physical step 201 and deliberately
   fail its .NET capture callback. The original Unity reply remains recorded,
   but the producer must fault and its body completeness must remain at step
   200. A further advance must issue no request. Independently inspect Unity
   again to establish that it stays held at step 201, then stop the player.

The expected record therefore contains **202 original Unity grid observations**
(0–201), **201 delivered body samples** (0–200), 201 actual physics advances,
four held-body inspections at steps 0, 3, 80 and 201, and a final stop command.
It contains no artificial motor teaching or receptor spikes. The capture failure
is an explicit test intervention, not a physiological mechanism.

`SavedEvidence.Audit` reconstructs the complete request/reply sequence, validates
body/contact identities, reconstructs expected samples from the original Unity
replies and replays saved packets through a fresh in-memory receiver. It checks
the saved receipts, original typed data, completeness, held poses and physical
motion/contact gates without starting Unity. The probe's `--audit DIRECTORY`
entry point is solely for this offline inspection after the operator reports
completion. Saved analysis is separate from the initial physical experiment.

## Build, evidence and execution status

The .NET probe and test assembly build with zero warnings/errors. Preparation
Python and launcher PowerShell parse successfully. Unity compilation and all
runtime checks remain prospective; they run only in the independent PowerShell
window. No new world, model or tests ran during source preparation.

`tools/prepare-unity-physical-sample.py` freezes clean pushed source and hashes
the existing FBX/texture and installed Unity editor executable. The launcher
`tools/start-unity-physical-sample-window.ps1` rechecks the initial C:/D: copies,
runs the stages, retains source/player/probe evidence and verifies the final
D: capture. Unity's writable import/Library cache is outside the capture; the
frozen inputs, generated project assets/settings, compiled player and build log
are retained. Build-time Avatar inspection now uses the explicit build output,
so this study does not overwrite the older fixed-path inspection artifact.

All physical work is a headless child of the visible PowerShell study. No agent
live monitoring follows launch. The existing editor's live world and brain
services are not deployed, restarted or relabelled. The isolated player's status
identifies sample ownership; live editor clock/freshness/completeness diagnostics
remain part of later integration.

The finer Golgi retry remains pending. This accuracy/ownership study measures
no speed, so it may run alongside Golgi. Both operator completion reports and
reviews are required before CUDA host timing. Neither a successful build nor
successful physical sampling qualifies afferent physiology, live neuronal
authority, balance learning, movement or Avatar recovery.

## Launch record — 13 November 2026

The frozen package is `artifacts/unity-physical-sample-20261113-1ff02e`, revision
`b41dc3afc69ca1a7e0ca7afda48e8492392b561e`. Its initial D: copy is
`D:\DNNE-desktop-runs\UnityWorld\unity-physical-sample-20261113-1ff02e`:
1,506 files plus manifest verified before launch and rechecked by the launcher.
The independent visible PowerShell task is `DNNE-Unity-Samples-7a2ebffbef3c`.

No output is inspected after launch until the operator reports completion.
The native Unity build, 39 focused checks and physical experiment remain
unreviewed; no pass or live-process state is inferred from starting the task.
This and corrected Golgi `golgi-sdirk3-fine-20261113-ca813f` are the two pending
reports. CUDA timing remains held until both captures have been reviewed.

## Reported result and verified review

The operator reported completion on 13 November. All 39 focused checks passed,
and Unity 6000.3.19f1 built and completed the isolated physical study. The saved
evidence contains 201 actual advances, 202 original grid observations (0–201),
and exactly 201 delivered body samples (0–200). The deliberate capture failure
after the final movement left completeness at step 200 and stopped later steps.
Direct and wrong-owner advances rejected. All 225 backpressure checks, 76 receipt
retries, two pause checks and four held-body inspections passed. Maximum pelvis
displacement was 0.66786236 m; recorded contact force reached 3720.658935546875 N.
Those values establish physical motion/contact evidence, not controlled balance.

`tools/review-unity-and-contacts-20261113.py` verified all 2,827 files in each C:/D:
capture (317,760,833 bytes per copy), frozen source against all 1,501 Git archive
files, executed test/probe binaries, 144 player files and 27 built C# source files.
The 39 exact test identities matched their source declarations. The saved probe's
offline `--audit` entry reproduced the complete saved analysis exactly, without
starting Unity or running tests/models. The capture manifest SHA-256 is
`262AFEC358D322DA2488884A2DAD6F0BB2654D8ED33FEE8DAA17049E62588917`.

The review is `artifacts/unity-contact-review-20261113/unity-review.json`; the
three-file combined review has its own verified D: copy plus manifest. Raw
15-link contacts were retained, but this body-only lane did not transport them.
Combined delivery, a live neural-clock handshake, afferent physiology and Avatar
recovery remain separate outstanding work. This window is no longer pending.
