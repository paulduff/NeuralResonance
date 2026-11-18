"""Prepare only after reported/reviewed components; run numerics visibly."""
import argparse
from datetime import datetime,timezone
import json
from pathlib import Path,PurePosixPath
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile
import afferent_component_gate as component
from afferent_numerical_protocol import protocol,validate,COUNTS

ROOT=Path(__file__).resolve().parents[1];h=component.h;read=component.read;save=component.save
REVISION='751f92fc6fa1101f36229481744c62ee0e82f8e4'
PRESERVED=sorted(set(component.PRESERVED+[
    'src/NRE.SimAvatar/AvatarVestibularAfferentMembrane.cs','src/NRE.SimAvatar/AvatarVestibularAfferentCable.cs',
    'src/NRE.SimAvatar/AvatarVestibularCleftAfferent.cs','tests/NeuralResonanceEngine.DNNE.Tests/VestibularAfferentMembraneTests.cs',
    'tests/NeuralResonanceEngine.DNNE.Tests/VestibularAfferentCableTests.cs','tests/NeuralResonanceEngine.DNNE.Tests/VestibularCleftAfferentTests.cs',
    'tools/afferent_component_gate.py','tools/cleft_membrane_reference.py','tools/cleft_spatial_reference.py',
    'tools/cleft_open_reference.py','tools/analyse-cleft-coupling.py','tools/cleft_membrane_protocol.py']))


def prepare(review_folder):
    assert not h.git('status','--porcelain'),'Commit first.'
    revision=h.git('rev-parse','HEAD');assert revision==h.git('rev-parse','origin/main'),'Push first.'
    assert shutil.disk_usage(ROOT).free>=4*1024**3 and shutil.disk_usage('D:/').free>=8*1024**3
    h.verify(review_folder,['review.json']);review=read(review_folder/'review.json')
    assert review['sourceRevision']==REVISION and review['operatorReportedCompletion'] and review['savedAnalysisExactlyReproduced']
    assert review['passedTests']==166 and review['componentCriteriaPassed']
    capture=Path(review['capture']);assert h.digest(capture/'backup-manifest.json')==review['manifestSha256']
    evidence=['source-decisions.json','qualified-refinement-review.json','managed-runtime-before.json','analysis.json',
        'source-evidence/template.json','source-evidence/paper_roles.json','source-evidence/supporting-information.pdf','source-evidence/source-manifest.json']
    h.verify(capture,evidence+['source/'+n for n in PRESERVED])
    for name in PRESERVED:assert (ROOT/name).read_text(encoding='utf-8-sig')==h.io(capture/'source'/name).read_text(encoding='utf-8-sig'),name
    refinement=read(capture/'qualified-refinement-review.json');refined=Path(refinement['capture'])
    assert refinement['sourceRevision']==component.REVISION and refinement['operatorReportedCompletion'] and refinement['numericalCriteriaPassed']
    assert h.digest(refined/'backup-manifest.json')==refinement['manifestSha256']
    h.verify(refined,['preparation-environment.json'])
    assert component.prior.trajectory.base.environment()==read(refined/'preparation-environment.json')
    methods=component.prior.component.inventory.inventory(ROOT,COUNTS);assert sum(methods.values())==166
    package=ROOT/'artifacts'/('afferent-numerics-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6]);backup=Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
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
        target=h.io(package/name);target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(h.io(source),target);assert h.digest(source)==h.digest(target)
    for name in evidence:
        target={'managed-runtime-before.json':'qualified-managed-runtime.json','analysis.json':'qualified-component-analysis.json'}.get(name,name)
        copy(capture/name,target)
    copy(review_folder/'review.json','qualified-component-review.json')
    copy(refined/'preparation-environment.json','preparation-environment.json')
    copy(package/'source/tools/backup-unity-run.ps1','backup-run.ps1');spec=protocol();save(package/'numerical-input.json',spec)
    save(package/'provenance.json',dict(originalRevision=REVISION,originalManifestSha256=review['manifestSha256'],
        preservedSources=[dict(path=n,sha256=h.digest(capture/'source'/n)) for n in PRESERVED],
        sourceEvidence=[dict(path=p.relative_to(package).as_posix(),sha256=h.digest(p)) for p in sorted((package/'source-evidence').iterdir())],
        pendingCapturesRead=False,originalBiologyChanged=False,modelsExecutedDuringPreparation=False,testsExecutedDuringPreparation=False))
    files=[dict(Path=p.relative_to(h.io(package)).as_posix(),Sha256=h.digest(p)) for p in sorted(h.io(package).rglob('*')) if p.is_file()]
    dotnet=shutil.which('dotnet');assert dotnet
    save(package/'configuration.json',dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),PackageFiles=files,
        DotnetExecutable=dotnet,DotnetExecutableSha256=h.digest(dotnet),PythonExecutable=sys.executable,PythonExecutableSha256=h.digest(sys.executable),
        BackupDestination=str(backup),IsolatedAfferentNumerics=True,NoLiveMonitoring=True,ExpectedTestCases=166,ExpectedClassCounts=COUNTS,
        ExpectedTestMethods=methods,TestFilter='|'.join('FullyQualifiedName~'+c for c in COUNTS),ExpectedCounts=spec['expectedCounts'],
        IndependentCoupledTrajectoryQualified=False,StationaryInitializationQualified=False,SpatialGeometryCalibrated=False,
        AfferentFiringQualified=False,PhysiologyQualified=False,AvatarRecoveryAssessed=False,LiveSelectionChanged=False))
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,NoLiveMonitoring=True,Launcher=str(package/'source/tools/start-afferent-numerics-window.ps1'))
    save(package.parent/(package.name+'-prepared.json'),pointer);print(json.dumps(pointer,indent=2),flush=True)


