"""Offline audit of three operator-reported captures. No solver or GPU execution."""
from datetime import datetime, timezone
from hashlib import sha256
import importlib.util
import json
import os
from pathlib import Path
import shutil
import sys
import xml.etree.ElementTree as ET
for name in ('OPENBLAS_NUM_THREADS','OMP_NUM_THREADS','MKL_NUM_THREADS'):os.environ[name]='1'
import numpy as np

def longpath(path):
    path=Path(path).absolute();text=str(path)
    return Path('\\\\?\\'+text) if os.name=='nt' and not text.startswith('\\\\?\\') else path
def display(path):
    text=str(path);return text[4:] if text.startswith('\\\\?\\') else text
root=longpath(Path(__file__).resolve().parents[1]);output=root/'artifacts/compute-cell-review-20261109'
assert output.is_dir() and not (output/'review.json').exists()
read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
def save(name,data):(output/name).write_text(json.dumps(data,indent=2,allow_nan=False)+'\n',encoding='utf-8')
def digest(path):
    h=sha256()
    with path.open('rb') as stream:
        for block in iter(lambda:stream.read(4*1024*1024),b''):h.update(block)
    return h.hexdigest().upper()
def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m
def verify(capture,binaries,exact=True):
    manifest=read(capture/'backup-manifest.json');backup=longpath(manifest['Destination'])
    assert (capture/'backup-manifest.json').read_bytes()==(backup/'backup-manifest.json').read_bytes()
    names=set();total=0
    for entry in manifest['Files']:
        relative=Path(entry['Path']);assert not relative.drive and '..' not in relative.parts
        assert relative.as_posix() not in names;names.add(relative.as_posix());total+=entry['Bytes']
        for folder in (capture,backup):
            path=folder/relative;assert path.stat().st_size==entry['Bytes'] and digest(path)==entry['Sha256'],str(path)
    for folder in (capture,backup):
        actual={p.relative_to(folder).as_posix() for p in folder.rglob('*') if p.is_file()}
        assert actual==names|{'backup-manifest.json'} if exact else names|{'backup-manifest.json'}<=actual
    configuration=read(capture/'configuration.json')
    for entry in configuration['PackageFiles']:assert digest(capture/entry['Path'])==entry['Sha256']
    for name in binaries:
        for entry in read(capture/name):
            path=longpath(entry['Path']);assert path.is_relative_to(capture) and digest(path)==entry['Sha256']
    return dict(capture=display(capture),originalBackup=display(backup),sourceRevision=configuration['Revision'],
        verifiedManifestFilesPerCopy=len(names),verifiedManifestBytesPerCopy=total,manifestCoversEntireCapture=exact,
        verifiedPackagedFiles=len(configuration['PackageFiles']))
def tests(capture,count):
    paths=list((capture/'test-results').glob('*.trx'));assert len(paths)==1
    ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    rows=ET.parse(paths[0]).findall('.//t:UnitTestResult',ns)
    assert len(rows)==count and all(r.attrib['outcome']=='Passed' for r in rows)
    return len(rows)

gpu=root/'artifacts/cuda-interop-20261109-589561'
gpu_review=verify(gpu,['probe-binaries.json'])
status=read(gpu/'window-status.json');assert status['Status']=='DIAGNOSTICS_COMPLETE' and status['ExitCode']==0
assert gpu_review['sourceRevision']==status['Revision']=='f52c4f8663354ed82169efd32b7794dec18a4dc2'
analysis=module('cuda_byte_audit',root/'tools/analyse-cuda-interop.py').audit(Path(display(gpu/'study')))
assert analysis==read(gpu/'study/analysis.json') and analysis['exactTransferAndArithmeticCases']==6
gpu_review.update(completedUtc=status['CompletedUtc'],savedBytesReverified=True,analysis=analysis)
save('cuda-review.json',gpu_review);print('CUDA capture, backup and all returned bytes verified.',flush=True)

