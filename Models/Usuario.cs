using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VidaFitBackend.Models
{
    [Table("usuarios")]
    public class Usuario
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("nombre")]
        [Required]
        [MaxLength(100)]
        public string? Nombre { get; set; }

        [Column("email")]
        [Required]
        [MaxLength(150)]
        public string Email { get; set; }

        [Column("password_hash")]
        [Required]
        public string PasswordHash { get; set; }

        [Column("rol")]
        [MaxLength(20)]
        public string Rol { get; set; } = "admin";

        [Column("telefono")]
        [MaxLength(20)]
        public string? Telefono { get; set; }

        [Column("activo")]
        public bool Activo { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}