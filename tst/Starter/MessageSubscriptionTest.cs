using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;

using Tlabs.Test.Common;
using Tlabs.JobCntrl.Model.Intern;
using Tlabs.JobCntrl.Model.Intern.Starter;
using Tlabs.Msg;
using Tlabs.JobCntrl.Model;

namespace Tlabs.JobCntrl.Test {

  [Collection("AppTimeScope")]
  public class MessageSubscriptionTest {
    SvcProvEnvironment appTimeEnv;
    IMessageBroker msgBroker;
    IJobControl jobCntrlRuntime;
    RTTestStarter rtStarter;

    string subscriptionSubject;
    Action<AutomationJobMessage> subscriptionHandler;
    Func<AutomationJobMessage, Task<IStarterCompletion>> subRequestHandler;

    public MessageSubscriptionTest(SvcProvEnvironment appTimeEnv) {
      this.appTimeEnv= appTimeEnv;
      var brokerMock= new Mock<IMessageBroker>();
      brokerMock.Setup(b => b.Unsubscribe(It.IsAny<Delegate>()));
      brokerMock.Setup(b => b.Publish(It.IsAny<string>(), It.IsAny<object>()));
      brokerMock.Setup(b => b.Subscribe<AutomationJobMessage>(It.IsAny<string>(), It.IsAny<Action<AutomationJobMessage>>()))
                .Callback<string, Action<AutomationJobMessage>>((sub, action) => { this.subscriptionSubject= sub; this.subscriptionHandler= action; });
      brokerMock.Setup(b => b.SubscribeRequest<AutomationJobMessage, IStarterCompletion>(It.IsAny<string>(), It.IsAny<Func<AutomationJobMessage, Task<IStarterCompletion>>>()))
                .Callback<string, Func<AutomationJobMessage, Task<IStarterCompletion>>>((sub, func) => {
        this.subscriptionSubject= sub;
        this.subRequestHandler= func;
      });
      this.msgBroker= brokerMock.Object;

      var jcntrlMock= new Mock<IJobControl>();
      this.rtStarter= new RTTestStarter();
      rtStarter.Initialize("msgStarter", "test description", null);
      jcntrlMock.Setup(j => j.Starters).Returns(new Dictionary<string, IStarter> {[rtStarter.Name]= rtStarter});
      this.jobCntrlRuntime= jcntrlMock.Object;
    }

    [Fact]
    public void BasicTest() {
      using var msgStarter= new MessageSubscription(msgBroker);
      msgStarter.Initialize("msgStarter", "test description", null);
      Assert.Null(subscriptionSubject);
      Assert.Null(subRequestHandler);
      msgStarter.Enabled= true;
      Assert.Equal(msgStarter.Name, this.subscriptionSubject);
      Assert.NotNull(subscriptionHandler);
      Assert.Null(subRequestHandler);

      int actCnt= 0;
      Model.StarterActivator handler= (starter, props) => ++actCnt >0;
      msgStarter.Activate+= handler;
      Assert.True(msgStarter.DoActivate(null));
      Assert.Equal(1, actCnt);

      actCnt= 0;
      msgStarter.Enabled= false;
      Assert.False(msgStarter.DoActivate(null));
      Assert.Equal(0, actCnt);

      msgStarter.Enabled= true;
      msgStarter.Activate-= handler;    //no handler
      Assert.False(msgStarter.DoActivate(null));
      Assert.Equal(0, actCnt);
    }

    [Fact]
    public void UnbufferdTest() {
      using var msgStarter= new MessageSubscription(msgBroker);
      msgStarter.Initialize("msgStarter", "test description", new Dictionary<string, object> {
        [MessageSubscription.PROP_MSG_SUBJECT]= "test"
      });
      msgStarter.Enabled= true;
      Assert.Equal("test", this.subscriptionSubject);
      int actCnt= 0;
      msgStarter.Activate+= (starter, props) => ++actCnt > 0;
      subscriptionHandler(new AutomationJobMessage("tstSource"));
      subscriptionHandler(new AutomationJobMessage("tstSource"));
      Assert.Equal(2, actCnt);
    }

    [Fact]
    public async Task BufferdTest() {
      using var msgStarter= new MessageSubscription(msgBroker);
      msgStarter.Initialize("msgStarter", "test description", new Dictionary<string, object> {
        [MessageSubscription.PROP_MSG_SUBJECT]= "test",
        [MessageSubscription.PROP_BUFFER]= 50
      });
      msgStarter.Enabled= true;
      Assert.Equal("test", this.subscriptionSubject);
      int actCnt= 0;
      msgStarter.Activate+= (starter, props) => ++actCnt > 0;
      subscriptionHandler(new AutomationJobMessage("tstSource"));
      subscriptionHandler(new AutomationJobMessage("tstSource"));
      subscriptionHandler(new AutomationJobMessage("tstSource"));
      await Task.Delay(5);
      subscriptionHandler(new AutomationJobMessage("tstSource"));
      await Task.Delay(100);
      subscriptionHandler(new AutomationJobMessage("tstSource"));
      await Task.Delay(100);
      Assert.Equal(2, actCnt);
    }

    [Fact]
    public void ReturnResultTest() {
      using var msgStarter= new MessageSubscription(msgBroker);
      var jobProps= new Dictionary<string, object> {["msg"]= "tst-message" };
      msgStarter.Initialize("msgStarter", "test description", new Dictionary<string, object> {
        [MessageSubscription.PROP_RET_RESULT]= true,
        [MasterStarter.PROP_RUNTIME]= this.jobCntrlRuntime
      });
      Assert.Null(subscriptionSubject);
      Assert.Null(subscriptionHandler);
      Assert.Null(subRequestHandler);
      msgStarter.Enabled= true;
      Assert.Equal(msgStarter.Name, this.subscriptionSubject);
      Assert.Null(subscriptionHandler);
      Assert.NotNull(subRequestHandler);

      Assert.Empty(subRequestHandler(new AutomationJobMessage("tstSource", optionalProps: jobProps)).GetAwaiter().GetResult().JobResults);

      IStarterCompletion cmplRes= null;
      Model.StarterActivator handler= (starter, props) => {
        Assert.True(rtStarter.IsCompletionRegistered);
        rtStarter.Properties= props;
        cmplRes= new TestCompletionResult(rtStarter, new List<IJobResult> { new JobResult(rtStarter.Name, true)});
        rtStarter.AsyncCompletionWith(cmplRes);
        return true;
      };
      msgStarter.Activate+= handler;
      var res= subRequestHandler(new AutomationJobMessage("tstSource", optionalProps: jobProps)).GetAwaiter().GetResult(); //this blocks until AsyncCompletionWith() executes...
      Assert.Equal(cmplRes, res);
    }

    [Fact]
    public void TaskDisposeTest() {
      bool wasCanceled= false;

      var cts= new CancellationTokenSource();
      var tsk= Task.Delay(50, cts.Token);
      tsk.ContinueWith(t => {
        wasCanceled= t.IsCanceled;
      });
      Thread.Sleep(5);
      Assert.Throws<InvalidOperationException>(() => tsk.Dispose());
      cts.Cancel();
      cts.Dispose();
      Thread.Sleep(100);
      Assert.True(wasCanceled);
    }

  }
}
