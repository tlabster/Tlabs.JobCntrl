using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Tlabs.JobCntrl.Model {
  using IProps= IReadOnlyDictionary<string, object?>;

  /// <summary>Interface of a JobControl model.</summary>
  public interface IModel : IDisposable {
    /// <summary>Model name.</summary>
    string Name { get; }

    /// <summary>Model description.</summary>
    string Description { get; }

    /// <summary>Model properties.</summary>
    IProps Properties { get; }
  }

  /// <summary>Interface of a JobControl model configuration template.</summary>
  public interface IModelCfg : IModel {
    /// <summary>Name of the MasterModel the runtime-instance of this configuration template is to be based on.</summary>
    string Master { get; }
  }
  /// <summary>Interface of a job model configuration template.</summary>
  public interface IJobCfg : IModelCfg {
    /// <summary>Name of the runtime-starter the runtime-job instance is associated with.</summary>
    string Starter { get; }
   }
  /// <summary>Abstract base model.</summary>
  public abstract class BaseModel : IModel {

    /// <summary>Default ctor.</summary>
    protected BaseModel() {
      this.Name= "?";
      this.Description= "";
      this.Properties= ConfigProperties.EMPTY;
    }

    /// <summary>Ctor from base properties.</summary>
    protected BaseModel(string name, string? description, IProps? properties) {
      InitBase(name, description, properties);
    }

    /// <summary>Base init.</summary>
    /// <remarks>Used to support implementing Job.Initialize()...</remarks>
    protected void InitBase(string name, string? description, IProps? properties) {
      ArgumentNullException.ThrowIfNull(name);
      this.Name= name;
      this.Description= description ?? "";
      this.Properties= properties ?? ConfigProperties.EMPTY;
    }

    /// <summary>Model name.</summary>
    public string Name { get; private set; }= null!; //initalized with InitBase()

    /// <summary>Model description.</summary>
    public string Description { get; private set; }= null!; //initalized with InitBase()

    /// <summary>Model properties.</summary>
    public IProps Properties { get; private set; }= null!; //initalized with InitBase()

    /// <summary>Return a property's string value or null if not existing or not a string.</summary>
    public string? PropertyString(string propKey) { return ConfigProperties.GetString(Properties, propKey); }

    /// <summary>Return a property's string value or <paramref name="defaultVal"/> if not existing or not a string.</summary>
    public string PropertyString(string propKey, string defaultVal) { return ConfigProperties.GetString(Properties, propKey, defaultVal) ?? defaultVal; }

    /// <summary>Return a property's integer value or <paramref name="defaultVal"/> if not existing or not convertible to int.</summary>
    public int PropertyInt(string propKey, int defaultVal) { return ConfigProperties.GetInt(Properties, propKey, defaultVal); }

    /// <summary>Return a property's boolean value or <paramref name="defaultVal"/> if not existing or not convertible to bool.</summary>
    public bool PropertyBool(string propKey, bool defaultVal) { return ConfigProperties.GetBool(Properties, propKey, defaultVal); }

    /// <summary>Return an enumeration member named by the property's string value or <paramref name="defaultEnum"/> if not existing.</summary>
    /// <exception cref="ArgumentException">if property's string value is not a named constant of the enumeration</exception>
    public object PropertyEnum(string propKey, Enum defaultEnum) {
      var valName= ConfigProperties.GetString(Properties, propKey);
      if (string.IsNullOrEmpty(valName)) return defaultEnum;
      return Enum.Parse(defaultEnum.GetType(), valName, true);  //ignore case
    }

    /// <summary>Dispose</summary>
    public void Dispose() {
      Dispose(true);
      GC.SuppressFinalize(this);
    }

    /// <summary>dispose when <param name="disposing"/> == true.</summary>
    protected abstract void Dispose(bool disposing);

  }

}
