"""Tighter and cross-method Golgi reference comparison; no biological retuning."""
from datetime import datetime, timezone
import hashlib
import importlib.util
import json
from pathlib import Path
import sys
import numpy as np
import scipy

read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path)
    result=importlib.util.module_from_spec(spec);spec.loader.exec_module(result)
    return result


def load(path):
    with np.load(path,allow_pickle=False) as data:return {k:data[k] for k in data.files}


def compare(a,b):
    assert np.array_equal(a['times'],b['times']) and a['states'].shape==b['states'].shape==(24001,159)
    assert np.isfinite(a['states']).all() and np.isfinite(b['states']).all()
    error=np.abs(a['states']-b['states'])
    timing=float(np.abs(a['spikes']-b['spikes']).max(initial=0)) if len(a['spikes'])==len(b['spikes']) else None
    voltage=float(error[:,0].max());idx=int(error[:,0].argmax())
    return dict(counts=[len(a['spikes']),len(b['spikes'])],maximumOrdinalDifferenceMs=timing,
        maximumSomaDifferenceMv=voltage,maximumSomaDifferenceAtMs=float(a['times'][idx]),
        maximumCableDifferenceMv=float(error[:,:133].max()),maximumGateDifference=float(error[:,133:151].max()),
        maximumSkDifference=float(error[:,151:157].max()),maximumCalciumDifferenceMm=float(error[:,157:].max()/1000),
        passedOriginalCriteria=timing is not None and timing<.01 and voltage<.05)


def main(package):
    inputs=package/'golgi-input';configuration=read(package/'configuration.json')
    for entry in configuration['PackageFiles']:
        assert hashlib.sha256((package/entry['Path']).read_bytes()).hexdigest().upper()==entry['Sha256'],entry['Path']
    output=package/'reference-refinement'
    if output.exists():raise ValueError('Use a new output directory; retain previous evidence.')
    output.mkdir()
    reference=module('golgi_reference',package/'source/tools/golgi-active-reference.py')
    cable=module('golgi_cable',package/'source/tools/analyse-golgi-cell.py')
    nodes=cable.morphology(inputs/'reference');cap,stiffness=cable.matrix(nodes)
    channels=reference.ReferenceChannels(inputs/'reference')
    gate_error=reference.verify_clamps(channels,read(inputs/'clamps/channels.json'))
    solver=reference.CoupledReference(nodes,cap,stiffness,channels)
    cases=read(inputs/'study/results.json')['cases']
    levels=[dict(label='bdf-tighter',method='BDF',rtol=1e-12,voltage_atol=1e-13,gate_atol=1e-15,calcium_atol=1e-14,max_step=.125),
            dict(label='radau-strict',method='Radau',rtol=1e-10,voltage_atol=1e-11,gate_atol=1e-13,calcium_atol=1e-12,max_step=.125)]
    specification=dict(createdUtc=datetime.now(timezone.utc).isoformat(),baseline=configuration['GolgiBaseline'],
        protocols=['quiet','depolarize','hyperpolarize','no-nap','no-hcn','no-sk'],levels=levels,
        inheritedCriteria=dict(maximumSomaDifferenceMv=.05,maximumOrdinalDifferenceMs=.01,matchingCounts=True),
        requiredPairs=['old-strict-vs-bdf-tighter','bdf-tighter-vs-radau-strict'],
        scipyVersion=scipy.__version__,numpyVersion=np.__version__,candidateUnchanged=True,
        biologicalParametersChanged=False,wholeCellPhysiologyQualified=False,avatarRecoveryAssessed=False)
    (output/'protocol.json').write_text(json.dumps(specification,indent=2)+'\n')
    comparisons=[];runs=[];candidate=[]
    try:
        for name in specification['protocols']:
            selected=[c for c in cases if c['protocol']==name];assert len(selected)==3
            old=load(inputs/f'study/active-reference/{name}-strict.npz');new={}
            for level in levels:
                print(name+': '+level['label'],flush=True)
                settings={k:v for k,v in level.items() if k!='label'}
                result,stats=solver.run(selected[0],**settings)
                np.savez_compressed(output/(name+'-'+level['label']+'.npz'),**result)
                runs.append(dict(protocol=name,**level,**stats));new[level['label']]=result
                (output/'completed-solves.json').write_text(json.dumps(runs,indent=2)+'\n')
            for pair,a,b in (('old-strict-vs-bdf-tighter',old,new['bdf-tighter']),
                             ('bdf-tighter-vs-radau-strict',new['bdf-tighter'],new['radau-strict'])):
                comparison=dict(protocol=name,pair=pair,**compare(a,b));comparisons.append(comparison)
                print('  '+pair+': '+str(comparison['passedOriginalCriteria']),flush=True)
            for case in selected:
                trace=np.genfromtxt(inputs/'study'/(case['key']+'.csv'),delimiter=',',names=True)
                for level in levels:
                    ref=new[level['label']];s=np.array(case['somaticCrossingsMs']);v=np.abs(trace['somaMv']-(ref['states'][:,0]-55))
                    candidate.append(dict(protocol=name,stepMs=case['stepMs'],reference=level['label'],counts=[len(s),len(ref['spikes'])],
                        maximumOrdinalDifferenceMs=float(np.abs(s-ref['spikes']).max(initial=0)) if len(s)==len(ref['spikes']) else None,
                        maximumSomaDifferenceMv=float(v.max()),maximumFirst20MsDifferenceMv=float(v[ref['times']<=20].max()),
                        maximumHighCalciumDifferenceMm=float(np.abs(trace['highCaMm']-ref['states'][:,-2]/1000).max())))
            (output/'partial-comparisons.json').write_text(json.dumps(dict(comparisons=comparisons,candidate=candidate),indent=2,allow_nan=False)+'\n')
        report=dict(completed=True,gateError=gate_error,allOriginalReferenceCriteriaPassed=all(c['passedOriginalCriteria'] for c in comparisons),
            completedSolves=len(runs),comparisons=comparisons,candidateComparisons=candidate,
            activeCandidateQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)
        (output/'analysis.json').write_text(json.dumps(report,indent=2,allow_nan=False)+'\n')
        if not report['allOriginalReferenceCriteriaPassed']:raise RuntimeError('Reference disagreement remains; criteria were not relaxed. All comparisons retained.')
        print('Original reference criteria passed for tighter and cross-method comparisons. Candidate accuracy/physiology remain separate.',flush=True)
    except Exception as error:
        (output/'FAILED.json').write_text(json.dumps(dict(error=repr(error),completedSolves=len(runs),
            completedComparisons=len(comparisons)),indent=2)+'\n')
        raise


if __name__=='__main__':main(Path(sys.argv[1]).resolve())
