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
    RTStarter rtStarter;
    
    string subscriptionSubject;
    Action<BackgroundJobMessage> subscriptionHandler;
    Func<BackgroundJobMessage, Task<IStarterCompletion>> subRequestHandler;

    public MessageSubscriptionTest(SvcProvEnvironment appTimeEnv) {
      this.appTimeEnv= appTimeEnv;
      var brokerMock= new Mock<IMessageBroker>();
      brokerMock.Setup(b => b.Unsubscribe(It.IsAny<Delegate>()));
      brokerMock.Setup(b => b.Publish(It.IsAny<string>(), It.IsAny<object>()));
      brokerMock.Setup(b => b.Subscribe<BackgroundJobMessage>(It.IsAny<string>(), It.IsAny<Action<BackgroundJobMessage>>()))
                .Callback<string, Action<BackgroundJobMessage>>((sub, action) => { this.subscriptionSubject= sub; this.subscriptionHandler= action; });
      brokerMock.Setup(b => b.SubscribeRequest<BackgroundJobMessage, IStarterCompletion>(It.IsAny<string>(), It.IsAny<Func<BackgroundJobMessage, Task<IStarterCompletion>>>()))
                .Callback<string, Func<BackgroundJobMessage, Task<IStarterCompletion>>>((sub, func) => {
        this.subscriptionSubject= sub;
        this.subRequestHandler= func;
      });
      this.msgBroker= brokerMock.Object;

      var jcntrlMock= new Mock<IJobControl>();
      this.rtStarter= new RTStarter();
      rtStarter.Initialize("msgStarter", "test description", null);
      jcntrlMock.Setup(j => j.Starters).Returns(new Dictionary<string, IStarter> {[rtStarter.Name]= rtStarter});
      this.jobCntrlRuntime= jcntrlMock.Object;
    }

    [Fact]
    public void BasicTest() {
      var msgStarter= new MessageSubscription(msgBroker);
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
      var msgStarter= new MessageSubscription(msgBroker);
      msgStarter.Initialize("msgStarter", "test description", new Dictionary<string, object> {
        [MessageSubscription.PROP_MSG_SUBJECT]= "test"
      });
      msgStarter.Enabled= true;
      Assert.Equal("test", this.subscriptionSubject);
      int actCnt= 0;
      msgStarter.Activate+= (starter, props) => ++actCnt > 0;
      subscriptionHandler(new BackgroundJobMessage { Source= "tstSource" });
      subscriptionHandler(new BackgroundJobMessage { Source= "tstSource" });
      Assert.Equal(2, actCnt);
    }

    [Fact]
    public void BufferdTest() {
      var msgStarter= new MessageSubscription(msgBroker);
      msgStarter.Initialize("msgStarter", "test description", new Dictionary<string, object> {
        [MessageSubscription.PROP_MSG_SUBJECT]= "test",
        [MessageSubscription.PROP_BUFFER]= 30
      });
      msgStarter.Enabled= true;
      Assert.Equal("test", this.subscriptionSubject);
      int actCnt= 0;
      msgStarter.Activate+= (starter, props) => ++actCnt > 0;
      subscriptionHandler(new BackgroundJobMessage { Source= "tstSource" });
      subscriptionHandler(new BackgroundJobMessage { Source= "tstSource" });
      subscriptionHandler(new BackgroundJobMessage { Source= "tstSource" });
      Thread.Sleep(5);
      subscriptionHandler(new BackgroundJobMessage { Source= "tstSource" });
      Thread.Sleep(50);
      subscriptionHandler(new BackgroundJobMessage { Source= "tstSource" });
      Thread.Sleep(50);
      Assert.Equal(2, actCnt);
    }

    [Fact]
    public void ReturnResultTest() {
      var msgStarter= new MessageSubscription(msgBroker);
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

      Assert.Empty(subRequestHandler(new BackgroundJobMessage { Source= "tstSource", JobProperties= jobProps }).GetAwaiter().GetResult().JobResults);

      IStarterCompletion cmplRes= null;
      Model.StarterActivator handler= (starter, props) => {
        Assert.True(rtStarter.IsCompletionRegistered);
        rtStarter.Properties= props;
        cmplRes= new CompletionResult(rtStarter, new List<IJobResult> { new JobResult(rtStarter.Name, true)});
        rtStarter.AsyncCompletionWith(cmplRes);
        return true;
      };
      msgStarter.Activate+= handler;
      var res= subRequestHandler(new BackgroundJobMessage { Source= "tstSource", JobProperties= jobProps }).GetAwaiter().GetResult(); //this blocks until AsyncCompletionWith() executes...
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

    class RTStarter : IRuntimeStarter {
      public bool IsStarted { get; set; }
      public bool Enabled { get; set; }
      public string Name { get; set; }
      public string Description { get; set; }
      public IReadOnlyDictionary<string, object> Properties { get; set; }
      public IStarter InternalStarter => throw new NotImplementedException();
      public event StarterActivationCompleter ActivationComplete;
      public event StarterActivator Activate {
        add => throw new NotImplementedException();
        remove => throw new NotImplementedException();
      }
      public void Dispose() { }
      public bool DoActivate(IReadOnlyDictionary<string, object> activationProps) {
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

    class CompletionResult : IStarterCompletion {
      public CompletionResult(IStarter rtStarter, IEnumerable<IJobResult> results) {
        this.StarterName= rtStarter.Name;
        this.Time= DateTime.Now;
        this.RunProperties= rtStarter.Properties;
        this.JobResults= results;
      }
      public string StarterName { get; }
      public DateTime Time { get; }
      public IReadOnlyDictionary<string, object> RunProperties { get; }
      public IEnumerable<IJobResult> JobResults { get; }
      public void Dispose() {}
    }
  }
}
