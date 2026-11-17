"""Prospective constitutive inputs and bounds; no equations are evaluated here."""
from itertools import product

COUNTS = dict(CleftCouplingTests=25, SolutionManifestTests=1)


def protocol():
    material_names = ['potassiumDiffusionSquareMicrometersPerMs', 'sodiumDiffusionSquareMicrometersPerMs',
                      'thermalVoltageMv', 'otherIonConductivityNsPerMicrometer']
    geometry_names = ['faceAreaSquareMicrometers', 'centerDistanceMicrometers',
                      'leftVolumeCubicMicrometers', 'rightVolumeCubicMicrometers']
    cap_names = ['hairApicalPf', 'hairPerilymphPf', 'hairCleftPf', 'calyxInnerPf', 'calyxOuterPf']
    port_names = ['hairToEndolymphPa', 'hairToPerilymphPa', 'hairToCleftPa', 'calyxToCleftPa',
                  'calyxToPerilymphPa', 'cleftOutgoingPa', 'calyxToAxonPa']
    voltage_names = ['hairMv', 'cleftMv', 'calyxMv', 'endolymphMv', 'perilymphMv']
    edge_cases = []
    for material, geometry, voltage, ions in product(
        [[.81,.56,26.,600.],[.81,.56,26.,0.],[0.,.56,26.,600.]],
        [[2.,4.,3.,7.],[.02,.5,.01,.02],[1.,10.,1000.,.01]],
        [-26000.,-520.,-52.,-.026,-.025974,-1e-10,0.,1e-10,.025974,.026,52.,520.,26000.],
        [[5.,140.,5.,140.],[8.,130.,3.,140.],[.1,.1,50.,300.],[5.,140.,10.,280.]]):
        edge_cases.append(dict(index=len(edge_cases),material=dict(zip(material_names,material,strict=True)),
            geometry=dict(zip(geometry_names,geometry,strict=True)),
            left=dict(potentialMv=0.,potassiumMm=ions[0],sodiumMm=ions[1]),
            right=dict(potentialMv=voltage,potassiumMm=ions[2],sodiumMm=ions[3])))
    charge_cases = []
    for caps, ports, voltage in product(
        [[3.,.4558,2.9463,2.9463,2.8833],[1.,0.,1.,1.,1.],[.1,.02,.5,.3,.2]],
        [[0.,0.,0.,0.,0.,0.,0.],[0.,0.,1.,0.,0.,0.,0.],[0.,0.,0.,1.,0.,0.,0.],
         [0.,0.,0.,0.,0.,-1.,0.],[7.,3.,11.,-13.,17.,19.,-23.]],
        [[-70.,3.,-60.,5.,0.],[-54.,19.,-44.,21.,16.],[5.,5.,5.,5.,5.],[12.,-4.,-20.,5.,2.]]):
        charge_cases.append(dict(index=len(charge_cases),capacitances=dict(zip(cap_names,caps,strict=True)),
            ports=dict(zip(port_names,ports,strict=True)),potentials=dict(zip(voltage_names,voltage,strict=True))))
    return dict(version='cleft-constitutive-comparison-v1',
        edgeVersion='cleft-monovalent-scharfetter-gummel-edge-v1',
        chargeVersion='hair-cleft-calyx-capacitance-block-v1',
        edgeCases=edge_cases,chargeCases=charge_cases,expectedEdges=468,expectedCharges=60,
        decimalPrecisions=[60,90],referenceAbsoluteTolerance='1e-40',referenceRelativeTolerance='1e-40',
        candidateAbsoluteTolerance='2e-10',candidateRelativeTolerance='2e-12',
        spatialGeometryCalibrated=False,timeIntegrated=False,physiologyQualified=False,avatarRecoveryAssessed=False)


def validate(value):
    assert value == protocol(), 'Constitutive inputs or prospective bounds changed.'
    return value
