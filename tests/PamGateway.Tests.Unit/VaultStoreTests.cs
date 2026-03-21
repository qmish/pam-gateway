using FluentAssertions;
using PamGateway.Api;
using PamGateway.Core;

namespace PamGateway.Tests.Unit;

public sealed class InMemoryCredentialStoreTests
{
    private readonly InMemoryCredentialStore _store = new();

    private Credential MakeCred(string id = "c1", string targetId = "t1") => new(
        id, targetId, "admin", "enc-pass",
        CredentialStatus.Available, DateTimeOffset.UtcNow,
        null, null, null, false, 24);

    [Fact]
    public void Add_And_GetById()
    {
        var c = _store.Add(MakeCred());
        _store.GetById("c1").Should().NotBeNull();
        _store.GetById("c1")!.Username.Should().Be("admin");
    }

    [Fact]
    public void GetAll_ReturnsAll()
    {
        _store.Add(MakeCred("c1"));
        _store.Add(MakeCred("c2"));
        _store.GetAll().Should().HaveCount(2);
    }

    [Fact]
    public void GetByTargetId_Filters()
    {
        _store.Add(MakeCred("c1", "t1"));
        _store.Add(MakeCred("c2", "t2"));
        _store.Add(MakeCred("c3", "t1"));
        _store.GetByTargetId("t1").Should().HaveCount(2);
        _store.GetByTargetId("t2").Should().HaveCount(1);
    }

    [Fact]
    public void Update_ModifiesExisting()
    {
        _store.Add(MakeCred());
        var updated = _store.GetById("c1")! with { Status = CredentialStatus.CheckedOut };
        _store.Update(updated);
        _store.GetById("c1")!.Status.Should().Be(CredentialStatus.CheckedOut);
    }

    [Fact]
    public void GetById_NotFound_ReturnsNull()
    {
        _store.GetById("nope").Should().BeNull();
    }
}

public sealed class InMemoryCredentialCheckoutStoreTests
{
    private readonly InMemoryCredentialCheckoutStore _store = new();

    private CredentialCheckout MakeCheckout(string id = "co1", string credId = "c1") => new(
        id, credId, "user1", DateTimeOffset.UtcNow, null, "testing");

    [Fact]
    public void Add_And_GetById()
    {
        _store.Add(MakeCheckout());
        _store.GetById("co1").Should().NotBeNull();
    }

    [Fact]
    public void GetByCredentialId_Filters()
    {
        _store.Add(MakeCheckout("co1", "c1"));
        _store.Add(MakeCheckout("co2", "c2"));
        _store.Add(MakeCheckout("co3", "c1"));
        _store.GetByCredentialId("c1").Should().HaveCount(2);
    }

    [Fact]
    public void Update_SetsCheckedInAt()
    {
        _store.Add(MakeCheckout());
        var updated = _store.GetById("co1")! with { CheckedInAt = DateTimeOffset.UtcNow };
        _store.Update(updated);
        _store.GetById("co1")!.CheckedInAt.Should().NotBeNull();
    }

    [Fact]
    public void GetAll_ReturnsAll()
    {
        _store.Add(MakeCheckout("co1"));
        _store.Add(MakeCheckout("co2"));
        _store.GetAll().Should().HaveCount(2);
    }
}
