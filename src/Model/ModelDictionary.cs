using System.Collections.Generic;

namespace Tlabs.JobCntrl.Model {

  /// <summary>Dictionary of <see cref="Model.IModel"/> objects with type <typeparam name="T"/>.</summary>
  /// <remarks>This <see cref="IDictionary{K, T}"/> implementation provides specialized error handling of the
  /// 'key not found' and 'duplicate key' conditions.</remarks>
  public class ModelDictionary<T> : AbstractErrorHandledNamedValues<T> where T : Model.IModel {

    /// <summary>Default ctor.</summary>
    public ModelDictionary() { this.dict= new NamedValues<T>(); }

    /// <summary>Ctor from <paramref name="capacity"/>.</summary>
    public ModelDictionary(int capacity) { this.dict= new NamedValues<T>(capacity); }

    /// <summary>Ctor from another <see cref="IDictionary{K, T}"/>.</summary>
    public ModelDictionary(IDictionary<string, T> other) { this.dict= other; }


    /// <summary>Custom KeyNotFound handler</summary>
    protected override T HandleKeyNotFound(string key) {
      throw new JobCntrlException(string.Format("Undefined {0}: '{1}'.", typeof(T).Name, key));
    }

    /// <summary>Custom DuplicateKey handler</summary>
    protected override void HandleDuplicateKey(string key, T newValue) {
      throw new JobCntrlException(string.Format("Error loading {0} with duplicate ID '{1}'.", typeof(T).Name, key));
    }
  }

}