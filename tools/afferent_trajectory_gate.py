"""Freeze/run a fast joint trajectory comparison after reviewed component evidence."""
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
import afferent_dynamics_gate as component
from afferent_trajectory_protocol import protocol,validate,COUNTS

ROOT=Path(__file__).resolve().parents[1];h=component.h;read=component.read;save=component.save
inventory=component.prior.component.prior.component.inventory
PRESERVED=sorted(set(component.PRESERVED+['src/NRE.SimAvatar/AvatarVestibularAfferentDynamics.cs',
    'tests/NeuralResonanceEngine.DNNE.Tests/VestibularAfferentDynamicsTests.cs','tools/afferent_dynamics_gate.py',
    'tools/cleft_trajectory_reference.py','tools/cleft_trajectory_analysis.py','tools/cleft_trajectory_protocol.py',
    'tools/cleft_rest_analysis.py','tools/cleft_rest_protocol.py']))


def prepare(review_folder):
    assert not h.git('status','--porcelain'),'Commit first.'
    revision=h.git('rev-parse','HEAD');assert revision==h.git('rev-parse','origin/main'),'Push first.'
    assert shutil.disk_usage(ROOT).free>=4*1024**3 and shutil.disk_usage('D:/').free>=8*1024**3
    h.verify(review_folder,['review.json']);review=read(review_folder/'review.json')
    assert review['operatorReportedCompletion'] and review['componentCriteriaPassed'] and review['jointTimeEvolutionChecked']
    assert review['savedAnalysisExactlyReproduced'] and review['passedTests']==181
    capture=Path(review['capture']);assert h.digest(capture/'backup-manifest.json')==review['manifestSha256']
    evidence=['managed-runtime-before.json','preparation-environment.json','analysis.json','source-decisions.json','qualified-derivative-review.json']
    h.verify(capture,evidence+['source/'+name for name in PRESERVED]);assert h.digest(capture/'analysis.json')==review['analysisSha256']
    for name in PRESERVED:assert (ROOT/name).read_text(encoding='utf-8-sig')==h.io(capture/'source'/name).read_text(encoding='utf-8-sig'),name
    assert component.prior.component.prior.trajectory.base.environment()==read(capture/'preparation-environment.json')
    methods=inventory.inventory(ROOT,COUNTS);assert sum(methods.values())==181
    package=ROOT/'artifacts'/('afferent-trajectories-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6])
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
    for name in evidence:copy(capture/name,{'managed-runtime-before.json':'qualified-managed-runtime.json','analysis.json':'qualified-dynamics-analysis.json'}.get(name,name))
    copy(review_folder/'review.json','qualified-dynamics-review.json');copy(package/'source/tools/backup-unity-run.ps1','backup-run.ps1')
    save(package/'trajectory-input.json',protocol())
    save(package/'provenance.json',dict(originalRevision=review['sourceRevision'],originalManifestSha256=review['manifestSha256'],
        preservedSources=[dict(path=n,sha256=h.digest(capture/'source'/n)) for n in PRESERVED],
        sourceDecisionsSha256=h.digest(capture/'source-decisions.json'),pendingCapturesRead=False,
        originalBiologyChanged=False,modelsExecutedDuringPreparation=False,testsExecutedDuringPreparation=False))
    files=[dict(Path=p.relative_to(h.io(package)).as_posix(),Sha256=h.digest(p)) for p in sorted(h.io(package).rglob('*')) if p.is_file()]
    dotnet=shutil.which('dotnet');assert dotnet
    save(package/'configuration.json',dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),PackageFiles=files,
        DotnetExecutable=dotnet,DotnetExecutableSha256=h.digest(dotnet),PythonExecutable=sys.executable,PythonExecutableSha256=h.digest(sys.executable),
        BackupDestination=str(backup),IsolatedAfferentTrajectory=True,FastTransientOnly=True,NoLiveMonitoring=True,ExpectedTestCases=181,
        ExpectedClassCounts=COUNTS,ExpectedTestMethods=methods,TestFilter='|'.join('FullyQualifiedName~'+c for c in COUNTS),
        ExpectedNativeRuns=21,ExpectedReferences=21,IndependentCoupledTrajectoryQualified=False,StationaryInitializationQualified=False,
        SpatialGeometryCalibrated=False,AfferentFiringQualified=False,PhysiologyQualified=False,AvatarRecoveryAssessed=False,LiveSelectionChanged=False))
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,NoLiveMonitoring=True,Launcher=str(package/'source/tools/start-afferent-trajectories-window.ps1'))
    save(package.parent/(package.name+'-prepared.json'),pointer);print(json.dumps(pointer,indent=2),flush=True)


