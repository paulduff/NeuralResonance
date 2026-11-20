"""Recheck saved sensory evidence after an explicit JSON-schema repair. No ODE/capture."""
import argparse
import ast
from collections import Counter
from datetime import datetime, timezone
import importlib
import json
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import sys
import unittest
import uuid
from zipfile import ZipFile
import xml.etree.ElementTree as ET
import head_turn_afferent_gate as prior
from afferent_precision_gate import h, read, save, inventory
from cleft_trajectory_reference import base

ROOT=Path(__file__).resolve().parents[1]
ORIGINAL=Path('D:/DNNE-build-work/head-turn-afferent-20261120-2e0ed3')
REVISION='5f3887d489c36b67012700bc96012b195f9fc686'
COUNTS=dict(StandingBalanceEvidenceTests=12,UnityStandingTrialTests=9,UnityBodySensorTests=16)
CHANGED='tools/head_turn_afferent_analysis.py'


def control_methods(source):
    tree=ast.parse((source/'tools/check_head_observation_identity.py').read_text(encoding='utf-8-sig'))
    cls=next(n for n in tree.body if isinstance(n,ast.ClassDef) and n.name=='HeadObservationIdentityControls')
    extra=sorted('check_head_observation_identity.HeadObservationIdentityControls.'+n.name for n in cls.body
        if isinstance(n,ast.FunctionDef) and n.name.startswith('test_'))
    assert len(extra)==8
    result=sorted(prior.control_methods(source)+extra);assert len(result)==len(set(result))==40
    return result


def declared_repair(source):
    before=(ORIGINAL/'source'/CHANGED).read_text(encoding='utf-8-sig')
    expected=before.replace('from head_turn_input_evidence import audit as head_audit\n',
        'from head_turn_input_evidence import audit as head_audit\nfrom head_observation_identity import original_observation\n')
    for side,index in [('heldInput','head-1'),('closingInput','head')]:
        old=f"assert json.loads(released['{side}']['skull']['rawObservationJson'])==head_input['observations'][{index}]"
        new=f"original_observation(released['{side}']['skull']['rawObservationJson'],head_input['observations'][{index}])"
        assert expected.count(old)==1;expected=expected.replace(old,new)
    assert (source/CHANGED).read_text(encoding='utf-8-sig')==expected
    with ZipFile(ORIGINAL/'source.zip') as archive:
        original_python=[n for n in archive.namelist() if n.endswith('.py') and n!=CHANGED]
        for name in original_python:
            assert (source/name).read_text(encoding='utf-8-sig')==archive.read(name).decode('utf-8-sig').replace('\r\n','\n'),name
    for name in prior.PRESERVED:
        assert (source/name).read_text(encoding='utf-8-sig')==(ORIGINAL/'source'/name).read_text(encoding='utf-8-sig'),name
    return dict(analysisChange='two declared JSON spellings; every original value remains exact',
        originalPythonFilesUnchanged=len(original_python),preservedSources=len(prior.PRESERVED),
        originalBoundsChanged=False,neuronalEquationsChanged=False,nativeCaptureRerun=False)


def prepare(review_folder):
    assert not h.git('status','--porcelain'),'Commit first.'
    revision=h.git('rev-parse','HEAD');assert revision==h.git('rev-parse','origin/main'),'Push first.'
    h.verify(review_folder,['review.json','failure-operands.json','failure.json'])
    review=read(review_folder/'review.json')
    assert Path(review['capture'])==ORIGINAL and review['sourceRevision']==REVISION and review['operatorReportedCompletion']
    assert review['originalFailurePreserved'] and review['passedTests']==212 and review['passedAnalyticControls']==32
    assert h.digest(ORIGINAL/'backup-manifest.json')==review['manifestSha256']
    assert base.environment()==read(ORIGINAL/'preparation-environment.json')
    declaration=declared_repair(ROOT)
    methods=inventory.inventory(ROOT,COUNTS);assert sum(methods.values())==37
    package=Path('D:/DNNE-build-work')/('head-turn-reanalysis-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6])
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
                target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(archive.read(entry))
    for src,dst in [(review_folder/'review.json','original-review.json'),(review_folder/'failure-operands.json','failure-operands.json'),
        (ORIGINAL/'preparation-environment.json','preparation-environment.json'),(ORIGINAL/'managed-runtime-before.json','qualified-managed-runtime.json'),
        (package/'source/tools/backup-unity-run.ps1','backup-run.ps1')]: shutil.copyfile(src,package/dst)
    save(package/'declaration.json',declaration)
    files=[dict(Path=f.relative_to(h.io(package)).as_posix(),Sha256=h.digest(f)) for f in sorted(h.io(package).rglob('*')) if f.is_file()]
    dotnet=shutil.which('dotnet');assert dotnet
    save(package/'configuration.json',dict(Revision=revision,PackageFiles=files,BackupDestination=str(backup),
        OriginalCapture=str(ORIGINAL),OriginalManifestSha256=review['manifestSha256'],SavedHeadTurnReanalysis=True,
        DotnetExecutable=dotnet,DotnetExecutableSha256=h.digest(dotnet),PythonExecutable=sys.executable,PythonExecutableSha256=h.digest(sys.executable),
        ExpectedTestCases=37,ExpectedClassCounts=COUNTS,ExpectedTestMethods=methods,ExpectedAnalyticControls=40,
        ExpectedAnalyticMethods=control_methods(ROOT),ExpectedSourceSteps=2654208,NoLiveMonitoring=True,
        TestFilter='|'.join('FullyQualifiedName~'+c for c in COUNTS),NativeCaptureRerun=False,
        IndependentCoupledTrajectoryQualified=False,AfferentFiringQualified=False,LiveSelectionChanged=False,
        PhysiologyQualified=False,AvatarRecoveryAssessed=False,NativeUnityCompilationQualified=False))
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,NoLiveMonitoring=True,
        Launcher=str(package/'source/tools/start-head-turn-reanalysis-window.ps1'))
    save(ROOT/'artifacts'/(package.name+'-prepared.json'),pointer)
    print(json.dumps(pointer,indent=2),flush=True)


