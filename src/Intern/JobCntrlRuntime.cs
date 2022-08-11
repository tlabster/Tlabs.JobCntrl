using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Tlabs.Config;
using Tlabs.JobCntrl.Model;
using Tlabs.JobCntrl.Model.Intern;

namespace Tlabs.JobCntrl.Intern {
  /// <summary>Job control execution runtime.</summary>
  public sealed class JobCntrlRuntime : IJobControl {
    static readonly ILogger<JobCntrlRuntime> log= App.Logger<JobCntrlRuntime>();
    static readonly IJobControlCfgLoader EMPTY_LOADER= new Config.JobCntrlCfgLoader(null);
    static readonly RuntimeConfig EMPTY_CFG= new RuntimeConfig(EMPTY_LOADER.LoadRuntimeConfiguration(EMPTY_LOADER.LoadMasterConfiguration()));

    readonly IJobControlCfgLoader cfgLoader;
    readonly IStarterCompletionPersister trigComplPersisiter;
    private bool started;
    private RuntimeConfig runtimeConfig;
    private int starterActivationCnt;
    private TaskCompletionSource completionSrc;

    /// <summary>Ctor from <paramref name="cfgLoader"/>.</summary>
    public JobCntrlRuntime(IJobControlCfgLoader cfgLoader) : this(cfgLoader, null) { }

    /// <summary>Ctor from <paramref name="cfgLoader"/> and <paramref name="starterCompletionPersister"/>.</summary>
    public JobCntrlRuntime(IJobControlCfgLoader cfgLoader, IStarterCompletionPersister starterCompletionPersister) {
      if (null == (this.cfgLoader= cfgLoader)) throw new ArgumentNullException(nameof(cfgLoader));
      this.trigComplPersisiter= starterCompletionPersister;
    }

    /// <inheritdoc/>
    public IJobControlCfgLoader ConfigLoader { get { return cfgLoader; } }

    /// <inheritdoc/>
    public IStarterCompletionPersister CompletionPersister { get { return trigComplPersisiter; } }

    /// <inheritdoc/>
    public void Init() {
      RuntimeConfig runCfg= null;
      try {
        runCfg= Misc.Safe.CompareExchange(ref runtimeConfig,
                                          null,
                                          ()=> new RuntimeConfig(cfgLoader.LoadRuntimeConfiguration(cfgLoader.LoadMasterConfiguration()),
                                                                monitorStarterActivation,
                                                                notifyStarterFinished)
        );
      }
      catch (Exception e) {
        throw new JobCntrlConfigException("Error loading runtime configuration.", e);
      }
      if (null != runCfg) throw new InvalidOperationException($"{nameof(JobCntrlRuntime)} already started.");
      log.LogInformation("{runtime} configuration initialized.", nameof(JobCntrlRuntime));
    }

    /// <inheritdoc/>
    public void Start() {
      var runCfg= runtimeConfig;
      if (null == runCfg) throw new InvalidOperationException("Not initialized.");
      lock(runCfg) {
        if (this.started) throw new InvalidOperationException($"{nameof(JobCntrlRuntime)} already started.");
        log.LogInformation("Starting {module}", this.GetType().AssemblyQualifiedName);
        try {
          ((RuntimeConfig)runCfg).Configure();
        }
        catch (Exception e) {
          runCfg.Dispose();
          throw new JobCntrlException("Error starting runtime.", e);
        }

        log.LogInformation("{n} Master Starter Template(s)", runCfg.MasterModels.Starters.Count);
        log.LogInformation("{n} Master Job Template(s)", runCfg.MasterModels.Jobs.Count);
        log.LogInformation("{n} Starter(s)", runCfg.Starters.Count);
        log.LogInformation("{n} Job(s)", runCfg.Jobs.Count);
        this.started= true;
      }
    }

    /// <inheritdoc/>
    public void Stop() {
      var runCfg= Interlocked.Exchange(ref runtimeConfig, EMPTY_CFG);
      if (null != runCfg) lock(runCfg) {
        if (this.started) log.LogInformation("Stopping {name}", GetType().Name);
        this.started= false;
        if (CurrentJobActivationCount > 0) {
          log.LogInformation("Waiting for {cnt} pending job avtivations to complete...", CurrentJobActivationCount);
          FullCompletion.GetAwaiter().GetResult();
        }
        runCfg.Dispose();
        runtimeConfig= null;    //enable initalization
      }
    }

