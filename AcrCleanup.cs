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
    public class AcrCleanup
    {
        private DefaultAzureCredential DefaultCredential;
        private string DefaultToken;
        private TokenCredentials DefaultTokenCredentials;
        private AzureCredentials DefaultAzureCredentials;
        private ILogger Log;

        [FunctionName("AcrCleanup")]
        public static void Run([TimerTrigger("%CLEANUP_SCHEDULE%")] TimerInfo myTimer, ILogger log)
        {
            var cleanup = new AcrCleanup(log);
            cleanup.ProcessAllSubscriptions();
        }

        private AcrCleanup(ILogger log)
        {
            Log = log;
            DefaultCredential = new DefaultAzureCredential();
            DefaultToken = DefaultCredential.GetToken(new TokenRequestContext(new[] { "https://management.azure.com/.default" })).Token;
            DefaultTokenCredentials = new TokenCredentials(DefaultToken);
            DefaultAzureCredentials = new AzureCredentials(DefaultTokenCredentials, DefaultTokenCredentials, null, AzureEnvironment.AzureGlobalCloud);
        }

        private void ProcessAllSubscriptions()
        {
            var azure = Microsoft.Azure.Management.Fluent.Azure.Configure().Authenticate(DefaultAzureCredentials).WithDefaultSubscription();
            var subscriptions = azure.Subscriptions.List();
            var tasks = new List<Task>();
            foreach (var subscription in subscriptions)
            {
                tasks.Add(Task.Run(() => ProcessSubscription(subscription)));
            }
            try
            {
                Task.WaitAll(tasks.ToArray());
            }
            catch (AggregateException ae)
            {
                foreach (var e in ae.InnerExceptions)
                {
                    Log.LogError(e.Message);
                }
            }
        }

        private void ProcessSubscription(ISubscription subscription)
        {
            Log.LogInformation($"Processing subscription {subscription.SubscriptionId} ...");
            var azure = Microsoft.Azure.Management.Fluent.Azure.Configure().Authenticate(DefaultAzureCredentials).WithSubscription(subscription.SubscriptionId);
            var containerRegistries = azure.ContainerRegistries.List();
            var tasks = new List<Task>();
            foreach (var containerRegistry in containerRegistries)
            {
                tasks.Add(Task.Run(() => ProcessContainerRegistry(containerRegistry)));
            }
            try
            {
                Task.WaitAll(tasks.ToArray());
            }
            catch (AggregateException ae)
            {
                foreach (var e in ae.InnerExceptions)
                {
                    Log.LogError(e.Message);
                }
            }
        }

        private void ProcessContainerRegistry(IRegistry containerRegistry)
        {
            Log.LogInformation($"Processing container registry {containerRegistry.Name} ...");
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
                tasks.Add(Task.Run(() => ProcessRepository(client, repositoryName)));
            }
            try
            {
                Task.WaitAll(tasks.ToArray());
            }
            catch (AggregateException ae)
            {
                foreach (var e in ae.InnerExceptions)
                {
                    Log.LogError(e.Message);
                }
            }
        }

        private void ProcessRepository(ContainerRegistryClient client, string repositoryName)
        {
            Log.LogInformation($"Processing repository {repositoryName} ...");
            var tasks = new List<Task>();
            Azure.Containers.ContainerRegistry.ContainerRepository repository = client.GetRepository(repositoryName);
            var manifests = repository.GetAllManifestProperties();
            foreach (var manifest in manifests)
            {
                if (manifest.Tags.Count < 1)
                {
                    Log.LogInformation($"Found untagged manifest {repositoryName}:{manifest.Digest}. Will delete.");
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
                    Log.LogError(e.Message);
                }
            }
        }
    }
}
