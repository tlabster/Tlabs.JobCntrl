using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

using Tlabs.JobCntrl.Intern;
using Tlabs.JobCntrl.Model;
using Tlabs.JobCntrl.Model.Intern;
using Tlabs.JobCntrl.Model.Intern.Starter;
using System.IO;
using System.Text;
using Tlabs.Test.Common;

namespace Tlabs.JobCntrl.Test {

  [Collection("AppTimeScope")]
  public class RuntimeTst {
    ITestOutputHelper tstout;
    SvcProvEnvironment appTimeEnv;
    Config.JsonJobCntrlCfgLoader tstCfgLoader;

    public RuntimeTst(SvcProvEnvironment appTimeEnv, ITestOutputHelper tstout) {
      this.appTimeEnv= appTimeEnv;
      this.tstout= tstout;

      var cfg= new Config.JobCntrlConfigurator();
      cfg.DefineMasterStarter(name: "MANUAL", description: "Manual activation only", type: typeof(Manual).AssemblyQualifiedName)
         .DefineMasterStarter(name: "SCHEDULE", description: "Time scheduled starter activation.", type: typeof(TimeSchedule).AssemblyQualifiedName)
         .DefineMasterStarter(name: "CHAINED", description: "Chained activation after completion of previous starter.", type: typeof(Chained).AssemblyQualifiedName)

         .DefineMasterJob(name: "TEST", description: "Test job.", type: typeof(Job.TestJob).AssemblyQualifiedName)

         .DefineStarter(master: "MANUAL", name: "ManualStarter", description: "Manual starter activation")
         .DefineStarter(master: "CHAINED", name: "ChainedStarter", description: "Chained starter activation", properties: new Dictionary<string, object> {
           [Chained.PROP_COMPLETED_STARTER]= "ManualStarter"
         })
         .DefineJob(master: "TEST", name: "Job1.1", starter: "ManualStarter", description: "Stage-1 / Job-1", properties: new Dictionary<string, object> {
           ["Log-Level"]= "Detail",
           ["jobProp01"]= "jobProp01"
         })
         .DefineJob(master: "TEST", name: "Job1.2", starter: "ManualStarter", description: "Stage-1 / Job-2", properties: new Dictionary<string, object> {
           ["min-Wait"]= 900,
           ["max-Wait"]= 1300
         })
        .DefineJob(master: "TEST", name: "Job2.1", starter: "ChainedStarter", description: "Stage-2 / Job-1")
        .DefineJob(master: "TEST", name: "Job2.2", starter: "ChainedStarter", description: "Stage-2 / Job-2", properties: new Dictionary<string, object> {
          ["throw"]= true
        });

      this.tstCfgLoader= new Config.JsonJobCntrlCfgLoader(
        new Config.JobCntrlCfgLoaderProperties(new Dictionary<string, string> { ["path"]= "EmptyCfg.json"} ),
        new IJobCntrlConfigurator[] { cfg }
      );
    }


    class TestStarterCompletion : IStarterCompletionPersister {
      public event Action<IStarterCompletionPersister, IStarterCompletion, object> CompletionInfoPersisted;
      public Stream GetLastCompletionInfo(string starterName, out string contentType, out Encoding encoding) => throw new NotImplementedException();
      public void StoreCompletionInfo(IStarterCompletion starterCompletion) => CompletionInfoPersisted?.Invoke(this, starterCompletion, null);
    }

    [Fact]
    public void BasicRuntimeTest() {
      var rt= new JobCntrlRuntime(this.tstCfgLoader, App.Logger<JobCntrlRuntime>());
      Assert.Throws<InvalidOperationException>(() => rt.Start());
      rt.Init();
      Assert.Throws<InvalidOperationException>(() => rt.Init());
      rt.Start();
      Assert.Throws<InvalidOperationException>(() => rt.Start());
      Assert.NotNull(rt.Starters["MANualStarter"]);
      rt.Stop();
    }

    [Fact]
    public void StartedRuntimeTest() {
      var actComplete= new Tlabs.Sync.SyncMonitor<bool>();
      int completionCnt= 0;
      var starterCompletion= new TestStarterCompletion();
      starterCompletion.CompletionInfoPersisted+= (p, compl, o) => {
        tstout.WriteLine($"Starter '{compl.StarterName}' completed running jobs:");
        foreach(var res in compl.JobResults) {
          tstout.WriteLine($"\tJob '{res.JobName}' ({res.Message})");
          if (res.JobName != "Job2.2" != res.IsSuccessful)
            tstout.WriteLine($"Job '{res.JobName}' failed: {res.Message}");
          if (null != res.ProcessingLog) foreach(var ent in res.ProcessingLog.Entries)
            tstout.WriteLine($"\t\tStep: {ent.ProcessStep} {ent.Message}");
        }
        if (++completionCnt > 0)
          actComplete.SignalPermanent(true);
      };

      var rt= new JobCntrlRuntime(this.tstCfgLoader, starterCompletion, App.Logger<JobCntrlRuntime>());
      rt.Init();
      rt.Start();

      var manualStarter= rt.Starters["manualStarter"];
      Assert.True(manualStarter.Enabled);
      manualStarter.DoActivate(new ConfigProperties {
        ["TST-RUN-PROP"]= "manual activation test"
      });
      actComplete.WaitForSignal(1500);
      Assert.True(completionCnt > 0);
    }
  }

}