using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using TravelAgencyAPI.Models;

namespace TravelAgencyAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class TripsController : ControllerBase
{
    private readonly SqlConnection _connection;

    public TripsController(SqlConnection connection)
    {
        _connection = connection;
    }
    
    /// Pobiera wszystkie wycieczki wraz z przypisanymi krajami
    [HttpGet]
    public async Task<IActionResult> GetTrips()
    {
        try
        {
            await _connection.OpenAsync();

            var query = @"
                SELECT t.IdTrip, t.Name, t.Description, t.DateFrom, t.DateTo, t.MaxPeople,
                       c.IdCountry, c.Name AS CountryName
                FROM Trip t
                LEFT JOIN Country_Trip ct ON t.IdTrip = ct.IdTrip
                LEFT JOIN Country c ON ct.IdCountry = c.IdCountry
                ORDER BY t.DateFrom";

            using (var command = new SqlCommand(query, _connection))
            {
                using (var reader = await command.ExecuteReaderAsync())
                {
                    var trips = new Dictionary<int, TripDto>();

                    while (await reader.ReadAsync())
                    {
                        var tripId = reader.GetInt32(reader.GetOrdinal("IdTrip"));

                        if (!trips.ContainsKey(tripId))
                        {
                            trips[tripId] = new TripDto
                            {
                                IdTrip = tripId,
                                Name = reader.GetString(reader.GetOrdinal("Name")),
                                Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? 
                                    null : reader.GetString(reader.GetOrdinal("Description")),
                                DateFrom = reader.GetDateTime(reader.GetOrdinal("DateFrom")),
                                DateTo = reader.GetDateTime(reader.GetOrdinal("DateTo")),
                                MaxPeople = reader.GetInt32(reader.GetOrdinal("MaxPeople")),
                                Countries = new List<CountryDto>()
                            };
                        }

                        if (!reader.IsDBNull(reader.GetOrdinal("IdCountry")))
                        {
                            trips[tripId].Countries.Add(new CountryDto
                            {
                                IdCountry = reader.GetInt32(reader.GetOrdinal("IdCountry")),
                                Name = reader.GetString(reader.GetOrdinal("CountryName"))
                            });
                        }
                    }

                    return Ok(trips.Values.ToList());
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
}