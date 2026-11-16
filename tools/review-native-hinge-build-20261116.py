"""Review operator-reported Unity import failure; no build, test or simulation."""
from collections import Counter
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import re
import shutil
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]


def main():
    spec=importlib.util.spec_from_file_location('capture_audit',ROOT/'tools/review-olive-and-motor-20261109.py')
    common=importlib.util.module_from_spec(spec);spec.loader.exec_module(common)
    package=ROOT/'artifacts/native-muscle-hinge-20261116-de7da9';revision='301fbfd4fc37f9ee1add8abb1176fc5dd4058b5e'
    review=common.verify(package,['test-binaries.json'],revision,'FAILED',1)
    archive=common.module('source_archive_audit',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    review['verifiedGitFiles']=archive.source_archive(common,package,revision)
    config=common.read(package/'configuration.json');status=common.read(package/'window-status.json')
    assert status['PassedFocusedTests']==75 and status['MusclePathAndRegressionChecksPassed']
    assert not status.get('NativePhysicsAttempted',False) and not (package/'native').exists()
    assert not (package/'player/build-result.json').exists()
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
    work=Path(config['UnityBuildWorkspace'])/'UnityWorld'
    build_entry='Assets/NRE/Editor/MuscleHingeBuild.cs';meta=build_entry+'.meta'
    for name in (build_entry,meta,'Assets/NRE/Runtime/MusclePathHingeQualification.cs'):
        assert common.io(package/'source/UnityWorld'/name).read_bytes()==common.io(work/name).read_bytes()
    metadata=common.io(package/'source/UnityWorld'/meta).read_text()
    guid=re.findall(r'^guid:\s*([^\r\n]+)',metadata,re.M)
    assert guid==['fa45dc6e8f264eaa85e24be67c0424f28'] and len(guid[0])==33
    log=common.io(package/'player/build.log').read_text()
    assert 'The .meta file '+meta+' does not have a valid GUID and its corresponding Asset file will be ignored.' in log
    assert "executeMethod class 'MuscleHingeBuild' could not be found." in log
    rsp=work/'Library/Bee/artifacts/1900b0aE.dag/Assembly-CSharp-Editor.rsp'
    compiler_input=common.io(rsp).read_text()
    assert build_entry not in compiler_input and 'Assets/NRE/Editor/MusclePathBuild.cs' in compiler_input
    validator=common.module('unity_metadata',ROOT/'tools/unity_metadata.py')
    try:validator.validate(package/'source/UnityWorld')
    except ValueError as exc:
        metadata_error=str(exc);assert 'Invalid Unity asset GUID' in metadata_error and 'MuscleHingeBuild.cs.meta' in metadata_error
    else:raise AssertionError('Static metadata preflight failed to reject the original malformed GUID.')
    current=validator.validate(ROOT/'UnityWorld')
    repaired_meta=(ROOT/'UnityWorld'/meta).read_text()
    repaired_guid=re.findall(r'^guid:\s*([^\r\n]+)',repaired_meta,re.M)[0]
    assert len(repaired_guid)==32 and metadata.replace(guid[0],repaired_guid)==repaired_meta
    same=['UnityWorld/Assets/NRE/Runtime/MusclePathHingeQualification.cs','UnityWorld/Assets/NRE/Editor/MuscleHingeBuild.cs','tools/analyse-native-muscle-hinge.py']
    assert all((ROOT/name).read_text()==common.io(package/'source'/name).read_text() for name in same)
    output=ROOT/'artifacts/native-hinge-build-review-20261116';output.mkdir()
    evidence=[rsp,work/'Library/Bee/1900b0aE-inputdata.json',work/'Library/ScriptAssemblies/Assembly-CSharp-Editor.dll',work/'Library/ScriptAssemblies/Assembly-CSharp.dll',work/meta]
    copied=[]
    for path in evidence:
        target=output/path.name;shutil.copyfile(common.io(path),target)
        assert common.digest(path)==common.digest(target)
        copied.append(dict(source=str(path),savedAs=target.name,sha256=common.digest(target)))
    (output/'current-metadata.json').write_text(json.dumps(current,indent=2)+'\n')
    review.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,
        passedTests=75,nativePlayerBuildEntered=False,nativePhysicsExecuted=False,
        cause='The 33-character editor-script GUID was rejected; Unity excluded the build entry from compilation.',
        malformedGuid=guid[0],correctedGuid=repaired_guid,originalMetadataRejectedByPreflight=True,
        currentMetadataFiles=current['metadataFiles'],currentScriptFiles=current['scriptFiles'],currentMetadataPreflightPassed=True,
        nativeHingeCodeAndBoundsUnchanged=True,compiledInputEvidence=copied,
        testsExecutedDuringReview=False,modelsSolvedDuringReview=False)
    (output/'review.json').write_text(json.dumps(review,indent=2)+'\n')
    print(json.dumps({k:v for k,v in review.items() if k!='compiledInputEvidence'},indent=2),flush=True)


if __name__=='__main__':main()
