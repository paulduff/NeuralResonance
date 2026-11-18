"""Freeze active resting-state diagnosis; numerical work only in its visible window."""
import argparse
from datetime import datetime,timezone
import json
from pathlib import Path,PurePosixPath
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile
import cleft_stationary_gate as component
import cleft_trajectory_gate as trajectory
from cleft_rest_protocol import protocol,validate,COUNTS

ROOT=Path(__file__).resolve().parents[1]
h=component.h;read=h.read;save=component.save
REVISION='fb61de2fd0af6b2ea01ec9b136c5e96fa0873415'
PRESERVED=sorted(set(component.PRESERVED+trajectory.PRESERVED+[
    'src/NRE.SimAvatar/AvatarCleftStationarySearch.cs',
    'tests/NeuralResonanceEngine.DNNE.Tests/CleftStationarySearchTests.cs',
    'tools/cleft_stationary_gate.py','tools/cleft_trajectory_protocol.py']))


def prepare(review_folder):
    assert not h.git('status','--porcelain'),'Commit first.'
    revision=h.git('rev-parse','HEAD');assert revision==h.git('rev-parse','origin/main'),'Push first.'
    # New root outputs are small; retain all prior evidence and reserve build space.
    assert shutil.disk_usage(ROOT).free>=4*1024**3 and shutil.disk_usage('D:/').free>=8*1024**3
    h.verify(review_folder,['review.json','trajectory-analysis.json']);review=read(review_folder/'review.json')
    assert review['sourceRevision']==REVISION and review['operatorReportedCompletion'] and review['savedAnalysisExactlyReproduced']
    assert review['passedTests']==132 and review['stationaryComponentCases']==12 and review['numericalCriteriaPassed']
    assert review['referenceCriteriaPassed'] and review['allSampledRefinementsPassed'] and review['allLedgerCriteriaPassed']
    capture=Path(review['capture']);assert h.digest(capture/'backup-manifest.json')==review['manifestSha256']
    dependencies=['managed-runtime-before.json','preparation-environment.json','trajectory-analysis.json']
    h.verify(capture,dependencies+['source/'+n for n in PRESERVED])
    for name in PRESERVED:assert (ROOT/name).read_text(encoding='utf-8-sig')==h.io(capture/'source'/name).read_text(encoding='utf-8-sig'),name
    assert trajectory.base.environment()==read(capture/'preparation-environment.json'),'Numerical dependencies changed.'
    methods=component.inventory.inventory(ROOT,COUNTS);assert sum(methods.values())==132
    package=ROOT/'artifacts'/('cleft-rest-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6])
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
    copy(review_folder/'review.json','qualified-refinement-review.json')
    copy(capture/'managed-runtime-before.json','qualified-managed-runtime.json')
    copy(capture/'preparation-environment.json','preparation-environment.json')
    copy(capture/'trajectory-analysis.json','qualified-trajectory-analysis.json')
    copy(package/'source/tools/backup-unity-run.ps1','backup-run.ps1')
    save(package/'rest-input.json',protocol())
    save(package/'provenance.json',dict(originalManifestSha256=review['manifestSha256'],originalRevision=REVISION,
        preservedSources=[dict(path=n,sha256=h.digest(capture/'source'/n)) for n in PRESERVED],
        originalBiologyChanged=False,modelsExecutedDuringPreparation=False,testsExecutedDuringPreparation=False,pendingCapturesRead=False))
    files=[dict(Path=p.relative_to(h.io(package)).as_posix(),Sha256=h.digest(p)) for p in sorted(h.io(package).rglob('*')) if p.is_file()]
    dotnet=shutil.which('dotnet');assert dotnet
    save(package/'configuration.json',dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),PackageFiles=files,
        DotnetExecutable=dotnet,DotnetExecutableSha256=h.digest(dotnet),PythonExecutable=sys.executable,PythonExecutableSha256=h.digest(sys.executable),
        BackupDestination=str(backup),IsolatedCleftRest=True,NoLiveMonitoring=True,ExpectedTestCases=132,ExpectedClassCounts=COUNTS,
        ExpectedTestMethods=methods,TestFilter='|'.join('FullyQualifiedName~'+c for c in COUNTS),ExpectedNativeSearches=18,ExpectedIndependentSearches=18,
        TimeIntegrated=False,PhysiologicalRestQualified=False,NonlinearStabilityProven=False,LiveSelectionChanged=False,
        SpatialGeometryCalibrated=False,AfferentFiringQualified=False,PhysiologyQualified=False,AvatarRecoveryAssessed=False))
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,NoLiveMonitoring=True,Launcher=str(package/'source/tools/start-cleft-rest-window.ps1'))
    save(package.parent/(package.name+'-prepared.json'),pointer);print(json.dumps(pointer,indent=2),flush=True)


def prerequisites(package):
    spec=validate(read(package/'rest-input.json'));cfg=read(package/'configuration.json')
    review=read(package/'qualified-refinement-review.json');provenance=read(package/'provenance.json')
    assert cfg['IsolatedCleftRest'] and cfg['NoLiveMonitoring'] and cfg['ExpectedNativeSearches']==cfg['ExpectedIndependentSearches']==18
    assert cfg['ExpectedTestCases']==132 and cfg['ExpectedClassCounts']==COUNTS
    assert review['sourceRevision']==provenance['originalRevision']==REVISION
    assert review['manifestSha256']==provenance['originalManifestSha256']
    assert review['operatorReportedCompletion'] and review['savedAnalysisExactlyReproduced'] and review['numericalCriteriaPassed']
    assert review['passedTests']==132 and review['stationaryComponentCases']==12
    assert review['analysis']==read(package/'qualified-trajectory-analysis.json')
    assert not any(provenance[n] for n in ('originalBiologyChanged','modelsExecutedDuringPreparation','testsExecutedDuringPreparation','pendingCapturesRead'))
    assert {r['path'] for r in provenance['preservedSources']}==set(PRESERVED)
    for row in provenance['preservedSources']:assert h.digest(package/'source'/row['path'])==row['sha256']
    assert trajectory.base.environment()==read(package/'preparation-environment.json')
    return spec


def preflight(package):
    prerequisites(package);save(package/'reference-preflight.json',dict(environment=trajectory.base.environment(),modelsExecuted=False,testsExecuted=False))


def runtime(package):
    prerequisites(package);expected=read(package/'qualified-managed-runtime.json')
    assert read(package/'managed-runtime-before.json')==expected
    for directory in expected['directories']:
        root=h.io(directory['path']);assert {str(p) for p in root.rglob('*') if p.is_file()}=={str(h.io(e['path'])) for e in directory['files']}
        for row in directory['files']:assert h.digest(row['path'])==row['sha256']
    print('Reviewed stationary kernel, sensory equations and runtimes preserved; no model or test executed.',flush=True)


def references(package):
    spec=prerequisites(package);runtime(package)
    from cleft_rest_reference import run,native_witnesses
    run(package,spec);native_witnesses(package,spec)


def compare(package):
    prerequisites(package);runtime(package);component.inventory.check_tests(package,COUNTS)
    from cleft_rest_analysis import analyse
    analyse(package)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    p=argparse.ArgumentParser();p.add_argument('command',choices=['prepare','preflight','postflight','runtime','check-tests','references','compare']);p.add_argument('path',type=Path)
    a=p.parse_args();dict(prepare=prepare,preflight=preflight,postflight=trajectory.base.postflight,runtime=runtime,
        references=references,compare=compare,**{'check-tests':component.check_tests})[a.command](a.path.resolve())
