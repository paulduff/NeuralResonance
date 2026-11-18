"""Explicit alternate activation continuation; preserve and replay original v1."""
import argparse
from copy import deepcopy
from datetime import datetime,timezone
from decimal import localcontext
import json
from pathlib import Path,PurePosixPath
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile
import afferent_precision_gate as precision
import vestibular_bundle_gate as bundle
from cleft_trajectory_reference import base

ROOT=Path(__file__).resolve().parents[1]
h=precision.h;read=precision.read;save=precision.save;inventory=precision.inventory
COUNTS=dict(bundle.COUNTS,AfferentActivationContinuationTests=15)
CHANGED=['src/NRE.SimAvatar/AvatarVestibularAfferentMembrane.cs','src/NRE.SimAvatar/AvatarVestibularAfferentCable.cs',
    'tools/afferent_numerical_reference.py','tools/NativeMotorBridgeProbe/AfferentTrajectoryStudy.cs','tools/NativeMotorBridgeProbe/Program.cs']
PRESERVED=sorted((set(precision.PRESERVED)|set(bundle.PRESERVED))-set(CHANGED))
MODEL='vestibular-afferent-may14-endpoint-hold-candidate-v1'


def decisions(original):
    result=deepcopy(original)
    result.update(modelVersion=MODEL,originalModelVersion=original['modelVersion'],activationOutsidePolicy='endpoint-hold',
        originalActivationOutside=original['activationOutside'],activationOutside='nearest endpoint: 0.26131 ms below -55 mV; 0.21091 ms above 60 mV',
        continuationAssumption='Hold nearest activation table endpoint outside [-55,60] mV; original knots and all other expressions unchanged.',
        physiologicalCalibrationQualified=False)
    return result


