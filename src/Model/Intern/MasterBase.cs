using System;
using System.Reflection;
using System.Collections.Generic;

namespace Tlabs.JobCntrl.Model.Intern {

  /// <summary>A master model's base class.</summary>
  /// <typeparam name="T">Target instance base type</typeparam>
  public abstract class MasterBase<T> : BaseModel {
    /// <summary>Ctor of the target instance.</summary>
    protected Type targetType;

    /// <summary>Internal ctor of a master model.</summary>
    /// <param name="name">unique master name</param>
    /// <param name="description">description string</param>
    /// <param name="targetType">Target instance type (must be assignable from <typeparamref name="T"/>)</param>
    /// <param name="properties">Master's configuration parameters</param>
    protected MasterBase(string name, string description, Type targetType, IReadOnlyDictionary<string, object> properties) : base(name, description, properties) {
      if (null == (this.targetType= targetType)) throw new ArgumentNullException(nameof(targetType));
      if (!typeof(T).IsAssignableFrom(targetType)) throw new ArgumentException($"{typeof(T)} not assignable from {nameof(targetType)}: {targetType.Name}");
    }

  }

}
