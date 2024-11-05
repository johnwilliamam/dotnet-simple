# OpenTelemetry Instrumentation para .NET
- Este projeto demonstra a configuração básica para capturar traces, logs, e métricas no .NET utilizando o OpenTelemetry SDK. As configurações permitem exportar dados para um backend OTLP, como o OpenTelemetry Collector, Datadog, ou outro sistema de monitoramento compatível.

## Pré-requisitos
    - .NET SDK 6.0 ou superior
    - Backend de observabilidade compatível com OTLP
    - Pacotes NuGet do OpenTelemetry
    - Instalação
    - Instale os pacotes necessários para instrumentação com o OpenTelemetry:
       - dotnet add package OpenTelemetry.Extensions.Hosting
       - dotnet add package OpenTelemetry.Logs
       - dotnet add package OpenTelemetry.Trace
       - dotnet add package OpenTelemetry.Metrics
       - dotnet add package OpenTelemetry.Instrumentation.AspNetCore
       - dotnet add package OpenTelemetry.Instrumentation.Http
## Configuração de Instrumentação
Abaixo estão os trechos essenciais para configurar traces, logs, métricas, e captura manual de payloads e query params em requisições HTTP.

## Configuração de Traces
No arquivo Program.cs, adicione a instrumentação para AspNetCore e HttpClient, e configure o exportador OTLP:

* Copiar código
```bash
builder.Services.AddOpenTelemetry()
      .ConfigureResource(resource => resource.AddService(serviceName))
      .WithTracing(tracing =>
      {
          tracing
              .AddSource(serviceName)
              .AddAspNetCoreInstrumentation()
              .AddHttpClientInstrumentation()
              .AddConsoleExporter()
              .AddSqlClientInstrumentation(options =>
              {
                  options.SetDbStatementForText = true;
                  options.RecordException = true;
              })
              .SetSampler(new AlwaysOnSampler())
              .AddOtlpExporter(otlpOptions =>
              {
                  otlpOptions.Endpoint = new Uri(builder.Configuration.GetValue("Otlp:Endpoint", defaultValue: "http://localhost:4317")!);
              });
      })
```
## Configuração de Logs
Para integrar o ILogger com o OpenTelemetry, adicione o seguinte ao seu Program.cs:

* Copiar código
```bash
builder.Logging.AddOpenTelemetry(logging =>
{
    logging
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("dotnet-simple"))
        .AddOtlpExporter(otlpOptions =>
        {
            otlpOptions.Endpoint = new Uri("http://<OTLP-ENDPOINT>:4317");
        });
});
```
## Configuração de Métricas
Para capturar métricas HTTP básicas e exportá-las:

* Copiar código
```bash
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(otlpOptions =>
            {
                otlpOptions.Endpoint = new Uri("http://<OTLP-ENDPOINT>:4317");
            });
    });
 ```   
## Middleware para Captura de Payload e Query Params
Adicione um middleware para capturar o payload e os query params manualmente e associá-los ao span ativo:

* Copiar código
```bash
app.Use(async (context, next) =>
{
    context.Request.EnableBuffering();

    // Captura o payload
    string body = "";
    if (context.Request.ContentLength > 0)
    {
        using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
        body = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0; // Reseta o corpo para leitura posterior
    }

    // Captura os query params
    var queryParams = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : "";

    // Adiciona ao span atual
    var activity = System.Diagnostics.Activity.Current;
    if (activity != null)
    {
        activity.SetTag("http.request.body", body);
        activity.SetTag("http.request.query_params", queryParams);
    }

    await next();
});
```
## Executando o Projeto
Defina o endereço do OTLP em cada exportador (substitua <OTLP-ENDPOINT>).

* Inicie o projeto:

* Copiar código
```bash
dotnet run
```