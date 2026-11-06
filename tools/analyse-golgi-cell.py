"""Independent passive-cable solve and measured whole-cell refinement.

The passive matrix is reconstructed from the pinned morphology, then solved by
SciPy sparse LU (discrete) and BDF (continuous). Neither validates active channel
coupling or establishes calibrated Golgi physiology.
"""
import csv
import hashlib
import json
from pathlib import Path
import re
import sys

import numpy as np
from scipy.integrate import solve_ivp
from scipy.sparse import csc_matrix, diags
from scipy.sparse.linalg import factorized

read = lambda p: json.loads(p.read_text(encoding="utf-8-sig"))


def morphology(reference):
    manifest = read(reference/"manifest.json")
    assert manifest["revision"] == "2ffaeca558235d61222240fc74247cab1cd02fe7"
    for name, value in manifest["sha256"].items():
        assert hashlib.sha256((reference/name).read_bytes()).hexdigest() == value, name
    hoc = (reference/"Golgi_template.hoc").read_text(encoding="latin-1")
    def section(name):
        body = re.search(re.escape(name)+r"\s*\{(.*?)\}", hoc, re.S).group(1)
        def number(field, default=None):
            found = re.search(r"(?m)^\s*"+field+r"\s*=\s*([-\d.eE]+)", body)
            return float(found.group(1)) if found else default
        return dict(length=number("L"), diameter=number("diam"), ra=number("Ra"), cm=number("cm",1), nseg=int(number("nseg")))
    soma, dendrite, axon, seal, electrode = [section(n) for n in ("soma","dend[i]","axon","seal","elec")]
    for statement in ("connect dend[i](0), soma(1)", "connect axon(0), soma(0)",
                      "connect seal(1), soma(1)", "connect elec(1), seal(0)"):
        assert statement in hoc
    leak = (reference/"Golgi_lkg.mod").read_text(encoding="latin-1")
    density = float(re.search(r"glbar\s*=\s*([-\d.eE]+)",leak).group(1))
    reversal = float(re.search(r"(?m)^\s*el\s*=\s*([-\d.eE]+)",leak).group(1))
    assert reversal == -55 and density == .000021
    def half_resistance(s):
        length_cm = s["length"]/s["nseg"]*1e-4
        radius_cm = s["diameter"]*1e-4/2
        return s["ra"]*length_cm/(2*np.pi*radius_cm**2)
    nodes = []
    def node(name, parent, s, resistance, leaking=True):
        area = np.pi*s["diameter"]*s["length"]/s["nseg"]
        nodes.append(dict(name=name,parent=parent,axialConductanceNs=0 if parent<0 else 1e9/resistance,
            capacitancePf=area*.01*s["cm"],areaUm2=area,leakConductanceNs=10*area*density if leaking else 0))
    node("soma",-1,soma,0)
    for name in ("soma-distal-junction","soma-proximal-junction"):
        nodes.append(dict(name=name,parent=0,axialConductanceNs=1e9/half_resistance(soma),capacitancePf=0,areaUm2=0,leakConductanceNs=0))
    for branch in range(3):
        for segment in range(dendrite["nseg"]):
            node(f"dendrite-{branch}-{segment}",1 if segment==0 else len(nodes)-1,dendrite,half_resistance(dendrite)*(1 if segment==0 else 2))
    for segment in range(axon["nseg"]):
        node(f"axon-{segment}",2 if segment==0 else len(nodes)-1,axon,half_resistance(axon)*(1 if segment==0 else 2))
    node("seal",1,seal,half_resistance(seal),False)
    node("electrode",len(nodes)-1,electrode,half_resistance(seal)+half_resistance(electrode),False)
    return nodes


def matrix(nodes):
    stiffness = np.diag([n["leakConductanceNs"] for n in nodes])
    capacitance = np.array([n["capacitancePf"] for n in nodes])
    for i,n in enumerate(nodes[1:],1):
        j=n["parent"];g=n["axialConductanceNs"]
        stiffness[i,i]+=g;stiffness[j,j]+=g;stiffness[i,j]-=g;stiffness[j,i]-=g
    return capacitance, stiffness


