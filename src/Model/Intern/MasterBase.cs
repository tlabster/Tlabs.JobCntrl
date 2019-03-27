using System;
using System.Reflection;
using System.Collections.Generic;

namespace Tlabs.JobCntrl.Model.Intern {

  /// <summary>A master model's base class.</summary>
  /// <typeparam name="T">Target instance base type</typeparam>
  public abstract class MasterBase<T> : BaseModel {
    /// <summary>Ctor of the target instance.</summary>
    protected ConstructorInfo typeCtor;

    /// <summary>Internal ctor of a master model.</summary>
    /// <param name="name">unique master name</param>
    /// <param name="description">description string</param>
    /// <param name="templType">Target instance type (must be assignable from <typeparamref name="T"/>)</param>
    /// <param name="properties">Master's configuration parameters</param>
    protected MasterBase(string name, string description, Type templType, IReadOnlyDictionary<string, object> properties)
      : base(name, description, properties) {
      if (null == templType) throw new ArgumentNullException("templType");
      var targetType= typeof(T);
      if (!targetType.IsAssignableFrom(templType)) throw new ArgumentException(string.Format("Invalid {0} type: {1}", targetType.Name, templType.AssemblyQualifiedName));
      if (null == (this.typeCtor= templType.GetConstructor(Type.EmptyTypes))) throw new ArgumentException("No public default ctor in master type: " + templType.AssemblyQualifiedName);
    }

  }

}