def prerequisites(package):
    spec=validate(read(package/'trajectory-input.json'));cfg=read(package/'configuration.json')
    review=read(package/'qualified-dynamics-review.json');provenance=read(package/'provenance.json')
    assert cfg['IsolatedAfferentTrajectory'] and cfg['FastTransientOnly'] and cfg['NoLiveMonitoring']
    assert cfg['ExpectedNativeRuns']==cfg['ExpectedReferences']==21 and cfg['ExpectedTestCases']==181 and cfg['ExpectedClassCounts']==COUNTS
    assert review['sourceRevision']==provenance['originalRevision'] and review['manifestSha256']==provenance['originalManifestSha256']
    assert review['operatorReportedCompletion'] and review['componentCriteriaPassed'] and review['jointTimeEvolutionChecked']
    assert review['savedAnalysisExactlyReproduced'] and review['passedTests']==181
    assert review['analysis']==read(package/'qualified-dynamics-analysis.json') and h.digest(package/'qualified-dynamics-analysis.json')==review['analysisSha256']
    assert not any(provenance[k] for k in ('pendingCapturesRead','originalBiologyChanged','modelsExecutedDuringPreparation','testsExecutedDuringPreparation'))
    assert {r['path'] for r in provenance['preservedSources']}==set(PRESERVED)
    for row in provenance['preservedSources']:assert h.digest(package/'source'/row['path'])==row['sha256']
    assert h.digest(package/'source-decisions.json')==provenance['sourceDecisionsSha256']
    prior=read(package/'qualified-derivative-review.json')
    assert prior['operatorReportedCompletion'] and prior['numericalCriteriaPassed'] and prior['savedAnalysisExactlyReproduced'] and prior['passedTests']==166
    assert component.prior.component.prior.trajectory.base.environment()==read(package/'preparation-environment.json')
    return spec


def preflight(package):
    prerequisites(package)
    for directory in read(package/'qualified-managed-runtime.json')['directories']:
        root=h.io(directory['path']);assert {str(p) for p in root.rglob('*') if p.is_file()}=={str(h.io(e['path'])) for e in directory['files']}
        for row in directory['files']:assert h.digest(row['path'])==row['sha256']
    print('Reviewed joint dynamics, earlier derivatives and numerical/runtime dependencies preserved; no model or test executed.',flush=True)


def runtime(package):
    preflight(package);assert read(package/'managed-runtime-before.json')==read(package/'qualified-managed-runtime.json')


def check_tests(package):
    prerequisites(package);print(json.dumps(inventory.check_tests(package,COUNTS),indent=2),flush=True)


def reference(package):
    spec=prerequisites(package);runtime(package);source=read(package/'source-decisions.json')
    from afferent_trajectory_reference import Equation,run
    from afferent_numerical_protocol import protocol as derivative_protocol
    from afferent_numerical_analysis import strings,agreement,joint_conservation
    from cleft_rest_analysis import comparison
    derivative_spec=derivative_protocol();witnesses=[]
    for case in spec['cases']:
        eq=Equation(case['fixture'],source,spec['referenceDecimalPrecision']);forcing=case['segments'][0]
        assert eq.input_at(eq.initial,forcing)==case['fixture']
        values=[eq.evaluation(eq.initial,forcing,p) for p in spec['referenceCheckPrecisions']]
        native=[read(package/'study'/f"{case['key']}-ticks{tick}"/'initial-derivative.json') for tick in spec['stepTicks']]
        assert all(v==native[0] for v in native)
        with localcontext() as context:
            context.prec=100
            checks=dict(key=case['key'],precision=comparison(values[0],values[1],derivative_spec['referenceComparison']),
                candidate=agreement('joints',native[0],values[1],derivative_spec),
                accounting=[joint_conservation(case['fixture'],v,derivative_spec['conservation']).result() for v in [native[0]]+values])
            checks['passed']=checks['precision']['passed'] and checks['candidate']['passed'] and all(a['passed'] for a in checks['accounting'])
        witnesses.append(dict(key=case['key'],input=case['fixture'],precisions=spec['referenceCheckPrecisions'],references=strings(values),checks=checks))
    save(package/'initial-reference-checks.json',witnesses)
    assert all(w['checks']['passed'] for w in witnesses),'Initial joint derivative comparison failed; witnesses retained.'
    run(package,spec,source)


def compare(package):
    spec=prerequisites(package);runtime(package);check_tests(package)
    from afferent_trajectory_analysis import analyse
    result=analyse(package,spec,read(package/'source-decisions.json'));save(package/'trajectory-analysis.json',result)
    for row in result['cases']:print(row['key'],row['passed'],[r['error']['maximumAllowedFraction'] for r in row['candidates']],flush=True)
    if not result['numericalTrajectoryCriteriaPassed']:raise RuntimeError('Fast joint sensory trajectories failed criteria; preserve all traces and original bounds.')


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser();parser.add_argument('command',choices=['prepare','preflight','postflight','runtime','check-tests','reference','compare']);parser.add_argument('path',type=Path)
    args=parser.parse_args();dict(prepare=prepare,preflight=preflight,postflight=preflight,runtime=runtime,reference=reference,compare=compare,
        **{'check-tests':check_tests})[args.command](args.path.resolve())
