"""Unexecuted comparison corruption controls for a later visible window."""
from copy import deepcopy
from pathlib import Path
import tempfile
import unittest

import numpy as np

from canal_afferent_reference_analysis import ErrorEnvelope, arrays, clocks, refinement


NAMES = ['hairMv', 'potassiumMm', 'activation', 'chargeFc', 'potassiumAttomoles']
BOUNDS = dict(absolute=dict(voltage=2., ion=.2, gate=.02, charge=.002, amount=.0002), relative=0.)
REFINEMENT = dict(maximumRemainingFraction=.4, normalizedErrorFloor=.01)


class ReferenceAnalysisControls(unittest.TestCase):
    def test_early_failure_and_every_state_flux_group_survive_chunking(self):
        expected = np.zeros((4, 5))
        actual = np.array([[4., .1, .01, .001, .0001], [0., .3, 0., 0., 0.],
                           [0., 0., .03, 0., 0.], [0., 0., 0., .003, .0003]])
        ticks = np.array([1, 2, 3, 4], dtype=np.int64)
        whole = ErrorEnvelope(NAMES, BOUNDS)
        whole.add(actual, expected, ticks)
        split = ErrorEnvelope(NAMES, BOUNDS)
        split.add(actual[:1], expected[:1], ticks[:1])
        split.add(actual[1:], expected[1:], ticks[1:])
        self.assertEqual(whole.report(), split.report())
        result = split.report()
        self.assertFalse(result['passed'])
        self.assertEqual(result['maximumAllowedFraction'], 2.)
        self.assertEqual(result['worst']['sourceTick'], 1)
        self.assertEqual(result['worst']['component'], 'hairMv')
        self.assertEqual(result['samples'], 4)
        self.assertEqual(result['numericValues'], 20)
        self.assertEqual(result['maximumAbsoluteByGroup'],
            dict(voltage=4., ion=.3, gate=.03, charge=.003, amount=.0003))

    def test_reference_relative_scale_is_not_replaced_by_candidate_scale(self):
        bounds = deepcopy(BOUNDS)
        bounds['relative'] = .5
        expected = np.ones((1, 5))
        actual = expected.copy()
        actual[0, 4] = 2.
        envelope = ErrorEnvelope(NAMES, bounds)
        envelope.add(actual, expected, np.array([0], dtype=np.int64))
        self.assertFalse(envelope.report()['passed'])
        self.assertEqual(envelope.report()['maximumAllowedFraction'], 1. / (.0002 + .5))

    def test_duplicate_reversed_and_noninteger_ticks_are_rejected(self):
        values = np.zeros((2, 5))
        for ticks in (np.array([1, 1]), np.array([2, 1], dtype=np.uint64), np.array([1., 2.])):
            with self.subTest(ticks=ticks.tolist()), self.assertRaises(ValueError):
                ErrorEnvelope(NAMES, BOUNDS).add(values, values, ticks)
        envelope = ErrorEnvelope(NAMES, BOUNDS)
        envelope.add(values, values, np.array([1, 2]))
        with self.assertRaises(ValueError):
            envelope.add(values, values, np.array([2, 3]))

    def test_empty_missing_state_and_nonfinite_comparisons_cannot_pass(self):
        with self.assertRaises(ValueError):
            ErrorEnvelope(NAMES, BOUNDS).report()
        expected = np.zeros((2, 5))
        for actual in (np.zeros((1, 5)), np.zeros((2, 4)), np.full((2, 5), np.nan), np.full((2, 5), np.inf)):
            with self.subTest(shape=actual.shape), self.assertRaises(ValueError):
                ErrorEnvelope(NAMES, BOUNDS).add(actual, expected, np.array([1, 2]))

    def test_same_sample_count_at_different_times_is_not_refinement(self):
        reports = []
        for ticks in (np.array([1, 2]), np.array([2, 3])):
            envelope = ErrorEnvelope(NAMES, BOUNDS)
            envelope.add(np.zeros((2, 5)), np.zeros((2, 5)), ticks)
            reports.append(envelope.report())
        with self.assertRaises(ValueError):
            refinement(reports, REFINEMENT)

    def test_original_refinement_limit_can_fail_despite_finest_accuracy(self):
        def reports(errors):
            results = []
            for error in errors:
                envelope = ErrorEnvelope(NAMES, BOUNDS)
                actual = np.zeros((1, 5))
                actual[0, 0] = error * 2
                envelope.add(actual, np.zeros((1, 5)), np.array([1]))
                results.append(envelope.report())
            return results
        passing = refinement(reports([2., .8, .32]), REFINEMENT)
        failing_reports = reports([2., 1., .5])
        self.assertTrue(passing['passed'])
        self.assertTrue(failing_reports[-1]['passed'])
        self.assertFalse(refinement(failing_reports, REFINEMENT)['passed'])
        self.assertEqual(REFINEMENT, dict(maximumRemainingFraction=.4, normalizedErrorFloor=.01))

    def test_all_three_integer_clocks_match_without_nearest_time_search(self):
        dt = 9.999999776482582
        p = dict(sourceStepsPerInterval=65536, physicalIntervals=10,
            connected=dict(mechanicalRefinement=4, bundle=dict(frame=dict(clock=dict(
                initialPhysicsStep=0, physicsStepMs=dt, neuralEpochMs=1000.)))))
        for interval in (0, 1, 9):
            fine = clocks(p, interval, 65536)
            for count in (16384, 32768, 65536):
                selected = clocks(p, interval, count)
                for key in fine:
                    np.testing.assert_array_equal(selected[key], fine[key][::65536 // count])
            self.assertEqual(fine['sourceTicks'][0], interval * 65536)
            self.assertEqual(fine['sourceTicks'][-1], (interval + 1) * 65536)
            self.assertEqual(fine['localMs'][0], 0.)
            self.assertEqual(fine['localMs'][-1], dt)
        for count in (True, 0, 3, 131072):
            with self.subTest(count=count), self.assertRaises(ValueError):
                clocks(p, 0, count)
        with self.assertRaises(ValueError):
            clocks(p, 10, 65536)

    def test_archive_rejects_wrong_tick_type_extra_fields_and_nonfinite_tail(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / 'samples.npz'
            variants = [dict(localTicks=np.array([0, 1], dtype=np.int32), values=np.zeros((5, 2))),
                        dict(localTicks=np.array([0, 1], dtype=np.int64), values=np.zeros((5, 2)), extra=np.array([0.])),
                        dict(localTicks=np.array([0, 1], dtype=np.int64), values=np.full((5, 2), np.nan))]
            for values in variants:
                np.savez(path, **values)
                with self.assertRaises(ValueError):
                    arrays(path, ('localTicks', 'values'))


if __name__ == '__main__':
    if not __debug__:
        raise RuntimeError('Assertions must remain enabled.')
    unittest.main(verbosity=2)
