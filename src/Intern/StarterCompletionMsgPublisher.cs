using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

using Tlabs.Msg;
using Tlabs.Config;
using Tlabs.JobCntrl.Model.Intern;

namespace Tlabs.JobCntrl.Intern {

  /// <summary>Default <see cref="App.ContentRoot"/> relative persistence path.</summary>
  public class StarterCompletionMsgPublisher : IStarterCompletionPersister {
    IMessageBroker msgBroker;

    /// <summary>Published message subject.</summary>
    public const string COMPLETION_SUBJECT= "JobCntrl.StarterCompletion";
    ///<inherit/>
    public event Action<IStarterCompletionPersister, IStarterCompletion, object> CompletionInfoPersisted;

    /// <summary>Ctor from <paramref name="msgBroker"/>.</summary>
    public StarterCompletionMsgPublisher(IMessageBroker msgBroker) {
      this.msgBroker= msgBroker;
    }

    ///<inherit/>
    public Stream GetLastCompletionInfo(string starterName, out string contentType, out Encoding encoding) => throw new NotImplementedException();

    ///<inherit/>
    public void StoreCompletionInfo(IStarterCompletion starterCompletion) => msgBroker.Publish(COMPLETION_SUBJECT, starterCompletion);

    ///<summary>Service configurator.</summary>
    public class Configurator : IConfigurator<IServiceCollection> {
      ///<inherit/>
      public void AddTo(IServiceCollection svcColl, IConfiguration cfg) {
        svcColl.AddSingleton<IStarterCompletionPersister, StarterCompletionMsgPublisher>();
      }
    }

  }
}