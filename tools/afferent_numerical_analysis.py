"""Audit saved derivative witnesses and electrical accounting; no model rerun."""
from copy import deepcopy
from decimal import Decimal,localcontext
import json
import re
from cleft_spatial_reference import value as d
from cleft_rest_analysis import comparison
from afferent_numerical_protocol import validate


def read(path):return json.loads(path.read_text(encoding='utf-8-sig'))
def rows(path):return [json.loads(line) for line in path.read_text(encoding='utf-8-sig').splitlines()]


def strings(value):
    if isinstance(value,Decimal):return str(value)
    if isinstance(value,dict):return {k:strings(v) for k,v in value.items()}
    if isinstance(value,(list,tuple)):return [strings(v) for v in value]
    return value


def ordinary_cable(value):
    # These two fields are NOT ignored: conservation() checks their reported
    # and independently re-summed values under the prospective roundoff bound.
    return {k:v for k,v in value.items() if k not in ('chargeResidualPa','powerResidualFw')}


def agreement(group,a,b,spec):
    if group=='joints':
        result=dict(sensory=comparison(a['sensory'],b['sensory'],spec['sensoryComparison']),
            cable=comparison(ordinary_cable(a['cable']),ordinary_cable(b['cable']),spec['cableComparison']),
            transfers=comparison(a['transfers'],b['transfers'],spec['cableComparison']))
    else:
        settings=spec[dict(membranes='membraneComparison',clamps='clampComparison',cables='cableComparison')[group]]
        result=dict(values=comparison(ordinary_cable(a) if group=='cables' else a,ordinary_cable(b) if group=='cables' else b,settings))
    return dict(passed=all(r['passed'] for r in result.values()),groups=result)


class Accounting:
    def __init__(self,settings):self.settings=settings;self.checks=[]
    def check(self,name,residual,terms,kind='current'):
        residual=d(residual);scale=sum(abs(d(x)) for x in terms)
        bound=d(self.settings['absoluteCurrentPa' if kind=='current' else 'absolutePowerFw'])+d(self.settings['roundoffMultiplier'])*d(self.settings['binary64Epsilon'])*scale
        assert residual.is_finite() and scale.is_finite() and bound>0
        self.checks.append(dict(name=name,kind=kind,residual=residual,scale=scale,bound=bound,passed=abs(residual)<=bound))
    def result(self):return dict(passed=bool(self.checks) and all(c['passed'] for c in self.checks),checks=strings(self.checks))


