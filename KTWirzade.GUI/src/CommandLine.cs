using Core;
using Interprocess;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Internal;
using System.Reflection;
using System.Runtime.Serialization;
using static Core.Log;
using static Interprocess.InterLink;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;

namespace KTWirzade.GUI
{
    public static class CommandLine
    {
        public class Interprocess : IArgumentData
        {
            public class NodeData
            {
                public InternalLevel Level { get; set; }

                public int ProcessID { get; set; }
            }

            [Required]
            [DefaultArgument]
            public Level Level { get; set; }

            [Required]
            public Mode Mode { get; set; }

            public NodeData[] Nodes { get; set; }

            public int Host { get; set; } = -1;

            // Session identity propagated by the root node: random per-run pipe namespace
            // suffix and the session-owner SID used for the restricted pipe DACL.
            public string Secret { get; set; } = null;

            public string OwnerSid { get; set; } = null;
        }

        public class Execute : IArgumentData
        {
            public enum CommandType
            {
                [RequiresArgumentData("RunData")]
                Run,
                Delete
            }

            public class Run : IArgumentData
            {
                [Required]
                [DefaultArgument]
                public string File { get; set; }
            }

            [Required]
            [DefaultArgument]
            public CommandType Command { get; set; }

            [RequiredArgumentData]
            public Run RunData { get; set; }
        }

        public interface IArgumentData
        {
        }

        [AttributeUsage(AttributeTargets.Field)]
        public class RequiresArgumentDataAttribute : Attribute
        {
            public string RequiredProperty { get; set; }

            public RequiresArgumentDataAttribute(string requiredProperty)
            {
                RequiredProperty = requiredProperty;
            }
        }

        public class RequiredArgumentDataAttribute : Attribute
        {
        }

        public class DefaultArgumentAttribute : Attribute
        {
        }

        public class ArgumentDictionary<TKey, TValue>
        {
            private Dictionary<TKey, TValue> _dictionary = new Dictionary<TKey, TValue>();

            private List<TKey> _index = new List<TKey>();

            public TValue this[TKey key]
            {
                get
                {
                    return _dictionary[key];
                }
                private set
                {
                    _dictionary[key] = value;
                }
            }

            public void Add(TKey key, TValue value)
            {
                _dictionary.Add(key, value);
                _index.Add(key);
            }

            public bool TryGetValueAfterIndex(int index, TKey key, out TValue value)
            {
                if (!_dictionary.TryGetValue(key, out value))
                {
                    return false;
                }
                if (_index.IndexOf(key) > index)
                {
                    return true;
                }
                throw new SerializationException($"Argument '--{key}' must come after '--{_index[index]}'.");
            }

            public int GetIndex(TKey key)
            {
                return _index.IndexOf(key);
            }
        }

        public static string SerializeArgument(object value)
        {
            Type propertyType = value.GetType();
            if (ReflectionHelper.IsValueTuple(propertyType))
            {
                FieldInfo[] fields = propertyType.GetFields();
                List<string> serializedProperties = new List<string>();
                FieldInfo[] array = fields;
                foreach (FieldInfo property in array)
                {
                    object propertyValue = property.GetValue(value);
                    serializedProperties.Add(property.Name + "=" + propertyValue?.ToString().Replace(":", "::"));
                }
                return string.Join(":", serializedProperties);
            }
            throw new ArgumentException("Expected a ValueTuple", "value");
        }

        public static IArgumentData ParseArguments()
        {
            return ParseArguments(Environment.GetCommandLineArgs().Skip(1).ToArray());
        }

