"""Freeze prescribed opposing head/canal/nerve capture after reviewed midpoint accuracy."""
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
import declared_descending_gate as prior
from head_turn_afferent_protocol import protocol
import ast
import unittest
from declared_descending_evidence import audit as source_audit
import hashlib
import difflib
from reticulospinal_partition_evidence import audit as partition_audit
NATIVE=Path('D:/DNNE-build-work/declared-descending-20261119-82111a')
NATIVE_REVISION='9416df8a15c5bc9057e4b7f15f6144904bb90f21'
MIDPOINT=Path('D:/DNNE-build-work/canal-midpoint-trajectory-20261119-e503fd')
MIDPOINT_REVISION='4488f9da1590efbed84c7316c03b5ec941e5a66e'
FAILURE=Path('D:/DNNE-build-work/canal-reference-20261119-2a402c')
FAILURE_REVISION='fab52528b3d4250be2d647929d70d0c6273a5700'
from afferent_trajectory_gate import inventory
from afferent_precision_gate import h, read, save
from cleft_trajectory_reference import base

ROOT=Path(__file__).resolve().parents[1]
COUNTS=dict(prior.COUNTS,PrescribedHeadTurnTests=20,SkullKinematicsTests=12)
PROGRAM='tools/NativeMotorBridgeProbe/Program.cs'
PRESERVED=sorted((set(prior.PRESERVED)|set(prior.CHANGED)|{
    'tools/NativeMotorBridgeProbe/PrescribedHeadTurn.cs',
    *('tests/NeuralResonanceEngine.DNNE.Tests/'+name+'.cs' for name in COUNTS),
})-{PROGRAM})
DISPATCH='if (args.Length == 3 && args[0] == "--head-turn-afferent")\n{\n    HeadTurnAfferentStudy.Run(args[1], args[2]);\n    return;\n}\n\n'


def declared_changes(original,candidate):
    before=(original/PROGRAM).read_text(encoding='utf-8-sig');after=(candidate/PROGRAM).read_text(encoding='utf-8-sig')
    assert before.count('try\n{\n')>=1 and after==before.replace('try\n{\n','try\n{\n'+DISPATCH,1)
    return [dict(path=PROGRAM,originalSha256=h.digest(original/PROGRAM),candidateSha256=h.digest(candidate/PROGRAM),
        diff=''.join(difflib.unified_diff(before.splitlines(keepends=True),after.splitlines(keepends=True),fromfile='original/'+PROGRAM,tofile='candidate/'+PROGRAM)))]


def control_methods(source):
    tree=ast.parse((source/'tools/check_head_turn_input_evidence.py').read_text(encoding='utf-8-sig'))
    cls=next(n for n in tree.body if isinstance(n,ast.ClassDef) and n.name=='HeadTurnInputControls')
    methods=sorted('check_head_turn_input_evidence.HeadTurnInputControls.'+n.name for n in cls.body if isinstance(n,ast.FunctionDef) and n.name.startswith('test_'))
    assert len(methods)==8
    return methods


def inventory_methods(source):
    result=prior.inventory_methods(source)
    result.update(inventory.inventory(source,dict(PrescribedHeadTurnTests=20,SkullKinematicsTests=12)))
    assert sum(result.values())==212
    return result


