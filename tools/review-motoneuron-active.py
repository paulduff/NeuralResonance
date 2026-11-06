"""Audit a completed active motor-cell capture; do not integrate or alter traces."""
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET
import numpy as np

read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path)
    result=importlib.util.module_from_spec(spec);spec.loader.exec_module(result);return result


def main(run,out):
    run=run.resolve();out=out.resolve();assert not out.exists() and run not in out.parents and run!=out
    hashing=module('capture_hashing',Path(__file__).with_name('review-isometric-plant.py'))
    status=read(run/'window-status.json');config=read(run/'configuration.json');manifest=read(run/'backup-manifest.json')
    backup=Path(config['BackupDestination']).resolve()
    assert run==Path(manifest['Source']).resolve() and backup==Path(manifest['Destination']).resolve()
    assert status['Status']=='FAILED' and status['ExitCode']==1 and status['PassedMotoneuronAndManifestTests']==10
    assert status['Revision']==config['Revision']=='1ef1b6a9426bd319d9866dbcc39e096af8d06035'
    assert not any(status[k] for k in ('LiveBrainAccessed','OldSavesLoaded','LiveMotorPathEnabled','NeuronalSourceQualified','AvatarRecoveryAssessed'))
    assert hashing.digest(run/'backup-manifest.json')==hashing.digest(backup/'backup-manifest.json')
    sys.path.insert(0,str(run/'source/tools'))
    reference=module('frozen_active_metrics',run/'source/tools/motoneuron-active-reference.py')
    comparison=read(run/'motoneuron-active-reference/comparison.json')
    inputs=read(run/'motoneuron-active-input.json');protocol=read(run/'motoneuron-active-study/protocol.json')
    results=read(run/'motoneuron-active-study/results.json');criteria=inputs['criteria']
    assert inputs==reference.protocol() and comparison['criteria']==criteria
    assert comparison['completed'] and not comparison['numericalCriteriaPassed'] and comparison['actionPotentialAndCalciumExercisePassed']
    assert results['completed'] and results['successfulCases']==results['expectedCases']==len(results['cases'])==30
    assert [c['scenario'] for c in comparison['cases']]==[c['key'] for c in inputs['cases']]
    assert protocol['inputSha256']==hashing.digest(run/'motoneuron-active-input.json')
    assert protocol['specification']=={k:inputs[k] for k in protocol['specification']}
    grid=np.arange(60001)*.1;cap=np.asarray(protocol['capacitancesPf']);summaries=[]
    with ThreadPoolExecutor(max_workers=2) as pool:
        left=pool.submit(hashing.audit,run,manifest);right=pool.submit(hashing.audit,backup,manifest)
        for saved in comparison['cases']:
            name=saved['scenario'];refs={};assert len(saved['references'])==3
            for row in saved['references']:
                assert row['completed']
                with np.load(run/'motoneuron-active-reference'/(name+'-'+row['method']+'.npz')) as trace:
                    t=trace['timeMs'];state=trace['state'];crossings=trace['crossingsMs']
                assert np.array_equal(t,grid)
                m=reference.metrics(grid,state,crossings,cap)
                assert m==row['metrics'] and reference.physical(m,criteria)==row['physicalAndAccountingPassed']
                refs[row['method']]=(state,crossings)
            for contrast in saved['referenceContrasts']:
                a,sa=refs[contrast['left']];b,sb=refs[contrast['right']]
                error=reference.compare(a,b,sa,sb)
                assert error==contrast['maximumAbsolute'] and reference.meets(error,criteria,'reference')==contrast['passed']
            ref_ok=all(r['physicalAndAccountingPassed'] for r in saved['references']) and all(c['passed'] for c in saved['referenceContrasts'])
            ref,rs=refs['dop853-tight'];candidate_summary=[]
            for row in saved['candidates']:
                original=next(c for c in results['cases'] if c['key']==row['key']);assert original['completed'] and row['completed']
                trace=np.loadtxt(run/'motoneuron-active-study'/(row['key']+'.csv'),delimiter=',',skiprows=1)
                assert trace.shape==(60001,31) and np.array_equal(trace[:,0],grid)
                state=trace[:,1:];crossings=np.asarray(original['crossingsMs'])
                m=reference.metrics(grid,state,crossings,cap);error=reference.compare(state,ref,crossings,rs)
                assert m==row['metrics'] and error==row['maximumAbsolute']
                physical=(reference.physical(m,criteria) and original['maximumCalciumLedgerErrorMm']<=criteria['calciumLedgerMm']
                          and original['maximumChargeLedgerErrorFc']<=criteria['chargeLedgerFc'])
                assert physical==row['physicalAndAccountingPassed']
                assert bool(physical and reference.meets(error,criteria,'candidate'))==row['passed']
                candidate_summary.append(dict(stepMs=row['stepMs'],crossingCount=m['crossingCount'],maximumAbsolute=error,
                    physicalAndAccountingPassed=bool(physical),maximumCalciumLedgerErrorMm=original['maximumCalciumLedgerErrorMm'],
                    maximumChargeLedgerErrorFc=original['maximumChargeLedgerErrorFc'],passed=row['passed']))
            refinement=[]
            for previous in saved['refinement']:
                coarse,fine=[next(c for c in saved['candidates'] if c['stepMs']==previous[k]) for k in ('coarseStepMs','fineStepMs')]
                failing=[]
                for field in ('voltageMv','gate','calciumMm','crossingMs'):
                    uncertainty=max(c['maximumAbsolute'][field] for c in saved['referenceContrasts'])
                    limit=max(2*uncertainty,criteria['refinementRatio']*coarse['maximumAbsolute'][field])
                    value=fine['maximumAbsolute'][field]
                    if value>limit:failing.append(dict(field=field,error=value,limit=limit,referenceDisagreement=uncertainty))
                passed=ref_ok and coarse['maximumAbsolute']['equalCrossingCounts'] and fine['maximumAbsolute']['equalCrossingCounts'] and not failing
                assert bool(passed)==previous['passed']
                refinement.append(dict(**previous,blockedByReference=not ref_ok,failingFields=failing))
            passed=ref_ok and saved['candidates'][-1]['passed'] and all(c['physicalAndAccountingPassed'] for c in saved['candidates']) and all(p['passed'] for p in refinement)
            assert bool(passed)==saved['passed']
            summaries.append(dict(scenario=name,passed=saved['passed'],referenceContrasts=saved['referenceContrasts'],
                referenceSpikeCounts={r['method']:r['metrics']['crossingCount'] for r in saved['references']},
                referencesPhysicalAndAccountingPassed=all(r['physicalAndAccountingPassed'] for r in saved['references']),
                candidates=candidate_summary,refinement=refinement))
            print('Reviewed retained '+name+' traces; no simulation.',flush=True)
        hashes=left.result();assert hashes==right.result()
    for item in config['PackageFiles']:assert hashes[Path(item['Path']).as_posix().lower()]==item['Sha256'].upper()
    for name in ('build-binaries.json','motoneuron-build-binaries.json'):
        for item in read(run/name):assert hashes[Path(item['Path']).resolve().relative_to(run).as_posix().lower()]==item['Sha256'].upper()
    ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    tests=ET.parse(run/'test-results/motor-axon.trx').findall('.//t:UnitTestResult',ns)
    assert len(tests)==10 and all(t.attrib['outcome']=='Passed' for t in tests)
    assert sum('.MotoneuronReferenceTests.' in t.attrib['testName'] for t in tests)==9 and sum('.SolutionManifestTests.' in t.attrib['testName'] for t in tests)==1
    report=dict(reviewedUtc=datetime.now(timezone.utc).isoformat(),capture=str(run),backup=str(backup),sourceRevision=config['Revision'],
        completedUtc=status['CompletedUtc'],verifiedFilesPerCopy=len(hashes),verifiedBytesPerCopy=sum(x['Bytes'] for x in manifest['Files']),
        packagedFilesVerified=len(config['PackageFiles']),passedFocusedAndManifestTests=10,criteria=criteria,
        cases=summaries,originalNumericalCriteriaPassed=False,allRetainedStateArraysFinite=True,referenceConditionsCompleted=30,
        candidateConditionsCompleted=30,physiologicalCalibrationComplete=False,neuronalSourceQualified=False,avatarRecoveryAssessed=False)
    out.mkdir(parents=True);(out/'review.json').write_text(json.dumps(report,indent=2,allow_nan=False)+'\n')
    print(json.dumps({k:v for k,v in report.items() if k!='cases'},indent=2),flush=True)


if __name__=='__main__':main(Path(sys.argv[1]),Path(sys.argv[2]))
