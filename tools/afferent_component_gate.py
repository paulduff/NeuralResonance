"""Freeze the identified sensory axon components; no inline tests or models."""
import argparse
import csv
from datetime import datetime,timezone
import json
from pathlib import Path,PurePosixPath
import re
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile
import cleft_rest_gate as prior

ROOT=Path(__file__).resolve().parents[1];h=prior.h;read=h.read;save=prior.save
COUNTS=dict(prior.COUNTS,VestibularAfferentMembraneTests=16,VestibularAfferentCableTests=10,VestibularCleftAfferentTests=8)
PRESERVED=prior.PRESERVED
REVISION=prior.REVISION
PIN='47d499449df66c6676ee43cbb08ea1995469a817'


def walk(value):
    if isinstance(value,dict):
        yield value
        for x in value.values():yield from walk(x)
    elif isinstance(value,list):
        for x in value:yield from walk(x)


def source_choices(source,implementation):
    assert source['provenance']['revision']==PIN and source['provenance']['xmlEqualsArchive']
    assert source['provenance']['sha256']=='9A3610A622D0BC032827812793524A6A338EA61BB912035FD84E58F1F80C8CEA'
    expressions={}
    for node in walk(source['sections']['ExprList']):
        tag=node.get('attributes',{}).get('tag')
        if tag in ('var12','var13','var14','var15','var16'):
            expressions[tag]={n['attributes']['name']:n['attributes']['expr'] for n in node.get('children',[]) if n['element']=='expressions'}
    tables={}
    for switch,child,name in [('sw1','int3','ActivationTimes'),('sw2','int4','InactivationTimes')]:
        parent=next(n for n in walk(source['sections']['FunctionFeatureList']) if n.get('element')=='FunctionFeature' and n.get('attributes',{}).get('tag')==switch)
        function=next(n for n in walk(parent['children']) if n.get('element')=='FunctionFeature' and n.get('attributes',{}).get('tag')==child)
        raw=next(n['attributes']['valueMatrix'] for n in function['children'] if n.get('attributes',{}).get('name')=='p:table')
        rows=[]
        for line in raw.split('|')[1:]:
            fields=next(csv.reader([line],quotechar="'"));assert fields[0]=='2';rows.append(dict(voltageMv=float(fields[1]),tauMs=float(fields[2])))
        assert int(raw.split('|')[0])==len(rows)
        tokens=re.search(r'\b'+name+r'=\[([^\]]+)\];',implementation).group(1).split(',')
        assert [float(x.strip()) for x in tokens]==[r['tauMs'] for r in rows],name
        tables[name]=dict(scope=switch+'/'+child,name=function['attributes']['name'],points=rows)
    assert len(tables['ActivationTimes']['points'])==24 and len(tables['InactivationTimes']['points'])==22
    assert expressions['var14']['J_navf']==expressions['var16']['J_navf']=='120*av_navf^3*iv_navf*(V_f-Ena)'
    return dict(sourceRevision=PIN,modelVersion='vestibular-afferent-deposited-may14-linear-v1',tables=tables,expressions=expressions,
        interpolation='explicit piecewise linear, not an inferred COMSOL default',activationOutside='0.2 ms outside [-55,60]',
        inactivationOutside='endpoint secants; nonpositive or nonfinite times rejected, no clipping',
        cableSign='outward current removes charge; sigma*pi*r^2/distance axial conductance',
        sourceSwitchDefaultEstablished=False,paperSimulationReproduced=False,equationsExecuted=False,testsExecuted=False)


