"""Saved-trace arithmetic only: correlate errors with sodium table crossings."""
import argparse
import csv
import json
from pathlib import Path
import shutil
import numpy as np
from afferent_precision_gate import h,read,save
from afferent_trajectory_protocol import group


def diagnose(review_folder,output):
    h.verify(review_folder,['review.json']);review=read(review_folder/'review.json')
    assert review['operatorReportedCompletion'] and review['savedAnalysisExactlyReproduced']
    assert review['originalFailurePreserved'] and not review['numericalTrajectoryCriteriaPassed']
    capture=Path(review['capture']);assert h.digest(capture/'backup-manifest.json')==review['manifestSha256']
    spec=read(capture/'trajectory-input.json');files=['trajectory-input.json','trajectory-analysis.json']
    for case in spec['cases']:
        files+=['reference-trajectories/'+case['key']+'-radau-tight/samples.npz']
        for ticks in spec['stepTicks']:
            files+=['study/'+case['key']+'-ticks'+str(ticks)+'/'+n for n in ('initial.json','trajectory.csv')]
    h.verify(capture,files);rows=[]
    for case in spec['cases']:
        key=case['key'];initial=read(capture/'study'/f'{key}-ticks1/initial.json');names=initial['stateLabels']+initial['ledgerLabels']
        with np.load(capture/'reference-trajectories'/f'{key}-radau-tight/samples.npz',allow_pickle=False) as data:
            reference=data['values'].copy();grid=data['ticks'].copy()
        time=grid/spec['ticksPerMs'];crossings=[]
        for j,name in enumerate(names):
            if not(name.startswith('axon/') and name.endswith('/potentialMv')):continue
            voltage=reference[:,j]-case['fixture']['sensory']['perilymphMv']
            for boundary in (-55,60):
                for i in np.flatnonzero((voltage[:-1]-boundary)*(voltage[1:]-boundary)<0):
                    crossings.append(dict(label=name,boundaryMv=boundary,lowerMs=float(time[i]),upperMs=float(time[i+1])))
        assert crossings
        first=min(c['lowerMs'] for c in crossings);before=time<=first
        gates=[i for i,n in enumerate(names) if n.startswith('axon/') and n.endswith('/sodiumActivation')]
        bound=spec['candidateComparison'];allowed=np.array([bound['absolute'][group(n)] for n in names])+bound['relative']*np.abs(reference)
        runs=[]
        for tick in spec['stepTicks']:
            with (capture/'study'/f'{key}-ticks{tick}/trajectory.csv').open(newline='') as stream:
                saved=[r for r in csv.DictReader(stream) if int(r['tick'])%spec['sampleTicks']==0]
            assert [int(r['tick']) for r in saved]==grid.tolist()
            values=np.array([[float(r[n]) for n in names] for r in saved]);error=np.abs(values-reference);scaled=error/allowed
            at=np.unravel_index(np.argmax(scaled),scaled.shape)
            runs.append(dict(stepTicks=tick,maximumBeforeFirstBoundary=float(np.max(scaled[before])),maximumWhole=float(np.max(scaled)),
                sodiumActivationErrorBefore=float(np.max(error[before][:,gates])),sodiumActivationErrorWhole=float(np.max(error[:,gates])),
                worstLabel=names[at[1]],worstTimeMs=float(time[at[0]])))
        rows.append(dict(key=key,crossings=crossings,runs=runs))
    assert not output.exists();output.mkdir()
    save(output/'diagnosis.json',dict(capture=str(capture),review=review,sourceFiles=[dict(path=n,sha256=h.digest(capture/n)) for n in files],
        cases=rows,savedArithmeticOnly=True,equationsEvaluated=False,testsExecuted=False,
        interpretation='Smooth-prefix refinement and boundary-correlated growth support a discontinuity hypothesis; controlled causal comparison remains required.',
        originalBoundsChanged=False,originalFailurePreserved=True,physiologyQualified=False))
    shutil.copyfile(__file__,output/Path(__file__).name)
    print(json.dumps(dict(output=str(output),cases=len(rows),savedArithmeticOnly=True),indent=2),flush=True)


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('review',type=Path);p.add_argument('output',type=Path);a=p.parse_args()
    diagnose(a.review.resolve(),a.output.resolve())
