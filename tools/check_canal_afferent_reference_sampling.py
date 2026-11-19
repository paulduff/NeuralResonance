"""Unexecuted analytic sampler controls for a later visible frozen window."""
import json
from pathlib import Path
import tempfile
import unittest

import numpy as np

from canal_afferent_continuous_reference import integrate_interval


class LinearDrive:
    def rates(self, time, state):
        return np.array([-2 * state[0], state[0]])

    def jacobian(self, time, state):
        return np.array([[-2., 0.], [1., 0.]])


def finite(values):
    if not np.isfinite(values).all():
        raise ValueError('Nonfinite analytic trajectory.')


class ReferenceSamplingControls(unittest.TestCase):
    def integrate(self, output, *, method='Radau', initial=None, drive=None, validate=finite, count=64):
        return integrate_interval(drive or LinearDrive(), np.array([3., 7.]) if initial is None else initial,
            1., count, dict(method=method, rtol=2e-12), np.array([1e-14, 1e-14]), .025, validate, output)

    def test_both_methods_retain_full_grid_and_match_analytic_state_and_flux(self):
        with tempfile.TemporaryDirectory() as temp:
            for method in ('Radau', 'BDF'):
                with self.subTest(method=method):
                    folder = Path(temp) / method
                    end, node_times, nodes, times, values = self.integrate(folder, method=method)
                    expected = np.vstack((3 * np.exp(-2 * times), 7 + 1.5 * (1 - np.exp(-2 * times))))
                    np.testing.assert_allclose(values, expected, rtol=0, atol=1e-9)
                    self.assertEqual(values.shape, (2, 65))
                    self.assertTrue((np.diff(node_times) > 0).all())
                    self.assertEqual(node_times[-1], 1.)
                    np.testing.assert_array_equal(end, nodes[:, -1])
                    with np.load(folder / 'samples.npz', allow_pickle=False) as saved:
                        np.testing.assert_array_equal(saved['localTicks'], np.arange(65))
                        np.testing.assert_array_equal(saved['values'], values)
                    status = json.loads((folder / 'integration.json').read_text())
                    self.assertTrue(status['completed'])
                    self.assertFalse(status['referenceTrajectoryCriteriaPassed'])

    def test_continuation_keeps_state_and_nonzero_accumulated_flux(self):
        with tempfile.TemporaryDirectory() as temp:
            initial = np.array([3., 7.])
            first = self.integrate(Path(temp) / 'first', initial=initial)
            second = self.integrate(Path(temp) / 'second', initial=first[0])
            np.testing.assert_array_equal(initial, [3., 7.])
            np.testing.assert_array_equal(second[2][:, 0], first[0])
            expected = [3 * np.exp(-4), 7 + 1.5 * (1 - np.exp(-4))]
            np.testing.assert_allclose(second[0], expected, rtol=0, atol=1e-9)
            self.assertGreater(second[0][1], first[0][1])

    def test_derivative_exception_retains_only_populated_prefix(self):
        class FailingDrive(LinearDrive):
            def rates(self, time, state):
                if time >= .4:
                    raise ArithmeticError('Deliberate reference RHS failure.')
                return super().rates(time, state)

        with tempfile.TemporaryDirectory() as temp:
            folder = Path(temp) / 'failed'
            with self.assertRaisesRegex(ArithmeticError, 'Deliberate'):
                self.integrate(folder, drive=FailingDrive())
            status = json.loads((folder / 'integration.json').read_text())
            self.assertFalse(status['completed'])
            self.assertGreater(status['acceptedNodes'], 1)
            self.assertLess(status['populatedSamples'], 65)
            self.assertFalse(status['unusedSampleTailSaved'])
            with np.load(folder / 'samples.npz', allow_pickle=False) as saved:
                self.assertEqual(saved['values'].shape, (2, status['populatedSamples']))
                self.assertTrue(np.isfinite(saved['values']).all())
                self.assertLessEqual(saved['localMs'][-1], status['completedThroughLocalMs'])
                self.assertTrue((saved['values'][1] >= 7).all())

    def test_failed_domain_check_preserves_the_rejected_accepted_node(self):
        def domain(values):
            finite(values)
            if (values[0] < 2.).any():
                raise ValueError('Analytic domain guard.')

        with tempfile.TemporaryDirectory() as temp:
            folder = Path(temp) / 'domain'
            with self.assertRaisesRegex(ValueError, 'domain guard'):
                self.integrate(folder, validate=domain)
            status = json.loads((folder / 'integration.json').read_text())
            self.assertFalse(status['completed'])
            self.assertIn('domain guard', status['error'])
            with np.load(folder / 'accepted-nodes.npz', allow_pickle=False) as saved:
                self.assertLess(saved['values'][0, -1], 2.)
                self.assertEqual(saved['values'].shape[1], status['acceptedNodes'])

    def test_initial_validation_failure_preserves_original_state_without_advancement(self):
        def reject(values):
            raise ValueError('Initial state rejected.')

        with tempfile.TemporaryDirectory() as temp:
            folder = Path(temp) / 'initial'
            with self.assertRaisesRegex(ValueError, 'Initial'):
                self.integrate(folder, validate=reject)
            status = json.loads((folder / 'integration.json').read_text())
            self.assertEqual(status['acceptedNodes'], 1)
            self.assertEqual(status['populatedSamples'], 1)
            self.assertIsNone(status['nfev'])
            with np.load(folder / 'samples.npz', allow_pickle=False) as saved:
                np.testing.assert_array_equal(saved['values'][:, 0], [3., 7.])

    def test_existing_output_cannot_be_overwritten(self):
        with tempfile.TemporaryDirectory() as temp:
            folder = Path(temp) / 'retained'
            folder.mkdir()
            sentinel = folder / 'evidence.txt'
            sentinel.write_text('original evidence')
            with self.assertRaises(FileExistsError):
                self.integrate(folder)
            self.assertEqual(sentinel.read_text(), 'original evidence')

    def test_invalid_source_grids_fail_before_creating_output(self):
        with tempfile.TemporaryDirectory() as temp:
            for count in (0, -1, 3, True, 2**25):
                folder = Path(temp) / ('grid-' + str(count))
                with self.subTest(count=count), self.assertRaises(ValueError):
                    self.integrate(folder, count=count)
                self.assertFalse(folder.exists())


if __name__ == '__main__':
    if not __debug__:
        raise RuntimeError('Assertions must remain enabled.')
    unittest.main(verbosity=2)
