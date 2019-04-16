using System.Collections.Generic;
using Tlabs.JobCntrl.Model.Intern;

namespace Tlabs.JobCntrl.Config {
  using PropDictionary= Dictionary<string, object>;

  /// <summary>JobControl configurator.</summary>
  public class JobCntrlConfigurator : JobCntrlCfg, IJobCntrlConfigurator {
    /// <inherit/>
    public JobCntrlCfg JobCntrlCfg => this;

    /// <summary>Default ctor.</summary>
    public JobCntrlConfigurator() {
      this.MasterCfg.Starters= new List<MasterCfgEntry>();
      this.MasterCfg.Jobs= new List<MasterCfgEntry>();
      this.ControlCfg.Starters= new List<StarterCfg>();
      this.ControlCfg.Jobs= new List<JobCfg>();
  }

    /// <summary>Define a <see cref="MasterStarter"/>.</summary>
    public JobCntrlConfigurator DefineMasterStarter(string name, string description, string type, PropDictionary properties= null) {
      MasterCfg.Starters.Add(new MasterCfgEntry {
        Name= name,
        Description= description,
        Type= type,
        Properties= properties
      });
      return this;
    }

    /// <summary>Define a <see cref="MasterJob"/>.</summary>
    public JobCntrlConfigurator DefineMasterJob(string name, string description, string type, PropDictionary properties= null) {
      MasterCfg.Jobs.Add(new MasterCfgEntry {
        Name= name,
        Description= description,
        Type= type,
        Properties= properties
      });
      return this;
    }

    /// <summary>Define a runtime starter.</summary>
    public JobCntrlConfigurator DefineStarter(string name, string master, string description, PropDictionary properties = null) {
      this.ControlCfg.Starters.Add(new StarterCfg {
        Master= master,
        Name= name,
        Description= description,
        Properties= properties
      });
      return this;
    }

    /// <summary>Define a runtime job.</summary>
    public JobCntrlConfigurator DefineJob(string name, string master, string starter, string description, PropDictionary properties = null) {
      this.ControlCfg.Jobs.Add(new JobCfg {
        Master= master,
        Name= name,
        Starter= starter,
        Description= description,
        Properties= properties
      });
      return this;
    }
  }
}