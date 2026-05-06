using GymApp.Api.DTOs;
using GymApp.Domain.Enums;
using Xunit;

namespace GymApp.Tests.DTOs;

public class StudentDtoTests
{
    [Fact]
    public void CreateStudentRequest_CanBeConstructed()
    {
        var req = new CreateStudentRequest("John", "john@example.com", "secret123", "123456789", DateOnly.FromDateTime(DateTime.Today.AddYears(-20)), "No health issues");
        Assert.Equal("John", req.Name);
        Assert.Equal("john@example.com", req.Email);
        Assert.Equal("secret123", req.Password);
        Assert.Equal("123456789", req.Phone);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today.AddYears(-20)), req.BirthDate);
        Assert.Equal("No health issues", req.HealthNotes);
    }

    [Fact]
    public void CreateStudentRequest_WithOptionalFieldsNull()
    {
        var req = new CreateStudentRequest("John", "john@example.com", null, null, null, null);
        Assert.Null(req.Password);
        Assert.Null(req.Phone);
        Assert.Null(req.BirthDate);
        Assert.Null(req.HealthNotes);
    }

    [Fact]
    public void UpdateStudentRequest_CanBeConstructed()
    {
        var req = new UpdateStudentRequest("John Updated", "999999999", DateOnly.FromDateTime(DateTime.Today.AddYears(-25)), "Updated notes", StudentStatus.Inactive);
        Assert.Equal("John Updated", req.Name);
        Assert.Equal("999999999", req.Phone);
        Assert.Equal(StudentStatus.Inactive, req.Status);
    }

    [Fact]
    public void UpdateStudentNotesRequest_CanBeConstructed()
    {
        var req = new UpdateStudentNotesRequest("Some health notes");
        Assert.Equal("Some health notes", req.Notes);
    }

    [Fact]
    public void UpdateStudentNotesRequest_WithNull()
    {
        var req = new UpdateStudentNotesRequest(null);
        Assert.Null(req.Notes);
    }

    [Fact]
    public void StudentResponse_CanBeConstructed()
    {
        var id = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;
        var req = new StudentResponse(id, "John", "john@example.com", "123456789", DateOnly.FromDateTime(DateTime.Today.AddYears(-20)), StudentStatus.Active, null, createdAt, 10, DateOnly.FromDateTime(DateTime.Today.AddDays(-5)), "No issues");
        Assert.Equal(id, req.Id);
        Assert.Equal("John", req.Name);
        Assert.Equal(10, req.TotalRemainingCredits);
        Assert.Equal(StudentStatus.Active, req.Status);
    }
}

public class BookingDtoTests
{
    [Fact]
    public void CreateBookingRequest_CanBeConstructed()
    {
        var sessionId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var req = new CreateBookingRequest(sessionId, studentId, null);
        Assert.Equal(sessionId, req.SessionId);
        Assert.Equal(studentId, req.StudentId);
        Assert.Null(req.PackageItemId);
    }

    [Fact]
    public void CreateBookingRequest_WithPackageItemId()
    {
        var sessionId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var packageItemId = Guid.NewGuid();
        var req = new CreateBookingRequest(sessionId, studentId, packageItemId);
        Assert.Equal(packageItemId, req.PackageItemId);
    }

    [Fact]
    public void CreateSalonBookingRequest_CanBeConstructed()
    {
        var date = DateOnly.FromDateTime(DateTime.Today);
        var time = new TimeOnly(14, 30);
        var serviceId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var req = new CreateSalonBookingRequest(date, time, serviceId, studentId, null);
        Assert.Equal(date, req.Date);
        Assert.Equal(time, req.StartTime);
        Assert.Equal(serviceId, req.ServiceId);
        Assert.Equal(studentId, req.StudentId);
        Assert.Null(req.ProfessionalId);
    }

    [Fact]
    public void CancelBookingRequest_CanBeConstructed()
    {
        var req = new CancelBookingRequest("Schedule conflict");
        Assert.Equal("Schedule conflict", req.Reason);
    }

    [Fact]
    public void CancelBookingRequest_WithNullReason()
    {
        var req = new CancelBookingRequest(null);
        Assert.Null(req.Reason);
    }

    [Fact]
    public void BookingResponse_CanBeConstructed()
    {
        var id = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.Today);
        var time = new TimeOnly(10, 0);
        var studentId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;
        var req = new BookingResponse(id, sessionId, date, time, "Boxing", studentId, "John", BookingStatus.Confirmed, null, createdAt, locationId);
        Assert.Equal(id, req.Id);
        Assert.Equal(BookingStatus.Confirmed, req.Status);
        Assert.Equal("Boxing", req.ClassTypeName);
    }
}