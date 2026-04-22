using REST_API_Przychodnia_APBD.DTOs;

namespace REST_API_Przychodnia_APBD.Services;

public interface IAppointmentsService
{
    Task<IEnumerable<AppointmentListDto>> GetAllAppointmentsAsync();
}