def continuous_passive(capacitance, stiffness, electrode, times, rtol, atol):
    # Eliminate only the two truly massless endpoints. Keep the small deposited
    # seal/electrode capacitances, rather than silently replacing them by wires.
    active=np.flatnonzero(capacitance>0);algebraic=np.flatnonzero(capacitance==0)
    algebraic_map=-np.linalg.solve(stiffness[np.ix_(algebraic,algebraic)],stiffness[np.ix_(algebraic,active)])
    reduced=stiffness[np.ix_(active,active)]+stiffness[np.ix_(active,algebraic)]@algebraic_map
    operator=csc_matrix(-reduced/capacitance[active,None])
    current=np.zeros(len(active));current[-1 if electrode else 0]=25
    x=np.full(len(active),-5.0);samples=[x.copy()];evaluations=0
    for a,b,on in ((0,20,False),(20,60,True),(60,100,False)):
        forcing=(current if on else np.zeros_like(current))/capacitance[active]
        solution=solve_ivp(lambda t,y:operator@y+forcing,(a,b),x,method="BDF",jac=operator,
            rtol=rtol,atol=atol,t_eval=times[(times>a)&(times<=b)])
        assert solution.success,solution.message
        samples.extend(solution.y.T);x=solution.y[:,-1];evaluations+=solution.nfev
    result=np.empty((len(times),len(capacitance)));result[:,active]=np.asarray(samples)
    result[:,algebraic]=np.asarray(samples)@algebraic_map.T
    return result-55,evaluations


