"""Independent Decimal membrane currents/gates coupled to the full open graph.

No candidate import, generated candidate current or candidate derivative is used.
The deposited reductions and binary64 constants match the declared source scope.
Execute only in the visible independent numerical window.
"""
from decimal import Decimal
from cleft_spatial_reference import value as d
from cleft_open_reference import evaluate as open_graph


def logistic(x): return 1/(1+(-x).exp())


def hcn_time(v):
    return d(209.479)+(((v+d(80.646))/d(6.916)).exp()+((v+d(80.646))/d(14.881)).exp())/d(2551.988)


def hair_rates(v):
    return dict(potassiumLow=dict(equilibrium=logistic((v+80)/d(2.84)),tauMs=10+d(429.7)*(-d(.2826)*(v+80)/d(2.84)).exp()),
        calcium=dict(equilibrium=logistic((v+44)/d(5.8)),tauMs=d(.6)),
        hcn1=dict(equilibrium=logistic(-(v+90)/d(6.8)),tauMs=hcn_time(v)))


def calyx_rates(v):
    return dict(kv74=dict(equilibrium=logistic((v+52)/16),
        tauMs=max(Decimal(1),1/(d(.0002488)*(-d(.04401)*v).exp()+d(.4506)*(d(.05437)*v).exp()))),
        hcn2=dict(equilibrium=logistic(-(v+95)/d(11.7)),tauMs=hcn_time(v)))


def pump(k,density,area): return d(96.485)*d(.1)*density*area/602000*(k/(k+d(1.5)))**2


def hair_currents(v,k,na,g,p,fraction):
    ek=26*(k/150).ln();ena=26*(na/12).ln();eca=13*(d(1.3)/d(.001)).ln()
    turnover=pump(k,p['pumpDensityPerSquareMicrometer'],p['cleftFacingAreaSquareMicrometers'])*fraction
    result=dict(metPa=Decimal(0),potassiumLowPa=fraction*p['potassiumLowMaximumNs']*g['potassiumLow']*(v-ek),
        calciumPa=fraction*p['calciumMaximumNs']*g['calcium']*(v-eca),
        hcnPotassiumPa=fraction*d(.8)*p['hcn1MaximumNs']*g['hcn1']*(v-ek),
        hcnSodiumPa=fraction*d(.2)*p['hcn1MaximumNs']*g['hcn1']*(v-ena),pumpPotassiumPa=-2*turnover,pumpSodiumPa=3*turnover)
    result['totalOutwardPa']=sum(result.values())
    result['basolateralPotassiumPa']=result['potassiumLowPa']+result['hcnPotassiumPa']+result['pumpPotassiumPa']
    result['basolateralSodiumPa']=result['hcnSodiumPa']+result['pumpSodiumPa']
    return result


def calyx_face(v,k,na,kv_gate,hcn_gate,p,fraction,inner):
    area=p['innerAreaSquareMicrometers' if inner else 'outerAreaSquareMicrometers']*fraction
    ek=26*(k/150).ln() if inner else d(-88.4311)
    ena=26*(na/12).ln() if inner else d(63.8751)
    split=p['kv74InnerFraction'] if inner else 1-p['kv74InnerFraction']
    hsplit=p['hcn2InnerFraction'] if inner else 1-p['hcn2InnerFraction']
    turnover=pump(k,p['innerPumpDensityPerSquareMicrometer' if inner else 'outerPumpDensityPerSquareMicrometer'],area)
    kcc=p['innerKccMaximumPaPerSquareMicrometer']*area*k*k/(k*k+d(17.5)**2) if inner else Decimal(0)
    currents=dict(kv74PotassiumPa=fraction*p['kv74MaximumNs']*split*kv_gate*(v-ek),
        hcnPotassiumPa=fraction*d(.8)*p['hcn2MaximumNs']*hsplit*hcn_gate*(v-ek),
        hcnSodiumPa=fraction*d(.2)*p['hcn2MaximumNs']*hsplit*hcn_gate*(v-ena),
        pumpPotassiumPa=-2*turnover,pumpSodiumPa=3*turnover,kccPotassiumPa=-kcc,kccChloridePa=kcc,
        unassignedLeakPa=Decimal(0) if inner else fraction*p['outerLeakNs']*v)
    currents['totalOutwardPa']=sum(currents.values())
    currents['potassiumOutwardPa']=sum(currents[n] for n in ('kv74PotassiumPa','hcnPotassiumPa','pumpPotassiumPa','kccPotassiumPa'))
    currents['sodiumOutwardPa']=currents['hcnSodiumPa']+currents['pumpSodiumPa']
    return currents


