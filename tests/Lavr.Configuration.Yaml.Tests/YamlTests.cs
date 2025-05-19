using System.IO;
using Microsoft.Extensions.Configuration;
using FluentAssertions;
using Xunit;
using Lavr.Configuration;

namespace Lavr.Configuration.Tests
{
    public class YamlTemplateTests
    {

        [Fact]
        public void AddYamlTemplateFile_LoadsValuesCorrectly_001()
        {

            var workingDir = Directory.GetCurrentDirectory();
            var valuesPath = Path.Combine(workingDir, "data/001/values.yaml");
            var tmplPath = Path.Combine(workingDir, "data/001/template.yaml.tmpl");

            System.Console.WriteLine($"tmplPath: {tmplPath}");
            System.Console.WriteLine($"valuesPath: {valuesPath}");

            var builder = new ConfigurationBuilder();
            builder.AddYamlTemplateFile(tmplPath, valuesPath, optional: false, reloadOnChange: false);
            var config = builder.Build();

            config["ConnectionStrings:Db1"].Should().Be("Server=test;Database=db;User Id=sa;");
        }

        [Fact]
        public void AddYamlTemplateFile_LoadsValuesCorrectly_002()
        {

            var workingDir = Directory.GetCurrentDirectory();
            var valuesPath = Path.Combine(workingDir, "data/002/values.yaml");
            var tmplPath = Path.Combine(workingDir, "data/002/template.yaml.tmpl");

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
