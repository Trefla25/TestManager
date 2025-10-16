using System.Text.Json.Serialization;

namespace eHub.Authentication.Models;
#pragma warning disable CS8618, CS1591 // Disable nullability warning for ORM-Wrapped config stuff; Ignore XML doc on ORM object
public class User
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Username { get; set; }

    [JsonIgnore]
    public string Password { get; set; }
}
#pragma warning restore CS8618, CS1591