def prerequisites(package):
    spec=validate(read(package/'numerical-input.json'));cfg=read(package/'configuration.json')
    review=read(package/'qualified-component-review.json');provenance=read(package/'provenance.json')
    assert cfg['IsolatedAfferentNumerics'] and cfg['NoLiveMonitoring'] and cfg['ExpectedCounts']==spec['expectedCounts']
    assert cfg['ExpectedTestCases']==166 and cfg['ExpectedClassCounts']==COUNTS
    assert review['sourceRevision']==provenance['originalRevision']==REVISION and review['manifestSha256']==provenance['originalManifestSha256']
    assert review['operatorReportedCompletion'] and review['savedAnalysisExactlyReproduced'] and review['componentCriteriaPassed'] and review['passedTests']==166
    assert review['analysis']==read(package/'qualified-component-analysis.json')
    assert not any(provenance[k] for k in ('pendingCapturesRead','originalBiologyChanged','modelsExecutedDuringPreparation','testsExecutedDuringPreparation'))
    assert {r['path'] for r in provenance['preservedSources']}==set(PRESERVED)
    for row in provenance['preservedSources']:assert h.digest(package/'source'/row['path'])==row['sha256']
    for row in provenance['sourceEvidence']:assert h.digest(package/row['path'])==row['sha256']
    assert component.source_choices(read(package/'source-evidence/template.json'),(package/'source/src/NRE.SimAvatar/AvatarVestibularAfferentMembrane.cs').read_text(encoding='utf-8-sig'))==read(package/'source-decisions.json')
    assert component.prior.trajectory.base.environment()==read(package/'preparation-environment.json')
    return spec


def preflight(package):
    prerequisites(package)
    for directory in read(package/'qualified-managed-runtime.json')['directories']:
        root=h.io(directory['path']);assert {str(p) for p in root.rglob('*') if p.is_file()}=={str(h.io(e['path'])) for e in directory['files']}
        for row in directory['files']:assert h.digest(row['path'])==row['sha256']
    print('Reviewed nerve/sensory sources, source data and numerical/runtime dependencies preserved; no model or test executed.',flush=True)


def runtime(package):
    preflight(package);assert read(package/'managed-runtime-before.json')==read(package/'qualified-managed-runtime.json')


def check_tests(package):
    prerequisites(package);print(json.dumps(component.prior.component.inventory.check_tests(package,COUNTS),indent=2),flush=True)


def references(package):
    spec=prerequisites(package);runtime(package)
    from afferent_numerical_reference import run
    run(package,spec,read(package/'source-decisions.json'))


def compare(package):
    prerequisites(package);runtime(package);check_tests(package)
    from afferent_numerical_analysis import analyse
    result=analyse(package);save(package/'analysis.json',result)
    for group in result['counts']:
        matching=[r for r in result['cases'] if r['group']==group]
        print(group,sum(r['passed'] for r in matching),'/',len(matching),'passed',flush=True)
    if not result['numericalCriteriaPassed']:raise RuntimeError('Sensory nerve derivative criteria failed; preserve every witness and original bound.')


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    p=argparse.ArgumentParser();p.add_argument('command',choices=['prepare','preflight','postflight','runtime','check-tests','references','compare']);p.add_argument('path',type=Path)
    a=p.parse_args();dict(prepare=prepare,preflight=preflight,postflight=preflight,runtime=runtime,references=references,compare=compare,**{'check-tests':check_tests})[a.command](a.path.resolve())
