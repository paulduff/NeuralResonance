"""Read-only review of the operator-reported cortical diagnostic, not a simulation."""
from collections import Counter
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timezone
import importlib.util
import json
import math
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
lines=lambda p:[json.loads(s) for s in p.read_text(encoding='utf-8-sig').splitlines()]


def main(run,out):
    run=run.resolve();out=out.resolve();assert not out.exists() and run not in out.parents and run!=out
    # Reuse only the complete-file hashing helpers, never the other study's main.
    spec=importlib.util.spec_from_file_location('capture_hashing',Path(__file__).with_name('review-isometric-plant.py'))
    hashing=importlib.util.module_from_spec(spec);spec.loader.exec_module(hashing)
    status=read(run/'window-status.json');config=read(run/'configuration.json');manifest=read(run/'backup-manifest.json')
    backup=Path(config['BackupDestination']).resolve()
    assert run==Path(manifest['Source']).resolve() and backup==Path(manifest['Destination']).resolve()
    assert status['Status']=='DIAGNOSTICS_COMPLETE' and status['ExitCode']==0 and status['DiagnosticCaptureComplete']
    assert status['Revision']==config['Revision']=='2b89c98a5d5e37ea710a618bc9d3a8149cd7503d'
    assert not any(status[k] for k in ('LiveBrainAccessed','OldSavesLoaded','BiologicalConsolidationQualified','FullUnitSuitePassed','OriginalConsolidationTestPassed'))
    assert status['OriginalTestProcessExitCode']==1 and config['OriginalConsolidationTestUnchanged']
    assert hashing.digest(run/'backup-manifest.json')==hashing.digest(backup/'backup-manifest.json')
    with ThreadPoolExecutor(max_workers=2) as pool:
        left=pool.submit(hashing.audit,run,manifest);right=pool.submit(hashing.audit,backup,manifest)
        hashes=left.result();assert hashes==right.result()
    for item in config['PackageFiles']:assert hashes[Path(item['Path']).as_posix().lower()]==item['Sha256'].upper()
    for name in ('test-binaries.json','probe-binaries.json'):
        for item in read(run/name):
            assert hashes[Path(item['Path']).resolve().relative_to(run).as_posix().lower()]==item['Sha256'].upper()
    ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    tests=ET.parse(run/'test-results/memory-fixture.trx').findall('.//t:UnitTestResult',ns)
    actual={t.attrib['testName']:t.attrib['outcome'] for t in tests}
    assert actual=={
        'NeuralResonanceEngine.DNNE.Tests.SolutionManifestTests.Solution_Manifest_Includes_Every_Project_In_The_Repository':'Passed',
        'NeuralResonanceEngine.DNNE.Tests.NeuronalMemoryTests.RepeatedCorticalExperienceGraduallyRaisesConsolidation':'Failed'} and len(tests)==2
    failed=next(t for t in tests if t.attrib['outcome']=='Failed')
    assert failed.find('t:Output/t:ErrorInfo/t:Message',ns).text==status['OriginalConsolidationFailure']
    analysis=read(run/'study/analysis.json');protocol=read(run/'study/protocol.json');results=read(run/'study/results.json')
    names=['legacy-observed','legacy-unobserved','continuous-simultaneous','continuous-spaced','continuous-unmodulated']
    assert analysis['completed'] and analysis['captureIntegrityPassed'] and analysis['observationInvariancePassed']
    assert not analysis['biologicalConsolidationQualified'] and results['completed']
    assert [c['scenario'] for c in analysis['cases']]==names==[c['key'] for c in protocol['scenarios']]==[c['key'] for c in results['cases']]
    cases=[];captures={}
    for declared,saved in zip(protocol['scenarios'],analysis['cases']):
        folder=run/'study'/declared['key'];ticks=lines(folder/'ticks.jsonl');events=lines(folder/'learning.jsonl')
        inbound=lines(folder/'inbound.jsonl');inputs=lines(folder/'inputs.jsonl')
        assert len(ticks)==len(inputs)==18 and len(events)==saved['learningEvents'] and len(inbound)==saved['inboundEvents']
        assert [t['signal']['timestampMs'] for t in ticks]==[55*r+dt for r in range(9) for dt in (25,55)]
        assert sum(t['signal']['tickDurationMs'] for t in ticks)==saved['declaredIntegratedMilliseconds']
        final=ticks[-1]['contacts'];cue=next(c for c in final if c['sourceNeuron']=='memory-feature-140')
        selected=[e for e in events if e['after']['contactId']==cue['contactId']]
        if declared['observe']:
            assert selected and all(e['before']['target']==e['after']['target']==cue['target'] for e in selected)
            net=Counter()
            for event in events:net[event['after']['contactId']]+=event['after']['weight']-event['before']['weight']
            assert all(abs(net[c['contactId']]-(c['weight']-c['baseline']))<=1e-7 for c in final)
        else:assert not events and not inbound
        injected=[s for row in inputs for s in row['spikes']];by_id={s['messageId']:s for s in injected}
        assert len(by_id)==len(injected)==saved['injectedEvents']
        if declared['observe']:
            seen=[e for e in events if e['kind']=='pre' and e['messageId'] in by_id]
            assert Counter(e['messageId'] for e in seen)==Counter(by_id.keys())
        # Independent reconstruction of the ensemble score from final contacts.
        ensemble=cue['target']//4%8
        supported=[c for c in final if c['target']//4%8==ensemble and c['neurotransmitter']==cue['neurotransmitter'] and c['updates']>0]
        def contribution(c):
            return max(0,c['weight']-c['baseline'])/max(.05,5-c['baseline'])*(.3+.7*min(1,math.log2(c['updates']+1)/6))
        score=sum(map(contribution,supported))/len(supported)
        assert abs(score-saved['cues'][-1]['consolidationScore'])<=1e-6
        modulators=[c for c in final if c['neurotransmitter'] in (3,5,6)]
        assert len(modulators)==(126 if declared['modulation'] else 0)
        change_by_kind={kind:sum(e['after']['weight']-e['before']['weight'] for e in selected if e['kind']==kind)
                        for kind in ('pre','post','modulation')}
        row=dict(case=declared['key'],cueTarget=cue['target'],modulatorContactTargets=dict(Counter(c['target'] for c in modulators)),
            cueLearningEvents=dict(Counter(e['kind'] for e in selected)),cueWeightChangeByKind=change_by_kind,
            maximumCueDopamine=max((e['dopamine'] for e in selected),default=None),
            maximumCueAcetylcholine=max((e['acetylcholine'] for e in selected),default=None),
            firstCueWeight=saved['cues'][0]['weight'],finalCueWeight=cue['weight'],
            firstEnsembleScore=saved['cues'][0]['consolidationScore'],finalEnsembleScore=saved['cues'][-1]['consolidationScore'],
            supportingContacts=[saved['cues'][0]['supportingSynapses'],len(supported)],
            maximumDeliveryLatenessMs=saved['maximumDeliveryLatenessMs'],integratedMs=saved['declaredIntegratedMilliseconds'],
            otherInboundEvents=saved['otherInboundEvents'],originalTestRandomIdentitiesReplayed=False)
        cases.append(row);captures[declared['key']]=ticks
    for a,b in zip(captures['legacy-observed'],captures['legacy-unobserved']):
        assert a['signal']==b['signal'] and a['ack']==b['ack'] and a['contacts']==b['contacts']
        scrub=lambda items:[{k:v for k,v in item.items() if k!='messageId'} for item in items]
        assert scrub(a['emitted'])==scrub(b['emitted'])
    report=dict(reviewedUtc=datetime.now(timezone.utc).isoformat(),capture=str(run),backup=str(backup),sourceRevision=config['Revision'],
        completedUtc=status['CompletedUtc'],verifiedFilesPerCopy=len(hashes),verifiedBytesPerCopy=sum(x['Bytes'] for x in manifest['Files']),
        packagedFilesVerified=len(config['PackageFiles']),unitOutcomes=actual,originalConsolidationFailure=status['OriginalConsolidationFailure'],
        observationInvariancePassed=True,cases=cases,biologicalConsolidationQualified=False,originalTestRepaired=False,
        fullUnitSuitePassed=False,liveBrainAccessed=False,avatarRecoveryAssessed=False)
    out.mkdir(parents=True);(out/'review.json').write_text(json.dumps(report,indent=2,allow_nan=False)+'\n')
    print(json.dumps(report,indent=2),flush=True)


if __name__=='__main__':main(Path(sys.argv[1]),Path(sys.argv[2]))
