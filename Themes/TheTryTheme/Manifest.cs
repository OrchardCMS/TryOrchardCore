using OrchardCore.DisplayManagement.Manifest;

// DefaultTenantOnly keeps this theme off the demo tenants entirely: ShellFeaturesManager runs the
// DefaultTenantOnlyFeatureValidationProvider over every feature, which rejects default-tenant-only
// features on any other shell — so the theme never appears in a demo site's Design > Themes picker (or
// its Features list), and CompositionStrategy will not load it into their shells. This is the same gate
// that already keeps the OrchardCore.Try module itself invisible to demo tenants.
[assembly: Theme(
    Name = "TheTryTheme",
    Author = "The Orchard Team",
    Website = "https://orchardcore.net",
    Version = "1.0.0",
    Description = "The theme for the Try Orchard Core signup site.",
    DefaultTenantOnly = true
)]
