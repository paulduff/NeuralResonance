"""Check saved timing and exact accuracy evidence; never execute a neuronal model."""
import argparse
from datetime import datetime
import gzip
from hashlib import sha256
import json
import math
from pathlib import Path
from statistics import median
import sys

from olive_host_timing_protocol import COUNTS, STEPS, MODES, VERSIONS, protocol, schedule

read = lambda p: json.loads(p.read_text(encoding='utf-8-sig'))
digest = lambda p: sha256(p.read_bytes()).hexdigest().upper()


def environment():
    paths = {Path(sys.executable).resolve(), Path(sys._base_executable).resolve()}
    paths.update(Path(sys.base_prefix).glob('python*.dll'))
    return dict(python=sys.version, interpreterFiles=[dict(path=str(p), sha256=digest(p)) for p in sorted(paths)])


def equal_bytes(actual, recorded, size):
    total = 0
    checksum = sha256()
    with gzip.open(actual, 'rb') as left, gzip.open(recorded, 'rb') as right:
        while True:
            a, b = left.read(1024*1024), right.read(1024*1024)
            assert a == b, 'Recorded accuracy bytes differ: '+str(actual)
            if not a: break
            checksum.update(a)
            total += len(a)
    assert total == size, 'Incomplete recorded population or trace.'
    return dict(bytes=total, sha256=checksum.hexdigest().upper())


