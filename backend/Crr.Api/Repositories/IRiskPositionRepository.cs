using Crr.Api.Models;

namespace Crr.Api.Repositories;

public interface IRiskPositionRepository
{
    IReadOnlyList<RiskPosition> GetAll();
}
