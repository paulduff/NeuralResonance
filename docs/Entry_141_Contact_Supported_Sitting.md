# Entry 141: Contact-Supported Sitting

## Observation

The long Entry 140 observation remained transport-stable but exposed a
mechanical contradiction. The avatar spent 3,567.07 of 6,140.33 observed
seconds in the `falling` phase without ever entering `fallen`. It travelled
16.99 metres across 17 terrain cells while spinal withdrawal remained active
for 5,769.36 seconds.

The accepted final frame had one loaded sole, a 334 N left-hand contact, a
centre of mass 0.583 m beyond the measured support boundary, and exhausted
bilateral gluteus medius muscles. Collision reconciliation could arrest the
geometric fall while the balance system continued reporting falling. This
confirms that collision support, muscle capacity, and balance-state
reconciliation require a shared physical invariant.

The same audit found that selecting the neuronal `sit` channel immediately
assigned 78% of body weight to the pelvis. No physical seat or ground contact
was required. The posture command therefore created a fictitious chair.

## Biological boundary

The pelvis is the principal load-transfer structure for sitting and lies near
the whole-body centre of mass during quiet stance. It is not a fixed centre of
balance. DNNE continues to calculate the moving whole-body centre of mass from
the masses and positions of all articulated segments.

`Sit` is a descending neuronal motor intention. It may flex the hips and knees,
but it cannot create support. The body becomes seated only when an upward
physical contact beneath the pelvis or proximal thighs arrests descent.
Without that contact the same joint configuration is an unsupported crouched
descent and must remain subject to fatigue, instability, gravity, and falling.

## Implemented repair

- Unsupported `sit` recruitment still operates the hip and knee muscle plant,
  but the accepted posture is reported as `crouching`, not `sitting`.
- Unsupported seated descent cannot generate planar propulsion.
- Pelvic body weight is not synthesized from the selected posture channel.
- Upward contact beneath the pelvis, left thigh, or right thigh is required
  before the body can report `sitting` or transfer load to the pelvis.
- A 180 ms physical contact-memory window prevents a real seat from flickering
  when the collision solver reports alternating sweep frames.
- The support requirement is a skeletal and environmental invariant. It adds
  no goal selection, movement policy, ML controller, or scripted behaviour.

## Pressure and fatigue

Contact is not harmless merely because it prevents penetration. Sustained
concentrated pressure must accumulate local mechanonociceptive activity. Broad
pelvic, thigh, chest, or back support remains tolerable at ordinary pressure,
while high pressure, small contact area, prolonged loading, or continuing
muscular effort increases local distress. Ordinary upward plantar support does
not become painful merely because standing continues, but excessive force,
concentrated load, penetration, or non-plantar foot contact can still recruit
foot nociceptors.

Sustained hip abduction or adduction must likewise fatigue the recruited
agonist. Exhaustion removes its available force, emits anatomically sided
group III/IV muscle distress, and permits passive or antagonistic motion back
toward neutral. Recovery is strongest in the true relaxed state. Moderate
isometric activation now accumulates fatigue instead of being cancelled by
simultaneous full-rate recovery, and increasing coronal hip excursion recruits
additional stabilizing tone in both antagonistic groups. Gluteal, adductor, and
iliopsoas distress is localized to the hip rather than the generic thigh.

## Regression requirements

- A `sit` command without pelvic or proximal-thigh support never reports
  `sitting` and creates no pelvic ground load.
- Unsupported seated descent produces no planar locomotion.
- An upward pelvic contact permits sitting and transfers measured load to the
  pelvis.
- A wall contact with a horizontal normal cannot masquerade as a seat.
- Removing seated support returns the body to unsupported descent after the
  bounded contact-memory interval.
- Whole-body balance continues to use the segment-derived centre of mass.
- Sustained wide stance fatigues its coronal hip stabilizers and reduces their
  available force.
- Relaxed muscle remains a zero-force state and recovers rather than fatiguing.
- Ordinary long-duration sole support does not create duration-only pain.
- Sustained non-foot pressure creates spatially local mechanonociceptive input.

## Next physical rung

Collision constraints must become capacity-aware. A hand or limb may brace the
body only while its muscles can oppose the resulting joint torque. When that
capacity is exhausted, the joint must yield without allowing surface
penetration, and gravity must continue the fall. A later observation should be
bounded to ten minutes and must demonstrate either physically sustained
support or progression to a completed fall rather than an indefinite
`falling` state.
