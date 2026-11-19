"""Prospective replay and query placement; no model evaluation."""
from copy import deepcopy
from canal_graded_protocol import reference_ticks


def protocol(graded):
    assert graded['version']=='retained-head-graded-canal-clock-v1'
    spec=deepcopy(graded)
    spec.update(version='retained-head-spatial-bundle-v1',
        bridgeVersion='graded-canal-spatial-bundle-owner-v1',denseVersion='canal-radau-iia2-quadratic-dense-v1',
        bindingVersion='declared-cupula-stiffness-binding-v1',
        geometry=dict(organId='declared-right-labyrinth-1',canal=0,
            source='Ifediba Table A1 material/aspect; circular geometry inferred from published HC stiffness, not measured Avatar anatomy',
            sourceSha256='07724245a89d5bf4959de50181830d932f70ad15ba402c6ffaef00dc2f0db1e2',
            shearModulusPascal=.37,thicknessOverRadius=.5),
        attachment=dict(hairCellId='ddc1d6ab-dc76-413d-b879-dc70d0d34e56',rootXOverRadius=0.,bundleLengthMeters=40e-6,excitatoryPolarity=1,
            source='Declared central 40 micrometre attachment and positive polarity; species transfer and human calibration unqualified'),
        queryReferenceTicks={str(r):[t for t in reference_ticks() if t%(4//r)==0] for r in (1,2,4)},
        queryPolicy='Every reference knot addressable on each integer owner clock; includes all accepted endpoints and all 1024 uniform anchors per interval',
        denseProjectionArithmeticRelative=1e-12,denseProjectionArithmeticAbsolute=1e-20,
        geometryArithmeticRelative=1e-12,originalAcceptedTrajectoriesMustMatchExactly=True,
        continuousMotionAccuracyQualified=False,connectedSensoryDynamicsExecuted=False)
    return spec
