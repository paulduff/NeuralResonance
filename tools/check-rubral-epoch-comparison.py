"""Prospective identity-mapping controls; run only in visible PowerShell."""
import argparse
from copy import deepcopy
import json
from pathlib import Path
import traceback
import rubral_epoch_comparison as mapping


def fixture(session):
    projection=mapping.stable(mapping.CIRCUIT,session,'motor-projection');synapse=mapping.stable(mapping.CIRCUIT,session,'motor-synapse')
    identity=dict(Instance=mapping.INSTANCE,Neuron=mapping.NEURON,Projection=projection,Synapse=synapse)
    contact=dict(sourceInstanceKey=mapping.INSTANCE,sourceNeuronId=mapping.NEURON,projectionId=projection,
        contactId=mapping.stable('motor-endplate-group-v1',mapping.INSTANCE,mapping.NEURON,projection,mapping.UNIT),motorUnitId=mapping.UNIT,conductionDelayMs=2.0625)
    initial=dict(session=session,scenario='strong-left',activeContact=contact,motorClock=dict(sessionId=session,neuralEpochMs=1000,bodyEpochMs=0))
    case=dict(key='strong-left',durationMs=499.9375,events=[dict(muscleIndex=0,arrivalAtMs=t,
        originalMessageId=mapping.stable(mapping.EMISSION,session,mapping.INSTANCE,mapping.NEURON,projection,synapse,i),
        originalSourceNeuronId=mapping.NEURON,originalContactId=contact['contactId']) for i,t in enumerate((10.,20.,30.),1)])
    return [initial,dict(identity=identity),case]


def run(output):
    assert not output.exists()
    original=fixture('ac4f324f-5c8c-4c8a-b0ca-527d2fc60ad2');fresh=fixture('57116667-47bd-451c-a984-e1365905324a')
    rows=[]
    def check(name,candidate,accept,original_case=None):
        observed=None;error=None;unexpected=False
        try:observed=mapping.compare(*(original_case or original),*candidate)
        except AssertionError as exc:error=str(exc) or 'AssertionError'
        except Exception:error=traceback.format_exc();unexpected=True
        passed=not unexpected and (observed is not None)==accept and (observed is not None or error is not None)
        rows.append(dict(name=name,expectedAccepted=accept,observedAccepted=observed is not None,passed=passed,error=error,unexpectedException=unexpected,result=observed))
    check('fresh-epoch-same-physical-events',fresh,True)
    check('reused-session',deepcopy(original),False)
    alterations=[('wrong-contact',lambda c:c[2]['events'][0].update(originalContactId=original[0]['activeContact']['contactId'])),
        ('wrong-message-epoch',lambda c:c[2]['events'][0].update(originalMessageId=original[2]['events'][0]['originalMessageId'])),
        ('changed-neuron',lambda c:c[2]['events'][0].update(originalSourceNeuronId='another-neuron')),
        ('changed-muscle',lambda c:c[2]['events'][0].update(muscleIndex=1)),
        ('changed-arrival',lambda c:c[2]['events'][0].update(arrivalAtMs=10.000000000000002)),
        ('missing-event',lambda c:c[2]['events'].pop()),
        ('changed-delay',lambda c:c[0]['activeContact'].update(conductionDelayMs=3.)),
        ('changed-clock',lambda c:c[0]['motorClock'].update(neuralEpochMs=999)),
        ('changed-route',lambda c:c[1]['identity'].update(Projection=original[1]['identity']['Projection']))]
    for name,alter in alterations:
        c=deepcopy(fresh);alter(c);check(name,c,False)
    damaged=deepcopy(original);damaged[2]['events'][0]['originalContactId']=fresh[0]['activeContact']['contactId']
    check('invalid-original-contact',fresh,False,damaged)
    result=dict(completed=True,passed=all(r['passed'] for r in rows),cases=rows,mappingVersion=mapping.VERSION,
        controlsExecuted=True,modelsExecuted=False,nativePhysicsExecuted=False,originalResultsModified=False)
    with output.open('x',encoding='utf-8') as stream:json.dump(result,stream,indent=2,allow_nan=False)
    if not result['passed']:raise RuntimeError('Epoch identity controls failed; retain the complete result.')
    print(f'{len(rows)} epoch identity controls passed; no model or body executed.',flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser();parser.add_argument('output',type=Path);run(parser.parse_args().output.resolve())
