#pragma warning disable CS1591

using System.Collections.Generic;
using Tlabs.JobCntrl.Model;
using Tlabs.JobCntrl.Model.Intern;

namespace Tlabs.JobCntrl.Config {
  using IProps= IReadOnlyDictionary<string, object>;

  /// <summary>JobControl config. loader properties.</summary>
  public class JobCntrlCfgLoaderProperties : Dictionary<string, string>, IJobCntrlCfgLoaderProperties {
    public JobCntrlCfgLoaderProperties(IDictionary<string, string> dict) : base(dict) { }
  }

  public class JobCntrlCfg {
    public MasterConfig MasterCfg { get; set; }
    public ControlConfig ControlCfg { get; set; }


    public class MasterConfig {
      public List<MasterCfgEntry> Starters { get; set; }
      public List<MasterCfgEntry> Jobs { get; set; }
    }

    public class MasterCfgEntry {
      public string Name { get; set; }

      public string Description { get; set; }
      public string Type { get; set; }
      public IProps Properties { get; set; }

      public MasterStarter ToMasterStarter() => new MasterStarter(Name, Description ?? "", Misc.Safe.LoadType(Type, "TargetStarterType"), Properties);
      public MasterJob ToMasterJob() => new MasterJob(Name, Description ?? "", Misc.Safe.LoadType(Type, "RuntimeJobType"), Properties);
    }

    public class ControlConfig {
      public List<StarterCfg> Starters { get; set; }
      public List<JobCfg> Jobs { get; set; }
    }

    public class StarterCfg : IModelCfg {
      public string Master { get; set; }
      public string Name { get; set; }
      public string Description { get; set; }
      public IProps Properties { get; set; }
#pragma warning disable CA1816    //nothing to dispose here
      public void Dispose() { }
#pragma warning restore CA1816
    }

    public class JobCfg : StarterCfg, IJobCfg {
      public string Starter { get; set; }
    }
  }

}