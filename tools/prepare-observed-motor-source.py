"""Freeze the observed motor-source gate using reviewed C:/D: reference evidence."""
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path, PurePosixPath
import re
import shutil
import subprocess
import uuid
from zipfile import ZipFile

ROOT=Path(__file__).resolve().parents[1]
COUNTS=dict(ObservedMotorSourceTests=8,MotorAxonTransportTests=32,MotorAxonReplayBufferTests=16,
            MotoneuronReferenceTests=11,SolutionManifestTests=1)


def main():
    spec=importlib.util.spec_from_file_location('packaging',ROOT/'tools/prepare-causal-motor.py')
    helper=importlib.util.module_from_spec(spec);spec.loader.exec_module(helper)
    assert not helper.git('status','--porcelain'),'Commit clean source first.'
    revision=helper.git('rev-parse','HEAD');assert revision==helper.git('rev-parse','origin/main'),'Push first.'
    review_folder=ROOT/'artifacts/motor-unit-circuit-review-20261109';helper.verify(review_folder,['review.json'])
    review=helper.read(review_folder/'review.json')
    assert review['operatorReportedCompletion'] and review['savedAnalysisExactlyReproduced'] and review['passedFocusedTests']==88
    capture=Path(review['capture']);assert helper.digest(capture/'backup-manifest.json')==review['manifestSha256']
    references=['boundary-analysis.json','boundary-input.json','source-results.json','source-metadata.json',
                'quiet-h0.003125.csv','soma-6000-h0.003125.csv']
    same=['Structures/_SharedRuntime/MotoneuronReferenceCell.cs','Structures/_SharedRuntime/MotoneuronChannelKinetics.cs',
          'tools/MotoneuronReferenceProbe/ClockAudit.cs','Protocol/MotorAxonProtocol.cs','Protocol/MotorAxonPublisher.cs',
          'Protocol/MotorAxonReplayBuffer.cs','src/NRE.SimAvatar/AvatarMotorAxonReceiver.cs']
    helper.verify(capture,['reference/'+n for n in references]+['source/'+n for n in same])
    for name in same:assert (ROOT/name).read_text(encoding='utf-8-sig')==helper.io(capture/'source'/name).read_text(encoding='utf-8-sig'),name
    methods={}
    for cls,count in COUNTS.items():
        code=(ROOT/'tests/NeuralResonanceEngine.DNNE.Tests'/(cls+'.cs')).read_text()
        found=re.findall(r'((?:\s*\[[^\n]+\])+\s*)public\s+(?:async\s+)?(?:void|Task)\s+(\w+)\(',code)
        members={name:(1 if '[Fact]' in attrs else attrs.count('[InlineData(')) for attrs,name in found if '[Fact]' in attrs or '[Theory]' in attrs}
        assert sum(members.values())==count and all(members.values()),cls
        methods.update({'NeuralResonanceEngine.DNNE.Tests.'+cls+'.'+name:n for name,n in members.items()})
    stamp=datetime.now(timezone.utc).strftime('%Y%m%d')
    package=ROOT/'artifacts'/('observed-motor-source-'+stamp+'-'+uuid.uuid4().hex[:6]);backup=Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
    assert not package.exists() and not backup.exists();package.mkdir()
    subprocess.run(['git','archive','--format=zip','--output='+str(package/'source.zip'),revision],cwd=ROOT,check=True)
    with ZipFile(package/'source.zip') as archive:
        for entry in archive.infolist():
            path=PurePosixPath(entry.filename)
            assert not path.is_absolute() and '..' not in path.parts and ':' not in entry.filename and '\\' not in entry.filename
            assert ((entry.external_attr>>16)&0o170000)!=0o120000
            target=helper.io(package/'source'/path)
            if entry.is_dir():target.mkdir(parents=True,exist_ok=True)
            else:
                target.parent.mkdir(parents=True,exist_ok=True)
                with target.open('xb') as f:f.write(archive.read(entry))
    (package/'reference').mkdir();copied=[]
    for name in references:
        source=capture/'reference'/name;target=package/'reference'/name
        shutil.copyfile(source,target);assert helper.digest(source)==helper.digest(target)
        copied.append(dict(original=str(source),path='reference/'+name,sha256=helper.digest(target)))
    shutil.copyfile(review_folder/'review.json',package/'reference/circuit-review.json')
    (package/'provenance.json').write_text(json.dumps(dict(circuitReview=review,qualifiedSourcePreserved=same,copied=copied,
        sourceEquationsAndSchedulerUnchanged=True,bodyComponentsUnchanged=True),indent=2)+'\n')
    shutil.copyfile(package/'source/tools/backup-unity-run.ps1',package/'backup-run.ps1')
    files=[dict(Path=p.relative_to(package).as_posix(),Sha256=helper.digest(p)) for p in sorted(package.rglob('*')) if p.is_file()]
    dotnet=shutil.which('dotnet');python=Path('C:/Users/duff_/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe')
    assert dotnet and python.is_file()
    config=dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),PackageFiles=files,
        DotnetExecutable=dotnet,DotnetExecutableSha256=helper.digest(dotnet),PythonExecutable=str(python),PythonExecutableSha256=helper.digest(python),
        BackupDestination=str(backup),NoLiveMonitoring=True,IsolatedObservedMotorSource=True,ExpectedConditions=5,
        ExpectedTestCases=sum(COUNTS.values()),ExpectedClassCounts=COUNTS,ExpectedTestMethods=methods,
        TestFilter='|'.join('FullyQualifiedName~'+c for c in COUNTS),NativePhysicsRequested=False)
    (package/'configuration.json').write_text(json.dumps(config,indent=2)+'\n')
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,PreparedOnly=True,NoLiveMonitoring=True,
        Launcher=str(package/'source/tools/start-observed-motor-source-window.ps1'))
    (ROOT/'artifacts'/('observed-motor-source-prepared-'+stamp+'.json')).write_text(json.dumps(pointer,indent=2)+'\n')
    print(json.dumps(pointer,indent=2),flush=True)


if __name__=='__main__':main()
