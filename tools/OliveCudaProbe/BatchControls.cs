using System.Security.Cryptography;
using System.Text.Json;

internal static class BatchControls
{
    internal static object[] Run(CudaDriverProbe cuda,nint kernel,Action<object[]> progress)
    {
        var a = new OlivaryCellIdentity("isolated-left","N_0"); var b = new OlivaryCellIdentity("isolated-right","N_0");
        OlivaryCellSeed Seed(OlivaryCellIdentity id) => OlivaryCellSeed.Capture(id,0,new());
        string Hash(IEnumerable<OlivaryCellSeed> cells) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            cells.OrderBy(c => c.Identity.Instance,StringComparer.Ordinal).ThenBy(c => c.Identity.Cell,StringComparer.Ordinal))));
        void Require(bool value,string reason) { if (!value) throw new InvalidOperationException(reason); }
        var rows = new List<object>();
        void Record(object row) { rows.Add(row); progress(rows.ToArray()); }
        var seed = Seed(a); seed.State[3] += Math.ScaleB(1d,-45);
        seed = seed with {SomaticSpikes=42,AxonalSpikes=17,AdditionalIonicLoad=.125,SynapticCurrent=-.375};
        using (var batch = new OlivaryCudaBatch(cuda,kernel,[seed,Seed(b)]))
        {
            string expected = Hash([seed,Seed(b)]), initial = Hash(batch.SnapshotAll());
            Require(initial == expected,"Initial double state/counter transfer changed.");
            seed.State[0] = 12; var exposed = batch.SnapshotAll(); exposed[0].State[1] = 12;
            Require(Hash(batch.SnapshotAll()) == initial,"Caller changed owned device state.");
            Record(new {name="seed-transfer-and-ownership",passed=true,before=expected,after=initial});
            var good = new OlivaryBatchStep(a,0,.125,default);
            var invalid = new[] {new OlivaryBatchStep(b,1,.125,default),new OlivaryBatchStep(b,-1,.125,default),
                new OlivaryBatchStep(b,0,double.NaN,default),new OlivaryBatchStep(b,0,.125,new(DendriticAmpa:-1)),
                new OlivaryBatchStep(new("missing","N_0"),0,.125,default),good};
            foreach (var bad in invalid)
            {
                bool rejected = false; try { batch.Advance([good,bad]); } catch (ArgumentException) { rejected=true; }
                Require(rejected && Hash(batch.SnapshotAll()) == initial,"Invalid interval changed accepted state.");
            }
            Record(new {name="invalid-requests-atomic",passed=true,rejected=invalid.Length,before=initial,after=Hash(batch.SnapshotAll())});
            Exception? error = null;
            var thread = new Thread(() => {try {batch.Advance([good]);}catch(Exception e){error=e;}});
            thread.Start(); thread.Join();
            Require(error is InvalidOperationException && Hash(batch.SnapshotAll()) == initial,"Foreign thread used the CUDA owner.");
            Record(new {name="thread-ownership",passed=true,before=initial,after=Hash(batch.SnapshotAll())});
            string untouched = Hash(batch.SnapshotAll().Where(c => c.Identity==b));
            for (int frame=0;frame<32;++frame) batch.Advance([new(a,frame*.125,.125,new(DendriticCurrent:8))]);
            string after = Hash(batch.SnapshotAll().Where(c => c.Identity==b));
            Require(after == untouched,"Unrequested cell advanced."); batch.VerifyReadOnlyBuffersAndGuards();
            Record(new {name="untouched-cell",passed=true,before=untouched,after});
        }
        using (var first = new OlivaryCudaBatch(cuda,kernel,[Seed(a),Seed(b)]))
        using (var second = new OlivaryCudaBatch(cuda,kernel,[Seed(b),Seed(a)]))
        {
            var hashes = new List<string>();
            for (int frame=0;frame<64;++frame)
            {
                OlivaryBatchStep[] steps = [new(a,frame*.125,.125,new(DendriticCurrent:8)),new(b,frame*.0625,.0625,new(SomaticGaba:.1))];
                Require(first.Advance(steps).SequenceEqual(second.Advance(steps.Reverse().ToArray())),"Permutation changed reports.");
                string one=Hash(first.SnapshotAll()), two=Hash(second.SnapshotAll());
                Require(one==two,"Permutation changed physical state."); hashes.Add(one);
            }
            first.VerifyReadOnlyBuffersAndGuards(); second.VerifyReadOnlyBuffersAndGuards();
            Record(new {name="identity-permutation",passed=true,frames=64,matchedStateHashes=hashes});
        }
        using (var batch = new OlivaryCudaBatch(cuda,kernel,[Seed(a),Seed(b)]))
        using (var clean = new OlivaryCudaBatch(cuda,kernel,[Seed(a),Seed(b)]))
        {
            string before=Hash(batch.SnapshotAll()); bool rejected=false;
            try {batch.Advance([new(a,0,.125,default),new(b,0,.125,new(DendriticCurrent:double.MaxValue))]);}
            catch (InvalidOperationException error) {rejected=error.Message.Contains("public state not committed",StringComparison.Ordinal);}
            string after=Hash(batch.SnapshotAll()); Require(rejected && before==after,"Failed lane partially committed.");
            OlivaryBatchStep[] steps = [new(a,0,.125,default),new(b,0,.125,default)];
            Require(batch.Advance(steps).SequenceEqual(clean.Advance(steps)),"Explicit valid request after failure differs.");
            string recovered=Hash(batch.SnapshotAll()); Require(recovered==Hash(clean.SnapshotAll()),"Staging retained a partial failed step.");
            batch.VerifyReadOnlyBuffersAndGuards(); clean.VerifyReadOnlyBuffersAndGuards();
            Record(new {name="numerical-failure-atomic",passed=true,before,after,explicitNextRequestHash=recovered});
        }
        return rows.ToArray();
    }
}
