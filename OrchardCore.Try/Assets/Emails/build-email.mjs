// Regenerates ../../Views/DemoSiteCreatedEmail.cshtml from demo-site-created.mjml.
//
// Usage:  node build-email.mjs
// Needs:  MJML, fetched on demand via npx (no node_modules committed). Internet access required the
//         first time. Version is pinned so the output is reproducible.
//
// Pipeline:  MJML -> HTML  ->  Razor transform  ->  DemoSiteCreatedEmail.cshtml
// The Razor transform escapes every literal '@' to '@@' (so the CSS @media/@import rules and the
// contact address survive Razor as text) and maps the {0}-{4} value slots to model expressions.
import { execFileSync } from 'node:child_process';
import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const MJML_VERSION = '5.4.0';

const here = dirname(fileURLToPath(import.meta.url));
const mjmlFile = join(here, 'demo-site-created.mjml');
const outFile = join(here, '..', '..', 'Views', 'DemoSiteCreatedEmail.cshtml');

// 1) Compile MJML -> HTML. `-s` writes the HTML to stdout, but it also prepends a
//    `<!-- FILE: /absolute/path -->` comment — strip it so the local path is not baked into the email.
const html = execFileSync('npx', ['--yes', `mjml@${MJML_VERSION}`, mjmlFile, '-s'], {
    encoding: 'utf8',
    stdio: ['ignore', 'pipe', 'inherit'],
}).replace(/<!--\s*FILE:[^>]*-->\s*/g, '').trim();

// 2) Razor transform. Order matters: escape all '@' first, then insert the single-'@' model
//    expressions for the placeholders.
const body = html
    .replace(/@/g, '@@')
    .replace(/\{0\}/g, '@Model.SiteName')
    .replace(/\{1\}/g, '@Model.SetupUrl')
    .replace(/\{2\}/g, '@Model.SiteUrl')   // the admin link is @Model.SiteUrl + literal "/admin"
    .replace(/\{3\}/g, '@Model.UserName')
    .replace(/\{4\}/g, '@Model.Password');

// 3) Razor header: typed model, no site-theme layout, and a do-not-edit notice.
const header = [
    '@model DemoSiteCreatedEmailViewModel',
    '@{',
    '    // GENERATED from Assets/Emails/demo-site-created.mjml by build-email.mjs. Do not edit by hand;',
    '    // edit the .mjml and re-run the script. Layout is cleared so the email is not wrapped in the',
    '    // site theme. The @@ escapes render as a single @ (CSS at-rules, the contact address); only the',
    '    // Model.* expressions are Razor.',
    '    Layout = string.Empty;',
    '}',
    '',
].join('\n');

writeFileSync(outFile, header + body + '\n');
console.log(`Wrote ${outFile} (${(header + body).length} bytes)`);
