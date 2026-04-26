using APBD_TASK6.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Net.NetworkInformation;
using System.Transactions;

namespace APBD_TASK6.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AppointmentsController : ControllerBase
    {
        private readonly string _connectionString;

        public AppointmentsController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "Missing 'DefaultConnection' in appsettings.json.");
        }

        [HttpGet]
        public async Task<IActionResult> GetAppointments(
            [FromQuery] string? status,
            [FromQuery] string? patientLastName)
        {
            const string sql = """
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
                """;

            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand(sql, connection);

            command.Parameters.AddWithValue("@Status", (object?)status ?? DBNull.Value);
            command.Parameters.AddWithValue("@PatientLastName", (object?)patientLastName ?? DBNull.Value);

            await connection.OpenAsync();

            var results = new List<AppointmentListDto>();

            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                results.Add(new AppointmentListDto
                {
                    IdAppointment = reader.GetInt32(0),
                    AppointmentDate = reader.GetDateTime(1),
                    Status = reader.GetString(2),
                    Reason = reader.GetString(3),
                    PatientFullName = reader.GetString(4),
                    PatientEmail = reader.GetString(5)
                });
            }

            return Ok(results);
        }

        [HttpGet("{Id}", Name = nameof(GetAppointments))]
        public async Task<IActionResult> GetAppointments(int Id)
        {
            const string sql = """
                SELECT
                    a.IdAppointment,
                    a.AppointmentDate,
                    p.FirstName AS PatientFirstName,
                	p.LastName AS PatientLastName,
                    p.Email AS PatientEmail,
                	p.PhoneNumber AS PatientPhoneNumber,
                	d.IdDoctor,
                	d.FirstName AS DoctorFirstName,
                	d.LastName AS DoctorLastName,
                	d.LicenseNumber,
                	a.CreatedAt,
                	a.Status,
                    a.Reason,
                	a.InternalNotes
                FROM dbo.Appointments a
                JOIN dbo.Patients p ON p.IdPatient = a.IdPatient
                JOIN dbo.Doctors d ON d.IdDoctor = a.IdDoctor
                WHERE (a.IdAppointment = @Id)
                ORDER BY a.AppointmentDate;
                """;

            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand(sql, connection);

            command.Parameters.AddWithValue("@Id", (object?)Id ?? DBNull.Value);

            await connection.OpenAsync();

            var result = new AppointmentDetailsDto();

            await using var reader = await command.ExecuteReaderAsync();

           if (await reader.ReadAsync())
            {
                result.IdAppointment = reader.GetInt32(0);
                result.AppointmentDate = reader.GetDateTime(1);
                result.PatientFirstName = reader.GetString(2);
                result.PatientLastName = reader.GetString(3);
                result.PatientEmail = reader.GetString(4);
                result.PatientPhoneNumber = reader.GetString(5);
                result.IdDoctor = reader.GetInt32(6);
                result.DoctorFirstName = reader.GetString(7);
                result.DoctorLastName = reader.GetString(8);
                result.LicenseNumber = reader.GetString(9);
                result.CreatedAt = reader.GetDateTime(10);
                result.Status = reader.GetString(11);
                result.Reason = reader.GetString(12);
                result.InternalNotes = reader.IsDBNull(13) ? null : reader.GetString(13);

                return Ok(result);
            }
           return NotFound(new ErrorResponseDto($"ID: {Id} not Found"));
        }

        [HttpPost]
        public async Task<ActionResult> CreateAppointments(
            [FromBody] CreateAppointmentRequestDto request)
        {
            const string sqlPatient = """
                SELECT p.IsActive
                FROM dbo.Patients p
                WHERE (p.IdPatient = @Id)
                """;

            const string sqlDoctor = """
                SELECT d.IsActive
                FROM dbo.Doctors d
                WHERE (d.IdDoctor = @Id)
                """;

            const string sqlAppointment = """
                SELECT 1
                FROM dbo.Appointments a
                WHERE a.IdDoctor = @Id
                AND DATEPART(YEAR, a.AppointmentDate) = DATEPART(YEAR, @AppointmentDate)
                AND DATEPART(MONTH, a.AppointmentDate) = DATEPART(MONTH, @AppointmentDate)
                AND DATEPART(DAY, a.AppointmentDate) = DATEPART(DAY, @AppointmentDate)
                AND DATEPART(HOUR, a.AppointmentDate) = DATEPART(HOUR, @AppointmentDate)
                AND DATEPART(MINUTE, a.AppointmentDate) = DATEPART(MINUTE, @AppointmentDate)
                """;

            if (request.AppointmentDate < DateTime.UtcNow)
            {
                return BadRequest(new ErrorResponseDto("Appointment date cannot be in the past"));
            }

            if (request.Reason.Length == 0 || request.Reason.Length > 250)
            {
                return BadRequest(new ErrorResponseDto("Reason cannot be empty and cannot be longer than 250 characters"));
            }

            await using var PatientConnection = new SqlConnection(_connectionString);
            await PatientConnection.OpenAsync();

            await using var PatientCommand = new SqlCommand(sqlPatient, PatientConnection);
            PatientCommand.Parameters.AddWithValue("@Id", (object?)request.IdPatient);

            await using var PatientReader = await PatientCommand.ExecuteReaderAsync();

            if (await PatientReader.ReadAsync())
            {
                if (!PatientReader.GetBoolean(0))
                {
                    return NotFound(new ErrorResponseDto("Patient must be active"));
                }
            }
            else
            {
                return NotFound(new ErrorResponseDto($"Patient with ID: {request.IdPatient} not found"));
            }

            PatientReader.Close();
            PatientConnection.Close();

            await using var DoctorConnection = new SqlConnection(_connectionString);
            await DoctorConnection.OpenAsync();

            await using var DoctorCommand = new SqlCommand(sqlDoctor, DoctorConnection);
            DoctorCommand.Parameters.AddWithValue("@Id", (object?)request.IdDoctor);

            await using var DoctorReader = await DoctorCommand.ExecuteReaderAsync();

            if (await DoctorReader.ReadAsync())
            {
                if (!DoctorReader.GetBoolean(0))
                {
                    return NotFound(new ErrorResponseDto("Doctor must be active"));
                }
            }
            else
            {
                return NotFound(new ErrorResponseDto($"Doctor with ID: {request.IdDoctor} not found"));
            }

            DoctorReader.Close();
            DoctorConnection.Close();

            await using var AppointmentConnection = new SqlConnection(_connectionString);
            await AppointmentConnection.OpenAsync();

            await using var AppointmentCommand = new SqlCommand(sqlAppointment, AppointmentConnection);
            AppointmentCommand.Parameters.AddWithValue("@Id", (object?)request.IdDoctor);
            AppointmentCommand.Parameters.AddWithValue("@AppointmentDate", (object?)request.AppointmentDate ?? DBNull.Value);

            await using var AppointmentReader = await AppointmentCommand.ExecuteReaderAsync();

            if (await AppointmentReader.ReadAsync())
            {
                return Conflict(new ErrorResponseDto($"Appointment already exists at this time"));
            }

            AppointmentReader.Close();
            AppointmentConnection.Close();

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            int newId;

            await using (var transaction = (SqlTransaction)await connection.BeginTransactionAsync())
            {

                const string insertSql = """
                       INSERT INTO dbo.Appointments (IdPatient, IdDoctor, AppointmentDate, Status, Reason)
                    OUTPUT INSERTED.IdAppointment
                    VALUES (@IdPatient, @IdDoctor, @AppointmentDate, 'Scheduled', @Reason)
                    """;

                await using var command = new SqlCommand(insertSql, connection, transaction);
                command.Parameters.AddWithValue("@IdPatient", request.IdPatient);
                command.Parameters.AddWithValue("@IdDoctor", request.IdDoctor);
                command.Parameters.AddWithValue("@AppointmentDate", request.AppointmentDate);
                command.Parameters.AddWithValue("@Reason", request.Reason);

                newId = (int)(await command.ExecuteScalarAsync())!;
                await transaction.CommitAsync();
            }
            return CreatedAtRoute(nameof(GetAppointments), new { Id = newId }, null);

        }

    }
}