    /// <inheritdoc/>
    public IMasterCfg MasterModels {
      get {
        var rc= runtimeConfig;
        if (null == rc) throw new JobCntrlException("disposed");
        return rc.MasterModels;
      }
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, IStarter> Starters {
      get {
        var rc= runtimeConfig;
        if (null == rc) throw new JobCntrlException("disposed");
        return rc.Starters;
      }
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, IJob> Jobs {
      get {
        var rc= runtimeConfig;
        if (null == rc) throw new JobCntrlException("disposed");
        return rc.Jobs;
      }
    }

    /// <inheritdoc/>
    public int CurrentJobActivationCount => starterActivationCnt;

    /// <inheritdoc/>
    public Task FullCompletion => completionSrc?.Task ?? Task.CompletedTask;

    #region IDisposable
    /// <inheritdoc/>
    public void Dispose() {
      Stop();
    }
    #endregion

    private void monitorStarterActivation(IStarterActivationRequest actRequest) {
      if (1 == Interlocked.Increment(ref this.starterActivationCnt))
        Interlocked.Exchange(ref completionSrc, new TaskCompletionSource());
    }

    private void notifyStarterFinished(IStarterCompletion starterCompletion) {
      if (0 == Interlocked.Decrement(ref this.starterActivationCnt))
        completionSrc?.TrySetResult();    //notify full completion

      if (null != trigComplPersisiter)
        Task.Run(() => trigComplPersisiter.StoreCompletionInfo(starterCompletion)); //fire and forget: store starterCompletion asynch.
    }

    private sealed class MasterConfig : IMasterCfg, IDisposable {
      private IMasterCfg cfg;
      public MasterConfig(IMasterCfg cfg) => this.cfg= cfg;
      public IReadOnlyDictionary<string, MasterStarter> Starters => cfg.Starters;
      public IReadOnlyDictionary<string, MasterJob> Jobs => cfg.Jobs;
      public void Dispose() {
        var c= cfg;
        if (null == c) return;
        foreach (var pair in c.Jobs) pair.Value.Dispose();
        foreach (var pair in c.Starters) pair.Value.Dispose();
        #pragma warning disable IDE0059 //help gc
        cfg= c= null;
      }
    }

    private sealed class RuntimeConfig : IDisposable {
      private MasterConfig masterCfg;
      private IJobControlCfg cntrlCfg;
      private ModelDictionary<IStarter> starters;
      private ModelDictionary<IJob> jobs;

      readonly StarterActivationMonitor handleActivation;
      readonly StarterActivationCompleter handleFinished;

      public RuntimeConfig(IJobControlCfg cntrlCfg,
                          StarterActivationMonitor starterActivationMonitor= null,
                          StarterActivationCompleter starterFinishedHandler= null)
      {
        this.masterCfg= new MasterConfig(cntrlCfg.MasterModels);
        this.cntrlCfg= cntrlCfg;
        this.handleActivation= starterActivationMonitor;
        this.handleFinished= starterFinishedHandler;

        this.starters= new ModelDictionary<IStarter>();
        this.jobs= new ModelDictionary<IJob>();
      }

      public void Configure() {
        /* Enable starters and register for completion event
         */
        foreach (var cfgStarter in cntrlCfg.Starters) {
          var masterStarter= masterCfg.Starters[cfgStarter.Master];
          var runStarter= masterStarter.CreateRuntimeStarter(cfgStarter.Name, cfgStarter.Description, cfgStarter.Properties);
          this.starters.Add(runStarter.Name, runStarter);
          if (null != this.handleActivation) runStarter.ActivationTriggered+= this.handleActivation;
          if (null != this.handleFinished) runStarter.ActivationFinalized+= this.handleFinished;
          runStarter.Enabled= true;
        }

        /* Configure jobs with starters
         */
        foreach(var cfgJob in cntrlCfg.Jobs) {
          var masterJob= masterCfg.Jobs[cfgJob.Master];
          var runJob= masterJob.CreateRuntimeJob(this.Starters[cfgJob.Starter], cfgJob.Name, cfgJob.Description, cfgJob.Properties);
          this.jobs.Add(runJob.Name, runJob);
        }
      }

      public IMasterCfg MasterModels => masterCfg;

      public IReadOnlyDictionary<string, IStarter> Starters => starters;

      public IReadOnlyDictionary<string, IJob> Jobs => jobs;

      public void Dispose() {
        var jobs= this.jobs;
        if (null != jobs) {
          foreach (var pair in jobs) pair.Value.Dispose();
          this.jobs= jobs= null;
        }
        var starters= this.starters;
        if (null != starters) {
          foreach (var pair in starters) if (pair.Value is IRuntimeStarter runSt) {
            if (null != this.handleActivation) runSt.ActivationTriggered-= this.handleActivation;
            if (null != this.handleFinished) runSt.ActivationComplete-= this.handleFinished;
            runSt.Dispose();
          }
          this.starters= starters= null;
        }
        cntrlCfg= null;
        masterCfg?.Dispose();
        masterCfg= null;
      }

    } //class RuntimConfig

    ///<summary>Service configurator.</summary>
    public class Configurator : IConfigurator<IServiceCollection> {
      ///<inheritdoc/>
      public void AddTo(IServiceCollection svcColl, IConfiguration cfg) {
        svcColl.AddSingleton<IJobControl, JobCntrlRuntime>();
      }
    }
  }//class JobCntrlRuntime

}