motor=root/'artifacts/motoneuron-numerical-20261106-640f85'
motor_review=verify(motor,['build-binaries.json','motoneuron-build-binaries.json'])
status=read(motor/'window-status.json');assert status['Status']=='FAILED' and status['ExitCode']==1
assert motor_review['sourceRevision']==status['Revision']=='22d295951135b2b04992884ddfd309607cc40f6c'
assert tests(motor,12)==status['PassedMotoneuronAndManifestTests']
spec=read(motor/'motoneuron-active-input.json');old=read(motor/'motoneuron-failed-active-baseline/motoneuron-active-input.json')
for key in ('cases','criteria','stepsMs','initialVoltageMv','thresholdMv','durationMs','sampleMs'):assert spec[key]==old[key]
saved=read(motor/'motoneuron-active-reference/comparison.json');assert saved['completed'] and not saved['numericalCriteriaPassed']
sys.path.insert(0,str(motor/'source/tools'))
audit=module('motor_saved_array_audit',motor/'source/tools/motoneuron-active-reference.py')
_,initial,area,cap,axial=audit.source_model(motor/'motoneuron-input')
outcomes=read(motor/'motoneuron-active-study/results.json');assert outcomes['completed'] and len(outcomes['cases'])==30
assert saved['criteria']==spec['criteria'];criteria=spec['criteria'];grid=np.arange(60001)*.1
rows=[];failed_refinements=[];all_reference_checks=True;finest_passed=0;accounting_count=0;candidate_passed=0
for row in saved['cases']:
    name=row['scenario'];reference={};errors={}
    for recorded in row['references']:
        method=recorded['method'];assert recorded['completed']
        with np.load(motor/'motoneuron-active-reference'/(name+'-'+method+'.npz'),allow_pickle=False) as data:
            assert np.array_equal(data['timeMs'],grid)
            state=data['state'].copy();crossings=data['crossingsMs'].copy()
        measured=audit.metrics(grid,state,crossings,cap)
        assert measured==recorded['metrics'] and audit.physical(measured,criteria)==recorded['physicalAndAccountingPassed']
        reference[method]=(state,crossings)
    for contrast in row['referenceContrasts']:
        a,sa=reference[contrast['left']];b,sb=reference[contrast['right']]
        error=audit.compare(a,b,sa,sb);assert error==contrast['maximumAbsolute']
        assert audit.meets(error,criteria,'reference')==contrast['passed']
    ref_ok=len(reference)==3 and len(row['referenceContrasts'])==2 and all(c['passed'] for c in row['referenceContrasts']) and all(c['physicalAndAccountingPassed'] for c in row['references'])
    all_reference_checks&=ref_ok
    target,events=reference['dop853-tighter']
    for candidate in row['candidates']:
        original=next(c for c in outcomes['cases'] if c['key']==candidate['key']);assert original['completed']
        trace=np.loadtxt(motor/'motoneuron-active-study'/(candidate['key']+'.csv'),delimiter=',',skiprows=1)
        assert trace.shape==(60001,31) and np.array_equal(trace[:,0],grid)
        state=trace[:,1:];crossings=np.asarray(original['crossingsMs']);assert np.max(np.abs(state[0]-initial))<=1e-12
        measured=audit.metrics(grid,state,crossings,cap);error=audit.compare(state,target,crossings,events)
        assert measured==candidate['metrics'] and error==candidate['maximumAbsolute']
        accounting=audit.physical(measured,criteria) and original['maximumCalciumLedgerErrorMm']<=criteria['calciumLedgerMm'] and original['maximumChargeLedgerErrorFc']<=criteria['chargeLedgerFc']
        assert accounting==candidate['physicalAndAccountingPassed'] and (accounting and audit.meets(error,criteria,'candidate'))==candidate['passed']
        errors[candidate['stepMs']]=error;accounting_count+=accounting;candidate_passed+=candidate['passed']
    ordered=sorted(row['candidates'],key=lambda c:c['stepMs'],reverse=True);refined=[]
    for coarse,fine in zip(ordered,ordered[1:]):
        passed=ref_ok and coarse['maximumAbsolute']['equalCrossingCounts'] and fine['maximumAbsolute']['equalCrossingCounts']
        for field in ('voltageMv','gate','calciumMm','crossingMs'):
            uncertainty=max(c['maximumAbsolute'][field] for c in row['referenceContrasts'])
            before=coarse['maximumAbsolute'][field];after=fine['maximumAbsolute'][field];limit=max(2*uncertainty,criteria['refinementRatio']*before)
            if after>limit:
                passed=False;failed_refinements.append(dict(scenario=name,coarseStepMs=coarse['stepMs'],fineStepMs=fine['stepMs'],field=field,coarseError=before,fineError=after,referenceSpread=uncertainty,originalLimit=limit))
        refined.append(dict(coarseStepMs=coarse['stepMs'],fineStepMs=fine['stepMs'],passed=bool(passed)))
    assert refined==row['refinement']
    passed=ref_ok and len(ordered)==3 and ordered[-1]['passed'] and all(x['passed'] for x in refined) and all(c['physicalAndAccountingPassed'] for c in ordered)
    assert passed==row['passed'];finest_passed+=ordered[-1]['passed']
    rows.append(dict(scenario=name,passed=bool(passed),finest=ordered[-1]['maximumAbsolute'],finestAccounting=ordered[-1]['metrics'],refinement=refined))
    print('Recomputed motor case '+name,flush=True)
