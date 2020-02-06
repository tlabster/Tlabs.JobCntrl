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
    ///<remarks>If this property is set to <c>true</c> <see cref="BackgroundJobMessage"/> must be published with <see cref="IMessageBroker.PublishRequest{TRet}(string, object, int)"/>!!</remarks>
    public const string PROP_RET_RESULT= "Return-Result";

    private static readonly ILogger log= App.Logger<MessageSubscription>();

    private IMessageBroker msgBroker;
    private string subscriptionSubject;
    private int buffer;
    private bool reqForResult;
    private Task bufferTask;
    private CancellationTokenSource cancelSource;
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
          this.myRuntimeStarter= null;
          return enabled;
        }

        if (null != subscriptionHandler) return enabled; //allready enabled

        if (reqForResult) {
          this.myRuntimeStarter= getMyRuntimeStarter();
          Func<BackgroundJobMessage, IStarterCompletion> msgHandler= this.handleMessageCompletion;
          msgBroker.SubscribeRequest<BackgroundJobMessage, IStarterCompletion>(subscriptionSubject, msgHandler);
          subscriptionHandler= msgHandler;
        }
        else {
          Action<BackgroundJobMessage> msgHandler= this.messageHandler;
          msgBroker.Subscribe<BackgroundJobMessage>(subscriptionSubject, msgHandler);
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

    private void messageHandler(BackgroundJobMessage message) {
      if (0 == buffer) {
        doActivateWithMessage(message);
        return;
      }

      setCancelSource();
      bufferTask= Task.Delay(buffer, cancelSource.Token);
      bufferTask.ContinueWith((t, o) => {
        CancellationTokenSource cts= (CancellationTokenSource)o;
        t.Dispose();
        if (cts == Interlocked.CompareExchange<CancellationTokenSource>(ref cancelSource, null, cts)) 
          cts.Dispose();
        doActivateWithMessage(message);
      }, cancelSource, TaskContinuationOptions.NotOnCanceled);
    }

    private IStarterCompletion handleMessageCompletion(BackgroundJobMessage message) {
      var res= new StarterCompletionAwaiter(this, message).Result;
      if (log.IsEnabled(LogLevel.Debug)) log.LogDebug("Starter[{name}] completed with {cnt} result(s).", Name, res.JobResults.Count());
      return res;
    }

    private void setCancelSource() {
      CancellationTokenSource cts0, cts;
      if (null != (cts= Interlocked.CompareExchange<CancellationTokenSource>(ref cancelSource, cts0= new CancellationTokenSource(), null))) {
        try {
          cts.Cancel(); //could throw if already disposed from bufferTask.ContinueWith()...
          cts.Dispose();
          bufferTask.Dispose();
        } catch (Exception e) when (Misc.Safe.NoDisastrousCondition(e)) { }
        if (cts != Interlocked.CompareExchange<CancellationTokenSource>(ref cancelSource, cts0, cts))
          cts0.Dispose(); // let other win;
      }
    }

    private bool doActivateWithMessage(BackgroundJobMessage message) {
      log.LogDebug("Starter[{name}] activation from message source: '{source}'.", Name, message.Source);
      var activated= this.DoActivate(message.JobProperties);
      log.LogDebug("Job(s) activated from Starter[{name}]: '{activated}'.", Name, activated);
      return activated;
    }

    /// <summary>Dispose managed resources on <paramref name="disposing"/> == true.</summary>
    protected override void Dispose(bool disposing) {
      base.Dispose(disposing);
      if (null == msgBroker) return;
      ChangeEnabledState(false);
      msgBroker= null;
    }

    private class StarterCompletionAwaiter {
      readonly MessageSubscription starter;
      readonly SyncMonitor<IStarterCompletion> syncRes= new SyncMonitor<IStarterCompletion>();
      // Dictionary<string, object> runProps;
      StarterActivationCompleter complHandler;
      public StarterCompletionAwaiter(MessageSubscription starter, BackgroundJobMessage message) {
        this.starter= starter;
        starter.myRuntimeStarter.ActivationComplete+= (this.complHandler= this.complAwaiter);
        if (!starter.doActivateWithMessage(message)) {
          MessageSubscription.log.LogDebug("Returning empty result (w/o any async await) - because of no job activation(s).");
          complAwaiter(new EmptyComplResult(starter));
        }
      }

      public IStarterCompletion Result { get {
        if (!syncRes.IsSignaled) MessageSubscription.log.LogDebug("Starter[{name}] waiting for activated job(s) to complete.", starter.Name);
        return syncRes.WaitForSignal();
      }}

      private void complAwaiter(IStarterCompletion cmpl) {
        starter.myRuntimeStarter.ActivationComplete-= complHandler;
        if (null != cmpl) MessageSubscription.log.LogDebug("Signaling of pending starter[{name}] completion.", cmpl.StarterName);
        syncRes.SignalPermanent(cmpl);
      }
      public override string ToString() => "[" + nameof(StarterCompletionAwaiter) + "]";
    }

    class EmptyComplResult : IStarterCompletion {
      public EmptyComplResult(IStarter rtStarter) {
        this.StarterName= rtStarter.Name;
        this.Time= App.TimeInfo.Now;
        this.RunProperties= rtStarter.Properties;
      }
      public string StarterName { get; }
      public DateTime Time { get; }
      public IReadOnlyDictionary<string, object> RunProperties { get; }
      public IEnumerable<IJobResult> JobResults=> Enumerable.Empty<IJobResult>();
      public void Dispose() { }
    }

  }
}
