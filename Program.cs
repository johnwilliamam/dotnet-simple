using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Instrumentation.SqlClient;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
const string serviceName = "dotnet-simple";

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
      .WithMetrics(metrics =>
      {
          metrics
              .AddMeter(serviceName)
              .AddHttpClientInstrumentation()
              .AddAspNetCoreInstrumentation()
              .AddOtlpExporter(otlpOptions =>
              {
                  otlpOptions.Endpoint = new Uri(builder.Configuration.GetValue("Otlp:Endpoint", defaultValue: "http://localhost:4317")!);
              });
      });

builder.Logging.AddOpenTelemetry(logging =>
{
    logging
    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName: serviceName))
    .AddConsoleExporter()
    .AddOtlpExporter(otlpOptions =>
    {
        otlpOptions.Endpoint = new Uri(builder.Configuration.GetValue("Otlp:Endpoint", defaultValue: "http://localhost:4317")!);
    });
});

builder.Services.AddControllers();

var app = builder.Build();

// Middleware para capturar payload e query params
app.Use(async (context, next) =>
{
    context.Request.EnableBuffering();

    // Captura o payload
    string body = "";
    if (context.Request.ContentLength > 0)
    {
        using (var reader = new StreamReader(context.Request.Body, leaveOpen: true))
        {
            body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0; // Reseta o corpo da requisicao para a leitura posterior
        }
    }

    // Captura os query params
    var queryParams = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : "";

    // Adiciona os detalhes ao Span
    var activity = System.Diagnostics.Activity.Current;
    if (activity != null)
    {
        activity.SetTag("http.request.body", body);
        activity.SetTag("http.request.query_params", queryParams);
    }

    await next();
});

// Rota para simular um lan�amento de dados GET
string HandleRollDice([FromServices] ILogger<Program> logger, string? player)
{
    var result = RollDice();

    if (string.IsNullOrEmpty(player))
    {
        logger.LogInformation("Anonymous player is rolling the dice: {result}", result);
    }
    else
    {
        logger.LogInformation("{player} is rolling the dice: {result}", player, result);
    }

    return result.ToString(CultureInfo.InvariantCulture);
}

// Rota para receber dados via POST
app.MapPost("/api/submit", async (HttpContext context) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    using var reader = new StreamReader(context.Request.Body);
    var body = await reader.ReadToEndAsync();

    logger.LogInformation("Received POST request with payload: {body}", body);

    return Results.Ok(new { message = "Data received successfully", receivedData = body });
});

int RollDice()
{
    return Random.Shared.Next(1, 7);
}

app.MapGet("/rolldice/{player?}", HandleRollDice);

app.Run();
