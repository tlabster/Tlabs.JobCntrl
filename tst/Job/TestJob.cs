using System;
using System.Collections.Generic;
using System.Threading;

namespace Tlabs.JobCntrl.Test.Job {

  class TestJob : Model.BaseJob {

    protected override Model.IJob InternalInit() {
      Log.ProcessStep= "INIT";
      Log.DetailFormat("Job {0}:{1:X6} initializing.", Name, GetHashCode());
      return this;
    }

    protected override Model.IJobResult InternalRun(IDictionary<string, object> runProperties) {
      Log.ProcessStep= "PROC";
      Log.Detail("Start running test processing.");
      Log.InfoFormat("Activated with {0:D} propertie(s):", runProperties.Count);

      var resObj= new Dictionary<string, object>();
      resObj["Job"]= this.Name;

      foreach (var pair in runProperties) {
        Log.InfoFormat("{0}: {1}", pair.Key, pair.Value);
        resObj[string.Format("{0}-{1}", this.Name, pair.Key)]= pair.Value;
      }
      var minWait= PropertyInt("min-Wait", 300);
      var maxWait= PropertyInt("max-Wait", 800);
      var rnd= new Random(GetHashCode());
      var msec= minWait + rnd.Next(maxWait-minWait);
      Log.InfoFormat("Waiting for {0:D}ms", msec);
      Thread.Sleep(msec);

      if (PropertyBool("throw", false))
        throw new ApplicationException("Property: 'throw' == true");

      Log.ProcessStep= "TERM";
      return CreateResult(resObj, "OK");
    }

    protected override void Dispose(bool disposing) {
      if (!disposing) return;
      Log.DetailFormat("Job {0}:{1:X6} disposed.", Name, GetHashCode());
    }
  }//class TestJob

}
