"""Freeze input-evaluator controls using completed mechanical evidence only."""
import argparse
from copy import deepcopy
from datetime import datetime, timezone
from fractions import Fraction
import json
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile

from afferent_precision_gate import h, read, save
from cleft_trajectory_reference import base

ROOT = Path(__file__).resolve().parents[1]
PRESERVED = ['tools/coupled_canal_reference.py', 'tools/canal_bundle_protocol.py',
             'docs/reference-data/vestibular-ifediba-2007.json']


def protocol(bundle):
    queries = sorted({Fraction(0), Fraction(1), *(Fraction(i, 65536) for i in range(1, 17)),
                      *(Fraction(i, 32) for i in range(1, 32)), Fraction(1, 3), Fraction(2, 3), Fraction(1, 10), Fraction(9, 10)})
    assert len(queries) == 53
    return dict(version='independent-canal-waveform-check-v1', conditions=['recorded-head', 'stationary-head'],
        physicalIntervals=10, expectedQueries=1060, analyticControls=15, exponentialControls=6,
        queries=[dict(numerator=q.numerator, denominator=q.denominator) for q in queries],
        referenceBounds=deepcopy(bundle['referenceBounds']), matrixPrecisionAbsolute=bundle['matrixPrecisionAbsolute'],
        matrixPrecisionRelative=bundle['matrixPrecisionRelative'], geometryArithmeticRelative=bundle['geometryArithmeticRelative'],
        queryPolicy='53 explicit queries per interval: endpoints, 16 earliest fine source steps, 31 uniform interior anchors and four non-dyadic fractions',
        pendingCaptureRequired=False, neuronsExecuted=False, physiologyQualified=False)


def prepare():
    assert not h.git('status', '--porcelain'), 'Commit first.'
    revision = h.git('rev-parse', 'HEAD')
    assert revision == h.git('rev-parse', 'origin/main'), 'Push first.'
    assert shutil.disk_usage('D:/').free >= 4 * 1024**3
    reviews = {key: ROOT / 'artifacts' / folder for key, folder in (
        ('bundle', 'canal-bundle-20261119-bd15d1-review'), ('graded', 'canal-graded-20261119-7880d2-review'))}
    captures, evidence = {}, {}
    for key, folder in reviews.items():
        h.verify(folder, ['review.json', 'analysis.json'])
        evidence[key] = read(folder / 'review.json')
        assert evidence[key]['operatorReportedCompletion'] and evidence[key]['numericalCriteriaPassed']
        captures[key] = Path(evidence[key]['capture'])
        assert h.digest(captures[key] / 'backup-manifest.json') == evidence[key]['manifestSha256']
    bundle = captures['bundle']
    graded = captures['graded']
    assert evidence['bundle']['passedTests'] == 204 and evidence['graded']['referenceCriteriaPassed']
    h.verify(bundle, ['bundle-input.json', 'analysis.json', *('source/' + n for n in PRESERVED)])
    angular_names = [f'baseline/baseline/reference-{p}.json' for p in (60, 90)]
    h.verify(graded, angular_names + ['preparation-environment.json'])
    assert base.environment() == read(graded / 'preparation-environment.json')
    for name in PRESERVED:
        assert (ROOT / name).read_text(encoding='utf-8-sig') == h.io(bundle / 'source' / name).read_text(encoding='utf-8-sig'), name
    package = Path('D:/DNNE-build-work') / ('canal-waveform-' + datetime.now(timezone.utc).strftime('%Y%m%d') + '-' + uuid.uuid4().hex[:6])
    backup = Path('D:/DNNE-desktop-runs/UnityWorld') / package.name
    assert not package.exists() and not backup.exists()
    package.mkdir()
    subprocess.run(['git', 'archive', '--format=zip', '--output=' + str(package / 'source.zip'), revision], cwd=ROOT, check=True)
    with ZipFile(package / 'source.zip') as archive:
        for entry in archive.infolist():
            name = PurePosixPath(entry.filename)
            assert not name.is_absolute() and '..' not in name.parts and ':' not in entry.filename and '\\' not in entry.filename
            assert ((entry.external_attr >> 16) & 0o170000) != 0o120000
            target = h.io(package / 'source' / name)
            if entry.is_dir():
                target.mkdir(parents=True, exist_ok=True)
            else:
                target.parent.mkdir(parents=True, exist_ok=True)
                with target.open('xb') as stream:
                    stream.write(archive.read(entry))
    provenance = []

    def copy(source, name):
        target = package / name
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(h.io(source), h.io(target))
        assert h.digest(source) == h.digest(target)
        provenance.append(dict(path=name, source=str(source), sha256=h.digest(source)))

    for key, folder in reviews.items():
        copy(folder / 'review.json', 'reviewed-' + key + '.json')
        copy(folder / 'analysis.json', 'reviewed-' + key + '-analysis.json')
    copy(bundle / 'bundle-input.json', 'baseline/bundle-input.json')
    for precision, name in zip((60, 90), angular_names, strict=True):
        copy(graded / name, f'baseline/angular-{precision}.json')
    copy(package / 'source/tools/backup-unity-run.ps1', 'backup-run.ps1')
    save(package / 'waveform-input.json', protocol(read(bundle / 'bundle-input.json')))
    save(package / 'preparation-environment.json', base.environment())
    save(package / 'provenance.json', dict(files=provenance, preservedSources=[dict(path=n, sha256=h.digest(bundle / 'source' / n)) for n in PRESERVED],
        reviewedManifests={key: value['manifestSha256'] for key, value in evidence.items()},
        modelsExecutedDuringPreparation=False, testsExecutedDuringPreparation=False, pendingCaptureAccessed=False))
    files = [dict(Path=p.relative_to(h.io(package)).as_posix(), Sha256=h.digest(p)) for p in sorted(h.io(package).rglob('*')) if p.is_file()]
    save(package / 'configuration.json', dict(Revision=revision, PackageFiles=files, BackupDestination=str(backup),
        PythonExecutable=sys.executable, PythonExecutableSha256=h.digest(sys.executable), IsolatedCanalWaveform=True,
        NoLiveMonitoring=True, ExpectedAnalyticControls=15, ExpectedExponentialControls=6, ExpectedQueries=1060,
        NeuronalModelsExecuted=False, LiveSelectionChanged=False, PhysiologyQualified=False, AvatarRecoveryAssessed=False))
    pointer = dict(Package=str(package), Backup=str(backup), Revision=revision, NoLiveMonitoring=True,
                   Launcher=str(package / 'source/tools/start-canal-waveform-window.ps1'))
    save(ROOT / 'artifacts' / (package.name + '-prepared.json'), pointer)
    print(json.dumps(pointer, indent=2), flush=True)


