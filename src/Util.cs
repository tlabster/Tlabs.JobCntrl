using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Tlabs.JobCntrl {

  /// <summary>Dictionary extension methods.</summary>
  public static class DictionaryExtension {

    /// <summary>Retrun a read-only version of a dictionary.</summary>
    public static IReadOnlyDictionary<K, T> AsReadOnly<K, T>(this IDictionary<K, T> dict) {
      if (null == dict) throw new ArgumentNullException(nameof(dict));
      if (   dict.IsReadOnly
          && dict is IReadOnlyDictionary<K, T> rod) return rod;
      return new ReadOnlyDictionary<K, T>(dict);
    }

    /// <summary>Set a range of <paramref name="items"/> in the dictionary.</summary>
    public static void SetRange<K, T>(this IDictionary<K, T> dict, IEnumerable<KeyValuePair<K, T>> items) {
      if (null == items) return;
      foreach (var item in items)
        dict[item.Key]= item.Value;
    }
  }

}