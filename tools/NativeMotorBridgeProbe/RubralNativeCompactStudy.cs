using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Path=System.IO.Path;

// Explicit new-run recording adapter. The existing circuit, source traces,
// physical/muscle step and full seven-condition accounting remain in RunAt.
internal static class RubralNativeCompactStudy
{
    internal static void Run(string player,string reference,string sourceGate,string package,string python,string publisher)
    {
        foreach(string path in new[]{player,reference,sourceGate,package,python,publisher})
            Require(Path.IsPathFullyQualified(path),"Absolute frozen inputs required.");
        Require(Directory.Exists(package)&&File.Exists(python)&&File.Exists(publisher),"Missing recording publisher inputs.");
        string output=Path.Combine(package,"native-bridge"),storage=Path.Combine(package,"capture-storage");
        Require(!Directory.Exists(output)&&!File.Exists(output),"Existing raw captures cannot be adopted.");
        using var ownership=JsonDocument.Parse(File.ReadAllText(Path.Combine(storage,"ownership.json")));
        var owner=ownership.RootElement;
        Require(owner.GetProperty("version").GetString()=="owned-new-native-capture-publication-v1"&&
            owner.GetProperty("package").GetString()==package&&owner.GetProperty("rawRoot").GetString()==output&&
            owner.GetProperty("rawRootAbsentAtCreation").GetBoolean()&&!owner.GetProperty("existingCapturesMayBeAdopted").GetBoolean(),
            "Require a fresh explicitly owned publication store.");
        string ownerId=owner.GetProperty("owner").GetString();
        Require(Guid.TryParse(ownerId,out var id)&&id!=Guid.Empty,"Nonempty capture ownership required.");
        void Publish(string key)
        {
            Require(new DriveInfo(Path.GetPathRoot(package)).AvailableFreeSpace>=10L*1024*1024*1024,
                "Preserve the completed raw condition: 10 GiB publication reserve required.");
            Require(owner.GetProperty("conditions").EnumerateArray().Any(x=>x.GetString()==key),"Undeclared native condition.");
            string folder=Path.Combine(output,key),finalPath=Path.Combine(folder,"final.json");
            Require(!File.Exists(Path.Combine(folder,"failure.json")),"Never publish a failed native prefix.");
            using var final=JsonDocument.Parse(File.ReadAllText(finalPath));var plant=final.RootElement.GetProperty("plant");
            Require(plant.GetProperty("completedPhysicsStep").GetInt64()==31996&&!plant.GetProperty("running").GetBoolean()&&
                !plant.GetProperty("faulted").GetBoolean(),"Require the complete paused native condition.");
            string hash;
            using(var stream=File.OpenRead(finalPath))hash=Convert.ToHexString(SHA256.HashData(stream));
            using(var file=new FileStream(Path.Combine(storage,key+".closed.json"),FileMode.CreateNew,FileAccess.Write,FileShare.Read))
            {
                JsonSerializer.Serialize(file,new {owner=ownerId,key,physicsSteps=31996,completionSha256=hash,
                    nativeProcessExitedSuccessfully=true,recordingScopesDisposed=true});file.Flush(flushToDisk:true);
            }
            Console.WriteLine("PUBLISH CLOSED NATIVE CONDITION "+key);
            var start=new ProcessStartInfo(python){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=package};
            foreach(string argument in new[]{"-B",publisher,"publish-closed",package,key})start.ArgumentList.Add(argument);
            using var process=Process.Start(start)??throw new InvalidOperationException("Capture publisher did not start.");
            try{process.WaitForExit();Require(process.ExitCode==0,"Capture publication failed; preserve its archives and remaining raw records.");}
            finally{if(!process.HasExited){process.Kill(entireProcessTree:true);process.WaitForExit();}}
            Require(!Directory.Exists(folder)&&File.Exists(Path.Combine(storage,key+".released.json")),"Publication did not complete owned raw release.");
            Require(new DriveInfo(Path.GetPathRoot(package)).AvailableFreeSpace>=10L*1024*1024*1024,
                "Preserve published conditions and stop before another epoch: 10 GiB reserve required.");
        }
        RubralNativeBodyStudy.RunAt(player,reference,sourceGate,output,3,1,afterConditionClosed:Publish);
    }
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}
