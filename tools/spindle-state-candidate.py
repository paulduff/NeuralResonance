"""Declared canonical-state repair of the pinned intrafusal mechanics source.

Preparation edits source bytes; verification/audit reads saved evidence only.
Model execution belongs to the independent visible Octave/PowerShell window.
"""
from hashlib import sha256
import math
from pathlib import Path
import shutil

VERSION = 'canonical-spindle-state-v1'
BASELINE = 'spindle-reference-20261113-98c085'
STATE_ABSOLUTE = 1e-12
STRESS_ABSOLUTE_PA = 1e-7


def verify_selected_capture(capture, names, base):
    manifest = base.read(capture / 'backup-manifest.json')
    backup = Path(manifest['Destination'])
    assert base.digest(capture / 'backup-manifest.json') == base.digest(backup / 'backup-manifest.json')
    entries = {e['Path'].replace('\\', '/'): e for e in manifest['Files']}
    for name in names:
        for folder in (capture, backup):
            path = folder / name
            assert base.io(path).stat().st_size == entries[name]['Bytes']
            assert base.digest(path) == entries[name]['Sha256'], name


def transform(original):
    """Exact declared edits only; retain the original separately with its license."""
    text = original.decode('utf-8').replace('\r\n', '\n')
    edits = []

    def replace(before, after):
        nonlocal text
        assert text.count(before) == 1, before
        text = text.replace(before, after)
        edits.append(dict(before=before, after=after))

    replace('        f_bound;\n', '')
    replace('        bin_pops;           % number of heads bound in each bin\n', '')
    replace('        no_detached;        % number of heads not attached to a binding site\n', '')
    replace('    methods\n', '''    % DNNE isolated repair: all reported populations derive from y.
    % The preserved upstream source and AGPL-3.0 license accompany this copy.
    properties (Dependent, SetAccess = private)
        bin_pops
        no_detached
        f_bound
    end

    methods
        function value = get.bin_pops(obj)
            value = reshape(obj.y(2:end-2), [], 1);
        end
        function value = get.no_detached(obj)
            value = obj.y(1);
        end
        function value = get.f_bound(obj)
            value = sum(obj.y(2:end-2));
        end
''')
    replace('            obj.bin_pops = zeros(obj.no_of_x_bins,1);\n            obj.y = [1 ; obj.bin_pops; 1; 0];',
            '            obj.y = [1, zeros(1, obj.no_of_x_bins), 1, 0];')
    replace('            if (x_overlap<0)', '            if (x_overlap<=0)')
    replace('            obj.f_bound = sum(obj.bin_pops);',
            '            % f_bound is a read-only view of the authoritative y state.')
    replace('        function evolve_cbDist(obj,time_step)\n', '''        function evolve_cbDist(obj,time_step)
            obj.update_filamentOverlap();
            if ~isscalar(obj.f_overlap) || ~isfinite(obj.f_overlap) || obj.f_overlap <= 0 || obj.f_overlap > 1
                error('DNNE:SpindleOverlapDomain', 'Cooperative kinetics require 0 < overlap <= 1; zero-overlap kinetics are not specified.');
            end
''')
    replace("            obj.bin_pops = y_new(end,2:end-2)';\n            obj.no_detached = y_new(end,1);\n", '')
    replace('            % Calculate forces\n',
            '            % Refresh geometry after imposed shifts, including zero-duration ones.\n            obj.update_filamentOverlap();\n            % Calculate forces\n')
    return text.encode('utf-8'), edits


