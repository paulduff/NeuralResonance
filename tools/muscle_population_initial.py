"""Frozen named TA population inputs and an independent passive balance comparison."""
from decimal import Decimal, localcontext
import importlib.util
import json
from pathlib import Path
import struct

ROOT=Path(__file__).resolve().parents[1]
NATIVE=Path('D:/DNNE-build-work/named-muscle-native-20261123-998900')


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path); value=importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value); return value


def read(path): return json.loads(path.read_text(encoding='utf-8-sig'))


def make_input(package):
    source=read(package/'population-source/population.json'); raw=source['originalMatFields']
    assert source['units']==200 and source['slowUnits']==147 and source['fastUnits']==53
    names=('storeSites','totalCalcium','releaseRatePerSecond','pumpRatePerSecond','bindingRatePerSecond',
        'unbindingRatePerSecond','releaseRiseSeconds','releaseDecaySeconds','hillExponent',
        'halfActivationBinding','activationSeconds','cooperativity','peakTetanicForceNewtons')
    rows=[]
    for i,r in enumerate(source['rows'][:147]):
        assert r['row']==i+1 and r['fiberType']=='slow'; p=r['parameters']
        assert struct.pack('<15d',*(p[k] for k in source['rowColumns'])).hex()==r['littleEndianBinary64Hex']
        rows.append(dict(row=i,sourceRow=i+1,calcium={k:p[k] for k in names},
            lengthGainAboveOptimal=p['lengthGainAboveOptimal'],lengthGainBelowOptimal=p['lengthGainBelowOptimal']))
    anatomy={r['name']:r for r in read(package/'population-anatomy/anatomy-catalog.json')['muscles']}
    maps={r['path']['MuscleId']:r for r in read(package/'frame-review/mapped-lower-leg-paths.json')['paths']}
    endpoints=[json.loads(line) for line in (package/'population-native/native/quiet/acknowledged.jsonl').read_text(encoding='utf-8-sig').splitlines()]
    assert len(endpoints)==9 and endpoints[1]['physicsStep']==1
    def value(bits): return struct.unpack('>d',bytes.fromhex(bits['bits'] if isinstance(bits,dict) else bits))[0]
    lengths={r['muscleId']:value(r['lengthMeters']) for r in endpoints[1]['paths']}
    original=dict(optimalFiberLengthMeters=raw['optimalLength']/100,referenceTendonLengthMeters=raw['L0T']/100,
        fiberMassKg=raw['mass'],pennationRadians=raw['pennationAngle'],referenceForceNewtons=raw['F0'],passiveLengthScale=raw['Lmax'])
    cases=[]; declarations=[]
    for muscle in ('tibant_l','tibant_r'):
        p=anatomy[muscle]['parameters']; scale=maps[muscle]['shinMap']['Scale']
        assert not anatomy[muscle]['requiresWrapping'] and scale==maps[muscle]['footMap']['Scale']
        optimal=p['optimal_fiber_length']['value']*scale
        # Keep the source TA force coefficients and passive force scale. Change
        # mass in proportion to fiber length to preserve the source PCSA, density
        # and specific-tension relation; do not multiply output forces.
        candidate=dict(optimalFiberLengthMeters=optimal,
            referenceTendonLengthMeters=p['tendon_slack_length']['value']*scale*(raw['L0T']/raw['tendonSlackLength']),
            fiberMassKg=raw['mass']*(optimal/original['optimalFiberLengthMeters']),
            pennationRadians=p['pennation_angle_at_optimal']['value'],referenceForceNewtons=raw['F0'],passiveLengthScale=raw['Lmax'])
        declarations.append(dict(muscleId=muscle,dimensionSource=p,pairedShinScale=scale,
            nominalTendonReferenceRatio=raw['L0T']/raw['tendonSlackLength'],sourceCrossSectionRetained=True,
            openSimForceImported=False,constantPennationAssumed=True,footScaleIsDeclaredShinRatio=True,
            measuredPathLengthMeters=lengths[muscle],pathFingerprint=maps[muscle]['path']['Fingerprint'],
            nativeBodyFingerprint=endpoints[1]['bodyFingerprint'],physicalStep=1))
        for shape,g in (('reference',original),('source-dimensions',candidate)):
            cases.append(dict(key=muscle+'-'+shape,muscleId=muscle,shape=shape,geometry=g,
                measuredPathLengthMeters=lengths[muscle],physicalTimeMs=value(endpoints[1]['physicalTimeMs']),
                lowerFiberMeters=.4*g['optimalFiberLengthMeters'],
                upperFiberMeters=1.4*g['optimalFiberLengthMeters']))
    return dict(version='named-ta-slow-population-initial-v1',parameterSha256=source['parameterSha256'],
        parameterRows=rows,cases=cases,geometryDeclarations=declarations,originalPeakForcesRetained=True,
        omittedFastUnits=53,sourceCapacityAccounting=source['capacityAccounting'],anatomicalCalibrationQualified=False,
        muscleTrajectoryExecuted=False,neuronalSourceExecuted=False,nativeUnityExecuted=False,avatarStandingQualified=False)


