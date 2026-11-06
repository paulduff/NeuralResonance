"""Read-only review of the operator-reported Golgi cell and motor captures.

No ODE integration or tests are run. Completed records and both backup copies
are hashed; full reference state differences are recalculated from saved NPZs.
"""
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET
import numpy as np

read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))


def digest(path):
    h=hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda:stream.read(8*1024*1024),b''):h.update(block)
    return h.hexdigest().upper()


def audit(run):
    manifest=read(run/'backup-manifest.json');backup=Path(manifest['Destination'])
    assert digest(run/'backup-manifest.json')==digest(backup/'backup-manifest.json')
    assert backup==Path(read(run/'configuration.json')['BackupDestination'])
    def verify(base):
        for row in manifest['Files']:
            p=(base/row['Path']).resolve();assert base.resolve() in p.parents
            assert p.stat().st_size==row['Bytes'] and digest(p)==row['Sha256'].upper(),str(p)
        return len(manifest['Files'])
    with ThreadPoolExecutor(max_workers=2) as pool:
        counts=list(pool.map(verify,(run,backup)))
    config=read(run/'configuration.json')
    for row in config['PackageFiles']:assert digest(run/row['Path'])==row['Sha256'].upper(),row['Path']
    return dict(capture=str(run),backup=str(backup),verifiedFilesPerCopy=counts[0],
        verifiedBytesPerCopy=sum(row['Bytes'] for row in manifest['Files']),revision=config['Revision'])


def main(golgi,motor,output):
    if output.exists():raise ValueError('Use a new review directory.')
    g=audit(golgi);m=audit(motor)
    gs=read(golgi/'window-status.json');ms=read(motor/'window-status.json')
    assert gs['Status']=='FAILED' and gs['ExitCode']==1 and not gs['LiveBrainAccessed']
    root=ET.parse(golgi/'test-results/channels.trx')
    cases=root.findall('.//{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}UnitTestResult')
    assert len(cases)==25 and all(c.attrib['outcome']=='Passed' for c in cases)
    passive=read(golgi/'study/cell-analysis.json');active=read(golgi/'study/active-reference-analysis.json')
    findings=[]
    for name in ('quiet','depolarize','hyperpolarize','no-nap','no-hcn','no-sk'):
        with np.load(golgi/f'study/active-reference/{name}-standard.npz',allow_pickle=False) as f:
            a={k:f[k] for k in f.files}
        with np.load(golgi/f'study/active-reference/{name}-strict.npz',allow_pickle=False) as f:
            b={k:f[k] for k in f.files}
        assert np.array_equal(a['times'],b['times']) and a['states'].shape==b['states'].shape==(24001,159)
        assert len(a['spikes'])==len(b['spikes'])
        difference=np.abs(a['states']-b['states']);i=int(np.argmax(difference[:,0]))
        errors=np.abs(a['spikes']-b['spikes'])
        saved=next(c for c in active['comparisons'] if c['protocol']==name)
        assert float(difference[:,0].max())==saved['referenceToleranceSomaDifferenceMv']
        assert float(errors.max(initial=0))==saved['referenceToleranceOrdinalDifferenceMs']
        findings.append(dict(protocol=name,spikeCount=len(a['spikes']),maximumSpikeDifferenceMs=float(errors.max(initial=0)),
            maximumSomaDifferenceMv=float(difference[:,0].max()),atTimeMs=float(a['times'][i]),
            voltagesAtMaximumMv=[float(a['states'][i,0]-55),float(b['states'][i,0]-55)],
            nearestStrictSpikeDistanceMs=float(np.min(np.abs(b['spikes']-a['times'][i]))) if len(b['spikes']) else None,
            maximumCableDifferenceMv=float(difference[:,:133].max()),maximumGateDifference=float(difference[:,133:151].max()),
            maximumSkDifference=float(difference[:,151:157].max()),maximumCalciumDifferenceMm=float(difference[:,157:].max()/1000),
            rmsSomaDifferenceMv=float(np.sqrt(np.mean(difference[:,0]**2))),
            samplesBeyondOriginalVoltageLimit=int(np.count_nonzero(difference[:,0]>=.05))))
    g.update(status=gs,passedUnitCases=25,gateReference=read(golgi/'clamps/gate-reference.json'),
        passiveDiscreteMaximumMv=max(c['independentDiscreteErrorMv'] for c in passive['passiveComparisons']),
        referenceComparisons=findings,candidateComparisons=active['comparisons'],caseFindings=passive['cases'])
    transcript=(motor/'powershell.log').read_text(encoding='utf-8-sig')
    assert 'The pipeline has been stopped.' in transcript and not (motor/'test-results/motor-axon.trx').exists()
    m.update(savedStatus=ms,observedOutcome='BUILD_PIPELINE_INTERRUPTED_BEFORE_TESTS',
        taskUnregisteredObservedDuringReview=True,testsExecuted=False,
        cause='PowerShell reports a stopped pipeline; who/what stopped it is not established. Saved RUNNING status is stale.')
    report=dict(reviewedUtc=datetime.now(timezone.utc).isoformat(),golgi=g,motor=m,
        originalsModified=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    output.mkdir(parents=True)
    (output/'review.json').write_text(json.dumps(report,indent=2,allow_nan=False)+'\n')
    print(json.dumps(dict(golgiFilesPerCopy=g['verifiedFilesPerCopy'],motorFilesPerCopy=m['verifiedFilesPerCopy'],
        passedGolgiTests=25,referenceComparisons=findings,motorOutcome=m['observedOutcome']),indent=2))


if __name__=='__main__':main(*(Path(x).resolve() for x in sys.argv[1:]))
