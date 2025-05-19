test:
	#dotnet clean src/Lavr.Configuration.Yaml
	#dotnet build src/Lavr.Configuration.Yaml
	dotnet clean tests/Lavr.Configuration.Yaml.Tests
	dotnet build tests/Lavr.Configuration.Yaml.Tests
	dotnet test tests/Lavr.Configuration.Yaml.Tests
