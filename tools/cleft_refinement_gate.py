"""Finer unchanged sensory integration against already completed references."""
import argparse
from copy import deepcopy
from datetime import datetime,timezone
import json
from pathlib import Path,PurePosixPath
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile
import numpy as np
import cleft_stationary_gate as component
import cleft_trajectory_gate as original
import cleft_trajectory_analysis as analysis

ROOT=Path(__file__).resolve().parents[1]
h=component.h;read=h.read;save=component.save;COUNTS=component.COUNTS
REVISION='22148e322fe862e8b69c2f42388aa26534b8539a'
PRESERVED=sorted(set(component.PRESERVED+original.PRESERVED+[
    'tools/'+n for n in ('cleft_trajectory_protocol.py','cleft_trajectory_reference.py','cleft_trajectory_analysis.py')]))


def protocol(baseline):
    original.validate(baseline);p=deepcopy(baseline)
    p['version']='joint-cleft-trajectory-refinement-v1';p['ticksPerMs']*=2;p['sampleTicks']*=2
    for c in p['cases']:
        c['totalTicks']*=2
        for s in c['segments']:s['endTick']*=2
    return p


def prepare(review_folder):
    assert not h.git('status','--porcelain'),'Commit first.'
    revision=h.git('rev-parse','HEAD');assert revision==h.git('rev-parse','origin/main'),'Push first.'
    h.verify(review_folder,['review.json','trajectory-analysis.json']);review=read(review_folder/'review.json')
    assert review['sourceRevision']==REVISION and review['operatorReportedCompletion'] and review['savedAnalysisExactlyReproduced']
    assert review['passedTests']==120 and review['referenceCriteriaPassed'] and review['failedCases']==['bundle-eight']
    assert not review['numericalCriteriaPassed'] and review['allSampledRefinementsPassed'] and review['allLedgerCriteriaPassed']
    capture=Path(review['capture']);assert h.digest(capture/'backup-manifest.json')==review['manifestSha256']
    dependencies=['trajectory-input.json','trajectory-analysis.json','managed-runtime-before.json','preparation-environment.json']
    dependencies+=['reference-trajectories/'+p.relative_to(h.io(capture/'reference-trajectories')).as_posix()
                   for p in h.io(capture/'reference-trajectories').rglob('*') if p.is_file()]
    h.verify(capture,dependencies+['source/'+n for n in PRESERVED])
    for name in PRESERVED:assert (ROOT/name).read_text(encoding='utf-8-sig')==h.io(capture/'source'/name).read_text(encoding='utf-8-sig'),name
    assert original.base.environment()==read(capture/'preparation-environment.json'),'Review changed numerical dependencies.'
    methods=component.inventory.inventory(ROOT,COUNTS);assert sum(methods.values())==132
    package=ROOT/'artifacts'/('cleft-refinement-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6])
    backup=Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
    assert not package.exists() and not backup.exists();package.mkdir()
    subprocess.run(['git','archive','--format=zip','--output='+str(package/'source.zip'),revision],cwd=ROOT,check=True)
    with ZipFile(package/'source.zip') as archive:
        for entry in archive.infolist():
            name=PurePosixPath(entry.filename)
            assert not name.is_absolute() and '..' not in name.parts and ':' not in entry.filename and '\\' not in entry.filename
            assert ((entry.external_attr>>16)&0o170000)!=0o120000
            target=h.io(package/'source'/name)
            if entry.is_dir():target.mkdir(parents=True,exist_ok=True)
            else:
                target.parent.mkdir(parents=True,exist_ok=True)
                with target.open('xb') as stream:stream.write(archive.read(entry))
    def copy(source,name):
        target=h.io(package/name);target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(h.io(source),target)
        assert h.digest(source)==h.digest(target)
    for name in dependencies:copy(capture/name,'baseline/'+name)
    copy(review_folder/'review.json','reviewed-trajectory.json')
    copy(capture/'managed-runtime-before.json','qualified-managed-runtime.json')
    copy(capture/'preparation-environment.json','preparation-environment.json')
    copy(package/'source/tools/backup-unity-run.ps1','backup-run.ps1')
    save(package/'trajectory-input.json',protocol(read(capture/'trajectory-input.json')))
    save(package/'provenance.json',dict(originalManifestSha256=review['manifestSha256'],originalRevision=REVISION,
        preservedSources=[dict(path=n,sha256=h.digest(capture/'source'/n)) for n in PRESERVED],
        reusedReferences=[dict(path=n,sha256=h.digest(capture/n)) for n in dependencies],
        originalBoundsChanged=False,modelsExecutedDuringPreparation=False,testsExecutedDuringPreparation=False))
    files=[dict(Path=p.relative_to(h.io(package)).as_posix(),Sha256=h.digest(p)) for p in sorted(h.io(package).rglob('*')) if p.is_file()]
    dotnet=shutil.which('dotnet');assert dotnet
    save(package/'configuration.json',dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),PackageFiles=files,
        DotnetExecutable=dotnet,DotnetExecutableSha256=h.digest(dotnet),PythonExecutable=sys.executable,PythonExecutableSha256=h.digest(sys.executable),
        BackupDestination=str(backup),IsolatedCleftRefinement=True,NoLiveMonitoring=True,ExpectedTestCases=132,ExpectedClassCounts=COUNTS,
        ExpectedTestMethods=methods,TestFilter='|'.join('FullyQualifiedName~'+c for c in COUNTS),ExpectedNativeRuns=21,ExpectedSavedReferences=21,
        IndependentCoupledTrajectoryQualified=False,StationaryInitializationQualified=False,LiveSelectionChanged=False,
        SpatialGeometryCalibrated=False,AfferentFiringQualified=False,PhysiologyQualified=False,AvatarRecoveryAssessed=False))
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,NoLiveMonitoring=True,Launcher=str(package/'source/tools/start-cleft-refinement-window.ps1'))
    save(package.parent/(package.name+'-prepared.json'),pointer);print(json.dumps(pointer,indent=2),flush=True)


