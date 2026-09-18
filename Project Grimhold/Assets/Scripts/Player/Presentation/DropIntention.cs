public readonly struct DropIntention
{
    public readonly DragPayload Payload;
    public readonly DragSlotLocation Target;
    public readonly EquipmentSlot TargetEquipmentSlot;

    public bool IsValid => Payload.IsValid && Target != DragSlotLocation.None;

    public DropIntention(DragPayload payload, DragSlotLocation target, EquipmentSlot targetEquipmentSlot)
    {
        Payload = payload;
        Target = target;
        TargetEquipmentSlot = targetEquipmentSlot;
    }
}
