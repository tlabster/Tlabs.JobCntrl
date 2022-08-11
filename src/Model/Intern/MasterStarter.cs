using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Tlabs.Diagnostic;

namespace Tlabs.JobCntrl.Model.Intern {
  using IProps = IReadOnlyDictionary<string, object>;

  ///<summary>Delegate to be invoked on completion of a starter activation.</summary>
  public delegate void StarterActivationCompleter(IStarterCompletion completion);

  ///<summary>Delegate to be invoked before starter activation.</summary>
  ///<remarks>Starter activation is canceled if <see cref="IStarterActivationRequest.Canceled"/>.</remarks>
  public delegate void StarterActivationMonitor(IStarterActivationRequest activationReq);

  ///<summary>Interface of a runtime-starter instance.</summary>
  public interface IRuntimeStarter : IStarter {
    ///<summary>Event on (before) starter activation.</summary>
    ///<remarks>Only fired if there are actual job(s) started. </remarks>
    event StarterActivationMonitor ActivationTriggered;

    ///<summary>Event on completion of a starter activation.</summary>
    ///<remarks>Only fired if there are actual job(s) started. </remarks>
    event StarterActivationCompleter ActivationComplete;

    ///<summary>Event after finished completion of a starter activation.</summary>
    ///<remarks>Only fired if there are actual job(s) started. </remarks>
    event StarterActivationCompleter ActivationFinalized;

    ///<summary>Internal starter instance.</summary>
    IStarter InternalStarter { get; }

    ///<summary>True when starter activated.</summary>
    bool IsStarted { get; set; }
  }

  /// <summary>A <see cref="IStarter"/>'s activation information.</summary>
  public  interface IStarterActivation : IStarter {
    /// <summary>Register a <see cref="IJobResult"/>.</summary>
    void AddResult(IJobResult jobResult);
  }

  /// <summary>A <see cref="IStarter"/>'s activation request.</summary>
  public interface IStarterActivationRequest : IStarter {
    /// <summary>Activation canceled.</summary>
    bool Canceled { get; set; }
  }

  /// <summary>A <see cref="IStarter"/>'s completion information.</summary>
  public interface IStarterCompletion : IDisposable {
    /// <summary><see cref="IStarter"/>'s name.</summary>
    string StarterName { get; }
    /// <summary>time of completion.</summary>
    DateTime Time { get; }
    /// <summary>Properties passed to <see cref="IJob.Run(IReadOnlyDictionary{string, object})"/> on job invocation.</summary>
    IProps RunProperties { get; }
    /// <summary>Job results.</summary>
    IEnumerable<IJobResult> JobResults { get; }
  }

  /// <summary>A <see cref="IStarter"/> master class.</summary>
  /// <remarks>Instances of this class are factory classes for actual <see cref="IStarter"/> implementations.</remarks>
  public class MasterStarter : MasterBase<IStarter> {
    internal static readonly ILogger Log= App.Logger<IStarter>();

    /// <summary>internal runtime property</summary>
    public const string PROP_RUNTIME= "$Runtime-Obj";
    /// <summary>If this run-property is set (true on DoAcivate), allow for parallel Starter invocation</summary>
    public const string RPROP_PARALLEL_START= "Parallel-Start";
    /// <summary>Ctor.</summary>
    /// <param name="name">unique master starter name</param>
    /// <param name="description">description string</param>
    /// <param name="starterType">Target <see cref="IStarter"/> instance type</param>
    /// <param name="properties">Master starter configuration parameters</param>
    public MasterStarter(string name, string description, Type starterType, IProps properties) : base(name, description, starterType, properties) { }

    /// <summary>Create a <see cref="IStarter"/> runtime instance.</summary>
    /// <param name="name">Starter's runtime name</param>
    /// <param name="description">Description</param>
    /// <param name="properties">Runtime configuration parameters</param>
    /// <returns></returns>
    public IRuntimeStarter CreateRuntimeStarter(string name, string description, IProps properties) {
      RuntimeStarter starter= null;
      try {
        starter= new RuntimeStarter(this);
        var ret= (IRuntimeStarter)starter.Initialize(name, description, properties);
        if (object.ReferenceEquals(starter, ret))
          starter= null;
        return ret;
      }
      finally { starter?.Dispose(); }
    }

    /// <summary>dispose when <param name="disposing"/> == true.</summary>
    protected override void Dispose(bool disposing) { }


    /// <summary>Internal delegation proxy to an actual IStarter implementation instance.</summary>
    /// <remarks>
    /// <para>Constructs an actual IStarter implementation from the meta-data of a MasterStarter and
    /// acts as a delegator to that Starter.</para>
    /// <para>Arranges to collect all the <see cref="IJobResult"/>(s) from the started jobs and signals the
    /// <see cref="IRuntimeStarter.ActivationComplete"/> event, when all started jobs have completed.</para>
    /// </remarks>
    sealed class RuntimeStarter : IRuntimeStarter {
      readonly MasterStarter masterStarter;
      private IStarter targetStarter;
      private StarterActivator targetStartHandler;
      private bool concurrentStart;
      private StarterCompletion pendingCompl;
      readonly object sync= new();
      private event StarterActivationCompleter internalStarterFinished;
      readonly RuntimeProxy runtimeProx= new();

