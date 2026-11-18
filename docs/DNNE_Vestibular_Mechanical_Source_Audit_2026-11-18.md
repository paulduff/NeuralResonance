# Separate organ mechanics from neural adaptation

The next physical implementation should use a declared mechanical model of the
endolymph, cupula and otolithic structures. A fitted multiplier on the existing
normalized activation or inherited reduced filters would not establish the
missing biological connection. The new bundle interface provides units and
causal ownership, but does not supply that calibration.

This review inspected five retained article bodies and transcribed the published
human canal matrices. No model, numerical reference, test or benchmark executed.
The three pending windows (`092238`, `ddef1a`, `e9bed6`) were not inspected.

## Canal mechanics and neural responses differ

Rabbitt's [2019 review](https://pmc.ncbi.nlm.nih.gov/articles/PMC6520623/)
(DOI `10.1152/jn.00708.2018`) distinguishes measured cupula relaxation from
adaptation of afferent discharge. Its Figure 3 and accompanying text report
toadfish mechanical relaxation spanning 13–104 seconds, while some afferents
adapt substantially faster. The macromechanics discussion estimates human
cupula relaxation at roughly 16–20 seconds. These are different species and
evidence types, not interchangeable calibration targets.

The current `AvatarClockedVestibularMechanics` retains a 5-second adaptation
coordinate and a 45 ms fast filter from the earlier normalized transducer.
Its numerical comparison qualifies the equations it implements; it does not
establish those constants as human cupula mechanics. Adding a gain in nm/(rad/s)
would leave this unqualified temporal response intact. Its state must not be
interpreted as an independent measurement of cupula displacement.

The review gives the mechanical model

`m Q'' + c Q' + k Q = f`,

where `Q` is displaced volume, `m` hydraulic mass, `c` hydraulic resistance,
`k` volumetric stiffness and `f` inertial pressure. In the circular-duct
approximation, `c = mu*l*lambda/A²`, `k = gamma*h*lambda/Ac²`,
`lambda ≈ 8*pi`, and `f = 2*pi*rho*R²*alpha` for rotation along the chosen
sensitive axis. The slow mechanical time scale is `c/k`. Mass may be neglected
for the declared low/mid-frequency approximation; this is not permission to
discard fast transient evidence in an Avatar fall.

The review limits the simple rigid-duct model's human applicability to roughly
below 6 Hz. Above this range, membranous duct deformation and perilymph mechanics
matter. That limitation remains relevant even if a numerical solver reproduces
the rigid model exactly. The review explicitly says the macromechanical model
does not resolve local hair-bundle displacement across the sensory epithelium.
`Q/Ac` is a mean displacement, not automatically a particular bundle's motion.

## A source for mechanically coupled canals

