namespace TradingLimitMVC.Models.AppSettings
{
    public class DepartmentRolesAppSetting
    {
        public Dictionary<string, CostCenterRole> DepartmentRoles { get; set; } = new Dictionary<string, CostCenterRole>();
    }

    public class CostCenterRole
    {
        public string HOD { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}
