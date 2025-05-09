using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using TravelAgencyAPI.Models;

namespace TravelAgencyAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ClientsController : ControllerBase
{
    private readonly SqlConnection _connection;

    public ClientsController(SqlConnection connection)
    {
        _connection = connection;
    }
    
    /// Pobiera wycieczki konkretnego klienta
    [HttpGet("{id}/trips")]
    public async Task<IActionResult> GetClientTrips(int id)
    {
        try
        {
            await _connection.OpenAsync();

            // Sprawdzenie czy klient istnieje
            var checkClientQuery = "SELECT 1 FROM Client WHERE IdClient = @IdClient";
            using (var checkClientCommand = new SqlCommand(checkClientQuery, _connection))
            {
                checkClientCommand.Parameters.AddWithValue("@IdClient", id);
                var clientExists = await checkClientCommand.ExecuteScalarAsync();

                if (clientExists == null)
                {
                    return NotFound($"Client with ID {id} not found");
                }
            }

            // Pobranie wycieczek klienta
            var query = @"
                SELECT t.IdTrip, t.Name, t.Description, t.DateFrom, t.DateTo, t.MaxPeople,
                       ct.RegisteredAt, ct.PaymentDate
                FROM Trip t
                JOIN Client_Trip ct ON t.IdTrip = ct.IdTrip
                WHERE ct.IdClient = @IdClient
                ORDER BY t.DateFrom";

            using (var command = new SqlCommand(query, _connection))
            {
                command.Parameters.AddWithValue("@IdClient", id);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    var trips = new List<ClientTripDto>();

                    while (await reader.ReadAsync())
                    {
                        var trip = new ClientTripDto
                        {
                            IdTrip = reader.GetInt32(reader.GetOrdinal("IdTrip")),
                            Name = reader.GetString(reader.GetOrdinal("Name")),
                            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? 
                                null : reader.GetString(reader.GetOrdinal("Description")),
                            DateFrom = reader.GetDateTime(reader.GetOrdinal("DateFrom")),
                            DateTo = reader.GetDateTime(reader.GetOrdinal("DateTo")),
                            MaxPeople = reader.GetInt32(reader.GetOrdinal("MaxPeople")),
                            RegisteredAt = reader.GetInt32(reader.GetOrdinal("RegisteredAt")),
                            PaymentDate = reader.IsDBNull(reader.GetOrdinal("PaymentDate")) ? 
                                null : reader.GetInt32(reader.GetOrdinal("PaymentDate"))
                        };

                        trips.Add(trip);
                    }

                    return Ok(trips.Count == 0 ? 
                        $"Client with ID {id} has no trips" : 
                        trips);
                }
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
        finally
        {
            await _connection.CloseAsync();
        }
    }
    
    /// Tworzy nowego klienta
    [HttpPost]
    public async Task<IActionResult> CreateClient([FromBody] ClientCreateDto client)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            await _connection.OpenAsync();

            // Sprawdzenie unikalności PESEL i Email
            var checkQuery = @"
                SELECT 1 FROM Client 
                WHERE Pesel = @Pesel OR Email = @Email";
            
            using (var checkCommand = new SqlCommand(checkQuery, _connection))
            {
                checkCommand.Parameters.AddWithValue("@Pesel", client.Pesel ?? (object)DBNull.Value);
                checkCommand.Parameters.AddWithValue("@Email", client.Email);

                if (await checkCommand.ExecuteScalarAsync() != null)
                {
                    return BadRequest("Client with this PESEL or Email already exists");
                }
            }

            // Dodanie nowego klienta
            var insertQuery = @"
                INSERT INTO Client (FirstName, LastName, Email, Telephone, Pesel)
                OUTPUT INSERTED.IdClient
                VALUES (@FirstName, @LastName, @Email, @Telephone, @Pesel)";

            using (var command = new SqlCommand(insertQuery, _connection))
            {
                command.Parameters.AddWithValue("@FirstName", client.FirstName);
                command.Parameters.AddWithValue("@LastName", client.LastName);
                command.Parameters.AddWithValue("@Email", client.Email);
                command.Parameters.AddWithValue("@Telephone", client.Telephone ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@Pesel", client.Pesel ?? (object)DBNull.Value);

                var newId = await command.ExecuteScalarAsync();

                return CreatedAtAction(
                    nameof(GetClientTrips), 
                    new { id = newId }, 
                    new { IdClient = newId });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
        finally
        {
            await _connection.CloseAsync();
        }
    }
    
    /// Rejestruje klienta na wycieczkę
    [HttpPut("{id}/trips/{tripid}")]
    public async Task<IActionResult> RegisterForTrip(int id, int tripid)
    {
        try
        {
            await _connection.OpenAsync();

            // Sprawdzenie czy klient istnieje
            if (!await CheckEntityExists("Client", "IdClient", id))
                return NotFound($"Client with ID {id} not found");

            // Sprawdzenie czy wycieczka istnieje
            if (!await CheckEntityExists("Trip", "IdTrip", tripid))
                return NotFound($"Trip with ID {tripid} not found");

            // Sprawdzenie czy klient już jest zapisany
            if (await CheckRegistrationExists(id, tripid))
                return BadRequest("Client is already registered for this trip");

            // Sprawdzenie dostępności miejsc
            if (!await CheckTripCapacity(tripid))
                return BadRequest("Trip has reached maximum capacity");

            // Rejestracja
            var registerQuery = @"
                INSERT INTO Client_Trip (IdClient, IdTrip, RegisteredAt, PaymentDate)
                VALUES (@IdClient, @IdTrip, @RegisteredAt, NULL)";

            using (var command = new SqlCommand(registerQuery, _connection))
            {
                command.Parameters.AddWithValue("@IdClient", id);
                command.Parameters.AddWithValue("@IdTrip", tripid);
                command.Parameters.AddWithValue("@RegisteredAt", DateTime.Now.ToString("yyyyMMdd"));

                await command.ExecuteNonQueryAsync();
                return Ok("Registration successful");
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
        finally
        {
            await _connection.CloseAsync();
        }
    }

    /// Usuwa rejestrację klienta z wycieczki
    [HttpDelete("{id}/trips/{tripid}")]
    public async Task<IActionResult> DeleteRegistration(int id, int tripid)
    {
        try
        {
            await _connection.OpenAsync();

            if (!await CheckRegistrationExists(id, tripid))
                return NotFound("Registration not found");

            var deleteQuery = @"
                DELETE FROM Client_Trip 
                WHERE IdClient = @IdClient AND IdTrip = @IdTrip";

            using (var command = new SqlCommand(deleteQuery, _connection))
            {
                command.Parameters.AddWithValue("@IdClient", id);
                command.Parameters.AddWithValue("@IdTrip", tripid);

                return await command.ExecuteNonQueryAsync() > 0
                    ? Ok("Registration deleted")
                    : StatusCode(500, "Failed to delete registration");
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
        finally
        {
            await _connection.CloseAsync();
        }
    }

    #region Helper Methods
    private async Task<bool> CheckEntityExists(string table, string idColumn, int id)
    {
        var query = $"SELECT 1 FROM {table} WHERE {idColumn} = @Id";
        using (var cmd = new SqlCommand(query, _connection))
        {
            cmd.Parameters.AddWithValue("@Id", id);
            return await cmd.ExecuteScalarAsync() != null;
        }
    }

    private async Task<bool> CheckRegistrationExists(int clientId, int tripId)
    {
        var query = "SELECT 1 FROM Client_Trip WHERE IdClient = @ClientId AND IdTrip = @TripId";
        using (var cmd = new SqlCommand(query, _connection))
        {
            cmd.Parameters.AddWithValue("@ClientId", clientId);
            cmd.Parameters.AddWithValue("@TripId", tripId);
            return await cmd.ExecuteScalarAsync() != null;
        }
    }

    private async Task<bool> CheckTripCapacity(int tripId)
    {
        var query = @"
            SELECT t.MaxPeople, COUNT(ct.IdClient) AS Current
            FROM Trip t
            LEFT JOIN Client_Trip ct ON t.IdTrip = ct.IdTrip
            WHERE t.IdTrip = @TripId
            GROUP BY t.MaxPeople";

        using (var cmd = new SqlCommand(query, _connection))
        {
            cmd.Parameters.AddWithValue("@TripId", tripId);
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    return reader.GetInt32(0) > reader.GetInt32(1);
                }
                return false;
            }
        }
    }
    #endregion
}

public class ClientTripDto
{
    public int IdTrip { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public int MaxPeople { get; set; }
    public int RegisteredAt { get; set; }
    public int? PaymentDate { get; set; }
}