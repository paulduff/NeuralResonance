"""Saved root/matrix accounting only; no root search, eigensolve or model rerun."""
import argparse
from datetime import datetime,timezone
import importlib.util
import json
from pathlib import Path
import sys

ROOT=Path(__file__).resolve().parents[1]
REVISION='e4e3cf2243d108a49a49162b3ef5173605989c25'


def module(name,path):
    s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m


def main(reported):
    assert reported,'Operator completion report required.'
    capture=ROOT/'artifacts/cleft-rest-20261118-760702';out=ROOT/'artifacts/cleft-rest-review-20261118';assert not out.exists()
    files=module('rest_review_files',ROOT/'tools/review-olive-and-motor-20261109.py')
    archive=module('rest_review_git',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    result=files.verify(capture,['test-binaries.json','probe-binaries.json'],REVISION)
    result['verifiedGitFiles']=archive.source_archive(files,capture,REVISION)
    sys.path.insert(0,str(capture/'source/tools'))
    import cleft_rest_gate as gate
    import cleft_rest_analysis as analysis
    import numpy as np
    gate.runtime(capture);tests=gate.component.inventory.check_tests(capture,gate.COUNTS);assert tests['passedCases']==132
    assert files.read(capture/'managed-runtime-before.json')==files.read(capture/'managed-runtime-after.json')
    assert files.read(capture/'reference-preflight.json')['environment']==files.read(capture/'reference-postflight.json')['environment']
    status=files.read(capture/'window-status.json');assert status['PassedFocusedTests']==132
    for k in ('ComponentChecksPassed','TestExecutionAttempted','NativeSearchesAttempted','IndependentSearchesAttempted','NumericalStationaryCriteriaPassed',
        'MatchesQualifiedManagedRuntime','NumericalEnvironmentUnchanged','ManagedRuntimePreserved','FrozenPackageUnchanged'):assert status[k] is True,k
    for k in ('TimeIntegrated','PhysiologicalRestQualified','NonlinearStabilityProven','AfferentFiringQualified','LiveBrainAccessed','OldSavesLoaded',
        'NativeBodyExecuted','PhysiologyQualified','AvatarRecoveryAssessed','FullUnitSuitePassed'):assert status[k] is False,k
    out.mkdir()
    class RedirectOutput:
        def __truediv__(self,name):return (out if name=='rest-analysis.json' else capture)/name
    reproduced=analysis.analyse(RedirectOutput());assert reproduced==files.read(capture/'rest-analysis.json')
    assert reproduced['numericalStationaryCriteriaPassed'] and reproduced['independentlyVerifiedNativeRoots']==reproduced['independentRoots']==18
    protocol=files.read(capture/'rest-input.json');settings=protocol['stability'];matrix_rows=[];root_rows=[]
    for case in protocol['cases']:
        folder=capture/'independent-rest'/case['key'];r=files.read(folder/'result.json');s=r['localStability'];n=s['dimension']
        with np.load(folder/'full-dynamic-jacobians.npz',allow_pickle=False) as f:
            assert set(f.files)=={'coarse','middle','fine','richardsonCoarse','richardsonFine','scale','state'}
            coarse,middle,fine=[f[k].copy() for k in ('coarse','middle','fine')]
            assert all(m.shape==(n,n) and np.isfinite(m).all() for m in (coarse,middle,fine))
            first=(16*middle-coarse)/15;last=(16*fine-middle)/15
            assert np.array_equal(first,f['richardsonCoarse']) and np.array_equal(last,f['richardsonFine'])
            assert np.array_equal(f['state'],r['fullState']) and f['scale'].shape==(n,) and (f['scale']>0).all()
        differences=[float(np.linalg.norm(b-a,2)) for a,b in ((coarse,middle),(middle,fine))]
        uncertainty=float(np.linalg.norm(last-first,2));assert differences==s['successiveJacobianDifferences'] and uncertainty==s['successiveRichardsonDifferenceNorm']
        gate_indices=[i for i in range(n) if i>0 and (i-1)%11>=4];taus=[]
        for m in r['precisionWitnesses'][0]['membranes']:
            taus.extend(float(m['hairRates'][k]['tauMs']) for k in ('potassiumLow','calcium','hcn1'))
            taus.extend(float(m['calyx']['rates'][side][k]['tauMs']) for side,k in [('inner','kv74'),('inner','hcn2'),('outer','kv74'),('outer','hcn2')])
        expected=np.diag([-1/t for t in taus]);error=float(np.max(np.abs(last[np.ix_(gate_indices,gate_indices)]-expected)))
        bound=settings['freeGateBlockAbsoluteTolerance']+settings['freeGateBlockRelativeTolerance']*float(np.max(np.abs(expected)))
        assert error==s['exactFreeGateBlockMaximumError'] and bound==s['exactFreeGateBlockAllowedError'] and error<=bound
        spectrum=s['spectrum'];eigenvalues=np.array([complex(e['real'],e['imaginary']) for e in spectrum['eigenvalues']])
        assert len(eigenvalues)==n and np.isfinite(eigenvalues).all();norm=float(np.linalg.norm(last,2))
        # Reconcile recorded eigenvalue summaries without doing a new eigensolve.
        # Vectors were not retained, so their residual/condition are frozen-study
        # evidence, not independently rechecked vector witnesses in this review.
        trace_error=float(abs(eigenvalues.sum()-np.trace(last)));assert trace_error<=1e-10*max(1.,n*norm)
        right=float(eigenvalues.real.max());assert right==spectrum['maximumRealPartPerMs']
        radius=spectrum['eigenvectorCondition']*(uncertainty+settings['roundoffMultiplier']*np.finfo(float).eps*n*max(1.,norm))
        assert radius==spectrum['sampledUncertaintyRadiusPerMs']
        classification='inconclusive'
        if spectrum['eigenResidualPassed'] and s['sampledJacobianRefinementPassed'] and s['exactFreeGateBlockPassed']:
            if right+radius < -settings['classificationMarginPerMs']:classification='locally-decaying estimate'
            elif right-radius > settings['classificationMarginPerMs']:classification='locally-growing estimate'
        assert classification==spectrum['classification']
        matrix_rows.append(dict(key=case['key'],dimension=n,richardsonMatricesExactlyReproduced=True,freeGateBlockExactlyReproduced=True,
            uncertaintyNormExactlyReproduced=True,recordedEigenvalueTraceError=trace_error,maximumRealPartPerMs=right,
            recordedConditionUncertaintyRadiusPerMs=radius,classification=classification,eigenvectorsRetained=False,newEigensolveExecuted=False))
        if case['seed']=='original':
            state=r['fullState'];bath=case['fixture']['perilymphMv'];patches=(n-1)//11
            root_rows.append(dict(geometry=case['geometryKey'],hairRelativeToBathMv=state[0]-bath,
                calyxRelativeToBathMv=[state[2+11*i]-bath for i in range(patches)],cleftRelativeToBathMv=[state[1+11*i]-bath for i in range(patches)],
                potassiumMm=[state[3+11*i] for i in range(patches)],sodiumMm=[state[4+11*i] for i in range(patches)]))
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,passedTests=132,
        savedAnalysisExactlyReproduced=True,numericalStationaryCriteriaPassed=True,independentlyVerifiedNativeRoots=18,independentRoots=18,
        allMethodsReachedSameBranch=reproduced['allMethodsReachedSameBranch'],allSeedsReachedSameBranch=all(x['withinDeclaredDistance'] for x in reproduced['seedComparisons']),
        matrixReviews=matrix_rows,representativeRoots=root_rows,eigenvectorsIndependentlyRechecked=False,modelsExecutedDuringReview=False,
        testsExecutedDuringReview=False,newEigensolveExecuted=False,branchUniquenessProven=False,nonlinearStabilityProven=False,
        physiologicalRestQualified=False,afferentFiringQualified=False,avatarRecoveryAssessed=False)
    (out/'review.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({k:result[k] for k in ('verifiedFilesPerCopy','passedTests','independentlyVerifiedNativeRoots','independentRoots','allMethodsReachedSameBranch','allSeedsReachedSameBranch')},indent=2),flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    p=argparse.ArgumentParser();p.add_argument('--operator-reported-complete',action='store_true');main(p.parse_args().operator_reported_complete)