def main(output, reference):
    protocol=read(output/"protocol.json");results=read(output/"results.json")
    assert protocol["study"]=="golgi-coupled-reference" and protocol["candidateVersion"]=="golgi-reference-cell-be-v1"
    assert protocol["caseCount"]==24 and protocol["stepsMs"]==[.015625,.0078125,.00390625]
    assert protocol["temperatureCelsius"]==23 and protocol["sampleMs"]==.25 and protocol["initialVoltageMv"]==-60
    assert protocol["thresholdMv"]==-20 and protocol["calciumMm"]==.00005
    assert results["completed"] and len(results["cases"])==24
    assert all(results[k] is False for k in ("physiologyQualified","activeReferenceCompared","synapticCircuitAssessed","axonalPropagationQualified","avatarRecoveryAssessed"))
    nodes=morphology(reference);assert len(nodes)==len(protocol["cable"])==135
    for expected,actual in zip(nodes,protocol["cable"]):
        assert expected.keys()==actual.keys()
        for key,value in expected.items():
            if isinstance(value,(float,np.floating)):assert np.isclose(value,actual[key],rtol=2e-13,atol=1e-13),(expected["name"],key)
            else:assert value==actual[key]
    cap,stiffness=matrix(nodes);traces={};case_map={};passive=[];summaries=[]
    protocols={"quiet":("None",0,False,6000),"depolarize":("None",50,False,6000),
        "hyperpolarize":("None",-50,False,6000),"no-nap":("WithoutPersistentSodium",0,False,6000),
        "no-hcn":("WithoutHcn",0,False,6000),"no-sk":("WithoutSk",0,False,6000),
        "passive-soma":("Passive",25,False,100),"passive-electrode":("Passive",25,True,100)}
    expected={(name,h) for name in protocols for h in protocol["stepsMs"]}
    for case in results["cases"]:
        key=(case["protocol"],case["stepMs"]);assert key in expected;expected.remove(key)
        block,current,electrode,duration=protocols[key[0]]
        assert (case["block"],case["injectionPa"],case["electrode"],case["durationMs"])==(block,current,electrode,duration)
        assert (case["injectionStartMs"],case["injectionEndMs"])==((20,60) if block=="Passive" else (2000,3000))
        for state_name in ("initial","final"):
            state=case[state_name];assert len(state["voltages"])==135 and len(state["gates"])==18 and len(state["sk"])==6
            assert all(np.isfinite(x) for values in state.values() for x in values)
            assert all(0<=x<=1 for x in state["gates"]+state["sk"])
            assert abs(sum(state["sk"])-1)<1e-8
        assert case["initial"]["voltages"]==[-60]*135
        trace=np.genfromtxt(output/(case["key"]+".csv"),delimiter=",",names=True)
        assert len(trace)==duration*4+1 and all(np.isfinite(trace[n]).all() for n in trace.dtype.names)
        assert np.array_equal(trace["timeMs"],np.arange(duration*4+1)/4)
        assert np.min(trace["highCaMm"])>0 and np.min(trace["lowCaMm"])>0
        for pool in ("high","low"):
            assert np.max(np.abs(trace[pool+"CaMm"]-.00005-trace[pool+"Entered"]+trace[pool+"Removed"]))<1e-9
        assert trace["chargeErrorFc"].max()<.02 and trace["kirchhoffResidualPa"].max()<1e-5
        assert trace["calciumErrorMm"].max()<1e-9 and trace["skMassError"].max()<1e-8
        spikes=case["somaticCrossingsMs"]
        assert all(0<t<=duration for t in spikes) and all(a<b for a,b in zip(spikes,spikes[1:]))
        assert case["final"]["voltages"][0]==trace["somaMv"][-1]
        assert case["maximumChargeBalanceErrorFc"]==trace["chargeErrorFc"][-1]
        traces[key]=trace;case_map[key]=case
        summaries.append(dict(protocol=key[0],stepMs=key[1],count=len(spikes),
            ratesHz={name:sum(a<t<=b for t in spikes)*1000/(b-a) for name,a,b in
                (("settled",1000,2000),("challenge",2000,3000),("recovery",4000,6000))} if duration==6000 else {},
            maximumChargeBalanceErrorFc=float(trace["chargeErrorFc"].max()),maximumKirchhoffResidualPa=float(trace["kirchhoffResidualPa"].max())))
    assert not expected
    times=np.arange(401)/4
    for name,electrode in (("passive-soma",False),("passive-electrode",True)):
        loose,n1=continuous_passive(cap,stiffness,electrode,times,1e-8,1e-10)
        strict,n2=continuous_passive(cap,stiffness,electrode,times,1e-10,1e-12)
        reference_error=float(np.max(np.abs(loose-strict)))
        for h in protocol["stepsMs"]:
            case=case_map[(name,h)];observed=np.loadtxt(output/(case["key"]+"-cable.csv"),delimiter=",",skiprows=1)
            assert observed.shape==(401,136) and np.array_equal(observed[:,0],times)
            assert np.isfinite(observed).all()
            solve=factorized(csc_matrix(stiffness)+diags(cap/h));x=np.full(135,-5.0);history=[x.copy()-55]
            drive=np.zeros(135);drive[-1 if electrode else 0]=25
            for index in range(int(100/h)):
                forcing=drive if 20<=index*h<60 else np.zeros(135)
                x=solve(cap/h*x+forcing)
                if (index+1)%int(.25/h)==0:history.append(x.copy()-55)
            discrete_error=float(np.max(np.abs(observed[:,1:]-np.asarray(history))))
            passive.append(dict(protocol=name,stepMs=h,independentDiscreteErrorMv=discrete_error,
                continuousReferenceErrorMv=float(np.max(np.abs(observed[:,1:]-strict))),
                referenceToleranceDifferenceMv=reference_error,referenceEvaluations=[n1,n2]))
    refinements=[]
    for name in protocols:
        for coarse,fine in zip(protocol["stepsMs"],protocol["stepsMs"][1:]):
            a,b=traces[(name,coarse)],traces[(name,fine)]
            sa,sb=case_map[(name,coarse)]["somaticCrossingsMs"],case_map[(name,fine)]["somaticCrossingsMs"]
            refinements.append(dict(protocol=name,coarseMs=coarse,fineMs=fine,counts=[len(sa),len(sb)],
                maximumOrdinalTimeDifferenceMs=max((abs(x-y) for x,y in zip(sa,sb)),default=0) if len(sa)==len(sb) else None,
                maximumSampledSomaDifferenceMv=float(np.max(np.abs(a["somaMv"]-b["somaMv"]))),
                maximumSampledHighCalciumDifferenceMm=float(np.max(np.abs(a["highCaMm"]-b["highCaMm"])))))
    report=dict(completed=True,accountingValidated=True,caseCount=24,geometryMatchesDeposit=True,
        passiveDiscreteAgreement=all(p["independentDiscreteErrorMv"]<2e-6 for p in passive),
        passiveReferenceToleranceAgreement=all(p["referenceToleranceDifferenceMv"]<2e-6 for p in passive),
        activeReferenceCompared=False,physiologyQualified=False,axonalPropagationQualified=False,
        synapticCircuitAssessed=False,avatarRecoveryAssessed=False,passiveComparisons=passive,refinements=refinements,cases=summaries)
    (output/"cell-analysis.json").write_text(json.dumps(report,indent=2,allow_nan=False)+"\n",encoding="utf-8")
    print(json.dumps({k:v for k,v in report.items() if k not in ("passiveComparisons","refinements","cases")},indent=2))
    assert report["passiveDiscreteAgreement"] and report["passiveReferenceToleranceAgreement"],"Passive reference check failed; retained detailed comparisons."


if __name__=="__main__":
    main(Path(sys.argv[1]),Path(sys.argv[2]))
