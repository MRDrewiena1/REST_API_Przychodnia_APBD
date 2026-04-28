using System.Data;
using Microsoft.Data.SqlClient;
using REST_API_Przychodnia_APBD.DTOs;

namespace REST_API_Przychodnia_APBD.Services;

public class AppointmentsService : IAppointmentsService
{
    private readonly string _connectionString;

    private static readonly HashSet<string> ValidStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "Scheduled", "Completed", "Cancelled" };

    public AppointmentsService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }
    
    public async Task<IEnumerable<AppointmentListDto>> GetAllAppointmentsAsync(
        string? status, string? patientLastName)
    {
        var results = new List<AppointmentListDto>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand("""
            SELECT
                a.IdAppointment,
                a.AppointmentDate,
                a.Status,
                a.Reason,
                p.FirstName + N' ' + p.LastName AS PatientFullName,
                p.Email AS PatientEmail
            FROM dbo.Appointments a
            JOIN dbo.Patients p ON p.IdPatient = a.IdPatient
            WHERE (@Status IS NULL OR a.Status = @Status)
              AND (@PatientLastName IS NULL OR p.LastName = @PatientLastName)
            ORDER BY a.AppointmentDate;
            """, connection);

        command.Parameters.Add("@Status", SqlDbType.NVarChar, 30).Value =
            (object?)status ?? DBNull.Value;
        command.Parameters.Add("@PatientLastName", SqlDbType.NVarChar, 80).Value =
            (object?)patientLastName ?? DBNull.Value;

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new AppointmentListDto
            {
                IdAppointment   = reader.GetInt32(reader.GetOrdinal("IdAppointment")),
                AppointmentDate = reader.GetDateTime(reader.GetOrdinal("AppointmentDate")),
                Status          = reader.GetString(reader.GetOrdinal("Status")),
                Reason          = reader.GetString(reader.GetOrdinal("Reason")),
                PatientFullName = reader.GetString(reader.GetOrdinal("PatientFullName")),
                PatientEmail    = reader.GetString(reader.GetOrdinal("PatientEmail")),
            });
        }

        return results;
    }
    
    public async Task<AppointmentDetailsDto?> GetAppointmentByIdAsync(int idAppointment)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand("""
            SELECT
                a.IdAppointment,
                a.AppointmentDate,
                a.Status,
                a.Reason,
                a.InternalNotes,
                a.CreatedAt,
                p.IdPatient,
                p.FirstName  AS PatFirstName,
                p.LastName   AS PatLastName,
                p.Email      AS PatEmail,
                p.PhoneNumber,
                p.DateOfBirth,
                d.IdDoctor,
                d.FirstName  AS DocFirstName,
                d.LastName   AS DocLastName,
                d.LicenseNumber,
                s.Name       AS Specialization
            FROM dbo.Appointments a
            JOIN dbo.Patients        p ON p.IdPatient        = a.IdPatient
            JOIN dbo.Doctors         d ON d.IdDoctor         = a.IdDoctor
            JOIN dbo.Specializations s ON s.IdSpecialization = d.IdSpecialization
            WHERE a.IdAppointment = @IdAppointment;
            """, connection);

        command.Parameters.Add("@IdAppointment", SqlDbType.Int).Value = idAppointment;

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        return new AppointmentDetailsDto
        {
            IdAppointment   = reader.GetInt32(reader.GetOrdinal("IdAppointment")),
            AppointmentDate = reader.GetDateTime(reader.GetOrdinal("AppointmentDate")),
            Status          = reader.GetString(reader.GetOrdinal("Status")),
            Reason          = reader.GetString(reader.GetOrdinal("Reason")),
            InternalNotes   = reader.IsDBNull(reader.GetOrdinal("InternalNotes"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("InternalNotes")),
            CreatedAt       = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            Patient = new PatientInfoDto
            {
                IdPatient   = reader.GetInt32(reader.GetOrdinal("IdPatient")),
                FirstName   = reader.GetString(reader.GetOrdinal("PatFirstName")),
                LastName    = reader.GetString(reader.GetOrdinal("PatLastName")),
                Email       = reader.GetString(reader.GetOrdinal("PatEmail")),
                PhoneNumber = reader.IsDBNull(reader.GetOrdinal("PhoneNumber"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("PhoneNumber")),
                DateOfBirth = reader.GetDateTime(reader.GetOrdinal("DateOfBirth")),
            },
            Doctor = new DoctorInfoDto
            {
                IdDoctor       = reader.GetInt32(reader.GetOrdinal("IdDoctor")),
                FirstName      = reader.GetString(reader.GetOrdinal("DocFirstName")),
                LastName       = reader.GetString(reader.GetOrdinal("DocLastName")),
                LicenseNumber  = reader.GetString(reader.GetOrdinal("LicenseNumber")),
                Specialization = reader.GetString(reader.GetOrdinal("Specialization")),
            }
        };
    }
    
    public async Task<(int newId, string? error, int statusCode)> CreateAppointmentAsync(
        CreateAppointmentRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason))
            return (0, "Reason cannot be empty.", 400);

        if (dto.Reason.Length > 250)
            return (0, "Reason must not exceed 250 characters.", 400);

        if (dto.AppointmentDate <= DateTime.UtcNow)
            return (0, "Appointment date must be in the future.", 400);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        if (!await EntityExistsAndActiveAsync(connection, "Patients", "IdPatient", dto.IdPatient))
            return (0, $"Patient with id {dto.IdPatient} does not exist or is not active.", 400);

        if (!await EntityExistsAndActiveAsync(connection, "Doctors", "IdDoctor", dto.IdDoctor))
            return (0, $"Doctor with id {dto.IdDoctor} does not exist or is not active.", 400);

        if (await DoctorHasConflictAsync(connection, dto.IdDoctor, dto.AppointmentDate, excludeId: null))
            return (0, "The doctor already has a scheduled appointment at that exact time.", 409);

        await using var cmd = new SqlCommand("""
            INSERT INTO dbo.Appointments (IdPatient, IdDoctor, AppointmentDate, Status, Reason)
            OUTPUT INSERTED.IdAppointment
            VALUES (@IdPatient, @IdDoctor, @AppointmentDate, N'Scheduled', @Reason);
            """, connection);

        cmd.Parameters.Add("@IdPatient",       SqlDbType.Int).Value           = dto.IdPatient;
        cmd.Parameters.Add("@IdDoctor",        SqlDbType.Int).Value           = dto.IdDoctor;
        cmd.Parameters.Add("@AppointmentDate", SqlDbType.DateTime2).Value     = dto.AppointmentDate;
        cmd.Parameters.Add("@Reason",          SqlDbType.NVarChar, 250).Value = dto.Reason;

        var newId = (int)(await cmd.ExecuteScalarAsync())!;
        return (newId, null, 201);
    }
    
    public async Task<(bool success, string? error, int statusCode)> UpdateAppointmentAsync(
        int idAppointment, UpdateAppointmentRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason))
            return (false, "Reason cannot be empty.", 400);

        if (dto.Reason.Length > 250)
            return (false, "Reason must not exceed 250 characters.", 400);

        if (!ValidStatuses.Contains(dto.Status))
            return (false, $"Status must be one of: {string.Join(", ", ValidStatuses)}.", 400);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var (exists, currentStatus, currentDate) =
            await GetAppointmentStatusAsync(connection, idAppointment);

        if (!exists)
            return (false, $"Appointment with id {idAppointment} not found.", 404);

        if (!await EntityExistsAndActiveAsync(connection, "Patients", "IdPatient", dto.IdPatient))
            return (false, $"Patient with id {dto.IdPatient} does not exist or is not active.", 400);

        if (!await EntityExistsAndActiveAsync(connection, "Doctors", "IdDoctor", dto.IdDoctor))
            return (false, $"Doctor with id {dto.IdDoctor} does not exist or is not active.", 400);

        if (string.Equals(currentStatus, "Completed", StringComparison.OrdinalIgnoreCase)
            && dto.AppointmentDate != currentDate)
            return (false, "Cannot change the date of a completed appointment.", 409);

        if (await DoctorHasConflictAsync(connection, dto.IdDoctor, dto.AppointmentDate, excludeId: idAppointment))
            return (false, "The doctor already has a scheduled appointment at that exact time.", 409);

        await using var cmd = new SqlCommand("""
            UPDATE dbo.Appointments
            SET IdPatient       = @IdPatient,
                IdDoctor        = @IdDoctor,
                AppointmentDate = @AppointmentDate,
                Status          = @Status,
                Reason          = @Reason,
                InternalNotes   = @InternalNotes
            WHERE IdAppointment = @IdAppointment;
            """, connection);

        cmd.Parameters.Add("@IdPatient",       SqlDbType.Int).Value           = dto.IdPatient;
        cmd.Parameters.Add("@IdDoctor",        SqlDbType.Int).Value           = dto.IdDoctor;
        cmd.Parameters.Add("@AppointmentDate", SqlDbType.DateTime2).Value     = dto.AppointmentDate;
        cmd.Parameters.Add("@Status",          SqlDbType.NVarChar, 30).Value  = dto.Status;
        cmd.Parameters.Add("@Reason",          SqlDbType.NVarChar, 250).Value = dto.Reason;
        cmd.Parameters.Add("@InternalNotes",   SqlDbType.NVarChar, 500).Value =
            (object?)dto.InternalNotes ?? DBNull.Value;
        cmd.Parameters.Add("@IdAppointment",   SqlDbType.Int).Value           = idAppointment;

        await cmd.ExecuteNonQueryAsync();
        return (true, null, 200);
    }
    
    public async Task<(bool success, string? error, int statusCode)> DeleteAppointmentAsync(int idAppointment)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var (exists, currentStatus, _) = await GetAppointmentStatusAsync(connection, idAppointment);

        if (!exists)
            return (false, $"Appointment with id {idAppointment} not found.", 404);

        if (string.Equals(currentStatus, "Completed", StringComparison.OrdinalIgnoreCase))
            return (false, "Cannot delete a completed appointment.", 409);

        await using var cmd = new SqlCommand(
            "DELETE FROM dbo.Appointments WHERE IdAppointment = @IdAppointment;", connection);
        cmd.Parameters.Add("@IdAppointment", SqlDbType.Int).Value = idAppointment;
        await cmd.ExecuteNonQueryAsync();

        return (true, null, 204);
    }
    
    private static async Task<bool> EntityExistsAndActiveAsync(
        SqlConnection connection, string table, string pkColumn, int id)
    {
        await using var cmd = new SqlCommand($"""
            SELECT COUNT(1) FROM dbo.{table}
            WHERE {pkColumn} = @Id AND IsActive = 1;
            """, connection);
        cmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;
        return (int)(await cmd.ExecuteScalarAsync())! > 0;
    }

    private static async Task<bool> DoctorHasConflictAsync(
        SqlConnection connection, int idDoctor, DateTime date, int? excludeId)
    {
        await using var cmd = new SqlCommand("""
            SELECT COUNT(1) FROM dbo.Appointments
            WHERE IdDoctor        = @IdDoctor
              AND AppointmentDate = @AppointmentDate
              AND Status          = N'Scheduled'
              AND (@ExcludeId IS NULL OR IdAppointment <> @ExcludeId);
            """, connection);

        cmd.Parameters.Add("@IdDoctor",        SqlDbType.Int).Value       = idDoctor;
        cmd.Parameters.Add("@AppointmentDate", SqlDbType.DateTime2).Value = date;
        cmd.Parameters.Add("@ExcludeId",       SqlDbType.Int).Value       =
            excludeId.HasValue ? excludeId.Value : DBNull.Value;

        return (int)(await cmd.ExecuteScalarAsync())! > 0;
    }

    private static async Task<(bool exists, string status, DateTime date)>
        GetAppointmentStatusAsync(SqlConnection connection, int idAppointment)
    {
        await using var cmd = new SqlCommand(
            "SELECT Status, AppointmentDate FROM dbo.Appointments WHERE IdAppointment = @IdAppointment;",
            connection);
        cmd.Parameters.Add("@IdAppointment", SqlDbType.Int).Value = idAppointment;

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return (false, string.Empty, default);

        return (true,
            reader.GetString(reader.GetOrdinal("Status")),
            reader.GetDateTime(reader.GetOrdinal("AppointmentDate")));
    }
}