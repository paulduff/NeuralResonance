"""Independent analytic controls only; no pending capture or neuronal inputs.

The full connected-reference gate remains completion-gated. This small driver
calls only its already authored test suite, using synthetic analytic fixtures.
"""
import argparse
from datetime import datetime, timezone
import json
from pathlib import Path
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile

from afferent_precision_gate import h, read, save
from cleft_trajectory_reference import base
from canal_afferent_reference_gate import control_inventory, controls, require_controls, relative

ROOT = Path(__file__).resolve().parents[1]
REVIEW = ROOT / 'artifacts/canal-waveform-20261119-2dd76a-review'


def prepare():
    assert not h.git('status', '--porcelain'), 'Commit first.'
    revision = h.git('rev-parse', 'HEAD')
    assert revision == h.git('rev-parse', 'origin/main'), 'Push first.'
    assert shutil.disk_usage('D:/').free >= 2 * 1024**3
    h.verify(REVIEW, ['review.json'])
    review = read(REVIEW / 'review.json')
    assert review['operatorReportedCompletion'] and review['numericalCriteriaPassed']
    capture = Path(review['capture'])
    assert h.digest(capture / 'backup-manifest.json') == review['manifestSha256']
    h.verify(capture, ['preparation-environment.json'])
    environment = base.environment()
    assert environment == read(capture / 'preparation-environment.json')
    methods = control_inventory(ROOT)
    package = Path('D:/DNNE-build-work') / ('canal-controls-' + datetime.now(timezone.utc).strftime('%Y%m%d') + '-' + uuid.uuid4().hex[:6])
    backup = Path('D:/DNNE-desktop-runs/UnityWorld') / package.name
    assert not package.exists() and not backup.exists()
    package.mkdir()
    subprocess.run(['git', 'archive', '--format=zip', '--output=' + str(package / 'source.zip'), revision], cwd=ROOT, check=True)
    with ZipFile(package / 'source.zip') as archive:
        assert archive.comment.decode().strip() == revision
        for entry in archive.infolist():
            name = relative(entry.filename)
            assert ((entry.external_attr >> 16) & 0o170000) != 0o120000
            target = h.io(package / 'source' / name)
            if entry.is_dir():
                target.mkdir(parents=True, exist_ok=True)
            else:
                target.parent.mkdir(parents=True, exist_ok=True)
                with target.open('xb') as stream:
                    stream.write(archive.read(entry))
    for source, name in ((REVIEW / 'review.json', 'reviewed-environment.json'),
                         (capture / 'preparation-environment.json', 'qualified-environment.json'),
                         (package / 'source/tools/backup-unity-run.ps1', 'backup-run.ps1')):
        shutil.copyfile(h.io(source), h.io(package / name))
        assert h.digest(source) == h.digest(package / name)
    save(package / 'preparation-environment.json', environment)
    save(package / 'provenance.json', dict(environmentManifestSha256=review['manifestSha256'],
        environmentSha256=h.digest(capture / 'preparation-environment.json'),
        fixturePolicy='synthetic analytic controls only; no captured head, neuronal trajectory or live service input',
        modelsExecutedDuringPreparation=False, testsExecutedDuringPreparation=False,
        pendingCaptureAccessed=False, connectedReferenceExecuted=False, biologicalParametersChanged=False))
    files = [dict(Path=f.relative_to(h.io(package)).as_posix(), Sha256=h.digest(f))
             for f in sorted(h.io(package).rglob('*')) if f.is_file()]
    save(package / 'configuration.json', dict(Revision=revision, PackageFiles=files, BackupDestination=str(backup),
        PythonExecutable=sys.executable, PythonExecutableSha256=h.digest(sys.executable),
        IsolatedCanalReferenceControls=True, NoLiveMonitoring=True, ExpectedControls=16, ExpectedControlMethods=methods,
        NeuronalModelsExecuted=False, NativeBodyExecuted=False, PendingCaptureAccessed=False,
        ConnectedReferenceExecuted=False, LiveSelectionChanged=False, PhysiologyQualified=False, AvatarBalanceAssessed=False))
    pointer = dict(Package=str(package), Backup=str(backup), Revision=revision, NoLiveMonitoring=True,
                   Launcher=str(package / 'source/tools/start-canal-controls-window.ps1'))
    save(ROOT / 'artifacts' / (package.name + '-prepared.json'), pointer)
    print(json.dumps(pointer, indent=2), flush=True)


def preflight(package):
    cfg, provenance = read(package / 'configuration.json'), read(package / 'provenance.json')
    assert cfg['IsolatedCanalReferenceControls'] and cfg['NoLiveMonitoring'] and cfg['ExpectedControls'] == 16
    assert cfg['ExpectedControlMethods'] == control_inventory(package / 'source')
    assert not any(cfg[k] for k in ('NeuronalModelsExecuted', 'NativeBodyExecuted', 'PendingCaptureAccessed',
                                  'ConnectedReferenceExecuted', 'LiveSelectionChanged', 'PhysiologyQualified', 'AvatarBalanceAssessed'))
    assert not any(provenance[k] for k in ('modelsExecutedDuringPreparation', 'testsExecutedDuringPreparation',
                                         'pendingCaptureAccessed', 'connectedReferenceExecuted', 'biologicalParametersChanged'))
    review = read(package / 'reviewed-environment.json')
    assert review['operatorReportedCompletion'] and review['numericalCriteriaPassed']
    assert review['manifestSha256'] == provenance['environmentManifestSha256']
    assert h.digest(package / 'qualified-environment.json') == provenance['environmentSha256']
    assert base.environment() == read(package / 'preparation-environment.json') == read(package / 'qualified-environment.json')
    for row in cfg['PackageFiles']:
        assert h.digest(package / relative(row['Path'])) == row['Sha256'], row['Path']
    assert h.digest(cfg['PythonExecutable']) == cfg['PythonExecutableSha256']
    print('Analytic control source and numerical dependencies preserved; no neuronal/native model or pending capture accessed.', flush=True)


if __name__ == '__main__':
    if not __debug__:
        raise RuntimeError('Assertions must remain enabled.')
    parser = argparse.ArgumentParser()
    parser.add_argument('command', choices=['prepare', 'preflight', 'postflight', 'controls'])
    parser.add_argument('path', type=Path, nargs='?')
    args = parser.parse_args()
    if args.command == 'prepare':
        prepare()
    else:
        if args.path is None:
            parser.error('A frozen controls package is required.')
        package = args.path.resolve()
        preflight(package)
        if args.command == 'controls':
            controls(package)
            require_controls(package)
            print('All 16 analytic controls passed. Connected sensory accuracy and physiology remain unqualified.', flush=True)
