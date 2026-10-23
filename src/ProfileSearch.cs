using System;
using System.Threading;
using System.Collections.Generic;

namespace GoodbyeTray {
 // Dependencies are supplied by the UI; selection and rollback can be tested without changing a service.
 static class ProfileSearch {
  internal sealed class Result { internal int Profile=-1, Score; internal readonly Dictionary<int,int> Scores=new Dictionary<int,int>(); }
  internal static Result Run(int count,int original,bool wasRunning,Action<int> select,Action<bool> power,Func<int> probe,Action<int,int> progress,CancellationToken cancel,Result result=null,int[] candidates=null) {
   if(count<1 || original<0 || original>=count) throw new ArgumentOutOfRangeException("original");
   if(candidates==null) { candidates=new int[count]; for(int i=0;i<count;i++) candidates[i]=i; }
   if(candidates.Length==0) throw new ArgumentException("candidates");
   foreach(int id in candidates) if(id<0 || id>=count) throw new ArgumentOutOfRangeException("candidates");
   int first=Math.Max(0,Array.IndexOf(candidates,original));
   result=result ?? new Result(); Exception failure=null;
   try {
    for(int step=0;step<candidates.Length;step++) {
     cancel.ThrowIfCancellationRequested();
     int profile=candidates[(first+step)%candidates.Length];
     progress(step+1,profile); select(profile); power(true);
     cancel.ThrowIfCancellationRequested();
     int score=probe(); result.Scores[profile]=score; cancel.ThrowIfCancellationRequested();
     // Keep the current profile on ties. A working control site alone is not a recommendation.
     if(score>result.Score) { result.Profile=profile; result.Score=score; }
    }
    return result;
   } catch(Exception ex) { failure=ex; throw; }
   finally {
    try { progress(0,original); } catch { } // Reporting must never prevent recovery.
    var recoveryErrors=new List<Exception>();
    try { select(original); } catch(Exception ex) { recoveryErrors.Add(ex); }
    try { power(wasRunning); } catch(Exception ex) { recoveryErrors.Add(ex); }
    if(recoveryErrors.Count>0) {
     if(failure!=null) recoveryErrors.Insert(0,failure);
     throw new AggregateException(L.T("Test sonrası eski ayarlara tam dönemedik. Profilini ve motorun durumunu kontrol et."),recoveryErrors);
    }
   }
  }
 }
}
