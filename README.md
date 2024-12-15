# Play.Identity
Identity Microservice

## Create and publish package
```powershell
$version="1.0.10"
$owner="Dot-Net-Micro-Services"
$gh_pat="[PAT HERE]"

dotnet pack src\Play.Identity.Contracts\ --configuration Release -p:PackageVersion=$version -p:RepositoryUrl=https://github.com/$owner/Play.Identity -o ..\packages

dotnet nuget push ..\packages\Play.Identity.Contracts.$version.nupkg --api-key $gh_pat --source "github"
```

## Build the docker image
```powershell
$env:GH_OWNER="Dot-Net-Micro-Services"
$env:GH_PAT="[PAT HERE]"
$acrname="playeconomyacrdev"
docker build --secret id=GH_OWNER --secret id=GH_PAT -t "$acrname.azurecr.io/play.identity:$version" .
```

## Run the docker image
```powershell
$adminPass="[PASSWORD HERE]"
$cosmosDbConnectionString="[CONNECTION STRING HERE]"
$serviceBusConnectionString="[CONNECTION STRING HERE]"
docker run -it --rm -p 5002:5002 --name identity -e  IdentitySettings__AdminUserPassword=$adminPass -e
MongoDbSettings__ConnectionString=$cosmosDbConnectionString -e
ServiceBusSettings__ConnectionString=$serviceBusConnectionString -e ServiceSettings__MessageBroker="SERVICEBUS" play.identity:$version
```

## Publish the docker image
```powershell
az acr login --name $acrname
docker push "$acrname.azurecr.io/play.identity:$version"
```

## Create the Kubernets namespace
```powershell
$namespace="identity"
kubectl create namespace $namespace
```

## Creating the Azure Managed Identity and granting it access to key vault secrets
```powershell
$appname="playeconomy"
$keyvaultname="playeconomy-vault-dev"
az identity create --resource-group $appname --name $namespace
$IDENTITY_CLIENT_ID=az identity show --resource-group $appname --name $namespace --query clientId -otsv
$SUBSCRIPTION_ID=az account show --query id
az role assignment create --assignee $IDENTITY_CLIENT_ID --role "Key Vault Secrets User" --scope "/subscriptions/$SUBSCRIPTION_ID/resourcegroups/$appname/providers/Microsoft.KeyVault/vaults/$keyvaultname"
```

## Establish the Federated Identity Credential
```powershell
$aksname="playeconomy-aks-dev"
$AKS_OIDC_ISSUER=az aks show --name $aksname --resource-group $appname --query "oidcIssuerProfile.issuerUrl" -otsv
az identity federated-credential create --name $namespace --identity-name $namespace --resource-group $appname --issuer $AKS_OIDC_ISSUER --subject "system:serviceaccount:${namespace}:${namespace}-serviceaccount"
```

## Install the helm chart
```powershell
helm install identity-service .\helm -f .\helm\values.yaml -n $namespace
```