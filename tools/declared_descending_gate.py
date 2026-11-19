"""Freeze explicit descending source anatomy controls after completed midpoint ownership review."""
import argparse
from collections import Counter
import xml.etree.ElementTree as ET
from datetime import datetime, timezone
from pathlib import Path, PurePosixPath
import json
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile
import canal_midpoint_gate as prior
from declared_descending_evidence import audit as source_audit
import hashlib
import difflib
from reticulospinal_partition_evidence import audit as partition_audit
NATIVE=Path('D:/DNNE-build-work/canal-midpoint-20261119-f8a807')
NATIVE_REVISION='7b50528fb3a2e29bccc52a5e8a01984dd791b6d3'
FAILURE=Path('D:/DNNE-build-work/canal-reference-20261119-2a402c')
FAILURE_REVISION='fab52528b3d4250be2d647929d70d0c6273a5700'
from afferent_trajectory_gate import inventory
from afferent_precision_gate import h, read, save
from cleft_trajectory_reference import base

ROOT=Path(__file__).resolve().parents[1]
COUNTS=dict(prior.COUNTS,DeclaredDescendingProjectionTests=8)
CHANGED={'tools/NativeMotorBridgeProbe/CanalAfferentStudy.cs': ('4b04639d29c8fb5ba8486dcfc3b36f607568fa2a21c2867072b582cc57fb6639', '25eeeaea1762ad145c88a74dc8cb2c875119d6d4622942794c4520c307ba979d'), 'tools/NativeMotorBridgeProbe/Program.cs': ('a50ceb5b45850bb3d0a3fe92b4f8d191bd1a8a2f13db4e1c23d0f8ad6c6b742f', '022a95f42242993e01c1f01267fb8813075179207ed91127c01eed3a6f7eb14d'), 'tools/canal_afferent_analysis.py': ('03f9453fc8de2673b3e3354ba65e45920490e558efd2daab75623e077f71461d', 'f6dc78f6cca1ea31fecd8a42b52347b1706f6b28cfaa52cdd869edff5f5990a4'), 'tools/canal_afferent_reference_analysis.py': ('b5f256492254caf5e897ba9e46a97a6dc0e491141d89b18d9be3677fb551c897', '8829a2ab337ca7a3f299b35cbc3fb5df8f13cdfdd3fa53dfa9d82a978958f930'), 'Structures/_SharedRuntime/StructureEngine.cs': ('90c5a74a5d0db46ae3f91d2a68c548ec0548cc6a0e84189e851946e347a7757b', '7bb3ff27bbc0b4e36b312e0d990a2b278e51a486fe8218fb6ad91afbb845b1af'), 'tools/NativeMotorBridgeProbe/VestibularProjectionReceiver.cs': ('8c505e38e5a64affd33c7fcf724eda6381201f3fd9d98c04b756a789e5e9f00f', '3b3b3ef72674f0a872a8a5e91c0de750ec918caffdb1a32858eaee85dce71089')}
PRESERVED=sorted((set(prior.PRESERVED)|set(prior.CHANGED)|{
    'tools/canal_midpoint_gate.py',
    *('tests/NeuralResonanceEngine.DNNE.Tests/'+name+'.cs' for name in prior.COUNTS),
})-set(CHANGED))


def declared_changes(original,candidate):
    result=[]
    for name,(before_hash,after_hash) in CHANGED.items():
        before=h.io(original/name).read_text(encoding='utf-8-sig');after=h.io(candidate/name).read_text(encoding='utf-8-sig')
        assert hashlib.sha256(before.encode()).hexdigest()==before_hash,name
        assert hashlib.sha256(after.encode()).hexdigest()==after_hash,name
        diff=''.join(difflib.unified_diff(before.splitlines(keepends=True),after.splitlines(keepends=True),fromfile='original/'+name,tofile='candidate/'+name))
        assert diff,name
        result.append(dict(path=name,originalSha256=h.digest(original/name),candidateSha256=h.digest(candidate/name),
            normalizedOriginalSha256=before_hash,normalizedCandidateSha256=after_hash,diff=diff))
    return result



def inventory_methods(source):
    result=prior.inventory_methods(source)
    result.update(inventory.inventory(source,dict(DeclaredDescendingProjectionTests=8)))
    assert sum(result.values())==180
    return result


