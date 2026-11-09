"""Offline component review. Run only after the operator reports completion."""
import argparse
from datetime import datetime, timezone
from hashlib import sha256
import json
import math
import os
from pathlib import Path
import xml.etree.ElementTree as ET

read = lambda p: json.loads(p.read_text(encoding='utf-8-sig'))


def io_path(path):
    path = path.absolute()
    if os.name == 'nt' and not str(path).startswith('\\\\?\\'):
        path = Path('\\\\?\\' + str(path))
    return path


def digest(path):
    return sha256(io_path(path).read_bytes()).hexdigest().upper()


def main(capture, output):
    manifest = read(capture/'backup-manifest.json'); backup = Path(manifest['Destination'])
    assert digest(capture/'backup-manifest.json') == digest(backup/'backup-manifest.json')
    expected = {e['Path'].replace('\\','/') for e in manifest['Files']}
    assert len(expected) == len(manifest['Files'])
    for folder in (capture,backup):
        directory=io_path(folder)
        actual = {p.relative_to(directory).as_posix() for p in directory.rglob('*') if p.is_file()} - {'backup-manifest.json'}
        assert expected == actual, 'Backup is not a complete matching capture.'
        for entry in manifest['Files']:
            assert digest(folder/entry['Path']) == entry['Sha256'], entry['Path']
    config = read(capture/'configuration.json'); status = read(capture/'window-status.json')
    assert status['Status']=='DIAGNOSTICS_COMPLETE' and status['ExitCode']==0
    order=config.get('SolverOrder',2);assert order in (2,3)
    expected_count=32 if order==2 else 43
    assert config['Revision']==status['Revision'] and config['ExpectedTestCases']==status['PassedFocusedTests']==expected_count
    classes = (['GolgiSdirk3Tests'] if order==3 else [])+['GolgiSdirkTests','GolgiReferenceCellTests','GolgiChannelKineticsTests','SolutionManifestTests']
    assert config['TestClasses']==classes and config['NoLiveMonitoring']
    for entry in config['PackageFiles']:
        assert digest(capture/entry['Path'])==entry['Sha256']
    for entry in read(capture/'test-binaries.json'):
        path = Path(entry['Path']); path.relative_to(capture/'source/artifacts')
        assert digest(path)==entry['Sha256']
    ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    trx=ET.parse(capture/'test-results/focused.trx')
    cases=trx.findall('.//t:UnitTestResult',ns)
    assert len(cases)==expected_count and all(c.attrib['outcome']=='Passed' for c in cases)
    counts={name:sum(c.attrib['testName'].startswith('NeuralResonanceEngine.DNNE.Tests.'+name+'.') for c in cases) for name in classes}
    assert list(counts.values())==(([11] if order==3 else [])+[7,8,16,1])
    refinements=[]
    if order==3:
        assert status['SolverOrder']==3 and config['TestClassCounts']==counts
        for case in cases:
            if 'GolgiSdirk3Tests.ActiveStateRefinesAndRetainsIndependentStageAccounting' not in case.attrib['testName']:continue
            text=case.findtext('t:Output/t:StdOut','',ns)
            rows=[json.loads(line[line.index('{'):]) for line in text.splitlines() if '{"block":' in line]
            assert len(rows)==4
            for row in rows:
                assert len(row['errors'])==3 and all(math.isfinite(v) and v>=0 for v in row['errors'])
                assert row['steps']==[.015625,.0078125,.00390625,.00048828125] and row['refinementFactor']==.2
                assert row['roundoffFloor']=={'voltageMv':2e-7,'gates':2e-9,'sk':2e-9,'calciumUm':2e-7}[row['group']]
                assert all(fine<=.2*coarse+row['roundoffFloor'] for coarse,fine in zip(row['errors'],row['errors'][1:]))
                row['aboveFloorRatios']=[coarse/fine if fine>row['roundoffFloor'] else None for coarse,fine in zip(row['errors'],row['errors'][1:])]
            refinements+=rows
        assert len(refinements)==16 and {(r['block'],r['group']) for r in refinements}=={
            (block,group) for block in ('None','WithoutPersistentSodium','WithoutHcn','WithoutSk')
            for group in ('voltageMv','gates','sk','calciumUm')}
    sources=['GolgiSdirkCell.cs','GolgiReferenceCell.cs','GolgiChannelKinetics.cs','GolgiCable.cs']
    report=dict(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,capture=str(capture),
        backup=str(backup),captureManifestSha256=digest(capture/'backup-manifest.json'),sourceRevision=config['Revision'],
        completedUtc=status['CompletedUtc'],verifiedFilesPerCopy=len(expected),verifiedBytesPerCopy=sum(e['Bytes'] for e in manifest['Files']),
        componentTestsPassed=expected_count,testCounts=counts,componentChecksPassed=True,
        sourceFiles={n:digest(capture/'source/Structures/_SharedRuntime'/n) for n in sources},
        longTrajectoryQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    if order==3:report.update(solverOrder=3,activeRefinement=refinements)
    output.mkdir()
    (output/'review.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(report,indent=2))


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('capture',type=Path); parser.add_argument('output',type=Path)
    parser.add_argument('--operator-reported',action='store_true',required=True)
    args=parser.parse_args(); main(args.capture.resolve(),args.output.resolve())
