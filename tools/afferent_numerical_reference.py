"""Independent Decimal channels, graph Laplacian and full sensory charge solve.

Only execute numerical functions in the isolated visible study. No candidate
assembly, candidate currents or candidate gate derivatives feed this reference.
"""
from copy import deepcopy
from decimal import Decimal
from decimal import localcontext
import importlib.util
import json
import math
from pathlib import Path
import traceback
from cleft_spatial_reference import value as d
from cleft_membrane_reference import evaluate as sensory_evaluate
from afferent_numerical_protocol import GATES

ROOT=Path(__file__).resolve().parents[1]
s=importlib.util.spec_from_file_location('afferent_full_matrix_reference',ROOT/'tools/analyse-cleft-coupling.py')
base=importlib.util.module_from_spec(s);s.loader.exec_module(base)


def sigmoid(x):return 1/(1+(-x).exp())


def time_from_source(voltage,points,activation):
    v=d(voltage);xy=[(d(p['voltageMv']),d(p['tauMs'])) for p in points]
    if activation and (v<xy[0][0] or v>xy[-1][0]):return d(.2)
    if v<=xy[0][0]:a,b=xy[:2]
    elif v>=xy[-1][0]:a,b=xy[-2:]
    else:a,b=next((a,b) for a,b in zip(xy,xy[1:]) if a[0]<=v<=b[0])
    result=((b[0]-v)*a[1]+(v-a[0])*b[1])/(b[0]-a[0]);assert result.is_finite() and result>0
    return result


def rates(v,source):
    v=d(v);tables=source['tables']
    result=dict(sodiumActivation=dict(equilibrium=sigmoid((v+d(40.1))/d(7.5)),tauMs=time_from_source(v,tables['ActivationTimes']['points'],True)),
        sodiumInactivation=dict(equilibrium=sigmoid(-(v+d(78.8))/6),tauMs=time_from_source(v,tables['InactivationTimes']['points'],False)),
        kv1Activation=dict(equilibrium=sigmoid((v+44)/d(7.1)),tauMs=d(3.7)),
        kv7Activation=dict(equilibrium=sigmoid((v+41)/d(7.4)),tauMs=Decimal(54)),
        kv34Activation=dict(equilibrium=sigmoid((v+d(31.3))/d(8.5)),tauMs=5*d(1.41)*(-d(.05589)*v).exp()*sigmoid((v+d(31.3))/d(8.5))),
        kv34Inactivation=dict(equilibrium=sigmoid(-(v+d(65.84))/d(5.51)),tauMs=d(25.4)))
    assert all(0<=r['equilibrium']<=1 and r['tauMs'].is_finite() and r['tauMs']>0 for r in result.values())
    return result


def membrane(voltage,gates,parameters,source):
    v=d(voltage);g={k:d(gates[k]) for k in GATES};p={k:d(x) for k,x in parameters.items()}
    assert v.is_finite() and all(0<=x<=1 for x in g.values())
    r=rates(v,source);ek=p['thermalVoltageMv']*(p['outsidePotassiumMm']/p['insidePotassiumMm']).ln();ena=p['thermalVoltageMv']*(p['outsideSodiumMm']/p['insideSodiumMm']).ln()
    current=dict(sodiumPaPerSquareMicrometer=p['sodiumMaximumNsPerSquareMicrometer']*g['sodiumActivation']**3*g['sodiumInactivation']*(v-ena),
        kv1PotassiumPaPerSquareMicrometer=p['kv1MaximumNsPerSquareMicrometer']*g['kv1Activation']*(v-ek),
        kv7PotassiumPaPerSquareMicrometer=p['kv7MaximumNsPerSquareMicrometer']*g['kv7Activation']*(v-ek),
        kv34PotassiumPaPerSquareMicrometer=p['kv34MaximumNsPerSquareMicrometer']*g['kv34Activation']*g['kv34Inactivation']*(v-ek))
    current['potassiumPaPerSquareMicrometer']=sum(current[k] for k in ('kv1PotassiumPaPerSquareMicrometer','kv7PotassiumPaPerSquareMicrometer','kv34PotassiumPaPerSquareMicrometer'))
    current['totalOutwardPaPerSquareMicrometer']=current['sodiumPaPerSquareMicrometer']+current['potassiumPaPerSquareMicrometer']
    return dict(membraneVoltageMv=v,potassiumReversalMv=ek,sodiumReversalMv=ena,rates=r,
        gateDerivatives={k+'PerMs':(r[k]['equilibrium']-g[k])/r[k]['tauMs'] for k in GATES},currents=current)


