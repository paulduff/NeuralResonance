"""Independent nonuniform organ replay; execute only in visible PowerShell."""
from contextlib import ExitStack
from decimal import Decimal as D,localcontext
import gzip
import numpy as np
from scipy.linalg import expm
from coupled_canal_reference import read,write,generator_decimal,generator_binary64,exponential,apply,controls


def run(package):
    spec=read(package/'graded-input.json');catalog=read(package/'source/docs/reference-data/vestibular-ifediba-2007.json')
    controls(package/'reference-controls')
    out=package/'reference-trajectories';out.mkdir();fine=spec['referenceTicksPerInterval']
    grid=spec['referenceEndpointTicks'];gaps=[b-a for a,b in zip([0]+grid,grid)];distinct=sorted(set(gaps))
    angular={p:[r for r in read(package/f'baseline/baseline/reference-{p}.json')['rows'] if r['sensorId']=='centre'] for p in (60,90)}
    assert all(len(rows)==201 and [r['step'] for r in rows]==list(range(201)) for rows in angular.values())
    operators={};records=[]
    for precision in (60,90):
        operators[precision]={}
        with localcontext() as context:
            context.prec=precision
            generator=generator_decimal(catalog)
            for gap in distinct:
                step=D.from_float(spec['frame']['clock']['physicsStepMs'])/D(1000)/D(fine)*D(gap)
                matrix,metadata=exponential([[v*step for v in row] for row in generator],precision)
                operators[precision][gap]=matrix
                records.append(dict(precision=precision,gapTicks=gap,stepSeconds=str(step),matrix=[[str(v) for v in row] for row in matrix],metadata=metadata))
    generator64=generator_binary64(catalog);binary_tick=(spec['frame']['clock']['physicsStepMs']/1000)/fine
    operators['scipy']={gap:expm(generator64*(binary_tick*gap)) for gap in distinct}
    write(out/'operators.json',records)
    np.savez_compressed(out/'scipy-operators.npz',**{'gap'+str(g):a for g,a in operators['scipy'].items()})
    count=spec['expectedReferenceSamples'];row=0;absolute_tick=0
    values={p:np.empty((count,12)) for p in (60,90,'scipy')};saved_ticks=np.empty(count,dtype=np.int64)
    points={p:[D(0)]*15 for p in (60,90)};point64=np.zeros(15)
    with ExitStack() as stack:
        streams={p:stack.enter_context(gzip.open(out/f'decimal{p}.csv.gz','xt',encoding='utf-8',newline='')) for p in (60,90)}
        def retain():
            saved_ticks[row]=absolute_tick
            for precision,point in points.items():
                values[precision][row]=[float(x) for x in point[:12]]
                streams[precision].write(str(absolute_tick)+','+','.join(str(x) for x in point[:12])+'\n')
            values['scipy'][row]=point64[:12]
        retain()
        for held_step in range(1,200):
            for precision in (60,90):points[precision][12:]=[D(x) for x in angular[precision][held_step]['projection']['prime']]
            point64[12:]=[float(x) for x in angular[90][held_step]['projection']['prime']]
            for gap in gaps:
                for precision in (60,90):points[precision]=apply(operators[precision][gap],points[precision],precision)
                point64=operators['scipy'][gap]@point64;row+=1;absolute_tick+=gap;retain()
            if held_step%20==0:print(f'INDEPENDENT GRADED INTERVAL {held_step}/199',flush=True)
    assert row+1==count and absolute_tick==199*fine
    assert all(np.isfinite(v).all() for v in values.values())
    np.savez_compressed(out/'samples.npz',ticks=saved_ticks,**{'decimal'+str(p):values[p] for p in (60,90)},scipy=values['scipy'])
    write(out/'results.json',dict(completed=True,samples=count,intervals=199,heldSteps=list(range(1,200)),initialAtStep=1,
        ticksPerInterval=fine,endpointTicks=grid,angularInputsFromIndependentSavedReferences=True,nativeValuesUsedForIntegration=False,
        continuousMotionAccuracyQualified=False,anatomicalCalibrationQualified=False,afferentFiringQualified=False,avatarBalanceAssessed=False))
