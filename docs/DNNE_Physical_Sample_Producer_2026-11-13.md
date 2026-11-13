# Physical stepping and sample ownership candidate

The reviewed physical-sample transport preserves measurements once captured,
but its caller must check capacity before advancing physics. The current Unity
`WorldController.Step` does not use that transport: its independent `BodyLoop`
captures on wall time and awaits HTTP. Adding reliable delivery alone therefore
does not establish a complete fixed-grid source.

`src/NRE.SimAvatar/AvatarPhysicalSampleProducer.cs` adds the isolated owner of
stepping and capture. It is not selected by any live World, Unity, Maze or DNNE
caller. The existing Avatar mechanics, muscles, vestibular/plantar measurements
and old saves are preserved. No balance controller, receptor model or spike
generator is added by this change.

## Producer behavior

An `IAvatarPhysicalSampleBody` implementation exposes its actual completed step
counter and fixed physical duration, synchronous single-step advancement, and
the existing `PhysicalBodyFrameRequest` observation. The producer must be its
exclusive owner. An adapter must report the physics engine's actual duration,
including Unity's float representation, rather than substituting a rounded
decimal clock. Body accessors and capture must not mutate physics.

The producer starts paused. It captures the unevolved initial state before
resume, then uses the reviewed outbox without changing sample identity or
physical-to-neural epoch mapping. For each advance it checks the body's counter
and duration, the next mapped timestamp, and capacity for the largest permitted
observation **before** calling physics. A full buffer returns backpressure with
no body movement or capture. Publishing does not release retained capacity;
only a matching receipt does. A receiver acceptance receipt still does not prove
receptor consumption or durable storage.

After the body advances, the producer requires exactly one completed step,
captures its observation immediately, and checks the counter/duration again
before committing the sample. It never substitutes a later observation for a
missed endpoint. A physics exception, changed counter/duration, invalid payload
or capture failure faults the epoch and prevents further advancement. It cannot
roll back partial physics. Earlier retained samples can still be published and
acknowledged after a producer fault, without declaring completeness for the
missing endpoint. An internal outbox failure retains that component's stronger
fault behavior.

Pause/resume changes operational permission, not the physical or neural epoch.
Publishing, retries and receipt handling never recapture the body. Mutation
operations and state snapshots belong to the original owner thread; callbacks
cannot reenter the producer. This is a synchronous ownership boundary, not a
cross-thread queue, network endpoint, crash journal or checkpoint. Changes to
the body outside that ownership contract cannot be made safe by a counter check
alone; integration must remove competing steppers and mutable capture owners.

## Checks and evidence boundaries

`PhysicalSampleProducerTests` adds sixteen cases covering initial capture and
paused startup, sample/byte backpressure, exact receipts, delayed wall time,
packet sizes and retries, signed measurements, partial physics failures,
post-movement capture failure, missing/doubled steps, external movement, changed
duration, capture-time mutation, reentry, another thread, clock exhaustion and
nonfinite observations. One case passes an actual previously captured Unity
fallen-body frame through the producer and preserves all 38 muscles, pressure,
balance and substrate observations. The other bodies are explicit counter and
observation fixtures, not physical simulations.

The new producer and complete test assembly build with zero warnings/errors.
Build success does not establish that these checks pass. The prospective
PowerShell run requires exactly 16 producer, 22 existing transport and one
solution-manifest check, all passing with unique names. It preserves frozen
source, executed binary hashes, TRX, logs and final C:/D: captures. Its preparation
and launch scripts are `tools/prepare-physical-sample-producer.py` and
`tools/start-physical-sample-producer-window.ps1`. Both parse successfully; all
runtime tests remain in an independent visible PowerShell window, with no live
inspection and review only after the operator's report.

The current tests exercise the actual producer/outbox/receiver implementations,
but not Unity's `Physics.Simulate`, contacts between samples, real-time throughput
or afferent physiology. They are a gate for an actual physical adapter, not proof
that the body-to-brain loop is fixed. A future adapter needs an explicit neural
epoch handshake and causal scheduling, original per-step body/contact capture,
bounded network backpressure and restart/session policy. It must not feed these
samples into legacy frame-generated pseudo-spikes and claim neuronal repair.
The separately documented spindle mechanics and afferent membrane work remains
required before live neuronal integration.

Since no live producer or editor-facing status changed, no editor label is
promoted. When the physical adapter becomes selectable, editor diagnostics must
distinguish captured, accepted and consumed physical steps, mapped neural time,
backpressure, faulted epochs and actual afferent events. Worlds remain paused
at startup.

## Execution order

The producer checks measure no performance and may run alongside the pending
Golgi accuracy retry. If launched, both operator reports and saved-result reviews
must precede CUDA host timing; that benchmark requires no competing studies or
builds. Do not inspect either window's live outputs. No test result or launch
is inferred from the successful build.
