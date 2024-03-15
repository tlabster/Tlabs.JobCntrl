using System;
using System.IO;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

using Tlabs.Msg;
using Tlabs.Config;
using Tlabs.JobCntrl.Model.Intern;

namespace Tlabs.JobCntrl.Intern {

  /// <summary>Default <see cref="App.ContentRoot"/> relative persistence path.</summary>
  public class StarterCompletionMsgPublisher : IStarterCompletionPersister {
    readonly IMessageBroker msgBroker;

    /// <summary>Published message subject.</summary>
    public const string COMPLETION_SUBJECT= "JobCntrl.StarterCompletion";
    ///<inheritdoc/>
    public event Action<IStarterCompletionPersister, IStarterCompletion, object?>? CompletionInfoPersisted;

    /// <summary>Ctor from <paramref name="msgBroker"/>.</summary>
    public StarterCompletionMsgPublisher(IMessageBroker msgBroker) {
      this.msgBroker= msgBroker;
    }

    ///<inheritdoc/>
    public Stream GetLastCompletionInfo(string starterName, out string contentType, out Encoding encoding) => throw new NotImplementedException();

    ///<inheritdoc/>
    public void StoreCompletionInfo(IStarterCompletion starterCompletion) {
      msgBroker.Publish(COMPLETION_SUBJECT, starterCompletion);
      CompletionInfoPersisted?.Invoke(this, starterCompletion, null);
    }

    ///<summary>Service configurator.</summary>
    public class Configurator : IConfigurator<IServiceCollection> {
      ///<inheritdoc/>
      public void AddTo(IServiceCollection svcColl, IConfiguration cfg) {
        svcColl.AddSingleton<IStarterCompletionPersister, StarterCompletionMsgPublisher>();
      }
    }

  }
}