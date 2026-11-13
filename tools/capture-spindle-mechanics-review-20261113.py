"""Hash and anchor an unexecuted source review; never run upstream model code."""
import argparse
from datetime import datetime, timezone
from hashlib import sha256
import json
import os
from pathlib import Path, PurePosixPath
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[1]
OLD = 'spindle-mechanics-source-20261109'
NEW = 'spindle-2026-source-20261113'
PINNED = {
    'Neuromechanics-Lab/Simha-Ting2023JExptPhysiol': (
        'e82b0d05509283a3913fafec335d5451a4b7eb34',
        '888B6B68D8C1585BAC236C37BE2627491B255C6DE39306D946E5F2E81CFE035F'),
    'Campbell-Muscle-Lab/MATMyoSim': (
        '18bf2e30c6998e7bfa27169bc6ce1ca5551f7278',
        '08D8CD0DE3065679B39819BC403806495BA868BE2F99DCB794B10CF5A50D8E87'),
    'SurabhiSimha/MATMyoSim': (
        '62fa024e15ecd53b77da34a6346be8d706348090',
        '6406AA1FC824CA19ACA22DCF0D008C0D21E4D7654EB4E040D6BB284716E024D7'),
    'Neuromechanics-Lab/spindleModelTutorial': (
        '721ba03cb775261548766ed1349c4bf2e0e35385',
        '050F71D846360410E4E917B74FB7A2F3E497C16E2107F0F53552C069D8869444'),
}


def io_path(path):
    path = Path(path).resolve()
    if os.name == 'nt' and not str(path).startswith('\\\\?\\'):
        return Path('\\\\?\\' + str(path))
    return path


def digest(data):
    return sha256(data).hexdigest().upper()


def require(condition, message):
    if not condition:
        raise ValueError(message)


def relative(name):
    p = PurePosixPath(name)
    require(not p.is_absolute() and '..' not in p.parts and ':' not in name
            and '\\' not in name, 'Unsafe archived path: ' + name)
    return p


def verify_snapshot(capture_name, metadata):
    capture = io_path(ROOT / 'artifacts' / capture_name)
    revision, archive_hash = PINNED[metadata['repository']]
    require((metadata['revision'], metadata['archiveSha256']) == (revision, archive_hash),
            'Snapshot pin changed')
    archive = capture / relative(metadata['archive'])
    require(digest(archive.read_bytes()) == archive_hash, 'Archive bytes changed')
    listed = {f['path']: f for f in metadata['files']}
    require(len(listed) == len(metadata['files']), 'Duplicate metadata entry')
    files = []
    with ZipFile(archive) as z:
        entries = [e for e in z.infolist() if not e.is_dir()]
        require(len(entries) == len(listed) and {e.filename for e in entries} == set(listed),
                'Archive and metadata file inventories differ')
        for entry in sorted(entries, key=lambda e: e.filename):
            path = relative(entry.filename)
            require((entry.external_attr >> 16) & 0o170000 != 0o120000,
                    'Archive contains a symlink')
            expected = z.read(entry)
            actual = (capture / 'upstream' / path).read_bytes()
            require(actual == expected and len(actual) == listed[entry.filename]['bytes'],
                    'Extracted file differs: ' + entry.filename)
            h = digest(actual)
            require('sha256' not in listed[entry.filename] or listed[entry.filename]['sha256'] == h,
                    'Metadata hash differs: ' + entry.filename)
            files.append(dict(path=entry.filename, bytes=len(actual), sha256=h))
    root_name = PurePosixPath(files[0]['path']).parts[0]
    require(all(PurePosixPath(f['path']).parts[0] == root_name for f in files),
            'Multiple extracted roots')
    extracted = capture / 'upstream' / root_name
    actual_paths = {p.relative_to(capture / 'upstream').as_posix()
                    for p in extracted.rglob('*') if p.is_file()}
    require(actual_paths == set(listed), 'Extracted tree has missing or extra files')
    return dict(capture=capture_name, repository=metadata['repository'], revision=revision,
                archive=metadata['archive'], archiveSha256=archive_hash, files=files), extracted


