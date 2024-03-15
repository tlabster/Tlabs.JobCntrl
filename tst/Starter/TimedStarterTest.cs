using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Moq;

using Tlabs.Misc;
using Tlabs.Test.Common;
using Tlabs.JobCntrl.Model.Intern;
using Tlabs.JobCntrl.Model.Intern.Starter;
using Tlabs.JobCntrl.Model;

namespace Tlabs.JobCntrl.Test {

  [Collection("AppTimeScope")]
  public class TimedStarterTest {
    SvcProvEnvironment appTimeEnv;
    IJobControl jobCntrlRuntime;
    RTTestStarter rtStarter;

    public TimedStarterTest(SvcProvEnvironment appTimeEnv) {
      this.appTimeEnv= appTimeEnv;

      var jcntrlMock= new Mock<IJobControl>();
      this.rtStarter= new RTTestStarter();
      rtStarter.Initialize("rtTimedStarter", "test description", null);
      jcntrlMock.Setup(j => j.Starters).Returns(new Dictionary<string, IStarter> {[rtStarter.Name]= rtStarter});
      this.jobCntrlRuntime= jcntrlMock.Object;
    }

    [Fact]
    public void ThrowTest() {
      var tmStarter= new TimeSchedule();
      Assert.ThrowsAny<JobCntrlConfigException>(() => tmStarter.Initialize("timedStarter", "test description", new Dictionary<string, object> {
        [TimeSchedule.PARAM_SCHEDULE_TIME]= ""
      }));
      Assert.Throws<InvalidOperationException>(() => tmStarter.Enabled= true);
    }

    [Fact]
    public async Task BasicTest() {
      using var tmStarter= new TimeSchedule();
      tmStarter.Initialize("timedStarter", "test description", new Dictionary<string, object> {
        [TimeSchedule.PARAM_SCHEDULE_TIME]= "*-*-* *:*:*"
      });
      var tcs= new TaskCompletionSource();
      var actCnt= 0;
      tmStarter.Activate+= (starter, props)=> {
        ++actCnt;
        tmStarter.Enabled= false;
        tcs.TrySetResult();
        return false;
      };
      tmStarter.Enabled= true;
      await tcs.Task.Timeout(2000);
      Assert.Equal(1, actCnt);
    }

  }

  public class RTTestStarter : IRuntimeStarter {
    public bool IsStarted { get; set; }
    public bool Enabled { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public IReadOnlyDictionary<string, object> Properties { get; set; }
    public IStarter InternalStarter => throw new NotImplementedException();
    public event StarterActivationCompleter ActivationComplete;
    public event StarterActivationMonitor ActivationTriggered;
    public event StarterActivationCompleter ActivationFinalized;

    public event StarterActivator Activate {
      add => throw new NotImplementedException();
      remove => throw new NotImplementedException();
    }
    public void Dispose() { }
    public bool DoActivate(IReadOnlyDictionary<string, object> activationProps) {
      ActivationTriggered?.Invoke(null);
      ActivationFinalized?.Invoke(null);
      throw new NotImplementedException();
    }
    public IStarter Initialize(string name, string description, IReadOnlyDictionary<string, object> properties) {
      this.Name= name;
      this.Description= description;
      this.Properties= properties;
      return this;
    }
    public bool IsCompletionRegistered => null != ActivationComplete;
    public void AsyncCompletionWith(IStarterCompletion cmpl) {
      Task.Delay(50)
          .ContinueWith((t, o) => {
            ActivationComplete(cmpl);
          }, null);
    }
  }

  public class TestCompletionResult : IStarterCompletion {
    public TestCompletionResult(IStarter rtStarter, IEnumerable<IJobResult> results) {
      this.StarterName= rtStarter.Name;
      this.Time= DateTime.Now;
      this.RunProperties= rtStarter.Properties;
      this.JobResults= results;
    }
    public string StarterName { get; }
    public DateTime Time { get; }
    public IReadOnlyDictionary<string, object> RunProperties { get; }
    public IEnumerable<IJobResult> JobResults { get; }
    public void Dispose() { }
  }

}