def prepare(package, base):
    root = Path(__file__).resolve().parents[1]
    reviewed = root / 'artifacts/muscle-spindle-profile-review-20261113'
    verify_selected_capture(reviewed, ['spindle-review.json'], base)
    evidence = base.read(reviewed / 'spindle-review.json')
    assert evidence['operatorReportedCompletion'] and evidence['savedAnalysisReproduced']
    assert evidence['runtimeBeforeAndAfterMatched'] and evidence['analysis']['complete']
    baseline = root / 'artifacts' / BASELINE
    assert base.digest(baseline / 'backup-manifest.json') == evidence['manifestSha256']
    labels = [f'{f}-{p}-{dt}us' for f in base.FIBERS for p in base.PROTOCOLS for dt in base.STEPS_US]
    names = ['mechanics/' + label + '.json' for label in labels]
    names += ['mechanics/' + f + '-state-boundaries.json' for f in base.FIBERS]
    names += ['mechanics/execution.json', 'mechanics/resolution.json', 'runtime-before.json', 'runtime-after.json']
    verify_selected_capture(baseline, names, base)
    reference = package / 'state-baseline'; reference.mkdir()
    shutil.copyfile(reviewed / 'spindle-review.json', reference / 'review.json')
    for name in names:
        target = reference / name; target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(baseline / name, target)
    shutil.copytree(base.io(package / 'octave-compatible/vendor'), base.io(package / 'canonical-state/vendor'))
    # Keep the applicable source license alongside each derived execution tree.
    shutil.copyfile(base.io(package / 'upstream/LICENSE'), base.io(package / 'canonical-state/LICENSE'))
    changes = []
    for fiber in ('Bag', 'Chain'):
        relative = Path('vendor/matlabMuscleSpindleModellingTools/spindle_model') / ('@halfSarcWithCoop' + fiber) / ('halfSarcWithCoop' + fiber + '.m')
        original = base.io(package / 'octave-compatible' / relative).read_bytes()
        candidate, edits = transform(original)
        base.io(package / 'canonical-state' / relative).write_bytes(candidate)
        changes.append(dict(path=relative.as_posix(), originalSha256=sha256(original).hexdigest().upper(),
            candidateSha256=sha256(candidate).hexdigest().upper(), edits=edits))
    base.write(package / 'canonical-state.json', dict(version=VERSION, changes=changes,
        baseline=BASELINE, originalSourcePreserved=True, zeroOverlapKineticsImplemented=False,
        stateAbsolute=STATE_ABSOLUTE, stressAbsolutePa=STRESS_ABSOLUTE_PA))


def verify(package, base):
    declaration = base.read(package / 'canonical-state.json')
    assert declaration['version'] == VERSION and declaration['baseline'] == BASELINE
    assert declaration['stateAbsolute'] == STATE_ABSOLUTE and declaration['stressAbsolutePa'] == STRESS_ABSOLUTE_PA
    expected = {entry['path']: entry for entry in declaration['changes']}
    assert len(expected) == 2
    original = base.io(package / 'octave-compatible/vendor'); candidate = base.io(package / 'canonical-state/vendor')
    original_files = {p.relative_to(original).as_posix(): p for p in original.rglob('*') if p.is_file()}
    candidate_files = {p.relative_to(candidate).as_posix(): p for p in candidate.rglob('*') if p.is_file()}
    assert original_files.keys() == candidate_files.keys()
    for name, path in original_files.items():
        data = path.read_bytes(); changed = candidate_files[name].read_bytes()
        if 'vendor/' + name in expected:
            entry = expected['vendor/' + name]
            actual, edits = transform(data)
            assert actual == changed and edits == entry['edits']
            assert sha256(data).hexdigest().upper() == entry['originalSha256']
            assert sha256(changed).hexdigest().upper() == entry['candidateSha256']
        else:
            assert changed == data
    assert base.io(package / 'canonical-state/LICENSE').read_bytes() == base.io(package / 'upstream/LICENSE').read_bytes()
    return len(original_files)


