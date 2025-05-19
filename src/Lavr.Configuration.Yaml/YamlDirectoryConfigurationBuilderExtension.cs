using System;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Microsoft.Extensions.Configuration;
using System.Linq;

namespace Lavr.Configuration
{

    public static class YamlDirConfigurationBuilderExtension
    {
        public static IConfigurationBuilder AddYamlDirectory(
            this IConfigurationBuilder builder,
            string yamlDir,
            bool optional = false,
            bool reloadOnChange = false)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (string.IsNullOrEmpty(yamlDir)) throw new ArgumentException("Template file path must be provided", nameof(yamlDir));

            if(!Directory.Exists(yamlDir)) {
                if (optional) return builder;
                throw new FileNotFoundException($"Yaml dir not found: {yamlDir}");
            }

            var patterns = new[] { "*.yml", "*.yaml" };
            var yamlFiles = patterns
                .SelectMany(pattern => Directory.EnumerateFiles(yamlDir, pattern, SearchOption.TopDirectoryOnly))
                .OrderBy(Path.GetFileName);

            if (!optional && !yamlFiles.Any())
                throw new FileNotFoundException($"Yaml dir is empty: {yamlDir}");

            foreach (var fullPath in yamlFiles)
            {
                builder.AddYamlFile(path: fullPath, reloadOnChange: reloadOnChange, optional: optional);
            }

            return builder;
        }

    }
}
