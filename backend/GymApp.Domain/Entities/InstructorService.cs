namespace GymApp.Domain.Entities;

public class InstructorService
{
    public Guid InstructorId { get; set; }
    public Guid ClassTypeId { get; set; }

    public Instructor Instructor { get; set; } = null!;
    public ClassType ClassType { get; set; } = null!;
}
