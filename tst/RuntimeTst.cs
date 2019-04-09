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
    AppTimeEnvironment appTimeEnv;

    public RuntimeTst(AppTimeEnvironment appTimeEnv, ITestOutputHelper tstout) {
      this.appTimeEnv= appTimeEnv;
      this.tstout= tstout;
    }

    class TestMasterCfg : IMasterCfg {
      private IReadOnlyDictionary<string, MasterStarter> starters= new ModelDictionary<MasterStarter> {
        ["MANUAL"]= new MasterStarter("MANUAL", "Manual activation only", typeof(Manual), null),
        ["SCHEDULE"]= new MasterStarter("SCHEDULE", "Time scheduled starter activation.", typeof(TimeSchedule), null),
        ["CHAINED"]= new MasterStarter("CHAINED", "Chained activation after completion of previous starter.", typeof(Chained), null),
      }.AsReadonly();
      private IReadOnlyDictionary<string, MasterJob> jobs= new ModelDictionary<MasterJob> {
        ["TEST"]= new MasterJob("TEST", "Test job.", typeof(Job.TestJob), null)
      }.AsReadOnly();

      public IReadOnlyDictionary<string, MasterStarter> Starters => starters;

      public IReadOnlyDictionary<string, MasterJob> Jobs => jobs;
    }
    class StarterCfg : IModelCfg {
      private string masterName;
      private string name;
      private string description;
      private IReadOnlyDictionary<string, object> properties;
      public StarterCfg(string master, string name, string description, IReadOnlyDictionary<string, object> properties) {
        this.masterName= master;
        this.name= name;
        this.description= description;
        this.properties= properties;
      }
      public string Master => masterName;
      public string Name => name;
      public string Description => description;
      public IReadOnlyDictionary<string, object> Properties => properties;
      public void Dispose() { }
    }
    class JobCfg : StarterCfg, IJobCfg {
      private string starter;
      public JobCfg(string master, string name, string starterName, string description, IReadOnlyDictionary<string, object> properties) : base(master, name, description, properties) {
        this.starter= starterName;
      }
      public string Starter => starter;
    }
    class TestJobCntrlCfg : IJobControlCfg {
      private IMasterCfg masterModels;
      private IEnumerable<IModelCfg> starters= new List<IModelCfg> {
        new StarterCfg("MANUAL", "ManualStarter", "Manual starter activation", null),
        new StarterCfg("CHAINED", "ChainedStarter", "Chained starter activation", new ConfigProperties {
          [Chained.PROP_COMPLETED_STARTER]= "ManualStarter"
        })
      };
      private IEnumerable<IJobCfg> jobs= new List<IJobCfg> {
        new JobCfg("TEST", "Job1.1", "ManualStarter", "Stage-1 / Job-1", null ),
        new JobCfg("TEST", "Job1.2", "ManualStarter", "Stage-1 / Job-2", new ConfigProperties {
          ["min-Wait"]= 900,
          ["max-Wait"]= 1300
        }),
        new JobCfg("TEST", "Job2.1", "ChainedStarter", "Stage-2 / Job-1", null ),
        new JobCfg("TEST", "Job2.2", "ChainedStarter", "Stage-2 / Job-2", new ConfigProperties {
          ["throw"]= true
        }),
      };
      public TestJobCntrlCfg(IMasterCfg masterCfg) { this.masterModels= masterCfg; }
      public IMasterCfg MasterModels => masterModels;
      public IEnumerable<IModelCfg> Starters => starters;
      public IEnumerable<IJobCfg> Jobs => jobs;
    }

    class TestConfigLoader : IJobControlCfgLoader {
      IJobControlCfg runtimeCfg= new TestJobCntrlCfg(new TestMasterCfg());
      public IJobControlCfgPersister ConfigPersister => throw new System.NotImplementedException();

      public IMasterCfg LoadMasterConfiguration() => runtimeCfg.MasterModels;

      public IJobControlCfg LoadRuntimeConfiguration(IMasterCfg masterCfg) => runtimeCfg;
    }

    class TestStarterCompletion : IStarterCompletionPersister {
      public event Action<IStarterCompletionPersister, IStarterCompletion, object> CompletionInfoPersisted;
      public Stream GetLastCompletionInfo(string starterName, out string contentType, out Encoding encoding) => throw new NotImplementedException();
      public void StoreCompletionInfo(IStarterCompletion starterCompletion) => CompletionInfoPersisted?.Invoke(this, starterCompletion, null);
    }

    [Fact]
    public void BasicRuntimeTest() {
      var rt= new JobCntrlRuntime(new TestConfigLoader(), App.Logger<JobCntrlRuntime>());
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
          Assert.Equal(res.JobName != "Job2.2", res.IsSuccessful);
          if (null != res.ProcessingLog) foreach(var ent in res.ProcessingLog.Entries)
            tstout.WriteLine($"\t\tStep: {ent.ProcessStep}");
        }
        if (++completionCnt > 1)
          actComplete.SignalPermanent(true);
      };
      var rt= new JobCntrlRuntime(new TestConfigLoader(), starterCompletion, App.Logger<JobCntrlRuntime>());
      rt.Init();
      rt.Start();

      var manualStarter= rt.Starters["manualStarter"];
      Assert.True(manualStarter.Enabled);
      manualStarter.DoActivate(new ConfigProperties {
        ["TST-PROP"]= "manual activation test"
      });
      actComplete.WaitForSignal();
      Assert.True(completionCnt > 0);
    }
  }

}