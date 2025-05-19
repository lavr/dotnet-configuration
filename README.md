Примеры реп:
- https://github.com/domaindrivendev/Swashbuckle.AspNetCore/
- https://github.com/dotnet/runtime/
- https://github.com/andrewlock/NetEscapades.Configuration


Пример темплейтинга:

```
using System;
using System.IO;
using Scriban;
using Scriban.Runtime;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace MyApp
{
    public class ConfigRenderer
    {
        private readonly dynamic _values;

        public ConfigRenderer(string valuesPath)
        {
            var yamlText = File.ReadAllText(valuesPath);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
            _values = deserializer.Deserialize<dynamic>(yamlText);
        }

        public void Render(string templatePath, string outputPath)
        {
            var templateText = File.ReadAllText(templatePath);
            var template = Scriban.Template.Parse(templateText);

            var scriptObject = new ScriptObject();

            scriptObject.Add("global", (object)_values["global"]);

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
                    var server = args["server"]?.ToString() ?? "postgres01";
                    var pg = _values["global"]["databases"][server];
                    var host = pg["host"].ToString();
                    var port = pg["port"].ToString();
                    return $"Server={host};Port={port};Database={database}";
                    // TODO: можно добавить больше параметров - таймауты, размер пула
                })
            );

            // TODO: добавить фильтр indent(N)

            var context = new TemplateContext();
            context.PushGlobal(scriptObject);

            var result = template.Render(context);
            File.WriteAllText(outputPath, result);
        }
    }
}


namespace MyApp {
    class Program
    {
        static void Main()
        {
            var renderer = new ConfigRenderer("values.yaml");
            renderer.Render("appsettings.yaml.tmpl", "appsettings.yaml");
            Console.WriteLine("Сгенериировали файл: appsettings.yaml");
        }
    }
}
```


пример темплейта:
```
{{ global.logging.common | to_yaml  }}
ConnectionStrings:
  Db1: {{ PostgresConnection { database: "dbname1", server: "postgres01" } }}
  Db2: {{ PostgresConnection { database: "dbname2" } }}
SomeApiSettings:
  Host: {{ global.public_url }}/sorting
Queue1:
{{ global.queues.queues | to_yaml | indent(2)  }}
```

пример values.yaml:
```
global:
  public_url: https://app.corp.tld
  databases:
    postgres01:
      host: postgres.corp.tld
      port: "6432"
      user: postgres-user
      password: postgres-password
  queues:
    rmq01:
      Host: rabbitmq.corp.tld
      VirtualHost: ETH
      User: rmquser
      Зassword: rmqpassword
  logging:
    common:
      ElasticApm: {}
```
