using System.Diagnostics;
using System.Text;
using Amazon.ECR;
using Amazon.ECR.Model;
using Management.Enums;
using Management.Models;
using Management.Requests;
using Management.Response;

namespace Management.Services;

public class DeployService(DatabaseService database, IAmazonECR ecrClient, ILogger<DeployService> logger)
{
    public async Task<DeployResponse> Deploy(DeployRequest request)
    {
        await DockerLogin();

        string composeFileDirectory = $"./repos/{request.Repository}";

        try
        {
            Environment.SetEnvironmentVariable("ENVIRONMENT", request.Environment);
            Environment.SetEnvironmentVariable("IMAGE", request.Image);

            ComposeDown(composeFileDirectory, request, out var composeDownErrors);

            var record = new DeployRecord
            {
                Id = request.Id,
                Environment = request.Environment,
                Repository = request.Repository,
                File = request.File,
                Image = request.Image,
                Status = DeployStatus.InProgress,
                Type = DeployType.Start,
                CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mmZ"),
                CreatedBy = request.Actor
            };

            await database.SaveDeployRecord(record);

            var response = ComposeUp(composeFileDirectory, request, out var composeUpMessages);

            record.Status = response.Status;
            await database.SaveDeployRecord(record);

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError("Exception: {exception}", ex.Message);
            return new DeployResponse
            {
                Id = request.Id,
                Status = DeployStatus.Failed,
                ErrorMessages = ["Internal Server Error."]
            };
        }
    }

    public async Task<DeployResponse> ShutDown(DeployRequest request)
    {
        await DockerLogin();
        //ListCurrentDirectory("\"ls ../\"");

        string composeFileDirectory = $"./repos/{request.Repository}";

        try
        {
            Environment.SetEnvironmentVariable("ENVIRONMENT", request.Environment);
            Environment.SetEnvironmentVariable("IMAGE", request.Image);

            var record = new DeployRecord
            {
                Id = request.Id,
                Environment = request.Environment,
                Repository = request.Repository,
                File = request.File,
                Image = request.Image,
                Type = DeployType.Stop,
                CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mmZ"),
                CreatedBy = request.Actor
            };

            if (ComposeDown(composeFileDirectory, request, out var errorMessages))
            {
                record.Status = DeployStatus.Success;
                await database.SaveDeployRecord(record);

                return new DeployResponse
                {
                    Id = request.Id,
                    Status = DeployStatus.Success
                };
            }

            record.Status = DeployStatus.Failed;
            await database.SaveDeployRecord(record);

            return new DeployResponse
            {
                Id = request.Id,
                Status = DeployStatus.Failed,
                ErrorMessages = errorMessages
            };
        }
        catch (Exception ex)
        {
            logger.LogError("Exception: {exception}", ex.Message);
            return new DeployResponse
            {
                Id = request.Id,
                Status = DeployStatus.Failed,
                ErrorMessages = ["Internal Server Error."]
            };
        }
    }

    private bool CheckIfRunning(string directory, DeployRequest request, out List<string> errorMessages)
    {
        errorMessages = [];

        var argument = $"{(OperatingSystem.IsWindows() ? "/c" : "-c")} \"docker-compose -f {directory}/{request.File} ls --format json\"";

        logger.LogInformation("Running command: {command}", argument);

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh",
                Arguments = argument,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();

        process.WaitForExit();

        if (!string.IsNullOrEmpty(error))
        {
            logger.LogError("Error: {error}", error.Trim());
            errorMessages.Add("Did not list services correctly.");
            return false;
        }

        return output.Contains("running", StringComparison.InvariantCultureIgnoreCase);
    }

    private bool ComposeDown(string directory, DeployRequest request, out List<string> errorMessages)
    {
        errorMessages = [];

        var argument = $"{(OperatingSystem.IsWindows() ? "/c" : "-c")} \"docker-compose -f {directory}/{request.File} down\"";
        
        logger.LogInformation("Running command: {command}", argument);

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh",
                Arguments = argument,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        logger.LogInformation("Spinning down {service}.", request.Repository);

        string error = process.StandardError.ReadToEnd();

        process.WaitForExit();

        if (error.Contains("removed", StringComparison.InvariantCultureIgnoreCase) || string.IsNullOrWhiteSpace(error))
        {
            return true;
        }

        logger.LogError("Error: {error}", error.Trim());
        errorMessages.Add("Service did not stop correctly.");
        return false;
    }

    private DeployResponse ComposeUp(string directory,DeployRequest request, out List<string> errorMessages)
    {
        errorMessages = [];

        var argument = $"{(OperatingSystem.IsWindows() ? "/c" : "-c")} \"docker-compose -f {directory}/{request.File} up -d\"";

        logger.LogInformation("Running command: {command}", argument);

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh",
                Arguments = argument,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        logger.LogInformation("Spinning up {service}.", request.Repository);

        string error = process.StandardError.ReadToEnd();

        process.WaitForExit();

        if (!error.Contains("started", StringComparison.InvariantCultureIgnoreCase))
        {
            errorMessages.Add("Service did not start correctly.");
            return new DeployResponse
            {
                Id = request.Id,
                Status = DeployStatus.Failed,
                ErrorMessages = errorMessages
            };
        }

        return new DeployResponse
        {
            Id = request.Id,
            Status = DeployStatus.Success
        };
    }

    private async Task DockerLogin()
    {
        logger.LogInformation("Logging in to ECR.");

        var response = await ecrClient.GetAuthorizationTokenAsync(new GetAuthorizationTokenRequest());

        var token = response.AuthorizationData.FirstOrDefault().AuthorizationToken;
        var decodedToken = Encoding.UTF8.GetString(Convert.FromBase64String(token)).Split(':').Last();

        var argument = $"{(OperatingSystem.IsWindows() ? "/c" : "-c")} \"docker login --username AWS --password {decodedToken} {Environment.GetEnvironmentVariable("ECR_REGISTRY")}\"";

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh",
                Arguments = argument,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();

        process.WaitForExit();

        Console.WriteLine(output);
        Console.WriteLine(error);
    }
}
