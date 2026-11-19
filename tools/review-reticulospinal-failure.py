"""Review the reported recurrent comparison failure; no model/test execution."""
import argparse
from collections import Counter
from datetime import datetime,timezone
import importlib.util
import json
from pathlib import Path
import shutil
import sys
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]
CAPTURE=Path('D:/DNNE-build-work/reticulospinal-20261119-4c18a7')
REVISION='143b6d5373020f4f87a3443c8d9e206828943723'

def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path);result=importlib.util.module_from_spec(spec);spec.loader.exec_module(result);return result

def main(package,reported):
    assert reported and package==CAPTURE
    output=ROOT/'artifacts'/(package.name+'-review');assert not output.exists(),'Preserve earlier reviews.'
    common=module('recurrent_files',ROOT/'tools/review-olive-and-motor-20261109.py')
    archive=module('recurrent_git',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    result=common.verify(package,['test-binaries.json','probe-binaries.json'],REVISION,'FAILED',1,progress_every=1000)
    result['verifiedGitFiles']=archive.source_archive(common,package,REVISION)
    status=common.read(package/'window-status.json')
    for name in ('TestExecutionAttempted','MatchesQualifiedManagedRuntime','QualifiedDependenciesPreserved','ManagedRuntimePreserved','FrozenPackageUnchanged'):
        assert status[name] is True,name
    for name in ('ComponentChecksPassed','RecurrentOwnershipChecksPassed','IndependentCoupledTrajectoryQualified','StationaryInitializationQualified',
                 'SpatialGeometryCalibrated','AfferentFiringQualified','LiveBrainAccessed','OldSavesLoaded','NativeBodyExecuted','PhysiologyQualified','AvatarRecoveryAssessed','FullUnitSuitePassed'):
        assert status[name] is False,name
    assert common.digest(package/'managed-runtime-before.json')==common.digest(package/'managed-runtime-after.json')
    sys.path.insert(0,str(package/'source/tools'))
    try:
        gate=module('recurrent_frozen_gate',package/'source/tools/reticulospinal_gate.py');gate.runtime(package)
        expected=gate.inventory_methods(package/'source');assert sum(expected.values())==146
        ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
        trx=ET.parse(common.io(package/'test-results/focused.trx'));rows=trx.findall('.//t:UnitTestResult',ns)
        definitions={}
        for unit in trx.findall('.//t:TestDefinitions/t:UnitTest',ns):
            method=unit.find('t:TestMethod',ns);definitions[unit.attrib['id']]=method.attrib['className']+'.'+method.attrib['name']
        assert len(rows)==len({r.attrib['testName'] for r in rows})==146
        assert Counter(definitions[r.attrib['testId']] for r in rows)==expected
        failed=[r for r in rows if r.attrib['outcome']=='Failed'];assert len(failed)==1
        assert sum(r.attrib['outcome']=='Passed' for r in rows)==145
        name=definitions[failed[0].attrib['testId']]
        assert name=='NeuralResonanceEngine.DNNE.Tests.ReticulospinalCircuitTests.FeedbackSchedulingAndInputPartitionPreserveActualCellsAndContacts'
        error=failed[0].find('t:Output/t:ErrorInfo/t:Message',ns).text
        code=common.io(package/'source/Structures/_SharedRuntime/StructureEngine.cs').read_text(encoding='utf-8-sig')
        assert 'spikeMessage.MessageId = Guid.NewGuid();' in code
    finally:sys.path.pop(0)
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,passedTests=145,failedTests=1,
        failedMethod=name,error=error,exactTestInventory=True,originalFailurePreserved=True,
        originalEngineCreatesFreshEventIds=True,completeCrossRunStatesRecorded=False,
        numericalStateEqualityEstablished=False,identityDifferenceIsSoleCauseEstablished=False,
        testsExecutedDuringReview=False,modelsExecutedDuringReview=False,pendingCaptureAccessed=False,
        recurrentOwnershipQualified=False,physiologyQualified=False,avatarBalanceAssessed=False)
    output.mkdir();(output/'review.json').write_text(json.dumps(result,indent=2,allow_nan=False)+'\n',encoding='utf-8')
    shutil.copyfile(package/'window-status.json',output/'failure.json');shutil.copyfile(__file__,output/Path(__file__).name)
    print(json.dumps(dict(output=str(output),verifiedFiles=result['verifiedFilesPerCopy'],passed=145,failed=1,
        numericalStateEqualityEstablished=False,completeComparisonNeeded=True),indent=2),flush=True)

if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser();parser.add_argument('package',type=Path);parser.add_argument('--operator-reported-complete',action='store_true');args=parser.parse_args()
    if not args.operator_reported_complete:parser.error('Wait for the final operator report before opening this capture.')
    main(args.package.resolve(),args.operator_reported_complete)
