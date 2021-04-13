#!/bin/sh

az login --identity

for subscription_id in $(az account list -o tsv --query '[].id'); do
    for registry_name in $(az acr list -o tsv --subscription $subscription_id --query '[].name'); do
        for repository_name in $(az acr repository list -o tsv -n $registry_name); do
            for manifest_digest in $(az acr repository show-manifests --name $registry_name --repository $repository_name --query "[?tags[0]==null].digest" -o tsv); do
            | az acr repository delete --name $registry_name --image $repository_name@$manifest_digest --yes &
            done
            wait
        done
    done
done