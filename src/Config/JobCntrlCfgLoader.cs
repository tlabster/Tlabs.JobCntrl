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

  ///<summary>Loads a <see cref="IJobControlCfg"/> form enumeration of <see cref="IJobCntrlConfigurator"/>(s).</summary>
  public class JobCntrlCfgLoader : IJobControlCfgLoader {

    readonly IEnumerable<IJobCntrlConfigurator> configs;
    ///<summary>Loaded configuration</summary>
    protected JobCntrlCfg jobCntrlCfg;

    ///<summary>Ctor from <paramref name="configs"/>.</summary>
    public JobCntrlCfgLoader(IEnumerable<IJobCntrlConfigurator>? configs= null) {
      this.configs= configs ?? Enumerable.Empty<IJobCntrlConfigurator>();
      this.jobCntrlCfg=  new JobCntrlCfg {
        MasterCfg= new JobCntrlCfg.MasterConfig {
          Starters= new List<JobCntrlCfg.MasterCfgEntry>(),
          Jobs=     new List<JobCntrlCfg.MasterCfgEntry>()
        },
        ControlCfg= new JobCntrlCfg.ControlConfig {
          Starters= new List<JobCntrlCfg.StarterCfg>(),
          Jobs=     new List<JobCntrlCfg.JobCfg>()
        }
      };
    }

    ///<inheritdoc/>
    public IJobControlCfgPersister ConfigPersister => throw new NotImplementedException();

    ///<inheritdoc/>
    public virtual IMasterCfg LoadMasterConfiguration() {

      foreach (var cfg in configs) {
        var cntrlCfg= cfg.JobCntrlCfg;
        this.jobCntrlCfg.MasterCfg.Starters.AddRange(cntrlCfg.MasterCfg.Starters);
        this.jobCntrlCfg.MasterCfg.Jobs.AddRange(cntrlCfg.MasterCfg.Jobs);
        this.jobCntrlCfg.ControlCfg.Starters.AddRange(cntrlCfg.ControlCfg.Starters);
        this.jobCntrlCfg.ControlCfg.Jobs.AddRange(cntrlCfg.ControlCfg.Jobs);
      }

      return new MasterCfg(this.jobCntrlCfg);
    }

    ///<inheritdoc/>
    public IJobControlCfg LoadRuntimeConfiguration(IMasterCfg masterCfg) {
      return new CntrlCfg(masterCfg, jobCntrlCfg.ControlCfg);
    }

    private class MasterCfg : IMasterCfg {
      public MasterCfg(JobCntrlCfg jobCntrlCfg) {
        if (null == jobCntrlCfg.MasterCfg.Starters) throw new AppConfigException($"Missing '{nameof(jobCntrlCfg.MasterCfg.Starters)}' property.");
        Starters= jobCntrlCfg.MasterCfg.Starters.Select(e => e.ToMasterStarter()).ToMasterDictionary();

        if (null == jobCntrlCfg.MasterCfg.Jobs) throw new AppConfigException($"Missing '{nameof(jobCntrlCfg.MasterCfg.Jobs)}' property.");
        Jobs= jobCntrlCfg.MasterCfg.Jobs.Select(e => e.ToMasterJob()).ToMasterDictionary();
      }
      public IReadOnlyDictionary<string, MasterStarter> Starters { get; }
      public IReadOnlyDictionary<string, MasterJob> Jobs { get; }
    }

    private class CntrlCfg : IJobControlCfg {
      readonly JobCntrlCfg.ControlConfig cntrlCfg;
      public CntrlCfg(IMasterCfg masterCfg, JobCntrlCfg.ControlConfig cntrlCfg) { this.MasterModels= masterCfg; this.cntrlCfg= cntrlCfg; }
      public IMasterCfg MasterModels { get; }

      public IEnumerable<IModelCfg> Starters => cntrlCfg.Starters;

      public IEnumerable<IJobCfg> Jobs => cntrlCfg.Jobs;
    }

    ///<summary>Service configurator to load config from JSON file.</summary>
    public class Configurator : IConfigurator<IServiceCollection> {
      ///<inheritdoc/>
      public void AddTo(IServiceCollection svcColl, IConfiguration cfg) {
        svcColl.AddSingleton<IJobControlCfgLoader, JobCntrlCfgLoader>();
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