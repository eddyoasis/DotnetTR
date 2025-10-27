using ClassLibrary1.Helpers;
using Microsoft.AspNetCore.Authentication.Cookies;
using TradingLimitMVC.Services;
using System.Security.Claims;

namespace TradingLimitMVC.Middlewares
{
    public class CookieAuthMiddleware(
        RequestDelegate next)
    {
        List<string> _whiteList =
            ["/Login/Index",
            "/Test/TestPOST",
            "/Margin/Logout",
            "/Login",
            "/Login/Login"];

        public async Task Invoke(HttpContext context)
        {
            // Detect AJAX requests to handle different response types
            bool isAjax = context.Request.Headers["X-Requested-With"] == "XMLHttpRequest";

            //If has return url from email, set it to cookie for login and redirect
            var returnUrl = context.Request.Query["ReturnUrl"].ToString();
            var hasReturnUrl = !string.IsNullOrEmpty(returnUrl);
            if (hasReturnUrl)
            {
                context.Response.Cookies.Append("WebReturnUrl", returnUrl, new CookieOptions
                {
                    HttpOnly = true,
                    Expires = DateTime.UtcNow.AddMinutes(5)
                });
            }

            if (!hasReturnUrl && _whiteList.Contains(context.Request.Path.Value))
            {
                await next(context);
                return;
            }

            if (!context.User.Identity.IsAuthenticated) // Always false at this stage
            {
                if (context.Request.Cookies.TryGetValue("AuthToken", out var authToken))
                {
                    var jwtInfo = JwtTokenHelper.DecodeJwtToken(authToken);
                    Console.WriteLine("=================== JWT DEBUG ===================");
                    Console.WriteLine($"JWT Username: {jwtInfo?.Username}");
                    Console.WriteLine($"JWT Email: {jwtInfo?.Email}");
                    //Console.WriteLine($"JWT Department: {jwtInfo?.Department}");
                    Console.WriteLine($"JWT JobTitle: {jwtInfo?.JobTitle}");
                    Console.WriteLine("JWT Claims:");
                    if (jwtInfo?.Claims != null)
                    {
                        foreach (var claim in jwtInfo.Claims)
                        {
                            Console.WriteLine($"  - {claim.Type}: {claim.Value}");
                        }
                    }
                    if (!string.IsNullOrEmpty(jwtInfo?.Username))
                    {
                        var identity = new ClaimsIdentity(jwtInfo.Claims, CookieAuthenticationDefaults.AuthenticationScheme);
                        var principal = new ClaimsPrincipal(identity);
                        context.User = principal;

                        BaseService.Username = jwtInfo.Username;
                        BaseService.Email = jwtInfo.Email;
                        BaseService.Department = jwtInfo.Department;
                        BaseService.JobTitle = jwtInfo.JobTitle;
                        //  DEBUG: Confirm BaseService values
                        Console.WriteLine($" BaseService.Username set to: {BaseService.Username}");
                        Console.WriteLine($" BaseService.Email set to: {BaseService.Email}");
                    }
                    else
                    {
                        Console.WriteLine(" ERROR: JWT Username is null or empty!");
                    }


                    if (hasReturnUrl)
                    {
                        context.Response.Cookies.Delete("WebReturnUrl");
                        context.Response.Redirect($"{returnUrl}?approverRole={jwtInfo.JobTitle}");
                        return;
                    }

                    await next(context);
                    return;
                }

                /*  Refresh token */

                if (isAjax)
                {
                    context.Response.StatusCode = 401; // Set status code
                    await context.Response.WriteAsync("Unauthorized"); // Send response message
                    return;
                }
                else
                {
                    if (hasReturnUrl)
                    {
                        await next(context);
                        return;
                    }

                    context.Response.Redirect("/Login/Index");
                    return;
                }
            }

            if (isAjax)
            {
                context.Response.StatusCode = 401; // Set status code
                await context.Response.WriteAsync("Unauthorized"); // Send response message
                return;
            }
            else
            {
                context.Response.Redirect("/Login/Index");
                return;
            }
        }
    }
}
