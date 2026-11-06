"""Error-controlled reference for the declared Golgi candidate equations.

Rate coefficients and maximal densities are read as numbers from the pinned
NMODL source; no HOC/NMODL code is executed. Ordinary ODE gates, a Markov SK2
system, cable voltage and two calcium pools are advanced together by SciPy BDF.
This differs from the candidate's exponential gates / Hines backward-Euler /
calcium splitting. The documented analytic-table, singularity and HCN fraction
choices are deliberately shared definitions, not independently validated biology.
"""
import importlib.util
import hashlib
import json
import math
from pathlib import Path
import re

import numpy as np
from scipy.integrate import solve_ivp
from scipy.sparse import csc_matrix, lil_matrix
from scipy.special import expit, exprel

GATE_ORDER = ['NaM','NaH','NaP','NaRS','NaRF','Kv','Km','KaA','KaB',
    'HvaS','HvaU','LvaM','LvaH','Hcn1Fast','Hcn1Slow','Hcn2Fast','Hcn2Slow','Bk']


class ReferenceChannels:
    def __init__(self, reference, temperature=23):
        self.temperature = temperature
        manifest=json.loads((reference/'manifest.json').read_text(encoding='utf-8-sig'))
        assert manifest['revision']=='2ffaeca558235d61222240fc74247cab1cd02fe7'
        for name,value in manifest['sha256'].items():
            assert hashlib.sha256((reference/name).read_bytes()).hexdigest()==value,name
        self.parameters = {}
        for name in ('Na','NaP','NaR','KV','KM','KA','Ca_HVA','Ca_LVA','hcn1','hcn2','BK','SK2','lkg','CALC','CALC_ca2'):
            text = (reference/f'Golgi_{name}.mod').read_text(encoding='latin-1')
            block = re.search(r'PARAMETER\s*\{(.*?)\}', text, re.S).group(1)
            self.parameters[name] = {key:float(value) for key,value in re.findall(
                r'(?m)^\s*(\w+)\s*=\s*([-+]?(?:\d*\.)?\d+(?:[eE][-+]?\d+)?)', block)}
        hoc = (reference/'Golgi_template.hoc').read_text(encoding='latin-1')
        self.ena = float(re.search(r'(?m)^\s*ena\s*=\s*([-\d.]+)', hoc).group(1))
        self.ek = float(re.search(r'(?m)^\s*ek\s*=\s*([-\d.]+)', hoc).group(1))
        assert self.ena == 87.39 and self.ek == -84.69
        sk = (reference/'Golgi_SK2.mod').read_text(encoding='latin-1')
        states = sk.split('STATE {',1)[1].split('}',1)[0].split()
        assert states == ['c1','c2','c3','c4','o1','o2']
        self.sk_edges = []
        for a,b,forward,reverse in re.findall(r'~\s*(\w+)\s*<->\s*(\w+)\s*\(\s*(\w+)\s*,\s*(\w+)\s*\)', sk):
            def rate(name):
                return (self.parameters['SK2'][name.removesuffix('_ca').removesuffix('_t')],name.endswith('_ca'))
            self.sk_edges.append((states.index(a),states.index(b),rate(forward),rate(reverse)))
        assert len(self.sk_edges) == 5

    @staticmethod
    def linoid(x, scale, cap=False):
        z = x/scale
        return x/math.expm1(200) if cap and z>200 else scale/exprel(z)

    def rates(self, voltage, calcium):
        v = float(voltage); ca = float(calcium); T = self.temperature
        equilibrium=[]; taus=[]
        def q(p, reference): return p.get('Q10',3)**((T-reference)/10)
        def sigmoid(x,k): return float(expit(-x/k))
        def ab(a,b): equilibrium.append(a/(a+b)); taus.append(1/(a+b))
        def direct(infinity,tau): equilibrium.append(infinity); taus.append(tau)
        def exponential(p,label,gate):
            return p['A'+label+'_'+gate]*math.exp((v-p['V0'+label+'_'+gate])/p['K'+label+'_'+gate])
        p=self.parameters['Na'];Q=q(p,20)
        ab(-Q*p['Aalpha_m']*self.linoid(v-p['V0alpha_m'],p['Kalpha_m']),Q*exponential(p,'beta','m'))
        ab(Q*exponential(p,'alpha','h'),Q*p['Abeta_h']*sigmoid(v-p['V0beta_h'],p['Kbeta_h']))
        p=self.parameters['NaP'];Q=q(p,30)
        total=Q*(p['Aalpha_m']*self.linoid(v-p['V0alpha_m'],p['Kalpha_m'])+p['Abeta_m']*self.linoid(v-p['V0beta_m'],p['Kbeta_m']))
        direct(sigmoid(v-p['V0_minf'],-p['B_minf']),5/total)
        p=self.parameters['NaR'];Q=q(p,20)
        ab(Q*(p['Shiftalpha_s']+p['Aalpha_s']*self.linoid(v+p['V0alpha_s'],p['Kalpha_s'])),
           Q*(p['Shiftbeta_s']+p['Abeta_s']*self.linoid(v+p['V0beta_s'],p['Kbeta_s'],True)))
        ab(Q*exponential(p,'alpha','f'),Q*exponential(p,'beta','f'))
        p=self.parameters['KV'];Q=q(p,6.3)
        ab(Q*p['Aalpha_n']*self.linoid(v-p['V0alpha_n'],p['Kalpha_n']),Q*exponential(p,'beta','n'))
        p=self.parameters['KM'];Q=q(p,22)
        direct(sigmoid(v-p['V0_ninf'],-p['B_ninf']),1/(Q*(exponential(p,'alpha','n')+exponential(p,'beta','n'))))
        p=self.parameters['KA'];Q=q(p,25.5)
        direct(sigmoid(v-p['V0_ainf'],p['K_ainf']),1/(Q*(p['Aalpha_a']*sigmoid(v-p['V0alpha_a'],p['Kalpha_a'])+p['Abeta_a']*math.exp(-(v-p['V0beta_a'])/p['Kbeta_a']))))
        direct(sigmoid(v-p['V0_binf'],p['K_binf']),1/(Q*(p['Aalpha_b']*sigmoid(v-p['V0alpha_b'],p['Kalpha_b'])+p['Abeta_b']*sigmoid(v-p['V0beta_b'],p['Kbeta_b']))))
        p=self.parameters['Ca_HVA'];Q=q(p,20)
        for gate in ('s','u'):ab(Q*exponential(p,'alpha',gate),Q*exponential(p,'beta',gate))
        p=self.parameters['Ca_LVA']
        for gate,Q in (('m',5**((T-24)/10)),('h',3**((T-24)/10))):
            direct(sigmoid(v+p['shift']-p['v0_'+gate+'_inf'],p['k_'+gate+'_inf']),
                (p['C_tau_'+gate]+p['A_tau_'+gate]/sum(math.exp((v+p['shift']-p['v0_tau_'+gate+j])/p['k_tau_'+gate+j]) for j in ('1','2')))/Q)
        for subtype in ('hcn1','hcn2'):
            p=self.parameters[subtype];fraction=min(1,max(0,p['rA']*v+p['rB']))
            if subtype=='hcn2':fraction=0 if v>=-64.70 else 1 if v<=-108.70 else fraction
            opened=float(expit(-(v-p['Ehalf'])*p['c']))
            for weight,kind in ((fraction,'f'),(1-fraction,'s')):
                direct(weight*opened,math.exp((p['tC'+kind]*v-p['tD'+kind])*p['tE'+kind]))
        p=self.parameters['BK'];Q=q(p,30)
        ab(Q*p['Aalpha_c']*ca/(ca+p['Balpha_c']*math.exp(v/p['Kalpha_c'])),
           Q*p['Abeta_c']*p['Bbeta_c']*math.exp(v/p['Kbeta_c'])/(ca+p['Bbeta_c']*math.exp(v/p['Kbeta_c'])))
        assert len(equilibrium)==18 and all(math.isfinite(t) and t>0 for t in taus)
        return np.array(equilibrium),np.array(taus)

    def sk_matrix(self, calcium):
        p=self.parameters['SK2'];scale=p['Q10']**((self.temperature-23)/10)
        matrix=np.zeros((6,6))
        for source,target,forward,reverse in self.sk_edges:
            for a,b,(coefficient,sensitive) in ((source,target,forward),(target,source,reverse)):
                rate=coefficient*scale*(calcium/p['diff'] if sensitive else 1)
                matrix[b,a]+=rate;matrix[a,a]-=rate
        return matrix

    def initial(self):
        gates,_=self.rates(-60,.00005)
        matrix=self.sk_matrix(.00005);matrix[-1,:]=1
        rhs=np.zeros(6);rhs[-1]=1
        return gates,np.linalg.solve(matrix,rhs)

    def currents(self, voltage, gates, occupancy, high_ca, low_ca, block):
        p=self.parameters;g=gates
        factors=np.array([g[0]**3*g[1],g[2],g[3]*g[4],g[5]**4,g[6],g[7]**3*g[8],
            g[9]**2*g[10],g[11]**2*g[12],g[13]+g[14],g[15]+g[16],g[17],occupancy[4]+occupancy[5]])
        names=(('Na','gnabar'),('NaP','gbar'),('NaR','gnabar'),('KV','gkbar'),('KM','gkbar'),('KA','gkbar'),
            ('Ca_HVA','gcabar'),('Ca_LVA','gca2bar'),('hcn1','gbar'),('hcn2','gbar'),('BK','gkbar'),('SK2','gkbar'))
        conductance=factors*np.array([p[n][k] for n,k in names])
        if block=='WithoutPersistentSodium':conductance[1]=0
        elif block=='WithoutHcn':conductance[8:10]=0
        elif block=='WithoutSk':conductance[11]=0
        else:assert block=='None'
        factor=1000*8.31446261815324*(self.temperature+273.15)/(2*96485.33212)
        assert high_ca>0 and low_ca>0
        reversal=np.array([self.ena]*3+[self.ek]*3+[factor*math.log(2/high_ca),factor*math.log(2/low_ca),
            p['hcn1']['Erev'],p['hcn2']['ehcn2'],self.ek,self.ek])
        return conductance*(voltage-reversal)


