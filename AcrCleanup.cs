using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Rest;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.ContainerRegistry.Fluent;
using Azure.Core;
using Azure.Identity;
using Azure.Containers.ContainerRegistry;

namespace AcrCleanup
{
    public static class AcrCleanup
    {
        private static DefaultAzureCredential DefaultCredential;
        private static string DefaultToken;
        private static TokenCredentials DefaultTokenCredentials;
        private static AzureCredentials DefaultAzureCredentials;

        [FunctionName("AcrCleanup")]
        public static void Run([TimerTrigger("%CLEANUP_SCHEDULE%")] TimerInfo myTimer, ILogger log)
        {
            DefaultCredential = new DefaultAzureCredential();
            DefaultToken = DefaultCredential.GetToken(new TokenRequestContext(new[] { "https://management.azure.com/.default" })).Token;
            DefaultTokenCredentials = new TokenCredentials(DefaultToken);
            DefaultAzureCredentials = new AzureCredentials(DefaultTokenCredentials, DefaultTokenCredentials, null, AzureEnvironment.AzureGlobalCloud);
            var azure = Microsoft.Azure.Management.Fluent.Azure.Configure().Authenticate(DefaultAzureCredentials).WithDefaultSubscription();
            var subscriptions = azure.Subscriptions.List();
            var tasks = new List<Task>();
            foreach (var subscription in subscriptions)
            {
                tasks.Add(Task.Run(() => ProcessSubscription(subscription, log)));
            }
            try
            {
                Task.WaitAll(tasks.ToArray());
            }
            catch (AggregateException ae)
            {
                foreach (var e in ae.InnerExceptions)
                {
                    log.LogError(e.Message);
                }
            }
        }
        private static void ProcessSubscription(ISubscription subscription, ILogger log)
        {
            log.LogInformation($"Processing subscription {subscription.SubscriptionId} ...");
            var azure = Microsoft.Azure.Management.Fluent.Azure.Configure().Authenticate(DefaultAzureCredentials).WithSubscription(subscription.SubscriptionId);
            var containerRegistries = azure.ContainerRegistries.List();
            var tasks = new List<Task>();
            foreach (var containerRegistry in containerRegistries)
            {
                tasks.Add(Task.Run(() => ProcessContainerRegistry(containerRegistry, log)));
            }
            try
            {
                Task.WaitAll(tasks.ToArray());
            }
            catch (AggregateException ae)
            {
                foreach (var e in ae.InnerExceptions)
                {
                    log.LogError(e.Message);
                }
            }
        }

        private static void ProcessContainerRegistry(IRegistry containerRegistry, ILogger log)
        {
            log.LogInformation($"Processing container registry {containerRegistry.Name} ...");
            var clientOptions = new ContainerRegistryClientOptions()
            {
                Retry =
                {
                    Delay = TimeSpan.FromSeconds(2),
                    MaxDelay = TimeSpan.FromSeconds(30),
                    MaxRetries = 25,
                    Mode = RetryMode.Exponential
                },
                Audience = ContainerRegistryAudience.AzureResourceManagerPublicCloud
            };
            var client = new ContainerRegistryClient(new Uri($"https://{containerRegistry.Name}.azurecr.io"), DefaultCredential, clientOptions);
            var repositoryNames = client.GetRepositoryNames();
            var tasks = new List<Task>();
            foreach (var repositoryName in repositoryNames)
            {
                tasks.Add(Task.Run(() => ProcessRepository(client, repositoryName, log)));
            }
            try
            {
                Task.WaitAll(tasks.ToArray());
            }
            catch (AggregateException ae)
            {
                foreach (var e in ae.InnerExceptions)
                {
                    log.LogError(e.Message);
                }
            }
        }

        private static void ProcessRepository(ContainerRegistryClient client, string repositoryName, ILogger log)
        {
            log.LogInformation($"Processing repository {repositoryName} ...");
            var tasks = new List<Task>();
            Azure.Containers.ContainerRegistry.ContainerRepository repository = client.GetRepository(repositoryName);
            var manifests = repository.GetAllManifestProperties();
            foreach (var manifest in manifests)
            {
                if (manifest.Tags.Count < 1)
                {
                    log.LogInformation($"Found untagged manifest {repositoryName}:{manifest.Digest}. Will delete.");
                    tasks.Add(client.GetArtifact(repositoryName, manifest.Digest).DeleteAsync());
                }
            }
            try
            {
                Task.WaitAll(tasks.ToArray());
            }
            catch (AggregateException ae)
            {
                foreach (var e in ae.InnerExceptions)
                {
                    log.LogError(e.Message);
                }
            }
        }
    }
}
