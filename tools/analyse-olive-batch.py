"""Audit complete saved CPU membrane traces; never execute a model or GPU."""
from hashlib import sha256
import json
import math
from pathlib import Path
import struct
import sys

read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))


def audit(root):
    protocol=read(root/'protocol.json');results=read(root/'results.json')
    assert protocol['version']=='olive-cpu-batch-v1' and protocol['frames']==1024
    assert protocol['counts']==[1,32,192] and protocol['membraneStepsMs']==[.025,.00625]
    methods=['reference','serial-batch','parallel-batch']
    assert protocol['methods']==methods and protocol['parallelWorkers']==4
    assert results['completed'] and len(results['cases'])==6
    assert not any(results[k] for k in ('gpuExecuted','speedupMeasured','physiologyQualified','avatarRecoveryAssessed'))
    record=struct.Struct('<17d2q');assert record.size==protocol['bytesPerCellFrame']==152
    seen=set();evidence=[]
    for case in results['cases']:
        count=case['count'];step=case['maximumStep'];key=case['key'];directory=root/key
        assert count in [1,32,192] and step in [.025,.00625] and (count,step) not in seen
        seen.add((count,step));assert case['frames']==1024 and case['exact']
        assert case==read(directory/'result.json')
        design=read(directory/'design.json');seeds=design['seeds']
        assert design['count']==count and design['maximumStep']==step and len(seeds)==count
        assert len({(s['Identity']['Instance'],s['Identity']['Cell']) for s in seeds})==count
        traces=[]
        for method in methods:
            raw=(directory/(method+'.bin')).read_bytes()
            assert len(raw)==152*count*1025 and sha256(raw).hexdigest().upper()==case['hashes'][method]
            traces.append(raw)
        assert traces[0]==traces[1]==traces[2],key
        soma=[0]*count;axon=[0]*count
        for index,values in enumerate(record.iter_unpack(traces[0])):
            frame,lane=divmod(index,count)
            assert all(math.isfinite(x) for x in values[:17])
            assert values[14]==frame*(.125 if lane%2==0 else .0625) and values[15]>=0
            assert values[11]>=0 and all(-1e-8<=values[i]<=1+1e-8 for i in range(3,14) if i!=11)
            assert values[17]>=soma[lane] and values[18]-axon[lane] in (0,1)
            if frame==0:
                seed=seeds[lane]
                assert values==tuple(seed['State']+[seed['TimeMs'],seed['AdditionalIonicLoad'],seed['SynapticCurrent'],seed['SomaticSpikes'],seed['AxonalSpikes']])
            soma[lane]=values[17];axon[lane]=values[18]
        assert sum(soma)==case['somaEvents'] and sum(axon)==case['axonEvents']
        evidence.append(dict(key=key,count=count,maximumStep=step,comparedFrames=1025,traceBytes=len(traces[0]),
            referenceSha256=case['hashes']['reference'],somaticSpikes=sum(soma),axonalSpikes=sum(axon)))
    return dict(completed=True,exactBatchCases=len(seen),comparisons=12,evidence=evidence,
        scope='CPU flat membrane layout and execution order only; no live scheduler, receptor or metabolic coupling',
        gpuExecuted=False,speedupMeasured=False,independentSolverQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)


if __name__=='__main__':
    root=Path(sys.argv[1]).resolve()
    (root/'analysis.json').write_text(json.dumps(audit(root),indent=2)+'\n',encoding='utf-8')
    print('Six cases: all twelve serial/parallel contrasts exactly match every saved original membrane-state byte.')
    print('This does not qualify the original solver, GPU performance, physiological calibration or Avatar recovery.')