def test_evidence(package):
    cfg=read(package/'configuration.json');expected=inventory_methods(package/'source')
    assert cfg['ExpectedClassCounts']==COUNTS and cfg['ExpectedTestCases']==sum(COUNTS.values())==180
    assert expected==Counter(cfg['ExpectedTestMethods'])
    ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    trx=ET.parse(h.io(package/'test-results/focused.trx'));results=trx.findall('.//t:UnitTestResult',ns)
    assert len(results)==len({r.attrib['testName'] for r in results})==180
    assert all(r.attrib['outcome']=='Passed' for r in results)
    definitions={}
    for unit in trx.findall('.//t:TestDefinitions/t:UnitTest',ns):
        method=unit.find('t:TestMethod',ns);assert method is not None and unit.attrib['id'] not in definitions
        definitions[unit.attrib['id']]=method.attrib['className']+'.'+method.attrib['name']
    assert Counter(definitions[r.attrib['testId']] for r in results)==expected
    return dict(passedCases=len(results),passedClasses=COUNTS,exactMethodMultiplicity=True)



def prepare(review_folder):
    assert not h.git('status','--porcelain'),'Commit first.'
    revision=h.git('rev-parse','HEAD');assert revision==h.git('rev-parse','origin/main'),'Push first.'
    failure_folder=ROOT/'artifacts'/(FAILURE.name+'-review')
    h.verify(failure_folder,['review.json','analysis.json','failure.json']);failure=read(failure_folder/'review.json')
    assert failure['operatorReportedCompletion'] and failure['originalFailurePreserved'] and failure['savedReportCriteriaReconciled']
    assert failure['sourceRevision']==FAILURE_REVISION and failure['comparedNativeSteps']==2293760 and not failure['sampledContinuousInputAccuracyPassed']
    assert h.digest(FAILURE/'backup-manifest.json')==failure['manifestSha256']
    assert review_folder==(ROOT/'artifacts'/f'{NATIVE.name}-review').resolve()
    h.verify(review_folder,['review.json','analysis.json']);review=read(review_folder/'review.json')
    assert review['operatorReportedCompletion'] and review['midpointComponentChecksPassed'] and review['savedAnalysisExactlyReproduced']
    assert Path(review['capture'])==NATIVE and review['sourceRevision']==NATIVE_REVISION and review['passedTests']==172
    assert h.digest(NATIVE/'backup-manifest.json')==review['manifestSha256']
    evidence=['managed-runtime-before.json','preparation-environment.json','analysis.json']
    h.verify(NATIVE,evidence+['source/'+n for n in PRESERVED]+['source/'+n for n in CHANGED])
    assert h.digest(NATIVE/'analysis.json')==review['analysisSha256']
    assert base.environment()==read(NATIVE/'preparation-environment.json')
    for name in PRESERVED:
        assert (ROOT/name).read_text(encoding='utf-8-sig')==h.io(NATIVE/'source'/name).read_text(encoding='utf-8-sig'),name
    declared_changes(NATIVE/'source',ROOT)
    methods=inventory_methods(ROOT);assert sum(methods.values())==180
    assert shutil.disk_usage('D:/').free>=8*1024**3
    package=Path('D:/DNNE-build-work')/('declared-descending-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6])
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
    copy(failure_folder/'review.json','failed-reference-review.json');copy(failure_folder/'analysis.json','failed-reference-analysis.json');copy(failure_folder/'failure.json','failed-reference-status.json')
    for name in evidence:
        copy(NATIVE/name,{'managed-runtime-before.json':'qualified-managed-runtime.json','analysis.json':'qualified-source-analysis.json'}.get(name,name))
    copy(package/'source/tools/backup-unity-run.ps1','backup-run.ps1')
    for name in CHANGED:
        target=package/'declared-original'/name;target.parent.mkdir(parents=True,exist_ok=True);copy(NATIVE/'source'/name,'declared-original/'+name)
    changes=declared_changes(package/'declared-original',package/'source')
    save(package/'provenance.json',dict(declaredChanges=changes,originalRevision=NATIVE_REVISION,originalManifestSha256=review['manifestSha256'],
        preservedSources=[dict(path=n,sha256=h.digest(NATIVE/'source'/n)) for n in PRESERVED],
        pendingCapturesRead=False,neuronalEquationsChanged=False,primaryOutputAnatomyChanged=True,modelsExecutedDuringPreparation=False,testsExecutedDuringPreparation=False))
    files=[dict(Path=f.relative_to(h.io(package)).as_posix(),Sha256=h.digest(f)) for f in sorted(h.io(package).rglob('*')) if f.is_file()]
    dotnet=shutil.which('dotnet');assert dotnet
    save(package/'configuration.json',dict(Revision=revision,PackageFiles=files,BackupDestination=str(backup),
        DotnetExecutable=dotnet,DotnetExecutableSha256=h.digest(dotnet),PythonExecutable=sys.executable,PythonExecutableSha256=h.digest(sys.executable),
        IsolatedDeclaredDescendingControls=True,NoLiveMonitoring=True,ExpectedTestCases=180,ExpectedClassCounts=COUNTS,
        CompletePartitionEvidenceRequired=True,UpstreamInputsAreFixtures=True,MotorFiringQualified=False,DescendingMotorTrajectoryAccuracyQualified=False,
        ExpectedTestMethods=methods,TestFilter='|'.join('FullyQualifiedName~'+c for c in COUNTS),
        IndependentCoupledTrajectoryQualified=False,StationaryInitializationQualified=False,SpatialGeometryCalibrated=False,
        AfferentFiringQualified=False,PhysiologyQualified=False,AvatarRecoveryAssessed=False,LiveSelectionChanged=False))
    preflight(package)
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,NoLiveMonitoring=True,
        Launcher=str(package/'source/tools/start-declared-descending-window.ps1'))
    save(ROOT/'artifacts'/(package.name+'-prepared.json'),pointer);print(json.dumps(pointer,indent=2),flush=True)


