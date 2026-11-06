# Isolated Golgi channel candidate

This increment implements voltage-clamped ionic mechanisms for a future Golgi
cell. **It is not a free-firing neuron, a connected inhibitory circuit, or a new
live structure.** No production host selects the candidate, so existing dynamics
and save fingerprints remain unchanged. Test outcomes are pending the operator's
report from the independent PowerShell window.

## Reference and provenance

Reference: Solinas, Forti, Cesana, Mapelli, De Schutter and D'Angelo (2007),
[Computational reconstruction of pacemaking and intrinsic electroresponsiveness
in cerebellar Golgi cells](https://doi.org/10.3389/neuro.03.002.2007).
The associated [ModelDB 112685 source](https://modeldb.science/112685) is pinned to
repository revision `2ffaeca558235d61222240fc74247cab1cd02fe7`. The test package
contains an unmodified reference snapshot with SHA-256 hashes. Its HOC programs
are not executed by this probe. The DNNE equations are expressed independently in
C# with attribution to the deposited mechanism authors.

The reference is a rat slice model at 23 C. Our 37 C sweep is a numerical/kinetic
check, not human physiological validation. The deposited template uses Na/K
reversals +87.39/-84.69 mV, a -55 mV leak reversal and initial -60 mV. Preserve this
voltage convention; do not mix it with junction-corrected measurements.

## Implemented mechanisms

`GolgiChannelKinetics` has 18 relaxation states for transient, persistent and
resurgent sodium; delayed, slow and A-type potassium; high/low-threshold calcium;
two HCN mechanisms; and BK. SK2 has six explicitly conserved Markov occupancies.
Conductance densities retain S/cm2 units; resulting currents are mA/cm2, positive
outward. Calcium is mM. Nernst reversal and the reference shell/pump derivative
are exposed but no free-running calcium balance is integrated in this increment.

Under a fixed voltage/calcium clamp, ordinary gates use exact exponential
relaxation. SK uses continuous-time Markov uniformization with a bounded Poisson
mean, positive transitions and no occupancy renormalization after advancement.
No periodic input, prescribed spike, activity-to-force shortcut or learning gain
is involved.

Explicit differences/choices relative to the deposit:

- Use the continuous removable limits of the rate expressions. The transient
  sodium source's `x/(1-exp(x/y))` fallback has the opposite sign at x=0; the
  candidate uses the correct limit -y. Resurgent singularities are also evaluated
  continuously instead of allowing 0/0.
- Bound the fitted HCN fast-component fractions to [0,1]. HCN1 exceeds one at
  sufficiently depolarized voltage; the rounded HCN2 branch boundary allows a
  small negative fraction. This changes those source edge cases explicitly.
- Evaluate analytic formulas without NMODL table interpolation or its voltage
  endpoint behaviour. The numerical input domain (-120..100 mV) exceeds the
  deposited table range (-100..30 mV); this does not extend physiological validity.
- Preserve the deposit's HCN time constants without applying its calculated but
  unused temperature factor. Other channels retain their individual Q10/reference
  temperatures. A consistent biological-temperature calibration is still needed.
- Use modern gas/Faraday constants in the Nernst and calcium-shell formulas.

## Verification package

All verification runs in a visible PowerShell window with saved logs/TRX/JSON
and a verified D: backup; Orion does not monitor it. A fresh package is required
for every invocation. Source, analyser and reference files are frozen and hashed
before launch and checked again afterward. The window builds its assemblies from
that frozen source and records/verifies their hashes around test execution.

The focused unit suite checks probability bounds, removable singularities, HCN
branch endpoints, current signs/reversals, calcium influx/removal signs,
temperature dependence, SK recruitment/recovery, equilibrium, time partition
invariance and rejection of invalid inputs without state mutation.

The capture matrix contains 36 fresh channel sets: two temperatures, six clamp
voltages and three calcium concentrations. Seven samples per case retain all
gate/occupancy states and every ionic current. The independent Python analyser
parses the pinned SK2 transition graph and constants, finds its initial stationary
distribution by a linear solve, then uses SciPy's matrix exponential to predict
each sample directly from t=0. This is independent of the candidate's detailed-
balance initializer and Poisson-series integrator. Maximum permitted absolute
occupancy and mass error is 2e-11.

This independent reference comparison covers **SK2**, not the other channels,
whole-cell voltage or anatomical physiology. Focused tests do not make the full
DNNE suite green; the separate cortical-consolidation failure remains open.

## Following work

After reviewing this capture, implement the coupled membrane and passive loading,
two calcium pools, energy accounting and actual spike detection. Compare against
the pinned reference with refinement and channel ablations. Then connect actual
mossy/granule spikes to Golgi cells and Golgi GABA contacts to granule cells,
preserving cell/contact identity. Only promote after those gates pass. At promotion
update structure registration, editor activity/anatomy, save fingerprints, Avatar
integration and tests together. No editor label is added for this isolated module.
