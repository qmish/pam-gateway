using Microsoft.Extensions.Configuration;
using PamGateway.Core;
using PamGateway.Integrations;

namespace PamGateway.Api;

public sealed class StubCmdbClient : ICmdbClient
{
    private readonly IReadOnlyList<CmdbTarget> _targets;

    public StubCmdbClient(IConfiguration configuration)
    {
        var targets = configuration.GetSection("Targets").Get<List<TargetSystem>>() ?? new List<TargetSystem>();
        _targets = targets
            .Select(item => new CmdbTarget(item.Id, item.Name, item.Type, item.Environment, item.Criticality, item.Status))
            .ToList();
    }

    public Task<IReadOnlyList<CmdbTarget>> FetchTargetsAsync(CancellationToken cancellationToken)
        => Task.FromResult(_targets);
}