        public static IArgumentData ParseArguments(string[] args)
        {
            if (args.Length == 0)
            {
                return null;
            }
            Type[] dataClasses = (from x in typeof(CommandLine).GetNestedTypes()
                                  where x.GetInterfaces().Contains(typeof(IArgumentData))
                                  select x).ToArray();
            Type dataClass = dataClasses.FirstOrDefault((Type x) => x.Name.Equals(args[0] ?? ""));
            if (dataClass == null)
            {
                throw new SerializationException("First argument must be one of the following: \r\n" + string.Join(Environment.NewLine, dataClasses.Select((Type x) => x.Name)));
            }
            List<string> args2 = args.Skip(1).ToList();
            IArgumentData data = (IArgumentData)Activator.CreateInstance(dataClass);
            return DeserializeArguments(args2, data);
        }

        private static IArgumentData DeserializeArguments(List<string> args, IArgumentData result)
        {
            //IL_02de: Unknown result type (might be due to invalid IL or missing references)
            PropertyInfo[] properties = result.GetType().GetProperties();
            PropertyInfo defaultProperty = null;
            if (args.Count > 0 && !args[0].StartsWith("--"))
            {
                defaultProperty = properties.FirstOrDefault((PropertyInfo x) => x.GetCustomAttribute(typeof(DefaultArgumentAttribute)) != null);
                if (defaultProperty == null)
                {
                    throw new SerializationException("Unexpected argument '" + args[0] + "'.");
                }
            }
            List<string> propertiesParsed = new List<string>();
            bool passedArgument = true;
            int i;
            for (i = 0; i < args.Count; i++)
            {
                if (!args[i].StartsWith("--"))
                {
                    if (i != 0 || !(defaultProperty != null))
                    {
                        if (passedArgument)
                        {
                            throw new SerializationException("Expected an argument starting with '--', instead got '" + args[i] + "'. Make sure to quote any arguments that contain spaces or special characters.");
                        }
                        passedArgument = true;
                        continue;
                    }
                    i--;
                }
                passedArgument = false;
                PropertyInfo property = defaultProperty ?? properties.FirstOrDefault((PropertyInfo x) => x.Name.Equals(args[i].Substring(2), StringComparison.OrdinalIgnoreCase) && x.GetCustomAttribute(typeof(RequiredArgumentDataAttribute)) == null);
                if (property == null)
                {
                    throw new SerializationException("Unrecognized argument '" + args[i] + "'.");
                }
                if (propertiesParsed.Contains(property.Name))
                {
                    throw new SerializationException("Duplicate argument '" + args[i] + "'.");
                }
                defaultProperty = null;
                propertiesParsed.Add(property.Name);
                if (property.PropertyType == typeof(bool) && (args.Count - 1 == i || args[i + 1].StartsWith("--")))
                {
                    DeserializeArgument(property.PropertyType, "true", result);
                    continue;
                }
                if (args.Count - 1 == i)
                {
                    throw new SerializationException("An empty value is not valid for '--" + property.Name + "'.");
                }
                if (property.PropertyType.IsEnum)
                {
                    TypeConverter converter = TypeDescriptor.GetConverter(property.PropertyType);
                    object enumValue = Wrap.ExecuteSafe<object>((Func<object>)(() => converter.ConvertFromString(args[i + 1])), false, (LogOptions)null).Value;
                    if (enumValue == null)
                    {
                        throw new SerializationException("Argument '" + args[i + 1] + "' must be one of the following: \r\n" + string.Join(Environment.NewLine, Enum.GetNames(property.PropertyType)));
                    }
                    property.SetValue(result, enumValue);
                    if (!EnumValueHasAttribute(property.PropertyType, enumValue, typeof(RequiresArgumentDataAttribute)))
                    {
                        continue;
                    }
                    RequiresArgumentDataAttribute attribute = (RequiresArgumentDataAttribute)Attribute.GetCustomAttribute(property.PropertyType.GetField(enumValue.ToString()), typeof(RequiresArgumentDataAttribute));
                    PropertyInfo requiredDataProperty = properties.FirstOrDefault((PropertyInfo x) => x.Name == attribute.RequiredProperty);
                    if (requiredDataProperty == null)
                    {
                        throw new SerializationException("Required property '" + attribute.RequiredProperty + "' not found in class '" + result.GetType().Name + "'.");
                    }
                    if (!typeof(IArgumentData).IsAssignableFrom(requiredDataProperty.PropertyType))
                    {
                        throw new SerializationException("Required property '" + attribute.RequiredProperty + "' type does not implement the 'IArgumentData' interface.");
                    }
                    IArgumentData requiredData = (IArgumentData)Activator.CreateInstance(requiredDataProperty.PropertyType);
                    DeserializeArguments(args.Skip(args.FindIndex((string x) => (object)x == args[i + 1]) + 1).ToList(), requiredData);
                    requiredDataProperty.SetValue(result, requiredData);
                    break;
                }
                property.SetValue(result, DeserializeArgument(property.PropertyType, args[i + 1], result));
            }
            PropertyInfo requiredProperty = properties.FirstOrDefault((PropertyInfo x) => x.GetCustomAttribute(typeof(RequiredAttribute)) != null && !propertiesParsed.Contains(x.Name));
            if (requiredProperty != null)
            {
                throw new SerializationException("Missing required argument '--" + requiredProperty.Name + "'");
            }
            return result;
        }

