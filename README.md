---
page_type: sample
languages:
- csharp
products:
- azure
description: "Sample to backup Managed Disks to another region using incremental snapshots"
urlFragment: "managed-disks-dotnet-backup-with-incremental-snapshots"
---

# Backup Azure Managed Disks to another region with differential capability of incremental snapshots

Incremental snapshots provide differential capability – a unique capability available only in Azure Managed Disks. It enables customers and independent solution vendors (ISV) to build backup and disaster recovery solutions for Managed Disks. It allows you to get the changes between two  snapshots of the same disk, thus copying only changed data between two snapshots across regions, reducing time and cost for backup and disaster recovery. Read more about incremental snapshots [here](http://go.microsoft.com/fwlink/?LinkId=330212)

In this sample, we demonstrate the following:
- How to fetch incremental snapshots created for a disk in an Azure resource group
- How to generate shared access signature (SAS) URI for a snapshot for getting the changes since the last snapshot 
- How to download the first incremental snapshot as a base blob in another region. 
- How to copy only the changes since the last snapshot to the base blob. 
- How to create snapshots on the base blob after the changes are copied. These snapshots represent your point in time backup of the disk in another region. 

## Prerequisites//Follow the instructions to generate the SAS token of a storage account https://docs.microsoft.com/en-us/azure/storage/common/storage-account-sas-create-dotnet

- If you don't have a Microsoft Azure subscription you can
get a FREE trial account [here](http://go.microsoft.com/fwlink/?LinkId=330212).
- Use the steps [here](https//docs.microsoft.com/en-us/azure/active-directory/develop/howto-create-service-principal-portal) to create a service principal in your subscription. Please note down the tenantId, applicationId and secret key for the service principal. It will be used in the sample
- Use the article [here](https://docs.microsoft.com/en-us/azure/virtual-machines/linux/disks-incremental-snapshots) to create incremental snapshots for a managed disk that you want to backup
- Create an Azure Storage account where you want to store your backups using the article [here](https://docs.microsoft.com/en-us/azure/storage/common/storage-quickstart-create-account?tabs=azure-portal)
- Follow the instructions [here](https://docs.microsoft.com/en-us/azure/storage/common/storage-account-sas-create-dotnet) to generate the SAS token of the storage account 

## Setup

Explain how to prepare the sample once the user clones or downloads the repository. The section should outline every step necessary to install dependencies and set up any settings (for example, API keys and output folders).

## Runnning the sample

Outline step-by-step instructions to execute the sample and see its output. Include steps for executing the sample from the IDE, starting specific services in the Azure portal or anything related to the overall launch of the code.

## Key concepts

Provide users with more context on the tools and services used in the sample. Explain some of the code that is being used and how services interact with each other.

## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