def prepare(review_folder):
    assert not h.git('status','--porcelain'),'Commit first.'
    revision=h.git('rev-parse','HEAD');assert revision==h.git('rev-parse','origin/main'),'Push first.'
    assert shutil.disk_usage(ROOT).free>=6*1024**3 and shutil.disk_usage('D:/').free>=8*1024**3
    h.verify(review_folder,['review.json']);review=read(review_folder/'review.json')
    assert review['operatorReportedCompletion'] and review['savedAnalysisExactlyReproduced'] and review['originalFailurePreserved']
    assert not review['numericalTrajectoryCriteriaPassed'] and review['passedTests']==181 and review['spectrumControlsPassed']==8
    capture=Path(review['capture']);assert h.digest(capture/'backup-manifest.json')==review['manifestSha256']
    bundle_review=ROOT/'artifacts/vestibular-bundle-20261118-092238-review'
    h.verify(bundle_review,['review.json']);br=read(bundle_review/'review.json');bc=Path(br['capture'])
    assert br['operatorReportedCompletion'] and br['savedAnalysisExactlyReproduced'] and br['bundleInputChecksPassed'] and br['passedTests']==317
    assert h.digest(bc/'backup-manifest.json')==br['manifestSha256']
    h.verify(bc,['source/'+n for n in PRESERVED])
    for n in PRESERVED:assert (ROOT/n).read_text(encoding='utf-8-sig')==h.io(bc/'source'/n).read_text(encoding='utf-8-sig'),n
    evidence=['trajectory-input.json','trajectory-analysis.json','initial-reference-checks.json','source-decisions.json',
        'managed-runtime-before.json','preparation-environment.json']
    spec=read(capture/'trajectory-input.json');assert spec==precision.protocol()
    replay=['study/input.json','study/results.json']
    for case in spec['cases']:
        for tick in spec['stepTicks']:
            replay+=['study/'+case['key']+'-ticks'+str(tick)+'/'+n for n in ('initial.json','final.json','initial-derivative.json','trajectory.csv')]
    h.verify(capture,evidence+replay+['source/'+n for n in CHANGED])
    assert base.environment()==read(capture/'preparation-environment.json')
    assert h.digest(capture/'trajectory-analysis.json')==review['analysisSha256']
    methods=inventory.inventory(ROOT,COUNTS);assert sum(methods.values())==332
    package=ROOT/'artifacts'/('afferent-endpoint-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6])
    backup=Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
    assert not package.exists() and not backup.exists();package.mkdir()
    subprocess.run(['git','archive','--format=zip','--output='+str(package/'source.zip'),revision],cwd=ROOT,check=True)
    with ZipFile(package/'source.zip') as archive:
        for entry in archive.infolist():
            name=PurePosixPath(entry.filename)
            assert not name.is_absolute() and '..' not in name.parts and ':' not in entry.filename and '\\' not in entry.filename
            assert ((entry.external_attr>>16)&0o170000)!=0o120000
            target=h.io(package/'source'/name)
            if entry.is_dir():target.mkdir(parents=True,exist_ok=True)
            else:
                target.parent.mkdir(parents=True,exist_ok=True)
                with target.open('xb') as stream:stream.write(archive.read(entry))
    def copy(source,name):
        target=h.io(package/name);target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(h.io(source),target)
        assert h.digest(source)==h.digest(target)
    for n in evidence:copy(capture/n,'baseline/'+n)
    for n in replay:copy(capture/n,'baseline/'+n)
    for n in CHANGED:copy(capture/'source'/n,'baseline/source/'+n)
    for n in ('trajectory-input.json','preparation-environment.json'):copy(capture/n,n)
    copy(capture/'managed-runtime-before.json','qualified-managed-runtime.json')
    copy(review_folder/'review.json','reviewed-precision.json');copy(bundle_review/'review.json','reviewed-bundle.json')
    copy(package/'source/tools/backup-unity-run.ps1','backup-run.ps1')
    save(package/'source-decisions.json',decisions(read(capture/'source-decisions.json')))
    save(package/'provenance.json',dict(originalRevision=review['sourceRevision'],originalManifestSha256=review['manifestSha256'],
        bundleRevision=br['sourceRevision'],bundleManifestSha256=br['manifestSha256'],
        preservedSources=[dict(path=n,sha256=h.digest(bc/'source'/n)) for n in PRESERVED],
        originalReplayFiles=[dict(path=n,sha256=h.digest(capture/n)) for n in replay],
        changedSources=CHANGED,candidateModel=MODEL,originalBoundsChanged=False,originalDefaultsChanged=False,
        newKineticAssumption=True,modelsExecutedDuringPreparation=False,testsExecutedDuringPreparation=False))
    files=[dict(Path=p.relative_to(h.io(package)).as_posix(),Sha256=h.digest(p)) for p in sorted(h.io(package).rglob('*')) if p.is_file()]
    dotnet=shutil.which('dotnet');assert dotnet
    save(package/'configuration.json',dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),PackageFiles=files,
        DotnetExecutable=dotnet,DotnetExecutableSha256=h.digest(dotnet),PythonExecutable=sys.executable,PythonExecutableSha256=h.digest(sys.executable),
        BackupDestination=str(backup),IsolatedAfferentEndpoint=True,FastTransientOnly=True,NoLiveMonitoring=True,ExpectedTestCases=332,
        ExpectedClassCounts=COUNTS,ExpectedTestMethods=methods,TestFilter='|'.join('FullyQualifiedName~'+c for c in COUNTS),
        ExpectedNativeRuns=21,ExpectedOriginalReplayRuns=21,ExpectedReferences=21,ExpectedSpectrumControls=8,
        IndependentCoupledTrajectoryQualified=False,StationaryInitializationQualified=False,SpatialGeometryCalibrated=False,
        AfferentFiringQualified=False,PhysiologyQualified=False,AvatarRecoveryAssessed=False,LiveSelectionChanged=False))
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,NoLiveMonitoring=True,Launcher=str(package/'source/tools/start-afferent-endpoint-window.ps1'))
    save(package.parent/(package.name+'-prepared.json'),pointer);print(json.dumps(pointer,indent=2),flush=True)


