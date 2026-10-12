using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Demo
{
    public class Config
    {
        private static string _mcpServerUrl = null!;
        private static string _webAppUrl = null!;
        private static string _webAppClientSecret = null!;
        private static string _apiUrl = null!;

        public static void Configure(
            string mcpServerUrl,
            string webAppUrl,
            string webAppClientSecret,
            string apiUrl)
        {
            if (!Uri.TryCreate(mcpServerUrl, UriKind.Absolute, out _) ||
                !Uri.TryCreate(webAppUrl, UriKind.Absolute, out _) ||
                string.IsNullOrWhiteSpace(webAppClientSecret) ||
                !Uri.TryCreate(apiUrl, UriKind.Absolute, out _))
            {
                throw new ArgumentException("Valid MCP and WebApp URLs and a WebApp client secret are required.");
            }

            _mcpServerUrl = mcpServerUrl.TrimEnd('/') + "/";
            _webAppUrl = webAppUrl.TrimEnd('/');
            _webAppClientSecret = webAppClientSecret;
            _apiUrl = apiUrl.TrimEnd('/');
        }
        private static List<string> AllIdentityScopes =>
            IdentityResources.Select(s => s.Name).ToList();

        private static List<string> AllApiScopes =>
            ApiScopes.Select(s => s.Name).ToList();

        private static List<string> AllScopes =>
            AllApiScopes.Concat(AllIdentityScopes).ToList();

        public static IEnumerable<IdentityResource> IdentityResources =>
            new List<IdentityResource>
            {
                new IdentityResources.OpenId(),
                new IdentityResources.Profile(),
                new IdentityResources.Email(),
            };

        public static IEnumerable<ApiScope> ApiScopes =>
            new List<ApiScope>
            {
                // backward compat
                new ApiScope("api"),

                // resource specific scopes
                new ApiScope("resource1.scope1"),
                new ApiScope("resource1.scope2"),

                new ApiScope("resource2.scope1"),
                new ApiScope("resource2.scope2"),

                new ApiScope("resource3.scope1"),
                new ApiScope("resource3.scope2"),

                // a scope without resource association
                new ApiScope("scope3"),
                new ApiScope("scope4"),

                // a scope shared by multiple resources
                new ApiScope("shared.scope"),

                // a parameterized scope
                new ApiScope("transaction", "Transaction")
                {
                    Description = "Some Transaction"
                }
            };

        public static IEnumerable<ApiResource> ApiResources =>
            new List<ApiResource>
            {
                new ApiResource("api", "Demo API", new[] { "name", "email" })
                {
                    ApiSecrets = { new Secret("secret".Sha256()) },

                    Scopes = { "api" }
                },

                new ApiResource(_mcpServerUrl, "CarvedRock MCP", new[] { "name", "email" })
                {
                    Scopes = { "api" }
                },

                new ApiResource("urn:resource1", "Resource 1")
                {
                    ApiSecrets = { new Secret("secret".Sha256()) },

                    Scopes = { "resource1.scope1", "resource1.scope2", "shared.scope" }
                },

                new ApiResource("urn:resource2", "Resource 2")
                {
                    ApiSecrets = { new Secret("secret".Sha256()) },

                    Scopes = { "resource2.scope1", "resource2.scope2", "shared.scope" }
                },

                new ApiResource("urn:resource3", "Resource 3 (isolated)")
                {
                    ApiSecrets = { new Secret("secret".Sha256()) },
                    RequireResourceIndicator = true,

                    Scopes = { "resource3.scope1", "resource3.scope2", "shared.scope" }
                }
            };

        private static Secret PublicKey = new Secret
        {
            Type = IdentityServerConstants.SecretTypes.JsonWebKey,
            Value =
                """
                {
                    "e":"AQAB",
                    "kid":"ZzAjSnraU3bkWGnnAqLapYGpTyNfLbjbzgAPbbW2GEA",
                    "kty":"RSA",
                    "n":"wWwQFtSzeRjjerpEM5Rmqz_DsNaZ9S1Bw6UbZkDLowuuTCjBWUax0vBMMxdy6XjEEK4Oq9lKMvx9JzjmeJf1knoqSNrox3Ka0rnxXpNAz6sATvme8p9mTXyp0cX4lF4U2J54xa2_S9NF5QWvpXvBeC4GAJx7QaSw4zrUkrc6XyaAiFnLhQEwKJCwUw4NOqIuYvYp_IXhw-5Ti_icDlZS-282PcccnBeOcX7vc21pozibIdmZJKqXNsL1Ibx5Nkx1F1jLnekJAmdaACDjYRLL_6n3W4wUp19UvzB1lGtXcJKLLkqB6YDiZNu16OSiSprfmrRXvYmvD8m6Fnl5aetgKw"
                }
                """
        };

        public static IEnumerable<Client> Clients =>
            new List<Client>
            {
                // non-interactive
                new Client
                {
                    ClientId = "m2m",
                    ClientName = "Machine to machine (client credentials)",
                    ClientSecrets = { new Secret("secret".Sha256()) },

                    AllowedGrantTypes = GrantTypes.ClientCredentials,
                    AllowedScopes = AllApiScopes,
                },
                new Client
                {
                    ClientId = "m2m.jwt",
                    ClientName = "Machine to machine (client credentials with JWT)",
                    AllowedGrantTypes = GrantTypes.ClientCredentials,
                    AllowedScopes = AllApiScopes,

                    ClientSecrets = { PublicKey }
                },
                new Client
                {
                    ClientId = "m2m.dpop",
                    ClientName = "Machine to machine (client credentials)",
                    ClientSecrets = { new Secret("secret".Sha256()) },

                    AllowedGrantTypes = GrantTypes.ClientCredentials,
                    AllowedScopes = AllApiScopes,

                    RequireDPoP = true,
                },
                new Client
                {
                    ClientId = "m2m.dpop.nonce",
                    ClientName = "Machine to machine (client credentials)",
                    ClientSecrets = { new Secret("secret".Sha256()) },

                    AllowedGrantTypes = GrantTypes.ClientCredentials,
                    AllowedScopes = AllApiScopes,

                    RequireDPoP = true,
                    DPoPValidationMode = DPoPTokenExpirationValidationMode.Nonce,
                },
                new Client
                {
                    ClientId = "m2m.short",
                    ClientName = "Machine to machine with short access token lifetime (client credentials)",
                    ClientSecrets = { new Secret("secret".Sha256()) },

                    AllowedGrantTypes = GrantTypes.ClientCredentials,
                    AllowedScopes = AllApiScopes,
                    AccessTokenLifetime = 75
                },
                new Client
                {
                    ClientId = "m2m.short.jwt",
                    ClientName = "Machine to machine (client credentials with JWT)",
                    AllowedGrantTypes = GrantTypes.ClientCredentials,
                    AllowedScopes = AllApiScopes,
                    AccessTokenLifetime = 75,

                    ClientSecrets = { PublicKey }
                },

                // interactive
                new Client
                {
                    ClientId = "interactive.public",
                    ClientName = "MCP Inspector",
                    RequireClientSecret = false,
                    AllowedGrantTypes = GrantTypes.Code,
                    RequirePkce = true,
                    RequireConsent = false,
                    RedirectUris =
                    {
                        "http://localhost:6274/oauth/callback",
                        "http://localhost:6274/oauth/callback/debug",
                        $"{_apiUrl}/swagger/oauth2-redirect.html"
                    },
                    AllowedCorsOrigins = { "http://localhost:6274", _apiUrl },
                    AllowedScopes = { "openid", "profile", "email", "api" },
                    AllowOfflineAccess = true
                },

                new Client
                {
                    ClientId = "interactive.confidential",
                    ClientName = "CarvedRock WebApp",
                    ClientSecrets = { new Secret(_webAppClientSecret.Sha256()) },
                    AllowedGrantTypes = GrantTypes.Code,
                    RequirePkce = true,
                    RequireConsent = false,
                    RedirectUris = { $"{_webAppUrl}/signin-oidc" },
                    PostLogoutRedirectUris = { $"{_webAppUrl}/signout-callback-oidc" },
                    AllowedScopes = { "openid", "profile", "email", "api" },
                    AllowOfflineAccess = true
                },


                new Client
                {
                    ClientId = "interactive.confidential.jwt",
                    ClientName = "Interactive client (Code with PKCE) using private key JWT authentication",

                    RedirectUris = { "https://notused" },
                    PostLogoutRedirectUris = { "https://notused" },

                    ClientSecrets = { PublicKey },

                    AllowedGrantTypes = GrantTypes.CodeAndClientCredentials,
                    RequireRequestObject = false,
                    AllowedScopes = AllScopes,

                    AllowOfflineAccess = true,
                    RefreshTokenUsage = TokenUsage.ReUse,
                    RefreshTokenExpiration = TokenExpiration.Sliding
                },

                new Client
                {
                    ClientId = "interactive.confidential.jar.jwt",
                    ClientName = "Interactive client (Code with PKCE) using JAR and private key JWT",

                    RedirectUris = { "https://notused" },
                    PostLogoutRedirectUris = { "https://notused" },

                    ClientSecrets = { PublicKey },

                    AllowedGrantTypes = GrantTypes.CodeAndClientCredentials,
                    RequireRequestObject = true,
                    AllowedScopes = AllScopes,

                    AllowOfflineAccess = true,
                    RefreshTokenUsage = TokenUsage.ReUse,
                    RefreshTokenExpiration = TokenExpiration.Sliding
                },

                new Client
                {
                    ClientId = "interactive.confidential.short",
                    ClientName = "Interactive client with short token lifetime (Code with PKCE)",

                    RedirectUris = { "https://notused" },
                    PostLogoutRedirectUris = { "https://notused" },

                    ClientSecrets = { new Secret("secret".Sha256()) },
                    RequireConsent = false,

                    AllowedGrantTypes = GrantTypes.CodeAndClientCredentials,
                    RequirePkce = true,
                    AllowedScopes = AllScopes,

                    AllowOfflineAccess = true,
                    RefreshTokenUsage = TokenUsage.ReUse,
                    RefreshTokenExpiration = TokenExpiration.Sliding,

                    AccessTokenLifetime = 75
                },

                new Client
                {
                    ClientId = "interactive.confidential.short.jwt",
                    ClientName = "Interactive client (Code with PKCE) using private key JWT authentication with short access token lifetime",

                    RedirectUris = { "https://notused" },
                    PostLogoutRedirectUris = { "https://notused" },

                    ClientSecrets = { PublicKey },

                    AllowedGrantTypes = GrantTypes.CodeAndClientCredentials,
                    RequireRequestObject = false,
                    AllowedScopes = AllScopes,

                    AllowOfflineAccess = true,
                    RefreshTokenUsage = TokenUsage.ReUse,
                    RefreshTokenExpiration = TokenExpiration.Sliding,

                    AccessTokenLifetime = 75
                },

                new Client
                {
                    ClientId = "interactive.confidential.short.jar.jwt",
                    ClientName = "Interactive client (Code with PKCE) using JAR and private key JWT",

                    RedirectUris = { "https://notused" },
                    PostLogoutRedirectUris = { "https://notused" },

                    ClientSecrets = { PublicKey },

                    AllowedGrantTypes = GrantTypes.CodeAndClientCredentials,
                    RequireRequestObject = true,
                    AllowedScopes = AllScopes,

                    AllowOfflineAccess = true,
                    RefreshTokenUsage = TokenUsage.ReUse,
                    RefreshTokenExpiration = TokenExpiration.Sliding,

                    AccessTokenLifetime = 75
                },

                new Client
                {
                    ClientId = "native.dpop",
                    ClientName = "Native client (Code with PKCE + DPop)",

                    RedirectUris = { "https://notused" },
                    PostLogoutRedirectUris = { "https://notused" },

                    RequireClientSecret = false,

                    AllowedGrantTypes = GrantTypes.Code,
                    AllowedScopes = AllScopes,

                    AllowOfflineAccess = true,
                    RefreshTokenUsage = TokenUsage.ReUse,
                    RefreshTokenExpiration = TokenExpiration.Sliding,

                    RequireDPoP = true,
                    DPoPValidationMode = DPoPTokenExpirationValidationMode.Nonce
                },

                new Client
                {
                    ClientId = "interactive.public.short",
                    ClientName = "Interactive client with short token lifetime (Code with PKCE)",

                    RedirectUris = { "https://notused" },
                    PostLogoutRedirectUris = { "https://notused" },

                    RequireClientSecret = false,

                    AllowedGrantTypes = GrantTypes.Code,
                    AllowedScopes = AllScopes,

                    AllowOfflineAccess = true,
                    RefreshTokenUsage = TokenUsage.OneTimeOnly,
                    RefreshTokenExpiration = TokenExpiration.Sliding,

                    AccessTokenLifetime = 75
                },

                // not recommended - only for clients that do not support PKCE
                new Client
                {
                    ClientId = "interactive.confidential.nopkce",
                    ClientName = "Interactive client (Code without PKCE)",

                    RedirectUris = { "https://notused" },
                    PostLogoutRedirectUris = { "https://notused" },

                    ClientSecrets = { new Secret("secret".Sha256()) },
                    RequirePkce = false,

                    AllowedGrantTypes = GrantTypes.CodeAndClientCredentials,
                    AllowedScopes = AllScopes,

                    AllowOfflineAccess = true,
                    RefreshTokenUsage = TokenUsage.ReUse,
                    RefreshTokenExpiration = TokenExpiration.Sliding
                },

                // hybrid as alternative to PKCE
                new Client
                {
                    ClientId = "interactive.confidential.hybrid",
                    ClientName = "Interactive client (Code with Hybrid Flow)",

                    RedirectUris = { "https://notused" },
                    PostLogoutRedirectUris = { "https://notused" },

                    ClientSecrets = { new Secret("secret".Sha256()) },
                    RequirePkce = false,

                    AllowedGrantTypes = GrantTypes.HybridAndClientCredentials,
                    AllowedScopes = AllScopes,

                    AllowOfflineAccess = true,
                    RefreshTokenUsage = TokenUsage.ReUse,
                    RefreshTokenExpiration = TokenExpiration.Sliding
                },

                new Client
                {
                    ClientId = "device",
                    ClientName = "Device Flow Client",

                    AllowedGrantTypes = GrantTypes.DeviceFlow,
                    RequireClientSecret = false,

                    AllowOfflineAccess = true,
                    RefreshTokenUsage = TokenUsage.OneTimeOnly,
                    RefreshTokenExpiration = TokenExpiration.Sliding,

                    AllowedScopes = AllScopes,
                },

                // oidc login only
                new Client
                {
                    ClientId = "interactive.implicit",

                    RedirectUris = { "https://notused" },
                    PostLogoutRedirectUris = { "https://notused" },

                    AllowedGrantTypes = GrantTypes.Implicit,
                    AllowedScopes = AllIdentityScopes,
                },

                // oidc login only - legacy client name. Keep until end of 2026
                // to not break existing demos.
                new Client
                {
                    ClientId = "login",

                    RedirectUris = { "https://notused" },
                    PostLogoutRedirectUris = { "https://notused" },

                    AllowedGrantTypes = GrantTypes.Implicit,
                    AllowedScopes = AllIdentityScopes,
                }
            };
    }
}
