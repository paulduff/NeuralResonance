"""Bounded numerical Jacobian for physical states plus passive accounting totals.

This is a Newton-solver aid, not a changed ODE or relaxed solution tolerance.
No perturbation factor persists or grows between calls. Ledger columns 27..29
are explicitly zero because the motor-cell RHS never reads those states.
"""
import numpy as np

VERSION='motoneuron-bounded-central-physical-v1'
SCALES=np.r_[np.ones(14),1e-4,np.ones(12)]


def bounded_jacobian(fun):
    def jac(t,y):
        y=np.asarray(y,dtype=float)
        if y.shape!=(30,) or not np.isfinite(y).all():raise ValueError('Finite 30-state motor reference required.')
        result=np.zeros((30,30))
        for column in range(27):
            h=np.cbrt(np.finfo(float).eps)*max(abs(y[column]),SCALES[column])
            plus=y.copy();minus=y.copy();plus[column]+=h;minus[column]-=h
            denominator=plus[column]-minus[column]
            if not np.isfinite(denominator) or denominator<=0:raise ValueError('Invalid bounded Jacobian increment.')
            high=np.asarray(fun(t,plus));low=np.asarray(fun(t,minus))
            if high.shape!=(30,) or low.shape!=(30,) or not np.isfinite(high).all() or not np.isfinite(low).all():
                raise ValueError('Nonfinite reference derivative; no clipping or silent repair.')
            result[:,column]=(high-low)/denominator
        if not np.isfinite(result).all():raise ValueError('Nonfinite bounded Jacobian.')
        return result
    return jac


def verify(rhs,initial):
    """Execute inside a future visible reference window, never on import."""
    # Analytically known independent linear system with three passive totals.
    matrix=np.zeros((30,30))
    for i in range(27):
        matrix[i,i]=-(i+1)*.1
        if i>0:matrix[i,i-1]=.03
    matrix[27,0]=2;matrix[28,14]=3;matrix[29,26]=4
    y=np.linspace(.1,1,30);y[14]=.0001
    linear=bounded_jacobian(lambda t,x:matrix@x+np.arange(30)*.001)
    errors=[]
    # Large ledger values cannot create larger perturbations or overflow: none
    # are perturbed/read by this derivative matrix.
    for magnitude in (0.,1.,1e100,1e300):
        y[27:]=magnitude
        errors.append(float(np.max(np.abs(linear(0,y)-matrix))))
    assert max(errors)<=1e-8
    command=dict(somaCurrentPa=3000,dendriteExcitatoryNs=25,dendriteInhibitoryNs=100)
    cases=[]
    for block in ('None','WithoutTransientSodium','WithoutCalciumL','WithoutPotassiumCalcium'):
        fun=rhs(block,command);jac=bounded_jacobian(fun)
        for voltage in (-80.,-65.,-40.,-20.,20.):
            for calcium in (.0001,.001):
                state=initial.copy();state[:5]=voltage;state[14]=calcium
                # Manufactured interior gate states keep this directional
                # smoothness check away from the influx rectification corner.
                # They are derivative fixtures, not simulated resting cells.
                state[5:14]=np.linspace(.2,.8,9);state[15:27]=np.tile([.3,.7,.4],4)
                saved=state.copy();j=jac(0,state)
                assert np.array_equal(state,saved) and np.all(j[:,27:]==0)
                changed=state.copy();changed[27:]=[1e100,-1e100,1e300]
                assert np.array_equal(fun(0,state),fun(0,changed)), 'Declared accounting totals unexpectedly affect dynamics.'
                # Independent directional differences use different increments
                # and combinations of variables, not the column construction.
                direction=np.r_[np.linspace(-.5,.5,5),np.linspace(-.1,.1,9),2e-5,np.linspace(-.1,.1,12),np.zeros(3)]
                tangent=j@direction;errors_direction=[]
                for step in (.001,.0005):
                    estimate=(fun(0,state+step*direction)-fun(0,state-step*direction))/(2*step)
                    errors_direction.append(float(np.max(np.abs(estimate-tangent)/np.maximum(1.,np.abs(tangent)))))
                assert errors_direction[-1]<=1e-5
                assert errors_direction[-1]<=max(1e-8,1.1*errors_direction[0])
                cases.append(dict(block=block,voltageMv=voltage,calciumMm=calcium,interiorGateFixture=True,relativeDirectionalErrors=errors_direction))
    return dict(version=VERSION,completed=True,passed=True,knownLinearMaximumAbsoluteError=max(errors),
        directionalCases=cases,structuralZeroColumns=[27,28,29],odeChanged=False,physiologicalQualification=False)