def prerequisites(package):
    cfg=read(package/'configuration.json');v=read(package/'provenance.json');review=read(package/'reviewed-precision.json');br=read(package/'reviewed-bundle.json')
    assert cfg['IsolatedAfferentEndpoint'] and cfg['NoLiveMonitoring'] and cfg['FastTransientOnly']
    assert cfg['ExpectedTestCases']==332 and cfg['ExpectedClassCounts']==COUNTS
    assert cfg['ExpectedNativeRuns']==cfg['ExpectedReferences']==cfg['ExpectedOriginalReplayRuns']==21
    assert cfg['ExpectedSpectrumControls']==8 and v['candidateModel']==MODEL and v['newKineticAssumption']
    assert review['operatorReportedCompletion'] and review['savedAnalysisExactlyReproduced'] and review['originalFailurePreserved']
    assert review['sourceRevision']==v['originalRevision'] and review['manifestSha256']==v['originalManifestSha256']
    assert h.digest(package/'baseline/trajectory-analysis.json')==review['analysisSha256']
    assert br['operatorReportedCompletion'] and br['bundleInputChecksPassed'] and br['passedTests']==317
    assert br['sourceRevision']==v['bundleRevision'] and br['manifestSha256']==v['bundleManifestSha256']
    spec=read(package/'trajectory-input.json');assert spec==precision.protocol()==read(package/'baseline/trajectory-input.json')
    assert read(package/'source-decisions.json')==decisions(read(package/'baseline/source-decisions.json'))
    assert {r['path'] for r in v['preservedSources']}==set(PRESERVED) and v['changedSources']==CHANGED
    for r in v['preservedSources']:assert h.digest(package/'source'/r['path'])==r['sha256'],r['path']
    for k in ('originalBoundsChanged','originalDefaultsChanged','modelsExecutedDuringPreparation','testsExecutedDuringPreparation'):assert v[k] is False
    assert base.environment()==read(package/'preparation-environment.json')
    return spec


def preflight(package):
    prerequisites(package)
    for r in read(package/'provenance.json')['originalReplayFiles']:assert h.digest(package/'baseline'/r['path'])==r['sha256']
    for directory in read(package/'qualified-managed-runtime.json')['directories']:
        root=h.io(directory['path']);assert {str(p) for p in root.rglob('*') if p.is_file()}=={str(h.io(e['path'])) for e in directory['files']}
        for row in directory['files']:assert h.digest(row['path'])==row['sha256']
    print('Original failure, native replay controls, unchanged limits and numerical/runtime dependencies preserved; no model or test executed.',flush=True)


def runtime(package):
    preflight(package);assert read(package/'managed-runtime-before.json')==read(package/'qualified-managed-runtime.json')


def check_tests(package):
    prerequisites(package);print(json.dumps(inventory.check_tests(package,COUNTS),indent=2),flush=True)


def controls(package):
    preflight(package)
    import check_afferent_spectrum as control
    control.run(package/'spectrum-controls.json')


def replay(package):
    prerequisites(package);runtime(package)
    rows=[]
    expected=read(package/'provenance.json')['originalReplayFiles']
    actual={p.relative_to(package/'original-study').as_posix() for p in (package/'original-study').rglob('*') if p.is_file()}
    assert actual=={str(PurePosixPath(r['path']).relative_to('study')) for r in expected}
    for r in expected:
        path=package/'original-study'/PurePosixPath(r['path']).relative_to('study')
        rows.append(dict(path=r['path'],expectedSha256=r['sha256'],actualSha256=h.digest(path)))
    result=dict(completed=True,files=rows,originalNativeReplayByteIdentical=all(r['expectedSha256']==r['actualSha256'] for r in rows))
    save(package/'original-replay.json',result)
    assert result['originalNativeReplayByteIdentical'],'Original v1 replay changed; retained outputs, do not proceed.'
    print('All 21 original native trajectories, fingerprints, initial derivatives and final records replayed byte for byte.',flush=True)


