using System.Collections.Generic;
using System.Collections.Immutable;

namespace Tlabs.JobCntrl.Config {
  using IProps= IReadOnlyDictionary<string, object?>;

  /// <summary>JobControl configurator.</summary>
  public class JobCntrlConfigurator : JobCntrlCfg, IJobCntrlConfigurator {
    /// <inheritdoc/>
    public JobCntrlCfg JobCntrlCfg => this;

    /// <inheritdoc/>
    public IJobCntrlConfigurator DefineMasterStarter(string name, string description, string type, IProps? properties= null) {
      MasterCfg.Starters.Add(new MasterCfgEntry {
        Name= name,
        Description= description,
        Type= type,
        Properties= properties ?? ImmutableDictionary<string, object?>.Empty
      });
      return this;
    }

    /// <inheritdoc/>
    public IJobCntrlConfigurator DefineMasterJob(string name, string description, string type, IProps? properties= null) {
      MasterCfg.Jobs.Add(new MasterCfgEntry {
        Name= name,
        Description= description,
        Type= type,
        Properties= properties ?? ImmutableDictionary<string, object?>.Empty
      });
      return this;
    }

    /// <inheritdoc/>
    public IJobCntrlConfigurator DefineStarter(string name, string master, string description, IProps? properties= null) {
      this.ControlCfg.Starters.Add(new StarterCfg {
        Master= master,
        Name= name,
        Description= description,
        Properties= properties ?? ImmutableDictionary<string, object?>.Empty
      });
      return this;
    }

    /// <inheritdoc/>
    public IJobCntrlConfigurator DefineJob(string name, string master, string starter, string description, IProps? properties= null) {
      this.ControlCfg.Jobs.Add(new JobCfg {
        Master= master,
        Name= name,
        Starter= starter,
        Description= description,
        Properties= properties ?? ImmutableDictionary<string, object?>.Empty
      });
      return this;
    }
  }
}