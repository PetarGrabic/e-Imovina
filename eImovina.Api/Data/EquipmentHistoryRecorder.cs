using eImovina.Api.Data.Entities;

namespace eImovina.Api.Data;

/// <summary>
/// Appends a history row whenever equipment location/status actually changes. Called directly
/// from the controllers that own these writes (EquipmentController, AssignmentsController,
/// WriteOffRequestsController) - no service layer, matching this codebase's convention.
/// </summary>
public static class EquipmentHistoryRecorder
{
    public static void RecordLocationChange(AppDbContext db, int equipmentId, int? fromLocationId, int toLocationId, int changedByUserId, DateTime nowUtc)
    {
        if (fromLocationId == toLocationId)
        {
            return;
        }

        db.EquipmentLocationHistories.Add(new EquipmentLocationHistory
        {
            EquipmentId = equipmentId,
            FromLocationId = fromLocationId,
            ToLocationId = toLocationId,
            ChangedAtUtc = nowUtc,
            ChangedByUserId = changedByUserId,
        });
    }

    public static void RecordStatusChange(AppDbContext db, int equipmentId, int? fromStatusId, int toStatusId, int changedByUserId, DateTime nowUtc)
    {
        if (fromStatusId == toStatusId)
        {
            return;
        }

        db.EquipmentStatusHistories.Add(new EquipmentStatusHistory
        {
            EquipmentId = equipmentId,
            FromStatusId = fromStatusId,
            ToStatusId = toStatusId,
            ChangedAtUtc = nowUtc,
            ChangedByUserId = changedByUserId,
        });
    }
}
