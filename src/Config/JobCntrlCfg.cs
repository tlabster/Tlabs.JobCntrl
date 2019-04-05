using System.Collections.Generic;
using Tlabs.JobCntrl.Model;
using Tlabs.JobCntrl.Model.Intern;

namespace Tlabs.JobCntrl.Config {

  internal class JobCntrlCfg {
    public MasterConfig MasterCfg { get; set; }
    public ControlConfig ControlCfg { get; set; }


    internal class MasterConfig {
      public List<MasterCfgEntry> Starters { get; set; }
      public List<MasterCfgEntry> Jobs { get; set; }
    }

    internal class MasterCfgEntry {
      public string Name { get; set; }

      public string Description { get; set; }
      public string Type { get; set; }
      public Dictionary<string, object> Properties { get; set; }

      public MasterStarter ToMasterStarter() => new MasterStarter(Name, Description ?? "", Misc.Safe.LoadType(Type, "MasterStarter"), Properties);
      public MasterJob ToMasterJob() => new MasterJob(Name, Description ?? "", Misc.Safe.LoadType(Type, "MasterStarter"), Properties);
    }

    internal class ControlConfig {
      public List<StarterCfg> Starters { get; set; }
      public List<JobCfg> Jobs { get; set; }
    }

    internal class StarterCfg : IModelCfg {
      private string masterName;
      private string name;
      private string description;
      private IReadOnlyDictionary<string, object> properties;
      public StarterCfg(string master, string name, string description, IReadOnlyDictionary<string, object> properties) {
        this.masterName= master;
        this.name= name;
        this.description= description;
        this.properties= properties;
      }
      public string MasterName => masterName;
      public string Name => name;
      public string Description => description;
      public IReadOnlyDictionary<string, object> Properties => properties;
      public void Dispose() { }
    }

    internal class JobCfg : StarterCfg, IJobCfg {
      private string starter;
      public JobCfg(string master, string name, string starterName, string description, IReadOnlyDictionary<string, object> properties) : base(master, name, description, properties) {
        this.starter= starterName;
      }
      public string StarterName => starter;
    }
  }

}