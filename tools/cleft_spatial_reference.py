"""Independent full graph reference for the shared-hair spatial derivative."""
from decimal import Decimal


def value(x): return Decimal.from_float(x) if isinstance(x,float) else Decimal(x)


def evaluate(row,edge_reference,dense_solve):
    nodes=sorted(row['compartments'],key=lambda n:n['id']);indices={n['id']:i for i,n in enumerate(nodes)}
    states={s['id']:s for s in row['states']};ports={p['id']:p for p in row['membranePorts']}
    n=len(nodes);dynamic=1+2*n;endolymph=dynamic;perilymph=dynamic+1;size=dynamic+2
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
        volume=value(node['volumeCubicMicrometers'])
        v[s]=value(state['cleftMv']);v[c]=value(state['calyxMv'])
        capacitor(0,s,value(node['hairCleftPf']));capacitor(c,s,value(node['calyxInnerPf']));capacitor(c,perilymph,value(node['calyxOuterPf']))
        rhs[0]-=p['hairToCleftPa'];rhs[s]=p['hairToCleftPa']+p['calyxToCleftPa']
        rhs[c]=-p['calyxToCleftPa']-p['calyxToPerilymphPa']-p['calyxToAxonPa']
        dk[i]=(p['hairPotassiumPa']+p['calyxPotassiumPa'])/faraday/volume
        dna[i]=(p['hairSodiumPa']+p['calyxSodiumPa'])/faraday/volume
    transfers=[]
    for e in sorted(row['connections'],key=lambda e:e['id']):
        left,right=sorted([e['leftId'],e['rightId']]);i=indices[left];j=indices[right]
        ls=1+2*i;lc=ls+1;rs=1+2*j;rc=rs+1
        site=lambda key:dict(potentialMv=states[key]['cleftMv'],potassiumMm=states[key]['potassiumMm'],sodiumMm=states[key]['sodiumMm'])
        spec=dict(material=row['material'],geometry=dict(faceAreaSquareMicrometers=e['cleftFaceAreaSquareMicrometers'],
            centerDistanceMicrometers=e['centerDistanceMicrometers'],leftVolumeCubicMicrometers=nodes[i]['volumeCubicMicrometers'],
            rightVolumeCubicMicrometers=nodes[j]['volumeCubicMicrometers']),left=site(left),right=site(right))
        flow=edge_reference(spec);axial=value(e['calyxAxialConductanceNs'])*(v[lc]-v[rc])
        rhs[ls]-=flow['totalCurrentPa'];rhs[rs]+=flow['totalCurrentPa'];rhs[lc]-=axial;rhs[rc]+=axial
        rates=flow['concentrationRates'];dk[i]+=rates['leftPotassiumMmPerMs'];dk[j]+=rates['rightPotassiumMmPerMs']
        dna[i]+=rates['leftSodiumMmPerMs'];dna[j]+=rates['rightSodiumMmPerMs']
        transfers.append(dict(id=e['id'],leftId=left,rightId=right,cleft=flow,calyxCurrentLeftToRightPa=axial))
    rates=dense_solve([r[:dynamic] for r in matrix[:dynamic]],rhs)+[Decimal(0),Decimal(0)]
    multiply=lambda x:[sum(a*b for a,b in zip(r,x,strict=True)) for r in matrix]
    q=multiply(v);dq=multiply(rates)
    compartments=[dict(id=node['id'],cleftMvPerMs=rates[1+2*i],calyxMvPerMs=rates[2+2*i],potassiumMmPerMs=dk[i],
        sodiumMmPerMs=dna[i],cleftChargeFc=q[1+2*i],calyxChargeFc=q[2+2*i],
        cleftChargeRatePa=dq[1+2*i],calyxChargeRatePa=dq[2+2*i]) for i,node in enumerate(nodes)]
    return dict(hairMvPerMs=rates[0],hairChargeFc=q[0],endolymphChargeFc=q[endolymph],perilymphChargeFc=q[perilymph],
        hairChargeRatePa=dq[0],endolymphChargeRatePa=dq[endolymph],perilymphChargeRatePa=dq[perilymph],
        storedEnergyAttojoules=sum(a*b for a,b in zip(v,q,strict=True))/2,
        storedEnergyRateFw=sum(a*b for a,b in zip(q,rates,strict=True)),compartments=compartments,transfers=transfers)
