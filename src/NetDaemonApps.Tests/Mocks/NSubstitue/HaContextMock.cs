using System.Linq;
using NetDaemon.HassModel.Entities;
using NSubstitute;

namespace NetDaemon.HassModel.Mocks.NSubstitute;

public class HaContextMock : HaContextMockBase, IHaContextMock
{
    public IHaContext Mock { get; } = Substitute.For<IHaContext>();

    public override void CallService(string domain, string service, ServiceTarget? target = null, object? data = null)
    {
        Mock.CallService(domain, service, target, data);
    }

    public override void SendEvent(string eventType, object? data = null)
    {
        Mock.SendEvent(eventType, data);
    }

    public override void VerifyServiceCalled(Entity entity, string domain, string service)
    {
        Mock
            .Received()
            .CallService(
                domain,
                service,
                Arg.Is<ServiceTarget>(n => n.EntityIds!.Contains(entity.EntityId)),
                Arg.Any<object?>()
            );
    }
}