def conservation(case,value,settings,account=None):
    account=account or Accounting(settings)
    topology=case['topology'];states={s['id']:s for s in case['states']};bath=d(case['perilymphMv'])
    boundary={t['id']:d(t['potentialMv'])-bath for t in case['terminalStates']}
    assert [n['id'] for n in value['nodes']]==sorted(states)
    assert [e['id'] for e in value['edges']]==sorted(e['id'] for e in topology['edges'])
    assert [t['id'] for t in value['terminals']]==sorted(t['id'] for t in topology['terminals'])
    edge_specs={e['id']:e for e in topology['edges']};terminal_specs={t['id']:t for t in topology['terminals']}
    vm={i:d(s['potentialMv'])-bath for i,s in states.items()};incidence={i:[] for i in states};dissipation=[];terminal_power=[]
    for e in value['edges']:
        spec=edge_specs[e['id']];assert (e['leftId'],e['rightId'])==(spec['leftId'],spec['rightId'])
        flow=d(e['leftToRightPa']);delta=vm[e['leftId']]-vm[e['rightId']]
        assert d(e['conductanceNs'])>0 and d(e['dissipationFw'])>=0 and flow*delta>=0
        incidence[e['leftId']].append(flow);incidence[e['rightId']].append(-flow)
        expected=flow*delta;dissipation.append(d(e['dissipationFw']))
        account.check('edge-power/'+e['id'],d(e['dissipationFw'])-expected,[e['dissipationFw'],expected],'power')
    for t in value['terminals']:
        spec=terminal_specs[t['id']];assert t['nodeId']==spec['nodeId']
        flow=d(t['nodeToTerminalPa']);delta=vm[t['nodeId']]-boundary[t['id']]
        assert d(t['dissipationFw'])>=0 and flow*delta>=0
        if spec['conductanceNs']==0:assert flow==0 and d(t['dissipationFw'])==0
        incidence[t['nodeId']].append(flow);expected=flow*delta;dissipation.append(d(t['dissipationFw']))
        terminal_power.append(flow*boundary[t['id']])
        account.check('terminal-power/'+t['id'],d(t['dissipationFw'])-expected,[t['dissipationFw'],expected],'power')
    charges=[];membrane=[];energy_rates=[];membrane_power=[]
    for n in value['nodes']:
        i=n['id'];q=d(n['chargeRatePa']);m=d(n['membraneOutwardPa']);axial=sum(incidence[i])
        assert d(n['capacitancePf'])>0 and d(n['areaSquareMicrometers'])>0
        account.check('node-incidence/'+i,d(n['netAxialOutwardPa'])-axial,incidence[i]+[n['netAxialOutwardPa']])
        account.check('node-charge/'+i,q+m+axial,[q,m]+incidence[i])
        cv=d(n['capacitancePf'])*d(n['potentialMvPerMs']);account.check('node-capacitance/'+i,cv-q,[cv,q])
        im=d(n['areaSquareMicrometers'])*d(n['membrane']['currents']['totalOutwardPaPerSquareMicrometer'])
        account.check('node-membrane/'+i,m-im,[m,im])
        charges.append(q);membrane.append(m);energy_rates.append(vm[i]*q);membrane_power.append(vm[i]*m)
    terminals=[d(t['nodeToTerminalPa']) for t in value['terminals']]
    for name,terms in [('totalMembraneOutwardPa',membrane),('totalTerminalOutwardPa',terminals),('totalChargeRatePa',charges)]:
        account.check(name,d(value[name])-sum(terms),terms+[value[name]])
    for name,terms in [('storedEnergyRateFw',energy_rates),('membranePowerFw',membrane_power),('terminalPowerOutwardFw',terminal_power),('axialDissipationFw',dissipation)]:
        account.check(name,d(value[name])-sum(terms),terms+[value[name]],'power')
    charge_terms=charges+membrane+terminals;power_terms=energy_rates+membrane_power+terminal_power+dissipation
    for name,terms,kind in [('chargeResidualPa',charge_terms,'current'),('powerResidualFw',power_terms,'power')]:
        account.check('recomputed/'+name,sum(terms),terms,kind)
        account.check('reported/'+name,value[name],terms,kind)
        account.check('residual-agreement/'+name,d(value[name])-sum(terms),terms+[value[name]],kind)
    return account


def joint_conservation(case,value,settings):
    f=case['sensory'];states={s['id']:s for s in f['states']};targets={a['terminalId']:a['cleftCompartmentId'] for a in case['attachments']}
    boundary=[dict(id=t['id'],potentialMv=states[targets[t['id']]]['calyxMv'] if t['id'] in targets else f['perilymphMv']) for t in case['cableTopology']['terminals']]
    account=conservation(dict(topology=case['cableTopology'],states=case['cableStates'],terminalStates=boundary,perilymphMv=f['perilymphMv']),value['cable'],settings)
    terminals={t['id']:t for t in value['cable']['terminals']};flows={i:[] for i in states};power=[];charge=[]
    assert [t['terminalId'] for t in value['transfers']]==sorted(targets)
    for t in value['transfers']:
        i=t['terminalId'];target=targets[i];assert t['cleftCompartmentId']==target
        outgoing=d(t['calyxToCablePa']);incoming=d(t['cableToCalyxPa'])
        assert incoming==d(terminals[i]['nodeToTerminalPa'])
        flows[target].append(outgoing);charge.extend((outgoing,incoming));power.append(outgoing*(d(states[target]['calyxMv'])-d(f['perilymphMv'])))
        account.check('interface/'+i,outgoing+incoming,[outgoing,incoming])
    for m in value['sensory']['membranes']:
        i=m['id'];account.check('calyx-port/'+i,d(m['port']['calyxToAxonPa'])-sum(flows[i]),flows[i]+[m['port']['calyxToAxonPa']])
    power.append(d(value['cable']['terminalPowerOutwardFw']))
    for field,terms,kind in [('interfaceChargeResidualPa',charge,'current'),('interfacePowerResidualFw',power,'power')]:
        account.check('recomputed/'+field,sum(terms),terms,kind)
        account.check('reported/'+field,value[field],terms,kind)
        account.check('residual-agreement/'+field,d(value[field])-sum(terms),terms+[value[field]],kind)
    return account


def fingerprint(group,case):
    if group=='membranes':return case['parameters']
    if group=='cables':return case['topology']
    f=case['sensory']
    return dict(sensory={k:f[k] for k in ('topology','hairParameters','calyxParameters')},topology=case['cableTopology'],attachments=case['attachments'])


