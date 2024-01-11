using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using Tlabs.Msg;
using Tlabs.Sync;

namespace Tlabs.JobCntrl.Model.Intern.Starter {

  /// <summary>Starter that activates on published messages.</summary>
  /// <remarks>
  /// <para>The starter subscribes on a message subject specified by the <c>PROP_MSG_SUBJECT</c> config. property.
  /// The published message gets passed as run-properties.</para>
  /// <para>If a buffer time (millisec.) was specified, multiple messages published within that buffer time are buffered into one single activation.</para>
  /// </remarks>
  public class MessageSubscription : BaseStarter {
    /// <summary>Name of a property that specifies the message subject.</summary>
    public const string PROP_MSG_SUBJECT= "Message-Subject";
    /// <summary>Name of a property that specifies a optional buffer time in miliseconds.</summary>
    public const string PROP_BUFFER= "Buffer-Ms";
    /// <summary>Name of a property that enables returning a result.</summary>
    ///<remarks>If this property is set to <c>true</c> <see cref="AutomationJobMessage"/> must be published with <see cref="IMessageBroker.PublishRequest{TRet}(string, object, int)"/>!!</remarks>
    public const string PROP_RET_RESULT= "Return-Result";

    private static readonly ILogger log= App.Logger<MessageSubscription>();

    readonly BufferedActionRunner bufferedAction= new();
    private IMessageBroker msgBroker;
    private string subscriptionSubject;
    private int buffer;
    private bool reqForResult;
    private Delegate subscriptionHandler;
    private IRuntimeStarter myRuntimeStarter;

    /// <summary>Ctor from <paramref name="msgBroker"/>.</summary>
    public MessageSubscription(IMessageBroker msgBroker) {
      this.msgBroker= msgBroker;
    }

    /// <summary>Internal starter initialization.</summary>
    protected override IStarter InternalInit() {
      this.subscriptionSubject= PropertyString(PROP_MSG_SUBJECT, Name); //subject name from property or starter name
      log.LogDebug("Message subscription starter[{name}] for subject '{sbj}' initialzed.", Name, subscriptionSubject);
      if (0 != (this.buffer= PropertyInt(PROP_BUFFER, 0)))
        log.LogDebug("Message buffer '{buffer}'ms.", this.buffer);
      if (true == (this.reqForResult= PropertyBool(PROP_RET_RESULT, false)))
        log.LogDebug("Starter[{name}] configured to return job results.", Name);
      ChangeEnabledState(this.isEnabled);
      return this;
    }

    /// <summary>Changes the enabled state of the starter according to <paramref name="enabled"/>.</summary>
    protected override void ChangeEnabledState(bool enabled) {
      enabled= updateEnabledState(enabled);
      log.LogDebug(nameof(MessageSubscription) + " starter[{name}] enabled = {state}.", Name, enabled);
    }

    private bool updateEnabledState(bool enabled) {
      lock (msgBroker) {
        if (false == (this.isEnabled= enabled)) {
          /* Disable starter:
           */
          msgBroker.Unsubscribe(subscriptionHandler);
          subscriptionHandler= null;
          myRuntimeStarter?.Dispose();
          myRuntimeStarter= null;
          return enabled;
        }

        if (null != subscriptionHandler) return enabled; //allready enabled

        if (reqForResult) {
          this.myRuntimeStarter= getMyRuntimeStarter();
          Func<AutomationJobMessage, Task<IStarterCompletion>> msgHandler= this.handleAsyncMessageCompletion;
          msgBroker.SubscribeRequest<AutomationJobMessage, IStarterCompletion>(subscriptionSubject, msgHandler);
          subscriptionHandler= msgHandler;
        }
        else {
          Action<AutomationJobMessage> msgHandler= this.messageHandler;
          msgBroker.Subscribe<AutomationJobMessage>(subscriptionSubject, msgHandler);
          subscriptionHandler= msgHandler;
        }
        return enabled;
      }
    }

    private IRuntimeStarter getMyRuntimeStarter() {
      IRuntimeStarter rtStarter;
      var runtime= Properties[MasterStarter.PROP_RUNTIME] as IJobControl;
      IStarter starter= null;
      if (   !(bool)runtime?.Starters.TryGetValue(Name, out starter)
          || null== (rtStarter= starter as IRuntimeStarter)) throw new InvalidOperationException($"No RuntimeStarter: {Name}");
      return rtStarter;
    }

    private void messageHandler(AutomationJobMessage message) => bufferedAction.Run(buffer, () => doActivateWithMessage(message));

    private Task<IStarterCompletion> handleAsyncMessageCompletion(AutomationJobMessage message) {
      var complSrc= new TaskCompletionSource<IStarterCompletion>();
      void complHandler(IStarterCompletion completion) {
        if (!isMatchingCompletion(message, completion)) return;

        if (log.IsEnabled(LogLevel.Debug)) log.LogDebug("Starter[{name}] completed with {cnt} result(s).", Name, completion.JobResults.Count());
        myRuntimeStarter.ActivationComplete-= complHandler;
        complSrc.TrySetResult(completion);
      }

      myRuntimeStarter.ActivationComplete+= complHandler;
      if (!doActivateWithMessage(message)) {
        log.LogDebug("Returning empty result (w/o any async await) - because of no job activation(s).");
        complHandler(new EmptyComplResult(this, message));
      }

      return complSrc.Task;
    }

    static bool isMatchingCompletion(AutomationJobMessage message, IStarterCompletion cmpl) {
      foreach (var pair in cmpl.RunProperties) {
        if (!message.JobProperties.TryGetValue(pair.Key, out var pval) || pval != pair.Value) return false;
      }
      return true;
    }

    private bool doActivateWithMessage(AutomationJobMessage message) {
      log.LogDebug("Starter[{name}] activation from message source: '{source}'.", Name, message.Source);
      var activated= this.DoActivate(message.JobProperties);
      log.LogDebug("Job(s) activated from Starter[{name}]: '{activated}'.", Name, activated);
      return activated;
    }

    /// <summary>Dispose managed resources on <paramref name="disposing"/> == true.</summary>
    protected override void Dispose(bool disposing) {
      if (!disposing || null == msgBroker) return;
      bufferedAction.Dispose();
      myRuntimeStarter?.Dispose();
      ChangeEnabledState(false);
      msgBroker= null;
      base.Dispose(disposing);
    }


    class EmptyComplResult : IStarterCompletion {
      public EmptyComplResult(IStarter rtStarter, AutomationJobMessage message) {
        this.StarterName= rtStarter.Name;
        this.Time= App.TimeInfo.Now;
        this.RunProperties= message.JobProperties;
      }
      public string StarterName { get; }
      public DateTime Time { get; }
      public IReadOnlyDictionary<string, object> RunProperties { get; }
      public IEnumerable<IJobResult> JobResults=> Enumerable.Empty<IJobResult>();
      public void Dispose() { }
    }

  }
}
