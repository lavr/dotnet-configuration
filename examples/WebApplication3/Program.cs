using Lavr.Configuration;
using Microsoft.VisualBasic;


IConfigurationBuilder configBuilder = new ConfigurationBuilder();

// Классическая конфигурация из appsettings.json
configBuilder
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile("configs/appsettings.json", optional: true);

// Конфигурация через из appsettings.yml
configBuilder
    .AddYamlFile("appsettings.yml", optional: true)
    .AddYamlFile("configs/appsettings.yml", optional: true);

// Конфигурация из темплейтов
configBuilder
    .AddYamlScribanTemplateFile("appsettings.yml.tmpl", "appvalues.yml", optional: true)
    .AddYamlScribanTemplateFile("appsettings.yml.tmpl", "secrets/appvalues.yml", optional: true)
    .AddYamlScribanTemplateFile("configs/appsettings.yml.tmpl", "secrets/appvalues.yml", optional: true);

configBuilder.AddEnvironmentVariables();

IConfigurationRoot configuration = configBuilder.Build();

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.Sources.Clear();
builder.Configuration.AddConfiguration(configuration);


var app = builder.Build();

app.MapGet("/", (IConfiguration cfg) =>
{
    var myValue = cfg["MySetting"];
    return Results.Text($"MySetting = {myValue}");
});


app.Run();
