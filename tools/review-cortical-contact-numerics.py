"""Read-only audit of the operator-reported cortical precision experiment."""
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timezone
import importlib.util
import json
import math
from pathlib import Path
import struct
import sys

read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
lines=lambda p:[json.loads(s) for s in p.read_text(encoding='utf-8-sig').splitlines()]
f32=lambda x:struct.unpack('<f',struct.pack('<f',x))[0]
module=importlib.util.spec_from_file_location('capture_audit',Path(__file__).with_name('review-isometric-plant.py'))
audit=importlib.util.module_from_spec(module);module.loader.exec_module(audit)

def main(run,out):
    run=run.resolve();out=out.resolve();assert not out.exists() and out!=run and run not in out.parents
    status=read(run/'window-status.json');config=read(run/'configuration.json');manifest=read(run/'backup-manifest.json')
    backup=Path(config['BackupDestination']).resolve()
    assert status['Revision']==config['Revision']=='6445104403219786dfb8f9edd4366c825eadcaaa'
    assert status['Status']=='DIAGNOSTICS_COMPLETE' and status['ExitCode']==0 and status['IsolatedNumericalCriteriaPassed']
    assert not status['LiveBrainAccessed'] and not status['OldSavesLoaded'] and not status['BiologicalConsolidationQualified']
    assert Path(manifest['Source']).resolve()==run and Path(manifest['Destination']).resolve()==backup
    assert audit.digest(run/'backup-manifest.json')==audit.digest(backup/'backup-manifest.json')
    with ThreadPoolExecutor(max_workers=2) as pool:
        left=pool.submit(audit.audit,run,manifest);right=pool.submit(audit.audit,backup,manifest)
        hashes=left.result();assert hashes==right.result()
    for item in config['PackageFiles']:assert hashes[Path(item['Path']).as_posix().lower()]==item['Sha256']
    for item in read(run/'probe-binaries.json'):assert hashes[Path(item['Path']).resolve().relative_to(run).as_posix().lower()]==item['Sha256']
    assert read(run/'study/results.json')==read(run/'input/completed-replay-results.json')
    result=read(run/'numerical-study/results.json');analysis=read(run/'numerical-study/analysis.json')
    protocol=read(run/'numerical-study/protocol.json')
    assert result['completed'] and result['numericalCriteriaPassed'] and result['invalidInputAtomicityPassed']
    assert analysis['completed'] and analysis['isolatedNumericalCriteriaPassed'] and analysis['originalCompleteBaselineReproduced']
    assert protocol['version']==result['version']=='isolated-cortical-contact-double-v1'
    assert protocol['maximumAbsoluteWeightError']==3e-8 and protocol['finestAbsoluteWeightError']==1e-9
    assert protocol['refinementRatio']==1.1 and protocol['refinementRoundoffFloor']==1e-11
    assert len(result['cadenceCases'])==27 and result['cadenceCases']==analysis['cadence']
    expected={(p,h) for p in ('pre-post','post-pre','unpaired') for h in (4,2,1,.5,.25,.125,.0625,.03125,.015625)}
    previous={};cadence=[]
    for row in result['cadenceCases']:
        key=row['pairing'],row['stepMs'];assert key in expected;expected.remove(key)
        a=row['initial'];b=row['final'];assert b['timeMs']-a['timeMs']==1000
        integral=f32(.35)*(f32(.35)+f32(.65))*f32(.02)*a['tag']*8*(1-math.exp(-1/8))/5
        limit=5-(5-a['weight'])*math.exp(-integral) if a['tag']>=0 else a['weight']*math.exp(integral)
        assert abs(limit-row['continuousLimit'])<=8*math.ulp(limit)
        error=abs(b['weight']-row['continuousLimit']);assert error==row['absoluteError']
        assert error<=3e-8 and (key[1]!=.015625 or error<=1e-9)
        assert error<=max(1e-11,1.1*previous.get(key[0],math.inf));previous[key[0]]=error
        assert (b['weight']>1 if key[0]=='pre-post' else b['weight']<1 if key[0]=='post-pre' else b['weight']==1)
        assert row['passed'] and row['directionPassed'] and row['maximumChargedWithoutWeight']==0 and b['budget']>0
        cadence.append(dict(pairing=key[0],stepMs=key[1],weight=b['weight'],absoluteError=error))
    assert not expected
    seen=set()
    for summary in result['replayCases']:
        name,mode=summary['key'],summary['intervention'];assert (name,mode) not in seen;seen.add((name,mode))
        original=lines(run/'input'/(name+'.jsonl'));rows=lines(run/'numerical-study'/(name+'-'+mode+'.jsonl'))
        assert len(rows)==len(original)==summary['eventCount']
        state=summary['initial'];change=dict(pre=0.,post=0.,modulation=0.);lost=0;charged=0
        for index,(row,event) in enumerate(zip(rows,original,strict=True)):
            assert row['index']==index and row['before']==state
            for key in ('kind','messageId','timestampMs','elapsedMs','activity','teaching'):assert row[key]==event[key]
            assert row['after']['contactId']==state['contactId']==event['before']['contactId']
            assert row['after']['timeMs']==event['timestampMs']
            assert all(math.isfinite(v) for v in row['after'].values() if isinstance(v,(int,float)))
            delta=row['after']['weight']-state['weight'];change[row['kind']]+=delta
            charge=row['after']['chargedPlasticity']-state['chargedPlasticity']
            if charge>0 and delta==0:lost+=1;charged+=charge
            state=row['after']
        assert state==summary['final'] and change==summary['weightChangeByKind']
        assert lost==summary['lostUpdates']==0 and charged==summary['lostCharge']==0
    assert seen=={(n,m) for n in ('continuous-colocated','continuous-neighbor','continuous-simultaneous','continuous-unmodulated')
        for m in ('RecordedSignals','NoTagCapture','NoDopamine','NoAcetylcholine')}
    report=dict(reviewedUtc=datetime.now(timezone.utc).isoformat(),capture=str(run),backup=str(backup),sourceRevision=config['Revision'],
        completedUtc=status['CompletedUtc'],verifiedFilesPerCopy=len(hashes),verifiedBytesPerCopy=sum(e['Bytes'] for e in manifest['Files']),
        packagedFilesVerified=len(config['PackageFiles']),completeLegacyBaselineReproduced=True,
        candidateReplayCases=16,candidateCadenceCases=27,isolatedNumericalCriteriaPassed=True,
        maximumAbsoluteWeightError=max(c['absoluteError'] for c in cadence),
        maximumFinestWeightError=max(c['absoluteError'] for c in cadence if c['stepMs']==.015625),
        cadence=cadence,interventions=analysis['cases'],biologicalConsolidationQualified=False,
        liveIntegrationQualified=False,avatarRecoveryAssessed=False)
    out.mkdir(parents=True);(out/'review.json').write_text(json.dumps(report,indent=2,allow_nan=False)+'\n')
    print(json.dumps({k:v for k,v in report.items() if k not in ('cadence','interventions')},indent=2))

if __name__=='__main__':main(Path(sys.argv[1]),Path(sys.argv[2]))
