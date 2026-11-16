"""Review the completed hinge capture using saved data only."""
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
    package=ROOT/'artifacts/native-muscle-hinge-20261116-5a98f8';revision='200d8042ac2622fb693dabae5c0ced473832fb83'
    review=common.verify(package,['test-binaries.json'],revision)
    archive=common.module('source_archive_audit',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    review['verifiedGitFiles']=archive.source_archive(common,package,revision)
    for entry in common.read(package/'player-files.json'):
        Path(entry['Path']).relative_to(package/'player');assert common.digest(entry['Path'])==entry['Sha256']
    source=common.io(package/'source/UnityWorld');compiled=0
    for path in source.rglob('*.cs'):
        assert path.read_bytes()==common.io(package/'built-project'/path.relative_to(source)).read_bytes();compiled+=1
    build=common.read(package/'player/build-result.json')
    assert build['result']=='Succeeded' and build['errors']==0
    config=common.read(package/'configuration.json');status=common.read(package/'window-status.json')
    assert status['NativeExitCode']==0 and status['NativeHingeChecksPassed'] and status['PassedFocusedTests']==75
    assert common.digest(config['UnityExecutable'])==config['UnityExecutableSha256']
    assert common.digest(config['PythonExecutable'])==config['PythonExecutableSha256']
    metadata=common.module('frozen_metadata',package/'source/tools/unity_metadata.py').validate(package/'source/UnityWorld')
    assert metadata==common.read(package/'unity-metadata.json')
    counts=dict(MusclePathGeometryTests=31,CausalMotorPlantTests=27,DynamicMuscleTests=16,SolutionManifestTests=1)
    assert config['ExpectedClassCounts']==counts and config['ExpectedTestCases']==75
    expected=Counter()
    for cls,count in counts.items():
        code=common.io(package/'source/tests/NeuralResonanceEngine.DNNE.Tests'/(cls+'.cs')).read_text()
        found=re.findall(r'((?:\s*\[[^\n]+\])+\s*)public\s+(?:async\s+)?(?:void|Task)\s+(\w+)\(',code)
        methods={name:(1 if '[Fact]' in attrs else attrs.count('[InlineData(')) for attrs,name in found if '[Fact]' in attrs or '[Theory]' in attrs}
        assert sum(methods.values())==count
        expected.update({'NeuralResonanceEngine.DNNE.Tests.'+cls+'.'+name:n for name,n in methods.items()})
    assert dict(expected)==config['ExpectedTestMethods']
    ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    tests=ET.parse(package/'test-results/focused.trx').findall('.//t:UnitTestResult',ns)
    assert len(tests)==len({t.attrib['testName'] for t in tests})==75 and all(t.attrib['outcome']=='Passed' for t in tests)
    assert Counter(t.attrib['testName'].split('(',1)[0] for t in tests)==expected
    print('Full C:/D: capture, source, binaries and regression coverage verified.',flush=True)
    audit=common.module('frozen_hinge_analysis',package/'source/tools/analyse-native-muscle-hinge.py')
    analysis=audit.audit(package);saved=common.read(package/'hinge-analysis.json');saved.pop('analysedUtc')
    assert analysis==saved and analysis['completed'] and analysis['originalCriteriaPassed']
    current=['src/NRE.SimAvatar/AvatarCausalMotorPlant.cs','src/NRE.SimAvatar/AvatarDynamicMuscle.cs',
        'src/NRE.SimAvatar/AvatarMusclePath.cs','UnityWorld/Assets/NRE/Runtime/MusclePathGeometry.cs',
        'UnityWorld/Assets/NRE/Runtime/MusclePathHingeQualification.cs','UnityWorld/Assets/NRE/Editor/MuscleHingeBuild.cs','tools/analyse-native-muscle-hinge.py']
    for name in current:assert (ROOT/name).read_text()==common.io(package/'source'/name).read_text(),name
    native=common.read(package/'native/results.json');by_step=[]
    for step in sorted({c['stepSeconds'] for c in native['cases']},reverse=True):
        keys={c['key'] for c in native['cases'] if c['stepSeconds']==step}
        cases=[c for c in analysis['cases'] if c['key'] in keys]
        by_step.append(dict(actualStepSeconds=step,cases=len(cases),maximumErrors={k:max(c['metrics'][k] for c in cases) for k in cases[0]['metrics']}))
    review.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,
        passedTests=75,passedNativeCases=24,actualPhysicsSteps=72,compiledUnitySourceFilesVerified=compiled,
        savedAnalysisExactlyReproduced=True,metadataVerified=True,currentQualifiedSourceMatches=True,
        originalBoundsChanged=False,analysis=analysis,byPhysicalStep=by_step,nativeHingeChecksPassed=True,
        coupledNativeTrajectoryQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False,
        testsExecutedDuringReview=False,modelsSolvedDuringReview=False)
    output=ROOT/'artifacts/native-hinge-review-20261116';output.mkdir()
    (output/'review.json').write_text(json.dumps(review,indent=2,allow_nan=False)+'\n')
    print(json.dumps({k:v for k,v in review.items() if k!='analysis'},indent=2),flush=True)


if __name__=='__main__':main()
