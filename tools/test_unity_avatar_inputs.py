"""Build-input regression controls; run only in the independent PowerShell window."""
from hashlib import sha256
import importlib.util
import json
from pathlib import Path
import shutil
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('avatar_build_inputs', Path(__file__).with_name('unity_metadata.py'))
metadata = importlib.util.module_from_spec(spec); spec.loader.exec_module(metadata)


class AvatarBuildInputTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.project = Path(self.temp.name)/'installed'; self.avatar = self.project/'Assets/Avatar'
        self.avatar.mkdir(parents=True)
        self.entries = []
        for i, name in enumerate(('MikeAlger.fbx','MikeAlger_Texture.jpeg'),1):
            data = ('input fixture '+name).encode(); (self.avatar/name).write_bytes(data)
            (self.avatar/(name+'.meta')).write_text(f'fileFormatVersion: 2\nguid: {i:032x}\n',encoding='utf-8')
            self.entries.append(dict(file=name,bytes=len(data),sha256=sha256(data).hexdigest()))
        self.provenance()

    def provenance(self):
        (self.avatar/'provenance.json').write_text(json.dumps(dict(files=self.entries)),encoding='utf-8')

    def test_freeze_preserves_payloads_and_frozen_importers(self):
        frozen = Path(self.temp.name)/'frozen'; avatar = frozen/'Assets/Avatar'; avatar.mkdir(parents=True)
        for name in ('provenance.json','MikeAlger.fbx.meta','MikeAlger_Texture.jpeg.meta'):
            shutil.copyfile(self.avatar/name,avatar/name)
        before = {p.name:p.read_bytes() for p in avatar.iterdir()}
        result = metadata.freeze_avatar_assets(self.project,frozen)
        self.assertEqual(len(result['attributedAssets']),2)
        for entry in self.entries:
            self.assertEqual((avatar/entry['file']).read_bytes(),(self.avatar/entry['file']).read_bytes())
        self.assertEqual({name:(avatar/name).read_bytes() for name in before},before)
        self.assertFalse(result['modelsImported']); self.assertFalse(result['testsExecuted'])
        with self.assertRaisesRegex(ValueError,'replace existing'): metadata.freeze_avatar_assets(self.project,frozen)

    def test_missing_model_rejected_before_import(self):
        (self.avatar/'MikeAlger.fbx').unlink()
        with self.assertRaisesRegex(ValueError,'Missing attributed'): metadata.avatar_assets(self.project)

    def test_missing_texture_rejected_before_import(self):
        (self.avatar/'MikeAlger_Texture.jpeg').unlink()
        with self.assertRaisesRegex(ValueError,'Missing attributed'): metadata.avatar_assets(self.project)

    def test_same_length_corruption_rejected(self):
        path = self.avatar/'MikeAlger.fbx'; data = path.read_bytes(); path.write_bytes(b'X'+data[1:])
        with self.assertRaisesRegex(ValueError,'differs'): metadata.avatar_assets(self.project)

    def test_truncated_asset_rejected(self):
        path = self.avatar/'MikeAlger_Texture.jpeg'; path.write_bytes(path.read_bytes()[:-1])
        with self.assertRaisesRegex(ValueError,'differs'): metadata.avatar_assets(self.project)

    def test_missing_importer_rejected(self):
        (self.avatar/'MikeAlger.fbx.meta').unlink()
        with self.assertRaisesRegex(ValueError,'Missing attributed'): metadata.avatar_assets(self.project)

    def test_duplicate_attribution_rejected(self):
        self.entries[1] = dict(self.entries[0]); self.provenance()
        with self.assertRaisesRegex(ValueError,'exactly the two'): metadata.avatar_assets(self.project)

    def test_outside_asset_path_rejected(self):
        self.entries[0]['file'] = '../MikeAlger.fbx'; self.provenance()
        with self.assertRaisesRegex(ValueError,'exactly the two'): metadata.avatar_assets(self.project)


if __name__=='__main__':
    suite = unittest.defaultTestLoader.loadTestsFromTestCase(AvatarBuildInputTests)
    if suite.countTestCases()!=8: raise RuntimeError('Build-input regression inventory changed.')
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    if result.testsRun!=8 or not result.wasSuccessful(): raise SystemExit(1)
