namespace TravelAgencyAPI.Models;

public class TripDto
{
    public int IdTrip { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public int MaxPeople { get; set; }
    public List<CountryDto> Countries { get; set; } = new();
}