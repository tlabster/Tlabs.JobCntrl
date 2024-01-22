#pragma warning disable CS1591

using System.Collections.Generic;
using System.Collections.Immutable;

using Tlabs.JobCntrl.Model;
using Tlabs.JobCntrl.Model.Intern;

namespace Tlabs.JobCntrl.Config {
  using IProps= IReadOnlyDictionary<string, object?>;

  /// <summary>JobControl config. loader properties.</summary>
  public class JobCntrlCfgLoaderProperties : Dictionary<string, string>, IJobCntrlCfgLoaderProperties {
    public JobCntrlCfgLoaderProperties(IDictionary<string, string> dict) : base(dict) { }
  }

  public class JobCntrlCfg {
    public MasterConfig MasterCfg { get; set; }= new JobCntrlCfg.MasterConfig {
                                                    Starters= new List<JobCntrlCfg.MasterCfgEntry>(),
                                                    Jobs=     new List<JobCntrlCfg.MasterCfgEntry>()
                                                  };
    public ControlConfig ControlCfg { get; set; }= new JobCntrlCfg.ControlConfig {
                                                      Starters= new List<JobCntrlCfg.StarterCfg>(),
                                                      Jobs=     new List<JobCntrlCfg.JobCfg>()
                                                    };
    public class MasterConfig {
      public required List<MasterCfgEntry> Starters { get; set; }
      public required List<MasterCfgEntry> Jobs { get; set; }
    }

    public class MasterCfgEntry {
      public required string Name { get; set; }

      public string? Description { get; set; }
      public required string Type { get; set; }
      public IProps? Properties { get; set; }

      public MasterStarter ToMasterStarter() => new MasterStarter(Name, Description ?? "", Misc.Safe.LoadType(Type, "TargetStarterType"), Properties);
      public MasterJob ToMasterJob() => new MasterJob(Name, Description ?? "", Misc.Safe.LoadType(Type, "RuntimeJobType"), Properties);
    }

    public class ControlConfig {
      public required List<StarterCfg> Starters { get; init; }
      public required List<JobCfg> Jobs { get; init; }
    }

    public class StarterCfg : IModelCfg {
      public string Master { get; set; }= "?";
      public string Name { get; set; }= "?";
      public string Description { get; set; }= "";
      public IProps Properties { get; set; }= ImmutableDictionary<string, object?>.Empty;
#pragma warning disable CA1816    //nothing to dispose here
      public void Dispose() { }
#pragma warning restore CA1816
    }

    public class JobCfg : StarterCfg, IJobCfg {
      public string Starter { get; set; }= "?";
    }
  }

}