def clamp(case,source):
    r=rates(case['voltageMv'],source);dt=d(case['elapsedMs']);assert dt>=0
    return {k:r[k]['equilibrium']+(d(case['gates'][k])-r[k]['equilibrium'])*(-dt/r[k]['tauMs']).exp() for k in GATES}


def cable(case,source):
    topology=case['topology'];nodes=sorted(topology['nodes'],key=lambda n:n['id']);indices={n['id']:i for i,n in enumerate(nodes)}
    states={s['id']:s for s in case['states']};boundary={t['id']:d(t['potentialMv']) for t in case['terminalStates']};bath=d(case['perilymphMv']);n=len(nodes)
    voltage=[d(states[node['id']]['potentialMv']) for node in nodes];relative=[v-bath for v in voltage]
    # Assemble an independent dense conductance Laplacian and boundary source.
    laplacian=[[Decimal(0) for _ in nodes] for _ in nodes];inflow=[Decimal(0) for _ in nodes];edges=[];terminals=[]
    for e in sorted(topology['edges'],key=lambda e:e['id']):
        a=indices[e['leftId']];b=indices[e['rightId']];g=d(e['conductivityNsPerMicrometer'])*d(e['crossSectionSquareMicrometers'])/d(e['centerDistanceMicrometers'])
        laplacian[a][a]+=g;laplacian[b][b]+=g;laplacian[a][b]-=g;laplacian[b][a]-=g
        delta=voltage[a]-voltage[b];flow=g*delta
        edges.append(dict(id=e['id'],leftId=e['leftId'],rightId=e['rightId'],conductanceNs=g,leftToRightPa=flow,dissipationFw=g*delta*delta))
    for t in sorted(topology['terminals'],key=lambda t:t['id']):
        i=indices[t['nodeId']];g=d(t['conductanceNs']);outside=boundary[t['id']];laplacian[i][i]+=g;inflow[i]+=g*(outside-bath)
        delta=voltage[i]-outside;terminals.append(dict(id=t['id'],nodeId=t['nodeId'],nodeToTerminalPa=g*delta,dissipationFw=g*delta*delta))
    node_results=[];charges=[];membrane_power=Decimal(0);energy=Decimal(0)
    for i,node in enumerate(nodes):
        area=2*d(math.pi)*d(node['radiusMicrometers'])*d(node['lengthMicrometers']);cap=area*d(node['membrane']['specificCapacitancePfPerSquareMicrometer'])
        evaluation=membrane(relative[i],states[node['id']]['gates'],node['membrane'],source);current=area*evaluation['currents']['totalOutwardPaPerSquareMicrometer']
        axial=sum(g*v for g,v in zip(laplacian[i],relative,strict=True))-inflow[i];dq=-current-axial;charges.append(dq)
        node_results.append(dict(id=node['id'],areaSquareMicrometers=area,capacitancePf=cap,membrane=evaluation,membraneOutwardPa=current,
            netAxialOutwardPa=axial,chargeRatePa=dq,potentialMvPerMs=dq/cap))
        energy+=cap*relative[i]**2/2;membrane_power+=current*relative[i]
    total_membrane=sum(n['membraneOutwardPa'] for n in node_results);total_terminal=sum(t['nodeToTerminalPa'] for t in terminals);charge=sum(charges)
    energy_rate=sum(v*q for v,q in zip(relative,charges,strict=True));terminal_power=sum(t['nodeToTerminalPa']*(boundary[t['id']]-bath) for t in terminals)
    dissipation=sum(e['dissipationFw'] for e in edges)+sum(t['dissipationFw'] for t in terminals)
    return dict(nodes=node_results,edges=edges,terminals=terminals,totalMembraneOutwardPa=total_membrane,totalTerminalOutwardPa=total_terminal,
        totalChargeRatePa=charge,chargeResidualPa=charge+total_membrane+total_terminal,storedEnergyAttojoules=energy,storedEnergyRateFw=energy_rate,
        membranePowerFw=membrane_power,terminalPowerOutwardFw=terminal_power,axialDissipationFw=dissipation,powerResidualFw=energy_rate+membrane_power+terminal_power+dissipation)


