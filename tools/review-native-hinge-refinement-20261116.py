"""Review the operator-reported refinement capture; read saved data only."""
from collections import Counter
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]


def main():
    spec=importlib.util.spec_from_file_location('capture_audit',ROOT/'tools/review-olive-and-motor-20261109.py')
    common=importlib.util.module_from_spec(spec);spec.loader.exec_module(common)
    package=ROOT/'artifacts/native-hinge-refinement-20261116-421387'
    revision='e59d9ea89adc049376b71bd933fa40986cb3d196'
    print('Verifying every completed C:/D: file and executed binary...',flush=True)
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
    assert status['PassedFocusedTests']==93 and status['NativeBridgeChecksPassed'] and status['NativeDriverSucceeded']
    assert status['NativeRefinementPassed'] and status['PassedNativeCases']==60 and status['ActualPhysicsSteps']==26624
    assert status['ReferenceEnvironmentUnchanged'] and not status['NeuronalModelExecuted']
    metadata=common.module('frozen_metadata',package/'source/tools/unity_metadata.py').validate(package/'source/UnityWorld')
    assert metadata==common.read(package/'unity-metadata.json')
    expected=Counter()
    counts=dict(MusclePathGeometryTests=31,CausalMotorPlantTests=27,DynamicMuscleTests=16,
                DiagnosticBridgeFileTests=6,MuscleHingeClockTests=12,SolutionManifestTests=1)
    for cls,count in counts.items():
        code=common.io(package/'source/tests/NeuralResonanceEngine.DNNE.Tests'/(cls+'.cs')).read_text()
        found=re.findall(r'((?:\s*\[[^\n]+\])+\s*)public\s+(?:async\s+)?(?:void|Task)\s+(\w+)\(',code)
        methods={name:(1 if '[Fact]' in attrs else attrs.count('[InlineData(')) for attrs,name in found if '[Fact]' in attrs or '[Theory]' in attrs}
        assert sum(methods.values())==count
        expected.update({'NeuralResonanceEngine.DNNE.Tests.'+cls+'.'+name:n for name,n in methods.items()})
    ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    tests=ET.parse(package/'test-results/focused.trx').findall('.//t:UnitTestResult',ns)
    assert len(tests)==len({t.attrib['testName'] for t in tests})==93 and all(t.attrib['outcome']=='Passed' for t in tests)
    assert Counter(t.attrib['testName'].split('(',1)[0] for t in tests)==expected==Counter(config['ExpectedTestMethods'])
    sys.path.insert(0,str(package/'source/tools'))
    bridge=common.module('frozen_bridge',package/'source/tools/analyse-native-motor-bridge.py')
    for condition in config['RefinementConditions']:
        print('Reproducing saved command/receipt audit: '+condition['key'],flush=True)
        folder=package/'conditions'/condition['key']
        result=bridge.audit(folder,condition['physicsResolution'],condition['muscleSubsteps'])
        saved=common.read(folder/'bridge-analysis.json');saved.pop('analysedUtc')
        assert result==saved and result['originalCriteriaPassed'] and result['completedReplyLockControlPassed']
    print('Reproducing saved-array convergence and reference comparison...',flush=True)
    audit=common.module('frozen_refinement',package/'source/tools/analyse-native-hinge-refinement.py')
    analysis=audit.audit(package);assert analysis==common.read(package/'refinement-analysis.json')
    assert analysis['completed'] and analysis['numericalCriteriaPassed'] and not analysis['originalBoundsChanged']
    same=['src/NRE.SimAvatar/AvatarCausalMotorPlant.cs','src/NRE.SimAvatar/AvatarDynamicMuscle.cs',
          'src/NRE.SimAvatar/AvatarMotorUnitCalcium.cs','src/NRE.SimAvatar/AvatarMusclePath.cs',
          'UnityWorld/Assets/NRE/Runtime/MuscleHingeBridge.cs','UnityWorld/Assets/NRE/Runtime/MuscleHingeWire.cs',
          'UnityWorld/Assets/NRE/Runtime/DiagnosticBridgeFiles.cs','tools/NativeMotorBridgeProbe/NativeHingeBody.cs']
    for name in same:assert (ROOT/name).read_text()==common.io(package/'source'/name).read_text(),name
    maxima={}
    for condition in config['RefinementConditions']:
        rows=[r for r in analysis['conditions'] if r['condition']==condition['key']]
        maxima[condition['key']]={key:max(c['errors'][key] for r in rows for c in r['contrasts'])
                                  for key in rows[0]['contrasts'][0]['errors']}
    review.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,
        passedTests=93,compiledUnitySourceFilesVerified=compiled,savedBridgeAnalysesExactlyReproduced=5,
        savedAnalysisExactlyReproduced=True,currentQualifiedSourceMatches=True,qualifiedSourcePreserved=same,
        nativeSteps=26624,nativePhysicalConditions=20,nativeRejectionControls=40,
        physicalRefinementContrasts=8,muscleRefinementContrasts=8,baselineRepeatsBitwiseEqual=4,
        maximumErrorsByResolution=maxima,analysis=analysis,numericalCriteriaPassed=True,
        testsExecutedDuringReview=False,modelsSolvedDuringReview=False,originalBoundsChanged=False,
        sampledPhysicalConvergenceQualified=True,muscleStepSensitivityQualified=True,
        finestHalfStepReferenceSamplesAssessed=False,fullCoupledTrajectoryQualified=False,
        neuronalModelExecuted=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    assert all(r['observedStateBitwiseEqual'] for r in analysis['baselineRepeat'])
    output=ROOT/'artifacts/native-hinge-refinement-review-20261116';output.mkdir()
    (output/'review.json').write_text(json.dumps(review,indent=2,allow_nan=False)+'\n')
    print(json.dumps({k:v for k,v in review.items() if k not in ('analysis','maximumErrorsByResolution','qualifiedSourcePreserved')},indent=2),flush=True)


if __name__=='__main__':main()
