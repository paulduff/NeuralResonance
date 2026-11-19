"""Prospective protocol and sample-coordinate controls for the visible workflow."""
from copy import deepcopy
import unittest
import numpy as np
from canal_midpoint_protocol import forcing_ticks, policy, protocol, ORIGINAL_VERSION, MIDPOINT_VERSION


class MidpointComparisonControls(unittest.TestCase):
    def test_only_capture_version_and_sampling_choice_change(self):
        reference = dict(version='continuous-canal-joint-afferent-reference-v1',
            connected=dict(version=ORIGINAL_VERSION, fixture=dict(values=[1., 2.]),
                limits=dict(charge=.0002), clocks=[16384, 32768, 65536]))
        before = deepcopy(reference)
        changed = protocol(reference)
        self.assertEqual(changed, dict(reference['connected'], version=MIDPOINT_VERSION, forcingPolicy='MidpointHeld'))
        self.assertEqual(reference, before)
        changed['fixture']['values'][0] = 9.
        self.assertEqual(reference, before)

    def test_reference_or_original_policy_cannot_be_silently_replaced(self):
        for reference in (dict(version='wrong', connected=dict(version=ORIGINAL_VERSION)),
                dict(version='continuous-canal-joint-afferent-reference-v1', connected=dict(version=MIDPOINT_VERSION)),
                dict(version='continuous-canal-joint-afferent-reference-v1', connected=dict(version=ORIGINAL_VERSION, forcingPolicy='StartHeld'))):
            with self.subTest(reference=reference), self.assertRaises(ValueError):
                protocol(reference)

    def test_capture_version_requires_its_exact_declared_policy(self):
        self.assertEqual(policy(dict(version=ORIGINAL_VERSION)), 'StartHeld')
        self.assertEqual(policy(dict(version=MIDPOINT_VERSION, forcingPolicy='MidpointHeld')), 'MidpointHeld')
        for bad in (dict(version=ORIGINAL_VERSION, forcingPolicy='MidpointHeld'),
                dict(version=MIDPOINT_VERSION), dict(version=MIDPOINT_VERSION, forcingPolicy='StartHeld'), dict(version='unknown')):
            with self.subTest(bad=bad), self.assertRaises(ValueError):
                policy(bad)

    def test_original_start_ticks_remain_exact_and_are_not_aliased(self):
        original = np.array([262144, 262148, 262152], dtype=np.int64)
        saved = original.copy()
        result = forcing_ticks(original, 4, 'StartHeld')
        np.testing.assert_array_equal(result, original)
        result[0] = 0
        np.testing.assert_array_equal(original, saved)

    def test_midpoints_at_all_three_resolutions_stay_inside_their_steps(self):
        for count in (16384, 32768, 65536):
            stride = 262144 // count
            for interval in (0, 1, 9):
                starts = (interval + 1) * 262144 + np.arange(count, dtype=np.int64) * stride
                result = forcing_ticks(starts, stride, 'MidpointHeld')
                np.testing.assert_array_equal(result - starts, np.full(count, stride // 2, dtype=np.int64))
                self.assertTrue(((result > starts) & (result < starts + stride)).all())
                self.assertEqual(result[-1], (interval + 2) * 262144 - stride // 2)

    def test_half_ticks_are_rejected_instead_of_rounded(self):
        starts = np.array([0, 4], dtype=np.int64)
        for stride in (1, 3, 5):
            with self.subTest(stride=stride), self.assertRaises(ValueError):
                forcing_ticks(starts, stride, 'MidpointHeld')
        np.testing.assert_array_equal(forcing_ticks(starts, 1, 'StartHeld'), starts)

    def test_noninteger_duplicate_reversed_or_empty_coordinates_are_rejected(self):
        for ticks in (np.array([], dtype=np.int64), np.array([0., 4.]), np.array([0, 4], dtype=np.uint64),
                np.array([0, 0], dtype=np.int64), np.array([4, 0], dtype=np.int64), np.array([-4, 0], dtype=np.int64)):
            with self.subTest(ticks=ticks.tolist()), self.assertRaises(ValueError):
                forcing_ticks(ticks, 4, 'MidpointHeld')
        for stride, selected in ((True, 'StartHeld'), (4., 'MidpointHeld'), (0, 'StartHeld'), (4, 'unknown')):
            with self.subTest(stride=stride, policy=selected), self.assertRaises(ValueError):
                forcing_ticks(np.array([0, 4], dtype=np.int64), stride, selected)

    def test_end_tick_overflow_is_rejected_even_if_midpoint_would_fit(self):
        largest = np.iinfo(np.int64).max
        for selected in ('StartHeld', 'MidpointHeld'):
            with self.subTest(policy=selected), self.assertRaises(ValueError):
                forcing_ticks(np.array([largest - 2], dtype=np.int64), 4, selected)
        with self.assertRaises(ValueError):
            forcing_ticks(np.array([0], dtype=np.int64), 2**64, 'MidpointHeld')


if __name__ == '__main__':
    if not __debug__:
        raise RuntimeError('Assertions must remain enabled.')
    unittest.main(verbosity=2)