def joint(case,source):
    f=deepcopy(case['sensory']);states={s['id']:s for s in f['states']};targets={a['terminalId']:a['cleftCompartmentId'] for a in case['attachments']}
    boundary=[dict(id=t['id'],potentialMv=states[targets[t['id']]]['calyxMv'] if t['id'] in targets else f['perilymphMv']) for t in case['cableTopology']['terminals']]
    c=cable(dict(topology=case['cableTopology'],perilymphMv=f['perilymphMv'],states=case['cableStates'],terminalStates=boundary),source)
    outgoing={i:Decimal(0) for i in states};transfers=[];calyx_power=Decimal(0)
    for t in c['terminals']:
        if t['id'] not in targets:assert t['nodeToTerminalPa']==0;continue
        target=targets[t['id']];flow=-t['nodeToTerminalPa'];outgoing[target]+=flow
        transfers.append(dict(terminalId=t['id'],cleftCompartmentId=target,calyxToCablePa=flow,cableToCalyxPa=t['nodeToTerminalPa']))
        calyx_power+=flow*(d(states[target]['calyxMv'])-d(f['perilymphMv']))
    for entry in f['inputs']:entry['axonOutgoingPa']=outgoing[entry['id']]
    sensory=sensory_evaluate(f,base.edge,base.dense_solve)
    return dict(sensory=sensory,cable=c,transfers=transfers,
        interfaceChargeResidualPa=sum(t['calyxToCablePa']+t['cableToCalyxPa'] for t in transfers),interfacePowerResidualFw=calyx_power+c['terminalPowerOutwardFw'])


def run(package,spec,source):
    from afferent_numerical_analysis import strings
    output=package/'independent';output.mkdir();failures=[]
    evaluators=dict(membranes=lambda c:membrane(c['voltageMv'],c['gates'],c['parameters'],source),
        clamps=lambda c:clamp(c,source),cables=lambda c:cable(c,source),joints=lambda c:joint(c,source))
    for group,evaluate in evaluators.items():
        print('INDEPENDENT 60/90-DIGIT SENSORY NERVE',group,len(spec[group]),flush=True)
        with (output/(group+'.jsonl')).open('x',encoding='utf-8') as stream:
            for case in spec[group]:
                result=dict(input=case,precisions=spec['decimalPrecisions'],completed=False,witnesses=[])
                try:
                    for precision in spec['decimalPrecisions']:
                        with localcontext() as ctx:
                            ctx.prec=precision;result['witnesses'].append(strings(evaluate(case)))
                    result['completed']=True
                except Exception:
                    result['error']=traceback.format_exc();failures.append(dict(group=group,key=case['key']))
                stream.write(json.dumps(result,allow_nan=False)+'\n');stream.flush()
    (output/'results.json').write_text(json.dumps(dict(completed=True,counts=spec['expectedCounts'],precisions=spec['decimalPrecisions'],
        failures=failures,timeIntegrated=False,afferentFiringQualified=False,physiologyQualified=False),indent=2)+'\n',encoding='utf-8')
