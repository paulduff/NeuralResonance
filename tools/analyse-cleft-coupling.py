"""Independent high-precision constitutive reference; run only in visible PowerShell.

Uses Decimal exponential/logarithm and a full graph capacitance matrix with
pivoted elimination. It imports no candidate equations and performs no ODE solve.
"""
import argparse
from decimal import Decimal, localcontext
import importlib.util
import json
from pathlib import Path
from cleft_coupling_protocol import validate, COUNTS

ROOT = Path(__file__).resolve().parents[1]
read = lambda p: json.loads(p.read_text(encoding='utf-8-sig'))


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m


def save(path,value):
    with path.open('x',encoding='utf-8') as stream: json.dump(value,stream,indent=2,allow_nan=False);stream.write('\n')


def dec(value):
    return Decimal.from_float(value) if isinstance(value,float) else Decimal(value)


def edge(row):
    p={k:dec(v) for k,v in row['material'].items()};g={k:dec(v) for k,v in row['geometry'].items()}
    left={k:dec(v) for k,v in row['left'].items()};right={k:dec(v) for k,v in row['right'].items()}
    drop=left['potentialMv']-right['potentialMv'];theta=p['thermalVoltageMv'];psi=-drop/theta
    area_over_distance=g['faceAreaSquareMicrometers']/g['centerDistanceMicrometers']
    def bernoulli(x): return Decimal(1) if x==0 else x/(x.exp()-1)
    def flow(ion,diffusion): return p[diffusion]*area_over_distance*(bernoulli(psi)*left[ion]-bernoulli(-psi)*right[ion])
    k=flow('potassiumMm','potassiumDiffusionSquareMicrometersPerMs')
    na=flow('sodiumMm','sodiumDiffusionSquareMicrometersPerMs')
    # Match the declared binary64 material constant exactly, not a rounded
    # decimal replacement introduced only in the independent reference.
    faraday=dec(96.485);ik=faraday*k;ina=faraday*na
    other=p['otherIonConductivityNsPerMicrometer']*area_over_distance*drop
    dk=theta*(left['potassiumMm']/right['potassiumMm']).ln()+drop
    dn=theta*(left['sodiumMm']/right['sodiumMm']).ln()+drop
    return dict(potassiumAttomolesPerMs=k,sodiumAttomolesPerMs=na,otherIonCurrentPa=other,
        concentrationRates=dict(leftPotassiumMmPerMs=-k/g['leftVolumeCubicMicrometers'],rightPotassiumMmPerMs=k/g['rightVolumeCubicMicrometers'],
            leftSodiumMmPerMs=-na/g['leftVolumeCubicMicrometers'],rightSodiumMmPerMs=na/g['rightVolumeCubicMicrometers']),
        potassiumElectrochemicalDropMv=dk,sodiumElectrochemicalDropMv=dn,potentialDropMv=drop,
        potassiumCurrentPa=ik,sodiumCurrentPa=ina,totalCurrentPa=ik+ina+other,dissipationFw=ik*dk+ina*dn+other*drop)


def dense_solve(matrix,rhs):
    rows=[list(row)+[value] for row,value in zip(matrix,rhs,strict=True)];n=len(rows)
    for col in range(n):
        pivot=max(range(col,n),key=lambda i:abs(rows[i][col]))
        assert rows[pivot][col]!=0,'Singular independent capacitance system.'
        rows[col],rows[pivot]=rows[pivot],rows[col]
        value=rows[col][col];rows[col]=[x/value for x in rows[col]]
        for i in range(n):
            if i!=col:
                factor=rows[i][col];rows[i]=[a-factor*b for a,b in zip(rows[i],rows[col],strict=True)]
    return [row[-1] for row in rows]


def charge(row):
    caps={k:dec(v) for k,v in row['capacitances'].items()};ports={k:dec(v) for k,v in row['ports'].items()}
    v=[dec(row['potentials'][k]) for k in ('hairMv','cleftMv','calyxMv','endolymphMv','perilymphMv')]
    # Full five-node graph; the candidate instead solves a three-node Schur
    # reduction. This construction retains both faces of every capacitor.
    matrix=[[Decimal(0) for _ in range(5)] for _ in range(5)]
    for a,b,name in [(0,3,'hairApicalPf'),(0,4,'hairPerilymphPf'),(0,1,'hairCleftPf'),(2,1,'calyxInnerPf'),(2,4,'calyxOuterPf')]:
        c=caps[name];matrix[a][a]+=c;matrix[b][b]+=c;matrix[a][b]-=c;matrix[b][a]-=c
    rhs=[-sum(ports[k] for k in ('hairToEndolymphPa','hairToPerilymphPa','hairToCleftPa')),
         ports['hairToCleftPa']+ports['calyxToCleftPa']-ports['cleftOutgoingPa'],
         -sum(ports[k] for k in ('calyxToCleftPa','calyxToPerilymphPa','calyxToAxonPa'))]
    rates=dense_solve([r[:3] for r in matrix[:3]],rhs)+[Decimal(0),Decimal(0)]
    multiply=lambda x:[sum(a*b for a,b in zip(row,x,strict=True)) for row in matrix]
    q=multiply(v);dq=multiply(rates)
    names=('hair','cleft','calyx','endolymph','perilymph')
    return dict(rates={n+'MvPerMs':x for n,x in zip(names[:3],rates[:3],strict=True)},
        chargeRates={n+'Pa':x for n,x in zip(names,dq,strict=True)},charges={n+'Fc':x for n,x in zip(names,q,strict=True)},
        storedEnergyAttojoules=sum(a*b for a,b in zip(v,q,strict=True))/2,
        storedEnergyRateFw=sum(a*b for a,b in zip(q,rates,strict=True)))


