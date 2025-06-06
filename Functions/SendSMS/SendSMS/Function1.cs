using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Twilio.Types;
using Twilio;
using System;
using System.Collections.Generic;
using Twilio.Rest.Api.V2010.Account;
using System.Net.NetworkInformation;
using System.Resources;
using Twilio.TwiML.Voice;
using Azure.Identity;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Compute.Models;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.ContainerInstance;
using Azure.Security.KeyVault.Secrets;

namespace SendSMS
{
    public class Function1
    {
        private readonly ILogger<Function1> _logger;

        public Function1(ILogger<Function1> logger)
        {
            _logger = logger;
        }

        [Function("sendSMS")]
        public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
        {
            string resourceId = req.Query["resourceid"];

            var KeyVaultUI = "https://akv-os-ccs.vault.azure.net/";
            var credential = new DefaultAzureCredential();
            var client = new SecretClient(new Uri(KeyVaultUI), credential);
            var armClient = new ArmClient(credential);

            if (!string.IsNullOrEmpty(resourceId))
            {
                string resourceString = $"/subscriptions/25b8b193-f48f-4ef7-be5c-0e97bf5c5737/resourceGroups/RG_OS_CCS/providers/Microsoft.ContainerInstance/containerGroups/{resourceId}";
                var containerGroupResource = armClient.GetContainerGroupResource(new ResourceIdentifier(resourceString));
                var operation = containerGroupResource.DeleteAsync(Azure.WaitUntil.Completed);
            }

            // Retrieve the twilio SID
            KeyVaultSecret sid = client.GetSecret("TWILIOSID");
            var accountSid = sid.Value;

            // Retrieve the twilio token for auth
            KeyVaultSecret token = client.GetSecret("TWILIOAUTHTOKEN");
            var authToken = token.Value;

            // And to not expose Leos Phone Number to strangers we also retrieve that
            KeyVaultSecret leoNummer = client.GetSecret("LeoPhoneNumber");
            var phoneNumber = leoNummer.Value;

            // Connect to the Twilio service
            TwilioClient.Init(accountSid, authToken);
            var messageOptions = new CreateMessageOptions(
              new PhoneNumber(phoneNumber));

            // Send from assigned source message
            messageOptions.From = new PhoneNumber("+18706148721");
            messageOptions.Body = $"Die Ressource mit der id {resourceId} wurde terminiert!";
            var message = MessageResource.Create(messageOptions);
            Console.WriteLine(message.Body);
            
            // Log results
            _logger.LogInformation("C# HTTP trigger function processed a request.");
            return new OkObjectResult("Message sent to Leo!");
        }
    }
}
