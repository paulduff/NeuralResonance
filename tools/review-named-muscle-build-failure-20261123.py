"""Verify the operator-reported Unity packaging failure; execute no model/test."""
from collections import Counter
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import shutil
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
PACKAGE = Path('D:/DNNE-build-work/named-muscle-native-20261123-afc890')
REVISION = 'a7fd7ce734552cf3aaf69be0185da65e989936d0'


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec); spec.loader.exec_module(result)
    return result


def main():
    assert '--operator-reported-complete' in sys.argv and __debug__
    output = ROOT/'artifacts'/(PACKAGE.name+'-review'); assert not output.exists()
    common = module('named_failure_files', ROOT/'tools/review-olive-and-motor-20261109.py')
    archive = module('named_failure_archive', ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    result = common.verify(PACKAGE, ['test-binaries.json','probe-binaries.json'], REVISION,
                           expected_status='FAILED', expected_exit=1, progress_every=1000)
    result['verifiedGitFiles'] = archive.source_archive(common, PACKAGE, REVISION)
    config = common.read(PACKAGE/'configuration.json'); status = common.read(PACKAGE/'window-status.json')
    assert status['FrozenInputsPreserved'] and status['ManagedRuntimePreserved'] and status['NamedMuscleProtocolChecksPassed']
    assert not status['NativeUnityExecuted'] and not status['LiveBrainAccessed'] and not status['OldSavesLoaded']
    assert 'Unity build failed' in status['Error']
    ns = {'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    tree = ET.parse(PACKAGE/'test-results/focused.trx')
    methods = {r.attrib['id']: r.find('t:TestMethod',ns).attrib for r in tree.findall('.//t:UnitTest',ns)}
    actual = Counter()
    for row in tree.findall('.//t:UnitTestResult',ns):
        assert row.attrib['outcome']=='Passed'
        method = methods[row.attrib['testId']]
        actual[method['className'].split(',')[0]+'.'+method['name']] += 1
    assert sum(actual.values())==config['ExpectedTestCases']==status['PassedFocusedTests']==359
    assert dict(actual)==config['ExpectedTestMethods']
    assert common.read(PACKAGE/'managed-runtime-before.json')==common.read(PACKAGE/'managed-runtime-after.json')
    build_log = (PACKAGE/'player/build.log').read_text(encoding='utf-8-sig')
    assert 'NullReferenceException' in build_log and 'WorldBuild.Inspect ()' in build_log
    assert 'WorldBuild.cs:10' in build_log and not (PACKAGE/'player/build-result.json').exists()
    assert not (PACKAGE/'native').exists()
    avatar = PACKAGE/'source/UnityWorld/Assets/Avatar'
    provenance = common.read(avatar/'provenance.json')
    missing = [e['file'] for e in provenance['files'] if not (avatar/e['file']).exists()]
    assert sorted(missing)==['MikeAlger.fbx','MikeAlger_Texture.jpeg']
    assert all((avatar/(name+'.meta')).is_file() for name in missing)
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(), operatorReportedCompletion=True,
        passedTests=359, missingAttributedAssets=missing, unityImportFailedBeforeNativeCases=True,
        originalFailurePreserved=True, numericalLimitsChanged=False, modelsExecutedDuringReview=False,
        testsExecutedDuringReview=False, nativeNamedMuscleDeliveryQualified=False, avatarStandingQualified=False)
    output.mkdir()
    (output/'review.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    for name in ('window-status.json','analysis.json','player/build.log'):
        shutil.copyfile(PACKAGE/name,output/Path(name).name)
    shutil.copyfile(__file__,output/Path(__file__).name)
    print(json.dumps(result,indent=2),flush=True)


if __name__=='__main__': main()
