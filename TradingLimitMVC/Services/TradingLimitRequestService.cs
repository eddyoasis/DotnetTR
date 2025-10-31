using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TradingLimitMVC.Data;
using TradingLimitMVC.Models;
using TradingLimitMVC.Models.AppSettings;

namespace TradingLimitMVC.Services
{
    public interface ITradingLimitRequestService
    {
        Task<IEnumerable<TradingLimitRequest>> GetAllAsync();
        Task<TradingLimitRequest?> GetByIdAsync(int id);
        Task<TradingLimitRequest> CreateAsync(TradingLimitRequest tradingLimitRequest);
        Task<TradingLimitRequest> UpdateAsync(TradingLimitRequest tradingLimitRequest);
        Task<bool> DeleteAsync(int id);
        Task<string> GenerateRequestIdAsync();
        Task<bool> SubmitAsync(int id, string submittedBy, string submittedByEmail, string approvalEmail);
        Task<bool> SubmitWithMultiApprovalAsync(int id, string submittedBy, string submittedByEmail, List<ApprovalStepRequest> approvers, string workflowType = "Sequential");
        Task<IEnumerable<TradingLimitRequest>> GetByStatusAsync(string status);
        Task<IEnumerable<TradingLimitRequest>> GetByUserAsync(string userName);
        Task<IEnumerable<TradingLimitRequest>> GetPendingApprovalsForUserAsync(string userEmail);
        Task<bool> AddAttachmentAsync(int requestId, TradingLimitRequestAttachment attachment);
        Task<bool> RemoveAttachmentAsync(int attachmentId);
        Task<TradingLimitRequestAttachment?> GetAttachmentAsync(int attachmentId);
    }

    public class TradingLimitRequestService : ITradingLimitRequestService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TradingLimitRequestService> _logger;
        private readonly IEmailService _emailService;
        private readonly IOptionsSnapshot<GeneralAppSetting> _generalAppSetting;
        private readonly IApprovalWorkflowService _approvalWorkflowService;

        public TradingLimitRequestService(
            IEmailService emailService,
            IOptionsSnapshot<GeneralAppSetting> generalAppSetting,
            ApplicationDbContext context,
            ILogger<TradingLimitRequestService> logger,
            IApprovalWorkflowService approvalWorkflowService)
        {
            _context = context;
            _logger = logger;
            _emailService = emailService;
            _generalAppSetting = generalAppSetting;
            _approvalWorkflowService = approvalWorkflowService;
        }

