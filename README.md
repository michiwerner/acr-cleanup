# acr-cleanup

## What?

This is an Azure Function App project that provides the functionality of deleting untagged manifests (images) from an Azure Container Registry. It supports both scheduled cleanups and cleanups triggered by image push events.

## Why?

First of all: In many cases you will NOT need this. Please review the following bullet points BEFORE using this project.

* Azure Container Registries come in different SKUs with different built-in capabilities. As of now (2021-02-20) the Premium SKU has an upcoming retention policy feature (currently in Preview) that allows you to configure the cleanup of untagged manifests (images) directly without any need for additional tools.
* By using Azure Container Registry Tasks, you can create tasks that will delete untagged manifests for repositories within an ACR. See https://docs.microsoft.com/en-us/azure/container-registry/container-registry-auto-purge for details. As of now (2021-02-20) you will have to add at least one filter line for every repository within a registry, though.
* The Azure CLI supports both listing and deleting of manifests. If you have a VM / external host running where you can execute and schedule a script, that is easily automatable.

The use case for this project is one where you want to have a single, serverless solution which can be configured to automatically pick up any new registries and repositories within its scope and start cleaning untagged manifests from those, and that will also work with Standard and Basic SKUs.

## How?

### DISCLAIMER

By using any contents of this project, in accordance with its license (see LICENSE for details) you implicitly agree to the following:

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

### Installation / Deployment

Before you can deploy this Function App to Azure, you will first need to create an Azure Function App in the Azure Portal or via the Azure CLI / Powershell Az Module. As runtime, choose Node.js in version 14 LTS. For the plan, in most cases Consumption will be the right choice since the cleanup functions will only run a few times a day. This is not true, though, if you either set the schedule for the scheduled cleanup to run very often (not recommended), or have a high number of image push events that trigger the cleanup-on-image-push function. In these cases, choosing a different plan option might help you avoid high costs. After creating the Funtion App, __enable its system-assigned managed identity__.

The next step is to either download the contents of this repository or clone them:
`git clone https://github.com/michiwerner/acr-cleanup.git`

For the actual deployment of the code - both first-time setup and updates - one of the easiest ways is to use Visual Studio Code. If you choose this option, please refer to https://docs.microsoft.com/en-us/azure/developer/javascript/tutorial/tutorial-vscode-serverless-node-deploy-hosting for a comprehensive guide.

If you don't want to use Visual Studio Code, you can deploy the code via command line as follows.

1. Make sure you have the Azure CLI tools installed. Then log in with `az login` and/or switch to the correct subscription using `az account set -s <SUBSCRIPTION_ID>`.
2. Install the Azure Functions Core Tools (https://www.npmjs.com/package/azure-functions-core-tools).
3. Read through the next section (Configuration) to find out if you need to change settings in the files before deployment.
4. Deploy the app by running `func azure functionapp publish <FunctionAppName>` where `FunctionAppName` is the name of the Azure Function App you created earlier. See https://docs.microsoft.com/en-us/azure/azure-functions/functions-run-local?tabs=linux%2Ccsharp%2Cbash#publish for further details.

### Configuration

To allow the app to actually access your ACR registries, you must do one of two things, depending on the scope you want the app to have:

1) Manually select the ACR's: Add a new role assignment to each relevant ACR and assign the AcrDelete role to the Function App's system-assigned managed identity.
2) Automatically run on all ACR's in a scope: If you want the app to automatically pick up new registries without you having to assign roles first, you would need to assign the AcrDelete role at a subscription or resource group level so that new ACR instances inherit the role assignment. Please keep in mind that the current version of this app is only tested for use with ACR instances that reside in the same Azure Subscription as the app itself.

Also review the `schedule` attribute inside the `scheduled-cleanup/function.json` file. The default is to run the function every day at 00:00:00 UTC. You can freely change this schedule, but remember that the more often the function runs, the more cost it will generate. Please refer to https://docs.microsoft.com/en-us/azure/azure-functions/functions-bindings-timer?tabs=csharp#ncrontab-expressions for help on the format. Make sure to (re-)deploy the app after making any changes to files.