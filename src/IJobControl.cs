using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

using Tlabs.JobCntrl.Model.Intern;
using Tlabs.JobCntrl.Config;

namespace Tlabs.JobCntrl {
  using IMasterStarterMap= IReadOnlyDictionary<string, MasterStarter>;
  using IMasterJobMap= IReadOnlyDictionary<string, MasterJob>;
  using IRuntimeCfg= IEnumerable<Model.IModelCfg>;
  using IJobCfg= IEnumerable<Model.IJobCfg>;
  using IStarterMap= IReadOnlyDictionary<string, Model.IStarter>;
  using IJobMap= IReadOnlyDictionary<string, Model.IJob>;
  using IProps= IReadOnlyDictionary<string, object>;

  /// <summary>Interface of a JobControl.</summary>
  public interface IJobControl : IDisposable {

    /// <summary>Returns the <see cref="IJobControlCfgLoader"/> used by the JobControl to load it's configuration.</summary>
    IJobControlCfgLoader ConfigLoader { get; }

    /// <summary>Returns the <see cref="IStarterCompletionPersister"/> used to persist activator completion data.</summary>
    IStarterCompletionPersister CompletionPersister { get; }

    /// <summary>Master model's configuration.</summary>
    IMasterCfg MasterModels { get; }

    /// <summary>Dictionary of runtime <see cref="Model.IStarter"/>(s).</summary>
    IStarterMap Starters { get; } //Note: returning IRuntimeStarter might be better, but it's definitely internal!

    /// <summary>Dictionary of runtime <see cref="Model.IJob"/>(s).</summary>
    IJobMap Jobs { get; }

    /// <summary>Initializes the <see cref="IJobControl"/> runtime by loading its configuration.</summary>
    /// <exception cref="JobCntrlException">thrown when JobControl was already started</exception>
    void Init();

    /// <summary>Load and start activating configured starter(s) and job(s).</summary>
    /// <exception cref="JobCntrlException">thrown when JobControl was already started or not initialized.</exception>
    void Start();

    /// <summary>Stop executing any starter(s) and jobs(s) and discards any configuration.</summary>
    /// <remarks>It is required to invoke <see cref="IJobControl.Init()"/> before re-starting the runtime.</remarks>
    void Stop();

  }

  /// <summary>Service interface of a component class that persists <see cref="IStarterCompletion"/> data.</summary>
  public interface IStarterCompletionPersister {

    /// <summary>Event fired when a starter completion info has been persisted.</summary>
    event Action<IStarterCompletionPersister, IStarterCompletion, object> CompletionInfoPersisted;

    /// <summary>Returns a starters completion persistent info as a stream.</summary>
    /// <param name="starterName">starter instance name</param>
    /// <param name="contentType">returned streams MIME content type</param>
    /// <param name="encoding">returned streams text encoding</param>
    /// <returns>A binray data stream for reading that must be disposed after usage.</returns>
    System.IO.Stream GetLastCompletionInfo(string starterName, out string contentType, out Encoding encoding);

    /// <summary>Store starters completion info in a persistent storage.</summary>
    void StoreCompletionInfo(IStarterCompletion starterCompletion);
  }

  /// <summary>Interface of the master model configuration.</summary>
  public interface IMasterCfg {
    /// <summary>Dictionary of starter(s) of <see cref="Model.Intern.MasterStarter"/>(s).</summary>
    IMasterStarterMap Starters { get; }

    /// <summary>Dictionary of job(s) of <see cref="Model.Intern.MasterJob"/>(s).</summary>
    IMasterJobMap Jobs { get; }
  }

  /// <summary>Interface of the JobControl's configuration.</summary>
  public interface IJobControlCfg {

    /// <summary>Master model's configuration.</summary>
    IMasterCfg MasterModels { get; }

    /// <summary>Dictionary of runtime <see cref="Model.IStarter"/>(s).</summary>
    IRuntimeCfg Starters { get; } //Note: returning IRuntimeStarter might be better, but it's definitely internal!

    /// <summary>Dictionary of runtime <see cref="Model.IJob"/>(s).</summary>
    IJobCfg Jobs { get; }

  }

  /// <summary>Interface of a configuration loader.</summary>
  /// <remarks>A configuration loader loads the actual JobControl configuration from some persistent storage.</remarks>
  public interface IJobControlCfgLoader {

    /// <summary>Returns the <see cref="IJobControlCfgPersister"/> used to persist a JobControl's configuration.</summary>
    IJobControlCfgPersister ConfigPersister { get; }

    /// <summary>Loads the master model configuration.</summary>
    /// <returns>The <see cref="IMasterCfg"/> loaded.</returns>
    IMasterCfg LoadMasterConfiguration();

    /// <summary>Loads the JobControl's runtime model configuration based on a <paramref name="masterCfg"/>.</summary>
    /// <param name="masterCfg"><see cref="IMasterCfg"/> that the JobControl configuration is to be based on.</param>
    /// <returns>The <see cref="IJobControlCfg"/> loaded.</returns>
    IJobControlCfg LoadRuntimeConfiguration(IMasterCfg masterCfg);

  }


  /// <summary>Types of JobControl configurations.</summary>
  public enum JobControlCfgType {
    /// <summary>Master model configuration.</summary>
    MasterModels,
    /// <summary>Runtime Model configuration.</summary>
    RuntimeModel
  }

  /// <summary>JobControl config. loader prop interface.</summary>
  public interface IJobCntrlCfgLoaderProperties : IDictionary<string, string> { }

  /// <summary>JobControl configurator interface.</summary>
  public interface IJobCntrlConfigurator {
    /// <summary>JobControl configuration.</summary>
    JobCntrlCfg JobCntrlCfg { get; }

    /// <summary>Define a <see cref="MasterStarter"/>.</summary>
    IJobCntrlConfigurator DefineMasterStarter(string name, string description, string type, IProps properties= null);
    /// <summary>Define a <see cref="MasterJob"/>.</summary>
    IJobCntrlConfigurator DefineMasterJob(string name, string description, string type, IProps properties= null);
    /// <summary>Define a runtime starter.</summary>
    IJobCntrlConfigurator DefineStarter(string name, string master, string description, IProps properties= null);
    /// <summary>Define a runtime job.</summary>
    IJobCntrlConfigurator DefineJob(string name, string master, string starter, string description, IProps properties= null);
  }

  /// <summary>Interface of a JobControl's configuration persister.</summary>
  public interface IJobControlCfgPersister {

    /// <summary>Stores a stream of configuration data in an internal persistence store.</summary>
    /// <param name="configStream">Stream of config. data</param>
    /// <param name="type">Configuration type</param>
    /// <param name="moduleID">optional module ID of the config stream</param>
    /// <param name="valdidateConfig">
    /// Optional callback delegate that gets invoked after the <paramref name="configStream"/> has been stored.
    /// <para>
    /// Specify, a non null delegate that could validate the new configuration
    /// (using <see cref="IJobControlCfgPersister.OpenConfigStream(JobControlCfgType, string)">OpenConfigStream()</see>.
    /// Any exception thrown from this callback will cause the stored configuration to be replaced with its previous version.
    /// </para>
    /// </param>
    void StoreConfigStream(Stream configStream, JobControlCfgType type, string moduleID, Action valdidateConfig);

    /// <summary>Opens a stream of configuration data from the internal persistence store.</summary>
    /// <param name="type">Configuration type</param>
    /// <param name="moduleID">optional module ID of the config stream</param>
    /// <returns>An open stream that must be closed/disposed by the caller.</returns>
    Stream OpenConfigStream(JobControlCfgType type, string moduleID);
  }

}
