#nullable disable
using System;
using System.IO;
using System.Text;

namespace Nre.UnityWorld {
public enum BridgeReadOutcome { Ready, Missing, SharingViolation }

// Immutable completed files are published by rename. A Windows sharing/region
// lock delays READING that file; it never authorizes resending a physical command.
public static class DiagnosticBridgeFiles {
 public static bool IsSharingViolation(IOException error){int code=error.HResult&0xffff;return code==32||code==33;}
 public static BridgeReadOutcome TryRead(string path,out string text){
  text=null;
  try {
   using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete))
   using(var reader=new StreamReader(stream,new UTF8Encoding(false,true),true))text=reader.ReadToEnd();
   return BridgeReadOutcome.Ready;
  }catch(FileNotFoundException){return BridgeReadOutcome.Missing;}
   catch(DirectoryNotFoundException){return BridgeReadOutcome.Missing;}
   catch(IOException error) when(IsSharingViolation(error)){return BridgeReadOutcome.SharingViolation;}
  // Permission errors, invalid data and other I/O failures are not transient
  // lock diagnoses. Their original exceptions remain visible to the owner.
 }
 // For finite startup/failure evidence reads outside the native coroutine.
 // Only missing/locked immutable reads are retried, under one fixed deadline.
 public static string ReadPublished(string path,int timeoutMilliseconds){
  if(timeoutMilliseconds<=0)throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds));
  var timer=System.Diagnostics.Stopwatch.StartNew();
  while(true){
   if(TryRead(path,out var text)==BridgeReadOutcome.Ready)return text;
   if(timer.Elapsed.TotalMilliseconds>=timeoutMilliseconds)throw new TimeoutException("Published evidence remained unavailable: "+path);
   System.Threading.Thread.Sleep(2);
  }
 }
}
}
