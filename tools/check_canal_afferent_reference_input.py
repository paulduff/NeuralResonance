"""Analytic reference-input controls; run only in a visible frozen study.

These controls are authored, not yet executed. They exercise closed-form toy
systems and interface errors, not a native or physiological sensory model.
"""
from dataclasses import replace
from copy import deepcopy
import math
import unittest

import numpy as np

from canal_afferent_reference_input import BundleProjection, CanalInterval, DrivenSensoryEquation, HeldBundleInput


class ReferenceInputControls(unittest.TestCase):
    def geometry_fixture(self):
        # Analytic synthetic disk of radius 1 mm, not a fitted anatomical case.
        shear, aspect, radius = .37, .5, .001
        stiffness = 8 * shear * aspect / (math.pi * radius**3)
        matrices = {}
        for name, diagonal in (('mass', 1.), ('damping', 1.), ('stiffness', stiffness)):
            matrices[name] = dict(exactUnitFactor='1', sourceDecimalStrings=[
                [str(diagonal if i == j else 0.) for j in range(6)] for i in range(6)])
        catalog = dict(retainedHtmlSha256='a' * 64, matrices=matrices,
                       stateOrder=[kind + '-' + axis for kind in ('endolymph', 'cupula') for axis in ('HC', 'AC', 'PC')],
                       forcing=dict(exactUnitFactor='1', sourceDecimalStrings=[['0'] * 3 for _ in range(6)]))
        spec = dict(geometry=dict(canal=0, organId='synthetic-disk', sourceSha256='a' * 64,
                                  shearModulusPascal=shear, thicknessOverRadius=aspect),
                    attachment=dict(hairCellId='ddc1d6ab-dc76-413d-b879-dc70d0d34e56', rootXOverRadius=0.,
                                    bundleLengthMeters=.5 * radius, excitatoryPolarity=1))
        return spec, catalog

    def make(self, *, acceleration=False, duration=10., initial=None, angular=None):
        matrix = np.zeros((15, 15))
        matrix[:6, 6:12] = np.eye(6)
        if acceleration:
            matrix[9, 12] = 1.
        return CanalInterval(matrix, np.zeros(12) if initial is None else initial,
                             np.zeros(3) if angular is None else angular, duration, 0, 2.)

    def test_stationary_zero_remains_zero(self):
        interval = self.make(acceleration=True)
        for time in (0., .25, 1., 7., 10.):
            self.assertEqual(interval.point_at_ms(time), (0.,) * 12)
            self.assertEqual(interval.at_ms(time), 0.)

    def test_constant_velocity_uses_seconds_and_solid_cupula_coordinate(self):
        state = np.arange(12, dtype=float)
        interval = self.make(initial=state)
        for ms in (0., 2.5, 10.):
            expected = state.copy()
            expected[:6] += state[6:] * ms / 1000
            np.testing.assert_allclose(interval.point_at_ms(ms), expected, rtol=2e-14, atol=1e-14)
            self.assertAlmostEqual(interval.at_ms(ms), 2 * (3 + 9 * ms / 1000), places=13)

    def test_constant_acceleration_matches_quadratic_solution(self):
        interval = self.make(acceleration=True, angular=[3., 0., 0.])
        for ms in (0., .001, .25, 4., 10.):
            seconds = ms / 1000
            point = interval.point_at_ms(ms)
            self.assertAlmostEqual(point[3], .5 * 3 * seconds**2, delta=2e-18)
            self.assertAlmostEqual(point[9], 3 * seconds, delta=2e-15)
            self.assertAlmostEqual(interval.at_ms(ms), 3 * seconds**2, delta=4e-18)

    def test_oscillator_matches_trigonometric_solution(self):
        matrix = np.zeros((15, 15))
        omega = 23.
        matrix[3, 9] = 1.
        matrix[9, 3] = -omega**2
        initial = np.zeros(12)
        initial[3] = 1.
        interval = CanalInterval(matrix, initial, [0., 0., 0.], 100., 0, 2.)
        for ms in (0., .125, 37., 100.):
            q, v = interval.point_at_ms(ms)[3], interval.point_at_ms(ms)[9]
            self.assertAlmostEqual(q, math.cos(omega * ms / 1000), delta=2e-14)
            self.assertAlmostEqual(v, -omega * math.sin(omega * ms / 1000), delta=2e-12)

    def test_rejected_or_reordered_queries_do_not_advance_state(self):
        interval = self.make(acceleration=True, angular=[3., 0., 0.])
        expected = interval.point_at_ms(2.)
        interval.point_at_ms(9.)
        interval.point_at_ms(.01)
        self.assertEqual(interval.point_at_ms(2.), expected)
        self.assertEqual(interval.point_at_ms(0.), (0.,) * 12)
        with self.assertRaises(TypeError):
            expected[0] = 4.

    def test_constructor_copies_input_arrays(self):
        matrix = np.zeros((15, 15))
        state = np.zeros(12)
        angular = np.zeros(3)
        interval = CanalInterval(matrix, state, angular, 10., 0, 2.)
        matrix[:] = 1.
        state[:] = 2.
        angular[:] = 3.
        self.assertEqual(interval.point_at_ms(10.), (0.,) * 12)

    def test_changed_input_preserves_endpoint_and_cannot_affect_previous_interval(self):
        first = self.make(acceleration=True, angular=[3., 0., 0.])
        endpoint = first.point_at_ms(10.)
        second = first.continue_with([-6., 0., 0.])
        self.assertEqual(second.point_at_ms(0.), endpoint)
        self.assertEqual(first.point_at_ms(10.), endpoint)
        point = second.point_at_ms(4.)
        self.assertAlmostEqual(point[3], .5 * 3 * .01**2 + .03 * .004 - .5 * 6 * .004**2, delta=3e-18)
        self.assertAlmostEqual(point[9], .03 - 6 * .004, delta=3e-15)

    def test_each_held_step_has_one_constant_value_including_its_final_stage(self):
        interval = self.make(acceleration=True, angular=[3., 0., 0.])
        segments = [interval.held_step(index, 8) for index in range(8)]
        self.assertEqual(segments[0].start_ms, 0.)
        self.assertEqual(segments[-1].end_ms, 10.)
        for index, segment in enumerate(segments):
            self.assertEqual(segment.source_step, index)
            expected = 3 * (index * .00125)**2
            for ms in (segment.start_ms, (segment.start_ms + segment.end_ms) / 2, segment.end_ms):
                self.assertAlmostEqual(segment.at_ms(ms), expected, delta=4e-18)
            if index:
                self.assertEqual(segment.start_ms, segments[index - 1].end_ms)
                self.assertNotEqual(segment.at_ms(segment.start_ms), segments[index - 1].at_ms(segment.start_ms))

    def test_continuous_input_is_distinct_from_start_held_input(self):
        interval = self.make(acceleration=True, angular=[3., 0., 0.])
        held = interval.held_step(0, 8)
        self.assertEqual(held.at_ms(.625), 0.)
        self.assertAlmostEqual(interval.at_ms(.625), 3 * .000625**2, delta=1e-20)

    def test_invalid_queries_are_rejected_without_clipping(self):
        interval = self.make()
        for bad in (-1., np.nextafter(0., 1.), np.nextafter(10., np.inf), float('nan'), float('inf'), True, '1'):
            with self.subTest(value=bad), self.assertRaises(ValueError):
                interval.point_at_ms(bad)
        held = HeldBundleInput(2, 2., 3., -4.)
        for bad in (np.nextafter(2., -np.inf), np.nextafter(3., np.inf)):
            with self.subTest(value=bad), self.assertRaises(ValueError):
                held.at_ms(bad)

    def test_invalid_source_grids_and_held_records_are_rejected(self):
        interval = self.make()
        for index, count in ((0, 0), (0, 3), (-1, 8), (8, 8), (True, 8), (0, True), (0, 2**25)):
            with self.subTest(index=index, count=count), self.assertRaises(ValueError):
                interval.held_step(index, count)
        seed = HeldBundleInput(0, 0., 1., 0.)
        for changes in ({'end_ms': 0.}, {'start_ms': -1.}, {'bundle_nm': float('nan')}, {'source_step': True}):
            with self.subTest(changes=changes), self.assertRaises(ValueError):
                replace(seed, **changes)

    def test_invalid_generator_and_state_shapes_are_rejected(self):
        matrix = np.zeros((15, 15))
        matrix[12, 12] = 1.
        for generator, initial, angular, duration, canal, gain in (
                (matrix, np.zeros(12), np.zeros(3), 10., 0, 2.),
                (np.zeros((12, 12)), np.zeros(12), np.zeros(3), 10., 0, 2.),
                (np.zeros((15, 15)), np.zeros(11), np.zeros(3), 10., 0, 2.),
                (np.zeros((15, 15)), np.zeros(12), np.zeros(2), 10., 0, 2.),
                (np.zeros((15, 15)), np.zeros(12), np.zeros(3), 0., 0, 2.),
                (np.zeros((15, 15)), np.zeros(12), np.zeros(3), 10., 3, 2.),
                (np.zeros((15, 15)), np.zeros(12), np.zeros(3), 10., 0, 0.)):
            with self.subTest(duration=duration, canal=canal), self.assertRaises(ValueError):
                CanalInterval(generator, initial, angular, duration, canal, gain)

    def test_reference_derivative_and_jacobian_receive_same_time_and_original_state(self):
        calls = []

        class Receiver:
            def rates(self, state, forcing):
                calls.append(('rates', state, forcing))
                return [forcing['bundleDisplacementNanometers']]

            def jacobian(self, state, forcing):
                calls.append(('jacobian', state, forcing))
                return [[0.]]

        state = [7.]
        drive = DrivenSensoryEquation(Receiver(), HeldBundleInput(0, 0., 1., -2.))
        self.assertEqual(drive.rates(.5, state), [-2.])
        self.assertEqual(drive.jacobian(.5, state), [[0.]])
        self.assertIs(calls[0][1], state)
        self.assertIs(calls[1][1], state)
        self.assertEqual(calls[0][2], calls[1][2])
        self.assertEqual(state, [7.])

    def test_source_stiffness_recovers_declared_disk_and_bundle_tip(self):
        spec, catalog = self.geometry_fixture()
        projection = BundleProjection.from_spec(spec, catalog)
        self.assertAlmostEqual(projection.radius_m, .001, delta=2e-18)
        self.assertAlmostEqual(projection.area_m2, math.pi * 1e-6, delta=2e-20)
        self.assertAlmostEqual(projection.gain_nm_per_m3 / (1.5e9 / (math.pi * 1e-6)), 1., delta=2e-14)
        self.assertEqual(projection.hair_cell_id, spec['attachment']['hairCellId'])
        self.assertEqual(projection.organ_id, spec['geometry']['organId'])
        for canal in (0, 1, 2):
            changed = deepcopy(spec)
            changed['geometry']['canal'] = canal
            changed['attachment']['excitatoryPolarity'] = -1
            reverse = BundleProjection.from_spec(changed, catalog)
            self.assertEqual(reverse.gain_nm_per_m3, -projection.gain_nm_per_m3)

    def test_spatial_binding_rejects_foreign_sources_order_and_invalid_attachments(self):
        spec, catalog = self.geometry_fixture()
        changed = deepcopy(catalog)
        changed['retainedHtmlSha256'] = 'b' * 64
        with self.assertRaises(ValueError):
            BundleProjection.from_spec(spec, changed)
        changed = deepcopy(catalog)
        changed['stateOrder'] = list(reversed(changed['stateOrder']))
        with self.assertRaises(ValueError):
            BundleProjection.from_spec(spec, changed)
        for key, value in (('rootXOverRadius', 1.), ('bundleLengthMeters', .003),
                           ('excitatoryPolarity', 0), ('excitatoryPolarity', True),
                           ('hairCellId', '00000000-0000-0000-0000-000000000000')):
            changed = deepcopy(spec)
            changed['attachment'][key] = value
            with self.subTest(key=key, value=value), self.assertRaises(ValueError):
                BundleProjection.from_spec(changed, catalog)


if __name__ == '__main__':
    if not __debug__:
        raise RuntimeError('Assertions must remain enabled.')
    unittest.main(verbosity=2)
