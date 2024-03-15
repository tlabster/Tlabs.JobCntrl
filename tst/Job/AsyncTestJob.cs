using System.Collections.Generic;
using System.Threading.Tasks;

namespace Tlabs.JobCntrl.Test.Job {

  class AsyncTestJob : TestJob {

    protected override Model.IJobResult InternalRun(IReadOnlyDictionary<string, object> runProperties) {
      Log.ProcessStep= "PROC";
      Log.Detail("Start running synchronously...");

      /* Invoke an async method (from sync. InternalTun())
       * (In this case we just run the job code in a task...)
       */
      var tsk= Task<Dictionary<string, object>>.Run(() => {
        Log.Detail("Continue running async...");
        return base.InternalRun(runProperties);
      });

      return CreateAsyncResult(tsk, jobRes => CreateResult(jobRes.ResultObjects , $"Async:{jobRes.Message}"));
    }
    protected override void Dispose(bool disposing) {
      if (!disposing) return;
      Log.DetailFormat("Careful -- Job {0}:{1:X6} gets disposed before async. task completes!!!", Name, GetHashCode());
    }

  }//class AsyncTestJob

}
