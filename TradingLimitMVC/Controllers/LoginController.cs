using ClassLibrary1.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TradingLimitMVC.Models.AppSettings;
using TradingLimitMVC.Models.ViewModels;
using TradingLimitMVC.Services;
using ClassLibrary1Models = ClassLibrary1.Models;

namespace TradingLimitMVC.Controllers
{
    public class LoginController(
        IOptionsSnapshot<LDAPAppSetting> _ldapAppSetting,
        ILoginService _loginService,
        ILogger<LoginController> logger) : Controller
    {
        // GET: Login
        [HttpGet]
        public ActionResult Index()
        {
            logger.LogInformation("Login page visited at {Time}", DateTime.UtcNow);

            return View(new LoginViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> Login([FromBody] LoginViewModel loginReq)
        {
            ClassLibrary1Models.JwtTokenModel jwtTokenModel = new ClassLibrary1Models.JwtTokenModel();

            jwtTokenModel = _ldapAppSetting.Value.IsBypass ?
               await _loginService.AuthenticateViaNugetByPass(loginReq.Username, loginReq.Password) :
               await _loginService.AuthenticateViaNuget(loginReq.Username, loginReq.Password);

            if (!string.IsNullOrEmpty(jwtTokenModel.AccessToken))
            {
                Response.Cookies.Append("AuthToken", jwtTokenModel.AccessToken, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = false, // Set to true in production (HTTPS required)
                    //Secure = true, // Set to true in production (HTTPS required)
                    //Expires = DateTime.UtcNow.AddHours(1) // Cookie expires in 1 hour
                    Expires = DateTime.UtcNow.AddHours(1) // Cookie expires in 1 hour
                });

                Response.Cookies.Append("refresh_token", jwtTokenModel.RefreshToken, new CookieOptions
                {
                    HttpOnly = true,
                    //Secure = true,
                    Secure = false,
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTimeOffset.UtcNow.AddDays(7)
                });

                //for return url from email
                var webReturnUrlFull = "";
                var hasWebReturnUrl = Request.Cookies.TryGetValue("WebReturnUrl", out var webReturnUrl);
                if (hasWebReturnUrl)
                {
                    var jwtInfo = JwtTokenHelper.DecodeJwtToken(jwtTokenModel.AccessToken);
                    Response.Cookies.Delete("WebReturnUrl");
                    webReturnUrlFull = $"{webReturnUrl}?approverRole={jwtInfo.JobTitle}";
                }

                return Json(new
                {
                    isSuccess = true,
                    webReturnUrl = hasWebReturnUrl ? webReturnUrlFull : ""
                });
            }

            return Json(new { isSuccess = false });
        }

        public async Task<IActionResult> Logout()
        {
            Response.Cookies.Delete("AuthToken");
            return RedirectToAction("Index", "Login");
        }
    }
}
