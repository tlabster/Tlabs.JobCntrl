using System;
using System.Collections.Generic;

namespace Tlabs.JobCntrl {
  using static Tlabs.Misc.Safe;

  /// <summary>Dictionary of configuration properties.</summary>
  /// <remarks>Uses a <see cref="StringComparer.OrdinalIgnoreCase"/> for the property names.</remarks>
  public class ConfigProperties :  AbstractErrorHandledNamedValues<object> {
    /// <summary>Empty (read-only) config properties.</summary>
    public static readonly IReadOnlyDictionary<string, object> EMPTY= new ConfigProperties().AsReadOnly();

    /// <summary>Return a property's string value or <paramref name="defaultVal"/> if not existing or not a string.</summary>
    public static string GetString(IReadOnlyDictionary<string, object> properties, string propKey, string defaultVal) {
      object val;
      string retStr;
      if (properties.TryGetValue(propKey, out val))
        return string.IsNullOrEmpty(retStr= val as string) ? defaultVal : retStr;
      return defaultVal;
    }

    /// <summary>Return a property's string value or null if not existing or not a string.</summary>
    public static string GetString(IReadOnlyDictionary<string, object> properties, string propKey) {
      return ConfigProperties.GetString(properties, propKey, null);
    }

    /// <summary>Return a property's integer value or <paramref name="defaultVal"/> if not existing or not convertible to int.</summary>
    public static int GetInt(IReadOnlyDictionary<string, object> properties, string propKey, int defaultVal) {
      object val;
      if (properties.TryGetValue(propKey, out val)) try {
          return ((IConvertible)val).ToInt32(System.Globalization.NumberFormatInfo.InvariantInfo);
        }
        catch (Exception e) when (NoDisastrousCondition(e)) { /* return defaultVal */ } 
      return defaultVal;
    }

    /// <summary>Return a property's boolean value or <paramref name="defaultVal"/> if not existing or not convertible to bool.</summary>
    public static bool GetBool(IReadOnlyDictionary<string, object> properties, string propKey, bool defaultVal) {
      object val;
      if (properties.TryGetValue(propKey, out val)) try {
          return ((IConvertible)val).ToBoolean(System.Globalization.NumberFormatInfo.InvariantInfo);
        }
        catch (Exception e) when (NoDisastrousCondition(e)) { /* return defaultVal */ } 
      return defaultVal;
    }

    /// <summary>Return a property's value or <paramref name="defaultVal"/> if not existing - in that case is also set as new properties value.</summary>
    public static object GetOrSet(IDictionary<string, object> properties, string propKey, object defaultVal) {
      object val;
      if (!properties.TryGetValue(propKey, out val))
        properties[propKey]= (val= defaultVal);
      return val;
    }

    /// <summary>Return a writable version of the dictionary.</summary>
    public static IDictionary<string, object> Writeable(IReadOnlyDictionary<string, object> props) {
      if ( null == props) new ConfigProperties();
      var dictProp= props as IDictionary<string, object>;
      return null == dictProp || dictProp.IsReadOnly ? new ConfigProperties(props) : dictProp;
    }

    /// <summary>Tries to resolve a value from (optionaly) nested properties dictionaries.</summary>
    /// <remarks>
    /// Assuming a <paramref name="propKeyPath"/> of <c>"p1.p2.p3"</c>. This would be (tried)
    /// to be resolved like: <c>properties["p1"]["p2"]["p3"]</c>,
    /// <para>
    /// The first key token of the <paramref name="propKeyPath"/> that is associated with a non dictionary value (or the last)
    /// is returned in <paramref name="resolvedKey"/>
    /// </para>
    /// </remarks>
    /// <param name="properties">(optionaly nested) properties dictionary</param>
    /// <param name="propKeyPath">properties key path (using '.' as path delimiter)</param>
    /// <param name="val">resolved value</param>
    /// <param name="resolvedKey">resolved key</param>
    /// <returns>true if a value could be resolved using the <paramref name="propKeyPath"/></returns>
    public static bool TryResolveValue(IReadOnlyDictionary<string, object> properties, string propKeyPath, out object val, out string resolvedKey) {
      val= resolvedKey= null;
      var valDict= properties;
      var keyToks= propKeyPath.Split('.');

      foreach(string ktok in keyToks) {
        resolvedKey= ktok;
        if (!valDict.TryGetValue(ktok, out val)) return false;
        valDict= val as IReadOnlyDictionary<string, object>;
        if (null == valDict) break;   //no more dictionaries to resolve
      }
      return true;
    }

