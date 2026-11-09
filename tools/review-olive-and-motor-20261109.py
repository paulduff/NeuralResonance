"""Review operator-reported CPU batch and motor arithmetic captures; no integration."""
from datetime import datetime, timezone
from decimal import Decimal
from hashlib import sha256
import importlib.util
import json
import os
from pathlib import Path
import xml.etree.ElementTree as ET

def io(path):
    path=Path(path).absolute(); value=str(path)
    return Path('\\\\?\\'+value) if os.name=='nt' and not value.startswith('\\\\?\\') else path
ROOT=Path(__file__).resolve().parents[1]
read=lambda p:json.loads(io(p).read_text(encoding='utf-8-sig'))
def digest(path):
    h=sha256()
    with io(path).open('rb') as stream:
        for block in iter(lambda:stream.read(4*1024*1024),b''): h.update(block)
    return h.hexdigest().upper()
def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path); result=importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result); return result
def verify(capture,binaries,revision,expected_status='DIAGNOSTICS_COMPLETE',expected_exit=0):
    manifest=read(capture/'backup-manifest.json'); backup=Path(manifest['Destination'])
    assert digest(capture/'backup-manifest.json')==digest(backup/'backup-manifest.json')
    names={e['Path'].replace('\\','/') for e in manifest['Files']}; assert len(names)==len(manifest['Files'])
    for folder in (capture,backup):
        root=io(folder)
        assert {p.relative_to(root).as_posix() for p in root.rglob('*') if p.is_file()}==names|{'backup-manifest.json'}
        for entry in manifest['Files']:
            path=folder/entry['Path']
            assert io(path).stat().st_size==entry['Bytes'] and digest(path)==entry['Sha256'],str(path)
    config=read(capture/'configuration.json'); status=read(capture/'window-status.json')
    assert status['Status']==expected_status and status['ExitCode']==expected_exit
    assert config['Revision']==status['Revision']==revision
    for entry in config['PackageFiles']: assert digest(capture/entry['Path'])==entry['Sha256']
    for name in binaries:
        for entry in read(capture/name):
            path=Path(entry['Path']); path.relative_to(capture/'source/artifacts')
            assert digest(path)==entry['Sha256']
    return dict(capture=str(capture),backup=str(backup),sourceRevision=revision,completedUtc=status['CompletedUtc'],
        verifiedFilesPerCopy=len(names),verifiedBytesPerCopy=sum(e['Bytes'] for e in manifest['Files']),
        verifiedPackageFiles=len(config['PackageFiles']),manifestSha256=digest(capture/'backup-manifest.json'))


def main():
    output=ROOT/'artifacts/olive-motor-review-20261109'; output.mkdir()
    olive=ROOT/'artifacts/olive-batch-20261109-11018c'
    olive_review=verify(olive,['test-binaries.json','probe-binaries.json'],'a723d71f92bd3a0bb5a424cb5123f28c47429a4b')
    ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    reports=list((olive/'test-results').glob('*.trx')); assert len(reports)==1
    cases=ET.parse(reports[0]).findall('.//t:UnitTestResult',ns)
    assert len(cases)==87 and all(c.attrib['outcome']=='Passed' for c in cases)
    config=read(olive/'configuration.json')
    classes={c.attrib['testName'].rsplit('.',1)[0] for c in cases}
    analysis=module('review_olive_batch',ROOT/'tools/analyse-olive-batch.py').audit(olive/'study')
    assert analysis==read(olive/'study/analysis.json') and analysis['exactBatchCases']==6 and analysis['comparisons']==12
    olive_review.update(passedTests=87,testClasses=sorted(classes),savedBytesReverified=True,analysis=analysis)
    (output/'olive-review.json').write_text(json.dumps(olive_review,indent=2)+'\n',encoding='utf-8')
    print('Olive: 87 tests, six complete cases and twelve exact contrasts verified with C:/D: copies.',flush=True)

    motor=ROOT/'artifacts/motor-precision-20261109-7703bd'
    motor_review=verify(motor,['binaries.json'],'dc3a789be10fde11c194c74fd3a1865dffdf39b7')
    analysis=module('review_motor_precision',ROOT/'tools/motoneuron-fixed-state-precision.py').audit(motor)
    assert analysis==read(motor/'precision-analysis.json') and analysis['evaluatedPoints']==396
    assert not analysis['rejectedReferencePoints'] and not analysis['originalBoundsChanged'] and not analysis['odeSolved']
    scenarios=[]
    for name in analysis['scenarios']:
        rows=[r for r in analysis['rows'] if r['scenario']==name]
        scenarios.append(dict(scenario=name,points=len(rows),
            maximumVoltageRhsDifferenceMvPerMs=max(r['maximumVoltageRhsDifferenceMvPerMs'] for r in rows),
            maximumGateRhsDifferencePerMs=max(r['maximumGateRhsDifferencePerMs'] for r in rows),
            maximumCalciumRhsDifferenceMmPerMs=max(r['calciumRhsDifferenceMmPerMs'] for r in rows),
            maximumCandidateCalciumBiasTimesRemovalMs=str(max(abs(Decimal(r['candidateCalciumLocalBiasTimesRemovalMs'])) for r in rows)),
            maximumReferenceCalciumBiasTimesRemovalMs=str(max(abs(Decimal(r['referenceCalciumLocalBiasTimesRemovalMs'])) for r in rows))))
    motor_review.update(recomputedArithmeticPoints=396,rejectedReferencePoints=0,scenarios=scenarios,geometry=analysis['geometry'],
        calciumReversal=analysis['calciumReversal'],originalNumericalStatus='FAILED',originalBoundsChanged=False,
        odeSolved=False,causalAttributionEstablished=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    (output/'motor-review.json').write_text(json.dumps(motor_review,indent=2)+'\n',encoding='utf-8')
    (output/'review.json').write_text(json.dumps(dict(reviewedUtc=datetime.now(timezone.utc).isoformat(),
        operatorReportedCompletion=True,olive=olive_review,motor=motor_review),indent=2)+'\n',encoding='utf-8')
    print(json.dumps(motor_review,indent=2),flush=True)


if __name__=='__main__': main()