def audit(package):
    specification = protocol()
    assert read(package/'host-timing-protocol.json') == specification
    assert read(package/'schedule.json') == read(package/'completed-processes.json') == schedule()
    shape = read(package/'schedule-check.json')
    assert shape['DirectCount'] == shape['ValidatedEntries'] == 72 and shape['ExactDeclaredOrderPassed']
    root = package/'study'
    current_device = read(root/'controls/reference/device.json')
    old_device = read(package/'timing-evidence/device.json')
    compiler = read(package/'timing-evidence/compiler.json')
    assert current_device['Architecture'] == old_device['Architecture']
    assert digest(package/'timing-evidence/olivary.ptx') == compiler['ptxSha256']
    assert compiler['Options'] == [f"--gpu-architecture=compute_{old_device['Architecture']}",
        '--std=c++17', '--fmad=false', '--ftz=false', '--prec-div=true', '--prec-sqrt=true']
    # Interpreter identity is enforced within the executed window. A later
    # read-only audit compares recorded evidence, without pretending to recreate
    # that interpreter merely because it reproduces the analysis.
    assert read(package/'analysis-environment.json') == read(package/'analysis-final-environment.json')
    common_host = None
    def host_identity(folder, mode):
        nonlocal common_host
        host = read(folder/'host.json')
        assert host['mode'] == mode and host['version'] == VERSIONS[mode]
        assert not host['costHooksPresent'] and not host['tieredCompilation'] and host['IsHighResolution']
        assert host['stopwatchFrequency'] > 0
        assert [r['name'] for r in host['runtimeFiles']] == ['System.Private.CoreLib.dll','coreclr.dll','clrjit.dll','hostpolicy.dll']
        invariant = {k:v for k,v in host.items() if k not in ('mode', 'version')}
        if common_host is None: common_host = invariant
        assert invariant == common_host, 'Runtime or timing environment differs between processes.'
        assert read(folder/'device.json') == current_device, 'Device/driver changed within the comparison.'
        assert read(folder/'memory-after.json')['ownedBufferBytes'] == 0
        return host['stopwatchFrequency']
    controls = {}
    for mode in MODES:
        folder = root/'controls'/mode
        host_identity(folder, mode)
        assert read(folder/'result.json') == dict(completed=True, mode=mode, controls=True, contextDisposed=True)
        for name, count in [('controls.json',6), ('host-controls.json',6)]:
            rows = read(folder/name)
            assert len(rows) == count and all(r['passed'] for r in rows)
            assert rows == read(package/'accuracy-controls'/mode/name)
            if mode == 'reference': controls[name] = rows
            else: assert rows == controls[name]
    cases = {f'cells-{c}-h-{s}':dict(count=c,step=s,rows=[],traceHashes=None) for c in COUNTS for s in STEPS}
    previous_end = None
    for entry in schedule():
        mode, count, step, round_id = (entry[k] for k in ('mode','count','step','round'))
        key = f'cells-{count}-h-{step}'
        case = cases[key]; folder = root/entry['path']
        frequency = host_identity(folder, mode)
        assert read(folder/'result.json') == dict(completed=True, mode=mode, count=count, step=step,
            round=round_id, trials=2, contextDisposed=True, liveBrainAccessed=False, oldSavesLoaded=False,
            physiologyQualified=False, avatarRecoveryAssessed=False)
        design = read(folder/'design.json')
        assert (design['mode'],design['count'],design['step'],design['round'],design['startFrame'],design['frames'],design['prototypes']) == (mode,count,step,round_id,256,128,192)
        assert design['alternateRequestOrder'] and math.isfinite(design['requestPreparationMs']) and design['requestPreparationMs'] >= 0
        baseline = package/'cpu-baseline'/f'cells-192-h-{step}'
        assert design['referenceSha256'] == digest(baseline/'reference.bin')
        original = read(baseline/'design.json')
        seeds = original.get('seeds', original.get('Seeds'))
        assert len(design['seeds']) == count and len(seeds) == 192
        for lane, seed in enumerate(design['seeds']):
            identity = seeds[lane%192]['Identity']
            assert seed == dict(lane=lane,prototype=lane%192,Identity=dict(
                Instance=identity['Instance']+f'/copy-{lane//192:04}',Cell=identity['Cell']))
        for trial in ('warmup','measured'):
            row = read(folder/(trial+'.json'))
            assert (row['mode'],row['count'],row['step'],row['round'],row['measured'],row['completedFrames']) == (mode,count,step,round_id,trial=='measured',128)
            assert row['recordedBytesMatch'] and row['initialStateChecked'] and row['identityOrderChecked']
            assert isinstance(row['elapsedTicks'], int) and row['elapsedTicks'] > 0
            assert row['elapsedMs'] == row['elapsedTicks']*1000./frequency
            assert all(math.isfinite(row[k]) and row[k] >= 0 for k in ('elapsedMs','setupMs','processCpuMs','allocatedBytes'))
            assert len(row['gcCollections']) == 3 and all(isinstance(n,int) and n >= 0 for n in row['gcCollections'])
            begin, end = (datetime.fromisoformat(row[k]) for k in ('beginUtc','endUtc'))
            assert end >= begin and (previous_end is None or begin >= previous_end)
            previous_end = end
            assert row['bufferBytes'] == row['memoryDuring']['ownedBufferBytes'] == (47*count+80)*8
            assert 0 <= row['memoryDuring']['freeBytes'] <= row['memoryDuring']['totalBytes']
            hashes = {}
            for name, size in [('reports',count*128*48), ('final',count*152)]:
                actual = folder/(trial+'-'+name+'.bin.gz')
                assert digest(actual) == row[name+'Sha256']
                hashes[name] = equal_bytes(actual, package/'recorded'/key/('measured-'+name+'.bin.gz'), size)
            if case['traceHashes'] is None: case['traceHashes'] = hashes
            assert hashes == case['traceHashes']
            case['rows'].append(dict(mode=mode,round=round_id,trial=trial,elapsedMs=row['elapsedMs'],
                setupMs=row['setupMs'],processCpuMs=row['processCpuMs'],allocatedBytes=row['allocatedBytes'],
                gcCollections=row['gcCollections']))
    summaries = []
    for key, case in cases.items():
        measured = {mode:[r for r in case['rows'] if r['trial']=='measured' and r['mode']==mode] for mode in MODES}
        assert len(case['rows']) == 24 and all([r['round'] for r in measured[m]] == list(range(6)) for m in MODES)
        medians = {m:median(r['elapsedMs'] for r in measured[m]) for m in MODES}
        ratios = [c['elapsedMs']/r['elapsedMs'] for r,c in zip(measured['reference'],measured['candidate'])]
        saving = 1-medians['candidate']/medians['reference']
        faster = sum(r < 1 for r in ratios)
        summaries.append(dict(key=key,count=case['count'],step=case['step'],accuracyPassed=True,
            traceHashes=case['traceHashes'],medianMs=medians,pairedCandidateToReferenceRatios=ratios,
            medianSpeedup=medians['reference']/medians['candidate'],medianTimeSavingFraction=saving,
            fasterPairs=faster,materialConsistentBenefit=saving >= specification['practicalTimeSavingFraction'] and faster >= specification['minimumFasterPairs'],
            medianAllocatedBytes={m:median(r['allocatedBytes'] for r in measured[m]) for m in MODES},
            rows=case['rows']))
    return dict(completed=True,accuracyPassed=True,processes=72,trials=144,warmups=72,measuredTrials=72,
        controlGroups=24,exactAcrossVariantsRoundsAndWarmups=True,contextCleanupReported=True,
        currentDevice=current_device,deviceMetadataChangedSinceOriginalAccuracy=current_device!=old_device,
        runtime=common_host,cases=summaries,benefitIsDescriptive=True,
        wholeBrainThroughputMeasured=False,physiologyQualified=False,avatarRecoveryAssessed=False)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('package', type=Path)
    parser.add_argument('--preflight', action='store_true')
    args = parser.parse_args(); package = args.package.resolve()
    if args.preflight:
        assert read(package/'host-timing-protocol.json') == protocol() and read(package/'schedule.json') == schedule()
        with (package/'analysis-environment.json').open('x', encoding='utf-8') as stream:
            stream.write(json.dumps(environment(), indent=2)+'\n')
        print('Declared schedule and interpreter fingerprint recorded; no model executed.')
    else:
        current = environment()
        with (package/'analysis-final-environment.json').open('x', encoding='utf-8') as stream:
            stream.write(json.dumps(current, indent=2)+'\n')
        assert current == read(package/'analysis-environment.json'), 'Analysis interpreter changed during the comparison.'
        result = audit(package)
        assert environment() == current
        with (package/'host-timing-analysis.json').open('x', encoding='utf-8') as stream:
            stream.write(json.dumps(result, indent=2, allow_nan=False)+'\n')
        for case in result['cases']:
            print(f"{case['key']}: reference {case['medianMs']['reference']:.3f} ms, candidate {case['medianMs']['candidate']:.3f} ms; material consistent benefit {case['materialConsistentBenefit']}")
        print('Matched timing and exact accuracy checks complete. Whole-brain speed and physiology remain unqualified.')
