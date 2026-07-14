using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Models;
using OrchardCore.Security.Permissions;
using OrchardCore.Try.ViewModels;
using System.Threading.Tasks;

namespace OrchardCore.Try.Controllers;

[Admin("TestTenants/{action=Index}", "TestTenants{action}")]
public sealed class AdminController : Controller
{
    // Same name as OrchardCore.Tenants' ManageTenants permission, checked by name.
    public static readonly Permission ManageTenantsPermission = new("ManageTenants");

    private const int MaxNameIndex = 100_000;

    private readonly IAuthorizationService _authorizationService;
    private readonly IShellSettingsManager _shellSettingsManager;
    private readonly IShellHost _shellHost;
    private readonly INotifier _notifier;

    public AdminController(
        IAuthorizationService authorizationService,
        IShellSettingsManager shellSettingsManager,
        IShellHost shellHost,
        INotifier notifier,
        IHtmlLocalizer<AdminController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _shellSettingsManager = shellSettingsManager;
        _shellHost = shellHost;
        _notifier = notifier;

        H = htmlLocalizer;
    }

    public IHtmlLocalizer H { get; set; }

    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, ManageTenantsPermission))
        {
            return Forbid();
        }

        return View(new CreateTestTenantsViewModel());
    }

    [HttpPost]
    [ActionName(nameof(Index))]
    public async Task<IActionResult> IndexPost(CreateTestTenantsViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ManageTenantsPermission))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return View(nameof(Index), model);
        }

        var created = 0;
        var skipped = 0;

        for (var index = 1; created < model.Count && index <= MaxNameIndex; index++)
        {
            var name = index.ToString("D8");

            if (_shellHost.TryGetSettings(name, out _))
            {
                skipped++;
                continue;
            }

            var shellSettings = new ShellSettings
            {
                Name = name,
                RequestUrlPrefix = name,
                RequestUrlHost = null,
                State = TenantState.Uninitialized
            };
            shellSettings["Description"] = $"Test tenant {name}";
            shellSettings["RecipeName"] = "Try";
            shellSettings["DatabaseProvider"] = "Sqlite";

            await _shellSettingsManager.SaveSettingsAsync(shellSettings);
            await _shellHost.GetOrCreateShellContextAsync(shellSettings);

            created++;
        }

        await _notifier.SuccessAsync(H["{0} test tenant(s) created, {1} existing name(s) skipped.", created, skipped]);

        return RedirectToAction(nameof(Index));
    }
}
