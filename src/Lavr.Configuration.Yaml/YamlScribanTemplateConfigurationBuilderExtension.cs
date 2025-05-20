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

    public static class YamlScribanTemplateConfigurationBuilderExtension
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
        public static IConfigurationBuilder AddYamlScribanTemplateFile(
            this IConfigurationBuilder builder,
            string templateFilePath,
            string valuesFilePath = "appvalues.yaml",
            bool optional = false,
            bool reloadOnChange = false,
            bool save = true)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (string.IsNullOrEmpty(templateFilePath)) throw new ArgumentException("Template file path must be provided", nameof(templateFilePath));

            void Load()
            {

                if (!File.Exists(templateFilePath)){
                      if (optional) return;
                      throw new FileNotFoundException($"Template file not found: {templateFilePath}");
                }

                if (!File.Exists(valuesFilePath)){
                      if (optional) return;
                      throw new FileNotFoundException($"Values file not found: {valuesFilePath}");
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
                        var db = Helpers.GetByPath(values, path);
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

                // Save rendered file if requested
                if (save)
                {
                    var dir = Path.GetDirectoryName(templateFilePath) ?? string.Empty;
                    var fileName = Path.GetFileName(templateFilePath);
                    // Remove only .tmpl extension
                    var trimmed = fileName.EndsWith(".tmpl", StringComparison.OrdinalIgnoreCase)
                        ? fileName.Substring(0, fileName.Length - ".tmpl".Length)
                        : fileName;
                    var saveName = "." + trimmed;
                    var savePath = Path.Combine(dir, saveName);
                    File.WriteAllText(savePath, rendered);
                }

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
