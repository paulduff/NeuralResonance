"""Freeze or run a saved-endpoint cubic crossing diagnostic.

Preparation reads only operator-reported, reviewed captures. Only the visible
window evaluates membrane derivatives and trial polynomial crossings. No ODE,
neuronal source, live transport or body state is advanced by this study.
"""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
import os
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile

for name in ('OMP_NUM_THREADS', 'OPENBLAS_NUM_THREADS', 'MKL_NUM_THREADS', 'NUMEXPR_NUM_THREADS'):
    os.environ[name] = '1'
import numpy as np

ROOT = Path(__file__).resolve().parents[1]


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec); spec.loader.exec_module(value)
    return value


def save(path, value):
    with path.open('x', encoding='utf-8') as stream:
        json.dump(value, stream, indent=2, allow_nan=False); stream.write('\n')


def prepare():
    h = module('crossing_package_helpers', ROOT / 'tools/prepare-causal-motor.py')
    assert not h.git('status', '--porcelain'), 'Commit clean source before freezing.'
    revision = h.git('rev-parse', 'HEAD'); assert revision == h.git('rev-parse', 'origin/main')
    review_folder = ROOT / 'artifacts/receptor-comparison-review-20261116'
    h.verify(review_folder, ['review.json']); review = h.read(review_folder / 'review.json')
    assert review['operatorReportedCompletion'] and review['originalStatus'] == 'FAILED'
    assert review['savedNumericalContrastsReproduced'] and review['savedCandidateAndOutputAuditsReproduced']
    capture = Path(review['capture'])
    assert h.digest(capture / 'backup-manifest.json') == review['manifestSha256']
    original = h.read(capture / 'backup-manifest.json')
    hashes = {e['Path'].replace('\\', '/'): e['Sha256'] for e in original['Files']}
    spec = h.read(capture / 'receptor-membrane-input.json')
    assert h.digest(capture / 'receptor-membrane-input.json') == hashes['receptor-membrane-input.json']
    rows = []; evidence = []; conditions = []
    for scenario in spec['cases']:
        name = scenario['key']; ref_name = 'membrane-reference/' + name + '-dop853-tighter.npz'
        assert h.digest(capture / ref_name) == hashes[ref_name]
        with np.load(capture / ref_name) as ref:
            reference_time = ref['timeMs']; reference_state = ref['state']; reference_events = ref['crossingMs']
        for step in spec['stepsMs']:
            key = name + '-h' + str(step); folder = 'membrane-study/' + key
            for relative in (folder + '/inputs.json', folder + '/final.json', folder + '/trajectory.csv'):
                assert h.digest(capture / relative) == hashes[relative]
                evidence.append(dict(path=relative, sha256=hashes[relative]))
            inputs = h.read(capture / folder / 'inputs.json'); final = h.read(capture / folder / 'final.json')
            by_id = {a['receipt']['contactId']: next(c for c in scenario['contacts'] if c['site'] == a['site'])
                     for a in inputs['admissions']}
            contacts = [by_id[k] for k in final['contactOrder']]
            selected = [r for r in review['crossingDecomposition'] if r['scenario'] == name and r['maximumStepMs'] == step]
            assert len(selected) == len(final['state']['crossings']) == len(reference_events)
            wanted = {r[k] for r in selected for k in ('bracketStartMs', 'bracketEndMs')}
            found = {}
            # Extract authoritative CSV rows verbatim numerically; no new event detection.
            with (capture / folder / 'trajectory.csv').open() as stream:
                header = stream.readline().strip().split(',')
                assert header[32:] == ['receptor_' + k for k in final['contactOrder']]
                for line in stream:
                    if not wanted: break
                    at = float(line.partition(',')[0])
                    if at in wanted:
                        values = [float(v) for v in line.strip().split(',')]
                        assert len(values) == len(header)
                        found[at] = values[2:]; wanted.remove(at)
            assert not wanted
            conditions.append(dict(key=key, scenario=name, maximumStepMs=step, crossings=len(selected)))
            for r in selected:
                t0, t1 = r['bracketStartMs'], r['bracketEndMs']
                i0, i1 = np.searchsorted(reference_time, [t0, t1])
                assert reference_time[i0] == t0 and reference_time[i1] == t1
                assert reference_events[r['ordinal']] == r['referenceCrossingMs']
                rows.append(dict(**r, contacts=contacts, startState=found[t0], endState=found[t1],
                    referenceStartState=reference_state[i0].tolist(), referenceEndState=reference_state[i1].tolist()))
        evidence.append(dict(path=ref_name, sha256=hashes[ref_name]))
    assert len(conditions) == 18 and len(rows) == sum(c['crossings'] for c in conditions) > 0
    environment = module('crossing_environment', ROOT / 'tools/golgi-reference-resume.py').environment()
    assert environment == h.read(capture / 'reference-preflight.json')['environment']
    package = ROOT / 'artifacts' / ('receptor-crossing-' + datetime.now(timezone.utc).strftime('%Y%m%d') + '-' + uuid.uuid4().hex[:6])
    backup = Path('D:/DNNE-desktop-runs/UnityWorld') / package.name
    assert not package.exists() and not backup.exists(); package.mkdir()
    subprocess.run(['git', 'archive', '--format=zip', '--output=' + str(package / 'source.zip'), revision], cwd=ROOT, check=True)
    with ZipFile(package / 'source.zip') as archive:
        for entry in archive.infolist():
            path = PurePosixPath(entry.filename)
            assert not path.is_absolute() and '..' not in path.parts and ':' not in entry.filename and '\\' not in entry.filename
            assert ((entry.external_attr >> 16) & 0o170000) != 0o120000
            target = h.io(package / 'source' / path)
            if entry.is_dir(): target.mkdir(parents=True, exist_ok=True)
            else:
                target.parent.mkdir(parents=True, exist_ok=True)
                with target.open('xb') as stream: stream.write(archive.read(entry))
    for group, names in (('motor-source', ('FivecomptcMG_0.hoc', 'na3rp.mod', 'naps.mod', 'kdrRL.mod', 'mAHP.mod', 'gh.mod', 'L_Ca_inact.mod')),
                         ('receptor-source', ('ampa.mod', 'gabaa.mod', 'README', 'source-manifest.json'))):
        target = package / 'reference' / group; target.mkdir(parents=True)
        for name in names:
            relative = 'reference/' + group + '/' + name
            assert h.digest(capture / relative) == hashes[relative]
            shutil.copyfile(capture / relative, target / name)
            evidence.append(dict(path=relative, sha256=hashes[relative]))
    # Preserve exact reference implementation and source provenance, not a new equation set.
    for name in ('receptor_membrane_reference.py', 'receptor_membrane_protocol.py', 'motoneuron-active-reference.py',
                 'motoneuron-reference-check.py', 'motoneuron_bounded_jacobian.py', 'motoneuron_active_protocol.py', 'golgi-reference-resume.py'):
        relative = 'source/tools/' + name
        assert (package / relative).read_bytes() == (capture / relative).read_bytes()
    shutil.copyfile(review_folder / 'review.json', package / 'reference/review.json')
    shutil.copyfile(capture / 'provenance.json', package / 'reference/original-provenance.json')
    save(package / 'crossing-input.json', dict(version='receptor-crossing-postprocess-v1', rows=rows, conditions=conditions,
        criteria=spec['criteria'], polynomialResidualMv=1e-8, expectedAnalyticControls=5,
        kineticsIntegrated=False, productionImplementationChanged=False))
    save(package / 'preparation-environment.json', environment)
    save(package / 'provenance.json', dict(capture=str(capture), manifestSha256=review['manifestSha256'],
        extractedFiles=evidence, endpointPairs=len(rows), originalStatus='FAILED', modelsExecutedDuringPreparation=False))
    shutil.copyfile(package / 'source/tools/backup-unity-run.ps1', package / 'backup-run.ps1')
    files = [dict(Path=p.relative_to(h.io(package)).as_posix(), Sha256=h.digest(p)) for p in sorted(h.io(package).rglob('*')) if p.is_file()]
    config = dict(Revision=revision, PreparedUtc=datetime.now(timezone.utc).isoformat(), PackageFiles=files,
        PythonExecutable=sys.executable, BackupDestination=str(backup), NoLiveMonitoring=True,
        IsolatedReceptorCrossing=True, ExpectedConditions=18, ExpectedEndpointPairs=len(rows), ExpectedAnalyticControls=5)
    save(package / 'configuration.json', config)
    pointer = dict(Package=str(package), Backup=str(backup), Revision=revision,
        Launcher=str(package / 'source/tools/start-receptor-crossing-window.ps1'), NoLiveMonitoring=True)
    save(package.parent / (package.name + '-prepared.json'), pointer)
    print(json.dumps(pointer, indent=2), flush=True)


