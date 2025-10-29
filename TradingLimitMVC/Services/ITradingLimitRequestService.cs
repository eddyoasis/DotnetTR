using TradingLimitMVC.Models;

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
        Task<bool> SubmitAsync(int id, string submittedBy);
        Task<IEnumerable<TradingLimitRequest>> GetByStatusAsync(string status);
        Task<IEnumerable<TradingLimitRequest>> GetByUserAsync(string userName);
        Task<bool> AddAttachmentAsync(int requestId, TradingLimitRequestAttachment attachment);
        Task<bool> RemoveAttachmentAsync(int attachmentId);
        Task<TradingLimitRequestAttachment?> GetAttachmentAsync(int attachmentId);
    }
}