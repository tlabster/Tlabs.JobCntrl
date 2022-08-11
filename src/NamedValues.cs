using System;
using System.Collections.Generic;

namespace Tlabs.JobCntrl {

  /// <summary>
  /// Class that provides a dictionary of values with type <typeparamref name="T"/>, that are keyed by
  /// names (strings).
  /// </summary>
  /// <remarks>The keys (names) of this dictionary are compared case insensitive.
  /// i.e. the two keys 'name' and 'nAMe' will return the same value...</remarks>
  /// <typeparam name="T">Type parameter for the contained named values</typeparam>
  public class NamedValues<T> : Dictionary<string, T> {
    /// <summary>Default capacity if not specified per ctor.</summary>
    public const int DEFAULT_CAPACITY= 16;

    /// <summary>Default ctor.</summary>
    public NamedValues() : base(StringComparer.OrdinalIgnoreCase) { }

    /// <summary>Create a new dictionary with the initial <paramref name="capacity"/>.</summary>
    public NamedValues(int capacity) : base(capacity, StringComparer.OrdinalIgnoreCase) { }

    /// <summary>Create a new dictionary with the contents of optional <paramref name="other"/>.</summary>
    public NamedValues(IEnumerable<KeyValuePair<string, T>> other) : base(DEFAULT_CAPACITY, StringComparer.OrdinalIgnoreCase) {
      this.SetRange(other);
    }

    /// <summary>Retuns a read-only version of this dctionary.</summary>
    public IReadOnlyDictionary<string, T> AsReadonly() => new System.Collections.ObjectModel.ReadOnlyDictionary<string, T>(this);
  }

  /// <summary>Dictionary of <see cref="NamedValues{T}"/> that provides abstracts method to handle errors.</summary>
  /// <typeparam name="T">Type parameter for the contained named values</typeparam>
  public abstract class AbstractErrorHandledNamedValues<T> : IDictionary<string, T>, IReadOnlyDictionary<string, T> {
    /// <summary>Internal dictionary.</summary>
    protected IDictionary<string, T> dict;

    /// <summary>Error handler method called on key-not-found errors.</summary>
    /// <remarks>Implementation could return a special value or even throw their own exception.</remarks>
    protected abstract T HandleKeyNotFound(string key);

    /// <summary>Error handler method called on an attempt to add a duplicate key.</summary>
    /// <remarks>Implementation could set the <paramref name="newValue"/> to the existing key or even throw their own exception.</remarks>
    protected abstract void HandleDuplicateKey(string key, T newValue);

    /// <inheritdoc/>
    public void Add(string key, T value) {
      try { dict.Add(key, value); }
      catch (ArgumentException) {
        if (null == key) throw;
        HandleDuplicateKey(key, value);
      }
    }

    /// <summary>Retuns a read-only version of this dctionary.</summary>
    public IReadOnlyDictionary<string, T> AsReadonly() => new System.Collections.ObjectModel.ReadOnlyDictionary<string, T>(dict);

    /// <inheritdoc/>
    public bool ContainsKey(string key) { return dict.ContainsKey(key); }

    /// <inheritdoc/>
    public ICollection<string> Keys { get { return dict.Keys; } }

    /// <inheritdoc/>
    public bool Remove(string key) { return dict.Remove(key); }

    /// <inheritdoc/>
    public bool TryGetValue(string key, out T value) { return dict.TryGetValue(key, out value); }

    /// <inheritdoc/>
    public ICollection<T> Values { get { return dict.Values; } }

    /// <inheritdoc/>
    public T this[string key] {
      get {
        try { return dict[key]; }
        catch (KeyNotFoundException) {
          return HandleKeyNotFound(key);
        }
      }
      set { dict[key]= value; }
    }

    /// <inheritdoc/>
    void ICollection<KeyValuePair<string, T>>.Add(KeyValuePair<string, T> item) {
      try { ((ICollection<KeyValuePair<string, T>>)dict).Add(item); }
      catch (ArgumentException) {
        if (null == item.Key) throw;
        HandleDuplicateKey(item.Key, item.Value);
      }
    }

    /// <inheritdoc/>
    public void Clear() { dict.Clear(); }

    /// <inheritdoc/>
    public bool Contains(KeyValuePair<string, T> item) {
      return ((ICollection<KeyValuePair<string, T>>)dict).Contains(item);
    }

    /// <inheritdoc/>
    public void CopyTo(KeyValuePair<string, T>[] array, int arrayIndex) {
      ((ICollection<KeyValuePair<string, T>>)dict).CopyTo(array, arrayIndex);
    }

    /// <inheritdoc/>
    public int Count { get { return dict.Count; } }

    /// <inheritdoc/>
    public bool IsReadOnly {
      get { return ((ICollection<KeyValuePair<string, T>>)dict).IsReadOnly; }
    }

    /// <inheritdoc/>
    IEnumerable<string> IReadOnlyDictionary<string, T>.Keys => dict.Keys;

    /// <inheritdoc/>
    IEnumerable<T> IReadOnlyDictionary<string, T>.Values => dict.Values;

    /// <inheritdoc/>
    public bool Remove(KeyValuePair<string, T> item) {
      return ((ICollection<KeyValuePair<string, T>>)dict).Remove(item);
    }

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<string, T>> GetEnumerator() { return dict.GetEnumerator(); }

    /// <inheritdoc/>
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { return dict.GetEnumerator(); }

  }

}

