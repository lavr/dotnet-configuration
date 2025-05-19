using System.IO;
using Microsoft.Extensions.Configuration;
using FluentAssertions;
using Xunit;
using Lavr.Configuration;

namespace Lavr.Configuration.Tests
{
    public class YamlTemplateTests
    {
        private const string Template = @"ConnectionStrings:\n  Db1: {{ global.databases.db1 }}";
        private const string Values = @"global:\n  databases:\n    db1: Server=test;Database=db;User Id=sa;";

        [Fact]
        public void AddYamlTemplateFile_LoadsValuesCorrectly()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);

            var tmplPath = Path.Combine(tempDir, "template.yaml.tmpl");
            var valuesPath = Path.Combine(tempDir, "values.yaml");
            File.WriteAllText(tmplPath, Template);
            File.WriteAllText(valuesPath, Values);

            var builder = new ConfigurationBuilder();
            builder.AddYamlTemplateFile(tmplPath, valuesPath, optional: false, reloadOnChange: false);
            var config = builder.Build();

            config["ConnectionStrings:Db1"].Should().Be("Server=test;Database=db;User Id=sa;");
        }

        [Fact]
        public void AddYamlTemplateFile_Optional_SkipsMissingFiles()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);

            var tmplPath = Path.Combine(tempDir, "missing.tmpl");
            var builder = new ConfigurationBuilder();
            // Should not throw
            builder.AddYamlTemplateFile(tmplPath, Path.Combine(tempDir, "no.yaml"), optional: true);
            builder.Build();
        }
    }
}
