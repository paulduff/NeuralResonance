using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// Evidence only: recorded baseline frames are never admitted to a cell. Retain
// every newly generated frame/sample and compare the complete qualified epoch.
internal sealed class RubralCircuitTrace : IDisposable
{
    private readonly StreamReader _oldFrames, _oldMotor;
    private readonly StreamWriter _frames, _admissions, _motor;
    private readonly string _path, _baselinePath;
    private int _frameCount, _motorRows;
    private bool _finished, _disposed;

    internal RubralCircuitTrace(string output, string baseline, bool disconnected, RubralMotorCircuit circuit)
    {
        if(Directory.Exists(output))throw new ArgumentException("Require a fresh circuit evidence folder.");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output,"input.json"),circuit.OriginalInputJson);
        File.WriteAllText(Path.Combine(output,"input-anatomy.json"),circuit.InputAnatomyJson);
        File.WriteAllText(Path.Combine(output,"topology.json"),circuit.TopologyJson);
        _path=Path.Combine(output,"trajectory.csv");
        _baselinePath=Path.Combine(baseline,disconnected?"contacts-disabled":"packets1","trajectory.csv");
        _oldFrames=new(new GZipStream(File.OpenRead(Path.Combine(baseline,"source-frames.jsonl.gz")),CompressionMode.Decompress));
        _oldMotor=new(_baselinePath);
        _frames=Compressed(Path.Combine(output,"source-frames.jsonl.gz"));
        _admissions=Compressed(Path.Combine(output,"admissions.jsonl.gz"));
        _motor=new(_path);
        var initial=circuit.CaptureMotor();string header=string.Join(',',new[]{"timeMs"}.Concat(Enumerable.Range(0,30).Select(i=>"state"+i))
            .Concat(initial.Cell.Contacts.Select(c=>"receptor_"+c.ContactId)).Concat(new[]{"integrationSteps","deliveredEvents","crossings"}));
        Require(header==_oldMotor.ReadLine(),"Original motor contact columns changed.");_motor.WriteLine(header);Record(initial);
    }

    internal void Record(RubralCircuitFrame frame)
    {
        Require(!_finished&&!_disposed,"Circuit evidence is closed.");
        _frames.WriteLine(JsonSerializer.Serialize(frame.OriginalSource));
        _admissions.WriteLine(JsonSerializer.Serialize(new{sourceSequence=frame.OriginalSource.Sequence,frame.Admissions,frame.SourceAcknowledgement}));
        var payload=IsolatedRubralSource.Decode(frame.OriginalSource);string previousText=_oldFrames.ReadLine();
        Require(previousText is not null,"Missing original source endpoint.");
        var previous=IsolatedRubralSource.Decode(JsonSerializer.Deserialize<RubralSourceFrame>(previousText));
        Require(JsonSerializer.Serialize(payload.Observation)==JsonSerializer.Serialize(previous.Observation)&&
            JsonSerializer.Serialize(payload.Energy)==JsonSerializer.Serialize(previous.Energy)&&
            payload.OriginalEvents.Select(e=>(e.SourceNeuronId,e.TimestampMs)).SequenceEqual(previous.OriginalEvents.Select(e=>(e.SourceNeuronId,e.TimestampMs))),
            "Native-body scheduling changed original upstream activity.");
        ++_frameCount;Require(frame.OriginalSource.Sequence==_frameCount,"Noncontiguous original source history.");
        foreach(var sample in frame.MotorSamples)Record(sample);
    }

    private void Record(ReceptorMotorSourceState sample)
    {
        var cell=sample.Cell;var values=new[]{cell.TimeMs}.Concat(cell.MembraneState).Concat(cell.Contacts.Select(r=>r.Value.OpenFraction))
            .Concat(new double[]{cell.IntegrationSteps,cell.DeliveredEvents,cell.Crossings.Length});
        string line=string.Join(',',values.Select(v=>v.ToString("R",CultureInfo.InvariantCulture)));
        Require(line==_oldMotor.ReadLine(),"Native-body scheduling changed an original motor sample at "+cell.TimeMs);
        _motor.WriteLine(line);++_motorRows;
    }

    internal void Finish()
    {
        Require(!_finished&&!_disposed&&_frameCount==12000&&_motorRows==15001,"Incomplete original source/motor horizon.");
        Require(_oldFrames.ReadLine() is null&&_oldMotor.ReadLine() is null,"Original reference has an unaccounted suffix.");
        _frames.Dispose();_admissions.Dispose();_motor.Dispose();
        Require(Hash(_path)==Hash(_baselinePath),"Complete motor trajectory bytes changed.");_finished=true;
    }

    public void Dispose()
    {
        if(_disposed)return;
        _frames.Dispose();_admissions.Dispose();_motor.Dispose();_oldFrames.Dispose();_oldMotor.Dispose();_disposed=true;
    }
    private static StreamWriter Compressed(string path)=>new(new GZipStream(new FileStream(path,FileMode.CreateNew),CompressionLevel.Fastest),new UTF8Encoding(false));
    private static string Hash(string path){using var stream=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(stream));}
    private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
}