def prepare(package):
    helper=module('population_input_preservation',ROOT/'tools/native_articulated_head_study.py')
    def copy(source,destination,names=None):
        if names is None:names=[e['Path'].replace('\\','/') for e in read(source/'backup-manifest.json')['Files']]
        helper.protected_copy(source,package/destination,names)
    copy(ROOT/'artifacts/motor-population-source-20261123','population-source')
    copy(ROOT/'artifacts/muscle-anatomy-source-20261123','population-anatomy',['anatomy-catalog.json','provenance.json','Rajagopal2016.osim'])
    review=ROOT/'artifacts/named-muscle-native-20261123-998900-review'
    proof=read(review/'review.json')
    assert proof['operatorReportedCompletion'] and proof['namedNativeForceAccountingPassed'] and proof['nativeCases']==12
    assert proof['sourceRevision']=='250fb111aeef3da1195b4decf2f03144310acbd8'
    copy(review,'population-native-review')
    assert helper.digest(NATIVE/'backup-manifest.json')==proof['manifestSha256']
    copy(NATIVE,'population-native',['native/quiet/acknowledged.jsonl'])
    with (package/'population-input.json').open('x',encoding='utf-8') as f:json.dump(make_input(package),f,indent=2,allow_nan=False);f.write('\n')


def preflight(package):
    assert read(package/'population-input.json')==make_input(package),'Population source/dimension inputs changed.'


def compare(package):
    preflight(package)
    assert read(package/'configuration.json')['MusclePopulationInitialStudy']
    reference=module('population_decimal_reference',ROOT/'tools/muscle_initial_balance_reference.py')
    source=read(package/'population-input.json'); result=read(package/'population-study/result.json')
    assert result['completed'] and result['passed'] and len(result['cases'])==4
    assert not any(result[k] for k in ('neuronalSourceExecuted','muscleTrajectoryExecuted','nativeUnityExecuted','anatomicalCalibrationQualified','avatarStandingQualified'))
    report=[]
    for case,row in zip(source['cases'],result['cases'],strict=True):
        assert case['key']==row['key'] and case['geometry']==row['geometry'] and row['declaredUnits']==147
        assert row['quietOriginalState'] and row['originalDefinitionsIntact'] and row['passed']
        r=row['result']; state=r['candidate']; assert r['balanced'] and r['bracketed'] and r['definition'] is not None
        assert r['measuredPathLengthMeters']==case['measuredPathLengthMeters']
        assert state['timeMs']==case['physicalTimeMs'] and state['fiberVelocityMetersPerSecond']==0
        assert r['requestedLowerFiberMeters']==case['lowerFiberMeters'] and r['requestedUpperFiberMeters']==case['upperFiberMeters']
        assert r['limits']==dict(fiberLengthToleranceMeters=1e-13,forceAbsoluteToleranceNewtons=1e-9,forceRelativeTolerance=1e-12,maximumIterations=128)
        units=r['definition']['units']; assert len(units)==len(state['units'])==147
        for definition,expected in zip(units,source['parameterRows'],strict=True):
            assert definition['calcium']==expected['calcium'] and definition['lengthGainAboveOptimal']==expected['lengthGainAboveOptimal'] and definition['lengthGainBelowOptimal']==expected['lengthGainBelowOptimal']
        a,ra=reference.reference(row,80); b,rb=reference.reference(row,110)
        with localcontext() as ctx:
            ctx.prec=110; error=abs(Decimal.from_float(state['fiberLengthMeters'])-b); agreement=abs(a-b)
        passed=agreement<=Decimal('1e-60') and error<=Decimal.from_float(1e-11) and abs(rb)<=Decimal.from_float(r['forceAllowanceNewtons'])
        report.append(dict(key=case['key'],reference80Meters=str(a),reference110Meters=str(b),referenceAgreementMeters=str(agreement),
            candidateErrorMeters=str(error),independentForceResidualNewtons=str(rb),forceAllowanceNewtons=r['forceAllowanceNewtons'],
            normalizedFiberLength=state['forces']['normalizedFiberLength'],axialTendonForceNewtons=state['forces']['tendonAxialForceNewtons'],passed=passed))
    outcome=dict(completed=True,passed=all(r['passed'] for r in report),cases=report,unitsPerMuscle=147,
        originalLimitsRetained=True,geometryIsExplicitCandidate=True,sourcePeakForcesRetained=True,fastUnitsImplemented=False,
        muscleTrajectoryQualified=False,anatomicalFitQualified=False,neuronalAuthority=False,avatarStandingQualified=False)
    with (package/'population-analysis.json').open('x',encoding='utf-8') as f:json.dump(outcome,f,indent=2,allow_nan=False);f.write('\n')
    if not outcome['passed']:raise RuntimeError('Population initial balance failed; preserve source, candidate geometry and original bounds.')
    print('Four complete 147-unit passive initial conditions reconciled at 80/110 digits. Candidate dimensions, active populations, native muscle coupling and standing remain separate.',flush=True)
