# Demo-site email

Source for the email sent when someone creates a demo site (`HomeController.IndexPost`).

- **`demo-site-created.mjml`** — the email, written in [MJML](https://documentation.mjml.io/). Edit this.
- **`build-email.mjs`** — compiles the MJML and writes the Razor view the app actually renders,
  `../../Views/DemoSiteCreatedEmail.cshtml`.

This folder is under the module's `Assets/`, which OrchardCore excludes from the build, so nothing here
is embedded in the shipped assembly — only the generated `.cshtml` is.

## Changing the email

### Prerequisites

- **Node.js** (v18+) — check with `node --version`.
- **Internet access** the first time, so `npx` can download MJML. The MJML version is pinned in the
  script, so the output is reproducible. Nothing is installed into this repo (no `node_modules`).

### Steps

1. Edit `demo-site-created.mjml`.
2. Run the build from this folder:
   ```bash
   cd OrchardCore.Try/Assets/Emails
   node build-email.mjs
   ```
   It compiles the MJML and rewrites `../../Views/DemoSiteCreatedEmail.cshtml`, printing the path it
   wrote. (The script resolves paths relative to itself, so `node OrchardCore.Try/Assets/Emails/build-email.mjs`
   from the repo root works too.)
3. Commit **both** files together — the `.cshtml` is generated, so never hand-edit it.

## How it works

`build-email.mjs` runs MJML → HTML, then converts it to a Razor template:

- Every literal `@` is escaped to `@@` so Razor treats the CSS `@media`/`@import` rules and the contact
  address as text (they render back to a single `@`).
- The value slots are mapped to the model:

  | MJML | Razor | Value |
  |---|---|---|
  | `{0}` | `@Model.SiteName` | demo site name |
  | `{1}` | `@Model.SetupUrl` | one-time activation link |
  | `{2}` | `@Model.SiteUrl` | site URL (admin link is `@Model.SiteUrl/admin`) |
  | `{3}` | `@Model.UserName` | admin username |
  | `{4}` | `@Model.Password` | admin password |

The model is `OrchardCore.Try.ViewModels.DemoSiteCreatedEmailViewModel`; the controller renders the
`DemoSiteCreatedEmail` shape with `IDisplayHelper`.
