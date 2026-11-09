"""Record a pinned, unexecuted upstream source inspection, not a model experiment."""
from datetime import datetime, timezone
from hashlib import sha256
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REVISION = 'da0ed89078a948167b4e2b511480787ddb681892'
CAPTURE = ROOT/'artifacts/spindle-source-20261109'
SOURCE = CAPTURE/'upstream'/('MechanisticSpindleManuscript-'+REVISION)


def anchor(file, text):
    lines = (SOURCE/file).read_text(encoding='utf-8-sig').splitlines()
    found = [i+1 for i, line in enumerate(lines) if text in line]
    assert found, (file, text)
    return dict(file=file, lines=found, sourceText=text)


def main():
    assert sha256((CAPTURE/'upstream.zip').read_bytes()).hexdigest().upper() == 'F72A8E59744BBEB44AF712C2B3FD5617560FC6FBD2D39535ABD4352221A5D11D'
    files = [dict(path=p.relative_to(SOURCE).as_posix(), bytes=p.stat().st_size,
                  sha256=sha256(p.read_bytes()).hexdigest().upper()) for p in sorted(SOURCE.rglob('*')) if p.is_file()]
    assert len(files) == 16
    evidence = [anchor('lib/sarc2spindle.m', 'Y = diff(Fd)./diff(t)'),
                anchor('lib/sarc2spindle.m', 'Y(end+1) = Y(end)'),
                anchor('lib/find_hsl_from_force.m', 'pF = obj.k_passive * (obj.hs_length - obj.hsl_slack)'),
                anchor('lib/sarcSimDriver.m', 'xB = find_hsl_from_force(hsB,0.0)'),
                anchor('run_Fig9.m', 'v(end) = v_reset')]
    for name in ('Bag', 'Chain'):
        file = '@halfSarc'+name+'/halfSarc'+name+'.m'
        for snippet in ('obj.no_detached = max([0 obj.f_activated-obj.f_bound])',
                        'dy(2:n) = (obj.f * obj.no_detached)',
                        'obj.no_detached = obj.no_detached',
                        'obj.bin_pops = zeros(obj.no_of_x_bins,1)',
                        'obj.update_fracBound()', 'obj.evolve_cbDist(time_step)'):
            evidence.append(anchor(file, snippet))
    report = dict(capturedUtc=datetime.now(timezone.utc).isoformat(), revision=REVISION,
                  repository='https://github.com/kylepblum/MechanisticSpindleManuscript',
                  article='https://elifesciences.org/articles/55177',
                  archiveSha256=sha256((CAPTURE/'upstream.zip').read_bytes()).hexdigest().upper(),
                  files=files, inspectedAnchors=evidence, codeExecuted=False, modelSolved=False,
                  sourceModified=False, adoptedInRuntime=False,
                  sourceLicenseFile='LICENSE',
                  observations=[
                      'Snapshot contains mechanics and rate conversion, plus a threshold-reset example; no Simulink .mdl/.slx file.',
                      'Rate conversion uses next-minus-current force and repeats final slope; a live causal port needs explicit interval timing.',
                      'Cross-bridge RHS uses cached detached amount rather than solver first state; final detached state is not copied back.',
                      'Slack root varies the shifted cross-bridge distribution but holds passive stress at the old length.',
                      'At constructor zero attachment, that root objective is a positive constant at the declared initial lengths; no finite root exists for target zero in that initial state.',
                      'These are source findings, not measured trajectory failures or proof about other author versions/data.'
                  ])
    with (CAPTURE/'source-audit.json').open('x', encoding='utf-8') as stream:
        stream.write(json.dumps(report, indent=2)+'\n')
    print(json.dumps(dict(capture=str(CAPTURE), sourceFiles=len(files), codeExecuted=False), indent=2))


if __name__ == '__main__': main()
