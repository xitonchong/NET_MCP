// Run against the local Aspire HTTPS WebApp. Uses only the tutorial Alice/Bob accounts.
// The optional --checkout flag sends one confirmation to local Mailpit; no product is changed.
import { randomBytes, createHash } from "node:crypto";
const base = process.argv[2];
if (!base) throw new Error("Usage: node tests/webapp-smoke.mjs <webapp-https-url> [--checkout]");
if (new URL(base).hostname !== "localhost") throw new Error("This check only runs against localhost.");
const decode = value => value.replace(/&amp;/g, "&").replace(/&quot;/g, '"').replace(/&#x([0-9a-f]+);/gi, (_, n) => String.fromCharCode(parseInt(n, 16))).replace(/&#([0-9]+);/g, (_, n) => String.fromCharCode(Number(n)));
const attributes = tag => Object.fromEntries([...tag.matchAll(/([\w.-]+)\s*=\s*["']([^"']*)["']/g)].map(m => [m[1].toLowerCase(), decode(m[2])]));
function form(html, marker) {
    const block = [...html.matchAll(/<form\b[^>]*>[\s\S]*?<\/form>/gi)].map(m => m[0]).find(s => s.includes(marker));
    if (!block) throw new Error("Expected form was not found: " + marker);
    const fields = new URLSearchParams();
    for (const input of block.matchAll(/<input\b[^>]*>/gi)) {
        const a = attributes(input[0]);
        if (a.name && a.type !== "checkbox") fields.set(a.name, a.value ?? "");
    }
    return { action: attributes(block.match(/^<form\b[^>]*>/i)[0]).action, fields };
}
function browser() {
    const cookies = new Map();
    async function request(url, options = {}) {
        url = new URL(url, base);
        if (url.hostname !== "localhost") throw new Error("Unexpected external redirect blocked.");
        const headers = new Headers(options.headers);
        headers.set("cookie", [...cookies.values()].filter(c => url.pathname.startsWith(c.path)).map(c => c.pair).join("; "));
        const response = await fetch(url, { ...options, headers, redirect: "manual", signal: AbortSignal.timeout(15000) });
        for (const cookie of response.headers.getSetCookie()) {
            const pair = cookie.split(";")[0];
            const name = pair.split("=")[0];
            const path = /;\s*path=([^;]+)/i.exec(cookie)?.[1] ?? "/";
            const key = name + path;
            if (/max-age=0|expires=Thu, 01 Jan 1970/i.test(cookie)) cookies.delete(key);
            else cookies.set(key, { pair, path });
        }
        return { status: response.status, url: url.href, location: response.headers.get("location"), html: await response.text() };
    }
    async function follow(url, user, options = {}, stopAt = null) {
        let page = await request(url, options);
        for (let i = 0; i < 20; i++) {
            if (page.location && page.status >= 300 && page.status < 400) {
                const target = new URL(page.location, page.url);
                if (stopAt && target.origin + target.pathname === stopAt)
                    return { status: 200, url: target.href, html: "" };
                page = await request(target);
            } else if (page.html.includes('name="Input.Username"')) {
                if (user === null) return page;
                const login = form(page.html, "Input.Username");
                login.fields.set("Input.Username", user);
                login.fields.set("Input.Password", user);
                login.fields.set("Input.Button", "login");
                page = await request(new URL(login.action || page.url, page.url), { method: "POST", body: login.fields });
            } else if (/name=['"]code['"]/.test(page.html) && /name=['"]state['"]/.test(page.html)) {
                const callback = form(page.html, "code");
                page = await request(new URL(callback.action, page.url), { method: "POST", body: callback.fields });
            } else return page;
        }
        throw new Error("Too many authentication redirects.");
    }
    return { request, follow };
}
if (process.argv.includes("--mcp")) {
    const authority = process.env.IDENTITYSERVER_URL ?? "https://localhost:5001";
    const endpoint = process.env.MCP_URL ?? "https://localhost:7275/";
    const callback = "http://localhost:6274/oauth/callback";
    for (const user of ["alice", "bob"]) {
        const verifier = randomBytes(32).toString("base64url");
        const state = randomBytes(16).toString("hex");
        const authorize = new URL("/connect/authorize", authority);
        authorize.search = new URLSearchParams({ client_id: "interactive.public", response_type: "code",
            redirect_uri: callback, scope: "openid profile email api", state,
            code_challenge: createHash("sha256").update(verifier).digest("base64url"), code_challenge_method: "S256" });
        const result = await browser().follow(authorize, user, {}, callback);
        const resultUrl = new URL(result.url);
        if (resultUrl.searchParams.get("state") !== state || !resultUrl.searchParams.get("code"))
            throw new Error("MCP OAuth authorization did not return a valid code.");
        const tokenResponse = await fetch(new URL("/connect/token", authority), { method: "POST", body: new URLSearchParams({
            grant_type: "authorization_code", client_id: "interactive.public", redirect_uri: callback,
            code: resultUrl.searchParams.get("code"), code_verifier: verifier
        }) });
        const tokens = await tokenResponse.json();
        if (!tokens.access_token) throw new Error("MCP token exchange failed.");
        let sessionId;
        async function rpc(message) {
            const headers = { "Content-Type": "application/json", Accept: "application/json, text/event-stream",
                Authorization: "Bearer " + tokens.access_token };
            if (sessionId) headers["Mcp-Session-Id"] = sessionId;
            const response = await fetch(endpoint, { method: "POST", headers, body: JSON.stringify(message), signal: AbortSignal.timeout(15000) });
            sessionId = response.headers.get("Mcp-Session-Id") ?? sessionId;
            if (!response.ok) throw new Error("MCP request failed with HTTP " + response.status);
            const body = await response.text();
            const data = body.split("\n").filter(l => l.startsWith("data: ")).map(l => JSON.parse(l.slice(6)));
            return data.length ? data.find(m => m.id === message.id) : body ? JSON.parse(body) : null;
        }
        await rpc({ jsonrpc: "2.0", id: 1, method: "initialize", params: {
            protocolVersion: "2025-06-18", capabilities: {}, clientInfo: { name: "carvedrock-smoke", version: "1" }
        } });
        await rpc({ jsonrpc: "2.0", method: "notifications/initialized" });
        const products = await rpc({ jsonrpc: "2.0", id: 2, method: "tools/call", params: { name: "get_products", arguments: {} } });
        if (!products?.result || products.result.isError) throw new Error("MCP get_products failed for " + user);
        console.log(`PASS ${user}: MCP OAuth and authenticated get_products`);
        if (user === "alice") {
            const denied = await rpc({ jsonrpc: "2.0", id: 3, method: "tools/call", params: { name: "delete_product", arguments: { id: -1 } } });
            if (!denied?.result?.isError) throw new Error("Ordinary user unexpectedly passed the admin MCP operation.");
            console.log("PASS Alice: MCP admin operation rejected");
        }
    }
}
function requirePage(page, text) {
    if (page.status !== 200 || !page.html.includes(text))
        throw new Error(`Expected ${text}; HTTP ${page.status}, path ${new URL(page.url).pathname}`);
}
for (const user of ["alice", "bob"]) {
    const session = browser();
    requirePage(await session.follow(base, user), "Products");
    console.log(`PASS ${user}: OIDC login and product listing`);
    const admin = await session.follow(new URL("/Admin", base), user);
    requirePage(admin, user === "bob" ? "Product Administration" : "Access denied");
    console.log(`PASS ${user}: admin page authorization`);
    if (user === "bob") {
        for (const path of ["/Admin/Create", "/Admin/Edit?id=1", "/Admin/Delete?id=1", "/CurrentPromotion"])
            requirePage(await session.follow(new URL(path, base), user), path === "/CurrentPromotion" ? "Current promotion" : "Product");
        console.log("PASS admin forms and promotion render");
        if (process.argv.includes("--checkout")) {
            const shop = await session.follow(new URL("/Listing?cat=all", base), user);
            const add = form(shop.html, "Add to cart");
            requirePage(await session.follow(new URL(add.action, shop.url), user, { method: "POST", body: add.fields }), "Shopping cart");
            const checkout = await session.follow(new URL("/Checkout", base), user);
            const submit = form(checkout.html, "Submit order");
            requirePage(await session.follow(new URL(submit.action, checkout.url), user, { method: "POST", body: submit.fields }), "Thank you!");
            console.log("PASS checkout sends confirmation to configured local SMTP");
        }
    }
    const logoutPage = await session.follow(new URL("/Logout", base), user);
    const logout = form(logoutPage.html, "Sign out");
    await session.follow(new URL(logout.action || logoutPage.url, logoutPage.url), null, { method: "POST", body: logout.fields });
    const signedOut = await session.follow(base, null);
    requirePage(signedOut, 'name="Input.Username"');
    console.log(`PASS ${user}: WebApp and IdentityServer sign out`);
}
