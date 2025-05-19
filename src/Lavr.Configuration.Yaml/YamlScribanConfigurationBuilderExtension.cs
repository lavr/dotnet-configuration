using System;
using System.Collections.Generic;
using System.IO;
using Scriban;
using Scriban.Runtime;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Microsoft.Extensions.Configuration;
using System.Linq;

namespace Lavr.Configuration
{

    public static class Helper
    {
        /// <summary>
        /// Извлекает значение из иерархии словарей по точечному пути.
        /// </summary>
        /// <param name="root">Корневой объект, полученный из deserializer.Deserialize&lt;dynamic&gt;().</param>
        /// <param name="path">Строка-путь, сегменты разделены точками: "x.y.z".</param>
        /// <returns>
        /// Либо найденный объект (может быть Dictionary, список, строка, число и т.п.),
        /// либо null, если какой-то ключ отсутствовал или корневой объект не словарь.
        /// </returns>
        public static object GetByPath(object root, string path)
        {
            if (root is not IDictionary<object, object> currentDict || string.IsNullOrWhiteSpace(path))
                return null;

            var segments = path.Split('.');
            object current = currentDict;

            foreach (var seg in segments)
            {
                if (current is IDictionary<object, object> dict && dict.TryGetValue(seg, out var next))
                {
                    current = next;
                }
                else
                {
                    // ключа нет или текущий объект уже не словарь
                    return null;
                }
            }

            return current;
        }

        /// <summary>
        /// Универсальный вариант с приводом к нужному типу.
        /// </summary>
        public static T GetByPath<T>(object root, string path)
        {
            var val = GetByPath(root, path);
            return val is T casted ? casted : default!;
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

                if (!File.Exists(templateFilePath)){
                      if (optional) return;
                      throw new FileNotFoundException($"Template file not found: {templateFilePath}");
                }

                var templateText = File.ReadAllText(templateFilePath);
                var valuesYaml = File.ReadAllText(valuesFilePath);

                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();
                // var values = deserializer.Deserialize<Dictionary<string, object>>(valuesYaml);
                var values = deserializer.Deserialize<dynamic>(valuesYaml);

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
                        var db = Helper.GetByPath(values, path);
                        var host = db["host"].ToString();
                        var port = db["port"]?.ToString() ?? "5432";
                        return $"Server={host};Port={port};Database={database}";
                        // TODO: добавить больше опциональных параметров - таймауты, размер пула
                    })
                );

                scriptObject.Import("indent", new Func<string, int, string>((text, spaces) =>
                {
                    var pad = new string(' ', spaces);
                    var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                    return string.Join(Environment.NewLine, lines.Select(line => pad + line));
                }));


                var scribanTemplate = Template.Parse(templateText);

                // scriptObject.Import(values);
                Scriban.Runtime.ScriptObjectExtensions.Import(scriptObject, values);

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
