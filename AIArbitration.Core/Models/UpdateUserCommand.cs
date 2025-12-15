namespace AIArbitration.Core.Models
{
    public class UpdateUserCommand
    {
        public required string UserId { get; set; }
        public string? Email { get; set; }
        public string? UserName { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Password { get; set; }
        public string? RoleId { get; set; }
        public bool? IsActive { get; set; }
        public bool? TwoFactorEnabled { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
