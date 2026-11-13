"""Analytic recording-boundary regressions; run only in the visible study window."""
import argparse
import json
from pathlib import Path
import unittest

import numpy as np
from dynamic_muscle_reference import Reference


class AnalyticReference(Reference):
    """Constant storage plus a scalar integral of causal event counts.

    Reuses the real solve/recording loop and SciPy solvers, but no muscle
    equations or physiological parameters. Every boundary interval contributes
    to q(t) = t + sum(max(t - arrival, 0)), even without a recorded sample.
    """
    def __init__(self, events, knots):
        self.g = dict(optimalFiberLengthMeters=1., pennationRadians=0., referenceTendonLengthMeters=1.)
        self.protocol = dict(maximumReferenceStepSeconds=.05)
        self.events = [np.array(events, dtype=float)]
        self.knots = np.column_stack((knots, np.zeros(len(knots))))
        self.checked_internal_points = 0

    def initial(self):
        return np.array([1., 0., 1., 0., 0., 0.])

    def length(self, t):
        return 2.

    def rhs(self, t, y, active=None):
        result = np.zeros_like(y)
        result[3] = 1 + len(active[0])
        return result

    def mechanics(self, t, y):
        return np.zeros(7)

    def domain(self, states, tolerance=1e-12):
        self.checked_internal_points += len(np.atleast_2d(states))
        return bool(np.isfinite(states).all())


class RecordingBoundaryTests(unittest.TestCase):
    protocol = None
    method_checks = 0

    def check_trace(self, events, knots, grid, require_empty):
        boundaries = np.unique(np.r_[grid[0], grid[-1], knots, events])
        masks = [(grid >= left) & ((grid < right) if right < grid[-1] else (grid <= right))
                 for left, right in zip(boundaries[:-1], boundaries[1:])]
        self.assertEqual(any(not np.any(mask) for mask in masks), require_empty)
        np.testing.assert_array_equal(np.sum(masks, axis=0), np.ones(len(grid)))
        expected = grid + np.maximum(grid[:, None] - np.array(events)[None, :], 0).sum(axis=1)
        for method in ('DOP853', 'Radau'):
            with self.subTest(method=method):
                ref = AnalyticReference(events, knots)
                state, forces, stats = ref.solve(dict(method=method, rtol=1e-11, atol=1e-13), grid)
                np.testing.assert_allclose(state[:, 3], expected, rtol=0, atol=1e-10)
                np.testing.assert_array_equal(state[:, :3], np.tile([1., 0., 1.], (len(grid), 1)))
                self.assertTrue(np.isfinite(forces).all())
                self.assertTrue(stats['acceptedInternalDomainPassed'] and stats['sampleDomainPassed'])
                self.assertEqual(ref.reference_partial['completedBoundarySeconds'], grid[-1])
                self.assertEqual(ref.checked_internal_points, stats['acceptedInternalPoints'] + len(grid))
                self.assertGreaterEqual(stats['acceptedInternalPoints'], 2 * (len(boundaries) - 1))
                type(self).method_checks += 1

    def test_clustered_boundaries_keep_unsampled_state_advancement(self):
        self.check_trace([.375, .4], [0., .38, .39, 1.], np.arange(5) / 4, True)

    def test_coincident_events_knots_and_final_sample(self):
        self.check_trace([0., .25, .25, .75, 1.], [0., .25, .5, 1.], np.arange(5) / 4, False)

    def test_original_active_stretch_partition(self):
        case = next(c for c in self.protocol['cases'] if c['key'] == 'active-stretch')
        grid = np.arange(round(case['durationMs'] / self.protocol['sampleMs']) + 1) * self.protocol['sampleMs'] / 1000
        events = [e['arrivalAtMs'] / 1000 for e in case['events']]
        knots = [k['timeMs'] / 1000 for k in case['lengthKnots']]
        self.check_trace(events, knots, grid, True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('package', type=Path)
    args = parser.parse_args()
    RecordingBoundaryTests.protocol = json.loads((args.package / 'dynamic-input.json').read_text())
    suite = unittest.defaultTestLoader.loadTestsFromTestCase(RecordingBoundaryTests)
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    passed = result.wasSuccessful() and result.testsRun == 3 and RecordingBoundaryTests.method_checks == 6
    with (args.package / 'reference-sampling-tests.json').open('x', encoding='utf-8') as stream:
        json.dump(dict(passed=passed, tests=result.testsRun, methodChecks=RecordingBoundaryTests.method_checks,
            analyticalOracle=True, physiologicalModelExecuted=False), stream, indent=2)
        stream.write('\n')
    raise SystemExit(0 if passed else 1)
