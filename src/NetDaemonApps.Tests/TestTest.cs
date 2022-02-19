using System.Reactive.Linq;
using System.Runtime.InteropServices.ComTypes;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Mocks.Moq;
using System;
using System.Linq;
using Moq;
using NetDaemon.HassModel.Entities;

namespace NetDaemonApps.Tests;

public class App
{
    public App(IHaContext ha)
    {
        ha.StateChanges()
            .Where(n => n.Entity.EntityId=="some.entity" && n.New?.State == "on")
            .Subscribe(_ =>
            {
                ha.CallService("domain", "service", ServiceTarget.FromEntity("some.entity"));
            });

    }
}

public class TestTest
{
    [Fact]
    public void TestStuff()
    {
        var haMock = new HaContextMock();

        var app = new App(haMock.Object);
        haMock.TriggerStateChange("some.entity", new EntityState{EntityId = "some.entity", State = "on"});

        haMock.VerifyServiceCalled(new Entity(haMock.Object, "some.entity"), "domain", "service");
    }

    [Fact]
    public void TestUsingMoqStuff()
    {
        var haMock = new HaContextMock();

        var app = new App(haMock.Object);
        haMock.TriggerStateChange("some.entity", new EntityState{EntityId = "some.entity", State = "on"});

        haMock.Verify(n => n.CallService("domain", "service", It.Is<ServiceTarget>(n => n.EntityIds!.Contains("some.entity")), It.IsAny<object?>()));
    }
}
