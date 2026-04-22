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
        public async Task<IActionResult> Get()
        {
            var appointments = await _appointmentsService.GetAllAppointmentsAsync();
            return Ok(appointments);
        }
    }
}