def verify_clamps(channels, data):
    assert data['gateOrder']==GATE_ORDER and data['caseCount']==36
    maximum=0;worst=None
    for case in data['cases']:
        channels.temperature=case['temperatureCelsius']
        initial,_=channels.rates(-60,.00005)
        equilibrium,tau=channels.rates(case['voltageMv'],case['calciumMm'])
        for sample in case['samples']:
            predicted=equilibrium+(initial-equilibrium)*np.exp(-sample['timeMs']/tau)
            errors=np.abs(predicted-np.array(sample['gates']));error=float(errors.max())
            if error>maximum:
                maximum=error;worst=dict(voltage=case['voltageMv'],calcium=case['calciumMm'],temperature=case['temperatureCelsius'],time=sample['timeMs'],gate=GATE_ORDER[int(errors.argmax())])
    assert maximum<2e-11,(maximum,worst)
    channels.temperature=23
    return maximum


class CoupledReference:
    def __init__(self,nodes,capacitance,stiffness,channels):
        self.channels=channels;self.nodes=nodes
        self.active=np.flatnonzero(capacitance>0);self.massless=np.flatnonzero(capacitance==0)
        self.capacitance=capacitance[self.active];self.n=len(self.active)
        self.algebraic_map=-np.linalg.solve(stiffness[np.ix_(self.massless,self.massless)],stiffness[np.ix_(self.massless,self.active)])
        reduced=stiffness[np.ix_(self.active,self.active)]+stiffness[np.ix_(self.active,self.massless)]@self.algebraic_map
        self.operator=csc_matrix(-reduced/self.capacitance[:,None])
        sparsity=lil_matrix((self.n+26,self.n+26),dtype=bool)
        sparsity[:self.n,:self.n]=self.operator!=0
        sparsity[0,self.n:]=True
        for i in range(self.n,self.n+18):sparsity[i,0]=True;sparsity[i,i]=True
        sparsity[self.n+17,-2]=True
        sparsity[self.n+18:self.n+24,self.n+18:self.n+24]=True
        sparsity[self.n+18:self.n+24,-2]=True
        sparsity[-2,0]=True;sparsity[-2,self.n+9:self.n+11]=True;sparsity[-2,-2]=True
        sparsity[-1,0]=True;sparsity[-1,self.n+11:self.n+13]=True;sparsity[-1,-1]=True
        self.sparsity=sparsity.tocsc()

    def run(self,case,rtol,voltage_atol,gate_atol,calcium_atol,method='BDF',max_step=.25):
        if method not in ('BDF','Radau') or not math.isfinite(max_step) or max_step<=0:
            raise ValueError('Declare an implicit reference method and positive maximum step.')
        n=self.n;channels=self.channels
        gates,sk=channels.initial()
        # Calcium states use micromolar for an explicit, useful absolute tolerance.
        state=np.concatenate((np.full(n,-5.0),gates,sk,[.05,.05]))
        atol=np.concatenate((np.full(n,voltage_atol),np.full(24,gate_atol),np.full(2,calcium_atol)))
        samples=[state.copy()];spikes=[];nfev=njev=nlu=0
        times=np.arange(24001)/4
        def crossing(t,y):return y[0]-35 # actual V = shifted V - 55; threshold -20 mV.
        crossing.direction=1
        for start,end in ((0,2000),(2000,3000),(3000,6000)):
            current=case['injectionPa'] if start==2000 else 0
            def derivative(t,y):
                voltage=y[0]-55;ca_hi,ca_lo=y[-2:]/1000
                gates=y[n:n+18];occupancy=y[n+18:n+24]
                equilibrium,tau=channels.rates(voltage,ca_hi)
                ionic=channels.currents(voltage,gates,occupancy,ca_hi,ca_lo,case['block'])
                result=np.empty_like(y)
                result[:n]=self.operator@y[:n]
                result[0]+=(current-ionic.sum()*self.nodes[0]['areaUm2']*10)/self.capacitance[0]
                result[n:n+18]=(equilibrium-gates)/tau
                result[n+18:n+24]=channels.sk_matrix(ca_hi)@occupancy
                for i,(name,calcium,current_density) in enumerate((('CALC',ca_hi,ionic[6]),('CALC_ca2',ca_lo,ionic[7]))):
                    p=channels.parameters[name]
                    result[n+24+i]=1000*(-current_density*1e4/(2*96485.33212*p['d'])-p['beta']*(calcium-.00005))
                return result
            solution=solve_ivp(derivative,(start,end),state,method=method,jac_sparsity=self.sparsity,
                rtol=rtol,atol=atol,max_step=max_step,t_eval=times[(times>start)&(times<=end)],events=crossing)
            if not solution.success:raise RuntimeError(solution.message)
            samples.extend(solution.y.T);state=solution.y[:,-1]
            spikes.extend(t for t in solution.t_events[0] if not spikes or t>spikes[-1])
            nfev+=solution.nfev;njev+=solution.njev;nlu+=solution.nlu
        states=np.asarray(samples)
        assert states.shape==(24001,n+26) and np.isfinite(states).all()
        assert states[:,n:n+24].min()>-1e-8 and states[:,n:n+24].max()<1+1e-8
        assert states[:,-2:].min()>0
        maximum_mass_error=float(np.max(np.abs(states[:,n+18:n+24].sum(axis=1)-1)))
        assert maximum_mass_error<1e-8
        return dict(times=times,states=states,spikes=np.array(spikes)),dict(nfev=nfev,njev=njev,nlu=nlu,skMassError=maximum_mass_error)