def hermite(t0, t1, v0, v1, d0, d1, threshold=-20.):
    h = t1-t0
    assert h > 0 and v0 < threshold <= v1 and np.isfinite([t0,t1,v0,v1,d0,d1]).all()
    a = 2*v0-2*v1+h*(d0+d1); b = -3*v0+3*v1-h*(2*d0+d1); c=h*d0; d=v0-threshold
    stationary = [0.,1.]
    if a and 0 < -b/(3*a) < 1: stationary.append(-b/(3*a))
    minimum = min(3*a*s*s+2*b*s+c for s in stationary)
    if minimum <= 0: raise ValueError('Cubic is not strictly increasing; do not select an ambiguous event.')
    lo, hi = 0.,1.
    for _ in range(64):
        middle=(lo+hi)*.5
        if middle == lo or middle == hi: break
        if ((a*middle+b)*middle+c)*middle+d < 0: lo=middle
        else: hi=middle
    fraction=(lo+hi)*.5; residual=((a*fraction+b)*fraction+c)*fraction+d
    return dict(timeMs=t0+h*fraction, residualMv=residual, minimumScaledSlope=minimum,
                coefficients=[a,b,c,d], fraction=fraction)


def run(package):
    assert Path(__file__).resolve() == (package / 'source/tools/receptor_crossing_study.py').resolve()
    h = module('crossing_run_helpers', Path(__file__).with_name('prepare-causal-motor.py'))
    config=h.read(package/'configuration.json'); spec=h.read(package/'crossing-input.json')
    env=module('crossing_run_environment',Path(__file__).with_name('golgi-reference-resume.py')).environment
    assert env()==h.read(package/'preparation-environment.json')
    for e in config['PackageFiles']: assert h.digest(package/e['Path'])==e['Sha256']
    report=dict(completed=False,diagnosticCriteriaPassed=False,controls=[],rows=[],conditions=[],refinement=[],
        derivativeEvaluations=True,kineticsIntegrated=False,productionImplementationChanged=False,
        presynapticNeuronalAuthorityQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    def record():
        (package/'crossing-analysis.json').write_text(json.dumps(report,indent=2,allow_nan=False)+'\n',encoding='utf-8')
    try:
        for root in (.2,.5,.8):
            v=lambda x: -20+(x-root)+.2*(x-root)**3
            dv=lambda x: 1+.6*(x-root)**2
            result=hermite(0.,1.,v(0.),v(1.),dv(0.),dv(1.))
            assert abs(result['timeMs']-root)<=1e-12 and abs(result['residualMv'])<=spec['polynomialResidualMv']
            report['controls'].append(dict(label='known-cubic-'+str(root),passed=True,**result))
        result=hermite(4.,6.,-21.,-17.,2.,2.)
        assert result['timeMs']==4.5
        report['controls'].append(dict(label='linear-limit',passed=True,**result))
        rejected=False
        try: hermite(0.,1.,-20.08,-19.92,.66,.66)
        except ValueError: rejected=True
        assert rejected
        report['controls'].append(dict(label='three-crossings-rejected',passed=True)); record()
        reference=module('crossing_reference_equations',Path(__file__).with_name('receptor_membrane_reference.py'))
        rates=reference.receptor_parameters(package/'reference/receptor-source')
        systems={}
        for row in spec['rows']:
            key=row['scenario']
            if key not in systems: systems[key]=reference.system(package/'reference/motor-source',row['contacts'],rates)[0]
            t0,t1=row['bracketStartMs'],row['bracketEndMs']
            interval=next((a,b,tx) for a,b,tx in reference.transitions(row['contacts'],1500.,rates) if a<=t0 and t1<=b)
            rhs,_=systems[key](interval[2])
            estimates={}
            for label,start,end in (('candidate','startState','endState'),('reference','referenceStartState','referenceEndState')):
                y0=np.asarray(row[start]);y1=np.asarray(row[end])
                estimates[label]=hermite(t0,t1,float(y0[0]),float(y1[0]),float(rhs(t0,y0)[0]),float(rhs(t1,y1)[0]))
                assert abs(estimates[label]['residualMv'])<=spec['polynomialResidualMv']
            error=estimates['candidate']['timeMs']-row['referenceCrossingMs']
            reference_error=estimates['reference']['timeMs']-row['referenceCrossingMs']
            report['rows'].append(dict(scenario=key,maximumStepMs=row['maximumStepMs'],ordinal=row['ordinal'],
                legacyErrorMs=row['totalErrorMs'],cubicErrorMs=error,referenceCubicErrorMs=reference_error,
                estimates=estimates,passed=abs(error)<=spec['criteria']['candidateCrossingMs'] and
                abs(reference_error)<=spec['criteria']['referenceCrossingMs'])); record()
        for condition in spec['conditions']:
            rows=[r for r in report['rows'] if r['scenario']==condition['scenario'] and r['maximumStepMs']==condition['maximumStepMs']]
            assert len(rows)==condition['crossings']
            report['conditions'].append(dict(**condition,maximumCubicErrorMs=max((abs(r['cubicErrorMs']) for r in rows),default=0.),
                maximumLegacyErrorMs=max((abs(r['legacyErrorMs']) for r in rows),default=0.),passed=all(r['passed'] for r in rows)))
        for name in dict.fromkeys(c['scenario'] for c in spec['conditions']):
            conditions=[c for c in report['conditions'] if c['scenario']==name]
            assert len(conditions)==3
            for coarse,fine in zip(conditions,conditions[1:]):
                bound=max(spec['criteria']['refinementFloor'],spec['criteria']['refinementRatio']*coarse['maximumCubicErrorMs'])
                report['refinement'].append(dict(scenario=name,coarseStepMs=coarse['maximumStepMs'],fineStepMs=fine['maximumStepMs'],
                    allowedMs=bound,fineErrorMs=fine['maximumCubicErrorMs'],passed=fine['maximumCubicErrorMs']<=bound))
        report['completed']=len(report['controls'])==5 and len(report['rows'])==config['ExpectedEndpointPairs'] and len(report['conditions'])==18 and len(report['refinement'])==12
        report['diagnosticCriteriaPassed']=report['completed'] and all(r['passed'] for key in ('controls','rows','conditions','refinement') for r in report[key])
        record()
        if not report['diagnosticCriteriaPassed']: raise RuntimeError('Crossing diagnostic criteria failed; retain original bounds.')
        print('Saved-endpoint cubic diagnostic complete; no neuronal or body integration.',flush=True)
    except Exception as error:
        report['error']=repr(error);record();raise
    finally:
        try:
            assert env()==h.read(package/'preparation-environment.json')
            for e in config['PackageFiles']: assert h.digest(package/e['Path'])==e['Sha256']
            report['frozenInputsAndEnvironmentPreserved']=True
        except Exception as error:
            report['frozenInputsAndEnvironmentPreserved']=False
            report['diagnosticCriteriaPassed']=False; report['integrityError']=repr(error)
            record(); raise
        record()


if __name__=='__main__':
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser();parser.add_argument('stage',choices=('prepare','run'));parser.add_argument('--package',type=Path)
    args=parser.parse_args()
    if args.stage=='prepare': prepare()
    else: run(args.package.resolve())