def main(output):
    old_capture = io_path(ROOT / 'artifacts' / OLD)
    metadata = json.loads((old_capture / 'sources.json').read_text())
    metadata.append(json.loads((old_capture / 'author-dependency.json').read_text()))
    snapshots, roots = [], {}
    for capture_name, records in [(OLD, metadata), (NEW, [json.loads(
            io_path(ROOT / 'artifacts' / NEW / 'source.json').read_text())])]:
        for record in records:
            snapshot, source = verify_snapshot(capture_name, record)
            snapshots.append(snapshot)
            roots[record['repository']] = source

    observations = []

    def finding(identifier, observation, references):
        anchors = []
        for repository, file, snippet in references:
            p = roots[repository] / file
            lines = p.read_text(encoding='utf-8-sig').splitlines()
            matches = [n for n, line in enumerate(lines, 1) if snippet in line]
            require(bool(matches), 'Missing review anchor: ' + file + ': ' + snippet)
            anchors.append(dict(repository=repository, file=file, sha256=digest(p.read_bytes()),
                                lines=matches, sourceText=snippet))
        observations.append(dict(id=identifier, observation=observation, anchors=anchors))

    old = 'Neuromechanics-Lab/Simha-Ting2023JExptPhysiol'
    new = 'Neuromechanics-Lab/spindleModelTutorial'
    spindle = 'vendor/matlabMuscleSpindleModellingTools/spindle_model/'
    finding('2023-dependency-scheme', 'The model declares 2state; the linked upstream kinetics dispatcher uses 2state_with_poly, while the author fork accepts 2state. This is not an execution result.', [
        (old, 'sim_input/Models/2StateBag.json', '"kinetic_scheme": "2state"'),
        ('Campbell-Muscle-Lab/MATMyoSim', 'code/@half_sarcomere/evolve_kinetics.m', "case '2state_with_poly'"),
        ('SurabhiSimha/MATMyoSim', 'code/@half_sarcomere/evolve_kinetics.m', "case '2state'")])
    finding('2023-mutable-input', 'Both shipped templates set k_off=0. simulateSarc saves back into the input template. Protocol branches set different subsets of parameters, allowing state from an earlier call to survive.', [
        (old, 'sim_input/Models/2StateBag.json', '"k_off": 0'),
        (old, 'sim_input/Models/2StateChain.json', '"k_off": 0'),
        (old, 'simulateSarc.m', 'base_model = loadjson(model_file)'),
        (old, 'simulateSarc.m', 'base_model.MyoSim_model.hs_props.parameters.k_off = 0'),
        (old, 'simulateSarc.m', "savejson('MyoSim_model'")])
    finding('2026-provenance', 'The tutorial retains its AGPL-3.0 license and declares 17 vendored dependency files. Vendored revisions are abbreviated in its metadata; this review pins the enclosing archive, not an inferred full dependency revision.', [
        (new, 'LICENSE', 'GNU AFFERO GENERAL PUBLIC LICENSE'),
        (new, 'vendor/VENDOR_INFO.txt', 'matlabMuscleSpindleModellingTools : ba39645'),
        (new, 'vendor/VENDOR_INFO.txt', 'gammaDriveOptimization            : 1107951')])
    finding('2026-path-selection', 'The path helper prefers environment overrides or sibling repositories. Reproduction must explicitly select and hash the vendored functions.', [
        (new, 'setupTutorialPaths.m', "getenv('SPINDLE_TOOLBOX_DIR')"),
        (new, 'setupTutorialPaths.m', "getenv('GAMMA_OPT_DIR')"),
        (new, 'setupTutorialPaths.m', "toolboxDir = fullfile(vendorRoot")])
    for cell, off_rate in [('Bag', 20), ('Chain', 200)]:
        file = spindle + f'@halfSarcWithCoop{cell}/halfSarcWithCoop{cell}.m'
        finding('2026-' + cell.lower() + '-state', 'The RHS uses the solver detached population and saves final y. Actin deactivation is nonzero. Unlike the 2020 source, these particular cached-detachment issues are absent. This does not establish trajectory accuracy.', [
            (new, file, 'f_myosin_detached = y(1)'),
            (new, file, 'obj.y=y_new(end,:)'),
            (new, file, f'J_off = {off_rate} *')])
        finding('2026-' + cell.lower() + '-boundary', 'The overlap branches omit x_overlap==0; cooperative fluxes divide by overlap. Exact-zero and no-overlap cases need explicit diagnostics before extending the domain.', [
            (new, file, 'if (x_overlap<0)'), (new, file, 'if ((x_overlap>0)'),
            (new, file, 'f_actin_on_unbound/obj.f_overlap')])
        finding('2026-' + cell.lower() + '-split-state', 'Shifting mutates y but does not synchronize bin_pops/no_detached there. Force uses y; the slack solver and reported populations read bin_pops. Snapshot consistency needs a focused reproduction.', [
            (new, file, "obj.bin_pops = y_new(end,2:end-2)'"),
            (new, file, 'obj.y(cb_indices) = interp1'),
            (new, file, 'obj.y(1) = obj.y(1) + cbs_lost'),
            (new, spindle + 'find_hsl_from_force_spindle20250627.m', 'griddedInterpolant(obj.x_bins, obj.bin_pops'),
            (new, spindle + 'sarcSimDriverIntrafusal20250627.m', f'data{cell[0]}(1).bin_pops(:,i) = hs{cell[0]}.bin_pops')])
    finding('2026-slack-root', 'Passive stress now uses the trial length. The specific old constant-root problem at zero attachment is removed in this source; full slack trajectories remain unexecuted.', [
        (new, spindle + 'find_hsl_from_force_spindle20250627.m', '((obj.hs_length + x) - obj.hsl_slack)')])
    finding('2026-slack-flags', 'The bag/chain slack checks form an if/elseif chain, leaving the other flag unchanged whenever either condition is taken. Independent transitions need reproduction.', [
        (new, spindle + 'sarcSimDriverIntrafusal20250627.m', 'if hsB.hs_force <= 1e3'),
        (new, spindle + 'sarcSimDriverIntrafusal20250627.m', 'elseif hsC.hs_force <= 1e3')])
    finding('2026-time-labels', 'The driver evolves one full step for each supplied timestamp, including the first t=0 sample, then assigns the supplied t vector to outputs. The tutorial forces the first ten samples to pCa=9, so onset duration changes under timestep refinement.', [
        (new, 'compute/tutorialForwardSim.m', 't  = 0:dt:p.sim.tEnd'),
        (new, spindle + 'sarcSimDriverIntrafusal20250627.m', 'for i=1:numel(t)'),
        (new, spindle + 'sarcSimDriverIntrafusal20250627.m', 'hsB.forwardStep(time_step,delta_hslB'),
        (new, spindle + 'sarcSimDriverIntrafusal20250627.m', 'dataB(1).t = t'),
        (new, 'compute/makeGammaDrive.m', 'sarcB.pCa(1:min(10, n)) = pCaCeil')])
    trans = spindle + 'sarc2spindle_20240310.m'
    finding('2026-offline-transduction', 'Force filtering uses both forward and reverse time. The nominal 300 cutoff passed as cutoff/N/2 corresponds to a 75 Hz single-pass design under MATLAB Nyquist normalization; filtfilt squares the response. Yank now aligns to interval endpoints except its first value repeats the first forward interval. Whole-trace processing is not a causal live receptor.', [
        (new, trans, 'cutoff = 300'), (new, trans, "butter(2,cutoff/N/2,'low')"),
        (new, trans, 'filtfilt(b,a,Fd)'), (new, trans, 'Y = [Y(1) Y]')])
    finding('2026-input-validation', 'Transduction drops NaN force indices, adopts chain timestamps without comparing bag times, and assumes a uniform grid for filtering. Live input must reject invalid/misaligned data explicitly.', [
        (new, trans, 'commonIndices = intersect'), (new, trans, 't1 = dataC.t'),
        (new, trans, 'N = 1/(dataC.t(2)-dataC.t(1))')])
    finding('2026-spike-conversion', 'The shipped spike generator integrates an offset-removed signal, resets at a fixed threshold, counts refractory samples and returns first IFR=Inf. The wrapper removes that first event along with its undefined IFR. This is not a sourced conductance membrane, and display filtering must not delete a live axonal event.', [
        (new, spindle + 'integrateAndFire_v2.m', 'threshold = 0.005'),
        (new, spindle + 'integrateAndFire_v2.m', 'refracPeriod > refracPeriod_threshold'),
        (new, spindle + 'integrateAndFire_v2.m', 'resetInd = i'),
        (new, spindle + 'integrateAndFire_v2.m', 'ISI = [0 diff'),
        (new, 'compute/tutorialForwardSim.m', 'ok = isfinite(IFR); t_firing = t_firing(ok)')])
    finding('2026-fit-scope', 'The tutorial documents receptor-potential fitting, self-generated targets and fixed true burst timing. A successful tutorial fit must not be called sensory-neuron calibration or identified biological gamma drive.', [
        (new, 'docs/07_differences_from_manuscript.md', 'Both now fit the model\'s **receptor potential**'),
        (new, 'docs/07_differences_from_manuscript.md', 'the fit starts with information it would not have on real data')])

    report = dict(capturedUtc=datetime.now(timezone.utc).isoformat(),
                  method='Static source and archive-byte inspection only', snapshots=snapshots,
                  observations=observations, upstreamCodeExecuted=False, modelSolved=False,
                  sourcesModified=False, adoptedInRuntime=False,
                  nextCandidate='Pinned 2026 intrafusal mechanics, isolated reproduction only',
                  conductanceAfferentQualified=False, liveSensoryPathQualified=False,
                  references=['https://www.mathworks.com/help/signal/ref/butter.html',
                              'https://www.mathworks.com/help/signal/ref/filtfilt.html'],
                  auditToolSha256=digest(Path(__file__).read_bytes()))
    output = io_path(output)
    output.mkdir(parents=True, exist_ok=False)
    (output / 'source-review.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(dict(output=str(output), snapshots=len(snapshots),
                          files=sum(len(s['files']) for s in snapshots),
                          observations=len(observations), upstreamCodeExecuted=False)))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    main(parser.parse_args().output)
