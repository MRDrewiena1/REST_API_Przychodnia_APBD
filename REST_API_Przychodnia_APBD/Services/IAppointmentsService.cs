using REST_API_Przychodnia_APBD.DTOs;

namespace REST_API_Przychodnia_APBD.Services;

public interface IAppointmentsService
{
    Task<IEnumerable<AppointmentListDto>> GetAllAppointmentsAsync(string? status, string? patientLastName);
    Task<AppointmentDetailsDto?> GetAppointmentByIdAsync(int idAppointment);
    Task<(int newId, string? error, int statusCode)> CreateAppointmentAsync(CreateAppointmentRequestDto dto);
    Task<(bool success, string? error, int statusCode)> UpdateAppointmentAsync(int idAppointment, UpdateAppointmentRequestDto dto);
    Task<(bool success, string? error, int statusCode)> DeleteAppointmentAsync(int idAppointment);
}