def reference(package):
    spec=prerequisites(package);runtime(package)
    assert read(package/'original-replay.json')['originalNativeReplayByteIdentical']
    from afferent_trajectory_reference import Equation,run
    from afferent_numerical_protocol import protocol as derivative_protocol
    from afferent_numerical_analysis import strings,agreement,joint_conservation
    from cleft_rest_analysis import comparison
    source=read(package/'source-decisions.json');old=read(package/'baseline/source-decisions.json');derivative_spec=derivative_protocol()
    previous={r['key']:r for r in read(package/'baseline/initial-reference-checks.json')};witnesses=[];legacy=[]
    for case in spec['cases']:
        eq=Equation(case['fixture'],source,spec['referenceDecimalPrecision']);forcing=case['segments'][0]
        assert eq.input_at(eq.initial,forcing)==case['fixture']
        original=Equation(case['fixture'],old,spec['referenceDecimalPrecision'])
        original_values=[original.evaluation(original.initial,forcing,p) for p in spec['referenceCheckPrecisions']]
        legacy.append(dict(key=case['key'],exactOriginalDerivatives=strings(original_values)==previous[case['key']]['references']))
        values=[eq.evaluation(eq.initial,forcing,p) for p in spec['referenceCheckPrecisions']]
        native=[read(package/'study'/f"{case['key']}-ticks{t}"/'initial-derivative.json') for t in spec['stepTicks']]
        assert all(n==native[0] for n in native)
        for t in spec['stepTicks']:
            before=read(package/'original-study'/f"{case['key']}-ticks{t}"/'initial.json');after=read(package/'study'/f"{case['key']}-ticks{t}"/'initial.json')
            assert before['fingerprint']!=after['fingerprint']
            for k in ('values','ledgers','stateLabels','ledgerLabels','settings','sessionId','cellId'):assert before[k]==after[k],k
        with localcontext() as context:
            context.prec=100
            checks=dict(key=case['key'],precision=comparison(values[0],values[1],derivative_spec['referenceComparison']),
                candidate=agreement('joints',native[0],values[1],derivative_spec),
                accounting=[joint_conservation(case['fixture'],v,derivative_spec['conservation']).result() for v in [native[0]]+values])
            checks['passed']=checks['precision']['passed'] and checks['candidate']['passed'] and all(a['passed'] for a in checks['accounting'])
        witnesses.append(dict(key=case['key'],input=case['fixture'],precisions=spec['referenceCheckPrecisions'],references=strings(values),checks=checks))
    save(package/'original-reference-replay.json',legacy);save(package/'initial-reference-checks.json',witnesses)
    assert all(w['exactOriginalDerivatives'] for w in legacy),'Original independent derivative changed.'
    assert all(w['checks']['passed'] for w in witnesses),'Candidate initial derivative failed; witnesses retained.'
    run(package,spec,source)


def compare(package):
    spec=prerequisites(package);runtime(package);check_tests(package)
    assert read(package/'original-replay.json')['originalNativeReplayByteIdentical']
    assert all(w['exactOriginalDerivatives'] for w in read(package/'original-reference-replay.json'))
    assert read(package/'spectrum-controls.json')==dict(completed=True,passed=8,expected=8,modelsExecuted=False)
    assert read(package/'study/activation-continuation.json')==dict(version=MODEL,policy='EndpointHold',originalDefaultChanged=False,physiologicalCalibrationQualified=False)
    from afferent_trajectory_analysis import analyse
    result=analyse(package,spec,read(package/'source-decisions.json'));save(package/'trajectory-analysis.json',result)
    for row in result['cases']:print(row['key'],row['passed'],[r['error']['maximumAllowedFraction'] for r in row['candidates']],flush=True)
    if not result['numericalTrajectoryCriteriaPassed']:raise RuntimeError('Explicit endpoint candidate failed original trajectory bounds; retain both model outcomes.')


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    p=argparse.ArgumentParser();p.add_argument('command',choices=['prepare','preflight','postflight','runtime','check-tests','controls','replay','reference','compare']);p.add_argument('path',type=Path)
    a=p.parse_args();dict(prepare=prepare,preflight=preflight,postflight=preflight,runtime=runtime,controls=controls,replay=replay,reference=reference,compare=compare,
        **{'check-tests':check_tests})[a.command](a.path.resolve())