def test_evidence(package):
    cfg=read(package/'configuration.json');expected=inventory_methods(package/'source')
    assert cfg['ExpectedClassCounts']==COUNTS and cfg['ExpectedTestCases']==sum(COUNTS.values())==212
    assert expected==Counter(cfg['ExpectedTestMethods'])
    ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    trx=ET.parse(h.io(package/'test-results/focused.trx'));results=trx.findall('.//t:UnitTestResult',ns)
    assert len(results)==len({r.attrib['testName'] for r in results})==212
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
    assert review['operatorReportedCompletion'] and review['descendingAnatomyComponentChecksPassed'] and review['savedAnalysisExactlyReproduced']
    assert Path(review['capture'])==NATIVE and review['sourceRevision']==NATIVE_REVISION and review['passedTests']==180
    assert h.digest(NATIVE/'backup-manifest.json')==review['manifestSha256']
    evidence=['managed-runtime-before.json','preparation-environment.json','analysis.json']
    h.verify(NATIVE,evidence+['source/'+n for n in PRESERVED]+['source/'+n for n in [PROGRAM]])
    assert h.digest(NATIVE/'analysis.json')==review['analysisSha256']
    assert base.environment()==read(NATIVE/'preparation-environment.json')
    for name in PRESERVED:
        assert (ROOT/name).read_text(encoding='utf-8-sig')==h.io(NATIVE/'source'/name).read_text(encoding='utf-8-sig'),name
    declared_changes(NATIVE/'source',ROOT)
    midpoint_review=ROOT/'artifacts'/(MIDPOINT.name+'-review')
    h.verify(midpoint_review,['review.json','analysis.json','window-status.json'])
    mr=read(midpoint_review/'review.json')
    assert mr['operatorReportedCompletion'] and mr['sampledContinuousInputAccuracyPassed'] and mr['savedReportCriteriaReconciled']
    assert mr['sourceRevision']==MIDPOINT_REVISION and Path(mr['capture'])==MIDPOINT
    assert h.digest(MIDPOINT/'backup-manifest.json')==mr['manifestSha256']
    midpoint_files=['connected-input.json','sensory/study/quiet-ticks64/initial.json','baseline/study/geometry.json','analysis.json']
    h.verify(MIDPOINT,midpoint_files)
    assert h.digest(MIDPOINT/'analysis.json')==mr['analysisSha256']
    methods=inventory_methods(ROOT);assert sum(methods.values())==212
    assert shutil.disk_usage('D:/').free>=32*1024**3
    package=Path('D:/DNNE-build-work')/('head-turn-afferent-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6])
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
        target=h.io(package/name);target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(h.io(source),target);assert h.digest(source)==h.digest(target)
    copy(review_folder/'review.json','qualified-source-review.json')
    copy(failure_folder/'review.json','failed-reference-review.json');copy(failure_folder/'analysis.json','failed-reference-analysis.json');copy(failure_folder/'failure.json','failed-reference-status.json')
    for name in evidence:
        copy(NATIVE/name,{'managed-runtime-before.json':'qualified-managed-runtime.json','analysis.json':'qualified-source-analysis.json'}.get(name,name))
    copy(midpoint_review/'review.json','qualified-midpoint-review.json')
    for name in midpoint_files:
        copy(MIDPOINT/name,{'connected-input.json':'qualified-connected-input.json','analysis.json':'qualified-midpoint-analysis.json'}.get(name,name))
    save(package/'connected-input.json',protocol(read(package/'qualified-connected-input.json')))
    copy(package/'source/tools/backup-unity-run.ps1','backup-run.ps1')
    for name in [PROGRAM]:
        target=package/'declared-original'/name;target.parent.mkdir(parents=True,exist_ok=True);copy(NATIVE/'source'/name,'declared-original/'+name)
    changes=declared_changes(package/'declared-original',package/'source')
    save(package/'provenance.json',dict(declaredChanges=changes,originalRevision=NATIVE_REVISION,originalManifestSha256=review['manifestSha256'],
        preservedSources=[dict(path=n,sha256=h.digest(NATIVE/'source'/n)) for n in PRESERVED],
        pendingCapturesRead=False,neuronalEquationsChanged=False,primaryOutputAnatomyChanged=False,prescribedHeadInputChanged=True,modelsExecutedDuringPreparation=False,testsExecutedDuringPreparation=False))
    files=[dict(Path=f.relative_to(h.io(package)).as_posix(),Sha256=h.digest(f)) for f in sorted(h.io(package).rglob('*')) if f.is_file()]
    dotnet=shutil.which('dotnet');assert dotnet
    save(package/'configuration.json',dict(Revision=revision,PackageFiles=files,BackupDestination=str(backup),
        DotnetExecutable=dotnet,DotnetExecutableSha256=h.digest(dotnet),PythonExecutable=sys.executable,PythonExecutableSha256=h.digest(sys.executable),
        IsolatedHeadTurnAfferent=True,ExpectedSourceSteps=2654208,ExpectedAnalyticControls=8,ExpectedAnalyticMethods=control_methods(ROOT),NoLiveMonitoring=True,ExpectedTestCases=212,ExpectedClassCounts=COUNTS,
        CompletePartitionEvidenceRequired=True,UpstreamInputsAreFixtures=True,MotorFiringQualified=False,DescendingMotorTrajectoryAccuracyQualified=False,
        ExpectedTestMethods=methods,TestFilter='|'.join('FullyQualifiedName~'+c for c in COUNTS),
        IndependentCoupledTrajectoryQualified=False,StationaryInitializationQualified=False,SpatialGeometryCalibrated=False,
        AfferentFiringQualified=False,PhysiologyQualified=False,AvatarRecoveryAssessed=False,LiveSelectionChanged=False))
    preflight(package)
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,NoLiveMonitoring=True,
        Launcher=str(package/'source/tools/start-head-turn-afferent-window.ps1'))
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
    assert cfg['IsolatedHeadTurnAfferent'] and cfg['NoLiveMonitoring'] and cfg['ExpectedTestCases']==212 and cfg['ExpectedClassCounts']==COUNTS
    assert inventory_methods(package/'source')==cfg['ExpectedTestMethods']
    assert review['sourceRevision']==provenance['originalRevision']==NATIVE_REVISION
    assert review['manifestSha256']==provenance['originalManifestSha256']
    assert review['operatorReportedCompletion'] and review['descendingAnatomyComponentChecksPassed'] and review['savedAnalysisExactlyReproduced'] and review['passedTests']==180
    assert h.digest(package/'qualified-source-analysis.json')==review['analysisSha256']
    assert not provenance['primaryOutputAnatomyChanged'] and provenance['prescribedHeadInputChanged']
    assert not any(provenance[k] for k in ('pendingCapturesRead','neuronalEquationsChanged','modelsExecutedDuringPreparation','testsExecutedDuringPreparation'))
    assert {r['path'] for r in provenance['preservedSources']}==set(PRESERVED)
    for row in provenance['preservedSources']:assert h.digest(package/'source'/row['path'])==row['sha256'],row['path']
    for row in cfg['PackageFiles']:assert h.digest(package/row['Path'])==row['Sha256'],row['Path']
    mr=read(package/'qualified-midpoint-review.json')
    assert mr['operatorReportedCompletion'] and mr['sampledContinuousInputAccuracyPassed'] and mr['sourceRevision']==MIDPOINT_REVISION
    assert h.digest(package/'qualified-midpoint-analysis.json')==mr['analysisSha256']
    assert read(package/'connected-input.json')==protocol(read(package/'qualified-connected-input.json'))
    assert cfg['ExpectedSourceSteps']==2654208 and cfg['ExpectedAnalyticControls']==8
    assert cfg['ExpectedAnalyticMethods']==control_methods(package/'source')
    assert base.environment()==read(package/'preparation-environment.json')
    for directory in read(package/'qualified-managed-runtime.json')['directories']:
        root=h.io(directory['path']);assert {str(f) for f in root.rglob('*') if f.is_file()}=={str(h.io(r['path'])) for r in directory['files']}
        for row in directory['files']:assert h.digest(row['path'])==row['sha256']
    print('Reviewed midpoint input, original sensory kernels, loaded state, geometry and dependencies preserved; prescribed motion declared. No model/test executed by preflight.',flush=True)


