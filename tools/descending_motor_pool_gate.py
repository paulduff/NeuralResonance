"""Freeze motor-cell ownership only after the completed recurrent comparison is reviewed."""
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
import reticulospinal_gate as prior
from reticulospinal_partition_evidence import audit as partition_audit
NATIVE=Path('D:/DNNE-build-work/reticulospinal-20261119-a974ff')
NATIVE_REVISION='de0087852dbcee7ba564a03ce55df9c9398dc035'
from afferent_trajectory_gate import inventory
from afferent_precision_gate import h, read, save
from cleft_trajectory_reference import base

ROOT=Path(__file__).resolve().parents[1]
COUNTS=dict(prior.COUNTS,DescendingMotorPoolTests=18)
PRESERVED=sorted(set(prior.PRESERVED)|set(prior.CHANGED)|set(prior.FAILED_UNCHANGED)|{
    'tools/reticulospinal_gate.py','tools/reticulospinal_partition_evidence.py',
    'tests/NeuralResonanceEngine.DNNE.Tests/ReticulospinalComparisonEvidence.cs',
    *('tests/NeuralResonanceEngine.DNNE.Tests/'+name+'.cs' for name in prior.COUNTS),
    'tools/NativeMotorBridgeProbe/DescendingMotorPool.cs',
    'Structures/_SharedRuntime/ReceptorMotorSource.cs','Structures/_SharedRuntime/ReceptorDrivenMotoneuron.cs',
    'Structures/_SharedRuntime/MotorReceptorContact.cs','Structures/_SharedRuntime/MotoneuronReferenceCell.cs',
    'Structures/_SharedRuntime/MotoneuronChannelKinetics.cs','Structures/_SharedRuntime/MotoneuronCrossing.cs',
    'Protocol/MotorAxonProtocol.cs','Protocol/MotorAxonReplayBuffer.cs',
    'src/NRE.SimAvatar/AvatarMotorAxonReceiver.cs',
})


def inventory_methods(source):
    result=prior.inventory_methods(source)
    result.update(inventory.inventory(source,dict(DescendingMotorPoolTests=18)))
    assert sum(result.values())==164
    return result


def test_evidence(package):
    cfg=read(package/'configuration.json');expected=inventory_methods(package/'source')
    assert cfg['ExpectedClassCounts']==COUNTS and cfg['ExpectedTestCases']==sum(COUNTS.values())==164
    assert expected==Counter(cfg['ExpectedTestMethods'])
    ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    trx=ET.parse(h.io(package/'test-results/focused.trx'));results=trx.findall('.//t:UnitTestResult',ns)
    assert len(results)==len({r.attrib['testName'] for r in results})==164
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
    assert review_folder==(ROOT/'artifacts'/f'{NATIVE.name}-review').resolve()
    h.verify(review_folder,['review.json','analysis.json']);review=read(review_folder/'review.json')
    assert review['operatorReportedCompletion'] and review['recurrentOwnershipChecksPassed'] and review['savedAnalysisExactlyReproduced']
    assert Path(review['capture'])==NATIVE and review['sourceRevision']==NATIVE_REVISION and review['passedTests']==146
    assert h.digest(NATIVE/'backup-manifest.json')==review['manifestSha256']
    evidence=['managed-runtime-before.json','preparation-environment.json','analysis.json']
    h.verify(NATIVE,evidence+['source/'+n for n in PRESERVED])
    assert h.digest(NATIVE/'analysis.json')==review['analysisSha256']
    assert base.environment()==read(NATIVE/'preparation-environment.json')
    for name in PRESERVED:
        assert (ROOT/name).read_text(encoding='utf-8-sig')==h.io(NATIVE/'source'/name).read_text(encoding='utf-8-sig'),name
    methods=inventory_methods(ROOT);assert sum(methods.values())==164
    assert shutil.disk_usage('D:/').free>=8*1024**3
    package=Path('D:/DNNE-build-work')/('descending-motor-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6])
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
    save(package/'provenance.json',dict(originalRevision=NATIVE_REVISION,originalManifestSha256=review['manifestSha256'],
        preservedSources=[dict(path=n,sha256=h.digest(NATIVE/'source'/n)) for n in PRESERVED],
        pendingCapturesRead=False,originalBiologyChanged=False,modelsExecutedDuringPreparation=False,testsExecutedDuringPreparation=False))
    files=[dict(Path=f.relative_to(h.io(package)).as_posix(),Sha256=h.digest(f)) for f in sorted(h.io(package).rglob('*')) if f.is_file()]
    dotnet=shutil.which('dotnet');assert dotnet
    save(package/'configuration.json',dict(Revision=revision,PackageFiles=files,BackupDestination=str(backup),
        DotnetExecutable=dotnet,DotnetExecutableSha256=h.digest(dotnet),PythonExecutable=sys.executable,PythonExecutableSha256=h.digest(sys.executable),
        IsolatedDescendingMotorPool=True,NoLiveMonitoring=True,ExpectedTestCases=164,ExpectedClassCounts=COUNTS,
        CompletePartitionEvidenceRequired=True,PrescribedSourceFixturesOnly=True,MotorFiringQualified=False,
        ExpectedTestMethods=methods,TestFilter='|'.join('FullyQualifiedName~'+c for c in COUNTS),
        IndependentCoupledTrajectoryQualified=False,StationaryInitializationQualified=False,SpatialGeometryCalibrated=False,
        AfferentFiringQualified=False,PhysiologyQualified=False,AvatarRecoveryAssessed=False,LiveSelectionChanged=False))
    preflight(package)
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,NoLiveMonitoring=True,
        Launcher=str(package/'source/tools/start-descending-motor-window.ps1'))
    save(ROOT/'artifacts'/(package.name+'-prepared.json'),pointer);print(json.dumps(pointer,indent=2),flush=True)


