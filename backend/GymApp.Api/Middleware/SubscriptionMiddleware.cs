using System.Security.Claims;
using GymApp.Infra.Services;

namespace GymApp.Api.Middleware;

/// <summary>
/// Runs after authentication. Blocks Student-role requests when the
/// tenant's subscription has no active access (trial expired, past due,
/// suspended, or cancellation period ended).
/// Admin users keep access so they can fix billing.
/// </summary>
public class SubscriptionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext)
    {
        if (tenantContext.IsResolved && !tenantContext.HasStudentAccess)
        {
            var role = context.User.FindFirstValue(ClaimTypes.Role);
            if (role == "Student")
            {
                context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "subscription_required",
                    message = "A academia não possui uma assinatura ativa."
                });
                return;
            }
        }

        await next(context);
    }
}
