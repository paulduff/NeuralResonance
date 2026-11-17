# Hair-cell numerical result

The operator reported `vestibular-hair-cell-20261117-959475` complete. Its reduced
hair-cell equations pass the original numerical criteria. This supports the
next local coupling stage, not a physiological or afferent-firing claim.

`tools/review-vestibular-hair-cell.py` verified all 2,472 files in each C:/D: copy,
1,787 Git source files, 1,800 frozen package entries, built assemblies and managed
runtime. The numerical dependency inventory stayed exactly unchanged. The saved
comparison reproduced exactly, with solver entry points disabled during review;
no ODE integration or test was rerun. Saved analytic-control arrays were also
checked. The separate `vestibular-hair-cell-review-20261117` review is D: verified.

The frozen source revision is `59b586bdb85a190797b077a9644b2e88496012f0` and the
capture manifest SHA-256 is
`3EB631B935B13D198FC7F80B9274B9DAB5124C69E73949B3D3CC45B79B0FD3B9`.

## Numerical evidence

All 47 exact test cases passed: 23 hair-cell cases, 16 physical-clock cases,
seven preserved labyrinth cases and the solution manifest. All 7,260 channel
arithmetic cases and twelve reference controls passed. Six prescribed bundle
histories were retained at three native steps, with 8,001 endpoints each:
144,018 complete native states. Eighteen independent reference trajectories
retain 103,366 accepted nodes, every segment boundary and all eleven states.

| Maximum discrepancy | Measured | Original limit |
|---|---:|---:|
| Independent reference voltage | 1.882e-8 mV | 1e-6 mV |
| Independent reference gates | 1.442e-12 | 2e-8 |
| Independent reference charge | 1.139e-7 fC | 1e-4 fC |
| Finest native voltage against any reference | 2.002e-6 mV | 5e-5 mV |
| Finest native gates | 5.561e-10 | 2e-7 |
| Finest native charge | 1.263e-5 fC | 5e-4 fC |
| Capacitive/ionic charge ledger | 1.180e-9 fC | 1e-7 fC |
| Pump stoichiometry ledger | 1.117e-9 fC | 1e-7 fC |

All eighteen reference contrasts, 54 native/reference contrasts, twelve step
refinements and 54 charge/domain checks passed. Refinement used the original
halving requirement and declared roundoff floors; no bound was changed.

## What the prescribed inputs do

Relative to the matching quiet trace at 750 ms, +300 nm gives +4.640 mV,
−300 nm gives −1.268 mV and +1,000 nm gives +6.455 mV. The positive and negative
physical deflections therefore produce opposite electrical responses in this
reduced model. These are recorded responses, not fitted human vestibular data.

The quiet trace starts at the declared −75 mV, reaches −87.585 mV and ends at
−85.239 mV after one second. That retained transient must not be described as
starting at free equilibrium or proving long-term settling. Residual responses
after release belong to the evolving channel state; no explicit MET adaptation
mechanism was added. All baths remain held reservoirs.

## Next coupling boundary

The calyx membrane receives local cleft voltage and ions; its inner and outer
faces need separate channel states and charge accounting. Its currents must
eventually participate in shared cleft dynamics and an actual afferent membrane,
with preserved physical time and emitted event identity. A scaled hair-cell
voltage or prescribed population activation cannot substitute for that chain.

Live body conversion to bundle displacement, spatial/reduced-cleft adequacy,
afferent firing, central contacts, physiology and Avatar balance remain open.
No current live transducer, world-start behavior, editor selection or save changed.
