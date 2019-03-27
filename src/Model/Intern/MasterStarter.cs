using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Tlabs.JobCntrl.Model.Intern {
  using IProps = IReadOnlyDictionary<string, object>;

  ///<summary>Delegate to be invoked on completion of a starter activation.</summary>
  public delegate void StarterActivationCompleter(IStarterCompletion completion);

  ///<summary>Interface of a runtime-starter instance.</summary>
  public interface IRuntimeStarter : IStarter {
    ///<summary>Event on completion of a starter activation.</summary>
    event StarterActivationCompleter ActivationComplete;

    ///<summary>Internal starter instance.</summary>
    IStarter InternalStarter { get; }

    ///<summary>True when starter activated.</summary>
    bool IsStarted { get; set; }
  }

  internal interface IStarterActivation : IStarter {
    void AddResult(IJobResult jobResult);
  }

  /// <summary>A <see cref="IStarter"/>'s completion information.</summary>
  public interface IStarterCompletion {
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
    /// <param name="operationType">Target <see cref="IStarter"/> instance type</param>
    /// <param name="properties">Master starter configuration parameters</param>
    public MasterStarter(string name, string description, Type operationType, IProps properties)
      : base(name, description, operationType, properties) { }

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
      private MasterStarter masterStarter;
      private IStarter targetStarter;
      private StarterActivator targetStartHandler;
      private StarterCompletion pendingCompl;
      private object sync= new object();
      private event StarterActivationCompleter internalStartComplete;
      private RuntimeProxy runtimeProx= new RuntimeProxy();

      internal RuntimeStarter(MasterStarter masterStarter) {
        this.masterStarter= masterStarter;
      }

      public event StarterActivator Activate;

      public event StarterActivationCompleter ActivationComplete {
        add {
          internalStartComplete+= value;
          var runtime= value.Target as IJobControl;
          if (null != runtime)
            runtimeProx.rt= runtime;
        }
        remove { internalStartComplete-= value; }
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
        targetStarter= (IStarter)this.masterStarter.typeCtor.Invoke(null);
        var props= new ConfigProperties(masterStarter.Properties, properties);
        props[PROP_RUNTIME]= runtimeProx;
        targetStarter= targetStarter.Initialize(name, description, props.AsReadOnly());


        /* In place of actual job(s) this runtime starter registers for the targetStarter's Activate event,
         * so the original targetStarter activation gets handled by DoActivate, which delegates the start
         * to the job(s) and arranges to collect the jobs' results and fires the StartComplete event.
         */
        this.targetStartHandler= (starter, runProps) => DoActivate(runProps);
        targetStarter.Activate+= this.targetStartHandler;

        return this;
      }

      public void DoActivate(IProps invocationProps) {
        if (null == targetStarter) throw new InvalidOperationException("Already disposed.");
        var start= this.Activate;
        if (null == start) return;

        CopyBaseConfigStarterProps(ref invocationProps);
        bool concurrentStart= ConfigProperties.GetBool(invocationProps, MasterStarter.RPROP_PARALLEL_START, false);

        StarterCompletion compl;
        lock (sync) {
          if (!concurrentStart && null != pendingCompl) {
            /* Already pending start invocation.
             * Do not invoke until finished.
             */
            Log.LogInformation("Completion for Starter '{ST}' pending - cannot activate.", Name);
            return;
          }
          compl= pendingCompl= new StarterCompletion(this, invocationProps);
        }
        Log.LogInformation("Starter '{ST}' activated.", Name);
        start(compl, invocationProps);
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
        StarterActivationCompleter fireComplete= null;

        lock (sync) {
          var jobStartResults= (ISet<IJobResult>)compl.JobResults;
          jobStartResults.Add(jobResult);
          var start= this.Activate;
          if (null == start) return;
          if (jobStartResults.Count == start.GetInvocationList().Length) {
            /* All started jobs have completed:
             */
            Log.LogInformation("Starter '{ST}' activation completed.", Name);
            pendingCompl= null;
            fireComplete= internalStartComplete;
          }
        }
        if (null != fireComplete)
          fireComplete(compl);    //fire event with lock on sync held
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
        targetStarter= trig= null;
      }

      class RuntimeProxy : IJobControl {
        public IJobControl rt;

        private IJobControl JobControl {
          get {
            if (null == rt) throw new ObjectDisposedException("RuntimeProxy");
            return rt;
          }
        }

        public IJobControlCfgLoader ConfigLoader => JobControl.ConfigLoader;

        public IStarterCompletionPersister CompletionPersister => JobControl.CompletionPersister;

        public void Start() => JobControl.Start();

        public void Stop() => JobControl.Stop();

        public IMasterCfg MasterModels => JobControl.MasterModels;

        public IReadOnlyDictionary<string, IStarter> Starters => JobControl.Starters;

        public IReadOnlyDictionary<string, IJob> Jobs => JobControl.Jobs;

        public void Dispose() { rt= null; }
      }

      class StarterCompletion : IStarterCompletion, IStarterActivation {
        RuntimeStarter starter;
        DateTime time= App.TimeInfo.Now;
        IProps runProps;
        readonly HashSet<IJobResult> opResults= new HashSet<IJobResult>(JobResult.EqComparer);

        public StarterCompletion(RuntimeStarter starter, IProps runProps) {
          this.starter= starter;
          this.runProps= runProps;
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

        void IStarter.DoActivate(IProps starterProps) => throw new InvalidOperationException();

        string IModel.Name => starter.Name;

        string IModel.Description => starter.Description;

        IProps IModel.Properties => runProps;

        void IDisposable.Dispose() { }

      }// class StarterCompletion

    }//class StarterInstance

  }//class StarterMaster

}
