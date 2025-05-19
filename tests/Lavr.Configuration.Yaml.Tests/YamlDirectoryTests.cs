using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Xunit;
using Microsoft.Extensions.Configuration;
using NetEscapades.Configuration.Yaml;
using Lavr.Configuration;

namespace Lavr.Configuration.Tests
{
    public class YamlDirConfigurationBuilderExtensionTests
    {
        // FakeBuilder теперь полностью реализует IConfigurationBuilder
        private class FakeBuilder : IConfigurationBuilder
        {
            public IList<IConfigurationSource> Sources { get; } = new List<IConfigurationSource>();
            public IDictionary<string, object> Properties { get; } = new Dictionary<string, object>();

            public IConfigurationBuilder Add(IConfigurationSource source)
            {
                Sources.Add(source);
                return this;
            }

            // Реализация Build() — возвращаем корень конфигурации на основе накопленных Sources
             public IConfigurationRoot Build()
            {
                var providers = Sources
                    .Select(src => src.Build(this))
                    .ToList();
                return new ConfigurationRoot(providers);
            }
        }

        [Fact]
        public void AddYamlDirectory_DirectoryDoesNotExist_ThrowsFileNotFoundException()
        {
            var builder = new FakeBuilder();
            var nonExistentDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            var ex = Assert.Throws<FileNotFoundException>(
                () => builder.AddYamlDirectory(nonExistentDir, optional: false, reloadOnChange: false)
            );

            Assert.Contains("Yaml dir not found", ex.Message);
        }

        [Fact]
        public void AddYamlDirectory_EmptyDirectory_ThrowsFileNotFoundException()
        {
            var emptyDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(emptyDir);

            try
            {
                var builder = new FakeBuilder();
                var ex = Assert.Throws<FileNotFoundException>(
                    () => builder.AddYamlDirectory(emptyDir, optional: false, reloadOnChange: false)
                );

                Assert.Contains("Yaml dir is empty", ex.Message);
            }
            finally
            {
                Directory.Delete(emptyDir);
            }
        }

        [Fact]
        public void AddYamlDirectory_WithYamlFiles_AddsCorrectNumberOfSources()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            var file1 = Path.Combine(tempDir, "a.yaml");
            var file2 = Path.Combine(tempDir, "b.yml");
            File.WriteAllText(file1, "key: value");
            File.WriteAllText(file2, "foo: bar");

            try
            {
                var builder = new FakeBuilder();
                builder.AddYamlDirectory(tempDir, optional: false, reloadOnChange: true);

                Assert.Equal(2, builder.Sources.Count);

                var yamlSources = builder.Sources
                    .OfType<YamlConfigurationSource>()
                    .ToList();

                Assert.Equal(2, yamlSources.Count);
                Assert.All(yamlSources, src => Assert.True(src.ReloadOnChange));

                Assert.Equal(file1, Path.Combine(tempDir, yamlSources[0].Path));
                Assert.Equal(file2, Path.Combine(tempDir, yamlSources[1].Path));
            }
            finally
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }

        [Fact]
        public void AddYamlDirectory_OptionalTrue_DirectoryMissing_NoException()
        {
            var builder = new FakeBuilder();
            var nonExistentDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            var returned = builder.AddYamlDirectory(nonExistentDir, optional: true, reloadOnChange: false);

            Assert.Same(builder, returned);
            Assert.Empty(builder.Sources);
        }
    }
}
