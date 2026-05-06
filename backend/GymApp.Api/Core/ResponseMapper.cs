using GymApp.Api.DTOs;
using GymApp.Domain.Entities;

namespace GymApp.Api.Core;

public static class ResponseMapper
{
    public static ScheduleResponse ToScheduleResponse(Schedule s) => new(
        s.Id, s.ClassTypeId, s.ClassType.Name, s.ClassType.Color,
        s.InstructorId, s.Instructor?.User.Name,
        s.LocationId,
        s.Weekday, s.StartTime, s.DurationMinutes, s.Capacity, s.IsActive
    );

    public static PackageResponse ToPackageResponse(Package p) => new(
        p.Id, p.Name, p.ExpiresAt, p.IsActive, p.CreatedAt,
        p.Items.Select(i => new PackageItemResponse(
            i.Id, i.ClassTypeId, i.ClassType.Name, i.ClassType.Color,
            i.TotalCredits, i.UsedCredits, i.TotalCredits - i.UsedCredits, i.PricePerCredit
        )).ToList()
    );
}

public static class EntityValidator
{
    public static bool IsValidEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email) && email.Contains('@') && email.Contains('.');

    public static bool IsValidPhone(string? phone) =>
        string.IsNullOrWhiteSpace(phone) || phone.Length >= 10;

    public static bool IsValidName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && name.Length >= 2;

    public static bool IsValidPassword(string? password) =>
        !string.IsNullOrWhiteSpace(password) && password.Length >= 6;

    public static bool IsValidSlug(string? slug) =>
        !string.IsNullOrWhiteSpace(slug) && slug.Length >= 2 && slug.All(c => char.IsLetterOrDigit(c) || c == '-');

    public static bool IsValidUrl(string? url) =>
        string.IsNullOrWhiteSpace(url) || Uri.TryCreate(url, UriKind.Absolute, out _);
}