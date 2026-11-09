"""Recompute timing statistics and audit every saved report/state byte. No model run."""
from hashlib import sha256
import gzip
import json
import math
from pathlib import Path
from statistics import median
import struct
import sys

read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
COUNTS=[1,32,192,768,3072,12288]
STEPS=[.025,.00625]
MODES=['cpu-1','cpu-8','cpu-16','cuda']
BOUNDS=[1e-6]*3+[1e-9]*8+[1e-6]+[1e-9]*2+[0,1e-6,1e-6]
STATE=struct.Struct('<17d2q'); REPORT=struct.Struct('<4d2q')

def audit(package):
    root=package/'study'; protocol=read(root/'protocol.json'); results=read(root/'results.json')
    assert protocol['version']=='olive-cuda-timing-v1' and protocol['candidateVersion']=='isolated-olive-cuda-double-v1'
    assert protocol['counts']==COUNTS and protocol['membraneStepsMs']==STEPS and protocol['modes']==MODES
    assert (protocol['startFrame'],protocol['frames'],protocol['repeats'],protocol['warmupsPerModeAndCase'])==(256,128,5,1)
    assert protocol['absoluteBounds']==BOUNDS and protocol['cpuExact']
    assert protocol['stateRecordBytes']==STATE.size and protocol['reportRecordBytes']==REPORT.size
    assert results['completed'] and results['contextDisposed'] and results['speedupMeasured']
    assert not any(results[k] for k in ('liveBrainAccessed','oldSavesLoaded','physiologyQualified','avatarRecoveryAssessed'))
    device=read(root/'device.json'); compiler=read(root/'compiler.json'); host=read(root/'host.json')
    assert compiler['Options']==[f"--gpu-architecture=compute_{device['Architecture']}",'--std=c++17','--fmad=false','--ftz=false','--prec-div=true','--prec-sqrt=true']
    assert compiler['nvrtcMajor']==13 and compiler['nvrtcMinor']==0 and compiler['basenameHandlesVerified']
    assert not host['tieredCompilation'] and host['stopwatchFrequency']>0
    for path,key in [('olivary.cu','sourceSha256'),('olivary.ptx','ptxSha256')]:
        assert sha256((root/path).read_bytes()).hexdigest().upper()==compiler[key]
    for path,key in [('CompilerPath','compilerSha256'),('BuiltinsPath','builtinsSha256')]:
        p=Path(compiler[path]); assert p.resolve().is_relative_to((package/'compiler').resolve())
        assert sha256(p.read_bytes()).hexdigest().upper()==compiler[key]
    controls=read(root/'controls.json')
    assert [r['name'] for r in controls]==['seed-transfer-and-ownership','invalid-requests-atomic','thread-ownership','untouched-cell','identity-permutation','numerical-failure-atomic']
    assert all(r['passed'] for r in controls) and controls[1]['rejected']==6 and controls[4]['frames']==64
    assert all(r['before']==r['after'] for r in controls if 'before' in r)
    assert len(controls[4]['matchedStateHashes'])==64 and len(controls[5]['explicitNextRequestHash'])==64
    assert read(root/'memory-before.json')['ownedBufferBytes']==read(root/'memory-after.json')['ownedBufferBytes']==0
    rows=results['trials']; assert len(rows)==12*24 and rows==read(root/'completed-trials.json')
    cursor=0; cases=[]
    for count in COUNTS:
        for step in STEPS:
            key=f'cells-{count}-h-{step}'; prototypes=min(count,192); copies=count//prototypes
            folder=root/key; baseline=package/'cpu-baseline'/f'cells-{prototypes}-h-{step}'
            design=read(folder/'design.json'); old=read(baseline/'design.json')
            assert (design['count'],design['step'],design['prototypes'],design['startFrame'],design['frames'])==(count,step,prototypes,256,128)
            assert len(design['seeds'])==count and len({(r['Identity']['Instance'],r['Identity']['Cell']) for r in design['seeds']})==count
            for lane,s in enumerate(design['seeds']):
                original=old['Seeds'][lane%prototypes]['Identity'] if 'Seeds' in old else old['seeds'][lane%prototypes]['Identity']
                assert (s['lane'],s['prototype'])==(lane,lane%prototypes)
                assert s['Identity']==dict(Instance=original['Instance']+f'/copy-{lane//prototypes:04}',Cell=original['Cell'])
            data=(baseline/'reference.bin').read_bytes()
            assert len(data)==STATE.size*prototypes*1025 and sha256(data).hexdigest().upper()==design['referenceSha256']
            expected=[list(STATE.iter_unpack(data[f*prototypes*STATE.size:(f+1)*prototypes*STATE.size])) for f in range(256,385)]
            case_rows=[]; maxima=[0.]*17; report_max=[0.,0.]
            for round_id in range(6):
                for position in range(4):
                    mode=MODES[(position+round_id)%4]; row=rows[cursor]; cursor+=1
                    trial=f'{mode}-round-{round_id}'
                    assert row==read(folder/(trial+'.json'))
                    assert (row['key'],row['count'],row['step'],row['mode'],row['round'],row['measured'])==(key,count,step,mode,round_id,round_id!=0)
                    assert row['completedFrames']==128 and row['error'] is None
                    assert row['initialStateChecked'] and row['matchingParameters'] and row['identityOrderChecked']
                    assert math.isfinite(row['elapsedMs']) and row['elapsedMs']>0 and row['setupMs']>=0 and row['allocatedBytes']>=0
                    assert len(row['gcCollections'])==3 and all(n>=0 for n in row['gcCollections'])
                    clock=read(folder/(trial+'-clock.json')); assert clock['elapsedMs']==row['elapsedMs']
                    memory=row['memoryDuring']; assert memory['freeBytes']<=memory['totalBytes']
                    assert row['bufferBytes']==(47*count+80)*8
                    assert memory['ownedBufferBytes']==(row['bufferBytes'] if mode=='cuda' else 0)
                    report_path=folder/(trial+'-reports.bin.gz')
                    assert sha256(report_path.read_bytes()).hexdigest().upper()==row['reportsSha256']
                    with gzip.open(report_path,'rb') as stream:
                        for frame in range(128):
                            raw=stream.read(count*REPORT.size); prefix=raw[:prototypes*REPORT.size]
                            assert len(raw)==count*REPORT.size and raw==prefix*copies,'Replicated cell reports differ.'
                            for lane,actual in enumerate(REPORT.iter_unpack(prefix)):
                                a,b=expected[frame][lane],expected[frame+1][lane]
                                target=(a[14],b[14],b[15],b[16],b[17]-a[17],b[18]-a[18])
                                assert all(math.isfinite(v) for v in actual[:4]) and actual[:2]==target[:2] and actual[4:]==target[4:]
                                assert actual[2]>=0 and actual[4]>=0 and actual[5] in (0,1)
                                if mode!='cuda': assert REPORT.pack(*actual)==REPORT.pack(*target),'CPU report changed.'
                                for j in range(2):
                                    error=abs(actual[2+j]-target[2+j]); assert error<=1e-6
                                    report_max[j]=max(report_max[j],error)
                        assert stream.read(1)==b''
                    final_path=folder/(trial+'-final.bin.gz')
                    assert sha256(final_path.read_bytes()).hexdigest().upper()==row['finalSha256']
                    with gzip.open(final_path,'rb') as stream: raw=stream.read(count*STATE.size+1)
                    assert len(raw)==count*STATE.size and raw==raw[:prototypes*STATE.size]*copies
                    for lane,actual in enumerate(STATE.iter_unpack(raw[:prototypes*STATE.size])):
                        target=expected[-1][lane]
                        assert all(math.isfinite(v) for v in actual[:17]) and actual[17:]==target[17:]
                        assert actual[11]>=0 and actual[15]>=0 and all(-1e-8<=actual[i]<=1+1e-8 for i in range(3,14) if i!=11)
                        if mode!='cuda': assert STATE.pack(*actual)==STATE.pack(*target),'CPU final state changed.'
                        for i in range(17):
                            error=abs(actual[i]-target[i]); assert error<=BOUNDS[i]; maxima[i]=max(maxima[i],error)
                    if round_id: case_rows.append(row)
            summaries={}
            for mode in MODES:
                selected=[r for r in case_rows if r['mode']==mode]; values=[r['elapsedMs'] for r in selected]; assert len(values)==5
                summaries[mode]=dict(medianMs=median(values),minimumMs=min(values),maximumMs=max(values),samplesMs=values,
                    medianSetupMs=median(r['setupMs'] for r in selected),medianAllocatedBytes=median(r['allocatedBytes'] for r in selected),
                    medianProcessCpuMs=median(r['processCpuMs'] for r in selected),gcCollections=[r['gcCollections'] for r in selected])
            best=min(MODES[:3],key=lambda m:summaries[m]['medianMs']); gpu=summaries['cuda']
            cases.append(dict(key=key,count=count,maximumStepMs=step,accuracyPassed=True,measuredTrials=20,
                maximumFinalStateErrors=maxima,maximumReportMetricErrors=report_max,backends=summaries,bestCpuMode=best,
                speedupVersusBestCpuMedian=summaries[best]['medianMs']/gpu['medianMs'],
                pairedSpeedupVersusBestCpu=[a/b for a,b in zip(summaries[best]['samplesMs'],gpu['samplesMs'])],
                minimumCellBiologicalMs=16 if count==1 else 8,deviceBufferBytes=(47*count+80)*8,
                minimumObservedFreeBytes=min(r['memoryDuring']['freeBytes'] for r in case_rows)))
    return dict(completed=True,accuracyPassed=True,totalTrials=288,measuredTrials=240,warmupTrials=48,cases=cases,
        contextCleanupReported=True,speedupMeasured=True,wholeBrainThroughputMeasured=False,
        independentSolverQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)

if __name__=='__main__':
    package=Path(sys.argv[1]).resolve(); result=audit(package)
    with (package/'timing-analysis.json').open('x',encoding='utf-8') as stream: stream.write(json.dumps(result,indent=2)+'\n')
    for row in result['cases']:
        print(f"{row['key']}: GPU {row['backends']['cuda']['medianMs']:.3f} ms; best CPU {row['bestCpuMode']}; ratio {row['speedupVersusBestCpuMedian']:.3f}x",flush=True)
    print('All saved reports and final states agree. Ratios measure the isolated batch API, not whole-brain or Avatar speed.')
