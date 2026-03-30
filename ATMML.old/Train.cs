// This code requires the Nuget package Microsoft.AspNet.WebApi.Client to be installed.
// Instructions for doing this in Visual Studio:
// Tools -> Nuget Package Manager -> Package Manager Console
// Install-Package Microsoft.AspNet.WebApi.Client
//
// Also, add a reference to Microsoft.WindowsAzure.Storage.dll for reading from and writing to the Azure blob storage

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
//using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

//using Microsoft.WindowsAzure.Storage;
//using Microsoft.WindowsAzure.Storage.Auth;
//using Microsoft.WindowsAzure.Storage.Blob;

namespace ATMML
{
    public class AzureBlobDataReference
    {
        // Storage connection string used for regular blobs. It has the following format:
        // DefaultEndpointsProtocol=https;AccountName=ACCOUNT_NAME;AccountKey=ACCOUNT_KEY
        // It's not used for shared access signature blobs.
        public string ConnectionString { get; set; }

        // Relative uri for the blob, used for regular blobs as well as shared access 
        // signature blobs.
        public string RelativeLocation { get; set; }

        // Base url, only used for shared access signature blobs.
        public string BaseLocation { get; set; }

        // Shared access signature, only used for shared access signature blobs.
        public string SasBlobToken { get; set; }
    }

    public enum BatchScoreStatusCode
    {
        NotStarted,
        Running,
        Failed,
        Cancelled,
        Finished
    }

    public class BatchScoreStatus
    {
        // Status code for the batch scoring job
        public BatchScoreStatusCode StatusCode { get; set; }


        // Locations for the potential multiple batch scoring outputs
        public IDictionary<string, AzureBlobDataReference> Results { get; set; }

        // Error details, if any
        public string Details { get; set; }
    }

    public class BatchExecutionRequest
    {

        public IDictionary<string, AzureBlobDataReference> Inputs { get; set; }
        public IDictionary<string, string> GlobalParameters { get; set; }

        // Locations for the potential multiple batch scoring outputs
        public IDictionary<string, AzureBlobDataReference> Outputs { get; set; }
    }

    public class TrainModel
    {

        static async Task WriteFailedResponse(HttpResponseMessage response)
        {
            //System.Diagnostics.Debug.WriteLine(string.Format("The request failed with status code: {0}", response.StatusCode));

            // Print the headers - they include the requert ID and the timestamp, which are useful for debugging the failure
            //System.Diagnostics.Debug.WriteLine(response.Headers.ToString());

            string responseContent = await response.Content.ReadAsStringAsync();
            //System.Diagnostics.Debug.WriteLine(responseContent);
        }

        static void UploadFileToBlob(string inputFileLocation, string inputBlobName, string storageContainerName, string storageConnectionString)
        {
            // Make sure the file exists
            if (!File.Exists(inputFileLocation))
            {
                throw new FileNotFoundException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "File {0} doesn't exist on local computer.",
                        inputFileLocation));
            }

            //System.Diagnostics.Debug.WriteLine("Uploading the input to blob storage...");

            //CloudStorageAccount connection = CloudStorageAccount.Parse(storageConnectionString);

            //var blobClient = connection.CreateCloudBlobClient();

