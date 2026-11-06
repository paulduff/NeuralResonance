"""Audit an operator-reported contact replay without executing learning."""
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import struct
import sys

read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
f32=lambda x:struct.unpack('<f',struct.pack('<f',x))[0]
module=importlib.util.spec_from_file_location('capture_audit',Path(__file__).with_name('review-isometric-plant.py'))
audit=importlib.util.module_from_spec(module);module.loader.exec_module(audit)

def main(run,out):
    run=run.resolve();out=out.resolve();assert not out.exists() and run not in out.parents
    config=read(run/'configuration.json');status=read(run/'window-status.json');manifest=read(run/'backup-manifest.json')
    backup=Path(config['BackupDestination']).resolve()
    assert status['Revision']==config['Revision']=='10228ade65624aa3741c745b2d284a46d52a5762'
    assert status['Status']=='DIAGNOSTICS_COMPLETE' and status['ExitCode']==0 and status['RecordedContactStatesReproduced']
    assert Path(manifest['Source']).resolve()==run and Path(manifest['Destination']).resolve()==backup
    assert audit.digest(run/'backup-manifest.json')==audit.digest(backup/'backup-manifest.json')
    with ThreadPoolExecutor(max_workers=2) as pool:
        left=pool.submit(audit.audit,run,manifest);right=pool.submit(audit.audit,backup,manifest)
        hashes=left.result();assert hashes==right.result()
    for item in config['PackageFiles']:assert hashes[Path(item['Path']).as_posix().lower()]==item['Sha256']
    for item in read(run/'probe-binaries.json'):assert hashes[Path(item['Path']).resolve().relative_to(run).as_posix().lower()]==item['Sha256']
    spec=read(run/'input/input.json');result=read(run/'study/results.json');analysis=read(run/'study/analysis.json')
    assert result['completed'] and analysis['completed'] and analysis['exactRecordedReplays']==4
    assert len(result['replayCases'])==analysis['replayCases']==16 and len(result['cadenceCases'])==analysis['cadenceCases']==27
    for case in spec['cases']:
        raw=(run/'input'/case['file']).read_bytes();assert audit.digest(run/'input'/case['file'])==case['sha256']
        origin=Path(spec['originalCapture'])/case['originalFile']
        assert audit.digest(origin)==case['originalSha256']==audit.digest(Path(spec['originalBackup'])/case['originalFile'])
        lines=origin.read_text(encoding='utf-8-sig').splitlines()
        assert raw==('\n'.join(lines[i-1] for i in case['originalLineNumbers'])+'\n').encode('utf-8')
        events=[json.loads(line) for line in raw.splitlines()]
        for mode in spec['interventions']:
            rows=[json.loads(line) for line in (run/'study'/(case['key']+'-'+mode+'.jsonl')).read_text().splitlines()]
            summary=next(x for x in result['replayCases'] if x['key']==case['key'] and x['intervention']==mode)
            assert len(rows)==len(events)==case['events']==summary['eventCount']
            changes=dict(pre=0.,post=0.,modulation=0.)
            for row,event in zip(rows,events,strict=True):
                for key in ('kind','messageId','timestampMs','elapsedMs','activity','teaching'):assert row[key]==event[key]
                if mode=='Recorded':assert row['before']==event['before'] and row['after']==event['after']
                changes[row['kind']]+=f32(row['after']['weight'])-f32(row['before']['weight'])
            assert changes==summary['weightChangeByKind'] and rows[-1]['after']==summary['final']
    assert result['cadenceCases']==analysis['cadence']
    cadence=[dict(pairing=c['pairing'],stepMs=c['stepMs'],finalWeight=f32(c['final']['weight']),
        continuousLimit=c['continuousLimit'],absoluteError=c['absoluteWeightDifference'],
        chargedPlasticity=c['final']['absoluteChange'],updates=c['final']['updates']) for c in result['cadenceCases']]
    report=dict(reviewedUtc=datetime.now(timezone.utc).isoformat(),capture=str(run),backup=str(backup),sourceRevision=config['Revision'],
        completedUtc=status['CompletedUtc'],verifiedFilesPerCopy=len(hashes),verifiedBytesPerCopy=sum(e['Bytes'] for e in manifest['Files']),
        packagedFilesVerified=len(config['PackageFiles']),exactRecordedReplays=4,interventionCases=16,cadenceCases=27,
        interventionSummaries=analysis['cases'],cadence=cadence,numericalCadenceDefectObserved=True,
        biologicalConsolidationQualified=False,liveBrainAccessed=False,avatarRecoveryAssessed=False)
    out.mkdir(parents=True);(out/'review.json').write_text(json.dumps(report,indent=2,allow_nan=False)+'\n')
    print(json.dumps({k:v for k,v in report.items() if k not in ('cadence','interventionSummaries')},indent=2))

if __name__=='__main__':main(Path(sys.argv[1]),Path(sys.argv[2]))
