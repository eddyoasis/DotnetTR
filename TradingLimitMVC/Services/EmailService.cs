using FluentEmail.Core;
using Microsoft.Extensions.Options;
using TradingLimitMVC.Models.AppSettings;
using System.Net.Mail;

namespace TradingLimitMVC.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(List<string> recipientsTo, List<string> recipientsCC, string subject, string body);
    }

    public class EmailService(
         IOptionsSnapshot<SmtpAppSetting> _smtpAppSetting
         ) : IEmailService
    {
        public Task SendEmailAsync(List<string> recipientsTo, List<string> recipientsCC, string subject, string body)
        {
            var smtpAppSetting = _smtpAppSetting.Value;

            // --- Email Configuration ---
            string smtpHost = smtpAppSetting.Host;
            string senderEmail = smtpAppSetting.EmailFrom;
            string emailSubject = subject;
            string strMailBody = body;

            if (smtpAppSetting.IsTestEmailTo)
            {
                recipientsTo = smtpAppSetting.EmailTo;
            }

            try
            {
                using MailMessage mailMessage = new();

                mailMessage.From = new MailAddress(senderEmail);
                mailMessage.To.AddRange(recipientsTo.Select(e => new MailAddress(e)));

                if (recipientsCC.Any())
                {
                    mailMessage.CC.AddRange(recipientsCC.Select(e => new MailAddress(e)));
                }

                // Set subject (optional)
                mailMessage.Subject = emailSubject;

                // Configure email body
                //mailMessage.IsBodyHtml = true; // Set to true if your body content is HTML
                mailMessage.IsBodyHtml = true;
                mailMessage.Priority = MailPriority.High; // Set email priority
                //mailMessage.Body = strMailBody; // Assign the body content

                string formattedMessage = strMailBody.Replace("    ", Environment.NewLine + Environment.NewLine);
                mailMessage.Body = formattedMessage;

                using SmtpClient smtpClient = new(smtpHost);

                smtpClient.Send(mailMessage);
            }
            catch (SmtpException)
            {
                // Log exception if needed
            }
            catch (Exception)
            {
                // Log exception if needed
            }
            
            return Task.CompletedTask;
        }
    }
}
