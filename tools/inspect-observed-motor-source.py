"""Read-only source provenance for the new observed-motor adapter; no models."""
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import re

ROOT=Path(__file__).resolve().parents[1]


def main():
    spec=importlib.util.spec_from_file_location('packaging',ROOT/'tools/prepare-causal-motor.py')
    common=importlib.util.module_from_spec(spec);spec.loader.exec_module(common)
    folder=ROOT/'artifacts/motor-unit-circuit-review-20261109';common.verify(folder,['review.json'])
    review=common.read(folder/'review.json');assert review['operatorReportedCompletion'] and review['savedAnalysisExactlyReproduced']
    assert review['passedFocusedTests']==88
    capture=Path(review['capture']);assert common.digest(capture/'backup-manifest.json')==review['manifestSha256']
    names=['Structures/_SharedRuntime/MotoneuronReferenceCell.cs','Structures/_SharedRuntime/MotoneuronChannelKinetics.cs',
        'tools/MotoneuronReferenceProbe/ClockAudit.cs','Protocol/MotorAxonPublisher.cs','Protocol/MotorAxonReplayBuffer.cs',
        'Protocol/MotorAxonProtocol.cs','src/NRE.SimAvatar/AvatarMotorAxonReceiver.cs']
    common.verify(capture,['source/'+name for name in names])
    checked=[]
    for name in names:
        assert (ROOT/name).read_text(encoding='utf-8-sig')==common.io(capture/'source'/name).read_text(encoding='utf-8-sig'),name
        checked.append(dict(path=name,qualifiedSha256=common.digest(capture/'source'/name),currentSha256=common.digest(ROOT/name),textExactlyEqual=True))
    code=(ROOT/'tests/NeuralResonanceEngine.DNNE.Tests/ObservedMotorSourceTests.cs').read_text()
    methods=re.findall(r'\[Fact\]\s+public void (\w+)\(',code)
    assert len(methods)==len(set(methods))==6
    report=dict(inspectedUtc=datetime.now(timezone.utc).isoformat(),qualifiedCircuitCapture=str(capture),
        qualifiedCaptureManifestSha256=review['manifestSha256'],sourceAndTransportTextPreserved=checked,
        newFocusedMethods=methods,newAdapterExecuted=False,newTestsExecuted=False,sourceReferenceSolved=False,
        nativePhysicsExecuted=False,liveBrainAccessed=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    output=ROOT/'artifacts/observed-motor-source-preparation-20261116';output.mkdir()
    (output/'source-review.json').write_text(json.dumps(report,indent=2)+'\n')
    print('Seven qualified neuronal/transport sources match; six new source-owner methods identified. No tests or models executed.')


if __name__=='__main__':main()
