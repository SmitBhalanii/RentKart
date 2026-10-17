namespace RentKart.Core.Enums;

public enum UserRole
{
    Customer = 1,
    Business = 2,
    Admin = 3
}

public enum BusinessStatus
{
    PendingApproval = 0,
    Approved = 1,
    Suspended = 2,
    Rejected = 3
}

public enum EquipmentStatus
{
    Active = 0,
    Inactive = 1,
    Decommissioned = 2
}

public enum EquipmentCondition
{
    New = 0,
    LikeNew = 1,
    Excellent = 2,
    Good = 3,
    Fair = 4
}

public enum BookingStatus
{
    Pending = 0,
    PartiallyConfirmed = 1,
    Confirmed = 2,
    Active = 3,
    Completed = 4,
    Cancelled = 5
}

public enum BookingGroupStatus
{
    PendingApproval = 0,
    Confirmed = 1,
    HandedOver = 2,
    Completed = 3,
    Rejected = 4,
    CancelledByCustomer = 5
}
