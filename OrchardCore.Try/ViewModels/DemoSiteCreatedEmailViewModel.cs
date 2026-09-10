using OrchardCore.DisplayManagement.Views;

namespace OrchardCore.Try.ViewModels;

/// <summary>
/// Model for the demo-site-created email. Deriving from <see cref="ShapeViewModel"/> makes it a shape of
/// type "DemoSiteCreatedEmail", so it renders through the Views/DemoSiteCreatedEmail.cshtml template.
/// HomeController builds one of these and renders it to the email's HTML body via IDisplayHelper.
/// </summary>
public class DemoSiteCreatedEmailViewModel : ShapeViewModel
{
    public DemoSiteCreatedEmailViewModel()
        : base("DemoSiteCreatedEmail")
    {
    }

    /// <summary>The name the visitor gave the demo site.</summary>
    public string SiteName { get; set; }

    /// <summary>The one-time activation link that provisions the site.</summary>
    public string SetupUrl { get; set; }

    /// <summary>The public URL of the site; the admin link is built as <c>{SiteUrl}/admin</c>.</summary>
    public string SiteUrl { get; set; }

    /// <summary>The admin username.</summary>
    public string UserName { get; set; }

    /// <summary>The generated admin password.</summary>
    public string Password { get; set; }
}
