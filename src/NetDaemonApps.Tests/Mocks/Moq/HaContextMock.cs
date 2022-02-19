using System.Linq;
using Moq;
using NetDaemon.HassModel.Entities;

namespace NetDaemon.HassModel.Mocks.Moq;

public class HaContextMock : Mock<HaContextMockBase>, IHaContextMock
{
    public HaContextMock()
    {
    }

    public void TriggerStateChange(Entity entity, string newStatevalue, object? attributes = null)
    {
        Object.TriggerStateChange(entity, newStatevalue, attributes);
    }

    public void TriggerStateChange(string entityId, EntityState newState)
    {
        Object.TriggerStateChange(entityId, newState);
    }

    public void VerifyServiceCalled(Entity entity, string domain, string service)
    {
        Verify(m => m.CallService(domain, service,
            It.Is<ServiceTarget?>(s => s!.EntityIds!.SingleOrDefault() == entity.EntityId),
            null));
    }
}