        private static object DeserializeArgument(Type propertyType, string value, IArgumentData data)
        {
            if (propertyType.IsArray)
            {
                Type itemType = propertyType.GetElementType();
                if (itemType.IsArray)
                {
                    throw new SerializationException("Arrays of arrays are not supported.");
                }
                string[] array = value.Split(',');
                List<object> items = new List<object>();
                string[] array2 = array;
                foreach (string itemValue in array2)
                {
                    items.Add(DeserializeArgument(itemType, itemValue, data));
                }
                Array itemsOfType = Array.CreateInstance(itemType, items.Count);
                for (int j = 0; j < items.Count; j++)
                {
                    itemsOfType.SetValue(Convert.ChangeType(items[j], itemType), j);
                }
                return itemsOfType;
            }
            if (propertyType == typeof(string))
            {
                return value;
            }
            if (propertyType == typeof(bool))
            {
                if (!bool.TryParse(value, out var boolValue))
                {
                    throw new SerializationException("Expected 'true' or 'false' for '--" + propertyType.Name + "'.");
                }
                return boolValue;
            }
            if (propertyType == typeof(int))
            {
                if (!int.TryParse(value, out var intValue))
                {
                    throw new SerializationException("Expected a number for '--" + propertyType.Name + "'.");
                }
                return intValue;
            }
            if (propertyType == typeof(long))
            {
                if (!long.TryParse(value, out var longValue))
                {
                    throw new SerializationException("Expected a number for '--" + propertyType.Name + "'.");
                }
                return longValue;
            }
            if (propertyType.IsClass)
            {
                object instance = Activator.CreateInstance(propertyType);
                string[] array2 = value.Split(':');
                for (int i = 0; i < array2.Length; i++)
                {
                    string[] parts = array2[i].Split('=');
                    string propName = parts[0].Trim();
                    string propValue = ((parts.Length > 1) ? parts[1].Trim().Replace("::", ":") : null);
                    if (!string.IsNullOrWhiteSpace(propValue))
                    {
                        PropertyInfo property = propertyType.GetProperty(propName, BindingFlags.Instance | BindingFlags.Public);
                        if (property != null)
                        {
                            object convertedValue = null;
                            convertedValue = ((!property.PropertyType.IsEnum) ? Convert.ChangeType(propValue, property.PropertyType) : Enum.Parse(property.PropertyType, propValue));
                            property.SetValue(instance, convertedValue);
                        }
                    }
                }
                return instance;
            }
            throw new SerializationException("Unexpected type '" + propertyType.Name + "' of property in class '" + data.GetType().Name + "'.");
        }

        public static bool EnumValueHasAttribute(Type enumType, object enumValue, Type attributeType)
        {
            return Attribute.GetCustomAttribute(enumType.GetField(enumValue.ToString()), attributeType) != null;
        }
    }
}
