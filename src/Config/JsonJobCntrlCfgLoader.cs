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
    JobCntrlCfg jobCntrlCfg;
    IMasterCfg masterCfg;
    ///<summary>Ctor from <paramref name="configPath"/>.</summary>
    public JsonJobCntrlCfgLoader(string configPath) {
      var json= JsonFormat.CreateSerializer<JobCntrlCfg>();
      this.jobCntrlCfg= json.LoadObj(File.OpenRead(configPath));
      if (null == this.jobCntrlCfg.MasterCfg) throw new AppConfigException($"Missing '{nameof(this.jobCntrlCfg.MasterCfg)}' property.");

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
      ///<summary>Config path property.</summary>
      public const string CFG_PATH= "path";

      private string configPath;
      ///<summary>Default ctor.</summary>
      public Configurator() : this(null) { }

      ///<summary>Ctor from <paramref name="config"/>.</summary>
      public Configurator(IDictionary<string, string> config) {
        config= config ?? new Dictionary<string, string>();
        config.TryGetValue(CFG_PATH, out configPath);
        if (string.IsNullOrEmpty(configPath)) throw new AppConfigException($"{GetType().Name}: Missing config property '{CFG_PATH}'");
        if (!Path.IsPathRooted(configPath))
          configPath= Path.Combine(App.ContentRoot, configPath);
      }

      ///<inherit/>
      public void AddTo(IServiceCollection svcColl, IConfiguration cfg) {
        svcColl.AddSingleton<IJobControlCfgLoader>(new JsonJobCntrlCfgLoader(configPath));
      }

    }
  }
}