# Entry 133: Long-Run Physics Feedback Corrections

## Observation run

The articulated avatar and neuronal stack ran for 38,366.5 seconds, or 10.66
hours. The authoritative world completed 1,157,354 ticks at an average of 30.17
Hz. The brain remained connected and all 119 services were healthy at graceful
shutdown.

The avatar travelled 475.51 metres, produced 7,804,407 motor dispatches, and
accepted 4,603,636 physical or somatic feedback frames. Only 235 body frames
were rejected, but those rejections exposed a persistent mechanical feedback
fault. Rejected somatic frames contained tangential speeds as high as 1,167.458
metres per second. Repeated contact also produced 55.2 collision observations
per metre and left the body fallen against a shelter wall.

Thirty-three physical deaths occurred without predators. The cause was normal
metabolic depletion during a motor-learning experiment, followed by body
respawn. This repeatedly reset tissue integrity and confounded the locomotion
observation. No food, water, or device interaction succeeded; all 1,076
interaction attempts were out of reach.

## Root cause

The collision engine correctly returned a constrained articulation when a limb
or body segment met a solid surface. The world accepted that constrained pose,
but the internal antagonistic-muscle plant retained the rejected proposed pose.
On the next tick, physics compared the visible accepted body with an internal
limb that had continued into the obstacle. Contact velocity was also calculated
from this rejected attempted sweep. Sustained pressure could therefore create
large artificial slip speeds and repeated contact impulses.

Terrain ascent telemetry recorded only the final instantaneous mode and
progress. A completed or aborted step disappeared as soon as the controller
returned to idle, so the run report could not establish whether the new ascent
mechanics had actually been exercised.

## Corrections

- Every collision-resolved joint, axial pose, balance pose, and manipulator
  extension is reconciled into the musculoskeletal plant before the next tick.
- Antagonistic muscle lengths are synchronized to the accepted joint angle, so
  proprioceptive velocity reports physical movement rather than rejected
  penetration.
- Contact velocity is calculated from the accepted collider pose and accepted
  root motion rather than the unconstrained attempted pose.
- Somatic and physical-body validators identify the exact invalid measurement,
  value, and supported range. Shoulder abduction, neck pose, and support-plane
  offset are now explicitly validated.
- The Blazor world's explicit motor-training mode suspends metabolic depletion
  while preserving collision, pain, tissue damage, body feedback, and neuronal
  motor authority. Standard world options retain normal metabolism.
- Terrain ascent now records encounters, starts, step and mantle starts,
  completions, aborts, rejections, and the last outcome. Cumulative values
  survive physical respawn and are written to the world run report.

## Authority boundary

No navigation policy, movement script, machine-learning controller, or host
decision was added. The brain still supplies every motor and manipulator drive.
The host supplies only body mechanics, collision constraints, experimental
metabolism configuration, and truthful sensory feedback from the motion that
the physical world accepted.

## Next observation

The next full run should keep predators suspended and confirm that:

1. body-frame rejection remains at zero under sustained wall contact;
2. contact tangential speed remains within the physical transducer range;
3. collision observations per metre fall substantially;
4. the avatar does not starve during motor training;
5. ascent counters prove whether quarter-metre steps begin and complete; and
6. manipulator output begins to produce reachable interactions rather than
   repeated out-of-reach attempts.
