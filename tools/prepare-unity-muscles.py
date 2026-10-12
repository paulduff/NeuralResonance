"""Port canonical muscle force/activation equations, using measured joint state."""
import argparse, hashlib, re
from pathlib import Path

BINDINGS = {
    'leftHip':('left_thigh',0), 'rightHip':('right_thigh',0),
    'leftHipAbduction':('left_thigh',1), 'rightHipAbduction':('right_thigh',1),
    'leftKnee':('left_shin',0), 'rightKnee':('right_shin',0),
    'leftAnkle':('left_foot',0), 'rightAnkle':('right_foot',0),
    'leftAnkleRoll':('left_foot',1), 'rightAnkleRoll':('right_foot',1),
    'leftShoulder':('left_arm',0), 'rightShoulder':('right_arm',0),
    'leftShoulderAbduction':('left_arm',1), 'rightShoulderAbduction':('right_arm',1),
    'leftElbow':('left_forearm',0), 'rightElbow':('right_forearm',0),
    'neckYaw':('head',1), 'neckPitch':('head',0), 'trunkYaw':('chest',0)}

def generate(root):
    source=(root/'src/NRE.SimAvatar/AvatarMuscle.cs').read_text(encoding='utf-8-sig')
    body=(root/'src/NRE.SimAvatar/AvatarArticulatedBody.cs').read_text(encoding='utf-8-sig')
    core=source[:source.index('public readonly record struct AvatarMechanicalOutput')]
    core=core.replace('using NeuralResonanceEngine.Shared.Contracts;', 'using System;')
    core=core.replace('namespace NRE.SimAvatar;', 'namespace Nre.UnityWorld {')
    # Diagnostic-only ablation. The default/control still executes the complete
    # inherited fatigue equation; live Unity stepping forbids this ablation.
    signature='public void Advance(double excitation, double lengthFraction, double dt)'
    fatigue_end='        if (target <= 0.0 && Activation < RelaxedActivationEpsilon)'
    if core.count(signature)!=1 or core.count(fatigue_end)!=1:
        raise ValueError('Unexpected canonical fatigue layout.')
    core=core.replace(signature,'''public MotorUnitPool DiagnosticMotorUnits { get; } = new MotorUnitPool();
    public double DiagnosticRequestedActivation { get; private set; }

    public void Advance(double excitation, double lengthFraction, double dt,
        bool suppressDiagnosticFatigue = false, MotorUnitFatigueParameters? diagnosticPoolParameters = null)''')
    activation_update='        Activation += (target - Activation) * alpha;'
    core=core.replace('var timeConstant = target > Activation ?',
        'var recruitmentState = diagnosticPoolParameters.HasValue ? DiagnosticRequestedActivation : Activation;\n        var timeConstant = target > recruitmentState ?')
    if core.count(activation_update)!=1: raise ValueError('Unexpected activation update.')
    core=core.replace(activation_update,'''        if (diagnosticPoolParameters.HasValue)
        {
            DiagnosticRequestedActivation += (target - DiagnosticRequestedActivation) * alpha;
            if (target <= 0 && DiagnosticRequestedActivation < RelaxedActivationEpsilon)
                DiagnosticRequestedActivation = 0;
            DiagnosticMotorUnits.Advance(DiagnosticRequestedActivation, dt, diagnosticPoolParameters.Value);
            Activation = DiagnosticMotorUnits.ActiveFraction;
        }
        else Activation += (target - Activation) * alpha;''')
    core=core.replace(fatigue_end,'''        if (diagnosticPoolParameters.HasValue) FatigueFraction = DiagnosticMotorUnits.FatiguedFraction;
        else if (suppressDiagnosticFatigue) FatigueFraction = 0.0;

'''+fatigue_end.replace('target <= 0.0 &&', '!diagnosticPoolParameters.HasValue && target <= 0.0 &&'))
    core=core.replace('var fatigueCapacity = FatigueCapacityFraction;',
        'var fatigueCapacity = diagnosticPoolParameters.HasValue ? 1.0 : FatigueCapacityFraction;')
    reset='        previousLength = 1.0;\n        Activation = 0.0;'
    if core.count(reset)!=1: raise ValueError('Unexpected muscle reset.')
    core=core.replace(reset,'        DiagnosticMotorUnits.Reset();\n        DiagnosticRequestedActivation = 0;\n'+reset)
    begin=core.index('    public void Advance(double targetAngle')
    end=core.index('    public void Reconcile(', begin)
    old=core[begin:end]
    activation=old[old.index('        var boundedTarget'):old.index('        var activeTorque')]
    if activation.count(', dt);')!=2: raise ValueError('Expected both antagonist advances.')
    activation=activation.replace(', dt);', ', dt, suppressDiagnosticFatigue, diagnosticPoolParameters);')
    # Explicit comparison candidate: equal resting antagonist forces must not
    # automatically cap the stronger agonist's active recruitment. The original
    # equations remain the default/control until this candidate is qualified.
    marker='        var span = Math.Max(0.1, maximum - minimum);'
    if activation.count(marker)!=1: raise ValueError('Unexpected recruitment equation layout.')
    activation=activation.replace(marker, '''        if (preserveActiveCapacity)
        {
            flexorExcitation = tone * (balancedForce / Flexor.MaximumIsometricForceNewtons) +
                (error > 0.0 ? reflex : 0.0) + (AngularVelocity < 0.0 ? velocityOpposition : 0.0) *
                (preserveVelocityCapacity ? 1.0 : balancedForce / Flexor.MaximumIsometricForceNewtons);
            extensorExcitation = tone * (balancedForce / Extensor.MaximumIsometricForceNewtons) +
                (error < 0.0 ? reflex : 0.0) + (AngularVelocity > 0.0 ? velocityOpposition : 0.0) *
                (preserveVelocityCapacity ? 1.0 : balancedForce / Extensor.MaximumIsometricForceNewtons);
        }

'''+marker)
    measured='''    // PhysX supplies position/velocity and performs the only pose integration.
    // Return active muscle torque; native implicit drives supply passive terms.
    public double AdvanceMeasured(double targetAngle, double descendingGain,
        double tone, double measuredAngle, double measuredVelocity, double dt,
        bool preserveActiveCapacity = false, bool preserveVelocityCapacity = true,
        bool suppressDiagnosticFatigue = false, MotorUnitFatigueParameters? diagnosticPoolParameters = null)
    {
        Angle = measuredAngle;
        AngularVelocity = measuredVelocity;
'''+activation+'''        return (Flexor.ForceNewtons - Extensor.ForceNewtons) * momentArm;
    }

'''
    core=core[:begin]+measured+core[end:]
    # Inertia belongs to the physical link, not a second angle integrator.
    core=core.replace('    private readonly double inertia;\n','').replace('        this.inertia = inertia;\n','')
    # Passive properties are consumed by Unity's implicit joint solver.
    core=core.replace('    public AvatarMuscle Flexor { get; }', '''    public double PassiveStiffness => passiveStiffness;
    public double PassiveDamping => damping;
    public double RestAngle => restAngle;
    public AvatarMuscle Flexor { get; }''')
    constants=dict(re.findall(r'public const double (\w+) = ([^;]+);',body))
    profiles=[]
    for match in re.finditer(r'(\w+) = CreateJoint\((.*?)\);',body,re.S):
        variable,args=match.groups()
        if variable not in BINDINGS: continue
        values=[value.strip() for value in args.split(',')]
        if len(values)!=12: raise ValueError('Unexpected muscle profile: '+variable)
        values=[constants.get(value,value) for value in values]
        joint,axis=BINDINGS[variable]
        profiles.append(f'            new MuscleProfile("{variable}", "{joint}", {axis}, '+', '.join(values)+'),')
    if len(profiles)!=19: raise ValueError('Expected all 19 muscle-joint profiles.')
    measurement='''
internal readonly struct PhysicalMuscleMeasurement {
    public readonly string Name, Side;
    public readonly float Activation, ForceNewtons, LengthFraction, VelocityPerSecond, FatigueFraction;
    public PhysicalMuscleMeasurement(string name, string side, float activation, float force,
        float length, float velocity, float fatigue) {
        Name=name; Side=side; Activation=activation; ForceNewtons=force;
        LengthFraction=length; VelocityPerSecond=velocity; FatigueFraction=fatigue;
    }
}
public static class MuscleProfiles {
    public static readonly MuscleProfile[] All = {
'''+ '\n'.join(profiles)+'''
    };
}
'''
    working=[]
    for name in ('WalkingHipFlexionRadians','WalkingHipExtensionRadians','WalkingKneeFlexionRadians'):
        value=re.search(r'private const double '+name+r' = ([^;]+);',body).group(1)
        working.append('    public const double '+name+' = '+value+';')
    measurement+='public static class MuscleWorkingRanges {\n'+'\n'.join(working)+'\n}\n}\n'
    digest=hashlib.sha256((source+body).encode()).hexdigest()
    return '// Generated from AvatarMuscle.cs and AvatarArticulatedBody.cs; do not edit.\n// Source SHA256: '+digest+'\n'+core+measurement

def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true');args=parser.parse_args()
    root=Path(__file__).resolve().parents[1];path=root/'UnityWorld/Assets/NRE/Runtime/MuscleCore.cs';expected=generate(root)
    if args.check:
        if path.read_text(encoding='utf-8-sig')!=expected: raise SystemExit('Unity muscle port is stale.')
        print('Unity muscle equations/profiles match canonical sources.')
    else: path.write_text(expected,encoding='utf-8',newline='\n');print(path)
if __name__=='__main__': main()
