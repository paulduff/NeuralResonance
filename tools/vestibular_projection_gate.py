"""Freeze acknowledged outgoing vestibular pathways; no pending reference is accessed."""
import argparse
import difflib
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path, PurePosixPath
import json
import re
import shutil
import subprocess
import sys
import uuid
import xml.etree.ElementTree as ET
from zipfile import ZipFile
from canal_convergence_gate import PRESERVED as CONVERGENCE_PRESERVED
NATIVE=Path('D:/DNNE-build-work/canal-convergence-20261119-6ceeae')
NATIVE_REVISION='6210b4263e782cd2ef613862656af1d7fe486da0'
from afferent_trajectory_gate import inventory
from afferent_precision_gate import h, read, save
from cleft_trajectory_reference import base

ROOT=Path(__file__).resolve().parents[1]
COUNTS=dict(VestibularProjectionOutboxTests=11,CanalNucleusProjectionTests=3,RoutingFanOutTests=4,CanalConvergenceConnectionTests=12,ConvergentAfferentIngressTests=14,ObservedAfferentIngressTests=20,CanalNucleusConnectionTests=10,AfferentEventObserverTests=20,CanalAfferentInputTests=25)
PRESERVED=sorted(set(CONVERGENCE_PRESERVED+['Structures/_SharedRuntime/StructureEngine.cs',
    'Structures/_SharedRuntime/ConvergentAfferentIngress.cs',
    'tests/NeuralResonanceEngine.DNNE.Tests/ConvergentAfferentIngressTests.cs','tools/afferent_convergence_gate.py',
    'tools/NativeMotorBridgeProbe/CanalConvergenceConnection.cs',
    'tests/NeuralResonanceEngine.DNNE.Tests/CanalConvergenceConnectionTests.cs',
    'tests/NeuralResonanceEngine.DNNE.Tests/RoutingFanOutTests.cs','connectivity/dnne-connectivity.json','tools/canal_convergence_gate.py']))
COORDINATOR='ControlProgram/Program.cs'
PRESERVED=[name for name in PRESERVED if name!=COORDINATOR]


def inventory_methods(source):
    ordinary={name:count for name,count in COUNTS.items() if name!='RoutingFanOutTests'}
    result=inventory.inventory(source,ordinary)
    code=(source/'tests/NeuralResonanceEngine.DNNE.Tests/RoutingFanOutTests.cs').read_text(encoding='utf-8-sig')
    # The unchanged routing tests have comments between/after attributes. The
    # older inventory scanner cannot read those; normalize comments here only.
    code=re.sub(r'//[^\n]*|/\*[\s\S]*?\*/','',code)
    found=re.findall(r'((?:\s*\[[^\n]+\])+\s*)public\s+(?:async\s+)?(?:void|Task)\s+(\w+)\(',code)
    methods={name:1 if '[Fact]' in attrs else attrs.count('[InlineData(')
        for attrs,name in found if '[Fact]' in attrs or '[Theory]' in attrs}
    assert methods==dict(ResolveRoutes_FansOutToDivergentEdges=2,ResolveRoutes_ReturnsEveryConnectomeEdge_ForEveryStructure=1,
        StriatalD1AndD2NeuronsUseAnatomicallyDistinctProjectionSets=1)
    result.update({'NeuralResonanceEngine.DNNE.Tests.RoutingFanOutTests.'+name:n for name,n in methods.items()})
    return result


def test_evidence(package):
    cfg=read(package/'configuration.json');expected=inventory_methods(package/'source')
    assert cfg['ExpectedClassCounts']==COUNTS and cfg['ExpectedTestCases']==sum(COUNTS.values())==119
    assert expected==Counter(cfg['ExpectedTestMethods'])
    ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    trx=ET.parse(h.io(package/'test-results/focused.trx'));results=trx.findall('.//t:UnitTestResult',ns)
    assert len(results)==len({r.attrib['testName'] for r in results})==119
    assert all(r.attrib['outcome']=='Passed' for r in results)
    definitions={}
    for unit in trx.findall('.//t:TestDefinitions/t:UnitTest',ns):
        method=unit.find('t:TestMethod',ns);assert method is not None and unit.attrib['id'] not in definitions
        definitions[unit.attrib['id']]=method.attrib['className']+'.'+method.attrib['name']
    assert Counter(definitions[r.attrib['testId']] for r in results)==expected
    return dict(passedCases=len(results),passedClasses=COUNTS,exactMethodMultiplicity=True)


def coordinator_change(original,candidate):
    before=original.read_text(encoding='utf-8-sig');after=candidate.read_text(encoding='utf-8-sig')
    expected=before
    for declaration in ('SpikeMessage RewriteSpikeForRoute(', 'IReadOnlyList<ServiceInstance> ResolveTargetInstances('):
        old='private static '+declaration;assert expected.count(old)==1
        expected=expected.replace(old,'internal static '+declaration)
    assert after==expected,'Only visibility of the two existing routing helpers may change.'
    return ''.join(difflib.unified_diff(before.splitlines(keepends=True),after.splitlines(keepends=True),
        fromfile='qualified/'+COORDINATOR,tofile='candidate/'+COORDINATOR))