      internal RuntimeStarter(MasterStarter masterStarter) {
        if (null == (this.masterStarter= masterStarter)) throw new ArgumentNullException(nameof(masterStarter));
      }

      public event StarterActivator Activate;
      public event StarterActivationCompleter ActivationComplete;
      public event StarterActivationMonitor ActivationTriggered;
      public event StarterActivationCompleter ActivationFinalized {
        add {
          internalStarterFinished+= value;
          if (value.Target is IJobControl runtime)
            runtimeProx.rt= runtime;
        }
        remove { internalStarterFinished-= value; }
      }

      public string Name { get { return targetStarter.Name; } }

      public string Description { get { return targetStarter.Name; } }

      public IProps Properties { get { return targetStarter.Properties; } }

      public IStarter InternalStarter { get { return targetStarter; } }

      public bool Enabled {
        get { return targetStarter.Enabled; }
        set { targetStarter.Enabled= value; }
      }

      public IStarter Initialize(string name, string description, IProps properties) {
        /* Create a targetStarter instance from masterStarter:
         */
        targetStarter= (IStarter)Tlabs.App.CreateResolvedInstance(this.masterStarter.targetType);
        var props= new ConfigProperties(masterStarter.Properties, properties){ [PROP_RUNTIME]= runtimeProx };
        this.concurrentStart= ConfigProperties.GetBool(props, MasterStarter.RPROP_PARALLEL_START, false);
        targetStarter= targetStarter.Initialize(name, description, props.AsReadOnly());


        /* In place of actual job(s) this runtime starter registers for the targetStarter's Activate event,
         * so the original targetStarter activation gets handled by DoActivate, which delegates the start
         * to the job(s) and arranges to collect the jobs' results and fires the StartComplete event.
         */
        this.targetStartHandler= (starter, runProps) => DoActivate(runProps);
        targetStarter.Activate+= this.targetStartHandler;

        return this;
      }

      public bool DoActivate(IProps invocationProps) {
        if (null == targetStarter) throw new InvalidOperationException("Already disposed.");
        var start= this.Activate;
        if (null == start) {
          Log.LogDebug("No manual runtimeStarter[{ST}] activation with no registered job(s).", Name);
          return false;
        }

        CopyBaseConfigStarterProps(ref invocationProps);

        StarterCompletion compl;
        lock (sync) {
          if (!concurrentStart && null != pendingCompl) {
            /* Already pending start invocation.
             * Do not invoke until finished.
             */
            Log.LogInformation("Completion for Starter[{ST}] pending - cannot activate.", Name);
            return false;
          }
          compl= pendingCompl= new StarterCompletion(this, invocationProps);
        }

        if (activationCancled(new StarterActivationRequest(targetStarter, invocationProps))) {
          Log.LogInformation("Starter[{ST}] activation canceled.", Name);
          return false;
        }

        Log.LogInformation("Activating Starter[{ST}]", Name);
        return start(compl, invocationProps);
      }

      private bool activationCancled(StarterActivationRequest actRequest) {
        ActivationTriggered?.Invoke(actRequest);
        return actRequest.Canceled;
      }

      /* Copy Starter config. properties with RUN_PROPERTY_PREFIX to the given runProps:
       */
      private void CopyBaseConfigStarterProps(ref IProps runProps) {
        var props= ConfigProperties.Writeable(runProps);
        foreach (var pair in Properties) {
          if (pair.Key.StartsWith(BaseStarter.RUN_PROPERTY_PREFIX, StringComparison.OrdinalIgnoreCase))
            props[pair.Key.Substring(BaseStarter.RUN_PROPERTY_PREFIX.Length)]= pair.Value;
        }
        runProps= props.AsReadOnly();
      }

      private void RegisterJobResult(IStarterCompletion compl, IJobResult jobResult) {

        lock (sync) {
          var jobStartResults= (ISet<IJobResult>)compl.JobResults;
          jobStartResults.Add(jobResult);
          var start= this.Activate;
          if (   null == start
              || jobStartResults.Count != start.GetInvocationList().Length) return; // jobs no all completed
          pendingCompl= null;
        }
        Log.LogInformation("Starter '{ST}' activation completed.", Name);
        //fire events with released lock:
        ActivationComplete?.Invoke(compl);
        internalStarterFinished?.Invoke(compl);   //fire finished after possible chained activations
        compl.Dispose();
      }

      public bool IsStarted {
        get { lock (sync) return null != pendingCompl; }
        set {
          if (value) throw new InvalidOperationException("Must call DoActivate()");
          lock (sync) pendingCompl= null;
        }
      }

