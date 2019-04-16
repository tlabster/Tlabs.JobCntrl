using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Tlabs.Config;
using Tlabs.Data.Serialize.Json;
using Tlabs.JobCntrl.Model;
using Tlabs.JobCntrl.Model.Intern;

namespace Tlabs.JobCntrl.Config {

  ///<summary>Loads a <see cref="IJobControlCfg"/> form a json file.</summary>
  public class JsonJobCntrlCfgLoader : IJobControlCfgLoader {
    ///<summary>Config path property.</summary>
    public const string CFG_PATH= "path";
    JobCntrlCfg jobCntrlCfg;
    IMasterCfg masterCfg;
    ///<summary>Ctor from <paramref name="props"/> and <paramref name="configs"/>.</summary>
    public JsonJobCntrlCfgLoader(IJobCntrlCfgLoaderProperties props, IEnumerable<IJobCntrlConfigurator> configs) {
      string configPath;
      if (!props.TryGetValue(CFG_PATH, out configPath)) throw new Tlabs.AppConfigException($"Missing {CFG_PATH} config");
      if (!Path.IsPathRooted(configPath))
        configPath= Path.Combine(App.ContentRoot, configPath);
      var json= JsonFormat.CreateSerializer<JobCntrlCfg>();
      this.jobCntrlCfg= json.LoadObj(File.OpenRead(configPath));

      this.jobCntrlCfg.MasterCfg=           this.jobCntrlCfg.MasterCfg ?? new JobCntrlCfg.MasterConfig();
      this.jobCntrlCfg.MasterCfg.Starters=  this.jobCntrlCfg.MasterCfg.Starters ?? new List<JobCntrlCfg.MasterCfgEntry>();
      this.jobCntrlCfg.MasterCfg.Jobs=      this.jobCntrlCfg.MasterCfg.Jobs ?? new List<JobCntrlCfg.MasterCfgEntry>();

      this.jobCntrlCfg.ControlCfg=          this.jobCntrlCfg.ControlCfg ?? new JobCntrlCfg.ControlConfig();
      this.jobCntrlCfg.ControlCfg.Starters= this.jobCntrlCfg.ControlCfg.Starters ?? new List<JobCntrlCfg.StarterCfg>();
      this.jobCntrlCfg.ControlCfg.Jobs=     this.jobCntrlCfg.ControlCfg.Jobs ?? new List<JobCntrlCfg.JobCfg>();

      foreach(var cfg in configs) {
        var cntrlCfg= cfg.JobCntrlCfg;
        this.jobCntrlCfg.MasterCfg.Starters.AddRange(cntrlCfg.MasterCfg.Starters);
        this.jobCntrlCfg.MasterCfg.Jobs.AddRange(cntrlCfg.MasterCfg.Jobs);
        this.jobCntrlCfg.ControlCfg.Starters.AddRange(cntrlCfg.ControlCfg.Starters);
        this.jobCntrlCfg.ControlCfg.Jobs.AddRange(cntrlCfg.ControlCfg.Jobs);
      }

      this.masterCfg= new MasterCfg(this.jobCntrlCfg);
    }

    ///<inherit/>
    public IJobControlCfgPersister ConfigPersister => throw new NotImplementedException();

    ///<inherit/>
    public IMasterCfg LoadMasterConfiguration() => this.masterCfg;

    ///<inherit/>
    public IJobControlCfg LoadRuntimeConfiguration(IMasterCfg masterCfg) {
      if (null == (this.masterCfg= masterCfg)) throw new ArgumentNullException(nameof(masterCfg));
      return new CntrlCfg(this);
    }

    private class MasterCfg : IMasterCfg {
      JobCntrlCfg jobCntrlCfg;
      public MasterCfg(JobCntrlCfg jobCntrlCfg) {
        this.jobCntrlCfg= jobCntrlCfg;

        if (null == jobCntrlCfg.MasterCfg.Starters) throw new AppConfigException($"Missing '{nameof(jobCntrlCfg.MasterCfg.Starters)}' property.");
        Starters= jobCntrlCfg.MasterCfg.Starters.Select(e => e.ToMasterStarter()).ToDictionary(s => s.Name);

        if (null == jobCntrlCfg.MasterCfg.Jobs) throw new AppConfigException($"Missing '{nameof(jobCntrlCfg.MasterCfg.Jobs)}' property.");
        Jobs= jobCntrlCfg.MasterCfg.Jobs.Select(e => e.ToMasterJob()).ToDictionary(j => j.Name);
      }
      public IReadOnlyDictionary<string, MasterStarter> Starters { get; }
      public IReadOnlyDictionary<string, MasterJob> Jobs { get; }
    }

    private class CntrlCfg : IJobControlCfg {
      JsonJobCntrlCfgLoader cfgLoader;
      public CntrlCfg(JsonJobCntrlCfgLoader cfgLoader) { this.cfgLoader= cfgLoader; }
      public IMasterCfg MasterModels => cfgLoader.masterCfg;

      public IEnumerable<IModelCfg> Starters => this.cfgLoader.jobCntrlCfg.ControlCfg.Starters;

      public IEnumerable<IJobCfg> Jobs => this.cfgLoader.jobCntrlCfg.ControlCfg.Jobs;
    }

    ///<summary>Service configurator.</summary>
    public class Configurator : IConfigurator<IServiceCollection> {
 
      private IDictionary<string, string> config;
      ///<summary>Default ctor.</summary>
      public Configurator() : this(null) { }

      ///<summary>Ctor from <paramref name="config"/>.</summary>
      public Configurator(IDictionary<string, string> config) {
        this.config= config ?? new Dictionary<string, string>();
      }

      ///<inherit/>
      public void AddTo(IServiceCollection svcColl, IConfiguration cfg) {
        svcColl.AddSingleton<IJobCntrlCfgLoaderProperties>(new JobCntrlCfgLoaderProperties(config));
        svcColl.AddSingleton<IJobControlCfgLoader, JsonJobCntrlCfgLoader>();
      }

    }
  }
}