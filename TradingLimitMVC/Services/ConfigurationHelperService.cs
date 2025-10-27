namespace TradingLimitMVC.Services
{
    public interface IConfigurationHelperService
    {
        string GetApproverEmail(string role);
        string GetApproverName(string role);
        (string Approver, string Email, string Role,string Name) GetCostCenterApproverInfo(string costCenterName);

    }

    public class ConfigurationHelperService : IConfigurationHelperService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ConfigurationHelperService> _logger;

        public ConfigurationHelperService(IConfiguration configuration, ILogger<ConfigurationHelperService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public string GetApproverEmail(string role)
        {
            var email = _configuration[$"AppSettings:ApproverEmails:{role}"];

            if (string.IsNullOrEmpty(email))
            {
                _logger.LogWarning($" Email not found for {role}, using Default");
                email = _configuration["AppSettings:ApproverEmails:Default"];
            }

            if (string.IsNullOrEmpty(email))
            {
                _logger.LogError($" Default email not configured");
                throw new InvalidOperationException("Default approver email not configured in appsettings.json");
            }

            return email;
        }

        public string GetApproverName(string role)
        {
            var name = _configuration[$"AppSettings:ApproverNames:{role}"];

            if (string.IsNullOrEmpty(name))
            {
                _logger.LogWarning($" Name not found for {role}, using role as name");
                return role;
            }

            return name;
        }
       
        public (string Approver, string Email, string Role, string Name) GetCostCenterApproverInfo(string costCenterName)
        {
            //var approver = _configuration[$"AppSettings:CostCenterApprovers:{costCenterName}:Approver"];
            var approver = "Approver";
            var email = _configuration[$"AppSettings:DepartmentRoles:{costCenterName}:Email"];
            var role = _configuration[$"AppSettings:DepartmentRoles:{costCenterName}:HOD"];
            var name = _configuration[$"AppSettings:DepartmentRoles:{costCenterName}:Name"];

            if (string.IsNullOrEmpty(approver) || string.IsNullOrEmpty(email)) 
            {
                _logger.LogWarning($" Approver info not found for {costCenterName}, using defaults");
                //return ("HOD", GetApproverEmail("Default"), "HOD");
                return ("HOD", GetApproverEmail("Default"), "HOD", "Default Name");
            }
            if (string.IsNullOrEmpty(name))
            {
                name = GetApproverName(role);
            }

            return (approver, email, role,name);
        }
    }
}
