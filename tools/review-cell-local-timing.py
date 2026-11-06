"""Read-only reanalysis of a reported completed capture; never starts a simulation."""
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timezone
from hashlib import sha256
import importlib.util
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

sys.dont_write_bytecode = True
read = lambda p: json.loads(p.read_text(encoding='utf-8-sig'))


def digest(path):
    value = sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(8*1024*1024), b''):
            value.update(block)
    return value.hexdigest().upper()


def audit(base, manifest):
    base = base.resolve(); hashes = {}
    for row in manifest['Files']:
        path = (base/row['Path']).resolve()
        assert base in path.parents and path.stat().st_size == row['Bytes'], str(path)
        key = path.relative_to(base).as_posix().lower()
        assert key not in hashes
        value = digest(path)
        assert value == row['Sha256'].upper(), str(path)
        hashes[key] = value
    return hashes


def compare_spikes(a, b):
    return dict(firstCount=len(a), secondCount=len(b), identical=a == b,
        maximumOrdinalDifferenceMs=max((abs(x-y) for x,y in zip(a,b)),default=0) if len(a)==len(b) else None)


def main(run, output):
    run, output = run.resolve(), output.resolve()
    assert not output.exists() and run not in output.parents and output != run
    status=read(run/'window-status.json'); config=read(run/'configuration.json')
    manifest=read(run/'backup-manifest.json'); backup=Path(config['BackupDestination']).resolve()
    assert backup == Path(manifest['Destination']).resolve()
    assert status['Status']=='FINISHED' and status['ExitCode']==0 and status['OldBackupUnchanged']
    assert status['Revision']==config['Revision']=='897f9b3fef5bfaceddc6b6b9e7f287625851b624'
    assert not status['LiveBrainAccessed'] and not status['OldSavesLoaded'] and not status['ProductionCellLocalModeEnabled']
    assert digest(run/'backup-manifest.json')==digest(backup/'backup-manifest.json')
    expected={row['Path'].replace('\\','/').lower():row['Sha256'].upper() for row in manifest['Files']}
    analyser=run/'source/tools/analyse-boundary-timing.py'
    assert digest(analyser)==expected['source/tools/analyse-boundary-timing.py']
    spec=importlib.util.spec_from_file_location('frozen_boundary_review',analyser)
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
    stage_reports=[]
    with ThreadPoolExecutor(max_workers=2) as pool:
        local=pool.submit(audit,run,manifest);remote=pool.submit(audit,backup,manifest)
        legacy=read(run/'legacy-captures.json')
        for stage in ('preflight','study'):
            print('Rechecking saved '+stage+' traces (no model execution).',flush=True)
            results={}
            for mode in ('global','local'):
                folder=run/stage/mode; saved=read(folder/'cell-analysis.json')
                recalculated=module.analyse(folder,write_report=False,cell_local=mode=='local')
                assert recalculated==saved
                results[mode]=recalculated
            combined=read(run/stage/'cell-analysis.json')
            assert combined['completed'] and combined['caseCount']==48 and combined['legacyCaptureMatches']==24
            assert combined['cellLocalityPassed']==results['local']['cellLocalityPassed'] is True
            assert combined['candidateComparisons']==results['local']['comparisons']
            assert combined['contactLocality']==results['local']['contactLocality']
            assert results['global']['boundaryHypothesisSupported']
            original=read(run/stage/'global/results.json')['cases']
            assert {c['key'] for c in original}==set(legacy[stage])
            for case in original:
                baseline=legacy[stage][case['key']]
                assert case['spikes']==baseline['spikes']
                for name,value in baseline['files'].items():
                    assert expected[(stage+'/global/'+case['key']+'/'+name).lower()]==value.upper()
            stage_reports.append(dict(stage=stage,caseCount=48,legacyMatches=24,
                savedAnalysisReproducedExactly=True,cellLocalityPassed=True,
                globalSummary={k:v for k,v in results['global'].items() if k!='comparisons'},
                localSummary={k:v for k,v in results['local'].items() if k not in ('comparisons','contactLocality')}))
        print('Finishing capture/backup hash verification.',flush=True)
        hashes=local.result(); assert hashes==remote.result()
    for row in config['PackageFiles']:
        assert hashes[Path(row['Path']).as_posix().lower()]==row['Sha256'].upper()
    for row in read(run/'build-binaries.json'):
        relative=Path(row['Path']).resolve().relative_to(run).as_posix().lower()
        assert hashes[relative]==row['Sha256'].upper()
    namespace={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    trx=ET.parse(run/'test-results/dnne.trx')
    cases=trx.findall('.//t:UnitTestResult',namespace)
    failed=[x.attrib['testName'] for x in cases if x.attrib['outcome']!='Passed']
    focused=[x for x in cases if '.ConnectedTimingTests.' in x.attrib['testName']]
    assert len(cases)==1075 and len(focused)==15 and all(x.attrib['outcome']=='Passed' for x in focused)
    assert failed==[config['KnownUnresolvedFullSuiteFailure']]==status['UnitTests']['Failed']
    candidate=read(run/'study/local/results.json')['cases']
    lookup={(c['sourceId'],c['boundaryCondition'],c['step']):c for c in candidate}
    refinement=[]
    for source in ('paired100-0','paired100-60'):
        for condition in ('connected','disconnected'):
            sequence=[lookup[(source,condition,h)] for h in (.125,.0625,.03125,.015625)]
            refinement.append(dict(source=source,condition=condition,
                stepsMs=[c['step'] for c in sequence],spikeCounts=[[len(t) for t in c['spikes']] for c in sequence],
                successiveSpikes=[dict(firstStepMs=a['step'],secondStepMs=b['step'],
                    cells=[compare_spikes(a['spikes'][i],b['spikes'][i]) for i in (0,1)]) for a,b in zip(sequence,sequence[1:])],
                contacts=[dict(stepMs=c['step'],values=[dict(source=x['state']['sourceInstance'],target=x['state']['target'],
                    weight=x['state']['weight'],peakCa=x['peakCa']) for x in c['contacts']]) for c in sequence]))
    report=dict(reviewedUtc=datetime.now(timezone.utc).isoformat(),capture=str(run),backup=str(backup),
        revision=status['Revision'],completedUtc=status['CompletedUtc'],
        verifiedFilesPerCopy=len(hashes),verifiedBytesPerCopy=sum(x['Bytes'] for x in manifest['Files']),
        packagedFilesVerified=len(config['PackageFiles']),
        olderProtection='Run recorded all 48,434 protected paths unchanged. This review rehashes its capture and D: copy only.',
        tests=dict(total=len(cases),passed=len(cases)-len(failed),failed=failed,timingPassed=len(focused)),
        stages=stage_reports,refinement=refinement,
        solverConvergenceQualified=False,physiologicalCalibrationComplete=False,physicalRecoveryAssessed=False)
    output.mkdir(parents=True)
    (output/'review.json').write_text(json.dumps(report,indent=2,allow_nan=False)+'\n',encoding='utf-8')
    print(json.dumps({k:v for k,v in report.items() if k not in ('refinement','stages')},indent=2),flush=True)


if __name__=='__main__':
    main(Path(sys.argv[1]),Path(sys.argv[2]))
