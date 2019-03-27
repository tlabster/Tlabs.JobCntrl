using System.Collections.Generic;
using Xunit;

using Tlabs.JobCntrl.Model;
using Tlabs.JobCntrl.Model.Intern;
using Tlabs.Test.Common;

namespace Tlabs.JobCntrl.Test {
  [CollectionDefinition("AppTimeScope")]
  public class AppTimeCollection : ICollectionFixture<AppTimeEnvironment> {
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
  }

  [Collection("AppTimeScope")]
  public class JobStarterTst {
    const string ACTIVATOR_RUN_PROP= "TmplRun.Prop";
    const string ACTIVATOR_RUN_PROP_KEY= BaseStarter.RUN_PROPERTY_PREFIX + ACTIVATOR_RUN_PROP;

    public ConfigProperties masterProps, runProps;
    public int starterInitCnt;
    public int ActivationCnt;
    public int starterCompletionCnt;

    public int jobInitCnt;
    public int jobRunCnt;
    public int jobDisposeCnt;
    private AppTimeEnvironment appTimeEnv;

    //StarterActivationCompleter completionTest;

    public JobStarterTst(AppTimeEnvironment appTimeEnv) {
      this.appTimeEnv= appTimeEnv;
      this.masterProps= new ConfigProperties {
        ["Tester"]= this,
        ["Test.Prop"]= 1,
        ["TEMPL.prop"]= "x",
        [ACTIVATOR_RUN_PROP_KEY]= ACTIVATOR_RUN_PROP
      };
      this.runProps= new ConfigProperties {
        ["run.prop"]= "xxx"
      };
    }

    class TestStarterImpl : BaseStarter {
      private JobStarterTst tester;

      protected override IStarter InternalInit() {
        object o;
        if (Properties.TryGetValue("tesTEr", out o)) {
          tester= (JobStarterTst)Properties["tesTEr"];
          ++tester.starterInitCnt;
        }
        return this;
      }

      protected override void ChangeEnabledState(bool enabled) {
        isEnabled= enabled;
      }
    }

    class TestJobImpl : BaseJob {
      private JobStarterTst tester;
      protected override IJob InternalInit() {
        tester= (JobStarterTst)Properties["tesTEr"];
        ++tester.jobInitCnt;
        return this;
      }
      protected override IJobResult InternalRun(IDictionary<string, object> runProperties) {
        tester= (JobStarterTst)runProperties["tesTEr"];
        ++tester.jobRunCnt;
        return CreateResult(true);
      }
      protected override void Dispose(bool disposing) {
        if (!disposing) return;
        tester= (JobStarterTst)Properties["tesTEr"];
        ++tester.jobDisposeCnt;
      }
    }

