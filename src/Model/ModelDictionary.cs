using System.Collections.Generic;

namespace Tlabs.JobCntrl.Model {

  /// <summary>Dictionary of <see cref="Model.IModel"/> objects with type <typeparam name="T"/>.</summary>
  /// <remarks>This <see cref="IDictionary{K, T}"/> implementation provides specialized error handling of the
  /// 'key not found' and 'duplicate key' conditions.</remarks>
  public class ModelDictionary<T> : AbstractErrorHandledNamedValues<T> where T : Model.IModel {

    /// <summary>Default ctor.</summary>
    public ModelDictionary() { }

    /// <summary>Ctor from <paramref name="capacity"/>.</summary>
    public ModelDictionary(int capacity) : base(capacity) { }

    /// <summary>Ctor from <paramref name="other"/>.</summary>
    public ModelDictionary(IEnumerable<KeyValuePair<string, T>> other) : base(other) { }


    /// <summary>Custom KeyNotFound handler</summary>
    protected override T HandleKeyNotFound(string key) {
      throw new JobCntrlException($"Undefined {typeof(T).Name}: '{key}'.");
    }

    /// <summary>Custom DuplicateKey handler</summary>
    protected override void HandleDuplicateKey(string key, T newValue) {
      throw new JobCntrlException($"Error loading {typeof(T).Name} with duplicate ID '{key}'.");
    }
  }

}