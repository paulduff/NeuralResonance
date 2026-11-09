"""Same motor protocols and limits; only endpoint-based step scheduling differs."""
from motoneuron_active_protocol import protocol as original_protocol

def protocol():
    result=original_protocol()
    result['version']='motoneuron-boundary-comparison-v1'
    return result