def evaluate(case,edge_reference,dense_solve):
    hp={k:d(v) for k,v in case['hairParameters'].items()};cp={k:d(v) for k,v in case['calyxParameters'].items()}
    topology=case['topology'];states={s['id']:s for s in case['states']};inputs={p['id']:p for p in case['inputs']}
    hair=d(case['hairMv']);bath=d(case['perilymphMv']);endo=bath+hp['endolymphPotentialMv']
    x=d(case['bundleDisplacementNanometers'])/1000+d(.2)
    met=hp['metMaximumNs']/(1+(-d(4.05)*(x-d(.39))).exp())/(1+(-d(14.5)*(x-d(.25))).exp())*(hair-endo-hp['metReversalMv'])
    membranes=[];ports=[]
    for node in sorted(topology['compartments'],key=lambda n:n['id']):
        state=states[node['id']];inp=inputs[node['id']];cleft=d(state['cleftMv']);calyx=d(state['calyxMv'])
        k=d(state['potassiumMm']);na=d(state['sodiumMm']);v=hair-cleft;iv=calyx-cleft;ov=calyx-bath
        hg={key:d(value) for key,value in inp['hairGates'].items()};cg={key:d(value) for key,value in inp['calyxGates'].items()}
        hf=d(node['hairCleftPf'])/(hp['specificCapacitancePfPerSquareMicrometer']*hp['cleftFacingAreaSquareMicrometers'])
        inf=d(node['calyxInnerPf'])/(cp['specificCapacitancePfPerSquareMicrometer']*cp['innerAreaSquareMicrometers'])
        outf=d(node['calyxOuterPf'])/(cp['specificCapacitancePfPerSquareMicrometer']*cp['outerAreaSquareMicrometers'])
        hr=hair_rates(v);hc=hair_currents(v,k,na,hg,hp,hf)
        ir=calyx_rates(iv);orr=calyx_rates(ov)
        ic=calyx_face(iv,k,na,cg['innerKv74'],cg['innerHcn2'],cp,inf,True)
        oc=calyx_face(ov,Decimal(5),Decimal(140),cg['outerKv74'],cg['outerHcn2'],cp,outf,False)
        calyx_result=dict(innerVoltageMv=iv,outerVoltageMv=ov,innerPotassiumReversalMv=26*(k/150).ln(),innerSodiumReversalMv=26*(na/12).ln(),
            rates=dict(inner=ir,outer=orr),inner=ic,outer=oc,totalOutwardPa=ic['totalOutwardPa']+oc['totalOutwardPa'])
        hd={name+'PerMs':(rate['equilibrium']-hg[name])/rate['tauMs'] for name,rate in hr.items()}
        cd={side+name[0].upper()+name[1:]+'PerMs':(rate['equilibrium']-cg[side+name[0].upper()+name[1:]])/rate['tauMs']
            for side,rates in [('inner',ir),('outer',orr)] for name,rate in rates.items()}
        port=dict(id=node['id'],hairToCleftPa=hc['totalOutwardPa'],calyxToCleftPa=ic['totalOutwardPa'],calyxToPerilymphPa=oc['totalOutwardPa'],
            calyxToAxonPa=d(inp['axonOutgoingPa']),hairPotassiumPa=hc['basolateralPotassiumPa'],hairSodiumPa=hc['basolateralSodiumPa'],
            calyxPotassiumPa=ic['potassiumOutwardPa'],calyxSodiumPa=ic['sodiumOutwardPa'])
        ports.append(port);membranes.append(dict(id=node['id'],hairMembraneVoltageMv=v,hairRates=hr,hairCurrents=hc,hairGateDerivatives=hd,
            calyx=calyx_result,calyxGateDerivatives=cd,port=port))
    spatial={key:topology[key] for key in ('hairCellId','hairApicalPf','hairPerilymphPf','material','compartments','connections')}
    spatial.update(states=case['states'],membranePorts=ports,hairMv=hair,endolymphMv=endo,perilymphMv=bath,hairToEndolymphPa=met,hairToPerilymphPa=Decimal(0))
    coupled=open_graph(dict(spatial=spatial,reservoir=topology['reservoir'],faces=topology['faces']),edge_reference,dense_solve)
    return dict(endolymphPotentialMv=endo,metCurrentPa=met,membranes=membranes,coupling=coupled)
