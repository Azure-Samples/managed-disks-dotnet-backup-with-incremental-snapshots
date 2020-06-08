using Microsoft.Azure.Management.Compute;
using Microsoft.Azure.Management.Compute.Models;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Rest;
using Microsoft.Rest.Azure;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Blobs.Models;
using Azure;


//----------------------------------------------------------------------------------

// Microsoft Developer & Platform Evangelism

//

// Copyright (c) Microsoft Corporation. All rights reserved.

//

// THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, 

// EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED WARRANTIES 

// OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE.

//----------------------------------------------------------------------------------

// The example companies, organizations, products, domain names,

// e-mail addresses, logos, people, places, and events depicted

// herein are fictitious.  No association with any real company,

// organization, product, domain name, email address, logo, person,

// places, or events is intended or should be inferred.

//----------------------------------------------------------------------------------


namespace BackupManagedDisksWithIncrementalSnapshots
{
    class Program
    {
        private static int OneMegabyteAsBytes = 1024 * 1024;
        private static int FourMegabyteAsBytes = 4 * OneMegabyteAsBytes;

        static async Task Main(string[] args)
        {
            //The subscription Id where the incremental snapshots of the managed disk are created
            string subscriptionId = "yourSubscriptionId";

            //The resource group name where incremental snapshots of the managed disks are created
            string resourceGroupName = "yourResourceGroupName";

            //The name of the disk that is backed up with incremental snapshots in the source region
            string diskName = "yourManagedDiskName";

            //The name of the storage account in the target region where incremental snapshots from source region are copied to a base blob. 
            string targetStorageAccountName = "yourTargetStorageAccountName";

            
            string targetStorageConnectionString = "targetStorageConnectionString";

            //the name of the container where base blob is stored on the target storage account
            string targetContainerName = "yourcontainername";

            //the name of the base VHD (blob) used for storing the backups in the target storage account 
            string targetBaseBlobName = "yourtargetbaseblobname.vhd";

            //Get the incremental snapshots associated 
            //The incremental snapshots are already sorted in the ascending order of the created datetime
            List<Snapshot> incrementalSnapshots = await GetIncrementalSnapshots(subscriptionId, resourceGroupName, diskName);

            //Get the SAS URI of the first snapshot that will be used to copy it to the back Storage account
            string firstSnapshotSASURI = GetSASURI(subscriptionId, resourceGroupName, incrementalSnapshots[0].Name);

            //Instantiate the page blob client
            PageBlobClient targetBaseBlobClient = await InstantiateBasePageBlobClient(targetStorageConnectionString, targetContainerName, targetBaseBlobName);

            //Copy the first snapshot as base blob in the target storage account 
            await CopyFirstSnapshotToBackupStorageAccount(targetBaseBlobClient, firstSnapshotSASURI);

            //The first incremental snapshot is the previous snapshot for the second incremental snapshot
            string previousSnapshotSASUri = firstSnapshotSASURI;

            //Remove the first snapshot as it is already copied to the base blob. 
            incrementalSnapshots.Remove(incrementalSnapshots[0]);

            //Loop through each incremental snapshots from second to the last 
            foreach (var isnapshot in incrementalSnapshots)
            {
                //Get the SAS URI of the current snapshot
                string currentSnapshotSASURI = GetSASURI(subscriptionId, resourceGroupName, isnapshot.Name);

                //Copy the changes since the last snapshot to the target base blob 
                await CopyChangesSinceLastSnapshotToBaseBlob(targetBaseBlobClient, previousSnapshotSASUri, currentSnapshotSASURI);

                //Set the current snapshot as the previous snapshot for the next snapshot
                previousSnapshotSASUri = currentSnapshotSASURI;
            }

        }

