using GymApp.Domain.Entities;

namespace GymApp.Api.Core;

public static class LocationValidator
{
    public static bool CanDeleteLocation(Location location, int totalLocations, int futureSessions)
    {
        return totalLocations > 1 && futureSessions == 0;
    }

    public static string? GetDeleteLocationError(Location location, int totalLocations, int futureSessions)
    {
        if (totalLocations <= 1)
            return "Não é possível excluir a última localização.";

        if (futureSessions > 0)
            return $"Esta localização possui {futureSessions} sessão(ões) futuras. Cancele ou mova as sessões antes de excluir.";

        return null;
    }

    public static void EnsureSingleMainLocation(Location newMain, IEnumerable<Location> existingLocations)
    {
        foreach (var loc in existingLocations.Where(l => l.IsMain && l.Id != newMain.Id))
        {
            loc.IsMain = false;
        }
        newMain.IsMain = true;
    }
}

public static class InstructorServiceManager
{
    public static List<Guid> FilterValidServiceIds(
        IEnumerable<Guid> requestedIds,
        IEnumerable<Guid> existingServiceIds,
        Func<Guid, bool> serviceExistsInTenant)
    {
        return requestedIds
            .Distinct()
            .Where(id => serviceExistsInTenant(id))
            .ToList();
    }

    public static void SyncInstructorServices(
        Instructor instructor,
        IEnumerable<Guid> newServiceIds,
        Func<Guid, bool> serviceExistsInTenant)
    {
        instructor.Services.Clear();
        foreach (var svcId in newServiceIds.Distinct())
        {
            if (serviceExistsInTenant(svcId))
            {
                instructor.Services.Add(new InstructorService
                {
                    InstructorId = instructor.Id,
                    ClassTypeId = svcId
                });
            }
        }
    }
}