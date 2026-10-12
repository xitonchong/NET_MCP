using System.Security.Claims; 
using Microsoft.AspNetCore.Authentication; 


namespace CarvedRock.Core;


public class AdminClaimsTransformation : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var emailClaim = principal.Claims.FirstOrDefault(c => 
            c.Type == ClaimTypes.Email || 
            c.Type == "email"); 

        if (principal.Identity?.IsAuthenticated == true &&
            !principal.IsInRole("admin") &&
            emailClaim != null &&
            emailClaim.Value.Split('@')[0].Equals("bobsmith", StringComparison.OrdinalIgnoreCase))
        {
            var clone = new ClaimsPrincipal(principal.Identities.Select(identity => identity.Clone()));
            var newIdentity = clone.Identities.First(identity => identity.IsAuthenticated);
            
            newIdentity.AddClaim(new Claim(newIdentity.RoleClaimType, "admin"));
            
            return Task.FromResult(clone);
        }
        return Task.FromResult(principal); 
    }
}