def runtime(package):
    preflight(package);assert read(package/'managed-runtime-before.json')==read(package/'qualified-managed-runtime.json')


def check_tests(package):
    runtime(package);result=test_evidence(package)
    partition=partition_audit(package)
    assert all(partition['checks'][k] for k in ('ReticularEqual','SpinalEqual','CanonicalDeliveriesEqual','CanonicalWaitingEqual'))
    source_evidence=source_audit(package)
    save(package/'test-analysis.json',dict(tests=result,sourceEvidence=source_evidence,partitionComparison=partition))
    print(json.dumps(result,indent=2),flush=True)


def controls(package):
    from check_head_turn_input_evidence import HeadTurnInputControls
    suite=unittest.defaultTestLoader.loadTestsFromTestCase(HeadTurnInputControls)
    methods=sorted(t.id() for t in suite)
    assert methods==read(package/'configuration.json')['ExpectedAnalyticMethods'] and suite.countTestCases()==8
    result=unittest.TextTestRunner(verbosity=2).run(suite)
    evidence=dict(completed=True,passed=result.wasSuccessful() and result.testsRun==8 and not result.skipped,
        cases=result.testsRun,methods=methods,failures=[(t.id(),text) for t,text in result.failures],
        errors=[(t.id(),text) for t,text in result.errors],skipped=result.skipped)
    save(package/'analytic-controls.json',evidence)
    assert evidence['passed'],'Input evidence controls failed.'


def analyse(package):
    from head_turn_afferent_analysis import analyse as account
    result=account(package);save(package/'analysis.json',result)
    assert result['completed'] and result['connectedAccountingPassed'] and result['prescribedMotionAccountingPassed']
    assert result['sourceSteps']==2654208
    for row in result['runs']:
        print(row['condition'],row['sourceStepsPerInterval'],'events',row['events'],'bundle nm',row['bundleRangeNm'],flush=True)



if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser();parser.add_argument('command',choices=['prepare','preflight','postflight','runtime','check-tests','controls','analyse']);parser.add_argument('path',type=Path)
    args=parser.parse_args()
    dict(prepare=prepare,preflight=preflight,postflight=preflight,runtime=runtime,controls=controls,analyse=analyse,**{'check-tests':check_tests})[args.command](args.path.resolve())