        /// <summary>
        /// This method copies the changes since the last snapshot to the target base blob 
        /// </summary>
        /// <param name="backupStorageAccountBlobClient">An instance of BlobClient which represents the storage account where the base blob is stored.</param>
        /// <param name="targetContainerName">The name of container in the target storage account where the base blob is stored</param>
        /// <param name="targetBaseBlobName">The name of the base blob used for storing the backups in the target storage account </param>
        /// <param name="lastSnapshotSASUri">The SAS URI of the last incremental snapshot</param>
        /// <param name="currentSnapshotSASUri">The SAS URI of the current snapshot</param>
        /// <returns></returns>
        private static async Task CopyChangesSinceLastSnapshotToBaseBlob(PageBlobClient targetBaseBlob, string lastSnapshotSASUri, string currentSnapshotSASUri)
        {

            PageBlobClient snapshot = new PageBlobClient(new Uri(currentSnapshotSASUri));

            //Get the changes since the last incremental snapshots of the managed disk.
            //GetManagedDiskDiffAsync is a new method introduced to get the changes since the last snapshot
            PageRangesInfo pageRanges = await snapshot.GetManagedDiskPageRangesDiffAsync(null, "",new Uri(lastSnapshotSASUri), null);

            foreach (var range in pageRanges.PageRanges)
            {   
                    Int64 rangeSize = (Int64)(range.Length);

                    // Chop a range into 4MB chunchs
                    for (Int64 subOffset = 0; subOffset < rangeSize; subOffset += FourMegabyteAsBytes)
                    {
                        int subRangeSize = (int)Math.Min(rangeSize - subOffset, FourMegabyteAsBytes);

                        HttpRange sourceRange = new HttpRange(subOffset, subRangeSize);

                        //When you use WritePagesAsync by passing the SAS URI of the source snapshot, the SDK uses Put Page From URL rest API: https://docs.microsoft.com/en-us/rest/api/storageservices/put-page-from-url
                        //When this API is invoked, the Storage service reads the data from source and copies the data to the target blob without requiring clients to buffer the data. 
                        await targetBaseBlob.UploadPagesFromUriAsync(new Uri(currentSnapshotSASUri), sourceRange, sourceRange, null, null, null, CancellationToken.None);

                    }
            }

            await targetBaseBlob.CreateSnapshotAsync();

        }

        /// <summary>
        /// Instantiate an instance of BlobClient which is used to perform common operations such as creating containers, blobs e.t.c. in a storage account
        /// </summary>
        /// <param name="storageAccountName">The name of a storage account</param>
        /// <param name="storageAccountSASToken">The SAS token of a storage account</param>
        /// <returns></returns>
        private static async Task<PageBlobClient> InstantiateBasePageBlobClient(string connectionString, string targetContainerName, string targetBaseBlobName)
        {
            BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);

            //Create the target container if not already exist 
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(targetContainerName);

            await containerClient.CreateIfNotExistsAsync();

            PageBlobClient targetBaseBlob = containerClient.GetPageBlobClient(targetBaseBlobName);
     
            return targetBaseBlob;
        }


        /// <summary>
        /// This method copies the first incremental snapshot as a base blob in the target region 
        /// </summary>
        /// <param name="backupStorageAccountBlobClient">An instance of BlobClient which represents the storage account where the base blob is stored.</param>
        /// <param name="targetContainerName">The name of container in the target storage account where the base blob is stored</param>
        /// <param name="targetBaseBlobName">The name of the base blob used for storing the backups in the target storage account </param>
        /// <param name="sourceSnapshotSASUri">The SAS URI of the source snapshot</param>
        /// <returns></returns>
        private static async Task CopyFirstSnapshotToBackupStorageAccount(PageBlobClient targetBaseBlob, string sourceSnapshotSASUri)
        {
            
            PageBlobClient sourceSnapshot = new PageBlobClient(new Uri(sourceSnapshotSASUri));

            //Get the size of the source snapshot
            var snapshotProperties = await sourceSnapshot.GetPropertiesAsync();

            long sourceSnapshotSize = snapshotProperties.Value.ContentLength;

            //Create the target base blob with the same size as the source snapshot
            await targetBaseBlob.CreateAsync(sourceSnapshotSize);

            //Snapshots are stored as page blobs under the hood
            //Get all the valid page ranges from the source snapshot. 
            //Learn more about page blobs and page ranges:
            //https://docs.microsoft.com/en-us/azure/storage/blobs/storage-blob-pageblob-overview
            ///https://blogs.msdn.microsoft.com/windowsazurestorage/2012/03/26/getting-the-page-ranges-of-a-large-page-blob-in-segments/
            ///https://docs.microsoft.com/en-us/rest/api/storageservices/get-page-ranges
            PageRangesInfo pageRanges = await sourceSnapshot.GetPageRangesAsync();

            await WritePageRanges(sourceSnapshotSASUri, targetBaseBlob, pageRanges);

            await targetBaseBlob.CreateSnapshotAsync();

        }

        /// <summary>
        /// This method writes the page ranges from the source snapshot in the source region to the target base blob in the target region
        /// </summary>
        /// <param name="sourceSnapshotSASUri">The SAS URI of the source snapshot</param>
        /// <param name="targetBaseBlob">An instance of CloudPageBlob which represents the target base blob</param>
        /// <param name="pageRanges">Page ranges on the source snapshots that have changed since the last snapshot</param>
        /// <returns></returns>
        private static async Task WritePageRanges(string sourceSnapshotSASUri, PageBlobClient targetBaseBlob, PageRangesInfo pageRangeInfo)
        {
            foreach (HttpRange range in pageRangeInfo.PageRanges)
            {
                Int64 rangeSize = (Int64)(range.Length);

                // Chop a range into 4MB chunchs
                for (Int64 subOffset = 0; subOffset < rangeSize; subOffset += FourMegabyteAsBytes)
                {
                    int subRangeSize = (int)Math.Min(rangeSize - subOffset, FourMegabyteAsBytes);

                    HttpRange sourceRange = new HttpRange(subOffset, subRangeSize);

                    await targetBaseBlob.UploadPagesFromUriAsync(new Uri(sourceSnapshotSASUri), sourceRange, sourceRange, null, null, null, CancellationToken.None);

                }
            }
        }


