"""Frozen independent trajectory workflow; models/tests run only in its visible window."""
import argparse
from datetime import datetime,timezone
from decimal import Decimal,localcontext
import json
from pathlib import Path,PurePosixPath
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile
import cleft_dynamics_gate as component
from cleft_trajectory_protocol import protocol,validate,COUNTS
from cleft_trajectory_reference import Equation,base,run as references
from cleft_trajectory_analysis import analyse

ROOT=Path(__file__).resolve().parents[1];h=component.h;read=h.read;save=component.save
REVISION='b4f0d57fa93fab7f935547345d8e078a05270608'
PRESERVED=component.PRESERVED+['src/NRE.SimAvatar/AvatarVestibularCleftDynamics.cs',
    'tests/NeuralResonanceEngine.DNNE.Tests/VestibularCleftDynamicsTests.cs']
PRESERVED+=['tools/'+name for name in ('cleft_membrane_reference.py','cleft_open_reference.py','cleft_spatial_reference.py',
    'analyse-cleft-coupling.py','cleft_membrane_protocol.py','cleft_dynamics_gate.py','NativeMotorBridgeProbe/CleftMembraneStudy.cs','golgi-reference-resume.py')]


def prepare(review_folder):
    assert not h.git('status','--porcelain'),'Commit first.'
    revision=h.git('rev-parse','HEAD');assert revision==h.git('rev-parse','origin/main'),'Push first.'
    h.verify(review_folder,['review.json']);review=read(review_folder/'review.json')
    assert review['operatorReportedCompletion'] and review['componentCriteriaPassed'] and review['savedAnalysisExactlyReproduced']
    assert review['sourceRevision']==REVISION and review['passedTests']==120
    capture=Path(review['capture']);assert h.digest(capture/'backup-manifest.json')==review['manifestSha256']
    h.verify(capture,['source/'+name for name in PRESERVED]+['managed-runtime-before.json','qualified-membrane-review.json'])
    for name in PRESERVED:assert (ROOT/name).read_text(encoding='utf-8-sig')==h.io(capture/'source'/name).read_text(encoding='utf-8-sig'),name
    prior=read(capture/'qualified-membrane-review.json');membrane=Path(prior['capture'])
    assert prior['numericalCriteriaPassed'] and prior['passedTests']==108 and prior['savedAnalysisExactlyReproduced']
    assert h.digest(membrane/'backup-manifest.json')==prior['manifestSha256']
    evidence_root=h.io(membrane/'source-evidence')
    evidence=['source-evidence/'+p.relative_to(evidence_root).as_posix() for p in evidence_root.rglob('*') if p.is_file()]
    h.verify(membrane,['preparation-environment.json']+evidence)
    environment=base.environment();assert environment==read(membrane/'preparation-environment.json'),'Review changed numerical dependencies explicitly.'
    methods=component.inventory.inventory(ROOT,COUNTS);assert sum(methods.values())==120
    package=ROOT/'artifacts'/('cleft-trajectories-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6])
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
    for name in PRESERVED:assert h.digest(package/'source'/name)==h.digest(capture/'source'/name),name
    def copy(source,name):
        target=h.io(package/name);target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(h.io(source),target)
        assert h.digest(source)==h.digest(target)
    copy(review_folder/'review.json','qualified-dynamics-review.json')
    copy(capture/'qualified-membrane-review.json','qualified-membrane-review.json')
    copy(capture/'managed-runtime-before.json','qualified-managed-runtime.json')
    for name in ['preparation-environment.json']+evidence:copy(membrane/name,name)
    copy(package/'source/tools/backup-unity-run.ps1','backup-run.ps1');save(package/'trajectory-input.json',protocol())
    save(package/'provenance.json',dict(qualifiedCaptureManifestSha256=review['manifestSha256'],qualifiedSourceRevision=REVISION,
        preservedSources=[dict(path=name,sha256=h.digest(capture/'source'/name)) for name in PRESERVED],
        qualifiedManagedRuntimeSha256=h.digest(capture/'managed-runtime-before.json'),
        numericalRuntimeMatchesReviewedMembranes=True,modelsExecutedDuringPreparation=False,testsExecutedDuringPreparation=False,pendingCapturesRead=False))
    files=[dict(Path=p.relative_to(h.io(package)).as_posix(),Sha256=h.digest(p)) for p in sorted(h.io(package).rglob('*')) if p.is_file()]
    dotnet=shutil.which('dotnet');assert dotnet
    save(package/'configuration.json',dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),PackageFiles=files,
        DotnetExecutable=dotnet,DotnetExecutableSha256=h.digest(dotnet),PythonExecutable=sys.executable,PythonExecutableSha256=h.digest(sys.executable),
        BackupDestination=str(backup),IsolatedCleftTrajectory=True,NoLiveMonitoring=True,ExpectedTestCases=120,ExpectedClassCounts=COUNTS,
        ExpectedTestMethods=methods,TestFilter='|'.join('FullyQualifiedName~'+c for c in COUNTS),ExpectedNativeRuns=21,ExpectedReferences=21,
        IndependentCoupledTrajectoryQualified=False,StationaryInitializationQualified=False,LiveSelectionChanged=False,
        SpatialGeometryCalibrated=False,AfferentFiringQualified=False,PhysiologyQualified=False,AvatarRecoveryAssessed=False))
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,NoLiveMonitoring=True,Launcher=str(package/'source/tools/start-cleft-trajectories-window.ps1'))
    save(package.parent/(package.name+'-prepared.json'),pointer);print(json.dumps(pointer,indent=2),flush=True)


