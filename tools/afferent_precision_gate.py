"""Tighter solver tolerances on the unchanged short sensory trajectory problem."""
import argparse
from copy import deepcopy
from datetime import datetime,timezone
import json
from pathlib import Path,PurePosixPath
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile
import afferent_trajectory_gate as original
from afferent_trajectory_protocol import protocol as original_protocol
from cleft_trajectory_reference import base

ROOT=Path(__file__).resolve().parents[1]
h=original.h;read=original.read;save=original.save;inventory=original.inventory;COUNTS=original.COUNTS
PRESERVED=sorted(set(original.PRESERVED+['tools/'+n for n in (
    'afferent_trajectory_protocol.py','afferent_trajectory_analysis.py','afferent_trajectory_reference.py',
    'NativeMotorBridgeProbe/AfferentTrajectoryStudy.cs')]))


def protocol():
    # Same protocol schema, cells, stimuli, physical clock and acceptance limits.
    # Only the configured solve tolerances change; they enter native fingerprints.
    p=deepcopy(original_protocol())
    p['nonlinearSettings'].update(voltageAbsoluteToleranceMv=1e-12,ionAbsoluteToleranceMm=1e-14,
        gateAbsoluteTolerance=1e-15,relativeTolerance=1e-14)
    p['referenceSettings']=[dict(key='radau-standard',method='Radau',rtol=2e-11,atolScale=.01),
        dict(key='radau-tight',method='Radau',rtol=2e-13,atolScale=.0001),
        dict(key='bdf-tight',method='BDF',rtol=2e-13,atolScale=.0001)]
    return p


def prepare(review_folder):
    assert not h.git('status','--porcelain'),'Commit first.'
    revision=h.git('rev-parse','HEAD');assert revision==h.git('rev-parse','origin/main'),'Push first.'
    assert shutil.disk_usage(ROOT).free>=4*1024**3 and shutil.disk_usage('D:/').free>=8*1024**3
    h.verify(review_folder,['review.json']);review=read(review_folder/'review.json')
    assert review['operatorReportedCompletion'] and review['savedAnalysisExactlyReproduced']
    assert review['originalFailurePreserved'] and not review['numericalTrajectoryCriteriaPassed'] and review['passedTests']==181
    capture=Path(review['capture']);assert h.digest(capture/'backup-manifest.json')==review['manifestSha256']
    evidence=['trajectory-input.json','trajectory-analysis.json','initial-reference-checks.json',
        'managed-runtime-before.json','preparation-environment.json','source-decisions.json']
    h.verify(capture,evidence+['source/'+n for n in PRESERVED]);assert h.digest(capture/'trajectory-analysis.json')==review['analysisSha256']
    for n in PRESERVED:assert (ROOT/n).read_text(encoding='utf-8-sig')==h.io(capture/'source'/n).read_text(encoding='utf-8-sig'),n
    assert read(capture/'trajectory-input.json')==original_protocol()
    assert base.environment()==read(capture/'preparation-environment.json')
    methods=inventory.inventory(ROOT,COUNTS);assert sum(methods.values())==181
    package=ROOT/'artifacts'/('afferent-precision-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6])
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
    for name in evidence:
        destination={'trajectory-input.json':'baseline/trajectory-input.json','trajectory-analysis.json':'baseline/trajectory-analysis.json',
            'managed-runtime-before.json':'qualified-managed-runtime.json'}.get(name,name)
        copy(capture/name,destination)
    copy(review_folder/'review.json','reviewed-trajectory.json')
    copy(package/'source/tools/backup-unity-run.ps1','backup-run.ps1')
    save(package/'trajectory-input.json',protocol())
    save(package/'provenance.json',dict(originalRevision=review['sourceRevision'],originalManifestSha256=review['manifestSha256'],
        preservedSources=[dict(path=n,sha256=h.digest(capture/'source'/n)) for n in PRESERVED],
        sourceDecisionsSha256=h.digest(capture/'source-decisions.json'),initialWitnessesSha256=h.digest(capture/'initial-reference-checks.json'),
        pendingCapturesRead=False,originalBoundsChanged=False,originalBiologyChanged=False,
        modelsExecutedDuringPreparation=False,testsExecutedDuringPreparation=False))
    files=[dict(Path=p.relative_to(h.io(package)).as_posix(),Sha256=h.digest(p)) for p in sorted(h.io(package).rglob('*')) if p.is_file()]
    dotnet=shutil.which('dotnet');assert dotnet
    save(package/'configuration.json',dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),PackageFiles=files,
        DotnetExecutable=dotnet,DotnetExecutableSha256=h.digest(dotnet),PythonExecutable=sys.executable,PythonExecutableSha256=h.digest(sys.executable),
        BackupDestination=str(backup),IsolatedAfferentPrecision=True,FastTransientOnly=True,NoLiveMonitoring=True,ExpectedTestCases=181,
        ExpectedClassCounts=COUNTS,ExpectedTestMethods=methods,TestFilter='|'.join('FullyQualifiedName~'+c for c in COUNTS),
        ExpectedNativeRuns=21,ExpectedReferences=21,ExpectedSpectrumControls=8,IndependentCoupledTrajectoryQualified=False,
        StationaryInitializationQualified=False,SpatialGeometryCalibrated=False,AfferentFiringQualified=False,PhysiologyQualified=False,
        AvatarRecoveryAssessed=False,LiveSelectionChanged=False))
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,NoLiveMonitoring=True,
        Launcher=str(package/'source/tools/start-afferent-precision-window.ps1'))
    save(package.parent/(package.name+'-prepared.json'),pointer);print(json.dumps(pointer,indent=2),flush=True)


