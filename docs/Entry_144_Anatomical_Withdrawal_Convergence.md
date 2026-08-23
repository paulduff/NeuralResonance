# Entry 144 - Anatomical Withdrawal Convergence

## Repaired-run observation

The first phasic-release repair was observed for 55.71 minutes with predators
suspended. The world completed 100,532 ticks at 30.074 Hz with no tick failure,
and the avatar travelled 2.802 metres across 3 of 13,462 cells.

The repair broke the previous frozen brace attractor:

- stable balance rose from about 19.8 percent to 90.926 percent;
- maximum uninterrupted hand contact fell from 24,752.26 seconds to 22.46
  seconds;
- maximum uninterrupted chest contact fell from 25,670.82 seconds to 23.85
  seconds; and
- tissue integrity remained at 100 percent.

Transport remained healthy. Of 57,521 physical and 574,272 somatic accepted
frames, one body-input request timed out. Error output was empty and the run had
no simulation failure.

The remaining fault was a high-frequency local contact loop. There were 55,003
collisions, or 16.45 per second, while aggregate withdrawal was active for
97.428 percent of the run. Several lower-limb and manipulator muscles reached
complete fatigue and zero force while the corresponding neuronal command lanes
remained strongly recruited.

## Cause

Each collider face and receptor sector had independent pressure-onset memory
and an independent spinal pulse clock. Contact moving among the palm, fingers,
forearm, chest edge, and adjacent wall faces could therefore present an
unchanged physical load as a sequence of new threats. Separate sensory spikes
also converged on the same spinal motor channel without a shared inhibitory
recovery state.

The result was many short pulses whose overlap reconstructed an almost tonic
withdrawal command even though no individual contact remained frozen.

## Implemented neuronal repair

1. Withdrawal pressure is integrated by anatomical body side and region rather
   than by raw collider face or receptor sector.
2. Contacts arriving within one sensory integration window share a peak
   pressure envelope. Lower-pressure faces cannot erase that peak and create a
   false onset on the next frame.
3. The ordinary ascending touch and pain populations retain their detailed
   receptor sectors. Only the protective spinal collateral converges.
4. Each anatomical withdrawal field has one phasic pulse gate. Unchanged
   inputs inside its recovery interval remain sensory evidence but do not gain
   repeated motor authority.
5. A meaningful increase in measured threat bypasses the gate and recruits a
   fresh withdrawal pulse.
6. Withdrawal channels in the spinal motor structure now build a local
   recurrent inhibitory trace after evoked firing. Convergent regions sharing
   one motor pool are therefore suppressed during recovery.
7. Stronger nociceptive volleys retain a graded escape path through that
   inhibition, preserving immediate protection from a worsening load.

No scripted release direction, symbolic movement policy, ML controller, or
host-authored escape action was added. Motor choice remains neuronal.

## Regression evidence

- Different collider faces on one anatomical hand share one withdrawal gate.
- Continuous local pain remains visible while the repeated spinal collateral
  is inhibited.
- Hand and forearm fields retain independent protective recruitment.
- A stronger load on another collider face bypasses the shared recovery gate.
- Recurrent inhibition suppresses an unchanged nociceptive volley more than a
  severe acute volley.
- Ordinary touch cannot acquire withdrawal authority and is not altered by the
  withdrawal inhibitory circuit.
- Focused neuronal sensorimotor suite: 122 passed, 0 failed.
- Full DNNE suite: 729 passed, 0 failed.
- Full Release solution build: 0 warnings, 0 errors.
- `git diff --check`: no whitespace errors.

## Next observation

Start from a clean body and neural state with predators suspended. Observe at
least 20 minutes and compare the generated world-run report with this entry.
Acceptance requires:

- aggregate withdrawal duty falls materially below 97.428 percent;
- collision frequency falls materially below 16.45 contacts per second;
- hand and chest contact episodes remain bounded near tens of seconds rather
  than accumulating through adjacent collider faces;
- command lanes release as their muscles fatigue instead of remaining near
  saturation at zero force;
- harmful pressure remains anatomically local and visible throughout contact;
- a real force increase still produces prompt local withdrawal; and
- terrain exploration resumes without any non-neuronal escape behaviour.

Tissue damage remains the next biological consequence to add after this motor
release loop passes observation. It should accumulate from measured trauma and
pressure dose, not serve as a substitute for neuronal recovery.
