using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aspire13BatteriesIncludedDemo.Web;

public class ConnectionShapingApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public async Task<ConnectionShapingInfo?> GetConnectionShapingAsync(CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<ConnectionShapingInfo>(
            "/diagnostics/connection-shaping", s_options, cancellationToken);
    }
}

// Models matching the enriched API response

public record ConnectionShapingInfo(
    string Title,
    [property: JsonPropertyName("EnvVar_Mechanism")] EnvVarMechanism EnvVarMechanism,
    [property: JsonPropertyName("Injected_EnvVars")] InjectedEnvVar[] InjectedEnvVars,
    ShapePipeline[] Pipeline,
    [property: JsonPropertyName("Key_Insight")] KeyInsight KeyInsight,
    RawConnectionStrings RawConnectionStrings);

public record EnvVarMechanism(
    string Title,
    string[] How,
    [property: JsonPropertyName("Why_This_Is_Brilliant")] string[] WhyThisIsBrilliant,
    [property: JsonPropertyName("DotNet_Convention")] string DotNetConvention);

public record InjectedEnvVar(
    string EnvVarName,
    string Value,
    [property: JsonPropertyName("MapsTo_IConfiguration")] string MapsToIConfiguration);

public record ShapePipeline(
    string Resource,
    [property: JsonPropertyName("Step1_AppHost_Resource")] string Step1AppHostResource,
    [property: JsonPropertyName("Step1_File")] string Step1File,
    [property: JsonPropertyName("Step2_AppHost_Reference")] string Step2AppHostReference,
    [property: JsonPropertyName("Step2_WhatHappens")] string Step2WhatHappens,
    [property: JsonPropertyName("Step2_EnvVarName")] string Step2EnvVarName,
    [property: JsonPropertyName("Step2_InjectedValue")] string Step2InjectedValue,
    [property: JsonPropertyName("Step3_ClientIntegration")] string Step3ClientIntegration,
    [property: JsonPropertyName("Step3_NuGetPackage")] string Step3NuGetPackage,
    [property: JsonPropertyName("Step3_File")] string Step3File,
    [property: JsonPropertyName("Step3_WhatHappens")] string Step3WhatHappens,
    [property: JsonPropertyName("Step3_RegisteredType")] string Step3RegisteredType,
    [property: JsonPropertyName("Step3_UsedVia")] string Step3UsedVia,
    string ProofConnectionString,
    string Endpoint);

public record KeyInsight(
    string Punto1,
    string Punto2,
    string Punto3,
    string Punto4,
    string Punto5);

public record RawConnectionStrings(
    string AppDb,
    string Cache);