      public void Dispose() {
        IsStarted= false;
        var trig= targetStarter;
        if (null != trig) {
          trig.Activate-= this.targetStartHandler;
          trig.Dispose();
        }
        runtimeProx.Dispose();
#pragma warning disable //help gc
        targetStarter= trig= null;
#pragma warning restore
      }

      class RuntimeProxy : IJobControl {
        public IJobControl rt;

        private IJobControl jobCntrl {
          get {
            if (null == rt) throw new ObjectDisposedException("RuntimeProxy");
            return rt;
          }
        }

        public IJobControlCfgLoader ConfigLoader => jobCntrl.ConfigLoader;

        public IStarterCompletionPersister CompletionPersister => jobCntrl.CompletionPersister;

        public void Init() => jobCntrl.Init();
        
        public void Start() => jobCntrl.Start();

        public void Stop() => jobCntrl.Stop();

        public IMasterCfg MasterModels => jobCntrl.MasterModels;

        public IReadOnlyDictionary<string, IStarter> Starters => jobCntrl.Starters;

        public IReadOnlyDictionary<string, IJob> Jobs => jobCntrl.Jobs;

        public int CurrentJobActivationCount => jobCntrl.CurrentJobActivationCount;

        public Task FullCompletion => jobCntrl.FullCompletion;

        public void Dispose() { rt= null; }
      }

      class StarterActivationRequest : IStarterActivationRequest {
        readonly IStarter starter;
        readonly IProps runProps;

        public StarterActivationRequest(IStarter starter, IProps runProps) {
          this.starter= starter;
          this.runProps= runProps;
        }

        public bool Canceled { get; set; }

        bool IStarter.Enabled {
          get => true;
          set { throw new InvalidOperationException(); }
        }

        string IModel.Name => starter.Name;

        string IModel.Description => starter.Description;

        IProps IModel.Properties => runProps;

        public event StarterActivator Activate {
          add => throw new InvalidOperationException("no activation");
          remove => throw new InvalidOperationException("no activation");
        }

        public void Dispose() { }

        public bool DoActivate(IProps activationProps) => throw new InvalidOperationException("already activated");

        public IStarter Initialize(string name, string description, IProps properties) => throw new InvalidOperationException("already initalized");
      }

      class StarterCompletion : IStarterCompletion, IStarterActivation {
        readonly RuntimeStarter starter;
        readonly DateTime time= App.TimeInfo.Now;
        readonly IProps runProps;
        readonly HashSet<IJobResult> opResults= new HashSet<IJobResult>(JobResult.EqComparer);

        public StarterCompletion(RuntimeStarter starter, IProps runProps) {
          this.starter= starter;
          this.runProps= runProps;
          startActivity(runProps);
        }

        #region IStarterCompletion
        public string StarterName => starter.Name;

        public DateTime Time => time;

        public IProps RunProperties => runProps;

        public IEnumerable<IJobResult> JobResults => opResults;

        public override string ToString() {
          var sb= new System.Text.StringBuilder("starter-compl[", 128);
          sb.Append(StarterName).Append("]: ");
          var first= true;
          foreach (var res in opResults) {
            if (!first) sb.Append(", ");
            first= false;
            sb.Append(res.JobName).Append('[').Append(res.IsSuccessful ? "OK" : "ERROR").Append(']');
          }
          return sb.ToString();
        }
        #endregion

        void IStarterActivation.AddResult(IJobResult operationResult) {
          diagSrc.LogEvent("JobResult", operationResult);
          starter.RegisterJobResult(this, operationResult);
        }

        event StarterActivator IStarter.Activate {
          add { throw new NotImplementedException("IStarter.Invoke"); }
          remove { throw new NotImplementedException("IStarter.Invoke"); }
        }

        bool IStarter.Enabled {
          get { return true; }
          set { throw new InvalidOperationException(); }
        }

        IStarter IStarter.Initialize(string name, string description, IProps properties) => throw new InvalidOperationException();

        bool IStarter.DoActivate(IProps starterProps) => throw new InvalidOperationException();

        string IModel.Name => starter.Name;

        string IModel.Description => starter.Description;

        IProps IModel.Properties => runProps;

        void IDisposable.Dispose() => stopActivity();

        #region Diagnostic instrumentation
        private static readonly DiagnosticListener diagSrc= new DiagnosticListener(DiagListenerName.STARTER);
        private Activity diagActivity;

        void startActivity(IProps runProps) {
          if (diagSrc.IsEnabled()) {
            diagActivity= new Activity("JobCntrl.Starter");
            diagActivity.AddTag("masterStarter.targetType", starter.targetStarter.GetType().FullName);
            diagActivity.AddPropTags(runProps);
            diagSrc.StartActivity(diagActivity, this);
          }
        }
        void stopActivity() {
          if (null != diagActivity)
            diagSrc.StopActivity(diagActivity, this);
          diagActivity= null;
        }
        #endregion
      }// class StarterCompletion

    }//class StarterInstance

  }//class StarterMaster

}
