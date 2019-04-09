using System;
using System.Collections.Generic;
using System.Threading;
using Xunit;
using Moq;

using Tlabs.Test.Common;
using Tlabs.JobCntrl.Model.Intern.Starter;
using Tlabs.Msg;

namespace Tlabs.JobCntrl.Test {

  [Collection("AppTimeScope")]
  public class MessageSubscriptionTest {
    private AppTimeEnvironment appTimeEnv;
    private IMessageBroker msgBroker;
    
    private string subscriptionSubject;
    private Action<BackgroundJobMessage> subscriptionHandler;

    public MessageSubscriptionTest(AppTimeEnvironment appTimeEnv) {
      this.appTimeEnv= appTimeEnv;
      var brokerMock= new Mock<IMessageBroker>();
      brokerMock.Setup(b => b.Unsubscribe(It.IsAny<Delegate>()));
      brokerMock.Setup(b => b.Publish(It.IsAny<string>(), It.IsAny<object>()));
      brokerMock.Setup(b => b.Subscribe<BackgroundJobMessage>(It.IsAny<string>(), It.IsAny<Action<BackgroundJobMessage>>()))
                .Callback<string, Action<BackgroundJobMessage>>((sub, action) => {this.subscriptionSubject= sub; this.subscriptionHandler= action;});
      this.msgBroker= brokerMock.Object;
    }

    [Fact]
    public void BasicMessageStarterTest() {
      var msgStarter= new MessageSubscription(msgBroker);
      msgStarter.Initialize("msgStarter", "test description", null);
      Assert.Null(subscriptionSubject);
      msgStarter.Enabled= true;
      Assert.Equal("--undefined--", this.subscriptionSubject);
      Assert.NotNull(subscriptionHandler);

      int actCnt= 0;
      msgStarter.Activate+= (starter, props) => {
        ++actCnt;
      };
      msgStarter.DoActivate(null);
      Assert.Equal(1, actCnt);
    }

    [Fact]
    public void MessageStarterUnbufferdTest() {
      var msgStarter= new MessageSubscription(msgBroker);
      msgStarter.Initialize("msgStarter", "test description", new Dictionary<string, object> {
        [MessageSubscription.PROP_MSG_SUBJECT]= "test"
      });
      msgStarter.Enabled= true;
      Assert.Equal("test", this.subscriptionSubject);
      int actCnt= 0;
      msgStarter.Activate+= (starter, props) => {
        ++actCnt;
      };
      subscriptionHandler(new BackgroundJobMessage { Source= "tstSource" });
      subscriptionHandler(new BackgroundJobMessage { Source= "tstSource" });
      Assert.Equal(2, actCnt);
    }

    [Fact]
    public void MessageStarterBufferdTest() {
      var msgStarter= new MessageSubscription(msgBroker);
      msgStarter.Initialize("msgStarter", "test description", new Dictionary<string, object> {
        [MessageSubscription.PROP_MSG_SUBJECT]= "test",
        [MessageSubscription.PROP_BUFFER]= 30
      });
      msgStarter.Enabled= true;
      Assert.Equal("test", this.subscriptionSubject);
      int actCnt= 0;
      msgStarter.Activate+= (starter, props) => {
        ++actCnt;
      };
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
  }
}
