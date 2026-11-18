"""Freeze loaded-rest continuation; all model work belongs to its visible window."""
import argparse
from datetime import datetime,timezone
from decimal import localcontext
import json
from pathlib import Path,PurePosixPath
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile
from afferent_precision_gate import h,read,save
import afferent_endpoint_gate as previous
import afferent_rest_gate as resting
from afferent_loaded_protocol import protocol,ROOT_KEY
from cleft_trajectory_reference import base

ROOT=Path(__file__).resolve().parents[1]
PRESERVED=sorted((set(previous.PRESERVED)|set(previous.CHANGED)|set(resting.PRESERVED)|{
    'tools/afferent_trajectory_reference.py','tools/afferent_trajectory_analysis.py','tools/afferent_rest_analysis.py',
    'tools/afferent_precision_gate.py','tools/afferent_endpoint_gate.py'})-{'tools/NativeMotorBridgeProbe/Program.cs'})


def prepare(review_folder):
    assert not h.git('status','--porcelain'),'Commit first.'
    revision=h.git('rev-parse','HEAD');assert revision==h.git('rev-parse','origin/main'),'Push first.'
    assert shutil.disk_usage('D:/').free>=20*1024**3
    h.verify(review_folder,['review.json','analysis.json']);review=read(review_folder/'review.json');capture=Path(review['capture'])
    assert review['operatorReportedCompletion'] and review['passedTests']==333 and review['numericalTrajectoryCriteriaPassed']
    assert review['originalNativeReplayByteIdentical'] and review['spectrumControlsPassed']==8
    assert h.digest(capture/'backup-manifest.json')==review['manifestSha256']
    evidence=['trajectory-analysis.json','source-decisions.json','preparation-environment.json','managed-runtime-before.json']
    h.verify(capture,evidence+['source/'+n for n in PRESERVED])
    assert h.digest(capture/'trajectory-analysis.json')==review['analysisSha256']
    assert read(review_folder/'analysis.json')==read(capture/'trajectory-analysis.json')
    for n in PRESERVED:assert (ROOT/n).read_text(encoding='utf-8-sig')==h.io(capture/'source'/n).read_text(encoding='utf-8-sig'),n
    rr=ROOT/'artifacts/afferent-rest-20261118-62949f-audit-review';h.verify(rr,['review.json','rest-analysis.json'])
    rest=read(rr/'review.json');rc=Path(rest['capture'])
    assert rest['operatorReportedCompletion'] and rest['originalFailurePreserved'] and rest['exactRecordedEigenResidualsReproduced']
    assert rest['passedTests']==196 and rest['independentlyVerifiedNativeRoots']==12 and rest['independentRoots']==18
    assert not rest['numericalStationaryCriteriaPassed']
    assert h.digest(rc/'backup-manifest.json')==rest['manifestSha256']
    assert h.digest(rr/'rest-analysis.json')==rest['analysisSha256']
    paths=['rest-input.json',f'study/{ROOT_KEY}/result.json',f'native-witnesses/{ROOT_KEY}.json']
    h.verify(rc,paths)
    n=read(rc/paths[1]);w=read(rc/paths[2]);assert n['stationaryCriteriaPassed']
    assert w['completed'] and w['pointAvailable'] and w['physicalResidual']['passed'] and w['precision']['passed']
    assert w['nativeEvaluationComparison']['passed'] and all(a['passed'] for a in w['accounting'])
    assert next(c for c in read(rr/'rest-analysis.json')['cases'] if c['key']==ROOT_KEY)['nativeRootIndependentlyVerified']
    assert base.environment()==read(capture/'preparation-environment.json')
    package=Path('D:/DNNE-build-work')/('afferent-loaded-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6])
    backup=Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
    assert not package.exists() and not backup.exists();package.mkdir()
    subprocess.run(['git','archive','--format=zip','--output='+str(package/'source.zip'),revision],cwd=ROOT,check=True)
    with ZipFile(package/'source.zip') as z:
        for entry in z.infolist():
            name=PurePosixPath(entry.filename)
            assert not name.is_absolute() and '..' not in name.parts and ':' not in entry.filename and '\\' not in entry.filename
            assert ((entry.external_attr>>16)&0o170000)!=0o120000
            target=h.io(package/'source'/name)
            if entry.is_dir():target.mkdir(parents=True,exist_ok=True)
            else:
                target.parent.mkdir(parents=True,exist_ok=True)
                with target.open('xb') as stream:stream.write(z.read(entry))
    def copy(src,name):
        target=package/name;target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(src,target);assert h.digest(src)==h.digest(target)
    for name in evidence:copy(capture/name,'baseline/'+name)
    copy(review_folder/'review.json','reviewed-trajectories.json');copy(rr/'review.json','reviewed-rest.json')
    copy(rr/'rest-analysis.json','baseline/rest-analysis.json')
    for name in paths:copy(rc/name,'baseline/rest/'+name)
    copy(capture/'source-decisions.json','source-decisions.json');copy(capture/'preparation-environment.json','preparation-environment.json')
    copy(capture/'managed-runtime-before.json','qualified-managed-runtime.json')
    copy(package/'source/tools/backup-unity-run.ps1','backup-run.ps1')
    save(package/'trajectory-input.json',protocol(read(rc/paths[0]),n))
    save(package/'provenance.json',dict(restManifestSha256=rest['manifestSha256'],trajectoryManifestSha256=review['manifestSha256'],
        preservedSources=[dict(path=name,sha256=h.digest(capture/'source'/name)) for name in PRESERVED],
        restFiles=[dict(path=name,sha256=h.digest(rc/name)) for name in paths],componentTestsReused=333,rootTestsReused=196,
        rootKey=ROOT_KEY,savedNativeStationaryInitialStateUsed=True,nativeTrajectoryValuesSeedReferences=False,
        originalRestFailurePreserved=True,originalBoundsChanged=False,originalDefaultsChanged=False,
        localStabilityReusedForEndpointHold=False,modelsExecutedDuringPreparation=False,testsExecutedDuringPreparation=False))
    files=[dict(Path=p.relative_to(h.io(package)).as_posix(),Sha256=h.digest(p)) for p in sorted(h.io(package).rglob('*')) if p.is_file()]
    dotnet=shutil.which('dotnet');assert dotnet
    save(package/'configuration.json',dict(Revision=revision,PackageFiles=files,BackupDestination=str(backup),
        DotnetExecutable=dotnet,DotnetExecutableSha256=h.digest(dotnet),PythonExecutable=sys.executable,PythonExecutableSha256=h.digest(sys.executable),
        IsolatedLoadedRest=True,NoLiveMonitoring=True,ExpectedNativeRuns=12,ExpectedReferenceCases=12,ComponentTestsReused=333,
        LiveSelectionChanged=False,PhysiologyQualified=False,AvatarRecoveryAssessed=False,ArchiveCopiesShareDrive=True))
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,NoLiveMonitoring=True,
        Launcher=str(package/'source/tools/start-afferent-loaded-window.ps1'))
    save(ROOT/'artifacts'/(package.name+'-prepared.json'),pointer);print(json.dumps(pointer,indent=2),flush=True)