def preflight(package):
    cfg=read(package/'configuration.json');review=read(package/'original-review.json')
    assert cfg['SavedHeadTurnReanalysis'] and not cfg['NativeCaptureRerun'] and cfg['NoLiveMonitoring']
    assert cfg['OriginalManifestSha256']==review['manifestSha256']==h.digest(ORIGINAL/'backup-manifest.json')
    assert cfg['ExpectedTestCases']==37 and cfg['ExpectedTestMethods']==inventory.inventory(package/'source',COUNTS)
    assert cfg['ExpectedAnalyticMethods']==control_methods(package/'source') and cfg['ExpectedAnalyticControls']==40
    for row in cfg['PackageFiles']:assert h.digest(package/row['Path'])==row['Sha256'],row['Path']
    assert declared_repair(package/'source')==read(package/'declaration.json')
    assert base.environment()==read(package/'preparation-environment.json')
    import importlib.util
    spec=importlib.util.spec_from_file_location('head_review_files',package/'source/tools/review-olive-and-motor-20261109.py')
    common=importlib.util.module_from_spec(spec);spec.loader.exec_module(common)
    verified=common.verify(ORIGINAL,['test-binaries.json','probe-binaries.json'],REVISION,expected_status='FAILED',expected_exit=1,progress_every=1000)
    assert verified['manifestSha256']==review['manifestSha256']
    print('Original capture and both protected copies verified; only JSON name mapping changes. No model/capture rerun.',flush=True)


def runtime(package):
    assert read(package/'managed-runtime-before.json')==read(package/'qualified-managed-runtime.json')


def check_tests(package):
    expected=Counter(read(package/'configuration.json')['ExpectedTestMethods'])
    ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    tree=ET.parse(package/'test-results/focused.trx');rows=tree.findall('.//t:UnitTestResult',ns)
    assert len(rows)==len({r.attrib['testName'] for r in rows})==37 and all(r.attrib['outcome']=='Passed' for r in rows)
    definitions={}
    for test in tree.findall('.//t:TestDefinitions/t:UnitTest',ns):
        method=test.find('t:TestMethod',ns);definitions[test.attrib['id']]=method.attrib['className']+'.'+method.attrib['name']
    assert Counter(definitions[r.attrib['testId']] for r in rows)==expected
    save(package/'test-analysis.json',dict(passedCases=37,exactMethodMultiplicity=True,nativeUnityCompilationQualified=False))


def controls(package):
    names=read(package/'configuration.json')['ExpectedAnalyticMethods']
    suite=unittest.TestSuite(unittest.defaultTestLoader.loadTestsFromName(n) for n in names)
    assert suite.countTestCases()==40
    result=unittest.TextTestRunner(verbosity=2).run(suite)
    evidence=dict(completed=True,passed=result.wasSuccessful() and result.testsRun==40 and not result.skipped,
        cases=result.testsRun,methods=names,failures=[(t.id(),s) for t,s in result.failures],errors=[(t.id(),s) for t,s in result.errors],skipped=result.skipped)
    save(package/'analytic-controls.json',evidence);assert evidence['passed']


def analyse(package):
    from head_observation_identity import original_observation
    from head_turn_afferent_analysis import analyse as account
    operands=read(package/'failure-operands.json')
    assert json.loads(operands['originalRawJson'])!=operands['expected']
    original_observation(operands['originalRawJson'],operands['expected'])
    save(package/'failure-replay.json',dict(originalComparisonFailed=True,declaredSchemaIdentityPassed=True))
    print('Replaying all 2,654,208 SAVED steps and original packets. No sensory simulation or ODE rerun.',flush=True)
    result=account(ORIGINAL);save(package/'analysis.json',result)
    assert result['completed'] and result['connectedAccountingPassed'] and result['prescribedMotionAccountingPassed']
    assert result['sourceSteps']==2654208
    for row in result['runs']:print(row['condition'],row['sourceStepsPerInterval'],'events',row['events'],'accounting',row['accountingPassed'],flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser();parser.add_argument('command',choices=['prepare','preflight','postflight','runtime','check-tests','controls','analyse']);parser.add_argument('path',type=Path)
    args=parser.parse_args()
    {'prepare':prepare,'preflight':preflight,'postflight':preflight,'runtime':runtime,'check-tests':check_tests,'controls':controls,'analyse':analyse}[args.command](args.path.resolve())
