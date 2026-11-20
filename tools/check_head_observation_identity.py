"""Strict format controls: property names may differ only by declared schema."""
import copy
import json
import unittest
from head_observation_identity import original_observation, SCALARS, VECTORS


class HeadObservationIdentityControls(unittest.TestCase):
    def fixture(self):
        actual = dict(PhysicsStep=2, SampleId='original-sample', HeadBodyId='head', SensorId='centre')
        for i,name in enumerate(VECTORS): actual[name] = dict(X=i*.125,Y=-i*.25,Z=i*.5)
        expected = {n[0].lower()+n[1:]:v if n in SCALARS else {k.lower():x for k,x in v.items()} for n,v in actual.items()}
        return actual, expected

    def test_exact_values_in_both_declared_schemas(self):
        actual, expected = self.fixture()
        self.assertTrue(original_observation(json.dumps(actual),expected))

    def test_every_identity_is_checked(self):
        actual, expected = self.fixture()
        for name in SCALARS:
            changed=copy.deepcopy(actual);changed[name]=3 if name=='PhysicsStep' else 'another-identity'
            with self.assertRaises(ValueError): original_observation(json.dumps(changed),expected)

    def test_every_vector_component_is_exact(self):
        actual, expected = self.fixture()
        for name in VECTORS:
            for axis in ('X','Y','Z'):
                changed=copy.deepcopy(actual);changed[name][axis]+=1e-12
                with self.assertRaises(ValueError): original_observation(json.dumps(changed),expected)

    def test_missing_or_added_properties_reject(self):
        actual, expected = self.fixture()
        for name in SCALARS+VECTORS:
            changed=copy.deepcopy(actual);del changed[name]
            with self.assertRaises(ValueError): original_observation(json.dumps(changed),expected)
        actual['extra']=0
        with self.assertRaises(ValueError): original_observation(json.dumps(actual),expected)

    def test_duplicate_original_properties_reject(self):
        actual, expected = self.fixture()
        raw=json.dumps(actual).replace('"PhysicsStep": 2','"PhysicsStep": 2, "PhysicsStep": 2')
        with self.assertRaises(ValueError): original_observation(raw,expected)
        raw=json.dumps(actual).replace('"X": 0.0','"X": 0.0, "X": 0.0',1)
        with self.assertRaises(ValueError): original_observation(raw,expected)

    def test_mixed_case_aliases_and_extra_vector_axes_reject(self):
        actual, expected = self.fixture();actual['physicsStep']=actual.pop('PhysicsStep')
        with self.assertRaises(ValueError): original_observation(json.dumps(actual),expected)
        actual, expected = self.fixture();actual['UpWorld']['w']=0
        with self.assertRaises(ValueError): original_observation(json.dumps(actual),expected)
        actual, expected = self.fixture();expected['upWorld']['X']=expected['upWorld'].pop('x')
        with self.assertRaises(ValueError): original_observation(json.dumps(actual),expected)

    def test_nonfinite_and_boolean_numbers_reject(self):
        for value in (float('nan'),float('inf'),True):
            actual, expected = self.fixture();actual['UpWorld']['X']=value;expected['upWorld']['x']=value
            with self.assertRaises(ValueError): original_observation(json.dumps(actual),expected)
        actual, expected = self.fixture();actual['PhysicsStep']=True;expected['physicsStep']=True
        with self.assertRaises(ValueError): original_observation(json.dumps(actual),expected)

    def test_wrong_expected_schema_rejects(self):
        actual, expected = self.fixture();expected['sampleID']=expected.pop('sampleId')
        with self.assertRaises(ValueError): original_observation(json.dumps(actual),expected)


if __name__=='__main__': unittest.main(verbosity=2)
