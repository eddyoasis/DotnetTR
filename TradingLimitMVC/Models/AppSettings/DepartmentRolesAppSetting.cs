namespace TradingLimitMVC.Models.AppSettings
{
    public class DepartmentRolesAppSetting
    {
        public Dictionary<string, CostCenterRole> DepartmentRoles { get; set; }
    }

    public class CostCenterRole
    {
        public string HOD { get; set; }
        public string Email { get; set; }
    }
}
