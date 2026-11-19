"""Prospective integer partitions only; construction evaluates no model."""
from itertools import accumulate
from canal_clock_protocol import protocol as original_protocol


def widths(refinement):
    assert refinement in (1,2,4)
    return [w for w in (1,2,4,8,16,32,64,128,256,512,1024) for _ in range(32*refinement)]+[32]*refinement


def ticks(refinement):
    return list(accumulate(widths(refinement)))


def reference_ticks():
    # Union of every finest candidate endpoint and the ORIGINAL uniform 1024
    # anchor grid. No native output determines reference placement.
    return sorted(set(ticks(4))|set(range(256,262145,256)))


def protocol(frame):
    spec=original_protocol(frame)
    del spec['subdivisions']
    grid=reference_ticks()
    assert all(set(t*(4//r) for t in ticks(r))<=set(grid) for r in (1,2,4))
    gaps=[b-a for a,b in zip([0]+grid,grid)]
    assert grid[-1]==262144 and all(g>0 and g&(g-1)==0 for g in gaps)
    spec.update(version='retained-head-graded-canal-clock-v1',ownerVersion='physical-clock-graded-canal-held-input-v1',
        scheduleVersion='declared-dyadic-canal-substeps-v1',refinements=[1,2,4],
        baseTicksPerInterval=65536,baseWidthsTicks=widths(1),referenceTicksPerInterval=262144,referenceEndpointTicks=grid,
        expectedNativeSteps=199*353*7,expectedReferenceSamples=199*len(grid)+1,
        schedulePolicy='32 substeps per dyadic band, explicit 32-tick closing remainder; each refinement splits every step equally',
        oldUniformAnchorSubdivisions=1024,originalUniformCapturePreserved=True,
        boundsSource='unchanged failed uniform-clock limits; all accepted graded endpoints and original independent anchors retained')
    assert all(sum(widths(r))==65536*r and len(widths(r))==353*r for r in (1,2,4))
    return spec