def prerequisites(package):
    p=validate(read(package/'trajectory-input.json'));cfg=read(package/'configuration.json');review=read(package/'qualified-dynamics-review.json')
    provenance=read(package/'provenance.json')
    assert cfg['IsolatedCleftTrajectory'] and cfg['NoLiveMonitoring'] and cfg['ExpectedNativeRuns']==cfg['ExpectedReferences']==21
    assert cfg['ExpectedTestCases']==120 and cfg['ExpectedClassCounts']==COUNTS
    assert review['sourceRevision']==provenance['qualifiedSourceRevision']==REVISION and review['passedTests']==120
    assert review['operatorReportedCompletion'] and review['componentCriteriaPassed'] and review['savedAnalysisExactlyReproduced']
    assert review['manifestSha256']==provenance['qualifiedCaptureManifestSha256']
    assert not provenance['modelsExecutedDuringPreparation'] and not provenance['testsExecutedDuringPreparation'] and provenance['numericalRuntimeMatchesReviewedMembranes']
    assert {r['path'] for r in provenance['preservedSources']}==set(PRESERVED)
    for row in provenance['preservedSources']:assert h.digest(package/'source'/row['path'])==row['sha256']
    assert h.digest(package/'qualified-managed-runtime.json')==provenance['qualifiedManagedRuntimeSha256']
    assert base.environment()==read(package/'preparation-environment.json'),'Numerical dependencies changed.'
    return p


def preflight(package):
    prerequisites(package);save(package/'reference-preflight.json',dict(environment=base.environment(),modelsExecuted=False,testsExecuted=False))


def runtime(package):
    prerequisites(package);expected=read(package/'qualified-managed-runtime.json')
    assert read(package/'managed-runtime-before.json')==expected
    for directory in expected['directories']:
        root=h.io(directory['path']);assert {str(p) for p in root.rglob('*') if p.is_file()}=={str(h.io(e['path'])) for e in directory['files']}
        for entry in directory['files']:assert h.digest(entry['path'])==entry['sha256']
    print('Exact reviewed coupled-cell runtime preserved; no model/test executed.',flush=True)


def reference(package):
    p=prerequisites(package);runtime(package)
    assert read(package/'study/results.json')['completed']
    witnesses=[]
    for case in p['cases']:
        eq=Equation(case['fixture'],p['referenceDecimalPrecision']);forcing=case['segments'][0]
        results=[eq.evaluation(eq.initial,forcing,precision) for precision in p['referenceCheckPrecisions']]
        with localcontext() as context:
            context.prec=90
            base.compare_values(results[0],results[1],Decimal('1e-40'),Decimal('1e-40'))
            records=[read(package/'study'/f"{case['key']}-ticks{tick}"/'initial-derivative.json') for tick in p['stepTicks']]
            assert all(r==records[0] for r in records)
            ratio=base.compare_values(records[0],results[1],Decimal('2e-10'),Decimal('2e-12'))
        witnesses.append(dict(key=case['key'],maximumAllowedFraction=str(ratio),references=[base.decimal_strings(r) for r in results]))
    save(package/'initial-reference-checks.json',witnesses)
    references(package,p)


def compare(package):
    p=prerequisites(package);runtime(package);component.inventory.check_tests(package,COUNTS);analyse(package,p)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser();parser.add_argument('command',choices=['prepare','preflight','runtime','check-tests','reference','compare','postflight']);parser.add_argument('path',type=Path)
    a=parser.parse_args();dict(prepare=prepare,preflight=preflight,runtime=runtime,reference=reference,compare=compare,
        postflight=base.postflight,**{'check-tests':component.check_tests})[a.command](a.path.resolve())
