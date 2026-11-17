"""Independent open graph and direct-exponential aperture flux; no candidate import.

Run only in the visible comparison window. Both held baths are included in the
full capacitor graph; only the 1+2N dynamic potentials are solved.
"""
from decimal import Decimal
from cleft_spatial_reference import value


def aperture(material,face,state,reservoir,perilymph):
    p={k:value(v) for k,v in material.items()}
    ratio=value(face['areaSquareMicrometers'])/value(face['centerToBathDistanceMicrometers'])
    drop=value(state['cleftMv'])-value(perilymph);theta=p['thermalVoltageMv'];psi=-drop/theta
    def bernoulli(x):return Decimal(1) if x==0 else x/(x.exp()-1)
    def ion(name,diffusion):
        inside=value(state[name]);outside=value(reservoir[name])
        return (p[diffusion]*ratio*(bernoulli(psi)*inside-bernoulli(-psi)*outside),
                theta*(inside/outside).ln()+drop)
    k,dk=ion('potassiumMm','potassiumDiffusionSquareMicrometersPerMs')
    na,dna=ion('sodiumMm','sodiumDiffusionSquareMicrometersPerMs')
    ik=value(96.485)*k;ina=value(96.485)*na;other=p['otherIonConductivityNsPerMicrometer']*ratio*drop
    return dict(potassiumAttomolesPerMs=k,sodiumAttomolesPerMs=na,otherIonCurrentPa=other,
        potassiumElectrochemicalDropMv=dk,sodiumElectrochemicalDropMv=dna,potentialDropMv=drop,
        potassiumCurrentPa=ik,sodiumCurrentPa=ina,totalCurrentPa=ik+ina+other,dissipationFw=ik*dk+ina*dna+other*drop)


