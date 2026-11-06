"""Read-only audit of an operator-reported completed integration run."""
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timezone
from hashlib import sha256
import json
import os
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))


def extended(path):
    name=str(path)
    if os.name!='nt' or name.startswith('\\\\?\\'):return path
    return Path('\\\\?\\UNC\\'+name[2:] if name.startswith('\\\\') else '\\\\?\\'+name)


def digest(path):
    h=sha256()
    with extended(path).open('rb') as stream:
        for block in iter(lambda:stream.read(8*1024*1024),b''):h.update(block)
    return h.hexdigest().upper()


def audit(root,manifest):
    result={}
    for entry in manifest['Files']:
        path=(root/entry['Path']).resolve()
        assert root in path.parents and extended(path).stat().st_size==entry['Bytes']
        key=path.relative_to(root).as_posix().lower();assert key not in result
        result[key]=digest(path);assert result[key]==entry['Sha256'].upper(),str(path)
    io_root=extended(root)
    assert {p.relative_to(io_root).as_posix().lower() for p in io_root.rglob('*') if p.is_file()}==set(result)|{'backup-manifest.json'}
    return result


def main(run,out):
    run=run.resolve();out=out.resolve();assert not out.exists() and out!=run and run not in out.parents
    status=read(run/'window-status.json');config=read(run/'configuration.json');manifest=read(run/'backup-manifest.json')
    backup=Path(config['BackupDestination']).resolve()
    assert run==Path(manifest['Source']).resolve() and backup==Path(manifest['Destination']).resolve()
    assert status['Status']=='FINISHED' and status['ExitCode']==0
    assert status['PassedIsometricPlantAndComponentTests']==status['PassedTests']==config['ExpectedTestCases']==61
    assert status['Revision']==config['Revision']=='33f3914780057d7c2df3794f75c47dc91822bece'
    assert config['IsometricMotorUnitPlant'] and not status['LiveBrainAccessed'] and not status['OldSavesLoaded']
    assert not status['LiveMotorPathEnabled'] and not status['NeuronalSourceQualified'] and not status['AvatarRecoveryAssessed']
    assert digest(run/'backup-manifest.json')==digest(backup/'backup-manifest.json')
    with ThreadPoolExecutor(max_workers=2) as pool:
        left=pool.submit(audit,run,manifest);right=pool.submit(audit,backup,manifest)
        hashes=left.result();assert hashes==right.result()
    for item in config['PackageFiles']:assert hashes[Path(item['Path']).as_posix().lower()]==item['Sha256'].upper()
    for item in read(run/'build-binaries.json'):
        assert hashes[Path(item['Path']).resolve().relative_to(run).as_posix().lower()]==item['Sha256'].upper()
    ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    tests=ET.parse(run/'test-results/motor-axon.trx').findall('.//t:UnitTestResult',ns)
    assert len(tests)==61 and all(x.attrib['outcome']=='Passed' for x in tests)
    expected=dict(MotorAxonTransportTests=32,MotorUnitCalciumTests=13,IsometricMotorUnitPlantTests=15,SolutionManifestTests=1)
    groups={name:sum('.'+name+'.' in x.attrib['testName'] for x in tests) for name in expected}
    assert groups==expected
    source=(run/'source/tests/NeuralResonanceEngine.DNNE.Tests/IsometricMotorUnitPlantTests.cs').read_text(encoding='utf-8-sig')
    methods=re.findall(r'\[Fact\]\s+public void (\w+)\(',source)
    actual=[x.attrib['testName'].rsplit('.',1)[-1] for x in tests if '.IsometricMotorUnitPlantTests.' in x.attrib['testName']]
    assert len(methods)==len(actual)==15 and set(methods)==set(actual)
    report=dict(reviewedUtc=datetime.now(timezone.utc).isoformat(),capture=str(run),backup=str(backup),sourceRevision=config['Revision'],
        completedUtc=status['CompletedUtc'],verifiedFilesPerCopy=len(hashes),verifiedBytesPerCopy=sum(x['Bytes'] for x in manifest['Files']),
        packagedFilesVerified=len(config['PackageFiles']),passedTests=groups,plantCases=sorted(actual),
        fixtureIntegrationPassed=True,neuronalSourceQualified=False,physiologicalCalibrationComplete=False,
        liveMotorPathEnabled=False,avatarRecoveryAssessed=False,fullUnitSuiteAssessed=False)
    out.mkdir(parents=True);(out/'review.json').write_text(json.dumps(report,indent=2,allow_nan=False)+'\n')
    print(json.dumps(report,indent=2),flush=True)


if __name__=='__main__':main(Path(sys.argv[1]),Path(sys.argv[2]))
