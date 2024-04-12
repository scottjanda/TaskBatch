using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace TaskBatch
{
    public class Function1
    {
        private readonly IConfiguration _config;

        public Function1(IConfiguration config)
        {
            _config = config;
        }

        [FunctionName("Function1")]
        public void Run([TimerTrigger("0 45 6 * * *")] TimerInfo myTimer, ILogger log)
        {
            // Get Azure Key Vault URL from app settings
            var keyVaultUrl = _config["KeyVaultURL"];

            // Create a new SecretClient using the default Azure credentials
            var client = new SecretClient(new Uri(keyVaultUrl), new DefaultAzureCredential());

            // Retrieve the secret containing the connection string
            var secretName = "PRODDBConnectionString";
            var secret = client.GetSecret(secretName);

            var connectionString = secret.Value.Value;

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                using (var command = new SqlCommand("USP_Batch", conn))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.ExecuteNonQuery();
                }
            }

            string selectStatement = "SELECT * FROM TaskItem";

            var table = new DataTable();

            using (var da = new SqlDataAdapter(selectStatement, connectionString))
            {
                da.Fill(table);
            }

            List<TaskItem> taskList = new();

            taskList = (from DataRow dr in table.Rows
                        select new TaskItem()
                        {
                            UserEmail = dr["UserEmail"].ToString(),
                            Description = dr["Description"].ToString(),
                            Details = dr["Details"].ToString(),
                            DueDate = Convert.ToDateTime(dr["DueDate"]),
                            FrequencyType = dr["FrequencyType"].ToString(),
                            FrequencyNumber = Convert.ToInt32(dr["FrequencyNumber"]),
                            Sensative = Convert.ToInt32(dr["Sensative"]),
                            LastCompleted = dr["LastCompleted"] != DBNull.Value ? Convert.ToDateTime(dr["LastCompleted"]) : (DateTime?)null
                        }).ToList();

            List<TaskItem> orderedTaskList = taskList
                .Where(x => x.LastCompleted == null)
                .OrderBy(x => x.DueDate)
                .ToList();

            // Retrieve SMTP password from Azure Key Vault
            var smtpPasswordSecretName = "PRODSMTPPassword"; // Replace with your secret name
            var smtpPasswordSecret = client.GetSecret(smtpPasswordSecretName);
            var smtpPassword = smtpPasswordSecret.Value.Value;

            // SMTP client configuration
            var smtpClient = new SmtpClient("smtp-relay.brevo.com")
            {
                Port = 587,
                Credentials = new NetworkCredential("scottjanda@gmail.com", smtpPassword), // Use the retrieved password
                EnableSsl = true,
            };


            // Email body construction
            var emailBody = new StringBuilder();
            emailBody.Append(@"<a href=""taskappmono.azurewebsites.net"" style=""font-size: 16px;"">Application Link</a><br>");
            emailBody.Append("<h1>Upcoming Tasks:</h1>");

            foreach (var item in orderedTaskList)
            {
                DateTime twoWeeksFromNow = DateTime.Now.AddDays(14);

                if (item.DueDate <= twoWeeksFromNow && item.Sensative == 0)
                {
                    emailBody.Append(item.DueDate <= DateTime.Now ? $"<h2 style=color:red;>{item.Description}: {item.DueDate.ToShortDateString()}</h2>" : $"<h2 style=color:white;>{item.Description}: {item.DueDate.ToShortDateString()}</h2>");
                }
                else if (item.DueDate <= twoWeeksFromNow && item.Sensative == 1)
                {
                    emailBody.Append(item.DueDate <= DateTime.Now ? $"<h2 style=color:red;>-Redacted-: {item.DueDate.ToShortDateString()}</h2>" : $"<h2 style=color:white;>-Redacted-: {item.DueDate.ToShortDateString()}</h2>");
                }
            }

            // Mail message configuration
            var mailMessage = new MailMessage
            {
                From = new MailAddress("DoNotReply@email.com"),
                Subject = "To-Do List " + DateTime.Now.ToString("M/d/yyyy"),
                Body = emailBody.ToString(),
                IsBodyHtml = true,
            };
            mailMessage.To.Add("scottjanda@gmail.com");

            // Sending email
            smtpClient.Send(mailMessage);

            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
        }
    }
}
