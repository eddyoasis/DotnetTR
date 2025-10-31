using FluentEmail.Core;
using Microsoft.Extensions.Options;
using TradingLimitMVC.Models.AppSettings;
using System.Net.Mail;
using TradingLimitMVC.Models;

namespace TradingLimitMVC.Services
{
    public interface IEmailService
    {
        Task SendApprovalEmail(TradingLimitRequest req, string approverEmail, List<string> EmailCCs);
        Task SendApprovalCompletedEmail(TradingLimitRequest req, string submittedByEmail, bool isApproved);
    }

    public class EmailService(
        IOptionsSnapshot<SmtpAppSetting> _smtpAppSetting,
        IOptionsSnapshot<GeneralAppSetting> _generalAppSetting) : IEmailService
    {
        public async Task SendApprovalEmail(TradingLimitRequest req, string submittedByEmail, List<string> EmailCCs)
        {
            var generalAppSetting = _generalAppSetting.Value;
            var domainHost = generalAppSetting.Host;

            var recipientsTo = new List<string> { submittedByEmail };
            var recipientsCC = EmailCCs;
            var subject = $"[PENDING SG IT] Trading limit request: {req.RequestId}";
            var body = $@"
                <p>Please refer to the trading limit request below for your approval.<br/>
                Awaiting your action.</p>
                <p><a href='{domainHost}/Login?ReturnUrl={domainHost}/Approval/Details/{req.Id}'>Click here to approve</a></p>
                Awaiting your action.</p>
                <p>
                    <strong>Requested ID:</strong> {req.RequestId}<br/>
                    <strong>Limit Start Date:</strong> {req.RequestDate.ToString("dd/MM/yyyy HH:mm:ss")}<br/>
                    <strong>Limit End Date:</strong> {req.LimitEndDate.ToString("dd/MM/yyyy HH:mm:ss")}<br/>
                    <strong>TRCode:</strong> {req.TRCode}<br/>
                    <strong>ClientCode:</strong> {req.ClientCode}<br/>
                    <strong>RequestType:</strong> {req.RequestType}<br/>
                    <strong>ReasonType:</strong> {req.ReasonType}<br/>
                    <strong>BriefDescription:</strong> {req.BriefDescription}<br/>
                    <strong>GL Proposed Limit:</strong> {req.GLProposedLimit}<br/><br/>

                    <strong>Submitted By:</strong> {req.SubmittedBy}<br/>
                    <strong>GLSubmitted Date:</strong> {req.SubmittedDate?.ToString("dd/MM/yyyy HH:mm:ss") ?? "N/A"}<br/>
                </p>";

            await SendEmailAsync(recipientsTo, recipientsCC, subject, body);
        }

        public async Task SendApprovalCompletedEmail(TradingLimitRequest req, string submmitedByEmail, bool isApproved)
        {
            var generalAppSetting = _generalAppSetting.Value;
            var domainHost = generalAppSetting.Host;

            var recipientsTo = new List<string> { submmitedByEmail };
            var recipientsCC = new List<string> { };
            var approvalStatus = isApproved ? "Approved" : "Rejected";
            var subject = $"[{approvalStatus}] Trading limit request: {req.RequestId}";
            var body = $@"
                <p>Please refer to the trading limit request below.</p>
                <p><a href='{domainHost}/Login?ReturnUrl={domainHost}/TradingLimitRequest/Details/{req.Id}'>Click here to see detail</a></p></p>
                <p>
                    <strong>Requested ID:</strong> {req.RequestId}<br/>
                    <strong>Limit Start Date:</strong> {req.RequestDate.ToString("dd/MM/yyyy HH:mm:ss")}<br/>
                    <strong>Limit End Date:</strong> {req.LimitEndDate.ToString("dd/MM/yyyy HH:mm:ss")}<br/>
                    <strong>TRCode:</strong> {req.TRCode}<br/>
                    <strong>ClientCode:</strong> {req.ClientCode}<br/>
                    <strong>RequestType:</strong> {req.RequestType}<br/>
                    <strong>ReasonType:</strong> {req.ReasonType}<br/>
                    <strong>BriefDescription:</strong> {req.BriefDescription}<br/>
                    <strong>GL Proposed Limit:</strong> {req.GLProposedLimit}<br/><br/>

                    <strong>Submitted By:</strong> {req.SubmittedBy}<br/>
                    <strong>GLSubmitted Date:</strong> {req.SubmittedDate?.ToString("dd/MM/yyyy HH:mm:ss") ?? "N/A"}<br/>
                </p>";

            await SendEmailAsync(recipientsTo, recipientsCC, subject, body);
        }

        private Task SendEmailAsync(List<string> recipientsTo, List<string> recipientsCC, string subject, string body)
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
