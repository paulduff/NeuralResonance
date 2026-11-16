"""Review the reported observed-source capture; do not rerun the neuron."""
from collections import Counter
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import re
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]


def main():
    spec=importlib.util.spec_from_file_location('capture_audit',ROOT/'tools/review-olive-and-motor-20261109.py')
    common=importlib.util.module_from_spec(spec);spec.loader.exec_module(common)
    package=ROOT/'artifacts/observed-motor-source-20261116-badc1b'
    revision='3cd9c72ba0c53578a694e7a5a4b32b2a27edea28'
    review=common.verify(package,['test-binaries.json','probe-binaries.json'],revision)
    archive=common.module('source_archive',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    review['verifiedGitFiles']=archive.source_archive(common,package,revision)
    config=common.read(package/'configuration.json');status=common.read(package/'window-status.json')
    assert status['PassedFocusedTests']==66 and status['SuccessfulConditions']==5 and status['SourceAdapterCriteriaPassed']
    assert status['NeuronalModelExecuted'] and not status['NativePhysicsExecuted'] and not status['MuscleDynamicsExecuted']
    expected=Counter()
    for cls,count in dict(ObservedMotorSourceTests=6,MotorAxonTransportTests=32,MotorAxonReplayBufferTests=16,
                          MotoneuronReferenceTests=11,SolutionManifestTests=1).items():
        code=common.io(package/'source/tests/NeuralResonanceEngine.DNNE.Tests'/(cls+'.cs')).read_text()
        found=re.findall(r'((?:\s*\[[^\n]+\])+\s*)public\s+(?:async\s+)?(?:void|Task)\s+(\w+)\(',code)
        methods={name:(1 if '[Fact]' in attrs else attrs.count('[InlineData(')) for attrs,name in found if '[Fact]' in attrs or '[Theory]' in attrs}
        assert sum(methods.values())==count
        expected.update({'NeuralResonanceEngine.DNNE.Tests.'+cls+'.'+name:n for name,n in methods.items()})
    ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    tests=ET.parse(package/'test-results/focused.trx').findall('.//t:UnitTestResult',ns)
    assert len(tests)==len({t.attrib['testName'] for t in tests})==66 and all(t.attrib['outcome']=='Passed' for t in tests)
    assert Counter(t.attrib['testName'].split('(',1)[0] for t in tests)==expected==Counter(config['ExpectedTestMethods'])
    provenance=common.read(package/'provenance.json')
    for row in provenance['copied']:
        assert common.digest(package/row['path'])==common.digest(row['original'])==row['sha256']
    same=provenance['qualifiedSourcePreserved']+['tools/NativeMotorBridgeProbe/ObservedMotorSource.cs',
                                              'tools/NativeMotorBridgeProbe/ObservedMotorSourceStudy.cs']
    for name in same:assert (ROOT/name).read_text(encoding='utf-8-sig')==common.io(package/'source'/name).read_text(encoding='utf-8-sig'),name
    audit=common.module('frozen_source_audit',package/'source/tools/analyse-observed-motor-source.py')
    analysis=audit.audit(package);assert analysis==common.read(package/'source-analysis.json')
    assert analysis['sourceAdapterCriteriaPassed'] and analysis['activeCadenceEventIdentityEqual']
    review.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,passedTests=66,
        savedAnalysisExactlyReproduced=True,currentSourcePreserved=same,analysis=analysis,sourceAdapterCriteriaPassed=True,
        activeCrossingsPerCondition=28,activeDeliveryPerCondition=28,exactRetriedBatches=85,
        completeStateTracesPerCondition=30,sourceDurationMs=6000,conditions=5,
        testsExecutedDuringReview=False,modelsSolvedDuringReview=False,nativePhysicsExecuted=False,muscleDynamicsExecuted=False,
        liveMotorAuthorityQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    output=ROOT/'artifacts/observed-motor-source-review-20261116';output.mkdir()
    (output/'review.json').write_text(json.dumps(review,indent=2,allow_nan=False)+'\n')
    print(json.dumps({k:v for k,v in review.items() if k not in ('analysis','currentSourcePreserved')},indent=2),flush=True)


if __name__=='__main__':main()
