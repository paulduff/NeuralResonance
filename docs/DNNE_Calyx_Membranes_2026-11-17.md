# Inner and outer vestibular nerve-ending membranes

The numerically reviewed hair cell needs a receiving nerve-ending mechanism.
`AvatarCalyxKernel` is the next isolated component: separate inner and outer
calyx membrane currents and channel kinetics. It has no free-voltage integrator,
cleft solver, axon or spike-generation shortcut. The existing Avatar transducer
and the qualified hair-cell implementation are preserved.

## Explicit source choice

This independently expressed reduction selects the **deposited model's calyx
current subset**, pinned at authors' revision
`47d499449df66c6676ee43cbb08ea1995469a817`, from
[Govindaraju et al.](https://doi.org/10.1073/pnas.2207466120).
`DNNE_Vestibular_Equation_Audit_2026-11-17.md` records differences from the printed
supplement. Source PDF/XML and licensing are retained with verified D: copies.

Both inspected deposited models use 3.7 nS total HCN2 and the stored negative
argument inside the first reciprocal exponential in its time constant. The
printed supplement uses a different sign and lists 3.6 nS total. This candidate
does not blend them or claim printed-paper reproduction. Its fingerprint names
`isolated-calyx-membranes-deposited-20221205-v1`. Tests explicitly distinguish the
chosen convention. Human physiology and the better biological fit remain open.

| Mechanism | Declared treatment |
|---|---|
| Inner / outer membrane area | 294.63 / 288.33 um² |
| Specific capacitance | 0.01 pF/um² on each face |
| Voltage | Inner: calyx minus cleft; outer: calyx relative to grounded perilymph |
| Kv7.4 | 24 nS whole-calyx conductance, 4/5 inner and 1/5 outer, separate gates |
| HCN2 | 3.7 nS whole-calyx, half per face; 4/5 K and 1/5 Na current per face |
| Inner reversals | 26 log(Kcleft/150), 26 log(Nacleft/12), in mV |
| Outer reversals | Literal stored −88.4311 / +63.8751 mV, not silently recomputed |
| Na/K pumps | Densities 1,000 / 2,000 per um²; two K in per three Na out |
| Inner KCC | Stored 2 pA/um² maximum and K-dependent occupancy; equal/opposite K/Cl electrical terms retained |
| Outer leak | Stored 2 nS, reversal 0 mV; ionic species not assigned |
| ERG and calyx sodium | Omitted from this subset because the inspected net-current expressions multiply/substitute them by zero |
| Temperature / supply | Unscaled source room-temperature kinetics; held intracellular reservoirs, no ATP model |

Positive current leaves the calyx interior. Inner-face current therefore enters
the cleft; outer-face current enters the surrounding perilymph. KCC's net current
is zero, but its potassium transport remains in the chemical ledger. Whole-cell
conductances do not acquire another area multiplier; transporter densities do.
Kv7.4's one-ms minimum time constant is part of the source's empirical fit,
not a numerical state clamp. Invalid/nonfinite inputs are rejected explicitly.

Gate initialization is voltage-conditioned, not a settled free-cell claim.
`RelaxVoltageClamp` supplies only the analytic response under an explicitly held
voltage. The future coupled model must integrate gates jointly with voltage and
cannot use an endpoint's voltage retroactively over an elapsed body interval.
An inner-cleft potential change does not change the outer membrane's reference.

## Prospective component check

The independent visible PowerShell pipeline freezes source and runtime metadata,
requires the D:-verified passing hair-cell review, preserves the original Avatar
and hair-cell source, checks exact test inventory and hashes built assemblies and
managed runtime. Initial/final D: backups are verified. Pending skull and rubral
muscle outputs are not read or polled.

- 23 exact unit cases: 22 calyx cases and the solution manifest.
- 15,984 native current/gate records: six declared variants, 37 voltages,
  three cleft potentials, three cleft potassium concentrations, two cleft sodium
  concentrations and four gate initializations.
- Variants retain baseline or remove Kv7.4, HCN2, pumps, KCC or outer leak.
- 224 voltage-clamp records: eight voltage pairs, four initializations and seven
  durations. One analytic transition and 0.125 ms subdivisions are retained.
- Independently written Python equations retain every expected current/rate and
  analytic gate response. Arithmetic limits are 2e-10 absolute + 2e-12 relative;
  clamp/subdivision absolute limit is 1e-12. No ODE reference is claimed.

The focused tests exercise ionic reversals, the two electrical references,
conductance versus density scaling, two-in/three-out pumps, electrically neutral
KCC, local ion changes, analytic relaxation, invalid-input rejection and source
versioning. Channel arithmetic is a prerequisite, not evidence for free-cell
voltage, a complete sensory neuron, local coupling or balance.

## Next implementation

After the component result is reviewed, implement a declared shared cleft and
membrane charge balance, carrying both electrical and chemical effects. The
source's spatial geometry cannot silently become a calibrated well-mixed gap.
Select and document the reduced geometry and its limits, then compare coupled
trajectories and refinements before connecting an actual afferent membrane.
Nodal sodium/cable discrepancies from the source audit remain to be resolved.

Authoring builds pass with zero warnings/errors; Python/PowerShell syntax and
the static 23-case inventory pass. Tests and model arithmetic have not executed
inline. No production host, editor or saved brain selects this candidate.

The prospective study is now launched as `calyx-membrane-20261117-920328`, frozen
at revision `635508a1d6d8dcaba156004d50d3e67e620b9acd`, in visible task
`DNNE-Calyx-Membrane-ed9d3e8d93fa`. Initial 1,812 files plus manifest are verified
on D:. No post-launch output/status/process has been inspected. Await the
operator's report; channel arithmetic and analytic clamps are not free-cell
or coupled sensory qualification.

The completed-result reviewer is now authored as `review-calyx-membrane.py`.
It requires an explicit operator completion report before reading a capture,
verifies full C:/D: and Git/binary/runtime/test provenance, and reconstructs
the original analysis from all saved native/independent current and clamp pairs.
It does not evaluate a new channel state or clamp. The new review must receive
its own verified D: backup before `prepare-cleft-coupling.py` can use it.
Neither reviewer nor dependent preparation has run while `920328` is pending.
