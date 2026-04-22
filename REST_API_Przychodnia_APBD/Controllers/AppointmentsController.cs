using Microsoft.AspNetCore.Mvc;
using REST_API_Przychodnia_APBD.Services;

namespace REST_API_Przychodnia_APBD.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AppointmentsController : ControllerBase
    {
        private readonly IAppointmentsService _appointmentsService;

        public AppointmentsController(IAppointmentsService appointmentsService)
        {
            _appointmentsService = appointmentsService;
        }
        
        [HttpGet]
        public async Task<IActionResult> GetAllAppointmentsList()
        {
            var appointments = await _appointmentsService.GetAllAppointmentsAsync();
            return Ok(appointments);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetAppointmentDetailsById(string id)
        {
            var details = await _appointmentsService.GetAppointmentByIdAsync(id);
            return Ok(details);
        }
    }
}
