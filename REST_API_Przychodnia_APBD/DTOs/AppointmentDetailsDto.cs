namespace REST_API_Przychodnia_APBD.DTOs;

public class AppointmentDetailsDto
{
    public int IdAppointment { get; set; }
    public DateTime AppointmentDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? InternalNotes { get; set; }
    public DateTime CreatedAt { get; set; }
 
    public PatientInfoDto Patient { get; set; } = new();
    public DoctorInfoDto Doctor { get; set; } = new();
}