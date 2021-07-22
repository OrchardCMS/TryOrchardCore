using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Abstractions.Setup;
using OrchardCore.Email;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Models;
using OrchardCore.Modules;
using OrchardCore.Setup.Services;
using OrchardCore.Try.ViewModels;
using OrchardCore.Users.Services;

namespace OrchardCore.Try.Controllers
{
    public class HomeController : Controller
    {
        private const string defaultAdminName = "admin";
        private const string dataProtectionPurpose = "Password";
        private const string emailSubject = "Try Orchard Core";
        private const bool emailToBcc = true;

        private readonly IUserService _userService;
        private readonly IShellSettingsManager _shellSettingsManager;
        private readonly IShellHost _shellHost;
        private readonly ISmtpService _smtpService;
        private readonly ISetupService _setupService;
        private readonly IOptions<SmtpSettings> _smtpSettingsOptions;
        private readonly IClock _clock;
        private readonly IDataProtectionProvider _dataProtectionProvider;
        private readonly ILogger<HomeController> _logger;

        public HomeController(
            IUserService userService,
            IShellSettingsManager shellSettingsManager,
            IShellHost shellHost,
            ISmtpService smtpService,
            ISetupService setupService,
            IOptions<SmtpSettings> smtpSettingsOptions,
            IClock clock,
            IDataProtectionProvider dataProtectionProvider,
            ILogger<HomeController> logger,
            IStringLocalizer<HomeController> stringLocalizer)
        {
            _userService = userService;
            _shellSettingsManager = shellSettingsManager;
            _shellHost = shellHost;
            _smtpService = smtpService;
            _setupService = setupService;
            _smtpSettingsOptions = smtpSettingsOptions;
            _clock = clock;
            _dataProtectionProvider = dataProtectionProvider;
            _logger = logger;

            T = stringLocalizer;
        }

        public IStringLocalizer T { get; set; }

        public IActionResult Index(RegisterUserViewModel model)
        {
            // Generate random site prefix
            model.Handle = GenerateRandomName();

            return View(model);
        }

        [HttpPost]
        [ActionName(nameof(Index))]
        public async Task<IActionResult> IndexPost(RegisterUserViewModel model)
        {
            if (!model.AcceptTerms)
            {
                ModelState.AddModelError(nameof(RegisterUserViewModel.AcceptTerms), T["Please, accept the terms and conditions."]);
            }

            if (!string.IsNullOrEmpty(model.Handle) && !Regex.IsMatch(model.Handle, @"^\w+$"))
            {
                ModelState.AddModelError(nameof(RegisterUserViewModel.Handle), T["Invalid tenant name. Must contain characters only and no spaces."]);
            }

            if (ModelState.IsValid)
            {
                if (_shellHost.TryGetSettings(model.Handle, out var shellSettings))
                {
                    ModelState.AddModelError(nameof(RegisterUserViewModel.Handle), T["This site name already exists."]);
                }
                else
                {
                    var adminName = defaultAdminName;
                    var adminPassword = GenerateRandomPassword();
                    var siteName = model.SiteName;

                    shellSettings = new ShellSettings
                    {
                        Name = model.Handle,
                        RequestUrlPrefix = model.Handle.ToLower(),
                        RequestUrlHost = null,
                        State = TenantState.Uninitialized
                    };
                    shellSettings["Description"] = $"{model.SiteName} {model.Email}";
                    shellSettings["RecipeName"] = model.RecipeName;
                    shellSettings["DatabaseProvider"] = "Sqlite";

                    await _shellSettingsManager.SaveSettingsAsync(shellSettings);
                    var shellContext = await _shellHost.GetOrCreateShellContextAsync(shellSettings);

                    var recipes = await _setupService.GetSetupRecipesAsync();
                    var recipe = recipes.FirstOrDefault(x => x.Name == model.RecipeName);

                    if (recipe == null)
                    {
                        ModelState.AddModelError(nameof(RegisterUserViewModel.RecipeName), T["Invalid recipe name."]);
                    }

                    var siteUrl = GetTenantUrl(shellSettings);

                    var dataProtector = _dataProtectionProvider.CreateProtector(dataProtectionPurpose).ToTimeLimitedDataProtector();
                    var encryptedPassword = dataProtector.Protect(adminPassword, _clock.UtcNow.Add(new TimeSpan(24, 0, 0)));
                    var confirmationLink = Url.Action(nameof(Confirm), "Home", new { email = model.Email, handle = model.Handle, siteName = model.SiteName, ep = encryptedPassword }, Request.Scheme);

                    var message = new MailMessage();
                    if (emailToBcc)
                    {
                        message.Bcc = _smtpSettingsOptions.Value.DefaultSender;
                    }
                    message.To = model.Email;
                    message.IsBodyHtml = true;
                    message.Subject = emailSubject;
                    message.Body = T["Hello,<br><br>Your demo site '{0}' has been created.<br><br>1) Setup your site by opening <a href=\"{1}\">this link</a>.<br><br>2) Log into the <a href=\"{2}/admin\">admin</a> with these credentials:<br>Username: {3}<br>Password: {4}", siteName, confirmationLink, siteUrl, adminName, adminPassword];

                    await _smtpService.SendAsync(message);

                    return RedirectToAction(nameof(Success));
                }
            }

            return View(nameof(Index), model);
        }