def additional_runtime(dotnet,qualified):
    # Reusing the coordinator adds the installed ASP.NET framework. Declare and
    # hash its exact files before launch; retain the previous core runtime whole.
    version=qualified['runtimeVersion'];major_minor=version.split('.')[:2]
    listing=subprocess.run([dotnet,'--list-runtimes'],check=True,capture_output=True,text=True,encoding='utf-8').stdout
    candidates=[]
    for line in listing.splitlines():
        match=re.fullmatch(r'Microsoft\.AspNetCore\.App (\d+\.\d+\.\d+) \[(.+)\]',line.strip())
        if match and match[1].split('.')[:2]==major_minor:
            candidates.append((tuple(map(int,match[1].split('.'))),Path(match[2])/match[1]))
    assert candidates,'Required ASP.NET framework is absent; do not install or change runtimes implicitly.'
    folder=max(candidates,key=lambda item:item[0])[1].resolve()
    assert str(folder) not in {d['path'] for d in qualified['directories']}
    files=sorted((p for p in folder.rglob('*') if p.is_file()),key=lambda p:str(p).casefold())
    assert files
    return dict(reason='Existing coordinator routing helper reuse; no host service starts.',
        directory=dict(path=str(folder),files=[dict(path=str(p),sha256=h.digest(p)) for p in files]),
        modelsExecuted=False,testsExecuted=False)


def verify_runtime_directory(directory):
    folder=h.io(directory['path'])
    assert {str(p) for p in folder.rglob('*') if p.is_file()}=={str(h.io(row['path'])) for row in directory['files']}
    for row in directory['files']:assert h.digest(row['path'])==row['sha256']


def prepare(review_folder):
    assert not h.git('status','--porcelain'),'Commit first.'
    revision=h.git('rev-parse','HEAD');assert revision==h.git('rev-parse','origin/main'),'Push first.'
    assert review_folder==(ROOT/'artifacts'/f'{NATIVE.name}-review').resolve()
    h.verify(review_folder,['review.json','analysis.json']);review=read(review_folder/'review.json')
    assert review['operatorReportedCompletion'] and review['sharedOwnerChecksPassed'] and review['savedAnalysisExactlyReproduced']
    assert Path(review['capture'])==NATIVE and review['sourceRevision']==NATIVE_REVISION and review['passedTests']==101
    assert h.digest(NATIVE/'backup-manifest.json')==review['manifestSha256']
    evidence=['managed-runtime-before.json','preparation-environment.json','analysis.json']
    h.verify(NATIVE,evidence+['source/'+n for n in PRESERVED]+['source/'+COORDINATOR])
    coordinator_patch=coordinator_change(NATIVE/'source'/COORDINATOR,ROOT/COORDINATOR)
    assert h.digest(NATIVE/'analysis.json')==review['analysisSha256']
    assert base.environment()==read(NATIVE/'preparation-environment.json')
    for name in PRESERVED:
        assert (ROOT/name).read_text(encoding='utf-8-sig')==h.io(NATIVE/'source'/name).read_text(encoding='utf-8-sig'),name
    methods=inventory_methods(ROOT);assert sum(methods.values())==119
    assert shutil.disk_usage('D:/').free>=8*1024**3
    package=Path('D:/DNNE-build-work')/('vestibular-projection-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6])
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
        target=h.io(package/name);shutil.copyfile(h.io(source),target);assert h.digest(source)==h.digest(target)
    copy(review_folder/'review.json','qualified-source-review.json')
    for name in evidence:
        copy(NATIVE/name,{'managed-runtime-before.json':'qualified-managed-runtime.json','analysis.json':'qualified-source-analysis.json'}.get(name,name))
    copy(package/'source/tools/backup-unity-run.ps1','backup-run.ps1')
    copy(NATIVE/'source'/COORDINATOR,'qualified-original-coordinator.cs')
    (package/'reviewed-coordinator-change.diff').write_text(coordinator_patch,encoding='utf-8')
    dotnet=shutil.which('dotnet');assert dotnet
    save(package/'declared-runtime-extension.json',additional_runtime(dotnet,read(package/'qualified-managed-runtime.json')))
    save(package/'provenance.json',dict(originalRevision=NATIVE_REVISION,originalManifestSha256=review['manifestSha256'],
        preservedSources=[dict(path=n,sha256=h.digest(NATIVE/'source'/n)) for n in PRESERVED],
        coordinatorOriginalSha256=h.digest(NATIVE/'source'/COORDINATOR),coordinatorRoutingVisibilityOnly=True,
        pendingCapturesRead=False,originalBiologyChanged=False,modelsExecutedDuringPreparation=False,testsExecutedDuringPreparation=False))
    files=[dict(Path=f.relative_to(h.io(package)).as_posix(),Sha256=h.digest(f)) for f in sorted(h.io(package).rglob('*')) if f.is_file()]
    dotnet=shutil.which('dotnet');assert dotnet
    save(package/'configuration.json',dict(Revision=revision,PackageFiles=files,BackupDestination=str(backup),
        DotnetExecutable=dotnet,DotnetExecutableSha256=h.digest(dotnet),PythonExecutable=sys.executable,PythonExecutableSha256=h.digest(sys.executable),
        IsolatedVestibularProjection=True,NoLiveMonitoring=True,ExpectedTestCases=119,ExpectedClassCounts=COUNTS,
        ExpectedTestMethods=methods,TestFilter='|'.join('FullyQualifiedName~'+c for c in COUNTS),
        IndependentCoupledTrajectoryQualified=False,StationaryInitializationQualified=False,SpatialGeometryCalibrated=False,
        AfferentFiringQualified=False,PhysiologyQualified=False,AvatarRecoveryAssessed=False,LiveSelectionChanged=False))
    preflight(package)
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,NoLiveMonitoring=True,
        Launcher=str(package/'source/tools/start-vestibular-projection-window.ps1'))
    save(ROOT/'artifacts'/(package.name+'-prepared.json'),pointer);print(json.dumps(pointer,indent=2),flush=True)


