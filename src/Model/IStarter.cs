using System;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;

namespace Tlabs.JobCntrl.Model {
  using IProps= IReadOnlyDictionary<string, object>;

  /// <summary>Delegate invoked on <see cref="IStarter.Activate"/> event.</summary>
  /// <param name="starter">Activated target Starter</param>
  /// <param name="properties">Job run properties</param>
  /// <returns>true if any jobs have been actually started.</returns>
  public delegate bool StarterActivator(IStarter starter, IProps properties);

  /// <summary>JobControl Starter to start the run of an Job.</summary>
  public interface IStarter : IModel, IDisposable {

    /// <summary>Event to be registered by Job(s)</summary>
    event StarterActivator Activate;

    /// <summary>Starter enable status</summary>
    bool Enabled { get; set; }

    /// <summary>Initialize a Starter</summary>
    /// <param name="name">Unique Starter name/ID</param>
    /// <param name="description">Starter description</param>
    /// <param name="properties">Properties dictionary</param>
    /// <returns>Should return this.(Utility to create instances like: <c>var triggger= new Starter().Initialize(name, description, properties);</c>)</returns>
    /// <remarks>Implementations should initialize a Starter as 'disabled' (i.e. <c>IStarter.enabled == false</c>)
    /// in order to let the runtime enable Starters after configuration loading.</remarks>
    IStarter Initialize(string name, string description, IProps properties);

    /// <summary>Manually activate the Starter.</summary>
    /// <param name="activationProps">Activation properties to be passed to the Job(s) being started.</param>
    /// <returns>true if actually at least one jobs has been activated, false if no job at all was activated (because the starter was disabled or no jobs are configured...</returns>
    bool DoActivate(IProps activationProps);
  }

  /// <summary>Starter base class.</summary>
  /// <remarks>Convenients base class for implementators of the <see cref="IStarter"/> interface.</remarks>
  public abstract class BaseStarter : BaseModel, IStarter {
    /// <summary>Prefix for configuration properties that are to be copied as run/starter properties (with prefix stripped off).</summary>
    public const string RUN_PROPERTY_PREFIX= "RUN-PROP-";

    /// <summary>Enambled state</summary>
    #pragma warning disable CA1805  //start as disabled - to be enabled by the runtime later on...
    protected bool isEnabled= false;      

    /// <summary>Event to be registered by Job(s)</summary>
    public event StarterActivator Activate;

    ///<inheritdoc/>
    public IStarter Initialize(string name, string description, IProps properties) {
      InitBase(name, description, properties);
      return InternalInit();
    }

    ///<inheritdoc/>
    public virtual bool DoActivate(IProps invocationProps) {
      var activateEvent= Activate;
      if (!Enabled || null == activateEvent) return false;
      return activateEvent.Invoke(this, invocationProps);
    }

    ///<inheritdoc/>
    public bool Enabled {
      get { return isEnabled; }
      set { if (isEnabled != value) ChangeEnabledState(value); }
    }

    /// <summary>Internal Starter initalization.</summary>
    /// <remarks>Called from <c>Initialize()</c> after name, description and properties have been set.</remarks>
    protected abstract IStarter InternalInit();

    /// <summary>Internal enabled state change.</summary>
    /// <remarks>Called from <see cref="BaseStarter.Enabled"/> setter (hence must not call the enabled setter to set new state!).</remarks>
    protected abstract void ChangeEnabledState(bool enabled);

    /// <summary>Internal Dispose.</summary>
    /// <remarks>Managed resources should only be disposed when <paramref name="disposing"/> == true.</remarks>
    protected override void Dispose(bool disposing) {
      if (disposing) Enabled= false;
    }
  }
}