def prerequisites(package):
    cfg=read(package/'configuration.json');review=read(package/'reviewed-trajectory.json');v=read(package/'provenance.json')
    assert cfg['IsolatedAfferentPrecision'] and cfg['NoLiveMonitoring'] and cfg['FastTransientOnly']
    assert cfg['ExpectedNativeRuns']==cfg['ExpectedReferences']==21 and cfg['ExpectedTestCases']==181
    assert cfg['ExpectedClassCounts']==COUNTS and cfg['ExpectedSpectrumControls']==8
    assert review['operatorReportedCompletion'] and review['savedAnalysisExactlyReproduced'] and review['originalFailurePreserved']
    assert not review['numericalTrajectoryCriteriaPassed'] and review['passedTests']==181
    assert review['sourceRevision']==v['originalRevision'] and review['manifestSha256']==v['originalManifestSha256']
    assert h.digest(package/'baseline/trajectory-analysis.json')==review['analysisSha256']
    assert read(package/'baseline/trajectory-input.json')==original_protocol()
    spec=read(package/'trajectory-input.json');assert spec==protocol()
    for name in ('candidateComparison','referenceComparison','refinement','ledgerResidualBounds','cases','stepTicks','ticksPerMs','sampleTicks'):
        assert spec[name]==original_protocol()[name],name
    assert not any(v[k] for k in ('pendingCapturesRead','originalBoundsChanged','originalBiologyChanged',
        'modelsExecutedDuringPreparation','testsExecutedDuringPreparation'))
    assert {r['path'] for r in v['preservedSources']}==set(PRESERVED)
    for row in v['preservedSources']:assert h.digest(package/'source'/row['path'])==row['sha256']
    assert h.digest(package/'source-decisions.json')==v['sourceDecisionsSha256']
    assert h.digest(package/'initial-reference-checks.json')==v['initialWitnessesSha256']
    assert base.environment()==read(package/'preparation-environment.json')
    return spec


def preflight(package):
    prerequisites(package)
    for directory in read(package/'qualified-managed-runtime.json')['directories']:
        root=h.io(directory['path']);assert {str(p) for p in root.rglob('*') if p.is_file()}=={str(h.io(e['path'])) for e in directory['files']}
        for row in directory['files']:assert h.digest(row['path'])==row['sha256']
    print('Original biological kernels, fixtures, accuracy limits and numerical dependencies preserved; no model or test executed.',flush=True)


def runtime(package):
    preflight(package);assert read(package/'managed-runtime-before.json')==read(package/'qualified-managed-runtime.json')


def check_tests(package):
    prerequisites(package);print(json.dumps(inventory.check_tests(package,COUNTS),indent=2),flush=True)


def controls(package):
    preflight(package)
    import check_afferent_spectrum as control
    control.run(package/'spectrum-controls.json')


def reference(package):
    spec=prerequisites(package);runtime(package)
    from afferent_trajectory_analysis import initial_checks
    from afferent_trajectory_reference import run
    assert all(r['passed'] for r in initial_checks(package,spec))
    run(package,spec,read(package/'source-decisions.json'))


def compare(package):
    spec=prerequisites(package);runtime(package);check_tests(package)
    assert read(package/'spectrum-controls.json')==dict(completed=True,passed=8,expected=8,modelsExecuted=False)
    from afferent_trajectory_analysis import analyse
    result=analyse(package,spec,read(package/'source-decisions.json'));save(package/'trajectory-analysis.json',result)
    for row in result['cases']:print(row['key'],row['passed'],[r['error']['maximumAllowedFraction'] for r in row['candidates']],flush=True)
    if not result['numericalTrajectoryCriteriaPassed']:raise RuntimeError('Tighter joint trajectory criteria failed; original bounds and all traces retained.')


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    p=argparse.ArgumentParser();p.add_argument('command',choices=['prepare','preflight','postflight','runtime','check-tests','controls','reference','compare']);p.add_argument('path',type=Path)
    a=p.parse_args();dict(prepare=prepare,preflight=preflight,postflight=preflight,runtime=runtime,controls=controls,reference=reference,compare=compare,
        **{'check-tests':check_tests})[a.command](a.path.resolve())
