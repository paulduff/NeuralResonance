"""Audit supplied intervals versus sample timestamps using exact integer units."""
import csv
from fractions import Fraction
from hashlib import sha256
import json
import math
from pathlib import Path
import sys

read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))

def audit(package):
    root=package/'study';protocol=read(root/'protocol.json');result=read(root/'results.json')
    assert protocol==dict(version='motor-clock-arithmetic-v1',scalePower=80,durationMs=6000,sampleMs=.1,
        stepsMs=[.0125,.00625,.003125],modes=['original','endpoint-difference'],boundariesMs=[1000,1002,1800,2200,3000,6000],
        modelExecuted=False,causalCalciumExplanationEstablished=False,originalBoundsChanged=False)
    assert result['completed'] and len(result['cases'])==6 and result['cases']==read(root/'progress.json')
    assert not any(result[k] for k in ('modelExecuted','solverRepairQualified','causalCalciumExplanationEstablished','physiologyQualified','avatarRecoveryAssessed'))
    cases=[];scale=1<<80
    expected=[(h,m) for h in [.0125,.00625,.003125] for m in ['original','endpoint-difference']]
    for row,(step,mode) in zip(result['cases'],expected):
        key=f'{mode}-h{step}';assert (row['key'],row['mode'],row['maximumStepMs'])==(key,mode,step)
        path=root/(key+'.csv');assert sha256(path.read_bytes()).hexdigest().upper()==row['sha256']
        last_calls=last_rounded=last_units=0;maximum=0;worst=0;boundaries=[]
        replay_time=0.;replay_units=replay_calls=replay_rounded=0
        with path.open(newline='') as stream:
            reader=csv.DictReader(stream);assert reader.fieldnames==['timeMs','suppliedUnits','clockUnits','errorUnits','calls','roundedAdditions']
            samples=0
            for i,sample in enumerate(reader):
                samples+=1;t=float(sample['timeMs']);units,clock,error,calls,rounded=(int(sample[k]) for k in reader.fieldnames[1:])
                assert t==i*.1 and Fraction.from_float(t)*scale==clock and units-clock==error
                # Independently decode each supplied Python binary64 duration
                # through its integer ratio, rather than reusing C#'s bit shifts.
                while replay_time<t:
                    if mode=='original':dt=min(step,t-replay_time)
                    else:
                        end=min(t,replay_time+step)
                        if end-replay_time>step:end=math.nextafter(end,-math.inf)
                        dt=end-replay_time
                    next_time=replay_time+dt
                    assert 0<dt<=step and replay_time<next_time<=t
                    n,d=dt.as_integer_ratio();assert n*scale%d==0
                    du=n*scale//d;before=Fraction.from_float(replay_time)*scale;after=Fraction.from_float(next_time)*scale
                    replay_units+=du;replay_calls+=1;replay_rounded+=int(after-before!=du);replay_time=next_time
                assert (units,calls,rounded)==(replay_units,replay_calls,replay_rounded),'Independent schedule arithmetic differs.'
                assert calls>=last_calls and last_rounded<=rounded<=calls and units>=last_units
                if i:assert calls>last_calls and units>last_units
                if mode=='endpoint-difference':assert error==0 and rounded==0
                if abs(error)>maximum:maximum=abs(error);worst=t
                if t in protocol['boundariesMs']:boundaries.append(dict(timeMs=t,errorUnits=str(error),errorMs=float(Fraction(error,scale))))
                last_calls,last_rounded,last_units=calls,rounded,units
        assert samples==60001 and t==row['finalTimeMs']==6000 and calls==row['calls'] and rounded==row['roundedClockAdditions']
        assert str(units)==row['finalSuppliedUnits'] and str(error)==row['finalErrorUnits']
        assert str(maximum)==row['maximumSampleErrorUnits'] and worst==row['worstSampleMs']
        if mode=='endpoint-difference':assert row['maximumSingleAdditionRoundingUnits']=='0'
        cases.append(dict(**row,maximumSampleErrorMs=float(Fraction(maximum,scale)),finalErrorMs=float(Fraction(error,scale)),
            maximumSingleAdditionRoundingMs=float(Fraction(int(row['maximumSingleAdditionRoundingUnits']),scale)),boundaries=boundaries))
    return dict(completed=True,exactClockAuditPassed=True,cases=cases,
        originalClockMismatchReproduced=all(r['maximumSampleErrorMs']>0 for r in cases if r['mode']=='original'),
        endpointDifferenceConservesRepresentedTime=True,modelExecuted=False,solverRepairQualified=False,
        causalCalciumExplanationEstablished=False,originalBoundsChanged=False,physiologyQualified=False,avatarRecoveryAssessed=False)

if __name__=='__main__':
    package=Path(sys.argv[1]).resolve();result=audit(package)
    with (package/'clock-analysis.json').open('x',encoding='utf-8') as stream:stream.write(json.dumps(result,indent=2)+'\n')
    for row in result['cases']:print(f"{row['key']}: maximum represented-time discrepancy {row['maximumSampleErrorMs']:.9g} ms",flush=True)
    print('Clock arithmetic audited. This has not replayed the neuronal trajectories or repaired their failed calcium gate.')
