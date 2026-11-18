"""Prospective saved-eigenvector controls, run only in the visible study window."""
from copy import deepcopy
import io
import json
from pathlib import Path
import tempfile
import unittest
import numpy as np
from afferent_spectrum_witness import arithmetic_layout,CANONICAL_LAYOUT,LEGACY_LAYOUT
from afferent_rest_analysis import audit_spectrum
from afferent_rest_reference import spectrum
from afferent_rest_protocol import protocol


def round_trip(w,v):
    stream=io.BytesIO();np.savez_compressed(stream,eigenvalues=w,eigenvectors=v);stream.seek(0)
    with np.load(stream,allow_pickle=False) as stored:return stored['eigenvalues'],stored['eigenvectors']


class SpectrumControls(unittest.TestCase):
    def test_legacy_real_layout_survives_saved_values(self):
        matrix=np.array([[-2.,.37,0.], [0.,-3.,.19], [0.,0.,-4.]])
        w,v=np.linalg.eig(matrix);stored=round_trip(w,v)
        rw,rv=arithmetic_layout(*stored,LEGACY_LAYOUT)
        self.assertEqual(w.strides,rw.strides);self.assertEqual(v.strides,rv.strides)
        np.testing.assert_array_equal(w,rw);np.testing.assert_array_equal(v,rv)
        np.testing.assert_array_equal(matrix@v-v*w,matrix@rv-rv*rw)

    def test_complex_witness_round_trip(self):
        w,v=np.linalg.eig(np.array([[-1.,-2.],[2.,-1.]]))
        rw,rv=arithmetic_layout(*round_trip(w,v),LEGACY_LAYOUT)
        np.testing.assert_array_equal(w,rw);np.testing.assert_array_equal(v,rv)

    def test_canonical_layout_is_contiguous(self):
        w,v=np.linalg.eig(np.array([[-2.,.37],[0.,-3.]]))
        rw,rv=arithmetic_layout(w,v,CANONICAL_LAYOUT)
        self.assertTrue(rw.flags.c_contiguous and rv.flags.c_contiguous)
        self.assertEqual(rv.strides,(16,8));np.testing.assert_array_equal(v,rv)

    def test_unknown_layout_rejected(self):
        with self.assertRaises(AssertionError):arithmetic_layout(np.ones(1),np.ones((1,1)),'guess')

    def test_mismatched_precision_rejected(self):
        with self.assertRaises(AssertionError):arithmetic_layout(np.ones(1,dtype=np.float32),np.ones((1,1)),CANONICAL_LAYOUT)

    def test_saved_canonical_audit_exact(self):
        self.check_audit()

    def test_changed_residual_rejected(self):
        self.check_audit(tamper='residual')

    def test_changed_witness_rejected(self):
        self.check_audit(tamper='witness')

    def check_audit(self,tamper=None):
        with tempfile.TemporaryDirectory(prefix='dnne-spectrum-controls-') as directory:
            path=Path(directory)/'witness.npz';matrix=np.array([[-2.,.37],[0.,-3.]])
            settings=protocol()['stability'];record=spectrum(matrix,0.,settings,path)
            self.assertEqual(record['witnessArithmeticLayout'],CANONICAL_LAYOUT)
            self.assertTrue(audit_spectrum(matrix,0.,settings,record,path)['eigenResidualPassed'])
            if tamper=='residual':
                changed=deepcopy(record);changed['normalizedEigenResiduals'][0]+=1e-20
                with self.assertRaises(AssertionError):audit_spectrum(matrix,0.,settings,changed,path)
            elif tamper=='witness':
                with np.load(path,allow_pickle=False) as saved:w=saved['eigenvalues'];v=saved['eigenvectors']
                v[0,0]+=.01;np.savez_compressed(path,eigenvalues=w,eigenvectors=v)
                with self.assertRaises(AssertionError):audit_spectrum(matrix,0.,settings,record,path)


def run(output):
    assert not output.exists()
    result=unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(SpectrumControls))
    passed=result.testsRun-len(result.failures)-len(result.errors)-len(result.skipped)
    output.write_text(json.dumps(dict(completed=result.wasSuccessful() and passed==8,passed=passed,expected=8,modelsExecuted=False),indent=2)+'\n',encoding='utf-8')
    if not result.wasSuccessful() or passed!=8:raise RuntimeError('Eigen-witness controls failed; preserve evidence.')
