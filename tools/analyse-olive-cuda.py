"""Read all saved GPU/CPU membrane bytes. No CUDA calls or model execution."""
from hashlib import sha256
import json
import math
from pathlib import Path
import struct
import sys

read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
BOUNDS=[1e-6]*3+[1e-9]*8+[1e-6]+[1e-9]*2+[0,1e-6,1e-6]
CONTROLS=['seed-transfer-and-ownership','invalid-requests-atomic','thread-ownership','untouched-cell','identity-permutation','numerical-failure-atomic']


def audit(package):
    root=package/'study'; baseline=package/'cpu-baseline'
    protocol=read(root/'protocol.json'); results=read(root/'results.json')
    assert protocol['version']=='olive-cuda-membrane-v1' and protocol['candidateVersion']=='isolated-olive-cuda-double-v1'
    assert protocol['absoluteBounds']==BOUNDS and protocol['frames']==1024 and protocol['bytesPerCellFrame']==152
    assert protocol['counts']==[1,32,192] and protocol['membraneStepsMs']==[.025,.00625]
    assert protocol['exactPerFrameCountersAndClocks'] and protocol['requiredControlGroups']==6
    controls=read(root/'controls.json')
    assert [c['name'] for c in controls]==CONTROLS and all(c['passed'] for c in controls)
    for row in controls:
        if 'before' in row: assert row['before']==row['after'] and len(row['before'])==64
    assert controls[1]['rejected']==6 and controls[4]['frames']==len(controls[4]['matchedStateHashes'])==64
    assert len(controls[5]['explicitNextRequestHash'])==64
    compiler=read(root/'compiler.json'); device=read(root/'device.json')
    assert compiler['Options']==[f"--gpu-architecture=compute_{device['Architecture']}",'--std=c++17','--fmad=false','--ftz=false','--prec-div=true','--prec-sqrt=true']
    assert compiler['nvrtcMajor']==13 and compiler['nvrtcMinor']==0 and compiler['basenameHandlesVerified']
    for file,key in [('olivary.cu','sourceSha256'),('olivary.ptx','ptxSha256')]:
        assert sha256((root/file).read_bytes()).hexdigest().upper()==compiler[key]
    assert results['completed'] and results['contextDisposed'] and len(results['cases'])==6
    assert not any(results[k] for k in ('liveBrainAccessed','oldSavesLoaded','speedupMeasured','independentSolverQualified','physiologyQualified','avatarRecoveryAssessed'))
    record=struct.Struct('<17d2q'); seen=set(); rows=[]
    for case in results['cases']:
        count,step,key=case['count'],case['maximumStep'],case['key']
        assert count in [1,32,192] and step in [.025,.00625] and (count,step) not in seen
        seen.add((count,step)); assert key==f'cells-{count}-h-{step}'
        assert case==read(root/key/'result.json')
        item=dict(key=key,completed=case['completed'],passed=False,error=case['error'])
        if not case['completed']:
            assert not case['passed']; rows.append(item); continue
        actual=(root/key/'gpu.bin').read_bytes(); expected=(baseline/key/'reference.bin').read_bytes()
        assert len(actual)==len(expected)==152*count*1025 and case['samples']==1025
        assert sha256(actual).hexdigest().upper()==case['gpuSha256']
        assert sha256(expected).hexdigest().upper()==case['referenceSha256']
        design=read(root/key/'design.json'); assert design==read(baseline/key/'design.json')
        assert len({(s['Identity']['Instance'],s['Identity']['Cell']) for s in design['seeds']})==count
        maxima=[0.]*17; matching=True; soma=[0]*count; axon=[0]*count
        for index,(a,b) in enumerate(zip(record.iter_unpack(actual),record.iter_unpack(expected))):
            frame,lane=divmod(index,count)
            assert all(math.isfinite(v) for v in a[:17])
            assert a[14]==b[14]==frame*(.125 if lane%2==0 else .0625)
            assert a[11]>=0 and a[15]>=0 and all(-1e-8<=a[i]<=1+1e-8 for i in range(3,14) if i!=11)
            assert a[17]>=soma[lane] and a[18]-axon[lane] in (0,1)
            soma[lane],axon[lane]=a[17:]
            if frame==0:
                assert record.pack(*a)==record.pack(*b),'Initial state was rounded or altered.'
            for component in range(17): maxima[component]=max(maxima[component],abs(a[component]-b[component]))
            matching &= a[17:]==b[17:]
        assert maxima==case['maxima'] and matching==case['matchingCounters']
        passed=matching and all(a<=b for a,b in zip(maxima,BOUNDS))
        assert passed==case['passed']
        item.update(passed=passed,maximumAbsoluteErrors=maxima,matchingPerFrameCounters=matching,
            somaticSpikes=sum(soma),axonalSpikes=sum(axon),comparedCellFrames=count*1025,sha256=case['gpuSha256'])
        rows.append(item)
    return dict(completed=True,controlGroupsPassed=6,attemptedCases=6,passedCases=sum(r['passed'] for r in rows),
        numericalAgreementPassed=all(r['passed'] for r in rows),cases=rows,absoluteBounds=BOUNDS,contextCleanupReported=True,
        speedupMeasured=False,independentSolverQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)


if __name__=='__main__':
    package=Path(sys.argv[1]).resolve(); result=audit(package)
    with (package/'gpu-analysis.json').open('x',encoding='utf-8') as stream: stream.write(json.dumps(result,indent=2)+'\n')
    if not result['numericalAgreementPassed']: raise RuntimeError('GPU/CPU agreement criteria not all met; preserve every trace and bound.')
    print('All six complete GPU traces satisfy the original declared state bounds and exact per-frame counts/clocks.')
    print('Speedup, independent solver accuracy, physiology and Avatar recovery remain separate.')
