using Hangfire.Dashboard;

namespace BitrixChecker.Infrastructure;

public sealed class AdminOnlyDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext?.User?.IsInRole("Admin") == true;
    }
}
