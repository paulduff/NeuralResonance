"""Synthetic receipt controls; run only in the independent PowerShell study."""
import copy
import importlib.util
from pathlib import Path
import unittest

root=Path(__file__).resolve().parents[1]
s=importlib.util.spec_from_file_location('population_receipt_analysis',root/'tools/native_population_analysis.py');analysis=importlib.util.module_from_spec(s);s.loader.exec_module(analysis)

def fixture():
    moving=[dict(operation='hello',sequence=0,physicsStep=0),dict(operation='prelude',sequence=1,physicsStep=1)]
    ack=copy.deepcopy(moving);replies=[copy.deepcopy(moving[1])]
    for i in range(2,42):
        advanced=dict(operation='step',sequence=2*i,physicsStep=i,stateHash='state-'+str(i),observationsJson='original-head-'+str(i),
            physicalTimeMs=i*5,paths=[dict(length=0.3)],geometryJson='original-geometry')
        captured=dict(advanced,operation='inspect',sequence=advanced['sequence']+1)
        moving.append(advanced);ack.append(captured);replies.extend(copy.deepcopy([advanced,captured]))
    return moving,ack,replies

class ReceiptControls(unittest.TestCase):
    def test_exact_immediate_inspections(self):
        values=fixture();before=copy.deepcopy(values);result=analysis.reconcile_endpoint_receipts(*values)
        self.assertEqual(len(result),40);self.assertEqual(values,before)
    def test_original_wrong_operation(self):
        m,a,r=fixture();a[2]['operation']='step'
        with self.assertRaises(AssertionError):analysis.reconcile_endpoint_receipts(m,a,r)
    def test_sequence_gap(self):
        m,a,r=fixture();a[2]['sequence']+=10
        with self.assertRaises(AssertionError):analysis.reconcile_endpoint_receipts(m,a,r)
    def test_payload_change_even_if_wire_agrees(self):
        for key,value in [('stateHash','changed'),('observationsJson','changed'),('physicalTimeMs',123),('paths',[]),('geometryJson','changed')]:
            with self.subTest(field=key):
                m,a,r=fixture();a[2][key]=value;r[2][key]=value
                with self.assertRaises(AssertionError):analysis.reconcile_endpoint_receipts(m,a,r)
    def test_missing_wire_receipt(self):
        m,a,r=fixture();r.pop(2)
        with self.assertRaises(KeyError):analysis.reconcile_endpoint_receipts(m,a,r)
    def test_duplicate_wire_sequence(self):
        m,a,r=fixture();r.append(copy.deepcopy(r[2]))
        with self.assertRaises(AssertionError):analysis.reconcile_endpoint_receipts(m,a,r)
    def test_initial_payload_changed(self):
        m,a,r=fixture();a[0]['physicsStep']=1
        with self.assertRaises(AssertionError):analysis.reconcile_endpoint_receipts(m,a,r)
    def test_extra_payload_field_is_not_ignored(self):
        m,a,r=fixture();a[2]['unexpected']=1;r[2]['unexpected']=1
        with self.assertRaises(AssertionError):analysis.reconcile_endpoint_receipts(m,a,r)

if __name__=='__main__':unittest.main(verbosity=2)
