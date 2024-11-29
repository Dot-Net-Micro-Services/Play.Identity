using System;
using System.ComponentModel.DataAnnotations;

namespace Play.Identity.Service.Dtos
{
    public record UserDto(Guid Id, string UserName, string Email, decimal Gil, DateTimeOffset createdDate);
    public record UpdateUserDto(
        [Required]
        [EmailAddress]
        string Email,
        [Range(0, 1000000)] 
        decimal Gil
    );

}