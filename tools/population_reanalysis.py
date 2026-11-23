"""Prepare/review saved muscle data without rerunning the body or muscle model."""
import argparse
from datetime import datetime,timezone
import importlib.util
from pathlib import Path,PurePosixPath
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile

ROOT=Path(__file__).resolve().parents[1]
ORIGINAL=Path('D:/DNNE-build-work/native-muscle-population-20261123-b3cf65')
ORIGINAL_REVISION='da4c72bf042c8c5aaa27189b6077ca4b2a4931b1'

def module(name,path):
    s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
h=module('population_reanalysis_helpers',ROOT/'tools/native_avatar_tendon_study.py')
read,write,digest=h.read,h.write,h.digest

def prepare():
    assert not h.git('status','--porcelain'),'Commit clean source first.'
    revision=h.git('rev-parse','HEAD');assert revision==h.git('rev-parse','origin/main')
    name='native-population-reanalysis-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6]
    package=Path('D:/DNNE-build-work')/name;backup=Path('D:/DNNE-desktop-runs/UnityWorld')/name
    assert not package.exists() and not backup.exists();package.mkdir()
    subprocess.run(['git','archive','--format=zip','--output='+str(package/'source.zip'),revision],cwd=ROOT,check=True)
    with ZipFile(package/'source.zip') as archive:
        for entry in archive.infolist():
            p=PurePosixPath(entry.filename);assert not p.is_absolute() and '..' not in p.parts and ':' not in entry.filename and '\\' not in entry.filename
            assert ((entry.external_attr>>16)&0o170000)!=0o120000
            target=package/'source'/p
            if entry.is_dir():target.mkdir(parents=True,exist_ok=True)
            else:
                target.parent.mkdir(parents=True,exist_ok=True)
                with target.open('xb') as f:f.write(archive.read(entry))
    protect=module('population_reanalysis_preservation',ROOT/'tools/native_articulated_head_study.py').protected_copy
    source=ROOT/'artifacts'/(ORIGINAL.name+'-review')
    protect(source,package/'failure-review',[e['Path'].replace('\\','/') for e in read(source/'backup-manifest.json')['Files']])
    protect(ORIGINAL,package/'original-identity',['source.zip','configuration.json','window-status.json','native-population-analysis.json'])
    shutil.copyfile(package/'source/tools/backup-unity-run.ps1',package/'backup-run.ps1')
    python=Path(sys.executable)
    write(package/'configuration.json',dict(Revision=revision,BackupDestination=str(backup),NoLiveMonitoring=True,
        PythonExecutable=str(python),PythonExecutableSha256=digest(python),PythonRuntime=h.python_runtime(),
        PackageFiles=[dict(Path=p.relative_to(package).as_posix(),Sha256=digest(p)) for p in sorted(package.rglob('*')) if p.is_file()],
        OriginalPackage=str(ORIGINAL),OriginalRevision=ORIGINAL_REVISION,OriginalManifestSha256=digest(ORIGINAL/'backup-manifest.json'),
        ExpectedControlTests=8,ExpectedCases=8,ExpectedPointForces=2560,ExpectedInspectReceipts=320,
        SavedAnalysisOnly=True,OriginalBoundsChanged=False,NativeUnityExecuted=False,ModelsExecuted=False,AvatarStandingQualified=False))
    preflight(package);print(str(package),flush=True)

def preflight(package):
    cfg=read(package/'configuration.json')
    assert cfg['NoLiveMonitoring'] and cfg['SavedAnalysisOnly'] and cfg['OriginalPackage']==str(ORIGINAL) and cfg['OriginalRevision']==ORIGINAL_REVISION
    assert [cfg[k] for k in ('ExpectedControlTests','ExpectedCases','ExpectedPointForces','ExpectedInspectReceipts')]==[8,8,2560,320]
    assert not any(cfg[k] for k in ('OriginalBoundsChanged','NativeUnityExecuted','ModelsExecuted','AvatarStandingQualified'))
    for e in cfg['PackageFiles']:assert digest(package/e['Path'])==e['Sha256'],e['Path']
    assert digest(cfg['PythonExecutable'])==cfg['PythonExecutableSha256'] and cfg['PythonRuntime']==h.python_runtime()
    review=read(package/'failure-review/review.json')
    assert review['operatorReportedCompletion'] and review['originalStatusRetained']=='FAILED' and review['nativeCasesCompleted']==8 and not review['physicsPayloadChanged']
    assert sum(len(r['immediateInspectReceipts']) for r in review['envelopeDiagnosis'])==320
    assert digest(ORIGINAL/'backup-manifest.json')==cfg['OriginalManifestSha256']==digest(package/'original-identity/backup-manifest.json')
    assert read(ORIGINAL/'window-status.json')==read(package/'original-identity/window-status.json')
    assert read(ORIGINAL/'configuration.json')['NativeLimits']==dict(nativePointMeters=2e-6,pathLengthMeters=2e-12,forceNewtons=1e-10,responseRadians=1e-6)
    for name in ('native_avatar_tendon_study.py','named_muscle_native_analysis.py','native_geometry_identity.py'):
        assert (ROOT/'tools'/name).read_text(encoding='utf-8-sig')==(ORIGINAL/'source/tools'/name).read_text(encoding='utf-8-sig')
    print('Original failed capture, unchanged numerical limits and decode helpers retained. No body/muscle model or test executed by preflight.',flush=True)

def preservation(package):
    preflight(package)
    common=module('original_population_bytes',ROOT/'tools/review-olive-and-motor-20261109.py')
    archive=module('original_population_archive',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    result=common.verify(ORIGINAL,['test-binaries.json','probe-binaries.json'],ORIGINAL_REVISION,'FAILED',1,1000)
    archive.source_archive(common,ORIGINAL,ORIGINAL_REVISION,reference_archive=package/'original-identity/source.zip')
    print('All original files and both protected copies remain exact. No model or native player rerun.',flush=True)

def controls(package):
    import unittest
    tests=module('population_receipt_controls',ROOT/'tools/test_population_receipts.py')
    suite=unittest.defaultTestLoader.loadTestsFromModule(tests);assert suite.countTestCases()==8
    result=unittest.TextTestRunner(verbosity=2).run(suite)
    write(package/'receipt-controls.json',dict(testsRun=result.testsRun,passed=result.wasSuccessful(),syntheticRecordsOnly=True))
    assert result.wasSuccessful() and result.testsRun==8

def compare(package):
    cfg=read(package/'configuration.json');assert read(package/'receipt-controls.json')==dict(testsRun=8,passed=True,syntheticRecordsOnly=True)
    assert package.resolve()!=ORIGINAL.resolve()
    module('saved_population_comparison',ROOT/'tools/native_population_analysis.py').compare(ORIGINAL,package)
    result=read(package/'native-population-analysis.json')
    assert len(result['cases'])==8 and sum(c['exactInspectReceipts'] for c in result['cases'])==320

if __name__=='__main__':
    assert __debug__
    p=argparse.ArgumentParser();p.add_argument('stage',choices=('prepare','preflight','preservation','controls','compare'));p.add_argument('package',nargs='?',type=Path);a=p.parse_args()
    if a.stage=='prepare':prepare()
    else:
        assert a.package is not None
        globals()[a.stage](a.package.resolve())