motor_review.update(completedUtc=status['CompletedUtc'],originalStatus='FAILED',passedTests=12,biologicalInputsAndOriginalBoundsUnchanged=True,
    cases=rows,passedScenarios=sum(r['passed'] for r in rows),allReferenceChecksPassed=bool(all_reference_checks),
    finestAbsoluteChecksPassed=finest_passed,candidateAbsoluteChecksPassed=candidate_passed,candidateAccountingPassed=accounting_count,
    failedRefinements=failed_refinements,numericalCriteriaPassed=False,physiologyQualified=False,avatarRecoveryAssessed=False)
save('motor-review.json',motor_review)

golgi=root/'artifacts/motor-axon-20261106-42c421';observed=read(output/'golgi-terminal-observation.json')
assert observed['State']=='Ready' and observed['LastTaskResult']==3221225786 and not observed['MatchingProcesses']
golgi_review=verify(golgi,['build-binaries.json'],exact=False)
assert golgi_review['sourceRevision']=='73db8166d81e0f88c46e6368aee196bf5b647070' and tests(golgi,33)==33
assert read(golgi/'window-status.json')['Status']=='RUNNING' and not (golgi/'reference-refinement/analysis.json').exists()
sealed=output/'golgi-stopped-capture';assert not sealed.exists();sealed.mkdir()
source_files=[p for p in golgi.rglob('*') if p.is_file()];sealed_entries=[]
for path in source_files:
    assert not path.is_symlink();relative=path.relative_to(golgi);target=sealed/relative;target.parent.mkdir(parents=True,exist_ok=True)
    before=digest(path);shutil.copy2(path,target);assert before==digest(path)==digest(target)
    sealed_entries.append(dict(path=relative.as_posix(),sha256=before,bytes=path.stat().st_size))
assert {p.relative_to(golgi) for p in source_files}=={p.relative_to(golgi) for p in golgi.rglob('*') if p.is_file()}
save('golgi-sealed-files.json',sealed_entries)
audit_g=module('golgi_saved_array_audit',golgi/'source/tools/golgi-reference-refinement.py')
partial=read(golgi/'reference-refinement/partial-comparisons.json');solves=read(golgi/'reference-refinement/completed-solves.json')
assert len(solves)==11 and len(partial['comparisons'])==10
for row in partial['comparisons']:
    name=row['protocol'];folder=golgi/'reference-refinement'
    a=golgi/'golgi-input/study/active-reference'/(name+'-strict.npz') if row['pair']=='old-strict-vs-bdf-tighter' else folder/(name+'-bdf-tighter.npz')
    b=folder/(name+('-bdf-tighter.npz' if row['pair']=='old-strict-vs-bdf-tighter' else '-radau-strict.npz'))
    assert dict(protocol=name,pair=row['pair'],**audit_g.compare(audit_g.load(a),audit_g.load(b)))==row
assert not (golgi/'reference-refinement/no-sk-radau-strict.npz').exists()
golgi_review.update(originalStatus='RUNNING',reviewedOutcome='INTERRUPTED_INCOMPLETE',terminalObservation=observed,passedTransportTests=33,
    completedReferenceSolves=11,requiredReferenceSolves=12,recomputedReferenceContrasts=10,passedRecordedContrasts=sum(r['passedOriginalCriteria'] for r in partial['comparisons']),
    missingSolve='no-sk / radau-strict',sealedCapture=display(sealed),sealedFiles=len(sealed_entries),sealedBytes=sum(e['bytes'] for e in sealed_entries),
    candidateComparisonsRetainedButNotRecomputed=True,referenceQualificationComplete=False,physiologyQualified=False,avatarRecoveryAssessed=False)
save('golgi-review.json',golgi_review)
save('review.json',dict(reviewedUtc=datetime.now(timezone.utc).isoformat(),cuda=gpu_review,motor=motor_review,golgi=golgi_review,
    originalCapturesUnchanged=True,noSolversOrGpuExecuted=True))
print('All reported capture reviews saved; complete the new review backup separately.',flush=True)
