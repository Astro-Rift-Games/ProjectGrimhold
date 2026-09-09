/// <summary>Read-only access to one confirmed eight-slot prepared Equipment projection.</summary>
public interface IPreparedEquipmentReadSource
{
    bool TryGetPreparedEquipment(out PreparedEquipmentLoadout equipment);
}
