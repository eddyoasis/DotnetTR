using Microsoft.Extensions.Options;
using System.DirectoryServices.Protocols;
using TradingLimitMVC.Models.AppSettings;
using ClassLibrary1Models = ClassLibrary1.Models;
using ClassLibrary1Services = ClassLibrary1.Services;

namespace TradingLimitMVC.Services
{
    public interface ILoginService
    {
        Task<ClassLibrary1Models.JwtTokenModel> AuthenticateViaNuget(string username, string password);
        Task<ClassLibrary1Models.JwtTokenModel> AuthenticateViaNugetByPass(string username, string password);
    }

    public class LoginService : ILoginService
    {
        private readonly ClassLibrary1Services.IAuthService _authNugetService;
        private readonly ClassLibrary1Models.JwtAppSetting _jwtAppSetting;
        private readonly ClassLibrary1Models.LDAPAppSetting _ldapAppSetting;

        public LoginService(
            IOptionsSnapshot<LDAPAppSetting> ldapAppSetting,
            IOptionsSnapshot<JwtAppSetting> jwtAppSetting)
        {
            _authNugetService = new ClassLibrary1Services.AuthService();

            var ldapAppSettingValue = ldapAppSetting.Value;
            _ldapAppSetting = new ClassLibrary1Models.LDAPAppSetting
            {
                Domain = ldapAppSettingValue.Domain,
                BaseDn = ldapAppSettingValue.BaseDn,
                Server = ldapAppSettingValue.Server,
                Port = ldapAppSettingValue.Port,
                IsBypass = ldapAppSettingValue.IsBypass,
            };

            var jwtAppSettingValue = jwtAppSetting.Value;
            _jwtAppSetting = new ClassLibrary1Models.JwtAppSetting
            {
                Key = jwtAppSettingValue.Key,
                Issuer = jwtAppSettingValue.Issuer,
                Audience = jwtAppSettingValue.Audience
            };
        }

        public async Task<ClassLibrary1Models.JwtTokenModel> AuthenticateViaNuget(string username, string password)
        {
            ClassLibrary1Models.JwtTokenModel jwtTokenModel = new ClassLibrary1Models.JwtTokenModel();

            try
            {
                jwtTokenModel = await _authNugetService.Login(_ldapAppSetting, _jwtAppSetting, username, password);
                return jwtTokenModel;
            }
            catch (LdapException ex)
            {
                return jwtTokenModel;
            }
        }

        public async Task<ClassLibrary1Models.JwtTokenModel> AuthenticateViaNugetByPass(string username, string password)
        {
            ClassLibrary1Models.JwtTokenModel jwtTokenModel = new ClassLibrary1Models.JwtTokenModel();

            try
            {
                jwtTokenModel = await _authNugetService.LoginByPass(_jwtAppSetting, username, password);
                return jwtTokenModel;
            }
            catch (LdapException ex)
            {
                return jwtTokenModel;
            }
        }
    }
}
