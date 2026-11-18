"""Static literature transcription only. No equations integrated or models executed."""
import argparse
from datetime import datetime, timezone
from decimal import Decimal
from hashlib import sha256
from html.parser import HTMLParser
import json
from pathlib import Path
import re
import xml.etree.ElementTree as ET


SOURCES = {
    'PMC6520623.xml': ('10.1152/jn.00708.2018', '2448677fefac0233f0df73c345becf39ee1ea02b19a292ed6548d68f85afee6b'),
    'PMC5980960.xml': ('10.3389/fneur.2018.00366', '055209e2be1cc35dd41dbdf2fbc07f993163626ad04994d2d78712a5cedf1517'),
    'PMC3005417.html': ('10.1007/s10439-007-9277-y', '07724245a89d5bf4959de50181830d932f70ad15ba402c6ffaef00dc2f0db1e2'),
    'PMC5658742.html': ('10.1016/j.bpj.2017.08.001', '8037d79489f224bfbb3ef10255f95a2193bdc0b0bd6e1895bc3a1621b6c4a5dd'),
    'PMC4141440.html': ('10.1007/s10162-014-0456-x', 'd128be503be1849e5f5fc4801e3383a12c476d5014dda475752b0a9edec33edb'),
}


class Text(HTMLParser):
    def __init__(self):
        super().__init__(); self.parts = []

    def handle_data(self, data):
        self.parts.append(data)


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')


def numeric_tables(html, identifier):
    table = re.search(r'<table[^>]+id="' + identifier + r'"[\s\S]*?</table>', html)
    assert table, identifier
    math = re.search(r'<math[\s\S]*?</math>', table.group()).group()
    root = ET.fromstring(math)
    matrices = []
    for node in root.iter():
        if node.tag.rsplit('}', 1)[-1] != 'mtable': continue
        matrix = []
        for row in node:
            values = [''.join(cell.itertext()).replace('\u2212', '-') for cell in row]
            assert all(re.fullmatch(r'[+-]?\d+(?:\.\d+)?(?:e[+-]?\d+)?', value) for value in values)
            matrix.append(values)
        matrices.append(matrix)
    return math, matrices


def converted(matrix, factor):
    # Exact decimal unit conversion of published literals, not model evaluation.
    return [[str(Decimal(value) * Decimal(factor)) for value in row] for row in matrix]


