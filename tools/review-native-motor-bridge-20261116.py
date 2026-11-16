"""Verify the completed native bridge evidence without running physics or a model."""
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
    package=ROOT/'artifacts/native-motor-bridge-20261116-e7ddb6';revision='ea47f667a7848be2dd58f0d7a5c1fba427a5352f'
    review=common.verify(package,['test-binaries.json','probe-binaries.json'],revision)
    archive=common.module('source_archive',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    review['verifiedGitFiles']=archive.source_archive(common,package,revision)
    for entry in common.read(package/'player-files.json'):
        Path(entry['Path']).relative_to(package/'player');assert common.digest(entry['Path'])==entry['Sha256']
    source=common.io(package/'source/UnityWorld');compiled=0
    for path in source.rglob('*.cs'):
        assert path.read_bytes()==common.io(package/'built-project'/path.relative_to(source)).read_bytes();compiled+=1
    build=common.read(package/'player/build-result.json');assert build['result']=='Succeeded' and build['errors']==0
    config=common.read(package/'configuration.json');status=common.read(package/'window-status.json')
    assert status['PassedFocusedTests']==81 and status['NativeBridgeChecksPassed'] and status['NativeDriverSucceeded']
    assert status['PassedNativeCases']==12 and status['ActualPhysicsSteps']==4096
    metadata=common.module('frozen_metadata',package/'source/tools/unity_metadata.py').validate(package/'source/UnityWorld')
    assert metadata==common.read(package/'unity-metadata.json')
    expected=Counter()
    for cls,count in dict(MusclePathGeometryTests=31,CausalMotorPlantTests=27,DynamicMuscleTests=16,DiagnosticBridgeFileTests=6,SolutionManifestTests=1).items():
        code=common.io(package/'source/tests/NeuralResonanceEngine.DNNE.Tests'/(cls+'.cs')).read_text()
        found=re.findall(r'((?:\s*\[[^\n]+\])+\s*)public\s+(?:async\s+)?(?:void|Task)\s+(\w+)\(',code)
        methods={name:(1 if '[Fact]' in attrs else attrs.count('[InlineData(')) for attrs,name in found if '[Fact]' in attrs or '[Theory]' in attrs}
        assert sum(methods.values())==count
        expected.update({'NeuralResonanceEngine.DNNE.Tests.'+cls+'.'+name:n for name,n in methods.items()})
    ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    tests=ET.parse(package/'test-results/focused.trx').findall('.//t:UnitTestResult',ns)
    assert len(tests)==len({t.attrib['testName'] for t in tests})==81 and all(t.attrib['outcome']=='Passed' for t in tests)
    assert Counter(t.attrib['testName'].split('(',1)[0] for t in tests)==expected==Counter(config['ExpectedTestMethods'])
    audit=common.module('frozen_bridge_audit',package/'source/tools/analyse-native-motor-bridge.py')
    analysis=audit.audit(package);saved=common.read(package/'bridge-analysis.json');saved.pop('analysedUtc')
    assert analysis==saved and analysis['completed'] and analysis['originalCriteriaPassed']
    for name in ['src/NRE.SimAvatar/AvatarCausalMotorPlant.cs','src/NRE.SimAvatar/AvatarDynamicMuscle.cs',
        'src/NRE.SimAvatar/AvatarMotorUnitCalcium.cs','src/NRE.SimAvatar/AvatarMusclePath.cs',
        'UnityWorld/Assets/NRE/Runtime/MuscleHingeBridge.cs','UnityWorld/Assets/NRE/Runtime/MuscleHingeWire.cs',
        'UnityWorld/Assets/NRE/Runtime/DiagnosticBridgeFiles.cs','tools/NativeMotorBridgeProbe/NativeHingeBody.cs']:
        assert (ROOT/name).read_text()==common.io(package/'source'/name).read_text(),name
    review.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,passedTests=81,
        compiledUnitySourceFilesVerified=compiled,savedAnalysisExactlyReproduced=True,currentQualifiedSourceMatches=True,
        analysis=analysis,testsExecutedDuringReview=False,modelsSolvedDuringReview=False,originalBoundsChanged=False,
        nativeCommandAndMuscleBridgeChecksPassed=True,coupledTrajectoryAccuracyQualified=False,
        physiologyQualified=False,avatarRecoveryAssessed=False)
    output=ROOT/'artifacts/native-motor-bridge-review-20261116';output.mkdir()
    (output/'review.json').write_text(json.dumps(review,indent=2,allow_nan=False)+'\n')
    print(json.dumps(review,indent=2),flush=True)


if __name__=='__main__':main()