def prerequisites(package):
    baseline=original.validate(read(package/'baseline/trajectory-input.json'));p=read(package/'trajectory-input.json')
    assert p==protocol(baseline)
    cfg=read(package/'configuration.json');review=read(package/'reviewed-trajectory.json');v=read(package/'provenance.json')
    assert cfg['IsolatedCleftRefinement'] and cfg['NoLiveMonitoring'] and cfg['ExpectedNativeRuns']==cfg['ExpectedSavedReferences']==21
    assert cfg['ExpectedTestCases']==132 and cfg['ExpectedClassCounts']==COUNTS
    assert review['sourceRevision']==v['originalRevision']==REVISION and review['manifestSha256']==v['originalManifestSha256']
    assert review['operatorReportedCompletion'] and review['savedAnalysisExactlyReproduced'] and review['referenceCriteriaPassed']
    assert review['analysis']==read(package/'baseline/trajectory-analysis.json')
    assert not any(v[n] for n in ('originalBoundsChanged','modelsExecutedDuringPreparation','testsExecutedDuringPreparation'))
    assert {r['path'] for r in v['preservedSources']}==set(PRESERVED)
    for row in v['preservedSources']:assert h.digest(package/'source'/row['path'])==row['sha256']
    for row in v['reusedReferences']:assert h.digest(package/'baseline'/row['path'])==row['sha256']
    assert original.base.environment()==read(package/'preparation-environment.json')
    return baseline,p


def preflight(package):
    prerequisites(package);save(package/'reference-preflight.json',dict(environment=original.base.environment(),modelsExecuted=False,testsExecuted=False))


def runtime(package):
    prerequisites(package);expected=read(package/'qualified-managed-runtime.json')
    assert read(package/'managed-runtime-before.json')==expected
    for directory in expected['directories']:
        root=h.io(directory['path']);assert {str(p) for p in root.rglob('*') if p.is_file()}=={str(h.io(e['path'])) for e in directory['files']}
        for row in directory['files']:assert h.digest(row['path'])==row['sha256']
    print('Reviewed sensory runtime and saved references preserved; no model or test executed.',flush=True)


