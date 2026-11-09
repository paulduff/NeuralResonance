"""Audit all recorded work and cost partitions; never run the membrane model."""
import gzip
from hashlib import sha256
import json
import math
from pathlib import Path
from statistics import median
import struct
import sys
from olive_cost_protocol import COUNTS,STEPS,MODES,PHASES,BOUNDS,protocol,schedule

read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
STATE=struct.Struct('<17d2q');REPORT=struct.Struct('<4d2q')
digest=lambda p:sha256(p.read_bytes()).hexdigest().upper()

def audit(package):
    assert read(package/'cost-protocol.json')==protocol()
    assert read(package/'schedule.json')==schedule()
    root=package/'study';recorded=read(package/'completed-processes.json')
    assert recorded==schedule(),'Incomplete or reordered process schedule.'
    old_device=read(package/'timing-evidence/device.json')
    old_compiler=read(package/'timing-evidence/compiler.json')
    assert digest(package/'timing-evidence/olivary.ptx')==old_compiler['ptxSha256']
    assert old_compiler['Options']==[f"--gpu-architecture=compute_{old_device['Architecture']}",'--std=c++17','--fmad=false','--ftz=false','--prec-div=true','--prec-sqrt=true']
    for mode in MODES:
        controls=root/'controls'/mode;status=read(controls/'result.json');rows=read(controls/'controls.json')
        assert status==dict(completed=True,mode=mode,controls=True,contextDisposed=True)
        assert [r['name'] for r in rows]==['seed-transfer-and-ownership','invalid-requests-atomic','thread-ownership','untouched-cell','identity-permutation','numerical-failure-atomic']
        assert all(r['passed'] for r in rows) and rows[1]['rejected']==6 and rows[4]['frames']==64
        assert all(r['before']==r['after'] for r in rows if 'before' in r)
        assert len(rows[4]['matchedStateHashes'])==64 and len(rows[5]['explicitNextRequestHash'])==64
        assert read(controls/'memory-after.json')['ownedBufferBytes']==0
    expected={};case_bits={};case_rows={};errors={}
    for item in schedule():
        count,step,round_id,mode=(item[k] for k in ('count','step','round','mode'))
        key=f'cells-{count}-h-{step}';folder=root/item['path'];baseline=package/'cpu-baseline'/f'cells-192-h-{step}'
        old=read(baseline/'design.json');design=read(folder/'design.json');host=read(folder/'host.json');device=read(folder/'device.json')
        assert device==old_device,'Device/driver changed since the reviewed timing capture.'
        assert host['mode']==mode and host['costHooksPresent']==(mode!='control') and not host['tieredCompilation']
        assert host['stopwatchFrequency']>0 and host['IsHighResolution']
        frequency=host['stopwatchFrequency'];result=read(folder/'result.json')
        assert result==dict(completed=True,mode=mode,count=count,step=step,round=round_id,trials=2,contextDisposed=True,
            liveBrainAccessed=False,oldSavesLoaded=False,physiologyQualified=False,avatarRecoveryAssessed=False)
        assert read(folder/'memory-after.json')['ownedBufferBytes']==0
        assert (design['mode'],design['count'],design['step'],design['round'],design['startFrame'],design['frames'],design['prototypes'])==(mode,count,step,round_id,256,128,192)
        assert len(design['seeds'])==count and len({(s['Identity']['Instance'],s['Identity']['Cell']) for s in design['seeds']})==count
        for lane,s in enumerate(design['seeds']):
            original=old.get('seeds',old.get('Seeds'))[lane%192]['Identity']
            assert s==dict(lane=lane,prototype=lane%192,Identity=dict(Instance=original['Instance']+f'/copy-{lane//192:04}',Cell=original['Cell']))
        if step not in expected:
            data=(baseline/'reference.bin').read_bytes()
            assert len(data)==152*192*1025
            expected[step]=[list(STATE.iter_unpack(data[f*192*152:(f+1)*192*152])) for f in range(256,385)]
        assert digest(baseline/'reference.bin')==design['referenceSha256']
        maxima=errors.setdefault(key,[0.]*19);copies=count//192
        for name in ('warmup','measured'):
            row=read(folder/(name+'.json'));measured=name=='measured'
            assert (row['mode'],row['count'],row['step'],row['round'],row['measured'],row['completedFrames'])==(mode,count,step,round_id,measured,128)
            assert row['initialStateChecked'] and row['identityOrderChecked']
            assert row['elapsedTicks']>0 and row['elapsedMs']==row['elapsedTicks']*1000./frequency
            assert all(math.isfinite(row[k]) and row[k]>=0 for k in ('elapsedMs','setupMs','processCpuMs','allocatedBytes'))
            assert len(row['gcCollections'])==3 and all(n>=0 for n in row['gcCollections'])
            assert row['bufferBytes']==(47*count+80)*8 and row['memoryDuring']['ownedBufferBytes']==row['bufferBytes']
            assert 0<=row['memoryDuring']['freeBytes']<=row['memoryDuring']['totalBytes']
            raw_hash=sha256()
            reports=folder/(name+'-reports.bin.gz');final=folder/(name+'-final.bin.gz')
            assert digest(reports)==row['reportsSha256'] and digest(final)==row['finalSha256']
            with gzip.open(reports,'rb') as stream:
                for f in range(128):
                    raw=stream.read(count*REPORT.size);prefix=raw[:192*REPORT.size];raw_hash.update(raw)
                    assert len(raw)==count*REPORT.size and raw==prefix*copies
                    for lane,a in enumerate(REPORT.iter_unpack(prefix)):
                        before,after=expected[step][f][lane],expected[step][f+1][lane]
                        target=(before[14],after[14],after[15],after[16],after[17]-before[17],after[18]-before[18])
                        assert all(math.isfinite(v) for v in a[:4]) and a[:2]==target[:2] and a[4:]==target[4:]
                        assert a[2]>=0 and a[4]>=0 and a[5] in (0,1)
                        for j in range(2):
                            error=abs(a[2+j]-target[2+j]);assert error<=1e-6;maxima[17+j]=max(maxima[17+j],error)
                assert stream.read(1)==b''
            with gzip.open(final,'rb') as stream:raw=stream.read(count*STATE.size+1)
            assert len(raw)==count*STATE.size and raw==raw[:192*STATE.size]*copies;raw_hash.update(raw)
            for lane,a in enumerate(STATE.iter_unpack(raw[:192*STATE.size])):
                target=expected[step][-1][lane]
                assert all(math.isfinite(v) for v in a[:17]) and a[17:]==target[17:]
                assert a[11]>=0 and a[15]>=0 and all(-1e-8<=a[j]<=1+1e-8 for j in range(3,14) if j!=11)
                for j in range(17):
                    error=abs(a[j]-target[j]);assert error<=BOUNDS[j];maxima[j]=max(maxima[j],error)
            bits=raw_hash.hexdigest();assert case_bits.setdefault(key,bits)==bits,'Profiling/round/warmup changed a returned bit.'
            assert row['eventsCreated']==row['eventsDestroyed']==(256 if mode=='device' else 0)
            totals=[0]*5;launch=sync=0;device_ms=0.
            if mode=='control':assert row['cost'] is None
            else:
                assert len(row['cost'])==128
                for f,cost in enumerate(row['cost']):
                    assert cost['frame']==f and len(cost['phaseTicks'])==5
                    assert all(isinstance(t,int) and t>=0 for t in cost['phaseTicks'])
                    assert cost['launchTicks']>=0 and cost['syncTicks']>=0
                    assert cost['launchTicks']+cost['syncTicks']<=cost['phaseTicks'][2]
                    totals=[a+b for a,b in zip(totals,cost['phaseTicks'])];launch+=cost['launchTicks'];sync+=cost['syncTicks']
                    if mode=='device':
                        assert math.isfinite(cost['deviceEventMs']) and cost['deviceEventMs']>=0;device_ms+=cost['deviceEventMs']
                    else:assert cost['deviceEventMs'] is None
                assert sum(totals)<=row['elapsedTicks'],'Phase timing exceeds enclosing timer.'
            if measured:
                case_rows.setdefault(key,[]).append(dict(mode=mode,round=round_id,elapsedMs=row['elapsedMs'],
                    phaseMs=[t*1000./frequency for t in totals] if mode!='control' else None,
                    outsidePhasesMs=(row['elapsedTicks']-sum(totals))*1000./frequency if mode!='control' else None,
                    launchMs=launch*1000./frequency if mode!='control' else None,syncMs=sync*1000./frequency if mode!='control' else None,
                    deviceEventMs=device_ms if mode=='device' else None,allocatedBytes=row['allocatedBytes'],gcCollections=row['gcCollections']))
    cases=[]
    for key,rows in case_rows.items():
        by_mode={m:sorted((r for r in rows if r['mode']==m),key=lambda r:r['round']) for m in MODES}
        assert all(len(r)==5 for r in by_mode.values())
        medians={m:median(r['elapsedMs'] for r in v) for m,v in by_mode.items()}
        summaries={}
        for mode in MODES:
            values=by_mode[mode];ratio=medians[mode]/medians['control']
            summaries[mode]=dict(medianMs=medians[mode],samplesMs=[r['elapsedMs'] for r in values],
                ratioToControlMedian=ratio,pairedRatiosToControl=[r['elapsedMs']/c['elapsedMs'] for r,c in zip(values,by_mode['control'])],
                materialInstrumentationDifference=ratio<.9 or ratio>1.1,
                medianPhaseMs=None if mode=='control' else {p:median(r['phaseMs'][i] for r in values) for i,p in enumerate(PHASES)},
                medianOutsidePhasesMs=None if mode=='control' else median(r['outsidePhasesMs'] for r in values),
                medianLaunchMs=None if mode=='control' else median(r['launchMs'] for r in values),
                medianSyncMs=None if mode=='control' else median(r['syncMs'] for r in values),
                medianDeviceEventMs=median(r['deviceEventMs'] for r in values) if mode=='device' else None)
        cases.append(dict(key=key,accuracyPassed=True,identicalAcrossModesRoundsAndWarmups=True,
            maximumFinalAndReportErrors=errors[key],modes=summaries,trials=rows))
    return dict(completed=True,accuracyPassed=True,processes=90,trials=180,warmups=90,measuredTrials=90,
        controlGroups=18,cases=cases,phaseAccountingPassed=True,contextCleanupReported=True,
        wholeBrainThroughputMeasured=False,physiologyQualified=False,avatarRecoveryAssessed=False)

if __name__=='__main__':
    package=Path(sys.argv[1]).resolve();result=audit(package)
    with (package/'cost-analysis.json').open('x',encoding='utf-8') as stream:stream.write(json.dumps(result,indent=2)+'\n')
    for case in result['cases']:
        print(case['key']+': '+', '.join(f"{m} {v['medianMs']:.3f} ms" for m,v in case['modes'].items()),flush=True)
    print('Accuracy and cost accounting passed. Profiling overhead and device scheduling must be considered before attribution.')