def decimal_strings(value):
    return {k:decimal_strings(v) for k,v in value.items()} if isinstance(value,dict) else str(value)


def compare_values(actual,expected,absolute,relative,location=''):
    if isinstance(expected,dict):
        assert actual.keys()==expected.keys(),location
        return max(compare_values(actual[k],v,absolute,relative,location+'/'+k) for k,v in expected.items())
    a=dec(actual);b=dec(expected)
    assert a.is_finite() and b.is_finite(),location
    error=abs(a-b);allowed=absolute+relative*abs(b)
    assert error<=allowed,(location,str(a),str(b),str(error),str(allowed))
    return error/allowed


def environment(): return module('cleft_dependencies',ROOT/'tools/golgi-reference-resume.py').environment()


def preflight(package):
    validate(read(package/'cleft-input.json'));current=environment()
    assert current==read(package/'preparation-environment.json')
    save(package/'reference-preflight.json',dict(environment=current,modelsExecuted=False,testsExecuted=False))


def postflight(package):
    current=environment();unchanged=current==read(package/'reference-preflight.json')['environment']
    save(package/'reference-postflight.json',dict(environment=current,unchanged=unchanged))
    assert unchanged,'Numerical dependencies changed during the study.'


def compare(package):
    spec=validate(read(package/'cleft-input.json'));assert environment()==read(package/'reference-preflight.json')['environment']
    tests=module('cleft_exact_tests',ROOT/'tools/receptor_membrane_gate.py').check_tests(package,COUNTS)
    captured=read(package/'study/input.json')
    assert captured=={k:spec[k] for k in ('version','edgeVersion','chargeVersion','edgeCases','chargeCases','expectedEdges','expectedCharges')}
    result=read(package/'study/results.json')
    assert result==dict(completed=True,edgeRows=468,chargeRows=60,timeIntegrated=False,
        spatialGeometryCalibrated=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    fingerprints={};maximum=Decimal(0);counts={}
    for label,key,fn in [('edges','edgeCases',edge),('charges','chargeCases',charge)]:
        count=0
        with (package/'study'/(label+'.jsonl')).open() as source,(package/('independent-'+label+'.jsonl')).open('x') as target:
            for expected,line in zip(spec[key],source,strict=True):
                row=json.loads(line);assert set(row)=={'input','fingerprint','evaluation'} and row['input']==expected
                identity=json.dumps([expected[k] for k in (('material','geometry') if label=='edges' else ('capacitances',))],sort_keys=True)
                assert len(row['fingerprint'])==64
                assert fingerprints.setdefault((label,identity),row['fingerprint'])==row['fingerprint']
                references=[]
                for precision in spec['decimalPrecisions']:
                    with localcontext() as context:
                        context.prec=precision;references.append(fn(expected))
                with localcontext() as context:
                    context.prec=90
                    compare_values(references[0],references[1],dec(spec['referenceAbsoluteTolerance']),dec(spec['referenceRelativeTolerance']),label+'/reference/'+str(count))
                    ratio=compare_values(row['evaluation'],references[1],dec(spec['candidateAbsoluteTolerance']),dec(spec['candidateRelativeTolerance']),label+'/candidate/'+str(count))
                    maximum=max(maximum,ratio)
                target.write(json.dumps(dict(index=count,input=expected,precision=spec['decimalPrecisions'],
                    expected=[decimal_strings(r) for r in references],maximumAllowedFraction=str(ratio)),allow_nan=False)+'\n')
                count+=1
        counts[label]=count
    assert counts==dict(edges=468,charges=60)
    assert len(set(fingerprints.values()))==len(fingerprints),'Different material/geometry identities collapsed.'
    save(package/'analysis.json',dict(completed=True,numericalComponentCriteriaPassed=True,tests=tests,counts=counts,
        maximumAllowedFraction=str(maximum),decimalPrecisions=spec['decimalPrecisions'],originalBoundsChanged=False,
        timeIntegrated=False,spatialGeometryCalibrated=False,physiologyQualified=False,avatarRecoveryAssessed=False))
    print('468 edge and 60 capacitance cases reconciled against 60/90-digit references; no time integration.',flush=True)


if __name__=='__main__':
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('command',choices=['preflight','compare','postflight']);parser.add_argument('package',type=Path)
    args=parser.parse_args();dict(preflight=preflight,compare=compare,postflight=postflight)[args.command](args.package.resolve())
