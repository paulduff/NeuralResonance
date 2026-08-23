# Entry 145 - Acceptable Instability, Axial Release, and Adductor Fatigue

## Observation

The post-convergence run showed that the earlier collider-face withdrawal storm
had been removed. The avatar could settle into a quieter chest and right-hand
wall brace, but three related problems remained:

- the balance response treated small recoverable postural error as if perfect
  stillness were required;
- painful chest or pelvis contact had ascending sensory evidence but no direct
  spinal route for releasing the axial command sustaining the brace; and
- sustained lower-limb recruitment could exhaust locomotor muscles, so the
  adductor fatigue path needed explicit regression coverage.

Human standing is controlled instability. A healthy body does not maintain a
mathematically perfect centre; it continually senses and corrects sway within a
recoverable support envelope. The engine must preserve that raw evidence for
neuronal learning without escalating every harmless deviation into an aversive
balance error.

## Implemented neuronal repair

1. Balance mechanics continue to calculate the raw centre of mass, extrapolated
   centre of mass, support margin, tilt, and proprioceptive signals.
2. Aversive balance error now has an acceptable recoverable envelope. Dynamic
   support remains acceptable while the measured margin is inside the existing
   dynamic stability allowance, and ordinary postural tilt has a 0.075-radian
   deadband.
3. Mechanical fall classification is unchanged. Loss of support, capture-point
   failure, crossed-leg instability, excessive tilt, and sustained instability
   still produce a real fall.
4. Chest and pelvis mechanonociceptors now carry a local contact-normal sector
   into the spinal withdrawal circuit.
5. Horizontal axial contact can recruit forward, reverse, turn, and trunk-yaw
   antagonists. A sufficiently strong spinal volley inhibits the previously
   selected action before recruiting the opposing axial drive.
6. Vertical chest or pelvis support remains localized pressure and pain
   evidence, but it cannot fall through to a generic directional spinal motor
   projection.
7. Sustained hip adduction recruits the existing antagonistic adductor muscle
   group. Fatigue accumulates under continued command and available force falls
   accordingly. A zero command remains a genuinely relaxed state that permits
   recovery.

No scripted locomotor choice, host-authored escape sequence, or learned
statistical controller was introduced. Directional protection is a spinal
neuronal reflex, while voluntary action selection and adaptation remain in the
existing neuronal network.

## Regression evidence

- Recoverable dynamic imbalance produces raw sensory state without aversive
  balance error.
- Passive or mechanically unrecoverable imbalance still produces substantial
  error and preserves fall behaviour.
- Direction-coded chest collision reaches the appropriate axial spinal motor
  populations.
- Strong axial nociception releases a previously selected forward brace and
  recruits reverse drive.
- Vertical pelvis support retains localized mechanonociceptive pain but emits
  no false directional spinal withdrawal.
- Sustained adduction raises adductor fatigue above 0.45 and reduces available
  force below half its fresh peak.
- Focused balance, contact, muscle, withdrawal, and motor suite: 155 passed,
  0 failed.
- Full DNNE suite: 736 passed, 0 failed.
- Full Release solution build: 0 warnings, 0 errors.

## Next observation

Run the complete neuronal stack with predators suspended and a clean body and
neural state. Observe whether the avatar tolerates small natural sway, releases
axial wall support when pressure becomes harmful, and changes stance as hip
adductors fatigue. Acceptance requires:

- no return of the collider-face withdrawal storm;
- no perfectly rigid upright attractor;
- no fall caused solely by harmless sway inside the support envelope;
- prompt falls when the support polygon or capture point is genuinely lost;
- bounded chest and hand bracing rather than indefinite wall support;
- declining adduction force under sustained wide or crossed-leg correction;
  and
- recovery of fatigued muscle groups when their neuronal command returns to
  zero.
