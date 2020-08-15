using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.Cosmos.Table;
using System.Collections.Generic;
using System.Linq;

namespace DeepSpaceNetwork
{
    public static class Transmission
    {
        [FunctionName("GetMessageHandler")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
            [Table("Authentication"), StorageAccount("Transmission")] CloudTable authentication,
            [Table("AuthenticatedCallbacks","Callback"), StorageAccount("Transmission")] CloudTable authenticatedCallbackLog,
            ILogger log)
        {
            log.LogInformation("Inbound message");
            TableQuery<Authentication> query = new TableQuery<Authentication>();
            string responseMessage = string.Empty;
            foreach (var queryField in req.Query)
            {
                query = new TableQuery<Authentication>().Where(
                    TableQuery.CombineFilters(
                        TableQuery.GenerateFilterCondition("CallbackKey", QueryComparisons.Equal, queryField.Key),
                        TableOperators.And,
                        TableQuery.GenerateFilterCondition("CallbackValue", QueryComparisons.Equal, queryField.Value)));
                var result = authentication.ExecuteQuery(query);
                if (result.Any())
                {
                    log.LogInformation("Authenticated callback received from: " + req.Host.ToString());
                    AuthenticatedCallBackLog authenticatedCallback = new AuthenticatedCallBackLog();
                    TableOperation logMessages;
                    foreach (Authentication authed in result)
                    {
                        authenticatedCallback.PartitionKey = authed.CallbackKey;
                        authenticatedCallback.RowKey = Guid.NewGuid().ToString() ;
                        authenticatedCallback.CallbackTime = DateTime.Now;
                        logMessages = TableOperation.InsertOrReplace(authenticatedCallback);
                        authenticatedCallbackLog.Execute(logMessages);
                        if (authed.ActivationStatus)
                        {
                            responseMessage = string.IsNullOrEmpty(authed.ActiveResponse)
                                ? authed.DefaultResponse
                                : authed.ActiveResponse;
                            return new OkObjectResult(responseMessage);
                        }
                    }
                }
            }

            return new RedirectResult("https://www.microsoft.com");
        }
    }

    public class Authentication : TableEntity
    {
        public string CallbackKey { get; set; }
        public string CallbackValue { get; set; }
        public bool ActivationStatus { get; set; }
        public string DefaultResponse { get; set; }
        public string ActiveResponse { get; set; }
    }

    public class AuthenticatedCallBackLog : TableEntity
    {
        public DateTime CallbackTime { get; set; }
    }
}
