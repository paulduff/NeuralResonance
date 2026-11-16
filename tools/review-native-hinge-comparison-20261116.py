"""Audit the operator-reported native trajectory comparison; no new integration."""
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import sys

ROOT=Path(__file__).resolve().parents[1]


def main():
    spec=importlib.util.spec_from_file_location('capture_audit',ROOT/'tools/review-olive-and-motor-20261109.py')
    common=importlib.util.module_from_spec(spec);spec.loader.exec_module(common)
    package=ROOT/'artifacts/native-hinge-comparison-20261116-967542';revision='9d6aca66712e9c207cbdc6a3ad4ebadfaf7296ef'
    review=common.verify(package,[],revision)
    archive=common.module('archive_audit',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    review['verifiedGitFiles']=archive.source_archive(common,package,revision)
    provenance=common.read(package/'provenance.json')
    for entry in provenance['copied']:
        assert common.digest(package/entry['path'])==common.digest(entry['original'])==entry['sha256']
    sys.path.insert(0,str(package/'source/tools'))
    audit=common.module('frozen_comparison_audit',package/'source/tools/analyse-native-hinge-comparison.py')
    analysis=audit.audit(package);assert analysis==common.read(package/'hinge-analysis.json')
    assert analysis['completed'] and analysis['capturedTrajectoryAgreementPassed'] and analysis['successfulReferences']==12
    assert common.read(package/'reference-postflight.json')['unchanged']
    assert common.read(package/'reference-postflight.json')['environment']==common.read(package/'reference-preflight.json')['environment']
    controls=common.read(package/'reference-controls/controls.json')
    import numpy as np
    geometry=audit.HingeGeometry(common.read(package/'hinge-input.json')['nativeGeometry'])
    for row in controls['mechanicalMethodChecks']:
        key=row['case']+'-'+row['method'];data=np.load(package/'reference-controls'/(key+'.npz'))
        y,grid,forces=data['state'],data['timeSeconds'],data['forces']
        assert y.shape==(81,28) and forces.shape==(81,2,7) and np.array_equal(grid,np.arange(81)*.000125)
        assert np.isfinite(y).all() and np.isfinite(forces).all()
        assert np.array_equal(data['pathLengths'],geometry.measure(y[:,0])['length'])
        assert float(np.max(abs(data['kineticChange']+data['potentialChange'])))==row['maximumEnergyResidualJoules']
        assert float(np.max(abs(geometry.inertia*(y[:,1]-y[0,1])-y[:,2])))==row['maximumMomentumResidualNewtonMeterSeconds']
        assert float(np.max(abs(data['kineticChange']-y[:,3])))==row['maximumJointWorkResidualJoules']
        assert row['passed']
    for name in provenance['qualifiedSourcePreserved']:
        assert (ROOT/name).read_text(encoding='utf-8-sig')==common.io(package/'source'/name).read_text(encoding='utf-8-sig')
    maxima={group:{key:max(row['errors'][key] for case in analysis['cases'] for row in case[group])
        for key in analysis['cases'][0][group][0]['errors']} for group in ('referenceContrasts','nativeContrasts')}
    review.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,
        savedAnalysisExactlyReproduced=True,qualifiedSourcePreserved=True,geometryControls=49,mechanicalMethodChecks=6,
        controlCrossMethodChecks=3,independentReferences=12,capturedNativeCases=4,capturedNativeSteps=4096,
        maximumErrors=maxima,analysis=analysis,capturedTrajectoryAgreementPassed=True,
        physicalStepRefinementAssessed=False,muscleStepRefinementAssessed=False,fullCoupledTrajectoryQualified=False,
        neuronalModelExecuted=False,physiologyQualified=False,avatarRecoveryAssessed=False,
        testsExecutedDuringReview=False,modelsSolvedDuringReview=False,originalBoundsChanged=False)
    output=ROOT/'artifacts/native-hinge-comparison-review-20261116';output.mkdir()
    (output/'review.json').write_text(json.dumps(review,indent=2,allow_nan=False)+'\n')
    print(json.dumps({k:v for k,v in review.items() if k not in ('analysis',)},indent=2),flush=True)


if __name__=='__main__':main()
