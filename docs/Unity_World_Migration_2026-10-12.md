# Unity physical-world migration — 12 October 2026

Unity 6000.3.19f1 and Windows build support are installed on the desktop. The
operator's Mike Alger FBX imported as a valid humanoid with seven skinned meshes.
The new `UnityWorld` project builds a Windows player with zero warnings/errors.
It contains a paused, flat-floor physical prototype and the existing neuronal
motor/receptor network boundary. It does not replace DNNE, Dyad or Entity and does
not include CIV.

The body has 15 dynamic articulation links and approximately 73.4 kg total mass.
It shares one native solver for gravity, joints and contacts. The appearance follows
physical bones; the physical body is not moved by animation or a navigation agent.
An explicit external-world lease pauses legacy WorldSim and prevents concurrent
Resume/Reset while Unity owns embodiment. Disconnect/expiry leaves the old world
paused. Initial lease drain is 10 seconds; renewal is every two seconds, with a
seven-second local acknowledgment cutoff. Stopped brain ticks also pause Unity.

The first isolated run, `unity-qualification-20261012-073737-307775`, completed:

- Gravity lowered the pelvis approximately 0.714 m.
- Peak measured contact was approximately 3669.7 N.
- Minimum collider height was approximately -1.64 mm.
- The anchored, gravity-disabled elbow fixture reached approximately 2.471 rad,
  within its configured limits.
- No sensory frames were sent to DNNE, and authority-gate checks passed.

Those initial criteria returned `qualified=true`, but the operator subsequently
observed a shaking preview. That qualification did not measure settling and must
not be presented as proof of stable full-body mechanics. The preview was stopped.
The revised body uses solver-integrated passive spring/damper drives instead of
explicit velocity-opposition torques, changes the timestep from 20 to 10 ms and
checks the final two seconds for linear speed below 0.15 m/s and joint speed below
0.75 rad/s. Revised builds v2 and v3 succeed without warnings/errors and both pass
the settling qualification. Final v3 run `unity-qualification-20261012-081457-b6baaa`
completed independently in PowerShell with exit 0:

- Pelvis gravity displacement: 0.63919 m; peak floor contact: 6099.45 N.
- Minimum collider height: -1.15 mm; elbow fixture response: 1.51103 rad.
- Maximum linear/joint speed in the final two seconds: 0.00316 m/s / 0.01211 rad/s.
- Finite state, authority gate, joint limits and isolated execution passed.
- Walking was not assessed. The operator confirmed a normal-looking fall.

Full fallen trunk roll exceeded the legacy receptor's principal-tilt range.
V3 sends principal gravity inclination for trunk pitch/roll, retains the full
orientation in balance/fall fields and measures COM/support in a gravity-aligned
frame. Actual saved fall/contact JSON passes the existing DNNE validators.

Eighteen .NET tests pass: editor host/axes, exclusive-world lease lifecycle,
canonical-versus-Unity spinal trace compatibility, solution membership and two
captured Unity sensory-frame compatibility tests.
The camera-axis regression previously hardcoded an obsolete zoom distance; it
now checks anatomical axis direction without tying the test to that camera distance.

The operator reported an Avast intervention on the utility. Readable local Avast
reports did not identify the exact executable or detection. The Unity player
launcher is unsigned, while its UnityPlayer.dll has a valid signature. That does
not establish what Avast detected or certify the application. The operator said
the alert disappeared; subsequent ordinary launches completed successfully.
Protection settings and exclusions have not been changed. The exact detection
and alert disposition remain unknown.

The updated editor was deployed with the legacy world paused and the existing
DNNE process left running. An HTTP handoff check acquired/renewed/released the
Unity lease and verified that legacy Resume returned 409 while ownership was
held. Neither the qualification nor this handoff test sent sensory fixtures to
the live brain. The tested preview session also remained disconnected.

Run logs and results have verified copies under
`D:\DNNE-desktop-runs\UnityWorld`. The final source/assets/player backup and hash
manifest are stored in `migration-20261012-stable` within that directory.

The [project instructions](../UnityWorld/README.md) document build/provisioning,
wire boundaries, independent PowerShell qualification and limitations. Native
floor/contact/joint qualification is required before claiming a stable body.
Ecology, physiological consequences, muscle/fatigue calibration, grip, complete
standing/stepping and editor integration remain subsequent migration work.

The subsequent [expanded actuator qualification](Unity_Actuator_Qualification_2026-10-12.md)
passed all 19 bidirectional axes and 40 independent native-boundary samples in
`unity-qualification-20261012-091859-6cb04a`. It also migrated canonical energy,
hydration and tissue consequences and corrected independent leg recruitment.
Standing, weight transfer, fatigue, grip and the wider ecology remain outstanding.
