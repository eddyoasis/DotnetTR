using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TradingLimitMVC.Data;
using TradingLimitMVC.Middlewares;
using TradingLimitMVC.Models.AppSettings;
using TradingLimitMVC.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

/*------------- AppSettings */
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

builder.Services.Configure<LDAPAppSetting>(builder.Configuration.GetSection("LDAPAppSettings"));
builder.Services.Configure<JwtAppSetting>(builder.Configuration.GetSection("JwtAppSettings"));
builder.Services.Configure<SmtpAppSetting>(builder.Configuration.GetSection("SmtpAppSettings"));
builder.Services.Configure<GeneralAppSetting>(builder.Configuration.GetSection("GeneralAppSettings"));
builder.Services.Configure<ApprovalThresholdsAppSetting>(builder.Configuration.GetSection("AppSettings:ApprovalThresholds"));
builder.Services.Configure<DepartmentRolesAppSetting>(builder.Configuration.GetSection("AppSettings"));
builder.Services.Configure<PowerWorkflowAppSetting>(builder.Configuration.GetSection("PowerWorkflowAppSettings"));

/*------------- DI */
builder.Services.AddScoped<ILoginService, LoginService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();

/*------------- JWT */
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
.AddCookie(options =>
{
    options.LoginPath = "/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["JwtAppSettings:Issuer"],
        ValidAudience = builder.Configuration["JwtAppSettings:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JwtAppSettings:Key"] ?? "default-key"))
    };
});

// Add Entity Framework with SQL Server
builder.Services.AddDbContext<ApplicationDbContext>(options =>
options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register custom services
builder.Services.AddScoped<IExchangeRateService, ExchangeRateService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IConfigurationHelperService, ConfigurationHelperService>();
builder.Services.AddScoped<ITradingLimitRequestService, TradingLimitRequestService>();
builder.Services.AddScoped<IGeneralService, GeneralService>();


// Add logging
builder.Services.AddLogging();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseMiddleware<CookieAuthMiddleware>();
app.UseAuthorization();

// Configure routing
app.MapControllerRoute(
name: "approval",
pattern: "Approval/{action=Index}/{id?}",
defaults: new { controller = "PurchaseRequisitionApproval" });

app.MapControllerRoute(
name: "default",
pattern: "{controller=Home}/{action=Index}/{id?}");

// Initialize and seed the database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();


    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();

        // Ensure database is created
        context.Database.EnsureCreated();

        // Note: Database seeding removed after Purchase feature cleanup

        logger.LogInformation("Database initialized successfully at {Time}", DateTime.Now);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while initializing the database at {Time}", DateTime.Now);
    }


}

app.Run();