[Ifediba et al. 2007](https://pmc.ncbi.nlm.nih.gov/articles/PMC3005417/)
(DOI `10.1007/s10439-007-9277-y`) supplies a human three-canal model with shared
duct segments and coupled endolymph/cupula states. Its appendix equations 23–26
give numerical mass, damping, stiffness and forcing coefficients for labyrinth 1.
The source reconstructs right temporal-bone specimens and registers their geometry
to a CT-based head frame. This is a published anatomical example, not Avatar's
measured anatomy or a universal human parameter set.

The exact printed literals are retained in
`docs/reference-data/vestibular-ifediba-2007.json`. Three 6×6 matrices and three
six-element forcing columns were extracted from the original MathML. State order
is endolymph HC/AC/PC followed by cupula HC/AC/PC. The source's printed precision
is retained; no extra anatomical precision or trajectory accuracy is claimed.

| Printed quantity | SI quantity | Exact numerical multiplier |
|---|---|---|
| Mass, g/cm⁴ | kg/m⁴ | 100000 |
| Damping, dyn·s/cm⁵ | Pa·s/m³ | 100000 |
| Stiffness, dyn/cm⁵ | Pa/m³ | 100000 |
| Forcing, dyn/cm² | Pa | 0.1 |
| Displaced volume, cm³ | m³ | 0.000001 |

Time in this equation is seconds. An interface using milliseconds needs an
explicit conversion. These exact decimal conversions are data transcription;
no eigensystem, steady state or trajectory was calculated in this review.

Several issues must be resolved before using the transcription as a body input:

- Equation 26 supplies forcing in published *prime directions*. Preserve the
  printed vectors and establish how the forcing components are reconstructed;
  do not silently treat these rounded directions as an orthonormal basis.
- Figure 5 defines posterior/right-lateral/superior axes. Avatar's skull basis
  is right/up/forward. Their reorder/sign map includes a reflection. Angular
  quantities are axial vectors, so the native rotation convention needs an
  explicit known-rotation control before any sign conversion is adopted.
- The source is a right labyrinth. Bilateral placement and excitatory polarity
  need an explicit mirrored anatomical construction, not copied scalar signs.
- Local cupula deformation at each hair bundle is still missing. The retained
  mean frontal area alone cannot establish an individual cell's displacement.
- Native and independent integrations must agree under input and solver
  refinement, with mechanical energy/dissipation and causal sample accounting.
  A published coefficient table is not a passed numerical comparison.

[Iversen and Rabbitt 2017](https://pmc.ncbi.nlm.nih.gov/articles/PMC5658742/)
(DOI `10.1016/j.bpj.2017.08.001`) provides the wider fluid/elastic wave model and
the low-frequency rigid reduction. It is useful for the domain limits and future
extension. Some rendered scalar viscosity units/values in these sources are
dimensionally suspect. This review does not resolve them by silently importing
one scalar into another paper's model. The matrix transcription uses the explicit
appendix matrix units, and keeps the two model versions separate.

## Otoliths need their own mechanical model

[Dunlap and Grant 2014](https://pmc.ncbi.nlm.nih.gov/articles/PMC4141440/)
(DOI `10.1007/s10162-014-0456-x`) measures utricular mechanics in red-eared
turtles, fitting both magnitude and phase. The organ is a damped mass–elastic
system, including surrounding-fluid effects, rather than a generic slow smoothing
filter. The authors report an underdamped system and a mean natural frequency
around 374 Hz. Their reported frequency values are labelled in Hz; they must not
be inserted as rad/s merely because the paper also uses the symbol omega.

The [2018 receptor review](https://pmc.ncbi.nlm.nih.gov/articles/PMC5980960/)
(DOI `10.3389/fneur.2018.00366`) describes both accelerometer and seismometer
operation and cites an estimated human transition near 600 Hz. This review's
human estimate is not a direct human calibration, and the turtle observations
must retain their species label. Neither supports treating the inherited 80 ms
otolith filter as qualified organ dynamics.

The next otolith implementation needs declared mass, stiffness, damping,
buoyancy/fluid assumptions and local bundle coupling. It must retain static
gravito-inertial displacement as well as motion. Numerical sampling that supports
slow balance inputs does not automatically resolve the reported fast mechanics.
No turtle parameter, estimated human frequency or desired neuronal firing rate
has been installed as a live preset.

## Evidence and implementation decision

`artifacts/vestibular-mechanics-source-20261118` retains the five original XML/HTML
bodies, URL/hash metadata, derived text, original equation MathML and a static
transcription report. Unavailable Europe PMC XML requests are retained separately
from the successful public PMC HTML captures. Their errors are not biological
evidence. The three earlier bibliography entries were also identified by title;
they concern afferent modelling/coding or gravito-inertial ambiguity and do not
by themselves provide the missing dimensional organ calibration.

`tools/review-vestibular-mechanics-sources.py` binds the five source bodies to
their SHA-256 hashes, checks exact matrix shape/symmetry/transcription, preserves
the source frame and writes the catalog with explicit unqualified status. It
executes no downloaded code or physical model. There is no live consumer of the
catalog. Earlier physiology, pending studies, thresholds and saves remain intact.

Proceed with a distinct, source-declared mechanical candidate and explicit
coordinate/input ownership. Retain the existing reduced implementation as prior
evidence. Keep mechanical deformation, hair-cell transduction, sensory axonal
firing and central synaptic learning separate, and qualify their connected
response before enabling the pathway in World or claiming balance/recovery.