def prepare(review_folder):
    assert not h.git('status','--porcelain'),'Commit first.'
    revision=h.git('rev-parse','HEAD');assert revision==h.git('rev-parse','origin/main'),'Push first.'
    assert shutil.disk_usage(ROOT).free>=4*1024**3 and shutil.disk_usage('D:/').free>=8*1024**3
    h.verify(review_folder,['review.json']);review=read(review_folder/'review.json')
    assert review['sourceRevision']==REVISION and review['operatorReportedCompletion'] and review['savedAnalysisExactlyReproduced']
    assert review['passedTests']==132 and review['numericalCriteriaPassed'] and review['stationaryComponentCases']==12
    capture=Path(review['capture']);assert h.digest(capture/'backup-manifest.json')==review['manifestSha256']
    h.verify(capture,['managed-runtime-before.json']+['source/'+n for n in PRESERVED])
    for name in PRESERVED:assert (ROOT/name).read_text(encoding='utf-8-sig')==h.io(capture/'source'/name).read_text(encoding='utf-8-sig'),name
    source=ROOT/'artifacts/vestibular-calyx-equations-20261117';pdf=ROOT/'artifacts/vestibular-calyx-source-20261116'
    h.verify(source,['xml-inspection/template.json','xml-inspection/paper_roles.json'])
    h.verify(pdf,['2022-12-11 Supporting Information.pdf','source-manifest.json'])
    implementation=(ROOT/'src/NRE.SimAvatar/AvatarVestibularAfferentMembrane.cs').read_text(encoding='utf-8-sig')
    choices=source_choices(read(source/'xml-inspection/template.json'),implementation)
    methods=prior.component.inventory.inventory(ROOT,COUNTS);assert sum(methods.values())==166
    package=ROOT/'artifacts'/('afferent-components-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6]);backup=Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
    assert not package.exists() and not backup.exists();package.mkdir()
    subprocess.run(['git','archive','--format=zip','--output='+str(package/'source.zip'),revision],cwd=ROOT,check=True)
    with ZipFile(package/'source.zip') as archive:
        for entry in archive.infolist():
            name=PurePosixPath(entry.filename)
            assert not name.is_absolute() and '..' not in name.parts and ':' not in entry.filename and '\\' not in entry.filename
            assert ((entry.external_attr>>16)&0o170000)!=0o120000
            target=h.io(package/'source'/name)
            if entry.is_dir():target.mkdir(parents=True,exist_ok=True)
            else:
                target.parent.mkdir(parents=True,exist_ok=True)
                with target.open('xb') as stream:stream.write(archive.read(entry))
    def copy(source_path,name):
        target=h.io(package/name);target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(h.io(source_path),target);assert h.digest(source_path)==h.digest(target)
    copy(review_folder/'review.json','qualified-refinement-review.json');copy(capture/'managed-runtime-before.json','qualified-managed-runtime.json')
    copy(source/'xml-inspection/template.json','source-evidence/template.json');copy(source/'xml-inspection/paper_roles.json','source-evidence/paper_roles.json')
    copy(pdf/'2022-12-11 Supporting Information.pdf','source-evidence/supporting-information.pdf');copy(pdf/'source-manifest.json','source-evidence/source-manifest.json')
    copy(package/'source/tools/backup-unity-run.ps1','backup-run.ps1');save(package/'source-decisions.json',choices)
    save(package/'provenance.json',dict(originalRevision=REVISION,originalManifestSha256=review['manifestSha256'],
        preservedSources=[dict(path=n,sha256=h.digest(capture/'source'/n)) for n in PRESERVED],
        sourceEvidence=[dict(path=p.relative_to(package).as_posix(),sha256=h.digest(p)) for p in sorted((package/'source-evidence').iterdir())],
        pendingCapturesRead=False,modelsExecutedDuringPreparation=False,testsExecutedDuringPreparation=False))
    files=[dict(Path=p.relative_to(h.io(package)).as_posix(),Sha256=h.digest(p)) for p in sorted(h.io(package).rglob('*')) if p.is_file()]
    dotnet=shutil.which('dotnet');assert dotnet
    save(package/'configuration.json',dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),PackageFiles=files,
        DotnetExecutable=dotnet,DotnetExecutableSha256=h.digest(dotnet),PythonExecutable=sys.executable,PythonExecutableSha256=h.digest(sys.executable),
        BackupDestination=str(backup),IsolatedAfferentComponents=True,NoLiveMonitoring=True,ExpectedTestCases=166,ExpectedClassCounts=COUNTS,
        ExpectedTestMethods=methods,TestFilter='|'.join('FullyQualifiedName~'+c for c in COUNTS),
        IndependentCoupledTrajectoryQualified=False,StationaryInitializationQualified=False,SpatialGeometryCalibrated=False,
        AfferentFiringQualified=False,PhysiologyQualified=False,AvatarRecoveryAssessed=False,LiveSelectionChanged=False))
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,NoLiveMonitoring=True,Launcher=str(package/'source/tools/start-afferent-components-window.ps1'))
    save(package.parent/(package.name+'-prepared.json'),pointer);print(json.dumps(pointer,indent=2),flush=True)


def preflight(package):
    cfg=read(package/'configuration.json');review=read(package/'qualified-refinement-review.json');provenance=read(package/'provenance.json')
    assert cfg['IsolatedAfferentComponents'] and cfg['NoLiveMonitoring'] and cfg['ExpectedTestCases']==166 and cfg['ExpectedClassCounts']==COUNTS
    assert review['sourceRevision']==provenance['originalRevision']==REVISION and review['manifestSha256']==provenance['originalManifestSha256']
    assert review['operatorReportedCompletion'] and review['savedAnalysisExactlyReproduced'] and review['numericalCriteriaPassed'] and review['passedTests']==132
    assert {r['path'] for r in provenance['preservedSources']}==set(PRESERVED)
    for row in provenance['preservedSources']:assert h.digest(package/'source'/row['path'])==row['sha256']
    for row in provenance['sourceEvidence']:assert h.digest(package/row['path'])==row['sha256']
    assert source_choices(read(package/'source-evidence/template.json'),(package/'source/src/NRE.SimAvatar/AvatarVestibularAfferentMembrane.cs').read_text(encoding='utf-8-sig'))==read(package/'source-decisions.json')
    for directory in read(package/'qualified-managed-runtime.json')['directories']:
        root=h.io(directory['path']);assert {str(p) for p in root.rglob('*') if p.is_file()}=={str(h.io(e['path'])) for e in directory['files']}
        for row in directory['files']:assert h.digest(row['path'])==row['sha256']
    print('Source timing data, earlier sensory kernels and reviewed managed runtime preserved; no model or test executed.',flush=True)


def runtime(package):
    preflight(package);assert read(package/'managed-runtime-before.json')==read(package/'qualified-managed-runtime.json')


def check_tests(package):
    preflight(package);result=prior.component.inventory.check_tests(package,COUNTS)
    save(package/'analysis.json',dict(completed=True,componentChecksPassed=True,tests=result,
        independentCoupledTrajectoryQualified=False,stationaryInitializationQualified=False,spatialGeometryCalibrated=False,
        afferentFiringQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False))
    print(json.dumps(result,indent=2),flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    p=argparse.ArgumentParser();p.add_argument('command',choices=['prepare','preflight','postflight','runtime','check-tests']);p.add_argument('path',type=Path)
    a=p.parse_args();dict(prepare=prepare,preflight=preflight,postflight=preflight,runtime=runtime,**{'check-tests':check_tests})[a.command](a.path.resolve())
