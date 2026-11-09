# Completed Golgi reference continuation

The operator reported `golgi-resume-20261109-3b4dff` complete. Source
`2d86a516046d016c5f8b69eaf37220bb43c0c166` finished at 09:46:36 UTC on 9 November.
The missing no-SK/Radau solve completed with 2,464,100 RHS evaluations and maximum
SK mass error `1.7875e-14`. The eleven earlier solves were reused unchanged.

`review-golgi-resume.py` recomputed all twelve reference comparisons and all
36 candidate comparisons from saved arrays without calling an integrator. Every
recorded metric reproduced exactly. Both complete C:/D: captures verify:
**1,473 files, 786,297,814 bytes**, plus identical manifests, including 1,460 frozen
package files. The separate `artifacts/golgi-resume-review-20261109/review.json`
and its D: backup are verified. The interrupted original and sealed copy remain
unchanged.

## Reference agreement now meets the original gate

All six protocols have matching crossing counts between reference methods. Both
old-strict versus tighter BDF and tighter BDF versus Radau pass the unchanged
`0.01 ms` timing and `0.05 mV` soma criteria. The table shows the maximum over
those two reference contrasts and the finest C# candidate versus Radau.

| Protocol | Crossings | Reference timing difference, ms | Reference soma difference, mV | Finest candidate timing difference, ms |
| --- | ---: | ---: | ---: | ---: |
| Quiet | 37 | 0.000004705 | 0.001326 | 1.567325 |
| Depolarizing pulse | 43 | 0.000005520 | 0.001655 | 1.917229 |
| Hyperpolarizing pulse | 32 | 0.000002697 | 0.000729 | 0.795962 |
| No persistent sodium | 0 | 0 | 0.000000008353 | 0 |
| No HCN | 27 | 0.000003913 | 0.001229 | 1.928369 |
| No SK | 128 | 0.000008870 | 0.002753 | 0.223184 |

This establishes the declared numerical agreement for these equations and test
conditions. The methods share the declared model and analytic rate conventions;
this is not native NEURON reproduction or physiological calibration.

## The candidate solver remains the next repair

The C# first-order coupled solver agrees on every count but has substantial
timing errors. Quiet timing errors at steps 0.015625, 0.0078125 and 0.00390625 ms
are 7.8456, 3.4044 and 1.5673 ms. The no-SK sequence is 0.5381, 0.2091 and
0.2232 ms, so it is not monotonically improving at the finest step.

Full unaligned soma waveform differences reach 51.5–90.7 mV in firing cases,
while first-20-ms differences at the finest step are below 0.0008 mV. Spike phase
errors can produce large waveform differences; neither aligning spikes nor
reporting only short quiet intervals would repair those errors. All raw metrics
remain available. The no-persistent-sodium nonfiring condition has a maximum
soma difference of about `3.085e-5 mV`.

The next implementation should improve coupled voltage/gate/calcium integration
and compare its refinements against these verified reference arrays. Preserve
the original split solver as a control. Keep channel parameters, injected inputs,
event definitions and declared accuracy limits fixed; retain every spike and
accounting quantity. A higher-order method must still demonstrate stability and
domain preservation rather than clipping failed states. No live Golgi population,
learning circuit, motor control or Avatar recovery is qualified by this result.
