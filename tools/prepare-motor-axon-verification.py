"""Freeze pushed source for an independent visible motor/calcium test window.

No tests, live services or old saves are accessed. Prior reference traces are
read only when explicitly selected as the Golgi comparison baseline.
"""
from datetime import datetime, timezone
from hashlib import sha256
import argparse
import json
from pathlib import Path
import shutil
import subprocess
import uuid
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[1]


def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT, encoding='utf-8').strip()


def write(path, value):
    path.write_text(json.dumps(value, indent=2, allow_nan=False)+'\n', encoding='utf-8')


def main():
    parser=argparse.ArgumentParser()
    selection=parser.add_mutually_exclusive_group()
    selection.add_argument('--golgi-reference-from',type=Path)
    selection.add_argument('--muscle-calcium',action='store_true')
    selection.add_argument('--motoneuron-reference',action='store_true')
    selection.add_argument('--motoneuron-active-from',type=Path)
    selection.add_argument('--motoneuron-numerical-from',type=Path)
    selection.add_argument('--motor-unit-plant',action='store_true')
    args=parser.parse_args()
    active_from=args.motoneuron_active_from
    active_review=None
    if args.motoneuron_numerical_from:
        active_review=args.motoneuron_numerical_from.resolve()
        reviewed_active=json.loads((active_review/'review.json').read_text(encoding='utf-8-sig'))
        assert reviewed_active['sourceRevision']=='1ef1b6a9426bd319d9866dbcc39e096af8d06035'
        assert reviewed_active['originalNumericalCriteriaPassed'] is False
        assert reviewed_active['candidateConditionsCompleted']==reviewed_active['referenceConditionsCompleted']==30
        previous_active=Path(reviewed_active['capture'])
        previous_config=json.loads((previous_active/'configuration.json').read_text(encoding='utf-8-sig'))
        active_from=Path(previous_config['MotoneuronActiveBaseline']['Review'])
    assert not git('status', '--porcelain'), 'Commit the coherent candidate before freezing.'
    revision = git('rev-parse', 'HEAD')
    assert revision == git('rev-parse', 'origin/main'), 'Push before packaging.'
    dotnet = shutil.which('dotnet'); assert dotnet
    stamp = datetime.now(timezone.utc).strftime('%Y%m%d')
    with_motoneuron=args.motoneuron_reference or active_from is not None
    prefix='motor-unit-plant' if args.motor_unit_plant else 'motoneuron-numerical' if active_review else 'motoneuron-active' if active_from else 'motoneuron-reference' if args.motoneuron_reference else 'motor-calcium' if args.muscle_calcium else 'motor-axon'
    package = ROOT/'artifacts'/(prefix+'-'+stamp+'-'+uuid.uuid4().hex[:6])
    backup = Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
    assert not package.exists() and not backup.exists()
    package.mkdir()
    subprocess.run(['git','archive','--format=zip','--output='+str(package/'source.zip'),revision],cwd=ROOT,check=True)
    with ZipFile(package/'source.zip') as archive:
        assert all(not Path(n).is_absolute() and '..' not in Path(n).parts for n in archive.namelist())
        archive.extractall(package/'source')
    shutil.copy2(package/'source/tools/backup-unity-run.ps1',package/'backup-run.ps1')
    baseline=None; muscle_reference=None; motoneuron_reference=None; motoneuron_active_baseline=None; original_active_baseline=None
    python=ROOT/'artifacts/numerical-reference-env-20261102/Scripts/python.exe'
    if with_motoneuron:
        assert python.is_file()
        reference=ROOT/'artifacts/reference-motoneuron-239582-20261106'
        reference_revision=subprocess.check_output(['git','rev-parse','HEAD'],cwd=reference,encoding='utf-8').strip()
        assert reference_revision=='0d11354490fad5fcd940d016a8d69b000693fd81'
        selected=['na3rp.mod','naps.mod','kdrRL.mod','mAHP.mod','gh.mod','L_Ca_inact.mod',
                  'init_5cmpt.hoc','FivecomptcMG/FivecomptcMG_0/FivecomptcMG_0.hoc']
        copied=[]
        target=package/'motoneuron-input';target.mkdir()
        for name in selected:
            original=reference/name
            committed=subprocess.check_output(['git','show',reference_revision+':'+name],cwd=reference)
            # Git checkout newline conversion can differ from the committed blob.
            # Freeze the blob itself and verify the copied bytes.
            destination=target/original.name;destination.write_bytes(committed)
            value=sha256(committed).hexdigest()
            assert sha256(destination.read_bytes()).hexdigest()==value
            copied.append(dict(SourcePath=name,PackagedName=original.name,Sha256=value))
        motoneuron_reference=dict(Revision=reference_revision,Model='https://modeldb.science/239582',
            Study='https://doi.org/10.1152/jn.00850.2016',Cell='FivecomptcMG_0',Files=copied,
            NativeNeuronExecuted=False,ActiveCellAssessment=bool(active_from))
        write(target/'provenance.json',motoneuron_reference)
    if active_from:
        from motoneuron_active_protocol import protocol
        reviewed=active_from.resolve()
        report=json.loads((reviewed/'review.json').read_text(encoding='utf-8-sig'))
        assert report['channelAndPassiveCriteriaPassed'] and report['backupRepaired']
        assert report['passedFocusedAndManifestTests']==10 and report['clampCases']==96 and report['passiveCases']==9
        assert report['sourceRevision']=='1ff49a1603599b3581b3557c7aab193774173c96'
        previous=Path(report['capture']);previous_backup=Path(report['backup'])
        for base in (previous,previous_backup):
            assert sha256((base/'backup-manifest.json').read_bytes()).hexdigest().upper()==report['repairedManifestSha256']
        review_manifest=json.loads((reviewed/'backup-manifest.json').read_text(encoding='utf-8-sig'))
        review_backup=Path(review_manifest['Destination'])
        assert sha256((reviewed/'review.json').read_bytes()).digest()==sha256((review_backup/'review.json').read_bytes()).digest()
        target=package/'motoneuron-baseline';target.mkdir()
        shutil.copy2(reviewed/'review.json',target/'review.json')
        shutil.copy2(previous/'window-status.json',target/'original-window-status.json')
        shutil.copy2(previous/'backup-manifest.json',target/'repaired-backup-manifest.json')
        motoneuron_active_baseline=dict(Review=str(reviewed),Capture=str(previous),Backup=str(previous_backup),
            ReviewSha256=sha256((reviewed/'review.json').read_bytes()).hexdigest(),
            ChannelsAndPassivePassed=True,ActiveCellPreviouslyAssessed=active_review is not None)
        write(package/'motoneuron-active-input.json',protocol())
    if active_review:
        review_manifest=json.loads((active_review/'backup-manifest.json').read_text(encoding='utf-8-sig'))
        review_bytes=(active_review/'review.json').read_bytes()
        assert sha256(review_bytes).hexdigest().upper()==next(e['Sha256'] for e in review_manifest['Files'] if e['Path']=='review.json')
        assert review_bytes==(Path(review_manifest['Destination'])/'review.json').read_bytes()
        target=package/'motoneuron-failed-active-baseline';target.mkdir()
        (target/'review.json').write_bytes(review_bytes)
        original_manifest=json.loads((previous_active/'backup-manifest.json').read_text(encoding='utf-8-sig'))
        expected={e['Path'].replace('\\','/').lower():e['Sha256'] for e in original_manifest['Files']}
        previous_active_backup=Path(reviewed_active['backup'])
        for name in ('configuration.json','window-status.json','motoneuron-active-input.json','motoneuron-active-reference/comparison.json'):
            raw=(previous_active/name).read_bytes()
            assert sha256(raw).hexdigest().upper()==expected[name.lower()]
            assert raw==(previous_active_backup/name).read_bytes()
            destination=target/name;destination.parent.mkdir(parents=True,exist_ok=True);destination.write_bytes(raw)
        original_spec=json.loads((target/'motoneuron-active-input.json').read_text(encoding='utf-8-sig'))
        new_spec=protocol()
        for key in ('cases','criteria','durationMs','sampleMs','initialVoltageMv','stepsMs','thresholdMv','sourceRevision','cell'):
            assert original_spec[key]==new_spec[key], 'Changed original biological inputs or acceptance bounds: '+key
        original_active_baseline=dict(Review=str(active_review),Capture=str(previous_active),Backup=str(previous_active_backup),
            ReviewSha256=sha256(review_bytes).hexdigest(),OriginalNumericalCriteriaPassed=False,
            BiologicalInputsAndAcceptanceBoundsUnchanged=True)
    if args.muscle_calcium:
        assert python.is_file()
        reference=ROOT/'artifacts/reference-muscle-nagamori-20261106'
        reference_revision=subprocess.check_output(['git','rev-parse','HEAD'],cwd=reference,encoding='utf-8').strip()
        assert reference_revision=='c317be803f01965a1ca8fc4cbad1ba9156433af8'
        original=reference/'modelParameter.mat'
        digest=sha256(original.read_bytes()).hexdigest()
        assert digest=='01123aec76d1acc18b306ae1bd9daabfd29fe9287b54212aef5db3f15f719ebd'
        target=package/'muscle-input/modelParameter.mat';target.parent.mkdir()
        shutil.copy2(original,target)
        assert sha256(target.read_bytes()).hexdigest()==digest
        muscle_reference=dict(Revision=reference_revision,ParameterSha256=digest,
            Source='https://github.com/anagamori/MU-Population-Model-Nagamori-2020',
            Study='https://doi.org/10.1371/journal.pcbi.1008707',
            RowOneBased=1,Length=1,Yield=1,Sag=1,NativeMatlabExecuted=False)
        write(package/'muscle-input/provenance.json',muscle_reference)
    if args.golgi_reference_from:
        previous=args.golgi_reference_from.resolve();assert python.is_file()
        status=json.loads((previous/'window-status.json').read_text(encoding='utf-8-sig'))
        assert status['Status']=='FAILED' and status['ExitCode']==1
        assert status['Revision']=='ba67f99bea6162b6bd0a77aa235182e91cc6a5c9'
        manifest=json.loads((previous/'backup-manifest.json').read_text(encoding='utf-8-sig'))
        expected={entry['Path'].replace('\\','/').lower():entry['Sha256'] for entry in manifest['Files']}
        selected=[p for folder in ('reference','study') for p in (previous/folder).rglob('*') if p.is_file()]
        selected.append(previous/'clamps/channels.json')
        for original in selected:
            relative=original.relative_to(previous)
            value=sha256(original.read_bytes()).hexdigest().upper()
            assert value==expected[relative.as_posix().lower()],str(original)
            target=package/'golgi-input'/relative;target.parent.mkdir(parents=True,exist_ok=True)
            shutil.copy2(original,target)
            assert sha256(target.read_bytes()).hexdigest().upper()==value
        baseline=dict(capture=str(previous),revision=status['Revision'],copiedInputFiles=len(selected),
            originalBackupManifestSha256=sha256((previous/'backup-manifest.json').read_bytes()).hexdigest())
    files=[dict(Path=p.relative_to(package).as_posix(),Sha256=sha256(p.read_bytes()).hexdigest().upper())
        for p in sorted(package.rglob('*')) if p.is_file()]
    write(package/'configuration.json',dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),
        DotnetExecutable=dotnet,PackageFiles=files,BackupDestination=str(backup),ExpectedTestCases=61 if args.motor_unit_plant else 12 if with_motoneuron else 46 if args.muscle_calcium else 33,
        TestFilter=('FullyQualifiedName~MotoneuronReferenceTests|FullyQualifiedName~SolutionManifestTests' if with_motoneuron else
            'FullyQualifiedName~MotorAxonTransportTests|FullyQualifiedName~SolutionManifestTests'+
            ('|FullyQualifiedName~MotorUnitCalciumTests' if args.muscle_calcium or args.motor_unit_plant else '')+
            ('|FullyQualifiedName~IsometricMotorUnitPlantTests' if args.motor_unit_plant else '')),
        NoLiveMonitoring=True,LiveMotorPathEnabled=False,GolgiBaseline=baseline,
        MuscleCalciumReference=muscle_reference,MotoneuronReference=motoneuron_reference,
        MotoneuronActiveBaseline=motoneuron_active_baseline,OriginalFailedActiveBaseline=original_active_baseline,
        IsometricMotorUnitPlant=args.motor_unit_plant,PythonExecutable=str(python)))
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,
        Launcher=str(package/'source/tools/start-motor-axon-window.ps1'),NoLiveMonitoring=True)
    write(ROOT/'artifacts'/(prefix+'-launch-'+stamp+'.json'),pointer)
    print(json.dumps(pointer,indent=2))


if __name__=='__main__':
    main()
