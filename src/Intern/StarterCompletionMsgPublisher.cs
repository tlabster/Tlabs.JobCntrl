using System;
using System.Collections.Generic;

using Tlabs.Misc;
using Tlabs.Msg;
using Tlabs.Data.Serialize.Json;
using System.IO;
using System.Text;
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
  }
}