def preflight(package):
    cfg=read(package/'configuration.json');review=read(package/'qualified-source-review.json');provenance=read(package/'provenance.json')
    assert cfg['CompletePartitionEvidenceRequired'] and cfg['PrescribedSourceFixturesOnly'] and not cfg['MotorFiringQualified']
    assert cfg['IsolatedDescendingMotorPool'] and cfg['NoLiveMonitoring'] and cfg['ExpectedTestCases']==164 and cfg['ExpectedClassCounts']==COUNTS
    assert inventory_methods(package/'source')==cfg['ExpectedTestMethods']
    assert review['sourceRevision']==provenance['originalRevision']==NATIVE_REVISION
    assert review['manifestSha256']==provenance['originalManifestSha256']
    assert review['operatorReportedCompletion'] and review['recurrentOwnershipChecksPassed'] and review['savedAnalysisExactlyReproduced'] and review['passedTests']==146
    assert h.digest(package/'qualified-source-analysis.json')==review['analysisSha256']
    assert not any(provenance[k] for k in ('pendingCapturesRead','originalBiologyChanged','modelsExecutedDuringPreparation','testsExecutedDuringPreparation'))
    assert {r['path'] for r in provenance['preservedSources']}==set(PRESERVED)
    for row in provenance['preservedSources']:assert h.digest(package/'source'/row['path'])==row['sha256'],row['path']
    for row in cfg['PackageFiles']:assert h.digest(package/row['Path'])==row['Sha256'],row['Path']
    assert base.environment()==read(package/'preparation-environment.json')
    for directory in read(package/'qualified-managed-runtime.json')['directories']:
        root=h.io(directory['path']);assert {str(f) for f in root.rglob('*') if f.is_file()}=={str(h.io(r['path'])) for r in directory['files']}
        for row in directory['files']:assert h.digest(row['path'])==row['sha256']
    print('Reviewed recurrent comparison, original motor/receptor equations and Avatar transport preserved; no model/test or pending reference accessed by preflight.',flush=True)


def runtime(package):
    preflight(package);assert read(package/'managed-runtime-before.json')==read(package/'qualified-managed-runtime.json')


def check_tests(package):
    runtime(package);result=test_evidence(package)
    partition=partition_audit(package)
    assert all(partition['checks'][k] for k in ('ReticularEqual','SpinalEqual','CanonicalDeliveriesEqual','CanonicalWaitingEqual'))
    save(package/'analysis.json',dict(completed=True,componentChecksPassed=True,motorPoolComponentChecksPassed=True,tests=result,
        partitionComparison=partition,prescribedSourceFixturesOnly=True,motorFiringQualified=False,
        independentCoupledTrajectoryQualified=False,stationaryInitializationQualified=False,spatialGeometryCalibrated=False,
        afferentFiringQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False))
    print(json.dumps(result,indent=2),flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser();parser.add_argument('command',choices=['prepare','preflight','postflight','runtime','check-tests']);parser.add_argument('path',type=Path)
    args=parser.parse_args()
    dict(prepare=prepare,preflight=preflight,postflight=preflight,runtime=runtime,**{'check-tests':check_tests})[args.command](args.path.resolve())
