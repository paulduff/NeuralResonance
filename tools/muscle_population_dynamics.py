"""Preserve completed initial states and declare a full slow-population study.

Preparation/preflight decode saved data only. Controls and integration are
explicit stages owned by the visible PowerShell runner.
"""
import importlib.util
import json
import os
from pathlib import Path

for name in ('OMP_NUM_THREADS','OPENBLAS_NUM_THREADS','MKL_NUM_THREADS','NUMEXPR_NUM_THREADS'): os.environ[name]='1'

ROOT=Path(__file__).resolve().parents[1]


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path); result=importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result); return result


def read(path): return json.loads(path.read_text(encoding='utf-8-sig'))


def write(path,value):
    with path.open('x',encoding='utf-8') as f: json.dump(value,f,indent=2,allow_nan=False); f.write('\n')


def environment(): return module('population_dependencies',ROOT/'tools/golgi-reference-resume.py').environment()


def make_input(package):
    limits=module('population_bounds',ROOT/'tools/dynamic_muscle_protocol.py')
    original=read(package/'population-input.json')
    proof=read(package/'population-initial-review/review.json')
    assert proof['operatorReportedCompletion'] and proof['initialBalancePassed'] and proof['unitsPerMuscle']==147
    assert proof['sourceRevision']=='917be280abd608f3b33910028cabb0d180de4234' and proof['passedManagedTests']==379
    assert read(package/'population-initial-review/population-input.json')==original
    completed=read(package/'population-initial-review/population-result.json')
    assert completed['completed'] and completed['passed'] and len(completed['cases'])==4
    initial={c['key']:c for c in completed['cases']}
    cases=[]
    def case(key,side,units=(),mixed=False,stretch=False,boundary='Free'):
        saved=initial['tibant_'+side+'-source-dimensions']; state=saved['result']['candidate']
        assert saved['passed'] and saved['quietOriginalState'] and saved['originalDefinitionsIntact']
        assert not state['faulted'] and state['fiberVelocityMetersPerSecond']==0 and state['yield']==1
        assert len(state['units'])==147 and all(all(u[k]==0 for k in ('freeCalcium','boundCalcium','activation',
            'releaseDrive','releasedCalcium','recoveredCalcium','netBoundCalcium','arrivals')) for u in state['units'])
        knots=[(0.,0.),(200.,0.)]
        if stretch: knots=[(0.,0.),(30.015625,0.),(80.015625,.001),(130.015625,-.001),(180.015625,0.),(200.,0.)]
        events=[dict(unitIndex=i,offsetMs=t+.015625*(i%7)) for i in units for t in ((5.,25.,45.,65.) if mixed else (5.,))]
        cases.append(dict(key=key,geometry=saved['geometry'],boundary=boundary,initialTimeMs=state['timeMs'],
            initialFiberLengthMeters=state['fiberLengthMeters'],initialMusculotendonLengthMeters=state['musculotendonLengthMeters'],
            durationMs=200.,lengthKnots=[dict(offsetMs=t,lengthOffsetMeters=x) for t,x in knots],
            events=sorted(events,key=lambda e:(e['offsetMs'],e['unitIndex']))))
    case('left-quiet','l'); case('right-quiet','r')
    case('left-first-unit','l',[0]); case('left-last-unit','l',[146])
    case('left-mixed','l',range(147),True); case('right-mixed','r',range(147),True)
    case('left-mixed-stretch','l',range(147),True,True)
    case('left-mixed-fixed','l',range(147),True,boundary='FixedLengthDiagnostic')
    return dict(version='named-ta-full-slow-population-dynamics-v1',parameterSha256=original['parameterSha256'],
        parameterRows=original['parameterRows'],stepsMs=[.125,.0625,.03125],sampleMs=.25,cases=cases,
        referenceSettings=limits.SETTINGS,maximumReferenceStepSeconds=.002,
        referenceBounds=limits.REFERENCE_BOUNDS,candidateBounds=limits.CANDIDATE_BOUNDS,
        refinementFraction=.2,refinementFloorFraction=.02,releaseWaveformAbsolute=1e-9,calciumLedgerAbsolute=1e-9,
        momentumLedgerNewtonSeconds=1e-10,finestKineticLedgerJoules=1e-7,
        originalInitializationManifest=proof['manifestSha256'],originalGeometryDeclarations=original['geometryDeclarations'],
        allOriginalPeakForcesRetained=True,omittedFastUnits=53,prescribedArrivals=True,neuronalSourceExecuted=False,
        nativeUnityExecuted=False,anatomicalCalibrationQualified=False,avatarStandingQualified=False)


def reference_input(spec,case):
    protocol=dict(geometry=case['geometry'],parameterRows=spec['parameterRows'],
        maximumReferenceStepSeconds=spec['maximumReferenceStepSeconds'])
    scenario=dict(unitRows=list(range(147)),boundary=case['boundary'],durationMs=case['durationMs'],
        initialFiberLengthMeters=case['initialFiberLengthMeters'],initialMusculotendonLengthMeters=case['initialMusculotendonLengthMeters'],
        events=[dict(unitIndex=e['unitIndex'],arrivalAtMs=e['offsetMs']) for e in case['events']],
        lengthKnots=[dict(timeMs=k['offsetMs'],offsetMeters=k['lengthOffsetMeters']) for k in case['lengthKnots']])
    return protocol,scenario


def prepare(package):
    helper=module('population_state_preservation',ROOT/'tools/native_articulated_head_study.py')
    source=ROOT/'artifacts/muscle-population-initial-20261123-370cb9-review'
    helper.protected_copy(source,package/'population-initial-review',
        [e['Path'].replace('\\','/') for e in read(source/'backup-manifest.json')['Files']])
    write(package/'population-dynamics-input.json',make_input(package))
    write(package/'population-numerical-environment.json',environment())


def preflight(package):
    assert read(package/'population-dynamics-input.json')==make_input(package)
    assert read(package/'population-numerical-environment.json')==environment(),'Numerical dependencies changed.'


def run_stage(package,stage):
    preflight(package)
    study=module('population_trajectory_analysis',ROOT/'tools/analyse-muscle-population.py')
    if stage=='controls': study.controls(package)
    elif stage=='reference': study.solve(package)
    elif stage=='compare': study.compare(package)
    else: raise ValueError(stage)
