using System.ComponentModel.DataAnnotations;

namespace TravelAgencyAPI.Models;

public class ClientCreateDto
{
    [Required][StringLength(120)] public required string FirstName { get; set; }
    [Required][StringLength(120)] public required string LastName { get; set; }
    [Required][EmailAddress][StringLength(120)] public required string Email { get; set; }
    [StringLength(120)] public string? Telephone { get; set; }
    [StringLength(120)] public string? Pesel { get; set; }
}