    /// <summary>Resolved property value.</summary>
    /// <param name="properties">A properties dictionary</param>
    /// <param name="propSpecifier">A property spcifier. If it is a name or property path enclosed in brackets like
    /// '[name.subKey]', the contents of the bracket are tried to be resolved with
    /// <see cref="TryResolveValue(IReadOnlyDictionary{string, object}, string , out object, out string)"/></param>
    /// <returns>The resolved property value given by the <paramref name="propSpecifier"/> or if could not be resolved as a property, the
    /// <paramref name="propSpecifier"/> it self.</returns>
    public static object ResolvedProperty(IReadOnlyDictionary<string, object> properties, string propSpecifier) {
      string dummy;
      object o,
             propVal= propSpecifier;  //default return
      if (   null == propSpecifier
          || propSpecifier.Length < 3
          || '[' != propSpecifier[0]
          || ']' != propSpecifier[propSpecifier.Length-1]) return propVal;

      if (TryResolveValue(properties, propSpecifier.Substring(1, propSpecifier.Length-2), out o, out dummy))
        propVal= o;

      return propVal;
    }

    /// <summary>Set <paramref name="val"/> to resolved (optionaly) nested properties dictionary.</summary>
    /// <remarks>
    /// Assuming a <paramref name="propKeyPath"/> of <c>"p1.p2.p3"</c>. This would be (tried)
    /// to be resolved like: <c>properties["p1"]["p2"]["p3"] = val</c>,
    /// <para>
    /// Every but the last tokens of the <paramref name="propKeyPath"/> that could not be resolved into a dictionary value
    /// gets created as a new dictionary, if not already existing. If it exists, but is not of dictionary type - false is returned.
    /// </para>
    /// </remarks>
    /// <param name="properties">(optionaly nested) properties dictionary</param>
    /// <param name="propKeyPath">properties key path (using '.' as path delimiter)</param>
    /// <param name="val">value to be set</param>
    /// <param name="resolvedKey">resolved key (last token of the path on success)</param>
    /// <returns>true if  value could be set</returns>
    public static bool SetResolvedValue(IDictionary<string, object> properties, string propKeyPath, object val, out string resolvedKey) {
      resolvedKey= null;
      var valDict= properties;
      var keyToks= propKeyPath.Split('.');
      IDictionary<string, object> dict;
      int l= 0;
      foreach (string ktok in keyToks) {
        object o;
        if(++l == keyToks.Length) {
          valDict[resolvedKey= ktok]= val;
          return true;
        }
        if (!valDict.TryGetValue((resolvedKey= ktok), out o)) {
          valDict[ktok]= dict= new NamedValues<object>();
          valDict= dict;
          continue;
        }
        if (null == (dict= o as IDictionary<string, object>)) return false;
        valDict= dict;
      }
      return true;
    }

    /// <summary>Default ctor</summary>
    public ConfigProperties() { this.dict= new NamedValues<object>(); }

    /// <summary>Ctor from <paramref name="capacity"/></summary>
    public ConfigProperties(int capacity) { this.dict= new NamedValues<object>(capacity); }

    /// <summary>Ctor to intialize from another properties dictionary.</summary>
    public ConfigProperties(IEnumerable<KeyValuePair<string, object>> other) { this.dict= new NamedValues<object>(other); }

    /// <summary>Ctor to intialize from another properties dictionary and overriding properties.</summary>
    public ConfigProperties(IEnumerable<KeyValuePair<string, object>> other, IEnumerable<KeyValuePair<string, object>> overridingProps) : this(other) {
      if (null == overridingProps) return;
      foreach (var pair in overridingProps)
        this[pair.Key]= pair.Value;
    }

    /// <summary>Custom KeyNotFound handler</summary>
    protected override object HandleKeyNotFound(string key) {
      throw new AppConfigException(string.Format("Undefined configuration property: '{0}'.", key));
    }

    /// <summary>Custom DuplicateKey handler</summary>
    protected override void HandleDuplicateKey(string key, object newValue) {
      throw new AppConfigException(string.Format("Error adding duplicate configuration property: '{0}'.", key));
    }
  }//class ConfigProperties

}
