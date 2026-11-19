"""Independent organ replay; execute only in its visible PowerShell window."""
from contextlib import ExitStack
from decimal import Decimal as D,localcontext
import gzip
import numpy as np
from scipy.linalg import expm
from coupled_canal_reference import read,write,generator_decimal,generator_binary64,exponential,apply,controls


def run(package):
    spec=read(package/'clock-input.json');catalog=read(package/'source/docs/reference-data/vestibular-ifediba-2007.json')
    controls(package/'reference-controls')
    out=package/'reference-trajectories';out.mkdir();fine=spec['subdivisions'][-1]
    angular={p:[r for r in read(package/f'baseline/reference-{p}.json')['rows'] if r['sensorId']=='centre'] for p in spec['referencePrecisions']}
    assert all(len(rows)==201 and [r['step'] for r in rows]==list(range(201)) for rows in angular.values())
    operators={};records=[]
    for precision in spec['referencePrecisions']:
        with localcontext() as context:
            context.prec=precision
            step=D.from_float(spec['frame']['clock']['physicsStepMs'])/D(1000)/D(fine)
            matrix,metadata=exponential([[v*step for v in row] for row in generator_decimal(catalog)],precision)
            operators[precision]=matrix;records.append(dict(precision=precision,stepSeconds=str(step),matrix=[[str(v) for v in row] for row in matrix],metadata=metadata))
    binary_step=(spec['frame']['clock']['physicsStepMs']/1000)/fine
    operators['scipy']=expm(generator_binary64(catalog)*binary_step)
    write(out/'operators.json',records);np.savez_compressed(out/'scipy-operator.npz',matrix=operators['scipy'])
    count=spec['expectedIntervals']*fine+1;tick=0
    values={p:np.empty((count,12)) for p in [60,90,'scipy']};points={p:[D(0)]*15 for p in (60,90)};point64=np.zeros(15)
    with ExitStack() as stack:
        streams={p:stack.enter_context(gzip.open(out/f'decimal{p}.csv.gz','xt',encoding='utf-8',newline='')) for p in (60,90)}
        def retain():
            for precision,point in points.items():
                values[precision][tick]=[float(x) for x in point[:12]]
                streams[precision].write(str(tick)+','+','.join(str(x) for x in point[:12])+'\n')
            values['scipy'][tick]=point64[:12]
        retain()
        for held_step in range(1,201):
            # Step 200 is the closing sample only: never use it retroactively.
            if held_step==200:break
            for precision in (60,90):points[precision][12:]=[D(x) for x in angular[precision][held_step]['projection']['prime']]
            point64[12:]=[float(x) for x in angular[90][held_step]['projection']['prime']]
            for _ in range(fine):
                for precision in (60,90):points[precision]=apply(operators[precision],points[precision],precision)
                point64=operators['scipy']@point64;tick+=1;retain()
            if held_step%20==0:print(f'INDEPENDENT CLOCK INTERVAL {held_step}/199',flush=True)
    assert tick+1==count
    assert all(np.isfinite(v).all() for v in values.values())
    np.savez_compressed(out/'samples.npz',ticks=np.arange(count,dtype=np.int64),**{'decimal'+str(p):values[p] for p in (60,90)},scipy=values['scipy'])
    write(out/'results.json',dict(completed=True,samples=count,intervals=199,heldSteps=list(range(1,200)),initialAtStep=1,
        angularInputsFromIndependentSavedReferences=True,nativeValuesUsedForIntegration=False,continuousMotionAccuracyQualified=False,
        anatomicalCalibrationQualified=False,afferentFiringQualified=False,avatarBalanceAssessed=False))
