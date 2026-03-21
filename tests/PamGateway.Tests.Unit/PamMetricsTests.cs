using System.Diagnostics.Metrics;
using FluentAssertions;
using PamGateway.Api.Services;

namespace PamGateway.Tests.Unit;

public sealed class PamMetricsTests
{
    private PamMetrics CreateMetrics()
    {
        var factory = new TestMeterFactory();
        return new PamMetrics(factory);
    }

    [Fact]
    public void SessionStarted_DoesNotThrow()
    {
        var metrics = CreateMetrics();
        var act = () => metrics.SessionStarted();
        act.Should().NotThrow();
    }

    [Fact]
    public void AllCounterMethods_DoNotThrow()
    {
        var metrics = CreateMetrics();
        metrics.SessionStarted();
        metrics.SessionTerminated();
        metrics.RequestCreated();
        metrics.RequestApproved();
        metrics.RequestDenied();
        metrics.RequestExpired();
        metrics.IntegrationError("jira");
        metrics.PolicyDenial();
        metrics.ActiveSessionChanged(1);
        metrics.ActiveSessionChanged(-1);
        metrics.OnlineAgentChanged(1);
        metrics.OnlineAgentChanged(-1);
    }

    [Fact]
    public void SessionStarted_IncreasesCounter()
    {
        var factory = new TestMeterFactory();
        var metrics = new PamMetrics(factory);

        long captured = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Name == "pam.sessions.started")
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            captured += measurement;
        });
        listener.Start();

        metrics.SessionStarted();
        metrics.SessionStarted();
        listener.RecordObservableInstruments();

        captured.Should().Be(2);
    }

    private sealed class TestMeterFactory : IMeterFactory
    {
        private readonly List<Meter> _meters = new();

        public Meter Create(MeterOptions options)
        {
            var meter = new Meter(options);
            _meters.Add(meter);
            return meter;
        }

        public void Dispose()
        {
            foreach (var m in _meters) m.Dispose();
        }
    }
}