def analyse(package, base):
    verify(package, base)
    report = base.analyse(package)
    records = []; passed = report['complete']
    for record in report['trajectories']:
        label = record['label']
        old = base.read(package / 'state-baseline/mechanics' / (label + '.json'))
        new = base.read(package / 'mechanics' / (label + '.json'))
        assert old['parameters'] == new['parameters'] and old['dtSeconds'] == new['dtSeconds']
        assert len(old['snapshots']) == len(new['snapshots'])
        state_error = 0.; force_error = 0.; scalar_initial = False
        for before, after in zip(old['snapshots'], new['snapshots']):
            assert before['step'] == after['step'] and before['timeSeconds'] == after['timeSeconds']
            y = base.vector(after['y'])
            state_error = max(state_error, max(abs(a-b) for a, b in zip(base.vector(before['y']), y)))
            assert base.vector(after['reportedBinPopulations']) == y[1:-2]
            assert base.one(after['reportedDetached']) == y[0]
            assert abs(base.one(after['reportedBound']) - sum(y[1:-2])) <= STATE_ABSOLUTE
            assert abs(y[0] + sum(y[1:-2]) - 1) <= STATE_ABSOLUTE
            assert abs(y[-2] + y[-1] - 1) <= STATE_ABSOLUTE
            assert min(y) >= -STATE_ABSOLUTE
            assert sum(y[1:-2]) <= y[-1] + STATE_ABSOLUTE
            assert y[-1] <= base.one(after['overlap']) + STATE_ABSOLUTE
            force = base.one(after['stressPa'])
            if after['step']:
                force_error = max(force_error, abs(force - base.one(before['stressPa'])))
            else:
                expected = new['parameters']['k_passive'] * (after['hsLengthNm'] - new['parameters']['hsl_slack'])
                scalar_initial = force == expected and base.one(after['crossBridgeStressPa']) == 0
        valid = bool(state_error <= STATE_ABSOLUTE and force_error <= STRESS_ABSOLUTE_PA and scalar_initial
            and record['maximumPopulationCacheDifference'] == 0 and not record['nonScalarForceSteps'])
        records.append(dict(label=label, maximumStateDifference=state_error,
            maximumPostInitialStressDifferencePa=force_error, scalarInitialPassiveStressPassed=scalar_initial, passed=valid))
        passed &= valid
    for boundary in report['boundaries']:
        passed &= boundary['exactBoundaryFromPositive'] == boundary['exactBoundaryFromZero'] == 0
        passed &= all(s['cacheDifference'] == 0 and s['forceElements'] == 1 for s in boundary['states'].values())
    controls = base.read(package / 'state-controls.json')
    assert controls['version'] == VERSION and len(controls['fibers']) == 2
    assert {f['fiber'] for f in controls['fibers']} == set(base.FIBERS)
    for stage in ('preflight', 'post-trajectory'):
        roots = base.read(package / (stage + '-root-diagnostics.json'))
        assert roots['stage'] == stage and not roots['boundChanged'] and not roots['kineticsExecuted']
        assert {f['fiber'] for f in roots['fibers']} == set(base.FIBERS)
        for r in roots['fibers']:
            assert r['initialStatePreserved'] and abs(r['scalarGuessRootNm'] - r['slackLengthNm']) <= 1e-9
            assert len(r['trialStressesPa']) == len(r['analyticPassiveStressesPa']) == 4
            assert max(abs(a-b) for a,b in zip(r['trialStressesPa'], r['analyticPassiveStressesPa'])) <= STRESS_ABSOLUTE_PA
    for fiber in controls['fibers']:
        assert fiber['fiber'] in base.FIBERS
        assert len(fiber['readOnlyProperties']) == 3 and all(fiber['readOnlyProperties'])
        assert fiber['zeroOverlapRejected'] and fiber['invalidOverlapRejected'] and fiber['statePreservedAfterRejectedKinetics']
        assert all(math.isfinite(x) for x in base.vector(fiber['initialRootNm']))
        assert abs(fiber['initialRootNm'] - fiber['slackLengthNm']) <= 1e-9
        assert fiber['maximumTrialStressErrorPa'] <= STRESS_ABSOLUTE_PA and fiber['trialQueriesPreserveState']
        assert fiber['maximumDetachedMassError'] <= STATE_ABSOLUTE
        assert fiber['shiftedDerivedPopulationError'] == 0
    return dict(complete=True, canonicalStateChecksPassed=bool(passed), trajectories=records, boundaryControls=controls,
        sourceComparison=report, zeroOverlapKineticsImplemented=False, independentNumericalAccuracyQualified=False,
        fullSlackDriverExecuted=False, afferentMembraneExecuted=False, liveBodyIntegrated=False,
        physiologyQualified=False, avatarRecoveryAssessed=False)
