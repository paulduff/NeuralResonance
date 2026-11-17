"""Synthetic spatial input fixtures only; no anatomical fit or equation execution."""
from itertools import product


def cases():
    result=[]
    for count,shape,state_kind,port_kind in product([1,2,4,8],['uniform','graded'],['uniform','voltage','ions','mixed'],['quiet','localized','distributed']):
        weights=[1.0/count if shape=='uniform' else (i+1)/(count*(count+1)/2) for i in range(count)]
        nodes=[dict(id=f'p{i:02d}',volumeCubicMicrometers=(8. if shape=='uniform' else 12.)*w,
                    hairCleftPf=2.9463*w,calyxInnerPf=2.9463*w,calyxOuterPf=2.8833*w) for i,w in enumerate(weights)]
        links=[dict(id=f'e{i:02d}',leftId=nodes[i]['id'],rightId=nodes[i+1]['id'],
            cleftFaceAreaSquareMicrometers=.02 if shape=='uniform' else .015,centerDistanceMicrometers=.5,
            calyxAxialConductanceNs=3. if shape=='uniform' else 1.5) for i in range(count-1)]
        states=[];ports=[]
        for i,(node,w) in enumerate(zip(nodes,weights,strict=True)):
            voltage=state_kind in ('voltage','mixed');ions=state_kind in ('ions','mixed')
            states.append(dict(id=node['id'],cleftMv=-4.+2*i if voltage else 0.,calyxMv=-60.+3*i if voltage else -60.,
                potassiumMm=5.+2*i if ions else 5.,sodiumMm=140.-3*i if ions else 140.))
            values=[0.]*8
            if port_kind=='localized' and i==0: values=[11.,-13.,17.,-23.,0.,0.,0.,0.]
            elif port_kind=='distributed': values=[12*w,-8*w,4*w,0.,96.485*w,0.,0.,-96.485*w]
            names=['hairToCleftPa','calyxToCleftPa','calyxToPerilymphPa','calyxToAxonPa',
                   'hairPotassiumPa','hairSodiumPa','calyxPotassiumPa','calyxSodiumPa']
            ports.append(dict(id=node['id'],**dict(zip(names,values,strict=True))))
        result.append(dict(index=len(result),hairCellId='fixture-hair',hairApicalPf=3.,hairPerilymphPf=.4558,
            material=dict(potassiumDiffusionSquareMicrometersPerMs=.81,sodiumDiffusionSquareMicrometersPerMs=.56,
                thermalVoltageMv=26.,otherIonConductivityNsPerMicrometer=600.),
            compartments=nodes,connections=links,states=states,membranePorts=ports,hairMv=-70.,endolymphMv=5.,perilymphMv=0.,
            hairToEndolymphPa=0. if port_kind=='quiet' else 7.,hairToPerilymphPa=0. if port_kind=='quiet' else 3.))
    return result
