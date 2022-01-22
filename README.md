# acr-cleanup

## What?

This is an Azure Function App project that provides the functionality of scheduled deletion of untagged manifests (images) from Azure Container Registries.

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

Before you can deploy this Function App to Azure, you will first need to create an Azure Function App in the Azure Portal or via the Azure CLI / Powershell Az Module. As runtime, choose .NET Core 3.1. For the plan, choose Consumption on Windows. In most cases Consumption will be the right choice since the cleanup function will only run once or a few times per day. If you want to see logs, you will also want to create an Application Insights instance in the Function App creation process.

After creating the Function App, __enable its system-assigned managed identity__.

For the actual deployment of this function you can go the manual or automatic way.

**Please note:** Depending on the Azure Functions version you are using, different .NET versions are available. For this reason, please use the `azfunc-v3` and `azfunc-v4` branches for deployments to Azure Functions version 3 and 4, respectively.

#### Automatic Deployment

With this deployment type, your Azure Function App will get the source from the GitHub repository, install required packages, and deploy the result to the production slot. Plus: If you want to update the app in the future, there is a handy Sync button available that will trigger a fresh deployment with the current code from GitHub.

To set this up, go to the Deployment Center, choose the External Git Code Source option, fill in https://github.com/michiwerner/acr-cleanup.git as the repository, and set branch to either `azfunc-v3` or `azfunc-v4`, depending on the Azure Functions version you are using. Then just hit the Save button and the app will trigger a deployment.

#### Manual Deployment

The first step is to either download the contents of this repository or clone them:
`git clone https://github.com/michiwerner/acr-cleanup.git`

For the actual deployment of the code - both first-time setup and updates - one of the easiest ways is to use Visual Studio Code. If you choose this option, please refer to https://docs.microsoft.com/en-us/azure/developer/javascript/tutorial/tutorial-vscode-serverless-node-deploy-hosting for a comprehensive guide.

If you don't want to use Visual Studio Code, you can deploy the code via command line as follows.

1. Make sure you have the Azure CLI tools installed. Then log in with `az login` and/or switch to the correct subscription using `az account set -s <SUBSCRIPTION_ID>`.
2. Install the Azure Functions Core Tools (https://www.npmjs.com/package/azure-functions-core-tools).
3. Read through the next section (Configuration) to find out if you need to change settings in the files before deployment.
4. Deploy the app by running `func azure functionapp publish <FunctionAppName>` where `FunctionAppName` is the name of the Azure Function App you created earlier. See https://docs.microsoft.com/en-us/azure/azure-functions/functions-run-local?tabs=linux%2Ccsharp%2Cbash#publish for further details.

### Configuration

To allow the app to actually access your ACR registries, you must do one of two things, depending on the scope you want the app to have:

1) Manually select the ACR's: Add two new role assignments to each relevant ACR and assign the AcrDelete and Reader roles to the Function App's system-assigned managed identity.
2) Automatically run on all ACR's in a scope: If you want the app to automatically pick up new registries without you having to assign roles first, you would need to assign the AcrDelete and Reader roles at a subscription or resource group level so that new ACR instances inherit the role assignment.

The last step is to set the schedule for the cleanup runs. For this, create an application setting `CLEANUP_SCHEDULE` and set it to a cron expression. Please refer to https://docs.microsoft.com/en-us/azure/azure-functions/functions-bindings-timer?tabs=csharp#ncrontab-expressions for help on the format. A warning for people who are used to the standard crontab format: The first column in the 6-column format used here are *SECONDS*, not minutes.