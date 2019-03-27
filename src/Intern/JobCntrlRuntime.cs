using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Tlabs.JobCntrl.Model;
using Tlabs.JobCntrl.Model.Intern;

namespace Tlabs.JobCntrl.Intern {
  /// <summary>Job control execution runtime.</summary>
  sealed class JobCntrlRuntime : IJobControl {
    private bool started;
    private ILogger<JobCntrlRuntime> log;
    private IJobControlCfgLoader cfgLoader;
    private RuntimConfig runtimeConfig;
    private IStarterCompletionPersister trigComplPersisiter;

    public JobCntrlRuntime(IJobControlCfgLoader cfgLoader, ILogger<JobCntrlRuntime> log) : this(cfgLoader, null, log) { }

    public JobCntrlRuntime(IJobControlCfgLoader cfgLoader, IStarterCompletionPersister starterCompletionPersister, ILogger<JobCntrlRuntime> log) {
      if (null == (this.cfgLoader= cfgLoader)) throw new ArgumentNullException("cfgLoader");
      this.trigComplPersisiter= starterCompletionPersister;
      this.log= log;
    }

    public IJobControlCfgLoader ConfigLoader { get { return cfgLoader; } }

    public IStarterCompletionPersister CompletionPersister { get { return trigComplPersisiter; } }

    public void Init() {
      // /* Deferre the start-up of the Job Runtime until the GlobalConfig.Components are
      //  * available. This allows Job/Starters to access component objects during their initialization.
      //  */
      // GlobalConfig.AfterComponentInit.BeginInvoke((Action)Start);
      var runCfg= runtimeConfig;
      if (null != runCfg) throw new InvalidOperationException("Already initialized.");
      try {
        var masterCfg= cfgLoader.LoadMasterConfiguration();
        runtimeConfig= runCfg= new RuntimConfig(cfgLoader.LoadRuntimeConfiguration(masterCfg), HandleStarterCompletion);
      }
      catch (Exception e) {
        log.LogError("Error loading runtime configuration: {msg}", e.Message);
        runtimeConfig?.Dispose();
        throw;
      }
      log.LogInformation("Initialized.");
    }

    public void Start() {
      var runCfg= runtimeConfig;
      if (null == runCfg) throw new InvalidOperationException("Not initialized.");
      if (this.started) throw new InvalidOperationException($"{nameof(JobCntrlRuntime)} already started.");

      log.LogInformation("Starting {module}", this.GetType().AssemblyQualifiedName);
      try {
        ((RuntimConfig)runCfg).Configure();
      }
      catch (Exception e) {
        log.LogError("Error starting {module}: {msg}", GetType().Name, e.Message);
        runtimeConfig?.Dispose();
        throw;
      }



      log.LogInformation("{n} Master Starter Template(s)", runCfg.MasterModels.Starters.Count);
      log.LogInformation("{n} Master Job Template(s)", runCfg.MasterModels.Jobs.Count);
      log.LogInformation("{n} Starter(s)", runCfg.Starters.Count);
      log.LogInformation("{n} Job(s)", runCfg.Jobs.Count);
      this.started= true;
    }

    public void Stop() {
      var runCfg= runtimeConfig;
      if (null != runCfg) {
        if (this.started) log.LogInformation("Stopping {0}", GetType().Name);
        runCfg.Dispose();
      }
      this.started= false;
      runtimeConfig= runCfg= null;

      //***TODO: We better should wait until everything went down...
      //System.Threading.Thread.Sleep(3141);
    }

    public IMasterCfg MasterModels {
      get {
        var rc= runtimeConfig;
        if (null == rc) throw new JobCntrlException("disposed");
        return rc.MasterModels;
      }
    }

    public IReadOnlyDictionary<string, IStarter> Starters {
      get {
        var rc= runtimeConfig;
        if (null == rc) throw new JobCntrlException("disposed");
        return rc.Starters;
      }
    }

    public IReadOnlyDictionary<string, IJob> Jobs {
      get {
        var rc= runtimeConfig;
        if (null == rc) throw new JobCntrlException("disposed");
        return rc.Jobs;
      }
    }

    #region IDisposable
    public void Dispose() {
      Stop();
    }
    #endregion

    private void HandleStarterCompletion(IStarterCompletion starterCompletion) {
      if (null == trigComplPersisiter) return;

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
        cfg= c= null;
      }
    }

    private sealed class RuntimConfig : IDisposable {
      private MasterConfig masterCfg;
      private IJobControlCfg cntrlCfg;
      private ModelDictionary<IStarter> starters;
      private ModelDictionary<IJob> jobs;


      private StarterActivationCompleter handleCompletion;

      public RuntimConfig(IJobControlCfg cntrlCfg, StarterActivationCompleter starterCompletionHandler) {
        this.masterCfg= new MasterConfig(cntrlCfg.MasterModels);
        this.cntrlCfg= cntrlCfg;
        this.handleCompletion= starterCompletionHandler;

        this.starters= new ModelDictionary<IStarter>();
        this.jobs= new ModelDictionary<IJob>();
      }

      public void Configure() {
        /* Enable starters and register for completion event
         */
        foreach (var cfgStarter in cntrlCfg.Starters) {
          var masterStarter= masterCfg.Starters[cfgStarter.MasterName];
          var runStarter= masterStarter.CreateRuntimeStarter(cfgStarter.Name, cfgStarter.Description, cfgStarter.Properties);
          this.starters.Add(runStarter.Name, runStarter);
          runStarter.ActivationComplete+= this.handleCompletion;
          runStarter.Enabled= true;
        }

        /* Configure jobs with starters
         */
        foreach(var cfgJob in cntrlCfg.Jobs) {
          var masterJob= masterCfg.Jobs[cfgJob.MasterName];
          var runJob= masterJob.CreateRuntimeJob(this.Starters[cfgJob.StarterName], cfgJob.Name, cfgJob.Description, cfgJob.Properties);
          this.jobs.Add(runJob.Name, runJob);
        }
      }

      public IMasterCfg MasterModels => masterCfg;

      public IReadOnlyDictionary<string, IStarter> Starters => starters;

      public IReadOnlyDictionary<string, IJob> Jobs => jobs;

      public void Dispose() {
        var starters= this.starters;
        if (null != starters) {
          foreach (var pair in starters) {
            var runStr= pair.Value as IRuntimeStarter;
            if (null != runStr)
              runStr.ActivationComplete-= this.handleCompletion;
            pair.Value.Dispose();
          }
          this.starters= starters= null;
        }
        var jobs= this.jobs;
        if (null != jobs) {
          foreach (var pair in jobs) pair.Value.Dispose();
          this.jobs= jobs= null;
        }
        cntrlCfg= null;
        masterCfg?.Dispose();
        masterCfg= null;
      }

    } //class RuntimConfig

  }//class JobCntrlRuntime

}
