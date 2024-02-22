using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Moq;

using Tlabs.Msg;
using Tlabs.Misc;
using Tlabs.JobCntrl.Intern;
using Tlabs.JobCntrl.Model.Intern;

namespace Tlabs.JobCntrl.Test {

  public class StarterCmplMsgPubTest {
    IMessageBroker msgBroker;
    IServiceCollection svcColl;
    List<ServiceDescriptor> svcList= new();

    public StarterCmplMsgPubTest() {
      var msgBrokMock= new Mock<IMessageBroker>();
      msgBrokMock.Setup(m => m.Publish(It.IsAny<string>(), It.IsAny<object>()))
                 .Callback<string, object>((subj, obj) => {
                    Assert.Equal(StarterCompletionMsgPublisher.COMPLETION_SUBJECT, subj);
                 });
      this.msgBroker= msgBrokMock.Object;

      var svcCollMock= new Mock<IServiceCollection>();
      svcCollMock.Setup(s => s.Add(It.IsAny<ServiceDescriptor>()))
                 .Callback<ServiceDescriptor>((sd) => this.svcList.Add(sd));
      this.svcColl= svcCollMock.Object;
    }

    [Fact]
    public void BasicTest() {
      var cnt= 0;
      var cmpl= new Mock<IStarterCompletion> { DefaultValue= DefaultValue.Mock }.Object;
      var scmPublisher= new StarterCompletionMsgPublisher(this.msgBroker);
      scmPublisher.CompletionInfoPersisted+= (scp, sp, obj) => {
        Assert.NotNull(scp);
        Assert.Same(cmpl, sp);
        ++cnt;
      };
      scmPublisher.StoreCompletionInfo(cmpl);
      Assert.Equal(1, cnt);
    }

    [Fact]
    public void ConfigTest() {
      this.svcList.Clear();
      var cfg= new StarterCompletionMsgPublisher.Configurator();
      cfg.AddTo(this.svcColl, Singleton<Tlabs.Config.Empty>.Instance);
      Assert.NotEmpty(this.svcList);
    }

  }
}
