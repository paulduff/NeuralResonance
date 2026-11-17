# Vestibular sensory equations and source versions

The retained calyx template is not yet a qualified numerical reference for an
Avatar sensory neuron. Visual inspection of the supplement and static inspection
of both the updated template and an original paper model found material equation
differences. Preserve each source separately; do not blend formulas and describe
the result as reproduction of the paper. No model, test, COMSOL session or live
sensory replacement was executed for this review.

## Source and reproducible inspection

The [authors' repository](https://github.com/acgsci/vestibularHairCellCalyxNQT/tree/47d499449df66c6676ee43cbb08ea1995469a817)
is pinned at `47d499449df66c6676ee43cbb08ea1995469a817`. Its README distinguishes
the Draft 12 files used for the paper from an updated template. Original files
and the repository's GPL-3.0 license remain in the ignored source artifacts.
No downloaded source code was executed or adopted in a runtime implementation.

| Source | SHA-256 |
|---|---|
| `2022-12-05 VHCC Model Template.mph` | `9A3610A622D0BC032827812793524A6A338EA61BB912035FD84E58F1F80C8CEA` |
| `2022-12-11 Supporting Information.pdf` | `C9145508AD7E705DEEA6F3749809438D112DA2D4889F1051F2380BCBB716EFA5` |
| `Draft 12 - Roles.mph` | `93A8DAB0954709BA02893B4D395A9D10750EFC5A9B06EBAC8AD19EBE286F6FA6` |

The original template/PDF capture is `vestibular-calyx-source-20261116`.
The new `vestibular-calyx-equations-20261117` artifact contains all 32 rendered
pages, the additionally downloaded paper model with verified Git blob/length,
read-only XML, source metadata and static inventories. PDF pages visually
reviewed were 2, 8–22 and 26. Other rendered pages are not claimed as visually
reviewed. Original PDF/model archives were not altered.

`tools/inspect-vestibular-calyx-source.py` verifies both pinned model hashes,
Git blobs and extracted XML against their archive entries. It preserves ten XML
sections, including scoped variables, selections, physics, functions, geometry,
meshes, studies and solvers. There are 279 parameter/variable definitions in each
model and 15 lexical differences between these scoped definitions. That count
excludes other structural differences; it is not an algebraic equivalence or
complete model-difference claim. Function selection defaults are not inferred.
Output must go to a new directory; source expressions are never evaluated.

## Mechanisms and limits

The model couples an equipotential type-I hair cell to a spatial cleft, calyx and
short afferent cable. Bundle displacement affects MET conductance; membrane
channels, extracellular ion transport and capacitance transmit signals. It
includes a hemi-node and two nodes, not the entire ganglion/central axon. Table
S7 combines measurements from different rodent organs and ages, not a human
vestibular calibration.

- `ge/ge2` (MET adaptation) is disabled. Its stored equations do not establish
  functioning adaptation.
- The inspected stationary/transient stages disable `var10` (quantal current);
  `var11` sets both quantal-current components to zero. This is a nonquantal
  reference, not a qualified vesicle-release synapse.
- ERG gate expressions exist, but the inner/outer current sums substitute zero
  for their contributions. Calyx sodium current also has a zero multiplier.
  A gate's presence alone does not demonstrate a functional channel.
- `q10=1/1` retains room-temperature kinetics, consistent with the supplement's
  23–27 °C source data. Its commented temperature option is not validation.
- KCC's potassium/chloride contributions cancel electrically. Na/K pump terms
  retain 2-in/3-out stoichiometry. Neither supplies a calibrated Avatar energy
  interface.
- Published initial voltages/concentrations precede a stationary solve. They
  are not evidence of a settled freely firing initial state.

## Differences requiring an explicit decision

These observations prevent an unspecified hybrid from being called the unchanged
reference; they do not establish that the paper's biological conclusion is false.
Both XML files share the important channel differences below.

| Item | Printed supplement | Saved XML |
|---|---|---|
| Cable sign, p.11 S7 | Minus membrane-current term on the right of the rearranged equation. | `c/cfeq1`: `c=radiusf^2`, `da=2*radiusf*rL*Cmf`, `f=-2*radiusf*rL*(J_mf+J_elec)`. Standard coefficient-form rearrangement gives a **plus** outward-current term on that side. |
| Diffusion units, p.15 | Table S5 labels diffusion in micrometres/ms. | Parameters describe micrometres squared/ms; p.10 also specifies squared-length units. |
| HCN2 conductance, pp.15/19 | 3.6 nS total, split equally. | `var7/g_hcn2i=1.85/ai`, `var8/g_hcn2e=1.85/ae`: 3.7 nS total. |
| HCN2 time constant, p.19 | First exponential inside the reciprocal has a positive argument. | `var7/avTau_hcn2i` and `var8/avTau_hcn2e` have a negative argument there. |
| Nodal Kv7.x, p.20 | Half-activation −47 mV, slope 8 mV, voltage-dependent time constant. | `var12/avInf_kLinof`: −41/7.4; `avTau_kLinof`: 54 ms. |
| Fiber sodium open probability, p.21 | Activation times inactivation. | `var13`, `var14`, `var16`: activation **cubed** times inactivation. |
| Fiber sodium steady-state gates, p.21 | Activation −40/8 and inactivation −69/7.6 mV. | `var12`: −40.1/7.5 and −78.8/6 mV. |
| Fiber sodium time constants, p.21 | Analytic fits with specified outside-range behavior. | `sw1`/`sw2` contain multiple interpolation/analytic children. Selected/default behavior is not established here. |

The sign inference uses the
[official coefficient-form equation](https://doc.comsol.com/6.4/doc/com.comsol.help.comsol/comsol_ref_equationbased.32.009.html):
with the other saved coefficients zero, `da*u_t - c*u_xx = f`. This is algebraic
interpretation, not execution or a claim of COMSOL-version reproduction. The
documentation page/equation image are retained with URL/hash provenance.

Table S3 also includes inner ERG current absent from the inspected net-current
expression; outer XML current includes Kv7.4. The fiber/calyx boundary integral
needs its geometry/normalization checked before transcribing p.11's axial-current
expression. The inventory preserves the original couplings.

The paper model additionally has `spike=1` and
`tsNavF=if(spike==1,1,1000)` in sodium activation timing. That diagnostic control
must not become biological modulation. Its nominal step study sweeps bundle
displacement. The template has separate step, voltage-clamp and sinusoid studies,
overriding top-level `stim=4`; that default alone does not identify the experiment.

## Implementation consequence

Expose physical bundle displacement, hair-cell membrane state, local coupling
and individual afferent membrane/event state as distinct causal stages. A
normalized activation per body frame cannot be renamed an emitted sensory spike.
Retain stable identities and complete physical-time coverage; missing body input
cannot be silently replaced with silence.

For a manageable first implementation, start with mutually supported MET,
hair-cell charge balance and channel terms as an explicitly reduced component.
Declare the fixed bath used for isolated checks and its omission of spatial
cleft feedback. Qualify signs, units, voltage-clamp arithmetic, equilibrium and
time-step refinement before calyx/afferent coupling. This staged decision does
not make an isolated hair cell a complete brain–body loop. Choose and name each
disputed downstream equation before comparison, without tuning it for balance.

No vestibular experimental window was launched. Reported motor/head outcomes
retain priority. Physiological sensory firing, closed-loop authority and Avatar
balance/recovery remain open.

## Follow-up: spatial membrane-current ownership

Before implementing joint membrane/cleft derivatives, both retained XML
inventories were checked for the hair membrane locations. `var4` sets the
uncovered net current and KL/Ca/HCN1 conductance densities to zero; `var5`
normalizes the active conductances by cleft-facing `ai`, with `g_klmaxh=80`.
`var6` and `bode2/dode1` use the local transmembrane voltage and independent
gate state. This supports distributing the active whole-cell currents across
cleft-facing patches while retaining uncovered capacitance. It does not support
spreading the same active density over the entire basolateral area.
The new assembly and remaining numerical/physiological limits are recorded in
`DNNE_Cleft_Membrane_Coupling_2026-11-17.md`. This source inspection evaluates no
equations and does not establish free sensory-cell operation.
