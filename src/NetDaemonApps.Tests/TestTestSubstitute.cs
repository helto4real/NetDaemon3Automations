using System.Reactive.Linq;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Mocks.NSubstitute;
using System;
using System.Linq;
using NetDaemon.HassModel.Entities;
using NSubstitute;

namespace NetDaemonApps.Tests;

public class AppSubs
{
    public AppSubs(IHaContext ha)
    {
        ha.StateChanges()
            .Where(n => n.Entity.EntityId=="some.entity" && n.New?.State == "on")
            .Subscribe(_ =>
            {
                ha.CallService("domain", "service", ServiceTarget.FromEntity("some.entity"));
            });

    }
}

public class TestTestSubs
{
    [Fact]
    public void TestStuff()
    {
        var haMock = new HaContextMock();

        var app = new App(haMock);
        haMock.TriggerStateChange("some.entity", new EntityState{EntityId = "some.entity", State = "on"});

        haMock.VerifyServiceCalled(new Entity(haMock, "some.entity"), "domain", "service");
    }

    [Fact]
    public void TestNSubstituteStuff()
    {
        var haMock = new HaContextMock();

        var app = new App(haMock);
        haMock.TriggerStateChange("some.entity", new EntityState{EntityId = "some.entity", State = "on"});

        haMock.Mock.Received()
            .CallService(
                "domain",
                "service",
                Arg.Is<ServiceTarget>(n => n.EntityIds!.Contains("some.entity")),
                Arg.Any<object?>()
            );
    }
}