def preflight(package):
    cfg, provenance = read(package / 'configuration.json'), read(package / 'provenance.json')
    assert cfg['IsolatedCanalWaveform'] and cfg['NoLiveMonitoring'] and cfg['ExpectedQueries'] == 1060
    assert cfg['ExpectedAnalyticControls'] == 15 and cfg['ExpectedExponentialControls'] == 6
    assert not any(cfg[k] for k in ('NeuronalModelsExecuted', 'LiveSelectionChanged', 'PhysiologyQualified', 'AvatarRecoveryAssessed'))
    assert read(package / 'waveform-input.json') == protocol(read(package / 'baseline/bundle-input.json'))
    assert {r['path'] for r in provenance['preservedSources']} == set(PRESERVED)
    for row in provenance['preservedSources']:
        assert h.digest(package / 'source' / row['path']) == row['sha256'], row['path']
    for row in provenance['files']:
        assert h.digest(package / row['path']) == row['sha256'], row['path']
    for key in ('bundle', 'graded'):
        review = read(package / ('reviewed-' + key + '.json'))
        assert review['operatorReportedCompletion'] and review['numericalCriteriaPassed']
        assert review['manifestSha256'] == provenance['reviewedManifests'][key]
    for row in cfg['PackageFiles']:
        assert h.digest(package / row['Path']) == row['Sha256'], row['Path']
    assert h.digest(cfg['PythonExecutable']) == cfg['PythonExecutableSha256']
    assert base.environment() == read(package / 'preparation-environment.json')
    print('Completed mechanical evidence, original limits, source and numerical dependencies preserved; no model/test executed by preflight.', flush=True)


if __name__ == '__main__':
    if not __debug__:
        raise RuntimeError('Assertions must remain enabled.')
    parser = argparse.ArgumentParser()
    parser.add_argument('command', choices=['prepare', 'preflight', 'postflight', 'controls', 'study'])
    parser.add_argument('path', type=Path, nargs='?')
    args = parser.parse_args()
    if args.command == 'prepare':
        prepare()
    else:
        package = args.path.resolve()
        preflight(package)
        if args.command in ('controls', 'study'):
            from canal_waveform_study import analytic_controls, run
            (analytic_controls if args.command == 'controls' else run)(package)
