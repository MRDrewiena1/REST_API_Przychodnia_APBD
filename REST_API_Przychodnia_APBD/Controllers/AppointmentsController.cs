using Microsoft.AspNetCore.Mvc;
using REST_API_Przychodnia_APBD.DTOs;
using REST_API_Przychodnia_APBD.Services;

namespace REST_API_Przychodnia_APBD.Controllers;

[ApiController]
[Route("api/appointments")]
public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentsService _service;

    public AppointmentsController(IAppointmentsService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        [FromQuery] string? patientLastName)
    {
        var results = await _service.GetAllAppointmentsAsync(status, patientLastName);
        return Ok(results);
    }

    [HttpGet("{idAppointment:int}")]
    public async Task<IActionResult> GetById(int idAppointment)
    {
        var dto = await _service.GetAppointmentByIdAsync(idAppointment);
        if (dto is null)
            return NotFound(new ErrorResponseDto($"Appointment with id {idAppointment} not found."));

        return Ok(dto);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAppointmentRequestDto dto)
    {
        var (newId, error, statusCode) = await _service.CreateAppointmentAsync(dto);

        if (error is not null)
        {
            return statusCode switch
            {
                409 => Conflict(new ErrorResponseDto(error)),
                _   => BadRequest(new ErrorResponseDto(error))
            };
        }

        return CreatedAtAction(nameof(GetById), new { idAppointment = newId }, new { idAppointment = newId });
    }

    [HttpPut("{idAppointment:int}")]
    public async Task<IActionResult> Update(int idAppointment, [FromBody] UpdateAppointmentRequestDto dto)
    {
        var (success, error, statusCode) = await _service.UpdateAppointmentAsync(idAppointment, dto);

        if (!success)
        {
            return statusCode switch
            {
                404 => NotFound(new ErrorResponseDto(error!)),
                409 => Conflict(new ErrorResponseDto(error!)),
                _   => BadRequest(new ErrorResponseDto(error!))
            };
        }

        return Ok();
    }

    [HttpDelete("{idAppointment:int}")]
    public async Task<IActionResult> Delete(int idAppointment)
    {
        var (success, error, statusCode) = await _service.DeleteAppointmentAsync(idAppointment);

        if (!success)
        {
            return statusCode switch
            {
                404 => NotFound(new ErrorResponseDto(error!)),
                409 => Conflict(new ErrorResponseDto(error!)),
                _   => BadRequest(new ErrorResponseDto(error!))
            };
        }

        return NoContent();
    }
}