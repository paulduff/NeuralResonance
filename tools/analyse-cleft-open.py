"""Independent open-cleft comparison; execute only in visible PowerShell."""
import argparse
from decimal import Decimal,localcontext
import importlib.util
import json
from pathlib import Path
from cleft_open_protocol import validate,COUNTS
from cleft_open_reference import evaluate

ROOT=Path(__file__).resolve().parents[1]


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m


base=module('open_cleft_original_comparison',ROOT/'tools/analyse-cleft-coupling.py')
read=base.read;save=base.save
FILES=('input.json','results.json','edges.jsonl','charges.jsonl','spatial.jsonl')


def prerequisites(package):
    spec=validate(read(package/'open-input.json'));closed=base.validate(read(package/'cleft-input.json'))
    cfg=read(package/'configuration.json');review=read(package/'qualified-cleft-review.json');provenance=read(package/'provenance.json')
    assert cfg['IsolatedCleftOpen'] and cfg['NoLiveMonitoring'] and cfg['ExpectedTestCases']==51
    assert cfg['ExpectedOpenCases']==192 and cfg['ExpectedClosedCases']==624 and cfg['ExpectedDecimalPrecisions']==[60,90]
    assert review['operatorReportedCompletion'] and review['savedAnalysisExactlyReproduced'] and review['independentSavedComparisonsRechecked']
    assert review['numericalCriteriaPassed'] and review['passedTests']==40 and not review['analysis']['originalBoundsChanged']
    assert review['capture']==provenance['qualifiedCapture'] and review['manifestSha256']==provenance['qualifiedCaptureManifestSha256']
    assert review['sourceRevision']==provenance['qualifiedSourceRevision']=='c53783a17e9ab26ab5968bca5d267d0c1a277fa5'
    assert provenance['runtimeMatchesQualifiedCleft'] and not provenance['modelsExecutedDuringPreparation'] and not provenance['testsExecutedDuringPreparation']
    helper=module('open_cleft_file_hash',ROOT/'tools/prepare-causal-motor.py')
    assert {r['path'] for r in provenance['closedFiles']}=={'reference/closed/'+name for name in FILES}
    for row in provenance['closedFiles']:assert helper.digest(package/row['path'])==row['sha256']
    assert closed['edgeCases'] and len(closed['edgeCases'])+len(closed['chargeCases'])+len(closed['spatialCases'])==624
    assert base.environment()==read(package/'preparation-environment.json')
    return spec


def preflight(package):
    prerequisites(package)
    save(package/'reference-preflight.json',dict(environment=base.environment(),modelsExecuted=False,testsExecuted=False))


def compare(package):
    spec=prerequisites(package)
    assert base.environment()==read(package/'reference-preflight.json')['environment']
    tests=module('open_cleft_inventory',ROOT/'tools/receptor_membrane_gate.py').check_tests(package,COUNTS)
    assert read(package/'study/input.json')=={k:spec[k] for k in ('version','openVersion','spatialVersion','cases','expectedCases','expectedClosedCases')}
    assert read(package/'study/results.json')==dict(completed=True,closedRows=624,openRows=192,timeIntegrated=False,
        spatialGeometryCalibrated=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    # Exact serialized records include input identity, fingerprint and every
    # floating output. Do not silently substitute tolerance for regression.
    for name in FILES:
        assert (package/'study/closed'/name).read_bytes()==(package/'reference/closed'/name).read_bytes(),('Closed regression differs',name)
    spatial_fingerprints={row['input']['index']:row['fingerprint'] for row in
        (json.loads(line) for line in (package/'reference/closed/spatial.jsonl').read_text().splitlines())}
    assert len(spatial_fingerprints)==96
    fingerprints={};maximum=Decimal(0);count=0
    with (package/'study/open.jsonl').open() as native,(package/'independent-open.jsonl').open('x') as output:
        for expected,line in zip(spec['cases'],native,strict=True):
            row=json.loads(line);assert set(row)=={'input','fingerprint','spatialFingerprint','evaluation'} and row['input']==expected
            assert row['spatialFingerprint']==spatial_fingerprints[expected['spatial']['index']]
            identity=json.dumps([row['spatialFingerprint'],expected['reservoir'],expected['faces']],sort_keys=True)
            assert len(row['fingerprint'])==64 and fingerprints.setdefault(identity,row['fingerprint'])==row['fingerprint']
            references=[]
            for precision in spec['decimalPrecisions']:
                with localcontext() as context:
                    context.prec=precision;references.append(evaluate(expected,base.edge,base.dense_solve))
            with localcontext() as context:
                context.prec=90
                base.compare_values(references[0],references[1],Decimal(spec['referenceAbsoluteTolerance']),Decimal(spec['referenceRelativeTolerance']),'open/reference/'+str(count))
                ratio=base.compare_values(row['evaluation'],references[1],Decimal(spec['candidateAbsoluteTolerance']),Decimal(spec['candidateRelativeTolerance']),'open/candidate/'+str(count))
                maximum=max(maximum,ratio)
            output.write(json.dumps(dict(index=count,input=expected,precision=spec['decimalPrecisions'],
                expected=[base.decimal_strings(r) for r in references],maximumAllowedFraction=str(ratio)),allow_nan=False)+'\n')
            count+=1
    assert count==192 and len(set(fingerprints.values()))==len(fingerprints)
    save(package/'analysis.json',dict(completed=True,numericalComponentCriteriaPassed=True,tests=tests,closedRows=624,openRows=count,
        closedRecordsBitwisePreserved=True,maximumAllowedFraction=str(maximum),decimalPrecisions=spec['decimalPrecisions'],
        originalBoundsChanged=False,timeIntegrated=False,spatialGeometryCalibrated=False,physiologyQualified=False,avatarRecoveryAssessed=False))
    print('624 closed records preserved byte for byte; 192 open graphs reconciled against 60/90-digit references.',flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('command',choices=['preflight','compare','postflight','check-tests']);parser.add_argument('package',type=Path)
    args=parser.parse_args();package=args.package.resolve()
    if args.command=='check-tests':print(module('open_cleft_inventory',ROOT/'tools/receptor_membrane_gate.py').check_tests(package,COUNTS))
    elif args.command=='postflight':base.postflight(package)
    else:dict(preflight=preflight,compare=compare)[args.command](package)