def analyse(package):
    spec=validate(read(package/'numerical-input.json'));source=read(package/'source-decisions.json')
    native=read(package/'study/results.json');reference=read(package/'independent/results.json')
    assert native==dict(completed=True,counts=spec['expectedCounts'],timeIntegrated=False,afferentFiringQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    assert reference['completed'] and reference['counts']==spec['expectedCounts'] and reference['precisions']==[60,90]
    assert not reference['timeIntegrated'] and not reference['afferentFiringQualified'] and not reference['physiologyQualified']
    assert read(package/'study/input.json')=={k:spec[k] for k in ('version','membraneVersion','cableVersion','jointVersion','expectedCounts','membranes','clamps','cables','joints')}
    assert read(package/'study/timing-tables.json')==dict(sodiumActivationTable=source['tables']['ActivationTimes']['points'],sodiumInactivationTable=source['tables']['InactivationTimes']['points'])
    summaries=[];fingerprints={};controls=[]
    with localcontext() as ctx:
        ctx.prec=100
        for group in spec['expectedCounts']:
            candidates=rows(package/'study'/(group+'.jsonl'));witnesses=rows(package/'independent'/(group+'.jsonl'))
            assert len(candidates)==len(witnesses)==spec['expectedCounts'][group]
            for case,n,r in zip(spec[group],candidates,witnesses,strict=True):
                assert n['input']==case and r['input']==case and r['precisions']==[60,90]
                if group!='clamps':
                    assert re.fullmatch('[0-9A-Fa-f]{64}',n['fingerprint'])
                    signature=json.dumps(fingerprint(group,case),sort_keys=True)
                    assert fingerprints.setdefault((group,signature),n['fingerprint'])==n['fingerprint']
                result=dict(group=group,key=case['key'],completed=r['completed'],passed=False)
                try:
                    assert r['completed'],r.get('error','Reference incomplete.')
                    lo,hi=r['witnesses'];result['precision']=comparison(lo,hi,spec['referenceComparison'])
                    result['candidate']=agreement(group,n['evaluation'],hi,spec)
                    if group in ('cables','joints'):
                        audit=conservation if group=='cables' else joint_conservation
                        result['conservation']=audit(case,n['evaluation'],spec['conservation']).result()
                        result['referenceConservation']=[audit(case,w,spec['conservation']).result() for w in (lo,hi)]
                    if group=='cables' and case['key'].startswith('analytic-'):
                        v=n['evaluation'];driven=case['key']=='analytic-driven'
                        observed=dict(edgePa=v['edges'][0]['leftToRightPa'],edgeDissipationFw=v['edges'][0]['dissipationFw'],
                            terminalPa=v['terminals'][0]['nodeToTerminalPa'],terminalDissipationFw=v['terminals'][0]['dissipationFw'],
                            charges=[x['chargeRatePa'] for x in v['nodes']],storedEnergyRateFw=v['storedEnergyRateFw'],terminalPowerOutwardFw=v['terminalPowerOutwardFw'])
                        expected=dict(edgePa=-30,edgeDissipationFw=300,terminalPa=-40 if driven else 0,terminalDissipationFw=800 if driven else 0,
                            charges=[70 if driven else 30,-30],storedEnergyRateFw=-300,terminalPowerOutwardFw=-800 if driven else 0)
                        result['analyticControl']=comparison(observed,expected,spec['cableComparison']);controls.append(dict(key=case['key'],observed=observed,expected=expected,**result['analyticControl']))
                    result['passed']=result['precision']['passed'] and result['candidate']['passed'] and result.get('conservation',{'passed':True})['passed'] and all(x['passed'] for x in result.get('referenceConservation',[])) and result.get('analyticControl',{'passed':True})['passed']
                except Exception as exc:result['error']=type(exc).__name__+': '+str(exc)
                summaries.append(result)
    assert len(controls)==2
    assert reference['failures']==[dict(group=r['group'],key=r['key']) for r in summaries if not r['completed']]
    return dict(completed=True,numericalCriteriaPassed=all(r['passed'] for r in summaries),counts=spec['expectedCounts'],cases=summaries,analyticControls=controls,
        independentDerivativeComparisonPerformed=True,timeIntegrated=False,stationaryInitializationQualified=False,spatialGeometryCalibrated=False,
        afferentFiringQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)
