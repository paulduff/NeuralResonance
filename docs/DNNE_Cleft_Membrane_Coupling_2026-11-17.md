# Local hair/calyx membranes coupled to the open cleft

`AvatarVestibularCleftMembranes` is an authored instantaneous derivative owner.
It supplies actual local membrane currents to `AvatarCleftOpenCoupling` and
returns the corresponding voltage, ion and channel-gate derivatives together.
It does not advance a clock, initialize equilibrium, generate afferent events
or replace the live Avatar transducer. Its 12 prospective component cases compile;
no new test, model evaluation or independent comparison has executed.

## Source layout checked before coupling

The pinned deposited template and paper-role XML inventories both specify:

| Scope | Expression | Consequence |
|---|---|---|
| `var4`, hair above calyx | `J_mh=0`; KL, Ca and HCN1 conductance densities zero | Uncovered hair membrane contributes capacitance, without an active-current port. |
| `var5`, hair within calyx | `g_klh=g_klmaxh/ai`, `g_cah=.5/ai`, `g_hcn1h=4.2/ai` | Whole-cell channel conductances are distributed over the cleft-facing area. |
| parameter | `g_klmaxh=80` | The declared reference retains the existing 80 nS total KL conductance. |
| `var6` | `V_mh=V_h-V_out`; gate targets/times use that voltage | Each spatial patch uses its own hair-to-cleft voltage and gate state. |
| `bode2/dode1` | Gate source is equilibrium minus current state; coefficient is the local time constant | Gate derivatives are evaluated jointly with current voltage and ions. |

These are retained source expressions, not new measurements. Provenance and
model hashes are in `DNNE_Vestibular_Equation_Audit_2026-11-17.md`; inventories
are `vestibular-calyx-equations-20261117/xml-inspection/template.json` and
`paper_roles.json`. The deposited calyx convention and its differences from the
printed supplement remain as previously declared.

## One state, one derivative

Every compartment must provide its identified cleft/calyx electrical and K/Na
state, three actual hair gates, four actual calyx gates and an explicit axonal
current port. Missing/duplicate/unknown identities or invalid gate probabilities
fail. Gates are never silently set to their current equilibrium. No future
endpoint voltage is used to fill an elapsed interval.

For each patch, the evaluator:

1. Computes hair membrane voltage relative to that patch's cleft. The existing
   hair rate functions supply local equilibrium/time constants, and the new
   derivative is `(equilibrium-currentGate)/tau` for each gate.
2. Uses current local K/Na concentrations for hair reversal potentials and pump
   occupancy. KL, Ca, HCN1 and pump currents are apportioned by the patch's
   cleft-facing fraction. Pump 2-in/3-out stoichiometry is retained.
3. Calls the unchanged calyx kernel with the patch's actual calyx and cleft
   voltages relative to perilymph. Inner and outer currents are apportioned by
   their respective areas; their gates and rate functions remain separate.
4. Sends total electrical currents and explicit K/Na currents to the open spatial
   solve. Electrically neutral KCC still contributes its potassium transfer.

The whole-hair MET current is evaluated once from the supplied physical bundle
displacement and apical potential difference. Endolymph is the configured offset
from perilymph. It is not copied into every spatial patch. The uncovered hair
region remains capacitive; its outward membrane-current port is zero under this
declared reduction. Increasing that uncovered area therefore adds capacitance
without multiplying active channels.

The caller must supply geometry whose capacitances correspond to the declared
hair and calyx membrane areas, with matching opposing patch areas and correct
total inner/outer areas. Construction checks permit only a small rounding
allowance (`1e-10 + 1e-12*abs(expected)`, in the checked quantity's units).
This is not a geometry fit or an independent numerical acceptance limit. Channel
fractions use equivalent capacitance ratios under uniform specific capacitance,
avoiding unnecessary divide/multiply rounding.

## Explicit reductions and unresolved work

The coupled version is `local-hair-calyx-open-cleft-membranes-v1`. Its fingerprint
includes open topology/bath identity, original hair/calyx fingerprints, patch
fractions and the declared chemistry/location choices. Fixed intracellular K=150,
Na=12 and Ca=.001 mM and extracellular Ca=1.3 mM are retained. The calcium current
contributes electrical charge but calcium concentration is not dynamic here.
Outer calyx reversals retain the source's rounded literal constants; consequently
this owner rejects outer baths other than K=5/Na=140 mM and thermal voltage other
than 26 mV. It does not silently combine variable outer chemistry with those
fixed source expressions.

Axonal current is an explicit instantaneous terminal input. A supplied zero
declares a sealed terminal; it is not proof of an implemented or inactive afferent
cable. There is no axon/spike shortcut, quantal release, ATP support model or new
body-displacement conversion. Gate kinetics remain the selected room-temperature
source reduction. Physiological calibration remains open.

The new tests cover reduction to the reviewed uniform membrane currents,
subdivision at two/eight patches, local gate and ion causality, one MET port,
passive uncovered capacitance, gauge/order invariance, KCC/calcium accounting,
external axon-current charge balance and rejected identity/geometry/chemistry.
The test-project authoring build passes with zero warnings/errors. The 12 cases
are not included in the earlier 51-case open-boundary workflow, and no passing
result for that earlier workflow can qualify this new coupling.

After open-boundary review, add an independent comparison for the complete
coupled derivative and preserve the original fixed-bath membrane controls. Then
qualify stationary initialization and a joint voltage/ion/gate time integrator,
including complete state, positivity/failure handling, charge/species ledgers
and step refinement. Running separate fixed-bath hair and voltage-clamped calyx
steps in sequence is not the coupled evolution implemented by these derivatives.
