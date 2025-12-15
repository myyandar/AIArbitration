using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Models
{
    public class CreateUserCommand
    {
        public required string Email { get; set; }
        public required string UserName { get; set; }
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public required string Password { get; set; }
        public required string TenantId { get; set; }
        public string? RoleId { get; set; }
        public string? ProjectId { get; set; }
        public bool SendWelcomeEmail { get; set; } = true;
    }
}