def preflight(package):
    cfg=read(package/'configuration.json');review=read(package/'qualified-source-review.json');provenance=read(package/'provenance.json')
    assert cfg['IsolatedVestibularProjection'] and cfg['NoLiveMonitoring'] and cfg['ExpectedTestCases']==119 and cfg['ExpectedClassCounts']==COUNTS
    assert inventory_methods(package/'source')==cfg['ExpectedTestMethods']
    assert review['sourceRevision']==provenance['originalRevision']==NATIVE_REVISION
    assert review['manifestSha256']==provenance['originalManifestSha256']
    assert review['operatorReportedCompletion'] and review['sharedOwnerChecksPassed'] and review['savedAnalysisExactlyReproduced'] and review['passedTests']==101
    assert h.digest(package/'qualified-source-analysis.json')==review['analysisSha256']
    assert not any(provenance[k] for k in ('pendingCapturesRead','originalBiologyChanged','modelsExecutedDuringPreparation','testsExecutedDuringPreparation'))
    assert provenance['coordinatorRoutingVisibilityOnly']
    assert h.digest(package/'qualified-original-coordinator.cs')==provenance['coordinatorOriginalSha256']
    assert coordinator_change(package/'qualified-original-coordinator.cs',package/'source'/COORDINATOR)==(package/'reviewed-coordinator-change.diff').read_text(encoding='utf-8')
    assert {r['path'] for r in provenance['preservedSources']}==set(PRESERVED)
    for row in provenance['preservedSources']:assert h.digest(package/'source'/row['path'])==row['sha256'],row['path']
    for row in cfg['PackageFiles']:assert h.digest(package/row['Path'])==row['Sha256'],row['Path']
    assert base.environment()==read(package/'preparation-environment.json')
    for directory in read(package/'qualified-managed-runtime.json')['directories']:
        verify_runtime_directory(directory)
    extension=read(package/'declared-runtime-extension.json')
    assert not extension['modelsExecuted'] and not extension['testsExecuted']
    verify_runtime_directory(extension['directory'])
    print('Original sensory/neuronal kernels and declared routing helper access preserved; no model/test or pending reference accessed by preflight.',flush=True)


def runtime(package):
    preflight(package);actual=read(package/'managed-runtime-before.json');qualified=read(package/'qualified-managed-runtime.json')
    assert {k:v for k,v in actual.items() if k!='directories'}=={k:v for k,v in qualified.items() if k!='directories'}
    expected=qualified['directories']+[read(package/'declared-runtime-extension.json')['directory']]
    assert actual['directories']==sorted(expected,key=lambda d:d['path'].casefold())


def check_tests(package):
    runtime(package);result=test_evidence(package)
    save(package/'analysis.json',dict(completed=True,componentChecksPassed=True,projectionHandoffChecksPassed=True,tests=result,
        independentCoupledTrajectoryQualified=False,stationaryInitializationQualified=False,spatialGeometryCalibrated=False,
        afferentFiringQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False))
    print(json.dumps(result,indent=2),flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser();parser.add_argument('command',choices=['prepare','preflight','postflight','runtime','check-tests']);parser.add_argument('path',type=Path)
    args=parser.parse_args()
    dict(prepare=prepare,preflight=preflight,postflight=preflight,runtime=runtime,**{'check-tests':check_tests})[args.command](args.path.resolve())