def preflight(package):
    cfg=read(package/'configuration.json');p=read(package/'provenance.json');review=read(package/'reviewed-trajectories.json');rest=read(package/'reviewed-rest.json')
    assert cfg['IsolatedLoadedRest'] and cfg['NoLiveMonitoring'] and cfg['ExpectedNativeRuns']==cfg['ExpectedReferenceCases']==12
    assert cfg['ComponentTestsReused']==p['componentTestsReused']==review['passedTests']==333
    assert review['operatorReportedCompletion'] and review['numericalTrajectoryCriteriaPassed'] and review['originalNativeReplayByteIdentical']
    assert h.digest(package/'baseline/trajectory-analysis.json')==review['analysisSha256']
    assert h.digest(package/'baseline/rest-analysis.json')==rest['analysisSha256']
    assert rest['operatorReportedCompletion'] and rest['originalFailurePreserved'] and p['originalRestFailurePreserved']
    assert rest['manifestSha256']==p['restManifestSha256'] and review['manifestSha256']==p['trajectoryManifestSha256']
    for row in p['restFiles']:assert h.digest(package/'baseline/rest'/row['path'])==row['sha256']
    assert read(package/'trajectory-input.json')==protocol(read(package/'baseline/rest/rest-input.json'),read(package/f'baseline/rest/study/{ROOT_KEY}/result.json'))
    assert {r['path'] for r in p['preservedSources']}==set(PRESERVED)
    for row in p['preservedSources']:assert h.digest(package/'source'/row['path'])==row['sha256'],row['path']
    assert read(package/'source-decisions.json')==read(package/'baseline/source-decisions.json')
    for row in cfg['PackageFiles']:assert h.digest(package/row['Path'])==row['Sha256'],row['Path']
    for tool in ('Dotnet','Python'):assert h.digest(cfg[tool+'Executable'])==cfg[tool+'ExecutableSha256']
    assert base.environment()==read(package/'preparation-environment.json')
    for directory in read(package/'qualified-managed-runtime.json')['directories']:
        root=h.io(directory['path']);assert {str(f) for f in root.rglob('*') if f.is_file()}=={str(h.io(r['path'])) for r in directory['files']}
        for row in directory['files']:assert h.digest(row['path'])==row['sha256']
    print('Reviewed sensory equations, saved resting state, original limits and dependencies preserved; no model/test executed.',flush=True)


