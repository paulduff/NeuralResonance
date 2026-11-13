"""Freeze the inspected muscle reference and source findings; execute no model."""
from datetime import datetime, timezone
from hashlib import sha256
from io import BytesIO
import json
from pathlib import Path
import subprocess
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[1]
REVISION = 'c317be803f01965a1ca8fc4cbad1ba9156433af8'
MAT_HASH = '01123aec76d1acc18b306ae1bd9daabfd29fe9287b54212aef5db3f15f719ebd'


def main():
    from scipy.io import loadmat  # Data decoding only, no numerical integration.
    reference = ROOT / 'artifacts/reference-muscle-nagamori-20261106'
    assert subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=reference, text=True).strip() == REVISION
    assert not subprocess.check_output(['git', 'status', '--porcelain'], cwd=reference, text=True).strip()
    archive = subprocess.check_output(['git', 'archive', '--format=zip', REVISION], cwd=reference)
    entries = []
    with ZipFile(BytesIO(archive)) as frozen:
        assert frozen.comment.decode().strip() == REVISION
        for entry in frozen.infolist():
            if not entry.is_dir():
                data = frozen.read(entry)
                assert data == (reference / entry.filename).read_bytes()
                entries.append(dict(path=entry.filename, bytes=len(data), sha256=sha256(data).hexdigest()))
    parameter_bytes = (reference / 'modelParameter.mat').read_bytes()
    assert sha256(parameter_bytes).hexdigest() == MAT_HASH
    parameter = loadmat(BytesIO(parameter_bytes), squeeze_me=True, struct_as_record=False)['modelParameter']
    source = (reference / 'MU_population_model.m').read_text()
    fragments = {
        'lengthGainAboveOptimal': 'k_3 = (phi_1.*k_3)*(Lce-1) + k_3;',
        'lengthGainBelowOptimal': 'k_3 = (phi_2.*k_3)*(Lce-1) + k_3;',
        'yieldVelocityDependence': 'Y_dot = (1-c_y.*(1-exp(-abs(V)./V_y))-Y)./T_y;',
        'tendonFirstStage': 'f1 = contraction_dynamics(A,L_se,x_m,',
        'tendonSecondStage': 'f2 = contraction_dynamics(A,L_se,km_1,',
        'tendonThirdStage': 'f3 = contraction_dynamics(A,L_se,km_2,',
        'tendonFourthStage': 'f4 = contraction_dynamics(A,L_se,km_3,',
        'tendonUpdatedAfterStep': 'L_se = (Lmt - L_ce*L0*cos(alpha))/L0T;',
    }
    locations = {}
    for name, fragment in fragments.items():
        found = [i + 1 for i, line in enumerate(source.splitlines()) if fragment in line]
        assert len(found) == 1
        locations[name] = dict(path='MU_population_model.m', line=found[0], source=fragment)
    assert locations['tendonUpdatedAfterStep']['line'] > locations['tendonFourthStage']['line']
    geometry = {k: float(getattr(parameter, k)) for k in
        ('optimalLength', 'L0T', 'pennationAngle', 'mass', 'F0', 'Lmax', 'L_ce', 'L_se', 'Lmt')}
    row = parameter.parameterMatrix[0].tolist()
    assert len(row) == 14 and row[12] == 1.9 and row[13] == 1.2999999999999998
    inspected = ('src/NRE.SimAvatar/AvatarMuscle.cs', 'src/NRE.SimAvatar/AvatarMotorUnitCalcium.cs',
        'src/NRE.SimAvatar/AvatarIsometricMotorUnitPlant.cs', 'UnityWorld/Assets/NRE/Runtime/MuscleCore.cs',
        'UnityWorld/Assets/NRE/Runtime/MusclePlant.cs', 'UnityWorld/Assets/NRE/Runtime/MotorUnitPool.cs',
        'tools/prepare-unity-muscles.py')
    local = [dict(path=p, sha256=sha256((ROOT / p).read_bytes()).hexdigest()) for p in inspected]
    output = ROOT / 'artifacts/motor-mechanics-source-20261113'
    output.mkdir()
    (output / 'upstream.zip').write_bytes(archive)
    report = dict(capturedUtc=datetime.now(timezone.utc).isoformat(), upstreamRevision=REVISION,
        source='https://github.com/anagamori/MU-Population-Model-Nagamori-2020/tree/' + REVISION,
        paper='https://journals.plos.org/ploscompbiol/article?id=10.1371/journal.pcbi.1008707',
        archiveSha256=sha256(archive).hexdigest(), upstreamFiles=entries, parameterSha256=MAT_HASH,
        firstMotorUnitParameterRow=row, firstMotorUnitTetanicForceNewtons=float(parameter.PTi[0]),
        rawGeometry=geometry, sourceGeometryLengthUnits='centimeters except normalized L_ce/L_se/Lmax',
        muscleMassUnits='kilograms', angleUnits='radians', modelUnits=int(parameter.N_MU), slowUnits=int(parameter.index_slow),
        sourceLocations=locations, inspectedOwningSources=local,
        findings=['Existing event-driven unit is restricted to optimal fixed length and equilibrium yield.',
            'Dynamic extension requires length-dependent binding/Hill/cooperativity and velocity-dependent yield, not only a force multiplier.',
            'Native tendon length is held across the four mechanical RK stages then updated after the step; continuous coupling requires a separate comparison.',
            'Legacy Avatar force and normalized joint-length mapping do not by themselves instantiate this reference tendon plant.',
            'Parameter anatomy is tibialis anterior; it is not calibration for all Avatar muscles.'],
        upstreamModelExecuted=False, testsExecuted=False, physiologicalCalibrationComplete=False)
    (output / 'review.json').write_text(json.dumps(report, indent=2, allow_nan=False) + '\n')
    print(json.dumps(dict(output=str(output), verifiedUpstreamFiles=len(entries), parameterSha256=MAT_HASH,
        firstRowLengthGains=row[12:14], upstreamModelExecuted=False), indent=2))


if __name__ == '__main__':
    main()
