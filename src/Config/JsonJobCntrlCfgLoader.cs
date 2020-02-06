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
    
    IEnumerable<IJobCntrlConfigurator> configs;
    string configPath;
    JsonFormat.Serializer<JobCntrlCfg> json;
    JobCntrlCfg jobCntrlCfg;
    ///<summary>Ctor from <paramref name="props"/> and <paramref name="configs"/>.</summary>
    public JsonJobCntrlCfgLoader(IJobCntrlCfgLoaderProperties props, IEnumerable<IJobCntrlConfigurator> configs) {
      if (!props.TryGetValue(CFG_PATH, out configPath)) throw new Tlabs.AppConfigException($"Missing {CFG_PATH} config");
      if (!Path.IsPathRooted(configPath))
        this.configPath= Path.Combine(App.ContentRoot, configPath);
      if (null == (this.configs= configs)) throw new ArgumentNullException(nameof(configs));
      this.json= JsonFormat.CreateSerializer<JobCntrlCfg>();
    }

    ///<inherit/>
    public IJobControlCfgPersister ConfigPersister => throw new NotImplementedException();

    ///<inherit/>
    public IMasterCfg LoadMasterConfiguration() {
      this.jobCntrlCfg= json.LoadObj(File.OpenRead(configPath));

      this.jobCntrlCfg.MasterCfg=           this.jobCntrlCfg.MasterCfg ?? new JobCntrlCfg.MasterConfig();
      this.jobCntrlCfg.MasterCfg.Starters=  this.jobCntrlCfg.MasterCfg.Starters ?? new List<JobCntrlCfg.MasterCfgEntry>();
      this.jobCntrlCfg.MasterCfg.Jobs=      this.jobCntrlCfg.MasterCfg.Jobs ?? new List<JobCntrlCfg.MasterCfgEntry>();

      this.jobCntrlCfg.ControlCfg=          this.jobCntrlCfg.ControlCfg ?? new JobCntrlCfg.ControlConfig();
      this.jobCntrlCfg.ControlCfg.Starters= this.jobCntrlCfg.ControlCfg.Starters ?? new List<JobCntrlCfg.StarterCfg>();
      this.jobCntrlCfg.ControlCfg.Jobs=     this.jobCntrlCfg.ControlCfg.Jobs ?? new List<JobCntrlCfg.JobCfg>();

      foreach (var cfg in configs) {
        var cntrlCfg= cfg.JobCntrlCfg;
        this.jobCntrlCfg.MasterCfg.Starters.AddRange(cntrlCfg.MasterCfg.Starters);
        this.jobCntrlCfg.MasterCfg.Jobs.AddRange(cntrlCfg.MasterCfg.Jobs);
        this.jobCntrlCfg.ControlCfg.Starters.AddRange(cntrlCfg.ControlCfg.Starters);
        this.jobCntrlCfg.ControlCfg.Jobs.AddRange(cntrlCfg.ControlCfg.Jobs);
      }

      return new MasterCfg(this.jobCntrlCfg);
    }

    ///<inherit/>
    public IJobControlCfg LoadRuntimeConfiguration(IMasterCfg masterCfg) {
      return new CntrlCfg(masterCfg, jobCntrlCfg.ControlCfg);
    }

    private class MasterCfg : IMasterCfg {
      JobCntrlCfg jobCntrlCfg;
      public MasterCfg(JobCntrlCfg jobCntrlCfg) {
        this.jobCntrlCfg= jobCntrlCfg;

        if (null == jobCntrlCfg.MasterCfg.Starters) throw new AppConfigException($"Missing '{nameof(jobCntrlCfg.MasterCfg.Starters)}' property.");
        Starters= jobCntrlCfg.MasterCfg.Starters.Select(e => e.ToMasterStarter()).ToMasterDictionary();

        if (null == jobCntrlCfg.MasterCfg.Jobs) throw new AppConfigException($"Missing '{nameof(jobCntrlCfg.MasterCfg.Jobs)}' property.");
        Jobs= jobCntrlCfg.MasterCfg.Jobs.Select(e => e.ToMasterJob()).ToMasterDictionary();
      }
      public IReadOnlyDictionary<string, MasterStarter> Starters { get; }
      public IReadOnlyDictionary<string, MasterJob> Jobs { get; }
    }

    private class CntrlCfg : IJobControlCfg {
      JobCntrlCfg.ControlConfig cntrlCfg;
      public CntrlCfg(IMasterCfg masterCfg, JobCntrlCfg.ControlConfig cntrlCfg) { this.MasterModels= masterCfg; this.cntrlCfg= cntrlCfg; }
      public IMasterCfg MasterModels { get; }

      public IEnumerable<IModelCfg> Starters => cntrlCfg.Starters;

      public IEnumerable<IJobCfg> Jobs => cntrlCfg.Jobs;
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

  ///<summary>Extension class.</summary>
  public static class ToMasterDictionaryExtension {
    ///<summary>Convert <paramref name="masters"/> into dictionary with overwriting exisiting.</summary>
    public static  Dictionary<string, T> ToMasterDictionary<T>(this IEnumerable<T> masters) where T : BaseModel {
      var dict= new Dictionary<string, T>();
      foreach (var m in masters)
        dict[m.Name]= m;
      return dict;
    }
  }
}