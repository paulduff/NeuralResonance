using static Nre.UnityWorld.MuscleHingeWire;

// Diagnose the known strong-left accuracy failure without changing its circuit,
// biological parameters, initial state or endpoint. Other directions, cut and
// retry controls remain supported only at their previously captured resolution.
internal static class RubralNativeRefinementStudy
{
    internal const string Version="rubral-native-partition-refinement-v1";
    private sealed record Resolution(string Key,int PhysicsResolution,int MuscleSubsteps,int Steps);
    private static readonly Resolution[] Resolutions=[
        new("repeat",1,2,7999),
        new("muscle-fine",1,4,7999),
        new("body-fine",2,2,15998),
        new("body-finer",3,1,31996),
        new("body-finer-muscle-fine",3,2,31996)];

    internal static void Run(string player,string reference,string sourceGate,string output)
    {
        Require(System.IO.Path.IsPathFullyQualified(output)&&!Directory.Exists(output)&&!File.Exists(output),"Require new absolute refinement output.");
        double end=7999*StepMsAt(1),inner=StepMsAt(1)/4;
        foreach(var r in Resolutions)Require(r.Steps*StepMsAt(r.PhysicsResolution)==end,"Refinement duration drifted.");
        foreach(var r in Resolutions.Skip(1).Take(3))Require(StepMsAt(r.PhysicsResolution)/r.MuscleSubsteps==inner,"Independent physical comparison changed muscle step.");
        Directory.CreateDirectory(output);
        NativeHingeBody.Write(System.IO.Path.Combine(output,"protocol.json"),new{version=Version,resolutions=Resolutions,caseKey="strong-left",
            durationMs=end,commonSamples=8000,actualPhysicsSteps=Resolutions.Sum(r=>r.Steps),newNeuronalEpochs=5,
            originalBoundsChanged=false,fullCaseMatrixAssessed=false,referenceOnlyMotorReplay=false,
            neuronalModelExecuted=true,nativePhysicsExecuted=true,physiologyQualified=false,avatarRecoveryAssessed=false});
        var rows=new List<object>();
        foreach(var r in Resolutions)
        {
            Require(new DriveInfo(System.IO.Path.GetPathRoot(output)).AvailableFreeSpace>=2L*1024*1024*1024,
                "Less than 2 GiB recording space remains; preserve completed conditions and stop before another epoch.");
            string directory=System.IO.Path.Combine(output,r.Key);Directory.CreateDirectory(directory);
            string wireEvidence=System.IO.Path.Combine(directory,"reference");Directory.CreateDirectory(wireEvidence);
            foreach(string name in new[] { "failed-request-000033.json","last-reply-000032.json" })
                File.Copy(System.IO.Path.Combine(reference,name),System.IO.Path.Combine(wireEvidence,name),overwrite:false);
            Console.WriteLine("RUBRAL BODY/MUSCLE REFINEMENT "+r.Key);
            // Each condition evolves the original neuronal source again. Saved
            // emissions are comparison evidence, never the drive of this body.
            RubralNativeBodyStudy.RunAt(player,reference,sourceGate,System.IO.Path.Combine(directory,"native-bridge"),
                r.PhysicsResolution,r.MuscleSubsteps,strongLeftOnly:true);
            rows.Add(new{key=r.Key,completed=true,physicsSteps=r.Steps});
        }
        NativeHingeBody.Write(System.IO.Path.Combine(output,"results.json"),new{completed=true,conditions=rows,
            actualPhysicsSteps=Resolutions.Sum(r=>r.Steps),fullCaseMatrixAssessed=false,coupledTrajectoryAccuracyQualified=false,
            physiologyQualified=false,avatarRecoveryAssessed=false});
    }
}
