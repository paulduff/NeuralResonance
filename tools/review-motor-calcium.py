"""Read-only review of reported motor-calcium results; no ODE or test execution."""
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timezone
from hashlib import sha256
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET
import numpy as np

read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
NAMES=('freeCalcium','boundCalcium','activation','released','recovered','netBound')


def digest(path):
    h=sha256()
    with path.open('rb') as stream:
        for block in iter(lambda:stream.read(8*1024*1024),b''):h.update(block)
    return h.hexdigest().upper()


def audit(root,manifest):
    root=root.resolve(); result={}
    for item in manifest['Files']:
        path=(root/item['Path']).resolve()
        assert root in path.parents and path.stat().st_size==item['Bytes']
        key=path.relative_to(root).as_posix().lower();assert key not in result
        result[key]=digest(path);assert result[key]==item['Sha256'].upper(),str(path)
    return result


def errors(a,b):
    return dict(zip(NAMES,map(float,np.max(np.abs(a-b),axis=0))))


def main(run,out):
    run=run.resolve();out=out.resolve()
    assert not out.exists() and out!=run and run not in out.parents
    status=read(run/'window-status.json');config=read(run/'configuration.json');manifest=read(run/'backup-manifest.json')
    backup=Path(config['BackupDestination']).resolve()
    assert backup==Path(manifest['Destination']).resolve()
    assert status['Status']=='FINISHED' and status['ExitCode']==0 and status['PassedTests']==46
    assert status['MuscleCalciumNumericalCriteriaPassed'] and not status['LiveMotorPathEnabled']
    assert not status['LiveBrainAccessed'] and not status['OldSavesLoaded']
    assert status['Revision']==config['Revision']=='825d17f4890fb99310c5afc48c64d8e44fac2836'
    assert digest(run/'backup-manifest.json')==digest(backup/'backup-manifest.json')
    with ThreadPoolExecutor(max_workers=2) as pool:
        local=pool.submit(audit,run,manifest);remote=pool.submit(audit,backup,manifest)
        comparison=read(run/'calcium-reference/comparison.json')
        protocol=read(run/'calcium-study/protocol.json')
        assert comparison['completed'] and comparison['numericalCriteriaPassed']
        assert len(comparison['protocols'])==7 and protocol['stepsMs']==[.125,.0625,.03125]
        summaries=[];ref_state_error=ref_flux_error=finest_error=force_error=ledger_error=0.
        for row in comparison['protocols']:
            name=row['protocol'];assert row['passed'] and row['refinementPassed']
            references={}
            for key in ('dop853','dop853-tight','radau'):
                with np.load(run/'calcium-reference'/(name+'-'+key+'.npz')) as saved:
                    grid=saved['timeSeconds'];state=saved['state']
                assert grid.shape==(24001,) and state.shape==(24001,6) and np.isfinite(state).all()
                references[key]=state
            for contrast in row['contrasts']:
                measured=errors(references[contrast['left']],references[contrast['right']])
                assert measured==contrast['maximumAbsolute'] and contrast['passed']
                ref_state_error=max(ref_state_error,max(measured[n] for n in NAMES[:3]))
                ref_flux_error=max(ref_flux_error,max(measured[n] for n in NAMES[3:]))
            for c in row['candidates']:
                trace=np.loadtxt(run/'calcium-study'/(name+'-h'+str(c['stepMs'])+'.csv'),delimiter=',',skiprows=1)
                assert trace.shape==(24001,11) and np.isfinite(trace).all()
                assert np.array_equal(trace[:,0],np.arange(24001)*.25)
                assert errors(trace[:,[2,3,5,7,8,9]],references['dop853-tight'])==c['maximumAbsolute']
                measured_force=float(np.max(np.abs(trace[:,6]-references['dop853-tight'][:,2]*protocol['parameters']['peakTetanicForceNewtons'])))
                assert measured_force==c['maximumForceErrorNewtons']
                ledger=max(float(np.max(trace[:,10])),float(np.max(np.abs(trace[:,2]-trace[:,7]+trace[:,8]+trace[:,9]))),float(np.max(np.abs(trace[:,3]-trace[:,9]))))
                assert ledger==c['maximumCalciumLedgerError']
                ledger_error=max(ledger_error,ledger)
                assert c['physicalDomainPassed'] and c['accountingPassed'] and c['silencePassed']
                assert np.min(trace[:,2:6])>=0 and np.max(trace[:,[3,5]])<=1
                if name=='quiet':assert np.all(trace[:,1:4]==0) and np.all(trace[:,5:]==0)
                if c['stepMs']==.03125:
                    finest_error=max(finest_error,max(c['maximumAbsolute'][n] for n in NAMES[:3]))
                    force_error=max(force_error,measured_force)
                    peak=int(np.argmax(trace[:,6]));window=(trace[:,0]>=2500)&(trace[:,0]<=3900)
                    metrics=dict(peakForceNewtons=float(trace[peak,6]),peakAtMs=float(trace[peak,0]),
                        lateDriveMeanForceNewtons=float(np.mean(trace[window,6])),lateDriveRangeNewtons=float(np.ptp(trace[window,6])),
                        finalActivation=float(trace[-1,5]))
                    for k,v in metrics.items():assert abs(v-c['metrics'][k])<=1e-11
                    summaries.append(dict(protocol=name,arrivals=len(protocol['protocols'][name]),**metrics))
        assert ref_state_error<=1e-8 and ref_flux_error<=1e-7 and finest_error<=1e-6 and force_error<=1e-6 and ledger_error<=1e-9
        hashes=local.result();assert hashes==remote.result()
    for item in config['PackageFiles']:
        assert hashes[Path(item['Path']).as_posix().lower()]==item['Sha256'].upper()
    for file in ('build-binaries.json','calcium-build-binaries.json'):
        for item in read(run/file):
            relative=Path(item['Path']).resolve().relative_to(run).as_posix().lower()
            assert hashes[relative]==item['Sha256'].upper()
    ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    tests=ET.parse(run/'test-results/motor-axon.trx').findall('.//t:UnitTestResult',ns)
    assert len(tests)==46 and all(t.attrib['outcome']=='Passed' for t in tests)
    groups={name:sum(('.'+name+'.') in t.attrib['testName'] for t in tests) for name in ('MotorAxonTransportTests','MotorUnitCalciumTests','SolutionManifestTests')}
    assert groups==dict(MotorAxonTransportTests=32,MotorUnitCalciumTests=13,SolutionManifestTests=1)
    report=dict(reviewedUtc=datetime.now(timezone.utc).isoformat(),capture=str(run),backup=str(backup),
        sourceRevision=status['Revision'],completedUtc=status['CompletedUtc'],verifiedFilesPerCopy=len(hashes),
        verifiedBytesPerCopy=sum(x['Bytes'] for x in manifest['Files']),packagedFilesVerified=len(config['PackageFiles']),
        passedTests=groups,savedStateAndForceDifferencesReproducedExactly=True,
        maximumReferenceStateDifference=ref_state_error,maximumReferenceFluxDifference=ref_flux_error,
        maximumFinestCandidateStateError=finest_error,maximumFinestForceErrorNewtons=force_error,
        maximumCalciumLedgerError=ledger_error,protocols=summaries,
        numericalCriteriaPassed=True,physiologicalCalibrationComplete=False,neuronalSourceQualified=False,avatarRecoveryAssessed=False)
    out.mkdir(parents=True);(out/'review.json').write_text(json.dumps(report,indent=2,allow_nan=False)+'\n',encoding='utf-8')
    print(json.dumps(report,indent=2),flush=True)


if __name__=='__main__':main(Path(sys.argv[1]),Path(sys.argv[2]))
