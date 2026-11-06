"""Audit reported, completed channel/passive evidence without running a cell.

Recompute errors from retained traces/references and analytic clamped relaxation;
verify every packaged file and both complete backup copies. Preserve FAILED as
the historical window outcome when its subsequent backup has been repaired.
"""
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timezone
from hashlib import sha256
import importlib.util
import json
import os
from pathlib import Path
import sys
import xml.etree.ElementTree as ET
import numpy as np

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
    observed={p.relative_to(io_root).as_posix().lower() for p in io_root.rglob('*') if p.is_file()}
    assert observed==set(result)|{'backup-manifest.json'}, 'Manifest must cover the entire capture.'
    return result


def main(run,out):
    run=run.resolve();out=out.resolve()
    assert out.is_dir() and not (out/'review.json').exists() and out!=run and run not in out.parents
    status=read(run/'window-status.json');config=read(run/'configuration.json');manifest=read(run/'backup-manifest.json')
    backup=Path(config['BackupDestination']).resolve()
    assert backup==Path(manifest['Destination']).resolve() and run==Path(manifest['Source']).resolve()
    assert status['Status']=='FAILED' and status['ExitCode']==1 and status['BackupError'] and 'Error' not in status
    assert status['PassedTests']==status['PassedMotoneuronAndManifestTests']==10
    assert status['MotoneuronChannelAndPassiveCriteriaPassed'] and not status['MotoneuronActiveFiringQualified']
    assert not status['LiveBrainAccessed'] and not status['OldSavesLoaded'] and not status['LiveMotorPathEnabled']
    assert status['Revision']==config['Revision']=='1ff49a1603599b3581b3557c7aab193774173c96'
    assert digest(out/'original-window-status.json')==digest(run/'window-status.json')
    assert digest(run/'backup-manifest.json')==digest(backup/'backup-manifest.json')
    with ThreadPoolExecutor(max_workers=2) as pool:
        local=pool.submit(audit,run,manifest);remote=pool.submit(audit,backup,manifest)
        comparison=read(run/'motoneuron-reference/comparison.json')
        protocol=read(run/'motoneuron-study/protocol.json');results=read(run/'motoneuron-study/results.json')
        assert comparison['completed'] and comparison['channelCriteriaPassed'] and comparison['passiveCriteriaPassed']
        assert results['completed'] and not results['activeFiringQualified']
        assert protocol['block']=='Passive' and protocol['initialVoltageMv']==-71
        assert protocol['stepsMs']==[.0125,.00625,.003125] and protocol['durationMs']==100
        assert comparison['criteria']==dict(gateAbsolute=1e-12,tauAbsoluteMs=1e-10,voltageAbsoluteMv=1e-7,chargeLedgerFc=1e-5)
        expected={(i,h) for i in (-100,100,200) for h in (.0125,.00625,.003125)}
        assert len(results['cases'])==len(comparison['cases'])==len(expected)==9
        cap=np.asarray(protocol['capacitancesPf']);maximum_voltage=maximum_ledger=0.
        for saved,actual in zip(comparison['cases'],results['cases']):
            assert saved['key']==actual['key'] and saved['passed'] and not actual['crossings']
            key=(actual['currentPa'],actual['stepMs']);assert key in expected;expected.remove(key)
            trace=np.loadtxt(run/'motoneuron-study'/(actual['key']+'.csv'),delimiter=',',skiprows=1)
            with np.load(run/'motoneuron-reference'/(actual['key']+'.npz')) as reference:
                times=reference['timeMs'];voltage=reference['voltageMv']
            assert trace.shape==(401,31) and voltage.shape==(401,5) and np.isfinite(trace).all() and np.isfinite(voltage).all()
            assert np.array_equal(trace[:,0],times) and np.array_equal(times,np.arange(401)*.25)
            error=float(np.max(np.abs(trace[:,1:6]-voltage)))
            ledger=float(np.max(np.abs((trace[:,1:6]+71)@cap-trace[:,30])))
            assert error==saved['maximumVoltageErrorMv'] and ledger==saved['chargeLedgerErrorFc']
            assert np.all(trace[:,15]==.0001) and np.all(trace[:,28:30]==0)
            maximum_voltage=max(maximum_voltage,error)
            maximum_ledger=max(maximum_ledger,ledger,actual['maximumChargeLedgerErrorFc'])
        assert maximum_voltage==comparison['maximumPassiveVoltageErrorMv'] and maximum_voltage<=1e-7 and maximum_ledger<=1e-5
        hashes=local.result();assert hashes==remote.result()
    for item in config['PackageFiles']:
        assert hashes[Path(item['Path']).as_posix().lower()]==item['Sha256'].upper()
    for name in ('build-binaries.json','motoneuron-build-binaries.json'):
        for item in read(run/name):
            relative=Path(item['Path']).resolve().relative_to(run).as_posix().lower()
            assert hashes[relative]==item['Sha256'].upper()
    # Load only the frozen independent analytic rate functions. main() does not
    # run, and neither ODE integration nor the numerical experiment is repeated.
    spec=importlib.util.spec_from_file_location('frozen_motor_rates',run/'source/tools/motoneuron-reference-check.py')
    reference=importlib.util.module_from_spec(spec);spec.loader.exec_module(reference)
    p,_,_=reference.model(run/'motoneuron-input')
    clamps=read(run/'motoneuron-study/clamps.json')
    expected={(t,v,ca,a) for t in (24,37) for v in (-100,-80,-65,-44,-29,-20,0,40) for ca in (.0001,.0002,.001) for a in (0,1)}
    assert len(clamps['cases'])==clamps['caseCount']==len(expected)==96
    maximum_gate=maximum_tau=maximum_equilibrium=0.
    for case in clamps['cases']:
        key=tuple(case[n] for n in ('temperature','voltage','calcium','availability'));assert key in expected;expected.remove(key)
        rates=reference.rates(case['voltage'],case['calcium'],case['temperature'],case['availability'],p)
        observed=np.asarray([[r['equilibrium'],r['tauMs']] for r in case['rates']])
        maximum_equilibrium=max(maximum_equilibrium,float(np.max(np.abs(observed[:,0]-rates[:,0]))))
        maximum_tau=max(maximum_tau,float(np.max(np.abs(observed[:,1]-rates[:,1]))))
        initial=reference.rates(-65,.0001,case['temperature'],case['availability'],p)[:,0]
        assert [s['timeMs'] for s in case['samples']]==[0,.0125,.125,1,10,100,1000]
        for sample in case['samples']:
            exact=rates[:,0]+(initial-rates[:,0])*np.exp(-sample['timeMs']/rates[:,1])
            gates=np.asarray(sample['gates']);assert np.isfinite(gates).all() and np.all((gates>=0)&(gates<=1))
            maximum_gate=max(maximum_gate,float(np.max(np.abs(exact-gates))))
    assert maximum_gate==comparison['maximumGateError'] and maximum_gate<=1e-12
    assert maximum_tau==comparison['maximumTauErrorMs'] and maximum_tau<=1e-10
    assert maximum_equilibrium==comparison['maximumEquilibriumError'] and maximum_equilibrium<=1e-12
    ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    tests=ET.parse(run/'test-results/motor-axon.trx').findall('.//t:UnitTestResult',ns)
    assert len(tests)==10 and all(t.attrib['outcome']=='Passed' for t in tests)
    assert sum('.MotoneuronReferenceTests.' in t.attrib['testName'] for t in tests)==9
    assert sum('.SolutionManifestTests.' in t.attrib['testName'] for t in tests)==1
    original_manifest=read(out/'original-backup-manifest.json')
    report=dict(reviewedUtc=datetime.now(timezone.utc).isoformat(),capture=str(run),backup=str(backup),
        sourceRevision=config['Revision'],completedUtc=status['CompletedUtc'],historicalWindowStatus='FAILED',
        originalFailure='Backup Copy-Item rejected an existing 260-character path; extended-path I/O successfully read and copied it.',
        originalStatusPreserved=True,backupRepaired=True,verifiedFilesPerCopy=len(hashes),
        verifiedBytesPerCopy=sum(x['Bytes'] for x in manifest['Files']),packagedFilesVerified=len(config['PackageFiles']),
        originalManifestSha256=digest(out/'original-backup-manifest.json'),originalManifestFiles=len(original_manifest['Files']),
        repairedManifestSha256=digest(run/'backup-manifest.json'),passedFocusedAndManifestTests=10,clampCases=96,passiveCases=9,
        maximumGateError=maximum_gate,maximumEquilibriumError=maximum_equilibrium,maximumTauErrorMs=maximum_tau,
        maximumPassiveVoltageErrorMv=maximum_voltage,maximumChargeLedgerErrorFc=maximum_ledger,
        channelAndPassiveCriteriaPassed=True,activeFiringQualified=False,nativeNeuronExecuted=False,
        physiologicalCalibrationComplete=False,avatarRecoveryAssessed=False)
    (out/'review.json').write_text(json.dumps(report,indent=2,allow_nan=False)+'\n',encoding='utf-8')
    print(json.dumps(report,indent=2),flush=True)


if __name__=='__main__':main(Path(sys.argv[1]),Path(sys.argv[2]))
