# Cloudflare Pages — staging site for `20062006.xyz`

Static demo page only. **$0** on the free Cloudflare plan. This is not NL session hosting.

## Upload (no GitHub required)

1. [Cloudflare dashboard](https://dash.cloudflare.com) → **Workers & Pages** → **Create** → **Pages** → **Upload assets**.
2. Project name: `nl-staging` (anything).
3. Drag the contents of **this folder** (`index.html` only is enough). Deploy.
4. **Custom domains** → `20062006.xyz` (and `www.20062006.xyz` if you want). Cloudflare will attach DNS.
5. Open `https://20062006.xyz`. Done when HTTPS loads this page.

Leave **play.** / **relay-*** records **unset** until an 8 GB VPS exists.

When the VPS exists: keep this apex site on Pages (orange cloud). Put `play.20062006.xyz` as a grey-cloud **A** record to the VPS. Game port **25555** cannot go through the Cloudflare proxy.