def main(folder, catalog_path):
    assert not (folder / 'source-review.json').exists(), 'Preserve earlier source review.'
    entries = []
    for name, (doi, expected_hash) in SOURCES.items():
        raw = (folder / name).read_bytes()
        assert sha256(raw).hexdigest() == expected_hash, name
        assert doi in raw.decode('utf-8'), name
        entries.append(dict(path=name, doi=doi, sha256=expected_hash, bytes=len(raw)))
    html = (folder / 'PMC3005417.html').read_text(encoding='utf-8')
    matrices = {}
    for identifier, name, old_units, new_units in (
        ('FD23', 'mass', 'g/cm^4', 'kg/m^4'),
        ('FD24', 'damping', 'dyn*s/cm^5', 'Pa*s/m^3'),
        ('FD25', 'stiffness', 'dyn/cm^5', 'Pa/m^3'),
    ):
        math, tables = numeric_tables(html, identifier)
        assert len(tables) == 1
        matrix = tables[0]
        assert len(matrix) == 6 and all(len(row) == 6 for row in matrix)
        assert all(matrix[i][j] == matrix[j][i] for i in range(6) for j in range(6))
        assert (folder / (identifier + '-mathml.xml')).read_text(encoding='utf-8') == math
        matrices[name] = dict(sourceEquation=identifier, sourceUnits=old_units,
            sourceDecimalStrings=matrix, siUnits=new_units, exactUnitFactor='100000',
            siDecimalStrings=converted(matrix, '100000'))
    math, force_columns = numeric_tables(html, 'FD26')
    assert (folder / 'FD26-mathml.xml').read_text(encoding='utf-8') == math
    assert len(force_columns) == 3 and all(len(c) == 6 and all(len(r) == 1 for r in c) for c in force_columns)
    force = [[column[row][0] for column in force_columns] for row in range(6)]
    prime = [['-0.064', '0.042', '-0.997'], ['0.702', '0.699', '-0.136'], ['0.857', '-0.557', '-0.103']]
    parser = Text(); parser.feed(html)
    plain = re.sub(r'\s+', '', ''.join(parser.parts).replace('\u2212', '-'))
    for vector in prime: assert '[' + ','.join(vector) + ']' in plain
    assert '+xasposterior,+yasrightlateral,and+zassuperior' in plain
    catalog = dict(version='ifediba-2007-published-matrix-transcription-v1',
        doi=SOURCES['PMC3005417.html'][0], url='https://pmc.ncbi.nlm.nih.gov/articles/PMC3005417/',
        retainedHtmlSha256=SOURCES['PMC3005417.html'][1],
        specimen='Published labyrinth 1; right human labyrinth; not Avatar anatomy',
        stateOrder=['endolymph-HC', 'endolymph-AC', 'endolymph-PC', 'cupula-HC', 'cupula-AC', 'cupula-PC'],
        equation='M d2Q/dt2 + C dQ/dt + K Q = F; Q in m^3, t in seconds, F in Pa after conversion',
        matrices=matrices,
        forcing=dict(sourceEquation='FD26', sourceUnits='dyn/cm^2 per (rad/s^2)', siUnits='Pa per (rad/s^2)',
            exactUnitFactor='0.1', sourceDecimalStrings=force, siDecimalStrings=converted(force, '0.1'),
            columnOrder=['HC-prime', 'AC-prime', 'PC-prime']),
        primeDirections=dict(source='Paragraph after equation 26; rounded published literals',
            sourceFrame=['posterior', 'right lateral', 'superior'], vectors=prime,
            axesNormalized=False, forcingComponentInterpretationQualified=False, avatarAngularConventionQualified=False),
        qualifications=dict(literalTranscriptionChecked=True, exactCgsSiConversionRecorded=True,
            matrixSpectrumComputed=False, trajectoriesComputed=False, independentNumericalAccuracyQualified=False,
            localBundleDisplacementMapQualified=False, avatarAnatomyCalibrated=False,
            afferentFiringQualified=False, balanceQualified=False, liveParameterPreset=False),
        limitations=[
            'Rounded printed coefficients, not original high precision author data.',
            'Published prime directions are not silently treated as an orthonormal Avatar basis.',
            'Native axial-vector conventions, source forcing components and bilateral placement need explicit checks.',
            'Cupula volume or mean displacement is not an individual hair bundle displacement.',
            'No scalar viscosity was imported from dimensionally suspect table text.',
            'Rigid duct approximation does not establish responses above the low/mid frequency domain.',
        ])
    write(catalog_path, catalog); write(folder / 'ifediba-2007-transcription.json', catalog)
    script = Path(__file__).read_bytes()
    (folder / Path(__file__).name).write_bytes(script)
    write(folder / 'source-review.json', dict(reviewedUtc=datetime.now(timezone.utc).isoformat(),
        sources=entries, nativeSourcesChanged=False, modelsExecuted=False, testsExecuted=False,
        matricesTranscribed=3, forcingColumnsTranscribed=3, referenceTrajectoriesExecuted=False,
        catalogSha256=sha256(catalog_path.read_bytes()).hexdigest(), scriptSha256=sha256(script).hexdigest(),
        adoptedAsLiveCalibration=False))
    print('Five source bodies verified; three 6x6 matrices and three force columns transcribed with exact decimal SI conversion. No model/test/trajectory executed.')


if __name__ == '__main__':
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    parser = argparse.ArgumentParser()
    parser.add_argument('folder', type=Path); parser.add_argument('--catalog', required=True, type=Path)
    args = parser.parse_args(); main(args.folder.resolve(), args.catalog.resolve())