        public IActionResult Success()
        {
            return View();
        }

        public async Task<IActionResult> Confirm(string email, string handle, string siteName, string ep)
        {
            if (!_shellHost.TryGetSettings(handle, out var shellSettings))
            {
                return NotFound();
            }

            if (shellSettings.State == TenantState.Uninitialized)
            {
                var recipes = await _setupService.GetSetupRecipesAsync();
                var recipe = recipes.FirstOrDefault(x => x.Name == shellSettings["RecipeName"]);

                if (recipe == null)
                {
                    return NotFound();
                }

                var password = Decrypt(ep);

                var setupContext = new SetupContext
                {
                    ShellSettings = shellSettings,
                    EnabledFeatures = null,
                    Errors = new Dictionary<string, string>(),
                    Recipe = recipe
                };

                setupContext.Properties[SetupConstants.SiteName] = siteName;
                setupContext.Properties[SetupConstants.AdminUsername] = defaultAdminName;
                setupContext.Properties[SetupConstants.AdminEmail] = email;
                setupContext.Properties[SetupConstants.AdminPassword] = password;
                setupContext.Properties[SetupConstants.SiteTimeZone] = _clock.GetSystemTimeZone().TimeZoneId;
                setupContext.Properties[SetupConstants.DatabaseProvider] = shellSettings["DatabaseProvider"];

                var executionId = await _setupService.SetupAsync(setupContext);

                // Check if a component in the Setup failed
                if (setupContext.Errors.Any())
                {
                    foreach (var error in setupContext.Errors)
                    {
                        ModelState.AddModelError(error.Key, error.Value);
                    }

                    return Redirect("Error");
                }
            }

            return Redirect("~/" + handle);
        }

        private string Decrypt(string encryptedString)
        {
            try
            {
                var dataProtector = _dataProtectionProvider.CreateProtector(dataProtectionPurpose).ToTimeLimitedDataProtector();

                return dataProtector.Unprotect(encryptedString, out var expiration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decrypting the string");
            }

            return null;
        }

        public string GetTenantUrl(ShellSettings shellSettings)
        {
            var requestHostInfo = Request.Host;

            var tenantUrlHost = shellSettings.RequestUrlHost?.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries).First() ?? requestHostInfo.Host;
            if (requestHostInfo.Port.HasValue)
            {
                tenantUrlHost += ":" + requestHostInfo.Port;
            }

            var result = $"{Request.Scheme}://{tenantUrlHost}";

            if (!string.IsNullOrEmpty(shellSettings.RequestUrlPrefix))
            {
                result += "/" + shellSettings.RequestUrlPrefix;
            }

            return result;
        }

        public string GenerateRandomName()
        {
            return Path.GetRandomFileName().Replace(".", "").Substring(0, 8);
        }

        public string GenerateRandomPassword(PasswordOptions opts = null)
        {
            if (opts == null) opts = new PasswordOptions()
            {
                RequiredLength = 8,
                RequiredUniqueChars = 4,
                RequireDigit = true,
                RequireLowercase = true,
                RequireNonAlphanumeric = true,
                RequireUppercase = true
            };

            string[] randomChars = new[] {
            "ABCDEFGHJKLMNOPQRSTUVWXYZ",    // uppercase 
            "abcdefghijkmnopqrstuvwxyz",    // lowercase
            "0123456789",                   // digits
            "!@$?_-"                        // non-alphanumeric
            };

            Random rand = new Random(System.Environment.TickCount);
            List<char> chars = new List<char>();

            if (opts.RequireUppercase)
            {
                chars.Insert(rand.Next(0, chars.Count), randomChars[0][rand.Next(0, randomChars[0].Length)]);
            }

            if (opts.RequireLowercase)
            {
                chars.Insert(rand.Next(0, chars.Count), randomChars[1][rand.Next(0, randomChars[1].Length)]);
            }

            if (opts.RequireDigit)
            {
                chars.Insert(rand.Next(0, chars.Count), randomChars[2][rand.Next(0, randomChars[2].Length)]);
            }

            if (opts.RequireNonAlphanumeric)
            {
                chars.Insert(rand.Next(0, chars.Count), randomChars[3][rand.Next(0, randomChars[3].Length)]);
            }

            for (int i = chars.Count; i < opts.RequiredLength || chars.Distinct().Count() < opts.RequiredUniqueChars; i++)
            {
                string rcs = randomChars[rand.Next(0, randomChars.Length)];
                chars.Insert(rand.Next(0, chars.Count), rcs[rand.Next(0, rcs.Length)]);
            }

            return new string(chars.ToArray());
        }
    }
}
