using System.Security.Cryptography;
using System.Text.Json;

internal static class HostControls
{
    internal static object[] Run(CudaDriverProbe cuda,nint kernel,Action<object[]> progress)
    {
        OlivaryCellIdentity[] ids=[new("z","2"),new("a","10"),new("a","2"),new("A","2"),new("α","1"),new("z","1"),new("aa","0")];
        OlivaryCellSeed Seed(OlivaryCellIdentity id)=>OlivaryCellSeed.Capture(id,0,new());
        string Hash(object value)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
        string State(OlivaryCudaBatch b)=>Hash(b.SnapshotAll().OrderBy(c=>c.Identity.Instance,StringComparer.Ordinal).ThenBy(c=>c.Identity.Cell,StringComparer.Ordinal));
        void Require(bool value,string why){if(!value)throw new InvalidOperationException(why);}
        var rows=new List<object>();
        void Record(object row){rows.Add(row);progress(rows.ToArray());}
        using(var first=new OlivaryCudaBatch(cuda,kernel,ids.Select(Seed).ToArray()))
        using(var second=new OlivaryCudaBatch(cuda,kernel,ids.Reverse().Select(Seed).ToArray()))
        {
            var clocks=new double[ids.Length];var frames=new List<object>();
            OlivaryBatchAdvance[]? retained=null;string? retainedHash=null;
            for(int frame=0;frame<40;++frame)
            {
                var indices=Enumerable.Range(0,ids.Length).Where(i=>(i+frame)%4!=0).ToArray();
                var steps=indices.Select(i=>new OlivaryBatchStep(ids[i],clocks[i],((i+frame)%3+1)/32d,
                    new(DendriticCurrent:8+i,SomaticGaba:frame%5==0?.1:0))).ToArray();
                var one=first.Advance(steps.Reverse().ToArray());var two=second.Advance(steps);
                Require(one.SequenceEqual(two),"Reordered subset changed reports.");
                var expected=steps.OrderBy(s=>s.FromMs+s.DurationMs).ThenBy(s=>s.Identity.Instance,StringComparer.Ordinal)
                    .ThenBy(s=>s.Identity.Cell,StringComparer.Ordinal).Select(s=>s.Identity);
                Require(one.Select(r=>r.Identity).SequenceEqual(expected),"End-time/ordinal identity order differs.");
                foreach(int i in indices)clocks[i]+=((i+frame)%3+1)/32d;
                foreach(var c in first.SnapshotAll())Require(c.TimeMs==clocks[Array.IndexOf(ids,c.Identity)],"Subset clock changed.");
                string hash=State(first);Require(hash==State(second),"Reordered subset changed state.");
                if(retained is not null)Require(Hash(retained)==retainedHash,"Later work changed a retained report.");
                if(frame==0){retained=one;retainedHash=Hash(one);}
                frames.Add(new {frame,reports=one,stateHash=hash});
            }
            first.VerifyReadOnlyBuffersAndGuards();second.VerifyReadOnlyBuffersAndGuards();
            Record(new {name="changing-subsets-and-order",passed=true,frames});
            string before=State(first);
            var invalid=new OlivaryBatchStep(ids[1],clocks[1]+1,.0625,default);
            bool rejected=false;
            try{first.Advance([new(ids[0],clocks[0],.0625,default),invalid]);}catch(ArgumentException){rejected=true;}
            Require(rejected&&State(first)==before,"Partial request validation changed public state.");
            first.VerifyReadOnlyBuffersAndGuards();
            Record(new {name="rejected-request-preserves-upload",passed=true,before,after=State(first)});
            Require(first.Advance([]).Length==0&&State(first)==before,"Empty request advanced state.");
            first.VerifyReadOnlyBuffersAndGuards();
            Record(new {name="empty-after-valid-and-rejected",passed=true,before,after=State(first)});
            OlivaryBatchStep[] next=[new(ids[2],clocks[2],.0625,new(DendriticCurrent:9))];
            var oneNext=first.Advance(next);var twoNext=second.Advance(next);
            Require(oneNext.SequenceEqual(twoNext)&&State(first)==State(second),"Rejected scratch contaminated next subset.");
            oneNext[0]=default;
            Require(State(first)==State(second),"Returned report mutation changed internal state.");
            first.VerifyReadOnlyBuffersAndGuards();second.VerifyReadOnlyBuffersAndGuards();
            Record(new {name="returned-report-ownership",passed=true,stateHash=State(first),expectedReports=twoNext});
        }
        using(var failed=new OlivaryCudaBatch(cuda,kernel,ids.Select(Seed).ToArray()))
        using(var clean=new OlivaryCudaBatch(cuda,kernel,ids.Select(Seed).ToArray()))
        {
            string before=State(failed);bool rejected=false;
            try{failed.Advance([new(ids[0],0,.125,default),new(ids[6],0,.125,new(DendriticCurrent:double.MaxValue))]);}
            catch(InvalidOperationException e){rejected=e.Message.Contains("public state not committed",StringComparison.Ordinal);}
            Require(rejected&&State(failed)==before,"Numerical failure committed a lane.");
            failed.VerifyReadOnlyBuffersAndGuards();
            OlivaryBatchStep[] next=[new(ids[1],0,.0625,new(DendriticCurrent:8))];
            var reports=failed.Advance(next);
            Require(reports.SequenceEqual(clean.Advance(next))&&State(failed)==State(clean),"Failed active lanes leaked into later subset.");
            failed.VerifyReadOnlyBuffersAndGuards();clean.VerifyReadOnlyBuffersAndGuards();
            Record(new {name="failed-work-then-different-subset",passed=true,before,after=State(failed),reports});
        }
        var disposed=new OlivaryCudaBatch(cuda,kernel,[Seed(ids[0])]);disposed.Dispose();disposed.Dispose();
        bool disposedRejected=false;try{disposed.Advance([]);}catch(ObjectDisposedException){disposedRejected=true;}
        Require(disposedRejected,"Disposed empty request accepted.");
        Record(new {name="disposed-empty-request",passed=true});
        return rows.ToArray();
    }
}
