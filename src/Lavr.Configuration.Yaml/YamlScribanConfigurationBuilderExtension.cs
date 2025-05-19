using System;
using System.Collections.Generic;
using System.IO;
using Scriban;
using Scriban.Runtime;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Microsoft.Extensions.Configuration;

namespace Lavr.Configuration
{

    public static class DictionaryExtensions
    {
        public static object GetDictValueByPath(this Dictionary<string, object> values, string path)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Path cannot be null or empty", nameof(path));

            object current = values;
            foreach (var key in path.Split('.'))
            {
                if (current is Dictionary<string, object> dict)
                {
                    if (!dict.TryGetValue(key, out current))
                        throw new KeyNotFoundException($"Key '{key}' not found in path '{path}'.");
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Expected a Dictionary<string, object> at '{key}', but found {current?.GetType().Name ?? "null"}");
                }
            }

            return current;
        }
     }

    /// <summary>
    /// Extension methods for loading YAML templates into <see cref="IConfigurationBuilder"/>.
    /// </summary>
    public static class YamlScribanConfigurationBuilderExtension
    {
        /// <summary>
        /// Renders a Scriban YAML template using a values YAML file and loads the result into configuration.
        /// </summary>
        /// <param name="builder">The configuration builder.</param>
        /// <param name="templateFilePath">Path to the Scriban YAML template.</param>
        /// <param name="valuesFilePath">Path to the YAML values file.</param>
        /// <param name="optional">Whether loading is optional if files are missing.</param>
        /// <param name="reloadOnChange">Whether to reload config when files change.</param>
        /// <returns>The configuration builder.</returns>
        public static IConfigurationBuilder AddYamlTemplateFile(
            this IConfigurationBuilder builder,
            string templateFilePath,
            string valuesFilePath = "values.yaml",
            bool optional = false,
            bool reloadOnChange = false)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (string.IsNullOrEmpty(templateFilePath)) throw new ArgumentException("Template file path must be provided", nameof(templateFilePath));

            void Load()
            {
                if (!optional && !File.Exists(templateFilePath))
                    throw new FileNotFoundException($"Template file not found: {templateFilePath}");
                if (!optional && !File.Exists(valuesFilePath))
                    throw new FileNotFoundException($"Values file not found: {valuesFilePath}");

                var templateText = File.ReadAllText(templateFilePath);
                var valuesYaml = File.ReadAllText(valuesFilePath);

                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();
                var values = deserializer.Deserialize<Dictionary<string, object>>(valuesYaml);
                // var dynamicValues = deserializer.Deserialize<dynamic>(valuesYaml);

                var scriptObject = new ScriptObject();
                Scriban.Runtime.ScriptObjectExtensions.Import(
                    scriptObject,
                    "to_yaml",
                    new Func<object, string>(obj =>
                    {
                        var serializer = new SerializerBuilder()
                            .WithNamingConvention(CamelCaseNamingConvention.Instance)
                            .Build();
                        return serializer.Serialize(obj);
                    })
                );

                Scriban.Runtime.ScriptObjectExtensions.Import(
                    scriptObject,
                    "PostgresConnection",
                    new Func<ScriptObject, string>(args =>
                    {
                        var database = args["database"]?.ToString() ?? throw new ArgumentException("Missing 'database'");
                        var path = args["path"]?.ToString() ?? "global.database.postgres01"; // TODO: не postgres01, а какое-то значение из конфига
                        var db = (Dictionary<string, object>)values.GetDictValueByPath(path);
                        var host = db["host"].ToString();
                        var port = db["port"].ToString(); // TODO: по-умолчанию 5432
                        return $"Server={host};Port={port};Database={database}";
                        // TODO: добавить больше опциональных параметров - таймауты, размер пула
                    })
                );


                var scribanTemplate = Template.Parse(templateText);

                scriptObject.Import(values);

                var context = new TemplateContext();
                context.PushGlobal(scriptObject);

                var rendered = scribanTemplate.Render(context);

                // Parse rendered YAML into key/value pairs
                var yamlDeserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();
                var document = yamlDeserializer.Deserialize<Dictionary<string, object>>(rendered);

                var data = new Dictionary<string, string>();
                Flatten(document, parentPath: null, data);

                builder.AddInMemoryCollection(data);
            }

            Load();

            if (reloadOnChange)
            {
                var dirTemplate = Path.GetDirectoryName(templateFilePath);
                var watcher1 = new FileSystemWatcher(dirTemplate)
                {
                    Filter = Path.GetFileName(templateFilePath),
                    NotifyFilter = NotifyFilters.LastWrite
                };
                watcher1.Changed += (s, e) => Load();
                watcher1.EnableRaisingEvents = true;

                var dirValues = Path.GetDirectoryName(valuesFilePath);
                var watcher2 = new FileSystemWatcher(dirValues)
                {
                    Filter = Path.GetFileName(valuesFilePath),
                    NotifyFilter = NotifyFilters.LastWrite
                };
                watcher2.Changed += (s, e) => Load();
                watcher2.EnableRaisingEvents = true;
            }

            return builder;
        }

        // Recursively flattens the YAML structure into key paths and string values
        private static void Flatten(
            IDictionary<string, object> dict,
            string parentPath,
            IDictionary<string, string> result)
        {
            foreach (var kvp in dict)
            {
                var key = parentPath == null ? kvp.Key : $"{parentPath}:{kvp.Key}";
                if (kvp.Value is IDictionary<object, object> nested)
                {
                    // Convert object->object to string-keyed dict
                    var nestedDict = new Dictionary<string, object>();
                    foreach (var inner in nested)
                        nestedDict[inner.Key.ToString()] = inner.Value;
                    Flatten(nestedDict, key, result);
                }
                else if (kvp.Value is IList<object> list)
                {
                    for (var i = 0; i < list.Count; i++)
                    {
                        var item = list[i];
                        if (item is IDictionary<object, object> nestedItem)
                        {
                            var nestedDict = new Dictionary<string, object>();
                            foreach (var inner in nestedItem)
                                nestedDict[inner.Key.ToString()] = inner.Value;
                            Flatten(nestedDict, $"{key}:{i}", result);
                        }
                        else
                        {
                            result[$"{key}:{i}"] = item?.ToString();
                        }
                    }
                }
                else
                {
                    result[key] = kvp.Value?.ToString();
                }
            }
        }
    }
}
