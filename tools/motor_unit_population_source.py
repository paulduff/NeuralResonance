"""Decode the pinned author population; no neuronal or muscle model is executed.

The full source contains fast units, while AvatarDynamicMuscle currently uses
slow force/length/velocity/yield equations only. Exporting slow parameters must
never silently select a fast row or redistribute the omitted force capacity.
"""
from datetime import datetime, timezone
from decimal import Decimal, localcontext
from hashlib import sha256
from io import BytesIO
import json
from pathlib import Path
import shutil
import struct
import sys
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[1]
REVISION = 'c317be803f01965a1ca8fc4cbad1ba9156433af8'
ARCHIVE_SHA256 = 'e89cfa1394bd0137e522e0c656d1cfcb981bc76b25c87021f8eeacacfabc8d6d'
MAT_SHA256 = '01123aec76d1acc18b306ae1bd9daabfd29fe9287b54212aef5db3f15f719ebd'
CALCIUM_NAMES = ('storeSites','totalCalcium','releaseRatePerSecond','pumpRatePerSecond',
    'bindingRatePerSecond','unbindingRatePerSecond','releaseRiseSeconds','releaseDecaySeconds',
    'hillExponent','halfActivationBinding','activationSeconds','cooperativity','peakTetanicForceNewtons')
ROW_NAMES = CALCIUM_NAMES[:12]+('lengthGainAboveOptimal','lengthGainBelowOptimal','peakTetanicForceNewtons')


