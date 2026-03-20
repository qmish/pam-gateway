using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PamGateway.Api;
using PamGateway.Api.Services;
using PamGateway.Core;

namespace PamGateway.Tests.Unit;

public sealed class SystemDataSeederTests
{
    private readonly InMemoryRoleStore _roles = new();
    private readonly InMemoryPolicyStore _policies = new();

    private SystemDataSeeder CreateSeeder()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRoleStore>(_roles);
        services.AddSingleton<IPolicyStore>(_policies);
        var sp = services.BuildServiceProvider();
        return new SystemDataSeeder(sp, Substitute.For<ILogger<SystemDataSeeder>>());
    }

    [Fact]
    public async Task SeedAsync_SeedsRolesOnEmptyStore()
    {
        var seeder = CreateSeeder();
        await seeder.SeedAsync();

        _roles.GetAll().Should().NotBeEmpty();
        _roles.GetAll().Should().Contain(r => r.Name == "PAM_Administrator");
        _roles.GetAll().Should().Contain(r => r.Name == "Security_Auditor");
        _roles.GetAll().Should().Contain(r => r.Name == "DB_Admin");
    }

    [Fact]
    public async Task SeedAsync_SeedsPoliciesOnEmptyStore()
    {
        var seeder = CreateSeeder();
        await seeder.SeedAsync();

        _policies.GetAll().Should().NotBeEmpty();
        _policies.GetAll().Should().Contain(p => p.Name == "RDP Access");
        _policies.GetAll().Should().Contain(p => p.Name == "SSH Access");
    }

    [Fact]
    public async Task SeedAsync_DoesNotDuplicateOnSecondRun()
    {
        var seeder = CreateSeeder();
        await seeder.SeedAsync();
        var roleCount = _roles.GetAll().Count;

        await seeder.SeedAsync();
        _roles.GetAll().Should().HaveCount(roleCount);
    }

    [Fact]
    public async Task SeedAsync_SkipsRolesIfStoreNotEmpty()
    {
        _roles.Add(new Role("ROLE-EXISTING", "Existing", "Already exists"));

        var seeder = CreateSeeder();
        await seeder.SeedAsync();

        _roles.GetAll().Should().HaveCount(1);
        _roles.GetAll().First().Name.Should().Be("Existing");
    }

    [Fact]
    public async Task SeedAsync_SkipsPoliciesIfStoreNotEmpty()
    {
        _policies.Add(new Policy("POL-EXISTING", "Existing", "SSH", "ssh", "Allow", null));

        var seeder = CreateSeeder();
        await seeder.SeedAsync();

        _policies.GetAll().Should().HaveCount(1);
    }
}