def main(output,reference,clamp_output):
    spec=importlib.util.spec_from_file_location('cable_analysis',Path(__file__).with_name('analyse-golgi-cell.py'))
    cable=importlib.util.module_from_spec(spec);spec.loader.exec_module(cable)
    nodes=cable.morphology(reference);capacitance,stiffness=cable.matrix(nodes)
    channels=ReferenceChannels(reference)
    clamp_error=verify_clamps(channels,json.loads((clamp_output/'channels.json').read_text(encoding='utf-8-sig')))
    solver=CoupledReference(nodes,capacitance,stiffness,channels)
    destination=output/'active-reference'
    if destination.exists():raise ValueError('Use a fresh reference directory; retain previous evidence.')
    destination.mkdir()
    cases=json.loads((output/'results.json').read_text())['cases'];references=[];comparisons=[]
    for protocol in ('quiet','depolarize','hyperpolarize','no-nap','no-hcn','no-sk'):
        selected=[c for c in cases if c['protocol']==protocol]
        assert len(selected)==3
        print('Active reference: '+protocol,flush=True)
        runs=[]
        for label,rtol,vatol,gatol,catol in (('standard',1e-8,1e-9,1e-11,1e-10),('strict',1e-10,1e-11,1e-13,1e-12)):
            run,statistics=solver.run(selected[0],rtol,vatol,gatol,catol)
            np.savez_compressed(destination/(protocol+'-'+label+'.npz'),**run)
            references.append(dict(protocol=protocol,level=label,rtol=rtol,voltageAtolMv=vatol,
                gateAtol=gatol,calciumAtolUm=catol,spikeCount=len(run['spikes']),**statistics))
            runs.append(run)
        ordinary,strict=runs
        reference_voltage=float(np.max(np.abs(ordinary['states'][:,0]-strict['states'][:,0])))
        reference_timing=float(np.max(np.abs(ordinary['spikes']-strict['spikes']))) if len(ordinary['spikes'])==len(strict['spikes']) and len(strict['spikes']) else (0.0 if not len(ordinary['spikes']) and not len(strict['spikes']) else None)
        for case in selected:
            trace=np.genfromtxt(output/(case['key']+'.csv'),delimiter=',',names=True)
            spikes=np.array(case['somaticCrossingsMs'])
            error=np.abs(trace['somaMv']-(strict['states'][:,0]-55))
            ordinal=float(np.max(np.abs(spikes-strict['spikes']))) if len(spikes)==len(strict['spikes']) and len(spikes) else (0.0 if not len(spikes) and not len(strict['spikes']) else None)
            comparisons.append(dict(protocol=protocol,stepMs=case['stepMs'],counts=[len(spikes),len(strict['spikes'])],
                maximumOrdinalDifferenceMs=ordinal,maximumSampledSomaDifferenceMv=float(error.max()),
                maximumFirst20MsSomaDifferenceMv=float(error[strict['times']<=20].max()),
                maximumSampledHighCalciumDifferenceMm=float(np.max(np.abs(trace['highCaMm']-strict['states'][:,-2]/1000))),
                referenceToleranceSomaDifferenceMv=reference_voltage,referenceToleranceOrdinalDifferenceMs=reference_timing,
                referenceToleranceCounts=[len(ordinary['spikes']),len(strict['spikes'])]))
    report=dict(completed=True,gateClampMaximumAbsoluteError=clamp_error,activeReferenceCompared=True,
        referenceToleranceAgreement=all(c['referenceToleranceSomaDifferenceMv']<.05 and c['referenceToleranceOrdinalDifferenceMs'] is not None and c['referenceToleranceOrdinalDifferenceMs']<.01 for c in comparisons),
        activeNumericalQualification=False,physiologyQualified=False,avatarRecoveryAssessed=False,
        interpretation='Independent coupled ODE comparison for the declared corrected analytic equations, not native NEURON reproduction or biological calibration. Full trajectories and spike counts are retained; refine or repair from these results.',
        references=references,comparisons=comparisons)
    (output/'active-reference-analysis.json').write_text(json.dumps(report,indent=2,allow_nan=False)+'\n')
    print('Active comparison saved; reference tolerance agreement: '+str(report['referenceToleranceAgreement']),flush=True)
    if not report['referenceToleranceAgreement']:raise RuntimeError('Independent reference needs further tolerance review; do not treat its strict trace as exact.')


if __name__=='__main__':
    import sys
    if len(sys.argv)==4 and sys.argv[1]=='--clamps':
        reference,output=map(Path,sys.argv[2:])
        error=verify_clamps(ReferenceChannels(reference),json.loads((output/'channels.json').read_text(encoding='utf-8-sig')))
        (output/'gate-reference.json').write_text(json.dumps(dict(completed=True,maximumAbsoluteGateError=error,caseCount=36,comparisonCount=4536),indent=2)+'\n')
        print('Independent ordinary-gate clamp error: '+str(error))
    else:
        main(*(Path(p) for p in sys.argv[1:]))
