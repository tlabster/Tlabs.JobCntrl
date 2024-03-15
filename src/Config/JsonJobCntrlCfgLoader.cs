using System.IO;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

using Tlabs.Data.Serialize.Json;
using Tlabs.Config;

namespace Tlabs.JobCntrl.Config {

  ///<summary>Loads a <see cref="IJobControlCfg"/> form a json file.</summary>
  public class JsonJobCntrlCfgLoader : JobCntrlCfgLoader {
    ///<summary>Config path property.</summary>
    public const string CFG_PATH= "path";

    readonly string? configPath;
    ///<summary>Ctor from <paramref name="props"/> and <paramref name="configs"/>.</summary>
    public JsonJobCntrlCfgLoader(IJobCntrlCfgLoaderProperties props, IEnumerable<IJobCntrlConfigurator> configs) : base(configs) {
      if (null != props && props.TryGetValue(CFG_PATH, out configPath) && !Path.IsPathRooted(configPath))
        this.configPath= Path.Combine(App.ContentRoot, configPath);
    }

    ///<inheritdoc/>
    public override IMasterCfg LoadMasterConfiguration() {
      var json= JsonFormat.CreateSerializer<JobCntrlCfg>();
      this.jobCntrlCfg=   string.IsNullOrEmpty(configPath)
                        ? new JobCntrlCfg()
                        : json.LoadObj(File.OpenRead(configPath)) ?? throw EX.New<JobCntrlConfigException>("Error loading configuration from '{path}'", configPath);
      return base.LoadMasterConfiguration();
    }

    ///<summary>Service configurator to load config from JSON file.</summary>
    public class JsonConfigurator : IConfigurator<IServiceCollection> {
      readonly IDictionary<string, string> config;
      ///<summary>Default ctor.</summary>
      public JsonConfigurator() : this(null) { }

      ///<summary>Ctor from <paramref name="config"/>.</summary>
      public JsonConfigurator(IDictionary<string, string>? config) {
        this.config= config ?? new Dictionary<string, string>();
      }

      ///<inheritdoc/>
      public void AddTo(IServiceCollection svcColl, IConfiguration cfg) {
        svcColl.AddSingleton<IJobCntrlCfgLoaderProperties>(new JobCntrlCfgLoaderProperties(config));
        svcColl.AddSingleton<IJobControlCfgLoader, JsonJobCntrlCfgLoader>();
      }
    }
  }

}