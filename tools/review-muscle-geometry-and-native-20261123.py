"""Review three operator-completed captures; no model/reference/test execution."""
from datetime import datetime,timezone
from decimal import Decimal
import importlib.util
import json
from pathlib import Path
import shutil
import sys

ROOT=Path(__file__).resolve().parents[1]
STUDIES={
    'native':('native-muscle-population-20261123-b3cf65','da4c72bf042c8c5aaa27189b6077ca4b2a4931b1',388),
    'cylinder':('cylinder-muscle-path-20261123-0b9165','67d0fb90a3f5544b53fbb3c54c4d9141a3c5bbe8',127),
    'patella':('patellar-path-20261123-a9bac5','4c95efc088eca30e0be918945af47e29f88ce58f',99),
    'routed':('routed-muscle-path-20261123-61820b','e4d2f181ccdb8f0ee8b91cdf3d0f0d56868455bb',169)}

def module(name,path):
    s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m

def main(kind):
    assert '--operator-reported-complete' in sys.argv and __debug__
    name,revision,count=STUDIES[kind];package=Path('D:/DNNE-build-work')/name;output=ROOT/'artifacts'/(name+'-review');assert not output.exists()
    common=module('completed_geometry_common',ROOT/'tools/review-olive-and-motor-20261109.py')
    archive=module('completed_geometry_archive',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    prior=module('completed_geometry_tests',ROOT/'tools/review-population-and-shared-motor-20261123.py')
    failed=kind=='native'
    report=common.verify(package,['test-binaries.json','probe-binaries.json'],revision,'FAILED' if failed else 'DIAGNOSTICS_COMPLETE',1 if failed else 0,1000)
    report['verifiedGitFiles']=archive.source_archive(common,package,revision)
    cfg=prior.check_tests(package,common,count);status=common.read(package/'window-status.json')
    assert status['PassedFocusedTests']==count and status['FrozenInputsPreserved'] and status['ManagedRuntimePreserved']
    assert common.read(package/'managed-runtime-before.json')==common.read(package/'managed-runtime-after.json')
    assert not any(status[k] for k in ('LiveBrainAccessed','OldSavesLoaded','NeuronalAuthorityQualified','AvatarStandingQualified'))
    copies=['window-status.json','component-analysis.json'];details={}
    if kind=='native':
        assert status['NativeUnityExecuted'] and status['PrescribedDiagnosticEvents']
        analysis=common.read(package/'native-population-analysis.json')
        assert analysis['completed'] and not analysis['nativeAccountingPassed'] and len(analysis['errors'])==8
        assert all('assert moving==ack and len(force_checks)==320' in e['traceback'] for e in analysis['errors'])
        player=common.read(package/'qualified-player/player-files.json')
        for e in player:
            relative=Path(e['Path']).relative_to(Path('D:/DNNE-build-work/named-muscle-native-20261123-998900'))
            assert common.digest(package/'qualified-player'/relative)==e['Sha256']
        findings=[]
        for condition in cfg['Cases']:
            folder=package/'native'/condition['name'];wire=folder/'tendon-bridge'
            ack=[json.loads(line) for line in (folder/'acknowledged.jsonl').read_text(encoding='utf-8-sig').splitlines()]
            replies=[common.read(p) for p in sorted(wire.glob('reply-*.json'))]
            requests=[common.read(p) for p in sorted(wire.glob('request-*.json'))];request_by_sequence={r['sequence']:r for r in requests}
            assert len(requests)==len(request_by_sequence)==len(replies)
            moving=[common.read(wire/'hello.json')]+[r for r in replies if r['operation'] in ('prelude','step')]
            assert len(ack)==len(moving)==42
            byseq={r['sequence']:r for r in replies};differences=[]
            for i,(captured,advanced) in enumerate(zip(ack,moving,strict=True)):
                keys={k for k in captured.keys()|advanced.keys() if captured.get(k)!=advanced.get(k)}
                if i<2:assert not keys
                else:
                    assert keys=={'operation','sequence'}
                    assert captured['operation']=='inspect' and advanced['operation']=='step' and captured['sequence']==advanced['sequence']+1
                    assert byseq[captured['sequence']]==captured
                    request=request_by_sequence[captured['sequence']]
                    assert request['operation']=='inspect' and request['expectedStep']==advanced['physicsStep'] and request['stateHash']==advanced['stateHash']
                    differences.append(dict(physicsStep=captured['physicsStep'],stepSequence=advanced['sequence'],inspectSequence=captured['sequence'],changedEnvelopeFields=sorted(keys)))
            points=sum(len(json.loads(r['applicationsJson'])['points']) for r in replies if r['operation']=='step')
            assert points==320
            saved=common.read(folder/'result.json');assert saved['completed'] and saved['condition']==condition and saved['endpoints']==42 and saved['closures']==40
            findings.append(dict(case=condition['name'],pointForces=points,payloadUnchanged=True,immediateInspectReceipts=differences))
        details=dict(originalStatusRetained='FAILED',nativeCasesCompleted=8,verifiedPlayerFiles=len(player),
            envelopeDiagnosis=findings,physicsPayloadChanged=False,completeCausalAccountingQualified=False)
        copies+=['native-population-analysis.json','compare.log']
    elif kind=='routed':
        a=common.read(package/'component-analysis.json')
        assert a==dict(completed=True,passedCases=169,newRoutedCases=18,exactInventory=True,unitySourceParityChecked=True,
            nativeUnityExecuted=False,anatomicalCalibrationQualified=False,avatarStandingQualified=False)
        assert status['RoutedAssemblyChecksPassed'] and not status['NativeUnityExecuted']
        details=dict(routedAssemblyChecksPassed=True,newRoutedCases=18,nativeUnityExecuted=False,independentCoupledTrajectoryQualified=False)
    else:
        prefix='cylinder' if kind=='cylinder' else 'patellar';a=common.read(package/(prefix+'-reference-analysis.json'))
        assert a['completed'] and a['passed'] and a['referenceDigits']==[80,110] and a['referenceAgreementBound']=='1e-60'
        expected=24 if kind=='cylinder' else 450
        assert a['cases']==len(a['rows'])==expected and all(r['passed'] for r in a['rows'])
        if kind=='cylinder':
            assert [a[k] for k in ('lengthBoundMeters','rateBoundMetersPerSecond','forceBoundNewtons','torqueBoundNewtonMeters')]==[2e-12,2e-12,1e-10,1e-10]
            bounds=[Decimal('2e-12')]*2+[Decimal('1e-10')]*12
        else:
            limits=dict(position='1e-12',first='1e-11',second='1e-10',applicationPoint='1e-12',force='1e-12',childTorque='1e-9',parentTorque='1e-9',movingVelocity='1e-10',generalizedForce='1e-9',speed='1e-12',power='1e-9',deliveredPower='1e-9')
            assert a['limits']==limits and a['sourceKneeRangeRadians']==[0,2.0944] and a['avatarMaximumKneeAngleRadians']==2.45
            bounds=[Decimal(v) for i,(_,v) in enumerate(limits.items()) for _ in range(3 if i<8 else 1)]
        for r in a['rows']:
            assert Decimal(r['maximumReferenceDifference'])<=Decimal('1e-60')
            assert len(r['errors'])==len(bounds) and all(Decimal(e)<=b for e,b in zip(r['errors'],bounds,strict=True))
            assert 0<=r['maximumAllowanceFraction']<=1
        details=dict(independentGeometryCriteriaPassed=True,independentComparisonCases=expected,maximumAllowanceFraction=max(r['maximumAllowanceFraction'] for r in a['rows']),
            maximumReferenceDifference=max((r['maximumReferenceDifference'] for r in a['rows']),key=Decimal),nativeUnityExecuted=False)
        copies+=[prefix+'-reference-analysis.json']
    report.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,passedManagedTests=count,
        modelsExecutedDuringReview=False,testsExecutedDuringReview=False,referenceExecutedDuringReview=False,
        neuronalAuthorityQualified=False,avatarStandingQualified=False,**details)
    output.mkdir();(output/'review.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    for file in copies:shutil.copyfile(package/file,output/file)
    shutil.copyfile(__file__,output/Path(__file__).name)
    print(json.dumps({k:v for k,v in report.items() if k!='envelopeDiagnosis'},indent=2),flush=True)

if __name__=='__main__':
    assert len(sys.argv)==3 and sys.argv[1] in STUDIES
    main(sys.argv[1])
