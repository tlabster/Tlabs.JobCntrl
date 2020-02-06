using System.Collections.Generic;
using Tlabs.JobCntrl.Model.Intern;

namespace Tlabs.JobCntrl.Config {
  using IProps= IReadOnlyDictionary<string, object>;

  /// <summary>JobControl configurator.</summary>
  public class JobCntrlConfigurator : JobCntrlCfg, IJobCntrlConfigurator {
    /// <inheritdoc/>
    public JobCntrlCfg JobCntrlCfg => this;

    /// <summary>Default ctor.</summary>
    public JobCntrlConfigurator() {
      this.MasterCfg= new JobCntrlCfg.MasterConfig();
      this.MasterCfg.Starters= new List<MasterCfgEntry>();
      this.MasterCfg.Jobs= new List<MasterCfgEntry>();

      this.ControlCfg= new JobCntrlCfg.ControlConfig();
      this.ControlCfg.Starters= new List<StarterCfg>();
      this.ControlCfg.Jobs= new List<JobCfg>();
  }

    /// <inheritdoc/>
    public IJobCntrlConfigurator DefineMasterStarter(string name, string description, string type, IProps properties= null) {
      MasterCfg.Starters.Add(new MasterCfgEntry {
        Name= name,
        Description= description,
        Type= type,
        Properties= properties
      });
      return this;
    }

    /// <inheritdoc/>
    public IJobCntrlConfigurator DefineMasterJob(string name, string description, string type, IProps properties= null) {
      MasterCfg.Jobs.Add(new MasterCfgEntry {
        Name= name,
        Description= description,
        Type= type,
        Properties= properties
      });
      return this;
    }

    /// <inheritdoc/>
    public IJobCntrlConfigurator DefineStarter(string name, string master, string description, IProps properties = null) {
      this.ControlCfg.Starters.Add(new StarterCfg {
        Master= master,
        Name= name,
        Description= description,
        Properties= properties
      });
      return this;
    }

    /// <inheritdoc/>
    public IJobCntrlConfigurator DefineJob(string name, string master, string starter, string description, IProps properties = null) {
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