        /// <summary>
        /// This method generates a SAS URI for a snapshot
        /// SAS URI can be used to download the underlying data of the snapshot or to get the changes since the last snapshot
        /// </summary>
        /// <param name="subscriptionId">Your subscriptionId</param>
        /// <param name="resourceGroupName">The name of the resource group where incremental snapshots are created</param>
        /// <param name="snapshotName">The name of the snapshot</param>
        /// <returns></returns>
        private static string GetSASURI(string subscriptionId, string resourceGroupName, string snapshotName)
        {

            var credential = GetClientCredential();

            using (var computeClient = new ComputeManagementClient(credential))
            {
                computeClient.SubscriptionId = subscriptionId;

                //Expiry is set as one day. 
                //If you expect the data size is large then please update it to higher value
                int sasExpiryInSec = 60 * 24 * 60 * 60;

                // Setting the access to Read generates a read-only SAS that can be used for downloading or reading the snapshot data.
                GrantAccessData grantAccessData = new GrantAccessData("Read", sasExpiryInSec);

                var getSasResponse = computeClient.Snapshots.GrantAccess(resourceGroupName, snapshotName, grantAccessData);

                return getSasResponse.AccessSAS;
            }

        }

        /// <summary>
        /// This method returns a list of incremental snapshots created for a managed disk in a resource group
        /// You can identify incremental snapshots of the same disk by using the SourceResourceId and SourceUniqueId properties of snapshots. 
        /// SourceResourceId is the Azure Resource Manager (ARM) resource Id of the parent disk. 
        /// SourceUniqueId is the value inherited from the UniqueId property of the disk. 
        /// If you delete a disk and then create a disk with the same name, the value of the UniqueId property will change. 
        /// </summary>
        /// <param name="subscriptionId">Your subscriptionId</param>
        /// <param name="resourceGroupName">The name of the resource group where incremental snapshots are created</param>
        /// <param name="diskName">The name of the parent disk which is backed by the incremental snapshots</param>
        /// <returns></returns>
        private static async Task<List<Snapshot>> GetIncrementalSnapshots(string subscriptionId, string resourceGroupName, string diskName)
        {
            var credential = GetClientCredential();


            List<Snapshot> incrementalSnapshots = new List<Snapshot>();
            
            using (var computeClient = new ComputeManagementClient(credential))
            {
                computeClient.SubscriptionId = subscriptionId;

                //Get the parent disk
                Disk disk = await computeClient.Disks.GetAsync(resourceGroupName, diskName);

                //Get all the snapshots in the resource group
                IPage<Snapshot> snapshots = await computeClient.Snapshots.ListByResourceGroupAsync(resourceGroupName);

                //Loop through each snapshot
                foreach (var snapshot in snapshots)
                {
                    //filter out full snapshots and incremental snapshots that are not created from the disk
                    if (snapshot.Incremental == true && snapshot.CreationData.SourceResourceId == disk.Id && snapshot.CreationData.SourceUniqueId == disk.UniqueId)
                    {
                        incrementalSnapshots.Add(snapshot);
                    }
                }
            }

            return incrementalSnapshots.OrderBy(s => s.TimeCreated).ToList();

        }

        /// <summary>
        /// This method generates a credential using the identity of a service principal associated with your subscription.
        /// The generated credential is used by the Azure SDK to authenticate itself and perform operations on your Azure resources in your subscription. 
        /// Use the steps in the article below to create a service principal in your subscription. It will help you to get tenantId, applicationId and secret key for the service principal. 
        /// https//docs.microsoft.com/en-us/azure/active-directory/develop/howto-create-service-principal-portal
        /// </summary>
        /// <returns></returns>
        private static ServiceClientCredentials GetClientCredential()
        {

            string tenantId = "";
            var applicationId = "";
            var applicatioSecretKey = "";

            var context = new Microsoft.IdentityModel.Clients.ActiveDirectory.AuthenticationContext("https://login.windows.net/" + tenantId);

            ClientCredential clientCredential = new ClientCredential(applicationId, applicatioSecretKey);

            var tokenResponse = context.AcquireTokenAsync("https://management.azure.com/", clientCredential).Result;

            var accessToken = tokenResponse.AccessToken;

            return new TokenCredentials(accessToken);
        }

        private static string Megabytes(long bytes)
        {
            return (bytes / OneMegabyteAsBytes).ToString() + " MB";
        }
    }
}

