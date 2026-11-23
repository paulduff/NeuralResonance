"""Independent Decimal equilibrium check of completed native initializers.

No candidate derivatives or candidate iterations are reused. The reference
bisects the original continuous passive/tendon equations at 80/110 digits.
Executed only in the independent visible PowerShell study.
"""
from decimal import Decimal as D, localcontext
import decimal
import _decimal
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET

LENGTH_BOUND = 1e-11
REFERENCE_AGREEMENT_METERS = D('1e-60')


def runtime():
    paths = sorted({Path(decimal.__file__), Path(_decimal.__file__)})
    return dict(decimalVersion=decimal.__version__, mpdecimalVersion=decimal.__libmpdec_version__,
                files=[dict(path=str(p), sha256=hashlib.sha256(p.read_bytes()).hexdigest().upper()) for p in paths])


def number(x):
    return D.from_float(x) if isinstance(x, float) else D(x)


def cosine(x):
    term = total = D(1)
    for k in range(1, 512):
        term *= -x*x/D((2*k-1)*(2*k)); next_total = total+term
        if next_total == total: return total
        total = next_total
    raise RuntimeError('Independent cosine series did not converge.')


def reference(row, precision):
    with localcontext() as context:
        context.prec = precision
        g = {k:number(v) for k,v in row['geometry'].items()}; r = row['result']
        length = number(r['measuredPathLengthMeters']); c = cosine(g['pennationRadians'])
        # Constitutive constants are the exact stored binary64 parameters,
        # independently evaluated at high precision, without native roundoff.
        pscale = number(23)*number(.046); tscale = number(27.8)*number(.0047)
        def imbalance(fiber):
            normalized = fiber/g['optimalFiberLengthMeters']
            tendon = (length-fiber*c)/g['referenceTendonLengthMeters']
            passive = g['referenceForceNewtons']*pscale*(1+((normalized/g['passiveLengthScale']-number(1.17))/number(.046)).exp()).ln()
            axial = g['referenceForceNewtons']*tscale*(1+((tendon-number(.964))/number(.0047)).exp()).ln()
            return axial*c-passive*c*c
        lo = number(r['requestedLowerFiberMeters']); hi = number(r['requestedUpperFiberMeters'])
        assert imbalance(lo) >= 0 and imbalance(hi) <= 0
        for _ in range(256):
            mid = (lo+hi)/2
            if imbalance(mid) > 0: lo = mid
            else: hi = mid
        return (lo+hi)/2, imbalance(number(r['candidate']['fiberLengthMeters']))


def compare(package):
    ns = {'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    tree = ET.parse(package/'test-results/focused.trx'); rows = []
    for output in tree.findall('.//t:UnitTestResult/t:Output/t:StdOut', ns):
        for line in (output.text or '').splitlines():
            line = line.strip()
            if line.startswith('INITIAL_BALANCE_JSON '): rows.append(json.loads(line[len('INITIAL_BALANCE_JSON '):]))
    expected = {'length-'+s for s in ('0.5','0.75','1','1.25','1.4')} | {'scale-'+s for s in ('0.5','1','2')}
    assert len(rows) == len({r['key'] for r in rows}) == 8 and {r['key'] for r in rows} == expected, 'Missing/duplicated native equilibrium records'
    reports = []
    for row in rows:
        a, residual_a = reference(row, 80); b, residual_b = reference(row, 110)
        r = row['result']; candidate = number(r['candidate']['fiberLengthMeters'])
        agreement = abs(a-b); error = abs(candidate-b)
        passed = agreement <= REFERENCE_AGREEMENT_METERS and error <= number(LENGTH_BOUND) and abs(residual_b) <= number(r['forceAllowanceNewtons'])
        reports.append(dict(key=row['key'], reference80Meters=str(a), reference110Meters=str(b),
            referenceAgreementMeters=str(agreement), candidateLengthErrorMeters=str(error),
            independentForceResidual80Newtons=str(residual_a), independentForceResidual110Newtons=str(residual_b),
            lengthAllowanceMeters=LENGTH_BOUND, forceAllowanceNewtons=r['forceAllowanceNewtons'], passed=passed))
    evidence = dict(completed=True, passed=all(r['passed'] for r in reports), cases=8,
        referencePrecisionDigits=[80,110], referenceAgreementAllowanceMeters=str(REFERENCE_AGREEMENT_METERS),
        candidateLengthAllowanceMeters=LENGTH_BOUND, originalForceAllowancesRetained=True, rows=reports,
        trajectoryAccuracyQualified=False, anatomicalCalibrationQualified=False, neuronalMotorAuthority=False, avatarStandingQualified=False)
    with (package/'initial-balance-analysis.json').open('x', encoding='utf-8') as stream:
        json.dump(evidence, stream, indent=2, allow_nan=False); stream.write('\n')
    assert evidence['passed'], 'Initial equilibrium criteria failed; retain records and original limits.'
    print('Eight native initial equilibria reconciled against 80/110-digit independent passive/tendon solutions. Anatomy and standing remain unqualified.', flush=True)
    return evidence
