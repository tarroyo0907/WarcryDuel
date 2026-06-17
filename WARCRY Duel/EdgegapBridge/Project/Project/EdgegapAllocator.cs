using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using EdgegapAllocatorModule.Client;
using EdgegapAllocatorModule.Client.Models;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Apis.Matchmaker;
using Unity.Services.CloudCode.Core;
using Unity.Services.Matchmaker.Model;

using IExecutionContext = Unity.Services.CloudCode.Core.IExecutionContext;

namespace EdgegapBridge.Project;

/// <summary>
/// Module configuration for dependency injection.
/// Registers IGameApiClient as a singleton for accessing Unity services like Secret Manager.
/// </summary>
public class ModuleConfig : ICloudCodeSetup
{
	public void Setup(ICloudCodeConfig config)
	{
		config.Dependencies.AddSingleton(GameApiClient.Create());
		config.Dependencies.AddScoped<IEdgegapHttpClientFactory, EdgegapHttpClientFactory>();
	}
}

public class EdgegapAllocator(IGameApiClient gameApiClient, IEdgegapHttpClientFactory httpClientFactory, ILogger<EdgegapAllocator> logger) : IMatchmakerAllocator
{
	// Configuration - users should modify these constants for their setup
	private const string ApplicationName = "warcry-duel";
	private const string VersionName = "26.05.31-18.26.15-UTC";
	private const string PortName = "gameport";

	// Edgegap Constants
	private const string EdgegapApiUrl = "https://api.edgegap.com";
	private const bool ForceCachedLocation = false; // You need to activate a cached application version in Edgegap to use this feature

	// Secret names - these must match the secrets stored in Unity Dashboard
	private const string EdgegapApiTokenSecretName = "EDGEGAP_API_TOKEN";