def evaluate(case,edge_reference,dense_solve):
    row=case['spatial'];reservoir=case['reservoir']
    nodes=sorted(row['compartments'],key=lambda n:n['id']);index={n['id']:i for i,n in enumerate(nodes)}
    states={s['id']:s for s in row['states']};ports={p['id']:p for p in row['membranePorts']}
    dynamic=1+2*len(nodes);endolymph=dynamic;perilymph=dynamic+1;size=dynamic+2
    matrix=[[Decimal(0) for _ in range(size)] for _ in range(size)]
    rhs=[Decimal(0) for _ in range(dynamic)];v=[Decimal(0) for _ in range(size)]
    v[0]=value(row['hairMv']);v[endolymph]=value(row['endolymphMv']);v[perilymph]=value(row['perilymphMv'])
    def capacitor(a,b,c):
        matrix[a][a]+=c;matrix[b][b]+=c;matrix[a][b]-=c;matrix[b][a]-=c
    capacitor(0,endolymph,value(row['hairApicalPf']));capacitor(0,perilymph,value(row['hairPerilymphPf']))
    rhs[0]=-value(row['hairToEndolymphPa'])-value(row['hairToPerilymphPa'])
    dk=[Decimal(0) for _ in nodes];dna=[Decimal(0) for _ in nodes];faraday=value(96.485)
    for i,node in enumerate(nodes):
        s=1+2*i;c=s+1;state=states[node['id']];p={k:value(x) for k,x in ports[node['id']].items() if k!='id'}
        volume=value(node['volumeCubicMicrometers']);v[s]=value(state['cleftMv']);v[c]=value(state['calyxMv'])
        capacitor(0,s,value(node['hairCleftPf']));capacitor(c,s,value(node['calyxInnerPf']));capacitor(c,perilymph,value(node['calyxOuterPf']))
        rhs[0]-=p['hairToCleftPa'];rhs[s]=p['hairToCleftPa']+p['calyxToCleftPa']
        rhs[c]=-p['calyxToCleftPa']-p['calyxToPerilymphPa']-p['calyxToAxonPa']
        dk[i]=(p['hairPotassiumPa']+p['calyxPotassiumPa'])/faraday/volume
        dna[i]=(p['hairSodiumPa']+p['calyxSodiumPa'])/faraday/volume
    transfers=[]
    for e in sorted(row['connections'],key=lambda e:e['id']):
        left,right=sorted([e['leftId'],e['rightId']]);i=index[left];j=index[right]
        ls=1+2*i;lc=ls+1;rs=1+2*j;rc=rs+1
        site=lambda key:dict(potentialMv=states[key]['cleftMv'],potassiumMm=states[key]['potassiumMm'],sodiumMm=states[key]['sodiumMm'])
        edge=dict(material=row['material'],geometry=dict(faceAreaSquareMicrometers=e['cleftFaceAreaSquareMicrometers'],
            centerDistanceMicrometers=e['centerDistanceMicrometers'],leftVolumeCubicMicrometers=nodes[i]['volumeCubicMicrometers'],
            rightVolumeCubicMicrometers=nodes[j]['volumeCubicMicrometers']),left=site(left),right=site(right))
        flow=edge_reference(edge);axial=value(e['calyxAxialConductanceNs'])*(v[lc]-v[rc])
        rhs[ls]-=flow['totalCurrentPa'];rhs[rs]+=flow['totalCurrentPa'];rhs[lc]-=axial;rhs[rc]+=axial
        rates=flow['concentrationRates'];dk[i]+=rates['leftPotassiumMmPerMs'];dk[j]+=rates['rightPotassiumMmPerMs']
        dna[i]+=rates['leftSodiumMmPerMs'];dna[j]+=rates['rightSodiumMmPerMs']
        transfers.append(dict(id=e['id'],leftId=left,rightId=right,cleft=flow,calyxCurrentLeftToRightPa=axial))
    apex=[];outk=Decimal(0);outna=Decimal(0);outcurrent=Decimal(0)
    for face in sorted(case['faces'],key=lambda f:f['id']):
        i=index[face['compartmentId']];volume=value(nodes[i]['volumeCubicMicrometers'])
        flow=aperture(row['material'],face,states[face['compartmentId']],reservoir,row['perilymphMv'])
        rhs[1+2*i]-=flow['totalCurrentPa'];dk[i]-=flow['potassiumAttomolesPerMs']/volume;dna[i]-=flow['sodiumAttomolesPerMs']/volume
        outk+=flow['potassiumAttomolesPerMs'];outna+=flow['sodiumAttomolesPerMs'];outcurrent+=flow['totalCurrentPa']
        apex.append(dict(id=face['id'],compartmentId=face['compartmentId'],reservoirId=reservoir['id'],outgoing=flow))
    rates=dense_solve([r[:dynamic] for r in matrix[:dynamic]],rhs)+[Decimal(0),Decimal(0)]
    multiply=lambda x:[sum(a*b for a,b in zip(r,x,strict=True)) for r in matrix]
    q=multiply(v);dq=multiply(rates)
    compartments=[dict(id=node['id'],cleftMvPerMs=rates[1+2*i],calyxMvPerMs=rates[2+2*i],potassiumMmPerMs=dk[i],
        sodiumMmPerMs=dna[i],cleftChargeFc=q[1+2*i],calyxChargeFc=q[2+2*i],
        cleftChargeRatePa=dq[1+2*i],calyxChargeRatePa=dq[2+2*i]) for i,node in enumerate(nodes)]
    spatial=dict(hairMvPerMs=rates[0],hairChargeFc=q[0],endolymphChargeFc=q[endolymph],perilymphChargeFc=q[perilymph],
        hairChargeRatePa=dq[0],endolymphChargeRatePa=dq[endolymph],perilymphChargeRatePa=dq[perilymph],
        storedEnergyAttojoules=sum(a*b for a,b in zip(v,q,strict=True))/2,
        storedEnergyRateFw=sum(a*b for a,b in zip(q,rates,strict=True)),compartments=compartments,transfers=transfers)
    membrane=value(row['hairToPerilymphPa'])+sum(value(ports[n['id']]['calyxToPerilymphPa']) for n in nodes)
    exchange=dict(cleftPotassiumAttomolesPerMs=outk,cleftSodiumAttomolesPerMs=outna,cleftCurrentPa=outcurrent,
        membraneCurrentPa=membrane,capacitiveChargeRatePa=dq[perilymph],holdingCurrentPa=dq[perilymph]-membrane-outcurrent)
    return dict(spatial=spatial,apexTransfers=apex,perilymph=exchange,
                endolymphHoldingCurrentPa=dq[endolymph]-value(row['hairToEndolymphPa']))