    [Fact]
    public void BasicStarterTest() {
      var masterStarter= new MasterStarter("0", "0", typeof(TestStarterImpl), null);
      IRuntimeStarter runtimeStarter= (IRuntimeStarter)masterStarter.CreateRuntimeStarter("tstRuntimeStarter", masterStarter.Description, null);
      runtimeStarter.Activate+= (starter, props) => ++ActivationCnt;
      runtimeStarter.Enabled= true;
      runtimeStarter.DoActivate(null);
      Assert.Equal(1, ActivationCnt);


      starterInitCnt= ActivationCnt= 0;
      masterStarter= new MasterStarter("tstMasterStarter", "Test Starter description", typeof(TestStarterImpl), this.masterProps);
      Assert.Equal(0, starterInitCnt);//, "MasterStarter must not create target Starter from ctor");
      Assert.Equal("tstMasterStarter", masterStarter.Name);//, "bad StarterTempl.Name");

      Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(masterStarter.Properties);//, "Starter templProperties must be read-only");
      Assert.Equal(masterProps.Count, masterStarter.Properties.Count);
      Assert.Equal("x", masterStarter.Properties["TEMPL.prop"]);

      var starterRunProps= new Dictionary<string, object> {
        {"Inst.Prop", "xx"}
      };
      runtimeStarter= (IRuntimeStarter)masterStarter.CreateRuntimeStarter("tstRuntimeStarter", masterStarter.Description, starterRunProps);
      Assert.IsNotType(typeof(TestStarterImpl), runtimeStarter);
      Assert.Equal(1, starterInitCnt);//, "MasterStarter must have created target Starter with CreateRuntimeStarter()");
      Assert.True(runtimeStarter.Properties.ContainsKey(MasterStarter.PROP_RUNTIME));//, "expected PROP_RUNTIME");
      Assert.Equal(starterRunProps.Count + masterProps.Count + 1, runtimeStarter.Properties.Count);

      runtimeStarter.Enabled= true;
      runtimeStarter.DoActivate(null);
      Assert.Equal(0, ActivationCnt);//, "Starter must NOT activate with no event subscriptions");

      var activationProps= new Dictionary<string, object> {
        {"activation.Prop", "xxx"}
      }.AsReadOnly();   //make sure that these activationProps are not getting changed!!!!
      runtimeStarter.Activate+= (starter, props) => {
        ++ActivationCnt;
        Assert.IsAssignableFrom<IStarterActivation>(starter);
        Assert.Equal(activationProps.Count+1, props.Count);//, "expected activationProps + StarterTempl props with RUN_PROPERTY_PREFIX...");
        Assert.Equal("xxx", props["activation.Prop"]);//, "bad activation.Prop");
        Assert.Equal(ACTIVATOR_RUN_PROP, props[ACTIVATOR_RUN_PROP]);//, "bad " + ACTIVATOR_RUN_PROP); //run prop w/o prefix
        runtimeStarter.DoActivate(props);    //must not activate in parallel
      };
      runtimeStarter.DoActivate(activationProps);
      Assert.Equal(1, ActivationCnt);//, "Starter must activate once");

      runtimeStarter.IsStarted= false;
      ActivationCnt= 0;
      runtimeStarter.Activate+= (starter, props) => ++ActivationCnt;
      runtimeStarter.InternalStarter.DoActivate(activationProps);
      Assert.Equal(2, ActivationCnt);//, "Starter must be activated twice");
    }

    [Fact]
    public void ConcurrentActivationStarterTest() {
      starterInitCnt= ActivationCnt= 0;
      var masterStarter= new MasterStarter("tstMasterStarter", "Test starter description", typeof(TestStarterImpl), masterProps);

      var starterRunProps= new Dictionary<string, object> {
        {"Inst.Prop", "xx"}
      };
      IRuntimeStarter runtimeStarter= (IRuntimeStarter)masterStarter.CreateRuntimeStarter("tstRuntimeStarter", masterStarter.Description, starterRunProps);

      var activationProps= new Dictionary<string, object> {
        {"activation.Prop", "xxx"},
        {MasterStarter.RPROP_PARALLEL_START, true}    //allow parallel activation
      };
      runtimeStarter.Enabled= true;

      runtimeStarter.Activate+= (a, props) => {
        if (++ActivationCnt <2)
          runtimeStarter.DoActivate(activationProps);    //do activate in parallel !
      };

      runtimeStarter.DoActivate(activationProps);
      Assert.Equal(2, ActivationCnt);//, "Starter must activate twice");
    }

    [Fact]
    public void JobStartTest() {
      var actComplete= new Tlabs.Sync.SyncMonitor<bool>();

      var masterStarter= new MasterStarter("0", "0", typeof(TestStarterImpl), null);
      IRuntimeStarter runtimeStarter= (IRuntimeStarter)masterStarter.CreateRuntimeStarter("tstRuntimeJobStarter", masterStarter.Description, null);
      var runtimeJob= new MasterJob("job", "job", typeof(TestJobImpl), masterProps).CreateRuntimeJob(runtimeStarter, "job", "job", null);
      Assert.Equal(0, this.jobInitCnt);
      Assert.Equal(0, this.jobDisposeCnt);

      runtimeStarter.ActivationComplete+= compl => {

        Assert.Equal("tstRuntimeJobStarter", compl.StarterName);
        Assert.Equal(1, this.jobInitCnt);
        Assert.Equal(1, this.jobRunCnt);
        Assert.Equal(1, this.jobDisposeCnt);
        Assert.NotEmpty(compl.JobResults);
        foreach(var res in compl.JobResults)
          Assert.True(res.IsSuccessful);
        ++this.starterCompletionCnt;

        actComplete.SignalPermanent(true);
      };

      runtimeStarter.DoActivate(runProps);
      actComplete.WaitForSignal();
      
      Assert.Equal(1, this.jobDisposeCnt);
      Assert.Equal(1, this.starterCompletionCnt);
    }
  }
}