def read(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def digest(path):
    return sha256(Path(path).read_bytes()).hexdigest()


def population(archive_bytes):
    """Return all 200 source rows and original bit patterns, without normalization."""
    from scipy.io import loadmat
    import numpy as np
    if sha256(archive_bytes).hexdigest()!=ARCHIVE_SHA256:
        raise ValueError('Require the preserved author source archive.')
    with ZipFile(BytesIO(archive_bytes)) as archive:
        if archive.comment.decode().strip()!=REVISION:
            raise ValueError('Unexpected author revision.')
        parameter_bytes = archive.read('modelParameter.mat')
    if sha256(parameter_bytes).hexdigest()!=MAT_SHA256:
        raise ValueError('Pinned MAT parameters changed.')
    data = loadmat(BytesIO(parameter_bytes),squeeze_me=True,struct_as_record=False)['modelParameter']
    count, slow = int(data.N_MU), int(data.index_slow)
    if (count,slow)!=(200,147) or data.parameterMatrix.shape!=(200,14):
        raise ValueError('Unexpected original population dimensions.')
    if not np.array_equal(data.i_MU,np.arange(1,201)):
        raise ValueError('Original row identities changed.')
    rows = []; packed = bytearray()
    for index in range(count):
        values = [float(x) for x in data.parameterMatrix[index]]+[float(data.PTi[index])]
        if not all(np.isfinite(values)):
            raise ValueError('Nonfinite source parameters.')
        raw = struct.pack('<15d',*values); packed.extend(raw)
        rows.append(dict(row=index+1,fiberType='slow' if index<slow else 'fast',
            parameters=dict(zip(ROW_NAMES,values)),littleEndianBinary64Hex=raw.hex()))
    # Every original field remains available for interpretation. Recruitment
    # tables are retained as source data, never installed as a spike generator.
    fields = {name: (value.tolist() if hasattr(value,'tolist') else value)
              for name in data._fieldnames for value in [getattr(data,name)]}
    with localcontext() as context:
        context.prec = 100
        forces = [Decimal.from_float(float(x)) for x in data.PTi]
        total = sum(forces); slow_total = sum(forces[:slow]); reference = Decimal.from_float(float(data.F0))
        capacities = dict(referenceForceNewtons=str(reference),sumAllSourceUnitForcesNewtons=str(total),
            sumSlowSourceUnitForcesNewtons=str(slow_total),sumFastSourceUnitForcesNewtons=str(total-slow_total),
            sumMinusReferenceNewtons=str(total-reference),forceCoefficientsRescaled=False)
    return dict(version='pinned-nagamori-motor-unit-population-v1',upstreamRevision=REVISION,
        archiveSha256=ARCHIVE_SHA256,parameterSha256=MAT_SHA256,sourceMuscle='tibialis anterior',
        units=count,slowUnits=slow,fastUnits=count-slow,rows=rows,originalMatFields=fields,
        rowColumns=ROW_NAMES,rowBytesSha256=sha256(packed).hexdigest(),capacityAccounting=capacities,
        upstreamModelExecuted=False,neuronalRecruitmentImported=False,fastDynamicsImplemented=False,
        physiologicalCalibrationQualified=False,liveMuscleSelected=False,avatarStandingQualified=False)


def slow_parameter_rows(archive_bytes, row_numbers):
    """Export slow parameters with separate zero-based slots and original rows.

    Existing comparison cases address array slots through unitRows. Keep that
    slot in `row` and the one-based author identity in `sourceRow`; selecting
    source row 147 alone must not create an out-of-range array index of 147.
    This only prepares data. It creates no contact, cell, state or spike.
    """
    source = population(archive_bytes); requested = tuple(row_numbers)
    if not requested or any(type(i) is not int or i<1 or i>source['slowUnits'] for i in requested):
        raise ValueError('Select explicit original slow rows 1..147; fast-unit dynamics are not implemented.')
    if len(set(requested))!=len(requested):
        raise ValueError('Do not clone an author row within this reference population.')
    result = []
    for slot, index in enumerate(requested):
        row = source['rows'][index-1]; p = row['parameters']
        result.append(dict(row=slot,sourceRow=index,calcium={name:p[name] for name in CALCIUM_NAMES},
            lengthGainAboveOptimal=p['lengthGainAboveOptimal'],lengthGainBelowOptimal=p['lengthGainBelowOptimal']))
    return result


def capture():
    original = ROOT/'artifacts/motor-mechanics-source-20261113'
    output = ROOT/'artifacts/motor-population-source-20261123'
    if output.exists(): raise FileExistsError('Keep previous source evidence intact: '+str(output))
    manifest = read(original/'backup-manifest.json'); backup = Path(manifest['Destination'])
    if digest(original/'backup-manifest.json')!=digest(backup/'backup-manifest.json'):
        raise ValueError('Original source manifests differ.')
    for entry in manifest['Files']:
        for folder in (original,backup):
            path = folder/entry['Path']
            if path.stat().st_size!=entry['Bytes'] or digest(path)!=entry['Sha256'].lower():
                raise ValueError('Original source/backup changed: '+str(path))
    archive_bytes = (original/'upstream.zip').read_bytes(); catalog = population(archive_bytes)
    encoded = json.dumps(catalog,indent=2,allow_nan=False)+'\n'
    decoded = json.loads(encoded)
    # File-format fidelity check only: each JSON number round-trips every original
    # binary64 bit. No force calculation, integrator, neuronal model or test runs.
    for row in decoded['rows']:
        if struct.pack('<15d',*(row['parameters'][name] for name in ROW_NAMES)).hex()!=row['littleEndianBinary64Hex']:
            raise ValueError('Population JSON changed an original parameter bit.')
    output.mkdir()
    (output/'population.json').write_text(encoded,encoding='utf-8')
    (output/'upstream.zip').write_bytes(archive_bytes)
    shutil.copyfile(__file__,output/Path(__file__).name)
    report = dict(capturedUtc=datetime.now(timezone.utc).isoformat(),sourceManifestSha256=digest(original/'backup-manifest.json'),
        source=str(original),protectedSource=str(backup),populationSha256=digest(output/'population.json'),
        sourceRows=200,slowRows=147,fastRows=53,exactBinary64Parameters=3000,originalRowOrderRetained=True,
        originalFieldsRetained=True,forceCoefficientsRescaled=False,upstreamModelExecuted=False,
        testsExecuted=False,liveBrainOrBodyAccessed=False,pendingRunAccessed=False,
        remaining=['slow population trajectory comparison','fast sag and force curves with neuronal event ownership',
            'muscle-specific capacities and geometry','heterogeneous neuronal recruitment','coupled native muscle qualification'])
    (output/'review.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(dict(output=str(output),rows=200,slow=147,fast=53,bitPreservedParameters=3000,
        capacities=catalog['capacityAccounting'],modelsExecuted=False,testsExecuted=False),indent=2))


if __name__=='__main__':
    if sys.argv[1:]!=['capture']: raise SystemExit('Use capture; no simulation entry point exists.')
    capture()
