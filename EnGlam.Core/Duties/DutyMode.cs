namespace EnGlam.Core.Duties;

/// <summary>Modi wie in AutoDuty (Enums.DutyMode); die Namen gehen per SetConfig an AutoDuty.</summary>
[Flags]
public enum DutyMode
{
    None = 0,
    Support = 1,
    Trust = 2,
    Squadron = 4,
    Regular = 8,
    Trial = 0x10,
    Raid = 0x20,
    Variant = 0x40,
    NoviceHall = 0x80,
    Crucible = 0x100,
}
