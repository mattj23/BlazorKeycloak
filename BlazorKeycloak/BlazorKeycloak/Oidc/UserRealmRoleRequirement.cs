using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace BlazorKeycloak.Oidc
{
    public class UserRealmRoleRequirement : IAuthorizationRequirement
    {
        public UserRealmRoleRequirement(string realmRoleName)
        {
            RealmRoleName = realmRoleName;
        }

        public string RealmRoleName { get; }
        
    }

    public class OfficeAdminRequirementHandler : AuthorizationHandler<UserRealmRoleRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, UserRealmRoleRequirement requirement)
        {
            if (context.User.HasClaim(c => c.Type == "user_realm_roles" && c.Value == requirement.RealmRoleName))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}