def compare(package):
    old,p=prerequisites(package);runtime(package);component.inventory.check_tests(package,COUNTS)
    assert read(package/'study/input.json')=={k:p[k] for k in ('version','kernelVersion','sessionId','ticksPerMs','sampleTicks','stepTicks',
        'expectedCases','expectedNativeRuns','expectedReferences','cases','nonlinearSettings')}
    assert read(package/'study/results.json')==dict(completed=True,nativeRuns=21,failedRuns=[],independentCoupledTrajectoryQualified=False,
        stationaryInitializationQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    previous=read(package/'baseline/trajectory-analysis.json');rows=[]
    for case,old_case,old_result in zip(p['cases'],old['cases'],previous['cases'],strict=True):
        assert case['key']==old_case['key']==old_result['key']
        names=sum(analysis.labels(case['fixture']),[]);references=[];ref_ledgers=[]
        expected_ticks=np.arange(0,case['totalTicks']+1,p['sampleTicks'],dtype=np.int64)
        for setting in old['referenceSettings']:
            folder=package/'baseline/reference-trajectories'/(case['key']+'-'+setting['key'])
            residuals=analysis.reference_saved(folder,old,old_case,setting)
            with np.load(folder/'samples.npz',allow_pickle=False) as data:
                assert np.array_equal(data['ticks']*2,expected_ticks);values=data['values'].copy()
            analysis.Equation(case['fixture'],p['referenceDecimalPrecision']).validate_samples(values.T)
            assert values.shape==(len(expected_ticks),len(names)) and np.isfinite(values).all()
            sample_residual=analysis.balance(case['fixture'],values)
            ref_ledgers.append({k:max(residuals[k],sample_residual[k]) for k in residuals});references.append(values)
        ref_errors=[analysis.comparison(references[i],references[1],names,p['referenceComparison']) for i in (0,2)]
        assert ref_errors==old_result['referenceErrors'] and ref_ledgers==old_result['referenceLedgers']
        reference_pass=all(r['maximumAllowedFraction']<=1 for r in ref_errors) and all(all(r[k]<=p['ledgerResidualBounds'][k] for k in r) for r in ref_ledgers)
        runs=[]
        for ticks in p['stepTicks']:
            grid,values,meta=analysis.native(package,p,case,ticks);assert np.array_equal(grid,expected_ticks)
            error=analysis.comparison(values,references[1],names,p['candidateComparison']);ledger=analysis.balance(case['fixture'],values)
            for k,field in [('chargeFc','maximumChargeResidualFc'),('ionAttomoles','maximumIonResidualAttomoles')]:
                ledger[k]=max(ledger[k],meta['final'][field],meta['independentAllEndpointLedgerResiduals'][k])
            runs.append(dict(stepTicks=ticks,stepMs=ticks/p['ticksPerMs'],rows=meta['rows'],error=error,ledger=ledger,
                fingerprint=meta['final']['fingerprint'],nonlinearEvaluations=meta['final']['nonlinearEvaluations']))
        # The overlapping physical steps use the same kernel and forcing, with
        # only the integer tick unit changed. Preserve every old metric exactly.
        for new_run,old_run in zip(runs[:2],old_result['candidates'][1:],strict=True):
            assert {k:v for k,v in new_run.items() if k!='stepTicks'}=={k:v for k,v in old_run.items() if k!='stepTicks'}
        ratios=[r['error']['maximumAllowedFraction'] for r in runs]
        refinement=all(b<=p['refinement']['maximumRemainingFraction']*a+p['refinement']['normalizedErrorFloor'] for a,b in zip(ratios,ratios[1:]))
        ledger_pass=all(all(r['ledger'][k]<=p['ledgerResidualBounds'][k] for k in p['ledgerResidualBounds']) for r in runs)
        rows.append(dict(key=case['key'],referenceCriteriaPassed=reference_pass,referenceErrors=ref_errors,referenceLedgers=ref_ledgers,
            candidates=runs,overlappingMetricsExactlyReproduced=True,finestCriteriaPassed=ratios[-1]<=1,
            sampledRefinementPassed=refinement,ledgerCriteriaPassed=ledger_pass,passed=reference_pass and ratios[-1]<=1 and refinement and ledger_pass))
    result=dict(completed=True,numericalTrajectoryCriteriaPassed=all(r['passed'] for r in rows),nativeRuns=21,reusedReferences=21,
        newReferenceSolves=0,cases=rows,originalBoundsChanged=False,stationaryInitializationQualified=False,
        spatialGeometryCalibrated=False,afferentFiringQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    save(package/'trajectory-analysis.json',result)
    for row in rows:print(row['key'],row['passed'],[r['error']['maximumAllowedFraction'] for r in row['candidates']],flush=True)
    if not result['numericalTrajectoryCriteriaPassed']:raise RuntimeError('Finer sensory trajectory criteria failed; retain original bounds and traces.')


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser();parser.add_argument('command',choices=['prepare','preflight','postflight','runtime','check-tests','compare']);parser.add_argument('path',type=Path)
    a=parser.parse_args();dict(prepare=prepare,preflight=preflight,postflight=original.base.postflight,runtime=runtime,
        compare=compare,**{'check-tests':component.check_tests})[a.command](a.path.resolve())
