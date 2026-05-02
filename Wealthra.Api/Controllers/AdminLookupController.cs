using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wealthra.Application.Common.Security;
using Wealthra.Domain.Enums;

namespace Wealthra.Api.Controllers;

[Authorize(Policy = AuthPolicies.AnyStaff)]
[Route("api/admin/lookup")]
public class AdminLookupController : AdminApiController
{
    [HttpGet]
    public IActionResult GetMetadata()
    {
        var roles = Enum.GetNames(typeof(Roles));
        var tiers = Enum.GetValues(typeof(SubscriptionTier))
            .Cast<SubscriptionTier>()
            .Select(t => new { Id = (int)t, Name = t.ToString() })
            .ToList();

        return Ok(new
        {
            Roles = roles,
            SubscriptionTiers = tiers
        });
    }
}