def runtime(package):
    preflight(package);assert read(package/'managed-runtime-before.json')==read(package/'qualified-managed-runtime.json')


def storage(package):
    required=10*1024**3;available=shutil.disk_usage(package).free
    save(package/('storage-'+str(len(list(package.glob('storage-*.json')))+1)+'.json'),dict(requiredBytes=required,availableBytes=available,passed=available>=required))
    assert available>=required,'Preserve output and stop before a new phase; 10 GiB reserve required.'


def reference(package):
    runtime(package)
    from afferent_loaded_reference import Equation,run
    from afferent_numerical_protocol import protocol as derivative_protocol
    from afferent_numerical_analysis import strings,agreement,joint_conservation
    from cleft_rest_analysis import comparison
    spec=read(package/'trajectory-input.json');source=read(package/'source-decisions.json');derivative=derivative_protocol();rows=[]
    for case in spec['cases']:
        eq=Equation(case['fixture'],source,spec['referenceDecimalPrecision']);forcing=case['segments'][0]
        assert eq.input_at(eq.initial,forcing)==case['fixture']
        values=[eq.evaluation(eq.initial,forcing,p) for p in spec['referenceCheckPrecisions']]
        native=[read(package/'study'/f"{case['key']}-ticks{t}"/'initial-derivative.json') for t in spec['stepTicks']]
        assert all(v==native[0] for v in native)
        with localcontext() as ctx:
            ctx.prec=100
            checks=dict(key=case['key'],precision=comparison(values[0],values[1],derivative['referenceComparison']),
                candidate=agreement('joints',native[0],values[1],derivative),
                accounting=[joint_conservation(case['fixture'],v,derivative['conservation']).result() for v in [native[0]]+values])
        checks['passed']=checks['precision']['passed'] and checks['candidate']['passed'] and all(a['passed'] for a in checks['accounting'])
        rows.append(dict(key=case['key'],input=case['fixture'],precisions=spec['referenceCheckPrecisions'],references=strings(values),checks=checks))
    save(package/'initial-reference-checks.json',rows);assert all(r['checks']['passed'] for r in rows)
    run(package,spec,source)


def compare(package):
    runtime(package)
    from afferent_loaded_analysis import analyse,observations
    spec=read(package/'trajectory-input.json');source=read(package/'source-decisions.json')
    assert read(package/'study/activation-continuation.json')==dict(version=previous.MODEL,policy='EndpointHold',
        originalDefaultChanged=False,physiologicalCalibrationQualified=False)
    result=analyse(package,spec,source);save(package/'trajectory-analysis.json',result)
    observed=observations(package,spec,source);save(package/'rest-observations.json',observed)
    for row in result['cases']:print(row['key'],row['passed'],[r['error']['maximumAllowedFraction'] for r in row['candidates']],flush=True)
    assert result['numericalTrajectoryCriteriaPassed'],'Loaded-rest numerical criteria failed; retain all traces and original limits.'
    # Rest drift is a separate observation, never a reason to alter dynamics.
    print('Quiet drift within prospective limits: '+str(observed['quietDriftPassed']),flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser();parser.add_argument('command',choices=['prepare','preflight','postflight','runtime','storage','reference','compare']);parser.add_argument('path',type=Path)
    a=parser.parse_args();dict(prepare=prepare,preflight=preflight,postflight=preflight,runtime=runtime,storage=storage,reference=reference,compare=compare)[a.command](a.path.resolve())
