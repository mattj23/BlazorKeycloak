# Server Side Blazor/Keycloak

This is a template for an ASP.NET 5.0 server-side Blazor app which uses Keycloak for OpenID Connect authentication. 

This is an easy way to deploy an internal line of business application in an environment with traditional Active Directory Domain Services identity management.  Keycloak can federate with a LDAP identity provider, and AD-DS security groups can be brought through into Keycloak as realm roles then used to protect components and controllers with the `UserRealmRoleRequirement`. 

In an AD-DS environment Keycloak can be quickly set up in a container using Docker or Kubernetes.  It can use many common RDBMs as a backend making the container deployment effectively stateless.  

This template borrows heavily from https://github.com/vip32/aspnetcore-keycloak