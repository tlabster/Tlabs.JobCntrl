using System.Collections.Generic;

namespace Tlabs.JobCntrl.Model.Intern.Starter {

  /// <summary>A simple starter to be activated by manual user interaction.</summary>
  /// <remarks>This starter does only activate on 'explicit' invocation of <see cref="IStarter.DoActivate(IReadOnlyDictionary{string, object})"/>.</remarks>
  public sealed class Manual : BaseStarter {

#region BaseStarter
    /// <summary>Internal init.</summary>
    protected override IStarter InternalInit() { return this; }

    /// <summary>Change enabled state</summary>
    protected override void ChangeEnabledState(bool enabled) { this.isEnabled= enabled; }

    /// <summary>Dispose</summary>
    protected override void Dispose(bool disposing) { base.Dispose(disposing); }
#endregion
  }
}
