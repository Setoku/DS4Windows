﻿using System.Text.RegularExpressions;

using JetBrains.Annotations;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Vapour.Client.Core.DependencyInjection;

namespace Vapour.Shared.Common.Tracing;

[UsedImplicitly]
public class OpenTelemetryRegistrar : IServiceRegistrar
{
    private const string AssemblyPrefix = "DS4Windows";

    private static readonly Regex Cleanup = new(", (Version|Culture|PublicKeyToken)=[0-9.\\w]+", RegexOptions.Compiled);

    public void ConfigureServices(IHostBuilder builder, HostBuilderContext context, IServiceCollection services)
    {
        // Get list of assemblies, register them all as potential tracing and metric sources
        string[] assemblyNames = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(assembly =>
            {
                string name = assembly.GetName().Name;
                return name != null && name.StartsWith(AssemblyPrefix);
            })
            .Select(assembly => Cleanup.Replace(assembly.GetName().Name!, string.Empty))
            .ToArray();

        if (bool.TryParse(context.Configuration.GetSection("OpenTelemetry:IsTracingEnabled").Value,
                out bool isTracingEnabled) &&
            isTracingEnabled)
        {
            // Add OpenTelemetry tracing
            services.AddOpenTelemetryTracing(tt => tt
                .SetSampler(new AlwaysOnSampler())
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(AssemblyPrefix))
                .AddSource(assemblyNames)
                .AddJaegerExporter(options => { options.ExportProcessorType = ExportProcessorType.Simple; })
            );
        }

        if (bool.TryParse(context.Configuration.GetSection("OpenTelemetry:IsMetricsEnabled").Value,
                out bool isMetricsEnabled) &&
            isMetricsEnabled)
        {
            // Add OpenTelemetry metrics
            services.AddOpenTelemetryMetrics(tm => tm.AddMeter(assemblyNames));
        }
    }
}