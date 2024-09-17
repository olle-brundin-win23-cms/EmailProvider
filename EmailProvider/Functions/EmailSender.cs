using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Azure;
using Azure.Communication.Email;
using Azure.Messaging.ServiceBus;
using EmailProvider.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace EmailProvider.Functions
{
    public class EmailSender
    {
        private readonly ILogger<EmailSender> _logger;
        private readonly EmailClient _emailClient;

        public EmailSender(ILogger<EmailSender> logger, EmailClient emailClient)
        {
            _logger = logger;
            _emailClient = emailClient;
        }

        [Function(nameof(EmailSender))]
        public async Task Run(
            [ServiceBusTrigger("email_request", Connection = "ServiceBusConnection")]
            ServiceBusReceivedMessage message,
            ServiceBusMessageActions messageActions)
        {
            try
            {
                var emailRequest = UnpackRequest(message);

                if (emailRequest != null && !string.IsNullOrEmpty(emailRequest.To))
                {
                    if (SendEmail(emailRequest))
                    {
                        await messageActions.CompleteMessageAsync(message);
                    }
                }
            }

            catch (Exception ex)
            {
                _logger.LogError($"Error in Run. {ex.Message}");
            }
        }

        public EmailRequestModel UnpackRequest(ServiceBusReceivedMessage message)
        {
            try
            {
                var request = JsonConvert.DeserializeObject<EmailRequestModel>(message.Body.ToString());

                if (request != null)
                {
                    return request;
                }
            }

            catch (Exception ex)
            {
                _logger.LogError($"Error in Unpack. {ex.Message}");
            }

            return null!;
        }

        public bool SendEmail(EmailRequestModel request)
        {
            try
            {
                var result = _emailClient.Send(
                        WaitUntil.Completed,

                        senderAddress: Environment.GetEnvironmentVariable("SenderAddress"),
                        recipientAddress: request.To,
                        subject: request.Subject,
                        htmlContent: request.HtmlContent,
                        plainTextContent: request.PlainText);

                if (result.HasCompleted)
                {
                    return true;
                }
            }

            catch (Exception ex)
            {
                _logger.LogError($"Error in Send. {ex.Message}");
            }

            return false;
        }
    }
}