           // var container = blobClient.GetContainerReference(storageContainerName);
            //container.CreateIfNotExists();
            //var blob = container.GetBlockBlobReference(inputBlobName);
            //blob.UploadFromFile(inputFileLocation);
        }

        static void SaveBlobToFile(AzureBlobDataReference blobLocation, string resultsLabel)
        {
            const string OutputFileLocation = @"scripts\azure_output_data.csv"; // Replace this with the location you would like to use for your output file, and valid file extension (usually .csv for scoring results, or .ilearner for trained models)

            //var credentials = new StorageCredentials(blobLocation.SasBlobToken);
            //var blobUrl = new Uri(new Uri(blobLocation.BaseLocation), blobLocation.RelativeLocation);
            //var cloudBlob = new CloudBlockBlob(blobUrl, credentials);

            //Console.WriteLine(string.Format("Reading the result from {0}", blobUrl.ToString()));
            //cloudBlob.DownloadToFile(OutputFileLocation, FileMode.Create);

            Console.WriteLine(string.Format("{0} have been written to the file {1}", resultsLabel, OutputFileLocation));
        }

        static void ProcessResults(BatchScoreStatus status)
        {
            bool first = true;
            foreach (var output in status.Results)
            {
                var blobLocation = output.Value;
                Console.WriteLine(string.Format("The result '{0}' is available at the following Azure Storage location:", output.Key));
                Console.WriteLine(string.Format("BaseLocation: {0}", blobLocation.BaseLocation));
                Console.WriteLine(string.Format("RelativeLocation: {0}", blobLocation.RelativeLocation));
                Console.WriteLine(string.Format("SasBlobToken: {0}", blobLocation.SasBlobToken));
                Console.WriteLine();

                // Save the first output to disk
                if (first)
                {
                    first = false;
                    SaveBlobToFile(blobLocation, string.Format("The results for {0}", output.Key));
                }
            }
        }

        public static async Task InvokeBatchExecutionService(bool train, string endPnt, string apiKey, string inputFileName)
        {
            // How this works:
            //
            // 1. Assume the input is present in a local file (if the web service accepts input)
            // 2. Upload the file to an Azure blob - you'd need an Azure storage account
            // 3. Call the Batch Execution Service to process the data in the blob. Any output is written to Azure blobs.
            // 4. Download the output blob, if any, to local file

            const string StorageAccountName = "atmmlstudiostorage"; // Replace this with your Azure Storage Account name
            const string StorageAccountKey = "wO/zRzWq451hVZqoBs9ync9Ml0zjg0KZtavGO9FAq6J3+YTQTESaCxTLcxpesIce81RIqvMaoC6cncJxnacZNA=="; // Replace this with your Azure Storage Key
            const string StorageContainerName = "uploadedresources"; // Replace this with your Azure Storage Container name
            string storageConnectionString = string.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}", StorageAccountName, StorageAccountKey);

            // set a time out for polling status
            const int TimeOutInMilliseconds = 240 * 1000; // Set a timeout of 4 minutes

            UploadFileToBlob(inputFileName /*Replace this with the location of your input file*/,
                "Azure_input_data.csv" /*Replace this with the name you would like to use for your Azure blob; this needs to have the same extension as the input file */,
                StorageContainerName, storageConnectionString);
       
            using (HttpClient client = new HttpClient())
            {
                var request = new BatchExecutionRequest();
                request.Inputs = new Dictionary<string, AzureBlobDataReference>();
                request.Inputs["input1"] = new AzureBlobDataReference() { ConnectionString = storageConnectionString, RelativeLocation = string.Format("{0}/Azure_input_data.csv", StorageContainerName) };
                request.Outputs = new Dictionary<string, AzureBlobDataReference>();
                if (train) request.Outputs["output2"] = new AzureBlobDataReference() { ConnectionString = storageConnectionString, RelativeLocation = string.Format("/{0}/output2results.ilearner", StorageContainerName) };
                request.Outputs["output1"] = new AzureBlobDataReference() { ConnectionString = storageConnectionString, RelativeLocation = string.Format("/{0}/output1results.csv", StorageContainerName) };
                request.GlobalParameters = new Dictionary<string, string>();

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                //System.Diagnostics.Debug.WriteLine("Submitting the job...");

                // submit the job
                //var response = await client.PostAsJsonAsync(endPnt + "?api-version=2.0", request);
                //if (!response.IsSuccessStatusCode)
                //{
                //    await WriteFailedResponse(response);
                //    return;
                //}

                //string jobId = await response.Content.ReadAsAsync<string>();
                //System.Diagnostics.Debug.WriteLine(string.Format("Job ID: {0}", jobId));


                // start the job
                //System.Diagnostics.Debug.WriteLine("Starting the job...");
                //response = await client.PostAsync(endPnt + "/" + jobId + "/start?api-version=2.0", null);
                //if (!response.IsSuccessStatusCode)
                //{
                //    await WriteFailedResponse(response);
                //    return;
                //}

                //string jobLocation = endPnt + "/" + jobId + "?api-version=2.0";
                //Stopwatch watch = Stopwatch.StartNew();
                //bool done = false;
                //while (!done)
                //{
                //    //System.Diagnostics.Debug.WriteLine("Checking the job status...");
                //    response = await client.GetAsync(jobLocation);
                //    if (!response.IsSuccessStatusCode)
                //    {
                //        await WriteFailedResponse(response);
                //        return;
                //    }

                //    BatchScoreStatus status = await response.Content.ReadAsAsync<BatchScoreStatus>();
                //    if (watch.ElapsedMilliseconds > TimeOutInMilliseconds)
                //    {
                //        done = true;
                //        //System.Diagnostics.Debug.WriteLine(string.Format("Timed out. Deleting job {0} ...", jobId));
                //        await client.DeleteAsync(jobLocation);
                //    }
                //    switch (status.StatusCode) {
                //        case BatchScoreStatusCode.NotStarted:
                //            //System.Diagnostics.Debug.WriteLine(string.Format("Job {0} not yet started...", jobId));
                //            break;
                //        case BatchScoreStatusCode.Running:
                //            //System.Diagnostics.Debug.WriteLine(string.Format("Job {0} running...", jobId));
                //            break;
                //        case BatchScoreStatusCode.Failed:
                //            //System.Diagnostics.Debug.WriteLine(string.Format("Job {0} failed!", jobId));
                //            //System.Diagnostics.Debug.WriteLine(string.Format("Error details: {0}", status.Details));
                //            done = true;
                //            break;
                //        case BatchScoreStatusCode.Cancelled:
                //            //System.Diagnostics.Debug.WriteLine(string.Format("Job {0} cancelled!", jobId));
                //            done = true;
                //            break;
                //        case BatchScoreStatusCode.Finished:
                //            done = true;
                //            //System.Diagnostics.Debug.WriteLine(string.Format("Job {0} finished!", jobId));

                //            ProcessResults(status);
                //            break;
                //    }

                //    if (!done) {
                //        Thread.Sleep(1000); // Wait one second
                //    
                ///}
            }
        }
    }
}