        public async Task<IEnumerable<TradingLimitRequest>> GetAllAsync()
        {
            try
            {
                return await _context.TradingLimitRequests
                    .Include(t => t.Attachments)
                    .OrderByDescending(t => t.CreatedDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all trading limit requests");
                throw;
            }
        }

        public async Task<TradingLimitRequest?> GetByIdAsync(int id)
        {
            try
            {
                return await _context.TradingLimitRequests
                    .Include(t => t.Attachments)
                    .FirstOrDefaultAsync(t => t.Id == id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving trading limit request with ID {Id}", id);
                throw;
            }
        }

        public async Task<TradingLimitRequest> CreateAsync(TradingLimitRequest tradingLimitRequest)
        {
            try
            {
                // Generate Request ID if not provided
                if (string.IsNullOrEmpty(tradingLimitRequest.RequestId))
                {
                    tradingLimitRequest.RequestId = await GenerateRequestIdAsync();
                }

                tradingLimitRequest.CreatedDate = DateTime.Now;
                tradingLimitRequest.Status = "Draft";

                _context.TradingLimitRequests.Add(tradingLimitRequest);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Trading limit request created with ID {Id}", tradingLimitRequest.Id);
                return tradingLimitRequest;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating trading limit request");
                throw;
            }
        }

        public async Task<TradingLimitRequest> UpdateAsync(TradingLimitRequest tradingLimitRequest)
        {
            try
            {
                tradingLimitRequest.ModifiedDate = DateTime.Now;
                
                _context.TradingLimitRequests.Update(tradingLimitRequest);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Trading limit request updated with ID {Id}", tradingLimitRequest.Id);
                return tradingLimitRequest;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating trading limit request with ID {Id}", tradingLimitRequest.Id);
                throw;
            }
        }

        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                var request = await _context.TradingLimitRequests.FindAsync(id);
                if (request == null)
                {
                    _logger.LogWarning("Trading limit request with ID {Id} not found for deletion", id);
                    return false;
                }

                _context.TradingLimitRequests.Remove(request);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Trading limit request with ID {Id} deleted successfully", id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting trading limit request with ID {Id}", id);
                throw;
            }
        }

        public async Task<string> GenerateRequestIdAsync()
        {
            try
            {
                var today = DateTime.Today;
                var prefix = $"TLR{today:yyyyMM}";
                
                var lastRequest = await _context.TradingLimitRequests
                    .Where(t => t.RequestId != null && t.RequestId.StartsWith(prefix))
                    .OrderByDescending(t => t.RequestId)
                    .FirstOrDefaultAsync();

                int sequenceNumber = 1;
                if (lastRequest != null && !string.IsNullOrEmpty(lastRequest.RequestId))
                {
                    var lastSequence = lastRequest.RequestId.Substring(prefix.Length);
                    if (int.TryParse(lastSequence, out int lastNumber))
                    {
                        sequenceNumber = lastNumber + 1;
                    }
                }

                return $"{prefix}{sequenceNumber:D4}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating request ID");
                throw;
            }
        }

        public async Task<bool> SubmitAsync(int id, string submittedBy, string submittedByEmail, string approvalEmail)
        {
            try
            {
                var request = await _context.TradingLimitRequests.FindAsync(id);
                if (request == null)
                {
                    _logger.LogWarning("Trading limit request with ID {Id} not found for submission", id);
                    return false;
                }

                request.Status = "Submitted";
                request.SubmittedDate = DateTime.Now;
                request.SubmittedBy = submittedBy;
                request.SubmittedByEmail = submittedByEmail;
                request.ApprovalEmail = approvalEmail;
                request.ModifiedDate = DateTime.Now;
                request.ModifiedBy = submittedBy;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Trading limit request with ID {Id} submitted by {SubmittedBy}", id, submittedBy);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting trading limit request with ID {Id}", id);
                throw;
            }
        }

        public async Task<bool> SubmitWithMultiApprovalAsync(int id, string submittedBy, string submittedByEmail, List<ApprovalStepRequest> approvers, string workflowType = "Sequential")
        {
            try
            {
                var request = await _context.TradingLimitRequests.FindAsync(id);
                if (request == null)
                {
                    _logger.LogWarning("Trading limit request with ID {Id} not found for submission", id);
                    return false;
                }

                // Update request status
                request.Status = "Submitted";
                request.SubmittedDate = DateTime.UtcNow.AddHours(8);
                request.SubmittedBy = submittedBy;
                request.SubmittedByEmail = submittedByEmail;
                request.ModifiedDate = DateTime.UtcNow.AddHours(8);
                request.ModifiedBy = submittedBy;

                // Create multi-approval workflow
                var workflow = await _approvalWorkflowService.CreateWorkflowAsync(id, approvers, workflowType);

                await _context.SaveChangesAsync();

                //Send Email
                var approvalStep = workflow.ApprovalSteps.FirstOrDefault();
                if (approvalStep != null && !string.IsNullOrEmpty(approvalStep.ApproverEmail))
                {
                    var approverEmail = approvalStep.ApproverEmail;
                    var observerEmails = await _context.GroupSettings.Where(x => x.GroupID == approvalStep.ApprovalGroupId && x.TypeID == 3).Select(x=>x.Email).ToListAsync();
                    observerEmails.Add(submittedByEmail);

                    await _emailService.SendApprovalEmail(request, approverEmail, observerEmails);
                }

                _logger.LogInformation("Trading limit request with ID {Id} submitted with multi-approval workflow by {SubmittedBy}", id, submittedBy);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting trading limit request with multi-approval workflow for ID {Id}", id);
                throw;
            }
        }

        public async Task<IEnumerable<TradingLimitRequest>> GetByStatusAsync(string status)
        {
            try
            {
                return await _context.TradingLimitRequests
                    .Include(t => t.Attachments)
                    .Where(t => t.Status == status)
                    .OrderByDescending(t => t.CreatedDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving trading limit requests with status {Status}", status);
                throw;
            }
        }

        public async Task<IEnumerable<TradingLimitRequest>> GetByUserAsync(string userName)
        {
            try
            {
                return await _context.TradingLimitRequests
                    .Include(t => t.Attachments)
                    .Where(t => t.CreatedBy == userName)
                    .OrderByDescending(t => t.CreatedDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving trading limit requests for user {UserName}", userName);
                throw;
            }
        }

        public async Task<bool> AddAttachmentAsync(int requestId, TradingLimitRequestAttachment attachment)
        {
            try
            {
                var request = await _context.TradingLimitRequests.FindAsync(requestId);
                if (request == null)
                {
                    _logger.LogWarning("Trading limit request with ID {RequestId} not found for attachment", requestId);
                    return false;
                }

                attachment.TradingLimitRequestId = requestId;
                attachment.UploadDate = DateTime.Now;

                _context.TradingLimitRequestAttachments.Add(attachment);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Attachment added to trading limit request {RequestId}", requestId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding attachment to trading limit request {RequestId}", requestId);
                throw;
            }
        }

        public async Task<bool> RemoveAttachmentAsync(int attachmentId)
        {
            try
            {
                var attachment = await _context.TradingLimitRequestAttachments.FindAsync(attachmentId);
                if (attachment == null)
                {
                    _logger.LogWarning("Attachment with ID {AttachmentId} not found for removal", attachmentId);
                    return false;
                }

                _context.TradingLimitRequestAttachments.Remove(attachment);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Attachment with ID {AttachmentId} removed successfully", attachmentId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing attachment with ID {AttachmentId}", attachmentId);
                throw;
            }
        }

        public async Task<TradingLimitRequestAttachment?> GetAttachmentAsync(int attachmentId)
        {
            try
            {
                return await _context.TradingLimitRequestAttachments
                    .FirstOrDefaultAsync(a => a.Id == attachmentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving attachment with ID {AttachmentId}", attachmentId);
                throw;
            }
        }

        public async Task<IEnumerable<TradingLimitRequest>> GetPendingApprovalsForUserAsync(string userEmail)
        {
            try
            {
                // Use the approval workflow service which handles both multi-approval and legacy single approval
                return await _approvalWorkflowService.GetPendingApprovalsForUserAsync(userEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending approvals for user {UserEmail}", userEmail);
                throw;
            }
        }
    }
}