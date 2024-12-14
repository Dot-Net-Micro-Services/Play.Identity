# Play.Identity
Identity Microservice

## Create and publish package
```powershell
$version="1.0.4"
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

## Create the Kubernetes secrets
```powershell
kubectl create secret generic identity-secrets 
--from-literal=cosmosdb-connectionstring=$cosmosDbConnectionString
--from-literal=servicebusconnectionstring=$serviceBusConnectionString
--from-literal=admin-password=$adminPass -n $namespace
```

## Create the Kubernets pod
```powershell
kubernetes apply -f .\kubernetes\identity.yaml -n $namespace
```