def preflight(package):
    cfg=read(package/'configuration.json');review=read(package/'qualified-source-review.json');provenance=read(package/'provenance.json')
    failure=read(package/'failed-reference-review.json');status=read(package/'failed-reference-status.json')
    assert failure['operatorReportedCompletion'] and failure['originalFailurePreserved'] and failure['savedReportCriteriaReconciled']
    assert failure['sourceRevision']==FAILURE_REVISION and failure['comparedNativeSteps']==2293760
    assert not failure['sampledContinuousInputAccuracyPassed'] and status['Status']=='FAILED' and status['Revision']==FAILURE_REVISION
    assert h.digest(package/'failed-reference-analysis.json')==failure['analysisSha256']
    assert declared_changes(package/'declared-original',package/'source')==provenance['declaredChanges']
    assert not cfg['DescendingMotorTrajectoryAccuracyQualified']
    assert cfg['CompletePartitionEvidenceRequired'] and cfg['UpstreamInputsAreFixtures'] and not cfg['MotorFiringQualified']
    assert cfg['IsolatedDeclaredDescendingControls'] and cfg['NoLiveMonitoring'] and cfg['ExpectedTestCases']==180 and cfg['ExpectedClassCounts']==COUNTS
    assert inventory_methods(package/'source')==cfg['ExpectedTestMethods']
    assert review['sourceRevision']==provenance['originalRevision']==NATIVE_REVISION
    assert review['manifestSha256']==provenance['originalManifestSha256']
    assert review['operatorReportedCompletion'] and review['midpointComponentChecksPassed'] and review['savedAnalysisExactlyReproduced'] and review['passedTests']==172
    assert h.digest(package/'qualified-source-analysis.json')==review['analysisSha256']
    assert provenance['primaryOutputAnatomyChanged']
    assert not any(provenance[k] for k in ('pendingCapturesRead','neuronalEquationsChanged','modelsExecutedDuringPreparation','testsExecutedDuringPreparation'))
    assert {r['path'] for r in provenance['preservedSources']}==set(PRESERVED)
    for row in provenance['preservedSources']:assert h.digest(package/'source'/row['path'])==row['sha256'],row['path']
    for row in cfg['PackageFiles']:assert h.digest(package/row['Path'])==row['Sha256'],row['Path']
    assert base.environment()==read(package/'preparation-environment.json')
    for directory in read(package/'qualified-managed-runtime.json')['directories']:
        root=h.io(directory['path']);assert {str(f) for f in root.rglob('*') if f.is_file()}=={str(h.io(r['path'])) for r in directory['files']}
        for row in directory['files']:assert h.digest(row['path'])==row['sha256']
    print('Original neuronal kernels retained; explicit output anatomy and prior harness changes recorded. No model/test or pending capture accessed by preflight.',flush=True)


def runtime(package):
    preflight(package);assert read(package/'managed-runtime-before.json')==read(package/'qualified-managed-runtime.json')


def check_tests(package):
    runtime(package);result=test_evidence(package)
    partition=partition_audit(package)
    assert all(partition['checks'][k] for k in ('ReticularEqual','SpinalEqual','CanonicalDeliveriesEqual','CanonicalWaitingEqual'))
    source_evidence=source_audit(package)
    save(package/'analysis.json',dict(completed=True,componentChecksPassed=True,descendingAnatomyComponentChecksPassed=True,tests=result,sourceEvidence=source_evidence,
        partitionComparison=partition,upstreamInputsAreFixtures=True,motorFiringQualified=False,descendingMotorTrajectoryAccuracyQualified=False,
        independentCoupledTrajectoryQualified=False,stationaryInitializationQualified=False,spatialGeometryCalibrated=False,
        afferentFiringQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False))
    print(json.dumps(result,indent=2),flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser();parser.add_argument('command',choices=['prepare','preflight','postflight','runtime','check-tests']);parser.add_argument('path',type=Path)
    args=parser.parse_args()
    dict(prepare=prepare,preflight=preflight,postflight=preflight,runtime=runtime,**{'check-tests':check_tests})[args.command](args.path.resolve())
