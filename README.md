# Lavr.Configuration.Yaml

[![NuGet](https://img.shields.io/nuget/v/Lavr.Configuration.Yaml.svg)](https://www.nuget.org/packages/Lavr.Configuration.Yaml)
[![CI](https://github.com/lavr/dotnet-configuration/actions/workflows/ci.yml/badge.svg)](https://github.com/lavr/dotnet-configuration/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)

YAML configuration providers for `Microsoft.Extensions.Configuration` with [Scriban](https://github.com/scriban/scriban) templating support.

Use Scriban templates to generate application configuration from a shared values file — keep secrets and environment-specific settings in one place and render them into your YAML configs at startup.

## Supported frameworks

`netcoreapp3.1` · `net5.0` · `net6.0` · `net7.0` · `net8.0` · `net9.0` · `net10.0`

## Installation

```
dotnet add package Lavr.Configuration.Yaml
```

## Quick start

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddYamlScribanTemplateFile(
        templateFilePath: "appsettings.yaml.tmpl",
        valuesFilePath: "appvalues.yaml");
```

## Features

### AddYamlScribanTemplateFile

Renders a Scriban YAML template using values from a separate YAML file and loads the result into configuration.

```csharp
builder.Configuration
    .AddYamlScribanTemplateFile(
        templateFilePath: "appsettings.yaml.tmpl",
        valuesFilePath: "appvalues.yaml",
        optional: false,
        reloadOnChange: false,
        save: true);
```

| Parameter | Default | Description |
|---|---|---|
| `templateFilePath` | — | Path to the Scriban template file |
| `valuesFilePath` | `"appvalues.yaml"` | Path to the YAML file with template values |
| `optional` | `false` | Skip silently if files are missing |
| `reloadOnChange` | `false` | Watch both files and reload on change |
| `save` | `true` | Save rendered output to a dot-file (e.g. `.appsettings.yaml`) |

#### Template example

**appvalues.yaml** — shared values (secrets, hosts, ports):

```yaml
global:
  public_url: https://app.corp.tld
  database:
    postgres01:
      host: postgres.corp.tld
      port: "6432"
    postgres02:
      host: postgres2.corp.tld
      port: "5432"
  queues:
    rmq01:
      Host: rabbitmq.corp.tld
      VirtualHost: ETH
  logging:
    common:
      ElasticApm: {}
```

**appsettings.yaml.tmpl** — Scriban template:

```
{{ global.logging.common | to_yaml }}
ConnectionStrings:
  Db1: {{ PostgresConnection { database: "dbname1" } }}
  Db2: {{ PostgresConnection { database: "dbname2", path: "global.database.postgres02" } }}
SomeApiSettings:
  Host: {{ global.public_url }}/sorting
Queue1:
{{ global.queues.rmq01 | to_yaml | indent(2) }}
```

#### Built-in template functions

| Function | Description | Example |
|---|---|---|
| `to_yaml` | Converts an object to YAML | `{{ global.logging.common \| to_yaml }}` |
| `indent` | Indents text by N spaces | `{{ value \| to_yaml \| indent(4) }}` |
| `PostgresConnection` | Generates a PostgreSQL connection string | `{{ PostgresConnection { database: "mydb", path: "global.database.postgres01" } }}` |

`PostgresConnection` reads `host` and `port` from the values file at the given path (defaults to `global.database.postgres01`) and produces: `Server={host};Port={port};Database={database}`.

### AddYamlDirectory

Loads all `*.yml` and `*.yaml` files from a directory in alphabetical order.

```csharp
builder.Configuration
    .AddYamlDirectory(
        yamlDir: "configs/",
        optional: false,
        reloadOnChange: false);
```

### Full configuration example

```csharp
builder.Configuration
    // JSON
    .AddJsonFile("appsettings.json", optional: true)
    // Plain YAML
    .AddYamlFile("appsettings.yml", optional: true)
    // YAML directory
    .AddYamlDirectory("configs/", optional: true)
    // Scriban templates with different value sources
    .AddYamlScribanTemplateFile("appsettings.yml.tmpl", "appvalues.yml", optional: true)
    .AddYamlScribanTemplateFile("appsettings.yml.tmpl", "secrets/appvalues.yml", optional: true)
    // Environment variables (highest priority)
    .AddEnvironmentVariables();
```

Configuration sources are applied in order — later sources override earlier ones.

## Dependencies

- [YamlDotNet](https://github.com/aaubry/YamlDotNet) — YAML parsing
- [Scriban](https://github.com/scriban/scriban) — template rendering
- [NetEscapades.Configuration.Yaml](https://github.com/andrewlock/NetEscapades.Configuration) — `AddYamlFile` extension

## License

[MIT](LICENSE)
