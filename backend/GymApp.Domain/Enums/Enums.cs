namespace GymApp.Domain.Enums;

public enum UserRole { SuperAdmin, Admin, Student, Affiliate }
public enum ModalityType { Group, Individual, Pair }
public enum DayOfWeekEnum { Sunday, Monday, Tuesday, Wednesday, Thursday, Friday, Saturday }
public enum SessionStatus { Scheduled, Cancelled }
public enum BookingStatus { Confirmed, CheckedIn, Cancelled }
public enum TenantPlan { Basic, Pro, Enterprise }
public enum StudentStatus { Active, Inactive, Suspended }
public enum PaymentStatus { Pending, Paid, Expired, Cancelled }
public enum SubscriptionStatus { Trial, Active, PastDue, Canceled, Suspended }
public enum TenantType { Gym, BeautySalon }
public enum PaymentMethod { Cash, Pix, DebitCard, CreditCard }
public enum AffiliateCommissionStatus { Pending, Paid }
public enum AffiliateWithdrawalStatus { Pending, Approved, Rejected }