    /// <summary>
    /// Retrieves the latest active version for the specified Edgegap application.
    /// </summary>
    private async Task<string?> GetLatestApplicationVersionAsync(HttpClient client)
    {
        try
        {
            HttpResponseMessage response = await client.GetAsync($"{EdgegapApiUrl}/v1/app/{ApplicationName}/versions");

            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync();
                logger.LogError("Failed to fetch Edgegap versions: {StatusCode} - {Content}", response.StatusCode, errorContent);
                return null;
            }

            string responseContent = await response.Content.ReadAsStringAsync();
            var versionsResponse = JsonConvert.DeserializeObject<JObject>(responseContent);

            // Edgegap returns versions in a "versions" array
            var versions = versionsResponse?["versions"] as JArray;

            if (versions == null || versions.Count == 0)
            {
                logger.LogWarning("No versions found for application {ApplicationName}", ApplicationName);
                return null;
            }

            // Get the first active version (Edgegap typically orders by most recent)
            var latestVersion = versions
                .FirstOrDefault(v => v["is_active"]?.Value<bool>() == true)?["name"]?.Value<string>();

            if (latestVersion == null)
            {
                logger.LogWarning("No active version found for application {ApplicationName}", ApplicationName);
            }

            return latestVersion;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching latest Edgegap version");
            return null;
        }
    }

	[CloudCodeFunction("Matchmaker_AllocateServer")]
	public async Task<AllocateResponse> Allocate(IExecutionContext context, AllocateRequest request)
	{
		try
		{
			Secret edgegapApiToken = await gameApiClient.SecretManager.GetSecret(context, EdgegapApiTokenSecretName);
			using HttpClient client = httpClientFactory.Create(edgegapApiToken.Value);

            // Fetch the latest version dynamically
            string? versionName = await GetLatestApplicationVersionAsync(client);

            if (string.IsNullOrEmpty(versionName))
            {
                // Fallback to hardcoded version if API fails
                logger.LogWarning("Using fallback version {VersionName}", VersionName);
                versionName = VersionName;
            }
            else
            {
                logger.LogInformation("Using latest Edgegap version: {VersionName}", versionName);
            }

			List<DeploymentUser> users = new();

            // Default Location Anchors (East U.S)
            double targetLatitude = 40.7128;
            double targetLongitude = -74.0060;

            // Default fallback region if parsing falls through
            string unityRegion = "us-east1";

            try
            {
                var matchProperties = request.MatchmakingResults?.MatchProperties;

                // 1. Extract Unity's targeted matchmaking region name
                if (matchProperties != null && matchProperties.TryGetValue("region", out var regionObj))
                {
                    unityRegion = regionObj?.ToString()?.ToLower();
                    logger.LogInformation($"Unity Matchmaker picked region cluster: {unityRegion}");

                    // Map Unity region selections to physical center-points
                    switch (unityRegion)
                    {
                        case "us-east1": // NY Area
                            targetLatitude = 40.7128;
                            targetLongitude = -74.0060;
                            break;
						case "us-central1": // Central U.S. area
                            targetLatitude = 41.8781;   // Chicago Latitude
                            targetLongitude = -87.6298; // Chicago Longitude
                            break;
                        case "us-west1": // Silicon Valley area
                            targetLatitude = 37.3382;   // Silicon Valley Latitude
                            targetLongitude = -121.8863;// Silicon Valley Longitude
                            break;
                        case "europe-central2":
                            targetLatitude = 52.2297;   // Warsaw Latitude
                            targetLongitude = 21.0122;  // Warsaw Longitude
                            break;
						case "europe-west4":
                            targetLatitude = 50.1109;   // Frankfurt Latitude
                            targetLongitude = 8.6821;   // Frankfurt Longitude
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to map QoS parameters: {ex.Message}");
            }

            logger.LogInformation($"Instructing Edgegap to deploy server cluster inside: {unityRegion}");

            users.Add(new DeploymentUser
            {
                UserType = "geo_coordinates",
                UserData = new UserData
                {
                    Latitude = targetLatitude,
                    Longitude = targetLongitude,
                },
            });

            

            // You can add additional deployment parameters here like tags (limited to 20 characters) or environment variables
            var deploymentRequest = new DeploymentRequest
			{
				Application = ApplicationName,
				Version = versionName,
				RequireCachedLocations = ForceCachedLocation,
                Users = users,
				EnvironmentVariables =
				[
					new EnvironmentVariable
					{
						Key = "MATCH_ID",
						Value = request.MatchId,
						IsHidden = false,
					},
				],
				Tags = ["ugs-matchmaker"],
			};

			var content = new StringContent(JsonConvert.SerializeObject(deploymentRequest), Encoding.UTF8, "application/json");
			HttpResponseMessage response = await client.PostAsync($"{EdgegapApiUrl}/v2/deployments", content);

			string responseContent = await response.Content.ReadAsStringAsync();
			if (!response.IsSuccessStatusCode)
			{
				logger.LogError("Edgegap deployment failed with status code {ResponseStatusCode}: {ResponseContent}", response.StatusCode, responseContent);
				return new AllocateResponse(AllocateStatus.Error)
				{
					Message = responseContent
				};
			}

			var edgegapDeployment = JsonConvert.DeserializeObject<DeploymentResponse>(responseContent);

			return new AllocateResponse(AllocateStatus.Created)
			{
				AllocationData = new Dictionary<string, object>
				{
					{
						"requestId", edgegapDeployment?.RequestId ?? string.Empty
					},
				},
			};
		}
		catch (Exception e)
		{
			logger.LogError(e, "Edgegap deployment failed");
			return new AllocateResponse(AllocateStatus.Error)
			{
				Message = e.Message,
			};
		}
	}

	[CloudCodeFunction("Matchmaker_PollAllocation")]
	public async Task<PollResponse> Poll(IExecutionContext context, PollRequest request)
	{
		var requestId = request.AllocationData["requestId"].ToString();
		try
		{
			Secret edgegapApiToken = await gameApiClient.SecretManager.GetSecret(context, EdgegapApiTokenSecretName);
			HttpClient client = httpClientFactory.Create(edgegapApiToken.Value);
			HttpResponseMessage response = await client.GetAsync($"{EdgegapApiUrl}/v1/status/{requestId}");
			string responseContent = await response.Content.ReadAsStringAsync();

			if (!response.IsSuccessStatusCode)
			{
				return new PollResponse(PollStatus.Error)
				{
					Message = responseContent,
				};
			}

			var deploymentStatus = JsonConvert.DeserializeObject<DeploymentStatusResponse>(responseContent);

			if (deploymentStatus == null)
			{
				return new PollResponse(PollStatus.Error)
				{
					Message = "Deployment status response is null",
				};
			}

			switch (deploymentStatus.CurrentStatus)
			{
				case DeploymentStatus.Ready:
					{
						if (deploymentStatus.Ports == null || deploymentStatus.Ports.Count == 0)
						{
							return new PollResponse(PollStatus.Error)
							{
								Message = $"Deployment has no exposed ports. Response: {responseContent}",
							};
						}

						if (!deploymentStatus.Ports.TryGetValue(PortName, out var port))
						{
		
							return new PollResponse(PollStatus.Error)
							{
								Message = $"Requested port {PortName} is not exposed by the deployment. Please verify the name of the port in the Edgegap dashboard."
							};
						}

						if (deploymentStatus.PublicIp == null)
						{
							return new PollResponse(PollStatus.Error)
							{
								Message = $"Deployment is READY but public_ip is missing. Response: {responseContent}",
							};
						}

						return new PollResponse(PollStatus.Allocated)
						{
							AssignmentData = AssignmentData.IpPort(
								deploymentStatus.PublicIp,
								port.External
							),
						};
					}
				case DeploymentStatus.Error:
					return new PollResponse(PollStatus.Error)
					{
						Message = $"Deployment failed with the current error: {deploymentStatus.ErrorDetail}",
					};
				case DeploymentStatus.Terminating:
				case DeploymentStatus.Terminated:
					return new PollResponse(PollStatus.Error)
					{
						Message = "Deployment is terminated or terminated and can't receive connection anymore",
					};
				case DeploymentStatus.Deploying:
				case DeploymentStatus.Seeking:
				default:
					return new PollResponse(PollStatus.Pending);
			}
		}
		catch (Exception e)
		{
			logger.LogError(e, "Error polling Edgegap");
			return new PollResponse(PollStatus.Error)
			{
				Message = e.Message,
			};
		}
	}


	/// <summary>
	/// Safely extracts all valid player IP addresses from Matchmaker match properties under the "player_ip" custom data key.
	/// </summary>
	private IReadOnlyList<IPAddress> ExtractValidPlayerIps(
		Dictionary<string, object> matchProperties
	)
	{
		var result = new List<IPAddress>();

		if (!matchProperties.TryGetValue("Players", out var playersObj))
		{
			logger.LogDebug("No Players key found in MatchProperties");
			return result;
		}

		if (playersObj is not JArray playersArray)
		{
			logger.LogWarning("Players is not a JArray (actual type: {type})", playersObj.GetType());
			return result;
		}

		List<Player>? players;
		try
		{
			players = playersArray.ToObject<List<Player>>();
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed to deserialize Players array");
			return result;
		}

		if (players == null)
			return result;

		foreach (var player in players)
		{
			if (player.CustomData is not JObject customData)
				continue;

			if (!customData.TryGetValue("player_ip", out var ipToken))
				continue;

			var ipString = ipToken.Type switch
			{
				JTokenType.String => ipToken.Value<string>(),
				_ => null
			};

			if (string.IsNullOrWhiteSpace(ipString))
				continue;

			if (IPAddress.TryParse(ipString, out var ip))
			{
				result.Add(ip);
			}
			else
			{
				logger.LogDebug("Invalid IP format ignored: {ip}", ipString);
			}
		}

		return result;
	}
}