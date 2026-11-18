# Neuronal sensory cable and reciprocal calyx connection

The sensory trajectory and stationary work previously ended at an explicit
external current port. A zero port meant a sealed calyx, not a sensory neuron.
The new candidate adds active axonal membrane equations, an identified cable,
and one owner for its instantaneous electrical exchange with the calyx. It is
not selected by live Avatar, Unity, DNNE services or the editor yet.

## Chosen biological model

Source: Govindaraju et al., PNAS 2023, DOI `10.1073/pnas.2207466120`, and the
[deposited repository](https://github.com/acgsci/vestibularHairCellCalyxNQT/tree/47d499449df66c6676ee43cbb08ea1995469a817)
at the pinned revision. The earlier equation audit records material differences
between the printed supplement and saved simulation expressions. Those
differences are retained. This candidate is explicitly named
`vestibular-afferent-deposited-may14-linear-v1`.

The candidate uses the template's `var12` to `var16` channel definitions:

* Sodium current has actual activation cubed times actual inactivation, `m^3 h`.
  Activation midpoint/slope are -40.1/7.5 mV; inactivation -78.8/6 mV.
* Kv1 activation uses -44/7.1 mV and a 3.7 ms time constant.
* Kv7 activation uses -41/7.4 mV and a 54 ms time constant.
* Kv3.4 retains separate activation/inactivation, the deposited voltage-dependent
  activation time and 25.4 ms inactivation time.
* Nodal/heminodal conductance densities are sodium 120, Kv1 0.13, Kv7 0.1 and
  Kv3.4 0.2 nS/um^2. The unmyelinated segment has sodium 30 and zero potassium
  conductances. The myelinated membrane has no ionic channel current here.
* Specific capacitance is 0.01 pF/um^2, or 0.00002 under myelin. Intracellular
  K/Na are held at 150/12 mM, external K/Na at 5/140 mM, thermal voltage 26 mV.
  Reversals use these concentrations; no pump, ATP recovery or evolving axonal
  ion concentration is fabricated. Kinetics remain the room-temperature model.

Sodium timing is an explicit model choice: the 24-point `sw1/int3` May14
activation table and 22-point `sw2/int4` May14 inactivation table. Piecewise
linear interpolation is declared. Activation outside [-55,60] mV is 0.2 ms;
inactivation extends the endpoint secants. Nonpositive/nonfinite extrapolated
time constants are rejected, not clipped. No diagnostic `spike`/`tsNavF`
multiplier is imported. The saved switch does not explicitly establish which
child/default interpolation COMSOL selected. This candidate therefore does
**not** claim an unchanged paper or COMSOL reproduction.

The 46 transcribed timing values match the retained source data exactly in a
static check. This establishes transcription, not physiological validity. The
supplement's m*h sodium formula and other different curves were not silently
combined with the deposited definitions. Printed pages 15,20,21 were visually
reviewed; their preserved images and source PDF were verified against D: copies.

## Physical current ownership

`AvatarAfferentMembraneKernel` evaluates currents and six gate derivatives from
actual local voltage and gates. Voltage-conditioned gates and analytic
held-voltage relaxation exist as explicit initialization/control operations;
neither is invoked by cable derivative evaluation to reset a moving cell.

`AvatarAfferentCable` uses named control volumes and physical connections.
Membrane area is `2*pi*r*L`, capacitance is area times specific capacitance, and
an axial edge has conductance `sigma*A/d`. For a cylindrical axon, `A=pi*r^2`.
Each edge current is added with opposite signs at its two nodes. Outward
membrane and terminal currents remove intracellular charge:

`C_i*dV_i/dt = -I_membrane_i - sum(I_axial_outward_i)`.

This fixes signs and units from charge conservation; it does not infer the
normalization of the source's mixed boundary expression. With held perilymph,
the cable also reports capacitor energy, membrane/terminal electrical power,
nonnegative axial dissipation, and explicit charge/power residuals. The powers
are electrical accounting, not an ATP consumption model. Conducting edges must
form a connected graph. Missing, duplicate or unknown state/terminal identities
are rejected. A sealed terminal is explicitly declared with zero conductance.

The reference mesh builder preserves the deposited 425 um cable and region
boundaries 0,8,9,80,82,382,384,425 um, at radius 1.5 um and conductivity
1000 nS/um. A declared number of cells subdivides each region without moving
those boundaries. Proximal conductance spans the first half-cell; the distal
end is explicitly sealed. This is a finite-volume anatomical hypothesis, not
the authors' finite-element mesh or a demonstrated spatially converged cable.

`AvatarVestibularCleftAfferent` maps identified cable terminals to identified
calyx patches. Every conducting terminal needs a declared owner. It uses the
same instantaneous calyx voltage as the cable boundary, then gives the calyx
the negative of that terminal current. Multiple contacts retain their separate
patch ownership. All other cable terminals must be sealed. The joint owner
requires matching chemical reservoirs and supplies the existing unchanged
membrane/cleft kernel with these actual currents. It reports interface current
and power cancellation. No body-frame average, delayed motor-like command or
scripted event replaces the reciprocal electrical connection.

## Qualification and continuation

Thirty-four prospective tests cover channel kinetics, actual gates, reversals,
channel removal, analytic voltage clamp, source geometry, passive exchange and
dissipation, membrane sign, reference shifts, identity, explicit cuts, individual
contacts and invalid inputs. Together with the earlier 132 checks, the focused
inventory is 166. Authoring test-project build passed with zero warnings/errors;
AST, inventory, timing-data transcription and PowerShell parsing passed. No new
test, model trajectory or independent numerical reference ran inline.

`afferent_component_gate.py` and `start-afferent-components-window.ps1` freeze
these component checks from a clean pushed revision, using reviewed `884934`
source/runtime evidence and verified biological source copies. Tests execute
in a separate visible PowerShell window, without agent monitoring, and the
finished package receives a verified D: backup. Old models and captures remain.

This stage qualifies neither free sensory firing nor a usable brain-body loop.
Next: independent high-precision comparisons of the new channel/cable/joint
derivatives; joint stationary initialization with the cable's actual load;
native/independent joint trajectories with spatial/time refinement and observed
distal neuronal crossings; then identified sensory-event delivery into DNNE.
The sealed-calyx rest result cannot automatically initialize the loaded cable.
No live model/editor selection changes until the relevant coupled checks pass.
